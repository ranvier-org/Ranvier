module Ranvier.View.Twin.Tests.ConformanceTests

open System.IO
open Expecto
open Ranvier.View.Twin
open Ranvier.View.Testing

let output () =
    Path.Combine (TwinHarness.repository (), "examples", "Fable.Ranvier.Playground", "generated-conformance", "twin")

/// <summary>Runs a case on a fresh twin host whose artefacts go to <c>directory</c>.</summary>
let runIn (directory: string) (run: Conformance.Harness -> unit) =
    let host = TwinHost.Create ()

    try
        run (TwinHarness.create host directory)
    finally
        (host.Document :> System.IDisposable).Dispose()

let private runCase (moduleName: string) (name: string) (run: Conformance.Harness -> unit) () =
    runIn (Path.Combine (output (), moduleName, TwinHarness.slug name)) run

[<Tests>]
let tests =
    // The check modules keep module-level logs: no two cases, here or in the members test, run at once.
    testSequencedGroup "conformance"
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
