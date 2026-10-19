# Vendored Thoth.Json

Sources of [Thoth.Json](https://github.com/thoth-org/Thoth.Json) by Maxime Mangel, MIT licence ([LICENSE.txt](LICENSE.txt)),
compiled into Ranvier.Wire.

| | |
|---|---|
| Upstream commit | `de668a4a6378245d86a96427cc90c5da23d195e2` ("chore: release multiple projects (#248)"; Thoth.Json.Core 0.10.0, Thoth.Json.JavaScript 0.10.0, Thoth.Json.System.Text.Json 0.10.0 sources) |
| Packages | `Thoth.Json.Core` (both targets), `Thoth.Json.System.Text.Json` (.NET), `Thoth.Json.JavaScript` (Fable), under the namespaces `Ranvier.Wire.Thoth.Json.Core`, `Ranvier.Wire.Thoth.Json.System.Text.Json` and `Ranvier.Wire.Thoth.Json.JavaScript` |

## Patches

Applied in name order by `refresh.ps1`. CI runs `refresh.ps1 -Check`, which fails when a file here differs from the commit plus these patches.

| Patch | Change | Drop when |
|---|---|---|
| `0001-thoth-pr-265-nativeaot.patch` | [thoth-org/Thoth.Json#265](https://github.com/thoth-org/Thoth.Json/pull/265) at `e22fd43`: the two typed interpolations become concatenation and the generic `integral` decoder takes `float` bounds, so both assemblies publish with NativeAOT, rooted, with no IL diagnostic | #265 is released and this directory is replaced by the released packages |
| `0002-target-guards.patch` | wraps the System.Text.Json files in `#if !FABLE_COMPILER` and the JavaScript files in `#if FABLE_COMPILER` | this directory is replaced by package references |
| `0003-linear-list-decode.patch` | `Decode.list` conses and reverses instead of appending, so a list decodes in linear time | the same change is released upstream |
| `0004-ranvier-namespaces.patch` | prefixes `Ranvier.Wire.` to every namespace, `open` and `cref` of `Thoth.Json.Core`, `Thoth.Json.System.Text.Json` and `Thoth.Json.JavaScript`, so an app that also references the upstream packages gets no duplicate types | this directory is replaced by package references (callers then open `Thoth.Json.*`) |
| `0005-obsolete-target-dependent-codecs.patch` | marks `Codec`, `Encode` and `Decode` `bigint`, `decimal` and `datetimeOffset` `[<Obsolete>]`, pointing to `Ranvier.Wire.Codecs`, and adds `#nowarn "44"` to `Codec.fs`, whose obsolete codecs use the obsolete members | this directory is replaced by package references |
| `0006-finite-float-decode.patch` | `Decode.float` returns an error for a number outside the double range (`1e400`) and `Decode.float32` for one outside the single range (`1e39`), instead of ±∞; the strings `"Infinity"` and `"-Infinity"` still decode | the same change is released upstream |

## Changing the vendored files

Edit only through a patch: add or change a file in `patches/`, run `pwsh refresh.ps1`, and review `git diff`. Fantomas and the
repository's comment rules do not apply here (`.fantomasignore`).
