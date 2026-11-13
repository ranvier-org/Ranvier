namespace Ranvier.Query

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Ranvier

/// <summary>A page-owned reference to a query. Dispose it when the page is removed.</summary>
[<Sealed>]
type QueryLease<'T> internal (entry: QueryEntry<'T>, guard: unit -> unit, disposeGuard: unit -> unit) =
    let mutable disposed = false
    let mutable held: QueryEntry<'T> = entry
    let mutable waiters: ResizeArray<QueryAwaiter<'T>> = null

    let check () =
        guard ()

        if disposed then
            raise (ObjectDisposedException "QueryLease")

    let awaitCurrent () =
        let waiter =
            held.Await (fun current ->
                if not (isNull waiters) then
                    waiters.Remove current |> ignore)

        if not waiter.IsCompleted then
            if isNull waiters then
                waiters <- ResizeArray<QueryAwaiter<'T>>()

            waiters.Add waiter

        waiter.Task

    /// <summary>A tracked cached value; initial loading suspends and an initial failure throws.</summary>
    member _.Value =
        check ()
        held.Value

    /// <summary>A tracked snapshot, including retained data during refresh or refresh failure.</summary>
    member _.State =
        check ()
        held.State

    /// <summary>Fetches missing or stale data, joining an existing request.</summary>
    member _.Ensure() =
        check ()
        held.Ensure ()

    /// <summary>Starts a new request, superseding any existing one.</summary>
    member _.Refresh() =
        check ()
        held.Refresh ()

    /// <summary>Loads missing or stale data and awaits graph publication, joining an existing request.</summary>
    /// <remarks>The task faults on fetch failure and cancels on supersession, reconciliation, or lease/client disposal.</remarks>
    member _.EnsureAsync() =
        check ()
        held.Ensure ()
        awaitCurrent ()

    /// <summary>Supersedes the current request and awaits the replacement's graph publication.</summary>
    /// <remarks>Failure faults the task while retaining cached data. Retirement or disposal cancels the task.</remarks>
    member _.RefreshAsync() =
        check ()
        held.Refresh ()
        awaitCurrent ()

    /// <summary>Releases this lease once. Further reads throw.</summary>
    member _.Dispose() =
        if not disposed then
            disposeGuard ()
            disposed <- true
            let target = held
            held <- null
            target.ReleaseLease ()

            if not (isNull waiters) then
                if waiters.Count > 0 then
                    let pending = waiters.ToArray ()
                    waiters.Clear ()

                    for i = 0 to pending.Length - 1 do
                        pending[i].Cancel()

                waiters <- null

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

/// <summary>A non-null dictionary key containing the original typed key, including unit.</summary>
[<Struct; NoEquality; NoComparison>]
type internal QueryKey<'Key> = { Value: 'Key }

/// <summary>Compares wrapped keys using the query family's supplied equality policy.</summary>
type internal QueryKeyComparer<'Key>(comparer: IEqualityComparer<'Key>) =
    interface IEqualityComparer<QueryKey<'Key>> with
        member _.Equals(left, right) =
            comparer.Equals (left.Value, right.Value)

        member _.GetHashCode(key) =
            comparer.GetHashCode (key.Value)

/// <summary>A typed request definition. Define it once per client and include all request parameters in its key.</summary>
[<Sealed>]
type QueryFamily<'Key, 'T>
    internal
    (
        graph: Graph,
        keyComparer: IEqualityComparer<'Key>,
        fetch: 'Key -> CancellationToken -> Task<'T>,
        guard: unit -> unit,
        disposeGuard: unit -> unit,
        describeGuard: unit -> unit,
        client: obj
    ) =
    let entries =
        Dictionary<QueryKey<'Key>, QueryEntry<'T>>(QueryKeyComparer keyComparer)

    let acquire key (owner: Owner) =
        guard ()
        let wrapped = { Value = key }

        let entry =
            match entries.TryGetValue wrapped with
            | true, existing -> existing
            | false, _ ->
                let created =
                    QueryEntry<'T>(graph, fetch key, guard, fun () -> entries.Remove wrapped |> ignore)

                entries.Add (wrapped, created)
                created

        entry.AcquireLease ()
        let lease = new QueryLease<'T> (entry, guard, disposeGuard)

        try
            if obj.ReferenceEquals (owner, null) then
                graph.AddCleanup (fun () -> lease.Dispose ())
            else
                owner.AddCleanup (fun () -> lease.Dispose ())
        with error ->
            lease.Dispose ()
            raise error

        entry.Ensure ()
        lease

    /// <summary>Acquires a shared query and starts missing or stale loading.</summary>
    /// <remarks>The current Ranvier owner releases this lease. Acquire once per page and retain it while the page is in History.</remarks>
    member _.Acquire(key: 'Key) =
        acquire key Unchecked.defaultof<Owner>

    /// <summary>Acquires a shared query whose lease belongs to the explicit page owner.</summary>
    /// <remarks>The owner must be alive; dispose it when navigation removes the page. The surrounding view's owner is not used.</remarks>
    member _.AcquireOwned(key: 'Key, owner: Owner) =
        guard ()

        if obj.ReferenceEquals (owner, null) then
            nullArg "owner"

        if owner.IsDisposed then
            raise (ObjectDisposedException "owner")

        acquire key owner

    /// <summary>Updates an existing loaded record. An existing empty entry is retired and invalidated.</summary>
    /// <returns>An edit description for <c>Commit</c> or mutation reconciliation; creating it performs no write.</returns>
    /// <remarks>A loaded entry is patched, a pending empty entry is invalidated, and an absent entry remains absent.</remarks>
    member _.UpdateIfLoaded(key: 'Key, updater: 'T -> 'T) =
        describeGuard ()

        QueryEdit (
            client,
            fun context ->
                match entries.TryGetValue { Value = key } with
                | true, entry -> entry.Draft(context).Update updater
                | _ -> ()
        )

    /// <summary>Describes replacement of an existing loaded record.</summary>
    /// <returns>An edit for <c>Commit</c> or mutation reconciliation.</returns>
    /// <remarks>A pending empty entry is retired and invalidated; an absent entry remains absent.</remarks>
    member this.SetIfLoaded(key: 'Key, value: 'T) =
        this.UpdateIfLoaded (key, fun _ -> value)

    /// <summary>Marks an existing key stale and retires its request without fetching.</summary>
    /// <returns>An edit description; pass it to <c>Commit</c> or return it from mutation reconciliation to execute it.</returns>
    member _.Invalidate(key: 'Key) =
        describeGuard ()

        QueryEdit (
            client,
            fun context ->
                match entries.TryGetValue { Value = key } with
                | true, entry -> entry.Draft(context).Invalidate()
                | _ -> ()
        )

    /// <summary>Invalidates matching existing keys; the predicate is evaluated during staging.</summary>
    /// <returns>An edit description; creating it neither invalidates entries nor starts IO.</returns>
    member _.InvalidateWhere(predicate: 'Key -> bool) =
        describeGuard ()

        QueryEdit (
            client,
            fun context ->
                for pair in entries do
                    if predicate pair.Key.Value then
                        pair.Value.Draft(context).Invalidate()
        )

    /// <summary>Stages all existing entries without invoking a user predicate.</summary>
    member internal _.InvalidateAll(context: QueryStaging) =
        for entry in entries.Values do
            entry.Draft(context).Invalidate()

    /// <summary>Evicts every entry in this family and retires outstanding requests.</summary>
    member internal _.Close() =
        if entries.Count > 0 then
            let old = Seq.toArray entries.Values
            entries.Clear ()

            for i = 0 to old.Length - 1 do
                old[i].Dispose()

/// <summary>A graph-bound query session. Dispose it on Home or logout; all actions run on the graph thread.</summary>
[<Sealed>]
type QueryClient(graph: Graph) as this =
    let mutable disposed = false
    let mutable staging = false
    let identity = obj ()
    let families = ResizeArray<unit -> unit>()
    let invalidations = ResizeArray<QueryStaging -> unit>()
    let queue = System.Collections.Generic.Queue<IQueryMutation>()
    let mutable running = false
    let mutable draining = false
    let mutable pooledStaging: QueryStaging = null

    let disposeGuard () =
        graph.AssertOnGraphThread "QueryClient.Dispose"

        if staging then
            invalidOp "Query callbacks cannot reenter the client during reconciliation."

    let describeGuard () =
        graph.AssertOnGraphThread "QueryClient"

        if disposed then
            raise (ObjectDisposedException "QueryClient")

    let guard () =
        describeGuard ()

        if staging then
            invalidOp "Query callbacks cannot reenter the client during reconciliation."

    let stageEdits context edits =
        for edit: QueryEdit in edits do
            if
                isNull (box edit)
                || not (obj.ReferenceEquals (edit.Client, identity))
            then
                invalidArg "edits" "All query edits must belong to this client."

        for edit in edits do
            edit.Stage context

    let commit (edits: QueryEdit list, build: QueryStaging -> unit) =
        guard ()

        let context =
            match pooledStaging with
            | null -> QueryStaging ()
            | cached ->
                pooledStaging <- null
                cached

        try
            staging <- true

            try
                graph.RunUntracked (fun () ->
                    if obj.ReferenceEquals (box build, null) then
                        stageEdits context edits
                    else
                        build context

                    let drafts = context.Drafts

                    for i = 0 to drafts.Count - 1 do
                        drafts[i].Prepare())
            finally
                staging <- false

            let drafts = context.Drafts

            if drafts.Count > 0 then
                try
                    graph.RunBatch (fun () ->
                        for i = 0 to drafts.Count - 1 do
                            drafts[i].Publish())
                finally
                    for i = 0 to drafts.Count - 1 do
                        drafts[i].Cancel()
        finally
            context.Reset ()

            if isNull pooledStaging then
                pooledStaging <- context

    let rec drain () =
        if not draining then
            draining <- true

            try
                while not disposed && not running && queue.Count > 0 do
                    running <- true

                    queue
                        .Peek()
                        .Start(fun () ->
                            if not disposed then
                                queue.Dequeue () |> ignore
                                running <- false
                                drain ())
            finally
                draining <- false

    do graph.AssertOnGraphThread "QueryClient"
    do graph.AddCleanup (fun () -> this.Dispose ())

    /// <summary>Defines a typed query family without performing IO. Keys use the supplied comparer.</summary>
    member _.Define(keyComparer: IEqualityComparer<'Key>, fetch: 'Key -> CancellationToken -> Task<'T>) =
        guard ()

        if isNull keyComparer then
            nullArg "keyComparer"

        if obj.ReferenceEquals (fetch, null) then
            nullArg "fetch"

        let family =
            QueryFamily<'Key, 'T>(graph, keyComparer, fetch, guard, disposeGuard, describeGuard, identity)

        families.Add (fun () -> family.Close ())
        invalidations.Add family.InvalidateAll
        family

    /// <summary>Defines a typed query without IO, using the default equality comparer for its keys.</summary>
    /// <remarks>Define once per client; use the comparer overload for a custom key identity policy.</remarks>
    member this.Define(fetch: 'Key -> CancellationToken -> Task<'T>) =
        this.Define (EqualityComparer<'Key>.Default, fetch)

    /// <summary>Validates and publishes all edits atomically. Updaters must return immutable values.</summary>
    member _.Commit(edits: QueryEdit list) =
        commit (edits, Unchecked.defaultof<_>)

    /// <summary>Builds mutation edits inside the protected staging phase.</summary>
    member internal _.Reconcile(build: unit -> QueryEdit list) =
        commit ([], fun context -> stageEdits context (build ()))

    /// <summary>The explicit graph used to marshal remote completion.</summary>
    member internal _.Graph = graph
    /// <summary>Whether the client can still accept a remote result.</summary>
    member internal _.IsDisposed = disposed

    /// <summary>Admits a remote operation to the client-wide FIFO.</summary>
    member internal _.Enqueue(operation: IQueryMutation) =
        guard ()
        queue.Enqueue operation
        drain ()

    /// <summary>Invalidates every cached query after an unsuccessful reconciliation.</summary>
    member internal _.InvalidateAll() =
        commit (
            [],
            fun context ->
                for i = 0 to invalidations.Count - 1 do
                    invalidations[i] context
        )

    /// <summary>Closes the session. Further operations throw.</summary>
    member _.Dispose() =
        if not disposed then
            disposeGuard ()
            disposed <- true
            let pending = queue.ToArray ()
            queue.Clear ()

            for operation in pending do
                operation.Cancel ()

            for close in families do
                close ()

            families.Clear ()
            invalidations.Clear ()
            pooledStaging <- null

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()
