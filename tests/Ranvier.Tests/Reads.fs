module Ranvier.Tests.Reads

open Expecto
open Ranvier

#if !FABLE_COMPILER
open System.Runtime.CompilerServices

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private failInNamedHelper () : int =
    raise (System.InvalidOperationException "the helper failed")

[<MethodImpl(MethodImplOptions.NoInlining)>]
let private readFailure (read: unit -> int) : exn =
    try
        read () |> ignore
        failtest "the read did not raise"
    with ex ->
        ex
#endif

/// <summary>
/// The three ways to read a node — <c>Value</c>, <c>TryValue</c>, <c>Peek</c> — differ in two
/// independent respects: whether the read records an edge, and whether it is
/// allowed to run the body. Every combination has a caller, and getting one
/// wrong is silent: an untracked read that should have been tracked produces a
/// node that is simply never woken again, and no assertion about its value will
/// notice until the value is stale.
/// </summary>
/// <remarks>
/// <c>Untrack</c> is the same question asked at the scope level, and it nests, so it
/// has to restore rather than clear.
/// </remarks>
[<Tests>]
let tests =
    testList
        "Reads"
        [
            test "Peek on a never-read memo does not run the body" {
                let g = new Graph ()
                let source = Signal (g, 5)
                let m = Make.Memo (g, (fun _ -> source.Value * 2))

                // The honest answer for a memo that has never computed is the
                // default, because there is nothing else to give without
                // running the body — which is the one thing `Peek` promises not
                // to do.
                Expect.equal m.Peek unset "an uninitialised Peek is the default, not the computed value"
                Expect.equal m.Runs 0 "and it stayed uncomputed"

                Expect.equal m.Value 10 "a real read computes it"
                Expect.equal m.Peek 10 "and now Peek has something to report"
            }

            test "Peek does not recompute a stale memo" {
                let g = new Graph ()
                let source = Signal (g, 1)
                let m = Make.Memo (g, (fun _ -> source.Value * 10))

                Expect.equal m.Value 10 "precondition"

                source.Value <- 2
                Expect.equal m.Peek 10 "Peek is the last computed value, staleness included"
                Expect.equal m.Runs 1 "it did not run the body to find out"

                Expect.equal m.Value 20 "and a tracked read brings it current"
            }

            test "Peek records no edge, so the reader is never woken" {
                let g = new Graph ()
                let source = Signal (g, 1)
                let runs = ref 0

                new Effect (
                    g,
                    fun () ->
                        source.Peek |> ignore
                        runs.Value <- runs.Value + 1
                )
                |> ignore

                Expect.equal runs.Value 1 "constructed and run"
                Expect.equal source.ObserverCount 0 "a peek is not a subscription"

                source.Value <- 2
                Expect.equal runs.Value 1 "so the write reaches nobody"
            }

            test "a memo read only through Peek still counts as unread" {
                let g = new Graph ()
                let source = Signal (g, 1)
                let m = Make.Memo (g, (fun _ -> source.Value * 2))

                m.Peek |> ignore
                Expect.equal source.ObserverCount 0 "peeking a memo does not make it read its own sources"
            }

            test "Untrack nests, and the outer tracking comes back" {
                let g = new Graph ()
                let tracked = Signal (g, 1)
                let hidden = Signal (g, 10)
                let alsoTracked = Signal (g, 100)

                let m =
                    Make.Memo (
                        g,
                        fun _ ->
                            let a = tracked.Value

                            let b =
                                g.Untrack (fun () ->
                                    // A nested untrack must not, on its way
                                    // out, turn tracking back *on* inside the
                                    // untracked region it was nested in.
                                    let inner = g.Untrack (fun () -> hidden.Value)
                                    inner + hidden.Value)

                            a + b + alsoTracked.Value
                    )

                Expect.equal m.Value 121 "1 + 20 + 100"
                Expect.equal m.SourceCount 2 "only the two tracked reads recorded edges"
                Expect.equal hidden.ObserverCount 0 "and the hidden one has no observers"

                hidden.Value <- 20
                Expect.equal m.Value 121 "a write to the untracked source wakes nobody"

                alsoTracked.Value <- 200
                // 1 + (20 + 20) + 200. Untracked does not mean cached: the
                // next run reads the current value of `hidden`, it just never
                // learns that it changed. That is the trap in untracking — the
                // stale read is not stale *forever*, it updates at whatever
                // moment something else happens to wake the node.
                Expect.equal m.Value 241 "the read after the untracked region was still tracked"
            }

            test "Untrack outside any computation is just a read" {
                let g = new Graph ()
                let s = Signal (g, 7)
                Expect.equal (g.Untrack (fun () -> s.Value)) 7 "the value comes back"
                Expect.equal s.ObserverCount 0 "and there was nobody to record an edge for anyway"
            }

            test "an untracked read of a stale memo still recomputes it" {
                let g = new Graph ()
                let source = Signal (g, 1)
                let m = Make.Memo (g, (fun _ -> source.Value * 10))

                Expect.equal m.Value 10 "precondition"
                source.Value <- 2

                // Untracked is about the *edge*, not about freshness. The memo
                // still owes its reader a current value; it just does not
                // remember who asked.
                Expect.equal (g.Untrack (fun () -> m.Value)) 20 "the value is current"
                Expect.equal m.Runs 2 "because the body ran"
            }

            test "TryValue records an edge exactly as Value does" {
                let g = new Graph ()
                let a = AsyncSource<int> g
                let runs = ref 0

                new Effect (
                    g,
                    fun () ->
                        a.TryValue |> ignore
                        runs.Value <- runs.Value + 1
                )
                |> ignore

                Expect.equal runs.Value 1 "ran, without suspending"

                a.Settle 1
                Expect.equal runs.Value 2 "and the non-throwing read still got it woken"
            }

            test "TryValue on a pending source reports rather than throws" {
                let g = new Graph ()
                let a = AsyncSource<int> g

                Expect.equal a.TryValue Pending "the whole point of the non-throwing read"

                a.Settle 3
                Expect.equal a.TryValue (Ready 3) "and it reports the settle the same way"
            }

            test "a fresh memo is Uninitialized until something reads it" {
                let g = new Graph ()
                let source = Signal (g, 1)
                let m = Make.Memo (g, (fun _ -> source.Value))

                Expect.isTrue (m.Status.HasFlag Status.Uninitialized) "nothing has computed yet"

                m.Value |> ignore
                Expect.isFalse (m.Status.HasFlag Status.Uninitialized) "and the first read clears it"
            }

            test "reading a memo twice in one body is one edge and one computation" {
                let g = new Graph ()
                let source = Signal (g, 2)
                let m = Make.Memo (g, (fun _ -> source.Value * 2))
                let outer = Make.Memo (g, (fun _ -> m.Value + m.Value))

                Expect.equal outer.Value 8 "precondition"
                Expect.equal m.Runs 1 "the second read hit the cache"
                Expect.equal m.ObserverCount 1 "and recorded one edge, not two"
            }

            test "TrackStatus tracks a pending source for a reader of any value type" {
                let g = new Graph ()
                let text = AsyncSource<string> g
                let count = AsyncSource<int> g
                let sources: INode[] = [| text; count |]

                let anyPending =
                    Make.Memo (
                        g,
                        fun _ ->
                            sources
                            |> Array.exists (fun node ->
                                g.TrackStatus node &&& Status.Pending
                                <> Status.None)
                    )

                Expect.isTrue anyPending.Value "both sources start pending"

                text.Settle "ready"
                Expect.isTrue anyPending.Value "one source is still pending"

                count.Settle 1
                Expect.isFalse anyPending.Value "the reader re-ran when the last pending source settled"
            }

            test "TrackStatus brings a stale memo up to date before reporting its status" {
                let g = new Graph ()
                let upstream = AsyncSource<int> g
                let gate = Signal (g, false)
                let m = Make.Memo (g, (fun _ -> if gate.Value then upstream.Value else 0))
                let failing = Make.Memo (g, (fun _ -> if gate.Value then failwith "boom" else 0))

                Expect.equal (g.TrackStatus m) Status.None "a settled memo"

                gate.Value <- true
                Expect.equal (g.TrackStatus m) Status.Pending "the memo recomputed and now reads a pending source"
                Expect.equal (g.TrackStatus failing) Status.Error "a failed memo reports its error without raising"
            }

            test "TrackStatus of an effect is untracked" {
                let g = new Graph ()
                let s = Signal (g, 1)
                let e = new Effect (g, (fun () -> s.Value |> ignore))
                let m = Make.Memo (g, (fun _ -> g.TrackStatus e))

                Expect.equal m.Value Status.None "the effect ran"
                Expect.equal m.SourceCount 0 "the memo read no source"
            }
#if !FABLE_COMPILER

            // .NET only: a JavaScript rethrow keeps the stack of the original `Error`.
            test "a stored failure keeps its throw site across repeated reads" {
                let g = new Graph ()
                let failing = Make.Memo (g, (fun _ -> failInNamedHelper ()))
                let dependent = Make.Memo (g, (fun _ -> failing.Value + 1))

                let read () =
                    dependent.Value

                let first = readFailure read
                let firstTrace = first.StackTrace
                let second = readFailure read

                Expect.isTrue (obj.ReferenceEquals (first, second)) "both reads raise the stored instance"
                Expect.stringContains second.StackTrace (nameof failInNamedHelper) "the trace names the throw site"
                Expect.equal second.StackTrace firstTrace "a repeated read leaves the trace unchanged"
            }

            test "reusing one exception across AsyncSource.Fail calls does not accumulate rethrow frames" {
                let g = new Graph ()
                let shared = exn "load failed"

                let readOnce () =
                    let s = AsyncSource<int> g
                    s.Fail shared
                    let m = Make.Memo (g, (fun _ -> s.Value + 1))
                    (readFailure (fun () -> m.Value)).StackTrace

                let firstTrace = readOnce ()

                for _ in 1..20 do
                    readOnce () |> ignore

                let laterTrace = readOnce ()
                Expect.equal laterTrace firstTrace "each source rethrows from the same baseline capture"
            }

            test "an equal off-thread signal write under ThreadAffinity.Guarded still raises" {
                use g = new Graph ()
                let s = Signal (g, 42)
                let mutable caught: exn option = None

                let t =
                    System.Threading.Thread (fun () ->
                        try
                            // fsharpanalyzer: ignore-line-next RAN013
                            s.Value <- 42
                        with ex ->
                            caught <- Some ex)

                t.Start ()
                t.Join ()

                match caught with
                | Some (:? System.InvalidOperationException) -> ()
                | Some ex -> failtestf "expected InvalidOperationException, got %O" ex
                | None -> failtest "expected equal off-thread write to raise InvalidOperationException"
            }
#endif

            test "cutoff signal writes still record journal entries without waking memos" {
                let intCodec: JournalCodec<int> =
                    {
                        Name = "int"
                        Version = 1
                        Encode = string
                        Decode =
                            fun text ->
                                try
                                    Ok (int text)
                                with ex ->
                                    Error ex.Message
                    }

                let journal = MemoryJournal "cutoff"
                use g = new Graph (GraphOptions.Default.WithJournal journal)
                let s = Signal (g, 10) |> Journal.key "s" intCodec
                let mutable runs = 0

                let m =
                    Make.Memo (
                        g,
                        (fun _ ->
                            runs <- runs + 1
                            s.Value * 2)
                    )

                Expect.equal m.Value 20 "initial read computes"
                Expect.equal runs 1 "initial run count"
                Expect.equal journal.Entries.Length 0 "no write recorded yet"

                s.Value <- 10
                Expect.equal m.Value 20 "equal write keeps cached value"
                Expect.equal runs 1 "equal write does not wake memo"
                Expect.equal journal.Entries.Length 1 "equal write still records a journal entry"

                s.Value <- 15
                Expect.equal m.Value 30 "changed write recomputes memo"
                Expect.equal runs 2 "changed write wakes memo"
                Expect.equal journal.Entries.Length 2 "changed write records second journal entry"

                s.Value <- 15
                Expect.equal m.Value 30 "second equal write keeps cached value"
                Expect.equal runs 2 "second equal write does not wake memo"
                Expect.equal journal.Entries.Length 3 "second equal write records third journal entry"
            }

            test "Memo.Value and TryValue preserve fast and slow path semantics across freshness and status" {
                // 1. Clean + None
                do
                    use g = new Graph ()
                    let s = Signal (g, 3)
                    let mutable runs = 0

                    let m =
                        Make.Memo (
                            g,
                            (fun _ ->
                                runs <- runs + 1
                                s.Value * 2)
                        )

                    Expect.equal m.Value 6 "initial Value computes"
                    let downstreamVal = Make.Memo (g, (fun _ -> m.Value + 1))

                    let downstreamTry =
                        Make.Memo (
                            g,
                            (fun _ ->
                                match m.TryValue with
                                | Ready v -> v + 10
                                | _ -> -1)
                        )

                    Expect.equal downstreamVal.Value 7 "cached fast Value read tracks dependency"
                    Expect.equal downstreamTry.Value 16 "cached fast TryValue read tracks dependency"
                    Expect.equal runs 1 "Clean + None hits cache"
                    Expect.equal m.TryValue (Ready 6) "cached TryValue returns Ready"
                    s.Value <- 5
                    Expect.equal downstreamVal.Value 11 "upstream change wakes Value tracker"
                    Expect.equal downstreamTry.Value 20 "upstream change wakes TryValue tracker"

                // 2. Clean + Error
                do
                    use g = new Graph ()
                    let gate = Signal (g, true)
                    let m = Make.Memo (g, (fun _ -> if gate.Value then failwith "boom" else 42))
                    Expect.throws (fun () -> m.Value |> ignore) "first read raises and stores error"

                    match m.TryValue with
                    | Failed ex -> Expect.stringContains ex.Message "boom" "Clean + Error TryValue returns Failed"
                    | other -> failtestf "expected Failed, got %A" other

                    Expect.throws (fun () -> m.Value |> ignore) "Clean + Error Value re-raises stored failure"

                    let tracker =
                        Make.Memo (
                            g,
                            (fun _ ->
                                match m.TryValue with
                                | Failed _ -> -1
                                | Ready v -> v
                                | Pending -> -2)
                        )

                    Expect.equal tracker.Value -1 "tracker sees Failed while recording dependency"
                    gate.Value <- false
                    Expect.equal tracker.Value 42 "source recovery wakes downstream tracker"

                // 3. Clean + Pending
                do
                    use g = new Graph ()
                    let src = AsyncSource<int> g
                    let m = Make.Memo (g, (fun _ -> src.Value))
                    Expect.throwsT<NotReadyException> (fun () -> m.Value |> ignore) "first read raises NotReadyException"
                    Expect.equal m.TryValue Pending "Clean + Pending TryValue returns Pending"

                    Expect.throwsT<NotReadyException> (fun () -> m.Value |> ignore) "Clean + Pending Value raises NotReadyException"

                    let tracker =
                        Make.Memo (
                            g,
                            (fun _ ->
                                match m.TryValue with
                                | Ready v -> v
                                | _ -> 0)
                        )

                    Expect.equal tracker.Value 0 "tracker sees Pending on initial read"
                    src.Settle 7
                    Expect.equal tracker.Value 7 "settling AsyncSource wakes downstream tracker"

                // 4. Check + None and 5. Dirty + None
                do
                    use g = new Graph ()
                    let s = Signal (g, 2)
                    let m1 = Make.Memo (g, (fun _ -> s.Value / 2))
                    let mutable m2Runs = 0

                    let m2 =
                        Make.Memo (
                            g,
                            (fun _ ->
                                m2Runs <- m2Runs + 1
                                m1.Value + 1)
                        )

                    let mutable m2TryRuns = 0

                    let m2Try =
                        Make.Memo (
                            g,
                            (fun _ ->
                                m2TryRuns <- m2TryRuns + 1

                                match m1.TryValue with
                                | Ready v -> v + 10
                                | _ -> -1)
                        )

                    Expect.equal m2.Value 2 "initial Check chain Value"
                    Expect.equal m2Try.Value 11 "initial Check chain TryValue"
                    s.Value <- 3
                    Expect.equal m2.Value 2 "Check + None Value cutoff avoids rerun"
                    Expect.equal m2Runs 1 "m2 body did not rerun on cutoff"
                    Expect.equal m2Try.TryValue (Ready 11) "Check + None TryValue cutoff avoids rerun"
                    Expect.equal m2TryRuns 1 "m2Try body did not rerun on cutoff"
                    s.Value <- 4
                    Expect.equal m2.Value 3 "Dirty + None Value recomputes on upstream change"
                    Expect.equal m2Runs 2 "m2 body ran once on change"
                    Expect.equal m2Try.TryValue (Ready 12) "Dirty + None TryValue recomputes on upstream change"
                    Expect.equal m2TryRuns 2 "m2Try body ran once on change"

                // 6. Disposed + Dirty + None
                do
                    use g = new Graph ()
                    let s = Signal (g, 1)
                    let mutable runs = 0

                    let m =
                        Make.Memo (
                            g,
                            (fun _ ->
                                runs <- runs + 1
                                s.Value + 1)
                        )

                    Expect.equal m.Value 2 "initial read before dispose"
                    s.Value <- 99
                    m.Dispose ()
                    Expect.equal m.Value 2 "Disposed + Dirty + None Value answers cached value"
                    Expect.equal m.TryValue (Ready 2) "Disposed + Dirty + None TryValue answers cached value"
                    Expect.equal runs 1 "disposed memo did not recompute"
            }
            test "default, custom, NaN, and faulty null-policy comparers preserve signal and memo cutoff semantics" {
                // Default comparer (null-sentinel fast path on .NET): equal int writes cut off; unequal writes update.
                do
                    use g = new Graph ()
                    let s = Signal (g, 1)
                    let mutable m1Runs = 0
                    let mutable m2Runs = 0

                    let m1 =
                        Make.Memo (
                            g,
                            (fun _ ->
                                m1Runs <- m1Runs + 1
                                s.Value / 2)
                        )

                    let m2 =
                        Make.Memo (
                            g,
                            (fun _ ->
                                m2Runs <- m2Runs + 1
                                m1.Value + 10)
                        )

                    Expect.equal m2.Value 10 "initial read computes chain"
                    Expect.equal m1Runs 1 "m1 ran once"
                    Expect.equal m2Runs 1 "m2 ran once"

                    s.Value <- 1
                    Expect.equal m2.Value 10 "equal signal write cuts off"
                    Expect.equal m1Runs 1 "m1 did not rerun on signal cutoff"
                    Expect.equal m2Runs 1 "m2 did not rerun on signal cutoff"

                    s.Value <- 0
                    Expect.equal m2.Value 10 "equal memo output cuts off downstream"
                    Expect.equal m1Runs 2 "m1 reran on changed signal"
                    Expect.equal m2Runs 1 "m2 did not rerun when m1 output stayed 0"

                    s.Value <- 2
                    Expect.equal m2.Value 11 "changed memo output updates downstream"
                    Expect.equal m1Runs 3 "m1 reran on second signal change"
                    Expect.equal m2Runs 2 "m2 reran on m1 change"

                // Custom comparer: case-insensitive equality on signal and memo.
                do
                    use g = new Graph ()

                    let ci =
                        { new System.Collections.Generic.IEqualityComparer<string> with
                            member _.Equals(a, b) =
                                System.String.Equals (a, b, System.StringComparison.OrdinalIgnoreCase)

                            member _.GetHashCode value =
                                if isNull value then
                                    0
                                else
                                    value.ToUpperInvariant().GetHashCode()
                        }

                    g.Run (fun () ->
                        let s = createSignalWithComparer ci "abc"
                        let mutable m1Runs = 0
                        let mutable m2Runs = 0

                        let m1 =
                            createMemoWithComparer ci (fun _ ->
                                m1Runs <- m1Runs + 1
                                s.Value)

                        let m2 =
                            createMemoWithComparer ci (fun _ ->
                                m2Runs <- m2Runs + 1
                                m1.Value.ToLowerInvariant ())

                        Expect.equal m2.Value "abc" "initial custom comparer read"
                        s.Value <- "ABC"
                        Expect.equal m2.Value "abc" "case-insensitive signal cuts off"
                        Expect.equal m1Runs 1 "m1 skipped on custom signal cutoff"
                        s.Value <- "xyz"
                        Expect.equal m2.Value "xyz" "changed custom signal propagates"
                        Expect.equal m1Runs 2 "m1 reran on changed custom signal"
                        Expect.equal m2Runs 2 "m2 reran on changed custom memo")

                // NaN semantics: JsIdentityPolicy propagates NaN over NaN; StructuralPolicy cuts off NaN on .NET.
                do
                    use gJs = new Graph ()
                    let sJs = Signal (gJs, System.Double.NaN)
                    let mutable jsRuns = 0

                    let mJs =
                        Make.Memo (
                            gJs,
                            (fun _ ->
                                jsRuns <- jsRuns + 1
                                sJs.Value)
                        )

                    ignore mJs.Value
                    sJs.Value <- System.Double.NaN
                    ignore mJs.Value
                    Expect.equal jsRuns 2 "JsIdentityPolicy propagates NaN over NaN on signal write"
#if !FABLE_COMPILER
                do
                    use gStruct =
                        new Graph (
                            { GraphOptions.Default with
                                Equality = StructuralPolicy ()
                            }
                        )

                    let sStruct = Signal (gStruct, System.Double.NaN)
                    let mutable structRuns = 0

                    let mStruct =
                        Make.Memo (
                            gStruct,
                            (fun _ ->
                                structRuns <- structRuns + 1
                                sStruct.Value)
                        )

                    ignore mStruct.Value
                    sStruct.Value <- System.Double.NaN
                    ignore mStruct.Value
                    Expect.equal structRuns 1 "StructuralPolicy cuts off NaN over NaN on .NET"

                // Faulty IEqualityPolicy returning literal null must still throw NullReferenceException on cutoff.
                do
                    let nullStringPolicy =
                        { new IEqualityPolicy with
                            member _.Comparer<'T>() =
                                if typeof<'T> = typeof<string> then
                                    Unchecked.defaultof<System.Collections.Generic.IEqualityComparer<'T>>
                                else
                                    System.Collections.Generic.EqualityComparer<'T>.Default
                        }

                    use gFault =
                        new Graph (
                            { GraphOptions.Default with
                                Equality = nullStringPolicy
                            }
                        )

                    let sNull = Signal (gFault, "init")

                    Expect.throwsT<System.NullReferenceException>
                        (fun () -> sNull.Value <- "next")
                        "null policy comparer throws NullReferenceException on signal write"

                    let sTrigger = Signal (gFault, 1)

                    let mNull = Make.Memo (gFault, (fun _ -> if sTrigger.Value = 1 then "a" else "b"))

                    Expect.equal mNull.Value "a" "first memo run publishes without invoking comparer"
                    sTrigger.Value <- 2

                    match mNull.TryValue with
                    | Failed (:? System.NullReferenceException) -> ()
                    | other -> failtest $"expected Failed NullReferenceException on memo cutoff, got %A{other}"

                // Off-thread equal writes under Guarded must throw even on uninitialized or foreign-owner threads.
                do
                    use gGuard = new Graph ()
                    let sGuard = Signal (gGuard, 1)

                    let mutable uninitError: exn option = None

                    let tUninit =
                        System.Threading.Thread (fun () ->
                            try
                                // fsharpanalyzer: ignore-line-next RAN013
                                sGuard.Value <- 1
                            with ex ->
                                uninitError <- Some ex)

                    tUninit.Start ()
                    tUninit.Join ()

                    match uninitError with
                    | Some (:? System.InvalidOperationException) -> ()
                    | other -> failtest $"expected InvalidOperationException on uninitialized off-thread equal write, got %A{other}"

                    let mutable otherError: exn option = None

                    let tOther =
                        System.Threading.Thread (fun () ->
                            use _other = new Graph ()

                            try
                                // fsharpanalyzer: ignore-line-next RAN013
                                sGuard.Value <- 1
                            with ex ->
                                otherError <- Some ex)

                    tOther.Start ()
                    tOther.Join ()

                    match otherError with
                    | Some (:? System.InvalidOperationException) -> ()
                    | other -> failtest $"expected InvalidOperationException on foreign-owner off-thread equal write, got %A{other}"

                // Reentrant writes inside a Serialised graph configured with an IInputJournal must still record writes.
                do
                    let intCodec: JournalCodec<int> =
                        {
                            Name = "int"
                            Version = 1
                            Encode = string
                            Decode =
                                fun text ->
                                    try
                                        Ok (int text)
                                    with ex ->
                                        Error ex.Message
                        }

                    let serJournal = MemoryJournal "ser-reentrant"

                    use gSer =
                        new Graph (
                            { GraphOptions.Default with
                                ThreadAffinity = Serialised
                                Journal = Some (serJournal :> IInputJournal)
                            }
                        )

                    let sJ = Signal (gSer, 0) |> Journal.key "sj" intCodec

                    let readBack =
                        gSer.Batch (
                            System.Func<int>(fun () ->
                                sJ.Value <- 7
                                sJ.Value <- 7
                                sJ.Value)
                        )

                    Expect.equal readBack 7 "reentrant journaled Serialised batch returns written signal value"

                    let recordedWrites =
                        serJournal.Entries
                        |> List.filter (fun entry -> entry.Kind = JournalEntryKind.Write)

                    Expect.equal recordedWrites.Length 2 "reentrant journaled Serialised writes record both writes"
#endif
            }
        ]
