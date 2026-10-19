module BoundaryChecks

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

/// <summary>Renders a boundary over one async source as HTML while it is pending, settled and failed.</summary>
let private server (ops: Operations<'view>) (run: 'view -> Backend -> unit) =
    use graph = new Graph ()

    graph.Run (fun () ->
        Api.createRoot (fun _ ->
            let source = Api.createAsyncSource<string>()

            let view =
                ops.Boundary (
                    ops.Text "<loading>",
                    (fun failure -> ops.Text failure.Error.Message),
                    fun _ -> Typed.div ops [||] [| Typed.reactiveText ops (fun () -> source.Value) |]
                )

            let render () =
                Html.render Reject (run view)

            equal "<!--[-->&lt;loading&gt;<!--]-->" (render ())
            source.Settle "<&"
            equal "<!--[--><div>&lt;&amp;</div><!--]-->" (render ())
            source.Fail (Exception "<failed>")
            equal "<!--[-->&lt;failed&gt;<!--]-->" (render ())))

let private host () : HTMLElement =
    globalThis.document.createElement "div"

let private modeOf queued =
    if queued then
        QueuedCommits (CollectAndContinue, ignore)
    else
        SynchronousCommits

/// <summary>Counts the node's own <c>insertBefore</c> calls; the returned function restores it and returns the count.</summary>
[<Emit("(() => { let count = 0; const old = $0.insertBefore; $0.insertBefore = function(n, b) { count++; return old.call(this, n, b); }; return () => { $0.insertBefore = old; return count; }; })()")>]
let private countInserts (node: Node) : unit -> int = jsNative

let private strict (errors: exn list) =
    failwithf "Unexpected report %A" [ for error in errors -> error.Message ]

/// <summary>
/// A boundary whose content holds a text on one source, keyed rows reading a second source, and an arm reading the
/// first. The content reveals in one insertion once both sources settle.
/// </summary>
let private initialReveal (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let first = graph.Run (fun () -> Api.createAsyncSource<string>())
    let second = graph.Run (fun () -> Api.createAsyncSource<string>())
    let log = ResizeArray<string>()
    let host = host ()

    let content (_: BoundaryStatus) =
        ops.Component (fun setup ->
            log.Add "content"
            setup.OnCleanup (fun () -> log.Add "content cleanup")

            ops.Fragment
                [|
                    Typed.reactiveText ops (fun () -> first.Value)
                    Keyed.list ops id (fun () -> [ 1; 2 ]) (fun current -> Typed.reactiveText ops (fun () -> second.Value + string (current ())))
                    Branch.select ops id (fun () -> 0) (fun _ -> Typed.reactiveText ops (fun () -> "|" + first.Value))
                |])

    let pending =
        ops.Component (fun setup ->
            log.Add "pending"
            setup.OnCleanup (fun () -> log.Add "pending cleanup")
            ops.Text "loading")

    let view =
        ops.Boundary (pending, (fun failure -> ops.Text failure.Error.Message), content)

    let mounted =
        mountWith (modeOf queued) graph None strict host None (fun context -> run view context.Backend)

    equal ("loading", [ "content"; "pending" ]) (host.textContent, List.ofSeq log)
    let inserts = countInserts host
    graph.Run (fun () -> first.Settle "A")
    mounted.Flush ()
    equal "loading" host.textContent
    graph.Run (fun () -> second.Settle "B")
    mounted.Flush ()
    equal ("AB1B2|A", 1) (host.textContent, inserts ())
    equal [ "content"; "pending"; "pending cleanup" ] (List.ofSeq log)
    mounted.Dispose ()
    equal ([ "content"; "pending"; "pending cleanup"; "content cleanup" ], "") (List.ofSeq log, host.textContent)

/// <summary>An inner boundary inside an outer boundary's content; each reveals when its own source settles.</summary>
let private nestedReveal (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued innerFirst =
    use graph = new Graph ()
    let outer = graph.Run (fun () -> Api.createAsyncSource<string>())
    let inner = graph.Run (fun () -> Api.createAsyncSource<string>())
    let host = host ()

    let failed (failure: BoundaryFailure) =
        ops.Text failure.Error.Message

    let innerView =
        ops.Boundary (ops.Text "[inner loading]", failed, (fun _ -> Typed.reactiveText ops (fun () -> inner.Value)))

    let view =
        ops.Boundary (ops.Text "[outer loading]", failed, fun _ -> ops.Fragment [| Typed.reactiveText ops (fun () -> outer.Value); innerView |])

    let mounted =
        mountWith (modeOf queued) graph None strict host None (fun context -> run view context.Backend)

    equal "[outer loading]" host.textContent

    if innerFirst then
        graph.Run (fun () -> inner.Settle "B")
        mounted.Flush ()
        equal "[outer loading]" host.textContent
        graph.Run (fun () -> outer.Settle "A")
        mounted.Flush ()
    else
        graph.Run (fun () -> outer.Settle "A")
        mounted.Flush ()
        equal "A[inner loading]" host.textContent
        graph.Run (fun () -> inner.Settle "B")
        mounted.Flush ()

    equal "AB" host.textContent
    mounted.Dispose ()

/// <summary>Disposes a mount while its boundary is pending, then settles the source.</summary>
let private disposedWhilePending (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createAsyncSource<string>())
    let log = ResizeArray<string>()
    let host = host ()

    let logged name (view: 'view) =
        ops.Component (fun setup ->
            log.Add name
            setup.OnCleanup (fun () -> log.Add (name + " cleanup"))
            view)

    let view =
        ops.Boundary (
            logged "pending" (ops.Text "loading"),
            (fun failure -> ops.Text failure.Error.Message),
            fun _ -> logged "content" (Typed.reactiveText ops (fun () -> source.Value))
        )

    let mounted =
        mountWith (modeOf queued) graph None strict host None (fun context -> run view context.Backend)

    let inserts = countInserts host
    mounted.Dispose ()
    equal ([ "content"; "pending"; "pending cleanup"; "content cleanup" ], 0.) (List.ofSeq log, host.childNodes.length)
    graph.Run (fun () -> source.Settle "late")
    mounted.Flush ()
    equal (4, 0., 0) (log.Count, host.childNodes.length, inserts ())

/// <summary>Moves a mount to another host while its boundary is pending; the content reveals there and handles clicks.</summary>
let private movedWhilePending (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createAsyncSource<string>())
    let mutable clicks = 0
    let first, second = host (), host ()

    let view =
        ops.Boundary (
            ops.Text "loading",
            (fun failure -> ops.Text failure.Error.Message),
            fun _ ->
                Typed.button ops [| Typed.listener "click" (fun _ -> clicks <- clicks + 1) |] [| Typed.reactiveText ops (fun () -> source.Value) |]
        )

    let mounted =
        mountWith (modeOf queued) graph None strict first None (fun context -> run view context.Backend)

    mounted.MoveBefore (second, None)
    graph.Run (fun () -> source.Settle "moved")
    mounted.Flush ()
    equal ("", "moved") (first.textContent, second.textContent)

    (second.querySelector "button"
     |> unbox<HTMLButtonElement>)
        .click()

    equal 1 clicks
    mounted.Dispose ()

/// <summary>Removes the one row still pending before the reveal; the boundary reveals the remaining rows.</summary>
let private removedPendingRow (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let never = graph.Run (fun () -> Api.createAsyncSource<string>())
    let other = graph.Run (fun () -> Api.createAsyncSource<string>())
    let rows = graph.Run (fun () -> Api.createSignal [ 1; 2 ])
    let host = host ()

    let view =
        ops.Boundary (
            ops.Text "loading",
            (fun failure -> ops.Text failure.Error.Message),
            fun _ ->
                Keyed.list ops id (fun () -> rows.Value) (fun current ->
                    Typed.reactiveText ops (fun () -> if current () = 2 then never.Value else other.Value))
        )

    let mounted =
        mountWith (modeOf queued) graph None strict host None (fun context -> run view context.Backend)

    graph.Run (fun () -> other.Settle "x")
    mounted.Flush ()
    equal "loading" host.textContent
    graph.Run (fun () -> rows.Value <- [ 1 ])
    mounted.Flush ()
    equal "x" host.textContent
    mounted.Dispose ()

let cases: (string * (unit -> unit)) array =
    [|
        "tree HTML renders a boundary's pending, content or error view between markers", fun () -> server Tree.ops Tree.run
        "program HTML renders a boundary's pending, content or error view between markers", fun () -> server Program.ops Program.run
        "tree synchronous: a boundary reveals its content in one insertion once every binding, row and arm has a value",
        fun () -> initialReveal Tree.ops Tree.run false
        "tree queued: a boundary reveals its content in one insertion once every binding, row and arm has a value",
        fun () -> initialReveal Tree.ops Tree.run true
        "program synchronous: a boundary reveals its content in one insertion once every binding, row and arm has a value",
        fun () -> initialReveal Program.ops Program.run false
        "program queued: a boundary reveals its content in one insertion once every binding, row and arm has a value",
        fun () -> initialReveal Program.ops Program.run true
        "tree synchronous: a nested boundary reveals after its outer boundary", fun () -> nestedReveal Tree.ops Tree.run false false
        "program queued: a nested boundary reveals after its outer boundary", fun () -> nestedReveal Program.ops Program.run true false
        "tree queued: a nested boundary revealed first appears with its outer boundary", fun () -> nestedReveal Tree.ops Tree.run true true
        "program synchronous: a nested boundary revealed first appears with its outer boundary",
        fun () -> nestedReveal Program.ops Program.run false true
        "tree synchronous: disposal during the pending phase disposes the hidden content and the pending view",
        fun () -> disposedWhilePending Tree.ops Tree.run false
        "program queued: disposal during the pending phase disposes the hidden content and the pending view",
        fun () -> disposedWhilePending Program.ops Program.run true
        "tree queued: a boundary moved while pending reveals at its new parent and handles clicks there",
        fun () -> movedWhilePending Tree.ops Tree.run true
        "program synchronous: a boundary moved while pending reveals at its new parent and handles clicks there",
        fun () -> movedWhilePending Program.ops Program.run false
        "tree synchronous: removing the last pending row reveals the boundary", fun () -> removedPendingRow Tree.ops Tree.run false
        "program queued: removing the last pending row reveals the boundary", fun () -> removedPendingRow Program.ops Program.run true
    |]
