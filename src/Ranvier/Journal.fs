namespace Ranvier

open System
open System.Collections.Generic

/// <summary>
/// The text form of a journaled value. <c>Name</c> and <c>Version</c> are recorded in each entry, and replay requires
/// the target's codec to carry the same pair.
/// </summary>
/// <remarks>
/// A Ranvier.Wire <c>WireCodec&lt;'T&gt;</c> adapts as
/// <c>{ Name = name; Version = 1; Encode = wire.Encode; Decode = wire.Decode &gt;&gt; Result.mapError string }</c>.
/// </remarks>
type JournalCodec<'T> =
    {
        Name: string
        Version: int
        Encode: 'T -> string
        Decode: string -> Result<'T, string>
    }

/// <summary>The journal binding of a node of value type <c>'T</c>.</summary>
type internal CodecBinding<'T>(key: string, codec: JournalCodec<'T>) =
    inherit JournalBinding(key, codec.Name, codec.Version)

    member _.Codec = codec

    override _.Encode value =
        codec.Encode (unbox<'T> value)

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

/// <summary>Registers input nodes on their graph's journal.</summary>
[<RequireQualifiedAccess>]
module Journal =
    let private register (graph: Graph) (node: INode) (key: string) (binding: JournalBinding) =
        if String.IsNullOrEmpty key then
            raise (ArgumentException ("A journal key must be a non-empty string.", nameof key))

        let recorder = graph.Journal

        if not (Platform.isNull recorder) then
            graph.AssertOnGraphThread "Registering a journal key"
            recorder.Bind (node.Id, binding)
            graph.AddCleanup (fun () -> recorder.Unbind (node.Id, key))
#if RANVIER_TRACE
            let log = ((box graph) :?> ITraced).TraceLog

            if not (log.HasLabel node.Id) then
                log.Append (TraceEventKind.Label, node.Id, 0, 1, 0, 0, key)
                |> ignore
#endif

    /// <summary>
    /// Registers <c>signal</c> on its graph's journal under the application key <c>key</c>, so its external writes
    /// record values encoded by <c>codec</c>, and returns <c>signal</c>. Does nothing when the graph has no journal.
    /// </summary>
    /// <remarks>
    /// The key is the node's stable name across runs. The registration belongs to the innermost enclosing scope: when
    /// that scope is disposed or its computation re-runs, the key is free for another node. The traced build also labels
    /// an unlabelled node with the key.
    /// </remarks>
    /// <exception cref="T:System.InvalidOperationException">
    /// The call runs in the body of a pure computation, such as a <c>createMemo</c> memo, as <c>onCleanup</c> does.
    /// </exception>
    /// <exception cref="T:System.ArgumentException">
    /// <c>key</c> is null or empty, or the graph already has a node registered under <c>key</c>.
    /// </exception>
    let key (key: string) (codec: JournalCodec<'T>) (signal: Signal<'T>) : Signal<'T> =
        register signal.Graph signal key (CodecBinding<'T>(key, codec))
        signal

    /// <summary>
    /// Registers <c>source</c> on its graph's journal under the application key <c>key</c>, as <c>key</c> registers a
    /// signal, and returns <c>source</c>. Its external settles record values encoded by <c>codec</c>; its external
    /// failures record the exception's message.
    /// </summary>
    /// <exception cref="T:System.ArgumentException">
    /// <c>key</c> is null or empty, or the graph already has a node registered under <c>key</c>.
    /// </exception>
    let keySource (key: string) (codec: JournalCodec<'T>) (source: AsyncSource<'T>) : AsyncSource<'T> =
        register source.Graph source key (CodecBinding<'T>(key, codec))
        source
