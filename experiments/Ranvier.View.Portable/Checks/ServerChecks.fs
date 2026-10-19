/// <summary>Server HTML checks, run on .NET by RepresentationChecks and under Fable by tests/server.test.mjs.</summary>
module ServerChecks

open System
open global.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open ServerFixtures

let equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

/// <summary>The exception <c>action</c> raises; fails when it returns.</summary>
let failure (action: unit -> unit) : exn =
    let mutable caught: exn = null

    try
        action ()
    with error ->
        caught <- error

    if isNull caught then
        failwith "Expected an exception"

    caught

let private templatePins () =
    equal """<div class="panel" title="T &amp;">Hello <!--!-->&lt;Ada&gt;<!--!-->!</div>""" (renderOmit greeting greetingProps)
    equal """<div class="panel" title="T &amp;"><span>&lt;Ada&gt;</span></div>""" (renderOmit named greetingProps)
    equal "<button></button>" (renderOmit clicking greetingProps)
    equal "<div></div>" (renderOmit nullTitle greetingProps)

    equal
        """<div class="card">A<!--[--><em>&lt;b&gt;</em><!--]--></div>"""
        (renderOmit
            holed
            {
                Caption = "A"
                Body = Program.element (HtmlTag "em") [||] [| Program.text "<b>" |]
            })

    equal
        """<div class="card">B<!--[--><!--]--></div>"""
        (renderOmit
            holed
            {
                Caption = "B"
                Body = Program.fragment [||]
            })

    equal "<ul><!--[--><li>a</li><li>&lt;b&gt;</li><!--]--></ul>" (renderOmit listed listedItems)
    equal "<ul><!--[--><!--]--></ul>" (renderOmit listed [||])
    equal """<div>&lt;Ada&gt;<span title="3">3</span></div>""" (renderOmit carded greetingProps)
    equal "<span>a<!--!-->&lt;Ada&gt;</span>" (renderOmit besideText greetingProps)

    equal
        [
            """<button title="a" aria-busy="false" data-state="x">go</button>"""
            """<button title="b" id="n" disabled="" aria-expanded="false" aria-busy="true" data-state="y">go</button>"""
            """<button title="c" id="m" aria-expanded="true" aria-busy="false" data-state="z">go</button>"""
            """<button aria-busy="false" data-state="n">go</button>"""
        ]
        [ for flags in flagCases -> renderOmit flagsTemplate flags ]

    equal
        """<div><button aria-expanded="false" title="t"></button><button disabled="" aria-expanded="true" id="i" data-k="v"></button></div>"""
        (renderOmit staticTemplate ())

    equal """<div class="panel" title="T &amp;">Hello <!--!-->&lt;Ada&gt;<!--!-->!</div><span>&lt;Ada&gt;</span>""" (renderOmit pair greetingProps)

let private armPins () =
    equal "<div><!--[--><span>working 3</span><!--]--></div>" (renderOmit staged (Working 3))
    equal "<div><!--[--><span>complete &lt;ok&gt;</span><!--]--></div>" (renderOmit staged (Complete "<ok>"))
    equal "<div><!--[--><span>4</span><!--]--></div>" (renderOmit decades 42)

let private boundaryPins () =
    use graph = new Graph ()
    boundarySource <- graph.Run (fun () -> Api.createAsyncSource<string>())
    equal "<div><!--[--><span>loading</span><!--]--></div>" (renderOmit guarded ())
    graph.Run (fun () -> boundarySource.Settle "<A>")
    equal "<div><!--[--><span>&lt;A&gt;</span><!--]--></div>" (renderOmit guarded ())
    graph.Run (fun () -> boundarySource.Fail (Exception "<bad>"))
    equal "<div><!--[--><span>&lt;bad&gt;</span><!--]--></div>" (renderOmit guarded ())

let private edgePins () =
    equal "<span></span>" (renderOmit emptyText "")
    equal "<div><br></div>" (renderOmit voidChildren ())
    equal "<table><tr><td>x</td></tr></table>" (renderOmit rowInTable ())

let private cleanupLog = ResizeArray<string>()

let private loggedChild =
    Component.define (fun setup () -> setup.OnCleanup (fun () -> cleanupLog.Add "child")) (fun t -> t.span [||] [| t.text "c" |])

let private loggedRow =
    Component.define
        (fun setup (item: unit -> int) ->
            let value = item ()
            setup.OnCleanup (fun () -> cleanupLog.Add ("row " + string value)))
        (fun t -> t.li [||] [||])

let private loggedParent =
    Component.define
        (fun setup (rows: int array) ->
            setup.OnCleanup (fun () -> cleanupLog.Add "parent")
            rows)
        (fun t -> t.div [||] [| t.render loggedChild (fun _ -> ()); t.ul [||] [| t.each id id loggedRow |] |])

let private inlinedCleanupOrder () =
    cleanupLog.Clear ()
    let html = renderOmit loggedParent [| 1; 2 |]
    equal "<div><span>c</span><ul><!--[--><li></li><li></li><!--]--></ul></div>" html
    equal [ "row 1"; "row 2"; "parent"; "child" ] (List.ofSeq cleanupLog)

let private rawContent =
    Component.define (fun _ (markup: string) -> markup) (fun t ->
        t.fragment
            [|
                t.div [| t.Unsafe.html (fun markup -> UnsafeHtml.Trust markup) |] [||]
                t.span [||] [| t.text "<b>" |]
            |])

let private rawHtmlPin () =
    equal "<div><b>raw</b> &amp; <!--note--></div><span>&lt;b&gt;</span>" (renderOmit rawContent "<b>raw</b> &amp; <!--note-->")

let private rawHtmlForgesNoMarker () =
    let rejected markup =
        let error = failure (fun () -> renderOmit rawContent markup |> ignore)

        error :? InvalidOperationException
        && error.Message = "Raw HTML cannot contain a comment that starts with [ or equals ]"

    let markers =
        [
            "<!--[-->x"
            "a<!--]-->"
            "<!--[a:0123abcd-->"
            "<!--[ x"
            "<![>"
            "</[>"
            "<!]>"
            "<!--x--!><!--[-->"
            "<!--]--!>"
            "<!--]-"
            "</]"
            "<![CDATA[x]]>"
        ]

    equal (List.map (fun _ -> true) markers) [ for markup in markers -> rejected markup ]

    let accepted =
        "<!-- [--><!--]x--><!-- ]--><!--]--x--><!doctype html><?xml?><!x></ x>"

    equal
        ("<div>"
         + accepted
         + "</div><span>&lt;b&gt;</span>")
        (renderOmit rawContent accepted)

let private nullEscape () =
    equal ("", "") (Html.escape false null, Html.escape true null)

    equal
        "<div title=\"\"></div>"
        (Html.render Omit (Program.run (Program.element Div [| Attribute ("title", null) |] [| Program.bind (=) (fun () -> (null: string)) |])))

let private preformatted =
    Component.define (fun _ (value: string) -> value) (fun t ->
        t.fragment
            [|
                t.pre [||] [| t.text (fun value -> value) |]
                t.element (HtmlTag "listing") [||] [| t.text "\nstatic" |]
                t.div [||] [| t.text (fun value -> value) |]
            |])

let private leadingLineFeed () =
    equal "<pre>\n\nslot</pre><listing>\n\nstatic</listing><div>\nslot</div>" (renderOmit preformatted "\nslot")
    equal "<pre>slot</pre><listing>\n\nstatic</listing><div>slot</div>" (renderOmit preformatted "slot")

let private escapeCorpus () =
    equal "&lt;&amp;&gt;&#13;\"'" (Html.escape false "<&>\r\"'")
    equal "&lt;&amp;&gt;&#13;&quot;'" (Html.escape true "<&>\r\"'")
    equal "\uD83D\uDE00 plain" (Html.escape false "\uD83D\uDE00 plain")
    equal "&lt;!--]--&gt;" (Html.escape false "<!--]-->")
    equal "&lt;/script&gt;" (Html.escape true "</script>")
    equal "a&amp;&amp;b" (Html.escape false "a&&b")
    equal "" (Html.escape true "")

let private namesAtDefinition () =
    let define (part: TemplateBuilder<unit> -> TemplateView<unit>) () =
        Component.define (fun _ () -> ()) part |> ignore

    let rejected =
        [
            failure (define (fun t -> t.div [| t.Attrs.attribute ("onclick", "x") |] [||]))
            failure (define (fun t -> t.div [| t.Attrs.attribute ("srcdoc", "x") |] [||]))
            failure (define (fun t -> t.div [| t.Attrs.attribute ("Title", "x") |] [||]))
            failure (define (fun t -> t.div [| t.Attrs.attribute ("", "x") |] [||]))
            failure (define (fun t -> t.element (HtmlTag "DIV") [||] [||]))
            failure (define (fun t -> t.element (HtmlTag "") [||] [||]))
        ]

    equal [ true; true; true; true; true; true ] [ for error in rejected -> error.Message.Contains "names must" ]

let private sharedPlan () =
    equal true (obj.ReferenceEquals (serverPlan greeting, serverPlan greeting))

    match (serverPlan staticTemplate).Ops with
    | [| Static _ |] -> ()
    | ops -> failwithf "Expected one merged static segment; got %d operations" ops.Length

let private browserRejection () =
    let error =
        failure (fun () ->
            Html.render Reject (Program.run (Component.render clicking greetingProps))
            |> ignore)

    equal (true, "Browser capability requires explicit Omit policy") (error :? InvalidOperationException, error.Message)

/// <summary>An owner whose cleanups run when it is disposed, after its children's.</summary>
type private StandInOwner() =
    member val Children = ResizeArray<StandInOwner>()
    member val Cleanups = ResizeArray<unit -> unit>()

    member this.Dispose() =
        for child in this.Children do
            child.Dispose ()

        for cleanup in this.Cleanups do
            cleanup ()

/// <summary>A server scope that gives each nested instance a child owner of <c>owner</c>.</summary>
type private OwnedScope(owner: StandInOwner) =
    inherit ServerScope()

    override _.Nest body =
        let child = StandInOwner ()
        owner.Children.Add child

        body
            (OwnedScope child)
            {
                OnCleanup = fun cleanup -> child.Cleanups.Add cleanup
            }

    override this.Attempt body = body this

let private holeChild =
    Component.define (fun setup () -> setup.OnCleanup (fun () -> cleanupLog.Add "hole child")) (fun t -> t.span [||] [| t.text "in" |])

let private holeHolder =
    Component.define
        (fun setup (body: Program.View) ->
            setup.OnCleanup (fun () -> cleanupLog.Add "holder")
            body)
        (fun t -> t.div [||] [| t.view id |])

let private holeTemplateOwnedByRequest () =
    cleanupLog.Clear ()
    let root = StandInOwner ()
    let sink = HtmlSink ()
    let backend = HtmlBackend (Omit, sink)
    backend.Scope <- OwnedScope root
    Program.run (Component.render holeHolder (Component.render holeChild ())) backend
    let during = List.ofSeq cleanupLog
    root.Dispose ()

    equal
        ("<div><!--[--><span>in</span><!--]--></div>", List.empty<string>, [ "hole child"; "holder" ])
        (sink.ToString (), during, List.ofSeq cleanupLog)

let private requestHolder =
    Component.define
        (fun setup (body: Program.View) ->
            setup.OnCleanup (fun () -> cleanupLog.Add "holder")
            body)
        (fun t -> t.div [||] [| t.view id; t.text (fun _ -> string cleanupLog.Count) |])

let private holeTemplateOwnedByRequestScope () =
    cleanupLog.Clear ()

    let html =
        Server.render Server.Options.defaults requestHolder (Component.render holeChild ())

    equal ("<!--[r--><div><!--[--><span>in</span><!--]-->0</div><!--]-->", [ "hole child"; "holder" ]) (html, List.ofSeq cleanupLog)

let private requestRoot () =
    let log = ResizeArray<string>()
    let owners = ResizeArray<Owner>()
    let graphs = ResizeArray<Graph>()

    let child =
        Component.define
            (fun setup (name: string) ->
                owners.Add (Api.getOwner ())
                setup.OnCleanup (fun () -> log.Add ("cleanup " + name))
                Api.createSignal name)
            (fun t -> t.span [||] [| t.text (fun (name: Signal<string>) -> name.Value) |])

    let parent =
        Component.define
            (fun setup () ->
                graphs.Add Graph.Current
                owners.Add (Api.getOwner ())
                setup.OnCleanup (fun () -> log.Add "cleanup parent"))
            (fun t -> t.div [||] [| t.render child (fun _ -> "a"); t.render child (fun _ -> "b") |])

    equal "<!--[r--><div><span>a</span><span>b</span></div><!--]-->" (Server.render Server.Options.defaults parent ())
    equal ("cleanup parent", [ "cleanup a"; "cleanup b"; "cleanup parent" ]) (Seq.last log, List.sort (List.ofSeq log))
    equal true (owners |> Seq.forall _.IsDisposed)
    equal true graphs[0].Root.IsDisposed

let private setupFailureDisposes () =
    let log = ResizeArray<string>()
    let owners = ResizeArray<Owner>()

    let cleaned =
        Component.define
            (fun setup () ->
                owners.Add (Api.getOwner ())
                setup.OnCleanup (fun () -> log.Add "first"))
            (fun t -> t.span [||] [||])

    let throwing =
        Component.define (fun _ () -> failwith "second failed") (fun t -> t.span [||] [||])

    let parent =
        Component.define (fun setup () -> setup.OnCleanup (fun () -> log.Add "parent")) (fun t ->
            t.div [||] [| t.render cleaned ignore; t.render throwing ignore |])

    let error =
        failure (fun () ->
            Server.render Server.Options.defaults parent ()
            |> ignore)

    equal ("second failed", [ "first"; "parent" ], true) (error.Message, List.ofSeq log, owners |> Seq.forall _.IsDisposed)

let private cleanupAfterSetupRejected () =
    let captured: Setup option ref = ref None

    let capturing =
        Component.define (fun setup () -> captured.Value <- Some setup) (fun t -> t.span [||] [||])

    Server.render Server.Options.defaults capturing ()
    |> ignore

    let error = failure (fun () -> captured.Value.Value.OnCleanup ignore)
    equal true (error :? InvalidOperationException)

let private duplicateKeys () =
    let items = [| { Id = 1; Text = "a" }; { Id = 1; Text = "b" } |]

    let expected =
        failure (fun () -> Projection.ensureUniqueKeys (fun (item: obj) -> box (unbox<Listed> item).Id) (Array.map box items))

    let actual =
        failure (fun () ->
            Server.render
                { Server.Options.defaults with
                    Policy = Omit
                }
                listed
                items
            |> ignore)

    equal (true, expected.Message) (actual :? InvalidOperationException, actual.Message)

let private renderToSegments () =
    let written = ResizeArray<string>()
    Server.renderTo Server.Options.defaults named greetingProps written.Add
    equal (Server.render Server.Options.defaults named greetingProps) (String.concat "" written)
    let failed = ResizeArray<string>()

    let throwing =
        Component.define (fun _ () -> failwith "setup") (fun t -> t.span [||] [||])

    failure (fun () -> Server.renderTo Server.Options.defaults throwing () failed.Add)
    |> ignore

    equal 0 failed.Count

let private aggregateFailures () =
    let cleanupFails =
        Component.define
            (fun setup (fails: bool) ->
                setup.OnCleanup (fun () -> failwith "cleanup failed")

                if fails then
                    failwith "setup failed")
            (fun t -> t.span [||] [||])

    let shape (error: exn) (inner: string list) =
#if FABLE_COMPILER
        // Fable's exception stores the inner array as its inner exception.
        let count = (unbox<exn array>(box error.InnerException)).Length
#else
        let count = (error :?> AggregateException).InnerExceptions.Count
#endif
        count,
        inner
        |> List.forall (fun (part: string) -> error.Message.Contains part)

    let setupAndCleanup =
        failure (fun () ->
            Server.render Server.Options.defaults cleanupFails true
            |> ignore)

    let cleanupOnly =
        failure (fun () ->
            Server.render Server.Options.defaults cleanupFails false
            |> ignore)

    equal
        ((2, true), (1, true))
        (shape setupAndCleanup [ "Server render failed: "; "setup failed"; "cleanup failed" ],
         shape cleanupOnly [ "Server render cleanup failed: "; "cleanup failed" ])

let private omitOptions =
    { Server.Options.defaults with
        Policy = Omit
    }

type private PageProps = { Title: string; Description: string }

let private description (t: TemplateBuilder<PageProps>) =
    t.Document.meta
        [|
            t.Attrs.attribute ("name", "description")
            t.Attrs.attribute ("content", fun (page: PageProps) -> Some page.Description)
        |]

let private headChild =
    Component.define (fun _ (page: PageProps) -> page) (fun t ->
        t.fragment [| t.Document.title (fun (page: PageProps) -> page.Title); description t |])

let private preload (t: TemplateBuilder<'inst>) (href: string) =
    t.Document.link [| t.Attrs.attribute ("rel", "preload"); t.Attrs.attribute ("href", href) |]

let private headParent =
    Component.define (fun _ () -> ()) (fun t ->
        t.div
            [||]
            [|
                t.Document.title "Parent"
                preload t "/a.css"
                t.Document.meta
                    [|
                        t.Attrs.attribute ("name", "description")
                        t.Attrs.attribute ("content", "parent")
                    |]
                t.render headChild (fun _ ->
                    {
                        Title = "Child"
                        Description = "child"
                    })
                preload t "/b.css"
                t.span [||] [| t.text "body" |]
            |])

let private headOrder () =
    equal
        {
            Server.Page.Body = "<!--[r--><div><span>body</span></div><!--]-->"
            Server.Page.Head =
                """<title data-ranvier-head="title">Child</title>"""
                + """<link data-ranvier-head="" rel="preload" href="/a.css">"""
                + """<meta data-ranvier-head="meta:name=description" name="description" content="child">"""
                + """<link data-ranvier-head="" rel="preload" href="/b.css">"""
        }
        (Server.renderPage Server.Options.defaults headParent ())

let private escapedPage =
    Component.define (fun _ (page: PageProps) -> page) (fun t ->
        t.fragment
            [|
                t.Document.title (fun (page: PageProps) -> page.Title)
                description t
                t.Document.meta [| t.Attrs.attribute ("charset", "utf-8") |]
                t.Document.link (
                    "canonical",
                    [|
                        t.Attrs.attribute ("rel", "canonical")
                        t.Attrs.attribute ("href", fun _ -> Some "/p?a=1&b=2")
                    |]
                )
                t.Document.link (
                    "icon",
                    [|
                        t.Attrs.attribute ("rel", "icon")
                        t.Attrs.attribute ("href", "/i.png")
                        t.Attrs.attribute ("type", fun _ -> None)
                    |]
                )
                preload t "/s.css"
                t.h1 [||] [| t.text (fun (page: PageProps) -> page.Title) |]
            |])

let private escapedProps =
    {
        Title = "<a & \"b\">"
        Description = "say \"hi\" & <go>"
    }

let private escapedHead =
    """<title data-ranvier-head="title">&lt;a &amp; "b"&gt;</title>"""
    + """<meta data-ranvier-head="meta:name=description" name="description" content="say &quot;hi&quot; &amp; &lt;go&gt;">"""
    + """<meta data-ranvier-head="meta:charset" charset="utf-8">"""
    + """<link data-ranvier-head="link:canonical" rel="canonical" href="/p?a=1&amp;b=2">"""
    + """<link data-ranvier-head="link:key:icon" rel="icon" href="/i.png">"""
    + """<link data-ranvier-head="" rel="preload" href="/s.css">"""

let private pageEscapes () =
    let page = Server.renderPage omitOptions escapedPage escapedProps
    equal escapedHead page.Head
    equal (Server.render omitOptions escapedPage escapedProps) page.Body
    equal """<!--[r--><h1>&lt;a &amp; "b"&gt;</h1><!--]-->""" page.Body

let private headPolicy () =
    let error =
        failure (fun () ->
            Server.render Server.Options.defaults escapedPage escapedProps
            |> ignore)

    equal (true, "Document metadata requires Server.renderPage") (error :? InvalidOperationException, error.Message)
    equal """<!--[r--><h1>&lt;a &amp; "b"&gt;</h1><!--]-->""" (Server.render omitOptions escapedPage escapedProps)
    equal escapedHead (Server.renderPage Server.Options.defaults escapedPage escapedProps).Head

let private holedHeads () =
    let child = { Title = "Held"; Description = "held" }

    let expected =
        """<title data-ranvier-head="title">Held</title>"""
        + """<meta data-ranvier-head="meta:name=description" name="description" content="held">"""

    let headOf (body: Program.View) =
        (Server.renderPage Server.Options.defaults holed { Caption = "C"; Body = body }).Head

    equal
        [ expected; expected ]
        [
            headOf (Component.render headChild child)
            headOf (Component.renderWith Program.ops headChild child)
        ]

/// <summary>A page writing a title before <c>pause</c> and a meta after it, both named by the props.</summary>
let private namedPage (pause: Component<unit>) =
    Component.define (fun _ (name: string) -> name) (fun t ->
        t.fragment
            [|
                t.Document.title (fun name -> "title " + name)
                t.render pause ignore
                t.Document.meta
                    [|
                        t.Attrs.attribute ("name", "description")
                        t.Attrs.attribute ("content", fun name -> Some ("meta " + name))
                    |]
            |])

let private namedHead (name: string) =
    "<title data-ranvier-head=\"title\">title "
    + name
    + "</title><meta data-ranvier-head=\"meta:name=description\" name=\"description\" content=\"meta "
    + name
    + "\">"

#if FABLE_COMPILER
let private sequentialHeads () =
    let page = namedPage (Component.define (fun _ () -> ()) (fun t -> t.span [||] [||]))

    let left = Server.renderPage Server.Options.defaults page "left"
    let right = Server.renderPage Server.Options.defaults page "right"
    equal (namedHead "left", namedHead "right") (left.Head, right.Head)
#endif

let cases: (string * (unit -> unit)) array =
    [|
        "renderPage writes keys in first-activation order and the last entry for a key", headOrder
        "renderPage escapes titles and attribute values and marks every element", pageEscapes
        "render keeps the document entry policy and renderPage collects whatever the policy", headPolicy
        "renderPage collects the entries of a compiled and an expanded template in a view hole", holedHeads
#if FABLE_COMPILER
        "two concurrent server requests collect only their own entries", sequentialHeads
#endif
        "server plan matches the current writer for every template fixture", templatePins
        "server plan matches the current writer for arms", armPins
        "server plan matches the current writer for boundaries", boundaryPins
        "empty text, void children and a tr in a table render as recorded", edgePins
        "an inlined child's cleanups run with its parent's, after the rows' own", inlinedCleanupOrder
        "raw HTML content is written verbatim", rawHtmlPin
        "raw HTML containing a marker-shaped comment raises", rawHtmlForgesNoMarker
        "a pre, textarea or listing first child text starting with a line feed gets a second one", leadingLineFeed
        "escape corpus is byte-identical on .NET and Fable", escapeCorpus
        "null text and attribute values escape as empty strings on .NET and Fable", nullEscape
        "attribute and tag names outside the validated set fail at definition time", namesAtDefinition
        "a definition's server plan is compiled once and merges adjacent static markup", sharedPlan
        "a browser-only part under Reject raises the browser capability error", browserRejection
        "a template in a view hole is owned by the backend's active scope", holeTemplateOwnedByRequest
        "a template in a view hole is owned by the request and cleaned up child-first after the render", holeTemplateOwnedByRequestScope
        "a request owns every instance and disposes them child-first after the render", requestRoot
        "setup failure disposes the request", setupFailureDisposes
        "OnCleanup after setup raises on the server", cleanupAfterSetupRejected
        "duplicate keys fail the server render with the core's exception", duplicateKeys
        "renderTo writes the rendered segments after success and nothing after failure", renderToSegments
        "a render whose cleanups fail raises an aggregate holding and naming every error", aggregateFailures
    |]

#if !FABLE_COMPILER
let private escapeKeepsReference () =
    let text = "plain text without markup"
    equal true (obj.ReferenceEquals (text, Html.escape false text))
    equal true (obj.ReferenceEquals (text, Html.escape true text))

/// <summary>Runs <c>count</c> calls of <c>body</c> on pool threads released together.</summary>
let private together (count: int) (body: unit -> 'value) : 'value array =
    use barrier = new System.Threading.Barrier (count)

    [|
        for _ in 1..count ->
            System.Threading.Tasks.Task.Run (fun () ->
                if not (barrier.SignalAndWait (TimeSpan.FromSeconds 10.)) then
                    failwith "Barrier timed out"

                body ())
    |]
    |> Array.map _.Result

let private concurrentPublication () =
    let fresh =
        Component.define (fun _ () -> ()) (fun t -> t.span [||] [| t.text "fresh" |])

    let plans = together 8 (fun () -> serverPlan fresh)

    equal
        true
        (plans
         |> Array.forall (fun plan -> obj.ReferenceEquals (plan, plans[0])))

    let arm =
        Component.define (fun _ (_: unit -> int) -> ()) (fun t -> t.span [||] [||])

    let readers = together 8 (fun () -> (TemplateBuilder<unit>().arm arm 1).Reader)

    equal
        true
        (readers
         |> Array.forall (fun reader -> obj.ReferenceEquals (reader, readers[0])))

let private concurrentRequests () =
    use barrier = new System.Threading.Barrier (2)
    let graphs = System.Collections.Concurrent.ConcurrentBag<Graph>()

    let waiting =
        Component.define
            (fun _ (name: string) ->
                graphs.Add Graph.Current

                if not (barrier.SignalAndWait (TimeSpan.FromSeconds 10.)) then
                    failwith "Barrier timed out"

                Api.createSignal name)
            (fun t -> t.span [||] [| t.text (fun (name: Signal<string>) -> name.Value) |])

    let request name =
        System.Threading.Tasks.Task.Run (fun () -> Server.render Server.Options.defaults waiting name)

    let left, right = request "left", request "right"
    equal ("<!--[r--><span>left</span><!--]-->", "<!--[r--><span>right</span><!--]-->") (left.Result, right.Result)
    equal [ true; true ] [ for graph in graphs -> graph.Root.IsDisposed ]
    equal false (obj.ReferenceEquals (Seq.head graphs, Seq.last graphs))

/// <summary>Reads and writes a signal of a graph outside the request from a request on a pool thread.</summary>
let private sharedSignal (affinity: ThreadAffinity) =
    use outer = new Graph (GraphOptions.Default.WithThreadAffinity affinity)
    let shared = outer.Run (fun () -> Api.createSignal "s")

    let reading =
        Component.define (fun _ () -> shared) (fun t -> t.span [||] [| t.text (fun (s: Signal<string>) -> s.Value) |])

    let writing =
        Component.define (fun _ (value: string) -> shared.Value <- value) (fun t -> t.span [||] [||])

    let onPool (body: unit -> string) =
        try
            Ok (System.Threading.Tasks.Task.Run(body).Result)
        with :? AggregateException as error ->
            Error error.InnerException

    equal (Ok "<!--[r--><span>s</span><!--]-->") (onPool (fun () -> Server.render Server.Options.defaults reading ()))

    match affinity, onPool (fun () -> Server.render Server.Options.defaults writing "w") with
    | ThreadAffinity.Guarded, Error error -> equal true (error :? InvalidOperationException)
    | ThreadAffinity.Unchecked, Ok _ ->
        equal (Ok "<!--[r--><span>w</span><!--]-->") (onPool (fun () -> Server.render Server.Options.defaults reading ()))
    | _, outcome -> failwithf "Unexpected outcome %A under %O" outcome affinity

/// <summary>The messages of a render failure: an <c>AggregateException</c>'s inner messages, or the message itself.</summary>
let private messages (error: exn) : string list =
    match error with
    | :? AggregateException as aggregate -> [ for inner in aggregate.InnerExceptions -> inner.Message ]
    | error -> [ error.Message ]

let private concurrentTeardown () =
    let parties = 8
    use barrier = new System.Threading.Barrier (parties)

    let throwing =
        Component.define
            (fun setup (name: string) ->
                setup.OnCleanup (fun () ->
                    if not (barrier.SignalAndWait (TimeSpan.FromSeconds 10.)) then
                        failwith "Barrier timed out"

                    failwith ("cleanup " + name))

                name)
            (fun t -> t.span [||] [| t.text id |])

    let request name =
        System.Threading.Tasks.Task.Run (fun () ->
            failure (fun () ->
                Server.render Server.Options.defaults throwing name
                |> ignore))

    let outcome (error: exn) =
        error.Message.StartsWith "Server render cleanup failed: ", messages error

    let names = [ for party in 1..parties -> "r" + string party ]

    let rounds =
        [
            for _ in 1..50 ->
                let requests = [ for name in names -> request name ]
                [ for request in requests -> outcome request.Result ]
        ]

    equal (List.replicate 50 [ for name in names -> true, [ "cleanup " + name ] ]) rounds

let private concurrentHeads () =
    let parties = 8
    use barrier = new System.Threading.Barrier (parties)

    let pause =
        Component.define
            (fun _ () ->
                if not (barrier.SignalAndWait (TimeSpan.FromSeconds 10.)) then
                    failwith "Barrier timed out")
            (fun t -> t.span [||] [||])

    let page = namedPage pause

    let request name =
        System.Threading.Tasks.Task.Run (fun () -> Server.renderPage Server.Options.defaults page name)

    let names = [ for party in 1..parties -> "r" + string party ]

    let rounds =
        [
            for _ in 1..50 ->
                let requests = [ for name in names -> request name ]
                [ for request in requests -> request.Result.Head ]
        ]

    equal (List.replicate 50 [ for name in names -> namedHead name ]) rounds

let private collectorTruncates () =
    let heads = HeadCollector ()
    heads.Add ("title", "<t1>")
    heads.Add (null, "<l1>")
    heads.Add ("meta:name=d", "<m1>")
    let mark = heads.Count
    heads.Add ("title", "<t2>")
    heads.Add (null, "<l2>")
    heads.Add ("meta:name=d", "<m2>")
    heads.Add ("link:canonical", "<c>")
    equal "<t2><l1><m2><l2><c>" (heads.ToString ())
    heads.Truncate mark
    equal (3, "<t1><l1><m1>") (heads.Count, heads.ToString ())

let dotnetCases: (string * (unit -> unit)) array =
    [|
        "escape returns the input string when nothing needs escaping", escapeKeepsReference
        "concurrent first renders publish one plan and one arm reader", concurrentPublication
        "two concurrent requests render their own props", concurrentRequests
        "a module-level signal read by two requests is shared: a Guarded graph rejects a request's write",
        fun () -> sharedSignal ThreadAffinity.Guarded
        "a module-level signal read by two requests is shared: an Unchecked graph takes a request's write",
        fun () -> sharedSignal ThreadAffinity.Unchecked
        "two requests whose cleanups fail concurrently each report only their own error", concurrentTeardown
        "two concurrent server requests collect only their own entries", concurrentHeads
        "truncating the head collector drops later entries and restores an overridden key's earlier entry", collectorTruncates
    |]
#endif
