# Observer removal benchmark

Can `ObserverSet.Remove` get cheaper by tombstoning slots instead of swap-removing them? The motivation was a Chromium
profile of disposing 128 view rows (Fable build), where `ObserverSet.Remove` took 49.6% of teardown, most of it in
`refIndexRemove` and `refIndexSet` on the `RefIndex` kept above 8 observers.

**Outcome: rejected.** On .NET the tombstone design made disposal 22-31% faster at 64 and 1024 observers. Under
Fable/V8 it made root disposal about 30% slower, and Fable is the build the profile came from. Master's swap-remove
stays. The benchmarks and tests from this work are committed. The engine change is not.

## Setup

- Machine: AMD Ryzen 9 9900X 4.40GHz, 12 cores / 24 threads; Windows 11 (10.0.26200.9457, 25H2); High performance plan
- .NET: SDK 10.0.402, runtime 10.0.13, X64 RyuJIT x86-64-v4; BenchmarkDotNet v0.15.8, job `UnrollFactor=16`
- JS: Node v26.7.0 running `dotnet fable fable/Ranvier.Tests.Fable -c Release` output; scripts are in the session
  scratchpad and are not committed
- Base: master `e2e0f95`. Benchmarks: commit `75c9296` (`bench/Ranvier.Benchmarks/Lifetimes.fs`,
  `ObserverRemovalBenchmarks` and `ObserverChurnBenchmarks`)
- Command: `dotnet run --project bench/Ranvier.Benchmarks -c Release -- --anyCategories ObserverRemoval`
- Raw base reports: `docs/.ai/benchmarks/results/Ranvier.Benchmarks.Lifetimes.Observer*-report-github.md`

Cases (`Nodes` = N effects on one source):

- `DisposeRoot`: create N effects under a root, then dispose the root. Removal runs newest first.
- `DisposeInCreationOrder`: create N effects, then dispose them oldest first. This is swap-remove's worst order.
- `RelinkAll`: every effect drops its edge to the wide source, then every effect links it again.
- `RelinkOne`: one effect drops its edge and links it again, cycling through the effects.
- `WriteWide`: one write that notifies all N effects. Setup has already relinked half of them.

## Design measured

- **Vacant slots.** `Remove` blanks the slot and moves no other observer. The live observers sit in a window
  `[head, length)`. Removing the first or last live observer shrinks the window and trims any vacant slots it exposes.
- **Compaction.** The live observers are moved to the front once vacant slots outnumber them 2:1. Each moved entry
  gets an index overwrite. A walk can trigger compaction, and that is safe: entries only move towards the front, so
  no unvisited observer is skipped.
- **Lazy end removal.** Removing an end observer leaves its index entry stale. A lookup checks the hit against the
  slot, so a stale entry reads as a miss. The index is cleared once stale entries exceed half the live count, and it
  is rebuilt when a lookup next needs it.
- **Eager middle removal.** Removing a middle observer deletes its index entry immediately.
- **Count zero.** When the live count reaches 0, the window resets and the index is cleared.

All existing tests passed with this design, including the traced and Fable suites, plus 21 new ones.

## .NET (BenchmarkDotNet, back-to-back runs, StdDev 1-3%)

| Case | N | master | tombstone | ratio |
| --- | ---: | ---: | ---: | ---: |
| DisposeRoot | 8 | 438.6 ns | 473.3 ns | 1.08 |
| DisposeRoot | 64 | 6,159.5 ns | 4,741.6 ns | 0.77 |
| DisposeRoot | 1024 | 101,567 ns | 79,234 ns | 0.78 |
| DisposeInCreationOrder | 8 | 649.8 ns | 453.4 ns | 0.70 |
| DisposeInCreationOrder | 64 | 6,976.0 ns | 4,795.3 ns | 0.69 |
| DisposeInCreationOrder | 1024 | 118,984 ns | 105,785 ns | 0.89 |
| RelinkAll | 8 | 378.0 ns | 410.1 ns | 1.08 |
| RelinkAll | 64 | 3,927.7 ns | 3,640.4 ns | 0.93 |
| RelinkAll | 1024 | 74,813 ns | 62,853 ns | 0.84 |
| RelinkOne | 8 | 60.95 ns | 60.15 ns | 0.99 |
| RelinkOne | 64 | 72.26 ns | 69.26 ns | 0.96 |
| RelinkOne | 1024 | 83.41 ns | 76.98 ns | 0.92 |
| WriteWide | 8 | 132.9 ns | 135.3 ns | 1.02 |
| WriteWide | 64 | 1,008.7 ns | 1,021.9 ns | 1.01 |
| WriteWide | 1024 | 17,975 ns | 16,954 ns | 0.94 |

The tombstone column clears stale entries above 16 rather than above half the live count. That threshold changes
only how many stale entries the index holds; the removal paths are the same. Allocation is identical in every case.
A first master run, made while the machine was busy, was about 30% slower across the board, including cases that
never touch removal. It is discarded.

## Fable / V8

These numbers come from the same cases written against the compiled graph, with master and the tombstone build
interleaved in one process, median of 11.

- **The isolated `ObserverSet` won where the profile pointed.** Adding N plain observers and removing them, per
  observer at N = 1024:

  | Removal order | master | tombstone |
  | --- | ---: | ---: |
  | Reverse | 41.3 ns | 33.3 ns |
  | Forward | 47.3 ns | 33.5 ns |
  | Scattered | 55.2 ns | 55.5 ns |

  At N = 64, reverse went from 34.2 ns to 28.9 ns and forward from 40.3 ns to 29.7 ns.
- **Through the graph, root disposal got slower.** `DisposeRoot` was measured in separate processes, 40,000
  iterations each:

  | N | master | tombstone |
  | ---: | ---: | ---: |
  | 64 | 11.9-12.0 µs | 15.7-16.3 µs |
  | 1024 | 192 µs | 244 µs |

  The `--trace-gc` counts over the same run explain it. Master did 162 scavenges and 30 mark-compacts. The tombstone
  build did 179 scavenges and 42 mark-compacts. The CPU profile agrees: `ObserverSet.Remove` drops from 7.4% of
  samples to under 1%, but the time moves into GC. Two causes are likely:
  - Removed effects stay reachable from stale `Map` entries until the clear, so more of them survive a scavenge.
  - `Map.clear()` followed by a rebuild reallocates the backing table, where master's progressive `delete` does not.
- **Other cases did not move clearly.** The interleaved runs gave these ratios (tombstone / master):

  | Case | Ratio range |
  | --- | --- |
  | `DisposeInCreationOrder` | 0.91-0.98 |
  | `RelinkAll` | 0.81-1.03 |
  | `RelinkOne` | 0.83-1.04 |
  | `WriteWide` | 0.97-1.35 |
  | `DisposeRoot` at 64 | 1.08-1.15 |
- **Other variants did no better.**
  - Deleting index entries eagerly on every removal keeps the window and compaction and has no retention. It was a
    wash: `DisposeRoot` 64 measured 12.1 µs against master's 12.0 µs, and 1024 measured 201 µs against 192 µs.
    Without the lazy deletion there is little left to save.
  - Clearing stale entries above the live count, not half of it, was slower still.

## What remains

- The cost the profile shows is hashing, and only removing the hash removes it. Store each edge's slot position on
  both sides: the `SourceList` entry and the `ObserverSet` slot. Removal then becomes an O(1) unlink with no index
  operation. This is the doubly-linked `Link` model the `SourceList` remarks already name as the end state. It
  changes `ISource.AddObserver` and `RemoveObserver` and their 12 pass-through implementers.
- Under Fable, `ObserverSlot` compiles to a heap object, so every `Add` allocates. The profile shows it as
  `ObserverSlot_$ctor`, at about 3% of a teardown-heavy run. A Fable-only representation that stores the observer
  directly would remove that allocation. The struct exists only for .NET's covariant-store check.
- The new tests (`Observers.fs` `ObserverSet`, and `Retention.fs`) pin the behaviour any future design must keep:
  - removal and compaction during a notification walk;
  - observers added mid-walk;
  - an exact `Count` under out-of-order and repeated removal;
  - re-adding after removal on a wide list;
  - wide-source retention.

## 2026-10-19: hash-free observer removal

Step 1 of the revised direction in `docs/.ai/superpowers/specs/2026-10-14-indexed-edges-design.md`. `ObserverSet`
drops `RefIndex`, and `SourceList` takes over duplicate-read detection. `ISource` is unchanged.

**Outcome: dispose wins on both runtimes and closes most of the browser gap, but scattered removal is not the only
loss.** A reader of many sources that changes its read order is 1.1-1.4× slower at 8 sources and 2.4-2.9× slower at
64 and 1024. Relinking one observer of 1024 is 2.8× slower on .NET and 4.5× on V8. By the design's rule, this does not
become the single model.

### Variants

All three keep swap-remove, the backwards re-clamped notification walk and `Add`'s check of the last slot. They differ
only in where removal looks for the observer.

- **A** (`c121218`) checks the last slot, then index 0, then scans from 1. The design expected teardown in either
  order to be O(1). It is not: after index 0 is removed, the last observer moves into 0, so the next oldest sits at 1,
  then 2, and so on. Creation-order teardown costs about n/8 compares per removal (129 at n = 1024, simulated) until
  the second half, which hits the last slot.
- **B** (`1d89caf`) checks the last slot, then scans from the position after the previous middle removal, wrapping
  round. Creation order costs about 1.5 compares per removal at any n.
- **C** (`6c05004`) is B with the wrapped scan written as two plain loops (`hint..last-1`, then `0..hint-1`). Its
  compare count matches B's, and V8 runs it faster.

The reader side is shared by all three. On an append, `SourceList` asks whether the source already holds a slot: a scan
up to 8 slots, and above that a reference index built on the first append of a run and dropped at `EndRun` or on a
trim (finding 6). A held source is not passed to `AddObserver`. Finding 1's handle-only check needs edge handles, so
it does not apply here.

### Setup

- Machine, OS, .NET and BenchmarkDotNet as above; Node v26.7.0; Chromium 153.0.8010.12; Solid 2.0.0-rc.14.
- Base: `d1121c0`, which is master `305d009`'s `src/` plus the new benchmarks. Each variant was built in its own
  worktree at the commit listed.
- .NET: `dotnet run --project bench/Ranvier.Benchmarks -c Release -- --anyCategories ObserverRemoval`, one process per
  run, interleaved base, A, B, base, A, B, then base, C, base, C. Each cell is the mean of two process means, with the
  per-process values in brackets.
- Fable/V8: each variant's `src/Ranvier` compiled with `dotnet fable -c Release`, all four loaded into one Node process
  and interleaved, 11 rounds, median per process. Three processes; each cell is the median of the process medians, with
  their range. `--trace-gc` counts come from separate processes, one variant and one case each, 2.56 million effects
  created and disposed. Scripts are in the session scratchpad and are not committed.
- Browser: `poc/fable-ranvier-dom` at `29f8505`, whose `src/Ranvier/Core.fs` is identical to master's. Each variant's
  `Core.fs` was copied in, `node benchmarks/frameworks/run.mjs --build --template-comparison` was run from
  `examples/Fable.Ranvier.Playground`, and the file was restored. Run order: base, B, A, base, B, base, C, A, base, C.
- New cases (`d1121c0`): `DisposeScattered` disposes in one fixed shuffled order. `OutOfOrderReadBenchmarks.RotateReads`
  is one effect reading N signals, each run rotated by one from the last, so every read after the first misses its
  slot.

### .NET

| Case | N | base | C | C/base | A/base | B/base |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| DisposeRoot | 8 | 415 ns (411, 419) | 413 ns (402, 425) | 1.00 | 0.99 | 1.00 |
| DisposeRoot | 64 | 4,275 ns (4,292, 4,258) | 3,086 ns (3,221, 2,951) | 0.72 | 0.69 | 0.67 |
| DisposeRoot | 1024 | 73.2 µs (73.2, 73.1) | 50.8 µs (50.1, 51.5) | 0.69 | 0.67 | 0.70 |
| DisposeInCreationOrder | 8 | 417 ns (417, 418) | 403 ns (403, 403) | 0.97 | 0.96 | 0.90 |
| DisposeInCreationOrder | 64 | 4,869 ns (4,812, 4,926) | 3,130 ns (3,128, 3,132) | 0.64 | 0.67 | 0.61 |
| DisposeInCreationOrder | 1024 | 87.0 µs (87.6, 86.4) | 53.6 µs (54.2, 53.0) | 0.62 | 1.29 | 0.60 |
| DisposeScattered | 8 | 419 ns (420, 418) | 432 ns (436, 429) | 1.03 | 0.98 | 0.94 |
| DisposeScattered | 64 | 4,979 ns (5,001, 4,957) | 3,455 ns (3,438, 3,472) | 0.69 | 0.68 | 0.68 |
| DisposeScattered | 1024 | 93.7 µs (95.0, 92.4) | 145.1 µs (139.3, 151.0) | 1.55 | 1.41 | 1.52 |
| RelinkOne | 8 | 59.6 ns (59.9, 59.2) | 54.3 ns (54.3, 54.4) | 0.91 | 0.94 | 0.90 |
| RelinkOne | 64 | 67.3 ns (68.0, 66.7) | 64.1 ns (64.0, 64.3) | 0.95 | 0.98 | 0.92 |
| RelinkOne | 1024 | 76.5 ns (76.7, 76.3) | 214.0 ns (217.3, 210.7) | 2.80 | 2.70 | 2.74 |
| RelinkAll | 8 | 360 ns (367, 353) | 404 ns (403, 405) | 1.12 | 1.02 | 1.05 |
| RelinkAll | 64 | 3,688 ns (3,680, 3,696) | 3,012 ns (3,023, 3,002) | 0.82 | 0.84 | 0.81 |
| RelinkAll | 1024 | 71.4 µs (72.1, 70.8) | 49.1 µs (49.1, 49.1) | 0.69 | 1.49 | 0.67 |
| WriteWide | 8 | 129 ns (128, 130) | 127 ns (129, 126) | 0.99 | 0.99 | 0.99 |
| WriteWide | 64 | 964 ns (935, 994) | 949 ns (961, 938) | 0.98 | 0.96 | 0.98 |
| WriteWide | 1024 | 17.5 µs (17.5, 17.5) | 17.4 µs (17.9, 16.9) | 1.00 | 1.02 | 0.99 |
| RotateReads | 8 | 79.2 ns (78.5, 79.9) | 107.8 ns (108.6, 106.9) | 1.36 | 1.30 | 1.29 |
| RotateReads | 64 | 467 ns (467, 467) | 1,287 ns (1,249, 1,325) | 2.76 | 2.73 | 2.80 |
| RotateReads | 1024 | 7.05 µs (7.41, 6.69) | 20.7 µs (21.0, 20.5) | 2.94 | 2.83 | 2.71 |

The A and B ratios are against the base processes of the first pass, which ran 2-6% slower than the second (base
`DisposeRoot` 64: 4,545 ns; 1024: 75.5 µs).

Allocation: each effect is 8 bytes larger, the `held` field on its `SourceList` (`DisposeRoot` 64: 21.3 KB against
20.8 KB). `RotateReads` allocates its per-run index: 5,216 B per run at 64 sources and 57,536 B at 1024, against none.
Every other case allocates nothing, as before.

### Fable / V8

| Case | N | base | C | C/base | A/base | B/base |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| DisposeRoot | 8 | 573 ns (552-574) | 498 ns (489-503) | 0.87 | 0.87 | 0.88 |
| DisposeRoot | 64 | 9.82 µs (9.33-10.3) | 3.73 µs (3.71-3.76) | 0.38 | 0.46 | 0.38 |
| DisposeRoot | 1024 | 140 µs (140-155) | 59.2 µs (58.7-59.4) | 0.42 | 0.48 | 0.43 |
| DisposeInCreationOrder | 8 | 654 ns (649-655) | 595 ns (592-610) | 0.91 | 0.90 | 0.92 |
| DisposeInCreationOrder | 64 | 11.9 µs (11.3-12.8) | 4.74 µs (4.71-4.75) | 0.40 | 0.48 | 0.40 |
| DisposeInCreationOrder | 1024 | 184 µs (180-200) | 75.5 µs (74.6-76.8) | 0.41 | 0.80 | 0.42 |
| DisposeScattered | 8 | 636 ns (628-640) | 558 ns (556-576) | 0.88 | 0.88 | 0.89 |
| DisposeScattered | 64 | 11.9 µs (11.8-12.4) | 5.11 µs (5.10-5.12) | 0.43 | 0.49 | 0.45 |
| DisposeScattered | 1024 | 197 µs (191-205) | 211 µs (209-212) | 1.07 | 1.00 | 1.34 |
| RelinkOne | 8 | 103 ns (103-103) | 95 ns (94-96) | 0.92 | 0.93 | 0.96 |
| RelinkOne | 64 | 132 ns (131-136) | 124 ns (123-126) | 0.94 | 0.84 | 1.04 |
| RelinkOne | 1024 | 139 ns (138-145) | 624 ns (598-651) | 4.49 | 2.62 | 5.42 |
| RelinkAll | 8 | 629 ns (625-636) | 633 ns (632-634) | 1.01 | 1.03 | 1.03 |
| RelinkAll | 64 | 7.58 µs (7.48-7.64) | 5.07 µs (5.06-5.15) | 0.67 | 0.70 | 0.69 |
| RelinkAll | 1024 | 130 µs (126-132) | 89.9 µs (87.7-94.5) | 0.69 | 1.18 | 0.67 |
| WriteWide | 8 | 311 ns (310-314) | 272 ns (270-274) | 0.87 | 0.90 | 0.89 |
| WriteWide | 64 | 2.41 µs (2.40-2.47) | 2.11 µs (2.08-2.18) | 0.87 | 0.89 | 0.90 |
| WriteWide | 1024 | 39.7 µs (38.9-41.8) | 35.9 µs (34.7-36.4) | 0.90 | 0.90 | 0.87 |
| RotateReads | 8 | 200 ns (200-201) | 228 ns (228-232) | 1.14 | 1.19 | 1.14 |
| RotateReads | 64 | 1.26 µs (1.25-1.26) | 3.12 µs (3.09-3.17) | 2.48 | 2.47 | 2.41 |
| RotateReads | 1024 | 19.8 µs (19.6-20.0) | 50.5 µs (50.4-51.3) | 2.55 | 2.55 | 2.56 |

`--trace-gc` counts over 2.56 million effects, three processes per cell:

| Case | N | base: scavenges / mark-compacts | A, B, C: scavenges / mark-compacts |
| --- | ---: | --- | --- |
| DisposeRoot | 64 | 110 / 1, 110 / 1, 60 / 10 | 96-97 / 1 in every process |
| DisposeRoot | 1024 | 61 / 10, 62 / 10, 103 / 1 | 76-85 / 1 in every process |
| DisposeInCreationOrder | 64 | 110-131 / 1 | 96-97 / 1 |
| DisposeInCreationOrder | 1024 | 85-111 / 1 | A 96-123 / 1; B and C 83-97 / 1 |
| DisposeScattered | 64 | 110-131 / 1 | 96-97 / 1 |
| DisposeScattered | 1024 | 91-100 / 1 | 84-128 / 1 |

Unlike tombstoning, removing the hash adds no GC work. The base build alone shows processes with 10 mark-compacts.
Within the interleaved runs, base `DisposeRoot` 1024 ranged 99-201 µs per process round, and C 56-76 µs.

### Browser: template component, Chromium

`ranvier-template`, µs per operation, median of three Chromium processes (process medians in brackets), and the ratio
to Solid 2 in the same run.

| Run | Variant | Dispose 128 | /Solid | Dispose 16 /Solid | Mount 128 /Solid | Update 128 /Solid |
| ---: | --- | ---: | ---: | ---: | ---: | ---: |
| 1 | base | 35.9 (35.9, 35.9, 36.1) | 1.39 | 1.35 | 1.11 | 1.05 |
| 4 | base | 36.7 (37.3, 32.3, 36.6) | 1.44 | 1.25 | 1.13 | 1.05 |
| 11 | base | 36.7 (37.0, 36.4, 36.7) | 1.42 | 1.23 | 1.10 | 1.06 |
| 14 | base | 35.6 (35.9, 35.6, 34.4) | 1.45 | 1.31 | 1.10 | 1.06 |
| 3 | A | 29.4 (29.4, 30.3, 26.6) | 1.13 | 1.01 | 1.09 | 1.07 |
| 13 | A | 28.1 (25.3, 29.8, 27.7) | 1.14 | 1.08 | 1.06 | 1.04 |
| 2 | B | 29.4 (30.0, 29.8, 26.4) | 1.14 | 1.13 | 1.13 | 1.05 |
| 5 | B | 26.9 (30.2, 26.6, 26.4) | 1.06 | 1.10 | 1.14 | 1.05 |
| 12 | C | 28.0 (28.9, 28.3, 27.2) | 1.15 | 1.02 | 1.07 | 1.05 |
| 15 | C | 29.2 (29.4, 29.2, 25.3) | 1.19 | 1.12 | 1.07 | 1.05 |

128-row dispose drops from 35.6-36.7 µs to 26.9-29.4 µs, and from 1.39-1.45× Solid to 1.06-1.19×. The three variants
cannot be told apart here. Mount and update stay within run-to-run noise.

### What won and what lost

- **Won.** Dispose in every order at 64 observers, and root and creation-order dispose at 1024: 0.62-0.72× on .NET and
  0.38-0.43× on V8 (C). Wide relinking is 0.67-0.82× (C). `WriteWide` is 0.87-0.90× on V8. The browser dispose gap to
  Solid shrinks by roughly two thirds.
- **Lost: scattered removal on a wide source.** `DisposeScattered` 1024 is 1.55× on .NET and 1.07× on V8 (C). B's
  wrapped loop was 1.34× on V8, and C's plain loops bring that down. `RelinkOne` 1024 pays the same scan on every
  relink: 2.8× on .NET and 4.5× on V8, where A's loop from index 1 is 2.6×.
- **Lost: readers of many sources that change their read order.** `RotateReads` is 1.14-1.36× at 8 sources (a scan per
  append) and 2.4-2.9× at 64 and 1024 (an index allocated and filled each run, 56 B per source on .NET) on both
  runtimes. The cost comes from
  moving duplicate detection to the reader, and the removal variant does not change it. Master pays nothing here: each
  source's `Add` finds an empty list.
- **Small fan-out.** At 8 observers every case is within 0.87-1.05×, except `RelinkAll` 8 on .NET in the second pass
  (1.12×; A and B measured 1.02-1.05× in the first) and `RotateReads` 8.

### Recommendation

Scattered removal is not the only loss, so by the design's rule this does not become the single model. Proceed to step
2, indexed handles with findings 1-8 applied. Handles address both losses: removal by position makes scattered removal
and `RelinkOne` O(1), and the handle-only record answers a repeat read at the source in O(1), so the reader's scan or
index is needed only on a miss. Measure step 2 against C as well as master: C sets the bar for the dispose cases. C is
the fallback if step 2 stalls. Its losses are confined to a wide source losing one observer from the middle, and to a
reader of many sources that changes its order every run.

### Not verified

- The browser comparison covers the template component only.
- The `--trace-gc` processes report counts only. Their timing lines interleaved with GC output and were not parsed; the
  interleaved runs give the timings.
- Retention under Fable: the wide-source retention test is excluded under Fable, as before.
