namespace CounterBench

/// <summary>A finite per-operation differential; unavailable metrics are omitted.</summary>
type Metric = { Name: string; Value: float }

/// <summary>A named version, warning, or unavailable-baseline reason.</summary>
type Label = { Name: string; Value: string }

/// <summary>One scenario and engine, with flags for collection and compilation in either measured region.</summary>
type ScenarioFigures =
    {
        Scenario: string
        Engine: string
        Gc: bool
        /// <summary>V8 compiled inside either region. Always false for .NET, and in files that predate the field.</summary>
        Compiled: bool
        Metrics: Metric[]
    }

/// <summary>
/// One process of a side (<c>base</c> or <c>head</c>) and runtime (<c>node</c>, <c>node-jitless</c> or <c>dotnet</c>).
/// </summary>
type ProcessFigures =
    {
        Side: string
        Commit: string
        Runtime: string
        Process: int
        Scenarios: ScenarioFigures[]
    }

/// <summary>Alternating process measurements and separate instrumented library counters.</summary>
type AbRun =
    {
        Leg: string
        Machine: string
        Base: string
        Head: string
        Rounds: int
        Versions: Label[]
        NoBaseline: Label[]
        Processes: ProcessFigures[]
        Counters: ProcessFigures[]
    }

/// <summary>The runtimes of an AB artifact, and which of them the verdict gates.</summary>
module Runtimes =
    /// <summary>The Fable harness under tiered V8.</summary>
    [<Literal>]
    let Node = "node"

    /// <summary>The Fable harness under <c>node --jitless</c>.</summary>
    [<Literal>]
    let NodeJitless = "node-jitless"

    [<Literal>]
    let Dotnet = "dotnet"

    /// <summary>
    /// The runtimes a leg measures, in measurement order. Only <c>arm64-counters</c> measures <c>node-jitless</c>.
    /// </summary>
    let measured (leg: string) =
        if leg = "arm64-counters" then
            [ Node; NodeJitless; Dotnet ]
        else
            [ Node; Dotnet ]

    /// <summary>
    /// Whether a runtime has a separate library-counter run. <c>node-jitless</c> runs the code of <c>node</c>, so its
    /// library counters are those of <c>node</c>.
    /// </summary>
    let instrumented (runtime: string) =
        runtime <> NodeJitless

    /// <summary>
    /// Whether the gate metric of a runtime can fail a gating leg. Tiered Node figures depend on JIT history, which
    /// objects an earlier run left alive, so they are reported and not gated; <c>node-jitless</c> is the gated Node
    /// runtime.
    /// </summary>
    let gated (runtime: string) =
        runtime <> Node
