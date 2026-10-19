module UnsafeHtmlChecks

open System
open Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open CheckSupport

let private raisesArgument (action: unit -> unit) =
    try
        action ()
        false
    with :? ArgumentException ->
        true

/// <summary>Observes the node's subtree; the returned function stops observing and returns the number of child-list records.</summary>
[<Emit("(() => { const records = []; const observer = new ($0.ownerDocument.defaultView.MutationObserver)(items => records.push(...items)); observer.observe($0, { childList: true, subtree: true }); return () => { records.push(...observer.takeRecords()); observer.disconnect(); return records.length; }; })()")>]
let private observeChildList (node: Node) : unit -> int = jsNative

let private article =
    Component.define (fun _ (body: unit -> UnsafeHtml) -> body) (fun t ->
        t.div [| t.Attrs.className "prose"; t.Unsafe.html (fun body -> body ()) |] [||])

let private reparse queued () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "<b>one</b>")
    let tick = graph.Run (fun () -> Api.createSignal 0)
    let target = host ()

    let body () =
        tick.Value |> ignore
        UnsafeHtml.Trust source.Value

    let mounted =
        mountTemplate queued graph strict target (Component.render article body)

    let div =
        target.querySelector "div"
        |> unbox<HTMLDivElement>

    let first = div.innerHTML
    let stop = observeChildList div
    graph.Run (fun () -> tick.Value <- 1)
    mounted.Flush ()
    graph.Run (fun () -> source.Value <- "<i>two</i>")
    mounted.Flush ()
    equal ("<b>one</b>", "<i>two</i>", 1) (first, div.innerHTML, stop ())
    mounted.Dispose ()

let private tableBody =
    Component.define (fun _ () -> ()) (fun t ->
        t.table
            [||]
            [|
                t.tbody [| t.Unsafe.html (UnsafeHtml.Trust "<tr><td>a</td></tr><tr><td>b</td></tr>") |] [||]
            |])

let private inContext queued () =
    use graph = new Graph ()
    let target = host ()

    let mounted =
        mountTemplate queued graph strict target (Component.render tableBody ())

    equal (2., "ab") ((target.querySelectorAll "tbody > tr").length, target.textContent)
    mounted.Dispose ()

let private definitionChecks () =
    let defines (build: TemplateBuilder<unit> -> TemplateView<unit>) =
        raisesArgument (fun () -> Component.define (fun _ () -> ()) build |> ignore)

    let raw = UnsafeHtml.Trust "<b>x</b>"

    equal
        [ true; true; true; true; true; true; true; true; true; false ]
        [
            defines (fun t -> t.div [| t.Unsafe.html raw |] [| t.text "child" |])
            defines (fun t -> t.div [| t.Unsafe.html raw |] [| t.fragment [||] |])
            defines (fun t -> t.div [| t.Hooks.widget (fun _ (_: HTMLDivElement) -> ignore) |] [| t.fragment [||] |])
            defines (fun t -> t.div [| t.Unsafe.html raw; t.Hooks.widget (fun _ (_: HTMLDivElement) -> ignore) |] [||])
            defines (fun t -> t.br [| t.Unsafe.html raw |])
            defines (fun t -> t.script [| t.Unsafe.html raw |] [||])
            defines (fun t -> t.template [| t.Unsafe.html raw |] [||])
            defines (fun t -> t.textarea [| t.Unsafe.html raw |] [||])
            defines (fun t -> t.title [| t.Unsafe.html raw |] [||])
            defines (fun t ->
                t.div
                    [|
                        t.Attrs.className "prose"
                        t.Unsafe.html raw
                        t.Hooks.lend (fun _ (_: HTMLDivElement) -> ignore)
                    |]
                    [||])
        ]

let mutable private rawSource: Signal<AsyncSource<string>> = Unchecked.defaultof<_>

let private rawContent =
    Component.define (fun _ (_: BoundaryStatus) -> ()) (fun t -> t.div [| t.Unsafe.html (fun () -> UnsafeHtml.Trust rawSource.Value.Value) |] [||])

let private rawPending =
    Component.define (fun _ () -> ()) (fun t -> t.span [||] [| t.text "loading" |])

let private rawFailed =
    Component.define (fun _ (failure: BoundaryFailure) -> failure) (fun t ->
        t.span [||] [| t.text (fun failure -> "failed: " + failure.Error.Message) |])

let private rawGuarded =
    Component.define (fun _ () -> ()) (fun t -> t.section [||] [| t.boundary rawPending rawFailed rawContent |])

let private pendingReader queued () =
    use graph = new Graph ()
    rawSource <- graph.Run (fun () -> Api.createSignal (Api.createAsyncSource<string>()))
    let reports = ResizeArray<exn list>()
    let target = host ()

    let mounted =
        mountTemplate queued graph reports.Add target (Component.render rawGuarded ())

    let pending = target.textContent
    graph.Run (fun () -> rawSource.Value.Settle "<b>ready</b>")
    mounted.Flush ()
    let revealed = (target.querySelector "b" |> unbox<HTMLElement>).textContent
    graph.Run (fun () -> rawSource.Value <- Api.createAsyncSource<string>())
    graph.Run (fun () -> rawSource.Value.Fail (Exception "markup"))
    mounted.Flush ()
    equal ("loading", "ready", "failed: markup", [ [ "markup" ] ]) (pending, revealed, target.textContent, messages reports)
    mounted.Dispose ()

let private serverRaw =
    Component.define (fun _ () -> ()) (fun t ->
        t.fragment
            [|
                t.div [| t.Unsafe.html (UnsafeHtml.Trust "<b>raw</b> &amp; <i>kept</i>") |] [||]
                t.span [||] [| t.text (fun () -> "<escaped>") |]
            |])

let private serverHtml () =
    let render policy =
        Html.render policy (Program.run (Component.render serverRaw ()))

    let expected = "<div><b>raw</b> &amp; <i>kept</i></div><span>&lt;escaped&gt;</span>"

    equal (expected, expected) (render Reject, render Omit)

/// <summary>Mounts trusted markup in a div appended to the document body; returns the div and the disposal.</summary>
let unsafeMarkup (markup: string) : HTMLElement * (unit -> unit) =
    let graph = new Graph ()
    let target = host ()
    attachToBody target

    let mounted =
        mountTemplate false graph strict target (Component.render article (fun () -> UnsafeHtml.Trust markup))

    target,
    fun () ->
        mounted.Dispose ()
        target.remove ()
        graph.Dispose ()

let cases: (string * (unit -> unit)) array =
    strictCases (
        Array.concat
            [
                templated "raw HTML replaces the element's content and is re-parsed only when the markup changes" reparse
                templated "raw HTML inside a tbody element parses table rows in context" inContext
                [|
                    "an element with raw HTML and template children fails at definition time; void, raw-text, template, textarea and title elements reject raw HTML",
                    definitionChecks
                    "server HTML writes raw content verbatim under Reject and Omit, and text slots beside it stay escaped", serverHtml
                |]
                templated "a pending raw-HTML reader keeps its boundary pending; a failing one shows the error view" pendingReader
            ]
    )
