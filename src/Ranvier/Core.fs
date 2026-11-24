namespace Ranvier

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks

#if FABLE_COMPILER
module private FableLocks =
    let inline lock (_target: obj) ([<InlineIfLambda>] action: unit -> 'T) : 'T = action ()

open FableLocks
#endif

/// <summary>
/// Dirtiness — the axis every reactive graph already has. Deliberately separate
/// from <c>Status</c>, which carries pending and error. Solid 2's contribution is
/// that the second axis exists and propagates; collapsing them into one ordered
/// enum is what stops every prior implementation short.
/// </summary>
/// <remarks>
/// An enum rather than a union, and measured rather than assumed: <c>=</c> on a
/// three-case struct DU goes through a generated <c>Equals</c> that does not inline,
/// and a clean tracked read — the most travelled path in the library — pays
/// that comparison on every read. Nothing pattern-matches on this type, so the
/// exhaustiveness <c>WarningsAsErrors 0025</c> would buy is not being given up.
/// </remarks>
type internal Freshness =
    | Clean = 0uy
    /// <summary>
    /// Maybe-dirty. Something above this node changed, but not necessarily in a
    /// way that moved the value it reads. Solid's <c>STATE_CHECK</c>.
    /// </summary>
    /// <remarks>
    /// This is what makes a cutoff work below the first derivation. Dirtiness
    /// has to propagate on the write, before any memo has recomputed and so
    /// before anyone knows whether values moved; without a third state the only
    /// thing a transitive dependent can be told is "dirty", and it re-runs
    /// whether or not anything it reads actually changed. <c>Check</c> is what it is
    /// told instead: ask your sources before you run.
    /// </remarks>
    | Check = 1uy
    | Dirty = 2uy

module internal Staleness =
    /// <summary><c>Status.Stale</c> when the node is not <c>Clean</c>.</summary>
    let inline ofFreshness (freshness: Freshness) =
        if freshness = Freshness.Clean then
            Status.None
        else
            Status.Stale

/// <summary>
/// A node that can be depended upon (AKA IObservable).
/// </summary>
/// <remarks>
/// Each edge has a handle: the observer's position in the source's observer list. The observer stores the handle
/// beside its own record of the edge, at a position of its choosing called the slot, and removes the edge by handle.
/// A swap-remove that moves an observer calls its <c>MovedIn</c> with the slot and the new handle.
/// </remarks>
type internal ISource =
    inherit INode
    /// <summary>
    /// Links an observer the caller holds no edge for, and returns the edge's handle. <c>MovedIn</c> receives
    /// <c>slot</c> back.
    /// </summary>
    abstract AddObserver: observer: IComputation * slot: int -> int

    /// <summary>
    /// <c>AddObserver</c> for an observer that may already be linked. Returns <c>EdgeHandle.None</c>, linking
    /// nothing, when the observer is linked, and <c>EdgeHandle.Unknown</c>, linking nothing, when the answer would
    /// take a scan of more than <c>EdgeHandle.ScanLimit</c> observers.
    /// </summary>
    abstract TryAddObserver: observer: IComputation * slot: int -> int

    /// <summary>
    /// Unlinks the observer at <c>handle</c>. <c>EdgeHandle.None</c> does nothing. Raises
    /// <c>InvalidOperationException</c> when another observer holds the handle. The caller then stores
    /// <c>EdgeHandle.None</c> as the handle.
    /// </summary>
    abstract RemoveObserverAt: observer: IComputation * handle: int -> unit

    /// <summary>
    /// Brings this source up to date if it is stale, so an observer resolving
    /// its own <c>Check</c> gets a definite answer. A no-op on sources that cannot
    /// be stale — a signal's value is whatever was last written to it.
    /// </summary>
    /// <remarks>
    /// A source that recomputes to a different value marks its observers dirty on the way out;
    /// a source whose value did not move says nothing, and leaves the asker in <c>Check</c>.
    /// </remarks>
    abstract UpdateIfNecessary: unit -> unit

    /// <summary>
    /// Whether this source is live and not <c>Clean</c>.
    /// </summary>
    abstract IsStale: bool

    /// <summary>
    /// Whether this source is currently running its body or resolving its own <c>Check</c>.
    /// </summary>
    abstract IsActive: bool

/// <summary>
/// A node that depends on sources, and therefore can be invalidated.
/// </summary>
and internal IComputation =
    inherit INode
    /// <summary>
    /// Noop if already <c>Dirty</c>.
    /// </summary>
    abstract MarkDirty: unit -> unit

    /// <summary>
    /// A <b>source</b> of this node may have changed. Does nothing if this node is
    /// already <c>Check</c> or <c>Dirty</c>: <c>Dirty</c> is strictly stronger information and
    /// must never be lost to a later <c>Check</c>.
    /// </summary>
    abstract MarkCheck: unit -> unit
    abstract AddSource: ISource -> unit

    /// <summary>
    /// The edge this node recorded at <c>slot</c> now has <c>handle</c>. Called by a source's swap-remove, possibly
    /// during a notification walk.
    /// </summary>
    abstract MovedIn: slot: int * handle: int -> unit

    /// <summary>
    /// Whether this computation's current run has already read past <c>slot</c>.
    /// </summary>
    abstract HasReadSlot: slot: int -> bool

/// <summary>
/// Something the scheduler can run. Memos are pull-based and never need this —
/// they recompute when read. Effects are the push side: nothing reads them, so
/// something has to run them.
/// </summary>
and internal IScheduled =
    abstract Execute: unit -> unit

/// <summary>
/// The nodes at both ends of the edge being linked, handed across <c>ISource.AddObserver</c> without changing it.
/// </summary>
/// <remarks>
/// <para>
/// An edge records the <c>Node</c> of a memo, effect or split effect beside its interface reference, so marks, checks
/// and the source walk call the node's operations directly. The interfaces stay as they are, and so does the public
/// surface: a source list that links an edge names its own node in <c>Observer</c>, and a memo answering
/// <c>AddObserver</c> names its node in <c>Source</c>. Each reader checks the node against the object it was handed,
/// so a value left by a nested link is never taken for another object's node.
/// </para>
/// <para>Typed <c>obj</c> because <c>Node</c> is declared after the edge lists. Touched only when an edge is linked.</para>
/// </remarks>
[<AbstractClass; Sealed>]
type internal EdgeContext =
    [<ThreadStatic; DefaultValue>]
    static val mutable private observer: obj

    [<ThreadStatic; DefaultValue>]
    static val mutable private source: obj

    [<ThreadStatic; DefaultValue>]
    static val mutable private sourceOf: obj

    /// <summary>The node of the computation linking an edge, or null.</summary>
    static member Observer
        with get () = EdgeContext.observer
        and set (value: obj) = EdgeContext.observer <- value

    /// <summary>The node of the memo that last answered <c>AddObserver</c>, or null.</summary>
    static member Source = EdgeContext.source

    /// <summary>
    /// Names <c>node</c> as the node of <c>memo</c>, which is answering <c>AddObserver</c>: only for a node's source list,
    /// which takes the name and clears it, so nothing outlives the link.
    /// </summary>
    static member NameSource(memo: obj, node: obj) =
        if not (Platform.isNull EdgeContext.observer) then
            EdgeContext.sourceOf <- memo
            EdgeContext.source <- node

    /// <summary>Drops every reference the hand-over holds.</summary>
    static member Clear() =
        EdgeContext.observer <- null
        EdgeContext.source <- null
        EdgeContext.sourceOf <- null

    /// <summary>The named node when it belongs to <c>source</c>, otherwise null; and clears the hand-over.</summary>
    static member TakeSource(source: obj) : obj =
        let node = EdgeContext.source
        let owner = EdgeContext.sourceOf
        EdgeContext.source <- null
        EdgeContext.sourceOf <- null

        if obj.ReferenceEquals (owner, source) then node else null

/// <summary>The kind of computation a <c>Node</c> belongs to, which selects its mark and run behaviour.</summary>
module internal NodeKind =
    [<Literal>]
    let Memo = 0uy

    [<Literal>]
    let Effect = 1uy

    [<Literal>]
    let EffectOn = 2uy

/// <summary>
/// Edge handle values that name no position, and the scan bound shared by both ends of an edge.
/// </summary>
module internal EdgeHandle =
    /// <summary>The handle of a missing edge, and of a slot that shares an earlier slot's edge.</summary>
    [<Literal>]
    let Unlinked = -1

    /// <summary>The <c>TryAddObserver</c> result that leaves the link to the caller.</summary>
    [<Literal>]
    let Unknown = -2

    /// <summary>The longest list either end of an edge scans for a repeated link.</summary>
    [<Literal>]
    let ScanLimit = 8

    /// <summary>
    /// Bit 29 marks an edge handle returned by a <c>Signal</c> so <c>ResolveCheck</c> skips <c>UpdateIfNecessary</c>.
    /// Bit 29 rather than 30 keeps a tagged handle a 31-bit small integer on pointer-compressed V8.
    /// </summary>
    [<Literal>]
    let SignalBit = 0x20000000

    /// <summary>Masks off <c>SignalBit</c> to recover the observer-set slot index.</summary>
    [<Literal>]
    let IndexMask = 0x1FFFFFFF

    /// <summary>True when <c>handle</c> names an edge owned by a <c>Signal</c>.</summary>
    let inline isSignal (handle: int) =
        handle >= SignalBit

/// <summary>
/// Single-bitmask predicates over <see cref="Status"/> so hot reads inspect the status byte without calling <c>Enum.HasFlag</c>.
/// </summary>
module internal StatusBits =
    let inline isUnready (status: Status) =
        (status &&& (Status.Pending ||| Status.Error))
        <> Status.None

    let inline isPending (status: Status) =
        (status &&& Status.Pending) <> Status.None

    let inline isError (status: Status) =
        (status &&& Status.Error) <> Status.None

#if FABLE_COMPILER
    let inline isCleanAndReady (freshness: Freshness) (status: Status) =
        ((int freshness) ||| (int status &&& 3)) = 0
#else
    let inline isCleanAndReady (freshness: Freshness) (status: Status) =
        ((byte freshness) ||| (byte status &&& 3uy)) = 0uy
#endif

/// <summary>
/// One slot in an observer list.
/// </summary>
/// <remarks>
/// On .NET, a struct slot holds the observer and its slot, and avoids covariant interface-array store checks.
/// Under Fable, the slot is the observer reference itself, and the observer's slot is kept in a parallel array.
/// Replace whole slots when updating the array.
/// </remarks>
#if FABLE_COMPILER
[<Fable.Core.Erase; NoEquality; NoComparison>]
type internal ObserverSlot =
    | ObserverSlot of IComputation

    member inline this.Observer =
        let (ObserverSlot observer) = this
        observer
#else
[<Struct; NoEquality; NoComparison>]
type internal ObserverSlot =
    val mutable Observer: IComputation

    /// <summary>The position the observer recorded the edge at, passed back in <c>MovedIn</c>.</summary>
    val mutable Slot: int

    new(observer: IComputation, slot: int) = { Observer = observer; Slot = slot }
#endif

module internal ObserverSlots =
    /// <summary>
    /// The zero-length list an observer set holds until its first observer.
    /// </summary>
#if FABLE_COMPILER
    let none: ObserverSlot[] = [||]
#else
    let inline none () =
        Array.Empty<ObserverSlot>()
#endif

#if FABLE_COMPILER
    /// <summary>
    /// The zero-length slot list an observer set holds until its first observer.
    /// </summary>
    let noSlots: int[] = Platform.positions 0
#endif

/// <summary>
/// The observers of one source, and the only safe way to notify them.
/// </summary>
/// <remarks>
/// An array rather than a <c>HashSet</c>, because notification is the hot half and
/// a set cannot be walked while it is mutated.
/// <para>
/// Each observer is held at most once. <c>Add</c> appends unconditionally; <c>TryAdd</c> answers a repeat from the
/// latest add's handle or from a scan of a list of at most <c>EdgeHandle.ScanLimit</c>. A position is an edge's handle,
/// and removal goes by handle, checked against the observer.
/// </para>
/// </remarks>
type internal ObserverSet() =
    let mutable first: IComputation = Unchecked.defaultof<IComputation>
    let mutable firstSlot = EdgeHandle.Unlinked
#if FABLE_COMPILER
    let mutable rest: ObserverSlot[] = ObserverSlots.none

    /// <summary>
    /// The slot of each overflow observer beyond slot 0. Grown and blanked together with <c>rest</c>, and never read past <c>count - 1</c>.
    /// </summary>
    let mutable restSlots: int[] = ObserverSlots.noSlots
#else
    let mutable rest: ObserverSlot[] = ObserverSlots.none ()
#endif
    let mutable count = 0

    /// <summary>
    /// The handle the latest add returned, moved along when a swap-remove moves its observer. Holds no reference, so
    /// a hit is checked against the observer at the handle.
    /// </summary>
    let mutable latest = 0

#if RANVIER_TRACE
    let mutable traceLog: TraceLog = null
    let mutable traceId = 0
#endif

    /// <summary>
    /// Doubles the capacity of the overflow arrays once a source gains a second observer.
    /// </summary>
    let growRest (restCount: int) =
        let size = max 4 (restCount * 2)
#if FABLE_COMPILER
        let grownObservers = Array.zeroCreate size
        let grownSlots = Platform.positions size
        Array.blit rest 0 grownObservers 0 restCount
        Platform.copyPositions restSlots grownSlots restCount
        rest <- grownObservers
        restSlots <- grownSlots
#else
        let grown = Array.zeroCreate size
        Array.blit rest 0 grown 0 restCount
        rest <- grown
#endif

    /// <summary>A new, empty set: the same call as the inline struct list's.</summary>
    static member inline Create() = ObserverSet ()

    /// <summary>
    /// How many observers this source has. Diagnostic: duplication in here is
    /// invisible in what the graph computes — a second dirty mark on a dirty
    /// node is a no-op — so nothing short of counting can assert against it.
    /// </summary>
    member _.Count = count

    /// <summary>
    /// Appends <c>c</c>, recording <c>slot</c> for its <c>MovedIn</c>, and returns its handle.
    /// </summary>
    member this.Add(c: IComputation, slot: int) =
        let handle = count

        if handle = 0 then
            first <- c
            firstSlot <- slot
            count <- 1
            latest <- 0
        else
            let restIdx = handle - 1

            if restIdx = rest.Length then
                growRest restIdx

#if FABLE_COMPILER
            Platform.setItemAt restIdx rest (ObserverSlot c)
            Platform.setItemAt restIdx restSlots slot
#else
            rest[restIdx] <- ObserverSlot (c, slot)
#endif
            count <- handle + 1
            latest <- handle
#if RANVIER_COUNTERS
        Counters.ObserverInserted ()
#endif
        Tracer.ObserverAdd (this, c)
        handle

    /// <summary>
    /// <c>Add</c> unless <c>c</c> is held: <c>EdgeHandle.Unlinked</c> when it is, and <c>EdgeHandle.Unknown</c>, adding
    /// nothing, when the latest add's handle misses on a list longer than <c>EdgeHandle.ScanLimit</c>.
    /// </summary>
    member this.TryAdd(c: IComputation, slot: int) =
        if count = 0 then
            this.Add (c, slot)
        elif
            latest < count
            && obj.ReferenceEquals (
                (if latest = 0 then
                     first
                 else
                     (Platform.itemAt (latest - 1) rest).Observer),
                c
            )
        then
            EdgeHandle.Unlinked
        elif count = 1 then
            if obj.ReferenceEquals (first, c) then
                EdgeHandle.Unlinked
            else
                this.Add (c, slot)
        elif count <= EdgeHandle.ScanLimit then
            if obj.ReferenceEquals (first, c) then
                EdgeHandle.Unlinked
            else
                let restCount = count - 1
                let mutable i = 0

                while i < restCount
                      && not (obj.ReferenceEquals ((Platform.itemAt i rest).Observer, c)) do
                    i <- i + 1

                if i < restCount then
                    EdgeHandle.Unlinked
                else
                    this.Add (c, slot)
        else
            EdgeHandle.Unknown

    /// <summary>
    /// Swap-removes the observer at <c>handle</c>: the last observer fills the hole and is told its new handle, so
    /// order is not preserved. Nothing here depends on order — a dirty mark is a set operation — and preserving it
    /// would cost a shift on the path a conditional body takes every time it drops a branch.
    /// </summary>
    /// <remarks>
    /// <c>EdgeHandle.Unlinked</c> does nothing. A handle holding another observer raises
    /// <c>InvalidOperationException</c>, in every build.
    /// </remarks>
    member this.RemoveAt(c: IComputation, taggedHandle: int) =
        if taggedHandle >= 0 then
            let handle = taggedHandle &&& EdgeHandle.IndexMask

            if
                handle >= count
                || not (
                    if handle = 0 then
                        obj.ReferenceEquals (first, c)
                    else
                        obj.ReferenceEquals ((Platform.itemAt (handle - 1) rest).Observer, c)
                )
            then
                raise (
                    InvalidOperationException (
                        "Edge handle "
                        + string handle
                        + " of "
                        + string count
                        + " does not hold the observer removed."
                    )
                )

#if RANVIER_COUNTERS
            Counters.ObserverRemoved ()
#endif
            Tracer.ObserverRemove (this, c)
            let last = count - 1

            if handle = 0 then
                if last = 0 then
                    // Blanked so a dropped observer is not kept alive after removal.
                    first <- Unchecked.defaultof<IComputation>
                    firstSlot <- EdgeHandle.Unlinked
                    count <- 0
                else
                    let lastRest = last - 1
                    let moved = (Platform.itemAt lastRest rest).Observer
#if FABLE_COMPILER
                    let movedSlot = Platform.itemAt lastRest restSlots
                    Platform.setItemAt lastRest rest (ObserverSlot Unchecked.defaultof<IComputation>)
                    Platform.setItemAt lastRest restSlots EdgeHandle.Unlinked
#else
                    let movedSlot = (Platform.itemAt lastRest rest).Slot
                    rest[lastRest] <- ObserverSlot (Unchecked.defaultof<IComputation>, EdgeHandle.Unlinked)
#endif
                    first <- moved
                    firstSlot <- movedSlot
                    count <- last

                    if latest = last then
                        latest <- 0

                    moved.MovedIn (movedSlot, taggedHandle &&& EdgeHandle.SignalBit)
            elif handle = last then
                let lastRest = last - 1
#if FABLE_COMPILER
                Platform.setItemAt lastRest rest (ObserverSlot Unchecked.defaultof<IComputation>)
                Platform.setItemAt lastRest restSlots EdgeHandle.Unlinked
#else
                rest[lastRest] <- ObserverSlot (Unchecked.defaultof<IComputation>, EdgeHandle.Unlinked)
#endif
                count <- last
            else
                let handleRest = handle - 1
                let lastRest = last - 1
                let moved = (Platform.itemAt lastRest rest).Observer
#if FABLE_COMPILER
                let movedSlot = Platform.itemAt lastRest restSlots
                Platform.setItemAt handleRest rest (ObserverSlot moved)
                Platform.setItemAt handleRest restSlots movedSlot
                Platform.setItemAt lastRest rest (ObserverSlot Unchecked.defaultof<IComputation>)
                Platform.setItemAt lastRest restSlots EdgeHandle.Unlinked
#else
                let movedSlot = (Platform.itemAt lastRest rest).Slot
                rest[handleRest] <- ObserverSlot (moved, movedSlot)
                rest[lastRest] <- ObserverSlot (Unchecked.defaultof<IComputation>, EdgeHandle.Unlinked)
#endif
                count <- last

                if latest = last then
                    latest <- handle

                moved.MovedIn (movedSlot, handle ||| (taggedHandle &&& EdgeHandle.SignalBit))

    /// <summary>
    /// Marks every current observer dirty. Observers added during the loop
    /// are not notified: they were linked by a computation that has already
    /// re-read this source, so they are current by construction.
    /// </summary>
    /// <remarks>
    /// Walked backwards, and re-clamped each step, because <c>MarkDirty</c> may
    /// remove observers — including this one — from underneath the walk. A
    /// removal at or below the cursor moves an already-visited observer down
    /// into the hole, so nothing unvisited is skipped; the moved observer is
    /// visited twice, which is free because <c>MarkDirty</c> on an already-dirty
    /// node returns immediately. Forwards, the same removal would skip.
    /// </remarks>
    member this.NotifyDirty() =
        if count = 1 then
            let observer = first
            Tracer.Mark (this, observer, true)
            observer.MarkDirty ()
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= count then
                    i <- count - 1

                if i > 0 then
                    let observer = (Platform.itemAt (i - 1) rest).Observer
                    Tracer.Mark (this, observer, true)
                    observer.MarkDirty ()
                    i <- i - 1

            if count > 0 then
                let observer = first
                Tracer.Mark (this, observer, true)
                observer.MarkDirty ()

    /// <summary>
    /// <c>NotifyDirty</c>, skipping <c>running</c> when its current run has not yet read its edge to this source.
    /// </summary>
    /// <remarks>
    /// The exception is the computation whose body is running right now when it
    /// is the direct reader of this source: it has not yet advanced its read
    /// cursor past this source's slot, and it receives the new value from that
    /// very read, so marking it dirty would only make it re-run on a value it
    /// already has. When <c>running</c> has already read this source earlier in
    /// its current run — or is resolving a <c>Check</c> walk over a later source
    /// whose recompute moved this already-read source — its cursor sits past
    /// the slot and it must be marked <c>Dirty</c>.
    /// </remarks>
    member this.NotifyDirtyExcept(running: IComputation) =
        if count = 1 then
            let observer = first

            if
                not (obj.ReferenceEquals (observer, running))
                || running.HasReadSlot firstSlot
            then
                Tracer.Mark (this, observer, true)
                observer.MarkDirty ()
            else
                Tracer.MarkSkip (this, observer)
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= count then
                    i <- count - 1

                if i > 0 then
#if FABLE_COMPILER
                    let idx = i - 1
                    let observer = (Platform.itemAt idx rest).Observer

                    if
                        not (obj.ReferenceEquals (observer, running))
                        || running.HasReadSlot (Platform.itemAt idx restSlots)
                    then
#else
                    let item = Platform.itemAt (i - 1) rest
                    let observer = item.Observer

                    if
                        not (obj.ReferenceEquals (observer, running))
                        || running.HasReadSlot item.Slot
                    then
#endif
                        Tracer.Mark (this, observer, true)
                        observer.MarkDirty ()
                    else
                        Tracer.MarkSkip (this, observer)

                    i <- i - 1

            if count > 0 then
                let observer = first

                if
                    not (obj.ReferenceEquals (observer, running))
                    || running.HasReadSlot firstSlot
                then
                    Tracer.Mark (this, observer, true)
                    observer.MarkDirty ()
                else
                    Tracer.MarkSkip (this, observer)

    /// <summary>
    /// <c>NotifyDirty</c>, unconditionally skipping <c>running</c>.
    /// </summary>
    member this.NotifyDirtyOther(running: IComputation) =
        if count = 1 then
            let observer = first

            if not (obj.ReferenceEquals (observer, running)) then
                Tracer.Mark (this, observer, true)
                observer.MarkDirty ()
            else
                Tracer.MarkSkip (this, observer)
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= count then
                    i <- count - 1

                if i > 0 then
                    let observer = (Platform.itemAt (i - 1) rest).Observer

                    if not (obj.ReferenceEquals (observer, running)) then
                        Tracer.Mark (this, observer, true)
                        observer.MarkDirty ()
                    else
                        Tracer.MarkSkip (this, observer)

                    i <- i - 1

            if count > 0 then
                let observer = first

                if not (obj.ReferenceEquals (observer, running)) then
                    Tracer.Mark (this, observer, true)
                    observer.MarkDirty ()
                else
                    Tracer.MarkSkip (this, observer)

    /// <summary>
    /// <c>NotifyDirty</c>'s weaker sibling: tells every observer that something
    /// above it moved, without claiming that this node's own value did. Same
    /// backwards re-clamped walk, for the same reason.
    /// </summary>
    member this.NotifyCheck() =
        if count = 1 then
            let observer = first
            Tracer.Mark (this, observer, false)
            observer.MarkCheck ()
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= count then
                    i <- count - 1

                if i > 0 then
                    let observer = (Platform.itemAt (i - 1) rest).Observer
                    Tracer.Mark (this, observer, false)
                    observer.MarkCheck ()
                    i <- i - 1

            if count > 0 then
                let observer = first
                Tracer.Mark (this, observer, false)
                observer.MarkCheck ()

#if RANVIER_TRACE
    interface ITraced with
        member _.TraceLog
            with get () = traceLog
            and set log = traceLog <- log

        member _.TraceId
            with get () = traceId
            and set id = traceId <- id

    interface ITracedEdges with
        member _.Owner = traceId
        member _.IsSources = false

        member _.Ids =
            Array.init count (fun i ->
                if i = 0 then
                    first.Id
                else
                    (Platform.itemAt (i - 1) rest).Observer.Id)
#endif

/// <summary>
/// An edge from a source to an observer shared by many sources. Forwards marks to the observer and holds the edge's
/// handle.
/// </summary>
/// <remarks>Reports the observer's id, so a trace names the observer.</remarks>
[<Sealed>]
type internal ForwardingEdge(target: IComputation) =
    member val Handle = EdgeHandle.Unlinked with get, set

    interface INode with
        member _.Id = target.Id
        member _.Status = target.Status

    interface IComputation with
        member _.AddSource _ = ()

        member _.MarkDirty() =
            target.MarkDirty ()

        member _.MarkCheck() =
            target.MarkCheck ()

        member this.MovedIn(_, handle) =
            this.Handle <- handle

        member _.HasReadSlot _ = false

#if FABLE_COMPILER || RANVIER_TRACE
/// <summary>
/// The observers of a signal or a node: the observer set itself when traced, whose events the trace needs, and under
/// Fable. Untraced on .NET it is a struct declared with <c>Graph</c>, held inline by its owner.
/// </summary>
type internal ObserverList = ObserverSet
#endif

/// <summary>
/// One slot in a computation's dependency list, with the same target-specific representation as <c>ObserverSlot</c>:
/// the source and its edge handle on .NET, the source alone under Fable.
/// </summary>
#if FABLE_COMPILER
[<Fable.Core.Erase; NoEquality; NoComparison>]
type internal SourceSlot =
    | SourceSlot of ISource

    member inline this.Source =
        let (SourceSlot source) = this
        source
#else
[<Struct; NoEquality; NoComparison>]
type internal SourceSlot =
    val mutable Source: ISource

    /// <summary>The handle of the slot's edge, or <c>EdgeHandle.Unlinked</c> for a source held by an earlier slot.</summary>
    val mutable Handle: int

    /// <summary>The source's <c>Node</c> when it is a memo, otherwise null.</summary>
    val mutable Node: obj

    new(source: ISource, handle: int) = { Source = source; Handle = handle; Node = null }
    new(source: ISource, handle: int, node: obj) = { Source = source; Handle = handle; Node = node }
#endif

module internal SourceSlots =
    /// <summary>
    /// The zero-length list a source list holds until its second source.
    /// </summary>
#if FABLE_COMPILER
    let none: SourceSlot[] = [||]
#else
    let inline none () =
        Array.Empty<SourceSlot>()
#endif

#if FABLE_COMPILER
    /// <summary>
    /// The zero-length handle list a source list holds until its second source.
    /// </summary>
    let noHandles: int[] = Platform.positions 0
#endif

/// <summary>
/// The sources one computation read on its last run, in the order it read
/// them.
/// </summary>
/// <remarks>
/// <para>
/// Order is the point. A body re-run after invalidation almost always reads
/// the same sources in the same order — a conditional that flips a branch is
/// the exception, not the rule — so the list is matched positionally against
/// the reads as they arrive, and an edge that is already in the right place
/// costs one reference comparison and nothing else. The alternative, which
/// this replaces, dropped every edge before the body and rebuilt it after:
/// measured at ~7 ns per edge per run, paid to arrive back where it started.
/// </para>
/// <para>
/// Divergence is handled by truncation rather than by patching: the first read
/// that does not match its slot drops that slot and everything after it, and
/// the rest of the run appends. A body that reads the same sources in a
/// different order therefore rebuilds all of them, which is the old cost and
/// no worse.
/// </para>
/// <para>
/// Each slot holds its edge's handle, and a slot's position is the slot the source passes back in <c>MovedIn</c>. A
/// source held by several slots has one edge, owned by its first slot; the later slots hold
/// <c>EdgeHandle.Unlinked</c>. Truncation drops an owning slot together with every later slot.
/// </para>
/// <para>
/// Upstream solves the same problem with a doubly-linked <c>Link</c> per edge, each
/// one simultaneously an entry in the source's subscriber list and in the
/// consumer's dependency list (<c>solid/packages/signals/src/core/graph.ts</c>,
/// after alien-signals), which also handles out-of-order re-reads by generation-stamping.
/// </para>
/// </remarks>
#if !FABLE_COMPILER && !RANVIER_TRACE
[<Struct; NoEquality; NoComparison>]
type internal SourceList =
    // Internal rather than private: `NodeOps` and `Graph.Track` match slots 0 and 1 inline without touching `rest`.
    /// <summary>The source at position 0.</summary>
    val mutable internal first: ISource
    /// <summary>The <c>Node</c> of the source at position 0 when it is a memo, otherwise null.</summary>
    val mutable internal firstNode: obj
    /// <summary>The handle of position 0.</summary>
    val mutable internal firstHandle: int
    /// <summary>The source at position 1.</summary>
    val mutable internal second: ISource
    /// <summary>The <c>Node</c> of the source at position 1 when it is a memo, otherwise null.</summary>
    val mutable internal secondNode: obj
    /// <summary>The handle of position 1.</summary>
    val mutable internal secondHandle: int
    /// <summary>Sources after the second. <c>rest[i - 2]</c> is the source at position <c>i</c>.</summary>
    val mutable internal rest: SourceSlot[]
    val mutable internal count: int
    /// <summary>How far into the list the current run has matched. Meaningless outside a run.</summary>
    val mutable internal cursor: int
    /// <summary>Every source in the list, for repeat checks over more than <c>EdgeHandle.ScanLimit</c> slots.</summary>
    val mutable internal held: Platform.RefIndex<ISource>

    internal new(unlinked: int) =
        {
            first = Unchecked.defaultof<ISource>
            firstNode = null
            firstHandle = unlinked
            second = Unchecked.defaultof<ISource>
            secondNode = null
            secondHandle = unlinked
            rest = SourceSlots.none ()
            count = 0
            cursor = 0
            held = null
        }

    static member inline Create() =
        SourceList EdgeHandle.Unlinked

    member private this.SourceAtRaw(i: int) =
        if i = 0 then
            this.first
        elif i = 1 then
            this.second
        else
            (Platform.itemAt (i - 2) this.rest).Source

    member private this.HandleAtRaw(i: int) =
        if i = 0 then
            this.firstHandle
        elif i = 1 then
            this.secondHandle
        else
            (Platform.itemAt (i - 2) this.rest).Handle

    member private this.SetHandle(i: int, handle: int) =
        if i = 0 then
            this.firstHandle <- handle
        elif i = 1 then
            this.secondHandle <- handle
        else
            this.rest[i - 2].Handle <- handle

    member private this.Grow() =
        let size = max 4 (this.rest.Length * 2)
        let grown = Array.zeroCreate size
        Array.blit this.rest 0 grown 0 this.rest.Length
        this.rest <- grown

    member private this.HeldBefore(until: int, source: ISource) =
        let mutable i = 0

        while i < until
              && not (obj.ReferenceEquals (this.SourceAtRaw i, source)) do
            i <- i + 1

        i < until

    member private this.Holds(until: int, source: ISource) =
        if until <= EdgeHandle.ScanLimit then
            this.HeldBefore (until, source)
        else
            if Platform.isNull this.held then
                this.held <- Platform.createRefIndex (until * 2)

                for i in 0 .. until - 1 do
                    Platform.refIndexSet this.held (this.SourceAtRaw i) i

            Platform.refIndexFind this.held source >= 0

    member private this.TrimFrom(self: IComputation, from: int) =
        if not (Platform.isNull this.held) then
            this.held <- null

        // Each handle is read when its slot is reached: a removal can move this list's later edges.
        for i in from .. this.count - 1 do
            // Blanked so a source dropped by a conditional body is not kept
            // alive by a slot past the end of the list.
            if i = 0 then
                if this.firstHandle >= 0 then
                    this.first.RemoveObserverAt (self, this.firstHandle)

                this.first <- Unchecked.defaultof<ISource>
                this.firstNode <- null
                this.firstHandle <- EdgeHandle.Unlinked
            elif i = 1 then
                if this.secondHandle >= 0 then
                    this.second.RemoveObserverAt (self, this.secondHandle)

                this.second <- Unchecked.defaultof<ISource>
                this.secondNode <- null
                this.secondHandle <- EdgeHandle.Unlinked
            else
                let k = i - 2
                let slot = Platform.itemAt k this.rest

                if slot.Handle >= 0 then
                    slot.Source.RemoveObserverAt (self, slot.Handle)

                this.rest[k] <- SourceSlot (Unchecked.defaultof<ISource>, EdgeHandle.Unlinked, null)
#if RANVIER_COUNTERS
            Counters.EdgeRemoved ()
#endif

        this.count <- from

    member this.Count = this.count
    member this.Cursor = this.cursor

    /// <summary>Whether any recorded source is currently stale.</summary>
    member this.HasStaleSource() =
        if this.count = 0 then
            false
        elif
            this.firstHandle < EdgeHandle.SignalBit
            && this.first.IsStale
        then
            true
        elif this.count = 1 then
            false
        elif
            this.secondHandle < EdgeHandle.SignalBit
            && this.second.IsStale
        then
            true
        else
            let limit = this.count - 2
            let mutable i = 0

            while i < limit
                  && (let s = Platform.itemAt i this.rest

                      s.Handle >= EdgeHandle.SignalBit
                      || not s.Source.IsStale) do
                i <- i + 1

            i < limit

    member this.SourceAt(i: int) =
        this.SourceAtRaw i

    member this.HandleAt(i: int) =
        this.HandleAtRaw i

    /// <summary>Updates slot <c>i</c> if its source is not a <c>Signal</c>.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member this.RefreshSourceAt(i: int) =
        if i = 0 then
            if this.firstHandle < EdgeHandle.SignalBit then
                this.first.UpdateIfNecessary ()
        elif i = 1 then
            if this.secondHandle < EdgeHandle.SignalBit then
                this.second.UpdateIfNecessary ()
        else
            let s = Platform.itemAt (i - 2) this.rest

            if s.Handle < EdgeHandle.SignalBit then
                s.Source.UpdateIfNecessary ()

    /// <summary>Stores the handle a source's swap-remove gave the edge at <c>slot</c>.</summary>
    member this.MovedIn(slot: int, handle: int) =
        this.SetHandle (slot, handle)

    /// <summary>Opens a run without detaching existing edges so matching reads reuse them.</summary>
    member this.BeginRun() =
        this.cursor <- 0


    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.Append(self: IComputation, selfNode: obj, source: ISource) =
        if this.cursor < this.count then
            this.TrimFrom (self, this.cursor)

        let slot = this.count

        if Platform.isNull selfNode then
            this.Link (self, null, source, slot)
        else
            EdgeContext.Observer <- selfNode

            try
                this.Link (self, selfNode, source, slot)
            finally
                EdgeContext.Clear ()

    /// <summary>Appends <c>source</c> at <c>slot</c> and links its edge.</summary>
    member private this.Link(self: IComputation, selfNode: obj, source: ISource, slot: int) =
        if slot = 0 then
            this.first <- source
            this.count <- 1
            this.cursor <- 1
#if RANVIER_COUNTERS
            Counters.EdgeAdded ()
#endif
            let handle = source.AddObserver (self, 0)
            this.firstHandle <- handle

            if handle < EdgeHandle.SignalBit && not (Platform.isNull selfNode) then
                this.firstNode <- EdgeContext.TakeSource source
        elif slot = 1 then
            this.second <- source
            this.secondHandle <- EdgeHandle.Unlinked
            this.secondNode <- null
            this.count <- 2
            this.cursor <- 2
#if RANVIER_COUNTERS
            Counters.EdgeAdded ()
#endif
            let tried = source.TryAddObserver (self, 1)

            let handle =
                if tried <> EdgeHandle.Unknown then tried
                elif obj.ReferenceEquals (this.first, source) then EdgeHandle.Unlinked
                else source.AddObserver (self, 1)

            this.secondHandle <- handle

            if handle < EdgeHandle.SignalBit && not (Platform.isNull selfNode) then
                this.secondNode <- EdgeContext.TakeSource source

            if not (Platform.isNull this.held) then
                Platform.refIndexSet this.held source 1
        else
            let k = slot - 2

            if k = this.rest.Length then
                this.Grow ()

            this.rest[k] <- SourceSlot (source, EdgeHandle.Unlinked, null)
            this.count <- slot + 1
            this.cursor <- this.count
#if RANVIER_COUNTERS
            Counters.EdgeAdded ()
#endif

            // A source already in the list keeps the one edge it has: A, B, A links A once.
            let tried = source.TryAddObserver (self, slot)

            let handle =
                if tried <> EdgeHandle.Unknown then tried
                elif this.Holds (slot, source) then EdgeHandle.Unlinked
                else source.AddObserver (self, slot)

            this.rest[k].Handle <- handle

            if handle < EdgeHandle.SignalBit && not (Platform.isNull selfNode) then
                this.rest[k].Node <- EdgeContext.TakeSource source

            if not (Platform.isNull this.held) then
                Platform.refIndexSet this.held source slot

    /// <summary>
    /// Records a read of <c>source</c> by <c>self</c>, whose node is <c>selfNode</c> or null: a slot hit advances the
    /// cursor, and a miss links an edge that records the nodes at both ends.
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member this.AddFrom(self: IComputation, selfNode: obj, source: ISource) =
        let cur = this.cursor

        if cur = 0 then
            if obj.ReferenceEquals (this.first, source) then
                this.cursor <- 1
            else
                this.Append (self, selfNode, source)
        elif cur = 1 then
            if obj.ReferenceEquals (this.second, source) then
                this.cursor <- 2
            elif obj.ReferenceEquals (this.first, source) then
                ()
            else
                this.Append (self, selfNode, source)
        elif
            cur < this.count
            && obj.ReferenceEquals ((Platform.itemAt (cur - 2) this.rest).Source, source)
        then
            this.cursor <- cur + 1
        elif
            obj.ReferenceEquals (
                (if cur = 2 then
                     this.second
                 else
                     (Platform.itemAt (cur - 3) this.rest).Source),
                source
            )
        then
            ()
        else
            this.Append (self, selfNode, source)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member this.Add(self: IComputation, source: ISource) = this.AddFrom (self, null, source)

    /// <summary>Closes a run, dropping whatever the body did not read this time.</summary>
    member this.EndRun(self: IComputation) =
        if this.cursor < this.count then
            this.TrimFrom (self, this.cursor)

    /// <summary>Drops every edge. Disposal, not re-collection.</summary>
    member this.Clear(self: IComputation) =
        this.TrimFrom (self, 0)
        this.cursor <- 0
#else
type internal SourceList() =
    /// <summary>
    /// The source at position 0.
    /// </summary>
    let mutable first: ISource = Unchecked.defaultof<ISource>

    /// <summary>
    /// The handle of position 0.
    /// </summary>
    let mutable firstHandle = EdgeHandle.Unlinked

    /// <summary>
    /// Sources after the first. <c>rest[i - 1]</c> is the source at position <c>i</c>.
    /// </summary>
#if FABLE_COMPILER
    let mutable rest: SourceSlot[] = SourceSlots.none
#else
    let mutable rest: SourceSlot[] = SourceSlots.none ()
#endif

#if FABLE_COMPILER
    /// <summary>
    /// The handle of each source in <c>rest</c>. Grown and blanked together with <c>rest</c>, and never read past
    /// <c>count</c>.
    /// </summary>
    let mutable restHandles: int[] = SourceSlots.noHandles
#endif

    let mutable count = 0

    /// <summary>
    /// How far into the list the current run has matched. Meaningless outside
    /// a run, which is why nothing reads it there.
    /// </summary>
    let mutable cursor = 0

    /// <summary>
    /// Every source in the list, for repeat checks over more than <c>EdgeHandle.ScanLimit</c> slots. Built by the first
    /// such check, extended by every later append and dropped by a trim; null otherwise.
    /// </summary>
    let mutable held: Platform.RefIndex<ISource> = null

#if RANVIER_TRACE
    let mutable traceLog: TraceLog = null
    let mutable traceId = 0
#endif

    let sourceAt (i: int) =
        if i = 0 then
            first
        else
            (Platform.itemAt (i - 1) rest).Source

    let handleAt (i: int) =
        if i = 0 then
            firstHandle
        else
#if FABLE_COMPILER
            Platform.itemAt (i - 1) restHandles
#else
            (Platform.itemAt (i - 1) rest).Handle
#endif

    let setHandle (i: int) (handle: int) =
        if i = 0 then
            firstHandle <- handle
        else
#if FABLE_COMPILER
            Platform.setItemAt (i - 1) restHandles handle
#else
            rest[i - 1].Handle <- handle
#endif

    /// <summary>
    /// Doubles the capacity of every array after the first position.
    /// </summary>
    let grow () =
        let size = max 4 (rest.Length * 2)
#if FABLE_COMPILER
        let grownSources = Array.zeroCreate size
        let grownHandles = Platform.positions size
        Array.blit rest 0 grownSources 0 rest.Length
        Platform.copyPositions restHandles grownHandles rest.Length
        rest <- grownSources
        restHandles <- grownHandles
#else
        let grown = Array.zeroCreate size
        Array.blit rest 0 grown 0 rest.Length
        rest <- grown
#endif

    /// <summary>
    /// Whether <c>source</c> holds a slot before <c>until</c>.
    /// </summary>
    let heldBefore (until: int) (source: ISource) =
        let mutable i = 0

        while i < until
              && not (obj.ReferenceEquals (sourceAt i, source)) do
            i <- i + 1

        i < until

    /// <summary>
    /// Whether <c>source</c> holds a slot before <c>until</c>. A scan up to <c>EdgeHandle.ScanLimit</c> slots, and a
    /// lookup in <c>held</c> above it.
    /// </summary>
    let holds (until: int) (source: ISource) =
        if until <= EdgeHandle.ScanLimit then
            heldBefore until source
        else
            if Platform.isNull held then
                held <- Platform.createRefIndex (until * 2)

                for i in 0 .. until - 1 do
                    Platform.refIndexSet held (sourceAt i) i

            Platform.refIndexFind held source >= 0

    /// <summary>
    /// Drops the slots from <c>from</c> on, unlinking the edges they own.
    /// </summary>
    let trimFrom (self: IComputation) from =
        if not (Platform.isNull held) then
            held <- null

        // Each handle is read when its slot is reached: a removal can move this list's later edges.
        for i in from .. count - 1 do
            // Blanked so a source dropped by a conditional body is not kept
            // alive by a slot past the end of the list.
            if i = 0 then
                if firstHandle >= 0 then
                    first.RemoveObserverAt (self, firstHandle)

                first <- Unchecked.defaultof<ISource>
                firstHandle <- EdgeHandle.Unlinked
            else
                let k = i - 1
#if FABLE_COMPILER
                let handle = Platform.itemAt k restHandles

                if handle >= 0 then
                    (Platform.itemAt k rest).Source.RemoveObserverAt(self, handle)

                Platform.setItemAt k rest (SourceSlot Unchecked.defaultof<ISource>)
                Platform.setItemAt k restHandles EdgeHandle.Unlinked
#else
                let slot = Platform.itemAt k rest

                if slot.Handle >= 0 then
                    slot.Source.RemoveObserverAt (self, slot.Handle)

                rest[k] <- SourceSlot (Unchecked.defaultof<ISource>, EdgeHandle.Unlinked)
#endif
#if RANVIER_COUNTERS
            Counters.EdgeRemoved ()
#endif

        count <- from

    static member inline Create() =
        SourceList ()

    member _.Count = count
    member _.Cursor = cursor

    /// <summary>
    /// Whether any recorded source is currently stale.
    /// </summary>
    member _.HasStaleSource() =
        if count = 0 then
            false
        elif
            firstHandle < EdgeHandle.SignalBit
            && first.IsStale
        then
            true
        else
            let limit = count - 1
            let mutable i = 0

            while i < limit
                  && (
#if FABLE_COMPILER
                  Platform.itemAt i restHandles
                  >= EdgeHandle.SignalBit
                  || not (Platform.itemAt i rest).Source.IsStale
#else
                      let s = Platform.itemAt i rest

                      s.Handle >= EdgeHandle.SignalBit
                      || not s.Source.IsStale
#endif
                  ) do
                i <- i + 1

            i < limit

    /// <summary>
    /// The source at <c>i</c>. For the <c>Check</c> walk, which has to ask each source in
    /// turn whether it actually moved, and is the only caller that looks at
    /// this list outside a run.
    /// </summary>
    member _.SourceAt(i: int) =
        sourceAt i

    /// <summary>
    /// The edge handle at <c>i</c>.
    /// </summary>
    member _.HandleAt(i: int) =
        handleAt i

    /// <summary>Updates slot <c>i</c> if its source is not a <c>Signal</c>.</summary>
#if FABLE_COMPILER
    member _.RefreshSourceAt(i: int) =
        if i = 0 then
            if firstHandle < EdgeHandle.SignalBit then
                first.UpdateIfNecessary ()
        elif Platform.itemAt (i - 1) restHandles < EdgeHandle.SignalBit then
            (Platform.itemAt (i - 1) rest).Source.UpdateIfNecessary()
#else
    member _.RefreshSourceAt(i: int) =
        if i = 0 then
            if firstHandle < EdgeHandle.SignalBit then
                first.UpdateIfNecessary ()
        else
            let s = Platform.itemAt (i - 1) rest

            if s.Handle < EdgeHandle.SignalBit then
                s.Source.UpdateIfNecessary ()
#endif

    /// <summary>
    /// Stores the handle a source's swap-remove gave the edge at <c>slot</c>.
    /// </summary>
    member _.MovedIn(slot: int, handle: int) =
        setHandle slot handle

    /// <summary>
    /// Opens a run. Deliberately does not detach: the edges stay live so the
    /// ones that are read again can be recognised.
    /// </summary>
    member _.BeginRun() =
        cursor <- 0

    /// <summary>
    /// Appends <c>source</c> after a read that missed its slot, trimming the unmatched slots first. Kept out of
    /// <c>Add</c>, whose matching path runs on every tracked read.
    /// </summary>
    member private this.Append(self: IComputation, source: ISource) =
        if cursor < count then
            Tracer.EdgesRemove (this, cursor)
            trimFrom self cursor

        let slot = count

        if slot = 0 then
            first <- source
            count <- 1
            cursor <- 1
#if RANVIER_COUNTERS
            Counters.EdgeAdded ()
#endif
            Tracer.EdgeAdd (this, source, 0)
            firstHandle <- source.AddObserver (self, 0)
        else
            let k = slot - 1

            if k = rest.Length then
                grow ()

#if FABLE_COMPILER
            Platform.setItemAt k rest (SourceSlot source)
#else
            rest[k] <- SourceSlot (source, EdgeHandle.Unlinked)
#endif
            count <- slot + 1
            cursor <- count
#if RANVIER_COUNTERS
            Counters.EdgeAdded ()
#endif
            Tracer.EdgeAdd (this, source, slot)

            // A source already in the list keeps the one edge it has: A, B, A links A once.
            let tried = source.TryAddObserver (self, slot)

            let handle =
                if tried <> EdgeHandle.Unknown then tried
                elif holds slot source then EdgeHandle.Unlinked
                else source.AddObserver (self, slot)

#if FABLE_COMPILER
            Platform.setItemAt k restHandles handle
#else
            rest[k].Handle <- handle
#endif

            if not (Platform.isNull held) then
                Platform.refIndexSet held source slot

    member this.Add(self: IComputation, source: ISource) =
        if cursor = 0 then
            if count > 0 && obj.ReferenceEquals (first, source) then
                cursor <- 1
            else
                this.Append (self, source)
        elif
            cursor < count
            && obj.ReferenceEquals ((Platform.itemAt (cursor - 1) rest).Source, source)
        then
            cursor <- cursor + 1
        elif
            obj.ReferenceEquals (
                (if cursor = 1 then
                     first
                 else
                     (Platform.itemAt (cursor - 2) rest).Source),
                source
            )
        then
            // The same source read twice in a row — a loop over one signal, or
            // a helper that reads what the caller just read. Recording it
            // again would make the list as long as the read count, and would
            // still match positionally, so nothing would ever shrink it.
            ()
        else
            this.Append (self, source)

    /// <summary>
    /// Closes a run, dropping whatever the body did not read this time. Must
    /// run even when the body threw: a suspended body read a prefix, and the
    /// edges past that prefix are no longer ones it depends on.
    /// </summary>
    member this.EndRun(self: IComputation) =
        if cursor < count then
            Tracer.EdgesRemove (this, cursor)
            trimFrom self cursor

    /// <summary>
    /// Drops every edge. Disposal, not re-collection.
    /// </summary>
    member this.Clear(self: IComputation) =
        Tracer.EdgesRemove (this, 0)
        trimFrom self 0
        cursor <- 0

#if RANVIER_TRACE
    interface ITraced with
        member _.TraceLog
            with get () = traceLog
            and set log = traceLog <- log

        member _.TraceId
            with get () = traceId
            and set id = traceId <- id

    interface ITracedEdges with
        member _.Owner = traceId
        member _.IsSources = true
        member _.Ids = Array.init count (fun i -> (sourceAt i).Id)
#endif
#endif

#if !FABLE_COMPILER
/// <summary>One registered cleanup action paired with its acquisition order inside an <see cref="Owner"/>.</summary>
[<Struct; NoEquality; NoComparison>]
type internal CleanupEntry =
    val Action: unit -> unit
    val Order: int
    new(action: unit -> unit, order: int) = { Action = action; Order = order }
#endif

/// <summary>
/// A child disposed with its owner's scope.
/// </summary>
type internal IOwned =
    /// <summary>
    /// Called only by the owner, after the child's link has left the owner's
    /// list. The child disposes without unlinking.
    /// </summary>
    abstract Release: unit -> unit

/// <summary>
/// One child's place in its owner's list.
/// </summary>
/// <remarks>
/// A flat list cannot serve: removing one entry by identity is a linear scan,
/// and an index is invalidated by every removal before it. Most children are
/// disposed individually rather than with their scope — re-running an effect
/// disposes its own scope, and a memo can outlive nothing at all — so a child
/// has to be able to leave its owner in constant time. Without that the list
/// only grows, and it retains every computation the graph has ever created,
/// closure included, long after disposal.
/// </remarks>
[<AllowNullLiteral>]
type internal OwnerLink(child: IOwned, owner: Owner) =
    member _.Child = child
    member _.Owner = owner

    member this.Detach() =
        owner.Unlink this

    member val Prev: OwnerLink = null with get, set
    member val Next: OwnerLink = null with get, set

    /// <summary>
    /// The owner's list generation the link joined, or -1 once the link has left the list.
    /// </summary>
    /// <remarks>
    /// A link at -1 is ignored by a later unlink, which would otherwise null out the owner's head.
    /// </remarks>
    member val Generation = 0 with get, set

    /// <summary>
    /// The owner's acquisition count when the link joined.
    /// </summary>
    member val Acquired = 0 with get, set

/// <summary>
/// A disposal scope.
/// </summary>
/// <remarks>
/// <para>
/// Reactive graphs leak by default: an effect is kept alive by the sources it
/// reads, so nothing collects it and nothing stops it running. Disposing every
/// effect by hand is the kind of bookkeeping that is wrong in one place and
/// silently keeps a subtree running. An owner makes lifetime structural — a
/// scope is torn down as a unit, in reverse acquisition order.
/// </para>
/// <para>
/// Owners nest, and every computation with a body is itself an owner: nodes
/// created and cleanups registered inside an effect, memo, async memo or
/// boundary body belong to that run and are discharged before the next one.
/// <c>OnCleanup</c> scopes subscriptions, timers and handles to the run that
/// created them.
/// </para>
/// </remarks>
and Owner internal (sink: Owner) =
    let mutable head: OwnerLink = null
    let mutable tail: OwnerLink = null

    /// <summary>
    /// The earliest cleanup registered since the last teardown, or null.
    /// </summary>
    let mutable firstCleanup: unit -> unit = Unchecked.defaultof<_>

    /// <summary>
    /// The acquisition count of <c>firstCleanup</c>.
    /// </summary>
    let mutable firstOrder = 0

    /// <summary>
    /// The cleanups registered after <c>firstCleanup</c>. Allocated by the second <c>OnCleanup</c>; null while a
    /// teardown runs the cleanups it detached.
    /// </summary>
#if FABLE_COMPILER
    let mutable cleanups: ResizeArray<unit -> unit> = null

    /// <summary>
    /// The acquisition count of each entry in <c>cleanups</c>.
    /// </summary>
    let mutable cleanupOrder: ResizeArray<int> = null
#else
    let mutable cleanups: ResizeArray<CleanupEntry> = null
#endif

    /// <summary>
    /// Counts cleanups and children registered since the last teardown.
    /// </summary>
    let mutable acquisitions = 0

    /// <summary>
    /// Allocated by the first error recorded without a sink.
    /// </summary>
    let mutable errors: ResizeArray<exn> = null
    let mutable disposed = false

    /// <summary>
    /// Bumped each time a teardown detaches the child list. A link from an
    /// earlier generation belongs to a detached list. Always non-negative.
    /// </summary>
    let mutable generation = 0

    /// <summary>
    /// Where this scope sits in its own parent, so it can leave in constant
    /// time when disposed on its own rather than with the parent.
    /// </summary>
    let mutable parentLink: OwnerLink = null

#if RANVIER_TRACE
    let mutable traceLog: TraceLog = null
    let mutable traceId = 0
#endif

#if RANVIER_COUNTERS
    do Counters.OwnerCreated ()
#endif

    new() = new Owner (Unchecked.defaultof<Owner>)

    member _.IsDisposed = disposed

    member internal this.SetParent(link: OwnerLink) =
        Tracer.OwnerAdopt (link.Owner, this, (this :? RootScope))
        parentLink <- link

    /// <summary>
    /// Exceptions recorded by this scope: from its cleanups, and on <c>Graph.Root</c> from the inbox.
    /// </summary>
    /// <remarks>
    /// Only <c>Graph.Root</c>, a <c>createRoot</c> scope and a scope from <c>new Owner ()</c> hold errors. The
    /// scope of a computation, projection or lookup records into <c>Graph.Root.Errors</c>, and its own list stays empty.
    /// </remarks>
    member _.Errors =
        Seq.delay (fun () ->
            if Platform.isNull errors then
                Seq.empty
            else
                errors :> seq<exn>)

    member _.ErrorCount = if Platform.isNull errors then 0 else errors.Count

    /// <summary>
    /// The errors of <c>Errors</c> recorded at or after position <c>index</c>, in recording order.
    /// </summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><c>index</c> is negative.</exception>
    member _.ErrorsFrom(index: int) : exn[] =
        if index < 0 then
            raise (ArgumentOutOfRangeException (nameof index))

        if Platform.isNull errors || index >= errors.Count then
            [||]
        else
            errors.GetRange(index, errors.Count - index).ToArray()

    /// <summary>
    /// Takes ownership of <c>child</c> without a handle: it is disposed with the
    /// scope, and cannot be released before then. On a disposed scope <c>child</c> is disposed immediately, in the context
    /// <c>OnCleanup</c> describes.
    /// </summary>
    member this.Attach(child: IDisposable) =
        Tracer.OwnerAdopt (this, child, false)

        this.AttachLinked
            { new IOwned with
                member _.Release() =
                    child.Dispose ()
            }
        |> ignore

    member internal this.AttachLinked(child: IOwned) =
        if disposed then
            this.DisposeLate child
        else
            this.Append child

    /// <summary>
    /// Disposes a child attached after the scope was disposed, handing back
    /// an unlinked link.
    /// </summary>
    member private this.DisposeLate(child: IOwned) =
        this.RunLate child.Release
        OwnerLink (child, this, Generation = -1)

    /// <summary>
    /// Runs <c>f</c> on behalf of the disposed scope: under the graph's teardown
    /// context for a root scope, inline otherwise. An exception from <c>f</c> is
    /// recorded.
    /// </summary>
    member private this.RunLate(f: unit -> unit) =
        match box this with
        | :? RootScope as r -> (r.Runner: ILateRunner).RunDetached(this, f)
        | _ ->
            try
                f ()
            with ex ->
                this.RecordError ex

    member private this.Append(child: IOwned) =
        let link = OwnerLink (child, this, Generation = generation, Acquired = acquisitions)
        acquisitions <- acquisitions + 1

        if Platform.isNull tail then
            head <- link
            tail <- link
        else
            link.Prev <- tail
            tail.Next <- link
            tail <- link

        link

    /// <summary>
    /// Releases a child that was disposed on its own. A no-op for a link on a
    /// list a teardown detached: the teardown's walk follows those pointers,
    /// and discards the list when it finishes.
    /// </summary>
    member internal _.Unlink(link: OwnerLink) =
        if
            not (Platform.isNull link)
            && link.Generation = generation
        then
            link.Generation <- -1

            if Platform.isNull link.Prev then
                head <- link.Next
            else
                link.Prev.Next <- link.Next

            if Platform.isNull link.Next then
                tail <- link.Prev
            else
                link.Next.Prev <- link.Prev

            link.Prev <- null
            link.Next <- null

    /// <summary>
    /// Records a failure from teardown or the inbox instead of rethrowing it.
    /// </summary>
    /// <remarks>
    /// The scope of an effect, memo, async memo, boundary, projection key or pass, or lookup records into
    /// <c>Graph.Root</c>. A <c>createRoot</c> scope and a scope from <c>new Owner ()</c> keep their own errors.
    /// </remarks>
    member internal _.RecordError(ex: exn) =
        if Platform.isNull (box sink) then
            if Platform.isNull errors then
                errors <- ResizeArray<exn>()

            errors.Add ex
        else
            sink.RecordError ex

    /// <summary>
    /// Registers <c>f</c> to run when the scope is torn down.
    /// </summary>
    /// <remarks>
    /// On a disposed scope <c>f</c> runs immediately. A scope created by
    /// <c>createRoot</c> runs it untracked, with effects deferred until it returns
    /// and the scope as the owner of every node it creates. Any other scope,
    /// including <c>Graph.Root</c> and a scope constructed with <c>new Owner ()</c>,
    /// runs it inline in the caller's tracking context.
    /// </remarks>
    member this.OnCleanup(f: Action) =
        this.AddCleanup f.Invoke

    member internal this.AddCleanup(f: unit -> unit) =
        if disposed then
            this.RunLate f
        else
            if Platform.isNull (box firstCleanup) then
                firstCleanup <- f
                firstOrder <- acquisitions
            else
#if FABLE_COMPILER
                if Platform.isNull cleanups then
                    cleanups <- ResizeArray<unit -> unit>()
                    cleanupOrder <- ResizeArray<int>()

                cleanups.Add f
                cleanupOrder.Add acquisitions
#else
                if Platform.isNull cleanups then
                    cleanups <- ResizeArray<CleanupEntry>()

                cleanups.Add (CleanupEntry (f, acquisitions))
#endif

            acquisitions <- acquisitions + 1

    /// <summary>
    /// Tears the scope down without retiring it, so it can be filled again.
    /// This is what an effect does before re-running its body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cleanups and children are torn down together in reverse acquisition
    /// order, as <c>use</c> bindings are disposed. A cleanup runs while every
    /// node created before its registration is still live.
    /// </para>
    /// <para>
    /// The cleanups and children are detached before the first one runs. A
    /// cleanup or child registered while this teardown runs joins the emptied
    /// scope and outlives the teardown. During the teardown <c>Dispose</c> runs, a
    /// cleanup or child registered with the scope runs, or is disposed,
    /// immediately, as <c>OnCleanup</c> describes.
    /// </para>
    /// </remarks>
    member internal this.DisposeScope() =
        let first = firstCleanup
        let firstAcquired = firstOrder
        let detached = cleanups
#if FABLE_COMPILER
        let detachedOrder = cleanupOrder
#endif
        let mutable node = tail
        head <- null
        tail <- null
        generation <- (generation + 1) &&& 0x7FFFFFFF
        acquisitions <- 0
        firstCleanup <- Unchecked.defaultof<_>
        cleanups <- null
#if FABLE_COMPILER
        cleanupOrder <- null
#endif

        // Index 0 is `first` when it is set; the rest index `detached` from `offset`.
        let offset = if Platform.isNull (box first) then 0 else 1

        let mutable index =
            offset
            + (if Platform.isNull detached then 0 else detached.Count)
            - 1

        while index >= 0 || not (Platform.isNull node) do
            if
                not (Platform.isNull node)
                && (index < 0
                    || node.Acquired >
                        (if index < offset then
                             firstAcquired
                         else
#if FABLE_COMPILER
                             Platform.entryAt (index - offset) detachedOrder
#else
                             (Platform.entryAt (index - offset) detached).Order
#endif
                        ))
            then
                let prev = node.Prev

                try
                    node.Child.Release ()
                with ex ->
                    this.RecordError ex

                node <- prev
            else
                try
                    if index < offset then
                        first ()
                    else
#if FABLE_COMPILER
                        (Platform.entryAt (index - offset) detached) ()
#else
                        (Platform.entryAt (index - offset) detached).Action()
#endif
                with ex ->
                    this.RecordError ex

                index <- index - 1

        if not (Platform.isNull detached) then
            // Guarded because `Clear` is not free where it matters most. Fable
            // compiles it to `col.splice(0)`, which allocates the array of
            // removed elements whether or not there were any.
            if detached.Count > 0 then
                detached.Clear ()
#if FABLE_COMPILER
                detachedOrder.Clear ()
#endif

            if Platform.isNull cleanups then
                cleanups <- detached
#if FABLE_COMPILER
                cleanupOrder <- detachedOrder
#endif

    /// <summary>
    /// Tears the scope down and retires it. Idempotent.
    /// </summary>
    /// <remarks>
    /// A root scope's teardown runs untracked, with effects deferred until it
    /// returns, and owns every node its cleanups create.
    /// </remarks>
    /// <exception cref="T:System.InvalidOperationException">
    /// A root scope of a <c>Serialised</c> graph is disposed outside the construction context, or while another
    /// thread is inside the graph. The scope stays live.
    /// </exception>
    member this.Dispose() =
        if not disposed then
            match box this with
            | :? RootScope as r ->
                let runner = r.Runner
                let held = runner.HoldTeardown ()

                try
                    this.TearDown runner
                finally
                    if held then
                        runner.ReleaseTeardown ()
            | _ -> this.TearDown Unchecked.defaultof<ILateRunner>

    /// <summary>
    /// Marks the scope disposed, disposes its children and unlinks it from its parent. A root scope passes its
    /// <c>runner</c>, which runs the teardown detached; any other scope passes null.
    /// </summary>
    member private this.TearDown(runner: ILateRunner) =
        disposed <- true
        Tracer.OwnerDispose this

        if Platform.isNull (box runner) then
            this.DisposeScope ()
        else
            runner.RunDetached (this, this.DisposeScope)

        if not (Platform.isNull parentLink) then
            parentLink.Detach ()
            parentLink <- null

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

    interface IOwned with
        member this.Release() =
            parentLink <- null
            this.Dispose ()

#if RANVIER_TRACE
    interface ITraced with
        member _.TraceLog
            with get () = traceLog
            and set log = traceLog <- log

        member _.TraceId
            with get () = traceId
            and set id = traceId <- id
#endif

/// <summary>
/// Runs work on behalf of a disposed root scope.
/// </summary>
and internal ILateRunner =
    /// <summary>
    /// Runs <c>f</c> untracked, with effects deferred until it returns and <c>owner</c>
    /// as the owner of every node it creates. An exception from <c>f</c> is
    /// recorded on <c>owner</c>.
    /// </summary>
    abstract RunDetached: owner: Owner * f: (unit -> unit) -> unit

    /// <summary>
    /// Holds a <c>Serialised</c> graph for a root scope's teardown.
    /// </summary>
    /// <returns>True when the caller must pair it with <c>ReleaseTeardown</c>.</returns>
    /// <exception cref="T:System.InvalidOperationException">
    /// The caller runs outside the construction context, or another thread holds the graph.
    /// </exception>
    abstract HoldTeardown: unit -> bool

    /// <summary>
    /// Frees a graph held by <c>HoldTeardown</c>.
    /// </summary>
    abstract ReleaseTeardown: unit -> unit

/// <summary>
/// The scope <c>Graph.CreateRoot</c> hands out.
/// </summary>
and [<Sealed>] internal RootScope(runner: ILateRunner) =
    inherit Owner(Unchecked.defaultof<Owner>)

    member _.Runner: ILateRunner = runner

/// <summary>
/// What a computation's body may create. Fixed at construction.
/// </summary>
type internal ScopeMode =
    /// <summary>
    /// Creating an owned node raises <c>InvalidOperationException</c>.
    /// </summary>
    | Pure = 0uy
    /// <summary>
    /// The body's nodes and cleanups belong to the computation's own scope.
    /// </summary>
    | Owning = 1uy
    /// <summary>
    /// A value-form projection row: pure, raising <c>ScopeMessages.valueRow</c>.
    /// </summary>
    | ValueRow = 2uy
    /// <summary>
    /// A factory-form projection row: pure, raising <c>ScopeMessages.factoryRow</c>.
    /// </summary>
    | FactoryRow = 3uy
    /// <summary>
    /// A lookup's <c>f</c>: pure, raising <c>ScopeMessages.lookup</c>.
    /// </summary>
    | Lookup = 4uy
    /// <summary>
    /// A pure async memo, raising <c>ScopeMessages.asyncMemo</c>.
    /// </summary>
    | PureAsync = 5uy
    /// <summary>
    /// The <c>compute</c> of <c>createEffectOn</c>: pure, raising <c>ScopeMessages.effectOn</c>.
    /// </summary>
    | EffectOn = 6uy

/// <summary>
/// The <c>InvalidOperationException</c> messages for a pure body that creates an
/// owned node.
/// </summary>
module internal ScopeMessages =
    [<Literal>]
    let private nodes =
        "a memo, effect, async value, boundary, root, projection, lookup, selector or onCleanup"

    let memo =
        $"A memo created by createMemo created an owned node in its body: {nodes}. createMemo is a pure derivation. Use createMemoWith for a memo that owns the nodes its body creates: they are disposed before each re-run and with the memo."

    let asyncMemo =
        $"An async value created by createAsync created an owned node in its body: {nodes}. createAsync is a pure derivation. Use createAsyncWith for an async value that owns the nodes its body creates: they are disposed before each flight and with the async value."

    let valueRow =
        $"A projection's map created an owned node: {nodes}. map re-runs whenever its key's item or a value it read changes. Use createProjectionWith, or createIndexProjectionWith, and create the node in the factory, which runs once per key."

    let factoryRow =
        $"A projection row's reader created an owned node: {nodes}. The reader re-runs whenever its key's item or a value it read changes. Move the creation into the factory body, which runs once per key."

    let lookup =
        $"A lookup's f created an owned node: {nodes}. f is a pure function of the source and the key, re-run for every affected key. Create the node outside the lookup."

    let effectOn =
        $"An effect created by createEffectOn created an owned node in its compute: {nodes}. compute is a pure derivation. Create the node in act, which owns it: it is disposed before the next act and with the effect."

    let lookupAffected =
        $"A lookup's affected created an owned node: {nodes}. affected is a pure function of the previous and the next source, re-run on every source change. Create the node outside the lookup."

    let forMode (mode: ScopeMode) =
        match mode with
        | ScopeMode.ValueRow -> valueRow
        | ScopeMode.FactoryRow -> factoryRow
        | ScopeMode.Lookup -> lookup
        | ScopeMode.PureAsync -> asyncMemo
        | ScopeMode.EffectOn -> effectOn
        | _ -> memo

    /// <summary>
    /// True when <c>ex</c> is the violation raised under <c>message</c>.
    /// </summary>
    let isViolation (message: string) (ex: exn) =
        Platform.isExactInvalidOperation ex
        && String.Equals (ex.Message, message, StringComparison.Ordinal)

    /// <summary>
    /// The error a violating run fails with: <c>ex</c> when it is the violation's
    /// own exception, a fresh one otherwise.
    /// </summary>
    let failure (mode: ScopeMode) (ex: exn) : exn =
        let message = forMode mode

        if isViolation message ex then
            ex
        else
            InvalidOperationException message

/// <summary>
/// A computation whose body runs under a scope rule of its own.
/// </summary>
type internal IScopeHost =
    /// <summary>
    /// The computation's scope, allocated by the first creation in the body.
    /// A pure computation raises <c>InvalidOperationException</c> instead, and
    /// its current run fails even when the body catches the exception.
    /// </summary>
    abstract Scope: Owner

    /// <summary>True when <c>Scope</c> will return a scope rather than raise; reading it leaves the run intact.</summary>
    abstract CanOwn: bool

/// <summary>
/// Holds each thread's <c>AmbientCell</c>. Held apart from <c>Graph</c>, whose static initialisation check would
/// otherwise guard every access.
/// </summary>
[<AbstractClass; Sealed>]
type internal AmbientSlot =
    [<ThreadStatic; DefaultValue>]
    static val mutable private cell: AmbientCell

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member internal InitCell() =
        let created = AmbientCell ()
        AmbientSlot.cell <- created
        created

    /// <summary>
    /// The calling thread's cell, created on first use.
    /// </summary>
    static member inline Cell =
        let c = AmbientSlot.cell

        if not (Platform.isNull c) then
            c
        else
            AmbientSlot.InitCell ()

    /// <summary>
    /// The calling thread's cell, or null before its first use.
    /// </summary>
    static member inline Existing = AmbientSlot.cell

/// <summary>
/// The running graph of one thread. A guarded graph holds its owner thread's
/// cell. Re-entering the graph already in <c>Hosting</c> writes nothing.
/// </summary>
and [<Sealed; AllowNullLiteral>] internal AmbientCell() =
    /// <summary>
    /// The graph most recently running a body, a flush or a stale read.
    /// Ambient while that graph is still running one.
    /// </summary>
    [<DefaultValue>]
    val mutable Hosting: obj

/// <summary>
/// The innermost <c>Graph.Activate</c> call. On .NET it flows with the execution context: an activation inside an
/// async body follows that body across an <c>await</c> and is invisible to other work on the activating thread.
/// </summary>
module internal Activation =
#if FABLE_COMPILER
    let mutable private activated: obj = null

    let current () = activated

    let set (entry: obj) =
        activated <- entry
#else
    let private activated = AsyncLocal<obj>()

    let current () =
        activated.Value

    let set (entry: obj) =
        activated.Value <- entry
#endif

/// <summary>
/// A <c>Graph.Activate</c> call: the graph, the activating thread, whether the graph is ambient on other threads,
/// and the handle that restores the enclosing activation when disposed.
/// </summary>
[<Sealed; AllowNullLiteral>]
type internal ActivationEntry(graph: obj, thread: int, anyThread: bool, previous: obj, cell: AmbientCell, previousHosting: obj) =
    /// <summary>
    /// The graph, when ambient on the calling thread, otherwise null.
    /// </summary>
    member _.GraphOnCurrentThread =
        if anyThread || thread = Platform.currentThreadId () then
            graph
        else
            null

    interface IDisposable with
        member _.Dispose() =
            Activation.set previous

            if obj.ReferenceEquals (AmbientSlot.Existing, cell) then
                cell.Hosting <- previousHosting

/// <summary>
/// A failed node's exception and the node it originated in. Every node the exception reaches through reads holds the
/// same record.
/// </summary>
/// <remarks>
/// The origin is the node whose body, comparer, flight, <c>Fail</c> or disposal raised the exception, as reported by
/// <c>ErrorOrigin</c>.
/// </remarks>
[<Sealed; AllowNullLiteral>]
type internal Failure(error: exn, origin: INode, supplied: bool) =
    let mutable captured: Platform.CapturedFailure = null

    new(error: exn, origin: INode) = Failure (error, origin, false)

    member _.Error = error
    member _.Origin = origin

    /// <summary>
    /// Raises <c>Error</c>. On .NET every rethrow carries the frames captured by the first, followed by the rethrowing
    /// reader's frames.
    /// </summary>
    member _.Rethrow() : unit =
        if Platform.isNull captured then
            captured <-
                if supplied then
                    Platform.captureSuppliedFailure error
                else
                    Platform.captureFailure null error

        Platform.rethrowStored captured

    /// <summary>The exception of <c>failure</c>, or null when <c>failure</c> is null.</summary>
    static member ErrorOf(failure: Failure) : exn =
        if Platform.isNull failure then null else failure.Error

    /// <summary>
    /// The payload of a trace <c>Moved</c> event: the exception of <c>failure</c>, or <c>value</c> when <c>failure</c> is
    /// null.
    /// </summary>
    static member Payload(failure: Failure, value: obj) : obj =
        if Platform.isNull failure then value else box failure.Error

    /// <summary>The origin of <c>failure</c>, or null when <c>failure</c> is null.</summary>
    static member OriginOf(failure: Failure) : INode =
        if Platform.isNull failure then
            Unchecked.defaultof<INode>
        else
            failure.Origin

    /// <summary>
    /// True when <c>next</c> and <c>previous</c> hold different exception instances, null counting as none. The cutoff of
    /// every node that stores a <c>Failure</c>.
    /// </summary>
    static member Moved(next: Failure, previous: Failure) =
        not (obj.ReferenceEquals (next, previous))
        && not (obj.ReferenceEquals (Failure.ErrorOf next, Failure.ErrorOf previous))

/// <summary>
/// The queue item that runs a graph's queued projection passes. Carries no id, so a trace records no node for it.
/// </summary>
[<Sealed>]
type internal PassDrain(run: unit -> unit) =
    interface IScheduled with
        member _.Execute() = run ()

#if !FABLE_COMPILER
/// <summary>
/// A <c>Graph.DispatchAsync</c> item waiting in a graph's inbox, faulted by <c>Abandon</c> when the graph is
/// disposed first.
/// </summary>
type internal IPendingDispatch =
    abstract Abandon: error: exn -> unit

/// <summary>
/// The task of a queued <c>Graph.DispatchAsync</c> item. Exactly one of <c>TryClaim</c>, <c>Cancel</c> and
/// <c>Abandon</c> takes effect; once the drain claims the item, the task completes with the work's outcome.
/// </summary>
[<Sealed>]
type internal PendingDispatch<'T>() =
    inherit TaskCompletionSource<'T>(TaskCreationOptions.RunContinuationsAsynchronously)

    /// <summary>0 while waiting, 1 once claimed by the drain, 2 once cancelled or abandoned.</summary>
    let mutable state = 0

    member private _.TryClose() =
        Interlocked.CompareExchange (&state, 2, 0) = 0

    /// <summary>True for the caller that moves the item from waiting to running.</summary>
    member _.TryClaim() =
        Interlocked.CompareExchange (&state, 1, 0) = 0

    member this.Cancel(token: CancellationToken) =
        if this.TryClose () then
            this.TrySetCanceled token |> ignore

    interface IPendingDispatch with
        member this.Abandon error =
            if this.TryClose () then
                this.SetException error

[<AbstractClass; Sealed>]
type internal OwnerThreadCounter =
    static let mutable next = 0L

    static member Next() : int64 =
        Interlocked.Increment &next

/// <summary>
/// Per-thread 64-bit token for <see cref="Graph"/> owner-thread checks on .NET. Held in a <c>.cctor</c>-free
/// non-GC <c>[&lt;ThreadStatic&gt;]</c> block so <c>Current</c> compiles to an inline TLS load without a helper call.
/// Uninitialized threads read <c>0L</c>; every owner thread is assigned a strictly positive token (<c>&gt;= 1L</c>).
/// </summary>
[<AbstractClass; Sealed>]
type internal OwnerThreadToken =
    [<ThreadStatic; DefaultValue>]
    static val mutable private token: int64

    static member inline Current: int64 = OwnerThreadToken.token

    static member Ensure() : int64 =
        let t = OwnerThreadToken.token

        if t <> 0L then
            t
        else
            let assigned = OwnerThreadCounter.Next ()
            OwnerThreadToken.token <- assigned
            assigned
#endif

/// <summary>The application key and codec of a journaled input node, with the value type erased.</summary>
[<AbstractClass>]
type internal JournalBinding(key: string, codec: string, version: int) =
    member _.Key = key
    member _.Codec = codec
    member _.Version = version

    /// <summary>The codec's text for <c>value</c>. Raises when the codec raises.</summary>
    abstract Encode: value: obj -> string

    /// <summary>True when the node accepts entries of <c>kind</c>.</summary>
    abstract Accepts: kind: JournalEntryKind -> bool

    /// <summary>
    /// The application of a <c>kind</c> entry carrying <c>data</c> to the node, or the decode error. A <c>Fail</c>
    /// entry's <c>data</c> is the exception's message.
    /// </summary>
    abstract Prepare: kind: JournalEntryKind * data: string -> Result<unit -> unit, string>

/// <summary>
/// The work a journaled input is attributed to: external input, an async work of the graph, or a replayed entry.
/// </summary>
[<Sealed; AllowNullLiteral>]
type internal JournalAttribution(origin: JournalOrigin, replayed: int, matched: string) =
    /// <summary>The origin an input of the work records.</summary>
    member _.Origin = origin

    /// <summary>The kind of the work in a setup record: <c>effect</c> for an effect's run, <c>work</c> otherwise.</summary>
    member val Kind = "work" with get, set

    /// <summary>The application key of the input that started the work, or null.</summary>
    member val Name: string = null with get, set

    /// <summary>The work's description in a setup record: its kind, followed by its quoted name when it has one.</summary>
    member this.Describe() =
        if isNull this.Name then
            this.Kind
        else
            this.Kind
            + " \""
            + this.Name.Replace("\\", "\\\\").Replace("\"", "\\\"")
            + "\""

    /// <summary>
    /// The <c>Seq</c> of the replayed entry the attribution applies, or 0. A replayed attribution is always admitted.
    /// </summary>
    member _.ReplayedSeq = replayed

    /// <summary>
    /// The key under which a diagnostic replay counts the recorded <c>Produced</c> entries of the work, or null for
    /// work started outside a diagnostic replay.
    /// </summary>
    member _.Match = matched

    /// <summary>The number of calls of the work that made inputs.</summary>
    member val Calls = 0 with get, set

    /// <summary>True while a call of the work runs, such as the body passed to <c>Journal.produce</c>.</summary>
    member val InCall = false with get, set

    /// <summary>The position of the running call among <c>Calls</c>, or -1 before the call's first input.</summary>
    member val Call = -1 with get, set

    /// <summary>
    /// The position of the call an input of the work arriving now belongs to: the running call, or a new call of its
    /// own outside one.
    /// </summary>
    member this.Claim() =
        if this.Call >= 0 then
            this.Call
        else
            let call = this.Calls
            this.Calls <- call + 1

            if this.InCall then
                this.Call <- call

            call

    static member val External = JournalAttribution (JournalOrigin.External, 0, null)

/// <summary>
/// A replay running on a graph: the position of the replayed entries, and the target <c>Seq</c> each applied entry
/// recorded.
/// </summary>
[<Sealed; AllowNullLiteral>]
type internal JournalReplaySession(id: int, diagnostic: bool, setupOpen: bool) =
    let recorded = Dictionary<int, int>()

    member _.Diagnostic = diagnostic

    /// <summary>
    /// True when the target recorded no input before the replay, so its setup work re-runs the recorded run's setup work.
    /// </summary>
    member _.SetupOpen = setupOpen

    /// <summary>The <c>Seq</c> of the last replayed non-<c>Flush</c> entry applied, or -1 before the first.</summary>
    member val Anchor = -1 with get, set

    /// <summary>The number of works started since <c>Anchor</c> was applied.</summary>
    member val Started = 0 with get, set

    /// <summary>The key of the work <c>(started, index)</c> of the replayed entries.</summary>
    member _.Key(started: int, index: int) =
        string id
        + ":"
        + string started
        + ":"
        + string index

    /// <summary>Records that the replayed entry <c>source</c> recorded the target entry <c>target</c>.</summary>
    member _.Recorded(source: int, target: int) =
        recorded[source] <- target

    /// <summary>The target <c>Seq</c> the replayed entry <c>source</c> recorded, or <c>source</c>.</summary>
    member _.TargetOf(source: int) =
        match recorded.TryGetValue source with
        | true, target -> target
        | _ -> source

/// <summary>
/// The async work of the effect body running in the calling context. On .NET it flows with the execution context into
/// every continuation of the body; under Fable it is always null.
/// </summary>
module internal JournalAmbient =
#if FABLE_COMPILER
    let current () : JournalAttribution = null
#else
    let private ambient = AsyncLocal<JournalAttribution>()

    let current () =
        ambient.Value

    let set (work: JournalAttribution) =
        ambient.Value <- work
#endif

/// <summary>
/// A graph's journal state: the journal, the bindings of its keyed input nodes, and the position of the last entry.
/// </summary>
[<Sealed; AllowNullLiteral>]
type internal JournalRecorder(journal: IInputJournal) =
    let bindings = Dictionary<int, JournalBinding>()
    let keys = Dictionary<string, int>()
    let mutable position = 0
    let mutable unflushed = false
    let mutable derivedDepth = 0
    let mutable attribution: JournalAttribution = null
    // The Seq of the last non-Flush entry, and the number of works started after it.
    let mutable anchor = 0
    let mutable started = 0
    let mutable session: JournalReplaySession = null
    let mutable sessions = 0
    let expected = Dictionary<string, int>()
    let mutable nextSite = 0
    // The call of the input last admitted, for the origin it records.
    let mutable inputCall = 0
    // The works started before the first entry, and their setup record, fixed at the first entry.
    let setupWorks = List<JournalAttribution>()
    let mutable setupRecord: string = null

    let describeSetup () =
        String.Join (
            ", ",
            setupWorks
            |> Seq.map (fun work -> work.Describe ())
        )

    /// <summary>
    /// The setup record of the graph: the descriptions of the works it started before its first entry, in start order,
    /// separated by <c>", "</c>. Before the first entry it describes the works started so far.
    /// </summary>
    member _.SetupRecord = if isNull setupRecord then describeSetup () else setupRecord

    /// <summary>The number of entries recorded on the graph, its <c>Setup</c> entry excluded.</summary>
    member _.Position = position

    /// <summary>True when an entry was recorded after the last <c>Flush</c> entry.</summary>
    member _.Unflushed = unflushed

    /// <summary>The mode of the replay running on the graph, if any.</summary>
    member val Replaying: ReplayMode option = None with get, set

    /// <summary>A fresh negative id for an input site that is not a node, such as an MVU loop's messages.</summary>
    member _.NextSiteId() =
        nextSite <- nextSite - 1
        nextSite

    /// <summary>The binding registered under <c>key</c>, or null.</summary>
    member _.BindingOfKey(key: string) : JournalBinding =
        match keys.TryGetValue key with
        | true, nodeId -> bindings[nodeId]
        | _ -> Unchecked.defaultof<JournalBinding>

    /// <summary>The nesting depth of derived sections: bodies that run outside the tracking context.</summary>
    member _.DerivedDepth = derivedDepth

    member _.EnterDerived() =
        derivedDepth <- derivedDepth + 1

    member _.LeaveDerived() =
        derivedDepth <- derivedDepth - 1

    /// <summary>
    /// The next async work on the graph, identified by the last non-<c>Flush</c> entry and its position among the works
    /// started after it. During a diagnostic replay it also carries its identity in the replayed entries.
    /// </summary>
    member _.NextWork(kind: string) =
        let origin = JournalOrigin.Produced (anchor, started, 0)

        let matched =
            if
                not (Platform.isNull session)
                && session.Anchor >= 0
            then
                let key =
                    if session.Diagnostic then
                        session.Key (session.Anchor, session.Started)
                    else
                        null

                session.Started <- session.Started + 1
                key
            elif anchor = 0 then
                // Setup work, started before the graph's first entry, is matched by its position among the setup work.
                JournalRecorder.SetupKey started
            else
                null

        started <- started + 1
        let work = JournalAttribution (origin, 0, matched)
        work.Kind <- kind

        if position = 0 then
            setupWorks.Add work

        work

    static member private SetupKey(index: int) =
        "setup:" + string index

    /// <summary>The key of a call of a work, under which a diagnostic replay counts the call's replayed entries.</summary>
    static member private CallKey(work: string, call: int) =
        work + "/" + string call

    /// <summary>
    /// The key of the call <c>call</c> of a work of the replayed entries, or null for setup work the replay leaves
    /// unmatched.
    /// </summary>
    member private _.ExpectedKey(started: int, index: int, call: int) =
        if started > 0 then
            JournalRecorder.CallKey (session.Key (started, index), call)
        elif session.SetupOpen then
            JournalRecorder.CallKey (JournalRecorder.SetupKey index, call)
        else
            null

    /// <summary>Starts a replay on the graph, and returns the replay it nests in, or null.</summary>
    member _.BeginReplay(mode: ReplayMode) : JournalReplaySession =
        let previous = session
        sessions <- sessions + 1
        session <- JournalReplaySession (sessions, (mode = ReplayMode.Diagnostic), (anchor = 0))
        previous

    /// <summary>Ends the running replay, restoring <c>previous</c>.</summary>
    member _.EndReplay(previous: JournalReplaySession) =
        session <- previous

    /// <summary>Runs <c>run</c>, the application of the replayed entry <c>work</c> applies.</summary>
    member this.RunReplayed(work: JournalAttribution, run: unit -> unit) =
        if not (Platform.isNull session) then
            session.Anchor <- work.ReplayedSeq
            session.Started <- 0

        this.RunAttributed (work, run)

    /// <summary>
    /// Runs <c>run</c>, the body of an effect, as a new async work: on .NET its continuations carry the work.
    /// </summary>
    member this.RunWork(run: unit -> unit) =
        let work = this.NextWork "effect"
#if FABLE_COMPILER
        ignore work
        run ()
#else
        let previous = JournalAmbient.current ()
        JournalAmbient.set work

        try
            run ()
        finally
            JournalAmbient.set previous
#endif

    /// <summary>
    /// Runs <c>run</c> with its inputs attributed to <c>work</c>, as one call of a live work: a call nested in a running
    /// call of the same work joins it.
    /// </summary>
    member _.RunAttributed(work: JournalAttribution, run: unit -> unit) =
        let previous = attribution
        attribution <- work
        let opens = work.ReplayedSeq = 0 && not work.InCall

        if opens then
            work.InCall <- true
            work.Call <- -1

        try
            run ()
        finally
            attribution <- previous

            if opens then
                work.InCall <- false
                work.Call <- -1

    /// <summary>The work an input arriving now is attributed to, or null for an external input.</summary>
    member _.Current: JournalAttribution =
        if Platform.isNull attribution then
            JournalAmbient.current ()
        else
            attribution

    /// <summary>
    /// Counts one replayed <c>Produced</c> entry of the call <c>call</c> of the work <c>(started, index)</c> of the
    /// running replay, for <c>Admit</c>.
    /// </summary>
    member this.Expect(started: int, index: int, call: int) =
        let key = this.ExpectedKey (started, index, call)

        if not (isNull key) then
            match expected.TryGetValue key with
            | true, count -> expected[key] <- count + 1
            | _ -> expected[key] <- 1

    /// <summary>Withdraws one count of <c>Expect</c>, for a replayed entry left unapplied.</summary>
    member this.Unexpect(started: int, index: int, call: int) =
        let key = this.ExpectedKey (started, index, call)

        if not (isNull key) then
            match expected.TryGetValue key with
            | true, count when count > 1 -> expected[key] <- count - 1
            | true, _ -> expected.Remove key |> ignore
            | _ -> ()

    /// <summary>
    /// False when the input arriving now is dropped: it comes from a call of a work re-run by a diagnostic replay, with
    /// an unmatched replayed <c>Produced</c> entry of the same call, which it then matches.
    /// </summary>
    member this.Admit() : bool =
        this.AdmitWork this.Current

    /// <summary>
    /// <c>Admit</c> for an input made inside a computation, a flush or another derived section: only an input of a
    /// <c>Journal.produce</c> call can match a replayed entry, as when a re-run work's await completes synchronously.
    /// </summary>
    member this.AdmitDerived() : bool =
        this.AdmitWork attribution

    member private _.AdmitWork(work: JournalAttribution) : bool =
        if Platform.isNull work || work.ReplayedSeq > 0 then
            true
        else
            inputCall <- work.Claim ()

            if Platform.isNull work.Match then
                true
            else
                let key = JournalRecorder.CallKey (work.Match, inputCall)

                match expected.TryGetValue key with
                | true, count when count > 1 ->
                    expected[key] <- count - 1
                    false
                | true, _ ->
                    expected.Remove key |> ignore
                    false
                | _ -> true

    /// <summary>Registers <c>binding</c> for the node <c>nodeId</c>.</summary>
    /// <exception cref="T:System.ArgumentException">
    /// The graph already has a node with the binding's key, or the node already has a key.
    /// </exception>
    member _.Bind(nodeId: int, binding: JournalBinding) =
        match bindings.TryGetValue nodeId with
        | true, held ->
            raise (
                ArgumentException (
                    "The node is already registered under the journal key \""
                    + held.Key
                    + "\"."
                )
            )
        | _ -> ()

        if keys.ContainsKey binding.Key then
            raise (
                ArgumentException (
                    "The journal key \""
                    + binding.Key
                    + "\" is already registered on this graph."
                )
            )

        keys[binding.Key] <- nodeId
        bindings[nodeId] <- binding

    /// <summary>Removes the binding of the node <c>nodeId</c>, and frees its key for another node.</summary>
    member _.Unbind(nodeId: int, key: string) =
        match keys.TryGetValue key with
        | true, holder when holder = nodeId ->
            keys.Remove key |> ignore
            bindings.Remove nodeId |> ignore
        | _ -> ()

    /// <summary>The binding of the node <c>nodeId</c>, or null when the node is unkeyed.</summary>
    member _.BindingOf(nodeId: int) : JournalBinding =
        match bindings.TryGetValue nodeId with
        | true, binding -> binding
        | _ -> Unchecked.defaultof<JournalBinding>

    member private this.Emit(kind: JournalEntryKind, key: string option, payloadType: string, payload: JournalPayload) =
        if position = 0 then
            setupRecord <- describeSetup ()

            if setupWorks.Count > 0 then
                journal.Record
                    {
                        Schema = 3
                        Stream = journal.Stream
                        Seq = 0
                        Kind = JournalEntryKind.Setup
                        Key = None
                        PayloadType = "setup"
                        Payload = JournalPayload.Encoded ("setup", 1, setupRecord)
                        Origin = JournalOrigin.External
                    }

            setupWorks.Clear ()

        position <- position + 1

        let work = if kind = JournalEntryKind.Flush then null else this.Current

        let origin =
            if Platform.isNull work then
                JournalOrigin.External
            elif work.ReplayedSeq > 0 then
                if not (Platform.isNull session) then
                    session.Recorded (work.ReplayedSeq, position)

                match work.Origin with
                | JournalOrigin.Produced (started, index, call) when not (Platform.isNull session) ->
                    JournalOrigin.Produced (session.TargetOf started, index, call)
                | origin -> origin
            else
                match work.Origin with
                | JournalOrigin.Produced (started, index, _) -> JournalOrigin.Produced (started, index, inputCall)
                | origin -> origin

        if kind <> JournalEntryKind.Flush then
            unflushed <- true
            anchor <- position
            started <- 0

        journal.Record
            {
                Schema = 3
                Stream = journal.Stream
                Seq = position
                Kind = kind
                Key = key
                PayloadType = payloadType
                Payload = payload
                Origin = origin
            }

    /// <summary>
    /// Records <c>value</c> arriving at the node <c>nodeId</c>: encoded by the node's codec when the node is keyed and the
    /// codec succeeds, and non-replayable otherwise. A <c>Fail</c> entry of a keyed node carries the exception's message
    /// under the <c>exn</c> codec, version 1.
    /// </summary>
    member this.Input(kind: JournalEntryKind, nodeId: int, value: obj) =
        let payloadType = Platform.typeName value
        let binding = this.BindingOf nodeId

        if Platform.isNull (box binding) then
            this.Emit (kind, None, payloadType, JournalPayload.NonReplayable payloadType)
        else
            let payload =
                try
                    if kind = JournalEntryKind.Fail then
                        JournalPayload.Encoded ("exn", 1, (value :?> exn).Message)
                    else
                        JournalPayload.Encoded (binding.Codec, binding.Version, binding.Encode value)
                with _ ->
                    JournalPayload.NonReplayable payloadType

            this.Emit (kind, Some binding.Key, payloadType, payload)

    /// <summary>Records a <c>Flush</c> entry when an entry was recorded since the previous one.</summary>
    member this.Boundary() =
        if unflushed then
            unflushed <- false
            this.Emit (JournalEntryKind.Flush, None, "", JournalPayload.Empty)


// Normalizes the intrinsic fast-path comparer (EqualityComparer<'T>.Default / ReferenceComparer<'T> on .NET,
// ReferenceComparer<'T> on Fable) to null so hot paths avoid interface dispatch and generic dictionary lookups,
// while preserving NullReferenceException for a faulty policy.
[<Sealed>]
type internal NullComparer<'T>() =
    static member val Instance: IEqualityComparer<'T> = NullComparer<'T>() :> IEqualityComparer<'T>

    interface IEqualityComparer<'T> with
        member _.Equals(_, _) =
            raise (NullReferenceException ())

        member _.GetHashCode(_) =
            raise (NullReferenceException ())

#if FABLE_COMPILER
module internal ComparerSentinel =
    let inline encode (comparer: IEqualityComparer<'T>) : IEqualityComparer<'T> =
        if obj.ReferenceEquals (comparer, null) then
            NullComparer<'T>.Instance
        elif (box comparer) :? ReferenceComparerBase then
            Unchecked.defaultof<IEqualityComparer<'T>>
        else
            comparer

    let inline equals (encoded: IEqualityComparer<'T>) (a: 'T) (b: 'T) : bool =
        if Platform.isNull (box encoded) then
            obj.ReferenceEquals (box a, box b)
        else
            encoded.Equals (a, b)
#else
module internal ComparerSentinel =
    let encode (comparer: IEqualityComparer<'T>) : IEqualityComparer<'T> =
        if obj.ReferenceEquals (comparer, null) then
            NullComparer<'T>.Instance
        elif typeof<'T>.IsValueType then
            if obj.ReferenceEquals (comparer, EqualityComparer<'T>.Default) then
                null
            else
                comparer
        elif comparer :? ReferenceComparer<'T> then
            null
        else
            comparer

    let inline equals (encoded: IEqualityComparer<'T>) (a: 'T) (b: 'T) : bool =
        if typeof<'T>.IsValueType then
            if obj.ReferenceEquals (encoded, null) then
                EqualityComparer<'T>.Default.Equals(a, b)
            else
                encoded.Equals (a, b)
        elif obj.ReferenceEquals (encoded, null) then
            obj.ReferenceEquals (box a, box b)
        else
            encoded.Equals (a, b)
#endif

#if !FABLE_COMPILER
/// <summary>
/// Whether <c>EqualityComparer&lt;'T&gt;.Default</c> cannot raise for <c>'T</c>: a reference type, compared by reference
/// when encoded as null, or a primitive, enum or framework value type. A user struct's <c>Equals</c> may raise.
/// </summary>
/// <remarks>
/// Lets a cutoff with the encoded null comparer compare without an exception handler. No reflection beyond the
/// type's own flags, so the assembly stays AOT-clean.
/// </remarks>
[<AbstractClass; Sealed>]
type internal NonThrowingDefaultEq<'T> private () =
    static member val IsSafe: bool =
        not typeof<'T>.IsValueType
        || typeof<'T>.IsPrimitive
        || typeof<'T>.IsEnum
        || typeof<'T> = typeof<decimal>
        || typeof<'T> = typeof<Guid>
        || typeof<'T> = typeof<DateTime>
        || typeof<'T> = typeof<DateTimeOffset>
        || typeof<'T> = typeof<TimeSpan>
#endif

module internal SafeEquality =
    /// <summary>
    /// True when <c>encoded</c> is the null-encoded intrinsic comparer and comparing with it cannot raise, so the
    /// caller may compare outside an exception handler.
    /// </summary>
#if FABLE_COMPILER
    let inline isSafe (encoded: IEqualityComparer<'T>) = Platform.isNull (box encoded)
#else
    let inline isSafe (encoded: IEqualityComparer<'T>) =
        obj.ReferenceEquals (encoded, null)
        && (not typeof<'T>.IsValueType
            // Folded by the JIT for the common primitives, so the cached flag is read only for other value types.
            || Type.op_Equality (typeof<'T>, typeof<int>)
            || Type.op_Equality (typeof<'T>, typeof<int64>)
            || Type.op_Equality (typeof<'T>, typeof<bool>)
            || Type.op_Equality (typeof<'T>, typeof<float>)
            || NonThrowingDefaultEq<'T>.IsSafe)
#endif

#if FABLE_COMPILER
namespace rec Ranvier

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open FableLocks
open CoreTypes

// Fickl checks the view and core sources together. Keep the graph's Node alias local to this file.
module private CoreTypes =
    type Node = ComputationNode
#endif

/// <summary>
/// Owns the tracking context and the construction-time policy. One graph is a
/// single consistency domain; on .NET that also means a single thread of
/// mutation, which is what <c>GraphOptions.Dispatcher</c> exists to enforce.
/// </summary>
type Graph(options: GraphOptions) =
    let mutable nextId = 0
    let mutable staleTransitions = 0
    let mutable activeSkips = 0

    /// <summary>
    /// Null rather than <c>IComputation option</c>. The option was 24 bytes of
    /// garbage per tracked run — allocated on the way in to every memo
    /// recomputation and every effect run, which is the hottest path the
    /// library has.
    /// </summary>
    /// <remarks>
    /// <c>Unchecked.defaultof</c> rather than <c>null</c>, and <c>ReferenceEquals</c> rather
    /// than <c>isNull</c>, because F# interfaces do not admit the null literal
    /// without <c>AllowNullLiteral</c> — and that attribute would have to cascade
    /// to <c>INode</c> and <c>ISource</c>, weakening three public types to save one
    /// private field an unchecked cast.
    /// </remarks>
    let mutable current: IComputation = Unchecked.defaultof<IComputation>

    /// <summary>
    /// The <c>Node</c> of <c>current</c> when a memo, effect or split effect entered through <c>EnterHostedNode</c>,
    /// otherwise null. Read by <c>Track</c> to match the read against the node's source list without dispatching
    /// through <c>IComputation.AddSource</c>.
    /// </summary>
    /// <remarks>
    /// Every write of <c>current</c> writes this too: null, or the node of the computation written. Null beside a
    /// node-backed <c>current</c> is allowed and only takes the dispatching path.
    /// </remarks>
    let mutable currentNode: Node = Unchecked.defaultof<Node>

    /// <summary>
    /// The node of the last pending read raised inside the running body, or
    /// null. Survives a <c>try/with</c> in the body that swallows the raise.
    /// </summary>
    let mutable raisedPending: INode = Unchecked.defaultof<INode>

#if !FABLE_COMPILER && !RANVIER_TRACE
    let mutable caught: exn = null
#endif

    /// <summary>
    /// The failure of the last failed read inside a stale read or a flush, or null. Taken by the next run that catches a
    /// failure, and cleared then and at the end of the outermost stale read and of each flush.
    /// </summary>
    /// <remarks>
    /// Every computation body runs inside a stale read or a flush, so the slot is null whenever the graph is idle.
    /// </remarks>
    let mutable lastRaised: Failure = null

    /// <summary>
    /// Effects invalidated in the current turn, waiting to be run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A list read through a cursor rather than a <c>Queue</c>. Both are O(1) at
    /// either end, but <c>Queue</c> carries head/tail arithmetic and a slot clear on
    /// every dequeue, and under Fable each of those lands on fable-library's
    /// bounds-checked <c>item</c> — about 7% of a write-heavy profile between them.
    /// A cursor is one indexed read and an increment.
    /// </para>
    /// <para>
    /// The list is never cleared and never shrinks: it is a buffer whose
    /// logical length is <c>queueCount</c>, and <c>Schedule</c> overwrites a spent slot
    /// before it appends. <c>Clear</c> was the first shape tried and it cost more
    /// than the <c>Queue</c> did — under Fable it is a <c>splice(0)</c> allocation, paid
    /// once per flush, which on a one-observer write is the whole op.
    /// Releasing each slot as it is consumed keeps the references collectable
    /// for the price of one store.
    /// </para>
    /// </remarks>
#if FABLE_COMPILER
    let queue = ResizeArray<IScheduled>()
#else
    let mutable queueSlots: QueueSlot[] = Array.Empty<QueueSlot>()
#endif

    /// <summary>
    /// Entries written. Slots at or past this are spent and reusable.
    /// </summary>
    let mutable queueCount = 0

    /// <summary>
    /// Next entry to run. Between flushes it is 0, except after a throw.
    /// </summary>
    let mutable queueHead = 0

    /// <summary>
    /// Queued projection passes and lookup refreshes, read through <c>passHead</c> and <c>passCount</c> as
    /// <c>queue</c> is. A flush runs every queued pass before the next <c>queue</c> item.
    /// </summary>
    /// <remarks>
    /// <para>A reader run ahead of a queued pass reads the previous keys, rows or lookup values.</para>
    /// <para>
    /// While a pass is queued, <c>queue</c> holds <c>passDrain</c> at or after <c>queueHead</c>, so a non-empty
    /// <c>queue</c> is the one test for queued work. A pass queued while an item runs puts <c>passDrain</c> at the
    /// head of <c>queue</c>, and one queued outside a flush puts it at the tail. A pass queued while
    /// <c>RunPasses</c> runs joins that run. A flush runs the passes queued before it starts ahead of its first item.
    /// </para>
    /// </remarks>
    let passes = ResizeArray<IScheduled>()

    /// <summary>Entries written to <c>passes</c>. Reset to 0 when <c>RunPasses</c> drains the buffer.</summary>
    let mutable passCount = 0

    /// <summary>Next entry of <c>passes</c> to run.</summary>
    let mutable passHead = 0

    /// <summary>The <c>queue</c> item that runs <c>RunPasses</c>. Created by the first queued pass.</summary>
    let mutable passDrain: IScheduled = Unchecked.defaultof<IScheduled>

    /// <summary>True while <c>RunPasses</c> runs; a pass queued then runs in the same <c>RunPasses</c>.</summary>
    let mutable runningPasses = false

#if !FABLE_COMPILER
    /// <summary>
    /// The thread the graph was constructed on. Everything mutating the graph
    /// must end up here; see <c>Dispatch</c>.
    /// </summary>
    let ownerThread = Platform.currentThreadId ()
    let ownerToken = OwnerThreadToken.Ensure ()
#endif
    let guarded = options.ThreadAffinity = Guarded

    /// <summary>
    /// The owner thread's ambient cell when the graph is guarded, otherwise
    /// null: an unguarded graph resolves the calling thread's cell each time.
    /// </summary>
    let ownerAmbient = if guarded then AmbientSlot.Cell else null

#if !FABLE_COMPILER
    let serialised = options.ThreadAffinity = Serialised

    /// <summary>
    /// The synchronisation context every entry of a <c>Serialised</c> graph must run on.
    /// </summary>
    let context = SynchronizationContext.Current

    /// <summary>
    /// The id of the thread inside a <c>Serialised</c> graph, or 0 when the graph is free.
    /// </summary>
    let mutable holder = 0
#endif

#if !FABLE_COMPILER
    /// <summary>
    /// True when the outermost stale read acquired the graph and its <c>ExitPull</c> releases it.
    /// </summary>
    let mutable pullHeld = false
#endif

    let mutable batchDepth = 0
    let mutable flushing = false

    /// <summary>The recorder of <c>options.Journal</c>, or null when the graph has no journal.</summary>
    let journal =
        match options.Journal with
        | Some inputJournal -> JournalRecorder inputJournal
        | None -> null

#if !FABLE_COMPILER
    /// <summary>
    /// Encodes unjournaled non-<c>Serialised</c> write eligibility into one <c>int64</c> load:
    /// <c>-1L</c> when <c>Serialised</c> or journaled, <c>ownerToken</c> (<c>&gt;= 1L</c>) when <c>Guarded</c> and unjournaled,
    /// and <c>0L</c> when <c>Unchecked</c> and unjournaled.
    /// </summary>
    let fastWriteToken =
        if serialised || not (Platform.isNull journal) then -1L
        elif guarded then ownerToken
        else 0L
#endif

    /// <summary>
    /// The scope everything created outside an explicit <c>Root</c> belongs to.
    /// Solid warns in this case and leaks; giving the graph a root instead
    /// means <c>graph.Dispose()</c> always tears everything down, and a leak is a
    /// test assertion rather than a console message.
    /// </summary>
    let root = new Owner ()
    do Tracer.GraphNew (root, guarded)

    /// <summary>
    /// The owner a node created now attaches to. Null while a scope host's
    /// body runs and has not yet created anything: <c>CurrentOwner</c> then
    /// allocates the scope of the running computation.
    /// </summary>
    let mutable currentOwner = root

    /// <summary>
    /// True while a hosted body runs and has not yet resolved its owner: the owner is then null, whatever
    /// <c>currentOwner</c> still holds. Set by entering a hosted run in place of storing null, so a run that creates
    /// nothing leaves <c>currentOwner</c> untouched and its exit stores no reference.
    /// </summary>
    /// <remarks>
    /// Every write of <c>currentOwner</c> clears it, and every save and restore of <c>currentOwner</c> saves and
    /// restores it too.
    /// </remarks>
    let mutable ownerPending = false

#if !FABLE_COMPILER
    /// <summary>
    /// Saved on entry to each flight continuation, keyed by the body's frame: the tracking
    /// context, owner and raised pending read. Innermost last.
    /// </summary>
    let mutable continuations: ResizeArray<struct (obj * IComputation * Owner * INode)> =
        null

    let mutable enterContinuationDelegate: obj -> bool = Unchecked.defaultof<_>
    let mutable leaveContinuationDelegate: obj -> unit = Unchecked.defaultof<_>
#endif

    /// <summary>
    /// The scope host whose body entered <c>untrack</c> before creating anything.
    /// Read only while both the owner and the tracking context are null.
    /// </summary>
    let mutable untrackedHost: IScopeHost = Unchecked.defaultof<IScopeHost>

    /// <summary>
    /// True inside an <c>untrack</c> block entered from a computation's body.
    /// </summary>
    let mutable untrackedInBody = false

    /// <summary>
    /// True when a flush was requested while a body ran or a stale read was
    /// bringing a computation current. The outermost such read runs it.
    /// </summary>
    let mutable flushOwed = false

    /// <summary>
    /// The number of stale reads in progress: computations bringing
    /// themselves current.
    /// </summary>
    let mutable pullDepth = 0

    /// <summary>
    /// The <c>Hosting</c> graph the outermost stale read replaced.
    /// </summary>
    let mutable pullAmbient: obj = null

    /// <summary>
    /// The number of teardowns in progress.
    /// </summary>
    let mutable detachedDepth = 0

#if !FABLE_COMPILER
    /// <summary>
    /// Work that arrived from another thread, waiting for this one to come and
    /// run it. See <c>Platform.Inbox</c> for why it is the queue it is.
    /// </summary>
    let inbox = Platform.Inbox<unit -> unit>()

    /// <summary>
    /// The <c>DispatchAsync</c> items in the inbox, faulted at disposal. Its lock also guards <c>dispatchClosed</c>.
    /// </summary>
    let pendingDispatches = HashSet<IPendingDispatch>(HashIdentity.Reference)

    /// <summary>
    /// True once disposal has faulted <c>pendingDispatches</c>; a later off-thread <c>DispatchAsync</c> faults at
    /// the call.
    /// </summary>
    let mutable dispatchClosed = false

    /// <summary>Cached <c>Action</c> delegate targeting <c>PumpFromDispatcher</c>, allocated on first post.</summary>
    let mutable pumpAction: Action = null
#endif

    let dispatcher =
        match options.Dispatcher with
        | Some d -> d
        | None -> Platform.defaultDispatcher ()

#if FABLE_COMPILER
    let usesJsIdentity = (box options.Equality) :? JsIdentityPolicy
#endif

    new() = new Graph (GraphOptions.Default)

    member _.Options = options

#if FABLE_COMPILER
    member internal _.EncodedComparer<'T>() : IEqualityComparer<'T> =
        if usesJsIdentity then
            Unchecked.defaultof<IEqualityComparer<'T>>
        else
            ComparerSentinel.encode (options.Equality.Comparer<'T>())
#else
    member inline internal this.EncodedComparer<'T>() : IEqualityComparer<'T> =
        ComparerSentinel.encode (this.Options.Equality.Comparer<'T>())
#endif

    /// <summary>
    /// The dispatcher this graph resolved at construction.
    /// </summary>
    member _.Dispatcher = dispatcher

    member _.Root = root

    /// <summary>
    /// The ambient graph on this thread: the graph running a body, a flush or
    /// a stale read, otherwise the activated one.
    /// </summary>
    static member TryCurrent =
        match Graph.Ambient with
        | :? Graph as g -> ValueSome g
        | _ -> ValueNone

#if !FABLE_COMPILER
    /// <summary>
    /// True with the ambient graph on this thread, as <c>TryCurrent</c> resolves it; otherwise false with <c>null</c>.
    /// </summary>
    static member TryGetCurrent([<System.Runtime.InteropServices.Out>] graph: byref<Graph>) : bool =
        match Graph.Ambient with
        | :? Graph as g ->
            graph <- g
            true
        | _ ->
            graph <- Unchecked.defaultof<Graph>
            false
#endif

    /// <summary>
    /// The ambient graph on this thread.
    /// </summary>
    /// <remarks>
    /// Throws rather than creating one on demand. A graph nobody constructed is
    /// a graph nobody disposes, and every effect it owns would outlive the
    /// scope the caller believes they wrote.
    /// </remarks>
    static member Current =
        match Graph.Ambient with
        | :? Graph as g -> g
        | _ ->
            raise (
                InvalidOperationException
                    "No ambient graph on this thread. Activate one with `use _ = graph.Activate ()`, or construct nodes against an explicit graph."
            )

    /// <summary>
    /// Makes this the ambient graph until the returned handle is disposed,
    /// restoring whatever was ambient before.
    /// </summary>
    /// <remarks>
    /// A stack rather than an assignment, so a library that activates its own
    /// graph for the length of a call cannot strand its caller's. On .NET the
    /// activation flows with the execution context, so it may span an <c>await</c>. A guarded graph is ambient on the
    /// activating thread only; an <c>Unchecked</c> or <c>Serialised</c> graph is ambient on every thread its execution
    /// context reaches.
    /// </remarks>
    member this.Activate() =
        let cell = AmbientSlot.Cell
        let activated = Activation.current ()
        let hosting = cell.Hosting

        let entry =
            new ActivationEntry (this, Platform.currentThreadId (), not guarded, activated, cell, hosting)

        Activation.set entry
        cell.Hosting <- null
        entry :> IDisposable

    /// <summary>
    /// The calling context's ambient graph, or null.
    /// </summary>
    static member private Ambient: obj =
        let cell = AmbientSlot.Existing

        match (if Platform.isNull cell then null else cell.Hosting) with
        | :? Graph as g when g.Running -> g
        | _ ->
            match Activation.current () with
            | :? ActivationEntry as entry -> entry.GraphOnCurrentThread
            | _ -> null

    /// <summary>
    /// True while the graph runs a body, a flush, a stale read or a teardown.
    /// </summary>
    member internal _.Running =
        flushing
        || pullDepth > 0
        || not (obj.ReferenceEquals (currentNode, null))
        || not (Platform.isNull (box current))
        || detachedDepth > 0
        || untrackedInBody

    /// <summary>The graph's journal recorder, or null when the graph has no journal.</summary>
    /// <remarks>Inline, so a guarded hook reads the field directly under Fable instead of calling a getter.</remarks>
    member inline internal _.Journal = journal

#if !FABLE_COMPILER
    member inline internal _.FastWriteToken = fastWriteToken
#endif

    /// <summary>
    /// Records <c>value</c> arriving at the node <c>nodeId</c> as a <c>kind</c> entry, unless a computation, flush,
    /// stale read, teardown or other derived section is running. Requires a journal.
    /// </summary>
    /// <returns>False when a diagnostic replay drops the input, which the caller then leaves unapplied.</returns>
    member internal this.RecordInput(kind: JournalEntryKind, nodeId: int, value: obj) : bool =
        if this.Running || journal.DerivedDepth > 0 then
            journal.AdmitDerived ()
        elif journal.Admit () then
            journal.Input (kind, nodeId, value)
            true
        else
            false

    /// <summary>
    /// Makes this graph ambient and returns the graph it replaced in <c>Hosting</c>.
    /// </summary>
    member inline private this.EnterAmbient() : obj =
        let cell =
            if Platform.isNull ownerAmbient then
                AmbientSlot.Cell
            else
                ownerAmbient

        let previous = cell.Hosting

        if not (obj.ReferenceEquals (previous, this)) then
            cell.Hosting <- this

        previous

    /// <summary>
    /// Restores the enclosing host. A guarded graph can stay cached on its owner thread between runs.
    /// </summary>
    member inline private this.LeaveAmbient(previous: obj) =
#if !FABLE_COMPILER
        if Platform.isNull ownerAmbient then
            if not (obj.ReferenceEquals (previous, this)) then
                AmbientSlot.Cell.Hosting <- previous
        else
#endif
        if not (obj.ReferenceEquals (previous, this)) then
            match previous with
            | :? Graph as g when g.Running ->
                let cell =
                    if Platform.isNull ownerAmbient then
                        AmbientSlot.Cell
                    else
                        ownerAmbient

                cell.Hosting <- previous
            | _ -> ()

#if !FABLE_COMPILER
    /// <summary>
    /// Makes the calling thread the graph's holder under <c>Serialised</c>.
    /// </summary>
    /// <returns>True when this call entered a free graph and must be paired with <c>Release</c>.</returns>
    /// <exception cref="T:System.InvalidOperationException">
    /// The caller runs outside the construction context, or another thread holds the graph.
    /// </exception>
    member private this.Acquire(operation: string) : bool =
        let me = Platform.currentThreadId ()

        if Volatile.Read &holder = me then
            false
        else
            this.CheckContext operation
            let prior = Interlocked.CompareExchange (&holder, me, 0)

            if prior <> 0 then
                this.FailConcurrent (operation, prior)

            true
#endif

    /// <summary>
    /// Frees a graph entered by <c>Acquire</c>, and posts a drain when the inbox holds work.
    /// </summary>
    member internal this.Release() =
#if !FABLE_COMPILER
        // A full fence: the inbox check below observes every enqueue whose drain found the graph held by this thread.
        Interlocked.Exchange (&holder, 0) |> ignore

        if not inbox.IsEmpty then
            this.PostDrain ()
#else
        ()
#endif

#if !FABLE_COMPILER
    member private this.PostDrain() =
        let mutable action = pumpAction

        if Platform.isNull action then
            action <- Action this.PumpFromDispatcher
            pumpAction <- action

        dispatcher.Post action
#endif

    /// <summary>
    /// Checks the caller as <c>AssertOnGraphThread</c> does, and under <c>Serialised</c> enters the graph.
    /// </summary>
    /// <returns>True when the caller must pair it with <c>Release</c>.</returns>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.Enter(operation: string) : bool =
#if FABLE_COMPILER
        ignore operation
        false
#else
        if guarded then
            if OwnerThreadToken.Current <> ownerToken then
                this.FailOffThread operation

            false
        else
            serialised && this.Acquire operation
#endif

    /// <summary>
    /// Runs <c>body</c> as the entry point <c>operation</c>: checked under <c>Guarded</c>, held under
    /// <c>Serialised</c>.
    /// </summary>
    member inline internal this.Entered<'T>(operation: string, [<InlineIfLambda>] body: unit -> 'T) : 'T =
#if FABLE_COMPILER
        body ()
#else
        if not (this.Enter operation) then
            body ()
        else
            try
                body ()
            finally
                this.Release ()
#endif

    member internal _.CurrentOwner =
        if ownerPending || Platform.isNull (box currentOwner) then
            let host =
                if not (obj.ReferenceEquals (currentNode, null)) then
                    currentNode.Self :?> IScopeHost
                elif Platform.isNull (box current) then
                    untrackedHost
                else
                    current :?> IScopeHost

            currentOwner <- host.Scope
            ownerPending <- false

        currentOwner

    /// <summary>
    /// <c>CurrentOwner</c>, or <c>ValueNone</c> inside a pure body, where <c>CurrentOwner</c> raises and fails the run.
    /// </summary>
    member internal _.TryCurrentOwner: Owner voption =
        if not ownerPending && not (Platform.isNull (box currentOwner)) then
            ValueSome currentOwner
        else
            let host =
                if not (obj.ReferenceEquals (currentNode, null)) then
                    currentNode.Self :?> IScopeHost
                elif Platform.isNull (box current) then
                    untrackedHost
                else
                    current :?> IScopeHost

            if not (Platform.isNull (box host)) && host.CanOwn then
                currentOwner <- host.Scope
                ownerPending <- false
                ValueSome currentOwner
            else
                ValueNone

    /// <summary>
    /// Creates a nested scope, runs <c>body</c> inside it, and hands back the owner
    /// so the caller can dispose the whole subtree at once.
    /// </summary>
    member this.CreateRoot(body: Func<Owner, 'T>) =
        this.RunRoot body.Invoke

    member internal this.RunRoot(body: Owner -> 'T) =
        this.Entered (
            "Creating a root",
            fun () ->
                let owner = new RootScope (this) :> Owner
                owner.SetParent (this.CurrentOwner.AttachLinked owner)
                let previous = currentOwner
                currentOwner <- owner

                try
                    body owner
                finally
                    currentOwner <- previous
        )

    /// <summary>
    /// Registers a cleanup with the innermost enclosing scope. Inside the body
    /// of an effect, memo, async memo or boundary that is the computation's own
    /// scope, so the cleanup runs before the next re-run as well as at disposal.
    /// On a disposed scope <c>f</c> runs immediately, untracked and with effects
    /// deferred, as a teardown runs it.
    /// </summary>
    member this.OnCleanup(f: Action) =
        this.AddCleanup f.Invoke

    member internal this.AddCleanup(f: unit -> unit) =
        this.Entered (
            "Registering a cleanup",
            fun () ->
                let owner = this.CurrentOwner

                if owner.IsDisposed then
                    this.DetachedCleanup (owner, f)
                    this.RequestFlush ()
                else
                    owner.AddCleanup f
        )

    member internal _.RunOwned(owner: Owner, body: unit -> 'T) =
        let previous = currentOwner
        let previousPending = ownerPending
        currentOwner <- owner
        ownerPending <- false

        try
            body ()
        finally
            currentOwner <- previous
            ownerPending <- previousPending

    /// <summary>Applies <c>f</c> to <c>arg</c> with <c>owner</c> as the current owner.</summary>
    member internal _.RunOwnedWith(owner: Owner, f: 'A -> unit, arg: 'A) =
        let previous = currentOwner
        let previousPending = ownerPending
        currentOwner <- owner
        ownerPending <- false

        try
            f arg
        finally
            currentOwner <- previous
            ownerPending <- previousPending

    /// <summary>
    /// Tears down every scope the graph owns, and faults the task of every <c>DispatchAsync</c> item still in the
    /// inbox with <c>ObjectDisposedException</c>.
    /// </summary>
    member this.Dispose() =
        this.Entered (
            "Disposing a graph",
            fun () ->
                root.Dispose ()
#if !FABLE_COMPILER
                let abandoned =
                    lock pendingDispatches (fun () ->
                        dispatchClosed <- true
                        let items = Seq.toArray pendingDispatches
                        pendingDispatches.Clear ()
                        items)

                for item in abandoned do
                    item.Abandon (ObjectDisposedException (nameof Graph))
#endif

                if
                    not (Platform.isNull ownerAmbient)
                    && obj.ReferenceEquals (ownerAmbient.Hosting, this)
                then
                    ownerAmbient.Hosting <- null
        )

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

#if RANVIER_TRACE
    interface ITraced with
        member _.TraceLog
            with get () = (root :> ITraced).TraceLog
            and set log = (root :> ITraced).TraceLog <- log

        member _.TraceId
            with get () = 0
            and set _ = ()
#endif

    /// <summary>
    /// True when the calling thread is the graph's owner thread; under <c>Serialised</c>, when the calling thread is
    /// inside the graph.
    /// </summary>
    member _.IsOnGraphThread =
#if FABLE_COMPILER
        true
#else
        if serialised then
            Volatile.Read &holder = Platform.currentThreadId ()
        else
            Platform.currentThreadId () = ownerThread
#endif

    /// <summary>
    /// Marshals <c>work</c> into the graph's consistency domain, running it inline
    /// when already there.
    /// </summary>
    /// <remarks>
    /// The inline fast path is the whole design. A cross-thread hop measured
    /// 2.35–3.32 µs, against 3.2 µs for a throw through ten frames — marshalling
    /// costs what suspension costs, and the queue primitive used barely matters
    /// because the consumer wake-up dominates it by ~100x. So the dispatcher's
    /// job is to be skipped, not to be fast. See
    /// docs/.ai/RESEARCH-loony-synchronization.md §4.3.
    /// </remarks>
    member this.Dispatch(work: Action) =
        this.Post work.Invoke

    member internal this.Post(work: unit -> unit) =
#if FABLE_COMPILER
        work ()
#else
        if this.IsOnGraphThread then
            work ()
        else
            this.PostQueued work
#endif

#if !FABLE_COMPILER
    // Out of line, so Post stays small enough to inline into its callers.
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member internal this.PostQueued(work: unit -> unit) =
        // The work goes into the graph's inbox; the dispatcher receives
        // only the drain. ImmediateDispatcher drains on this thread: under
        // Unchecked the work runs here, and under Guarded the pump raises
        // and the work stays queued.
        let attributed =
            if Platform.isNull journal then
                null
            else
                JournalAmbient.current ()

        if Platform.isNull attributed then
            inbox.Enqueue work
        else
            inbox.Enqueue (fun () -> journal.RunAttributed (attributed, work))

        this.PostDrain ()
#endif

    /// <summary>
    /// Marshals <c>work</c> as <c>Dispatch</c> does. The returned task completes once
    /// <c>work</c> has run, whether or not it threw.
    /// </summary>
    member internal this.DispatchApplied(work: unit -> unit) : Task =
#if FABLE_COMPILER
        this.Post work
        Platform.completedTask
#else
        if this.IsOnGraphThread then
            work ()
            Platform.completedTask
        else
            let applied = TaskCompletionSource ()

            this.Post (fun () ->
                try
                    work ()
                finally
                    applied.SetResult ())

            applied.Task
#endif

    /// <summary>
    /// Runs <c>work</c> in the graph's consistency domain, as <c>Dispatch</c> does, and returns a task that completes
    /// with its result once it has run.
    /// </summary>
    /// <remarks>
    /// On the graph thread the work runs before the call returns and the task is already complete. From another
    /// thread the work joins the inbox behind earlier <c>Dispatch</c> and <c>DispatchAsync</c> items and runs on the
    /// drain the dispatcher schedules; under <c>ManualDispatcher</c> that is the owner's next <c>Pump</c>. Continuations
    /// of a queued item's task run on the thread pool, outside the drain. A continuation attached to an inline call's
    /// task, by <c>await</c> or <c>ExecuteSynchronously</c>, runs at once on the graph thread, inside the graph.
    /// Blocking on the task from the owning thread deadlocks when that thread is the only one that drains.
    ///
    /// Completion marks the end of the work, not of what it triggers. Effects woken by its writes run as they do for
    /// <c>Dispatch</c> work: at the write, or at the end of a batch the work opens, before the task completes. An
    /// inline call inside an enclosing batch completes its task first, and that batch's flush runs the effects. Async
    /// memos the work starts settle later.
    ///
    /// When the work throws, the task faults with that exception, and the caller is its only recipient:
    /// <c>Root.Errors</c> stays unchanged and later inbox items still run.
    ///
    /// On a disposed graph the task faults with <c>ObjectDisposedException</c> and the work does not run. Disposal
    /// faults the task of every item still in the inbox in the same way, without waiting for a drain.
    /// </remarks>
    member this.DispatchAsync(work: Func<'T>) : Task<'T> =
        this.DispatchAsync (work, CancellationToken.None)

    /// <summary>
    /// Runs <c>work</c> in the graph's consistency domain as the single-argument overload does, and cancels the task
    /// instead when <c>cancellationToken</c> is cancelled before the work starts.
    /// </summary>
    /// <remarks>
    /// A token already cancelled at the call returns a cancelled task and queues nothing. A token cancelled while the
    /// item waits in the inbox cancels the task at once, and the drain skips the work. Once the work starts it runs to
    /// completion and the token no longer applies. Cancellation is checked before disposal.
    /// </remarks>
    member this.DispatchAsync(work: Func<'T>, cancellationToken: CancellationToken) : Task<'T> =
#if FABLE_COMPILER
        let finished = Platform.completion<'T>()

        if cancellationToken.IsCancellationRequested then
            finished.Reject (OperationCanceledException "The operation was canceled.")
        elif root.IsDisposed then
            finished.Reject (ObjectDisposedException (nameof Graph))
        else
            let outcome =
                try
                    Result.Ok (work.Invoke ())
                with ex ->
                    Result.Error ex

            match outcome with
            | Result.Ok value -> finished.Resolve value
            | Result.Error ex -> finished.Reject ex

        finished.Task
#else
        if cancellationToken.IsCancellationRequested then
            Task.FromCanceled<'T> cancellationToken
        elif root.IsDisposed then
            Task.FromException<'T>(ObjectDisposedException (nameof Graph))
        elif this.IsOnGraphThread then
            let outcome =
                try
                    Result.Ok (work.Invoke ())
                with ex ->
                    Result.Error ex

            match outcome with
            | Result.Ok value -> Task.FromResult value
            | Result.Error ex -> Task.FromException<'T> ex
        else
            this.EnqueueDispatch (work, cancellationToken)

    /// <summary>
    /// Queues <c>work</c> from another thread as a <c>DispatchAsync</c> item, registered for disposal and
    /// cancellation.
    /// </summary>
    member private this.EnqueueDispatch(work: Func<'T>, cancellationToken: CancellationToken) : Task<'T> =
        let pending = PendingDispatch<'T>()

        let admitted =
            lock pendingDispatches (fun () ->
                not dispatchClosed
                && pendingDispatches.Add pending)

        if not admitted then
            Task.FromException<'T>(ObjectDisposedException (nameof Graph))
        else
            let forget () =
                lock pendingDispatches (fun () -> pendingDispatches.Remove pending |> ignore)

            let registration =
                if cancellationToken.CanBeCanceled then
                    cancellationToken.Register (fun () ->
                        pending.Cancel cancellationToken
                        forget ())
                else
                    new CancellationTokenRegistration ()

            this.Post (fun () ->
                if pending.TryClaim () then
                    forget ()
                    registration.Dispose ()

                    let outcome =
                        try
                            Result.Ok (work.Invoke ())
                        with ex ->
                            Result.Error ex

                    match outcome with
                    | Result.Ok value -> pending.SetResult value
                    | Result.Error ex -> pending.SetException ex)

            pending.Task
#endif

    /// <summary>
    /// Work that arrived from another thread and has not been drained yet.
    /// </summary>
    member _.PendingWork =
#if FABLE_COMPILER
        0
#else
        inbox.Count
#endif

    /// <summary>
    /// Runs everything the inbox has collected, then flushes. Must be called on
    /// the graph's own thread — that is the entire point of the inbox.
    /// </summary>
    /// <remarks>
    /// Returns the number of items run, so a caller draining in a loop can tell
    /// progress from a spin.
    /// </remarks>
    member this.Pump() =
        this.Entered ("Pump", (fun () -> this.Drain ()))

    member private this.Drain() =
#if FABLE_COMPILER
        0
#else
        let mutable ran = 0
        let mutable draining = true

        // An inbox item carries its own attribution from Post, so the drain
        // runs outside the work of the context that posted the drain.
        let carried =
            if Platform.isNull journal then
                null
            else
                JournalAmbient.current ()

        if not (Platform.isNull carried) then
            JournalAmbient.set null

        try
            while draining do
                match inbox.TryTake () with
                | ValueNone -> draining <- false
                | ValueSome work ->
                    ran <- ran + 1

                    // One bad continuation must not strand the rest of the inbox,
                    // for the same reason one bad effect must not strand the flush
                    // queue.
                    try
                        work ()
                    with ex ->
                        root.RecordError ex

            if ran > 0 then
                this.RequestFlushFromEntered ()
        finally
            if not (Platform.isNull carried) then
                JournalAmbient.set carried

        ran
#endif

    member private this.PumpFromDispatcher() =
#if FABLE_COMPILER
        this.Pump () |> ignore
#else
        if not serialised then
            this.Pump () |> ignore
        else
            let me = Platform.currentThreadId ()

            if Volatile.Read &holder = me then
                this.Drain () |> ignore
            else
                this.CheckContext "Pump"

                // Held by another thread: its Release posts a new drain.
                if Interlocked.CompareExchange (&holder, me, 0) = 0 then
                    try
                        this.Drain () |> ignore
                    finally
                        this.Release ()
#endif

    /// <summary>
    /// Raises if the caller is not on the thread that owns this graph. Under <c>Serialised</c>, raises if another
    /// thread is inside the graph, or if the caller is outside it and runs off the construction context.
    /// </summary>
    /// <remarks>
    /// Thread-affinity checks are omitted under Fable.
    /// </remarks>
#if FABLE_COMPILER
    member inline this.AssertOnGraphThread(operation: string) =
        ignore operation
#else
    member this.AssertOnGraphThread(operation: string) =
        if guarded then
            if OwnerThreadToken.Current <> ownerToken then
                this.FailOffThread operation
        elif serialised then
            this.CheckHeld operation
#endif

#if !FABLE_COMPILER
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.FailOffThread(operation: string) : unit =
        raise (
            InvalidOperationException (
                operation
                + " ran on thread "
                + string (Platform.currentThreadId ())
                + ", but this graph is owned by thread "
                + string ownerThread
                + ". Marshal through Graph.Dispatch, or set GraphOptions.ThreadAffinity to Unchecked if affinity is guaranteed some other way."
            )
        )

    /// <summary>
    /// Raises unless the calling thread is inside the graph, or the graph is free and the caller runs on its
    /// context. Enters nothing.
    /// </summary>
    member private this.CheckHeld(operation: string) =
        let held = Volatile.Read &holder

        if held <> Platform.currentThreadId () then
            if held <> 0 then
                this.FailConcurrent (operation, held)

            this.CheckContext operation
#endif

#if !FABLE_COMPILER
    member private _.CheckContext(operation: string) =
        if not (obj.ReferenceEquals (SynchronizationContext.Current, context)) then
            raise (
                InvalidOperationException (
                    operation
                    + " ran on thread "
                    + string (Platform.currentThreadId ())
                    + " outside the synchronisation context this Serialised graph was constructed on. Marshal through Graph.Dispatch."
                )
            )

    member private _.FailConcurrent(operation: string, other: int) : unit =
        raise (
            InvalidOperationException (
                operation
                + " ran on thread "
                + string (Platform.currentThreadId ())
                + " while thread "
                + string other
                + " was inside this Serialised graph. Two threads entered it at once."
            )
        )
#endif


#if !FABLE_COMPILER
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.GrowQueue() =
        let size = if queueSlots.Length = 0 then 8 else queueSlots.Length * 2
        let grown = Array.zeroCreate size

        if queueSlots.Length > 0 then
            Array.blit queueSlots 0 grown 0 queueSlots.Length

        queueSlots <- grown
#endif

    /// <summary>Queues an effect or split effect node directly without <c>IScheduled</c> interface dispatch.</summary>
#if FABLE_COMPILER
    member internal this.ScheduleNode(n: Node) =
        this.Schedule (NodeOps.Scheduled n)
#else
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.ScheduleNode(n: Node) =
        let selfObj: obj = box n.Self
        Tracer.Schedule (this, selfObj, queueCount - queueHead + passCount - passHead)

        if queueCount = queueSlots.Length then
            this.GrowQueue ()

        queueSlots[queueCount] <- QueueSlot (Unchecked.defaultof<IScheduled>, n)
        queueCount <- queueCount + 1
#endif

    member internal this.Schedule(item: IScheduled) =
        Tracer.Schedule (this, item, queueCount - queueHead + passCount - passHead)

#if FABLE_COMPILER
        if queueCount < queue.Count then
            Platform.setEntryAt queueCount queue item
        else
            queue.Add item
#else
        if queueCount = queueSlots.Length then
            this.GrowQueue ()

        queueSlots[queueCount] <- QueueSlot (item, Unchecked.defaultof<Node>)
#endif
        queueCount <- queueCount + 1

    /// <summary>Queues a projection pass or a lookup refresh. A flush runs it before the next item <c>Schedule</c> queued.</summary>
    member internal this.SchedulePass(item: IScheduled) =
        Tracer.Schedule (this, item, passCount - passHead)

        if passHead = passCount && not runningPasses then
            this.QueueDrain ()

        if passCount < passes.Count then
#if FABLE_COMPILER
            Platform.setEntryAt passCount passes item
#else
            passes[passCount] <- item
#endif
        else
            passes.Add item

        passCount <- passCount + 1

    /// <summary>
    /// Queues <c>passDrain</c> for a pass queued while none is pending: at the head of <c>queue</c> when a spent slot
    /// precedes it, as inside a flush, and at the tail otherwise.
    /// </summary>
    member private this.QueueDrain() =
        if Platform.isNull (box passDrain) then
            passDrain <- PassDrain (fun () -> this.RunPasses ())

        if queueHead > 0 then
            queueHead <- queueHead - 1
#if FABLE_COMPILER
            Platform.setEntryAt queueHead queue passDrain
#else
            queueSlots[queueHead] <- QueueSlot (passDrain, Unchecked.defaultof<Node>)
#endif
        else
#if FABLE_COMPILER
            if queueCount < queue.Count then
                Platform.setEntryAt queueCount queue passDrain
            else
                queue.Add passDrain
#else
            if queueCount = queueSlots.Length then
                this.GrowQueue ()

            queueSlots[queueCount] <- QueueSlot (passDrain, Unchecked.defaultof<Node>)
#endif
            queueCount <- queueCount + 1

    /// <summary>Runs the queued passes, including those queued by a pass, and empties the pass buffer.</summary>
    /// <remarks>A pass that throws leaves the passes after it queued, with <c>passDrain</c> queued to run them.</remarks>
    member private this.RunPasses() =
        runningPasses <- true

        try
            while passHead < passCount do
                let item = Platform.entryAt passHead passes
                Tracer.Dequeue (this, item)
                Platform.releaseSlot passes passHead
                passHead <- passHead + 1
                item.Execute ()
        finally
            runningPasses <- false

            if passHead < passCount then
                this.QueueDrain ()
            else
                passHead <- 0
                passCount <- 0

    /// <summary>
    /// Runs every queued effect. Re-entrant calls are absorbed: an effect that
    /// writes a signal adds to the queue this loop is already draining, rather
    /// than starting a nested flush.
    /// </summary>
    /// <remarks>
    /// The graph is ambient while the loop runs. Every queued projection pass and lookup refresh runs before the next
    /// queued effect, so an effect reads the keys, rows and lookup values of the write that woke it.
    /// </remarks>
    member this.Flush() =
        this.Entered ("A flush", (fun () -> this.RunFlush ()))

#if !FABLE_COMPILER && !RANVIER_TRACE
    member private this.RunFlush() =
        if not flushing then
#if RANVIER_COUNTERS
            Counters.Flushed ()
#endif
            flushing <- true
            flushOwed <- false
            let previousAmbient = this.EnterAmbient ()

            if passCount = 0 && Platform.isNull journal then
                this.DrainNodesFast previousAmbient
            else
                this.DrainSlow previousAmbient

    /// <summary>
    /// Drains effect and split-effect nodes without an exception frame (<c>drain_no_escape</c>): neither <c>NodeOps.Execute</c>
    /// nor any <c>UpdateIfNecessary</c> lets an exception escape. Falls back to <c>DrainSlow</c> on the first non-node item.
    /// </summary>
    member private this.DrainNodesFast(previousAmbient: obj) =
        let mutable fast = true

        while fast && queueHead < queueCount do
            let n = queueSlots[queueHead].Node

            if not (obj.ReferenceEquals (n, null)) then
                queueSlots[queueHead].Node <- Unchecked.defaultof<Node>
                queueHead <- queueHead + 1
                NodeOps.Execute n
            else
                fast <- false

        if fast then
            queueHead <- 0
            queueCount <- 0
            flushing <- false

            if staleTransitions <> 0 && pullDepth = 0 then
                staleTransitions <- 0
                activeSkips <- 0

            if not (Platform.isNull lastRaised) then
                lastRaised <- null

            this.LeaveAmbient previousAmbient
        else
            this.DrainSlow previousAmbient

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.DrainSlow(previousAmbient: obj) =
        try
            if passCount <> 0 then
                this.RunPasses ()

            while queueHead < queueCount do
                let slot = queueSlots[queueHead]
                queueSlots[queueHead] <- QueueSlot (Unchecked.defaultof<IScheduled>, Unchecked.defaultof<Node>)
                queueHead <- queueHead + 1

                if not (obj.ReferenceEquals (slot.Node, null)) then
                    NodeOps.Execute slot.Node
                else
                    slot.Item.Execute ()

            queueHead <- 0
            queueCount <- 0
        finally
            flushing <- false

            if staleTransitions <> 0 && pullDepth = 0 then
                staleTransitions <- 0
                activeSkips <- 0

            if not (Platform.isNull lastRaised) then
                lastRaised <- null

            this.LeaveAmbient previousAmbient

            if not (Platform.isNull journal) then
                journal.Boundary ()

    /// <summary>A published memo takes this exception immediately after its body returns.</summary>
    member internal _.Caught
        with get () = caught
        and set (ex: exn) = caught <- ex

    /// <summary>True when a signal write on an entered graph can fuse an immediate single-observer flush (Theorem B5).</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.CanFuseIdleFlush() : bool =
        fastWriteToken >= 0L
        && ((batchDepth ||| pullDepth ||| passCount) = 0)
        && not flushing
        && not untrackedInBody
        && queueHead = queueCount
        && obj.ReferenceEquals (currentNode, null)
        && Platform.isNull (box current)

    /// <summary>True when neither a stale read nor a flush is active, so <c>NoteStaleTransition</c> is a no-op.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.IsIdleForMarks() : bool =
        pullDepth = 0 && not flushing

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.BeginFusedFlush() : obj =
#if RANVIER_COUNTERS
        Counters.Flushed ()
#endif
        if queueSlots.Length = 0 then
            this.GrowQueue ()

        queueHead <- 1
        queueCount <- 1
        flushing <- true
        flushOwed <- false
        this.EnterAmbient ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.EndFusedFlush(previousAmbient: obj) =
        if queueHead < queueCount || passCount <> 0 then
            this.DrainNodesFast previousAmbient
        else
            queueHead <- 0
            queueCount <- 0
            flushing <- false

            if staleTransitions <> 0 then
                staleTransitions <- 0
                activeSkips <- 0

            if not (Platform.isNull lastRaised) then
                lastRaised <- null

            this.LeaveAmbient previousAmbient
#else
    member private this.RunFlush() =
        if not flushing then
#if RANVIER_COUNTERS
            Counters.Flushed ()
#endif
            Tracer.FlushStart this
            flushing <- true
            flushOwed <- false
            let previousAmbient = this.EnterAmbient ()

            try
                if passCount <> 0 then
                    this.RunPasses ()

                while queueHead < queueCount do
#if FABLE_COMPILER
                    let item = Platform.entryAt queueHead queue
                    Tracer.Dequeue (this, item)
                    Platform.releaseSlot queue queueHead
                    queueHead <- queueHead + 1
                    item.Execute ()
#else
                    let slot = queueSlots[queueHead]

                    let tracedItem: obj =
                        if not (obj.ReferenceEquals (slot.Node, null)) then
                            box slot.Node.Self
                        else
                            box slot.Item

                    Tracer.Dequeue (this, tracedItem)
                    queueSlots[queueHead] <- QueueSlot (Unchecked.defaultof<IScheduled>, Unchecked.defaultof<Node>)
                    queueHead <- queueHead + 1

                    if not (obj.ReferenceEquals (slot.Node, null)) then
                        NodeOps.Execute slot.Node
                    else
                        slot.Item.Execute ()
#endif

                queueHead <- 0
                queueCount <- 0
            finally
                flushing <- false

                if staleTransitions <> 0 && pullDepth = 0 then
                    staleTransitions <- 0
                    activeSkips <- 0

                if not (Platform.isNull lastRaised) then
                    lastRaised <- null

                Tracer.FlushEnd this
                Tracer.Flushed this
                this.LeaveAmbient previousAmbient

                if not (Platform.isNull journal) then
                    journal.Boundary ()
#endif

    /// <summary>
    /// Runs the queued effects. Inside a batch they run when the batch returns;
    /// a batch that throws leaves them queued until the next flush. Inside a
    /// computation's body, including <c>untrack</c> within one, or a stale
    /// read, they run once the outermost body or read has finished.
    /// </summary>
    member internal this.RequestFlush() =
        if queueHead < queueCount && batchDepth = 0 then
            if
                pullDepth = 0
                && obj.ReferenceEquals (currentNode, null)
                && Platform.isNull (box current)
                && not untrackedInBody
            then
                this.Flush ()
            elif not flushing then
                flushOwed <- true

    /// <summary>
    /// <c>RequestFlush</c> from a caller that has already passed <c>Enter</c> (a signal write, batch exit, or inbox drain).
    /// </summary>
    member internal this.RequestFlushFromEntered() =
        if queueHead < queueCount && batchDepth = 0 then
            if
                pullDepth = 0
                && obj.ReferenceEquals (currentNode, null)
                && Platform.isNull (box current)
                && not untrackedInBody
            then
                this.RunFlush ()
            elif not flushing then
                flushOwed <- true

    /// <summary>
    /// Runs the owed flush when the caller is outside every body and read.
    /// </summary>
    member private this.SettleFlush() =
        if
            flushOwed
            && batchDepth = 0
            && pullDepth = 0
            && obj.ReferenceEquals (currentNode, null)
            && Platform.isNull (box current)
            && not untrackedInBody
        then
            this.Flush ()

    /// <summary>
    /// Owes a flush for effects queued while a read or discharge is in progress.
    /// </summary>
    member internal _.OweFlush() =
        if
            queueHead < queueCount
            && batchDepth = 0
            && not flushing
        then
            flushOwed <- true

    /// <summary>True while the queue holds work: the inline test in front of a flush request.</summary>
    member inline internal _.QueuePending = queueHead < queueCount

    /// <summary>Clears a swallowed failed-read record after an untracked refresh inside a flush.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.ClearLastRaisedIfTop() =
        if
            not (Platform.isNull lastRaised)
            && pullDepth = 0
            && obj.ReferenceEquals (currentNode, null)
            && Platform.isNull (box current)
        then
            lastRaised <- null

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.ClearLastRaised() =
        lastRaised <- null

    member inline internal _.HasLastRaised = not (Platform.isNull lastRaised)

    /// <summary>
    /// True inside a flush, a stale read, or while a computation's body runs outside <c>untrack</c> (Theorem B1:
    /// <c>flushing_implies_no_flush_owed</c> &amp; <c>deferring_with_flushing_eq</c>). Under <c>Serialised</c>, false on
    /// every thread other than the holder, so a stale read from such a thread enters the graph through <c>EnterPull</c>.
    /// </summary>
    member internal _.Deferring
        with [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>] get () =
            (flushing
             || pullDepth > 0
             || not (obj.ReferenceEquals (currentNode, null))
             || not (Platform.isNull (box current)))
#if !FABLE_COMPILER
            && (not serialised
                || Volatile.Read &holder = Platform.currentThreadId ())
#endif

    /// <summary>
    /// Marks the start of a stale read. The outermost one makes this graph
    /// ambient until it ends.
    /// </summary>
    member internal this.EnterPull() =
#if !FABLE_COMPILER
        if this.Enter "A stale read" then
            pullHeld <- true
#endif

        if pullDepth = 0 then
            let previous = this.EnterAmbient ()

            if not (obj.ReferenceEquals (pullAmbient, previous)) then
                pullAmbient <- previous

        pullDepth <- pullDepth + 1

#if !FABLE_COMPILER
    /// <summary>
    /// Opens the outermost stale read when <c>EnterPull</c> would change nothing but the depth: no read is open, the
    /// calling thread owns the guarded graph and already hosts it. False leaves the read to <c>EnterPull</c>.
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.TryEnterPullFast() : bool =
        if
            pullDepth = 0
            && not (Platform.isNull ownerAmbient)
            && obj.ReferenceEquals (AmbientSlot.Existing, ownerAmbient)
            && obj.ReferenceEquals (ownerAmbient.Hosting, this)
        then
            pullDepth <- 1
            true
        else
            false

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member internal this.ExitPullFastOnException() =
        pullAmbient <- this
        this.ExitPull ()

    /// <summary>
    /// <c>ExitPullDeferSettle</c> for a read opened by <c>TryEnterPullFast</c>: one test of the fields the exit
    /// would touch. True when a flush is owed, for the caller to settle once it has read the node.
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.ExitPullFast() : bool =
        pullDepth <- 0

        if
            Platform.isNull lastRaised
            && not flushOwed
            && staleTransitions = 0
        then
            false
        else
            this.ExitPullFastSlow ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.ExitPullFastSlow() : bool =
        if not (Platform.isNull lastRaised) then
            lastRaised <- null

        if flushOwed then
            true
        else
            if staleTransitions <> 0 && not flushing then
                staleTransitions <- 0
                activeSkips <- 0

            false
#endif
    /// <summary>
    /// Marks the end of a stale read. The outermost one runs the owed flush.
    /// </summary>
    member internal this.ExitPull() =
        pullDepth <- pullDepth - 1

        if pullDepth = 0 then
            this.LeaveAmbient pullAmbient

            if not (Platform.isNull lastRaised) then
                lastRaised <- null

            if staleTransitions <> 0 && not flushing then
                staleTransitions <- 0
                activeSkips <- 0

#if !FABLE_COMPILER
            if pullHeld then
                pullHeld <- false

                try
                    if flushOwed then
                        this.SettleFlush ()
                finally
                    this.Release ()
            elif flushOwed then
                this.SettleFlush ()
#else
            if flushOwed then
                this.SettleFlush ()
#endif

    /// <summary>
    /// Marks the end of a stale read that has not thrown. When the outermost read owes a flush, defers
    /// <c>SettleFlush</c> (and releasing <c>pullHeld</c>) to <c>FinishPullSettle</c> so the caller can read the node's
    /// fields first.
    /// </summary>
    member internal this.ExitPullDeferSettle() : bool =
        pullDepth <- pullDepth - 1

        if pullDepth = 0 then
            this.LeaveAmbient pullAmbient

            if not (Platform.isNull lastRaised) then
                lastRaised <- null

            if flushOwed then
                true
            else
                if staleTransitions <> 0 && not flushing then
                    staleTransitions <- 0
                    activeSkips <- 0

#if !FABLE_COMPILER
                if pullHeld then
                    pullHeld <- false
                    this.Release ()
#endif
                false
        else
            false

    member internal this.FinishPullSettle() =
        if pullDepth = 0 then
            if staleTransitions <> 0 && not flushing then
                staleTransitions <- 0
                activeSkips <- 0

#if !FABLE_COMPILER
            if pullHeld then
                pullHeld <- false

                try
                    this.SettleFlush ()
                finally
                    this.Release ()
            else
                this.SettleFlush ()
#else
            this.SettleFlush ()
#endif

    /// <summary>
    /// Evaluates <c>body</c> without recording anything it reads.
    /// </summary>
    /// <remarks>
    /// The public face of the tracking context: a read inside <c>body</c> still
    /// returns the current value, it just does not create an edge, so the
    /// enclosing computation is not woken when that source changes.
    /// </remarks>
    member this.Untrack(body: Func<'T>) =
        this.RunUntracked body.Invoke

    /// <summary>
    /// The current status of <c>node</c>. A readable node (a signal, memo, async value, boundary or projection) is
    /// brought up to date first, and inside a computation the read is tracked, as <c>TryValue</c> is.
    /// </summary>
    /// <remarks>
    /// A pending or failed node returns its status without raising, so a computation can test nodes of any value type.
    /// An effect or other unreadable node returns its status untracked.
    /// </remarks>
    member this.TrackStatus(node: INode) : Status =
        let source =
#if FABLE_COMPILER
            // Fable compiles an interface type test to false: probe for the member instead.
            if Platform.hasMember node "UpdateIfNecessary" then
                node :?> ISource
            else
                Unchecked.defaultof<ISource>
#else
            match node with
            | :? ISource as source -> source
            | _ -> Unchecked.defaultof<ISource>
#endif

        if not (Platform.isNull (box source)) then
            source.UpdateIfNecessary ()
            this.Track source

            if source.IsStale then
                this.MarkReaderCheck ()

        node.Status

    /// <summary>
    /// Defers the flush until <c>body</c> returns, so a group of writes produces one
    /// effect run rather than one per write.
    /// </summary>
    member this.Batch(body: Func<'T>) =
        this.RunBatch body.Invoke

    member internal this.RunBatch(body: unit -> 'T) =
#if FABLE_COMPILER
        this.Entered (
            "A batch",
            fun () ->
                batchDepth <- batchDepth + 1
                Tracer.BatchEnter (this, batchDepth)

                let result =
                    try
                        body ()
                    finally
                        batchDepth <- batchDepth - 1
                        Tracer.BatchExit (this, batchDepth)

                this.RequestFlushFromEntered ()
                result
        )
#else
        if not (this.Enter "A batch") then
            this.RunBatchCore body
        else
            this.RunBatchAfterAcquire body

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.RunBatchCore(body: unit -> 'T) =
        batchDepth <- batchDepth + 1
        Tracer.BatchEnter (this, batchDepth)

        let result =
            try
                body ()
            finally
                batchDepth <- batchDepth - 1
                Tracer.BatchExit (this, batchDepth)

        this.RequestFlushFromEntered ()
        result

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RunBatchAfterAcquire(body: unit -> 'T) =
        try
            this.RunBatchCore body
        finally
            this.Release ()
#endif

    /// <summary>
    /// <c>RunBatch</c> over <c>f arg</c>, tracked, with no closure: one batch around <c>f arg</c>, a flush request on
    /// normal exit only, and the entry checks of a batch.
    /// </summary>
    /// <remarks>
    /// On .NET an unjournaled graph that is not <c>Serialised</c> passes its entry check with one token load. A
    /// failed batch keeps its writes and queued work for the next flush, as <c>RunBatch</c> does.
    /// </remarks>
#if FABLE_COMPILER
    member internal this.RunBatchWith(f: 'A -> 'T, arg: 'A) : 'T = this.RunBatch (fun () -> f arg)

    /// <summary><c>RunBatchWith</c> for a body that returns nothing.</summary>
    member internal this.RunBatchUnitWith(f: 'A -> unit, arg: 'A) : unit = this.RunBatch (fun () -> f arg)
#else
    member internal this.RunBatchWith<'A, 'T>(f: 'A -> 'T, arg: 'A) : 'T =
        let tok = fastWriteToken

        if tok = OwnerThreadToken.Current || tok = 0L then
            this.RunBatchCoreWith (f, arg)
        elif not (this.Enter "A batch") then
            this.RunBatchCoreWith (f, arg)
        else
            this.RunBatchAfterAcquireWith (f, arg)

    /// <summary><c>RunBatchWith</c> for a body that returns nothing.</summary>
    member internal this.RunBatchUnitWith<'A>(f: 'A -> unit, arg: 'A) : unit = this.RunBatchWith<'A, unit> (f, arg)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.RunBatchCoreWith<'A, 'T>(f: 'A -> 'T, arg: 'A) : 'T =
        batchDepth <- batchDepth + 1
        Tracer.BatchEnter (this, batchDepth)

        let result =
            try
                f arg
            finally
                batchDepth <- batchDepth - 1
                Tracer.BatchExit (this, batchDepth)

        if queueHead < queueCount then
            this.RequestFlushFromEntered ()

        result

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RunBatchAfterAcquireWith<'A, 'T>(f: 'A -> 'T, arg: 'A) : 'T =
        try
            this.RunBatchCoreWith (f, arg)
        finally
            this.Release ()

    /// <summary>The open batch depth. A write that sees it above zero leaves the flush to the batch's exit.</summary>
    member inline internal _.BatchDepth = batchDepth
#endif

    /// <summary>
    /// Discharges <c>owner</c> with effects deferred. A computation discharges its
    /// scope before re-running its body. The effects woken by its cleanups
    /// run once, after the outermost stale read has finished.
    /// </summary>
    /// <remarks>
    /// The cleanups run untracked, and a node they create belongs to <c>owner</c>.
    /// </remarks>
    member internal this.Discharge(owner: Owner) =
        Tracer.DischargeStart owner
        this.DetachedDisposeScope owner
        Tracer.DischargeEnd owner
        this.OweFlush ()

    /// <summary>
    /// Disposes a computation's scope as <c>Discharge</c> discharges it. A node
    /// its cleanups create is disposed immediately.
    /// </summary>
    member internal this.Retire(owner: Owner) =
        this.DetachedDispose owner

        if queueHead < queueCount then
            this.RequestFlush ()

    interface ILateRunner with
        member this.RunDetached(owner, f) =
            // Holds a Serialised graph for a root scope's teardown; Guarded checks each disposed node instead.
#if !FABLE_COMPILER
            let held = serialised && this.Acquire "Disposing a root"
#endif

            try
                this.Detached (
                    owner,
                    fun () ->
                        try
                            f ()
                        with ex ->
                            owner.RecordError ex
                )

                if queueHead < queueCount then
                    this.RequestFlush ()
            finally
#if !FABLE_COMPILER
                if held then
                    this.Release ()
#else
                ()
#endif

        member this.HoldTeardown() =
#if FABLE_COMPILER
            false
#else
            serialised && this.Acquire "Disposing a root"
#endif

        member this.ReleaseTeardown() =
            this.Release ()

    /// <summary>
    /// Runs <c>teardown</c> untracked, with effects deferred and <c>owner</c> as the
    /// owner of every node it creates. The graph is ambient while it runs.
    /// </summary>
    member private this.Detached(owner: Owner, teardown: unit -> unit) =
        let previous = current
        let previousNode = currentNode
        let previousOwner = currentOwner
        let previousPending = ownerPending
        let previousAmbient = this.EnterAmbient ()
        current <- Unchecked.defaultof<IComputation>
        currentNode <- Unchecked.defaultof<Node>
        currentOwner <- owner
        ownerPending <- false
        batchDepth <- batchDepth + 1
        detachedDepth <- detachedDepth + 1

        try
            teardown ()
        finally
            detachedDepth <- detachedDepth - 1
            batchDepth <- batchDepth - 1
            current <- previous
            currentNode <- previousNode
            currentOwner <- previousOwner
            ownerPending <- previousPending
            this.LeaveAmbient previousAmbient

    member private this.DetachedDisposeScope(owner: Owner) =
        let previous = current
        let previousNode = currentNode
        let previousOwner = currentOwner
        let previousPending = ownerPending
        let previousAmbient = this.EnterAmbient ()
        current <- Unchecked.defaultof<IComputation>
        currentNode <- Unchecked.defaultof<Node>
        currentOwner <- owner
        ownerPending <- false
        batchDepth <- batchDepth + 1
        detachedDepth <- detachedDepth + 1

        try
            owner.DisposeScope ()
        finally
            detachedDepth <- detachedDepth - 1
            batchDepth <- batchDepth - 1
            current <- previous
            currentNode <- previousNode
            currentOwner <- previousOwner
            ownerPending <- previousPending
            this.LeaveAmbient previousAmbient

    member private this.DetachedDispose(owner: Owner) =
        let previous = current
        let previousNode = currentNode
        let previousOwner = currentOwner
        let previousPending = ownerPending
        let previousAmbient = this.EnterAmbient ()
        current <- Unchecked.defaultof<IComputation>
        currentNode <- Unchecked.defaultof<Node>
        currentOwner <- owner
        ownerPending <- false
        batchDepth <- batchDepth + 1
        detachedDepth <- detachedDepth + 1

        try
            owner.Dispose ()
        finally
            detachedDepth <- detachedDepth - 1
            batchDepth <- batchDepth - 1
            current <- previous
            currentNode <- previousNode
            currentOwner <- previousOwner
            ownerPending <- previousPending
            this.LeaveAmbient previousAmbient

    member private this.DetachedCleanup(owner: Owner, f: unit -> unit) =
        let previous = current
        let previousNode = currentNode
        let previousOwner = currentOwner
        let previousPending = ownerPending
        let previousAmbient = this.EnterAmbient ()
        current <- Unchecked.defaultof<IComputation>
        currentNode <- Unchecked.defaultof<Node>
        currentOwner <- owner
        ownerPending <- false
        batchDepth <- batchDepth + 1
        detachedDepth <- detachedDepth + 1

        try
            owner.AddCleanup f
        finally
            detachedDepth <- detachedDepth - 1
            batchDepth <- batchDepth - 1
            current <- previous
            currentNode <- previousNode
            currentOwner <- previousOwner
            ownerPending <- previousPending
            this.LeaveAmbient previousAmbient

    member internal this.NextId() =
        this.AssertOnGraphThread "Creating a node"
        nextId <- nextId + 1
        nextId

    /// <summary>The last id <c>NextId</c> issued.</summary>
    member internal _.NodeCount = nextId

    member inline internal _.StaleTransitions = staleTransitions

    member inline internal _.ActiveSkips = activeSkips

    member inline internal _.NoteStaleTransition() =
        if pullDepth > 0 || flushing then
            staleTransitions <- staleTransitions + 1

    member internal _.NoteActiveSkip() =
        activeSkips <- activeSkips + 1
        staleTransitions <- staleTransitions + 1

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.NotifyMoved(observers: ObserverSet) =
        let c =
            if not (obj.ReferenceEquals (currentNode, null)) then
                currentNode.Self
            else
                current

        if staleTransitions = 0 then
            observers.NotifyDirtyOther c
        else
            observers.NotifyDirtyExcept c

    /// <summary>
    /// Links <c>source</c> into the computation currently being evaluated, if any.
    /// </summary>
    /// <remarks>
    /// MANDATORY: callers must invoke this before throwing or returning
    /// Pending. A read that suspends without first linking the edge leaves the
    /// consumer stranded — it never learns that the source settled. This is
    /// upstream bug #2893.
    /// </remarks>
#if FABLE_COMPILER
    member internal _.Track(source: ISource) =
        if not (obj.ReferenceEquals (current, null)) then
            // The observer half is linked by the consumer's own source list,
            // which is the only side that knows whether this edge is new.
            current.AddSource source

    /// <summary>
    /// The computation whose body is running, or null. Read by a source that
    /// has just recomputed, to avoid marking its own reader dirty — see
    /// <c>ObserverSet.NotifyDirtyExcept</c>.
    /// </summary>
    member internal _.CurrentComputation = current

    member internal _.IsTracking() =
        not (obj.ReferenceEquals (current, null))
#else
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.Track(source: ISource) =
        let n = currentNode

        if not (obj.ReferenceEquals (n, null)) then
#if !RANVIER_TRACE
            // Theorem B2 (`track_slot0_fast_eq` & `track_slot1_fast_eq`): `source` is never null, and a disposed
            // node clears `first` and `second` to null, so a reference match proves `n` live and advances cursor.
            let cur = n.Sources.cursor

            if cur = 0 && obj.ReferenceEquals (n.Sources.first, source) then
                n.Sources.cursor <- 1
            else
                this.TrackNodeRest (n, cur, source)
#else
            NodeOps.AddSource (n, source)
#endif
        elif not (obj.ReferenceEquals (current, null)) then
            this.TrackOtherSlow source

#if !RANVIER_TRACE
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.TrackNodeRest(n: Node, cur: int, source: ISource) =
        if cur = 1 && obj.ReferenceEquals (n.Sources.second, source) then
            n.Sources.cursor <- 2
        else
            this.TrackNodeSlow (n, source)
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.TrackNodeSlow(n: Node, source: ISource) =
        NodeOps.AddSource (n, source)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.TrackOtherSlow(source: ISource) =
        current.AddSource source

    /// <summary>
    /// The computation whose body is running, or null. Read by a source that
    /// has just recomputed, to avoid marking its own reader dirty — see
    /// <c>ObserverSet.NotifyDirtyExcept</c>.
    /// </summary>
    member internal _.CurrentComputation
        with [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>] get () =
            if not (obj.ReferenceEquals (currentNode, null)) then
                currentNode.Self
            else
                current

    /// <summary>The <c>Node</c> of the running computation when it entered through <c>EnterHostedNode</c>, or null.</summary>
    member inline internal _.CurrentNode = currentNode

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.IsTracking() =
        not (obj.ReferenceEquals (currentNode, null))
        || not (Platform.isNull (box current))
#endif

    /// <summary>
    /// Marks the running computation dirty, if any. Called by a tracked read that has just linked a source still
    /// stale after its pull.
    /// </summary>
    member internal this.MarkReaderDirty() =
        if not (obj.ReferenceEquals (currentNode, null)) then
            this.NoteStaleTransition ()
            NodeOps.MarkDirty currentNode
        elif not (obj.ReferenceEquals (current, null)) then
            this.NoteStaleTransition ()
            current.MarkDirty ()

    /// <summary>
    /// Marks the running computation <c>Check</c>, if any: <c>MarkReaderDirty</c> for a source that tells its readers
    /// <c>Check</c> when it goes dirty, so a re-run to an equal value stops at the cutoff.
    /// </summary>
    member internal this.MarkReaderCheck() =
        if not (obj.ReferenceEquals (currentNode, null)) then
            this.NoteStaleTransition ()
            NodeOps.MarkCheck currentNode
        elif not (obj.ReferenceEquals (current, null)) then
            this.NoteStaleTransition ()
            current.MarkCheck ()

#if !FABLE_COMPILER
    member internal _.EnterHosted(host: IComputation) : HostedFrame =
        let saved = HostedFrame (current, currentNode, currentOwner, ownerPending, raisedPending)
        current <- host
        currentNode <- Unchecked.defaultof<Node>
        ownerPending <- true

        if not (Platform.isNull (box raisedPending)) then
            raisedPending <- Unchecked.defaultof<INode>

        saved

    /// <summary><c>EnterHosted</c> for a memo, effect or split effect, so <c>Track</c> reaches its node directly.</summary>
    /// <summary>True when no hosted computation, pending owner or raised pending node is active.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.CanEnterHostedTop() : bool =
        obj.ReferenceEquals (currentNode, null)
        && Platform.isNull (box current)
        && not ownerPending
        && Platform.isNull (box raisedPending)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.EnterHostedTop(host: Node) : Owner =
        let prevOwner = currentOwner
        currentNode <- host
        ownerPending <- true
        prevOwner

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.LeaveHostedTop(prevOwner: Owner) =
        currentNode <- Unchecked.defaultof<Node>

        if not ownerPending then
            currentOwner <- prevOwner

        ownerPending <- false

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member internal _.LeaveHostedTopFailed(prevOwner: Owner) =
        currentNode <- Unchecked.defaultof<Node>

        if not ownerPending then
            currentOwner <- prevOwner

        ownerPending <- false

        if not (Platform.isNull (box raisedPending)) then
            raisedPending <- Unchecked.defaultof<INode>

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.EnterHostedNode(host: Node) : HostedFrame =
        let saved = HostedFrame (current, currentNode, currentOwner, ownerPending, raisedPending)

        if not (Platform.isNull (box current)) then
            current <- Unchecked.defaultof<IComputation>

        currentNode <- host
        ownerPending <- true

        if not (Platform.isNull (box raisedPending)) then
            raisedPending <- Unchecked.defaultof<INode>

        saved

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.LeaveHosted(saved: HostedFrame) =
        if not (obj.ReferenceEquals (saved.PreviousNode, null)) then
            currentNode <- saved.PreviousNode

            if not (Platform.isNull (box current)) then
                current <- Unchecked.defaultof<IComputation>
        elif Platform.isNull (box saved.Previous) then
            currentNode <- Unchecked.defaultof<Node>

            if not (Platform.isNull (box current)) then
                current <- Unchecked.defaultof<IComputation>
        else
            currentNode <- Unchecked.defaultof<Node>
            current <- saved.Previous

        // A body that never resolved its owner left currentOwner as it was.
        if not ownerPending then
            currentOwner <- saved.PreviousOwner

        ownerPending <- saved.PreviousPending

        if not (obj.ReferenceEquals (raisedPending, saved.PreviousRaised)) then
            raisedPending <- saved.PreviousRaised
#endif

    /// <summary>
    /// Evaluates <c>body</c> with <c>host</c> as the tracking context and its scope as
    /// the owner of every node <c>body</c> creates. <c>host</c> implements <c>IScopeHost</c>.
    /// </summary>
    /// <remarks>
    /// A body that returns after a pending read raised inside it raises
    /// <c>NotReadyException</c> for that read, whatever it returned.
    /// </remarks>
    member internal _.RunHosted(host: IComputation, body: unit -> 'T) =
        let previous = current
        let previousNode = currentNode
        let previousOwner = currentOwner
        let previousPending = ownerPending
        let previousRaised = raisedPending
        current <- host
        currentNode <- Unchecked.defaultof<Node>
        ownerPending <- true
        raisedPending <- Unchecked.defaultof<INode>

        try
            let result = body ()

            if not (Platform.isNull (box raisedPending)) then
                raise (NotReadyException raisedPending)

            result
        finally
            current <- previous
            currentNode <- previousNode

            // Each reference store is a GC write barrier: a body that never resolved its owner left it as it was.
            if not ownerPending then
                currentOwner <- previousOwner

            ownerPending <- previousPending

            if not (obj.ReferenceEquals (raisedPending, previousRaised)) then
                raisedPending <- previousRaised

    /// <summary>
    /// Evaluates <c>body arg</c> as <c>RunHosted(host, body)</c> evaluates <c>body ()</c>.
    /// </summary>
    member internal _.RunHosted(host: IComputation, body: 'A -> 'T, arg: 'A) =
        let previous = current
        let previousNode = currentNode
        let previousOwner = currentOwner
        let previousPending = ownerPending
        let previousRaised = raisedPending
        current <- host
        currentNode <- Unchecked.defaultof<Node>
        ownerPending <- true
        raisedPending <- Unchecked.defaultof<INode>

        try
            let result = body arg

            if not (Platform.isNull (box raisedPending)) then
                raise (NotReadyException raisedPending)

            result
        finally
            current <- previous
            currentNode <- previousNode

            // Each reference store is a GC write barrier: a body that never resolved its owner left it as it was.
            if not ownerPending then
                currentOwner <- previousOwner

            ownerPending <- previousPending

            if not (obj.ReferenceEquals (raisedPending, previousRaised)) then
                raisedPending <- previousRaised

#if !FABLE_COMPILER
    /// <summary>
    /// Detaches a flight continuation starting on the graph thread from the
    /// computation it interrupts: reads in it are untracked, and nodes it
    /// creates belong to the root.
    /// </summary>
    /// <returns>
    /// True when it detaches. False off the graph thread, or when <c>frame</c> is already
    /// entered and a nested continuation is returning to it.
    /// </returns>
    member internal this.EnterContinuation(frame: obj) : bool =
        if not this.IsOnGraphThread then
            false
        else
            if Platform.isNull continuations then
                continuations <- ResizeArray ()

            let mutable entered = false

            for i in 0 .. continuations.Count - 1 do
                let struct (saved, _, _, _) = continuations[i]

                if Object.ReferenceEquals (saved, frame) then
                    entered <- true

            if entered then
                false
            else
                // A pending owner is saved as the null it stands for.
                let owner = if ownerPending then Unchecked.defaultof<Owner> else currentOwner
                continuations.Add (struct (frame, this.CurrentComputation, owner, raisedPending))
                current <- Unchecked.defaultof<IComputation>
                currentNode <- Unchecked.defaultof<Node>
                currentOwner <- root
                ownerPending <- false
                raisedPending <- Unchecked.defaultof<INode>

                if not (Platform.isNull journal) then
                    journal.EnterDerived ()

                true

    /// <summary>Restores what <c>EnterContinuation</c> detached for <c>frame</c>.</summary>
    member internal this.LeaveContinuation(frame: obj) =
        if
            this.IsOnGraphThread
            && not (Platform.isNull continuations)
            && continuations.Count > 0
        then
            let top = continuations.Count - 1
            let struct (saved, computation, owner, raised) = continuations[top]

            if Object.ReferenceEquals (saved, frame) then
                continuations.RemoveAt top
                current <- computation
                // Null is always safe: the restored computation's reads take the dispatching path.
                currentNode <- Unchecked.defaultof<Node>
                currentOwner <- owner
                ownerPending <- false
                raisedPending <- raised

                if not (Platform.isNull journal) then
                    journal.LeaveDerived ()

    member internal this.EnterContinuationDelegate =
        let mutable fn = enterContinuationDelegate

        if Platform.isNull (box fn) then
            fn <- this.EnterContinuation
            enterContinuationDelegate <- fn

        fn

    member internal this.LeaveContinuationDelegate =
        let mutable fn = leaveContinuationDelegate

        if Platform.isNull (box fn) then
            fn <- this.LeaveContinuation
            leaveContinuationDelegate <- fn

        fn
#endif

    /// <summary>
    /// The exception a pending read of <c>node</c> raises. Inside a computation's
    /// body, the read is recorded for <c>RunHosted</c>.
    /// </summary>
    member internal this.NotReady(node: INode) : exn =
        let c = this.CurrentComputation

        if not (Platform.isNull (box c)) then
            Tracer.Suspend (this, c, node)
            raisedPending <- node

        NotReadyException node

    /// <summary>
    /// Records <c>failure</c> as the failure of the last failed read, for <c>FailureOf</c> in the run that catches it.
    /// </summary>
    /// <remarks>Records nothing outside every stale read and flush.</remarks>
    member internal _.Raised(failure: Failure) =
        if pullDepth > 0 || flushing then
            lastRaised <- failure

    /// <summary>Raises the exception of <c>failure</c> to the reader of a failed node, as recorded by <c>Raised</c>.</summary>
    member internal this.Raise(failure: Failure) : unit =
        this.Raised failure
        failure.Rethrow ()

    /// <summary>
    /// The failure a run records for <c>error</c>: the last failed read's when it holds <c>error</c>, else
    /// <c>previous</c> when it holds <c>error</c>, else a new failure originating in <c>origin</c>. Clears the last
    /// failed read.
    /// </summary>
    /// <remarks>
    /// A run that catches a failed read of A, then reads a failed B, then rethrows A's exception records it as its own.
    /// </remarks>
    member internal _.FailureOf(error: exn, origin: INode, previous: Failure) : Failure =
        let raised = lastRaised

        if not (Platform.isNull raised) then
            lastRaised <- null

        if
            not (Platform.isNull raised)
            && obj.ReferenceEquals (raised.Error, error)
        then
            raised
        elif
            not (Platform.isNull previous)
            && obj.ReferenceEquals (previous.Error, error)
        then
            previous
        else
            Failure (error, origin)

    /// <summary>True when the running body has raised a pending read.</summary>
    member inline internal _.RaisedPending = not (Platform.isNull (box raisedPending))

    /// <summary>
    /// Forgets the pending reads raised so far in the running body. Called by
    /// a boundary that handles them.
    /// </summary>
    member internal _.ClearRaised() =
        raisedPending <- Unchecked.defaultof<INode>

    /// <summary>
    /// Raises <c>NotReadyException</c> if a pending read raised in the running body
    /// since the last <c>ClearRaised</c>.
    /// </summary>
    member internal _.CheckRaised() =
        if not (Platform.isNull (box raisedPending)) then
            raise (NotReadyException raisedPending)

    /// <summary>
    /// Evaluates <c>body</c> with tracking suppressed.
    /// </summary>
    member internal this.RunUntracked(body: unit -> 'T) =
        this.Entered (
            "An untracked read",
            fun () ->
                let previous = current
                let previousNode = currentNode
                let previousHost = untrackedHost
                let previousInBody = untrackedInBody

                let prevComp = if not (obj.ReferenceEquals (previousNode, null)) then previousNode.Self else previous

                if not (Platform.isNull (box prevComp)) then
                    untrackedInBody <- true

                    if ownerPending || Platform.isNull (box currentOwner) then
                        untrackedHost <- prevComp :?> IScopeHost

                current <- Unchecked.defaultof<IComputation>
                currentNode <- Unchecked.defaultof<Node>

                try
                    body ()
                finally
                    current <- previous
                    currentNode <- previousNode
                    untrackedHost <- previousHost
                    untrackedInBody <- previousInBody
        )

    /// <summary>Evaluates <c>f arg</c> with tracking suppressed, avoiding per-call closure allocation.</summary>
    member internal this.RunUntrackedWith(f: 'A -> 'T, arg: 'A) : 'T =
        this.Entered (
            "An untracked read",
            fun () ->
                let previous = current
                let previousNode = currentNode
                let previousHost = untrackedHost
                let previousInBody = untrackedInBody

                let prevComp = if not (obj.ReferenceEquals (previousNode, null)) then previousNode.Self else previous

                if not (Platform.isNull (box prevComp)) then
                    untrackedInBody <- true

                    if ownerPending || Platform.isNull (box currentOwner) then
                        untrackedHost <- prevComp :?> IScopeHost

                current <- Unchecked.defaultof<IComputation>
                currentNode <- Unchecked.defaultof<Node>

                try
                    f arg
                finally
                    current <- previous
                    currentNode <- previousNode
                    untrackedHost <- previousHost
                    untrackedInBody <- previousInBody
        )

    /// <summary>Evaluates <c>body</c> untracked inside one batch, flushing if allowed on return.</summary>
    member internal this.RunUntrackedBatch(body: unit -> unit) =
        this.Entered (
            "An untracked read",
            fun () ->
                let previous = current
                let previousNode = currentNode
                let previousHost = untrackedHost
                let previousInBody = untrackedInBody

                let prevComp = if not (obj.ReferenceEquals (previousNode, null)) then previousNode.Self else previous

                if not (Platform.isNull (box prevComp)) then
                    untrackedInBody <- true

                    if ownerPending || Platform.isNull (box currentOwner) then
                        untrackedHost <- prevComp :?> IScopeHost

                current <- Unchecked.defaultof<IComputation>
                currentNode <- Unchecked.defaultof<Node>
                batchDepth <- batchDepth + 1
                Tracer.BatchEnter (this, batchDepth)

                try
                    try
                        body ()
                    finally
                        batchDepth <- batchDepth - 1
                        Tracer.BatchExit (this, batchDepth)

                    this.RequestFlushFromEntered ()
                finally
                    current <- previous
                    currentNode <- previousNode
                    untrackedHost <- previousHost
                    untrackedInBody <- previousInBody
        )

#if !FABLE_COMPILER
    /// <summary>Evaluates <c>f arg</c> untracked inside one batch without closure allocation.</summary>
    member internal this.RunUntrackedBatchWith(f: 'A -> unit, arg: 'A) =
        if not (this.Enter "An untracked read") then
            this.RunUntrackedBatchCore (f, arg)
        else
            try
                this.RunUntrackedBatchCore (f, arg)
            finally
                this.Release ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.RunUntrackedBatchCore(f: 'A -> unit, arg: 'A) =
        let previous = current
        let previousNode = currentNode
        let previousHost = untrackedHost
        let previousInBody = untrackedInBody

        let prevComp = if not (obj.ReferenceEquals (previousNode, null)) then previousNode.Self else previous

        if not (Platform.isNull (box prevComp)) then
            untrackedInBody <- true

            if ownerPending || Platform.isNull (box currentOwner) then
                untrackedHost <- prevComp :?> IScopeHost

        current <- Unchecked.defaultof<IComputation>
        currentNode <- Unchecked.defaultof<Node>
        batchDepth <- batchDepth + 1
        Tracer.BatchEnter (this, batchDepth)

        try
            try
                f arg
            finally
                batchDepth <- batchDepth - 1
                Tracer.BatchExit (this, batchDepth)

            this.RequestFlushFromEntered ()
        finally
            current <- previous
            currentNode <- previousNode
            untrackedHost <- previousHost
            untrackedInBody <- previousInBody
#endif

    /// <summary>
    /// Evaluates <c>body</c> with tracking suppressed and <c>host</c>'s scope rule
    /// applied to every node <c>body</c> creates.
    /// </summary>
    member internal _.RunUntrackedHosted(host: IScopeHost, body: unit -> 'T) =
        let previous = current
        let previousNode = currentNode
        let previousOwner = currentOwner
        let previousPending = ownerPending
        let previousHost = untrackedHost
        let previousInBody = untrackedInBody

        if not (obj.ReferenceEquals (previousNode, null)) || not (Platform.isNull (box previous)) then
            untrackedInBody <- true

        current <- Unchecked.defaultof<IComputation>
        currentNode <- Unchecked.defaultof<Node>
        ownerPending <- true
        untrackedHost <- host

        if not (Platform.isNull journal) then
            journal.EnterDerived ()

        try
            body ()
        finally
            current <- previous
            currentNode <- previousNode
            if not ownerPending then
                currentOwner <- previousOwner

            ownerPending <- previousPending
            untrackedHost <- previousHost
            untrackedInBody <- previousInBody

            if not (Platform.isNull journal) then
                journal.LeaveDerived ()

    /// <summary>
    /// <c>RunUntrackedHosted</c> specialised to <c>f arg</c>, avoiding per-call closure allocation.
    /// </summary>
    member internal _.RunUntrackedHostedArg(host: IScopeHost, f: 'A -> 'T, arg: 'A) : 'T =
        let previous = current
        let previousNode = currentNode
        let previousOwner = currentOwner
        let previousPending = ownerPending
        let previousHost = untrackedHost
        let previousInBody = untrackedInBody

        if not (obj.ReferenceEquals (previousNode, null)) || not (Platform.isNull (box previous)) then
            untrackedInBody <- true

        current <- Unchecked.defaultof<IComputation>
        currentNode <- Unchecked.defaultof<Node>
        ownerPending <- true
        untrackedHost <- host

        if not (Platform.isNull journal) then
            journal.EnterDerived ()

        try
            f arg
        finally
            current <- previous
            currentNode <- previousNode
            if not ownerPending then
                currentOwner <- previousOwner

            ownerPending <- previousPending
            untrackedHost <- previousHost
            untrackedInBody <- previousInBody

            if not (Platform.isNull journal) then
                journal.LeaveDerived ()

    /// <summary>
    /// Applies <c>f</c> to <c>arg</c> with tracking suppressed and the scope rule of <c>host</c> applied to every node
    /// created by <c>f</c>.
    /// </summary>
    /// <remarks>
    /// The caller marks the journal's derived section: <c>EffectOn</c>'s action does so on its journaled path.
    /// </remarks>
    member internal _.RunUntrackedHostedWith(host: IScopeHost, f: 'A -> unit, arg: 'A) =
        let previous = current
        let previousNode = currentNode
        let previousOwner = currentOwner
        let previousPending = ownerPending
        let previousHost = untrackedHost
        let previousInBody = untrackedInBody

        if not (obj.ReferenceEquals (previousNode, null)) || not (Platform.isNull (box previous)) then
            untrackedInBody <- true

        current <- Unchecked.defaultof<IComputation>
        currentNode <- Unchecked.defaultof<Node>
        ownerPending <- true
        untrackedHost <- host

        try
            f arg
        finally
            current <- previous
            currentNode <- previousNode
            if not ownerPending then
                currentOwner <- previousOwner

            ownerPending <- previousPending
            untrackedHost <- previousHost
            untrackedInBody <- previousInBody

#if !FABLE_COMPILER
#if !RANVIER_TRACE
    member inline internal _.CanNestInReader =
        not serialised && ownerPending && not (obj.ReferenceEquals (currentNode, null)) && Platform.isNull (box raisedPending)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.LeaveNestedReader(parent: Node, previousOwner: Owner) =
        currentNode <- parent

        if not ownerPending then
            currentOwner <- previousOwner
            ownerPending <- true

    member inline internal _.HasUntrackedHost = not (Platform.isNull (box untrackedHost))

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.SwitchToActionTop(host: IScopeHost, previousOwner: Owner) =
        currentNode <- Unchecked.defaultof<Node>

        if not ownerPending then
            currentOwner <- previousOwner
            ownerPending <- true

        untrackedHost <- host

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.LeaveActionTop(previousOwner: Owner) =
        untrackedHost <- Unchecked.defaultof<IScopeHost>

        if not ownerPending then
            currentOwner <- previousOwner

        ownerPending <- false
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.EnterUntrackedHosted(host: IScopeHost) : UntrackedHostedFrame =
        let saved =
            UntrackedHostedFrame (current, currentNode, currentOwner, ownerPending, untrackedHost, untrackedInBody)

        if not (obj.ReferenceEquals (saved.PreviousNode, null)) then
            untrackedInBody <- true
            currentNode <- Unchecked.defaultof<Node>
        elif not (Platform.isNull (box saved.Previous)) then
            untrackedInBody <- true
            current <- Unchecked.defaultof<IComputation>

        ownerPending <- true
        untrackedHost <- host
        saved

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.LeaveUntrackedHosted(saved: UntrackedHostedFrame) =
        if not (obj.ReferenceEquals (saved.PreviousNode, null)) then
            currentNode <- saved.PreviousNode
        elif not (Platform.isNull (box currentNode)) then
            currentNode <- Unchecked.defaultof<Node>

        if not (Platform.isNull (box saved.Previous)) then
            current <- saved.Previous
        elif not (Platform.isNull (box current)) then
            current <- Unchecked.defaultof<IComputation>

        if not ownerPending then
            currentOwner <- saved.PreviousOwner

        ownerPending <- saved.PreviousPending
        untrackedHost <- saved.PreviousHost
        untrackedInBody <- saved.PreviousInBody
#endif

#if !FABLE_COMPILER
/// <summary>One entry in <c>Graph.queueSlots</c>: holds <c>Node</c> directly for effects/split effects, or <c>Item</c> otherwise.</summary>
and [<Struct; NoEquality; NoComparison>] internal QueueSlot =
    val mutable Item: IScheduled
    val mutable Node: Node

    new(item: IScheduled, node: Node) = { Item = item; Node = node }

/// <summary>Saved tracking, owner, and pending-read context around a hosted computation run.</summary>
and [<Struct; NoEquality; NoComparison>] internal HostedFrame =
    val Previous: IComputation
    val PreviousNode: Node
    val PreviousOwner: Owner
    val PreviousPending: bool
    val PreviousRaised: INode

    new(previous: IComputation, previousNode: Node, previousOwner: Owner, previousPending: bool, previousRaised: INode) =
        {
            Previous = previous
            PreviousNode = previousNode
            PreviousOwner = previousOwner
            PreviousPending = previousPending
            PreviousRaised = previousRaised
        }

/// <summary>Saved tracking, owner, and untracked-host state around an untracked hosted callback run.</summary>
and [<Struct; NoEquality; NoComparison>] internal UntrackedHostedFrame =
    val Previous: IComputation
    val PreviousNode: Node
    val PreviousOwner: Owner
    val PreviousPending: bool
    val PreviousHost: IScopeHost
    val PreviousInBody: bool

    new(previous: IComputation, previousNode: Node, previousOwner: Owner, previousPending: bool, previousHost: IScopeHost, previousInBody: bool) =
        {
            Previous = previous
            PreviousNode = previousNode
            PreviousOwner = previousOwner
            PreviousPending = previousPending
            PreviousHost = previousHost
            PreviousInBody = previousInBody
        }
#endif

#if !FABLE_COMPILER && !RANVIER_TRACE
/// <summary>One overflow slot of an <c>ObserverList</c>: the observer, its node when it has one, and its slot.</summary>
and [<Struct; NoEquality; NoComparison>] internal NodeSlot =
    val mutable Observer: IComputation

    /// <summary>The observer's <c>Node</c>, or null for an observer that is not a memo, effect or split effect.</summary>
    val mutable Node: obj

    /// <summary>The position the observer recorded the edge at, passed back in <c>MovedIn</c>.</summary>
    val mutable Slot: int

    new(observer: IComputation, node: obj, slot: int) = { Observer = observer; Node = node; Slot = slot }

/// <summary>
/// The observers of a signal or a node, held inline by their owner: <c>ObserverSet</c>'s algorithm without its object.
/// </summary>
/// <remarks>
/// <para>
/// No allocation for zero or one observer. Each entry carries the observer's <c>Node</c> when it has one, so a mark
/// reaches a memo, effect or split effect through <c>NodeOps</c> instead of an interface dispatch.
/// </para>
/// <para>
/// The walk order, re-clamping and swap-remove are exactly <c>ObserverSet</c>'s: the differential check pins the
/// order effects are queued in.
/// </para>
/// </remarks>
and [<Struct; NoEquality; NoComparison>] internal ObserverList =
    val mutable First: IComputation
    val mutable FirstNode: obj
    val mutable FirstSlot: int
    val mutable Count: int

    /// <summary>The handle the latest add returned, moved along when a swap-remove moves its observer.</summary>
    val mutable Latest: int

    /// <summary>Observers after the first, <c>Rest[i - 1]</c> at handle <c>i</c>. Null until a second observer.</summary>
    val mutable Rest: NodeSlot[]

    /// <summary>An empty list.</summary>
    static member inline Create() = Unchecked.defaultof<ObserverList>

    /// <summary>The node of the computation linking this edge when it is <c>c</c>'s, otherwise null.</summary>
    static member private NodeOf(c: IComputation) : obj =
        let node = EdgeContext.Observer

        if
            not (Platform.isNull node)
            && obj.ReferenceEquals ((Platform.fastCast<Node> node).Self, c)
        then
            node
        else
            null

    member private this.ObserverAt(handle: int) =
        if handle = 0 then
            this.First
        else
            (Platform.itemAt (handle - 1) this.Rest).Observer

    /// <summary>Appends <c>c</c>, recording <c>slot</c> for its <c>MovedIn</c>, and returns its handle.</summary>
    member this.Add(c: IComputation, slot: int) =
        let handle = this.Count
        let node = ObserverList.NodeOf c

        if handle = 0 then
            this.First <- c
            this.FirstNode <- node
            this.FirstSlot <- slot
            this.Count <- 1
            this.Latest <- 0
        else
            let restIdx = handle - 1

            if Platform.isNull this.Rest then
                this.Rest <- Array.zeroCreate 4
            elif restIdx = this.Rest.Length then
                let grown = Array.zeroCreate (restIdx * 2)
                Array.blit this.Rest 0 grown 0 restIdx
                this.Rest <- grown

            this.Rest[restIdx] <- NodeSlot (c, node, slot)
            this.Count <- handle + 1
            this.Latest <- handle
#if RANVIER_COUNTERS
        Counters.ObserverInserted ()
#endif
        handle

    /// <summary><c>ObserverSet.TryAdd</c>.</summary>
    member this.TryAdd(c: IComputation, slot: int) =
        let count = this.Count

        if count = 0 then
            this.Add (c, slot)
        elif
            this.Latest < count
            && obj.ReferenceEquals (this.ObserverAt this.Latest, c)
        then
            EdgeHandle.Unlinked
        elif count = 1 then
            if obj.ReferenceEquals (this.First, c) then
                EdgeHandle.Unlinked
            else
                this.Add (c, slot)
        elif count <= EdgeHandle.ScanLimit then
            if obj.ReferenceEquals (this.First, c) then
                EdgeHandle.Unlinked
            else
                let restCount = count - 1
                let rest = this.Rest
                let mutable i = 0

                while i < restCount
                      && not (obj.ReferenceEquals ((Platform.itemAt i rest).Observer, c)) do
                    i <- i + 1

                if i < restCount then
                    EdgeHandle.Unlinked
                else
                    this.Add (c, slot)
        else
            EdgeHandle.Unknown

    /// <summary><c>ObserverSet.RemoveAt</c>: a swap-remove that tells the moved observer its new handle.</summary>
    member this.RemoveAt(c: IComputation, taggedHandle: int) =
        if taggedHandle >= 0 then
            let handle = taggedHandle &&& EdgeHandle.IndexMask
            let count = this.Count

            if
                handle >= count
                || not (obj.ReferenceEquals (this.ObserverAt handle, c))
            then
                raise (
                    InvalidOperationException (
                        "Edge handle "
                        + string handle
                        + " of "
                        + string count
                        + " does not hold the observer removed."
                    )
                )

#if RANVIER_COUNTERS
            Counters.ObserverRemoved ()
#endif
            let last = count - 1

            if handle = 0 then
                if last = 0 then
                    // Blanked so a dropped observer is not kept alive after removal.
                    this.First <- Unchecked.defaultof<IComputation>
                    this.FirstNode <- null
                    this.FirstSlot <- EdgeHandle.Unlinked
                    this.Count <- 0
                else
                    let lastRest = last - 1
                    let movedEntry = Platform.itemAt lastRest this.Rest
                    this.Rest[lastRest] <- NodeSlot (Unchecked.defaultof<IComputation>, null, EdgeHandle.Unlinked)
                    this.First <- movedEntry.Observer
                    this.FirstNode <- movedEntry.Node
                    this.FirstSlot <- movedEntry.Slot
                    this.Count <- last

                    if this.Latest = last then
                        this.Latest <- 0

                    NodeOps.MovedIn (movedEntry.Observer, movedEntry.Node, movedEntry.Slot, taggedHandle &&& EdgeHandle.SignalBit)
            elif handle = last then
                this.Rest[last - 1] <- NodeSlot (Unchecked.defaultof<IComputation>, null, EdgeHandle.Unlinked)
                this.Count <- last
            else
                let lastRest = last - 1
                let movedEntry = Platform.itemAt lastRest this.Rest
                this.Rest[handle - 1] <- movedEntry
                this.Rest[lastRest] <- NodeSlot (Unchecked.defaultof<IComputation>, null, EdgeHandle.Unlinked)
                this.Count <- last

                if this.Latest = last then
                    this.Latest <- handle

                NodeOps.MovedIn (
                    movedEntry.Observer,
                    movedEntry.Node,
                    movedEntry.Slot,
                    handle ||| (taggedHandle &&& EdgeHandle.SignalBit)
                )

    /// <summary><c>ObserverSet.NotifyDirty</c>: marks every current observer dirty, walking backwards, re-clamped.</summary>
    member this.NotifyDirty() =
        let count = this.Count

        if count = 1 then
            NodeOps.MarkDirtyOf (this.First, this.FirstNode)
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= this.Count then
                    i <- this.Count - 1

                if i > 0 then
                    let entry = Platform.itemAt (i - 1) this.Rest
                    NodeOps.MarkDirtyOf (entry.Observer, entry.Node)
                    i <- i - 1

            if this.Count > 0 then
                NodeOps.MarkDirtyOf (this.First, this.FirstNode)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member private MarkIdle(observer: IComputation, nodeObj: obj) =
        if Platform.isNull nodeObj then
            observer.MarkDirty ()
        else
            let m = Platform.fastCast<Node> nodeObj

            if m.Kind = NodeKind.Memo && m.Freshness = Freshness.Clean then
                m.Freshness <- Freshness.Dirty

                if m.Observers.Count <> 0 then
                    m.Graph.NoteStaleTransition ()
                    NodeOps.NotifyCheckSpine m
            else
                NodeOps.MarkDirty m

    /// <summary>Marks observers in reverse order while the graph has no active pull or flush.</summary>
    member this.NotifyDirtyIdle() =
        let mutable i = this.Count - 1

        while i > 0 do
            if i >= this.Count then
                i <- this.Count - 1

            if i > 0 then
                let entry = Platform.itemAt (i - 1) this.Rest
                ObserverList.MarkIdle (entry.Observer, entry.Node)
                i <- i - 1

        if this.Count > 0 then
            ObserverList.MarkIdle (this.First, this.FirstNode)

    /// <summary><c>ObserverSet.NotifyDirtyExcept</c>.</summary>
    member this.NotifyDirtyExcept(running: IComputation) =
        let count = this.Count

        if count = 1 then
            let observer = this.First

            if
                not (obj.ReferenceEquals (observer, running))
                || NodeOps.HasReadSlotOf (observer, this.FirstNode, this.FirstSlot)
            then
                NodeOps.MarkDirtyOf (observer, this.FirstNode)
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= this.Count then
                    i <- this.Count - 1

                if i > 0 then
                    let entry = Platform.itemAt (i - 1) this.Rest
                    let observer = entry.Observer

                    if
                        not (obj.ReferenceEquals (observer, running))
                        || NodeOps.HasReadSlotOf (observer, entry.Node, entry.Slot)
                    then
                        NodeOps.MarkDirtyOf (observer, entry.Node)

                    i <- i - 1

            if this.Count > 0 then
                let observer = this.First

                if
                    not (obj.ReferenceEquals (observer, running))
                    || NodeOps.HasReadSlotOf (observer, this.FirstNode, this.FirstSlot)
                then
                    NodeOps.MarkDirtyOf (observer, this.FirstNode)

    /// <summary><c>ObserverSet.NotifyDirtyOther</c>.</summary>
    member this.NotifyDirtyOther(running: IComputation) =
        let count = this.Count

        if count = 1 then
            let observer = this.First

            if not (obj.ReferenceEquals (observer, running)) then
                NodeOps.MarkDirtyOf (observer, this.FirstNode)
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= this.Count then
                    i <- this.Count - 1

                if i > 0 then
                    let entry = Platform.itemAt (i - 1) this.Rest

                    if not (obj.ReferenceEquals (entry.Observer, running)) then
                        NodeOps.MarkDirtyOf (entry.Observer, entry.Node)

                    i <- i - 1

            if this.Count > 0 then
                let observer = this.First

                if not (obj.ReferenceEquals (observer, running)) then
                    NodeOps.MarkDirtyOf (observer, this.FirstNode)

    /// <summary><c>ObserverSet.NotifyCheck</c>.</summary>
    member this.NotifyCheck() =
        let count = this.Count

        if count = 1 then
            NodeOps.MarkCheckOf (this.First, this.FirstNode)
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= this.Count then
                    i <- this.Count - 1

                if i > 0 then
                    let entry = Platform.itemAt (i - 1) this.Rest
                    NodeOps.MarkCheckOf (entry.Observer, entry.Node)
                    i <- i - 1

            if this.Count > 0 then
                NodeOps.MarkCheckOf (this.First, this.FirstNode)
#endif

/// <summary>
/// The reactive state of a memo, effect or split effect: what the Rocq model's <c>node</c> record holds, minus the
/// value, whose type only the public object knows.
/// </summary>
/// <remarks>
/// <para>
/// Non-generic, so marks, checks, the source walk and the scheduler handshake run in <c>NodeOps</c> without an
/// interface dispatch or a shared-generic lookup. <c>Self</c> is the public object, which keeps every identity the
/// graph exposes: observer entries, <c>CurrentComputation</c>, error origins and pending sources.
/// </para>
/// <para>
/// A field a kind does not use is left at its initial value.
/// </para>
/// </remarks>
#if FABLE_COMPILER
and [<NoEquality; NoComparison; CompiledName("Node")>] internal ComputationNode =
#else
and [<NoEquality; NoComparison>] internal Node =
#endif
    {
        Id: int
        Kind: byte
        Graph: Graph

        /// <summary>Runs the public object's body: a memo's <c>Run</c>, an effect's or split effect's <c>Run</c>.</summary>
#if FABLE_COMPILER
        RunCallback: Node -> unit
#else
        RunCallback: Action<Node>
#endif

        /// <summary>The public object. Set by its attach, before anything can mark or run it.</summary>
        mutable Self: IComputation
        mutable Freshness: Freshness
        mutable Status: Status

        /// <summary>0 idle, 1 running its body, 2 resolving its own <c>Check</c>.</summary>
        mutable Active: byte
#if !FABLE_COMPILER && !RANVIER_TRACE
        /// <summary>The next resolving ancestor while this memo is on the single-source check stack.</summary>
        mutable CheckParent: Node
#endif
        mutable Disposed: bool

        /// <summary>
        /// Set when a memo's run ends stale, from a mark received during the run. While set, the next mark notifies
        /// the observers, as the <c>Clean</c> transition does, and clears it.
        /// </summary>
        mutable Unannounced: bool
        mutable Queued: bool

        /// <summary>Set while an effect's cleanups are discharged: its marks then refuse to re-queue it.</summary>
        mutable InCleanup: bool

        /// <summary>Set on an effect whose readers are marked only when its body writes; it runs with the passes.</summary>
        mutable Publishes: bool

        /// <summary>Set when a pure body creates an owned node, and cleared when the run fails for it.</summary>
        mutable Violated: bool

        /// <summary>Set by a memo's first successful run.</summary>
        mutable Published: bool
        mutable Runs: int
        mutable Failure: Failure

        /// <summary>The source of the last run's last pending read.</summary>
        mutable PendingSource: INode

        /// <summary>The owner of the nodes the body creates, allocated by the first of them.</summary>
        mutable Scope: Owner
        mutable Link: OwnerLink
        mutable Sources: SourceList
        mutable Observers: ObserverList
    }

/// <summary>
/// The graph operations of a <c>Node</c>, called directly by its edges and by the public object's members.
/// </summary>
/// <remarks>
/// Each operation is the memo's, effect's or split effect's former interface member, moved here unchanged: the
/// differential check pins every mark, run and queue order.
/// </remarks>
and [<AbstractClass; Sealed>] internal NodeOps =
    /// <summary>A node of <c>kind</c> on <c>graph</c>, taking the graph's next id.</summary>
#if FABLE_COMPILER
    static member Create(graph: Graph, kind: byte, runCallback: Node -> unit, freshness: Freshness) : Node =
#else
    static member Create(graph: Graph, kind: byte, runCallback: Action<Node>, freshness: Freshness) : Node =
#endif
        let id = graph.NextId ()

        {
            Id = id
            Kind = kind
            Graph = graph
            RunCallback = runCallback
            Self = Unchecked.defaultof<IComputation>
            Freshness = freshness
            Status = Status.Uninitialized
            Active = 0uy
#if !FABLE_COMPILER && !RANVIER_TRACE
            CheckParent = Unchecked.defaultof<Node>
#endif
            Disposed = false
            Unannounced = false
            Queued = false
            InCleanup = false
            Publishes = false
            Violated = false
            Published = false
            Runs = 0
            Failure = null
            PendingSource = Unchecked.defaultof<INode>
            Scope = Unchecked.defaultof<Owner>
            Link = null
            Sources = SourceList.Create ()
            Observers = ObserverList.Create ()
        }

    /// <summary>Runs the public object's body.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member Run(n: Node) =
#if FABLE_COMPILER
        n.RunCallback n
#else
        n.RunCallback.Invoke n
#endif

    /// <summary>The <c>IScheduled</c> face of an effect or split effect.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member Scheduled(n: Node) : IScheduled = Platform.fastCast<IScheduled> (box n.Self)

    // ---------------------------------------------------------------------------------------------------------
    // Marks
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>A memo's <c>MarkDirty</c>.</summary>
    static member MemoMarkDirty(n: Node) =
        if n.Freshness <> Freshness.Dirty then
            let wasClean = n.Freshness = Freshness.Clean
            n.Freshness <- Freshness.Dirty

            // `Check`, not `Dirty`: this memo knows its own body has to re-run, but not yet whether re-running it
            // will produce a different value. Only on the Clean transition — an already-`Check` memo has told
            // them this once.
            if wasClean then
                n.Graph.NoteStaleTransition ()
                NodeOps.NotifyCheck n
            elif n.Unannounced then
                n.Unannounced <- false
                n.Graph.NoteStaleTransition ()
                NodeOps.NotifyCheck n
        elif n.Unannounced then
            n.Unannounced <- false
            n.Graph.NoteStaleTransition ()
            NodeOps.NotifyCheck n

    /// <summary>A memo's <c>MarkCheck</c>.</summary>
    static member MemoMarkCheck(n: Node) =
        if n.Freshness = Freshness.Clean then
            n.Freshness <- Freshness.Check
            NodeOps.NotifyCheck n
        elif n.Unannounced then
            n.Unannounced <- false
            n.Graph.NoteStaleTransition ()
            NodeOps.NotifyCheck n

    /// <summary>
    /// An effect's or split effect's <c>MarkDirty</c>. Upgrades even when already queued: an effect scheduled on a
    /// <c>Check</c> and since told <c>Dirty</c> must carry the stronger fact to <c>Execute</c>.
    /// </summary>
    static member EffectMarkDirty(n: Node) =
        if not n.Disposed && not n.InCleanup then
            n.Freshness <- Freshness.Dirty

            if not n.Queued then
                n.Queued <- true

                if n.Publishes then
                    n.Graph.SchedulePass (NodeOps.Scheduled n)
                else
                    n.Graph.ScheduleNode n

    /// <summary>An effect's or split effect's <c>MarkCheck</c>.</summary>
    static member EffectMarkCheck(n: Node) =
        if not n.Disposed && not n.InCleanup then
            if n.Freshness = Freshness.Clean then
                n.Freshness <- Freshness.Check

            if not n.Queued then
                n.Queued <- true

                if n.Publishes then
                    n.Graph.SchedulePass (NodeOps.Scheduled n)
                else
                    n.Graph.ScheduleNode n

    /// <summary>Tells a memo's observers <c>Check</c>; no call when it has none.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member NotifyCheck(n: Node) =
        if n.Observers.Count <> 0 then
#if !FABLE_COMPILER && !RANVIER_TRACE
            NodeOps.NotifyCheckSpine n
#else
            n.Observers.NotifyCheck ()
#endif

#if !FABLE_COMPILER && !RANVIER_TRACE
    /// <summary>
    /// Iterative single-memo <c>MarkCheck</c> spine walk (Theorem B3, <c>iterative_mark_check_chain_eq</c>).
    /// </summary>
    static member NotifyCheckSpine(n: Node) =
        let mutable cur = n
        let mutable walking = true

        while walking do
            if
                cur.Observers.Count = 1
                && not (Platform.isNull cur.Observers.FirstNode)
            then
                let m = Platform.fastCast<Node> cur.Observers.FirstNode

                if m.Kind = NodeKind.Memo && m.Freshness = Freshness.Clean then
                    m.Freshness <- Freshness.Check

                    if m.Observers.Count = 0 then
                        walking <- false
                    else
                        cur <- m
                else
                    NodeOps.MarkCheck m
                    walking <- false
            else
                cur.Observers.NotifyCheck ()
                walking <- false
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member MarkDirty(n: Node) =
        if n.Kind = NodeKind.Memo then
#if !FABLE_COMPILER && !RANVIER_TRACE
            if n.Freshness = Freshness.Clean && n.Observers.Count = 0 then
                n.Freshness <- Freshness.Dirty
                n.Graph.NoteStaleTransition ()
            else
                NodeOps.MemoMarkDirty n
#else
            NodeOps.MemoMarkDirty n
#endif
        else
            NodeOps.EffectMarkDirty n

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member MarkCheck(n: Node) =
        if n.Kind = NodeKind.Memo then
            NodeOps.MemoMarkCheck n
        else
            NodeOps.EffectMarkCheck n

    /// <summary>Marks an observer entry dirty: through its node when it has one.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member MarkDirtyOf(observer: IComputation, node: obj) =
        if Platform.isNull node then
            observer.MarkDirty ()
        else
            NodeOps.MarkDirty (Platform.fastCast<Node> node)

    /// <summary>Marks an observer entry <c>Check</c>: through its node when it has one.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member MarkCheckOf(observer: IComputation, node: obj) =
        if Platform.isNull node then
            observer.MarkCheck ()
        else
            NodeOps.MarkCheck (Platform.fastCast<Node> node)

    /// <summary>Whether an observer entry's current run has read past <c>slot</c>.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member HasReadSlotOf(observer: IComputation, node: obj, slot: int) =
        if Platform.isNull node then
            observer.HasReadSlot slot
        else
            slot < (Platform.fastCast<Node> node).Sources.Cursor

    /// <summary>Tells an observer entry its edge at <c>slot</c> now has <c>handle</c>.</summary>
    static member MovedIn(observer: IComputation, node: obj, slot: int, handle: int) =
        if Platform.isNull node then
            observer.MovedIn (slot, handle)
        else
            (Platform.fastCast<Node> node).Sources.MovedIn (slot, handle)

#if !FABLE_COMPILER && !RANVIER_TRACE
    /// <summary>Promotes a sole checked observer in place when no further notification or scheduling is due.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member TryPromoteTop(n: Node) =
        if n.Observers.Count = 1 && n.Graph.StaleTransitions = 0 && not (Platform.isNull n.Observers.FirstNode) then
            let m = Platform.fastCast<Node> n.Observers.FirstNode

            if
                m.Freshness = Freshness.Check
                && (if m.Kind = NodeKind.Memo then
                        not m.Unannounced
                    else
                        m.Queued && not m.Disposed && not m.InCleanup)
            then
                m.Freshness <- Freshness.Dirty
                true
            else
                false
        else
            false

    /// <summary><c>NotifyMoved</c> after <c>LeaveHostedTop</c>, where <c>CurrentComputation</c> is guaranteed null.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member NotifyMovedTop(n: Node) =
        let count = n.Observers.Count

        if count <> 0 then
            let g = n.Graph

            if count = 1 && g.StaleTransitions = 0 && not (Platform.isNull n.Observers.FirstNode) then
                let m = Platform.fastCast<Node> n.Observers.FirstNode

                if
                    m.Kind = NodeKind.Memo
                    && m.Freshness = Freshness.Check
                    && not m.Unannounced
                then
                    m.Freshness <- Freshness.Dirty
                else
                    NodeOps.MarkDirty m
            elif g.StaleTransitions = 0 then
                n.Observers.NotifyDirty ()
            else
                n.Observers.NotifyDirtyExcept Unchecked.defaultof<IComputation>
#endif

    /// <summary>
    /// Marks a moved memo's observers dirty, skipping the running computation as <c>Graph.NotifyMoved</c> does.
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member NotifyMoved(n: Node) =
        if n.Observers.Count <> 0 then
            let g = n.Graph
#if !FABLE_COMPILER && !RANVIER_TRACE
            if
                n.Observers.Count = 1
                && g.StaleTransitions = 0
                && not (Platform.isNull n.Observers.FirstNode)
            then
                if not (obj.ReferenceEquals (n.Observers.First, g.CurrentComputation)) then
                    let m = Platform.fastCast<Node> n.Observers.FirstNode

                    if
                        m.Kind = NodeKind.Memo
                        && m.Freshness = Freshness.Check
                        && not m.Unannounced
                    then
                        m.Freshness <- Freshness.Dirty
                    else
                        NodeOps.MarkDirty m
            elif g.StaleTransitions = 0 then
                n.Observers.NotifyDirtyOther g.CurrentComputation
            else
                n.Observers.NotifyDirtyExcept g.CurrentComputation
#else
            if g.StaleTransitions = 0 then
                n.Observers.NotifyDirtyOther g.CurrentComputation
            else
                n.Observers.NotifyDirtyExcept g.CurrentComputation
#endif

    // ---------------------------------------------------------------------------------------------------------
    // Edges
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>Records a read of <c>source</c> by the running node. Ignored once the node is disposed.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member AddSource(n: Node, source: ISource) =
        if not n.Disposed then
#if !FABLE_COMPILER && !RANVIER_TRACE
            n.Sources.AddFrom (n.Self, n, source)
#else
            n.Sources.Add (n.Self, source)
#endif

    // ---------------------------------------------------------------------------------------------------------
    // The check walk
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>Brings the source at <c>i</c> up to date when it is not a signal: a memo's node directly.</summary>
#if !FABLE_COMPILER && !RANVIER_TRACE
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member RefreshSourceAt(n: Node, i: int) =
        if i = 0 then
            if n.Sources.firstHandle < EdgeHandle.SignalBit then
                let sourceNode = n.Sources.firstNode

                if Platform.isNull sourceNode then
                    n.Sources.first.UpdateIfNecessary ()
                else
                    NodeOps.EnsureCurrent (Platform.fastCast<Node> sourceNode)
        elif i = 1 then
            if n.Sources.secondHandle < EdgeHandle.SignalBit then
                let sourceNode = n.Sources.secondNode

                if Platform.isNull sourceNode then
                    n.Sources.second.UpdateIfNecessary ()
                else
                    NodeOps.EnsureCurrent (Platform.fastCast<Node> sourceNode)
        else
            let s = Platform.itemAt (i - 2) n.Sources.rest

            if s.Handle < EdgeHandle.SignalBit then
                if Platform.isNull s.Node then
                    s.Source.UpdateIfNecessary ()
                else
                    NodeOps.EnsureCurrent (Platform.fastCast<Node> s.Node)

    /// <summary>Whether a source at <c>i</c> that is not a signal is live and not <c>Clean</c>.</summary>
    static member private SourceStale(source: ISource, sourceNode: obj) =
        if Platform.isNull sourceNode then
            source.IsStale
        else
            let m = Platform.fastCast<Node> sourceNode
            m.Freshness <> Freshness.Clean && not m.Disposed

    /// <summary>Whether any recorded source is currently stale.</summary>
    static member HasStaleSource(n: Node) =
        let count = n.Sources.count

        if count = 0 then
            false
        elif
            n.Sources.firstHandle < EdgeHandle.SignalBit
            && NodeOps.SourceStale (n.Sources.first, n.Sources.firstNode)
        then
            true
        elif count = 1 then
            false
        elif
            n.Sources.secondHandle < EdgeHandle.SignalBit
            && NodeOps.SourceStale (n.Sources.second, n.Sources.secondNode)
        then
            true
        else
            let limit = count - 2
            let rest = n.Sources.rest
            let mutable i = 0

            while i < limit
                  && (let s = Platform.itemAt i rest

                      s.Handle >= EdgeHandle.SignalBit
                      || not (NodeOps.SourceStale (s.Source, s.Node))) do
                i <- i + 1

            i < limit
#else
    static member RefreshSourceAt(n: Node, i: int) = n.Sources.RefreshSourceAt i

    /// <summary>Whether any recorded source is currently stale.</summary>
    static member HasStaleSource(n: Node) = n.Sources.HasStaleSource ()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member CheckActiveSource(n: Node, i: int) =
        if n.Freshness = Freshness.Check then
            let source = n.Sources.SourceAt i

            if source.IsStale && source.IsActive then
                NodeOps.MarkDirty n

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member ResolveCheckAgain(n: Node) =
        let g = n.Graph
        let mutable again = NodeOps.HasStaleSource n

        while again do
            let before = g.StaleTransitions
            let mutable i = 0

            while n.Freshness = Freshness.Check && i < n.Sources.Count do
                NodeOps.RefreshSourceAt (n, i)

                if g.ActiveSkips <> 0 then
                    NodeOps.CheckActiveSource (n, i)

                i <- i + 1

            again <-
                n.Freshness = Freshness.Check
                && g.StaleTransitions <> before
                && NodeOps.HasStaleSource n

    /// <summary>
    /// Asks each source, in read order, whether it moved, until one marks the node <c>Dirty</c>. Shared by a memo's
    /// <c>ResolveCheck</c> and an effect's <c>Execute</c>; leaves the node in <c>Check</c> when nothing moved.
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member WalkSources(n: Node) =
        let g = n.Graph
        let before = g.StaleTransitions
        let mutable i = 0

        while n.Freshness = Freshness.Check && i < n.Sources.Count do
            NodeOps.RefreshSourceAt (n, i)

            if g.ActiveSkips <> 0 then
                NodeOps.CheckActiveSource (n, i)

            i <- i + 1

        if
            n.Freshness = Freshness.Check
            && g.StaleTransitions <> before
        then
            NodeOps.ResolveCheckAgain n

    /// <summary>
    /// Resolves a memo's <c>Check</c> into <c>Clean</c> or <c>Dirty</c>. A source that recomputes to a new value marks
    /// this memo <c>Dirty</c> from inside the walk; reaching the end still in <c>Check</c> means nothing above moved.
    /// </summary>
    static member ResolveCheck(n: Node) =
        let g = n.Graph
        Tracer.CheckStart (g, n.Id)
        n.Active <- 2uy
        NodeOps.WalkSources n
        n.Active <- 0uy
        Tracer.CheckResolved (g, n.Id, (n.Freshness = Freshness.Dirty))

        if n.Freshness = Freshness.Check then
            n.Freshness <- Freshness.Clean
            n.Unannounced <- false

#if !FABLE_COMPILER && !RANVIER_TRACE
    /// <summary>Whether <c>n</c> reads one source, and that source is a memo's node.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member SingleMemoSource(n: Node) =
        n.Sources.count = 1
        && n.Sources.firstHandle < EdgeHandle.SignalBit
        && not (Platform.isNull n.Sources.firstNode)

    /// <summary>
    /// Iterative single-memo <c>Check</c>-chain refresh without an EH frame or recursive call stack (Theorem B4,
    /// <c>iterative_chain_refresh_eq</c>).
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member RefreshChain(n: Node) =
        let g = n.Graph
        let before = g.StaleTransitions
        let mutable cur = n
        let mutable parent = Unchecked.defaultof<Node>
        let mutable descending = true

        while descending do
            cur.Active <- 2uy
            cur.CheckParent <- parent
            parent <- cur
            let source = Platform.fastCast<Node> cur.Sources.firstNode

            if
                source.Freshness = Freshness.Check
                && source.Active = 0uy
                && not source.Disposed
                && NodeOps.SingleMemoSource source
            then
                cur <- source
            else
                if source.Freshness = Freshness.Dirty && source.Active = 0uy && not source.Disposed then
                    NodeOps.Recompute source
                    g.ClearLastRaisedIfTop ()
                else
                    NodeOps.EnsureCurrent source

                descending <- false

        while not (obj.ReferenceEquals (parent, null)) do
            let x = parent
            parent <- x.CheckParent
            x.CheckParent <- Unchecked.defaultof<Node>

            if g.ActiveSkips <> 0 || x.Freshness = Freshness.Check then
                NodeOps.FinishChainCheck (g, x, before)

            x.Active <- 0uy

            if x.Freshness = Freshness.Check then
                x.Freshness <- Freshness.Clean
                x.Unannounced <- false
            elif not (obj.ReferenceEquals (parent, null)) && x.Freshness = Freshness.Dirty then
                NodeOps.Recompute x

    static member private FinishChainCheck(g: Graph, x: Node, before: int) =
        if g.ActiveSkips <> 0 && x.Sources.count > 0 then
            NodeOps.CheckActiveSource (x, 0)

        if x.Freshness = Freshness.Check then
            let mutable i = 1

            while x.Freshness = Freshness.Check && i < x.Sources.count do
                NodeOps.RefreshSourceAt (x, i)

                if g.ActiveSkips <> 0 then
                    NodeOps.CheckActiveSource (x, i)

                i <- i + 1

            if x.Freshness = Freshness.Check && g.StaleTransitions <> before then
                NodeOps.ResolveCheckAgain x
#endif

    // ---------------------------------------------------------------------------------------------------------
    // Memo refresh
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Discharges the previous run's scope, then runs the body once, unless the memo was disposed meanwhile.
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member Recompute(n: Node) =
        if Platform.isNull (box n.Scope) then
            NodeOps.Run n
        else
            NodeOps.RecomputeScoped n

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member RecomputeScoped(n: Node) =
        let before = n.Runs
        n.Freshness <- Freshness.Clean
        n.Graph.Discharge n.Scope

        if not n.Disposed then
            if n.Runs = before then
                NodeOps.Run n
            elif n.Freshness = Freshness.Dirty then
                NodeOps.Recompute n
        elif n.Freshness = Freshness.Clean then
            n.Unannounced <- false

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member RefreshActive(n: Node) =
        n.Graph.NoteActiveSkip ()

        if n.Active = 2uy then
            NodeOps.MemoMarkDirty n

    /// <summary>Brings a stale memo current.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member Refresh(n: Node) =
        if n.Active = 0uy then
            if n.Freshness = Freshness.Check then
#if !FABLE_COMPILER && !RANVIER_TRACE
                if
                    not (Platform.isNull n.Sources.firstNode)
                    && (n.Sources.count = 1 || (Platform.fastCast<Node> n.Sources.firstNode).Freshness = Freshness.Dirty)
                    && n.Graph.Deferring
                then
                    NodeOps.RefreshChain n
                else
                    NodeOps.ResolveCheck n
#else
                NodeOps.ResolveCheck n
#endif

            if n.Freshness = Freshness.Dirty then
                NodeOps.Recompute n
        else
            NodeOps.RefreshActive n

    /// <summary>Brings a stale memo current when <c>Graph.Deferring</c> is false.</summary>
    static member Pull(n: Node) =
        let g = n.Graph
#if !FABLE_COMPILER
        if g.TryEnterPullFast () then
            try
                NodeOps.Refresh n
            with _ ->
                g.ExitPullFastOnException ()
                reraise ()

            if g.ExitPullFast () then
                g.FinishPullSettle ()
        else
#endif
        g.EnterPull ()

        try
            NodeOps.Refresh n
        finally
            g.ExitPull ()

    /// <summary>
    /// <c>Pull</c>, leaving an owed flush for the caller to settle once it has read the memo's fields. True when the
    /// caller must settle, or the memo is still stale.
    /// </summary>
    static member PullWithoutSettle(n: Node) : bool =
        let g = n.Graph
#if !FABLE_COMPILER
        if g.TryEnterPullFast () then
            try
                NodeOps.Refresh n
            with _ ->
                g.ExitPullFastOnException ()
                reraise ()

            g.ExitPullFast ()
            || n.Freshness <> Freshness.Clean
        else
#endif
        g.EnterPull ()

        try
            NodeOps.Refresh n
        with _ ->
            g.ExitPull ()
            reraise ()

        g.ExitPullDeferSettle ()
        || n.Freshness <> Freshness.Clean

    /// <summary>A memo's <c>UpdateIfNecessary</c>.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member EnsureCurrent(n: Node) =
        if n.Freshness <> Freshness.Clean && not n.Disposed then
            let g = n.Graph

            if g.Deferring then
                NodeOps.Refresh n
                g.ClearLastRaisedIfTop ()
            else
                NodeOps.Pull n

    /// <summary>
    /// Brings a stale memo current for a read: true when the caller must settle an owed flush after reading the memo's
    /// fields, or the memo is still stale.
    /// </summary>
    static member RefreshForRead(n: Node) : bool =
        n.Freshness <> Freshness.Clean
        && not n.Disposed
        && (let g = n.Graph

            if g.Deferring then
                NodeOps.Refresh n
                g.ClearLastRaisedIfTop ()
                n.Freshness <> Freshness.Clean
            else
                NodeOps.PullWithoutSettle n)

    /// <summary>
    /// Brings a stale memo current for an untracked read: true when the caller must settle an owed flush.
    /// </summary>
    static member RefreshForUntrackedRead(n: Node) : bool =
        n.Freshness <> Freshness.Clean
        && not n.Disposed
        && (let g = n.Graph

            if g.Deferring then
                NodeOps.Refresh n
                g.ClearLastRaisedIfTop ()
                false
            else
                NodeOps.PullWithoutSettle n)

    // ---------------------------------------------------------------------------------------------------------
    // Effects & Idle Fused Flush (Theorem B5)
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// An effect's or split effect's <c>Execute</c>. <c>Queued</c> stays set across the resolution walk: a source
    /// recomputed by the walk marks this effect dirty from inside it, and a cleared flag would queue it twice.
    /// </summary>
    static member Execute(n: Node) =
        if n.Disposed then
            n.Queued <- false
        else
            if n.Freshness = Freshness.Check then
                let g = n.Graph
                Tracer.CheckStart (g, n.Id)
                NodeOps.WalkSources n
                Tracer.CheckResolved (g, n.Id, (n.Freshness = Freshness.Dirty))

                if g.HasLastRaised then
                    g.ClearLastRaised ()

                if n.Freshness = Freshness.Check then
                    n.Freshness <- Freshness.Clean

            let shouldRun = n.Freshness = Freshness.Dirty
            n.Queued <- false

            // Clean before the body: an invalidation raised by the body itself has to survive the run that raised it.
            n.Freshness <- Freshness.Clean

            if shouldRun then
                NodeOps.Run n

#if !FABLE_COMPILER && !RANVIER_TRACE
    /// <summary>Runs the sole memo and its scheduled observer in one flush (<c>fused_single_memo_effect_flush_eq</c>).</summary>
    static member RunFusedMemoEffect(g: Graph, n: Node, e: Node) =
        let prevAmbient = g.BeginFusedFlush ()
        e.Queued <- true
        e.Freshness <- Freshness.Check
        NodeOps.Run n

        if e.Disposed then
            e.Queued <- false
        else
            if e.Freshness = Freshness.Check then
                if g.ActiveSkips <> 0 && e.Sources.count > 0 then
                    NodeOps.CheckActiveSource (e, 0)

                if e.Freshness = Freshness.Check && g.StaleTransitions <> 0 then
                    NodeOps.ResolveCheckAgain e

                if g.HasLastRaised then
                    g.ClearLastRaised ()

            let shouldRun = e.Freshness = Freshness.Dirty
            e.Queued <- false
            e.Freshness <- Freshness.Clean

            if shouldRun then
                NodeOps.Run e

        g.EndFusedFlush prevAmbient

    /// <summary>Runs the sole scheduled signal observer in one flush (<c>fused_single_signal_effect_eq</c>).</summary>
    static member RunFusedEffect(g: Graph, n: Node) =
        let prevAmbient = g.BeginFusedFlush ()
        n.Freshness <- Freshness.Clean
        NodeOps.Run n
        g.EndFusedFlush prevAmbient
#endif

/// <summary>
/// A settable source.
/// </summary>
type Signal<'T> private (graph: Graph, initial: 'T, equal: IEqualityComparer<'T>) =
    let id = graph.NextId ()
#if FABLE_COMPILER || RANVIER_TRACE
    let observers = ObserverList.Create ()
    do Tracer.Bind (observers, graph, id)
#else
    /// <summary>The signal's readers, held inline: no allocation for zero or one reader.</summary>
    let mutable observers = ObserverList.Create ()
#endif
    let mutable value = initial

#if RANVIER_COUNTERS
    do Counters.SignalCreated ()
#endif

    do Tracer.SignalNew (graph, id, observers)

    // Resolved once, here, rather than per write: the policy's generic member
    // is the only place the value type is known, and a typed comparer keeps the
    // cutoff test allocation-free.
    /// <summary>
    /// A signal holding <c>initial</c>, compared by the comparer <c>graph</c>'s equality policy supplies for <c>'T</c>.
    /// </summary>
    new(graph: Graph, initial: 'T) = Signal<'T>(graph, initial, graph.EncodedComparer<'T>())

    static member internal CreateWithComparer(graph: Graph, initial: 'T, comparer: IEqualityComparer<'T>) =
        Signal<'T>(graph, initial, ComparerSentinel.encode comparer)

    static member internal OfEncoded(graph: Graph, initial: 'T, encodedEqual: IEqualityComparer<'T>) =
        Signal<'T>(graph, initial, encodedEqual)

#if RANVIER_TRACE
    interface ITracedNode with
        member _.TraceGraph = box graph
#endif

    interface INode with
        member _.Id = id
        member _.Status = Status.None

    interface ISource with
        member _.AddObserver(c, slot) =
            observers.Add (c, slot) ||| EdgeHandle.SignalBit

        member _.TryAddObserver(c, slot) =
            let h = observers.TryAdd (c, slot)

            if h >= 0 then h ||| EdgeHandle.SignalBit else h

        member _.RemoveObserverAt(c, handle) =
            observers.RemoveAt (c, handle)

        /// <summary>
        /// Nothing to bring current: the value is whatever was last written.
        /// </summary>
        member _.UpdateIfNecessary() = ()

        member _.IsStale = false

        member _.IsActive = false

    /// <summary>
    /// Transparent, tracked read. A plain signal is never pending, so this
    /// never throws.
    /// </summary>
#if FABLE_COMPILER
    member this.Value
        with get () =
            graph.Track (this :> ISource)
            value
        and set v =
            // The guard sits before the cutoff, not after: an off-thread write
            // that happens to be equal is still a bug, and one that would
            // otherwise only show up on the write that differs.
            graph.Entered (
                "A signal write",
                fun () ->
                    // Written with && so that Fable emits a short-circuit, not a ternary, on the unjournaled path.
                    if
                        not (
                            not (Platform.isNull graph.Journal)
                            && not (this.RecordWrite v)
                        )
                    then
                        if not (ComparerSentinel.equals equal value v) then
                            value <- v
                            Tracer.Write (observers, true, box v)

                            observers.NotifyDirty ()

                            Tracer.Notified observers

                            graph.RequestFlushFromEntered ()
                        else
                            Tracer.Write (observers, false, box v)
            )
#else
    member this.Value
        with [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>] get () =
            graph.Track (this :> ISource)
            value
        and [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>] set (v: 'T) =
            this.SetFast v

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.SetFast(v: 'T) =
        // The guard sits before the cutoff, not after: an off-thread write
        // that happens to be equal is still a bug, and one that would
        // otherwise only show up on the write that differs.
        let tok = graph.FastWriteToken

        if tok = OwnerThreadToken.Current || tok = 0L then
            if obj.ReferenceEquals (equal, null) then
                let unchanged =
                    if typeof<'T>.IsValueType then
                        EqualityComparer<'T>.Default.Equals(value, v)
                    else
                        obj.ReferenceEquals (box value, box v)

                if not unchanged then
                    this.SetChanged v
                else
                    Tracer.Write (observers, false, box v)
            else
                this.SetCustom v
        else
            this.SetSlow v

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.SetCustom(v: 'T) =
        if not (equal.Equals (value, v)) then
            value <- v
            Tracer.Write (observers, true, box v)

            observers.NotifyDirty ()

            Tracer.Notified observers

            graph.RequestFlushFromEntered ()
        else
            Tracer.Write (observers, false, box v)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.SetSlow(v: 'T) =
        if not (graph.Enter "A signal write") then
            if Platform.isNull graph.Journal then
                if not (ComparerSentinel.equals equal value v) then
                    this.SetChanged v
                else
                    Tracer.Write (observers, false, box v)
            else
                this.SetJournaled v
        else
            this.SetAfterAcquire v

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.SetChanged(v: 'T) =
        value <- v
#if !RANVIER_TRACE
        if observers.Count = 1 && not (Platform.isNull observers.FirstNode) then
            let n = Platform.fastCast<Node> observers.FirstNode

            if n.Kind = NodeKind.Memo && n.Freshness = Freshness.Clean then
                let count = n.Observers.Count

                if count = 1 && not (Platform.isNull n.Observers.FirstNode) then
                    let e = Platform.fastCast<Node> n.Observers.FirstNode

                    if
                        e.Kind <> NodeKind.Memo
                        && e.Freshness = Freshness.Clean
                        && not e.Disposed
                        && not e.InCleanup
                        && not e.Publishes
                        && not e.Queued
                        && e.Sources.count = 1
                        && obj.ReferenceEquals (e.Sources.firstNode, n)
                        && not n.Disposed
                        && n.Active = 0uy
                        && Platform.isNull (box n.Scope)
                        && graph.CanFuseIdleFlush ()
                    then
                        NodeOps.RunFusedMemoEffect (graph, n, e)
                    else
                        n.Freshness <- Freshness.Dirty
                        n.Graph.NoteStaleTransition ()
                        NodeOps.NotifyCheckSpine n

                        if graph.QueuePending then
                            graph.RequestFlushFromEntered ()
                else
                    n.Freshness <- Freshness.Dirty
                    n.Graph.NoteStaleTransition ()

                    if count <> 0 then
                        NodeOps.NotifyCheckSpine n

                    if graph.QueuePending then
                        graph.RequestFlushFromEntered ()
            elif
                n.Kind <> NodeKind.Memo
                && not n.Disposed
                && not n.InCleanup
                && not n.Publishes
                && not n.Queued
                && graph.CanFuseIdleFlush ()
            then
                NodeOps.RunFusedEffect (graph, n)
            else
                NodeOps.MarkDirty n

                if graph.QueuePending then
                    graph.RequestFlushFromEntered ()
        elif observers.Count > 0 then
            if graph.IsIdleForMarks () then
                observers.NotifyDirtyIdle ()
            else
                observers.NotifyDirty ()

            if graph.QueuePending then
                graph.RequestFlushFromEntered ()
#else
        Tracer.Write (observers, true, box v)
        observers.NotifyDirty ()
        Tracer.Notified observers

        if graph.QueuePending then
            graph.RequestFlushFromEntered ()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.SetJournaled(v: 'T) =
        if this.RecordWrite v then
            if not (ComparerSentinel.equals equal value v) then
                this.SetChanged v
            else
                Tracer.Write (observers, false, box v)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.SetAfterAcquire(v: 'T) =
        try
            if Platform.isNull graph.Journal then
                if not (ComparerSentinel.equals equal value v) then
                    value <- v
                    Tracer.Write (observers, true, box v)

                    observers.NotifyDirty ()

                    Tracer.Notified observers

                    graph.RequestFlushFromEntered ()
                else
                    Tracer.Write (observers, false, box v)
            else
                this.SetJournaled v
        finally
            graph.Release ()
#endif

    /// <summary>
    /// Writes <c>v</c> as the setter does, leaving <c>running</c> unmarked. For a computation that reads the new value
    /// in its current run.
    /// </summary>
#if FABLE_COMPILER
    member internal _.WriteExcept(v: 'T, running: IComputation) =
        graph.Entered (
            "A signal write",
            fun () ->
                if not (ComparerSentinel.equals equal value v) then
                    value <- v
                    Tracer.Write (observers, true, box v)

                    observers.NotifyDirtyOther running

                    Tracer.Notified observers
                    graph.RequestFlushFromEntered ()
                else
                    Tracer.Write (observers, false, box v)
        )
#else
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.WriteExcept(v: 'T, running: IComputation) =
        let tok = graph.FastWriteToken

        if tok = OwnerThreadToken.Current || tok = 0L then
            if obj.ReferenceEquals (equal, null) then
                let unchanged =
                    if typeof<'T>.IsValueType then
                        EqualityComparer<'T>.Default.Equals(value, v)
                    else
                        obj.ReferenceEquals (box value, box v)

                if not unchanged then
                    this.WriteExceptChanged (v, running)
                else
                    Tracer.Write (observers, false, box v)
            else
                this.WriteExceptCustom (v, running)
        else
            this.WriteExceptSlow (v, running)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.WriteExceptCustom(v: 'T, running: IComputation) =
        if not (equal.Equals (value, v)) then
            value <- v
            Tracer.Write (observers, true, box v)

            observers.NotifyDirtyOther running

            Tracer.Notified observers
            graph.RequestFlushFromEntered ()
        else
            Tracer.Write (observers, false, box v)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.WriteExceptSlow(v: 'T, running: IComputation) =
        if not (graph.Enter "A signal write") then
            if not (ComparerSentinel.equals equal value v) then
                this.WriteExceptChanged (v, running)
            else
                Tracer.Write (observers, false, box v)
        else
            this.WriteExceptAfterAcquire (v, running)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.WriteExceptChanged(v: 'T, running: IComputation) =
        value <- v
        Tracer.Write (observers, true, box v)

        observers.NotifyDirtyOther running

        Tracer.Notified observers
        graph.RequestFlushFromEntered ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.WriteExceptAfterAcquire(v: 'T, running: IComputation) =
        try
            if not (ComparerSentinel.equals equal value v) then
                this.WriteExceptChanged (v, running)
            else
                Tracer.Write (observers, false, box v)
        finally
            graph.Release ()
#endif

    // Out of line, so the setter's body stays at one field load and a null check when the graph has no journal.
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.RecordWrite(v: 'T) : bool =
        graph.RecordInput (JournalEntryKind.Write, id, box v)

    /// <summary>
    /// Writes <c>v</c> as the setter does, for a caller that has already entered the graph and holds a batch open:
    /// the journal record, cutoff, trace and observer marks, without the entry check or the flush request, which
    /// the enclosing batch's exit makes.
    /// </summary>
    member internal this.SetFromEnteredBatch(v: 'T) =
        if Platform.isNull graph.Journal || this.RecordWrite v then
            if not (ComparerSentinel.equals equal value v) then
                value <- v
                Tracer.Write (observers, true, box v)
                observers.NotifyDirty ()
                Tracer.Notified observers
            else
                Tracer.Write (observers, false, box v)

    /// <summary>Marks every reader for a check, leaving the value unchanged.</summary>
    member internal _.NotifyCheck() =
        observers.NotifyCheck ()

    /// <summary>
    /// Non-throwing, tracked read.
    /// </summary>
#if FABLE_COMPILER
    member this.TryValue: Reading<'T> =
        graph.Track (this :> ISource)
        Ready value
#else
    member this.TryValue
        with [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>] get () : Reading<'T> = this.GetTryFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetTryFast() : Reading<'T> =
        graph.Track (this :> ISource)
        Ready value
#endif

    /// <summary>
    /// Untracked read.
    /// </summary>
    member _.Peek = value

    member internal _.Graph = graph

    /// <summary>
    /// How many computations currently read this signal. Exposed for tests
    /// that need to assert an edge was dropped, or not duplicated.
    /// </summary>
    member internal _.ObserverCount =
        observers.Count

/// <summary>
/// A source whose value arrives later.
/// </summary>
/// <remarks>
/// This is the primitive <c>Memo.createAsync</c> is built on, and the smallest thing
/// that can actually be pending. Reading it before it settles suspends the
/// reader.
/// </remarks>
type AsyncSource<'T>(graph: Graph) =
    let id = graph.NextId ()
#if !RANVIER_TRACE
    /// <summary>The source's readers; null until the first reader arrives.</summary>
    let mutable observers: ObserverSet = Unchecked.defaultof<_>
#else
    let observers = ObserverSet ()
    do Tracer.Bind (observers, graph, id)
#endif
    let mutable value = Unchecked.defaultof<'T>
    let mutable failure: Failure = null

    let mutable status = Status.Pending ||| Status.Uninitialized

    do Tracer.AsyncSourceNew (graph, id, observers)

#if RANVIER_TRACE
    interface ITracedNode with
        member _.TraceGraph = box graph
#endif

    interface INode with
        member _.Id = id
        member _.Status = status

    interface ISource with
        member _.AddObserver(c, slot) =
#if !RANVIER_TRACE
            if Platform.isNull (box observers) then
                observers <- ObserverSet ()
#endif
            observers.Add (c, slot)

        member _.TryAddObserver(c, slot) =
#if !RANVIER_TRACE
            if Platform.isNull (box observers) then
                observers <- ObserverSet ()
#endif
            observers.TryAdd (c, slot)

        member _.RemoveObserverAt(c, handle) =
            observers.RemoveAt (c, handle)

        /// <summary>
        /// Nothing to bring current: the value is whatever was last written.
        /// </summary>
        member _.UpdateIfNecessary() = ()

        member _.IsStale = false

        member _.IsActive = false

    member _.Status = status

    member private this.SettleOnGraph(v: 'T) =
        if
            not (
                not (Platform.isNull graph.Journal)
                && not (this.RecordSettle v)
            )
        then
            value <- v
            status <- Status.None
            Tracer.SourceSettled (observers, false, box v)

            if not (Platform.isNull (box observers)) then
                observers.NotifyDirty ()

            Tracer.Notified observers

            graph.RequestFlush ()

    member private this.FailOnGraph(reason: exn) =
        if
            not (
                not (Platform.isNull graph.Journal)
                && not (this.RecordFail reason)
            )
        then
            failure <- Failure (reason, (this :> INode), true)
            status <- Status.Error
            Tracer.SourceSettled (observers, true, reason)

            if not (Platform.isNull (box observers)) then
                observers.NotifyDirty ()

            Tracer.Notified observers

            graph.RequestFlush ()

    /// <summary>
    /// Publishes a value and wakes every dependent.
    /// </summary>
    /// <remarks>
    /// Marshalled through <c>Dispatch</c>, because this is the one entry point that
    /// is genuinely called from off-thread: it is what a completing <c>Task</c> runs.
    /// </remarks>
    member this.Settle(v: 'T) =
#if FABLE_COMPILER
        this.SettleOnGraph v
#else
        if graph.IsOnGraphThread then
            this.SettleOnGraph v
        else
            graph.PostQueued (fun () -> this.SettleOnGraph v)
#endif

    /// <summary>
    /// Publishes a failure as a settled outcome: dependents suspended on the source read <c>reason</c> as its error.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>reason</c> is null.</exception>
    member this.Fail(reason: exn) =
        if Platform.isNull reason then
            raise (ArgumentNullException (nameof reason))

#if FABLE_COMPILER
        this.FailOnGraph reason
#else
        if graph.IsOnGraphThread then
            this.FailOnGraph reason
        else
            graph.PostQueued (fun () -> this.FailOnGraph reason)
#endif

    // Out of line, so the settle and fail closures stay at one field load and a null check when the graph has no journal.
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.RecordSettle(v: 'T) : bool =
        graph.RecordInput (JournalEntryKind.Settle, id, box v)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.RecordFail(reason: exn) : bool =
        graph.RecordInput (JournalEntryKind.Fail, id, reason)

    /// <summary>
    /// Transparent, tracked read. Links the edge, then suspends if pending.
    /// </summary>
#if FABLE_COMPILER
    member this.Value: 'T =
        graph.Track (this :> ISource)

        if StatusBits.isUnready status then
            if StatusBits.isPending status then
                raise (graph.NotReady (this :> INode))

            graph.Raise failure

        value
#else
    member this.Value
        with [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>] get () : 'T = this.GetFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetFast() : 'T =
        graph.Track (this :> ISource)

        if StatusBits.isUnready status then
            this.RaiseUnready ()
        else
            value

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RaiseUnready() : 'T =
        if StatusBits.isPending status then
            raise (graph.NotReady (this :> INode))

        graph.Raise failure
        value
#endif

    /// <summary>
    /// Non-throwing, tracked read. Links the edge exactly as <c>Value</c> does, so a
    /// <c>Pending</c> result still wakes the reader when the source settles.
    /// </summary>
    member this.TryValue: Reading<'T> =
        graph.Track (this :> ISource)

        if StatusBits.isUnready status then
            if StatusBits.isPending status then
                Pending
            else
                Failed failure.Error
        else
            Ready value

    /// <summary>The source itself while <c>Status</c> has <c>Error</c>, otherwise null.</summary>
    member this.ErrorOrigin: INode =
        if StatusBits.isError status then
            this :> INode
        else
            Unchecked.defaultof<INode>

    member internal _.Graph = graph

/// <summary>
/// A derived, cached computation.
/// </summary>
/// <remarks>
/// <para>
/// On invalidation the body is re-run from the top, never resumed. Resuming
/// — keeping the reads that happened before the suspension point and running
/// only the remainder — pairs a prefix read at T0 with a suffix read at T1 and
/// yields values corresponding to no state the graph was ever in. See
/// <c>DiamondTests</c>, which pins that failure.
/// </para>
/// <para>
/// A memo is pure or owning. A pure memo's body raises
/// <c>InvalidOperationException</c> when it creates an owned node: a memo, effect,
/// async value, boundary, root, projection, lookup or cleanup. An owning
/// memo's body owns what it creates: the nodes and cleanups are disposed
/// before each re-run and with the memo.
/// </para>
/// <para>
/// The body's argument is the value last published, <c>ValueNone</c> before the
/// first. After a run that suspends or fails, the next run receives the same
/// argument. While the memo holds a value, a body that returns the value inside its
/// argument keeps its dependents clean.
/// </para>
/// </remarks>
type Memo<'T>
    private
    (
        graph: Graph,
        computeFn: 'T voption -> 'T,
        computeFunc: Func<'T voption, 'T>,
        seed: 'T,
        seededFunc: Func<'T, 'T>,
        mode: ScopeMode,
        equal: IEqualityComparer<'T>
    ) =
    // Exactly one of computeFn, computeFunc and seededFunc is set: the body is invoked through the delegate it was
    // given, not through a wrapper around it.
    let node =
        NodeOps.Create (graph, NodeKind.Memo, Memo<'T>.RunFn, Freshness.Dirty)

    do Tracer.Bind (node.Observers, graph, node.Id)
    do Tracer.Bind (node.Sources, graph, node.Id)

    let mutable value = Unchecked.defaultof<'T>

    /// <summary>The companion disposed with the memo, or null.</summary>
    let mutable adopted: IDisposable = null

#if RANVIER_COUNTERS
    do Counters.MemoCreated ()
#endif

    /// <summary>Runs a memo's body for its node: the node's <c>RunCallback</c>, shared by every memo of <c>'T</c>.</summary>
#if FABLE_COMPILER
    static member val internal RunFn: Node -> unit = (fun (n: Node) -> (Platform.fastCast<Memo<'T>> (box n.Self)).Run ())
#else
    static member private RunNode(n: Node) =
        (Platform.fastCast<Memo<'T>> (box n.Self)).RunFromNode n

    static member val internal RunFn: Action<Node> = Action<Node>(Memo<'T>.RunNode)
#endif

    /// <summary>
    /// A memo over <c>compute</c>, owned by the current owner.
    /// </summary>
    static member internal Create(graph: Graph, compute: 'T voption -> 'T, mode: ScopeMode) =
        let memo =
            Memo<'T>(graph, compute, null, Unchecked.defaultof<'T>, null, mode, graph.EncodedComparer<'T>())

        memo.Attach ()
        memo

    /// <summary>A memo whose cutoff uses <c>comparer</c>, owned by the current owner.</summary>
    static member internal CreateWithComparer(graph: Graph, compute: 'T voption -> 'T, mode: ScopeMode, comparer: IEqualityComparer<'T>) =
        let memo =
            Memo<'T>(graph, compute, null, Unchecked.defaultof<'T>, null, mode, ComparerSentinel.encode comparer)

        memo.Attach ()
        memo

    static member internal CreateEncoded(graph: Graph, compute: 'T voption -> 'T, mode: ScopeMode, encodedEqual: IEqualityComparer<'T>) =
        let memo =
            Memo<'T>(graph, compute, null, Unchecked.defaultof<'T>, null, mode, encodedEqual)

        memo.Attach ()
        memo

    /// <summary>
    /// Attaches the memo to the current owner. Disposing that owner disposes
    /// the memo.
    /// </summary>
    member internal this.Attach() =
        node.Self <- this
        node.Link <- graph.CurrentOwner.AttachLinked this
        Tracer.MemoNew (graph, node.Id, node.Link.Owner)

    /// <summary>The memo's reactive state.</summary>
    member inline internal _.Node = node

    /// <summary>
    /// Dependencies are re-collected on every run, so the old edges have to go.
    /// Clearing the set alone drops this memo's own record of the edge while
    /// leaving the source's observer entry in place — the source then keeps
    /// waking a memo that no longer reads it, and keeps it alive for as long as
    /// the source lives. A conditional body is where that shows up: the branch
    /// not taken is still a subscription.
    /// </summary>
    member private this.DetachSources() =
        node.Sources.Clear (this :> IComputation)

    /// <summary>Invokes the body with the argument its delegate takes.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private _.Invoke(previous: 'T) : 'T =
        if not (Platform.isNull computeFunc) then
            computeFunc.Invoke (if node.Published then ValueSome previous else ValueNone)
        elif not (Platform.isNull (box computeFn)) then
            computeFn (if node.Published then ValueSome previous else ValueNone)
        else
            seededFunc.Invoke (if node.Published then previous else seed)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.Run() =
        this.RunFromNode node

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.RunFromNode(n: Node) =
#if !FABLE_COMPILER && !RANVIER_TRACE
        if
            n.Published
            && n.Status = Status.None
            && SafeEquality.isSafe equal
        then
            if graph.CanEnterHostedTop () then
                this.RunTopCore n
            elif graph.CanNestInReader then
                this.RunReadyNested n
            else
                this.RunGeneral n
        else
            this.RunGeneral n
#else
        this.RunGeneral n
#endif

#if !FABLE_COMPILER && !RANVIER_TRACE
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.RunTopCore(n: Node) =
        let prevOwner = graph.EnterHostedTop n
        let previous = value
        let result = this.ComputePublished (n, previous)

        if not (Platform.isNull graph.Caught) || graph.RaisedPending || n.Violated then
            this.RunFailedTop (n, prevOwner, this.TakeBodyException ())
        else
            value <- result
            graph.LeaveHostedTop prevOwner
            n.Active <- 0uy

            let trim = n.Disposed || n.Sources.cursor < n.Sources.count

            if trim then
                this.FinishTop (n, prevOwner, previous, true)
            elif
                (n.Observers.Count <> 0 || graph.QueuePending)
                && not (ComparerSentinel.equals equal previous value)
            then
                if NodeOps.TryPromoteTop n then
                    if graph.QueuePending then
                        graph.OweFlush ()

                    if n.Freshness <> Freshness.Clean then
                        this.NoteStale n
                else
                    this.FinishTop (n, prevOwner, previous, false)
            elif n.Freshness <> Freshness.Clean then
                this.NoteStale n

    member private _.NoteStale(n: Node) =
        n.Unannounced <- true
        graph.NoteStaleTransition ()

    member private this.RunReadyNested(n: Node) =
        let parent = graph.CurrentNode
        let previousOwner = graph.EnterHostedTop n
        let previous = value
        let result = this.ComputePublished (n, previous)
        let failed = not (Platform.isNull graph.Caught) || graph.RaisedPending || n.Violated

        if not failed then
            value <- result
            graph.LeaveNestedReader (parent, previousOwner)
            n.Active <- 0uy

            if n.Disposed || n.Sources.cursor < n.Sources.count then
                this.FinishSources ()
        else
            let saved = HostedFrame (Unchecked.defaultof<IComputation>, parent, previousOwner, true, Unchecked.defaultof<INode>)
            this.RunFailed (saved, this.TakeBodyException (), null)

        if failed || not (ComparerSentinel.equals equal previous value) then
            if n.Observers.Count <> 1 || not (obj.ReferenceEquals (n.Observers.FirstNode, parent)) || graph.StaleTransitions <> 0 then
                NodeOps.NotifyMoved n

            if graph.QueuePending then
                graph.OweFlush ()

        if n.Freshness <> Freshness.Clean then
            this.NoteStale n

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.ComputePublished(n: Node, previous: 'T) =
        n.Freshness <- Freshness.Clean
        n.Unannounced <- false
        n.Sources.BeginRun ()
        n.Runs <- n.Runs + 1
#if RANVIER_COUNTERS
        Counters.MemoRecomputed ()
#endif
        n.Active <- 1uy
        this.InvokePublished previous

    member private _.InvokePublished(previous: 'T) =
        try
            if not (Platform.isNull computeFunc) then
                computeFunc.Invoke (ValueSome previous)
            elif not (Platform.isNull (box computeFn)) then
                computeFn (ValueSome previous)
            else
                seededFunc.Invoke previous
        with ex ->
            graph.Caught <- ex
            Unchecked.defaultof<'T>

    member private this.TakeBodyException() =
        let ex = graph.Caught

        if Platform.isNull ex then
            try
                this.FailAfterBody ()
                null
            with ex ->
                ex
        else
            graph.Caught <- null
            ex

    member private this.FinishTop(n: Node, prevOwner: Owner, previous: 'T, trim: bool) =
        try
            if trim then
                this.FinishSources ()

            if not trim || not (ComparerSentinel.equals equal previous value) then
                NodeOps.NotifyMovedTop n

                if graph.QueuePending then
                    graph.OweFlush ()

            if n.Freshness <> Freshness.Clean then
                this.NoteStale n
        with ex ->
            this.RunFailedTop (n, prevOwner, ex)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RunFailedTop(n: Node, prevOwner: Owner, ex: exn) =
        graph.LeaveHostedTopFailed prevOwner
        n.Active <- 0uy

        if n.Disposed then
            n.Sources.Clear (this :> IComputation)
        else
            n.Sources.EndRun (this :> IComputation)

        if n.Violated then
            n.Violated <- false
            n.Failure <- graph.FailureOf (ScopeMessages.failure mode ex, this, null)
            n.Status <- Status.Error
        else
            match ex with
            | NotReadyException source ->
                n.PendingSource <- source
                n.Status <- Status.Pending
            | _ ->
                n.Failure <- graph.FailureOf (ex, this, null)
                n.Status <- Status.Error

        NodeOps.NotifyMoved n

        if graph.QueuePending then
            graph.OweFlush ()

        if n.Freshness <> Freshness.Clean then
            n.Unannounced <- true
            graph.NoteStaleTransition ()
#endif

    member private this.RunGeneral(n: Node) =
        // Marked clean *before* the body runs, not after.
        n.Freshness <- Freshness.Clean
        n.Unannounced <- false
        n.Sources.BeginRun ()

        if not (Platform.isNull (box n.PendingSource)) then
            n.PendingSource <- Unchecked.defaultof<INode>

        let previous = value
        let previousStatus = n.Status
        let previousFailure = n.Failure

        n.Status <- Status.None
        n.Failure <- null
        n.Runs <- n.Runs + 1
#if RANVIER_COUNTERS
        Counters.MemoRecomputed ()
#endif
        Tracer.RunStart (graph, n.Id, n.Runs)
        n.Active <- 1uy

#if FABLE_COMPILER
        try
            try
                let prev = if n.Published then ValueSome previous else ValueNone
                let result = graph.RunHosted (this :> IComputation, computeFn, prev)

                if n.Violated then
                    raise (InvalidOperationException (ScopeMessages.forMode mode))

                value <- result
                n.Published <- true
            finally
                n.Active <- 0uy

                if n.Disposed then
                    n.Sources.Clear (this :> IComputation)
                else
                    n.Sources.EndRun (this :> IComputation)
        with
        | ex when n.Violated ->
            n.Violated <- false
            n.Failure <- graph.FailureOf (ScopeMessages.failure mode ex, this, previousFailure)
            n.Status <- Status.Error
        | NotReadyException source ->
            n.PendingSource <- source
            n.Status <- Status.Pending
        | ex ->
            n.Failure <- graph.FailureOf (ex, this, previousFailure)
            n.Status <- Status.Error
#else
        let saved = graph.EnterHostedNode n
        let ex = this.TryCompute previous

        if not (Platform.isNull ex) then
            this.RunFailed (saved, ex, previousFailure)
        else
            n.Published <- true
            graph.LeaveHosted saved
            n.Active <- 0uy

            if n.Disposed || n.Sources.Cursor < n.Sources.Count then
                this.FinishSources ()
#endif

        this.Publish (previous, previousStatus, previousFailure)

#if !FABLE_COMPILER
    member private this.TryCompute(previous: 'T) : exn =
        try
            let result = this.Invoke previous

            if graph.RaisedPending || node.Violated then
                this.FailAfterBody ()

            value <- result
            null
        with ex ->
            ex

    /// <summary>
    /// Raises for a body that returned after a pending read raised inside it, or after it created an owned node
    /// in a pure memo.
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.FailAfterBody() =
        graph.CheckRaised ()

        if node.Violated then
            raise (InvalidOperationException (ScopeMessages.forMode mode))

    /// <summary>Trims the reads the run did not repeat, or drops every edge of a memo disposed by its own body.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.FinishSources() =
        if node.Disposed then
            node.Sources.Clear (this :> IComputation)
        else
            node.Sources.EndRun (this :> IComputation)

    /// <summary>Unwinds a run whose body or post-checks threw, then records the outcome.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RunFailed(saved: HostedFrame, ex: exn, previousFailure: Failure) =
        let n = node
        graph.LeaveHosted saved
        n.Active <- 0uy

        if n.Disposed then
            n.Sources.Clear (this :> IComputation)
        else
            n.Sources.EndRun (this :> IComputation)

        if n.Violated then
            n.Violated <- false
            n.Failure <- graph.FailureOf (ScopeMessages.failure mode ex, this, previousFailure)
            n.Status <- Status.Error
        else
            match ex with
            | NotReadyException source ->
                n.PendingSource <- source
                n.Status <- Status.Pending
            | _ ->
                n.Failure <- graph.FailureOf (ex, this, previousFailure)
                n.Status <- Status.Error
#endif

    /// <summary>
    /// The cutoff, and the whole point of <c>Check</c>. Status counts as part of the published value: a memo that goes
    /// from a value to pending has changed what its dependents see even when <c>value</c> is untouched, and so has a
    /// memo that fails with a different exception. A throwing comparer fails the run as a throwing body does.
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.Publish(previous: 'T, previousStatus: Status, previousFailure: Failure) =
        let n = node

        let mutable moved =
            n.Status <> previousStatus
            || (not (obj.ReferenceEquals (n.Failure, previousFailure))
                && Failure.Moved (n.Failure, previousFailure))

        if not moved then
            moved <-
                if SafeEquality.isSafe equal then
                    not (ComparerSentinel.equals equal previous value)
                else
                    this.CompareGuarded (previous, previousFailure)

        if moved then
            Tracer.Moved (graph, n.Id, Failure.Payload (n.Failure, box value))
            NodeOps.NotifyMoved n
            Tracer.Notified graph
            Tracer.RunEnd (graph, n.Id, n.Status)

            if graph.QueuePending then
                graph.OweFlush ()
        else
            Tracer.RunEnd (graph, n.Id, n.Status)

        if n.Freshness <> Freshness.Clean then
            n.Unannounced <- true
            graph.NoteStaleTransition ()

    /// <summary>Compares through a comparer that may raise: a raise restores the previous value and fails the run.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CompareGuarded(previous: 'T, previousFailure: Failure) : bool =
        try
            not (ComparerSentinel.equals equal previous value)
        with ex ->
            value <- previous
            node.Failure <- graph.FailureOf (ex, this, previousFailure)
            node.Status <- Status.Error
            true

    member internal this.EnsureCurrent() = NodeOps.EnsureCurrent node

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.SettlePullAndRead() : 'T =
        let currentStatus = node.Status
        let currentFailure = node.Failure
        let currentValue = value
        graph.FinishPullSettle ()
        graph.Track (this :> ISource)

        if node.Freshness <> Freshness.Clean && not node.Disposed then
            graph.MarkReaderCheck ()

        if StatusBits.isUnready currentStatus then
            if StatusBits.isPending currentStatus then
                raise (graph.NotReady (this :> INode))

            graph.Raise currentFailure

        currentValue

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.SettlePullAndReadTry() : Reading<'T> =
        let currentStatus = node.Status
        let currentFailure = node.Failure
        let currentValue = value
        graph.FinishPullSettle ()
        graph.Track (this :> ISource)

        if node.Freshness <> Freshness.Clean && not node.Disposed then
            graph.MarkReaderCheck ()

        if StatusBits.isUnready currentStatus then
            if StatusBits.isPending currentStatus then
                Pending
            else
                Failed currentFailure.Error
        else
            Ready currentValue

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.SettlePullAndReadUntracked() : Reading<'T> =
        let currentStatus = node.Status
        let currentFailure = node.Failure
        let currentValue = value
        graph.FinishPullSettle ()

        if StatusBits.isUnready currentStatus then
            if StatusBits.isPending currentStatus then
                Pending
            else
                Failed currentFailure.Error
        else
            Ready currentValue

    /// <summary>
    /// Detaches from every source. Idempotent, and usually called for you by
    /// the enclosing owner.
    /// </summary>
    /// <remarks>
    /// A disposed memo answers reads with its last computed outcome, or the
    /// type's default when it has never run. It stops tracking and stops being
    /// woken, so the sources it read no longer retain or invalidate it.
    /// </remarks>
    member this.Dispose() =
        graph.Entered (
            "Disposing a node",
            fun () ->
                if not node.Disposed then
                    node.Disposed <- true
                    Tracer.NodeDispose (graph, node.Id)

                    if not (Platform.isNull node.Link) then
                        node.Link.Detach ()
                        node.Link <- null

                    this.DetachSources ()

                    if not (Platform.isNull (box node.Scope)) then
                        graph.Retire node.Scope

                    if not (Platform.isNull adopted) then
                        let companion = adopted
                        adopted <- null
                        companion.Dispose ()
        )

    /// <summary>
    /// Marks every reader dirty. A reader of a row whose key was removed
    /// re-runs and reads the key as absent.
    /// </summary>
    member internal _.NotifyRemoved() =
        node.Observers.NotifyDirty ()

    /// <summary>
    /// Whether the run in progress has created an owned node in a pure body. A violated run fails and keeps the previous
    /// value.
    /// </summary>
    member internal _.Violated = node.Violated

    /// <summary>
    /// Disposes <c>companion</c> when the memo is disposed, or at once when it already is. A replaced companion is
    /// disposed at once.
    /// </summary>
    member internal _.Adopt(companion: IDisposable) =
        if node.Disposed then
            companion.Dispose ()
        else
            let replaced = adopted
            adopted <- companion

            if
                not (Platform.isNull replaced)
                && not (obj.ReferenceEquals (replaced, companion))
            then
                replaced.Dispose ()


    interface IOwned with
        member this.Release() =
            node.Link <- null
            this.Dispose ()

    interface IScopeHost with
        member _.CanOwn =
            not (Platform.isNull (box node.Scope))
            || mode = ScopeMode.Owning

        member _.Scope =
            if Platform.isNull (box node.Scope) then
                if mode = ScopeMode.Owning then
                    node.Scope <- new Owner (graph.Root)
                    Tracer.ScopeNew (node.Scope, graph, node.Id)

                    if node.Disposed then
                        node.Scope.Dispose ()
                else
                    node.Violated <- true
                    raise (InvalidOperationException (ScopeMessages.forMode mode))

            node.Scope

#if RANVIER_TRACE
    interface ITracedNode with
        member _.TraceGraph = box graph
#endif

    interface INode with
        member _.Id = node.Id
        member _.Status = node.Status ||| Staleness.ofFreshness node.Freshness

    interface ISource with
        // Each names the memo's node for the source list linking the edge; see EdgeContext.
        member this.AddObserver(c, slot) =
            EdgeContext.NameSource (this, node)
            node.Observers.Add (c, slot)

        member this.TryAddObserver(c, slot) =
            EdgeContext.NameSource (this, node)
            node.Observers.TryAdd (c, slot)

        member _.RemoveObserverAt(c, handle) =
            node.Observers.RemoveAt (c, handle)

        member _.UpdateIfNecessary() =
            NodeOps.EnsureCurrent node

        member _.IsStale = node.Freshness <> Freshness.Clean && not node.Disposed

        member _.IsActive = node.Active <> 0uy

    interface IComputation with
        member _.AddSource s = NodeOps.AddSource (node, s)

        member _.MovedIn(slot, handle) =
            node.Sources.MovedIn (slot, handle)

        member _.HasReadSlot slot =
            slot < node.Sources.Cursor

        member _.MarkDirty() = NodeOps.MemoMarkDirty node

        member _.MarkCheck() = NodeOps.MemoMarkCheck node

    member _.Status = node.Status

    /// <summary>
    /// The source of the last run's last pending read, or empty when the run did not suspend.
    /// </summary>
    /// <remarks>
    /// A body that catches several pending reads lists only the last; a body that does not catch one stops at it.
    /// </remarks>
    member internal _.PendingSource = node.PendingSource

    member _.PendingSources =
        if Platform.isNull (box node.PendingSource) then
            Seq.empty
        else
            [| node.PendingSource |] :> seq<INode>

    /// <summary>
    /// How many times the body has executed. Exposed for tests that need to
    /// distinguish a genuine cache hit from a silent re-run.
    /// </summary>
    member _.Runs = node.Runs

    /// <summary>
    /// Transparent, tracked read. Suspends the caller if this memo is itself
    /// suspended, which is how the pending channel propagates.
    /// </summary>
#if FABLE_COMPILER
    member this.Value: 'T =
        if StatusBits.isCleanAndReady node.Freshness node.Status then
            graph.Track (this :> ISource)
            value
        else
            this.ReadSlow ()
#else
    member this.Value
        with [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>] get () : 'T = this.GetFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetFast() : 'T =
        let n = node

        if StatusBits.isCleanAndReady n.Freshness n.Status then
            graph.Track (this :> ISource)
            value
        else
            this.ReadSlow ()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ReadSlow() : 'T =
        let n = node
#if !FABLE_COMPILER && !RANVIER_TRACE
        if
            n.Active = 0uy
            && not n.Disposed
            && Platform.isNull (box n.Scope)
            && n.Published
            && n.Status = Status.None
            && SafeEquality.isSafe equal
        then
            if graph.IsIdleForMarks () && graph.CanEnterHostedTop () && graph.TryEnterPullFast () then
                if n.Freshness = Freshness.Dirty then
                    this.RunTopCore n
                else
                    this.RefreshIdleCheck n

                if graph.ExitPullFast () || n.Freshness <> Freshness.Clean then
                    this.SettlePullAndRead ()
                elif StatusBits.isUnready n.Status then
                    this.RaiseUnready n
                else
                    value
            elif n.Freshness = Freshness.Dirty && graph.CanNestInReader then
                this.RunReadyNested n

                if n.Freshness <> Freshness.Clean then
                    this.SettlePullAndRead ()
                else
                    graph.Track (this :> ISource)

                    if StatusBits.isUnready n.Status then this.RaiseUnready n else value
            else
                this.ReadSlowGeneral n
        else
            this.ReadSlowGeneral n

    member private this.RefreshIdleCheck(n: Node) =
        try
            NodeOps.Refresh n
        with _ ->
            graph.ExitPullFastOnException ()
            reraise ()

    member private this.RaiseUnready(n: Node) : 'T =
        if StatusBits.isPending n.Status then
            raise (graph.NotReady (this :> INode))

        graph.Raise n.Failure
        value

    member private this.ReadSlowGeneral(n: Node) : 'T =
#endif
        if NodeOps.RefreshForRead n then
            this.SettlePullAndRead ()
        else
            graph.Track (this :> ISource)

            if StatusBits.isUnready n.Status then
                if StatusBits.isPending n.Status then
                    raise (graph.NotReady (this :> INode))

                graph.Raise n.Failure

            value

    /// <summary>
    /// Non-throwing, tracked read.
    /// </summary>
#if FABLE_COMPILER
    member this.TryValue: Reading<'T> =
        if StatusBits.isCleanAndReady node.Freshness node.Status then
            graph.Track (this :> ISource)
            Ready value
        else
            this.ReadTrySlow ()
#else
    member this.TryValue
        with [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>] get () : Reading<'T> = this.GetTryFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetTryFast() : Reading<'T> =
        if StatusBits.isCleanAndReady node.Freshness node.Status then
            graph.Track (this :> ISource)
            Ready value
        else
            this.ReadTrySlow ()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ReadTrySlow() : Reading<'T> =
        if NodeOps.RefreshForRead node then
            this.SettlePullAndReadTry ()
        else
            graph.Track (this :> ISource)

            if StatusBits.isUnready node.Status then
                if StatusBits.isPending node.Status then
                    Pending
                else
                    Failed node.Failure.Error
            else
                Ready value

    static member val private ReadUntrackedFn: Memo<'T> -> Reading<'T> = fun m -> m.ReadUntracked ()

    member inline internal _.IsCleanAndReady = StatusBits.isCleanAndReady node.Freshness node.Status

    member inline internal _.IsStaleNode = node.Freshness <> Freshness.Clean && not node.Disposed

    member inline internal _.IsActiveNode = node.Active <> 0uy

    /// <summary>
    /// Zero-closure untracked non-throwing read: hits a clean inline check before falling back to <c>ReadUntracked</c>.
    /// </summary>
    member internal this.TryValueUntracked() : Reading<'T> =
        graph.Entered (
            "An untracked read",
            fun () ->
                if StatusBits.isCleanAndReady node.Freshness node.Status then
                    Ready value
                else
                    this.TryValueUntrackedSlow ()
        )

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.TryValueUntrackedSlow() : Reading<'T> =
        if obj.ReferenceEquals (graph.CurrentComputation, null) then
            this.ReadUntracked ()
        else
            graph.RunUntrackedWith (Memo<'T>.ReadUntrackedFn, this)

    /// <summary>
    /// Non-throwing read that brings the memo up to date without recording a dependency.
    /// </summary>
    member internal this.ReadUntracked() : Reading<'T> =
        if NodeOps.RefreshForUntrackedRead node then
            this.SettlePullAndReadUntracked ()
        elif StatusBits.isUnready node.Status then
            if StatusBits.isPending node.Status then
                Pending
            else
                Failed node.Failure.Error
        else
            Ready value

    /// <summary>
    /// Brings the memo up to date without recording a dependency, returning its <see cref="Status"/> with
    /// <c>Status.Stale</c> set when the memo remains live and stale after the refresh.
    /// </summary>
    member internal this.EnsureCurrentUntracked() : Status =
        if NodeOps.RefreshForUntrackedRead node then
            let currentStatus = node.Status
            graph.FinishPullSettle ()

            if node.Freshness <> Freshness.Clean && not node.Disposed then
                currentStatus ||| Status.Stale
            else
                currentStatus
        elif node.Freshness <> Freshness.Clean && not node.Disposed then
            node.Status ||| Status.Stale
        else
            node.Status

    /// <summary>
    /// The node the current failure originated in while <c>Status</c> has <c>Error</c>, otherwise null. Untracked.
    /// </summary>
    /// <remarks>
    /// The memo itself when its body, its comparer or its purity check raised the exception; the upstream node when the
    /// body rethrew the exception of a failed read.
    /// </remarks>
    member _.ErrorOrigin: INode = Failure.OriginOf node.Failure

    /// <summary>The current failure, or null.</summary>
    member internal _.Failure = node.Failure

    /// <summary>
    /// Untracked read of the cached value, without recomputing.
    /// </summary>
    member _.Peek = value

    /// <summary>Whether the memo has published a value at least once. Untracked.</summary>
    member internal _.Published = node.Published

    /// <summary>
    /// How many computations currently read this memo. Exposed for tests that
    /// need to assert an edge was dropped, or not duplicated.
    /// </summary>
    member internal _.ObserverCount = node.Observers.Count

    /// <summary>
    /// How many sources this memo read on its last run. Exposed for the same
    /// reason: a dependency list that grows by one entry per run changes
    /// nothing the graph computes, so only a count can catch it.
    /// </summary>
    member internal _.SourceCount = node.Sources.Count

#if !FABLE_COMPILER
    static member val private ReadUntrackedStructFn: Memo<'T> -> struct (Status * 'T * exn) = fun m -> m.ReadUntrackedStructCore ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.TryReadStruct() : struct (Status * 'T * exn) =
        if StatusBits.isCleanAndReady node.Freshness node.Status then
            graph.Track (this :> ISource)
            struct (Status.None, value, null)
        else
            this.TryReadStructSlow ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.TryReadStructSlow() : struct (Status * 'T * exn) =
        if NodeOps.RefreshForRead node then
            let currentStatus = node.Status
            let currentFailure = node.Failure
            let currentValue = value
            graph.FinishPullSettle ()
            graph.Track (this :> ISource)

            if node.Freshness <> Freshness.Clean && not node.Disposed then
                graph.MarkReaderCheck ()

            if StatusBits.isUnready currentStatus then
                if StatusBits.isPending currentStatus then
                    struct (Status.Pending, Unchecked.defaultof<'T>, null)
                else
                    struct (Status.Error, Unchecked.defaultof<'T>, currentFailure.Error)
            else
                struct (Status.None, currentValue, null)
        else
            graph.Track (this :> ISource)

            if StatusBits.isUnready node.Status then
                if StatusBits.isPending node.Status then
                    struct (Status.Pending, Unchecked.defaultof<'T>, null)
                else
                    struct (Status.Error, Unchecked.defaultof<'T>, node.Failure.Error)
            else
                struct (Status.None, value, null)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.TryReadUntrackedStruct() : struct (Status * 'T * exn) =
        if
            not (graph.IsTracking ())
            && StatusBits.isCleanAndReady node.Freshness node.Status
        then
            struct (Status.None, value, null)
        else
            this.TryReadUntrackedStructSlow ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.TryReadUntrackedStructSlow() : struct (Status * 'T * exn) =
        if not (graph.IsTracking ()) then
            this.ReadUntrackedStructCore ()
        else
            graph.RunUntrackedWith (Memo<'T>.ReadUntrackedStructFn, this)

    member private this.ReadUntrackedStructCore() : struct (Status * 'T * exn) =
        if NodeOps.RefreshForUntrackedRead node then
            let currentStatus = node.Status
            let currentFailure = node.Failure
            let currentValue = value
            graph.FinishPullSettle ()

            if StatusBits.isUnready currentStatus then
                if StatusBits.isPending currentStatus then
                    struct (Status.Pending, Unchecked.defaultof<'T>, null)
                else
                    struct (Status.Error, Unchecked.defaultof<'T>, currentFailure.Error)
            else
                struct (Status.None, currentValue, null)
        elif StatusBits.isUnready node.Status then
            if StatusBits.isPending node.Status then
                struct (Status.Pending, Unchecked.defaultof<'T>, null)
            else
                struct (Status.Error, Unchecked.defaultof<'T>, node.Failure.Error)
        else
            struct (Status.None, value, null)
#endif

// The public constructors live in an intrinsic augmentation: a constructor that calls back into the object it built
// needs a self-identifier, and on the type's own declaration that would put an initialisation check on every member.
// Fable cannot translate a secondary constructor with a self-identifier; under Fable the `Api` functions construct
// this type through `Create`.
#if !FABLE_COMPILER
type Memo<'T> with
    /// <summary>
    /// A pure memo over <c>compute</c>.
    /// </summary>
    new(graph: Graph, compute: Func<'T voption, 'T>) as this =
        Memo<'T>(graph, Unchecked.defaultof<'T voption -> 'T>, compute, Unchecked.defaultof<'T>, null, ScopeMode.Pure, graph.EncodedComparer<'T>())
        then this.Attach ()

    /// <summary>
    /// An owning memo over <c>compute</c> when <c>owning</c> is true, a pure one
    /// otherwise.
    /// </summary>
    new(graph: Graph, compute: Func<'T voption, 'T>, owning: bool) as this =
        Memo<'T>(
            graph,
            Unchecked.defaultof<'T voption -> 'T>,
            compute,
            Unchecked.defaultof<'T>,
            null,
            (if owning then ScopeMode.Owning else ScopeMode.Pure),
            graph.EncodedComparer<'T>()
        )
        then this.Attach ()

    /// <summary>
    /// A pure memo over <c>compute</c>, which receives the value last published, or <c>seed</c> before the first.
    /// </summary>
    new(graph: Graph, seed: 'T, compute: Func<'T, 'T>) as this =
        Memo<'T>(graph, Unchecked.defaultof<'T voption -> 'T>, null, seed, compute, ScopeMode.Pure, graph.EncodedComparer<'T>())
        then this.Attach ()

    /// <summary>
    /// A memo over <c>compute</c>, which receives the value last published, or <c>seed</c> before the first. Owning
    /// when <c>owning</c> is true, pure otherwise.
    /// </summary>
    new(graph: Graph, seed: 'T, compute: Func<'T, 'T>, owning: bool) as this =
        Memo<'T>(
            graph,
            Unchecked.defaultof<'T voption -> 'T>,
            null,
            seed,
            compute,
            (if owning then ScopeMode.Owning else ScopeMode.Pure),
            graph.EncodedComparer<'T>()
        )
        then this.Attach ()
#endif

/// <summary>
/// A side effect that re-runs when its dependencies change.
/// </summary>
/// <remarks>
/// <para>
/// The push half of the graph: memos are pulled by whoever reads them, but
/// nothing reads an effect, so the scheduler runs it. Invalidation only queues
/// it — the body runs on the next flush, which is the end of the current write
/// or batch.
/// </para>
/// <para>
/// A suspended effect does not run its side effect. That is the point of the
/// pending channel here: a body that would have fired a request, written to a
/// DOM node, or logged a line is aborted before it does so, and re-runs from
/// the top once the source it waited on settles.
/// </para>
/// </remarks>
// Fable cannot translate a secondary constructor with a self-identifier, so
// under Fable the primary constructor starts the effect.
#if FABLE_COMPILER
type Effect(graph: Graph, bodyFn: unit -> unit) as this =
    let bodyAction: Action = null
#else
type Effect private (graph: Graph, bodyFn: unit -> unit, bodyAction: Action) =
#endif
    // Exactly one of bodyFn and bodyAction is set, typed at construction rather than tested on every run.
    let node =
        NodeOps.Create (graph, NodeKind.Effect, Effect.RunFn, Freshness.Clean)

    do Tracer.Bind (node.Sources, graph, node.Id)

#if RANVIER_COUNTERS
    do Counters.EffectCreated ()
#endif

#if FABLE_COMPILER
    do this.Start ()
#endif

    /// <summary>Runs an effect's body for its node: the node's <c>RunCallback</c>.</summary>
#if FABLE_COMPILER
    static member val internal RunFn: Node -> unit = (fun (n: Node) -> (Platform.fastCast<Effect> (box n.Self)).Run ())
#else
    static member private RunNode(n: Node) =
        (Platform.fastCast<Effect> (box n.Self)).RunFromNode n

    static member val internal RunFn: Action<Node> = Action<Node>(Effect.RunNode)
#endif

    /// <summary>
    /// An effect running <c>body</c>, owned by the current owner and queued for
    /// its first run.
    /// </summary>
    static member internal Create(graph: Graph, body: unit -> unit) =
#if FABLE_COMPILER
        new Effect (graph, body)
#else
        let effect = new Effect (graph, body, null)
        effect.Start ()
        effect
#endif

    /// <summary>The effect's reactive state.</summary>
    member inline internal _.Node = node

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private _.InvokeBody() =
        if not (Platform.isNull (box bodyFn)) then
            bodyFn ()
        else
            bodyAction.Invoke ()

    member internal this.Start() =
        // Attaching to the enclosing scope is what makes disposal structural:
        // the caller tears down one owner, not a list of effects it had to
        // remember to collect.
        node.Self <- this
        node.Link <- graph.CurrentOwner.AttachLinked this
        Tracer.EffectNew (graph, node.Id, node.Link.Owner)
        NodeOps.EffectMarkDirty node
        graph.RequestFlush ()

    /// <summary>
    /// Dependencies are re-collected on every run, so the old edges have to go
    /// or a source dropped by a conditional body would keep waking it.
    /// </summary>
    member private this.DetachSources() =
        node.Sources.Clear (this :> IComputation)


    interface IOwned with
        member this.Release() =
            node.Link <- null
            this.Dispose ()

#if RANVIER_TRACE
    interface ITracedNode with
        member _.TraceGraph = box graph
#endif

    interface INode with
        member _.Id = node.Id
        member _.Status = node.Status ||| Staleness.ofFreshness node.Freshness

    interface IComputation with
        member _.AddSource s = NodeOps.AddSource (node, s)

        member _.MovedIn(slot, handle) =
            node.Sources.MovedIn (slot, handle)

        member _.HasReadSlot slot =
            slot < node.Sources.Cursor

        member _.MarkDirty() = NodeOps.EffectMarkDirty node

        member _.MarkCheck() = NodeOps.EffectMarkCheck node

    /// <summary>
    /// Set on an effect whose readers are marked only when its body writes, such as a lookup's refresh. Every run
    /// queued after the flag is set runs with the projection passes, ahead of every other queued effect. The first
    /// run, queued at creation, is on the main queue.
    /// </summary>
    member internal _.Publishes
        with set value = node.Publishes <- value

    interface IScheduled with
        member _.Execute() = NodeOps.Execute node

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.Run() =
        this.RunFromNode node

    member private this.RunFromNode(n: Node) =
#if !FABLE_COMPILER && !RANVIER_TRACE
        if
            Platform.isNull (box n.Scope)
            && not n.Disposed
            && n.Status = Status.None
            && Platform.isNull graph.Journal
            && graph.CanEnterHostedTop ()
        then
            this.RunTopCore n
        else
            this.RunGeneral n
#else
        this.RunGeneral n
#endif

#if !FABLE_COMPILER && !RANVIER_TRACE
    member private this.RunTopCore(n: Node) =
        let prevOwner = graph.EnterHostedTop n
        n.Sources.BeginRun ()
        n.Runs <- n.Runs + 1
#if RANVIER_COUNTERS
        Counters.EffectRan ()
#endif
        let ex = this.TryInvokeBody ()

        if Platform.isNull ex then
            graph.LeaveHostedTop prevOwner

            if n.Disposed || n.Sources.cursor < n.Sources.count then
                this.FinishTopSources prevOwner
        else
            this.BodyFailedTop (prevOwner, ex)

    member private this.FinishTopSources(prevOwner: Owner) =
        try
            this.FinishSources ()
        with ex ->
            this.BodyFailedTop (prevOwner, ex)
#endif

    member private this.RunGeneral(n: Node) =
        n.Sources.BeginRun ()

        // Discharge the previous run's cleanups before the next run
        // registers its own. Guarded so a cleanup writing one of this effect's
        // own sources does not re-queue the run it is making way for; see
        // `Node.InCleanup`.
        if not (Platform.isNull (box n.Scope)) then
            this.DischargeScope ()

        // A cleanup that disposes the effect, directly or through its owner,
        // ends the run here.
        if not n.Disposed then
            this.RunBody ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.DischargeScope() =
        node.InCleanup <- true

        try
            graph.Discharge node.Scope
        finally
            node.InCleanup <- false

    member private this.RunBody() =
        let n = node

        if not (Platform.isNull (box n.PendingSource)) then
            n.PendingSource <- Unchecked.defaultof<INode>

        n.Status <- Status.None
        n.Failure <- null
        n.Runs <- n.Runs + 1
#if RANVIER_COUNTERS
        Counters.EffectRan ()
#endif
        Tracer.RunStart (graph, n.Id, n.Runs)

#if FABLE_COMPILER
        try
            try
                if Platform.isNull graph.Journal then
                    graph.RunHosted (this :> IComputation, bodyFn)
                else
                    this.RunBodyJournaled ()
            finally
                if n.Disposed then
                    n.Sources.Clear (this :> IComputation)
                else
                    n.Sources.EndRun (this :> IComputation)
        with ex ->
            this.RecordFailure ex
#else
        if Platform.isNull graph.Journal then
            if graph.CanEnterHostedTop () then
                let prevOwner = graph.EnterHostedTop n
                let ex = this.TryInvokeBody ()

                if not (Platform.isNull ex) then
                    this.BodyFailedTop (prevOwner, ex)
                else
                    graph.LeaveHostedTop prevOwner

                    if n.Disposed || n.Sources.Cursor < n.Sources.Count then
                        this.FinishSources ()
            else
                let saved = graph.EnterHostedNode n
                let ex = this.TryInvokeBody ()

                if not (Platform.isNull ex) then
                    this.BodyFailed (saved, ex)
                else
                    graph.LeaveHosted saved

                    if n.Disposed || n.Sources.Cursor < n.Sources.Count then
                        this.FinishSources ()
        else
            this.RunBodyJournaledGuarded ()
#endif

        Tracer.RunEnd (graph, n.Id, n.Status)

    /// <summary>
    /// Records a run's exception. Never rethrown into the flush loop: one failing effect must not strand every effect
    /// queued behind it. Solid routes this to the nearest error boundary; we have none yet, so it is recorded and
    /// readable.
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RecordFailure(ex: exn) =
        match ex with
        | NotReadyException source ->
            node.PendingSource <- source
            node.Status <- Status.Pending
        | _ ->
            node.Failure <- graph.FailureOf (ex, this, null)
            node.Status <- Status.Error

#if !FABLE_COMPILER
    member private this.TryInvokeBody() : exn =
        try
            this.InvokeBody ()

            if graph.RaisedPending then
                graph.CheckRaised ()

            null
        with ex ->
            ex

    /// <summary>Trims the reads the run did not repeat, or drops every edge of an effect disposed by its own body.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.FinishSources() =
        if node.Disposed then
            node.Sources.Clear (this :> IComputation)
        else
            node.Sources.EndRun (this :> IComputation)

    /// <summary>Unwinds a run whose body threw, then records the outcome.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.BodyFailed(saved: HostedFrame, ex: exn) =
        graph.LeaveHosted saved
        this.FinishSources ()
        this.RecordFailure ex

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.BodyFailedTop(prevOwner: Owner, ex: exn) =
        graph.LeaveHostedTopFailed prevOwner
        this.FinishSources ()
        this.RecordFailure ex

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RunBodyJournaledGuarded() =
        try
            this.RunBodyJournaled ()
        with ex ->
            this.RecordFailure ex
#endif

    // Out of line, so RunBody allocates no closure and, under Fable, no closure context when the graph has no journal.
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RunBodyJournaled() =
#if FABLE_COMPILER
        graph.Journal.RunWork (fun () -> graph.RunHosted (this :> IComputation, bodyFn))
#else
        try
            graph.Journal.RunWork (fun () ->
                let saved = graph.EnterHostedNode node

                try
                    this.InvokeBody ()
                    graph.CheckRaised ()
                finally
                    graph.LeaveHosted saved)
        finally
            if node.Disposed then
                node.Sources.Clear (this :> IComputation)
            else
                node.Sources.EndRun (this :> IComputation)
#endif

    /// <summary>
    /// Detaches from every source and discharges the run scope. Idempotent.
    /// </summary>
    /// <remarks>
    /// Usually called for you: the effect attached itself to the enclosing
    /// owner at construction, so disposing that owner disposes this.
    /// </remarks>
    member this.Dispose() =
        graph.Entered (
            "Disposing a node",
            fun () ->
                if not node.Disposed then
                    node.Disposed <- true
                    Tracer.NodeDispose (graph, node.Id)

                    if not (Platform.isNull node.Link) then
                        node.Link.Detach ()
                        node.Link <- null

                    this.DetachSources ()

                    if not (Platform.isNull (box node.Scope)) then
                        graph.Retire node.Scope
        )

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

    interface IScopeHost with
        member _.CanOwn = true

        member _.Scope =
            if Platform.isNull (box node.Scope) then
                node.Scope <- new Owner (graph.Root)
                Tracer.ScopeNew (node.Scope, graph, node.Id)

                if node.Disposed then
                    node.Scope.Dispose ()

            node.Scope

    member _.Status = node.Status

    /// <summary>
    /// The error from the last run, or null. Effects do not throw out of the
    /// flush loop, so this is the only way to see one.
    /// </summary>
    member _.Error = Failure.ErrorOf node.Failure

    /// <summary>The node the last run's failure originated in, or null when the run did not fail.</summary>
    /// <remarks>
    /// The effect itself when its body raised the exception; the upstream node when the body rethrew the exception of a
    /// failed read.
    /// </remarks>
    member _.ErrorOrigin: INode = Failure.OriginOf node.Failure

    /// <summary>
    /// The source of the last run's last pending read, or empty when the run did not suspend.
    /// </summary>
    /// <remarks>
    /// A body that catches several pending reads lists only the last; a body that does not catch one stops at it.
    /// </remarks>
    member internal _.PendingSource = node.PendingSource

    member _.PendingSources =
        if Platform.isNull (box node.PendingSource) then
            Seq.empty
        else
            [| node.PendingSource |] :> seq<INode>

    /// <summary>
    /// How many times the body has executed.
    /// </summary>
    member _.Runs = node.Runs

// The public constructor lives in an intrinsic augmentation: it starts the effect it built, which needs a
// self-identifier, and on the type's own declaration that would put an initialisation check on every member.
#if !FABLE_COMPILER
type Effect with
    /// <summary>
    /// An effect running <c>body</c>, owned by the current owner and queued for
    /// its first run.
    /// </summary>
    new(graph: Graph, body: Action) as this =
        new Effect (graph, Unchecked.defaultof<unit -> unit>, body)
        then this.Start ()
#endif

/// <summary>
/// A side effect split into a tracked, pure <c>compute</c> and an untracked <c>act</c> that runs with each settled value
/// unequal to the last value acted on. This library's <c>createEffectOn</c>.
/// </summary>
/// <remarks>
/// A pending read or a failure in <c>compute</c> keeps the previous action and its cleanups. Nodes and cleanups
/// created by <c>act</c> belong to a reused scope, discharged before the next <c>act</c> and disposed with the effect.
/// </remarks>
[<Sealed>]
type internal EffectOn<'T> private (graph: Graph, compute: unit -> 'T, act: 'T -> unit, equal: IEqualityComparer<'T>) =
    let node =
        NodeOps.Create (graph, NodeKind.EffectOn, EffectOn<'T>.RunFn, Freshness.Clean)

    do Tracer.Bind (node.Sources, graph, node.Id)

    /// <summary>
    /// The value <c>act</c> last ran with: the cutoff baseline once <c>hasActed</c>.
    /// </summary>
    let mutable last = Unchecked.defaultof<'T>
    let mutable hasActed = false

    /// <summary>
    /// The scope of <c>act</c>'s nodes and cleanups, allocated by the first node or cleanup created in <c>act</c>.
    /// </summary>
    let mutable actionOwner: Owner = Unchecked.defaultof<Owner>

    /// <summary>
    /// True while <c>act</c> runs; selects the owning rule of <c>IScopeHost.Scope</c> over the pure one.
    /// </summary>
    let mutable acting = false
    let mutable error: exn = null

#if RANVIER_COUNTERS
    do Counters.EffectCreated ()
#endif

    /// <summary>Runs a split effect for its node: the node's <c>RunCallback</c>.</summary>
#if FABLE_COMPILER
    static member val internal RunFn: Node -> unit = (fun (n: Node) -> (Platform.fastCast<EffectOn<'T>> (box n.Self)).Run ())
#else
    static member private RunNode(n: Node) =
        (Platform.fastCast<EffectOn<'T>> (box n.Self)).Run ()

    static member val internal RunFn: Action<Node> = Action<Node>(EffectOn<'T>.RunNode)
#endif

    /// <summary>An effect owned by the current owner, with its first run queued.</summary>
    static member internal Create(graph: Graph, compute: unit -> 'T, act: 'T -> unit) =
        let node = new EffectOn<'T> (graph, compute, act, graph.EncodedComparer<'T>())

        node.Start ()
        node

    /// <summary>An owned split effect whose cutoff uses <c>comparer</c>, with its first run queued.</summary>
    static member internal CreateWithComparer(graph: Graph, compute: unit -> 'T, act: 'T -> unit, comparer: IEqualityComparer<'T>) =
        let node = new EffectOn<'T> (graph, compute, act, ComparerSentinel.encode comparer)
        node.Start ()
        node

    /// <summary>The split effect's reactive state.</summary>
    member inline internal _.Node = node

    member private this.Start() =
        node.Self <- this
        node.Link <- graph.CurrentOwner.AttachLinked this
        Tracer.EffectNew (graph, node.Id, node.Link.Owner)
        NodeOps.EffectMarkDirty node
        graph.RequestFlush ()

    /// <summary>Runs <c>compute</c>, then <c>act</c> on the first settled value and on each one unequal to <c>last</c>.</summary>
    member private this.Run() =
#if !FABLE_COMPILER && !RANVIER_TRACE
        if
            hasActed
            && SafeEquality.isSafe equal
            && Platform.isNull (box actionOwner)
            && Platform.isNull graph.Journal
            && not graph.HasUntrackedHost
            && graph.CanEnterHostedTop ()
        then
            this.RunTopCore ()
        else
            this.RunGeneral ()

    member private this.RunTopCore() =
        let n = node
        n.Sources.BeginRun ()
        n.Status <- Status.None
        error <- null
#if RANVIER_COUNTERS
        Counters.EffectRan ()
#endif
        let previousOwner = graph.EnterHostedTop n
        let mutable result = Unchecked.defaultof<'T>
        let ex = this.TryCompute &result

        if not (Platform.isNull ex) then
            this.ComputeFailedTop (previousOwner, ex)
        elif n.Disposed || n.Sources.cursor < n.Sources.count || not (Platform.isNull (box actionOwner)) then
            graph.LeaveHostedTop previousOwner
            this.FinishSources ()

            if not n.Disposed && not (ComparerSentinel.equals equal last result) then
                this.Act result
        elif ComparerSentinel.equals equal last result then
            graph.LeaveHostedTop previousOwner
        else
            last <- result
            acting <- true
            graph.SwitchToActionTop (this :> IScopeHost, previousOwner)
            let ex = this.TryAct result
            graph.LeaveActionTop previousOwner
            acting <- false

            if not (Platform.isNull ex) then
                this.RecordActFailure ex

    member private this.RunGeneral() =
#endif
        let n = node
        n.Sources.BeginRun ()
        n.Status <- Status.None
        error <- null
#if RANVIER_COUNTERS
        Counters.EffectRan ()
#endif
        Tracer.RunStart (graph, n.Id, 0)
        let mutable v = Unchecked.defaultof<'T>
        let mutable settled = false

#if FABLE_COMPILER
        try
            try
                v <- graph.RunHosted (this :> IComputation, compute)

                if n.Violated then
                    raise (InvalidOperationException ScopeMessages.effectOn)

                settled <- true
            finally
                if n.Disposed then
                    n.Sources.Clear (this :> IComputation)
                else
                    n.Sources.EndRun (this :> IComputation)
        with ex ->
            this.RecordComputeFailure ex
#else
        if graph.CanEnterHostedTop () then
            let prevOwner = graph.EnterHostedTop n

            let ex = this.TryCompute &v

            if not (Platform.isNull ex) then
                this.ComputeFailedTop (prevOwner, ex)
            else
                settled <- true
                graph.LeaveHostedTop prevOwner

                if n.Disposed || n.Sources.Cursor < n.Sources.Count then
                    this.FinishSources ()
        else
            settled <- this.ComputeNested &v
#endif

        // A throwing comparer fails the run as a throwing `compute` does.
        let mutable unchanged = false

        if settled && hasActed then
            if SafeEquality.isSafe equal then
                unchanged <- ComparerSentinel.equals equal last v
            else
                match this.CompareGuarded v with
                | 1uy -> unchanged <- true
                | 2uy -> settled <- false
                | _ -> ()

        // RunEnd in both branches keeps the untraced IL equal to the unhooked method (tools/verify-trace.fsx, gate 1).
        if settled && not n.Disposed && not unchanged then
            this.Act v
            Tracer.RunEnd (graph, n.Id, n.Status)
        else
            Tracer.RunEnd (graph, n.Id, n.Status)

    /// <summary>Records <c>compute</c>'s exception.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.RecordComputeFailure(ex: exn) =
        if node.Violated then
            node.Violated <- false
            error <- ScopeMessages.failure ScopeMode.EffectOn ex
            node.Status <- Status.Error
        else
            match ex with
            | NotReadyException _ -> node.Status <- Status.Pending
            | _ ->
                error <- ex
                node.Status <- Status.Error

    /// <summary>Compares through a comparer that may raise: 0 moved, 1 unchanged, 2 raised and failed the run.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.CompareGuarded(v: 'T) : byte =
        try
            if ComparerSentinel.equals equal last v then 1uy else 0uy
        with ex ->
            error <- ex
            node.Status <- Status.Error
            2uy

#if !FABLE_COMPILER
    member private this.ComputeNested(result: byref<'T>) =
        let saved = graph.EnterHostedNode node
        let ex = this.TryCompute &result

        if Platform.isNull ex then
            graph.LeaveHosted saved

            if node.Disposed || node.Sources.Cursor < node.Sources.Count then
                this.FinishSources ()

            true
        else
            this.ComputeFailed (saved, ex)
            false

    member private this.TryCompute(result: byref<'T>) : exn =
        try
            result <- compute ()

            if graph.RaisedPending || node.Violated then
                this.FailAfterCompute ()

            null
        with ex ->
            ex

    /// <summary>Raises for a <c>compute</c> that returned after a pending read raised inside it, or after it created an owned node.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.FailAfterCompute() =
        graph.CheckRaised ()

        if node.Violated then
            raise (InvalidOperationException ScopeMessages.effectOn)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.FinishSources() =
        if node.Disposed then
            node.Sources.Clear (this :> IComputation)
        else
            node.Sources.EndRun (this :> IComputation)

    /// <summary>Unwinds a run whose <c>compute</c> threw, then records the outcome.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ComputeFailed(saved: HostedFrame, ex: exn) =
        graph.LeaveHosted saved
        this.FinishSources ()
        this.RecordComputeFailure ex

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ComputeFailedTop(prevOwner: Owner, ex: exn) =
        graph.LeaveHostedTopFailed prevOwner
        this.FinishSources ()
        this.RecordComputeFailure ex
#endif

    /// <summary>Records <c>act</c>'s exception.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.RecordActFailure(ex: exn) =
        match ex with
        | NotReadyException _ -> node.Status <- Status.Pending
        | _ ->
            error <- ex
            node.Status <- Status.Error

    /// <summary>
    /// Discharges the previous action's scope, then runs <c>act</c> with <c>v</c>. An exception from <c>act</c> is recorded
    /// and leaves <c>v</c> as the cutoff baseline.
    /// </summary>
    member private this.Act(v: 'T) =
        last <- v
        hasActed <- true

        if not (Platform.isNull (box actionOwner)) then
            graph.Discharge actionOwner

        if not node.Disposed then
            acting <- true

#if FABLE_COMPILER
            try
                try
                    if Platform.isNull graph.Journal then
                        graph.RunUntrackedHostedWith (this :> IScopeHost, act, v)
                    else
                        this.ActJournaled v
                finally
                    acting <- false
            with ex ->
                this.RecordActFailure ex
#else
            if Platform.isNull graph.Journal then
                let saved = graph.EnterUntrackedHosted (this :> IScopeHost)
                let ex = this.TryAct v
                graph.LeaveUntrackedHosted saved
                acting <- false

                if not (Platform.isNull ex) then
                    this.RecordActFailure ex
            else
                this.ActJournaledGuarded v
#endif

#if !FABLE_COMPILER
    member private _.TryAct(v: 'T) : exn =
        try
            act v
            null
        with ex ->
            ex

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ActJournaledGuarded(v: 'T) =
        try
            try
                this.ActJournaled v
            finally
                acting <- false
        with ex ->
            this.RecordActFailure ex
#endif

    // Out of line, so Act allocates no closure and, under Fable, no closure context when the graph has no journal.
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ActJournaled(v: 'T) =
        let recorder = graph.Journal

        recorder.RunWork (fun () ->
            recorder.EnterDerived ()

            try
                graph.RunUntrackedHostedWith (this :> IScopeHost, act, v)
            finally
                recorder.LeaveDerived ())

    /// <summary>Detaches from the sources and disposes the action's scope. Idempotent.</summary>
    member this.Dispose() =
        graph.Entered (
            "Disposing a node",
            fun () ->
                if not node.Disposed then
                    node.Disposed <- true
                    Tracer.NodeDispose (graph, node.Id)

                    if not (Platform.isNull node.Link) then
                        node.Link.Detach ()
                        node.Link <- null

                    node.Sources.Clear (this :> IComputation)

                    if not (Platform.isNull (box actionOwner)) then
                        graph.Retire actionOwner
        )

    /// <summary>The error of the last run, from <c>compute</c> or <c>act</c>; null when it did not fail.</summary>
    member _.Error = error

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()


    interface IOwned with
        member this.Release() =
            node.Link <- null
            this.Dispose ()

#if RANVIER_TRACE
    interface ITracedNode with
        member _.TraceGraph = box graph
#endif

    interface INode with
        member _.Id = node.Id
        member _.Status = node.Status ||| Staleness.ofFreshness node.Freshness

    interface IComputation with
        member _.AddSource source = NodeOps.AddSource (node, source)

        member _.MovedIn(slot, handle) =
            node.Sources.MovedIn (slot, handle)

        member _.HasReadSlot slot =
            slot < node.Sources.Cursor

        member _.MarkDirty() = NodeOps.EffectMarkDirty node

        member _.MarkCheck() = NodeOps.EffectMarkCheck node

    interface IScheduled with
        member _.Execute() = NodeOps.Execute node

    interface IScopeHost with
        member _.CanOwn = acting

        member _.Scope =
            if acting then
                if Platform.isNull (box actionOwner) then
                    actionOwner <- new Owner (graph.Root)
                    Tracer.ScopeNew (actionOwner, graph, node.Id)

                    if node.Disposed then
                        actionOwner.Dispose ()

                actionOwner
            else
                node.Violated <- true
                raise (InvalidOperationException ScopeMessages.effectOn)

/// <summary>The async memo a <c>Previous</c> reads.</summary>
type internal IPreviousSource<'T> =
    /// <summary>
    /// The value last published, once <c>position</c> chained results are applied.
    /// </summary>
    abstract Settled: position: int -> Task<'T voption>

/// <summary>The completed <c>ValueNone</c> task shared by every node of one value type.</summary>
[<AbstractClass; Sealed>]
type internal NothingPublished<'T> private () =
#if FABLE_COMPILER
    static member val Task: Task<'T voption> = Platform.completedWith ValueNone
#else
    [<DefaultValue>]
    static val mutable private task: Task<'T voption>

    static member Task: Task<'T voption> =
        if obj.ReferenceEquals (NothingPublished<'T>.task, null) then
            NothingPublished<'T>.task <- Platform.completedWith ValueNone

        NothingPublished<'T>.task
#endif

/// <summary>
/// The value an async memo published before a flight, handed to the flight's body.
/// </summary>
/// <remarks>
/// <para>
/// Under every policy except <c>Queue</c>, <c>Settled</c> is complete when the body runs. Under <c>Queue</c>, a flight
/// that starts while an earlier flight's result is unapplied receives the value as it stands once that result is
/// applied, so a chain of flights folds in start order. A faulted or dropped predecessor leaves the last settled value,
/// and disposing the memo completes <c>Settled</c> with the value last published.
/// </para>
/// <para>
/// Tracking stops at the body's first await that suspends. Read every input, then await <c>Settled</c>: under
/// <c>Queue</c>, a read made after awaiting it is not tracked.
/// </para>
/// </remarks>
[<Struct; NoComparison; NoEquality>]
type Previous<'T> internal (source: IPreviousSource<'T>, position: int) =
    /// <summary>
    /// Completes with the value last published, <c>ValueNone</c> before the first. Under <c>Queue</c> it completes after
    /// the preceding flight's result is applied. Its awaiters resume outside the graph's apply.
    /// </summary>
    member _.Settled: Task<'T voption> =
        if Platform.isNull (box source) then
            NothingPublished<'T>.Task
        else
            source.Settled position

/// <summary>
/// A derived computation whose value arrives later: the body runs synchronously and returns a <c>Task</c>. This library's
/// <c>createAsync</c>.
/// </summary>
/// <remarks>
/// Reads are tracked up to the body's first await that suspends; a read after it is untracked. On .NET a continuation run
/// inline inside another computation's body is detached from that body. Under Fable every await suspends and an outcome
/// lands on a later microtask. Pure or owning, as a <c>Memo</c> is: an owning scope is discharged before each flight starts.
/// The body receives the value last published as a <c>Previous</c>. Flights follow the <c>FlightPolicy</c> given at
/// creation, or the graph's <c>GraphOptions.FlightPolicy</c> when none was given.
/// </remarks>
type AsyncMemo<'T> private (graph: Graph, compute: Previous<'T> -> CancellationToken -> Task<'T>, mode: ScopeMode, policy: FlightPolicy) =
    let id = graph.NextId ()
    let observers = ObserverSet ()
    do Tracer.Bind (observers, graph, id)
    let mutable sources = SourceList.Create ()
    do Tracer.Bind (sources, graph, id)
    let mutable pendingSource: INode = Unchecked.defaultof<INode>

    let mutable freshness = Freshness.Dirty
    let mutable status = Status.Pending ||| Status.Uninitialized
    let mutable value = Unchecked.defaultof<'T>
    let mutable failure: Failure = null
    let mutable runs = 0
    let mutable disposed = false
    let mutable link: OwnerLink = null

    /// <summary>Set when a run ends stale, as on <c>Memo</c>. The next mark notifies the observers and clears it.</summary>
    let mutable unannounced = false

    /// <summary>
    /// Owner of the nodes an owning body creates, allocated by the first of
    /// them. Discharged before each flight starts and disposed with the
    /// computation.
    /// </summary>
    let mutable scope: Owner = Unchecked.defaultof<Owner>

    /// <summary>
    /// Set when a pure body creates an owned node, and cleared when the run
    /// fails for it.
    /// </summary>
    let mutable violated = false

    /// <summary>
    /// Which flight is current. A settling task publishes only if it is still
    /// the one the graph is waiting for; anything older carries a value that
    /// belongs to a dependency state the graph has already left, which is the
    /// same stale-pairing the diamond test pins.
    /// </summary>
    let mutable generation = 0

    /// <summary>
    /// The source of the flight token. Under <c>CancelPrevious</c> each flight gets its own. Under the other policies
    /// the flights in progress share one, disposed once every flight holding it has settled.
    /// </summary>
    let mutable cts: CancellationTokenSource = null

    /// <summary>The number of flights started and not yet settled, under <c>CancelPrevious</c> and <c>KeepLatest</c>.</summary>
    let mutable flying = 0

    /// <summary>The number of bodies executing. The shared source is retained while it is nonzero.</summary>
    let mutable running = 0
    let mutable checking = false

    /// <summary>
    /// Out-of-order settled outcomes awaiting an earlier sequence slot under <c>Queue</c>; null until needed.
    /// </summary>
    let mutable pendingQueue: Dictionary<int, struct (int * Platform.FlightOutcome<'T>)> =
        null

    /// <summary>
    /// Under <c>Queue</c>, the number of results queued and not yet applied. Under
    /// <c>FinishCurrent</c>, <c>Drop</c> and <c>DropWhilePending</c>, 0 with no flight in progress, 1 with a flight in
    /// progress, 2 with a flight in progress that has deferred or discarded a change. Zero under the other policies.
    /// </summary>
    let mutable queued = 0

    /// <summary>The number of results ever chained onto <c>tail</c>, under <c>Queue</c>.</summary>
    let mutable chained = 0

    /// <summary>The number of chained results applied, under <c>Queue</c>.</summary>
    let mutable applied = 0

    /// <summary>True once a result has written <c>value</c>.</summary>
    let mutable published = false

    /// <summary>The completed task of the value last published, created on first read.</summary>
    let mutable settledTask: Task<'T voption> = null

    /// <summary>
    /// The <c>Previous.Settled</c> tasks still waiting, keyed by the number of applied results that completes each.
    /// </summary>
    let mutable waiters: Dictionary<int, Platform.Deferred<'T voption>> = null

    /// <summary>
    /// True while <c>Launch</c> attaches the settle continuation. A flight already complete
    /// publishes inline, during the read that launched it.
    /// </summary>
    let mutable launching = false

    let wake () =
        Tracer.Moved (graph, id, Failure.Payload (failure, box value))

        if launching then
            graph.NotifyMoved observers
        else
            graph.NoteStaleTransition ()
            observers.NotifyDirty ()

        Tracer.Notified graph
        graph.RequestFlush ()

    let lastSettled () =
        if published then ValueSome value else ValueNone

    // Under the lock `Settled` takes, so a read off the graph thread never caches a value this write replaces.
    let write (v: 'T) =
        lock observers (fun () ->
            value <- v
            published <- true
            settledTask <- null)

    /// <summary>
    /// The value last published, as a completed task shared by every read until the next publish. Called under the
    /// <c>observers</c> lock.
    /// </summary>
    let settledNow () =
        if Platform.isNull settledTask then
            settledTask <-
                if published then
                    Platform.completedWith (ValueSome value)
                else
                    NothingPublished<'T>.Task

        settledTask

    /// <summary>
    /// Counts one more chained result applied and completes the waiter keyed on the new count. Called under the
    /// <c>observers</c> lock.
    /// </summary>
    let releaseNext () =
        applied <- applied + 1

        if not (Platform.isNull waiters) then
            let waiter = Platform.findOrDefault waiters applied

            if not (Platform.isNull (box waiter)) then
                waiters.Remove applied |> ignore
                Platform.resolve waiter (lastSettled ())

    /// <summary>Resets or disposes the flight token's source without cancelling it.</summary>
    let retireSource () =
        if not (Platform.isNull cts) then
#if NET6_0_OR_GREATER && !FABLE_COMPILER
            if not (cts.TryReset ()) then
                cts.Dispose ()
                cts <- null
#else
            cts.Dispose ()
            cts <- null
#endif

    /// <summary>
    /// Retires the flight token's source once no body is executing and every flight holding it has settled.
    /// Registrations left on the token are released with it.
    /// </summary>
    let retireIfQuiet () =
        if running = 0 then
            match policy with
            | FlightPolicy.Queue
            | FinishCurrent
            | Drop
            | DropWhilePending when queued = 0 -> retireSource ()
#if !FABLE_COMPILER
            | CancelPrevious
#endif
            | KeepLatest when flying = 0 -> retireSource ()
            | _ -> ()

    /// <summary>
    /// Brings every source current. Clears a stale source left behind by a discarded change, so the source's next
    /// change reaches this node.
    /// </summary>
    let refreshSources () =
        graph.EnterPull ()

        try
            let mutable i = 0

            while i < sources.Count do
                sources.RefreshSourceAt i
                i <- i + 1
        finally
            graph.ExitPull ()

    /// <summary>
    /// Discards the change left by a flight under <c>Drop</c> or a successful flight under <c>DropWhilePending</c>:
    /// the sources are brought current and the node is Clean.
    /// </summary>
    let discardAtSettle (gen: int) =
        refreshSources ()

        if freshness = Freshness.Dirty then
            Tracer.RunDropped (graph, id, gen)

        freshness <- Freshness.Clean

    /// <summary>Applies the result of the run numbered <c>gen</c>. Runs on the graph thread.</summary>
    let applyResult (gen: int) (outcome: Platform.FlightOutcome<'T>) =
        // True under `FinishCurrent` when a change arrived during the flight, and under `DropWhilePending` when a
        // failed flight discarded a change.
        let mutable owed = false

        // `Queue` applies every result in the order the flights started,
        // so it is the one policy that does not discard the superseded.
        let current =
            match policy with
            | FlightPolicy.Queue ->
                queued <- queued - 1
                true
            | KeepLatest ->
                flying <- flying - 1
                gen = generation
#if FABLE_COMPILER
            | CancelPrevious -> gen = generation
#else
            | CancelPrevious ->
                flying <- flying - 1
                gen = generation
#endif
            | Drop ->
                if
                    not disposed
                    && (queued = 2 || freshness <> Freshness.Clean)
                then
                    discardAtSettle gen

                queued <- 0
                gen = generation
            | DropWhilePending ->
                if
                    not disposed
                    && (queued = 2 || freshness <> Freshness.Clean)
                then
                    if
                        (match outcome with
                         | Platform.FlightOutcome.Completed _ -> true
                         | _ -> false)
                    then
                        discardAtSettle gen
                    else
                        // A Check is a discarded change only once a source resolves it to Dirty.
                        if queued = 1 && freshness = Freshness.Check then
                            refreshSources ()

                        if queued = 2 || freshness = Freshness.Dirty then
                            owed <- true
                        else
                            freshness <- Freshness.Clean

                queued <- 0
                gen = generation
            | FinishCurrent ->
                owed <- queued = 2
                queued <- 0
                gen = generation

        retireIfQuiet ()

        // An owed trailing run holds the node pending as a pending source does.
        let suspended = owed || not (Platform.isNull (box pendingSource))

        if current && not disposed then
            match outcome with
            // Under `Queue`, the newest run is waiting on a source: an
            // older flight's value is the last settled one, and the node
            // stays pending.
            | Platform.FlightOutcome.Completed v when suspended ->
                Tracer.FlightSettled (graph, id, gen, 0, (if owed then 2 else 1), box v)
                write v
                status <- Status.Pending
            | Platform.FlightOutcome.Faulted _
            | Platform.FlightOutcome.Canceled _ when suspended ->
                // An older flight's failure is dropped; the node stays pending on the source or the run owed.
                Tracer.FlightFailureDrop (
                    graph,
                    id,
                    gen,
                    owed,
                    (match policy with
                     | FlightPolicy.FinishCurrent -> true
                     | _ -> false)
                )
            | Platform.FlightOutcome.Completed v ->
                Tracer.FlightSettled (graph, id, gen, 0, 0, box v)
                write v
                failure <- null
                status <- Status.None
                wake ()
            // A cancellation of the current flight is a failure.
            | Platform.FlightOutcome.Faulted ex
            | Platform.FlightOutcome.Canceled ex ->
                Tracer.FlightSettled (
                    graph,
                    id,
                    gen,
                    (match outcome with
                     | Platform.FlightOutcome.Canceled _ -> 2
                     | _ -> 1),
                    0,
                    ex
                )
                // The owner link is the one reference to this node in reach of a let-bound function.
                failure <- graph.FailureOf (ex, (link.Child :?> INode), null)
                status <- Status.Error
                wake ()
        else
            Tracer.FlightDrop (graph, id, gen, (if disposed then 2 else 1))

        match policy with
        // Every chained result, dropped or failed included, completes the waiter behind it with the last settled value.
        | FlightPolicy.Queue -> lock observers releaseNext
        // The trailing or replacing run starts at the next read: readers are told the value is stale again.
        | FinishCurrent
        | DropWhilePending when owed && not disposed ->
            freshness <- Freshness.Dirty
            Tracer.TrailingRun (graph, id)
            graph.NoteStaleTransition ()
            observers.NotifyDirty ()
            Tracer.Notified graph
            graph.RequestFlush ()
        | CancelPrevious
        | KeepLatest
        | FinishCurrent
        | Drop
        | DropWhilePending -> ()

    let applyQueued (seqNum: int) (gen: int) (outcome: Platform.FlightOutcome<'T>) =
        if seqNum = applied + 1 then
            applyResult gen outcome

            while not (Platform.isNull pendingQueue)
                  && pendingQueue.Count > 0
                  && pendingQueue.ContainsKey (applied + 1) do
                let nextSeq = applied + 1
                let struct (nextGen, nextOutcome) = pendingQueue[nextSeq]
                pendingQueue.Remove nextSeq |> ignore
                applyResult nextGen nextOutcome
        else
            if Platform.isNull pendingQueue then
                pendingQueue <- Dictionary<int, struct (int * Platform.FlightOutcome<'T>)>()

            pendingQueue[seqNum] <- struct (gen, outcome)

#if FABLE_COMPILER
    let publish (gen: int) (outcome: Platform.FlightOutcome<'T>) =
        applyResult gen outcome

    let publishQueued (seqNum: int) (gen: int) (outcome: Platform.FlightOutcome<'T>) =
        applyQueued seqNum gen outcome
#else
    let publish (gen: int) (outcome: Platform.FlightOutcome<'T>) =
        if graph.IsOnGraphThread then
            applyResult gen outcome
        else
            graph.PostQueued (fun () -> applyResult gen outcome)

    let publishQueued (seqNum: int) (gen: int) (outcome: Platform.FlightOutcome<'T>) =
        if graph.IsOnGraphThread then
            applyQueued seqNum gen outcome
        else
            graph.PostQueued (fun () -> applyQueued seqNum gen outcome)
#endif

    // Fable cannot translate a secondary constructor of a type with a
    // self-identifier. Under Fable the `Api` functions construct this type.
#if !FABLE_COMPILER
    /// <summary>
    /// A pure async memo over <c>compute</c>.
    /// </summary>
    new(graph: Graph, compute: Func<Previous<'T>, CancellationToken, Task<'T>>) as this =
        new AsyncMemo<'T> (graph, (fun previous token -> compute.Invoke (previous, token)), ScopeMode.PureAsync, graph.Options.FlightPolicy)
        then this.Attach ()

    /// <summary>
    /// An owning async memo over <c>compute</c> when <c>owning</c> is true, a pure one
    /// otherwise.
    /// </summary>
    new(graph: Graph, compute: Func<Previous<'T>, CancellationToken, Task<'T>>, owning: bool) as this =
        new AsyncMemo<'T> (
            graph,
            (fun previous token -> compute.Invoke (previous, token)),
            (if owning then ScopeMode.Owning else ScopeMode.PureAsync),
            graph.Options.FlightPolicy
        )

        then this.Attach ()
#endif

    /// <summary>
    /// An async memo over <c>compute</c>, owned by the current owner.
    /// </summary>
    static member internal Create(graph: Graph, compute: Previous<'T> -> CancellationToken -> Task<'T>, mode: ScopeMode) =
        AsyncMemo<'T>.Create(graph, compute, mode, graph.Options.FlightPolicy)

    /// <summary>
    /// An async memo over <c>compute</c> under <c>policy</c>, owned by the current owner.
    /// </summary>
    static member internal Create(graph: Graph, compute: Previous<'T> -> CancellationToken -> Task<'T>, mode: ScopeMode, policy: FlightPolicy) =
        let memo = new AsyncMemo<'T> (graph, compute, mode, policy)
        memo.Attach ()
        memo

    member private this.Attach() =
        link <- graph.CurrentOwner.AttachLinked this
        Tracer.AsyncMemoNew (graph, id, link.Owner)

    member private this.DetachSources() =
        sources.Clear (this :> IComputation)

    /// <summary>
    /// Discharges the previous flight's scope, then starts one flight, unless
    /// the async memo was disposed meanwhile. A read from a cleanup serves the
    /// previous state. A flight started by a cleanup's read replaces this one,
    /// and a write after that read starts over, as on <c>Memo.Recompute</c>.
    /// With a flight in progress, under <c>FinishCurrent</c> owes a trailing run instead and keeps the scope, and under
    /// <c>Drop</c> and <c>DropWhilePending</c> discards the change and keeps the scope.
    /// </summary>
    member private this.Start() =
        if
            queued <> 0
            && (match policy with
                | FlightPolicy.Queue -> false
                | _ -> true)
        then
            freshness <- Freshness.Clean
            queued <- 2

            Tracer.RunDeferred (
                graph,
                id,
                generation,
                (match policy with
                 | FlightPolicy.FinishCurrent -> true
                 | _ -> false)
            )
        elif Platform.isNull (box scope) then
            this.Launch ()
        else
            let before = runs
            freshness <- Freshness.Clean
            graph.Discharge scope

            if not disposed then
                if runs = before then
                    this.Launch ()
                elif freshness = Freshness.Dirty then
                    this.Start ()
            elif freshness = Freshness.Clean then
                unannounced <- false


    member private this.FailLaunch(gen: int, ex: exn) =
        match policy with
        | FlightPolicy.Queue when queued > 0 ->
            queued <- queued + 1
            chained <- chained + 1
            let seqNum = chained
#if !FABLE_COMPILER
            if graph.IsOnGraphThread then
                applyQueued seqNum gen (Platform.FlightOutcome<'T>.Faulted ex)
            else
                publishQueued seqNum gen (Platform.FlightOutcome<'T>.Faulted ex)
#else
            publishQueued seqNum gen (Platform.FlightOutcome<'T>.Faulted ex)
#endif
        | _ ->
            failure <- graph.FailureOf (ex, this, null)
            status <- Status.Error

    member private this.Launch() =
        freshness <- Freshness.Clean
        unannounced <- false
        sources.BeginRun ()

        if not (Platform.isNull (box pendingSource)) then
            pendingSource <- Unchecked.defaultof<INode>

        // Bumped before the cancel: a superseded flight settled inline by its
        // own cancellation must already be out of date when it publishes.
        generation <- generation + 1
        let gen = generation

        match policy with
        | CancelPrevious ->
#if FABLE_COMPILER
            if not (Platform.isNull cts) then
                cts.Cancel ()
                cts.Dispose ()

            cts <- new CancellationTokenSource ()
#else
            if flying > 0 && not (Platform.isNull cts) then
                let superseded = cts
                cts <- null
                superseded.Cancel ()
                superseded.Dispose ()

            if Platform.isNull cts then
                cts <- new CancellationTokenSource ()
#endif
        | KeepLatest
        | FlightPolicy.Queue
        | FinishCurrent
        | Drop
        | DropWhilePending ->
            if Platform.isNull cts then
                cts <- new CancellationTokenSource ()

        let token = cts.Token
        // The flight resumes from the value once every result chained before it is applied.
        let previous = Previous<'T>(this :> IPreviousSource<'T>, chained)
        runs <- runs + 1
        Tracer.RunStart (graph, id, runs)
        failure <- null

        status <-
            Status.Pending
            ||| (status &&& Status.Uninitialized)

        let mutable flight = null

        running <- running + 1

        try
            try
                // A flight dropped for a pending read or a violation gets a
                // continuation, so its rejection is handled; under Fable an
                // unhandled rejection ends the node process.
#if FABLE_COMPILER
                let started =
                    graph.RunHosted (
                        this :> IComputation,
                        fun () ->
                            let s = compute previous token

                            if
                                (violated || graph.RaisedPending)
                                && not (Platform.isNull s)
                            then
                                Platform.outcomeOf s |> ignore

                            s
                    )
#else
                let saved = graph.EnterHosted (this :> IComputation)

                let started =
                    try
                        let s =
                            Platform.runFlightBodyWith graph.EnterContinuationDelegate graph.LeaveContinuationDelegate compute previous token

                        if
                            (violated || graph.RaisedPending)
                            && not (Platform.isNull s)
                        then
                            Platform.outcomeOf s |> ignore

                        graph.CheckRaised ()
                        s
                    finally
                        graph.LeaveHosted saved
#endif

                if violated then
                    raise (InvalidOperationException (ScopeMessages.forMode mode))

                flight <- started
            finally
                running <- running - 1

                if disposed then
                    sources.Clear (this :> IComputation)
                else
                    sources.EndRun (this :> IComputation)
        with
        | ex when violated ->
            violated <- false
            this.FailLaunch (gen, ScopeMessages.failure mode ex)
        | NotReadyException source ->
            // The body suspended before it could start a flight: it read a
            // source that is itself pending. There is nothing to await, only
            // another source to wait on.
            pendingSource <- source
        | ex -> this.FailLaunch (gen, ex)

        if not (Platform.isNull flight) then
            let flight = flight
            Tracer.FlightStart (graph, id, gen)
            launching <- true

            try
#if !FABLE_COMPILER
                if flight.IsCompleted && graph.IsOnGraphThread then
                    let outcome = Platform.settledOutcome flight

                    match policy with
                    | FlightPolicy.Queue ->
                        queued <- queued + 1
                        chained <- chained + 1
                        let seqNum = chained
                        applyQueued seqNum gen outcome
                    | KeepLatest
                    | CancelPrevious ->
                        flying <- flying + 1
                        applyResult gen outcome
                    | FinishCurrent
                    | Drop
                    | DropWhilePending ->
                        queued <- 1
                        applyResult gen outcome
                else
                    match policy with
                    | FlightPolicy.Queue ->
                        queued <- queued + 1
                        chained <- chained + 1
                        let seqNum = chained

                        Platform.whenSettled flight (publishQueued seqNum gen)
                        |> ignore
                    | KeepLatest
                    | CancelPrevious ->
                        flying <- flying + 1

                        Platform.whenSettled flight (publish gen)
                        |> ignore
                    | FinishCurrent
                    | Drop
                    | DropWhilePending ->
                        queued <- 1

                        Platform.whenSettled flight (publish gen)
                        |> ignore
#else
                match policy with
                | FlightPolicy.Queue ->
                    queued <- queued + 1
                    chained <- chained + 1
                    let seqNum = chained

                    Platform.whenSettled flight (publishQueued seqNum gen)
                    |> ignore
                | KeepLatest ->
                    flying <- flying + 1

                    Platform.whenSettled flight (publish gen)
                    |> ignore
                | CancelPrevious ->
                    Platform.whenSettled flight (publish gen)
                    |> ignore
                | FinishCurrent
                | Drop
                | DropWhilePending ->
                    queued <- 1

                    Platform.whenSettled flight (publish gen)
                    |> ignore
#endif
            finally
                launching <- false

            Tracer.RunEnd (graph, id, status)
        else
            // A run that failed or suspended before starting a flight leaves no settle to retire the source.
            retireIfQuiet ()
            Tracer.RunEnd (graph, id, status)

        if freshness <> Freshness.Clean then
            unannounced <- true
            graph.NoteStaleTransition ()

    member private this.EnsureCurrent() =
        // One comparison on the cached path, as on `Memo`.
        if freshness <> Freshness.Clean && not disposed then
            this.Pull ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.SettlePullAndRead() : 'T =
        let currentStatus = status
        let currentFailure = failure
        let currentValue = value
        graph.FinishPullSettle ()
        graph.Track (this :> ISource)

        if freshness <> Freshness.Clean && not disposed then
            graph.MarkReaderDirty ()

        if StatusBits.isUnready currentStatus then
            if StatusBits.isPending currentStatus then
                raise (graph.NotReady (this :> INode))

            graph.Raise currentFailure

        currentValue

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.SettlePullAndReadTry() : Reading<'T> =
        let currentStatus = status
        let currentFailure = failure
        let currentValue = value
        graph.FinishPullSettle ()
        graph.Track (this :> ISource)

        if freshness <> Freshness.Clean && not disposed then
            graph.MarkReaderDirty ()

        if StatusBits.isUnready currentStatus then
            if StatusBits.isPending currentStatus then
                Pending
            else
                Failed currentFailure.Error
        else
            Ready currentValue

    /// <summary>
    /// Brings a stale async memo current.
    /// </summary>
    member private this.Pull() =
        graph.EnterPull ()

        try
            this.Refresh ()
        finally
            graph.ExitPull ()

    member private this.PullWithoutSettle() : bool =
        graph.EnterPull ()

        try
            this.Refresh ()
        with _ ->
            graph.ExitPull ()
            reraise ()

        graph.ExitPullDeferSettle ()
        || freshness <> Freshness.Clean

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CheckActiveSource(i: int) =
        if freshness = Freshness.Check then
            let source = sources.SourceAt i

            if source.IsStale && source.IsActive then
                (this :> IComputation).MarkDirty()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ResolveCheckAgain() =
        let mutable again = sources.HasStaleSource ()

        while again do
            let before = graph.StaleTransitions
            let mutable i = 0

            while freshness = Freshness.Check && i < sources.Count do
                sources.RefreshSourceAt i

                if graph.ActiveSkips <> 0 then
                    this.CheckActiveSource i

                i <- i + 1

            again <-
                freshness = Freshness.Check
                && graph.StaleTransitions <> before
                && sources.HasStaleSource ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RefreshActive() =
        graph.NoteActiveSkip ()

        if checking then
            (this :> IComputation).MarkDirty()

    member private this.Refresh() =
        // Resolving `Check` matters more here than anywhere: the work it
        // may avoid is a network round trip, not a multiplication.
        if not checking && running = 0 then
            if freshness = Freshness.Check then
                Tracer.CheckStart (graph, id)
                checking <- true
                let before = graph.StaleTransitions
                let mutable i = 0

                while freshness = Freshness.Check && i < sources.Count do
                    sources.RefreshSourceAt i

                    if graph.ActiveSkips <> 0 then
                        this.CheckActiveSource i

                    i <- i + 1

                if
                    freshness = Freshness.Check
                    && graph.StaleTransitions <> before
                then
                    this.ResolveCheckAgain ()

                checking <- false
                Tracer.CheckResolved (graph, id, (freshness = Freshness.Dirty))

                if freshness = Freshness.Check then
                    freshness <- Freshness.Clean
                    unannounced <- false

            if freshness = Freshness.Dirty then
                this.Start ()
        else
            this.RefreshActive ()

    interface IOwned with
        member this.Release() =
            link <- null
            this.Dispose ()

    interface IScopeHost with
        member _.CanOwn =
            not (Platform.isNull (box scope))
            || mode = ScopeMode.Owning

        member _.Scope =
            if Platform.isNull (box scope) then
                if mode = ScopeMode.Owning then
                    scope <- new Owner (graph.Root)
                    Tracer.ScopeNew (scope, graph, id)

                    if disposed then
                        scope.Dispose ()
                else
                    violated <- true
                    raise (InvalidOperationException (ScopeMessages.forMode mode))

            scope

#if RANVIER_TRACE
    interface ITracedNode with
        member _.TraceGraph = box graph
#endif

    interface INode with
        member _.Id = id
        member _.Status = status ||| Staleness.ofFreshness freshness

    interface ISource with
        member _.AddObserver(c, slot) =
            observers.Add (c, slot)

        member _.TryAddObserver(c, slot) =
            observers.TryAdd (c, slot)

        member _.RemoveObserverAt(c, handle) =
            observers.RemoveAt (c, handle)

        member this.UpdateIfNecessary() =
            this.EnsureCurrent ()

        member _.IsStale = freshness <> Freshness.Clean && not disposed

        member _.IsActive = running > 0 || checking

    interface IComputation with
        member this.AddSource s =
            if not disposed then
                sources.Add (this :> IComputation, s)

        member _.MovedIn(slot, handle) =
            sources.MovedIn (slot, handle)

        member _.HasReadSlot slot =
            slot < sources.Cursor

        member _.MarkDirty() =
            if freshness <> Freshness.Dirty || unannounced then
                if freshness = Freshness.Clean || unannounced then
                    graph.NoteStaleTransition ()

                freshness <- Freshness.Dirty
                unannounced <- false

                // `Dirty`, not `Check`, and unlike `Memo` this is not deferred
                // to the recompute: re-running an async memo *always* changes
                // what it publishes, because starting a flight makes it
                // pending. There is no cutoff to wait for. Dependents are told
                // immediately rather than when the flight lands, because they
                // have to learn that what they read is now stale, and an effect
                // has to re-read to start the flight at all.
                observers.NotifyDirty ()

        member _.MarkCheck() =
            if freshness = Freshness.Clean then
                freshness <- Freshness.Check
                unannounced <- false
                observers.NotifyCheck ()
            elif unannounced then
                unannounced <- false
                graph.NoteStaleTransition ()
                observers.NotifyCheck ()

    /// <summary>
    /// Cancels the flight in progress and detaches from every source.
    /// Idempotent. Usually called for you, by the enclosing owner.
    /// </summary>
    /// <remarks>
    /// A settled memo keeps its last value. A memo still pending fails with
    /// <c>ObjectDisposedException</c> and wakes its readers once.
    /// </remarks>
    member this.Dispose() =
        graph.Entered (
            "Disposing a node",
            fun () ->
                if not disposed then
                    disposed <- true
                    Tracer.NodeDispose (graph, id)

                    if not (Platform.isNull link) then
                        link.Detach ()
                        link <- null

                    this.DetachSources ()

                    if not (Platform.isNull (box scope)) then
                        graph.Retire scope

                    if not (Platform.isNull cts) then
                        // Detached before the cancel: a flight settled inline by it runs `retireSource`.
                        let source = cts
                        cts <- null
                        source.Cancel ()
                        source.Dispose ()

                    if StatusBits.isPending status then
                        pendingSource <- Unchecked.defaultof<INode>

                        failure <- Failure (ObjectDisposedException (this.GetType().Name), (this :> INode))
                        status <- Status.Error
                        wake ()

                    lock observers (fun () ->
                        if not (Platform.isNull waiters) then
                            let settled = lastSettled ()

                            for waiter in waiters.Values do
                                Platform.resolve waiter settled

                            waiters <- null)
        )

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

    // The lock covers a body reading `Settled` from a continuation off the graph thread.
    interface IPreviousSource<'T> with
        member _.Settled position =
            lock observers (fun () ->
                if disposed || applied >= position then
                    settledNow ()
                else
                    if Platform.isNull waiters then
                        waiters <- Dictionary<int, Platform.Deferred<'T voption>>()

                    let waiter = Platform.findOrDefault waiters position

                    if Platform.isNull (box waiter) then
                        let waiter = Platform.deferred<'T voption>()
                        waiters[position] <- waiter
                        Platform.deferredTask waiter
                    else
                        Platform.deferredTask waiter)

    member _.Status = status

    /// <summary>
    /// The source of the last run's last pending read, or empty when the run did not suspend.
    /// </summary>
    /// <remarks>
    /// Under <c>KeepLatest</c> and <c>Queue</c>, an earlier flight may still be in progress while a source is listed.
    /// Under <c>FinishCurrent</c>, <c>Drop</c> and <c>DropWhilePending</c>, a source is listed only by a run that started
    /// no flight.
    /// </remarks>
    member internal _.PendingSource = pendingSource

    member _.PendingSources =
        if Platform.isNull (box pendingSource) then
            Seq.empty
        else
            [| pendingSource |] :> seq<INode>

    /// <summary>
    /// How many times the body has run, including runs that suspended or threw before starting a flight.
    /// </summary>
    member _.Runs = runs

    /// <summary>
    /// Transparent, tracked read. Raises <c>NotReadyException</c> while <c>Status</c> is Pending and the recorded
    /// error while it is Error.
    /// </summary>
    /// <remarks>
    /// Pending while a flight is in progress, except under <c>Queue</c> once an earlier flight's outcome is applied.
    /// </remarks>
#if FABLE_COMPILER
    member this.Value: 'T =
        if StatusBits.isCleanAndReady freshness status then
            graph.Track (this :> ISource)
            value
        else
            this.ReadSlow ()
#else
    member this.Value: 'T = this.GetFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetFast() : 'T =
        if StatusBits.isCleanAndReady freshness status then
            graph.Track (this :> ISource)
            value
        else
            this.ReadSlow ()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ReadSlow() : 'T =
        if
            freshness <> Freshness.Clean
            && not disposed
            && this.PullWithoutSettle ()
        then
            this.SettlePullAndRead ()
        else
            graph.Track (this :> ISource)

            if StatusBits.isUnready status then
                if StatusBits.isPending status then
                    raise (graph.NotReady (this :> INode))

                graph.Raise failure

            value

    /// <summary>
    /// Non-throwing, tracked read.
    /// </summary>
#if FABLE_COMPILER
    member this.TryValue: Reading<'T> =
        if StatusBits.isCleanAndReady freshness status then
            graph.Track (this :> ISource)
            Ready value
        else
            this.ReadTrySlow ()
#else
    member this.TryValue
        with [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>] get () : Reading<'T> = this.GetTryFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetTryFast() : Reading<'T> =
        if StatusBits.isCleanAndReady freshness status then
            graph.Track (this :> ISource)
            Ready value
        else
            this.ReadTrySlow ()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ReadTrySlow() : Reading<'T> =
        if
            freshness <> Freshness.Clean
            && not disposed
            && this.PullWithoutSettle ()
        then
            this.SettlePullAndReadTry ()
        else
            graph.Track (this :> ISource)

            if StatusBits.isUnready status then
                if StatusBits.isPending status then
                    Pending
                else
                    Failed failure.Error
            else
                Ready value

    /// <summary>
    /// The node the current failure originated in while <c>Status</c> has <c>Error</c>, otherwise null. Untracked.
    /// </summary>
    /// <remarks>
    /// The async memo itself for a faulted or cancelled flight, a throwing body, a purity violation or disposal while
    /// pending; the upstream node when the body rethrew the exception of a failed read before its first await that
    /// suspends. A failure read after that await can report the async memo instead.
    /// </remarks>
    member _.ErrorOrigin: INode = Failure.OriginOf failure

    /// <summary>
    /// The last settled value, untracked, without starting a flight. The one
    /// read that never suspends — Solid's <c>latest</c>.
    /// </summary>
    member _.Peek = value

/// <summary>
/// Where a channel stops.
/// </summary>
/// <remarks>
/// <para>
/// Pending and error both propagate: a suspended source suspends everything
/// that reads it, all the way out. Something has to end that, or the only
/// possible response to one in-flight request is for the whole graph to wait.
/// A boundary is a computation that catches a channel coming out of its body
/// and substitutes a value, so its own dependents see an ordinary value and
/// never learn that anything was in flight.
/// </para>
/// <para>
/// This is control flow over the reactive graph, not rendering. A boundary
/// yields a value of the same type its body yields; whether that value is a
/// view, a message or a number is the caller's business. Solid's <c>Suspense</c> is
/// this plus a DOM fallback, and the DOM half belongs in Partas.Solid.
/// </para>
/// <para>
/// Absorbing a channel does not mean ignoring the source. The read that
/// suspended still linked its edge before throwing, so the settle marks the
/// boundary dirty, its dependents are notified, and the next read runs the body
/// again — from the top, never resumed.
/// </para>
/// <para>
/// Constructed through the static members: the three of them name the three
/// things a boundary can catch.
/// </para>
/// </remarks>
type Boundary<'T>
    private
    (graph: Graph, body: unit -> 'T, onPending: ('T voption -> 'T) voption, onError: (exn -> 'T voption -> 'T) voption, equal: IEqualityComparer<'T>)
    =
    let id = graph.NextId ()
    let observers = ObserverSet ()
    do Tracer.Bind (observers, graph, id)
    let mutable sources = SourceList.Create ()
    do Tracer.Bind (sources, graph, id)
    let mutable pendingFirst: INode = Unchecked.defaultof<INode>
    let mutable pendingSecond: INode = Unchecked.defaultof<INode>

    let mutable freshness = Freshness.Dirty
    let mutable status = Status.Uninitialized
    let mutable value = Unchecked.defaultof<'T>
    let mutable failure: Failure = null
    let mutable caught: Failure = null
    let mutable waiting = false
    let mutable runs = 0
    let mutable active = 0
    let mutable disposed = false
    let mutable link: OwnerLink = null

    /// <summary>Set when a run ends stale, as on <c>Memo</c>. The next mark notifies the observers and clears it.</summary>
    let mutable unannounced = false

    /// <summary>
    /// True once a run has produced a value: the body's, a fallback's or a recover's.
    /// </summary>
    let mutable shown = false

    /// <summary>
    /// Owner of the nodes the body creates, allocated by the first of them.
    /// Discharged before each run and disposed with the computation.
    /// </summary>
    let mutable scope: Owner = Unchecked.defaultof<Owner>

    /// <summary>
    /// A boundary over <c>body</c>, owned by the current owner.
    /// </summary>
    static member internal Create(graph: Graph, body: unit -> 'T, onPending: ('T voption -> 'T) voption, onError: (exn -> 'T voption -> 'T) voption) =
        let boundary =
            Boundary<'T>(graph, body, onPending, onError, graph.EncodedComparer<'T>())

        boundary.Attach ()
        boundary

    /// <summary>An owned boundary whose value cutoff uses <c>comparer</c>.</summary>
    static member internal CreateWithComparer
        (
            graph: Graph,
            body: unit -> 'T,
            onPending: ('T voption -> 'T) voption,
            onError: (exn -> 'T voption -> 'T) voption,
            comparer: IEqualityComparer<'T>
        ) =
        let boundary =
            Boundary<'T>(graph, body, onPending, onError, ComparerSentinel.encode comparer)

        boundary.Attach ()
        boundary

    // Owned through `IOwned`, for the reason given on Memo.
    member private this.Attach() =
        link <- graph.CurrentOwner.AttachLinked this
        Tracer.BoundaryNew (graph, id, link.Owner)

    member private this.DetachSources() =
        sources.Clear (this :> IComputation)

    /// <summary>
    /// Discharges the previous run's scope, then runs the body once, unless
    /// the boundary was disposed meanwhile. A read from a cleanup serves the
    /// previous value. A run started by a cleanup's read replaces this one,
    /// and a write after that read starts over, as on <c>Memo.Recompute</c>.
    /// </summary>
    member private this.Recompute() =
        if Platform.isNull (box scope) then
            this.Run ()
        else
            let before = runs
            freshness <- Freshness.Clean
            graph.Discharge scope

            if not disposed then
                if runs = before then
                    this.Run ()
                elif freshness = Freshness.Dirty then
                    this.Recompute ()
            elif freshness = Freshness.Clean then
                unannounced <- false


    member private _.RecordPending(source: INode) =
        if Platform.isNull (box pendingFirst) then
            pendingFirst <- source
        elif not (obj.ReferenceEquals (pendingFirst, source)) then
            pendingSecond <- source

    // Nothing to show and nothing to report: what this is, is waiting.
    // Keeps the previous value, which is all `Peek` ever promised.
    member private this.Suspend(source: INode) : 'T =
        graph.ClearRaised ()
        this.RecordPending source
        status <- Status.Pending
        value

    member private this.RecoverOrFail(ex: exn, last: 'T voption, previousFailure: Failure, previousCaught: Failure) : 'T =
        graph.ClearRaised ()

        match onError with
        | ValueSome recover ->
            // Taken before `recover` runs: a failed read inside it replaces the last failed read.
            let from = graph.FailureOf (ex, this, previousCaught)

            try
                let recovered = recover ex last
                graph.CheckRaised ()
                caught <- from
                recovered
            with
            | NotReadyException inner -> this.Suspend inner
            | rethrown ->
                graph.ClearRaised ()

                failure <-
                    if obj.ReferenceEquals (rethrown, ex) then
                        from
                    else
                        graph.FailureOf (rethrown, this, previousFailure)

                status <- Status.Error
                value

        | ValueNone ->
            failure <- graph.FailureOf (ex, this, previousFailure)
            status <- Status.Error
            value

    member private this.Run() =
        // Clean before the body runs, for the reason given on Memo.Recompute:
        // an invalidation raised while the body is running has to survive it.
        freshness <- Freshness.Clean
        unannounced <- false
        sources.BeginRun ()

        if not (Platform.isNull (box pendingFirst)) then
            pendingFirst <- Unchecked.defaultof<INode>

            if not (Platform.isNull (box pendingSecond)) then
                pendingSecond <- Unchecked.defaultof<INode>

        // Captured before the run overwrites them, to decide at the end whether
        // the observers told `Check` are owed a `Dirty`. `waiting` is part of
        // the comparison because a caught fallback can compute the same value
        // the body did, and a dependent asking `IsWaiting` would not see it
        // change. `caught` is compared for the same reason: a recover can
        // return the same value for a different exception.
        let previous = value
        let previousStatus = status
        let previousWaiting = waiting
        let previousFailure = failure
        let previousCaught = caught

        status <- Status.None
        failure <- null
        caught <- null
        waiting <- false
        runs <- runs + 1
        Tracer.RunStart (graph, id, runs)

        let last = if shown then ValueSome previous else ValueNone

        active <- 1

#if !FABLE_COMPILER
        let saved = graph.EnterHosted (this :> IComputation)
#endif

        try
#if FABLE_COMPILER
            value <-
                graph.RunHosted (
                    this :> IComputation,
                    fun () ->
                        try
                            let result = body ()
                            graph.CheckRaised ()
                            result
                        with
                        | NotReadyException source ->
                            graph.ClearRaised ()
                            this.RecordPending source

                            match onPending with
                            | ValueSome fallback ->
                                // Caught: the status stays clear, so nothing downstream
                                // ever sees Pending. The edge to `source` is already
                                // linked, which is what brings us back here when it
                                // settles.
                                try
                                    let substitute = fallback last
                                    graph.CheckRaised ()
                                    waiting <- true
                                    substitute
                                with
                                | NotReadyException inner -> this.Suspend inner
                                | ex -> this.RecoverOrFail (ex, last, previousFailure, previousCaught)

                            | ValueNone -> this.Suspend source

                        | ex -> this.RecoverOrFail (ex, last, previousFailure, previousCaught)
                )
#else
            value <-
                try
                    let result = body ()
                    graph.CheckRaised ()
                    result
                with
                | NotReadyException source ->
                    graph.ClearRaised ()
                    this.RecordPending source

                    match onPending with
                    | ValueSome fallback ->
                        // Caught: the status stays clear, so nothing downstream
                        // ever sees Pending. The edge to `source` is already
                        // linked, which is what brings us back here when it
                        // settles.
                        try
                            let substitute = fallback last
                            graph.CheckRaised ()
                            waiting <- true
                            substitute
                        with
                        | NotReadyException inner -> this.Suspend inner
                        | ex -> this.RecoverOrFail (ex, last, previousFailure, previousCaught)

                    | ValueNone -> this.Suspend source

                | ex -> this.RecoverOrFail (ex, last, previousFailure, previousCaught)
#endif
        finally
#if !FABLE_COMPILER
            graph.LeaveHosted saved
#endif
            active <- 0

            if disposed then
                sources.Clear (this :> IComputation)
            else
                sources.EndRun (this :> IComputation)

        // The cutoff. See `Memo.Run`. A throwing comparer fails the boundary
        // without reaching `recover`.
        let mutable moved =
            status <> previousStatus
            || waiting <> previousWaiting
            || Failure.Moved (failure, previousFailure)
            || Failure.Moved (caught, previousCaught)

        if not moved then
            try
                moved <- not (ComparerSentinel.equals equal previous value)
            with ex ->
                value <- previous
                failure <- graph.FailureOf (ex, this, previousFailure)
                caught <- null
                waiting <- false
                status <- Status.Error
                moved <- true

        if status = Status.None then
            shown <- true

        if moved then
            Tracer.Moved (graph, id, Failure.Payload (failure, box value))
            graph.NotifyMoved observers
            Tracer.Notified graph
            Tracer.RunEnd (graph, id, status)
            graph.OweFlush ()
        else
            Tracer.RunEnd (graph, id, status)

        if freshness <> Freshness.Clean then
            unannounced <- true
            graph.NoteStaleTransition ()

    member private this.EnsureCurrent() =
        // One comparison on the path a cached read takes. Testing `disposed`
        // or `Check` first costs every clean read an extra comparison, which
        // measured at ~1 ns against a ~1.3 ns baseline — the read is small
        // enough that one avoidable test is most of it.
        if freshness <> Freshness.Clean && not disposed then
            this.Pull ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.SettlePullAndRead() : 'T =
        let currentStatus = status
        let currentFailure = failure
        let currentValue = value
        graph.FinishPullSettle ()
        graph.Track (this :> ISource)

        if freshness <> Freshness.Clean && not disposed then
            graph.MarkReaderCheck ()

        if StatusBits.isUnready currentStatus then
            if StatusBits.isPending currentStatus then
                raise (graph.NotReady (this :> INode))

            graph.Raise currentFailure

        currentValue

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.SettlePullAndReadTry() : Reading<'T> =
        let currentStatus = status
        let currentFailure = failure
        let currentValue = value
        graph.FinishPullSettle ()
        graph.Track (this :> ISource)

        if freshness <> Freshness.Clean && not disposed then
            graph.MarkReaderCheck ()

        if StatusBits.isUnready currentStatus then
            if StatusBits.isPending currentStatus then
                Pending
            else
                Failed currentFailure.Error
        else
            Ready currentValue

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.SettlePullAndReadWaiting() : bool =
        let currentWaiting = waiting
        graph.FinishPullSettle ()
        graph.Track (this :> ISource)

        if freshness <> Freshness.Clean && not disposed then
            graph.MarkReaderCheck ()

        currentWaiting

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.SettlePullAndReadCaught() : Failure =
        let currentCaught = caught
        graph.FinishPullSettle ()
        graph.Track (this :> ISource)

        if freshness <> Freshness.Clean && not disposed then
            graph.MarkReaderCheck ()

        currentCaught

    /// <summary>
    /// Brings a stale boundary current.
    /// </summary>
    member private this.Pull() =
        graph.EnterPull ()

        try
            this.Refresh ()
        finally
            graph.ExitPull ()

    member private this.PullWithoutSettle() : bool =
        graph.EnterPull ()

        try
            this.Refresh ()
        with _ ->
            graph.ExitPull ()
            reraise ()

        graph.ExitPullDeferSettle ()
        || freshness <> Freshness.Clean

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CheckActiveSource(i: int) =
        if freshness = Freshness.Check then
            let source = sources.SourceAt i

            if source.IsStale && source.IsActive then
                (this :> IComputation).MarkDirty()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ResolveCheckAgain() =
        let mutable again = sources.HasStaleSource ()

        while again do
            let before = graph.StaleTransitions
            let mutable i = 0

            while freshness = Freshness.Check && i < sources.Count do
                sources.RefreshSourceAt i

                if graph.ActiveSkips <> 0 then
                    this.CheckActiveSource i

                i <- i + 1

            again <-
                freshness = Freshness.Check
                && graph.StaleTransitions <> before
                && sources.HasStaleSource ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RefreshActive() =
        graph.NoteActiveSkip ()

        if active = 2 then
            (this :> IComputation).MarkDirty()

    member private this.Refresh() =
        if active = 0 then
            if freshness = Freshness.Check then
                Tracer.CheckStart (graph, id)
                active <- 2
                let before = graph.StaleTransitions
                let mutable i = 0

                while freshness = Freshness.Check && i < sources.Count do
                    sources.RefreshSourceAt i

                    if graph.ActiveSkips <> 0 then
                        this.CheckActiveSource i

                    i <- i + 1

                if
                    freshness = Freshness.Check
                    && graph.StaleTransitions <> before
                then
                    this.ResolveCheckAgain ()

                active <- 0
                Tracer.CheckResolved (graph, id, (freshness = Freshness.Dirty))

                if freshness = Freshness.Check then
                    freshness <- Freshness.Clean
                    unannounced <- false

            if freshness = Freshness.Dirty then
                this.Recompute ()
        else
            this.RefreshActive ()

    /// <summary>
    /// Detaches from every source. Idempotent, and usually called for you by
    /// the enclosing owner. A disposed boundary keeps answering reads with the
    /// last value it produced — fallback included — and stops being woken.
    /// </summary>
    member this.Dispose() =
        graph.Entered (
            "Disposing a node",
            fun () ->
                if not disposed then
                    disposed <- true
                    Tracer.NodeDispose (graph, id)

                    if not (Platform.isNull link) then
                        link.Detach ()
                        link <- null

                    this.DetachSources ()

                    if not (Platform.isNull (box scope)) then
                        graph.Retire scope
        )

    /// <summary>
    /// Catches the pending channel. Errors still propagate — a failure is not a
    /// slow success, and conflating them is how a permanent error becomes a
    /// spinner that never stops.
    /// </summary>
    static member Suspense(graph: Graph, body: Func<'T>, fallback: Func<'T voption, 'T>) =
        Boundary<'T>.Create(graph, body.Invoke, ValueSome fallback.Invoke, ValueNone)

    /// <summary>
    /// Catches the error channel. Pending still propagates, so a boundary that
    /// reports failures does not also swallow the fact that something is in
    /// flight.
    /// </summary>
    static member Errors(graph: Graph, body: Func<'T>, recover: Func<exn, 'T voption, 'T>) =
        Boundary<'T>.Create(graph, body.Invoke, ValueNone, ValueSome (fun ex previous -> recover.Invoke (ex, previous)))

    /// <summary>
    /// Catches both.
    /// </summary>
    static member Catching(graph: Graph, body: Func<'T>, fallback: Func<'T voption, 'T>, recover: Func<exn, 'T voption, 'T>) =
        Boundary<'T>.Create(graph, body.Invoke, ValueSome fallback.Invoke, ValueSome (fun ex previous -> recover.Invoke (ex, previous)))

    /// <summary>
    /// <c>Suspense</c> whose <c>fallback</c> receives the boundary's last value, or <c>seed</c> before its first.
    /// </summary>
    static member Suspense(graph: Graph, body: Func<'T>, seed: 'T, fallback: Func<'T, 'T>) =
        Boundary<'T>.Create(graph, body.Invoke, ValueSome (fun previous -> fallback.Invoke (ValueOption.defaultValue seed previous)), ValueNone)

    /// <summary>
    /// <c>Errors</c> whose <c>recover</c> receives the error and the boundary's last value, or <c>seed</c> before its
    /// first.
    /// </summary>
    static member Errors(graph: Graph, body: Func<'T>, seed: 'T, recover: Func<exn, 'T, 'T>) =
        Boundary<'T>.Create(graph, body.Invoke, ValueNone, ValueSome (fun ex previous -> recover.Invoke (ex, ValueOption.defaultValue seed previous)))

    /// <summary>
    /// <c>Catching</c> whose handlers receive the boundary's last value, or <c>seed</c> before its first.
    /// </summary>
    static member Catching(graph: Graph, body: Func<'T>, seed: 'T, fallback: Func<'T, 'T>, recover: Func<exn, 'T, 'T>) =
        Boundary<'T>
            .Create(
                graph,
                body.Invoke,
                ValueSome (fun previous -> fallback.Invoke (ValueOption.defaultValue seed previous)),
                ValueSome (fun ex previous -> recover.Invoke (ex, ValueOption.defaultValue seed previous))
            )

    interface IOwned with
        member this.Release() =
            link <- null
            this.Dispose ()

    interface IScopeHost with
        member _.CanOwn = true

        member _.Scope =
            if Platform.isNull (box scope) then
                scope <- new Owner (graph.Root)
                Tracer.ScopeNew (scope, graph, id)

                if disposed then
                    scope.Dispose ()

            scope

#if RANVIER_TRACE
    interface ITracedNode with
        member _.TraceGraph = box graph
#endif

    interface INode with
        member _.Id = id
        member _.Status = status ||| Staleness.ofFreshness freshness

    interface ISource with
        member _.AddObserver(c, slot) =
            observers.Add (c, slot)

        member _.TryAddObserver(c, slot) =
            observers.TryAdd (c, slot)

        member _.RemoveObserverAt(c, handle) =
            observers.RemoveAt (c, handle)

        member this.UpdateIfNecessary() =
            this.EnsureCurrent ()

        member _.IsStale = freshness <> Freshness.Clean && not disposed

        member _.IsActive = active <> 0

    interface IComputation with
        member this.AddSource s =
            if not disposed then
                sources.Add (this :> IComputation, s)

        member _.MovedIn(slot, handle) =
            sources.MovedIn (slot, handle)

        member _.HasReadSlot slot =
            slot < sources.Cursor

        member _.MarkDirty() =
            if freshness <> Freshness.Dirty then
                let wasClean = freshness = Freshness.Clean
                freshness <- Freshness.Dirty

                // `Check`, for the reason given on `Memo.MarkDirty`: a
                // boundary that re-runs to the same value — the common case
                // while a fallback is standing in and an unrelated source
                // moves — has published nothing new.
                if wasClean then
                    graph.NoteStaleTransition ()
                    observers.NotifyCheck ()
                elif unannounced then
                    unannounced <- false
                    graph.NoteStaleTransition ()
                    observers.NotifyCheck ()
            elif unannounced then
                unannounced <- false
                graph.NoteStaleTransition ()
                observers.NotifyCheck ()

        member _.MarkCheck() =
            if freshness = Freshness.Clean then
                freshness <- Freshness.Check
                observers.NotifyCheck ()
            elif unannounced then
                unannounced <- false
                graph.NoteStaleTransition ()
                observers.NotifyCheck ()

    member _.Status = status

    /// <summary>True while the body is suspended and the fallback is standing in for it.</summary>
    /// <remarks>
    /// The only way to tell a fallback from a real value, since a caught channel is invisible to dependents by
    /// construction. A tracked read that brings the boundary current, as <c>TryValue</c> does.
    /// </remarks>
    member this.IsWaiting =
        if
            freshness <> Freshness.Clean
            && not disposed
            && this.PullWithoutSettle ()
        then
            this.SettlePullAndReadWaiting ()
        else
            graph.Track (this :> ISource)
            waiting

    /// <summary>The error the boundary recovered from on its current run, or null.</summary>
    /// <remarks>A tracked read that brings the boundary current, as <c>TryValue</c> does.</remarks>
    member this.Caught =
        if
            freshness <> Freshness.Clean
            && not disposed
            && this.PullWithoutSettle ()
        then
            Failure.ErrorOf (this.SettlePullAndReadCaught ())
        else
            graph.Track (this :> ISource)
            Failure.ErrorOf caught

    /// <summary>The node <c>Caught</c> originated in, or null when <c>Caught</c> is null.</summary>
    /// <remarks>
    /// A tracked read that brings the boundary current, as <c>Caught</c> does. The boundary itself when its body raised
    /// the exception; the upstream node when the body rethrew the exception of a failed read.
    /// </remarks>
    member this.CaughtFrom: INode =
        if
            freshness <> Freshness.Clean
            && not disposed
            && this.PullWithoutSettle ()
        then
            Failure.OriginOf (this.SettlePullAndReadCaught ())
        else
            graph.Track (this :> ISource)
            Failure.OriginOf caught

    /// <summary>
    /// The source of the last run's last pending read, whether or not a fallback caught it, and the source a fallback or
    /// recover suspended on; empty when the run did not suspend.
    /// </summary>
    member _.PendingSources =
        if Platform.isNull (box pendingFirst) then
            Seq.empty
        elif Platform.isNull (box pendingSecond) then
            [| pendingFirst |] :> seq<INode>
        else
            [| pendingFirst; pendingSecond |] :> seq<INode>

    /// <summary>
    /// How many times the body has run.
    /// </summary>
    member _.Runs = runs

    /// <summary>
    /// Transparent, tracked read. Raises <c>NotReadyException</c> while <c>Status</c> is Pending and the recorded
    /// error while it is Error.
    /// </summary>
    /// <remarks>
    /// A channel the boundary does not catch passes through. A caught channel reaches the reader when its handler
    /// suspends or throws.
    /// </remarks>
#if FABLE_COMPILER
    member this.Value: 'T =
        if StatusBits.isCleanAndReady freshness status then
            graph.Track (this :> ISource)
            value
        else
            this.ReadSlow ()
#else
    member this.Value: 'T = this.GetFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetFast() : 'T =
        if StatusBits.isCleanAndReady freshness status then
            graph.Track (this :> ISource)
            value
        else
            this.ReadSlow ()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ReadSlow() : 'T =
        if
            freshness <> Freshness.Clean
            && not disposed
            && this.PullWithoutSettle ()
        then
            this.SettlePullAndRead ()
        else
            graph.Track (this :> ISource)

            if StatusBits.isUnready status then
                if StatusBits.isPending status then
                    raise (graph.NotReady (this :> INode))

                graph.Raise failure

            value

    /// <summary>
    /// Non-throwing, tracked read.
    /// </summary>
#if FABLE_COMPILER
    member this.TryValue: Reading<'T> =
        if StatusBits.isCleanAndReady freshness status then
            graph.Track (this :> ISource)
            Ready value
        else
            this.ReadTrySlow ()
#else
    member this.TryValue
        with [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>] get () : Reading<'T> = this.GetTryFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetTryFast() : Reading<'T> =
        if StatusBits.isCleanAndReady freshness status then
            graph.Track (this :> ISource)
            Ready value
        else
            this.ReadTrySlow ()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ReadTrySlow() : Reading<'T> =
        if
            freshness <> Freshness.Clean
            && not disposed
            && this.PullWithoutSettle ()
        then
            this.SettlePullAndReadTry ()
        else
            graph.Track (this :> ISource)

            if StatusBits.isUnready status then
                if StatusBits.isPending status then
                    Pending
                else
                    Failed failure.Error
            else
                Ready value

    /// <summary>
    /// The node the current failure originated in while <c>Status</c> has <c>Error</c>, otherwise null. Untracked.
    /// </summary>
    /// <remarks>
    /// The boundary itself when its comparer raised the exception, or <c>recover</c> or a fallback raised a new one; the
    /// upstream node for an exception that passed through, including one rethrown by <c>recover</c>.
    /// </remarks>
    member _.ErrorOrigin: INode = Failure.OriginOf failure

    /// <summary>
    /// Untracked read of the cached value, without re-running the body.
    /// </summary>
    member _.Peek = value
