namespace Ranvier.Query

open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Fable.Core

/// <summary>Portable completion-source cancellation.</summary>
module internal QueryTask =
    /// <summary>Cancels a completion source using the target runtime's operation.</summary>
    [<Emit("$0.SetCancelled()")>]
    let cancel (source: TaskCompletionSource<'T>) =
        source.SetCanceled ()

/// <summary>A detachable load waiter that settles once and releases its lease callbacks.</summary>
type internal QueryAwaiter<'T>(completed: QueryAwaiter<'T> -> unit) as this =
    let source = TaskCompletionSource<'T>()
    let mutable terminal = false
    let mutable release = completed

    member private _.Finish(deliver: unit -> unit) =
        if not terminal then
            terminal <- true
            let callback = release
            release <- ignore
            callback this
            deliver ()

    /// <summary>The load result, delivered after graph publication.</summary>
    member _.Task = source.Task
    /// <summary>Whether this waiter has already detached.</summary>
    member _.IsCompleted = terminal

    /// <summary>Publishes an accepted result to this waiter.</summary>
    member this.Settle(value: 'T) =
        this.Finish (fun () -> source.SetResult value)

    /// <summary>Faults this waiter with a request failure.</summary>
    member this.Fail(error: exn) =
        this.Finish (fun () -> source.SetException error)

    /// <summary>Cancels this waiter without cancelling the shared request.</summary>
    member this.Cancel() =
        this.Finish (fun () -> QueryTask.cancel source)

/// <summary>A staged entry whose validation precedes all publication.</summary>
[<AllowNullLiteral>]
type internal IQueryDraft =
    abstract Prepare: unit -> unit
    abstract Publish: unit -> unit
    abstract Cancel: unit -> unit

/// <summary>An entry contract driven by a typed draft without per-stage closures.</summary>
[<AllowNullLiteral>]
type internal IQueryEntry<'T> =
    abstract PrepareDraft: initial: 'T option * next: 'T option * updated: bool -> bool
    abstract PublishDraft: next: 'T option * changed: bool * stale: bool * draft: QueryDraft<'T> -> unit
    abstract CancelDraft: waiters: QueryAwaiter<'T>[] * cancellation: CancellationTokenSource -> unit

/// <summary>A typed editable draft shared by ordered descriptions targeting one entry.</summary>
and [<AllowNullLiteral>] internal QueryDraft<'T>(entry: IQueryEntry<'T>, initial: 'T option) =
    let mutable data = initial
    let mutable stale = true
    let mutable updated = false
    let mutable changed = false
    let mutable retiredWaiters: QueryAwaiter<'T>[] = null
    let mutable retiredCancellation: CancellationTokenSource = null

    /// <summary>Records wait and cancellation handles retired during publish for deferred teardown.</summary>
    member internal _.SetRetired(waiters: QueryAwaiter<'T>[], cancellation: CancellationTokenSource) =
        retiredWaiters <- waiters
        retiredCancellation <- cancellation

    member _.Update(updater: 'T -> 'T) =
        match data with
        | Some value ->
            data <- Some (updater value)
            stale <- false
            updated <- true
        | None -> ()

    member _.Invalidate() =
        stale <- true

    interface IQueryDraft with
        member _.Prepare() =
            changed <- entry.PrepareDraft (initial, data, updated)

        member this.Publish() =
            entry.PublishDraft (data, changed, stale, this)

        member _.Cancel() =
            if
                not (isNull retiredWaiters)
                || not (isNull retiredCancellation)
            then
                let waiters = retiredWaiters
                let cts = retiredCancellation
                retiredWaiters <- null
                retiredCancellation <- null
                entry.CancelDraft (waiters, cts)

/// <summary>A transaction's heterogeneous drafts; family lookup remains fully typed.</summary>
[<AllowNullLiteral>]
type internal QueryStaging() =
    let entries = Dictionary<obj, obj>(HashIdentity.Reference)
    let drafts = ResizeArray<IQueryDraft>()

    member _.TryGet(identity: obj) : obj =
        match entries.TryGetValue identity with
        | true, value -> value
        | false, _ -> null

    member _.Add(identity: obj, draft: IQueryDraft) =
        entries.Add (identity, box draft)
        drafts.Add draft

    member _.Drafts = drafts

    member _.Reset() =
        if entries.Count > 0 then
            entries.Clear ()

        if drafts.Count > 0 then
            drafts.Clear ()

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
type internal PublicationComparer<'T>() =
    static let instance = PublicationComparer<'T>() :> IEqualityComparer<'T>

    static member Instance = instance

    interface IEqualityComparer<'T> with
        member _.Equals(_, _) = false
        member _.GetHashCode(_) = 0
