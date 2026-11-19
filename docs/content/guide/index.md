---
title: Overview
order: 1
---

Build reactive state for .NET and Fable: update a value, and the computations that depend on it update too.

* **Signals** hold values you can read and write.
* **Memos** derive values from tracked reads.
* **Effects** run when a value they read changes.
* **Bindings** copy one fixed signal or memo into an external target.

:::tip Async state travels through the graph
A node waiting on an async dependency is **pending**. Its readers can report loading, keep a
previous result, or use a boundary's fallback. This pending channel follows Solid 2 and is
independent of whether a value is out of date.
:::

## Choose a starting point

- **Setup & basics:** [Installation](installation.md) and [Getting started](getting-started.md) cover NuGet packages, target frameworks, and your first graph. [Examples](examples.md) and the [desktop comparison](../blog/desktop-adaptive.md) show full apps.
- **Core graph & lifetimes:** [Graphs](graph.fsx), [Signals](signals.fsx), [Memos](memos.md), [Effects](effects.md), [Bindings](bindings.md), [Equality](equality.md), [Roots](roots.md), [Cleanup](cleanup.md), [Untrack](untrack.md), and [Batch](batch.md).
- **Async work & scheduling:** [Async and pending](async-and-pending.md) covers requests, streams, boundaries, loading/errors, and dispatch; [Debounce and throttle](timing.md) control admission.
- **Collections & forms:** [Collections](collections.fsx) and [Projections](projections.fsx) cover keyed sources, incremental views, lookups, selectors, and [Aggregates](aggregates.fsx); [Editable values](editable.md), [Drafts](drafts.md), and [Forms](forms.md) manage local edits.
- **Diagnostics & hosts:** [Tracing](tracing.md), [Input journal and replay](journal.md), [Queries and mutations](queries.md), [MCP inspection](mcp.md), [C#](csharp.md), [Blazor Server](blazor-server.md), and [Migrating from Elmish](elmish.md).
- **UI & persistence:** [Views](https://ranvier-org.github.io/Ranvier.View/view/), [Typed CSS](https://ranvier-org.github.io/Ranvier.Css/), and [Persisted inputs](https://ranvier-org.github.io/Ranvier.Persistence/guide/persistence/) live in sibling repositories.

This guide describes the current source (pre-1.0); see [Roadmap](../concepts/roadmap.md) for active and planned work.
