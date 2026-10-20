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

/// <summary>The graph counters as name-value pairs; empty without <c>RANVIER_COUNTERS</c>.</summary>
let private tally () : (string * float) list =
#if RANVIER_COUNTERS
    [ for name, value in Counters.Snapshot () -> name, float value ]
#else
    []
#endif

let private delta (before: (string * float) list) (after: (string * float) list) =
    List.map2 (fun (name, was) (_, now) -> name, now - was) before after

let private click (selector: string) (root: HTMLElement) =
    (root.querySelector selector |> unbox<HTMLElement>).click()

/// <summary>
/// What a twin pair did: markup, reports, counter deltas of the mount, the steps that act on one side and the disposal,
/// listener registrations per host, and native writes per step.
/// </summary>
type private Trace =
    {
        Shots: (string * string) list
        LeftReports: string list list
        RightReports: string list list
        Counts: ((string * float) list * (string * float) list) list
        Listeners: int * int
        Writes: (string array * string array) list
    }

/// <summary>Both hosts' behaviour across the mount, each step and the disposal.</summary>
let private twins queued (graph: Graph) (explicitView: Program.View) (sugarView: Program.View) (steps: Step list) =
    let left, right = host (), host ()
    let leftReports, rightReports = ResizeArray<exn list>(), ResizeArray<exn list>()
    let leftWrites, rightWrites = watchWrites left, watchWrites right
    let listeners = watchListeners ()

    try
        let counts = ResizeArray ()
        let writes = ResizeArray ()

        let mount target (reports: ResizeArray<exn list>) view =
            let before = tally ()
            let mounted = mountTemplate queued graph reports.Add target view
            mounted.Flush ()
            mounted, delta before (tally ())

        let explicitMount, explicitCounts = mount left leftReports explicitView
        let sugarMount, sugarCounts = mount right rightReports sugarView
        counts.Add ((explicitCounts, sugarCounts))

        let shot () =
            explicitMount.Flush ()
            sugarMount.Flush ()
            writes.Add ((leftWrites.drain (), rightWrites.drain ()))
            left.innerHTML, right.innerHTML

        let measured (flush: unit -> unit) (act: unit -> unit) =
            let before = tally ()
            act ()
            flush ()
            delta before (tally ())

        let shots = ResizeArray [ shot () ]

        for step in steps do
            match step with
            | Write write -> graph.Run write
            | Each act ->
                let explicitDelta = measured explicitMount.Flush (fun () -> act left)
                let sugarDelta = measured sugarMount.Flush (fun () -> act right)
                counts.Add ((explicitDelta, sugarDelta))

            shots.Add (shot ())

        let explicitDelta = measured ignore explicitMount.Dispose
        let sugarDelta = measured ignore sugarMount.Dispose
        counts.Add ((explicitDelta, sugarDelta))
        writes.Add ((leftWrites.drain (), rightWrites.drain ()))
        shots.Add ((left.innerHTML, right.innerHTML))

        {
            Shots = List.ofSeq shots
            LeftReports = messages leftReports
            RightReports = messages rightReports
            Counts = List.ofSeq counts
            Listeners = listeners.under left, listeners.under right
            Writes = List.ofSeq writes
        }
    finally
        leftWrites.stop ()
        rightWrites.stop ()
        listeners.stop ()

let private same (trace: Trace) =
    for index, (left, right) in List.indexed trace.Shots do
        equal (index, left) (index, right)

    equal trace.LeftReports trace.RightReports

    for index, (explicitCounts, sugarCounts) in List.indexed trace.Counts do
        equal (index, explicitCounts) (index, sugarCounts)

    equal (fst trace.Listeners) (snd trace.Listeners)

    for index, (left, right) in List.indexed trace.Writes do
        equal (index, left) (index, right)

let private benchClicks queued () =
    use graph = new Graph ()
    same (twins queued graph (Component.render benchExplicit 3) (Component.render benchSugar 3) [ Each (click "button"); Each (click "button") ])

let private textUpdates queued () =
    use graph = new Graph ()
    let name = graph.Run (fun () -> Api.createSignal "a")
    same (twins queued graph (Component.render textsExplicit name) (Component.render textsSugar name) [ Write (fun () -> name.Value <- "b") ])

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

let private keyedLive queued () =
    use graph = new Graph ()
    let rows = graph.Run (fun () -> Api.createSignal (keyedItems ()))
    same (twins queued graph (Component.render keyedLiveExplicit rows) (Component.render keyedLiveSugar rows) (keyedSteps rows))

let private keyedFixed queued () =
    use graph = new Graph ()
    let rows = graph.Run (fun () -> Api.createSignal (keyedItems ()))
    same (twins queued graph (Component.render keyedFixedExplicit rows) (Component.render keyedFixedSugar rows) (keyedSteps rows))

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
    use graph = new Graph ()
    let rows = graph.Run (fun () -> Api.createSignal (armItems ()))

    same (
        twins
            queued
            graph
            (Component.render armsExplicit rows)
            (Component.render armsSugar rows)
            [
                Write (fun () -> rows.Value[0].Mode.Value <- Counted 1)
                Write (fun () -> rows.Value[0].Mode.Value <- Counted 2)
                Write (fun () -> rows.Value[1].Mode.Value <- Plain)
                Write (fun () -> rows.Value <- Array.rev rows.Value)
            ]
    )

/// <summary>The twins of a <c>Toggle</c> fixture: each side has its own log, and both read the same signals.</summary>
let private toggled queued (explicitDefinition: Component<Toggle>) (sugarDefinition: Component<Toggle>) () =
    use graph = new Graph ()
    let left = graph.Run (fun () -> toggle true)
    let right = { left with Log = ResizeArray () }

    let trace =
        twins
            queued
            graph
            (Component.render explicitDefinition left)
            (Component.render sugarDefinition right)
            [
                Write (fun () -> left.On.Value <- false)
                Write (fun () -> left.Name.Value <- "m")
                Write (fun () -> left.On.Value <- true)
                Write (fun () -> left.On.Value <- false)
            ]

    same trace
    equal (List.ofSeq left.Log) (List.ofSeq right.Log)
    equal [ "mounted"; "cleanup"; "mounted"; "cleanup" ] (List.ofSeq right.Log)

let private nested queued () =
    use graph = new Graph ()
    let state = graph.Run board

    same (
        twins
            queued
            graph
            (Component.render nestedExplicit state)
            (Component.render nestedSugar state)
            [
                Write (fun () -> state.Entries.Value[0].Open.Value <- true)
                Write (fun () -> state.Entries.Value <- Array.rev state.Entries.Value)
                Write (fun () -> state.Listing.Value <- false)
                Write (fun () -> state.Listing.Value <- true)
                Write (fun () -> state.Entries.Value[1].Open.Value <- false)
            ]
    )

let private bounded queued () =
    use graph = new Graph ()
    same (twins queued graph (Component.render boundedExplicit ()) (Component.render boundedSugar ()) [])

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
/// Hydrates the explicit and the sugared definition from the server HTML of <c>served</c>; both adopt the same number of
/// server nodes and show the same markup after hydration, each write and disposal.
/// </summary>
let private hydratedTwins
    (served: Component<'props>)
    (explicitDefinition: Component<'props>)
    (sugarDefinition: Component<'props>)
    (props: 'props)
    (writes: (unit -> unit) list)
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

    equal (adopted leftNodes left) (adopted rightNodes right)
    equal left.innerHTML right.innerHTML

    for write in writes do
        graph.Run write
        equal left.innerHTML right.innerHTML

    explicitMount.Dispose ()
    sugarMount.Dispose ()
    equal left.innerHTML right.innerHTML

/// <summary>Each pair hydrated from the explicit twin's HTML and from the sugared twin's HTML.</summary>
let private hydration () =
    for fromSugar in [ false; true ] do
        use graph = new Graph ()

        let pick (explicitDefinition: Component<'p>) (sugarDefinition: Component<'p>) =
            if fromSugar then sugarDefinition else explicitDefinition

        hydratedTwins (pick benchExplicit benchSugar) benchExplicit benchSugar 3 [] graph
        let rows = graph.Run (fun () -> Api.createSignal (keyedItems ()))

        hydratedTwins
            (pick keyedLiveExplicit keyedLiveSugar)
            keyedLiveExplicit
            keyedLiveSugar
            rows
            [ fun () -> rows.Value <- Array.rev rows.Value ]
            graph

        let armed = graph.Run (fun () -> Api.createSignal (armItems ()))
        hydratedTwins (pick armsExplicit armsSugar) armsExplicit armsSugar armed [ fun () -> armed.Value[1].Mode.Value <- Plain ] graph
        let state = graph.Run (fun () -> toggle false)

        hydratedTwins
            (pick showExplicit showSugar)
            showExplicit
            showSugar
            state
            [ (fun () -> state.On.Value <- true); (fun () -> state.On.Value <- false) ]
            graph

        let listed = graph.Run board

        hydratedTwins
            (pick nestedExplicit nestedSugar)
            nestedExplicit
            nestedSugar
            listed
            [ fun () -> listed.Entries.Value[0].Open.Value <- true ]
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
                templated "a boundary around sugared content renders like its twin" bounded
                [| "sugared and explicit twins hydrate each other's server HTML", hydration |]
            ]
    )
