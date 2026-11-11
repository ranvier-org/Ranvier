module Ranvier.Tests.Invalidation

open Expecto
open Ranvier

/// <summary>
/// A computation is marked clean before its body runs, so an invalidation
/// raised during the run survives it. Clearing the flag afterwards instead
/// discards that invalidation silently: the node goes on serving a value the
/// graph has already moved past, and nothing ever re-runs it.
/// </summary>
[<Tests>]
let tests =
    testList
        "Invalidation"
        [
            test "a memo sees a write its own body made" {
                let g = new Graph ()
                let s = Signal (g, 1)

                let c =
                    Make.Memo (
                        g,
                        fun _ ->
                            let v = s.Value

                            if v < 3 then
                                s.Value <- v + 1

                            v
                    )

                // A memo that writes is outside the contract, but it is the
                // cheapest way to put a write in the middle of a run. The same
                // shape arises legitimately from a source that settles inline.
                c.TryValue |> ignore
                Expect.equal s.Peek 2 "precondition: the body wrote"
                Expect.equal c.TryValue (Ready 2) "the next read must re-run against the new value"
                Expect.equal c.TryValue (Ready 3) "and converge once the body stops writing"
                Expect.equal c.Runs 3 "one run per invalidation, no more"
            }

            test "a boundary sees a write its own body made" {
                let g = new Graph ()
                let s = Signal (g, 1)

                let b =
                    Boundary<int>
                        .Suspense(
                            g,
                            (fun () ->
                                let v = s.Value

                                if v < 3 then
                                    s.Value <- v + 1

                                v),
                            fun _ -> 0
                        )

                b.TryValue |> ignore
                Expect.equal b.TryValue (Ready 2) "catching a channel does not change invalidation"
                Expect.equal b.TryValue (Ready 3) "and it converges the same way"
            }

            test "a memo that reads itself yields a default rather than overflowing" {
                let g = new Graph ()
                let mutable self: Memo<int> option = None

                let c =
                    Make.Memo (
                        g,
                        fun _ ->
                            match self with
                            | Some (m: Memo<int>) -> m.Peek + 1
                            | None -> 0
                    )

                self <- Some c

                // Not a supported thing to write, but a cycle must fail as a
                // value, not as a stack overflow the caller cannot catch.
                Expect.equal c.TryValue (Ready 1) "one level, then the cached default"
            }

            test "a running memo that marks and reads itself does not start a nested recomputation" {
                let g = new Graph ()
                let flip = Signal (g, 1)
                let a = Signal (g, 10)
                let b = Signal (g, 20)
                let mutable self: Memo<int> option = None

                let m =
                    Make.Memo (
                        g,
                        fun _ ->
                            let f = flip.Value

                            if f = 1 then
                                a.Value
                            elif f = 2 then
                                let v = a.Value
                                flip.Value <- 3

                                match self with
                                | Some s -> s.Value |> ignore
                                | None -> ()

                                v
                            else
                                b.Value
                    )

                self <- Some m
                let seen = ResizeArray ()
                use _e = new Effect (g, (fun () -> seen.Add m.Value))

                flip.Value <- 2
                Expect.equal (Seq.toList seen) [ 10; 20 ] "the effect observes the converged value"
                Expect.equal m.Value 20 "the memo holds the converged branch's value"
                Expect.equal a.ObserverCount 0 "the old branch's edge is dropped"
                Expect.equal b.ObserverCount 1 "the new branch's edge is kept"
            }

            test "a memo in Check with a self-edge resolves without overflowing" {
                let g = new Graph ()
                let s = Signal (g, 0)
                let a = Make.Memo (g, (fun _ -> s.Value))
                let mutable self: Memo<int> option = None

                let m =
                    Make.Memo (
                        g,
                        fun _ ->
                            match self with
                            | Some x -> x.Value |> ignore
                            | None -> ()

                            a.Value
                    )

                self <- Some m
                Expect.equal m.Value 0 "first read"
                Expect.equal m.Value 0 "second read links the self-edge"
                s.Value <- 1
                Expect.equal m.Value 1 "resolves Check and recomputes without overflowing"
            }

            test "two memos in Check with mutual edges resolve without overflowing" {
                let g = new Graph ()
                let s = Signal (g, 0)
                let a = Make.Memo (g, (fun _ -> s.Value))
                let mutable other: Memo<int> option = None

                let x =
                    Make.Memo (
                        g,
                        fun _ ->
                            match other with
                            | Some y -> y.Value |> ignore
                            | None -> ()

                            a.Value
                    )

                let y = Make.Memo (g, (fun _ -> x.Value))
                other <- Some y

                for _ in 1..2 do
                    x.Value |> ignore
                    y.Value |> ignore

                s.Value <- 1
                Expect.equal x.Value 1 "first memo resolves the check cycle and recomputes"
                Expect.equal y.Value 1 "second memo answers the updated value"
            }

            test "an unrelated write still invalidates normally" {
                let g = new Graph ()
                let s = Signal (g, 1)
                let c = Make.Memo (g, (fun _ -> s.Value * 2))

                Expect.equal c.TryValue (Ready 2) "first"
                Expect.equal c.Runs 1 "cached"
                s.Value <- 5
                Expect.equal c.TryValue (Ready 10) "invalidated"
                Expect.equal c.Runs 2 "exactly one re-run"
            }
        ]
