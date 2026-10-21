/// <summary>One Arms.Match call, for the trimmed publish of check.sh.</summary>
module TrimCallSite.Main

open global.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms

let site =
    Component.template
        (fun _ (state: Signal<int option>) -> state)
        (div () {
            Arms.Match (
                (fun (state: Signal<int option>) -> state.Value),
                function
                | Some n -> b () { fun _ -> string n }
                | None -> span () { "none" }
            )
        })

[<EntryPoint>]
let main _ =
    use graph = new Graph ()
    let state = graph.Run (fun () -> Api.createSignal (Some 1))
    printfn "%s" (Server.render Server.Options.defaults site state)
    0
