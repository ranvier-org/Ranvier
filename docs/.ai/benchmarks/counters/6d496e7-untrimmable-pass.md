# Counter bench, untrimmable keyed pass (6d496e7 against 86d812f)

- Date: 2026-10-15
- Machine: AMD64 Family 26 Model 68 Stepping 0, AuthenticAMD, 24 logical processors, Microsoft Windows 10.0.26200, .NET 10.0.13, Node.js v26.7.0
- Change: `6d496e7` perf(core): take the full visit when a keyed pass cannot trim
- Base: `86d812f`, which is `bef1521` (master) with only the keyed-pass scenarios added; the engine is identical to `bef1521`
- Command, run from each worktree: `./counters.ps1 -Repeat 5 -Only keyed-`, elevated, untraced
- Order: base, change, base, change. Each figure is the mean of the two rounds' medians; each round's median is over 5 processes
- Node.js instructions and cycles are main-thread figures
- Node.js bytes/op come from one extra run of each plain harness with `--max-semi-space-size=512`, so no region collects; the counter runs collected inside the fresh-item regions of the base build and report negative bytes there
- Every figure is (m(2N) - m(N)) / N at N = 50. Differences under 5 % are within run-to-run noise. Cycles vary more than instructions between runs

## Scenarios

A projection of 1,000 rows keyed by `Id`, mapped to `Value`, with one effect per row and one effect on `Keys`. An operation writes the next list; operations alternate between the edited list and the initial list.

- `keyed-remove`: remove the middle row, then restore it
- `keyed-swap`: swap the second and second-to-last rows, then swap them back
- `keyed-insert`: prepend a row with a new key, then remove it

**fresh items**: every write holds new item records (`{ item with Id = item.Id }`), so under the default `===` policy every position differs from the previous pass. **stable items**: every write is a new array of the initial records, so the prefix and suffix trim.

## Results

| Runtime | Scenario | Items | instr/op base | instr/op change | ratio | cycles/op base | cycles/op change | ratio | br-miss/op base | br-miss/op change | bytes/op base | bytes/op change | ratio |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| .NET | keyed-remove | fresh | 1,954,333 | 1,558,138 | 0.80 | 693,802 | 567,536 | 0.82 | 232.6 | 152.4 | 8,516 | 8,516 | 1.00 |
| .NET | keyed-remove | stable | 68,776 | 68,677 | 1.00 | 17,108 | 27,064 | 1.58 | 1.7 | -3.8 | 8,516 | 8,516 | 1.00 |
| .NET | keyed-swap | fresh | 1,991,595 | 1,605,284 | 0.81 | 705,388 | 609,892 | 0.86 | 185.4 | 117.2 | 8,216 | 8,216 | 1.00 |
| .NET | keyed-swap | stable | 71,828 | 67,954 | 0.95 | 28,433 | 24,405 | 0.86 | 7.3 | 19.1 | 8,216 | 8,216 | 1.00 |
| .NET | keyed-insert | fresh | 1,952,513 | 1,558,823 | 0.80 | 694,984 | 626,003 | 0.90 | 108.2 | 173.8 | 8,524 | 8,524 | 1.00 |
| .NET | keyed-insert | stable | 79,560 | 79,641 | 1.00 | 27,351 | 26,881 | 0.98 | 6.2 | 10.8 | 8,524 | 8,524 | 1.00 |
| Node | keyed-remove | fresh | 5,831,403 | 3,681,324 | 0.63 | 2,457,006 | 784,971 | 0.32 | 16,379.4 | -55.8 | 573,491 | 340,950 | 0.59 |
| Node | keyed-remove | stable | 711,505 | 685,242 | 0.96 | 158,185 | 53,186 | 0.34 | 119.8 | -319.1 | 19,168 | 19,145 | 1.00 |
| Node | keyed-swap | fresh | 5,570,864 | 3,625,996 | 0.65 | 1,807,557 | 1,064,576 | 0.59 | 7,969.3 | 1,436.3 | 668,591 | 368,915 | 0.55 |
| Node | keyed-swap | stable | 904,335 | 229,928 | 0.25 | 332,732 | 44,904 | 0.13 | 1,330.3 | -5.9 | 19,294 | 19,258 | 1.00 |
| Node | keyed-insert | fresh | 5,841,732 | 4,075,370 | 0.70 | 2,804,601 | 1,475,406 | 0.53 | 15,988.7 | 4,632.5 | 573,503 | 341,682 | 0.60 |
| Node | keyed-insert | stable | 732,856 | 754,191 | 1.03 | 150,524 | 162,309 | 1.08 | 51.3 | 54.1 | 19,165 | 19,145 | 1.00 |

## Reading

- Fresh items: instructions fall by 19-20 % on .NET and by 30-37 % on Node.js. On Node.js, allocations fall by 40-45 %. The pass skips the per-position hash-set bookkeeping of the trim and runs the full visit.
- Stable items: instructions are within noise on .NET (swap 0.95, at the noise bound) and on Node.js for remove and insert. The stable swap under Node.js runs 4x fewer instructions on the change in both rounds; the trimmed path executes the same work, and the difference was not traced further.
- Library counters (MemoRecomputes, EffectRuns, edges) are identical between base and change in every scenario.

The tool's JSON output for round 1: [change](6d496e7-untrimmable-pass.json), [base](6d496e7-untrimmable-pass-base.json).
