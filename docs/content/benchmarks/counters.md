---
title: Instruction counts
order: 9
---

The counter bench measures whole scenarios rather than single primitives: creating and disposing
rows, propagating writes, projections, application-shaped workloads, graph shapes and async
boundaries. Each figure is retired processor instructions per operation, `(m(2N) − m(N)) / N`,
read from hardware counters. Under matched, stable measurement conditions, differences below
about 5 % are treated as run-to-run noise. Larger differences still need measurement-state checks.
Under Node, the harness settles each case under full tiering and keeps tier-up disabled across its
measured regions; a region in which V8 compiles is reported as compiled.

CI also runs every Node case under `node --jitless` (runtime `node-jitless`), with one warm-up run
and no settling. The interpreter executes the same bytecode whichever objects earlier runs left
alive, so these counts follow the work done, and they are the Node figures the perf gate checks.
Tiered Node counts depend on V8's optimisation history and are reported without a gate.

The static .NET and Node charts below are the historical `e13f159` run. The interactive
views use recorded CI measurements (hardware counters and allocations on arm64, CPU time on x64)
and identify the selected commit explicitly.
The broader [October 4 report at `878f294`](https://github.com/ranvier-org/Ranvier/blob/master/docs/.ai/benchmarks/counters/878f294.md)
includes timed operators and additional derived/effect scenarios. The later
[keyed-pass report](https://github.com/ranvier-org/Ranvier/blob/master/docs/.ai/benchmarks/counters/6d496e7-untrimmable-pass.md)
is a targeted comparison for #56, rather than a replacement for every chart panel.

Each panel scales from zero to its largest bar, so bar lengths compare engines within a scenario,
not scenarios with each other.

## CI counter history

The chart compares the selected metric per operation across commits and includes engine
comparisons where the run provides them. Choose a runtime, metric, **From** baseline (defaulting to
the last 30 measured commits) and scenario; .NET and Node.js remain separate because their
measurements are not comparable. Select a commit point or label to inspect its library comparison;
open its source using the commit link, or click **Start from** to re-anchor the window there. Hover
or focus marks for exact values, toggle libraries in the legend, and switch between **Change since
From**, per-operation values, and values relative to Ranvier. The site builds this history from the
`perf-data` branch after each successful master measurement. Perf skips documentation-only commits
and commits whose final message paragraph carries a `Perf-Ignore:` trailer, so the history holds
measured commits only.

<div class="rv-perf" data-perf-history><p role="status">Loading recorded measurements…</p></div>

## Library comparison

Explore the scenarios together, filter by name, or choose a recorded commit. Each panel has its
own scale from zero; compare library costs within a scenario. Toggle a library in the legend to
isolate the others. Relative values use Ranvier at the selected commit as their reference.

<div class="rv-perf" data-perf-compare><p role="status">Loading recorded measurements…</p></div>
<link rel="stylesheet" href="/Ranvier/benchmarks/perf-charts.css">
<script src="/Ranvier/benchmarks/perf-charts.js"></script>

## .NET

.NET 10.0.12, with tiered compilation, PGO and ReadyToRun off. Ranvier at `e13f159`,
FSharp.Data.Adaptive 1.2.27, R3 1.3.1.

![Instructions per operation under .NET](/Ranvier/benchmarks/counters-dotnet.svg)

## Fable under Node.js

Node.js v26.7.0 (`--expose-gc --single-threaded`), fable-library-js 5.18.0. Ranvier at `e13f159`,
Fable.Ripple 1.0.0-beta.5. Bars are main-thread figures. The CI charts label each commit with the Fable.Ripple
version it measured.

The CI counter history runs Node.js 26 with
`--expose-gc --single-threaded --min-semi-space-size=32 --max-semi-space-size=32`. The semi-space
flags fix each V8 semi-space at 32 MB, and every measured region starts with the same
young-generation capacity.

![Instructions per operation under Node.js](/Ranvier/benchmarks/counters-node.svg)

Scenarios with a negative figure (`derive-effect`, `derive-on`, `shape-dynamic`, `async-recover`)
show no bar. A negative subtraction is a measurement limitation, not negative engine work.

### Fable core follow-up

[#57](https://github.com/ranvier-org/Ranvier/pull/57), merged as `2ce4b7d`, removes redundant Fable affinity/inbox work, observer/dependency slot wrappers, async continuation and lock callbacks, and checked scheduler queue stores. The .NET executable implementation is preserved.

The [source investigation at `d962b84`](https://github.com/ranvier-org/Ranvier/blob/d962b84e34d27f19b42e003131b2b5ac5e7a5540/docs/research/2026-10-16-generated-fable-savings.md) measured the combined patch before its core-only cherry-pick into `master`. Three fresh processes per version/scenario, forty warmups and twenty-one measured rounds gave median elapsed-time reductions of 36% for equal writes, 11% for updates, 26% for disposing 1,000 rows and 34% for creating 1,000 rows; the 2% chain difference was treated as noise. These are timings for that experiment, not fresh retired-instruction counts for #57. Creation/disposal remain sensitive to JIT state and warmup. Renderer changes and the measured playground bundle saving in that report were not merged by #57.

The companion [historical-counter investigation](https://github.com/ranvier-org/Ranvier/blob/d962b84e34d27f19b42e003131b2b5ac5e7a5540/docs/research/2026-10-16-instruction-erosions.md) compares `d87920f`, `878f294` and the PoC's `2ef3876` snapshot. Historical Node creation totals were nonlinear in N; separate traces of the isolated scenario found compilation/deoptimisation inside measured regions. Those traces demonstrate the protocol's vulnerability, not the exact compilation cost in the historical full-suite runs. The reported +18.5% creation delta therefore does not establish a steady-state source regression. The disposal gap remains an observation under that protocol, requiring a compilation-audited, scale-checked matched rerun before assigning it to a feature. No new hardware-counter acceptance run is claimed for #57.

## Wave B scenarios

Seven scenarios measure Ranvier alone, on .NET only. A scenario with variants reports each variant as its own bar.

| Scenario | One operation | Variants |
| --- | --- | --- |
| `chain-affinity` | Write the source of a chain of 4 memos and read the tail. | `Guarded`, `Unchecked`, `Serialised` |
| `project-churn` | Replace the last key of a 1000-row projection with a new key. | No key reader; one key reader read by an effect |
| `flight` | Write an async memo's trigger and read it, twice, then settle every flight the writes started. | `CancelPrevious`, `KeepLatest`, `Queue`, `FinishCurrent`, `Drop`, `DropWhilePending` |
| `fail-recompute` | Re-run a memo and its one reader, then read the reader's `ErrorOrigin`. | Succeeding; failing with a fresh exception |
| `editable-edit` | Edit every 10th of 1000 editables, each read by one effect. | — |
| `editable-upstream` | Write the seed source of every 10th of 1000 editables. | — |
| `mvu-dispatch` | Change one field of a 64-field model with one reader per field. | A signal per field; `Mvu.Dispatch` with a `Select` per field |

The static .NET chart includes these scenarios at `e13f159`, which predates the `Drop` and
`DropWhilePending` variants of `flight`; the CI charts include every variant. Bars are colored by engine, and a scenario's variants share their engine's color.

## Later scenarios

These scenarios postdate `e13f159` and appear only in the CI charts. Each measures Ranvier alone.

| Scenario | One operation | Variants | Runtimes |
| --- | --- | --- | --- |
| `keyed-remove`, `keyed-swap`, `keyed-insert` | Alternately apply and undo one edit of a 1000-row projection: remove the middle row, swap the second and second-to-last rows, or prepend a row. | Fresh items; stable items; memo-free rows | .NET, Node.js |
| [`stream-burst-1`, `stream-burst-16`, `stream-burst-256`](streams.md#stream-burst) | Deliver 1, 16 or 256 items per round into a stream fold read by a memo and an effect. | A stream, batched; an `AsyncSource`, batched and unbatched | .NET, Node.js |
| [`stream-restart`](streams.md#stream-restart) | Write a dependency of a stream, restarting it, then deliver one item; or of an async memo, restarting its flight. | A stream; an async memo under `CancelPrevious` | .NET, Node.js |
| `control` | Refill a 256-entry closure table, sum it and sort a copy of its array. | — | .NET, Node.js |

`control` runs no Ranvier code. Every worker process measures it last, as a reference for the state of the
process and the machine. When `control` moves on a runtime, the perf verdict reports that runtime's regressions as
inconclusive.

## Comparable APIs

Each scenario below measures one shape on Ranvier and on every comparator library that has a counterpart API, each
through that library's own idiom. On .NET the comparators are FSharp.Data.Adaptive, R3, System.Reactive (Rx.NET),
SignalsDotnet and DynamicData; under Node.js they are Fable.Ripple and Solid 2 (`@solidjs/signals`). Scenarios other
than `create`, `update`, `chain`, `cutoff` and `dispose` appear only in the CI charts.

| Scenario | Ranvier | Comparators | Runtimes |
| --- | --- | --- | --- |
| `create`, `update`, `dispose` | a root of 1000 rows, each an `Effect` | FSharp.Data.Adaptive `AVal.map2` and a callback; R3 and System.Reactive `CombineLatest`; SignalsDotnet `Effect`; Fable.Ripple `Signal.effect`; Solid `createEffect` | .NET, Node.js |
| `chain` | 4 `createMemo` | FSharp.Data.Adaptive `AVal.map`; R3 and System.Reactive `Select`; SignalsDotnet `Signal.Computed`; Fable.Ripple `Signal.computed`; Solid `createMemo` | .NET, Node.js |
| `cutoff` | an equal write to a signal read by an effect | FSharp.Data.Adaptive `cval`; R3 `ReactiveProperty`; System.Reactive `DistinctUntilChanged`; SignalsDotnet `Signal`; Fable.Ripple `Var`; Solid `createSignal` | .NET, Node.js |
| `chain-map` | `Signal.map` then `Memo.map` | Fable.Ripple `Signal.map` | Node.js |
| `derive-effect` | an `Effect` that derives in its body | SignalsDotnet `Effect`; Fable.Ripple `Signal.effect` | .NET, Node.js |
| `derive-on` | `createEffectOn` | FSharp.Data.Adaptive `AVal.map` and a callback; R3 and System.Reactive `Select` then `DistinctUntilChanged`; SignalsDotnet `Signal.Computed` read by `Effect`; Fable.Ripple `Signal.computed` read by `Signal.effect`; Solid `createEffect` | .NET, Node.js |
| `work-status`, `work-value` | `createEffect`; `createEffectOn` | FSharp.Data.Adaptive `AVal.map`; R3 `Select` then `DistinctUntilChanged`; SignalsDotnet `Effect`, or `Signal.Computed` read by `Effect`; Fable.Ripple `Signal.effect`, or `Signal.computed` read by `Signal.effect`; Solid `createEffect` | .NET, Node.js |
| `work-form` | `createEffect`; `createEffectOn` | FSharp.Data.Adaptive `AVal.custom`; SignalsDotnet `Effect` or `Signal.Computed`; Fable.Ripple `Signal.effect` or `Signal.computed`; Solid `createEffect` or `createMemo` | .NET, Node.js |
| `keyed-remove`, `keyed-swap`, `keyed-insert` | `createProjection` | FSharp.Data.Adaptive `cmap` read by `AMap.tryFind`; DynamicData `SourceCache.EditDiff` read by `WatchValue`; Solid `mapArray` keyed by Id | .NET, Node.js |
| `shape-diamond` | `createMemo` branches and join | FSharp.Data.Adaptive `AVal.map` and `AVal.custom`; SignalsDotnet `Signal.Computed`; Fable.Ripple `Signal.computed`; Solid `createMemo` | .NET, Node.js |
| `shape-dynamic` | `createEffect` | FSharp.Data.Adaptive `AVal.bind`; R3 `Select` then `Switch`; SignalsDotnet `Effect`; Fable.Ripple `Signal.effect`; Solid `createEffect` | .NET, Node.js |
| `select` | `createSelector`; each row compares the index | FSharp.Data.Adaptive `AVal.map2` per row; SignalsDotnet `Effect` per row; Fable.Ripple `Signal.effect` per row; Solid `createProjection`, or `createEffect` per row | .NET, Node.js |
| `batch` | `Graph.Batch` | FSharp.Data.Adaptive `transact`; SignalsDotnet `Effect.AtomicOperation`; Fable.Ripple `Signal.batch`; Solid writes then one `flush` | .NET, Node.js |
| `stream-burst-1`, `-16`, `-256` | `createStreamFold` | R3 and System.Reactive `Subject`, `Scan` and `Select` | .NET |

`select` moves the selection over 1000 rows, each read by one effect. `batch` writes 8 sources read by one effect
inside one batch. The keyed scenarios write the whole list each operation, with fresh items.

## Reading the charts

### Interactive chart controls

On the topic pages, **Library comparison** compares engines at a selected commit; **Trends** follows
each scenario across recorded commits, with one panel per Ranvier variant. Choose the **From**
baseline (defaulting to the last 30 commits, or selecting a window, package version or commit SHA,
reflected in `?from=`), **Metric** (`instructions`, `cycles`, `allocated bytes`, `branch misses`,
or `x64 CPU time`), **Values** (`Change since From` or per operation), **Order**, and movement filter
pills (`Improved`, `Regressed`, `Within ±5%`). Runtime, metric and `From` selections carry between
tabs, and the site remembers the last selected tab across benchmark pages.

Scrubbing or arrow-keying any trend panel moves a synchronized crosshair across sibling panels, with
commit-to-commit deltas (`Δ prev`), net change since **From**, process min–max bands, and GitHub
commit/diff links in the sticky readout above the panels. Click a commit or press `Enter` to pin
it or re-anchor **From** there. Changes of at least 5% are highlighted (`−` green / `+` red); `▲`
marks commits where Ranvier's library counters changed, and `◆` marks accepted regressions with
their recorded reason.

### Comparator versions

Each commit carries the version of every comparator library it measured: FSharp.Data.Adaptive, R3,
System.Reactive, SignalsDotnet and DynamicData on .NET; Fable.Ripple and Solid under Node.js. Bar labels, readouts and the provenance line name the version, as in
`Fable.Ripple 1.0.0-beta.8 (compare)`, and the provenance line adds the commit's .NET, Node.js and Fable versions.
A `▽` in the library's color, at the top of a trend panel, marks a commit whose comparator version differs from the
previous commit's; the readout names both versions. **From** offers a **Comparator versions** group that anchors the
window at the first commit of a version (`?from=cmp:Fable.Ripple@1.0.0-beta.8`).

Comparator packages float to their newest release: `*` (newest stable) on .NET, `*-*` for Fable.Ripple, which
publishes prereleases only, and npm's `latest` tag for `@solidjs/signals`. Measurements label the versions their
builds restored. Records that predate those labels show the versions pinned by `bench/Ranvier.Counters` and `fable/Ranvier.Counters` at the commit.

### Comparison limits

- **Engines differ in semantics.** R3 and System.Reactive push each write or item straight to their
  subscribers, without batching or glitch-free ordering. FSharp.Data.Adaptive disposal removes callback
  subscriptions only. SignalsDotnet holds weak subscriptions, so each case keeps its effects referenced.
- **Solid applies writes when it flushes.** Every Solid operation calls `flush()` after each write it makes, so the
  effect work falls inside the measured region.
- **Keyed lists differ in shape.** Ranvier keeps one reader per key; Solid's `mapArray` creates and disposes a row
  owner as a row enters and leaves; FSharp.Data.Adaptive and DynamicData diff the written list into per-key changes.
- **.NET and Node figures do not compare.** The .NET worker runs with PGO off; V8 optimises
  adaptively.

The full report, with cycles, branch misses, allocations and library counters per scenario, is
[`docs/.ai/benchmarks/counters/e13f159.md`](https://github.com/ranvier-org/Ranvier/blob/master/docs/.ai/benchmarks/counters/e13f159.md)
in the repository.
