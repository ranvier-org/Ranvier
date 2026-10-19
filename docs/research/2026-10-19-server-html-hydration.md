# Server HTML and hydration: measurements and mutation checks

Date: 2026-10-19. Branch `poc/fable-ranvier-dom`. Plan: [server HTML and hydration](../superpowers/plans/2026-10-19-server-html-hydration.md).

## First content on .NET

Render time and allocated bytes per render, BenchmarkDotNet 0.15.8, Release, default job with `MemoryDiagnoser`. The fixtures are `ServerFixtures.renderComponentPage` and `ServerFixtures.renderKeyedPage`.

Machine: AMD Ryzen 9 9900X (12 cores), Windows 11 25H2, .NET SDK 10.0.402, .NET 10.0.13 X64 RyuJIT x86-64-v4.

### Harness

Every server task re-runs these two commands from the repository root and adds rows labelled `after <task>`. `OUT` is a local directory that is not committed; BenchmarkDotNet artifacts and the byte JSON stay there.

```bash
OUT=<local output directory>/server-html/<label>
dotnet run -c Release --project experiments/Ranvier.View.Portable/ServerBench/ServerBench.fsproj -- --filter '*' --exporters json --artifacts $OUT/dotnet
dotnet run -c Release --project experiments/Ranvier.View.Portable/ServerBench/ServerBench.fsproj -- --bytes > $OUT/bytes.json
```

`--bytes` prints one JSON object per line: `{"fixture":"component"|"keyed","rows":N,"bytes":B,"bytesPerRow":B/N}`, with `bytes` the UTF-8 length of the rendered page. The timing tables read `Mean`, `Error`, `StdDev` and `Allocated` from `$OUT/dotnet/results/*-report-github.md`.

Raw artifacts for each phase were written to `server-html/<phase>/` under the local benchmark output directory (`before-s1`, `after-s1`, `task5` for after S2 and `task-7` for after S3; local, not committed).

BenchmarkDotNet's environment block tags `[Host]` with `DEBUG`: the tag describes the F# host assembly that launches the run, while the timed child process is built with `-c Release` and `Optimize=true`.

Any "before S1 / after Sx" comparison in this note is cross-session, not a gate.

### Results

Mean, Error and StdDev are in µs.

| Phase | Fixture | Rows | Mean (µs) | Error (µs) | StdDev (µs) | Allocated | HTML bytes | HTML bytes per row |
|---|---|---|---|---|---|---|---|---|
| before S1 | component | 1 | 1.019 | 0.0203 | 0.0579 | 5.62 KB | 110 | 110.0 |
| before S1 | component | 16 | 8.405 | 0.1638 | 0.2243 | 41.43 KB | 1249 | 78.1 |
| before S1 | component | 128 | 60.464 | 0.4580 | 0.4060 | 310.13 KB | 9931 | 77.6 |
| before S1 | keyed | 1000 | 1115 | 7.8 | 6.9 | 3.86 MB | 203846 | 203.8 |
| before S1 | keyed | 10000 | 16749 | 332.7 | 771.1 | 38.72 MB | 2057848 | 205.8 |
| after S1 | component | 1 | 0.652 | 0.0128 | 0.0120 | 4.02 KB | 110 | 110.0 |
| after S1 | component | 16 | 5.521 | 0.2269 | 0.6689 | 25.66 KB | 1249 | 78.1 |
| after S1 | component | 128 | 40.765 | 0.8082 | 1.1591 | 188.48 KB | 9931 | 77.6 |
| after S1 | keyed | 1000 | 395.0 | 9.1 | 26.4 | 1.38 MB | 203846 | 203.8 |
| after S1 | keyed | 10000 | 14635 | 281.5 | 276.5 | 13.92 MB | 2057848 | 205.8 |
| after S2 | component | 1 | 1.589 | 0.0312 | 0.0383 | 6.47 KB | 127 | 127.0 |
| after S2 | component | 16 | 9.935 | 0.0919 | 0.0768 | 39.52 KB | 1266 | 79.1 |
| after S2 | component | 128 | 67.000 | 1.3151 | 2.0858 | 287.27 KB | 9948 | 77.7 |
| after S2 | keyed | 1000 | 578.2 | 15.04 | 44.12 | 2.11 MB | 203863 | 203.9 |
| after S2 | keyed | 10000 | 15504 | 43.05 | 33.61 | 21.27 MB | 2057865 | 205.8 |
| after S3 | component | 1 | 1.162 | 0.0118 | 0.0105 | 7.37 KB | 141 | 141.0 |
| after S3 | component | 16 | 7.298 | 0.0536 | 0.0447 | 44.62 KB | 1497 | 93.6 |
| after S3 | component | 128 | 51.519 | 1.0080 | 1.3107 | 326.34 KB | 11888 | 92.9 |
| after S3 | keyed | 1000 | 442.1 | 8.79 | 14.93 | 2.24 MB | 203863 | 203.9 |
| after S3 | keyed | 10000 | 15301 | 179.09 | 167.52 | 22.57 MB | 2057865 | 205.8 |
| after S4 | component | 1 | 1.199 | 0.0192 | 0.0180 | 7.42 KB | 141 | 141.0 |
| after S4 | component | 16 | 7.369 | 0.0551 | 0.0515 | 44.79 KB | 1497 | 93.6 |
| after S4 | component | 128 | 50.556 | 0.5061 | 0.4734 | 327.39 KB | 11888 | 92.9 |
| after S4 | keyed | 1000 | 435.1 | 8.64 | 19.86 | 2.25 MB | 203863 | 203.9 |
| after S4 | keyed | 10000 | 15434 | 66.75 | 62.44 | 22.65 MB | 2057865 | 205.8 |

Every row is cross-session, not a gate. The keyed 10000 rows are noisy: each render writes a 2 MB page, nearly every operation triggers a Gen2 collection (984 per 1000 after S1), and the before-S1 StdDev is 771 µs. After S1, the .NET escape scan is `SearchValues<char>` with `IndexOfAny`; the HTML bytes are identical to before S1.

After S2, the fixtures render through `Server.render`: each render creates a request graph, a request root and one owner per instance, and disposes them. The HTML grows by the 17 bytes of the root marker pair (`<!--[r-->` is 9 bytes, `<!--]-->` is 8). The fixed cost of the graph and root shows at one component row (0.652 to 1.589 µs); the per-instance owners show in the keyed rows (395 to 578 µs and 1.38 to 2.11 MB at 1000 rows).

After S3, a reflected property writes its attribute: each component row's `input` gains its `value` attribute (14 bytes at one row), and the keyed pages are unchanged. The after-S3 rows come from a later session than the after-S2 rows, with the positional ids of Task 6 in between, so the timing difference is not a regression measure.

After S4, a page carries arm hashes, boundary markers and text separators where its template has arms, boundaries and adjacent text slots. Neither fixture has any of these, so the HTML bytes equal the after-S3 bytes. The after-S4 rows are cross-session, not a gate.

## First content under Node against Solid 2

Microseconds per render under Node: Ranvier's `ServerFixtures.renderComponentPage` and `renderKeyedPage` (Fable, Release, compiled without `RANVIER_VIEW_COUNTERS`) against the same markup rendered by Solid 2's `renderToString` (`solid-js`, `@solidjs/web` and `@solidjs/compiler` 2.0.0-rc.14; `generate: "ssr"`, `hydratable: true`). Ranvier and Solid 2 run in one session: each of three `node --expose-gc` processes runs 15 rounds after 3 warmups per fixture and row count, the mode order rotated per round and process, with a `gc()` before each sample. A sample times 256, 64 or 16 renders at 1, 16 or 128 component rows, and 4 or 1 renders at 1000 or 10000 keyed rows.

Command, from `examples/Fable.Ranvier.Playground`: `node benchmarks/frameworks/server-render.mjs --build [--out=<dir>]`. Output schema: `measuredAt`, `baseCommit`, `dirty`, `node`, `cpu`, `platform`, `unit`, `verified` (per fixture, rows and mode, the count of `class="panel"` or `<tr` elements, equal to `rows`, and the UTF-8 bytes), `samples` and `summaries`. The run recorded here was written to the local benchmark output directory (`server-html/task-12/node-first-content.json`, not committed); base commit b872d0b with the harness files uncommitted.

Machine: AMD Ryzen 9 9900X, Windows 11 25H2 (10.0.26200), Node v26.7.0.

| Fixture | Rows | Ranvier median (µs) | Ranvier p25–p75 | Ranvier per process | Solid 2 median (µs) | Solid 2 p25–p75 | Solid 2 per process | Ranvier / Solid 2 | Ranvier bytes per row | Solid 2 bytes per row |
|---|---|---|---|---|---|---|---|---|---|---|
| component | 1 | 11.57 | 11.38–11.84 | 11.42 / 11.77 / 11.59 | 7.23 | 7.04–7.49 | 7.19 / 7.29 / 7.18 | 1.60 | 141.0 | 122.0 |
| component | 16 | 65.71 | 62.71–68.09 | 66.24 / 66.29 / 62.71 | 20.25 | 19.90–20.82 | 20.07 / 20.56 / 20.19 | 3.24 | 93.6 | 99.9 |
| component | 128 | 407.2 | 390.3–422.2 | 406.1 / 402.2 / 414.8 | 111.1 | 103.0–116.5 | 108.2 / 116.5 / 112.8 | 3.67 | 92.9 | 102.1 |
| keyed | 1000 | 2313 | 2232–2386 | 2251 / 2336 / 2326 | 443.9 | 406.6–467.0 | 437.5 / 447.2 / 444.4 | 5.21 | 203.9 | 213.8 |
| keyed | 10000 | 17388 | 16669–17999 | 17388 / 17056 / 17810 | 3010 | 2888–3111 | 3045 / 2947 / 2961 | 5.78 | 205.8 | 216.6 |

Each median is over 45 samples (3 processes × 15 rounds). Solid 2 renders the first content faster at every fixture and row count: 1.6 times at one component row, rising to 5.8 times at 10000 keyed rows. The per-process medians of each mode lie within 8 % of each other, so the ordering is stable across processes.

Solid 2's output carries its hydration keys: an unquoted `_hk=` attribute on each template root (`<section _hk=0>`, `<div _hk=100 …>`, `<tr _hk=100>`). Ranvier's output carries its markers: the root pair `<!--[r-->` … `<!--]-->` and the list region pair `<!--[-->` … `<!--]-->`. Solid 2 pays its key on every row, Ranvier its markers once per page, so Solid 2's page is smaller at one component row (122 against 141 bytes) and larger from 16 rows on (about 10 bytes per keyed row). Ranvier's bytes under Node equal its bytes on .NET.

Ranvier under Node renders 1000 keyed rows in 2313 µs, against 435 µs on .NET in the after-S4 row above; the two harnesses differ, so the comparison is cross-runtime, not a gate.
