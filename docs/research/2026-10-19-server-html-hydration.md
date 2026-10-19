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

Every row is cross-session, not a gate. The keyed 10000 rows are noisy: each render writes a 2 MB page, nearly every operation triggers a Gen2 collection (984 per 1000 after S1), and the before-S1 StdDev is 771 µs. After S1, the .NET escape scan is `SearchValues<char>` with `IndexOfAny`; the HTML bytes are identical to before S1.

After S2, the fixtures render through `Server.render`: each render creates a request graph, a request root and one owner per instance, and disposes them. The HTML grows by the 17 bytes of the root marker pair (`<!--[r-->` is 9 bytes, `<!--]-->` is 8). The fixed cost of the graph and root shows at one component row (0.652 to 1.589 µs); the per-instance owners show in the keyed rows (395 to 578 µs and 1.38 to 2.11 MB at 1000 rows).

After S3, a reflected property writes its attribute: each component row's `input` gains its `value` attribute (14 bytes at one row), and the keyed pages are unchanged. The after-S3 rows come from a later session than the after-S2 rows, with the positional ids of Task 6 in between, so the timing difference is not a regression measure.
