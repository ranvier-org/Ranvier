namespace Ranvier.Query

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Ranvier

module internal QueryLeaseGuard =
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    let raiseDisposed () : unit =
        raise (ObjectDisposedException "QueryLease")

    /// <summary>The session guard, then the lease's own disposal check.</summary>
    let inline check (session: QuerySession) (disposed: bool) =
        session.Guard ()

        if disposed then
            raiseDisposed ()

/// <summary>A page-owned reference to a query. Dispose it when the page is removed.</summary>
[<Sealed>]
type QueryLease<'T> internal (session: QuerySession, entry: QueryEntry<'T>) =
    let mutable disposed = false
    let mutable held: QueryEntry<'T> = entry
    // The anchor of the ring of this lease's waiters; null before the first waiter and after disposal.
    let mutable waiters: QueryAwaiter<'T> = null
    let mutable link: OwnerLink = null

    let awaitCurrent () =
        if held.IsFetching then
            if isNull waiters then
                waiters <- QueryAwaiter<'T>.LeaseAnchor()

            held.AttachWaiter waiters
        else
            held.AwaitImmediate ()

    member internal _.IsDisposed = disposed

    member internal this.AttachTo(owner: Owner) =
        let attached = owner.AttachLinked (this :> IOwned)

        if not disposed then
            link <- attached

    /// <summary>A tracked cached value; initial loading suspends and an initial failure throws.</summary>
    member _.Value =
        QueryLeaseGuard.check session disposed
        held.ValueUnchecked

    /// <summary>A tracked snapshot, including retained data during refresh or refresh failure.</summary>
    member _.State =
        QueryLeaseGuard.check session disposed
        held.StateUnchecked

    /// <summary>Fetches missing or stale data, joining an existing request.</summary>
    member _.Ensure() =
        QueryLeaseGuard.check session disposed
        held.EnsureUnchecked ()

    /// <summary>Starts a new request, superseding any existing one.</summary>
    member _.Refresh() =
        QueryLeaseGuard.check session disposed
        held.RefreshUnchecked ()

    /// <summary>Loads missing or stale data and awaits graph publication, joining an existing request.</summary>
    /// <remarks>The task faults on fetch failure and cancels on supersession, reconciliation, or lease/client disposal.</remarks>
    member _.EnsureAsync() =
        QueryLeaseGuard.check session disposed
        held.EnsureUnchecked ()
        awaitCurrent ()

    /// <summary>Supersedes the current request and awaits the replacement's graph publication.</summary>
    /// <remarks>Failure faults the task while retaining cached data. Retirement or disposal cancels the task.</remarks>
    member _.RefreshAsync() =
        QueryLeaseGuard.check session disposed
        held.RefreshUnchecked ()
        awaitCurrent ()

    /// <summary>Releases this lease once. Further reads throw.</summary>
    member _.Dispose() =
        if not disposed then
            session.DisposeGuard ()
            disposed <- true
            let target = held
            held <- null
            let attached = link
            link <- null

            if not (isNull attached) then
                attached.Detach ()

            target.ReleaseLease ()
            let pending = waiters

            if not (isNull pending) then
                waiters <- null
                let mutable waiter = pending.LeaseNext

                while not (obj.ReferenceEquals (waiter, pending)) do
                    waiter.Cancel ()
                    waiter <- pending.LeaseNext

    interface IOwned with
        member this.Release() =
            this.Dispose ()

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

#if FABLE_COMPILER
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
#else
/// <summary>A non-null dictionary key containing the original typed key, including unit.</summary>
[<Struct; CustomEquality; NoComparison>]
type internal QueryKey<'Key>(value: 'Key) =
    member _.Value = value

    interface IEquatable<QueryKey<'Key>> with
        member _.Equals(other: QueryKey<'Key>) =
            EqualityComparer<'Key>.Default.Equals(value, other.Value)

    override _.Equals(other: obj) =
        match other with
        | :? QueryKey<'Key> as otherKey -> EqualityComparer<'Key>.Default.Equals(value, otherKey.Value)
        | _ -> false

    override _.GetHashCode() =
        if
            not typeof<'Key>.IsValueType
            && obj.ReferenceEquals (box value, null)
        then
            0
        else
            EqualityComparer<'Key>.Default.GetHashCode value

/// <summary>Compares wrapped keys using the query family's supplied equality policy.</summary>
type internal QueryKeyComparer<'Key>(comparer: IEqualityComparer<'Key>) =
    interface IEqualityComparer<QueryKey<'Key>> with
        member _.Equals(left, right) =
            comparer.Equals (left.Value, right.Value)

        member _.GetHashCode(key) =
            comparer.GetHashCode (key.Value)
#endif

module private QueryKeys =
    let inline wrap (key: 'Key) : QueryKey<'Key> =
#if FABLE_COMPILER
        { Value = key }
#else
        QueryKey<'Key>(key)
#endif

/// <summary>Family callback contract allowing entries to invoke fetches and evict themselves without closures.</summary>
[<AllowNullLiteral>]
type internal IQueryKeyOwner<'Key, 'T> =
    abstract InvokeFetch: 'Key * CancellationToken -> Task<'T>
    abstract EvictKey: 'Key -> unit

/// <summary>Per-key query entry storing its own typed key and family reference without per-entry closures.</summary>
and [<Sealed; AllowNullLiteral>] internal QueryFamilyEntry<'Key, 'T>(session: QuerySession, key: 'Key, owner: IQueryKeyOwner<'Key, 'T>) =
    inherit QueryEntry<'T>(session)

    override _.InvokeFetch(token: CancellationToken) =
        owner.InvokeFetch (key, token)

    override _.OnEvict() =
        owner.EvictKey key

/// <summary>A typed request definition. Define it once per client and include all request parameters in its key.</summary>
[<Sealed>]
type QueryFamily<'Key, 'T> internal (session: QuerySession, keyComparer: IEqualityComparer<'Key>, fetch: 'Key -> CancellationToken -> Task<'T>) =
    let graph = session.Graph

#if !FABLE_COMPILER
    let fetchFn =
        OptimizedClosures.FSharpFunc<'Key, CancellationToken, Task<'T>>.Adapt fetch
#endif

    let entries =
#if FABLE_COMPILER
        Dictionary<QueryKey<'Key>, QueryFamilyEntry<'Key, 'T>>(QueryKeyComparer keyComparer)
#else
        if obj.ReferenceEquals (keyComparer, EqualityComparer<'Key>.Default) then
            Dictionary<QueryKey<'Key>, QueryFamilyEntry<'Key, 'T>>()
        else
            Dictionary<QueryKey<'Key>, QueryFamilyEntry<'Key, 'T>>(QueryKeyComparer keyComparer)
#endif

    let entryOwner =
        { new IQueryKeyOwner<'Key, 'T> with
            member _.InvokeFetch(key, token) =
#if FABLE_COMPILER
                fetch key token
#else
                fetchFn.Invoke (key, token)
#endif

            member _.EvictKey(key) =
                entries.Remove (QueryKeys.wrap key) |> ignore
        }

    let acquire key (owner: Owner) =
        session.Guard ()

        let targetOwner =
            if obj.ReferenceEquals (owner, null) then
                graph.CurrentOwner
            else
                owner

        let wrapped = QueryKeys.wrap key

        let entry =
            match entries.TryGetValue wrapped with
            | true, existing -> existing
            | false, _ ->
                let created = QueryFamilyEntry<'Key, 'T>(session, key, entryOwner)
                entries.Add (wrapped, created)
                created

        entry.AcquireLease ()
        let lease = new QueryLease<'T> (session, entry)

        try
            lease.AttachTo targetOwner
        with error ->
            lease.Dispose ()
            raise error

        if not lease.IsDisposed then
            entry.EnsureUnchecked ()

        lease

    /// <summary>Acquires a shared query and starts missing or stale loading.</summary>
    /// <remarks>The current Ranvier owner releases this lease. Acquire once per page and retain it while the page is in History.</remarks>
    member _.Acquire(key: 'Key) =
        acquire key Unchecked.defaultof<Owner>

    /// <summary>Acquires a shared query whose lease belongs to the explicit page owner.</summary>
    /// <remarks>The owner must be alive; dispose it when navigation removes the page. The surrounding view's owner is not used.</remarks>
    member _.AcquireOwned(key: 'Key, owner: Owner) =
        session.Guard ()

        if obj.ReferenceEquals (owner, null) then
            nullArg "owner"

        if owner.IsDisposed then
            raise (ObjectDisposedException "owner")

        acquire key owner

    /// <summary>Updates an existing loaded record. An existing empty entry is retired and invalidated.</summary>
    /// <returns>An edit description for <c>Commit</c> or mutation reconciliation; creating it performs no write.</returns>
    /// <remarks>A loaded entry is patched, a pending empty entry is invalidated, and an absent entry remains absent.</remarks>
    member _.UpdateIfLoaded(key: 'Key, updater: 'T -> 'T) =
        session.DescribeGuard ()

        QueryEdit (
            session,
            fun context ->
                match entries.TryGetValue (QueryKeys.wrap key) with
                | true, entry -> entry.StageUpdate (context, updater)
                | _ -> ()
        )

    /// <summary>Describes replacement of an existing loaded record.</summary>
    /// <returns>An edit for <c>Commit</c> or mutation reconciliation.</returns>
    /// <remarks>A pending empty entry is retired and invalidated; an absent entry remains absent.</remarks>
    member _.SetIfLoaded(key: 'Key, value: 'T) =
        session.DescribeGuard ()

        QueryEdit (
            session,
            fun context ->
                match entries.TryGetValue (QueryKeys.wrap key) with
                | true, entry -> entry.StageSet (context, value)
                | _ -> ()
        )

    /// <summary>Marks an existing key stale and retires its request without fetching.</summary>
    /// <returns>An edit description; pass it to <c>Commit</c> or return it from mutation reconciliation to execute it.</returns>
    member _.Invalidate(key: 'Key) =
        session.DescribeGuard ()

        QueryEdit (
            session,
            fun context ->
                match entries.TryGetValue (QueryKeys.wrap key) with
                | true, entry -> entry.StageInvalidate context
                | _ -> ()
        )

    /// <summary>Invalidates matching existing keys; the predicate is evaluated during staging.</summary>
    /// <returns>An edit description; creating it neither invalidates entries nor starts IO.</returns>
    member _.InvalidateWhere(predicate: 'Key -> bool) =
        session.DescribeGuard ()

        QueryEdit (
            session,
            fun context ->
                for pair in entries do
                    if predicate pair.Key.Value then
                        pair.Value.StageInvalidate context
        )

    interface IQueryFamily with
        member _.InvalidateAll(context: QueryStaging) =
            for entry in entries.Values do
                entry.StageInvalidate context

        member _.Close() =
            if entries.Count > 0 then
                let old = Seq.toArray entries.Values
                entries.Clear ()

                for i = 0 to old.Length - 1 do
                    old[i].Dispose()

/// <summary>A graph-bound query session. Dispose it on Home or logout; all actions run on the graph thread.</summary>
[<Sealed>]
type QueryClient(graph: Graph) =
    let session = QuerySession graph
    let families = ResizeArray<IQueryFamily>()
    let queue = System.Collections.Generic.Queue<IQueryMutation>()
    let mutable running = false
    let mutable draining = false
    let mutable pooledStaging: QueryStaging = null
    let mutable ownerLink: OwnerLink = null
    let mutable onMutationFinished: unit -> unit = Unchecked.defaultof<_>
    let mutable invalidateAllAction: QueryStaging -> unit = Unchecked.defaultof<_>

    let stageEdits (context: QueryStaging) (edits: QueryEdit list) =
        let mutable rest = edits

        while not rest.IsEmpty do
            let edit = rest.Head

            if
                isNull (box edit)
                || not (obj.ReferenceEquals (edit.Client, box session))
            then
                invalidArg "edits" "All query edits must belong to this client."

            rest <- rest.Tail

        let mutable cur = edits

        while not cur.IsEmpty do
            cur.Head.Stage context
            cur <- cur.Tail

    let stageAndPrepare (context: QueryStaging) (edits: QueryEdit list) (build: QueryStaging -> unit) =
        if isNull (box build) then
            stageEdits context edits
        else
            build context

        let drafts = context.Drafts

        for i = 0 to drafts.Count - 1 do
            drafts[i].Prepare()

    let commit (edits: QueryEdit list, build: QueryStaging -> unit) =
        session.Guard ()

        let context =
            match pooledStaging with
            | null -> QueryStaging ()
            | cached ->
                pooledStaging <- null
                cached

        try
            session.Staging <- true

            try
                if not (graph.IsTracking ()) then
                    if not (graph.Enter "An untracked read") then
                        stageAndPrepare context edits build
                    else
                        try
                            stageAndPrepare context edits build
                        finally
                            graph.Release ()
                else
                    graph.RunUntracked (fun () -> stageAndPrepare context edits build)
            finally
                session.Staging <- false

            let drafts = context.Drafts

            if drafts.Count > 0 then
                try
                    graph.RunBatch context.PublishAction
                finally
                    let retired = context.Retired

                    for i = 0 to retired.Count - 1 do
                        retired[i].Cancel()
        finally
            context.Reset ()

            if isNull pooledStaging then
                pooledStaging <- context

    let rec drain () =
        if not draining then
            draining <- true

            try
                while not session.Disposed
                      && not running
                      && queue.Count > 0 do
                    running <- true

                    if isNull (box onMutationFinished) then
                        onMutationFinished <-
                            fun () ->
                                if not session.Disposed then
                                    queue.Dequeue () |> ignore
                                    running <- false
                                    drain ()

                    queue.Peek().Start(onMutationFinished)
            finally
                draining <- false

    let disposeClient () =
        if not session.Disposed then
            session.DisposeGuard ()
            session.Disposed <- true
            let attached = ownerLink
            ownerLink <- null

            if not (isNull attached) then
                attached.Detach ()

            if queue.Count > 0 then
                let pending = queue.ToArray ()
                queue.Clear ()

                for operation in pending do
                    operation.Cancel ()

            for i = 0 to families.Count - 1 do
                families[i].Close()

            families.Clear ()
            pooledStaging <- null

    do
        graph.AssertOnGraphThread "QueryClient"
        let owner = graph.CurrentOwner

        let owned =
            { new IOwned with
                member _.Release() =
                    disposeClient ()
            }

        let attached = owner.AttachLinked owned

        if not session.Disposed then
            ownerLink <- attached

        if owner.IsDisposed then
            graph.RequestFlush ()

    /// <summary>Defines a typed query family without performing IO. Keys use the supplied comparer.</summary>
    member _.Define(keyComparer: IEqualityComparer<'Key>, fetch: 'Key -> CancellationToken -> Task<'T>) =
        session.Guard ()

        if isNull keyComparer then
            nullArg "keyComparer"

        if obj.ReferenceEquals (fetch, null) then
            nullArg "fetch"

        let family = QueryFamily<'Key, 'T>(session, keyComparer, fetch)
        families.Add (family :> IQueryFamily)
        family

    /// <summary>Defines a typed query without IO, using the default equality comparer for its keys.</summary>
    /// <remarks>Define once per client; use the comparer overload for a custom key identity policy.</remarks>
    member this.Define(fetch: 'Key -> CancellationToken -> Task<'T>) =
        this.Define (EqualityComparer<'Key>.Default, fetch)

    /// <summary>Validates and publishes all edits atomically. Updaters must return immutable values.</summary>
    member _.Commit(edits: QueryEdit list) =
        commit (edits, Unchecked.defaultof<_>)

    /// <summary>Builds mutation edits inside the protected staging phase.</summary>
    member internal _.ReconcileWith(reconcile: 'T -> QueryEdit list, result: 'T) =
        commit ([], fun context -> stageEdits context (reconcile result))

    /// <summary>The explicit graph used to marshal remote completion.</summary>
    member internal _.Graph = graph
    /// <summary>Whether the client can still accept a remote result.</summary>
    member internal _.IsDisposed = session.Disposed

    /// <summary>Admits a remote operation to the client-wide FIFO.</summary>
    member internal _.Enqueue(operation: IQueryMutation) =
        session.Guard ()
        queue.Enqueue operation
        drain ()

    /// <summary>Invalidates every cached query after an unsuccessful reconciliation.</summary>
    member internal _.InvalidateAll() =
        if isNull (box invalidateAllAction) then
            invalidateAllAction <-
                fun context ->
                    for i = 0 to families.Count - 1 do
                        families[i].InvalidateAll context

        commit ([], invalidateAllAction)

    /// <summary>Closes the session. Further operations throw.</summary>
    member _.Dispose() =
        disposeClient ()

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()
