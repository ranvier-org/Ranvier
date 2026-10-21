module Ranvier.Analyzers.Tests.Ran002

open Expecto
open Ranvier.Analyzers
open Ranvier.Analyzers.Tests.Options

let private findings (source: string) =
    run Analyzers.ran002Cli (context (ranvier.Force ()) source)

let private policy =
    """
module Fixture
type Policy =
    | Queue
    | Drop of int
    member this.IsUserDefined = true
"""

[<Tests>]
let tests =
    testList
        "RAN002"
        [
            test "a tester of a case without fields, with its exact message" {
                let messages =
                    findings (
                        policy
                        + "let isQueue (policy: Policy) = policy.IsQueue\n"
                    )

                Expect.equal
                    (messages |> List.map _.Message)
                    [
                        "`policy.IsQueue` calls a compiler-generated union tester, which a Fable precompiled assembly does not declare. Write `(match policy with Policy.Queue -> true | _ -> false)`."
                    ]
                    "one message"
            }

            test "a tester of a case with fields" {
                let messages =
                    findings (
                        policy
                        + "let isDrop (policy: Policy) = policy.IsDrop\n"
                    )

                Expect.equal (messages |> List.map _.Code) [ "RAN002" ] "one finding"
                Expect.stringContains messages.Head.Message "Policy.Drop _ -> true" "the field wildcard"
            }

            test "a tester of a struct union" {
                let source =
                    "module Fixture\n[<Struct>]\ntype Flag =\n    | On\n    | Off\nlet on (flag: Flag) = flag.IsOn\n"

                Expect.equal (findings source |> List.map _.Code) [ "RAN002" ] "one finding"
            }

            test "a tester of a generic union" {
                let source =
                    "module Fixture\ntype Outcome<'T> =\n    | Done of 'T\n    | Failed of exn\nlet finished (o: Outcome<int>) = o.IsDone\n"

                Expect.equal (findings source |> List.map _.Code) [ "RAN002" ] "one finding"
            }

            test "a tester of an internal union" {
                let source =
                    "module Fixture\ntype internal Mode =\n    | Fast\n    | Slow\nlet internal fast (mode: Mode) = mode.IsFast\n"

                Expect.equal (findings source |> List.map _.Code) [ "RAN002" ] "one finding"
            }

            test "the regression seed of 2cf84be fires" {
                let source =
                    """
module Fixture
type FlightPolicy =
    | CancelPrevious
    | FinishCurrent
let trace (graph: obj, id: int, gen: int, owed: bool, finish: bool) = ()
let drop (policy: FlightPolicy) graph id gen owed = trace (graph, id, gen, owed, policy.IsFinishCurrent)
"""

                Expect.equal (findings source |> List.map _.Code) [ "RAN002" ] "Core.fs:4545 before 2cf84be"
            }

            test "a user-written IsX member is not a generated tester" {
                Expect.isEmpty
                    (findings (
                        policy
                        + "let custom (policy: Policy) = policy.IsUserDefined\n"
                    ))
                    "no finding"
            }

            test "Option.IsSome is not a generated tester" { Expect.isEmpty (findings "module Fixture\nlet some = (Some 1).IsSome\n") "no finding" }

            test "a tester of a union from a referenced assembly is out of scope" {
                Expect.isEmpty (findings "module Fixture\nlet queued (policy: Ranvier.FlightPolicy) = policy.IsQueue\n") "Ranvier's FlightPolicy"
            }

            test "a project without Fable.Core is not analyzed" {
                let source =
                    policy
                    + "let isQueue (policy: Policy) = policy.IsQueue\n"

                Expect.isEmpty (run Analyzers.ran002Cli (context (plain.Force ()) source)) "no Fable.Core reference"
            }

            test "the fix type-checks and clears the finding" {
                let source =
                    policy
                    + "let isDrop (policy: Policy) = policy.IsDrop\n"

                let fix = (findings source).Head.Fixes |> List.exactlyOne
                let repaired = applyFix source fix
                Expect.stringContains repaired "(match policy with Policy.Drop _ -> true | _ -> false)" "the match"
                Expect.isEmpty (findings repaired) "no finding after the fix"
            }
        ]
