/// <summary>Sugared and explicit twins mounted side by side, driven by the same writes, and hydrated from each other's server HTML.</summary>
module SugarChecks

open System
open global.Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open CheckSupport
open SugarFixtures

type private Step =
    | Write of (unit -> unit)
    | Each of (HTMLElement -> unit)

/// <summary>Native writes recorded under a root since the previous drain.</summary>
type private WriteWatch =
    abstract drain: unit -> string array
    abstract stop: unit -> unit

[<Emit("(() => { const o = new MutationObserver(() => {}); o.observe($0, { attributes: true, childList: true, characterData: true, subtree: true }); return { drain: () => o.takeRecords().map(r => r.type + ':' + (r.attributeName ?? '') + ':' + r.addedNodes.length + ':' + r.removedNodes.length), stop: () => o.disconnect() }; })()")>]
let private watchWrites (root: HTMLElement) : WriteWatch = jsNative

/// <summary>Listener registrations made through <c>addEventListener</c> since the wrap was installed.</summary>
type private ListenerWatch =
    abstract under: HTMLElement -> int
    abstract stop: unit -> unit

[<Emit("(() => { const log = []; const proto = window.EventTarget.prototype; const original = proto.addEventListener; proto.addEventListener = function (type, ...rest) { log.push([this, type]); return original.call(this, type, ...rest); }; return { under: (root) => log.filter(([target]) => target instanceof Node && root.contains(target)).length, stop: () => { proto.addEventListener = original; } }; })()")>]
let private watchListeners () : ListenerWatch = jsNative

[<Emit("globalThis.ranvierExpectCounters === true")>]
let private countersExpected () : bool = jsNative

/// <summary>The graph counters as name-value pairs; fails when counters are expected but not compiled.</summary>
let private tally () : (string * float) list =
#if RANVIER_COUNTERS
    [ for name, value in Counters.Snapshot () -> name, float value ]
#else
    if countersExpected () then
        failwith "The run expects counters; compile with RanvierCounters=true"

    []
#endif

let private delta (before: (string * float) list) (after: (string * float) list) =
    List.map2 (fun (name, was) (_, now) -> name, now - was) before after

let private click (selector: string) (root: HTMLElement) =
    (root.querySelector selector |> unbox<HTMLElement>).click()

/// <summary>
/// One twin's run: markup, reports, counter deltas, listener registrations under the host and native writes after the
/// mount and each step, plus the counter delta and writes of the disposal.
/// </summary>
type private Trace =
    {
        Shots: string list
        Reports: string list list
        Counts: (string * float) list list
        Listeners: int list
        Writes: string array list
    }

/// <summary>
/// Mounts <c>view</c> on a graph and state of its own, runs <c>steps</c> and disposes. Counters are read around the
/// mount, each step and the disposal, so every difference between twins belongs to one twin.
/// </summary>
let private run queued (make: Graph -> 'state) (view: 'state -> Program.View) (steps: 'state -> Step list) =
    use graph = new Graph ()
    let state = make graph
    let target = host ()
    let reports = ResizeArray<exn list>()
    let writes = watchWrites target
    let listeners = watchListeners ()

    try
        let shots, counts, registrations, native =
            ResizeArray (), ResizeArray (), ResizeArray (), ResizeArray ()

        let measured (act: unit -> unit) =
            let before = tally ()
            act ()
            counts.Add (delta before (tally ()))

        let mounted = ref Unchecked.defaultof<_>

        measured (fun () ->
            mounted.Value <- mountTemplate queued graph reports.Add target (view state)
            mounted.Value.Flush ())

        let record () =
            shots.Add target.innerHTML
            registrations.Add (listeners.under target)
            native.Add (writes.drain ())

        record ()

        for step in steps state do
            measured (fun () ->
                match step with
                | Write write -> graph.Run write
                | Each act -> act target

                mounted.Value.Flush ())

            record ()

        measured mounted.Value.Dispose
        shots.Add target.innerHTML
        native.Add (writes.drain ())

        {
            Shots = List.ofSeq shots
            Reports = messages reports
            Counts = List.ofSeq counts
            Listeners = List.ofSeq registrations
            Writes = List.ofSeq native
        },
        state
    finally
        writes.stop ()
        listeners.stop ()

/// <summary>Runs the explicit twin, then the sugared twin, each on its own graph and state.</summary>
let private twins queued make explicitView sugarView steps =
    let explicitTrace, explicitState = run queued make explicitView steps
    let sugarTrace, sugarState = run queued make sugarView steps
    (explicitTrace, sugarTrace), (explicitState, sugarState)

let private same (explicitTrace: Trace, sugarTrace: Trace) =
    equal explicitTrace.Shots sugarTrace.Shots
    equal explicitTrace.Reports sugarTrace.Reports

    for index, (left, right) in List.indexed (List.zip explicitTrace.Counts sugarTrace.Counts) do
        equal (index, left) (index, right)

    equal explicitTrace.Listeners sugarTrace.Listeners

    for index, (left, right) in List.indexed (List.zip explicitTrace.Writes sugarTrace.Writes) do
        equal (index, left) (index, right)

let private pair queued make explicitView sugarView steps =
    same (fst (twins queued make explicitView sugarView steps))

let private benchClicks queued () =
    pair queued (fun _ -> ()) (fun () -> Component.render benchExplicit 3) (fun () -> Component.render benchSugar 3) (fun () ->
        [ Each (click "button"); Each (click "button") ])

let private textUpdates queued () =
    let make (graph: Graph) =
        graph.Run (fun () -> Api.createSignal "a")

    pair queued make (Component.render textsExplicit) (Component.render textsSugar) (fun name -> [ Write (fun () -> name.Value <- "b") ])

let private keyedSteps (rows: Signal<KeyedItem array>) =
    let removed = ref Unchecked.defaultof<KeyedItem>

    [
        Write (fun () -> rows.Value <- Array.rev rows.Value)
        Write (fun () ->
            removed.Value <- rows.Value[0]
            rows.Value <- rows.Value[1..])
        Write (fun () -> rows.Value <- Array.append rows.Value [| removed.Value |])
        Write (fun () -> rows.Value <- [| rows.Value[0]; rows.Value[0] |])
        Write (fun () ->
            rows.Value <-
                [|
                    {
                        Id = 2
                        Label = Api.createSignal "replaced"
                    }
                    removed.Value
                |])
        Write (fun () -> rows.Value[0].Label.Value <- "relabelled")
        Write (fun () -> rows.Value <- [||])
    ]

let private keyedRows (graph: Graph) =
    graph.Run (fun () -> Api.createSignal (keyedItems ()))

let private keyedLive queued () =
    pair queued keyedRows (Component.render keyedLiveExplicit) (Component.render keyedLiveSugar) keyedSteps

let private keyedFixed queued () =
    pair queued keyedRows (Component.render keyedFixedExplicit) (Component.render keyedFixedSugar) keyedSteps

let private liveSourceFollowsReplacement queued () =
    use graph = new Graph ()

    let first =
        graph.Run (fun () ->
            {
                KeyedItem.Id = 1
                Label = Api.createSignal "old"
            })

    let rows = graph.Run (fun () -> Api.createSignal [| first |])
    let target = host ()

    let mounted =
        mountTemplate queued graph strict target (Component.render keyedLiveSugar rows)

    let replacement =
        graph.Run (fun () ->
            {
                KeyedItem.Id = 1
                Label = Api.createSignal "new"
            })

    graph.Run (fun () -> rows.Value <- [| replacement |])
    mounted.Flush ()
    let afterReplace = target.textContent
    graph.Run (fun () -> replacement.Label.Value <- "new 2")
    mounted.Flush ()
    let afterNew = target.textContent
    graph.Run (fun () -> first.Label.Value <- "old 2")
    mounted.Flush ()
    equal ("1new", "1new 2", "1new 2") (afterReplace, afterNew, target.textContent)
    mounted.Dispose ()

let private arms queued () =
    pair
        queued
        (fun (graph: Graph) -> graph.Run (fun () -> Api.createSignal (armItems ())))
        (Component.render armsExplicit)
        (Component.render armsSugar)
        (fun rows ->
            [
                Write (fun () -> rows.Value[0].Mode.Value <- Counted 1)
                Write (fun () -> rows.Value[0].Mode.Value <- Counted 2)
                Write (fun () -> rows.Value[1].Mode.Value <- Plain)
                Write (fun () -> rows.Value <- Array.rev rows.Value)
            ])

/// <summary>The twins of a <c>Toggle</c> fixture: each has its own signals and log.</summary>
let private toggled queued (explicitDefinition: Component<Toggle>) (sugarDefinition: Component<Toggle>) () =
    let traces, (explicitState, sugarState) =
        twins
            queued
            (fun (graph: Graph) -> graph.Run (fun () -> toggle true))
            (Component.render explicitDefinition)
            (Component.render sugarDefinition)
            (fun state ->
                [
                    Write (fun () -> state.On.Value <- false)
                    Write (fun () -> state.Name.Value <- "m")
                    Write (fun () -> state.On.Value <- true)
                    Write (fun () -> state.On.Value <- false)
                ])

    same traces
    equal (List.ofSeq explicitState.Log) (List.ofSeq sugarState.Log)
    equal [ "mounted"; "cleanup"; "mounted"; "cleanup" ] (List.ofSeq sugarState.Log)

let private nested queued () =
    pair queued (fun (graph: Graph) -> graph.Run board) (Component.render nestedExplicit) (Component.render nestedSugar) (fun state ->
        [
            Write (fun () -> state.Entries.Value[0].Open.Value <- true)
            Write (fun () -> state.Entries.Value <- Array.rev state.Entries.Value)
            Write (fun () -> state.Listing.Value <- false)
            Write (fun () -> state.Listing.Value <- true)
            Write (fun () -> state.Entries.Value[1].Open.Value <- false)
        ])

/// <summary>A live <c>forEach</c> in a <c>matchWith</c> arm: the twins agree on markup, resources and the order of row mounts and cleanups.</summary>
let private nestedLive queued () =
    let traces, (explicitState, sugarState) =
        twins queued (fun (graph: Graph) -> graph.Run lined) (Component.render nestedLiveExplicit) (Component.render nestedLiveSugar) (fun state ->
            [
                Write (fun () -> state.Lines.Value <- Array.rev state.Lines.Value)
                Write (fun () -> state.Lines.Value <- state.Lines.Value[1..])
                Write (fun () -> state.Shown.Value <- false)
                Write (fun () -> state.Shown.Value <- true)
                Write (fun () -> state.Lines.Value[0].Label.Value <- "relabelled")
            ])

    same traces
    equal (List.ofSeq explicitState.Trail) (List.ofSeq sugarState.Trail)
    equal false (explicitState.Trail.Count = 0)

let private bounded queued () =
    pair queued (fun _ -> ()) (fun () -> Component.render boundedExplicit ()) (fun () -> Component.render boundedSugar ()) (fun () -> [])

[<Emit("(() => { const w = document.createTreeWalker($0, 0xFFFFFFFF); const a = []; while (w.nextNode()) a.push(w.currentNode); return a; })()")>]
let private nodesOf (root: Node) : Node array = jsNative

[<Emit("$1.contains($0)")>]
let private within (node: Node) (root: Node) : bool = jsNative

let private omit =
    { Server.Options.defaults with
        Policy = Omit
    }

let private strictOptions: Hydration.HydrationOptions =
    { IdPrefix = "r"; Strict = true }

/// <summary>
/// Hydrates the explicit and the sugared definition from the server HTML of <c>served</c>. Both adopt exactly
/// <c>adopts</c> server nodes, keep the same nodes after each write, and show the same markup after hydration, each write and disposal.
/// </summary>
let private hydratedTwins
    (served: Component<'props>)
    (explicitDefinition: Component<'props>)
    (sugarDefinition: Component<'props>)
    (props: 'props)
    (writes: (unit -> unit) list)
    (adopts: int)
    (graph: Graph)
    =
    let html = Server.render omit served props
    let left, right = host (), host ()
    left.innerHTML <- html
    right.innerHTML <- html
    let leftNodes, rightNodes = nodesOf left, nodesOf right

    let explicitMount =
        Hydration.hydrate graph None strict strictOptions left explicitDefinition props

    let sugarMount =
        Hydration.hydrate graph None strict strictOptions right sugarDefinition props

    let adopted (before: Node array) (root: HTMLElement) =
        before
        |> Array.filter (fun node -> within node root)
        |> Array.length

    equal adopts (adopted leftNodes left)
    equal adopts (adopted rightNodes right)
    equal left.innerHTML right.innerHTML

    for write in writes do
        graph.Run write
        equal (adopted leftNodes left) (adopted rightNodes right)
        equal left.innerHTML right.innerHTML

    explicitMount.Dispose ()
    sugarMount.Dispose ()
    equal left.innerHTML right.innerHTML

/// <summary>Server nodes adopted per case by this branch's hydration, where lists and arms mount fresh; update these when the hydration branch is merged.</summary>
module private Adopted =
    /// <summary>All 6 server nodes.</summary>
    let bench = 6
    /// <summary>6 of 21 server nodes.</summary>
    let keyed = 6
    /// <summary>6 of 22 server nodes.</summary>
    let arms = 6
    let show = 7
    /// <summary>5 of 19 server nodes.</summary>
    let nested = 5

/// <summary>Each pair hydrated from the explicit twin's HTML and from the sugared twin's HTML.</summary>
let private hydration () =
    for fromSugar in [ false; true ] do
        use graph = new Graph ()

        let pick (explicitDefinition: Component<'p>) (sugarDefinition: Component<'p>) =
            if fromSugar then sugarDefinition else explicitDefinition

        hydratedTwins (pick benchExplicit benchSugar) benchExplicit benchSugar 3 [] Adopted.bench graph
        let rows = graph.Run (fun () -> Api.createSignal (keyedItems ()))

        hydratedTwins
            (pick keyedLiveExplicit keyedLiveSugar)
            keyedLiveExplicit
            keyedLiveSugar
            rows
            [ fun () -> rows.Value <- Array.rev rows.Value ]
            Adopted.keyed
            graph

        let armed = graph.Run (fun () -> Api.createSignal (armItems ()))
        hydratedTwins (pick armsExplicit armsSugar) armsExplicit armsSugar armed [ fun () -> armed.Value[1].Mode.Value <- Plain ] Adopted.arms graph
        let state = graph.Run (fun () -> toggle false)

        hydratedTwins
            (pick showExplicit showSugar)
            showExplicit
            showSugar
            state
            [ (fun () -> state.On.Value <- true); (fun () -> state.On.Value <- false) ]
            Adopted.show
            graph

        let listed = graph.Run board

        hydratedTwins
            (pick nestedExplicit nestedSugar)
            nestedExplicit
            nestedSugar
            listed
            [ fun () -> listed.Entries.Value[0].Open.Value <- true ]
            Adopted.nested
            graph

let cases: (string * (unit -> unit)) array =
    strictCases (
        Array.concat
            [
                templated "sugared benchmark row clicks like its twin" benchClicks
                templated "sugared text forms update like their twin" textUpdates
                templated "forEach reorders, removes, reinserts, rejects duplicates and replaces like its twin" keyedLive
                templated "forEachFixed reorders, removes, reinserts, rejects duplicates and replaces like its twin" keyedFixed
                templated "a live forEach row reads the replacement item's source" liveSourceFollowsReplacement
                templated "armOf arms switch and update their payload like their twins" arms
                templated "show toggles with cleanups like its twin" (fun queued -> toggled queued showExplicit showSugar)
                templated "showElse toggles with cleanups like its twin" (fun queued -> toggled queued showElseExplicit showElseSugar)
                templated "show in forEachFixed in an armOf arm updates like its twin" nested
                templated "a live forEach in an armOf arm mounts and cleans up rows like its twin" nestedLive
                templated "a boundary around sugared content renders like its twin" bounded
                [| "sugared and explicit twins hydrate each other's server HTML", hydration |]
            ]
    )
