namespace Ranvier

open System

/// <summary>A reusable one-shot timer, created disarmed by a monotonic clock.</summary>
/// <remarks>A callback may race with disarm or disposal; consumers must validate their current deadline.</remarks>
[<AbstractClass; AllowNullLiteral>]
type TimedTimer() =
    /// <summary>Replaces the outstanding wait with a nonnegative duration; never invokes the callback synchronously.</summary>
    abstract Arm: TimeSpan -> unit
    /// <summary>Cancels the outstanding wait.</summary>
    abstract Disarm: unit -> unit
    /// <summary>Releases the timer and cancels its outstanding wait. Repeated disposal is harmless.</summary>
    abstract Dispose: unit -> unit

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

/// <summary>A monotonic clock and timer factory for timed reactive values.</summary>
/// <remarks>Clock origins are arbitrary. An adapter with worker-thread callbacks must support concurrent timer operations.</remarks>
[<AbstractClass; AllowNullLiteral>]
type TimedClock() =
    /// <summary>Elapsed milliseconds from an arbitrary origin, advancing monotonically.</summary>
    abstract NowMilliseconds: float
    /// <summary>Creates a disarmed timer that invokes the supplied callback after an armed wait.</summary>
    /// <remarks>Callback exceptions follow the host's dispatcher and timer exception handling.</remarks>
    abstract CreateTimer: Action -> TimedTimer

module private DeadlineTimerHelpers =
    let inline nativeDue (remaining: float) =
        min 2147483647. (Math.Ceiling remaining)

type private DeadlineTimer private (now: unit -> float, callback: Action) =
    inherit TimedTimer()
    let gate = obj ()
    let mutable active = false
    let mutable disposed = false
    let mutable deadline = 0.
    let mutable arm = Unchecked.defaultof<float -> unit>
    let mutable disarm = Unchecked.defaultof<unit -> unit>
    let mutable dispose = Unchecked.defaultof<unit -> unit>

    member private this.Init(create: Action -> (float -> unit) * (unit -> unit) * (unit -> unit)) =
        let a, d, disp = create (Action this.Fire)
        arm <- a
        disarm <- d
        dispose <- disp

    static member Create(now: unit -> float, create: Action -> (float -> unit) * (unit -> unit) * (unit -> unit), callback: Action) : TimedTimer =
        let timer = new DeadlineTimer (now, callback)
        timer.Init create
        timer :> TimedTimer

    member private _.Fire() =
        let deliver =
            lock gate (fun () ->
                if disposed || not active then
                    false
                else
                    let remaining = deadline - now ()

                    if remaining > 0. then
                        arm (DeadlineTimerHelpers.nativeDue remaining)
                        false
                    else
                        active <- false
                        true)

        if deliver then
            callback.Invoke ()

    override _.Arm(delay) =
        if delay < TimeSpan.Zero then
            raise (ArgumentOutOfRangeException (nameof delay))

        lock gate (fun () ->
            if disposed then
                raise (ObjectDisposedException (nameof TimedTimer))

            deadline <- now () + delay.TotalMilliseconds
            active <- true
            arm (DeadlineTimerHelpers.nativeDue delay.TotalMilliseconds))

    override _.Disarm() =
        lock gate (fun () ->
            if not disposed then
                active <- false
                disarm ())

    override _.Dispose() =
        lock gate (fun () ->
            if not disposed then
                disposed <- true
                active <- false
                dispose ())

#if !FABLE_COMPILER
module private SystemClockHelpers =
    let inline nowMilliseconds () =
        float (Diagnostics.Stopwatch.GetTimestamp ())
        * 1000.
        / float Diagnostics.Stopwatch.Frequency

    let inline nativeDue (remaining: float) =
        int (min 2147483647. (Math.Ceiling remaining))

type private SystemDeadlineTimer internal (callback: Action) =
    inherit TimedTimer()
    let mutable active = false
    let mutable disposed = false
    let mutable deadline = 0.
    let mutable timer: Threading.Timer = null

    member internal this.Init(cb: Threading.TimerCallback) =
        timer <- new Threading.Timer (cb, this, Threading.Timeout.Infinite, Threading.Timeout.Infinite)

    member internal this.Fire() =
        let deliver =
            lock this (fun () ->
                if disposed || not active then
                    false
                else
                    let remaining = deadline - SystemClockHelpers.nowMilliseconds ()

                    if remaining > 0. then
                        timer.Change (SystemClockHelpers.nativeDue remaining, Threading.Timeout.Infinite)
                        |> ignore

                        false
                    else
                        active <- false
                        true)

        if deliver then
            callback.Invoke ()

    override this.Arm(delay) =
        if delay < TimeSpan.Zero then
            raise (ArgumentOutOfRangeException (nameof delay))

        lock this (fun () ->
            if disposed then
                raise (ObjectDisposedException (nameof TimedTimer))

            deadline <-
                SystemClockHelpers.nowMilliseconds ()
                + delay.TotalMilliseconds

            active <- true

            timer.Change (SystemClockHelpers.nativeDue delay.TotalMilliseconds, Threading.Timeout.Infinite)
            |> ignore)

    override this.Disarm() =
        lock this (fun () ->
            if not disposed then
                active <- false

                timer.Change (Threading.Timeout.Infinite, Threading.Timeout.Infinite)
                |> ignore)

    override this.Dispose() =
        lock this (fun () ->
            if not disposed then
                disposed <- true
                active <- false
                timer.Dispose ())

module private SystemDeadlineTimerFactory =
    let private timerCallback =
        Threading.TimerCallback (fun state -> (state :?> SystemDeadlineTimer).Fire())

    let create (callback: Action) : TimedTimer =
        let timer = new SystemDeadlineTimer (callback)
        timer.Init timerCallback
        timer :> TimedTimer
#endif

/// <summary>System and .NET time-provider adapters for timed values.</summary>
[<RequireQualifiedAccess>]
module TimedClock =
    let private systemClock =
        lazy
            { new TimedClock() with
                member _.NowMilliseconds =
#if FABLE_COMPILER
                    Fable.Core.JsInterop.emitJsExpr () "performance.now()"
#else
                    SystemClockHelpers.nowMilliseconds ()
#endif
                member this.CreateTimer(callback) =
                    if Platform.isNull callback then
                        nullArg (nameof callback)

#if FABLE_COMPILER
                    let create (fire: Action) =
                        let mutable handle: obj = null

                        let stop () =
                            if not (Platform.isNull handle) then
                                Fable.Core.JsInterop.emitJsExpr handle "clearTimeout($0)"
                                handle <- null

                        let start delay =
                            stop ()
                            handle <- Fable.Core.JsInterop.emitJsExpr (fire, delay) "setTimeout($0, $1)"

                        start, stop, stop

                    DeadlineTimer.Create ((fun () -> this.NowMilliseconds), create, callback)
#else
                    SystemDeadlineTimerFactory.create callback
#endif
            }

    /// <summary>A reusable system clock using monotonic timestamps and one-shot native timers.</summary>
    /// <remarks>Long waits are chunked; positive fractional milliseconds round up.</remarks>
    let system = systemClock.Value

#if NET8_0_OR_GREATER && !FABLE_COMPILER
    /// <summary>Uses a .NET time provider's timestamps and timers with the same deadline and rounding rules as the system clock.</summary>
    /// <exception cref="T:System.ArgumentNullException"><c>provider</c> is null.</exception>
    let ofTimeProvider (provider: TimeProvider) =
        if Platform.isNull provider then
            nullArg (nameof provider)

        { new TimedClock() with
            member _.NowMilliseconds =
                float (provider.GetTimestamp ()) * 1000.
                / float provider.TimestampFrequency

            member this.CreateTimer(callback) =
                if Platform.isNull callback then
                    nullArg (nameof callback)

                let create (fire: Action) =
                    let timer =
                        provider.CreateTimer (
                            Threading.TimerCallback (fun _ -> fire.Invoke ()),
                            null,
                            Threading.Timeout.InfiniteTimeSpan,
                            Threading.Timeout.InfiniteTimeSpan
                        )

                    (fun (delay: float) ->
                        timer.Change (TimeSpan.FromMilliseconds delay, Threading.Timeout.InfiniteTimeSpan)
                        |> ignore),
                    (fun () ->
                        timer.Change (Threading.Timeout.InfiniteTimeSpan, Threading.Timeout.InfiniteTimeSpan)
                        |> ignore),
                    timer.Dispose

                DeadlineTimer.Create ((fun () -> this.NowMilliseconds), create, callback)
        }
#endif
