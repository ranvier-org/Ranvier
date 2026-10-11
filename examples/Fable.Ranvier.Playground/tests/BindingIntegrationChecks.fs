module BindingIntegrationChecks

open System
open Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Scheduling
open Ranvier.View.Foundation.Mounting

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

[<Emit("document.createElement('div')")>]
let private host () : HTMLElement = jsNative

[<Emit("(() => { const original = document.createTextNode; document.createTextNode = function(value) { const node = original.call(this, value); Object.defineProperty(node, 'textContent', { set() { throw new Error('setter'); }, configurable: true }); return node; }; return () => { document.createTextNode = original; }; })()")>]
let private failTextWrites () : unit -> unit = jsNative

let private setupFailure queued =
    use graph = new Graph ()
    let host = host ()
    let mutable cleaned, rejected = false, false
    let restore = failTextWrites ()

    try
        let mode =
            if queued then
                QueuedCommits (CollectAndContinue, ignore)
            else
                SynchronousCommits

        let view =
            Tree.ops.Component (fun setup ->
                setup.OnCleanup (fun () -> cleaned <- true)
                Typed.reactiveText Tree.ops (fun () -> "value"))

        try
            mountWith mode graph None ignore host None (fun context -> Tree.run view context.Backend)
            |> ignore
        with error ->
            equal "setter" error.Message
            rejected <- true

        equal (true, true, "") (rejected, cleaned, host.textContent)
    finally
        restore ()

let private boundary (ops: Operations<'view>) run queued =
    use graph = new Graph ()

    let flight =
        graph.Run (fun () -> Api.createSignal (Api.createAsyncSource<string>()))

    let mutable pending, failed, bodies = 0, 0, 0
    let host = host ()

    let mode =
        if queued then
            QueuedCommits (CollectAndContinue, ignore)
        else
            SynchronousCommits

    let view =
        ops.Component (fun _ ->
            let result =
                Api.createBoundary
                    (fun _ ->
                        pending <- pending + 1
                        "loading")
                    (fun _ _ ->
                        failed <- failed + 1
                        "failed")
                    (fun () ->
                        bodies <- bodies + 1
                        flight.Value.Value)

            Typed.div
                ops
                []
                [
                    Typed.reactiveText ops (fun () -> result.Value)
                    Typed.reactiveText ops (fun () -> result.Value)
                ])

    let mounted =
        mountWith mode graph None ignore host None (fun context -> run view context.Backend)

    equal "loadingloading" host.textContent
    let nodes = host.firstChild.Value.childNodes
    let first, second = nodes.item 0., nodes.item 1.
    equal (1, 0, 1) (pending, failed, bodies)
    graph.Run (fun () -> flight.Value.Settle "ready")

    if queued then
        equal "loadingloading" host.textContent

    mounted.Flush ()
    equal "readyready" host.textContent
    graph.Run (fun () -> flight.Value <- Api.createAsyncSource<string>())
    mounted.Flush ()
    equal "loadingloading" host.textContent
    graph.Run (fun () -> flight.Value.Fail (Exception "reader"))
    mounted.Flush ()
    equal "failedfailed" host.textContent
    equal (2, 1, 4) (pending, failed, bodies)
    graph.Run (fun () -> flight.Value.Settle "recovered")
    mounted.Flush ()
    equal "recoveredrecovered" host.textContent
    equal true (obj.ReferenceEquals (first, nodes.item 0.))
    equal true (obj.ReferenceEquals (second, nodes.item 1.))
    graph.Run (fun () -> flight.Value.Settle "stale")
    mounted.Dispose ()
    let before = bodies
    graph.Run (fun () -> flight.Value.Fail (Exception "disposed"))
    mounted.Flush ()
    equal before bodies
    equal "" host.textContent

let private nested (ops: Operations<'view>) run =
    use graph = new Graph ()
    let rows = graph.Run (fun () -> Api.createSignal [ 1, "one"; 2, "two" ])

    let view =
        Keyed.list ops (=) fst (fun () -> rows.Value) (fun current -> ops.Component (fun _ -> Typed.reactiveText ops (fun () -> snd (current ()))))

    let host = host ()

    let mounted =
        mountWith (QueuedCommits (CollectAndContinue, ignore)) graph None ignore host None (fun context -> run view context.Backend)

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

let private independentMounts () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "one")
    let view = Typed.reactiveText Tree.ops (fun () -> source.Value)
    let a, b = host (), host ()

    let attach host =
        mountWith (QueuedCommits (CollectAndContinue, ignore)) graph None ignore host None (fun context -> Tree.run view context.Backend)

    let first, second = attach a, attach b
    graph.Run (fun () -> source.Value <- "two")
    first.Dispose ()
    second.Flush ()
    equal ("", "two") (a.textContent, b.textContent)
    second.Dispose ()

let private serverBoundary (ops: Operations<'view>) run =
    use graph = new Graph ()

    graph.Run (fun () ->
        Api.createRoot (fun _ ->
            let source = Api.createAsyncSource<string>()

            let result =
                Api.createBoundary (fun _ -> "<pending>") (fun ex _ -> ex.Message) (fun () -> source.Value)

            let view = Typed.reactiveText ops (fun () -> result.Value)
            equal "&lt;pending&gt;" (Html.render Reject (run view))
            source.Settle "<&"
            equal "&lt;&amp;" (Html.render Reject (run view))
            source.Fail (Exception "<failed>")
            equal "&lt;failed&gt;" (Html.render Reject (run view))))

let portableCases =
    [|
        "tree HTML serializes the boundary's escaped result", fun () -> serverBoundary Tree.ops Tree.run
        "program HTML serializes the boundary's escaped result", fun () -> serverBoundary Program.ops Program.run
    |]

let cases =
    Array.append
        portableCases
        [|
            "initial synchronous DOM setter failure unwinds setup", fun () -> setupFailure false
            "initial queued DOM setter failure unwinds setup", fun () -> setupFailure true
            "tree synchronous text shares core pending/error boundary", fun () -> boundary Tree.ops Tree.run false
            "tree queued text shares core pending/error boundary", fun () -> boundary Tree.ops Tree.run true
            "program synchronous text shares core pending/error boundary", fun () -> boundary Program.ops Program.run false
            "program queued text shares core pending/error boundary", fun () -> boundary Program.ops Program.run true
            "tree keyed components share queue and cancel removed rows", fun () -> nested Tree.ops Tree.run
            "program keyed components share queue and cancel removed rows", fun () -> nested Program.ops Program.run
            "mounts own independent queues on a shared graph", independentMounts
        |]
