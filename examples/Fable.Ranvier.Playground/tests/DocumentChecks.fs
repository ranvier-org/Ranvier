module DocumentChecks

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

[<Emit("document.title")>]
let private title () : string = jsNative

[<Emit("document.title = $0")>]
let private setTitle (value: string) : unit = jsNative

[<Emit("document.head.querySelectorAll($0).length")>]
let private headCount (selector: string) : int = jsNative

[<Emit("(document.head.querySelector($0)?.getAttribute($1) ?? null)")>]
let private headAttribute (selector: string) (name: string) : string = jsNative

/// <summary>Adds a marked meta description to the head, as a server page carries it, and returns it.</summary>
[<Emit("(() => { const meta = document.createElement('meta'); meta.setAttribute('name', 'description'); meta.setAttribute('content', 'server'); meta.setAttribute('data-ranvier-head', 'meta:name=description'); document.head.append(meta); return meta; })()")>]
let private serverDescription () : Node = jsNative

[<Emit("$0.isConnected")>]
let private isConnected (node: Node) : bool = jsNative

[<Emit("(document.head.querySelector($0) === $1)")>]
let private headHolds (selector: string) (node: Node) : bool = jsNative

let private description (content: string) (t: TemplateBuilder<'inst>) =
    t.Document.meta
        [|
            t.Attrs.attribute ("name", "description")
            t.Attrs.attribute ("content", content)
        |]

let private titled =
    Component.define (fun _ (name: string) -> name) (fun t ->
        t.fragment
            [|
                t.Document.title (fun name -> name + " · Shop")
                t.h1 [||] [| t.text (fun name -> name) |]
            |])

let private titleRestored queued () =
    use graph = new Graph ()
    setTitle "Before"

    let mounted =
        mountTemplate queued graph strict (host ()) (Component.render titled "Lamp")

    let shown = title ()
    mounted.Dispose ()
    equal ("Lamp · Shop", "Before") (shown, title ())

let private innerMeta =
    Component.define (fun _ (_: unit -> unit) -> ()) (fun t -> description "inner" t)

let private noMeta =
    Component.define (fun _ (_: unit -> unit) -> ()) (fun t -> t.fragment [||])

let private outerMeta =
    Component.define (fun _ (opened: Signal<bool>) -> opened) (fun t ->
        t.div
            [||]
            [|
                description "outer" t
                t.branch (fun (opened: Signal<bool>) -> opened.Value) (fun opened -> if opened then t.arm innerMeta () else t.arm noMeta ())
            |])

let private latestShown queued () =
    use graph = new Graph ()
    let opened = graph.Run (fun () -> Api.createSignal true)

    let mounted =
        mountTemplate queued graph strict (host ()) (Component.render outerMeta opened)

    let read () =
        headCount "meta[name=description]", headAttribute "meta[name=description]" "content"

    let first = read ()
    graph.Run (fun () -> opened.Value <- false)
    mounted.Flush ()
    let closed = read ()
    graph.Run (fun () -> opened.Value <- true)
    mounted.Flush ()
    let reopened = read ()
    mounted.Dispose ()
    equal ((1, "inner"), (1, "outer"), (1, "inner"), (0, null)) (first, closed, reopened, read ())

let private linkRow =
    Component.define (fun _ (item: unit -> int) -> item) (fun t ->
        t.fragment
            [|
                t.Document.link
                    [|
                        t.Attrs.attribute ("rel", "stylesheet")
                        t.Attrs.attribute ("href", (fun (item: unit -> int) -> Some (sprintf "/%d.css" (item ()))))
                    |]
                t.Document.link
                    [|
                        t.Attrs.attribute ("rel", "canonical")
                        t.Attrs.attribute ("href", (fun (item: unit -> int) -> Some (sprintf "/%d" (item ()))))
                    |]
            |])

let private linkRows =
    Component.define (fun _ () -> ()) (fun t -> t.div [||] [| t.each id (fun _ -> [| 1; 2 |]) linkRow |])

let private linkKeys queued () =
    use graph = new Graph ()

    let mounted =
        mountTemplate queued graph strict (host ()) (Component.render linkRows ())

    let live =
        headCount "link[rel=stylesheet]", headCount "link[rel=canonical]", headAttribute "link[rel=canonical]" "href"

    mounted.Dispose ()
    equal ((2, 1, "/2"), (0, 0)) (live, (headCount "link[rel=stylesheet]", headCount "link[rel=canonical]"))

let mutable private titleSource: Signal<AsyncSource<string>> =
    Unchecked.defaultof<_>

let private pendingTitle =
    Component.define (fun _ (_: BoundaryStatus) -> ()) (fun t ->
        t.fragment
            [|
                t.Document.title (fun () -> titleSource.Value.Value)
                t.span [||] [| t.text "page" |]
            |])

let private titlePending =
    Component.define (fun _ () -> ()) (fun t -> t.span [||] [| t.text "loading" |])

let private titleFailed =
    Component.define (fun _ (failure: BoundaryFailure) -> failure) (fun t ->
        t.span [||] [| t.text (fun failure -> "failed: " + failure.Error.Message) |])

let private guardedTitle =
    Component.define (fun _ () -> ()) (fun t -> t.div [||] [| t.boundary titlePending titleFailed pendingTitle |])

let private reactiveTitle queued () =
    use graph = new Graph ()
    setTitle "Before"
    titleSource <- graph.Run (fun () -> Api.createSignal (Api.createAsyncSource<string>()))
    let target = host ()

    let mounted =
        mountTemplate queued graph strict target (Component.render guardedTitle ())

    let pending = target.textContent, title ()
    graph.Run (fun () -> titleSource.Value.Settle "Ready")
    mounted.Flush ()
    let revealed = target.textContent, title ()
    let next = graph.Run (fun () -> Api.createAsyncSource<string>())
    graph.Run (fun () -> titleSource.Value <- next)
    mounted.Flush ()
    let refetching = title ()
    graph.Run (fun () -> next.Settle "Again")
    mounted.Flush ()
    let updated = title ()
    mounted.Dispose ()

    equal (("loading", "Before"), ("page", "Ready"), "Ready", "Again", "Before") (pending, revealed, refetching, updated, title ())

let mutable private metaSource: Signal<AsyncSource<string>> = Unchecked.defaultof<_>

let private pendingMeta =
    Component.define (fun _ (_: BoundaryStatus) -> ()) (fun t ->
        t.fragment
            [|
                description "content" t
                t.span [||] [| t.text (fun () -> metaSource.Value.Value) |]
            |])

let private guardedMeta =
    Component.define (fun _ () -> ()) (fun t -> t.div [||] [| t.boundary titlePending titleFailed pendingMeta |])

let private hiddenEntries queued () =
    use graph = new Graph ()
    metaSource <- graph.Run (fun () -> Api.createSignal (Api.createAsyncSource<string>()))
    let reports = ResizeArray<exn list>()

    let mounted =
        mountTemplate queued graph reports.Add (host ()) (Component.render guardedMeta ())

    let hidden = headCount "meta[name=description]"
    graph.Run (fun () -> metaSource.Value.Settle "ready")
    mounted.Flush ()
    let shown = headCount "meta[name=description]"
    let next = graph.Run (fun () -> Api.createAsyncSource<string>())
    graph.Run (fun () -> metaSource.Value <- next)
    graph.Run (fun () -> next.Fail (Exception "gone"))
    mounted.Flush ()
    let tornDown = headCount "meta[name=description]"
    mounted.Dispose ()
    equal ((0, 1, 0), [ [ "gone" ] ]) ((hidden, shown, tornDown), messages reports)

let private clientMeta =
    Component.define (fun _ () -> ()) (fun t -> t.div [||] [| description "client" t |])

let private adoptsMarked queued () =
    use graph = new Graph ()
    let server = serverDescription ()

    let mounted =
        mountTemplate queued graph strict (host ()) (Component.render clientMeta ())

    let adopted =
        headCount "meta[name=description]", headAttribute "meta[name=description]" "content", headHolds "meta[name=description]" server

    mounted.Dispose ()
    equal ((1, "client", true), (0, false)) (adopted, (headCount "meta[name=description]", isConnected server))

/// <summary>An outer and an inner entry of a marked key: the outer one adopts the marked element, the inner one owns a new one.</summary>
let private adoptsOnce queued () =
    use graph = new Graph ()
    let server = serverDescription ()
    let opened = graph.Run (fun () -> Api.createSignal true)

    let mounted =
        mountTemplate queued graph strict (host ()) (Component.render outerMeta opened)

    let read () =
        headCount "meta[name=description]", headAttribute "meta[name=description]" "content", headHolds "meta[name=description]" server

    let first = read ()
    graph.Run (fun () -> opened.Value <- false)
    mounted.Flush ()
    let closed = read ()
    mounted.Dispose ()
    equal ((1, "inner", false), (1, "outer", true), 0) (first, closed, headCount "meta[name=description]")

/// <summary>Two title entries on two graphs; the first activated withdraws while the second is shown.</summary>
let private crossGraphTitles queued () =
    use first = new Graph ()
    use second = new Graph ()
    setTitle "Before"
    let a = mountTemplate queued first strict (host ()) (Component.render titled "A")
    let b = mountTemplate queued second strict (host ()) (Component.render titled "B")
    let both = title ()
    a.Dispose ()
    let afterFirst = title ()
    b.Dispose ()
    equal ("B · Shop", "B · Shop", "Before") (both, afterFirst, title ())

let private definitionChecks () =
    let defines (build: TemplateBuilder<unit> -> TemplateView<unit>) =
        raisesArgument (fun () -> Component.define (fun _ () -> ()) build |> ignore)

    equal
        [ true; true; true; true; true ]
        [
            defines (fun t -> t.Document.meta [| t.Attrs.attribute ("name", "x"); t.Events.onClick (fun _ _ -> ()) |])
            defines (fun t ->
                t.Document.meta
                    [|
                        t.Attrs.attribute ("name", "x")
                        t.Props.propertyStatic ((fun (node: HTMLMetaElement) (value: string) -> node.content <- value), "x")
                    |])
            defines (fun t -> t.Document.meta [| t.Attrs.attribute ("name", (fun () -> Some "x")) |])
            defines (fun t ->
                t.Document.meta
                    [|
                        t.Attrs.attribute ("property", "og:x")
                        t.Attrs.attribute ("name", (fun () -> Some "x"))
                    |])
            defines (fun t -> t.Document.meta [| t.Attrs.attribute ("content", "x") |])
        ]

/// <summary>The template rendered by <c>Html.render</c> as compiled and as expanded into a Program view.</summary>
let private serverEntries () =
    let views =
        [
            Component.render titled "Lamp"
            Component.renderWith Program.ops titled "Lamp"
        ]

    let rejected view =
        try
            Html.render Reject (Program.run view) |> ignore
            false
        with :? InvalidOperationException as error ->
            error.Message = "Document metadata requires Server.renderPage"

    equal [ ("<h1>Lamp</h1>", true); ("<h1>Lamp</h1>", true) ] [ for view in views -> Html.render Omit (Program.run view), rejected view ]

let cases: (string * (unit -> unit)) array =
    strictCases (
        Array.concat
            [
                templated "a title entry sets document.title once placed and restores the previous title when disposed" titleRestored
                templated "the most recent entry for a key is shown and the previous one returns when it withdraws" latestShown
                templated "unkeyed links are each present while live; a canonical link is keyed" linkKeys
                templated "a reactive title updates document.title and its pending read waits under a boundary" reactiveTitle
                templated "entries under hidden content activate at reveal and withdraw on teardown" hiddenEntries
                templated "a client mount adopts marked head elements without hydrate" adoptsMarked
                templated "a marked head element is adopted by one entry of its key" adoptsOnce
                templated "title entries from two graphs withdrawn out of order keep the shown title and restore the original" crossGraphTitles
                [|
                    "event and property parts on a document entry fail at definition time; a dynamic key attribute fails", definitionChecks
                    "Html.render writes no document entry under Omit and raises under Reject", serverEntries
                |]
            ]
    )
