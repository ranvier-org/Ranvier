---
title: Installation
order: 2
---

Choose the package for your language or host, then continue with [Getting started](getting-started.md).

## Supported targets

:::center
|**Target**|Supported|Recommended|
|---:|:---:|:---:|
| **`net10`** | Yes | Yes |
| `netstandard2.1` | Yes | No |
:::

Applications on .NET 8 and .NET 9 use the `netstandard2.1` build, which omits
`TimedClock.ofTimeProvider` and carries no trimming or Native AOT annotations.

## From NuGet

The guide describes the current source. Packable libraries here carry `0.2.3` (`Ranvier`,
`Ranvier.CSharp`, `Ranvier.Elmish`, `Ranvier.Query`, `Ranvier.Wire`) or `0.1.1` (`Ranvier.Dom`,
`Fable.Ranvier.Dom`), each with a `.Traced` variant (`Ranvier.Mcp` is source/tooling only).

| Package | Version | Purpose | Guide |
| --- | --- | --- | --- |
| `Ranvier` | `0.2.3` | Core reactive graph, signals, memos, async, collections | [Getting started](getting-started.md) |
| `Ranvier.CSharp` | `0.2.3` | C# delegates, LINQ collections, XAML bindings, commands | [C#](csharp.md) |
| `Ranvier.Elmish` | `0.2.3` | MVU bridge (`Mvu`) | [Migrating from Elmish](elmish.md) |
| `Ranvier.Query` | `0.2.3` | Typed reactive query cache, leases, mutations | [Queries](queries.md) |
| `Ranvier.Wire` | `0.2.3` | Cross-target JSON wire codecs (.NET + Fable) | — |
| `Ranvier.Dom` | `0.1.1` | Shared WebKit-derived DOM contracts (.NET host) | — |
| `Fable.Ranvier.Dom` | `0.1.1` | Browser DOM contracts sharing `Ranvier.Dom` sources | [Fable](../fable/index.md) |

::::::tabs
:::::tab Core
::::tabs
:::tab NuGet

```bash
dotnet add package Ranvier
```

:::

:::tab Package reference

```xml
<PackageReference Include="Ranvier" Version="0.2.3" />
```

:::

::::
:::::
:::::tab C#


::::tabs
:::tab NuGet

```bash
dotnet add package Ranvier.CSharp
```
:::
:::tab Package reference

```xml
<PackageReference Include="Ranvier.CSharp" Version="0.2.3" />
```
:::
::::
C# projects use `Ranvier.CSharp`; see [C#](csharp.md).

:::::
:::::tab Elmish
::::tabs
:::tab NuGet

```bash
dotnet add package Ranvier.Elmish
```

:::

:::tab Package reference

```xml
<PackageReference Include="Ranvier.Elmish" Version="0.2.3" />
```

:::
::::

The MVU bridge, `Mvu`, ships as its own package, `Ranvier.Elmish`, which depends on `Ranvier`; see
[Migrating from Elmish](elmish.md).

:::::
::::::

For typed reactive queries and mutation reconciliation, install `Ranvier.Query` with
`dotnet add package Ranvier.Query`; see [Queries](queries.md).

## Views

The view packages, `Ranvier.View`, `Ranvier.View.Dom` and `Ranvier.View.Anime`, and the
`Ranvier.Analyzers` package are built in
[ranvier-org/Ranvier.View](https://github.com/ranvier-org/Ranvier.View). Their
[documentation](https://ranvier-org.github.io/Ranvier.View/) covers installation, the additional
`Xantham.*` package feed, templates, server rendering, motion and the analyzer rules. `Fable.Ranvier.Anime`, the anime.js bindings, is built in
[ranvier-org/Ranvier.Anime](https://github.com/ranvier-org/Ranvier.Anime).

## Typed CSS

`Ranvier.Css`, `Ranvier.Css.Bindings`, `Ranvier.Css.View` and `Ranvier.Css.Build` are built in
[ranvier-org/Ranvier.Css](https://github.com/ranvier-org/Ranvier.Css). See
[typed CSS](https://ranvier-org.github.io/Ranvier.Css/view/css/),
[composite utilities](https://ranvier-org.github.io/Ranvier.Css/view/css-utilities/) and
[extracting CSS](https://ranvier-org.github.io/Ranvier.Css/view/css-build/).

## Persistence

`Ranvier.Persistence` and its store adapters, `Ranvier.Persistence.Marten` and
`Ranvier.Persistence.KurrentDB`, are built in
[ranvier-org/Ranvier.Persistence](https://github.com/ranvier-org/Ranvier.Persistence). See
[persisted inputs](https://ranvier-org.github.io/Ranvier.Persistence/guide/persistence/).

## Fable

`Ranvier`, `Ranvier.CSharp`, `Ranvier.Elmish`, `Ranvier.Query`, `Ranvier.Wire`, `Fable.Ranvier.Dom`,
the view packages, and their `.Traced` variants include Fable sources and a standalone
`fable/<package>.fsproj`. Reference the NuGet package normally from a Fable project compiled with
`dotnet fickl`. See [Fable (JavaScript)](../fable/index.md#package-sources-and-ci) for target
differences and verification.

## Native AOT and trimming

All packable runtime libraries (`Ranvier`, `Ranvier.CSharp`, `Ranvier.Elmish`, `Ranvier.Query`,
`Ranvier.Wire`, and `Ranvier.Dom`, plus their `.Traced` variants) support trimming and Native AOT on
`net10.0`. CI publishes traced and untraced smoke applications on .NET 10 for `linux-x64` and treats
trim and AOT warnings as errors. Publish with `<PublishAot>true</PublishAot>`. The view packages
carry no trimming or Native AOT annotations.

Trace payload previews rely on each application type's `ToString` implementation (arrays/collections
preview up to 64 elements, 4 levels deep, within 60 characters per line); override `ToString` on
custom records or unions to preserve diagnostic text when trimmed.

## Traced packages

Runtime libraries have a `.Traced` variant (`<PackageId>.Traced`); `Ranvier.Analyzers` does not. See
[Tracing](tracing.md#from-nuget) for switching between traced and untraced builds.
