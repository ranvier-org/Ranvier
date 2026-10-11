# Narrow DOM generator

Run from the repository root:

```powershell
rtk npm ci --prefix tools/Ranvier.Dom.Generate --ignore-scripts
rtk dotnet run --project tools/Ranvier.Dom.Generate/Ranvier.Dom.Generate.fsproj -c Release
rtk proxy python tools/Ranvier.Dom.Generate/tests.py
rtk proxy python tools/Ranvier.Dom.Generate/check-types.py
```

The tool's own project restores published Cloudsmith `Xantham.Fable.Core.TS` 0.2.1 and `Xantham.TypeScript.Wire` 0.3.0. Its npm lock pins TypeScript 7.1.0-dev.20260902.1, revision `43a90f4c105bc9db7cb7aa299beddafbabe1d23e`. Generation reads installed packages; it never reads a sibling Xantham checkout. Output is formatted by pinned `Fantomas.Core` 7.0.5 and written as UTF-8 with LF endings. An optional first argument changes the output file, and a second argument selects a directory containing `xantham/catalogue.json` and `xantham/declarations.json.br`, useful for failure tests.

The generator checks descriptor/package/schema identity, the pinned Brotli payload and native assembly hashes, compatibility versions, compiler version/revision and every TypeScript library input hash from the catalogue. Brotli decoding is bounded at 128 MiB. Wire resolves the tag and event maps, members and readonly flags. Compiler declaration handles are normalized to the catalogue's package/version/file/position/kind handles, then resolved to one existing native binding. The native assembly supplies the final F# representation of selected properties, including optional references. Each reflected named type must resolve through the catalogue; unsupported shapes and missing/ambiguous identities stop generation.

The selected vocabulary is policy: five tags, inherited `title`, relevant `value`, input `checked`, `files` and readonly `form` getters, and typed input listeners. `files` is writable upstream; Ranvier intentionally emits only its getter. `form` is readonly according to Wire and receives no setter. This tool does not infer HTML attribute or live form behavior from declarations; generated setters/listeners delegate to handwritten `Typed` runtime operations. It is a narrow adapter, not a general TypeScript-to-F# mapper or complete DOM catalogue authenticator.

Published artifact evidence inspected on 2026-10-11:

| Artifact | SHA-256 |
| --- | --- |
| [Core.TS 0.2.1 nupkg](https://dl.cloudsmith.io/public/shayanhabibi/xantham/nuget/Xantham.Fable.Core.TS/0.2.1/Xantham.Fable.Core.TS.0.2.1.nupkg) | `f6d5cc77a76e14f503e93998c56496757a1fb9343f5df8e1dcf29fdc775e97ad` |
| `xantham/catalogue.json` | `40d670b86bdb994b6108ae271cee1441215f04893b6d820e5d39075895f73942` |
| `xantham/declarations.json.br` | `ad2722747e79381c6ec729db17d96c9eb1046d68627099cb63986942563687c8` |
| `lib/net8.0/Xantham.Fable.Core.TS.dll` | `663021de210d5f4a4ae4b9edbbd71d850343edf1243a16bb322634051ffe0450` |
| Wire 0.3.0 nupkg | `fc2d451b0137dc73308aa8b3b7b2c3e47c09d8a7376f16e9e61378ac291eba24` |
| Compiler `lib/lib.dom.d.ts` | `d6b1eba8496bdd0eed6fc8a685768fe01b2da4a0388b5fe7df558290bffcf32f` |

Core.TS nuspec repository commit: `0d9626867755b04d294c4313cd8a1ec299ccf543`. The descriptor is schema 1, declarations schema 2, with 4,062 declarations, contract 1 / identity 5 / API 4 / inference 2 / customization 1 / AST protocol 8. Generator 0.2.0 remains published but is unnecessary for this tool. Its exported assembly API was inspected in SageFs: no public standalone package catalogue loader/resolver is available. Explicit pinned paths and the narrow owned reader avoid relying on that absent API.
