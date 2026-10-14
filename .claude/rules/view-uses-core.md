## The view builds on the reactive core

The view renders what the core computes. Ownership, disposal, keyed identity, change detection
and scheduling belong to the core (`src/Ranvier`). The view uses the core's primitives for them,
the way Solid's `<For>` uses `mapArray`:

- Lists and keyed collections: `createProjection` / `createProjectionWith` /
  `createIndexProjection`. The view lays out the rows' nodes in the projection's order.
- Arm and branch selection: a memo with a comparer (`createMemoWithComparer`,
  `createOptionMemo`), with an owner per selected arm.
- Ownership and cleanup: `Owner`, `createRoot`, `onCleanup`, `runWithOwner`.
- Skipping unchanged values: the comparer overloads (`createEffectOnWithComparer`,
  `createMemoWithComparer`).
- Scheduling: `batch`, `flush` and the graph's dispatcher.

The view owns only state with no core equivalent: DOM nodes and their layout, template tables,
event delegation, ownership claims on native nodes, attribute and property setters, and server
HTML. The last value written to a DOM target is DOM state, so the view keeps it.

A view-side ownership tree, key matcher, reconciler, selection cache, commit queue or other state
manager that duplicates a core primitive requires agreement with the user before it is written.
Raise it as a question: name the core primitive, and the behaviour it lacks. When the core lacks
a behaviour, the default proposal is to add it to the core.

Existing duplicates, agreed for replacement on 2026-10-14: the keyed region reconciler
(`Mounting.fs` `KeyedRegion`, `Keyed.validateUniqueKeys`), arm switches routed through it, and the
renderer `Scope` tree (`Lifetime.fs`). Under review: the queued commit `Queue` (`Scheduling.fs`).
