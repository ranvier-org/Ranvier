# Core trimmed pass: keyed list cost

Date: 2026-10-14

## Method

- Commits: `B0` = `093269fcc4009a8d59866f1b3910a5746f03d549` (the benchmarks commit on `perf/core-trimmed-pass`, on top of `dc0ee50`, the branch's projection work: memo-free `createProjectionOnce`, the shared `ensureUniqueKeys`, the create-first stop and rollback kept off the default pass). The core is unchanged from `dc0ee50`.
- Machine: AMD Ryzen 9 9900X 12-Core Processor, 24 logical cores, 62 GB, win32 10.0.26200. Node v26.7.0, .NET SDK 10.0.402, Fable 5.18.0, `@solidjs/signals` 2.0.0-rc.14.
- Node run: `docs/research/core-trimmed-pass/before.json` (commit `B0`, `dirtyAtStart` empty). .NET run: `docs/research/core-trimmed-pass/dotnet-before/` (see `provenance.json`).

Seven Node processes, each with 10 warmup and 31 timed rounds per operation. A round times the operation once per framework in a seeded random order, each on a fresh list after a forced GC. Before timing, each operation's resulting rows (order and text) are checked equal across the frameworks.

Each .NET benchmark runs two passes and alternates between two states of equal cost. With `Layout`, an effect reads every row after every key change, which is linear in the list length in both versions.

## Before

### Node: Ranvier against Solid 2 (milliseconds per operation)

| Operation | Solid 2 ms | Ranvier ms | Ratio Ranvier / Solid 2 [q1, q3] | Per-process medians |
| --- | --- | --- | --- | --- |
| create1k | 1.4107 | 4.3832 | 3.079 [2.539, 3.747] | 3.32, 3.09, 2.94, 3.01, 3.21, 2.99, 2.86 |
| create10k | 7.1480 | 25.3979 | 3.428 [2.605, 4.602] | 3.51, 3.34, 3.09, 3.50, 3.88, 3.48, 3.93 |
| replace1k | 1.8512 | 4.4127 | 2.407 [1.988, 2.866] | 2.69, 2.26, 2.32, 2.43, 2.12, 2.42, 2.59 |
| append1k | 1.6978 | 3.6939 | 2.136 [1.709, 2.740] | 2.45, 2.05, 2.32, 1.87, 1.97, 2.20, 2.03 |
| update10th1k | 0.2224 | 0.1736 | 0.805 [0.599, 1.060] | 0.73, 0.78, 0.81, 0.87, 0.84, 0.83, 0.68 |
| swap1k | 0.2092 | 0.8253 | 4.175 [3.062, 5.668] | 4.09, 4.10, 3.98, 3.75, 4.36, 4.62, 4.62 |
| swap10k | 0.8850 | 6.9082 | 8.207 [5.428, 12.073] | 10.13, 9.34, 6.05, 8.42, 7.45, 8.69, 8.44 |
| remove1k | 0.2747 | 1.0608 | 3.815 [3.050, 5.009] | 3.53, 4.22, 3.61, 3.76, 3.79, 3.73, 4.09 |
| remove10k | 1.2121 | 7.0198 | 5.942 [4.457, 8.061] | 6.05, 5.94, 5.01, 5.00, 6.19, 7.85, 5.98 |
| clear1k | 0.6671 | 1.5776 | 2.446 [1.840, 3.366] | 2.58, 2.82, 2.52, 2.40, 2.12, 3.43, 2.10 |
| clear10k | 3.3818 | 9.0862 | 2.663 [2.032, 3.771] | 2.53, 2.83, 2.37, 2.63, 2.60, 3.50, 2.48 |

### .NET: ListPassBenchmarks

| Benchmark | Rows | Layout | Mean | Allocated |
| --- | --- | --- | --- | --- |
| Swap | 1000 | False | 35.36 us | 15.93 KB |
| RemoveAndRestore | 1000 | False | 34.79 us | 16.65 KB |
| UpdateOneItem | 1000 | False | 33.19 us | 8.10 KB |
| ReplaceAll | 1000 | False | 433.49 us | 1461.22 KB |
| ClearAndCreate | 1000 | False | 202.62 us | 730.70 KB |
| Swap | 1000 | True | 53.96 us | 15.99 KB |
| RemoveAndRestore | 1000 | True | 53.94 us | 16.71 KB |
| UpdateOneItem | 1000 | True | 33.1 us | 8.10 KB |
| ReplaceAll | 1000 | True | 465.45 us | 1461.28 KB |
| ClearAndCreate | 1000 | True | 192.52 us | 730.76 KB |
| Swap | 10000 | False | 313.68 us | 156.55 KB |
| RemoveAndRestore | 10000 | False | 309.24 us | 157.27 KB |
| UpdateOneItem | 10000 | False | 310.7 us | 78.41 KB |
| ReplaceAll | 10000 | False | 10,178.97 us | 14609.66 KB |
| ClearAndCreate | 10000 | False | 2,908.06 us | 7304.91 KB |
| Swap | 10000 | True | 530.9 us | 156.62 KB |
| RemoveAndRestore | 10000 | True | 545.11 us | 157.34 KB |
| UpdateOneItem | 10000 | True | 375.76 us | 78.41 KB |
| ReplaceAll | 10000 | True | 11,677.75 us | 14609.72 KB |
| ClearAndCreate | 10000 | True | 3,267.12 us | 7304.98 KB |

### .NET: ProjectionBenchmarks

| Benchmark | Items | Mean | Allocated |
| --- | --- | --- | --- |
| ReadRow | 8 | 8.8 ns | 0 B |
| EditOneItem | 8 | 289.0 ns | 184 B |
| Reorder | 8 | 261.9 ns | 440 B |
| ChurnOneKey | 8 | 440.8 ns | 752 B |
| ReadRow | 64 | 8.8 ns | 0 B |
| EditOneItem | 64 | 1200.9 ns | 184 B |
| Reorder | 64 | 1417.9 ns | 2456 B |
| ChurnOneKey | 64 | 1327.0 ns | 1424 B |
| ReadRow | 512 | 8.8 ns | 0 B |
| EditOneItem | 512 | 8320.2 ns | 184 B |
| Reorder | 512 | 10517.0 ns | 18584 B |
| ChurnOneKey | 512 | 8666.7 ns | 6800 B |
