---
title: Concepts
order: 1
---

Understand dependency tracking, pending state and ownership here. For API walkthroughs, start
with the [guide](../guide/index.md).

Ranvier is a fine-grained reactive computation library for .NET and Fable, written in F#. It provides a dependency graph
of **signals** (settable sources), **memos** (cached derived values) and **effects** (side effects that re-run
when something they read changes). Dependencies are tracked automatically: a computation depends on whatever
it read during its last run ([`run_end_edges_are_latest_reads`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Runs.html#run_end_edges_are_latest_reads)). Derived values pull upstream dependencies on demand so that for well-behaved synchronous graphs (`well_behaved_program`), a computation never observes a mix of old and new inputs ([`glitch_free_read`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.WholeGraph.html#glitch_free_read), [`convergence`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.WholeGraph.html#convergence)). An equality cutoff stops propagation when a recomputed value is equal to the previous
one ([`cutoff`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Runs.html#cutoff), [`resolve_clean_unmoved`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Check.html#resolve_clean_unmoved)). See the [Rocq & Iris proof explorer](https://ranvier-org.github.io/Ranvier/proofs/) for the full set of machine-checked guarantees and exact scope premises.

The **pending channel** carries not-ready and failed status from an async source through dependent
computations without explicit option plumbing until a boundary handles it; see
[Async and pending](../guide/async-and-pending.md) for usage and [Suspension](suspension.md) for
the mechanics.

## Pages in this section

- [Suspension](suspension.md) covers what happens when a computation reads a value that is not ready. It
  explains the pending channel, why a read throws instead of returning a marker, how boundaries stop the
  channel with a fallback, and how errors and recovery use the same path.
- [The async graph on .NET](async-graph.md) covers tasks as computation results, superseded flights, thread
  ownership and dispatch, disposal, and what changes when the same core compiles to JavaScript through
  Fable.
- [Contracts](contracts.md) states which threads may touch a graph, what a failure does to the nodes that
  read it, and who owns a node created inside a computation.
- [Ecosystem](ecosystem.md) covers where Ranvier sits beside FSharp.Data.Adaptive, SignalsDotnet, R3 and
  System.Reactive, Fable.Ripple and Solid 2, and when one of those is the better choice.
- [Roadmap](roadmap.md) lists implemented core, serialization, DOM, verification, and exploratory areas.

## Reading design notes

**Implemented** describes current behavior; **considered and rejected** explains an evaluated
alternative; **exploratory** marks ideas unavailable to use. Stopwatch measurements describe
one machine and one path, rather than application performance or comparisons with other libraries.
