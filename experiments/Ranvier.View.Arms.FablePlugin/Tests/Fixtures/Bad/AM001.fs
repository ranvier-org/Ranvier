module Fixtures.Bad.AM001

#if BAD_AM001
open global.Ranvier
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms
open Fixtures.Types

let site =
    Component.template
        (fun _ (state: Signal<Load>) -> state)
        (div () {
            Arms.Match (
                (fun (state: Signal<Load>) -> state.Value),
                function
                | Loaded (text, count) when count > 0 -> span () { text }
                | _ -> span () { "Other" }
            )
        })
#endif
