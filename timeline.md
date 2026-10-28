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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 83,052 | 127,148 | 1,734,165 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 83,306 | 127,188 | 1,734,156 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 83,052 | 127,131 | 1,734,118 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 82,993 | 127,165 | 1,734,029 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 83,006 | 127,186 | 1,734,155 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 83,006 | 127,142 | 1,734,128 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 83,006 | 127,210 | 1,734,154 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 82,998 | 127,158 | 1,734,142 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 82,993 | 127,176 | 1,734,338 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 83,044 | 127,137 | 1,734,153 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 83,110 | 127,147 | 1,734,130 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 83,332 | 127,108 | 1,734,426 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 83,208 | 127,155 | 1,734,216 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 83,284 | 127,178 | 1,734,155 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 83,044 | 127,150 | 1,734,108 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 83,044 | 127,109 | 1,734,259 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 83,018 | 127,158 | 1,734,028 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 82,983 | 127,156 | 1,734,666 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 82,985 | 127,177 | 1,734,666 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 83,060 | 127,169 | 1,734,660 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 83,046 | 127,102 | 1,734,026 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 82,998 | 127,188 | 1,733,954 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 83,003 | 127,164 | 1,732,378 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 82,985 | 127,180 | 1,734,056 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 83,131 | 127,176 | 1,734,023 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 83,047 | 127,156 | 1,734,026 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 83,081 | 127,141 | 1,734,016 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 83,132 | 127,079 | 1,734,045 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 83,017 | 127,154 | 1,733,979 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 82,999 | 127,122 | 1,733,999 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 82,986 | 127,138 | 1,734,405 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 83,068 | 127,180 | 1,734,473 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 83,000 | 127,114 | 1,734,521 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 83,060 | 127,488 | 1,738,341 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 83,194 | 127,487 | 1,738,349 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 83,124 | 127,505 | 1,738,309 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 83,123 | 127,529 | 1,738,294 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 83,059 | 127,456 | 1,738,327 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 83,311 | 127,493 | 1,731,319 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 83,373 | 127,543 | 1,731,275 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 83,312 | 127,528 | 1,731,330 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 83,373 | 127,517 | 1,742,171 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 83,328 | 127,586 | 1,742,189 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 83,313 | 127,616 | 1,742,209 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 83,342 | 127,559 | 1,742,177 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 83,310 | 127,520 | 1,742,203 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 83,311 | 127,501 | 1,742,250 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 83,376 | 127,519 | 1,742,264 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 83,373 | 127,514 | 1,742,206 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 83,377 | 127,537 | 1,742,214 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 83,694 | 127,534 | 1,742,237 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 83,375 | 127,523 | 1,742,155 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 83,311 | 127,585 | 1,742,209 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 83,317 | 127,524 | 1,742,196 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 83,310 | 127,562 | 1,742,193 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 83,439 | 127,576 | 1,742,257 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 83,373 | 127,624 | 1,742,169 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 83,313 | 127,527 | 1,742,134 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 83,303 | 127,538 | 1,742,345 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 83,445 | 127,566 | 1,742,343 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 83,367 | 127,618 | 1,742,225 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 83,304 | 127,582 | 1,742,194 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 4,416,202 | 9,203,775 | 107,301,567 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 4,416,312 | 9,325,388 | 107,415,846 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 4,416,278 | 9,203,988 | 107,451,321 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 4,416,184 | 9,482,482 | 107,429,728 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 4,416,259 | 9,320,392 | 107,580,269 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 4,416,223 | 9,203,595 | 107,451,682 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 4,416,213 | 9,203,548 | 107,415,831 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 4,416,138 | 9,319,189 | 107,430,181 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 4,416,147 | 9,204,859 | 107,586,080 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 4,416,157 | 9,364,954 | 107,322,350 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 4,416,228 | 9,322,764 | 107,544,455 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 4,417,303 | 9,364,719 | 107,451,544 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 4,416,238 | 9,203,731 | 107,431,191 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 4,416,366 | 9,320,151 | 107,579,948 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 4,416,210 | 9,320,035 | 107,302,072 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 4,416,154 | 9,203,687 | 107,708,264 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 4,416,115 | 9,320,259 | 107,301,908 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 4,416,101 | 9,480,802 | 107,544,214 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 4,416,062 | 9,203,664 | 107,451,110 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 4,416,458 | 9,203,385 | 107,544,033 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 4,416,064 | 9,203,925 | 107,579,989 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 4,416,080 | 9,319,178 | 107,544,285 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 4,416,060 | 9,206,635 | 107,451,150 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 4,416,178 | 9,203,633 | 107,416,196 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 4,416,256 | 9,087,879 | 107,416,006 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 4,416,141 | 9,203,967 | 107,301,745 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 4,416,278 | 9,320,024 | 107,301,725 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 4,416,231 | 9,203,874 | 107,694,957 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 4,416,206 | 9,480,484 | 107,694,590 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 4,416,188 | 9,205,959 | 107,707,951 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 4,416,389 | 9,203,681 | 107,429,766 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 4,416,188 | 9,369,750 | 107,432,071 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 4,416,115 | 9,481,154 | 107,580,228 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 4,420,605 | 9,398,056 | 107,623,698 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 4,420,580 | 9,236,876 | 107,623,895 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 4,420,695 | 9,360,467 | 107,628,723 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 4,420,722 | 9,355,611 | 107,908,270 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 4,420,555 | 9,353,287 | 107,629,882 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 4,417,779 | 9,538,966 | 107,885,493 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 4,417,707 | 9,248,877 | 107,807,178 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 4,417,690 | 9,539,457 | 107,632,003 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 4,417,687 | 9,384,003 | 107,924,063 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 4,417,867 | 9,273,867 | 107,727,285 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 4,417,667 | 9,454,780 | 107,748,759 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 4,417,821 | 9,383,849 | 107,930,537 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 4,417,703 | 9,275,856 | 107,746,715 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 4,417,647 | 9,454,620 | 107,853,559 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 4,417,590 | 9,273,397 | 107,853,155 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 4,417,715 | 9,274,074 | 107,924,113 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 4,417,675 | 9,274,239 | 107,852,885 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 4,417,723 | 9,383,641 | 107,748,203 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 4,417,787 | 9,274,379 | 107,882,167 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 4,417,694 | 9,272,684 | 107,879,432 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 4,417,787 | 9,275,619 | 107,668,903 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 4,417,772 | 9,275,276 | 107,755,626 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 4,417,699 | 9,451,888 | 107,756,158 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 4,417,867 | 9,163,007 | 107,727,039 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 4,417,716 | 9,456,160 | 107,629,145 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 4,417,722 | 9,562,436 | 107,851,349 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 4,417,697 | 9,163,311 | 107,930,654 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 4,417,665 | 9,386,141 | 107,851,416 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 4,417,761 | 9,291,753 | 107,851,265 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 139,040 | 39,723 | 440,649 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 139,040 | 39,714 | 440,649 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 139,040 | 39,722 | 440,652 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 138,908 | 39,667 | 440,646 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 138,908 | 39,726 | 440,641 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 138,908 | 39,724 | 440,640 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 138,940 | 39,711 | 440,653 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 138,992 | 39,704 | 440,655 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 138,992 | 39,722 | 440,645 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 138,992 | 39,725 | 440,649 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 138,992 | 39,723 | 440,655 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 138,992 | 39,721 | 440,655 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 138,992 | 39,648 | 440,647 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 138,992 | 39,724 | 440,650 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 138,992 | 39,721 | 440,644 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 138,992 | 39,720 | 440,650 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 139,024 | 39,725 | 440,657 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 139,024 | 39,655 | 440,649 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 139,024 | 39,706 | 440,651 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 138,968 | 39,715 | 440,645 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 138,968 | 39,717 | 440,660 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 138,968 | 39,715 | 440,654 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 138,968 | 39,718 | 440,651 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 138,968 | 39,717 | 440,660 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 139,064 | 39,725 | 440,649 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 139,064 | 39,721 | 440,651 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 139,064 | 39,718 | 440,650 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 139,064 | 39,733 | 440,651 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 139,064 | 39,718 | 440,654 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 139,064 | 39,723 | 440,655 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 138,908 | 39,731 | 440,670 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 138,908 | 39,653 | 440,663 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 138,907 | 39,721 | 440,669 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 139,042 | 39,710 | 442,087 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 138,926 | 39,721 | 442,090 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 139,026 | 39,716 | 442,089 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 139,026 | 39,713 | 442,088 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 139,026 | 39,720 | 442,093 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 139,071 | 39,094 | 442,894 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 139,071 | 39,099 | 442,892 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 139,071 | 39,087 | 442,897 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 139,071 | 41,269 | 434,850 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 139,071 | 41,267 | 434,860 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 139,071 | 41,258 | 434,853 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 139,071 | 41,267 | 434,846 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 139,071 | 41,264 | 434,852 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 139,071 | 41,224 | 434,856 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 139,071 | 41,259 | 434,858 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 139,071 | 41,263 | 434,859 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 139,071 | 41,269 | 434,849 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 139,183 | 41,263 | 434,848 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 139,183 | 41,273 | 434,855 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 139,183 | 41,273 | 434,847 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 139,183 | 41,273 | 434,848 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 139,183 | 41,267 | 434,841 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 139,183 | 41,264 | 434,846 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 139,183 | 41,241 | 434,851 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 139,183 | 41,266 | 434,847 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 139,139 | 41,263 | 434,855 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 139,139 | 41,272 | 434,860 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 139,139 | 41,262 | 434,852 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 139,139 | 41,269 | 434,854 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 2,792,309 | 1,085,796 | 12,314,132 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 2,792,310 | 1,085,311 | 12,314,272 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 2,792,309 | 1,085,567 | 12,317,141 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 2,788,709 | 1,085,426 | 12,314,050 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 2,788,708 | 1,085,476 | 12,314,312 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 2,788,708 | 1,085,588 | 12,317,052 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 2,790,309 | 1,085,295 | 12,317,167 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 2,791,509 | 1,085,032 | 12,313,990 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 2,791,510 | 1,085,385 | 12,314,741 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 2,791,509 | 1,085,675 | 12,314,016 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 2,791,509 | 1,085,589 | 12,313,686 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 2,791,509 | 1,085,758 | 12,313,955 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 2,791,509 | 1,085,495 | 12,313,811 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 2,791,509 | 1,085,565 | 12,314,034 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 2,791,510 | 1,085,374 | 12,314,033 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 2,791,509 | 1,085,437 | 12,317,226 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 2,791,509 | 1,085,574 | 12,317,097 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 2,791,509 | 1,085,810 | 12,313,939 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 2,791,509 | 1,085,035 | 12,317,307 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 2,789,909 | 1,085,452 | 12,314,126 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 2,789,909 | 1,085,420 | 12,313,854 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 2,789,910 | 1,086,015 | 12,314,010 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 2,789,909 | 1,085,692 | 12,313,980 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 2,789,910 | 1,085,349 | 12,317,662 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 2,793,108 | 1,085,781 | 12,314,116 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 2,793,108 | 1,085,441 | 12,317,429 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 2,793,110 | 1,085,526 | 12,317,430 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 2,793,108 | 1,085,316 | 12,317,611 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 2,793,109 | 1,085,488 | 12,313,966 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 2,793,109 | 1,085,700 | 12,317,496 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 2,788,709 | 1,085,366 | 12,315,766 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 2,788,710 | 1,085,309 | 12,315,360 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 2,788,717 | 1,084,693 | 12,318,636 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 2,790,161 | 1,091,609 | 12,354,635 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 2,785,761 | 1,091,640 | 12,357,746 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 2,788,562 | 1,091,501 | 12,357,856 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 2,788,561 | 1,091,968 | 12,354,405 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 2,788,561 | 1,091,532 | 12,357,844 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 2,788,051 | 1,044,522 | 12,371,925 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 2,788,051 | 1,044,653 | 12,371,673 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 2,788,051 | 1,044,380 | 12,368,231 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 2,788,051 | 1,022,537 | 11,970,184 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 2,788,052 | 1,023,018 | 11,967,224 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 2,788,050 | 1,023,023 | 11,970,202 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 2,788,052 | 1,022,542 | 11,966,937 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 2,788,051 | 1,023,010 | 11,966,893 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 2,788,051 | 1,023,108 | 11,970,214 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 2,788,051 | 1,023,029 | 11,970,210 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 2,788,052 | 1,022,848 | 11,966,856 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 2,788,052 | 1,022,986 | 11,970,392 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 2,791,251 | 1,023,056 | 11,966,803 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 2,791,250 | 1,022,849 | 11,966,852 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 2,791,251 | 1,022,471 | 11,970,488 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 2,791,251 | 1,023,152 | 11,966,843 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 2,791,251 | 1,022,996 | 11,970,218 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 2,791,251 | 1,023,048 | 11,970,356 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 2,791,251 | 1,022,839 | 11,970,195 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 2,791,251 | 1,023,231 | 11,967,005 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 2,790,467 | 1,022,959 | 11,967,345 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 2,790,468 | 1,023,085 | 11,970,199 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 2,790,467 | 1,023,048 | 11,966,904 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 2,790,467 | 1,023,215 | 11,967,121 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,783 | 66,618 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,783 | 66,618 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,783 | 66,618 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 13,035 | 318 | 2,450 | 4,491 | 3,179 | 72,782 | 66,618 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,618 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,624 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 13,035 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,624 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 13,099 | 318 | 2,450 | 4,491 | 3,180 | 72,782 | 66,624 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 13,035 | 318 | 2,453 | 4,491 | 3,209 | 72,783 | 66,755 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 13,035 | 318 | 2,453 | 4,491 | 3,208 | 72,783 | 66,755 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 13,035 | 318 | 2,453 | 4,491 | 3,209 | 72,782 | 66,755 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 13,035 | 318 | 2,453 | 4,491 | 3,209 | 72,783 | 66,755 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 13,035 | 318 | 2,453 | 4,491 | 3,209 | 72,782 | 66,755 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,781 | 66,827 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 13,035 | 318 | 2,454 | 4,491 | 3,217 | 72,782 | 66,827 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,781 | 66,827 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,782 | 66,827 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,781 | 66,827 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,781 | 66,827 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,781 | 66,827 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 13,035 | 318 | 2,454 | 4,491 | 3,218 | 72,781 | 66,827 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 2,549 | 3,067 | 2,496 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 2,549 | 3,067 | 2,496 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 2,549 | 3,067 | 2,496 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 2,549 | 3,067 | 2,496 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 2,549 | 3,067 | 2,496 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 2,549 | 3,067 | 2,496 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 2,549 | 3,067 | 2,496 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 2,549 | 3,067 | 2,496 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 2,549 | 3,067 | 2,496 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 2,549 | 3,067 | 2,496 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 2,549 | 3,067 | 2,496 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 2,549 | 3,067 | 2,496 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 2,549 | 3,067 | 2,496 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 2,549 | 3,067 | 2,496 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 2,549 | 3,067 | 2,496 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 2,549 | 3,067 | 2,496 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 2,549 | 3,067 | 2,496 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 2,549 | 3,067 | 2,496 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 2,549 | 3,067 | 2,496 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 2,549 | 3,067 | 2,496 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 2,549 | 3,067 | 2,496 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 2,549 | 3,067 | 2,496 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 2,549 | 3,067 | 2,496 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 2,549 | 3,067 | 2,496 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 2,549 | 3,067 | 2,496 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 2,549 | 3,067 | 2,496 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 2,549 | 3,067 | 2,496 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 2,549 | 3,067 | 2,496 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 2,549 | 3,067 | 2,496 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 2,549 | 3,067 | 2,496 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 2,549 | 3,067 | 2,496 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 2,549 | 3,067 | 2,496 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 2,549 | 3,067 | 2,496 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 2,552 | 3,070 | 2,499 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 2,552 | 3,070 | 2,499 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 2,552 | 3,070 | 2,499 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 2,552 | 3,070 | 2,499 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 2,552 | 3,070 | 2,499 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 2,553 | 3,071 | 2,500 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 2,553 | 3,071 | 2,500 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 2,553 | 3,071 | 2,500 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 2,553 | 3,071 | 2,500 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 2,553 | 3,071 | 2,500 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 2,553 | 3,071 | 2,500 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 2,553 | 3,071 | 2,500 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 2,553 | 3,071 | 2,500 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 2,553 | 3,071 | 2,500 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 2,553 | 3,071 | 2,500 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 2,553 | 3,071 | 2,500 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 2,553 | 3,071 | 2,500 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 2,553 | 3,071 | 2,500 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 2,553 | 3,071 | 2,500 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 2,553 | 3,071 | 2,500 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 2,553 | 3,071 | 2,500 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 2,553 | 3,071 | 2,500 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 2,553 | 3,071 | 2,500 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 2,553 | 3,071 | 2,500 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 2,553 | 3,071 | 2,500 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 2,553 | 3,071 | 2,500 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 2,553 | 3,071 | 2,500 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 2,553 | 3,071 | 2,500 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 2,553 | 3,071 | 2,500 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 74,709 | 833,937 | 2,605,105 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 74,709 | 833,953 | 2,605,077 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 74,709 | 833,990 | 2,605,118 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 74,709 | 833,964 | 2,605,007 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 74,709 | 833,966 | 2,605,083 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 74,709 | 834,004 | 2,605,080 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 74,709 | 833,995 | 2,605,046 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 74,709 | 833,949 | 2,605,060 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 74,709 | 833,998 | 2,604,957 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 74,709 | 833,951 | 2,605,072 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 74,709 | 835,974 | 2,605,092 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 74,709 | 834,047 | 2,605,114 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 74,709 | 833,976 | 2,605,092 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 74,709 | 833,967 | 2,605,089 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 74,709 | 833,986 | 2,605,089 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 74,709 | 833,924 | 2,605,062 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 74,709 | 833,926 | 2,605,031 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 74,709 | 839,547 | 2,604,319 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 74,709 | 839,567 | 2,604,220 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 74,709 | 839,561 | 2,604,210 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 74,709 | 833,805 | 2,589,553 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 74,709 | 833,798 | 2,589,525 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 74,709 | 833,855 | 2,589,410 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 74,709 | 833,847 | 2,589,489 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 74,709 | 833,849 | 2,597,161 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 74,709 | 833,906 | 2,597,154 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 74,709 | 833,760 | 2,597,060 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 74,709 | 838,556 | 2,597,110 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 74,709 | 833,862 | 2,597,154 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 74,709 | 833,914 | 2,596,994 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 74,709 | 837,041 | 2,589,572 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 74,709 | 837,048 | 2,589,648 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 74,709 | 837,139 | 2,589,628 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 74,709 | 829,632 | 2,592,919 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 74,710 | 829,596 | 2,592,943 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 74,709 | 829,429 | 2,593,735 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 74,709 | 829,388 | 2,593,875 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 74,709 | 829,388 | 2,593,780 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 74,709 | 829,457 | 2,593,768 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 74,709 | 829,406 | 2,593,680 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 74,709 | 829,372 | 2,593,767 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 74,709 | 836,642 | 2,603,099 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 74,709 | 836,691 | 2,603,104 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 74,709 | 836,687 | 2,603,140 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 74,709 | 836,706 | 2,603,155 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 74,709 | 836,677 | 2,603,116 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 74,709 | 836,664 | 2,603,212 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 74,709 | 836,613 | 2,603,135 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 74,709 | 836,702 | 2,603,175 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 74,709 | 836,722 | 2,603,147 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 74,709 | 836,704 | 2,603,209 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 74,709 | 836,642 | 2,603,152 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 74,709 | 836,766 | 2,603,124 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 74,709 | 836,767 | 2,603,208 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 74,710 | 836,749 | 2,603,114 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 74,710 | 836,660 | 2,603,146 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 74,709 | 836,742 | 2,603,084 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 74,709 | 836,699 | 2,603,057 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 74,710 | 836,545 | 2,603,179 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 74,710 | 836,694 | 2,603,244 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 74,710 | 836,450 | 2,603,110 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 74,710 | 836,523 | 2,603,048 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 13,450,071 | 2,859,678 | 2,532,361 | 2,131,458 | 4,004,154 | 34,066,730 | 49,723,845 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 13,450,057 | 2,859,678 | 2,532,361 | 2,131,518 | 4,003,959 | 34,063,624 | 49,723,566 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 13,466,120 | 2,859,678 | 2,532,361 | 2,131,031 | 4,004,071 | 34,066,460 | 49,723,523 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 13,450,081 | 2,859,678 | 2,532,361 | 2,131,924 | 4,004,054 | 34,063,483 | 49,723,886 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 13,450,076 | 2,859,678 | 2,532,362 | 2,131,574 | 4,004,174 | 34,066,813 | 49,723,672 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 13,450,056 | 2,859,678 | 2,532,361 | 2,131,490 | 4,004,047 | 34,063,093 | 49,723,513 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 13,449,996 | 2,875,677 | 2,532,362 | 2,131,822 | 4,004,040 | 34,066,822 | 49,723,813 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 13,529,938 | 2,859,680 | 2,532,377 | 2,131,505 | 4,003,504 | 34,063,398 | 49,723,393 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 13,449,972 | 2,859,678 | 2,532,361 | 2,132,050 | 4,004,167 | 34,066,994 | 49,724,103 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 13,450,115 | 2,875,678 | 2,532,361 | 2,131,788 | 4,003,908 | 34,063,338 | 49,723,627 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 13,449,960 | 2,859,678 | 2,532,362 | 2,131,719 | 4,004,136 | 34,066,996 | 49,723,800 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 13,450,075 | 2,875,678 | 2,532,362 | 2,130,838 | 4,004,190 | 34,066,742 | 49,723,617 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 13,465,944 | 2,859,678 | 2,532,361 | 2,131,883 | 4,003,981 | 34,064,135 | 49,723,917 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 13,449,944 | 2,859,678 | 2,532,361 | 2,131,856 | 4,003,995 | 34,066,518 | 49,723,693 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 13,450,079 | 2,859,678 | 2,532,361 | 2,131,028 | 4,004,129 | 34,063,281 | 49,723,741 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 13,450,044 | 2,875,677 | 2,532,362 | 2,131,829 | 4,003,596 | 34,063,832 | 49,723,813 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 13,449,958 | 2,859,679 | 2,532,361 | 2,131,745 | 4,003,679 | 34,066,647 | 49,723,316 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 13,450,084 | 2,859,678 | 2,532,361 | 2,131,033 | 4,001,852 | 34,069,376 | 49,687,590 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 13,449,986 | 2,859,678 | 2,532,361 | 2,131,908 | 4,002,734 | 34,068,466 | 49,687,582 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 13,449,950 | 2,859,678 | 2,532,361 | 2,131,765 | 4,002,748 | 34,068,707 | 49,687,454 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 13,497,950 | 2,859,678 | 2,532,363 | 2,132,052 | 4,002,883 | 33,868,258 | 49,467,981 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 13,449,965 | 2,859,678 | 2,532,363 | 2,131,893 | 4,003,302 | 33,867,964 | 49,467,917 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 13,449,993 | 2,859,678 | 2,532,379 | 2,131,916 | 4,003,461 | 33,868,930 | 49,468,018 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 13,575,486 | 2,875,678 | 2,532,380 | 2,131,775 | 4,003,088 | 33,868,647 | 49,467,552 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 13,466,086 | 2,859,678 | 2,532,363 | 2,131,942 | 4,003,018 | 33,868,366 | 49,400,878 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 13,450,061 | 2,859,678 | 2,548,363 | 2,131,658 | 4,003,277 | 33,868,220 | 49,400,934 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 13,465,945 | 2,859,679 | 2,532,363 | 2,131,573 | 4,002,853 | 33,868,016 | 49,401,172 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 13,450,057 | 2,859,678 | 2,532,364 | 2,131,537 | 4,003,184 | 33,867,549 | 49,401,072 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 13,450,091 | 2,859,678 | 2,532,363 | 2,130,981 | 4,003,216 | 33,868,056 | 49,401,145 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 13,449,957 | 2,859,678 | 2,564,363 | 2,131,698 | 4,003,313 | 33,868,255 | 49,401,076 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 13,605,990 | 2,865,678 | 2,536,382 | 2,131,957 | 4,004,436 | 34,301,728 | 49,793,576 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 13,480,428 | 2,897,678 | 2,552,366 | 2,132,159 | 4,004,654 | 34,301,500 | 49,793,679 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 13,448,465 | 2,865,678 | 2,536,366 | 2,131,835 | 4,004,481 | 34,301,543 | 49,793,653 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 13,448,327 | 2,865,677 | 2,545,361 | 2,131,637 | 4,060,268 | 33,949,607 | 49,780,114 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 13,464,272 | 2,865,677 | 2,545,362 | 2,131,836 | 4,059,170 | 33,949,740 | 49,780,150 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 13,448,396 | 2,865,677 | 2,545,376 | 2,131,082 | 4,059,806 | 33,946,840 | 49,754,165 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 13,544,374 | 2,865,677 | 2,545,392 | 2,132,160 | 4,059,639 | 33,946,381 | 49,753,988 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 13,824,943 | 2,881,676 | 2,548,413 | 2,131,773 | 4,060,083 | 34,018,814 | 49,813,940 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 13,448,436 | 2,865,677 | 2,570,383 | 2,131,081 | 4,067,177 | 34,019,300 | 50,276,852 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 13,573,806 | 2,865,678 | 2,570,400 | 2,131,808 | 4,067,073 | 34,019,049 | 50,276,819 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 13,464,369 | 2,865,677 | 2,570,384 | 2,131,571 | 4,067,334 | 34,019,178 | 50,276,933 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 13,448,312 | 2,865,678 | 2,570,370 | 2,127,777 | 4,045,090 | 34,002,910 | 50,334,350 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 13,448,400 | 2,865,678 | 2,570,369 | 2,127,882 | 4,045,126 | 34,003,261 | 50,334,433 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 13,573,949 | 2,865,677 | 2,570,400 | 2,127,528 | 4,045,580 | 34,003,138 | 50,334,149 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 13,528,309 | 2,865,677 | 2,570,368 | 2,127,994 | 4,045,120 | 34,002,936 | 50,334,146 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 13,448,358 | 2,865,678 | 2,570,368 | 2,127,930 | 4,045,763 | 34,003,110 | 50,334,353 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 13,464,365 | 2,865,677 | 2,570,368 | 2,128,126 | 4,043,114 | 34,003,168 | 50,334,260 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 13,464,300 | 2,865,678 | 2,570,400 | 2,127,915 | 4,045,530 | 34,003,214 | 50,333,928 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 13,448,437 | 2,865,677 | 2,570,368 | 2,127,524 | 4,045,260 | 34,003,185 | 50,334,303 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 13,573,788 | 2,865,676 | 2,570,370 | 2,127,106 | 4,045,348 | 34,003,379 | 50,334,392 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 13,464,447 | 2,865,678 | 2,570,416 | 2,128,104 | 4,042,729 | 34,003,242 | 50,333,902 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 13,699,314 | 2,865,677 | 2,570,368 | 2,127,905 | 4,042,588 | 34,003,480 | 50,334,364 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 13,464,410 | 2,865,677 | 2,570,369 | 2,127,803 | 4,045,313 | 34,003,051 | 50,334,236 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 13,573,921 | 2,865,678 | 2,570,400 | 2,127,999 | 4,045,090 | 34,003,035 | 50,333,879 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 13,621,910 | 2,865,678 | 2,570,368 | 2,126,793 | 4,045,510 | 34,003,180 | 50,334,160 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 13,448,404 | 2,865,677 | 2,570,368 | 2,127,836 | 4,045,348 | 34,003,308 | 50,334,322 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 13,573,931 | 2,865,677 | 2,570,368 | 2,127,109 | 4,045,390 | 34,003,156 | 50,334,350 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 13,904,875 | 2,865,678 | 2,570,400 | 2,127,589 | 4,045,417 | 34,003,263 | 50,334,402 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 13,480,234 | 2,865,677 | 2,570,368 | 2,128,178 | 4,044,648 | 33,999,974 | 50,423,970 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 13,448,341 | 2,865,677 | 2,570,369 | 2,127,891 | 4,041,804 | 33,995,836 | 50,391,353 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 13,448,211 | 2,865,677 | 2,570,368 | 2,127,858 | 4,044,688 | 33,999,830 | 50,424,091 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 13,448,216 | 2,865,678 | 2,570,448 | 2,127,883 | 4,044,626 | 33,999,699 | 50,423,920 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 2,926,484 | 4,147,505 | 54,424,688 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 2,926,483 | 4,147,660 | 54,424,821 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 2,926,480 | 4,147,488 | 54,424,555 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 2,926,483 | 4,147,276 | 54,424,664 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 2,910,483 | 4,147,560 | 54,424,712 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 2,910,483 | 4,147,510 | 54,424,652 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 2,926,484 | 4,147,650 | 54,424,657 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 2,910,484 | 4,147,496 | 54,424,666 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 2,910,483 | 4,147,483 | 54,424,652 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 2,910,483 | 4,147,514 | 54,424,698 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 2,910,484 | 4,147,618 | 54,424,693 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 2,910,483 | 4,147,736 | 54,424,698 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 2,910,483 | 4,147,492 | 54,424,907 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 2,910,531 | 4,147,579 | 54,424,865 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 2,926,483 | 4,147,607 | 54,424,898 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 2,910,484 | 4,147,496 | 54,424,776 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 2,910,483 | 4,147,438 | 54,424,708 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 2,910,483 | 4,147,491 | 54,424,704 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 2,910,484 | 4,147,387 | 54,424,576 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 2,910,483 | 4,147,405 | 54,425,006 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 2,910,484 | 4,147,653 | 54,424,696 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 2,910,486 | 4,147,486 | 54,424,787 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 2,910,486 | 4,147,271 | 54,424,718 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 2,958,500 | 4,147,452 | 54,424,919 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 2,910,501 | 4,147,618 | 54,424,700 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 2,910,484 | 4,147,730 | 54,424,672 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 2,910,484 | 4,147,316 | 54,424,712 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 2,942,484 | 4,147,489 | 54,424,570 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 2,910,484 | 4,147,499 | 54,424,718 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 2,910,484 | 4,147,689 | 54,424,523 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 2,947,519 | 4,400,562 | 54,430,827 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 2,931,487 | 4,400,707 | 54,430,820 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 2,915,488 | 4,400,463 | 54,430,662 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 2,924,485 | 4,412,446 | 54,639,966 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 2,924,487 | 4,412,424 | 54,639,765 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 2,924,485 | 4,412,335 | 54,639,993 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 2,924,485 | 4,412,422 | 54,639,972 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 2,927,489 | 4,412,468 | 54,638,750 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 2,965,484 | 4,175,729 | 55,101,489 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 2,949,516 | 4,175,528 | 55,101,712 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 2,949,484 | 4,175,653 | 55,101,544 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 2,949,485 | 4,179,698 | 55,229,092 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 2,949,484 | 4,179,589 | 55,229,096 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 2,949,484 | 4,179,565 | 55,228,972 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 2,949,484 | 4,179,397 | 55,229,084 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 2,949,484 | 4,179,982 | 55,229,164 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 2,949,484 | 4,179,475 | 55,229,058 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 2,949,484 | 4,179,846 | 55,229,109 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 2,949,484 | 4,179,590 | 55,229,382 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 2,949,517 | 4,179,708 | 55,229,348 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 2,965,484 | 4,179,748 | 55,229,128 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 2,949,485 | 4,179,982 | 55,229,174 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 2,949,484 | 4,179,680 | 55,229,117 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 2,949,484 | 4,179,771 | 55,229,172 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 2,949,484 | 4,179,669 | 55,228,942 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 2,949,484 | 4,179,866 | 55,229,172 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 2,949,484 | 4,179,730 | 55,229,194 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 2,949,485 | 4,179,663 | 55,229,279 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 2,949,487 | 4,179,748 | 55,229,091 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 2,949,500 | 4,179,658 | 55,229,108 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 2,965,484 | 4,179,527 | 55,228,825 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 2,965,564 | 4,179,829 | 55,229,108 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 1,182 | 50 | 111 | 65 | 35 | 1,095 | 875 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 1,182 | 50 | 114 | 65 | 51 | 1,095 | 1,006 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 765 | 768 | 16,102 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 765 | 768 | 16,102 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 765 | 768 | 16,102 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 765 | 768 | 16,102 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 765 | 768 | 16,102 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 765 | 768 | 16,102 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 765 | 768 | 16,102 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 765 | 768 | 16,102 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 765 | 768 | 16,102 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 765 | 768 | 16,102 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 765 | 768 | 16,102 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 765 | 768 | 16,102 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 765 | 768 | 16,102 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 765 | 768 | 16,102 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 765 | 768 | 16,102 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 765 | 768 | 16,102 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 765 | 768 | 16,102 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 765 | 768 | 16,102 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 765 | 768 | 16,102 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 765 | 768 | 16,102 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 765 | 768 | 16,102 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 765 | 768 | 16,102 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 765 | 768 | 16,102 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 765 | 768 | 16,102 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 765 | 768 | 16,102 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 765 | 768 | 16,102 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 765 | 768 | 16,102 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 765 | 768 | 16,102 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 765 | 768 | 16,102 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 765 | 768 | 16,102 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 765 | 768 | 16,108 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 765 | 768 | 16,108 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 765 | 768 | 16,108 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 780 | 744 | 16,448 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 780 | 744 | 16,448 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 780 | 744 | 16,448 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 780 | 744 | 16,448 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 780 | 744 | 16,448 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 781 | 751 | 16,520 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 781 | 751 | 16,520 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 781 | 751 | 16,520 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 781 | 751 | 16,520 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 781 | 751 | 16,520 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 781 | 751 | 16,520 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 781 | 751 | 16,520 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 781 | 751 | 16,520 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 781 | 751 | 16,520 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 781 | 751 | 16,520 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 781 | 751 | 16,520 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 781 | 751 | 16,520 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 781 | 751 | 16,520 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 781 | 751 | 16,520 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 781 | 751 | 16,520 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 781 | 751 | 16,520 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 781 | 751 | 16,520 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 781 | 751 | 16,520 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 781 | 751 | 16,520 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 781 | 751 | 16,520 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 781 | 751 | 16,520 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 781 | 751 | 16,520 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 781 | 751 | 16,520 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 781 | 751 | 16,520 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 737 | 775 | 16,261 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 737 | 775 | 16,261 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 737 | 775 | 16,261 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 737 | 775 | 16,261 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 737 | 775 | 16,261 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 737 | 775 | 16,261 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 737 | 775 | 16,261 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 737 | 775 | 16,261 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 737 | 775 | 16,261 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 737 | 775 | 16,261 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 737 | 775 | 16,261 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 737 | 775 | 16,261 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 737 | 775 | 16,261 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 737 | 775 | 16,261 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 737 | 775 | 16,261 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 737 | 775 | 16,261 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 737 | 775 | 16,261 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 737 | 775 | 16,261 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 737 | 775 | 16,261 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 737 | 775 | 16,261 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 737 | 775 | 16,261 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 737 | 775 | 16,261 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 737 | 775 | 16,261 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 737 | 775 | 16,261 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 737 | 775 | 16,261 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 737 | 775 | 16,261 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 737 | 775 | 16,261 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 769 | 775 | 16,261 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 737 | 775 | 16,261 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 737 | 775 | 16,261 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 737 | 775 | 16,267 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 737 | 775 | 16,267 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 737 | 775 | 16,267 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 750 | 795 | 16,471 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 750 | 795 | 16,471 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 750 | 795 | 16,471 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 750 | 795 | 16,471 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 750 | 795 | 16,471 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 751 | 802 | 16,543 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 751 | 802 | 16,543 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 751 | 802 | 16,543 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 751 | 802 | 16,543 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 751 | 802 | 16,543 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 751 | 802 | 16,543 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 751 | 802 | 16,543 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 751 | 802 | 16,543 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 751 | 802 | 16,543 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 751 | 802 | 16,543 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 751 | 802 | 16,543 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 751 | 802 | 16,543 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 751 | 802 | 16,543 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 751 | 802 | 16,543 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 751 | 802 | 16,543 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 751 | 802 | 16,543 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 751 | 802 | 16,543 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 751 | 802 | 16,543 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 751 | 802 | 16,543 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 751 | 802 | 16,543 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 751 | 802 | 16,543 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 751 | 802 | 16,543 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 751 | 802 | 16,543 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 751 | 802 | 16,543 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 2,707,643 | 806,290 | 367,497 | 466,202 | 485,089 | 13,194,030 | 9,106,150 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 2,707,641 | 806,291 | 367,497 | 466,091 | 485,125 | 13,195,635 | 9,105,967 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 2,707,645 | 806,291 | 367,497 | 466,146 | 485,190 | 13,192,878 | 9,105,766 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 2,707,643 | 806,291 | 367,497 | 466,161 | 485,312 | 13,195,906 | 9,105,580 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 2,706,243 | 806,291 | 367,497 | 465,939 | 485,409 | 13,192,461 | 9,106,048 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 2,707,643 | 806,291 | 367,497 | 465,989 | 485,057 | 13,195,293 | 9,105,879 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 2,707,642 | 806,291 | 367,497 | 466,092 | 485,284 | 13,194,632 | 9,106,657 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 2,707,643 | 806,290 | 367,497 | 466,264 | 485,030 | 13,195,047 | 9,105,988 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 2,706,244 | 806,291 | 367,497 | 466,055 | 485,160 | 13,193,777 | 9,106,022 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 2,707,643 | 806,291 | 367,497 | 466,177 | 485,125 | 13,195,614 | 9,106,166 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 2,706,242 | 806,291 | 367,497 | 466,354 | 485,301 | 13,193,913 | 9,105,874 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 2,706,747 | 806,291 | 367,497 | 465,883 | 485,318 | 13,194,087 | 9,106,232 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 2,707,644 | 806,291 | 367,497 | 465,498 | 485,102 | 13,194,662 | 9,106,948 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 2,707,644 | 806,291 | 367,497 | 466,121 | 485,066 | 13,195,020 | 9,106,226 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 2,707,646 | 806,291 | 367,497 | 466,079 | 485,247 | 13,195,636 | 9,106,272 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 2,707,644 | 806,291 | 367,497 | 465,801 | 485,236 | 13,195,316 | 9,105,912 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 2,707,646 | 806,291 | 367,497 | 465,654 | 485,338 | 13,195,132 | 9,105,877 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 2,706,242 | 806,291 | 367,497 | 466,089 | 484,937 | 13,193,985 | 9,106,498 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 2,706,747 | 806,291 | 367,497 | 465,792 | 485,187 | 13,195,757 | 9,106,266 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 2,706,246 | 806,291 | 367,497 | 466,323 | 485,036 | 13,194,087 | 9,105,868 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 2,707,640 | 806,291 | 367,497 | 466,327 | 485,261 | 13,193,963 | 9,106,924 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 2,706,242 | 806,291 | 367,497 | 466,065 | 485,358 | 13,192,394 | 9,105,869 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 2,706,248 | 806,291 | 367,497 | 466,108 | 484,997 | 13,193,620 | 9,106,178 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 2,707,642 | 806,290 | 367,497 | 466,114 | 485,395 | 13,194,158 | 9,106,187 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 2,706,245 | 806,291 | 367,497 | 465,943 | 485,288 | 13,194,036 | 9,106,085 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 2,706,245 | 806,291 | 367,497 | 466,106 | 485,501 | 13,194,030 | 9,106,268 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 2,706,757 | 806,290 | 367,497 | 465,559 | 485,089 | 13,193,697 | 9,106,101 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 2,707,643 | 806,291 | 367,497 | 466,319 | 485,204 | 13,193,892 | 9,106,272 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 2,707,644 | 806,291 | 367,497 | 466,175 | 485,281 | 13,193,888 | 9,105,996 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 2,707,644 | 806,291 | 367,497 | 466,079 | 485,114 | 13,195,543 | 9,106,268 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 2,706,248 | 806,290 | 367,497 | 466,000 | 486,449 | 13,195,148 | 9,106,166 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 2,707,636 | 806,290 | 367,497 | 466,195 | 486,527 | 13,195,501 | 9,106,443 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 2,707,639 | 806,291 | 367,497 | 466,044 | 486,252 | 13,193,977 | 9,106,577 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 2,707,643 | 806,290 | 367,497 | 466,169 | 487,299 | 13,194,208 | 9,106,194 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 2,706,245 | 806,288 | 367,497 | 466,062 | 487,183 | 13,194,184 | 9,106,625 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 2,707,643 | 806,288 | 367,497 | 466,222 | 487,441 | 13,194,247 | 9,107,294 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 2,706,247 | 806,288 | 367,497 | 465,944 | 487,296 | 13,193,640 | 9,106,356 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 2,707,643 | 806,288 | 367,497 | 465,948 | 487,144 | 13,194,030 | 9,106,393 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 2,706,242 | 806,288 | 367,497 | 466,182 | 495,308 | 13,194,070 | 9,106,072 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 2,707,640 | 806,290 | 367,497 | 465,634 | 495,102 | 13,196,100 | 9,106,984 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 2,707,645 | 806,288 | 367,497 | 466,183 | 495,039 | 13,195,558 | 9,106,668 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 2,707,642 | 806,288 | 367,497 | 475,692 | 495,182 | 13,206,949 | 9,105,449 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 2,707,643 | 806,288 | 367,497 | 475,929 | 495,188 | 13,205,856 | 9,106,171 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 2,707,643 | 806,288 | 367,497 | 475,594 | 495,048 | 13,207,036 | 9,106,195 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 2,706,752 | 806,288 | 367,497 | 475,761 | 495,157 | 13,207,490 | 9,106,430 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 2,706,748 | 806,288 | 367,497 | 475,854 | 495,208 | 13,205,947 | 9,105,996 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 2,707,643 | 806,288 | 367,497 | 476,004 | 495,564 | 13,205,969 | 9,106,160 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 2,707,645 | 806,288 | 367,497 | 475,504 | 495,038 | 13,207,606 | 9,106,841 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 2,707,643 | 806,288 | 367,497 | 475,715 | 495,220 | 13,207,653 | 9,105,684 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 2,706,245 | 806,288 | 367,497 | 475,688 | 495,008 | 13,206,144 | 9,105,874 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 2,707,644 | 806,288 | 367,497 | 475,855 | 495,255 | 13,205,721 | 9,105,615 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 2,706,249 | 806,288 | 367,497 | 475,658 | 495,063 | 13,207,602 | 9,106,018 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 2,707,646 | 806,288 | 367,497 | 475,497 | 495,286 | 13,205,967 | 9,106,377 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 2,706,247 | 806,288 | 367,497 | 476,299 | 495,394 | 13,207,518 | 9,105,764 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 2,706,244 | 806,288 | 367,497 | 475,510 | 495,048 | 13,207,556 | 9,106,495 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 2,707,639 | 806,288 | 367,497 | 475,508 | 495,005 | 13,207,554 | 9,105,777 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 2,707,643 | 806,288 | 367,497 | 476,065 | 495,175 | 13,207,467 | 9,106,408 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 2,707,644 | 806,290 | 367,497 | 475,552 | 494,926 | 13,207,094 | 9,105,844 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 2,706,754 | 806,291 | 367,497 | 475,762 | 495,186 | 13,205,724 | 9,106,519 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 2,706,253 | 806,291 | 367,497 | 475,512 | 495,192 | 13,207,233 | 9,106,135 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 2,707,643 | 806,291 | 367,497 | 475,922 | 495,214 | 13,207,403 | 9,106,717 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 2,707,641 | 806,291 | 367,497 | 476,024 | 495,144 | 13,206,164 | 9,105,971 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 346,497 | 478,608 | 8,902,307 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 346,497 | 478,565 | 8,901,904 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 346,497 | 478,514 | 8,902,582 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 346,497 | 478,643 | 8,902,770 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 346,497 | 478,604 | 8,902,544 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 346,497 | 478,438 | 8,902,380 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 346,497 | 478,697 | 8,902,918 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 346,497 | 478,452 | 8,902,198 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 346,497 | 478,482 | 8,902,694 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 346,497 | 478,565 | 8,902,562 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 346,497 | 478,440 | 8,902,364 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 346,497 | 478,413 | 8,902,586 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 346,497 | 478,428 | 8,902,356 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 346,497 | 478,762 | 8,902,680 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 346,497 | 478,707 | 8,901,848 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 346,497 | 478,520 | 8,902,234 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 346,497 | 478,841 | 8,902,586 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 346,497 | 478,729 | 8,902,566 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 346,497 | 478,633 | 8,902,293 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 346,497 | 478,476 | 8,902,430 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 346,497 | 478,708 | 8,902,658 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 346,497 | 478,687 | 8,902,464 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 346,497 | 478,339 | 8,902,557 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 346,497 | 478,420 | 8,902,212 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 346,497 | 478,514 | 8,903,043 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 346,497 | 478,551 | 8,902,833 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 346,497 | 478,276 | 8,902,420 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 346,497 | 478,790 | 8,902,289 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 346,497 | 478,565 | 8,902,783 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 346,497 | 478,314 | 8,903,258 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 346,497 | 479,349 | 8,902,525 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 346,497 | 479,719 | 8,902,591 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 346,497 | 479,680 | 8,902,704 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 346,497 | 479,549 | 8,902,236 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 346,497 | 479,746 | 8,902,751 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 346,497 | 479,389 | 8,902,700 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 346,497 | 479,677 | 8,902,589 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 346,497 | 479,415 | 8,902,532 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 346,497 | 487,992 | 8,902,840 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 346,497 | 487,761 | 8,902,562 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 346,497 | 487,750 | 8,903,040 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 346,497 | 487,761 | 8,902,854 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 346,497 | 487,922 | 8,902,634 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 346,497 | 487,418 | 8,902,460 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 346,497 | 487,834 | 8,902,439 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 346,497 | 488,106 | 8,903,111 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 346,497 | 487,566 | 8,902,693 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 346,497 | 487,653 | 8,902,955 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 346,497 | 487,750 | 8,902,697 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 346,497 | 487,814 | 8,902,964 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 346,497 | 487,792 | 8,902,627 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 346,497 | 487,598 | 8,902,254 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 346,497 | 488,201 | 8,902,738 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 346,497 | 487,780 | 8,902,210 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 346,497 | 487,909 | 8,902,609 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 346,497 | 487,911 | 8,902,051 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 346,497 | 487,884 | 8,902,906 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 346,497 | 487,787 | 8,902,744 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 346,497 | 487,652 | 8,902,180 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 346,497 | 487,740 | 8,902,925 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 346,497 | 487,704 | 8,902,647 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 346,497 | 487,814 | 8,902,413 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 180,106 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 180,106 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 180,106 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 180,106 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 180,106 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 180,106 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 180,106 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 180,106 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 180,106 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 180,106 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 180,106 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 180,106 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 180,106 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 180,106 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 180,106 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 180,106 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 180,106 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 180,106 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 180,106 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 180,106 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 180,106 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 180,106 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 180,106 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 180,106 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 180,106 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 180,106 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 180,106 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 180,106 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 180,106 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 180,106 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 180,106 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 180,106 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 180,106 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 181,606 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 181,606 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 181,606 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 181,606 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 181,606 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 181,706 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 181,706 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 181,706 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 181,706 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 181,706 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 181,706 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 181,706 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 181,706 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 181,706 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 181,706 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 181,706 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 181,706 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 181,706 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 181,706 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 181,706 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 181,706 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 181,706 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 181,706 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 181,706 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 181,706 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 181,706 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 181,706 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 181,706 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 181,706 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 231,255 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 231,255 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 231,255 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 231,255 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 231,255 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 231,255 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 231,255 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 231,255 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 231,255 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 231,255 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 231,255 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 231,255 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 231,255 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 231,255 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 231,255 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 231,255 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 231,255 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 231,255 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 231,255 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 231,255 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 231,255 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 231,255 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 231,255 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 231,255 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 231,255 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 231,255 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 231,255 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 231,255 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 231,255 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 231,255 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 231,255 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 231,255 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 231,255 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 232,755 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 232,755 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 232,755 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 232,755 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 232,755 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 232,855 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 232,855 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 232,855 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 232,855 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 232,855 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 232,855 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 232,855 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 232,855 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 232,855 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 232,855 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 232,855 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 232,855 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 232,855 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 232,855 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 232,855 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 232,855 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 232,855 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 232,855 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 232,855 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 232,855 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 232,855 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 232,855 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 232,855 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 232,855 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 65,082 | 1,366 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 65,082 | 1,366 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 65,082 | 1,366 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 65,106 | 1,366 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 65,106 | 1,366 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 65,106 | 1,366 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 65,106 | 1,366 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 65,130 | 1,366 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 65,130 | 1,366 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 65,130 | 1,366 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 65,130 | 1,366 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 65,130 | 1,366 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 65,130 | 1,366 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 65,130 | 1,366 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 65,130 | 1,366 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 65,130 | 1,366 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 65,082 | 1,366 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 65,082 | 1,366 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 65,082 | 1,366 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 65,106 | 1,366 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 65,106 | 1,366 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 65,106 | 1,366 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 65,106 | 1,366 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 65,106 | 1,366 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 65,106 | 1,366 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 65,106 | 1,366 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 65,106 | 1,366 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 65,106 | 1,366 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 65,106 | 1,366 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 65,106 | 1,366 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 64,930 | 1,366 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 64,930 | 1,366 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 64,930 | 1,366 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 64,917 | 1,369 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 64,885 | 1,369 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 64,845 | 1,369 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 64,845 | 1,369 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 65,125 | 1,369 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 65,086 | 1,370 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 65,086 | 1,370 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 65,086 | 1,370 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 65,086 | 1,370 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 65,086 | 1,370 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 65,086 | 1,370 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 65,086 | 1,370 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 65,086 | 1,370 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 65,086 | 1,370 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 65,086 | 1,370 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 65,086 | 1,370 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 65,086 | 1,370 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 65,094 | 1,370 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 65,094 | 1,370 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 65,094 | 1,370 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 65,094 | 1,370 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 65,094 | 1,370 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 65,094 | 1,370 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 65,094 | 1,370 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 65,094 | 1,370 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 64,938 | 1,370 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 64,938 | 1,370 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 64,938 | 1,370 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 64,938 | 1,370 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 10,995 | 6,215 | 6,216 | 11,316 | 10,763 | 22,343 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 10,995 | 6,215 | 6,216 | 11,316 | 10,763 | 22,343 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 10,995 | 6,215 | 6,216 | 11,316 | 10,763 | 22,343 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 10,995 | 6,215 | 6,216 | 11,316 | 10,763 | 22,341 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 10,995 | 6,215 | 6,216 | 11,316 | 10,763 | 22,341 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 10,993 | 6,215 | 6,216 | 11,315 | 10,761 | 22,339 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 10,991 | 6,216 | 6,217 | 11,317 | 10,763 | 22,337 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 10,991 | 6,216 | 6,217 | 11,317 | 10,763 | 22,337 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 10,991 | 6,216 | 6,217 | 11,317 | 10,763 | 22,337 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 10,943 | 6,195 | 6,196 | 11,269 | 10,715 | 22,343 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 10,943 | 6,195 | 6,196 | 11,269 | 10,715 | 22,343 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 10,943 | 6,195 | 6,196 | 11,269 | 10,715 | 22,343 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 10,943 | 6,195 | 6,196 | 11,269 | 10,715 | 22,343 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 10,945 | 6,194 | 6,195 | 11,267 | 10,713 | 22,345 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 10,947 | 6,196 | 6,197 | 11,269 | 10,715 | 22,347 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 10,947 | 6,196 | 6,197 | 11,269 | 10,715 | 22,347 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 10,947 | 6,196 | 6,197 | 11,269 | 10,715 | 22,347 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 10,947 | 6,196 | 6,197 | 11,269 | 10,715 | 22,347 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 10,947 | 6,196 | 6,197 | 11,269 | 10,715 | 22,347 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 10,947 | 6,196 | 6,197 | 11,269 | 10,715 | 22,347 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 10,947 | 6,196 | 6,197 | 11,269 | 10,715 | 22,347 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 10,947 | 6,196 | 6,197 | 11,269 | 10,715 | 22,347 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 10,947 | 6,196 | 6,197 | 11,269 | 10,715 | 22,347 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 10,947 | 6,196 | 6,197 | 11,269 | 10,715 | 22,347 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 10,947 | 6,196 | 6,197 | 11,269 | 10,715 | 22,347 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 10,947 | 6,196 | 6,197 | 11,269 | 10,715 | 22,347 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 10,949 | 6,197 | 6,198 | 11,271 | 10,717 | 22,349 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 10,949 | 6,197 | 6,198 | 11,271 | 10,717 | 22,349 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 10,949 | 6,197 | 6,198 | 11,271 | 10,717 | 22,349 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 10,949 | 6,197 | 6,198 | 11,271 | 10,717 | 22,349 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 10,949 | 6,197 | 6,198 | 11,271 | 10,717 | 22,349 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 10,949 | 6,197 | 6,198 | 11,271 | 10,717 | 22,349 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 10,949 | 6,197 | 6,198 | 11,271 | 10,717 | 22,349 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 10,949 | 6,197 | 6,198 | 11,271 | 10,717 | 22,349 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 10,949 | 6,197 | 6,198 | 11,271 | 10,717 | 22,353 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 10,949 | 6,197 | 6,198 | 11,271 | 10,717 | 22,353 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 10,949 | 6,197 | 6,198 | 11,271 | 10,717 | 22,353 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 10,949 | 6,197 | 6,198 | 11,271 | 10,717 | 22,353 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 1,921,801 | 53,951 | 54,942 | 3,218,668 | 661,900 | 176,563 | 44,849,844 | 2,634,980 | 2,654,193 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 1,921,809 | 53,951 | 54,947 | 3,218,777 | 661,746 | 176,641 | 44,849,870 | 2,634,910 | 2,654,364 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 1,921,809 | 53,951 | 54,952 | 3,218,744 | 661,701 | 176,618 | 44,849,862 | 2,634,953 | 2,654,174 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 1,921,809 | 53,951 | 54,948 | 3,218,784 | 661,918 | 176,593 | 44,849,854 | 2,634,915 | 2,654,166 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 1,921,825 | 53,951 | 54,960 | 3,218,701 | 661,896 | 176,569 | 44,849,827 | 2,634,913 | 2,654,316 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 1,921,825 | 53,951 | 54,961 | 3,218,690 | 661,901 | 176,511 | 44,849,856 | 2,634,976 | 2,654,203 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 1,921,809 | 53,951 | 54,947 | 3,218,680 | 661,702 | 176,547 | 44,849,857 | 2,634,918 | 2,654,122 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 1,921,810 | 53,951 | 54,948 | 3,218,792 | 661,710 | 176,636 | 44,849,887 | 2,634,939 | 2,654,360 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 1,921,809 | 53,951 | 54,948 | 3,218,740 | 661,718 | 176,639 | 44,849,855 | 2,634,913 | 2,654,113 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 1,921,793 | 53,951 | 54,931 | 3,218,642 | 661,692 | 176,636 | 44,851,063 | 2,634,916 | 2,654,134 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 1,921,810 | 53,967 | 54,948 | 3,218,719 | 661,707 | 176,629 | 44,849,867 | 2,634,889 | 2,654,179 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 1,921,833 | 53,951 | 54,970 | 3,218,668 | 661,706 | 176,544 | 44,849,876 | 2,634,990 | 2,654,166 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 1,921,809 | 53,951 | 54,948 | 3,218,794 | 661,705 | 176,502 | 44,849,993 | 2,634,936 | 2,654,351 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 1,921,809 | 53,951 | 54,952 | 3,218,712 | 661,698 | 176,544 | 44,849,835 | 2,634,964 | 2,654,183 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 1,921,809 | 53,951 | 54,946 | 3,218,693 | 661,749 | 176,629 | 44,851,072 | 2,634,967 | 2,654,213 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 1,921,809 | 53,951 | 54,942 | 3,218,727 | 661,707 | 176,576 | 44,849,857 | 2,634,923 | 2,654,118 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 1,921,825 | 53,951 | 54,960 | 3,218,685 | 661,945 | 176,561 | 44,849,869 | 2,634,924 | 2,654,178 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 1,921,961 | 53,951 | 55,097 | 3,218,645 | 661,756 | 176,524 | 44,845,239 | 2,635,014 | 2,654,056 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 1,921,809 | 53,951 | 54,952 | 3,218,691 | 661,742 | 176,604 | 44,846,491 | 2,635,039 | 2,654,085 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 1,921,825 | 53,951 | 54,958 | 3,218,721 | 661,738 | 176,459 | 44,845,260 | 2,635,033 | 2,654,078 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 1,921,913 | 53,951 | 55,052 | 3,218,786 | 661,689 | 176,583 | 44,858,901 | 2,634,940 | 2,654,384 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 1,921,809 | 53,951 | 54,942 | 3,218,694 | 661,702 | 176,529 | 44,863,324 | 2,634,938 | 2,654,362 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 1,921,810 | 53,951 | 54,954 | 3,218,743 | 661,611 | 176,521 | 44,864,602 | 2,635,027 | 2,654,287 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 1,921,809 | 53,951 | 54,950 | 3,218,665 | 661,723 | 176,613 | 44,864,609 | 2,634,991 | 2,654,373 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 1,921,793 | 53,951 | 54,932 | 3,218,704 | 661,687 | 176,495 | 44,863,340 | 2,634,975 | 2,654,389 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 1,921,825 | 53,951 | 54,960 | 3,218,696 | 661,696 | 176,329 | 44,869,087 | 2,634,979 | 2,654,396 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 1,922,001 | 53,981 | 55,041 | 3,218,693 | 661,653 | 176,513 | 44,869,054 | 2,634,996 | 2,654,375 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 1,921,809 | 53,951 | 54,946 | 3,218,650 | 661,698 | 176,436 | 44,869,121 | 2,634,987 | 2,654,332 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 1,921,825 | 53,951 | 54,964 | 3,218,615 | 661,693 | 176,538 | 44,869,065 | 2,635,016 | 2,654,403 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 1,921,809 | 53,951 | 54,948 | 3,218,804 | 661,699 | 176,560 | 44,870,299 | 2,635,014 | 2,654,376 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 1,921,791 | 53,955 | 54,920 | 3,219,268 | 661,715 | 176,266 | 44,862,152 | 2,635,376 | 2,652,875 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 1,921,807 | 53,955 | 54,936 | 3,219,291 | 661,726 | 176,176 | 44,863,396 | 2,635,332 | 2,652,855 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 1,921,791 | 53,955 | 54,920 | 3,219,252 | 661,729 | 176,192 | 44,862,141 | 2,635,373 | 2,652,858 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 1,920,822 | 54,064 | 54,956 | 3,239,702 | 240,484 | 176,291 | 44,993,954 | 2,635,845 | 2,655,148 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 1,920,810 | 54,064 | 54,935 | 3,239,799 | 240,557 | 176,339 | 44,992,741 | 2,635,840 | 2,655,142 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 1,920,718 | 53,968 | 54,939 | 3,259,493 | 240,279 | 176,322 | 44,998,803 | 2,635,995 | 2,655,144 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 1,920,718 | 53,968 | 54,940 | 3,259,515 | 240,592 | 176,209 | 45,000,051 | 2,635,989 | 2,655,155 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 1,920,725 | 53,910 | 54,948 | 3,258,506 | 237,796 | 176,294 | 45,005,831 | 2,635,843 | 2,655,150 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 1,921,737 | 53,949 | 54,957 | 3,273,577 | 237,381 | 176,310 | 45,077,674 | 2,634,604 | 2,655,548 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 1,921,737 | 53,917 | 54,952 | 3,273,613 | 237,683 | 176,385 | 45,078,856 | 2,634,611 | 2,655,553 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 1,921,737 | 53,917 | 54,954 | 3,273,764 | 237,648 | 176,418 | 45,077,696 | 2,634,670 | 2,655,561 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 1,921,737 | 53,917 | 54,956 | 3,277,840 | 237,592 | 176,024 | 45,075,885 | 2,634,501 | 2,655,518 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 1,921,753 | 53,949 | 54,968 | 3,277,660 | 237,586 | 176,049 | 45,075,887 | 2,634,528 | 2,655,499 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 1,921,737 | 53,917 | 54,954 | 3,277,752 | 237,612 | 176,023 | 45,075,849 | 2,634,560 | 2,655,519 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 1,921,737 | 53,917 | 54,958 | 3,277,778 | 237,598 | 176,063 | 45,086,078 | 2,634,508 | 2,655,460 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 1,921,745 | 53,917 | 54,965 | 3,277,722 | 237,880 | 176,033 | 45,075,892 | 2,634,569 | 2,655,548 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 1,921,737 | 53,917 | 54,956 | 3,277,867 | 237,634 | 176,029 | 45,075,893 | 2,634,566 | 2,655,518 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 1,921,737 | 53,917 | 54,956 | 3,277,661 | 237,650 | 176,043 | 45,075,847 | 2,634,583 | 2,655,507 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 1,921,737 | 53,917 | 54,958 | 3,277,845 | 237,644 | 176,040 | 45,075,873 | 2,634,486 | 2,655,495 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 1,921,737 | 53,917 | 54,953 | 3,277,634 | 237,680 | 176,006 | 45,075,909 | 2,634,539 | 2,655,541 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 1,921,753 | 54,013 | 54,966 | 3,277,579 | 237,664 | 175,990 | 45,075,896 | 2,634,553 | 2,655,465 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 1,921,737 | 54,013 | 54,952 | 3,277,873 | 237,670 | 176,032 | 45,075,910 | 2,634,535 | 2,655,497 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 1,921,737 | 53,917 | 54,952 | 3,277,860 | 237,684 | 176,004 | 45,075,850 | 2,634,489 | 2,655,516 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 1,921,737 | 53,981 | 54,957 | 3,277,797 | 237,669 | 176,020 | 45,075,853 | 2,634,545 | 2,655,534 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 1,921,737 | 53,917 | 54,953 | 3,277,766 | 237,645 | 176,025 | 45,075,855 | 2,634,573 | 2,655,551 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 1,921,737 | 54,013 | 54,958 | 3,277,545 | 237,685 | 176,037 | 45,075,865 | 2,634,483 | 2,655,541 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 1,921,737 | 53,917 | 54,950 | 3,277,719 | 237,631 | 175,986 | 45,075,876 | 2,634,480 | 2,655,518 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 1,921,737 | 53,917 | 54,954 | 3,277,611 | 237,646 | 176,000 | 45,075,865 | 2,634,552 | 2,655,530 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 1,921,743 | 53,917 | 54,969 | 3,277,598 | 237,685 | 176,021 | 45,075,895 | 2,634,579 | 2,655,550 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 1,921,823 | 54,109 | 55,036 | 3,277,804 | 237,685 | 176,021 | 45,076,328 | 2,634,600 | 2,655,526 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 1,921,737 | 53,917 | 54,961 | 3,277,603 | 237,644 | 175,983 | 45,075,899 | 2,634,484 | 2,655,519 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 1,921,729 | 53,917 | 54,960 | 3,277,660 | 237,647 | 176,039 | 45,075,908 | 2,634,559 | 2,655,531 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 1,920,734 | 43,628 | 45,276 | 3,244,996 | 593,198 | 605,050 | 44,798,159 | 1,924,898 | 1,958,578 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 1,920,739 | 43,628 | 45,285 | 3,244,990 | 593,054 | 605,284 | 44,798,146 | 1,924,864 | 1,958,576 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 1,920,737 | 43,628 | 45,285 | 3,245,053 | 593,038 | 604,934 | 44,798,145 | 1,924,888 | 1,958,553 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 1,920,740 | 43,628 | 45,285 | 3,244,988 | 593,073 | 605,180 | 44,798,138 | 1,924,879 | 1,958,566 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 1,920,752 | 43,628 | 45,300 | 3,245,009 | 593,223 | 605,048 | 44,798,161 | 1,924,915 | 1,958,567 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 1,920,748 | 43,628 | 45,300 | 3,245,014 | 592,981 | 605,218 | 44,798,135 | 1,924,905 | 1,958,573 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 1,920,732 | 43,628 | 45,285 | 3,244,980 | 593,134 | 605,233 | 44,798,152 | 1,924,867 | 1,958,533 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 1,920,739 | 43,628 | 45,284 | 3,245,000 | 593,200 | 605,224 | 44,798,120 | 1,924,904 | 1,958,584 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 1,920,740 | 43,628 | 45,284 | 3,245,012 | 593,450 | 605,283 | 44,798,165 | 1,924,933 | 1,958,514 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 1,920,724 | 43,628 | 45,268 | 3,245,006 | 593,175 | 605,242 | 44,798,133 | 1,924,937 | 1,958,588 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 1,920,734 | 43,644 | 45,284 | 3,245,018 | 593,047 | 605,007 | 44,798,152 | 1,924,854 | 1,958,555 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 1,920,755 | 43,628 | 45,309 | 3,245,035 | 593,019 | 605,251 | 44,798,137 | 1,924,869 | 1,958,598 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 1,920,736 | 43,628 | 45,285 | 3,245,066 | 593,166 | 605,227 | 44,798,119 | 1,924,926 | 1,958,524 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 1,920,739 | 43,628 | 45,284 | 3,245,053 | 593,000 | 605,219 | 44,798,156 | 1,924,904 | 1,958,568 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 1,920,730 | 43,628 | 45,284 | 3,245,079 | 593,035 | 605,264 | 44,798,124 | 1,924,928 | 1,958,574 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 1,920,744 | 43,628 | 45,285 | 3,245,033 | 593,260 | 605,129 | 44,798,152 | 1,924,857 | 1,958,555 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 1,920,750 | 43,628 | 45,300 | 3,245,048 | 593,248 | 605,283 | 44,798,162 | 1,924,836 | 1,958,495 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 1,920,891 | 43,628 | 45,436 | 3,245,029 | 593,196 | 604,996 | 44,802,360 | 1,924,835 | 1,958,886 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 1,920,739 | 43,628 | 45,285 | 3,244,914 | 593,213 | 604,945 | 44,801,844 | 1,924,803 | 1,958,856 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 1,920,750 | 43,628 | 45,300 | 3,244,960 | 593,214 | 605,189 | 44,801,838 | 1,924,842 | 1,958,868 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 1,920,842 | 43,611 | 45,372 | 3,245,051 | 593,263 | 604,992 | 44,798,546 | 1,924,844 | 1,958,836 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 1,920,749 | 43,611 | 45,268 | 3,245,069 | 593,201 | 605,029 | 44,798,430 | 1,924,844 | 1,958,927 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 1,920,751 | 43,611 | 45,266 | 3,245,041 | 593,443 | 604,930 | 44,807,441 | 1,925,025 | 1,958,921 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 1,920,753 | 43,611 | 45,266 | 3,245,062 | 593,221 | 604,966 | 44,807,461 | 1,924,876 | 1,958,823 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 1,920,738 | 43,611 | 45,252 | 3,245,047 | 593,233 | 604,841 | 44,804,863 | 1,924,920 | 1,958,870 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 1,920,772 | 43,611 | 45,283 | 3,245,026 | 593,199 | 604,949 | 44,804,866 | 1,924,920 | 1,958,915 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 1,920,940 | 43,641 | 45,362 | 3,245,009 | 593,219 | 604,990 | 44,804,921 | 1,924,909 | 1,958,800 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 1,920,744 | 43,611 | 45,267 | 3,245,079 | 593,209 | 605,186 | 44,805,451 | 1,924,925 | 1,958,821 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 1,920,756 | 43,611 | 45,284 | 3,244,948 | 593,215 | 605,025 | 44,804,916 | 1,924,933 | 1,958,884 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 1,920,744 | 43,611 | 45,266 | 3,245,098 | 593,179 | 604,953 | 44,804,886 | 1,924,957 | 1,958,859 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 1,920,733 | 43,630 | 45,268 | 3,245,037 | 593,491 | 604,981 | 44,839,233 | 1,925,294 | 1,958,964 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 1,920,733 | 43,630 | 45,283 | 3,245,050 | 593,174 | 605,000 | 44,838,696 | 1,925,258 | 1,959,006 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 1,920,724 | 43,631 | 45,267 | 3,245,011 | 593,186 | 604,928 | 44,838,676 | 1,925,293 | 1,959,003 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 1,919,747 | 43,739 | 45,301 | 3,264,634 | 592,362 | 605,898 | 44,955,080 | 1,925,853 | 1,959,714 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 1,919,751 | 43,733 | 45,275 | 3,264,677 | 592,285 | 605,925 | 44,955,086 | 1,925,953 | 1,959,766 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 1,919,660 | 43,628 | 45,255 | 3,284,394 | 592,276 | 605,921 | 44,954,058 | 1,925,932 | 1,959,719 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 1,919,656 | 43,627 | 45,255 | 3,284,510 | 592,061 | 605,813 | 44,954,116 | 1,925,895 | 1,959,735 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 1,919,669 | 43,616 | 45,276 | 3,283,430 | 592,210 | 606,167 | 44,961,172 | 1,925,815 | 1,959,775 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 1,920,672 | 43,639 | 45,301 | 3,289,507 | 592,304 | 606,090 | 45,030,312 | 1,924,545 | 1,960,192 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 1,920,673 | 43,607 | 45,299 | 3,289,537 | 592,338 | 606,065 | 45,030,934 | 1,924,505 | 1,960,236 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 1,920,675 | 43,607 | 45,301 | 3,289,502 | 592,329 | 606,029 | 45,030,376 | 1,924,527 | 1,960,248 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 1,920,672 | 43,607 | 45,299 | 3,292,960 | 591,913 | 606,244 | 45,041,223 | 1,924,602 | 1,960,110 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 1,920,687 | 43,639 | 45,315 | 3,292,872 | 592,001 | 605,806 | 45,041,051 | 1,924,661 | 1,960,130 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 1,920,676 | 43,607 | 45,299 | 3,293,041 | 591,961 | 605,868 | 45,041,010 | 1,924,551 | 1,960,196 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 1,920,677 | 43,607 | 45,301 | 3,292,699 | 591,892 | 606,178 | 45,041,048 | 1,924,661 | 1,960,208 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 1,920,685 | 43,607 | 45,309 | 3,292,745 | 591,927 | 606,033 | 45,053,059 | 1,924,711 | 1,960,210 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 1,920,676 | 43,608 | 45,300 | 3,292,847 | 591,905 | 605,747 | 45,052,996 | 1,924,554 | 1,960,149 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 1,920,671 | 43,607 | 45,301 | 3,292,925 | 591,834 | 606,164 | 45,041,027 | 1,924,648 | 1,960,192 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 1,920,685 | 43,607 | 45,301 | 3,292,933 | 592,045 | 606,202 | 45,041,065 | 1,924,575 | 1,960,147 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 1,920,673 | 43,607 | 45,299 | 3,292,650 | 592,034 | 606,201 | 45,041,042 | 1,924,672 | 1,960,219 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 1,920,691 | 43,703 | 45,317 | 3,292,807 | 591,954 | 606,158 | 45,041,032 | 1,924,627 | 1,960,161 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 1,920,671 | 43,703 | 45,301 | 3,292,840 | 591,678 | 605,972 | 45,053,053 | 1,924,678 | 1,960,146 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 1,920,675 | 43,607 | 45,301 | 3,292,963 | 592,007 | 606,142 | 45,053,008 | 1,924,852 | 1,960,200 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 1,920,673 | 43,671 | 45,301 | 3,292,893 | 592,094 | 606,142 | 45,041,057 | 1,924,609 | 1,960,146 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 1,920,671 | 43,608 | 45,300 | 3,292,923 | 591,910 | 606,070 | 45,052,984 | 1,924,627 | 1,960,153 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 1,920,676 | 43,702 | 45,301 | 3,292,821 | 592,034 | 605,821 | 45,041,049 | 1,924,630 | 1,960,147 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 1,920,676 | 43,607 | 45,301 | 3,292,702 | 591,998 | 606,219 | 45,041,016 | 1,924,667 | 1,960,108 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 1,920,670 | 43,607 | 45,300 | 3,292,632 | 592,035 | 606,152 | 45,041,072 | 1,924,639 | 1,960,145 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 1,920,677 | 43,600 | 45,315 | 3,292,991 | 592,225 | 606,037 | 45,041,250 | 1,924,889 | 1,960,192 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 1,920,752 | 43,792 | 45,396 | 3,292,885 | 592,288 | 605,938 | 45,041,266 | 1,924,846 | 1,960,163 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 1,920,661 | 43,600 | 45,307 | 3,292,638 | 592,441 | 606,194 | 45,041,246 | 1,924,732 | 1,960,181 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 1,920,655 | 43,600 | 45,299 | 3,292,665 | 592,258 | 606,132 | 45,041,264 | 1,924,837 | 1,960,115 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 1,973,412 | 46,162 | 46,900 | 3,560,289 | 148,782 | 511,268 | 45,980,332 | 916,504 | 932,917 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 1,973,428 | 46,161 | 46,915 | 3,564,429 | 148,798 | 511,205 | 45,980,650 | 916,496 | 932,804 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 1,973,428 | 46,162 | 46,915 | 3,565,364 | 148,801 | 511,378 | 45,980,639 | 916,505 | 932,899 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 1,973,428 | 46,161 | 46,915 | 3,562,638 | 148,816 | 511,394 | 45,976,612 | 916,412 | 932,865 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 1,973,444 | 46,161 | 46,931 | 3,561,040 | 148,774 | 511,419 | 45,980,255 | 916,495 | 932,920 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 1,973,444 | 46,162 | 46,932 | 3,564,528 | 148,811 | 511,379 | 45,980,641 | 916,502 | 932,916 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 1,973,428 | 46,161 | 46,915 | 3,564,008 | 148,754 | 511,231 | 45,980,301 | 916,461 | 932,903 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 1,973,429 | 46,161 | 46,916 | 3,564,915 | 148,772 | 511,314 | 45,980,307 | 916,531 | 932,950 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 1,973,428 | 46,161 | 46,915 | 3,562,414 | 148,753 | 511,343 | 45,980,287 | 916,568 | 932,845 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 1,973,412 | 46,162 | 46,900 | 3,560,828 | 148,793 | 511,252 | 45,980,291 | 916,543 | 932,915 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 1,973,429 | 46,177 | 46,916 | 3,562,974 | 148,792 | 511,367 | 45,980,662 | 916,582 | 932,900 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 1,973,444 | 46,161 | 46,931 | 3,562,723 | 148,806 | 511,285 | 45,980,302 | 916,547 | 932,887 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 1,973,428 | 46,161 | 46,915 | 3,563,674 | 148,790 | 511,369 | 45,980,286 | 916,510 | 932,853 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 1,973,428 | 46,161 | 46,916 | 3,565,407 | 148,776 | 511,384 | 45,980,674 | 916,497 | 932,852 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 1,973,428 | 46,161 | 46,915 | 3,560,726 | 148,809 | 511,217 | 45,980,647 | 916,465 | 932,928 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 1,973,428 | 46,161 | 46,915 | 3,562,791 | 148,813 | 511,376 | 45,980,235 | 916,544 | 932,813 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 1,973,444 | 46,161 | 46,931 | 3,565,335 | 148,793 | 511,395 | 45,980,246 | 916,486 | 932,898 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 1,973,524 | 46,162 | 47,012 | 3,563,252 | 148,803 | 511,212 | 45,980,371 | 916,856 | 932,960 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 1,973,428 | 46,161 | 46,915 | 3,560,900 | 148,806 | 511,334 | 45,980,419 | 916,836 | 932,837 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 1,973,444 | 46,161 | 46,931 | 3,564,622 | 148,831 | 511,212 | 45,980,398 | 916,763 | 932,841 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 1,973,528 | 46,161 | 47,011 | 3,561,803 | 149,105 | 511,392 | 45,980,663 | 916,660 | 932,900 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 1,973,432 | 46,161 | 46,915 | 3,564,102 | 148,822 | 511,451 | 45,980,335 | 916,675 | 932,839 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 1,973,433 | 46,161 | 46,915 | 3,560,378 | 148,802 | 511,421 | 45,968,585 | 916,669 | 932,909 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 1,973,432 | 46,161 | 46,915 | 3,562,257 | 149,095 | 511,422 | 45,968,559 | 916,684 | 932,810 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 1,973,416 | 46,162 | 46,899 | 3,561,458 | 148,802 | 511,458 | 45,980,331 | 916,648 | 932,835 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 1,973,448 | 46,162 | 46,931 | 3,561,331 | 149,136 | 511,463 | 45,980,333 | 916,685 | 932,872 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 1,973,624 | 46,181 | 47,011 | 3,558,316 | 148,834 | 511,464 | 45,973,023 | 916,687 | 932,822 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 1,973,432 | 46,162 | 46,915 | 3,563,272 | 148,799 | 511,460 | 45,980,314 | 916,709 | 932,874 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 1,973,448 | 46,162 | 46,931 | 3,564,630 | 149,079 | 511,296 | 45,972,673 | 916,699 | 932,874 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 1,973,432 | 46,161 | 46,915 | 3,563,238 | 148,790 | 511,212 | 45,972,978 | 916,697 | 932,827 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 1,973,390 | 46,165 | 46,897 | 3,564,357 | 149,098 | 511,438 | 45,976,956 | 916,571 | 932,843 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 1,973,406 | 46,166 | 46,914 | 3,562,685 | 149,206 | 511,449 | 45,977,288 | 916,603 | 932,815 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 1,973,390 | 46,165 | 46,898 | 3,562,172 | 149,119 | 511,471 | 45,977,338 | 916,614 | 932,871 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 1,972,429 | 46,275 | 46,930 | 3,577,912 | 149,193 | 511,883 | 46,112,920 | 917,180 | 933,523 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 1,972,429 | 46,275 | 46,914 | 3,587,178 | 149,199 | 511,925 | 46,111,069 | 917,166 | 933,510 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 1,972,329 | 46,180 | 46,915 | 3,620,303 | 149,155 | 511,928 | 46,119,450 | 917,232 | 933,551 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 1,972,329 | 46,180 | 46,915 | 3,614,835 | 149,547 | 511,917 | 46,119,229 | 917,147 | 933,566 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 1,972,349 | 46,196 | 46,930 | 3,613,711 | 149,092 | 511,905 | 46,115,068 | 917,190 | 933,557 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 1,973,362 | 46,237 | 46,927 | 3,618,345 | 149,011 | 512,004 | 46,187,343 | 915,635 | 933,773 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 1,973,362 | 46,205 | 46,927 | 3,620,210 | 148,987 | 512,023 | 46,187,322 | 915,724 | 933,769 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 1,973,362 | 46,205 | 46,927 | 3,618,931 | 148,953 | 512,015 | 46,187,695 | 915,696 | 933,799 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 1,973,362 | 46,205 | 46,927 | 3,620,162 | 149,189 | 512,967 | 46,198,630 | 915,560 | 936,125 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 1,973,378 | 46,237 | 46,943 | 3,628,862 | 148,943 | 513,216 | 46,198,622 | 915,705 | 936,146 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 1,973,362 | 46,205 | 46,927 | 3,628,294 | 148,893 | 512,970 | 46,198,972 | 915,640 | 936,135 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 1,973,362 | 46,206 | 46,928 | 3,634,384 | 148,850 | 513,004 | 46,198,998 | 915,596 | 936,180 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 1,973,362 | 46,205 | 46,927 | 3,627,248 | 148,938 | 513,156 | 46,198,875 | 915,632 | 936,204 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 1,973,362 | 46,206 | 46,928 | 3,626,909 | 148,879 | 513,118 | 46,198,947 | 915,632 | 936,211 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 1,973,362 | 46,205 | 46,927 | 3,633,157 | 148,902 | 511,997 | 46,198,638 | 915,668 | 936,096 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 1,973,362 | 46,205 | 46,927 | 3,628,661 | 148,900 | 513,034 | 46,198,650 | 915,697 | 936,178 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 1,973,362 | 46,205 | 46,927 | 3,630,057 | 148,957 | 513,084 | 46,198,990 | 915,760 | 936,079 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 1,973,362 | 46,301 | 46,928 | 3,628,649 | 148,860 | 512,977 | 46,198,624 | 915,656 | 936,147 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 1,973,362 | 46,301 | 46,927 | 3,628,542 | 149,212 | 512,965 | 46,198,638 | 915,706 | 936,160 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 1,973,362 | 46,205 | 46,927 | 3,625,522 | 148,928 | 513,087 | 46,198,629 | 915,651 | 936,174 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 1,973,362 | 46,253 | 46,927 | 3,626,875 | 148,984 | 512,987 | 46,199,001 | 915,693 | 936,160 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 1,973,362 | 46,206 | 46,928 | 3,631,793 | 148,900 | 512,953 | 46,203,170 | 915,653 | 936,247 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 1,973,362 | 46,301 | 46,927 | 3,620,407 | 148,894 | 513,046 | 46,198,638 | 915,663 | 936,145 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 1,973,362 | 46,205 | 46,928 | 3,628,789 | 148,914 | 512,985 | 46,198,959 | 915,619 | 936,144 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 1,973,362 | 46,205 | 46,927 | 3,615,679 | 148,887 | 513,178 | 46,198,619 | 915,628 | 936,050 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 1,973,371 | 46,206 | 46,943 | 3,628,338 | 149,234 | 512,580 | 46,203,971 | 915,709 | 936,162 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 1,973,451 | 46,397 | 47,008 | 3,626,919 | 149,052 | 512,558 | 46,203,941 | 915,636 | 936,109 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 1,973,356 | 46,205 | 46,928 | 3,607,345 | 149,154 | 512,680 | 46,203,982 | 915,631 | 936,180 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 1,973,356 | 46,205 | 46,928 | 3,602,284 | 149,167 | 512,504 | 46,203,613 | 915,645 | 936,034 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 58,261 | 772 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 58,261 | 772 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 58,261 | 772 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 58,261 | 772 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 58,261 | 772 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 58,261 | 772 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 58,261 | 772 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 58,261 | 772 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 58,261 | 772 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 58,261 | 772 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 58,261 | 772 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 58,261 | 772 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 58,261 | 772 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 58,261 | 772 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 58,261 | 772 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 58,261 | 772 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 58,261 | 772 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 58,261 | 772 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 58,261 | 772 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 58,261 | 772 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 58,261 | 772 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 58,261 | 772 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 58,261 | 772 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 58,261 | 772 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 58,261 | 772 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 58,261 | 772 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 58,261 | 772 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 58,261 | 772 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 58,261 | 772 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 58,261 | 772 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 58,261 | 772 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 58,261 | 772 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 58,261 | 772 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 58,291 | 787 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 58,291 | 787 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 58,291 | 787 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 58,291 | 787 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 58,291 | 787 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 58,292 | 788 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 58,292 | 788 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 58,292 | 788 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 58,292 | 788 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 58,292 | 788 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 58,292 | 788 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 58,292 | 788 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 58,292 | 788 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 58,292 | 788 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 58,292 | 788 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 58,292 | 788 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 58,292 | 788 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 58,292 | 788 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 58,292 | 788 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 58,292 | 788 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 58,292 | 788 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 58,292 | 788 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 58,292 | 788 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 58,292 | 788 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 58,292 | 788 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 58,292 | 788 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 58,292 | 788 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 58,292 | 788 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 58,292 | 788 |

## persist-write

| Commit | Date | Subject | dotnet Ranvier (persisted signal) | dotnet Ranvier (signal) |
|---|---|---|---:|---:|
| `878e5bd` | 2026-10-16 | feat(ci): CI performance/benchmark automation (#64) | — | — |
| `67d3919` | 2026-10-16 | docs(perf): added foundation for automatic benchmark visualisations (#74) | — | — |
| `3c1c144` | 2026-10-16 | docs: make benchmark charts interactive and publish after perf history (#75) | — | — |
| `1faeb63` | 2026-10-17 | perf(core): skip the row observer scan for memo-free projections (#78) | — | — |
| `66007fe` | 2026-10-17 | bench(counters): add memo-free row variant of the keyed scenarios (#77) | — | — |
| `28141fa` | 2026-10-17 | chore!: drop the net8.0 target; .NET 8 and 9 apps use netstandard2.1 (#80) | — | — |
| `2cb2977` | 2026-10-17 | perf(counters): mode-aware verdict with a Ranvier-free control (#81) | — | — |
| `e9107dd` | 2026-10-18 | fix(counters): read history records that predate the compiled flag (#86) | — | — |
| `1cc8633` | 2026-10-18 | ci: add contribution checks, docs-only runs, aggregate required checks and tree-based perf reuse (#87) | — | — |
| `a17bd85` | 2026-10-18 | feat(core): add the Drop and DropWhilePending flight policies (#88) | — | — |
| `1517a08` | 2026-10-18 | feat(core): add Projection.KeysVersion and ChangedSince, the window of keys a pass changed (#89) | — | — |
| `ab8fd7a` | 2026-10-18 | fix(trace): record a pending node's dropped failure through a one-statement hook (#90) | — | — |
| `2650152` | 2026-10-18 | docs(api): add the projection options, Owner error and ensureUniqueKeys members to the public API baseline (#91) | — | — |
| `f60ca58` | 2026-10-18 | fix(tracing): support NativeAOT in traced builds (#92) | — | — |
| `32c3a60` | 2026-10-18 | perf(core): store an owner's first cleanup in fields instead of two arrays (#93) | — | — |
| `1cd30d7` | 2026-10-18 | bench(counters): fix V8's semi-space at 32 MB in every Node run (#95) | — | — |
| `7853137` | 2026-10-18 | perf(core): share one empty array across observer sets and source lists, and give a first observer one slot (#94) | — | — |
| `305d009` | 2026-10-18 | docs(benchmarks): list the Node flags the CI counter runs use (#97) | — | — |
| `168f44e` | 2026-10-19 | perf(core): remove reactive edges by handle (#98) | — | — |
| `2191dd4` | 2026-10-19 | docs(benchmarks): add per-scenario instruction trends to the primitive pages (#99) | — | — |
| `d3c60be` | 2026-10-19 | ci(perf): skip measuring docs-only master pushes and still deploy their docs (#102) | — | — |
| `9f2398d` | 2026-10-19 | feat(core): add stream memos (#100) | — | — |
| `059dd7a` | 2026-10-19 | docs(benchmarks): chart the stream scenarios on their own page (#107) | — | — |
| `d35ba5a` | 2026-10-19 | test(streams): keep the stream trace test on the graph's thread (#108) | — | — |
| `31e061c` | 2026-10-19 | fix(core): skip empty stream wakes and gate Fable promise-only failures (#110) | — | — |
| `72df34d` | 2026-10-19 | feat(core): per-node flight policy for async memos (#112) | — | — |
| `8eb33e0` | 2026-10-19 | feat(wire): one wire text on .NET and Fable through a vendored, patched Thoth.Json (#109) (#117) | — | — |
| `365736e` | 2026-10-19 | ci: build the core before the packages so parallel builds stop racing on its outputs (#118) | — | — |
| `00560e1` | 2026-10-20 | feat(projection): add the next batch of incremental Projection functions (#119) | — | — |
| `e34da8c` | 2026-10-20 | fix(elmish): drop messages dispatched to an Mvu after its scope is disposed (#121) (#122) | — | — |
| `1de2432` | 2026-10-20 | build: exclude Ranvier.Tests.Fable from the root dotnet build (#125) | — | — |
| `61674f4` | 2026-10-20 | feat(fable): Fable.Ranvier PoC DOM (#42) | — | — |
| `b8cd416` | 2026-10-20 | fix(core): run queued projection passes before their readers so a flush reads no stale keys (#123) (#127) | — | — |
| `56ecfb1` | 2026-10-20 | ci: run the Fable playground's DOM, sugar and Chromium suites (#128) | — | — |
| `65844e1` | 2026-10-20 | feat(view): builder sugar for the template path (#120) | — | — |
| `4021de0` | 2026-10-20 | perf(core): compare sort keys with a cached unboxed comparer (#130) | — | — |
| `20f3323` | 2026-10-20 | bench: gate Node on --jitless instruction counts and report tiered Node without a gate (#131) | — | — |
| `18f9446` | 2026-10-20 | fix: release disposed mounts and spent scheduler-queue slots under Fable (#129) | — | — |
| `642d889` | 2026-10-20 | feat(view): view sugar v2 — free tags, inferred instances and declaration helpers (#133) | — | — |
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | — | — |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | — | — |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | — | — |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | — | — |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | — | — |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | — | — |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | — | — |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | — | — |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | — | — |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | — | — |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | — | — |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | — | — |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | — | — |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | — | — |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | — | — |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | — | — |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | — | — |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | — | — |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | — | — |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | — | — |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | — | — |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | — | — |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | — | — |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | — | — |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | — | — |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | — | — |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | — | — |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | — | — |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | — | — |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | — | — |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | — | — |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | — | — |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | — | — |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | — | — |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 7,013 | 883 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 7,013 | 883 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 7,029 | 883 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 7,044 | 883 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 7,014 | 884 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 7,062 | 900 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 7,014 | 884 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 7,062 | 900 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 7,046 | 884 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 7,046 | 884 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 7,046 | 884 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 7,014 | 884 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 7,014 | 884 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 7,046 | 884 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 7,030 | 884 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 7,014 | 884 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 7,093 | 884 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 7,013 | 884 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 7,013 | 884 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 7,029 | 884 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 7,013 | 884 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 7,013 | 884 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 7,029 | 884 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 7,013 | 884 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | — | — |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | — | — |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | — | — |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | — | — |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 1,070,385 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 1,070,355 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 1,070,343 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 1,070,241 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 1,070,307 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 1,070,225 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 1,070,326 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 1,070,208 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 1,070,324 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 1,070,166 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 1,070,313 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 1,070,219 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 1,070,360 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 1,070,308 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 1,070,391 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 1,070,200 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 1,070,358 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 1,070,247 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 1,070,169 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 1,070,653 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 1,070,251 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 1,070,228 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 1,070,203 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 1,070,363 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 1,070,280 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 1,070,263 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 1,070,385 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 1,070,279 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 1,070,245 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 1,070,249 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 1,070,346 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 1,070,282 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 1,070,232 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 1,071,789 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 1,071,688 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 1,071,811 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 1,071,750 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 1,071,772 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 1,071,763 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 1,071,716 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 1,071,765 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 1,071,781 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 1,071,750 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 1,071,685 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 1,071,734 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 1,071,687 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 1,071,720 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 1,071,750 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 1,071,835 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 1,071,703 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 1,071,801 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 1,071,976 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 1,071,888 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 1,071,959 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 1,071,864 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 1,071,836 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 1,071,928 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 1,071,762 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 1,071,710 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 1,071,811 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 1,071,831 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 1,071,720 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 36,473 | 34,310 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 36,489 | 34,326 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 36,489 | 34,326 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 36,473 | 34,310 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 36,473 | 34,310 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 36,489 | 34,326 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 36,489 | 34,326 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 36,490 | 34,326 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 36,474 | 34,310 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 36,490 | 34,326 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 36,490 | 34,326 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 36,474 | 34,310 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 36,490 | 34,326 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 36,490 | 34,326 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 36,506 | 34,342 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 36,490 | 34,326 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 36,586 | 34,422 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 36,474 | 34,310 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 36,474 | 34,310 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 36,490 | 34,326 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 36,474 | 34,310 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 36,474 | 34,310 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 36,474 | 34,310 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 36,538 | 34,374 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 36,506 | 34,328 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 36,506 | 34,326 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 36,490 | 34,326 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 36,490 | 34,326 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 36,474 | 34,310 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 36,490 | 34,326 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 36,472 | 34,308 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 36,472 | 34,308 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 36,472 | 34,308 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 36,521 | 34,356 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 36,503 | 34,338 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 36,487 | 34,340 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 36,487 | 34,340 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 36,486 | 34,321 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 36,385 | 34,250 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 36,385 | 34,250 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 36,385 | 34,250 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 36,385 | 34,250 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 36,385 | 34,266 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 36,385 | 34,250 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 36,385 | 34,250 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 36,385 | 34,250 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 36,385 | 34,250 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 36,385 | 34,250 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 36,385 | 34,250 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 36,385 | 34,268 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 36,417 | 34,278 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 36,385 | 34,250 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 36,470 | 34,346 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 36,385 | 34,250 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 36,385 | 34,250 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 36,385 | 34,250 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 36,385 | 34,250 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 36,385 | 34,250 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 36,385 | 34,250 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 36,401 | 34,266 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 36,481 | 34,346 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 36,385 | 34,250 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 50,415 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 50,431 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 50,415 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 50,415 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 50,431 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 50,431 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 50,415 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 50,431 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 50,415 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 50,415 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 50,431 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 50,415 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 50,431 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 50,431 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 50,431 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 50,431 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 50,513 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 50,415 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 50,415 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 50,431 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 50,415 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 50,431 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 50,415 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 50,431 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 50,431 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 50,431 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 50,415 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 50,431 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 50,431 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 50,431 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 50,413 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 50,429 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 50,413 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 50,441 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 50,425 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 50,426 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 50,426 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 50,441 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 50,460 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 50,460 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 50,460 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 50,476 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 50,508 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 50,460 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 50,460 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 50,460 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 50,460 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 50,460 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 50,476 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 50,460 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 50,556 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 50,556 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 50,556 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 50,460 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 50,460 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 50,460 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 50,460 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 50,460 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 50,460 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 50,492 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 50,556 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 50,460 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 779,156 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 779,170 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 779,156 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 779,155 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 779,171 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 779,172 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 779,156 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 779,171 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 779,155 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 779,156 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 779,171 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 779,154 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 779,170 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 779,170 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 779,170 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 779,170 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 779,267 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 779,155 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 779,155 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 779,171 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 779,147 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 779,163 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 779,147 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 779,163 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 779,148 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 779,148 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 779,147 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 779,164 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 779,164 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 779,147 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 779,148 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 779,166 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 779,150 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 780,175 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 780,156 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 780,074 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 780,073 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 780,074 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 780,068 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 780,068 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 780,068 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 780,084 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 780,116 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 780,069 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 780,069 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 780,067 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 780,068 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 780,068 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 780,067 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 780,068 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 780,164 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 780,164 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 780,164 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 780,068 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 780,068 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 780,067 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 780,067 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 780,068 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 780,084 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 780,085 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 780,180 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 780,084 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 676,332 | 1,131,490 | 9,832,897 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 676,332 | 1,130,471 | 9,832,698 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 676,332 | 1,131,668 | 9,832,805 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 676,238 | 1,130,708 | 9,832,837 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 676,302 | 1,131,265 | 9,832,279 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 676,238 | 1,131,336 | 9,833,037 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 676,233 | 1,131,188 | 9,832,759 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 676,230 | 1,131,084 | 9,833,305 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 676,230 | 1,130,744 | 9,833,942 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 676,230 | 1,131,332 | 9,834,179 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 676,230 | 1,131,482 | 9,833,829 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 677,830 | 1,131,281 | 9,833,492 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 676,231 | 1,131,644 | 9,833,939 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 676,230 | 1,130,643 | 9,834,145 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 676,230 | 1,131,227 | 9,834,142 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 676,230 | 1,131,183 | 9,833,800 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 675,723 | 1,131,493 | 9,833,582 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 675,724 | 1,131,458 | 9,834,209 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 675,724 | 1,131,578 | 9,834,282 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 675,857 | 1,131,085 | 9,832,639 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 675,825 | 1,130,425 | 9,834,047 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 675,889 | 1,130,848 | 9,834,015 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 675,857 | 1,130,618 | 9,832,925 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 675,825 | 1,131,028 | 9,832,638 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 675,724 | 1,131,008 | 9,833,970 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 675,756 | 1,130,374 | 9,834,088 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 677,319 | 1,130,889 | 9,833,171 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 675,756 | 1,130,590 | 9,833,904 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 675,788 | 1,130,390 | 9,834,176 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 675,756 | 1,130,648 | 9,833,736 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 679,491 | 1,132,733 | 9,833,916 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 677,891 | 1,132,784 | 9,834,020 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 676,291 | 1,133,147 | 9,833,920 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 680,585 | 1,148,609 | 9,894,252 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 680,553 | 1,148,774 | 9,893,486 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 680,712 | 1,148,418 | 9,894,453 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 682,248 | 1,148,325 | 9,894,110 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 680,651 | 1,148,192 | 9,893,767 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 680,651 | 1,148,275 | 9,894,039 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 680,651 | 1,148,744 | 9,893,748 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 682,251 | 1,148,281 | 9,894,189 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 680,651 | 1,147,343 | 9,887,486 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 680,651 | 1,147,580 | 9,887,459 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 680,715 | 1,147,878 | 9,888,668 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 680,683 | 1,147,747 | 9,888,423 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 680,651 | 1,147,490 | 9,888,617 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 680,651 | 1,147,818 | 9,888,400 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 680,651 | 1,147,848 | 9,888,447 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 682,251 | 1,147,606 | 9,888,328 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 680,652 | 1,147,481 | 9,888,270 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 680,652 | 1,147,319 | 9,828,978 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 680,652 | 1,147,526 | 9,828,297 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 680,652 | 1,147,247 | 9,829,156 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 682,252 | 1,147,118 | 9,828,900 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 680,652 | 1,146,953 | 9,828,860 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 680,716 | 1,147,507 | 9,829,123 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 680,652 | 1,147,149 | 9,828,685 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 680,652 | 1,147,693 | 9,829,307 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | — | — | — |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | — | — | — |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | — | — | — |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | — | — | — |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 59,899 | 79,968 | 1,763,357 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 59,899 | 79,968 | 1,763,357 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 59,899 | 79,968 | 1,763,357 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 59,899 | 79,968 | 1,763,357 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 59,899 | 79,968 | 1,763,357 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 59,899 | 79,968 | 1,763,357 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 59,899 | 79,968 | 1,763,357 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 59,899 | 79,968 | 1,763,357 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 59,899 | 79,968 | 1,763,357 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 59,899 | 79,968 | 1,763,357 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 59,899 | 79,968 | 1,763,357 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 59,899 | 79,968 | 1,763,357 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 59,899 | 79,968 | 1,763,357 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 59,899 | 79,968 | 1,763,357 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 59,899 | 79,968 | 1,763,357 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 59,899 | 79,968 | 1,763,357 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 59,899 | 79,968 | 1,763,357 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 59,899 | 79,968 | 1,763,357 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 59,899 | 79,968 | 1,763,357 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 59,899 | 79,968 | 1,763,357 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 59,899 | 79,968 | 1,763,357 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 59,899 | 79,968 | 1,763,357 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 59,899 | 79,968 | 1,763,357 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 59,899 | 79,968 | 1,763,357 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 59,899 | 79,968 | 1,763,357 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 59,899 | 79,968 | 1,763,357 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 59,899 | 79,968 | 1,763,357 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 59,899 | 79,968 | 1,763,357 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 59,899 | 79,967 | 1,763,357 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 59,899 | 79,967 | 1,763,357 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 59,899 | 79,968 | 1,763,363 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 59,899 | 79,968 | 1,763,363 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 59,899 | 79,968 | 1,763,363 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 59,914 | 80,437 | 1,763,703 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 59,914 | 80,437 | 1,763,703 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 59,914 | 80,437 | 1,763,703 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 59,914 | 80,437 | 1,763,703 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 59,914 | 80,437 | 1,763,703 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 59,915 | 80,445 | 1,763,775 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 59,915 | 80,445 | 1,763,775 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 59,915 | 80,445 | 1,763,775 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 59,915 | 80,445 | 1,764,075 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 59,915 | 80,445 | 1,764,075 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 59,915 | 80,445 | 1,764,075 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 59,915 | 80,445 | 1,764,075 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 59,915 | 80,445 | 1,764,075 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 59,915 | 80,445 | 1,764,075 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 59,915 | 80,445 | 1,764,075 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 59,915 | 80,445 | 1,764,075 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 59,915 | 80,445 | 1,764,075 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 59,915 | 80,445 | 1,764,075 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 59,915 | 80,445 | 1,764,075 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 59,915 | 80,445 | 1,764,075 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 59,915 | 80,445 | 1,764,075 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 59,915 | 80,445 | 1,764,075 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 59,915 | 80,445 | 1,764,075 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 59,915 | 80,445 | 1,764,075 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 59,915 | 80,445 | 1,764,075 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 59,915 | 80,445 | 1,764,075 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 59,915 | 80,445 | 1,764,075 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 59,915 | 80,445 | 1,764,075 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 59,915 | 80,445 | 1,764,075 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 82,921 | 107,033 | 2,006,140 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 82,921 | 107,033 | 2,006,140 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 82,921 | 107,033 | 2,006,140 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 82,921 | 107,033 | 2,006,140 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 82,921 | 107,033 | 2,006,144 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 82,921 | 107,033 | 2,006,140 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 82,921 | 107,033 | 2,006,140 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 82,921 | 107,030 | 2,006,140 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 82,921 | 107,033 | 2,006,141 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 82,921 | 107,033 | 2,006,140 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 82,921 | 107,033 | 2,006,140 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 82,921 | 107,033 | 2,006,141 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 82,921 | 107,033 | 2,006,140 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 82,921 | 107,033 | 2,006,140 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 82,921 | 107,033 | 2,006,140 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 82,921 | 107,031 | 2,006,140 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 82,921 | 107,030 | 2,006,140 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 82,921 | 107,034 | 2,006,144 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 82,921 | 107,032 | 2,006,144 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 82,921 | 107,034 | 2,006,144 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 82,921 | 107,035 | 2,006,147 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 82,921 | 107,035 | 2,006,147 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 82,921 | 107,035 | 2,006,147 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 82,921 | 107,035 | 2,006,147 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 82,921 | 107,035 | 2,006,148 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 82,921 | 107,036 | 2,006,148 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 82,921 | 107,035 | 2,006,148 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 82,921 | 107,035 | 2,006,148 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 82,921 | 107,035 | 2,006,148 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 82,921 | 107,035 | 2,006,148 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 82,921 | 107,036 | 2,006,441 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 82,921 | 107,037 | 2,006,442 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 82,921 | 107,037 | 2,006,441 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 83,778 | 108,194 | 2,030,348 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 83,778 | 108,194 | 2,030,350 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 83,778 | 108,194 | 2,030,349 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 83,778 | 108,194 | 2,030,350 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 83,778 | 108,194 | 2,030,350 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 84,378 | 108,947 | 2,037,685 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 84,378 | 108,947 | 2,037,686 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 84,378 | 108,947 | 2,037,685 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 84,378 | 108,947 | 2,038,136 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 84,378 | 108,947 | 2,038,136 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 84,378 | 108,943 | 2,038,136 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 84,378 | 108,944 | 2,038,136 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 84,378 | 108,947 | 2,038,136 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 84,378 | 108,947 | 2,038,136 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 84,378 | 108,945 | 2,038,136 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 84,378 | 108,947 | 2,038,136 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 84,378 | 108,947 | 2,038,136 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 84,378 | 108,947 | 2,038,136 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 84,378 | 108,947 | 2,038,136 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 84,378 | 108,947 | 2,038,136 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 84,378 | 108,943 | 2,038,136 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 84,378 | 108,947 | 2,038,136 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 84,378 | 108,945 | 2,038,136 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 84,378 | 108,947 | 2,038,136 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 84,378 | 108,947 | 2,038,136 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 84,378 | 108,947 | 2,038,136 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 84,378 | 108,947 | 2,038,136 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 84,378 | 108,940 | 2,038,136 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 84,378 | 108,947 | 2,038,136 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 1,974 | 1,572 | 2,491 | 4,672 | 3,644 | 13,369 | 47,201 | 45,417 | 55,169 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 1,974 | 1,572 | 2,491 | 4,672 | 3,644 | 13,369 | 47,201 | 45,417 | 55,169 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 1,974 | 1,572 | 2,491 | 4,671 | 3,644 | 13,371 | 47,201 | 45,417 | 55,171 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 1,974 | 1,572 | 2,506 | 4,668 | 3,644 | 13,368 | 47,201 | 45,417 | 55,168 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 1,974 | 1,572 | 2,490 | 4,671 | 3,644 | 13,371 | 47,201 | 45,417 | 55,171 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 1,974 | 1,572 | 2,506 | 4,671 | 3,644 | 13,368 | 47,199 | 45,417 | 55,171 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 1,974 | 1,572 | 2,506 | 4,671 | 3,644 | 13,371 | 47,201 | 45,417 | 55,169 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 1,974 | 1,572 | 2,507 | 4,671 | 3,644 | 13,371 | 47,201 | 45,417 | 55,168 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 1,974 | 1,572 | 2,491 | 4,671 | 3,644 | 13,371 | 47,201 | 45,417 | 55,171 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 1,974 | 1,572 | 2,491 | 4,671 | 3,644 | 13,371 | 47,200 | 45,417 | 55,171 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 1,974 | 1,572 | 2,491 | 4,670 | 3,644 | 13,371 | 47,199 | 45,417 | 55,171 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 1,974 | 1,572 | 2,507 | 4,672 | 3,644 | 13,371 | 47,201 | 45,417 | 55,171 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 1,974 | 1,572 | 2,491 | 4,672 | 3,644 | 13,371 | 47,198 | 45,417 | 55,171 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 1,974 | 1,572 | 2,491 | 4,671 | 3,644 | 13,369 | 47,201 | 45,417 | 55,167 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 1,974 | 1,572 | 2,507 | 4,671 | 3,644 | 13,368 | 47,201 | 45,417 | 55,171 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 1,974 | 1,572 | 2,491 | 4,672 | 3,644 | 13,367 | 47,201 | 45,417 | 55,168 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 1,974 | 1,572 | 2,491 | 4,672 | 3,644 | 13,372 | 47,199 | 45,417 | 55,171 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 1,974 | 1,572 | 2,491 | 4,671 | 3,644 | 13,371 | 47,204 | 45,417 | 55,168 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 1,974 | 1,572 | 2,491 | 4,671 | 3,644 | 13,378 | 47,202 | 45,417 | 55,171 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 1,974 | 1,572 | 2,507 | 4,671 | 3,644 | 13,371 | 47,201 | 45,417 | 55,168 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 1,974 | 1,572 | 2,491 | 4,672 | 3,644 | 13,370 | 47,202 | 45,417 | 55,169 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 1,974 | 1,572 | 2,491 | 4,672 | 3,644 | 13,370 | 47,200 | 45,417 | 55,167 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 1,974 | 1,572 | 2,491 | 4,670 | 3,644 | 13,369 | 47,199 | 45,415 | 55,167 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 1,974 | 1,572 | 2,491 | 4,671 | 3,644 | 13,372 | 47,201 | 45,417 | 55,168 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 1,974 | 1,572 | 2,490 | 4,672 | 3,644 | 13,370 | 47,202 | 45,417 | 55,171 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 1,974 | 1,572 | 2,506 | 4,672 | 3,644 | 13,369 | 47,201 | 45,417 | 55,171 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 1,974 | 1,572 | 2,542 | 4,672 | 3,644 | 13,376 | 47,202 | 45,417 | 55,168 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 1,974 | 1,572 | 2,538 | 4,671 | 3,644 | 13,370 | 47,202 | 45,417 | 55,169 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 1,974 | 1,572 | 2,490 | 4,671 | 3,644 | 13,368 | 47,202 | 45,417 | 55,167 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 1,974 | 1,572 | 2,526 | 4,672 | 3,644 | 13,370 | 47,202 | 45,417 | 55,171 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 1,974 | 1,572 | 2,507 | 4,676 | 3,646 | 13,377 | 47,213 | 45,423 | 55,175 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 1,974 | 1,572 | 2,507 | 4,673 | 3,646 | 13,377 | 47,213 | 45,423 | 55,175 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 1,974 | 1,572 | 2,491 | 4,673 | 3,646 | 13,377 | 47,213 | 45,423 | 55,174 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 1,950 | 1,548 | 2,519 | 4,693 | 3,672 | 13,466 | 47,549 | 45,790 | 55,386 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 1,950 | 1,548 | 2,519 | 4,695 | 3,672 | 13,466 | 47,553 | 45,790 | 55,386 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 1,950 | 1,548 | 2,503 | 4,696 | 3,672 | 13,471 | 47,584 | 45,790 | 55,385 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 1,950 | 1,548 | 2,519 | 4,695 | 3,672 | 13,467 | 47,587 | 45,790 | 55,386 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 1,950 | 1,548 | 2,502 | 4,696 | 3,672 | 13,468 | 47,581 | 45,790 | 55,386 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 1,950 | 1,548 | 2,502 | 4,696 | 3,672 | 13,469 | 47,577 | 45,790 | 55,386 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 1,950 | 1,548 | 2,502 | 4,696 | 3,672 | 13,468 | 47,581 | 45,790 | 55,385 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 1,950 | 1,548 | 2,534 | 4,696 | 3,672 | 13,465 | 47,581 | 45,790 | 55,386 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 1,950 | 1,548 | 2,502 | 4,693 | 3,672 | 13,469 | 47,577 | 45,790 | 55,383 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 1,950 | 1,548 | 2,518 | 4,695 | 3,672 | 13,470 | 47,580 | 45,790 | 55,383 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 1,950 | 1,548 | 2,502 | 4,696 | 3,672 | 13,470 | 47,579 | 45,790 | 55,383 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 1,950 | 1,548 | 2,502 | 4,695 | 3,672 | 13,467 | 47,580 | 45,790 | 55,383 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 1,950 | 1,548 | 2,502 | 4,696 | 3,672 | 13,470 | 47,580 | 45,790 | 55,379 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 1,950 | 1,548 | 2,502 | 4,695 | 3,672 | 13,472 | 47,580 | 45,790 | 55,383 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 1,950 | 1,548 | 2,502 | 4,692 | 3,672 | 13,470 | 47,578 | 45,790 | 55,380 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 1,950 | 1,548 | 2,502 | 4,695 | 3,672 | 13,470 | 47,580 | 45,790 | 55,379 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 1,950 | 1,548 | 2,502 | 4,696 | 3,672 | 13,470 | 47,577 | 45,790 | 55,380 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 1,950 | 1,548 | 2,518 | 4,696 | 3,672 | 13,469 | 47,580 | 45,790 | 55,380 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 1,950 | 1,548 | 2,502 | 4,692 | 3,672 | 13,471 | 47,580 | 45,790 | 55,383 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 1,950 | 1,548 | 2,502 | 4,695 | 3,672 | 13,472 | 47,580 | 45,790 | 55,383 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 1,950 | 1,548 | 2,502 | 4,692 | 3,672 | 13,466 | 47,577 | 45,790 | 55,380 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 1,950 | 1,548 | 2,502 | 4,695 | 3,672 | 13,467 | 47,577 | 45,790 | 55,383 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 1,950 | 1,548 | 2,518 | 4,695 | 3,672 | 13,476 | 47,580 | 45,790 | 55,380 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 1,950 | 1,548 | 2,518 | 4,695 | 3,672 | 13,468 | 47,580 | 45,790 | 55,383 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 1,950 | 1,548 | 2,502 | 4,696 | 3,672 | 13,472 | 47,577 | 45,790 | 55,383 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 1,950 | 1,548 | 2,503 | 4,695 | 3,672 | 13,471 | 47,553 | 45,790 | 55,384 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 1,950 | 1,548 | 2,503 | 4,694 | 3,672 | 13,475 | 47,553 | 45,790 | 55,382 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 1,950 | 1,548 | 2,503 | 4,695 | 3,672 | 13,470 | 47,555 | 45,790 | 55,377 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 1,950 | 1,548 | 2,503 | 4,695 | 3,672 | 13,470 | 47,553 | 45,790 | 55,384 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 5,631 | 25,017 | 5,701 | 9,503 | 49,601 | 8,051 | 120,973 | 714,180 | 113,519 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 5,631 | 25,017 | 5,701 | 9,501 | 49,601 | 8,051 | 120,969 | 714,180 | 113,520 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 5,631 | 25,017 | 5,701 | 9,499 | 49,601 | 8,051 | 120,971 | 714,180 | 113,522 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 5,631 | 25,017 | 5,716 | 9,498 | 49,601 | 8,051 | 120,968 | 714,180 | 113,521 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 5,631 | 25,017 | 5,700 | 9,502 | 49,601 | 8,051 | 120,973 | 714,180 | 113,522 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 5,631 | 25,017 | 5,716 | 9,501 | 49,601 | 8,049 | 120,969 | 714,180 | 113,522 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 5,631 | 25,017 | 5,716 | 9,502 | 49,601 | 8,051 | 120,970 | 714,180 | 113,522 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 5,631 | 25,017 | 5,717 | 9,499 | 49,601 | 8,052 | 120,971 | 714,180 | 113,521 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 5,631 | 25,017 | 5,701 | 9,500 | 49,601 | 8,048 | 120,970 | 714,180 | 113,519 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 5,631 | 25,017 | 5,701 | 9,501 | 49,601 | 8,051 | 120,971 | 714,181 | 113,522 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 5,631 | 25,017 | 5,701 | 9,499 | 49,601 | 8,052 | 120,972 | 714,180 | 113,521 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 5,631 | 25,017 | 5,717 | 9,500 | 49,601 | 8,051 | 120,973 | 714,180 | 113,520 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 5,631 | 25,017 | 5,701 | 9,500 | 49,599 | 8,052 | 120,973 | 714,180 | 113,518 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 5,631 | 25,017 | 5,701 | 9,501 | 49,601 | 8,051 | 120,973 | 714,180 | 113,522 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 5,631 | 25,017 | 5,717 | 9,501 | 49,601 | 8,051 | 120,969 | 714,180 | 113,522 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 5,631 | 25,017 | 5,701 | 9,500 | 49,598 | 8,051 | 120,973 | 714,180 | 113,519 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 5,631 | 25,017 | 5,701 | 9,503 | 49,601 | 8,051 | 120,973 | 714,180 | 113,518 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 5,631 | 25,017 | 5,701 | 9,499 | 49,601 | 8,050 | 120,976 | 714,180 | 113,519 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 5,631 | 25,017 | 5,701 | 9,502 | 49,601 | 8,051 | 120,972 | 714,180 | 113,522 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 5,631 | 25,017 | 5,717 | 9,498 | 49,601 | 8,051 | 120,970 | 714,180 | 113,519 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 5,631 | 25,017 | 5,701 | 9,503 | 49,601 | 8,051 | 120,970 | 714,180 | 113,519 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 5,631 | 25,017 | 5,701 | 9,500 | 49,599 | 8,051 | 120,973 | 714,180 | 113,522 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 5,631 | 25,017 | 5,701 | 9,503 | 49,600 | 8,051 | 120,964 | 714,182 | 113,516 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 5,631 | 25,017 | 5,701 | 9,500 | 49,599 | 8,051 | 120,969 | 714,180 | 113,515 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 5,631 | 25,017 | 5,700 | 9,499 | 49,601 | 8,050 | 120,973 | 714,180 | 113,521 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 5,631 | 25,017 | 5,716 | 9,503 | 49,601 | 8,050 | 120,973 | 714,180 | 113,519 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 5,631 | 25,017 | 5,752 | 9,503 | 49,601 | 8,049 | 120,965 | 714,180 | 113,517 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 5,631 | 25,017 | 5,748 | 9,500 | 49,601 | 8,050 | 120,972 | 714,180 | 113,522 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 5,631 | 25,017 | 5,700 | 9,498 | 49,601 | 8,048 | 120,973 | 714,180 | 113,522 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 5,631 | 25,017 | 5,736 | 9,501 | 49,601 | 8,049 | 120,971 | 714,183 | 113,521 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 5,631 | 25,017 | 5,717 | 9,501 | 49,599 | 8,050 | 121,075 | 714,279 | 113,524 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 5,631 | 25,017 | 5,717 | 9,503 | 49,598 | 8,052 | 121,075 | 714,276 | 113,525 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 5,631 | 25,017 | 5,701 | 9,503 | 49,601 | 8,050 | 121,072 | 714,276 | 113,528 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 5,022 | 24,618 | 5,729 | 9,738 | 50,033 | 8,068 | 123,779 | 720,147 | 113,737 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 5,022 | 24,618 | 5,729 | 9,739 | 50,033 | 8,070 | 123,794 | 720,148 | 113,737 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 5,022 | 24,618 | 5,713 | 9,738 | 50,033 | 8,071 | 123,812 | 720,148 | 113,736 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 5,022 | 24,618 | 5,729 | 9,738 | 50,033 | 8,071 | 123,814 | 720,148 | 113,737 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 5,022 | 24,618 | 5,712 | 9,738 | 50,033 | 8,069 | 123,812 | 720,147 | 113,737 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 5,022 | 24,618 | 5,712 | 9,740 | 50,033 | 8,071 | 123,812 | 720,148 | 113,737 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 5,022 | 24,618 | 5,712 | 9,740 | 50,033 | 8,071 | 123,807 | 720,147 | 113,734 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 5,022 | 24,618 | 5,744 | 9,741 | 50,030 | 8,071 | 123,812 | 720,147 | 113,737 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 5,022 | 24,618 | 5,712 | 9,728 | 50,029 | 8,070 | 123,809 | 720,149 | 113,726 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 5,022 | 24,618 | 5,728 | 9,739 | 50,029 | 8,071 | 123,803 | 720,148 | 113,729 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 5,022 | 24,618 | 5,712 | 9,729 | 50,033 | 8,071 | 123,810 | 720,148 | 113,729 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 5,022 | 24,618 | 5,712 | 9,739 | 50,033 | 8,071 | 123,810 | 720,148 | 113,734 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 5,022 | 24,618 | 5,712 | 9,736 | 50,031 | 8,071 | 123,809 | 720,149 | 113,731 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 5,022 | 24,618 | 5,712 | 9,737 | 50,033 | 8,071 | 123,812 | 720,148 | 113,734 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 5,022 | 24,618 | 5,712 | 9,722 | 50,033 | 8,071 | 123,808 | 720,148 | 113,731 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 5,022 | 24,618 | 5,712 | 9,738 | 50,033 | 8,066 | 123,812 | 720,151 | 113,733 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 5,022 | 24,618 | 5,712 | 9,735 | 50,033 | 8,071 | 123,810 | 720,148 | 113,732 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 5,022 | 24,618 | 5,728 | 9,727 | 50,033 | 8,071 | 123,809 | 720,150 | 113,731 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 5,022 | 24,618 | 5,712 | 9,732 | 50,032 | 8,071 | 123,808 | 720,148 | 113,731 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 5,022 | 24,618 | 5,712 | 9,742 | 50,033 | 8,069 | 123,810 | 720,148 | 113,733 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 5,022 | 24,618 | 5,712 | 9,738 | 50,033 | 8,069 | 123,812 | 720,148 | 113,734 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 5,022 | 24,618 | 5,712 | 9,738 | 50,032 | 8,067 | 123,810 | 720,150 | 113,733 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 5,022 | 24,618 | 5,728 | 9,728 | 50,033 | 8,067 | 123,810 | 720,148 | 113,734 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 5,022 | 24,618 | 5,728 | 9,727 | 50,033 | 8,067 | 123,808 | 720,148 | 113,734 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 5,022 | 24,618 | 5,712 | 9,732 | 50,032 | 8,071 | 123,810 | 720,150 | 113,733 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 5,022 | 24,618 | 5,713 | 9,739 | 50,033 | 8,071 | 123,785 | 720,148 | 113,735 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 5,022 | 24,618 | 5,713 | 9,738 | 50,033 | 8,068 | 123,791 | 720,148 | 113,731 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 5,022 | 24,618 | 5,713 | 9,739 | 50,033 | 8,070 | 123,789 | 720,148 | 113,728 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 5,022 | 24,618 | 5,713 | 9,738 | 50,033 | 8,067 | 123,788 | 720,148 | 113,729 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 64,131 | 400,101 | 57,040 | 127,474 | 787,200 | 74,605 | 1,301,576 | 11,413,287 | 1,046,550 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 64,131 | 400,101 | 57,040 | 127,452 | 787,223 | 74,603 | 1,301,576 | 11,413,290 | 1,046,484 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 64,131 | 400,101 | 57,040 | 127,383 | 787,263 | 74,605 | 1,301,511 | 11,413,287 | 1,046,550 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 64,131 | 400,101 | 57,055 | 127,302 | 787,259 | 74,571 | 1,301,576 | 11,413,288 | 1,046,549 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 64,131 | 400,101 | 57,039 | 127,472 | 787,263 | 74,603 | 1,301,499 | 11,413,287 | 1,046,540 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 64,131 | 400,101 | 57,055 | 127,406 | 787,260 | 74,605 | 1,301,528 | 11,413,287 | 1,046,548 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 64,131 | 400,101 | 57,055 | 127,408 | 787,241 | 74,603 | 1,301,576 | 11,413,287 | 1,046,548 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 64,131 | 400,101 | 57,056 | 127,372 | 787,200 | 74,603 | 1,301,579 | 11,413,327 | 1,046,550 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 64,131 | 400,101 | 57,040 | 127,472 | 787,263 | 74,603 | 1,301,576 | 11,413,248 | 1,046,550 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 64,131 | 400,101 | 57,040 | 127,468 | 787,187 | 74,605 | 1,301,505 | 11,413,287 | 1,046,532 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 64,131 | 400,101 | 57,040 | 127,412 | 787,260 | 74,605 | 1,301,576 | 11,413,350 | 1,046,550 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 64,131 | 400,101 | 57,056 | 127,416 | 787,263 | 74,605 | 1,301,576 | 11,413,287 | 1,046,549 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 64,131 | 400,101 | 57,040 | 127,427 | 787,216 | 74,603 | 1,301,515 | 11,413,366 | 1,046,481 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 64,131 | 400,101 | 57,040 | 127,472 | 787,263 | 74,605 | 1,301,575 | 11,413,287 | 1,046,488 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 64,131 | 400,101 | 57,056 | 127,440 | 787,189 | 74,605 | 1,301,511 | 11,413,287 | 1,046,550 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 64,131 | 400,101 | 57,040 | 127,397 | 787,203 | 74,605 | 1,301,517 | 11,413,352 | 1,046,482 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 64,131 | 400,101 | 57,040 | 127,431 | 787,263 | 74,605 | 1,301,530 | 11,413,287 | 1,046,549 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 64,131 | 400,101 | 57,040 | 127,412 | 787,250 | 74,603 | 1,301,658 | 11,413,288 | 1,046,552 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 64,131 | 400,101 | 57,040 | 127,431 | 787,250 | 74,605 | 1,301,661 | 11,413,287 | 1,046,532 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 64,131 | 400,101 | 57,056 | 127,416 | 787,248 | 74,605 | 1,301,530 | 11,413,287 | 1,046,484 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 64,131 | 400,101 | 57,040 | 127,470 | 787,248 | 74,560 | 1,301,578 | 11,413,339 | 1,046,549 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 64,131 | 400,101 | 57,040 | 127,437 | 787,211 | 74,566 | 1,301,581 | 11,413,283 | 1,046,550 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 64,131 | 400,101 | 57,040 | 127,380 | 787,196 | 74,492 | 1,301,512 | 11,413,284 | 1,046,550 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 64,131 | 400,101 | 57,040 | 127,470 | 787,198 | 74,563 | 1,301,532 | 11,413,283 | 1,046,548 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 64,131 | 400,101 | 57,039 | 126,999 | 787,169 | 74,564 | 1,301,581 | 11,413,283 | 1,046,548 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 64,131 | 400,101 | 57,055 | 127,109 | 787,246 | 74,474 | 1,301,581 | 11,413,283 | 1,046,511 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 64,131 | 400,101 | 57,091 | 126,882 | 787,152 | 74,561 | 1,301,492 | 11,413,283 | 1,046,482 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 64,131 | 400,101 | 57,087 | 126,921 | 787,222 | 74,563 | 1,301,579 | 11,413,283 | 1,046,548 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 64,131 | 400,101 | 57,039 | 126,833 | 787,250 | 74,565 | 1,301,581 | 11,413,283 | 1,046,549 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 64,131 | 400,101 | 57,075 | 127,267 | 787,247 | 74,509 | 1,301,557 | 11,413,399 | 1,046,481 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 64,131 | 400,101 | 57,056 | 127,476 | 787,189 | 74,565 | 1,303,121 | 11,414,822 | 1,046,554 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 64,131 | 400,101 | 57,056 | 127,253 | 787,183 | 74,571 | 1,303,121 | 11,414,822 | 1,046,554 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 64,131 | 400,101 | 57,040 | 127,088 | 787,175 | 74,575 | 1,303,121 | 11,414,822 | 1,046,555 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 54,162 | 393,702 | 57,068 | 131,853 | 792,887 | 74,745 | 1,343,139 | 11,508,828 | 1,046,701 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 54,162 | 393,702 | 57,068 | 131,968 | 792,887 | 74,718 | 1,342,998 | 11,508,772 | 1,046,751 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 54,162 | 393,702 | 57,052 | 131,529 | 792,884 | 74,708 | 1,343,878 | 11,508,772 | 1,046,765 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 54,162 | 393,702 | 57,068 | 131,735 | 792,786 | 74,717 | 1,343,840 | 11,508,772 | 1,046,764 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 54,162 | 393,702 | 57,051 | 131,968 | 792,888 | 74,743 | 1,343,775 | 11,508,772 | 1,046,764 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 54,162 | 393,702 | 57,051 | 131,866 | 792,798 | 74,713 | 1,343,780 | 11,508,770 | 1,046,765 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 54,162 | 393,702 | 57,051 | 131,874 | 792,887 | 74,743 | 1,343,732 | 11,508,770 | 1,046,765 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 54,162 | 393,702 | 57,083 | 131,966 | 792,861 | 74,743 | 1,343,719 | 11,508,770 | 1,046,762 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 54,162 | 393,702 | 57,051 | 131,869 | 792,825 | 74,692 | 1,343,775 | 11,508,770 | 1,046,727 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 54,162 | 393,702 | 57,067 | 131,952 | 792,829 | 74,743 | 1,343,779 | 11,508,770 | 1,046,725 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 54,162 | 393,702 | 57,051 | 131,907 | 792,833 | 74,743 | 1,343,677 | 11,508,670 | 1,046,725 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 54,162 | 393,702 | 57,051 | 131,801 | 792,840 | 74,743 | 1,343,779 | 11,508,770 | 1,046,725 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 54,162 | 393,702 | 57,051 | 131,885 | 792,808 | 74,743 | 1,343,750 | 11,508,770 | 1,046,728 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 54,162 | 393,702 | 57,051 | 131,978 | 792,882 | 74,705 | 1,343,739 | 11,508,770 | 1,046,722 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 54,162 | 393,702 | 57,051 | 131,945 | 792,883 | 74,743 | 1,343,779 | 11,508,770 | 1,046,658 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 54,162 | 393,702 | 57,051 | 131,996 | 792,832 | 74,743 | 1,343,668 | 11,508,770 | 1,046,720 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 54,162 | 393,702 | 57,051 | 131,977 | 792,866 | 74,743 | 1,343,730 | 11,508,841 | 1,046,688 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 54,162 | 393,702 | 57,067 | 132,057 | 792,882 | 74,743 | 1,343,778 | 11,508,677 | 1,046,726 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 54,162 | 393,702 | 57,051 | 131,999 | 792,829 | 74,743 | 1,343,698 | 11,508,770 | 1,046,723 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 54,162 | 393,702 | 57,051 | 131,957 | 792,880 | 74,743 | 1,343,714 | 11,508,770 | 1,046,721 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 54,162 | 393,702 | 57,051 | 131,938 | 792,830 | 74,743 | 1,343,691 | 11,508,713 | 1,046,728 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 54,162 | 393,702 | 57,051 | 131,987 | 792,876 | 74,693 | 1,343,778 | 11,508,770 | 1,046,727 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 54,162 | 393,702 | 57,067 | 132,065 | 792,882 | 74,743 | 1,343,779 | 11,508,869 | 1,046,727 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 54,162 | 393,702 | 57,067 | 131,888 | 792,880 | 74,743 | 1,343,779 | 11,508,864 | 1,046,723 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 54,162 | 393,702 | 57,051 | 131,978 | 792,882 | 74,743 | 1,343,732 | 11,508,770 | 1,046,722 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 54,162 | 393,702 | 57,052 | 131,923 | 792,883 | 74,743 | 1,343,136 | 11,508,770 | 1,046,762 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 54,162 | 393,702 | 57,052 | 131,872 | 792,883 | 74,743 | 1,343,098 | 11,508,772 | 1,046,762 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 54,162 | 393,702 | 57,052 | 131,880 | 792,808 | 74,743 | 1,343,196 | 11,508,770 | 1,046,749 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 54,162 | 393,702 | 57,052 | 131,909 | 792,883 | 74,743 | 1,343,098 | 11,508,770 | 1,046,762 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 4,188 | 4,976 | 6,106 | 16,360 | 41,498 | 70,402 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 4,188 | 4,976 | 6,106 | 16,358 | 41,497 | 70,404 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 4,188 | 4,976 | 6,106 | 16,360 | 41,493 | 70,403 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 4,188 | 4,991 | 6,101 | 16,353 | 41,498 | 70,403 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 4,188 | 4,975 | 6,106 | 16,360 | 41,495 | 70,406 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 4,187 | 4,991 | 6,104 | 16,356 | 41,499 | 70,403 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 4,187 | 4,991 | 6,106 | 16,358 | 41,495 | 70,406 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 4,187 | 4,992 | 6,106 | 16,355 | 41,499 | 70,406 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 4,187 | 4,996 | 6,106 | 16,360 | 41,496 | 70,404 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 4,187 | 4,976 | 6,106 | 16,358 | 41,498 | 70,403 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 4,187 | 4,992 | 6,105 | 16,356 | 41,498 | 70,402 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 4,187 | 4,992 | 6,106 | 16,356 | 41,493 | 70,402 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 4,187 | 4,976 | 6,106 | 16,353 | 41,497 | 70,401 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 4,187 | 5,008 | 6,106 | 16,356 | 41,497 | 70,403 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 4,187 | 4,992 | 6,106 | 16,354 | 41,498 | 70,406 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 4,187 | 4,976 | 6,106 | 16,353 | 41,497 | 70,402 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 4,187 | 4,976 | 6,106 | 16,359 | 41,500 | 70,406 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) (accepted) | 4,187 | 4,976 | 6,359 | 16,586 | 42,490 | 71,395 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 4,187 | 4,976 | 6,357 | 16,584 | 42,491 | 71,400 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 4,187 | 4,992 | 6,360 | 16,588 | 42,491 | 71,399 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 4,187 | 4,976 | 6,359 | 16,588 | 42,491 | 71,403 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 4,187 | 4,976 | 6,359 | 16,582 | 42,491 | 71,400 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 4,187 | 4,976 | 6,360 | 16,583 | 42,485 | 71,392 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 4,187 | 4,976 | 6,356 | 16,585 | 42,489 | 71,397 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 4,187 | 4,984 | 6,355 | 16,582 | 42,491 | 71,401 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 4,187 | 5,000 | 6,355 | 16,586 | 42,488 | 71,402 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 4,187 | 5,048 | 6,355 | 16,584 | 42,491 | 71,400 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 4,187 | 5,016 | 6,355 | 16,583 | 42,487 | 71,401 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 4,187 | 4,968 | 6,357 | 16,583 | 42,490 | 71,400 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 4,187 | 5,024 | 6,356 | 16,586 | 42,487 | 71,399 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 4,186 | 4,980 | 6,361 | 16,585 | 42,496 | 71,407 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 4,186 | 4,980 | 6,365 | 16,590 | 42,496 | 71,407 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 4,186 | 4,964 | 6,365 | 16,588 | 42,494 | 71,407 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 4,201 | 5,007 | 6,534 | 16,848 | 42,836 | 71,954 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 4,201 | 5,023 | 6,536 | 16,845 | 42,838 | 71,946 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 4,201 | 4,991 | 6,533 | 16,842 | 42,836 | 71,952 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 4,201 | 5,007 | 6,537 | 16,846 | 42,836 | 71,954 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 4,202 | 4,997 | 6,535 | 16,845 | 42,834 | 71,954 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 4,203 | 4,998 | 6,575 | 16,887 | 42,908 | 72,026 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 4,203 | 4,998 | 6,574 | 16,885 | 42,908 | 72,026 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 4,203 | 5,030 | 6,569 | 16,887 | 42,908 | 72,027 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 4,203 | 4,998 | 6,575 | 16,886 | 42,899 | 72,023 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 4,203 | 5,014 | 6,575 | 16,890 | 42,893 | 72,017 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 4,203 | 4,998 | 6,575 | 16,886 | 42,894 | 72,022 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 4,203 | 4,998 | 6,575 | 16,891 | 42,908 | 72,026 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 4,203 | 4,998 | 6,579 | 16,886 | 42,890 | 72,019 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 4,203 | 4,998 | 6,576 | 16,888 | 42,879 | 72,023 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 4,203 | 4,998 | 6,579 | 16,890 | 42,886 | 72,023 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 4,203 | 4,998 | 6,576 | 16,887 | 42,902 | 72,017 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 4,203 | 4,998 | 6,576 | 16,889 | 42,878 | 72,020 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 4,204 | 5,011 | 6,573 | 16,884 | 42,897 | 72,023 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 4,204 | 4,995 | 6,573 | 16,887 | 42,880 | 72,022 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 4,204 | 4,995 | 6,579 | 16,889 | 42,881 | 72,022 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 4,204 | 4,995 | 6,579 | 16,889 | 42,880 | 72,024 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 4,204 | 4,995 | 6,576 | 16,890 | 42,893 | 72,024 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 4,204 | 5,011 | 6,579 | 16,889 | 42,896 | 72,025 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 4,204 | 5,011 | 6,571 | 16,883 | 42,883 | 72,024 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 4,204 | 4,995 | 6,579 | 16,886 | 42,902 | 72,026 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 4,203 | 5,004 | 6,576 | 16,889 | 42,909 | 72,023 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 4,203 | 5,004 | 6,579 | 16,886 | 42,909 | 72,018 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 4,203 | 5,004 | 6,575 | 16,893 | 42,909 | 72,013 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 4,203 | 5,004 | 6,575 | 16,889 | 42,908 | 72,015 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 118,591 | 134,965 | 2,679,929 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 118,591 | 134,965 | 2,679,928 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 118,591 | 134,964 | 2,679,929 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 118,591 | 134,963 | 2,679,928 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 119,615 | 134,965 | 2,679,929 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 118,591 | 134,965 | 2,679,929 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 118,591 | 134,965 | 2,679,928 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 118,591 | 134,962 | 2,679,929 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 118,591 | 134,963 | 2,679,928 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 118,591 | 134,962 | 2,679,929 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 118,591 | 134,965 | 2,679,928 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 118,591 | 134,964 | 2,679,929 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 118,591 | 134,964 | 2,679,929 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 118,591 | 134,961 | 2,679,928 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 118,591 | 134,965 | 2,679,929 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 118,591 | 134,962 | 2,679,929 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 118,591 | 134,964 | 2,679,929 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 118,591 | 134,963 | 2,679,932 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 118,591 | 134,962 | 2,679,932 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 118,591 | 134,963 | 2,679,932 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 118,591 | 134,962 | 2,679,800 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 118,591 | 134,961 | 2,679,800 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 118,591 | 134,962 | 2,679,788 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 118,591 | 134,961 | 2,679,787 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 118,591 | 134,963 | 2,679,829 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 119,615 | 134,963 | 2,679,816 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 118,591 | 134,964 | 2,679,829 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 118,591 | 134,963 | 2,679,816 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 118,591 | 134,963 | 2,679,828 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 120,639 | 134,963 | 2,679,817 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 118,591 | 134,961 | 2,679,937 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 119,615 | 134,961 | 2,679,937 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 118,591 | 134,963 | 2,679,937 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 117,708 | 135,986 | 2,693,518 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 117,708 | 135,985 | 2,693,544 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 119,756 | 135,978 | 2,693,518 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 117,708 | 135,988 | 2,693,518 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 117,708 | 135,991 | 2,693,518 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 117,709 | 130,052 | 2,693,588 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 117,709 | 130,118 | 2,693,586 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 117,709 | 130,074 | 2,693,588 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 117,709 | 130,035 | 2,693,565 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 117,709 | 130,036 | 2,693,565 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 117,709 | 130,035 | 2,693,565 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 117,709 | 130,035 | 2,693,578 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 117,709 | 130,035 | 2,693,576 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 117,708 | 130,037 | 2,693,565 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 117,709 | 130,036 | 2,693,578 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 121,805 | 130,036 | 2,693,577 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 117,709 | 130,035 | 2,693,577 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 117,709 | 130,036 | 2,693,565 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 117,709 | 130,034 | 2,693,578 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 117,709 | 130,036 | 2,693,565 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 117,709 | 130,039 | 2,693,577 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 117,709 | 130,037 | 2,693,578 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 117,709 | 130,038 | 2,693,577 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 118,733 | 130,036 | 2,693,565 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 117,709 | 130,039 | 2,693,577 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 117,709 | 130,036 | 2,693,564 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 117,709 | 130,035 | 2,693,563 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 117,709 | 130,035 | 2,693,576 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 117,709 | 130,036 | 2,693,576 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 117,496 | 133,558 | 2,640,285 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 117,496 | 133,557 | 2,640,272 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 117,496 | 133,559 | 2,640,272 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 117,496 | 133,556 | 2,640,272 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 118,520 | 133,583 | 2,640,285 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 117,496 | 133,558 | 2,640,272 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 117,496 | 133,558 | 2,640,272 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 117,496 | 133,560 | 2,640,272 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 117,496 | 133,561 | 2,640,272 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 117,496 | 133,558 | 2,640,285 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 117,496 | 133,558 | 2,640,272 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 117,496 | 133,558 | 2,640,285 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 117,496 | 133,559 | 2,640,285 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 117,496 | 133,558 | 2,640,284 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 117,496 | 133,556 | 2,640,272 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 117,496 | 133,555 | 2,640,285 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 117,496 | 133,558 | 2,640,272 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 117,496 | 133,554 | 2,640,283 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 117,496 | 133,557 | 2,640,285 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 117,496 | 133,581 | 2,640,285 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 117,496 | 133,557 | 2,640,296 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 117,496 | 133,557 | 2,640,271 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 117,496 | 133,557 | 2,640,296 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 117,496 | 133,556 | 2,640,297 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 117,496 | 133,558 | 2,640,271 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 118,520 | 133,559 | 2,640,328 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 117,496 | 133,553 | 2,640,303 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 117,496 | 133,555 | 2,640,303 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 117,496 | 133,554 | 2,640,303 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 119,544 | 133,556 | 2,640,329 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 117,496 | 133,553 | 2,640,291 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 118,520 | 133,544 | 2,640,291 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 117,496 | 133,545 | 2,640,291 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 116,613 | 134,491 | 2,653,870 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 116,613 | 134,501 | 2,653,870 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 118,661 | 134,526 | 2,653,870 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 116,613 | 134,524 | 2,653,871 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 116,613 | 134,528 | 2,653,871 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 116,614 | 128,621 | 2,653,942 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 116,614 | 128,620 | 2,653,942 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 116,614 | 128,631 | 2,653,942 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 116,614 | 128,617 | 2,654,130 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 116,614 | 128,606 | 2,654,128 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 116,614 | 128,608 | 2,654,130 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 116,618 | 128,595 | 2,654,130 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 116,614 | 128,614 | 2,654,117 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 116,614 | 128,619 | 2,654,117 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 116,614 | 128,618 | 2,654,129 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 120,710 | 128,620 | 2,654,117 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 116,614 | 128,617 | 2,654,128 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 116,614 | 128,619 | 2,654,129 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 116,614 | 128,607 | 2,654,129 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 116,614 | 128,619 | 2,654,130 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 116,614 | 128,613 | 2,654,129 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 116,614 | 128,614 | 2,654,117 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 116,618 | 128,619 | 2,654,117 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 117,638 | 128,618 | 2,654,116 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 116,614 | 128,616 | 2,654,128 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 116,614 | 128,630 | 2,654,129 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 116,614 | 128,630 | 2,654,129 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 116,614 | 128,620 | 2,654,129 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 116,614 | 128,615 | 2,654,129 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 69,479 | 79,053 | 1,611,692 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 69,479 | 79,053 | 1,611,692 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 69,479 | 79,053 | 1,611,692 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 69,479 | 79,051 | 1,611,692 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 70,503 | 79,053 | 1,611,692 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 69,479 | 79,053 | 1,611,692 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 69,479 | 79,053 | 1,611,692 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 69,479 | 79,049 | 1,611,692 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 69,479 | 79,052 | 1,611,692 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 69,479 | 79,053 | 1,611,692 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 69,479 | 79,053 | 1,611,692 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 69,479 | 79,053 | 1,611,692 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 69,479 | 79,052 | 1,611,692 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 69,479 | 79,052 | 1,611,692 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 69,479 | 79,053 | 1,611,692 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 69,479 | 79,051 | 1,611,692 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 69,479 | 79,053 | 1,611,692 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 69,479 | 79,053 | 1,611,692 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 69,479 | 79,053 | 1,611,692 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 69,479 | 79,050 | 1,611,692 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 69,479 | 79,050 | 1,611,692 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 69,479 | 79,052 | 1,611,692 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 69,479 | 79,052 | 1,611,692 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 69,479 | 79,053 | 1,611,692 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 69,479 | 79,053 | 1,611,692 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 70,503 | 79,053 | 1,611,692 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 69,479 | 79,053 | 1,611,692 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 69,479 | 79,053 | 1,611,692 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 69,479 | 79,053 | 1,611,692 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 71,527 | 79,052 | 1,611,692 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 69,479 | 79,053 | 1,611,698 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 70,503 | 79,053 | 1,611,698 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 69,479 | 79,053 | 1,611,698 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 69,620 | 79,523 | 1,620,606 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 69,620 | 79,523 | 1,620,606 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 70,644 | 79,523 | 1,620,606 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 69,620 | 79,523 | 1,620,606 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 69,620 | 79,523 | 1,620,606 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 69,621 | 79,565 | 1,620,678 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 69,621 | 79,564 | 1,620,678 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 69,621 | 79,565 | 1,620,678 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 69,621 | 79,567 | 1,620,677 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 69,621 | 79,565 | 1,620,677 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 69,621 | 79,565 | 1,620,677 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 69,621 | 79,564 | 1,620,677 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 69,621 | 79,566 | 1,620,677 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 69,621 | 79,566 | 1,620,677 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 69,621 | 79,563 | 1,620,677 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 71,669 | 79,565 | 1,620,677 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 69,621 | 79,566 | 1,620,677 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 69,621 | 79,566 | 1,620,677 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 69,621 | 79,568 | 1,620,677 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 69,621 | 79,564 | 1,620,677 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 69,621 | 79,566 | 1,620,677 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 69,621 | 79,566 | 1,620,677 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 69,621 | 79,565 | 1,620,677 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 70,645 | 79,566 | 1,620,677 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 69,621 | 79,565 | 1,620,677 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 69,621 | 79,566 | 1,620,678 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 69,621 | 79,566 | 1,620,677 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 69,621 | 79,565 | 1,620,678 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 69,621 | 79,566 | 1,620,678 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 117,758 | 133,877 | 2,650,128 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 117,758 | 133,853 | 2,650,128 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 117,758 | 133,877 | 2,650,140 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 117,758 | 133,852 | 2,650,141 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 118,782 | 133,854 | 2,650,141 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 117,758 | 133,878 | 2,650,128 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 117,758 | 133,879 | 2,650,128 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 117,758 | 133,873 | 2,650,140 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 117,758 | 133,850 | 2,650,140 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 117,758 | 133,875 | 2,650,128 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 117,758 | 133,852 | 2,650,141 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 117,758 | 133,852 | 2,650,128 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 117,758 | 133,849 | 2,650,128 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 117,758 | 133,852 | 2,650,140 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 117,758 | 133,872 | 2,650,141 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 117,758 | 133,874 | 2,650,128 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 117,758 | 133,852 | 2,650,128 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 117,758 | 133,878 | 2,650,141 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 117,758 | 133,866 | 2,650,141 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 117,758 | 133,875 | 2,650,140 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 117,758 | 133,865 | 2,650,126 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 117,758 | 133,874 | 2,650,132 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 117,758 | 133,877 | 2,650,178 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 117,758 | 133,877 | 2,650,152 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 117,758 | 133,865 | 2,650,133 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 118,782 | 133,875 | 2,650,159 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 117,758 | 133,878 | 2,650,159 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 117,758 | 133,865 | 2,650,179 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 117,758 | 133,876 | 2,650,159 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 119,806 | 133,876 | 2,650,133 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 117,758 | 133,863 | 2,650,147 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 118,782 | 133,874 | 2,650,147 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 117,758 | 133,876 | 2,650,147 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 116,875 | 134,830 | 2,663,726 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 116,875 | 134,835 | 2,663,726 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 118,923 | 134,843 | 2,663,727 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 116,875 | 134,843 | 2,663,727 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 116,875 | 134,848 | 2,663,726 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 116,876 | 128,947 | 2,663,798 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 116,876 | 128,943 | 2,663,798 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 116,876 | 128,939 | 2,663,798 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 116,876 | 128,936 | 2,663,985 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 116,876 | 128,937 | 2,663,973 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 116,876 | 128,926 | 2,663,984 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 116,876 | 128,937 | 2,663,984 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 116,876 | 128,927 | 2,663,985 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 116,876 | 128,926 | 2,663,985 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 116,876 | 128,938 | 2,663,986 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 120,972 | 128,936 | 2,663,973 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 116,876 | 128,929 | 2,663,973 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 116,876 | 128,937 | 2,663,973 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 116,876 | 128,934 | 2,663,986 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 116,876 | 128,937 | 2,663,985 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 116,876 | 128,938 | 2,663,985 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 116,876 | 128,939 | 2,663,973 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 116,876 | 128,935 | 2,663,985 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 117,900 | 128,937 | 2,663,985 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 116,876 | 128,933 | 2,663,985 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 116,876 | 128,936 | 2,663,985 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 116,876 | 128,938 | 2,663,985 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 116,876 | 128,941 | 2,663,985 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 116,876 | 128,952 | 2,663,985 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 35,726 | 44,808 | 846,997 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 35,726 | 44,808 | 846,999 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 35,726 | 44,807 | 846,997 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 35,726 | 44,808 | 846,997 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 35,726 | 44,808 | 847,000 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 35,726 | 44,807 | 846,997 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 35,726 | 44,807 | 846,997 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 35,726 | 44,807 | 846,998 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 35,726 | 44,807 | 846,997 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 35,726 | 44,808 | 846,999 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 35,726 | 44,808 | 846,997 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 35,726 | 44,808 | 846,997 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 35,726 | 44,805 | 847,000 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 35,726 | 44,808 | 846,997 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 35,726 | 44,807 | 846,997 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 35,726 | 44,808 | 846,997 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 35,726 | 44,806 | 846,997 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 35,726 | 44,800 | 846,996 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 35,726 | 44,797 | 846,998 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 35,726 | 44,797 | 846,996 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 35,726 | 44,808 | 846,879 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 35,726 | 44,807 | 846,878 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 35,726 | 44,807 | 846,880 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 35,726 | 44,805 | 846,879 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 35,726 | 44,808 | 846,890 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 35,726 | 44,806 | 846,890 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 35,726 | 44,806 | 846,890 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 35,726 | 44,806 | 846,890 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 35,726 | 44,807 | 846,890 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 35,726 | 44,808 | 846,890 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 35,726 | 44,808 | 847,002 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 35,726 | 44,808 | 847,002 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 35,726 | 44,808 | 847,002 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 35,739 | 44,927 | 847,205 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 35,739 | 45,267 | 847,205 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 36,763 | 44,926 | 846,756 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 35,739 | 44,893 | 846,755 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 35,739 | 44,925 | 847,205 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 35,740 | 44,944 | 847,278 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 35,740 | 44,942 | 847,277 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 35,740 | 44,945 | 847,278 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 35,740 | 44,935 | 846,919 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 35,740 | 44,936 | 846,916 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 35,740 | 44,935 | 846,917 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 35,740 | 44,936 | 846,917 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 35,740 | 44,934 | 846,919 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 35,740 | 44,936 | 846,917 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 35,740 | 44,936 | 846,917 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 37,788 | 44,937 | 846,919 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 35,740 | 44,936 | 846,916 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 35,740 | 44,935 | 846,917 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 35,740 | 44,935 | 846,917 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 35,740 | 44,936 | 846,917 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 35,740 | 44,936 | 846,917 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 35,740 | 44,935 | 846,916 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 35,740 | 44,936 | 846,920 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 35,740 | 44,935 | 846,917 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 35,740 | 44,936 | 846,917 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 35,740 | 44,937 | 846,247 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 35,740 | 44,935 | 846,249 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 35,740 | 44,933 | 846,247 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 35,740 | 44,936 | 846,247 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 35,598 | 44,104 | 847,375 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 35,598 | 44,104 | 847,375 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 35,598 | 44,104 | 847,375 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 35,598 | 44,104 | 847,375 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 35,598 | 44,104 | 847,375 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 35,598 | 44,104 | 847,375 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 35,598 | 44,104 | 847,375 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 35,598 | 44,104 | 847,375 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 35,598 | 44,104 | 847,375 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 35,598 | 44,104 | 847,376 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 35,598 | 44,104 | 847,375 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 35,598 | 44,104 | 847,375 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 35,598 | 44,104 | 847,375 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 35,598 | 44,104 | 847,375 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 35,598 | 44,104 | 847,375 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 35,598 | 44,104 | 847,375 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 35,598 | 44,104 | 847,375 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 35,598 | 44,104 | 847,375 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 35,598 | 44,104 | 847,375 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 35,598 | 44,104 | 847,376 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 35,598 | 44,104 | 847,376 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 35,598 | 44,104 | 847,376 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 35,598 | 44,103 | 847,377 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 35,598 | 44,104 | 847,376 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 35,598 | 44,104 | 847,376 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 35,598 | 44,104 | 847,376 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 35,598 | 44,104 | 847,376 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 35,598 | 44,104 | 847,376 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 35,598 | 44,104 | 847,380 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 35,598 | 44,104 | 847,376 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 35,598 | 44,104 | 847,382 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 35,598 | 44,104 | 847,382 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 35,598 | 44,104 | 847,382 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 35,611 | 44,190 | 847,585 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 35,611 | 44,190 | 847,585 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 36,635 | 44,190 | 847,584 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 35,611 | 44,190 | 847,585 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 35,611 | 44,190 | 847,585 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 35,612 | 44,169 | 847,659 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 35,612 | 44,168 | 847,657 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 35,612 | 44,168 | 847,657 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 35,612 | 44,233 | 847,657 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 35,612 | 44,232 | 847,657 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 35,612 | 44,232 | 847,657 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 35,612 | 44,233 | 847,657 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 35,612 | 44,233 | 847,657 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 35,612 | 44,233 | 847,657 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 35,612 | 44,233 | 847,657 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 37,660 | 44,232 | 847,657 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 35,612 | 44,232 | 847,657 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 35,612 | 44,232 | 847,657 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 35,612 | 44,232 | 847,659 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 35,612 | 44,232 | 847,657 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 35,612 | 44,232 | 847,657 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 35,612 | 44,233 | 847,657 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 35,612 | 44,233 | 847,657 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 35,612 | 44,232 | 847,657 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 35,612 | 44,232 | 847,657 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 35,612 | 44,232 | 847,657 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 35,612 | 44,233 | 847,656 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 35,612 | 44,232 | 847,657 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 35,612 | 44,232 | 847,657 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 32,078 | 36,612 | 745,372 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 32,078 | 36,612 | 745,372 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 32,078 | 36,612 | 745,371 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 32,078 | 36,612 | 745,372 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 32,078 | 36,612 | 745,372 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 32,078 | 36,612 | 745,370 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 32,078 | 36,612 | 745,370 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 32,078 | 36,612 | 745,372 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 32,078 | 36,612 | 745,370 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 32,078 | 36,612 | 745,370 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 32,078 | 36,612 | 745,370 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 32,078 | 36,611 | 745,370 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 32,078 | 36,610 | 745,370 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 32,078 | 36,611 | 745,370 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 32,078 | 36,612 | 745,370 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 32,078 | 36,612 | 745,372 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 32,078 | 36,612 | 745,370 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 32,078 | 36,612 | 745,370 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 32,078 | 36,612 | 745,370 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 32,078 | 36,612 | 745,370 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 32,078 | 36,612 | 745,371 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 32,078 | 36,612 | 745,370 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 32,078 | 36,610 | 745,372 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 32,078 | 36,611 | 745,372 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 32,078 | 36,612 | 745,370 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 32,078 | 36,611 | 745,370 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 32,078 | 36,612 | 745,370 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 32,078 | 36,612 | 745,370 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 32,078 | 36,612 | 745,370 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 32,078 | 36,611 | 745,371 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 32,078 | 36,612 | 745,378 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 32,078 | 36,612 | 745,378 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 32,078 | 36,612 | 745,376 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 32,091 | 36,699 | 745,578 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 32,091 | 36,698 | 745,579 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 33,115 | 36,699 | 745,579 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 32,091 | 36,699 | 745,579 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 32,091 | 36,699 | 745,579 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 32,092 | 36,741 | 745,651 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 32,092 | 36,740 | 745,651 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 32,092 | 36,741 | 745,651 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 32,092 | 36,742 | 745,653 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 32,092 | 36,741 | 745,652 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 32,092 | 36,740 | 745,651 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 32,092 | 36,742 | 745,651 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 32,092 | 36,741 | 745,654 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 32,092 | 36,741 | 745,651 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 32,092 | 36,742 | 745,651 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 34,140 | 36,742 | 745,651 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 32,092 | 36,742 | 745,653 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 32,092 | 36,742 | 745,651 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 32,092 | 36,742 | 745,653 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 32,092 | 36,742 | 745,651 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 32,092 | 36,742 | 745,653 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 32,092 | 36,742 | 745,651 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 32,092 | 36,742 | 745,651 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 32,092 | 36,741 | 745,651 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 32,092 | 36,742 | 745,651 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 32,092 | 36,742 | 745,651 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 32,092 | 36,742 | 745,651 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 32,092 | 36,741 | 745,651 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 32,092 | 36,742 | 745,651 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 35,534 | 44,040 | 839,503 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 35,534 | 44,040 | 839,503 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 35,534 | 44,040 | 839,503 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 35,534 | 44,040 | 839,503 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 35,534 | 44,040 | 839,503 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 35,534 | 44,040 | 839,503 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 35,534 | 44,040 | 839,503 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 35,534 | 44,040 | 839,503 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 35,534 | 44,040 | 839,503 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 35,534 | 44,040 | 839,503 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 35,534 | 44,040 | 839,503 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 35,534 | 44,040 | 839,503 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 35,534 | 44,040 | 839,503 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 35,534 | 44,040 | 839,503 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 35,534 | 44,040 | 839,503 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 35,534 | 44,040 | 839,503 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 35,534 | 44,040 | 839,503 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 35,534 | 44,040 | 839,503 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 35,534 | 44,040 | 839,503 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 35,534 | 44,040 | 839,503 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 35,534 | 44,040 | 839,504 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 35,534 | 44,040 | 839,504 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 35,534 | 44,039 | 839,506 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 35,534 | 44,040 | 839,505 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 35,534 | 44,040 | 839,510 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 35,534 | 44,040 | 839,504 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 35,534 | 44,038 | 839,509 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 35,534 | 44,040 | 839,504 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 35,534 | 44,040 | 839,504 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 35,534 | 44,040 | 839,504 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 35,534 | 44,040 | 839,512 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 35,534 | 44,040 | 839,511 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 35,534 | 44,040 | 839,510 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 35,547 | 44,126 | 839,713 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 35,547 | 44,126 | 839,713 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 36,571 | 44,126 | 839,713 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 35,547 | 44,126 | 839,713 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 35,547 | 44,124 | 839,712 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 35,548 | 44,104 | 839,787 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 35,548 | 44,104 | 839,786 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 35,548 | 44,105 | 839,785 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 35,548 | 44,167 | 839,785 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 35,548 | 44,168 | 839,785 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 35,548 | 44,168 | 839,785 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 35,548 | 44,168 | 839,785 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 35,548 | 44,168 | 839,787 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 35,548 | 44,169 | 839,785 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 35,548 | 44,167 | 839,785 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 37,596 | 44,168 | 839,785 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 35,548 | 44,168 | 839,785 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 35,548 | 44,168 | 839,785 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 35,548 | 44,168 | 839,785 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 35,548 | 44,168 | 839,784 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 35,548 | 44,168 | 839,785 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 35,548 | 44,168 | 839,785 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 35,548 | 44,168 | 839,786 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 35,548 | 44,168 | 839,787 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 35,548 | 44,169 | 839,784 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 35,548 | 44,168 | 839,785 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 35,548 | 44,168 | 839,785 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 35,548 | 44,166 | 839,785 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 35,548 | 44,169 | 839,785 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 1,089,283 | 38,805 | 82,505 | 262,483 | 151,715 | 3,350,266 | 2,502,844 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 1,087,683 | 38,805 | 82,505 | 262,463 | 151,710 | 3,350,232 | 2,502,805 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 1,087,682 | 38,805 | 82,505 | 263,098 | 151,681 | 3,350,208 | 2,502,803 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 1,087,690 | 38,805 | 82,505 | 262,472 | 151,719 | 3,350,264 | 2,502,780 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 1,087,690 | 38,805 | 82,505 | 262,607 | 151,714 | 3,350,250 | 2,502,787 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 1,087,683 | 38,805 | 82,505 | 262,521 | 151,660 | 3,350,225 | 2,502,762 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 1,087,690 | 38,805 | 82,505 | 262,591 | 151,714 | 3,350,265 | 2,502,811 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 1,089,290 | 38,805 | 82,505 | 262,578 | 151,678 | 3,350,137 | 2,502,783 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 1,087,690 | 38,805 | 82,505 | 262,499 | 151,702 | 3,350,265 | 2,502,743 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 1,087,683 | 38,805 | 82,505 | 262,550 | 151,711 | 3,350,235 | 2,502,805 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 1,090,890 | 38,805 | 82,505 | 262,551 | 151,708 | 3,350,205 | 2,502,796 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 1,087,682 | 38,805 | 82,505 | 262,890 | 151,710 | 3,350,266 | 2,502,844 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 1,087,683 | 38,805 | 82,505 | 262,777 | 151,662 | 3,350,265 | 2,502,720 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 1,087,690 | 38,805 | 82,505 | 262,564 | 151,702 | 3,350,265 | 2,502,828 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 1,087,690 | 38,805 | 82,505 | 262,537 | 151,712 | 3,350,266 | 2,502,845 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 1,087,690 | 38,805 | 82,505 | 262,876 | 151,656 | 3,350,234 | 2,502,817 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 1,087,690 | 38,805 | 82,505 | 262,575 | 151,734 | 3,350,265 | 2,502,848 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 1,089,282 | 38,805 | 82,505 | 262,622 | 151,671 | 3,350,263 | 2,502,795 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 1,087,690 | 38,805 | 82,505 | 262,290 | 151,723 | 3,350,151 | 2,502,737 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 1,087,690 | 38,805 | 82,505 | 262,726 | 151,682 | 3,350,255 | 2,502,807 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 1,087,690 | 38,805 | 82,505 | 262,845 | 151,672 | 3,350,287 | 2,502,840 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 1,087,690 | 38,805 | 82,505 | 262,748 | 151,696 | 3,350,391 | 2,502,797 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 1,087,690 | 38,805 | 82,505 | 262,602 | 151,640 | 3,350,442 | 2,502,786 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 1,092,490 | 38,805 | 82,505 | 262,880 | 151,717 | 3,350,212 | 2,502,797 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 1,089,283 | 38,805 | 82,505 | 262,645 | 151,697 | 3,350,261 | 2,502,803 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 1,087,683 | 38,805 | 84,105 | 262,751 | 151,736 | 3,350,261 | 2,502,830 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 1,087,690 | 38,805 | 82,505 | 262,806 | 151,732 | 3,350,288 | 2,502,722 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 1,089,283 | 38,805 | 82,505 | 262,846 | 151,638 | 3,350,261 | 2,502,817 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 1,087,683 | 38,805 | 82,505 | 262,598 | 151,683 | 3,350,262 | 2,502,821 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 1,087,690 | 38,805 | 82,505 | 262,776 | 151,716 | 3,350,319 | 2,502,807 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 1,089,383 | 38,805 | 82,505 | 262,570 | 151,637 | 3,350,298 | 2,503,355 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 1,087,783 | 38,805 | 84,105 | 262,527 | 151,689 | 3,350,208 | 2,503,399 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 1,087,783 | 38,805 | 82,505 | 262,733 | 151,663 | 3,350,260 | 2,503,403 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 1,087,782 | 38,805 | 84,005 | 262,555 | 154,803 | 3,350,234 | 2,537,401 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 1,087,790 | 38,805 | 84,005 | 262,891 | 154,801 | 3,350,261 | 2,537,448 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 1,087,790 | 38,805 | 84,005 | 263,174 | 154,754 | 3,350,232 | 2,537,374 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 1,087,783 | 38,805 | 84,005 | 262,547 | 154,752 | 3,350,244 | 2,537,434 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 1,087,783 | 38,805 | 84,005 | 262,982 | 154,811 | 3,350,262 | 2,537,435 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 1,087,790 | 38,805 | 84,105 | 263,049 | 145,048 | 3,350,263 | 2,544,622 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 1,089,390 | 38,805 | 84,105 | 262,771 | 145,114 | 3,350,099 | 2,544,568 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 1,097,383 | 38,805 | 84,105 | 262,817 | 145,074 | 3,350,263 | 2,544,663 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 1,087,790 | 38,805 | 84,105 | 262,935 | 145,780 | 3,351,330 | 2,545,076 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 1,087,790 | 38,805 | 84,105 | 263,233 | 145,794 | 3,351,281 | 2,545,019 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 1,100,143 | 38,805 | 84,105 | 263,233 | 145,794 | 3,351,282 | 2,545,002 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 1,087,783 | 38,805 | 84,105 | 263,143 | 145,755 | 3,351,295 | 2,545,050 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 1,087,790 | 38,805 | 84,105 | 262,996 | 145,792 | 3,351,258 | 2,544,975 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 1,087,783 | 38,805 | 84,105 | 262,678 | 145,832 | 3,351,295 | 2,545,099 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 1,087,790 | 38,805 | 84,105 | 262,961 | 145,790 | 3,351,232 | 2,544,986 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 1,087,790 | 38,805 | 84,105 | 262,937 | 145,803 | 3,351,235 | 2,544,974 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 1,087,790 | 38,805 | 84,105 | 262,933 | 145,755 | 3,351,259 | 2,545,002 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 1,087,790 | 38,805 | 84,105 | 263,281 | 145,824 | 3,351,329 | 2,545,028 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 1,095,790 | 38,805 | 84,105 | 263,233 | 145,789 | 3,351,283 | 2,545,097 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 1,087,783 | 38,805 | 84,105 | 263,233 | 145,781 | 3,351,254 | 2,545,044 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 1,089,390 | 38,805 | 84,105 | 263,027 | 145,782 | 3,351,346 | 2,545,037 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 1,087,783 | 38,805 | 84,105 | 263,037 | 145,791 | 3,351,344 | 2,545,096 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 1,087,783 | 38,805 | 84,105 | 263,233 | 145,809 | 3,351,293 | 2,545,016 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 1,090,983 | 38,805 | 84,105 | 263,278 | 145,816 | 3,351,241 | 2,544,998 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 1,092,590 | 38,805 | 84,105 | 262,639 | 145,806 | 3,351,260 | 2,545,014 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 1,089,390 | 38,805 | 84,105 | 263,443 | 145,791 | 3,351,291 | 2,545,061 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 1,087,783 | 38,805 | 84,105 | 263,151 | 145,766 | 3,351,277 | 2,545,064 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 1,087,790 | 38,805 | 84,105 | 263,124 | 145,802 | 3,351,254 | 2,545,102 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 1,087,790 | 38,805 | 84,105 | 263,191 | 145,803 | 3,351,301 | 2,545,062 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 93,005 | 165,259 | 2,830,433 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 93,005 | 165,219 | 2,830,335 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 93,005 | 165,230 | 2,830,354 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 93,005 | 165,206 | 2,830,335 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 93,005 | 165,209 | 2,830,434 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 93,005 | 165,214 | 2,830,397 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 93,005 | 165,239 | 2,830,387 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 93,005 | 165,198 | 2,830,367 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 93,005 | 165,257 | 2,830,436 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 93,005 | 165,226 | 2,830,425 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 93,005 | 165,239 | 2,830,408 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 93,005 | 165,198 | 2,830,418 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 93,005 | 165,175 | 2,830,409 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 93,005 | 165,221 | 2,830,436 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 93,005 | 165,202 | 2,830,408 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 93,005 | 165,212 | 2,830,406 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 93,005 | 165,181 | 2,830,412 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 93,005 | 165,257 | 2,830,424 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 93,005 | 165,221 | 2,830,434 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 93,005 | 165,192 | 2,830,302 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 93,005 | 165,259 | 2,830,351 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 93,005 | 165,242 | 2,830,394 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 93,005 | 165,220 | 2,830,339 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 93,005 | 165,204 | 2,830,360 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 93,005 | 165,220 | 2,830,387 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 93,005 | 165,240 | 2,830,389 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 93,005 | 165,229 | 2,830,302 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 96,205 | 165,182 | 2,830,411 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 93,005 | 165,259 | 2,830,363 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 93,005 | 165,222 | 2,830,329 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 93,005 | 165,190 | 2,830,960 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 93,005 | 165,214 | 2,830,982 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 93,005 | 165,160 | 2,831,030 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 94,505 | 168,124 | 2,864,949 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 94,505 | 168,110 | 2,865,002 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 94,505 | 168,159 | 2,865,037 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 94,505 | 168,078 | 2,865,019 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 94,505 | 168,117 | 2,864,997 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 94,605 | 168,803 | 2,872,175 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 94,605 | 168,822 | 2,872,196 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 94,605 | 168,816 | 2,872,180 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 94,605 | 168,996 | 2,872,398 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 94,605 | 169,007 | 2,872,497 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 94,605 | 169,007 | 2,872,408 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 94,605 | 169,008 | 2,872,412 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 94,605 | 169,007 | 2,872,459 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 94,605 | 169,011 | 2,872,476 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 94,605 | 169,047 | 2,872,389 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 94,605 | 169,014 | 2,872,465 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 94,605 | 169,000 | 2,872,329 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 94,605 | 169,008 | 2,872,347 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 94,605 | 169,013 | 2,872,455 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 94,605 | 169,034 | 2,872,417 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 94,605 | 169,017 | 2,872,412 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 94,605 | 168,991 | 2,872,339 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 94,605 | 169,004 | 2,872,431 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 94,605 | 169,025 | 2,872,416 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 94,605 | 169,019 | 2,872,336 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 94,605 | 169,024 | 2,872,458 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 94,605 | 169,040 | 2,872,435 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 94,605 | 168,991 | 2,872,514 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 94,605 | 169,016 | 2,872,439 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 170,300 | 163,392 | 295,948 | 291,398 | 5,349,284 | 5,295,444 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 170,300 | 163,392 | 295,901 | 291,347 | 5,349,287 | 5,295,440 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 170,300 | 163,392 | 295,967 | 291,425 | 5,349,286 | 5,295,422 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 170,300 | 163,392 | 295,902 | 291,347 | 5,349,235 | 5,295,442 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 170,300 | 163,427 | 295,967 | 291,364 | 5,349,259 | 5,295,445 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 170,300 | 163,392 | 295,899 | 291,430 | 5,349,272 | 5,295,444 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 170,300 | 163,392 | 295,967 | 291,402 | 5,349,270 | 5,295,440 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 170,300 | 163,392 | 295,909 | 291,430 | 5,349,284 | 5,295,471 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 170,300 | 163,392 | 295,966 | 291,392 | 5,349,272 | 5,295,419 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 170,300 | 163,392 | 295,929 | 291,375 | 5,349,238 | 5,295,469 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 170,300 | 163,392 | 295,935 | 291,405 | 5,349,266 | 5,295,444 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 170,300 | 163,392 | 295,944 | 291,412 | 5,349,290 | 5,295,423 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 170,300 | 163,392 | 295,914 | 291,384 | 5,349,252 | 5,295,459 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 170,300 | 163,392 | 295,929 | 291,359 | 5,349,306 | 5,295,404 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 170,300 | 163,392 | 295,960 | 291,401 | 5,349,263 | 5,295,443 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 170,300 | 163,392 | 295,918 | 291,433 | 5,349,208 | 5,295,435 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 170,300 | 163,392 | 295,967 | 291,427 | 5,349,179 | 5,295,442 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 170,300 | 163,392 | 295,940 | 291,403 | 5,349,286 | 5,295,444 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 170,300 | 163,392 | 295,957 | 291,430 | 5,349,217 | 5,295,441 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 170,300 | 163,392 | 295,967 | 291,428 | 5,349,196 | 5,295,422 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 170,300 | 163,392 | 295,967 | 291,361 | 5,349,236 | 5,295,424 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 170,300 | 163,392 | 295,967 | 291,360 | 5,349,282 | 5,295,421 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 170,300 | 163,392 | 295,894 | 291,316 | 5,349,236 | 5,295,387 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 170,300 | 163,408 | 295,935 | 291,360 | 5,349,303 | 5,295,429 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 170,300 | 163,392 | 295,946 | 291,346 | 5,349,271 | 5,295,442 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 170,300 | 163,408 | 295,967 | 291,327 | 5,349,324 | 5,295,441 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 170,300 | 167,392 | 295,933 | 291,314 | 5,349,277 | 5,295,444 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 170,300 | 163,392 | 295,875 | 291,326 | 5,349,323 | 5,295,442 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 170,300 | 163,392 | 295,912 | 291,350 | 5,349,238 | 5,295,440 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 170,300 | 163,392 | 295,900 | 291,354 | 5,349,270 | 5,295,443 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 170,300 | 163,392 | 295,966 | 291,420 | 5,349,998 | 5,296,201 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 170,300 | 163,392 | 295,929 | 291,420 | 5,350,004 | 5,296,175 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 170,300 | 163,392 | 295,948 | 291,420 | 5,350,027 | 5,296,200 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 172,552 | 165,024 | 307,355 | 294,796 | 5,409,038 | 5,322,075 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 172,552 | 165,024 | 307,332 | 294,826 | 5,409,039 | 5,322,112 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 172,552 | 165,024 | 307,318 | 294,766 | 5,409,014 | 5,322,184 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 172,552 | 165,024 | 307,355 | 294,805 | 5,409,039 | 5,322,182 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 172,552 | 165,024 | 307,327 | 294,791 | 5,409,041 | 5,322,182 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 172,679 | 165,150 | 308,457 | 296,713 | 5,418,080 | 5,331,169 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 172,679 | 165,150 | 308,486 | 296,695 | 5,418,158 | 5,331,170 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 172,679 | 165,150 | 308,486 | 296,692 | 5,418,068 | 5,331,166 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 172,679 | 165,150 | 308,826 | 296,924 | 5,421,725 | 5,334,888 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 172,679 | 165,150 | 308,863 | 296,954 | 5,421,825 | 5,334,914 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 172,679 | 165,150 | 308,819 | 296,963 | 5,421,846 | 5,334,878 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 172,679 | 165,150 | 308,857 | 297,017 | 5,421,862 | 5,334,888 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 172,679 | 165,150 | 308,823 | 296,961 | 5,421,805 | 5,334,914 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 172,679 | 165,150 | 308,828 | 296,936 | 5,421,861 | 5,334,917 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 172,679 | 165,150 | 308,840 | 296,990 | 5,421,850 | 5,334,940 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 172,679 | 165,150 | 308,803 | 296,954 | 5,421,774 | 5,334,955 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 172,679 | 165,150 | 308,831 | 296,967 | 5,421,863 | 5,334,925 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 172,679 | 165,150 | 308,823 | 296,990 | 5,421,830 | 5,334,915 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 172,679 | 165,150 | 308,809 | 296,973 | 5,421,902 | 5,334,925 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 172,679 | 165,150 | 308,807 | 296,917 | 5,421,861 | 5,334,934 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 172,679 | 165,150 | 308,831 | 296,998 | 5,421,904 | 5,334,884 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 172,679 | 165,150 | 308,847 | 296,985 | 5,421,905 | 5,334,896 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 172,679 | 165,150 | 308,825 | 296,926 | 5,421,808 | 5,334,908 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 172,679 | 165,150 | 308,861 | 296,995 | 5,421,757 | 5,334,903 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 172,679 | 165,150 | 308,861 | 296,979 | 5,421,822 | 5,334,914 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 172,679 | 165,150 | 308,861 | 296,991 | 5,421,823 | 5,334,919 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 172,679 | 165,183 | 308,861 | 296,988 | 5,421,818 | 5,334,889 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 172,679 | 165,150 | 308,850 | 296,945 | 5,421,848 | 5,334,879 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 172,679 | 165,150 | 308,853 | 296,990 | 5,421,851 | 5,334,904 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 100,445 | 78,815 | 264,843 | 152,965 | 2,611,046 | 2,360,873 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 100,445 | 78,815 | 264,817 | 152,938 | 2,611,041 | 2,360,865 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 100,445 | 78,815 | 264,846 | 152,929 | 2,611,056 | 2,360,860 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 100,445 | 78,815 | 264,855 | 152,961 | 2,611,043 | 2,360,848 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 100,445 | 78,815 | 264,796 | 152,948 | 2,611,019 | 2,360,873 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 100,445 | 78,815 | 264,844 | 152,958 | 2,611,054 | 2,360,876 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 100,445 | 78,815 | 264,858 | 152,942 | 2,611,061 | 2,360,865 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 100,445 | 78,815 | 264,878 | 152,933 | 2,610,913 | 2,360,885 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 100,445 | 78,815 | 264,842 | 152,924 | 2,611,026 | 2,360,755 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 100,445 | 78,815 | 264,828 | 152,951 | 2,610,976 | 2,360,875 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 100,445 | 78,815 | 264,846 | 152,965 | 2,610,961 | 2,360,869 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 100,445 | 78,815 | 264,844 | 152,942 | 2,611,066 | 2,360,869 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 100,445 | 78,815 | 264,828 | 152,926 | 2,611,015 | 2,360,866 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 100,445 | 78,815 | 264,858 | 152,941 | 2,610,945 | 2,360,863 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 100,445 | 78,815 | 264,849 | 152,963 | 2,611,016 | 2,360,859 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 100,445 | 78,815 | 264,866 | 152,944 | 2,611,050 | 2,360,865 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 100,445 | 78,815 | 264,829 | 152,965 | 2,611,039 | 2,360,842 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 100,445 | 78,815 | 264,846 | 152,958 | 2,611,063 | 2,360,865 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 100,445 | 78,815 | 264,906 | 152,965 | 2,610,988 | 2,360,853 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 100,445 | 78,815 | 264,855 | 152,927 | 2,610,951 | 2,360,835 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 100,445 | 78,815 | 264,848 | 152,963 | 2,611,029 | 2,360,863 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 100,445 | 78,815 | 264,818 | 152,949 | 2,611,023 | 2,360,832 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 100,445 | 78,815 | 264,823 | 152,943 | 2,610,963 | 2,360,836 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 100,445 | 78,815 | 264,865 | 152,942 | 2,610,999 | 2,360,836 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 100,445 | 78,815 | 264,880 | 152,987 | 2,611,020 | 2,360,853 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 100,445 | 78,815 | 264,834 | 152,958 | 2,611,050 | 2,360,857 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 100,445 | 78,815 | 264,836 | 152,959 | 2,611,010 | 2,360,821 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 100,445 | 82,015 | 264,825 | 152,938 | 2,611,040 | 2,360,854 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 100,445 | 78,815 | 264,837 | 152,966 | 2,610,994 | 2,360,869 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 100,445 | 78,815 | 264,854 | 152,903 | 2,611,049 | 2,360,808 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 100,445 | 78,815 | 264,831 | 153,224 | 2,611,513 | 2,361,485 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 100,445 | 78,815 | 264,826 | 153,149 | 2,611,525 | 2,361,446 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 100,445 | 78,815 | 264,826 | 153,190 | 2,611,645 | 2,361,512 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 101,945 | 80,135 | 267,829 | 155,459 | 2,645,618 | 2,383,235 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 101,945 | 80,135 | 267,845 | 155,466 | 2,645,665 | 2,383,054 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 101,945 | 80,135 | 267,865 | 155,462 | 2,645,538 | 2,383,203 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 101,945 | 80,135 | 267,829 | 155,440 | 2,645,604 | 2,383,239 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 101,945 | 80,135 | 267,824 | 155,449 | 2,645,657 | 2,383,239 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 102,045 | 80,235 | 268,512 | 156,080 | 2,652,728 | 2,390,414 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 102,045 | 80,235 | 268,554 | 156,070 | 2,652,710 | 2,390,298 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 102,045 | 80,235 | 268,517 | 156,058 | 2,652,726 | 2,390,441 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 102,045 | 80,235 | 269,360 | 156,096 | 2,659,229 | 2,391,285 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 102,045 | 80,235 | 269,323 | 156,079 | 2,659,266 | 2,391,182 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 102,045 | 80,235 | 269,327 | 156,116 | 2,659,214 | 2,391,279 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 102,045 | 80,235 | 269,336 | 156,131 | 2,659,237 | 2,391,279 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 102,045 | 80,235 | 269,312 | 156,109 | 2,659,267 | 2,391,279 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 102,045 | 80,235 | 269,341 | 156,138 | 2,659,262 | 2,391,299 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 102,045 | 80,235 | 269,341 | 156,108 | 2,659,268 | 2,391,319 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 102,045 | 80,235 | 269,318 | 156,102 | 2,659,169 | 2,391,261 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 102,045 | 80,235 | 269,330 | 156,122 | 2,659,181 | 2,391,286 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 102,045 | 80,235 | 269,331 | 156,100 | 2,659,120 | 2,391,237 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 102,045 | 80,235 | 269,355 | 156,087 | 2,659,278 | 2,391,271 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 102,045 | 80,235 | 269,334 | 156,147 | 2,659,262 | 2,391,339 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 102,045 | 80,235 | 269,318 | 156,117 | 2,659,246 | 2,391,312 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 102,045 | 80,235 | 269,331 | 156,133 | 2,659,213 | 2,391,299 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 102,045 | 80,235 | 269,342 | 156,125 | 2,659,280 | 2,391,318 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 102,045 | 80,235 | 269,349 | 156,123 | 2,659,303 | 2,391,259 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 102,045 | 80,235 | 269,369 | 156,135 | 2,659,245 | 2,391,271 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 102,045 | 80,235 | 269,347 | 156,124 | 2,659,298 | 2,391,298 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 102,045 | 80,235 | 269,322 | 156,113 | 2,659,301 | 2,391,322 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 102,045 | 80,235 | 269,332 | 156,109 | 2,659,251 | 2,391,290 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 102,045 | 80,235 | 269,299 | 156,102 | 2,659,261 | 2,391,261 |

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
| `9e57438` | 2026-10-20 | docs: view layer and server documentation (#132) | 107,569 | 119,469 | 265,072 | 279,903 | 2,595,717 | 2,920,193 |
| `2833eca` | 2026-10-20 | ci: run the DOM generator type, generator and docs-sample checks (#134) | 107,569 | 119,471 | 265,066 | 279,870 | 2,595,602 | 2,920,213 |
| `970998f` | 2026-10-20 | feat(anime): typed anime.js 4.5.0 bindings (#135) | 107,569 | 119,469 | 265,058 | 279,881 | 2,595,669 | 2,920,175 |
| `b3f398e` | 2026-10-20 | feat(view): motion, exit and reflow hooks with held departures (#136) | 107,569 | 119,469 | 265,079 | 279,859 | 2,595,627 | 2,920,219 |
| `0a7fe32` | 2026-10-21 | fix(view): arm and list sources reading a pending value wait with their boundary (#137) | 107,569 | 119,469 | 265,105 | 279,902 | 2,595,684 | 2,920,181 |
| `2cf84be` | 2026-10-21 | fix(core): match on flight policy cases so Fable precompile emits the assembly (#138) | 107,569 | 119,469 | 265,043 | 279,864 | 2,595,683 | 2,920,202 |
| `bcdb561` | 2026-10-21 | feat(server): render a whole document around a component (#139) | 107,569 | 119,470 | 265,084 | 279,878 | 2,595,640 | 2,920,188 |
| `5afc604` | 2026-10-21 | feat(view): Motion parts over Anime.js, and client element refusals (#140) | 107,570 | 119,469 | 265,050 | 279,830 | 2,595,715 | 2,920,113 |
| `0ba119a` | 2026-10-21 | feat(examples): Ranvier.Motion example pages over Motion parts (#141) | 107,571 | 119,470 | 265,092 | 279,869 | 2,595,634 | 2,920,180 |
| `d99fc64` | 2026-10-21 | docs(view): motion parts, motion testing and anime migration (#142) | 107,569 | 119,469 | 265,091 | 279,896 | 2,595,679 | 2,920,197 |
| `cbfc45f` | 2026-10-21 | build: bump the core packages to the next preview (#143) | 107,569 | 119,471 | 265,102 | 279,887 | 2,595,699 | 2,920,161 |
| `9476dac` | 2026-10-21 | feat(view): motion slots, flip start hints and follow's shared unit (#144) | 107,569 | 119,471 | 265,049 | 279,897 | 2,595,677 | 2,920,191 |
| `141ac28` | 2026-10-21 | docs: publish the Motion example pages on the docs site under examples/motion/ (#146) | 107,569 | 119,469 | 265,039 | 279,894 | 2,595,660 | 2,920,177 |
| `a0aa9c4` | 2026-10-21 | build(deps): bump the npm\_and\_yarn group across 3 directories with 3 updates (#145) | 107,569 | 119,470 | 265,070 | 279,895 | 2,595,625 | 2,920,163 |
| `40ea738` | 2026-10-21 | build: ship embedded PDBs instead of empty symbol packages (#148) | 107,569 | 119,469 | 265,103 | 279,880 | 2,595,722 | 2,920,176 |
| `14baa51` | 2026-10-21 | ci: group Dependabot updates and auto-merge them on green (#149) | 107,569 | 119,470 | 265,095 | 279,908 | 2,595,573 | 2,920,217 |
| `9a8946d` | 2026-10-21 | build(view): drop Foundation's Portable internals and the FSharp.Core pin (#147) | 107,570 | 119,469 | 265,101 | 279,915 | 2,595,703 | 2,920,165 |
| `1062c1f` | 2026-10-21 | build: compile with Fickl instead of Fable (#152) | 107,569 | 119,469 | 265,111 | 279,912 | 2,595,672 | 2,920,215 |
| `fb1a85b` | 2026-10-21 | feat(view): Native.fs, the .NET DOM twin and shared conformance (#153) | 107,569 | 119,471 | 265,063 | 279,878 | 2,595,698 | 2,920,176 |
| `b4eebc5` | 2026-10-21 | feat(analyzers): Ranvier.Analyzers, build-time diagnostics for Ranvier traps (#154) | 107,569 | 119,470 | 265,065 | 279,868 | 2,595,591 | 2,920,211 |
| `433da79` | 2026-10-21 | refactor(view)!: graduate the view to src as Ranvier.View, Ranvier.View.Dom and Ranvier.View.Anime (#155) | 107,569 | 119,471 | 265,104 | 279,898 | 2,595,629 | 2,920,187 |
| `183cd59` | 2026-10-21 | build(publish): accept dependencies served by sources nuget.config maps (#156) | 107,569 | 119,471 | 265,109 | 279,920 | 2,595,682 | 2,920,190 |
| `b2a5781` | 2026-10-22 | build(deps): bump the npm-version-updates group across 5 directories with 11 updates (#151) | 107,570 | 119,469 | 265,113 | 279,871 | 2,595,668 | 2,920,160 |
| `9651ffa` | 2026-10-22 | feat(view): add opt-in Arms.Match lowering (#158) | 107,571 | 119,470 | 265,089 | 279,924 | 2,595,692 | 2,920,158 |
| `386dc43` | 2026-10-23 | feat(view): add DOM twin and inspection testing (#159) | 107,569 | 119,469 | 265,045 | 279,896 | 2,595,673 | 2,920,170 |
| `a72c14e` | 2026-10-23 | feat(view): generate Motion pages from shared F# declarations (#160) | 107,569 | 119,469 | 265,068 | 279,928 | 2,595,680 | 2,920,190 |
| `d9e9ccb` | 2026-10-23 | feat(playground): generate shared F# pages and modernize examples (#161) | 107,571 | 119,470 | 265,088 | 279,918 | 2,595,611 | 2,920,176 |
| `d230fe0` | 2026-10-23 | feat(examples): add F# server hydration and hosted demos (#162) | 107,569 | 122,669 | 265,043 | 279,926 | 2,595,654 | 2,920,185 |
| `26a1c71` | 2026-10-23 | chore: pin Fickl 0.5.3 and organize docs navigation (#163) | 107,569 | 119,469 | 265,020 | 279,930 | 2,595,656 | 2,920,191 |
| `f13bdbb` | 2026-10-24 | feat(docs): render signal maps with Ranvier.View (#169) | 107,571 | 119,470 | 265,094 | 279,895 | 2,595,657 | 2,920,099 |
| `01c8295` | 2026-10-24 | Merge pull request #170 from shayanhabibi/integration/issues | 107,569 | 119,471 | 265,075 | 279,898 | 2,596,276 | 2,920,804 |
| `b148911` | 2026-10-24 | Merge pull request #186 from shayanhabibi/integration/issues | 107,569 | 119,471 | 265,075 | 279,902 | 2,596,306 | 2,920,791 |
| `8403149` | 2026-10-24 | Merge pull request #191 from shayanhabibi/integration/issues | 107,569 | 119,469 | 265,075 | 279,894 | 2,596,287 | 2,920,839 |
| `0fa891c` | 2026-10-25 | Merge pull request #204 from shayanhabibi/integration/issues | 109,069 | 120,971 | 268,088 | 282,313 | 2,630,292 | 2,954,695 |
| `801f919` | 2026-10-25 | Merge pull request #217 from shayanhabibi/integration/issues | 109,070 | 120,969 | 268,110 | 282,277 | 2,630,307 | 2,954,793 |
| `980ebcc` | 2026-10-25 | perf(core): size a keyed projection's tables before an untrimmed pass (#212) | 109,068 | 120,969 | 268,094 | 282,290 | 2,630,217 | 2,954,713 |
| `9e5e9b0` | 2026-10-25 | perf(view): keep expandos on a host's own objects on .NET (#213) | 109,069 | 120,971 | 268,101 | 282,258 | 2,630,243 | 2,954,824 |
| `4a95a10` | 2026-10-25 | perf(core): create a memo-free projection row without a closure (#215) | 109,069 | 120,969 | 268,087 | 282,265 | 2,630,267 | 2,954,827 |
| `1504b3b` | 2026-10-25 | perf(core): allocate a signal's observer set on its first observer (#216) | 109,169 | 121,071 | 268,758 | 282,916 | 2,637,460 | 2,962,018 |
| `6784ff0` | 2026-10-25 | Merge pull request #219 from shayanhabibi/integration/issues | 109,169 | 121,070 | 268,780 | 282,897 | 2,637,465 | 2,961,981 |
| `d769b24` | 2026-10-26 | Merge pull request #221 from shayanhabibi/feat/220-mcp-detection-loop | 109,169 | 121,069 | 268,796 | 282,900 | 2,637,459 | 2,962,022 |
| `cc30cd7` | 2026-10-26 | docs: refresh guides and move Nacara examples to Ranvier.View (#222) | 109,171 | 121,069 | 269,597 | 283,410 | 2,643,856 | 2,968,512 |
| `55ac77b` | 2026-10-26 | fix(docs): restore the state dial and speed up live compilation (#223) | 109,169 | 121,071 | 269,562 | 283,383 | 2,643,858 | 2,968,314 |
| `4154db8` | 2026-10-26 | docs: showcase sugar in the live coffee cart example (#224) | 109,170 | 121,069 | 269,596 | 283,393 | 2,643,809 | 2,968,433 |
| `367df5f` | 2026-10-26 | feat(persistence): add scoped Marten state adapter (#225) | 109,169 | 121,071 | 269,596 | 283,385 | 2,643,818 | 2,968,469 |
| `f9c9245` | 2026-10-26 | feat(persistence): add scoped KurrentDB snapshot adapter (#226) | 109,169 | 121,071 | 269,600 | 283,434 | 2,643,832 | 2,968,437 |
| `6066b93` | 2026-10-26 | feat(persistence): add Equinox and FsCodec state sample (#227) | 109,169 | 121,069 | 269,574 | 283,416 | 2,643,946 | 2,968,476 |
| `5ef8f40` | 2026-10-26 | ci: publish all Ranvier packages from verified builds (#229) | 109,169 | 121,069 | 269,568 | 283,428 | 2,643,854 | 2,968,475 |
| `19a8845` | 2026-10-26 | ci: finish NuGet publishing when uploads succeed (#230) | 109,169 | 121,071 | 269,559 | 283,410 | 2,643,818 | 2,968,474 |
| `f56950c` | 2026-10-26 | feat(motion): add interactive graphs and fix dashboard streaming (#231) | 109,171 | 121,070 | 269,558 | 283,386 | 2,643,900 | 2,968,428 |
| `9598e16` | 2026-10-27 | feat(css): add typed styling, composite utilities and static extraction (#232) | 109,169 | 121,071 | 269,595 | 283,434 | 2,643,828 | 2,968,476 |
| `aaf508c` | 2026-10-27 | feat(css): derive numeric expression semantics for generation | 109,169 | 121,071 | 269,582 | 283,376 | 2,643,835 | 2,968,493 |
| `869d472` | 2026-10-27 | feat(css): generate unified typed numeric expressions | 109,169 | 121,071 | 269,637 | 283,411 | 2,643,940 | 2,968,454 |
| `1099e6a` | 2026-10-27 | feat(anime): animate typed CSS custom properties (#236) | 109,169 | 121,071 | 269,560 | 283,413 | 2,643,893 | 2,968,441 |
| `827c2c1` | 2026-10-27 | ci: speed up CSS checks and stabilize KurrentDB retries (#237) | 109,169 | 121,069 | 269,577 | 283,434 | 2,643,903 | 2,968,480 |
| `acb0165` | 2026-10-28 | feat(css): generate compact discoverable grammar bindings (#238) | 109,169 | 121,071 | 269,592 | 283,396 | 2,643,921 | 2,968,434 |
| `1f10137` | 2026-10-28 | refactor!: move to ranvier-org and remove the extracted CSS libraries (#242) | 109,169 | 121,071 | 269,586 | 283,418 | 2,643,850 | 2,968,422 |
| `de8da60` | 2026-10-28 | feat(webcore): Ranvier's DOM and view on Ranvier.WebCore (preview) (#239) | 109,169 | 121,071 | 269,548 | 283,399 | 2,643,909 | 2,968,462 |
| `61715ab` | 2026-10-28 | refactor!: move the view, analyzer and persistence projects out of core (#244) | 109,169 | 121,071 | 269,604 | 283,389 | 2,643,939 | 2,968,448 |
| `cb27f92` | 2026-10-28 | fix(docs): exercise the retained signal-map live editor | 109,169 | 121,071 | 269,571 | 283,399 | 2,643,945 | 2,968,473 |
| `01a5d4d` | 2026-10-28 | fix(ci): prepare view checkout before dependency submission | 109,169 | 121,069 | 269,573 | 283,380 | 2,643,921 | 2,968,430 |
| `68041c0` | 2026-10-28 | fix(ci): include view dependencies in Linux submission scans | 109,170 | 121,069 | 269,612 | 283,395 | 2,643,940 | 2,968,439 |

