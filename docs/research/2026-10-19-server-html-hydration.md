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

Raw artifacts for `before S1` were written to `server-html/before-s1/` under the local benchmark output directory (local, not committed).

Any "before S1 / after Sx" comparison in this note is cross-session, not a gate.

### Results

| Phase | Fixture | Rows | Mean | Error | StdDev | Allocated | HTML bytes | HTML bytes per row |
|---|---|---|---|---|---|---|---|---|
| before S1 | component | 1 | 1.019 µs | 0.0203 µs | 0.0579 µs | 5.62 KB | 110 | 110.0 |
| before S1 | component | 16 | 8.405 µs | 0.1638 µs | 0.2243 µs | 41.43 KB | 1249 | 78.1 |
| before S1 | component | 128 | 60.464 µs | 0.4580 µs | 0.4060 µs | 310.13 KB | 9931 | 77.6 |
| before S1 | keyed | 1000 | 1115 µs | 7.8 µs | 6.9 µs | 3.86 MB | 203846 | 203.8 |
| before S1 | keyed | 10000 | 16749 µs | 332.7 µs | 771.1 µs | 38.72 MB | 2057848 | 205.8 |
