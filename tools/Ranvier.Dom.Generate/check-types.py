"""Compile valid generated usage and reject incompatible/readonly examples."""
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[2]
project = root / "examples/Fable.Ranvier.Playground/tests/DomTests.fsproj"
prefix = """module GeneratedTypeCheck
open Fable.Core.TS
open Ranvier.View.Foundation.Representation
module H = Ranvier.View.Foundation.Generated.Html
"""
examples = [
    ("valid helpers", "let view = H.div Tree.ops [] [ H.input Tree.ops [ H.Input.checkedStatic true ]; H.select Tree.ops [] [ H.option Tree.ops [] [] ] ]", None),
    ("input modifier on div", "let view = H.div Tree.ops [ H.Input.checkedStatic true ] []", "FS0001"),
    ("wrong property value", 'let modifier = H.Input.checkedStatic "true"', "FS0001"),
    ("readonly native property", "let write (node: Dom.HTMLInputElement) = node.form <- None", "FS0810"),
    ("files setter excluded by policy", "let modifier = H.Input.filesStatic None", "FS0039"),
]

with tempfile.TemporaryDirectory(prefix="ranvier-generated-types-") as directory:
    path = Path(directory)
    check = path / "Check.fs"
    fsproj = path / "Check.fsproj"
    fsproj.write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><GenerateDocumentationFile>false</GenerateDocumentationFile></PropertyGroup>
  <ItemGroup><Compile Include="Check.fs" /><ProjectReference Include="{project.as_posix()}" /><PackageReference Update="FSharp.Core" Version="10.1.401" /></ItemGroup>
</Project>
''', encoding="utf-8")
    check.write_text(prefix + examples[0][1] + "\n", encoding="utf-8")
    restored = subprocess.run(["rtk", "proxy", "dotnet", "restore", str(fsproj), "--configfile", str(root / "NuGet.Config")], capture_output=True, text=True)
    if restored.returncode:
        raise RuntimeError(restored.stdout + restored.stderr)
    for name, body, diagnostic in examples:
        check.write_text(prefix + body + "\n", encoding="utf-8")
        result = subprocess.run(["rtk", "proxy", "dotnet", "build", str(fsproj), "--no-restore", "--configuration", "Release"], capture_output=True, text=True)
        output = result.stdout + result.stderr
        if diagnostic is None:
            if result.returncode:
                raise AssertionError(f"{name} failed:\n{output}")
        elif result.returncode == 0 or f"error {diagnostic}" not in output:
            raise AssertionError(f"{name} did not fail with {diagnostic}:\n{output}")
        print(f"PASS {name}")
