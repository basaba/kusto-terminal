# Kusto Terminal

A terminal-style Azure Data Explorer (Kusto) client, built with .NET 8 and Terminal.Gui.

## Prerequisites

- Azure CLI (`az login` command)

## Installation

1. Clone the repository
2. Build the solution:
   ```bash
   dotnet build
   ```
3. Run the application:
   ```bash
   dotnet run --project src/KustoTerminal.CLI
   ```

## Recall the last execution

Place the cursor in a query and press `F8` to restore its latest successful
result for the active cluster and database. Kusto Terminal loads the result
table and execution metadata from the local cache without sending a request to
Kusto or changing any text in the query editor.

Cached executions are stored under `~/.kusto-terminal/history`, retained for 30
days, and limited to the newest 1,000 entries. Query results can contain
sensitive data, so protect this directory like any other local data export.
## Managed shell integration (Windows and Linux x64 glibc)

An optional fixed-target mode accepts credentials from a parent process rather than
performing login:

```text
KustoTerminal --shell-managed --cluster https://cluster.example --database database --tenant opaque-tenant --shell-pipe kt-11111111-2222-3333-4444-555555555555 --shell-parent-pid 123
```

On Linux, replace `--shell-pipe` with an absolute local Unix-domain socket path:

```text
KustoTerminal --shell-managed --cluster https://cluster.example --database database --tenant opaque-tenant --shell-socket /tmp/kt-11111111-2222-3333-4444-555555555555/s --shell-parent-pid 123
```

All six arguments are required in managed mode, with exactly one transport;
other/duplicate arguments are rejected. Named pipes are Windows-only and sockets
are Linux x64 glibc-only. macOS, ARM and musl managed startup are unsupported;
there is no alternative picker/login fallback. Linux PowerShell parents using
the .NET 8 bridge require PowerShell 7.4 or later.

The cluster must be an exact HTTPS origin (an optional trailing slash
is retained). The tenant is an opaque parent-shell context, not an instruction
to perform local login. The parent must already be listening. The client connects
**before UI initialization**, with a 30-second deadline, verifies its peer, and
authenticates before opening the UI:

* Windows: local `kt-<GUID>` named pipe; `GetNamedPipeServerProcessId` must match
  the supplied PID. The parent should restrict pipe access and verify its child's PID.
* Linux: the parent must atomically create a random private directory with mode
  `0700`, bind its socket there, and authenticate the accepted child's UID and PID.
  The client verifies `SO_PEERCRED` (`SOL_SOCKET=1`, option `17`) against its
  effective UID and the supplied parent PID. The socket path must have at most
  107 UTF-8 bytes, begin with a single `/`, and contain no empty, `.` or `..`
  components, backslashes or control characters. The immediate directory must
  have mode `0700`; symlinks in the socket path are rejected.

Protocol v1 uses UTF-8 JSON with a four-byte little-endian byte-count prefix:
requests are at most 4096 bytes, responses at most 65536 bytes. Property names
are case-sensitive. Unknown, duplicate and missing response fields are rejected.

* Request: `Version: 1`, a fresh printable-ASCII `RequestId` (1–64 characters),
  and `ClusterUri` equal to the supplied origin.
* Response: `Version: 1`, matching `RequestId`, integer `Scheme` (`0` = MSAL,
  `1` = dSTS), `AccessToken`, ISO8601 `ExpiresOn` with an explicit offset or `Z`,
  and `ErrorCode: ""`.
* Authentication failure: `ErrorCode: "authentication-failed"`, empty token,
  and default expiry. All error responses close the channel.

Tokens must be nonempty, contain no whitespace/control characters, and expire
more than two minutes in the future. MSAL credentials use the SDK's AAD user-token
mode; on Windows, dSTS credentials use its dSTS user-token mode. Linux rejects
dSTS responses and closes the channel. Acquisitions are serialized
and bounded to five minutes, including time waiting for another acquisition.
Each operation requests credentials again, allowing the parent to renew them.
Acquisition cancellation, EOF, invalid frames, peer mismatch and disposal fail closed.
There is no login fallback or reconnect to another parent.

The cluster/database are fixed for tabs, queries, schema and cancellation.
Connection creation/edit/delete and `#connect` are disabled; the connection tree
shows only the supplied target. No saved connections, settings, query/result
history, schema-cache files, user configuration or FPS logs are loaded/written
in managed mode. Explicit user actions such as result export remain available.
Ordinary standalone startup is unchanged.

The source capability marker is
`ManagedShellOptions.Capability = "fixed-target-managed-shell-v1"` in
`src/KustoTerminal.Core/Services/ManagedShellOptions.cs`. This is a feature marker,
not redistribution, signing or platform acceptance approval.

Portable checks (existing dependencies only, .NET 8):

```sh
dotnet build KustoTerminal.sln
dotnet run --project tests/ManagedShell.Tests
python3 tests/ManagedShell.Tests/check-isolation.py
python3 tests/ManagedShell.Tests/check-publish-targets.py
dotnet run --project tests/ManagedShell.Tests --no-build -- --emit-protocol-fixtures
```

The last command emits a JSON map of Base64-encoded complete wire frames, based
on public synthetic vectors in `tests/ManagedShell.Tests/wire-fixtures.json`.
Other implementations can decode these fixtures to check framing, casing,
numeric schemes and both ISO8601 offset forms. Tokens and target are synthetic.
The executable tests exercise the production channel over fragmented streams,
renewal, rejection cases, SDK builders, fixed-target UI wiring and memory-only
state. On Linux they also use real sockets and separate peer processes to test
PID binding, renewal, fragmented frames, disconnection, cancellation, path safety
and dSTS rejection. The foreign-UID listener test runs when the test process has
effective UID 0; it can also run without host root on Linux systems that support
subordinate UID mappings:

```sh
unshare --user --map-auto --map-root-user dotnet tests/ManagedShell.Tests/bin/Debug/net8.0/ManagedShell.Tests.dll
```

For a parent implementation's integration test, launch the test executable with
`--interop-client` followed by the normal managed arguments. It uses the production
connector and two MSAL SDK acquisitions without opening the UI, making Kusto
requests, persisting state or logging tokens. The supplied PID must be that of
the actual socket listener.

### Publishing managed terminals

Publish each RID sequentially into separate directories (including all published
dependencies). Explicit RIDs govern framework/Windows clipboard selection even
when the build host uses the other OS:

```sh
dotnet publish src/KustoTerminal.CLI/KustoTerminal.CLI.csproj -c Release -r linux-x64 -p:TargetFramework=net8.0 --self-contained true -o artifacts/Terminal/linux-x64
dotnet publish src/KustoTerminal.CLI/KustoTerminal.CLI.csproj -c Release -r win-x64 -p:TargetFramework=net8.0-windows -p:EnableWindowsTargeting=true --self-contained true -o artifacts/Terminal/win-x64
```

The Linux apphost is `KustoTerminal`; Windows uses `KustoTerminal.exe`. Linux
deployment/archive tooling must preserve or restore its executable permission
(`chmod 755 Terminal/linux-x64/KustoTerminal`), especially when packaging on
Windows. The Linux SDK publish sets this permission automatically. Test the native
Linux output with:

```sh
python3 tests/ManagedShell.Tests/check-isolation.py --cli artifacts/Terminal/linux-x64/KustoTerminal
```

These checks do not prove native Windows pipe PID binding, live authentication,
signed deployment or SAW acceptance; those require platform integration testing.
