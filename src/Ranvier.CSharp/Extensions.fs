namespace Ranvier.CSharp

open System
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks
open Ranvier

/// <summary>Extension methods on graphs and signals.</summary>
[<Extension; AbstractClass; Sealed>]
type GraphExtensions =

    /// <summary>Activates the graph, runs <c>body</c>, and restores the previous ambient graph.</summary>
    /// <remarks>The graph outlives the call, so the effects created inside <c>body</c> keep running.</remarks>
    [<Extension>]
    static member Run<'T>(graph: Graph, body: Func<'T>) : 'T =
        use _ = graph.Activate ()
        body.Invoke ()

    /// <summary>Activates the graph, runs <c>body</c>, and restores the previous ambient graph.</summary>
    /// <remarks>The graph outlives the call, so the effects created inside <c>body</c> keep running.</remarks>
    [<Extension>]
    static member Run(graph: Graph, body: Action) : unit =
        use _ = graph.Activate ()
        body.Invoke ()

    /// <summary>Writes <c>update</c> applied to the signal's current value, read untracked.</summary>
    /// <exception cref="T:System.ArgumentNullException"><c>signal</c> or <c>update</c> is null.</exception>
    [<Extension>]
    static member Update<'T>(signal: Signal<'T>, update: Func<'T, 'T>) : unit =
        if isNull (box signal) then
            nullArg "signal"

        if isNull (box update) then
            nullArg "update"

        signal.Value <- update.Invoke signal.Peek

/// <summary>
/// Awaitable reads of <c>Previous.Settled</c> for C#: the value or a seed, or a <c>(HasValue, Value)</c> pair.
/// </summary>
/// <remarks>
/// Each completes when <c>Settled</c> completes. A completed <c>Settled</c> yields a completed <c>ValueTask</c> without
/// allocating; a pending one allocates one continuation. Read every input, then await, as with <c>Settled</c>.
/// </remarks>
[<Extension; AbstractClass; Sealed>]
type PreviousExtensions =

    /// <summary>The value last published, or <c>seed</c> before the first.</summary>
    [<Extension>]
    static member SettledOr<'T>(previous: Previous<'T>, seed: 'T) : ValueTask<'T> =
        let settled = previous.Settled

        if settled.IsCompletedSuccessfully then
            ValueTask<'T>(ValueOption.defaultValue seed settled.Result)
        else
            ValueTask<'T>(
                task {
                    let! last = settled
                    return ValueOption.defaultValue seed last
                }
            )

    /// <summary>
    /// <c>(true, value)</c> with the value last published, or <c>(false, default)</c> before the first.
    /// </summary>
    [<Extension>]
    static member TrySettled<'T>(previous: Previous<'T>) : [<TupleElementNames([| "HasValue"; "Value" |])>] ValueTask<struct (bool * 'T)> =
        let settled = previous.Settled

        if settled.IsCompletedSuccessfully then
            ValueTask<struct (bool * 'T)>(PreviousExtensions.Unpack settled.Result)
        else
            ValueTask<struct (bool * 'T)>(
                task {
                    let! last = settled
                    return PreviousExtensions.Unpack last
                }
            )

    static member private Unpack(last: 'T voption) : struct (bool * 'T) =
        match last with
        | ValueSome value -> struct (true, value)
        | ValueNone -> struct (false, Unchecked.defaultof<'T>)

/// <summary>
/// Live operators on projections. Each returns a node that updates per changed row rather than recomputing the whole
/// collection.
/// </summary>
/// <remarks>The result belongs to the scope active when the operator is called.</remarks>
[<Extension; AbstractClass; Sealed>]
type ProjectionExtensions =

    /// <summary>The rows whose value satisfies <c>predicate</c>, in upstream order.</summary>
    [<Extension>]
    static member Where<'K, 'V when 'K: equality>(upstream: Projection<'K, 'V>, predicate: Func<'V, bool>) =
        Projection.filter predicate.Invoke upstream

    /// <summary>Each row's value mapped by <c>selector</c>, under the same key.</summary>
    [<Extension>]
    static member Select<'K, 'V, 'U when 'K: equality>(upstream: Projection<'K, 'V>, selector: Func<'V, 'U>) =
        Projection.map selector.Invoke upstream

    /// <summary>The rows ordered by <c>keySelector</c>, ascending and stable.</summary>
    [<Extension>]
    static member OrderBy<'K, 'V, 'S when 'K: equality and 'S: comparison>(upstream: Projection<'K, 'V>, keySelector: Func<'V, 'S>) =
        Projection.sortBy keySelector.Invoke upstream

    /// <summary>The rows grouped by <c>keySelector</c>, one inner projection per group.</summary>
    [<Extension>]
    static member GroupBy<'K, 'V, 'G when 'K: equality and 'G: equality>(upstream: Projection<'K, 'V>, keySelector: Func<'V, 'G>) =
        Projection.groupBy keySelector.Invoke upstream

    /// <summary>The first <c>count</c> rows.</summary>
    [<Extension>]
    static member Take<'K, 'V when 'K: equality>(upstream: Projection<'K, 'V>, count: int) =
        Projection.take (fun () -> count) upstream

    /// <summary>The first <c>count ()</c> rows; the view moves when a value <c>count</c> read changes.</summary>
    [<Extension>]
    static member Take<'K, 'V when 'K: equality>(upstream: Projection<'K, 'V>, count: Func<int>) =
        Projection.take count.Invoke upstream

    /// <summary>The rows after the first <c>count</c>.</summary>
    [<Extension>]
    static member Skip<'K, 'V when 'K: equality>(upstream: Projection<'K, 'V>, count: int) =
        Projection.skip (fun () -> count) upstream

    /// <summary>The rows after the first <c>count ()</c>; the view moves when a value <c>count</c> read changes.</summary>
    [<Extension>]
    static member Skip<'K, 'V when 'K: equality>(upstream: Projection<'K, 'V>, count: Func<int>) =
        Projection.skip count.Invoke upstream

    /// <summary>A window of <c>count ()</c> rows starting at <c>offset ()</c>.</summary>
    [<Extension>]
    static member Slice<'K, 'V when 'K: equality>(upstream: Projection<'K, 'V>, offset: Func<int>, count: Func<int>) =
        Projection.sub offset.Invoke count.Invoke upstream

    /// <summary>A memo of <c>seed</c> folded over every row with <c>folder</c>. A row change re-folds every row.</summary>
    [<Extension>]
    static member Aggregate<'K, 'V, 'S when 'K: equality>(upstream: Projection<'K, 'V>, seed: 'S, folder: Func<'S, 'V, 'S>) =
        Projection.fold (fun state value -> folder.Invoke (state, value)) seed upstream

    /// <summary>
    /// A memo of <c>zero</c> folded over every row with <c>add</c>. A row change subtracts the old value and adds the
    /// new one; a throwing <c>subtract</c> re-folds every row.
    /// </summary>
    [<Extension>]
    static member Aggregate<'K, 'V, 'S when 'K: equality>(upstream: Projection<'K, 'V>, zero: 'S, add: Func<'S, 'V, 'S>, subtract: Func<'S, 'V, 'S>) =
        Projection.foldGroup (fun state value -> add.Invoke (state, value)) (fun state value -> subtract.Invoke (state, value)) zero upstream

    /// <summary>A memo of the sum of <c>selector</c> over every row, kept current per row change.</summary>
    [<Extension>]
    static member Sum<'K, 'V when 'K: equality>(upstream: Projection<'K, 'V>, selector: Func<'V, int>) : Memo<int> =
        Projection.sumBy selector.Invoke upstream

    /// <summary>A memo of the sum of <c>selector</c> over every row, kept current per row change.</summary>
    [<Extension>]
    static member Sum<'K, 'V when 'K: equality>(upstream: Projection<'K, 'V>, selector: Func<'V, int64>) : Memo<int64> =
        Projection.sumBy selector.Invoke upstream

    /// <summary>A memo of the sum of <c>selector</c> over every row, kept current per row change.</summary>
    [<Extension>]
    static member Sum<'K, 'V when 'K: equality>(upstream: Projection<'K, 'V>, selector: Func<'V, decimal>) : Memo<decimal> =
        Projection.sumBy selector.Invoke upstream

    /// <summary>A memo of the sum of <c>selector</c> over every row, kept current per row change.</summary>
    /// <remarks>While the sum is infinite or NaN, each row change re-adds every row.</remarks>
    [<Extension>]
    static member Sum<'K, 'V when 'K: equality>(upstream: Projection<'K, 'V>, selector: Func<'V, float>) : Memo<float> =
        Projection.sumBy selector.Invoke upstream

    /// <summary>A memo of how many rows satisfy <c>predicate</c>.</summary>
    [<Extension>]
    static member Count<'K, 'V when 'K: equality>(upstream: Projection<'K, 'V>, predicate: Func<'V, bool>) : Memo<int> =
        Projection.countBy predicate.Invoke upstream

    /// <summary>A memo of whether any row satisfies <c>predicate</c>.</summary>
    [<Extension>]
    static member Any<'K, 'V when 'K: equality>(upstream: Projection<'K, 'V>, predicate: Func<'V, bool>) : Memo<bool> =
        Projection.exists predicate.Invoke upstream

    /// <summary>A memo of whether every row satisfies <c>predicate</c>.</summary>
    [<Extension>]
    static member All<'K, 'V when 'K: equality>(upstream: Projection<'K, 'V>, predicate: Func<'V, bool>) : Memo<bool> =
        Projection.forall predicate.Invoke upstream

/// <summary>
/// LINQ-compatible reactive combinators (<c>Select</c>, <c>Zip</c>, <c>SelectMany</c>, and <c>SelectManyFold</c>) over
/// signals, memos, async values, and streams.
/// </summary>
/// <remarks>
/// Each operator resolves the source node's graph directly and attaches to the enclosing scope on that graph's thread.
/// Dependency tracking is closed inside mapping and projection callbacks.
/// </remarks>
[<Extension; AbstractClass; Sealed>]
type RanvierExtensions =

    /// <summary>A pure memo computing <c>selector(source.Value)</c> with dependency tracking closed inside <c>selector</c>.</summary>
    /// <exception cref="T:System.ArgumentNullException"><c>source</c> or <c>selector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called inside a pure body or off the graph's thread.</exception>
    [<Extension>]
    static member Select<'T, 'U>(source: Signal<'T>, selector: Func<'T, 'U>) : Memo<'U> =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull selector then
            nullArg (nameof selector)

        Signal.map selector.Invoke source

    /// <summary>
    /// A pure memo computing <c>selector(source.Value)</c> once <c>source</c> is ready, with dependency tracking closed
    /// inside <c>selector</c>.
    /// </summary>
    /// <remarks>Unobserved linear <c>Select</c> chains sharing an <c>Owner</c> fuse in untraced unjournaled builds.</remarks>
    /// <exception cref="T:System.ArgumentNullException"><c>source</c> or <c>selector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called inside a pure body or off the graph's thread.</exception>
    [<Extension>]
    static member Select<'T, 'U>(source: Memo<'T>, selector: Func<'T, 'U>) : Memo<'U> =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull selector then
            nullArg (nameof selector)

        Memo.map selector.Invoke source

    /// <summary>
    /// A pure memo computing <c>selector(source.Value)</c> once <c>source</c> settles, with dependency tracking closed
    /// inside <c>selector</c>.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>source</c> or <c>selector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called inside a pure body or off the graph's thread.</exception>
    [<Extension>]
    static member Select<'T, 'U>(source: AsyncMemo<'T>, selector: Func<'T, 'U>) : Memo<'U> =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull selector then
            nullArg (nameof selector)

        AsyncMemo.map selector.Invoke source

    /// <summary>
    /// A pure memo computing <c>selector(source.Value)</c> once <c>source</c> settles, with dependency tracking closed
    /// inside <c>selector</c>.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>source</c> or <c>selector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called inside a pure body or off the graph's thread.</exception>
    [<Extension>]
    static member Select<'T, 'U>(source: AsyncSource<'T>, selector: Func<'T, 'U>) : Memo<'U> =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull selector then
            nullArg (nameof selector)

        AsyncSource.map selector.Invoke source

    /// <summary>
    /// A pure memo computing <c>selector(source.Value)</c> once <c>source</c> publishes, with dependency tracking
    /// closed inside <c>selector</c>.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>source</c> or <c>selector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called inside a pure body or off the graph's thread.</exception>
    [<Extension>]
    static member Select<'T, 'U>(source: StreamMemo<'T>, selector: Func<'T, 'U>) : Memo<'U> =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull selector then
            nullArg (nameof selector)

        StreamMemo.map selector.Invoke source

    /// <summary>A pure memo pairing <c>first.Value</c> and <c>second.Value</c> as a <c>ValueTuple</c>.</summary>
    /// <exception cref="T:System.ArgumentNullException"><c>first</c> or <c>second</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    /// <c>first</c> and <c>second</c> belong to different graphs, or called inside a pure body.
    /// </exception>
    [<Extension>]
    static member Zip<'A, 'B>(first: Signal<'A>, second: Signal<'B>) : Memo<struct ('A * 'B)> =
        if isNull (box first) then
            nullArg (nameof first)

        if isNull (box second) then
            nullArg (nameof second)

        Signal.map2 (fun a b -> struct (a, b)) first second

    /// <summary>
    /// A pure memo computing <c>resultSelector(first.Value, second.Value)</c> with dependency tracking closed inside
    /// <c>resultSelector</c>.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>first</c>, <c>second</c>, or <c>resultSelector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    /// <c>first</c> and <c>second</c> belong to different graphs, or called inside a pure body.
    /// </exception>
    [<Extension>]
    static member Zip<'A, 'B, 'C>(first: Signal<'A>, second: Signal<'B>, resultSelector: Func<'A, 'B, 'C>) : Memo<'C> =
        if isNull (box first) then
            nullArg (nameof first)

        if isNull (box second) then
            nullArg (nameof second)

        if isNull resultSelector then
            nullArg (nameof resultSelector)

        Signal.map2 (fun a b -> resultSelector.Invoke (a, b)) first second

    /// <summary>
    /// A pure memo pairing <c>first.Value</c> and <c>second.Value</c> as a <c>ValueTuple</c>, with left-to-right
    /// pending and failure precedence.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>first</c> or <c>second</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    /// <c>first</c> and <c>second</c> belong to different graphs, or called inside a pure body.
    /// </exception>
    [<Extension>]
    static member Zip<'A, 'B>(first: Memo<'A>, second: Memo<'B>) : Memo<struct ('A * 'B)> =
        if isNull (box first) then
            nullArg (nameof first)

        if isNull (box second) then
            nullArg (nameof second)

        Memo.map2 (fun a b -> struct (a, b)) first second

    /// <summary>
    /// A pure memo computing <c>resultSelector(first.Value, second.Value)</c> with left-to-right pending and failure
    /// precedence and tracking closed inside <c>resultSelector</c>.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>first</c>, <c>second</c>, or <c>resultSelector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    /// <c>first</c> and <c>second</c> belong to different graphs, or called inside a pure body.
    /// </exception>
    [<Extension>]
    static member Zip<'A, 'B, 'C>(first: Memo<'A>, second: Memo<'B>, resultSelector: Func<'A, 'B, 'C>) : Memo<'C> =
        if isNull (box first) then
            nullArg (nameof first)

        if isNull (box second) then
            nullArg (nameof second)

        if isNull resultSelector then
            nullArg (nameof resultSelector)

        Memo.map2 (fun a b -> resultSelector.Invoke (a, b)) first second

    /// <summary>
    /// A pure memo pairing <c>first.Value</c> and <c>second.Value</c> as a <c>ValueTuple</c>, with left-to-right
    /// pending and failure precedence.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>first</c> or <c>second</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    /// <c>first</c> and <c>second</c> belong to different graphs, or called inside a pure body.
    /// </exception>
    [<Extension>]
    static member Zip<'A, 'B>(first: AsyncMemo<'A>, second: AsyncMemo<'B>) : Memo<struct ('A * 'B)> =
        if isNull (box first) then
            nullArg (nameof first)

        if isNull (box second) then
            nullArg (nameof second)

        AsyncMemo.map2 (fun a b -> struct (a, b)) first second

    /// <summary>
    /// A pure memo computing <c>resultSelector(first.Value, second.Value)</c> with left-to-right pending and failure
    /// precedence and tracking closed inside <c>resultSelector</c>.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>first</c>, <c>second</c>, or <c>resultSelector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    /// <c>first</c> and <c>second</c> belong to different graphs, or called inside a pure body.
    /// </exception>
    [<Extension>]
    static member Zip<'A, 'B, 'C>(first: AsyncMemo<'A>, second: AsyncMemo<'B>, resultSelector: Func<'A, 'B, 'C>) : Memo<'C> =
        if isNull (box first) then
            nullArg (nameof first)

        if isNull (box second) then
            nullArg (nameof second)

        if isNull resultSelector then
            nullArg (nameof resultSelector)

        AsyncMemo.map2 (fun a b -> resultSelector.Invoke (a, b)) first second

    /// <summary>
    /// A pure memo pairing <c>first.Value</c> and <c>second.Value</c> as a <c>ValueTuple</c>, with left-to-right
    /// pending and failure precedence.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>first</c> or <c>second</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    /// <c>first</c> and <c>second</c> belong to different graphs, or called inside a pure body.
    /// </exception>
    [<Extension>]
    static member Zip<'A, 'B>(first: AsyncSource<'A>, second: AsyncSource<'B>) : Memo<struct ('A * 'B)> =
        if isNull (box first) then
            nullArg (nameof first)

        if isNull (box second) then
            nullArg (nameof second)

        AsyncSource.map2 (fun a b -> struct (a, b)) first second

    /// <summary>
    /// A pure memo computing <c>resultSelector(first.Value, second.Value)</c> with left-to-right pending and failure
    /// precedence and tracking closed inside <c>resultSelector</c>.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>first</c>, <c>second</c>, or <c>resultSelector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    /// <c>first</c> and <c>second</c> belong to different graphs, or called inside a pure body.
    /// </exception>
    [<Extension>]
    static member Zip<'A, 'B, 'C>(first: AsyncSource<'A>, second: AsyncSource<'B>, resultSelector: Func<'A, 'B, 'C>) : Memo<'C> =
        if isNull (box first) then
            nullArg (nameof first)

        if isNull (box second) then
            nullArg (nameof second)

        if isNull resultSelector then
            nullArg (nameof resultSelector)

        AsyncSource.map2 (fun a b -> resultSelector.Invoke (a, b)) first second

    /// <summary>
    /// A pure memo pairing <c>first.Value</c> and <c>second.Value</c> as a <c>ValueTuple</c>, with left-to-right
    /// pending and failure precedence.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>first</c> or <c>second</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    /// <c>first</c> and <c>second</c> belong to different graphs, or called inside a pure body.
    /// </exception>
    [<Extension>]
    static member Zip<'A, 'B>(first: StreamMemo<'A>, second: StreamMemo<'B>) : Memo<struct ('A * 'B)> =
        if isNull (box first) then
            nullArg (nameof first)

        if isNull (box second) then
            nullArg (nameof second)

        StreamMemo.map2 (fun a b -> struct (a, b)) first second

    /// <summary>
    /// A pure memo computing <c>resultSelector(first.Value, second.Value)</c> with left-to-right pending and failure
    /// precedence and tracking closed inside <c>resultSelector</c>.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>first</c>, <c>second</c>, or <c>resultSelector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    /// <c>first</c> and <c>second</c> belong to different graphs, or called inside a pure body.
    /// </exception>
    [<Extension>]
    static member Zip<'A, 'B, 'C>(first: StreamMemo<'A>, second: StreamMemo<'B>, resultSelector: Func<'A, 'B, 'C>) : Memo<'C> =
        if isNull (box first) then
            nullArg (nameof first)

        if isNull (box second) then
            nullArg (nameof second)

        if isNull resultSelector then
            nullArg (nameof resultSelector)

        StreamMemo.map2 (fun a b -> resultSelector.Invoke (a, b)) first second

    /// <summary>
    /// Switches dynamically onto the inner memo returned by <c>selector(source.Value)</c> whenever <c>source</c>
    /// changes to an unequal value.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>source</c> or <c>selector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called inside a pure body or off the graph's thread.</exception>
    [<Extension>]
    static member SelectMany<'T, 'U>(source: Signal<'T>, selector: Func<'T, Memo<'U>>) : Memo<'U> =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull selector then
            nullArg (nameof selector)

        Signal.bind selector.Invoke source

    /// <summary>
    /// Projects <c>source</c> through <c>collectionSelector</c> onto an inner memo and combines both values with
    /// <c>resultSelector</c>.
    /// </summary>
    /// <remarks>
    /// Inner memo ticks re-run <c>resultSelector</c> with output cutoff while preserving the outer
    /// <c>collectionSelector</c> scope.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">
    /// <c>source</c>, <c>collectionSelector</c>, or <c>resultSelector</c> is null.
    /// </exception>
    /// <exception cref="T:System.InvalidOperationException">Called inside a pure body or off the graph's thread.</exception>
    [<Extension>]
    static member SelectMany<'T, 'U, 'R>(source: Signal<'T>, collectionSelector: Func<'T, Memo<'U>>, resultSelector: Func<'T, 'U, 'R>) : Memo<'R> =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull collectionSelector then
            nullArg (nameof collectionSelector)

        if isNull resultSelector then
            nullArg (nameof resultSelector)

        Signal.bind
            (fun x ->
                let inner = collectionSelector.Invoke x

                if isNull (box inner) then
                    Unchecked.defaultof<Memo<'R>>
                else
                    CombinatorMemos.ensureSameSignalMemoGraph source inner
                    Memo.map (fun y -> resultSelector.Invoke (x, y)) inner)
            source

    /// <summary>
    /// Switches dynamically onto the inner memo returned by <c>selector(source.Value)</c> whenever <c>source</c>
    /// settles on an unequal value.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>source</c> or <c>selector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called inside a pure body or off the graph's thread.</exception>
    [<Extension>]
    static member SelectMany<'T, 'U>(source: Memo<'T>, selector: Func<'T, Memo<'U>>) : Memo<'U> =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull selector then
            nullArg (nameof selector)

        Memo.bind selector.Invoke source

    /// <summary>
    /// Projects <c>source</c> through <c>collectionSelector</c> onto an inner memo and combines both values with
    /// <c>resultSelector</c>.
    /// </summary>
    /// <remarks>
    /// Inner memo ticks re-run <c>resultSelector</c> with output cutoff while preserving the outer
    /// <c>collectionSelector</c> scope.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">
    /// <c>source</c>, <c>collectionSelector</c>, or <c>resultSelector</c> is null.
    /// </exception>
    /// <exception cref="T:System.InvalidOperationException">Called inside a pure body or off the graph's thread.</exception>
    [<Extension>]
    static member SelectMany<'T, 'U, 'R>(source: Memo<'T>, collectionSelector: Func<'T, Memo<'U>>, resultSelector: Func<'T, 'U, 'R>) : Memo<'R> =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull collectionSelector then
            nullArg (nameof collectionSelector)

        if isNull resultSelector then
            nullArg (nameof resultSelector)

        Memo.bind
            (fun x ->
                let inner = collectionSelector.Invoke x

                if isNull (box inner) then
                    Unchecked.defaultof<Memo<'R>>
                else
                    CombinatorMemos.ensureSameMemoGraph source inner
                    Memo.map (fun y -> resultSelector.Invoke (x, y)) inner)
            source

    /// <summary>
    /// Starts an untracked flight <c>selector(x, token)</c> whenever <c>source</c> settles on an unequal value
    /// <c>x</c>, using the graph's flight policy.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>source</c> or <c>selector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called inside a pure body or off the graph's thread.</exception>
    [<Extension>]
    static member SelectMany<'T, 'U>(source: AsyncMemo<'T>, selector: Func<'T, CancellationToken, Task<'U>>) : AsyncMemo<'U> =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull selector then
            nullArg (nameof selector)

        AsyncMemo.bind (fun value token -> selector.Invoke (value, token)) source

    /// <summary>
    /// Starts an untracked flight <c>selector(x, token)</c> whenever <c>source</c> settles on an unequal value
    /// <c>x</c>, managing overlapping flights under <c>policy</c>.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>source</c> or <c>selector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called inside a pure body or off the graph's thread.</exception>
    [<Extension>]
    static member SelectMany<'T, 'U>(source: AsyncMemo<'T>, selector: Func<'T, CancellationToken, Task<'U>>, policy: FlightPolicy) : AsyncMemo<'U> =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull selector then
            nullArg (nameof selector)

        AsyncMemo.bindWithPolicy policy (fun value token -> selector.Invoke (value, token)) source

    /// <summary>
    /// Starts an untracked flight <c>selector(x, token)</c> whenever <c>source</c> settles on an unequal value
    /// <c>x</c>, using the graph's flight policy.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>source</c> or <c>selector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called inside a pure body or off the graph's thread.</exception>
    [<Extension>]
    static member SelectMany<'T, 'U>(source: AsyncSource<'T>, selector: Func<'T, CancellationToken, Task<'U>>) : AsyncMemo<'U> =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull selector then
            nullArg (nameof selector)

        AsyncSource.bind (fun value token -> selector.Invoke (value, token)) source

    /// <summary>
    /// Connects to the <c>StreamSource</c> returned by <c>selector(k, token)</c> whenever <c>source</c> publishes an
    /// unequal key <c>k</c>.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>source</c> or <c>selector</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called inside a pure body or off the graph's thread.</exception>
    [<Extension>]
    static member SelectMany<'K, 'T>(source: StreamMemo<'K>, selector: Func<'K, CancellationToken, StreamSource<'T>>) : StreamMemo<'T> =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull selector then
            nullArg (nameof selector)

        StreamMemo.bind (fun key token -> selector.Invoke (key, token)) source

    /// <summary>
    /// Folds items from <c>start(k, token)</c> into <c>seed</c>, restarting from <c>seed</c> whenever <c>source</c>
    /// publishes an unequal key <c>k</c>.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>source</c>, <c>fold</c>, or <c>start</c> is null.</exception>
    /// <exception cref="T:System.InvalidOperationException">Called inside a pure body or off the graph's thread.</exception>
    [<Extension>]
    static member SelectManyFold<'K, 'S, 'T>
        (source: StreamMemo<'K>, seed: 'S, fold: Func<'S, 'T, 'S>, start: Func<'K, CancellationToken, StreamSource<'T>>)
        : StreamMemo<'S> =
        if isNull (box source) then
            nullArg (nameof source)

        if isNull fold then
            nullArg (nameof fold)

        if isNull start then
            nullArg (nameof start)

        StreamMemo.bindFold seed (fun state item -> fold.Invoke (state, item)) (fun key token -> start.Invoke (key, token)) source
