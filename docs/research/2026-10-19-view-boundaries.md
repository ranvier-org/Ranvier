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

Before: `keyed-before.json` and `template-before.json`, recorded at `28f8291` (the code before any boundary work; `a120cec` adds only a test and these baselines). After: `keyed-after.json`, `template-after.json`, `boundary-reveal.json` and `dispatch-after.json`, recorded at `40ab5ba`. Each value is the median over three browser processes with the per-process medians' minimum to maximum in brackets. Before and after were not measured in one session, so a uniform shift across every row reflects the machine, not the code.

### Operations without a boundary

No operation in any table is slower after with a disjoint range. Ranges overlap where the last column says yes; every other row is faster after.

#### Keyed rows, Ranvier (ms per operation)

| Operation | Before (ms, per-process range) | After (ms, per-process range) | Ranges overlap |
|---|---|---|---|
| create1k | 6.195 (5.875-6.900) | 5.380 (5.370-5.400) | no, faster after |
| create10k | 53.020 (51.020-54.680) | 48.215 (47.645-48.565) | no, faster after |
| replace1k | 5.770 (5.175-7.685) | 4.580 (4.505-4.605) | no, faster after |
| update10th1k | 0.080 (0.060-0.110) | 0.055 (0.055-0.055) | no, faster after |
| swap1k | 0.170 (0.145-0.225) | 0.130 (0.130-0.135) | no, faster after |
| remove1k | 0.205 (0.160-0.215) | 0.135 (0.130-0.140) | no, faster after |
| remove10k | 0.670 (0.660-0.675) | 0.585 (0.575-0.600) | no, faster after |
| clear1k | 1.015 (0.935-1.095) | 0.850 (0.840-0.860) | no, faster after |
| clear10k | 7.035 (6.560-8.435) | 5.845 (5.815-5.925) | no, faster after |
| append1k | 3.760 (3.525-3.950) | 3.365 (3.330-3.410) | no, faster after |
| armsCreate1k | 11.645 (11.485-12.150) | 9.740 (9.680-9.870) | no, faster after |
| armsSwitch1k | 5.215 (4.730-5.600) | 3.980 (3.895-4.040) | no, faster after |
| armsPayload1k | 0.990 (0.860-1.115) | 0.675 (0.670-0.685) | no, faster after |
| armsClear1k | 2.515 (2.400-2.790) | 1.415 (1.395-1.470) | no, faster after |

#### Template comparison, Ranvier modes (µs per operation)

| Mode | Width | Phase | Before (µs, per-process range) | After (µs, per-process range) | Ranges overlap |
|---|---|---|---|---|---|
| ranvier | 1 | mount | 9.41 (9.02-10.51) | 8.22 (8.20-8.24) | no, faster after |
| ranvier-template | 1 | mount | 5.90 (5.59-6.19) | 5.57 (5.35-5.86) | yes |
| ranvier | 1 | update | 1.12 (1.10-1.20) | 1.02 (1.01-1.04) | no, faster after |
| ranvier-template | 1 | update | 1.13 (1.11-1.14) | 1.02 (0.97-1.03) | no, faster after |
| ranvier | 1 | dispose | 2.36 (1.97-2.95) | 1.39 (1.33-1.48) | no, faster after |
| ranvier-template | 1 | dispose | 1.82 (1.48-2.38) | 1.25 (1.23-1.33) | no, faster after |
| ranvier | 16 | mount | 77.27 (66.80-90.59) | 62.70 (58.55-65.59) | no, faster after |
| ranvier-template | 16 | mount | 46.68 (45.98-48.16) | 42.19 (40.90-43.79) | no, faster after |
| ranvier | 16 | update | 14.45 (14.31-15.01) | 13.34 (13.29-13.44) | no, faster after |
| ranvier-template | 16 | update | 15.28 (15.19-15.46) | 13.94 (13.91-14.12) | no, faster after |
| ranvier | 16 | dispose | 13.87 (10.51-15.04) | 8.75 (8.16-10.04) | no, faster after |
| ranvier-template | 16 | dispose | 6.80 (4.53-8.95) | 4.26 (4.02-5.31) | yes |
| ranvier | 128 | mount | 524.06 (467.66-527.97) | 532.66 (510.31-541.09) | yes |
| ranvier-template | 128 | mount | 332.19 (323.28-364.22) | 305.31 (299.06-320.31) | no, faster after |
| ranvier | 128 | update | 115.02 (112.10-116.72) | 105.67 (103.10-107.95) | no, faster after |
| ranvier-template | 128 | update | 121.65 (119.53-121.97) | 110.70 (108.55-111.18) | no, faster after |
| ranvier | 128 | dispose | 90.62 (83.13-93.59) | 63.12 (60.47-66.25) | no, faster after |
| ranvier-template | 128 | dispose | 33.13 (31.72-34.22) | 24.22 (23.91-26.41) | no, faster after |

#### Cost of the boundary code outside a boundary

Two additions run for code with no boundary: the `try`/`with` around each event-handler dispatch, and the render effect's check of the boundary flag. Keyed rows and the template comparison dispatch no events. The keyed-row and template mount and update operations exercise the flag check: every binding's render effect runs it on each write. The dispatch measurement exercises the `try`/`with`: it mounts 100 rows without a boundary and clicks each row 50 times, in 15 rounds over 3 processes (`dispatch-clicks.mjs`). Before was run on `a120cec` in a separate worktree, after on `40ab5ba`.

| Mounted rows | Operation | Before (µs per click, per-process range) | After (µs per click, per-process range) | Ranges overlap |
|---|---|---|---|---|
| 100 authored buttons | click | 1.360 (1.340-1.380) | 1.380 (1.360-1.400) | yes |
| 100 template list rows | click | 1.480 (1.440-1.560) | 1.520 (1.460-1.560) | yes |

### Boundary reveal

Rows read one async value under a boundary (Solid 2: `Loading`). Scenario `reveal` mounts the boundary at the root and settles the source afterwards. Scenario `switch` mounts the boundary from a branch switch over an already settled source, so the boundary mounts inside a flush, mounts its pending view, removes it and reveals within that flush; Solid 2's counterpart is a `Show` switch over an already resolved promise. Medians are milliseconds per boundary converted to µs.

| Scenario | Width | Ranvier median (µs, range) | Solid 2 median (µs, range) | Ranvier / Solid 2 | Ranvier retained bytes per row | Solid 2 retained bytes per row |
|---|---|---|---|---|---|---|
| reveal | 1 | 11.2 (10.1-12.1) | 10.5 (10.5-10.6) | 1.07 | 9561 | 949 |
| reveal | 16 | 41.4 (40.9-42.0) | 27.2 (26.7-28.0) | 1.52 | 1611 | 553 |
| reveal | 128 | 221.2 (215.6-234.4) | 130.6 (128.1-132.5) | 1.69 | 954 | 567 |
| switch | 1 | 15.8 (15.2-16.4) | 11.1 (10.3-11.2) | 1.42 | 15234 | 2531 |
| switch | 16 | 30.9 (30.0-31.4) | 24.2 (24.1-24.4) | 1.28 | 1777 | 527 |
| switch | 128 | 140.0 (137.5-144.4) | 125.0 (123.1-126.9) | 1.12 | 803 | 552 |

Retained bytes per row at width 1 are dominated by the boundary's fixed cost, which the single row carries. Ranvier's revealed boundary retains roughly 1.4 to 1.7 times Solid 2's bytes per row at width 128 and more at narrower widths. Mounting a boundary inside a flush costs Ranvier 4.6 µs more than at the root at width 1 and is cheaper at widths 16 and 128 (the source is settled, so no second pass runs), and the ratio to Solid 2 there is 1.12 at width 128.
