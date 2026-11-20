# Science Slice, Plan 1: Library, Benchmark and Probes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A tested F# library for scientific quantities on the Ranvier graph (float policy, tolerances, units, quantities, restartable runs, a confidence sequence), a measured node cost, and the probe results the two later plans depend on.

**Architecture:** A new local repository, `C:\wk\ranvier-release\Ranvier.Science`, holds three library projects built on the public API of the published core package, one test suite that runs on .NET and, compiled by Fickl, under Node, and one benchmark project. Nothing under `Ranvier/src` or `AstroShowcase` changes. Every workaround of the core is recorded in `docs/PROBES.md` for the findings report.

**Tech Stack:** F# on .NET 10 (SDK pinned as in `Ranvier/global.json`); `Ranvier` 0.2.0 from nuget.org; Expecto 11.0.0-alpha8; Fickl 0.10.1 with Fable.Core 5.3.0 and Fable.Mocha 2.17.0; mocha 12; BenchmarkDotNet 0.15.8.

**Spec:** `C:\wk\ranvier-release\Ranvier\docs\superpowers\specs\2026-11-19-science-vertical-slice-design.md` (approved 2026-11-19). Section numbers below refer to it.

**Scope.** The spec has three parts that each produce working software. This plan is the first. The other two are written after Task 3's probes report, because the probes decide their details.

| Plan | Covers (spec sections) | Waits on |
|---|---|---|
| **1, this plan** | §6 node cost; §7.3 statistics; §7.4 float policy and tolerance; §7.5 units, quantities, models; §8 `Run`; §9 steps 1 to 5 | nothing |
| 2, sequential monitor page | §7.1, §7.2, §7.5 formula notation, §7.6; §9 steps 6 and 7. Its first task is the probe "an empty View page on the current packages through Fickl" | probes B, C, D of Task 3 |
| 3, AstroTracked ports and findings | §8 pages, §10; §9 steps 8 and 9 | probe A; `Run` from Task 11 |

## Global Constraints

- No file under `C:\wk\ranvier-release\Ranvier\src` changes. `C:\wk\ranvier-release\AstroShowcase` is not edited.
- Create and edit files only under `C:\wk\ranvier-release\Ranvier.Science`. Read anything in the workspace.
- A node holds a quantity, an array or a block: never an arithmetic operation or an array element.
- Library and shared test code is Fable-compatible F#: no reflection, no `System.Random`, no thread APIs outside `#if !FABLE_COMPILER`. Shared test files use only what the Expecto shim provides (`testList`, `test`, `testCase`, `testCaseAsync`, `Expect.*`); no FsCheck there.
- The package keeps no dependency tracker, cache or scheduler of its own beside the core's.
- Every figure written to a file carries a label: *measured here* (with the command and what was timed), *from documentation* (with the file), or *general knowledge*. Two figures are compared only when both come from one run.
- Commits are local. No remote is added, nothing is pushed, tagged or published. Each commit message ends with the session's attribution line.
- Shell: write multi-line scripts to a file and run the file; use absolute paths; run commands plainly.
- F# inner loop: SageFs when it is available, otherwise `rfs check --fail-on-error`. Each task ends with one clean `dotnet build -c Release` and one unfiltered run of both test suites.
- Doc comments follow `C:\wk\ranvier-release\Ranvier\.claude\rules\comments.md`. Prose uses British spelling.
- No window is opened and no app is launched in this plan.

## Review Focus

1. **A non-finite observation** (`NaN`, `±∞`) reaches a sum or moments. Expected: the state shows `NaN`, nothing raises, and a maintained aggregate equals the from-scratch one again once the row is removed. Test in Task 6.
2. **An empty or one-item sample** reaches the statistics, or `alpha` or `rho` is out of range. Expected: `NaN` bounds for count 0, a zero-width interval for count 1, `ArgumentOutOfRangeException` for a bad `alpha` or `rho`. Tests in Tasks 5 and 13.
3. **A removal from an empty aggregate.** Expected: `InvalidOperationException` from `Moments.remove` and `Moments.unmerge`; a maintained aggregate whose last row is removed reads zero. Tests in Tasks 5 and 6.
4. **A formula body that raises, or a malformed or duplicate identifier.** Expected: the quantity reports `Failed` and re-raises on read, other quantities are unaffected; the declaration raises `ArgumentException`. Tests in Task 10.
5. **A clock that stands still, is `NaN`, or an `advance` that raises.** Expected: no advance and no restart for an equal or `NaN` clock; a raising `advance` fails the read and the next restart recovers. Tests in Task 11.

---

## File Structure

```
Ranvier.Science/
  .config/dotnet-tools.json          fickl 0.10.1
  global.json  nuget.config  Directory.Build.props  Ranvier.Science.slnx  package.json  .gitignore  README.md
  src/Ranvier.Numerics/              Sum.fs  Moments.fs  FloatPolicy.fs  Aggregate.fs  Tolerance.fs  Generator.fs
  src/Ranvier.Quantities/            Units.fs  Model.fs  Quantity.fs  Run.fs
  src/Ranvier.Statistics/            NormalQuantile.fs  ConfidenceSequence.fs
  tests/Ranvier.Science.Tests/       TestFiles.props  Support.fs  <one file per task>  Main.fs
  tests/Ranvier.Science.Tests.Fable/ Expecto.fs  Main.fs
  bench/Ranvier.Science.Benchmarks/  Bodies.fs  KitMemo.fs  NodeCost.fs  Aggregates.fs  Program.fs
  docs/PROBES.md  docs/measurements/node-cost.md  docs/measurements/aggregates.md
```

`Ranvier.Numerics` references `Ranvier` 0.2.0. `Ranvier.Quantities` references `Ranvier.Numerics`. `Ranvier.Statistics` references `Ranvier.Numerics`. All three target `net10.0`.

Commands used throughout, run from `C:\wk\ranvier-release\Ranvier.Science`:

- **.NET suite:** `dotnet run -c Release --project tests/Ranvier.Science.Tests` (add `-- --filter-test-list "<name>"` for one list)
- **JavaScript suite:** `dotnet fickl tests/Ranvier.Science.Tests.Fable -e .fs.js -o dist/tests -c Release` then `npx mocha dist/tests/Main.fs.js`

---

### Task 1: Repository and a test suite that runs on both targets

**Files:**
- Create: the root files of the File Structure; the three library projects, each with one placeholder module; `tests/Ranvier.Science.Tests/{Ranvier.Science.Tests.fsproj, TestFiles.props, Support.fs, Smoke.fs, Main.fs}`; `tests/Ranvier.Science.Tests.Fable/{Ranvier.Science.Tests.Fable.fsproj, Expecto.fs, Main.fs}`; `docs/PROBES.md`

**Interfaces:**
- Produces: `Ranvier.Science.Tests.Support.withGraph : (unit -> unit) -> unit`, which runs a body with a fresh active graph (`use g = new Graph ()`, `use _ = g.Activate ()`), and `ulps : float -> float -> float`, the distance between two doubles in units in the last place of the larger magnitude. Later tasks add their test file to `TestFiles.props` and their root to both `Main.fs` files.

- [ ] **Step 1: Scaffold.** `git init` in the new folder. Copy `global.json` and the `fickl` entry of `.config/dotnet-tools.json` from `C:\wk\ranvier-release\Ranvier`, and `nuget.config` from `C:\wk\ranvier-release\AstroShowcase`. `Directory.Build.props` sets `LangVersion` to `latest` and `IsPackable` to `false`. Model the two test projects on `Ranvier/tests/Ranvier.Tests` and `Ranvier/fable/Ranvier.Tests.Fable`: the same package versions, a shared `TestFiles.props`, and a copy of that folder's `Expecto.fs` shim. `package.json` has `mocha` `^12.0.3` as its one dev dependency. `.gitignore` covers `bin/`, `obj/`, `dist/`, `node_modules/`, `BenchmarkDotNet.Artifacts/`.

- [ ] **Step 2: Write the failing test** in `Smoke.fs`:

```fsharp
module Ranvier.Science.Tests.Smoke

open Expecto
open Ranvier

[<Tests>]
let tests =
    testList "Smoke" [
        test "a memo follows its signal" {
            withGraph (fun () ->
                let a = createSignal 2.0
                let double = createMemo (fun _ -> a.Value * 2.0)
                Expect.equal double.Value 4.0 "first read"
                a.Value <- 5.0
                Expect.equal double.Value 10.0 "after a write")
        }
    ]
```

- [ ] **Step 3: Run the .NET suite.** Expected: 1 test passed.
- [ ] **Step 4: Run the JavaScript suite.** Expected: 1 passing. If Fickl cannot compile the `Ranvier` 0.2.0 package reference, give the Fable test project a `ProjectReference` to `..\..\..\Ranvier\src\Ranvier\Ranvier.fsproj` in place of the package, and record that under "Core package through Fickl" in `docs/PROBES.md`.
- [ ] **Step 5: Start `docs/PROBES.md`** with one heading per probe (Core package through Fickl, A, B, C, D, E) and, under the first, what Step 4 found, with the command.
- [ ] **Step 6: Commit** `chore: repository, libraries and a two-target test suite`.

### Task 2: Node-cost benchmark (spec §6)

**Files:**
- Create: `bench/Ranvier.Science.Benchmarks/{Ranvier.Science.Benchmarks.fsproj, Bodies.fs, KitMemo.fs, NodeCost.fs, Program.fs}`, `docs/measurements/node-cost.md`

**Interfaces:**
- Produces: `Bodies.rate`, `Bodies.kepler`, `Bodies.rigidStep`, `Bodies.decay`, each `float -> float` (one input varied, the rest fixed at the page's defaults; array results reduced to their first element), reused by Plan 3.

- [ ] **Step 1: Copy the four bodies** verbatim into `Bodies.fs`, each with a comment naming its source: the decay rate (`AstroShowcase/PagesOrbit.fs:584-590`, with `bands`), `keplerToState` (`Astro.fs:37`), one `rk4` step of the rigid-body derivative (`Astro.fs:73`, `PagesFlight.fs:179-195`), and `decay` (`PagesOrbit.fs:593-604`). Copy `Kit.memo` (`Kit.fs:73-82`) into `KitMemo.fs`.
- [ ] **Step 2: Write the check mode.** `Program.fs` with `--check` calls each body five ways (direct; `KitMemo` with an unchanged key; `KitMemo` with a changed key; a `createMemo` node read while cached; a `createMemo` node whose input signal is written before the read) on ten inputs, and exits 1 unless all five results are bitwise equal for every body and input.
- [ ] **Step 3: Run** `dotnet run -c Release --project bench/Ranvier.Science.Benchmarks -- --check`. Expected: exit 0 and the line `check: 4 bodies x 10 inputs x 5 paths equal`.
- [ ] **Step 4: Write `NodeCost.fs`**: one BenchmarkDotNet class with `[<MemoryDiagnoser>]`, a `[<Params>]` over the four bodies and the five call paths of Step 2 as benchmark methods, `Direct` as the baseline.
- [ ] **Step 5: Run** `dotnet run -c Release --project bench/Ranvier.Science.Benchmarks -- --filter *NodeCost*`. Expected: a summary table of 20 rows.
- [ ] **Step 6: Write `docs/measurements/node-cost.md`**: the machine and runtime lines of the summary, the command, the 20-row table (mean, allocated, ratio to `Direct`), and one sentence naming the cheapest body whose write-then-read node costs under 10% more than its direct call, or saying that none of the four does. Label it *measured here*.
- [ ] **Step 7: Commit** `bench: node cost around four AstroShowcase formula bodies`.

### Task 3: Probes

Each probe is a test that states the hoped-for behaviour. When one fails, record what happened in `docs/PROBES.md`, change the test to assert the observed behaviour, and say there which later interface it affects.

**Files:**
- Create: `tests/Ranvier.Science.Tests/Probes.fs`
- Modify: `docs/PROBES.md`

**Interfaces:**
- Produces: `docs/PROBES.md`, read by Tasks 9 and 10 and by Plans 2 and 3. Probe B (an empty View page through Fickl) is the first task of Plan 2 and is listed in the file as not yet run.

- [ ] **Step 1: Write the probe tests** in one list, `"Probes"`:

| Probe | Test name | Asserts |
|---|---|---|
| A | `units of measure compile and erase` | With `[<Measure>] type m` and `[<Measure>] type s`: `(6.0<m> / 2.0<s>) = 3.0<m/s>`; `float 3.0<m/s> = 3.0`; `LanguagePrimitives.FloatWithMeasure<m> 2.0 = 2.0<m>`; a generic `let twice (x: float<'u>) = x + x` gives `4.0<m>` for `2.0<m>` |
| C | `a stream fold can be created in a projection row factory` | A `createProjectionWith` whose factory calls `createStreamFold 0 (+) start` and returns a reader of it; pushing 1, 2, 3 into the row's stream makes the row read 6; removing the key cancels the stream's token |
| D | `a memo reading a pending stream is pending` | `createMemo (fun _ -> stream.Value + 1)` over a stream with no item: `memo.TryValue` reports pending and does not raise; after one item 4 it reads 5 |
| E1 | `a fold that returns its previous state notifies no reader` | A fold `fun s x -> if x = 0 then s else { s with Total = s.Total + x }` over a record state; an effect reading the stream runs once more per non-zero item and not for a zero item |
| E2 | `a fold that returns a new equal struct` | The same with a struct state rebuilt on every item. Assert `#if FABLE_COMPILER` that a zero item does re-run the effect, `#else` that it does not (spec §7.4; `Ranvier/src/Ranvier/Types.fs:279-307`) |

Drive the streams with a manual source as `Ranvier/tests/Ranvier.Tests/Streams.fs:9-49` does; use its `settled`/`drained` pattern where a read follows a completion.

- [ ] **Step 2: Run both suites.** Expected: the list runs on both targets. A failure is a result, handled as the task's first paragraph says.
- [ ] **Step 3: Fill `docs/PROBES.md`**: per probe, the outcome on each target, the command, and the consequence. For A failing under Fickl only, the consequence is the fallback of Task 9. For C failing, Plan 2 gives each channel a `createRoot`. For D, record the member that reports a memo's status without raising.
- [ ] **Step 4: Commit** `test: probes of units, row-owned streams, pending reads and fold equality`.

### Task 4: `Sum`, a compensated sum that accepts removals

**Files:**
- Create: `src/Ranvier.Numerics/Sum.fs`, `tests/Ranvier.Science.Tests/SumTests.fs`
- Modify: `tests/Ranvier.Science.Tests/Support.fs` (add `exactSum`)

**Interfaces:**
- Produces:

```fsharp
namespace Ranvier.Numerics

/// A running sum with its rounding error carried beside it.
[<Struct>]
type Sum = { Total: float; Carry: float }

module Sum =
    val zero : Sum
    /// Neumaier's compensated addition when `compensated`; plain addition, with a zero carry, otherwise.
    val add : compensated: bool -> Sum -> float -> Sum
    /// `add` of the negated value.
    val remove : compensated: bool -> Sum -> float -> Sum
    /// `Total + Carry`.
    val value : Sum -> float
```

- Test support: `exactSum : float[] -> float`, the correctly rounded sum by Shewchuk's partials (the algorithm of Python's `math.fsum`).

- [ ] **Step 1: Write the failing tests** (`"Sum"`):

```fsharp
let private total compensated (xs: float[]) =
    xs |> Array.fold (Sum.add compensated) Sum.zero |> Sum.value

test "1e20, 1, -1e20 sums to 1 compensated and to 0 plain" {
    let xs = [| 1e20; 1.0; -1e20 |]
    Expect.equal (total true xs) 1.0 "compensated"
    Expect.equal (total false xs) 0.0 "plain"
}
test "1, 1e100, 1, -1e100 sums to 2" { Expect.equal (total true [| 1.0; 1e100; 1.0; -1e100 |]) 2.0 "" }
test "ten thousand tenths are within 2 ulp of the exact sum" {
    let xs = Array.create 10_000 0.1
    Expect.isLessThanOrEqual (ulps (total true xs) (exactSum xs)) 2.0 ""
}
test "alternating 1e9 with small terms is within 2 ulp of the exact sum" {
    let xs = Array.init 10_000 (fun i -> (if i % 2 = 0 then 1e9 else -1e9) + float i * 1e-3)
    Expect.isLessThanOrEqual (ulps (total true xs) (exactSum xs)) 2.0 ""
}
test "removing every other term leaves the exact sum of the rest within 2 ulp" {
    let xs = Array.init 10_000 (fun i -> 1.0 + float i * 1e-3)
    let all = xs |> Array.fold (Sum.add true) Sum.zero
    let rest = xs |> Array.indexed |> Array.filter (fun (i, _) -> i % 2 = 1) |> Array.map snd
    let left = xs |> Array.indexed |> Array.fold (fun s (i, x) -> if i % 2 = 0 then Sum.remove true s x else s) all
    Expect.isLessThanOrEqual (ulps (Sum.value left) (exactSum rest)) 2.0 ""
}
test "a NaN term gives NaN and raises nothing" { Expect.isTrue (System.Double.IsNaN (total true [| 1.0; nan; 2.0 |])) "" }
```

- [ ] **Step 2: Run** the `Sum` list on .NET. Expected: FAIL, `Sum` is not defined.
- [ ] **Step 3: Implement** `Sum.fs` and `exactSum`.
- [ ] **Step 4: Run both suites.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(numerics): compensated sum with removal`.

### Task 5: `Moments`

**Files:**
- Create: `src/Ranvier.Numerics/Moments.fs`, `tests/Ranvier.Science.Tests/MomentsTests.fs`

**Interfaces:**
- Consumes: `Sum` (Task 4).
- Produces:

```fsharp
/// Count, mean and spread of a set of observations. Immutable.
[<Struct; NoComparison>]
type Moments   // representation private

module Moments =
    val zero : Moments
    val add : compensated: bool -> Moments -> float -> Moments
    /// Raises InvalidOperationException when the count is zero. Returns `zero` when the count reaches zero.
    val remove : compensated: bool -> Moments -> float -> Moments
    val merge : compensated: bool -> Moments -> Moments -> Moments
    /// `unmerge whole part` is the moments of the observations of `whole` outside `part`.
    /// Raises InvalidOperationException when `part` has more observations than `whole`; returns `zero` when the counts are equal.
    val unmerge : compensated: bool -> whole: Moments -> part: Moments -> Moments
    /// A whole number, exact up to 2^53 (a float so the count has one representation on both targets).
    val count : Moments -> float
    /// NaN when the count is zero.
    val mean : Moments -> float
    /// The population variance, (1/n)·Σy² − mean², never negative; NaN when the count is zero.
    val variance : Moments -> float
    /// sqrt (variance / count).
    val standardError : Moments -> float
```

- [ ] **Step 1: Write the failing tests** (`"Moments"`), with `ofArray c xs = Array.fold (Moments.add c) Moments.zero xs` and `rel a b = abs (a - b) / abs b`:

| Test name | Asserts |
|---|---|
| `0 to 999 has mean 499.5 and variance 83333.25` | both `Expect.equal`, compensated |
| `1e9 + k keeps the variance` | for `xs = Array.init 1000 (fun k -> 1e9 + float k)`: `rel (variance m) 83333.25 <= 1e-9`; `mean m = 1e9 + 499.5` |
| `folding twice gives the same bits` | two folds of one 5,000-item array give `=` states |
| `merge of two halves matches one fold` | `rel` of mean `<= 1e-12`, of variance `<= 1e-9`, counts equal |
| `unmerge undoes merge` | `unmerge (merge a b) b` against `a`: same tolerances |
| `remove undoes add` | add 1,000 values, remove the last 500 in reverse order: against the fold of the first 500, same tolerances |
| `removing everything returns zero` | after removing all items the state `= Moments.zero` |
| `remove from empty raises` | `Expect.throwsT<System.InvalidOperationException>` for `remove` on `zero` and for `unmerge zero (ofArray true [| 1.0 |])` |
| `an empty sample has NaN mean and variance` | both `Double.IsNaN` |
| `one observation has zero variance` | `variance = 0.0`, `standardError = 0.0`, `mean = 3.5` for `[| 3.5 |]` |
| `the count stays exact past 2^31` | merging a one-item state with itself 32 times gives `count = 4294967296.0` |
| `a NaN observation gives NaN and raises nothing` | mean is NaN after adding `nan` |

- [ ] **Step 2: Run** the list on .NET. Expected: FAIL, `Moments` is not defined.
- [ ] **Step 3: Implement** `Moments.fs`. A Welford-style update (count, mean, sum of squared deviations) with a `Sum` for each running total meets the tests; the representation is the implementer's choice.
- [ ] **Step 4: Run both suites.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(numerics): moments with add, remove, merge and unmerge`.

### Task 6: Float policy and maintained aggregates (spec §7.4)

**Files:**
- Create: `src/Ranvier.Numerics/FloatPolicy.fs`, `src/Ranvier.Numerics/Aggregate.fs`, `tests/Ranvier.Science.Tests/AggregateTests.fs`

**Interfaces:**
- Consumes: `Sum`, `Moments`; `Projection.foldGroupBy` and `Projection.fold` (`Ranvier/src/Ranvier/Combinators.fs:1843`, `1807`).
- Produces:

```fsharp
/// How a floating-point aggregate accumulates, and when it re-folds from its cached inputs.
type FloatPolicy =
    { /// Carry rounding error beside every running total.
      Compensated: bool
      /// Re-fold once removals since the last re-fold reach the number of rows.
      RefoldAfterRemovals: bool }

module FloatPolicy =
    val standard : FloatPolicy   // { Compensated = true;  RefoldAfterRemovals = true }
    val fast : FloatPolicy       // { Compensated = false; RefoldAfterRemovals = false }

/// An aggregate kept current per row change.
type Maintained<'T> =
    { Value: Memo<'T>
      /// Re-folds since creation. Read it after `Value`.
      Refolds: unit -> int }

module Aggregate =
    val sumBy : policy: FloatPolicy -> projection: ('V -> float) -> upstream: Projection<'K, 'V> -> Maintained<float>
    val momentsBy : policy: FloatPolicy -> projection: ('V -> Moments) -> upstream: Projection<'K, 'V> -> Maintained<Moments>
    /// The same value folded from every row on each change.
    val momentsFromScratchBy : policy: FloatPolicy -> projection: ('V -> Moments) -> upstream: Projection<'K, 'V> -> Memo<Moments>
```

Under either policy an aggregate re-folds when its state is not finite. The re-fold is requested by raising from `subtract`: `ProjectionFold.Step` treats any exception from a step as a request for a full re-fold (`Combinators.fs:1058-1064`). Record this use under "Workarounds" in `docs/PROBES.md`.

- [ ] **Step 1: Write the failing tests** (`"Aggregate"`). Rows are a `createKeyedCollection` of `struct (int * float)` keyed by the first element, or of `struct (int * Moments)`. Every test reads the aggregate's value after each edit, so each edit is one subtract and one add.

| Test name | Asserts |
|---|---|
| `1e20 then 1, then 1e20 removed` | `sumBy standard`: value `1.0`; `sumBy fast`: value `0.0` |
| `a row edit makes one subtract and one add` | with a counting `projection` over 1,000 rows, one `AddOrUpdate` of an existing key calls `projection` once and `Refolds ()` stays 0 |
| `standard re-folds after as many removals as rows` | 10 rows; 9 edits: `Refolds () = 0`; the 10th: `Refolds () = 1` |
| `fast never re-folds on finite values` | 10 rows, 1,000 edits: `Refolds () = 0` |
| `after a re-fold the maintained sum has the from-scratch bits` | on the edit that makes `Refolds ()` rise, `Value.Value` `=` the plain `Array.fold (Sum.add true)` over the current values in key order |
| `between re-folds the sum stays within 8 ulp` | 1,000 rows of values in `[1, 1e6]`; 1,000,000 edits on .NET (100,000 under `FABLE_COMPILER`), each replacing one row's value from a fixed arithmetic sequence and followed by a read; after every 1,000th edit `ulps maintained exact <= 8.0`, where `exact` is `exactSum` of the current values. The test prints the largest distance seen |
| `maintained moments follow from-scratch moments` | 100 rows of `Moments` (each of 50 values); 10,000 edits; after each 100th: mean within relative `1e-12`, variance within relative `1e-9` of `momentsFromScratchBy` |
| `after a re-fold the maintained moments have the from-scratch bits` | as the sum test, against `momentsFromScratchBy`'s value |
| `a NaN row poisons the sum until it is removed` | add a `nan` row: value is NaN; remove it: value `=` the from-scratch sum |
| `removing the last row returns zero` | value `0.0` and `Moments.zero` |
| `.NET: moments folded from a second thread count every item` | under `#if !FABLE_COMPILER`, with a manual-dispatcher graph and an off-thread push as in `Ranvier/tests/Ranvier.Tests/Streams.fs:51-63`: `createStreamFold Moments.zero (Moments.add true) start` fed 10,000 items from another thread has `count = 10000.0` and a mean `=` the single-thread fold's (spec §7.2) |

If the two "from-scratch bits" tests fail because the core re-folds in another order than key order, compare against a fold in the order the core uses, and record the order in `docs/PROBES.md`.

- [ ] **Step 2: Run** the list on .NET. Expected: FAIL, `Aggregate` is not defined.
- [ ] **Step 3: Implement** `FloatPolicy.fs` and `Aggregate.fs`. The fold state is a struct of the accumulator, the row count and the removals since the last re-fold; `Refolds` reads a counter the closure owns.
- [ ] **Step 4: Run both suites.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(numerics): float policy and maintained sum and moments`.

### Task 7: Aggregate benchmark and the cost of adding a key

**Files:**
- Create: `bench/Ranvier.Science.Benchmarks/Aggregates.fs`, `docs/measurements/aggregates.md`
- Modify: `bench/Ranvier.Science.Benchmarks/Program.fs`

**Interfaces:**
- Consumes: `Aggregate.sumBy`, `Aggregate.momentsBy` (Task 6).

- [ ] **Step 1: Write `Aggregates.fs`**, one class with `[<MemoryDiagnoser>]` over a keyed collection of 100,000 rows:

| Benchmark | One operation is |
|---|---|
| `SumStandard_Revise1Percent`, `SumFast_Revise1Percent` | replace 1,000 rows' values in one `batch`, then read the sum |
| `CoreSumBy_Revise1Percent` (baseline) | the same through `Projection.sumBy` |
| `MomentsStandard_Revise1Percent` | the same through `momentsBy standard` |
| `SumStandard_AddOneKey`, `CoreSumBy_AddOneKey` | add one new key, read the sum, remove it, read the sum |
| `FromScratch_Revise1Percent` | the same revision read through `Projection.fold (+) 0.0` |

- [ ] **Step 2: Add a drift mode.** `Program.fs` with `--drift` runs 1,000 revision passes under each policy and prints, per policy, the largest distance in ulps between the maintained sum and `exactSum`, and the number of re-folds.
- [ ] **Step 3: Run** `dotnet run -c Release --project bench/Ranvier.Science.Benchmarks -- --filter *Aggregates*` and then `-- --drift`. Expected: seven benchmark rows; two drift lines.
- [ ] **Step 4: Write `docs/measurements/aggregates.md`**: both outputs, labelled *measured here*, and two sentences: what the standard policy costs against the fast one in this run, and what adding one key to 100,000 rows cost, set beside the two descriptions it settles (`Ranvier/docs/content/guide/aggregates.fsx:71` says an O(N) key diff; `Combinators.fs:1774` says O(Δ)).
- [ ] **Step 5: Commit** `bench: maintained aggregates under both float policies`.

### Task 8: `Tolerance`

**Files:**
- Create: `src/Ranvier.Numerics/Tolerance.fs`, `tests/Ranvier.Science.Tests/ToleranceTests.fs`

**Interfaces:**
- Produces:

```fsharp
/// How far a value may move from the last published one and still count as unchanged.
type Tolerance = { Absolute: float; Relative: float }

module Tolerance =
    val absolute : float -> Tolerance
    val relative : float -> Tolerance
    /// |next − previous| ≤ max Absolute (Relative · |previous|). Two NaNs are within; equal infinities are within.
    val within : Tolerance -> previous: float -> next: float -> bool
    /// `previous` when it exists and `next` is within the tolerance of it; `next` otherwise.
    val hold : Tolerance -> previous: float voption -> next: float -> float
    /// The `previous` array itself when it has the length of `next` and every element is within; `next` otherwise.
    val holdSeries : Tolerance -> previous: float[] voption -> next: float[] -> float[]
```

Raises `ArgumentOutOfRangeException` for a negative or NaN bound.

- [ ] **Step 1: Write the failing tests** (`"Tolerance"`):

| Test name | Asserts |
|---|---|
| `within is measured from the previous value` | `absolute 1.0`: `within 0.0 1.0` true, `within 0.0 1.0000001` false; `relative 0.01`: `within 100.0 101.0` true, `within 100.0 101.1` false |
| `NaN and infinity` | `within nan nan`, `within infinity infinity` true; `within 1.0 nan`, `within infinity -infinity` false |
| `small steps cannot accumulate unseen` | `a = createSignal 0.0`; `m = createMemo (fun prev -> Tolerance.hold (Tolerance.absolute 1.0) prev a.Value)`; an effect records `m.Value`; writes `0.4, 0.8, 1.2, 1.6, 2.0, 2.4` record exactly `[0.0; 1.2; 2.4]` |
| `holdSeries returns the same array instance` | `obj.ReferenceEquals (holdSeries t (ValueSome a) b, a)` when every element is within; `b` when one is not or the lengths differ |
| `a negative bound is rejected` | `Expect.throwsT<System.ArgumentOutOfRangeException>` for `absolute -1.0` and `relative nan` |

- [ ] **Step 2: Run** on .NET. Expected: FAIL, `Tolerance` is not defined.
- [ ] **Step 3: Implement** `Tolerance.fs`.
- [ ] **Step 4: Run both suites.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(numerics): tolerance held against the last published value`.

### Task 9: Units (spec §7.5)

**Files:**
- Create: `src/Ranvier.Quantities/Units.fs`, `tests/Ranvier.Science.Tests/UnitsTests.fs`

**Interfaces:**
- Consumes: probe A's outcome in `docs/PROBES.md`.
- Produces:

```fsharp
namespace Ranvier.Quantities

/// Integer exponents over the seven SI base dimensions.
[<Struct>]
type Dimension =
    { Length: int; Mass: int; Time: int; Current: int; Temperature: int; Amount: int; Luminosity: int }

/// A unit at run time.
type UnitInfo =
    { /// Named factors with their exponents, in the order written: [ "km", 1; "s", -1 ].
      Factors: (string * int) list
      Dimension: Dimension
      /// The size of one of this unit in SI base units.
      Scale: float }

module Dimension =
    val none : Dimension
    val length : Dimension    // and mass, time, current, temperature, amount, luminosity

module UnitInfo =
    val dimensionless : UnitInfo
    val multiply : UnitInfo -> UnitInfo -> UnitInfo
    val divide : UnitInfo -> UnitInfo -> UnitInfo
    val power : UnitInfo -> int -> UnitInfo
    /// Error names the unit when an exponent is odd.
    val sqrt : UnitInfo -> Result<UnitInfo, string>
    val sameDimension : UnitInfo -> UnitInfo -> bool
    /// Same dimension and same scale.
    val sameUnit : UnitInfo -> UnitInfo -> bool
    /// "km/s", "mK^2", "kg·m/s^2"; "" for a dimensionless unit.
    val symbol : UnitInfo -> string

/// A unit of measure with the description its values carry at run time.
[<Sealed>]
type UnitOf<[<Measure>] 'u> =
    member Info : UnitInfo
    static member ( * ) : UnitOf<'a> * UnitOf<'b> -> UnitOf<'a 'b>
    static member ( / ) : UnitOf<'a> * UnitOf<'b> -> UnitOf<'a / 'b>

module UnitOf =
    /// The caller's annotation fixes 'u. Raises ArgumentOutOfRangeException unless scale is positive and finite.
    val define : symbol: string -> dimension: Dimension -> scale: float -> UnitOf<'u>
    val squared : UnitOf<'u> -> UnitOf<'u ^ 2>

[<Measure>] type m
[<Measure>] type km
[<Measure>] type kg
[<Measure>] type s
[<Measure>] type K
[<Measure>] type mK

module Units =
    val one : UnitOf<1>
    val metre : UnitOf<m>
    val kilometre : UnitOf<km>      // scale 1000
    val kilogram : UnitOf<kg>
    val second : UnitOf<s>
    val kelvin : UnitOf<K>
    val millikelvin : UnitOf<mK>    // scale 0.001
```

**If probe A failed under Fickl:** drop `[<Measure>]` from `'u` everywhere in this task and Task 10, make the six unit types plain marker types, have `( * )`, `( / )` and `squared` return `UnitOf<'c>` with `'c` fixed by the caller's annotation, and use `float` where Task 10 says `float<'u>`. Tests and run-time behaviour are unchanged. Record the change in `docs/PROBES.md`.

- [ ] **Step 1: Write the failing tests** (`"Units"`):

| Test name | Asserts |
|---|---|
| `kilometres per second` | `(Units.kilometre / Units.second).Info`: `symbol = "km/s"`, `Dimension = { Dimension.none with Length = 1; Time = -1 }`, `Scale = 1000.0` |
| `millikelvin squared and back` | `(UnitOf.squared Units.millikelvin).Info`: `symbol = "mK^2"`, `Temperature = 2`, `Scale` within relative `1e-15` of `1e-6`; `UnitInfo.sqrt` of it is `Ok u` with `symbol u = "mK"` and `sameUnit u Units.millikelvin.Info` |
| `the square root of an odd power is an error` | `UnitInfo.sqrt Units.metre.Info` is `Error` and the message contains `"m"` |
| `metres and kilometres share a dimension and not a unit` | `sameDimension` true, `sameUnit` false |
| `a product cancels to dimensionless` | `(Units.second * (Units.one / Units.second)).Info`: `Dimension = Dimension.none`, `symbol = ""` |
| `a unit needs a positive finite scale` | `Expect.throwsT<System.ArgumentOutOfRangeException>` for scales `0.0`, `-1.0`, `nan`, `infinity` |

- [ ] **Step 2: Run** on .NET. Expected: FAIL, `Units` is not defined.
- [ ] **Step 3: Implement** `Units.fs`.
- [ ] **Step 4: Run both suites.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(quantities): units with a run-time description`.

### Task 10: Quantities and models (spec §7.5)

**Files:**
- Create: `src/Ranvier.Quantities/Model.fs`, `src/Ranvier.Quantities/Quantity.fs`, `tests/Ranvier.Science.Tests/QuantityTests.fs`

**Interfaces:**
- Consumes: `UnitOf`, `UnitInfo` (Task 9); `Tolerance.hold` (Task 8); probe D's record of how a memo reports its status.
- Produces:

```fsharp
type QuantityKind = Input | Constant | Derived

type QuantityStatus =
    | Ready
    | Pending
    | Failed of exn

/// What a model holds for one quantity.
type Entry =
    { Id: string
      Name: string
      Unit: UnitInfo
      Kind: QuantityKind
      /// The reference a constant was taken from; "" otherwise.
      Source: string
      /// A tracked read of the value as a plain number in `Unit`. Raises as the node's read does.
      Read: unit -> float
      /// A tracked read of the status. Never raises.
      Status: unit -> QuantityStatus }

/// Quantities by identifier, with an optional parent searched after it.
[<Sealed>]
type Model =
    member Name : string
    member Parent : Model voption
    /// This model's own entries, in declaration order.
    member Entries : Entry list
    /// This model's entry, else the nearest ancestor's.
    member TryFind : id: string -> Entry voption

module Model =
    val create : name: string -> Model
    val child : parent: Model -> name: string -> Model

[<Sealed>]
type Quantity<[<Measure>] 'u> =
    member Entry : Entry
    /// A tracked read.
    member Value : float<'u>

[<Sealed>]
type Input<[<Measure>] 'u> =
    inherit Quantity<'u>
    member Set : float<'u> -> unit

[<Sealed>]
type Block<'T> =
    member Id : string
    member Name : string
    member Value : 'T

module Quantity =
    val input : Model -> id: string -> name: string -> unit: UnitOf<'u> -> initial: float<'u> -> Input<'u>
    val constant : Model -> id: string -> name: string -> unit: UnitOf<'u> -> value: float<'u> -> source: string -> Quantity<'u>
    val derive : Model -> id: string -> name: string -> unit: UnitOf<'u> -> compute: (unit -> float<'u>) -> Quantity<'u>
    val deriveWithin : Tolerance -> Model -> id: string -> name: string -> unit: UnitOf<'u> -> compute: (unit -> float<'u>) -> Quantity<'u>
    /// Not entered in the model's entries: a block has no single unit.
    val block : Model -> id: string -> name: string -> compute: (unit -> 'T) -> Block<'T>
```

An identifier matches `[A-Za-z_][A-Za-z0-9_]*`. A declaration raises `ArgumentException` for any other identifier and for one the same model already holds; a child may shadow its parent. Quantities wrap `createSignal` and `createMemo` and are created under the owner current at the call.

- [ ] **Step 1: Write the failing tests** (`"Quantity"`):

| Test name | Asserts |
|---|---|
| `a derived quantity recomputes for the inputs it reads and no others` | inputs `a`, `b`; `d = derive … (fun () -> runs <- runs + 1; a.Value * 2.0)`; after a first read `runs = 1`; `b.Set` then read: `1`; `a.Set` then read: `2` |
| `an equal write recomputes nothing` | `a.Set a.Value` then read: `runs` unchanged |
| `deriveWithin holds its value inside the tolerance` | with `Tolerance.absolute 1.0` over an input stepped `0.4, 0.8, 1.2` from `0.0`: a downstream derived quantity recomputes once, at `1.2` |
| `an entry describes its quantity` | for `constant model "g0" "Standard gravity" (Units.metre / (UnitOf.squared Units.second)) 9.80665<_> "CGPM 1901"`: `Id`, `Name`, `Kind = Constant`, `Source`, `UnitInfo.symbol Unit = "m/s^2"`, `Read () = 9.80665`, `Status () = Ready` |
| `a child model finds its own entry, then its parent's` | parent holds `alpha`; child holds `mean` and its own `alpha`: `child.TryFind "mean"`, `child.TryFind "alpha"` is the child's, `parent.TryFind "mean"` is `ValueNone`; a child without `alpha` finds the parent's |
| `a duplicate or malformed identifier is rejected` | `Expect.throwsT<System.ArgumentException>` for a second `"alpha"` in one model and for `"2x"`, `"a b"`, `""` |
| `a body that raises fails its quantity alone` | `d1` raises `InvalidOperationException "boom"`, `d2` reads another input: `d1.Entry.Status ()` is `Failed` with that exception, `d1.Value` raises it, `d2.Value` is correct; after the input that `d1` reads changes to a value that does not raise, `d1.Entry.Status () = Ready` |
| `a quantity over a pending stream is pending` | `derive` reading a stream fold with no item: `Status () = Pending`; after an item: `Ready` and the value |
| `a block is tracked like a quantity` | `block … (fun () -> Array.init 3 (fun i -> a.Value * float i))` recomputes when `a` changes and not when `b` does |

- [ ] **Step 2: Run** on .NET. Expected: FAIL, `Quantity` is not defined.
- [ ] **Step 3: Implement** `Model.fs` and `Quantity.fs`.
- [ ] **Step 4: Run both suites.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(quantities): named quantities and models`.

### Task 11: `Run`, a simulation as one restartable node (spec §8)

**Files:**
- Create: `src/Ranvier.Quantities/Run.fs`, `tests/Ranvier.Science.Tests/RunTests.fs`

**Interfaces:**
- Produces:

```fsharp
/// A simulation advanced with a clock. Its whole state is one node.
[<Sealed>]
type Run<'s> =
    /// A tracked read that first brings the state up to the clock's current value.
    member State : 's

module Run =
    /// `start` runs when the run is first read, when a quantity `start` read changes, and when the clock
    /// is below its last value by more than 1e-9. A start records the clock and does not advance.
    /// When the clock is above its last value, `advance state (min maxStep (clock − last))` runs once.
    /// Reads inside `advance` are not tracked.
    val createWith : maxStep: float -> clock: (unit -> float) -> start: (unit -> 's) -> advance: ('s -> float -> unit) -> Run<'s>
    /// `createWith infinity`.
    val create : clock: (unit -> float) -> start: (unit -> 's) -> advance: ('s -> float -> unit) -> Run<'s>
```

These are the rules of `Kit.Flow.At` (`AstroShowcase/Kit.fs:169-180`), with tracked reads of `start` in place of `keyOf`; `Flow`'s fixed step limit of 0.1 is `maxStep`. An equal or NaN clock value neither advances nor restarts. A reader of `State` is notified after every advance and every start, though the state object may be the same. Spec §8 gives a reference construction from two memos.

- [ ] **Step 1: Write the failing tests** (`"Run"`), with `type Sim = { mutable X: float; mutable Steps: int }`, signals `clock`, `x0`, `rate`, `start = fun () -> { X = x0.Value; Steps = 0 }` and `advance = fun s dt -> s.X <- s.X + rate.Value * dt; s.Steps <- s.Steps + 1`. `advance` reads `rate.Value` on purpose: the run must leave that read untracked.

| Test name | Asserts |
|---|---|
| `the first read starts and does not advance` | `X = x0`, `Steps = 0` |
| `a later clock value advances once by the gap` | clock `0 → 2.5`, read: `Steps = 1`, `X = x0 + rate * 2.5` |
| `clock writes nobody reads advance nothing` | clock `1, 2, 3` with no read between: the `advance` call count is 0; one read then makes it 1 with a gap of 3 |
| `maxStep limits one advance` | `createWith 0.1`, clock `0 → 2.5`: `X = x0 + rate * 0.1` |
| `a quantity read by start restarts the run` | `x0` changes: next read has a new state object, `Steps = 0`, `X` the new `x0` |
| `a quantity read only by advance changes the course without a restart` | `rate` changes between two advances: same state object, `Steps = 2`, `X = x0 + r1 * d1 + r2 * d2` |
| `the clock going back restarts` | clock `5 → 1`: new state, `Steps = 0` |
| `an equal or NaN clock does nothing` | re-writing the same clock value, then `nan`: same state object, `Steps` unchanged, no start |
| `a reader is notified after each advance` | an effect reading `run.State.Steps` records `[0; 1; 2]` over two clock writes |
| `an advance that raises fails the read, and a restart recovers` | `advance` raises once: `run.State` raises it; after `x0` changes, `run.State` reads the new start |

- [ ] **Step 2: Run** on .NET. Expected: FAIL, `Run` is not defined.
- [ ] **Step 3: Implement** `Run.fs`.
- [ ] **Step 4: Run both suites.** Expected: PASS.
- [ ] **Step 5: Commit** `feat(quantities): a simulation as one restartable node`.

### Task 12: `Generator`, deterministic deviates

**Files:**
- Create: `src/Ranvier.Numerics/Generator.fs`, `tests/Ranvier.Science.Tests/GeneratorTests.fs`

**Interfaces:**
- Produces:

```fsharp
/// A pseudo-random generator with value semantics: the same state always gives the same next value.
[<Struct>]
type Generator   // representation private

module Generator =
    val create : seed: int -> Generator
    /// A uniform value in [0, 1) and the next generator.
    val uniform : Generator -> struct (float * Generator)
    /// A standard normal deviate and the next generator.
    val normal : Generator -> struct (float * Generator)
    /// `count` normal deviates with the given mean and standard deviation, and the next generator.
    val normals : mean: float -> sd: float -> count: int -> Generator -> struct (float[] * Generator)
```

State updates use 32-bit integer arithmetic only, so JavaScript runs them without 64-bit emulation. The algorithm is the implementer's choice; uniform values are bit-identical on both targets, normal deviates may differ in the last place because they use `log`.

- [ ] **Step 1: Write the failing tests** (`"Generator"`):

| Test name | Asserts |
|---|---|
| `the same seed gives the same sequence` | 1,000 `uniform` values from two generators of seed 7 are `=` |
| `different seeds differ` | seeds 1 and 2 differ within the first 4 values |
| `uniform stays in [0, 1)` | 100,000 values: min `>= 0.0`, max `< 1.0`, mean within `0.005` of `0.5` |
| `normal has mean 0 and variance 1` | 100,000 values through `Moments`: `abs mean <= 0.02`, `abs (variance - 1.0) <= 0.03` |
| `normals scales and shifts` | `normals 10.0 2.0 50_000`: mean within `0.05` of `10.0`, variance within `0.15` of `4.0`; the returned generator continues the sequence |
| `the first uniform values of seed 1 are pinned` | the first 8 values `=` eight literals, written into the test from the .NET run in Step 3 and then run under JavaScript |

- [ ] **Step 2: Run** on .NET. Expected: FAIL, `Generator` is not defined.
- [ ] **Step 3: Implement** `Generator.fs`; print the eight pinned values with round-trip formatting and paste them into the last test.
- [ ] **Step 4: Run both suites.** Expected: PASS, including the pinned values under JavaScript.
- [ ] **Step 5: Commit** `feat(numerics): deterministic generator on 32-bit arithmetic`.

### Task 13: The confidence sequence (spec §7.3)

**Files:**
- Create: `src/Ranvier.Statistics/NormalQuantile.fs`, `src/Ranvier.Statistics/ConfidenceSequence.fs`, `tests/Ranvier.Science.Tests/ConfidenceSequenceTests.fs`
- Modify: `docs/PROBES.md` (a "Sources read" entry)

**Interfaces:**
- Consumes: `Moments` (Task 5), `Generator` (Task 12).
- Produces:

```fsharp
namespace Ranvier.Statistics

[<Struct>]
type Interval = { Lower: float; Upper: float }

module NormalQuantile =
    /// The standard normal quantile of p in (0, 1), to a relative error of 1.2e-9 or better.
    val ofProbability : p: float -> float

module ConfidenceSequence =
    /// sd · sqrt( (2(tρ² + 1) / (t²ρ²)) · ln( sqrt(tρ² + 1) / α ) )
    val radius : alpha: float -> rho: float -> t: float -> sd: float -> float
    /// mean ± radius at the moments' count and population standard deviation. NaN bounds for an empty sample.
    val ofMoments : alpha: float -> rho: float -> Moments -> Interval
    /// The rho for which `radius` at t = n is smallest.
    val rhoTightestAt : alpha: float -> n: float -> float

module FixedSample =
    /// mean ± z(1 − α/2) · sd / sqrt t. Valid at one planned sample size only.
    val ofMoments : alpha: float -> Moments -> Interval
```

Every function raises `ArgumentOutOfRangeException` unless `0 < alpha < 1`, `rho > 0` and `n > 0`.

- [ ] **Step 1: Read the source.** Read Theorem 2.2 (equation 13) and appendix B.2 of arXiv 2103.06476 in the PDF. Confirm the `radius` formula above, or correct it, and take `rhoTightestAt` from B.2. If the authors' reference implementation is reachable, compare `radius` with it on three inputs. Record in `docs/PROBES.md` what was read, the equation numbers, and any correction. The spec's ⚠️ on this formula is cleared or kept by this step.

- [ ] **Step 2: Write the failing tests** (`"ConfidenceSequence"`):

| Test name | Asserts |
|---|---|
| `radius at alpha 0.05, rho 1, t 100, sd 1` | within `1e-6` of `0.3273019` (*calculated here* from the formula) |
| `radius scales with sd` | `radius a r t 3.0 = 3.0 * radius a r t 1.0` within relative `1e-15` |
| `rhoTightestAt is a minimum at n` | for `alpha = 0.05`, `n = 1000` and `r = rhoTightestAt alpha n`: `radius alpha r n 1.0 <= radius alpha (r * k) n 1.0` for `k` in `0.5, 0.9, 1.1, 2.0` |
| `normal quantile` | `ofProbability 0.975` within `1e-8` of `1.959963985`; `ofProbability 0.5 = 0.0` within `1e-12`; `ofProbability 0.025 = -(ofProbability 0.975)` within `1e-9` |
| `an empty sample has NaN bounds` | both intervals for `Moments.zero` |
| `one observation gives a zero-width interval` | `Lower = Upper = 3.5` for `[| 3.5 |]` |
| `arguments out of range are rejected` | `Expect.throwsT<System.ArgumentOutOfRangeException>` for `alpha` `0.0`, `1.0`, `nan`; `rho` `0.0`, `-1.0`; `n` `0.0` |
| `the sequence covers under continuous monitoring and the fixed-sample interval does not` | below |

The coverage test: for `i` in `1..2000`, a stream of 10,000 `Generator.normal` deviates from seed `i` (true mean 0), folded one at a time into `Moments`. With `alpha = 0.05` and `rho = rhoTightestAt 0.05 1000.0`, a stream *misses* when its interval excludes 0 at any `t >= 30`. Assert the sequence's miss fraction is `<= 0.07` (α plus a 0.02 simulation margin) and the fixed-sample interval's is `>= 0.15`. The test prints both fractions.

- [ ] **Step 3: Run** on .NET. Expected: FAIL, `ConfidenceSequence` is not defined.
- [ ] **Step 4: Implement** the two files. Acklam's rational approximation meets the quantile's stated error.
- [ ] **Step 5: Run both suites.** Expected: PASS. Copy the two printed fractions, per target, into `docs/PROBES.md` under "Coverage", labelled *measured here*.
- [ ] **Step 6: Commit** `feat(statistics): asymptotic confidence sequence with a coverage test`.

### Task 14: One recorded stream, the same numbers on both targets (spec §7.4, guarantee 2)

**Files:**
- Create: `tests/Ranvier.Science.Tests/Recorded.fs`, `tests/Ranvier.Science.Tests/CrossTargetTests.fs`
- Modify: `README.md`

**Interfaces:**
- Consumes: `Moments`, `ConfidenceSequence`, `Generator`.
- Produces: `Recorded.stream : float[]`, 1,000 doubles as round-trip literals, reused by Plan 2's page test.

- [ ] **Step 1: Generate the stream once on .NET**: `Generator.normals 0.84 3.0 1000 (Generator.create 20261119)`, written to `Recorded.fs` as literals with round-trip formatting. The file's first comment says how it was made and that it is data: it is never regenerated by a test.
- [ ] **Step 2: Write the failing test** (`"CrossTarget"`), `the recorded stream gives pinned numbers`: fold `Recorded.stream` with `Moments.add true`; assert `Moments.count`, `Moments.mean` and `Moments.variance` are `=` three literals (exact equality), and that `ConfidenceSequence.ofMoments 0.05 (rhoTightestAt 0.05 1000.0)` gives bounds within relative `1e-12` of two literals. Use placeholders that fail.
- [ ] **Step 3: Run** on .NET. Expected: FAIL on the placeholders. Paste the printed round-trip values in as the literals.
- [ ] **Step 4: Run both suites.** Expected: PASS on both. A mean or variance that differs under JavaScript is a finding: record the two values in `docs/PROBES.md` and stop for review, because spec §7.4 claims they are equal.
- [ ] **Step 5: Write `README.md`**: what the repository is, the two test commands, the two benchmark commands, and links to `docs/PROBES.md` and the two measurement files.
- [ ] **Step 6: Final gate.** `dotnet build -c Release` with no warnings from the three library projects; both suites, unfiltered; `git status` clean after the commit.
- [ ] **Step 7: Commit** `test: one recorded stream gives the same numbers on .NET and JavaScript`.

---

## What Plans 2 and 3 take from this plan

- `docs/PROBES.md`: probe outcomes, the list of core workarounds, the sources read, the coverage fractions.
- `docs/measurements/node-cost.md` and `aggregates.md`: the answers to the spec's Q1 and half of Q2.
- The interfaces in each task's **Produces** block.
