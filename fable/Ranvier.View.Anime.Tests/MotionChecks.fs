/// <summary>The Motion parts mounted under jsdom on a clock advanced by the test, through motion.test.mjs.</summary>
module MotionChecks

open Fable.Core
open Fable.Core.TS.Dom
open global.Ranvier
open Ranvier.Anime
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Scheduling
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Anime

/// <summary>Advances the mocked <c>Date</c> and timers by the given milliseconds.</summary>
type Clock = float -> unit

[<Global>]
let private document: Document = jsNative

[<Emit("$0.style.getPropertyValue($1)")>]
let private styleOf (element: obj) (property: string) : string = jsNative

[<Emit("$0.querySelector($1)")>]
let private find (root: obj) (selector: string) : HTMLElement = jsNative

[<Emit("Array.from($0.querySelectorAll($1), n => n.textContent)")>]
let private textsOf (root: obj) (selector: string) : string array = jsNative

[<Emit("$0.isConnected")>]
let private connected (node: obj) : bool = jsNative

[<Emit("globalThis.motionLog.splice(0)")>]
let private logged () : string array = jsNative

[<Emit("(() => { const r = $0.ownerDocument.createRange(); r.selectNodeContents($0); $0.replaceChildren(r.createContextualFragment($1)); })()")>]
let private loadServed (target: HTMLElement) (html: string) : unit = jsNative

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

let private advance (clock: Clock) (ms: float) =
    clock ms
    Testing.step ()

let private report (errors: exn list) =
    failwithf "Unexpected report %A" [ for error in errors -> error.Message ]

let private host () =
    let target = document.createElement "div"
    document.body.appendChild target |> ignore
    target

let private mountIn (queued: bool) (graph: Graph) (target: HTMLElement) (view: Program.View) =
    let mode =
        if queued then
            QueuedCommits (CollectAndContinue, ignore)
        else
            SynchronousCommits

    let mounted =
        Mounting.mountWith mode graph None report target None (fun context -> Program.run view context.Backend)

    mounted.Flush ()
    mounted

let private write (graph: Graph) (mounted: Mounting.Mounted) (action: unit -> unit) =
    graph.Run action
    mounted.Flush ()

/// <summary>Hands anime's engine to the test clock; called once, after <c>animejs</c> is imported.</summary>
let prepare () =
    Testing.manual ()

type Rows = { Items: Signal<int array> }

let private rows (items: int array) =
    { Items = Api.createSignal items }

let private fadeIn (_: unit -> int) (node: Element) =
    Anime.animate (node, Anim (opacity = Value.fromTo (0, 1), duration = 1000, ease = Ease.linear))

let private fadeOut (_: unit -> int) (node: Element) =
    Anime.animate (node, Anim (opacity = 0, duration = 1000, ease = Ease.linear))

let private entering =
    Component.template
        (fun _ (state: Rows) -> state)
        (ul () { forEach id (fun (s: Rows) -> s.Items.Value) (li (Motion.enter fadeIn) { fun (row: unit -> int) -> string (row ()) }) })

let private exiting =
    Component.template
        (fun _ (state: Rows) -> state)
        (ul () { forEach id (fun (s: Rows) -> s.Items.Value) (li (Motion.exit fadeOut) { fun (row: unit -> int) -> string (row ()) }) })

let private both =
    Component.template
        (fun _ (state: Rows) -> state)
        (ul () {
            forEach id (fun (s: Rows) -> s.Items.Value) (li (Motion.enter fadeIn, Motion.exit fadeOut) { fun (row: unit -> int) -> string (row ()) })
        })

let private present =
    Component.template
        (fun _ (state: Rows) -> state)
        (ul () { forEach id (fun (s: Rows) -> s.Items.Value) (li (Motion.presence fadeIn fadeOut) { fun (row: unit -> int) -> string (row ()) }) })

let private rowOf (target: HTMLElement) (index: int) : HTMLElement =
    find target (sprintf "li:nth-of-type(%d)" index)

let private enterValues queued (clock: Clock) =
    use graph = new Graph ()
    let state = graph.Run (fun () -> rows [| 1 |])
    let target = host ()
    let mounted = mountIn queued graph target (Component.render entering state)
    let first = styleOf (rowOf target 1) "opacity"
    write graph mounted (fun () -> state.Items.Value <- [| 1; 2 |])
    let row = rowOf target 2
    advance clock 0.
    let start = styleOf row "opacity"
    advance clock 488.
    let half = styleOf row "opacity"
    advance clock 600.
    let finish = styleOf row "opacity"
    mounted.Dispose ()
    // anime starts a resumed tickable 12 ms early, so each read shows the value 12 ms on.
    equal ("", "0", "0.5", "1") (first, start, half, finish)

let private exitRemovesAtCompletion queued (clock: Clock) =
    use graph = new Graph ()
    let state = graph.Run (fun () -> rows [| 1; 2 |])
    let target = host ()
    let mounted = mountIn queued graph target (Component.render exiting state)
    let row = rowOf target 2
    write graph mounted (fun () -> state.Items.Value <- [| 1 |])
    let held = List.ofArray (textsOf target "li")
    advance clock 0.
    advance clock 987.
    let before = connected row, styleOf row "opacity"
    // The engine skips a tick shorter than its frame, so the clock moves 1 ms at a time until the node leaves; while
    // the node is connected the animation has not rendered its end value.
    let early = ResizeArray ()
    let mutable steps = 0

    while connected row && steps < 50 do
        advance clock 1.
        steps <- steps + 1

        if connected row && styleOf row "opacity" = "0" then
            early.Add steps

    let after = connected row, List.ofArray (textsOf target "li"), List.ofSeq early
    mounted.Dispose ()
    equal ([ "1"; "2" ], (true, "0.001"), (false, [ "1" ], [])) (held, before, after)

let private onRevertsOnDisposal (clock: Clock) =
    use graph = new Graph ()
    let target = host ()

    let view =
        Component.template
            (fun _ () -> ())
            (div (Motion.on (fun _ node -> Anime.animate (node, Anim (opacity = 0.5, duration = 1000, ease = Ease.linear)))) { "on" })

    let mounted = mountIn false graph target (Component.render view ())
    let element = find target "div"
    advance clock 0.
    advance clock 488.
    let running = styleOf element "opacity"
    mounted.Dispose ()
    equal ("0.75", "") (running, styleOf element "opacity")

let private pausedWhenExitFollows (view: Component<Rows>) queued (clock: Clock) =
    use graph = new Graph ()
    let state = graph.Run (fun () -> rows [| 1 |])
    let target = host ()
    let mounted = mountIn queued graph target (Component.render view state)
    write graph mounted (fun () -> state.Items.Value <- [| 1; 2 |])
    let row = rowOf target 2
    advance clock 0.
    advance clock 488.
    let entered = styleOf row "opacity"
    write graph mounted (fun () -> state.Items.Value <- [| 1 |])
    let paused = styleOf row "opacity"
    advance clock 0.
    advance clock 488.
    let leaving = styleOf row "opacity"
    advance clock 600.
    let left = connected row
    mounted.Dispose ()
    equal ("0.5", "0.5", "0.25", false) (entered, paused, leaving, left)

type Toggle = { Shown: Signal<bool> }

let private toggled =
    Component.template
        (fun _ (state: Toggle) -> state)
        (div () {
            show
                (fun (s: Toggle) -> s.Shown.Value)
                (p (
                    Motion.presence
                        (fun _ node -> Anime.animate (node, Anim (opacity = Value.fromTo (0, 1), duration = 1000, ease = Ease.linear)))
                        (fun _ node -> Anime.animate (node, Anim (opacity = 0, duration = 1000, ease = Ease.linear)))
                ) {
                    "panel"
                })
        })

let private armPresence queued (clock: Clock) =
    use graph = new Graph ()

    let state =
        {
            Shown = graph.Run (fun () -> Api.createSignal false)
        }

    let target = host ()
    let mounted = mountIn queued graph target (Component.render toggled state)
    write graph mounted (fun () -> state.Shown.Value <- true)
    let panel = find target "p"
    advance clock 0.
    advance clock 488.
    let entered = styleOf panel "opacity"
    write graph mounted (fun () -> state.Shown.Value <- false)
    let held = connected panel, styleOf panel "opacity"
    advance clock 0.
    advance clock 488.
    let leaving = styleOf panel "opacity"
    advance clock 600.
    let left = connected panel
    mounted.Dispose ()
    equal ("0.5", (true, "0.5"), "0.25", false) (entered, held, leaving, left)

let private presencePins (clock: Clock) =
    use graph = new Graph ()
    let state = graph.Run (fun () -> rows [| 1; 2 |])
    let target = host ()
    let mounted = mountIn false graph target (Component.render present state)
    let row = rowOf target 2
    let before = styleOf row "position"
    write graph mounted (fun () -> state.Items.Value <- [| 1 |])
    let pinned = styleOf row "position"
    mounted.Dispose ()
    equal ("", "absolute", false) (before, pinned, connected row)

let private disposalEndsExits queued (clock: Clock) =
    use graph = new Graph ()
    let state = graph.Run (fun () -> rows [| 1; 2 |])
    let target = host ()
    let mounted = mountIn queued graph target (Component.render exiting state)
    let row = rowOf target 2
    write graph mounted (fun () -> state.Items.Value <- [| 1 |])
    advance clock 0.
    advance clock 488.
    mounted.Dispose ()
    let gone = connected row
    let value = styleOf row "opacity"
    advance clock 600.
    equal (false, "0.5", "0.5") (gone, value, styleOf row "opacity")

let private reducedPolicy queued (clock: Clock) =
    use graph = new Graph ()
    let state = graph.Run (fun () -> rows [| 1 |])
    graph.Run (fun () -> (Reactive.policy ()).Value <- Reactive.Reduced)
    let target = host ()
    let mounted = mountIn queued graph target (Component.render both state)
    write graph mounted (fun () -> state.Items.Value <- [| 1; 2 |])
    let row = rowOf target 2
    advance clock 0.
    advance clock 100.
    let entered = styleOf row "opacity"
    write graph mounted (fun () -> state.Items.Value <- [| 1 |])
    let left = connected row
    mounted.Dispose ()
    equal ("", false) (entered, left)

let private flipped =
    Component.template
        (fun _ (state: Rows) -> state)
        (ul (Motion.flip (LayoutParams (duration = 300))) {
            forEach id (fun (s: Rows) -> s.Items.Value) (li (Motion.exit fadeOut) { fun (row: unit -> int) -> string (row ()) })
        })

let private flipOrder queued (clock: Clock) =
    use graph = new Graph ()
    let state = graph.Run (fun () -> rows [| 1; 2; 3 |])
    let target = host ()
    let mounted = mountIn queued graph target (Component.render flipped state)
    let mounting = logged ()
    write graph mounted (fun () -> state.Items.Value <- [| 3; 1; 2 |])
    let created = logged ()
    write graph mounted (fun () -> state.Items.Value <- [| 3; 1 |])
    let removed = logged ()
    let held = List.ofArray (textsOf target "li")
    graph.Run (fun () -> (Reactive.policy ()).Value <- Reactive.Reduced)
    write graph mounted (fun () -> state.Items.Value <- [| 1; 3 |])
    let reduced = logged ()
    mounted.Dispose ()

    equal ([||], [| "animate 3 1 2" |], [| "record 3 1 2"; "animate 3 1" |], [ "3"; "1"; "2" ], [||]) (mounting, created, removed, held, reduced)

let private appeared =
    Component.template
        (fun _ () -> ())
        (p (Motion.appear (fun _ node -> Anime.animate (node, Anim (opacity = Value.fromTo (0, 1), duration = 1000)))) { "served" })

let private appearSkipsHydrated (clock: Clock) =
    use graph = new Graph ()
    let served = host ()
    loadServed served (Server.render Server.Options.defaults appeared ())

    let hydrated =
        Hydration.hydrate graph None report { IdPrefix = "r"; Strict = true } served appeared ()

    advance clock 0.
    let adopted = styleOf (find served "p") "opacity"
    hydrated.Dispose ()
    let fresh = host ()
    let mounted = mountIn false graph fresh (Component.render appeared ())
    advance clock 0.
    let created = styleOf (find fresh "p") "opacity"
    mounted.Dispose ()
    equal ("", "0") (adopted, created)

let private modes (name: string) (check: bool -> Clock -> unit) =
    [|
        for queued in [ false; true ] do
            yield sprintf "%s (%s commits)" name (if queued then "queued" else "synchronous"), check queued
    |]

/// <summary>Each check by name; a check receives the test clock.</summary>
let cases: (string * (Clock -> unit)) array =
    Array.concat
        [
            modes "enter reads its values at 0, half and end, and first-pass rows stay still" enterValues
            modes "exit removes the row's node at the tick its animation completes" exitRemovesAtCompletion
            [|
                "on reverts its animation when the instance is disposed", onRevertsOnDisposal
            |]
            modes "enter and exit on one element: the exit starts from the paused enter's values" (pausedWhenExitFollows both)
            modes "presence: the exit starts from the paused enter's values" (pausedWhenExitFollows present)
            modes "an arm with presence enters when shown and leaves from its paused values when hidden" armPresence
            [| "presence pins the leaving node out of flow", presencePins |]
            modes "disposing the mount mid-exit removes the node and stops the exit" disposalEndsExits
            modes "under Reduced, enter does not play and exit finishes at once" reducedPolicy
            modes "flip records before and animates after each pass, without leaving rows" flipOrder
            [|
                "appear skips hydrated elements and plays on created ones", appearSkipsHydrated
            |]
        ]
