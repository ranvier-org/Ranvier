import pathlib
import subprocess
import tempfile
import json
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
    assert "let ``files`` (" in text
    assert "let filesStatic" not in text
    assert "let formStatic" not in text
    coverage = json.loads(first.with_suffix(".coverage.json").read_text())
    assert len(coverage["tags"]) > 100, "full HTML tag map must be generated"
    assert any(tag["tag"] == "br" and tag["isVoid"] for tag in coverage["tags"])
    assert all("let ``" + tag + "`` " in text for tag in ["div", "button", "input", "select", "option", "textarea", "img", "br", "article"])
    assert "let onClick " in text and "Dom.MouseEvent -> unit" in text
    assert first.with_suffix(".coverage.json").read_bytes() == second.with_suffix(".coverage.json").read_bytes()
    assert any("unsupported type" in item["reason"] for item in coverage["omissions"])
    assert coverage["attributeCount"] == 34
    assert coverage["attributePolicySha256"]
    assert "module Attrs =" in text and "let classReactive" in text
    assert "Typed.reactiveBooleanAttribute \"disabled\"" in text
    assert "Typed.reactiveAriaBooleanAttribute \"aria-expanded\"" in text
    assert "let innerHTMLStatic" not in text and "let outerHTMLStatic" not in text
    tampered = pathlib.Path(folder) / "tampered"
    shutil.copytree(root / "bin/Release/net10.0/xantham", tampered / "xantham")
    payload = tampered / "xantham/declarations.json.br"
    data = bytearray(payload.read_bytes())
    data[-1] ^= 1
    payload.write_bytes(data)
    result = subprocess.run(["rtk", "dotnet", "run", "--project", str(root / "Ranvier.Dom.Generate.fsproj"), "-c", "Release", "--no-build", "--", str(first), str(tampered)], capture_output=True, text=True)
    assert result.returncode != 0 and "Catalogue hash mismatch" in result.stderr, result.stdout + result.stderr
    assert first.read_bytes() == second.read_bytes(), "rejection must preserve existing output"
    policy_path = root / "attributes.json"
    original_policy = policy_path.read_bytes()
    try:
        for invalid_name in ("", "1title", "-title", "title\u0661"):
            policy = json.loads(original_policy)
            policy["groups"][0]["entries"][0]["name"] = invalid_name
            policy_path.write_text(json.dumps(policy), encoding="utf-8")
            result = subprocess.run(["rtk", "dotnet", "run", "--project", str(root / "Ranvier.Dom.Generate.fsproj"), "-c", "Release", "--no-build", "--", str(first)], capture_output=True, text=True)
            assert result.returncode != 0 and "Invalid attribute policy name" in result.stderr, result.stdout + result.stderr
            assert first.read_bytes() == second.read_bytes(), "invalid policy must preserve existing output"
    finally:
        policy_path.write_bytes(original_policy)
print("PASS: deterministic full HTML output and coverage, typed native members/events, policy exclusions, corrupt catalogue rejection")
