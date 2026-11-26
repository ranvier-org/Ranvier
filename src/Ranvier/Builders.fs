namespace Ranvier.Internal

open System
open System.Collections.Generic
open System.ComponentModel
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open Ranvier

/// <summary>Phantom phase marker indicating a computation-expression step whose body has not executed yet.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type ColdPhase = struct end

/// <summary>Phantom phase marker indicating a computation-expression step rooted in a reactive bind.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type BoundPhase = struct end

/// <summary>Phantom leaf marker indicating an <c>effect</c> step eligible for static <c>EffectOn</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type IsLeaf = struct end

/// <summary>Phantom leaf marker indicating an <c>effect</c> step requiring dynamic <c>Effect</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type NotLeaf = struct end

/// <summary>Phantom scope mode marker for pure memo computation expressions.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type PureMode = struct end

/// <summary>Phantom scope mode marker for owning memo computation expressions.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type OwningMode = struct end

/// <summary>Phantom mode marker for option-deduplicating memo computation expressions.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type OptionMode = struct end

/// <summary>Phantom marker indicating that no <c>previous</c> context token was bound.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type NoPrev = struct end

/// <summary>Phantom marker indicating that a <c>previous</c> or <c>previousOr</c> token was bound.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type WithPrev = struct end

/// <summary>Phantom marker indicating an <c>effect</c> expression returning <c>unit</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type UnitOut = struct end

/// <summary>Phantom marker indicating an <c>effect</c> expression returning <c>INode</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type NodeOut = struct end

/// <summary>Phantom marker indicating no <c>comparer</c> header operation was supplied.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type NoCmp = struct end

/// <summary>Phantom marker carrying an explicit output <c>IEqualityComparer</c> from a <c>comparer</c> header operation.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type WithCmp<'U> =
    val Comparer: IEqualityComparer<'U>
    new(comparer: IEqualityComparer<'U>) = { Comparer = comparer }

/// <summary>Phantom marker indicating no <c>pending</c> header operation on <c>boundary</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type NoPending = struct end

/// <summary>Phantom marker carrying the <c>pending</c> fallback handler on <c>boundary</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type WithPending<'U> =
    val OnPending: 'U voption -> 'U
    new(onPending: 'U voption -> 'U) = { OnPending = onPending }

/// <summary>Phantom marker indicating no <c>failed</c> header operation on <c>boundary</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type NoFailed = struct end

/// <summary>Phantom marker carrying the <c>failed</c> recovery handler on <c>boundary</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type WithFailed<'U> =
    val OnFailed: exn -> 'U voption -> 'U
    new(onFailed: exn -> 'U voption -> 'U) = { OnFailed = onFailed }

/// <summary>Context token bound inside <c>memo</c> to read the previous value as <c>'U voption</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type MemoPreviousToken = struct end

/// <summary>Context token bound inside <c>memo</c> to read the previous value with fallback seed.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type MemoPreviousOrToken<'U> =
    val Seed: 'U
    new(seed: 'U) = { Seed = seed }

/// <summary>Context token bound inside <c>editable</c> to read the seed's previous value as <c>'U voption</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type EditablePreviousToken = struct end

/// <summary>Context token bound inside <c>editable</c> to read the seed's previous value with fallback seed.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type EditablePreviousOrToken<'U> =
    val Seed: 'U
    new(seed: 'U) = { Seed = seed }

/// <summary>Context token bound inside <c>draft</c> to read the seed's previous value as <c>'U voption</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type DraftPreviousToken = struct end

/// <summary>Context token bound inside <c>draft</c> to read the seed's previous value with fallback seed.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type DraftPreviousOrToken<'U> =
    val Seed: 'U
    new(seed: 'U) = { Seed = seed }

/// <summary>Phantom marker indicating no explicit <c>FlightPolicy</c> header operation on <c>asyncMemo</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type DefaultPolicy = struct end

/// <summary>Phantom marker carrying an explicit <c>FlightPolicy</c> header operation on <c>asyncMemo</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type ExplicitPolicy =
    val Policy: FlightPolicy
    new(policy: FlightPolicy) = { Policy = policy }

/// <summary>Context token bound inside <c>asyncMemo</c> to obtain <c>Previous&lt;'U&gt;</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type AsyncPreviousToken = struct end

/// <summary>Context token bound inside <c>asyncMemo</c> to obtain the flight <c>CancellationToken</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type AsyncCancellationToken = struct end

/// <summary>Phantom mode marker indicating latest-item mode on <c>stream</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type LatestMode = struct end

/// <summary>Phantom mode marker carrying seed and folder from <c>fold</c> on <c>stream</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type FoldMode<'S, 'T> =
    val Seed: 'S
    val Folder: 'S -> 'T -> 'S
    new(seed: 'S, folder: 'S -> 'T -> 'S) = { Seed = seed; Folder = folder }

/// <summary>Context token bound inside <c>stream</c> to obtain the stream <c>CancellationToken</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type StreamCancellationToken = struct end

#if FABLE_COMPILER
module private CeEvalSlot =
    let mutable evalDepth = 0
    let mutable hasPrev = false
    let mutable prevValue: obj = null
    let mutable effectGraph: Graph = Unchecked.defaultof<Graph>
    let mutable effectObserver: obj = null
    let mutable phase1Pending: exn = null
    let mutable hasReactiveBind = false
    let mutable isOwning = false
    let mutable optionDedup: obj = null
    let mutable isProbing = false
#endif

/// <summary>Tracks reentrant reactive computation-expression evaluation state on the current thread.</summary>
[<AbstractClass; Sealed; EditorBrowsable(EditorBrowsableState.Never)>]
type InternalCeState =
#if FABLE_COMPILER
    /// <summary>True when executing inside an active dynamic computation-expression evaluation.</summary>
    static member IsEvaluating: bool = CeEvalSlot.evalDepth > 0

    /// <summary>Enters one dynamic computation-expression evaluation level and returns the previous depth.</summary>
    static member EnterEval() : int =
        let prev = CeEvalSlot.evalDepth
        CeEvalSlot.evalDepth <- prev + 1
        prev

    /// <summary>Restores the evaluation depth saved by <c>EnterEval</c> or <c>SuspendForNestedRun</c>.</summary>
    static member ExitEval(prev: int) : unit =
        CeEvalSlot.evalDepth <- prev

    /// <summary>Resets evaluation depth to 0 while constructing a nested node and returns the saved depth.</summary>
    static member SuspendForNestedRun() : int =
        let prev = CeEvalSlot.evalDepth
        CeEvalSlot.evalDepth <- 0
        prev

    static member SwapPrevious<'U>(prev: 'U voption) : struct (bool * obj) =
        let saved = struct (CeEvalSlot.hasPrev, CeEvalSlot.prevValue)

        match prev with
        | ValueSome v ->
            CeEvalSlot.hasPrev <- true
            CeEvalSlot.prevValue <- box v
        | ValueNone ->
            CeEvalSlot.hasPrev <- false
            CeEvalSlot.prevValue <- null

        saved

    static member SuspendPrevious() : struct (bool * obj) =
        let saved = struct (CeEvalSlot.hasPrev, CeEvalSlot.prevValue)
        CeEvalSlot.hasPrev <- false
        CeEvalSlot.prevValue <- null
        saved

    static member RestorePrevious(saved: struct (bool * obj)) : unit =
        let struct (hp, pv) = saved
        CeEvalSlot.hasPrev <- hp
        CeEvalSlot.prevValue <- pv

    static member ReadPrevious<'U>() : 'U voption =
        if CeEvalSlot.hasPrev then
            ValueSome (unbox<'U> CeEvalSlot.prevValue)
        else
            ValueNone

    static member ReadPreviousOr<'U>(seed: 'U) : 'U =
        if CeEvalSlot.hasPrev then
            unbox<'U> CeEvalSlot.prevValue
        else
            seed

    static member CurrentEffectGraph: Graph = CeEvalSlot.effectGraph

    static member CurrentEffectObserver: obj = CeEvalSlot.effectObserver

    static member SwapEffectObserver(g: Graph, obs: obj) : struct (Graph * obj) =
        let prev = struct (CeEvalSlot.effectGraph, CeEvalSlot.effectObserver)
        CeEvalSlot.effectGraph <- g
        CeEvalSlot.effectObserver <- obs
        prev

    static member RestoreEffectObserver(saved: struct (Graph * obj)) : unit =
        let struct (g, obs) = saved
        CeEvalSlot.effectGraph <- g
        CeEvalSlot.effectObserver <- obs

    static member CurrentPhase1Pending: exn = CeEvalSlot.phase1Pending

    static member SetPhase1Pending(ex: exn) : unit =
        CeEvalSlot.phase1Pending <- ex

    static member SwapPhase1Pending(ex: exn) : exn =
        let prev = CeEvalSlot.phase1Pending
        CeEvalSlot.phase1Pending <- ex
        prev

    static member HasReactiveBind: bool = CeEvalSlot.hasReactiveBind

    static member SwapReactiveBind(value: bool) : bool =
        let prev = CeEvalSlot.hasReactiveBind
        CeEvalSlot.hasReactiveBind <- value
        prev

    static member IsOwning: bool = CeEvalSlot.isOwning

    static member SwapOwning(value: bool) : bool =
        let prev = CeEvalSlot.isOwning
        CeEvalSlot.isOwning <- value
        prev

    static member CurrentOptionDedup: obj = CeEvalSlot.optionDedup

    static member SwapOptionDedup(dedup: obj) : obj =
        let prev = CeEvalSlot.optionDedup
        CeEvalSlot.optionDedup <- dedup
        prev

    static member IsProbing: bool = CeEvalSlot.isProbing

    static member SwapProbing(value: bool) : bool =
        let prev = CeEvalSlot.isProbing
        CeEvalSlot.isProbing <- value
        prev
#else
    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private evalDepth: int

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private hasPrev: bool

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private prevValue: obj

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private effectGraph: Graph

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private effectObserver: obj

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private phase1Pending: exn

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private hasReactiveBind: bool

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private isOwning: bool

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private optionDedup: obj

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private isProbing: bool

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member private GetIsEvaluating() : bool =
        InternalCeState.evalDepth > 0

    /// <summary>True when executing inside an active dynamic computation-expression evaluation.</summary>
    static member IsEvaluating: bool = InternalCeState.GetIsEvaluating ()

    /// <summary>Enters one dynamic computation-expression evaluation level and returns the previous depth.</summary>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member EnterEval() : int =
        let prev = InternalCeState.evalDepth
        InternalCeState.evalDepth <- prev + 1
        prev

    /// <summary>Restores the evaluation depth saved by <c>EnterEval</c> or <c>SuspendForNestedRun</c>.</summary>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member ExitEval(prev: int) : unit =
        InternalCeState.evalDepth <- prev

    /// <summary>Resets evaluation depth to 0 while constructing a nested node and returns the saved depth.</summary>
    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member SuspendForNestedRun() : int =
        let prev = InternalCeState.evalDepth
        InternalCeState.evalDepth <- 0
        prev

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member SwapPrevious<'U>(prev: 'U voption) : struct (bool * obj) =
        let saved = struct (InternalCeState.hasPrev, InternalCeState.prevValue)

        match prev with
        | ValueSome v ->
            InternalCeState.hasPrev <- true
            InternalCeState.prevValue <- box v
        | ValueNone ->
            InternalCeState.hasPrev <- false
            InternalCeState.prevValue <- null

        saved

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member SuspendPrevious() : struct (bool * obj) =
        let saved = struct (InternalCeState.hasPrev, InternalCeState.prevValue)
        InternalCeState.hasPrev <- false
        InternalCeState.prevValue <- null
        saved

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member RestorePrevious(saved: struct (bool * obj)) : unit =
        let struct (hp, pv) = saved
        InternalCeState.hasPrev <- hp
        InternalCeState.prevValue <- pv

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member ReadPrevious<'U>() : 'U voption =
        if InternalCeState.hasPrev then
            ValueSome (unbox<'U> InternalCeState.prevValue)
        else
            ValueNone

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member ReadPreviousOr<'U>(seed: 'U) : 'U =
        if InternalCeState.hasPrev then
            unbox<'U> InternalCeState.prevValue
        else
            seed

    static member CurrentEffectGraph: Graph = InternalCeState.effectGraph

    static member CurrentEffectObserver: obj = InternalCeState.effectObserver

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member SwapEffectObserver(g: Graph, obs: obj) : struct (Graph * obj) =
        let prev = struct (InternalCeState.effectGraph, InternalCeState.effectObserver)
        InternalCeState.effectGraph <- g
        InternalCeState.effectObserver <- obs
        prev

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member RestoreEffectObserver(saved: struct (Graph * obj)) : unit =
        let struct (g, obs) = saved
        InternalCeState.effectGraph <- g
        InternalCeState.effectObserver <- obs

    static member CurrentPhase1Pending: exn = InternalCeState.phase1Pending

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member SetPhase1Pending(ex: exn) : unit =
        InternalCeState.phase1Pending <- ex

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member SwapPhase1Pending(ex: exn) : exn =
        let prev = InternalCeState.phase1Pending
        InternalCeState.phase1Pending <- ex
        prev

    static member HasReactiveBind: bool = InternalCeState.hasReactiveBind

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member SwapReactiveBind(value: bool) : bool =
        let prev = InternalCeState.hasReactiveBind
        InternalCeState.hasReactiveBind <- value
        prev

    static member IsOwning: bool = InternalCeState.isOwning

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member SwapOwning(value: bool) : bool =
        let prev = InternalCeState.isOwning
        InternalCeState.isOwning <- value
        prev

    static member CurrentOptionDedup: obj = InternalCeState.optionDedup

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member SwapOptionDedup(dedup: obj) : obj =
        let prev = InternalCeState.optionDedup
        InternalCeState.optionDedup <- dedup
        prev

    static member IsProbing: bool = InternalCeState.isProbing

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member SwapProbing(value: bool) : bool =
        let prev = InternalCeState.isProbing
        InternalCeState.isProbing <- value
        prev
#endif

/// <summary>Pairs a reactive source with an explicit Phase-1 key equality comparer.</summary>
#if FABLE_COMPILER
[<Sealed; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type ComparedReactive<'K>(source: IReactive<'K>, comparer: IEqualityComparer<'K>) =
    do
        if isNull (box source) then
            nullArg (nameof source)

        if isNull (box comparer) then
            nullArg (nameof comparer)

    /// <summary>The wrapped reactive source.</summary>
    member _.Source = source
    /// <summary>The custom Phase-1 key comparer.</summary>
    member _.Comparer = comparer

    interface IReactive<'K> with
        member _.Graph = source.Graph

        member _.ReadTracked() =
            source.ReadTracked ()
#else
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type ComparedReactive<'K> =
    val private source: IReactive<'K>
    val private comparer: IEqualityComparer<'K>

    /// <summary>Creates a compared wrapper validating non-null <c>source</c> and <c>comparer</c>.</summary>
    new(source: IReactive<'K>, comparer: IEqualityComparer<'K>) =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull (box comparer) then
            nullArg (nameof comparer)

        { source = source; comparer = comparer }

    /// <summary>The wrapped reactive source.</summary>
    member this.Source = this.source
    /// <summary>The custom Phase-1 key comparer.</summary>
    member this.Comparer = this.comparer

    interface IReactive<'K> with
        member this.Graph = this.source.Graph

        member this.ReadTracked() =
            this.source.ReadTracked ()
#endif

/// <summary>Pairs two same-graph signals for static <c>Signal.map2</c> dispatch.</summary>
#if FABLE_COMPILER
[<Sealed; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type MergedSignals<'A, 'B>(source1: Signal<'A>, source2: Signal<'B>) =
    do
        if isNull (box source1) then
            nullArg (nameof source1)

        if isNull (box source2) then
            nullArg (nameof source2)

        if not (obj.ReferenceEquals (source1.Graph, source2.Graph)) then
            raise (InvalidOperationException UntrackedPhase.crossGraphNodes)

    /// <summary>The left signal.</summary>
    member _.Source1 = source1
    /// <summary>The right signal.</summary>
    member _.Source2 = source2

    interface IReactive<struct ('A * 'B)> with
        member _.Graph = source1.Graph

        member _.ReadTracked() =
            let a = source1.Value
            let b = source2.Value
            struct (a, b)
#else
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type MergedSignals<'A, 'B> =
    val private source1: Signal<'A>
    val private source2: Signal<'B>

    /// <summary>Creates a paired signal descriptor belonging to one graph.</summary>
    new(source1: Signal<'A>, source2: Signal<'B>) =
        if isNull (box source1) then
            nullArg (nameof source1)

        if isNull (box source2) then
            nullArg (nameof source2)

        if not (obj.ReferenceEquals (source1.Graph, source2.Graph)) then
            raise (InvalidOperationException UntrackedPhase.crossGraphNodes)

        { source1 = source1; source2 = source2 }

    /// <summary>The left signal.</summary>
    member this.Source1 = this.source1
    /// <summary>The right signal.</summary>
    member this.Source2 = this.source2

    interface IReactive<struct ('A * 'B)> with
        member this.Graph = this.source1.Graph

        member this.ReadTracked() =
            let a = this.source1.Value
            let b = this.source2.Value
            struct (a, b)
#endif

/// <summary>Pairs two same-graph memos for static <c>Memo.map2</c> dispatch.</summary>
#if FABLE_COMPILER
[<Sealed; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type MergedMemos<'A, 'B>(source1: Memo<'A>, source2: Memo<'B>) =
    do
        if isNull (box source1) then
            nullArg (nameof source1)

        if isNull (box source2) then
            nullArg (nameof source2)

        if not (obj.ReferenceEquals (source1.Graph, source2.Graph)) then
            raise (InvalidOperationException UntrackedPhase.crossGraphNodes)

    /// <summary>The left memo.</summary>
    member _.Source1 = source1
    /// <summary>The right memo.</summary>
    member _.Source2 = source2

    interface IReactive<struct ('A * 'B)> with
        member _.Graph = source1.Graph

        member _.ReadTracked() =
            let a = source1.Value
            let b = source2.Value
            struct (a, b)
#else
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type MergedMemos<'A, 'B> =
    val private source1: Memo<'A>
    val private source2: Memo<'B>

    /// <summary>Creates a paired memo descriptor belonging to one graph.</summary>
    new(source1: Memo<'A>, source2: Memo<'B>) =
        if isNull (box source1) then
            nullArg (nameof source1)

        if isNull (box source2) then
            nullArg (nameof source2)

        if not (obj.ReferenceEquals (source1.Graph, source2.Graph)) then
            raise (InvalidOperationException UntrackedPhase.crossGraphNodes)

        { source1 = source1; source2 = source2 }

    /// <summary>The left memo.</summary>
    member this.Source1 = this.source1
    /// <summary>The right memo.</summary>
    member this.Source2 = this.source2

    interface IReactive<struct ('A * 'B)> with
        member this.Graph = this.source1.Graph

        member this.ReadTracked() =
            let a = this.source1.Value
            let b = this.source2.Value
            struct (a, b)
#endif

/// <summary>Pairs any two same-graph <c>IReactive</c> sources while preserving left-to-right read precedence.</summary>
#if FABLE_COMPILER
[<Sealed; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type MergedReactive<'A, 'B>(source1: IReactive<'A>, source2: IReactive<'B>) =
    do
        if isNull (box source1) then
            nullArg (nameof source1)

        if isNull (box source2) then
            nullArg (nameof source2)

        if not (obj.ReferenceEquals (source1.Graph, source2.Graph)) then
            raise (InvalidOperationException UntrackedPhase.crossGraphNodes)

    /// <summary>The left reactive source.</summary>
    member _.Source1 = source1
    /// <summary>The right reactive source.</summary>
    member _.Source2 = source2

    interface IReactive<struct ('A * 'B)> with
        member _.Graph = source1.Graph

        member _.ReadTracked() =
            let a = source1.ReadTracked ()
            let b = source2.ReadTracked ()
            struct (a, b)
#else
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type MergedReactive<'A, 'B> =
    val private source1: IReactive<'A>
    val private source2: IReactive<'B>

    /// <summary>Creates a paired reactive descriptor belonging to one graph.</summary>
    new(source1: IReactive<'A>, source2: IReactive<'B>) =
        if isNull (box source1) then
            nullArg (nameof source1)

        if isNull (box source2) then
            nullArg (nameof source2)

        if not (obj.ReferenceEquals (source1.Graph, source2.Graph)) then
            raise (InvalidOperationException UntrackedPhase.crossGraphNodes)

        { source1 = source1; source2 = source2 }

    /// <summary>The left reactive source.</summary>
    member this.Source1 = this.source1
    /// <summary>The right reactive source.</summary>
    member this.Source2 = this.source2

    interface IReactive<struct ('A * 'B)> with
        member this.Graph = this.source1.Graph

        member this.ReadTracked() =
            let a = this.source1.ReadTracked ()
            let b = this.source2.ReadTracked ()
            struct (a, b)
#endif

/// <summary>Combined row input carrier used exclusively by <c>RowBuilder.MergeSources</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type RowInput<'T> =
    val ReadFn: unit -> 'T
    new(readFn: unit -> 'T) = { ReadFn = readFn }

/// <summary>Pure <c>IScopeHost</c> raising the supplied violation message if an owned node is created.</summary>
[<Sealed; NoEquality; NoComparison>]
type internal PureCeScopeHost(message: string) =
    static member val MemoInstance: IScopeHost = PureCeScopeHost ScopeMessages.memo :> IScopeHost
    static member val RowInstance: IScopeHost = PureCeScopeHost ScopeMessages.factoryRow :> IScopeHost
    static member val AsyncInstance: IScopeHost = PureCeScopeHost ScopeMessages.asyncMemo :> IScopeHost

    interface IScopeHost with
        member _.CanOwn = false
        member _.Scope = raise (InvalidOperationException message)

/// <summary>Deduplicates <c>Some</c> wrapper identity on <c>memo { option ... }</c>.</summary>
[<EditorBrowsable(EditorBrowsableState.Never)>]
type IOptionDeduplicator<'U> =
    abstract WrapStatic: graph: Graph * mapping: ('K -> 'U) -> ('K -> 'U)

/// <summary>Typed <c>IOptionDeduplicator</c> retaining previous <c>Some</c> reference equality for <c>'A option</c>.</summary>
[<Sealed; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type OptionDeduplicator<'A>() =
    interface IOptionDeduplicator<'A option> with
        member _.WrapStatic<'K>(graph: Graph, mapping: 'K -> 'A option) : 'K -> 'A option =
            let innerEq = graph.EncodedComparer<'A>()
            let mutable hasLast = false
            let mutable lastSome: 'A option = None

            fun k ->
                let next = mapping k

                match next with
                | Some cur ->
                    if hasLast then
                        match lastSome with
                        | Some prev when
                            Identity.same prev cur
                            || ComparerSentinel.equals innerEq prev cur
                            ->
                            lastSome
                        | _ ->
                            lastSome <- next
                            next
                    else
                        hasLast <- true
                        lastSome <- next
                        next
                | None ->
                    hasLast <- false
                    lastSome <- None
                    None

    static member Create<'A>(graph: Graph, compute: unit -> 'A option) : Memo<'A option> =
        let innerEq = graph.EncodedComparer<'A>()

        let wrapped (last: 'A option voption) =
            let next = compute ()

            match last, next with
            | ValueSome (Some previous as kept), Some current when
                Identity.same previous current
                || ComparerSentinel.equals innerEq previous current
                ->
                kept
            | _ -> next

        graph.Entered ("Creating a node", fun () -> Memo<'A option>.Create(graph, wrapped, ScopeMode.Pure))

/// <summary>Shared runtime helpers invoked by computation-expression builders.</summary>
[<AbstractClass; Sealed; EditorBrowsable(EditorBrowsableState.Never)>]
type CeRuntime =
#if FABLE_COMPILER
    [<Fable.Core.Emit("$0.then(undefined, function (e) { throw $1(e); })")>]
    static member private RejectMap<'U>(t: Task<'U>, mapErr: exn -> exn) : Task<'U> =
        Fable.Core.Util.jsNative
#endif

    static member private PickPureHost(pureKind: int, g: Graph) : IScopeHost =
        let fallbackHost =
            if pureKind = 1 then PureCeScopeHost.RowInstance
            elif pureKind = 2 then PureCeScopeHost.AsyncInstance
            else PureCeScopeHost.MemoInstance

        let candidate =
            let obs = g.CurrentObserver

            if not (Platform.isNull (box obs)) then
                box obs
            elif obj.ReferenceEquals (g, InternalCeState.CurrentEffectGraph) then
                InternalCeState.CurrentEffectObserver
            else
                null

#if FABLE_COMPILER
        if
            not (Platform.isNull candidate)
            && Platform.hasMember candidate "CanOwn"
        then
            let sh = candidate :?> IScopeHost
            if not sh.CanOwn then sh else fallbackHost
        else
            fallbackHost
#else
        if not (Platform.isNull candidate) then
            match candidate with
            | :? IScopeHost as sh when not sh.CanOwn -> sh
            | _ -> fallbackHost
        else
            fallbackHost
#endif

    static member TryGetAmbientGraph() : Graph =
#if FABLE_COMPILER
        match Graph.TryCurrent with
        | ValueSome g -> g
        | ValueNone -> Unchecked.defaultof<Graph>
#else
        let mutable g = Unchecked.defaultof<Graph>

        if Graph.TryGetCurrent &g then
            g
        else
            Unchecked.defaultof<Graph>
#endif

    static member ProbeBound<'T>(probe: unit -> 'T) : 'T =
        let saved = InternalCeState.SwapProbing true

        try
            probe ()
        finally
            InternalCeState.SwapProbing saved |> ignore

    static member DisposeResource<'D when 'D :> IDisposable>(resource: 'D) : unit =
        if not (Platform.isNull (box resource)) then
            resource.Dispose ()

    static member inline RunUsing<'D, 'T when 'D :> IDisposable>(resource: 'D, [<InlineIfLambda>] eval: unit -> 'T) : 'T =
        try
            eval ()
        finally
            CeRuntime.DisposeResource resource

    static member ProbeDisposed<'T>() : 'T =
        invalidOp "Probe-only step evaluated."

    static member RunTryWithHandler<'T>(caughtEx: exn, handle: unit -> 'T) : 'T =
        let savedPending = InternalCeState.SwapPhase1Pending null

        try
            handle ()
        with ex2 ->
            if obj.ReferenceEquals (ex2, caughtEx) then
                InternalCeState.SetPhase1Pending savedPending

            reraise ()

    static member private RunUntrackedPureBody<'A, 'T>(pureKind: int, g: Graph, f: 'A -> 'T, arg: 'A) : 'T =
        g.Entered (
            "An untracked read",
            fun () ->
                let host = CeRuntime.PickPureHost (pureKind, g)

#if FABLE_COMPILER
                g.RunUntrackedPureArg (host, f, arg)
#else
                let hasJournal = not (Platform.isNull g.Journal)

                if hasJournal then
                    g.Journal.EnterDerived ()

                let saved = g.EnterUntrackedPure host

                try
                    f arg
                finally
                    g.LeaveUntrackedPure saved

                    if hasJournal then
                        g.Journal.LeaveDerived ()
#endif
        )

    static member private RunUntrackedPureCore<'A, 'T>(pureKind: int, pendingMessage: string, g: Graph, f: 'A -> 'T, arg: 'A) : 'T =
        try
            CeRuntime.RunUntrackedPureBody (pureKind, g, f, arg)
        with NotReadyException _ as ex ->
            raise (UntrackedPhase.wrapPending pendingMessage ex)

    static member RunUntrackedPureArg<'A, 'T>(isRow: bool, g: Graph, f: 'A -> 'T, arg: 'A) : 'T =
        CeRuntime.RunUntrackedPureCore ((if isRow then 1 else 0), UntrackedPhase.mapPending, g, f, arg)

    static member RunUntrackedBoundaryArg<'A, 'T>(g: Graph, f: 'A -> 'T, arg: 'A) : 'T =
        try
            g.RunUntrackedWith (f, arg)
        with NotReadyException _ as ex ->
            raise (UntrackedPhase.wrapPending UntrackedPhase.mapPending ex)

    static member RunUntrackedRowArg<'A, 'T>(explicitGraph: Graph, f: 'A -> 'T, arg: 'A) : 'T =
        let ambient = CeRuntime.TryGetAmbientGraph ()

        if not (Platform.isNull (box ambient)) then
            CeRuntime.RunUntrackedPureArg (true, ambient, f, arg)
        elif not (Platform.isNull (box explicitGraph)) then
            use _ = explicitGraph.Activate ()
            CeRuntime.RunUntrackedPureArg (true, explicitGraph, f, arg)
        else
            f arg

    static member RunUntrackedMemoBind<'A, 'U>(owning: bool, g: Graph, f: 'A -> 'U, arg: 'A) : 'U =
        if owning then
            CeRuntime.RunUntrackedBoundaryArg (g, f, arg)
        else
            CeRuntime.RunUntrackedPureArg (false, g, f, arg)

    static member RunAsyncUntrackedArg<'A, 'U>(owning: bool, g: Graph, f: 'A -> 'U, arg: 'A) : 'U =
        if owning then
            try
                g.RunUntrackedWith (f, arg)
            with NotReadyException _ as ex ->
                raise (UntrackedPhase.wrapPending UntrackedPhase.asyncOnPending ex)
        else
            CeRuntime.RunUntrackedPureCore (2, UntrackedPhase.asyncOnPending, g, f, arg)

    static member RunStreamUntrackedArg<'A, 'T>(g: Graph, f: 'A -> 'T, arg: 'A) : 'T =
        CeRuntime.RunUntrackedPureCore (0, UntrackedPhase.streamOnPending, g, f, arg)

    static member RunAsyncUntracked<'A, 'T>(f: 'A -> 'T, arg: 'A) : 'T =
        let g = CeRuntime.TryGetAmbientGraph ()

        try
            if not (Platform.isNull (box g)) then
                g.RunUntrackedWith (f, arg)
            else
                f arg
        with NotReadyException _ as ex ->
            raise (UntrackedPhase.wrapPending UntrackedPhase.asyncOnPending ex)

    static member WrapAsyncPending<'U>(t: Task<'U>) : Task<'U> =
#if FABLE_COMPILER
        if Platform.isNull t then
            t
        else
            CeRuntime.RejectMap (
                t,
                fun (ex: exn) ->
                    match ex with
                    | :? NotReadyException -> UntrackedPhase.wrapPending UntrackedPhase.asyncOnPending ex
                    | _ -> ex
            )
#else
        if
            Platform.isNull t
            || t.IsCompletedSuccessfully
            || t.IsCanceled
        then
            t
        elif t.IsFaulted then
            let baseEx = t.Exception.GetBaseException ()

            match baseEx with
            | :? NotReadyException -> Task.FromException<'U>(UntrackedPhase.wrapPending UntrackedPhase.asyncOnPending baseEx)
            | _ -> t
        else
            task {
                try
                    return! t
                with NotReadyException _ as ex ->
                    return raise (UntrackedPhase.wrapPending UntrackedPhase.asyncOnPending ex)
            }
#endif

    static member RequireStreamFolder<'S, 'T>(folder: 'S -> 'T -> 'S) : unit =
        if UntrackedPhase.isNullFn2 folder then
            nullArg (nameof folder)

    static member ReadTrackedOn<'T>(g: Graph, readFn: unit -> 'T) : 'T =
        let savedObs = InternalCeState.CurrentEffectObserver
        let activeObs = g.CurrentObserver

        try
            if not (Platform.isNull (box activeObs)) then
                readFn ()
            elif
                not (Platform.isNull savedObs)
                && obj.ReferenceEquals (g, InternalCeState.CurrentEffectGraph)
            then
                g.ResumeTracked (unbox<IComputation> savedObs, readFn)
            else
                g.Entered ("A reactive read", readFn)
        with NotReadyException _ as ex ->
            InternalCeState.SetPhase1Pending ex
            reraise ()

    static member ReadCeTracked<'K>(source: IReactive<'K>) : 'K =
        CeRuntime.ReadTrackedOn (source.Graph, source.ReadTracked)

    static member ReadRowFnTracked<'T>(readFn: unit -> 'T) : 'T =
        let g = CeRuntime.TryGetAmbientGraph ()

        if not (Platform.isNull (box g)) then
            CeRuntime.ReadTrackedOn (g, readFn)
        else
            readFn ()

    static member private RunUntrackedContinuationCore<'K, 'T>
        (pureKind: int, owning: bool, pendingMessage: string, g: Graph, key: 'K, cont: 'K -> 'T)
        : 'T =
        let struct (obsGraph, obs) =
            let active = g.CurrentObserver

            if not (Platform.isNull (box active)) then
                struct (g, box active)
            else
                struct (InternalCeState.CurrentEffectGraph, InternalCeState.CurrentEffectObserver)

        let prevObs = InternalCeState.SwapEffectObserver (obsGraph, obs)

        try
            try
                if owning then
                    g.RunUntrackedWith (cont, key)
                else
                    CeRuntime.RunUntrackedPureBody (pureKind, g, cont, key)
            with NotReadyException _ as ex ->
                if obj.ReferenceEquals (ex, InternalCeState.CurrentPhase1Pending) then
                    reraise ()
                else
                    raise (UntrackedPhase.wrapPending pendingMessage ex)
        finally
            InternalCeState.RestoreEffectObserver prevObs

    static member RunUntrackedContinuation<'K, 'T>(isRow: bool, owning: bool, g: Graph, key: 'K, cont: 'K -> 'T) : 'T =
        CeRuntime.RunUntrackedContinuationCore ((if isRow then 1 else 0), owning, UntrackedPhase.mapPending, g, key, cont)

    static member RunAsyncUntrackedContinuation<'K, 'T>(owning: bool, g: Graph, key: 'K, cont: 'K -> 'T) : 'T =
        CeRuntime.RunUntrackedContinuationCore (2, owning, UntrackedPhase.asyncOnPending, g, key, cont)

    static member RunStreamUntrackedContinuation<'K, 'T>(g: Graph, key: 'K, cont: 'K -> 'T) : 'T =
        CeRuntime.RunUntrackedContinuationCore (0, false, UntrackedPhase.streamOnPending, g, key, cont)

    static member RunRowContinuation<'K, 'V>(explicitGraph: Graph, key: 'K, cont: 'K -> 'V) : 'V =
        let ambient = CeRuntime.TryGetAmbientGraph ()

        if not (Platform.isNull (box ambient)) then
            CeRuntime.RunUntrackedContinuation (true, false, ambient, key, cont)
        elif not (Platform.isNull (box explicitGraph)) then
            use _ = explicitGraph.Activate ()
            CeRuntime.RunUntrackedContinuation (true, false, explicitGraph, key, cont)
        else
            cont key

    static member EvalRow<'V>(eval: unit -> 'V) : 'V =
        let g = CeRuntime.TryGetAmbientGraph ()

        let struct (obsGraph, activeObs) =
            if not (Platform.isNull (box g)) then
                struct (g, box g.CurrentObserver)
            else
                struct (Unchecked.defaultof<Graph>, null)

        let savedObs = InternalCeState.SwapEffectObserver (obsGraph, activeObs)
        let savedPending = InternalCeState.SwapPhase1Pending null
        let savedProbing = InternalCeState.SwapProbing false

        try
            eval ()
        finally
            InternalCeState.SwapProbing savedProbing |> ignore

            InternalCeState.SwapPhase1Pending savedPending
            |> ignore

            InternalCeState.RestoreEffectObserver savedObs

    static member EvalStreamBody<'V>(graph: Graph, eval: unit -> 'V) : 'V =
        let savedDepth = InternalCeState.EnterEval ()
        let savedReactive = InternalCeState.SwapReactiveBind false
        let savedOwning = InternalCeState.SwapOwning false
        let savedObs = InternalCeState.SwapEffectObserver (graph, box graph.CurrentObserver)
        let savedPending = InternalCeState.SwapPhase1Pending null
        let savedPrev = InternalCeState.SuspendPrevious ()
        let savedProbing = InternalCeState.SwapProbing false

        try
            CeRuntime.RunStreamUntrackedContinuation (graph, (), eval)
        finally
            InternalCeState.SwapProbing savedProbing |> ignore
            InternalCeState.RestorePrevious savedPrev

            InternalCeState.SwapPhase1Pending savedPending
            |> ignore

            InternalCeState.RestoreEffectObserver savedObs
            InternalCeState.SwapOwning savedOwning |> ignore

            InternalCeState.SwapReactiveBind savedReactive
            |> ignore

            InternalCeState.ExitEval savedDepth

    static member CreateDynamicStreamLatest<'T>(graph: Graph, evalStart: CancellationToken -> StreamSource<'T>) : StreamMemo<'T> =
        let start (ct: CancellationToken) =
            CeRuntime.EvalStreamBody (graph, fun () -> evalStart ct)

        graph.Entered (
            "Creating a node",
            fun () -> StreamMemo<'T>.Create<'T>(graph, Platform.zero<'T>(), (fun (_: 'T) (item: 'T) -> item), start, true)
        )

    static member CreateDynamicStreamFold<'S, 'T>
        (graph: Graph, seed: 'S, folder: 'S -> 'T -> 'S, evalStart: CancellationToken -> StreamSource<'T>)
        : StreamMemo<'S> =
        let start (ct: CancellationToken) =
            CeRuntime.EvalStreamBody (graph, fun () -> evalStart ct)

        graph.Entered ("Creating a node", fun () -> StreamMemo<'S>.Create<'T>(graph, seed, folder, start, false))

    static member CreateAsyncFromReactive<'K, 'U>
        (source: IReactive<'K>, build: 'K -> Previous<'U> -> CancellationToken -> Task<'U>, owning: bool, policy: FlightPolicy voption)
        : AsyncMemo<'U> =
        let g = source.Graph
        let mode = if owning then ScopeMode.Owning else ScopeMode.PureAsync

        g.Entered (
            "Creating a node",
            fun () ->
                let pol =
                    match policy with
                    | ValueSome p -> p
                    | ValueNone -> g.Options.FlightPolicy

                match source with
                | :? ComparedReactive<'K> as cr -> AsyncMemo<'U>.CreateOn(g, cr.Source.ReadTracked, build, mode, pol, keyComparer = cr.Comparer)
                | _ -> AsyncMemo<'U>.CreateOn(g, source.ReadTracked, build, mode, pol)
        )

    static member TryAsMemo<'T>(source: IReactive<'T>) : Memo<'T> voption =
        match source with
        | :? Memo<'T> as m -> ValueSome m
        | :? Editable<'T> as e -> ValueSome e.ValueMemo
        | _ -> ValueNone

    static member CreateStaticMemo<'K, 'U>
        (
            graph: Graph,
            readKey: unit -> 'K,
            mapping: 'K -> 'U,
            owning: bool,
            cmp: IEqualityComparer<'U> voption,
            keyCmp: IEqualityComparer<'K> voption,
            dedupObj: obj
        ) : Memo<'U> =
        let mode = if owning then ScopeMode.Owning else ScopeMode.Pure

        let mappedFn =
            if isNull dedupObj then
                mapping
            else
                (unbox<IOptionDeduplicator<'U>> dedupObj).WrapStatic(graph, mapping)

        graph.Entered (
            "Creating a node",
            fun () ->
                match keyCmp with
                | ValueSome kc -> Memo<'U>.CreateOn(graph, readKey, mappedFn, mode, cmp, ?keyComparer = Some kc)
                | ValueNone -> Memo<'U>.CreateOn(graph, readKey, mappedFn, mode, cmp)
        )

    static member CreateMemoFromReactive<'K, 'U>
        (source: IReactive<'K>, mapping: 'K -> 'U, owning: bool, cmp: IEqualityComparer<'U> voption, dedupObj: obj)
        : Memo<'U> =
        let g = source.Graph

        if not owning && cmp.IsNone && isNull dedupObj then
            match source with
            | :? Signal<'K> as s -> CombinatorMemos.createSignalMap mapping s
            | :? Memo<'K> as m -> CombinatorMemos.createMemoMap mapping m
            | :? Editable<'K> as e -> CombinatorMemos.createMemoMap mapping e.ValueMemo
            | :? AsyncMemo<'K> as am -> CombinatorMemos.createSourceMap mapping (AsyncMemoAdapter<'K> am)
            | :? AsyncSource<'K> as asrc -> CombinatorMemos.createSourceMap mapping (AsyncSourceAdapter<'K> asrc)
            | :? StreamMemo<'K> as sm -> CombinatorMemos.createSourceMap mapping (StreamMemoAdapter<'K> sm)
            | :? Boundary<'K> as bnd -> CombinatorMemos.createSourceMap mapping (BoundaryAdapter<'K> bnd)
            | :? Timed<'K> as t -> CombinatorMemos.createSourceMap mapping (TimedAdapter<'K> t)
            | :? ComparedReactive<'K> as cr ->
                g.Entered (
                    "Creating a node",
                    fun () -> Memo<'U>.CreateOn(g, cr.Source.ReadTracked, mapping, ScopeMode.Pure, ValueNone, ?keyComparer = Some cr.Comparer)
                )
            | _ -> g.Entered ("Creating a node", fun () -> Memo<'U>.CreateOn(g, source.ReadTracked, mapping, ScopeMode.Pure, ValueNone))
        else
            match source with
            | :? ComparedReactive<'K> as cr ->
                CeRuntime.CreateStaticMemo (g, cr.Source.ReadTracked, mapping, owning, cmp, ValueSome cr.Comparer, dedupObj)
            | _ -> CeRuntime.CreateStaticMemo (g, source.ReadTracked, mapping, owning, cmp, ValueNone, dedupObj)

    static member CreateMemoFromMergedReactive<'A, 'B, 'U>
        (merged: MergedReactive<'A, 'B>, mapping: struct ('A * 'B) -> 'U, owning: bool, cmp: IEqualityComparer<'U> voption, dedupObj: obj)
        : Memo<'U> =
        if not owning && cmp.IsNone && isNull dedupObj then
            match CeRuntime.TryAsMemo merged.Source1, CeRuntime.TryAsMemo merged.Source2 with
            | ValueSome m1, ValueSome m2 -> CombinatorMemos.createMemoMap2 (fun a b -> mapping (struct (a, b))) m1 m2
            | _ ->
                let g = (merged :> IReactive<struct ('A * 'B)>).Graph

                g.Entered (
                    "Creating a node",
                    fun () -> Memo<'U>.CreateOn(g, (merged :> IReactive<struct ('A * 'B)>).ReadTracked, mapping, ScopeMode.Pure, ValueNone)
                )
        else
            let g = (merged :> IReactive<struct ('A * 'B)>).Graph
            CeRuntime.CreateStaticMemo (g, (merged :> IReactive<struct ('A * 'B)>).ReadTracked, mapping, owning, cmp, ValueNone, dedupObj)

    static member CreateDynamicMemo<'U>(graph: Graph, eval: unit -> 'U, owning: bool, cmp: IEqualityComparer<'U> voption) : Memo<'U> =
        let mode = if owning then ScopeMode.Owning else ScopeMode.Pure

        let runBody (prev: 'U voption) : 'U =
            let savedDepth = InternalCeState.EnterEval ()
            let savedReactive = InternalCeState.SwapReactiveBind false
            let savedOwning = InternalCeState.SwapOwning owning
            let savedObs = InternalCeState.SwapEffectObserver (graph, box graph.CurrentObserver)
            let savedPending = InternalCeState.SwapPhase1Pending null
            let savedPrev = InternalCeState.SwapPrevious prev
            let savedProbing = InternalCeState.SwapProbing false

            try
                eval ()
            finally
                InternalCeState.SwapProbing savedProbing |> ignore

                InternalCeState.RestorePrevious savedPrev

                InternalCeState.SwapPhase1Pending savedPending
                |> ignore

                InternalCeState.RestoreEffectObserver savedObs

                InternalCeState.SwapOwning savedOwning |> ignore

                InternalCeState.SwapReactiveBind savedReactive
                |> ignore

                InternalCeState.ExitEval savedDepth

        graph.Entered (
            "Creating a node",
            fun () ->
                match cmp with
                | ValueSome c -> Memo<'U>.CreateWithComparer(graph, runBody, mode, c)
                | ValueNone -> Memo<'U>.Create(graph, runBody, mode)
        )

    static member CreateDynamicOptionMemo<'A>(graph: Graph, eval: unit -> 'A option) : Memo<'A option> =
        let runBody () : 'A option =
            let savedDepth = InternalCeState.EnterEval ()
            let savedReactive = InternalCeState.SwapReactiveBind false
            let savedOwning = InternalCeState.SwapOwning false
            let savedObs = InternalCeState.SwapEffectObserver (graph, box graph.CurrentObserver)
            let savedPending = InternalCeState.SwapPhase1Pending null
            let savedPrev = InternalCeState.SuspendPrevious ()
            let savedProbing = InternalCeState.SwapProbing false

            try
                eval ()
            finally
                InternalCeState.SwapProbing savedProbing |> ignore

                InternalCeState.RestorePrevious savedPrev

                InternalCeState.SwapPhase1Pending savedPending
                |> ignore

                InternalCeState.RestoreEffectObserver savedObs

                InternalCeState.SwapOwning savedOwning |> ignore

                InternalCeState.SwapReactiveBind savedReactive
                |> ignore

                InternalCeState.ExitEval savedDepth

        OptionDeduplicator<'A>.Create(graph, runBody)

    static member CreateStaticTail<'K, 'U>(source: IReactive<'K>, binder: 'K -> Memo<'U>, cmp: IEqualityComparer<'U> voption) : Memo<'U> =
        let g = source.Graph

        if cmp.IsNone then
            match source with
            | :? Signal<'K> as s -> CombinatorMemos.createSignalBind binder s
            | :? Memo<'K> as m -> CombinatorMemos.createMemoBind binder m
            | :? Editable<'K> as e -> CombinatorMemos.createMemoBind binder e.ValueMemo
            | :? ComparedReactive<'K> as cr -> CombinatorMemos.createReactiveBind (g, cr.Source.ReadTracked, binder, ValueSome cr.Comparer, ValueNone)
            | _ -> CombinatorMemos.createReactiveBind (g, source.ReadTracked, binder, ValueNone, ValueNone)
        else
            match source with
            | :? ComparedReactive<'K> as cr -> CombinatorMemos.createReactiveBind (g, cr.Source.ReadTracked, binder, ValueSome cr.Comparer, cmp)
            | _ -> CombinatorMemos.createReactiveBind (g, source.ReadTracked, binder, ValueNone, cmp)

    static member ReadEffectTracked<'K>(source: IReactive<'K>) : 'K =
        CeRuntime.ReadCeTracked source

    static member RunEffectContinuation<'K>(source: IReactive<'K>, key: 'K, cont: 'K -> unit) : unit =
        let g = source.Graph

        let struct (obsGraph, obs) =
            let active = g.CurrentObserver

            if not (Platform.isNull (box active)) then
                struct (g, box active)
            else
                struct (InternalCeState.CurrentEffectGraph, InternalCeState.CurrentEffectObserver)

        let prevObs = InternalCeState.SwapEffectObserver (obsGraph, obs)

        try
            g.RunUntrackedWith (cont, key)
        finally
            InternalCeState.RestoreEffectObserver prevObs

    static member CreateEffectOn<'K>(source: IReactive<'K>, act: 'K -> unit, asNode: bool) : INode =
        let g = source.Graph

        let node =
            g.Entered (
                "Creating a node",
                fun () ->
                    match source with
                    | :? ComparedReactive<'K> as cr -> EffectOn<'K>.CreateWithComparer(g, cr.Source.ReadTracked, act, cr.Comparer) :> INode
                    | _ -> EffectOn<'K>.Create(g, source.ReadTracked, act) :> INode
            )

        if asNode then node else Unchecked.defaultof<INode>

    static member CreateDynamicEffect(graph: Graph, eval: unit -> unit, asNode: bool) : INode =
        let runBody () =
            let savedDepth = InternalCeState.EnterEval ()
            let savedPrev = InternalCeState.SuspendPrevious ()
            let savedObs = InternalCeState.SwapEffectObserver (graph, box graph.CurrentObserver)
            let savedPending = InternalCeState.SwapPhase1Pending null
            let savedProbing = InternalCeState.SwapProbing false

            try
                eval ()
            finally
                InternalCeState.SwapProbing savedProbing |> ignore

                InternalCeState.SwapPhase1Pending savedPending
                |> ignore

                InternalCeState.RestoreEffectObserver savedObs

                InternalCeState.RestorePrevious savedPrev
                InternalCeState.ExitEval savedDepth

        let eff =
            graph.Entered ("Creating a node", fun () -> Effect.Create (graph, runBody) :> INode)

        if asNode then eff else Unchecked.defaultof<INode>

    static member CreateEditable<'U>(graph: Graph, eval: unit -> 'U, draft: bool) : Editable<'U> =
        let seed (prev: 'U voption) : 'U =
            let savedDepth = InternalCeState.EnterEval ()
            let savedReactive = InternalCeState.SwapReactiveBind false
            let savedOwning = InternalCeState.SwapOwning false
            let savedObs = InternalCeState.SwapEffectObserver (graph, box graph.CurrentObserver)
            let savedPending = InternalCeState.SwapPhase1Pending null
            let savedPrev = InternalCeState.SwapPrevious prev
            let savedProbing = InternalCeState.SwapProbing false

            try
                eval ()
            finally
                InternalCeState.SwapProbing savedProbing |> ignore

                InternalCeState.RestorePrevious savedPrev

                InternalCeState.SwapPhase1Pending savedPending
                |> ignore

                InternalCeState.RestoreEffectObserver savedObs

                InternalCeState.SwapOwning savedOwning |> ignore

                InternalCeState.SwapReactiveBind savedReactive
                |> ignore

                InternalCeState.ExitEval savedDepth

        graph.Entered ("Creating a node", fun () -> Editable<'U>(graph, seed, draft))

    static member CreateBoundary<'U>
        (
            outerGraph: Graph,
            eval: unit -> 'U,
            onPending: ('U voption -> 'U) voption,
            onError: (exn -> 'U voption -> 'U) voption,
            comparer: IEqualityComparer<'U> voption
        ) : Boundary<'U> =
        let g =
            match Graph.TryCurrent with
            | ValueSome ambient -> ambient
            | ValueNone ->
                if not (Platform.isNull (box outerGraph)) then
                    outerGraph
                else
                    Graph.Current

        let body () =
            let savedDepth = InternalCeState.EnterEval ()
            let savedOwning = InternalCeState.SwapOwning true
            let savedObs = InternalCeState.SwapEffectObserver (g, box g.CurrentObserver)
            let savedPending = InternalCeState.SwapPhase1Pending null
            let savedPrev = InternalCeState.SuspendPrevious ()
            let savedProbing = InternalCeState.SwapProbing false

            try
                eval ()
            finally
                InternalCeState.SwapProbing savedProbing |> ignore

                InternalCeState.RestorePrevious savedPrev

                InternalCeState.SwapPhase1Pending savedPending
                |> ignore

                InternalCeState.RestoreEffectObserver savedObs

                InternalCeState.SwapOwning savedOwning |> ignore
                InternalCeState.ExitEval savedDepth

        g.Entered (
            "Creating a node",
            fun () ->
                match comparer with
                | ValueSome cmp -> Boundary<'U>.CreateWithComparer(g, body, onPending, onError, cmp)
                | ValueNone -> Boundary<'U>.Create(g, body, onPending, onError)
        )

    static member inline SuspendNested<'T>([<InlineIfLambda>] build: unit -> 'T) : 'T =
        let savedDepth = InternalCeState.SuspendForNestedRun ()
        let savedPrev = InternalCeState.SuspendPrevious ()
        let savedReactive = InternalCeState.SwapReactiveBind false
        let savedOwning = InternalCeState.SwapOwning false
        let savedObs = InternalCeState.SwapEffectObserver (Unchecked.defaultof<Graph>, null)
        let savedPending = InternalCeState.SwapPhase1Pending null
        let savedDedup = InternalCeState.SwapOptionDedup null
        let savedProbing = InternalCeState.SwapProbing false

        try
            build ()
        finally
            InternalCeState.SwapProbing savedProbing |> ignore

            InternalCeState.SwapOptionDedup savedDedup
            |> ignore

            InternalCeState.SwapPhase1Pending savedPending
            |> ignore

            InternalCeState.RestoreEffectObserver savedObs

            InternalCeState.SwapOwning savedOwning |> ignore

            InternalCeState.SwapReactiveBind savedReactive
            |> ignore

            InternalCeState.RestorePrevious savedPrev
            InternalCeState.ExitEval savedDepth

/// <summary>Factory delegate carrier for <c>MemoBuilder</c> static/dynamic dispatch.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type MemoStep<'Phase, 'Prev, 'U> =
    | MemoInline of inlineVal: 'U
    | MemoDynamic of dynGraph: Graph * dynEval: (unit -> 'U)
    | MemoStatic of staticGraph: Graph * staticFactory: (bool -> IEqualityComparer<'U> voption -> obj -> Memo<'U>)

    member inline this.TryGraph =
        match this with
        | MemoInline _ -> Unchecked.defaultof<Graph>
        | MemoDynamic (g, _) -> g
        | MemoStatic (g, _) -> g

    member this.EvalValue() =
        match this with
        | MemoInline v -> v
        | MemoDynamic (_, f) -> f ()
        | MemoStatic (_, _) -> invalidOp "Static MemoStep evaluated outside dynamic context."

/// <summary>Return-from tail carrier compiling pure dynamic child binds onto <c>SplitBind</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type MemoTail<'Phase, 'U> =
    | TailReady of readyMemo: Memo<'U>
    | TailDynamic of tailGraph: Graph * tailEval: (unit -> Memo<'U>)
    | TailStatic of tailGraph: Graph * tailFactory: (IEqualityComparer<'U> voption -> Memo<'U>)

    member this.EvalMemo() =
        match this with
        | TailReady m -> m
        | TailDynamic (_, f) -> f ()
        | TailStatic (_, f) -> f ValueNone

/// <summary>Header descriptor configured via <c>owning</c>, <c>comparer</c>, and <c>option</c> inside <c>memo</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type MemoHeader<'Mode, 'Cmp> =
    val Cmp: 'Cmp
    new(cmp: 'Cmp) = { Cmp = cmp }

/// <summary>Configured <c>memo</c> step preserving static header capabilities and phase.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type ConfiguredMemo<'Mode, 'Cmp, 'Phase, 'Prev, 'U> =
    val Header: MemoHeader<'Mode, 'Cmp>
    val Body: unit -> MemoStep<'Phase, 'Prev, 'U>
    new(header: MemoHeader<'Mode, 'Cmp>, body: unit -> MemoStep<'Phase, 'Prev, 'U>) = { Header = header; Body = body }

/// <summary>Configured <c>memo</c> <c>return!</c> tail preserving header capabilities and phase.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type ConfiguredMemoTail<'Mode, 'Cmp, 'Phase, 'U> =
    val Header: MemoHeader<'Mode, 'Cmp>
    val Body: unit -> MemoTail<'Phase, 'U>
    new(header: MemoHeader<'Mode, 'Cmp>, body: unit -> MemoTail<'Phase, 'U>) = { Header = header; Body = body }

/// <summary>Static builder support for <c>MemoBuilder</c> <c>BoundPhase</c> execution.</summary>
[<AbstractClass; Sealed; EditorBrowsable(EditorBrowsableState.Never)>]
type MemoRunner =
    static member EvalBound<'Prev, 'U>(d: unit -> MemoStep<BoundPhase, 'Prev, 'U>, owning: bool, cmp: IEqualityComparer<'U> voption) : Memo<'U> =
        CeRuntime.SuspendNested (fun () ->
            match CeRuntime.ProbeBound d with
            | MemoStatic (_, factory) -> factory owning cmp null
            | MemoDynamic (g, _) ->
                let graph = if Platform.isNull (box g) then Graph.Current else g
                CeRuntime.CreateDynamicMemo (graph, (fun () -> (d ()).EvalValue()), owning, cmp)
            | MemoInline _ ->
                let graph = Graph.Current
                CeRuntime.CreateDynamicMemo (graph, (fun () -> (d ()).EvalValue()), owning, cmp))

    static member EvalCold<'Prev, 'U>(d: unit -> MemoStep<ColdPhase, 'Prev, 'U>, owning: bool, cmp: IEqualityComparer<'U> voption) : Memo<'U> =
        CeRuntime.CreateDynamicMemo (Graph.Current, (fun () -> (d ()).EvalValue()), owning, cmp)

    static member EvalBoundOption<'A>(d: unit -> MemoStep<BoundPhase, NoPrev, 'A option>) : Memo<'A option> =
        CeRuntime.SuspendNested (fun () ->
            let dedup = OptionDeduplicator<'A>() :> IOptionDeduplicator<'A option>

            InternalCeState.SwapOptionDedup (box dedup)
            |> ignore

            match CeRuntime.ProbeBound d with
            | MemoStatic (_, factory) -> factory false ValueNone (box dedup)
            | MemoDynamic (g, _) ->
                let graph = if Platform.isNull (box g) then Graph.Current else g
                CeRuntime.CreateDynamicOptionMemo (graph, fun () -> (d ()).EvalValue())
            | MemoInline _ -> CeRuntime.CreateDynamicOptionMemo (Graph.Current, fun () -> (d ()).EvalValue()))

    static member EvalColdOption<'A>(d: unit -> MemoStep<ColdPhase, NoPrev, 'A option>) : Memo<'A option> =
        CeRuntime.CreateDynamicOptionMemo (Graph.Current, fun () -> (d ()).EvalValue())

    static member EvalBoundTail<'U>(d: unit -> MemoTail<BoundPhase, 'U>, cmp: IEqualityComparer<'U> voption) : Memo<'U> =
        CeRuntime.SuspendNested (fun () ->
            match CeRuntime.ProbeBound d with
            | TailStatic (_, factory) -> factory cmp
            | TailDynamic (g, _) ->
                let graph = if Platform.isNull (box g) then Graph.Current else g

                match cmp with
                | ValueSome c -> Memo<'U>.CreateWithComparer(graph, (fun _ -> (d ()).EvalMemo().Value), ScopeMode.Owning, c)
                | ValueNone -> Memo<'U>.CreateUnit(graph, (fun () -> (d ()).EvalMemo().Value), ScopeMode.Owning)
            | TailReady m -> m)

    static member EvalColdTail<'U>(d: unit -> MemoTail<ColdPhase, 'U>, cmp: IEqualityComparer<'U> voption) : Memo<'U> =
        let graph = Graph.Current

        match cmp with
        | ValueSome c ->
            graph.Entered ("Creating a node", fun () -> Memo<'U>.CreateWithComparer(graph, (fun _ -> (d ()).EvalMemo().Value), ScopeMode.Owning, c))
        | ValueNone -> graph.Entered ("Creating a node", fun () -> Memo<'U>.CreateUnit(graph, (fun () -> (d ()).EvalMemo().Value), ScopeMode.Owning))

    static member UsingStep<'D, 'P, 'POut, 'Prev, 'U when 'D :> IDisposable>
        (resource: 'D, body: 'D -> MemoStep<'P, 'Prev, 'U>)
        : MemoStep<'POut, 'Prev, 'U> =
        if InternalCeState.IsProbing then
            CeRuntime.DisposeResource resource
            MemoDynamic (Unchecked.defaultof<Graph>, fun () -> CeRuntime.ProbeDisposed ())
        else
            MemoDynamic (Unchecked.defaultof<Graph>, fun () -> CeRuntime.RunUsing (resource, fun () -> (body resource).EvalValue()))

    static member BindSource<'K, 'Prev, 'U>(source: IReactive<'K>, f: 'K -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        if InternalCeState.IsEvaluating then
            let key = CeRuntime.ReadCeTracked source
            MemoInline (CeRuntime.RunUntrackedMemoBind (InternalCeState.IsOwning, source.Graph, f, key))
        else
            let g = source.Graph

            MemoStatic (g, fun owning cmp dedup -> CeRuntime.CreateMemoFromReactive (source, f, owning, cmp, dedup))

    static member BindSignal<'K, 'Prev, 'U>(source: Signal<'K>, f: 'K -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        if InternalCeState.IsEvaluating then
            let key = CeRuntime.ReadTrackedOn (source.Graph, fun () -> source.Value)
            MemoInline (CeRuntime.RunUntrackedMemoBind (InternalCeState.IsOwning, source.Graph, f, key))
        else
            let g = source.Graph

            MemoStatic (
                g,
                fun owning cmp dedup ->
                    if not owning && cmp.IsNone && isNull dedup then
                        CombinatorMemos.createSignalMap f source
                    else
                        CeRuntime.CreateStaticMemo (g, (fun () -> source.Value), f, owning, cmp, ValueNone, dedup)
            )

    static member BindMemo<'K, 'Prev, 'U>(source: Memo<'K>, f: 'K -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        if InternalCeState.IsEvaluating then
            let key = CeRuntime.ReadTrackedOn (source.Graph, fun () -> source.Value)
            MemoInline (CeRuntime.RunUntrackedMemoBind (InternalCeState.IsOwning, source.Graph, f, key))
        else
            let g = source.Graph

            MemoStatic (
                g,
                fun owning cmp dedup ->
                    if not owning && cmp.IsNone && isNull dedup then
                        CombinatorMemos.createMemoMap f source
                    else
                        CeRuntime.CreateStaticMemo (g, (fun () -> source.Value), f, owning, cmp, ValueNone, dedup)
            )

    static member BindMergedSignals<'A, 'B, 'Prev, 'U>(source: MergedSignals<'A, 'B>, f: struct ('A * 'B) -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        let g = (source :> IReactive<struct ('A * 'B)>).Graph

        if InternalCeState.IsEvaluating then
            let key = CeRuntime.ReadCeTracked source
            MemoInline (CeRuntime.RunUntrackedMemoBind (InternalCeState.IsOwning, g, f, key))
        else
            MemoStatic (
                g,
                fun owning cmp dedup ->
                    if not owning && cmp.IsNone && isNull dedup then
                        CombinatorMemos.createSignalMap2 (fun a b -> f (struct (a, b))) source.Source1 source.Source2
                    else
                        CeRuntime.CreateStaticMemo (g, (source :> IReactive<struct ('A * 'B)>).ReadTracked, f, owning, cmp, ValueNone, dedup)
            )

    static member BindMergedMemos<'A, 'B, 'Prev, 'U>(source: MergedMemos<'A, 'B>, f: struct ('A * 'B) -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        let g = (source :> IReactive<struct ('A * 'B)>).Graph

        if InternalCeState.IsEvaluating then
            let key = CeRuntime.ReadCeTracked source
            MemoInline (CeRuntime.RunUntrackedMemoBind (InternalCeState.IsOwning, g, f, key))
        else
            MemoStatic (
                g,
                fun owning cmp dedup ->
                    if not owning && cmp.IsNone && isNull dedup then
                        CombinatorMemos.createMemoMap2 (fun a b -> f (struct (a, b))) source.Source1 source.Source2
                    else
                        CeRuntime.CreateStaticMemo (g, (source :> IReactive<struct ('A * 'B)>).ReadTracked, f, owning, cmp, ValueNone, dedup)
            )

    static member BindMergedReactive<'A, 'B, 'Prev, 'U>(source: MergedReactive<'A, 'B>, f: struct ('A * 'B) -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        let g = (source :> IReactive<struct ('A * 'B)>).Graph

        if InternalCeState.IsEvaluating then
            let key = CeRuntime.ReadCeTracked source
            MemoInline (CeRuntime.RunUntrackedMemoBind (InternalCeState.IsOwning, g, f, key))
        else
            MemoStatic (g, fun owning cmp dedup -> CeRuntime.CreateMemoFromMergedReactive (source, f, owning, cmp, dedup))

    static member BindReactive<'K, 'Phase, 'Prev, 'U>
        (source: IReactive<'K>, continuation: 'K -> MemoStep<'Phase, 'Prev, 'U>)
        : MemoStep<BoundPhase, 'Prev, 'U> =
        let run () =
            let key = CeRuntime.ReadCeTracked source
            let saved = InternalCeState.SwapReactiveBind true

            try
                CeRuntime.RunUntrackedContinuation (false, InternalCeState.IsOwning, source.Graph, key, fun k -> (continuation k).EvalValue())
            finally
                InternalCeState.SwapReactiveBind saved |> ignore

        if InternalCeState.IsEvaluating then
            MemoInline (run ())
        else
            MemoDynamic (source.Graph, run)

    static member BindPrevVal<'T, 'Phase, 'InnerPrev, 'U>
        (probeValue: 'T, readPrev: unit -> 'T, continuation: 'T -> MemoStep<'Phase, 'InnerPrev, 'U>)
        : MemoStep<'Phase, WithPrev, 'U> =
        if InternalCeState.IsEvaluating then
            MemoInline ((continuation (readPrev ())).EvalValue())
        else
            let ambient = CeRuntime.TryGetAmbientGraph ()

            let g =
                if not (Platform.isNull (box ambient)) then
                    ambient
                else
                    (continuation probeValue).TryGraph

            MemoDynamic (g, fun () -> (continuation (readPrev ())).EvalValue())

    static member BindReturnPrevVal<'T, 'Phase, 'U>(prev: 'T, f: 'T -> 'U) : MemoStep<'Phase, WithPrev, 'U> =
        if InternalCeState.HasReactiveBind then
            MemoInline (CeRuntime.RunUntrackedMemoBind (InternalCeState.IsOwning, Graph.Current, f, prev))
        else
            MemoInline (f prev)

/// <summary>Effect step carrier supporting 1-bind <c>EffectOn</c> vs multi-bind dynamic <c>Effect</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type EffectStep<'Leaf, 'Phase> =
    | EffectUnit
    | EffectDynamicFn of effGraph: Graph * effEval: (unit -> unit)
    | EffectStatic of effOnGraph: Graph * effFactory: (bool -> INode) * effDynEval: (unit -> unit)

    member this.EvalUnit() =
        match this with
        | EffectUnit -> ()
        | EffectDynamicFn (_, f) -> f ()
        | EffectStatic (_, _, f) -> f ()

/// <summary>Header descriptor configured via <c>node</c> inside <c>effect</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type EffectHeader<'Out> = struct end

/// <summary>Configured <c>effect</c> step preserving output handle flavor, leaf state, and phase.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type ConfiguredEffect<'Out, 'Leaf, 'Phase> =
    val Body: unit -> EffectStep<'Leaf, 'Phase>
    new(body: unit -> EffectStep<'Leaf, 'Phase>) = { Body = body }

/// <summary>Static runtime helpers for <c>EffectBuilder</c>.</summary>
[<AbstractClass; Sealed; EditorBrowsable(EditorBrowsableState.Never)>]
type EffectRunner =
    static member EvalBound<'Leaf>(d: unit -> EffectStep<'Leaf, BoundPhase>, asNode: bool) : INode =
        CeRuntime.SuspendNested (fun () ->
            match CeRuntime.ProbeBound d with
            | EffectStatic (_, factory, _) -> factory asNode
            | EffectDynamicFn (g, _) ->
                let graph = if Platform.isNull (box g) then Graph.Current else g
                CeRuntime.CreateDynamicEffect (graph, (fun () -> (d ()).EvalUnit()), asNode)
            | EffectUnit -> CeRuntime.CreateDynamicEffect (Graph.Current, (fun () -> (d ()).EvalUnit()), asNode))

    static member EvalCold<'Leaf>(d: unit -> EffectStep<'Leaf, ColdPhase>, asNode: bool) : INode =
        CeRuntime.SuspendNested (fun () -> CeRuntime.CreateDynamicEffect (Graph.Current, (fun () -> (d ()).EvalUnit()), asNode))

    static member UsingStep<'D, 'InLeaf, 'InPhase, 'OutLeaf, 'OutPhase when 'D :> IDisposable>
        (resource: 'D, body: 'D -> EffectStep<'InLeaf, 'InPhase>)
        : EffectStep<'OutLeaf, 'OutPhase> =
        if InternalCeState.IsProbing then
            CeRuntime.DisposeResource resource
            EffectDynamicFn (Unchecked.defaultof<Graph>, fun () -> CeRuntime.ProbeDisposed ())
        else
            EffectDynamicFn (Unchecked.defaultof<Graph>, fun () -> CeRuntime.RunUsing (resource, fun () -> (body resource).EvalUnit()))

/// <summary>Step carrier shared by <c>EditableBuilder</c> and <c>DraftBuilder</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type EditableStep<'Phase, 'Prev, 'U> =
    | EditInline of editVal: 'U
    | EditDynamic of editGraph: Graph * editEval: (unit -> 'U)

    member inline this.TryGraph =
        match this with
        | EditInline _ -> Unchecked.defaultof<Graph>
        | EditDynamic (g, _) -> g

    member this.EvalValue() =
        match this with
        | EditInline v -> v
        | EditDynamic (_, f) -> f ()

/// <summary>Static runtime helpers for <c>EditableBuilder</c> and <c>DraftBuilder</c>.</summary>
[<AbstractClass; Sealed; EditorBrowsable(EditorBrowsableState.Never)>]
type EditableRunner =
    static member EvalBound<'Prev, 'U>(d: unit -> EditableStep<BoundPhase, 'Prev, 'U>, draft: bool) : Editable<'U> =
        CeRuntime.SuspendNested (fun () ->
            match CeRuntime.ProbeBound d with
            | EditDynamic (g, _) ->
                let graph = if Platform.isNull (box g) then Graph.Current else g
                CeRuntime.CreateEditable (graph, (fun () -> (d ()).EvalValue()), draft)
            | EditInline _ -> CeRuntime.CreateEditable (Graph.Current, (fun () -> (d ()).EvalValue()), draft))

    static member EvalCold<'Prev, 'U>(d: unit -> EditableStep<ColdPhase, 'Prev, 'U>, draft: bool) : Editable<'U> =
        CeRuntime.CreateEditable (Graph.Current, (fun () -> (d ()).EvalValue()), draft)

    static member BindReturnReactive<'K, 'Prev, 'U>(source: IReactive<'K>, f: 'K -> 'U) : EditableStep<BoundPhase, 'Prev, 'U> =
        let run () =
            let key = CeRuntime.ReadCeTracked source
            CeRuntime.RunUntrackedPureArg (false, source.Graph, f, key)

        if InternalCeState.IsEvaluating then
            EditInline (run ())
        else
            EditDynamic (source.Graph, run)

    static member BindReturnPrevVal<'T, 'Phase, 'U>(prev: 'T, f: 'T -> 'U) : EditableStep<'Phase, WithPrev, 'U> =
        if InternalCeState.HasReactiveBind then
            EditInline (CeRuntime.RunUntrackedPureArg (false, Graph.Current, f, prev))
        else
            EditInline (f prev)

    static member BindReactive<'K, 'Phase, 'Prev, 'U>
        (source: IReactive<'K>, continuation: 'K -> EditableStep<'Phase, 'Prev, 'U>)
        : EditableStep<BoundPhase, 'Prev, 'U> =
        let run () =
            let key = CeRuntime.ReadCeTracked source
            let saved = InternalCeState.SwapReactiveBind true

            try
                CeRuntime.RunUntrackedContinuation (false, false, source.Graph, key, fun k -> (continuation k).EvalValue())
            finally
                InternalCeState.SwapReactiveBind saved |> ignore

        if InternalCeState.IsEvaluating then
            EditInline (run ())
        else
            EditDynamic (source.Graph, run)

    static member BindPrevVal<'T, 'Phase, 'InnerPrev, 'U>
        (probeValue: 'T, readPrev: unit -> 'T, continuation: 'T -> EditableStep<'Phase, 'InnerPrev, 'U>)
        : EditableStep<'Phase, WithPrev, 'U> =
        if InternalCeState.IsEvaluating then
            EditInline ((continuation (readPrev ())).EvalValue())
        else
            let ambient = CeRuntime.TryGetAmbientGraph ()

            let g =
                if not (Platform.isNull (box ambient)) then
                    ambient
                else
                    (continuation probeValue).TryGraph

            EditDynamic (g, fun () -> (continuation (readPrev ())).EvalValue())

    static member CombineSteps<'P1, 'P2, 'POut, 'Prev, 'U>
        (first: EditableStep<'P1, 'Prev, unit>, second: unit -> EditableStep<'P2, 'Prev, 'U>)
        : EditableStep<'POut, 'Prev, 'U> =
        EditDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                first.EvalValue ()
                (second ()).EvalValue()
        )

    static member WhileStep<'P, 'POut, 'Prev>(guard: unit -> bool, body: unit -> EditableStep<'P, 'Prev, unit>) : EditableStep<'POut, 'Prev, unit> =
        EditDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                while guard () do
                    (body ()).EvalValue()
        )

    static member TryWithStep<'P1, 'P2, 'POut, 'Prev, 'U>
        (body: unit -> EditableStep<'P1, 'Prev, 'U>, handler: exn -> EditableStep<'P2, 'Prev, 'U>)
        : EditableStep<'POut, 'Prev, 'U> =
        EditDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalValue()
                with ex ->
                    CeRuntime.RunTryWithHandler (ex, fun () -> (handler ex).EvalValue())
        )

    static member TryFinallyStep<'P, 'POut, 'Prev, 'U>
        (body: unit -> EditableStep<'P, 'Prev, 'U>, compensation: unit -> unit)
        : EditableStep<'POut, 'Prev, 'U> =
        EditDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalValue()
                finally
                    compensation ()
        )

    static member UsingStep<'D, 'P, 'POut, 'Prev, 'U when 'D :> IDisposable>
        (resource: 'D, body: 'D -> EditableStep<'P, 'Prev, 'U>)
        : EditableStep<'POut, 'Prev, 'U> =
        if InternalCeState.IsProbing then
            CeRuntime.DisposeResource resource
            EditDynamic (Unchecked.defaultof<Graph>, fun () -> CeRuntime.ProbeDisposed ())
        else
            EditDynamic (Unchecked.defaultof<Graph>, fun () -> CeRuntime.RunUsing (resource, fun () -> (body resource).EvalValue()))

    static member ForSeqStep<'T, 'P, 'POut, 'Prev>(sequence: seq<'T>, body: 'T -> EditableStep<'P, 'Prev, unit>) : EditableStep<'POut, 'Prev, unit> =
        EditDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                for item in sequence do
                    (body item).EvalValue()
        )

/// <summary>Step carrier for <c>BoundaryBuilder</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type BoundaryStep<'Phase, 'U> =
    | BoundaryInline of bndVal: 'U
    | BoundaryDynamic of bndGraph: Graph * bndEval: (unit -> 'U)

    member this.EvalValue() =
        match this with
        | BoundaryInline v -> v
        | BoundaryDynamic (_, f) -> f ()

/// <summary>Header descriptor configured via <c>pending</c>, <c>failed</c>, and <c>comparer</c> inside <c>boundary</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type BoundaryHeader<'Pend, 'Fail, 'Cmp> =
    val Pend: 'Pend
    val Fail: 'Fail
    val Cmp: 'Cmp
    new(pend: 'Pend, fail: 'Fail, cmp: 'Cmp) = { Pend = pend; Fail = fail; Cmp = cmp }

/// <summary>Configured <c>boundary</c> step preserving header handlers and phase.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type ConfiguredBoundary<'Pend, 'Fail, 'Cmp, 'Phase, 'U> =
    val Header: BoundaryHeader<'Pend, 'Fail, 'Cmp>
    val Body: unit -> BoundaryStep<'Phase, 'U>
    new(header: BoundaryHeader<'Pend, 'Fail, 'Cmp>, body: unit -> BoundaryStep<'Phase, 'U>) = { Header = header; Body = body }

/// <summary>Static runtime helpers for <c>BoundaryBuilder</c>.</summary>
[<AbstractClass; Sealed; EditorBrowsable(EditorBrowsableState.Never)>]
type BoundaryRunner =
    static member EvalBound<'U>
        (
            d: unit -> BoundaryStep<BoundPhase, 'U>,
            onPending: ('U voption -> 'U) voption,
            onError: (exn -> 'U voption -> 'U) voption,
            comparer: IEqualityComparer<'U> voption
        ) : Boundary<'U> =
        CeRuntime.SuspendNested (fun () ->
            match CeRuntime.ProbeBound d with
            | BoundaryDynamic (g, _) -> CeRuntime.CreateBoundary (g, (fun () -> (d ()).EvalValue()), onPending, onError, comparer)
            | BoundaryInline _ -> CeRuntime.CreateBoundary (Unchecked.defaultof<Graph>, (fun () -> (d ()).EvalValue()), onPending, onError, comparer))

    static member EvalCold<'U>
        (
            d: unit -> BoundaryStep<ColdPhase, 'U>,
            onPending: ('U voption -> 'U) voption,
            onError: (exn -> 'U voption -> 'U) voption,
            comparer: IEqualityComparer<'U> voption
        ) : Boundary<'U> =
        CeRuntime.CreateBoundary (Unchecked.defaultof<Graph>, (fun () -> (d ()).EvalValue()), onPending, onError, comparer)

    static member UsingStep<'D, 'P, 'POut, 'U when 'D :> IDisposable>(resource: 'D, body: 'D -> BoundaryStep<'P, 'U>) : BoundaryStep<'POut, 'U> =
        if InternalCeState.IsProbing then
            CeRuntime.DisposeResource resource
            BoundaryDynamic (Unchecked.defaultof<Graph>, fun () -> CeRuntime.ProbeDisposed ())
        else
            BoundaryDynamic (Unchecked.defaultof<Graph>, fun () -> CeRuntime.RunUsing (resource, fun () -> (body resource).EvalValue()))

    static member BindReturnReactive<'K, 'U>(source: IReactive<'K>, f: 'K -> 'U) : BoundaryStep<BoundPhase, 'U> =
        let run () =
            let key = CeRuntime.ReadCeTracked source
            CeRuntime.RunUntrackedBoundaryArg (source.Graph, f, key)

        if InternalCeState.IsEvaluating then
            BoundaryInline (run ())
        else
            BoundaryDynamic (source.Graph, run)

    static member BindReactive<'K, 'Phase, 'U>(source: IReactive<'K>, continuation: 'K -> BoundaryStep<'Phase, 'U>) : BoundaryStep<BoundPhase, 'U> =
        let run () =
            let key = CeRuntime.ReadCeTracked source
            CeRuntime.RunUntrackedContinuation (false, true, source.Graph, key, fun k -> (continuation k).EvalValue())

        if InternalCeState.IsEvaluating then
            BoundaryInline (run ())
        else
            BoundaryDynamic (source.Graph, run)

/// <summary>Step carrier for <c>RowBuilder</c> returning <c>unit -&gt; 'V</c> without extra nodes.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type RowStep<'Phase, 'V> =
    | RowReadyVal of rowVal: 'V
    | RowThunk of rowEval: (unit -> 'V)

    member this.EvalValue() =
        match this with
        | RowReadyVal v -> v
        | RowThunk f -> f ()

/// <summary>Result of evaluating one step in <c>AsyncMemoBuilder</c> before forcing a <c>Task</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type AsyncEval<'U> =
    | AsyncEvalSync of syncVal: 'U
    | AsyncEvalTask of asyncTask: Task<'U>

/// <summary>Static factory bridge allowing <c>AsyncStatic</c> to wrap context tokens while preserving <c>CreateOn</c> and <c>keyComparer</c>.</summary>
[<EditorBrowsable(EditorBrowsableState.Never)>]
type IAsyncStaticFactory<'U> =
    abstract Graph: Graph
    abstract Create: owning: bool * policy: FlightPolicy voption -> AsyncMemo<'U>
    abstract WithContext: extract: (Previous<'U> -> CancellationToken -> IAsyncStaticFactory<'U>) -> IAsyncStaticFactory<'U>

/// <summary>Static factory implementation dispatching single or merged reactive <c>asyncMemo</c> blocks to <c>AsyncMemo.CreateOn</c>.</summary>
[<Sealed; EditorBrowsable(EditorBrowsableState.Never)>]
type AsyncOnFactory<'K, 'U>(source: IReactive<'K>, buildWithKey: 'K -> Previous<'U> -> CancellationToken -> Task<'U>) =
    member _.InvokeKey(k: 'K, prev: Previous<'U>, ct: CancellationToken) : Task<'U> =
        buildWithKey k prev ct

    interface IAsyncStaticFactory<'U> with
        member _.Graph = source.Graph

        member _.Create(owning, policy) =
            CeRuntime.CreateAsyncFromReactive (source, buildWithKey, owning, policy)

        member _.WithContext(extract) =
            AsyncOnFactory<'K, 'U>(
                source,
                fun k prev ct ->
                    let nextFactory =
                        CeRuntime.SuspendNested (fun () -> CeRuntime.ProbeBound (fun () -> extract prev ct))

                    (unbox<AsyncOnFactory<'K, 'U>>(box nextFactory)).InvokeKey(k, prev, ct)
            )
            :> IAsyncStaticFactory<'U>

/// <summary>Two-phase step carrier for <c>AsyncMemoBuilder</c> enforcing compile-time await ordering.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type AsyncStep<'Leaf, 'Phase, 'U> =
    | AsyncInline of inlineVal: 'U
    | AsyncDynamic of dynGraph: Graph * dynEval: (Previous<'U> -> CancellationToken -> AsyncEval<'U>)
    | AsyncStatic of staticFactory: IAsyncStaticFactory<'U> * staticEval: (Previous<'U> -> CancellationToken -> AsyncEval<'U>)

    member inline this.TryGraph =
        match this with
        | AsyncInline _ -> Unchecked.defaultof<Graph>
        | AsyncDynamic (g, _) -> g
        | AsyncStatic (f, _) -> f.Graph

    member this.EvalAsync(prev: Previous<'U>, ct: CancellationToken) : AsyncEval<'U> =
        match this with
        | AsyncInline v -> AsyncEvalSync v
        | AsyncDynamic (_, f) -> f prev ct
        | AsyncStatic (_, f) -> f prev ct

/// <summary>Header descriptor configured via <c>owning</c> and <c>policy</c> inside <c>asyncMemo</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type AsyncHeader<'Mode, 'Pol> =
    val Pol: 'Pol
    new(pol: 'Pol) = { Pol = pol }

/// <summary>Configured <c>asyncMemo</c> step preserving header mode, flight policy, leaf state, and phase.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type ConfiguredAsync<'Mode, 'Pol, 'Leaf, 'Phase, 'U> =
    val Header: AsyncHeader<'Mode, 'Pol>
    val Body: unit -> AsyncStep<'Leaf, 'Phase, 'U>
    new(header: AsyncHeader<'Mode, 'Pol>, body: unit -> AsyncStep<'Leaf, 'Phase, 'U>) = { Header = header; Body = body }

/// <summary>Static builder support for <c>AsyncMemoBuilder</c> two-phase execution.</summary>
[<AbstractClass; Sealed; EditorBrowsable(EditorBrowsableState.Never)>]
type AsyncRunner =
    static member private DummyPrevious<'U>() : Previous<'U> =
        Previous<'U>(Unchecked.defaultof<IPreviousSource<'U>>, 0)

#if FABLE_COMPILER
    // InlineTask in Fable test delivery implements `.then(onResolved, onRejected)` without `.finally(...)`,
    // so run compensation after `Platform.outcomeOf` instead of lowering through `TaskBuilder.TryFinally`.
    static member private RunAfterTask<'U>(t: Task<'U>, compensation: unit -> unit) : Task<'U> =
        task {
            let! outcome = Platform.outcomeOf t
            compensation ()

            match outcome with
            | Platform.FlightOutcome.Completed v -> return v
            | Platform.FlightOutcome.Faulted ex
            | Platform.FlightOutcome.Canceled ex -> return raise ex
        }
#endif

    static member EvalTask<'U>(run: unit -> AsyncEval<'U>) : Task<'U> =
        match run () with
        | AsyncEvalSync v -> Task.FromResult v
        | AsyncEvalTask t -> t

    static member CreateDynamicAsync<'U>
        (graph: Graph, eval: Previous<'U> -> CancellationToken -> AsyncEval<'U>, owning: bool, policy: FlightPolicy voption)
        : AsyncMemo<'U> =
        let mode = if owning then ScopeMode.Owning else ScopeMode.PureAsync

        let compute (prev: Previous<'U>) (ct: CancellationToken) : Task<'U> =
            let savedDepth = InternalCeState.EnterEval ()
            let savedReactive = InternalCeState.SwapReactiveBind false
            let savedOwning = InternalCeState.SwapOwning owning
            let savedObs = InternalCeState.SwapEffectObserver (graph, box graph.CurrentObserver)
            let savedPending = InternalCeState.SwapPhase1Pending null
            let savedPrev = InternalCeState.SuspendPrevious ()
            let savedProbing = InternalCeState.SwapProbing false

            try
                match CeRuntime.RunAsyncUntrackedContinuation (owning, graph, (), fun () -> eval prev ct) with
                | AsyncEvalSync v -> Task.FromResult v
                | AsyncEvalTask t -> CeRuntime.WrapAsyncPending t
            finally
                InternalCeState.SwapProbing savedProbing |> ignore
                InternalCeState.RestorePrevious savedPrev

                InternalCeState.SwapPhase1Pending savedPending
                |> ignore

                InternalCeState.RestoreEffectObserver savedObs
                InternalCeState.SwapOwning savedOwning |> ignore

                InternalCeState.SwapReactiveBind savedReactive
                |> ignore

                InternalCeState.ExitEval savedDepth

        graph.Entered (
            "Creating a node",
            fun () ->
                match policy with
                | ValueSome p -> AsyncMemo<'U>.Create(graph, compute, mode, p)
                | ValueNone -> AsyncMemo<'U>.Create(graph, compute, mode)
        )

    static member ExtractStaticFactory<'Leaf, 'Phase, 'U>(step: AsyncStep<'Leaf, 'Phase, 'U>) : IAsyncStaticFactory<'U> =
        match step with
        | AsyncStatic (factory, _) -> factory
        | _ -> invalidOp "Unexpected non-static AsyncStep during context extraction."

    static member EvalBound<'Leaf, 'U>(d: unit -> AsyncStep<'Leaf, BoundPhase, 'U>, owning: bool, policy: FlightPolicy voption) : AsyncMemo<'U> =
        CeRuntime.SuspendNested (fun () ->
            match CeRuntime.ProbeBound d with
            | AsyncStatic (factory, _) -> factory.Create (owning, policy)
            | AsyncDynamic (g, _) ->
                let graph = if Platform.isNull (box g) then Graph.Current else g
                AsyncRunner.CreateDynamicAsync (graph, (fun prev ct -> (d ()).EvalAsync(prev, ct)), owning, policy)
            | AsyncInline _ ->
                let graph = Graph.Current
                AsyncRunner.CreateDynamicAsync (graph, (fun prev ct -> (d ()).EvalAsync(prev, ct)), owning, policy))

    static member EvalCold<'Leaf, 'U>(d: unit -> AsyncStep<'Leaf, ColdPhase, 'U>, owning: bool, policy: FlightPolicy voption) : AsyncMemo<'U> =
        AsyncRunner.CreateDynamicAsync (Graph.Current, (fun prev ct -> (d ()).EvalAsync(prev, ct)), owning, policy)

    static member ReturnTask<'Leaf, 'Phase, 'U>(t: Task<'U>) : AsyncStep<'Leaf, 'Phase, 'U> =
        AsyncDynamic (Unchecked.defaultof<Graph>, fun _ _ -> AsyncEvalTask t)

    static member ReturnUnitTask(t: Task) : AsyncStep<'Leaf, 'Phase, unit> =
#if !FABLE_COMPILER
        if t.IsCompletedSuccessfully then
            AsyncInline ()
        else
            AsyncDynamic (
                Unchecked.defaultof<Graph>,
                fun _ _ ->
                    AsyncEvalTask (
                        task {
                            do! t
                            return ()
                        }
                    )
            )
#else
        AsyncDynamic (
            Unchecked.defaultof<Graph>,
            fun _ _ ->
                AsyncEvalTask (
                    task {
                        do! t
                        return ()
                    }
                )
        )
#endif

#if !FABLE_COMPILER
    static member ReturnValueTask<'Leaf, 'Phase, 'U>(vt: ValueTask<'U>) : AsyncStep<'Leaf, 'Phase, 'U> =
        AsyncDynamic (
            Unchecked.defaultof<Graph>,
            fun _ _ ->
                if vt.IsCompletedSuccessfully then
                    AsyncEvalSync vt.Result
                else
                    AsyncEvalTask (vt.AsTask ())
        )

    static member ReturnUnitValueTask(vt: ValueTask) : AsyncStep<'Leaf, 'Phase, unit> =
        AsyncDynamic (
            Unchecked.defaultof<Graph>,
            fun _ _ ->
                if vt.IsCompletedSuccessfully then
                    vt.GetAwaiter().GetResult()
                    AsyncEvalSync ()
                else
                    let t = vt.AsTask ()

                    AsyncEvalTask (
                        task {
                            do! t
                            return ()
                        }
                    )
        )
#endif

    static member EvalCombine<'L1, 'P1, 'L2, 'P2, 'U>
        (first: AsyncStep<'L1, 'P1, unit>, second: unit -> AsyncStep<'L2, 'P2, 'U>, prev: Previous<'U>, ct: CancellationToken)
        : AsyncEval<'U> =
        match first.EvalAsync (AsyncRunner.DummyPrevious<unit>(), ct) with
        | AsyncEvalSync () -> (second ()).EvalAsync(prev, ct)
        | AsyncEvalTask t1 ->
#if !FABLE_COMPILER
            if t1.IsCompletedSuccessfully then
                (second ()).EvalAsync(prev, ct)
            else
                AsyncEvalTask (
                    task {
                        let! () = t1
                        return! AsyncRunner.EvalTask (fun () -> (second ()).EvalAsync(prev, ct))
                    }
                )
#else
            AsyncEvalTask (
                task {
                    let! () = t1
                    return! AsyncRunner.EvalTask (fun () -> (second ()).EvalAsync(prev, ct))
                }
            )
#endif

    static member private WhileAsyncLoop<'L, 'P>
        (guard: unit -> bool, body: unit -> AsyncStep<'L, 'P, unit>, pendingFirst: Task<unit>, ct: CancellationToken)
        : Task<unit> =
        task {
            let! () = pendingFirst
            let dummyPrev = AsyncRunner.DummyPrevious<unit>()

            while guard () do
                let! () = AsyncRunner.EvalTask (fun () -> (body ()).EvalAsync(dummyPrev, ct))
                ()

            return ()
        }

    static member EvalWhile<'L, 'P>(guard: unit -> bool, body: unit -> AsyncStep<'L, 'P, unit>, ct: CancellationToken) : AsyncEval<unit> =
        let mutable pending: Task<unit> = Unchecked.defaultof<Task<unit>>
        let dummyPrev = AsyncRunner.DummyPrevious<unit>()

        while Platform.isNull pending && guard () do
            match (body ()).EvalAsync(dummyPrev, ct) with
            | AsyncEvalSync () -> ()
            | AsyncEvalTask t ->
#if !FABLE_COMPILER
                if not t.IsCompletedSuccessfully then
                    pending <- t
#else
                pending <- t
#endif

        if Platform.isNull pending then
            AsyncEvalSync ()
        else
            AsyncEvalTask (AsyncRunner.WhileAsyncLoop (guard, body, pending, ct))

    static member EvalTryWith<'L1, 'P1, 'L2, 'P2, 'U>
        (body: unit -> AsyncStep<'L1, 'P1, 'U>, handler: exn -> AsyncStep<'L2, 'P2, 'U>, prev: Previous<'U>, ct: CancellationToken)
        : AsyncEval<'U> =
        try
            match (body ()).EvalAsync(prev, ct) with
            | AsyncEvalSync _ as syncVal -> syncVal
            | AsyncEvalTask t ->
#if !FABLE_COMPILER
                if t.IsCompletedSuccessfully then
                    AsyncEvalSync t.Result
                else
                    AsyncEvalTask (
                        task {
                            try
                                return! t
                            with ex ->
                                return! AsyncRunner.EvalTask (fun () -> CeRuntime.RunTryWithHandler (ex, fun () -> (handler ex).EvalAsync(prev, ct)))
                        }
                    )
#else
                AsyncEvalTask (
                    task {
                        try
                            return! t
                        with ex ->
                            return! AsyncRunner.EvalTask (fun () -> CeRuntime.RunTryWithHandler (ex, fun () -> (handler ex).EvalAsync(prev, ct)))
                    }
                )
#endif
        with ex ->
            CeRuntime.RunTryWithHandler (ex, fun () -> (handler ex).EvalAsync(prev, ct))

    static member EvalTryFinally<'L, 'P, 'U>
        (body: unit -> AsyncStep<'L, 'P, 'U>, compensation: unit -> unit, prev: Previous<'U>, ct: CancellationToken)
        : AsyncEval<'U> =
        let mutable handoff = false

        try
            let eval = (body ()).EvalAsync(prev, ct)

            match eval with
            | AsyncEvalSync _ -> eval
            | AsyncEvalTask t ->
#if !FABLE_COMPILER
                if t.IsCompletedSuccessfully then
                    AsyncEvalSync t.Result
                else
                    handoff <- true

                    AsyncEvalTask (
                        task {
                            try
                                return! t
                            finally
                                compensation ()
                        }
                    )
#else
                handoff <- true
                AsyncEvalTask (AsyncRunner.RunAfterTask (t, compensation))
#endif
        finally
            if not handoff then
                compensation ()

    static member EvalUsing<'D, 'L, 'P, 'U when 'D :> IDisposable>
        (resource: 'D, body: 'D -> AsyncStep<'L, 'P, 'U>, prev: Previous<'U>, ct: CancellationToken)
        : AsyncEval<'U> =
        let mutable handoff = false

        try
            let eval = (body resource).EvalAsync(prev, ct)

            match eval with
            | AsyncEvalSync _ -> eval
            | AsyncEvalTask t ->
#if !FABLE_COMPILER
                if t.IsCompletedSuccessfully then
                    AsyncEvalSync t.Result
                else
                    handoff <- true

                    AsyncEvalTask (
                        task {
                            try
                                return! t
                            finally
                                CeRuntime.DisposeResource resource
                        }
                    )
#else
                handoff <- true
                AsyncEvalTask (AsyncRunner.RunAfterTask (t, fun () -> CeRuntime.DisposeResource resource))
#endif
        finally
            if not handoff then
                CeRuntime.DisposeResource resource

    static member UsingStep<'D, 'InLeaf, 'InPhase, 'OutLeaf, 'OutPhase, 'U when 'D :> IDisposable>
        (resource: 'D, body: 'D -> AsyncStep<'InLeaf, 'InPhase, 'U>)
        : AsyncStep<'OutLeaf, 'OutPhase, 'U> =
        if InternalCeState.IsProbing then
            CeRuntime.DisposeResource resource
            AsyncDynamic (Unchecked.defaultof<Graph>, fun _ _ -> CeRuntime.ProbeDisposed ())
        else
            AsyncDynamic (Unchecked.defaultof<Graph>, fun prev ct -> AsyncRunner.EvalUsing (resource, body, prev, ct))

    static member private ForSeqAsyncLoop<'T, 'L, 'P>
        (enumerator: IEnumerator<'T>, body: 'T -> AsyncStep<'L, 'P, unit>, pendingFirst: Task<unit>, ct: CancellationToken)
        : Task<unit> =
#if FABLE_COMPILER
        let bodyTask =
            task {
                let! () = pendingFirst
                let dummyPrev = AsyncRunner.DummyPrevious<unit>()

                while enumerator.MoveNext () do
                    let! () =
                        AsyncRunner.EvalTask (fun () -> (body enumerator.Current).EvalAsync(dummyPrev, ct))

                    ()

                return ()
            }

        AsyncRunner.RunAfterTask (bodyTask, fun () -> CeRuntime.DisposeResource enumerator)
#else
        task {
            try
                let! () = pendingFirst
                let dummyPrev = AsyncRunner.DummyPrevious<unit>()

                while enumerator.MoveNext () do
                    let! () =
                        AsyncRunner.EvalTask (fun () -> (body enumerator.Current).EvalAsync(dummyPrev, ct))

                    ()

                return ()
            finally
                CeRuntime.DisposeResource enumerator
        }
#endif

    static member EvalForSeq<'T, 'L, 'P>(sequence: seq<'T>, body: 'T -> AsyncStep<'L, 'P, unit>, ct: CancellationToken) : AsyncEval<unit> =
        let enumerator = sequence.GetEnumerator ()
        let mutable handoff = false

        try
            let mutable pending: Task<unit> = Unchecked.defaultof<Task<unit>>
            let dummyPrev = AsyncRunner.DummyPrevious<unit>()

            while Platform.isNull pending && enumerator.MoveNext () do
                match (body enumerator.Current).EvalAsync(dummyPrev, ct) with
                | AsyncEvalSync () -> ()
                | AsyncEvalTask t ->
#if !FABLE_COMPILER
                    if not t.IsCompletedSuccessfully then
                        pending <- t
#else
                    pending <- t
#endif

            if Platform.isNull pending then
                AsyncEvalSync ()
            else
                handoff <- true
                AsyncEvalTask (AsyncRunner.ForSeqAsyncLoop (enumerator, body, pending, ct))
        finally
            if not handoff then
                CeRuntime.DisposeResource enumerator

    static member BindReturnReactive<'K, 'U>(source: IReactive<'K>, f: 'K -> 'U) : AsyncStep<NotLeaf, BoundPhase, 'U> =
        if InternalCeState.IsEvaluating then
            let key = CeRuntime.ReadCeTracked source
            AsyncInline (CeRuntime.RunAsyncUntrackedArg (InternalCeState.IsOwning, source.Graph, f, key))
        else
            let build (k: 'K) (_prev: Previous<'U>) (_ct: CancellationToken) : Task<'U> =
                Task.FromResult (CeRuntime.SuspendNested (fun () -> f k))

            let evalDyn (_prev: Previous<'U>) (_ct: CancellationToken) : AsyncEval<'U> =
                let key = CeRuntime.ReadCeTracked source
                AsyncEvalSync (CeRuntime.RunAsyncUntrackedArg (InternalCeState.IsOwning, source.Graph, f, key))

            AsyncStatic (AsyncOnFactory<'K, 'U>(source, build) :> IAsyncStaticFactory<'U>, evalDyn)

    static member BindReactiveStatic<'K, 'U>
        (source: IReactive<'K>, continuation: 'K -> AsyncStep<IsLeaf, ColdPhase, 'U>)
        : AsyncStep<NotLeaf, BoundPhase, 'U> =
        let evalDyn (prev: Previous<'U>) (ct: CancellationToken) : AsyncEval<'U> =
            let key = CeRuntime.ReadCeTracked source
            CeRuntime.RunAsyncUntrackedContinuation (InternalCeState.IsOwning, source.Graph, key, fun k -> (continuation k).EvalAsync(prev, ct))

        if InternalCeState.IsEvaluating then
            AsyncDynamic (source.Graph, evalDyn)
        else
            let build (k: 'K) (prev: Previous<'U>) (ct: CancellationToken) : Task<'U> =
                CeRuntime.SuspendNested (fun () -> AsyncRunner.EvalTask (fun () -> (continuation k).EvalAsync(prev, ct)))

            AsyncStatic (AsyncOnFactory<'K, 'U>(source, build) :> IAsyncStaticFactory<'U>, evalDyn)

    static member BindReactiveDynamic<'K, 'AnyLeaf, 'ContPhase, 'U>
        (source: IReactive<'K>, continuation: 'K -> AsyncStep<'AnyLeaf, 'ContPhase, 'U>)
        : AsyncStep<NotLeaf, BoundPhase, 'U> =
        let evalDyn (prev: Previous<'U>) (ct: CancellationToken) : AsyncEval<'U> =
            let key = CeRuntime.ReadCeTracked source
            let saved = InternalCeState.SwapReactiveBind true

            try
                CeRuntime.RunAsyncUntrackedContinuation (InternalCeState.IsOwning, source.Graph, key, fun k -> (continuation k).EvalAsync(prev, ct))
            finally
                InternalCeState.SwapReactiveBind saved |> ignore

        AsyncDynamic (source.Graph, evalDyn)

    static member BindPrevious<'Leaf, 'Phase, 'U>(continuation: Previous<'U> -> AsyncStep<'Leaf, 'Phase, 'U>) : AsyncStep<'Leaf, 'Phase, 'U> =
        if InternalCeState.IsProbing then
            let probed = continuation (AsyncRunner.DummyPrevious<'U>())

            match probed with
            | AsyncStatic (factory, _) ->
                AsyncStatic (
                    factory.WithContext (fun prev _ -> AsyncRunner.ExtractStaticFactory (continuation prev)),
                    fun prev ct -> (continuation prev).EvalAsync(prev, ct)
                )
            | _ ->
                let probedGraph = probed.TryGraph

                let resolvedGraph =
                    if not (Platform.isNull (box probedGraph)) then
                        probedGraph
                    else
                        CeRuntime.TryGetAmbientGraph ()

                AsyncDynamic (resolvedGraph, fun prev ct -> (continuation prev).EvalAsync(prev, ct))
        else
            AsyncDynamic (Unchecked.defaultof<Graph>, fun prev ct -> (continuation prev).EvalAsync(prev, ct))

    static member BindCancellationToken<'Leaf, 'Phase, 'U>
        (continuation: CancellationToken -> AsyncStep<'Leaf, 'Phase, 'U>)
        : AsyncStep<'Leaf, 'Phase, 'U> =
        if InternalCeState.IsProbing then
            let probed = continuation CancellationToken.None

            match probed with
            | AsyncStatic (factory, _) ->
                AsyncStatic (
                    factory.WithContext (fun _ ct -> AsyncRunner.ExtractStaticFactory (continuation ct)),
                    fun prev ct -> (continuation ct).EvalAsync(prev, ct)
                )
            | _ ->
                let probedGraph = probed.TryGraph

                let resolvedGraph =
                    if not (Platform.isNull (box probedGraph)) then
                        probedGraph
                    else
                        CeRuntime.TryGetAmbientGraph ()

                AsyncDynamic (resolvedGraph, fun prev ct -> (continuation ct).EvalAsync(prev, ct))
        else
            AsyncDynamic (Unchecked.defaultof<Graph>, fun prev ct -> (continuation ct).EvalAsync(prev, ct))

    static member EvalBindTaskReturn<'A, 'U>(source: Task<'A>, f: 'A -> 'U) : AsyncEval<'U> =
#if !FABLE_COMPILER
        if source.IsCompletedSuccessfully then
            AsyncEvalSync (CeRuntime.RunAsyncUntracked (f, source.Result))
        else
            AsyncEvalTask (
                task {
                    let! result = source
                    return CeRuntime.RunAsyncUntracked (f, result)
                }
            )
#else
        AsyncEvalTask (
            task {
                let! result = source
                return CeRuntime.RunAsyncUntracked (f, result)
            }
        )
#endif

    static member EvalBindTask<'A, 'Leaf, 'U>
        (source: Task<'A>, continuation: 'A -> AsyncStep<'Leaf, ColdPhase, 'U>, prev: Previous<'U>, ct: CancellationToken)
        : AsyncEval<'U> =
#if !FABLE_COMPILER
        if source.IsCompletedSuccessfully then
            CeRuntime.RunAsyncUntracked ((fun x -> (continuation x).EvalAsync(prev, ct)), source.Result)
        else
            AsyncEvalTask (
                task {
                    let! result = source
                    return! AsyncRunner.EvalTask (fun () -> CeRuntime.RunAsyncUntracked ((fun x -> (continuation x).EvalAsync(prev, ct)), result))
                }
            )
#else
        AsyncEvalTask (
            task {
                let! result = source
                return! AsyncRunner.EvalTask (fun () -> CeRuntime.RunAsyncUntracked ((fun x -> (continuation x).EvalAsync(prev, ct)), result))
            }
        )
#endif

    static member EvalBindUnitTaskReturn<'U>(source: Task, f: unit -> 'U) : AsyncEval<'U> =
#if !FABLE_COMPILER
        if source.IsCompletedSuccessfully then
            AsyncEvalSync (CeRuntime.RunAsyncUntracked (f, ()))
        else
            AsyncEvalTask (
                task {
                    do! source
                    return CeRuntime.RunAsyncUntracked (f, ())
                }
            )
#else
        AsyncEvalTask (
            task {
                do! source
                return CeRuntime.RunAsyncUntracked (f, ())
            }
        )
#endif

    static member EvalBindUnitTask<'Leaf, 'U>
        (source: Task, continuation: unit -> AsyncStep<'Leaf, ColdPhase, 'U>, prev: Previous<'U>, ct: CancellationToken)
        : AsyncEval<'U> =
#if !FABLE_COMPILER
        if source.IsCompletedSuccessfully then
            CeRuntime.RunAsyncUntracked ((fun () -> (continuation ()).EvalAsync(prev, ct)), ())
        else
            AsyncEvalTask (
                task {
                    do! source
                    return! AsyncRunner.EvalTask (fun () -> CeRuntime.RunAsyncUntracked ((fun () -> (continuation ()).EvalAsync(prev, ct)), ()))
                }
            )
#else
        AsyncEvalTask (
            task {
                do! source
                return! AsyncRunner.EvalTask (fun () -> CeRuntime.RunAsyncUntracked ((fun () -> (continuation ()).EvalAsync(prev, ct)), ()))
            }
        )
#endif

#if !FABLE_COMPILER
    static member EvalBindValueTaskReturn<'A, 'U>(source: ValueTask<'A>, f: 'A -> 'U) : AsyncEval<'U> =
        if source.IsCompletedSuccessfully then
            AsyncEvalSync (CeRuntime.RunAsyncUntracked (f, source.Result))
        else
            let t = source.AsTask ()

            AsyncEvalTask (
                task {
                    let! result = t
                    return CeRuntime.RunAsyncUntracked (f, result)
                }
            )

    static member EvalBindValueTask<'A, 'Leaf, 'U>
        (source: ValueTask<'A>, continuation: 'A -> AsyncStep<'Leaf, ColdPhase, 'U>, prev: Previous<'U>, ct: CancellationToken)
        : AsyncEval<'U> =
        if source.IsCompletedSuccessfully then
            CeRuntime.RunAsyncUntracked ((fun x -> (continuation x).EvalAsync(prev, ct)), source.Result)
        else
            let t = source.AsTask ()

            AsyncEvalTask (
                task {
                    let! result = t
                    return! AsyncRunner.EvalTask (fun () -> CeRuntime.RunAsyncUntracked ((fun x -> (continuation x).EvalAsync(prev, ct)), result))
                }
            )

    static member EvalBindUnitValueTaskReturn<'U>(source: ValueTask, f: unit -> 'U) : AsyncEval<'U> =
        if source.IsCompletedSuccessfully then
            source.GetAwaiter().GetResult()
            AsyncEvalSync (CeRuntime.RunAsyncUntracked (f, ()))
        else
            let t = source.AsTask ()

            AsyncEvalTask (
                task {
                    do! t
                    return CeRuntime.RunAsyncUntracked (f, ())
                }
            )

    static member EvalBindUnitValueTask<'Leaf, 'U>
        (source: ValueTask, continuation: unit -> AsyncStep<'Leaf, ColdPhase, 'U>, prev: Previous<'U>, ct: CancellationToken)
        : AsyncEval<'U> =
        if source.IsCompletedSuccessfully then
            source.GetAwaiter().GetResult()
            CeRuntime.RunAsyncUntracked ((fun () -> (continuation ()).EvalAsync(prev, ct)), ())
        else
            let t = source.AsTask ()

            AsyncEvalTask (
                task {
                    do! t
                    return! AsyncRunner.EvalTask (fun () -> CeRuntime.RunAsyncUntracked ((fun () -> (continuation ()).EvalAsync(prev, ct)), ()))
                }
            )
#endif

/// <summary>Result of evaluating one step in <c>StreamBuilder</c>, distinguishing <c>unit</c> statements from <c>StreamSource</c> values.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type StreamEval<'T> =
    | StreamEvalUnit
    | StreamEvalSource of streamSource: StreamSource<'T>

/// <summary>Static factory bridge allowing <c>StreamStatic</c> to construct either latest-item or folded <c>StreamMemo</c> nodes.</summary>
[<EditorBrowsable(EditorBrowsableState.Never)>]
type IStreamStaticFactory<'T> =
    abstract Graph: Graph
    abstract CreateLatest: unit -> StreamMemo<'T>
    abstract CreateFold: seed: 'S * folder: ('S -> 'T -> 'S) -> StreamMemo<'S>
    abstract WithContext: extract: (CancellationToken -> IStreamStaticFactory<'T>) -> IStreamStaticFactory<'T>

/// <summary>Static factory implementation dispatching single or merged reactive <c>stream</c> blocks to <c>CreateOn</c>/<c>CreateFoldOn</c>.</summary>
[<Sealed; EditorBrowsable(EditorBrowsableState.Never)>]
type StreamOnFactory<'K, 'T>(source: IReactive<'K>, build: 'K -> CancellationToken -> StreamSource<'T>) =
    member _.InvokeKey(k: 'K, ct: CancellationToken) : StreamSource<'T> =
        build k ct

    interface IStreamStaticFactory<'T> with
        member _.Graph = source.Graph

        member _.CreateLatest() =
            let g = source.Graph

            g.Entered (
                "Creating a node",
                fun () ->
                    match source with
                    | :? ComparedReactive<'K> as cr -> StreamMemo<'T>.CreateOn(g, cr.Source.ReadTracked, build, keyComparer = cr.Comparer)
                    | _ -> StreamMemo<'T>.CreateOn(g, source.ReadTracked, build)
            )

        member _.CreateFold<'S>(seed: 'S, folder: 'S -> 'T -> 'S) =
            let g = source.Graph

            g.Entered (
                "Creating a node",
                fun () ->
                    match source with
                    | :? ComparedReactive<'K> as cr ->
                        StreamMemo<'S>.CreateFoldOn(g, seed, folder, cr.Source.ReadTracked, build, keyComparer = cr.Comparer)
                    | _ -> StreamMemo<'S>.CreateFoldOn(g, seed, folder, source.ReadTracked, build)
            )

        member _.WithContext(extract) =
            StreamOnFactory<'K, 'T>(
                source,
                fun k ct ->
                    let nextFactory =
                        CeRuntime.SuspendNested (fun () -> CeRuntime.ProbeBound (fun () -> extract ct))

                    (unbox<StreamOnFactory<'K, 'T>>(box nextFactory)).InvokeKey(k, ct)
            )
            :> IStreamStaticFactory<'T>

/// <summary>Two-phase step carrier for <c>StreamBuilder</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type StreamStep<'Leaf, 'Phase, 'T> =
    | StreamUnit
    | StreamReady of readySource: StreamSource<'T>
    | StreamDynamic of streamGraph: Graph * streamEval: (CancellationToken -> StreamEval<'T>)
    | StreamStatic of streamFactory: IStreamStaticFactory<'T> * streamDynEval: (CancellationToken -> StreamEval<'T>)

    member inline this.TryGraph =
        match this with
        | StreamUnit -> Unchecked.defaultof<Graph>
        | StreamReady _ -> Unchecked.defaultof<Graph>
        | StreamDynamic (g, _) -> g
        | StreamStatic (f, _) -> f.Graph

    member this.EvalResult(ct: CancellationToken) : StreamEval<'T> =
        match this with
        | StreamUnit -> StreamEvalUnit
        | StreamReady src -> StreamEvalSource src
        | StreamDynamic (_, f) -> f ct
        | StreamStatic (_, f) -> f ct

    member this.EvalUnit(ct: CancellationToken) : unit =
        this.EvalResult ct |> ignore

    member this.EvalSource(ct: CancellationToken) : StreamSource<'T> =
        match this.EvalResult ct with
        | StreamEvalSource src -> src
        | StreamEvalUnit -> invalidOp "Stream computation expression did not return a StreamSource."

/// <summary>Header descriptor configured via <c>fold</c> inside <c>stream</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type StreamHeader<'Mode> =
    val Mode: 'Mode
    new(mode: 'Mode) = { Mode = mode }

/// <summary>Configured <c>stream</c> step preserving latest vs fold mode, leaf state, and phase.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type ConfiguredStream<'Mode, 'Leaf, 'Phase, 'T> =
    val Header: StreamHeader<'Mode>
    val Body: unit -> StreamStep<'Leaf, 'Phase, 'T>
    new(header: StreamHeader<'Mode>, body: unit -> StreamStep<'Leaf, 'Phase, 'T>) = { Header = header; Body = body }

/// <summary>Static builder support for <c>StreamBuilder</c> execution.</summary>
[<AbstractClass; Sealed; EditorBrowsable(EditorBrowsableState.Never)>]
type StreamRunner =
    static member EvalBoundLatest<'Leaf, 'T>(d: unit -> StreamStep<'Leaf, BoundPhase, 'T>) : StreamMemo<'T> =
        CeRuntime.SuspendNested (fun () ->
            match CeRuntime.ProbeBound d with
            | StreamStatic (factory, _) -> factory.CreateLatest ()
            | StreamDynamic (g, _) ->
                let graph = if Platform.isNull (box g) then Graph.Current else g
                CeRuntime.CreateDynamicStreamLatest (graph, fun ct -> (d ()).EvalSource(ct))
            | StreamReady _
            | StreamUnit -> CeRuntime.CreateDynamicStreamLatest (Graph.Current, fun ct -> (d ()).EvalSource(ct)))

    static member EvalBoundFold<'S, 'Leaf, 'T>(d: unit -> StreamStep<'Leaf, BoundPhase, 'T>, seed: 'S, folder: 'S -> 'T -> 'S) : StreamMemo<'S> =
        CeRuntime.SuspendNested (fun () ->
            match CeRuntime.ProbeBound d with
            | StreamStatic (factory, _) -> factory.CreateFold<'S>(seed, folder)
            | StreamDynamic (g, _) ->
                let graph = if Platform.isNull (box g) then Graph.Current else g
                CeRuntime.CreateDynamicStreamFold (graph, seed, folder, fun ct -> (d ()).EvalSource(ct))
            | StreamReady _
            | StreamUnit -> CeRuntime.CreateDynamicStreamFold (Graph.Current, seed, folder, fun ct -> (d ()).EvalSource(ct)))

    static member EvalColdLatest<'Leaf, 'T>(d: unit -> StreamStep<'Leaf, ColdPhase, 'T>) : StreamMemo<'T> =
        CeRuntime.CreateDynamicStreamLatest (Graph.Current, fun ct -> (d ()).EvalSource(ct))

    static member EvalColdFold<'S, 'Leaf, 'T>(d: unit -> StreamStep<'Leaf, ColdPhase, 'T>, seed: 'S, folder: 'S -> 'T -> 'S) : StreamMemo<'S> =
        CeRuntime.CreateDynamicStreamFold (Graph.Current, seed, folder, fun ct -> (d ()).EvalSource(ct))

    static member UsingStep<'D, 'InLeaf, 'InPhase, 'OutLeaf, 'OutPhase, 'T when 'D :> IDisposable>
        (resource: 'D, body: 'D -> StreamStep<'InLeaf, 'InPhase, 'T>)
        : StreamStep<'OutLeaf, 'OutPhase, 'T> =
        if InternalCeState.IsProbing then
            CeRuntime.DisposeResource resource
            StreamDynamic (Unchecked.defaultof<Graph>, fun _ -> CeRuntime.ProbeDisposed ())
        else
            StreamDynamic (Unchecked.defaultof<Graph>, fun ct -> CeRuntime.RunUsing (resource, fun () -> (body resource).EvalResult(ct)))

    static member BindReturnReactive<'K, 'T>(source: IReactive<'K>, f: 'K -> StreamSource<'T>) : StreamStep<NotLeaf, BoundPhase, 'T> =
        if InternalCeState.IsEvaluating then
            let key = CeRuntime.ReadCeTracked source
            StreamReady (CeRuntime.RunStreamUntrackedArg (source.Graph, f, key))
        else
            let build (k: 'K) (_ct: CancellationToken) : StreamSource<'T> =
                CeRuntime.SuspendNested (fun () -> f k)

            let evalDyn (_ct: CancellationToken) : StreamEval<'T> =
                let key = CeRuntime.ReadCeTracked source
                StreamEvalSource (CeRuntime.RunStreamUntrackedArg (source.Graph, f, key))

            StreamStatic (StreamOnFactory<'K, 'T>(source, build) :> IStreamStaticFactory<'T>, evalDyn)

    static member BindReactiveStatic<'K, 'T>
        (source: IReactive<'K>, continuation: 'K -> StreamStep<IsLeaf, ColdPhase, 'T>)
        : StreamStep<NotLeaf, BoundPhase, 'T> =
        let evalDyn (ct: CancellationToken) : StreamEval<'T> =
            let key = CeRuntime.ReadCeTracked source
            CeRuntime.RunStreamUntrackedContinuation (source.Graph, key, fun k -> (continuation k).EvalResult(ct))

        if InternalCeState.IsEvaluating then
            StreamDynamic (source.Graph, evalDyn)
        else
            let build (k: 'K) (ct: CancellationToken) : StreamSource<'T> =
                CeRuntime.SuspendNested (fun () -> (continuation k).EvalSource(ct))

            StreamStatic (StreamOnFactory<'K, 'T>(source, build) :> IStreamStaticFactory<'T>, evalDyn)

    static member BindReactiveDynamic<'K, 'AnyLeaf, 'ContPhase, 'T>
        (source: IReactive<'K>, continuation: 'K -> StreamStep<'AnyLeaf, 'ContPhase, 'T>)
        : StreamStep<NotLeaf, BoundPhase, 'T> =
        let evalDyn (ct: CancellationToken) : StreamEval<'T> =
            let key = CeRuntime.ReadCeTracked source
            let saved = InternalCeState.SwapReactiveBind true

            try
                CeRuntime.RunStreamUntrackedContinuation (source.Graph, key, fun k -> (continuation k).EvalResult(ct))
            finally
                InternalCeState.SwapReactiveBind saved |> ignore

        StreamDynamic (source.Graph, evalDyn)

    static member ExtractStaticFactory<'Leaf, 'Phase, 'T>(step: StreamStep<'Leaf, 'Phase, 'T>) : IStreamStaticFactory<'T> =
        match step with
        | StreamStatic (factory, _) -> factory
        | _ -> invalidOp "Unexpected non-static StreamStep during context extraction."

    static member BindCancellationToken<'Leaf, 'Phase, 'T>
        (continuation: CancellationToken -> StreamStep<'Leaf, 'Phase, 'T>)
        : StreamStep<'Leaf, 'Phase, 'T> =
        if InternalCeState.IsProbing then
            let probed = continuation CancellationToken.None

            match probed with
            | StreamStatic (factory, _) ->
                StreamStatic (
                    factory.WithContext (fun ct -> StreamRunner.ExtractStaticFactory (continuation ct)),
                    fun ct -> (continuation ct).EvalResult(ct)
                )
            | _ ->
                let probedGraph = probed.TryGraph

                let resolvedGraph =
                    if not (Platform.isNull (box probedGraph)) then
                        probedGraph
                    else
                        CeRuntime.TryGetAmbientGraph ()

                StreamDynamic (resolvedGraph, fun ct -> (continuation ct).EvalResult(ct))
        else
            StreamDynamic (Unchecked.defaultof<Graph>, fun ct -> (continuation ct).EvalResult(ct))

namespace Ranvier.CE

open System
open System.Collections.Generic
open System.ComponentModel
open System.Threading
open System.Threading.Tasks
open Ranvier
open Ranvier.Internal

/// <summary>Computation expression builder producing <c>Memo&lt;'U&gt;</c> nodes with compile-time static combinator selection.</summary>
[<Sealed>]
type MemoBuilder() =
    /// <summary>Context token bound via <c>let! prev = memo.previous</c> for <c>'U voption</c>.</summary>
    member inline _.previous: MemoPreviousToken = MemoPreviousToken ()

    /// <summary>Context token bound via <c>let! prev = memo.previousOr seed</c> with an initial fallback value.</summary>
    member inline _.previousOr(seed: 'U) : MemoPreviousOrToken<'U> =
        MemoPreviousOrToken<'U> seed

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(_: unit) : MemoHeader<PureMode, NoCmp> =
        MemoHeader<PureMode, NoCmp>(NoCmp ())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Zero() : MemoStep<'Phase, 'Prev, unit> =
        MemoInline ()

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Return(value: 'U) : MemoStep<'Phase, 'Prev, 'U> =
        MemoInline value

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.ReturnFrom(memo: Memo<'U>) : MemoTail<'Phase, 'U> =
        TailReady memo

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] f: unit -> 'T) : unit -> 'T = f

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine
        (first: MemoStep<ColdPhase, 'Prev, unit>, [<InlineIfLambda>] second: unit -> MemoStep<ColdPhase, 'Prev, 'U>)
        : MemoStep<'Phase, 'Prev, 'U> =
        MemoDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                first.EvalValue ()
                (second ()).EvalValue()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.While
        ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> MemoStep<ColdPhase, 'Prev, unit>)
        : MemoStep<'Phase, 'Prev, unit> =
        MemoDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                while guard () do
                    (body ()).EvalValue()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryWith
        ([<InlineIfLambda>] body: unit -> MemoStep<ColdPhase, 'Prev, 'U>, [<InlineIfLambda>] handler: exn -> MemoStep<ColdPhase, 'Prev, 'U>)
        : MemoStep<'Phase, 'Prev, 'U> =
        MemoDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalValue()
                with ex ->
                    CeRuntime.RunTryWithHandler (ex, fun () -> (handler ex).EvalValue())
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryFinally
        ([<InlineIfLambda>] body: unit -> MemoStep<ColdPhase, 'Prev, 'U>, [<InlineIfLambda>] compensation: unit -> unit)
        : MemoStep<'Phase, 'Prev, 'U> =
        MemoDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalValue()
                finally
                    compensation ()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Using
        (resource: 'D, [<InlineIfLambda>] body: 'D -> MemoStep<ColdPhase, 'Prev, 'U>)
        : MemoStep<'Phase, 'Prev, 'U> when 'D :> IDisposable =
        MemoRunner.UsingStep (resource, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For
        (header: MemoHeader<'Mode, 'Cmp>, [<InlineIfLambda>] body: unit -> MemoStep<'Phase, 'Prev, 'U>)
        : ConfiguredMemo<'Mode, 'Cmp, 'Phase, 'Prev, 'U> =
        ConfiguredMemo<'Mode, 'Cmp, 'Phase, 'Prev, 'U>(header, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For
        (header: MemoHeader<'Mode, 'Cmp>, [<InlineIfLambda>] body: unit -> MemoTail<'Phase, 'U>)
        : ConfiguredMemoTail<'Mode, 'Cmp, 'Phase, 'U> =
        ConfiguredMemoTail<'Mode, 'Cmp, 'Phase, 'U>(header, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> MemoStep<ColdPhase, 'Prev, unit>) : MemoStep<'Phase, 'Prev, unit> =
        MemoDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                for item in sequence do
                    (body item).EvalValue()
        )

    /// <summary>Gives the memo its own Phase-2 scope (disposing created nodes and cleanups on re-run and disposal).</summary>
    [<CustomOperation("owning")>]
    member inline _.Owning(header: MemoHeader<PureMode, 'Cmp>) : MemoHeader<OwningMode, 'Cmp> =
        MemoHeader<OwningMode, 'Cmp>(header.Cmp)

    /// <summary>Overrides the output cutoff equality comparer on the resulting memo.</summary>
    [<CustomOperation("comparer")>]
    member inline _.Comparer(header: MemoHeader<PureMode, NoCmp>, comparer: IEqualityComparer<'U>) : MemoHeader<PureMode, WithCmp<'U>> =
        ignore header

        if isNull (box comparer) then
            nullArg (nameof comparer)

        MemoHeader<PureMode, WithCmp<'U>>(WithCmp<'U> comparer)

    /// <summary>Overrides the output cutoff equality comparer on the owning memo.</summary>
    [<CustomOperation("comparer")>]
    member inline _.Comparer(header: MemoHeader<OwningMode, NoCmp>, comparer: IEqualityComparer<'U>) : MemoHeader<OwningMode, WithCmp<'U>> =
        ignore header

        if isNull (box comparer) then
            nullArg (nameof comparer)

        MemoHeader<OwningMode, WithCmp<'U>>(WithCmp<'U> comparer)

    /// <summary>Deduplicates <c>Some</c> wrapper identity while inner values stay equal under the graph policy.</summary>
    [<CustomOperation("option")>]
    member inline _.Option(header: MemoHeader<PureMode, NoCmp>) : MemoHeader<OptionMode, NoCmp> =
        ignore header
        MemoHeader<OptionMode, NoCmp>(NoCmp ())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: Signal<'A>, source2: Signal<'B>) : MergedSignals<'A, 'B> =
        MergedSignals<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: Memo<'A>, source2: Memo<'B>) : MergedMemos<'A, 'B> =
        MergedMemos<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: IReactive<'A>, source2: IReactive<'B>) : MergedReactive<'A, 'B> =
        MergedReactive<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: Signal<'K>, f: 'K -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        MemoRunner.BindSignal (source, f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: Memo<'K>, f: 'K -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        MemoRunner.BindMemo (source, f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: MergedSignals<'A, 'B>, f: struct ('A * 'B) -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        MemoRunner.BindMergedSignals (source, f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: MergedMemos<'A, 'B>, f: struct ('A * 'B) -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        MemoRunner.BindMergedMemos (source, f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: MergedReactive<'A, 'B>, f: struct ('A * 'B) -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        MemoRunner.BindMergedReactive (source, f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: IReactive<'K>, f: 'K -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        MemoRunner.BindSource (source, f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(_token: MemoPreviousToken, f: 'U voption -> 'U) : MemoStep<'Phase, WithPrev, 'U> =
        MemoRunner.BindReturnPrevVal (InternalCeState.ReadPrevious<'U>(), f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(token: MemoPreviousOrToken<'U>, f: 'U -> 'U) : MemoStep<'Phase, WithPrev, 'U> =
        MemoRunner.BindReturnPrevVal (InternalCeState.ReadPreviousOr token.Seed, f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: IReactive<'K>, continuation: 'K -> MemoStep<'Phase, 'Prev, 'U>) : MemoStep<BoundPhase, 'Prev, 'U> =
        MemoRunner.BindReactive (source, continuation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(_token: MemoPreviousToken, continuation: 'U voption -> MemoStep<'Phase, 'InnerPrev, 'U>) : MemoStep<'Phase, WithPrev, 'U> =
        MemoRunner.BindPrevVal (ValueNone, InternalCeState.ReadPrevious<'U>, continuation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(token: MemoPreviousOrToken<'U>, continuation: 'U -> MemoStep<'Phase, 'InnerPrev, 'U>) : MemoStep<'Phase, WithPrev, 'U> =
        let seed = token.Seed
        MemoRunner.BindPrevVal (seed, (fun () -> InternalCeState.ReadPreviousOr seed), continuation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: IReactive<'K>, continuation: 'K -> MemoTail<'Phase, 'U>) : MemoTail<BoundPhase, 'U> =
        let g = source.Graph

        TailStatic (g, fun cmp -> CeRuntime.CreateStaticTail (source, (fun k -> (continuation k).EvalMemo()), cmp))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> MemoStep<ColdPhase, 'Prev, 'U>) : Memo<'U> =
        MemoRunner.EvalCold (d, false, ValueNone)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> MemoTail<ColdPhase, 'U>) : Memo<'U> =
        MemoRunner.EvalColdTail (d, ValueNone)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredMemo<PureMode, NoCmp, ColdPhase, 'Prev, 'U>) : Memo<'U> =
        MemoRunner.EvalCold ((fun () -> (d ()).Body()), false, ValueNone)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredMemo<OwningMode, NoCmp, ColdPhase, 'Prev, 'U>) : Memo<'U> =
        MemoRunner.EvalCold ((fun () -> (d ()).Body()), true, ValueNone)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredMemo<PureMode, WithCmp<'U>, ColdPhase, 'Prev, 'U>) : Memo<'U> =
        let cfg = d ()
        MemoRunner.EvalCold (cfg.Body, false, ValueSome cfg.Header.Cmp.Comparer)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredMemo<OwningMode, WithCmp<'U>, ColdPhase, 'Prev, 'U>) : Memo<'U> =
        let cfg = d ()
        MemoRunner.EvalCold (cfg.Body, true, ValueSome cfg.Header.Cmp.Comparer)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredMemo<OptionMode, NoCmp, ColdPhase, NoPrev, 'A option>) : Memo<'A option> =
        MemoRunner.EvalColdOption (fun () -> (d ()).Body())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredMemoTail<OwningMode, NoCmp, ColdPhase, 'U>) : Memo<'U> =
        MemoRunner.EvalColdTail ((fun () -> (d ()).Body()), ValueNone)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredMemoTail<PureMode, WithCmp<'U>, ColdPhase, 'U>) : Memo<'U> =
        let cfg = d ()
        MemoRunner.EvalColdTail (cfg.Body, ValueSome cfg.Header.Cmp.Comparer)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredMemoTail<OwningMode, WithCmp<'U>, ColdPhase, 'U>) : Memo<'U> =
        let cfg = d ()
        MemoRunner.EvalColdTail (cfg.Body, ValueSome cfg.Header.Cmp.Comparer)

/// <summary>Computation expression builder producing <c>Effect</c> / <c>EffectOn</c> nodes with closed action bodies.</summary>
[<Sealed>]
type EffectBuilder() =
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(_: unit) : EffectHeader<UnitOut> =
        EffectHeader<UnitOut>()

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Zero() : EffectStep<'Leaf, 'Phase> =
        EffectUnit

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] f: unit -> 'T) : unit -> 'T = f

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine
        (first: EffectStep<IsLeaf, 'P1>, [<InlineIfLambda>] second: unit -> EffectStep<IsLeaf, 'P2>)
        : EffectStep<'OutLeaf, 'OutPhase> =
        EffectDynamicFn (
            Unchecked.defaultof<Graph>,
            fun () ->
                first.EvalUnit ()
                (second ()).EvalUnit()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.While
        ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> EffectStep<IsLeaf, 'P>)
        : EffectStep<'OutLeaf, 'OutPhase> =
        EffectDynamicFn (
            Unchecked.defaultof<Graph>,
            fun () ->
                while guard () do
                    (body ()).EvalUnit()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryWith
        ([<InlineIfLambda>] body: unit -> EffectStep<IsLeaf, 'P1>, [<InlineIfLambda>] handler: exn -> EffectStep<IsLeaf, 'P2>)
        : EffectStep<'OutLeaf, 'OutPhase> =
        EffectDynamicFn (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalUnit()
                with ex ->
                    CeRuntime.RunTryWithHandler (ex, fun () -> (handler ex).EvalUnit())
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryFinally
        ([<InlineIfLambda>] body: unit -> EffectStep<IsLeaf, 'P>, [<InlineIfLambda>] compensation: unit -> unit)
        : EffectStep<'OutLeaf, 'OutPhase> =
        EffectDynamicFn (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalUnit()
                finally
                    compensation ()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Using
        (resource: 'D, [<InlineIfLambda>] body: 'D -> EffectStep<IsLeaf, 'P>)
        : EffectStep<'OutLeaf, 'OutPhase> when 'D :> IDisposable =
        EffectRunner.UsingStep (resource, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For
        (header: EffectHeader<'Out>, [<InlineIfLambda>] body: unit -> EffectStep<'Leaf, 'Phase>)
        : ConfiguredEffect<'Out, 'Leaf, 'Phase> =
        ignore header
        ConfiguredEffect<'Out, 'Leaf, 'Phase>(body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> EffectStep<IsLeaf, 'P>) : EffectStep<'OutLeaf, 'OutPhase> =
        EffectDynamicFn (
            Unchecked.defaultof<Graph>,
            fun () ->
                for item in sequence do
                    (body item).EvalUnit()
        )

    /// <summary>Returns the created effect's <c>INode</c> handle instead of <c>unit</c>.</summary>
    [<CustomOperation("node")>]
    member inline _.Node(_header: EffectHeader<UnitOut>) : EffectHeader<NodeOut> =
        EffectHeader<NodeOut>()

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: Signal<'A>, source2: Signal<'B>) : MergedSignals<'A, 'B> =
        MergedSignals<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: Memo<'A>, source2: Memo<'B>) : MergedMemos<'A, 'B> =
        MergedMemos<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: IReactive<'A>, source2: IReactive<'B>) : MergedReactive<'A, 'B> =
        MergedReactive<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: IReactive<'K>, continuation: 'K -> EffectStep<IsLeaf, 'ContPhase>) : EffectStep<NotLeaf, BoundPhase> =
        let evalDyn () =
            let key = CeRuntime.ReadEffectTracked source
            CeRuntime.RunEffectContinuation (source, key, fun k -> (continuation k).EvalUnit())

        if InternalCeState.IsEvaluating then
            evalDyn ()
            EffectUnit
        else
            let g = source.Graph

            EffectStatic (g, (fun asNode -> CeRuntime.CreateEffectOn (source, (fun k -> (continuation k).EvalUnit()), asNode)), evalDyn)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> EffectStep<'Leaf, ColdPhase>) : unit =
        EffectRunner.EvalCold (d, false) |> ignore

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredEffect<UnitOut, 'Leaf, ColdPhase>) : unit =
        EffectRunner.EvalCold ((fun () -> (d ()).Body()), false)
        |> ignore

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredEffect<NodeOut, 'Leaf, ColdPhase>) : INode =
        EffectRunner.EvalCold ((fun () -> (d ()).Body()), true)

/// <summary>Computation expression builder producing <c>Editable&lt;'U&gt;</c> handles.</summary>
[<Sealed>]
type EditableBuilder() =
    /// <summary>Context token bound via <c>let! prev = editable.previous</c> for the seed's previous value.</summary>
    member inline _.previous: EditablePreviousToken = EditablePreviousToken ()

    /// <summary>Context token bound via <c>let! prev = editable.previousOr seed</c> with fallback seed.</summary>
    member inline _.previousOr(seed: 'U) : EditablePreviousOrToken<'U> =
        EditablePreviousOrToken<'U> seed

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Zero() : EditableStep<'Phase, 'Prev, unit> =
        EditInline ()

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Return(value: 'U) : EditableStep<'Phase, 'Prev, 'U> =
        EditInline value

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] f: unit -> 'T) : unit -> 'T = f

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine
        (first: EditableStep<ColdPhase, 'Prev, unit>, [<InlineIfLambda>] second: unit -> EditableStep<ColdPhase, 'Prev, 'U>)
        : EditableStep<'Phase, 'Prev, 'U> =
        EditableRunner.CombineSteps (first, second)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.While
        ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> EditableStep<ColdPhase, 'Prev, unit>)
        : EditableStep<'Phase, 'Prev, unit> =
        EditableRunner.WhileStep (guard, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryWith
        ([<InlineIfLambda>] body: unit -> EditableStep<ColdPhase, 'Prev, 'U>, [<InlineIfLambda>] handler: exn -> EditableStep<ColdPhase, 'Prev, 'U>)
        : EditableStep<'Phase, 'Prev, 'U> =
        EditableRunner.TryWithStep (body, handler)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryFinally
        ([<InlineIfLambda>] body: unit -> EditableStep<ColdPhase, 'Prev, 'U>, [<InlineIfLambda>] compensation: unit -> unit)
        : EditableStep<'Phase, 'Prev, 'U> =
        EditableRunner.TryFinallyStep (body, compensation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Using
        (resource: 'D, [<InlineIfLambda>] body: 'D -> EditableStep<ColdPhase, 'Prev, 'U>)
        : EditableStep<'Phase, 'Prev, 'U> when 'D :> IDisposable =
        EditableRunner.UsingStep (resource, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> EditableStep<ColdPhase, 'Prev, unit>) : EditableStep<'Phase, 'Prev, unit> =
        EditableRunner.ForSeqStep (sequence, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: Signal<'A>, source2: Signal<'B>) : MergedSignals<'A, 'B> =
        MergedSignals<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: Memo<'A>, source2: Memo<'B>) : MergedMemos<'A, 'B> =
        MergedMemos<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: IReactive<'A>, source2: IReactive<'B>) : MergedReactive<'A, 'B> =
        MergedReactive<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: IReactive<'K>, f: 'K -> 'U) : EditableStep<BoundPhase, 'Prev, 'U> =
        EditableRunner.BindReturnReactive (source, f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(_token: EditablePreviousToken, f: 'U voption -> 'U) : EditableStep<'Phase, WithPrev, 'U> =
        EditableRunner.BindReturnPrevVal (InternalCeState.ReadPrevious<'U>(), f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(token: EditablePreviousOrToken<'U>, f: 'U -> 'U) : EditableStep<'Phase, WithPrev, 'U> =
        EditableRunner.BindReturnPrevVal (InternalCeState.ReadPreviousOr token.Seed, f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: IReactive<'K>, continuation: 'K -> EditableStep<'Phase, 'Prev, 'U>) : EditableStep<BoundPhase, 'Prev, 'U> =
        EditableRunner.BindReactive (source, continuation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind
        (_token: EditablePreviousToken, continuation: 'U voption -> EditableStep<'Phase, 'InnerPrev, 'U>)
        : EditableStep<'Phase, WithPrev, 'U> =
        EditableRunner.BindPrevVal (ValueNone, InternalCeState.ReadPrevious<'U>, continuation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind
        (token: EditablePreviousOrToken<'U>, continuation: 'U -> EditableStep<'Phase, 'InnerPrev, 'U>)
        : EditableStep<'Phase, WithPrev, 'U> =
        let seed = token.Seed
        EditableRunner.BindPrevVal (seed, (fun () -> InternalCeState.ReadPreviousOr seed), continuation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> EditableStep<ColdPhase, 'Prev, 'U>) : Editable<'U> =
        EditableRunner.EvalCold (d, false)

/// <summary>Computation expression builder producing draft <c>Editable&lt;'U&gt;</c> handles that keep edits until <c>Reset</c>.</summary>
[<Sealed>]
type DraftBuilder() =
    /// <summary>Context token bound via <c>let! prev = draft.previous</c> for the seed's previous value.</summary>
    member inline _.previous: DraftPreviousToken = DraftPreviousToken ()

    /// <summary>Context token bound via <c>let! prev = draft.previousOr seed</c> with fallback seed.</summary>
    member inline _.previousOr(seed: 'U) : DraftPreviousOrToken<'U> =
        DraftPreviousOrToken<'U> seed

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Zero() : EditableStep<'Phase, 'Prev, unit> =
        EditInline ()

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Return(value: 'U) : EditableStep<'Phase, 'Prev, 'U> =
        EditInline value

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] f: unit -> 'T) : unit -> 'T = f

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine
        (first: EditableStep<ColdPhase, 'Prev, unit>, [<InlineIfLambda>] second: unit -> EditableStep<ColdPhase, 'Prev, 'U>)
        : EditableStep<'Phase, 'Prev, 'U> =
        EditableRunner.CombineSteps (first, second)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.While
        ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> EditableStep<ColdPhase, 'Prev, unit>)
        : EditableStep<'Phase, 'Prev, unit> =
        EditableRunner.WhileStep (guard, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryWith
        ([<InlineIfLambda>] body: unit -> EditableStep<ColdPhase, 'Prev, 'U>, [<InlineIfLambda>] handler: exn -> EditableStep<ColdPhase, 'Prev, 'U>)
        : EditableStep<'Phase, 'Prev, 'U> =
        EditableRunner.TryWithStep (body, handler)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryFinally
        ([<InlineIfLambda>] body: unit -> EditableStep<ColdPhase, 'Prev, 'U>, [<InlineIfLambda>] compensation: unit -> unit)
        : EditableStep<'Phase, 'Prev, 'U> =
        EditableRunner.TryFinallyStep (body, compensation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Using
        (resource: 'D, [<InlineIfLambda>] body: 'D -> EditableStep<ColdPhase, 'Prev, 'U>)
        : EditableStep<'Phase, 'Prev, 'U> when 'D :> IDisposable =
        EditableRunner.UsingStep (resource, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> EditableStep<ColdPhase, 'Prev, unit>) : EditableStep<'Phase, 'Prev, unit> =
        EditableRunner.ForSeqStep (sequence, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: Signal<'A>, source2: Signal<'B>) : MergedSignals<'A, 'B> =
        MergedSignals<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: Memo<'A>, source2: Memo<'B>) : MergedMemos<'A, 'B> =
        MergedMemos<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: IReactive<'A>, source2: IReactive<'B>) : MergedReactive<'A, 'B> =
        MergedReactive<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: IReactive<'K>, f: 'K -> 'U) : EditableStep<BoundPhase, 'Prev, 'U> =
        EditableRunner.BindReturnReactive (source, f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(_token: DraftPreviousToken, f: 'U voption -> 'U) : EditableStep<'Phase, WithPrev, 'U> =
        EditableRunner.BindReturnPrevVal (InternalCeState.ReadPrevious<'U>(), f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(token: DraftPreviousOrToken<'U>, f: 'U -> 'U) : EditableStep<'Phase, WithPrev, 'U> =
        EditableRunner.BindReturnPrevVal (InternalCeState.ReadPreviousOr token.Seed, f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: IReactive<'K>, continuation: 'K -> EditableStep<'Phase, 'Prev, 'U>) : EditableStep<BoundPhase, 'Prev, 'U> =
        EditableRunner.BindReactive (source, continuation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind
        (_token: DraftPreviousToken, continuation: 'U voption -> EditableStep<'Phase, 'InnerPrev, 'U>)
        : EditableStep<'Phase, WithPrev, 'U> =
        EditableRunner.BindPrevVal (ValueNone, InternalCeState.ReadPrevious<'U>, continuation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind
        (token: DraftPreviousOrToken<'U>, continuation: 'U -> EditableStep<'Phase, 'InnerPrev, 'U>)
        : EditableStep<'Phase, WithPrev, 'U> =
        let seed = token.Seed
        EditableRunner.BindPrevVal (seed, (fun () -> InternalCeState.ReadPreviousOr seed), continuation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> EditableStep<ColdPhase, 'Prev, 'U>) : Editable<'U> =
        EditableRunner.EvalCold (d, true)

/// <summary>Computation expression builder producing <c>Boundary&lt;'U&gt;</c> suspense/error boundaries.</summary>
[<Sealed>]
type BoundaryBuilder() =
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(_: unit) : BoundaryHeader<NoPending, NoFailed, NoCmp> =
        BoundaryHeader<NoPending, NoFailed, NoCmp>(NoPending (), NoFailed (), NoCmp ())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Zero() : BoundaryStep<'Phase, unit> =
        BoundaryInline ()

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Return(value: 'U) : BoundaryStep<'Phase, 'U> =
        BoundaryInline value

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] f: unit -> 'T) : unit -> 'T = f

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine
        (first: BoundaryStep<ColdPhase, unit>, [<InlineIfLambda>] second: unit -> BoundaryStep<ColdPhase, 'U>)
        : BoundaryStep<'Phase, 'U> =
        BoundaryDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                first.EvalValue ()
                (second ()).EvalValue()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.While
        ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> BoundaryStep<ColdPhase, unit>)
        : BoundaryStep<'Phase, unit> =
        BoundaryDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                while guard () do
                    (body ()).EvalValue()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryWith
        ([<InlineIfLambda>] body: unit -> BoundaryStep<ColdPhase, 'U>, [<InlineIfLambda>] handler: exn -> BoundaryStep<ColdPhase, 'U>)
        : BoundaryStep<'Phase, 'U> =
        BoundaryDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalValue()
                with ex ->
                    CeRuntime.RunTryWithHandler (ex, fun () -> (handler ex).EvalValue())
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryFinally
        ([<InlineIfLambda>] body: unit -> BoundaryStep<ColdPhase, 'U>, [<InlineIfLambda>] compensation: unit -> unit)
        : BoundaryStep<'Phase, 'U> =
        BoundaryDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalValue()
                finally
                    compensation ()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Using
        (resource: 'D, [<InlineIfLambda>] body: 'D -> BoundaryStep<ColdPhase, 'U>)
        : BoundaryStep<'Phase, 'U> when 'D :> IDisposable =
        BoundaryRunner.UsingStep (resource, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For
        (header: BoundaryHeader<'Pend, 'Fail, 'Cmp>, [<InlineIfLambda>] body: unit -> BoundaryStep<'Phase, 'U>)
        : ConfiguredBoundary<'Pend, 'Fail, 'Cmp, 'Phase, 'U> =
        ConfiguredBoundary<'Pend, 'Fail, 'Cmp, 'Phase, 'U>(header, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> BoundaryStep<ColdPhase, unit>) : BoundaryStep<'Phase, unit> =
        BoundaryDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                for item in sequence do
                    (body item).EvalValue()
        )

    /// <summary>Catches <c>NotReadyException</c> inside the boundary body using <c>fallback</c>.</summary>
    [<CustomOperation("pending")>]
    member inline _.Pending
        (header: BoundaryHeader<NoPending, 'Fail, 'Cmp>, fallback: 'U voption -> 'U)
        : BoundaryHeader<WithPending<'U>, 'Fail, 'Cmp> =
        if isNull (box fallback) then
            nullArg (nameof fallback)

        BoundaryHeader<WithPending<'U>, 'Fail, 'Cmp>(WithPending<'U> fallback, header.Fail, header.Cmp)

    /// <summary>Catches exceptions raised by the boundary body using <c>recover</c>.</summary>
    [<CustomOperation("failed")>]
    member inline _.Failed
        (header: BoundaryHeader<'Pend, NoFailed, 'Cmp>, recover: exn -> 'U voption -> 'U)
        : BoundaryHeader<'Pend, WithFailed<'U>, 'Cmp> =
        if isNull (box recover) then
            nullArg (nameof recover)

        BoundaryHeader<'Pend, WithFailed<'U>, 'Cmp>(header.Pend, WithFailed<'U> recover, header.Cmp)

    /// <summary>Overrides the output cutoff equality comparer on the resulting boundary.</summary>
    [<CustomOperation("comparer")>]
    member inline _.Comparer
        (header: BoundaryHeader<'Pend, 'Fail, NoCmp>, comparer: IEqualityComparer<'U>)
        : BoundaryHeader<'Pend, 'Fail, WithCmp<'U>> =
        if isNull (box comparer) then
            nullArg (nameof comparer)

        BoundaryHeader<'Pend, 'Fail, WithCmp<'U>>(header.Pend, header.Fail, WithCmp<'U> comparer)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: Signal<'A>, source2: Signal<'B>) : MergedSignals<'A, 'B> =
        MergedSignals<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: Memo<'A>, source2: Memo<'B>) : MergedMemos<'A, 'B> =
        MergedMemos<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: IReactive<'A>, source2: IReactive<'B>) : MergedReactive<'A, 'B> =
        MergedReactive<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: IReactive<'K>, f: 'K -> 'U) : BoundaryStep<BoundPhase, 'U> =
        BoundaryRunner.BindReturnReactive (source, f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: IReactive<'K>, continuation: 'K -> BoundaryStep<'Phase, 'U>) : BoundaryStep<BoundPhase, 'U> =
        BoundaryRunner.BindReactive (source, continuation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredBoundary<WithPending<'U>, NoFailed, NoCmp, ColdPhase, 'U>) : Boundary<'U> =
        let cfg = d ()
        BoundaryRunner.EvalCold (cfg.Body, ValueSome cfg.Header.Pend.OnPending, ValueNone, ValueNone)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredBoundary<NoPending, WithFailed<'U>, NoCmp, ColdPhase, 'U>) : Boundary<'U> =
        let cfg = d ()
        BoundaryRunner.EvalCold (cfg.Body, ValueNone, ValueSome cfg.Header.Fail.OnFailed, ValueNone)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredBoundary<WithPending<'U>, WithFailed<'U>, NoCmp, ColdPhase, 'U>) : Boundary<'U> =
        let cfg = d ()
        BoundaryRunner.EvalCold (cfg.Body, ValueSome cfg.Header.Pend.OnPending, ValueSome cfg.Header.Fail.OnFailed, ValueNone)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredBoundary<WithPending<'U>, NoFailed, WithCmp<'U>, ColdPhase, 'U>) : Boundary<'U> =
        let cfg = d ()
        BoundaryRunner.EvalCold (cfg.Body, ValueSome cfg.Header.Pend.OnPending, ValueNone, ValueSome cfg.Header.Cmp.Comparer)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredBoundary<NoPending, WithFailed<'U>, WithCmp<'U>, ColdPhase, 'U>) : Boundary<'U> =
        let cfg = d ()
        BoundaryRunner.EvalCold (cfg.Body, ValueNone, ValueSome cfg.Header.Fail.OnFailed, ValueSome cfg.Header.Cmp.Comparer)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredBoundary<WithPending<'U>, WithFailed<'U>, WithCmp<'U>, ColdPhase, 'U>) : Boundary<'U> =
        let cfg = d ()
        BoundaryRunner.EvalCold (cfg.Body, ValueSome cfg.Header.Pend.OnPending, ValueSome cfg.Header.Fail.OnFailed, ValueSome cfg.Header.Cmp.Comparer)

/// <summary>Computation expression builder producing per-key projection reader functions <c>unit -&gt; 'V</c> without extra node allocations.</summary>
[<Sealed>]
type RowBuilder() =
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Zero() : RowStep<'Phase, unit> =
        RowReadyVal ()

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Return(value: 'V) : RowStep<'Phase, 'V> =
        RowReadyVal value

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] f: unit -> 'T) : unit -> 'T = f

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(first: RowStep<ColdPhase, unit>, [<InlineIfLambda>] second: unit -> RowStep<ColdPhase, 'V>) : RowStep<'Phase, 'V> =
        RowThunk (fun () ->
            first.EvalValue ()
            (second ()).EvalValue())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.While([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> RowStep<ColdPhase, unit>) : RowStep<'Phase, unit> =
        RowThunk (fun () ->
            while guard () do
                (body ()).EvalValue())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryWith
        ([<InlineIfLambda>] body: unit -> RowStep<ColdPhase, 'V>, [<InlineIfLambda>] handler: exn -> RowStep<ColdPhase, 'V>)
        : RowStep<'Phase, 'V> =
        RowThunk (fun () ->
            try
                (body ()).EvalValue()
            with ex ->
                CeRuntime.RunTryWithHandler (ex, fun () -> (handler ex).EvalValue()))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryFinally
        ([<InlineIfLambda>] body: unit -> RowStep<ColdPhase, 'V>, [<InlineIfLambda>] compensation: unit -> unit)
        : RowStep<'Phase, 'V> =
        RowThunk (fun () ->
            try
                (body ()).EvalValue()
            finally
                compensation ())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Using(resource: 'D, [<InlineIfLambda>] body: 'D -> RowStep<ColdPhase, 'V>) : RowStep<'Phase, 'V> when 'D :> IDisposable =
        RowThunk (fun () -> CeRuntime.RunUsing (resource, fun () -> (body resource).EvalValue()))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> RowStep<ColdPhase, unit>) : RowStep<'Phase, unit> =
        RowThunk (fun () ->
            for item in sequence do
                (body item).EvalValue())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: unit -> 'A, source2: unit -> 'B) : RowInput<struct ('A * 'B)> =
        RowInput<struct ('A * 'B)>(fun () ->
            let a = source1 ()
            let b = source2 ()
            struct (a, b))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: unit -> 'A, source2: IReactive<'B>) : RowInput<struct ('A * 'B)> =
        RowInput<struct ('A * 'B)>(fun () ->
            let a = source1 ()
            let b = source2.ReadTracked ()
            struct (a, b))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: IReactive<'A>, source2: unit -> 'B) : RowInput<struct ('A * 'B)> =
        RowInput<struct ('A * 'B)>(fun () ->
            let a = source1.ReadTracked ()
            let b = source2 ()
            struct (a, b))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: IReactive<'A>, source2: IReactive<'B>) : RowInput<struct ('A * 'B)> =
        RowInput<struct ('A * 'B)>(fun () ->
            let a = source1.ReadTracked ()
            let b = source2.ReadTracked ()
            struct (a, b))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: unit -> 'A, source2: RowInput<'B>) : RowInput<struct ('A * 'B)> =
        RowInput<struct ('A * 'B)>(fun () ->
            let a = source1 ()
            let b = source2.ReadFn ()
            struct (a, b))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: IReactive<'A>, source2: RowInput<'B>) : RowInput<struct ('A * 'B)> =
        RowInput<struct ('A * 'B)>(fun () ->
            let a = source1.ReadTracked ()
            let b = source2.ReadFn ()
            struct (a, b))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: RowInput<'A>, source2: unit -> 'B) : RowInput<struct ('A * 'B)> =
        RowInput<struct ('A * 'B)>(fun () ->
            let a = source1.ReadFn ()
            let b = source2 ()
            struct (a, b))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: RowInput<'A>, source2: IReactive<'B>) : RowInput<struct ('A * 'B)> =
        RowInput<struct ('A * 'B)>(fun () ->
            let a = source1.ReadFn ()
            let b = source2.ReadTracked ()
            struct (a, b))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: RowInput<'A>, source2: RowInput<'B>) : RowInput<struct ('A * 'B)> =
        RowInput<struct ('A * 'B)>(fun () ->
            let a = source1.ReadFn ()
            let b = source2.ReadFn ()
            struct (a, b))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: unit -> 'A, f: 'A -> 'V) : RowStep<BoundPhase, 'V> =
        let v = CeRuntime.ReadRowFnTracked source
        RowReadyVal (CeRuntime.RunUntrackedRowArg (Unchecked.defaultof<Graph>, f, v))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: IReactive<'A>, f: 'A -> 'V) : RowStep<BoundPhase, 'V> =
        let v = CeRuntime.ReadCeTracked source
        RowReadyVal (CeRuntime.RunUntrackedRowArg (source.Graph, f, v))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: RowInput<'A>, f: 'A -> 'V) : RowStep<BoundPhase, 'V> =
        let v = CeRuntime.ReadRowFnTracked source.ReadFn
        RowReadyVal (CeRuntime.RunUntrackedRowArg (Unchecked.defaultof<Graph>, f, v))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: unit -> 'A, continuation: 'A -> RowStep<'Phase, 'V>) : RowStep<BoundPhase, 'V> =
        let v = CeRuntime.ReadRowFnTracked source
        RowReadyVal (CeRuntime.RunRowContinuation (Unchecked.defaultof<Graph>, v, fun k -> (continuation k).EvalValue()))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: IReactive<'A>, continuation: 'A -> RowStep<'Phase, 'V>) : RowStep<BoundPhase, 'V> =
        let v = CeRuntime.ReadCeTracked source
        RowReadyVal (CeRuntime.RunRowContinuation (source.Graph, v, fun k -> (continuation k).EvalValue()))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: RowInput<'A>, continuation: 'A -> RowStep<'Phase, 'V>) : RowStep<BoundPhase, 'V> =
        let v = CeRuntime.ReadRowFnTracked source.ReadFn
        RowReadyVal (CeRuntime.RunRowContinuation (Unchecked.defaultof<Graph>, v, fun k -> (continuation k).EvalValue()))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> RowStep<ColdPhase, 'V>) : unit -> 'V =
        fun () -> CeRuntime.EvalRow (fun () -> (d ()).EvalValue())

/// <summary>Two-phase computation expression builder producing <c>AsyncMemo&lt;'U&gt;</c> nodes with compile-time static/dynamic dispatch.</summary>
[<Sealed>]
type AsyncMemoBuilder() =
    /// <summary>Context token bound via <c>let! prev = asyncMemo.previous</c> for <c>Previous&lt;'U&gt;</c>.</summary>
    member inline _.previous: AsyncPreviousToken = AsyncPreviousToken ()

    /// <summary>Context token bound via <c>let! ct = asyncMemo.cancellationToken</c> for the flight <c>CancellationToken</c>.</summary>
    member inline _.cancellationToken: AsyncCancellationToken = AsyncCancellationToken ()

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(_: unit) : AsyncHeader<PureMode, DefaultPolicy> =
        AsyncHeader<PureMode, DefaultPolicy>(DefaultPolicy ())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Zero() : AsyncStep<'Leaf, 'Phase, unit> =
        AsyncInline ()

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Return(value: 'U) : AsyncStep<'Leaf, 'Phase, 'U> =
        AsyncInline value

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.ReturnFrom(value: Task<'U>) : AsyncStep<'Leaf, 'Phase, 'U> =
        AsyncRunner.ReturnTask value

#if !FABLE_COMPILER
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.ReturnFrom(value: ValueTask<'U>) : AsyncStep<'Leaf, 'Phase, 'U> =
        AsyncRunner.ReturnValueTask value
#endif

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] f: unit -> 'T) : unit -> 'T = f

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine
        (first: AsyncStep<IsLeaf, 'P1, unit>, [<InlineIfLambda>] second: unit -> AsyncStep<IsLeaf, 'P2, 'U>)
        : AsyncStep<'OutLeaf, 'OutPhase, 'U> =
        AsyncDynamic (Unchecked.defaultof<Graph>, fun prev ct -> AsyncRunner.EvalCombine (first, second, prev, ct))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.While
        ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> AsyncStep<IsLeaf, 'P, unit>)
        : AsyncStep<'OutLeaf, 'OutPhase, unit> =
        AsyncDynamic (Unchecked.defaultof<Graph>, fun _ ct -> AsyncRunner.EvalWhile (guard, body, ct))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryWith
        ([<InlineIfLambda>] body: unit -> AsyncStep<IsLeaf, 'P1, 'U>, [<InlineIfLambda>] handler: exn -> AsyncStep<IsLeaf, 'P2, 'U>)
        : AsyncStep<'OutLeaf, 'OutPhase, 'U> =
        AsyncDynamic (Unchecked.defaultof<Graph>, fun prev ct -> AsyncRunner.EvalTryWith (body, handler, prev, ct))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryFinally
        ([<InlineIfLambda>] body: unit -> AsyncStep<IsLeaf, 'P, 'U>, [<InlineIfLambda>] compensation: unit -> unit)
        : AsyncStep<'OutLeaf, 'OutPhase, 'U> =
        AsyncDynamic (Unchecked.defaultof<Graph>, fun prev ct -> AsyncRunner.EvalTryFinally (body, compensation, prev, ct))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Using
        (resource: 'D, [<InlineIfLambda>] body: 'D -> AsyncStep<IsLeaf, 'P, 'U>)
        : AsyncStep<'OutLeaf, 'OutPhase, 'U> when 'D :> IDisposable =
        AsyncRunner.UsingStep (resource, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For
        (header: AsyncHeader<'Mode, 'Pol>, [<InlineIfLambda>] body: unit -> AsyncStep<'Leaf, 'Phase, 'U>)
        : ConfiguredAsync<'Mode, 'Pol, 'Leaf, 'Phase, 'U> =
        ConfiguredAsync<'Mode, 'Pol, 'Leaf, 'Phase, 'U>(header, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> AsyncStep<IsLeaf, 'P, unit>) : AsyncStep<'OutLeaf, 'OutPhase, unit> =
        AsyncDynamic (Unchecked.defaultof<Graph>, fun _ ct -> AsyncRunner.EvalForSeq (sequence, body, ct))

    /// <summary>Allows the Phase-2 computation up to its first await to own child reactive nodes and cleanups.</summary>
    [<CustomOperation("owning")>]
    member inline _.Owning(header: AsyncHeader<PureMode, 'Pol>) : AsyncHeader<OwningMode, 'Pol> =
        AsyncHeader<OwningMode, 'Pol>(header.Pol)

    /// <summary>Configures the concurrency policy applied when Phase 1 changes while a flight is active.</summary>
    [<CustomOperation("policy")>]
    member inline _.Policy(header: AsyncHeader<'Mode, DefaultPolicy>, policy: FlightPolicy) : AsyncHeader<'Mode, ExplicitPolicy> =
        AsyncHeader<'Mode, ExplicitPolicy>(ExplicitPolicy policy)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: Signal<'A>, source2: Signal<'B>) : MergedSignals<'A, 'B> =
        MergedSignals<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: Memo<'A>, source2: Memo<'B>) : MergedMemos<'A, 'B> =
        MergedMemos<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: IReactive<'A>, source2: IReactive<'B>) : MergedReactive<'A, 'B> =
        MergedReactive<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: IReactive<'K>, f: 'K -> 'U) : AsyncStep<NotLeaf, BoundPhase, 'U> =
        AsyncRunner.BindReturnReactive (source, f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(_token: AsyncPreviousToken, f: Previous<'U> -> 'U) : AsyncStep<'Leaf, 'Phase, 'U> =
        AsyncDynamic (Unchecked.defaultof<Graph>, fun prev _ -> AsyncEvalSync (f prev))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(_token: AsyncCancellationToken, f: CancellationToken -> 'U) : AsyncStep<'Leaf, 'Phase, 'U> =
        AsyncDynamic (Unchecked.defaultof<Graph>, fun _ ct -> AsyncEvalSync (f ct))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: Task<'A>, f: 'A -> 'U) : AsyncStep<'OutLeaf, 'OutPhase, 'U> =
        AsyncDynamic (Unchecked.defaultof<Graph>, fun _ _ -> AsyncRunner.EvalBindTaskReturn (source, f))

#if !FABLE_COMPILER
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: ValueTask<'A>, f: 'A -> 'U) : AsyncStep<'OutLeaf, 'OutPhase, 'U> =
        AsyncDynamic (Unchecked.defaultof<Graph>, fun _ _ -> AsyncRunner.EvalBindValueTaskReturn (source, f))
#endif

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: IReactive<'K>, continuation: 'K -> AsyncStep<IsLeaf, ColdPhase, 'U>) : AsyncStep<NotLeaf, BoundPhase, 'U> =
        AsyncRunner.BindReactiveStatic (source, continuation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(_token: AsyncPreviousToken, continuation: Previous<'U> -> AsyncStep<'Leaf, 'Phase, 'U>) : AsyncStep<'Leaf, 'Phase, 'U> =
        AsyncRunner.BindPrevious continuation

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind
        (_token: AsyncCancellationToken, continuation: CancellationToken -> AsyncStep<'Leaf, 'Phase, 'U>)
        : AsyncStep<'Leaf, 'Phase, 'U> =
        AsyncRunner.BindCancellationToken continuation

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: Task<'A>, continuation: 'A -> AsyncStep<IsLeaf, ColdPhase, 'U>) : AsyncStep<'OutLeaf, 'OutPhase, 'U> =
        AsyncDynamic (Unchecked.defaultof<Graph>, fun prev ct -> AsyncRunner.EvalBindTask (source, continuation, prev, ct))

#if !FABLE_COMPILER
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: ValueTask<'A>, continuation: 'A -> AsyncStep<IsLeaf, ColdPhase, 'U>) : AsyncStep<'OutLeaf, 'OutPhase, 'U> =
        AsyncDynamic (Unchecked.defaultof<Graph>, fun prev ct -> AsyncRunner.EvalBindValueTask (source, continuation, prev, ct))
#endif

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> AsyncStep<'Leaf, ColdPhase, 'U>) : AsyncMemo<'U> =
        AsyncRunner.EvalCold (d, false, ValueNone)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredAsync<PureMode, DefaultPolicy, 'Leaf, ColdPhase, 'U>) : AsyncMemo<'U> =
        AsyncRunner.EvalCold ((fun () -> (d ()).Body()), false, ValueNone)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredAsync<OwningMode, DefaultPolicy, 'Leaf, ColdPhase, 'U>) : AsyncMemo<'U> =
        AsyncRunner.EvalCold ((fun () -> (d ()).Body()), true, ValueNone)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredAsync<PureMode, ExplicitPolicy, 'Leaf, ColdPhase, 'U>) : AsyncMemo<'U> =
        let cfg = d ()
        AsyncRunner.EvalCold (cfg.Body, false, ValueSome cfg.Header.Pol.Policy)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredAsync<OwningMode, ExplicitPolicy, 'Leaf, ColdPhase, 'U>) : AsyncMemo<'U> =
        let cfg = d ()
        AsyncRunner.EvalCold (cfg.Body, true, ValueSome cfg.Header.Pol.Policy)

/// <summary>Two-phase computation expression builder producing <c>StreamMemo&lt;'U&gt;</c> nodes.</summary>
[<Sealed>]
type StreamBuilder() =
    /// <summary>Context token bound via <c>let! ct = stream.cancellationToken</c> for the stream run <c>CancellationToken</c>.</summary>
    member inline _.cancellationToken: StreamCancellationToken = StreamCancellationToken ()

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Yield(_: unit) : StreamHeader<LatestMode> =
        StreamHeader<LatestMode>(LatestMode ())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Zero() : StreamStep<'Leaf, 'Phase, unit> =
        StreamUnit

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Return(source: StreamSource<'T>) : StreamStep<'Leaf, 'Phase, 'T> =
        StreamReady source

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.ReturnFrom(source: StreamSource<'T>) : StreamStep<'Leaf, 'Phase, 'T> =
        StreamReady source

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] f: unit -> 'T) : unit -> 'T = f

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine
        (first: StreamStep<IsLeaf, 'P1, unit>, [<InlineIfLambda>] second: unit -> StreamStep<IsLeaf, 'P2, 'T>)
        : StreamStep<'OutLeaf, 'OutPhase, 'T> =
        StreamDynamic (
            Unchecked.defaultof<Graph>,
            fun ct ->
                first.EvalUnit ct
                (second ()).EvalResult(ct)
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.While
        ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> StreamStep<IsLeaf, 'P, unit>)
        : StreamStep<'OutLeaf, 'OutPhase, unit> =
        StreamDynamic (
            Unchecked.defaultof<Graph>,
            fun ct ->
                while guard () do
                    (body ()).EvalUnit(ct)

                StreamEvalUnit
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryWith
        ([<InlineIfLambda>] body: unit -> StreamStep<IsLeaf, 'P1, 'T>, [<InlineIfLambda>] handler: exn -> StreamStep<IsLeaf, 'P2, 'T>)
        : StreamStep<'OutLeaf, 'OutPhase, 'T> =
        StreamDynamic (
            Unchecked.defaultof<Graph>,
            fun ct ->
                try
                    (body ()).EvalResult(ct)
                with ex ->
                    CeRuntime.RunTryWithHandler (ex, fun () -> (handler ex).EvalResult(ct))
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryFinally
        ([<InlineIfLambda>] body: unit -> StreamStep<IsLeaf, 'P, 'T>, [<InlineIfLambda>] compensation: unit -> unit)
        : StreamStep<'OutLeaf, 'OutPhase, 'T> =
        StreamDynamic (
            Unchecked.defaultof<Graph>,
            fun ct ->
                try
                    (body ()).EvalResult(ct)
                finally
                    compensation ()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Using
        (resource: 'D, [<InlineIfLambda>] body: 'D -> StreamStep<IsLeaf, 'P, 'T>)
        : StreamStep<'OutLeaf, 'OutPhase, 'T> when 'D :> IDisposable =
        StreamRunner.UsingStep (resource, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For
        (header: StreamHeader<'Mode>, [<InlineIfLambda>] body: unit -> StreamStep<'Leaf, 'Phase, 'T>)
        : ConfiguredStream<'Mode, 'Leaf, 'Phase, 'T> =
        ConfiguredStream<'Mode, 'Leaf, 'Phase, 'T>(header, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For
        (sequence: seq<'TItem>, [<InlineIfLambda>] body: 'TItem -> StreamStep<IsLeaf, 'P, unit>)
        : StreamStep<'OutLeaf, 'OutPhase, unit> =
        StreamDynamic (
            Unchecked.defaultof<Graph>,
            fun ct ->
                for item in sequence do
                    (body item).EvalUnit(ct)

                StreamEvalUnit
        )

    /// <summary>Accumulates incoming stream items onto <c>seed</c> using <c>folder</c>, starting from <c>seed</c> on each stream restart.</summary>
    [<CustomOperation("fold")>]
    member inline _.Fold(_header: StreamHeader<LatestMode>, seed: 'S, folder: 'S -> 'T -> 'S) : StreamHeader<FoldMode<'S, 'T>> =
        CeRuntime.RequireStreamFolder folder
        StreamHeader<FoldMode<'S, 'T>>(FoldMode<'S, 'T>(seed, folder))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: Signal<'A>, source2: Signal<'B>) : MergedSignals<'A, 'B> =
        MergedSignals<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: Memo<'A>, source2: Memo<'B>) : MergedMemos<'A, 'B> =
        MergedMemos<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.MergeSources(source1: IReactive<'A>, source2: IReactive<'B>) : MergedReactive<'A, 'B> =
        MergedReactive<'A, 'B>(source1, source2)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: IReactive<'K>, f: 'K -> StreamSource<'T>) : StreamStep<NotLeaf, BoundPhase, 'T> =
        StreamRunner.BindReturnReactive (source, f)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(_token: StreamCancellationToken, f: CancellationToken -> StreamSource<'T>) : StreamStep<'Leaf, 'Phase, 'T> =
        StreamDynamic (Unchecked.defaultof<Graph>, fun ct -> StreamEvalSource (f ct))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: IReactive<'K>, continuation: 'K -> StreamStep<IsLeaf, ColdPhase, 'T>) : StreamStep<NotLeaf, BoundPhase, 'T> =
        StreamRunner.BindReactiveStatic (source, continuation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind
        (_token: StreamCancellationToken, continuation: CancellationToken -> StreamStep<'Leaf, 'Phase, 'T>)
        : StreamStep<'Leaf, 'Phase, 'T> =
        StreamRunner.BindCancellationToken continuation

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> StreamStep<'Leaf, ColdPhase, 'T>) : StreamMemo<'T> =
        StreamRunner.EvalColdLatest d

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredStream<LatestMode, 'Leaf, ColdPhase, 'T>) : StreamMemo<'T> =
        StreamRunner.EvalColdLatest (fun () -> (d ()).Body())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredStream<FoldMode<'S, 'T>, 'Leaf, ColdPhase, 'T>) : StreamMemo<'S> =
        let cfg = d ()
        StreamRunner.EvalColdFold (cfg.Body, cfg.Header.Mode.Seed, cfg.Header.Mode.Folder)

/// <summary>Computation expression builders and combinators explicitly gated behind <c>Ranvier.CE</c>.</summary>
[<AutoOpen>]
module Builders =
    /// <summary>Computation expression builder producing <c>Memo&lt;'U&gt;</c> nodes with closed Phase-2 bodies.</summary>
    let memo = MemoBuilder ()

    /// <summary>Computation expression builder producing <c>Effect</c> / <c>EffectOn</c> nodes with closed action bodies.</summary>
    let effect = EffectBuilder ()

    /// <summary>Computation expression builder producing <c>AsyncMemo&lt;'U&gt;</c> nodes with compile-time Phase-1/Phase-2 dispatch.</summary>
    let asyncMemo = AsyncMemoBuilder ()

    /// <summary>Computation expression builder producing <c>StreamMemo&lt;'U&gt;</c> nodes with latest-item or <c>fold</c> mode.</summary>
    let stream = StreamBuilder ()

    /// <summary>Computation expression builder producing <c>Editable&lt;'U&gt;</c> handles.</summary>
    let editable = EditableBuilder ()

    /// <summary>Computation expression builder producing draft <c>Editable&lt;'U&gt;</c> handles.</summary>
    let draft = DraftBuilder ()

    /// <summary>Computation expression builder producing <c>Boundary&lt;'U&gt;</c> suspense/error boundaries.</summary>
    let boundary = BoundaryBuilder ()

    /// <summary>Computation expression builder producing per-key projection readers <c>unit -&gt; 'V</c>.</summary>
    let row = RowBuilder ()

    /// <summary>
    /// Wraps <c>source</c> so Phase-1 key comparison in <c>effect</c>, <c>memo</c>, <c>asyncMemo</c>, or <c>stream</c>
    /// compares keys under <c>comparer</c> instead of the graph's equality policy.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>comparer</c> or <c>source</c> is null.</exception>
    let inline withComparer (comparer: IEqualityComparer<'K>) (source: IReactive<'K>) : ComparedReactive<'K> =
        if isNull (box comparer) then
            nullArg (nameof comparer)

        ComparedReactive<'K>(source, comparer)

/// <summary>Lower-priority <c>BoundPhase</c> computation-expression overloads resolving unambiguously against intrinsic <c>ColdPhase</c> methods.</summary>
[<AutoOpen>]
module BuilderExtensions =
    type MemoBuilder with
        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Combine
            (first: MemoStep<'P1, 'Prev, unit>, [<InlineIfLambda>] second: unit -> MemoStep<'P2, 'Prev, 'U>)
            : MemoStep<'OutPhase, 'Prev, 'U> =
            MemoDynamic (
                Unchecked.defaultof<Graph>,
                fun () ->
                    first.EvalValue ()
                    (second ()).EvalValue()
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.While
            ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> MemoStep<'P, 'Prev, unit>)
            : MemoStep<'OutPhase, 'Prev, unit> =
            MemoDynamic (
                Unchecked.defaultof<Graph>,
                fun () ->
                    while guard () do
                        (body ()).EvalValue()
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryWith
            ([<InlineIfLambda>] body: unit -> MemoStep<'P1, 'Prev, 'U>, [<InlineIfLambda>] handler: exn -> MemoStep<'P2, 'Prev, 'U>)
            : MemoStep<'OutPhase, 'Prev, 'U> =
            MemoDynamic (
                Unchecked.defaultof<Graph>,
                fun () ->
                    try
                        (body ()).EvalValue()
                    with ex ->
                        CeRuntime.RunTryWithHandler (ex, fun () -> (handler ex).EvalValue())
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryFinally
            ([<InlineIfLambda>] body: unit -> MemoStep<'P, 'Prev, 'U>, [<InlineIfLambda>] compensation: unit -> unit)
            : MemoStep<'OutPhase, 'Prev, 'U> =
            MemoDynamic (
                Unchecked.defaultof<Graph>,
                fun () ->
                    try
                        (body ()).EvalValue()
                    finally
                        compensation ()
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Using
            (resource: 'D, [<InlineIfLambda>] body: 'D -> MemoStep<'P, 'Prev, 'U>)
            : MemoStep<'OutPhase, 'Prev, 'U> when 'D :> IDisposable =
            MemoRunner.UsingStep (resource, body)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> MemoStep<'P, 'Prev, unit>) : MemoStep<'OutPhase, 'Prev, unit> =
            MemoDynamic (
                Unchecked.defaultof<Graph>,
                fun () ->
                    for item in sequence do
                        (body item).EvalValue()
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> MemoStep<BoundPhase, 'Prev, 'U>) : Memo<'U> =
            MemoRunner.EvalBound (d, false, ValueNone)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> MemoTail<BoundPhase, 'U>) : Memo<'U> =
            MemoRunner.EvalBoundTail (d, ValueNone)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredMemo<PureMode, NoCmp, BoundPhase, 'Prev, 'U>) : Memo<'U> =
            MemoRunner.EvalBound ((fun () -> (d ()).Body()), false, ValueNone)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredMemo<OwningMode, NoCmp, BoundPhase, 'Prev, 'U>) : Memo<'U> =
            MemoRunner.EvalBound ((fun () -> (d ()).Body()), true, ValueNone)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredMemo<PureMode, WithCmp<'U>, BoundPhase, 'Prev, 'U>) : Memo<'U> =
            let cfg = d ()
            MemoRunner.EvalBound (cfg.Body, false, ValueSome cfg.Header.Cmp.Comparer)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredMemo<OwningMode, WithCmp<'U>, BoundPhase, 'Prev, 'U>) : Memo<'U> =
            let cfg = d ()
            MemoRunner.EvalBound (cfg.Body, true, ValueSome cfg.Header.Cmp.Comparer)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredMemo<OptionMode, NoCmp, BoundPhase, NoPrev, 'A option>) : Memo<'A option> =
            MemoRunner.EvalBoundOption (fun () -> (d ()).Body())

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredMemoTail<OwningMode, NoCmp, BoundPhase, 'U>) : Memo<'U> =
            MemoRunner.EvalBoundTail ((fun () -> (d ()).Body()), ValueNone)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredMemoTail<PureMode, WithCmp<'U>, BoundPhase, 'U>) : Memo<'U> =
            let cfg = d ()
            MemoRunner.EvalBoundTail (cfg.Body, ValueSome cfg.Header.Cmp.Comparer)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredMemoTail<OwningMode, WithCmp<'U>, BoundPhase, 'U>) : Memo<'U> =
            let cfg = d ()
            MemoRunner.EvalBoundTail (cfg.Body, ValueSome cfg.Header.Cmp.Comparer)

    type EffectBuilder with
        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Combine
            (first: EffectStep<'L1, 'P1>, [<InlineIfLambda>] second: unit -> EffectStep<'L2, 'P2>)
            : EffectStep<NotLeaf, 'OutPhase> =
            EffectDynamicFn (
                Unchecked.defaultof<Graph>,
                fun () ->
                    first.EvalUnit ()
                    (second ()).EvalUnit()
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.While
            ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> EffectStep<'L, 'P>)
            : EffectStep<NotLeaf, 'OutPhase> =
            EffectDynamicFn (
                Unchecked.defaultof<Graph>,
                fun () ->
                    while guard () do
                        (body ()).EvalUnit()
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryWith
            ([<InlineIfLambda>] body: unit -> EffectStep<'L1, 'P1>, [<InlineIfLambda>] handler: exn -> EffectStep<'L2, 'P2>)
            : EffectStep<NotLeaf, 'OutPhase> =
            EffectDynamicFn (
                Unchecked.defaultof<Graph>,
                fun () ->
                    try
                        (body ()).EvalUnit()
                    with ex ->
                        CeRuntime.RunTryWithHandler (ex, fun () -> (handler ex).EvalUnit())
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryFinally
            ([<InlineIfLambda>] body: unit -> EffectStep<'L, 'P>, [<InlineIfLambda>] compensation: unit -> unit)
            : EffectStep<NotLeaf, 'OutPhase> =
            EffectDynamicFn (
                Unchecked.defaultof<Graph>,
                fun () ->
                    try
                        (body ()).EvalUnit()
                    finally
                        compensation ()
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Using
            (resource: 'D, [<InlineIfLambda>] body: 'D -> EffectStep<'L, 'P>)
            : EffectStep<NotLeaf, 'OutPhase> when 'D :> IDisposable =
            EffectRunner.UsingStep (resource, body)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> EffectStep<'L, 'P>) : EffectStep<NotLeaf, 'OutPhase> =
            EffectDynamicFn (
                Unchecked.defaultof<Graph>,
                fun () ->
                    for item in sequence do
                        (body item).EvalUnit()
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Bind(source: IReactive<'K>, continuation: 'K -> EffectStep<'AnyLeaf, 'Phase>) : EffectStep<NotLeaf, BoundPhase> =
            if InternalCeState.IsEvaluating then
                let key = CeRuntime.ReadEffectTracked source
                CeRuntime.RunEffectContinuation (source, key, fun k -> (continuation k).EvalUnit())
                EffectUnit
            else
                let g = source.Graph

                EffectDynamicFn (
                    g,
                    fun () ->
                        let key = CeRuntime.ReadEffectTracked source
                        CeRuntime.RunEffectContinuation (source, key, fun k -> (continuation k).EvalUnit())
                )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> EffectStep<'Leaf, BoundPhase>) : unit =
            EffectRunner.EvalBound (d, false) |> ignore

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredEffect<UnitOut, 'Leaf, BoundPhase>) : unit =
            EffectRunner.EvalBound ((fun () -> (d ()).Body()), false)
            |> ignore

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredEffect<NodeOut, 'Leaf, BoundPhase>) : INode =
            EffectRunner.EvalBound ((fun () -> (d ()).Body()), true)

    type EditableBuilder with
        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Combine
            (first: EditableStep<'P1, 'Prev, unit>, [<InlineIfLambda>] second: unit -> EditableStep<'P2, 'Prev, 'U>)
            : EditableStep<'OutPhase, 'Prev, 'U> =
            EditableRunner.CombineSteps (first, second)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.While
            ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> EditableStep<'P, 'Prev, unit>)
            : EditableStep<'OutPhase, 'Prev, unit> =
            EditableRunner.WhileStep (guard, body)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryWith
            ([<InlineIfLambda>] body: unit -> EditableStep<'P1, 'Prev, 'U>, [<InlineIfLambda>] handler: exn -> EditableStep<'P2, 'Prev, 'U>)
            : EditableStep<'OutPhase, 'Prev, 'U> =
            EditableRunner.TryWithStep (body, handler)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryFinally
            ([<InlineIfLambda>] body: unit -> EditableStep<'P, 'Prev, 'U>, [<InlineIfLambda>] compensation: unit -> unit)
            : EditableStep<'OutPhase, 'Prev, 'U> =
            EditableRunner.TryFinallyStep (body, compensation)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Using
            (resource: 'D, [<InlineIfLambda>] body: 'D -> EditableStep<'P, 'Prev, 'U>)
            : EditableStep<'OutPhase, 'Prev, 'U> when 'D :> IDisposable =
            EditableRunner.UsingStep (resource, body)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> EditableStep<'P, 'Prev, unit>) : EditableStep<'OutPhase, 'Prev, unit> =
            EditableRunner.ForSeqStep (sequence, body)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> EditableStep<BoundPhase, 'Prev, 'U>) : Editable<'U> =
            EditableRunner.EvalBound (d, false)

    type DraftBuilder with
        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Combine
            (first: EditableStep<'P1, 'Prev, unit>, [<InlineIfLambda>] second: unit -> EditableStep<'P2, 'Prev, 'U>)
            : EditableStep<'OutPhase, 'Prev, 'U> =
            EditableRunner.CombineSteps (first, second)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.While
            ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> EditableStep<'P, 'Prev, unit>)
            : EditableStep<'OutPhase, 'Prev, unit> =
            EditableRunner.WhileStep (guard, body)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryWith
            ([<InlineIfLambda>] body: unit -> EditableStep<'P1, 'Prev, 'U>, [<InlineIfLambda>] handler: exn -> EditableStep<'P2, 'Prev, 'U>)
            : EditableStep<'OutPhase, 'Prev, 'U> =
            EditableRunner.TryWithStep (body, handler)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryFinally
            ([<InlineIfLambda>] body: unit -> EditableStep<'P, 'Prev, 'U>, [<InlineIfLambda>] compensation: unit -> unit)
            : EditableStep<'OutPhase, 'Prev, 'U> =
            EditableRunner.TryFinallyStep (body, compensation)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Using
            (resource: 'D, [<InlineIfLambda>] body: 'D -> EditableStep<'P, 'Prev, 'U>)
            : EditableStep<'OutPhase, 'Prev, 'U> when 'D :> IDisposable =
            EditableRunner.UsingStep (resource, body)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> EditableStep<'P, 'Prev, unit>) : EditableStep<'OutPhase, 'Prev, unit> =
            EditableRunner.ForSeqStep (sequence, body)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> EditableStep<BoundPhase, 'Prev, 'U>) : Editable<'U> =
            EditableRunner.EvalBound (d, true)

    type BoundaryBuilder with
        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Combine
            (first: BoundaryStep<'P1, unit>, [<InlineIfLambda>] second: unit -> BoundaryStep<'P2, 'U>)
            : BoundaryStep<'OutPhase, 'U> =
            BoundaryDynamic (
                Unchecked.defaultof<Graph>,
                fun () ->
                    first.EvalValue ()
                    (second ()).EvalValue()
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.While
            ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> BoundaryStep<'P, unit>)
            : BoundaryStep<'OutPhase, unit> =
            BoundaryDynamic (
                Unchecked.defaultof<Graph>,
                fun () ->
                    while guard () do
                        (body ()).EvalValue()
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryWith
            ([<InlineIfLambda>] body: unit -> BoundaryStep<'P1, 'U>, [<InlineIfLambda>] handler: exn -> BoundaryStep<'P2, 'U>)
            : BoundaryStep<'OutPhase, 'U> =
            BoundaryDynamic (
                Unchecked.defaultof<Graph>,
                fun () ->
                    try
                        (body ()).EvalValue()
                    with ex ->
                        CeRuntime.RunTryWithHandler (ex, fun () -> (handler ex).EvalValue())
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryFinally
            ([<InlineIfLambda>] body: unit -> BoundaryStep<'P, 'U>, [<InlineIfLambda>] compensation: unit -> unit)
            : BoundaryStep<'OutPhase, 'U> =
            BoundaryDynamic (
                Unchecked.defaultof<Graph>,
                fun () ->
                    try
                        (body ()).EvalValue()
                    finally
                        compensation ()
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Using
            (resource: 'D, [<InlineIfLambda>] body: 'D -> BoundaryStep<'P, 'U>)
            : BoundaryStep<'OutPhase, 'U> when 'D :> IDisposable =
            BoundaryRunner.UsingStep (resource, body)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> BoundaryStep<'P, unit>) : BoundaryStep<'OutPhase, unit> =
            BoundaryDynamic (
                Unchecked.defaultof<Graph>,
                fun () ->
                    for item in sequence do
                        (body item).EvalValue()
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredBoundary<WithPending<'U>, NoFailed, NoCmp, BoundPhase, 'U>) : Boundary<'U> =
            let cfg = d ()
            BoundaryRunner.EvalBound (cfg.Body, ValueSome cfg.Header.Pend.OnPending, ValueNone, ValueNone)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredBoundary<NoPending, WithFailed<'U>, NoCmp, BoundPhase, 'U>) : Boundary<'U> =
            let cfg = d ()
            BoundaryRunner.EvalBound (cfg.Body, ValueNone, ValueSome cfg.Header.Fail.OnFailed, ValueNone)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredBoundary<WithPending<'U>, WithFailed<'U>, NoCmp, BoundPhase, 'U>) : Boundary<'U> =
            let cfg = d ()
            BoundaryRunner.EvalBound (cfg.Body, ValueSome cfg.Header.Pend.OnPending, ValueSome cfg.Header.Fail.OnFailed, ValueNone)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredBoundary<WithPending<'U>, NoFailed, WithCmp<'U>, BoundPhase, 'U>) : Boundary<'U> =
            let cfg = d ()
            BoundaryRunner.EvalBound (cfg.Body, ValueSome cfg.Header.Pend.OnPending, ValueNone, ValueSome cfg.Header.Cmp.Comparer)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredBoundary<NoPending, WithFailed<'U>, WithCmp<'U>, BoundPhase, 'U>) : Boundary<'U> =
            let cfg = d ()
            BoundaryRunner.EvalBound (cfg.Body, ValueNone, ValueSome cfg.Header.Fail.OnFailed, ValueSome cfg.Header.Cmp.Comparer)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredBoundary<WithPending<'U>, WithFailed<'U>, WithCmp<'U>, BoundPhase, 'U>) : Boundary<'U> =
            let cfg = d ()

            BoundaryRunner.EvalBound (
                cfg.Body,
                ValueSome cfg.Header.Pend.OnPending,
                ValueSome cfg.Header.Fail.OnFailed,
                ValueSome cfg.Header.Cmp.Comparer
            )

    type RowBuilder with
        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Combine(first: RowStep<'P1, unit>, [<InlineIfLambda>] second: unit -> RowStep<'P2, 'V>) : RowStep<'OutPhase, 'V> =
            RowThunk (fun () ->
                first.EvalValue ()
                (second ()).EvalValue())

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.While([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> RowStep<'P, unit>) : RowStep<'OutPhase, unit> =
            RowThunk (fun () ->
                while guard () do
                    (body ()).EvalValue())

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryWith
            ([<InlineIfLambda>] body: unit -> RowStep<'P1, 'V>, [<InlineIfLambda>] handler: exn -> RowStep<'P2, 'V>)
            : RowStep<'OutPhase, 'V> =
            RowThunk (fun () ->
                try
                    (body ()).EvalValue()
                with ex ->
                    CeRuntime.RunTryWithHandler (ex, fun () -> (handler ex).EvalValue()))

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryFinally
            ([<InlineIfLambda>] body: unit -> RowStep<'P, 'V>, [<InlineIfLambda>] compensation: unit -> unit)
            : RowStep<'OutPhase, 'V> =
            RowThunk (fun () ->
                try
                    (body ()).EvalValue()
                finally
                    compensation ())

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Using(resource: 'D, [<InlineIfLambda>] body: 'D -> RowStep<'P, 'V>) : RowStep<'OutPhase, 'V> when 'D :> IDisposable =
            RowThunk (fun () -> CeRuntime.RunUsing (resource, fun () -> (body resource).EvalValue()))

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> RowStep<'P, unit>) : RowStep<'OutPhase, unit> =
            RowThunk (fun () ->
                for item in sequence do
                    (body item).EvalValue())

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> RowStep<BoundPhase, 'V>) : unit -> 'V =
            fun () -> CeRuntime.EvalRow (fun () -> (d ()).EvalValue())

    type AsyncMemoBuilder with
        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.ReturnFrom(value: Task) : AsyncStep<'Leaf, 'Phase, unit> =
            AsyncRunner.ReturnUnitTask value

#if !FABLE_COMPILER
        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.ReturnFrom(value: ValueTask) : AsyncStep<'Leaf, 'Phase, unit> =
            AsyncRunner.ReturnUnitValueTask value
#endif

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Combine
            (first: AsyncStep<'L1, 'P1, unit>, [<InlineIfLambda>] second: unit -> AsyncStep<'L2, 'P2, 'U>)
            : AsyncStep<NotLeaf, 'OutPhase, 'U> =
            AsyncDynamic (Unchecked.defaultof<Graph>, fun prev ct -> AsyncRunner.EvalCombine (first, second, prev, ct))

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.While
            ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> AsyncStep<'L, 'P, unit>)
            : AsyncStep<NotLeaf, 'OutPhase, unit> =
            AsyncDynamic (Unchecked.defaultof<Graph>, fun _ ct -> AsyncRunner.EvalWhile (guard, body, ct))

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryWith
            ([<InlineIfLambda>] body: unit -> AsyncStep<'L1, 'P1, 'U>, [<InlineIfLambda>] handler: exn -> AsyncStep<'L2, 'P2, 'U>)
            : AsyncStep<NotLeaf, 'OutPhase, 'U> =
            AsyncDynamic (Unchecked.defaultof<Graph>, fun prev ct -> AsyncRunner.EvalTryWith (body, handler, prev, ct))

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryFinally
            ([<InlineIfLambda>] body: unit -> AsyncStep<'L, 'P, 'U>, [<InlineIfLambda>] compensation: unit -> unit)
            : AsyncStep<NotLeaf, 'OutPhase, 'U> =
            AsyncDynamic (Unchecked.defaultof<Graph>, fun prev ct -> AsyncRunner.EvalTryFinally (body, compensation, prev, ct))

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Using
            (resource: 'D, [<InlineIfLambda>] body: 'D -> AsyncStep<'L, 'P, 'U>)
            : AsyncStep<NotLeaf, 'OutPhase, 'U> when 'D :> IDisposable =
            AsyncRunner.UsingStep (resource, body)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> AsyncStep<'L, 'P, unit>) : AsyncStep<NotLeaf, 'OutPhase, unit> =
            AsyncDynamic (Unchecked.defaultof<Graph>, fun _ ct -> AsyncRunner.EvalForSeq (sequence, body, ct))

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.BindReturn(source: Task, f: unit -> 'U) : AsyncStep<'OutLeaf, 'OutPhase, 'U> =
            AsyncDynamic (Unchecked.defaultof<Graph>, fun _ _ -> AsyncRunner.EvalBindUnitTaskReturn (source, f))

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Bind(source: Task, continuation: unit -> AsyncStep<IsLeaf, ColdPhase, 'U>) : AsyncStep<'OutLeaf, 'OutPhase, 'U> =
            AsyncDynamic (Unchecked.defaultof<Graph>, fun prev ct -> AsyncRunner.EvalBindUnitTask (source, continuation, prev, ct))

#if !FABLE_COMPILER
        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.BindReturn(source: ValueTask, f: unit -> 'U) : AsyncStep<'OutLeaf, 'OutPhase, 'U> =
            AsyncDynamic (Unchecked.defaultof<Graph>, fun _ _ -> AsyncRunner.EvalBindUnitValueTaskReturn (source, f))

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Bind(source: ValueTask, continuation: unit -> AsyncStep<IsLeaf, ColdPhase, 'U>) : AsyncStep<'OutLeaf, 'OutPhase, 'U> =
            AsyncDynamic (Unchecked.defaultof<Graph>, fun prev ct -> AsyncRunner.EvalBindUnitValueTask (source, continuation, prev, ct))
#endif

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Bind(source: IReactive<'K>, continuation: 'K -> AsyncStep<'AnyLeaf, 'ContPhase, 'U>) : AsyncStep<NotLeaf, BoundPhase, 'U> =
            AsyncRunner.BindReactiveDynamic (source, continuation)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> AsyncStep<'Leaf, BoundPhase, 'U>) : AsyncMemo<'U> =
            AsyncRunner.EvalBound (d, false, ValueNone)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredAsync<PureMode, DefaultPolicy, 'Leaf, BoundPhase, 'U>) : AsyncMemo<'U> =
            AsyncRunner.EvalBound ((fun () -> (d ()).Body()), false, ValueNone)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredAsync<OwningMode, DefaultPolicy, 'Leaf, BoundPhase, 'U>) : AsyncMemo<'U> =
            AsyncRunner.EvalBound ((fun () -> (d ()).Body()), true, ValueNone)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredAsync<PureMode, ExplicitPolicy, 'Leaf, BoundPhase, 'U>) : AsyncMemo<'U> =
            let cfg = d ()
            AsyncRunner.EvalBound (cfg.Body, false, ValueSome cfg.Header.Pol.Policy)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredAsync<OwningMode, ExplicitPolicy, 'Leaf, BoundPhase, 'U>) : AsyncMemo<'U> =
            let cfg = d ()
            AsyncRunner.EvalBound (cfg.Body, true, ValueSome cfg.Header.Pol.Policy)

    type StreamBuilder with
        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Combine
            (first: StreamStep<'L1, 'P1, unit>, [<InlineIfLambda>] second: unit -> StreamStep<'L2, 'P2, 'T>)
            : StreamStep<NotLeaf, 'OutPhase, 'T> =
            StreamDynamic (
                Unchecked.defaultof<Graph>,
                fun ct ->
                    first.EvalUnit ct
                    (second ()).EvalResult(ct)
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.While
            ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> StreamStep<'L, 'P, unit>)
            : StreamStep<NotLeaf, 'OutPhase, unit> =
            StreamDynamic (
                Unchecked.defaultof<Graph>,
                fun ct ->
                    while guard () do
                        (body ()).EvalUnit(ct)

                    StreamEvalUnit
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryWith
            ([<InlineIfLambda>] body: unit -> StreamStep<'L1, 'P1, 'T>, [<InlineIfLambda>] handler: exn -> StreamStep<'L2, 'P2, 'T>)
            : StreamStep<NotLeaf, 'OutPhase, 'T> =
            StreamDynamic (
                Unchecked.defaultof<Graph>,
                fun ct ->
                    try
                        (body ()).EvalResult(ct)
                    with ex ->
                        CeRuntime.RunTryWithHandler (ex, fun () -> (handler ex).EvalResult(ct))
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.TryFinally
            ([<InlineIfLambda>] body: unit -> StreamStep<'L, 'P, 'T>, [<InlineIfLambda>] compensation: unit -> unit)
            : StreamStep<NotLeaf, 'OutPhase, 'T> =
            StreamDynamic (
                Unchecked.defaultof<Graph>,
                fun ct ->
                    try
                        (body ()).EvalResult(ct)
                    finally
                        compensation ()
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Using
            (resource: 'D, [<InlineIfLambda>] body: 'D -> StreamStep<'L, 'P, 'T>)
            : StreamStep<NotLeaf, 'OutPhase, 'T> when 'D :> IDisposable =
            StreamRunner.UsingStep (resource, body)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.For
            (sequence: seq<'TItem>, [<InlineIfLambda>] body: 'TItem -> StreamStep<'L, 'P, unit>)
            : StreamStep<NotLeaf, 'OutPhase, unit> =
            StreamDynamic (
                Unchecked.defaultof<Graph>,
                fun ct ->
                    for item in sequence do
                        (body item).EvalUnit(ct)

                    StreamEvalUnit
            )

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Bind(source: IReactive<'K>, continuation: 'K -> StreamStep<'AnyLeaf, 'ContPhase, 'T>) : StreamStep<NotLeaf, BoundPhase, 'T> =
            StreamRunner.BindReactiveDynamic (source, continuation)

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> StreamStep<'Leaf, BoundPhase, 'T>) : StreamMemo<'T> =
            StreamRunner.EvalBoundLatest d

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredStream<LatestMode, 'Leaf, BoundPhase, 'T>) : StreamMemo<'T> =
            StreamRunner.EvalBoundLatest (fun () -> (d ()).Body())

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredStream<FoldMode<'S, 'T>, 'Leaf, BoundPhase, 'T>) : StreamMemo<'S> =
            let cfg = d ()
            StreamRunner.EvalBoundFold (cfg.Body, cfg.Header.Mode.Seed, cfg.Header.Mode.Folder)
