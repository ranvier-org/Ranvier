module Ranvier.View.Twin.Tests.ClockTests

open Expecto
open Ranvier.View.Twin

[<Tests>]
let tests =
    testList
        "Clock"
        [
            testCase "timers run by due time, then insertion, with microtasks after each"
            <| fun () ->
                let clock = TwinClock ()
                let log = ResizeArray<string>()

                clock.SetTimeout (
                    (fun () ->
                        log.Add "b"
                        clock.Post (fun () -> log.Add "b-micro")),
                    10.0
                )
                |> ignore

                clock.SetTimeout ((fun () -> log.Add "a"), 5.0)
                |> ignore

                clock.SetTimeout ((fun () -> log.Add "c"), 10.0)
                |> ignore

                clock.AdvanceBy 10.0
                Expect.equal (List.ofSeq log) [ "a"; "b"; "b-micro"; "c" ] "order"
                Expect.equal clock.Now 10.0 "now"

            testCase "frames run at each FrameMs boundary crossed"
            <| fun () ->
                let clock = TwinClock ()
                let stamps = ResizeArray<float>()

                let rec frame (t: float) =
                    stamps.Add t

                    if stamps.Count < 3 then
                        clock.RequestAnimationFrame frame |> ignore

                clock.RequestAnimationFrame frame |> ignore
                clock.AdvanceBy 40.0
                Expect.equal (List.ofSeq stamps) [ 16.0; 32.0 ] "two boundaries"
                clock.Frame ()
                Expect.equal (List.ofSeq stamps) [ 16.0; 32.0; 40.0 ] "Frame runs at Now"

            testCase "real time never advances the clock"
            <| fun () ->
                let clock = TwinClock ()
                System.Threading.Thread.Sleep 20
                Expect.equal clock.Now 0.0 "unchanged"

            testCase "Settle raises when a queue keeps refilling"
            <| fun () ->
                let clock = TwinClock ()
                use graph = new Ranvier.Graph ()

                let rec again () =
                    clock.Post again

                clock.Post again

                Expect.throwsC (fun () -> clock.Settle (graph, 100)) (fun e ->
                    match e with
                    | TwinRunawayException (queue, _) -> Expect.equal queue "microtasks" "queue"
                    | other -> failtestf "unexpected %O" other)

            testCase "the TimeProvider reads twin time"
            <| fun () ->
                let clock = TwinClock ()
                clock.AdvanceBy 1500.0
                Expect.equal (clock.TimeProvider.GetUtcNow ()) (System.DateTimeOffset.UnixEpoch.AddMilliseconds 1500.0) "now"
        ]
