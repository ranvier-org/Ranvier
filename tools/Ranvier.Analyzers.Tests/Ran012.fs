module Ranvier.Analyzers.Tests.Ran012

open Expecto
open Ranvier.Analyzers
open Ranvier.Analyzers.Tests.Options

let private findings (body: string) =
    let source =
        "module Fixture\nopen Ranvier\nopen System.Threading.Tasks\n"
        + body

    run Analyzers.ran012Cli (context (ranvier.Force ()) source)

let private codes body =
    findings body |> List.map _.Code

let private asyncMemo (inner: string) =
    "let load (userId: Signal<int>) (previous: Task<int>) =\n    createAsync (fun _ _ ->\n        task {\n"
    + inner
    + "        })\n"

[<Tests>]
let tests =
    testList
        "RAN012"
        [
            test "a read after do! Task.Yield, with its exact message" {
                Expect.equal
                    (findings (asyncMemo "            do! Task.Yield ()\n            return userId.Value\n")
                     |> List.map _.Message)
                    [
                        "`userId.Value` is read after the first `let!`/`do!` of this async memo; a read after an await that suspends is untracked, and a change to `userId` starts no flight. Read it before the first await and bind it to a local."
                    ]
                    "one message"
            }

            test "a read after let!" {
                Expect.equal (codes (asyncMemo "            let! _ = previous\n            return userId.Value\n")) [ "RAN012" ] "let!"
            }

            test "a read in an and! continuation" {
                Expect.equal
                    (codes (asyncMemo "            let! a = previous\n            and! b = previous\n            return a + b + userId.Value\n"))
                    [ "RAN012" ]
                    "and!"
            }

            test "a read after the await of createAsyncWithPolicy" {
                let body =
                    "let load (userId: Signal<int>) =\n    createAsyncWithPolicy FlightPolicy.KeepLatest (fun _ _ ->\n        task {\n            do! Task.Yield ()\n            return userId.Value\n        })\n"

                Expect.equal (codes body) [ "RAN012" ] "policy form"
            }

            test "a read before the first await" {
                Expect.isEmpty
                    (codes (asyncMemo "            let id = userId.Value\n            do! Task.Yield ()\n            return id\n"))
                    "tracked"
            }

            test "Peek after the await" {
                Expect.isEmpty (codes (asyncMemo "            do! Task.Yield ()\n            return userId.Peek\n")) "untracked by design"
            }

            test "an await outside an async memo" {
                Expect.isEmpty
                    (codes "let t (s: Signal<int>) =\n    task {\n        do! Task.Yield ()\n        return s.Value\n    }\n")
                    "no async memo"
            }

            test "a read in the body of an effect created after the await belongs to the effect" {
                Expect.isEmpty
                    (codes (
                        asyncMemo
                            "            do! Task.Yield ()
            createEffect (fun () -> userId.Value |> ignore)
            return 1
"
                    ))
                    "the effect tracks its own reads"
            }

            test "a read under untrack after the await" {
                Expect.isEmpty
                    (codes (
                        asyncMemo
                            "            do! Task.Yield ()
            return untrack (fun () -> userId.Value)
"
                    ))
                    "untracked by intent"
            }

            test "a user Value property after an await is not a reactive read" {
                let body =
                    "type Box() =\n    member _.Value = 1\nlet load (box: Box) =\n    createAsync (fun _ _ ->\n        task {\n            do! Task.Yield ()\n            return box.Value\n        })\n"

                Expect.isEmpty (codes body) "user type"
            }
        ]
