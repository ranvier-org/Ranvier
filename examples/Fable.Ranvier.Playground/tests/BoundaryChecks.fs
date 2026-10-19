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

let private messages (reports: ResizeArray<exn list>) =
    [ for errors in reports -> errors |> List.map _.Message ]

/// <summary>
/// Mounts <c>content</c> under a boundary whose error view shows the failure's message. The content and error views log
/// their setup and cleanup.
/// </summary>
let private underBoundary
    (ops: Operations<'view>)
    (run: 'view -> Backend -> unit)
    queued
    (graph: Graph)
    (host: HTMLElement)
    (log: ResizeArray<string>)
    (reports: ResizeArray<exn list>)
    (content: unit -> 'view)
    =
    let view =
        ops.Boundary (
            ops.Text "loading",
            (fun failure ->
                ops.Component (fun setup ->
                    log.Add ("error " + failure.Error.Message)
                    setup.OnCleanup (fun () -> log.Add "error cleanup")
                    ops.Text ("failed: " + failure.Error.Message))),
            fun _ ->
                ops.Component (fun setup ->
                    log.Add "content"
                    setup.OnCleanup (fun () -> log.Add "content cleanup")
                    content ())
        )

    mountWith (modeOf queued) graph None reports.Add host None (fun context -> run view context.Backend)

let private readerFailure (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "ok")
    let host, log, reports = host (), ResizeArray<string>(), ResizeArray<exn list>()

    let mounted =
        underBoundary ops run queued graph host log reports (fun () ->
            Typed.reactiveText ops (fun () ->
                if source.Value = "bad" then
                    failwith "reader"
                else
                    source.Value))

    equal "ok" host.textContent
    graph.Run (fun () -> source.Value <- "bad")
    mounted.Flush ()

    let expected =
        "failed: reader", [ "content"; "content cleanup"; "error reader" ], [ [ "reader" ] ]

    equal expected (host.textContent, List.ofSeq log, messages reports)
    graph.Run (fun () -> source.Value <- "bad again")
    mounted.Flush ()
    equal expected (host.textContent, List.ofSeq log, messages reports)
    mounted.Dispose ()

/// <summary>A content binding whose reader raises on its first read, during the content's setup.</summary>
let private initialReaderFailure (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let host, log, reports = host (), ResizeArray<string>(), ResizeArray<exn list>()

    let mounted =
        underBoundary ops run queued graph host log reports (fun () -> Typed.reactiveText ops (fun () -> failwith "initial"))

    mounted.Flush ()

    equal
        ("failed: initial", [ "content"; "content cleanup"; "error initial" ], [ [ "initial" ] ])
        (host.textContent, List.ofSeq log, messages reports)

    mounted.Dispose ()

let private setterFailure (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "ok")
    let host, log, reports = host (), ResizeArray<string>(), ResizeArray<exn list>()

    let mounted =
        underBoundary ops run queued graph host log reports (fun () ->
            Typed.div
                ops
                [|
                    Typed.reactiveProperty
                        (fun (node: HTMLDivElement) (value: string) ->
                            if value = "bad" then
                                failwith "setter"

                            node.title <- value)
                        (fun () -> source.Value)
                |]
                [| Typed.reactiveText ops (fun () -> source.Value) |])

    graph.Run (fun () -> source.Value <- "bad")
    mounted.Flush ()

    equal
        ("failed: setter", [ "content"; "content cleanup"; "error setter" ], [ [ "setter" ] ], (if queued then 1 else 0))
        (host.textContent, List.ofSeq log, messages reports, mounted.CommitErrors.Length)

    mounted.Dispose ()

let private rowSetupFailure (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let rows = graph.Run (fun () -> Api.createSignal [ 1; 2 ])
    let host, log, reports = host (), ResizeArray<string>(), ResizeArray<exn list>()

    let mounted =
        underBoundary ops run queued graph host log reports (fun () ->
            Keyed.list ops id (fun () -> rows.Value) (fun current ->
                ops.Component (fun _ ->
                    if current () = 3 then
                        failwith "row setup"

                    Typed.reactiveText ops (fun () -> string (current ())))))

    equal "12" host.textContent
    graph.Run (fun () -> rows.Value <- [ 1; 2; 3 ])
    mounted.Flush ()

    equal
        ("failed: row setup", [ "content"; "content cleanup"; "error row setup" ], [ [ "row setup" ] ])
        (host.textContent, List.ofSeq log, messages reports)

    mounted.Dispose ()

let private armSetupFailure (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let selected = graph.Run (fun () -> Api.createSignal 0)
    let host, log, reports = host (), ResizeArray<string>(), ResizeArray<exn list>()

    let mounted =
        underBoundary ops run queued graph host log reports (fun () ->
            Branch.select ops id (fun () -> selected.Value) (fun current ->
                ops.Component (fun _ ->
                    if current () = 1 then
                        failwith "arm setup"

                    ops.Text "arm")))

    equal "arm" host.textContent
    graph.Run (fun () -> selected.Value <- 1)
    mounted.Flush ()

    equal
        ("failed: arm setup", [ "content"; "content cleanup"; "error arm setup" ], [ [ "arm setup" ] ])
        (host.textContent, List.ofSeq log, messages reports)

    mounted.Dispose ()

/// <summary>
/// Two setters under one boundary fail in the same queued flush. The error view shows the first failure in commit
/// order, set by the core's effect order.
/// </summary>
let private twoQueuedFailures (ops: Operations<'view>) (run: 'view -> Backend -> unit) =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "ok")
    let host, log, reports = host (), ResizeArray<string>(), ResizeArray<exn list>()

    let failing (message: string) =
        Typed.reactiveProperty
            (fun (node: HTMLDivElement) (value: string) ->
                if value = "bad" then
                    failwith message

                node.title <- value)
            (fun () -> source.Value)

    let mounted =
        underBoundary ops run true graph host log reports (fun () -> Typed.div ops [| failing "first"; failing "second" |] [||])

    graph.Run (fun () -> source.Value <- "bad")
    mounted.Flush ()

    let committedFirst =
        reports
        |> Seq.tryHead
        |> Option.bind List.tryHead
        |> Option.map _.Message
        |> Option.defaultValue ""

    equal
        ("failed: " + committedFirst, [ "content"; "content cleanup"; "error " + committedFirst ], [ [ "first"; "second" ] ])
        (host.textContent, List.ofSeq log, [ for errors in reports -> errors |> List.map _.Message |> List.sort ])

    mounted.Dispose ()

/// <summary>A content whose setup fails while <c>broken</c>; the error view's button retries it.</summary>
let private retry (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let mutable broken = true

    let flight =
        graph.Run (fun () -> Api.createSignal (Api.createAsyncSource<string>()))

    let setups = ResizeArray<string>()
    let reports = ResizeArray<exn list>()
    let host = host ()

    let view =
        ops.Boundary (
            ops.Text "loading",
            (fun failure ->
                Typed.button ops [| Typed.listener "click" (fun _ -> failure.Retry ()) |] [| ops.Text ("retry " + failure.Error.Message) |]),
            fun _ ->
                ops.Component (fun _ ->
                    setups.Add "content"

                    if broken then
                        failwith "setup"

                    Typed.reactiveText ops (fun () -> flight.Value.Value))
        )

    let mounted =
        mountWith (modeOf queued) graph None reports.Add host None (fun context -> run view context.Backend)

    let click () =
        (host.querySelector "button"
         |> unbox<HTMLButtonElement>)
            .click()

    equal ("retry setup", 1) (host.textContent, setups.Count)
    click ()
    equal ("retry setup", 2, 2) (host.textContent, setups.Count, reports.Count)
    broken <- false
    click ()
    equal ("loading", 3) (host.textContent, setups.Count)
    graph.Run (fun () -> flight.Value.Settle "ready")
    mounted.Flush ()
    equal ("ready", 2) (host.textContent, reports.Count)
    mounted.Dispose ()

/// <summary>An inner boundary's pending view fails; the outer boundary shows its error view.</summary>
let private pendingViewFailure (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createAsyncSource<string>())
    let reports = ResizeArray<exn list>()
    let host = host ()

    let failed prefix (failure: BoundaryFailure) =
        ops.Text (prefix + failure.Error.Message)

    let inner =
        ops.Boundary (ops.Component (fun _ -> failwith "pending view"), failed "inner: ", fun _ -> Typed.reactiveText ops (fun () -> source.Value))

    let view =
        ops.Boundary (ops.Text "outer loading", failed "outer: ", (fun _ -> inner))

    let mounted =
        mountWith (modeOf queued) graph None reports.Add host None (fun context -> run view context.Backend)

    equal ("outer: pending view", [ [ "pending view" ] ]) (host.textContent, messages reports)
    mounted.Dispose ()

/// <summary>An inner boundary's content and error view both fail; the outer boundary shows its error view.</summary>
let private errorViewFailure (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let reports = ResizeArray<exn list>()
    let host = host ()

    let failed prefix (failure: BoundaryFailure) =
        ops.Text (prefix + failure.Error.Message)

    let inner =
        ops.Boundary (
            ops.Text "inner loading",
            (fun _ -> ops.Component (fun _ -> failwith "error view")),
            fun _ -> ops.Component (fun _ -> failwith "content")
        )

    let view =
        ops.Boundary (ops.Text "outer loading", failed "outer: ", (fun _ -> inner))

    let mounted =
        mountWith (modeOf queued) graph None reports.Add host None (fun context -> run view context.Backend)

    equal ("outer: error view", [ [ "content" ]; [ "error view" ] ]) (host.textContent, messages reports)
    mounted.Dispose ()

/// <summary>A content whose setup raises one exception object on every attempt.</summary>
let private retryCachedFailure (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let cached = Exception "cached"
    let reports = ResizeArray<exn list>()
    let host = host ()

    let view =
        ops.Boundary (
            ops.Text "loading",
            (fun failure ->
                Typed.button ops [| Typed.listener "click" (fun _ -> failure.Retry ()) |] [| ops.Text ("retry " + failure.Error.Message) |]),
            fun _ -> ops.Component (fun _ -> raise cached)
        )

    let mounted =
        mountWith (modeOf queued) graph None reports.Add host None (fun context -> run view context.Backend)

    (host.querySelector "button"
     |> unbox<HTMLButtonElement>)
        .click()

    equal ("retry cached", [ [ "cached" ]; [ "cached" ] ]) (host.textContent, messages reports)
    mounted.Dispose ()

/// <summary>Two readers of one failed memo under a boundary receive the memo's exception object.</summary>
let private sharedReaderFailure (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "ok")

    let memo =
        graph.Run (fun () ->
            Api.createMemo (fun _ ->
                if source.Value = "bad" then
                    failwith "memo"
                else
                    source.Value))

    let host, log, reports = host (), ResizeArray<string>(), ResizeArray<exn list>()

    let mounted =
        underBoundary ops run queued graph host log reports (fun () ->
            ops.Fragment
                [|
                    Typed.reactiveText ops (fun () -> memo.Value)
                    Typed.reactiveText ops (fun () -> memo.Value)
                |])

    equal "okok" host.textContent
    graph.Run (fun () -> source.Value <- "bad")
    mounted.Flush ()

    let expected =
        "failed: memo", [ "content"; "content cleanup"; "error memo" ], [ [ "memo" ] ]

    equal expected (host.textContent, List.ofSeq log, messages reports)
    graph.Run (fun () -> source.Value <- "bad again")
    mounted.Flush ()
    equal expected (host.textContent, List.ofSeq log, messages reports)
    mounted.Dispose ()

/// <summary>Setters under an inner boundary and under the outer boundary enclosing it fail in one queued flush.</summary>
let private nestedQueuedFailures (ops: Operations<'view>) (run: 'view -> Backend -> unit) =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "ok")
    let reports = ResizeArray<exn list>()
    let host = host ()

    let failing (message: string) =
        Typed.reactiveProperty
            (fun (node: HTMLDivElement) (value: string) ->
                if value = "bad" then
                    failwith message

                node.title <- value)
            (fun () -> source.Value)

    let failed prefix (failure: BoundaryFailure) =
        ops.Text (prefix + failure.Error.Message)

    let inner =
        ops.Boundary (ops.Text "inner loading", failed "inner: ", (fun _ -> Typed.div ops [| failing "inner" |] [||]))

    let view =
        ops.Boundary (ops.Text "outer loading", failed "outer: ", fun _ -> ops.Fragment [| Typed.div ops [| failing "outer" |] [||]; inner |])

    let mounted =
        mountWith (modeOf true) graph None reports.Add host None (fun context -> run view context.Backend)

    graph.Run (fun () -> source.Value <- "bad")
    mounted.Flush ()

    equal
        ("outer: outer", [ "inner"; "outer" ])
        (host.textContent,
         [
             for errors in reports do
                 for error in errors -> error.Message
         ]
         |> List.sort)

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
        "tree synchronous: a failed reader under a boundary shows the error view, cleans up once and reports once",
        fun () -> readerFailure Tree.ops Tree.run false
        "program queued: a failed reader under a boundary shows the error view, cleans up once and reports once",
        fun () -> readerFailure Program.ops Program.run true
        "tree synchronous: a live setter failure under a boundary replaces the content with the error view",
        fun () -> setterFailure Tree.ops Tree.run false
        "program synchronous: a live setter failure under a boundary replaces the content with the error view",
        fun () -> setterFailure Program.ops Program.run false
        "tree queued: a queued setter failure under a boundary replaces the content after the flush", fun () -> setterFailure Tree.ops Tree.run true
        "program queued: a queued setter failure under a boundary replaces the content after the flush",
        fun () -> setterFailure Program.ops Program.run true
        "tree queued: a row setup failure under a boundary shows the error view", fun () -> rowSetupFailure Tree.ops Tree.run true
        "program synchronous: a row setup failure under a boundary shows the error view", fun () -> rowSetupFailure Program.ops Program.run false
        "tree synchronous: an arm setup failure under a boundary shows the error view", fun () -> armSetupFailure Tree.ops Tree.run false
        "program queued: an arm setup failure under a boundary shows the error view", fun () -> armSetupFailure Program.ops Program.run true
        "tree: two setters failing in one queued flush tear the content down once", fun () -> twoQueuedFailures Tree.ops Tree.run
        "program: two setters failing in one queued flush tear the content down once", fun () -> twoQueuedFailures Program.ops Program.run
        "tree synchronous: retry remounts the content with a new setup and pending phase", fun () -> retry Tree.ops Tree.run false
        "program queued: retry remounts the content with a new setup and pending phase", fun () -> retry Program.ops Program.run true
        "tree queued: a failing pending view goes to the enclosing boundary", fun () -> pendingViewFailure Tree.ops Tree.run true
        "program synchronous: a failing pending view goes to the enclosing boundary", fun () -> pendingViewFailure Program.ops Program.run false
        "tree synchronous: a failing error view goes to the enclosing boundary", fun () -> errorViewFailure Tree.ops Tree.run false
        "program queued: a failing error view goes to the enclosing boundary", fun () -> errorViewFailure Program.ops Program.run true
        "tree synchronous: a retry reports again an exception object reported before the retry", fun () -> retryCachedFailure Tree.ops Tree.run false
        "program queued: a retry reports again an exception object reported before the retry",
        fun () -> retryCachedFailure Program.ops Program.run true
        "tree queued: two readers of one failed memo under a boundary report it once and tear down once",
        fun () -> sharedReaderFailure Tree.ops Tree.run true
        "program synchronous: two readers of one failed memo under a boundary report it once and tear down once",
        fun () -> sharedReaderFailure Program.ops Program.run false
        "tree: an inner and an outer failure in one queued flush are each reported once", fun () -> nestedQueuedFailures Tree.ops Tree.run
        "program: an inner and an outer failure in one queued flush are each reported once", fun () -> nestedQueuedFailures Program.ops Program.run
        "tree synchronous: a content binding failing on its first read shows the error view", fun () -> initialReaderFailure Tree.ops Tree.run false
        "tree queued: a content binding failing on its first read shows the error view", fun () -> initialReaderFailure Tree.ops Tree.run true
        "program synchronous: a content binding failing on its first read shows the error view",
        fun () -> initialReaderFailure Program.ops Program.run false
        "program queued: a content binding failing on its first read shows the error view",
        fun () -> initialReaderFailure Program.ops Program.run true
    |]
