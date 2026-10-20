# Motion seam: placed-hook kinds, departures and reflow

Date: 2026-10-21. Steps 3–7 of `docs/superpowers/specs/2026-10-21-motion-design.md`: the `Motion`, `Exit` and `Reflow` placed-hook kinds, the placement context, held exits (`Departures.fs`) and reflow measurement in region passes. No anime.js code is involved; the tests drive the hooks with plain callbacks (`examples/Fable.Ranvier.Playground/tests/DepartureChecks.fs`).

## Where the registry lives

`Departures.fs` is in `experiments/Ranvier.View.Portable` (after `Typed.fs`), not in the Foundation as the spec places it. The mount paths reach the registry only through `Departures.seam`, a plain object whose `Enabled` and `ReflowEnabled` flags are unset and whose operations (`Retire`, `NextLive`, `AnyExit`, `AnyLeaving`, `Before`, `After`) are null until the first registration. The registrations are called from the payloads of `HookBuilder.exit` and `HookBuilder.reflow`, so a bundle that never uses either hook holds the seam object and its call sites, and none of the registry. `HookBuilder` is Portable code, so the registry it calls is Portable code too; on .NET the seam is null and never read.

Each placed hook is registered with its container and runs once the container is placed, whatever its kind. The runtime passes a motion hook the flags of its context (1 hydrated, 2 inserted), which `HookBuilder.motion` turns into a `MotionContext`; an exit hook's payload registers its exit and returns no cleanup; a reflow hook's payload registers its measurement and returns the withdrawal as its cleanup.

## Two paths carry template rows

A sugar template mounts through one of two paths, chosen by the mount's commit mode:

- **Synchronous commits:** `TemplateDom.materialise`. List rows and arms are template rows, built by `RowBuilder` into the region's staging fragment and removed by `Row.Remove`.
- **Queued commits:** `TemplateComponent.Expand` over the mount's `Backend`. List rows and arms are nested mounts (`MountRow`), and their roots leave through the nested mount's `OwnedRange.Release`.

The spec places exits on template rows only. Under queued commits a sugar template has no template rows, so the seam also covers the expanded path: `MountState.Placed` dispatches the three kinds as `TemplateDom.bind` does, and a list row's or arm's nested mount retires its roots into the enclosing mount's held list on release. Boundary views and view holes do not hold their roots (`MountRow ... departs = false`).

## Sibling-walk audit (Risk 1)

Every sibling walk, bulk removal and boundary placement in `experiments/Ranvier.View.Foundation`, with the case that covers it or the ruling that leaves it unchanged. Case names are those of `DepartureChecks.cases`, each run under synchronous and queued commits unless marked.

| Site | What it walks | Held nodes | Coverage |
|---|---|---|---|
| `Row.ValidateWindow` start cursor and row stepping | region contents from `regionStart` or the previous row | skipped with `Departures.seam.NextLive` once an exit is registered | "reorders and inserts validate while roots are held", "a key re-added while its old root leaves gets a new row", "moving the mount carries its held roots" |
| `Row.Remove` | a row's roots, `First` to `Last` | the retire point: each root is offered to `Departures.seam.Retire` | "an exit holds its row's root until finish" |
| `OwnedRange.Release` (nested list-row and arm mounts) | the range's entries | the retire point on the expanded path | queued variants of every exit case |
| `RegionSite.FlushReleased` cleared path → `Row.ClearBetween` → `deleteBetween` / `clearChildren` | everything between the markers | taken only when no released root has an exit and no node between the markers is leaving | "clearing a list with exits holds every root" |
| `Row.MoveBefore` | a live row's roots | none: a held node never sits between a live row's `First` and `Last`; rows are inserted before a live row's `First` or `regionEnd` | ruling 1 |
| `Row.Layout` anchors and staged insert | rows of `next`, anchored at `next[bEnd].First` or `regionEnd` | stay where they are; rows move around them | "reorders and inserts validate while roots are held" |
| `ArmRegion` `Row.MoveBefore (row, parent, regionEnd)` | the new arm's roots | the old arm's held root stays before the new arm | "an arm switch leaves the old and new arm roots in order" |
| `OwnedRange.intactAt`, `ValidateDestination`, `Move` | entries and region spans | inside a region span, walked or moved with it | "moving the mount carries its held roots" |
| `MountState.release` | — | ends every held exit (cancel, then remove) before the range is released | "disposing the mount cancels and removes every held root" |
| `clearAdopted`, `positionAfter`, `RowBuilder` separator and row-marker adoption, `TemplateDom.logicalNext` | server nodes at the hydration cursor | none: they run in a region's hydrating first pass, before any row of the region can retire | ruling 2; "a hydrated row's root with an exit is held when its row is retired" (synchronous) covers retirement after hydration |
| `ViewBoundary` `copyServed`, `clearServed` → `Row.ClearBetween`, `place` → `fillsFragment` | the boundary's markers and its view rows | none: boundary views do not hold roots, and a list inside a view keeps its held roots inside the view's own nodes | ruling 3 |
| `HoleRegion` `Row.MoveBefore` | the hole's view row | none: holes do not hold roots | ruling 3 |
| `TemplateDom.materialise` root and region-marker walks | a fresh clone | none | not applicable |

Rulings:

1. Ruling: `Row.MoveBefore` keeps its plain `nextOrNull` walk — a held root is outside every live row's `First`..`Last` span, because retirement removes the row from the laid-out rows before any later layout — cost if wrong: a move would carry a held root with a live row, which the next validation would report as external mutation.
2. Ruling: hydration walks keep `nextOrNull` — they run only inside a region's hydrating first pass, and a held root exists only after a later pass retires a row — cost if wrong: a recovered hydration mismatch would delete a leaving root early (its exit's `finish` then does nothing).
3. Ruling: boundary views and view holes do not hold their roots — the spec excludes exits on boundary content, and holding a view root between boundary markers would break `clearServed` — cost if wrong: an exit on a boundary view root ends with the view instead of animating.

## Rulings

- Ruling: `MotionContext.Hydrated` is read from the services' hydration cursor at bind time (template path) or the mount's cursor (expanded path), not from `root` — a non-root element of an adopted row is also hydrated, and a row created after hydration in a hydrated mount is not — cost if wrong: `Motion.appear` would play or skip on the wrong rows.
- Ruling: an exit registers when its container is placed, like every placed hook — the runtime then needs no exit-specific dispatch, which keeps the registry out of bundles without exits — cost if wrong: a row retired before its hooks run leaves at once, with no exit.
- Ruling: the Debug root check ("An exit hook requires a row or arm root element") runs when the instance binds, while a template row's roots are still children of the staging fragment; it is exact on the template path and permissive on the expanded path, where an element receives its modifiers before it is appended — cost if wrong: a misplaced exit under queued commits is silently inert (non-root exits are never retired).
- Ruling: `Departures.fs` lives in the Portable project, beside `HookBuilder`, instead of the Foundation — only a registry reached from the hook payloads stays out of bundles that do not use the hooks — cost if wrong: none at run time; the file moves with the view either way.
- Ruling: a forced removal (the mount, or an enclosing mount, tears down) passes no held list, and the node's exit entry stays in the `WeakMap` until the node is collected — cost if wrong: none; a removed node is never retired again.
- Ruling: a root held inside a row that is then removed leaves with that row; its exit keeps running until `finish` or the safety timeout, and `finish` then does nothing visible — tracking nested held roots per row would add state to every row — cost if wrong: an exit callback runs for up to 10 s on a detached node.
- Ruling: an exit that raises finishes at once (its node leaves) and the error is raised through the row release, which reports it with the pass — cost if wrong: none beyond the report.
- Ruling: `MountState.leaving` holds each held root's end action (cancel, then remove) instead of `Node * cancel` tuples — removal by reference needs an identity, and F# tuples compare structurally under Fable — cost if wrong: none.

## Seam gate (step 7)

Baseline: `268c357` (master `642d889` plus the spec), measured in a detached worktree of that commit on the same machine (AMD Ryzen 9 9900X, Node 26.7.0, Chromium 153.0.8010.12, Fable 5.18.0).

### Bundle delta

`benchmarks/frameworks/sugar-size.mjs` (rolldown, minified, gzip level 9), the explicit entry against the baseline:

| Entry | Baseline raw / gzip / brotli | Seam raw / gzip / brotli | Delta gzip |
|---|---|---|---|
| explicit | 212381 / 61180 / 53654 | 214068 / 61767 / 54151 | **+587** |
| explicit-combined | 213763 / 61361 / 53756 | 215451 / 61950 / 54296 | +589 |
| sugared | 216416 / 62020 / 54249 | 218104 / 62592 / 54784 | +572 |

The budget is ≤ 150 B gzip; the seam is over it. The first implementation, with the registry in the Foundation and called directly from the mount paths, measured +1236 B. Moving the registry behind `Departures.seam` (see above), registering every kind through one container path and folding `forced` into a null held list brought it to +587 B. Two removal experiments on the final code size the remaining parts:

| Removed from the final code | Explicit delta gzip |
|---|---|
| nothing | +587 |
| exits on the expanded path (`OwnedRange.Release` retire, the nested mount's held list) | +543 |
| that, and reflow measurement in list and arm passes (`MeasureBefore`, `MeasureAfter`, their call sites) | +421 |

The +421 B floor is the code every bundle runs on each region pass or row removal once a hook might exist: three `PlacedKind` cases and the part-name change in server code, the motion-flag argument, `Placement.Inserted` and its pass copy, the `nextRowNode` stepping in `Row.ValidateWindow`, the retire test in `Row.Remove`, the cleared-path guard, the held list and its release in `MountState`, and the seam object.

### Keyed rows

`keyed-rows.mjs --build`, 3 processes × 15 rounds, per-process medians (ms). Every operation's range overlaps between baseline and seam except `clear10k` for `ranvier-sugar`, where the seam is faster (baseline 7.03–9.69, seam 6.28–6.48). No measurable regression, so the Risk 4 fallback (a separate `Part` case for the new kinds) is not applied.

| Operation | ranvier baseline → seam | ranvier-sugar baseline → seam |
|---|---|---|
| create1k | 6.50 [6.36–6.93] → 6.83 [6.22–7.42] | 6.58 [6.50–7.19] → 6.79 [6.37–7.44] |
| create10k | 53.60 [53.02–54.28] → 53.07 [52.24–56.71] | 53.71 [52.83–54.48] → 52.88 [52.67–54.12] |
| replace1k | 5.79 [5.58–5.83] → 5.58 [5.34–6.26] | 5.58 [5.49–5.81] → 5.63 [5.34–6.34] |
| update10th1k | 0.065 [0.065–0.070] → 0.065 [0.055–0.075] | 0.065 [0.060–0.070] → 0.065 [0.065–0.075] |
| swap1k | 0.140 [0.135–0.145] → 0.150 [0.145–0.165] | 0.155 [0.150–0.155] → 0.150 [0.135–0.175] |
| remove1k | 0.155 [0.140–0.170] → 0.170 [0.160–0.205] | 0.150 [0.145–0.175] → 0.165 [0.160–0.190] |
| remove10k | 0.655 [0.655–0.660] → 0.645 [0.635–0.655] | 0.630 [0.615–0.640] → 0.640 [0.630–0.665] |
| clear1k | 0.965 [0.925–0.995] → 0.990 [0.940–1.005] | 0.965 [0.935–1.015] → 0.970 [0.955–1.030] |
| clear10k | 6.89 [6.57–7.95] → 6.82 [6.42–7.24] | 9.12 [7.03–9.69] → 6.34 [6.28–6.48] |
| append1k | 3.90 [3.77–3.93] → 3.89 [3.82–3.97] | 3.66 [3.64–3.79] → 3.63 [3.56–3.81] |
| armsCreate1k | 11.63 [11.16–12.38] → 11.33 [11.30–11.56] | 11.86 [11.54–12.12] → 11.42 [11.26–11.99] |
| armsSwitch1k | 5.67 [5.65–5.84] → 5.54 [5.43–5.73] | 5.66 [5.40–5.88] → 5.43 [5.25–5.57] |
| armsPayload1k | 0.785 [0.770–0.805] → 0.790 [0.775–0.805] | 0.805 [0.770–0.870] → 0.795 [0.780–0.825] |
| armsClear1k | 1.89 [1.66–2.00] → 1.73 [1.66–1.88] | 1.86 [1.66–2.06] → 1.68 [1.63–1.81] |

### Template comparison

`run.mjs --template-comparison`, run five times in the order baseline, seam, seam, baseline, seam (3 processes each). In the first pair alone, 10 of the 45 Ranvier cells had disjoint ranges, and Solid, Ripple and React, which the seam does not touch, drifted by the same 5–10 % on update and dispose. Pooled over all runs (6 baseline and 9 seam process medians per cell), every one of the 45 Ranvier cells (widths 1, 16, 128 × mount, update, dispose × five Ranvier modes) overlaps; median changes lie within −8.2 % to +8.7 %, with the template modes within −1.7 % to +5.6 %. The per-cell table is in the job's `motion-seam/template-pooled.txt`.

### Counters

`generated-tests-counters` (core counters and `RANVIER_VIEW_COUNTERS`) after one script of keyed-list operations (create 1000, update every 10th, swap, remove, append 100, clear, create 200, dispose) and arm-list operations (create 200, switch, payload, clear, dispose), with no motion, exit or reflow hook. The baseline and the seam give identical counts:

```
core: SignalsCreated 3410, MemosCreated 400, EffectsCreated 2102, OwnersCreated 2308, EdgesAdded 2304, EdgesRemoved 2304,
      ObserverInserts 2304, ObserverRemoves 2304, MemoRecomputes 1000, EffectRuns 2810, Flushes 13, FactoryRowsCreated 0,
      OnceRowsCreated 1500
view: ListCounters lookups 1502, validatedRows 210, increasingRuns 0; PlacementCounters held 0; BoundaryCounters wakes 0
```
