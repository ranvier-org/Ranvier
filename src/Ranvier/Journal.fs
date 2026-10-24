namespace Ranvier

open System.Collections.Generic

/// <summary>An input journal that keeps its entries in memory, in recording order.</summary>
type MemoryJournal(stream: string) =
    let entries = List<JournalEntry>()

    /// <summary>The entries recorded so far, oldest first.</summary>
    member _.Entries: JournalEntry list = List.ofSeq entries

    /// <summary>Removes every recorded entry.</summary>
    member _.Clear() =
        entries.Clear ()

    interface IInputJournal with
        member _.Stream = stream

        member _.Record entry =
            entries.Add entry

    // AoT compat
    /// <summary>The type name, the stream and the entry count.</summary>
    override _.ToString() =
        "MemoryJournal "
        + stream
        + " ("
        + string entries.Count
        + " entries)"
