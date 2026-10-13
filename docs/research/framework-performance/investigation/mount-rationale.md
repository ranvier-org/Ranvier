# Mount cost rationale

This accounts for the remaining 128-row mount gap after [owner lifetime](../../2026-10-13-owner-lifetime.md): Ranvier 886–893 µs against Solid 370 µs and Ripple 540 µs. Each cost is classified as buying a guarantee Solid or Ripple lacks, buying a design choice, or buying nothing.

## Behaviour probes

`benchmarks/frameworks/semantics.mjs` runs the same scenarios against all three frameworks in Chromium; [raw results](semantics-results.json).

| Scenario | Ranvier | Solid 1.9.17 | Ripple beta.9 |
|---|---|---|---|
| Caret: input node, focus and selection 2–2 kept after a reactive input write | kept | kept | kept |
| Row 3 of 4 throws from a property setter: host afterwards | `<span>keep</span>` | `<span>keep</span>` | `<span>keep</span>` |
| … row cleanups run | 3 | 0 | 0 |
| … reactive reads caused by a later source write | 0 | 3 | 3 |
| Row 3 of 4 throws from its title reader: mount | completes, row 3 title unset | throws | throws |
| … reactive reads caused by a later source write | 3, from a live mount | 2, with no mount to dispose | 2, with no mount to dispose |

Caret preservation is common to all three: each skips equal writes. A failed Solid or Ripple mount leaves the bindings created before the failure running for the life of the page, without a handle to dispose them. Ranvier disposes them and runs their cleanups. A throwing reader is a failed value in Ranvier, not a failed mount.

## Measured costs

`ladder-results.json` holds a re-run on owner-lifetime production, with two variant rungs compared against production. At 128 rows, µs per operation:

| Cost | Mount | Dispose | Solid | Ripple | Buys |
|---|---|---|---|---|---|
| Per-element `addEventListener`, owner-aware wrapper and cleanup registration | 118 (−121 / −72 / −56 per process) | 57 | delegates `onClick` | pays | nothing beyond delegation: the delegated variant keeps the owner check, batching and untracked handlers |
| Per-component core owner | 49 | 49 | absent: components are functions | pays (`Signal.root` per row) | component cleanup; cleanup and disposal after a failed mount |
| Setup checks | 28, mixed | — | absent | absent | rejection of view construction outside live setup |
| Attribute validation | not resolved (−62 / +57 / +18) | — | absent | absent | rejection of `on*` and `srcdoc` attribute names |
| Binding guards | not resolved | — | equal-write skip only | equal-write skip only | rollback on a failing setter; queued commits |
| Range ownership | 3 | — | absent | absent | shared hosts, movable mounts, borrowed native nodes |
| Reactive core | 124 | 14 | lighter nodes | lighter nodes | pending and failure values, async flights, `use`-order teardown |
| Backend dispatch and mount | 337 (+499 / +422 / +360) | 109 | compiled direct DOM code | direct functions, eager DOM | reusable, portable view values without a compiler |

## Dispatch profile

`owner-profile/` holds source-mapped CPU profiles of 256 mounts per framework and an allocation sample. Native DOM work is comparable: about 64 ms of `createElement`, `appendChild`, `setAttribute`, `insertBefore` and `createTextNode` for Ranvier against about 57 ms of `cloneNode`, `setAttribute`, `appendChild` and `insertBefore` for Solid. Element creation strategy is therefore not the gap.

Allocation is. Sampled JavaScript allocation per mount is 607 KiB for Ranvier, 375 KiB for Ripple, 177 KiB for Solid and 28 KiB for handwritten DOM; garbage collection takes 63, 31 and 23 ms of the respective profiled runs. Ranvier's largest allocation sites:

- Per-instance view definitions in the authoring fixture: 159 KiB. Each component instance rebuilds its modifier arrays and view closures.
- `startBinding`: 90 KiB, about 176 bytes per binding in closures and captured `FSharpRef` cells.
- Core nodes: 137 KiB, including effects, owner links and cleanup arrays.
- Listener wrappers: 66 KiB in `Typed.listener` and `Events_onClick`.

F# compilation artefacts appear as CPU self time without semantic purpose: `uncurry2` adapters at 15 ms, `FSharpRef` construction, structural `equals` for string option comparison, and `Seq` enumerators in modifier loops.

## Classification

- Buys a guarantee Solid and Ripple lack, and stays: per-component owners, setup checks, attribute validation, binding guards, range ownership and the reactive core's semantics. Together roughly 200–250 µs of the gap, more than half of it the reactive core.
- Buys a design choice: interpreting view values at runtime rather than compiling them. The interpretation itself is cheap; its cost is allocation from rebuilding definitions per instance.
- Buys nothing: per-element listeners, about 118 µs mount and 57 µs disposal, and the F# compilation artefacts and per-instance allocation inside dispatch, most of the 337 µs.

## Reproduction

From `examples/Fable.Ranvier.Playground`:

```powershell
rtk node benchmarks/frameworks/semantics.mjs --build
rtk node benchmarks/frameworks/ladder.mjs --build
rtk node benchmarks/frameworks/diagnose.mjs --build --out=owner-profile
rtk node benchmarks/frameworks/summarize-profiles.mjs --out=owner-profile
rtk node benchmarks/frameworks/allocations.mjs --out=owner-profile
```
