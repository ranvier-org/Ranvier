/// <summary>
/// Minimal reproductions of library bugs met while building the pages, each stating the correct behaviour; the fixed ones
/// stay as regression checks.
/// </summary>
module LibraryRepros

open global.Ranvier
open Ranvier.Anime
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Scheduling
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Anime
open Harness

type Rows = { Items: Signal<int array> }

let private fadingIn =
    Component.template
        (fun _ (rows: Rows) -> rows)
        (ul (Motion.flip (LayoutParams (duration = 300, ease = Ease.linear))) {
            forEach
                id
                (fun (rows: Rows) -> rows.Items.Value)
                (li (
                    Motion.enter (fun _ element -> Anime.animate (element, Anim (opacity = Value.fromTo (0, 1), duration = 100, ease = Ease.linear)))
                ) {
                    fun (row: unit -> int) -> string (row ())
                })
        })

/// <summary>
/// Motion.flip with an opacity enter on the same rows: the enter completes at 100 ms, the layout at 300 ms, and the row
/// stays at opacity 1. A regression check of a fixed bug: the layout wrote back the opacity it measured at the enter's
/// first frame, 0.
/// </summary>
let private flipRestoresEnteringOpacity (clock: Clock) =
    use graph = new Graph ()
    let rows = graph.Run (fun () -> { Items = Api.createSignal [| 1 |] })
    let target = host ()

    let mounted =
        Mounting.mountWith SynchronousCommits graph None report target None (fun context ->
            Program.run (Component.render fadingIn rows) context.Backend)

    write graph mounted (fun () -> rows.Items.Value <- [| 1; 2 |])
    let entering = find target "li:nth-of-type(2)"
    run clock 16. 160.
    let entered = styleOf entering "opacity"
    run clock 16. 400.
    let settled = styleOf entering "opacity"
    mounted.Dispose ()
    equal ("1", "1") (entered, settled)

let private boundaryWith (source: AsyncSource<float>) =
    let pending =
        Component.template (fun _ () -> ()) (p (Attrs.className "pending") { "pending" })

    let failed =
        Component.template (fun _ (failure: BoundaryFailure) -> failure) (p (Attrs.className "failed") { fun failure -> failure.Error.Message })

    let content =
        Component.template
            (fun _ (status: BoundaryStatus) -> status)
            (div (Attrs.className "level") {
                fun _ -> string source.Value
                show (fun _ -> source.Value > 0.8) (strong () { "high" })
            })

    Component.template (fun _ () -> ()) (section () { boundary pending failed content })

/// <summary>
/// A <c>show</c> whose condition reads a pending value inside a boundary's content: the boundary shows its pending view and
/// reveals the content once the value settles. A regression check of a fixed bug: the arm region's selection memo raised
/// the pending read, and the boundary showed its failed view.
/// </summary>
let private armReadPendsBoundary (clock: Clock) =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createAsyncSource<float>())
    let target = host ()
    let view = boundaryWith source

    let mounted =
        Mounting.mountWith SynchronousCommits graph None report target None (fun context -> Program.run (Component.render view ()) context.Backend)

    let waiting = countOf target ".pending", countOf target ".failed"
    graph.Run (fun () -> source.Settle 0.9)
    advance clock 16.

    let revealed =
        countOf target ".pending", countOf target ".failed", textsOf target ".level"

    mounted.Dispose ()
    equal ((1, 0), (0, 0, [| "0.9high" |])) (waiting, revealed)

/// <summary>
/// An SVG element's camel-case attribute, <c>viewBox</c>, is written as given under queued commits. A regression check of
/// a fixed bug: the attribute-name check rejected every name with an upper-case letter.
/// </summary>
let private svgCamelCaseAttribute (_: Clock) =
    let chart =
        Component.template (fun _ () -> ()) (svg (Attrs.attribute ("viewBox", "0 0 10 10")) { () })

    use graph = new Graph ()
    let target = host ()

    let mounted =
        Mounting.mountWith (QueuedCommits (CollectAndContinue, ignore)) graph None report target None (fun context ->
            Program.run (Component.render chart ()) context.Backend)

    mounted.Flush ()
    let written = attributeOf (find target "svg") "viewBox"
    mounted.Dispose ()
    equal "0 0 10 10" written

[<Fable.Core.Emit("$0.namespaceURI")>]
let private namespaceOf (node: obj) : string =
    Fable.Core.Util.jsNative

/// <summary>
/// A <c>path</c> tag mounted under queued commits is in the SVG namespace, as under synchronous commits. A regression
/// check of a fixed bug: queued commits created every template element in the HTML namespace.
/// </summary>
let private svgUnderQueuedCommits (_: Clock) =
    let chart = Component.template (fun _ () -> ()) (svg () { path () { () } })

    use graph = new Graph ()
    let target = host ()

    let mounted =
        Mounting.mountWith (QueuedCommits (CollectAndContinue, ignore)) graph None report target None (fun context ->
            Program.run (Component.render chart ()) context.Backend)

    mounted.Flush ()
    let space = namespaceOf (find target "path")
    mounted.Dispose ()
    equal "http://www.w3.org/2000/svg" space

let cases: Case list =
    [
        case "flip keeps an entering row's opacity enter" flipRestoresEnteringOpacity
        case "a show condition reading a pending value inside a boundary waits with the boundary" armReadPendsBoundary
        case "an SVG camel-case attribute is written as given under queued commits" svgCamelCaseAttribute
        case "an SVG path tag mounted under queued commits is in the SVG namespace" svgUnderQueuedCommits
    ]
