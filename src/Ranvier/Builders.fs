namespace Ranvier.Internal

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open Ranvier

/// <summary>Phantom phase marker indicating a computation-expression step whose body has not executed yet.</summary>
[<Struct; NoEquality; NoComparison; System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
type ColdPhase = struct end

/// <summary>Phantom phase marker indicating a computation-expression step rooted in a reactive bind.</summary>
[<Struct; NoEquality; NoComparison; System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
type BoundPhase = struct end

#if FABLE_COMPILER
module private CeEvalSlot =
    let mutable evalDepth = 0
#endif

/// <summary>Tracks reentrant reactive computation-expression evaluation depth on the current thread.</summary>
[<AbstractClass; Sealed; System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
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
#else
    [<ThreadStatic; DefaultValue>]
    static val mutable private evalDepth: int

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
#endif

/// <summary>Pairs a reactive source with an explicit Phase-1 key equality comparer.</summary>
#if FABLE_COMPILER
[<Sealed; NoEquality; NoComparison; System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
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
[<Struct; NoEquality; NoComparison; System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
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
[<Sealed; NoEquality; NoComparison; System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
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
[<Struct; NoEquality; NoComparison; System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
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
[<Sealed; NoEquality; NoComparison; System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
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
[<Struct; NoEquality; NoComparison; System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
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
[<Sealed; NoEquality; NoComparison; System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
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
[<Struct; NoEquality; NoComparison; System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
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

namespace Ranvier.CE

open System.Collections.Generic
open Ranvier
open Ranvier.Internal

/// <summary>Computation expression builders and combinators explicitly gated behind <c>Ranvier.CE</c>.</summary>
[<AutoOpen>]
module Builders =
    /// <summary>
    /// Wraps <c>source</c> so Phase-1 key comparison in <c>effect</c>, <c>memo</c>, <c>asyncMemo</c>, or <c>stream</c>
    /// compares keys under <c>comparer</c> instead of the graph's equality policy.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>comparer</c> or <c>source</c> is null.</exception>
    let inline withComparer (comparer: IEqualityComparer<'K>) (source: IReactive<'K>) : ComparedReactive<'K> =
        if isNull (box comparer) then
            nullArg (nameof comparer)

        ComparedReactive<'K>(source, comparer)
