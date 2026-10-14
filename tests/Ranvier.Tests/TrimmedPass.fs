module Ranvier.Tests.TrimmedPass

open System
open Expecto
open Ranvier

[<Tests>]
let tests =
    testList
        "Trimmed pass"
        [
            test "list, lazy seq and array sources give the same keys and rows" {
                use g = new Graph ()
                use _ = g.Activate ()
                let items = createSignal [ 3; 1; 2 ]
                let fromList = createProjection id (fun x -> x * 10) (fun () -> items.Value)

                let fromSeq =
                    createProjection id (fun x -> x * 10) (fun () -> seq { for x in items.Value -> x })

                let fromArray =
                    createProjection id (fun x -> x * 10) (fun () -> List.toArray items.Value :> seq<_>)

                let read (p: Projection<int, int>) =
                    p.Keys |> Array.map (fun k -> k, p.Get k)

                for next in [ [ 1; 2; 3 ]; [ 2 ]; []; [ 4; 2; 9 ] ] do
                    items.Value <- next
                    Expect.equal (read fromSeq) (read fromList) $"seq after {next}"
                    Expect.equal (read fromArray) (read fromList) $"array after {next}"
            }

            test "an array mutated in place and republished is compared against the previous pass's copy" {
                use g = new Graph ()
                use _ = g.Activate ()
                let array = [| "a"; "b"; "c" |]
                let tick = createSignal 0

                let proj =
                    createProjection id id (fun () ->
                        tick.Value |> ignore
                        array :> seq<_>)

                Expect.sequenceEqual proj.Keys [ "a"; "b"; "c" ] "first pass"
                tick.Value <- 1
                Expect.sequenceEqual proj.Keys [ "a"; "b"; "c" ] "an unchanged republish"
                array[1] <- "x"
                tick.Value <- 2
                Expect.sequenceEqual proj.Keys [ "a"; "x"; "c" ] "the in-place change is seen"
            }

            test "an int array source reads by index" {
                use g = new Graph ()
                use _ = g.Activate ()
                let items = createSignal [| 1; 2; 3; 4 |]
                let proj = createProjection id (fun x -> x + 100) (fun () -> items.Value :> seq<_>)
                Expect.sequenceEqual proj.Keys [ 1; 2; 3; 4 ] "first pass"
                items.Value <- [| 1; 3; 2; 4 |]
                Expect.sequenceEqual proj.Keys [ 1; 3; 2; 4 ] "swapped"
                items.Value <- [| 3; 1 |]
                Expect.sequenceEqual proj.Keys [ 3; 1 ] "shrunk"
                Expect.equal (proj.Get 1) 101 "row"
            }

            test "a lazy source is enumerated to completion before keyOf runs" {
                use g = new Graph ()
                use _ = g.Activate ()
                let log = ResizeArray<string>()

                let proj =
                    createProjection
                        (fun x ->
                            log.Add ("key " + string x)
                            x)
                        id
                        (fun () ->
                            seq {
                                for x in [ 1; 2 ] do
                                    log.Add ("item " + string x)
                                    yield x
                            })

                proj.Keys |> ignore
                Expect.sequenceEqual log [ "item 1"; "item 2"; "key 1"; "key 2" ] "source first, then keyOf"
            }

            test "a nested pass failing in its source leaves the outer pass's items intact" {
                use g = new Graph ()
                use _ = g.Activate ()
                let tick = createSignal 0
                let mutable failing = false
                let mutable fired = false
                let mutable proj: Projection<int, int> = Unchecked.defaultof<_>

                proj <-
                    createProjection
                        (fun x ->
                            if x = 2 && not fired then
                                fired <- true
                                failing <- true
                                tick.Value <- 1

                                try
                                    proj.Keys |> ignore
                                with _ ->
                                    ()

                                failing <- false

                            x)
                        (fun x -> x * 10)
                        (fun () ->
                            tick.Value |> ignore

                            if failing then
                                failwith "source failed"

                            [| 1; 2; 3; 4 |] :> seq<_>)

                // The nested pass restarts the pass's keys, so the outer pass publishes the items it visits after it.
                let keys = proj.Keys
                Expect.sequenceEqual keys [ 2; 3; 4 ] "the outer pass's keys"
                Expect.equal (keys |> Array.map proj.Get) [| 20; 30; 40 |] "the outer pass's rows"
            }
        ]
