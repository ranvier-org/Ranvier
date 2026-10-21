module NoTargets.Site

open global.Ranvier
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
