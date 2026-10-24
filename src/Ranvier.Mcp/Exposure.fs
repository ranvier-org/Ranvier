namespace Ranvier.Mcp

open System
open System.Collections.Generic
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.RegularExpressions
open System.Threading
open Ranvier

/// <summary>An immutable published state: the exposed values, encoded, at one version.</summary>
/// <remarks>Every value in a snapshot comes from the same flush. Version 0 is the empty state before the first publication.</remarks>
type Snapshot =
    {
        Version: int64
        Values: Map<string, JsonElement>
    }

/// <summary>A new snapshot, and the names of the values whose encoding differs from the previous snapshot.</summary>
type Publication =
    {
        Snapshot: Snapshot
        /// <summary>In registration order.</summary>
        Changed: string list
    }

/// <summary>An exposed value: its name and the reader that encodes its current value.</summary>
type internal ExposedValue =
    { Name: string; Read: unit -> JsonNode }

/// <summary>
/// The graph state an adapter may show: named values registered before <c>Start</c>, published as versioned snapshots.
/// </summary>
/// <remarks>
/// <para>
/// <c>Start</c> creates one effect, under the current owner, that reads every exposed value. A flush that changes any
/// encoding publishes one snapshot with a version one higher; a re-run whose encodings all equal the previous snapshot's
/// publishes nothing, as does a memo cutoff, which stops the effect re-running at all. A batch produces one publication.
/// </para>
/// <para>
/// <c>Snapshot</c> is safe to read from any thread and never enters the graph. Registration and <c>Start</c> run on the
/// graph thread. A reader that raises fails the publication effect, and the snapshot keeps its previous value.
/// </para>
/// </remarks>
[<Sealed>]
type Exposure() =
    static let validName = Regex ("^[A-Za-z0-9_.~-]+$", RegexOptions.CultureInvariant)

    static let nullElement = JsonDocument.Parse("null").RootElement.Clone()

    static let encodeText (node: JsonNode) =
        match node with
        | null -> "null"
        | node -> node.ToJsonString ()

    static let parse (text: string) =
        if text = "null" then
            nullElement
        else
            use document = JsonDocument.Parse text
            document.RootElement.Clone ()

    let entries = ResizeArray<ExposedValue>()
    let names = HashSet<string>(StringComparer.Ordinal)
    let published = Event<Publication>()
    let mutable started = false
    let mutable snapshot = { Version = 0L; Values = Map.empty }
    let mutable lastTexts: string array = null


    let publish () =
        let texts =
            entries
            |> Seq.map (fun entry -> encodeText (entry.Read ()))
            |> Array.ofSeq

        let changed =
            [
                for index in 0 .. texts.Length - 1 do
                    if
                        isNull lastTexts
                        || lastTexts[index] <> texts[index]
                    then
                        entries[index].Name
            ]

        if not changed.IsEmpty then
            let previous = snapshot

            let values =
                (previous.Values, Seq.indexed entries)
                ||> Seq.fold (fun values (index, entry) ->
                    if
                        isNull lastTexts
                        || lastTexts[index] <> texts[index]
                    then
                        Map.add entry.Name (parse texts[index]) values
                    else
                        values)

            let next =
                {
                    Version = previous.Version + 1L
                    Values = values
                }

            lastTexts <- texts
            Volatile.Write (&snapshot, next)
            published.Trigger { Snapshot = next; Changed = changed }

    /// <summary>The latest published snapshot. Readable from any thread.</summary>
    member _.Snapshot = Volatile.Read &snapshot

    /// <summary>The exposed names, in registration order.</summary>
    member _.Names = entries |> Seq.map _.Name |> List.ofSeq

    /// <summary>
    /// Raised on the graph thread, inside the publishing flush, after <c>Snapshot</c> holds the new snapshot.
    /// </summary>
    member _.Published = published.Publish

    /// <summary>Exposes <c>read</c>'s value under <c>name</c>, encoded by <c>encode</c>.</summary>
    /// <exception cref="T:System.InvalidOperationException">Called after <c>Start</c>.</exception>
    /// <exception cref="T:System.ArgumentException">The name is taken, or holds a character outside <c>[A-Za-z0-9_.~-]</c>.</exception>
    member _.Value(name: string, encode: 'T -> JsonNode, read: unit -> 'T) =
        if started then
            invalidOp $"Exposure has started; '{name}' must be exposed before Start."

        if isNull name || not (validName.IsMatch name) then
            invalidArg (nameof name) $"'{name}' is not an exposed name: use [A-Za-z0-9_.~-]+."

        if not (names.Add name) then
            invalidArg (nameof name) $"'{name}' is already exposed."

        entries.Add
            {
                Name = name
                Read = fun () -> encode (read ())
            }

    /// <summary>
    /// Closes registration and creates the publication effect under the current owner of the ambient graph, which
    /// publishes the first snapshot.
    /// </summary>
    /// <exception cref="T:System.InvalidOperationException">Called twice, or with no ambient graph.</exception>
    member _.Start() =
        if started then
            invalidOp "Exposure has already started."

        Graph.Current |> ignore
        started <- true
        createEffect publish

/// <summary>Registration functions over an <c>Exposure</c>, for use in a host's setup.</summary>
[<RequireQualifiedAccess>]
module Expose =
    /// <summary>Exposes <c>read</c>'s value under <c>name</c>, encoded by <c>encode</c>.</summary>
    let value (name: string) (encode: 'T -> JsonNode) (read: unit -> 'T) (exposure: Exposure) =
        exposure.Value (name, encode, read)
