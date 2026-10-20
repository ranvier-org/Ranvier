/// <summary>Owners, effects, edges, listener registrations and DOM writes of sugared and explicit twins; compiled with RANVIER_COUNTERS.</summary>
module SugarCounterChecks

open global.Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open CheckSupport
open SugarFixtures

[<Emit("(() => { const p = EventTarget.prototype, original = p.addEventListener; let count = 0; p.addEventListener = function (...args) { count++; return original.apply(this, args); }; return () => { p.addEventListener = original; return count; }; })()")>]
let private countListeners () : unit -> int = jsNative

[<Emit("(() => { const o = new MutationObserver(() => {}); o.observe($0, { subtree: true, childList: true, attributes: true, characterData: true }); return () => { const n = o.takeRecords().length; o.disconnect(); return n; }; })()")>]
let private recordMutations (root: Node) : unit -> int = jsNative

type private Resources =
    {
        Mount: (string * int64) array
        Update: (string * int64) array
        Dispose: (string * int64) array
        Listeners: int
        Writes: int
    }

let private measure (graph: Graph) (view: Program.View) (steps: (HTMLElement -> unit) list) : Resources =
#if RANVIER_COUNTERS
    let target = host ()
    let listeners = countListeners ()
    let writes = recordMutations target
    Counters.Reset ()
    let mounted = mountTemplate false graph strict target view
    let atMount = Counters.Snapshot ()

    for step in steps do
        step target

    let atUpdate = Counters.Snapshot ()
    mounted.Dispose ()

    {
        Mount = atMount
        Update = atUpdate
        Dispose = Counters.Snapshot ()
        Listeners = listeners ()
        Writes = writes ()
    }
#else
    failwith "SugarCounterChecks needs a build with RanvierCounters=true"
#endif

/// <summary>Measures each twin on its own graph and state; a warm-up mount of the explicit twin runs first.</summary>
let private sameResources
    (make: Graph -> 'state)
    (explicitView: 'state -> Program.View)
    (sugarView: 'state -> Program.View)
    (steps: Graph -> 'state -> (HTMLElement -> unit) list)
    ()
    =
    let run view =
        use graph = new Graph ()
        let state = make graph
        measure graph (view state) (steps graph state)

    run explicitView |> ignore
    equal (run explicitView) (run sugarView)

let private write (graph: Graph) (action: unit -> unit) =
    fun (_: HTMLElement) -> graph.Run action

let cases: (string * (unit -> unit)) array =
    strictCases
        [|
            "the sugared benchmark row mounts, updates and disposes the same resources",
            sameResources (fun _ -> ()) (fun () -> Component.render benchExplicit 3) (fun () -> Component.render benchSugar 3) (fun _ () ->
                [
                    (fun target ->
                        (target.querySelector "button"
                         |> unbox<HTMLElement>)
                            .click())
                ])
            "sugared text forms mount, update and dispose the same resources",
            sameResources
                (fun graph -> graph.Run (fun () -> Api.createSignal "a"))
                (Component.render textsExplicit)
                (Component.render textsSugar)
                (fun graph name -> [ write graph (fun () -> name.Value <- "b") ])
            "forEach mounts, updates and disposes the same resources",
            sameResources
                (fun graph -> graph.Run (fun () -> Api.createSignal (keyedItems ())))
                (Component.render keyedLiveExplicit)
                (Component.render keyedLiveSugar)
                (fun graph rows ->
                    [
                        write graph (fun () -> rows.Value <- Array.rev rows.Value)
                        write graph (fun () -> rows.Value <- [| { Id = 2; Label = Api.createSignal "x" } |])
                        write graph (fun () -> rows.Value <- [||])
                    ])
            "forEachFixed mounts, updates and disposes the same resources",
            sameResources
                (fun graph -> graph.Run (fun () -> Api.createSignal (keyedItems ())))
                (Component.render keyedFixedExplicit)
                (Component.render keyedFixedSugar)
                (fun graph rows ->
                    [
                        write graph (fun () -> rows.Value <- Array.rev rows.Value)
                        write graph (fun () -> rows.Value[0].Label.Value <- "x")
                        write graph (fun () -> rows.Value <- [||])
                    ])
            "armOf arms mount, switch and dispose the same resources",
            sameResources
                (fun graph -> graph.Run (fun () -> Api.createSignal (armItems ())))
                (Component.render armsExplicit)
                (Component.render armsSugar)
                (fun graph rows ->
                    [
                        write graph (fun () -> rows.Value[0].Mode.Value <- Counted 1)
                        write graph (fun () -> rows.Value[0].Mode.Value <- Counted 2)
                        write graph (fun () -> rows.Value[1].Mode.Value <- Plain)
                    ])
            "show mounts, toggles and disposes the same resources",
            sameResources
                (fun graph -> graph.Run (fun () -> toggle true))
                (Component.render showExplicit)
                (Component.render showSugar)
                (fun graph state ->
                    [
                        write graph (fun () -> state.On.Value <- false)
                        write graph (fun () -> state.On.Value <- true)
                    ])
            "showElse mounts, toggles and disposes the same resources",
            sameResources
                (fun graph -> graph.Run (fun () -> toggle true))
                (Component.render showElseExplicit)
                (Component.render showElseSugar)
                (fun graph state ->
                    [
                        write graph (fun () -> state.On.Value <- false)
                        write graph (fun () -> state.On.Value <- true)
                    ])
            "nested sugar mounts, updates and disposes the same resources",
            sameResources (fun graph -> graph.Run board) (Component.render nestedExplicit) (Component.render nestedSugar) (fun graph state ->
                [
                    write graph (fun () -> state.Entries.Value[0].Open.Value <- true)
                    write graph (fun () -> state.Listing.Value <- false)
                    write graph (fun () -> state.Listing.Value <- true)
                ])
            "a boundary around sugared content mounts and disposes the same resources",
            sameResources (fun _ -> ()) (fun () -> Component.render boundedExplicit ()) (fun () -> Component.render boundedSugar ()) (fun _ () -> [])
        |]
