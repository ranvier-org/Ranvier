# Instruction counts by commit (arm64, instructions per operation, median of 5 processes)

## app-detail

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 101,890 | 694,459 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 101,890 | 694,466 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 101,889 | 694,691 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 101,890 | 694,775 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 102,022 | 695,263 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 101,893 | 691,623 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 101,894 | 755,171 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 101,724 | 140,584 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 101,724 | 140,671 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 101,726 | 141,063 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 101,732 | 141,030 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 101,728 | 140,927 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 101,727 | 141,020 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 101,728 | 140,987 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 101,963 | 141,037 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 101,860 | 139,716 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 101,598 | 140,609 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 101,662 | 140,620 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 84,053 | 125,352 |

## app-table

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 9,255,710 | 11,934,165 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 9,255,854 | 11,922,488 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 9,255,710 | 11,917,743 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 9,255,710 | 11,919,484 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 9,255,733 | 11,916,154 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 9,259,218 | 11,292,665 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 9,264,959 | 10,844,545 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 9,233,272 | 9,776,957 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 9,233,235 | 9,825,499 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 9,233,227 | 9,642,607 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 9,233,705 | 9,775,470 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 9,233,700 | 9,642,269 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 9,233,690 | 9,776,297 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 9,233,708 | 9,639,718 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 9,233,726 | 9,777,758 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 9,233,689 | 9,691,748 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 9,229,983 | 9,629,653 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 9,228,152 | 9,630,607 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 9,120,258 | 9,782,395 |

## async-recover

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 138,706 | 313 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 138,706 | 301 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 138,706 | 339 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 138,806 | 320 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 138,738 | 314 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 138,805 | 382 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 138,829 | -7,037 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 138,757 | 37,543 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 138,757 | 37,516 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 138,757 | 37,532 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 138,829 | 37,525 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 138,829 | 37,533 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 138,829 | 37,525 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 138,829 | 37,536 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 138,829 | 37,529 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 138,829 | 37,457 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 138,826 | 37,112 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 138,826 | 37,104 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 138,968 | 37,303 |

## async-resolve

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 2,793,821 | 2,789,979 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 2,793,821 | 2,791,481 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 2,793,821 | 2,788,424 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 2,796,621 | 2,787,890 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 2,795,421 | 2,789,817 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 2,796,619 | 2,788,702 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 2,798,220 | 3,239,160 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 2,795,019 | 1,170,731 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 2,795,019 | 1,174,766 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 2,795,019 | 1,175,803 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 2,798,220 | 1,174,873 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 2,798,220 | 1,175,506 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 2,798,219 | 1,175,763 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 2,798,219 | 1,175,912 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 2,797,419 | 1,171,819 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 2,797,419 | 1,076,841 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 2,796,739 | 1,066,675 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 2,796,740 | 1,066,673 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 2,791,003 | 1,073,813 |

## chain

| Commit | Date | Subject | dotnet FSharp.Data.Adaptive | dotnet R3 | dotnet Ranvier | node Fable.Ripple | node Ranvier |
|---|---|---|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 13,106 | 318 | 2,470 | 4,671 | 3,911 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 13,042 | 318 | 2,470 | 4,671 | 3,911 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 13,042 | 318 | 2,470 | 4,671 | 3,911 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 13,106 | 318 | 2,470 | 4,671 | 3,911 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 13,042 | 318 | 2,470 | 4,671 | 3,911 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 13,042 | 318 | 2,470 | 4,671 | 3,911 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 13,042 | 318 | 2,470 | 4,671 | 3,464 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 13,042 | 318 | 2,470 | 4,491 | 3,180 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 13,042 | 318 | 2,470 | 4,491 | 3,180 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 13,042 | 318 | 2,470 | 4,491 | 3,180 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 13,042 | 318 | 2,470 | 4,491 | 3,180 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 13,042 | 318 | 2,470 | 4,491 | 3,180 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 13,106 | 318 | 2,470 | 4,491 | 3,180 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 13,042 | 318 | 2,470 | 4,491 | 3,180 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 13,042 | 318 | 2,470 | 4,491 | 3,180 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 13,042 | 318 | 2,470 | 4,491 | 3,180 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 13,042 | 318 | 2,470 | 4,491 | 3,180 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 13,042 | 318 | 2,470 | 4,491 | 3,180 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 13,035 | 318 | 2,450 | 4,491 | 3,180 |

## chain-affinity

| Commit | Date | Subject | dotnet Ranvier (Guarded) | dotnet Ranvier (Serialised) | dotnet Ranvier (Unchecked) |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 2,569 | 3,087 | 2,516 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 2,569 | 3,087 | 2,516 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 2,569 | 3,087 | 2,516 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 2,569 | 3,087 | 2,516 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 2,569 | 3,087 | 2,516 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 2,569 | 3,087 | 2,516 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 2,569 | 3,087 | 2,516 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 2,569 | 3,087 | 2,516 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 2,569 | 3,087 | 2,516 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 2,569 | 3,087 | 2,516 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 2,569 | 3,087 | 2,516 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 2,569 | 3,087 | 2,516 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 2,569 | 3,087 | 2,516 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 2,569 | 3,087 | 2,516 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 2,569 | 3,087 | 2,516 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 2,569 | 3,087 | 2,516 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 2,569 | 3,087 | 2,516 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 2,569 | 3,087 | 2,516 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 2,549 | 3,067 | 2,496 |

## control

| Commit | Date | Subject | dotnet Control | node Control |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 74,719 | 734,920 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 74,719 | 846,628 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 74,719 | 845,939 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 74,719 | 845,860 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 74,719 | 846,668 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 74,719 | 846,627 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 74,719 | 846,629 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 74,719 | 846,604 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 74,719 | 846,544 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 74,719 | 834,846 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 74,720 | 834,925 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 74,720 | 834,910 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 74,719 | 826,138 |

## create

| Commit | Date | Subject | dotnet FSharp.Data.Adaptive | dotnet R3 | dotnet Ranvier | node Fable.Ripple | node Ranvier |
|---|---|---|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 13,525,310 | 2,881,677 | 3,043,239 | 7,807,536 | 6,315,549 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 13,618,826 | 2,865,677 | 3,043,319 | 7,795,439 | 6,319,990 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 13,477,327 | 2,881,677 | 3,043,243 | 7,806,041 | 6,324,076 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 13,744,352 | 2,881,677 | 3,043,255 | 7,801,802 | 6,329,754 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 13,461,358 | 2,865,678 | 3,043,256 | 7,808,774 | 6,313,417 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 13,461,358 | 2,881,677 | 3,043,272 | 7,801,309 | 8,370,484 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 13,461,365 | 2,865,677 | 3,043,240 | 7,788,481 | 3,847,642 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 13,586,828 | 2,877,676 | 3,043,258 | 2,186,132 | 4,208,779 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 13,461,445 | 2,861,677 | 3,043,230 | 2,185,843 | 4,208,429 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 13,477,436 | 2,861,678 | 3,041,258 | 2,185,613 | 4,216,772 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 13,728,357 | 2,861,678 | 3,041,229 | 2,186,610 | 4,213,405 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 13,461,481 | 2,861,678 | 3,041,229 | 2,186,260 | 4,224,631 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 13,586,881 | 2,861,678 | 3,041,306 | 2,186,464 | 4,210,268 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 13,461,450 | 2,861,678 | 3,041,229 | 2,186,430 | 4,202,693 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 13,461,376 | 2,861,678 | 3,041,229 | 2,185,794 | 4,214,446 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 13,602,860 | 2,861,678 | 3,041,257 | 2,185,680 | 4,214,317 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 13,461,452 | 2,893,677 | 3,035,209 | 2,131,279 | 4,184,451 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 13,461,435 | 2,861,678 | 3,035,210 | 2,132,010 | 4,179,898 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 13,531,174 | 2,859,679 | 2,528,399 | 2,132,031 | 3,988,931 |

## create-on

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 3,440,886 | 6,652,100 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 3,456,854 | 6,651,806 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 3,424,854 | 6,652,295 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 3,424,870 | 6,654,339 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 3,440,854 | 6,653,995 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 3,440,854 | 6,817,932 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 3,424,855 | 8,787,748 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 3,429,854 | 4,619,737 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 3,509,854 | 4,630,395 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 3,445,855 | 4,615,793 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 3,429,855 | 4,621,052 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 3,429,855 | 4,626,771 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 3,445,856 | 4,641,377 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 3,445,854 | 4,622,398 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 3,429,877 | 4,634,474 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 3,429,878 | 4,626,474 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 3,439,904 | 4,348,490 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 3,423,905 | 4,354,534 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 2,911,490 | 4,366,645 |

## cutoff

| Commit | Date | Subject | dotnet FSharp.Data.Adaptive | dotnet R3 | dotnet Ranvier | node Fable.Ripple | node Ranvier |
|---|---|---|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 1,181 | 50 | 111 | 51 | 35 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 1,181 | 50 | 111 | 51 | 35 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 1,181 | 50 | 111 | 51 | 35 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 1,181 | 50 | 111 | 51 | 35 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 1,181 | 50 | 111 | 51 | 35 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 1,181 | 50 | 111 | 51 | 35 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 1,181 | 50 | 111 | 51 | 35 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 1,181 | 50 | 111 | 65 | 35 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 1,181 | 50 | 111 | 65 | 35 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 1,181 | 50 | 111 | 65 | 35 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 1,181 | 50 | 111 | 65 | 35 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 1,181 | 50 | 111 | 65 | 35 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 1,181 | 50 | 111 | 65 | 35 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 1,181 | 50 | 111 | 65 | 35 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 1,181 | 50 | 111 | 65 | 35 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 1,181 | 50 | 111 | 65 | 35 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 1,181 | 50 | 111 | 65 | 35 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 1,181 | 50 | 111 | 65 | 35 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 1,182 | 50 | 111 | 65 | 35 |

## derive-effect

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 765 | -1,632 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 765 | -1,632 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 765 | -1,632 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 765 | -1,632 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 765 | -1,632 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 765 | -807 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 765 | -2,002 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 765 | 716 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 765 | 716 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 765 | 716 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 765 | 716 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 765 | 716 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 765 | 716 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 765 | 716 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 765 | 716 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 765 | 716 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 765 | 716 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 765 | 716 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 760 | 680 |

## derive-on

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 740 | -1,380 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 740 | -1,380 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 740 | -1,380 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 740 | -1,380 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 756 | -1,380 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 756 | -798 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 740 | -1,541 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 740 | 684 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 756 | 684 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 740 | 684 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 740 | 684 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 740 | 684 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 740 | 684 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 756 | 684 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 740 | 684 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 740 | 684 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 740 | 684 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 740 | 684 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 735 | 684 |

## dispose

| Commit | Date | Subject | dotnet FSharp.Data.Adaptive | dotnet R3 | dotnet Ranvier | node Fable.Ripple | node Ranvier |
|---|---|---|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 2,707,580 | 806,291 | 498,486 | 471,252 | 1,027,798 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 2,707,580 | 806,291 | 498,486 | 471,252 | 1,027,669 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 2,706,689 | 806,290 | 498,486 | 471,252 | 1,027,643 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 2,707,581 | 806,291 | 498,486 | 471,252 | 1,027,470 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 2,706,102 | 806,292 | 498,486 | 471,252 | 1,026,446 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 2,707,584 | 806,293 | 498,486 | 471,082 | 1,037,758 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 2,707,581 | 806,288 | 498,486 | 471,238 | 787,101 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 2,706,184 | 806,288 | 498,486 | 465,871 | 703,008 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 2,707,578 | 806,290 | 498,486 | 466,112 | 703,398 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 2,707,581 | 806,290 | 498,486 | 465,769 | 703,182 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 2,707,581 | 806,290 | 498,486 | 466,180 | 703,456 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 2,707,581 | 806,291 | 498,486 | 466,014 | 703,538 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 2,706,689 | 806,291 | 498,486 | 466,060 | 702,086 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 2,707,580 | 806,291 | 498,486 | 466,194 | 703,651 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 2,706,181 | 806,290 | 501,497 | 466,276 | 702,782 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 2,707,580 | 806,291 | 501,497 | 466,038 | 702,173 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 2,707,579 | 806,291 | 501,497 | 466,263 | 704,526 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 2,706,184 | 806,290 | 501,497 | 466,110 | 704,570 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 2,706,750 | 806,290 | 367,497 | 466,306 | 434,416 |

## dispose-on

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 477,486 | 1,127,685 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 477,486 | 1,141,547 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 477,486 | 1,127,690 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 477,486 | 1,141,638 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 477,486 | 1,087,005 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 477,486 | 1,100,499 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 477,486 | 883,282 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 477,486 | 699,680 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 477,486 | 699,312 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 477,486 | 699,855 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 477,486 | 699,340 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 477,486 | 699,320 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 477,486 | 699,013 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 477,486 | 699,518 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 480,497 | 699,418 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 480,497 | 700,132 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 480,497 | 698,094 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 480,497 | 698,438 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 346,497 | 430,156 |

## editable-edit

| Commit | Date | Subject | dotnet Ranvier |
|---|---|---|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 181,106 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 181,106 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 181,106 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 181,106 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 181,106 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 181,106 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 181,106 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 181,106 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 181,106 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 181,106 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 181,106 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 181,106 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 181,106 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 181,106 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 181,106 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 181,106 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 181,106 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 181,106 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 179,606 |

## editable-upstream

| Commit | Date | Subject | dotnet Ranvier |
|---|---|---|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 232,756 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 232,756 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 232,756 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 232,756 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 232,756 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 232,755 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 232,755 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 232,755 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 232,755 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 232,757 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 232,756 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 232,756 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 232,756 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 232,756 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 232,755 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 232,756 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 232,755 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 232,755 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 230,755 |

## fail-recompute

| Commit | Date | Subject | dotnet Ranvier (failing) | dotnet Ranvier (succeeding) |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 64,848 | 1,376 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 64,848 | 1,376 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 64,848 | 1,376 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 64,872 | 1,376 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 64,940 | 1,376 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 64,940 | 1,376 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 65,108 | 1,376 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 64,880 | 1,376 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 64,880 | 1,376 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 64,948 | 1,376 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 64,932 | 1,376 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 64,932 | 1,376 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 64,932 | 1,376 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 64,932 | 1,376 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 64,852 | 1,376 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 64,852 | 1,376 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 64,924 | 1,376 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 64,924 | 1,376 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 64,914 | 1,366 |

## flight

| Commit | Date | Subject | dotnet Ranvier (CancelPrevious) | dotnet Ranvier (Drop) | dotnet Ranvier (DropWhilePending) | dotnet Ranvier (FinishCurrent) | dotnet Ranvier (KeepLatest) | dotnet Ranvier (Queue) |
|---|---|---|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 11,041 | — | — | 11,368 | 10,809 | 22,381 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 11,041 | — | — | 11,368 | 10,809 | 22,381 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 11,041 | — | — | 11,367 | 10,809 | 22,381 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 11,041 | — | — | 11,368 | 10,809 | 22,381 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 11,039 | — | — | 11,365 | 10,807 | 22,381 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 11,035 | — | — | 11,366 | 10,807 | 22,381 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 11,035 | — | — | 11,365 | 10,807 | 22,381 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 11,033 | — | — | 11,363 | 10,805 | 22,379 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 11,033 | — | — | 11,363 | 10,805 | 22,379 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 11,049 | 6,244 | 6,245 | 11,372 | 10,817 | 22,381 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 11,051 | 6,245 | 6,246 | 11,374 | 10,819 | 22,383 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 11,051 | 6,245 | 6,246 | 11,375 | 10,819 | 22,383 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 11,051 | 6,245 | 6,246 | 11,374 | 10,819 | 22,383 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 11,051 | 6,245 | 6,246 | 11,374 | 10,819 | 22,383 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 11,051 | 6,245 | 6,246 | 11,375 | 10,819 | 22,383 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 11,051 | 6,245 | 6,246 | 11,374 | 10,819 | 22,383 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 11,051 | 6,245 | 6,246 | 11,375 | 10,819 | 22,383 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 11,051 | 6,245 | 6,246 | 11,374 | 10,819 | 22,383 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 11,041 | 6,240 | 6,241 | 11,365 | 10,809 | 22,373 |

## keyed-insert

| Commit | Date | Subject | dotnet Ranvier (fresh items) | dotnet Ranvier (memo-free rows) | dotnet Ranvier (stable items) | node Ranvier (fresh items) | node Ranvier (memo-free rows) | node Ranvier (stable items) |
|---|---|---|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 1,944,611 | — | 73,358 | 4,548,866 | — | 214,055 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 1,944,587 | — | 73,334 | 4,548,960 | — | 214,109 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 1,944,587 | — | 73,334 | 4,548,807 | — | 213,809 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 1,944,587 | — | 73,334 | 4,543,663 | — | 213,991 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 1,944,609 | 72,667 | 73,324 | 4,547,218 | 246,069 | 214,072 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 1,921,657 | 54,104 | 54,752 | 4,543,583 | 209,597 | 194,141 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 1,921,671 | 54,104 | 54,767 | 4,296,964 | 209,171 | 192,218 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 1,921,560 | 53,846 | 54,581 | 3,092,666 | 162,633 | 172,627 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 1,921,560 | 53,846 | 54,582 | 3,092,565 | 162,828 | 172,579 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 1,921,603 | 53,864 | 54,597 | 3,092,660 | 162,680 | 172,520 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 1,921,590 | 53,936 | 54,674 | 3,092,673 | 163,160 | 172,920 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 1,921,612 | 53,936 | 54,698 | 3,092,648 | 163,142 | 173,032 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 1,921,599 | 53,927 | 54,686 | 3,092,666 | 163,127 | 173,069 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 1,921,590 | 53,946 | 54,675 | 3,092,613 | 163,149 | 173,067 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 1,921,588 | 53,976 | 54,699 | 3,092,666 | 163,057 | 173,057 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 1,921,573 | 53,966 | 54,665 | 3,092,872 | 162,911 | 172,991 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 1,921,568 | 53,934 | 54,623 | 3,092,994 | 163,085 | 172,557 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 1,921,766 | 53,941 | 54,668 | 3,092,947 | 163,151 | 172,662 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 1,918,543 | 53,948 | 54,624 | 3,114,971 | 164,122 | 167,165 |

## keyed-remove

| Commit | Date | Subject | dotnet Ranvier (fresh items) | dotnet Ranvier (memo-free rows) | dotnet Ranvier (stable items) | node Ranvier (fresh items) | node Ranvier (memo-free rows) | node Ranvier (stable items) |
|---|---|---|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 1,943,561 | — | 66,481 | 4,150,967 | — | 187,077 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 1,943,538 | — | 66,457 | 4,154,900 | — | 184,034 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 1,943,553 | — | 66,463 | 4,153,664 | — | 181,760 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 1,943,547 | — | 66,463 | 4,152,875 | — | 184,851 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 1,943,547 | 65,120 | 66,456 | 4,146,197 | 272,464 | 182,669 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 1,920,611 | 43,808 | 45,128 | 4,333,715 | 165,760 | 123,085 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 1,920,614 | 43,793 | 45,145 | 3,746,188 | 164,238 | 114,438 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 1,920,500 | 43,539 | 44,963 | 3,092,572 | 130,252 | 142,773 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 1,920,789 | 43,539 | 45,100 | 3,092,513 | 130,276 | 142,726 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 1,920,550 | 43,566 | 44,995 | 3,092,470 | 130,256 | 142,766 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 1,920,537 | 43,638 | 45,058 | 3,092,529 | 130,695 | 143,307 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 1,920,550 | 43,638 | 45,081 | 3,092,463 | 130,641 | 143,342 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 1,920,541 | 43,629 | 45,066 | 3,092,569 | 130,837 | 143,257 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 1,920,532 | 43,647 | 45,058 | 3,092,431 | 130,613 | 143,361 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 1,920,547 | 43,635 | 45,050 | 3,092,537 | 132,187 | 143,308 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 1,920,533 | 43,626 | 45,039 | 3,092,718 | 132,010 | 143,311 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 1,920,519 | 43,626 | 45,007 | 3,092,959 | 134,022 | 143,253 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 1,920,716 | 43,632 | 45,046 | 3,092,992 | 134,114 | 143,433 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 1,917,460 | 43,626 | 45,002 | 3,114,557 | 131,766 | 140,540 |

## keyed-swap

| Commit | Date | Subject | dotnet Ranvier (fresh items) | dotnet Ranvier (memo-free rows) | dotnet Ranvier (stable items) | node Ranvier (fresh items) | node Ranvier (memo-free rows) | node Ranvier (stable items) |
|---|---|---|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 1,996,310 | — | 71,544 | 4,492,185 | — | 181,044 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 1,996,294 | — | 71,528 | 4,491,416 | — | 181,067 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 1,996,294 | — | 71,528 | 4,492,820 | — | 181,072 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 1,996,294 | — | 71,528 | 4,494,937 | — | 179,703 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 1,996,301 | 71,111 | 71,526 | 4,491,295 | 229,835 | 179,487 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 1,973,263 | 46,335 | 46,760 | 4,469,743 | 98,302 | 82,515 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 1,973,283 | 46,335 | 46,776 | 4,233,627 | 98,015 | 81,077 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 1,973,152 | 46,080 | 46,557 | 3,215,194 | 76,966 | 83,156 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 1,973,152 | 46,081 | 46,558 | 3,215,157 | 77,033 | 82,398 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 1,973,185 | 46,098 | 46,574 | 3,215,219 | 76,971 | 83,291 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) (accepted) | 1,973,171 | 46,167 | 46,649 | 3,215,270 | 79,489 | 84,811 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 1,973,203 | 46,167 | 46,681 | 3,215,232 | 79,407 | 84,869 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 1,973,189 | 46,167 | 46,667 | 3,215,206 | 79,449 | 85,165 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 1,973,171 | 46,168 | 46,649 | 3,215,194 | 79,487 | 84,995 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 1,973,202 | 46,167 | 46,667 | 3,215,181 | 79,316 | 84,944 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 1,973,184 | 46,167 | 46,649 | 3,215,502 | 79,183 | 85,189 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 1,973,171 | 46,168 | 46,649 | 3,215,847 | 79,219 | 84,875 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 1,973,379 | 46,183 | 46,699 | 3,215,874 | 79,374 | 84,887 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 1,970,160 | 46,161 | 46,638 | 3,264,730 | 79,254 | 84,519 |

## mvu-dispatch

| Commit | Date | Subject | dotnet Ranvier (Mvu) | dotnet Ranvier (signal per field) |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 58,392 | 772 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 58,391 | 772 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 58,391 | 772 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 58,391 | 772 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 58,391 | 772 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 58,391 | 772 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 58,391 | 772 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 58,391 | 772 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 58,391 | 772 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 58,391 | 772 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 58,391 | 772 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 58,391 | 772 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 58,391 | 772 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 58,391 | 772 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 58,391 | 772 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 58,391 | 772 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 58,391 | 772 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 58,391 | 772 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 58,066 | 767 |

## project-chain

| Commit | Date | Subject | dotnet Ranvier |
|---|---|---|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 3,865,868 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 3,865,951 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 3,865,852 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 3,865,856 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 3,865,882 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 3,847,882 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 3,847,866 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 3,837,487 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 3,837,396 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 3,837,412 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 3,837,485 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 3,837,421 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 3,837,404 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 3,837,405 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 3,837,438 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 3,837,389 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 3,837,400 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 3,837,432 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 3,834,759 |

## project-churn

| Commit | Date | Subject | dotnet Ranvier (key reader) | dotnet Ranvier (no reader) |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 61,103 | 59,026 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 61,103 | 59,026 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 61,103 | 59,026 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 61,103 | 59,022 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 61,102 | 59,022 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 36,375 | 34,218 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 36,375 | 34,204 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 36,202 | 34,033 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 36,202 | 34,033 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 36,202 | 34,033 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 36,290 | 34,121 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 36,290 | 34,121 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 36,290 | 34,121 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 36,290 | 34,121 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 36,290 | 34,121 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 36,290 | 34,121 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 36,252 | 34,083 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 36,252 | 34,083 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 36,206 | 34,048 |

## project-edit

| Commit | Date | Subject | dotnet Ranvier |
|---|---|---|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 68,439 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 68,343 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 68,343 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 68,378 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 68,378 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 50,364 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 50,364 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 50,173 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 50,188 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 50,173 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 50,201 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 50,218 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 50,186 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 50,202 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 50,186 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 50,186 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 50,185 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 50,185 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 50,181 |

## project-reorder

| Commit | Date | Subject | dotnet Ranvier |
|---|---|---|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 802,063 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 802,031 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 802,031 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 802,066 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 802,069 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 779,022 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 779,020 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 778,875 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 778,875 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 778,879 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 778,907 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 778,907 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 778,907 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 778,924 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 778,895 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 778,895 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 778,907 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 778,907 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 778,916 |

## shape-diamond

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 60,899 | 116,578 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 60,899 | 116,575 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 60,899 | 116,578 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 60,899 | 116,611 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 60,899 | 116,579 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 60,899 | 117,208 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 60,899 | 106,625 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 60,899 | 88,455 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 60,899 | 88,455 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 60,899 | 88,455 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 60,899 | 88,455 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 60,899 | 88,455 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 60,899 | 88,455 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 60,899 | 88,455 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 60,899 | 88,455 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 60,899 | 88,455 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 60,899 | 88,455 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 60,899 | 88,455 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 59,894 | 88,455 |

## shape-dynamic

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 82,420 | -24,661 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 82,420 | -24,701 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 82,420 | -24,728 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 82,420 | -24,425 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 82,420 | -24,744 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 82,420 | -19,948 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 82,420 | -130,233 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 82,420 | 97,997 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 82,420 | 98,007 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 82,420 | 98,000 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 82,420 | 98,007 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 82,420 | 98,003 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 82,420 | 98,003 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 82,420 | 98,007 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 83,732 | 98,003 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 82,420 | 98,001 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 82,420 | 98,000 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 82,420 | 98,000 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 82,520 | 95,378 |

## timed-admit-both

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 130,748 | 166,599 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 130,748 | 166,581 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 130,748 | 166,630 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 133,820 | 166,570 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 130,748 | 166,851 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 131,772 | 166,599 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 130,748 | 150,665 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 118,909 | 133,573 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 119,933 | 133,572 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 118,909 | 133,573 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 118,909 | 133,573 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 118,909 | 133,569 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 118,909 | 133,570 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 118,909 | 133,573 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 118,909 | 133,571 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 118,909 | 133,569 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 118,909 | 133,650 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 118,909 | 133,655 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 118,269 | 133,685 |

## timed-admit-debounce

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 129,654 | 162,933 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 129,654 | 162,898 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 129,654 | 162,868 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 132,726 | 162,881 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 129,654 | 162,874 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 130,678 | 162,874 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 129,654 | 146,428 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 117,814 | 132,166 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 118,838 | 132,165 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 117,814 | 132,166 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 117,814 | 132,167 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 117,814 | 132,165 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 117,814 | 132,169 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 117,818 | 132,166 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 117,814 | 132,167 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 117,814 | 132,162 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 117,814 | 132,158 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 117,818 | 132,160 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 117,174 | 132,327 |

## timed-admit-first

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 76,517 | 101,552 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 76,517 | 101,552 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 76,517 | 101,552 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 78,565 | 101,552 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 76,517 | 101,552 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 77,541 | 101,552 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 76,517 | 93,948 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 69,925 | 81,964 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 70,949 | 81,964 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 69,925 | 81,962 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 69,925 | 81,964 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 69,925 | 81,963 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 69,925 | 81,964 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 69,925 | 81,964 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 69,925 | 81,964 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 69,925 | 81,964 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 69,925 | 81,964 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 69,925 | 81,964 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 69,285 | 82,092 |

## timed-admit-last

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 129,916 | 162,163 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 129,916 | 162,139 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 129,916 | 162,134 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 132,988 | 162,139 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 129,916 | 162,141 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 130,940 | 162,139 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 129,916 | 146,061 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 118,076 | 132,485 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 119,100 | 132,483 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 118,076 | 132,486 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 118,076 | 132,487 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 118,076 | 132,487 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 118,076 | 132,486 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 118,076 | 132,487 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 118,076 | 132,487 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 118,076 | 132,470 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 118,076 | 132,470 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 118,076 | 132,480 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 117,436 | 132,611 |

## timed-capture-both

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 42,700 | 52,067 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 42,700 | 52,067 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 42,700 | 52,067 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 43,724 | 52,067 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 42,700 | 52,067 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 42,700 | 52,065 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 42,700 | 50,543 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 36,044 | 44,956 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 36,044 | 44,958 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 36,044 | 44,954 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 36,044 | 44,959 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 36,044 | 44,957 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 36,044 | 44,957 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 36,048 | 44,957 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 36,044 | 44,956 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 36,044 | 44,965 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 36,044 | 44,981 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 36,044 | 44,978 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 35,724 | 45,028 |

## timed-capture-debounce

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 42,572 | 54,165 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 42,572 | 54,165 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 42,572 | 54,165 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 43,596 | 54,165 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 42,572 | 54,165 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 42,572 | 54,165 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 42,572 | 49,892 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 35,916 | 43,583 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 35,916 | 43,583 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 35,916 | 43,583 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 35,916 | 43,583 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 35,916 | 43,583 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 35,916 | 43,583 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 35,916 | 43,583 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 35,916 | 43,583 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 35,916 | 43,583 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 35,916 | 43,583 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 35,916 | 43,583 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 35,596 | 43,646 |

## timed-capture-first

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 37,772 | 47,381 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 37,772 | 47,381 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 37,772 | 47,381 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 38,796 | 47,381 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 37,772 | 47,381 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 37,772 | 47,381 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 37,772 | 45,452 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 32,396 | 38,804 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 32,396 | 38,804 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 32,396 | 38,802 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 32,396 | 38,804 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 32,396 | 38,804 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 32,396 | 38,804 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 32,396 | 38,804 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 32,396 | 38,804 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 32,396 | 38,804 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 32,396 | 38,804 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 32,396 | 38,804 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 32,076 | 38,868 |

## timed-capture-last

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 42,508 | 51,303 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 42,508 | 51,303 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 42,508 | 51,303 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 43,532 | 51,303 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 42,508 | 51,303 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 42,508 | 51,303 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 42,508 | 49,779 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 35,852 | 43,519 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 35,852 | 43,519 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 35,852 | 43,519 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 35,852 | 43,519 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 35,852 | 43,519 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 35,852 | 43,519 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 35,852 | 43,519 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 35,852 | 43,519 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 35,852 | 43,519 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 35,852 | 43,519 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 35,852 | 43,519 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 35,532 | 43,583 |

## update

| Commit | Date | Subject | dotnet FSharp.Data.Adaptive | dotnet R3 | dotnet Ranvier | node Fable.Ripple | node Ranvier |
|---|---|---|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 1,090,280 | 38,805 | 83,005 | 472,334 | 377,510 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 1,095,080 | 38,805 | 83,005 | 472,299 | 377,285 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 1,090,280 | 38,805 | 83,005 | 472,399 | 377,202 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 1,095,080 | 38,805 | 83,005 | 472,204 | 377,236 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 1,088,680 | 38,805 | 83,005 | 471,711 | 377,241 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 1,088,680 | 38,805 | 83,005 | 473,466 | 382,448 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 1,088,680 | 38,805 | 83,005 | 472,939 | 362,204 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 1,088,680 | 38,805 | 83,005 | 263,397 | 207,816 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 1,090,272 | 38,805 | 83,005 | 263,444 | 207,813 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 1,088,680 | 38,805 | 83,005 | 263,413 | 207,830 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 1,090,272 | 38,805 | 83,005 | 263,459 | 207,781 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 1,088,680 | 38,805 | 83,005 | 263,427 | 207,822 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 1,091,872 | 38,805 | 83,005 | 263,428 | 207,810 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 1,088,672 | 38,805 | 83,005 | 263,483 | 207,800 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 1,088,680 | 38,805 | 83,005 | 263,408 | 207,795 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 1,088,680 | 38,805 | 83,005 | 263,113 | 207,770 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 1,090,280 | 38,805 | 83,005 | 262,840 | 207,848 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 1,088,673 | 38,805 | 83,005 | 262,809 | 207,798 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 1,087,690 | 38,805 | 82,005 | 262,586 | 207,601 |

## update-on

| Commit | Date | Subject | dotnet Ranvier | node Ranvier |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 93,805 | 402,300 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 93,805 | 402,349 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 93,805 | 402,224 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 93,805 | 402,195 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 95,405 | 402,258 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 95,405 | 407,658 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 93,805 | 379,712 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 93,805 | 222,107 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 95,405 | 222,117 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 93,805 | 222,082 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 93,805 | 222,091 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 93,805 | 222,096 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 93,805 | 222,111 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 95,405 | 222,096 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 93,805 | 222,116 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 93,805 | 222,065 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 93,805 | 222,097 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 93,805 | 222,064 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 92,805 | 221,283 |

## work-form

| Commit | Date | Subject | dotnet Ranvier (createEffect) | dotnet Ranvier (createEffectOn) | node Ranvier (createEffect) | node Ranvier (createEffectOn) |
|---|---|---|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 174,678 | 168,144 | 340,679 | 309,540 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 174,678 | 168,144 | 340,579 | 309,540 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 174,678 | 168,144 | 340,750 | 309,540 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 174,678 | 168,144 | 340,714 | 309,540 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 174,678 | 168,144 | 340,961 | 309,519 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 174,678 | 168,144 | 269,046 | 306,828 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 174,678 | 168,144 | 405,151 | 291,380 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 174,678 | 168,144 | 288,773 | 287,120 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 174,678 | 168,161 | 288,770 | 287,116 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 174,678 | 168,144 | 288,733 | 287,095 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 174,678 | 168,144 | 288,766 | 287,120 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 174,678 | 168,177 | 288,770 | 287,120 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 174,678 | 168,144 | 288,761 | 287,120 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 174,678 | 168,144 | 288,773 | 287,120 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 174,678 | 168,163 | 288,724 | 287,115 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 174,678 | 168,177 | 288,722 | 287,066 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 174,678 | 172,144 | 288,766 | 287,118 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 174,678 | 168,161 | 288,750 | 287,109 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 169,672 | 163,139 | 288,752 | 287,018 |

## work-status

| Commit | Date | Subject | dotnet Ranvier (createEffect) | dotnet Ranvier (createEffectOn) | node Ranvier (createEffect) | node Ranvier (createEffectOn) |
|---|---|---|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 100,445 | 79,115 | 712,634 | 976,643 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 100,445 | 79,115 | 712,580 | 976,604 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 100,445 | 79,115 | 712,610 | 976,647 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 100,445 | 79,115 | 712,615 | 976,593 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 100,445 | 80,715 | 712,424 | 976,952 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 100,445 | 80,715 | 861,993 | 654,738 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 100,445 | 79,115 | 616,750 | 919,678 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 100,445 | 79,115 | 315,937 | 155,377 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 100,445 | 80,715 | 315,963 | 155,385 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 100,445 | 79,115 | 315,927 | 155,377 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 100,445 | 79,115 | 315,923 | 155,422 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 100,445 | 79,115 | 315,945 | 155,384 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 100,445 | 79,115 | 315,962 | 155,370 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 100,445 | 80,715 | 315,935 | 155,346 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 100,445 | 79,115 | 315,942 | 155,383 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 100,445 | 79,115 | 315,910 | 155,356 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 100,445 | 79,116 | 315,892 | 155,398 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 100,445 | 79,116 | 315,849 | 155,384 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 99,945 | 78,615 | 320,915 | 155,412 |

## work-value

| Commit | Date | Subject | dotnet Ranvier (createEffect) | dotnet Ranvier (createEffectOn) | node Ranvier (createEffect) | node Ranvier (createEffectOn) |
|---|---|---|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 107,571 | 119,770 | 766,592 | 766,912 |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 107,569 | 119,770 | 766,630 | 767,077 |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 107,569 | 119,770 | 766,628 | 766,926 |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 107,569 | 119,771 | 766,556 | 766,953 |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 107,570 | 121,369 | 766,614 | 766,837 |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 107,571 | 121,370 | 756,707 | 742,054 |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 107,569 | 119,771 | 629,511 | 691,109 |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 107,571 | 119,770 | 316,167 | 283,706 |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 107,569 | 121,369 | 316,178 | 283,728 |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 107,570 | 119,769 | 316,156 | 283,730 |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 107,569 | 119,771 | 316,193 | 283,676 |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 107,571 | 119,770 | 316,170 | 283,707 |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 107,569 | 119,769 | 316,135 | 283,696 |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 107,569 | 121,369 | 316,136 | 283,703 |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 107,569 | 119,771 | 316,246 | 283,710 |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 107,571 | 119,770 | 316,149 | 283,694 |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 107,569 | 119,770 | 316,130 | 283,716 |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 107,569 | 119,769 | 316,141 | 283,721 |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 107,071 | 119,269 | 321,107 | 283,844 |

