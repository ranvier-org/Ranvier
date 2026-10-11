module BindingChecks

open System
open Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Scheduling
open Ranvier.View.Foundation.Binding
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation.Mounting

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

let private statusName =
    function
    | Reading.Ready _ -> "ready"
    | Reading.Pending -> "pending"
    | Reading.Failed error -> "failed:" + error.Message

let private fixture queued initial pending fallback =
    let graph = new Graph ()
    let scope = Scope (ignore, ignore)
    let posts = ResizeArray<unit -> unit>()
    let queue = Queue (graph, CollectAndContinue, posts.Add)
    let source = graph.Run (fun () -> Api.createSignal initial)

    let writes, statuses, errors =
        ResizeArray<int>(), ResizeArray<string>(), ResizeArray<string>()

    let mutable fail = false

    let policy =
        {
            Equals = (=)
            Initial = Immediate
            Pending = fun () -> pending
            Failed = fun _ -> fallback
            Status = fun reading -> statuses.Add (statusName reading)
            Report = fun error -> errors.Add error.Message
        }

    graph.Run (fun () ->
        start
            scope.OnCleanup
            (fun () -> not scope.IsDisposed)
            (if queued then Queued queue else Synchronous)
            policy
            (fun () -> source.Value)
            (fun value ->
                if fail then
                    failwith "write"

                writes.Add value))

    graph, scope, queue, posts, source, writes, statuses, errors, (fun value -> fail <- value)

let private outcomes queued =
    let graph, scope, queue, _, source, writes, statuses, errors, _ =
        fixture queued Reading.Pending (Some -1) (Some -2)

    use _graph = graph
    equal [ -1 ] (List.ofSeq writes)

    let update reading =
        graph.Run (fun () -> source.Value <- reading)

        if queued then
            queue.Flush ()

    update (Reading.Ready 7)
    update Reading.Pending
    update (Reading.Failed (Exception "read"))
    update (Reading.Ready 7)
    equal [ -1; 7; -1; -2; 7 ] (List.ofSeq writes)
    equal [ "pending"; "ready"; "pending"; "failed:read"; "ready" ] (List.ofSeq statuses)
    equal [ "read" ] (List.ofSeq errors)
    scope.Dispose ()
    queue.Dispose ()

let private retention queued =
    let graph, scope, queue, _, source, writes, _, errors, _ =
        fixture queued (Reading.Ready 7) None None

    use _graph = graph
    graph.Run (fun () -> source.Value <- Reading.Ready 8)
    graph.Run (fun () -> source.Value <- Reading.Pending)
    queue.Flush ()
    equal (if queued then [ 7 ] else [ 7; 8 ]) (List.ofSeq writes)
    graph.Run (fun () -> source.Value <- Reading.Failed (Exception "read"))
    queue.Flush ()
    equal [ "read" ] (List.ofSeq errors)
    scope.Dispose ()
    queue.Dispose ()

let private failedSetter queued =
    let graph, scope, queue, _, source, writes, _, errors, setFail =
        fixture queued (Reading.Ready 1) None None

    use _graph = graph
    setFail true
    graph.Run (fun () -> source.Value <- Reading.Ready 2)

    if queued then
        queue.Flush ()

    equal [ "write" ] (List.ofSeq errors)
    setFail false
    graph.Run (fun () -> source.Value <- Reading.Pending)
    graph.Run (fun () -> source.Value <- Reading.Ready 2)

    if queued then
        queue.Flush ()

    equal [ 1; 2 ] (List.ofSeq writes)
    scope.Dispose ()
    queue.Dispose ()

let private queuedWaves () =
    use graph = new Graph ()
    let posts = ResizeArray<unit -> unit>()
    let queue = Queue (graph, CollectAndContinue, posts.Add)
    let a, b = queue.Reserve (), queue.Reserve ()
    let writes = ResizeArray<int>()

    queue.Enqueue (
        a,
        fun () ->
            writes.Add 1
            queue.Enqueue (b, fun () -> writes.Add 3)
    )

    queue.Enqueue (b, fun () -> writes.Add 2)
    equal 1 posts.Count
    queue.Flush ()
    equal [ 1 ] (List.ofSeq writes)
    equal 2 posts.Count
    posts[0]()
    equal [ 1 ] (List.ofSeq writes)
    posts[1]()
    equal [ 1; 3 ] (List.ofSeq writes)
    queue.Dispose ()

let private queueFailure failure =
    use graph = new Graph ()
    let queue = Queue (graph, failure, ignore)
    let a, b = queue.Reserve (), queue.Reserve ()
    let writes = ResizeArray<int>()
    queue.Enqueue (a, fun () -> failwith "commit")
    queue.Enqueue (b, fun () -> writes.Add 2)
    queue.Flush ()
    equal (if failure = CollectAndContinue then [ 2 ] else []) (List.ofSeq writes)

    equal
        [ "commit" ]
        (queue.Errors
         |> Array.map _.Message
         |> Array.toList)

    queue.Flush ()
    equal (if failure = CollectAndContinue then [ 2 ] else []) (List.ofSeq writes)
    queue.Enqueue (b, fun () -> writes.Add 3)
    queue.Flush ()
    equal (if failure = CollectAndContinue then [ 2; 3 ] else [ 3 ]) (List.ofSeq writes)
    queue.Dispose ()

let private disposal queued =
    let graph, scope, queue, posts, source, writes, statuses, _, _ =
        fixture queued (Reading.Ready 1) None None

    use _graph = graph
    graph.Run (fun () -> source.Value <- Reading.Ready 2)
    scope.Dispose ()
    let before = statuses.Count
    graph.Run (fun () -> source.Value <- Reading.Ready 3)
    queue.Flush ()

    for post in posts do
        post ()

    equal (if queued then [ 1 ] else [ 1; 2 ]) (List.ofSeq writes)
    equal before statuses.Count
    queue.Dispose ()

let private untracked queued =
    use graph = new Graph ()
    let scope = Scope (ignore, ignore)
    let queue = Queue (graph, CollectAndContinue, ignore)
    let source, other = graph.Run (fun () -> Api.createSignal 1, Api.createSignal 0)
    let mutable writes, statuses = 0, 0

    let policy =
        {
            Equals =
                fun left right ->
                    other.Value |> ignore
                    left = right
            Initial = Immediate
            Pending = fun () -> None
            Failed = fun _ -> None
            Status =
                fun _ ->
                    other.Value |> ignore
                    statuses <- statuses + 1
            Report = ignore
        }

    graph.Run (fun () ->
        start
            scope.OnCleanup
            (fun () -> not scope.IsDisposed)
            (if queued then Queued queue else Synchronous)
            policy
            (fun () -> Reading.Ready source.Value)
            (fun _ ->
                other.Value |> ignore
                writes <- writes + 1))

    graph.Run (fun () -> source.Value <- 2)

    if queued then
        queue.Flush ()

    let before = writes, statuses
    graph.Run (fun () -> other.Value <- 1)
    queue.Flush ()
    equal before (writes, statuses)
    scope.Dispose ()
    queue.Dispose ()

let private recursiveFlush () =
    use graph = new Graph ()
    let queue = Queue (graph, CollectAndContinue, ignore)
    let mutable rejected = false

    queue.Enqueue (
        queue.Reserve (),
        fun () ->
            try
                queue.Flush ()
            with :? InvalidOperationException ->
                rejected <- true
    )

    queue.Flush ()
    equal true rejected
    queue.Dispose ()

let private snapshotDisposal () =
    use graph = new Graph ()
    let queue = Queue (graph, CollectAndContinue, ignore)
    let writes = ResizeArray<int>()

    queue.Enqueue (
        queue.Reserve (),
        fun () ->
            writes.Add 1
            queue.Dispose ()
    )

    queue.Enqueue (queue.Reserve (), fun () -> writes.Add 2)
    queue.Flush ()
    equal [ 1 ] (List.ofSeq writes)

let private initialAndEquality queued scheduled alwaysWrite =
    use graph = new Graph ()
    let scope = Scope (ignore, ignore)
    let queue = Queue (graph, CollectAndContinue, ignore)
    let source = graph.Run (fun () -> Api.createSignal (Reading.Ready 1))
    let writes = ResizeArray<int>()

    let policy =
        {
            Equals = if alwaysWrite then (fun _ _ -> false) else (=)
            Initial = if scheduled then Scheduled else Immediate
            Pending = fun () -> None
            Failed = fun _ -> None
            Status = ignore
            Report = ignore
        }

    graph.Run (fun () ->
        start
            scope.OnCleanup
            (fun () -> not scope.IsDisposed)
            (if queued then Queued queue else Synchronous)
            policy
            (fun () -> source.Value)
            writes.Add)

    equal (if queued && scheduled then [] else [ 1 ]) (List.ofSeq writes)
    queue.Flush ()
    graph.Run (fun () -> source.Value <- Reading.Pending)
    graph.Run (fun () -> source.Value <- Reading.Ready 1)
    queue.Flush ()
    equal (if alwaysWrite then [ 1; 1 ] else [ 1 ]) (List.ofSeq writes)
    scope.Dispose ()
    queue.Dispose ()

let private callbackDisposal queued comparer =
    use graph = new Graph ()
    let scope = Scope (ignore, ignore)
    let queue = Queue (graph, CollectAndContinue, ignore)
    let source = graph.Run (fun () -> Api.createSignal 1)
    let writes = ResizeArray<int>()

    let policy =
        {
            Equals =
                fun a b ->
                    if comparer then
                        scope.Dispose ()

                    a = b
            Initial = Immediate
            Pending = fun () -> None
            Failed = fun _ -> None
            Status =
                fun _ ->
                    if not comparer && source.Value = 2 then
                        scope.Dispose ()
            Report = ignore
        }

    graph.Run (fun () ->
        start
            scope.OnCleanup
            (fun () -> not scope.IsDisposed)
            (if queued then Queued queue else Synchronous)
            policy
            (fun () -> Reading.Ready source.Value)
            writes.Add)

    graph.Run (fun () -> source.Value <- 2)
    queue.Flush ()
    equal [ 1 ] (List.ofSeq writes)
    scope.Dispose ()
    queue.Dispose ()

let private registrationDisposal () =
    use graph = new Graph ()
    let mutable reads = 0
    let mutable rejected = false

    let policy =
        {
            Equals = (=)
            Initial = Immediate
            Pending = fun () -> None
            Failed = fun _ -> None
            Status = ignore
            Report = ignore
        }

    try
        graph.Run (fun () ->
            start
                (fun cleanup -> cleanup ())
                (fun () -> true)
                Synchronous
                policy
                (fun () ->
                    reads <- reads + 1
                    Reading.Ready 1)
                ignore)
    with :? InvalidOperationException ->
        rejected <- true

    equal (true, 0) (rejected, reads)

let private writeCleanup queued =
    use graph = new Graph ()
    let scope = Scope (ignore, ignore)
    let queue = Queue (graph, CollectAndContinue, ignore)
    let source = graph.Run (fun () -> Api.createSignal 1)
    let mutable cleanups = 0

    let policy =
        {
            Equals = (=)
            Initial = Immediate
            Pending = fun () -> None
            Failed = fun _ -> None
            Status = ignore
            Report = ignore
        }

    graph.Run (fun () ->
        start
            scope.OnCleanup
            (fun () -> not scope.IsDisposed)
            (if queued then Queued queue else Synchronous)
            policy
            (fun () -> Reading.Ready source.Value)
            (fun _ -> Api.onCleanup (fun () -> cleanups <- cleanups + 1)))

    graph.Run (fun () -> source.Value <- 2)
    equal 1 cleanups
    queue.Flush ()
    scope.Dispose ()
    equal 2 cleanups
    queue.Dispose ()

let private reporterFailure () =
    use graph = new Graph ()
    let scope = Scope (ignore, ignore)
    let queue = Queue (graph, CollectAndContinue, ignore)

    let policy =
        {
            Equals = (=)
            Initial = Scheduled
            Pending = fun () -> None
            Failed = fun _ -> None
            Status = ignore
            Report = fun _ -> failwith "reporter"
        }

    graph.Run (fun () ->
        start scope.OnCleanup (fun () -> not scope.IsDisposed) (Queued queue) policy (fun () -> Reading.Ready 1) (fun _ -> failwith "setter"))

    queue.Flush ()
    equal 1 queue.Errors.Length
    equal true (queue.Errors[0].Message.Contains "setter")
    equal true (queue.Errors[0].Message.Contains "reporter")
    scope.Dispose ()
    queue.Dispose ()

[<Emit("document.createElement('div')")>]
let private host () : HTMLElement = jsNative

let private nativeBinding queued =
    use graph = new Graph ()
    let queue = Queue (graph, CollectAndContinue, ignore)
    let source = graph.Run (fun () -> Api.createSignal (Reading.Ready "first"))
    let host, node = host (), host ()

    let policy =
        {
            Equals = (=)
            Initial = Immediate
            Pending = fun () -> Some "loading"
            Failed = fun _ -> Some "error"
            Status = ignore
            Report = ignore
        }

    let mounted =
        mount graph None ignore host None (fun context ->
            context.Native (node, Owned)

            start
                context.OnCleanup
                (fun () -> context.Phase () <> ScopePhase.Disposed)
                (if queued then Queued queue else Synchronous)
                policy
                (fun () -> source.Value)
                (fun value -> node.textContent <- value))

    equal "first" node.textContent
    graph.Run (fun () -> source.Value <- Reading.Pending)
    queue.Flush ()
    equal "loading" node.textContent
    graph.Run (fun () -> source.Value <- Reading.Failed (Exception "read"))
    queue.Flush ()
    equal "error" node.textContent
    graph.Run (fun () -> source.Value <- Reading.Ready "last")
    mounted.Dispose ()
    queue.Flush ()
    equal (if queued then "error" else "last") node.textContent
    equal None node.parentNode
    queue.Dispose ()

type MicrotaskFixture =
    {
        Update: int -> unit
        Values: unit -> int array
        Dispose: unit -> unit
    }

let microtaskFixture () =
    let graph = new Graph ()
    let scope = Scope (ignore, ignore)
    let queue = Microtask.create graph CollectAndContinue
    let source = graph.Run (fun () -> Api.createSignal 1)
    let writes = ResizeArray<int>()

    let policy =
        {
            Equals = (=)
            Initial = Scheduled
            Pending = fun () -> None
            Failed = fun _ -> None
            Status = ignore
            Report = ignore
        }

    graph.Run (fun () ->
        start scope.OnCleanup (fun () -> not scope.IsDisposed) (Queued queue) policy (fun () -> Reading.Ready source.Value) writes.Add)

    {
        Update = fun value -> graph.Run (fun () -> source.Value <- value)
        Values = fun () -> writes.ToArray ()
        Dispose =
            fun () ->
                scope.Dispose ()
                queue.Dispose ()
                graph.Dispose ()
    }

let private coreStatus queued =
    use graph = new Graph ()
    let scope = Scope (ignore, ignore)
    let queue = Queue (graph, CollectAndContinue, ignore)
    let source = graph.Run (fun () -> Api.createAsyncSource<int>())
    let writes, statuses = ResizeArray<int>(), ResizeArray<string>()

    let policy =
        {
            Equals = (=)
            Initial = Immediate
            Pending = fun () -> Some -1
            Failed = fun _ -> Some -2
            Status = fun reading -> statuses.Add (statusName reading)
            Report = ignore
        }

    graph.Run (fun () ->
        start
            scope.OnCleanup
            (fun () -> not scope.IsDisposed)
            (if queued then Queued queue else Synchronous)
            policy
            (fun () -> source.TryValue)
            writes.Add)

    equal [ "pending" ] (List.ofSeq statuses)
    graph.Run (fun () -> source.Settle 7)
    queue.Flush ()
    equal [ "pending"; "ready" ] (List.ofSeq statuses)
    equal [ -1; 7 ] (List.ofSeq writes)
    scope.Dispose ()
    queue.Dispose ()

let private equalityLayers () =
    use graph = new Graph ()
    let scope = Scope (ignore, ignore)
    let source = graph.Run (fun () -> Api.createSignal 1)
    let mutable computes, reads = 0, 0

    let memo =
        graph.Run (fun () ->
            Api.createMemo (fun _ ->
                computes <- computes + 1
                source.Value / 2))

    let writes = ResizeArray<int>()

    let policy =
        {
            Equals = fun a b -> a % 2 = b % 2
            Initial = Immediate
            Pending = fun () -> None
            Failed = fun _ -> None
            Status = ignore
            Report = ignore
        }

    graph.Run (fun () ->
        start
            scope.OnCleanup
            (fun () -> not scope.IsDisposed)
            Synchronous
            policy
            (fun () ->
                reads <- reads + 1
                memo.TryValue)
            writes.Add)

    let initial = computes, reads
    graph.Run (fun () -> source.Value <- 1)
    equal initial (computes, reads)
    graph.Run (fun () -> source.Value <- 0)
    equal (fst initial + 1, snd initial) (computes, reads)
    graph.Run (fun () -> source.Value <- 4)
    equal (fst initial + 2, snd initial + 1) (computes, reads)
    equal [ 0 ] (List.ofSeq writes)
    graph.Run (fun () -> source.Value <- 6)
    equal [ 0; 3 ] (List.ofSeq writes)
    scope.Dispose ()

let private bindingCommitFailure failure =
    use graph = new Graph ()
    let scope = Scope (ignore, ignore)
    let queue = Queue (graph, failure, ignore)
    let source = graph.Run (fun () -> Api.createSignal 1)
    let writes, errors = ResizeArray<int>(), ResizeArray<string>()

    let policy =
        {
            Equals = (=)
            Initial = Scheduled
            Pending = fun () -> None
            Failed = fun _ -> None
            Status = ignore
            Report = fun error -> errors.Add error.Message
        }

    graph.Run (fun () ->
        start scope.OnCleanup (fun () -> not scope.IsDisposed) (Queued queue) policy (fun () -> Reading.Ready source.Value) (fun value ->
            if value = 1 then
                failwith "setter")

        start scope.OnCleanup (fun () -> not scope.IsDisposed) (Queued queue) policy (fun () -> Reading.Ready source.Value) writes.Add)

    queue.Flush ()
    equal [ "setter" ] (List.ofSeq errors)
    equal (if failure = CollectAndContinue then [ 1 ] else []) (List.ofSeq writes)
    graph.Run (fun () -> source.Value <- 2)
    queue.Flush ()
    equal (if failure = CollectAndContinue then [ 1; 2 ] else [ 2 ]) (List.ofSeq writes)
    scope.Dispose ()
    queue.Dispose ()

let private initialError queued =
    let graph, scope, queue, _, source, writes, statuses, errors, _ =
        fixture queued (Reading.Failed (Exception "initial")) None (Some -2)

    use _graph = graph
    equal [ -2 ] (List.ofSeq writes)
    equal [ "failed:initial" ] (List.ofSeq statuses)
    equal [ "initial" ] (List.ofSeq errors)
    graph.Run (fun () -> source.Value <- Reading.Ready 4)
    queue.Flush ()
    equal [ -2; 4 ] (List.ofSeq writes)
    scope.Dispose ()
    queue.Dispose ()

let private failFastReplacements () =
    use graph = new Graph ()
    let queue = Queue (graph, FailFastCancel, ignore)
    let a, b, c = queue.Reserve (), queue.Reserve (), queue.Reserve ()
    let writes = ResizeArray<int>()

    queue.Enqueue (
        a,
        fun () ->
            queue.Enqueue (b, fun () -> writes.Add 3)
            queue.Enqueue (c, fun () -> writes.Add 4)
            failwith "commit"
    )

    queue.Enqueue (b, fun () -> writes.Add 2)
    queue.Flush ()
    equal [] (List.ofSeq writes)
    queue.Flush ()
    equal [ 4 ] (List.ofSeq writes)
    queue.Dispose ()

let private flushInsideBatch () =
    let graph, scope, queue, _, source, writes, _, _, _ =
        fixture true (Reading.Ready 1) None None

    use _graph = graph

    graph.Run (fun () ->
        Api.batch (fun () ->
            source.Value <- Reading.Ready 2
            queue.Flush ()
            equal [ 1; 2 ] (List.ofSeq writes)))

    scope.Dispose ()
    queue.Dispose ()

let private retainedCleanup queued =
    use graph = new Graph ()
    let scope = Scope (ignore, ignore)
    let queue = Queue (graph, CollectAndContinue, ignore)
    let source = graph.Run (fun () -> Api.createSignal (Reading.Ready 1))
    let mutable writes, cleanups = 0, 0

    let policy =
        {
            Equals = (=)
            Initial = Immediate
            Pending = fun () -> None
            Failed = fun _ -> None
            Status = ignore
            Report = ignore
        }

    graph.Run (fun () ->
        start
            scope.OnCleanup
            (fun () -> not scope.IsDisposed)
            (if queued then Queued queue else Synchronous)
            policy
            (fun () -> source.Value)
            (fun _ ->
                writes <- writes + 1
                Api.onCleanup (fun () -> cleanups <- cleanups + 1)))

    graph.Run (fun () -> source.Value <- Reading.Pending)
    equal (1, 1) (writes, cleanups)
    graph.Run (fun () -> source.Value <- Reading.Ready 1)
    queue.Flush ()
    equal (1, 1) (writes, cleanups)
    scope.Dispose ()
    equal (1, 1) (writes, cleanups)
    queue.Dispose ()

let private reentrantCommit () =
    use graph = new Graph ()
    let scope = Scope (ignore, ignore)
    let queue = Queue (graph, CollectAndContinue, ignore)
    let source = graph.Run (fun () -> Api.createSignal 1)
    let writes = ResizeArray<string>()

    let policy =
        {
            Equals = (=)
            Initial = Scheduled
            Pending = fun () -> None
            Failed = fun _ -> None
            Status = ignore
            Report = ignore
        }

    graph.Run (fun () ->
        start scope.OnCleanup (fun () -> not scope.IsDisposed) (Queued queue) policy (fun () -> Reading.Ready source.Value) (fun value ->
            writes.Add ("a" + string value)

            if value = 1 then
                source.Value <- 2
                Api.flush ())

        start scope.OnCleanup (fun () -> not scope.IsDisposed) (Queued queue) policy (fun () -> Reading.Ready source.Value) (fun value ->
            writes.Add ("b" + string value)))

    queue.Flush ()
    equal [ "a1" ] (List.ofSeq writes)
    queue.Flush ()
    equal [ "a2"; "b2" ] (writes |> Seq.skip 1 |> Seq.sort |> List.ofSeq)
    scope.Dispose ()
    queue.Dispose ()

let pureCases =
    [|
        "synchronous bindings expose pending failed and recovered outcomes", fun () -> outcomes false
        "queued bindings expose pending failed and recovered outcomes", fun () -> outcomes true
        "synchronous retention is explicitly selected", fun () -> retention false
        "pending outcomes cancel stale queued ready writes", fun () -> retention true
        "synchronous failed setters do not advance committed equality", fun () -> failedSetter false
        "queued failed setters do not advance committed equality", fun () -> failedSetter true
        "queue snapshots invalidate replaced work and schedule another wave", queuedWaves
        "collect-and-continue commits preserve later work", fun () -> queueFailure CollectAndContinue
        "fail-fast cancels remaining snapshot work without implicit retries", fun () -> queueFailure FailFastCancel
        "synchronous disposal stops binding status and reads", fun () -> disposal false
        "queued disposal cancels pending writes and tickets", fun () -> disposal true
        "synchronous status comparer and write callbacks remain untracked", fun () -> untracked false
        "queued status comparer and write callbacks remain untracked", fun () -> untracked true
        "recursive commit flushing is rejected", recursiveFlush
        "disposal midway through a commit cancels the remaining snapshot", snapshotDisposal
        "sync initial scheduling and committed equality are explicit", fun () -> initialAndEquality false true false
        "queued initial writes can be scheduled", fun () -> initialAndEquality true true false
        "queued initial writes can be immediate", fun () -> initialAndEquality true false false
        "sync bindings can select always-write equality", fun () -> initialAndEquality false false true
        "queued bindings can select always-write equality", fun () -> initialAndEquality true false true
        "sync status disposal prevents the write", fun () -> callbackDisposal false false
        "queued status disposal prevents the write", fun () -> callbackDisposal true false
        "sync comparer disposal prevents the write", fun () -> callbackDisposal false true
        "queued comparer disposal prevents the write", fun () -> callbackDisposal true true
        "cleanup registration disposal rejects binding before reading", registrationDisposal
        "sync writes own per-run cleanup", fun () -> writeCleanup false
        "queued writes own per-run cleanup", fun () -> writeCleanup true
        "reporter failures preserve both error messages", reporterFailure
        "sync bindings observe core pending without split effect suppression", fun () -> coreStatus false
        "queued bindings observe core pending without split effect suppression", fun () -> coreStatus true
        "source memo and binding equality suppress different layers", equalityLayers
        "binding setter failures collect and continue explicitly", fun () -> bindingCommitFailure CollectAndContinue
        "binding setter failures cancel the wave explicitly", fun () -> bindingCommitFailure FailFastCancel
        "sync initial error fallback recovers explicitly", fun () -> initialError false
        "queued initial error fallback recovers explicitly", fun () -> initialError true
        "fail-fast cancels replacements for skipped slots and preserves independent next-wave work", failFastReplacements
        "manual commit flush settles graph work inside a batch", flushInsideBatch
        "sync retained output keeps per-reader-run cleanup semantics", fun () -> retainedCleanup false
        "queued retained output keeps per-reader-run cleanup semantics", fun () -> retainedCleanup true
        "reentrant graph flush invalidates stale queued bindings", reentrantCommit
    |]

let cases =
    Array.append
        pureCases
        [|
            "native synchronous binding follows mount lifetime", fun () -> nativeBinding false
            "native queued binding cancels writes after mount disposal", fun () -> nativeBinding true
        |]
