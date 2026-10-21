# Arms.Match: measurements

Measurements of the `Arms.Match` implementation ([spec](../superpowers/specs/2026-10-21-arms-match-design.md),
[plan](../superpowers/plans/2026-10-21-arms-match.md)). Fickl 0.3.0 (Fable 5.22), net10.0, Node 26; the Phase 1 discovery ran under Fable 5.18.0 and again under Fickl.

**Summary.** The plugin is found through a project reference and the `.targets`. On .NET a site lowers in about
1.1-1.4 ms once the process is warm; the first five-target site of a process costs 125-146 ms, 9-12 ms after
`Arms.Prepare ()` and about 7 ms with ReadyToRun as well. Under Fable the first `Arms.Match` site costs 370 B gzip over
its twin (budget 400 B) and each further site -22 B (budget 60 B). The retained heap per row is within 10 % of
`ranvier-sugar` after create and about 18 % above it after a switch. In Chromium, create holds its gate, switch is about
14 % slower than the sugared twin (a miss), and payload and clear are not settled.

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
(`Compile(preferInterpretation = true)`). Each lambda inside it that runs per call (the selector, text and
attribute readers, row readers, event handlers) is a `Hot` function: interpreted for its first 64 calls, then compiled.
Delegates (hooks) and lambdas over lambda-bound variables stay interpreted.

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
A lambda over `let`-bound values of its case body (`let suffix = "!" in pre () { fun _ -> t + suffix }`) is built by
a factory over those values: the factory runs interpreted once per site construction, and the first of its `Hot`
functions to reach 64 calls compiles it once. A failed compile leaves the function interpreted and writes a `Trace`
warning.

## Fickl

Master replaced `dotnet fable` (Fable 5.18.0) with `dotnet fickl` (Fickl 0.3.0, which reports Fable 5.22.0) in #152.
Its tool folder holds the same `Fable.AST` 5.1.0 and FSharp.Core 10.1.203, and the plugin loads unchanged: the
discovery app renders the same HTML on both targets, and the plugin tests pass with the same diagnostics, ranges and
emitted sites.

## Bundle

`experiments/Ranvier.View.Arms/Checks/size.mjs` compiles the Playground's DomTests in Release with no defines into
`generated-size-tests`, then bundles each entry with Vite 8.3.3 (minified), gzip level 9 and Brotli quality 11; the
numbers are in [`arms-match/bundle.json`](arms-match/bundle.json).

| Entry | raw | gzip | Brotli |
|---|---|---|---|
| explicit loader | 207,850 | 60,202 | 52,828 |
| Arms.Match loader | 208,666 | 60,572 | 53,130 |
| two explicit sites | 208,693 | 60,411 | 52,980 |
| two Arms.Match sites | 209,511 | 60,759 | 53,313 |

The first site costs 370 B gzip over its twin (budget 400 B) and the second -22 B (budget 60 B). The method is a
whole-bundle gzip difference, so it resolves about ±20 B: a build with `RANVIER_VIEW_COUNTERS` moves the first figure by
19 B and the second by 25 B without touching `Ranvier.View.Arms`. An entry that calls no `Arms.Match` holds no
`Ranvier.View.Arms` module, the `Arms.Match` entry holds no quotation code and no curry helper, and `Arms.Prepare ()`
in the entry adds no bytes.

How the first-site cost came down from 771 B:

| Change | First site, gzip |
|---|---|
| F# classes over fable-library arrays (first cells) | 771 |
| cells as JavaScript objects in a `WeakMap`, `Site.create` inline | 460 |
| a target's single live entry held without an array (Task 10, for the heap) | 496 |
| AM006 as a code and docs anchor in Release; no run-time AM005 branch; `enter` and `values` inlined into `Arms.Match` as closures; one `serves` closure; unchecked indexing | 370 |

The remaining cost is the cells' code: entry registration and withdrawal, the read over live and withdrawn entries
with graph tags, and the site's arm table. Holding every target's entries in an array would save about 50 B and cost
about 90 B of heap per row; dropping graph tags under Fable would save about 36 B and let a reader under one graph read
another graph's arm.

## Chromium

`keyed-rows.mjs --modes=ranvier-sugar,ranvier-arms-match --ops=armsCreate1k,armsSwitch1k,armsPayload1k,armsClear1k
--processes=3 --rounds=15 --pairs=ranvier-arms-match:ranvier-sugar`, under the benchmark and browser locks, at
`f5e2b3d`'s cells (Fickl 0.3.0, Fable 5.22.0; source hashes in the JSON); results in `arms-match/arms-match.json`. The
run started after three load readings below 20 %; the CPU load was 33 % at its start and 31 % at its end (other agents'
work), against 59-77 % for the earlier runs.

| Operation | ranvier-sugar median (process medians), ms | ranvier-arms-match median (process medians), ms | paired median ratio | 95 % interval | arms slower in |
|---|---|---|---|---|---|
| armsCreate1k | 13.67 (14.37, 12.62, 13.62) | 14.20 (14.57, 12.72, 14.39) | 1.021 | 1.007-1.046 | 30/45 |
| armsSwitch1k | 6.67 (7.00, 6.57, 6.38) | 7.67 (7.86, 7.22, 7.63) | 1.144 | 1.111-1.176 | 44/45 |
| armsPayload1k | 0.92 (0.91, 1.03, 0.83) | 1.01 (1.07, 1.09, 0.95) | 1.112 | 1.023-1.178 | 30/45 |
| armsClear1k | 1.93 (1.66, 2.57, 1.77) | 2.00 (1.89, 2.51, 1.94) | 1.065 | 0.994-1.159 | 27/45 |

The paired ratio divides each `ranvier-arms-match` sample by the `ranvier-sugar` sample of the same process and round;
the interval is a percentile bootstrap of its median. armsCreate1k holds the gate (median at most 1.05).
**armsSwitch1k misses it**: about 14 % slower, with the whole interval above 1.05; each switch registers a new entry
and withdraws the old one, work the twin does not do. armsPayload1k and armsClear1k have medians above 1.05 with
intervals that reach below it, so they are not settled by this run.

## Heap

`keyed-heap.mjs --modes=ranvier-sugar,ranvier-arms-match --fixture=arms --counts=1000,10000`, after create and, with
`--after=switch`, after one switch of every row; results in `arms-match/arms-match-heap.json` and
`arms-match/arms-match-heap-switch.json`.

| Rows | ranvier-sugar bytes per row | ranvier-arms-match after create | Δ | ranvier-arms-match after a switch | Δ |
|---|---|---|---|---|---|
| 1,000 | 3,511 | 3,843 | +332 (+9.5 %) | 4,135 | +624 (+17.8 %) |
| 10,000 | 3,528 | 3,873 | +345 (+9.8 %) | 4,165 | +637 (+18.1 %) |

After create the gate holds: both counts stay within 10 % of `ranvier-sugar`. The extra bytes are the slots object and
entry, the arm's cleanup closure, and the arm's `current` closure kept by the entry. After a switch each row also keeps
its withdrawn entry, which holds the disposed arm's `current` and its last values: about 290 B more per row, bounded by
one withdrawn entry per target and instance. The sugared twin keeps nothing after a switch.
