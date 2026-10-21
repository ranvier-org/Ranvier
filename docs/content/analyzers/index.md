---
title: Analyzers
order: 1
---

:::info
Preview: the analyzers run in this repository and ship in `Ranvier.Analyzers` after phase 1.
:::

`Ranvier.Analyzers` reports Ranvier traps at build time, at the source range of the cause. Each code below links to its
cause, its fix and an example.

| Code | Title | Severity |
|---|---|---|
| [RAN002](ran002.md) | Generated union case tester in a Fable library | Warning |

## Running the analyzers

In this repository, after `dotnet build Ranvier.slnx -c Release`:

```bash
dotnet fsi build.fsx -- analyze
```

The command builds the analyzers, runs them over the repository's projects with every code promoted to an error, and
writes `artifacts/analyzers/ranvier.sarif`. A project that sets `<RanvierAnalyze>true</RanvierAnalyze>` reports them in
its own `dotnet build` output after each compile.

## Suppression

Write the reason on the line before the SDK's comment:

```fsharp
// The snapshot is read before any write, so Fable's inlining leaves the value unchanged.
// fsharpanalyzer: ignore-line-next RAN001
let setups = Loops.childSetups
```

`ignore-line`, `ignore-region-start`/`ignore-region-end` and `ignore-file` take the same codes.
