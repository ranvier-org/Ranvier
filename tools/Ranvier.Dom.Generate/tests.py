import pathlib
import subprocess
import tempfile
import shutil

root = pathlib.Path(__file__).resolve().parent
with tempfile.TemporaryDirectory() as folder:
    first = pathlib.Path(folder) / "first.fs"
    second = pathlib.Path(folder) / "second.fs"
    for target in (first, second):
        result = subprocess.run(["rtk", "dotnet", "run", "--project", str(root / "Ranvier.Dom.Generate.fsproj"), "-c", "Release", "--", str(target)], capture_output=True, text=True)
        assert result.returncode == 0, result.stdout + result.stderr
    assert first.read_bytes() == second.read_bytes(), "regeneration must be byte-identical"
    text = first.read_text()
    assert "Dom.HTMLInputElement" in text
    assert "Dom.InputEvent -> unit" in text
    assert "Dom.FileList option" in text
    assert "let files (" in text
    assert "let filesStatic" not in text
    assert "let formStatic" not in text
    assert all("let " + tag + " " in text for tag in ["div", "button", "input", "select", "option"])
    tampered = pathlib.Path(folder) / "tampered"
    shutil.copytree(root / "bin/Release/net10.0/xantham", tampered / "xantham")
    payload = tampered / "xantham/declarations.json.br"
    data = bytearray(payload.read_bytes())
    data[-1] ^= 1
    payload.write_bytes(data)
    result = subprocess.run(["rtk", "dotnet", "run", "--project", str(root / "Ranvier.Dom.Generate.fsproj"), "-c", "Release", "--no-build", "--", str(first), str(tampered)], capture_output=True, text=True)
    assert result.returncode != 0 and "Catalogue hash mismatch" in result.stderr, result.stdout + result.stderr
    assert first.read_bytes() == second.read_bytes(), "rejection must preserve existing output"
print("PASS: deterministic output, native event/files types, five tags, suppressed files/form setters, corrupt catalogue rejection")
