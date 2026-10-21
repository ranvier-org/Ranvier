module Ranvier.Analyzers.Tests.Ran025

open Expecto
open Ranvier.Analyzers
open Ranvier.Analyzers.Tests.Options

let private header =
    "module Fixture\nopen Ranvier\nopen Ranvier.View.Foundation\nopen Ranvier.View.Foundation.Scheduling\nopen Ranvier.View.Foundation.Hydration\nlet hydrated graph host definition (failure: CommitFailure) (post: (unit -> unit) -> unit) =\n"

let private findings (body: string) =
    run Analyzers.ran025Cli (context (ranvier.Force ()) (header + body))

let private codes body =
    findings body |> List.map _.Code

let private call (mode: string) =
    $"    hydrateWith ({mode}) graph None ignore HydrationOptions.defaults host definition ()\n"

[<Tests>]
let tests =
    testList
        "RAN025"
        [
            test "a QueuedCommits construction, with its exact message" {
                Expect.equal
                    (findings (call "QueuedCommits (failure, post)")
                     |> List.map _.Message)
                    [
                        "Hydration supports synchronous commits only, so this call raises. Pass `SynchronousCommits`, or call `hydrate`."
                    ]
                    "one message"
            }

            test "the finding's range is the mode argument" {
                let body = call "QueuedCommits (failure, post)"
                let range = (findings body).Head.Range
                Expect.equal (textAt (header + body) range) "QueuedCommits (failure, post)" "the mode"
            }

            test "Microtask.mode" { Expect.equal (codes (call "Microtask.mode failure")) [ "RAN025" ] "Microtask.fs:16" }

            test "a let-bound queued mode" {
                Expect.equal
                    (codes (
                        "    let mode = QueuedCommits (failure, post)\n"
                        + call "mode"
                    ))
                    [ "RAN025" ]
                    "let-bound"
            }

            test "SynchronousCommits" { Expect.isEmpty (codes (call "SynchronousCommits")) "supported" }

            test "a mode parameter" {
                let body =
                    "    ()\nlet other (mode: CommitMode) graph host definition =\n    hydrateWith mode graph None ignore HydrationOptions.defaults host definition ()\n"

                Expect.isEmpty (codes body) "unknown mode"
            }

            test "hydrate" {
                Expect.isEmpty (codes "    hydrate graph None ignore HydrationOptions.defaults host definition ()\n") "synchronous by definition"
            }
        ]
