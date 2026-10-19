module BoundaryChecks

open System
open Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Scheduling
open Ranvier.View.Foundation.Mounting

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

/// <summary>Renders a boundary over one async source as HTML while it is pending, settled and failed.</summary>
let private server (ops: Operations<'view>) (run: 'view -> Backend -> unit) =
    use graph = new Graph ()

    graph.Run (fun () ->
        Api.createRoot (fun _ ->
            let source = Api.createAsyncSource<string>()

            let view =
                ops.Boundary (
                    ops.Text "<loading>",
                    (fun failure -> ops.Text failure.Error.Message),
                    fun _ -> Typed.div ops [||] [| Typed.reactiveText ops (fun () -> source.Value) |]
                )

            let render () =
                Html.render Reject (run view)

            equal "<!--[-->&lt;loading&gt;<!--]-->" (render ())
            source.Settle "<&"
            equal "<!--[--><div>&lt;&amp;</div><!--]-->" (render ())
            source.Fail (Exception "<failed>")
            equal "<!--[-->&lt;failed&gt;<!--]-->" (render ())))

let cases: (string * (unit -> unit)) array =
    [|
        "tree HTML renders a boundary's pending, content or error view between markers", fun () -> server Tree.ops Tree.run
        "program HTML renders a boundary's pending, content or error view between markers", fun () -> server Program.ops Program.run
    |]
