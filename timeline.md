# Instruction counts by commit (arm64, instructions per operation, median of 5 processes)

## app-detail

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 101,890 | 694,459 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 101,890 | 694,466 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 101,889 | 694,691 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 101,890 | 694,775 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 102,022 | 695,263 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 101,893 | 691,623 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 101,894 | 755,171 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 101,724 | 140,584 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 101,724 | 140,671 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 101,726 | 141,063 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 101,732 | 141,030 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 101,728 | 140,927 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 101,727 | 141,020 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 101,728 | 140,987 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 101,963 | 141,037 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 101,860 | 139,716 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 101,598 | 140,609 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 101,662 | 140,620 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 84,053 | 125,352 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 84,053 | 125,324 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 84,051 | 125,351 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 84,042 | 126,438 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 84,044 | 126,437 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 84,045 | 126,319 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 84,043 | 126,340 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 84,043 | 126,334 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 84,042 | 126,373 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 84,044 | 126,391 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 84,130 | 127,478 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 84,129 | 127,464 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 84,133 | 127,465 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 84,134 | 127,505 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 83,000 | 125,893 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 82,998 | 125,847 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 83,015 | 125,883 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 83,016 | 125,845 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 83,006 | 125,908 | 1,732,908 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 83,048 | 127,105 | 1,734,142 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 83,432 | 127,121 | 1,734,404 |

## app-table

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 9,255,710 | 11,934,165 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 9,255,854 | 11,922,488 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 9,255,710 | 11,917,743 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 9,255,710 | 11,919,484 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 9,255,733 | 11,916,154 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 9,259,218 | 11,292,665 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 9,264,959 | 10,844,545 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 9,233,272 | 9,776,957 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 9,233,235 | 9,825,499 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 9,233,227 | 9,642,607 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 9,233,705 | 9,775,470 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 9,233,700 | 9,642,269 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 9,233,690 | 9,776,297 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 9,233,708 | 9,639,718 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 9,233,726 | 9,777,758 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 9,233,689 | 9,691,748 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 9,229,983 | 9,629,653 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 9,228,152 | 9,630,607 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 9,120,258 | 9,782,395 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 9,120,251 | 9,605,263 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 9,120,290 | 9,716,121 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 9,120,286 | 9,607,873 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 9,120,416 | 9,753,433 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 9,120,343 | 9,608,947 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 9,120,295 | 9,711,135 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 9,120,311 | 9,716,978 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 9,120,327 | 9,716,330 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 9,120,285 | 9,783,165 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 9,135,649 | 9,838,145 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 9,135,632 | 9,661,994 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 9,135,840 | 9,661,584 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 9,135,842 | 9,669,436 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 9,135,960 | 9,728,643 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 9,136,099 | 9,667,396 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 9,137,917 | 9,770,657 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 4,416,155 | 9,667,570 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 4,416,056 | 9,769,936 | 107,687,281 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 4,416,352 | 9,203,562 | 107,430,896 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 4,416,157 | 9,491,867 | 107,302,102 |

## async-recover

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 138,706 | 313 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 138,706 | 301 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 138,706 | 339 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 138,806 | 320 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 138,738 | 314 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 138,805 | 382 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 138,829 | -7,037 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 138,757 | 37,543 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 138,757 | 37,516 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 138,757 | 37,532 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 138,829 | 37,525 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 138,829 | 37,533 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 138,829 | 37,525 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 138,829 | 37,536 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 138,829 | 37,529 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 138,829 | 37,457 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 138,826 | 37,112 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 138,826 | 37,104 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 138,968 | 37,303 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 138,968 | 37,305 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 138,968 | 37,306 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 138,884 | 37,321 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 138,884 | 37,319 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 138,884 | 37,315 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 138,884 | 37,323 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 138,884 | 37,316 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 138,884 | 37,318 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 138,884 | 37,321 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 139,024 | 37,319 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 139,024 | 37,325 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 139,024 | 37,318 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 138,971 | 37,321 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 138,975 | 37,447 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 138,975 | 37,453 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 139,063 | 37,451 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 139,048 | 37,449 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 139,047 | 37,452 | 440,318 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 139,048 | 39,725 | 440,644 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 139,040 | 39,650 | 440,652 |

## async-resolve

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 2,793,821 | 2,789,979 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 2,793,821 | 2,791,481 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 2,793,821 | 2,788,424 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 2,796,621 | 2,787,890 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 2,795,421 | 2,789,817 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 2,796,619 | 2,788,702 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 2,798,220 | 3,239,160 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 2,795,019 | 1,170,731 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 2,795,019 | 1,174,766 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 2,795,019 | 1,175,803 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 2,798,220 | 1,174,873 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 2,798,220 | 1,175,506 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 2,798,219 | 1,175,763 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 2,798,219 | 1,175,912 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 2,797,419 | 1,171,819 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 2,797,419 | 1,076,841 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 2,796,739 | 1,066,675 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 2,796,740 | 1,066,673 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 2,791,003 | 1,073,813 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 2,791,002 | 1,073,817 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 2,791,002 | 1,074,343 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 2,788,200 | 1,074,609 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 2,788,201 | 1,074,533 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 2,788,200 | 1,074,848 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 2,788,201 | 1,074,446 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 2,788,200 | 1,074,517 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 2,788,202 | 1,074,577 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 2,788,200 | 1,074,546 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 2,791,802 | 1,074,391 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 2,791,802 | 1,081,668 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 2,791,801 | 1,074,462 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 2,790,985 | 1,074,472 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 2,789,917 | 1,079,794 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 2,789,917 | 1,079,809 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 2,793,119 | 1,080,083 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 2,793,109 | 1,079,946 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 2,793,109 | 1,079,567 | 12,301,104 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 2,793,110 | 1,085,386 | 12,317,049 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 2,792,310 | 1,085,329 | 12,314,001 |

## chain

| Commit | Date | Subject | dotnet FSharp.Data.Adaptive | dotnet R3 | dotnet Ranvier | node Fable.Ripple | node Ranvier | node-jitless Fable.Ripple | node-jitless Ranvier |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 13,106 | 318 | 2,470 | 4,671 | 3,911 | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 13,042 | 318 | 2,470 | 4,671 | 3,911 | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 13,042 | 318 | 2,470 | 4,671 | 3,911 | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 13,106 | 318 | 2,470 | 4,671 | 3,911 | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 13,042 | 318 | 2,470 | 4,671 | 3,911 | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 13,042 | 318 | 2,470 | 4,671 | 3,911 | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 13,042 | 318 | 2,470 | 4,671 | 3,464 | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 13,042 | 318 | 2,470 | 4,491 | 3,180 | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 13,042 | 318 | 2,470 | 4,491 | 3,180 | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 13,042 | 318 | 2,470 | 4,491 | 3,180 | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 13,042 | 318 | 2,470 | 4,491 | 3,180 | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 13,042 | 318 | 2,470 | 4,491 | 3,180 | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 13,106 | 318 | 2,470 | 4,491 | 3,180 | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 13,042 | 318 | 2,470 | 4,491 | 3,180 | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 13,042 | 318 | 2,470 | 4,491 | 3,180 | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 13,042 | 318 | 2,470 | 4,491 | 3,180 | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 13,042 | 318 | 2,470 | 4,491 | 3,180 | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 13,042 | 318 | 2,470 | 4,491 | 3,180 | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 13,355 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 13,043 | 318 | 2,450 | 4,492 | 3,180 | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 13,107 | 318 | 2,450 | 4,492 | 3,180 | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 13,043 | 318 | 2,450 | 4,492 | 3,180 | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 13,075 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 13,043 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 13,043 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 13,075 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 13,107 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 13,043 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 13,043 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 13,043 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |

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
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 2,549 | 3,067 | 2,496 |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 2,549 | 3,067 | 2,496 |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 2,549 | 3,067 | 2,496 |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 2,549 | 3,067 | 2,496 |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 2,549 | 3,067 | 2,496 |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 2,549 | 3,067 | 2,496 |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 2,549 | 3,067 | 2,496 |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 2,549 | 3,067 | 2,496 |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 2,549 | 3,067 | 2,496 |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 2,549 | 3,067 | 2,496 |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 2,549 | 3,067 | 2,496 |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 2,549 | 3,067 | 2,496 |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 2,549 | 3,067 | 2,496 |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 2,549 | 3,067 | 2,496 |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 2,549 | 3,067 | 2,496 |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 2,549 | 3,067 | 2,496 |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 2,549 | 3,067 | 2,496 |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 2,549 | 3,067 | 2,496 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 2,549 | 3,067 | 2,496 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 2,549 | 3,067 | 2,496 |

## control

| Commit | Date | Subject | dotnet Control | node Control | node-jitless Control |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | — | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | — | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | — | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | — | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | — | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | — | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 74,719 | 734,920 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 74,719 | 846,628 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 74,719 | 845,939 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 74,719 | 845,860 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 74,719 | 846,668 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 74,719 | 846,627 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 74,719 | 846,629 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 74,719 | 846,604 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 74,719 | 846,544 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 74,719 | 834,846 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 74,720 | 834,925 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 74,720 | 834,910 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 74,719 | 826,138 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 74,719 | 832,961 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 74,719 | 826,177 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 74,720 | 826,806 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 74,720 | 826,788 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 74,720 | 826,863 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 74,720 | 836,174 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 74,720 | 843,978 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 74,720 | 843,934 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 74,720 | 844,008 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 74,719 | 833,977 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 74,719 | 833,959 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 74,719 | 833,925 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 74,719 | 833,959 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 74,719 | 833,983 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 74,719 | 833,913 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 74,719 | 834,039 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 74,709 | 833,882 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 74,709 | 834,030 | 2,605,052 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 74,709 | 833,999 | 2,605,091 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 74,709 | 834,956 | 2,605,086 |

## create

| Commit | Date | Subject | dotnet FSharp.Data.Adaptive | dotnet R3 | dotnet Ranvier | node Fable.Ripple | node Ranvier | node-jitless Fable.Ripple | node-jitless Ranvier |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 13,525,310 | 2,881,677 | 3,043,239 | 7,807,536 | 6,315,549 | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 13,618,826 | 2,865,677 | 3,043,319 | 7,795,439 | 6,319,990 | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 13,477,327 | 2,881,677 | 3,043,243 | 7,806,041 | 6,324,076 | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 13,744,352 | 2,881,677 | 3,043,255 | 7,801,802 | 6,329,754 | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 13,461,358 | 2,865,678 | 3,043,256 | 7,808,774 | 6,313,417 | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 13,461,358 | 2,881,677 | 3,043,272 | 7,801,309 | 8,370,484 | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 13,461,365 | 2,865,677 | 3,043,240 | 7,788,481 | 3,847,642 | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 13,586,828 | 2,877,676 | 3,043,258 | 2,186,132 | 4,208,779 | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 13,461,445 | 2,861,677 | 3,043,230 | 2,185,843 | 4,208,429 | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 13,477,436 | 2,861,678 | 3,041,258 | 2,185,613 | 4,216,772 | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 13,728,357 | 2,861,678 | 3,041,229 | 2,186,610 | 4,213,405 | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 13,461,481 | 2,861,678 | 3,041,229 | 2,186,260 | 4,224,631 | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 13,586,881 | 2,861,678 | 3,041,306 | 2,186,464 | 4,210,268 | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 13,461,450 | 2,861,678 | 3,041,229 | 2,186,430 | 4,202,693 | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 13,461,376 | 2,861,678 | 3,041,229 | 2,185,794 | 4,214,446 | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 13,602,860 | 2,861,678 | 3,041,257 | 2,185,680 | 4,214,317 | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 13,461,452 | 2,893,677 | 3,035,209 | 2,131,279 | 4,184,451 | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 13,461,435 | 2,861,678 | 3,035,210 | 2,132,010 | 4,179,898 | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 13,531,174 | 2,859,679 | 2,528,399 | 2,132,031 | 3,988,931 | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 13,451,365 | 2,859,678 | 2,528,380 | 2,131,543 | 3,989,083 | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 13,451,300 | 2,875,680 | 2,528,385 | 2,131,328 | 3,988,938 | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 13,463,506 | 2,865,678 | 2,528,361 | 2,131,968 | 3,984,059 | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 13,589,022 | 2,865,678 | 2,528,378 | 2,131,533 | 3,984,654 | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 13,714,524 | 2,865,678 | 2,528,410 | 2,131,816 | 3,984,556 | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 13,589,068 | 2,881,678 | 2,528,362 | 2,131,083 | 3,984,508 | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 13,621,031 | 2,865,678 | 2,528,362 | 2,131,910 | 3,985,016 | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 13,839,911 | 2,881,678 | 2,528,376 | 2,131,058 | 3,984,927 | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 13,575,375 | 2,913,676 | 2,528,442 | 2,132,028 | 3,984,669 | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 13,589,016 | 2,865,678 | 2,528,377 | 2,131,888 | 3,984,728 | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 13,463,453 | 2,865,679 | 2,528,361 | 2,131,887 | 3,984,560 | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 13,463,470 | 2,897,741 | 2,528,376 | 2,131,010 | 3,984,540 | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 13,463,416 | 2,865,677 | 2,528,360 | 2,132,018 | 3,984,596 | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 13,465,934 | 2,875,678 | 2,532,362 | 2,131,779 | 4,002,416 | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 13,449,947 | 2,859,679 | 2,532,361 | 2,131,112 | 4,002,120 | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 13,449,953 | 2,859,678 | 2,532,409 | 2,131,062 | 4,002,325 | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 13,449,953 | 2,859,678 | 2,532,377 | 2,131,932 | 4,002,655 | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 13,449,960 | 2,875,677 | 2,548,361 | 2,131,894 | 3,998,782 | 34,065,187 | 49,618,972 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 13,575,594 | 2,859,678 | 2,532,361 | 2,131,334 | 4,003,678 | 34,063,391 | 49,723,780 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 13,450,133 | 2,875,677 | 2,532,361 | 2,131,946 | 4,004,114 | 34,063,566 | 49,723,544 |

## create-on

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 3,440,886 | 6,652,100 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 3,456,854 | 6,651,806 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 3,424,854 | 6,652,295 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 3,424,870 | 6,654,339 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 3,440,854 | 6,653,995 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 3,440,854 | 6,817,932 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 3,424,855 | 8,787,748 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 3,429,854 | 4,619,737 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 3,509,854 | 4,630,395 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 3,445,855 | 4,615,793 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 3,429,855 | 4,621,052 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 3,429,855 | 4,626,771 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 3,445,856 | 4,641,377 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 3,445,854 | 4,622,398 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 3,429,877 | 4,634,474 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 3,429,878 | 4,626,474 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 3,439,904 | 4,348,490 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 3,423,905 | 4,354,534 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 2,911,490 | 4,366,645 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 2,927,488 | 4,366,998 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 2,911,520 | 4,366,997 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 2,910,484 | 4,367,150 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 2,910,485 | 4,366,970 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 2,910,484 | 4,366,886 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 2,910,484 | 4,367,023 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 2,910,484 | 4,366,671 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 2,910,484 | 4,367,075 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 2,910,564 | 4,366,918 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 2,942,484 | 4,367,066 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 2,926,484 | 4,366,959 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 2,926,484 | 4,367,202 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 2,910,486 | 4,367,118 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 2,926,482 | 4,375,976 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 2,910,484 | 4,375,772 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 2,910,485 | 4,375,852 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 2,910,486 | 4,375,978 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 2,926,532 | 4,375,792 | 54,330,721 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 2,910,483 | 4,147,726 | 54,424,662 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 2,910,483 | 4,147,556 | 54,424,689 |

## cutoff

| Commit | Date | Subject | dotnet FSharp.Data.Adaptive | dotnet R3 | dotnet Ranvier | node Fable.Ripple | node Ranvier | node-jitless Fable.Ripple | node-jitless Ranvier |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 1,181 | 50 | 111 | 51 | 35 | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 1,181 | 50 | 111 | 51 | 35 | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 1,181 | 50 | 111 | 51 | 35 | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 1,181 | 50 | 111 | 51 | 35 | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 1,181 | 50 | 111 | 51 | 35 | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 1,181 | 50 | 111 | 51 | 35 | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 1,181 | 50 | 111 | 51 | 35 | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 1,181 | 50 | 111 | 65 | 35 | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 1,181 | 50 | 111 | 65 | 35 | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 1,181 | 50 | 111 | 65 | 35 | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 1,181 | 50 | 111 | 65 | 35 | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 1,181 | 50 | 111 | 65 | 35 | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 1,181 | 50 | 111 | 65 | 35 | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 1,181 | 50 | 111 | 65 | 35 | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 1,181 | 50 | 111 | 65 | 35 | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 1,181 | 50 | 111 | 65 | 35 | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 1,181 | 50 | 111 | 65 | 35 | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 1,181 | 50 | 111 | 65 | 35 | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 1,182 | 50 | 111 | 65 | 35 | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |

## derive-effect

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 765 | -1,632 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 765 | -1,632 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 765 | -1,632 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 765 | -1,632 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 765 | -1,632 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 765 | -807 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 765 | -2,002 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 765 | 716 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 765 | 716 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 765 | 716 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 765 | 716 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 765 | 716 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 765 | 716 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 765 | 716 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 765 | 716 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 765 | 716 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 765 | 716 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 765 | 716 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 760 | 680 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 760 | 680 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 760 | 680 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 760 | 680 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 760 | 680 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 760 | 680 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 760 | 680 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 760 | 680 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 760 | 680 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 760 | 680 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 760 | 680 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 760 | 680 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 760 | 680 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 760 | 680 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 765 | 699 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 765 | 699 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 765 | 699 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 765 | 699 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 765 | 699 | 15,940 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 765 | 768 | 16,102 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 765 | 768 | 16,102 |

## derive-on

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 740 | -1,380 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 740 | -1,380 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 740 | -1,380 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 740 | -1,380 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 756 | -1,380 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 756 | -798 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 740 | -1,541 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 740 | 684 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 756 | 684 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 740 | 684 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 740 | 684 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 740 | 684 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 740 | 684 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 756 | 684 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 740 | 684 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 740 | 684 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 740 | 684 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 740 | 684 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 735 | 684 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 751 | 684 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 735 | 684 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 735 | 684 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 735 | 684 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 735 | 684 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 735 | 684 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 735 | 684 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 735 | 684 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 735 | 684 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 767 | 684 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 751 | 684 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 735 | 684 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 735 | 684 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 753 | 694 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 737 | 694 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 737 | 694 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 737 | 694 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 753 | 694 | 16,099 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 737 | 775 | 16,261 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 737 | 775 | 16,261 |

## dispose

| Commit | Date | Subject | dotnet FSharp.Data.Adaptive | dotnet R3 | dotnet Ranvier | node Fable.Ripple | node Ranvier | node-jitless Fable.Ripple | node-jitless Ranvier |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 2,707,580 | 806,291 | 498,486 | 471,252 | 1,027,798 | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 2,707,580 | 806,291 | 498,486 | 471,252 | 1,027,669 | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 2,706,689 | 806,290 | 498,486 | 471,252 | 1,027,643 | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 2,707,581 | 806,291 | 498,486 | 471,252 | 1,027,470 | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 2,706,102 | 806,292 | 498,486 | 471,252 | 1,026,446 | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 2,707,584 | 806,293 | 498,486 | 471,082 | 1,037,758 | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 2,707,581 | 806,288 | 498,486 | 471,238 | 787,101 | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 2,706,184 | 806,288 | 498,486 | 465,871 | 703,008 | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 2,707,578 | 806,290 | 498,486 | 466,112 | 703,398 | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 2,707,581 | 806,290 | 498,486 | 465,769 | 703,182 | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 2,707,581 | 806,290 | 498,486 | 466,180 | 703,456 | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 2,707,581 | 806,291 | 498,486 | 466,014 | 703,538 | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 2,706,689 | 806,291 | 498,486 | 466,060 | 702,086 | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 2,707,580 | 806,291 | 498,486 | 466,194 | 703,651 | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 2,706,181 | 806,290 | 501,497 | 466,276 | 702,782 | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 2,707,580 | 806,291 | 501,497 | 466,038 | 702,173 | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 2,707,579 | 806,291 | 501,497 | 466,263 | 704,526 | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 2,706,184 | 806,290 | 501,497 | 466,110 | 704,570 | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 2,706,750 | 806,290 | 367,497 | 466,306 | 434,416 | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 2,706,242 | 806,291 | 367,497 | 466,002 | 434,253 | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 2,706,248 | 806,291 | 367,497 | 466,242 | 434,019 | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 2,706,244 | 806,291 | 367,497 | 466,122 | 434,369 | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 2,706,235 | 806,291 | 367,497 | 466,120 | 434,101 | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 2,707,642 | 806,291 | 367,497 | 466,150 | 434,399 | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 2,706,245 | 806,291 | 367,497 | 466,179 | 434,221 | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 2,706,255 | 806,290 | 367,497 | 466,078 | 434,233 | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 2,706,755 | 806,290 | 367,497 | 466,198 | 434,098 | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 2,707,644 | 806,290 | 367,497 | 466,421 | 434,430 | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 2,706,244 | 806,290 | 367,497 | 466,182 | 434,518 | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 2,706,242 | 806,291 | 367,497 | 466,205 | 434,132 | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 2,707,640 | 806,291 | 367,497 | 466,421 | 434,170 | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 2,707,647 | 806,291 | 367,497 | 466,419 | 434,178 | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 2,706,750 | 806,291 | 367,497 | 466,097 | 434,568 | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 2,706,752 | 806,291 | 367,497 | 466,200 | 434,499 | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 2,707,646 | 806,290 | 367,497 | 465,980 | 434,616 | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 2,707,644 | 806,291 | 367,497 | 465,967 | 434,888 | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 2,706,241 | 806,290 | 367,497 | 466,249 | 434,395 | 13,195,661 | 9,112,722 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 2,707,644 | 806,291 | 367,497 | 466,067 | 485,210 | 13,195,163 | 9,105,961 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 2,707,643 | 806,291 | 367,497 | 465,931 | 485,220 | 13,195,546 | 9,106,491 |

## dispose-on

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 477,486 | 1,127,685 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 477,486 | 1,141,547 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 477,486 | 1,127,690 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 477,486 | 1,141,638 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 477,486 | 1,087,005 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 477,486 | 1,100,499 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 477,486 | 883,282 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 477,486 | 699,680 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 477,486 | 699,312 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 477,486 | 699,855 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 477,486 | 699,340 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 477,486 | 699,320 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 477,486 | 699,013 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 477,486 | 699,518 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 480,497 | 699,418 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 480,497 | 700,132 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 480,497 | 698,094 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 480,497 | 698,438 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 346,497 | 430,156 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 346,497 | 430,446 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 346,497 | 430,305 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 346,497 | 430,144 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 346,497 | 430,205 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 346,497 | 430,425 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 346,497 | 430,458 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 346,497 | 430,207 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 346,497 | 430,725 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 346,497 | 430,432 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 346,497 | 430,169 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 346,497 | 430,463 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 346,497 | 430,362 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 346,497 | 430,305 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 346,497 | 430,578 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 346,497 | 430,530 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 346,497 | 430,526 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 346,497 | 430,629 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 346,497 | 430,507 | 8,908,961 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 346,497 | 478,468 | 8,902,248 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 346,497 | 478,492 | 8,902,693 |

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
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 179,606 |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 179,606 |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 179,606 |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 179,606 |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 179,606 |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 179,606 |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 179,606 |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 179,606 |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 179,606 |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 179,606 |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 179,606 |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 179,606 |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 179,606 |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 180,106 |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 180,106 |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 180,106 |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 180,106 |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 180,106 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 180,106 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 180,106 |

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
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 230,755 |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 230,755 |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 230,755 |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 230,755 |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 230,755 |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 230,755 |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 230,755 |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 230,755 |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 230,755 |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 230,755 |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 230,755 |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 230,755 |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 230,755 |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 231,255 |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 231,255 |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 231,255 |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 231,255 |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 231,255 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 231,255 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 231,255 |

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
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 64,914 | 1,366 |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 64,914 | 1,366 |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 64,926 | 1,366 |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 64,926 | 1,366 |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 64,926 | 1,366 |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 64,950 | 1,366 |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 64,974 | 1,366 |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 64,974 | 1,366 |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 64,974 | 1,366 |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 64,990 | 1,366 |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 64,990 | 1,366 |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 64,990 | 1,366 |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 64,930 | 1,366 |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 64,946 | 1,366 |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 64,946 | 1,366 |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 64,914 | 1,366 |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 64,930 | 1,366 |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 65,098 | 1,366 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 65,098 | 1,366 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 65,082 | 1,366 |

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
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 11,041 | 6,240 | 6,241 | 11,365 | 10,809 | 22,373 |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 11,041 | 6,240 | 6,241 | 11,364 | 10,809 | 22,373 |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 11,017 | 6,228 | 6,229 | 11,340 | 10,785 | 22,371 |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 11,017 | 6,228 | 6,229 | 11,340 | 10,785 | 22,371 |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 11,017 | 6,228 | 6,229 | 11,340 | 10,785 | 22,371 |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 11,017 | 6,228 | 6,229 | 11,340 | 10,785 | 22,371 |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 10,999 | 6,217 | 6,218 | 11,320 | 10,766 | 22,345 |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 10,999 | 6,217 | 6,218 | 11,320 | 10,767 | 22,345 |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 10,999 | 6,217 | 6,218 | 11,320 | 10,767 | 22,345 |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 10,993 | 6,216 | 6,217 | 11,318 | 10,765 | 22,349 |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 10,993 | 6,216 | 6,217 | 11,318 | 10,765 | 22,349 |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 10,993 | 6,216 | 6,217 | 11,318 | 10,765 | 22,349 |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 10,995 | 6,215 | 6,216 | 11,316 | 10,763 | 22,341 |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 10,995 | 6,215 | 6,216 | 11,316 | 10,763 | 22,343 |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 10,995 | 6,215 | 6,216 | 11,316 | 10,763 | 22,343 |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 10,995 | 6,215 | 6,216 | 11,316 | 10,763 | 22,343 |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 10,995 | 6,215 | 6,216 | 11,316 | 10,763 | 22,343 |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 10,995 | 6,215 | 6,216 | 11,316 | 10,763 | 22,343 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 10,995 | 6,215 | 6,216 | 11,316 | 10,763 | 22,343 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 10,995 | 6,215 | 6,216 | 11,316 | 10,763 | 22,343 |

## keyed-insert

| Commit | Date | Subject | dotnet Ranvier (fresh items) | dotnet Ranvier (memo-free rows) | dotnet Ranvier (stable items) | node Ranvier (fresh items) | node Ranvier (memo-free rows) | node Ranvier (stable items) | node-jitless Ranvier (fresh items) | node-jitless Ranvier (memo-free rows) | node-jitless Ranvier (stable items) |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 1,944,611 | — | 73,358 | 4,548,866 | — | 214,055 | — | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 1,944,587 | — | 73,334 | 4,548,960 | — | 214,109 | — | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 1,944,587 | — | 73,334 | 4,548,807 | — | 213,809 | — | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 1,944,587 | — | 73,334 | 4,543,663 | — | 213,991 | — | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 1,944,609 | 72,667 | 73,324 | 4,547,218 | 246,069 | 214,072 | — | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 1,921,657 | 54,104 | 54,752 | 4,543,583 | 209,597 | 194,141 | — | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 1,921,671 | 54,104 | 54,767 | 4,296,964 | 209,171 | 192,218 | — | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 1,921,560 | 53,846 | 54,581 | 3,092,666 | 162,633 | 172,627 | — | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 1,921,560 | 53,846 | 54,582 | 3,092,565 | 162,828 | 172,579 | — | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 1,921,603 | 53,864 | 54,597 | 3,092,660 | 162,680 | 172,520 | — | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 1,921,590 | 53,936 | 54,674 | 3,092,673 | 163,160 | 172,920 | — | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 1,921,612 | 53,936 | 54,698 | 3,092,648 | 163,142 | 173,032 | — | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 1,921,599 | 53,927 | 54,686 | 3,092,666 | 163,127 | 173,069 | — | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 1,921,590 | 53,946 | 54,675 | 3,092,613 | 163,149 | 173,067 | — | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 1,921,588 | 53,976 | 54,699 | 3,092,666 | 163,057 | 173,057 | — | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 1,921,573 | 53,966 | 54,665 | 3,092,872 | 162,911 | 172,991 | — | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 1,921,568 | 53,934 | 54,623 | 3,092,994 | 163,085 | 172,557 | — | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 1,921,766 | 53,941 | 54,668 | 3,092,947 | 163,151 | 172,662 | — | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 1,918,543 | 53,948 | 54,624 | 3,114,971 | 164,122 | 167,165 | — | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 1,918,527 | 53,948 | 54,610 | 3,115,059 | 164,083 | 167,374 | — | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 1,918,527 | 53,980 | 54,609 | 3,115,019 | 164,104 | 166,925 | — | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 1,918,552 | 53,949 | 54,625 | 3,115,013 | 164,072 | 167,272 | — | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 1,918,536 | 53,948 | 54,611 | 3,115,088 | 164,098 | 167,471 | — | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 1,918,536 | 53,948 | 54,610 | 3,115,027 | 163,853 | 167,236 | — | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 1,918,536 | 53,948 | 54,610 | 3,115,210 | 164,120 | 167,409 | — | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 1,918,536 | 53,948 | 54,610 | 3,115,145 | 164,071 | 167,354 | — | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 1,918,538 | 53,948 | 54,610 | 3,115,288 | 164,080 | 167,386 | — | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 1,918,672 | 54,044 | 54,745 | 3,115,213 | 164,099 | 167,492 | — | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 1,918,533 | 53,949 | 54,613 | 3,115,365 | 164,077 | 167,439 | — | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 1,918,533 | 53,949 | 54,614 | 3,115,302 | 164,107 | 167,393 | — | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 1,918,534 | 53,949 | 54,613 | 3,115,265 | 164,106 | 167,361 | — | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 1,918,525 | 53,950 | 54,613 | 3,115,354 | 164,055 | 167,126 | — | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 1,921,810 | 53,951 | 54,951 | 3,164,266 | 167,267 | 167,379 | — | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 1,921,810 | 53,951 | 54,950 | 3,164,403 | 167,255 | 167,103 | — | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 1,921,825 | 53,971 | 54,964 | 3,164,362 | 167,302 | 167,653 | — | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 1,921,809 | 53,951 | 54,946 | 3,164,311 | 167,299 | 167,724 | — | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 1,921,905 | 53,951 | 55,044 | 3,164,391 | 167,252 | 167,625 | 44,661,301 | 2,639,507 | 2,657,596 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 1,921,809 | 53,951 | 54,949 | 3,218,687 | 661,692 | 176,576 | 44,849,813 | 2,634,960 | 2,654,184 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 1,921,809 | 53,971 | 54,948 | 3,218,737 | 661,695 | 176,469 | 44,849,847 | 2,634,939 | 2,654,314 |

## keyed-remove

| Commit | Date | Subject | dotnet Ranvier (fresh items) | dotnet Ranvier (memo-free rows) | dotnet Ranvier (stable items) | node Ranvier (fresh items) | node Ranvier (memo-free rows) | node Ranvier (stable items) | node-jitless Ranvier (fresh items) | node-jitless Ranvier (memo-free rows) | node-jitless Ranvier (stable items) |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 1,943,561 | — | 66,481 | 4,150,967 | — | 187,077 | — | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 1,943,538 | — | 66,457 | 4,154,900 | — | 184,034 | — | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 1,943,553 | — | 66,463 | 4,153,664 | — | 181,760 | — | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 1,943,547 | — | 66,463 | 4,152,875 | — | 184,851 | — | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 1,943,547 | 65,120 | 66,456 | 4,146,197 | 272,464 | 182,669 | — | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 1,920,611 | 43,808 | 45,128 | 4,333,715 | 165,760 | 123,085 | — | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 1,920,614 | 43,793 | 45,145 | 3,746,188 | 164,238 | 114,438 | — | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 1,920,500 | 43,539 | 44,963 | 3,092,572 | 130,252 | 142,773 | — | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 1,920,789 | 43,539 | 45,100 | 3,092,513 | 130,276 | 142,726 | — | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 1,920,550 | 43,566 | 44,995 | 3,092,470 | 130,256 | 142,766 | — | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 1,920,537 | 43,638 | 45,058 | 3,092,529 | 130,695 | 143,307 | — | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 1,920,550 | 43,638 | 45,081 | 3,092,463 | 130,641 | 143,342 | — | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 1,920,541 | 43,629 | 45,066 | 3,092,569 | 130,837 | 143,257 | — | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 1,920,532 | 43,647 | 45,058 | 3,092,431 | 130,613 | 143,361 | — | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 1,920,547 | 43,635 | 45,050 | 3,092,537 | 132,187 | 143,308 | — | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 1,920,533 | 43,626 | 45,039 | 3,092,718 | 132,010 | 143,311 | — | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 1,920,519 | 43,626 | 45,007 | 3,092,959 | 134,022 | 143,253 | — | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 1,920,716 | 43,632 | 45,046 | 3,092,992 | 134,114 | 143,433 | — | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 1,917,460 | 43,626 | 45,002 | 3,114,557 | 131,766 | 140,540 | — | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 1,917,458 | 43,626 | 44,988 | 3,114,556 | 131,915 | 140,265 | — | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 1,917,448 | 43,659 | 44,987 | 3,114,552 | 131,804 | 140,487 | — | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 1,917,471 | 43,618 | 44,988 | 3,114,478 | 131,272 | 139,998 | — | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 1,917,459 | 43,618 | 44,972 | 3,114,491 | 131,586 | 139,989 | — | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 1,917,458 | 43,617 | 44,971 | 3,114,439 | 131,414 | 139,928 | — | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 1,917,454 | 43,617 | 44,971 | 3,114,459 | 131,648 | 139,888 | — | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 1,917,455 | 43,618 | 44,972 | 3,114,492 | 131,716 | 139,915 | — | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 1,917,462 | 43,618 | 44,970 | 3,114,568 | 132,052 | 139,972 | — | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 1,917,586 | 43,714 | 45,108 | 3,114,500 | 131,444 | 139,977 | — | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 1,917,459 | 43,619 | 44,974 | 3,115,016 | 131,917 | 140,743 | — | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 1,917,466 | 43,619 | 44,975 | 3,115,030 | 131,871 | 140,793 | — | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 1,917,462 | 43,619 | 44,974 | 3,115,023 | 131,915 | 140,812 | — | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 1,917,458 | 43,625 | 44,990 | 3,114,969 | 131,923 | 140,729 | — | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 1,920,738 | 43,628 | 45,285 | 3,163,821 | 133,239 | 140,975 | — | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 1,920,740 | 43,628 | 45,284 | 3,163,814 | 133,127 | 141,021 | — | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 1,920,761 | 43,648 | 45,300 | 3,163,880 | 133,139 | 140,930 | — | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 1,920,729 | 43,628 | 45,284 | 3,163,876 | 133,201 | 141,010 | — | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 1,920,844 | 43,628 | 45,381 | 3,163,763 | 133,188 | 140,934 | 44,623,063 | 1,929,588 | 1,962,426 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 1,920,755 | 43,628 | 45,284 | 3,245,043 | 593,156 | 605,276 | 44,798,164 | 1,924,878 | 1,958,567 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 1,920,742 | 43,648 | 45,284 | 3,245,029 | 593,013 | 605,193 | 44,798,627 | 1,924,937 | 1,958,580 |

## keyed-swap

| Commit | Date | Subject | dotnet Ranvier (fresh items) | dotnet Ranvier (memo-free rows) | dotnet Ranvier (stable items) | node Ranvier (fresh items) | node Ranvier (memo-free rows) | node Ranvier (stable items) | node-jitless Ranvier (fresh items) | node-jitless Ranvier (memo-free rows) | node-jitless Ranvier (stable items) |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 1,996,310 | — | 71,544 | 4,492,185 | — | 181,044 | — | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 1,996,294 | — | 71,528 | 4,491,416 | — | 181,067 | — | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 1,996,294 | — | 71,528 | 4,492,820 | — | 181,072 | — | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 1,996,294 | — | 71,528 | 4,494,937 | — | 179,703 | — | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 1,996,301 | 71,111 | 71,526 | 4,491,295 | 229,835 | 179,487 | — | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 1,973,263 | 46,335 | 46,760 | 4,469,743 | 98,302 | 82,515 | — | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 1,973,283 | 46,335 | 46,776 | 4,233,627 | 98,015 | 81,077 | — | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 1,973,152 | 46,080 | 46,557 | 3,215,194 | 76,966 | 83,156 | — | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 1,973,152 | 46,081 | 46,558 | 3,215,157 | 77,033 | 82,398 | — | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 1,973,185 | 46,098 | 46,574 | 3,215,219 | 76,971 | 83,291 | — | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) (accepted) | 1,973,171 | 46,167 | 46,649 | 3,215,270 | 79,489 | 84,811 | — | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 1,973,203 | 46,167 | 46,681 | 3,215,232 | 79,407 | 84,869 | — | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 1,973,189 | 46,167 | 46,667 | 3,215,206 | 79,449 | 85,165 | — | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 1,973,171 | 46,168 | 46,649 | 3,215,194 | 79,487 | 84,995 | — | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 1,973,202 | 46,167 | 46,667 | 3,215,181 | 79,316 | 84,944 | — | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 1,973,184 | 46,167 | 46,649 | 3,215,502 | 79,183 | 85,189 | — | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 1,973,171 | 46,168 | 46,649 | 3,215,847 | 79,219 | 84,875 | — | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 1,973,379 | 46,183 | 46,699 | 3,215,874 | 79,374 | 84,887 | — | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 1,970,160 | 46,161 | 46,638 | 3,264,730 | 79,254 | 84,519 | — | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 1,970,144 | 46,160 | 46,622 | 3,264,638 | 79,189 | 84,515 | — | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 1,970,144 | 46,192 | 46,623 | 3,264,757 | 79,143 | 84,492 | — | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 1,970,170 | 46,160 | 46,639 | 3,264,855 | 79,104 | 84,546 | — | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 1,970,154 | 46,161 | 46,624 | 3,264,892 | 79,137 | 84,571 | — | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 1,970,154 | 46,161 | 46,624 | 3,264,879 | 79,228 | 84,527 | — | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 1,970,154 | 46,161 | 46,624 | 3,264,823 | 79,109 | 84,534 | — | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 1,970,154 | 46,160 | 46,623 | 3,264,936 | 79,083 | 84,571 | — | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 1,970,155 | 46,160 | 46,624 | 3,264,995 | 79,178 | 84,538 | — | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 1,970,250 | 46,256 | 46,719 | 3,265,024 | 79,175 | 84,567 | — | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 1,970,155 | 46,162 | 46,624 | 3,264,841 | 79,090 | 84,442 | — | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 1,970,155 | 46,161 | 46,624 | 3,264,869 | 79,079 | 84,420 | — | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 1,970,156 | 46,161 | 46,624 | 3,264,836 | 79,032 | 84,334 | — | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 1,970,143 | 46,161 | 46,624 | 3,264,831 | 79,023 | 84,420 | — | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 1,973,429 | 46,161 | 46,916 | 3,282,926 | 80,397 | 85,105 | — | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 1,973,429 | 46,162 | 46,916 | 3,282,896 | 80,397 | 85,137 | — | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 1,973,444 | 46,177 | 46,931 | 3,282,864 | 80,396 | 85,129 | — | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 1,973,428 | 46,162 | 46,916 | 3,282,926 | 80,399 | 85,118 | — | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 1,973,524 | 46,161 | 47,011 | 3,282,982 | 80,381 | 85,111 | 45,819,080 | 919,448 | 935,758 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 1,973,428 | 46,161 | 46,916 | 3,566,419 | 148,779 | 511,233 | 45,980,315 | 916,555 | 932,783 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 1,973,428 | 46,177 | 46,916 | 3,565,260 | 148,814 | 511,392 | 45,980,289 | 916,542 | 932,942 |

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
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 58,066 | 767 |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 58,066 | 767 |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 58,066 | 767 |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 58,066 | 767 |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 58,066 | 767 |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 58,066 | 767 |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 58,066 | 767 |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 58,066 | 767 |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 58,066 | 767 |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 58,066 | 767 |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 58,067 | 767 |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 58,067 | 767 |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 58,067 | 767 |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 58,261 | 772 |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 58,261 | 772 |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 58,261 | 772 |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 58,261 | 772 |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 58,261 | 772 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 58,261 | 772 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 58,261 | 772 |

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
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 3,834,745 |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 3,834,824 |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 3,834,791 |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 3,834,809 |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 3,834,823 |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 3,834,800 |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 3,834,828 |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 3,834,822 |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 3,834,796 |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 3,843,198 |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 3,843,167 |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 3,843,182 |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 3,843,463 |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 3,844,584 |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 3,844,591 |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 3,844,844 |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 1,070,308 |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 1,070,194 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 1,070,368 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 1,070,317 |

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
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 36,206 | 34,048 |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 36,206 | 34,048 |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 36,207 | 34,047 |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 36,207 | 34,047 |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 36,207 | 34,047 |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 36,207 | 34,047 |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 36,207 | 34,047 |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 36,207 | 34,047 |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 36,207 | 34,047 |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 36,213 | 34,051 |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 36,213 | 34,051 |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 36,213 | 34,051 |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 36,212 | 34,069 |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 36,489 | 34,326 |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 36,505 | 34,342 |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 36,489 | 34,326 |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 36,473 | 34,310 |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 36,473 | 34,310 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 36,473 | 34,310 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 36,489 | 34,326 |

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
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 50,165 |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 50,165 |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 50,166 |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 50,166 |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 50,166 |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 50,182 |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 50,166 |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 50,182 |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 50,198 |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 50,168 |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 50,168 |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 50,168 |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 50,167 |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 50,431 |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 50,431 |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 50,431 |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 50,415 |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 50,431 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 50,415 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 50,431 |

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
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 778,899 |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 778,901 |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 778,884 |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 778,884 |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 778,886 |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 778,902 |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 778,886 |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 778,885 |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 778,886 |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 778,887 |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 778,886 |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 778,886 |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 778,899 |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 779,171 |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 779,171 |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 779,171 |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 779,156 |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 779,170 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 779,155 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 779,172 |

## server-render

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | — | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | — | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | — | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | — | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | — | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | — | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | — | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | — | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | — | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | — | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | — | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | — | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | — | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | — | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | — | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | — | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | — | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | — | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | — | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | — | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | — | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | — | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | — | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | — | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | — | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | — | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | — | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | — | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | — | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | — | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | — | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 677,749 | 1,130,753 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 676,332 | 1,131,247 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 676,332 | 1,131,418 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 677,932 | 1,131,206 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 676,332 | 1,131,179 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 676,396 | 1,131,282 | 9,832,906 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 677,932 | 1,131,263 | 9,832,850 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 676,364 | 1,131,339 | 9,833,276 |

## shape-diamond

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 60,899 | 116,578 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 60,899 | 116,575 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 60,899 | 116,578 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 60,899 | 116,611 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 60,899 | 116,579 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 60,899 | 117,208 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 60,899 | 106,625 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 60,899 | 88,455 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 60,899 | 88,455 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 60,899 | 88,455 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 60,899 | 88,455 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 60,899 | 88,455 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 60,899 | 88,455 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 60,899 | 88,455 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 60,899 | 88,455 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 60,899 | 88,455 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 60,899 | 88,455 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 60,899 | 88,455 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 59,894 | 88,455 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 59,894 | 88,455 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 59,894 | 88,455 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 59,894 | 88,557 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 59,894 | 88,557 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 59,894 | 88,557 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 59,894 | 88,557 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 59,894 | 88,557 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 59,894 | 88,557 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 59,894 | 88,557 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 59,894 | 88,557 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 59,894 | 88,557 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 59,894 | 88,557 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 59,894 | 88,557 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 59,899 | 88,772 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 59,899 | 88,772 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 59,899 | 88,772 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 59,899 | 88,772 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 59,899 | 88,772 | 1,763,207 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 59,899 | 79,968 | 1,763,357 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 59,899 | 79,968 | 1,763,357 |

## shape-dynamic

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 82,420 | -24,661 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 82,420 | -24,701 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 82,420 | -24,728 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 82,420 | -24,425 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 82,420 | -24,744 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 82,420 | -19,948 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 82,420 | -130,233 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 82,420 | 97,997 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 82,420 | 98,007 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 82,420 | 98,000 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 82,420 | 98,007 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 82,420 | 98,003 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 82,420 | 98,003 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 82,420 | 98,007 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 83,732 | 98,003 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 82,420 | 98,001 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 82,420 | 98,000 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 82,420 | 98,000 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 82,520 | 95,378 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 82,520 | 95,381 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 82,520 | 95,381 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 82,520 | 95,580 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 82,520 | 95,583 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 82,520 | 95,581 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 82,520 | 95,583 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 82,520 | 95,582 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 82,520 | 95,581 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 82,520 | 95,583 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 82,520 | 95,580 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 82,520 | 95,575 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 82,520 | 95,572 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 82,520 | 95,577 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 82,921 | 96,580 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 82,921 | 96,576 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 82,921 | 96,578 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 82,921 | 96,580 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 82,921 | 96,577 | 1,992,771 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 82,921 | 107,033 | 2,006,140 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 82,921 | 107,033 | 2,006,140 |

## stream-burst-1

| Commit | Date | Subject | dotnet Ranvier (AsyncSource, batched) | dotnet Ranvier (AsyncSource, unbatched) | dotnet Ranvier (stream, batched) | node Ranvier (AsyncSource, batched) | node Ranvier (AsyncSource, unbatched) | node Ranvier (stream, batched) | node-jitless Ranvier (AsyncSource, batched) | node-jitless Ranvier (AsyncSource, unbatched) | node-jitless Ranvier (stream, batched) |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | — | — | — | — | — | — | — | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | — | — | — | — | — | — | — | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | — | — | — | — | — | — | — | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | — | — | — | — | — | — | — | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | — | — | — | — | — | — | — | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | — | — | — | — | — | — | — | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | — | — | — | — | — | — | — | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | — | — | — | — | — | — | — | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | — | — | — | — | — | — | — | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | — | — | — | — | — | — | — | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | — | — | — | — | — | — | — | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | — | — | — | — | — | — | — | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | — | — | — | — | — | — | — | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | — | — | — | — | — | — | — | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | — | — | — | — | — | — | — | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | — | — | — | — | — | — | — | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | — | — | — | — | — | — | — | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | — | — | — | — | — | — | — | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | — | — | — | — | — | — | — | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | — | — | — | — | — | — | — | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | — | — | — | — | — | — | — | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 1,969 | 1,567 | 2,500 | 5,235 | 6,299 | 10,091 | — | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 1,969 | 1,567 | 2,500 | 5,235 | 6,299 | 10,088 | — | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 1,969 | 1,567 | 2,500 | 5,235 | 6,299 | 10,091 | — | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) (accepted) | 1,969 | 1,567 | 2,485 | 5,234 | 6,299 | 10,434 | — | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 1,969 | 1,567 | 2,485 | 5,235 | 6,299 | 10,438 | — | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 1,969 | 1,567 | 2,485 | 5,233 | 6,299 | 10,438 | — | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 1,969 | 1,567 | 2,485 | 5,235 | 6,299 | 10,436 | — | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 1,969 | 1,567 | 2,486 | 5,232 | 6,299 | 10,434 | — | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 1,969 | 1,567 | 2,486 | 5,235 | 6,299 | 10,436 | — | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 1,969 | 1,567 | 2,486 | 5,233 | 6,299 | 10,438 | — | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 1,969 | 1,567 | 2,486 | 5,235 | 6,299 | 10,438 | — | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 1,974 | 1,572 | 2,490 | 5,260 | 6,373 | 10,444 | — | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 1,974 | 1,572 | 2,490 | 5,258 | 6,373 | 10,446 | — | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 1,974 | 1,572 | 2,490 | 5,260 | 6,373 | 10,444 | — | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 1,974 | 1,572 | 2,491 | 5,260 | 6,373 | 10,446 | — | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 1,974 | 1,572 | 2,491 | 5,260 | 6,373 | 10,446 | 47,094 | 45,312 | 54,896 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 1,974 | 1,572 | 2,491 | 4,672 | 3,644 | 13,372 | 47,201 | 45,417 | 55,171 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 1,974 | 1,572 | 2,491 | 4,670 | 3,644 | 13,366 | 47,201 | 45,417 | 55,168 |

## stream-burst-16

| Commit | Date | Subject | dotnet Ranvier (AsyncSource, batched) | dotnet Ranvier (AsyncSource, unbatched) | dotnet Ranvier (stream, batched) | node Ranvier (AsyncSource, batched) | node Ranvier (AsyncSource, unbatched) | node Ranvier (stream, batched) | node-jitless Ranvier (AsyncSource, batched) | node-jitless Ranvier (AsyncSource, unbatched) | node-jitless Ranvier (stream, batched) |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | — | — | — | — | — | — | — | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | — | — | — | — | — | — | — | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | — | — | — | — | — | — | — | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | — | — | — | — | — | — | — | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | — | — | — | — | — | — | — | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | — | — | — | — | — | — | — | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | — | — | — | — | — | — | — | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | — | — | — | — | — | — | — | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | — | — | — | — | — | — | — | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | — | — | — | — | — | — | — | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | — | — | — | — | — | — | — | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | — | — | — | — | — | — | — | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | — | — | — | — | — | — | — | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | — | — | — | — | — | — | — | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | — | — | — | — | — | — | — | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | — | — | — | — | — | — | — | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | — | — | — | — | — | — | — | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | — | — | — | — | — | — | — | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | — | — | — | — | — | — | — | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | — | — | — | — | — | — | — | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | — | — | — | — | — | — | — | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 5,626 | 24,937 | 5,710 | 10,349 | 47,734 | 12,740 | — | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 5,626 | 24,937 | 5,710 | 10,353 | 47,730 | 12,744 | — | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 5,626 | 24,937 | 5,710 | 10,350 | 47,734 | 12,742 | — | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) (accepted) | 5,626 | 24,937 | 5,695 | 10,350 | 47,734 | 13,089 | — | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 5,626 | 24,937 | 5,695 | 10,349 | 47,734 | 13,089 | — | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 5,626 | 24,937 | 5,695 | 10,349 | 47,734 | 13,089 | — | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 5,626 | 24,937 | 5,695 | 10,346 | 47,734 | 13,089 | — | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 5,626 | 24,937 | 5,696 | 10,347 | 47,734 | 13,087 | — | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 5,626 | 24,937 | 5,696 | 10,351 | 47,732 | 13,089 | — | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 5,626 | 24,937 | 5,696 | 10,350 | 47,732 | 13,092 | — | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 5,626 | 24,937 | 5,696 | 10,350 | 47,734 | 13,089 | — | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) (accepted) | 5,631 | 25,017 | 5,700 | 10,631 | 48,406 | 13,097 | — | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 5,631 | 25,017 | 5,700 | 10,631 | 48,404 | 13,097 | — | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 5,631 | 25,017 | 5,700 | 10,631 | 48,403 | 13,097 | — | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 5,631 | 25,017 | 5,701 | 10,631 | 48,406 | 13,097 | — | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 5,631 | 25,017 | 5,701 | 10,632 | 48,403 | 13,097 | 120,864 | 712,439 | 113,248 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 5,631 | 25,017 | 5,701 | 9,499 | 49,601 | 8,050 | 120,973 | 714,180 | 113,522 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 5,631 | 25,017 | 5,701 | 9,502 | 49,601 | 8,051 | 120,973 | 714,180 | 113,519 |

## stream-burst-256

| Commit | Date | Subject | dotnet Ranvier (AsyncSource, batched) | dotnet Ranvier (AsyncSource, unbatched) | dotnet Ranvier (stream, batched) | node Ranvier (AsyncSource, batched) | node Ranvier (AsyncSource, unbatched) | node Ranvier (stream, batched) | node-jitless Ranvier (AsyncSource, batched) | node-jitless Ranvier (AsyncSource, unbatched) | node-jitless Ranvier (stream, batched) |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | — | — | — | — | — | — | — | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | — | — | — | — | — | — | — | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | — | — | — | — | — | — | — | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | — | — | — | — | — | — | — | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | — | — | — | — | — | — | — | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | — | — | — | — | — | — | — | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | — | — | — | — | — | — | — | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | — | — | — | — | — | — | — | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | — | — | — | — | — | — | — | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | — | — | — | — | — | — | — | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | — | — | — | — | — | — | — | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | — | — | — | — | — | — | — | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | — | — | — | — | — | — | — | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | — | — | — | — | — | — | — | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | — | — | — | — | — | — | — | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | — | — | — | — | — | — | — | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | — | — | — | — | — | — | — | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | — | — | — | — | — | — | — | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | — | — | — | — | — | — | — | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | — | — | — | — | — | — | — | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | — | — | — | — | — | — | — | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 64,126 | 398,821 | 57,049 | 119,395 | 792,637 | 70,591 | — | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 64,126 | 398,821 | 57,049 | 119,391 | 792,583 | 70,588 | — | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 64,126 | 398,821 | 57,049 | 119,395 | 792,558 | 70,588 | — | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 64,126 | 398,821 | 57,034 | 119,395 | 792,635 | 70,590 | — | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 64,126 | 398,821 | 57,034 | 119,307 | 792,572 | 70,590 | — | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 64,126 | 398,821 | 57,034 | 119,341 | 792,637 | 70,590 | — | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 64,126 | 398,821 | 57,034 | 119,302 | 792,633 | 70,590 | — | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 64,126 | 398,821 | 57,035 | 119,306 | 792,637 | 70,590 | — | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 64,126 | 398,821 | 57,035 | 119,391 | 792,627 | 70,590 | — | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 64,126 | 398,821 | 57,035 | 119,319 | 792,639 | 70,590 | — | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 64,126 | 398,821 | 57,035 | 119,372 | 792,642 | 70,590 | — | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) (accepted) | 64,131 | 400,101 | 57,039 | 123,003 | 795,709 | 70,598 | — | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 64,131 | 400,101 | 57,039 | 123,049 | 795,709 | 70,598 | — | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 64,131 | 400,101 | 57,039 | 123,029 | 795,710 | 70,601 | — | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 64,131 | 400,101 | 57,040 | 123,049 | 795,709 | 70,598 | — | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 64,131 | 400,101 | 57,040 | 122,983 | 795,712 | 70,598 | 1,301,559 | 11,385,440 | 1,046,210 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 64,131 | 400,101 | 57,040 | 127,472 | 787,260 | 74,603 | 1,301,576 | 11,413,287 | 1,046,550 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 64,131 | 400,101 | 57,040 | 127,421 | 787,259 | 74,595 | 1,301,529 | 11,413,287 | 1,046,482 |

## stream-restart

| Commit | Date | Subject | dotnet Ranvier (async memo, CancelPrevious) | dotnet Ranvier (stream) | node Ranvier (async memo, CancelPrevious) | node Ranvier (stream) | node-jitless Ranvier (async memo, CancelPrevious) | node-jitless Ranvier (stream) |
|---|---|---|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | — | — | — | — | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | — | — | — | — | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | — | — | — | — | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | — | — | — | — | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | — | — | — | — | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | — | — | — | — | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | — | — | — | — | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | — | — | — | — | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | — | — | — | — | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | — | — | — | — | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | — | — | — | — | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | — | — | — | — | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | — | — | — | — | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | — | — | — | — | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | — | — | — | — | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | — | — | — | — | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | — | — | — | — | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | — | — | — | — | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | — | — | — | — | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | — | — | — | — | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | — | — | — | — | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 4,187 | 4,980 | 12,700 | 8,950 | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 4,187 | 4,980 | 12,699 | 8,949 | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 4,187 | 4,980 | 12,698 | 8,949 | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 4,187 | 4,965 | 12,696 | 8,953 | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 4,184 | 4,965 | 12,436 | 8,950 | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 4,184 | 4,965 | 12,436 | 8,953 | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 4,184 | 4,981 | 12,436 | 8,951 | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 4,181 | 4,978 | 12,437 | 8,950 | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 4,181 | 4,962 | 12,433 | 8,952 | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 4,181 | 4,962 | 12,435 | 8,952 | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 4,183 | 4,982 | 12,433 | 8,951 | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 4,188 | 4,976 | 12,498 | 8,966 | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 4,188 | 4,996 | 12,496 | 8,972 | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 4,188 | 4,976 | 12,496 | 8,970 | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 4,188 | 4,976 | 12,498 | 8,970 | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 4,188 | 4,976 | 12,497 | 8,974 | 41,396 | 70,017 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 4,188 | 4,976 | 6,106 | 16,355 | 41,497 | 70,403 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 4,188 | 4,996 | 6,102 | 16,358 | 41,495 | 70,406 |

## timed-admit-both

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 130,748 | 166,599 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 130,748 | 166,581 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 130,748 | 166,630 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 133,820 | 166,570 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 130,748 | 166,851 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 131,772 | 166,599 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 130,748 | 150,665 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 118,909 | 133,573 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 119,933 | 133,572 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 118,909 | 133,573 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 118,909 | 133,573 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 118,909 | 133,569 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 118,909 | 133,570 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 118,909 | 133,573 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 118,909 | 133,571 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 118,909 | 133,569 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 118,909 | 133,650 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 118,909 | 133,655 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 118,269 | 133,685 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 120,317 | 133,684 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 120,317 | 133,688 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 118,269 | 133,817 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 118,269 | 133,827 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 119,293 | 133,819 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 118,269 | 133,815 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 118,269 | 133,820 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 118,269 | 133,820 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 118,269 | 133,823 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 120,317 | 133,817 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 118,269 | 133,819 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 118,269 | 133,817 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 118,269 | 133,821 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 118,591 | 135,174 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 118,591 | 135,172 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 120,639 | 135,172 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 118,591 | 135,174 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 119,615 | 135,166 | 2,653,179 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 118,591 | 134,967 | 2,679,929 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 118,591 | 134,965 | 2,679,929 |

## timed-admit-debounce

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 129,654 | 162,933 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 129,654 | 162,898 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 129,654 | 162,868 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 132,726 | 162,881 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 129,654 | 162,874 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 130,678 | 162,874 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 129,654 | 146,428 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 117,814 | 132,166 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 118,838 | 132,165 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 117,814 | 132,166 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 117,814 | 132,167 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 117,814 | 132,165 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 117,814 | 132,169 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 117,818 | 132,166 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 117,814 | 132,167 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 117,814 | 132,162 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 117,814 | 132,158 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 117,818 | 132,160 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 117,174 | 132,327 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 119,222 | 132,300 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 119,222 | 132,316 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 117,174 | 132,378 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 117,174 | 132,395 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 118,198 | 132,385 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 117,174 | 132,419 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 117,174 | 132,382 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 117,174 | 132,381 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 117,174 | 132,382 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 119,222 | 132,420 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 117,174 | 132,415 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 117,174 | 132,418 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 117,174 | 132,412 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 117,496 | 133,772 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 117,496 | 133,769 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 119,544 | 133,759 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 117,496 | 133,769 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 118,520 | 133,743 | 2,613,535 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 117,496 | 133,558 | 2,640,272 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 117,496 | 133,558 | 2,640,273 |

## timed-admit-first

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 76,517 | 101,552 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 76,517 | 101,552 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 76,517 | 101,552 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 78,565 | 101,552 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 76,517 | 101,552 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 77,541 | 101,552 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 76,517 | 93,948 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 69,925 | 81,964 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 70,949 | 81,964 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 69,925 | 81,962 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 69,925 | 81,964 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 69,925 | 81,963 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 69,925 | 81,964 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 69,925 | 81,964 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 69,925 | 81,964 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 69,925 | 81,964 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 69,925 | 81,964 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 69,925 | 81,964 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 69,285 | 82,092 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 71,333 | 82,091 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 70,309 | 82,092 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 69,285 | 82,222 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 69,285 | 82,222 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 70,309 | 82,222 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 69,285 | 82,222 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 69,285 | 82,222 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 69,285 | 82,220 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 69,285 | 82,222 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 70,309 | 82,222 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 69,285 | 82,222 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 69,285 | 82,221 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 69,285 | 82,222 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 69,479 | 82,869 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 69,479 | 82,868 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 71,527 | 82,869 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 69,479 | 82,869 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 70,503 | 82,868 | 1,594,156 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 69,479 | 79,052 | 1,611,692 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 69,479 | 79,053 | 1,611,692 |

## timed-admit-last

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 129,916 | 162,163 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 129,916 | 162,139 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 129,916 | 162,134 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 132,988 | 162,139 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 129,916 | 162,141 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 130,940 | 162,139 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 129,916 | 146,061 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 118,076 | 132,485 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 119,100 | 132,483 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 118,076 | 132,486 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 118,076 | 132,487 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 118,076 | 132,487 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 118,076 | 132,486 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 118,076 | 132,487 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 118,076 | 132,487 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 118,076 | 132,470 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 118,076 | 132,470 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 118,076 | 132,480 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 117,436 | 132,611 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 119,484 | 132,618 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 119,484 | 132,627 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 117,436 | 132,734 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 117,436 | 132,722 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 118,460 | 132,728 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 117,436 | 132,726 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 117,436 | 132,698 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 117,436 | 132,701 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 117,436 | 132,699 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 119,484 | 132,712 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 117,436 | 132,738 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 117,436 | 132,736 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 117,436 | 132,738 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 117,758 | 134,091 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 117,758 | 134,116 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 119,806 | 134,086 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 117,758 | 134,079 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 118,782 | 134,089 | 2,623,379 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 117,758 | 133,852 | 2,650,128 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 117,758 | 133,852 | 2,650,141 |

## timed-capture-both

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 42,700 | 52,067 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 42,700 | 52,067 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 42,700 | 52,067 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 43,724 | 52,067 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 42,700 | 52,067 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 42,700 | 52,065 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 42,700 | 50,543 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 36,044 | 44,956 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 36,044 | 44,958 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 36,044 | 44,954 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 36,044 | 44,959 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 36,044 | 44,957 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 36,044 | 44,957 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 36,048 | 44,957 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 36,044 | 44,956 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 36,044 | 44,965 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 36,044 | 44,981 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 36,044 | 44,978 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 35,724 | 45,028 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 36,748 | 45,029 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 36,748 | 45,029 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 35,724 | 45,094 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 35,724 | 45,092 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 35,724 | 45,094 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 35,724 | 45,094 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 35,724 | 45,021 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 35,724 | 45,022 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 35,724 | 45,022 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 35,724 | 45,095 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 35,724 | 45,095 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 35,724 | 45,092 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 35,724 | 45,095 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 35,726 | 45,087 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 35,726 | 45,078 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 35,726 | 45,087 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 35,726 | 45,087 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 35,726 | 45,098 | 837,335 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 35,726 | 44,805 | 846,997 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 35,726 | 44,808 | 846,997 |

## timed-capture-debounce

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 42,572 | 54,165 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 42,572 | 54,165 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 42,572 | 54,165 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 43,596 | 54,165 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 42,572 | 54,165 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 42,572 | 54,165 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 42,572 | 49,892 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 35,916 | 43,583 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 35,916 | 43,583 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 35,916 | 43,583 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 35,916 | 43,583 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 35,916 | 43,583 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 35,916 | 43,583 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 35,916 | 43,583 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 35,916 | 43,583 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 35,916 | 43,583 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 35,916 | 43,583 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 35,916 | 43,583 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 35,596 | 43,646 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 36,620 | 43,647 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 36,620 | 43,646 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 35,596 | 43,713 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 35,596 | 43,713 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 35,596 | 43,713 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 35,596 | 43,713 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 35,596 | 43,713 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 35,596 | 43,713 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 35,596 | 43,713 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 35,596 | 43,712 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 35,596 | 43,712 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 35,596 | 43,712 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 35,596 | 43,713 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 35,598 | 43,719 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 35,598 | 43,718 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 35,598 | 43,719 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 35,598 | 43,719 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 35,598 | 43,719 | 838,606 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 35,598 | 44,103 | 847,375 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 35,598 | 44,104 | 847,375 |

## timed-capture-first

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 37,772 | 47,381 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 37,772 | 47,381 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 37,772 | 47,381 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 38,796 | 47,381 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 37,772 | 47,381 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 37,772 | 47,381 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 37,772 | 45,452 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 32,396 | 38,804 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 32,396 | 38,804 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 32,396 | 38,802 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 32,396 | 38,804 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 32,396 | 38,804 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 32,396 | 38,804 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 32,396 | 38,804 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 32,396 | 38,804 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 32,396 | 38,804 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 32,396 | 38,804 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 32,396 | 38,804 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 32,076 | 38,868 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 33,100 | 38,868 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 33,100 | 38,868 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 32,076 | 38,934 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 32,076 | 38,934 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 32,076 | 38,934 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 32,076 | 38,934 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 32,076 | 38,934 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 32,076 | 38,934 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 32,076 | 38,933 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 32,076 | 38,934 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 32,076 | 38,934 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 32,076 | 38,934 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 32,076 | 38,934 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 32,078 | 39,004 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 32,078 | 39,003 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 32,078 | 39,004 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 32,078 | 39,004 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 32,078 | 39,003 | 736,153 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 32,078 | 36,612 | 745,370 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 32,078 | 36,612 | 745,370 |

## timed-capture-last

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 42,508 | 51,303 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 42,508 | 51,303 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 42,508 | 51,303 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 43,532 | 51,303 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 42,508 | 51,303 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 42,508 | 51,303 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 42,508 | 49,779 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 35,852 | 43,519 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 35,852 | 43,519 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 35,852 | 43,519 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 35,852 | 43,519 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 35,852 | 43,519 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 35,852 | 43,519 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 35,852 | 43,519 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 35,852 | 43,519 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 35,852 | 43,519 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 35,852 | 43,519 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 35,852 | 43,519 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 35,532 | 43,583 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 36,556 | 43,583 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 36,556 | 43,583 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 35,532 | 43,649 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 35,532 | 43,647 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 35,532 | 43,649 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 35,532 | 43,649 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 35,532 | 43,649 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 35,532 | 43,649 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 35,532 | 43,649 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 35,532 | 43,648 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 35,532 | 43,648 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 35,532 | 43,649 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 35,532 | 43,648 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 35,534 | 43,653 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 35,534 | 43,655 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 35,534 | 43,654 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 35,534 | 43,654 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 35,534 | 43,655 | 830,736 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 35,534 | 44,040 | 839,503 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 35,534 | 44,040 | 839,503 |

## update

| Commit | Date | Subject | dotnet FSharp.Data.Adaptive | dotnet R3 | dotnet Ranvier | node Fable.Ripple | node Ranvier | node-jitless Fable.Ripple | node-jitless Ranvier |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 1,090,280 | 38,805 | 83,005 | 472,334 | 377,510 | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 1,095,080 | 38,805 | 83,005 | 472,299 | 377,285 | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 1,090,280 | 38,805 | 83,005 | 472,399 | 377,202 | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 1,095,080 | 38,805 | 83,005 | 472,204 | 377,236 | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 1,088,680 | 38,805 | 83,005 | 471,711 | 377,241 | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 1,088,680 | 38,805 | 83,005 | 473,466 | 382,448 | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 1,088,680 | 38,805 | 83,005 | 472,939 | 362,204 | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 1,088,680 | 38,805 | 83,005 | 263,397 | 207,816 | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 1,090,272 | 38,805 | 83,005 | 263,444 | 207,813 | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 1,088,680 | 38,805 | 83,005 | 263,413 | 207,830 | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 1,090,272 | 38,805 | 83,005 | 263,459 | 207,781 | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 1,088,680 | 38,805 | 83,005 | 263,427 | 207,822 | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 1,091,872 | 38,805 | 83,005 | 263,428 | 207,810 | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 1,088,672 | 38,805 | 83,005 | 263,483 | 207,800 | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 1,088,680 | 38,805 | 83,005 | 263,408 | 207,795 | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 1,088,680 | 38,805 | 83,005 | 263,113 | 207,770 | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 1,090,280 | 38,805 | 83,005 | 262,840 | 207,848 | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 1,088,673 | 38,805 | 83,005 | 262,809 | 207,798 | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 1,087,690 | 38,805 | 82,005 | 262,586 | 207,601 | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 1,094,083 | 38,805 | 82,005 | 262,564 | 207,605 | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 1,087,683 | 38,805 | 82,005 | 262,714 | 207,579 | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 1,088,383 | 38,805 | 82,005 | 262,492 | 207,613 | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 1,089,983 | 38,805 | 82,005 | 262,790 | 207,577 | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 1,089,983 | 38,805 | 82,005 | 262,702 | 207,667 | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 1,088,390 | 38,805 | 82,005 | 262,864 | 207,640 | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 1,091,583 | 38,805 | 82,005 | 262,810 | 207,589 | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 1,088,390 | 38,805 | 82,005 | 262,616 | 207,667 | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 1,088,390 | 38,805 | 82,005 | 262,888 | 207,655 | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 1,089,983 | 38,805 | 82,005 | 262,549 | 207,612 | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 1,088,390 | 38,805 | 82,005 | 262,517 | 207,660 | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 1,088,390 | 38,805 | 82,005 | 262,630 | 207,629 | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 1,088,390 | 38,805 | 82,005 | 262,514 | 207,638 | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 1,087,690 | 38,805 | 82,505 | 262,572 | 209,535 | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 1,087,690 | 38,805 | 82,505 | 262,596 | 209,569 | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 1,087,690 | 38,805 | 82,505 | 262,800 | 209,569 | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 1,087,690 | 38,805 | 82,505 | 262,806 | 209,556 | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 1,087,690 | 38,805 | 82,505 | 262,360 | 209,567 | 3,350,233 | 2,489,232 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 1,089,283 | 38,805 | 82,505 | 262,529 | 151,711 | 3,350,289 | 2,502,802 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 1,087,683 | 38,805 | 82,505 | 262,474 | 151,703 | 3,350,265 | 2,502,833 |

## update-on

| Commit | Date | Subject | dotnet Ranvier | node Ranvier | node-jitless Ranvier |
|---|---|---|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 93,805 | 402,300 | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 93,805 | 402,349 | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 93,805 | 402,224 | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 93,805 | 402,195 | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 95,405 | 402,258 | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 95,405 | 407,658 | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 93,805 | 379,712 | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 93,805 | 222,107 | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 95,405 | 222,117 | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 93,805 | 222,082 | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 93,805 | 222,091 | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 93,805 | 222,096 | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 93,805 | 222,111 | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 95,405 | 222,096 | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 93,805 | 222,116 | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 93,805 | 222,065 | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 93,805 | 222,097 | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 93,805 | 222,064 | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 92,805 | 221,283 | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 94,405 | 221,303 | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 92,805 | 221,301 | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 92,805 | 221,308 | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 92,805 | 221,299 | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 92,805 | 221,299 | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 92,805 | 221,307 | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 92,805 | 221,305 | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 92,805 | 221,289 | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 92,805 | 221,297 | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 96,005 | 221,313 | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 94,405 | 221,334 | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 92,805 | 221,297 | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 92,805 | 221,275 | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 94,605 | 222,273 | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 93,005 | 222,262 | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 93,005 | 222,286 | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 93,005 | 222,299 | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 94,605 | 222,303 | 2,816,807 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 93,005 | 165,229 | 2,830,393 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 93,005 | 165,185 | 2,830,404 |

## work-form

| Commit | Date | Subject | dotnet Ranvier (createEffect) | dotnet Ranvier (createEffectOn) | node Ranvier (createEffect) | node Ranvier (createEffectOn) | node-jitless Ranvier (createEffect) | node-jitless Ranvier (createEffectOn) |
|---|---|---|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 174,678 | 168,144 | 340,679 | 309,540 | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 174,678 | 168,144 | 340,579 | 309,540 | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 174,678 | 168,144 | 340,750 | 309,540 | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 174,678 | 168,144 | 340,714 | 309,540 | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 174,678 | 168,144 | 340,961 | 309,519 | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 174,678 | 168,144 | 269,046 | 306,828 | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 174,678 | 168,144 | 405,151 | 291,380 | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 174,678 | 168,144 | 288,773 | 287,120 | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 174,678 | 168,161 | 288,770 | 287,116 | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 174,678 | 168,144 | 288,733 | 287,095 | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 174,678 | 168,144 | 288,766 | 287,120 | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 174,678 | 168,177 | 288,770 | 287,120 | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 174,678 | 168,144 | 288,761 | 287,120 | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 174,678 | 168,144 | 288,773 | 287,120 | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 174,678 | 168,163 | 288,724 | 287,115 | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 174,678 | 168,177 | 288,722 | 287,066 | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 174,678 | 172,144 | 288,766 | 287,118 | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 174,678 | 168,161 | 288,750 | 287,109 | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 169,672 | 163,139 | 288,752 | 287,018 | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 169,672 | 163,139 | 288,718 | 287,090 | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 169,672 | 163,139 | 288,772 | 287,114 | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 169,672 | 163,139 | 288,731 | 287,119 | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 169,672 | 163,139 | 288,743 | 287,087 | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 169,672 | 163,139 | 288,779 | 287,117 | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 169,672 | 163,139 | 288,739 | 287,069 | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 169,672 | 163,139 | 288,705 | 287,129 | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 169,672 | 163,139 | 288,794 | 287,138 | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 169,672 | 163,172 | 288,738 | 287,081 | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 169,672 | 163,139 | 288,761 | 287,093 | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 169,672 | 163,139 | 288,739 | 287,121 | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 169,672 | 163,139 | 288,716 | 287,084 | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 169,672 | 163,139 | 288,778 | 287,115 | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) (accepted) | 170,300 | 163,392 | 290,624 | 293,073 | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 170,300 | 163,392 | 290,635 | 293,046 | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 170,300 | 165,392 | 290,646 | 293,110 | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 170,300 | 163,392 | 290,634 | 293,092 | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 170,300 | 165,392 | 290,646 | 293,110 | 5,332,104 | 5,269,666 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 170,300 | 163,392 | 295,967 | 291,422 | 5,349,177 | 5,295,441 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 170,300 | 163,392 | 295,912 | 291,395 | 5,349,284 | 5,295,446 |

## work-status

| Commit | Date | Subject | dotnet Ranvier (createEffect) | dotnet Ranvier (createEffectOn) | node Ranvier (createEffect) | node Ranvier (createEffectOn) | node-jitless Ranvier (createEffect) | node-jitless Ranvier (createEffectOn) |
|---|---|---|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 100,445 | 79,115 | 712,634 | 976,643 | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 100,445 | 79,115 | 712,580 | 976,604 | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 100,445 | 79,115 | 712,610 | 976,647 | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 100,445 | 79,115 | 712,615 | 976,593 | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 100,445 | 80,715 | 712,424 | 976,952 | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 100,445 | 80,715 | 861,993 | 654,738 | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 100,445 | 79,115 | 616,750 | 919,678 | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 100,445 | 79,115 | 315,937 | 155,377 | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 100,445 | 80,715 | 315,963 | 155,385 | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 100,445 | 79,115 | 315,927 | 155,377 | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 100,445 | 79,115 | 315,923 | 155,422 | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 100,445 | 79,115 | 315,945 | 155,384 | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 100,445 | 79,115 | 315,962 | 155,370 | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 100,445 | 80,715 | 315,935 | 155,346 | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 100,445 | 79,115 | 315,942 | 155,383 | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 100,445 | 79,115 | 315,910 | 155,356 | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 100,445 | 79,116 | 315,892 | 155,398 | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 100,445 | 79,116 | 315,849 | 155,384 | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 99,945 | 78,615 | 320,915 | 155,412 | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 99,945 | 80,215 | 320,892 | 155,412 | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 99,945 | 78,615 | 320,911 | 155,426 | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 99,945 | 78,615 | 320,893 | 155,405 | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 99,945 | 78,615 | 320,907 | 155,406 | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 99,945 | 78,615 | 320,929 | 155,415 | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 99,945 | 78,615 | 320,872 | 155,428 | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 99,945 | 78,615 | 320,866 | 155,460 | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 99,945 | 78,615 | 320,901 | 155,429 | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 99,945 | 78,615 | 320,914 | 155,421 | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 99,945 | 81,815 | 320,867 | 155,443 | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 99,945 | 80,215 | 320,903 | 155,394 | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 99,945 | 78,615 | 320,874 | 155,391 | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 99,945 | 78,615 | 320,931 | 155,426 | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 100,445 | 80,415 | 322,808 | 156,087 | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 100,445 | 78,815 | 322,790 | 156,108 | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 100,445 | 78,815 | 322,752 | 156,116 | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 100,445 | 78,815 | 322,831 | 156,132 | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 100,445 | 80,415 | 322,818 | 156,135 | 2,597,373 | 2,351,545 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 100,445 | 78,815 | 264,842 | 152,911 | 2,611,017 | 2,360,847 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 100,445 | 78,815 | 264,821 | 152,964 | 2,611,057 | 2,360,877 |

## work-value

| Commit | Date | Subject | dotnet Ranvier (createEffect) | dotnet Ranvier (createEffectOn) | node Ranvier (createEffect) | node Ranvier (createEffectOn) | node-jitless Ranvier (createEffect) | node-jitless Ranvier (createEffectOn) |
|---|---|---|---:|---:|---:|---:|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | 107,571 | 119,770 | 766,592 | 766,912 | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | 107,569 | 119,770 | 766,630 | 767,077 | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | 107,569 | 119,770 | 766,628 | 766,926 | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | 107,569 | 119,771 | 766,556 | 766,953 | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | 107,570 | 121,369 | 766,614 | 766,837 | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | 107,571 | 121,370 | 756,707 | 742,054 | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | 107,569 | 119,771 | 629,511 | 691,109 | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | 107,571 | 119,770 | 316,167 | 283,706 | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | 107,569 | 121,369 | 316,178 | 283,728 | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | 107,570 | 119,769 | 316,156 | 283,730 | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | 107,569 | 119,771 | 316,193 | 283,676 | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | 107,571 | 119,770 | 316,170 | 283,707 | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | 107,569 | 119,769 | 316,135 | 283,696 | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | 107,569 | 121,369 | 316,136 | 283,703 | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | 107,569 | 119,771 | 316,246 | 283,710 | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | 107,571 | 119,770 | 316,149 | 283,694 | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | 107,569 | 119,770 | 316,130 | 283,716 | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | 107,569 | 119,769 | 316,141 | 283,721 | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | 107,071 | 119,269 | 321,107 | 283,844 | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | 107,069 | 120,869 | 321,228 | 283,833 | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | 107,069 | 119,269 | 321,142 | 283,789 | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | 107,069 | 119,269 | 321,163 | 283,797 | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | 107,069 | 119,271 | 321,129 | 283,794 | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | 107,069 | 119,269 | 321,250 | 283,777 | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | 107,069 | 119,269 | 321,113 | 283,780 | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | 107,069 | 119,269 | 321,119 | 283,814 | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | 107,070 | 119,269 | 321,175 | 283,799 | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | 107,069 | 119,271 | 321,148 | 283,799 | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | 107,069 | 122,469 | 321,138 | 283,817 | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | 107,069 | 120,869 | 321,107 | 283,810 | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | 107,069 | 119,269 | 321,122 | 283,739 | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | 107,069 | 119,269 | 321,148 | 283,819 | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | 107,570 | 121,069 | 322,982 | 284,497 | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | 107,569 | 119,469 | 323,049 | 284,505 | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | 107,571 | 119,470 | 323,069 | 284,502 | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | 107,569 | 119,469 | 323,011 | 284,508 | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | 107,571 | 121,069 | 323,040 | 284,460 | 2,582,023 | 2,910,903 |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | 107,569 | 119,469 | 265,090 | 279,879 | 2,595,670 | 2,920,217 |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | 107,569 | 119,469 | 265,067 | 279,896 | 2,595,694 | 2,920,213 |

