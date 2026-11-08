// Runs each conformance scenario on real Signal, Memo and Effect objects, and writes the event traces as
// theories/Conformance/Expected.v. Each program in theories/Conformance/Check.v states the same scenario in the
// command language, with node ids equal to creation indexes.
//
//   dotnet build src/Ranvier -c Release
//   dotnet fsi proofs/conformance/Scenarios.fsx [output path]
#r "../../src/Ranvier/bin/Release/net10.0/Ranvier.dll"

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open Ranvier

type Outcome =
    | OkV of int
    | FailV of int

/// One Rocq `event`, in trace order.
type Ev =
    | Run of int
    | Log of int * int
    | Cleanup of int
    | FailE of int * int
    | Disposed of int
    | HostRead of int * Outcome

/// An exception whose identity is `f`, as `Fail f` denotes one exception instance.
exception Boom of int

let private privateField (o: obj) (name: string) =
    o.GetType().GetField(name, BindingFlags.Instance ||| BindingFlags.NonPublic).GetValue o

let private internalProp (o: obj) (name: string) =
    o
        .GetType()
        .GetProperty(
            name,
            BindingFlags.Instance
            ||| BindingFlags.NonPublic
            ||| BindingFlags.Public
        )
        .GetValue
        o

let private idOf (node: obj) : int =
    let inode = typeof<Graph>.Assembly.GetType "Ranvier.INode"
    inode.GetProperty("Id").GetValue node :?> int

let private statusOf (node: obj) : string =
    let inode = typeof<Graph>.Assembly.GetType "Ranvier.INode"
    (inode.GetProperty("Status").GetValue node).ToString()

/// A graph with a trace. `Effect`, `Memo` and `Signal` take the label a scenario expects, and fail the scenario when
/// the label differs from the node's id.
type Ctx() =
    let g = new Graph ()
    let events = ResizeArray<Ev>()
    let booms = Dictionary<int, exn>()

    member _.Graph = g
    member _.Events = List.ofSeq events

    member _.Emit(e: Ev) =
        events.Add e

    member _.Throw(f: int) : 'a =
        match booms.TryGetValue f with
        | true, ex -> raise ex
        | _ ->
            let ex = Boom f
            booms[f] <- ex
            raise ex

    member _.Check (condition: bool) (message: string) =
        if not condition then
            failwith message

    member this.Labelled (label: int) (node: 'n) : 'n =
        this.Check (idOf (box node) = label) $"node labelled {label} has id {idOf (box node)}"
        node

    member this.Signal (label: int) (v: int) : Signal<int> =
        this.Labelled label (Signal<int>(g, v))

    member this.Memo (label: int) (body: unit -> int) : Memo<int> =
        this.Labelled label (Memo<int>(g, Func<int voption, int>(fun _ -> body ())))

    member this.OwningMemo (label: int) (body: unit -> int) : Memo<int> =
        this.Labelled label (Memo<int>(g, Func<int voption, int>(fun _ -> body ()), true))

    member this.Effect (label: int) (body: unit -> unit) : Effect =
        let run () =
            this.Emit (Run label)

            try
                body ()
            with Boom f as ex ->
                this.Emit (FailE (label, f))
                raise ex

        this.Labelled label (new Effect (g, Action run))

    member this.Cleanup (label: int) (body: unit -> unit) =
        let run () =
            this.Emit (Cleanup label)

            try
                body ()
            with Boom f as ex ->
                this.Emit (FailE (label, f))
                raise ex

        g.OnCleanup (Action run)

    member this.Log (label: int) (v: int) =
        this.Emit (Log (label, v))

    member this.Batch(body: unit -> unit) =
        g.Batch (Func<unit>(body))

    /// `CDispose`: the event marks a disposal that takes effect.
    member this.Dispose (label: int) (dispose: unit -> unit) (node: obj) =
        let was = privateField node "disposed" :?> bool
        dispose ()

        if not was then
            this.Emit (Disposed label)

    member this.HostRead (label: int) (read: unit -> int) : int =
        try
            let v = read ()
            this.Emit (HostRead (label, OkV v))
            v
        with Boom f as ex ->
            this.Emit (HostRead (label, FailV f))
            raise ex

let private lessThan (a: int) (b: int) =
    if a < b then 1 else 0

let scenarios: (string * (Ctx -> unit)) list =
    [
        // Diamond: two memos over one signal, an effect reads both.
        "S01",
        fun c ->
            let s = c.Signal 1 0

            let a =
                c.Memo 2 (fun () ->
                    let v = s.Value
                    c.Log 2 v
                    v + 1)

            let b =
                c.Memo 3 (fun () ->
                    let v = s.Value
                    c.Log 3 v
                    v * 2)

            c.Effect 4 (fun () ->
                let x = a.Value
                let y = b.Value
                c.Log 4 (x + y))
            |> ignore

            s.Value <- 1
            s.Value <- 1
            s.Value <- 2

        // Cutoff: a memo's value is unchanged, and its effect does not run.
        "S02",
        fun c ->
            let s = c.Signal 1 0

            let m =
                c.Memo 2 (fun () ->
                    let v = s.Value
                    c.Log 2 v
                    lessThan v 10)

            c.Effect 3 (fun () -> c.Log 3 m.Value) |> ignore
            s.Value <- 1
            s.Value <- 20
            s.Value <- 30

        // #302, the test markedByInlineSettle at d701e6c5, with a plain memo for its async memo. The middle x reads m, a
        // memo over a, then a itself. The run of a under m marks x while x runs, in place of the inline settle. The value
        // a / 2 is unchanged by odd writes, so those runs end stale, and each even write still reaches the effect.
        "S03",
        fun c ->
            let s = c.Signal 1 0
            let h = c.Signal 2 0
            let k = c.Signal 3 0
            let a = c.Memo 4 (fun () -> s.Value)

            let m =
                c.Memo 5 (fun () ->
                    k.Value |> ignore
                    a.Value)

            let x =
                c.Memo 6 (fun () ->
                    h.Value |> ignore
                    m.Value |> ignore
                    let v = a.Value
                    c.Log 6 v
                    v / 2)

            c.Effect 7 (fun () -> c.Log 7 x.Value) |> ignore

            for i in 1..4 do
                c.Batch (fun () ->
                    h.Value <- i
                    k.Value <- i
                    s.Value <- i)

        // #290, the test lateLinkConverges at 2e8f9690. A memo invalidates itself until it reaches a fixpoint, and an
        // effect reads it for the first time. The effect is told each time the memo ends a run stale.
        "S04",
        fun c ->
            let s = c.Signal 1 0
            let wants = c.Signal 2 0

            let m =
                c.Memo 3 (fun () ->
                    let v = s.Value

                    if v < 3 then
                        s.Value <- v + 1

                    v)

            c.Effect 4 (fun () -> if wants.Value <> 0 then c.Log 4 m.Value else c.Log 4 -1)
            |> ignore

            wants.Value <- 1
            c.HostRead 1 (fun () -> s.Value) |> ignore

        // A conditional branch flips and drops an edge. A write to the old branch wakes nothing.
        "S05",
        fun c ->
            let flag = c.Signal 1 1
            let a = c.Signal 2 10
            let b = c.Signal 3 20

            c.Effect 4 (fun () -> if flag.Value <> 0 then c.Log 4 a.Value else c.Log 4 b.Value)
            |> ignore

            flag.Value <- 0
            a.Value <- 11
            b.Value <- 21

        // Reads A, B, A. The re-run tracks A only, and reads B untracked.
        "S06",
        fun c ->
            let a = c.Signal 1 0
            let b = c.Signal 2 0

            c.Effect 3 (fun () ->
                let v = a.Value

                if v = 0 then
                    let w = b.Value
                    let u = a.Value
                    c.Log 3 (v + w + u)
                else
                    let w = c.Graph.Untrack (Func<int>(fun () -> b.Value))
                    c.Log 3 (v + w))
            |> ignore

            a.Value <- 1
            b.Value <- 5
            a.Value <- 2

        // A batch writes 0, 1, 0 under an effect that reads the signal directly.
        "S07",
        fun c ->
            let s = c.Signal 1 0
            c.Effect 2 (fun () -> c.Log 2 s.Value) |> ignore

            c.Batch (fun () ->
                s.Value <- 1
                s.Value <- 0)

        // An effect's cleanup writes its own source. The cleanup queues no run.
        "S08",
        fun c ->
            let s = c.Signal 1 0

            c.Effect 2 (fun () ->
                let v = s.Value
                c.Log 2 v
                c.Cleanup 2 (fun () -> s.Value <- v + 10))
            |> ignore

            s.Value <- 1
            s.Value <- 11

        // Review Focus 1. An effect body throws in a flush. Its status holds the failure, the effect behind it runs, and
        // the next run clears the status.
        "S09a",
        fun c ->
            let s = c.Signal 1 0
            let a = c.Effect 2 (fun () -> c.Log 2 s.Value)

            let b =
                c.Effect 3 (fun () ->
                    let v = s.Value

                    if lessThan 0 v <> 0 then
                        c.Throw 7

                    c.Log 3 v)

            c.Effect 4 (fun () -> c.Log 4 s.Value) |> ignore
            s.Value <- 1
            c.Check (statusOf b = "Error") $"the status holds the failure: {statusOf b}"
            c.Check (statusOf a = "None") $"the other effect's status is clear: {statusOf a}"
            s.Value <- 0
            c.Check (statusOf b = "None") $"the next run clears the status: {statusOf b}"

        // Review Focus 1. An effect's cleanup throws during its re-run. The failure stays out of the flush: the write
        // returns, the effect's body and the effect behind it run, and Root.Errors holds the exception.
        "S09b",
        fun c ->
            let s = c.Signal 1 0
            c.Effect 2 (fun () -> c.Log 2 s.Value) |> ignore

            let f =
                c.Effect 3 (fun () ->
                    let v = s.Value
                    c.Cleanup 3 (fun () -> c.Throw 8)
                    c.Log 3 v)

            s.Value <- 1
            c.Check (c.Graph.Root.ErrorCount = 1) $"Root.Errors after the first write: {c.Graph.Root.ErrorCount}"
            c.Check (statusOf f = "None") $"a cleanup's failure leaves the status clear: {statusOf f}"
            s.Value <- 2
            c.Check (c.Graph.Root.ErrorCount = 2) $"Root.Errors after the second write: {c.Graph.Root.ErrorCount}"

        // Review Focus 3. A body disposes its own computation and keeps reading. The run's end clears its edges, and
        // later writes run nothing of it.
        "S10",
        fun c ->
            let s = c.Signal 1 0
            let self: Effect ref = ref Unchecked.defaultof<Effect>

            // The constructor runs the first body unless a batch defers it, and the body needs its own reference.
            c.Batch (fun () ->
                self.Value <-
                    c.Effect 2 (fun () ->
                        let v = s.Value
                        c.Log 2 v
                        c.Cleanup 2 (fun () -> c.Log 2 100)
                        c.Dispose 2 (fun () -> self.Value.Dispose ()) (box self.Value)
                        c.Log 2 s.Value))

            c.Effect 3 (fun () -> c.Log 3 s.Value) |> ignore
            c.Check ((internalProp s "ObserverCount" :?> int) = 1) "only the live effect observes the signal"
            s.Value <- 1
            s.Value <- 2

        // Review Focus 2. An owning memo's cleanup writes a source and reads the memo, so a nested run replaces the run
        // the discharge makes way for.
        "S11",
        fun c ->
            let s = c.Signal 1 0
            let self: Memo<int> ref = ref Unchecked.defaultof<Memo<int>>

            let m =
                c.OwningMemo 2 (fun () ->
                    let v = s.Value

                    c.Cleanup 2 (fun () ->
                        if v = 1 then
                            s.Value <- 2

                        c.Log 2 self.Value.Value)

                    v)

            self.Value <- m
            c.Effect 3 (fun () -> c.Log 3 m.Value) |> ignore
            s.Value <- 1
            s.Value <- 5

        // Owner disposal: reverse acquisition order over cleanups and children, each cleanup once, and a registration
        // during the teardown runs at once.
        "S12",
        fun c ->
            let children = ResizeArray<Effect>()

            let p =
                c.Effect 1 (fun () ->
                    c.Cleanup 1 (fun () -> c.Log 1 1)
                    children.Add (c.Effect 2 (fun () -> c.Cleanup 2 (fun () -> c.Log 2 21)))
                    c.Cleanup 1 (fun () -> c.Log 1 2)
                    children.Add (c.Effect 3 (fun () -> c.Cleanup 3 (fun () -> c.Log 3 31)))

                    c.Cleanup 1 (fun () ->
                        c.Log 1 3
                        c.Cleanup 1 (fun () -> c.Log 1 4)))

            c.Dispose 1 (fun () -> p.Dispose ()) (box p)
            c.Dispose 1 (fun () -> p.Dispose ()) (box p)
            c.Dispose 2 (fun () -> children[0].Dispose()) (box children[0])

        // An effect writes a source it reads, climbing to a fixpoint.
        "S13",
        fun c ->
            let s = c.Signal 1 0

            c.Effect 2 (fun () ->
                let v = s.Value
                c.Log 2 v

                if v < 3 then
                    s.Value <- v + 1)
            |> ignore

        // A memo writes its own source, and an effect reads it. A run ends stale with an unchanged value.
        "S14",
        fun c ->
            let s = c.Signal 1 0

            let m =
                c.Memo 2 (fun () ->
                    let v = s.Value
                    c.Log 2 v

                    if v < 3 then
                        s.Value <- v + 1

                    0)

            c.Effect 3 (fun () -> c.Log 3 m.Value) |> ignore
            c.HostRead 2 (fun () -> m.Value) |> ignore
            c.HostRead 1 (fun () -> s.Value) |> ignore

        // The effect reads [h, x, y] and x reads [h, y]. A batch writes h and y's source.
        "S15",
        fun c ->
            let h = c.Signal 1 0
            let u = c.Signal 2 0

            let y =
                c.Memo 3 (fun () ->
                    let v = u.Value
                    c.Log 3 v
                    v)

            let x =
                c.Memo 4 (fun () ->
                    let w = h.Value
                    let v = y.Value
                    c.Log 4 (w + v)
                    w + v)

            c.Effect 5 (fun () ->
                let w = h.Value
                let v = x.Value
                let z = y.Value
                c.Log 5 (w + v + z))
            |> ignore

            c.Batch (fun () ->
                h.Value <- 1
                u.Value <- 1)

        // BUGS.md: an effect reads a memo over a signal, and its cleanup writes that signal. The mark stops at the effect
        // in its cleanups, and the body reads the written value.
        "in_cleanup_observer_stays_clean",
        fun c ->
            let s = c.Signal 1 0
            let m = c.Memo 2 (fun () -> s.Value)

            c.Effect 3 (fun () ->
                let v = m.Value
                c.Log 3 v
                c.Cleanup 3 (fun () -> s.Value <- 100))
            |> ignore

            s.Value <- 1
            c.HostRead 1 (fun () -> s.Value) |> ignore
            s.Value <- 2
            c.HostRead 2 (fun () -> m.Value) |> ignore

        // BUGS.md: an effect disposes itself, reads a memo over a signal, and writes that signal. The read links an edge
        // until the run's end, and the mark leaves the effect clean.
        "disposed_running_observer_stays_clean",
        fun c ->
            let s = c.Signal 1 0
            let m = c.Memo 2 (fun () -> s.Value)
            let started = ref false

            // The constructor runs the first body unless a batch defers it, and the body needs its own reference.
            let self: Effect ref = ref Unchecked.defaultof<Effect>

            c.Batch (fun () ->
                self.Value <-
                    c.Effect 3 (fun () ->
                        started.Value <- true
                        c.Dispose 3 (fun () -> self.Value.Dispose ()) (box self.Value)
                        let v = m.Value
                        c.Log 3 v
                        s.Value <- v + 1))

            c.Check started.Value "the effect ran"
            c.Check ((internalProp m "ObserverCount" :?> int) = 0) "the run's end cleared the effect's edge"
            s.Value <- 50
            c.HostRead 2 (fun () -> m.Value) |> ignore
    ]

let private renderOutcome =
    function
    | OkV v -> $"(Ok {v}%%Z)"
    | FailV f -> $"(Fail {f}%%Z)"

let private z (v: int) =
    if v < 0 then $"(({v})%%Z)" else $"{v}%%Z"

let private render (e: Ev) =
    match e with
    | Run n -> $"EvRun {n}"
    | Log (n, v) -> $"EvLog {n} {z v}"
    | Cleanup n -> $"EvCleanup {n}"
    | FailE (n, f) -> $"EvFail {n} {z f}"
    | Disposed n -> $"EvDispose {n}"
    | HostRead (n, o) -> $"EvHostRead {n} {renderOutcome o}"

let private definition (name: string, run: Ctx -> unit) =
    let c = Ctx ()
    run c

    let lines =
        c.Events
        |> List.map render
        |> List.chunkBySize 6
        |> List.map (String.concat "; ")
        |> String.concat ";\n  "

    $"Definition exp_{name} : list event := [\n  {lines}\n].\n"

let private body =
    scenarios
    |> List.map definition
    |> String.concat "\n"

let private text =
    "(** Generated by proofs/conformance/Scenarios.fsx from runs of the F# core. *)\n"
    + "From stdpp Require Import prelude.\n"
    + "From Stdlib Require Import ZArith.\n"
    + "From Ranvier.Base Require Import Prelude Syntax.\n\nOpen Scope nat_scope.\n\n"
    + body

let private target =
    match fsi.CommandLineArgs |> Array.toList with
    | _ :: path :: _ -> path
    | _ -> Path.Combine (__SOURCE_DIRECTORY__, "..", "theories", "Conformance", "Expected.v")

Directory.CreateDirectory (Path.GetDirectoryName (Path.GetFullPath target))
|> ignore

File.WriteAllText (target, text.Replace ("\r\n", "\n"))
printfn "Wrote %d scenarios to %s" scenarios.Length target
