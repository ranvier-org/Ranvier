# Keyed create and clear: where the time goes, and an ordered `ReleasedAll` — 2026-10-19

## Environment and commits

- Machine: AMD Ryzen 9 9900X (12 cores, 62 GB), Windows 11 10.0.26200. Rider open and idle; profiles and timings ran one at a time, with no build running alongside.
- Chromium 153.0.8010.12 (Playwright 1.63.0), Node 26.7.0, Fable 5.18.0 (`--noCache`, Release). Solid 2: `solid-js`, `@solidjs/web`, `@solidjs/compiler` 2.0.0-rc.14.
- Profile: `d264aed` (`poc/fable-ranvier-dom`, master `7853137` merged in), bundle `generated-keyed-profile` (unminified, `RANVIER_PERF_PROFILE`).
- Timings: before `d264aed`, bundle `generated-keyed-rel0`; after `d264aed` with the `ReleasedAll` change, bundle `generated-keyed-rel1`. Both bundles were built before the timings, and every JSON records `commit: d264aed` and the change as uncommitted.

Raw data in [`framework-performance/investigation/released-all/`](framework-performance/investigation/released-all/): `profile-buckets.json` (`keyed-profile-buckets.mjs`: buckets, functions by self and by total time) and `rel0-1..3.json`, `rel1-1..3.json` (one process each, alternating).

## Where the time goes

Loop protocol, 200 fresh lists per operation, 50 µs sampling, the samples inside `measuredRun`. Sampled ms per operation:

| Operation | Ranvier | Solid 2 |
|---|---|---|
| create1k | 3.72 | 2.69 |
| replace1k | 4.91 | 3.36 |
| clear1k | 0.68 | 0.45 |
| clear10k | 7.12 | 5.62 |

create1k:

| Function | Ranvier | Solid 2 |
|---|---|---|
| `cloneNode` | 0.92 | 0.82 |
| `insertBefore` | 0.38 (the staging fragment, once per pass) | 0.37 (each row) |
| `appendChild` | 0.32 (each row, into the staging fragment) | — |
| the node walk (`collect`; Solid 2's row function) | 0.39 | 0.37 |
| GC | 0.48 | 0.33 |
| core graph and projection pass | 0.54 | — |

- Each row enters the DOM twice: `RowBuilder` appends it to the region's staging fragment, and `Row.Layout` inserts the fragment before the region's end. Solid 2 inserts each row once.

clear10k:

| Function | Ranvier | Solid 2 |
|---|---|---|
| removing the region's nodes (`Row.ClearBetween`; Solid 2's `cleanChildren`) | 3.83 | 3.62 |
| row and owner disposal | 1.01 | 1.32 |
| `Row.ValidateWindow` over every row before the clear | 0.57 | — |
| `RegionSite.ReleasedAll` | 0.39 | — |
| `KeyMap.Find` and `KeyMap.Remove` in `Projection.RetireRemoved` | 0.40 | — |

- The removal itself costs about as much as Solid 2's. The extra time is the validation before the clear, the `ReleasedAll` guard, and the projection's lookup and removal of each retired key.
- The validation reports a foreign node between the region's markers instead of deleting it with the region's contents.

## The change

`RegionSite.ReleasedAll` compares the held rows with the live laid-out rows in one walk when they are in the same order, which holds for a clear of a list in creation order. Any other order compares through a set, as before.

## Results

Ranvier's median ms `[per-process medians]` and the median ratio to Solid 2, three processes per build, 3 warmups and 21 shuffled rounds, a forced GC before every sample:

| Operation | Before | After |
|---|---|---|
| create1k | 8.150 [8.17, 8.15, 8.09] 1.67× | 8.175 [7.96, 8.18, 8.25] 1.56× |
| clear1k | 1.550 [1.52, 1.69, 1.55] 1.87× | 1.555 [1.56, 1.64, 1.48] 1.37× |
| clear10k | 8.980 [8.98, 9.28, 8.94] 1.61× | 8.730 [8.57, 9.02, 8.73] 1.53× |
| remove1k | 0.285 [0.28, 0.29, 0.29] 0.94× | 0.270 [0.26, 0.28, 0.27] 1.17× |
| armsClear1k | 3.175 [3.18, 3.05, 3.24] 1.81× | 3.240 [2.89, 3.31, 3.24] 1.88× |

- Absolute times in this run are about 35% above the 2026-10-18 runs (create1k 8.15 ms against 6.04 ms), with Solid 2's slower too, so only ratios compare across days.
- clear10k is 0.25 ms faster and every per-process median is lower, but the ranges overlap. The profile predicts 0.39 ms.
- clear1k's and remove1k's ratios move with Solid 2's medians; Ranvier's own ranges overlap.

Tests: 380 Node DOM tests. New: a clear after a swap removes the rows in one operation. Both mutants are killed: the set comparison returning false (the new test) and the ordered comparison returning false (four clear tests).

## Remaining work

- The staging fragment's second insertion on create (0.32 ms of 3.72).
- The validation before an emptying pass's clear (0.57 ms of 7.12 at 10,000 rows).
- `Projection.RetireRemoved`'s lookup of each removed key (0.26 ms at 10,000 rows), a core change.

## Reproduction

From `examples/Fable.Ranvier.Playground`, one at a time:

```sh
node benchmarks/frameworks/keyed-rows.mjs --build --profile --repeats=200 --ops=create1k,replace1k,clear1k,clear10k --pairs=ranvier:solid --label=prof-x200 --save-cpu=<dir>/cpu --out=<dir>
node benchmarks/frameworks/keyed-profile-buckets.mjs <dir>/cpu generated-keyed-profile/assets/<bundle>.js.map --out=<dir>/buckets.json
RanvierCounters=false RANVIER_PERF_OUTPUT=generated-keyed-<bundle> dotnet fable tests/DomTests.fsproj --outDir generated-tests --configuration Release --noCache
RANVIER_PERF_OUTPUT=generated-keyed-<bundle> node node_modules/vite/bin/vite.js build --config benchmarks/frameworks/vite.config.mjs
node benchmarks/frameworks/keyed-rows.mjs --dist=generated-keyed-<bundle> --processes=1 --rounds=21 --shuffle --no-alloc --pairs=ranvier:solid --ops=create1k,clear1k,clear10k,remove1k,armsClear1k --label=<bundle>-<n> --out=<dir>
```
