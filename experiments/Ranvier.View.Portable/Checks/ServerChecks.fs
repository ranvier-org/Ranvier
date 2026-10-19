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
type private Owner() =
    member val Children = ResizeArray<Owner>()
    member val Cleanups = ResizeArray<unit -> unit>()

    member this.Dispose() =
        for child in this.Children do
            child.Dispose ()

        for cleanup in this.Cleanups do
            cleanup ()

/// <summary>A server scope that gives each nested instance a child owner of <c>owner</c>.</summary>
type private OwnedScope(owner: Owner) =
    inherit ServerScope()

    override _.Nest body =
        let child = Owner ()
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
    let root = Owner ()
    let sink = HtmlSink ()
    let backend = HtmlBackend (Omit, sink)
    backend.Scope <- OwnedScope root
    Program.run (Component.render holeHolder (Component.render holeChild ())) backend
    let during = List.ofSeq cleanupLog
    root.Dispose ()

    equal
        ("<div><!--[--><span>in</span><!--]--></div>", List.empty<string>, [ "hole child"; "holder" ])
        (sink.ToString (), during, List.ofSeq cleanupLog)

let cases: (string * (unit -> unit)) array =
    [|
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

let dotnetCases: (string * (unit -> unit)) array =
    [|
        "escape returns the input string when nothing needs escaping", escapeKeepsReference
        "concurrent first renders publish one plan and one arm reader", concurrentPublication
    |]
#endif
