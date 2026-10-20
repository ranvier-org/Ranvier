/// <summary>The motion seam without anime: motion contexts, exit holds and reflow order, through tests/departures.test.mjs.</summary>
module DepartureChecks

open System
open global.Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open CheckSupport
open Ranvier.View.Foundation.Sugar

type Rows =
    {
        Items: Signal<int array>
        Shown: Signal<bool>
    }

let private rows (items: int array) (shown: bool) =
    {
        Items = Api.createSignal items
        Shown = Api.createSignal shown
    }

/// <summary>The entries the hooks of the current case wrote, in the order written.</summary>
let private log = ResizeArray<string>()

let private taken () =
    let entries = List.ofSeq log
    log.Clear ()
    entries

let private context (name: string) (motion: MotionContext) =
    log.Add (sprintf "%s hydrated=%b inserted=%b" name motion.Hydrated motion.Inserted)

let private rowMotion (row: unit -> int) (_: HTMLLIElement) (motion: MotionContext) =
    context ("li " + string (row ())) motion
    ignore

let private shownMotion (_: Rows) (_: HTMLSpanElement) (motion: MotionContext) =
    context "span" motion
    ignore

/// <summary>A list whose rows report their motion contexts, and a shown span that reports its own.</summary>
let private contexts =
    Component.template
        (fun _ (state: Rows) -> state)
        (div () {
            ul () { forEach id (fun (s: Rows) -> s.Items.Value) (li (Hooks.motion rowMotion) { fun (row: unit -> int) -> string (row ()) }) }
            show (fun s -> s.Shown.Value) (span (Hooks.motion shownMotion) { "on" })
        })

let private innerMotion (row: unit -> int) (_: HTMLElement) (motion: MotionContext) =
    context ("inner " + string (row ())) motion
    ignore

/// <summary>Rows that each hold a list of two inner rows.</summary>
let private nested =
    Component.template
        (fun _ (state: Rows) -> state)
        (ul () {
            forEach
                id
                (fun (s: Rows) -> s.Items.Value)
                (li (Hooks.motion rowMotion) {
                    forEach
                        id
                        (fun (row: unit -> int) -> [| row () * 10; row () * 10 + 1 |])
                        (b (Hooks.motion innerMotion) { fun (row: unit -> int) -> string (row ()) })
                })
        })

let private write (graph: Graph) (mounted: Mounting.Mounted) (action: unit -> unit) =
    graph.Run action
    mounted.Flush ()

let private insertedAfterFirstPass queued () =
    use graph = new Graph ()
    log.Clear ()
    let state = graph.Run (fun () -> rows [| 1; 2 |] false)

    let mounted =
        mountTemplate queued graph strict (host ()) (Component.render contexts state)

    mounted.Flush ()
    let first = taken ()
    write graph mounted (fun () -> state.Items.Value <- [| 1; 2; 3 |])
    let added = taken ()
    write graph mounted (fun () -> state.Shown.Value <- true)
    let shown = taken ()
    mounted.Dispose ()

    equal
        ([ "li 1 hydrated=false inserted=false"; "li 2 hydrated=false inserted=false" ],
         [ "li 3 hydrated=false inserted=true" ],
         [ "span hydrated=false inserted=true" ])
        (first, added, shown)

let private shownOnFirstPass queued () =
    use graph = new Graph ()
    log.Clear ()
    let state = graph.Run (fun () -> rows [||] true)

    let mounted =
        mountTemplate queued graph strict (host ()) (Component.render contexts state)

    mounted.Flush ()
    let first = taken ()
    mounted.Dispose ()
    equal [ "span hydrated=false inserted=false" ] first

let private nestedRowsDoNotEnter queued () =
    use graph = new Graph ()
    log.Clear ()
    let state = graph.Run (fun () -> rows [| 1 |] false)

    let mounted =
        mountTemplate queued graph strict (host ()) (Component.render nested state)

    mounted.Flush ()
    log.Clear ()
    write graph mounted (fun () -> state.Items.Value <- [| 1; 2 |])
    let added = taken ()
    mounted.Dispose ()

    equal
        [
            "li 2 hydrated=false inserted=true"
            "inner 20 hydrated=false inserted=false"
            "inner 21 hydrated=false inserted=false"
        ]
        added

let private omit =
    { Server.Options.defaults with
        Policy = Omit
    }

let private hydratedThenInserted () =
    use graph = new Graph ()
    log.Clear ()
    let state = graph.Run (fun () -> rows [| 1; 2 |] true)
    let target = host ()
    loadServed target (Server.render omit contexts state)

    let mounted =
        Hydration.hydrate graph None strict { IdPrefix = "r"; Strict = true } target contexts state

    let first = taken ()
    write graph mounted (fun () -> state.Items.Value <- [| 1; 2; 3 |])
    let added = taken ()
    write graph mounted (fun () -> state.Shown.Value <- false)
    write graph mounted (fun () -> state.Shown.Value <- true)
    let shown = taken ()
    mounted.Dispose ()

    equal
        ([
            "li 1 hydrated=true inserted=false"
            "li 2 hydrated=true inserted=false"
            "span hydrated=true inserted=false"
         ],
         [ "li 3 hydrated=false inserted=true" ],
         [ "span hydrated=false inserted=true" ])
        (first, added, shown)

let cases: (string * (unit -> unit)) array =
    strictCases (
        Array.concat
            [
                templated "rows and a shown arm are inserted only after their region's first pass" insertedAfterFirstPass
                templated "an arm shown by the first pass is not inserted" shownOnFirstPass
                templated "rows created by the first pass of a region inside an inserted row are not inserted" nestedRowsDoNotEnter
                [|
                    "hydrated rows and arms are hydrated and not inserted; later ones are inserted", hydratedThenInserted
                |]
            ]
    )
