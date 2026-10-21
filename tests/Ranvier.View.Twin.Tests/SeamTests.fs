module Ranvier.View.Twin.Tests.SeamTests

open Expecto
open Ranvier.View.Foundation

[<Tests>]
let tests =
    testList
        "Departures seam"
        [
            testCase "the seam is an object on .NET whose flags start off"
            <| fun () ->
                let seam = Departures.seam
                Expect.isNotNull (box seam) "seam"
                Expect.isFalse seam.Enabled "exits off"
                Expect.isFalse seam.ReflowEnabled "reflows off"

            testCase "the seam's operations are settable"
            <| fun () ->
                let seam = Departures.seam
                let previous = seam.NextLive
                seam.NextLive <- System.Func<_, _> id
                Expect.isNotNull (box seam.NextLive) "set"
                seam.NextLive <- previous
        ]
