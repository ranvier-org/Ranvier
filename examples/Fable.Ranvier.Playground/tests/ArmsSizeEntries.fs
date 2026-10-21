/// <summary>The bundle-size entries of experiments/Ranvier.View.Arms/Checks/size.mjs: loader sites as Arms.Match and as their explicit twins.</summary>
module ArmsSizeEntries

open global.Ranvier
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms

type Load =
    | Idle
    | Loading of percent: int
    | Loaded of text: string * count: int

let private mountAll (graph: Graph) (host: HTMLElement) (views: Program.View list) =
    for view in views do
        Mounting.mountWith Scheduling.SynchronousCommits graph None ignore host None (fun context -> Program.run view context.Backend)
        |> ignore

let private matchLoader () =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "loader") {
            Arms.Match (
                (fun (state: Signal<Load>) -> state.Value),
                function
                | Idle -> span () { "Not started" }
                | Loading percent -> progress (Attrs.attribute ("max", "100"), Attrs.attribute ("value", fun _ -> Some (string percent))) { }
                | Loaded (text, count) ->
                    pre () {
                        fun _ -> text
                        " x"
                        fun _ -> string count
                    }
            )
        })

let private explicitLoader () =
    let idle = armOf (span () { "Not started" })

    let loading =
        armOf (progress (Attrs.attribute ("max", "100"), Attrs.attribute ("value", fun (payload: unit -> int) -> Some (string (payload ())))) { })

    let loaded =
        armOf (
            pre () {
                fun (payload: unit -> string * int) -> fst (payload ())
                " x"
                fun (payload: unit -> string * int) -> string (snd (payload ()))
            }
        )

    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "loader") {
            matchWith (fun (state: Signal<Load>) -> state.Value) (function
                | Idle -> arm idle ()
                | Loading percent -> arm loading percent
                | Loaded (text, count) -> arm loaded (text, count))
        })

let private matchRich () =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "rich") {
            Arms.Match (
                (fun (state: Signal<Load>) -> state.Value),
                function
                | Idle
                | Loading 0 -> span () { "Not started" }
                | Loading percent -> b () { fun _ -> string percent }
                | Loaded (_, 0) -> span () { "Empty" }
                | Loaded (text, _) -> i () { fun _ -> text }
            )
        })

let private explicitRich () =
    let idle = armOf (span () { "Not started" })
    let percent = armOf (b () { fun (payload: unit -> int) -> string (payload ()) })
    let empty = armOf (span () { "Empty" })
    let text = armOf (i () { fun (payload: unit -> string) -> payload () })

    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div (Attrs.className "rich") {
            matchWith (fun (state: Signal<Load>) -> state.Value) (function
                | Idle
                | Loading 0 -> arm idle ()
                | Loading value -> arm percent value
                | Loaded (_, 0) -> arm empty ()
                | Loaded (value, _) -> arm text value)
        })

let matchOne graph host (state: Signal<Load>) =
    Arms.Prepare ()
    mountAll graph host [ Component.render (matchLoader ()) state ]

let explicitOne graph host (state: Signal<Load>) =
    mountAll graph host [ Component.render (explicitLoader ()) state ]

let matchTwo graph host (state: Signal<Load>) =
    mountAll
        graph
        host
        [
            Component.render (matchLoader ()) state
            Component.render (matchRich ()) state
        ]

let explicitTwo graph host (state: Signal<Load>) =
    mountAll
        graph
        host
        [
            Component.render (explicitLoader ()) state
            Component.render (explicitRich ()) state
        ]

/// <summary>An app that compiles the Arms.Match runtime and calls no Arms.Match.</summary>
let plain graph host (state: Signal<Load>) =
    mountAll
        graph
        host
        [
            Component.render (Component.template (fun _ (state: Signal<Load>) -> state) (div () { "plain" })) state
        ]
