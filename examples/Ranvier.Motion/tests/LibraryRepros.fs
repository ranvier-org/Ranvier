/// <summary>Minimal reproductions of library bugs met while building the pages; each states the correct behaviour.</summary>
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
/// Motion.flip with Motion.enter (or presence) on the same rows: an entering row's opacity enter completes at 100 ms, the
/// layout completes at 300 ms, and the row is expected at opacity 1. The layout writes back the inline opacity it
/// measured when the pass's layout was animated, the enter's first frame, 0.
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

let cases: Case list =
    [
        bug
            "Motion.flip's layout restores the inline opacity measured at its animate() when it completes"
            "flip restores an entering row's opacity to the enter's first frame"
            flipRestoresEnteringOpacity
    ]
