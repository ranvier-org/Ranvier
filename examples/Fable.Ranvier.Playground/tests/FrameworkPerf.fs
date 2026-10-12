module FrameworkPerf

open global.Ranvier
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation

/// <summary>Mounts the overloaded fixture with its own graph and returns update and disposal callbacks.</summary>
let mount (host: HTMLElement) width =
    let graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")

    let disabled =
        graph.Run (fun () -> Api.createMemo (fun _ -> source.Value = "blocked"))

    let rows = AuthoringPerfFixtures.create "overloaded" width source disabled ignore
    let view = Generated.Html.Program.section [||] [| rows |]

    let mounted =
        Mounting.mount graph None (fun errors -> failwithf "%A" errors) host None (fun context -> Program.run view context.Backend)

    let mutable live = true

    let update value =
        if live then
            graph.Run (fun () -> source.Value <- value)

    let dispose () =
        if live then
            live <- false
            mounted.Dispose ()
            graph.Dispose ()

    update, dispose
