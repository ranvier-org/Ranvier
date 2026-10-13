# Mount cost ladder

This follows the prepared-template checkpoint at `f64a227`. The ladder attributes production mount time to individual renderer setup steps. Four compile-time gates in `Mounting.fs` replace one step each with its naive equivalent. Without defines, the emitted `Mounting.js` is byte-identical to the ungated source.

| Step | Production | Naive replacement (`LADDER_NAIVE_*`) |
|---|---|---|
| Setup checks | `ensureSetup` around modifiers and appends, active-element check, `validateAttribute` | empty `ensureSetup`; attribute validation skipped (`CHECKS`) |
| Range ownership | `range.Add`/`AppendGenerated`: entries, WeakSet claims, descendant list; attach validation | `appendChild`; top-level roots recorded and removed once (`RANGE`) |
| Component ownership | child `Scope`, `createRoot`, two cleanup links, `untrack` | setup runs inline in the mount scope (`COMPONENTS`) |
| Binding guards | `startBinding`: committed value, comparer, liveness, untracked write, setup failure capture | `createEffect (fun () -> write (read ()))` (`BINDINGS`) |

Each rung adds one step to the previous rung. R0 defines all four symbols; R4 defines none and is production. Step costs therefore sum exactly to R4 minus R0. A step's cost depends on its position in this order; gates interact, and a different order would assign shared costs differently.

## Validation

Every rung passes the framework contract for `ranvier`, `ranvier-direct` and `native-create` at widths 1, 16 and 128 in all three processes: 135 checks. The ungated full playground suite passes 205 Node tests and 22 Chromium tests. Naive rungs are not run against the full suite; they deliberately omit ownership guarantees.

## Measurements

`ladder-results.json` contains one bundle per rung in three independent Chromium processes, three warmups and eleven measured rounds, with rung and adapter order rotated each round. `native-create` is identical in every bundle; its spread across rungs bounds page-to-page variance. At 128 rows it ranges 381–421 µs pooled and 305–457 µs per process. Deltas below roughly 40 µs are not resolved.

At 128 rows, public authoring path (`ranvier`), µs per mount:

| Rung | Median | Process medians |
|---|---|---|
| R0 floor | 774 | 733 / 728 / 825 |
| R1 + setup checks | 840 | 860 / 838 / 828 |
| R2 + range ownership | 967 | 967 / 938 / 986 |
| R3 + component ownership | 994 | 988 / 1032 / 994 |
| R4 production | 1044 | 1003 / 1044 / 1049 |

| Step | Δ µs | Share of R4 | Process deltas |
|---|---|---|---|
| Setup checks | 66 | 6.3% | 127 / 110 / 4 |
| Range ownership | 127 | 12.2% | 107 / 100 / 158 |
| Component ownership | 27 | 2.6% | 21 / 94 / 8 |
| Binding guards | 50 | 4.8% | 14 / 12 / 55 |

The `ranvier-direct` adapter assigns 58, 103, 156 and 75 µs to the same steps. Range ownership is the only step resolved consistently above the noise bound in both adapters and all processes. Component ownership disagrees between adapters by 129 µs; direct construction builds button modifier arrays inside each component, so the adapters do not perform identical work per step.

The four gated steps account for 270 µs, 26% of production. The R0 floor accounts for the remaining 774 µs. It still contains the graph, 128 local signals, 512 naive effects, `Program.View` closures, modifier arrays and backend dispatch. `native-create`, which performs the same DOM work without reactivity, takes about 400 µs. About 370 µs of the floor is therefore reactive core and authoring/dispatch cost, the largest unattributed bucket. Ripple's complete mount at 128 rows was 556 µs in the prepared comparison.

At 16 rows every step is below 10 µs and within noise; total production is 105 µs against an 87 µs floor.

## Reproduction

From `examples/Fable.Ranvier.Playground`:

```powershell
rtk node benchmarks/frameworks/ladder.mjs --build
```

`--build` compiles each rung's Fable output into the ignored `generated-ladder/<rung>` directory and bundles it into `generated-ladder-dist/<rung>`; Vite redirects the page's `generated-tests` imports through `RANVIER_LADDER_TESTS`. Without `--build`, the script reuses existing bundles. The artifact records each rung's defines, source and bundle hashes, every raw sample, per-rung summaries and per-step deltas.
