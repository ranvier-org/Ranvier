module Ranvier.View.Twin.Tests.RowMetadataTests

open Expecto
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.Twin

let private row =
    Component.define (fun _ (item: unit -> int) -> item) (fun t ->
        t.li
            [|
                t.Hooks.exit (System.Func<_, _, _, _>(fun (_: unit -> int) (_: Fable.Core.TS.Dom.HTMLLIElement) _ -> ignore))
            |]
            [| t.text (fun item -> string (item ())) |])

let private rows =
    Component.define (fun _ (items: Signal<int array>) -> items) (fun t ->
        t.ul [||] [| t.each id (fun (items: Signal<int array>) -> items.Value) row |])

[<Tests>]
let tests =
    testList
        "RowMetadata"
        [
            testCase "held row keys survive retirement and disappear after physical removal"
            <| fun () ->
                let items = ref Unchecked.defaultof<Signal<int array>>

                use session =
                    Twin.mountView TwinMount.defaults (fun context ->
                        items.Value <- Api.createSignal [| 17 |]
                        Program.run (Component.render rows items.Value) context.Backend)

                let rec firstRow (node: TwinNode) =
                    match Inspect.probeOf (unbox node.Native) with
                    | Some (Inspect.ListProbe (_, readRows, _)) -> readRows () |> Array.tryHead |> Option.map snd
                    | _ -> node.Children |> Seq.tryPick firstRow

                let first =
                    firstRow (Bridge.nodeOf session.Host)
                    |> Option.get

                Expect.equal (Inspect.rowKeyOf first) (Some (box 17)) "active row key"
                session.Act (fun () -> items.Value.Value <- [||])
                Expect.equal (Inspect.rowKeyOf first) (Some (box 17)) "retired row retains its key during exit"
                session.AdvanceBy 10000.0
                Expect.isNone (Inspect.rowKeyOf first) "finished row metadata clears"
                session.Host.appendChild first |> ignore
                Expect.isNone (Inspect.rowKeyOf first) "cleared metadata stays absent after reattachment"
        ]
