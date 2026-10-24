module Ranvier.Tests.Journal

open Expecto
open Ranvier

let journaled () =
    let journal = MemoryJournal "test"
    journal, new Graph (GraphOptions.Default.WithJournal journal)

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

            test "the entry unions print their case names" {
                Expect.equal (JournalEntryKind.Settle.ToString ()) "Settle" "a kind"
                Expect.equal (JournalPayload.Empty.ToString ()) "Empty" "Empty"
                Expect.equal ((JournalPayload.Encoded ("int", 1, "3")).ToString()) "Encoded (int, 1, 3)" "Encoded"
                Expect.equal ((JournalPayload.NonReplayable "T").ToString()) "NonReplayable T" "NonReplayable"
            }
        ]
