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

All numbers below are from one run on this machine (AMD Ryzen 9 9900X, Chromium 153.0.8010.12) at commit `b934a13`, measured against Solid 2.0.0-rc.14 compiled with `@solidjs/compiler`. The machine has had slow phases, and the second process of this run was one: the comparisons are ratios between frameworks measured in the same processes; absolute values are not comparable with earlier notes.

The measured code includes binding reader-failure reporting: every synchronous binding and template effect wraps its reads in a `try`/`catch` and checks a failure flag on each run. It also includes the parse validation in `compile`, which runs once per definition.

[Framework results](framework-performance/investigation/template-framework-results.json) (`run.mjs --template-comparison`), three Chromium processes, all 45 correctness checks passing. At 128 rows, µs per operation, overall median with the per-process medians below it:

| | Ranvier (authored) | Ranvier template | Solid 2 | Ripple | React |
|---|---|---|---|---|---|
| Mount | 1145 | 625 | 516 | 883 | 1003 |
| | 1145 / 2013 / 945 | 591 / 944 / 571 | 490 / 724 / 472 | 883 / 1163 / 678 | 1005 / 1154 / 807 |
| Update | 186 | 191 | 177 | 186 | 424 |
| | 181 / 198 / 181 | 184 / 216 / 191 | 172 / 200 / 168 | 188 / 201 / 182 | 425 / 469 / 397 |
| Dispose | 168 | 74 | 52 | 94 | 146 |
| | 174 / 164 / 167 | 80 / 69 / 73 | 59 / 45 / 52 | 100 / 90 / 84 | 152 / 153 / 139 |
| Retained JS heap per mount | 343 KiB | 112 KiB | 115 KiB | 163 KiB | 189 KiB |

Against the authored path in the same processes, templates mount in 0.55× the time, dispose in 0.44× and retain 0.33× the heap; updates are 1.03×. Against Solid 2, template mounting is 1.21× (1.21 / 1.30 / 1.21 per process), down from 1.42× at `5e69af2`; the authored path is 2.22×. Disposal is 1.43×, updates 1.08×. Template mounting is 0.71× Ripple. Templates retain 0.97× Solid 2's heap per mount.

[Allocation sampling](framework-performance/investigation/template-profile/allocations.json), one browser process, 4096-byte interval, per 128-row mount:

| | Ranvier (authored) | Ranvier template | Solid 2 | Ripple |
|---|---|---|---|---|
| Allocated | 513 KiB | 149 KiB | 138 KiB | 373 KiB |

Templates allocate 1.08× Solid 2 and 0.29× the authored path; the authored path allocates 3.73× Solid 2.

## CPU profile

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

## Spec targets

- **Mount within 1.3× Solid 2: met.** The 128-row template mount is 1.21× Solid 2 (625 against 516 µs); each process measured between 1.21× and 1.30×.
- **Allocation within 1.5× Solid 2: met.** The 128-row template mount allocates 1.08× Solid 2 (149.0 against 137.8 KiB), within the spec's bound of 1.5 × 137.8 = 206.7 KiB.

## Next costs

After the two commits, the template mount's self time is dominated by native DOM work that Solid 2 also pays: `cloneNode` 135 ms, the `input.value` setter 125 ms, `appendChild` and `insertBefore` 86 ms, `setAttribute` 37 ms. The remaining Ranvier-side costs:

| Cost | Profile share after `19ea929` |
|---|---|
| Core effect creation and first run outside the slot reads and writes: the `Effect` constructor, `RunFlush`, `RunBody`, `Graph.Ambient` (`src/Ranvier`) | about 61 ms inclusive, 8.8% |
| `SourceList.Add` and `ObserverSet.Add` for each slot read (`src/Ranvier`) | 29 ms inclusive, 4.2% |
| `collect`, the DOM walk to the dynamic nodes | 36 ms, 5.2% |
| `write`, the slot-target dispatch including the text node's `data` setter | 26 ms, 3.7% |

The render effect still allocates an `untrack` arrow per run; it recurs on updates, not on mount. Removing it needs a value Fable does not inline, or an untracked entry point in core that takes no closure.

The largest self-allocating functions per 128-row mount: `materialise` 43.5 KiB, `SourceList.Add` 18.9 KiB, the `Effect` constructor 16.6 KiB, runtime `set` 10.9 KiB, the fixture's `Array.init` 9.0 KiB, the per-row `Local` signal 7.1 KiB, `ObserverSet.Add` 7.0 KiB.

## Acceptance

The playground suites pass 244 Node tests and 22 Chromium tests; the 7 portable .NET representation checks print `PASS`.
