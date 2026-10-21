namespace Ranvier.View.Twin

open System
open System.Collections.Generic

type ClockPending =
    {
        Microtasks: int
        Timers: int
        Frames: int
        NextTimerAt: float option
    }

/// <summary>A queue that kept refilling past <c>Settle</c>'s round limit; <c>origin</c> names its last callback.</summary>
exception TwinRunawayException of queue: string * origin: string with
    override this.Message =
        $"The twin's {this.queue} kept refilling; the last callback was {this.origin}"

type private Timer =
    {
        Id: int
        Due: float
        Sequence: int
        Callback: unit -> unit
    }

/// <summary>The twin's event loop: microtasks, timers and animation frames, run only when the test asks.</summary>
[<Sealed>]
type TwinClock() as this =
    let microtasks = Queue<unit -> unit>()
    let timers = ResizeArray<Timer>()
    let frames = ResizeArray<int * (float -> unit)>()
    let mutable now = 0.0
    let mutable sequence = 0
    let mutable lastOrigin = ""

    let origin (callback: obj) =
        callback.GetType().FullName

    let provider =
        { new TimeProvider() with
            override _.GetUtcNow() =
                DateTimeOffset.UnixEpoch.AddMilliseconds now

            override _.TimestampFrequency = 10_000_000L

            override _.GetTimestamp() =
                int64 (now * 10_000.0)

            override _.CreateTimer(callback, state, dueTime, period) =
                let mutable id = -1
                let mutable periodMs = -1.0

                let rec schedule (delay: float) =
                    id <-
                        this.SetTimeout (
                            (fun () ->
                                callback.Invoke state

                                if periodMs > 0.0 then
                                    schedule periodMs),
                            delay
                        )

                let change (due: TimeSpan) (every: TimeSpan) =
                    if id >= 0 then
                        this.ClearTimeout id

                    id <- -1

                    periodMs <-
                        (if every = Threading.Timeout.InfiniteTimeSpan then
                             -1.0
                         else
                             every.TotalMilliseconds)

                    if due <> Threading.Timeout.InfiniteTimeSpan then
                        schedule due.TotalMilliseconds

                    true

                change dueTime period |> ignore

                { new Threading.ITimer with
                    member _.Change(due, every) =
                        change due every

                    member _.Dispose() =
                        if id >= 0 then
                            this.ClearTimeout id

                    member _.DisposeAsync() =
                        (if id >= 0 then
                             this.ClearTimeout id)

                        System.Threading.Tasks.ValueTask ()
                }
        }

    member _.Now = now
    member _.TimeProvider: TimeProvider = provider
    member val FrameMs = 16.0 with get, set

    member _.Post(callback: unit -> unit) =
        microtasks.Enqueue callback

    member this.RunMicrotasks() : int =
        this.Drain System.Int32.MaxValue

    /// <summary>Runs queued microtasks, including those they queue, until none remain or <c>limit</c> have run.</summary>
    member internal _.Drain(limit: int) : int =
        let mutable count = 0

        while microtasks.Count > 0 && count < limit do
            let callback = microtasks.Dequeue ()
            lastOrigin <- origin callback
            callback ()
            count <- count + 1

        count

    member _.SetTimeout(callback: unit -> unit, milliseconds: float) : int =
        sequence <- sequence + 1

        timers.Add
            {
                Id = sequence
                Due = now + max 0.0 milliseconds
                Sequence = sequence
                Callback = callback
            }

        sequence

    member _.ClearTimeout(id: int) =
        timers.RemoveAll (fun t -> t.Id = id) |> ignore

    member _.RequestAnimationFrame(callback: float -> unit) : int =
        sequence <- sequence + 1
        frames.Add ((sequence, callback))
        sequence

    member _.CancelAnimationFrame(id: int) =
        frames.RemoveAll (fun (i, _) -> i = id) |> ignore

    member private this.RunTimer(timer: Timer) =
        timers.Remove timer |> ignore
        now <- max now timer.Due
        lastOrigin <- origin timer.Callback
        timer.Callback ()
        this.RunMicrotasks () |> ignore

    member this.Frame() =
        let due = List.ofSeq frames
        frames.Clear ()

        for _, callback in due do
            lastOrigin <- origin callback
            callback now
            this.RunMicrotasks () |> ignore

    member this.AdvanceBy(milliseconds: float) =
        let target = now + milliseconds
        let mutable going = true

        while going do
            let timer =
                timers
                |> Seq.filter (fun t -> t.Due <= target)
                |> Seq.sortBy (fun t -> t.Due, t.Sequence)
                |> Seq.tryHead

            let frame =
                if frames.Count = 0 then
                    None
                else
                    let boundary = (floor (now / this.FrameMs) + 1.0) * this.FrameMs
                    if boundary <= target then Some boundary else None

            match timer, frame with
            | Some t, Some f when f < t.Due ->
                now <- f
                this.Frame ()
            | Some t, _ -> this.RunTimer t
            | None, Some f ->
                now <- f
                this.Frame ()
            | None, None -> going <- false

        now <- target

    member _.Pending: ClockPending =
        {
            Microtasks = microtasks.Count
            Timers = timers.Count
            Frames = frames.Count
            NextTimerAt =
                if timers.Count = 0 then
                    None
                else
                    Some (timers |> Seq.map (fun t -> t.Due) |> Seq.min)
        }

    /// <summary>Pumps the graph's inbox, drains microtasks and runs due timers until all three are empty.</summary>
    member this.Settle(graph: Ranvier.Graph, ?maxRounds: int) =
        let limit = defaultArg maxRounds 10_000
        let counts = Dictionary<string, int>()

        let bump name n =
            counts[name] <-
                (match counts.TryGetValue name with
                 | true, c -> c
                 | _ -> 0)
                + n

        let mutable rounds = 0
        let mutable busy = true

        while busy do
            let pumped = graph.Pump ()
            let ran = this.Drain limit

            let due =
                timers
                |> Seq.filter (fun t -> t.Due <= now)
                |> Seq.sortBy (fun t -> t.Due, t.Sequence)
                |> List.ofSeq

            for timer in due do
                this.RunTimer timer

            bump "inbox" pumped
            bump "microtasks" ran
            bump "timers" due.Length
            busy <- pumped + ran + due.Length > 0
            rounds <- rounds + 1

            if busy && rounds >= limit then
                let busiest = counts |> Seq.maxBy (fun pair -> pair.Value)
                raise (TwinRunawayException (busiest.Key, lastOrigin))
