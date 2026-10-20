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

[<Emit("$0.getAttribute($1)")>]
let private attributeOf (element: obj) (name: string) : string = jsNative

[<Emit("$0.querySelector($1)")>]
let private find (root: obj) (selector: string) : HTMLElement = jsNative

[<Emit("Array.from($0.querySelectorAll($1), n => n.textContent)")>]
let private textsOf (root: obj) (selector: string) : string array = jsNative

[<Emit("$0.querySelectorAll($1).length")>]
let private countOf (root: obj) (selector: string) : int = jsNative

[<Emit("$0.namespaceURI")>]
let private namespaceOf (node: obj) : string = jsNative

[<Emit("$0.isConnected")>]
let private connected (node: obj) : bool = jsNative

[<Emit("globalThis.motionLog.splice(0)")>]
let private logged () : string array = jsNative

[<Emit("globalThis.motionDraggable")>]
let private lastDraggable () : Draggable = jsNative

[<Emit("globalThis.motionSets")>]
let private draggableSets () : int = jsNative

[<Emit("globalThis.motionObserver")>]
let private lastObserver () : ScrollObserver = jsNative

[<Emit("Object.defineProperty($0, 'progress', { value: $1, configurable: true })")>]
let private fixProgress (observer: ScrollObserver) (progress: float) : unit = jsNative

[<Emit("$0.onUpdate($0)")>]
let private fireUpdate (observer: ScrollObserver) : unit = jsNative

[<Emit("$0.onEnter($0)")>]
let private fireEnter (observer: ScrollObserver) : unit = jsNative

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

type Level = { Level: Signal<float> }

let private followReaches (clock: Clock) =
    use graph = new Graph ()
    let target = host ()

    let state =
        {
            Level = graph.Run (fun () -> Api.createSignal 0.2)
        }

    let view =
        Component.template
            (fun _ (s: Level) -> s)
            (div (Motion.follow "opacity" (AnimatableParams (duration = 1000, ease = Ease.linear)) (fun (s: Level) -> s.Level.Value)) { "level" })

    let mounted = mountIn false graph target (Component.render view state)
    let element = find target "div"
    let first = styleOf element "opacity"
    write graph mounted (fun () -> state.Level.Value <- 1.)
    advance clock 0.
    advance clock 488.
    let half = styleOf element "opacity"
    advance clock 600.
    let reached = styleOf element "opacity"
    mounted.Dispose ()
    equal ("0.2", "0.6", "1", "") (first, half, reached, styleOf element "opacity")

let private replayed =
    Component.template
        (fun _ (s: Level) -> s)
        (div (
            Motion.animateOn (fun (s: Level) -> s.Level.Value) (fun _ node level ->
                Anime.animate (node, Anim (opacity = level, duration = 1000, ease = Ease.linear)))
        ) {
            "level"
        })

let private animateOnReplaces (clock: Clock) =
    use graph = new Graph ()
    let target = host ()

    let state =
        {
            Level = graph.Run (fun () -> Api.createSignal 0.2)
        }

    let mounted = mountIn false graph target (Component.render replayed state)
    let element = find target "div"
    let first = styleOf element "opacity"
    write graph mounted (fun () -> state.Level.Value <- 1.)
    advance clock 0.
    advance clock 488.
    let half = styleOf element "opacity"
    write graph mounted (fun () -> state.Level.Value <- 0.4)
    advance clock 0.
    advance clock 488.
    let replaced = styleOf element "opacity"
    advance clock 600.
    let reached = styleOf element "opacity"
    advance clock 1000.
    let settled = styleOf element "opacity"
    mounted.Dispose ()
    equal ("0.2", "0.6", "0.5", "0.4", "0.4") (first, half, replaced, reached, settled)

let private stroked =
    Component.define (fun _ (s: Level) -> s) (fun t ->
        t.element
            (HtmlTag "svg")
            [||]
            [|
                t.element
                    (HtmlTag "path")
                    [|
                        Motion.draw (AnimatableParams (duration = 1000, ease = Ease.linear)) (fun (s: Level) -> s.Level.Value)
                    |]
                    [||]
            |])

let private drawReaches (clock: Clock) =
    use graph = new Graph ()
    let target = host ()

    let state =
        {
            Level = graph.Run (fun () -> Api.createSignal 0.25)
        }

    let mounted = mountIn false graph target (Component.render stroked state)
    let path = find target "path"
    let first = attributeOf path "stroke-dasharray", namespaceOf path
    write graph mounted (fun () -> state.Level.Value <- 0.75)
    advance clock 0.
    advance clock 488.
    let half = attributeOf path "stroke-dasharray"
    advance clock 600.
    let reached = attributeOf path "stroke-dasharray"
    mounted.Dispose ()
    equal (("250 760", "http://www.w3.org/2000/svg"), "500 510", "750 260") (first, half, reached)

type Outline = { Shape: Signal<string> }

let private morphed =
    Component.define (fun _ (s: Outline) -> s) (fun t ->
        t.element
            (HtmlTag "svg")
            [||]
            [|
                t.element
                    (HtmlTag "path")
                    [|
                        Motion.morph (Anim (duration = 1000, ease = Ease.linear)) (fun (s: Outline) -> s.Shape.Value)
                    |]
                    [||]
            |])

let private morphReaches (clock: Clock) =
    use graph = new Graph ()
    let target = host ()

    let state =
        {
            Shape = graph.Run (fun () -> Api.createSignal "M0 0 L10 0")
        }

    let mounted = mountIn false graph target (Component.render morphed state)
    let path = find target "path"
    let first = attributeOf path "d"
    write graph mounted (fun () -> state.Shape.Value <- "M0 10 L10 10")
    advance clock 0.
    advance clock 488.
    let half = attributeOf path "d"
    advance clock 600.
    let reached = attributeOf path "d"
    mounted.Dispose ()
    equal ("M0 0 L10 0", true, "M0 10 L10 10") (first, half <> first && half <> reached, reached)

type Puck =
    {
        Position: Signal<struct (float * float)>
        Dragging: Signal<bool>
    }

let private dragged =
    Component.template
        (fun _ (p: Puck) -> p)
        (div (
            Motion.drag (DraggableParams (onUpdate = fun _ -> ())) (fun (p: Puck) ->
                {
                    Position = p.Position
                    Dragging = ValueSome p.Dragging
                })
        ) {
            "puck"
        })

let private dragEchoGuard (clock: Clock) =
    use graph = new Graph ()
    let target = host ()

    let puck =
        graph.Run (fun () ->
            {
                Position = Api.createSignal (struct (0., 0.))
                Dragging = Api.createSignal false
            })

    let mounted = mountIn false graph target (Component.render dragged puck)
    let draggable = lastDraggable ()
    let initial = draggableSets ()
    write graph mounted (fun () -> puck.Position.Value <- struct (100., 50.))
    advance clock 0.
    advance clock 16.

    let moved =
        draggableSets () - initial, struct (draggable.x, draggable.y), graph.Run (fun () -> puck.Position.Value)

    draggable.x <- 30.
    advance clock 16.

    let afterDrag =
        draggableSets () - initial, graph.Run (fun () -> puck.Position.Value)

    mounted.Dispose ()
    equal ((2, struct (100., 50.), struct (100., 50.)), (3, struct (30., 50.))) (moved, afterDrag)

type Scrolled =
    {
        Progress: Signal<float>
        InView: Signal<bool>
    }

let private scrolled =
    Component.template
        (fun _ (s: Scrolled) -> s)
        (section (
            Motion.scroll
                (ScrollParams ())
                (fun (s: Scrolled) ->
                    {
                        Progress = ValueSome s.Progress
                        InView = ValueSome s.InView
                    })
                (fun _ node -> Anime.animate (node, Anim (opacity = Value.fromTo (0, 1), duration = 1000, ease = Ease.linear)))
        ) {
            "story"
        })

let private scrollWrites (clock: Clock) =
    use graph = new Graph ()
    let target = host ()

    let state =
        graph.Run (fun () ->
            {
                Progress = Api.createSignal 0.
                InView = Api.createSignal false
            })

    let mounted = mountIn false graph target (Component.render scrolled state)
    let observer = lastObserver ()
    fixProgress observer 0.25
    fireUpdate observer
    fireEnter observer
    let written = graph.Run (fun () -> state.Progress.Value, state.InView.Value)
    mounted.Dispose ()
    equal (0.25, true) written

let private splitted =
    Component.template
        (fun _ () -> ())
        (h1 (
            Motion.split (SplitParams (chars = true)) (fun _ splitter ->
                Anime.animate (splitter.chars, Anim (opacity = Value.fromTo (0, 1), duration = 100)))
        ) {
            "Hi"
        })

let private splitReverts (clock: Clock) =
    use graph = new Graph ()
    let target = host ()
    let mounted = mountIn false graph target (Component.render splitted ())
    let heading = find target "h1"
    let parts = countOf heading "*"
    mounted.Dispose ()
    equal (true, "Hi") (parts >= 2, heading.innerHTML)

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
                "follow sets its first value and animates to each write", followReaches
                "animateOn replaces a running animation and settles on the last value", animateOnReplaces
                "draw animates the stroke to each fraction", drawReaches
                "morph animates to each shape and ends on it exactly", morphReaches
                "drag writes its position and moves on writes without echo", dragEchoGuard
                "scroll writes the observer's progress and in-view state", scrollWrites
                "split splits the text and its cleanup restores it", splitReverts
                "appear skips hydrated elements and plays on created ones", appearSkipsHydrated
            |]
        ]
