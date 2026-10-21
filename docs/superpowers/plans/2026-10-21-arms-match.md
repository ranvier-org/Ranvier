# Arms.Match Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `Arms.Match (read, function ...)`, an opt-in arm site written as a plain F# `match`, with the arms, hashes, server HTML and hydration of the hand-written `armOf` + `matchWith` form, on .NET (quotation lowering) and under Fable (compiler plugin).

**Architecture:** A runtime project, `experiments/Ranvier.View.Arms`, holds `Arms.Match`, the per-site `Cells` table and `Site.create`, built on the public view API only. Under Fable a plugin project, `experiments/Ranvier.View.Arms.FablePlugin`, rewrites every `Arms.Match` call into a target selector and one case body per target; on .NET the same call receives a quotation and `Quoted.lower` applies the same rules. Both producers feed `Site.create`, so hashes, layouts and server HTML come from one runtime path.

**Tech Stack:** F# 10, FSharp.Core 8.0.100 floor (library) and 10.1.203 (plugin), Fickl 0.3.0 (`dotnet fickl`, Fable 5.22) with Fable.AST 5.0.0, net10.0 and netstandard2.0, FSharp.Linq `LeafExpressionConverter`, Node 26 with jsdom 30.1.1 (`node --test`), Vite 8.3.2, Playwright Chromium for the benchmark harness.

**Spec:** [`docs/superpowers/specs/2026-10-21-arms-match-design.md`](../specs/2026-10-21-arms-match-design.md), approved 2026-10-21 with Decisions 11-18 (the former open questions). Read it, and the research note [`docs/research/2026-10-21-idiomatic-arms.md`](../../research/2026-10-21-idiomatic-arms.md), before any task. The probe sources under `experiments/arms-probes` are the starting code.

## Core and view state

Required by `.claude/rules/view-uses-core.md`; copied from the spec.

**Core primitives used:**

- The arm memo behind `matchWith` (`Template.fs:2721-2734`): `Arms.Match` lowers to `matchWith` over one definition per match target. Selection, the owner per selected arm and remount-versus-update stay with the existing region; the key is the definition (`KeyedItems.pairKey`).
- `Owner` cleanups through `Setup.OnCleanup` (`Representation.fs:15-17`): an arm withdraws its payload reader from the site's cells when its owner is disposed.
- `Graph.TryCurrent` (`src/Ranvier/Core.fs:1741`): a reader picks the payload readers registered under its own graph, so concurrent server renders stay apart.

**View-owned state (new):** one `Cells` table per site, a `WeakMap` under Fable and a `ConditionalWeakTable` on .NET, from an instance to the payload readers of the instance's mounted arms, one list per target, plus the values held by the last withdrawn entry of each target.

## Global Constraints

Every task's requirements include this section.

- Toolchain: Fickl 0.3.0, the Fable compiler of `.config/dotnet-tools.json` since #152 (`dotnet fickl`, Fable 5.22.0, fable-library 5.22), Fantomas 8.0.5, net10.0.
- `Fable.AST` (plugin, compile reference): 5.0.0. `FSharp.Core` (plugin): 10.1.203. `FSharp.Core` (library): the view projects' 8.0.100 floor (`FSharpCoreImplicitPackageVersion`, no pin). `FableMinimumVersion`: `"5.0"`.
- Plugin target: netstandard2.0, with `[<assembly: ScanForPlugins>]`. Runtime targets: net10.0;net8.0 (as Portable since #147), Fable sources.
- `armOf`, `arm`, `matchWith`, `branch`, `show` and `showElse` (`TemplateSugar.fs:101-171`) keep their signatures, behaviour, docs and bundle bytes. No file under `src/`, `experiments/Ranvier.View.Portable/*.fs` or `experiments/Ranvier.View.Foundation/*.fs` changes. A task that finds a change there necessary stops and reports it.
- The runtime uses only public view API (`Component.define`, `matchWith`, `arm`, `Setup.OnCleanup`): an app that does not reference the package gets 0 B and 0 code-path changes.
- The .NET `Arms.Match` carries `[<RequiresUnreferencedCode>]` and `[<RequiresDynamicCode>]` with the text `"Arms.Match lowers a quotation through reflection and System.Linq.Expressions. Use matchWith with armOf in trimmed or NativeAOT apps."`. `tests/Ranvier.AotSmoke` is unchanged.
- Diagnostic texts are fixed (spec, "The payload rule"); they live once, in `experiments/Ranvier.View.Arms/Diagnostics.fs`, which the plugin links. .NET prefixes `<file>(<line>): ` to every site-build-time message.
- Bundle budget: `Arms.Match` entry vs its explicit twin ≤ 250 B gzip; each further site ≤ 60 B gzip; an app without `Arms.Match` 0 B; the Playground `sugar-size.mjs` explicit entry 0 B before and after; no `quotation` code in an `Arms.Match` entry; 0 `curry`, `uncurry`, `partialApply` in the emitted `Ranvier.View.Arms` modules and prologues.
- .NET lowering budget: ≤ 10 ms for the first five-target site of a process, ≤ 4 ms mean over 200 more.
- Chromium gate: each of `armsCreate1k`, `armsSwitch1k`, `armsPayload1k`, `armsClear1k` within 5 % of `ranvier-sugar` by median, or the ranges overlap. Heap: retained bytes per row within 10 % of `ranvier-sugar` plus the cells entries.
- Work in `C:\Users\shaya\RiderProjects\Ranvier-arms-research` on branch `feat/arms-match`. Merge `origin/master` (never rebase) at the start of each phase; a parallel effort (`feat/dom-twin`, adding `Native.fs` in Portable and a .NET DOM twin) may land first, and every task that edits `DomTests.fsproj`, `RepresentationChecks.fsproj` or `RepresentationChecks.fsx` (Tasks 3-8, 10) merges master before it starts.
- Commits: one conventional line, a blank line, then exactly:
  ```
  Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01DgoUpTaqPJBUQ7p2oFznr2
  ```
  Write the message to a file under `C:\Users\shaya\.claude\jobs\7fee9ca9\tmp\` and commit with `git commit -F <file>`. Before each commit: `dotnet fantomas <each changed .fs file>`, `git add` of named paths only, `git diff --cached --check` empty, no `FOR-REVIEW` comment staged. Push with `git push origin HEAD:feat/arms-match`. Never rebase, amend a pushed commit or force-push.
- Comments follow `.claude/rules/comments.md`.
- Every existing test passes unchanged. A test that has to change is a plan conflict: stop and report it.

### Gates

Task gates, from the worktree root, all exit 0 (run the ones the task's files reach; the last task of each phase runs all of them):

```bash
dotnet fsi build.fsx -- test
dotnet build experiments/Ranvier.View.Arms.FablePlugin/Ranvier.View.Arms.FablePlugin.fsproj -c Release
dotnet fsi experiments/Ranvier.View.Arms.FablePlugin/Tests/PluginTests.fsx
dotnet run --project experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj -c Release
dotnet run --project docs/samples/view/Ranvier.Docs.ViewSamples.fsproj -c Release
dotnet fsi docs/samples/view/Generate.fsx --check
(cd tools/Ranvier.Dom.Generate && npm ci) && dotnet fsi tools/Ranvier.Dom.Generate/GeneratorTests.fsx
dotnet fsi tools/Ranvier.Dom.Generate/CheckDocs.fsx
dotnet fsi tools/Ranvier.Dom.Generate/CheckTypes.fsx
cd examples/Fable.Ranvier.Playground && npm ci && npm run test:dom && npm run test:sugar-debug && npm run test:sugar-counters && npm run test:arms
dotnet fantomas --check <each changed .fs file>
```

Phase gates (end of Tasks 1, 4, 6, 8, 11, 12) add, from `examples/Fable.Ranvier.Playground`, under the browser lock:

```bash
until mkdir C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/browser.lock 2>/dev/null; do sleep 30; done
npm run test:browser
rmdir C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/browser.lock
node benchmarks/frameworks/sugar-size.mjs
node -e "const a=require('C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-sugar-size-before.json').explicit,b=require('../../docs/research/framework-performance/investigation/view-sugar-v2/bundle.json').explicit;if(JSON.stringify(a)!==JSON.stringify(b)){console.error(a,b);process.exit(1)}"
git checkout -- ../../docs/research/framework-performance/investigation/view-sugar-v2/bundle.json
```

The explicit entry's bytes equal the record Task 1 Step 1 takes before any change (Ruling 12); the regenerated file is not committed. When a merge of `origin/master` changes a file the explicit entry compiles (anything under `experiments/Ranvier.View.Portable`, `experiments/Ranvier.View.Foundation` or `src/`), retake the record from the merged master: `git worktree add C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-baseline origin/master`, run Task 1 Step 1 there (its own `npm ci`), copy the file, then `git worktree remove C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-baseline`.

and, from Task 9 on, `node experiments/Ranvier.View.Arms/Checks/size.mjs` from the worktree root. The PluginTests line applies from Task 2 on; `npm run test:arms` from Task 4 on.

### Repo hazards

- **Fable 5.18 inlines a single-use immutable `let` snapshot of a module `mutable`** (observed before #152; assume Fickl does too). `let snapshot = counter` followed by one use reads the live value. Use `let mutable snapshot = counter`, or check the emitted JS.
- **A union's `.IsCase` properties break Fable precompile.** Write a `match` instead.
- **No `node_modules` junctions or symlinks.** Run `npm ci` in each package directory of this worktree (`examples/Fable.Ranvier.Playground`, `tools/Ranvier.Dom.Generate`).
- **Locks.** `browser.lock` (any Chromium or Playwright run) and `bench.lock` (any timed benchmark) are directories under `C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/`: `mkdir` before, waiting while it exists, `rmdir` after. Only the creator removes a lock.
- **The plugin assembly must exist before `dotnet fickl` evaluates a project that imports the `.targets`.** `npm run compile:tests` builds it; any other `dotnet fickl` of `DomTests.fsproj` (the counters build, `keyed-rows.mjs --build`) runs after a plugin build. After a plugin change, compile with `--noCache`: Fable's cache does not track the plugin. `dotnet fsi build.fsx -- test` cleans every `bin` folder, the plugin's included, so the plugin build runs after it; a missing plugin assembly fails the Fable compile with "Ranvier.View.Arms.FablePlugin.dll is missing from ..." (the `.targets` check).
- Leave untracked `generated-*` directories alone; new output directories (`generated-arms-size`, `Checks/Discovery/out`, `Checks/TrimCallSite/bin`) are git-ignored by the task that creates them.

## Rulings on the spec

Each ruling picks an implementation where the spec leaves one open, or settles a conflict.

1. **The open questions become Decisions 11-18 of the spec**, in their order, and the "unsupported form" error of Decision 11 is **AM007** (`Arms.Match: this pattern form is not supported.`). *If wrong:* one code and one table row.
2. **"Refers to a controlled identifier" means a reference outside every lambda.** A `let view = pre () { fun _ -> t }` in a case body is a leaf, and its reader is rewritten; with the spec's literal rule it would be a decision binding computed per region read and then raise AM001. **A compiler-generated `let`** (a Fable ident with `IsCompilerGenerated`, a quotation variable whose name contains `@`) is decision only when its value extracts a part of a controlled identifier (the probe's rule), so a CE's `builder@` binding stays in the leaf. *If wrong:* a `let` over a view that reads a pattern variable only in a reader becomes a misleading AM001.
3. **Emitted names are derived from a source range** (`armsValues_L12C8`), because `TransformCall` receives no enclosing member: the call's, else the arms lambda's, else the read's; a call with none of them fails with a plugin error, so no process-wide counter exists. The names are JS locals and reach no HTML, hash or marker. `PluginTests.fsx` replaces each `L<line>C<column>` by its ordinal in the file before comparing, as the spec's "names normalised" says, so the expectation changes only when the lowering does.
4. **The plugin packs a function, not values.** `?lowered` receives `fun values -> (select, bodies)`, where `values: Func<obj, int, obj array>` reads the cells; `Arms.Match` creates the cells, calls the function once and hands its result to `Site.create`. Each reader's prologue is `values(instance, target)` (an `Emit`). The cells stay F# code, and the plugin emits no constructor or import. *If wrong:* one prologue shape in `Lower.fs` and five lines in `Arms.fs`.
5. **`BuildTimeReadException` and `ArmsMatchException` exist on .NET only.** Under Fable, AM005 and AM006 raise `InvalidOperationException` with the same text; the other codes are compile errors there. *If wrong:* Fable code catching the named types does not compile.
6. **Two files beyond the spec's list:** `Errors.fs` (the exception types and raise helpers, runtime only) and the plugin's link of `Diagnostics.fs`. Exceptions cannot live in the linked file: the plugin assembly is a reference of every Fable compilation, and two public `ArmsMatchException` types would collide.
7. **`Site` and `Cells` are internal.** `Site.create` takes the internal `Cells` type, so it cannot be public. *If wrong:* none; the spec's API names no caller outside the package.
8. **Under Fable, an instance type that is a generic parameter passes AM004 at compile time.** Only a concrete type can be judged; `WeakMap.set` raises at mount for a primitive. *If wrong:* a generic helper instantiated at `int` fails at mount instead of at compile.
9. **The .NET lowering evaluates the site as one LINQ expression through the interpreter** (`Compile(preferInterpretation = true)`), landing in Task 3. Compiling it measured 6.8 ms mean, most of it the first call's JIT of the emitted method, over the 4 ms budget; the interpreter measures about 1 ms, and server renders show no measurable difference (research note). The `lowering|` budget is enforced when `RepresentationChecks` runs with `--timing`, which Task 3's steps and the research note use. CI prints the line without failing on it: shared runners are not the budget's machine. *If wrong:* a CI job fails on runner noise.
10. **The plugin tests are a `dotnet fsi` script** (`PluginTests.fsx`), as the generator's `GeneratorTests.fsx` is. A bad fixture asserts exit code 1, the file, the line and the exact message, and the column where the range is the identifier or the call: AM001, AM003, AM004 (and the AM001b fixture, which reports AM001; see Ruling 24). Fable prints a plugin error's column 0-based (an FCS error's 1-based: the no-targets FS0101 at `Site.fs(12,13)`). AM002 and AM007 assert the line only.
11. **A prologue carries the range of the first pattern-variable reference inside its reader.** A Fable `Lambda` has no range of its own.
12. **The 0 B rule for an app that references the package and calls no `Arms.Match`** is checked as the absence of every `Ranvier.View.Arms` module from that entry's bundle, plus the Playground explicit entry's bytes before (recorded in Task 1) and after. A second Fable compilation without the reference would cost a full compile per size run for the same evidence.
13. **`DomTests.fsproj` links the `Ranvier.View.Arms` sources** and imports the `.targets`, as it links every other view source; a project reference would bring a second copy of the Portable types.
14. **`npm run test:arms` is its own script**, run by the CI `arms` job and by the gates; it is not added to `npm test`, which the `playground` job runs. The arms counters comparison runs from the counters build (`generated-tests-counters`).
15. **Golden lines name states by label** (`Loaded done 3`), not `%A`, which prints differently under Fable.
16. **The incomplete-match fixture keeps its raising state out of the parity lines**; a check of its own asserts that the site raises for it on each target. The two runtimes' `MatchFailureException` messages differ.
17. **The twin-trace harness of `SugarChecks.fs` moves to `tests/TwinTrace.fs`**, unchanged, so `ArmsChecks.fs` reuses it.
18. **A cells entry records the values of each read** (`Last`), and withdrawal keeps the last entry's `Last`. No read happens at setup, so core counters match the twin's.
19. **A thunk over `unit` and a lambda over a compiler-generated parameter are transparent to AM002.** A pattern variable inside `fun () -> ...` (a computation expression's `Delay`) or inside the CE's `fun builder@ -> ...` is reported as AM001, read outside a reader, and not as a read inside a lambda over `unit` or over the element builder.
20. **AM005 is a compile error at the call** (spec Decision 17, amended). The `.targets` defines `RANVIER_ARMS_PLUGIN`; without it `Arms.fs` compiles an `[<Obsolete(Diagnostics.noPlugin, true)>]` overload, so the call fails with FS0101 and the AM005 text. `Ranvier.View.Arms.fsproj` does not import the `.targets`: Fable compiles referenced sources with the app's references and defines (verified in Phase 1), and the project's own import would define the symbol for an app without the plugin. `Tests/NoTargets` pins the FS0101 outcome. The run-time AM005 remains for a referenced plugin that does not run.
21. **The .NET lowering rewrites a delegate over the instance** (`NewDelegate`) as a reader, as the plugin rewrites a Fable `Delegate`; `Hooks.exit (Func<_, _, _, _>(fun s node finish -> ...))` is one.
22. **Fable.AST at graduation** (spec Decision 16, amended): the package declares `Fable.AST` as a NuGet dependency and ships only `Ranvier.View.Arms.FablePlugin.dll` under `fable-plugin/` from an explicit file list. The experiment references the copied `Fable.AST.dll` directly, since no package exists to exercise the dependency form.
23. **The `.targets` names its plugin folder `RanvierArmsPluginDir`** (default: the Release output) and fails the Fable compile when the plugin assembly is missing there; Fable's cracking runs the check (`BeforeTargets="ResolveAssemblyReferences"`, verified in Phase 1).
24. **The simple-match AM001b shape does not occur under Fickl 0.3.0.** For `| Loaded (text, _) -> span () { text } | _ -> ...`, FCS hands Fable a bound `text`, so the plugin reports AM001; the `AM001b` fixture asserts that. The AM001b text stays for an inlined `matched.fields[k]` read outside a reader.
25. **A named arms function arrives eta-expanded.** Fable passes `Arms.Match (read, arms)` as `fun _arg1 -> arms _arg1`, a lambda over a compiler-generated parameter whose body applies a value to it; the plugin reports AM003 for that shape.

## Review Focus

1. Two .NET server renders of one site on two threads at once, each with its own graph and instance: each response shows its own instance's payload. Test: `concurrentRenders` (Task 5).
2. An arm hidden by an enclosing `show` and shown again for the same instance: the remounted arm's readers follow later writes, not the withdrawn snapshot. Test: `remountAfterHide` (Task 5).
3. A helper view typed `TemplateView<'inst>` that takes a reader, called in a case body as `labelled (fun _ -> text)`: the reader is rewritten and the helper's markup matches its twin. Test: the `helper` fixture (Task 4).
4. One `Arms.Match` site per row of a keyed list (the instance is the row item): after a reorder, each row shows its own payload, and a payload write reaches only its row. Test: `siteInRows` (Task 6).
5. A batch that switches a site away and back to the same target (`Loaded a` → `Idle` → `Loaded b`) under queued commits: the arm shows `b` and nothing is reported. Test: `switchBackInOneBatch` (Task 6).

## File structure

| File | Responsibility | Tasks |
|---|---|---|
| `experiments/Ranvier.View.Arms/Ranvier.View.Arms.fsproj` (new) | runtime project; imports the `.targets` | 1 |
| `experiments/Ranvier.View.Arms/Ranvier.View.Arms.targets` (new) | adds the plugin assembly as a `Reference` under Fable | 1 |
| `experiments/Ranvier.View.Arms/Diagnostics.fs` (new) | AM001-AM007 texts, the trim-annotation text | 1, 2 |
| `experiments/Ranvier.View.Arms/Errors.fs` (new) | `BuildTimeReadException`, `ArmsMatchException` (.NET), raise helpers | 1 |
| `experiments/Ranvier.View.Arms/Cells.fs` (new) | per-site instance table: single slot (Task 1), live lists (Task 5) | 1, 5 |
| `experiments/Ranvier.View.Arms/Site.fs` (new) | `Site.create` | 1 |
| `experiments/Ranvier.View.Arms/Quoted.fs` (new) | .NET quotation lowering | 1, 3 |
| `experiments/Ranvier.View.Arms/Arms.fs` (new) | `Arms.Match`, both targets | 1, 3 |
| `experiments/Ranvier.View.Arms.FablePlugin/*` (new) | `Shape.fs`, `Lower.fs`, `Plugin.fs`, the plugin project | 1, 2 |
| `experiments/Ranvier.View.Arms.FablePlugin/Tests/*` (new) | `PluginTests.fsx`, fixtures project, `expected/Sites.js` | 2 |
| `experiments/Ranvier.View.Arms/Checks/Discovery/*` (new) | phase-1 discovery app | 1 |
| `experiments/Ranvier.View.Arms/Checks/ArmsFixtures.fs` (new) | pattern-table fixtures and explicit twins | 4, 5, 6, 8 |
| `experiments/Ranvier.View.Arms/Checks/ArmsServerChecks.fs` (new) | twin layouts, hashes, HTML; golden lines | 4, 7 |
| `experiments/Ranvier.View.Arms/Checks/ArmsLoweringChecks.fs` (new) | .NET rejections, timing, cells, concurrency | 3, 5 |
| `experiments/Ranvier.View.Arms/Checks/arms-server.golden.txt` (new) | cross-runtime server HTML | 7 |
| `experiments/Ranvier.View.Arms/Checks/size.mjs`, `size.config.mjs` (new) | bundle budget | 9 |
| `experiments/Ranvier.View.Arms/Checks/TrimCallSite/*` (new) | trim-annotation check | 11 |
| `experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj`, `RepresentationChecks.fsx` | compile and run the arms checks | 3, 4, 5, 7 |
| `examples/Fable.Ranvier.Playground/tests/DomTests.fsproj` | links the arms sources and checks; imports the `.targets` | 4, 6, 9, 10 |
| `examples/Fable.Ranvier.Playground/tests/TwinTrace.fs` (new), `SugarChecks.fs` | shared twin-trace harness | 6 |
| `examples/Fable.Ranvier.Playground/tests/ArmsChecks.fs` (new) | mounted behaviour, hydration, Debug, held exits, pending | 5, 6, 7, 8 |
| `examples/Fable.Ranvier.Playground/tests/arms*.test.mjs` (new) | jsdom runners | 4-8 |
| `examples/Fable.Ranvier.Playground/tests/ArmsSizeEntries.fs`, `ArmsSizePlain.fs` (new) | size entries | 9 |
| `examples/Fable.Ranvier.Playground/tests/KeyedRowsArmsMatchPerf.fs` (new), `benchmarks/frameworks/keyed-page.mjs`, `keyed-heap.mjs`, `keyed-rows.mjs` | Chromium mode and heap profile | 10 |
| `examples/Fable.Ranvier.Playground/package.json`, `tests/sugar-counters-build.mjs` | plugin build before Fable; `test:arms` | 4, 6 |
| `.github/workflows/ci.yml`, `Ranvier.slnx` | `arms` job; solution entries | 1, 11 |
| `docs/content/view/arms.md`, `reference.md`, `troubleshooting.md`, `docs/samples/view/*` | docs and samples | 12 |
| `docs/research/2026-10-21-arms-match.md` (new), `docs/research/arms-match/bundle.json` (new) | discovery, lowering, bundle, benchmark and heap results | 1, 3, 9, 10, 12 |

## Spec coverage

| Spec item | Task |
|---|---|
| Phase 1: projects, `.targets`, discovery through a project reference, `FABLE_COMPILER` in cracking, netstandard2.0 load | 1 |
| Decisions 1-4, 8, 9; public API | 1, 3 |
| Widened decision rule, throw leaves, `Delegate` readers, AM001b, AM002-AM004, AM007, stable names (Phase 2, Testing 1) | 2 (Fable), 3 (.NET) |
| Pattern-table fixtures and twins on both targets | 4 |
| Cells lists, graph tags, withdrawal, locking, last-withdrawn values, AM006 (Decision 7, Phase 3) | 5 |
| .NET one-lambda compile, `lowering|` budget (Testing 2) | 3 |
| Testing 3-7 (mounted traces, twice-shown, events, value payloads, rows) | 5, 6 |
| Parity guarantees, golden file, hydration (Phase 4) | 4, 7 |
| Testing 8-10 (Debug, held exits, pending sources) | 8 |
| Bundle budget | 9 |
| Testing 11-12 (Chromium, heap) | 10 |
| Testing 14, trim annotation; CI `arms` job | 11 |
| Docs, Testing 13 | 12 |
| Phase 7 (analyzer) | separate spec, not in this plan |

---

## Phase 1: scaffold and discovery

### Task 1: Runtime and plugin projects, the `.targets`, and plugin discovery through a project reference

**Files:**
- Create: `experiments/Ranvier.View.Arms.FablePlugin/Ranvier.View.Arms.FablePlugin.fsproj`, `Shape.fs`, `Lower.fs`, `Plugin.fs`
- Create: `experiments/Ranvier.View.Arms/Ranvier.View.Arms.fsproj`, `Ranvier.View.Arms.targets`, `Diagnostics.fs`, `Errors.fs`, `Cells.fs`, `Site.fs`, `Quoted.fs`, `Arms.fs`
- Create: `experiments/Ranvier.View.Arms/Checks/Discovery/Discovery.fsproj`, `Main.fs`, `.gitignore`
- Create: `docs/research/2026-10-21-arms-match.md`
- Modify: `Ranvier.slnx` (one folder entry)
- Test: `experiments/Ranvier.View.Arms/Checks/Discovery/Main.fs`, run on .NET and under Fable

**Interfaces:**
- Consumes: the probe sources `experiments/arms-probes/Plugin/Plugin.fs`, `Probe/CaseArms.fs`, `Probe/QuotedArms.fs`.
- Produces (later tasks rely on these names):
  - `Ranvier.View.Arms.Arms.Match`: Fable `Match(read: 'inst -> 'u, arms: 'u -> TemplateView<'inst>, ?lowered: obj) : TemplateView<'inst>`; .NET `Match(read: 'inst -> 'u, [<ReflectedDefinition>] arms: Expr<'u -> TemplateView<'inst>>, [<CallerFilePath>] ?file: string, [<CallerLineNumber>] ?line: int) : TemplateView<'inst>`.
  - `module internal Ranvier.View.Arms.Diagnostics`: literals `reflection`, `notLiteral`, `noPlugin`, `noArm`, `unsupported`; `outsideReader: string -> string`; `fieldOutsideReader: field: string -> case: string -> string`; `otherLambda: variable: string -> lambda: string -> instance: string -> string`; `notObject: string -> string`.
  - `Ranvier.View.Arms.BuildTimeReadException(variable: string, message: string)` with `Variable: string`; `Ranvier.View.Arms.ArmsMatchException(message: string)` (.NET only).
  - `module internal Ranvier.View.Arms.Errors`: `noArm: unit -> 'a`, `noPlugin: unit -> 'a`.
  - `module internal Ranvier.View.Arms.Cells`: `type Cells`; `create: unit -> Cells`; `enter: Cells -> Setup -> instance: obj -> target: int -> values: (unit -> obj array) -> unit`; `values: Cells -> instance: obj -> target: int -> obj array`.
  - `module internal Ranvier.View.Arms.Site`: `create: Cells.Cells -> read: ('inst -> 'u) -> select: ('u -> int * obj array) -> bodies: TemplateView<'inst> array -> TemplateView<'inst>`.
  - `module internal Ranvier.View.Arms.Quoted` (.NET): `type At = { File: string; Line: int }`; `lower: Cells.Cells -> At -> Expr<'u -> TemplateView<'inst>> -> ('u -> int * obj array) * TemplateView<'inst> array`.
  - Plugin: `Ranvier.View.Arms.FablePlugin.CaseArmsAttribute`; `Shape.children`, `Shape.map`, `Shape.exists`, `Shape.refersTo`, `Shape.refersOutsideLambdas`, `Shape.firstReference`; `Lower.siteName: SourceLocation option -> string`; `Lower.site: PluginHelper -> read: Expr -> arms: Expr -> SourceLocation option -> Expr option`.
  - The packed contract (Ruling 4): `lowered` is `fun (values: Func<obj, int, obj array>) -> (select, bodies)`.

- [ ] **Step 1: Record the Playground explicit bundle before any change**

From `examples/Fable.Ranvier.Playground`:

```bash
npm ci
npm run compile:tests
node benchmarks/frameworks/sugar-size.mjs
cp ../../docs/research/framework-performance/investigation/view-sugar-v2/bundle.json C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-sugar-size-before.json
git checkout -- ../../docs/research/framework-performance/investigation/view-sugar-v2/bundle.json
```

Expected: exit 0; the copied file holds an `explicit` object with `raw`, `gzip` and `brotli`.

- [ ] **Step 2: Write the discovery app (the failing test)**

`experiments/Ranvier.View.Arms/Checks/Discovery/Discovery.fsproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- An app that opts into Arms.Match the documented way: a project reference and the .targets. -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Main.fs" />
    <ProjectReference Include="../../Ranvier.View.Arms.fsproj" />
    <PackageReference Update="FSharp.Core" Version="10.1.401" />
  </ItemGroup>
  <Import Project="../../Ranvier.View.Arms.targets" />
</Project>
```

`experiments/Ranvier.View.Arms/Checks/Discovery/.gitignore`:

```
out/
# Fable writes the referenced Ranvier.View.Arms modules beside the project.
*.js
```

`experiments/Ranvier.View.Arms/Checks/Discovery/Main.fs`:

```fsharp
/// <summary>One Arms.Match site rendered on the server for three states, one line per state.</summary>
module Discovery.Main

open global.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms

type Load =
    | Idle
    | Loading of percent: int
    | Loaded of text: string * count: int

let loader =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "loader") {
            Arms.Match (
                (fun (state: Signal<Load>) -> state.Value),
                function
                | Idle -> span () { "Not started" }
                | Loading percent -> progress (Attrs.attribute ("max", "100"), Attrs.attribute ("value", fun _ -> Some (string percent))) { }
                | Loaded (text, count) ->
                    pre () {
                        fun _ -> text
                        " x"
                        fun _ -> string count
                    }
            )
        })

let private omit =
    { Server.Options.defaults with
        Policy = Omit
    }

[<EntryPoint>]
let main _ =
    for label, state in [ "Idle", Idle; "Loading 40", Loading 40; "Loaded done 3", Loaded ("done", 3) ] do
        let graph = new Graph ()
        let signal = graph.Run (fun () -> Api.createSignal state)
        printfn "%s|%s" label (Server.render omit loader signal)

    0
```

- [ ] **Step 3: Run it to verify it fails**

Run: `dotnet run --project experiments/Ranvier.View.Arms/Checks/Discovery/Discovery.fsproj -c Release`
Expected: FAIL, the referenced project `../../Ranvier.View.Arms.fsproj` does not exist.

- [ ] **Step 4: Create the plugin project**

`experiments/Ranvier.View.Arms.FablePlugin/Ranvier.View.Arms.FablePlugin.fsproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- The Fable plugin that lowers Arms.Match calls; it runs inside the dotnet fickl process. -->
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <!-- Copies Fable.AST.dll beside the plugin, where Ranvier.View.Arms.targets references it. -->
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../Ranvier.View.Arms/Diagnostics.fs" Link="Diagnostics.fs" />
    <Compile Include="Shape.fs" />
    <Compile Include="Lower.fs" />
    <Compile Include="Plugin.fs" />
    <PackageReference Include="Fable.AST" Version="5.0.0" />
    <PackageReference Update="FSharp.Core" Version="10.1.203" />
  </ItemGroup>
</Project>
```

`experiments/Ranvier.View.Arms.FablePlugin/Shape.fs` (the probe's walker with the full expression set; final in this task):

```fsharp
/// <summary>The children of a Fable expression and the function that rebuilds it from new children.</summary>
module internal Ranvier.View.Arms.FablePlugin.Shape

open Fable.AST
open Fable.AST.Fable

let private valueShape (kind: ValueKind) (r: SourceLocation option) : Expr list * (Expr list -> Expr) =
    match kind with
    | NewArray (ArrayValues values, t, k) -> values, (fun xs -> Value (NewArray (ArrayValues xs, t, k), r))
    | NewArray (ArrayAlloc size, t, k) -> [ size ], (fun xs -> Value (NewArray (ArrayAlloc xs.Head, t, k), r))
    | NewArray (ArrayFrom source, t, k) -> [ source ], (fun xs -> Value (NewArray (ArrayFrom xs.Head, t, k), r))
    | NewTuple (values, s) -> values, (fun xs -> Value (NewTuple (xs, s), r))
    | NewRecord (values, e, g) -> values, (fun xs -> Value (NewRecord (xs, e, g), r))
    | NewAnonymousRecord (values, names, g, s) -> values, (fun xs -> Value (NewAnonymousRecord (xs, names, g, s), r))
    | NewUnion (values, tag, e, g) -> values, (fun xs -> Value (NewUnion (xs, tag, e, g), r))
    | NewOption (Some value, t, s) -> [ value ], (fun xs -> Value (NewOption (Some xs.Head, t, s), r))
    | NewList (Some (head, tail), t) -> [ head; tail ], (fun xs -> Value (NewList (Some (xs[0], xs[1]), t), r))
    | StringTemplate (tag, parts, values) -> values, (fun xs -> Value (StringTemplate (tag, parts, xs), r))
    | _ -> [], (fun _ -> Value (kind, r))

let private callShape (info: CallInfo) : Expr list * (Expr list -> CallInfo) =
    Option.toList info.ThisArg @ info.Args,
    fun xs ->
        match info.ThisArg with
        | Some _ ->
            { info with
                ThisArg = Some xs.Head
                Args = xs.Tail
            }
        | None -> { info with Args = xs }

/// <summary>The direct subexpressions of an expression and its rebuilder.</summary>
let children (expr: Expr) : Expr list * (Expr list -> Expr) =
    match expr with
    | Lambda (arg, body, n) -> [ body ], (fun xs -> Lambda (arg, xs.Head, n))
    | Delegate (args, body, n, tags) -> [ body ], (fun xs -> Delegate (args, xs.Head, n, tags))
    | Let (id, value, body) -> [ value; body ], (fun xs -> Let (id, xs[0], xs[1]))
    | LetRec (bindings, body) ->
        (List.map snd bindings) @ [ body ],
        (fun xs -> LetRec (List.map2 (fun (id, _) x -> id, x) bindings (List.take bindings.Length xs), List.last xs))
    | Call (callee, info, t, r) ->
        let args, rebuild = callShape info
        callee :: args, (fun xs -> Call (xs.Head, rebuild xs.Tail, t, r))
    | Emit (info, t, r) ->
        let args, rebuild = callShape info.CallInfo
        args, (fun xs -> Emit ({ info with CallInfo = rebuild xs }, t, r))
    | CurriedApply (f, args, t, r) -> f :: args, (fun xs -> CurriedApply (xs.Head, xs.Tail, t, r))
    | IfThenElse (c, a, b, r) -> [ c; a; b ], (fun xs -> IfThenElse (xs[0], xs[1], xs[2], r))
    | DecisionTree (tree, targets) ->
        tree :: List.map snd targets, (fun xs -> DecisionTree (xs.Head, List.map2 (fun (ids, _) x -> ids, x) targets xs.Tail))
    | DecisionTreeSuccess (index, values, t) -> values, (fun xs -> DecisionTreeSuccess (index, xs, t))
    | Sequential xs -> xs, Sequential
    | Get (x, ExprGet key, t, r) -> [ x; key ], (fun xs -> Get (xs[0], ExprGet xs[1], t, r))
    | Get (x, kind, t, r) -> [ x ], (fun xs -> Get (xs.Head, kind, t, r))
    | Set (x, ExprSet key, t, value, r) -> [ x; key; value ], (fun xs -> Set (xs[0], ExprSet xs[1], t, xs[2], r))
    | Set (x, kind, t, value, r) -> [ x; value ], (fun xs -> Set (xs[0], kind, t, xs[1], r))
    | Operation (Unary (op, a), tags, t, r) -> [ a ], (fun xs -> Operation (Unary (op, xs[0]), tags, t, r))
    | Operation (Binary (op, a, b), tags, t, r) -> [ a; b ], (fun xs -> Operation (Binary (op, xs[0], xs[1]), tags, t, r))
    | Operation (Logical (op, a, b), tags, t, r) -> [ a; b ], (fun xs -> Operation (Logical (op, xs[0], xs[1]), tags, t, r))
    | Test (x, kind, r) -> [ x ], (fun xs -> Test (xs.Head, kind, r))
    | TypeCast (x, t) -> [ x ], (fun xs -> TypeCast (xs.Head, t))
    | Value (kind, r) -> valueShape kind r
    | WhileLoop (c, body, r) -> [ c; body ], (fun xs -> WhileLoop (xs[0], xs[1], r))
    | ForLoop (id, a, b, body, up, r) -> [ a; b; body ], (fun xs -> ForLoop (id, xs[0], xs[1], xs[2], up, r))
    | TryCatch (body, catch, finalizer, r) ->
        body
        :: (Option.toList (Option.map snd catch)
            @ Option.toList finalizer),
        (fun xs ->
            let rest = xs.Tail
            TryCatch (xs.Head, Option.map (fun (id, _) -> id, rest.Head) catch, Option.map (fun _ -> List.last rest) finalizer, r))
    | Extended (Throw (Some e, t), r) -> [ e ], (fun xs -> Extended (Throw (Some xs.Head, t), r))
    | Extended (Curry (e, arity), r) -> [ e ], (fun xs -> Extended (Curry (xs.Head, arity), r))
    | ObjectExpr (members, t, baseCall) ->
        (List.map (fun (m: ObjectExprMember) -> m.Body) members)
        @ Option.toList baseCall,
        (fun xs ->
            let bodies = List.take members.Length xs
            ObjectExpr (List.map2 (fun (m: ObjectExprMember) b -> { m with Body = b }) members bodies, t, List.tryItem members.Length xs))
    | _ -> [], (fun _ -> expr)

/// <summary>Rewrites top-down: <c>f</c> returns a replacement, or None to descend into the children.</summary>
let rec map (f: Expr -> Expr option) (expr: Expr) : Expr =
    match f expr with
    | Some replaced -> replaced
    | None ->
        match children expr with
        | [], _ -> expr
        | xs, rebuild -> rebuild (List.map (map f) xs)

let rec exists (f: Expr -> bool) (expr: Expr) : bool =
    f expr
    || (fst (children expr) |> List.exists (exists f))

let refersTo (names: Set<string>) (expr: Expr) =
    exists
        (function
        | IdentExpr id -> names.Contains id.Name
        | _ -> false)
        expr

/// <summary>Whether the expression refers to one of <c>names</c> outside every lambda and delegate.</summary>
let rec refersOutsideLambdas (names: Set<string>) (expr: Expr) =
    match expr with
    | IdentExpr id -> names.Contains id.Name
    | Lambda _
    | Delegate _ -> false
    | _ ->
        fst (children expr)
        |> List.exists (refersOutsideLambdas names)

let rec firstReference (names: Set<string>) (expr: Expr) : Ident option =
    match expr with
    | IdentExpr id when names.Contains id.Name -> Some id
    | _ ->
        fst (children expr)
        |> List.tryPick (firstReference names)
```

`experiments/Ranvier.View.Arms.FablePlugin/Lower.fs` (the probe's rules with the packed contract of Ruling 4; Task 2 replaces this file):

```fsharp
/// <summary>The lowering of an Arms.Match call into a target selector and one case body per target.</summary>
module internal Ranvier.View.Arms.FablePlugin.Lower

open Fable
open Fable.AST
open Fable.AST.Fable
open Ranvier.View.Arms

let ident (name: string) (t: Type) : Ident =
    {
        Name = name
        Type = t
        IsMutable = false
        IsThisArgument = false
        IsCompilerGenerated = true
        Range = None
        IsInlineIfLambda = false
    }

let private int32 (value: int) =
    Value (NumberConstant (NumberValue.Int32 value, NumberInfo.Empty), None)

let private emit (macro: string) (args: Expr list) (r: SourceLocation option) =
    Emit (
        {
            Macro = macro
            IsStatement = false
            CallInfo = CallInfo.Create (args = args)
        },
        Any,
        r
    )

/// <summary>Whether <c>value</c> is a field, tuple item or case payload of a controlled identifier.</summary>
let rec private extracts (control: Set<string>) (value: Expr) =
    match value with
    | IdentExpr id -> control.Contains id.Name
    | Get (inner, (UnionField _ | TupleIndex _ | FieldGet _ | OptionValue | ListHead | ListTail), _, _)
    | TypeCast (inner, _) -> extracts control inner
    | _ -> false

/// <summary>Rebuilds the decision part of <c>tree</c>, replacing each leaf by <c>leaf bound body</c> in walk order.</summary>
let mapLeaves (control: Set<string>) (leaf: Ident list -> Expr -> Expr) (tree: Expr) : Expr =
    let rec walk (control: Set<string>) (bound: Ident list) (expr: Expr) =
        match expr with
        | DecisionTree (decision, targets) ->
            DecisionTree (
                decision,
                [
                    for ids, body in targets -> ids, walk (control + set [ for id in ids -> id.Name ]) (bound @ ids) body
                ]
            )
        | IfThenElse (condition, whenTrue, whenFalse, r) when Shape.refersTo control condition ->
            let whenTrue = walk control bound whenTrue
            IfThenElse (condition, whenTrue, walk control bound whenFalse, r)
        | Let (id, value, body) when extracts control value -> Let (id, value, walk (control.Add id.Name) (bound @ [ id ]) body)
        | body -> leaf bound body

    walk control [] tree

/// <summary>
/// The suffix of a site's emitted names: the line and column of the first available range, in the order given (the
/// call, its arms, its read).
/// </summary>
let siteName (ranges: SourceLocation option list) : string option =
    ranges
    |> List.tryPick id
    |> Option.map (fun r -> sprintf "L%dC%d" r.start.line r.start.column)

/// <summary>The packed function of a call, or None after reporting a rejection.</summary>
let site (helper: PluginHelper) (read: Expr) (arms: Expr) (r: SourceLocation option) : Expr option =
    match arms, siteName [ r; arms.Range; read.Range ] with
    | Lambda _, None ->
        helper.LogError Diagnostics.noRange
        None
    | Lambda (matched, tree, _), Some name ->

        let instanceType =
            match read.Type with
            | LambdaType (instance, _) -> instance
            | other -> other

        let control = Set.singleton matched.Name
        let leaves = System.Collections.Generic.List<Ident list * Expr>()

        mapLeaves
            control
            (fun bound body ->
                leaves.Add (bound, body)
                body)
            tree
        |> ignore

        let valuesFn = ident ("armsValues_" + name) Any

        let selector =
            let position = ref 0

            let pick (bound: Ident list) (_: Expr) =
                let target = position.Value
                position.Value <- target + 1
                let values = [ for id in bound @ [ matched ] -> IdentExpr id ]
                Value (NewTuple ([ int32 target; Value (NewArray (ArrayValues values, Any, MutableArray), None) ], false), None)

            Lambda (matched, mapLeaves control pick tree, None)

        let rewrite (target: int) (bound: Ident list, body: Expr) =
            let slots = bound @ [ matched ]
            let names = set [ for id in slots -> id.Name ]

            let lowered =
                body
                |> Shape.map (fun expr ->
                    match expr with
                    | Lambda (argument, inner, n) when
                        argument.Type = instanceType
                        && Shape.refersTo names inner
                        ->
                        let values = ident ("armsRead_" + name) Any
                        let fresh = [ for id in slots -> id, ident (sprintf "%s_%s" id.Name name) id.Type ]

                        let renamed =
                            inner
                            |> Shape.map (function
                                | IdentExpr id ->
                                    fresh
                                    |> List.tryFind (fun (slot, _) -> slot.Name = id.Name)
                                    |> Option.map (snd >> IdentExpr)
                                | _ -> None)

                        let prologue =
                            List.foldBack
                                (fun (position, (_, local: Ident)) acc ->
                                    Let (local, Get (IdentExpr values, ExprGet (int32 position), local.Type, None), acc))
                                (List.indexed fresh)
                                renamed

                        let current =
                            emit "$0($1, $2)" [ IdentExpr valuesFn; IdentExpr argument; int32 target ] None

                        Some (Lambda (argument, Let (values, current, prologue), n))
                    | _ -> None)

            let outsideReaders =
                body
                |> Shape.map (fun expr ->
                    match expr with
                    | Lambda (argument, _, _) when argument.Type = instanceType -> Some (Value (UnitConstant, None))
                    | _ -> None)

            match Shape.firstReference names outsideReaders with
            | Some id ->
                let at =
                    match id.Range with
                    | Some _ -> id.Range
                    | None -> r

                helper.LogError (Diagnostics.outsideReader id.DisplayName, ?range = at)
            | None -> ()

            lowered

        let bodies = [ for target, leaf in Seq.indexed leaves -> rewrite target leaf ]

        Some (
            Lambda (
                valuesFn,
                Value (
                    NewTuple (
                        [
                            selector
                            Value (NewArray (ArrayValues bodies, tree.Type, MutableArray), None)
                        ],
                        false
                    ),
                    None
                ),
                None
            )
        )
    | _ ->
        helper.LogError (Diagnostics.notLiteral, ?range = r)
        None
```

`experiments/Ranvier.View.Arms.FablePlugin/Plugin.fs` (final in this task):

```fsharp
namespace Ranvier.View.Arms.FablePlugin

open Fable
open Fable.AST.Fable

[<assembly: ScanForPlugins>]
do ()

module internal Calls =
    let private isMatch (helper: PluginHelper) (info: CallInfo) =
        match info.MemberRef with
        | Some ref -> (helper.GetMember ref).FullName = "Ranvier.View.Arms.Arms.Match"
        | None -> false

    /// <summary>The lowered form of a call of <c>Arms.Match</c>, or None to keep the call.</summary>
    let lower (helper: PluginHelper) (expr: Expr) : Expr option =
        match expr with
        | Call (callee, info, t, r) when isMatch helper info ->
            match info.Args with
            | [ read; arms ] ->
                Lower.site helper read arms r
                |> Option.map (fun packed ->
                    Call (
                        callee,
                        { info with
                            Args = [ read; Value (Null Any, None); packed ]
                        },
                        t,
                        r
                    ))
            | _ -> None
        | _ -> None

/// <summary>Placed on <c>Arms.Match</c>: Fable lowers every call of it; the attribute is required only on the member.</summary>
type CaseArmsAttribute() =
    inherit MemberDeclarationPluginAttribute()
    override _.FableMinimumVersion = "5.0"

    override _.Transform(_: PluginHelper, _: File, decl: MemberDecl) = decl

    override _.TransformCall(helper: PluginHelper, _: MemberFunctionOrValue, expr: Expr) =
        Calls.lower helper expr
        |> Option.defaultValue expr
```

- [ ] **Step 5: Create the runtime project**

`experiments/Ranvier.View.Arms/Ranvier.View.Arms.fsproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net10.0;net8.0</TargetFrameworks>
    <OutputType>Library</OutputType>
    <IsPackable>false</IsPackable>
    <FSharpCoreImplicitPackageVersion>8.0.100</FSharpCoreImplicitPackageVersion>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Diagnostics.fs" />
    <Compile Include="Errors.fs" />
    <Compile Include="Cells.fs" />
    <Compile Include="Site.fs" />
    <Compile Include="Quoted.fs" />
    <Compile Include="Arms.fs" />
    <ProjectReference Include="../Ranvier.View.Portable/Ranvier.View.Portable.fsproj" />
    <InternalsVisibleTo Include="RepresentationChecks" />
  </ItemGroup>
</Project>
```

`experiments/Ranvier.View.Arms/Ranvier.View.Arms.targets`:

```xml
<Project>
  <!--
  The Arms.Match opt-in for a project compiled by Fable. The plugin assembly must exist before `dotnet fickl` runs:
  dotnet build experiments/Ranvier.View.Arms.FablePlugin/Ranvier.View.Arms.FablePlugin.fsproj -c Release
  -->
  <PropertyGroup>
    <RanvierArmsPluginDir Condition="'$(RanvierArmsPluginDir)' == ''">$(MSBuildThisFileDirectory)../Ranvier.View.Arms.FablePlugin/bin/Release/netstandard2.0/</RanvierArmsPluginDir>
  </PropertyGroup>
  <!-- RANVIER_ARMS_PLUGIN selects the plugin-lowered Arms.Match; without it a Fable call of Arms.Match fails with AM005. -->
  <PropertyGroup Condition="'$(FABLE_COMPILER)' == 'true'">
    <DefineConstants>$(DefineConstants);RANVIER_ARMS_PLUGIN</DefineConstants>
  </PropertyGroup>
  <ItemGroup Condition="'$(FABLE_COMPILER)' == 'true'">
    <Reference Include="$(RanvierArmsPluginDir)Ranvier.View.Arms.FablePlugin.dll" />
    <!-- Fable.AST.dll: the base type of the plugin attribute on Arms.Match. -->
    <Reference Include="$(RanvierArmsPluginDir)Fable.AST.dll" />
  </ItemGroup>
  <Target Name="RanvierArmsPluginExists" BeforeTargets="ResolveAssemblyReferences" Condition="'$(FABLE_COMPILER)' == 'true'">
    <Error Condition="!Exists('$(RanvierArmsPluginDir)Ranvier.View.Arms.FablePlugin.dll')" Text="Ranvier.View.Arms.FablePlugin.dll is missing from $(RanvierArmsPluginDir). Build the plugin first: dotnet build experiments/Ranvier.View.Arms.FablePlugin/Ranvier.View.Arms.FablePlugin.fsproj -c Release" />
  </Target>
</Project>
```

`experiments/Ranvier.View.Arms/Diagnostics.fs` (final in this task; the plugin links it):

```fsharp
/// <summary>The texts of the Arms.Match diagnostics, shared by the runtime and the Fable plugin.</summary>
module internal Ranvier.View.Arms.Diagnostics

[<Literal>]
let reflection =
    "Arms.Match lowers a quotation through reflection and System.Linq.Expressions. Use matchWith with armOf in trimmed or NativeAOT apps."

/// <summary>AM001.</summary>
let outsideReader (variable: string) =
    "Arms.Match: '"
    + variable
    + "' is read outside a reader of the instance. Read it inside a lambda over the instance, such as (fun _ -> "
    + variable
    + ")."

/// <summary>AM001b: the compiler replaced the pattern variable by a read of the case field.</summary>
let fieldOutsideReader (field: string) (case: string) =
    "Arms.Match: the field '"
    + field
    + "' of '"
    + case
    + "' is read outside a reader of the instance. Read it inside a lambda over the instance, such as (fun _ -> "
    + field
    + ")."

/// <summary>AM002.</summary>
let otherLambda (variable: string) (lambda: string) (instance: string) =
    "Arms.Match: '"
    + variable
    + "' is read inside a lambda over '"
    + lambda
    + "', which is not the instance type '"
    + instance
    + "'. Pass the value to the nested view through its items or props."

/// <summary>AM003.</summary>
[<Literal>]
let notLiteral =
    "Arms.Match takes a function literal as its second argument, such as (function | Case x -> ...)."

/// <summary>AM004.</summary>
let notObject (instance: string) =
    "Arms.Match: the instance type '"
    + instance
    + "' is not an object type. An arm site keys each arm's payload by its instance; use a record, class, union or function instance."

/// <summary>AM005.</summary>
[<Literal>]
let noPlugin =
    "Arms.Match needs the Ranvier.View.Arms Fable plugin; import Ranvier.View.Arms.targets in the project compiled by Fable."

/// <summary>AM006.</summary>
[<Literal>]
let noArm =
    "Arms.Match: no arm of this site is mounted for the instance this reader received. A lambda over the instance type inside a case body runs with the arm's instance."

/// <summary>AM007.</summary>
[<Literal>]
let unsupported = "Arms.Match: this pattern form is not supported."

/// <summary>A call with no source location, from which the plugin names its emitted locals.</summary>
[<Literal>]
let noRange =
    "Arms.Match: the call carries no source location, from which the emitted names of its site derive."
```

`experiments/Ranvier.View.Arms/Errors.fs`:

```fsharp
namespace Ranvier.View.Arms

open System

#if !FABLE_COMPILER
/// <summary>A pattern variable read outside a reader of the instance, raised when a .NET site is built.</summary>
type BuildTimeReadException(variable: string, message: string) =
    inherit InvalidOperationException(message)

    /// <summary>The pattern variable, or the union field when the compiler removed the binding.</summary>
    member _.Variable = variable

/// <summary>A site rejected by the .NET lowering: AM003, AM004, AM006 or AM007.</summary>
type ArmsMatchException(message: string) =
    inherit InvalidOperationException(message)
#endif

module internal Errors =
    /// <summary>AM006.</summary>
    let noArm () : 'a =
#if FABLE_COMPILER
        raise (InvalidOperationException Diagnostics.noArm)
#else
        raise (ArmsMatchException Diagnostics.noArm)
#endif

    /// <summary>AM005.</summary>
    let noPlugin () : 'a =
        raise (InvalidOperationException Diagnostics.noPlugin)
```

`experiments/Ranvier.View.Arms/Cells.fs` (single slot per instance and target, the probe's behaviour; Task 5 replaces the slots):

```fsharp
/// <summary>A site's lookup from an instance to the payload readers of its mounted arms, by target.</summary>
module internal Ranvier.View.Arms.Cells

open Ranvier.View.Foundation.Representation

/// <summary>The payload reader of each target's mounted arm for an instance.</summary>
[<AllowNullLiteral>]
type Slots() =
    let readers = ResizeArray<unit -> obj array>()

    member _.Set(target: int, read: unit -> obj array) =
        while readers.Count <= target do
            readers.Add Unchecked.defaultof<_>

        readers[target] <- read

    member _.Get(target: int) : unit -> obj array =
        if target < readers.Count then
            readers[target]
        else
            Unchecked.defaultof<_>

#if FABLE_COMPILER
open Fable.Core

[<Emit("new WeakMap()")>]
let private newTable () : obj = jsNative

[<Emit("$0.get($1)")>]
let private tryFind (table: obj) (instance: obj) : Slots = jsNative

[<Emit("$0.set($1, $2)")>]
let private store (table: obj) (instance: obj) (slots: Slots) : unit = jsNative

let private slotsOf (table: obj) (instance: obj) : Slots =
    match tryFind table instance with
    | null ->
        let slots = Slots ()
        store table instance slots
        slots
    | slots -> slots
#else
open System.Runtime.CompilerServices

let private newTable () : obj =
    ConditionalWeakTable<obj, Slots>() :> obj

let private tryFind (table: obj) (instance: obj) : Slots =
    match (table :?> ConditionalWeakTable<obj, Slots>).TryGetValue instance with
    | true, slots -> slots
    | _ -> null

let private slotsOf (table: obj) (instance: obj) : Slots =
    (table :?> ConditionalWeakTable<obj, Slots>).GetValue(instance, fun _ -> Slots ())
#endif

[<Sealed>]
type Cells internal (table: obj) =
    member internal _.Table = table

let create () =
    Cells (newTable ())

/// <summary>Registers the arm's payload reader for the instance and target.</summary>
let enter (cells: Cells) (_: Setup) (instance: obj) (target: int) (values: unit -> obj array) : unit =
    (slotsOf cells.Table instance).Set(target, values)

/// <summary>The bound values of the reader's own arm: the pattern variables, then the matched value.</summary>
let values (cells: Cells) (instance: obj) (target: int) : obj array =
    match tryFind cells.Table instance with
    | null -> Errors.noArm ()
    | slots ->
        let read = slots.Get target

        if isNull (box read) then Errors.noArm () else read ()
```

`experiments/Ranvier.View.Arms/Site.fs` (final in this task):

```fsharp
/// <summary>The runtime shape every Arms.Match site lowers to, on both targets.</summary>
module internal Ranvier.View.Arms.Site

open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar

/// <summary>
/// One arm per body, built when the site is built. The same target keeps its arm and replaces its bound values;
/// another target remounts. A body's instance is the enclosing instance.
/// </summary>
let create (cells: Cells.Cells) (read: 'inst -> 'u) (select: 'u -> int * obj array) (bodies: TemplateView<'inst> array) : TemplateView<'inst> =
    let definitions =
        bodies
        |> Array.mapi (fun target body ->
            Component.define
                (fun setup (current: unit -> 'inst * obj array) ->
                    let instance = fst (current ())
                    Cells.enter cells setup (box instance) target (fun () -> snd (current ()))
                    instance)
                (fun _ -> body))

    matchWith id (fun instance ->
        let target, values = select (read instance)
        arm definitions[target] (instance, values))
```

`experiments/Ranvier.View.Arms/Quoted.fs` (the probe's rules; Task 3 replaces this file):

```fsharp
/// <summary>The .NET lowering of a quoted arms function into a target selector and one case body per target.</summary>
module internal Ranvier.View.Arms.Quoted

#if !FABLE_COMPILER
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Linq.RuntimeHelpers
open Ranvier.View.Foundation.Template

/// <summary>The call site of an Arms.Match call, prefixed to its exception messages.</summary>
type At = { File: string; Line: int }

let private located (at: At) (message: string) =
    sprintf "%s(%d): %s" at.File at.Line message

let private refersTo (control: Set<Var>) (expr: Expr) =
    expr.GetFreeVars () |> Seq.exists control.Contains

let rec private extracts (control: Set<Var>) (value: Expr) =
    match value with
    | Var var -> control.Contains var
    | PropertyGet (Some inner, _, [])
    | TupleGet (inner, _)
    | Coerce (inner, _) -> extracts control inner
    | _ -> false

let private mapLeaves (control: Set<Var>) (leaf: Var list -> Expr -> Expr) (expr: Expr) : Expr =
    let rec walk (control: Set<Var>) (bound: Var list) (expr: Expr) =
        match expr with
        | IfThenElse (condition, whenTrue, whenFalse) when refersTo control condition ->
            let whenTrue = walk control bound whenTrue
            Expr.IfThenElse (condition, whenTrue, walk control bound whenFalse)
        | Let (var, value, body) when extracts control value -> Expr.Let (var, value, walk (Set.add var control) (bound @ [ var ]) body)
        | body -> leaf bound body

    walk control [] expr

let private unboxMethod =
    match <@ unbox<int> null @> with
    | Call (_, info, _) -> info.GetGenericMethodDefinition ()
    | _ -> failwith "unbox"

let private getMethod =
    match <@ (null: obj array).[0] @> with
    | Call (_, info, _) -> info
    | _ -> failwith "get"

let private valuesMethod =
    match <@ Cells.values Unchecked.defaultof<Cells.Cells> null 0 @> with
    | Call (_, info, _) -> info
    | _ -> failwith "Cells.values"

/// <summary>Lowers <c>arms</c> into a target selector and one body per target, for the site's cells.</summary>
let lower (cells: Cells.Cells) (at: At) (arms: Expr<'u -> TemplateView<'inst>>) : ('u -> int * obj array) * TemplateView<'inst> array =
    let matched, tree =
        match arms with
        | Lambda (matched, tree) -> matched, tree
        | _ -> raise (ArmsMatchException (located at Diagnostics.notLiteral))

    let control = Set.singleton matched
    let leaves = ResizeArray<Var list * Expr>()

    mapLeaves
        control
        (fun bound body ->
            leaves.Add (bound, body)
            body)
        tree
    |> ignore

    let selector =
        let position = ref 0

        let pick (bound: Var list) (_: Expr) =
            let target = position.Value
            position.Value <- target + 1

            Expr.NewTuple
                [
                    Expr.Value target
                    Expr.NewArray (typeof<obj>, [ for var in bound @ [ matched ] -> Expr.Coerce (Expr.Var var, typeof<obj>) ])
                ]

        Expr.Lambda (matched, mapLeaves control pick tree)

    let rewrite (target: int) (bound: Var list, body: Expr) =
        let slots = bound @ [ matched ]
        let slotSet = Set.ofList slots

        let rec visit (expr: Expr) =
            match expr with
            | Lambda (argument, inner) when
                argument.Type = typeof<'inst>
                && refersTo slotSet inner
                ->
                let values = Var ("values", typeof<obj array>)

                let substituted =
                    inner.Substitute (fun var ->
                        match List.tryFindIndex ((=) var) slots with
                        | Some field ->
                            Some (
                                Expr.Call (unboxMethod.MakeGenericMethod var.Type, [ Expr.Call (getMethod, [ Expr.Var values; Expr.Value field ]) ])
                            )
                        | None -> None)

                let current =
                    Expr.Call (
                        valuesMethod,
                        [
                            Expr.Value (box cells, typeof<Cells.Cells>)
                            Expr.Coerce (Expr.Var argument, typeof<obj>)
                            Expr.Value target
                        ]
                    )

                Expr.Lambda (argument, Expr.Let (values, current, substituted))
            | ExprShape.ShapeVar _ -> expr
            | ExprShape.ShapeLambda (var, inner) -> Expr.Lambda (var, visit inner)
            | ExprShape.ShapeCombination (shape, args) -> ExprShape.RebuildShapeCombination (shape, List.map visit args)

        let lowered = visit body

        match
            lowered.GetFreeVars ()
            |> Seq.tryFind slotSet.Contains
        with
        | Some var -> raise (BuildTimeReadException (var.Name, located at (Diagnostics.outsideReader var.Name)))
        | None -> lowered

    let bodies =
        [|
            for target, leaf in Seq.indexed leaves ->
                rewrite target leaf
                |> LeafExpressionConverter.EvaluateQuotation
                |> unbox<TemplateView<'inst>>
        |]

    LeafExpressionConverter.EvaluateQuotation selector
    |> unbox<'u -> int * obj array>,
    bodies
#endif
```

`experiments/Ranvier.View.Arms/Arms.fs`:

```fsharp
namespace Ranvier.View.Arms

open System
open System.Diagnostics.CodeAnalysis
open Ranvier.View.Foundation.Template
#if !FABLE_COMPILER
open Microsoft.FSharp.Quotations
#endif

/// <summary>Arm sites written as a plain F# match: one arm per match target.</summary>
[<AbstractClass; Sealed>]
type Arms =
#if FABLE_COMPILER && RANVIER_ARMS_PLUGIN
    /// <summary>One arm per match target of <c>arms</c>; the Ranvier.View.Arms Fable plugin supplies <c>lowered</c>.</summary>
    [<Ranvier.View.Arms.FablePlugin.CaseArms>]
    static member Match(read: 'inst -> 'u, arms: 'u -> TemplateView<'inst>, ?lowered: obj) : TemplateView<'inst> =
        match lowered with
        | Some packed ->
            let cells = Cells.create ()
            let lower = unbox<Func<Func<obj, int, obj array>, obj>> packed

            let select, bodies =
                unbox<('u -> int * obj array) * TemplateView<'inst> array>(
                    lower.Invoke (Func<obj, int, obj array>(fun instance target -> Cells.values cells instance target))
                )

            Site.create cells read select bodies
        | None -> Errors.noPlugin ()
#else
#if FABLE_COMPILER
    /// <summary>AM005: the project compiled by Fable does not import Ranvier.View.Arms.targets.</summary>
    [<Obsolete(Diagnostics.noPlugin, true)>]
    static member Match(read: 'inst -> 'u, arms: 'u -> TemplateView<'inst>) : TemplateView<'inst> =
        Errors.noPlugin ()
#else
    /// <summary>One arm per match target of <c>arms</c>, lowered from its quotation when the site is built.</summary>
    [<RequiresUnreferencedCode(Diagnostics.reflection); RequiresDynamicCode(Diagnostics.reflection)>]
    static member Match
        (
            read: 'inst -> 'u,
            [<ReflectedDefinition>] arms: Expr<'u -> TemplateView<'inst>>,
            [<System.Runtime.CompilerServices.CallerFilePath>] ?file: string,
            [<System.Runtime.CompilerServices.CallerLineNumber>] ?line: int
        ) : TemplateView<'inst> =
        let at: Quoted.At =
            {
                File = defaultArg file ""
                Line = defaultArg line 0
            }

        let cells = Cells.create ()
        let select, bodies = Quoted.lower cells at arms
        Site.create cells read select bodies
#endif
#endif
```

Add to `Ranvier.slnx`, after the `/experiments/Ranvier.View.Anime/` folder:

```xml
  <Folder Name="/experiments/Ranvier.View.Arms/">
    <Project Path="experiments/Ranvier.View.Arms/Ranvier.View.Arms.fsproj" />
    <Project Path="experiments/Ranvier.View.Arms.FablePlugin/Ranvier.View.Arms.FablePlugin.fsproj" />
    <Project Path="experiments/Ranvier.View.Arms/Checks/Discovery/Discovery.fsproj" />
  </Folder>
```

- [ ] **Step 6: Build both projects**

```bash
dotnet build experiments/Ranvier.View.Arms.FablePlugin/Ranvier.View.Arms.FablePlugin.fsproj -c Release
dotnet build experiments/Ranvier.View.Arms/Ranvier.View.Arms.fsproj -c Release
```

Expected: both succeed; `experiments/Ranvier.View.Arms.FablePlugin/bin/Release/netstandard2.0/Ranvier.View.Arms.FablePlugin.dll` exists.

- [ ] **Step 7: Run the discovery app on .NET and under Fable, and compare**

```bash
dotnet run --project experiments/Ranvier.View.Arms/Checks/Discovery/Discovery.fsproj -c Release > C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-discovery-net.txt
dotnet fickl experiments/Ranvier.View.Arms/Checks/Discovery/Discovery.fsproj --outDir experiments/Ranvier.View.Arms/Checks/Discovery/out -c Release --noCache
node experiments/Ranvier.View.Arms/Checks/Discovery/out/Main.js > C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-discovery-fable.txt
diff --strip-trailing-cr C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-discovery-net.txt C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-discovery-fable.txt
rtk grep -c "armsValues_L" experiments/Ranvier.View.Arms/Checks/Discovery/out/Main.js
test ! -e experiments/Ranvier.View.Arms/Checks/Discovery/bin/Release/net10.0/Ranvier.View.Arms.FablePlugin.dll
test ! -e experiments/Ranvier.View.Arms/Checks/Discovery/bin/Release/net10.0/Fable.AST.dll
```

Expected: `diff` prints nothing; the .NET file is exactly

```
Idle|<!--[r--><div class="loader"><!--[a:220727aa--><span>Not started</span><!--]--></div><!--]-->
Loading 40|<!--[r--><div class="loader"><!--[a:a8ebc344--><progress max="100" value="40"></progress><!--]--></div><!--]-->
Loaded done 3|<!--[r--><div class="loader"><!--[a:646a1db6--><pre>done<!--!--> x<!--!-->3</pre><!--]--></div><!--]-->
```

and `Main.js` names `armsValues_L` at least once, and the .NET output holds neither the plugin nor `Fable.AST.dll`. If `dotnet fickl` fails with FS0101 naming AM005, Fable did not set `FABLE_COMPILER` as an MSBuild property during cracking: remove the `Condition`s from the `.targets` (the spec's fallback), rerun this step, and record it in Step 8. If it fails with a plugin load error, record the message and stop: the netstandard2.0 load is the phase-1 question.

- [ ] **Step 8: Record the discovery results**

`docs/research/2026-10-21-arms-match.md` (later tasks append sections):

```markdown
# Arms.Match: measurements

Measurements of the `Arms.Match` implementation ([spec](../superpowers/specs/2026-10-21-arms-match-design.md),
[plan](../superpowers/plans/2026-10-21-arms-match.md)). Fable 5.18.0, net10.0, Node 26.

## Plugin discovery

From `experiments/Ranvier.View.Arms/Checks/Discovery`, an app with a project reference to `Ranvier.View.Arms` and an
import of `Ranvier.View.Arms.targets`:

- **Discovery through a project reference and the `.targets`:** the plugin lowers the app's `Arms.Match` call; the
  emitted `Main.js` holds the `armsValues_L…` prologues.
- **`FABLE_COMPILER` during project cracking:** set; the `.targets` reference is conditional, and a .NET build carries
  no plugin assembly.
- **netstandard2.0 plugin in Fable 5.18:** loads (Fable.AST 5.0.0 compile reference, FSharp.Core 10.1.203).
- **Server HTML:** the three states render byte-identical HTML on .NET (quotation path) and under Fable (plugin path).
```

Write each bullet as observed in Step 7; when the fallback of Step 7 applied, the second bullet reads "**`FABLE_COMPILER` during project cracking:** not set; the `.targets` reference is unconditional, and .NET builds carry the unused plugin assembly."

- [ ] **Step 9: Format, run the phase gates, commit**

```bash
dotnet fantomas experiments/Ranvier.View.Arms experiments/Ranvier.View.Arms.FablePlugin
git add experiments/Ranvier.View.Arms experiments/Ranvier.View.Arms.FablePlugin Ranvier.slnx docs/research/2026-10-21-arms-match.md
git diff --cached --check
git commit -F C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-task-1-commit.txt
git push origin HEAD:feat/arms-match
```

Message subject: `feat(arms): scaffold Arms.Match runtime and Fable plugin`. The phase gates run before the commit.

---

## Phase 2: lowering rules

Start of phase: `git fetch origin && git merge origin/master` (message file with the trailers), then the task gates.

### Task 2: The plugin's lowering rules, stable names and diagnostics, under plugin tests

**Files:**
- Modify: `experiments/Ranvier.View.Arms.FablePlugin/Lower.fs` (replaced whole)
- Create: `experiments/Ranvier.View.Arms.FablePlugin/Tests/PluginTests.fsx`, `Tests/sourcemap.mjs`
- Create: `experiments/Ranvier.View.Arms.FablePlugin/Tests/NoTargets/NoTargets.fsproj`, `Tests/NoTargets/Site.fs` (a project without the `.targets`)
- Create: `experiments/Ranvier.View.Arms.FablePlugin/Tests/Fixtures/PluginFixtures.fsproj`, `Types.fs`, `Sites.fs`, `Bad/AM001.fs`, `Bad/AM001b.fs`, `Bad/AM002.fs`, `Bad/AM003.fs`, `Bad/AM004.fs`, `Bad/AM007.fs`
- Create: `experiments/Ranvier.View.Arms.FablePlugin/Tests/expected/Sites.js` (written by `--update`)
- Modify: `.fantomasignore` (the fixtures keep the line numbers the tests assert, and their emitted names)
- Test: `experiments/Ranvier.View.Arms.FablePlugin/Tests/PluginTests.fsx`

**Interfaces:**
- Consumes: Task 1's `Shape` functions, `Diagnostics`, the packed contract, `Lower.siteName`.
- Produces: `Lower.site` with the same signature, implementing the decision rule of Ruling 2, throw leaves, `Lambda` and `Delegate` readers, AM001, AM001b, AM002, AM003, AM004, AM007; `Lower.show: PluginHelper -> Type -> string`. The emitted shape of a site, which Task 9 measures: `Arms_Match(read, null, (armsValues_L<line>C<col>) => [select, [body, ...]])`, each reader starting `const armsRead_L<line>C<col> = armsValues_L<line>C<col>(instance, target)`.

- [ ] **Step 1: Write the fixtures project and the good sites**

`experiments/Ranvier.View.Arms.FablePlugin/Tests/Fixtures/PluginFixtures.fsproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Compiled by PluginTests.fsx with dotnet fickl only; each Bad file compiles under its own define. -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Types.fs" />
    <Compile Include="Sites.fs" />
    <Compile Include="Bad/AM001.fs" />
    <Compile Include="Bad/AM001b.fs" />
    <Compile Include="Bad/AM002.fs" />
    <Compile Include="Bad/AM003.fs" />
    <Compile Include="Bad/AM004.fs" />
    <Compile Include="Bad/AM007.fs" />
    <ProjectReference Include="../../../Ranvier.View.Arms/Ranvier.View.Arms.fsproj" />
    <PackageReference Update="FSharp.Core" Version="10.1.401" />
  </ItemGroup>
  <Import Project="../../../Ranvier.View.Arms/Ranvier.View.Arms.targets" />
</Project>
```

`Tests/Fixtures/Types.fs`:

```fsharp
module Fixtures.Types

type Load =
    | Idle
    | Loading of percent: int
    | Loaded of text: string * count: int

[<Struct>]
type Point = { X: int; Y: int }

type Panel =
    {
        State: global.Ranvier.Signal<Load>
        Open: global.Ranvier.Signal<bool>
    }
```

`Tests/Fixtures/Sites.fs`:

```fsharp
/// <summary>Sites whose emitted JavaScript the plugin tests compare with expected/Sites.js.</summary>
module Fixtures.Sites

#nowarn "25"

open System
open global.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms
open Fixtures.Types

let read (state: Signal<Load>) = state.Value

/// <summary>An or-pattern, an inlined field, a literal pattern and a guard.</summary>
let rich =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            Arms.Match (
                read,
                function
                | Idle
                | Loading 0 -> span () { "Not started" }
                | Loading percent -> b () { fun _ -> string percent }
                | Loaded (_, 0) -> span () { "Empty" }
                | Loaded (text, count) when count > 99 -> b () { fun _ -> text }
                | Loaded (text, count) -> pre () { fun _ -> text + string count }
            )
        })

/// <summary>An incomplete match: the selector raises for Loaded.</summary>
let incomplete =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            Arms.Match (
                read,
                function
                | Idle -> span () { "Not started" }
                | Loading percent -> b () { fun _ -> string percent }
            )
        })

/// <summary>A delegate over the instance reading a pattern variable.</summary>
let held =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            Arms.Match (
                read,
                function
                | Loaded (text, _) ->
                    span (
                        Hooks.exit (
                            Func<_, _, _, _>(fun (_: Signal<Load>) (_: Fable.Core.TS.Dom.HTMLSpanElement) finish ->
                                if text = "" then finish ()
                                ignore)
                        )
                    ) {
                        "held"
                    }
                | _ -> span () { "Other" }
            )
        })

/// <summary>An if and a let at the top of case bodies, and a let of a view.</summary>
let bodies =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            Arms.Match (
                read,
                function
                | Loaded (_, n) when n > 100 -> if n > 1000 then b () { "huge" } else i () { "big" }
                | Loaded (t, 0) ->
                    let label = t + "!"
                    pre () { fun _ -> label }
                | Loaded (t, _) ->
                    let view = pre () { fun _ -> t }
                    view
                | _ -> span () { "Other" }
            )
        })

/// <summary>A site inside a case body reading the outer site's pattern variable.</summary>
let nested =
    Component.template
        (fun _ (panel: Panel) -> panel)
        (div () {
            Arms.Match (
                (fun (panel: Panel) -> panel.State.Value),
                function
                | Loaded (t, _) ->
                    section () {
                        Arms.Match (
                            (fun (panel: Panel) -> panel.Open.Value),
                            function
                            | true -> pre () { fun _ -> t }
                            | false -> span () { "closed" }
                        )
                    }
                | _ -> span () { "Other" }
            )
        })
```

- [ ] **Step 2: Write the bad fixtures**

Each file starts with the same eight lines, so the reported line numbers below hold. `Tests/Fixtures/Bad/AM001.fs`:

```fsharp
module Fixtures.Bad.AM001

#if BAD_AM001
open global.Ranvier
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms
open Fixtures.Types

let site =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            Arms.Match (
                (fun (state: Signal<Load>) -> state.Value),
                function
                | Loaded (text, count) when count > 0 -> span () { text }
                | _ -> span () { "Other" }
            )
        })
#endif
```

`Bad/AM001b.fs`: the same file with `AM001b` in the module name and the define (`#if BAD_AM001B`), and line 17 replaced by

```fsharp
                | Loaded (text, _) -> span () { text }
```

`Bad/AM002.fs`: `AM002`, `#if BAD_AM002`, line 17 replaced by

```fsharp
                | Loaded (text, count) when count > 0 -> ul () { forEach id (fun _ -> [| "a" |]) (li () { fun (item: unit -> string) -> item () + text }) }
```

`Bad/AM007.fs`: `AM007`, `#if BAD_AM007`, line 4 replaced by `open System` followed by `open global.Ranvier` (so every later line moves down one), and the first case replaced by

```fsharp
                | Loaded (text, count) when count > 0 -> (Console.WriteLine "x"; if text = "" then span () { "empty" } else span () { "full" })
```

which lands on line 18.

`Bad/AM003.fs`:

```fsharp
module Fixtures.Bad.AM003

#if BAD_AM003
open global.Ranvier
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms
open Fixtures.Types

let private arms =
    function
    | Idle -> span () { "Idle" }
    | _ -> span () { "Other" }

let site =
    Component.template (fun _ (state: Signal<Load>) -> state) (div () { Arms.Match ((fun (state: Signal<Load>) -> state.Value), arms) })
#endif
```

`Bad/AM004.fs` (five sites, one define each; the reported line is the `Arms.Match` call):

```fsharp
module Fixtures.Bad.AM004

open global.Ranvier
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms
open Fixtures.Types

#if BAD_AM004_INT
let site = Component.template (fun _ (n: int) -> n) (div () { Arms.Match ((fun (n: int) -> n), function 0 -> span () { "zero" } | _ -> span () { "other" }) })
#endif
#if BAD_AM004_STRING
let site = Component.template (fun _ (s: string) -> s) (div () { Arms.Match ((fun (s: string) -> s.Length), function 0 -> span () { "zero" } | _ -> span () { "other" }) })
#endif
#if BAD_AM004_UNIT
let site = Component.template (fun _ (u: unit) -> u) (div () { Arms.Match ((fun (_: unit) -> 0), function 0 -> span () { "zero" } | _ -> span () { "other" }) })
#endif
#if BAD_AM004_OPTION
let site = Component.template (fun _ (o: int option) -> o) (div () { Arms.Match ((fun (o: int option) -> defaultArg o 0), function 0 -> span () { "zero" } | _ -> span () { "other" }) })
#endif
#if BAD_AM004_STRUCT
let site = Component.template (fun _ (p: Point) -> p) (div () { Arms.Match ((fun (p: Point) -> p.X), function 0 -> span () { "zero" } | _ -> span () { "other" }) })
#endif
```

The five `let site` lines are lines 10, 13, 16, 19 and 22.

A project that references `Ranvier.View.Arms` and does not import the `.targets` (Decision 17: AM005 at the call, line 12, column 13). `experiments/Ranvier.View.Arms.FablePlugin/Tests/NoTargets/NoTargets.fsproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- An app that references Ranvier.View.Arms without importing Ranvier.View.Arms.targets: PluginTests.fsx expects the compile to fail. -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Site.fs" />
    <ProjectReference Include="../../../Ranvier.View.Arms/Ranvier.View.Arms.fsproj" />
    <PackageReference Update="FSharp.Core" Version="10.1.401" />
  </ItemGroup>
</Project>
```

`experiments/Ranvier.View.Arms.FablePlugin/Tests/NoTargets/Site.fs`:

```fsharp
module NoTargets.Site

open global.Ranvier
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms

let site =
    Component.template
        (fun _ (state: Signal<int option>) -> state)
        (div () {
            Arms.Match (
                (fun (state: Signal<int option>) -> state.Value),
                function
                | Some n -> b () { fun _ -> string n }
                | None -> span () { "none" }
            )
        })
```

Add to `.fantomasignore`:

```
# The plugin fixtures keep the lines and columns PluginTests.fsx asserts.
experiments/Ranvier.View.Arms.FablePlugin/Tests/Fixtures/
experiments/Ranvier.View.Arms.FablePlugin/Tests/NoTargets/
```

- [ ] **Step 3: Write the plugin tests**

`experiments/Ranvier.View.Arms.FablePlugin/Tests/PluginTests.fsx`:

```fsharp
// Compiles the plugin fixtures with dotnet fickl: the emitted sites against expected/Sites.js, and each bad fixture
// against its diagnostic (exit code 1, file, line, exact message). Needs the plugin built in Release.
//   dotnet fsi experiments/Ranvier.View.Arms.FablePlugin/Tests/PluginTests.fsx            checks
//   dotnet fsi experiments/Ranvier.View.Arms.FablePlugin/Tests/PluginTests.fsx --update   rewrites expected/Sites.js
// Exits 1 on a failure.
open System
open System.Diagnostics
open System.IO
open System.Text.RegularExpressions

let here = __SOURCE_DIRECTORY__
let repo = Path.GetFullPath (Path.Combine (here, "..", "..", ".."))
let project = Path.Combine (here, "Fixtures", "PluginFixtures.fsproj")
let update = fsi.CommandLineArgs |> Array.contains "--update"
let scratch = Path.Combine (Path.GetTempPath (), "ranvier-arms-plugin-tests")
let failures = ResizeArray<string>()

let check (name: string) (ok: bool) (detail: string) =
    if ok then
        printfn "PASS %s" name
    else
        failures.Add (name + ": " + detail)
        printfn "FAIL %s" name

/// Runs a process from the repository root; its exit code and its whole output.
let exec (file: string) (args: string list) =
    let info =
        ProcessStartInfo (file, args, WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true)

    use run = Process.Start info
    let output = run.StandardOutput.ReadToEndAsync ()
    let errors = run.StandardError.ReadToEndAsync ()
    run.WaitForExit ()
    run.ExitCode, output.Result + errors.Result

/// Runs dotnet fickl on a fixtures project with the defines and extra arguments, into its own folder.
let fableOf (fixtures: string) (name: string) (defines: string list) (extra: string list) =
    let outDir = Path.Combine (scratch, name)

    let code, log =
        exec
            "dotnet"
            ([
                "fickl"
                fixtures
                "--outDir"
                outDir
                "--configuration"
                "Release"
                "--noCache"
             ]
             @ (defines
                |> List.collect (fun define -> [ "--define"; define ]))
             @ extra)

    code, outDir, log

let fable name defines extra =
    fableOf project name defines extra

/// <summary>
/// Line endings unified, the fable-library folder's version dropped, and each site suffix (L<line>C<column>) replaced
/// by its ordinal in the file.
/// </summary>
let normalise (text: string) =
    let ordinals = Collections.Generic.Dictionary<string, int>()

    Regex.Replace (
        Regex.Replace (text.Replace ("\r\n", "\n"), @"fable-library-js\.\d+\.\d+\.\d+", "fable-library-js"),
        @"L\d+C\d+",
        fun found ->
            if not (ordinals.ContainsKey found.Value) then
                ordinals[found.Value] <- ordinals.Count + 1

            "S" + string ordinals[found.Value]
    )

let code, outDir, log = fable "good" [] [ "--sourceMaps" ]
check "the good fixtures compile" (code = 0) log

if code = 0 then
    let emitted = Path.Combine (outDir, "Sites.js")
    let expected = Path.Combine (here, "expected", "Sites.js")

    if update then
        Directory.CreateDirectory (Path.GetDirectoryName expected)
        |> ignore

        File.WriteAllText (expected, normalise (File.ReadAllText emitted))
        printfn "UPDATED %s" expected
    else
        check
            "the emitted sites equal expected/Sites.js"
            (File.Exists expected
             && normalise (File.ReadAllText emitted) = normalise (File.ReadAllText expected))
            ("diff " + emitted + " " + expected)

    // Each prologue maps back to the line of the reader it opens (spec, IDE experience).
    let mapped, mapLog =
        exec
            "node"
            [
                Path.Combine (here, "sourcemap.mjs")
                emitted
                Path.Combine (here, "Fixtures", "Sites.fs")
            ]

    check "each prologue maps to its reader's source line" (mapped = 0) mapLog

// The reported column is asserted where the range is the identifier or the call (AM001, AM003, AM004); for AM001b it is
// the field read, and for AM002 and AM007 the expression Fable holds, so only the line is asserted.
let bad =
    [
        "BAD_AM001",
        "AM001.fs",
        17,
        Some 67,
        "Arms.Match: 'text' is read outside a reader of the instance. Read it inside a lambda over the instance, such as (fun _ -> text)."
        "BAD_AM001B",
        "AM001b.fs",
        17,
        Some 48,
        "Arms.Match: 'text' is read outside a reader of the instance. Read it inside a lambda over the instance, such as (fun _ -> text)."
        "BAD_AM002",
        "AM002.fs",
        17,
        None,
        "Arms.Match: 'text' is read inside a lambda over 'unit -> string', which is not the instance type 'Signal<Load>'. Pass the value to the nested view through its items or props."
        "BAD_AM003", "AM003.fs", 16, Some 72, "Arms.Match takes a function literal as its second argument, such as (function | Case x -> ...)."
        "BAD_AM004_INT",
        "AM004.fs",
        10,
        Some 62,
        "Arms.Match: the instance type 'int' is not an object type. An arm site keys each arm's payload by its instance; use a record, class, union or function instance."
        "BAD_AM004_STRING",
        "AM004.fs",
        13,
        Some 65,
        "Arms.Match: the instance type 'string' is not an object type. An arm site keys each arm's payload by its instance; use a record, class, union or function instance."
        "BAD_AM004_UNIT",
        "AM004.fs",
        16,
        Some 63,
        "Arms.Match: the instance type 'unit' is not an object type. An arm site keys each arm's payload by its instance; use a record, class, union or function instance."
        "BAD_AM004_OPTION",
        "AM004.fs",
        19,
        Some 69,
        "Arms.Match: the instance type 'int option' is not an object type. An arm site keys each arm's payload by its instance; use a record, class, union or function instance."
        "BAD_AM004_STRUCT",
        "AM004.fs",
        22,
        Some 64,
        "Arms.Match: the instance type 'Point' is not an object type. An arm site keys each arm's payload by its instance; use a record, class, union or function instance."
        "BAD_AM007", "AM007.fs", 18, None, "Arms.Match: this pattern form is not supported."
    ]

/// Whether some line of the log holds the message at file(line,column), or at file(line, when the column is None.
let reportedAt (log: string) (file: string) (line: int) (column: int option) (message: string) =
    let at = Regex (Regex.Escape file + @"\((\d+),(\d+)\)")

    log.Split '\n'
    |> Array.exists (fun entry ->
        let found = at.Match entry

        entry.Contains message
        && found.Success
        && found.Groups[1].Value = string line
        && (column
            |> Option.forall (fun column -> found.Groups[2].Value = string column)))

for define, file, line, column, message in bad do
    let code, _, log = fable (define.ToLowerInvariant ()) [ define ] []

    check
        (sprintf "%s fails at %s(%d) with its message" define file line)
        (code = 1
         && reportedAt log file line column message)
        log

// A project that references Ranvier.View.Arms without importing the .targets fails at its call with AM005.
let missing, _, missingLog =
    fableOf (Path.Combine (here, "NoTargets", "NoTargets.fsproj")) "no-targets" [] []

check
    "a project without the .targets fails at its Arms.Match call with AM005"
    (missing = 1
     && reportedAt
         missingLog
         "Site.fs"
         12
         (Some 13)
         "This construct is deprecated. Arms.Match needs the Ranvier.View.Arms Fable plugin; import Ranvier.View.Arms.targets in the project compiled by Fable.")
    missingLog

if failures.Count > 0 then
    for failure in failures do
        eprintfn "%s" failure

    exit 1
```

`experiments/Ranvier.View.Arms.FablePlugin/Tests/sourcemap.mjs` (reads `@jridgewell/trace-mapping` from the Playground's `node_modules`; run `npm ci` there first):

```js
// Maps each Arms.Match prologue call (armsValues_L<line>C<column>(...)) of an emitted file back to its F# source line,
// and exits 1 unless every one lands inside a reader: on a line holding "fun" or "Func<", or at most two lines below one.
// Usage: node sourcemap.mjs <emitted .js> <F# source>   (the .js.map beside the .js; Fable --sourceMaps writes it)
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";

const require = createRequire(new URL("../../../examples/Fable.Ranvier.Playground/package.json", import.meta.url));
const { TraceMap, originalPositionFor } = require("@jridgewell/trace-mapping");
const [emitted, source] = process.argv.slice(2);
const map = new TraceMap(readFileSync(emitted + ".map", "utf8"));
const lines = readFileSync(source, "utf8").split(/\r?\n/);
const prologue = /armsValues_L\d+C\d+\(/g;
const opensReader = row => row >= 0 && /\bfun\b|Func</.test(lines[row] ?? "");
const misplaced = [];
let checked = 0;

readFileSync(emitted, "utf8").split("\n").forEach((text, index) => {
  for (const found of text.matchAll(prologue)) {
    checked++;
    const original = originalPositionFor(map, { line: index + 1, column: found.index });
    const at = original.line == null ? -1 : original.line - 1;
    if (![at, at - 1, at - 2].some(opensReader)) misplaced.push(`${index + 1}:${found.index} -> ${original.line}: ${(lines[at] ?? "").trim()}`);
  }
});

console.log(`${checked} prologues checked`);
if (checked === 0 || misplaced.length > 0) {
  console.error(checked === 0 ? "no prologue found" : misplaced.join("\n"));
  process.exit(1);
}
```

- [ ] **Step 4: Run the plugin tests to verify they fail**

```bash
dotnet build experiments/Ranvier.View.Arms.FablePlugin/Ranvier.View.Arms.FablePlugin.fsproj -c Release
dotnet fsi experiments/Ranvier.View.Arms.FablePlugin/Tests/PluginTests.fsx
```

Expected: FAIL. `the emitted sites equal expected/Sites.js` fails (no expectation yet); `BAD_AM001B`, `BAD_AM002`, every `BAD_AM004_*` and `BAD_AM007` fail (Task 1's lowering reports AM001 or nothing for them); the good fixtures may fail to compile with an AM001 on `bodies` (the `let view` leaf); the source-map check fails (Task 1's prologues carry no range). The no-targets check passes already: Task 1's review fixes added the obsolete overload.

- [ ] **Step 5: Replace `Lower.fs` with the full rules**

`experiments/Ranvier.View.Arms.FablePlugin/Lower.fs`:

```fsharp
/// <summary>The lowering of an Arms.Match call into a target selector and one case body per target.</summary>
module internal Ranvier.View.Arms.FablePlugin.Lower

open Fable
open Fable.AST
open Fable.AST.Fable
open Ranvier.View.Arms

let ident (name: string) (t: Type) : Ident =
    {
        Name = name
        Type = t
        IsMutable = false
        IsThisArgument = false
        IsCompilerGenerated = true
        Range = None
        IsInlineIfLambda = false
    }

let private int32 (value: int) =
    Value (NumberConstant (NumberValue.Int32 value, NumberInfo.Empty), None)

let private emit (macro: string) (args: Expr list) (r: SourceLocation option) =
    Emit (
        {
            Macro = macro
            IsStatement = false
            CallInfo = CallInfo.Create (args = args)
        },
        Any,
        r
    )

/// <summary>An F# rendering of a type, for diagnostics.</summary>
let rec show (helper: PluginHelper) (t: Type) : string =
    match t with
    | Type.Unit -> "unit"
    | Type.Boolean -> "bool"
    | Type.Char -> "char"
    | Type.String -> "string"
    | Type.Number (kind, _) ->
        match kind with
        | NumberKind.Int32 -> "int"
        | NumberKind.Float64 -> "float"
        | NumberKind.Decimal -> "decimal"
        | NumberKind.Int64 -> "int64"
        | NumberKind.Float32 -> "float32"
        | NumberKind.UInt8 -> "byte"
        | other -> (string other).ToLowerInvariant()
    | Type.Option (arg, false) -> atom helper arg + " option"
    | Type.Option (arg, true) -> atom helper arg + " voption"
    | Type.Array (arg, _) -> atom helper arg + " array"
    | Type.List arg -> atom helper arg + " list"
    | Type.Tuple (args, _) ->
        args
        |> List.map (atom helper)
        |> String.concat " * "
    | Type.LambdaType (domain, range) -> atom helper domain + " -> " + show helper range
    | Type.DeclaredType (ref, []) -> (helper.GetEntity ref).DisplayName
    | Type.DeclaredType (ref, args) ->
        (helper.GetEntity ref).DisplayName
        + "<"
        + (args
           |> List.map (show helper)
           |> String.concat ", ")
        + ">"
    | Type.GenericParam (name, _, _) -> "'" + name
    | _ -> "obj"

and private atom (helper: PluginHelper) (t: Type) =
    match t with
    | Type.LambdaType _
    | Type.Tuple _ -> "(" + show helper t + ")"
    | _ -> show helper t

/// <summary>Whether a value of the type is a JavaScript primitive or a .NET value type (AM004).</summary>
let private notObject (helper: PluginHelper) (t: Type) =
    match t with
    | Type.Unit
    | Type.Boolean
    | Type.Char
    | Type.String
    | Type.Number _
    | Type.Option _
    | Type.Tuple (_, true)
    | Type.AnonymousRecordType (_, _, true) -> true
    | Type.DeclaredType (ref, _) ->
        let entity = helper.GetEntity ref
        entity.IsValueType || entity.IsEnum
    | _ -> false

let private raising =
    set
        [
            "Microsoft.FSharp.Core.Operators.raise"
            "Microsoft.FSharp.Core.Operators.failwith"
            "Microsoft.FSharp.Core.Operators.invalidOp"
            "Microsoft.FSharp.Core.Operators.invalidArg"
        ]

/// <summary>Whether a case body is a raised exception, which builds no definition.</summary>
let private isThrow (helper: PluginHelper) (expr: Expr) =
    match expr with
    | Extended (Throw _, _) -> true
    | Call (_, { MemberRef = Some ref }, _, _) -> raising.Contains (helper.GetMember ref).FullName
    | _ -> false

/// <summary>A raised exception retyped as the selector's result.</summary>
let private retype (t: Type) (expr: Expr) =
    match expr with
    | Extended (Throw (e, _), r) -> Extended (Throw (e, t), r)
    | Call (callee, info, _, r) -> Call (callee, info, t, r)
    | other -> other

/// <summary>Whether <c>value</c> is a field, tuple item or case payload of a controlled identifier.</summary>
let rec private extracts (control: Set<string>) (value: Expr) =
    match value with
    | IdentExpr id -> control.Contains id.Name
    | Get (inner, (UnionField _ | TupleIndex _ | FieldGet _ | OptionValue | ListHead | ListTail), _, _)
    | TypeCast (inner, _) -> extracts control inner
    | _ -> false

/// <summary>Whether a <c>let</c> belongs to the selection (Ruling 2).</summary>
let private decides (control: Set<string>) (id: Ident) (value: Expr) =
    if id.IsCompilerGenerated then
        extracts control value
    else
        Shape.refersOutsideLambdas control value

/// <summary>Rebuilds the decision part of <c>tree</c>, replacing each leaf by <c>leaf bound body</c> in walk order.</summary>
let mapLeaves (control: Set<string>) (leaf: Ident list -> Expr -> Expr) (tree: Expr) : Expr =
    let rec walk (control: Set<string>) (bound: Ident list) (expr: Expr) =
        match expr with
        | DecisionTree (decision, targets) ->
            DecisionTree (
                decision,
                [
                    for ids, body in targets -> ids, walk (control + set [ for id in ids -> id.Name ]) (bound @ ids) body
                ]
            )
        | IfThenElse (condition, whenTrue, whenFalse, r) when Shape.refersOutsideLambdas control condition ->
            let whenTrue = walk control bound whenTrue
            IfThenElse (condition, whenTrue, walk control bound whenFalse, r)
        | Let (id, value, body) when decides control id value -> Let (id, value, walk (control.Add id.Name) (bound @ [ id ]) body)
        | body -> leaf bound body

    walk control [] tree

/// <summary>The range of a decision node that a wrapper at the root of a leaf hides from the walk (AM007).</summary>
let rec private hidden (control: Set<string>) (expr: Expr) : SourceLocation option option =
    match expr with
    | IfThenElse (condition, _, _, r) when Shape.refersOutsideLambdas control condition -> Some r
    | Let (id, value, _) when decides control id value -> Some id.Range
    | DecisionTree (decision, _) when Shape.refersOutsideLambdas control decision -> Some None
    | TypeCast (inner, _)
    | TryCatch (inner, _, _, _)
    | Let (_, _, inner) -> hidden control inner
    | Sequential xs when not xs.IsEmpty -> hidden control (List.last xs)
    | _ -> None

/// <summary>
/// The suffix of a site's emitted names: the line and column of the first available range, in the order given (the
/// call, its arms, its read).
/// </summary>
let siteName (ranges: SourceLocation option list) : string option =
    ranges
    |> List.tryPick id
    |> Option.map (fun r -> sprintf "L%dC%d" r.start.line r.start.column)

/// <summary>
/// Whether a lambda is the compiler's eta-expansion of a function value, <c>fun x -> f x</c>, which Fable passes for a
/// named arms function (AM003).
/// </summary>
let private isEtaExpansion (expr: Expr) =
    match expr with
    | Lambda (argument, CurriedApply (f, [ IdentExpr passed ], _, _), _)
    | Lambda (argument, Call (f, { Args = [ IdentExpr passed ] }, _, _), _) ->
        argument.IsCompilerGenerated
        && passed.Name = argument.Name
        && not (Shape.refersTo (Set.singleton argument.Name) f)
    | _ -> false

/// <summary>The parameter of a lambda or delegate over the instance type.</summary>
let private readerArgument (instanceType: Type) (expr: Expr) : Ident option =
    match expr with
    | Lambda (argument, _, _) when argument.Type = instanceType -> Some argument
    | Delegate (argument :: _, _, _, _) when argument.Type = instanceType -> Some argument
    | _ -> None

let private bodyOf (expr: Expr) =
    match expr with
    | Lambda (_, body, _)
    | Delegate (_, body, _, _) -> body
    | other -> other

let private withBody (expr: Expr) (body: Expr) =
    match expr with
    | Lambda (argument, _, n) -> Lambda (argument, body, n)
    | Delegate (args, _, n, tags) -> Delegate (args, body, n, tags)
    | other -> other

/// <summary>A pattern-variable reference left outside every reader, with the lambda it sits in.</summary>
type private Found =
    {
        Ident: Ident
        Field: (UnionFieldInfo * SourceLocation option) option
        Over: Type option
    }

let rec private find (names: Set<string>) (over: Type option) (expr: Expr) : Found option =
    match expr with
    | Get (IdentExpr id, UnionField info, _, r) when names.Contains id.Name ->
        Some
            {
                Ident = id
                Field = Some (info, r)
                Over = over
            }
    | IdentExpr id when names.Contains id.Name ->
        Some
            {
                Ident = id
                Field = None
                Over = over
            }
    | Lambda (argument, body, _) when
        argument.Type = Type.Unit
        || argument.IsCompilerGenerated
        ->
        find names over body
    | Delegate (argument :: _, body, _, _) when argument.IsCompilerGenerated -> find names over body
    | Lambda (argument, body, _) -> find names (Some argument.Type) body
    | Delegate (argument :: _, body, _, _) -> find names (Some argument.Type) body
    | _ ->
        fst (Shape.children expr)
        |> List.tryPick (find names over)

/// <summary>Logs AM001, AM001b or AM002 for a reference outside every reader.</summary>
let private report (helper: PluginHelper) (matched: Ident) (instanceType: Type) (fallback: SourceLocation option) (found: Found) =
    let at (r: SourceLocation option) =
        match r with
        | Some _ -> r
        | None -> fallback

    let field =
        match found.Field with
        | Some (info, r) when found.Ident.Name = matched.Name ->
            let case =
                (helper.GetEntity info.Entity).UnionCases
                |> List.item info.CaseIndex

            Some ((case.UnionCaseFields |> List.item info.FieldIndex).Name, case.Name, r)
        | _ -> None

    match found.Over, field with
    | Some over, Some (name, _, r) -> helper.LogError (Diagnostics.otherLambda name (show helper over) (show helper instanceType), ?range = at r)
    | Some over, None ->
        helper.LogError (Diagnostics.otherLambda found.Ident.DisplayName (show helper over) (show helper instanceType), ?range = at found.Ident.Range)
    | None, Some (name, case, r) -> helper.LogError (Diagnostics.fieldOutsideReader name case, ?range = at r)
    | None, None -> helper.LogError (Diagnostics.outsideReader found.Ident.DisplayName, ?range = at found.Ident.Range)

/// <summary>The packed function of a call, or None after reporting AM003 or AM004.</summary>
let site (helper: PluginHelper) (read: Expr) (arms: Expr) (r: SourceLocation option) : Expr option =
    let instanceType =
        match read.Type with
        | LambdaType (instance, _) -> instance
        | other -> other

    match arms, siteName [ r; arms.Range; read.Range ] with
    | _ when notObject helper instanceType ->
        helper.LogError (Diagnostics.notObject (show helper instanceType), ?range = r)
        None
    | _ when isEtaExpansion arms ->
        helper.LogError (Diagnostics.notLiteral, ?range = r)
        None
    | Lambda _, None ->
        helper.LogError Diagnostics.noRange
        None
    | Lambda (matched, tree, _), Some name ->
        let control = Set.singleton matched.Name
        let leaves = System.Collections.Generic.List<Ident list * Expr>()

        mapLeaves
            control
            (fun bound body ->
                leaves.Add (bound, body)
                body)
            tree
        |> ignore

        // The target of each leaf; a raising leaf has none.
        let targets =
            leaves
            |> Seq.fold
                (fun (acc, next) (_, body) ->
                    if isThrow helper body then
                        None :: acc, next
                    else
                        Some next :: acc, next + 1)
                ([], 0)
            |> fst
            |> List.rev
            |> Array.ofList

        let valuesFn = ident ("armsValues_" + name) Any

        let selectType =
            Type.Tuple (
                [
                    Type.Number (NumberKind.Int32, NumberInfo.Empty)
                    Type.Array (Any, MutableArray)
                ],
                false
            )

        let selector =
            let position = ref 0

            let pick (bound: Ident list) (body: Expr) =
                let leaf = position.Value
                position.Value <- leaf + 1

                match targets[leaf] with
                | None -> retype selectType body
                | Some target ->
                    let values = [ for id in bound @ [ matched ] -> IdentExpr id ]
                    Value (NewTuple ([ int32 target; Value (NewArray (ArrayValues values, Any, MutableArray), None) ], false), None)

            Lambda (matched, mapLeaves control pick tree, None)

        let rewrite (target: int) (bound: Ident list) (body: Expr) =
            let slots = bound @ [ matched ]
            let names = set [ for id in slots -> id.Name ]

            let lowered =
                body
                |> Shape.map (fun expr ->
                    match readerArgument instanceType expr with
                    | Some argument when Shape.refersTo names (bodyOf expr) ->
                        let inner = bodyOf expr

                        let used =
                            slots
                            |> List.filter (fun id -> Shape.refersTo (Set.singleton id.Name) inner)

                        let fresh = [ for id in used -> id, ident (sprintf "%s_%s" id.Name name) id.Type ]

                        let renamed =
                            inner
                            |> Shape.map (function
                                | IdentExpr id ->
                                    fresh
                                    |> List.tryFind (fun (slot, _) -> slot.Name = id.Name)
                                    |> Option.map (snd >> IdentExpr)
                                | _ -> None)

                        let values = ident ("armsRead_" + name) Any

                        let prologue =
                            List.foldBack
                                (fun (slot: Ident, local: Ident) acc ->
                                    let position =
                                        slots
                                        |> List.findIndex (fun s -> s.Name = slot.Name)

                                    Let (local, Get (IdentExpr values, ExprGet (int32 position), local.Type, None), acc))
                                fresh
                                renamed

                        let range =
                            Shape.firstReference names inner
                            |> Option.bind _.Range

                        let current =
                            emit "$0($1, $2)" [ IdentExpr valuesFn; IdentExpr argument; int32 target ] range

                        Some (withBody expr (Let (values, current, prologue)))
                    | _ -> None)

            match hidden names body with
            | Some range ->
                helper.LogError (
                    Diagnostics.unsupported,
                    ?range =
                        (match range with
                         | Some _ -> range
                         | None -> r)
                )
            | None ->
                let outsideReaders =
                    body
                    |> Shape.map (fun expr ->
                        match readerArgument instanceType expr with
                        | Some _ -> Some (Value (UnitConstant, None))
                        | None -> None)

                match find names None outsideReaders with
                | Some found -> report helper matched instanceType r found
                | None -> ()

            lowered

        let bodies =
            [
                for leaf, (bound, body) in Seq.indexed leaves do
                    match targets[leaf] with
                    | Some target -> yield rewrite target bound body
                    | None -> ()
            ]

        Some (
            Lambda (
                valuesFn,
                Value (
                    NewTuple (
                        [
                            selector
                            Value (NewArray (ArrayValues bodies, tree.Type, MutableArray), None)
                        ],
                        false
                    ),
                    None
                ),
                None
            )
        )
    | _ ->
        helper.LogError (Diagnostics.notLiteral, ?range = r)
        None
```

- [ ] **Step 6: Inspect the emitted sites, then write the expectation**

```bash
dotnet build experiments/Ranvier.View.Arms.FablePlugin/Ranvier.View.Arms.FablePlugin.fsproj -c Release
dotnet fsi experiments/Ranvier.View.Arms.FablePlugin/Tests/PluginTests.fsx --update
```

Read `experiments/Ranvier.View.Arms.FablePlugin/Tests/expected/Sites.js` and confirm, before keeping it:

- every site is `Arms_Match(<read>, null, (armsValues_L<line>C<col>) => [<selector>, [<bodies>]])`;
- `rich` has five bodies and its selector returns `[0, …]` for `Idle` and `Loading 0`;
- `incomplete` has two bodies and its selector holds a `throw` (or the raise call) for `Loaded`;
- `held`'s exit delegate starts with `armsRead_L…= armsValues_L…(`, and `text` inside it is read from that array;
- `bodies`: the `n > 1000` and `n <= 1000` branches are two targets; `label` is a bound value of the second target; the `let view` target's reader is rewritten;
- `nested`: the inner site's reader of `t` starts with the outer prologue and holds the inner one;
- no body (outside the selector lambdas) reads `.fields[`.

A deviation is a defect in Step 5: fix it, rebuild and regenerate.

- [ ] **Step 7: Run the plugin tests to verify they pass**

Run: `dotnet fsi experiments/Ranvier.View.Arms.FablePlugin/Tests/PluginTests.fsx`
Expected: every line `PASS`, exit 0. If `BAD_AM001B` reports AM001 `'text'` instead (FCS bound the variable for that shape), keep the fixture, change its expected message to the AM001 text, and ledger it as a ruling; if `BAD_AM001` reports AM001b, do the converse.

- [ ] **Step 8: Re-run the discovery comparison of Task 1 Step 7**

Expected: unchanged output, `diff` empty.

- [ ] **Step 9: Format and commit**

```bash
dotnet fantomas experiments/Ranvier.View.Arms.FablePlugin/Lower.fs
git add experiments/Ranvier.View.Arms.FablePlugin .fantomasignore
git diff --cached --check
git commit -F C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-task-2-commit.txt
git push origin HEAD:feat/arms-match
```

Message subject: `feat(arms): plugin lowering rules and diagnostics under plugin tests`.

---

### Task 3: The .NET lowering rules, rejections and the one-expression evaluation

Merge `origin/master` before starting (this task edits `RepresentationChecks.*`).

**Files:**
- Modify: `experiments/Ranvier.View.Arms/Quoted.fs` (replaced whole), `experiments/Ranvier.View.Arms/Arms.fs` (one line)
- Create: `experiments/Ranvier.View.Arms/Checks/ArmsLoweringChecks.fs`
- Modify: `experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj`, `experiments/Ranvier.View.Foundation/RepresentationChecks.fsx`
- Modify: `docs/research/2026-10-21-arms-match.md` (a "Lowering cost" section)
- Test: `experiments/Ranvier.View.Arms/Checks/ArmsLoweringChecks.fs`, run by `RepresentationChecks`

**Interfaces:**
- Consumes: Task 1's `Quoted.At`, `Cells.values`, `Diagnostics`, the exception types.
- Produces: `Quoted.lower` (same signature) with the rules of Task 2 on quotations; `Quoted.checkInstance: At -> System.Type -> unit` (AM004); `Quoted.show: System.Type -> string`; `ArmsLoweringChecks.cases: (string * (unit -> unit)) array`, run by `RepresentationChecks.fsx`.

- [ ] **Step 1: Write the failing checks**

`experiments/Ranvier.View.Arms/Checks/ArmsLoweringChecks.fs`:

```fsharp
/// <summary>The .NET lowering of Arms.Match: each rejection with its text and location, accepted forms, and the lowering cost.</summary>
module ArmsLoweringChecks

#nowarn "25"

open System
open System.IO
open global.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms

type Load =
    | Idle
    | Loading of percent: int
    | Loaded of text: string * count: int

[<Struct>]
type Point = { X: int; Y: int }

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

let private here = Path.Combine (__SOURCE_DIRECTORY__, __SOURCE_FILE__)

let private omit =
    { Server.Options.defaults with
        Policy = Omit
    }

let private read (state: Signal<Load>) =
    state.Value

/// <summary>What building a site raised: the kind and variable, and the message after the location prefix.</summary>
let private raised (build: unit -> obj) =
    let unprefixed (message: string) =
        if message.StartsWith (here + "(") then
            message.Substring (message.IndexOf "): " + 3)
        else
            "unprefixed: " + message

    try
        build () |> ignore
        "none", ""
    with
    | :? BuildTimeReadException as error -> "BuildTimeRead " + error.Variable, unprefixed error.Message
    | :? ArmsMatchException as error -> "ArmsMatch", unprefixed error.Message

let private notObject (name: string) =
    "ArmsMatch",
    "Arms.Match: the instance type '"
    + name
    + "' is not an object type. An arm site keys each arm's payload by its instance; use a record, class, union or function instance."

let private am001 () =
    equal
        ("BuildTimeRead text",
         "Arms.Match: 'text' is read outside a reader of the instance. Read it inside a lambda over the instance, such as (fun _ -> text).")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (state: Signal<Load>) -> state)
                    (div () {
                        Arms.Match (
                            read,
                            function
                            | Loaded (text, _) -> span () { text }
                            | _ -> span () { "Other" }
                        )
                    })
            )))

let private am002 () =
    equal
        ("BuildTimeRead text",
         "Arms.Match: 'text' is read inside a lambda over 'unit -> string', which is not the instance type 'Signal<Load>'. Pass the value to the nested view through its items or props.")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (state: Signal<Load>) -> state)
                    (div () {
                        Arms.Match (
                            read,
                            function
                            | Loaded (text, _) -> ul () { forEach id (fun _ -> [| "a" |]) (li () { fun (item: unit -> string) -> item () + text }) }
                            | _ -> span () { "Other" }
                        )
                    })
            )))

let private am003 () =
    let arms: Load -> TemplateView<Signal<Load>> =
        function
        | Idle -> span () { "Idle" }
        | _ -> span () { "Other" }

    equal
        ("ArmsMatch", "Arms.Match takes a function literal as its second argument, such as (function | Case x -> ...).")
        (raised (fun () -> box (Component.template (fun _ (state: Signal<Load>) -> state) (div () { Arms.Match (read, arms) }))))

let private am004 () =
    equal
        (notObject "int")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (n: int) -> n)
                    (div () {
                        Arms.Match (
                            (fun (n: int) -> n),
                            function
                            | 0 -> span () { "zero" }
                            | _ -> span () { "other" }
                        )
                    })
            )))

    equal
        (notObject "string")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (s: string) -> s)
                    (div () {
                        Arms.Match (
                            (fun (s: string) -> s.Length),
                            function
                            | 0 -> span () { "zero" }
                            | _ -> span () { "other" }
                        )
                    })
            )))

    equal
        (notObject "unit")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (u: unit) -> u)
                    (div () {
                        Arms.Match (
                            (fun (_: unit) -> 0),
                            function
                            | 0 -> span () { "zero" }
                            | _ -> span () { "other" }
                        )
                    })
            )))

    equal
        (notObject "int option")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (o: int option) -> o)
                    (div () {
                        Arms.Match (
                            (fun (o: int option) -> defaultArg o 0),
                            function
                            | 0 -> span () { "zero" }
                            | _ -> span () { "other" }
                        )
                    })
            )))

    equal
        (notObject "Point")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (p: Point) -> p)
                    (div () {
                        Arms.Match (
                            (fun (p: Point) -> p.X),
                            function
                            | 0 -> span () { "zero" }
                            | _ -> span () { "other" }
                        )
                    })
            )))

let private am007 () =
    equal
        ("ArmsMatch", "Arms.Match: this pattern form is not supported.")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (state: Signal<Load>) -> state)
                    (div () {
                        Arms.Match (
                            read,
                            function
                            | Loaded (text, _) ->
                                (Console.Write ""

                                 if text = "" then
                                     span () { "empty" }
                                 else
                                     span () { "full" })
                            | _ -> span () { "Other" }
                        )
                    })
            )))

let private render (definition: Component<Signal<Load>>) (state: Load) =
    use graph = new Graph ()
    let signal = graph.Run (fun () -> Api.createSignal state)
    Server.render omit definition signal

/// <summary>An incomplete match builds; the unmatched case raises at the region read, as a plain match does.</summary>
let private incompleteRaisesAtRead () =
    let site =
        Component.template
            (fun _ (state: Signal<Load>) -> state)
            (div () {
                Arms.Match (
                    read,
                    function
                    | Idle -> span () { "Not started" }
                    | Loading percent -> b () { fun _ -> string percent }
                )
            })

    equal "<!--[r--><div><!--[a:220727aa--><span>Not started</span><!--]--></div><!--]-->" (render site Idle)

    let unmatched =
        try
            render site (Loaded ("x", 1)) |> ignore
            false
        with :? MatchFailureException ->
            true

    equal true unmatched

/// <summary>A delegate over the instance reading a pattern variable is rewritten as a reader.</summary>
let private delegateReader () =
    let site =
        Component.template
            (fun _ (state: Signal<Load>) -> state)
            (div () {
                Arms.Match (
                    read,
                    function
                    | Loaded (text, _) ->
                        span (
                            Hooks.exit (
                                Func<_, _, _, _>(fun (_: Signal<Load>) (_: Fable.Core.TS.Dom.HTMLSpanElement) finish ->
                                    if text = "" then
                                        finish ()

                                    ignore)
                            )
                        ) {
                            "held"
                        }
                    | _ -> span () { "Other" }
                )
            })

    equal true ((render site (Loaded ("a", 1))).Contains "<span>held</span>")

/// <summary>An if and a let at the top of case bodies, and a let of a view.</summary>
let private bodyForms () =
    let site =
        Component.template
            (fun _ (state: Signal<Load>) -> state)
            (div () {
                Arms.Match (
                    read,
                    function
                    | Loaded (_, n) when n > 100 -> if n > 1000 then b () { "huge" } else i () { "big" }
                    | Loaded (t, 0) ->
                        let label = t + "!"
                        pre () { fun _ -> label }
                    | Loaded (t, _) ->
                        let view = pre () { fun _ -> t }
                        view
                    | _ -> span () { "Other" }
                )
            })

    let hugeArm = armOf (b () { "huge" })
    let bigArm = armOf (i () { "big" })
    let textArm = armOf (pre () { fun (p: unit -> string) -> p () })
    let otherArm = armOf (span () { "Other" })

    let twin =
        Component.template
            (fun _ (state: Signal<Load>) -> state)
            (div () {
                matchWith read (function
                    | Loaded (_, n) when n > 1000 -> arm hugeArm ()
                    | Loaded (_, n) when n > 100 -> arm bigArm ()
                    | Loaded (t, 0) -> arm textArm (t + "!")
                    | Loaded (t, _) -> arm textArm t
                    | _ -> arm otherArm ())
            })

    for state in
        [
            Loaded ("x", 2000)
            Loaded ("x", 200)
            Loaded ("x", 0)
            Loaded ("y", 3)
            Idle
        ] do
        equal (state, render twin state) (state, render site state)

/// <summary>Milliseconds for the first and the mean of 200 further lowerings of a five-target site.</summary>
let private loweringCost () =
    let lower () =
        Arms.Match (
            read,
            function
            | Idle
            | Loading 0 -> span () { "Not started" }
            | Loading percent -> b () { fun _ -> string percent }
            | Loaded (_, 0) -> span () { "Empty" }
            | Loaded (text, count) when count > 99 -> b () { fun _ -> text }
            | Loaded (text, count) -> pre () { fun _ -> text + string count }
        )
        |> ignore

    let watch = Diagnostics.Stopwatch.StartNew ()
    lower ()
    let first = watch.Elapsed.TotalMilliseconds
    watch.Restart ()

    for _ in 1..200 do
        lower ()

    first, watch.Elapsed.TotalMilliseconds / 200.0

let private loweringBudget () =
    let first, mean = loweringCost ()
    printfn "lowering|first=%.2fms mean=%.3fms" first mean

    if
        Environment.GetCommandLineArgs ()
        |> Array.contains "--timing"
    then
        equal (true, true) (first <= 10.0, mean <= 4.0)

let cases: (string * (unit -> unit)) array =
    [|
        "a pattern variable as static text raises AM001 with the call's location", am001
        "a pattern variable in a row reader raises AM002", am002
        "a non-literal arms function raises AM003", am003
        "int, string, unit, option and struct instances raise AM004", am004
        "a decision hidden under a sequence raises AM007", am007
        "an incomplete match builds and raises MatchFailureException for the unmatched case", incompleteRaisesAtRead
        "a delegate over the instance reading a pattern variable is a reader", delegateReader
        "if, let and a let of a view at the top of case bodies", bodyForms
        "the lowering of a five-target site stays within its budget", loweringBudget
    |]
```

`experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj`: after the `SugarServerChecks.fs` line add

```xml
    <Compile Include="../Ranvier.View.Arms/Checks/ArmsLoweringChecks.fs" Link="Checks/ArmsLoweringChecks.fs" />
```

and beside the Portable project reference

```xml
    <ProjectReference Include="../Ranvier.View.Arms/Ranvier.View.Arms.fsproj" />
```

`experiments/Ranvier.View.Foundation/RepresentationChecks.fsx`: after `runCases SugarServerChecks.dotnetCases` add

```fsharp
runCases ArmsLoweringChecks.cases
```

- [ ] **Step 2: Run the checks to verify they fail**

Run: `dotnet run --project experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj -c Release`
Expected: FAIL at `a pattern variable in a row reader raises AM002` (Task 1's lowering raises AM001's text) or earlier; AM004 and AM007 are not raised at all yet.

- [ ] **Step 3: Replace `Quoted.fs` and check the instance type in `Arms.fs`**

`experiments/Ranvier.View.Arms/Quoted.fs`:

```fsharp
/// <summary>The .NET lowering of a quoted arms function into a target selector and one case body per target.</summary>
module internal Ranvier.View.Arms.Quoted

#if !FABLE_COMPILER
open System
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Reflection
open Microsoft.FSharp.Linq.RuntimeHelpers
open Ranvier.View.Foundation.Template

/// <summary>The call site of an Arms.Match call, prefixed to its exception messages.</summary>
type At = { File: string; Line: int }

let private located (at: At) (message: string) =
    sprintf "%s(%d): %s" at.File at.Line message

/// <summary>An F# rendering of a type, for diagnostics; the same text as the plugin's for the same type.</summary>
let rec show (t: Type) : string =
    if t = typeof<unit> then
        "unit"
    elif t = typeof<int> then
        "int"
    elif t = typeof<string> then
        "string"
    elif t = typeof<bool> then
        "bool"
    elif t = typeof<char> then
        "char"
    elif t = typeof<float> then
        "float"
    elif t = typeof<decimal> then
        "decimal"
    elif t = typeof<int64> then
        "int64"
    elif t = typeof<float32> then
        "float32"
    elif t = typeof<byte> then
        "byte"
    elif t = typeof<obj> then
        "obj"
    elif FSharpType.IsFunction t then
        let domain, range = FSharpType.GetFunctionElements t
        atom domain + " -> " + show range
    elif FSharpType.IsTuple t then
        FSharpType.GetTupleElements t
        |> Array.map atom
        |> String.concat " * "
    elif t.IsArray then
        atom (t.GetElementType ()) + " array"
    elif t.IsGenericType then
        let definition = t.GetGenericTypeDefinition ()
        let args = t.GetGenericArguments ()

        if definition = typedefof<option<_>> then
            atom args[0] + " option"
        elif definition = typedefof<voption<_>> then
            atom args[0] + " voption"
        elif definition = typedefof<list<_>> then
            atom args[0] + " list"
        else
            t.Name.Substring (0, t.Name.IndexOf '`')
            + "<"
            + (args |> Array.map show |> String.concat ", ")
            + ">"
    else
        t.Name

and private atom (t: Type) =
    if FSharpType.IsFunction t || FSharpType.IsTuple t then
        "(" + show t + ")"
    else
        show t

/// <summary>Raises AM004 when the instance type is a value type, string, unit or an option.</summary>
let checkInstance (at: At) (t: Type) =
    if
        t.IsValueType
        || t = typeof<string>
        || t = typeof<unit>
        || (t.IsGenericType
            && t.GetGenericTypeDefinition () = typedefof<option<_>>)
    then
        raise (ArmsMatchException (located at (Diagnostics.notObject (show t))))

/// <summary>Whether the expression refers to a controlled variable outside every lambda.</summary>
let rec private refersOutsideLambdas (control: Set<Var>) (expr: Expr) =
    let refers = refersOutsideLambdas control

    match expr with
    | Var var -> control.Contains var
    | Lambda _ -> false
    | Let (_, value, body) -> refers value || refers body
    | LetRecursive (bindings, body) ->
        List.exists (snd >> refers) bindings
        || refers body
    | ExprShape.ShapeVar _
    | ExprShape.ShapeLambda _ -> false
    | ExprShape.ShapeCombination (_, args) -> List.exists refers args

let private refersTo (control: Set<Var>) (expr: Expr) =
    expr.GetFreeVars () |> Seq.exists control.Contains

let rec private extracts (control: Set<Var>) (value: Expr) =
    match value with
    | Var var -> control.Contains var
    | PropertyGet (Some inner, _, [])
    | TupleGet (inner, _)
    | Coerce (inner, _) -> extracts control inner
    | _ -> false

/// <summary>Whether a <c>let</c> belongs to the selection (Ruling 2): a compiler variable only when it extracts.</summary>
let private decides (control: Set<Var>) (var: Var) (value: Expr) =
    if var.Name.Contains "@" then
        extracts control value
    else
        refersOutsideLambdas control value

let private mapLeaves (control: Set<Var>) (leaf: Var list -> Expr -> Expr) (expr: Expr) : Expr =
    let rec walk (control: Set<Var>) (bound: Var list) (expr: Expr) =
        match expr with
        | IfThenElse (condition, whenTrue, whenFalse) when refersOutsideLambdas control condition ->
            let whenTrue = walk control bound whenTrue
            Expr.IfThenElse (condition, whenTrue, walk control bound whenFalse)
        | Let (var, value, body) when decides control var value -> Expr.Let (var, value, walk (Set.add var control) (bound @ [ var ]) body)
        | body -> leaf bound body

    walk control [] expr

/// <summary>Whether a wrapper at the root of a leaf hides a decision node from the walk (AM007).</summary>
let rec private hidden (control: Set<Var>) (expr: Expr) =
    match expr with
    | IfThenElse (condition, _, _) when refersOutsideLambdas control condition -> true
    | Let (var, value, _) when decides control var value -> true
    | Coerce (inner, _)
    | TryFinally (inner, _)
    | Let (_, _, inner)
    | Sequential (_, inner) -> hidden control inner
    | TryWith (inner, _, _, _, _) -> hidden control inner
    | _ -> false

let private raising = set [ "Raise"; "FailWith"; "InvalidOp"; "InvalidArg" ]

/// <summary>Whether a case body is a raised exception, which builds no definition.</summary>
let private isThrow (expr: Expr) =
    match expr with
    | Call (None, info, _) ->
        info.DeclaringType.FullName = "Microsoft.FSharp.Core.Operators"
        && raising.Contains info.Name
    | _ -> false

let private retype (t: Type) (expr: Expr) =
    match expr with
    | Call (None, info, args) when info.IsGenericMethod -> Expr.Call (info.GetGenericMethodDefinition().MakeGenericMethod t, args)
    | other -> other

/// <summary>The first slot variable left free in a lowered body, with the type of the lambda it sits in.</summary>
let private find (slots: Set<Var>) (expr: Expr) : (Var * Type option) option =
    let rec go (over: Type option) (expr: Expr) =
        match expr with
        | Var var when slots.Contains var -> Some (var, over)
        | Let (_, value, body) -> go over value |> Option.orElse (go over body)
        | ExprShape.ShapeVar _ -> None
        | ExprShape.ShapeLambda (var, body) when var.Type = typeof<unit> || var.Name.Contains "@" -> go over body
        | ExprShape.ShapeLambda (var, body) -> go (Some var.Type) body
        | ExprShape.ShapeCombination (_, args) -> List.tryPick (go over) args

    go None expr

/// <summary>
/// The expression with each assignment followed by <c>()</c>: LeafExpressionConverter types an assignment by its value,
/// and a lambda returning it would no longer be <c>unit</c>-valued.
/// </summary>
let rec private unitAssignments (expr: Expr) : Expr =
    match expr with
    | ExprShape.ShapeVar _ -> expr
    | ExprShape.ShapeLambda (var, body) -> Expr.Lambda (var, unitAssignments body)
    | ExprShape.ShapeCombination (shape, args) ->
        match ExprShape.RebuildShapeCombination (shape, List.map unitAssignments args) with
        | PropertySet _
        | FieldSet _
        | VarSet _ as assignment -> Expr.Sequential (assignment, Expr.Value (box (), typeof<unit>))
        | rebuilt -> rebuilt

let private unboxMethod =
    match <@ unbox<int> null @> with
    | Call (_, info, _) -> info.GetGenericMethodDefinition ()
    | _ -> failwith "unbox"

let private getMethod =
    match <@ (null: obj array).[0] @> with
    | Call (_, info, _) -> info
    | _ -> failwith "get"

let private valuesMethod =
    match <@ Cells.values Unchecked.defaultof<Cells.Cells> null 0 @> with
    | Call (_, info, _) -> info
    | _ -> failwith "Cells.values"

/// <summary>
/// Lowers <c>arms</c> into a target selector and one body per target, for the site's cells, evaluated as one LINQ
/// expression by the interpreter.
/// </summary>
let lower (cells: Cells.Cells) (at: At) (arms: Expr<'u -> TemplateView<'inst>>) : ('u -> int * obj array) * TemplateView<'inst> array =
    let matched, tree =
        match arms with
        | Lambda (matched, tree) -> matched, tree
        | _ -> raise (ArmsMatchException (located at Diagnostics.notLiteral))

    let control = Set.singleton matched
    let leaves = ResizeArray<Var list * Expr>()

    mapLeaves
        control
        (fun bound body ->
            leaves.Add (bound, body)
            body)
        tree
    |> ignore

    // The target of each leaf; a raising leaf has none.
    let targets =
        leaves
        |> Seq.fold
            (fun (acc, next) (_, body) ->
                if isThrow body then
                    None :: acc, next
                else
                    Some next :: acc, next + 1)
            ([], 0)
        |> fst
        |> List.rev
        |> Array.ofList

    let selector =
        let position = ref 0

        let pick (bound: Var list) (body: Expr) =
            let leaf = position.Value
            position.Value <- leaf + 1

            match targets[leaf] with
            | None -> retype typeof<int * obj array> body
            | Some target ->
                Expr.NewTuple
                    [
                        Expr.Value target
                        Expr.NewArray (typeof<obj>, [ for var in bound @ [ matched ] -> Expr.Coerce (Expr.Var var, typeof<obj>) ])
                    ]

        Expr.Lambda (matched, mapLeaves control pick tree)

    let rewrite (target: int) (bound: Var list) (body: Expr) =
        let slots = bound @ [ matched ]
        let slotSet = Set.ofList slots

        if hidden slotSet body then
            raise (ArmsMatchException (located at Diagnostics.unsupported))

        let prologue (argument: Var) (inner: Expr) =
            let values = Var ("values", typeof<obj array>)

            let substituted =
                inner.Substitute (fun var ->
                    match List.tryFindIndex ((=) var) slots with
                    | Some field ->
                        Some (Expr.Call (unboxMethod.MakeGenericMethod var.Type, [ Expr.Call (getMethod, [ Expr.Var values; Expr.Value field ]) ]))
                    | None -> None)

            let current =
                Expr.Call (
                    valuesMethod,
                    [
                        Expr.Value (box cells, typeof<Cells.Cells>)
                        Expr.Coerce (Expr.Var argument, typeof<obj>)
                        Expr.Value target
                    ]
                )

            Expr.Let (values, current, substituted)

        let rec visit (expr: Expr) =
            match expr with
            | Lambda (argument, inner) when
                argument.Type = typeof<'inst>
                && refersTo slotSet inner
                ->
                Expr.Lambda (argument, prologue argument inner)
            | NewDelegate (delegateType, argument :: rest, inner) when
                argument.Type = typeof<'inst>
                && refersTo slotSet inner
                ->
                Expr.NewDelegate (delegateType, argument :: rest, prologue argument inner)
            | ExprShape.ShapeVar _ -> expr
            | ExprShape.ShapeLambda (var, inner) -> Expr.Lambda (var, visit inner)
            | ExprShape.ShapeCombination (shape, args) -> ExprShape.RebuildShapeCombination (shape, List.map visit args)

        let lowered = visit body |> unitAssignments

        match find slotSet lowered with
        | Some (var, Some over) when over <> typeof<'inst> ->
            raise (BuildTimeReadException (var.Name, located at (Diagnostics.otherLambda var.Name (show over) (show typeof<'inst>))))
        | Some (var, _) -> raise (BuildTimeReadException (var.Name, located at (Diagnostics.outsideReader var.Name)))
        | None -> lowered

    let bodies =
        [
            for leaf, (bound, body) in Seq.indexed leaves do
                match targets[leaf] with
                | Some target -> yield rewrite target bound body
                | None -> ()
        ]

    let site =
        LeafExpressionConverter.QuotationToExpression (Expr.NewTuple [ selector; Expr.NewArray (typeof<TemplateView<'inst>>, bodies) ])

    // The site expression runs once, through the LINQ interpreter.
    Linq.Expressions.Expression
        .Lambda<Func<obj>>(Linq.Expressions.Expression.Convert (site, typeof<obj>))
        .Compile(preferInterpretation = true)
        .Invoke()
    |> unbox<('u -> int * obj array) * TemplateView<'inst> array>
#endif
```

In `experiments/Ranvier.View.Arms/Arms.fs`, .NET branch, insert before `let cells = Cells.create ()`:

```fsharp
        Quoted.checkInstance at typeof<'inst>
```

- [ ] **Step 4: Run the checks to verify they pass, with the timing budget**

```bash
dotnet run --project experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj -c Release -- --timing
```

Expected: `PASS` for every `ArmsLoweringChecks` case and a line `lowering|first=<≤ 10>ms mean=<≤ 4>ms` (about 1.4 ms and 1.0 ms on the development machine). If the budget fails, run it three times; a mean above 4 ms in all three is a spec conflict: stop and report the three lines. If `delegateReader` fails inside `RebuildShapeCombination`, the quotation holds the delegate's lambdas under a shape the `NewDelegate` case does not match: print `arms` for that site and add the matching case.

- [ ] **Step 5: Record the lowering cost**

Append to `docs/research/2026-10-21-arms-match.md`:

```markdown
## .NET lowering cost

`ArmsLoweringChecks` lowers the five-target site of the research (or-pattern, literal pattern, guard) once, then 200
more times, compiling the selector and the bodies as one LINQ lambda. Three runs of
`RepresentationChecks -- --timing` on the development machine:

| Run | First site (ms) | Mean of 200 (ms) |
|---|---|---|
| 1 | <first> | <mean> |
| 2 | <first> | <mean> |
| 3 | <first> | <mean> |

Budget: ≤ 10 ms first, ≤ 4 ms mean (spec, "The .NET quotation path"). The probe compiled one lambda per body and the
selector: 9.06 ms first, 8.55 ms mean.
```

with the three runs' printed numbers in the table.

- [ ] **Step 6: Re-run the Task 1 discovery comparison and the plugin tests**

Expected: `diff` empty; PluginTests all `PASS`.

- [ ] **Step 7: Format and commit**

```bash
dotnet fantomas experiments/Ranvier.View.Arms/Quoted.fs experiments/Ranvier.View.Arms/Arms.fs experiments/Ranvier.View.Arms/Checks/ArmsLoweringChecks.fs experiments/Ranvier.View.Foundation/RepresentationChecks.fsx
git add experiments/Ranvier.View.Arms experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj experiments/Ranvier.View.Foundation/RepresentationChecks.fsx docs/research/2026-10-21-arms-match.md
git diff --cached --check
git commit -F C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-task-3-commit.txt
git push origin HEAD:feat/arms-match
```

Message subject: `feat(arms): .NET lowering rules, rejections and one-lambda compile`.

---

### Task 4: Pattern-table fixtures and their explicit twins, compared on both targets

Merge `origin/master` before starting (this task edits `DomTests.fsproj` and `RepresentationChecks.*`).

**Files:**
- Create: `experiments/Ranvier.View.Arms/Checks/ArmsFixtures.fs`, `experiments/Ranvier.View.Arms/Checks/ArmsServerChecks.fs`
- Create: `examples/Fable.Ranvier.Playground/tests/arms-server.test.mjs`
- Modify: `examples/Fable.Ranvier.Playground/tests/DomTests.fsproj` (links and the `.targets` import), `examples/Fable.Ranvier.Playground/package.json` (`compile:tests`, `test:arms`), `examples/Fable.Ranvier.Playground/tests/sugar-counters-build.mjs`, `examples/Fable.Ranvier.Playground/benchmarks/frameworks/keyed-rows.mjs` (plugin build first)
- Modify: `experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj`, `RepresentationChecks.fsx`
- Test: `ArmsServerChecks.cases`, on .NET through `RepresentationChecks` and under Fable through `tests/arms-server.test.mjs`

**Interfaces:**
- Consumes: `Arms.Match` (Tasks 1-3).
- Produces: `ArmsFixtures` (module): types `Load`, `Item`, `Score`, `Panel`, `Editor`, `Point`, `Value`; `Case = { Fixture: string; State: string; Site: TemplateComponent; Twin: TemplateComponent; Render: Server.Options -> string * string }`; `all: Case list`; `incompleteRaises: unit -> bool`; each fixture component beside its twin, which later tasks mount: `loader`/`loaderTwin`, `rich`/`richTwin`, `rows`/`rowsTwin`, `rowsTuple`/`rowsTupleTwin`, `tupled`/`tupledTwin`, `optional`, `listed`, `scored`, `whole`/`wholeTwin`, `typed`, `halves`, `parsed`, `forms`/`formsTwin`, `helper`/`helperTwin`, `nested`/`nestedTwin`, `editor`/`editorTwin`, `values`/`valuesTwin`, `incomplete`/`incompleteTwin` (each twin named `<site>Twin`); `loadSteps: Load list` (the probe's eight writes). `ArmsServerChecks.cases: (string * (unit -> unit)) array`. The npm script `test:arms`.

- [ ] **Step 1: Write the fixtures**

`experiments/Ranvier.View.Arms/Checks/ArmsFixtures.fs`:

```fsharp
/// <summary>Arms.Match sites over each pattern form of the spec, each beside its explicit armOf twin, with named states.</summary>
module ArmsFixtures

#nowarn "25"

open System
open global.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms

type Load =
    | Idle
    | Loading of percent: int
    | Loaded of text: string * count: int

type Item = { Id: int; Name: string }

type Score = { Name: string; Points: int }

type Panel =
    {
        State: Signal<Load>
        Open: Signal<bool>
    }

type Editor =
    {
        State: Signal<Load>
        Saved: Signal<string>
    }

[<Struct>]
type Point = { X: int; Y: int }

type Value =
    | Ratio of ratio: float
    | Money of amount: decimal
    | Flag of on: bool
    | Letter of letter: char
    | At of point: Point
    | Maybe of maybe: int voption
    | Count of count: int

/// <summary>One state of one fixture: the site, its twin, and their server HTML under given options.</summary>
type Case =
    {
        Fixture: string
        State: string
        Site: TemplateComponent
        Twin: TemplateComponent
        Render: Server.Options -> string * string
    }

let private renderWith (options: Server.Options) (definition: Component<'props>) (make: unit -> 'props) =
    try
        use graph = new Graph ()
        let props = graph.Run make
        Server.render options definition props
    with error ->
        "raised: " + error.Message

let private cases (fixture: string) (site: Component<'props>) (twin: Component<'props>) (states: (string * (unit -> 'props)) list) =
    [
        for state, make in states ->
            {
                Fixture = fixture
                State = state
                Site = site :> TemplateComponent
                Twin = twin :> TemplateComponent
                Render = fun options -> renderWith options site make, renderWith options twin make
            }
    ]

let private read (state: Signal<Load>) =
    state.Value

let private loadStates =
    [
        "Idle", Idle
        "Loading 0", Loading 0
        "Loading 40", Loading 40
        "Loaded done 3", Loaded ("done", 3)
        "Loaded none 0", Loaded ("none", 0)
        "Loaded big 120", Loaded ("big", 120)
    ]

let private signals (states: (string * 'v) list) =
    [ for label, value in states -> label, (fun () -> Api.createSignal value) ]

/// <summary>The probe's eight writes after an initial <c>Loading 10</c>.</summary>
let loadSteps =
    [
        Loading 70
        Loaded ("a", 1)
        Loaded ("b", 2)
        Loaded ("c", 0)
        Loaded ("d", 150)
        Loaded ("e", 5)
        Loaded ("f", 3)
        Idle
    ]

// Shared twin arms.
let private idleArm = armOf (span () { "Not started" })

let private loadingArm =
    armOf (progress (Attrs.attribute ("max", "100"), Attrs.attribute ("value", fun (payload: unit -> int) -> Some (string (payload ())))) { })

let private loadedArm =
    armOf (
        pre () {
            fun (payload: unit -> string * int) -> fst (payload ())
            " x"
            fun (payload: unit -> string * int) -> string (snd (payload ()))
        }
    )

let private otherArm = armOf (span () { "Other" })
let private emptyArm = armOf (span () { "Empty" })
let private noneArm = armOf (span () { "none" })
let private textArm = armOf (pre () { fun (payload: unit -> string) -> payload () })
let private boldArm = armOf (b () { fun (payload: unit -> string) -> payload () })
let private italicArm = armOf (i () { fun (payload: unit -> string) -> payload () })

// Union cases with and without payload.
let loader =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "loader") {
            Arms.Match (
                read,
                function
                | Idle -> span () { "Not started" }
                | Loading percent -> progress (Attrs.attribute ("max", "100"), Attrs.attribute ("value", fun _ -> Some (string percent))) { }
                | Loaded (text, count) ->
                    pre () {
                        fun _ -> text
                        " x"
                        fun _ -> string count
                    }
            )
        })

let loaderTwin =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "loader") {
            matchWith read (function
                | Idle -> arm idleArm ()
                | Loading percent -> arm loadingArm percent
                | Loaded (text, count) -> arm loadedArm (text, count))
        })

// Or-pattern, literal nested pattern, guard.
let rich =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "loader") {
            Arms.Match (
                read,
                function
                | Idle
                | Loading 0 -> span () { "Not started" }
                | Loading percent -> progress (Attrs.attribute ("max", "100"), Attrs.attribute ("value", fun _ -> Some (string percent))) { }
                | Loaded (_, 0) -> span () { "Empty" }
                | Loaded (text, count) when count > 99 -> b () { fun _ -> text }
                | Loaded (text, count) ->
                    pre () {
                        fun _ -> text
                        " x"
                        fun _ -> string count
                    }
            )
        })

let richTwin =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "loader") {
            matchWith read (function
                | Idle
                | Loading 0 -> arm idleArm ()
                | Loading percent -> arm loadingArm percent
                | Loaded (_, 0) -> arm emptyArm ()
                | Loaded (text, count) when count > 99 -> arm boldArm text
                | Loaded (text, count) -> arm loadedArm (text, count))
        })

// A keyed list inside a case body.
let rows =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "loader") {
            Arms.Match (
                read,
                function
                | Loaded (_, count) -> ul () { forEach id (fun _ -> Array.init count string) (li () { fun (item: unit -> string) -> item () }) }
                | _ -> span () { "Other" }
            )
        })

let private rowsArm =
    armOf (ul () { forEach id (fun (payload: unit -> int) -> Array.init (payload ()) string) (li () { fun (item: unit -> string) -> item () }) })

let rowsTwin =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "loader") {
            matchWith read (function
                | Loaded (_, count) -> arm rowsArm count
                | _ -> arm otherArm ())
        })

// Rows reading a pattern variable through the items reader (spec, "Rows").
let rowsTuple =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            Arms.Match (
                read,
                function
                | Loaded (owner, count) ->
                    ul () {
                        forEach
                            snd
                            (fun _ -> Array.init count (fun index -> owner, index))
                            (li () { fun (row: unit -> string * int) -> fst (row ()) + string (snd (row ())) })
                    }
                | _ -> span () { "Other" }
            )
        })

let private rowsTupleArm =
    armOf (
        ul () {
            forEach
                snd
                (fun (payload: unit -> string * int) -> let owner, count = payload () in Array.init count (fun index -> owner, index))
                (li () { fun (row: unit -> string * int) -> fst (row ()) + string (snd (row ())) })
        }
    )

let rowsTupleTwin =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            matchWith read (function
                | Loaded (owner, count) -> arm rowsTupleArm (owner, count)
                | _ -> arm otherArm ())
        })

// Tuple scrutinee.
let tupled =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            Arms.Match (
                (fun (state: Signal<Load>) -> state.Value, state.Value = Idle),
                function
                | Loaded (text, _), _ -> pre () { fun _ -> text }
                | _, true -> span () { "Not started" }
                | _ -> span () { "Other" }
            )
        })

let tupledTwin =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            matchWith (fun (state: Signal<Load>) -> state.Value, state.Value = Idle) (function
                | Loaded (text, _), _ -> arm textArm text
                | _, true -> arm idleArm ()
                | _ -> arm otherArm ())
        })

// Option pattern.
let optional =
    Component.template
        (fun _ (state: Signal<string option>) -> state)
        (div () {
            Arms.Match (
                (fun (state: Signal<string option>) -> state.Value),
                function
                | Some value -> pre () { fun _ -> value }
                | None -> span () { "none" }
            )
        })

let optionalTwin =
    Component.template
        (fun _ (state: Signal<string option>) -> state)
        (div () {
            matchWith (fun (state: Signal<string option>) -> state.Value) (function
                | Some value -> arm textArm value
                | None -> arm noneArm ())
        })

// List pattern.
let private listOneArm =
    armOf (b () { fun (payload: unit -> int) -> string (payload ()) })

let private listManyArm =
    armOf (
        i () {
            fun (payload: unit -> int * int list) ->
                string (fst (payload ()))
                + "+"
                + string (snd (payload ())).Length
        }
    )

let listed =
    Component.template
        (fun _ (state: Signal<int list>) -> state)
        (div () {
            Arms.Match (
                (fun (state: Signal<int list>) -> state.Value),
                function
                | [] -> span () { "none" }
                | [ x ] -> b () { fun _ -> string x }
                | x :: rest -> i () { fun _ -> string x + "+" + string rest.Length }
            )
        })

let listedTwin =
    Component.template
        (fun _ (state: Signal<int list>) -> state)
        (div () {
            matchWith (fun (state: Signal<int list>) -> state.Value) (function
                | [] -> arm noneArm ()
                | [ x ] -> arm listOneArm x
                | x :: rest -> arm listManyArm (x, rest))
        })

// Record pattern.
let private noPointsArm = armOf (span () { "no points" })

let scored =
    Component.template
        (fun _ (state: Signal<Score>) -> state)
        (div () {
            Arms.Match (
                (fun (state: Signal<Score>) -> state.Value),
                function
                | { Points = 0 } -> span () { "no points" }
                | { Name = name; Points = points } -> pre () { fun _ -> name + ":" + string points }
            )
        })

let scoredTwin =
    Component.template
        (fun _ (state: Signal<Score>) -> state)
        (div () {
            matchWith (fun (state: Signal<Score>) -> state.Value) (function
                | { Points = 0 } -> arm noPointsArm ()
                | { Name = name; Points = points } -> arm textArm (name + ":" + string points))
        })

// as pattern.
let whole =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            Arms.Match (
                read,
                function
                | Loaded _ as loaded ->
                    pre () {
                        fun _ ->
                            match loaded with
                            | Loaded (text, count) -> text + string count
                            | _ -> ""
                    }
                | _ -> span () { "Other" }
            )
        })

let wholeTwin =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            matchWith read (function
                | Loaded (text, count) -> arm textArm (text + string count)
                | _ -> arm otherArm ())
        })

// Type test over an obj scrutinee.
let typed =
    Component.template
        (fun _ (state: Signal<obj>) -> state)
        (div () {
            Arms.Match (
                (fun (state: Signal<obj>) -> state.Value),
                function
                | :? Item as item -> b () { fun _ -> item.Name }
                | :? string as name -> i () { fun _ -> name }
                | _ -> span () { "Other" }
            )
        })

let typedTwin =
    Component.template
        (fun _ (state: Signal<obj>) -> state)
        (div () {
            matchWith (fun (state: Signal<obj>) -> state.Value) (function
                | :? Item as item -> arm boldArm item.Name
                | :? string as name -> arm italicArm name
                | _ -> arm otherArm ())
        })

// Total and partial active patterns.
let (|Half|Odd|) (n: int) =
    if n % 2 = 0 then Half (n / 2) else Odd

let (|Number|_|) (text: string) =
    match Int32.TryParse text with
    | true, n -> Some n
    | _ -> None

let private halfArm =
    armOf (b () { fun (payload: unit -> int) -> string (payload ()) })

let private oddArm = armOf (span () { "odd" })

let halves =
    Component.template
        (fun _ (state: Signal<int>) -> state)
        (div () {
            Arms.Match (
                (fun (state: Signal<int>) -> state.Value),
                function
                | Half half -> b () { fun _ -> string half }
                | Odd -> span () { "odd" }
            )
        })

let halvesTwin =
    Component.template
        (fun _ (state: Signal<int>) -> state)
        (div () {
            matchWith (fun (state: Signal<int>) -> state.Value) (function
                | Half half -> arm halfArm half
                | Odd -> arm oddArm ())
        })

let parsed =
    Component.template
        (fun _ (state: Signal<string>) -> state)
        (div () {
            Arms.Match (
                (fun (state: Signal<string>) -> state.Value),
                function
                | Number n -> b () { fun _ -> string (n * 2) }
                | other -> i () { fun _ -> other }
            )
        })

let parsedTwin =
    Component.template
        (fun _ (state: Signal<string>) -> state)
        (div () {
            matchWith (fun (state: Signal<string>) -> state.Value) (function
                | Number n -> arm halfArm (n * 2)
                | other -> arm italicArm other)
        })

// if, let and a let of a view at the top of case bodies.
let private hugeArm = armOf (b () { "huge" })
let private bigArm = armOf (i () { "big" })

let forms =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            Arms.Match (
                read,
                function
                | Loaded (_, n) when n > 100 -> if n > 1000 then b () { "huge" } else i () { "big" }
                | Loaded (t, 0) ->
                    let label = t + "!"
                    pre () { fun _ -> label }
                | Loaded (t, _) ->
                    let view = pre () { fun _ -> t }
                    view
                | _ -> span () { "Other" }
            )
        })

let formsTwin =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            matchWith read (function
                | Loaded (_, n) when n > 1000 -> arm hugeArm ()
                | Loaded (_, n) when n > 100 -> arm bigArm ()
                | Loaded (t, 0) -> arm textArm (t + "!")
                | Loaded (t, _) -> arm textArm t
                | _ -> arm otherArm ())
        })

// A helper view of the enclosing instance type that takes a reader (Review Focus 3).
let private labelled (label: 'inst -> string) : TemplateView<'inst> =
    b (Attrs.className "label") { label }

let helper =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            Arms.Match (
                read,
                function
                | Loaded (text, _) -> labelled (fun _ -> text)
                | _ -> span () { "Other" }
            )
        })

let private labelledArm =
    armOf (labelled (fun (payload: unit -> string) -> payload ()))

let helperTwin =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            matchWith read (function
                | Loaded (text, _) -> arm labelledArm text
                | _ -> arm otherArm ())
        })

// A site inside a case body reading the outer pattern variable.
let private closedArm = armOf (span () { "closed" })

let nested =
    Component.template
        (fun _ (panel: Panel) -> panel)
        (div () {
            Arms.Match (
                (fun (panel: Panel) -> panel.State.Value),
                function
                | Loaded (t, _) ->
                    section () {
                        Arms.Match (
                            (fun (panel: Panel) -> panel.Open.Value),
                            function
                            | true -> pre () { fun _ -> t }
                            | false -> span () { "closed" }
                        )
                    }
                | _ -> span () { "Other" }
            )
        })

let private openArm =
    armOf (
        section () {
            matchWith (fun (payload: unit -> string * Panel) -> (snd (payload ())).Open.Value, fst (payload ())) (function
                | true, t -> arm textArm t
                | false, _ -> arm closedArm ())
        }
    )

let nestedTwin =
    Component.template
        (fun _ (panel: Panel) -> panel)
        (div () {
            matchWith (fun (panel: Panel) -> panel.State.Value, panel) (function
                | Loaded (t, _), panel -> arm openArm (t, panel)
                | _ -> arm otherArm ())
        })

// An event handler reading a pattern variable.
let editor =
    Component.template
        (fun _ (editor: Editor) -> editor)
        (div () {
            Arms.Match (
                (fun (editor: Editor) -> editor.State.Value),
                function
                | Loaded (text, _) -> button (Events.onClick (fun (editor: Editor) _ -> editor.Saved.Value <- text)) { fun _ -> text }
                | _ -> span () { "Other" }
            )
        })

let private saveArm =
    armOf (
        button (Events.onClick (fun (payload: unit -> string * Signal<string>) _ -> (snd (payload ())).Value <- fst (payload ()))) {
            fun (payload: unit -> string * Signal<string>) -> fst (payload ())
        }
    )

let editorTwin =
    Component.template
        (fun _ (editor: Editor) -> editor)
        (div () {
            matchWith (fun (editor: Editor) -> editor.State.Value, editor.Saved) (function
                | Loaded (text, _), saved -> arm saveArm (text, saved)
                | _ -> arm otherArm ())
        })

// Value-type payload fields.
let values =
    Component.template
        (fun _ (state: Signal<Value>) -> state)
        (div () {
            Arms.Match (
                (fun (state: Signal<Value>) -> state.Value),
                function
                | Ratio ratio -> b () { fun _ -> string ratio }
                | Money amount -> b () { fun _ -> string amount }
                | Flag on -> b () { fun _ -> if on then "on" else "off" }
                | Letter letter -> b () { fun _ -> string letter }
                | At point -> b () { fun _ -> string point.X + "," + string point.Y }
                | Maybe (ValueSome n) -> b () { fun _ -> string n }
                | Maybe ValueNone -> span () { "none" }
                | Count count -> b () { fun _ -> string count }
            )
        })

let private ratioArm =
    armOf (b () { fun (payload: unit -> float) -> string (payload ()) })

let private moneyArm =
    armOf (b () { fun (payload: unit -> decimal) -> string (payload ()) })

let private flagArm =
    armOf (b () { fun (payload: unit -> bool) -> if payload () then "on" else "off" })

let private letterArm =
    armOf (b () { fun (payload: unit -> char) -> string (payload ()) })

let private pointArm =
    armOf (
        b () {
            fun (payload: unit -> Point) ->
                string (payload ()).X
                + ","
                + string (payload ()).Y
        }
    )

let private countArm =
    armOf (b () { fun (payload: unit -> int) -> string (payload ()) })

let valuesTwin =
    Component.template
        (fun _ (state: Signal<Value>) -> state)
        (div () {
            matchWith (fun (state: Signal<Value>) -> state.Value) (function
                | Ratio ratio -> arm ratioArm ratio
                | Money amount -> arm moneyArm amount
                | Flag on -> arm flagArm on
                | Letter letter -> arm letterArm letter
                | At point -> arm pointArm point
                | Maybe (ValueSome n) -> arm countArm n
                | Maybe ValueNone -> arm noneArm ()
                | Count count -> arm countArm count)
        })

// An incomplete match: Loaded raises at the region read.
let incomplete =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            Arms.Match (
                read,
                function
                | Idle -> span () { "Not started" }
                | Loading percent -> b () { fun _ -> string percent }
            )
        })

let private percentArm =
    armOf (b () { fun (payload: unit -> int) -> string (payload ()) })

let incompleteTwin =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            matchWith read (function
                | Idle -> arm idleArm ()
                | Loading percent -> arm percentArm percent
                | Loaded _ -> arm otherArm ())
        })

let private panels =
    [
        "Loaded open",
        (fun () ->
            {
                State = Api.createSignal (Loaded ("t", 1))
                Open = Api.createSignal true
            }
            : Panel)
        "Loaded closed",
        (fun () ->
            {
                State = Api.createSignal (Loaded ("t", 1))
                Open = Api.createSignal false
            }
            : Panel)
        "Idle",
        (fun () ->
            {
                State = Api.createSignal Idle
                Open = Api.createSignal true
            }
            : Panel)
    ]

let private editors =
    [
        "Loaded",
        (fun () ->
            {
                State = Api.createSignal (Loaded ("draft", 1))
                Saved = Api.createSignal ""
            }
            : Editor)
        "Idle",
        (fun () ->
            {
                State = Api.createSignal Idle
                Saved = Api.createSignal ""
            }
            : Editor)
    ]

/// <summary>Every fixture state; the incomplete fixture's unmatched state is checked on its own.</summary>
let all: Case list =
    List.concat
        [
            cases "loader" loader loaderTwin (signals loadStates)
            cases "rich" rich richTwin (signals loadStates)
            cases "rows" rows rowsTwin (signals loadStates)
            cases "rowsTuple" rowsTuple rowsTupleTwin (signals loadStates)
            cases "tupled" tupled tupledTwin (signals loadStates)
            cases "optional" optional optionalTwin (signals [ "Some", Some "ink"; "None", None ])
            cases "listed" listed listedTwin (signals [ "empty", []; "one", [ 4 ]; "three", [ 1; 2; 3 ] ])
            cases "scored" scored scoredTwin (signals [ "zero", { Name = "ada"; Points = 0 }; "five", { Name = "ada"; Points = 5 } ])
            cases "whole" whole wholeTwin (signals loadStates)
            cases "typed" typed typedTwin (signals [ "Item", box { Id = 1; Name = "pen" }; "string", box "ink"; "other", box 3 ])
            cases "halves" halves halvesTwin (signals [ "four", 4; "five", 5 ])
            cases "parsed" parsed parsedTwin (signals [ "number", "21"; "word", "ink" ])
            cases
                "forms"
                forms
                formsTwin
                (signals
                    [
                        "huge", Loaded ("x", 2000)
                        "big", Loaded ("x", 200)
                        "zero", Loaded ("x", 0)
                        "three", Loaded ("y", 3)
                        "Idle", Idle
                    ])
            cases "helper" helper helperTwin (signals loadStates)
            cases "nested" nested nestedTwin panels
            cases "editor" editor editorTwin editors
            cases
                "values"
                values
                valuesTwin
                (signals
                    [
                        "ratio", Ratio 1.5
                        "money", Money 2.25m
                        "flag", Flag true
                        "letter", Letter 'q'
                        "point", At { X = 3; Y = 4 }
                        "some", Maybe (ValueSome 7)
                        "none", Maybe ValueNone
                        "count", Count 9
                    ])
            cases "incomplete" incomplete incompleteTwin (signals [ "Idle", Idle; "Loading 40", Loading 40 ])
        ]

/// <summary>Whether the incomplete site raises for its unmatched case.</summary>
let incompleteRaises () =
    let html =
        renderWith Server.Options.defaults incomplete (fun () -> Api.createSignal (Loaded ("x", 1)))

    html.StartsWith "raised: "
```

- [ ] **Step 2: Write the server checks**

`experiments/Ranvier.View.Arms/Checks/ArmsServerChecks.fs`:

```fsharp
/// <summary>Arms.Match fixtures compared with their explicit twins by layout, structural hash and server HTML.</summary>
module ArmsServerChecks

open global.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open ArmsFixtures

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

let private omit =
    { Server.Options.defaults with
        Policy = Omit
    }

let private sameLayouts () =
    for case in all do
        equal
            (case.Fixture, SugarFixtures.layoutSignature case.Twin.Root, structuralHash case.Twin)
            (case.Fixture, SugarFixtures.layoutSignature case.Site.Root, structuralHash case.Site)

let private sameHtml (options: Server.Options) () =
    for case in all do
        let site, twin = case.Render options
        equal (case.Fixture, case.State, twin) (case.Fixture, case.State, site)

let cases: (string * (unit -> unit)) array =
    [|
        "Arms.Match fixtures share their twins' layouts and hashes", sameLayouts
        "Arms.Match fixtures render their twins' server HTML under Reject", sameHtml Server.Options.defaults
        "Arms.Match fixtures render their twins' server HTML under Omit", sameHtml omit
        "an incomplete Arms.Match raises for its unmatched case", (fun () -> equal true (incompleteRaises ()))
    |]
```

`examples/Fable.Ranvier.Playground/tests/arms-server.test.mjs`:

```js
import test from "node:test";
import { cases } from "../generated-tests/experiments/Ranvier.View.Arms/Checks/ArmsServerChecks.js";

for (const [name, check] of cases) test(name, () => check());
```

Register on .NET. `RepresentationChecks.fsproj`, before the `ArmsLoweringChecks.fs` line:

```xml
    <Compile Include="../Ranvier.View.Arms/Checks/ArmsFixtures.fs" Link="Checks/ArmsFixtures.fs" />
    <Compile Include="../Ranvier.View.Arms/Checks/ArmsServerChecks.fs" Link="Checks/ArmsServerChecks.fs" />
```

`RepresentationChecks.fsx`, after `runCases ArmsLoweringChecks.cases`:

```fsharp
runCases ArmsServerChecks.cases
```

Register under Fable. `DomTests.fsproj`, after the `Server.fs` link:

```xml
    <Compile Include="../../../experiments/Ranvier.View.Arms/Diagnostics.fs" Link="Arms/Diagnostics.fs" />
    <Compile Include="../../../experiments/Ranvier.View.Arms/Errors.fs" Link="Arms/Errors.fs" />
    <Compile Include="../../../experiments/Ranvier.View.Arms/Cells.fs" Link="Arms/Cells.fs" />
    <Compile Include="../../../experiments/Ranvier.View.Arms/Site.fs" Link="Arms/Site.fs" />
    <Compile Include="../../../experiments/Ranvier.View.Arms/Quoted.fs" Link="Arms/Quoted.fs" />
    <Compile Include="../../../experiments/Ranvier.View.Arms/Arms.fs" Link="Arms/Arms.fs" />
```

after the `SugarServerChecks.fs` link:

```xml
    <Compile Include="../../../experiments/Ranvier.View.Arms/Checks/ArmsFixtures.fs" Link="Checks/ArmsFixtures.fs" />
    <Compile Include="../../../experiments/Ranvier.View.Arms/Checks/ArmsServerChecks.fs" Link="Checks/ArmsServerChecks.fs" />
```

and before `</Project>`:

```xml
  <Import Project="../../../experiments/Ranvier.View.Arms/Ranvier.View.Arms.targets" />
```

`package.json` scripts: replace `compile:tests` and add `test:arms`:

```json
    "compile:tests": "dotnet build ../../experiments/Ranvier.View.Arms.FablePlugin/Ranvier.View.Arms.FablePlugin.fsproj -c Release -v q && dotnet fickl tests/DomTests.fsproj --outDir generated-tests --configuration Release",
    "test:arms": "npm run compile:tests -- --define RANVIER_VIEW_COUNTERS && node --test tests/arms-server.test.mjs",
```

`tests/sugar-counters-build.mjs`: before the `execFileSync("dotnet", ["fickl", ...` call add

```js
execFileSync("dotnet", ["build", "../../experiments/Ranvier.View.Arms.FablePlugin/Ranvier.View.Arms.FablePlugin.fsproj", "-c", "Release", "-v", "q"], {
  stdio: "inherit", windowsHide: true, shell: process.platform === "win32"
});
```

`benchmarks/frameworks/keyed-rows.mjs`: make the first entry of the `--build` list

```js
    [["dotnet", "build", "../../experiments/Ranvier.View.Arms.FablePlugin/Ranvier.View.Arms.FablePlugin.fsproj", "-c", "Release", "-v", "q"], {}],
```

- [ ] **Step 3: Run the .NET checks**

Run: `dotnet run --project experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj -c Release`
Expected: `PASS` for the four `ArmsServerChecks` cases. A failing fixture is a lowering defect in `Quoted.fs` (Task 3) or a twin that differs from its site; fix the lowering, not the twin, unless the twin's markup differs from the site's source text.

- [ ] **Step 4: Run the Fable checks**

From `examples/Fable.Ranvier.Playground`: `npm ci && npm run test:arms`
Expected: the four tests pass. A compile error naming an AM code is a plugin defect (Task 2) on a valid fixture: fix `Lower.fs`, add the fixture's shape to `Tests/Fixtures/Sites.fs`, regenerate the expectation with `--update` after reviewing it, and rerun with `--noCache`.

- [ ] **Step 5: Run the phase gates**

All of the Global Constraints' task and phase gates, including `npm run test:dom`, `test:sugar-debug`, `test:sugar-counters` (the Debug and counters builds now compile the arms sources), `test:browser` under the browser lock, and the sugar-size comparison:

```bash
cd examples/Fable.Ranvier.Playground
node benchmarks/frameworks/sugar-size.mjs
node -e "const a=require('C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-sugar-size-before.json').explicit,b=require('../../docs/research/framework-performance/investigation/view-sugar-v2/bundle.json').explicit;if(JSON.stringify(a)!==JSON.stringify(b)){console.error(a,b);process.exit(1)}"
git checkout -- ../../docs/research/framework-performance/investigation/view-sugar-v2/bundle.json
```

Expected: every gate exits 0; the explicit entry's bytes equal the Task 1 record.

- [ ] **Step 6: Format and commit**

```bash
dotnet fantomas experiments/Ranvier.View.Arms/Checks/ArmsFixtures.fs experiments/Ranvier.View.Arms/Checks/ArmsServerChecks.fs experiments/Ranvier.View.Foundation/RepresentationChecks.fsx
git add experiments/Ranvier.View.Arms/Checks experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj experiments/Ranvier.View.Foundation/RepresentationChecks.fsx examples/Fable.Ranvier.Playground/tests/DomTests.fsproj examples/Fable.Ranvier.Playground/tests/arms-server.test.mjs examples/Fable.Ranvier.Playground/tests/sugar-counters-build.mjs examples/Fable.Ranvier.Playground/package.json examples/Fable.Ranvier.Playground/benchmarks/frameworks/keyed-rows.mjs
git diff --cached --check
git commit -F C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-task-4-commit.txt
git push origin HEAD:feat/arms-match
```

Message subject: `test(arms): pattern-table fixtures and explicit twins on both targets`. End of Phase 2.

---

## Phase 3: runtime

Start of phase: merge `origin/master`, then the task gates.

### Task 5: Cells with live lists per target, graph tags, withdrawal and AM006

**Files:**
- Modify: `experiments/Ranvier.View.Arms/Cells.fs` (replaced whole)
- Modify: `experiments/Ranvier.View.Arms/Checks/ArmsLoweringChecks.fs` (runtime cases), `experiments/Ranvier.View.Arms/Checks/ArmsFixtures.fs` (`plainScore`, `Shown`, `shownLoader`)
- Create: `examples/Fable.Ranvier.Playground/tests/ArmsChecks.fs`, `examples/Fable.Ranvier.Playground/tests/arms.test.mjs`
- Modify: `examples/Fable.Ranvier.Playground/tests/DomTests.fsproj`, `examples/Fable.Ranvier.Playground/package.json` (`test:arms`)
- Test: `ArmsChecks.cases` (jsdom), `ArmsLoweringChecks.runtimeCases` (.NET)

**Interfaces:**
- Consumes: `Cells.create`, `Cells.enter`, `Cells.values` signatures (unchanged); `ArmsFixtures.loader`.
- Produces: the Decision 7 behaviour behind the same signatures; `ArmsFixtures.plainScore: Component<Score>`, `ArmsFixtures.Shown = { Visible: Signal<bool>; State: Signal<Load> }`, `ArmsFixtures.shownLoader: Component<Shown>`; `ArmsChecks.cases` (Task 6 appends to it); `ArmsLoweringChecks.runtimeCases`.

- [ ] **Step 1: Add the fixtures the runtime checks mount**

Append to `experiments/Ranvier.View.Arms/Checks/ArmsFixtures.fs`, before `/// <summary>Every fixture state`:

```fsharp
/// <summary>A site whose instance is a plain record, shared by concurrent server renders.</summary>
let plainScore =
    Component.template
        (fun _ (score: Score) -> score)
        (div () {
            Arms.Match (
                id,
                function
                | { Points = 0 } -> span () { "no points" }
                | { Name = name; Points = points } -> pre () { fun _ -> name + ":" + string points }
            )
        })

type Shown =
    {
        Visible: Signal<bool>
        State: Signal<Load>
    }

/// <summary>A site under a <c>show</c>: hiding disposes its arm, showing mounts a new one for the same instance.</summary>
let shownLoader =
    Component.template
        (fun _ (shown: Shown) -> shown)
        (div () {
            show
                (fun (shown: Shown) -> shown.Visible.Value)
                (div () {
                    Arms.Match (
                        (fun (shown: Shown) -> shown.State.Value),
                        function
                        | Loaded (text, count) ->
                            pre () {
                                fun _ -> text
                                " x"
                                fun _ -> string count
                            }
                        | _ -> span () { "Other" }
                    )
                })
        })
```

- [ ] **Step 2: Write the failing checks**

`examples/Fable.Ranvier.Playground/tests/ArmsChecks.fs`:

```fsharp
/// <summary>Arms.Match sites mounted in jsdom: cells per arm, events, value payloads, rows, hydration and exits.</summary>
module ArmsChecks

open System
open global.Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open CheckSupport
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms
open ArmsFixtures

/// <summary>The text of the first element matching the selector, or null.</summary>
[<Emit("($0.querySelector($1)?.textContent ?? null)")>]
let private textOf (target: HTMLElement) (selector: string) : string = jsNative

let private write (graph: Graph) (mounted: Mounting.Mounted list) (change: unit -> unit) =
    graph.Run change

    for mount in mounted do
        mount.Flush ()

/// <summary>Two mounts of one site with one instance: disposing either leaves the other reading live values (Testing 4).</summary>
let private twiceShown queued () =
    use graph = new Graph ()
    let state = graph.Run (fun () -> Api.createSignal (Loaded ("a", 1)))
    let first, second = host (), host ()
    let a = mountTemplate queued graph strict first (Component.render loader state)
    let b = mountTemplate queued graph strict second (Component.render loader state)
    a.Flush ()
    b.Flush ()
    equal ("a x1", "a x1") (textOf first "pre", textOf second "pre")
    write graph [ a; b ] (fun () -> state.Value <- Loaded ("b", 2))
    equal ("b x2", "b x2") (textOf first "pre", textOf second "pre")
    b.Dispose ()
    write graph [ a ] (fun () -> state.Value <- Loaded ("c", 3))
    equal "c x3" (textOf first "pre")
    let third = host ()
    let c = mountTemplate queued graph strict third (Component.render loader state)
    c.Flush ()
    write graph [ a; c ] (fun () -> state.Value <- Loading 5)
    write graph [ a; c ] (fun () -> state.Value <- Loaded ("d", 4))
    equal ("d x4", "d x4") (textOf first "pre", textOf third "pre")
    a.Dispose ()
    write graph [ c ] (fun () -> state.Value <- Loaded ("e", 5))
    equal "e x5" (textOf third "pre")
    c.Dispose ()

/// <summary>An arm hidden and shown again for the same instance follows later writes (Review Focus 2).</summary>
let private remountAfterHide queued () =
    use graph = new Graph ()

    let shown =
        graph.Run (fun () ->
            {
                Visible = Api.createSignal true
                State = Api.createSignal (Loaded ("a", 1))
            })

    let target = host ()

    let mounted =
        mountTemplate queued graph strict target (Component.render shownLoader shown)

    mounted.Flush ()
    write graph [ mounted ] (fun () -> shown.State.Value <- Loaded ("b", 2))
    write graph [ mounted ] (fun () -> shown.Visible.Value <- false)
    equal null (textOf target "pre")
    write graph [ mounted ] (fun () -> shown.Visible.Value <- true)
    equal "b x2" (textOf target "pre")
    write graph [ mounted ] (fun () -> shown.State.Value <- Loaded ("c", 3))
    equal "c x3" (textOf target "pre")
    mounted.Dispose ()

let cases: (string * (unit -> unit)) array =
    strictCases (
        Array.concat
            [
                templated "one site shown twice for one instance reads live values through either disposal" twiceShown
                templated "an arm hidden and shown again follows later writes" remountAfterHide
            ]
    )
```

`examples/Fable.Ranvier.Playground/tests/arms.test.mjs`:

```js
import test, { after } from "node:test";
import { JSDOM } from "jsdom";
import { cases } from "../generated-tests/ArmsChecks.js";

const dom = new JSDOM("<!doctype html><body></body>");
const names = ["window", "document", "FocusEvent", "MutationObserver", "Node", "Event", "EventTarget"];
for (const name of names) globalThis[name] = name === "window" ? dom.window : dom.window[name];
after(() => { dom.window.close(); for (const name of names) delete globalThis[name]; });
for (const [name, check] of cases) test(name, () => check());
```

`DomTests.fsproj`: after the `SugarDebugChecks.fs` line add

```xml
    <Compile Include="ArmsChecks.fs" />
```

`package.json`: `test:arms` becomes

```json
    "test:arms": "npm run compile:tests -- --define RANVIER_VIEW_COUNTERS && node --test tests/arms-server.test.mjs tests/arms.test.mjs",
```

Append to `experiments/Ranvier.View.Arms/Checks/ArmsLoweringChecks.fs`, after `cases`:

```fsharp
/// <summary>A reader with no arm of its site for its instance raises AM006.</summary>
let private noArm () =
    let message =
        try
            Ranvier.View.Arms.Cells.values (Ranvier.View.Arms.Cells.create ()) (obj ()) 0 |> ignore
            ""
        with :? ArmsMatchException as error ->
            error.Message

    equal
        "Arms.Match: no arm of this site is mounted for the instance this reader received. A lambda over the instance type inside a case body runs with the arm's instance."
        message

/// <summary>Sixteen threads render one site for one shared instance, each under its own graph, 50 times each (Review Focus 1).</summary>
let private concurrentRenders () =
    let score: ArmsFixtures.Score = { Name = "ada"; Points = 5 }
    let expected = Server.render omit ArmsFixtures.plainScore score
    let failures = Collections.Concurrent.ConcurrentBag<string>()
    use barrier = new Threading.Barrier 16

    let threads =
        [|
            for _ in 1..16 ->
                Threading.Thread (fun () ->
                    barrier.SignalAndWait ()

                    for _ in 1..50 do
                        try
                            let html = Server.render omit ArmsFixtures.plainScore score

                            if html <> expected then
                                failures.Add html
                        with error ->
                            failures.Add error.Message)
        |]

    for thread in threads do
        thread.Start ()

    for thread in threads do
        thread.Join ()

    equal ("<!--[r--><div><!--[a:", []) (expected.Substring (0, 21), List.ofSeq failures)

let runtimeCases: (string * (unit -> unit)) array =
    [|
        "a reader with no arm for its instance raises AM006", noArm
        "concurrent server renders of one instance read their own arms", concurrentRenders
    |]
```

`RepresentationChecks.fsx`, after `runCases ArmsServerChecks.cases`:

```fsharp
runCases ArmsLoweringChecks.runtimeCases
```

- [ ] **Step 3: Run the checks to verify they fail**

```bash
dotnet run --project experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj -c Release
cd examples/Fable.Ranvier.Playground && npm run test:arms
```

Expected: the jsdom test `one site shown twice for one instance reads live values through either disposal` fails at `equal "c x3"` with `got "b x2"` (the single slot holds the disposed mount's reader, as in `results/twice.txt`). The .NET runtime cases may pass already: the AM006 text exists since Task 1, and the concurrency check guards the graph tags rather than reproducing a failure.

- [ ] **Step 4: Replace `Cells.fs` with live lists**

`experiments/Ranvier.View.Arms/Cells.fs`:

```fsharp
/// <summary>A site's lookup from an instance to the payload readers of its mounted arms, one list per target.</summary>
module internal Ranvier.View.Arms.Cells

open global.Ranvier
open Ranvier.View.Foundation.Representation

/// <summary>A mounted arm's payload reader, the graph its setup ran under, and the values of its latest read.</summary>
[<AllowNullLiteral>]
type Entry(graph: obj, values: unit -> obj array) =
    member _.Graph = graph
    member _.Values = values
    member val Last: obj array = null with get, set

/// <summary>The live entries of one instance per target, and the values the last withdrawn entry of each target held.</summary>
[<AllowNullLiteral>]
type Slots() =
    let live = ResizeArray<ResizeArray<Entry>>()
    let withdrawn = ResizeArray<obj array>()

    member private _.Ensure(target: int) =
        while live.Count <= target do
            live.Add (ResizeArray ())
            withdrawn.Add null

    member this.Add(target: int, entry: Entry) =
        this.Ensure target
        live[target].Add entry

    member this.Withdraw(target: int, entry: Entry) =
        this.Ensure target
        live[target].Remove entry |> ignore
        withdrawn[target] <- entry.Last

    member this.Live(target: int) : ResizeArray<Entry> =
        this.Ensure target
        live[target]

    member this.Withdrawn(target: int) : obj array =
        this.Ensure target
        withdrawn[target]

#if FABLE_COMPILER
open Fable.Core

[<Emit("new WeakMap()")>]
let private newTable () : obj = jsNative

[<Emit("$0.get($1)")>]
let private tryFind (table: obj) (instance: obj) : Slots = jsNative

[<Emit("$0.set($1, $2)")>]
let private store (table: obj) (instance: obj) (slots: Slots) : unit = jsNative

let private slotsOf (table: obj) (instance: obj) : Slots =
    match tryFind table instance with
    | null ->
        let slots = Slots ()
        store table instance slots
        slots
    | slots -> slots
#else
open System.Runtime.CompilerServices

let private newTable () : obj =
    ConditionalWeakTable<obj, Slots>() :> obj

let private tryFind (table: obj) (instance: obj) : Slots =
    match (table :?> ConditionalWeakTable<obj, Slots>).TryGetValue instance with
    | true, slots -> slots
    | _ -> null

let private slotsOf (table: obj) (instance: obj) : Slots =
    (table :?> ConditionalWeakTable<obj, Slots>).GetValue(instance, fun _ -> Slots ())
#endif

/// <summary>Runs <c>action</c> holding the instance's lock on .NET; Fable runs it directly.</summary>
let inline private guarded (slots: Slots) ([<InlineIfLambda>] action: unit -> 'a) : 'a =
#if FABLE_COMPILER
    action ()
#else
    lock slots action
#endif

let private ambient () : obj =
    match Graph.TryCurrent with
    | ValueSome graph -> box graph
    | ValueNone -> null

[<Sealed>]
type Cells internal (table: obj) =
    member internal _.Table = table

let create () =
    Cells (newTable ())

/// <summary>Registers the arm's payload reader for the instance and target, and withdraws it on the arm's cleanup.</summary>
let enter (cells: Cells) (setup: Setup) (instance: obj) (target: int) (values: unit -> obj array) : unit =
    let slots = slotsOf cells.Table instance
    let entry = Entry (ambient (), values)
    guarded slots (fun () -> slots.Add (target, entry))
    setup.OnCleanup (fun () -> guarded slots (fun () -> slots.Withdraw (target, entry)))

/// <summary>
/// The bound values of the reader's own arm: the pattern variables, then the matched value. Reads every live entry of
/// the ambient graph (every entry when no graph is ambient) and returns the first; with no live entry, the values the
/// last withdrawn entry held.
/// </summary>
let values (cells: Cells) (instance: obj) (target: int) : obj array =
    match tryFind cells.Table instance with
    | null -> Errors.noArm ()
    | slots ->
        let graph = ambient ()

        guarded slots (fun () ->
            let mutable first: obj array = null

            for entry in slots.Live target do
                if
                    isNull graph
                    || obj.ReferenceEquals (entry.Graph, graph)
                then
                    let read = entry.Values ()
                    entry.Last <- read

                    if isNull first then
                        first <- read

            if not (isNull first) then
                first
            else
                match slots.Withdrawn target with
                | null -> Errors.noArm ()
                | held -> held)
```

- [ ] **Step 5: Run the checks to verify they pass**

```bash
dotnet run --project experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj -c Release
cd examples/Fable.Ranvier.Playground && npm run test:arms
```

Expected: all `PASS`, the jsdom suite green with no late report. If the emitted `Cells.js` reads a stale `first` (the Fable mutable hazard), check the emitted loop; `first` is a `let mutable` local and is assigned in place.

- [ ] **Step 6: Run the task gates, format and commit**

```bash
dotnet fantomas experiments/Ranvier.View.Arms/Cells.fs experiments/Ranvier.View.Arms/Checks/ArmsFixtures.fs experiments/Ranvier.View.Arms/Checks/ArmsLoweringChecks.fs examples/Fable.Ranvier.Playground/tests/ArmsChecks.fs experiments/Ranvier.View.Foundation/RepresentationChecks.fsx
git add experiments/Ranvier.View.Arms experiments/Ranvier.View.Foundation/RepresentationChecks.fsx examples/Fable.Ranvier.Playground/tests/ArmsChecks.fs examples/Fable.Ranvier.Playground/tests/arms.test.mjs examples/Fable.Ranvier.Playground/tests/DomTests.fsproj examples/Fable.Ranvier.Playground/package.json
git diff --cached --check
git commit -F C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-task-5-commit.txt
git push origin HEAD:feat/arms-match
```

Message subject: `feat(arms): cells hold live arms per instance and target`.

---

### Task 6: Mounted behaviour against the twins: traces, events, value payloads, rows

Merge `origin/master` before starting (this task edits `DomTests.fsproj`).

**Files:**
- Create: `examples/Fable.Ranvier.Playground/tests/TwinTrace.fs`, `examples/Fable.Ranvier.Playground/tests/arms-counters.test.mjs`
- Modify: `examples/Fable.Ranvier.Playground/tests/SugarChecks.fs` (lines 14-147 move to `TwinTrace.fs`), `tests/ArmsChecks.fs`, `tests/DomTests.fsproj`, `package.json` (`test:arms`)
- Test: `ArmsChecks.cases` (jsdom, and the counters build)

**Interfaces:**
- Consumes: `ArmsFixtures` (Task 4), `ArmsChecks` (Task 5).
- Produces: `module TwinTrace` with `Step = Write of (unit -> unit) | Each of (HTMLElement -> unit)`, `Trace`, `click: string -> HTMLElement -> unit`, `run`, `twins`, `same: Trace * Trace -> unit`, `pair: bool -> (Graph -> 'state) -> ('state -> Program.View) -> ('state -> Program.View) -> ('state -> Step list) -> unit` (all as they are in `SugarChecks.fs` today, made public).

- [ ] **Step 1: Move the twin-trace harness**

Create `examples/Fable.Ranvier.Playground/tests/TwinTrace.fs` holding, unchanged except that every `private` on these declarations is removed, lines 14-147 of `SugarChecks.fs` (from `type private Step =` through the end of `let private pair`), under this header:

```fsharp
/// <summary>Two views mounted on graphs of their own, driven by the same steps, and compared by markup, reports, counters, listeners and native writes.</summary>
module TwinTrace

open System
open global.Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open CheckSupport
```

In `SugarChecks.fs`, delete lines 14-147 and add `open TwinTrace` after `open SugarFixtures`. In `DomTests.fsproj`, add `<Compile Include="TwinTrace.fs" />` before `<Compile Include="SugarChecks.fs" />`.

Run, from `examples/Fable.Ranvier.Playground`: `npm run test:dom && npm run test:sugar-counters`
Expected: PASS, the same test count as before the move.

- [ ] **Step 2: Write the failing checks**

In `tests/ArmsChecks.fs`, add `open TwinTrace` after `open CheckSupport`, and insert before `let cases`:

```fsharp
let private initial (graph: Graph) =
    graph.Run (fun () -> Api.createSignal (Loading 10))

let private loadWrites (state: Signal<Load>) =
    [ for step in loadSteps -> Write (fun () -> state.Value <- step) ]

/// <summary>The probe's nine-step trace of each Load fixture equals its twin's (Testing 3).</summary>
let private traces queued () =
    for twin, site in
        [
            loaderTwin, loader
            richTwin, rich
            rowsTwin, rows
            rowsTupleTwin, rowsTuple
            tupledTwin, tupled
            wholeTwin, whole
            formsTwin, forms
            helperTwin, helper
        ] do
        pair queued initial (Component.render twin) (Component.render site) loadWrites

/// <summary>Every value-type payload case on the client equals its twin (Testing 6).</summary>
let private valueTraces queued () =
    let make (graph: Graph) =
        graph.Run (fun () -> Api.createSignal (Count 0))

    let steps (state: Signal<Value>) =
        [
            for value in
                [
                    Ratio 1.5
                    Ratio 2.5
                    Money 2.25m
                    Flag true
                    Flag false
                    Letter 'q'
                    At { X = 3; Y = 4 }
                    At { X = 5; Y = 6 }
                    Maybe (ValueSome 7)
                    Maybe ValueNone
                    Count 9
                ] -> Write (fun () -> state.Value <- value)
        ]

    pair queued make (Component.render valuesTwin) (Component.render values) steps

let private editorState (graph: Graph) =
    graph.Run (fun () ->
        ({
            State = Api.createSignal (Loaded ("a", 1))
            Saved = Api.createSignal ""
        }
        : Editor))

/// <summary>An event handler reads its arm's payload at event time (Testing 5).</summary>
let private eventsReadLatest queued () =
    use graph = new Graph ()
    let state = editorState graph
    let target = host ()
    let mounted = mountTemplate queued graph strict target (Component.render editor state)
    mounted.Flush ()

    let press () =
        click "button" target
        mounted.Flush ()

    write graph [ mounted ] (fun () -> state.State.Value <- Loaded ("b", 1))
    write graph [ mounted ] (fun () -> state.State.Value <- Loaded ("c", 1))
    press ()
    equal "c" (graph.Run (fun () -> state.Saved.Value))
    write graph [ mounted ] (fun () -> state.State.Value <- Idle)
    write graph [ mounted ] (fun () -> state.State.Value <- Loaded ("d", 1))
    press ()
    equal "d" (graph.Run (fun () -> state.Saved.Value))
    mounted.Dispose ()

let private editorTrace queued () =
    pair queued editorState (Component.render editorTwin) (Component.render editor) (fun state ->
        [
            Write (fun () -> state.State.Value <- Loaded ("b", 1))
            Each (click "button")
            Write (fun () -> state.State.Value <- Idle)
            Write (fun () -> state.State.Value <- Loaded ("c", 2))
            Each (click "button")
        ])

/// <summary>Rows fed by the items reader keep their elements across payload updates and show the new payload (Testing 7).</summary>
let private rowsKeep queued () =
    use graph = new Graph ()
    let state = graph.Run (fun () -> Api.createSignal (Loaded ("a", 2)))
    let target = host ()
    let mounted = mountTemplate queued graph strict target (Component.render rowsTuple state)
    mounted.Flush ()
    let first = target.querySelector "li"
    write graph [ mounted ] (fun () -> state.Value <- Loaded ("b", 2))
    equal (true, "b0b1") (obj.ReferenceEquals (first, target.querySelector "li"), textOf target "ul")
    mounted.Dispose ()

type private Row = { Id: int; State: Signal<Load> }

let private rowList =
    Component.template
        (fun _ (rows: Signal<Row array>) -> rows)
        (ul () {
            forEachFixed
                (fun (row: Row) -> row.Id)
                (fun (rows: Signal<Row array>) -> rows.Value)
                (li () {
                    Arms.Match (
                        (fun (row: Row) -> row.State.Value),
                        function
                        | Loaded (text, _) -> b () { fun _ -> text }
                        | _ -> span () { "Other" }
                    )
                })
        })

[<Emit("Array.from($0.querySelectorAll('li'), n => n.textContent)")>]
let private rowTexts (target: HTMLElement) : string array = jsNative

/// <summary>One site per row: after a reorder each row shows its own payload, and a write reaches its row only (Review Focus 4).</summary>
let private siteInRows queued () =
    use graph = new Graph ()

    let rows =
        graph.Run (fun () ->
            Api.createSignal [|
                for id, text in [ 1, "a"; 2, "b"; 3, "c" ] ->
                    {
                        Id = id
                        State = Api.createSignal (Loaded (text, id))
                    }
            |])

    let target = host ()
    let mounted = mountTemplate queued graph strict target (Component.render rowList rows)
    mounted.Flush ()
    equal [| "a"; "b"; "c" |] (rowTexts target)
    write graph [ mounted ] (fun () -> rows.Value <- Array.rev rows.Value)
    equal [| "c"; "b"; "a" |] (rowTexts target)
    write graph [ mounted ] (fun () -> rows.Value[2].State.Value <- Loaded ("z", 1))
    equal [| "c"; "b"; "z" |] (rowTexts target)
    write graph [ mounted ] (fun () -> rows.Value[0].State.Value <- Idle)
    equal [| "Other"; "b"; "z" |] (rowTexts target)
    mounted.Dispose ()

/// <summary>A batch that leaves a target and returns to it shows the last payload and reports nothing (Review Focus 5).</summary>
let private switchBackInOneBatch queued () =
    use graph = new Graph ()
    let state = graph.Run (fun () -> Api.createSignal (Loaded ("a", 1)))
    let target = host ()
    let mounted = mountTemplate queued graph strict target (Component.render loader state)
    mounted.Flush ()

    write graph [ mounted ] (fun () ->
        Api.batch (fun () ->
            state.Value <- Idle
            state.Value <- Loaded ("b", 2)))

    equal "b x2" (textOf target "pre")
    mounted.Dispose ()
```

and add to the `Array.concat` list of `cases`:

```fsharp
                templated "each Load fixture's nine-step trace equals its twin's" traces
                templated "value-type payload fields equal their twin" valueTraces
                templated "an event handler reads its arm's payload at event time" eventsReadLatest
                templated "an event handler's trace equals its twin's" editorTrace
                templated "rows fed by the items reader keep their elements across payload updates" rowsKeep
                templated "one site per keyed row follows its own row through a reorder" siteInRows
                templated "a batch that leaves a target and returns to it shows the last payload" switchBackInOneBatch
```

`tests/arms-counters.test.mjs`:

```js
import test, { after } from "node:test";
import { JSDOM } from "jsdom";
import { cases } from "../generated-tests-counters/ArmsChecks.js";

const dom = new JSDOM("<!doctype html><body></body>");
const names = ["window", "document", "FocusEvent", "MutationObserver", "Node", "Event", "EventTarget"];
for (const name of names) globalThis[name] = name === "window" ? dom.window : dom.window[name];
globalThis.ranvierExpectCounters = true;
after(() => { dom.window.close(); for (const name of names) delete globalThis[name]; delete globalThis.ranvierExpectCounters; });
for (const [name, check] of cases) test(name, () => check());
```

`package.json`: `test:arms` becomes

```json
    "test:arms": "npm run compile:tests -- --define RANVIER_VIEW_COUNTERS && node --test tests/arms-server.test.mjs tests/arms.test.mjs && node tests/sugar-counters-build.mjs && node --test tests/arms-counters.test.mjs",
```

- [ ] **Step 3: Run the checks**

From `examples/Fable.Ranvier.Playground`: `npm run test:arms`
Expected: every new test passes in both builds. These tests pin behaviour the Task 1-5 code already provides; a failure is a defect to fix in `Lower.fs`, `Site.fs` or `Cells.fs`. If only the counters build fails, at `Counts`, print both traces' counter deltas: an Arms.Match reader must make the same core reads as its twin's `payload ()`; a difference is a defect in `Cells.values` (an extra read) unless it is a count of allocations the twin does not make, which is a plan conflict to report.

- [ ] **Step 4: Run the phase gates, format and commit**

All task and phase gates (end of Phase 3), then:

```bash
dotnet fantomas examples/Fable.Ranvier.Playground/tests/TwinTrace.fs examples/Fable.Ranvier.Playground/tests/SugarChecks.fs examples/Fable.Ranvier.Playground/tests/ArmsChecks.fs
git add examples/Fable.Ranvier.Playground/tests/TwinTrace.fs examples/Fable.Ranvier.Playground/tests/SugarChecks.fs examples/Fable.Ranvier.Playground/tests/ArmsChecks.fs examples/Fable.Ranvier.Playground/tests/arms-counters.test.mjs examples/Fable.Ranvier.Playground/tests/DomTests.fsproj examples/Fable.Ranvier.Playground/package.json
git diff --cached --check
git commit -F C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-task-6-commit.txt
git push origin HEAD:feat/arms-match
```

Message subject: `test(arms): mounted traces, events, value payloads and rows against twins`. End of Phase 3.

---

## Phase 4: parity

Start of phase: merge `origin/master`, then the task gates.

### Task 7: Cross-runtime golden file and Strict hydration of .NET server HTML

**Files:**
- Modify: `experiments/Ranvier.View.Arms/Checks/ArmsServerChecks.fs` (`goldenLines`, `dotnetCases`)
- Create: `experiments/Ranvier.View.Arms/Checks/arms-server.golden.txt` (written by `--update`)
- Modify: `experiments/Ranvier.View.Foundation/RepresentationChecks.fsx`
- Modify: `examples/Fable.Ranvier.Playground/tests/arms-server.test.mjs`, `tests/ArmsChecks.fs` (`hydrations`)
- Create: `examples/Fable.Ranvier.Playground/tests/arms-hydration.test.mjs`
- Modify: `examples/Fable.Ranvier.Playground/package.json` (`test:arms`)
- Test: `ArmsServerChecks.dotnetCases`, `tests/arms-server.test.mjs`, `tests/arms-hydration.test.mjs`

**Interfaces:**
- Consumes: `ArmsFixtures.all`, `Case.Render`.
- Produces: `ArmsServerChecks.goldenLines: unit -> string array` (`fixture|state|hash|html` under `Omit`); `ArmsServerChecks.dotnetCases` (.NET); `ArmsChecks.hydrations: (string * (string -> unit)) array`, each taking the served HTML of the fixture's `Loaded done 3` state.

- [ ] **Step 1: Write the golden checks**

Append to `ArmsServerChecks.fs`:

```fsharp
/// <summary>One line per fixture state: fixture, state, the site's structural hash and its server HTML under Omit.</summary>
let goldenLines () : string array =
    [|
        for case in all -> sprintf "%s|%s|%s|%s" case.Fixture case.State (structuralHash case.Site) (fst (case.Render omit))
    |]

#if !FABLE_COMPILER
let private goldenPath =
    System.IO.Path.Combine (__SOURCE_DIRECTORY__, "arms-server.golden.txt")

/// <summary>Compares the .NET lines with the committed file; <c>--update</c> rewrites the file instead.</summary>
let private golden () =
    let lines = goldenLines ()

    if System.Environment.GetCommandLineArgs () |> Array.contains "--update" then
        System.IO.File.WriteAllText (goldenPath, String.concat "\n" lines + "\n")
    else
        let committed =
            System.IO.File.ReadAllText(goldenPath).Replace("\r\n", "\n").TrimEnd('\n').Split '\n'

        equal (List.ofArray committed) (List.ofArray lines)

let dotnetCases: (string * (unit -> unit)) array =
    [| "the .NET server HTML of every fixture equals the golden file", golden |]
#endif
```

`RepresentationChecks.fsx`, after `runCases ArmsServerChecks.cases`:

```fsharp
runCases ArmsServerChecks.dotnetCases
```

`tests/arms-server.test.mjs` becomes:

```js
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { cases, goldenLines } from "../generated-tests/experiments/Ranvier.View.Arms/Checks/ArmsServerChecks.js";

for (const [name, check] of cases) test(name, () => check());

test("the Fable server HTML of every fixture equals the golden file", () => {
  const golden = new URL("../../../experiments/Ranvier.View.Arms/Checks/arms-server.golden.txt", import.meta.url);
  const committed = readFileSync(golden, "utf8").replace(/\r\n/g, "\n").replace(/\n+$/, "").split("\n");
  assert.deepEqual([...goldenLines()], committed);
});
```

In `tests/ArmsChecks.fs`, insert before `let cases`:

```fsharp
[<Emit("$0.firstElementChild")>]
let private firstElement (target: HTMLElement) : obj = jsNative

/// <summary>
/// Each Load fixture hydrates the served HTML of its <c>Loaded done 3</c> state under Strict, then three writes; the
/// site's markup after each, and whether the served root element was adopted, equal its twin's from the same HTML.
/// </summary>
let hydrations: (string * (string -> unit)) array =
    [|
        for name, site, twin in
            [
                "loader", loader, loaderTwin
                "rich", rich, richTwin
                "rows", rows, rowsTwin
                "rowsTuple", rowsTuple, rowsTupleTwin
                "tupled", tupled, tupledTwin
                "whole", whole, wholeTwin
                "helper", helper, helperTwin
            ] ->
            name,
            fun (served: string) ->
                let shots (definition: Component<Signal<Load>>) =
                    use graph = new Graph ()
                    let state = graph.Run (fun () -> Api.createSignal (Loaded ("done", 3)))
                    let target = host ()
                    loadServed target served
                    let root = firstElement target

                    let mounted =
                        Hydration.hydrate graph None strict { Hydration.HydrationOptions.defaults with Strict = true } target definition state

                    let shots = ResizeArray [ sprintf "adopted=%b %s" (obj.ReferenceEquals (root, firstElement target)) target.innerHTML ]

                    for step in [ Loaded ("later", 4); Loading 5; Loaded ("again", 1) ] do
                        graph.Run (fun () -> state.Value <- step)
                        mounted.Flush ()
                        shots.Add target.innerHTML

                    mounted.Dispose ()
                    List.ofSeq shots

                let twinShots = shots twin
                equal true (twinShots.Head.StartsWith "adopted=true")
                equal twinShots (shots site)
    |]
```

`tests/arms-hydration.test.mjs`:

```js
import test, { after } from "node:test";
import { readFileSync } from "node:fs";
import { JSDOM } from "jsdom";
import { hydrations } from "../generated-tests/ArmsChecks.js";

const dom = new JSDOM("<!doctype html><body></body>");
const names = ["window", "document", "FocusEvent", "MutationObserver", "Node", "Event", "EventTarget"];
for (const name of names) globalThis[name] = name === "window" ? dom.window : dom.window[name];
after(() => { dom.window.close(); for (const name of names) delete globalThis[name]; });

const golden = readFileSync(new URL("../../../experiments/Ranvier.View.Arms/Checks/arms-server.golden.txt", import.meta.url), "utf8")
  .replace(/\r\n/g, "\n").replace(/\n+$/, "").split("\n").map(line => line.split("|"));

for (const [name, check] of hydrations)
  test(`${name} hydrates its .NET server HTML under Strict and updates like its twin`, () => {
    const line = golden.find(([fixture, state]) => fixture === name && state === "Loaded done 3");
    check(line.slice(3).join("|"));
  });
```

`package.json`: in `test:arms`, `node --test tests/arms-server.test.mjs tests/arms.test.mjs` becomes `node --test tests/arms-server.test.mjs tests/arms.test.mjs tests/arms-hydration.test.mjs`.

- [ ] **Step 2: Run the checks to verify they fail**

```bash
dotnet run --project experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj -c Release
```

Expected: FAIL at `the .NET server HTML of every fixture equals the golden file`, the file does not exist.

- [ ] **Step 3: Write the golden file from .NET, and read it**

```bash
dotnet run --project experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj -c Release -- --update
```

Read `experiments/Ranvier.View.Arms/Checks/arms-server.golden.txt`: one line per state of `ArmsFixtures.all`, the `loader` lines carrying `220727aa`, `a8ebc344` and `646a1db6`, and the `rich` lines `c054a3d7` and `7f75a908` (the probe's hashes). No line starts its HTML with `raised: ` except the `editor` lines, which render under `Omit` and must not raise either; a `raised: ` line is a defect.

- [ ] **Step 4: Run both sides**

```bash
dotnet run --project experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj -c Release
cd examples/Fable.Ranvier.Playground && npm run test:arms
```

Expected: PASS on .NET; under Fable the golden test and the seven hydration tests pass. A golden difference is a parity defect: the side whose HTML differs from its own twin's is wrong; when both sides match their twins but differ from each other, the fixture prints a value whose text differs between .NET and JavaScript (`string` of a `float` or `bool`), and the fixture changes to print an equal text.

- [ ] **Step 5: Format and commit**

```bash
dotnet fantomas experiments/Ranvier.View.Arms/Checks/ArmsServerChecks.fs examples/Fable.Ranvier.Playground/tests/ArmsChecks.fs experiments/Ranvier.View.Foundation/RepresentationChecks.fsx
git add experiments/Ranvier.View.Arms/Checks experiments/Ranvier.View.Foundation/RepresentationChecks.fsx examples/Fable.Ranvier.Playground/tests/arms-server.test.mjs examples/Fable.Ranvier.Playground/tests/arms-hydration.test.mjs examples/Fable.Ranvier.Playground/tests/ArmsChecks.fs examples/Fable.Ranvier.Playground/package.json
git diff --cached --check
git commit -F C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-task-7-commit.txt
git push origin HEAD:feat/arms-match
```

Message subject: `test(arms): cross-runtime golden server HTML and Strict hydration`.

---

### Task 8: Fable Debug build, held exits and pending sources

**Files:**
- Modify: `examples/Fable.Ranvier.Playground/tests/ArmsChecks.fs`
- Create: `examples/Fable.Ranvier.Playground/tests/arms-debug.test.mjs`
- Modify: `examples/Fable.Ranvier.Playground/package.json` (`test:sugar-debug`)
- Test: `ArmsChecks.cases` in the Debug build; the held-exit and pending cases in every build

**Interfaces:**
- Consumes: `ArmsChecks` helpers (`textOf`, `write`), `CheckSupport`.
- Produces: two more `ArmsChecks.cases` entries.

- [ ] **Step 1: Write the failing checks**

In `tests/ArmsChecks.fs`, insert before `let cases`:

```fsharp
let private finishes = ResizeArray<unit -> unit>()

/// <summary>A site whose Loaded body holds its root on exit until <c>finish</c> runs.</summary>
let private held =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            Arms.Match (
                (fun (state: Signal<Load>) -> state.Value),
                function
                | Loaded (text, count) ->
                    pre (
                        Hooks.exit (
                            Func<_, _, _, _>(fun (_: Signal<Load>) (_: HTMLPreElement) finish ->
                                finishes.Add finish
                                ignore)
                        )
                    ) {
                        fun _ -> text
                        " x"
                        fun _ -> string count
                    }
                | _ -> span () { "Other" }
            )
        })

/// <summary>The outgoing arm, held by its exit, keeps its own case's values until finish (Testing 9).</summary>
let private heldExit queued () =
    finishes.Clear ()
    use graph = new Graph ()
    let state = graph.Run (fun () -> Api.createSignal (Loaded ("a", 1)))
    let target = host ()
    let mounted = mountTemplate queued graph strict target (Component.render held state)
    mounted.Flush ()
    write graph [ mounted ] (fun () -> state.Value <- Idle)
    equal ("a x1", "Other") (textOf target "pre", textOf target "span")
    write graph [ mounted ] (fun () -> state.Value <- Loading 3)
    equal ("a x1", "Other") (textOf target "pre", textOf target "span")
    let pending = List.ofSeq finishes
    finishes.Clear ()

    for finish in pending do
        finish ()

    mounted.Flush ()
    equal (null, "Other") (textOf target "pre", textOf target "span")
    mounted.Dispose ()

[<Emit("$0.querySelectorAll($1).length")>]
let private countOf (target: HTMLElement) (selector: string) : int = jsNative

let private pendingView =
    Component.template (fun _ () -> ()) (p (Attrs.className "pending") { "pending" })

let private failedView =
    Component.template (fun _ (failure: BoundaryFailure) -> failure) (p (Attrs.className "failed") { fun failure -> failure.Error.Message })

/// <summary>A boundary whose content is an Arms.Match site over an async value.</summary>
let private bounded (flight: Signal<AsyncSource<int>>) =
    let content =
        Component.template
            (fun _ (status: BoundaryStatus) -> status)
            (div (Attrs.className "level") {
                Arms.Match (
                    (fun (_: BoundaryStatus) -> flight.Value.Value),
                    function
                    | 0 -> em () { "none" }
                    | n -> b () { fun _ -> string n }
                )
            })

    Component.template (fun _ () -> ()) (section () { boundary pendingView failedView content })

/// <summary>A pending read waits with the boundary, as matchWith does (Testing 10).</summary>
let private pendingSource queued () =
    use graph = new Graph ()
    let flight = graph.Run (fun () -> Api.createSignal (Api.createAsyncSource<int>()))
    let target = host ()

    let mounted =
        Mounting.mountWith (modeOf queued) graph None strict target None (fun context ->
            Program.run (Component.render (bounded flight) ()) context.Backend)

    let state () =
        mounted.Flush ()
        mounted.Flush ()
        countOf target ".pending", countOf target ".failed", textOf target ".level"

    equal (1, 0, null) (state ())
    graph.Run (fun () -> flight.Value.Settle 2)
    equal (0, 0, "2") (state ())
    graph.Run (fun () -> flight.Value <- Api.createAsyncSource<int>())
    graph.Run (fun () -> flight.Value.Settle 0)
    equal (0, 0, "none") (state ())
    mounted.Dispose ()
    equal "" target.textContent
```

and add to the `Array.concat` list of `cases`:

```fsharp
                templated "an outgoing arm held by its exit keeps its own case's values until finish" heldExit
                templated "a pending source inside a boundary waits with the boundary" pendingSource
```

`tests/arms-debug.test.mjs`:

```js
import test, { after } from "node:test";
import { JSDOM } from "jsdom";
import { cases } from "../generated-tests-debug/ArmsChecks.js";

const dom = new JSDOM("<!doctype html><body></body>");
const names = ["window", "document", "FocusEvent", "MutationObserver", "Node", "Event", "EventTarget"];
for (const name of names) globalThis[name] = name === "window" ? dom.window : dom.window[name];
after(() => { dom.window.close(); for (const name of names) delete globalThis[name]; });
for (const [name, check] of cases) test(`debug: ${name}`, () => check());
```

`package.json`: in `test:sugar-debug`, `node --test tests/sugar-debug.test.mjs` becomes `node --test tests/sugar-debug.test.mjs tests/arms-debug.test.mjs`.

- [ ] **Step 2: Run the checks**

From `examples/Fable.Ranvier.Playground`: `npm run test:arms && npm run test:sugar-debug`
Expected: PASS. The Debug suite runs every arms case, so "a template definition was created while a region was read" in any report fails it; such a failure means a definition is built inside a region read, which `Site.create` must not do. If `heldExit` sees `textOf target "pre"` change to another case's text, a held arm's reader read the next case: `Cells.values` must return the withdrawn values when the target has no live entry. If `pendingSource` reports a failure instead of waiting, check `Site.create`'s selector runs inside `matchWith`'s region read, where #137's pending handling applies.

- [ ] **Step 3: Run the phase gates, format and commit**

All task and phase gates (end of Phase 4), then:

```bash
dotnet fantomas examples/Fable.Ranvier.Playground/tests/ArmsChecks.fs
git add examples/Fable.Ranvier.Playground/tests/ArmsChecks.fs examples/Fable.Ranvier.Playground/tests/arms-debug.test.mjs examples/Fable.Ranvier.Playground/package.json
git diff --cached --check
git commit -F C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-task-8-commit.txt
git push origin HEAD:feat/arms-match
```

Message subject: `test(arms): Debug build, held exits and pending sources`. End of Phase 4.

---

## Phase 5: budgets

Start of phase: merge `origin/master`, then the task gates.

### Task 9: Bundle budget

**Files:**
- Create: `examples/Fable.Ranvier.Playground/tests/ArmsSizeEntries.fs`
- Create: `experiments/Ranvier.View.Arms/Checks/size.mjs`, `experiments/Ranvier.View.Arms/Checks/size.config.mjs`, `experiments/Ranvier.View.Arms/Checks/.gitignore`
- Create: `docs/research/arms-match/bundle.json` (written by `size.mjs`)
- Modify: `examples/Fable.Ranvier.Playground/tests/DomTests.fsproj`, `docs/research/2026-10-21-arms-match.md` (a "Bundle" section)
- Test: `node experiments/Ranvier.View.Arms/Checks/size.mjs` (exit code 1 on a breach)

**Interfaces:**
- Consumes: `Arms.Match`; the Playground's `generated-tests` (`npm run compile:tests`).
- Produces: `ArmsSizeEntries.matchOne`, `explicitOne`, `matchTwo`, `explicitTwo`, `plain`, each `Graph -> HTMLElement -> Signal<Load> -> unit`; `docs/research/arms-match/bundle.json` with `sizes`, `one`, `perSite`, `plain`, `quotation`, `helpers`, each budget with `withinBudget`.

- [ ] **Step 1: Write the size entries**

`examples/Fable.Ranvier.Playground/tests/ArmsSizeEntries.fs` (each entry builds its definitions when called, so an entry's bundle holds only its own sites):

```fsharp
/// <summary>The bundle-size entries of experiments/Ranvier.View.Arms/Checks/size.mjs: loader sites as Arms.Match and as their explicit twins.</summary>
module ArmsSizeEntries

open global.Ranvier
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms

type Load =
    | Idle
    | Loading of percent: int
    | Loaded of text: string * count: int

let private mountAll (graph: Graph) (host: HTMLElement) (views: Program.View list) =
    for view in views do
        Mounting.mountWith Scheduling.SynchronousCommits graph None ignore host None (fun context -> Program.run view context.Backend)
        |> ignore

let private matchLoader () =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "loader") {
            Arms.Match (
                (fun (state: Signal<Load>) -> state.Value),
                function
                | Idle -> span () { "Not started" }
                | Loading percent -> progress (Attrs.attribute ("max", "100"), Attrs.attribute ("value", fun _ -> Some (string percent))) { }
                | Loaded (text, count) ->
                    pre () {
                        fun _ -> text
                        " x"
                        fun _ -> string count
                    }
            )
        })

let private explicitLoader () =
    let idle = armOf (span () { "Not started" })

    let loading =
        armOf (progress (Attrs.attribute ("max", "100"), Attrs.attribute ("value", fun (payload: unit -> int) -> Some (string (payload ())))) { })

    let loaded =
        armOf (
            pre () {
                fun (payload: unit -> string * int) -> fst (payload ())
                " x"
                fun (payload: unit -> string * int) -> string (snd (payload ()))
            }
        )

    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "loader") {
            matchWith (fun (state: Signal<Load>) -> state.Value) (function
                | Idle -> arm idle ()
                | Loading percent -> arm loading percent
                | Loaded (text, count) -> arm loaded (text, count))
        })

let private matchRich () =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "rich") {
            Arms.Match (
                (fun (state: Signal<Load>) -> state.Value),
                function
                | Idle
                | Loading 0 -> span () { "Not started" }
                | Loading percent -> b () { fun _ -> string percent }
                | Loaded (_, 0) -> span () { "Empty" }
                | Loaded (text, _) -> i () { fun _ -> text }
            )
        })

let private explicitRich () =
    let idle = armOf (span () { "Not started" })
    let percent = armOf (b () { fun (payload: unit -> int) -> string (payload ()) })
    let empty = armOf (span () { "Empty" })
    let text = armOf (i () { fun (payload: unit -> string) -> payload () })

    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "rich") {
            matchWith (fun (state: Signal<Load>) -> state.Value) (function
                | Idle
                | Loading 0 -> arm idle ()
                | Loading value -> arm percent value
                | Loaded (_, 0) -> arm empty ()
                | Loaded (value, _) -> arm text value)
        })

let matchOne graph host (state: Signal<Load>) =
    mountAll graph host [ Component.render (matchLoader ()) state ]

let explicitOne graph host (state: Signal<Load>) =
    mountAll graph host [ Component.render (explicitLoader ()) state ]

let matchTwo graph host (state: Signal<Load>) =
    mountAll graph host [ Component.render (matchLoader ()) state; Component.render (matchRich ()) state ]

let explicitTwo graph host (state: Signal<Load>) =
    mountAll graph host [ Component.render (explicitLoader ()) state; Component.render (explicitRich ()) state ]

/// <summary>An app that compiles the Arms.Match runtime and calls no Arms.Match.</summary>
let plain graph host (state: Signal<Load>) =
    mountAll graph host [ Component.render (Component.template (fun _ (state: Signal<Load>) -> state) (div () { "plain" })) state ]
```

`DomTests.fsproj`: after `<Compile Include="ArmsChecks.fs" />` add `<Compile Include="ArmsSizeEntries.fs" />`.

- [ ] **Step 2: Write the size script**

`experiments/Ranvier.View.Arms/Checks/.gitignore`:

```
generated-size/
```

`experiments/Ranvier.View.Arms/Checks/size.config.mjs`:

```js
// Bundles one Arms.Match size entry for size.mjs: ARMS_SIZE_ENTRY names it, ARMS_SIZE_MINIFY=false keeps it readable.
const entry = process.env.ARMS_SIZE_ENTRY;
const minify = process.env.ARMS_SIZE_MINIFY !== "false";
export default {
  logLevel: "warn",
  build: {
    outDir: `generated-size/${entry}${minify ? "" : "-unminified"}`, emptyOutDir: true, minify, modulePreload: false,
    rollupOptions: { input: `generated-size/entry-${entry}.mjs`, preserveEntrySignatures: "strict", output: { entryFileNames: "bundle.js", inlineDynamicImports: true } }
  }
};
```

`experiments/Ranvier.View.Arms/Checks/size.mjs`:

```js
// Bundle budget of Arms.Match (spec, "Bundle budget"), over the ArmsSizeEntries functions in the Playground's
// generated-tests (run npm run compile:tests there first), bundled by the Playground's Vite with minification, gzip
// level 9 and Brotli quality 11:
// - one: the Arms.Match loader against its explicit twin, at most 250 B gzip;
// - perSite: a second site's delta (two sites minus one) against its twin's, at most 60 B gzip;
// - plain: an entry calling no Arms.Match holds no Ranvier.View.Arms module (Ruling 12);
// - quotation: the unminified Arms.Match entry holds no quotation code;
// - helpers: the emitted Ranvier.View.Arms modules and ArmsSizeEntries.js hold no curry, uncurry or partialApply.
// Exits 1 on a breach. Writes docs/research/arms-match/bundle.json.
import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { gzipSync, brotliCompressSync, constants } from "node:zlib";

const here = new URL("./", import.meta.url);
const playground = new URL("../../../examples/Fable.Ranvier.Playground/", here);
const generated = new URL("generated-tests/", playground);
const output = new URL("../../../docs/research/arms-match/bundle.json", here);
const pathOf = url => decodeURIComponent(url.pathname).replace(/^\/([A-Za-z]:)/, "$1");
mkdirSync(new URL("generated-size/", here), { recursive: true });

const build = (name, minify) => {
  writeFileSync(new URL(`generated-size/entry-${name}.mjs`, here),
    `export { ${name} as mount } from "../../../../examples/Fable.Ranvier.Playground/generated-tests/ArmsSizeEntries.js";\n`);
  execFileSync(process.execPath, [pathOf(new URL("node_modules/vite/bin/vite.js", playground)), "build", "--config", "size.config.mjs"], {
    cwd: pathOf(here), stdio: ["ignore", 2, 2], windowsHide: true, env: { ...process.env, ARMS_SIZE_ENTRY: name, ARMS_SIZE_MINIFY: String(minify) }
  });
  return readFileSync(new URL(`generated-size/${name}${minify ? "" : "-unminified"}/bundle.js`, here));
};
const sizeOf = code => ({
  raw: code.length,
  gzip: gzipSync(code, { level: 9 }).length,
  brotli: brotliCompressSync(code, { params: { [constants.BROTLI_PARAM_QUALITY]: 11 } }).length
});

const sizes = Object.fromEntries(["explicitOne", "matchOne", "explicitTwo", "matchTwo", "plain"].map(name => [name, sizeOf(build(name, true))]));
const one = sizes.matchOne.gzip - sizes.explicitOne.gzip;
const perSite = sizes.matchTwo.gzip - sizes.explicitTwo.gzip - one;
// Unminified bundles keep a region comment naming each bundled module's path.
const armsModule = /Ranvier\.View\.Arms\//;
const matchUnminified = build("matchOne", false).toString();
const plainHoldsArms = armsModule.test(build("plain", false).toString());
const quotation = /quotation/i.test(matchUnminified);
const helperFiles = ["experiments/Ranvier.View.Arms/Cells.js", "experiments/Ranvier.View.Arms/Site.js", "experiments/Ranvier.View.Arms/Arms.js", "ArmsSizeEntries.js"];
const helpers = Object.fromEntries(helperFiles.map(file =>
  [file, (readFileSync(new URL(file, generated), "utf8").match(/\b(curry\d*|uncurry\d*|partialApply)\b/g) ?? []).length]));

const result = {
  sizes,
  one: { deltaGzip: one, budgetGzip: 250, withinBudget: one <= 250 },
  perSite: { deltaGzip: perSite, budgetGzip: 60, withinBudget: perSite <= 60 },
  plain: { matchEntryHoldsArmsModule: armsModule.test(matchUnminified), plainHoldsArmsModule: plainHoldsArms, withinBudget: armsModule.test(matchUnminified) && !plainHoldsArms },
  quotation: { present: quotation, withinBudget: !quotation },
  helpers: { counts: helpers, withinBudget: Object.values(helpers).every(count => count === 0) }
};
mkdirSync(new URL("./", output), { recursive: true });
writeFileSync(output, JSON.stringify(result, null, 2) + "\n");
console.log(JSON.stringify(result, null, 2));
if (["one", "perSite", "plain", "quotation", "helpers"].some(key => !result[key].withinBudget)) process.exitCode = 1;
```

- [ ] **Step 3: Run it**

```bash
cd examples/Fable.Ranvier.Playground && npm run compile:tests && cd ../..
node experiments/Ranvier.View.Arms/Checks/size.mjs
```

Expected: exit 0; `one.deltaGzip` ≤ 250, `perSite.deltaGzip` ≤ 60, `plain.matchEntryHoldsArmsModule` true and `plain.plainHoldsArmsModule` false, `quotation.present` false, every `helpers.counts` 0. If `matchEntryHoldsArmsModule` is false, the bundler writes no region comments: switch both tests to a search for `Site_create` (the emitted name of `Site.create`) and ledger it. A budget breach is a spec conflict to report with the numbers; a nonzero helper count names the file: fix the curried call it holds (an F# function value passed where a tupled or `Func` form fits) and rerun.

- [ ] **Step 4: Record the bundle numbers**

Append to `docs/research/2026-10-21-arms-match.md`:

```markdown
## Bundle

`experiments/Ranvier.View.Arms/Checks/size.mjs`, Vite 8.3.2 with minification, gzip level 9, Brotli quality 11; the
numbers are in [`arms-match/bundle.json`](arms-match/bundle.json).

| Entry | raw | gzip | Brotli |
|---|---|---|---|
| explicit loader | <raw> | <gzip> | <brotli> |
| Arms.Match loader | <raw> | <gzip> | <brotli> |
| two explicit sites | <raw> | <gzip> | <brotli> |
| two Arms.Match sites | <raw> | <gzip> | <brotli> |

The first site costs <one> B gzip over its twin (budget 250; the probe's single-slot cells cost 139), and the second
<perSite> B (budget 60). An entry that calls no `Arms.Match` holds no `Ranvier.View.Arms` module, and the Playground
explicit entry is byte-identical to its bytes before this work.
```

with the values of `bundle.json`.

- [ ] **Step 5: Format and commit**

```bash
dotnet fantomas examples/Fable.Ranvier.Playground/tests/ArmsSizeEntries.fs
git add examples/Fable.Ranvier.Playground/tests/ArmsSizeEntries.fs examples/Fable.Ranvier.Playground/tests/DomTests.fsproj experiments/Ranvier.View.Arms/Checks/size.mjs experiments/Ranvier.View.Arms/Checks/size.config.mjs experiments/Ranvier.View.Arms/Checks/.gitignore docs/research/arms-match/bundle.json docs/research/2026-10-21-arms-match.md
git diff --cached --check
git commit -F C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-task-9-commit.txt
git push origin HEAD:feat/arms-match
```

Message subject: `test(arms): bundle budget`.

---

### Task 10: Chromium benchmark mode and heap profile

Merge `origin/master` before starting (this task edits `DomTests.fsproj`).

**Files:**
- Create: `examples/Fable.Ranvier.Playground/tests/KeyedRowsArmsMatchPerf.fs`
- Modify: `examples/Fable.Ranvier.Playground/tests/DomTests.fsproj`, `benchmarks/frameworks/keyed-page.mjs`, `benchmarks/frameworks/keyed-rows.mjs`, `benchmarks/frameworks/keyed-heap.mjs`
- Create: `docs/research/arms-match/` result files (written by the harness)
- Modify: `docs/research/2026-10-21-arms-match.md` ("Chromium" and "Heap" sections)
- Test: `keyed-rows.mjs --verify-only` equivalence through the harness's `verify`, then the timed run

**Interfaces:**
- Consumes: `KeyedRowsPerf.ArmMode`, `ArmRow`, `ArmOperations`.
- Produces: `KeyedRowsArmsMatchPerf.mountArms: HTMLElement -> ArmOperations`; the benchmark mode `ranvier-arms-match` (arm fixture only); `window.keyedPerf.armModes`; `keyedPerf.mounted(mode, fixture)`; `keyed-heap.mjs --modes=a,b --fixture=arms`.

- [ ] **Step 1: Write the fixture**

`examples/Fable.Ranvier.Playground/tests/KeyedRowsArmsMatchPerf.fs`:

```fsharp
/// <summary>The arm benchmark fixture of <c>KeyedRowsSugarPerf</c> with its arm site written as Arms.Match.</summary>
module KeyedRowsArmsMatchPerf

open global.Ranvier
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms
open KeyedRowsPerf

let private armList =
    Component.template
        (fun _ (rows: Signal<ArmRow array>) -> rows)
        (table () {
            tbody () {
                forEachFixed
                    (fun row -> row.Id)
                    (fun (rows: Signal<_ array>) -> rows.Value)
                    (tr () {
                        td () { fun row -> string row.Id }

                        td () {
                            Arms.Match (
                                (fun (row: ArmRow) -> row.Mode.Value),
                                function
                                | Plain -> span (Attrs.className "a") { "A" }
                                | Counted count -> b () { fun _ -> string count }
                            )
                        }
                    })
            }
        })

/// <summary>Mounts an empty Arms.Match arm list into the host.</summary>
let mountArms (host: HTMLElement) : ArmOperations =
    let graph = new Graph ()
    let rows = graph.Run (fun () -> Api.createSignal ([||]: ArmRow array))

    let mounted =
        Mounting.mount graph None (fun errors -> failwithf "%A" errors) host None (fun context ->
            Program.run (Component.render armList rows) context.Backend)

    let mutable live = true

    let each change =
        graph.Run (fun () ->
            Api.batch (fun () ->
                for row in rows.Value do
                    row.Mode.Value <- change row.Mode.Value))

    {
        Create =
            fun count ->
                graph.Run (fun () ->
                    rows.Value <-
                        Array.init count (fun index ->
                            {
                                Id = index + 1
                                Mode = Api.createSignal (if index % 2 = 0 then Plain else Counted 0)
                            }))
        Switch =
            fun () ->
                each (function
                    | Plain -> Counted 0
                    | Counted _ -> Plain)
        Payload =
            fun () ->
                each (function
                    | Plain -> Plain
                    | Counted count -> Counted (count + 1))
        Clear = fun () -> graph.Run (fun () -> rows.Value <- [||])
        Dispose =
            fun () ->
                if live then
                    live <- false
                    mounted.Dispose ()
                    graph.Dispose ()
    }
```

`DomTests.fsproj`: after `<Compile Include="KeyedRowsSugarPerf.fs" />` add `<Compile Include="KeyedRowsArmsMatchPerf.fs" />`. `KeyedRowsSugarPerf.fs` sits before `ArmsChecks.fs` and the arms sources are linked after `Server.fs`, so the order holds.

- [ ] **Step 2: Add the mode to the harness**

`benchmarks/frameworks/keyed-page.mjs`:

- after the second import line add
  ```js
  import { mountArms as armsMatchMount } from "../../generated-tests/KeyedRowsArmsMatchPerf.js";
  ```
- after `const sugarArms = ...;` add
  ```js
  // ranvier-arms-match: the sugared arm fixture with its site written as Arms.Match (arm operations only).
  const armsMatchArms = host => {
    const ops = armsMatchMount(host);
    return { create: n => ops.Create(n), switch: () => ops.Switch(), payload: () => ops.Payload(), clear: () => ops.Clear(), dispose: () => ops.Dispose() };
  };
  ```
- `const armAdapters = { ranvier: ranvierArms, "ranvier-sugar": sugarArms, solid: solidArms };` becomes
  ```js
  const armAdapters = { ranvier: ranvierArms, "ranvier-sugar": sugarArms, "ranvier-arms-match": armsMatchArms, solid: solidArms };
  ```
- in `window.keyedPerf`, after `modes: Object.keys(adapters),` add
  ```js
    /// Modes with an arm adapter only; selectable with --modes.
    armModes: Object.keys(armAdapters).filter(mode => !(mode in adapters)),
  ```
- `mounted(mode) {` and its first line become
  ```js
  mounted(mode, fixture) {
    const node = host(), list = (fixture === "arms" ? armAdapters : adapters)[mode](node);
  ```

`benchmarks/frameworks/keyed-rows.mjs`, in the replicate loop:

```js
    ({ modes, names } = await page.evaluate(() => ({ modes: window.keyedPerf.modes, names: window.keyedPerf.operations })));
    modes = only(modes, "modes"); names = only(names, "ops");
```

becomes

```js
    let armModes;
    ({ modes, armModes, names } = await page.evaluate(() => ({ modes: window.keyedPerf.modes, armModes: window.keyedPerf.armModes, names: window.keyedPerf.operations })));
    modes = argument("modes") ? only([...modes, ...armModes], "modes") : modes; names = only(names, "ops");
```

`benchmarks/frameworks/keyed-heap.mjs`:

- `const bundles = { production: ["ranvier", "ranvier-sugar", "solid"] };` becomes
  ```js
  // `--modes=a,b` picks the modes; `--fixture=arms` mounts the arm fixture instead of the keyed rows.
  const bundles = { production: argument("modes", "ranvier,ranvier-sugar,solid").split(",") };
  const fixture = argument("fixture", "rows");
  ```
- both `window.keyedPerf.mounted(mode)` calls take the fixture: `window.keyedPerf.mounted(mode, fixture)`, with `fixture` passed in the `page.evaluate` argument object beside `mode` (`({ mode, fixture }) => ...`, `{ mode, fixture }`).

- [ ] **Step 3: Verify the mode renders the same rows**

From `examples/Fable.Ranvier.Playground`, under both locks:

```bash
until mkdir C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/bench.lock 2>/dev/null; do sleep 30; done
until mkdir C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/browser.lock 2>/dev/null; do sleep 30; done
node benchmarks/frameworks/keyed-rows.mjs --build --modes=ranvier-sugar,ranvier-arms-match --ops=armsCreate1k,armsSwitch1k,armsPayload1k,armsClear1k --processes=1 --rounds=1 --out=C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-bench-smoke/
```

Expected: exit 0; the harness's per-operation `verify` reports equal row texts for both modes (it fails the run otherwise). Keep both locks for Step 4.

- [ ] **Step 4: Run the timed benchmark and the heap profile, then release the locks**

```bash
node benchmarks/frameworks/keyed-rows.mjs --bundle --modes=ranvier-sugar,ranvier-arms-match --ops=armsCreate1k,armsSwitch1k,armsPayload1k,armsClear1k --processes=3 --rounds=15 --label=arms-match --out=../../docs/research/arms-match/
node benchmarks/frameworks/keyed-heap.mjs --modes=ranvier-sugar,ranvier-arms-match --fixture=arms --counts=1000,10000 --label=arms-match-heap --out=../../docs/research/arms-match/
rmdir C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/browser.lock
rmdir C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/bench.lock
```

Expected: both exit 0 and write JSON files under `docs/research/arms-match/`. Gates (Global Constraints): each operation's `ranvier-arms-match` median within 5 % of `ranvier-sugar`, or their ranges overlap; retained bytes per row within 10 % of `ranvier-sugar` plus one `WeakMap` entry and one entry list per row instance. A miss is a spec conflict: report the table, do not tune the benchmark.

- [ ] **Step 5: Record the results**

Append to `docs/research/2026-10-21-arms-match.md`:

```markdown
## Chromium

`keyed-rows.mjs --modes=ranvier-sugar,ranvier-arms-match --ops=armsCreate1k,armsSwitch1k,armsPayload1k,armsClear1k
--processes=3 --rounds=15`, under the benchmark and browser locks; results in `arms-match/`.

| Operation | ranvier-sugar median (range), ms | ranvier-arms-match median (range), ms | Δ median |
|---|---|---|---|
| armsCreate1k | | | |
| armsSwitch1k | | | |
| armsPayload1k | | | |
| armsClear1k | | | |

## Heap

`keyed-heap.mjs --modes=ranvier-sugar,ranvier-arms-match --fixture=arms --counts=1000,10000`.

| Rows | ranvier-sugar bytes per row | ranvier-arms-match bytes per row | Δ |
|---|---|---|---|
| 1,000 | | | |
| 10,000 | | | |
```

with every cell filled from the JSON files, and one sentence per table stating whether its gate holds.

- [ ] **Step 6: Format and commit**

```bash
dotnet fantomas examples/Fable.Ranvier.Playground/tests/KeyedRowsArmsMatchPerf.fs
git add examples/Fable.Ranvier.Playground/tests/KeyedRowsArmsMatchPerf.fs examples/Fable.Ranvier.Playground/tests/DomTests.fsproj examples/Fable.Ranvier.Playground/benchmarks/frameworks/keyed-page.mjs examples/Fable.Ranvier.Playground/benchmarks/frameworks/keyed-rows.mjs examples/Fable.Ranvier.Playground/benchmarks/frameworks/keyed-heap.mjs docs/research/arms-match docs/research/2026-10-21-arms-match.md
git diff --cached --check
git commit -F C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-task-10-commit.txt
git push origin HEAD:feat/arms-match
```

Message subject: `perf(arms): Chromium arm benchmark mode and heap profile`.

---

### Task 11: Trim annotation check and the CI `arms` job

**Files:**
- Create: `experiments/Ranvier.View.Arms/Checks/TrimCallSite/TrimCallSite.fsproj`, `Main.fs`, `check.sh`
- Modify: `.github/workflows/ci.yml`, `Ranvier.slnx`
- Test: `bash experiments/Ranvier.View.Arms/Checks/TrimCallSite/check.sh win-x64` (locally), `linux-x64` in CI

**Interfaces:**
- Consumes: the `.NET` `Arms.Match` annotations (Task 1).
- Produces: the CI job `arms`, in `result.needs`.

- [ ] **Step 1: Write the trim check**

`experiments/Ranvier.View.Arms/Checks/TrimCallSite/TrimCallSite.fsproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- One Arms.Match call published trimmed: ILLink reports IL2026 naming Arms.Match at the call. -->
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <PublishTrimmed>true</PublishTrimmed>
    <TrimmerSingleWarn>false</TrimmerSingleWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Main.fs" />
    <ProjectReference Include="../../Ranvier.View.Arms.fsproj" />
    <PackageReference Update="FSharp.Core" Version="10.1.401" />
  </ItemGroup>
</Project>
```

`Main.fs`:

```fsharp
/// <summary>One Arms.Match call, for the trimmed publish of check.sh.</summary>
module TrimCallSite.Main

open global.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms

let site =
    Component.template
        (fun _ (state: Signal<int option>) -> state)
        (div () {
            Arms.Match (
                (fun (state: Signal<int option>) -> state.Value),
                function
                | Some n -> b () { fun _ -> string n }
                | None -> span () { "none" }
            )
        })

[<EntryPoint>]
let main _ =
    use graph = new Graph ()
    let state = graph.Run (fun () -> Api.createSignal (Some 1))
    printfn "%s" (Server.render Server.Options.defaults site state)
    0
```

`check.sh`:

```bash
#!/usr/bin/env bash
# Publishes TrimCallSite trimmed for the runtime identifier in $1 (linux-x64 by default) and fails unless ILLink reports
# IL2026 for its Arms.Match call.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
log="$(mktemp)"
dotnet publish "$here/TrimCallSite.fsproj" -c Release -r "${1:-linux-x64}" -o "$here/bin/publish" 2>&1 | tee "$log"
if ! grep -E "IL2026.*Arms\.Match" "$log" > /dev/null; then
  echo "The trimmed publish reported no IL2026 naming Arms.Match" >&2
  exit 1
fi
```

`Ranvier.slnx`, in the `/experiments/Ranvier.View.Arms/` folder:

```xml
    <Project Path="experiments/Ranvier.View.Arms/Checks/TrimCallSite/TrimCallSite.fsproj" />
```

- [ ] **Step 2: Run it, then remove the annotation to see it fail**

```bash
bash experiments/Ranvier.View.Arms/Checks/TrimCallSite/check.sh win-x64
```

Expected: exit 0, the log holds `IL2026` naming `Ranvier.View.Arms.Arms.Match`. Then delete `RequiresUnreferencedCode(Diagnostics.reflection); ` from `Arms.fs`, rerun: expected exit 1 with "reported no IL2026"; restore the attribute (`git checkout -- experiments/Ranvier.View.Arms/Arms.fs`) and rerun: exit 0.

- [ ] **Step 3: Add the CI job**

In `.github/workflows/ci.yml`, before the `result:` job, add:

```yaml
  # Arms.Match (experiments/Ranvier.View.Arms): the Fable plugin's tests, the .NET lowering, fixture, golden and runtime
  # checks inside the representation checks, the jsdom suites (test:arms), the bundle budget, and the trim annotation.
  arms:
    name: Arms.Match
    needs: changes
    if: needs.changes.outputs.docs != 'true'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json
      # The Playground requires Node 26 or newer (package.json engines).
      - uses: actions/setup-node@v5
        with:
          node-version: '26'
          cache: npm
          cache-dependency-path: examples/Fable.Ranvier.Playground/package-lock.json
      - name: Restore tools
        run: dotnet tool restore
      # sourcemap.mjs in the plugin tests reads the Playground's node_modules.
      - name: Install npm packages
        run: npm ci
        working-directory: examples/Fable.Ranvier.Playground
      - name: Build the Fable plugin
        run: dotnet build experiments/Ranvier.View.Arms.FablePlugin/Ranvier.View.Arms.FablePlugin.fsproj -c Release
      - name: Run the plugin tests
        run: dotnet fsi experiments/Ranvier.View.Arms.FablePlugin/Tests/PluginTests.fsx
      # The documented opt-in (a project reference and the .targets): the same server HTML on .NET and under Fable, and
      # no plugin assembly in the .NET output.
      - name: Run the discovery app on both targets
        run: |
          dotnet run --project experiments/Ranvier.View.Arms/Checks/Discovery/Discovery.fsproj -c Release > discovery-net.txt
          dotnet fickl experiments/Ranvier.View.Arms/Checks/Discovery/Discovery.fsproj --outDir experiments/Ranvier.View.Arms/Checks/Discovery/out -c Release --noCache
          node experiments/Ranvier.View.Arms/Checks/Discovery/out/Main.js > discovery-fable.txt
          diff --strip-trailing-cr discovery-net.txt discovery-fable.txt
          grep -q armsValues_L experiments/Ranvier.View.Arms/Checks/Discovery/out/Main.js
          test ! -e experiments/Ranvier.View.Arms/Checks/Discovery/bin/Release/net10.0/Ranvier.View.Arms.FablePlugin.dll
          test ! -e experiments/Ranvier.View.Arms/Checks/Discovery/bin/Release/net10.0/Fable.AST.dll
      - name: Run the representation checks with the arms checks
        run: dotnet run --project experiments/Ranvier.View.Foundation/RepresentationChecks.fsproj -c Release
      - name: Run the arms suites
        run: npm run test:arms
        working-directory: examples/Fable.Ranvier.Playground
      - name: Check the bundle budget
        run: node experiments/Ranvier.View.Arms/Checks/size.mjs
      # size.mjs rewrites the committed bundle numbers; a difference means they are stale.
      - name: Check the committed bundle numbers
        run: git diff --exit-code -- docs/research/arms-match/bundle.json
      - name: Check the trim annotation
        run: bash experiments/Ranvier.View.Arms/Checks/TrimCallSite/check.sh linux-x64
```

and change `needs: [changes, scripts, dotnet, aot, packages, format, fable, playground, sugar, anime, motion-view, motion]` to `needs: [changes, scripts, dotnet, aot, packages, format, fable, playground, sugar, anime, motion-view, motion, arms]`.

- [ ] **Step 4: Run the phase gates, commit and check CI**

All task and phase gates (end of Phase 5), then:

```bash
dotnet fantomas experiments/Ranvier.View.Arms/Checks/TrimCallSite/Main.fs
git add experiments/Ranvier.View.Arms/Checks/TrimCallSite .github/workflows/ci.yml Ranvier.slnx
git diff --cached --check
git commit -F C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-task-11-commit.txt
git push origin HEAD:feat/arms-match
```

Message subject: `ci(arms): trim annotation check and the Arms.Match job`. End of Phase 5. When a pull request exists for the branch, `gh pr checks` shows the `Arms.Match` job green.

---

## Phase 6: docs

Start of phase: merge `origin/master`, then the task gates.

### Task 12: The `Arms.Match` docs, reference, troubleshooting and samples

**Files:**
- Modify: `docs/content/view/arms.md` (a section after "`branch` over a key"), `docs/content/view/reference.md`, `docs/content/view/troubleshooting.md`
- Modify: `docs/samples/view/Arms.fs` (regenerated), `docs/samples/view/Program.fs`, `docs/samples/view/Ranvier.Docs.ViewSamples.fsproj`
- Modify: `docs/research/2026-10-21-arms-match.md` (opening summary)
- Test: `dotnet run --project docs/samples/view/Ranvier.Docs.ViewSamples.fsproj -c Release`, `dotnet fsi docs/samples/view/Generate.fsx --check`

**Interfaces:**
- Consumes: the public API; the measured numbers of Tasks 3, 9 and 10.
- Produces: `Arms.loaderMatch` in the docs samples.

- [ ] **Step 1: Write the failing sample check**

`docs/samples/view/Program.fs`, after the `"arms loader"` expectation:

```fsharp
        expect
            "arms Arms.Match loader"
            (Server.render omit Arms.loader (createSignal (Arms.Loaded "done")))
            (Server.render omit Arms.loaderMatch (createSignal (Arms.Loaded "done")))

        expect
            "arms Arms.Match idle"
            (root """<div class="loader"><!--[a:220727aa--><span>Not started</span><!--]--></div>""")
            (Server.render omit Arms.loaderMatch (createSignal Arms.Idle))
```

`Ranvier.Docs.ViewSamples.fsproj`, beside the Foundation project reference:

```xml
    <ProjectReference Include="../../../experiments/Ranvier.View.Arms/Ranvier.View.Arms.fsproj" />
```

Run: `dotnet run --project docs/samples/view/Ranvier.Docs.ViewSamples.fsproj -c Release`
Expected: FAIL to compile, `Arms.loaderMatch` is not defined.

- [ ] **Step 2: Write the docs section**

In `docs/content/view/arms.md`, insert before `## Server HTML`:

````markdown
## `Arms.Match`: a plain match (opt-in)

`Arms.Match` takes an ordinary F# `match` over a union, with pattern variables, nested patterns, guards and
or-patterns, and builds the same arms as `armOf` and `matchWith`: the same server HTML, hashes and hydration, on .NET
and under Fable. It lives in its own project; an app opts in with two lines:

```xml
<ProjectReference Include="../../experiments/Ranvier.View.Arms/Ranvier.View.Arms.fsproj" />
<Import Project="../../experiments/Ranvier.View.Arms/Ranvier.View.Arms.targets" />
```

The `.targets` adds the Fable plugin that lowers each call. The plugin assembly must be built before `dotnet fickl`
runs: `dotnet build experiments/Ranvier.View.Arms.FablePlugin -c Release`.

```fsharp
open Ranvier.View.Arms

let loaderMatch =
    Component.template (fun _ (state: Signal<Load>) -> state) (
        div (Attrs.className "loader") {
            Arms.Match (
                (fun (state: Signal<Load>) -> state.Value),
                function
                | Idle -> span () { "Not started" }
                | Loading percent -> progress (Attrs.attribute ("max", "100"), Attrs.attribute ("value", fun _ -> Some (string percent))) { }
                | Loaded "" -> span () { "Empty" }
                | Loaded text -> pre () { fun _ -> text }
            )
        }
    )
```

**A pattern variable is read inside a reader**: a lambda over the instance, such as `fun _ -> text`. Inside it, the
variable holds the payload of the arm currently mounted, so `Loaded "a"` followed by `Loaded "b"` keeps the `<pre>`
and updates its text. Event handlers (`fun s e -> ...`) and hooks over the instance are readers too, and read the
payload at event time.

A pattern variable used anywhere else is rejected, because a case body is built once and shared by every instance.
`| Loaded text -> span () { text }` fails the Fable compile at `text`, and on .NET raises `BuildTimeReadException`
when the site is built:

```
Arms.Match: 'text' is read outside a reader of the instance. Read it inside a lambda over the instance, such as (fun _ -> text).
```

**Rows.** A `forEach` row reads its own item, not the instance, so a pattern variable inside a row reader is rejected
(AM002). Pass it through the items reader, which runs over the instance:

```fsharp
type Folder =
    | NoFiles
    | Files of owner: string * names: string array

let private folderView =
    Component.template (fun _ (folder: Signal<Folder>) -> folder) (
        ul () {
            Arms.Match (
                (fun (folder: Signal<Folder>) -> folder.Value),
                function
                | NoFiles -> li () { "No files" }
                | Files (owner, names) ->
                    forEach snd (fun _ -> names |> Array.map (fun name -> owner, name)) (li () { fun (row: unit -> string * string) -> fst (row ()) + "/" + snd (row ()) })
            )
        }
    )
```

**What remounts.** Each row of the match (each *target*) is one arm. The same target keeps its arm and replaces its
payload; another target remounts, including a guard, a literal pattern or an `if` at the top of a case body that
selects another branch. `Loaded "x"` to `Loaded ""` above remounts from the `<pre>` to the "Empty" `<span>`.

**Costs.** Under Fable the first site of a bundle adds the runtime, at most 250 B gzip, and each further site at most
60 B. On .NET each evaluation of the `Arms.Match` call lowers the quotation, a few milliseconds per site: bind sites
with `let`, as arm definitions are bound above. Trimmed and NativeAOT apps use `matchWith` with `armOf`; `Arms.Match`
carries `RequiresUnreferencedCode` and `RequiresDynamicCode`, so a trimmed publish warns at each call.

The diagnostics, AM001 to AM007, are listed in [Troubleshooting](troubleshooting.md).
````

- [ ] **Step 3: Regenerate the samples and run them**

```bash
dotnet fsi docs/samples/view/Generate.fsx
dotnet run --project docs/samples/view/Ranvier.Docs.ViewSamples.fsproj -c Release
dotnet fsi docs/samples/view/Generate.fsx --check
```

Expected: `Arms.fs` gains `open Ranvier.View.Arms`, `loaderMatch`, `Folder` and `folderView`; the samples program exits 0; `--check` exits 0. If the generated `open` after earlier declarations does not compile, move the `open Ranvier.View.Arms` line of the page's first block into the opens list that `Generate.fsx` passes for `view/arms.md` (`fableOpens` plus `"open Ranvier.View.Arms"`) and keep the line in the page.

- [ ] **Step 4: Reference and troubleshooting**

`docs/content/view/reference.md`, before `## Other projects`:

```markdown
## `experiments/Ranvier.View.Arms`

| Module | Contents | Page |
| --- | --- | --- |
| [`Ranvier.View.Arms`](https://github.com/shayanhabibi/Ranvier/blob/master/experiments/Ranvier.View.Arms/Arms.fs) | `Arms.Match`; on .NET `BuildTimeReadException` and `ArmsMatchException` (both inherit `InvalidOperationException`, which shared code catches on both targets) | [Conditional arms](arms.md) |
| [`Ranvier.View.Arms.FablePlugin`](https://github.com/shayanhabibi/Ranvier/tree/master/experiments/Ranvier.View.Arms.FablePlugin) | `CaseArmsAttribute`: the Fable plugin that lowers `Arms.Match` calls, referenced through `Ranvier.View.Arms.targets` | [Conditional arms](arms.md) |
```

`docs/content/view/troubleshooting.md`, at the end of `## Compile errors`:

```markdown
**FS0101 "This construct is deprecated. Arms.Match needs the Ranvier.View.Arms Fable plugin" (AM005) at an
`Arms.Match` call.** The project `dotnet fickl` compiles references `Ranvier.View.Arms` without importing
`Ranvier.View.Arms.targets`. Import it.

**"Ranvier.View.Arms.FablePlugin.dll is missing from ..."** when `dotnet fickl` starts: the plugin is not built in
Release. Run `dotnet build experiments/Ranvier.View.Arms.FablePlugin -c Release`, or set `RanvierArmsPluginDir` to the
folder of another build.

**FS0039 "The type 'FablePlugin' is not defined" in `Arms.fs`.** The project defines `RANVIER_ARMS_PLUGIN` without
importing the `.targets`, so the plugin assembly is missing from the compilation. Import the `.targets` instead of
defining the symbol.

**AM001 "Arms.Match: 'x' is read outside a reader of the instance"** (Fable compile; `BuildTimeReadException` on .NET).
A pattern variable is used as a build-time value, for example as static text or as the argument of a helper that takes
a value. Read it inside a lambda over the instance: `fun _ -> x`, or pass a reader to the helper.

**AM001b "Arms.Match: the field 'x' of 'Case' is read outside a reader of the instance".** The same rule, where the
compiler replaced the pattern variable by a read of the case field. Read it inside `fun _ -> x`.

**AM002 "Arms.Match: 'x' is read inside a lambda over 'T', which is not the instance type".** A pattern variable is
read inside a row reader or another lambda over a type other than the instance. Pass the value through the row's
items, `forEach key (fun _ -> items |> Array.map (fun item -> x, item)) row`, or through props.

**AM003 "Arms.Match takes a function literal as its second argument".** The arms are a function value. Write the
`function | Case x -> ...` at the call.

**AM004 "Arms.Match: the instance type 'T' is not an object type".** The instance is a value type, `string`, `unit` or
an option, which the site cannot key payloads by. Wrap it in a record or pass a signal.

**AM007 "Arms.Match: this pattern form is not supported."** An `if`, `match` or `let` over a pattern variable sits under
a sequence, a type cast or a `try` at the top of a case body. Move the condition into the match, as a case or a guard.
```

and at the end of `## Runtime errors`:

```markdown
Shared code that catches an `Arms.Match` rejection catches `InvalidOperationException`: `BuildTimeReadException` and
`ArmsMatchException` exist on .NET only, and Fable raises `InvalidOperationException` with the same texts.

**AM005 "Arms.Match needs the Ranvier.View.Arms Fable plugin"** when a site is built: the plugin assembly is
referenced but did not run, for example under a Fable version below its `FableMinimumVersion`. Use Fable 5.

**AM006 "Arms.Match: no arm of this site is mounted for the instance this reader received"**: a lambda over the
instance type inside a case body received another value of that type, as a row whose item type equals the instance
type does. Give the row a type of its own.
```

- [ ] **Step 5: Open the research note with its summary**

Insert after the first paragraph of `docs/research/2026-10-21-arms-match.md`:

```markdown
**Summary.** The plugin is found through a project reference and the `.targets`; the .NET lowering costs <first> ms
for the first five-target site and <mean> ms on average; the first `Arms.Match` site costs <one> B gzip and each
further site <perSite> B; the Chromium arm operations and the retained heap per row are within their gates against
`ranvier-sugar` (tables below).
```

with the numbers of the sections below it (run 1 of the lowering table).

- [ ] **Step 6: Run the phase gates, format and commit**

All task and phase gates (end of Phase 6), then:

```bash
git add docs/content/view/arms.md docs/content/view/reference.md docs/content/view/troubleshooting.md docs/samples/view docs/research/2026-10-21-arms-match.md
git diff --cached --check
git commit -F C:/Users/shaya/.claude/jobs/7fee9ca9/tmp/arms-task-12-commit.txt
git push origin HEAD:feat/arms-match
```

`docs/samples/` is in `.fantomasignore` and keeps its page layout. Message subject: `docs(view): Arms.Match, its diagnostics and samples`. End of Phase 6.
