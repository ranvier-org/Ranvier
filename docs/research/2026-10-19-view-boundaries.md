# View boundaries: mutation checks and measurements

Date: 2026-10-19. Branch `poc/fable-ranvier-dom`. Plan: [view boundaries](../superpowers/plans/2026-10-19-view-boundaries.md).

## Mutation checks

Each mutant is one edit to `experiments/Ranvier.View.Foundation`, compiled into the JS under test by `npm run test:dom` (which runs the Fable compile first), then restored with `git checkout`. The baseline run is 485 tests, all passing; the final run is 487 after the test added for the last mutant. The rows of the table and their counts were measured on `ace6b20`. File names below are in that directory.

| Mutant | Edit | Killed by |
|---|---|---|
| Authored waiting registration removed | `Mounting.fs` `startBinding`, synchronous effect: `state <- BindingState.rejected scope state error` becomes `state <- state` | 11 failures of 485, including `tree synchronous: a boundary reveals its content in one insertion once every binding, row and arm has a value`, `tree synchronous: removing the last pending row reveals the boundary`, `tree synchronous: retry remounts the content with a new setup and pending phase` |
| Template render-slot waiting registration removed | `TemplateDom.fs` `startRender`: `flags <- BindingState.rejected scope flags error` becomes `flags <- flags` | `template: a boundary waits for a pending render slot after its isolated slots settle`, `a recurring slot failure object is reported again after the slot recovers` (2 of 485) |
| Template isolated-slot waiting registration removed | `TemplateDom.fs` `startIsolated`: the same edit | `template: a boundary waits for a pending isolated slot after its render slots settle` (1 of 485) |
| `Ready` reveal check removed | `Mounting.fs` `ViewBoundary` `Ready`: `if waiting = 0 then` becomes `if true then` | 9 of 485, including `tree synchronous: a boundary reveals its content in one insertion once every binding, row and arm has a value`, `program queued: removing the last pending row reveals the boundary`, `template: removing the one pending row before the reveal reveals the remaining row` |
| `MountContent` reveal check removed | `Mounting.fs` `MountContent`: `\| None when waiting = 0 -> this.Reveal ()` becomes `\| None when true -> this.Reveal ()` | 25 of 485, including all four `... a boundary reveals its content in one insertion ...` tests and `tree queued: a failing pending view goes to the enclosing boundary` |
| Unregistration on disposal removed | `Mounting.fs` `startBinding`: `if scope.Tracks then Api.onCleanup (...)` becomes `if false then ...` | `program queued: removing the last pending row reveals the boundary`, `tree synchronous: removing the last pending row reveals the boundary` (2 of 485) |
| Synchronous setter catch removed | `Mounting.fs` `startBinding` `commit`: `\| error -> scope.Fail [ error ]` becomes `\| error -> reraise ()` | `tree: a live synchronous setter failure is reported and an equal later value retries the write`, `tree synchronous: a live setter failure under a boundary replaces the content with the error view`, and 6 more (8 of 485) |
| Queued per-scope report removed | `Scheduling.fs` `Queue.Flush`: `if failures.Count > 0 then` becomes `if false then` | `a flush reports each scope's failures in one call, in commit order`, `tree: a live queued setter failure is reported after the flush and kept in the queue's errors`, `tree queued: a queued setter failure under a boundary replaces the content after the flush`, and 12 more (15 of 485) |
| Authored handler catch removed | `Mounting.fs` `MountState.Listen`: `scope.FailAll [ error ]` becomes `reraise ()` | `tree synchronous: a throwing delegated handler under a boundary shows the error view`, `program synchronous: a throwing direct handler under a boundary shows the error view`, and 12 more (14 of 485) |
| Template delegated handler catch removed | `TemplateDom.fs` `dispatchFor`: `scope.FailAll [ error ])` becomes `reraise ())` | `template: a throwing delegated handler under a boundary shows the error view`, `template: a delegated handler throwing NotReadyException under a boundary shows the error view`, `template: a delegated handler after one whose failure tore the content down is skipped` (3 of 485) |
| Template direct handler catch removed | `TemplateDom.fs` `listenDirect`: `scope.FailAll [ error ]` becomes `reraise ()` | `template: a throwing direct handler under a boundary shows the error view`, `template: a direct handler throwing NotReadyException under a boundary shows the error view`, `template: a direct handler after one whose failure tore the content down is skipped` (3 of 485) |
| Reveal run synchronously in `Ready` | `Mounting.fs` `ViewBoundary`: `member _.Ready()` becomes `member this.Ready()` and `Api.untrack (fun () -> wake.Value <- wake.Value + 1)` becomes `this.Settle ()` | Survived at 485 of 485. Killed after adding `tree synchronous: replacing the last pending row reveals both rows after the pass` and `program queued: replacing the last pending row reveals both rows after the pass` (2 failures of 487). On `6e68d4b`, the 93 boundary tests: 6 fail, all six `replacing a pending arm with another pending arm` cases; the `replacing the last pending row` cases pass |
| Pending flag not set on a later pending read | `Mounting.fs` `ViewBoundary` `Wait`: `pending.Value <- true` becomes `()` | `tree synchronous: a pending read after the reveal keeps the content and sets IsPending`, `template synchronous: a boundary reveals rows and arms in one insertion and keeps them through a refetch`, and 4 more (6 of 485) |
| Per-handler disposal check removed | `TemplateDom.fs` `dispatchFor`: the line `&& not owner.IsDisposed` is deleted | `delegated clicks reach a moved row and stop after its removal`, `template: a delegated handler after one whose failure tore the content down is skipped` (2 of 485) |

### The surviving mutant

Running a reveal synchronously from `Ready` left every test passing. The reveal runs inside a list pass when the pending row's disposal releases the last waiting binding. The existing `removing the last pending row` tests remove the row, and the existing tests change nothing else in the pass. `replacedPendingRow` removes the pending row and adds a row in one pass (`[1; 2]` to `[3; 1]`) and expects `x3x1` once the pass ends. The mutant fails it in the tree synchronous and program queued modes.

## Finding

Replacing a pending `Branch.select` arm with another pending arm reveals the boundary with empty content on the unmutated code. `Settle` had no `&& waiting = 0` clause in this code; adding it alone does not change the result.

Removing the `Settle` clause added by `7e90084` (mutant M1) fails the 5 arm cases of the 90 boundary tests at that commit.

Cause and fix: a new binding's first read runs in its own queued effect, after the boundary effect that the replaced binding's `Ready` already queued, so `Settle` saw `waiting = 0` before the new binding registered; `7e90084` records each new binding under an unrevealed boundary as waiting until its first read (`ViewScope.Expect`) and adds `&& waiting = 0` to `Settle`; template arms show the same bug and take the same fix, and a keyed row replacing the one pending row with another pending row is fixed by either part, the `Settle` clause or `Expect`, alone.

A boundary mounted inside a flush mounts, sets up, cleans up and removes its pending view within the same flush, with no paint between; Task 10 measures the cost.

`6e68d4b` writes the wake from `Ready` only after the content has set up (`MountContent` checks `waiting` itself): a boundary over three ready bindings and one pending binding now writes no wake during setup and one when the read settles (`BoundaryCounters.wakes`, tests `... a boundary writes no wake while its content sets up`); removing the `not settingUp` guard fails both.

## Measurements

Results: [`framework-performance/investigation/view-boundaries/`](framework-performance/investigation/view-boundaries/).

### Operations without a boundary

Before is a120cec (the code of 28f8291 plus a test and baseline files) and after is de6b960, each built in its own worktree with the same flags and the same `page.mjs` (the after bundle omits the `boundary-page.mjs` import, so the bundles differ by product code only). The runs were interleaved in one session: before, after, before, after, each a full run of three browser processes, giving six per-process medians per side. Each cell is the median of the six with their minimum to maximum in brackets. Files: `keyed-before-same.json`, `keyed-after-same.json`, `template-before-same.json`, `template-after-same.json`, `dispatch-before.json`, `dispatch-after.json`. The earlier files `keyed-before.json` and `template-before.json` were recorded in a different session; compared with an after run from this session, every row looked faster after, which indicates machine state, so they are kept only as the earlier session's record.

Every Ranvier operation has an overlapping before and after range. After over before ratios of the medians run from 0.917 to 1.091 with a median of 1.000.

What six-process ranges detect: a row's range width is a median of 9% of its median (90th percentile 25%, maximum 44% for the noisiest rows, such as 16-wide template mount). A uniform difference of a few percent, for example the 1.5 to 4.4% of the dispatch rows, lies inside those ranges and is not resolved; a difference larger than a row's range width would be. Absence of a disjoint range therefore supports "no regression above roughly 10 to 25% per row", not "identical".

Rows that exercise the render effect's flag check: operations that create bindings or re-run their render effects, namely keyed `create1k`, `create10k`, `replace1k`, `update10th1k`, `append1k`, `armsCreate1k`, `armsSwitch1k`, `armsPayload1k`, and template `mount` and `update` at every width. Rows that run no render effect and serve as controls: keyed `swap1k`, `remove1k`, `remove10k`, `clear1k`, `clear10k`, `armsClear1k` and template `dispose`.

#### Keyed rows, Ranvier (ms per operation)

| Operation | Before (ms, per-process range) | After (ms, per-process range) | After / before | Ranges overlap |
|---|---|---|---|---|
| create1k | 5.400 (5.265-5.560) | 5.375 (5.320-5.880) | 0.995 | yes |
| create10k | 48.050 (47.425-48.390) | 48.530 (48.035-48.940) | 1.010 | yes |
| replace1k | 4.675 (4.520-4.740) | 4.685 (4.540-4.735) | 1.002 | yes |
| update10th1k | 0.055 (0.055-0.060) | 0.060 (0.050-0.065) | 1.091 | yes |
| swap1k | 0.135 (0.125-0.140) | 0.135 (0.125-0.140) | 1.000 | yes |
| remove1k | 0.135 (0.130-0.140) | 0.140 (0.135-0.145) | 1.037 | yes |
| remove10k | 0.605 (0.585-0.630) | 0.595 (0.575-0.615) | 0.983 | yes |
| clear1k | 0.850 (0.835-0.925) | 0.850 (0.830-0.905) | 1.000 | yes |
| clear10k | 6.005 (5.950-6.070) | 5.885 (5.805-5.980) | 0.980 | yes |
| append1k | 3.405 (3.310-3.415) | 3.405 (3.350-3.445) | 1.000 | yes |
| armsCreate1k | 9.700 (9.270-10.260) | 9.910 (9.625-11.115) | 1.022 | yes |
| armsSwitch1k | 4.055 (3.885-4.095) | 4.120 (3.960-4.850) | 1.016 | yes |
| armsPayload1k | 0.685 (0.655-0.725) | 0.685 (0.675-0.715) | 1.000 | yes |
| armsClear1k | 1.405 (1.360-1.435) | 1.480 (1.350-1.490) | 1.053 | yes |

#### Template comparison, Ranvier modes (µs per operation)

| Mode | Width | Phase | Before (µs, per-process range) | After (µs, per-process range) | After / before | Ranges overlap |
|---|---|---|---|---|---|---|
| ranvier | 1 | mount | 8.26 (8.01-8.93) | 8.28 (7.91-9.02) | 1.002 | yes |
| ranvier-template | 1 | mount | 5.49 (5.29-5.68) | 5.61 (5.31-6.04) | 1.021 | yes |
| ranvier | 1 | update | 1.02 (1.00-1.29) | 1.01 (0.99-1.02) | 0.990 | yes |
| ranvier-template | 1 | update | 1.01 (0.99-1.24) | 0.99 (0.98-1.01) | 0.975 | yes |
| ranvier | 1 | dispose | 1.46 (1.35-1.68) | 1.45 (1.33-1.93) | 0.987 | yes |
| ranvier-template | 1 | dispose | 1.27 (1.23-1.45) | 1.27 (1.23-1.39) | 1.000 | yes |
| ranvier | 16 | mount | 62.27 (59.22-86.45) | 65.16 (63.32-68.63) | 1.046 | yes |
| ranvier-template | 16 | mount | 42.62 (41.56-42.81) | 42.27 (40.98-43.55) | 0.992 | yes |
| ranvier | 16 | update | 13.38 (13.19-13.42) | 13.38 (13.32-13.51) | 1.000 | yes |
| ranvier-template | 16 | update | 13.99 (13.83-14.03) | 13.93 (13.82-13.97) | 0.995 | yes |
| ranvier | 16 | dispose | 9.26 (7.93-10.78) | 9.49 (8.67-10.08) | 1.025 | yes |
| ranvier-template | 16 | dispose | 4.45 (4.18-4.84) | 4.14 (3.71-5.04) | 0.930 | yes |
| ranvier | 128 | mount | 521.72 (454.06-554.84) | 518.28 (462.19-540.78) | 0.993 | yes |
| ranvier-template | 128 | mount | 316.25 (302.50-325.94) | 326.88 (300.16-333.91) | 1.034 | yes |
| ranvier | 128 | update | 105.30 (102.70-107.35) | 104.58 (103.33-108.05) | 0.993 | yes |
| ranvier-template | 128 | update | 110.25 (106.45-112.52) | 110.02 (108.75-112.08) | 0.998 | yes |
| ranvier | 128 | dispose | 63.59 (59.53-75.78) | 65.94 (62.03-77.81) | 1.037 | yes |
| ranvier-template | 128 | dispose | 26.41 (22.50-28.28) | 24.22 (23.28-25.78) | 0.917 | yes |

#### Cost of the handler dispatch wrapper

The `try`/`with` around each event-handler dispatch runs on every click. Keyed rows and the template comparison dispatch no events, so `dispatch-clicks.mjs` mounts 100 rows without a boundary, clicks each row 50 times per sample, in 15 rounds per process; before and after runs were interleaved as above. One sample spans 5000 clicks; the page is not cross-origin isolated, so `performance.now()` resolves to about 0.1 ms, which quantises a per-click value by about 0.02 µs.

| Mounted rows | Before (µs per click, per-process range) | After (µs per click, per-process range) | After / before | Ranges overlap |
|---|---|---|---|---|
| 100 authored buttons | 1.360 (1.320-1.400) | 1.420 (1.360-1.440) | 1.044 | yes |
| 100 template list rows | 1.540 (1.440-1.560) | 1.560 (1.460-1.600) | 1.013 | yes |

The wrapper's cost is at most a few percent of a click and lies within the measured spread.

### Boundary reveal

Rows read one async value under a boundary (Solid 2: `Loading`). Scenario `reveal` mounts the boundary at the root and settles the source afterwards. Scenario `switch` mounts the boundary from a branch switch over an already settled source, so the boundary mounts inside a flush. In both frameworks the switch scenario inserts the pending view, then removes it and inserts the rows: `verify` observes the insertion, asserts that the pending view is absent after the reveal and that the host is empty after disposal. The difference lies in timing: Ranvier reveals within the flush that mounts the boundary, while Solid 2 reads the resolved promise through an async memo created where the branch is created and reveals in a later microtask turn, which the adapter awaits. The switch ratio contains that turn. Medians are per boundary, in µs.

| Scenario | Width | Ranvier median (µs, range) | Solid 2 median (µs, range) | Ranvier / Solid 2 | Ranvier retained bytes per row | Solid 2 retained bytes per row |
|---|---|---|---|---|---|---|
| reveal | 1 | 11.8 (11.4-12.0) | 10.8 (10.2-11.4) | 1.09 | 10744 | 1120 |
| reveal | 16 | 42.0 (41.1-43.1) | 28.1 (26.9-28.6) | 1.49 | 1630 | 682 |
| reveal | 128 | 209.4 (207.5-213.7) | 131.3 (127.5-136.2) | 1.60 | 957 | 567 |
| switch | 1 | 15.9 (15.9-16.4) | 12.1 (12.0-12.3) | 1.32 | 14077 | 3139 |
| switch | 16 | 30.6 (30.0-31.6) | 25.9 (25.3-26.1) | 1.18 | 1758 | 758 |
| switch | 128 | 141.9 (140.0-146.9) | 124.4 (123.7-128.7) | 1.14 | 808 | 584 |

The 1-wide `reveal` row is parity: the ranges touch at 11.4 µs. Retained bytes per row at width 1 are dominated by the boundary's fixed cost, which the single row carries. At width 128 Ranvier's revealed boundary retains 1.4 to 1.7 times Solid 2's bytes per row, and more at narrower widths. Mounting the boundary inside a flush costs Ranvier about 4 µs more at width 1 than mounting it at the root, and about 11 µs less at width 16 and 68 µs less at width 128; this comparison was not decomposed.
