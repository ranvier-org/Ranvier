module Ranvier.View.Twin.Tests.InspectTests

open Expecto
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.Twin
open Ranvier.View.Twin.Tests.Fixtures

let private probes (session: Session) =
    let rec walk (node: TwinNode) =
        [
            match Inspect.probeOf (unbox node.Native) with
            | Some probe -> probe
            | None -> ()

            for child in node.Children do
                yield! walk child
        ]

    walk (Bridge.nodeOf session.Host)

[<Tests>]
let tests =
    testList
        "Inspect"
        [
            testCase "a keyed list registers its rows' keys in order"
            <| fun () ->
                let source = ref Unchecked.defaultof<Signal<int array>>

                use session =
                    Twin.mountView TwinMount.defaults (fun context ->
                        source.Value <- Api.createSignal [| 3; 1; 2 |]
                        Program.run (Component.render rows source.Value) context.Backend)

                let keys =
                    probes session
                    |> List.pick (fun p ->
                        match p with
                        | Inspect.ListProbe (_, rows) ->
                            Some (
                                rows ()
                                |> Array.map (fun (key, _) -> unbox<int> key)
                            )
                        | _ -> None)

                Expect.equal keys [| 3; 1; 2 |] "keys"

            testCase "an instance registers its definition and value"
            <| fun () ->
                use session = Twin.mount counter 4

                let found =
                    probes session
                    |> List.exists (fun p ->
                        match p with
                        | Inspect.InstanceProbe (_, instance, _, _) -> instance :? CounterState
                        | _ -> false)

                Expect.isTrue found "instance probe"

            testCase "a mount registers its phase, commit mode and identity prefix"
            <| fun () ->
                use session = Twin.mount counter 4

                let mount =
                    probes session
                    |> List.pick (fun p ->
                        match p with
                        | Inspect.MountProbe (phase, queued, flushing, _) -> Some (phase (), queued, flushing ())
                        | _ -> None)

                Expect.equal mount (Attached, false, false) "an attached synchronous mount"

            testCase "a disposed mount leaves the registry"
            <| fun () ->
                let session = Twin.mount counter 4
                let root = Bridge.nodeOf session.Host
                let first = root.FirstChild
                (session :> System.IDisposable).Dispose()
                Expect.isNone (Inspect.probeOf (unbox first.Native)) "removed"
        ]
