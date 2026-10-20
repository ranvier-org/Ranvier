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
