using System.Globalization;
using System.Text;

namespace KustoTerminal.Core.Services;

/// <summary>Fixed-target managed-shell protocol v1 capability (Windows or Linux x64 glibc).</summary>
public sealed class ManagedShellOptions
{
    public const string Capability = "fixed-target-managed-shell-v1";
    public string ClusterUri { get; }
    public string Database { get; }
    public string Tenant { get; }
    public string? PipeName { get; }
    public string? SocketPath { get; }
    public int ParentProcessId { get; }

    private ManagedShellOptions(string cluster, string database, string tenant, string? pipe, string? socket, int pid)
    {
        ClusterUri = cluster;
        Database = database;
        Tenant = tenant;
        PipeName = pipe;
        SocketPath = socket;
        ParentProcessId = pid;
    }

    public static ManagedShellOptions? Parse(string[] args)
    {
        var names = new[] { "--cluster", "--database", "--tenant", "--shell-pipe", "--shell-socket", "--shell-parent-pid" };
        if (!args.Contains("--shell-managed"))
        {
            if (args.Any(a => a.StartsWith("--shell-", StringComparison.Ordinal) || names.Contains(a)))
                throw new ArgumentException("Managed shell arguments require --shell-managed.");
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var managed = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--shell-managed" && !managed) { managed = true; continue; }
            if (!names.Contains(args[i]) || i + 1 >= args.Length ||
                !values.TryAdd(args[i], args[++i]))
                throw new ArgumentException("Invalid or duplicate managed shell argument.");
        }
        if (values.Count != 5 || values.ContainsKey("--shell-pipe") == values.ContainsKey("--shell-socket") ||
            !values.ContainsKey("--cluster") || !values.ContainsKey("--database") ||
            !values.ContainsKey("--tenant") || !values.ContainsKey("--shell-parent-pid") ||
            values.Values.Any(v => string.IsNullOrWhiteSpace(v) || v.Any(char.IsControl)))
            throw new ArgumentException("All managed shell arguments are required.");

        var cluster = values["--cluster"];
        if (cluster.Length > 2048 || !Uri.TryCreate(cluster, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 ||
            uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            (cluster != uri.GetLeftPart(UriPartial.Authority) &&
             cluster != uri.GetLeftPart(UriPartial.Authority) + "/"))
            throw new ArgumentException("Managed cluster must be an exact HTTPS origin.");
        values.TryGetValue("--shell-pipe", out var pipe);
        values.TryGetValue("--shell-socket", out var socket);
        if (pipe != null && (!pipe.StartsWith("kt-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(pipe[3..], "D", out _) || pipe.Length != 39))
            throw new ArgumentException("Invalid managed shell pipe.");
        if (socket != null && (!socket.StartsWith('/') || socket.Contains('\\') ||
            socket.Split('/').Skip(1).Any(p => p is "" or "." or "..") ||
            Encoding.UTF8.GetByteCount(socket) > 107))
            throw new ArgumentException("Managed shell socket must be an absolute local filesystem path.");
        if (!int.TryParse(values["--shell-parent-pid"], NumberStyles.None, CultureInfo.InvariantCulture,
                out var pid) || pid <= 0)
            throw new ArgumentException("Invalid managed shell parent process.");
        return new(cluster, values["--database"], values["--tenant"], pipe, socket, pid);
    }
}
