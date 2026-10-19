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

    equal (true, true) (error :? InvalidOperationException, error.Message.Contains "<button> listener click")

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
type private OwnedScope(owner: StandInOwner, identity: IdentityScope) =
    inherit ServerScope()

    override _.Nest body =
        let child = StandInOwner ()
        owner.Children.Add child

        body
            (OwnedScope (child, identity))
            {
                OnCleanup = fun cleanup -> child.Cleanups.Add cleanup
                CreateId = identity.CreateId
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
    backend.Scope <- OwnedScope (root, backend.Identity)
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

let private earlyChild =
    Component.define (fun _ () -> ()) (fun t ->
        t.fragment
            [|
                t.Document.title "Child"
                t.Document.meta
                    [|
                        t.Attrs.attribute ("name", "description")
                        t.Attrs.attribute ("content", "child")
                    |]
                preload t "/c.css"
            |])

let private lateParent =
    Component.define (fun _ () -> ()) (fun t ->
        t.div
            [||]
            [|
                t.render earlyChild ignore
                t.Document.title "Parent"
                t.Document.meta
                    [|
                        t.Attrs.attribute ("name", "description")
                        t.Attrs.attribute ("content", "parent")
                    |]
                preload t "/p.css"
            |])

let private titledRow =
    Component.define (fun _ (item: unit -> int) -> item) (fun t -> t.Document.title (fun (item: unit -> int) -> "Row " + string (item ())))

let private rowsThenChild =
    Component.define (fun _ (rows: int array) -> rows) (fun t -> t.div [||] [| t.ul [||] [| t.each id id titledRow |]; t.render earlyChild ignore |])

let private activationOrder () =
    equal
        ("""<title data-ranvier-head="title">Child</title>"""
         + """<meta data-ranvier-head="meta:name=description" name="description" content="child">"""
         + """<link data-ranvier-head="" rel="preload" href="/p.css">"""
         + """<link data-ranvier-head="" rel="preload" href="/c.css">""")
        (Server.renderPage Server.Options.defaults lateParent ()).Head

    equal
        ("""<title data-ranvier-head="title">Row 2</title>"""
         + """<meta data-ranvier-head="meta:name=description" name="description" content="child">"""
         + """<link data-ranvier-head="" rel="preload" href="/c.css">""")
        (Server.renderPage Server.Options.defaults rowsThenChild [| 1; 2 |]).Head

let private innerRow =
    Component.define (fun _ (item: unit -> int) -> item) (fun t -> t.fragment [| t.Document.title "Inner"; preload t "/inner.css" |])

let private outerRow =
    Component.define (fun _ (item: unit -> int) -> item ()) (fun t ->
        t.fragment
            [|
                t.Document.title (fun (index: int) -> "Row " + string index)
                t.Document.link
                    [|
                        t.Attrs.attribute ("rel", "preload")
                        t.Attrs.attribute ("href", fun (index: int) -> Some ("/row-" + string index + ".css"))
                    |]
                t.ul
                    [||]
                    [|
                        t.each id (fun (index: int) -> if index = 1 then [| 10 |] else [||]) innerRow
                    |]
            |])

let private nestedLists =
    Component.define (fun _ () -> [| 1; 2 |]) (fun t -> t.div [||] [| t.each id id outerRow |])

let private nestedListsHead =
    """<title data-ranvier-head="title">Inner</title>"""
    + """<link data-ranvier-head="" rel="preload" href="/row-1.css">"""
    + """<link data-ranvier-head="" rel="preload" href="/row-2.css">"""
    + """<link data-ranvier-head="" rel="preload" href="/inner.css">"""

let private containerOrder () =
    let expandedHole =
        Server.renderPage
            Server.Options.defaults
            holed
            {
                Caption = "C"
                Body = Component.renderWith Program.ops rowsThenChild [| 1; 2 |]
            }

    equal
        (nestedListsHead,
         nestedListsHead,
         """<title data-ranvier-head="title">Row 2</title>"""
         + """<meta data-ranvier-head="meta:name=description" name="description" content="child">"""
         + """<link data-ranvier-head="" rel="preload" href="/c.css">""")
        ((Server.renderPage Server.Options.defaults nestedLists ()).Head,
         (Server.renderPage
             Server.Options.defaults
             holed
             {
                 Caption = "C"
                 Body = Component.renderWith Program.ops nestedLists ()
             })
             .Head,
         expandedHole.Head)

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

/// <summary>A page writing a title before <c>pause</c> and a meta after it, both taken from the props.</summary>
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

let private serverIdentity () =
    identityLog.Clear ()

    Server.render
        { Server.Options.defaults with
            IdPrefix = "a"
        }
        identified
        [| 7; 8 |]
    |> ignore

    equal (identityIds "a") (List.sort (List.ofSeq identityLog))

let private createIdAfterSetup () =
    let captured: Setup option ref = ref None

    let capturing =
        Component.define (fun setup () -> captured.Value <- Some setup) (fun t -> t.span [||] [||])

    Server.render Server.Options.defaults capturing ()
    |> ignore

    equal true (failure (fun () -> captured.Value.Value.CreateId () |> ignore) :? InvalidOperationException)

let private authoredIds () =
    let ids = ResizeArray<string>()

    let authored =
        Program.createComponent (fun setup ->
            ids.Add (setup.CreateId ())
            Program.text "x")

    Html.render Reject (Program.run (Program.fragment [| authored; authored |]))
    |> ignore

    identityLog.Clear ()

    Html.render Reject (Program.run (Component.render idLeaf ()))
    |> ignore

    equal ([ "r-0-a0"; "r-0-a1" ], [ "r-0-0" ]) (List.ofSeq ids, List.ofSeq identityLog)

let private holeIdentity () =
    identityLog.Clear ()
    let html = Server.render Server.Options.defaults idHole ()

    equal
        ("""<!--[r--><div id="r-0-0"><!--[--><i id="r-0.r0.0-0"></i><i id="r-0.r0.0.a0-0"></i><!--]--></div><!--]-->""", holeIds)
        (html, List.ofSeq identityLog)

/// <summary>The <c>id</c> attribute values of <c>html</c>, in document order.</summary>
let private htmlIds (html: string) =
    [
        for found in Text.RegularExpressions.Regex.Matches (html, " id=\"([^\"]*)\"") do
            found.Groups[1].Value
    ]

let private nestedIdentity () =
    identityLog.Clear ()
    let html = Server.render Server.Options.defaults idNested [| 1; 2 |]
    equal (nestedIds, nestedIds) (List.ofSeq identityLog, htmlIds html)

let private holesIdentity () =
    identityLog.Clear ()
    let html = Server.render Server.Options.defaults idHoles ()
    equal (holesIds, holesIds) (List.ofSeq identityLog, htmlIds html)

let private createIdInLaterSetup () =
    let error =
        failure (fun () ->
            Server.render Server.Options.defaults (capturedCreateId ()) ()
            |> ignore)

    equal (true, "CreateId is valid only while a component's setup runs") (error :? InvalidOperationException, error.Message)

let private serialised =
    Component.define (fun _ () -> ()) (fun t ->
        t.div
            [|
                t.Props.className (fun _ -> "a<b")
                t.Props.tabIndex (fun _ -> 2.0)
                t.Props.ariaBusy (fun _ -> Some "true")
            |]
            [|
                t.input
                    [|
                        t.Props.Input.value (fun _ -> "v\"")
                        t.Props.Input.``checked`` (fun _ -> true)
                        t.Props.Input.disabled (fun _ -> false)
                    |]
                t.button [| t.Props.Button.disabled true |] [||]
            |])

let private browserOnlyProperty =
    Component.define (fun _ () -> ()) (fun t -> t.div [| t.Props.accessKey (fun _ -> "k") |] [| t.text "x" |])

let private serialisedProperties () =
    equal
        """<!--[r--><div class="a&lt;b" tabindex="2" aria-busy="true"><input value="v&quot;" checked=""><button disabled=""></button></div><!--]-->"""
        (Server.render Server.Options.defaults serialised ())

    equal
        "<!--[r--><div>x</div><!--]-->"
        (Server.render
            { Server.Options.defaults with
                Policy = Omit
            }
            browserOnlyProperty
            ())

    let error =
        failure (fun () ->
            Server.render Server.Options.defaults browserOnlyProperty ()
            |> ignore)

    equal (true, true) (error :? InvalidOperationException, error.Message.Contains "<div> property")

let private browserPartsUninvoked () =
    let mutable invoked = 0

    let hook =
        Program.element
            Div
            [|
                browserModifier (fun _ ->
                    invoked <- invoked + 1
                    ignore)
            |]
            [||]

    let parts =
        Component.define (fun _ () -> ()) (fun t ->
            t.div
                [||]
                [|
                    t.button [| t.Events.onClick (fun _ _ -> invoked <- invoked + 1) |] [||]
                    t.view (fun _ -> hook)
                |])

    equal
        "<!--[r--><div><button></button><!--[--><div></div><!--]--></div><!--]-->"
        (Server.render
            { Server.Options.defaults with
                Policy = Omit
            }
            parts
            ())

    failure (fun () ->
        Server.render Server.Options.defaults parts ()
        |> ignore)
    |> ignore

    equal 0 invoked

let private duplicateAttributeSource () =
    let both =
        Component.define (fun _ () -> ()) (fun t -> t.div [| t.Attrs.className "x"; t.Props.className (fun _ -> "y") |] [||])

    let error =
        failure (fun () ->
            Server.render Server.Options.defaults both ()
            |> ignore)

    equal (true, true) (error :? InvalidOperationException, error.Message.Contains "class")

let private childText =
    Component.define (fun _ (value: string) -> value) (fun t ->
        t.fragment
            [|
                t.textarea [| t.Props.TextArea.value (fun (value: string) -> value) |] [||]
                t.textarea [| t.Props.TextArea.value "<static>" |] [||]
            |])

let private textareaChildText () =
    equal
        "<!--[r--><textarea>a&lt;b&amp;</textarea><textarea>&lt;static&gt;</textarea><!--]-->"
        (Server.render Server.Options.defaults childText "a<b&")

    let withChildren =
        Component.define (fun _ () -> ()) (fun t -> t.textarea [| t.Props.TextArea.value (fun _ -> "v") |] [| t.text "child" |])

    let error =
        failure (fun () ->
            Server.render Server.Options.defaults withChildren ()
            |> ignore)

    equal (true, true) (error :? InvalidOperationException, error.Message.Contains "<textarea>")

let private placedRejection () =
    let placed =
        Component.define (fun _ () -> ()) (fun t -> t.div [| t.Hooks.mounted (fun _ _ -> ignore) |] [||])

    let authored =
        Program.element (HtmlTag "span") [| browserModifier (fun _ -> ignore) |] [||]

    let messages =
        [
            (failure (fun () ->
                Server.render Server.Options.defaults placed ()
                |> ignore))
                .Message
            (failure (fun () ->
                Html.render Reject (Program.run authored)
                |> ignore))
                .Message
        ]

    equal
        [
            "Browser capability requires explicit Omit policy: <div> mounted hook"
            "Browser capability requires explicit Omit policy: <span> browser hook"
        ]
        messages

let private meter =
    Component.define (fun _ (value: float) -> value) (fun t -> t.meter [| t.Props.Meter.value (fun (value: float) -> value) |] [||])

let private numberForm () =
    let values =
        [
            2.0
            0.5
            2.5e-5
            1e-7
            1e21
            1.2345678901234568e20
            -0.0
            nan
            infinity
            -infinity
            123.456
            1e-6
        ]

    let expected =
        [
            "2"
            "0.5"
            "0.000025"
            "1e-7"
            "1e+21"
            "123456789012345680000"
            "0"
            "NaN"
            "Infinity"
            "-Infinity"
            "123.456"
            "0.000001"
        ]

    equal
        [
            for text in expected ->
                "<!--[r--><meter value=\""
                + text
                + "\"></meter><!--]-->"
        ]
        [ for value in values -> Server.render Server.Options.defaults meter value ]

let private holeTitle =
    Component.define (fun _ () -> ()) (fun t -> t.Document.title "Hole")

let private childTitle =
    Component.define (fun _ () -> ()) (fun t -> t.Document.title "Child")

let private holeThenChild =
    Component.define (fun _ () -> Component.render holeTitle ()) (fun t -> t.div [||] [| t.view id; t.render childTitle ignore |])

let private holeAfterChildren () =
    let hole = """<title data-ranvier-head="title">Hole</title>"""

    let expanded =
        Server.renderPage
            Server.Options.defaults
            holed
            {
                Caption = "C"
                Body = Component.renderWith Program.ops holeThenChild ()
            }

    equal (hole, hole) ((Server.renderPage Server.Options.defaults holeThenChild ()).Head, expanded.Head)

type FormState =
    {
        Text: string
        On: bool
        Choice: string
    }

let formFixture =
    Component.define (fun _ (state: FormState) -> state) (fun t ->
        t.form
            [||]
            [|
                t.textarea [| t.Props.TextArea.value (fun r -> r.Text) |] [||]
                t.input
                    [|
                        t.Attrs.Input.``type`` "checkbox"
                        t.Props.Input.``checked`` (fun r -> r.On)
                    |]
                t.select
                    [| t.Props.Select.value (fun r -> r.Choice) |]
                    [|
                        t.option
                            [|
                                t.Attrs.Option.value "a"
                                t.Props.Option.selected (fun r -> r.Choice = "a")
                            |]
                            [| t.text "A" |]
                        t.option
                            [|
                                t.Attrs.Option.value "b"
                                t.Props.Option.selected (fun r -> r.Choice = "b")
                            |]
                            [| t.text "B" |]
                    |]
                t.pre [||] [| t.text (fun r -> r.Text) |]
                t.pre [||] [| t.text "\nstatic" |]
            |])

let private forms () =
    equal
        "<!--[r--><form><textarea>\n\nline &lt;1&gt;</textarea><input type=\"checkbox\" checked=\"\"><select><option value=\"a\">A</option><option value=\"b\" selected=\"\">B</option></select><pre>\n\nline &lt;1&gt;</pre><pre>\n\nstatic</pre></form><!--]-->"
        (Server.render
            { Server.Options.defaults with
                Policy = Omit
            }
            formFixture
            {
                Text = "\nline <1>"
                On = true
                Choice = "b"
            })

let private staticTextarea =
    Component.define (fun _ () -> ()) (fun t -> t.textarea [| t.Props.TextArea.value "\nstatic" |] [||])

let private staticTextareaLineFeed () =
    equal "<!--[r--><textarea>\n\nstatic</textarea><!--]-->" (Server.render Server.Options.defaults staticTextarea ())

let cases: (string * (unit -> unit)) array =
    [|
        "CreateId numbers inlined children in pre-order and puts an inlined child's rows under its path", nestedIdentity
        "each view row counts its authored steps from 0 and the enclosing row resumes its count after it", holesIdentity
        "a Setup kept from one setup raises when called during another instance's setup", createIdInLaterSetup
        "CreateId follows the instance path for inlined children before and after regions, nested rows and arms", serverIdentity
        "CreateId after setup raises", createIdAfterSetup
        "authored components receive ids distinct from template ids", authoredIds
        "a view hole's first template takes the row's position and a later one the authored step under it", holeIdentity
        "renderPage writes keys in first-activation order and the last entry for a key", headOrder
        "renderPage activates an instance's own entries before its inlined children's and its regions' instances after both", activationOrder
        "renderPage activates every row of a pass before the rows' inner rows, compiled and expanded", containerOrder
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
        "a browser-only part under Reject names the element and the part", browserRejection
        "serialised properties write their attributes; browser-only properties raise under Reject and write nothing under Omit", serialisedProperties
        "listeners and browser hooks are never invoked on the server", browserPartsUninvoked
        "a property and a static attribute writing the same attribute fail at plan compile", duplicateAttributeSource
        "number properties serialise in ECMAScript number form on .NET and Fable", numberForm
        "a textarea's value property writes its escaped child text, and a textarea with children and that property fails at plan compile",
        textareaChildText
        "a placed hook and an authored browser hook under Reject name the element and the part", placedRejection
        "renderPage activates a view hole's entries after a later inlined child's, compiled and expanded", holeAfterChildren
        "a template in a view hole is owned by the backend's active scope", holeTemplateOwnedByRequest
        "a template in a view hole is owned by the request and cleaned up child-first after the render", holeTemplateOwnedByRequestScope
        "a request owns every instance and disposes them child-first after the render", requestRoot
        "setup failure disposes the request", setupFailureDisposes
        "OnCleanup after setup raises on the server", cleanupAfterSetupRejected
        "duplicate keys fail the server render with the core's exception", duplicateKeys
        "renderTo writes the rendered segments after success and nothing after failure", renderToSegments
        "a render whose cleanups fail raises an aggregate holding and naming every error", aggregateFailures
        "textarea, checkbox and select write their server state; select.value is browser-only", forms
        "a static textarea value starting with a line feed gets a second one", staticTextareaLineFeed
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
    heads.Open ()
    let outer = heads.EnterGroup ()
    heads.Add ("title", "<t2>")
    heads.Add (null, "<l2>")
    heads.LeaveGroup outer
    heads.Add ("meta:name=d", "<m2>")
    heads.Add ("link:canonical", "<c>")
    heads.Close ()
    equal "<t2><l1><m2><c><l2>" (heads.ToString ())
    heads.Truncate mark
    equal (3, "<t1><l1><m1>") (heads.Count, heads.ToString ())
    equal true ((failure (fun () -> heads.Truncate 4)) :? ArgumentException)

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
