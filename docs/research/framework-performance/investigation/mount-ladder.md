# Mount cost ladder

This follows the prepared-template checkpoint at `f64a227`. The ladder attributes production mount time to individual renderer setup steps. Four compile-time gates in `Mounting.fs` replace one step each with its naive equivalent. Without defines, the emitted `Mounting.js` is byte-identical to the ungated source.

| Step | Production | Naive replacement (`LADDER_NAIVE_*`) |
|---|---|---|
| Setup checks | `ensureSetup` around modifiers and appends, active-element check, `validateAttribute` | empty `ensureSetup`; attribute validation skipped (`CHECKS`) |
| Range ownership | `range.Add`/`AppendGenerated`: entries, WeakSet claims, descendant list; attach validation | `appendChild`; top-level roots recorded and removed once (`RANGE`) |
| Component ownership | child `Scope`, `createRoot`, two cleanup links, `untrack` | setup runs inline in the mount scope (`COMPONENTS`) |
| Binding guards | `startBinding`: committed value, comparer, liveness, untracked write, setup failure capture | `createEffect (fun () -> write (read ()))` (`BINDINGS`) |

Each rung adds one step to the previous rung. R0 defines all four symbols; R4 defines none and is production. Step costs therefore sum exactly to R4 minus R0. A step's cost depends on its position in this order; gates interact, and a different order would assign shared costs differently.

## Floor split

Below R0, the floor splits along adapters measured on the R0 page, each adding one layer to the previous:

| Adapter | Adds |
|---|---|
| `native-create` | handwritten DOM construction, listeners, no reactivity |
| `reactive-native` | graph, a local signal per row, one bare effect per binding, one shared root; no renderer |
| `ranvier-direct` | gated backend through direct `Backend` calls: mount scope, range staging and attach, modifier closures, typed listeners with cleanup registration |
| `ranvier` | public authoring: `Program.View` closures, overloaded helpers, modifier arrays |

`native-create` and `reactive-native` bypass the gated renderer and are identical in every bundle, so they also bound page-to-page variance.

## Validation

Every rung passes the framework contract for all four adapters at widths 1, 16 and 128 in all three processes: 180 checks. The ungated full playground suite passes 205 Node tests and 22 Chromium tests. Naive rungs are not run against the full suite; they deliberately omit ownership guarantees.

## Measurements

`ladder-results.json` contains one bundle per rung in three independent Chromium processes, three warmups and eleven measured rounds, with rung and adapter order rotated each round. At 128 rows, the control adapters range 409–431 µs (`native-create`) and 510–547 µs (`reactive-native`) pooled across rungs, and 355–461 µs per process. Pooled deltas below roughly 40 µs, and single-process deltas below roughly 100 µs, are not resolved. Absolute levels also shift between runs: an earlier run of the same gates measured R0 at 774 µs and R4 at 1,044 µs.

At 128 rows, public authoring path, µs per mount:

| Layer or step | Δ µs | Share of R4 | Process deltas |
|---|---|---|---|
| Native DOM (`native-create`) | 419 | 38.7% | 435 / 415 / 411 |
| + reactive core | 112 | 10.3% | 104 / 125 / 91 |
| + backend dispatch and mount | 260 | 24.0% | 251 / 238 / 307 |
| + authoring layer | 82 | 7.5% | 125 / 129 / 29 |
| = R0 floor | 872 | | 915 / 906 / 837 |
| + setup checks | 26 | 2.4% | 20 / −38 / 69 |
| + range ownership | 115 | 10.6% | 79 / 5 / 110 |
| + component ownership | 16 | 1.5% | 172 / 124 / −14 |
| + binding guards | 56 | 5.1% | −73 / −7 / 65 |
| = R4 production | 1,084 | | 1,112 / 991 / 1,067 |

Backend dispatch and mount is the largest attributable layer, about 2 µs per row, and is resolved in every process. The reactive core is also consistent, about 0.9 µs per row for one signal and four effects. Range ownership is the largest gated step in both runs: 127 then 115 µs through public authoring, and 103 then 139 µs through direct calls. Setup checks, component ownership and binding guards remain within noise in at least one process in each run. The `ranvier-direct` gated deltas are 111, 139, 12 and 42 µs.

Renderer-specific work above the reactive core totals 555 µs of the 1,084: dispatch 260, authoring 82 and the four gated steps 213. Ripple's complete mount at 128 rows was 556 µs in the prepared comparison.

At 16 rows every gated step is within noise; total production is 114 µs against a 48 µs native mount.

## Reproduction

From `examples/Fable.Ranvier.Playground`:

```powershell
rtk node benchmarks/frameworks/ladder.mjs --build
```

`--build` compiles each rung's Fable output into the ignored `generated-ladder/<rung>` directory and bundles it into `generated-ladder-dist/<rung>`; Vite redirects the page's `generated-tests` imports through `RANVIER_LADDER_TESTS`. Without `--build`, the script reuses existing bundles. The artifact records each rung's defines, source and bundle hashes, every raw sample, per-rung summaries, per-step deltas and the floor split.
