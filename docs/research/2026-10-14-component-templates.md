# Component templates

[Component templates](../superpowers/specs/2026-10-13-component-templates-design.md) build a component's view once per component type and materialise each instance as one skeleton clone, one state array, one render effect and one property write per event node. `Template.fs` holds the portable model: a backend-neutral template tree, a typed builder, `Component.define`/`render`/`renderWith`, an expansion into the existing `Operations`, and one markup writer shared by the DOM skeleton and server HTML. `TemplateDom.fs` compiles a definition once into a parsed skeleton, a walk program and slot tables; the mount backend's `Backend.Template` override materialises instances through it, and the delegated dispatcher finds template handlers on the node's state array. Other backends render templates through the default expansion.

The implementation plan fixes five points the spec left open:

- **Builder parameter.** Templates are written against a builder, `Component.define setup (fun t -> t.div [| ... |] [| ... |])`. The builder's type parameter fixes the instance type before F# resolves the overloaded part constructors; the spec's static `T.*` sketch cannot type its lambdas.
- **Static properties** are written once per instance during materialisation, not baked into the skeleton: a clone copies attributes, not property values, and server HTML omits properties.
- **`from` slots** evaluate inside the render effect until the fixed-dependency binding lands.
- **Queued commit mode** renders templates through the default expansion; the compiled path serves synchronous mounts, which the benchmark uses.
- **Component-ownership check.** The allocation sample of the template adapter replaces the spec's ladder re-run, because the ladder measures authored components and has no template rung.

The benchmark row is the existing framework row written as a template: a `div` with a computed `id`, a static class and a `title` from the shared source; an `input` whose `value` follows the source; a `button` whose `disabled` follows a shared memo, with a click handler and a text slot reading a per-row signal. The `ranvier-template` adapter is `FrameworkPerf.mountTemplate`, built on the same `mountCore` helper as the authored `ranvier` adapter.

## Measurements

All numbers below are from one run on this machine (AMD Ryzen 9 9900X, Chromium 153.0.8010.12) at commit `8762e5c`, measured against Solid 2.0.0-rc.14 compiled with `@solidjs/compiler`. The machine has slow phases: the comparisons are ratios between frameworks measured in the same processes; absolute values are not comparable with earlier notes. The previous figures, at `b934a13`, are from one run of the same benchmark.

The measured code includes binding reader-failure reporting: every synchronous binding and template effect wraps its reads in a `try`/`catch` and checks a failure flag on each run. It also includes the parse validation in `compile`, which runs once per definition.

[Framework results](framework-performance/investigation/template-framework-results.json) (`run.mjs --template-comparison`), three Chromium processes, all 45 correctness checks passing. At 128 rows, µs per operation, overall median with the per-process medians below it:

| | Ranvier (authored) | Ranvier template | Solid 2 | Ripple | React |
|---|---|---|---|---|---|
| Mount | 897 | 530 | 481 | 672 | 849 |
| | 880 / 859 / 1142 | 518 / 501 / 599 | 481 / 416 / 511 | 631 / 623 / 722 | 809 / 729 / 915 |
| Update | 167 | 166 | 155 | 166 | 375 |
| | 169 / 159 / 176 | 165 / 163 / 179 | 154 / 151 / 168 | 162 / 164 / 177 | 374 / 361 / 417 |
| Dispose | 143 | 67 | 50 | 85 | 135 |
| | 136 / 132 / 148 | 71 / 66 / 69 | 51 / 48 / 51 | 82 / 82 / 94 | 143 / 134 / 132 |
| Retained JS heap per mount | 345 KiB | 113 KiB | 115 KiB | 163 KiB | 189 KiB |

The template path against Solid 2 and Ripple at every width, µs per operation (overall median), at `b934a13` and at `8762e5c`:

| Phase, rows | Template at `b934a13` | × Solid 2 | × Ripple | Template at `8762e5c` | Solid 2 | Ripple | × Solid 2 | × Ripple |
|---|---|---|---|---|---|---|---|---|
| Mount, 1 | 15.4 | 1.85 | 1.61 | 8.1 | 7.6 | 8.4 | 1.07 | 0.97 |
| Mount, 16 | 81.1 | 1.46 | 1.00 | 61.6 | 57.0 | 71.9 | 1.08 | 0.86 |
| Mount, 128 | 625.5 | 1.21 | 0.71 | 529.8 | 480.8 | 671.6 | 1.10 | 0.79 |
| Update, 1 | 1.6 | 0.75 | 0.96 | 1.2 | 1.5 | 1.3 | 0.81 | 0.87 |
| Update, 16 | 21.9 | 1.04 | 0.97 | 22.3 | 21.5 | 22.3 | 1.04 | 1.00 |
| Update, 128 | 190.7 | 1.08 | 1.02 | 166.1 | 155.0 | 166.3 | 1.07 | 1.00 |
| Dispose, 1 | 4.3 | 2.07 | 3.26 | 2.3 | 1.5 | 0.7 | 1.53 | 3.05 |
| Dispose, 16 | 17.0 | 1.54 | 1.03 | 14.9 | 10.2 | 15.8 | 1.47 | 0.94 |
| Dispose, 128 | 74.4 | 1.43 | 0.79 | 67.3 | 49.8 | 84.8 | 1.35 | 0.79 |

Per process, the one-row mount is 1.21 / 1.22 / 0.98× Solid 2 and the one-row disposal 1.39 / 1.68 / 1.49×; the 128-row mount is 1.08 / 1.20 / 1.17×.

Against the authored path in the same processes, templates mount in 0.59× the time, dispose in 0.47× and retain 0.33× the heap; updates are 1.00×. Against Solid 2, the authored path mounts in 1.87×. Templates retain 0.98× Solid 2's heap per 128-row mount. The one-row heap measurement varies by more than its value between processes (Solid 2 measured −1.7, 3.2 and 2.0 KiB) and is not compared.

[Allocation sampling](framework-performance/investigation/template-profile/allocations.json), one browser process, 4096-byte interval, per 128-row mount:

| | Ranvier (authored) | Ranvier template | Solid 2 | Ripple |
|---|---|---|---|---|
| Allocated | 467 KiB | 145 KiB | 143 KiB | 368 KiB |

Templates allocate 1.02× Solid 2 (1.08× at `b934a13`) and 0.31× the authored path; the authored path allocates 3.27× Solid 2.

## CPU profile

### Mount at 128 rows

[The mount profile](framework-performance/investigation/template-mount-cpu-profile.json) (`diagnose.mjs --modes=ranvier-template,solid --phases=mount --roots=1024`, then mapped through the bundle's source map) samples one run of 1024 128-row mounts per framework at 100 µs, in one Chromium process. Busy time excludes idle and unattributed `(program)` samples.

At `5e69af2` the template mount was busy for 1167 ms against Solid 2's 722 ms. Ranked by self time:

| Cost | ms | Share | Solid 2 |
|---|---|---|---|
| `cloneNode` | 199 | 17.0% | 166 |
| Garbage collection | 173 | 14.9% | 109 |
| `input.value` writer (native setter) | 166 | 14.2% | inside the row's template function, 152 in total |
| `materialise` | 99 | 8.5% | |
| `appendChild` of each row into the section | 84 | 7.2% | 18 (`insertBefore`) |
| `insertBefore` of the section into the host | 64 | 5.4% | 48 (`appendChild`) |
| `collect` | 50 | 4.3% | |
| `setAttribute` | 47 | 4.1% | 54 |
| `SourceList.Add` (core) | 39 | 3.3% | |
| `write` | 31 | 2.6% | |
| `Array.fill` for the state array | 24 | 2.1% | |

Inclusive, the render effects took 358 ms (30.7%), of which 249 ms were slot writes. Two template-path costs exceeded Solid 2's equivalents. A row cloned from the skeleton fragment is still a fragment child: appending it costs 2.3× appending a cloned element, which a separate Chromium micro-measurement reproduced (about 56 against 33 µs per 128 rows). The bounds-checked `item`/`setItem` calls and the `fill` kept `materialise` at 99 ms of self time.

Two commits address them: a single-root template clones its root node and walks from it, appending the clone without a roots array; multi-root templates append the fragment's children directly (`51d0783`). State-array and slot-table accesses compile to plain indexing, and the state array is allocated without a fill pass (`19ea929`). After both, the template mount was busy for 686 ms against Solid 2's 679 ms in the same profiling session: `materialise` self time fell to 12 ms, `collect` to 36 ms, `fill` disappeared, and row insertion fell to 50 ms (`appendChild`) and 37 ms (`insertBefore`), against Solid 2's 46 and 25 ms.

Two variants were measured and rejected. `document.importNode` in place of `cloneNode` raised the mount ratio from about 1.3× to 1.56× in a five-process spot check. Hoisting the commit closure out of the render effect does not change the emitted JavaScript: Fable inlines a single-use lambda binding, including a `Func`, back into the effect body.

### Disposal and per-root costs

The remaining gap fell with width, so the cost was per mount root and, for disposal, per row. [The disposal and per-root profile](framework-performance/investigation/template-dispose-cpu-profile.json) (`diagnose.mjs --modes=ranvier-template,solid --phases=mount,dispose --repeat`, mapped through the bundle's source map) samples, at 100 µs in one Chromium process per width, 160 runs of 256 one-row roots and 96 runs of 32 128-row roots. A single profiled run of 1024 roots in one document measures document size instead: its disposal cost 285 µs per 128-row root, against about 50 µs in the benchmark's 32-root batches. Busy time counts the samples inside the timed runs, including the garbage collector; a category holds JavaScript self time by source, with native DOM time and `fable-library` time attributed to the nearest Ranvier or Solid caller.

At `9798fd2`, before the fixes:

| Phase, rows | Ranvier template busy | Solid 2 busy | Ratio |
|---|---|---|---|
| Mount, 1 (40960 roots) | 645.5 ms | 300.4 ms | 2.15 |
| Dispose, 1 (40960 roots) | 173.0 ms | 128.7 ms | 1.34 |
| Dispose, 128 (3072 roots) | 234.7 ms | 143.8 ms | 1.63 |

One-row mount, ranked by excess over Solid 2:

| Cost | Ranvier | Solid 2 |
|---|---|---|
| View-layer JavaScript, including `fable-library` | 249.6 ms (38.7%) | 96.0 ms (`@solidjs/web` and the compiled row) |
| Garbage collection | 108.3 ms (16.8%) | 30.7 ms |
| Native DOM work | 184.2 ms (28.5%) | 124.5 ms |
| Core (`src/Ranvier`): effect creation and first run | 58.2 ms (9.0%) | 49.2 ms |

Within the view layer: each mount evaluated an object expression, so Fable emitted a new backend class per mount; its construction took `Prepared.Backend` 23.3 ms and part of `mountCore`'s 26.4 ms of self time, and the fixture's view, whose backend calls met a different class on every mount, took 31.6 ms of self time (1.5 ms at `8762e5c`). `OwnedRange.Attach` took 84.9 ms inclusive: range validation through `Option` closures and enumerators (`intactAt` 29.2 ms, `ValidateDestination` 10.4 ms) and the insertion of the staging fragment (46.3 ms; Solid 2's `appendChild` and `insertBefore` together took 27.1 ms). The per-host delegation helper allocated a `Map`, a listener closure and an immediately invoked arrow per host (25.5 ms inclusive with `addEventListener`).

One-row disposal: `remove` of the two range comments and the root took 35.1 ms, which matches Solid 2's `textContent` clear (34.9 ms). The rest was view-layer bookkeeping: `unclaim` 14.4 ms, two calls per single-node entry; `Scope` disposal through enumerators, a shared `ref` cell and a failure list built on success (about 37 ms); delegation release 22.0 ms. Core owner teardown took 32.0 ms against Solid 2's 15.1 ms.

128-row disposal: core owner teardown took 118.6 ms (50.5%) against Solid 2's 44.6 ms (31.0%); native removal matched (95.7 ms `remove` against 94.7 ms `textContent`); the view layer took 12.6 ms.

Eight commits change the view layer, each with both playground suites passing:

- `3d43e1c`: owned ranges walk their entries with index loops and plain `parentNode`/`nextSibling` reads, share one no-op callback, unclaim a single-node entry once, and add the mount's own created roots without the native-ownership overlap checks.
- `3cc4812`: scope disposal uses index loops and builds its failure list only on failure.
- `10afa06`: the mount backend is one `MountBackend` class that forwards to a record of per-mount delegates. The emitted code keeps the one `curry2` adapter the `Browser` member already had.
- `e3ddb17`: element modifiers apply through an index loop instead of an enumerator.
- `0f2b455`: a scope tree keeps its disposal order on its topmost scope instead of a `ref` cell per root.
- `9535212`: one delegated listener per event name serves every mount host; the host itself carries its mount count, and the listener reads its host from `this`.
- `0bf14ff`: marking child scopes attached uses an index loop.
- `8762e5c`: a mount's created roots stay detached during setup and attach one by one; the staging fragment is used once a keyed region, a native node or a root that arrives in a cloned fragment needs a shared parent. In a separate Chromium measurement, attaching two comments and a row this way took 1.60 µs against 1.75 µs through a fragment.

At `8762e5c`:

| Phase, rows | Ranvier template busy | Solid 2 busy | Ratio |
|---|---|---|---|
| Mount, 1 | 431.5 ms | 323.9 ms | 1.33 |
| Dispose, 1 | 160.3 ms | 118.8 ms | 1.35 |
| Dispose, 128 | 235.6 ms | 176.1 ms | 1.34 |

The one-row mount's garbage collection fell from 108.3 to 63.6 ms (Solid 2: 33.8 ms) and its view-layer JavaScript from 249.6 to 142.7 ms (Solid 2: 105.4 ms). Profiled ratios move between sessions on this machine: the 128-row disposal ratio changed from 1.63 to 1.34 with no change to its per-row path. The benchmark tables above give the before and after ratios.

### Core costs

These costs are in `src/Ranvier` and are left unchanged:

| Cost | Ranvier at `8762e5c` | Solid 2 |
|---|---|---|
| Per-row effect disposal at 128 rows: `Owner.TearDown` → `Effect.Dispose` → `SourceList.Clear`/`trimFrom` → `RemoveObserver` → `ObserverSet.Remove` | 116.8 ms of 235.6 (49.6%), about 0.30 µs per row; `ObserverSet.Remove` 54.9 ms self, `SourceList.trimFrom` 38.6 ms self | `disposeChildren` → `clearDeps` → `unlinkSubs`: 61.1 ms (34.7%), about 0.16 µs per row |
| One-row owner teardown | 37.7 ms of 160.3 (23.5%) | `disposeRootSelf` 25.0 ms (21.0%) |
| Dependency edges at a 128-row mount: `SourceList.Add` with its growth `fill`, `ObserverSet.Add`, `positionOf` and its growth `fill` | about 97 ms of 1773.5 (5.5%) | `link` 13.5 ms |

An `ObserverSet` above eight observers keeps a `Map` index; every removal deletes the observer from the index and moves the last observer into the hole, which writes the index again. The shared source and memo of the benchmark have 128 observers per mount, so each row's disposal pays two `Map` writes per shared edge. Solid 2 keeps each edge in doubly linked lists and unlinks it with a constant number of pointer writes. A swap-remove without the index, an index-free removal while the whole owner is being disposed, or linked edges would remove most of this cost; each is a core change.

## Spec targets

- **Mount within 1.3× Solid 2: met.** The 128-row template mount is 1.10× Solid 2 (529.8 against 480.8 µs); each process measured between 1.08× and 1.20×.
- **Allocation within 1.5× Solid 2: met.** The 128-row template mount allocates 1.02× Solid 2 (145.1 against 142.6 KiB).

## Next costs

Disposal at 16 and 128 rows is now dominated by the core edge removal above. The remaining view-layer costs, from the one-row profile at `8762e5c` and a 128-byte-interval allocation sample of 4096 one-row mounts (5.8 KiB per Ranvier mount against 3.9 KiB for Solid 2):

| Cost | Size |
|---|---|
| `mountCore` creates about 45 closures per mount: its local helpers, the 19 backend delegates and the `Mounted` callbacks | 1.5 KiB of the 5.8 KiB per one-row mount; a class holding the mount state would replace them with methods |
| Range boundary comments: two `createComment` calls, three `WeakSet` claims and two extra `remove` calls per mount | `createComment` 6.8 ms, `claim` 13.2 ms and `unclaim` 9.4 ms in the one-row profiles; Solid 2 owns its host and clears it with `textContent` |
| The template services record, created on a mount's first template | 10 closures, 0.24 KiB per mount |
| `collect` and `write` per row | 94 and 68 ms of the 128-row mount profile |

The render effect still allocates an `untrack` arrow per run; it recurs on updates, not on mount. Removing it needs a value Fable does not inline, or an untracked entry point in core that takes no closure.

The fixture gives every Ranvier mount its own `Graph`; its construction and activation (`Graph`, `Graph.Activate`) allocate about 0.4 KiB per mount. Solid 2's roots share one scheduler.

## Acceptance

The playground suites pass 249 Node tests and 22 Chromium tests; the 7 portable .NET representation checks print `PASS`, and the 8 pure lifetime checks pass.
