# Journal guard measurement and optimization plan

> **For agentic workers:** Use superpowers:executing-plans or subagent-driven-development for implementation and independent acceptance before integration.

**Goal:** Meet every acceptance criterion of Ranvier issue209: recover cutoff, derive-effect and batched stream-burst-16/256 under Node jitless, with no other gated regression and unchanged journal behavior.

**Architecture:** Keep the public Signal constructor and journal semantics. First measure the existing hot-path guard using the canonical counter harness, then select a measured implementation. A cache is a candidate, not a pre-approved result: an extra node field or residual null test can still fail allocation or instruction gates.

**Tech stack:** F#, Fable, .NET10, Node26, Linux thread hardware counters, SageFs for pure behavioral experiments.

**Spec:** https://github.com/ranvier-org/Ranvier/issues/209 and docs/.ai/benchmarks/journal/README.md.

## Global constraints

- Recover all four specified jitless rows within the actual gate noise; no new Perf-Accepted waiver.
- Preserve exactly-once replay, every JournalReplayException case, normal/traced/Fable behavior and public API.
- List every changed untraced IL method in the PR. Keep emitted Fable differences and actual paired counter evidence.
- All gated rows matter, including allocation and construction; do not accept a steady-state improvement that merely moves the regression.
- Own worktree only, explicit project paths, no whole-solution FSAC check, no mutation of protected native build sites or shared daemon. Clean this checkout and owned processes after pushed source and reader release.

## Review focus

- Equal writes are still journal inputs: moving the journal hook after equality can corrupt replay.
- Ignored replay writes must leave value and notifications unchanged.
- Async settle/fail retains owner-thread dispatch, failure behavior and event ordering.
- Node construction and server-render allocations can regress when a cached recorder field is added.
- A different runtime branch must preserve trace attribution and the public Signal constructor contract.

## Current source and environment

This isolated branch starts at 6c5e44743b537831b1c1b011b7a1d371fc82c6a3. Bounded FCS Core outline reports no diagnostics. Core.fs Signal is lines3688–3836, AsyncSource3846–3972. Signal.Value checks graph.Journal before equality and calls out-of-line RecordWrite; AsyncSource.Settle/Fail check it inside graph.Post before out-of-line RecordSettle/RecordFail. Signal's internal primary/public secondary constructors do not provide a factory seam that can silently return a different concrete type.

Serena refuses this worktree as outside its activated primary project; the exact error was observed, so scoped FCS and bounded source reads are used here. Full FCS outline is automatically downgraded to headers above its response budget; do not claim a complete member inventory from that output.

WSL Ubuntu can open an own-thread instructions:u hardware counter without policy changes. Existing /home/shaya/.dotnet/dotnet has SDK10.0.403; /home/shaya/node26/bin/node is26.12.0. They are absent from non-login PATH. The perf CLI is absent but unnecessary for the direct-counter harness. Capability evidence is C:/wk/project2/wsl-instruction-counter-probe.json; this is not a benchmark result.

## Task1: Establish the exact measured baseline

Files: bench/Ranvier.Counters, bench/Ranvier.Counters.Tests, fable/Ranvier.Counters, existing build.fsx performance routes; new compact evidence under docs/.ai/benchmarks/journal209.

- [ ] Export exact source identities and the canonical gate base/threshold rules. Preserve both current-head and original pre-journal comparison identities; current accepted trailers do not redefine the target.
- [ ] Set up an owned WSL checkout/export with explicit runtime paths and valid Git metadata. Do not alter the Windows worktree's Git pointers or shared repositories to make WSL work.
- [ ] Use actual SageFs build/run leases before external dotnet commands. Baseline full counter/workflow tests pass before measurements; process/counter execution belongs to CLI, not the REPL.
- [ ] Build canonical normal .NET/Fable outputs and collect paired/interleaved measurements for the four target rows plus the complete gate matrix. Preserve instruction-counter availability in every measured region and separately scope x64 local versus required arm64 CI.
- [ ] Capture exact current emitted JS and untraced IL for the relevant hot paths. Keep compact logs and hashes; remove disposable outputs after no reader needs them.

## Task2: Select and prove an optimization

Primary file: src/Ranvier/Core.fs. Test ownership: existing journal/replay test modules, normal/traced/Fable runs. No public signature change is presumed.

- [ ] Measure a minimal cached-recorder candidate against the baseline; inspect construction/allocation and emitted JS as well as the four hot rows. This is an experiment, not acceptance merely because one row improves.
- [ ] If caching misses any threshold, retain the negative evidence and investigate a construction-time specialization that preserves public constructors. Before any public signature/type change, run fcs_refactor_impact and obtain a concrete compatible design review.
- [ ] Exercise equal writes, ignored replay writes, async settle/fail, trace attribution and every existing replay error case in the owning tests. Use Sage for pure RED/GREEN, persist, reset/rebuild and recheck actual source.
- [ ] Keep only the candidate that meets every specified row and causes no other gated regression; do not change expected values, benchmark work or gate policy to make it pass.

## Task3: Full acceptance and delivery

- [ ] Run the unfiltered normal/traced/Fable behavioral suites and canonical analyzer groups; no filtered run substitutes for acceptance.
- [ ] Compare the complete benchmark matrix and list changed untraced IL methods. Local x64 evidence does not replace canonical arm64-counter and x64-timing CI legs.
- [ ] Independent acceptance reviews source, counter regions, runtime/source identities and all issue209 criteria. Implementation continues elsewhere while CI runs.
- [ ] Merge and close only after full scoped evidence; archive the clean pushed worktree and stop its owned sessions/clients/processes. Retain compact reproducible evidence.

No product source has changed and no performance improvement is claimed by this preparation.
