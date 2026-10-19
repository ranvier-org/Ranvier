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

[<Emit("document")>]
let private currentDocument () : Document = jsNative

[<Emit("document.implementation.createHTMLDocument($0)")>]
let private otherDocument (documentTitle: string) : Document = jsNative

[<Emit("$0.title")>]
let private titleOf (document: Document) : string = jsNative

[<Emit("$0.head.querySelectorAll($1).length")>]
let private headCountOf (document: Document) (selector: string) : int = jsNative

/// <summary>Adds a marked meta description to the document's head and returns it.</summary>
[<Emit("(() => { const meta = $0.createElement('meta'); meta.setAttribute('name', 'description'); meta.setAttribute('data-ranvier-head', 'meta:name=description'); $0.head.append(meta); return meta; })()")>]
let private markedDescriptionIn (document: Document) : Element = jsNative

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

let private childTitle =
    Component.define (fun _ () -> ()) (fun t -> t.fragment [| t.Document.title "Child"; t.span [||] [||] |])

let private layoutTitle =
    Component.define (fun _ () -> ()) (fun t -> t.fragment [| t.div [||] [| t.render childTitle id |]; t.Document.title "Layout" |])

let private templateNestedTitle queued () =
    use graph = new Graph ()
    setTitle "Before"

    let mounted =
        mountTemplate queued graph strict (host ()) (Component.render layoutTitle ())

    let shown = title ()
    mounted.Dispose ()
    equal ("Child", "Before") (shown, title ())

let private titleEntry (value: string) : HeadEntry =
    {
        Kind = HeadKind.Title
        Key = "title"
        Attributes = [||]
        Readers = [||]
        Title = fun () -> value
    }

/// <summary>A layout component whose title entry follows a div holding a child component's title entry.</summary>
let private nestedTitle (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    setTitle "Before"

    let entry value =
        ops.Embed (fun backend -> backend.Head (titleEntry value))

    let view =
        ops.Component (fun _ ->
            ops.Fragment
                [|
                    Typed.div ops [||] [| ops.Component (fun _ -> entry "Child") |]
                    entry "Layout"
                |])

    let mounted = mountView ops run queued graph strict (host ()) view
    let shown = title ()
    mounted.Dispose ()
    equal ("Child", "Before") (shown, title ())

let private canonicalAndExplicit =
    Component.define (fun _ () -> ()) (fun t ->
        t.fragment
            [|
                t.Document.link
                    [|
                        t.Attrs.attribute ("rel", "canonical")
                        t.Attrs.attribute ("href", "/page")
                    |]
                t.Document.link (
                    "canonical",
                    [|
                        t.Attrs.attribute ("rel", "alternate")
                        t.Attrs.attribute ("href", "/alternate")
                    |]
                )
            |])

let private explicitKeyDistinct queued () =
    use graph = new Graph ()

    let mounted =
        mountTemplate queued graph strict (host ()) (Component.render canonicalAndExplicit ())

    let live =
        headAttribute "link[rel=canonical]" "href", headAttribute "link[rel=alternate]" "href"

    mounted.Dispose ()
    equal (("/page", "/alternate"), (0, 0)) (live, (headCount "link[rel=canonical]", headCount "link[rel=alternate]"))

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

let mutable private siblingSource: Signal<AsyncSource<string>> =
    Unchecked.defaultof<_>

let private pendingSibling =
    Component.define (fun _ (_: BoundaryStatus) -> ()) (fun t ->
        t.fragment
            [|
                description "content" t
                t.span [||] [| t.text (fun () -> siblingSource.Value.Value) |]
            |])

let private siblingRegions =
    Component.define (fun _ (second: Signal<bool>) -> second) (fun t ->
        t.div
            [||]
            [|
                t.boundary titlePending titleFailed pendingSibling
                t.branch (fun (second: Signal<bool>) -> second.Value) (fun second -> if second then t.arm innerMeta () else t.arm noMeta ())
            |])

/// <summary>
/// A pending boundary's entry adopts the marked element, and a sibling region's entry of the same key activates first.
/// The head holds one element of the key throughout.
/// </summary>
let private siblingAdoption (fails: bool) queued () =
    use graph = new Graph ()
    let server = serverDescription ()
    siblingSource <- graph.Run (fun () -> Api.createSignal (Api.createAsyncSource<string>()))
    let second = graph.Run (fun () -> Api.createSignal false)
    let reports = ResizeArray<exn list>()

    let mounted =
        mountTemplate queued graph reports.Add (host ()) (Component.render siblingRegions second)

    let read () =
        headCount "meta[name=description]", headAttribute "meta[name=description]" "content"

    let pending = read ()
    graph.Run (fun () -> second.Value <- true)
    mounted.Flush ()
    let sibling = read ()

    graph.Run (fun () ->
        if fails then
            siblingSource.Value.Fail (Exception "gone")
        else
            siblingSource.Value.Settle "ready")

    mounted.Flush ()
    let settled = read ()
    graph.Run (fun () -> second.Value <- false)
    mounted.Flush ()
    let closed = read (), isConnected server
    mounted.Dispose ()

    let expectedSettled, expectedClosed, expectedReports =
        if fails then
            (1, "inner"), ((0, null), false), [ [ "gone" ] ]
        else
            (1, "content"), ((1, "content"), true), []

    equal
        ((1, "content"), (1, "inner"), expectedSettled, expectedClosed, 0, expectedReports)
        (pending, sibling, settled, closed, headCount "meta[name=description]", messages reports)

/// <summary>A pending boundary's entry adopts the marked element; the element leaves the head when the boundary fails.</summary>
let private pendingAdopterFails queued () =
    use graph = new Graph ()
    let server = serverDescription ()
    siblingSource <- graph.Run (fun () -> Api.createSignal (Api.createAsyncSource<string>()))
    let second = graph.Run (fun () -> Api.createSignal false)
    let reports = ResizeArray<exn list>()

    let mounted =
        mountTemplate queued graph reports.Add (host ()) (Component.render siblingRegions second)

    let pending = headCount "meta[name=description]", isConnected server
    graph.Run (fun () -> siblingSource.Value.Fail (Exception "gone"))
    mounted.Flush ()
    let failed = headCount "meta[name=description]", isConnected server
    mounted.Dispose ()
    equal ((1, true), (0, false), [ [ "gone" ] ]) (pending, failed, messages reports)

let private nullTitle =
    Component.define (fun _ () -> ()) (fun t -> t.fragment [| t.Document.title (fun () -> (null: string)) |])

let private nullTitleEmpty queued () =
    use graph = new Graph ()
    setTitle "Before"

    let mounted =
        mountTemplate queued graph strict (host ()) (Component.render nullTitle ())

    let shown = title ()
    mounted.Dispose ()
    equal ("", "Before") (shown, title ())

/// <summary>Title and meta records of the global document and of a second document.</summary>
let private perDocument () =
    setTitle "Before"
    let other = otherDocument "Other"
    let marked = markedDescriptionIn other
    let here = DocumentHead.create (currentDocument ()) HeadKind.Title "title"
    let there = DocumentHead.create other HeadKind.Title "title"
    DocumentHead.setTitle here "Here"
    DocumentHead.setTitle there "There"
    DocumentHead.activate here
    DocumentHead.activate there
    let titles = title (), titleOf other
    DocumentHead.withdraw here
    let afterHere = title (), titleOf other
    DocumentHead.withdraw there
    let restored = title (), titleOf other

    let hereMeta =
        DocumentHead.create (currentDocument ()) HeadKind.Meta "meta:name=description"

    let thereMeta = DocumentHead.create other HeadKind.Meta "meta:name=description"
    DocumentHead.activate hereMeta
    DocumentHead.activate thereMeta

    let adopted =
        obj.ReferenceEquals (thereMeta.Element, marked),
        obj.ReferenceEquals (hereMeta.Element, marked),
        headCount "meta[data-ranvier-head]",
        headCountOf other "meta[name=description]"

    DocumentHead.withdraw hereMeta
    DocumentHead.withdraw thereMeta

    equal
        (("Here", "There"), ("Before", "There"), ("Before", "Other"), (true, false, 1, 1), (0, 0))
        (titles, afterHere, restored, adopted, (headCount "meta[data-ranvier-head]", headCountOf other "meta[name=description]"))

let private definitionChecks () =
    let defines (build: TemplateBuilder<unit> -> TemplateView<unit>) =
        raisesArgument (fun () -> Component.define (fun _ () -> ()) build |> ignore)

    equal
        [ true; true; true; true; true; true; true; true; true; false ]
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
            defines (fun t -> t.Document.link ("theme", [| t.Attrs.attribute ("rel", (fun () -> Some "stylesheet")) |]))
            defines (fun t -> t.Document.link [| t.Attrs.attribute ("rel", (fun () -> Some "canonical")) |])
            defines (fun t ->
                t.Document.meta
                    [|
                        t.Attrs.attribute ("name", "x")
                        t.Attrs.attribute ("data-ranvier-head", "meta:name=y")
                    |])
            defines (fun t ->
                t.Document.link
                    [|
                        t.Attrs.attribute ("rel", "stylesheet")
                        t.Attrs.attribute ("data-ranvier-head", (fun () -> Some "x"))
                    |])
            defines (fun t ->
                t.Document.link
                    [|
                        t.Attrs.attribute ("rel", "alternate")
                        t.Attrs.attribute ("name", (fun () -> Some "x"))
                        t.Attrs.attribute ("property", (fun () -> Some "x"))
                    |])
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

let private earlyChild =
    Component.define (fun _ () -> ()) (fun t -> t.fragment [| t.Document.title "Child"; description "child" t |])

let private lateParent =
    Component.define (fun _ () -> ()) (fun t ->
        t.div
            [||]
            [|
                t.render earlyChild ignore
                t.Document.title "Parent"
                description "parent" t
            |])

let private titledRow =
    Component.define (fun _ (item: unit -> int) -> item) (fun t -> t.Document.title (fun (item: unit -> int) -> "Row " + string (item ())))

let private rowsThenChild =
    Component.define (fun _ () -> ()) (fun t ->
        t.div
            [||]
            [|
                t.ul [||] [| t.each id (fun _ -> [| 1; 2 |]) titledRow |]
                t.render earlyChild ignore
            |])

/// <summary>The title text and the meta description of <c>Server.renderPage</c>'s head for <c>definition</c>.</summary>
let private serverShown (definition: Component<unit>) =
    let head = (Server.renderPage Server.Options.defaults definition ()).Head
    let opening = "<title data-ranvier-head=\"title\">"
    let start = head.IndexOf opening + opening.Length
    let marker = "name=\"description\" content=\""
    let content = head.IndexOf marker + marker.Length
    head.Substring (start, head.IndexOf ("</title>", start) - start), head.Substring (content, head.IndexOf ("\"", content) - content)

let private serverHeadShown queued () =
    let clientShown (definition: Component<unit>) =
        use graph = new Graph ()

        let mounted =
            mountTemplate queued graph strict (host ()) (Component.render definition ())

        let shown = title (), headAttribute "meta[name=description]" "content"
        mounted.Dispose ()
        shown

    equal [ ("Child", "child"); ("Row 2", "child") ] [ for definition in [ lateParent; rowsThenChild ] -> clientShown definition ]

    equal [ ("Child", "child"); ("Row 2", "child") ] [ for definition in [ lateParent; rowsThenChild ] -> serverShown definition ]

let private innerRow =
    Component.define (fun _ (_: unit -> int) -> ()) (fun t -> t.fragment [| t.Document.title "Inner"; description "inner" t |])

let private outerRow =
    Component.define (fun _ (item: unit -> int) -> item ()) (fun t ->
        t.fragment
            [|
                t.Document.title (fun (index: int) -> "Row " + string index)
                t.Document.meta
                    [|
                        t.Attrs.attribute ("name", "description")
                        t.Attrs.attribute ("content", fun (index: int) -> Some ("row " + string index))
                    |]
                t.ul
                    [||]
                    [|
                        t.each id (fun (index: int) -> if index = 1 then [| 10 |] else [||]) innerRow
                    |]
            |])

let private nestedLists =
    Component.define (fun _ () -> ()) (fun t -> t.div [||] [| t.each id (fun _ -> [| 1; 2 |]) outerRow |])

/// <summary>A template whose view hole holds <c>definition</c> expanded into a Program view.</summary>
let private expandedIn (definition: Component<unit>) =
    Component.define (fun _ () -> Component.renderWith Program.ops definition ()) (fun t -> t.div [||] [| t.view id |])

let private containerShown queued () =
    let clientShown (definition: Component<unit>) =
        use graph = new Graph ()

        let mounted =
            mountTemplate queued graph strict (host ()) (Component.render definition ())

        let shown = title (), headAttribute "meta[name=description]" "content"
        mounted.Dispose ()
        shown

    let definitions = [ nestedLists; expandedIn nestedLists; expandedIn rowsThenChild ]

    let expected = [ ("Inner", "inner"); ("Inner", "inner"); ("Row 2", "child") ]

    equal expected [ for definition in definitions -> clientShown definition ]
    equal expected [ for definition in definitions -> serverShown definition ]

let cases: (string * (unit -> unit)) array =
    strictCases (
        Array.concat
            [
                templated "a title entry sets document.title once placed and restores the previous title when disposed" titleRestored
                templated "the most recent entry for a key is shown and the previous one returns when it withdraws" latestShown
                templated "unkeyed links are each present while live; a canonical link is keyed" linkKeys
                templated "a link with the explicit key canonical and a canonical link are both shown" explicitKeyDistinct
                templated "an inlined child's title entry is shown over its parent's later title entry" templateNestedTitle
                both
                    "an inlined child's title entry is shown over its parent's later title entry"
                    (nestedTitle Tree.ops Tree.run)
                    (nestedTitle Program.ops Program.run)
                templated
                    "renderPage's head shows the title and meta a client mount shows for an inlined child before its parent's later entries and rows after both"
                    serverHeadShown
                templated
                    "renderPage's head shows the title and meta a client mount shows for nested lists and for rows and a child expanded in a view hole"
                    containerShown
                templated "a reactive title updates document.title and its pending read waits under a boundary" reactiveTitle
                templated "entries under hidden content activate at reveal and withdraw on teardown" hiddenEntries
                templated "a client mount adopts marked head elements without hydrate" adoptsMarked
                templated "a marked head element is adopted by one entry of its key" adoptsOnce
                templated
                    "a pending entry's adopted element gives way to a sibling entry of its key and leaves when the boundary fails"
                    (siblingAdoption true)
                templated "a pending entry's adopted element gives way to a sibling entry of its key and returns at reveal" (siblingAdoption false)
                templated "a pending entry's adopted element leaves the head when the boundary fails" pendingAdopterFails
                templated "a title reader returning null sets an empty document.title" nullTitleEmpty
                templated "title entries from two graphs withdrawn out of order keep the shown title and restore the original" crossGraphTitles
                [|
                    "event and property parts on a document entry fail at definition time; a dynamic key attribute and a data-ranvier-head attribute fail",
                    definitionChecks
                    "Html.render writes no document entry under Omit and raises under Reject", serverEntries
                    "each document keeps its own title and adopts only its own marked elements", perDocument
                |]
            ]
    )
