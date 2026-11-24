namespace Ranvier

open System
open System.Collections.Generic

/// <summary>One key of a collection: an immutable key and value signal, linked in insertion order.</summary>
/// <remarks>A mutable reference record. A missing neighbour is the null reference, tested through <c>Platform.isNull (box _)</c>.</remarks>
[<NoEquality; NoComparison>]
type internal CollectionItem<'K, 'V> =
    { Key: 'K
      Signal: Signal<'V>
      mutable Prev: CollectionItem<'K, 'V>
      mutable Next: CollectionItem<'K, 'V> }

/// <summary>The rows and membership journal of an editable collection.</summary>
type internal CollectionProjection<'K, 'V when 'K: equality> private (graph: Graph) =
    inherit RowsOf<Signal<'V>, 'K, 'V>(graph, (fun item -> item.Value), Unchecked.defaultof<_>)

    let empty = KeyChanges.empty<'K> ()
    let items = Platform.KeyMap<'K, CollectionItem<'K, 'V>>()
    let mutable head: CollectionItem<'K, 'V> = Unchecked.defaultof<_>
    let mutable tail: CollectionItem<'K, 'V> = Unchecked.defaultof<_>
    let mutable count = 0
    let revision = Signal<int>.OfEncoded(graph, 0, Unchecked.defaultof<_>)
    let mutable changes = KeyChanges<'K>()
    let mutable previous: 'K[] = Array.empty
    let mutable first = true
    let mutable stopped = false

    // The batch bodies, built once by Init and bound to this collection. They take their arguments as a struct, so an
    // edit allocates no closure. A let-bound lambda would compile to a method and a fresh closure at each use; a mutable
    // field assigned once holds one object.
    let mutable addOrUpdateFn: struct ('K * 'V) -> unit = Unchecked.defaultof<_>
    let mutable addOrUpdateByFn: struct (('V -> 'K) * 'V) -> unit = Unchecked.defaultof<_>
    let mutable removeFn: 'K -> bool = Unchecked.defaultof<_>
    let mutable clearFn: unit -> unit = Unchecked.defaultof<_>

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
        this.DirectItem <- true
        addOrUpdateFn <- fun (struct (key, value)) -> this.AddOrUpdateInBatch (key, value)
        addOrUpdateByFn <- fun (struct (keyOf, value)) -> this.AddOrUpdateByInBatch (keyOf, value)
        removeFn <- fun key -> this.RemoveInBatch key
        clearFn <- fun () -> this.ClearInBatch ()

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
                    head <- Unchecked.defaultof<_>
                    tail <- Unchecked.defaultof<_>
                    count <- 0
                    changes.Clear ()
                    previous <- Array.empty)
        )

    /// <summary>Creates the projection and wires it. A collection is always made here, never by the constructor.</summary>
    static member Create(graph: Graph) : CollectionProjection<'K, 'V> =
        let created = new CollectionProjection<'K, 'V>(graph)
        created.Init ()
        created

    member private _.EnsureLive() =
        if stopped then
            raise (ObjectDisposedException "The keyed collection was disposed.")

    /// <summary>Raises when the collection is disposed. Run by an edit body inside its batch.</summary>
    member internal this.CheckLive() = this.EnsureLive ()

    member private _.AddOrUpdateCore(key: 'K, value: 'V) =
        let item = items.Find key

        if Platform.isNull (box item) then
            let fresh =
                { Key = key
                  Signal = Signal<'V>(graph, value)
                  Prev = Unchecked.defaultof<_>
                  Next = Unchecked.defaultof<_> }

            if Platform.isNull (box tail) then
                head <- fresh
                tail <- fresh
            else
                fresh.Prev <- tail
                tail.Next <- fresh
                tail <- fresh

            items.Set (key, fresh)
            count <- count + 1
            changes.Record (key, KeyChange.Added)
            revision.Value <- revision.Peek + 1
        else
            // The batch is entered, so the write skips the token and flush checks of a public write.
            item.Signal.SetFromEnteredBatch value

    member internal this.AddOrUpdateInBatch(key: 'K, value: 'V) =
        this.EnsureLive ()
        this.AddOrUpdateCore (key, value)

    member internal this.AddOrUpdateByInBatch(keyOf: 'V -> 'K, value: 'V) =
        this.EnsureLive ()
        let key = keyOf value
        // The selector may have disposed the collection, which cleared the items; an upsert now would insert a zombie.
        this.EnsureLive ()
        this.AddOrUpdateCore (key, value)

    member internal this.RemoveInBatch(key: 'K) : bool =
        this.EnsureLive ()
        let item = items.Find key

        if Platform.isNull (box item) then
            false
        else
            items.Remove key
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
                node.Prev <- Unchecked.defaultof<_>
                node.Next <- Unchecked.defaultof<_>
                changes.Record (node.Key, KeyChange.Removed)
                node <- next

            items.Clear ()
            head <- Unchecked.defaultof<_>
            tail <- Unchecked.defaultof<_>
            count <- 0
            revision.Value <- revision.Peek + 1

    member this.AddOrUpdate(key: 'K, value: 'V) =
        graph.RunBatchUnitWith (addOrUpdateFn, struct (key, value))

    member this.AddOrUpdateBy(keyOf: 'V -> 'K, value: 'V) =
        graph.RunBatchUnitWith (addOrUpdateByFn, struct (keyOf, value))

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

            while this.Runs = mine && not (Platform.isNull (box node)) do
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

    /// <summary>Runs the edit callback inside the batch, after the liveness check. One object per instantiation.</summary>
    static member val private EditBody: struct (KeyedCollection<'K, 'V> * Action<KeyedCollection<'K, 'V>>) -> unit =
        (fun (struct (self, edit)) ->
            self.CheckLive ()
            edit.Invoke self) with get

    /// <summary>The reactive view used by collection operators and readers.</summary>
    member _.Rows = rows :> Projection<'K, 'V>

    member internal _.CheckLive() = rows.CheckLive ()

    /// <summary>Adds a new key at the end, or updates its existing row without changing its position.</summary>
    member _.AddOrUpdate(value: 'V) =
        rows.AddOrUpdateBy (keyOf, value)

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

        rows.EditWith (KeyedCollection<'K, 'V>.EditBody, struct (this, edit))

    /// <summary>Disposes the rows and resets existing readers to empty. Idempotent.</summary>
    member _.Dispose() =
        rows.Dispose ()

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()
