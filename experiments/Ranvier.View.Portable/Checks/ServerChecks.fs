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

let cases: (string * (unit -> unit)) array =
    [|
        "server plan matches the current writer for every template fixture", templatePins
        "server plan matches the current writer for arms", armPins
        "server plan matches the current writer for boundaries", boundaryPins
        "empty text, void children and a tr in a table render as recorded", edgePins
    |]

#if !FABLE_COMPILER
let dotnetCases: (string * (unit -> unit)) array = [||]
#endif
