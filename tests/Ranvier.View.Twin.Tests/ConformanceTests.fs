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

// The artefacts of an earlier run go before the first case writes, so a renamed case leaves none behind.
let private cleared =
    lazy
        (let root = output ()

         if Directory.Exists root then
             Directory.Delete (root, true))

let private runCase (moduleName: string) (name: string) (run: Conformance.Harness -> unit) () =
    cleared.Force ()
    runIn (Path.Combine (output (), moduleName, TwinHarness.slug name)) run

[<Tests>]
let tests =
    testList
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
