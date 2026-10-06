"""Run portable/socket tests and fail-closed CLI startup with monitored application-state paths."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import struct
import subprocess
from concurrent.futures import ThreadPoolExecutor
import uuid

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument("--cli", type=Path, help="Also test a published native CLI executable.")
options = parser.parse_args()
state = root / "tests/ManagedShell.Tests/isolation-state"
socket_directory = root / (".kt-" + uuid.uuid4().hex[:8])
if state.exists():
    raise RuntimeError("Remove the previous isolation-state fixture before running this test.")
state.mkdir()
try:
    for relative in [
        ".config/KustoTerminal/connections.json",
        ".config/KustoTerminal/user-settings.json",
        ".kusto-terminal/cache/schema.json",
        ".kusto-terminal-fps.log",
        ".tui/config.json",
    ]:
        path = state / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("invalid persisted fixture -- must not be read or changed", encoding="utf-8")

    def snapshot():
        return {str(p.relative_to(state)): hashlib.sha256(p.read_bytes()).hexdigest()
                for p in state.rglob("*") if p.is_file()}

    before = snapshot()
    env = dict(os.environ, HOME=str(state), XDG_CONFIG_HOME=str(state / ".config"),
               APPDATA=str(state / ".config"), USERPROFILE=str(state))
    result = subprocess.run(
        ["dotnet", "tests/ManagedShell.Tests/bin/Debug/net8.0/ManagedShell.Tests.dll"],
        cwd=root, env=env, text=True, capture_output=True, timeout=30)
    assert result.returncode == 0, result.stdout + result.stderr
    assert "PASS:" in result.stdout, result.stdout
    print(result.stdout.strip())
    if os.name != "nt":
        cli = ([str(options.cli.resolve())] if options.cli else
               ["dotnet", "src/KustoTerminal.CLI/bin/Debug/net8.0/KustoTerminal.dll"])
        args = ["--shell-managed", "--cluster", "https://example.kusto.windows.net",
                "--database", "db", "--tenant", "opaque", "--shell-pipe",
                "kt-11111111-2222-3333-4444-555555555555", "--shell-parent-pid", "123"]
        for extra in [[], ["--fps"], ["--cluster", "https://other.test"]]:
            result = subprocess.run(cli + args + extra, cwd=root, env=env, text=True,
                                    capture_output=True, timeout=10)
            assert result.returncode != 0 and "Managed shell startup failed" in result.stdout, result
            assert "Starting Terminal UI" not in result.stdout, result.stdout
        if os.uname().sysname == "Linux":
            socket_directory.mkdir(mode=0o700)
            socket_path = socket_directory / "s"
            socket_args = args.copy()
            socket_args[7:9] = ["--shell-socket", str(socket_path)]
            socket_args[-1] = str(os.getpid())
            for mode in ["authentication-error", "dsts"]:
                with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as server:
                    server.bind(str(socket_path))
                    server.listen(1)
                    server.settimeout(10)

                    def respond():
                        with server.accept()[0] as connection:
                            connection.settimeout(10)
                            with connection.makefile("rb") as stream:
                                size = struct.unpack("<i", stream.read(4))[0]
                                assert 0 < size <= 4096, size
                                request = json.loads(stream.read(size))
                            assert request["Version"] == 1 and request["ClusterUri"] == args[2], request
                            response = json.dumps(dict(
                                Version=1, RequestId=request["RequestId"], Scheme=1 if mode == "dsts" else 0,
                                AccessToken="synthetic-do-not-log", ExpiresOn="2099-01-01T00:00:00Z",
                                ErrorCode="" if mode == "dsts" else "authentication-failed")).encode()
                            connection.sendall(struct.pack("<i", len(response)) + response)

                    with ThreadPoolExecutor(max_workers=1) as pool:
                        response = pool.submit(respond)
                        result = subprocess.run(cli + socket_args, cwd=root, env=env, text=True,
                                                capture_output=True, timeout=10)
                        response.result(timeout=10)
                    assert result.returncode != 0 and "Managed shell startup failed" in result.stdout, result
                    assert "Starting Terminal UI" not in result.stdout, result.stdout
                    assert "synthetic-do-not-log" not in result.stdout + result.stderr, result
                socket_path.unlink()
    assert snapshot() == before, "Managed execution changed application state."
    assert not (root / "tests/ManagedShell.Tests/forbidden-cache").exists(), "Schema cache was created."
    print("PASS: no managed application-state writes; wrong transport and rejected Linux socket credentials fail before UI.")
finally:
    shutil.rmtree(state)
    if socket_directory.exists():
        shutil.rmtree(socket_directory)
