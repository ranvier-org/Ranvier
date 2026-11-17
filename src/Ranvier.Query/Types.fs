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
type internal QuerySession(graph: Graph) =
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member private RaiseDisposed() : unit =
        raise (ObjectDisposedException "QueryClient")

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member private RaiseReentry() : unit =
        invalidOp "Query callbacks cannot reenter the client during reconciliation."

    member _.Graph = graph
    member val Disposed = false with get, set
    member val Staging = false with get, set

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member this.DescribeGuard() =
        graph.AssertOnGraphThread "QueryClient"

        if this.Disposed then
            QuerySession.RaiseDisposed ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member this.Guard() =
        this.DescribeGuard ()

        if this.Staging then
            QuerySession.RaiseReentry ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member this.DisposeGuard() =
        graph.AssertOnGraphThread "QueryClient.Dispose"

        if this.Staging then
            QuerySession.RaiseReentry ()

/// <summary>A detachable load waiter that settles once and unregisters itself from entry and lease lists.</summary>
[<Sealed>]
type internal QueryAwaiter<'T>(entryWaiters: ResizeArray<QueryAwaiter<'T>>, leaseWaiters: ResizeArray<QueryAwaiter<'T>>) =
    let source = TaskCompletionSource<'T>()
    let mutable detached = false

    member private this.TryClaim() : bool =
        if detached then
            false
        else
            detached <- true

            if
                not (isNull entryWaiters)
                && entryWaiters.Count > 0
            then
                entryWaiters.Remove this |> ignore

            if
                not (isNull leaseWaiters)
                && leaseWaiters.Count > 0
            then
                leaseWaiters.Remove this |> ignore

            true

    /// <summary>The load result, delivered after graph publication.</summary>
    member _.Task = source.Task

    /// <summary>Publishes an accepted result to this waiter.</summary>
    member this.Settle(value: 'T) =
        if this.TryClaim () then
            source.SetResult value

    /// <summary>Faults this waiter with a request failure.</summary>
    member this.Fail(error: exn) =
        if this.TryClaim () then
            source.SetException error

    /// <summary>Cancels this waiter without cancelling the shared request.</summary>
    member this.Cancel() =
        if this.TryClaim () then
            QueryTask.cancel source

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
