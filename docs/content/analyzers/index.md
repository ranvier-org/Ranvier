---
title: Analyzers
order: 1
---

:::info
Preview: `Ranvier.Analyzers` is packed with the next release. Reference it and set `RanvierAnalyze`:

```xml
<PackageReference Include="Ranvier.Analyzers" Version="0.1.0-preview.1" PrivateAssets="all" />
<PropertyGroup>
  <RanvierAnalyze>true</RanvierAnalyze>
</PropertyGroup>
```

with the CLI in the tool manifest: `dotnet tool install fsharp-analyzers --version 0.41.0`.
:::

`Ranvier.Analyzers` reports Ranvier traps at build time, at the source range of the cause. Each code below links to its
cause, its fix and an example.

| Code | Title | Severity |
|---|---|---|
| [RAN001](ran001.md) | Fable inlines a snapshot of a module mutable | Warning |
| [RAN002](ran002.md) | Generated union case tester in a Fable library | Warning |
| [RAN010](ran010.md) | Owned node created in a pure derivation | Error |
| [RAN011](ran011.md) | Async value created and read in the owner that replaces it | Warning |
| [RAN012](ran012.md) | Reactive read after the first await of an async memo | Warning |
| [RAN013](ran013.md) | Signal write or Pump on a thread-pool callback | Warning |
| [RAN020](ran020.md) | Reactive read at template build time | Warning |
| [RAN021](ran021.md) | Unstable list key | Warning / Info |
| [RAN022](ran022.md) | Definition created on every region read | Warning |
| [RAN023](ran023.md) | Invalid content parts on an element | Error |

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

## Columns

The CLI's console output, and the `dotnet build` warnings made from it, count columns from 0: `B.fs(4,8)` is the
identifier at the ninth character of line 4. The SARIF report, and the CI annotations made from it, count from 1.
