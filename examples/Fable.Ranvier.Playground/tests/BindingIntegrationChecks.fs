module BindingIntegrationChecks

open System
open Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Binding
open Ranvier.View.Foundation.Scheduling
open Ranvier.View.Foundation.Mounting

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

[<Emit("document.createElement('div')")>]
let private host () : HTMLElement = jsNative

let private policy (statuses: ResizeArray<Reading<string>>) (errors: ResizeArray<string>) =
    {
        Equals = (=)
        Initial = Immediate
        Pending = fun () -> Some "loading"
        Failed = fun _ -> Some "failed"
        Status = fun reading -> statuses.Add reading
        Report = fun error -> errors.Add error.Message
    }

let private outcomes (ops: Operations<'view>) run queued =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal Reading.Pending)
    let statuses, errors = ResizeArray<Reading<string>>(), ResizeArray<string>()

    let view =
        Typed.div ops [] [ Typed.reactiveText ops (policy statuses errors) (fun () -> source.Value) ]

    let host = host ()
    let posts = ResizeArray<unit -> unit>()

    let mode =
        if queued then
            QueuedCommits (CollectAndContinue, posts.Add)
        else
            SynchronousCommits

    let mounted =
        mountWith mode graph None ignore host None (fun context -> run view context.Backend)

    equal "loading" host.textContent
    graph.Run (fun () -> source.Value <- Reading.Ready "ready")
    equal (if queued then "loading" else "ready") host.textContent
    mounted.Flush ()
    equal "ready" host.textContent
    graph.Run (fun () -> source.Value <- Reading.Failed (Exception "reader"))
    mounted.Flush ()
    equal "failed" host.textContent
    equal [ "reader" ] (List.ofSeq errors)
    graph.Run (fun () -> source.Value <- Reading.Ready "recovered")
    mounted.Flush ()
    equal "recovered" host.textContent
    equal 4 statuses.Count
    graph.Run (fun () -> source.Value <- Reading.Ready "disposed")
    mounted.Dispose ()
    let before = statuses.Count
    graph.Run (fun () -> source.Value <- Reading.Pending)

    for post in posts do
        post ()

    mounted.Flush ()
    equal before statuses.Count
    equal "" host.textContent

let private nested (ops: Operations<'view>) run =
    use graph = new Graph ()
    let rows = graph.Run (fun () -> Api.createSignal [ 1, "one"; 2, "two" ])
    let statuses, errors = ResizeArray<Reading<string>>(), ResizeArray<string>()
    let policy = policy statuses errors

    let view =
        Keyed.list ops (=) fst (fun () -> rows.Value) (fun current ->
            ops.Component (fun _ -> Typed.reactiveText ops policy (fun () -> Reading.Ready (snd (current ())))))

    let host = host ()
    let posts = ResizeArray<unit -> unit>()

    let mounted =
        mountWith (QueuedCommits (CollectAndContinue, posts.Add)) graph None ignore host None (fun context -> run view context.Backend)

    equal "onetwo" host.textContent
    graph.Run (fun () -> rows.Value <- [ 1, "ONE"; 2, "TWO" ])
    equal "onetwo" host.textContent
    mounted.Flush ()
    equal "ONETWO" host.textContent
    graph.Run (fun () -> rows.Value <- [ 1, "stale"; 2, "next" ])
    graph.Run (fun () -> rows.Value <- [ 2, "next" ])
    mounted.Flush ()
    equal "next" host.textContent
    mounted.Dispose ()

    for post in posts do
        post ()

    equal "" host.textContent

let private setupFailure (ops: Operations<'view>) run =
    use graph = new Graph ()
    let host = host ()
    let mutable cleaned, rejected = false, false
    let statuses, errors = ResizeArray<Reading<string>>(), ResizeArray<string>()

    let failing =
        { policy statuses errors with
            Status = fun _ -> failwith "status"
        }

    let view =
        ops.Component (fun setup ->
            setup.OnCleanup (fun () -> cleaned <- true)
            ops.BindWith failing (fun () -> Reading.Ready "value"))

    try
        mount graph None ignore host None (fun context -> run view context.Backend)
        |> ignore
    with error ->
        equal "status" error.Message
        rejected <- true

    equal (true, true, "") (rejected, cleaned, host.textContent)
    equal [ "status" ] (List.ofSeq errors)

let private independentMounts () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal (Reading.Ready "one"))
    let statuses, errors = ResizeArray<Reading<string>>(), ResizeArray<string>()
    let view = Tree.ops.BindWith (policy statuses errors) (fun () -> source.Value)
    let a, b = host (), host ()
    let posts = ResizeArray<unit -> unit>()

    let mount host =
        mountWith (QueuedCommits (CollectAndContinue, posts.Add)) graph None ignore host None (fun context -> Tree.run view context.Backend)

    let first, second = mount a, mount b
    graph.Run (fun () -> source.Value <- Reading.Ready "two")
    first.Dispose ()
    second.Flush ()
    equal ("", "two") (a.textContent, b.textContent)

    for post in posts do
        post ()

    second.Dispose ()

let private portable (ops: Operations<'view>) run =
    let clientOnly () =
        failwith "client callback ran on server"

    let policy =
        {
            Equals = (=)
            Initial = Scheduled
            Pending = clientOnly
            Failed = fun _ -> clientOnly ()
            Status = fun _ -> clientOnly ()
            Report = fun _ -> clientOnly ()
        }

    let mutable reading = Reading.Ready "<&"
    let view = ops.BindWith policy (fun () -> reading)

    let snapshot =
        function
        | Reading.Ready text -> Some text
        | Reading.Pending -> Some "<pending>"
        | Reading.Failed _ -> None

    equal "&lt;&amp;" (Html.renderBindings snapshot Reject (run view))
    reading <- Reading.Pending
    equal "&lt;pending&gt;" (Html.renderBindings snapshot Reject (run view))
    reading <- Reading.Failed (Exception "reader")
    equal "" (Html.renderBindings snapshot Reject (run view))
    let mutable rejected = false

    try
        Html.render Reject (run view) |> ignore
    with :? NotSupportedException ->
        rejected <- true

    equal true rejected

let private checkedSetter queued scheduled =
    use graph = new Graph ()
    let scope = Scope (ignore, ignore)
    let queue = Queue (graph, CollectAndContinue, ignore)
    let source = graph.Run (fun () -> Api.createSignal 1)
    let errors, writes = ResizeArray<string>(), ResizeArray<int>()
    let mutable reads, rejected = 0, false

    let policy =
        {
            Equals = (=)
            Initial = if scheduled then Scheduled else Immediate
            Pending = fun () -> None
            Failed = fun _ -> None
            Status = ignore
            Report = fun error -> errors.Add error.Message
        }

    try
        graph.Run (fun () ->
            startChecked
                scope.OnCleanup
                (fun () -> not scope.IsDisposed)
                (if queued then Queued queue else Synchronous)
                policy
                (fun () ->
                    reads <- reads + 1
                    Reading.Ready source.Value)
                (fun value -> if value = 1 then failwith "setter" else writes.Add value))
    with error ->
        equal "setter" error.Message
        rejected <- true

    let deferred = queued && scheduled
    equal (not deferred) rejected

    if deferred then
        queue.Flush ()

        equal
            [ "setter" ]
            (queue.Errors
             |> Array.map _.Message
             |> Array.toList)

    equal [ "setter" ] (List.ofSeq errors)
    graph.Run (fun () -> source.Value <- 2)
    queue.Flush ()
    equal (if deferred then 2 else 1) reads
    equal (if deferred then [ 2 ] else []) (List.ofSeq writes)
    scope.Dispose ()
    queue.Dispose ()

let portableCases =
    [|
        "tree outcome bindings require explicit escaped server snapshots", fun () -> portable Tree.ops Tree.run
        "program outcome bindings require explicit escaped server snapshots", fun () -> portable Program.ops Program.run
        "checked sync setters reject setup and release the binding", fun () -> checkedSetter false false
        "checked immediate queued setters reject setup and release the binding", fun () -> checkedSetter true false
        "checked scheduled setters report commit failure and recover", fun () -> checkedSetter true true
    |]

let cases =
    Array.append
        portableCases
        [|
            "tree sync outcome text integrates through the view backend", fun () -> outcomes Tree.ops Tree.run false
            "tree queued outcome text integrates through the view backend", fun () -> outcomes Tree.ops Tree.run true
            "program sync outcome text integrates through the view backend", fun () -> outcomes Program.ops Program.run false
            "program queued outcome text integrates through the view backend", fun () -> outcomes Program.ops Program.run true
            "tree keyed components share the mount queue and cancel removed rows", fun () -> nested Tree.ops Tree.run
            "program keyed components share the mount queue and cancel removed rows", fun () -> nested Program.ops Program.run
            "tree initial callback failure unwinds mount setup", fun () -> setupFailure Tree.ops Tree.run
            "program initial callback failure unwinds mount setup", fun () -> setupFailure Program.ops Program.run
            "mounts on a shared graph own independent commit queues", independentMounts
        |]
