module Ranvier.View.Twin.Tests.MembersTests

open System
open System.IO
open Expecto
open Ranvier.View.Twin

let private recorded () =
    Path.Combine (TwinHarness.repository (), "experiments", "Ranvier.View.Twin", "members.txt")

[<Tests>]
let tests =
    testSequenced
    <| testList
        "Bridge coverage"
        [
            testCase "the served members equal members.txt"
            <| fun () ->
                let served =
                    Bridge.served ()
                    |> List.filter (fun m -> not (m.StartsWith "Test"))

                let path = recorded ()

                if Environment.GetEnvironmentVariable "RANVIER_UPDATE_MEMBERS" = "1" then
                    File.WriteAllLines (path, served)
                else
                    let expected = File.ReadAllLines path |> List.ofArray
                    let added = served |> List.except expected
                    Expect.isEmpty added $"Members served but missing from members.txt (rerun with RANVIER_UPDATE_MEMBERS=1): {added}"
        ]
