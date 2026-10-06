"""Evaluate real MSBuild target settings for both publishing hosts; no XML text assertions."""
import json
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[2]
for host in ["Unix", "Windows_NT"]:
    for rid, framework in [("linux-x64", "net8.0"), ("win-x64", "net8.0-windows")]:
        for project in ["CLI", "UI"]:
            for explicit_framework in [False, True]:
                command = [
                    "dotnet", "msbuild", f"src/KustoTerminal.{project}/KustoTerminal.{project}.csproj",
                    "-nologo", f"-p:OS={host}", f"-p:RuntimeIdentifier={rid}",
                    "-getProperty:TargetFramework,UseWindowsForms,DefineConstants",
                    "-getItem:FrameworkReference",
                ]
                if explicit_framework:
                    command.append(f"-p:TargetFramework={framework}")
                result = subprocess.run(command, cwd=root, check=True, text=True, capture_output=True)
                settings = json.loads(result.stdout)
                properties = settings["Properties"]
                if project == "CLI" or explicit_framework:
                    assert properties["TargetFramework"] == framework, (command, properties)
                if project == "UI":
                    references = [item["Identity"] for item in settings["Items"]["FrameworkReference"]]
                    windows = rid == "win-x64"
                    assert ("WINDOWS" in properties["DefineConstants"].split(";")) == windows, (command, properties)
                    assert ("Microsoft.WindowsDesktop.App.WindowsForms" in references) == windows, (command, references)
                    if not windows:
                        assert properties["UseWindowsForms"].lower() != "true", (command, properties)
        # The SDK initially queries libraries without a RID to decide whether to pass it to their builds.
        result = subprocess.run([
            "dotnet", "msbuild", "src/KustoTerminal.UI/KustoTerminal.UI.csproj", "-nologo",
            f"-p:OS={host}", "-t:GetTargetFrameworks", "-getItem:_ThisProjectBuildMetadata",
        ], cwd=root, check=True, text=True, capture_output=True)
        metadata = json.loads(result.stdout)["Items"]["_ThisProjectBuildMetadata"]
        assert len(metadata) == 1 and metadata[0]["IsRidAgnostic"] == "false", metadata
print("PASS: both RIDs select host-independent frameworks, Windows defines and Forms references.")
