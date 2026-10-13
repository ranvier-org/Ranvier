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

All numbers below are from one run on this machine (AMD Ryzen 9 9900X, Chromium 153.0.8010.12), measured against Solid 2.0.0-rc.14 compiled with `@solidjs/compiler`. The machine has had slow phases, so the comparisons are ratios between frameworks measured in the same processes; absolute values are not comparable with earlier notes.

[Framework results](framework-performance/investigation/template-framework-results.json) (`run.mjs --template-comparison`), three Chromium processes, all 45 correctness checks passing. At 128 rows, µs per operation, overall median with the per-process medians below it:

| | Ranvier (authored) | Ranvier template | Solid 2 | Ripple | React |
|---|---|---|---|---|---|
| Mount | 897 | 672 | 454 | 653 | 823 |
| | 940 / 892 / 959 | 678 / 673 / 626 | 462 / 454 / 437 | 735 / 652 / 641 | 843 / 793 / 820 |
| Update | 176 | 182 | 165 | 173 | 395 |
| | 199 / 155 / 173 | 186 / 180 / 178 | 172 / 161 / 160 | 179 / 173 / 163 | 403 / 363 / 391 |
| Dispose | 143 | 64 | 48 | 80 | 137 |
| | 150 / 151 / 119 | 66 / 54 / 63 | 47 / 48 / 49 | 87 / 66 / 81 | 137 / 136 / 142 |
| Retained JS heap per mount | 341 KiB | 109 KiB | 115 KiB | 163 KiB | 189 KiB |

Against the authored path in the same processes, templates mount in 0.75× the time, dispose in 0.45× and retain 0.32× the heap; updates are equal within noise (1.04×). Against Solid 2, template mounting is 1.48× (1.47 / 1.48 / 1.43 per process), down from 1.98× for the authored path; disposal is 1.35×, down from 2.99×; updates are 1.10×. Template mounting is 1.03× Ripple. Templates retain 0.95× Solid 2's heap per mount.

[Allocation sampling](framework-performance/investigation/template-profile/allocations.json), one browser process, 4096-byte interval, per 128-row mount:

| | Ranvier (authored) | Ranvier template | Solid 2 | Ripple |
|---|---|---|---|---|
| Allocated | 501 KiB | 157 KiB | 137 KiB | 370 KiB |

Templates allocate 1.15× Solid 2 and 0.31× the authored path.

## Spec targets

- **Mount within 1.3× Solid 2: not met.** The 128-row template mount is 1.48× Solid 2 (672 against 454 µs); each process measured between 1.43× and 1.48×.
- **Allocation within 1.5× Solid 2: met.** The 128-row template mount allocates 1.15× Solid 2 (157 against 137 KiB), within the spec's 210 KiB bound.

## Next costs

The largest self-allocating functions in the template profile, per 128-row mount: `materialise` 47.1 KiB (377 B per instance), `SourceList.Add` 18.5 KiB, the `Effect` constructor 18.0 KiB (one render effect per instance), runtime `set` 10.6 KiB, the fixture's `Array.init` 9.0 KiB (the per-row `Component.render` views), `collect` 7.9 KiB (DOM wrappers created by the walk), the per-row `Local` signal 7.5 KiB.

Four costs in the materialise path were logged during review and remain in this measurement:

| Cost | What the profile shows |
|---|---|
| The render effect allocates an `untrack` arrow per run: Fable inlines `commitAll` into the effect body as an arrow | The effect body's self-allocation is 3.9 KiB per mount, 31 B per instance, one closure per run. It recurs on every update. |
| Every state-array access emits Fable's bounds-checked `item`/`setItem` | CPU only; the allocation profile does not attribute it. A CPU profile is needed to size it. |
| `Array.zeroCreate` emits a `fill` and a second loop writes the unset sentinel | CPU only: the state array itself is required. This row's state array has 18 entries (4 header, 4 nodes, 5 previous values, 5 pending values), about 96 B per instance and 12 KiB per mount, inside `materialise`'s 47.1 KiB. |
| A per-instance roots array | A one-element array, about 32 B per instance and 4 KiB per mount by object-layout estimate, inside `materialise`'s 47.1 KiB; the sampler does not separate it. |

The allocation target is met with 53 KiB of headroom, so the mount gap to Solid 2 is CPU time rather than allocation volume. The next round needs a CPU profile of the template mount to rank the bounds checks, the double initialisation and the walk against the reactive-graph costs (`SourceList.Add`, `Effect`, observer sets).

## Acceptance

The playground suites pass 232 Node tests (unchanged; this task adds a benchmark adapter, not tests) and 22 Chromium tests; the 7 portable .NET representation checks print `PASS`.
