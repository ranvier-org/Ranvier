namespace Ranvier

open System
open System.Collections.Generic

/// <summary>One key of a collection: an immutable key and value signal, linked in insertion order.</summary>
/// <remarks>A mutable reference record. A missing neighbour is the null reference, tested through <c>Platform.isNull (box _)</c>.</remarks>
[<NoEquality; NoComparison>]
type internal CollectionItem<'K, 'V> =
    {
        Key: 'K
        Signal: Signal<'V>
        mutable Prev: CollectionItem<'K, 'V>
        mutable Next: CollectionItem<'K, 'V>
        mutable Live: bool
    }

/// <summary>The rows and membership journal of an editable collection.</summary>
type internal CollectionProjection<'K, 'V when 'K: equality> private (graph: Graph) =
    inherit RowsOf<Signal<'V>, 'K, 'V>(graph, (fun item -> item.Value), Unchecked.defaultof<_>)

    let empty = KeyChanges.empty<'K>()
    let items = Platform.KeyMap<'K, CollectionItem<'K, 'V>>()
    let mutable head: CollectionItem<'K, 'V> = Unchecked.defaultof<_>
    let mutable tail: CollectionItem<'K, 'V> = Unchecked.defaultof<_>
    let mutable count = 0

    /// <summary>The item of the last key upserted, or null. Cleared by every removal.</summary>
    let mutable lastItem: CollectionItem<'K, 'V> = Unchecked.defaultof<_>

    let revision = Signal<int>.OfEncoded(graph, 0, Unchecked.defaultof<_>)
    let mutable changes = KeyChanges<'K>()
    let mutable previous: 'K[] = Array.empty
    let mutable first = true
    let mutable stopped = false

    // The batch bodies, built once by Init and bound to this collection, so an edit allocates no closure. A let-bound
    // lambda would compile to a method and a fresh closure at each use; a mutable field assigned once holds one object.
    let mutable clearFn: unit -> unit = Unchecked.defaultof<_>
    let mutable removeFn: 'K -> bool = Unchecked.defaultof<_>

    let toArray () =
        if count = 0 then
            Array.empty
        else
            let arr = Array.zeroCreate<'K> count
            let mutable node = head
            let mutable i = 0

            while not (Platform.isNull (box node)) do
                arr[i] <- node.Key
                i <- i + 1
                node <- node.Next

            arr

    /// <summary>Wires the delta readers and the cleanup. Called once by <c>Create</c>, before the projection is shared.</summary>
    member private this.Init() =
        // A key's item signal is immutable: a replaced or removed key retires its row, and a re-added key gets a new row.
#if FABLE_COMPILER && !RANVIER_TRACE
        this.DirectSignalRow <- true
#else
        this.DirectBody <- Func<_, _, _>(fun entry signal -> this.RowBody (entry, signal))
#endif
        clearFn <- fun () -> this.ClearInBatch ()
        removeFn <- fun key -> this.RemoveInBatch key

        this.ReadDelta <-
            fun () ->
                revision.Value |> ignore

                if first then
                    let current = toArray ()
                    changes.Clear ()
                    let delta = ProjectionDelta (empty, current, previous, true)
                    previous <- current
                    first <- false
                    delta
                elif changes.Size = 0 then
                    ProjectionDelta (empty, previous, previous, false)
                else
                    let current = toArray ()
                    let recorded = changes
                    changes <- KeyChanges<'K>()
                    let delta = ProjectionDelta (recorded, current, previous, false)
                    previous <- current
                    delta

        this.VisitDelta <-
            fun key ->
                let item = items.Find key

                if not (Platform.isNull (box item)) then
                    this.Visit (key, item.Signal)

        graph.RunOwned (
            this.Scope,
            fun () ->
                this.Scope.OnCleanup (fun () ->
                    stopped <- true
                    items.Clear ()
                    lastItem <- Unchecked.defaultof<_>
                    head <- Unchecked.defaultof<_>
                    tail <- Unchecked.defaultof<_>
                    count <- 0
                    changes.Clear ()
                    previous <- Array.empty)
        )

#if !FABLE_COMPILER || RANVIER_TRACE
    /// <summary>The memo body of the row of <c>signal</c>: a tracked read of the signal, which settles the row.</summary>
    member private _.RowBody(entry: RowEntry<'K, 'V>, signal: Signal<'V>) : 'V voption -> 'V =
        fun _ ->
            let value = signal.Value
            entry.Settled <- true
            value
#endif

    /// <summary>Creates the projection and wires it. A collection is always made here, never by the constructor.</summary>
    static member Create(graph: Graph) : CollectionProjection<'K, 'V> =
        let created = new CollectionProjection<'K, 'V> (graph)
        created.Init ()
        created

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.RaiseDisposed() =
        raise (ObjectDisposedException "The keyed collection was disposed.")

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.EnsureLive() =
        if stopped then
            this.RaiseDisposed ()

    /// <summary>Raises when the collection is disposed. Run by an edit body inside its batch.</summary>
    member internal this.CheckLive() =
        this.EnsureLive ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.AddOrUpdateCore(key: 'K, value: 'V) =
        let last = lastItem

        if
            not (Platform.isNull (box last))
            && last.Live
            && Platform.keyEquals last.Key key
            && not stopped
            && last.Live
        then
            // The batch is entered, so the write skips the token and flush checks of a public write.
            last.Signal.SetFromEnteredBatch value
        else
            this.AddOrUpdateFound (key, value)

    /// <summary>The upsert of a key other than the last one upserted.</summary>
    member private this.AddOrUpdateFound(key: 'K, value: 'V) =
        this.EnsureLive ()
        let item = items.Find key
        this.EnsureLive ()

        if Platform.isNull (box item) then
            let fresh =
                {
                    Key = key
                    Signal = Signal<'V>(graph, value)
                    Prev = Unchecked.defaultof<_>
                    Next = Unchecked.defaultof<_>
                    Live = true
                }

            if Platform.isNull (box tail) then
                head <- fresh
                tail <- fresh
            else
                fresh.Prev <- tail
                tail.Next <- fresh
                tail <- fresh

            items.Set (key, fresh)
            lastItem <- fresh
            count <- count + 1
            changes.Record (key, KeyChange.Added)
            revision.Value <- revision.Peek + 1
        elif item.Live then
            lastItem <- item
            item.Signal.SetFromEnteredBatch value
        else
            this.AddOrUpdateFound (key, value)

    member internal this.AddOrUpdateByInBatch(keyOf: 'V -> 'K, value: 'V) =
        this.EnsureLive ()
        let key = keyOf value
        // The selector may have disposed the collection, which cleared the items; an upsert now would insert a zombie.
        this.EnsureLive ()
        this.AddOrUpdateCore (key, value)

    member internal this.RemoveInBatch(key: 'K) : bool =
        this.EnsureLive ()
        let item = items.Find key
        this.EnsureLive ()

        if Platform.isNull (box item) || not item.Live then
            false
        else
            item.Live <- false
            items.Remove key
            lastItem <- Unchecked.defaultof<_>
            let prev = item.Prev
            let next = item.Next

            if Platform.isNull (box prev) then
                head <- next
            else
                prev.Next <- next

            if Platform.isNull (box next) then
                tail <- prev
            else
                next.Prev <- prev

            item.Prev <- Unchecked.defaultof<_>
            item.Next <- Unchecked.defaultof<_>
            count <- count - 1
            changes.Record (key, KeyChange.Removed)
            revision.Value <- revision.Peek + 1
            true

    member internal this.ClearInBatch() =
        this.EnsureLive ()

        if count > 0 then
            let mutable node = head

            while not (Platform.isNull (box node)) do
                let next = node.Next
                node.Live <- false
                node.Prev <- Unchecked.defaultof<_>
                node.Next <- Unchecked.defaultof<_>
                changes.Record (node.Key, KeyChange.Removed)
                node <- next

            items.Clear ()
            lastItem <- Unchecked.defaultof<_>
            head <- Unchecked.defaultof<_>
            tail <- Unchecked.defaultof<_>
            count <- 0
            revision.Value <- revision.Peek + 1

    /// <summary>The upsert of a value under <c>keyOf value</c>, as a batch body for <c>EditWith</c>.</summary>
    member this.UpsertBody(keyOf: 'V -> 'K) : 'V -> unit =
        fun value -> this.AddOrUpdateByInBatch (keyOf, value)

    member this.Remove(key: 'K) : bool =
        graph.RunBatchWith (removeFn, key)

    member this.Clear() =
        graph.RunBatchUnitWith (clearFn, ())

    /// <summary>Runs <c>body arg</c> in one batch. The body checks liveness itself, through <c>CheckLive</c>.</summary>
    member this.EditWith(body: 'A -> unit, arg: 'A) =
        graph.RunBatchUnitWith (body, arg)

    override this.Enumerate() =
        revision.Value |> ignore
        let mine = this.Runs

        if this.Entries.Count = 0 then
            let mutable node = head

            while this.Runs = mine
                  && not (Platform.isNull (box node)) do
                this.Visit (node.Key, node.Signal)
                node <- node.Next
        else
            let keys = toArray ()
            let mutable i = 0

            while this.Runs = mine && i < keys.Length do
                let key = Platform.itemAt i keys
                i <- i + 1
                let item = items.Find key

                if not (Platform.isNull (box item)) then
                    let entry = this.Entries.Find key

                    if
                        not (Platform.isNull entry)
                        && not (obj.ReferenceEquals ((entry :?> ItemRow<Signal<'V>, 'K, 'V>).Item.Peek, item.Signal))
                    then
                        this.RetireChanged key

                    if this.Runs = mine then
                        this.Visit (key, item.Signal)

/// <summary>An editable collection with insertion order and separately observable rows.</summary>
/// <remarks>Owned by the creating scope. Updates use the graph's value equality policy; keys use structural equality.</remarks>
type KeyedCollection<'K, 'V when 'K: equality> internal (graph: Graph, keyOf: 'V -> 'K) =
    let rows = CollectionProjection<'K, 'V>.Create graph

    // One function object for every upsert.
    let upsert = rows.UpsertBody keyOf

#if FABLE_COMPILER
    /// <summary>
    /// Runs an edit callback inside the batch, after the liveness check. One object per collection, built by the first
    /// <c>Edit</c>: under Fable a struct tuple of the collection and the callback is an array.
    /// </summary>
    let mutable editBody: Action<KeyedCollection<'K, 'V>> -> unit =
        Unchecked.defaultof<_>
#else
    /// <summary>Runs the edit callback inside the batch, after the liveness check. One object per instantiation.</summary>
    static member val private EditBody: struct (KeyedCollection<'K, 'V> * Action<KeyedCollection<'K, 'V>>) -> unit =
        (fun (struct (self, edit)) ->
            self.CheckLive ()
            edit.Invoke self) with get
#endif

    /// <summary>The reactive view used by collection operators and readers.</summary>
    member _.Rows = rows :> Projection<'K, 'V>

    member internal _.CheckLive() =
        rows.CheckLive ()

    /// <summary>Adds a new key at the end, or updates its existing row without changing its position.</summary>
    member _.AddOrUpdate(value: 'V) =
        rows.EditWith (upsert, value)

    /// <summary>Removes the key. Returns false when it is absent.</summary>
    member _.Remove(key: 'K) =
        rows.Remove key

    /// <summary>Removes every key.</summary>
    member _.Clear() =
        rows.Clear ()

    /// <summary>Batches the synchronous edits made by the callback.</summary>
    /// <remarks>Applied edits survive a throwing callback. This operation does not roll back or span asynchronous work.</remarks>
    member this.Edit(edit: Action<KeyedCollection<'K, 'V>>) =
        if Platform.isNull edit then
            nullArg "edit"

#if FABLE_COMPILER
        if Platform.isNull (box editBody) then
            editBody <-
                fun edit ->
                    rows.CheckLive ()
                    edit.Invoke this

        rows.EditWith (editBody, edit)
#else
        rows.EditWith (KeyedCollection<'K, 'V>.EditBody, struct (this, edit))
#endif

    /// <summary>Disposes the rows and resets existing readers to empty. Idempotent.</summary>
    member _.Dispose() =
        rows.Dispose ()

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()
