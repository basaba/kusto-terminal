using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using System.Text.Json;
using KustoTerminal.Core.Models;
using KustoTerminal.Core.Services;
using KustoTerminal.Language.Services;
using KustoTerminal.UI;
using KustoTerminal.UI.Panes;
using KustoTerminal.UI.Services;

if (args.FirstOrDefault() == "--socket-peer")
{
    if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
    await LinuxSocketTests.RunPeerAsync(args[1], args[2]);
    return;
}
if (args.FirstOrDefault() == "--interop-client")
{
    using var channel = await ManagedTokenChannel.ConnectAsync(ManagedShellOptions.Parse(args[1..])!);
    using var session = new ManagedSession(channel);
    for (var i = 0; i < 2; i++)
    {
        var builder = await session.CreateConnectionStringBuilderAsync(session.CreateConnection());
        if (!builder.FederatedSecurity || builder.DstsFederatedSecurity || string.IsNullOrEmpty(builder.UserToken))
            throw new Exception("Interop MSAL SDK mode failed.");
    }
    Console.WriteLine("PASS: production transport, two acquisitions, fixed-target MSAL SDK builders (no tokens logged).");
    return;
}
if (OperatingSystem.IsLinux())
    await LinuxSocketTests.RunAsync();

using var fixtures = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "wire-fixtures.json")));
if (args.SequenceEqual(new[] { "--emit-protocol-fixtures" }))
{
    var frames = fixtures.RootElement.EnumerateObject().ToDictionary(p => p.Name, p =>
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(p.Value);
        var frame = new byte[payload.Length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
        payload.CopyTo(frame, 4);
        return Convert.ToBase64String(frame);
    });
    Console.WriteLine(JsonSerializer.Serialize(frames));
    return;
}

// No test framework dependency: a failed assertion exits nonzero.
if (typeof(KustoClient).Assembly.GetType("KustoTerminal.Core.Services.ManagedShellOptions") == null)
    throw new Exception("Fixed-target managed shell capability is missing.");

var args0 = new[] { "--shell-managed", "--cluster", "https://example.kusto.windows.net",
    "--database", "db", "--tenant", "opaque-tenant", "--shell-pipe",
    "kt-11111111-2222-3333-4444-555555555555", "--shell-parent-pid", "123" };
var options = ManagedShellOptions.Parse(args0)!;
foreach (var name in new[] { "MsalResponse", "DstsResponse", "ErrorResponse" })
{
    using var stream = new ParentStream((id, _) =>
        fixtures.RootElement.GetProperty(name).GetRawText().Replace("fixture-1", id));
    using var channel = new ManagedTokenChannel(stream, options);
    if (name == "ErrorResponse" || (name == "DstsResponse" && !OperatingSystem.IsWindows()))
        await Reject(() => channel.AcquireAsync(), "error fixture");
    else
    {
        var credential = await channel.AcquireAsync();
        Check((int)credential.Scheme == fixtures.RootElement.GetProperty(name).GetProperty("Scheme").GetInt32(),
            "wire fixture scheme");
    }
}
Check(ManagedShellOptions.Parse(["--fps"]) == null, "standalone unchanged");
Check(options.ClusterUri == args0[2] && options.Database == "db", "fixed target parsed");
foreach (var invalid in new[] {
    args0.Concat(new[] { "--fps" }).ToArray(),
    args0.Concat(new[] { "--cluster", args0[2] }).ToArray(),
    args0.Skip(1).ToArray(),
    Replace(args0, args0[2], "http://example.test"),
    Replace(args0, args0[2], "https://example.test/path"),
    Replace(args0, args0[2], "https://user@example.test"),
    Replace(args0, args0[2], "https://example.test?x"),
    Replace(args0, args0[2], "https://example.test/#x"),
    Replace(args0, "123", "0"),
    Replace(args0, args0[8], "../pipe"),
    Replace(args0, "db", " "),
    Replace(args0, "opaque-tenant", "") })
    await Reject(() => Task.FromResult(ManagedShellOptions.Parse(invalid)), "bad CLI");

var now = DateTimeOffset.UtcNow;
string Response(string id, int scheme = 0, string token = "synthetic-token", DateTimeOffset? expiry = null,
    string error = "") => JsonSerializer.Serialize(new {
        Version = 1, RequestId = id, Scheme = scheme, AccessToken = token,
        ExpiresOn = expiry ?? now.AddHours(1), ErrorCode = error });

using (var stream = new ParentStream((id, n) => Response(id, OperatingSystem.IsWindows() ? n % 2 : 0, $"synthetic-{n}")))
using (var channel = new ManagedTokenChannel(stream, options))
using (var session = new ManagedSession(channel))
{
    var connection = session.CreateConnection();
    var first = await session.CreateConnectionStringBuilderAsync(connection);
    var second = await session.CreateConnectionStringBuilderAsync(connection);
    Check(first.UserToken == "synthetic-0" && first.FederatedSecurity && !first.DstsFederatedSecurity,
        "MSAL SDK mode");
    Check(second.UserToken == "synthetic-1" && second.DstsFederatedSecurity == OperatingSystem.IsWindows() &&
        second.FederatedSecurity != OperatingSystem.IsWindows(), "renewed OS-appropriate SDK mode");
    Check(first.InitialCatalog == "db" && first.DataSource == options.ClusterUri, "builder target");
    await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => session.CreateConnectionStringBuilderAsync(connection)));
    Check(stream.Requests == 10 && stream.MaximumConcurrent == 1, "serialized renewal");
    connection.Database = "other";
    await Reject(() => session.CreateConnectionStringBuilderAsync(connection), "database mutation");
    connection = session.CreateConnection();
    connection.ClusterUri = "https://other.test";
    await Reject(() => session.CreateConnectionStringBuilderAsync(connection), "cluster mutation");
    Check(stream.Requests == 10, "reject target before requesting credential");
    await Reject(() => Task.FromResult(new KustoClient(connection, session)), "client target mutation");
    var client = new KustoClient(session.CreateConnection(), session);
    try
    {
        Check((await client.GetDatabasesAsync()).SequenceEqual(new[] { "db" }), "no database discovery");
        Check((await client.GetTablesAsync("other")).Length == 0, "table target guard");
        Check(await client.GetDatabaseSchemaAsync("other") == null, "schema target guard");
        Check((await client.GetTableSchemasViaQueryAsync("other", ["Table"])).Count == 0,
            "table backfill target guard");
        Check((await client.GetFunctionSchemasViaQueryAsync("other", [])).Count == 0,
            "function backfill target guard");
        Check(stream.Requests == 10, "no credentials for alternate database");
    }
    finally { client.Dispose(); }
    await Reject(() => Task.FromResult(AuthenticationProviderFactory.CreateProvider(AuthenticationType.ManagedShell)),
        "no standalone authentication fallback");
    var stored = (await session.GetConnectionsAsync()).Single();
    stored.Database = "other";
    Check((await session.GetConnectionsAsync()).Single().Database == "db", "defensive connection copy");
    await Reject(() => session.AddConnectionAsync(stored), "no added connection");
    await Reject(() => session.UpdateConnectionAsync(stored), "no updated connection");
    await Reject(() => session.DeleteConnectionAsync(stored.Id), "no deleted connection");
    await session.SaveLastQueryAsync("sensitive query");
    await session.SaveTabsAsync([new TabState()]);
    await session.SaveSettingsAsync(new UserSettings { LastQuery = "sensitive query" });
    await session.SaveConnectionsAsync();
    await session.LoadConnectionsAsync();
    Check(await session.GetLastQueryAsync() == "" && (await session.GetTabsAsync()).Count == 0,
        "query and tabs not retained");
    Check((await session.LoadSettingsAsync()).LastQuery == "", "settings not retained");
    Check(await session.GetLatestAsync(session.CreateConnection(), "query") == null, "no history");
}

foreach (var response in new Func<string, string>[] {
    id => Response("wrong"), id => Response(id, 2), id => Response(id, token: ""),
    id => Response(id, token: "token with spaces"), id => Response(id, token: "token\n"),
    id => Response(id, expiry: now.AddMinutes(2)), id => Response(id, expiry: now.AddSeconds(-1)),
    id => Response(id, error: "authentication-failed"),
    id => Response(id, token: "", expiry: default(DateTimeOffset), error: "authentication-failed"),
    id => Response(id).Replace("\"Version\":1", "\"Version\":2"),
    id => Response(id).Replace("\"Version\":1", "\"Version\":1,\"Version\":1"),
    id => Response(id).Replace("\"Version\":1", "\"Unknown\":1,\"Version\":1"),
    id => Response(id).Replace("\"Version\":1,", ""),
    id => Response(id).Replace("\"Scheme\":0", "\"Scheme\":\"0\""),
    id => Response(id).Replace("\"ErrorCode\":\"\"", "\"ErrorCode\":null"),
    id => fixtures.RootElement.GetProperty("MsalResponse").GetRawText().Replace("fixture-1", id)
        .Replace("2099-01-01T00:00:00+00:00", "2099-01-01T00:00:00"),
    id => "{", id => "[]", id => Response(id, token: new string('x', 65536)) })
{
    using var stream = new ParentStream((id, _) => response(id));
    using var channel = new ManagedTokenChannel(stream, options);
    await Reject(() => channel.AcquireAsync(), "bad response");
    Check(stream.Closed, "invalid response closes channel");
}
foreach (var size in new[] { 0, -1, 65537 })
{
    var prefix = new byte[4];
    BinaryPrimitives.WriteInt32LittleEndian(prefix, size);
    using var stream = new ParentStream((_, _) => "", rawResponse: prefix);
    using var channel = new ManagedTokenChannel(stream, options);
    await Reject(() => channel.AcquireAsync(), "invalid frame size");
}
using (var stream = new ParentStream((_, _) => "", rawResponse: [5, 0, 0, 0, 123]))
using (var channel = new ManagedTokenChannel(stream, options))
    await Reject(() => channel.AcquireAsync(), "truncated frame");
using (var stream = new ParentStream((id, _) => Response(id), stall: true))
using (var channel = new ManagedTokenChannel(stream, options))
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
    await Reject(() => channel.AcquireAsync(cts.Token), "acquisition cancellation");
    Check(stream.Closed, "cancellation closes channel");
}
using (var stream = new ParentStream((id, _) => Response(id), stall: true))
{
    var channel = new ManagedTokenChannel(stream, options);
    var acquisition = channel.AcquireAsync();
    channel.Dispose();
    await Reject(() => acquisition, "dispose cancels pending acquisition");
    await Reject(() => channel.AcquireAsync(), "disposed channel cannot be reused");
    Check(stream.Closed, "dispose closes channel");
}
if (!OperatingSystem.IsWindows())
    await Reject(() => ManagedTokenChannel.ConnectAsync(options), "Windows pipes rejected on other platforms");
using (var stream = new ParentStream((id, _) => Response(id, error: "authentication-failed")))
using (var channel = new ManagedTokenChannel(stream, options))
using (var session = new ManagedSession(channel))
{
    var schema = new ClusterSchemaService(new LanguageService(),
        new CacheConfiguration { EnableDiskCache = true, CacheDirectory = "tests/ManagedShell.Tests/forbidden-cache" },
        session);
    Check(Field(schema, "_cacheManager") == null, "managed schema overrides disk cache");
    using var window = new MainWindow(session, session);
    Check(ReferenceEquals(Field(window, "_executionHistoryManager"), session), "managed history wiring");
    Check(ReferenceEquals(Field(window, "_userSettingsManager"), session), "managed settings wiring");
    var tabs = (TabManagerService)Field(window, "_tabManager")!;
    Check(tabs.ActiveTab!.Connection!.Database == "db", "initial fixed tab");
    Call(window, "CreateNewTab");
    Check(tabs.Tabs.All(t => t.Connection!.Database == "db"), "new fixed tab");
    var pane = (ConnectionPane)Field(window, "_connectionPane")!;
    foreach (var method in new[] { "OnAddClicked", "OnEditClicked", "OnDeleteClicked" })
        Call(pane, method);
    await Reject(() => pane.SelectByClusterUriAsync("https://other.test", "db"), "UI cluster switch");
    await Reject(() => pane.SelectByClusterUriAsync(options.ClusterUri, "other"), "UI database switch");
    const string directive = "#connect cluster('https://other.test').database('other')\nprint 1";
    Check(ConnectDirectiveParser.TryParse(directive, out _, out _, out _, out _), "valid directive fixture");
    await (Task)Call(window, "ExecuteQueryAsync", directive)!;
    Check(tabs.ActiveTab!.Connection!.ClusterUri == options.ClusterUri, "directive cannot retarget");
}
Console.WriteLine("PASS: CLI, framing, validation, renewal, serialization, SDK modes, fixed target, memory-only state, cancellation, UI wiring.");

static object? Field(object instance, string name) => instance.GetType()
    .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance);
static object? Call(object instance, string name, params object[] args) => instance.GetType()
    .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(instance, args);
static string[] Replace(string[] values, string oldValue, string newValue) =>
    values.Select(v => v == oldValue ? newValue : v).ToArray();
static void Check(bool value, string name) { if (!value) throw new Exception(name); }
static async Task Reject(Func<Task> action, string name)
{
    try { await action(); } catch { return; }
    throw new Exception($"Accepted {name}");
}

sealed class ParentStream(Func<string, int, string> response, byte[]? rawResponse = null, bool stall = false) : Stream
{
    readonly MemoryStream request = new();
    MemoryStream reply = new();
    int concurrent;
    public int Requests { get; private set; }
    public int MaximumConcurrent { get; private set; }
    public bool Closed { get; private set; }
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        MaximumConcurrent = Math.Max(MaximumConcurrent, Interlocked.Increment(ref concurrent));
        await Task.Yield();
        var bytes = request.ToArray();
        if (bytes.Length < 4 || BinaryPrimitives.ReadInt32LittleEndian(bytes) != bytes.Length - 4 || bytes.Length - 4 > 4096)
            throw new Exception("invalid request framing");
        using var json = JsonDocument.Parse(bytes.AsMemory(4));
        var root = json.RootElement;
        var names = root.EnumerateObject().Select(p => p.Name).ToArray();
        if (!names.SequenceEqual(new[] { "Version", "RequestId", "ClusterUri" }) ||
            root.GetProperty("Version").GetInt32() != 1 ||
            root.GetProperty("ClusterUri").GetString() != "https://example.kusto.windows.net")
            throw new Exception("invalid request fields or target");
        var id = root.GetProperty("RequestId").GetString()!;
        if (id.Length is < 1 or > 64 || id.Any(c => c < 32 || c > 126))
            throw new Exception("invalid request correlation");
        var payload = Encoding.UTF8.GetBytes(response(id, Requests++));
        var framed = new byte[payload.Length + 4];
        BinaryPrimitives.WriteInt32LittleEndian(framed, payload.Length);
        payload.CopyTo(framed, 4);
        reply = new MemoryStream(rawResponse ?? framed);
        request.SetLength(0);
        Interlocked.Decrement(ref concurrent);
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (stall) await Task.Delay(Timeout.Infinite, cancellationToken);
        // Deliberately fragment every prefix and payload.
        return await reply.ReadAsync(buffer[..Math.Min(3, buffer.Length)], cancellationToken);
    }
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        request.WriteAsync(buffer, cancellationToken);
    protected override void Dispose(bool disposing) { Closed = true; request.Dispose(); reply.Dispose(); base.Dispose(disposing); }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
