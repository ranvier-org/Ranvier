module App

open System
open Fable.Core
open Fable.Core.TS.Dom
open Fable.Ranvier
open global.Ranvier
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Template

module FoundationMounting = Ranvier.View.Foundation.Mounting
module FoundationProgram = Ranvier.View.Foundation.Representation.Program
module View = Ranvier.View.Foundation.Authoring
module GeneratedHtml = Ranvier.View.Foundation.Generated.Html
module Elements = GeneratedHtml.Program
module GeneratedAttrs = GeneratedHtml.Attrs
module FoundationMicrotask = Ranvier.View.Foundation.Microtask
module FoundationScheduling = Ranvier.View.Foundation.Scheduling

let private node tag modifiers children : Node =
    Dom.element tag modifiers children

let private css value =
    Dom.attribute "class" value

let private testId value =
    Dom.attribute "data-testid" value

/// <summary>The demo's state, kept on the app's graph so a remount keeps it.</summary>
type DemoState =
    {
        Count: Signal<int>
        Name: Signal<string>
    }

type DemoInstance =
    { State: DemoState; Doubled: Memo<int> }

let private demoView =
    Component.define
        (fun _ (state: DemoState) ->
            {
                State = state
                Doubled = Api.createMemo (fun _ -> state.Count.Value * 2)
            })
        (fun t ->
            t.section
                [|
                    t.Attrs.className "demo-grid"
                    t.Attrs.data ("testid", "demo-root")
                    t.Attrs.data ("parity", (fun (demo: DemoInstance) -> Some (if demo.State.Count.Value % 2 = 0 then "even" else "odd")))
                |]
                [|
                    t.article
                        [| t.Attrs.className "panel counter-panel" |]
                        [|
                            t.div
                                [| t.Attrs.className "panel-heading" |]
                                [|
                                    t.span [| t.Attrs.className "eyebrow" |] [| t.text "01 / Counter" |]
                                    t.span [| t.Attrs.className "tag" |] [| t.text "signal → text" |]
                                |]
                            t.output
                                [|
                                    t.Attrs.className "count"
                                    t.Attrs.data ("testid", "count")
                                    t.Attrs.aria ("live", "polite")
                                |]
                                [| t.text (fun (demo: DemoInstance) -> string demo.State.Count.Value) |]
                            t.div
                                [| t.Attrs.className "derived" |]
                                [|
                                    t.span [||] [| t.text "Doubled" |]
                                    t.output
                                        [| t.Attrs.data ("testid", "doubled") |]
                                        [| t.text (fun (demo: DemoInstance) -> string demo.Doubled.Value) |]
                                |]
                            t.div
                                [| t.Attrs.className "actions" |]
                                [|
                                    t.button
                                        [|
                                            t.Attrs.className "primary"
                                            t.Attrs.data ("testid", "increment")
                                            t.Attrs.attribute ("type", "button")
                                            t.Events.onClick (fun (demo: DemoInstance) _ -> demo.State.Count.Value <- demo.State.Count.Value + 1)
                                        |]
                                        [| t.text "Increment +1" |]
                                    t.button
                                        [|
                                            t.Attrs.className "secondary"
                                            t.Attrs.data ("testid", "reset")
                                            t.Attrs.attribute ("type", "button")
                                            t.Props.Button.disabled (fun (demo: DemoInstance) -> demo.State.Count.Value = 0)
                                            t.Events.onClick (fun (demo: DemoInstance) _ -> demo.State.Count.Value <- 0)
                                        |]
                                        [| t.text "Reset" |]
                                    t.button
                                        [|
                                            t.Attrs.className "secondary"
                                            t.Attrs.data ("testid", "burst")
                                            t.Attrs.attribute ("type", "button")
                                            t.Events.onClick (fun (demo: DemoInstance) _ ->
                                                Api.batch (fun () ->
                                                    for _ in 1..100 do
                                                        demo.State.Count.Value <- demo.State.Count.Value + 1))
                                        |]
                                        [| t.text "Burst +100" |]
                                |]
                            t.p [| t.Attrs.className "note" |] [| t.text "The output changes. The DOM nodes stay." |]
                        |]
                    t.article
                        [| t.Attrs.className "panel name-panel" |]
                        [|
                            t.div
                                [| t.Attrs.className "panel-heading" |]
                                [|
                                    t.span [| t.Attrs.className "eyebrow" |] [| t.text "02 / Input" |]
                                    t.span [| t.Attrs.className "tag" |] [| t.text "event → signal" |]
                                |]
                            t.label [| t.Attrs.attribute ("for", "name-field") |] [| t.text "Your name" |]
                            t.input
                                [|
                                    t.Attrs.id "name-field"
                                    t.Attrs.attribute ("type", "text")
                                    t.Attrs.attribute ("autocomplete", "off")
                                    t.Attrs.data ("testid", "name")
                                    t.Props.Input.value (fun (demo: DemoInstance) -> demo.State.Name.Value)
                                    t.Events.onInput (fun (demo: DemoInstance) event ->
                                        demo.State.Name.Value <- (unbox<HTMLInputElement> event.currentTarget.Value).value)
                                |]
                            t.p
                                [|
                                    t.Attrs.className "greeting"
                                    t.Attrs.data ("testid", "greeting")
                                    t.Attrs.aria ("live", "polite")
                                |]
                                [| t.text (fun (demo: DemoInstance) -> $"Hello, {demo.State.Name.Value}") |]
                            t.p [| t.Attrs.className "note" |] [| t.text "Type anywhere in the field. Focus and selection are preserved." |]
                        |]
                |])

let start () : unit -> unit =
    let graph = new Graph ()
    let foundationGraph = new Graph ()

    try
        let host =
            Global.document.getElementById ("app")
            |> Option.defaultWith (fun () -> invalidOp "The playground needs an #app host.")

        let count, name, mounted, queued =
            graph.Run (fun () -> createSignal 0, createSignal "world", createSignal true, createSignal false)

        let demoHost = Dom.createElement "div"
        let foundationHost = Dom.createElement "section"
        let mutable demo: FoundationMounting.Mounted option = None

        let mountDemo () =
            let mode =
                if queued.Value then
                    FoundationMicrotask.mode FoundationScheduling.CollectAndContinue
                else
                    FoundationScheduling.SynchronousCommits

            demo <-
                Some (
                    FoundationMounting.mountWith mode graph None ignore demoHost None (fun context ->
                        FoundationProgram.run (Component.render demoView { Count = count; Name = name }) context.Backend)
                )

        let toggle () =
            match demo with
            | Some current ->
                current.Dispose ()
                demo <- None
                mounted.Value <- false
            | None ->
                mountDemo ()
                mounted.Value <- true

        let toggleScheduling () =
            queued.Value <- not queued.Value

            match demo with
            | Some current ->
                current.Dispose ()
                demo <- None
                mountDemo ()
            | None -> ()

        let shell =
            Mount.mount graph host (fun () ->
                node
                    "div"
                    [ css "playground" ]
                    [
                        node
                            "header"
                            [ css "page-heading" ]
                            [
                                node
                                    "div"
                                    [ css "brand" ]
                                    [
                                        node "span" [ css "brand-mark" ] [ Dom.text "r" ]
                                        node "span" [] [ Dom.text "Fable.Ranvier" ]
                                    ]
                                node "span" [ css "preview" ] [ Dom.text "DOM PoC" ]
                            ]
                        node
                            "section"
                            [ css "intro" ]
                            [
                                node "p" [ css "eyebrow" ] [ Dom.text "F# · Fable · Browser DOM" ]
                                node
                                    "h1"
                                    []
                                    [
                                        Dom.text "Create once."
                                        node "br" [] []
                                        node "span" [] [ Dom.text "Update what changes." ]
                                    ]
                                node
                                    "p"
                                    [ css "lead" ]
                                    [
                                        Dom.text
                                            "A small playground for reactive text, properties and events. No component tree rebuild on each edit."
                                    ]
                            ]
                        node
                            "div"
                            [ css "mount-bar" ]
                            [
                                node
                                    "span"
                                    [ testId "scheduling-status" ]
                                    [
                                        Dom.reactiveText (fun () ->
                                            if queued.Value then
                                                "Microtask DOM commits · batched events"
                                            else
                                                "Synchronous DOM commits · batched events")
                                    ]
                                node
                                    "button"
                                    [
                                        css "secondary"
                                        testId "scheduling-toggle"
                                        Dom.attribute "type" "button"
                                        Dom.on "click" (fun _ -> toggleScheduling ())
                                    ]
                                    [
                                        Dom.reactiveText (fun () -> if queued.Value then "Use synchronous" else "Use microtasks")
                                    ]
                                node
                                    "button"
                                    [
                                        css "secondary"
                                        testId "flush-dom"
                                        Dom.attribute "type" "button"
                                        Dom.on "click" (fun _ ->
                                            demo
                                            |> Option.iter (fun current -> current.Flush ()))
                                    ]
                                    [ Dom.text "Flush DOM" ]
                            ]
                        node
                            "div"
                            [ css "mount-bar" ]
                            [
                                node
                                    "div"
                                    [ css "mount-state" ]
                                    [
                                        node
                                            "span"
                                            [
                                                css "status-dot"
                                                Dom.bindAttribute "data-active" (fun () -> if mounted.Value then Some "true" else None)
                                            ]
                                            []
                                        node
                                            "span"
                                            [ testId "mount-status" ]
                                            [
                                                Dom.reactiveText (fun () -> if mounted.Value then "Demo mounted" else "Demo unmounted")
                                            ]
                                    ]
                                node
                                    "button"
                                    [
                                        css "secondary toggle"
                                        testId "mount-toggle"
                                        Dom.attribute "type" "button"
                                        Dom.on "click" (fun _ -> toggle ())
                                    ]
                                    [
                                        Dom.reactiveText (fun () -> if mounted.Value then "Unmount demo" else "Remount demo")
                                    ]
                            ]
                        demoHost
                        foundationHost
                        node
                            "section"
                            [
                                css "empty-state"
                                Dom.bindAttribute "hidden" (fun () -> if mounted.Value then Some "" else None)
                            ]
                            [
                                node "h2" [] [ Dom.text "The mount is disposed." ]
                                node
                                    "p"
                                    []
                                    [
                                        Dom.text
                                            "Its nodes, bindings and event listeners have been removed. Remount to continue with the same state."
                                    ]
                            ]
                        node
                            "footer"
                            []
                            [
                                Dom.text "Built with Xantham DOM bindings and Ranvier scopes. Experimental API; DOM scheduling is opt-in."
                            ]
                    ])

        mountDemo ()

        let foundationRows =
            foundationGraph.Run (fun () -> createSignal [| 1, "Alpha"; 2, "Beta" |])

        let loadSource =
            foundationGraph.Run (fun () -> createSignal (createAsyncSource<string>()))

        let loadResult =
            foundationGraph.Run (fun () ->
                createBoundary (fun _ -> "Loading…") (fun _ _ -> "Could not load the result") (fun () -> loadSource.Value.Value))

        foundationGraph.Run (fun () -> loadSource.Value.Settle "Loaded result")

        let foundationRowsView =
            View.keyedArray fst (fun () -> foundationRows.Value) (fun current ->
                let key = fst (current ())

                Elements.div [| GeneratedAttrs.idStatic ("foundation-row-" + string key) |] [| View.reactiveText (fun () -> snd (current ())) |])

        let foundationView =
            Elements.section
                [|
                    GeneratedAttrs.idStatic "foundation-demo"
                    GeneratedAttrs.classStatic "panel"
                    GeneratedAttrs.dataReactive "row-count" (fun () -> Some (string foundationRows.Value.Length))
                |]
                [|
                    View.text "Foundation implementation: keyed rows"
                    Elements.button
                        [|
                            GeneratedAttrs.idStatic "foundation-add"
                            GeneratedHtml.Button.onClick (fun _ ->
                                let next =
                                    foundationRows.Value
                                    |> Array.map fst
                                    |> Array.max
                                    |> (+) 1

                                foundationRows.Value <- Array.append foundationRows.Value [| next, "Item " + string next |])
                        |]
                        [| View.text "Add row" |]
                    Elements.button
                        [|
                            GeneratedAttrs.idStatic "foundation-reverse"
                            GeneratedHtml.Button.onClick (fun _ -> foundationRows.Value <- Array.rev foundationRows.Value)
                        |]
                        [| View.text "Reverse rows" |]
                    foundationRowsView
                |]

        let foundation =
            FoundationMounting.mountWith
                (FoundationMicrotask.mode FoundationScheduling.CollectAndContinue)
                foundationGraph
                None
                ignore
                foundationHost
                None
                (fun context ->
                    FoundationProgram.run foundationView context.Backend
                    FoundationProgram.run (ComponentsExample.view ()) context.Backend

                    let action id label update =
                        Elements.button
                            [|
                                GeneratedAttrs.idStatic id
                                GeneratedHtml.Button.onClick (fun _ -> update ())
                            |]
                            [| View.text label |]

                    let statusPanel =
                        Elements.div
                            [|
                                GeneratedAttrs.idStatic "foundation-load"
                                GeneratedAttrs.roleStatic "region"
                                GeneratedAttrs.ariaLabelStatic "Load result"
                                GeneratedAttrs.ariaBusyReactive (fun () ->
                                    loadResult.Value |> ignore
                                    Some loadResult.IsWaiting)
                            |]
                            [|
                                View.text "Load result"
                                Elements.div
                                    [| GeneratedAttrs.idStatic "foundation-load-status" |]
                                    [|
                                        View.reactiveText (fun () ->
                                            loadResult.Value |> ignore

                                            if loadResult.IsWaiting then "Pending"
                                            elif not (isNull loadResult.Caught) then "Failed"
                                            else "Ready")
                                    |]
                                Elements.div
                                    [| GeneratedAttrs.idStatic "foundation-load-result" |]
                                    [| View.reactiveText (fun () -> loadResult.Value) |]
                                Elements.div
                                    [| GeneratedAttrs.idStatic "foundation-load-error" |]
                                    [|
                                        View.reactiveText (fun () ->
                                            loadResult.Value |> ignore

                                            if isNull loadResult.Caught then
                                                ""
                                            else
                                                loadResult.Caught.Message)
                                    |]
                                action "foundation-pending" "Start loading" (fun () ->
                                    foundationGraph.Run (fun () -> loadSource.Value <- createAsyncSource<string>()))
                                action "foundation-fail" "Simulate failure" (fun () -> loadSource.Value.Fail (Exception "Connection unavailable"))
                                action "foundation-recover" "Recover" (fun () -> loadSource.Value.Settle "Recovered result")
                            |]

                    FoundationProgram.run statusPanel context.Backend)

        let mutable stopped = false

        fun () ->
            if not stopped then
                stopped <- true

                foundation.Dispose ()
                foundationGraph.Dispose ()

                demo
                |> Option.iter (fun scope -> scope.Dispose ())

                demo <- None
                shell.Dispose ()
                graph.Dispose ()
    with _ ->
        foundationGraph.Dispose ()
        graph.Dispose ()
        reraise ()
