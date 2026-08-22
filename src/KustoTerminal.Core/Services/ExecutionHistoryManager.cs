using System.Data;
using System.IO.Compression;
using KustoTerminal.Core.Interfaces;
using KustoTerminal.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KustoTerminal.Core.Services;

public sealed class ExecutionHistoryManager : IExecutionHistoryManager, IDisposable
{
    private const string IndexFileName = "history.json";
    private const string PayloadExtension = ".result.json.gz";

    private static readonly IReadOnlyDictionary<string, Type> s_supportedTypes =
        new[]
        {
            typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float),
            typeof(double), typeof(decimal), typeof(string), typeof(char),
            typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(Guid),
            typeof(byte[])
        }.ToDictionary(type => type.FullName!, type => type, StringComparer.Ordinal);

    private readonly ExecutionHistoryConfiguration _configuration;
    private readonly string _indexFilePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializer _serializer = JsonSerializer.CreateDefault();
    private List<QueryHistory> _entries = new();
    private bool _initialized;
    private bool _disposed;

    public ExecutionHistoryManager(ExecutionHistoryConfiguration? configuration = null)
    {
        _configuration = configuration ?? new ExecutionHistoryConfiguration();

        if (string.IsNullOrWhiteSpace(_configuration.HistoryDirectory))
            throw new ArgumentException("History directory cannot be empty.", nameof(configuration));
        if (_configuration.RetentionDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(configuration), "Retention days must be positive.");
        if (_configuration.RetentionEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(configuration), "Retention entries must be positive.");

        _indexFilePath = Path.Combine(_configuration.HistoryDirectory, IndexFileName);
    }

    public async Task AppendAsync(
        KustoConnection connection,
        string query,
        QueryResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess || string.IsNullOrWhiteSpace(query) || result.Data == null)
            return;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            EnsureInitialized();

            var payloadFileName = $"{result.ExecutedAt:yyyy-MM-dd--HH-mm-ss}-{Guid.NewGuid():N}{PayloadExtension}";
            var payloadPath = Path.Combine(_configuration.HistoryDirectory, payloadFileName);
            var entry = new QueryHistory
            {
                Query = query,
                ClusterUri = NormalizeClusterUri(connection.ClusterUri),
                Database = NormalizeDatabase(connection.Database),
                ExecutedAt = result.ExecutedAt,
                IsCommand = query.TrimStart().StartsWith(".", StringComparison.Ordinal),
                DurationTicks = result.Duration.Ticks,
                ClientRequestId = result.ClientRequestId,
                ResultFileName = payloadFileName
            };

            WritePayloadAtomically(payloadPath, ResultPayload.FromDataTable(result.Data, _serializer));

            var proposedEntries = _entries
                .Append(entry)
                .OrderBy(item => item.ExecutedAt)
                .ToList();
            var (retainedEntries, removedEntries) = ApplyRetention(proposedEntries);

            try
            {
                WriteIndexAtomically(retainedEntries);
            }
            catch
            {
                TryDeleteFile(payloadPath);
                throw;
            }

            _entries = retainedEntries;
            DeletePayloads(removedEntries);
            DeleteOrphanedPayloads();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QueryResult?> GetLatestAsync(
        KustoConnection connection,
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query))
            return null;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            EnsureInitialized();

            var clusterUri = NormalizeClusterUri(connection.ClusterUri);
            var database = NormalizeDatabase(connection.Database);
            var candidates = _entries
                .Where(entry =>
                    string.Equals(entry.ClusterUri, clusterUri, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entry.Database, database, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entry.Query.Trim(), query.Trim(), StringComparison.Ordinal))
                .OrderByDescending(entry => entry.ExecutedAt)
                .ToList();

            foreach (var entry in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? payloadPath = null;

                try
                {
                    payloadPath = GetPayloadPath(entry.ResultFileName);
                    var payload = ReadPayload(payloadPath);
                    return new QueryResult
                    {
                        IsSuccess = true,
                        Query = entry.Query,
                        Data = payload.ToDataTable(_serializer),
                        Duration = TimeSpan.FromTicks(entry.DurationTicks),
                        ExecutedAt = entry.ExecutedAt,
                        ClientRequestId = entry.ClientRequestId,
                        IsCached = true,
                        CachedAt = entry.ExecutedAt
                    };
                }
                catch (Exception ex) when (
                    ex is IOException
                    or UnauthorizedAccessException
                    or InvalidDataException
                    or JsonException
                    or InvalidOperationException
                    or ArgumentException
                    or FormatException
                    or OverflowException)
                {
                    Console.WriteLine($"Warning: Ignoring corrupt execution history entry '{entry.Id}': {ex.Message}");
                    _entries.Remove(entry);
                    WriteIndexAtomically(_entries);
                    if (payloadPath != null)
                        TryDeleteFile(payloadPath);
                }
            }

            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureInitialized()
    {
        if (_initialized)
            return;

        Directory.CreateDirectory(_configuration.HistoryDirectory);
        RestrictPermissions(_configuration.HistoryDirectory, isDirectory: true);

        if (File.Exists(_indexFilePath))
        {
            try
            {
                _entries = JsonConvert.DeserializeObject<List<QueryHistory>>(
                    File.ReadAllText(_indexFilePath)) ?? new List<QueryHistory>();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                Console.WriteLine($"Warning: Failed to load execution history index: {ex.Message}");
                _entries = new List<QueryHistory>();
            }
        }

        var (retainedEntries, removedEntries) = ApplyRetention(_entries);
        _entries = retainedEntries;
        WriteIndexAtomically(_entries);
        DeletePayloads(removedEntries);
        DeleteOrphanedPayloads();
        _initialized = true;
    }

    private (List<QueryHistory> Retained, List<QueryHistory> Removed) ApplyRetention(
        List<QueryHistory> entries)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_configuration.RetentionDays);
        var retained = entries
            .Where(entry => entry.ExecutedAt >= cutoff)
            .OrderByDescending(entry => entry.ExecutedAt)
            .Take(_configuration.RetentionEntries)
            .OrderBy(entry => entry.ExecutedAt)
            .ToList();
        var retainedIds = retained.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        var removed = entries.Where(entry => !retainedIds.Contains(entry.Id)).ToList();
        return (retained, removed);
    }

    private void WriteIndexAtomically(List<QueryHistory> entries)
    {
        var tempPath = GetTempPath(_indexFilePath);
        try
        {
            File.WriteAllText(tempPath, JsonConvert.SerializeObject(entries, Formatting.Indented));
            RestrictPermissions(tempPath, isDirectory: false);
            File.Move(tempPath, _indexFilePath, overwrite: true);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private void WritePayloadAtomically(string payloadPath, ResultPayload payload)
    {
        var tempPath = GetTempPath(payloadPath);
        try
        {
            using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var gzip = new GZipStream(file, CompressionLevel.Optimal))
            using (var writer = new StreamWriter(gzip))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                _serializer.Serialize(jsonWriter, payload);
            }

            RestrictPermissions(tempPath, isDirectory: false);
            File.Move(tempPath, payloadPath);
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    private ResultPayload ReadPayload(string payloadPath)
    {
        using var file = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        using var jsonReader = new JsonTextReader(reader);
        return _serializer.Deserialize<ResultPayload>(jsonReader)
            ?? throw new InvalidDataException("Execution history payload was empty.");
    }

    private void DeletePayloads(IEnumerable<QueryHistory> entries)
    {
        foreach (var entry in entries)
        {
            try
            {
                TryDeleteFile(GetPayloadPath(entry.ResultFileName));
            }
            catch (InvalidDataException ex)
            {
                Console.WriteLine($"Warning: Ignoring invalid execution history filename: {ex.Message}");
            }
        }
    }

    private void DeleteOrphanedPayloads()
    {
        var retainedFiles = _entries
            .Select(entry => entry.ResultFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in Directory.EnumerateFiles(
                     _configuration.HistoryDirectory, $"*{PayloadExtension}"))
        {
            if (!retainedFiles.Contains(Path.GetFileName(filePath)))
                TryDeleteFile(filePath);
        }
    }

    private static string NormalizeClusterUri(string clusterUri)
    {
        if (!Uri.TryCreate(clusterUri, UriKind.Absolute, out var uri))
            return clusterUri.Trim().TrimEnd('/').ToLowerInvariant();

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/').ToLowerInvariant();
    }

    private static string NormalizeDatabase(string database) =>
        database.Trim().ToLowerInvariant();

    private static string GetTempPath(string targetPath) =>
        $"{targetPath}.{Guid.NewGuid():N}.tmp";

    private string GetPayloadPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
            || !fileName.EndsWith(PayloadExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"'{fileName}' is not a valid result payload filename.");
        }

        return Path.Combine(_configuration.HistoryDirectory, fileName);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Warning: Failed to delete execution history file '{path}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Warning: Failed to delete execution history file '{path}': {ex.Message}");
        }
    }

    private static void RestrictPermissions(string path, bool isDirectory)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            var mode = isDirectory
                ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                : UnixFileMode.UserRead | UnixFileMode.UserWrite;
            File.SetUnixFileMode(path, mode);
        }
        catch (PlatformNotSupportedException)
        {
            // The current platform does not expose Unix permissions.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _gate.Dispose();
    }

    private sealed class ResultPayload
    {
        public List<ResultColumn> Columns { get; set; } = new();
        public List<List<JToken>> Rows { get; set; } = new();

        public static ResultPayload FromDataTable(DataTable table, JsonSerializer serializer)
        {
            var payload = new ResultPayload
            {
                Columns = table.Columns.Cast<DataColumn>()
                    .Select(column => new ResultColumn
                    {
                        Name = column.ColumnName,
                        TypeName = GetSupportedTypeName(column.DataType)
                    })
                    .ToList()
            };

            foreach (DataRow row in table.Rows)
            {
                payload.Rows.Add(table.Columns.Cast<DataColumn>()
                    .Select(column => row.IsNull(column)
                        ? JValue.CreateNull()
                        : JToken.FromObject(row[column], serializer))
                    .ToList());
            }

            return payload;
        }

        public DataTable ToDataTable(JsonSerializer serializer)
        {
            if (Columns == null || Rows == null)
                throw new InvalidDataException("Execution history payload is missing its schema or rows.");

            var table = new DataTable();
            foreach (var column in Columns)
                table.Columns.Add(column.Name, GetSupportedType(column.TypeName));

            foreach (var serializedRow in Rows)
            {
                if (serializedRow == null || serializedRow.Count != Columns.Count)
                    throw new InvalidDataException("Execution history row does not match its schema.");

                var row = table.NewRow();
                for (var index = 0; index < Columns.Count; index++)
                {
                    var token = serializedRow[index];
                    row[index] = token.Type is JTokenType.Null or JTokenType.Undefined
                        ? DBNull.Value
                        : token.ToObject(GetSupportedType(Columns[index].TypeName), serializer)
                          ?? DBNull.Value;
                }
                table.Rows.Add(row);
            }

            return table;
        }

        private static string GetSupportedTypeName(Type type)
        {
            var typeName = type.FullName;
            if (typeName == null || !s_supportedTypes.ContainsKey(typeName))
                throw new InvalidOperationException(
                    $"Result column type '{type.FullName ?? type.Name}' is not supported by execution history.");
            return typeName;
        }

        private static Type GetSupportedType(string typeName)
        {
            if (!s_supportedTypes.TryGetValue(typeName, out var type))
                throw new InvalidDataException(
                    $"Execution history column type '{typeName}' is not supported.");
            return type;
        }
    }

    private sealed class ResultColumn
    {
        public string Name { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
    }
}
