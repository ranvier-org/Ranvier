namespace Ranvier

open System
open System.Collections.Generic

[<AllowNullLiteral>]
type internal CollectionItem<'K, 'V>(key: 'K, signal: Signal<'V>) =
    member _.Key = key
    member _.Signal = signal
    member val Prev: CollectionItem<'K, 'V> = null with get, set
    member val Next: CollectionItem<'K, 'V> = null with get, set

/// <summary>The rows and membership journal of an editable collection.</summary>
type internal CollectionProjection<'K, 'V when 'K: equality>(graph: Graph) as this =
    inherit RowsOf<Signal<'V>, 'K, 'V>(graph, (fun item -> item.Value), Unchecked.defaultof<_>)

    let empty = KeyChanges.empty<'K>()
    let items = Platform.KeyMap<'K, CollectionItem<'K, 'V>>()
    let mutable head: CollectionItem<'K, 'V> = null
    let mutable tail: CollectionItem<'K, 'V> = null
    let mutable count = 0
    let revision = Signal<int>(graph, 0, EqualityComparer<int>.Default)
    let mutable changes = KeyChanges<'K>()
    let mutable previous: 'K[] = Array.empty
    let mutable first = true
    let mutable stopped = false

    let toArray () =
        if count = 0 then
            Array.empty
        else
            let arr = Array.zeroCreate<'K> count
            let mutable node = head
            let mutable i = 0

            while not (Platform.isNull node) do
                arr[i] <- node.Key
                i <- i + 1
                node <- node.Next

            arr

    do
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

                if not (Platform.isNull item) then
                    this.Visit (key, item.Signal)

        graph.RunOwned (
            this.Scope,
            fun () ->
                this.Scope.OnCleanup (fun () ->
                    stopped <- true
                    items.Clear ()
                    head <- null
                    tail <- null
                    count <- 0
                    changes.Clear ()
                    previous <- Array.empty)
        )

    member private _.EnsureLive() =
        if stopped then
            raise (ObjectDisposedException "The keyed collection was disposed.")

    member this.AddOrUpdate(key: 'K, value: 'V) =
        graph.RunBatch (fun () ->
            this.EnsureLive ()
            let item = items.Find key

            if Platform.isNull item then
                let fresh = CollectionItem<'K, 'V>(key, Signal<'V>(graph, value))

                if Platform.isNull tail then
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
                item.Signal.Value <- value)

    member this.Remove(key: 'K) =
        graph.RunBatch (fun () ->
            this.EnsureLive ()
            let item = items.Find key

            if Platform.isNull item then
                false
            else
                items.Remove key
                let prev = item.Prev
                let next = item.Next

                if Platform.isNull prev then
                    head <- next
                else
                    prev.Next <- next

                if Platform.isNull next then
                    tail <- prev
                else
                    next.Prev <- prev

                item.Prev <- null
                item.Next <- null
                count <- count - 1
                changes.Record (key, KeyChange.Removed)
                revision.Value <- revision.Peek + 1
                true)

    member this.Clear() =
        graph.RunBatch (fun () ->
            this.EnsureLive ()

            if count > 0 then
                let mutable node = head

                while not (Platform.isNull node) do
                    let next = node.Next
                    node.Prev <- null
                    node.Next <- null
                    changes.Record (node.Key, KeyChange.Removed)
                    node <- next

                items.Clear ()
                head <- null
                tail <- null
                count <- 0
                revision.Value <- revision.Peek + 1)

    member this.Edit(body: unit -> unit) =
        graph.RunBatch (fun () ->
            this.EnsureLive ()
            body ())

    override this.Enumerate() =
        revision.Value |> ignore
        let mine = this.Runs
        let keys = toArray ()
        let mutable i = 0

        while this.Runs = mine && i < keys.Length do
            let key = Platform.itemAt i keys
            i <- i + 1
            let item = items.Find key

            if not (Platform.isNull item) then
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
    let rows = new CollectionProjection<'K, 'V> (graph)
    do rows.Attach ()

    /// <summary>The reactive view used by collection operators and readers.</summary>
    member _.Rows = rows :> Projection<'K, 'V>

    /// <summary>Adds a new key at the end, or updates its existing row without changing its position.</summary>
    member _.AddOrUpdate(value: 'V) =
        rows.Edit (fun () -> rows.AddOrUpdate (keyOf value, value))

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

        rows.Edit (fun () -> edit.Invoke this)

    /// <summary>Disposes the rows and resets existing readers to empty. Idempotent.</summary>
    member _.Dispose() =
        rows.Dispose ()

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()
