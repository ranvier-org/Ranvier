module Ranvier.View.Twin.Tests.ConformanceTests

open System.IO
open Expecto
open Ranvier.View.Twin
open Ranvier.View.Testing

let output () =
    Path.Combine (TwinHarness.repository (), "examples", "Fable.Ranvier.Playground", "generated-conformance", "twin")

let private runCase (moduleName: string) (name: string) (run: Conformance.Harness -> unit) () =
    let host = TwinHost.Create ()

    try
        run (TwinHarness.create host (Path.Combine (output (), moduleName, TwinHarness.slug name)))
    finally
        (host.Document :> System.IDisposable).Dispose()

[<Tests>]
let tests =
    testSequenced
    <| testList
        "Conformance"
        [
            for moduleName, cases in ConformanceCases.modules do
                testList
                    moduleName
                    [
                        for name, run in cases do
                            if Conformance.skippedOn "twin" name then
                                ptestCase name ignore
                            else
                                testCase name (runCase moduleName name run)
                    ]
        ]
