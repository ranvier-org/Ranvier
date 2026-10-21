# Arms.Match: measurements

Measurements of the `Arms.Match` implementation ([spec](../superpowers/specs/2026-10-21-arms-match-design.md),
[plan](../superpowers/plans/2026-10-21-arms-match.md)). Fickl 0.3.0 (Fable 5.22), net10.0, Node 26; the Phase 1 discovery ran under Fable 5.18.0 and again under Fickl.

## Plugin discovery

From `experiments/Ranvier.View.Arms/Checks/Discovery`, an app with a project reference to `Ranvier.View.Arms` and an
import of `Ranvier.View.Arms.targets`:

- **Discovery through a project reference and the `.targets`:** the plugin lowers the app's `Arms.Match` call; the
  emitted `Main.js` holds the `armsValues_L…` prologues.
- **`FABLE_COMPILER` during project cracking:** set; the `.targets` reference is conditional, and a .NET build carries
  no plugin assembly.
- **netstandard2.0 plugin in Fable 5.18 and Fickl 0.3.0:** loads (Fable.AST 5.0.0 compile reference, FSharp.Core 10.1.203).
- **Server HTML:** the three states render byte-identical HTML on .NET (quotation path) and under Fable (plugin path).
- **Fable.AST in the compilation:** the plugin attribute on `Arms.Match` derives from `MemberDeclarationPluginAttribute`,
  so the compilation that holds `Arms.fs` references `Fable.AST.dll` as well; without it Fable reports FS0074 at the
  attribute and skips the plugin assembly ("`ScanForPluginsAttribute` is required here and is unavailable"). The plugin
  project copies `Fable.AST.dll` beside its own assembly, and the `.targets` references both.
- **References and defines of referenced sources:** Fable compiles `Ranvier.View.Arms` with the app's assembly
  references and the app's `DefineConstants`, not those of `Ranvier.View.Arms.fsproj`. With the `.targets` imported by
  the app alone, `RANVIER_ARMS_PLUGIN` reaches `Arms.fs`; an app without the import (`Tests/NoTargets`) fails at its
  call with FS0101 and the AM005 text.
- **MSBuild targets during cracking:** Fable's project cracking runs a target declared
  `BeforeTargets="ResolveAssemblyReferences"`; the `.targets` check for a missing plugin assembly fires under
  `dotnet fickl`.
- **`dotnet fsi build.fsx -- test` cleans the plugin's `bin` folder** with every other; the plugin is rebuilt after it.

## .NET lowering cost

The site's selector and bodies form one LINQ expression. Its construction runs once in the LINQ interpreter
(`Compile(preferInterpretation = true)`). Each closed lambda inside it that runs per call (the selector, text and
attribute readers, row readers, event handlers) is a `Hot` function: interpreted for its first 64 calls, then compiled.
Delegates (hooks) stay interpreted.

**Cold first site.** `experiments/Ranvier.View.Arms/Checks/DotnetCost` lowers the research's five-target site
(or-pattern, literal pattern, guard) as the first `Arms.Match` of a fresh process, then 200 more, and renders a 1,000-row
site against its `armOf` twin. Three processes per row:

| Mode | First site, fresh process (ms) | First site after the view is warm (`--twin-first`, ms) | Mean of 200 (ms) |
|---|---|---|---|
| interpreted, hot lambdas compiled after 64 calls (shipped) | 162, 147, 133 | 109, 118, 143 | 1.07-1.36 |
| all interpreted | 137, 141, 198 | 109, 105, 110 | 1.01-1.04 |
| all compiled (`preferInterpretation = false`) | 132, 134, 163 | 117, 126, 126 | 9.41-10.11 |
| hot lambdas compiled at lowering | | 126, 132, 122 | 6.09-6.44 |

Building the `armOf` twin first, cold, takes 29-32 ms (the view's own JIT). The rest of the first `Arms.Match` is JIT
of FSharp.Core's quotation deserialiser, `LeafExpressionConverter` and the Arms lowering ("Cold start" below). No mode meets the spec's 10 ms for the first site of a process; the mean meets the 4 ms
budget. The mitigation is the spec's start-up advice: bind sites at module level and render once at start-up, before
the first request. `RepresentationChecks` prints `lowering|warm-first=...` (about 1.5-2.5 ms) for a site lowered after
earlier ones.

**Server render of 1,000 rows** (the rows fixture against its `armOf` twin, one `li` reader per row, mean of 200 renders
after 20 warm-up renders, three rounds per process, three processes):

| Mode | Arms.Match (ms) | twin (ms) |
|---|---|---|
| interpreted, hot lambdas compiled after 64 calls (shipped) | 0.40-0.86 | 0.29-0.77 |
| all interpreted | 0.54-1.42 | 0.21-0.73 |
| all compiled | 0.60-0.77 | 0.52-0.81 |

All-interpreted readers cost about 0.3-0.6 µs more per row reader and render, roughly twice the twin's time for a
reader-heavy page. Compiling each per-call lambda after 64 calls matches the twin once warm, and keeps the lowering near
the all-interpreted cost.

The probe compiled one lambda per body and the selector: 9.06 ms first (warm), 8.55 ms mean.

## Cold start

Measured with `Checks/DotnetCost` (FSharp.Core 10.1.401, the app's pin; the library's floor is 8.0.100), Release,
net10.0, a fresh process per run, under the benchmark lock.

**Where the first site's time goes** (the Phase 3 review's phase profiler, each stage warmed separately, in order;
`System.Runtime.JitInfo` deltas, three processes):

| Stage | Wall (ms) | JIT (ms) | Methods |
|---|---|---|---|
| the view's cold start (an `armOf`/`matchWith` site) | 8.7-8.9 | 8.1-8.4 | 98 |
| quotation deserialisation (a quoted `match` of the same shape) | 30-33 | 26-29 | 382 |
| `LeafExpressionConverter.QuotationToExpression` | 50-54 | 46-49 | 335 |
| `System.Linq.Expressions` interpreter | 3.1-3.5 | 0.4-0.5 | 9 |
| the first `Arms.Match` after those (lowering, `Hot` reflection, `Site.create`, F# collection generics) | 37-40 | 32-33 | 358 |

The cost is JIT of IL-only code: FSharp.Core from NuGet is not ReadyToRun, and the shared framework is, so
`DOTNET_ReadyToRun=0` raises the first site only to 192 ms; tiered-compilation settings change nothing.

**`Arms.Prepare` and ReadyToRun** (`dotnet publish -r win-x64 -p:PublishReadyToRun=true`), three processes each; "first
site" is the five-target site, timed after `Prepare` when it runs:

| Build | `Arms.Prepare ()` (ms) | First site (ms) | First site JIT (ms) |
|---|---|---|---|
| JIT | — | 125-146 | 111-128 |
| JIT, after `Prepare` | 167-182 | 9.2-11.9 | 5.7-7.4 |
| ReadyToRun (first run after publish: 113) | — | 56-57 | 30-31 |
| ReadyToRun, after `Prepare` | 68-75 | 6.9-7.4 | 3.3-3.5 |

`Prepare` moves the cost to start-up, on the calling thread or on the thread pool; ReadyToRun removes about two thirds
of it. With both, the first user site costs about 7 ms, within the draft's 10 ms. The remaining JIT is generic
instantiation over the app's own types, which ReadyToRun cannot pregenerate. Removing the rest needs a build-time
lowering (spec Decision 15).

**Hot compiles.** A per-call lambda queues its compilation on the thread pool at its 64th call and keeps running
interpreted until the compiled delegate is published, so no render waits for a compile (the review measured an inline
compile at 8.2-9.1 ms for the first of a process). `Prepare` compiles one lambda, so the pool's first compile starts warm.
A lambda that closes over a variable of its case body (`let suffix = "!" in pre () { fun _ -> t + suffix }`) is not
closed, so it is never compiled and stays interpreted for the life of the site.

## Fickl

Master replaced `dotnet fable` (Fable 5.18.0) with `dotnet fickl` (Fickl 0.3.0, which reports Fable 5.22.0) in #152.
Its tool folder holds the same `Fable.AST` 5.1.0 and FSharp.Core 10.1.203, and the plugin loads unchanged: the
discovery app renders the same HTML on both targets, and the plugin tests pass with the same diagnostics, ranges and
emitted sites.
