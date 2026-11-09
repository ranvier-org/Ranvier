module Ranvier.Tests.NotifySkip

open Expecto
open Ranvier

/// <summary>Whether <c>s</c>'s stamp is the graph's current epoch, so its next write skips the walk.</summary>
let private stamped (s: Signal<'T>) = s.Stamped

#if RANVIER_TRACE
/// <summary>The marks made by walks of <c>source</c> after trace sequence <c>since</c>.</summary>
let private marksFrom (g: Graph) (source: INode) (since: int) =
    Trace.eventsSince g since
    |> Array.filter (fun e ->
        e.Kind = TraceEventKind.Mark
        && e.Other = source.Id)
    |> Array.length
#endif

/// <summary>
/// A signal write skips its notify walk while every observer of the signal is saturated: a memo <c>Dirty</c> with no
/// announcement due, or an effect <c>Dirty</c> and queued, in cleanup or disposed. <c>proofs/theories/Pure/Optimised.v</c>
/// proves the skip changes no event (<c>optimised_trace_eq</c>).
/// </summary>
/// <remarks>
/// The regression cases are the conformance scenarios of <c>proofs/conformance/Scenarios.fsx</c> whose traces depend on
/// the epoch's advance points. Their logs are the present engine's, and the skip must reproduce them.
/// </remarks>
[<Tests>]
let tests =
    testList
        "NotifySkip"
        [
            test "a write to a signal whose 64 memo readers are saturated marks none of them" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let memos = Array.init 64 (fun _ -> createMemo (fun _ -> s.Value + 1))

                for m in memos do
                    m.Value |> ignore

                s.Value <- 1
#if RANVIER_TRACE
                let before = Trace.lastSeq g
#endif
                s.Value <- 2
#if RANVIER_TRACE
                Expect.equal (marksFrom g s before) 0 "the second write marks no reader"
#endif
                Expect.isTrue (stamped s) "the walk over saturated readers records the stamp"
                Expect.isTrue (memos |> Array.forall (fun m -> m.Value = 3)) "every memo reads the latest value"
                Expect.isFalse (stamped s) "the recomputes advanced the epoch"
                s.Value <- 3
#if RANVIER_TRACE
                let before = Trace.lastSeq g
#endif
                s.Value <- 4
#if RANVIER_TRACE
                Expect.equal (marksFrom g s before) 0 "a walk over the dirty memos records again"
#endif
                Expect.isTrue (memos |> Array.forall (fun m -> m.Value = 5)) "every memo reads the latest value"
            }

            test "in a batch of ten writes the nine later writes perform no walk" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let seen = ResizeArray<int>()

                for _ in 1..8 do
                    createEffect (fun () -> seen.Add s.Value)
                    |> ignore

                seen.Clear ()
#if RANVIER_TRACE
                let before = Trace.lastSeq g
#endif
                batch (fun () ->
                    for i in 1..10 do
                        s.Value <- i)
#if RANVIER_TRACE
                Expect.equal (marksFrom g s before) 8 "only the first write marks the eight effects"
#endif
                Expect.equal (List.ofSeq seen) (List.replicate 8 10) "each effect runs once, with the last value"
            }

            test "a signal read by a binding walks on every write" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let log = ResizeArray<int>()
                let m = createMemo (fun _ -> s.Value)
                m.Value |> ignore
                bindSignal s log (fun t v -> t.Add v)
#if RANVIER_TRACE
                let before = Trace.lastSeq g
#endif
                batch (fun () ->
                    s.Value <- 1
                    s.Value <- 2
                    s.Value <- 3)
#if RANVIER_TRACE
                Expect.equal (marksFrom g s before) 6 "each write marks the memo and the binding"
#endif
                Expect.isFalse (stamped s) "a walk over a binding records nothing"
                Expect.equal (List.ofSeq log) [ 0; 3 ] "the binding writes the latest value"
                Expect.equal m.Value 3 "the memo reads the latest value"
            }

            test "a new effect reading a stamped signal clears the stamp, and runs on the next write" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let m = createMemo (fun _ -> s.Value)
                m.Value |> ignore
                s.Value <- 1
                Expect.isTrue (stamped s) "the walk over the dirty memo records the stamp"
                let seen = ResizeArray<int>()

                createEffect (fun () -> seen.Add s.Value)
                |> ignore

                Expect.isFalse (stamped s) "the effect's edge clears the stamp"
                s.Value <- 2
                Expect.equal (List.ofSeq seen) [ 1; 2 ] "the effect runs on the write after its creation"
            }

            test "S03, #302: each even write reaches the effect through a stale middle" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let h = createSignal 0
                let k = createSignal 0
                let a = createMemo (fun _ -> s.Value)

                let m =
                    createMemo (fun _ ->
                        k.Value |> ignore
                        a.Value)

                let xs = ResizeArray<int>()
                let es = ResizeArray<int>()

                let x =
                    createMemo (fun _ ->
                        h.Value |> ignore
                        m.Value |> ignore
                        let v = a.Value
                        xs.Add v
                        v / 2)

                createEffect (fun () -> es.Add x.Value) |> ignore

                for i in 1..4 do
                    batch (fun () ->
                        h.Value <- i
                        k.Value <- i
                        s.Value <- i)

                Expect.equal (List.ofSeq xs) [ 0; 1; 2; 2; 3; 4; 4 ] "x's runs"
                Expect.equal (List.ofSeq es) [ 0; 1; 2 ] "the effect runs on the even writes"
            }

            test "S08: an effect's cleanup writes its own source and queues no run" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let log = ResizeArray<int>()

                createEffect (fun () ->
                    let v = s.Value
                    log.Add v
                    onCleanup (fun () -> s.Value <- v + 10))
                |> ignore

                s.Value <- 1
                s.Value <- 11
                Expect.equal (List.ofSeq log) [ 0; 10; 20 ] "the body reads each cleanup's write"
                Expect.equal s.Value 20 "the last cleanup's write"
            }

            test "S11: an owning memo's cleanup writes its source and reads the memo" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let self: Memo<int> ref = ref Unchecked.defaultof<Memo<int>>
                let cleanups = ResizeArray<int>()
                let es = ResizeArray<int>()

                let m =
                    createMemoWith (fun _ ->
                        let v = s.Value

                        onCleanup (fun () ->
                            if v = 1 then
                                s.Value <- 2

                            cleanups.Add self.Value.Value)

                        v)

                self.Value <- m
                createEffect (fun () -> es.Add m.Value) |> ignore
                s.Value <- 1
                s.Value <- 5
                s.Value <- 6
                Expect.equal (List.ofSeq cleanups) [ 0; 2; 2 ] "each cleanup reads the memo"
                Expect.equal (List.ofSeq es) [ 0; 1; 2; 6 ] "the effect's runs"
            }

            test "S13: an effect writing its source climbs to a fixpoint" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let log = ResizeArray<int>()

                createEffect (fun () ->
                    let v = s.Value
                    log.Add v

                    if v < 3 then
                        s.Value <- v + 1)
                |> ignore

                Expect.equal (List.ofSeq log) [ 0; 1; 2; 3 ] "one run per value"
            }

            test "S14: a memo writing its own source ends its runs stale" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let ms = ResizeArray<int>()
                let es = ResizeArray<int>()

                let m =
                    createMemo (fun _ ->
                        let v = s.Value
                        ms.Add v

                        if v < 3 then
                            s.Value <- v + 1

                        0)

                createEffect (fun () -> es.Add m.Value) |> ignore
                Expect.equal s.Peek 2 "the first flush leaves the signal at 2"
                Expect.equal m.Value 0 "the host read"
                Expect.equal s.Value 3 "the host read's run writes 3"
                Expect.equal (List.ofSeq ms) [ 0; 1; 2; 3 ] "the memo's runs"
                Expect.equal (List.ofSeq es) [ 0 ] "the memo's value never moves"
            }

            test "S16: an effect's cleanup rewrites its source between two host writes" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let log = ResizeArray<int>()

                createEffect (fun () ->
                    onCleanup (fun () -> s.Value <- 100)
                    log.Add s.Value)
                |> ignore

                s.Value <- 1
                s.Value <- 2
                Expect.equal (List.ofSeq log) [ 0; 100; 100 ] "the effect runs on both host writes"
            }

            test "S17: a self-writing memo's announcement reaches its reader after a later write" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let ms = ResizeArray<int>()

                let m =
                    createMemo (fun _ ->
                        let v = s.Value
                        ms.Add v

                        if v < 1 then
                            s.Value <- v + 1
                            0
                        else
                            v / 10)

                let n = createMemo (fun _ -> m.Value)
                let reads = ResizeArray<int>()
                reads.Add n.Value
                reads.Add n.Value
                s.Value <- 0
                reads.Add n.Value
                s.Value <- 20
                reads.Add n.Value
                Expect.equal (List.ofSeq reads) [ 0; 0; 0; 2 ] "the reader sees the last write"
                Expect.equal (List.ofSeq ms) [ 0; 1; 0; 20 ] "the memo's runs"
            }
        ]
