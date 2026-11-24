module Ranvier.Tests.QueryWaiters

open System
open System.Threading
open System.Threading.Tasks
open Expecto
open Ranvier
open Ranvier.Query

let private newGraph () =
    new Graph (
        { GraphOptions.Default with
            ThreadAffinity = ThreadAffinity.Unchecked
            Dispatcher = Some (ImmediateDispatcher ())
        }
    )

let private outcomeOf (completed: Task<int>) =
    if completed.IsCanceled then "cancelled"
    elif completed.IsFaulted then "faulted"
    else string completed.Result

/// <summary>Runs <c>act</c> with the waiter's outcome on the thread that completes the waiter.</summary>
let private onCompleted (act: string -> unit) (pending: Task<int>) : Task =
    pending.ContinueWith (
        (fun (completed: Task<int>) -> act (outcomeOf completed)),
        CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default
    )

/// <summary>Appends <c>name=outcome</c> to <c>log</c> on the thread that completes the waiter.</summary>
let private recordInto (log: ResizeArray<string>) (name: string) (pending: Task<int>) : Task =
    onCompleted (fun outcome -> lock log (fun () -> log.Add (name + "=" + outcome))) pending

let private finished (tasks: Task array) =
    Task.WaitAll (tasks, 10000)

[<Tests>]
let tests =
    testList
        "QueryWaiters"
        [
            testCase "waiters of one request complete in registration order across leases"
            <| fun () ->
                use graph = newGraph ()
                use client = new QueryClient (graph)
                let reply = TaskCompletionSource<int>()
                let family = client.Define (fun () _ -> reply.Task)
                use first = family.Acquire ()
                use second = family.Acquire ()
                let log = ResizeArray<string>()

                let completions =
                    [|
                        for index in 0..7 ->
                            let lease = if index % 2 = 0 then first else second
                            recordInto log ("w" + string index) (lease.EnsureAsync ())
                    |]

                reply.SetResult 7
                Expect.isTrue (finished completions) "every waiter completed"
                Expect.sequenceEqual log [ for index in 0..7 -> "w" + string index + "=7" ] "registration order"

            testCase "a waiter registered by a continuation joins the next request"
            <| fun () ->
                use graph = newGraph ()
                use client = new QueryClient (graph)

                let replies = [| TaskCompletionSource<int>(); TaskCompletionSource<int>() |]

                let mutable calls = 0

                let family =
                    client.Define (fun () _ ->
                        let reply = replies[calls]
                        calls <- calls + 1
                        reply.Task)

                use page = family.Acquire ()
                let log = ResizeArray<string>()
                let mutable later: Task = null

                let first =
                    page.EnsureAsync ()
                    |> onCompleted (fun outcome ->
                        log.Add ("first=" + outcome)
                        later <- recordInto log "later" (page.RefreshAsync ()))

                let second = recordInto log "second" (page.EnsureAsync ())
                replies[0].SetResult 1
                Expect.isTrue (finished [| first; second |]) "the first request's waiters completed"
                Expect.equal calls 2 "the continuation started the next request"
                Expect.isFalse later.IsCompleted "the new waiter waits for the next request"
                Expect.sequenceEqual log [ "first=1"; "second=1" ] "the completing request keeps its remaining waiter"
                replies[1].SetResult 2
                Expect.isTrue (finished [| later |]) "the next request's waiter completed"
                Expect.sequenceEqual log [ "first=1"; "second=1"; "later=2" ] "the new waiter sees the next result"
                Expect.equal page.Value 2 "the next result is published"

            testCase "a continuation disposing a lease cancels that lease's waiters on the completing request"
            <| fun () ->
                use graph = newGraph ()
                use client = new QueryClient (graph)
                let reply = TaskCompletionSource<int>()
                let family = client.Define (fun () _ -> reply.Task)
                use kept = family.Acquire ()
                let closing = family.Acquire ()
                let log = ResizeArray<string>()

                let a =
                    kept.EnsureAsync ()
                    |> onCompleted (fun outcome ->
                        log.Add ("a=" + outcome)
                        closing.Dispose ())

                let b = recordInto log "b" (closing.EnsureAsync ())
                let c = recordInto log "c" (kept.EnsureAsync ())
                let d = recordInto log "d" (closing.EnsureAsync ())
                reply.SetResult 5
                Expect.isTrue (finished [| a; b; c; d |]) "every waiter completed"
                Expect.sequenceEqual log [ "a=5"; "b=cancelled"; "d=cancelled"; "c=5" ] "the disposed lease's waiters cancel in order"
                Expect.equal kept.Value 5 "the retained lease reads the result"

            testCase "lease disposal cancels its waiters in registration order and leaves other leases waiting"
            <| fun () ->
                use graph = newGraph ()
                use client = new QueryClient (graph)
                let reply = TaskCompletionSource<int>()
                let family = client.Define (fun () _ -> reply.Task)
                use kept = family.Acquire ()
                let closing = family.Acquire ()
                let log = ResizeArray<string>()
                let x0 = recordInto log "x0" (closing.EnsureAsync ())
                let y0 = recordInto log "y0" (kept.EnsureAsync ())
                let x1 = recordInto log "x1" (closing.EnsureAsync ())
                let y1 = recordInto log "y1" (kept.EnsureAsync ())
                let x2 = recordInto log "x2" (closing.EnsureAsync ())
                closing.Dispose ()
                closing.Dispose ()
                Expect.isTrue (finished [| x0; x1; x2 |]) "the disposed lease's waiters completed"
                Expect.sequenceEqual log [ "x0=cancelled"; "x1=cancelled"; "x2=cancelled" ] "cancelled in registration order"
                Expect.throws (fun () -> closing.EnsureAsync () |> ignore) "a disposed lease rejects a wait"
                reply.SetResult 9
                Expect.isTrue (finished [| y0; y1 |]) "the retained lease's waiters completed"

                Expect.sequenceEqual log [ "x0=cancelled"; "x1=cancelled"; "x2=cancelled"; "y0=9"; "y1=9" ] "the retained waiters settle in order"

            testCase "a failed request faults its waiters in order and a continuation's retry joins the next request"
            <| fun () ->
                use graph = newGraph ()
                use client = new QueryClient (graph)

                let replies = [| TaskCompletionSource<int>(); TaskCompletionSource<int>() |]

                let mutable calls = 0

                let family =
                    client.Define (fun () _ ->
                        let reply = replies[calls]
                        calls <- calls + 1
                        reply.Task)

                use page = family.Acquire ()
                let log = ResizeArray<string>()
                let mutable retry: Task = null

                let first =
                    page.EnsureAsync ()
                    |> onCompleted (fun outcome ->
                        log.Add ("first=" + outcome)
                        retry <- recordInto log "retry" (page.EnsureAsync ()))

                let second = recordInto log "second" (page.EnsureAsync ())
                replies[0].SetException(InvalidOperationException "offline")
                Expect.isTrue (finished [| first; second |]) "the failed request's waiters completed"
                Expect.sequenceEqual log [ "first=faulted"; "second=faulted" ] "faulted in registration order"
                Expect.equal calls 2 "the retry started the next request"
                replies[1].SetResult 4
                Expect.isTrue (finished [| retry |]) "the retry completed"
                Expect.equal log[2] "retry=4" "the retry sees the next result"

            testCase "every waiter of a large shared lease completes once, and disposal cancels a large pending set"
            <| fun () ->
                use graph = newGraph ()
                use client = new QueryClient (graph)

                let replies = [| TaskCompletionSource<int>(); TaskCompletionSource<int>() |]

                let mutable calls = 0

                let family =
                    client.Define (fun () _ ->
                        let reply = replies[calls]
                        calls <- calls + 1
                        reply.Task)

                let page = family.Acquire ()
                let count = 20000
                let loads = Array.init count (fun _ -> page.EnsureAsync ())
                replies[0].SetResult 3
                Expect.isTrue (finished (loads |> Array.map (fun load -> load :> Task))) "every waiter completed"

                Expect.isTrue
                    (loads
                     |> Array.forall (fun load -> load.Result = 3))
                    "every waiter holds the result"

                let first = page.RefreshAsync ()
                let pending = Array.init count (fun _ -> page.EnsureAsync ())

                let settled =
                    pending
                    |> Array.map (recordInto (ResizeArray<string>()) "pending")

                page.Dispose ()
                Expect.isTrue (finished settled) "every pending waiter completed"
                Expect.isTrue first.IsCanceled "the refresh waiter is cancelled"

                Expect.isTrue
                    (pending
                     |> Array.forall (fun load -> load.IsCanceled))
                    "every pending waiter is cancelled"

            testCase "one lease waits across successive requests"
            <| fun () ->
                use graph = newGraph ()
                use client = new QueryClient (graph)
                let mutable reply = TaskCompletionSource<int>()
                let family = client.Define (fun () _ -> reply.Task)
                use page = family.Acquire ()

                for round in 1..50 do
                    let loads = [| page.RefreshAsync (); page.EnsureAsync (); page.EnsureAsync () |]

                    let current = reply
                    reply <- TaskCompletionSource<int>()
                    current.SetResult round
                    Expect.isTrue (finished (loads |> Array.map (fun load -> load :> Task))) "the round's waiters completed"

                    Expect.isTrue
                        (loads
                         |> Array.forall (fun load -> load.Result = round))
                        "each waiter holds the round's result"

                Expect.equal page.Value 50 "the last result is published"

            testCase "invalidating a request without waiters cancels the request"
            <| fun () ->
                use graph = newGraph ()
                use client = new QueryClient (graph)
                let mutable token = CancellationToken.None

                let family =
                    client.Define (fun () ct ->
                        token <- ct
                        TaskCompletionSource<int>().Task)

                use page = family.Acquire ()
                Expect.isFalse token.IsCancellationRequested "the request is in flight"
                client.Commit [ family.Invalidate () ]
                Expect.isTrue token.IsCancellationRequested "invalidation retires the request"
                let late = page.EnsureAsync ()
                Expect.isFalse late.IsCompleted "a later wait joins a new request"

            testCase "RefreshAsync returns a cancelled task when a cancelled waiter continuation disposes the lease"
            <| fun () ->
                use graph = newGraph ()
                use client = new QueryClient (graph)
                let family = client.Define (fun () _ -> TaskCompletionSource<int>().Task)
                let lease = family.Acquire ()
                let first = lease.EnsureAsync ()

                let _cont =
                    first.ContinueWith (
                        (fun (_: Task<int>) -> lease.Dispose ()),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default
                    )

                let refreshed = lease.RefreshAsync ()
                Expect.isTrue refreshed.IsCanceled "reentrant lease disposal returns a cancelled task"
        ]
