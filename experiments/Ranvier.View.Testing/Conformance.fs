/// <summary>The conformance case format: cases written once, run under jsdom and on the DOM twin.</summary>
module Ranvier.View.Testing.Conformance

open Fable.Core.TS.Dom

/// <summary>The host-specific operations a case uses: the document, the clock, and the artefact sink.</summary>
type Harness =
    abstract Platform: string
    abstract Document: Document
    /// <summary>The <c>post</c> of <c>QueuedCommits</c>: appends to the harness's queue.</summary>
    abstract Post: (unit -> unit) -> unit
    /// <summary>Drains the harness's queue, including callbacks queued while it drains.</summary>
    abstract RunMicrotasks: unit -> unit
    abstract AdvanceBy: float -> unit
    /// <summary>Records a named artefact (host HTML, dump text) for the cross-host comparison.</summary>
    abstract Record: name: string * text: string -> unit
    /// <summary>The messages of the listener failures since the last call, which it clears.</summary>
    abstract ListenerErrors: unit -> string list

type Case = string * (Harness -> unit)

/// <summary>
/// The running case's harness. It keeps the listener failures already recorded as artefacts, and its
/// <c>ListenerErrors</c> returns them with the later ones.
/// </summary>
type private Running(inner: Harness) =
    let recorded = ResizeArray<string>()

    /// <summary>The failures since the last call, kept for <c>ListenerErrors</c>.</summary>
    member _.Fresh() : string list =
        let fresh = inner.ListenerErrors ()
        recorded.AddRange fresh
        fresh

    interface Harness with
        member _.Platform = inner.Platform
        member _.Document = inner.Document

        member _.Post callback =
            inner.Post callback

        member _.RunMicrotasks() =
            inner.RunMicrotasks ()

        member _.AdvanceBy milliseconds =
            inner.AdvanceBy milliseconds

        member _.Record(name, text) =
            inner.Record (name, text)

        member _.ListenerErrors() =
            let all = List.ofSeq recorded @ inner.ListenerErrors ()
            recorded.Clear ()
            all

#if FABLE_COMPILER
let mutable private current: Harness = Unchecked.defaultof<_>

let private read () = current

let private write (value: Harness) =
    current <- value
#else
[<Sealed; AbstractClass>]
type private Slot =
    [<System.ThreadStatic; DefaultValue>]
    static val mutable private current: Harness

    static member Current
        with get () = Slot.current
        and set (value: Harness) = Slot.current <- value

let private read () =
    Slot.Current

let private write (value: Harness) =
    Slot.Current <- value
#endif

let harness () : Harness =
    match box (read ()) with
    | null -> raise (System.InvalidOperationException "No conformance case is running on this thread")
    | value -> unbox<Harness> value

let document () : Document =
    (harness ()).Document

/// <summary>Records a named artefact with the running case's harness; outside a case it does nothing.</summary>
let recordIfRunning (name: string) (text: string) : unit =
    match box (read ()) with
    | null -> ()
    | value -> (unbox<Harness> value).Record(name, text)

/// <summary>
/// Records the listener failures since the last such record as a <c>listener-errors</c> artefact, one message per
/// line, when there are any; the running harness's <c>ListenerErrors</c> still returns them.
/// </summary>
let recordListenerErrors () : unit =
    match box (read ()) with
    | null -> ()
    | value ->
        match (unbox<Running> value).Fresh() with
        | [] -> ()
        | messages -> (unbox<Harness> value).Record("listener-errors", String.concat "\n" messages)

/// <summary>The node's document, or the node itself when it is a document.</summary>
let documentOf (node: Node) : Document =
    match node.ownerDocument with
    | Some document -> document
    | None -> unbox<Document> node

let private run (value: Harness) (check: unit -> unit) =
    let previous = read ()
    write (Running value)

    try
        check ()
    finally
        write previous

let ofChecks (checks: (string * (unit -> unit)) array) : Case array =
    checks
    |> Array.map (fun (name, check) -> name, (fun value -> run value check))

let platformOnly (platform: string) (reason: string) (name: string, check: unit -> unit) =
    $"[{platform}] {name} ({reason})", check

let skippedOn (platform: string) (name: string) =
    (name.StartsWith "[jsdom]" && platform <> "jsdom")
    || (name.StartsWith "[twin]" && platform <> "twin")
