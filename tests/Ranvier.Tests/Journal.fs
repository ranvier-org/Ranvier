module Ranvier.Tests.InputJournal

open Expecto
open Ranvier

let journaled () =
    let journal = MemoryJournal "test"
    journal, new Graph (GraphOptions.Default.WithJournal journal)

let intCodec: JournalCodec<int> =
    {
        Name = "int"
        Version = 1
        Encode = string
        Decode =
            fun text ->
                match System.Int32.TryParse text with
                | true, value -> Ok value
                | _ -> Error ("not an int: " + text)
    }

let writes (journal: MemoryJournal) =
    journal.Entries
    |> List.filter (fun e -> e.Kind = JournalEntryKind.Write)

[<Tests>]
let tests =
    testList
        "Journal"
        [
            test "GraphOptions.Default has no journal" { Expect.isNone GraphOptions.Default.Journal "the default records nothing" }

            test "WithJournal sets the journal" {
                let journal =
                    { new IInputJournal with
                        member _.Stream = "s"
                        member _.Record _ = ()
                    }

                let options = GraphOptions.Default.WithJournal journal

                Expect.isTrue
                    (options.Journal
                     |> Option.exists (fun j -> obj.ReferenceEquals (j, journal)))
                    "the given journal"
            }

            test "an external write to an unkeyed signal records a non-replayable Write" {
                let journal, g = journaled ()
                let s = Signal (g, 1)
                s.Value <- 2

                match journal.Entries with
                | [ entry ] ->
                    Expect.equal entry.Schema 1 "schema 1"
                    Expect.equal entry.Stream "test" "the journal's stream"
                    Expect.equal entry.Seq 1 "the first entry"
                    Expect.equal entry.Kind JournalEntryKind.Write "a write"
                    Expect.isNone entry.Key "an unkeyed node"
                    Expect.equal entry.Payload (JournalPayload.NonReplayable entry.PayloadType) "no codec"
                    Expect.isNonEmpty entry.PayloadType "the value's type"
                | entries -> failtestf "expected one entry, got %A" entries
            }

            test "an equal write is recorded" {
                let journal, g = journaled ()
                let s = Signal (g, 1)
                s.Value <- 1
                s.Value <- 1

                Expect.equal (journal.Entries |> List.map (fun e -> e.Seq)) [ 1; 2 ] "both writes, in order"
            }

            test "a graph without a journal records nothing and writes as before" {
                let g = new Graph ()
                let s = Signal (g, 1)
                s.Value <- 2
                Expect.equal s.Peek 2 "the write lands"
            }

            test "writes made by a running computation are not journaled" {
                let journal, g = journaled ()
                let s = Signal (g, 0)
                let byEffect = Signal (g, 0)
                let byUntrack = Signal (g, 0)
                let byAct = Signal (g, 0)
                let byMemo = Signal (g, 0)

                let m =
                    g.Run (fun () ->
                        createEffect (fun () -> byEffect.Value <- s.Value)

                        createEffect (fun () ->
                            let v = s.Value
                            untrack (fun () -> byUntrack.Value <- v))

                        createEffectOn (fun () -> s.Value) (fun v -> byAct.Value <- v)

                        createMemo (fun _ ->
                            byMemo.Value <- s.Value
                            s.Value))

                Expect.equal m.Value 0 "the memo runs"
                s.Value <- 5
                Expect.equal m.Value 5 "the memo re-runs"

                Expect.equal (byEffect.Peek, byUntrack.Peek, byAct.Peek, byMemo.Peek) (5, 5, 5, 5) "every derived write landed"

                Expect.equal
                    (journal.Entries
                     |> List.filter (fun e -> e.Kind = JournalEntryKind.Write)
                     |> List.length)
                    1
                    "only the external write to s"
            }

            test "a keyed write records its key and encoded value" {
                let journal, g = journaled ()
                let s = Signal (g, 1) |> Journal.key "count" intCodec
                s.Value <- 7

                match writes journal with
                | [ entry ] ->
                    Expect.equal entry.Key (Some "count") "the key"
                    Expect.equal entry.Payload (JournalPayload.Encoded ("int", 1, "7")) "the codec's text"
                | entries -> failtestf "expected one write, got %A" entries
            }

            test "Journal.key returns the signal it registers" {
                let _, g = journaled ()
                let s = Signal (g, 1)
                Expect.isTrue (obj.ReferenceEquals (Journal.key "count" intCodec s, s)) "the same signal"
            }

            test "a duplicate key on one graph raises, naming the key" {
                let _, g = journaled ()

                Signal (g, 1)
                |> Journal.key "count" intCodec
                |> ignore

                let error =
                    try
                        Signal (g, 2)
                        |> Journal.key "count" intCodec
                        |> ignore

                        None
                    with ex ->
                        Some ex

                match error with
                | Some (:? System.ArgumentException as ex) -> Expect.stringContains ex.Message "count" "names the key"
                | other -> failtestf "expected an ArgumentException, got %A" other
            }

            test "a key registered in a disposed root is free again" {
                let journal, g = journaled ()

                let mount () =
                    g.Run (fun () ->
                        createRoot (fun owner ->
                            let s = Signal (g, 0) |> Journal.key "count" intCodec

                            owner, s))

                let first, _ = mount ()
                first.Dispose ()
                let second, s = mount ()
                s.Value <- 4

                match writes journal with
                | [ entry ] -> Expect.equal entry.Key (Some "count") "the second mount's signal holds the key"
                | entries -> failtestf "expected one write, got %A" entries

                second.Dispose ()
            }

            test "a key registered in an owning memo's body is free again when the memo re-runs" {
                let _, g = journaled ()
                let trigger = Signal (g, 0)

                let m =
                    g.Run (fun () ->
                        createMemoWith (fun _ ->
                            Signal (g, trigger.Value)
                            |> Journal.key "row" intCodec
                            |> ignore

                            trigger.Value))

                Expect.equal m.Value 0 "the first run"
                trigger.Value <- 1
                Expect.equal m.Value 1 "the re-run registers the key again"
            }

            test "Journal.key in a pure memo's body raises the scope error" {
                let _, g = journaled ()

                let m =
                    g.Run (fun () ->
                        createMemo (fun _ ->
                            Signal (g, 0)
                            |> Journal.key "row" intCodec
                            |> ignore

                            0))

                Expect.throwsT<System.InvalidOperationException> (fun () -> m.Value |> ignore) "the registration is owned"

                g.Run (fun () ->
                    Signal (g, 1)
                    |> Journal.key "row" intCodec
                    |> ignore)
            }

            test "a keyed node registered under a second key raises and keeps its first key" {
                let journal, g = journaled ()

                let owner, s =
                    g.Run (fun () -> createRoot (fun owner -> owner, Signal (g, 0) |> Journal.key "a" intCodec))

                let error =
                    try
                        g.Run (fun () -> s |> Journal.key "b" intCodec |> ignore)
                        None
                    with ex ->
                        Some ex

                match error with
                | Some (:? System.ArgumentException as ex) -> Expect.stringContains ex.Message "\"a\"" "names the held key"
                | other -> failtestf "expected an ArgumentException, got %A" other

                s.Value <- 5

                match writes journal with
                | [ entry ] -> Expect.equal entry.Key (Some "a") "the first key still records"
                | entries -> failtestf "expected one write, got %A" entries

                owner.Dispose ()

                g.Run (fun () ->
                    Signal (g, 1)
                    |> Journal.key "b" intCodec
                    |> ignore)
            }

            test "a write after an await in an async memo's body is not journaled" {
                let journal, g = journaled ()
                let target = Signal (g, 0)
                let gate = System.Threading.Tasks.TaskCompletionSource<int>()

                let a =
                    Make.AsyncMemo<int>(
                        g,
                        fun _ _ ->
                            task {
                                let! value = gate.Task
                                target.Value <- value
                                return value
                            }
                    )

                a.TryValue |> ignore
                gate.SetResult 3
                Expect.equal target.Peek 3 "the continuation's write landed"
                Expect.isEmpty (writes journal) "a continuation is part of the body"
            }

            test "the same key on two graphs is allowed" {
                let _, g1 = journaled ()
                let _, g2 = journaled ()

                Signal (g1, 1)
                |> Journal.key "count" intCodec
                |> ignore

                Signal (g2, 1)
                |> Journal.key "count" intCodec
                |> ignore
            }

            test "Journal.key on a graph without a journal is a no-op" {
                let g = new Graph ()
                let s = Signal (g, 1) |> Journal.key "count" intCodec

                Signal (g, 2)
                |> Journal.key "count" intCodec
                |> ignore

                s.Value <- 3
                Expect.equal s.Peek 3 "the write lands"
            }

            test "an Encode that raises records a non-replayable entry and the write lands" {
                let journal, g = journaled ()

                let failing =
                    { intCodec with
                        Encode = fun _ -> failwith "no"
                    }

                let s = Signal (g, 1) |> Journal.key "count" failing
                s.Value <- 4

                Expect.equal s.Peek 4 "the write lands"

                match writes journal with
                | [ entry ] ->
                    Expect.equal entry.Key (Some "count") "the key"
                    Expect.equal entry.Payload (JournalPayload.NonReplayable entry.PayloadType) "not encodable"
                | entries -> failtestf "expected one write, got %A" entries
            }

#if RANVIER_TRACE
            test "a traced Journal.key labels an unlabelled node with the key" {
                let _, g = journaled ()
                let s = Signal (g, 1) |> Journal.key "count" intCodec
                Expect.equal (Trace.resolve g "/count") (Some (s :> INode).Id) "the key is the trace label"
            }

            test "a traced Journal.key keeps an existing label" {
                let _, g = journaled ()
                let s = Signal (g, 1)
                Trace.label (g, s, "mine")
                Journal.key "count" intCodec s |> ignore
                Expect.equal (Trace.resolve g "/mine") (Some (s :> INode).Id) "the label stays"
            }
#endif

            test "settles and failures of a keyed source record in graph-thread order" {
                let journal, g = journaled ()

                let source =
                    AsyncSource<int> g
                    |> Journal.keySource "load" intCodec

                source.Settle 3
                source.Fail (System.InvalidOperationException "boom")

                Expect.equal
                    (journal.Entries
                     |> List.map (fun e -> e.Kind, e.Key, e.Payload))
                    [
                        JournalEntryKind.Settle, Some "load", JournalPayload.Encoded ("int", 1, "3")
                        JournalEntryKind.Fail, Some "load", JournalPayload.Encoded ("exn", 1, "boom")
                    ]
                    "a settle, then a failure carrying its message"

                Expect.equal (journal.Entries |> List.map (fun e -> e.Seq)) [ 1; 2 ] "consecutive positions"
#if !FABLE_COMPILER
                Expect.equal journal.Entries[1].PayloadType "System.InvalidOperationException" "the exception's type"
#endif
            }

            test "settles of an unkeyed source record non-replayable entries" {
                let journal, g = journaled ()
                let source = AsyncSource<int> g
                source.Settle 3
                source.Fail (System.InvalidOperationException "boom")

                Expect.all
                    journal.Entries
                    (fun e ->
                        e.Key = None
                        && e.Payload = JournalPayload.NonReplayable e.PayloadType)
                    "no codec"

                Expect.equal (journal.Entries.Length) 2 "both recorded"
            }

            test "a settle made by a running computation is not journaled" {
                let journal, g = journaled ()
                let s = Signal (g, 0)

                let source =
                    AsyncSource<int> g
                    |> Journal.keySource "load" intCodec

                g.Run (fun () -> createEffect (fun () -> source.Settle s.Value))
                s.Value <- 1

                match source.TryValue with
                | Ready value -> Expect.equal value 1 "the derived settle landed"
                | other -> failtestf "expected Ready 1, got %O" other

                Expect.equal
                    (journal.Entries |> List.map (fun e -> e.Kind))
                    [ JournalEntryKind.Write; JournalEntryKind.Flush ]
                    "only the external write and its flush"
            }

            test "one Flush follows each flush pass that followed recorded entries" {
                let journal, g = journaled ()
                let s = Signal (g, 0)
                let seen = ResizeArray ()
                g.Run (fun () -> createEffect (fun () -> seen.Add s.Value))
                s.Value <- 1
                s.Value <- 2

                Expect.sequenceEqual seen [ 0; 1; 2 ] "each write ran the effect"

                Expect.equal
                    (journal.Entries |> List.map (fun e -> e.Kind))
                    [
                        JournalEntryKind.Write
                        JournalEntryKind.Flush
                        JournalEntryKind.Write
                        JournalEntryKind.Flush
                    ]
                    "a boundary after each pass"

                Expect.equal (journal.Entries |> List.map (fun e -> e.Seq)) [ 1; 2; 3; 4 ] "consecutive positions"

                Expect.all
                    (journal.Entries
                     |> List.filter (fun e -> e.Kind = JournalEntryKind.Flush))
                    (fun e ->
                        e.Key = None
                        && e.Payload = JournalPayload.Empty
                        && e.PayloadType = "")
                    "a Flush carries nothing"
            }

            test "a batch of two writes gives two Write entries and one Flush" {
                let journal, g = journaled ()
                let s = Signal (g, 0)
                let t = Signal (g, 0)
                g.Run (fun () -> createEffect (fun () -> s.Value + t.Value |> ignore))

                g.Batch (fun () ->
                    s.Value <- 1
                    t.Value <- 2)

                Expect.equal
                    (journal.Entries |> List.map (fun e -> e.Kind))
                    [ JournalEntryKind.Write; JournalEntryKind.Write; JournalEntryKind.Flush ]
                    "one boundary for the batch"
            }

            test "a write that queues nothing records no Flush" {
                let journal, g = journaled ()
                let s = Signal (g, 0)
                s.Value <- 1

                Expect.equal (journal.Entries |> List.map (fun e -> e.Kind)) [ JournalEntryKind.Write ] "no flush pass followed"

                g.Flush ()

                Expect.equal
                    (journal.Entries |> List.map (fun e -> e.Kind))
                    [ JournalEntryKind.Write; JournalEntryKind.Flush ]
                    "an explicit flush after the write is a boundary"
            }

            test "replay of writes, settles and failures into a fresh graph gives equal values" {
                let build () =
                    let journal, g = journaled ()
                    let a = Signal (g, 0) |> Journal.key "a" intCodec
                    let b = Signal (g, 0) |> Journal.key "b" intCodec

                    let load =
                        AsyncSource<int> g
                        |> Journal.keySource "load" intCodec

                    let fails =
                        AsyncSource<int> g
                        |> Journal.keySource "fails" intCodec

                    let sum = g.Run (fun () -> createMemo (fun _ -> a.Value + b.Value))
                    let runs = ResizeArray<int>()
                    g.Run (fun () -> createEffect (fun () -> runs.Add sum.Value))
                    journal, g, a, b, load, fails, sum, runs

                let journal, g, a, b, load, fails, sum, runs = build ()
                a.Value <- 2

                g.Run (fun () ->
                    batch (fun () ->
                        a.Value <- 3
                        b.Value <- 4))

                load.Settle 9
                fails.Fail (System.TimeoutException "late")

                let _, g2, a2, b2, load2, fails2, sum2, runs2 = build ()
                Journal.replay ReplayMode.Diagnostic g2 journal.Entries

                Expect.equal (a2.Peek, b2.Peek) (a.Peek, b.Peek) "the signals"
                Expect.equal sum2.Value sum.Value "the memo"
                Expect.equal (List.ofSeq runs2) (List.ofSeq runs) "the effect ran once per recorded flush"

                match load2.TryValue with
                | Ready value -> Expect.equal value 9 "the recorded settle"
                | other -> failtestf "expected Ready 9, got %O" other

                match fails2.TryValue with
                | Failed (:? JournalReplayedFailure as ex) -> Expect.equal ex.Message "late" "the recorded message"
                | other -> failtestf "expected the replayed failure, got %O" other

                match fails.TryValue with
                | Failed _ -> ()
                | other -> failtestf "expected the live failure, got %O" other
            }

            test "re-enactment skips the recorded settles" {
                let journal, g = journaled ()
                let a = Signal (g, 0) |> Journal.key "a" intCodec

                let load =
                    AsyncSource<int> g
                    |> Journal.keySource "load" intCodec

                a.Value <- 1
                load.Settle 9

                let _, g2 = journaled ()
                let a2 = Signal (g2, 0) |> Journal.key "a" intCodec

                let load2 =
                    AsyncSource<int> g2
                    |> Journal.keySource "load" intCodec

                Journal.replay ReplayMode.Reenact g2 journal.Entries

                Expect.equal a2.Peek 1 "the write is applied"
                Expect.equal load2.TryValue Pending "the settle is left to the re-run async work"
            }

            test "Journal.replaying returns the mode while the replay runs" {
                let journal, g = journaled ()
                let a = Signal (g, 0) |> Journal.key "a" intCodec
                a.Value <- 1

                let _, g2 = journaled ()
                let a2 = Signal (g2, 0) |> Journal.key "a" intCodec
                let seen = ResizeArray<ReplayMode option>()

                g2.Run (fun () ->
                    createEffect (fun () ->
                        seen.Add (
                            a2.Value |> ignore
                            Journal.replaying g2
                        )))

                Journal.replay ReplayMode.Diagnostic g2 journal.Entries

                Expect.equal (List.ofSeq seen) [ None; Some ReplayMode.Diagnostic ] "the effect's run inside the replay"
                Expect.isNone (Journal.replaying g2) "after the replay"
            }

            test "replay requires a graph with a journal" {
                let g = new Graph (GraphOptions.Default)
                Expect.throwsT<System.InvalidOperationException> (fun () -> Journal.replay ReplayMode.Diagnostic g []) "no journal"
            }

            testList
                "replay raises JournalReplayException"
                [
                    let entry kind key payload =
                        {
                            Schema = 1
                            Stream = "test"
                            Seq = 7
                            Kind = kind
                            Key = key
                            PayloadType = "System.Int32"
                            Payload = payload
                        }

                    let raises name (entry: JournalEntry) (reason: string) =
                        test name {
                            let _, g = journaled ()
                            let a = Signal (g, 0) |> Journal.key "a" intCodec

                            match
                                (try
                                    Journal.replay ReplayMode.Diagnostic g [ entry ]
                                    None
                                 with ex ->
                                     Some ex)
                            with
                            | Some (:? JournalReplayException as ex) ->
                                Expect.equal ex.Seq 7 "names the seq"
                                Expect.equal ex.Key entry.Key "names the key"
                                Expect.equal ex.PayloadType "System.Int32" "names the payload type"
                                Expect.stringContains ex.Reason reason "says why"
                                Expect.stringContains ex.Message "Journal entry 7" "the message names the entry"
                            | other -> failtestf "expected a JournalReplayException, got %A" other

                            Expect.equal a.Peek 0 "nothing is applied"
                        }

                    raises
                        "on a non-replayable entry"
                        (entry JournalEntryKind.Write None (JournalPayload.NonReplayable "System.Int32"))
                        "non-replayable"

                    raises
                        "on a key the target graph lacks"
                        (entry JournalEntryKind.Write (Some "b") (JournalPayload.Encoded ("int", 1, "3")))
                        "not registered"

                    raises
                        "on a codec name mismatch"
                        (entry JournalEntryKind.Write (Some "a") (JournalPayload.Encoded ("text", 1, "3")))
                        "codec text version 1"

                    raises
                        "on a codec version mismatch"
                        (entry JournalEntryKind.Write (Some "a") (JournalPayload.Encoded ("int", 2, "3")))
                        "version 2"

                    raises
                        "on a decode error"
                        (entry JournalEntryKind.Write (Some "a") (JournalPayload.Encoded ("int", 1, "three")))
                        "not an int: three"

                    raises
                        "on an entry kind the key's input does not take"
                        (entry JournalEntryKind.Settle (Some "a") (JournalPayload.Encoded ("int", 1, "3")))
                        "takes no Settle"
                ]

            testList
                "Mvu.journal"
                [
                    let counter (g: Graph) =
                        g.Run (fun () ->
                            Ranvier.Elmish.Mvu.withCmd (0, []) (fun (msg: int) model ->
                                if msg = 0 then
                                    model, []
                                else
                                    model + msg, [ fun dispatch -> dispatch 0 ])
                            |> Ranvier.Elmish.Mvu.journal "counter" intCodec)

                    test "an external dispatch records one Message entry and its flush" {
                        let journal, g = journaled ()
                        let app = counter g
                        let seen = ResizeArray<int>()
                        g.Run (fun () -> createEffect (fun () -> seen.Add app.Model))
                        app.Dispatch 2

                        Expect.equal
                            (journal.Entries
                             |> List.map (fun e -> e.Kind, e.Key, e.Payload))
                            [
                                JournalEntryKind.Message, Some "counter", JournalPayload.Encoded ("int", 1, "2")
                                JournalEntryKind.Flush, None, JournalPayload.Empty
                            ]
                            "the model write and the command's dispatch are derived"
                    }

                    test "a dispatch from an effect records nothing" {
                        let journal, g = journaled ()
                        let app = counter g
                        let s = Signal (g, 0)

                        g.Run (fun () ->
                            createEffect (fun () ->
                                if s.Value > 0 then
                                    app.Dispatch s.Value))

                        s.Value <- 3

                        Expect.equal app.Model 3 "the dispatch landed"

                        Expect.equal
                            (journal.Entries |> List.map (fun e -> e.Kind))
                            [ JournalEntryKind.Write; JournalEntryKind.Flush ]
                            "only the external write"
                    }

                    test "replay dispatches the recorded messages" {
                        let journal, g = journaled ()
                        let app = counter g
                        app.Dispatch 2
                        app.Dispatch 5

                        let replayed, g2 = journaled ()
                        let app2 = counter g2
                        Journal.replay ReplayMode.Diagnostic g2 journal.Entries

                        Expect.equal app2.Model app.Model "the model"
                        Expect.equal replayed.Entries journal.Entries "the replay records the same entries"
                    }
                ]

            test "the entry unions print their case names" {
                Expect.equal (JournalEntryKind.Settle.ToString ()) "Settle" "a kind"
                Expect.equal (JournalPayload.Empty.ToString ()) "Empty" "Empty"
                Expect.equal ((JournalPayload.Encoded ("int", 1, "3")).ToString()) "Encoded (int, 1, 3)" "Encoded"
                Expect.equal ((JournalPayload.NonReplayable "T").ToString()) "NonReplayable T" "NonReplayable"
            }
        ]
