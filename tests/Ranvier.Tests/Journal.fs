module Ranvier.Tests.Journal

open Expecto
open Ranvier

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

            test "the entry unions print their case names" {
                Expect.equal (JournalEntryKind.Settle.ToString ()) "Settle" "a kind"
                Expect.equal (JournalPayload.Empty.ToString ()) "Empty" "Empty"
                Expect.equal ((JournalPayload.Encoded ("int", 1, "3")).ToString()) "Encoded (int, 1, 3)" "Encoded"
                Expect.equal ((JournalPayload.NonReplayable "T").ToString()) "NonReplayable T" "NonReplayable"
            }
        ]
