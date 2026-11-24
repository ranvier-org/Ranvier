namespace Ranvier.Query

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Ranvier

/// <summary>Portable completion-source cancellation and zero-state-machine task helpers.</summary>
module internal QueryTask =
    /// <summary>Cancels a completion source using the target runtime's operation.</summary>
    [<Emit("$0.SetCancelled()")>]
    let cancel (source: TaskCompletionSource<'T>) =
        source.SetCanceled ()

#if FABLE_COMPILER
    /// <summary>Returns an already-succeeded task carrying <c>value</c>.</summary>
    let settled (value: 'T) : Task<'T> =
        Task.FromResult value

    /// <summary>Returns an already-faulted task carrying <c>error</c>.</summary>
    let faulted<'T> (error: exn) : Task<'T> =
        let source = TaskCompletionSource<'T>()
        source.SetException error
        source.Task

    /// <summary>Returns an already-canceled task.</summary>
    let cancelled<'T> () : Task<'T> =
        let source = TaskCompletionSource<'T>()
        cancel source
        source.Task
#else
    /// <summary>Typed receiver notified when an in-flight request completes.</summary>
    [<AllowNullLiteral>]
    type internal IQueryTaskObserver<'T> =
        abstract OnCompleted: Task<'T> -> unit

    [<AbstractClass; Sealed>]
    type internal CancelledTask<'T>() =
        static member val Instance: Task<'T> = Task.FromCanceled<'T>(CancellationToken true)

    [<Sealed>]
    type internal QueryTaskContinuation<'T>() =
        static member val Invoke: Action<Task<'T>, obj> = Action<Task<'T>, obj>(fun t s -> (s :?> IQueryTaskObserver<'T>).OnCompleted t)

    /// <summary>Returns an already-succeeded task carrying <c>value</c>.</summary>
    let inline settled (value: 'T) : Task<'T> =
        Task.FromResult value

    /// <summary>Returns an already-faulted task carrying <c>error</c>.</summary>
    let inline faulted<'T> (error: exn) : Task<'T> =
        Task.FromException<'T> error

    /// <summary>Returns a shared already-canceled task.</summary>
    let inline cancelled<'T> () : Task<'T> =
        CancelledTask<'T>.Instance

    /// <summary>Extracts the underlying exception from a faulted or canceled task without unnecessary throws.</summary>
    let extractError (completed: Task<'T>) : exn =
        if completed.IsFaulted then
            let agg = completed.Exception

            if
                not (isNull agg)
                && not (isNull agg.InnerException)
            then
                agg.InnerException
            else
                agg :> exn
        else
            try
                completed.GetAwaiter().GetResult() |> ignore
                OperationCanceledException () :> exn
            with error ->
                error

    /// <summary>Observes completion of <c>request</c> through a stateless continuation.</summary>
    let inline observe (request: Task<'T>) (observer: IQueryTaskObserver<'T>) =
        request.ContinueWith (
            QueryTaskContinuation<'T>.Invoke,
            box observer,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        )
        |> ignore
#endif

/// <summary>Shared thread, disposal, and reentrancy guard state for one <c>QueryClient</c>.</summary>
[<Sealed; AllowNullLiteral>]
type internal QuerySession =
    val Graph: Graph
    val mutable Disposed: bool
    val mutable Staging: bool

    new(graph: Graph) =
        {
            Graph = graph
            Disposed = false
            Staging = false
        }

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member RaiseDisposed() : unit =
        raise (ObjectDisposedException "QueryClient")

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member RaiseReentry() : unit =
        invalidOp "Query callbacks cannot reenter the client during reconciliation."

    member inline this.DescribeGuard() =
        this.Graph.AssertOnGraphThread "QueryClient"

        if this.Disposed then
            QuerySession.RaiseDisposed ()

    member inline this.Guard() =
        this.Graph.AssertOnGraphThread "QueryClient"

        if this.Disposed then
            QuerySession.RaiseDisposed ()

        if this.Staging then
            QuerySession.RaiseReentry ()

    member inline this.DisposeGuard() =
        this.Graph.AssertOnGraphThread "QueryClient.Dispose"

        if this.Staging then
            QuerySession.RaiseReentry ()

/// <summary>
/// A load waiter linked into the waiter ring of its entry's request and the waiter ring of its lease. It completes
/// once, leaving both rings before its task completes.
/// </summary>
/// <remarks>
/// A ring is circular through an anchor, a node with a null <c>Source</c> that completes nothing. The entry links run
/// through an entry's anchor and the lease links through a lease's; each ring holds its waiters in registration
/// order. A waiter's links are null once it has completed.
/// </remarks>
[<Sealed; AllowNullLiteral>]
type internal QueryAwaiter<'T> =
    val Source: TaskCompletionSource<'T>
    val mutable EntryPrev: QueryAwaiter<'T>
    val mutable EntryNext: QueryAwaiter<'T>
    val mutable LeasePrev: QueryAwaiter<'T>
    val mutable LeaseNext: QueryAwaiter<'T>

    new(source: TaskCompletionSource<'T>) =
        {
            Source = source
            EntryPrev = null
            EntryNext = null
            LeasePrev = null
            LeaseNext = null
        }

    /// <summary>The anchor of an empty entry ring.</summary>
    static member EntryAnchor() : QueryAwaiter<'T> =
        let anchor = QueryAwaiter<'T>(null)
        anchor.EntryPrev <- anchor
        anchor.EntryNext <- anchor
        anchor

    /// <summary>The anchor of an empty lease ring.</summary>
    static member LeaseAnchor() : QueryAwaiter<'T> =
        let anchor = QueryAwaiter<'T>(null)
        anchor.LeasePrev <- anchor
        anchor.LeaseNext <- anchor
        anchor

    /// <summary>A waiter appended to the entry ring of <c>entryAnchor</c> and the lease ring of <c>leaseAnchor</c>.</summary>
    static member Attach(entryAnchor: QueryAwaiter<'T>, leaseAnchor: QueryAwaiter<'T>) : QueryAwaiter<'T> =
        let waiter = QueryAwaiter<'T>(TaskCompletionSource<'T>())
        let entryLast = entryAnchor.EntryPrev
        waiter.EntryPrev <- entryLast
        waiter.EntryNext <- entryAnchor
        entryLast.EntryNext <- waiter
        entryAnchor.EntryPrev <- waiter
        let leaseLast = leaseAnchor.LeasePrev
        waiter.LeasePrev <- leaseLast
        waiter.LeaseNext <- leaseAnchor
        leaseLast.LeaseNext <- waiter
        leaseAnchor.LeasePrev <- waiter
        waiter

    /// <summary>Unlinks this waiter from both rings; false when it has already completed.</summary>
    member private this.TryClaim() : bool =
        let entryNext = this.EntryNext

        if isNull entryNext then
            false
        else
            let entryPrev = this.EntryPrev
            entryPrev.EntryNext <- entryNext
            entryNext.EntryPrev <- entryPrev
            this.EntryPrev <- null
            this.EntryNext <- null
            let leasePrev = this.LeasePrev
            let leaseNext = this.LeaseNext
            leasePrev.LeaseNext <- leaseNext
            leaseNext.LeasePrev <- leasePrev
            this.LeasePrev <- null
            this.LeaseNext <- null
            true

    /// <summary>The load result, delivered after graph publication.</summary>
    member this.Task = this.Source.Task

    /// <summary>Publishes an accepted result to this waiter.</summary>
    member this.Settle(value: 'T) =
        if this.TryClaim () then
            this.Source.SetResult value

    /// <summary>Faults this waiter with a request failure.</summary>
    member this.Fail(error: exn) =
        if this.TryClaim () then
            this.Source.SetException error

    /// <summary>Cancels this waiter without cancelling the shared request.</summary>
    member this.Cancel() =
        if this.TryClaim () then
            QueryTask.cancel this.Source

/// <summary>Retired request handles captured during draft publication for post-batch teardown.</summary>
[<Struct; NoEquality; NoComparison>]
type internal QueryRetired(draft: IQueryDraft, waiters: obj, cancellation: CancellationTokenSource) =
    member _.Cancel() =
        draft.CancelRetired (waiters, cancellation)

/// <summary>A staged entry whose validation precedes all publication and post-batch teardown.</summary>
and [<AllowNullLiteral>] internal IQueryDraft =
    abstract Prepare: unit -> unit
    abstract Publish: QueryStaging -> unit
    abstract CancelRetired: waiters: obj * cancellation: CancellationTokenSource -> unit
    abstract ResetDraft: unit -> unit

/// <summary>A transaction's staged entries and retired request handles.</summary>
and [<Sealed; AllowNullLiteral>] internal QueryStaging() =
    let drafts = ResizeArray<IQueryDraft>()
    let retired = ResizeArray<QueryRetired>()
    let mutable publishAction: unit -> unit = Unchecked.defaultof<_>

    member _.Drafts = drafts
    member _.Retired = retired

    member _.AddRetired(item: QueryRetired) =
        retired.Add item

    member private this.PublishAll() =
        for i = 0 to drafts.Count - 1 do
            drafts[i].Publish this

    member this.PublishAction =
        let mutable fn = publishAction

        if isNull (box fn) then
            fn <- this.PublishAll
            publishAction <- fn

        fn

    member _.Reset() =
        for i = 0 to drafts.Count - 1 do
            drafts[i].ResetDraft()

        if drafts.Count > 0 then
            drafts.Clear ()

        if retired.Count > 0 then
            retired.Clear ()

/// <summary>Family operations driven by the owning <c>QueryClient</c>.</summary>
[<AllowNullLiteral>]
type internal IQueryFamily =
    abstract InvalidateAll: QueryStaging -> unit
    abstract Close: unit -> unit

/// <summary>An opaque, client-bound description of a cache update or invalidation.</summary>
[<Sealed>]
type QueryEdit internal (client: obj, stage: QueryStaging -> unit) =
    member internal _.Client = client

    member internal _.Stage(context) =
        stage context

/// <summary>A queued remote operation with client-owned cancellation.</summary>
type internal IQueryMutation =
    abstract Start: (unit -> unit) -> unit
    abstract Cancel: unit -> unit

/// <summary>The current request activity, independent of cached data availability.</summary>
[<RequireQualifiedAccess>]
type FetchStatus =
    | Idle
    | Fetching

/// <summary>Internal request state without tuple allocation on .NET.</summary>
#if !FABLE_COMPILER
[<Struct; NoEquality; NoComparison>]
#else
[<NoEquality; NoComparison>]
#endif
type internal QueryMeta =
    {
        FetchStatus: FetchStatus
        Error: exn option
        IsStale: bool
    }

/// <summary>Cached data and request state. A refresh failure retains the last accepted data.</summary>
type QuerySnapshot<'T> =
    {
        Data: 'T option
        FetchStatus: FetchStatus
        Error: exn option
        IsStale: bool
    }

/// <summary>The result of a remote mutation and its local cache reconciliation.</summary>
[<RequireQualifiedAccess>]
type MutationOutcome<'T> =
    | Applied of 'T
    | RequestFailed of exn
    | ReconciliationFailed of result: 'T * error: exn

/// <summary>A publication comparer that accepts prevalidated source writes.</summary>
[<Sealed>]
type internal PublicationComparer<'T> private () =
    static member val Instance: IEqualityComparer<'T> = PublicationComparer<'T>() :> IEqualityComparer<'T>

    interface IEqualityComparer<'T> with
        member _.Equals(_, _) = false
        member _.GetHashCode(_) = 0

/// <summary>Sentinel-encoded equality comparison avoiding interface dispatch under default policies.</summary>
module internal QueryValueComparer =
#if FABLE_COMPILER
    let encode<'T> (graph: Graph) : IEqualityComparer<'T> =
        if (box graph.Options.Equality) :? JsIdentityPolicy then
            Unchecked.defaultof<IEqualityComparer<'T>>
        else
            let comparer = graph.Options.Equality.Comparer<'T>()

            if obj.ReferenceEquals (comparer, null) then
                NullComparer<'T>.Instance
            elif (box comparer) :? ReferenceComparerBase then
                Unchecked.defaultof<IEqualityComparer<'T>>
            else
                comparer

    let inline equals (encoded: IEqualityComparer<'T>) (a: 'T) (b: 'T) : bool =
        if isNull (box encoded) then
            obj.ReferenceEquals (box a, box b)
        else
            encoded.Equals (a, b)
#else
    let encode<'T> (graph: Graph) : IEqualityComparer<'T> =
        ComparerSentinel.encode (graph.Options.Equality.Comparer<'T>())

    let inline equals (encoded: IEqualityComparer<'T>) (a: 'T) (b: 'T) : bool =
        if typeof<'T>.IsValueType then
            if obj.ReferenceEquals (encoded, null) then
                EqualityComparer<'T>.Default.Equals(a, b)
            else
                encoded.Equals (a, b)
        elif obj.ReferenceEquals (encoded, null) then
            obj.ReferenceEquals (box a, box b)
        else
            encoded.Equals (a, b)
#endif
