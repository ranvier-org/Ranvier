/// <summary>Ownership and signals for anime objects: reverting with an owner, playback as memos, signal-driven seeking.</summary>
module Ranvier.Anime.Reactive

open Fable.Core
open global.Ranvier

/// <summary>Reverts <c>revertible</c> when the current owner is disposed, and returns it.</summary>
/// <remarks>Outside every root the owner is the graph's own scope, which reverts it when the graph is disposed.</remarks>
let own (revertible: 'r) : 'r :> IRevertible =
    Api.onCleanup (fun () -> revertible.revert ())
    revertible

/// <summary>A tickable's playback state as memos, updated from its callbacks.</summary>
type Playback =
    {
        /// <summary>The overall progress in 0..1.</summary>
        Progress: Memo<float>
        /// <summary>Whether the tickable is playing and not completed.</summary>
        Running: Memo<bool>
        Completed: Memo<bool>
    }

[<Emit("(() => { const previous = $0[$1]; $0[$1] = self => { if (previous) previous(self); $2(); }; return () => { $0[$1] = previous; }; })()")>]
let private chain (tickable: ITickable) (callback: string) (after: unit -> unit) : unit -> unit = jsNative

/// <summary>
/// The playback state of <c>tickable</c>, written on its begin, update, loop, pause and completion. The callbacks it
/// already had still run, first.
/// </summary>
/// <remarks>The callbacks it had are restored when the current owner is disposed.</remarks>
let track (tickable: #ITickable) : Playback =
    let tickable = tickable :> ITickable
    let graph = Graph.Current
    let progress = Api.createSignal tickable.progress
    let running = Api.createSignal (not tickable.paused && not tickable.completed)
    let completed = Api.createSignal tickable.completed

    let write () =
        graph.Batch (fun () ->
            progress.Value <- tickable.progress
            running.Value <- not tickable.paused && not tickable.completed
            completed.Value <- tickable.completed)

    let restores =
        [| "onBegin"; "onUpdate"; "onLoop"; "onPause"; "onComplete" |]
        |> Array.map (fun callback -> chain tickable callback write)

    Api.onCleanup (fun () ->
        for restore in restores do
            restore ())

    {
        Progress = Api.createMemo (fun _ -> progress.Value)
        Running = Api.createMemo (fun _ -> running.Value)
        Completed = Api.createMemo (fun _ -> completed.Value)
    }

/// <summary>An effect that seeks <c>tickable</c> to the progress returned by <c>read</c>, in 0..1, whenever it changes.</summary>
/// <remarks>A tickable created with <c>autoplay = false</c> moves only when <c>read</c> changes.</remarks>
let scrub (read: unit -> float) (tickable: #ITickable) : unit =
    Api.createEffectOn read (fun progress -> tickable.progress <- progress)

/// <summary>An effect that animates <c>property</c> of <c>animatable</c> to <c>read ()</c> whenever it changes.</summary>
let drive (read: unit -> float) (animatable: Animatable) (property: string) : unit =
    Api.createEffectOn read (fun value -> animatable.set (property, value))

/// <summary>Whether animations run in full or reduced to their end states.</summary>
type MotionPolicy =
    | Full
    | Reduced

[<AllowNullLiteral>]
type private MediaQuery =
    abstract matches: bool
    abstract addEventListener: string * (MediaQuery -> unit) -> unit

[<Emit("(typeof window !== 'undefined' && typeof window.matchMedia === 'function' ? window.matchMedia($0) : null)")>]
let private matchMedia (query: string) : MediaQuery = jsNative

let mutable private policyGraph: Graph = Unchecked.defaultof<_>
let mutable private policySignal: Signal<MotionPolicy> = Unchecked.defaultof<_>

let private policyOf (matches: bool) =
    if matches then Reduced else Full

/// <summary>
/// The motion policy of the current graph: a signal seeded from <c>(prefers-reduced-motion: reduce)</c> on first read and
/// written on each change of the media query.
/// </summary>
/// <remarks>
/// Every call on a graph returns the same signal. A write overrides the media query until its next change. Without
/// <c>matchMedia</c>, as on .NET or in jsdom, the seed is <c>Full</c>.
/// </remarks>
let policy () : Signal<MotionPolicy> =
    let graph = Graph.Current

    if not (obj.ReferenceEquals (policyGraph, graph)) then
#if FABLE_COMPILER
        let query = matchMedia "(prefers-reduced-motion: reduce)"
#else
        let query: MediaQuery = null
#endif
        let signal =
            Api.createSignal (if isNull query then Full else policyOf query.matches)

        if not (isNull query) then
            query.addEventListener ("change", (fun change -> signal.Value <- policyOf change.matches))

        policyGraph <- graph
        policySignal <- signal

    policySignal
