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

let private run (value: Harness) (check: unit -> unit) =
    let previous = read ()
    write value

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
