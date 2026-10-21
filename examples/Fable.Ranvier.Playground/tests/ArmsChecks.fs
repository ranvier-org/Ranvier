/// <summary>Arms.Match sites mounted in jsdom: cells per arm, events, value payloads, rows, hydration and exits.</summary>
module ArmsChecks

open System
open global.Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open CheckSupport
open TwinTrace
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms
open ArmsFixtures

/// <summary>The text of the first element matching the selector, or null.</summary>
[<Emit("($0.querySelector($1)?.textContent ?? null)")>]
let private textOf (target: HTMLElement) (selector: string) : string = jsNative

/// <summary>The first element matching the selector, or null.</summary>
[<Emit("$0.querySelector($1)")>]
let private queryNode (target: HTMLElement) (selector: string) : obj = jsNative

let private write (graph: Graph) (mounted: Mounting.Mounted list) (change: unit -> unit) =
    graph.Run change

    for mount in mounted do
        mount.Flush ()

/// <summary>Two mounts of one site with one instance: disposing either leaves the other reading live values (Testing 4).</summary>
let private twiceShown queued () =
    use graph = new Graph ()
    let state = graph.Run (fun () -> Api.createSignal (Loaded ("a", 1)))
    let first, second = host (), host ()
    let a = mountTemplate queued graph strict first (Component.render loader state)
    let b = mountTemplate queued graph strict second (Component.render loader state)
    a.Flush ()
    b.Flush ()
    equal ("a x1", "a x1") (textOf first "pre", textOf second "pre")
    write graph [ a; b ] (fun () -> state.Value <- Loaded ("b", 2))
    equal ("b x2", "b x2") (textOf first "pre", textOf second "pre")
    b.Dispose ()
    write graph [ a ] (fun () -> state.Value <- Loaded ("c", 3))
    equal "c x3" (textOf first "pre")
    let third = host ()
    let c = mountTemplate queued graph strict third (Component.render loader state)
    c.Flush ()
    write graph [ a; c ] (fun () -> state.Value <- Loading 5)
    write graph [ a; c ] (fun () -> state.Value <- Loaded ("d", 4))
    equal ("d x4", "d x4") (textOf first "pre", textOf third "pre")
    a.Dispose ()
    write graph [ c ] (fun () -> state.Value <- Loaded ("e", 5))
    equal "e x5" (textOf third "pre")
    c.Dispose ()

/// <summary>An arm hidden and shown again for the same instance follows later writes (Review Focus 2).</summary>
let private remountAfterHide queued () =
    use graph = new Graph ()

    let shown =
        graph.Run (fun () ->
            {
                Visible = Api.createSignal true
                State = Api.createSignal (Loaded ("a", 1))
            })

    let target = host ()

    let mounted =
        mountTemplate queued graph strict target (Component.render shownLoader shown)

    mounted.Flush ()
    write graph [ mounted ] (fun () -> shown.State.Value <- Loaded ("b", 2))
    write graph [ mounted ] (fun () -> shown.Visible.Value <- false)
    equal null (textOf target "pre")
    write graph [ mounted ] (fun () -> shown.Visible.Value <- true)
    equal "b x2" (textOf target "pre")
    write graph [ mounted ] (fun () -> shown.State.Value <- Loaded ("c", 3))
    equal "c x3" (textOf target "pre")
    mounted.Dispose ()

let private initial (graph: Graph) =
    graph.Run (fun () -> Api.createSignal (Loading 10))

let private loadWrites (state: Signal<Load>) =
    [ for step in loadSteps -> Write (fun () -> state.Value <- step) ]

/// <summary>The probe's nine-step trace of each Load fixture equals its twin's (Testing 3).</summary>
let private traces queued () =
    for twin, site in
        [
            loaderTwin, loader
            richTwin, rich
            rowsTwin, rows
            rowsTupleTwin, rowsTuple
            tupledTwin, tupled
            wholeTwin, whole
            formsTwin, forms
            helperTwin, helper
        ] do
        pair queued initial (Component.render twin) (Component.render site) loadWrites

/// <summary>Every value-type payload case on the client equals its twin (Testing 6).</summary>
let private valueTraces queued () =
    let make (graph: Graph) =
        graph.Run (fun () -> Api.createSignal (Count 0))

    let steps (state: Signal<Value>) =
        [
            for value in
                [
                    Ratio 1.5
                    Ratio 2.5
                    Money 2.25m
                    Flag true
                    Flag false
                    Letter 'q'
                    At { X = 3; Y = 4 }
                    At { X = 5; Y = 6 }
                    Maybe (ValueSome 7)
                    Maybe ValueNone
                    Count 9
                ] -> Write (fun () -> state.Value <- value)
        ]

    pair queued make (Component.render valuesTwin) (Component.render values) steps

let private editorState (graph: Graph) =
    graph.Run (fun () ->
        ({
            State = Api.createSignal (Loaded ("a", 1))
            Saved = Api.createSignal ""
        }
        : Editor))

/// <summary>An event handler reads its arm's payload at event time (Testing 5).</summary>
let private eventsReadLatest queued () =
    use graph = new Graph ()
    let state = editorState graph
    let target = host ()

    let mounted =
        mountTemplate queued graph strict target (Component.render editor state)

    mounted.Flush ()

    let press () =
        click "button" target
        mounted.Flush ()

    write graph [ mounted ] (fun () -> state.State.Value <- Loaded ("b", 1))
    write graph [ mounted ] (fun () -> state.State.Value <- Loaded ("c", 1))
    press ()
    equal "c" (graph.Run (fun () -> state.Saved.Value))
    write graph [ mounted ] (fun () -> state.State.Value <- Idle)
    write graph [ mounted ] (fun () -> state.State.Value <- Loaded ("d", 1))
    press ()
    equal "d" (graph.Run (fun () -> state.Saved.Value))
    mounted.Dispose ()

let private editorTrace queued () =
    pair queued editorState (Component.render editorTwin) (Component.render editor) (fun state ->
        [
            Write (fun () -> state.State.Value <- Loaded ("b", 1))
            Each (click "button")
            Write (fun () -> state.State.Value <- Idle)
            Write (fun () -> state.State.Value <- Loaded ("c", 2))
            Each (click "button")
        ])

/// <summary>Rows fed by the items reader keep their elements across payload updates and show the new payload (Testing 7).</summary>
let private rowsKeep queued () =
    use graph = new Graph ()
    let state = graph.Run (fun () -> Api.createSignal (Loaded ("a", 2)))
    let target = host ()

    let mounted =
        mountTemplate queued graph strict target (Component.render rowsTuple state)

    mounted.Flush ()
    let first = queryNode target "li"
    write graph [ mounted ] (fun () -> state.Value <- Loaded ("b", 2))
    equal (true, "b0b1") (obj.ReferenceEquals (first, queryNode target "li"), textOf target "ul")
    mounted.Dispose ()

type private Row = { Id: int; State: Signal<Load> }

let private rowList =
    Component.template
        (fun _ (rows: Signal<Row array>) -> rows)
        (ul () {
            forEachFixed
                (fun (row: Row) -> row.Id)
                (fun (rows: Signal<Row array>) -> rows.Value)
                (li () {
                    Arms.Match (
                        (fun (row: Row) -> row.State.Value),
                        function
                        | Loaded (text, _) -> b () { fun _ -> text }
                        | _ -> span () { "Other" }
                    )
                })
        })

[<Emit("Array.from($0.querySelectorAll('li'), n => n.textContent)")>]
let private rowTexts (target: HTMLElement) : string array = jsNative

/// <summary>One site per row: after a reorder each row shows its own payload, and a write reaches its row only (Review Focus 4).</summary>
let private siteInRows queued () =
    use graph = new Graph ()

    let rows =
        graph.Run (fun () ->
            Api.createSignal
                [|
                    for id, text in [ 1, "a"; 2, "b"; 3, "c" ] ->
                        {
                            Id = id
                            State = Api.createSignal (Loaded (text, id))
                        }
                |])

    let target = host ()

    let mounted =
        mountTemplate queued graph strict target (Component.render rowList rows)

    mounted.Flush ()
    equal [| "a"; "b"; "c" |] (rowTexts target)
    write graph [ mounted ] (fun () -> rows.Value <- Array.rev rows.Value)
    equal [| "c"; "b"; "a" |] (rowTexts target)
    write graph [ mounted ] (fun () -> rows.Value[2].State.Value <- Loaded ("z", 1))
    equal [| "c"; "b"; "z" |] (rowTexts target)
    write graph [ mounted ] (fun () -> rows.Value[0].State.Value <- Idle)
    equal [| "Other"; "b"; "z" |] (rowTexts target)
    mounted.Dispose ()

/// <summary>A batch that leaves a target and returns to it shows the last payload and reports nothing (Review Focus 5).</summary>
let private switchBackInOneBatch queued () =
    use graph = new Graph ()
    let state = graph.Run (fun () -> Api.createSignal (Loaded ("a", 1)))
    let target = host ()

    let mounted =
        mountTemplate queued graph strict target (Component.render loader state)

    mounted.Flush ()

    write graph [ mounted ] (fun () ->
        Api.batch (fun () ->
            state.Value <- Idle
            state.Value <- Loaded ("b", 2)))

    equal "b x2" (textOf target "pre")
    mounted.Dispose ()

let cases: (string * (unit -> unit)) array =
    strictCases (
        Array.concat
            [
                templated "one site shown twice for one instance reads live values through either disposal" twiceShown
                templated "an arm hidden and shown again follows later writes" remountAfterHide
                templated "each Load fixture's nine-step trace equals its twin's" traces
                templated "value-type payload fields equal their twin" valueTraces
                templated "an event handler reads its arm's payload at event time" eventsReadLatest
                templated "an event handler's trace equals its twin's" editorTrace
                templated "rows fed by the items reader keep their elements across payload updates" rowsKeep
                templated "one site per keyed row follows its own row through a reorder" siteInRows
                templated "a batch that leaves a target and returns to it shows the last payload" switchBackInOneBatch
            ]
    )
