namespace Ranvier.Internal

open System
open System.Collections.Generic
open System.ComponentModel
open System.Runtime.CompilerServices
open Ranvier

/// <summary>Phantom phase marker indicating a computation-expression step whose body has not executed yet.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type ColdPhase = struct end

/// <summary>Phantom phase marker indicating a computation-expression step rooted in a reactive bind.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type BoundPhase = struct end

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

#if FABLE_COMPILER
module private CeEvalSlot =
    let mutable evalDepth = 0
    let mutable hasPrev = false
    let mutable prevValue: obj = null
    let mutable effectObserver: obj = null
    let mutable hasReactiveBind = false
    let mutable isOwning = false
    let mutable optionDedup: obj = null
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

    static member CurrentEffectObserver: obj = CeEvalSlot.effectObserver

    static member SwapEffectObserver(obs: obj) : obj =
        let prev = CeEvalSlot.effectObserver
        CeEvalSlot.effectObserver <- obs
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
#else
    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private evalDepth: int

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private hasPrev: bool

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private prevValue: obj

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private effectObserver: obj

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private hasReactiveBind: bool

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private isOwning: bool

    [<ThreadStatic; Microsoft.FSharp.Core.DefaultValue>]
    static val mutable private optionDedup: obj

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

    static member CurrentEffectObserver: obj = InternalCeState.effectObserver

    [<MethodImpl(MethodImplOptions.AggressiveInlining)>]
    static member SwapEffectObserver(obs: obj) : obj =
        let prev = InternalCeState.effectObserver
        InternalCeState.effectObserver <- obs
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
    static member private PickPureHost(isRow: bool, g: Graph) : IScopeHost =
        let fallbackHost =
            if isRow then
                PureCeScopeHost.RowInstance
            else
                PureCeScopeHost.MemoInstance

        let obs = g.CurrentObserver

#if FABLE_COMPILER
        if
            not (Platform.isNull (box obs))
            && Platform.hasMember obs "CanOwn"
        then
            let sh = box obs :?> IScopeHost
            if not sh.CanOwn then sh else fallbackHost
        else
            fallbackHost
#else
        if not (Platform.isNull (box obs)) then
            match obs with
            | :? IScopeHost as sh when not sh.CanOwn -> sh
            | _ -> fallbackHost
        else
            fallbackHost
#endif

    static member RunUntrackedPureArg<'A, 'T>(isRow: bool, g: Graph, f: 'A -> 'T, arg: 'A) : 'T =
        let host = CeRuntime.PickPureHost (isRow, g)

        try
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
        with NotReadyException _ as ex ->
            raise (UntrackedPhase.wrapPending UntrackedPhase.mapPending ex)

    static member RunUntrackedRowArg<'A, 'T>(f: 'A -> 'T, arg: 'A) : 'T =
#if FABLE_COMPILER
        match Graph.TryCurrent with
        | ValueSome g -> CeRuntime.RunUntrackedPureArg (true, g, f, arg)
        | ValueNone -> f arg
#else
        let mutable g = Unchecked.defaultof<Graph>

        if Graph.TryGetCurrent &g then
            CeRuntime.RunUntrackedPureArg (true, g, f, arg)
        else
            f arg
#endif

    static member RunUntrackedMemoBind<'A, 'U>(owning: bool, f: 'A -> 'U, arg: 'A) : 'U =
        let g = Graph.Current

        if owning then
            try
                g.RunUntrackedWith (f, arg)
            with NotReadyException _ as ex ->
                raise (UntrackedPhase.wrapPending UntrackedPhase.mapPending ex)
        else
            CeRuntime.RunUntrackedPureArg (false, g, f, arg)

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
            let savedPrev = InternalCeState.SwapPrevious prev

            try
                eval ()
            finally
                InternalCeState.RestorePrevious savedPrev
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
            let savedPrev = InternalCeState.SuspendPrevious ()

            try
                eval ()
            finally
                InternalCeState.RestorePrevious savedPrev
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
        let g = source.Graph
        let savedObs = InternalCeState.CurrentEffectObserver
        let activeObs = g.CurrentObserver

        if not (Platform.isNull (box activeObs)) then
            source.ReadTracked ()
        elif not (Platform.isNull savedObs) then
            g.RunHosted (unbox<IComputation> savedObs, fun () -> source.ReadTracked ())
        else
            source.ReadTracked ()

    static member RunEffectContinuation<'K>(source: IReactive<'K>, key: 'K, cont: 'K -> unit) : unit =
        let g = source.Graph

        let obs: obj =
            let active = g.CurrentObserver

            if not (Platform.isNull (box active)) then
                box active
            else
                InternalCeState.CurrentEffectObserver

        let prevObs = InternalCeState.SwapEffectObserver obs

        try
            g.RunUntrackedWith (cont, key)
        finally
            InternalCeState.SwapEffectObserver prevObs
            |> ignore

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
            let savedObs = InternalCeState.SwapEffectObserver (box graph.CurrentObserver)

            try
                eval ()
            finally
                InternalCeState.SwapEffectObserver savedObs
                |> ignore

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
            let savedPrev = InternalCeState.SwapPrevious prev

            try
                eval ()
            finally
                InternalCeState.RestorePrevious savedPrev
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
            let savedPrev = InternalCeState.SuspendPrevious ()

            try
                eval ()
            finally
                InternalCeState.RestorePrevious savedPrev
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
        let savedObs = InternalCeState.SwapEffectObserver null
        let savedDedup = InternalCeState.SwapOptionDedup null

        try
            build ()
        finally
            InternalCeState.SwapOptionDedup savedDedup
            |> ignore

            InternalCeState.SwapEffectObserver savedObs
            |> ignore

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
            let step = d ()

            match step with
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

            let step = d ()

            match step with
            | MemoStatic (_, factory) -> factory false ValueNone (box dedup)
            | MemoDynamic (g, _) ->
                let graph = if Platform.isNull (box g) then Graph.Current else g
                CeRuntime.CreateDynamicOptionMemo (graph, fun () -> (d ()).EvalValue())
            | MemoInline _ -> CeRuntime.CreateDynamicOptionMemo (Graph.Current, fun () -> (d ()).EvalValue()))

    static member EvalColdOption<'A>(d: unit -> MemoStep<ColdPhase, NoPrev, 'A option>) : Memo<'A option> =
        CeRuntime.CreateDynamicOptionMemo (Graph.Current, fun () -> (d ()).EvalValue())

    static member EvalBoundTail<'U>(d: unit -> MemoTail<BoundPhase, 'U>, cmp: IEqualityComparer<'U> voption) : Memo<'U> =
        CeRuntime.SuspendNested (fun () ->
            match d () with
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

    static member BindSource<'K, 'Prev, 'U>(source: IReactive<'K>, f: 'K -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        if InternalCeState.IsEvaluating then
            let key = source.ReadTracked ()
            MemoInline (CeRuntime.RunUntrackedMemoBind (InternalCeState.IsOwning, f, key))
        else
            let g = source.Graph

            MemoStatic (g, fun owning cmp dedup -> CeRuntime.CreateMemoFromReactive (source, f, owning, cmp, dedup))

    static member BindSignal<'K, 'Prev, 'U>(source: Signal<'K>, f: 'K -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        if InternalCeState.IsEvaluating then
            let key = source.Value
            MemoInline (CeRuntime.RunUntrackedMemoBind (InternalCeState.IsOwning, f, key))
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
            let key = source.Value
            MemoInline (CeRuntime.RunUntrackedMemoBind (InternalCeState.IsOwning, f, key))
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
        if InternalCeState.IsEvaluating then
            let key = (source :> IReactive<struct ('A * 'B)>).ReadTracked()
            MemoInline (CeRuntime.RunUntrackedMemoBind (InternalCeState.IsOwning, f, key))
        else
            let g = (source :> IReactive<struct ('A * 'B)>).Graph

            MemoStatic (
                g,
                fun owning cmp dedup ->
                    if not owning && cmp.IsNone && isNull dedup then
                        CombinatorMemos.createSignalMap2 (fun a b -> f (struct (a, b))) source.Source1 source.Source2
                    else
                        CeRuntime.CreateStaticMemo (g, (source :> IReactive<struct ('A * 'B)>).ReadTracked, f, owning, cmp, ValueNone, dedup)
            )

    static member BindMergedMemos<'A, 'B, 'Prev, 'U>(source: MergedMemos<'A, 'B>, f: struct ('A * 'B) -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        if InternalCeState.IsEvaluating then
            let key = (source :> IReactive<struct ('A * 'B)>).ReadTracked()
            MemoInline (CeRuntime.RunUntrackedMemoBind (InternalCeState.IsOwning, f, key))
        else
            let g = (source :> IReactive<struct ('A * 'B)>).Graph

            MemoStatic (
                g,
                fun owning cmp dedup ->
                    if not owning && cmp.IsNone && isNull dedup then
                        CombinatorMemos.createMemoMap2 (fun a b -> f (struct (a, b))) source.Source1 source.Source2
                    else
                        CeRuntime.CreateStaticMemo (g, (source :> IReactive<struct ('A * 'B)>).ReadTracked, f, owning, cmp, ValueNone, dedup)
            )

    static member BindMergedReactive<'A, 'B, 'Prev, 'U>(source: MergedReactive<'A, 'B>, f: struct ('A * 'B) -> 'U) : MemoStep<BoundPhase, 'Prev, 'U> =
        if InternalCeState.IsEvaluating then
            let key = (source :> IReactive<struct ('A * 'B)>).ReadTracked()
            MemoInline (CeRuntime.RunUntrackedMemoBind (InternalCeState.IsOwning, f, key))
        else
            let g = (source :> IReactive<struct ('A * 'B)>).Graph

            MemoStatic (g, fun owning cmp dedup -> CeRuntime.CreateMemoFromMergedReactive (source, f, owning, cmp, dedup))

    static member BindReactive<'K, 'Phase, 'Prev, 'U>
        (source: IReactive<'K>, continuation: 'K -> MemoStep<'Phase, 'Prev, 'U>)
        : MemoStep<BoundPhase, 'Prev, 'U> =
        let run () =
            let key = source.ReadTracked ()
            let saved = InternalCeState.SwapReactiveBind true

            try
                (continuation key).EvalValue()
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
            let inner = continuation probeValue
            let g = inner.TryGraph

            MemoDynamic (g, fun () -> (continuation (readPrev ())).EvalValue())

    static member BindReturnPrevVal<'T, 'Phase, 'U>(prev: 'T, f: 'T -> 'U) : MemoStep<'Phase, WithPrev, 'U> =
        if InternalCeState.HasReactiveBind then
            MemoInline (CeRuntime.RunUntrackedMemoBind (InternalCeState.IsOwning, f, prev))
        else
            MemoInline (f prev)

/// <summary>Effect step carrier supporting 1-bind <c>EffectOn</c> vs multi-bind dynamic <c>Effect</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type EffectStep<'Phase> =
    | EffectUnit
    | EffectDynamicFn of effGraph: Graph * effEval: (unit -> unit)
    | EffectStatic of effOnGraph: Graph * effFactory: (bool -> INode)

    member this.EvalUnit() =
        match this with
        | EffectUnit -> ()
        | EffectDynamicFn (_, f) -> f ()
        | EffectStatic (_, _) -> ()

/// <summary>Header descriptor configured via <c>node</c> inside <c>effect</c>.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type EffectHeader<'Out> = struct end

/// <summary>Configured <c>effect</c> step preserving output handle flavor and phase.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type ConfiguredEffect<'Out, 'Phase> =
    val Body: unit -> EffectStep<'Phase>
    new(body: unit -> EffectStep<'Phase>) = { Body = body }

/// <summary>Static runtime helpers for <c>EffectBuilder</c>.</summary>
[<AbstractClass; Sealed; EditorBrowsable(EditorBrowsableState.Never)>]
type EffectRunner =
    static member EvalBound(d: unit -> EffectStep<BoundPhase>, asNode: bool) : INode =
        CeRuntime.SuspendNested (fun () ->
            match d () with
            | EffectStatic (_, factory) -> factory asNode
            | EffectDynamicFn (g, _) ->
                let graph = if Platform.isNull (box g) then Graph.Current else g
                CeRuntime.CreateDynamicEffect (graph, (fun () -> (d ()).EvalUnit()), asNode)
            | EffectUnit -> CeRuntime.CreateDynamicEffect (Graph.Current, (fun () -> (d ()).EvalUnit()), asNode))

    static member EvalCold(d: unit -> EffectStep<ColdPhase>, asNode: bool) : INode =
        CeRuntime.CreateDynamicEffect (Graph.Current, (fun () -> (d ()).EvalUnit()), asNode)

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
            let step = d ()

            match step with
            | EditDynamic (g, _) ->
                let graph = if Platform.isNull (box g) then Graph.Current else g
                CeRuntime.CreateEditable (graph, (fun () -> (d ()).EvalValue()), draft)
            | EditInline _ -> CeRuntime.CreateEditable (Graph.Current, (fun () -> (d ()).EvalValue()), draft))

    static member EvalCold<'Prev, 'U>(d: unit -> EditableStep<ColdPhase, 'Prev, 'U>, draft: bool) : Editable<'U> =
        CeRuntime.CreateEditable (Graph.Current, (fun () -> (d ()).EvalValue()), draft)

    static member BindReturnReactive<'K, 'Prev, 'U>(source: IReactive<'K>, f: 'K -> 'U) : EditableStep<BoundPhase, 'Prev, 'U> =
        let run () =
            let key = source.ReadTracked ()
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
            let key = source.ReadTracked ()
            let saved = InternalCeState.SwapReactiveBind true

            try
                (continuation key).EvalValue()
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
            let inner = continuation probeValue
            let g = inner.TryGraph

            EditDynamic (g, fun () -> (continuation (readPrev ())).EvalValue())

    static member CombineSteps<'P1, 'P2, 'Prev, 'U>
        (first: EditableStep<'P1, 'Prev, unit>, second: unit -> EditableStep<'P2, 'Prev, 'U>)
        : EditableStep<ColdPhase, 'Prev, 'U> =
        EditDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                first.EvalValue ()
                (second ()).EvalValue()
        )

    static member WhileStep<'P, 'Prev>(guard: unit -> bool, body: unit -> EditableStep<'P, 'Prev, unit>) : EditableStep<ColdPhase, 'Prev, unit> =
        EditDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                while guard () do
                    (body ()).EvalValue()
        )

    static member TryWithStep<'P1, 'P2, 'Prev, 'U>
        (body: unit -> EditableStep<'P1, 'Prev, 'U>, handler: exn -> EditableStep<'P2, 'Prev, 'U>)
        : EditableStep<ColdPhase, 'Prev, 'U> =
        EditDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalValue()
                with ex ->
                    (handler ex).EvalValue()
        )

    static member TryFinallyStep<'P, 'Prev, 'U>
        (body: unit -> EditableStep<'P, 'Prev, 'U>, compensation: unit -> unit)
        : EditableStep<ColdPhase, 'Prev, 'U> =
        EditDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalValue()
                finally
                    compensation ()
        )

    static member UsingStep<'D, 'P, 'Prev, 'U when 'D :> IDisposable>
        (resource: 'D, body: 'D -> EditableStep<'P, 'Prev, 'U>)
        : EditableStep<ColdPhase, 'Prev, 'U> =
        EditDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body resource).EvalValue()
                finally
                    if not (Platform.isNull (box resource)) then
                        resource.Dispose ()
        )

    static member ForSeqStep<'T, 'P, 'Prev>(sequence: seq<'T>, body: 'T -> EditableStep<'P, 'Prev, unit>) : EditableStep<ColdPhase, 'Prev, unit> =
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
            match d () with
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

    static member BindReturnReactive<'K, 'U>(source: IReactive<'K>, f: 'K -> 'U) : BoundaryStep<BoundPhase, 'U> =
        if InternalCeState.IsEvaluating then
            let key = source.ReadTracked ()
            BoundaryInline (source.Graph.RunUntrackedWith (f, key))
        else
            let g = source.Graph

            BoundaryDynamic (
                g,
                fun () ->
                    let key = source.ReadTracked ()
                    g.RunUntrackedWith (f, key)
            )

    static member BindReactive<'K, 'Phase, 'U>(source: IReactive<'K>, continuation: 'K -> BoundaryStep<'Phase, 'U>) : BoundaryStep<BoundPhase, 'U> =
        if InternalCeState.IsEvaluating then
            let key = source.ReadTracked ()
            BoundaryInline ((continuation key).EvalValue())
        else
            let g = source.Graph

            BoundaryDynamic (
                g,
                fun () ->
                    let key = source.ReadTracked ()
                    (continuation key).EvalValue()
            )

/// <summary>Step carrier for <c>RowBuilder</c> returning <c>unit -&gt; 'V</c> without extra nodes.</summary>
[<Struct; NoEquality; NoComparison; EditorBrowsable(EditorBrowsableState.Never)>]
type RowStep<'Phase, 'V> =
    | RowReadyVal of rowVal: 'V
    | RowThunk of rowEval: (unit -> 'V)

    member this.EvalValue() =
        match this with
        | RowReadyVal v -> v
        | RowThunk f -> f ()

namespace Ranvier.CE

open System
open System.Collections.Generic
open System.ComponentModel
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
        (first: MemoStep<'P1, 'Prev, unit>, [<InlineIfLambda>] second: unit -> MemoStep<'P2, 'Prev, 'U>)
        : MemoStep<ColdPhase, 'Prev, 'U> =
        MemoDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                first.EvalValue ()
                (second ()).EvalValue()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.While
        ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> MemoStep<'P, 'Prev, unit>)
        : MemoStep<ColdPhase, 'Prev, unit> =
        MemoDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                while guard () do
                    (body ()).EvalValue()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryWith
        ([<InlineIfLambda>] body: unit -> MemoStep<'P1, 'Prev, 'U>, [<InlineIfLambda>] handler: exn -> MemoStep<'P2, 'Prev, 'U>)
        : MemoStep<ColdPhase, 'Prev, 'U> =
        MemoDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalValue()
                with ex ->
                    (handler ex).EvalValue()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryFinally
        ([<InlineIfLambda>] body: unit -> MemoStep<'P, 'Prev, 'U>, [<InlineIfLambda>] compensation: unit -> unit)
        : MemoStep<ColdPhase, 'Prev, 'U> =
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
        : MemoStep<ColdPhase, 'Prev, 'U> when 'D :> IDisposable =
        MemoDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body resource).EvalValue()
                finally
                    if not (isNull (box resource)) then
                        resource.Dispose ()
        )

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
    member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> MemoStep<'P, 'Prev, unit>) : MemoStep<ColdPhase, 'Prev, unit> =
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
    member inline _.Zero() : EffectStep<'Phase> =
        EffectUnit

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Delay([<InlineIfLambda>] f: unit -> 'T) : unit -> 'T = f

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Combine(first: EffectStep<'P1>, [<InlineIfLambda>] second: unit -> EffectStep<'P2>) : EffectStep<ColdPhase> =
        EffectDynamicFn (
            Unchecked.defaultof<Graph>,
            fun () ->
                first.EvalUnit ()
                (second ()).EvalUnit()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.While([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> EffectStep<'P>) : EffectStep<ColdPhase> =
        EffectDynamicFn (
            Unchecked.defaultof<Graph>,
            fun () ->
                while guard () do
                    (body ()).EvalUnit()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryWith
        ([<InlineIfLambda>] body: unit -> EffectStep<'P1>, [<InlineIfLambda>] handler: exn -> EffectStep<'P2>)
        : EffectStep<ColdPhase> =
        EffectDynamicFn (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalUnit()
                with ex ->
                    (handler ex).EvalUnit()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryFinally
        ([<InlineIfLambda>] body: unit -> EffectStep<'P>, [<InlineIfLambda>] compensation: unit -> unit)
        : EffectStep<ColdPhase> =
        EffectDynamicFn (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalUnit()
                finally
                    compensation ()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Using(resource: 'D, [<InlineIfLambda>] body: 'D -> EffectStep<'P>) : EffectStep<ColdPhase> when 'D :> IDisposable =
        EffectDynamicFn (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body resource).EvalUnit()
                finally
                    if not (isNull (box resource)) then
                        resource.Dispose ()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(header: EffectHeader<'Out>, [<InlineIfLambda>] body: unit -> EffectStep<'Phase>) : ConfiguredEffect<'Out, 'Phase> =
        ignore header
        ConfiguredEffect<'Out, 'Phase>(body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> EffectStep<'P>) : EffectStep<ColdPhase> =
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
    member inline _.Bind(source: IReactive<'K>, continuation: 'K -> EffectStep<ColdPhase>) : EffectStep<BoundPhase> =
        if InternalCeState.IsEvaluating then
            let key = CeRuntime.ReadEffectTracked source
            CeRuntime.RunEffectContinuation (source, key, fun k -> (continuation k).EvalUnit())
            EffectUnit
        else
            let g = source.Graph

            EffectStatic (g, fun asNode -> CeRuntime.CreateEffectOn (source, (fun k -> (continuation k).EvalUnit()), asNode))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> EffectStep<ColdPhase>) : unit =
        EffectRunner.EvalCold (d, false) |> ignore

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredEffect<UnitOut, ColdPhase>) : unit =
        EffectRunner.EvalCold ((fun () -> (d ()).Body()), false)
        |> ignore

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> ConfiguredEffect<NodeOut, ColdPhase>) : INode =
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
        (first: EditableStep<'P1, 'Prev, unit>, [<InlineIfLambda>] second: unit -> EditableStep<'P2, 'Prev, 'U>)
        : EditableStep<ColdPhase, 'Prev, 'U> =
        EditableRunner.CombineSteps (first, second)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.While
        ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> EditableStep<'P, 'Prev, unit>)
        : EditableStep<ColdPhase, 'Prev, unit> =
        EditableRunner.WhileStep (guard, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryWith
        ([<InlineIfLambda>] body: unit -> EditableStep<'P1, 'Prev, 'U>, [<InlineIfLambda>] handler: exn -> EditableStep<'P2, 'Prev, 'U>)
        : EditableStep<ColdPhase, 'Prev, 'U> =
        EditableRunner.TryWithStep (body, handler)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryFinally
        ([<InlineIfLambda>] body: unit -> EditableStep<'P, 'Prev, 'U>, [<InlineIfLambda>] compensation: unit -> unit)
        : EditableStep<ColdPhase, 'Prev, 'U> =
        EditableRunner.TryFinallyStep (body, compensation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Using
        (resource: 'D, [<InlineIfLambda>] body: 'D -> EditableStep<'P, 'Prev, 'U>)
        : EditableStep<ColdPhase, 'Prev, 'U> when 'D :> IDisposable =
        EditableRunner.UsingStep (resource, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> EditableStep<'P, 'Prev, unit>) : EditableStep<ColdPhase, 'Prev, unit> =
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
        (first: EditableStep<'P1, 'Prev, unit>, [<InlineIfLambda>] second: unit -> EditableStep<'P2, 'Prev, 'U>)
        : EditableStep<ColdPhase, 'Prev, 'U> =
        EditableRunner.CombineSteps (first, second)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.While
        ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> EditableStep<'P, 'Prev, unit>)
        : EditableStep<ColdPhase, 'Prev, unit> =
        EditableRunner.WhileStep (guard, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryWith
        ([<InlineIfLambda>] body: unit -> EditableStep<'P1, 'Prev, 'U>, [<InlineIfLambda>] handler: exn -> EditableStep<'P2, 'Prev, 'U>)
        : EditableStep<ColdPhase, 'Prev, 'U> =
        EditableRunner.TryWithStep (body, handler)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryFinally
        ([<InlineIfLambda>] body: unit -> EditableStep<'P, 'Prev, 'U>, [<InlineIfLambda>] compensation: unit -> unit)
        : EditableStep<ColdPhase, 'Prev, 'U> =
        EditableRunner.TryFinallyStep (body, compensation)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Using
        (resource: 'D, [<InlineIfLambda>] body: 'D -> EditableStep<'P, 'Prev, 'U>)
        : EditableStep<ColdPhase, 'Prev, 'U> when 'D :> IDisposable =
        EditableRunner.UsingStep (resource, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> EditableStep<'P, 'Prev, unit>) : EditableStep<ColdPhase, 'Prev, unit> =
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
    member inline _.Combine(first: BoundaryStep<'P1, unit>, [<InlineIfLambda>] second: unit -> BoundaryStep<'P2, 'U>) : BoundaryStep<ColdPhase, 'U> =
        BoundaryDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                first.EvalValue ()
                (second ()).EvalValue()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.While
        ([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> BoundaryStep<'P, unit>)
        : BoundaryStep<ColdPhase, unit> =
        BoundaryDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                while guard () do
                    (body ()).EvalValue()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryWith
        ([<InlineIfLambda>] body: unit -> BoundaryStep<'P1, 'U>, [<InlineIfLambda>] handler: exn -> BoundaryStep<'P2, 'U>)
        : BoundaryStep<ColdPhase, 'U> =
        BoundaryDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalValue()
                with ex ->
                    (handler ex).EvalValue()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryFinally
        ([<InlineIfLambda>] body: unit -> BoundaryStep<'P, 'U>, [<InlineIfLambda>] compensation: unit -> unit)
        : BoundaryStep<ColdPhase, 'U> =
        BoundaryDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body ()).EvalValue()
                finally
                    compensation ()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Using(resource: 'D, [<InlineIfLambda>] body: 'D -> BoundaryStep<'P, 'U>) : BoundaryStep<ColdPhase, 'U> when 'D :> IDisposable =
        BoundaryDynamic (
            Unchecked.defaultof<Graph>,
            fun () ->
                try
                    (body resource).EvalValue()
                finally
                    if not (isNull (box resource)) then
                        resource.Dispose ()
        )

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For
        (header: BoundaryHeader<'Pend, 'Fail, 'Cmp>, [<InlineIfLambda>] body: unit -> BoundaryStep<'Phase, 'U>)
        : ConfiguredBoundary<'Pend, 'Fail, 'Cmp, 'Phase, 'U> =
        ConfiguredBoundary<'Pend, 'Fail, 'Cmp, 'Phase, 'U>(header, body)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> BoundaryStep<'P, unit>) : BoundaryStep<ColdPhase, unit> =
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
    member inline _.Combine(first: RowStep<'P1, unit>, [<InlineIfLambda>] second: unit -> RowStep<'P2, 'V>) : RowStep<ColdPhase, 'V> =
        RowThunk (fun () ->
            first.EvalValue ()
            (second ()).EvalValue())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.While([<InlineIfLambda>] guard: unit -> bool, [<InlineIfLambda>] body: unit -> RowStep<'P, unit>) : RowStep<ColdPhase, unit> =
        RowThunk (fun () ->
            while guard () do
                (body ()).EvalValue())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryWith
        ([<InlineIfLambda>] body: unit -> RowStep<'P1, 'V>, [<InlineIfLambda>] handler: exn -> RowStep<'P2, 'V>)
        : RowStep<ColdPhase, 'V> =
        RowThunk (fun () ->
            try
                (body ()).EvalValue()
            with ex ->
                (handler ex).EvalValue())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.TryFinally
        ([<InlineIfLambda>] body: unit -> RowStep<'P, 'V>, [<InlineIfLambda>] compensation: unit -> unit)
        : RowStep<ColdPhase, 'V> =
        RowThunk (fun () ->
            try
                (body ()).EvalValue()
            finally
                compensation ())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Using(resource: 'D, [<InlineIfLambda>] body: 'D -> RowStep<'P, 'V>) : RowStep<ColdPhase, 'V> when 'D :> IDisposable =
        RowThunk (fun () ->
            try
                (body resource).EvalValue()
            finally
                if not (isNull (box resource)) then
                    resource.Dispose ())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.For(sequence: seq<'T>, [<InlineIfLambda>] body: 'T -> RowStep<'P, unit>) : RowStep<ColdPhase, unit> =
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
        let v = source ()
        RowReadyVal (CeRuntime.RunUntrackedRowArg (f, v))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: IReactive<'A>, f: 'A -> 'V) : RowStep<BoundPhase, 'V> =
        let v = source.ReadTracked ()
        RowReadyVal (CeRuntime.RunUntrackedRowArg (f, v))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.BindReturn(source: RowInput<'A>, f: 'A -> 'V) : RowStep<BoundPhase, 'V> =
        let v = source.ReadFn ()
        RowReadyVal (CeRuntime.RunUntrackedRowArg (f, v))

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: unit -> 'A, continuation: 'A -> RowStep<'Phase, 'V>) : RowStep<BoundPhase, 'V> =
        let v = source ()
        RowReadyVal ((continuation v).EvalValue())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: IReactive<'A>, continuation: 'A -> RowStep<'Phase, 'V>) : RowStep<BoundPhase, 'V> =
        let v = source.ReadTracked ()
        RowReadyVal ((continuation v).EvalValue())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Bind(source: RowInput<'A>, continuation: 'A -> RowStep<'Phase, 'V>) : RowStep<BoundPhase, 'V> =
        let v = source.ReadFn ()
        RowReadyVal ((continuation v).EvalValue())

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    member inline _.Run(d: unit -> RowStep<ColdPhase, 'V>) : unit -> 'V =
        fun () -> (d ()).EvalValue()

/// <summary>Computation expression builders and combinators explicitly gated behind <c>Ranvier.CE</c>.</summary>
[<AutoOpen>]
module Builders =
    /// <summary>Computation expression builder producing <c>Memo&lt;'U&gt;</c> nodes with closed Phase-2 bodies.</summary>
    let memo = MemoBuilder ()

    /// <summary>Computation expression builder producing <c>Effect</c> / <c>EffectOn</c> nodes with closed action bodies.</summary>
    let effect = EffectBuilder ()

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
        member inline _.Bind(source: IReactive<'K>, continuation: 'K -> EffectStep<BoundPhase>) : EffectStep<BoundPhase> =
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
        member inline _.Run(d: unit -> EffectStep<BoundPhase>) : unit =
            EffectRunner.EvalBound (d, false) |> ignore

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredEffect<UnitOut, BoundPhase>) : unit =
            EffectRunner.EvalBound ((fun () -> (d ()).Body()), false)
            |> ignore

        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> ConfiguredEffect<NodeOut, BoundPhase>) : INode =
            EffectRunner.EvalBound ((fun () -> (d ()).Body()), true)

    type EditableBuilder with
        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> EditableStep<BoundPhase, 'Prev, 'U>) : Editable<'U> =
            EditableRunner.EvalBound (d, false)

    type DraftBuilder with
        [<EditorBrowsable(EditorBrowsableState.Never)>]
        member inline _.Run(d: unit -> EditableStep<BoundPhase, 'Prev, 'U>) : Editable<'U> =
            EditableRunner.EvalBound (d, true)

    type BoundaryBuilder with
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
        member inline _.Run(d: unit -> RowStep<BoundPhase, 'V>) : unit -> 'V =
            fun () -> (d ()).EvalValue()
