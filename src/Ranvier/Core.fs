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

/// <summary>Unwraps a public <c>Memo&lt;'T></c> handle to its underlying <c>MemoNode</c> <c>ISource</c>.</summary>
type internal IMemoHandle =
    abstract UnwrapMemoNode: unit -> ISource
#if !RANVIER_TRACE
    abstract OnOwnerDispose: unit -> unit
#endif

module internal MemoHandles =
#if FABLE_COMPILER
    let inline unwrap (source: ISource) : ISource =
        if Platform.hasMember source "UnwrapMemoNode" then
            (box source :?> IMemoHandle).UnwrapMemoNode()
        else
            source
#else
    let inline unwrap (source: ISource) : ISource =
        match box source with
        | :? IMemoHandle as h -> h.UnwrapMemoNode ()
        | _ -> source
#endif

#if !FABLE_COMPILER
/// <summary>Implemented by public <c>Effect</c> so <c>ObserverSet</c>/<c>Signal.WriteExcept</c> match <c>EffectNode</c> identity.</summary>
type internal IEffectHandle =
    abstract UnwrapEffectNode: unit -> IComputation

module internal EffectHandles =
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    let unwrap (observer: IComputation) : IComputation =
        match box observer with
        | :? IEffectHandle as h -> h.UnwrapEffectNode ()
        | _ -> observer
#endif

/// <summary>The kind of computation a <c>Node</c> belongs to, which selects its mark and run behaviour.</summary>
module internal NodeKind =
    [<Literal>]
    let Memo = 0uy

    [<Literal>]
    let Effect = 1uy

    [<Literal>]
    let EffectOn = 2uy

#if !RANVIER_TRACE
/// <summary>
/// Bit-packed flags stored in <c>Node.Flags</c> on untraced .NET and Fable (Theorem B8).
/// All bits live in 0..20 (positive 31-bit Smi on V8, zero sign-bit hazards on .NET).
/// </summary>
module internal NodeFlags =
    [<Literal>]
    let KindShift = 0

    [<Literal>]
    let KindMask = 0x3

    [<Literal>]
    let KindMemo = 0

    [<Literal>]
    let KindEffect = 1

    [<Literal>]
    let KindEffectOn = 2

    [<Literal>]
    let FreshShift = 2

    [<Literal>]
    let FreshMask = 0xC

    [<Literal>]
    let FreshClean = 0

    [<Literal>]
    let FreshCheck = 4

    [<Literal>]
    let FreshDirty = 8

    [<Literal>]
    let StatusShift = 4

    [<Literal>]
    let StatusMask = 0xF0

    [<Literal>]
    let StatusNone = 0

    [<Literal>]
    let StatusPending = 0x10

    [<Literal>]
    let StatusError = 0x20

    [<Literal>]
    let StatusUnreadyMask = 0x30

    [<Literal>]
    let StatusUninitialized = 0x40

    [<Literal>]
    let ActiveShift = 8

    [<Literal>]
    let ActiveMask = 0x300

    [<Literal>]
    let ActiveBody = 0x100

    [<Literal>]
    let ActiveCheck = 0x200

    [<Literal>]
    let Disposed = 0x00000400

    [<Literal>]
    let Unannounced = 0x00000800

    [<Literal>]
    let Queued = 0x00001000

    [<Literal>]
    let InCleanup = 0x00002000

    [<Literal>]
    let Publishes = 0x00004000

    [<Literal>]
    let Violated = 0x00008000

    [<Literal>]
    let Published = 0x00010000

    [<Literal>]
    let SafeEq = 0x00020000

    [<Literal>]
    let HasActed = 0x00040000

    [<Literal>]
    let Acting = 0x00080000

    [<Literal>]
    let IsAction = 0x00100000

    /// <summary>Initial flags for a MemoNode without safe equality: KindMemo ||| FreshDirty ||| StatusUninitialized.</summary>
    [<Literal>]
    let InitMemo = 0x00000048

    /// <summary>Initial flags for a MemoNode with safe equality: InitMemo ||| SafeEq.</summary>
    [<Literal>]
    let InitMemoSafeEq = 0x00020048

    /// <summary>Initial flags for an EffectNode: KindEffect ||| FreshClean ||| StatusUninitialized.</summary>
    [<Literal>]
    let InitEffect = 0x00000041

    /// <summary>Initial flags for an EffectOn: KindEffectOn ||| FreshClean ||| StatusUninitialized.</summary>
    [<Literal>]
    let InitEffectOn = 0x00000042

    /// <summary>Clean &amp; ready mask: FreshMask ||| StatusUnreadyMask.</summary>
    [<Literal>]
    let CleanAndReadyMask = 0x3C

    /// <summary>Clean live memo mask for NotifyDirtyIdle / NotifyCheckSpine: KindMask ||| FreshMask ||| Disposed.</summary>
    [<Literal>]
    let CleanMemoMask = 0x40F

    /// <summary>Clean idle live memo mask: KindMask ||| FreshMask ||| ActiveMask ||| Disposed.</summary>
    [<Literal>]
    let CleanMemoIdleMask = 0x70F

    /// <summary>Chain descent mask: FreshMask ||| ActiveMask ||| Disposed.</summary>
    [<Literal>]
    let ChainActiveDisposedMask = 0x70C

    /// <summary>Dirty-and-announced mask: FreshMask ||| Unannounced.</summary>
    [<Literal>]
    let DirtyAnnouncedMask = 0x80C

    /// <summary>Ready published safe-eq memo fast-path mask: StatusMask ||| ActiveMask ||| Disposed ||| Published ||| SafeEq.</summary>
    [<Literal>]
    let FastReadMemoMask = 0x307F0

    /// <summary>Expected value under <c>FastReadMemoMask</c>: Published ||| SafeEq (Status=None, Active=0, not Disposed).</summary>
    [<Literal>]
    let FastReadMemoValue = 0x30000

    /// <summary>RunFromNode ready published safe-eq mask: StatusMask ||| Published ||| SafeEq.</summary>
    [<Literal>]
    let FastRunMemoMask = 0x300F0

    /// <summary>Expected value under <c>FastRunMemoMask</c>: Published ||| SafeEq.</summary>
    [<Literal>]
    let FastRunMemoValue = 0x30000

    /// <summary>Idle effect fusion guard mask: KindMask ||| FreshMask ||| Disposed ||| InCleanup ||| Publishes ||| Queued.</summary>
    [<Literal>]
    let IdleEffectMask = 0x740F

    /// <summary>Idle effect direct-fusion mask: KindMask ||| FreshMask ||| StatusUnreadyMask ||| Disposed ||| InCleanup ||| Publishes ||| Queued.</summary>
    [<Literal>]
    let IdleEffectReadyMask = 0x743F

    /// <summary>Direct signal-to-effect fusion mask (any freshness): KindMask ||| Disposed ||| InCleanup ||| Publishes ||| Queued.</summary>
    [<Literal>]
    let LiveIdleEffectMask = 0x7403

    /// <summary>Mask for <c>EffectNode.RunFromNode</c>: StatusUnreadyMask ||| Disposed.</summary>
    [<Literal>]
    let EffectRunTopMask = 0x430

    /// <summary>Mask for <c>TryPromoteTop</c> on a memo: KindMask ||| FreshMask ||| Unannounced.</summary>
    [<Literal>]
    let PromoteMemoMask = 0x80F

    /// <summary>Mask for <c>TryPromoteTop</c> on an effect: FreshMask ||| Queued ||| Disposed ||| InCleanup.</summary>
    [<Literal>]
    let PromoteEffectMask = 0x340C

    /// <summary>Expected value under <c>PromoteEffectMask</c>: FreshCheck ||| Queued.</summary>
    [<Literal>]
    let PromoteEffectValue = 0x1004

    /// <summary>Fused check bits set on a downstream effect: Queued ||| FreshCheck.</summary>
    [<Literal>]
    let FusedCheckBits = 0x1004

    /// <summary>Fused check clear mask: Queued ||| FreshMask.</summary>
    [<Literal>]
    let FusedCheckClearMask = 0x100C

    /// <summary>Fused Signal.map leaf-Dirty mask: FreshMask ||| StatusMask ||| ActiveMask ||| Disposed ||| Violated ||| Published.</summary>
    [<Literal>]
    let FusedLeafDirtyMask = 0x187FC

    /// <summary>Expected value under <c>FusedLeafDirtyMask</c>: FreshDirty ||| Published (Status=None, Active=0, live, unviolated).</summary>
    [<Literal>]
    let FusedLeafDirtyValue = 0x10008

    /// <summary>Memo is claimed as an unread intermediate stage of a fused <c>Memo.map</c> chain.</summary>
    [<Literal>]
    let ClaimedFused = 0x00200000

    /// <summary>Memo uses a <c>FusedRunner&lt;'T&gt;</c> (<c>computeKind &gt;= 6uy</c>).</summary>
    [<Literal>]
    let FusedMap = 0x00400000
#endif

/// <summary>
/// Edge handle values that name no position, and the scan bound shared by both ends of an edge (Theorem B7).
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
    /// Bit 28 marks an edge handle returned by a <c>MemoNode</c> so check walks cast the source directly to <c>MemoNode</c>.
    /// </summary>
    [<Literal>]
    let MemoBit = 0x10000000

    /// <summary>
    /// Bit 29 marks an edge handle returned by a <c>Signal</c> so <c>ResolveCheck</c> skips <c>UpdateIfNecessary</c>.
    /// Keeping tags within bits 28–29 preserves 31-bit signed small integers on pointer-compressed V8.
    /// </summary>
    [<Literal>]
    let SignalBit = 0x20000000

    /// <summary>Tag bits preserved across swap-remove <c>MovedIn</c> notifications (<c>SignalBit ||| MemoBit</c>).</summary>
    [<Literal>]
    let TagMask = 0x30000000

    /// <summary>Masks off <c>SignalBit</c> and <c>MemoBit</c> to recover the observer-list slot index.</summary>
    [<Literal>]
    let IndexMask = 0x0FFFFFFF

    /// <summary>
    /// Bit 29 on an observer's slot marks that the observer reference is a <c>Node</c> (not a foreign <c>IComputation</c>).
    /// Fits in a positive 31-bit signed V8 Smi (<c>0x20000000 .. 0x3FFFFFFF</c>).
    /// </summary>
    [<Literal>]
    let NodeSlotBit = 0x20000000

    /// <summary>Masks off <c>NodeSlotBit</c> to recover the observer's source slot index.</summary>
    [<Literal>]
    let SlotMask = 0x1FFFFFFF

    /// <summary>True when <c>handle</c> names an edge owned by a <c>Signal</c>.</summary>
    let inline isSignal (handle: int) =
        handle >= SignalBit

    /// <summary>True when <c>handle</c> names an edge owned by a <c>MemoNode</c> (false for <c>Unlinked</c>, <c>Unknown</c>, foreign sources and signals).</summary>
    let inline isMemo (handle: int) =
        handle >= MemoBit && handle < SignalBit

    /// <summary>True when <c>slot</c> carries <c>NodeSlotBit</c> (false for <c>Unlinked</c> and foreign computations).</summary>
    let inline isNodeSlot (slot: int) =
        slot >= NodeSlotBit

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
    [<Fable.Core.Emit("(($0 | ($1 & 3)) === 0)")>]
    let isCleanAndReady (freshness: Freshness) (status: Status) : bool =
        Fable.Core.Util.jsNative
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

    /// <summary>
    /// The zero-length observer reference list an observer list holds until its second observer.
    /// </summary>
    let noRefs: obj[] = [||]
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
    static member inline Create() =
        ObserverSet ()

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

        if handle > EdgeHandle.IndexMask then
            raise (InvalidOperationException "Observer handle capacity exceeded.")

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

            if handle = last then
                if last = 0 then
                    // Blanked so a dropped observer is not kept alive after removal.
                    first <- Platform.nil ()
                    firstSlot <- EdgeHandle.Unlinked
                else
                    let lastRest = last - 1
#if FABLE_COMPILER
                    Platform.setItemAt lastRest rest (ObserverSlot (Platform.nil ()))
                    Platform.setItemAt lastRest restSlots EdgeHandle.Unlinked
#else
                    rest[lastRest] <- ObserverSlot (Platform.nil (), EdgeHandle.Unlinked)
#endif

                count <- last
            elif handle = 0 then
                let lastRest = last - 1
                let moved = (Platform.itemAt lastRest rest).Observer
#if FABLE_COMPILER
                let movedSlot = Platform.itemAt lastRest restSlots
                Platform.setItemAt lastRest rest (ObserverSlot (Platform.nil ()))
                Platform.setItemAt lastRest restSlots EdgeHandle.Unlinked
#else
                let movedSlot = (Platform.itemAt lastRest rest).Slot
                rest[lastRest] <- ObserverSlot (Platform.nil (), EdgeHandle.Unlinked)
#endif
                first <- moved
                firstSlot <- movedSlot
                count <- last

                if latest = last then
                    latest <- 0

                moved.MovedIn (movedSlot, 0)
            else
                let handleRest = handle - 1
                let lastRest = last - 1
                let moved = (Platform.itemAt lastRest rest).Observer
#if FABLE_COMPILER
                let movedSlot = Platform.itemAt lastRest restSlots
                Platform.setItemAt handleRest rest (ObserverSlot moved)
                Platform.setItemAt handleRest restSlots movedSlot
                Platform.setItemAt lastRest rest (ObserverSlot (Platform.nil ()))
                Platform.setItemAt lastRest restSlots EdgeHandle.Unlinked
#else
                let movedSlot = (Platform.itemAt lastRest rest).Slot
                rest[handleRest] <- ObserverSlot (moved, movedSlot)
                rest[lastRest] <- ObserverSlot (Platform.nil (), EdgeHandle.Unlinked)
#endif
                count <- last

                if latest = last then
                    latest <- handle

                moved.MovedIn (movedSlot, handle)

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
#if !FABLE_COMPILER
        let running = EffectHandles.unwrap running
#endif
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
#if !FABLE_COMPILER
        let running = EffectHandles.unwrap running
#endif
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
            this.Handle <-
                (this.Handle &&& EdgeHandle.TagMask)
                ||| (handle &&& EdgeHandle.IndexMask)

        member _.HasReadSlot _ = false

// Untraced, the observers of a signal or a node are an `ObserverList` declared with `Graph`: each entry carries the
// observer's `Node`, in a struct held inline by its owner on .NET and in a class under Fable.
#if !RANVIER_TRACE
#else
/// <summary>
/// The observers of a signal or a node when traced: the observer set itself, whose events the trace needs.
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

    new(source: ISource, handle: int) = { Source = source; Handle = handle }
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
#if !RANVIER_TRACE
// Untraced, positions 0 and 1 are inline fields: a 48 B struct held inline by its owner on .NET (refs before ints),
// and a class with 2 overflow arrays under Fable (no Node shadow fields — MemoBit on Handle identifies MemoNode).
#if FABLE_COMPILER
[<Sealed; NoEquality; NoComparison>]
type internal SourceList =
    val mutable internal first: ISource
    val mutable internal second: ISource
    val mutable internal rest: SourceSlot[]
    val mutable internal restHandles: int[]
    val mutable internal held: Platform.RefIndex<ISource>
    val mutable internal firstHandle: int
    val mutable internal secondHandle: int
    val mutable internal count: int
    val mutable internal cursor: int

    internal new(unlinked: int) =
        {
            first = Platform.nil ()
            second = Platform.nil ()
            rest = SourceSlots.none
            restHandles = SourceSlots.noHandles
            held = Platform.nil ()
            firstHandle = unlinked
            secondHandle = unlinked
            count = 0
            cursor = 0
        }
#else
[<Struct; NoEquality; NoComparison>]
type internal SourceList =
    val mutable internal first: ISource
    val mutable internal second: ISource
    val mutable internal rest: SourceSlot[]
    val mutable internal held: Platform.RefIndex<ISource>
    val mutable internal firstHandle: int
    val mutable internal secondHandle: int
    val mutable internal count: int
    val mutable internal cursor: int

    internal new(unlinked: int) =
        {
            first = Platform.nil ()
            second = Platform.nil ()
            rest = null
            held = null
            firstHandle = unlinked
            secondHandle = unlinked
            count = 0
            cursor = 0
        }
#endif

    static member inline Create() =
        SourceList EdgeHandle.Unlinked

    member private this.SourceAtRaw(i: int) =
        if i = 0 then this.first
        elif i = 1 then this.second
        else (Platform.itemAt (i - 2) this.rest).Source

    member private this.HandleAtRaw(i: int) =
        if i = 0 then
            this.firstHandle
        elif i = 1 then
            this.secondHandle
        else
#if FABLE_COMPILER
            Platform.itemAt (i - 2) this.restHandles
#else
            (Platform.itemAt (i - 2) this.rest).Handle
#endif

    member private this.SetHandle(i: int, handle: int) =
        if i = 0 then
            this.firstHandle <- handle
        elif i = 1 then
            this.secondHandle <- handle
        else
#if FABLE_COMPILER
            Platform.setItemAt (i - 2) this.restHandles handle
#else
            this.rest[i - 2].Handle <- handle
#endif

    member private this.Grow() =
#if FABLE_COMPILER
        let used = this.rest.Length
        let size = max 4 (used * 2)
        let grownSources = Array.zeroCreate size
        let grownHandles = Platform.positions size
        Array.blit this.rest 0 grownSources 0 used
        Platform.copyPositions this.restHandles grownHandles used
        this.rest <- grownSources
        this.restHandles <- grownHandles
#else
        if Platform.isNull this.rest then
            this.rest <- Array.zeroCreate 4
        else
            let size = this.rest.Length * 2
            let grown = Array.zeroCreate size
            Array.blit this.rest 0 grown 0 this.rest.Length
            this.rest <- grown
#endif

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
        let count = this.count

        if from < count then
            if not (Platform.isNull this.held) then
                this.held <- Platform.nil ()

            // Specialize the common 1-slot, 2-slot, and last-slot removals without loop overhead.
            if from = count - 1 then
                if from = 0 then
                    if this.firstHandle >= 0 then
                        this.first.RemoveObserverAt (self, this.firstHandle)

                    this.first <- Platform.nil ()
                    this.firstHandle <- EdgeHandle.Unlinked
                elif from = 1 then
                    if this.secondHandle >= 0 then
                        this.second.RemoveObserverAt (self, this.secondHandle)

                    this.second <- Platform.nil ()
                    this.secondHandle <- EdgeHandle.Unlinked
                else
                    let k = from - 2
#if FABLE_COMPILER
                    let handle = Platform.itemAt k this.restHandles

                    if handle >= 0 then
                        (Platform.itemAt k this.rest).Source.RemoveObserverAt(self, handle)

                    Platform.setItemAt k this.rest (SourceSlot (Platform.nil ()))
                    Platform.setItemAt k this.restHandles EdgeHandle.Unlinked
#else
                    let slot = Platform.itemAt k this.rest

                    if slot.Handle >= 0 then
                        slot.Source.RemoveObserverAt (self, slot.Handle)

                    this.rest[k] <- SourceSlot (Platform.nil (), EdgeHandle.Unlinked)
#endif
#if RANVIER_COUNTERS
                Counters.EdgeRemoved ()
#endif
            elif from = 0 && count = 2 then
                if this.firstHandle >= 0 then
                    this.first.RemoveObserverAt (self, this.firstHandle)

                this.first <- Platform.nil ()
                this.firstHandle <- EdgeHandle.Unlinked

                if this.secondHandle >= 0 then
                    this.second.RemoveObserverAt (self, this.secondHandle)

                this.second <- Platform.nil ()
                this.secondHandle <- EdgeHandle.Unlinked
#if RANVIER_COUNTERS
                Counters.EdgeRemoved ()
                Counters.EdgeRemoved ()
#endif
            else
                // Each handle is read when its slot is reached: a removal can move this list's later edges.
                for i in from .. count - 1 do
                    if i = 0 then
                        if this.firstHandle >= 0 then
                            this.first.RemoveObserverAt (self, this.firstHandle)

                        this.first <- Platform.nil ()
                        this.firstHandle <- EdgeHandle.Unlinked
                    elif i = 1 then
                        if this.secondHandle >= 0 then
                            this.second.RemoveObserverAt (self, this.secondHandle)

                        this.second <- Platform.nil ()
                        this.secondHandle <- EdgeHandle.Unlinked
                    else
                        let k = i - 2
#if FABLE_COMPILER
                        let handle = Platform.itemAt k this.restHandles

                        if handle >= 0 then
                            (Platform.itemAt k this.rest).Source.RemoveObserverAt(self, handle)

                        Platform.setItemAt k this.rest (SourceSlot (Platform.nil ()))
                        Platform.setItemAt k this.restHandles EdgeHandle.Unlinked
#else
                        let slot = Platform.itemAt k this.rest

                        if slot.Handle >= 0 then
                            slot.Source.RemoveObserverAt (self, slot.Handle)

                        this.rest[k] <- SourceSlot (Platform.nil (), EdgeHandle.Unlinked)
#endif
#if RANVIER_COUNTERS
                    Counters.EdgeRemoved ()
#endif

            this.count <- from

#if FABLE_COMPILER
    member inline this.Count = this.count
    member inline this.Cursor = this.cursor

    member inline this.SetCursor(pos: int) =
        this.cursor <- pos
#else
    member this.Count = this.count
    member this.Cursor = this.cursor

    member inline this.SetCursor(pos: int) =
        this.cursor <- pos
#endif

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

#if FABLE_COMPILER
            while i < limit
                  && (Platform.itemAt i this.restHandles
                      >= EdgeHandle.SignalBit
                      || not (Platform.itemAt i this.rest).Source.IsStale) do
                i <- i + 1
#else
            while i < limit
                  && (let s = Platform.itemAt i this.rest

                      s.Handle >= EdgeHandle.SignalBit
                      || not s.Source.IsStale) do
                i <- i + 1
#endif

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
#if FABLE_COMPILER
            let h = Platform.itemAt (i - 2) this.restHandles

            if h < EdgeHandle.SignalBit then
                (Platform.itemAt (i - 2) this.rest).Source.UpdateIfNecessary()
#else
            let s = Platform.itemAt (i - 2) this.rest

            if s.Handle < EdgeHandle.SignalBit then
                s.Source.UpdateIfNecessary ()
#endif

    /// <summary>Stores the handle a source's swap-remove gave the edge at <c>slot</c>, preserving its source-kind tag.</summary>
    member this.MovedIn(slot: int, handle: int) =
        this.SetHandle (
            slot,
            (this.HandleAtRaw slot &&& EdgeHandle.TagMask)
            ||| (handle &&& EdgeHandle.IndexMask)
        )

    /// <summary>Opens a run without detaching existing edges so matching reads reuse them.</summary>
#if FABLE_COMPILER
    member inline this.BeginRun() =
        this.cursor <- 0
#else
    member this.BeginRun() =
        this.cursor <- 0
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member internal this.Append(self: IComputation, source: ISource, slotTag: int) =
        if this.cursor < this.count then
            this.TrimFrom (self, this.cursor)

        let slot = this.count
        this.Link (self, source, slot, slotTag)

    /// <summary>Appends <c>source</c> at <c>slot</c> and links its edge using bit-tagged <c>slot ||| slotTag</c>.</summary>
    member private this.Link(self: IComputation, source: ISource, slot: int, slotTag: int) =
        if slot > EdgeHandle.SlotMask then
            raise (InvalidOperationException "Source slot capacity exceeded.")

        let taggedSlot = slot ||| slotTag

        if slot = 0 then
            let handle = source.AddObserver (self, taggedSlot)
            this.first <- source
            this.firstHandle <- handle
            this.count <- 1
            this.cursor <- 1
#if RANVIER_COUNTERS
            Counters.EdgeAdded ()
#endif
        elif slot = 1 then
            let tried = source.TryAddObserver (self, taggedSlot)

            let handle =
                if tried <> EdgeHandle.Unknown then
                    tried
                elif obj.ReferenceEquals (this.first, source) then
                    EdgeHandle.Unlinked
                else
                    source.AddObserver (self, taggedSlot)

            this.second <- source
            this.secondHandle <- handle
            this.count <- 2
            this.cursor <- 2
#if RANVIER_COUNTERS
            Counters.EdgeAdded ()
#endif
            if not (Platform.isNull this.held) then
                Platform.refIndexSet this.held source 1
        else
            // A source already in the list keeps the one edge it has: A, B, A links A once.
            let tried = source.TryAddObserver (self, taggedSlot)

            let handle =
                if tried <> EdgeHandle.Unknown then tried
                elif this.Holds (slot, source) then EdgeHandle.Unlinked
                else source.AddObserver (self, taggedSlot)

            let k = slot - 2

#if FABLE_COMPILER
            if k = this.rest.Length then
                this.Grow ()

            Platform.setItemAt k this.rest (SourceSlot source)
            Platform.setItemAt k this.restHandles handle
#else
            if Platform.isNull this.rest || k = this.rest.Length then
                this.Grow ()

            this.rest[k] <- SourceSlot (source, handle)
#endif
            this.count <- slot + 1
            this.cursor <- this.count
#if RANVIER_COUNTERS
            Counters.EdgeAdded ()
#endif
            if not (Platform.isNull this.held) then
                Platform.refIndexSet this.held source slot

    /// <summary>
    /// Records a read of <c>source</c> by <c>self</c> with <c>slotTag</c> (<c>EdgeHandle.NodeSlotBit</c> for a
    /// <c>Node</c>, <c>0</c> for a foreign computation): a slot hit advances the cursor, and a miss links a tagged edge.
    /// </summary>
#if FABLE_COMPILER
    member inline this.AddTagged(self: IComputation, source: ISource, slotTag: int) =
#else
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member this.AddTagged(self: IComputation, source: ISource, slotTag: int) =
#endif
        let cur = this.cursor

        if cur = 0 then
            if obj.ReferenceEquals (this.first, source) then
                this.cursor <- 1
            else
                this.Append (self, source, slotTag)
        elif cur = 1 then
            if obj.ReferenceEquals (this.second, source) then
                this.cursor <- 2
            elif obj.ReferenceEquals (this.first, source) then
                ()
            else
                this.Append (self, source, slotTag)
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
            this.Append (self, source, slotTag)

#if FABLE_COMPILER
    member inline this.Add(self: IComputation, source: ISource) =
        this.AddTagged (self, source, 0)
#else
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member this.Add(self: IComputation, source: ISource) =
        this.AddTagged (self, source, 0)
#endif

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
    /// Stores the handle a source's swap-remove gave the edge at <c>slot</c>, preserving its source-kind tag.
    /// </summary>
    member _.MovedIn(slot: int, handle: int) =
        setHandle
            slot
            ((handleAt slot &&& EdgeHandle.TagMask)
             ||| (handle &&& EdgeHandle.IndexMask))

    /// <summary>
    /// Opens a run. Deliberately does not detach: the edges stay live so the
    /// ones that are read again can be recognised.
    /// </summary>
    member _.BeginRun() =
        cursor <- 0

    member inline _.SetCursor(pos: int) =
        cursor <- pos

    /// <summary>
    /// Appends <c>source</c> after a read that missed its slot, trimming the unmatched slots first. Kept out of
    /// <c>Add</c>, whose matching path runs on every tracked read.
    /// </summary>
    member private this.Append(self: IComputation, source: ISource) =
        this.TrimAndLink (self, source)

    member private this.TrimAndLink(self: IComputation, source: ISource) =
        if cursor < count then
            Tracer.EdgesRemove (this, cursor)
            trimFrom self cursor

        let slot = count

        if slot > EdgeHandle.SlotMask then
            raise (InvalidOperationException "Source slot capacity exceeded.")

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
/// One child's place in its owner's list. Plain fields avoid accessor dispatch under Fable; built-in <c>Node</c>
/// types inherit this directly so attaching them allocates no separate link object.
/// </summary>
#if FABLE_COMPILER
[<AbstractClass; AllowNullLiteral>]
type internal OwnerLink(owner: Owner, generation: int, acquired: int) =
    let mutable owner = owner
    let mutable prev: OwnerLink = Platform.nil ()
    let mutable next: OwnerLink = Platform.nil ()
    let mutable generation = generation
    let mutable acquired = acquired

    member inline _.Owner
        with get () = owner
        and set v = owner <- v

    member inline _.Prev
        with get () = prev
        and set v = prev <- v

    member inline _.Next
        with get () = next
        and set v = next <- v

    member inline _.Generation
        with get () = generation
        and set v = generation <- v

    member inline _.Acquired
        with get () = acquired
        and set v = acquired <- v

    abstract ReleaseChild: unit -> unit

    member this.Detach() =
        let o = owner

        if not (Platform.isNull (box o)) then
            owner <- Platform.nil ()
            o.Unlink this

/// <summary>Standalone owner link for non-<c>Node</c> <c>IOwned</c> children.</summary>
and [<Sealed>] internal ForeignOwnerLink(child: IOwned, owner: Owner, generation: int, acquired: int) =
    inherit OwnerLink(owner, generation, acquired)

    override _.ReleaseChild() =
        child.Release ()
#else
[<AbstractClass; AllowNullLiteral>]
type internal OwnerLink =
    val mutable Owner: Owner
    val mutable Prev: OwnerLink
    val mutable Next: OwnerLink

    /// <summary>
    /// The owner's list generation the link joined, or -1 once the link has left the list.
    /// </summary>
    /// <remarks>
    /// A link at -1 is ignored by a later unlink, which would otherwise null out the owner's head.
    /// </remarks>
    val mutable Generation: int

    /// <summary>
    /// The owner's acquisition count when the link joined.
    /// </summary>
    val mutable Acquired: int

    internal new(owner: Owner, generation: int, acquired: int) =
        {
            Owner = owner
            Prev = Platform.nil ()
            Next = Platform.nil ()
            Generation = generation
            Acquired = acquired
        }

    abstract ReleaseChild: unit -> unit

    member this.Detach() =
        let owner = this.Owner

        if not (Platform.isNull (box owner)) then
            this.Owner <- Platform.nil ()
            owner.Unlink this

/// <summary>Standalone owner link for non-<c>Node</c> <c>IOwned</c> children.</summary>
and [<Sealed>] internal ForeignOwnerLink =
    inherit OwnerLink
    val Child: IOwned

    internal new(child: IOwned, owner: Owner, generation: int, acquired: int) =
        {
            inherit OwnerLink(owner, generation, acquired)
            Child = child
        }

    override this.ReleaseChild() =
        this.Child.Release ()
#endif

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
    let mutable head: OwnerLink = Platform.nil ()
    let mutable tail: OwnerLink = Platform.nil ()

    /// <summary>
    /// The earliest cleanup registered since the last teardown, or null.
    /// </summary>
    let mutable firstCleanup: unit -> unit = Platform.nil ()

    /// <summary>
    /// The acquisition count of <c>firstCleanup</c>.
    /// </summary>
    let mutable firstOrder = 0

    /// <summary>
    /// The cleanups registered after <c>firstCleanup</c>. Allocated by the second <c>OnCleanup</c>; null while a
    /// teardown runs the cleanups it detached.
    /// </summary>
#if FABLE_COMPILER
    let mutable cleanups: ResizeArray<unit -> unit> = Platform.nil ()

    /// <summary>
    /// The acquisition count of each entry in <c>cleanups</c>.
    /// </summary>
    let mutable cleanupOrder: ResizeArray<int> = Platform.nil ()
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
    let mutable errors: ResizeArray<exn> = Platform.nil ()
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
    let mutable parentLink: OwnerLink = Platform.nil ()

#if RANVIER_TRACE
    let mutable traceLog: TraceLog = null
    let mutable traceId = 0
#endif

#if RANVIER_COUNTERS
    do Counters.OwnerCreated ()
#endif

    new() = new Owner (Platform.nil ())

    member _.IsDisposed = disposed

    member internal this.AttachToParent(parent: Owner) =
        let link = parent.AttachLinked this
        Tracer.OwnerAdopt (parent, this, (this :? RootScope))
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

    /// <summary>Attaches an embedded <c>OwnerLink</c> (such as a <c>Node</c>) directly with zero link allocation.</summary>
    member internal this.AttachLink(link: OwnerLink) =
        if disposed then
            link.Generation <- -1
            this.DisposeLateLink link
            link.Owner <- Platform.nil ()
        else
            link.Owner <- this
            link.Generation <- generation
            link.Acquired <- acquisitions
            acquisitions <- acquisitions + 1

            if Platform.isNull tail then
                head <- link
                tail <- link
            else
                link.Prev <- tail
                tail.Next <- link
                tail <- link

    member internal this.AttachLinked(child: IOwned) : OwnerLink =
        if disposed then
            this.DisposeLate child
        else
            this.Append child

    /// <summary>
    /// Disposes a child attached after the scope was disposed, handing back
    /// an unlinked link.
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.DisposeLate(child: IOwned) : OwnerLink =
        this.RunLate child.Release
        Platform.nil ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.DisposeLateLink(link: OwnerLink) =
        this.RunLate (fun () -> link.ReleaseChild ())

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

    member private this.Append(child: IOwned) : OwnerLink =
        let link = ForeignOwnerLink (child, this, generation, acquisitions)
        acquisitions <- acquisitions + 1

        if Platform.isNull tail then
            head <- link
            tail <- link
        else
            link.Prev <- tail
            tail.Next <- link
            tail <- link

        link :> OwnerLink

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

            link.Prev <- Platform.nil ()
            link.Next <- Platform.nil ()
            link.Owner <- Platform.nil ()

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
#if FABLE_COMPILER
        this.AddCleanup (unbox<unit -> unit> f)
#else
        this.AddCleanup f.Invoke
#endif

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
        head <- Platform.nil ()
        tail <- Platform.nil ()
        generation <- (generation + 1) &&& 0x7FFFFFFF
        acquisitions <- 0
        firstCleanup <- Platform.nil ()
        cleanups <- Platform.nil ()
#if FABLE_COMPILER
        cleanupOrder <- Platform.nil ()
#endif

        // Index 0 is `first` when it is set; the rest index `detached` from `offset`.
        let offset = if Platform.isNull (box first) then 0 else 1

        let mutable index =
            offset
            + (if Platform.isNull detached then 0 else detached.Count)
            - 1

        if index < 0 then
            // Fast path: no OnCleanup callbacks on this scope; any cleanup registered reentrantly during ReleaseChild
            // lands in the emptied scope, leaving index < 0 invariant throughout the walk.
            while not (Platform.isNull node) do
                let prev = node.Prev
                node.Generation <- -1
                node.Owner <- Platform.nil ()
                node.Prev <- Platform.nil ()
                node.Next <- Platform.nil ()

                try
                    node.ReleaseChild ()
                with ex ->
                    this.RecordError ex

                node <- prev
        else
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
                    node.Generation <- -1
                    node.Owner <- Platform.nil ()
                    node.Prev <- Platform.nil ()
                    node.Next <- Platform.nil ()

                    try
                        node.ReleaseChild ()
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
            | _ -> this.TearDown (Platform.nil ())

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
            runner.RunDetachedDisposeScope this

        if not (Platform.isNull parentLink) then
            parentLink.Detach ()
            parentLink <- Platform.nil ()

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

    interface IOwned with
        member this.Release() =
            parentLink <- Platform.nil ()
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
    /// Closure-free <c>RunDetached</c> specialized for <c>owner.DisposeScope()</c>.
    /// </summary>
    abstract RunDetachedDisposeScope: owner: Owner -> unit

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
    inherit Owner(Platform.nil ())

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
/// The running graph of one thread. Lazy-epoch tracking (Theorem B10): <c>Clock</c> increments only in
/// <c>Graph.Activate</c>, so steady-state <c>EnterFrame</c>/<c>LeaveFrame</c> touch only <c>HostDepth</c>.
/// Under Fable, all epoch fields are 31-bit Smi ints in plain arrays so V8 never allocates heap numbers.
/// </summary>
and [<Sealed; AllowNullLiteral>] internal AmbientCell =
    /// <summary>
    /// The graph hosted on this thread while <c>HostDepth &gt; 0</c>, or a cached guarded graph while idle.
    /// </summary>
    val mutable Hosting: obj
    val mutable HostDepth: int
    val mutable ShadowCount: int
    val mutable EpochDepth: int
    val mutable private epochTop: int

#if FABLE_COMPILER
    val mutable Clock: float
    val mutable HostEpoch: float
    val mutable private epochDepths: int[]
    val mutable private epochClocks: float[]

    new() =
        {
            Hosting = Platform.nil ()
            HostDepth = 0
            ShadowCount = 0
            EpochDepth = -1
            epochTop = 0
            Clock = 0.
            HostEpoch = 0.
            epochDepths = Platform.nil ()
            epochClocks = Platform.nil ()
        }

    member internal cell.PushEpoch(depth: int) =
        if depth = 0 then
            cell.HostEpoch <- cell.Clock
            cell.EpochDepth <- -1
            cell.HostDepth <- 1
        else
            let top = cell.epochTop
            let mutable depths = cell.epochDepths
            let mutable clocks = cell.epochClocks

            if Platform.isNull depths then
                depths <- Platform.positions 8
                clocks <- Array.zeroCreate 8
                cell.epochDepths <- depths
                cell.epochClocks <- clocks
            elif top = depths.Length then
                let grownDepths = Platform.positions (depths.Length * 2)
                let grownClocks = Array.zeroCreate (clocks.Length * 2)
                Platform.copyPositions depths grownDepths depths.Length
                Array.blit clocks 0 grownClocks 0 clocks.Length
                depths <- grownDepths
                clocks <- grownClocks
                cell.epochDepths <- depths
                cell.epochClocks <- clocks

            Platform.setItemAt top depths cell.EpochDepth
            Platform.setItemAt top clocks cell.HostEpoch
            cell.epochTop <- top + 1
            cell.EpochDepth <- depth
            cell.HostEpoch <- cell.Clock
            cell.HostDepth <- depth + 1

    member internal cell.PopEpoch() =
        let top = cell.epochTop - 1
        cell.epochTop <- top
        cell.EpochDepth <- Platform.itemAt top cell.epochDepths
        cell.HostEpoch <- Platform.itemAt top cell.epochClocks

    member inline internal cell.EnterFrame() =
        let depth = cell.HostDepth

        if cell.HostEpoch = cell.Clock then
            cell.HostDepth <- depth + 1
        else
            cell.PushEpoch depth

    member inline internal cell.LeaveFrame() : int =
        let next = cell.HostDepth - 1
        cell.HostDepth <- next

        if next = cell.EpochDepth then
            cell.PopEpoch ()

        next
#else
    val mutable Clock: int64
    val mutable HostEpoch: int64
    val mutable private epochStack: struct (int * int64)[]
    val ThreadId: int

    new() =
        {
            Hosting = null
            HostDepth = 0
            ShadowCount = 0
            EpochDepth = -1
            epochTop = 0
            Clock = 0L
            HostEpoch = 0L
            epochStack = null
            ThreadId = Platform.currentThreadId ()
        }

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member internal cell.PushEpoch(depth: int) =
        if depth = 0 then
            cell.HostEpoch <- cell.Clock
            cell.EpochDepth <- -1
            cell.HostDepth <- 1
        else
            let top = cell.epochTop
            let mutable stack = cell.epochStack

            if Platform.isNull stack then
                stack <- Array.zeroCreate<struct (int * int64)> 8
                cell.epochStack <- stack
            elif top = stack.Length then
                let grown = Array.zeroCreate<struct (int * int64)>(stack.Length * 2)
                Array.blit stack 0 grown 0 stack.Length
                stack <- grown
                cell.epochStack <- stack

            stack[top] <- struct (cell.EpochDepth, cell.HostEpoch)
            cell.epochTop <- top + 1
            cell.EpochDepth <- depth
            cell.HostEpoch <- cell.Clock
            cell.HostDepth <- depth + 1

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member internal cell.PopEpoch() =
        let top = cell.epochTop - 1
        cell.epochTop <- top
        let struct (prevDepth, prevEpoch) = cell.epochStack[top]
        cell.EpochDepth <- prevDepth
        cell.HostEpoch <- prevEpoch

    member inline internal cell.EnterFrame() =
        let depth = cell.HostDepth

        if cell.HostEpoch = cell.Clock then
            cell.HostDepth <- depth + 1
        else
            cell.PushEpoch depth

    member inline internal cell.LeaveFrame() : int =
        let next = cell.HostDepth - 1
        cell.HostDepth <- next

        if next = cell.EpochDepth then
            cell.PopEpoch ()

        next
#endif

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
#if FABLE_COMPILER
[<Sealed; AllowNullLiteral>]
type internal ActivationEntry(graph: obj, thread: int, anyThread: bool, previous: obj, cell: AmbientCell, clock: float, shadowed: bool) =
    let mutable disposed = false
#else
[<Sealed; AllowNullLiteral>]
type internal ActivationEntry(graph: obj, thread: int, anyThread: bool, previous: obj, cell: AmbientCell, clock: int64, shadowed: bool) =
    let mutable disposed = 0
#endif

    member _.Cell = cell
    member _.Clock = clock
    member _.Previous = previous
#if FABLE_COMPILER
    member _.IsDisposed = disposed
#else
    member _.IsDisposed = Volatile.Read &disposed <> 0
#endif

    /// <summary>
    /// The graph, when ambient on the calling thread, otherwise null.
    /// </summary>
    member _.GraphOnCurrentThread =
        if anyThread || thread = Platform.currentThreadId () then
            graph
        else
            null

    static member internal FirstLive(cur: obj) : ActivationEntry =
        let mutable c = cur
        let mutable found: ActivationEntry = Platform.nil ()

        while Platform.isNull found && not (Platform.isNull c) do
            let e = Platform.fastCast<ActivationEntry> c

            if not e.IsDisposed then found <- e else c <- e.Previous

        found

    interface IDisposable with
        member this.Dispose() =
#if FABLE_COMPILER
            if not disposed then
                disposed <- true

                if obj.ReferenceEquals (Activation.current (), this) then
                    Activation.set (box (ActivationEntry.FirstLive previous))

                if shadowed then
                    cell.ShadowCount <- cell.ShadowCount - 1
#else
            if Interlocked.Exchange (&disposed, 1) = 0 then
                if obj.ReferenceEquals (Activation.current (), this) then
                    Activation.set (box (ActivationEntry.FirstLive previous))

                if shadowed then
                    Interlocked.Decrement &cell.ShadowCount |> ignore
#endif

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


#if FABLE_COMPILER
/// <summary>
/// The caught exception of a body, held where null means the body raised nothing. JavaScript can throw <c>null</c>
/// or <c>undefined</c>; <c>hold</c> stores a stand-in for them and <c>take</c> returns what was thrown.
/// </summary>
module internal Thrown =
    let private nothing: exn = exn "A null value was thrown."

    let hold (ex: exn) : exn =
        if Platform.isNull (box ex) then nothing else ex

    let take (held: exn) : exn =
        if obj.ReferenceEquals (held, nothing) then
            Unchecked.defaultof<exn>
        else
            held
#endif

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
            Platform.nil ()
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
/// <summary>Marker interface for internal value types whose default equality is non-throwing (Theorem B13).</summary>
type internal INonThrowingEquality = interface end

module private DefaultEquality =
    let rec isSafeType (t: Type) =
        if
            t.IsPrimitive
            || t.IsEnum
            || t = typeof<string>
            || t = typeof<decimal>
            || t = typeof<Guid>
            || t = typeof<DateTime>
            || t = typeof<DateTimeOffset>
            || t = typeof<TimeSpan>
            || typeof<INonThrowingEquality>.IsAssignableFrom t
        then
            true
        elif t.IsValueType && t.IsGenericType then
            let d = t.GetGenericTypeDefinition ()

            (d = typedefof<ValueTuple<_>>
             || d = typedefof<ValueTuple<_, _>>
             || d = typedefof<ValueTuple<_, _, _>>
             || d = typedefof<ValueTuple<_, _, _, _>>
             || d = typedefof<ValueTuple<_, _, _, _, _>>
             || d = typedefof<ValueTuple<_, _, _, _, _, _>>
             || d = typedefof<ValueTuple<_, _, _, _, _, _, _>>
             || d = typedefof<ValueTuple<_, _, _, _, _, _, _, _>>
             || d = typedefof<voption<_>>
             || d = typedefof<Nullable<_>>
             || d = typedefof<KeyValuePair<_, _>>)
            && Array.forall isSafeType (t.GetGenericArguments ())
        else
            false

/// <summary>Whether the null-encoded comparer uses reference identity or a nonthrowing value comparison.</summary>
[<AbstractClass; Sealed>]
type internal NonThrowingDefaultEq<'T> private () =
    static member val IsSafe =
        not typeof<'T>.IsValueType
        || DefaultEquality.isSafeType typeof<'T>
#endif

module internal SafeEquality =
    /// <summary>
    /// True when <c>encoded</c> is the null-encoded intrinsic comparer and comparing with it cannot raise, so the
    /// caller may compare outside an exception handler.
    /// </summary>
#if FABLE_COMPILER
    let inline isSafe (encoded: IEqualityComparer<'T>) =
        Platform.isNull (box encoded)
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
    let mutable current: IComputation = Platform.nil ()

    /// <summary>
    /// The <c>Node</c> of <c>current</c> when a memo, effect or split effect entered through <c>EnterHostedNode</c>,
    /// otherwise null. Read by <c>Track</c> to match the read against the node's source list without dispatching
    /// through <c>IComputation.AddSource</c>.
    /// </summary>
    /// <remarks>
    /// Every write of <c>current</c> writes this too: null, or the node of the computation written. Null beside a
    /// node-backed <c>current</c> is allowed and only takes the dispatching path.
    /// </remarks>
    let mutable currentNode: Node = Platform.nil ()

    /// <summary>
    /// The node of the last pending read raised inside the running body, or
    /// null. Survives a <c>try/with</c> in the body that swallows the raise.
    /// </summary>
    let mutable raisedPending: INode = Platform.nil ()

#if !RANVIER_TRACE
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
    let mutable passDrain: IScheduled = Platform.nil ()

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

#if FABLE_COMPILER
    let ownerAmbient = AmbientSlot.Cell
#else
    let ownerAmbient = if guarded then AmbientSlot.Cell else null
#endif
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
    // The physical thread running the flush, or zero when idle. Fable uses one for its single thread.
    let mutable flushingThread = 0

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
    let mutable disposed = false

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
    /// context and node, owner and raised pending read. Innermost last.
    /// </summary>
    let mutable continuations: ResizeArray<struct (obj * IComputation * Node * Owner * INode)> =
        null
#endif

    /// <summary>
    /// The scope host whose body entered <c>untrack</c> before creating anything.
    /// Read only while both the owner and the tracking context are null.
    /// </summary>
    let mutable untrackedHost: IScopeHost = Platform.nil ()

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
    /// Ambient frames pushed by nested stale reads entered while shadowed by a competing activation.
    /// </summary>
    let mutable nestedPullAmbients: ResizeArray<struct (int * obj)> = Platform.nil ()

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
    let usesJsIdentity =
        Platform.sameExactClass (box options.Equality) Graph.ExemplarJsIdentity

    let usesStructural =
        Platform.sameExactClass (box options.Equality) Graph.ExemplarStructural

    let cachedStructuralComparer =
        if usesStructural then
            ComparerSentinel.encode (EqualityComparer<obj>.Default)
        else
            Platform.nil ()
#endif

    new() = new Graph (GraphOptions.Default)

#if FABLE_COMPILER
    static member val private ExemplarJsIdentity: obj = box (JsIdentityPolicy ())
    static member val private ExemplarStructural: obj = box (StructuralPolicy ())
#endif

    member _.Options = options

#if FABLE_COMPILER
    member internal _.EncodedComparer<'T>() : IEqualityComparer<'T> =
        if usesJsIdentity then
            Platform.nil ()
        elif usesStructural then
            unbox<IEqualityComparer<'T>> cachedStructuralComparer
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
#if FABLE_COMPILER
        let cell = AmbientSlot.Existing

        let found =
            if not (Platform.isNull cell) && cell.HostDepth > 0 then
                if cell.ShadowCount = 0 then
                    cell.Hosting
                else
                    Graph.ResolveShadowed cell
            else
                Graph.ActivatedGraph ()

        if not (Platform.isNull found) then
            Platform.fastCast<Graph> found
        else
            raise (
                InvalidOperationException
                    "No ambient graph on this thread. Activate one with `use _ = graph.Activate ()`, or construct nodes against an explicit graph."
            )
#else
        match Graph.Ambient with
        | :? Graph as g -> g
        | _ ->
            raise (
                InvalidOperationException
                    "No ambient graph on this thread. Activate one with `use _ = graph.Activate ()`, or construct nodes against an explicit graph."
            )
#endif

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
#if FABLE_COMPILER
        let clock = cell.Clock + 1.
#else
        let clock = cell.Clock + 1L
#endif
        cell.Clock <- clock
        let shadowed = cell.HostDepth > 0

        if shadowed then
#if FABLE_COMPILER
            cell.ShadowCount <- cell.ShadowCount + 1
#else
            Interlocked.Increment &cell.ShadowCount |> ignore
#endif
        elif guarded && Platform.isNull cell.Hosting then
            cell.Hosting <- this

        let entry =
            new ActivationEntry (this, Platform.currentThreadId (), not guarded, activated, cell, clock, shadowed)

        Activation.set entry
        entry :> IDisposable

    static member private ActivatedGraph() =
        let live = ActivationEntry.FirstLive (Activation.current ())

        if not (Platform.isNull live) then
            live.GraphOnCurrentThread
        else
            null

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member private ResolveShadowed(cell: AmbientCell) : obj =
        let top = ActivationEntry.FirstLive (Activation.current ())

        if not (Platform.isNull top) then
            let mutable cur: obj = box top
            let mutable shadows = false

            while not shadows && not (Platform.isNull cur) do
                let e = Platform.fastCast<ActivationEntry> cur

                if
                    not e.IsDisposed
                    && obj.ReferenceEquals (e.Cell, cell)
                    && e.Clock > cell.HostEpoch
                then
                    shadows <- true
                else
                    cur <- e.Previous

            if shadows then top.GraphOnCurrentThread else cell.Hosting
        else
            cell.Hosting

    /// <summary>The calling context's ambient graph, or null.</summary>
    static member private Ambient: obj =
        let cell = AmbientSlot.Existing

        if not (Platform.isNull cell) && cell.HostDepth > 0 then
            if cell.ShadowCount = 0 then
                cell.Hosting
            else
                Graph.ResolveShadowed cell
        else
            Graph.ActivatedGraph ()

    /// <summary>
    /// True while the graph runs a body, a flush, a stale read or a teardown.
    /// </summary>
    member internal _.Running =
        flushingThread <> 0
        || pullDepth > 0
        || not (Platform.isNull (box currentNode))
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

#if FABLE_COMPILER
    /// <summary>
    /// Makes this graph ambient and returns the graph it replaced in <c>Hosting</c>.
    /// </summary>
    member inline private this.EnterAmbient() : obj =
        ownerAmbient.EnterFrame ()
        let previous = ownerAmbient.Hosting

        if not (obj.ReferenceEquals (previous, this)) then
            ownerAmbient.Hosting <- this

        previous

    /// <summary>
    /// Restores the enclosing host. A guarded graph can stay cached on its owner thread between runs.
    /// </summary>
    member inline private this.LeaveAmbient(previous: obj) =
        let depth = ownerAmbient.LeaveFrame ()

        if depth > 0 then
            if not (obj.ReferenceEquals (previous, this)) then
                ownerAmbient.Hosting <- previous
        elif
            (not guarded || disposed)
            && not (Platform.isNull ownerAmbient.Hosting)
        then
            ownerAmbient.Hosting <- null

#else
    member inline private _.AmbientCell() =
        if guarded then ownerAmbient else AmbientSlot.Cell

    member inline private this.PushAmbient(cell: AmbientCell) =
        cell.EnterFrame ()
        let previous = cell.Hosting

        if not (obj.ReferenceEquals (previous, this)) then
            cell.Hosting <- this

        previous

    member inline private this.PushFlushAmbient(cell: AmbientCell) =
        this.PushAmbient cell

    member inline private this.PopAmbient(cell: AmbientCell, previous: obj) =
        let depth = cell.LeaveFrame ()

        if depth > 0 then
            if not (obj.ReferenceEquals (previous, this)) then
                cell.Hosting <- previous
        elif
            (not guarded || disposed)
            && not (Platform.isNull cell.Hosting)
        then
            cell.Hosting <- null

    member inline private this.EnterAmbient() =
        this.PushAmbient (this.AmbientCell ())

    member inline private this.LeaveAmbient(previous: obj) =
        this.PopAmbient (this.AmbientCell (), previous)
#endif

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
                if not (Platform.isNull (box currentNode)) then
                    currentNode :> IScopeHost
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
        if
            not ownerPending
            && not (Platform.isNull (box currentOwner))
        then
            ValueSome currentOwner
        else
            let host =
                if not (Platform.isNull (box currentNode)) then
                    currentNode :> IScopeHost
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
                owner.AttachToParent this.CurrentOwner
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
#if FABLE_COMPILER
        this.AddCleanup (unbox<unit -> unit> f)
#else
        this.AddCleanup f.Invoke
#endif

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
                disposed <- true
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

#if FABLE_COMPILER
                if
                    obj.ReferenceEquals (ownerAmbient.Hosting, this)
                    && ownerAmbient.HostDepth = 0
                then
                    ownerAmbient.Hosting <- null
#else
                if
                    not (Platform.isNull ownerAmbient)
                    && ownerAmbient.HostDepth = 0
                    && obj.ReferenceEquals (ownerAmbient.Hosting, this)
                then
                    ownerAmbient.Hosting <- null
#endif
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
    member internal _.QueueCapacity = queueSlots.Length

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.EnsureQueueRoom() =
        if queueHead = queueCount && queueHead > 0 then
            // QueueDrain prepends priority passes in the slot before the head.
            queueHead <- 1
            queueCount <- 1
        else
            let size = if queueSlots.Length = 0 then 8 else queueSlots.Length * 2
            let grown = Array.zeroCreate size

            if queueSlots.Length > 0 then
                Array.blit queueSlots 0 grown 0 queueSlots.Length

            queueSlots <- grown
#endif

    /// <summary>Queues an effect or split effect node directly without <c>IScheduled</c> interface dispatch.</summary>
#if FABLE_COMPILER
    member inline internal this.ScheduleNode(n: Node) =
        // EffectNode and EffectOn implement IScheduled directly.
        this.Schedule (unbox<IScheduled> n)
#else
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.ScheduleNode(n: Node) =
        Tracer.Schedule (this, box n, queueCount - queueHead + passCount - passHead)

        if queueCount = queueSlots.Length then
            this.EnsureQueueRoom ()

        queueSlots[queueCount] <- QueueSlot (Unchecked.defaultof<IScheduled>, n)
        queueCount <- queueCount + 1
#endif

#if FABLE_COMPILER && !RANVIER_TRACE
    member inline internal _.Schedule(item: IScheduled) =
        if queueCount < queue.Count then
            Platform.setEntryAt queueCount queue item
        else
            queue.Add item

        queueCount <- queueCount + 1
#else
    member internal this.Schedule(item: IScheduled) =
        Tracer.Schedule (this, item, queueCount - queueHead + passCount - passHead)

#if FABLE_COMPILER
        if queueCount < queue.Count then
            Platform.setEntryAt queueCount queue item
        else
            queue.Add item
#else
        if queueCount = queueSlots.Length then
            this.EnsureQueueRoom ()

        queueSlots[queueCount] <- QueueSlot (item, Unchecked.defaultof<Node>)
#endif
        queueCount <- queueCount + 1
#endif

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

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CreatePassDrain() : IScheduled =
        let drain = PassDrain (fun () -> this.RunPasses ()) :> IScheduled
        passDrain <- drain
        drain

    /// <summary>
    /// Queues <c>passDrain</c> for a pass queued while none is pending: at the head of <c>queue</c> when a spent slot
    /// precedes it, as inside a flush, and at the tail otherwise.
    /// </summary>
    member private this.QueueDrain() =
        let drain =
            if Platform.isNull (box passDrain) then
                this.CreatePassDrain ()
            else
                passDrain

        if queueHead > 0 then
            queueHead <- queueHead - 1
#if FABLE_COMPILER
            Platform.setEntryAt queueHead queue drain
#else
            queueSlots[queueHead] <- QueueSlot (drain, Unchecked.defaultof<Node>)
#endif
        else
#if FABLE_COMPILER
            if queueCount < queue.Count then
                Platform.setEntryAt queueCount queue drain
            else
                queue.Add drain
#else
            if queueCount = queueSlots.Length then
                this.EnsureQueueRoom ()

            queueSlots[queueCount] <- QueueSlot (drain, Unchecked.defaultof<Node>)
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
        if flushingThread = 0 then
#if RANVIER_COUNTERS
            Counters.Flushed ()
#endif
            let cell = this.AmbientCell ()
            flushingThread <- cell.ThreadId
            flushOwed <- false
            let previousAmbient = this.PushFlushAmbient cell

            if passCount = 0 && Platform.isNull journal then
                this.DrainNodesFast (cell, previousAmbient)
            else
                this.DrainSlow (cell, previousAmbient)

    /// <summary>
    /// Drains effect and split-effect nodes without an exception frame (<c>drain_no_escape</c>): neither <c>NodeOps.Execute</c>
    /// nor any <c>UpdateIfNecessary</c> lets an exception escape. Falls back to <c>DrainSlow</c> on the first non-node item.
    /// </summary>
    member private this.DrainNodesFast(cell: AmbientCell, previousAmbient: obj) =
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
            flushingThread <- 0

            if staleTransitions <> 0 && pullDepth = 0 then
                staleTransitions <- 0
                activeSkips <- 0

            if not (Platform.isNull lastRaised) then
                lastRaised <- null

            this.PopAmbient (cell, previousAmbient)
        else
            this.DrainSlow (cell, previousAmbient)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.DrainSlow(cell: AmbientCell, previousAmbient: obj) =
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
            flushingThread <- 0

            if staleTransitions <> 0 && pullDepth = 0 then
                staleTransitions <- 0
                activeSkips <- 0

            if not (Platform.isNull lastRaised) then
                lastRaised <- null

            this.PopAmbient (cell, previousAmbient)

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
        && flushingThread = 0
        && not untrackedInBody
        && queueHead = queueCount
        && obj.ReferenceEquals (currentNode, null)
        && Platform.isNull (box current)

    /// <summary>True when neither a stale read nor a flush is active, so <c>NoteStaleTransition</c> is a no-op.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.IsIdleForMarks() : bool =
        pullDepth = 0 && flushingThread = 0

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.BeginFusedFlush() =
#if RANVIER_COUNTERS
        Counters.Flushed ()
#endif
        if queueSlots.Length = 0 then
            this.EnsureQueueRoom ()

        queueHead <- 1
        queueCount <- 1
        let cell = this.AmbientCell ()
        flushingThread <- cell.ThreadId
        flushOwed <- false
        struct (cell, this.PushFlushAmbient cell)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.EndFusedFlush(struct (cell, previousAmbient)) =
        if queueHead < queueCount || passCount <> 0 then
            this.DrainNodesFast (cell, previousAmbient)
        else
            queueHead <- 0
            queueCount <- 0
            flushingThread <- 0

            if staleTransitions <> 0 then
                staleTransitions <- 0
                activeSkips <- 0

            if not (Platform.isNull lastRaised) then
                lastRaised <- null

            this.PopAmbient (cell, previousAmbient)
#else
#if FABLE_COMPILER && !RANVIER_TRACE
    member private this.RunFlush() =
        if flushingThread = 0 then
#if RANVIER_COUNTERS
            Counters.Flushed ()
#endif
            flushingThread <- 1
            flushOwed <- false
            let previousAmbient = this.EnterAmbient ()

            try
                if passCount <> 0 then
                    this.RunPasses ()

                while queueHead < queueCount do
                    let item = Platform.entryAt queueHead queue
                    Platform.releaseSlot queue queueHead
                    queueHead <- queueHead + 1
                    item.Execute ()

                queueHead <- 0
                queueCount <- 0
            finally
                this.CloseFlush previousAmbient

    /// <summary>
    /// Runs the queued passes and items of an open flush, then closes it. An exception closes the flush and leaves the
    /// rest queued for the next one.
    /// </summary>
    member private this.DrainQueue(previousAmbient: obj) =
        try
            if passCount <> 0 then
                this.RunPasses ()

            while queueHead < queueCount do
                let item = Platform.entryAt queueHead queue
                Platform.releaseSlot queue queueHead
                queueHead <- queueHead + 1
                item.Execute ()

            queueHead <- 0
            queueCount <- 0
        finally
            this.CloseFlush previousAmbient

    member inline private this.CloseFlush(previousAmbient: obj) =
        flushingThread <- 0

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

    /// <summary>True when a signal write can fuse an immediate single-observer flush (Theorem B5).</summary>
    member inline internal _.CanFuseIdleFlush() : bool =
        Platform.isNull journal
        && ((batchDepth ||| pullDepth ||| passCount) = 0)
        && flushingThread = 0
        && not untrackedInBody
        && queueHead = queueCount
        && Platform.isNull (box currentNode)
        && Platform.isNull (box current)

    /// <summary>True when neither a stale read nor a flush is active, so <c>NoteStaleTransition</c> is a no-op.</summary>
    member inline internal _.IsIdleForMarks() : bool =
        pullDepth = 0 && flushingThread = 0

    /// <summary>
    /// Opens a flush whose one item is already dequeued, and returns the graph <c>Hosting</c> held. The spent slot 0
    /// keeps a pass queued by the run at the head of the queue, as in a flush that dequeued the item.
    /// </summary>
    member internal this.BeginFusedFlush() : obj =
#if RANVIER_COUNTERS
        Counters.Flushed ()
#endif
        if queue.Count = 0 then
            queue.Add (Platform.nil ())

        queueHead <- 1
        queueCount <- 1
        flushingThread <- 1
        flushOwed <- false
        this.EnterAmbient ()

    member internal this.EndFusedFlush(previousAmbient: obj) =
        if queueHead < queueCount || passCount <> 0 then
            this.DrainQueue previousAmbient
        else
            queueHead <- 0
            queueCount <- 0
            this.CloseFlush previousAmbient

    /// <summary>Closes a fused flush whose run let an exception escape, leaving the queue as it stands.</summary>
    member internal this.AbortFusedFlush(previousAmbient: obj) =
        this.CloseFlush previousAmbient
#else
    member private this.RunFlush() =
        if flushingThread = 0 then
#if RANVIER_COUNTERS
            Counters.Flushed ()
#endif
            Tracer.FlushStart this
#if FABLE_COMPILER
            flushingThread <- 1
            let previousAmbient = this.EnterAmbient ()
#else
            let cell = this.AmbientCell ()
            flushingThread <- cell.ThreadId
            let previousAmbient = this.PushFlushAmbient cell
#endif
            flushOwed <- false

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
                            box slot.Node
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
                flushingThread <- 0

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
                && Platform.isNull (box currentNode)
                && Platform.isNull (box current)
                && not untrackedInBody
            then
#if FABLE_COMPILER
                this.RunFlush ()
#else
                this.Flush ()
#endif
            elif flushingThread = 0 then
                flushOwed <- true

    /// <summary>
    /// <c>RequestFlush</c> from a caller that has already passed <c>Enter</c> (a signal write, batch exit, or inbox drain).
    /// </summary>
    member internal this.RequestFlushFromEntered() =
        if queueHead < queueCount && batchDepth = 0 then
            if
                pullDepth = 0
                && Platform.isNull (box currentNode)
                && Platform.isNull (box current)
                && not untrackedInBody
            then
                this.RunFlush ()
            elif flushingThread = 0 then
                flushOwed <- true

    /// <summary>
    /// Runs the owed flush when the caller is outside every body and read.
    /// </summary>
    member private this.SettleFlush() =
        if
            flushOwed
            && batchDepth = 0
            && pullDepth = 0
            && Platform.isNull (box currentNode)
            && Platform.isNull (box current)
            && not untrackedInBody
        then
#if FABLE_COMPILER
            this.RunFlush ()
#else
            this.Flush ()
#endif

    /// <summary>
    /// Owes a flush for effects queued while a read or discharge is in progress.
    /// </summary>
#if FABLE_COMPILER
    member inline internal _.OweFlush() =
#else
    member internal _.OweFlush() =
#endif
        if
            queueHead < queueCount
            && batchDepth = 0
            && flushingThread = 0
        then
            flushOwed <- true

    /// <summary>True while the queue holds work: the inline test in front of a flush request.</summary>
    member inline internal _.QueuePending = queueHead < queueCount

    /// <summary>Clears a swallowed failed-read record after an untracked refresh inside a flush.</summary>
#if FABLE_COMPILER
    member inline internal this.ClearLastRaisedIfTop() =
#else
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.ClearLastRaisedIfTop() =
#endif
        if not (Platform.isNull lastRaised) then
            this.ClearLastRaisedIfTopSlow ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member internal _.ClearLastRaisedIfTopSlow() =
        if
            pullDepth = 0
            && Platform.isNull (box currentNode)
            && Platform.isNull (box current)
        then
            lastRaised <- null

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.ClearLastRaised() =
        lastRaised <- null

    member inline internal _.HasLastRaised = not (Platform.isNull lastRaised)

#if FABLE_COMPILER
    member inline internal this.IsHostedUnshadowed() : bool =
        ownerAmbient.HostDepth > 0
        && obj.ReferenceEquals (ownerAmbient.Hosting, this)
        && ownerAmbient.ShadowCount = 0

    /// <summary>
    /// True inside a flush, a stale read, or while a computation's body runs outside <c>untrack</c> when this graph is
    /// already the unshadowed host on the calling thread (Theorem B1).
    /// </summary>
    member inline internal this.Deferring =
        (flushingThread <> 0
         || pullDepth > 0
         || not (Platform.isNull (box currentNode))
         || not (Platform.isNull (box current)))
        && this.IsHostedUnshadowed ()
#else
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.IsHostedUnshadowed() : bool =
        if guarded then
            OwnerThreadToken.Current = ownerToken
            && ownerAmbient.HostDepth > 0
            && obj.ReferenceEquals (ownerAmbient.Hosting, this)
            && ownerAmbient.ShadowCount = 0
        else
            let cell = AmbientSlot.Existing

            not (Platform.isNull cell)
            && cell.HostDepth > 0
            && obj.ReferenceEquals (cell.Hosting, this)
            && cell.ShadowCount = 0
            && (not serialised
                || Volatile.Read &holder = Platform.currentThreadId ())

    /// <summary>
    /// True inside a flush, a stale read, or while a computation's body runs outside <c>untrack</c> when this graph is
    /// already the unshadowed host on the calling thread (Theorem B1: <c>flushing_implies_no_flush_owed</c> &amp;
    /// <c>deferring_with_flushing_eq</c>). False off-thread, under a competing <c>Activate</c>, or outside the holder of a
    /// <c>Serialised</c> graph, so a stale read there enters through <c>EnterPull</c>.
    /// </summary>
    member internal this.Deferring = this.IsDeferring ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.IsDeferring() =
        (flushingThread <> 0
         || pullDepth > 0
         || not (Platform.isNull (box currentNode))
         || not (Platform.isNull (box current)))
        && this.IsHostedUnshadowed ()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.EnterShadowedNestedPull() =
        if Platform.isNull nestedPullAmbients then
            nestedPullAmbients <- ResizeArray<struct (int * obj)>()

        nestedPullAmbients.Add (struct (pullDepth, this.EnterAmbient ()))

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.TryLeaveShadowedNestedPull() =
        let lastIdx = nestedPullAmbients.Count - 1
        let struct (d, prev) = Platform.entryAt lastIdx nestedPullAmbients

        if d = pullDepth then
            nestedPullAmbients.RemoveAt lastIdx
            this.LeaveAmbient prev

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
        elif not (this.IsHostedUnshadowed ()) then
            this.EnterShadowedNestedPull ()

        pullDepth <- pullDepth + 1

#if !FABLE_COMPILER
    /// <summary>
    /// Opens the outermost stale read when <c>EnterPull</c> would change nothing but the depth: no read is open, the
    /// calling thread owns the guarded graph and already hosts it unshadowed. False leaves the read to <c>EnterPull</c>.
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.TryEnterPullFast() : bool =
        if
            pullDepth = 0
            && guarded
            && OwnerThreadToken.Current = ownerToken
            && obj.ReferenceEquals (ownerAmbient.Hosting, this)
            && ownerAmbient.ShadowCount = 0
        then
            ownerAmbient.EnterFrame ()
            pullDepth <- 1
            true
        else
            false
#else
#if !RANVIER_TRACE
    /// <summary>True when the ambient cell already holds this graph unshadowed, so entering and leaving it writes no reference.</summary>
    member inline private this.IsHosted =
        obj.ReferenceEquals (ownerAmbient.Hosting, this)
        && ownerAmbient.ShadowCount = 0

    /// <summary>
    /// Opens the outermost stale read when <c>EnterPull</c> would change nothing but the depth: no read is open and
    /// the ambient cell already holds the graph unshadowed. False leaves the read to <c>EnterPull</c>.
    /// </summary>
    member internal this.TryEnterPullFast() : bool =
        if pullDepth = 0 && this.IsHosted then
            ownerAmbient.EnterFrame ()
            pullDepth <- 1
            true
        else
            false
#endif
#endif

#if !FABLE_COMPILER || !RANVIER_TRACE
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
        let depth = ownerAmbient.LeaveFrame ()

        if
            Platform.isNull lastRaised
            && not flushOwed
            && staleTransitions = 0
            && (depth > 0 || not disposed)
        then
            false
        else
            this.ExitPullFastSlow depth

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ExitPullFastSlow(depth: int) : bool =
        if
            depth = 0
            && disposed
            && obj.ReferenceEquals (ownerAmbient.Hosting, this)
        then
            ownerAmbient.Hosting <- null

        if not (Platform.isNull lastRaised) then
            lastRaised <- null

        if flushOwed then
            true
        else
            if staleTransitions <> 0 && flushingThread = 0 then
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

            if staleTransitions <> 0 && flushingThread = 0 then
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
        elif
            not (Platform.isNull nestedPullAmbients)
            && nestedPullAmbients.Count > 0
        then
            this.TryLeaveShadowedNestedPull ()

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
                if staleTransitions <> 0 && flushingThread = 0 then
                    staleTransitions <- 0
                    activeSkips <- 0

#if !FABLE_COMPILER
                if pullHeld then
                    pullHeld <- false
                    this.Release ()
#endif
                false
        else
            if
                not (Platform.isNull nestedPullAmbients)
                && nestedPullAmbients.Count > 0
            then
                this.TryLeaveShadowedNestedPull ()

            false

    member internal this.FinishPullSettle() =
        if pullDepth = 0 then
            if staleTransitions <> 0 && flushingThread = 0 then
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
            if Platform.hasMember node "UnwrapMemoNode" then
                (box node :?> IMemoHandle).UnwrapMemoNode()
            elif Platform.hasMember node "UpdateIfNecessary" then
                node :?> ISource
            else
                Platform.nil ()
#else
            match node with
            | :? IMemoHandle as h -> h.UnwrapMemoNode ()
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
        // `Enter` is constant false under Fable: JavaScript has one thread.
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
    member internal this.RunBatchWith(f: 'A -> 'T, arg: 'A) : 'T =
        batchDepth <- batchDepth + 1
        Tracer.BatchEnter (this, batchDepth)

        let result =
            try
                f arg
            finally
                batchDepth <- batchDepth - 1
                Tracer.BatchExit (this, batchDepth)

        this.RequestFlushFromEntered ()
        result

    /// <summary><c>RunBatchWith</c> for a body that returns nothing.</summary>
    member internal this.RunBatchUnitWith(f: 'A -> unit, arg: 'A) : unit =
        batchDepth <- batchDepth + 1
        Tracer.BatchEnter (this, batchDepth)

        try
            f arg
        finally
            batchDepth <- batchDepth - 1
            Tracer.BatchExit (this, batchDepth)

        this.RequestFlushFromEntered ()
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
    member internal this.RunBatchUnitWith<'A>(f: 'A -> unit, arg: 'A) : unit =
        this.RunBatchWith<'A, unit>(f, arg)

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
#endif

    /// <summary>The open batch depth. A write that sees it above zero leaves the flush to the batch's exit.</summary>
    member inline internal _.BatchDepth = batchDepth

    /// <summary>True while <c>RunFlush</c> is draining the schedule queue.</summary>
    member inline internal _.IsFlushing = flushingThread <> 0

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

        member this.RunDetachedDisposeScope(owner) =
#if !FABLE_COMPILER
            let held = serialised && this.Acquire "Disposing a root"

            if not held then
                this.DetachedDisposeScope owner

                if queueHead < queueCount then
                    this.RequestFlush ()
            else
                try
                    this.DetachedDisposeScope owner

                    if queueHead < queueCount then
                        this.RequestFlushFromEntered ()
                finally
                    this.Release ()
#else
            this.DetachedDisposeScope owner

            if queueHead < queueCount then
                this.RequestFlush ()
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
        current <- Platform.nil ()
        currentNode <- Platform.nil ()
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
        current <- Platform.nil ()
        currentNode <- Platform.nil ()
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
        current <- Platform.nil ()
        currentNode <- Platform.nil ()
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
        current <- Platform.nil ()
        currentNode <- Platform.nil ()
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

#if FABLE_COMPILER && !RANVIER_TRACE
    member inline internal _.NextId() =
        nextId <- nextId + 1
        nextId
#else
    member internal this.NextId() =
        this.AssertOnGraphThread "Creating a node"
        nextId <- nextId + 1
        nextId
#endif

    /// <summary>The last id <c>NextId</c> issued.</summary>
    member internal _.NodeCount = nextId

    member inline internal _.StaleTransitions = staleTransitions

    member inline internal _.ActiveSkips = activeSkips

    member inline internal _.NoteStaleTransition() =
        if pullDepth > 0 || flushingThread <> 0 then
            staleTransitions <- staleTransitions + 1

    member internal _.NoteActiveSkip() =
        activeSkips <- activeSkips + 1
        staleTransitions <- staleTransitions + 1

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.NotifyMoved(observers: ObserverSet) =
        let c = this.CurrentObserver

        if staleTransitions = 0 then
            observers.NotifyDirtyOther c
        else
            observers.NotifyDirtyExcept c

#if !FABLE_COMPILER && !RANVIER_TRACE
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.NotifyMovedList(observers: byref<ObserverList>) =
        if observers.Count <> 0 then
            let c = this.CurrentObserver

            if staleTransitions = 0 then
                observers.NotifyDirtyOther c
            else
                observers.NotifyDirtyExcept c
#endif

#if FABLE_COMPILER && !RANVIER_TRACE
    member inline internal this.NotifyMovedList(observers: ObserverList) =
        if not (Platform.isNull observers) then
            let count = observers.Count

            if count = 1 then
                let refObj = observers.FirstRef
                let c = this.CurrentObserver

                if not (obj.ReferenceEquals (refObj, box c)) then
                    NodeOps.MarkDirtyOf (refObj, observers.FirstSlot)
                elif
                    staleTransitions <> 0
                    && NodeOps.HasReadSlotOf (refObj, observers.FirstSlot)
                then
                    NodeOps.MarkDirtyOf (refObj, observers.FirstSlot)
            elif count > 1 then
                let c = this.CurrentObserver

                if staleTransitions = 0 then
                    observers.NotifyDirtyOther c
                else
                    observers.NotifyDirtyExcept c
#endif

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
#if !RANVIER_TRACE
    member internal _.Track(source: ISource) =
        let n = currentNode

        if not (Platform.isNull (box n)) then
            // A read of the source at position 0 or 1 advances the cursor. A disposed node holds null in both
            // positions, and its read is ignored. Self-hosted SourceList fields live directly on Node.
            let sources = Platform.fastCast<SourceList>(box n)
            let cur = sources.cursor

            if
                cur = 0
                && obj.ReferenceEquals (sources.first, source)
            then
                sources.cursor <- 1
            elif
                cur = 1
                && obj.ReferenceEquals (sources.second, source)
            then
                sources.cursor <- 2
            else
                NodeOps.AddSource (n, source)
        elif not (Platform.isNull (box current)) then
            current.AddSource source

    /// <summary>The observer identity of the running computation (<c>Node</c> directly when node-hosted), or null.</summary>
    member inline internal _.CurrentObserver: IComputation =
        if not (Platform.isNull (box currentNode)) then
            currentNode :> IComputation
        else
            current

    /// <summary>The computation whose body is running, or null.</summary>
    member internal _.CurrentComputation =
        if not (Platform.isNull (box currentNode)) then
            currentNode :> IComputation
        else
            current

    /// <summary>The <c>Node</c> of the running computation when it entered through its node, or null.</summary>
    member inline internal _.CurrentNode = currentNode

    member internal _.IsTracking() =
        not (Platform.isNull (box currentNode))
        || not (Platform.isNull (box current))
#else
    member internal _.Track(source: ISource) =
        if not (Platform.isNull (box current)) then
            // The observer half is linked by the consumer's own source list,
            // which is the only side that knows whether this edge is new.
            current.AddSource source

    member inline internal _.CurrentObserver: IComputation = current

    /// <summary>
    /// The computation whose body is running, or null. Read by a source that
    /// has just recomputed, to avoid marking its own reader dirty — see
    /// <c>ObserverSet.NotifyDirtyExcept</c>.
    /// </summary>
    member internal _.CurrentComputation = current

    member internal _.IsTracking() =
        not (Platform.isNull (box current))
#endif
#else
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.Track(source: ISource) =
        let n = currentNode

        if not (obj.ReferenceEquals (n, null)) then
#if !RANVIER_TRACE
            // Theorem B2 (`track_slot0_fast_eq` & `track_slot1_fast_eq`): `source` is never null, and a disposed
            // node clears `first` and `second` to null, so a reference match proves `n` live and advances cursor.
            let cur = n.SourcesField.cursor

            if
                cur = 0
                && obj.ReferenceEquals (n.SourcesField.first, source)
            then
                n.SourcesField.cursor <- 1
            else
                this.TrackNodeRest (n, cur, source)
#else
            NodeOps.AddSource (n, source)
#endif
        elif not (obj.ReferenceEquals (current, null)) then
            current.AddSource source

#if !RANVIER_TRACE
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.TrackNodeRest(n: Node, cur: int, source: ISource) =
        if
            cur = 1
            && obj.ReferenceEquals (n.SourcesField.second, source)
        then
            n.SourcesField.cursor <- 2
        else
            this.TrackNodeSlow (n, source)
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.TrackNodeSlow(n: Node, source: ISource) =
        NodeOps.AddSource (n, source)

    /// <summary>The observer identity of the running computation (<c>Node</c> directly when node-hosted), or null.</summary>
    member internal this.CurrentObserver = this.GetCurrentObserver ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private _.GetCurrentObserver() : IComputation =
        if not (obj.ReferenceEquals (currentNode, null)) then
            currentNode :> IComputation
        else
            current

    /// <summary>
    /// The computation whose body is running, or null. Read by a source that
    /// has just recomputed, to avoid marking its own reader dirty — see
    /// <c>ObserverSet.NotifyDirtyExcept</c>.
    /// </summary>
    member internal this.CurrentComputation = this.GetCurrentComputation ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private _.GetCurrentComputation() : IComputation =
        if not (obj.ReferenceEquals (currentNode, null)) then
            match currentNode with
            | :? EffectNode as e when not (obj.ReferenceEquals (e.PublicNode, null)) -> e.PublicNode :?> IComputation
            | _ -> currentNode :> IComputation
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
        if not (Platform.isNull (box currentNode)) then
            this.NoteStaleTransition ()
            NodeOps.MarkDirty currentNode
        elif not (Platform.isNull (box current)) then
            this.NoteStaleTransition ()
            current.MarkDirty ()

    /// <summary>
    /// Marks the running computation <c>Check</c>, if any: <c>MarkReaderDirty</c> for a source that tells its readers
    /// <c>Check</c> when it goes dirty, so a re-run to an equal value stops at the cutoff.
    /// </summary>
    member internal this.MarkReaderCheck() =
        if not (Platform.isNull (box currentNode)) then
            this.NoteStaleTransition ()
            NodeOps.MarkCheck currentNode
        elif not (Platform.isNull (box current)) then
            this.NoteStaleTransition ()
            current.MarkCheck ()

#if !FABLE_COMPILER
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.EnterHosted(host: IComputation) : HostedFrame =
        let saved =
            HostedFrame (current, currentNode, currentOwner, ownerPending, raisedPending)

        current <- host

        if not (obj.ReferenceEquals (currentNode, null)) then
            currentNode <- Unchecked.defaultof<Node>

        ownerPending <- true

        if not (Platform.isNull (box raisedPending)) then
            raisedPending <- Unchecked.defaultof<INode>

        saved
#endif

#if FABLE_COMPILER
    member inline internal _.HostedCurrent = current
    member inline internal _.HostedNode = currentNode
    member inline internal _.HostedOwner = currentOwner
    member inline internal _.HostedPending = ownerPending
    member inline internal _.HostedRaised = raisedPending

    member inline internal _.BeginHosted(host: IComputation) =
        current <- host
        currentNode <- Platform.nil ()
        ownerPending <- true
        raisedPending <- Platform.nil ()

    member inline internal _.EndHosted
        (previous: IComputation, previousNode: Node, previousOwner: Owner, previousPending: bool, previousRaised: INode)
        =
        current <- previous
        currentNode <- previousNode

        if not ownerPending then
            currentOwner <- previousOwner

        ownerPending <- previousPending
        raisedPending <- previousRaised
#endif

#if !FABLE_COMPILER || !RANVIER_TRACE
#if FABLE_COMPILER
    /// <summary>True when a hosted node can install its reader without saving an enclosing reader or pending read.</summary>
    member inline internal _.CanEnterHostedTop() : bool =
        Platform.isNull (box currentNode)
        && Platform.isNull (box current)
        && Platform.isNull (box raisedPending)

    member internal this.CanPullTopDirect() =
        pullDepth = 0
        && flushingThread = 0
        && this.CanEnterHostedTop ()
        && not ownerPending
        && this.IsHosted

    member internal _.EnterPullDirect() =
        ownerAmbient.EnterFrame ()
        pullDepth <- 1

    member internal this.CanPullUncached() =
        pullDepth = 0
        && flushingThread = 0
        && this.CanEnterHostedTop ()

    member inline internal _.OwnerPending = ownerPending

    member inline internal _.EnterHostedTop(host: Node) : Owner =
        let saved = currentOwner
        currentNode <- host
        ownerPending <- true
        saved

    member inline internal _.LeaveHostedTop(previousOwner: Owner, previousPending: bool) =
        currentNode <- Platform.nil ()

        if not ownerPending then
            currentOwner <- previousOwner

        ownerPending <- previousPending

    member internal this.LeaveHostedTopFailed(previousOwner: Owner, previousPending: bool) =
        this.LeaveHostedTop (previousOwner, previousPending)

        if not (Platform.isNull (box raisedPending)) then
            raisedPending <- Platform.nil ()
#else
    /// <summary>True when a hosted node can install its reader without saving an enclosing reader or pending read.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.CanEnterHostedTop() : bool =
        Platform.isNull (box currentNode)
        && Platform.isNull (box current)
        && Platform.isNull (box raisedPending)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.CanPullTopDirect() =
        pullDepth = 0
        && flushingThread = 0
        && this.CanEnterHostedTop ()
        && not ownerPending
        && guarded
        && OwnerThreadToken.Current = ownerToken
        && obj.ReferenceEquals (ownerAmbient.Hosting, this)
        && ownerAmbient.ShadowCount = 0

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.EnterPullDirect() =
        ownerAmbient.EnterFrame ()
        pullDepth <- 1

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.CanPullUncached() =
        pullDepth = 0
        && flushingThread = 0
        && not serialised
        && this.CanEnterHostedTop ()

    member inline internal _.OwnerPending = ownerPending

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.EnterHostedTop(host: Node) : Owner =
        let saved = currentOwner
        currentNode <- host
        ownerPending <- true
        saved

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.LeaveHostedTop(previousOwner: Owner, previousPending: bool) =
        currentNode <- Unchecked.defaultof<Node>

        if not ownerPending then
            currentOwner <- previousOwner

        ownerPending <- previousPending

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member internal this.LeaveHostedTopFailed(previousOwner: Owner, previousPending: bool) =
        this.LeaveHostedTop (previousOwner, previousPending)

        if not (Platform.isNull (box raisedPending)) then
            raisedPending <- Unchecked.defaultof<INode>
#endif
#endif

#if !FABLE_COMPILER
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.EnterHostedNode(host: Node) : HostedFrame =
        let saved =
            HostedFrame (current, currentNode, currentOwner, ownerPending, raisedPending)

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
        currentNode <- Platform.nil ()
        ownerPending <- true
        raisedPending <- Platform.nil ()

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
        currentNode <- Platform.nil ()
        ownerPending <- true
        raisedPending <- Platform.nil ()

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

#if FABLE_COMPILER && !RANVIER_TRACE
    /// <summary>
    /// <c>RunHosted(host, body, arg)</c> for a memo, effect or split effect: its node is the tracking context, so a
    /// read in <c>body</c> is matched against the node's source list directly.
    /// </summary>
    member internal _.RunHostedNode(host: Node, body: 'A -> 'T, arg: 'A) =
        let previous = current
        let previousNode = currentNode
        let previousOwner = currentOwner
        let previousPending = ownerPending
        let previousRaised = raisedPending
        current <- Platform.nil ()
        currentNode <- host
        ownerPending <- true
        raisedPending <- Platform.nil ()

        try
            let result = body arg

            if not (Platform.isNull (box raisedPending)) then
                raise (NotReadyException raisedPending)

            result
        finally
            current <- previous
            currentNode <- previousNode

            if not ownerPending then
                currentOwner <- previousOwner

            ownerPending <- previousPending

            if not (obj.ReferenceEquals (raisedPending, previousRaised)) then
                raisedPending <- previousRaised
#endif

    /// <summary>
    /// Temporarily restores <c>observer</c> as the active tracking context and clears untracked
    /// body suppression without resetting <c>raisedPending</c> or <c>currentOwner</c>.
    /// </summary>
    member internal _.ResumeTracked(observer: IComputation, body: unit -> 'T) : 'T =
        let previous = current
        let previousNode = currentNode
        let previousHost = untrackedHost
        let previousInBody = untrackedInBody

        untrackedHost <- Platform.nil ()
        untrackedInBody <- false

#if !FABLE_COMPILER || !RANVIER_TRACE
        match box observer with
        | :? Node as n ->
            current <- Platform.nil ()
            currentNode <- n
        | _ ->
            current <- observer
            currentNode <- Platform.nil ()
#else
        current <- observer
        currentNode <- Platform.nil ()
#endif

        try
            body ()
        finally
            current <- previous
            currentNode <- previousNode
            untrackedHost <- previousHost
            untrackedInBody <- previousInBody

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
                let struct (saved, _, _, _, _) = continuations[i]

                if Object.ReferenceEquals (saved, frame) then
                    entered <- true

            if entered then
                false
            else
                // A pending owner is saved as the null it stands for; both current and currentNode are saved so top-level
                // node-hosted fast lanes resume with their exact current/currentNode pair intact.
                let owner =
                    if ownerPending then
                        Unchecked.defaultof<Owner>
                    else
                        currentOwner

                continuations.Add (struct (frame, current, currentNode, owner, raisedPending))
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
            let struct (saved, computation, node, owner, raised) = continuations[top]

            if Object.ReferenceEquals (saved, frame) then
                continuations.RemoveAt top
                current <- computation
                currentNode <- node
                currentOwner <- owner
                ownerPending <- false
                raisedPending <- raised

                if not (Platform.isNull journal) then
                    journal.LeaveDerived ()

    interface Platform.IContinuationHost with
        member this.EnterContinuation frame =
            this.EnterContinuation frame

        member this.LeaveContinuation frame =
            this.LeaveContinuation frame
#endif

    /// <summary>
    /// The exception a pending read of <c>node</c> raises. Inside a computation's
    /// body, the read is recorded for <c>RunHosted</c>.
    /// </summary>
    member internal this.NotReady(node: INode) : exn =
        if not (Platform.isNull (box currentNode)) then
            Tracer.Suspend (this, currentNode, node)
            raisedPending <- node
        elif not (Platform.isNull (box current)) then
            Tracer.Suspend (this, current, node)
            raisedPending <- node

        NotReadyException node

    /// <summary>
    /// Records <c>failure</c> as the failure of the last failed read, for <c>FailureOf</c> in the run that catches it.
    /// </summary>
    /// <remarks>Records nothing outside every stale read and flush.</remarks>
    member internal _.Raised(failure: Failure) =
        if pullDepth > 0 || flushingThread <> 0 then
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
#if FABLE_COMPILER
    member inline internal _.ClearRaised() =
        if not (Platform.isNull (box raisedPending)) then
            raisedPending <- Platform.nil ()

    member inline internal _.CheckRaised() =
        if not (Platform.isNull (box raisedPending)) then
            raise (NotReadyException raisedPending)
#else
    member internal _.ClearRaised() =
        raisedPending <- Platform.nil ()

    /// <summary>
    /// Raises <c>NotReadyException</c> if a pending read raised in the running body
    /// since the last <c>ClearRaised</c>.
    /// </summary>
    member internal _.CheckRaised() =
        if not (Platform.isNull (box raisedPending)) then
            raise (NotReadyException raisedPending)
#endif

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

                if not (Platform.isNull (box previousNode)) then
                    untrackedInBody <- true

                    if ownerPending || Platform.isNull (box currentOwner) then
                        untrackedHost <- previousNode :> IScopeHost
                elif not (Platform.isNull (box previous)) then
                    untrackedInBody <- true

                    if ownerPending || Platform.isNull (box currentOwner) then
                        untrackedHost <- previous :?> IScopeHost

                current <- Platform.nil ()
                currentNode <- Platform.nil ()

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

                if not (Platform.isNull (box previousNode)) then
                    untrackedInBody <- true

                    if ownerPending || Platform.isNull (box currentOwner) then
                        untrackedHost <- previousNode :> IScopeHost
                elif not (Platform.isNull (box previous)) then
                    untrackedInBody <- true

                    if ownerPending || Platform.isNull (box currentOwner) then
                        untrackedHost <- previous :?> IScopeHost

                current <- Platform.nil ()
                currentNode <- Platform.nil ()

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

                if not (Platform.isNull (box previousNode)) then
                    untrackedInBody <- true

                    if ownerPending || Platform.isNull (box currentOwner) then
                        untrackedHost <- previousNode :> IScopeHost
                elif not (Platform.isNull (box previous)) then
                    untrackedInBody <- true

                    if ownerPending || Platform.isNull (box currentOwner) then
                        untrackedHost <- previous :?> IScopeHost

                current <- Platform.nil ()
                currentNode <- Platform.nil ()
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

        if not (obj.ReferenceEquals (previousNode, null)) then
            untrackedInBody <- true

            if ownerPending || Platform.isNull (box currentOwner) then
                untrackedHost <- previousNode :> IScopeHost
        elif not (Platform.isNull (box previous)) then
            untrackedInBody <- true

            if ownerPending || Platform.isNull (box currentOwner) then
                untrackedHost <- previous :?> IScopeHost

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

        if
            not (Platform.isNull (box previousNode))
            || not (Platform.isNull (box previous))
        then
            untrackedInBody <- true

        current <- Platform.nil ()
        currentNode <- Platform.nil ()
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

        if
            not (Platform.isNull (box previousNode))
            || not (Platform.isNull (box previous))
        then
            untrackedInBody <- true

        current <- Platform.nil ()
        currentNode <- Platform.nil ()
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

        if
            not (Platform.isNull (box previousNode))
            || not (Platform.isNull (box previous))
        then
            untrackedInBody <- true

        current <- Platform.nil ()
        currentNode <- Platform.nil ()
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

#if !RANVIER_TRACE
#if FABLE_COMPILER
    member inline internal _.CanNestInReader =
        ownerPending
        && not (Platform.isNull (box currentNode))
        && Platform.isNull (box raisedPending)

    /// <summary>Restores the reader <c>parent</c> after a nested memo's run failed, and drops the run's pending read.</summary>
    member internal _.LeaveNestedReaderFailed(parent: Node, previousOwner: Owner) =
        currentNode <- parent

        if not ownerPending then
            currentOwner <- previousOwner

        ownerPending <- true

        if not (Platform.isNull (box raisedPending)) then
            raisedPending <- Platform.nil ()
#else
    member inline internal _.CanNestInReader =
        not serialised
        && ownerPending
        && not (obj.ReferenceEquals (currentNode, null))
        && Platform.isNull (box raisedPending)
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.LeaveNestedReader(parent: Node, previousOwner: Owner) =
        currentNode <- parent

        if not ownerPending then
            currentOwner <- previousOwner
            ownerPending <- true

    member inline internal _.HasUntrackedHost = not (Platform.isNull (box untrackedHost))

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.SwitchToActionTop(host: IScopeHost, previousOwner: Owner) =
        currentNode <- Platform.nil ()

        if not ownerPending then
            currentOwner <- previousOwner
            ownerPending <- true

        untrackedHost <- host

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.LeaveActionTop(previousOwner: Owner) =
        untrackedHost <- Platform.nil ()

        if not ownerPending then
            currentOwner <- previousOwner

        ownerPending <- false
#endif

#if !FABLE_COMPILER
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

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.EnterUntrackedPure(host: IScopeHost) : UntrackedPureFrame =
        let saved =
            UntrackedPureFrame (current, currentNode, ownerPending, untrackedHost, untrackedInBody)

        if not (obj.ReferenceEquals (saved.PreviousNode, null)) then
            currentNode <- Unchecked.defaultof<Node>

        if not (Platform.isNull (box saved.Previous)) then
            current <- Unchecked.defaultof<IComputation>

        untrackedInBody <- true
        ownerPending <- true
        untrackedHost <- host
        saved

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.LeaveUntrackedPure(saved: UntrackedPureFrame) =
        currentNode <- saved.PreviousNode
        current <- saved.Previous
        ownerPending <- saved.PreviousPending
        untrackedHost <- saved.PreviousHost
        untrackedInBody <- saved.PreviousInBody

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.EnterStaticRefresh(host: Node) : StaticRefreshFrame =
        let saved = StaticRefreshFrame (current, currentNode)

        if not (Platform.isNull (box saved.Previous)) then
            current <- Unchecked.defaultof<IComputation>

        currentNode <- host
        saved

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.LeaveStaticRefresh(saved: StaticRefreshFrame) =
        currentNode <- saved.PreviousNode
        current <- saved.Previous
#else
    member inline internal _.RunStaticRefresh(host: Node, [<InlineIfLambda>] body: unit -> unit) =
        let previous = current
        let previousNode = currentNode
#if !RANVIER_TRACE
        current <- Platform.nil ()
        currentNode <- host
#else
        current <- host :> IComputation
        currentNode <- Platform.nil ()
#endif

        try
            body ()
        finally
            current <- previous
            currentNode <- previousNode

    member internal _.RunUntrackedPureArg(host: IScopeHost, f: 'A -> 'T, arg: 'A) : 'T =
        let previous = current
        let previousNode = currentNode
        let previousPending = ownerPending
        let previousHost = untrackedHost
        let previousInBody = untrackedInBody
        current <- Platform.nil ()
        currentNode <- Platform.nil ()
        ownerPending <- true
        untrackedHost <- host
        untrackedInBody <- true

        if not (Platform.isNull journal) then
            journal.EnterDerived ()

        try
            f arg
        finally
            current <- previous
            currentNode <- previousNode
            ownerPending <- previousPending
            untrackedHost <- previousHost
            untrackedInBody <- previousInBody

            if not (Platform.isNull journal) then
                journal.LeaveDerived ()

    member internal _.RunUntrackedPure2(host: IScopeHost, f: 'A -> 'B -> 'T, a: 'A, b: 'B) : 'T =
        let previous = current
        let previousNode = currentNode
        let previousPending = ownerPending
        let previousHost = untrackedHost
        let previousInBody = untrackedInBody
        current <- Platform.nil ()
        currentNode <- Platform.nil ()
        ownerPending <- true
        untrackedHost <- host
        untrackedInBody <- true

        if not (Platform.isNull journal) then
            journal.EnterDerived ()

        try
            f a b
        finally
            current <- previous
            currentNode <- previousNode
            ownerPending <- previousPending
            untrackedHost <- previousHost
            untrackedInBody <- previousInBody

            if not (Platform.isNull journal) then
                journal.LeaveDerived ()
#endif

#if !RANVIER_TRACE
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.EnterUntrackedPureTop(host: IScopeHost) =
        ownerAmbient.EnterFrame ()
        pullDepth <- 1
        ownerPending <- true
        untrackedHost <- host
        untrackedInBody <- true

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal _.CloseLeafPureBody() =
        ownerPending <- false
        untrackedHost <- Platform.nil ()
        untrackedInBody <- false
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

/// <summary>Saved tracking and untracked-host state around an untracked pure callback run (no <c>currentOwner</c> mutation).</summary>
and [<Struct; NoEquality; NoComparison>] internal UntrackedPureFrame =
    val Previous: IComputation
    val PreviousNode: Node
    val PreviousPending: bool
    val PreviousHost: IScopeHost
    val PreviousInBody: bool

    new(previous: IComputation, previousNode: Node, previousPending: bool, previousHost: IScopeHost, previousInBody: bool) =
        {
            Previous = previous
            PreviousNode = previousNode
            PreviousPending = previousPending
            PreviousHost = previousHost
            PreviousInBody = previousInBody
        }

/// <summary>Saved reader identity while a static combinator refreshes its already-linked upstream sources.</summary>
and [<Struct; NoEquality; NoComparison>] internal StaticRefreshFrame =
    val Previous: IComputation
    val PreviousNode: Node

    new(previous: IComputation, previousNode: Node) =
        {
            Previous = previous
            PreviousNode = previousNode
        }
#endif

#if !FABLE_COMPILER && !RANVIER_TRACE
/// <summary>
/// One overflow slot of an <c>ObserverList</c>: the observer (a <c>Node</c> directly when <c>NodeSlotBit</c> is set in
/// <c>Slot</c>, otherwise a non-<c>Node</c> <c>IComputation</c>) and its tagged slot (Theorem B7).
/// </summary>
and [<Struct; NoEquality; NoComparison>] internal NodeSlot =
    val mutable Ref: obj

    /// <summary>The position the observer recorded the edge at, ORed with <c>EdgeHandle.NodeSlotBit</c> when <c>Ref</c> is a <c>Node</c>.</summary>
    val mutable Slot: int

    new(refObj: obj, slot: int) = { Ref = refObj; Slot = slot }

/// <summary>
/// The observers of a signal or a node, held inline by their owner: <c>ObserverSet</c>'s algorithm without its object.
/// </summary>
/// <remarks>
/// <para>
/// No allocation for zero or one observer. Each entry stores a single object reference and a bit-tagged slot (Theorem B7):
/// when <c>EdgeHandle.NodeSlotBit</c> is set in <c>Slot</c>, <c>Ref</c> is a <c>Node</c> reached directly through
/// <c>NodeOps</c> instead of an interface dispatch.
/// </para>
/// <para>
/// The walk order, re-clamping and swap-remove are exactly <c>ObserverSet</c>'s: the differential check pins the
/// order effects are queued in.
/// </para>
/// </remarks>
and [<Struct; NoEquality; NoComparison>] internal ObserverList =
    val mutable FirstRef: obj

    /// <summary>Observers after the first, <c>Rest[i - 1]</c> at handle <c>i</c>. Null until a second observer.</summary>
    val mutable Rest: NodeSlot[]

    val mutable FirstSlot: int
    val mutable Count: int

    /// <summary>The handle the latest add returned, moved along when a swap-remove moves its observer.</summary>
    val mutable Latest: int

    /// <summary>An empty list.</summary>
    static member inline Create() =
        Unchecked.defaultof<ObserverList>

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.ObserverAt(handle: int) : obj =
        if handle = 0 then
            this.FirstRef
        else
            (Platform.itemAt (handle - 1) this.Rest).Ref

    /// <summary>Appends <c>c</c>, recording <c>slot</c> for its <c>MovedIn</c>, and returns its handle.</summary>
    member this.Add(c: IComputation, slot: int) =
        let handle = this.Count

        if handle > EdgeHandle.IndexMask then
            raise (InvalidOperationException "Observer handle capacity exceeded.")

        let refObj: obj = box c

        if handle = 0 then
            this.FirstRef <- refObj
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

            this.Rest[restIdx] <- NodeSlot (refObj, slot)
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
        else
            let refObj: obj = box c

            if
                this.Latest < count
                && obj.ReferenceEquals (this.ObserverAt this.Latest, refObj)
            then
                EdgeHandle.Unlinked
            elif count = 1 then
                if obj.ReferenceEquals (this.FirstRef, refObj) then
                    EdgeHandle.Unlinked
                else
                    this.Add (c, slot)
            elif count <= EdgeHandle.ScanLimit then
                if obj.ReferenceEquals (this.FirstRef, refObj) then
                    EdgeHandle.Unlinked
                else
                    let restCount = count - 1
                    let rest = this.Rest
                    let mutable i = 0

                    while i < restCount
                          && not (obj.ReferenceEquals ((Platform.itemAt i rest).Ref, refObj)) do
                        i <- i + 1

                    if i < restCount then
                        EdgeHandle.Unlinked
                    else
                        this.Add (c, slot)
            else
                EdgeHandle.Unknown

    /// <summary><c>ObserverSet.RemoveAt</c>: fast last-slot pop, otherwise swap-remove notifying the moved observer.</summary>
    member this.RemoveAt(c: IComputation, taggedHandle: int) =
        if taggedHandle >= 0 then
            let handle = taggedHandle &&& EdgeHandle.IndexMask
            let count = this.Count

            if
                handle >= count
                || not (obj.ReferenceEquals (this.ObserverAt handle, box c))
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

            if handle = last then
                if last = 0 then
                    this.FirstRef <- null
                    this.FirstSlot <- EdgeHandle.Unlinked
                    this.Count <- 0
                else
                    this.Rest[last - 1] <- NodeSlot (null, EdgeHandle.Unlinked)
                    this.Count <- last
            elif handle = 0 then
                let lastRest = last - 1
                let movedEntry = Platform.itemAt lastRest this.Rest
                this.Rest[lastRest] <- NodeSlot (null, EdgeHandle.Unlinked)
                this.FirstRef <- movedEntry.Ref
                this.FirstSlot <- movedEntry.Slot
                this.Count <- last

                if this.Latest = last then
                    this.Latest <- 0

                NodeOps.MovedIn (movedEntry.Ref, movedEntry.Slot, 0)
            else
                let lastRest = last - 1
                let movedEntry = Platform.itemAt lastRest this.Rest
                this.Rest[handle - 1] <- movedEntry
                this.Rest[lastRest] <- NodeSlot (null, EdgeHandle.Unlinked)
                this.Count <- last

                if this.Latest = last then
                    this.Latest <- handle

                NodeOps.MovedIn (movedEntry.Ref, movedEntry.Slot, handle)

    /// <summary><c>ObserverSet.NotifyDirty</c>: marks every current observer dirty, walking backwards, re-clamped.</summary>
    member this.NotifyDirty() =
        let count = this.Count

        if count = 1 then
            NodeOps.MarkDirtyOf (this.FirstRef, this.FirstSlot)
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= this.Count then
                    i <- this.Count - 1

                if i > 0 then
                    let entry = Platform.itemAt (i - 1) this.Rest
                    NodeOps.MarkDirtyOf (entry.Ref, entry.Slot)
                    i <- i - 1

            if this.Count > 0 then
                NodeOps.MarkDirtyOf (this.FirstRef, this.FirstSlot)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member private MarkIdle(refObj: obj, slot: int) =
        if EdgeHandle.isNodeSlot slot then
            let n = Platform.fastCast<Node> refObj

            if (n.Flags &&& NodeFlags.CleanMemoMask) = NodeFlags.KindMemo then
                let m = Platform.fastCast<MemoNode> n
                m.Flags <- m.Flags ||| NodeFlags.FreshDirty

                if m.Observers.Count <> 0 then
                    m.Graph.NoteStaleTransition ()
                    NodeOps.NotifyCheckSpine m
            else
                NodeOps.MarkDirty n
        else
            (Platform.fastCast<IComputation> refObj).MarkDirty()

    /// <summary>Marks observers in reverse order while the graph has no active pull or flush.</summary>
    member this.NotifyDirtyIdle() =
        let mutable rest = this.Rest
        let mutable i = this.Count - 1

        while i > 0 do
            let entry = Platform.itemAt (i - 1) rest
            i <- i - 1

            if EdgeHandle.isNodeSlot entry.Slot then
                let n = Platform.fastCast<Node> entry.Ref

                if (n.Flags &&& NodeFlags.CleanMemoMask) = NodeFlags.KindMemo then
                    let m = Platform.fastCast<MemoNode> n
                    m.Flags <- m.Flags ||| NodeFlags.FreshDirty

                    if m.Observers.Count <> 0 then
                        m.Graph.NoteStaleTransition ()
                        NodeOps.NotifyCheckSpine m
                        rest <- this.Rest

                        if i >= this.Count then
                            i <- this.Count - 1
                else
                    NodeOps.MarkDirty n
                    rest <- this.Rest

                    if i >= this.Count then
                        i <- this.Count - 1
            else
                (Platform.fastCast<IComputation> entry.Ref).MarkDirty()
                rest <- this.Rest

                if i >= this.Count then
                    i <- this.Count - 1

        if this.Count > 0 then
            ObserverList.MarkIdle (this.FirstRef, this.FirstSlot)

    /// <summary><c>ObserverSet.NotifyDirtyExcept</c>.</summary>
    member this.NotifyDirtyExcept(running: IComputation) =
        let count = this.Count
        let runObj: obj = box running

        if count = 1 then
            let refObj = this.FirstRef

            if
                not (obj.ReferenceEquals (refObj, runObj))
                || NodeOps.HasReadSlotOf (refObj, this.FirstSlot)
            then
                NodeOps.MarkDirtyOf (refObj, this.FirstSlot)
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= this.Count then
                    i <- this.Count - 1

                if i > 0 then
                    let entry = Platform.itemAt (i - 1) this.Rest

                    if
                        not (obj.ReferenceEquals (entry.Ref, runObj))
                        || NodeOps.HasReadSlotOf (entry.Ref, entry.Slot)
                    then
                        NodeOps.MarkDirtyOf (entry.Ref, entry.Slot)

                    i <- i - 1

            if this.Count > 0 then
                let refObj = this.FirstRef

                if
                    not (obj.ReferenceEquals (refObj, runObj))
                    || NodeOps.HasReadSlotOf (refObj, this.FirstSlot)
                then
                    NodeOps.MarkDirtyOf (refObj, this.FirstSlot)

    /// <summary><c>ObserverSet.NotifyDirtyOther</c>.</summary>
    member this.NotifyDirtyOther(running: IComputation) =
        let count = this.Count
        let runObj: obj = box running

        if count = 1 then
            let refObj = this.FirstRef

            if not (obj.ReferenceEquals (refObj, runObj)) then
                NodeOps.MarkDirtyOf (refObj, this.FirstSlot)
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= this.Count then
                    i <- this.Count - 1

                if i > 0 then
                    let entry = Platform.itemAt (i - 1) this.Rest

                    if not (obj.ReferenceEquals (entry.Ref, runObj)) then
                        NodeOps.MarkDirtyOf (entry.Ref, entry.Slot)

                    i <- i - 1

            if this.Count > 0 then
                let refObj = this.FirstRef

                if not (obj.ReferenceEquals (refObj, runObj)) then
                    NodeOps.MarkDirtyOf (refObj, this.FirstSlot)

    /// <summary><c>ObserverSet.NotifyCheck</c>.</summary>
    member this.NotifyCheck() =
        let count = this.Count

        if count = 1 then
            NodeOps.MarkCheckOf (this.FirstRef, this.FirstSlot)
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= this.Count then
                    i <- this.Count - 1

                if i > 0 then
                    let entry = Platform.itemAt (i - 1) this.Rest
                    NodeOps.MarkCheckOf (entry.Ref, entry.Slot)
                    i <- i - 1

            if this.Count > 0 then
                NodeOps.MarkCheckOf (this.FirstRef, this.FirstSlot)
#endif

#if FABLE_COMPILER && !RANVIER_TRACE
/// <summary>
/// The observers of a signal or a node under Fable: <c>ObserverSet</c>'s walk order, re-clamping and swap-remove,
/// with bit-tagged slot entries in two parallel arrays (Theorem B7).
/// </summary>
and [<Sealed; AllowNullLiteral; NoEquality; NoComparison>] internal ObserverList =
    val mutable FirstRef: obj
    val mutable FirstSlot: int
    val mutable Count: int

    /// <summary>The handle the latest add returned, moved along when a swap-remove moves its observer.</summary>
    val mutable Latest: int

    /// <summary>Observers after the first, <c>Rest[i - 1]</c> at handle <c>i</c>.</summary>
    val mutable Rest: obj[]

    /// <summary>The tagged position each observer in <c>Rest</c> recorded the edge at.</summary>
    val mutable RestSlots: int[]

    new() =
        {
            FirstRef = Platform.nil ()
            FirstSlot = EdgeHandle.Unlinked
            Count = 0
            Latest = 0
            Rest = ObserverSlots.noRefs
            RestSlots = ObserverSlots.noSlots
        }

    /// <summary>An empty list.</summary>
    static member inline Create() =
        ObserverList ()

    member inline private this.ObserverAt(handle: int) : obj =
        if handle = 0 then
            this.FirstRef
        else
            Platform.itemAt (handle - 1) this.Rest

    member private this.Grow(restCount: int) =
        let size = if restCount < 2 then 4 else restCount * 2
        let refs = Array.zeroCreate size
        let slots = Platform.positions size
        Array.blit this.Rest 0 refs 0 restCount
        Platform.copyPositions this.RestSlots slots restCount
        this.Rest <- refs
        this.RestSlots <- slots

    /// <summary>Blanks the overflow entry at <c>index</c>, so a dropped observer is not kept alive.</summary>
    member inline private this.Blank(index: int) =
        Platform.setItemAt index this.Rest (Platform.nil ())
        Platform.setItemAt index this.RestSlots EdgeHandle.Unlinked

    /// <summary>Appends <c>c</c>, recording <c>slot</c> for its <c>MovedIn</c>, and returns its handle.</summary>
    member this.Add(c: IComputation, slot: int) =
        let handle = this.Count

        if handle > EdgeHandle.IndexMask then
            raise (InvalidOperationException "Observer handle capacity exceeded.")

        let refObj: obj = box c

        if handle = 0 then
            this.FirstRef <- refObj
            this.FirstSlot <- slot
            this.Count <- 1
            this.Latest <- 0
        else
            let restIdx = handle - 1

            if restIdx = this.Rest.Length then
                this.Grow restIdx

            Platform.setItemAt restIdx this.Rest refObj
            Platform.setItemAt restIdx this.RestSlots slot
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
        else
            let refObj: obj = box c

            if
                this.Latest < count
                && obj.ReferenceEquals (this.ObserverAt this.Latest, refObj)
            then
                EdgeHandle.Unlinked
            elif count = 1 then
                if obj.ReferenceEquals (this.FirstRef, refObj) then
                    EdgeHandle.Unlinked
                else
                    this.Add (c, slot)
            elif count <= EdgeHandle.ScanLimit then
                if obj.ReferenceEquals (this.FirstRef, refObj) then
                    EdgeHandle.Unlinked
                else
                    let restCount = count - 1
                    let rest = this.Rest
                    let mutable i = 0

                    while i < restCount
                          && not (obj.ReferenceEquals (Platform.itemAt i rest, refObj)) do
                        i <- i + 1

                    if i < restCount then
                        EdgeHandle.Unlinked
                    else
                        this.Add (c, slot)
            else
                EdgeHandle.Unknown

    /// <summary><c>ObserverSet.RemoveAt</c>: fast last-slot pop, otherwise swap-remove notifying the moved observer.</summary>
    member this.RemoveAt(c: IComputation, taggedHandle: int) =
        if taggedHandle >= 0 then
            let handle = taggedHandle &&& EdgeHandle.IndexMask
            let count = this.Count

            if
                handle >= count
                || not (obj.ReferenceEquals (this.ObserverAt handle, box c))
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

            if handle = last then
                if last = 0 then
                    this.FirstRef <- Platform.nil ()
                    this.FirstSlot <- EdgeHandle.Unlinked
                    this.Count <- 0
                else
                    this.Blank (last - 1)
                    this.Count <- last
            elif handle = 0 then
                let lastRest = last - 1
                let movedRef = Platform.itemAt lastRest this.Rest
                let movedSlot = Platform.itemAt lastRest this.RestSlots
                this.Blank lastRest
                this.FirstRef <- movedRef
                this.FirstSlot <- movedSlot
                this.Count <- last

                if this.Latest = last then
                    this.Latest <- 0

                NodeOps.MovedIn (movedRef, movedSlot, 0)
            else
                let handleRest = handle - 1
                let lastRest = last - 1
                let movedRef = Platform.itemAt lastRest this.Rest
                let movedSlot = Platform.itemAt lastRest this.RestSlots
                Platform.setItemAt handleRest this.Rest movedRef
                Platform.setItemAt handleRest this.RestSlots movedSlot
                this.Blank lastRest
                this.Count <- last

                if this.Latest = last then
                    this.Latest <- handle

                NodeOps.MovedIn (movedRef, movedSlot, handle)

    /// <summary><c>ObserverSet.NotifyDirty</c>: marks every current observer dirty, walking backwards, re-clamped.</summary>
    member this.NotifyDirty() =
        let count = this.Count

        if count = 1 then
            NodeOps.MarkDirtyOf (this.FirstRef, this.FirstSlot)
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= this.Count then
                    i <- this.Count - 1

                if i > 0 then
                    let idx = i - 1
                    NodeOps.MarkDirtyOf (Platform.itemAt idx this.Rest, Platform.itemAt idx this.RestSlots)
                    i <- i - 1

            if this.Count > 0 then
                NodeOps.MarkDirtyOf (this.FirstRef, this.FirstSlot)

    /// <summary>Marks observers in reverse order while the graph has no active pull or flush.</summary>
    member this.NotifyDirtyIdle() =
        NodeOps.NotifyDirtyIdle this

    /// <summary><c>ObserverSet.NotifyDirtyExcept</c>.</summary>
    member this.NotifyDirtyExcept(running: IComputation) =
        let count = this.Count
        let runObj: obj = box running

        if count = 1 then
            let refObj = this.FirstRef

            if
                not (obj.ReferenceEquals (refObj, runObj))
                || NodeOps.HasReadSlotOf (refObj, this.FirstSlot)
            then
                NodeOps.MarkDirtyOf (refObj, this.FirstSlot)
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= this.Count then
                    i <- this.Count - 1

                if i > 0 then
                    let idx = i - 1
                    let refObj = Platform.itemAt idx this.Rest
                    let slot = Platform.itemAt idx this.RestSlots

                    if
                        not (obj.ReferenceEquals (refObj, runObj))
                        || NodeOps.HasReadSlotOf (refObj, slot)
                    then
                        NodeOps.MarkDirtyOf (refObj, slot)

                    i <- i - 1

            if this.Count > 0 then
                let refObj = this.FirstRef

                if
                    not (obj.ReferenceEquals (refObj, runObj))
                    || NodeOps.HasReadSlotOf (refObj, this.FirstSlot)
                then
                    NodeOps.MarkDirtyOf (refObj, this.FirstSlot)

    /// <summary><c>ObserverSet.NotifyDirtyOther</c>.</summary>
    member this.NotifyDirtyOther(running: IComputation) =
        let count = this.Count
        let runObj: obj = box running

        if count = 1 then
            let refObj = this.FirstRef

            if not (obj.ReferenceEquals (refObj, runObj)) then
                NodeOps.MarkDirtyOf (refObj, this.FirstSlot)
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= this.Count then
                    i <- this.Count - 1

                if i > 0 then
                    let idx = i - 1
                    let refObj = Platform.itemAt idx this.Rest

                    if not (obj.ReferenceEquals (refObj, runObj)) then
                        NodeOps.MarkDirtyOf (refObj, Platform.itemAt idx this.RestSlots)

                    i <- i - 1

            if this.Count > 0 then
                let refObj = this.FirstRef

                if not (obj.ReferenceEquals (refObj, runObj)) then
                    NodeOps.MarkDirtyOf (refObj, this.FirstSlot)

    /// <summary><c>ObserverSet.NotifyCheck</c>.</summary>
    member this.NotifyCheck() =
        let count = this.Count

        if count = 1 then
            NodeOps.MarkCheckOf (this.FirstRef, this.FirstSlot)
        elif count > 1 then
            let mutable i = count - 1

            while i > 0 do
                if i >= this.Count then
                    i <- this.Count - 1

                if i > 0 then
                    let idx = i - 1
                    NodeOps.MarkCheckOf (Platform.itemAt idx this.Rest, Platform.itemAt idx this.RestSlots)
                    i <- i - 1

            if this.Count > 0 then
                NodeOps.MarkCheckOf (this.FirstRef, this.FirstSlot)
#endif

/// <summary>
/// Shared reactive base of <c>MemoNode</c>, <c>EffectNode</c>, and <c>EffectOn&lt;'T&gt;</c>. Inherits <c>OwnerLink</c>
/// directly so built-in computations attach to their <c>Owner</c> with zero link allocations.
/// </summary>
#if FABLE_COMPILER && !RANVIER_TRACE
and [<AbstractClass; NoEquality; NoComparison; CompiledName("Node")>] internal ComputationNode(id: int, graph: Graph, initialFlags: int) =
    inherit OwnerLink(Platform.nil (), -1, 0)
    let mutable runsField = 0
    let mutable failureField: Failure = Platform.nil ()
    let mutable pendingSourceField: INode = Platform.nil ()
    let mutable scopeField: Owner = Platform.nil ()
    // Self-hosted SourceList fields: identical JS property names to `SourceList`, so `SourcesField` is a zero-cost
    // cast of `this` and creating a Memo/Effect/EffectOn allocates zero `SourceList` objects.
    let mutable first: ISource = Platform.nil ()
    let mutable second: ISource = Platform.nil ()
    let mutable rest: SourceSlot[] = SourceSlots.none
    let mutable restHandles: int[] = SourceSlots.noHandles
    let mutable held: Platform.RefIndex<ISource> = Platform.nil ()
    let mutable firstHandle = EdgeHandle.Unlinked
    let mutable secondHandle = EdgeHandle.Unlinked
    let mutable count = 0
    let mutable cursor = 0
    let mutable flags = initialFlags

    // Keeps all 9 self-hosted SourceList fields as instance properties on Node (never called at runtime).
    member private _.KeepSourceListLayout() =
        first <- Platform.nil ()
        second <- Platform.nil ()
        rest <- SourceSlots.none
        restHandles <- SourceSlots.noHandles
        held <- Platform.nil ()
        firstHandle <- EdgeHandle.Unlinked
        secondHandle <- EdgeHandle.Unlinked
        count <- 0
        cursor <- 0

    member inline internal _.NodeId = id
    member inline internal _.Graph = graph

    member inline internal _.RunsField
        with get () = runsField
        and set v = runsField <- v

    member inline internal _.FailureField
        with get () = failureField
        and set v = failureField <- v

    member inline internal _.PendingSourceField
        with get () = pendingSourceField
        and set v = pendingSourceField <- v

    member inline internal _.ScopeField
        with get () = scopeField
        and set v = scopeField <- v

    member inline internal _.Flags
        with get () = flags
        and set v = flags <- v

    member inline internal this.SourcesField: SourceList =
        Platform.fastCast<SourceList>(box this)

    member inline internal _.Freshness: Freshness =
        LanguagePrimitives.EnumOfValue<byte, Freshness>(
            byte (
                (flags &&& NodeFlags.FreshMask)
                >>> NodeFlags.FreshShift
            )
        )

    member inline internal _.Status: Status =
        LanguagePrimitives.EnumOfValue<byte, Status>(
            byte (
                (flags &&& NodeFlags.StatusMask)
                >>> NodeFlags.StatusShift
            )
        )

    member inline internal _.Disposed = (flags &&& NodeFlags.Disposed) <> 0
    member inline internal _.Unannounced = (flags &&& NodeFlags.Unannounced) <> 0
    member inline internal _.Published = (flags &&& NodeFlags.Published) <> 0

    member inline internal _.Publishes
        with get () = (flags &&& NodeFlags.Publishes) <> 0
        and set (v: bool) =
            if v then
                flags <- flags ||| NodeFlags.Publishes
            else
                flags <- flags &&& ~~~NodeFlags.Publishes

    member inline internal _.IsDisposed = (flags &&& NodeFlags.Disposed) <> 0

    member inline internal _.SetDisposed() =
        flags <- flags ||| NodeFlags.Disposed

    member inline internal _.SetStatusPending() =
        flags <-
            (flags &&& ~~~NodeFlags.StatusMask)
            ||| NodeFlags.StatusPending

    member inline internal _.SetStatusError() =
        flags <-
            (flags &&& ~~~NodeFlags.StatusMask)
            ||| NodeFlags.StatusError

    member inline internal _.SetStatusNone() =
        flags <- flags &&& ~~~NodeFlags.StatusMask
#else
#if FABLE_COMPILER
and [<AbstractClass; NoEquality; NoComparison; CompiledName("Node")>] internal ComputationNode
    (id: int, kind: byte, graph: Graph, freshness: Freshness) =
    inherit OwnerLink(Platform.nil (), -1, 0)
    let mutable runsField = 0
    let mutable failureField: Failure = Platform.nil ()
    let mutable pendingSourceField: INode = Platform.nil ()
    let mutable scopeField: Owner = Platform.nil ()
    let mutable sourcesField = SourceList.Create ()
    let mutable freshnessField = freshness
    let mutable statusField = Status.Uninitialized
    let mutable activeField = 0uy
    let mutable disposedField = false
    let mutable unannouncedField = false
    let mutable queuedField = false
    let mutable inCleanupField = false
    let mutable publishesField = false
    let mutable violatedField = false
    let mutable publishedField = false

    member inline internal _.NodeId = id
    member inline internal _.Graph = graph
    member inline internal _.Kind = kind

    member inline internal _.RunsField
        with get () = runsField
        and set v = runsField <- v

    member inline internal _.FailureField
        with get () = failureField
        and set v = failureField <- v

    member inline internal _.PendingSourceField
        with get () = pendingSourceField
        and set v = pendingSourceField <- v

    member inline internal _.ScopeField
        with get () = scopeField
        and set v = scopeField <- v

    member inline internal _.SourcesField
        with get () = sourcesField
        and set v = sourcesField <- v

    member inline internal _.Freshness
        with get () = freshnessField
        and set v = freshnessField <- v

    member inline internal _.StatusField
        with get () = statusField
        and set v = statusField <- v

    member inline internal _.Active
        with get () = activeField
        and set v = activeField <- v

    member inline internal _.Disposed
        with get () = disposedField
        and set v = disposedField <- v

    member inline internal _.Unannounced
        with get () = unannouncedField
        and set v = unannouncedField <- v

    member inline internal _.Queued
        with get () = queuedField
        and set v = queuedField <- v

    member inline internal _.InCleanup
        with get () = inCleanupField
        and set v = inCleanupField <- v

    member inline internal _.Publishes
        with get () = publishesField
        and set v = publishesField <- v

    member inline internal _.Violated
        with get () = violatedField
        and set v = violatedField <- v

    member inline internal _.Published
        with get () = publishedField
        and set v = publishedField <- v

    member inline internal _.Status = statusField
    member inline internal _.IsDisposed = disposedField

    member inline internal _.SetDisposed() =
        disposedField <- true

    member inline internal _.SetStatusPending() =
        statusField <- Status.Pending

    member inline internal _.SetStatusError() =
        statusField <- Status.Error

    member inline internal _.SetStatusNone() =
        statusField <- Status.None
#else
and [<AbstractClass; NoEquality; NoComparison>] internal Node =
    inherit OwnerLink
    val internal NodeId: int
    val internal Graph: Graph
    val mutable internal RunsField: int
    val mutable internal FailureField: Failure
    val mutable internal PendingSourceField: INode
    val mutable internal ScopeField: Owner
    val mutable internal SourcesField: SourceList

#if !RANVIER_TRACE
    /// <summary>Bit-packed reactive flags (see <c>NodeFlags</c>, Theorem B8).</summary>
    val mutable internal Flags: int

    internal new(id: int, graph: Graph, initialFlags: int) =
        {
            inherit OwnerLink(null, -1, 0)
            NodeId = id
            Graph = graph
            RunsField = 0
            FailureField = null
            PendingSourceField = Unchecked.defaultof<INode>
            ScopeField = Unchecked.defaultof<Owner>
            SourcesField = SourceList.Create ()
            Flags = initialFlags
        }

    // Internal CLR properties kept for conformance/differential reflection (`Harness.fsx`, `Generate.fsx`).
    member internal this.Kind = byte (this.Flags &&& NodeFlags.KindMask)

    member internal this.Freshness
        with get (): Freshness =
            LanguagePrimitives.EnumOfValue<byte, Freshness>(
                byte (
                    (this.Flags &&& NodeFlags.FreshMask)
                    >>> NodeFlags.FreshShift
                )
            )
        and set (v: Freshness) =
            this.Flags <-
                (this.Flags &&& ~~~NodeFlags.FreshMask)
                ||| (((int v) <<< NodeFlags.FreshShift)
                     &&& NodeFlags.FreshMask)

    member internal this.Status
        with get (): Status =
            LanguagePrimitives.EnumOfValue<byte, Status>(
                byte (
                    (this.Flags &&& NodeFlags.StatusMask)
                    >>> NodeFlags.StatusShift
                )
            )
        and set (v: Status) =
            this.Flags <-
                (this.Flags &&& ~~~NodeFlags.StatusMask)
                ||| (((int v) <<< NodeFlags.StatusShift)
                     &&& NodeFlags.StatusMask)

    member internal this.Active
        with get (): byte =
            byte (
                (this.Flags &&& NodeFlags.ActiveMask)
                >>> NodeFlags.ActiveShift
            )
        and set (v: byte) =
            this.Flags <-
                (this.Flags &&& ~~~NodeFlags.ActiveMask)
                ||| (((int v) <<< NodeFlags.ActiveShift)
                     &&& NodeFlags.ActiveMask)

    member internal this.Disposed
        with get () = (this.Flags &&& NodeFlags.Disposed) <> 0
        and set (v: bool) =
            if v then
                this.Flags <- this.Flags ||| NodeFlags.Disposed
            else
                this.Flags <- this.Flags &&& ~~~NodeFlags.Disposed

    member internal this.Unannounced
        with get () = (this.Flags &&& NodeFlags.Unannounced) <> 0
        and set (v: bool) =
            if v then
                this.Flags <- this.Flags ||| NodeFlags.Unannounced
            else
                this.Flags <- this.Flags &&& ~~~NodeFlags.Unannounced

    member internal this.Queued
        with get () = (this.Flags &&& NodeFlags.Queued) <> 0
        and set (v: bool) =
            if v then
                this.Flags <- this.Flags ||| NodeFlags.Queued
            else
                this.Flags <- this.Flags &&& ~~~NodeFlags.Queued

    member internal this.InCleanup
        with get () = (this.Flags &&& NodeFlags.InCleanup) <> 0
        and set (v: bool) =
            if v then
                this.Flags <- this.Flags ||| NodeFlags.InCleanup
            else
                this.Flags <- this.Flags &&& ~~~NodeFlags.InCleanup

    member internal this.Publishes
        with get () = (this.Flags &&& NodeFlags.Publishes) <> 0
        and set (v: bool) =
            if v then
                this.Flags <- this.Flags ||| NodeFlags.Publishes
            else
                this.Flags <- this.Flags &&& ~~~NodeFlags.Publishes

    member internal this.Violated
        with get () = (this.Flags &&& NodeFlags.Violated) <> 0
        and set (v: bool) =
            if v then
                this.Flags <- this.Flags ||| NodeFlags.Violated
            else
                this.Flags <- this.Flags &&& ~~~NodeFlags.Violated

    member internal this.Published
        with get () = (this.Flags &&& NodeFlags.Published) <> 0
        and set (v: bool) =
            if v then
                this.Flags <- this.Flags ||| NodeFlags.Published
            else
                this.Flags <- this.Flags &&& ~~~NodeFlags.Published

    member internal this.Runs = this.RunsField
    member internal this.Failure = this.FailureField
    member internal this.Sources = this.SourcesField

    member inline internal this.IsDisposed = (this.Flags &&& NodeFlags.Disposed) <> 0

    member inline internal this.SetDisposed() =
        this.Flags <- this.Flags ||| NodeFlags.Disposed

    member inline internal this.SetStatusPending() =
        this.Flags <-
            (this.Flags &&& ~~~NodeFlags.StatusMask)
            ||| NodeFlags.StatusPending

    member inline internal this.SetStatusError() =
        this.Flags <-
            (this.Flags &&& ~~~NodeFlags.StatusMask)
            ||| NodeFlags.StatusError

    member inline internal this.SetStatusNone() =
        this.Flags <- this.Flags &&& ~~~NodeFlags.StatusMask
#else
    val internal KindField: byte
    val mutable internal FreshnessField: Freshness
    val mutable internal StatusField: Status
    val mutable internal ActiveField: byte
    val mutable internal DisposedField: bool
    val mutable internal UnannouncedField: bool
    val mutable internal QueuedField: bool
    val mutable internal InCleanupField: bool
    val mutable internal PublishesField: bool
    val mutable internal ViolatedField: bool
    val mutable internal PublishedField: bool

    internal new(id: int, kind: byte, graph: Graph, freshness: Freshness) =
        {
            inherit OwnerLink(null, -1, 0)
            NodeId = id
            Graph = graph
            RunsField = 0
            FailureField = null
            PendingSourceField = Unchecked.defaultof<INode>
            ScopeField = Unchecked.defaultof<Owner>
            SourcesField = SourceList.Create ()
            KindField = kind
            FreshnessField = freshness
            StatusField = Status.Uninitialized
            ActiveField = 0uy
            DisposedField = false
            UnannouncedField = false
            QueuedField = false
            InCleanupField = false
            PublishesField = false
            ViolatedField = false
            PublishedField = false
        }

    member internal this.Kind = this.KindField

    member internal this.Freshness
        with get () = this.FreshnessField
        and set (v: Freshness) = this.FreshnessField <- v

    member internal this.Status
        with get () = this.StatusField
        and set (v: Status) = this.StatusField <- v

    member internal this.Active
        with get () = this.ActiveField
        and set (v: byte) = this.ActiveField <- v

    member internal this.Disposed
        with get () = this.DisposedField
        and set (v: bool) = this.DisposedField <- v

    member internal this.Unannounced
        with get () = this.UnannouncedField
        and set (v: bool) = this.UnannouncedField <- v

    member internal this.Queued
        with get () = this.QueuedField
        and set (v: bool) = this.QueuedField <- v

    member internal this.InCleanup
        with get () = this.InCleanupField
        and set (v: bool) = this.InCleanupField <- v

    member internal this.Publishes
        with get () = this.PublishesField
        and set (v: bool) = this.PublishesField <- v

    member internal this.Violated
        with get () = this.ViolatedField
        and set (v: bool) = this.ViolatedField <- v

    member internal this.Published
        with get () = this.PublishedField
        and set (v: bool) = this.PublishedField <- v

    member internal this.Runs = this.RunsField
    member internal this.Failure = this.FailureField
    member internal this.Sources = this.SourcesField

    member inline internal this.IsDisposed = this.DisposedField

    member inline internal this.SetDisposed() =
        this.DisposedField <- true

    member inline internal this.SetStatusPending() =
        this.StatusField <- Status.Pending

    member inline internal this.SetStatusError() =
        this.StatusField <- Status.Error

    member inline internal this.SetStatusNone() =
        this.StatusField <- Status.None
#endif
#endif
#endif

    /// <summary>Disposes this node when torn down by its <c>Owner</c>, skipping redundant <c>OwnerLink.Detach</c>.</summary>
    member internal this.DisposeFromOwner() =
        let g = this.Graph

        g.Entered (
            "Disposing a node",
            fun () ->
                if not this.IsDisposed then
                    this.SetDisposed ()
                    Tracer.NodeDispose (g, this.NodeId)
                    this.SourcesField.Clear (this :> IComputation)

                    if not (Platform.isNull (box this.ScopeField)) then
                        g.Retire this.ScopeField
        )

    /// <summary>Disposes this node from user code, unlinking from its enclosing <c>Owner</c> first.</summary>
    member internal this.DisposeFromUser() =
        let g = this.Graph

        g.Entered (
            "Disposing a node",
            fun () ->
                if not this.IsDisposed then
                    this.SetDisposed ()
                    Tracer.NodeDispose (g, this.NodeId)
                    this.Detach ()
                    this.SourcesField.Clear (this :> IComputation)

                    if not (Platform.isNull (box this.ScopeField)) then
                        g.Retire this.ScopeField
        )

    override this.ReleaseChild() =
        this.DisposeFromOwner ()

    abstract RunDirect: unit -> unit
    abstract CanOwnNode: bool
    abstract GetScopeNode: unit -> Owner

    interface INode with
        member this.Id = this.NodeId

        member this.Status =
            this.Status
            ||| Staleness.ofFreshness this.Freshness

    interface IComputation with
        member this.AddSource s =
            NodeOps.AddSource (this, s)

        member this.MovedIn(slot, handle) =
            this.SourcesField.MovedIn (slot &&& EdgeHandle.SlotMask, handle)

        member this.HasReadSlot slot =
            (slot &&& EdgeHandle.SlotMask) < this.SourcesField.Cursor

        member this.MarkDirty() =
            NodeOps.MarkDirty this

        member this.MarkCheck() =
            NodeOps.MarkCheck this

    interface IScopeHost with
        member this.CanOwn = this.CanOwnNode
        member this.Scope = this.GetScopeNode ()

/// <summary>
/// Reactive state of a <c>Memo&lt;'T&gt;</c>: adds <c>Observers</c>, <c>CheckParent</c>, and <c>ISource</c>
/// (tagging returned handles with <c>EdgeHandle.MemoBit</c>, Theorem B7).
/// </summary>
#if FABLE_COMPILER && !RANVIER_TRACE
and [<Sealed; NoEquality; NoComparison>] internal MemoNode(g: Graph, mode: ScopeMode, safeEq: bool) =
    inherit
        Node(
            g.NextId (),
            g,
            (if safeEq then
                 NodeFlags.InitMemoSafeEq
             else
                 NodeFlags.InitMemo)
        )

    let mutable observers: ObserverList = Platform.nil ()
    let mutable checkParent: MemoNode = Platform.nil ()
    let mutable ownerMemo: obj = Platform.nil ()
    let mutable adopted: IDisposable = Platform.nil ()

    member inline internal _.Observers
        with get () = observers
        and set v = observers <- v

    member inline internal _.CheckParent
        with get () = checkParent
        and set v = checkParent <- v

    member inline internal _.OwnerMemo
        with get () = ownerMemo
        and set v = ownerMemo <- v

    member inline internal _.Adopted
        with get () = adopted
        and set v = adopted <- v

    member inline internal _.Mode = mode
#else
#if FABLE_COMPILER
and [<Sealed; NoEquality; NoComparison>] internal MemoNode(g: Graph, mode: ScopeMode, runCallback: MemoNode -> unit) =
    inherit Node(g.NextId (), NodeKind.Memo, g, Freshness.Dirty)
    let mutable observers = ObserverList.Create ()
    let mutable ownerMemo: obj = Platform.nil ()
    let mutable adopted: IDisposable = Platform.nil ()

    member inline internal _.Observers
        with get () = observers
        and set v = observers <- v

    member inline internal _.OwnerMemo
        with get () = ownerMemo
        and set v = ownerMemo <- v

    member inline internal _.Adopted
        with get () = adopted
        and set v = adopted <- v

    member inline internal _.Mode = mode
    member inline internal _.RunCallback = runCallback
#else
and [<Sealed; NoEquality; NoComparison>] internal MemoNode =
    inherit Node
    val mutable internal Observers: ObserverList
#if !RANVIER_TRACE
    /// <summary>The next resolving ancestor while this memo is on the single-source check stack.</summary>
    val mutable internal CheckParent: MemoNode
#endif
    /// <summary>The owning <c>Memo&lt;'T&gt;</c> wrapper, set at construction.</summary>
    val mutable internal OwnerMemo: obj
    /// <summary>The companion disposed with the memo, or null.</summary>
    val mutable internal Adopted: IDisposable
    val internal Mode: ScopeMode
#if !RANVIER_TRACE
    /// <summary>Runs the memo; when <c>child</c> is non-null and sole observer with 0 stale transitions, returns <c>moved</c> directly (Theorem B11).</summary>
    val internal RunCallback: Func<MemoNode, Node, bool>

    internal new(graph: Graph, mode: ScopeMode, safeEq: bool, runCallback: Func<MemoNode, Node, bool>) =
        {
            inherit
                Node(
                    graph.NextId (),
                    graph,
                    (if safeEq then
                         NodeFlags.InitMemoSafeEq
                     else
                         NodeFlags.InitMemo)
                )
            Observers = ObserverList.Create ()
            CheckParent = Unchecked.defaultof<MemoNode>
            OwnerMemo = null
            Adopted = null
            Mode = mode
            RunCallback = runCallback
        }
#else
    val internal RunCallback: Action<MemoNode>

    internal new(graph: Graph, mode: ScopeMode, runCallback: Action<MemoNode>) =
        {
            inherit Node(graph.NextId (), NodeKind.Memo, graph, Freshness.Dirty)
            Observers = ObserverList.Create ()
            OwnerMemo = null
            Adopted = null
            Mode = mode
            RunCallback = runCallback
        }
#endif
#endif
#endif

#if FABLE_COMPILER && !RANVIER_TRACE
    member inline internal this.EnsureObservers() : ObserverList =
        let mutable obs = this.Observers

        if Platform.isNull obs then
            obs <- ObserverList ()
            this.Observers <- obs

        obs

    member inline internal this.ObserverCount =
        if Platform.isNull this.Observers then
            0
        else
            this.Observers.Count
#else
    member inline internal this.ObserverCount = this.Observers.Count
#endif

    member internal this.DisposeMemoFromOwner() =
        let g = this.Graph

        g.Entered (
            "Disposing a node",
            fun () ->
                if not this.IsDisposed then
                    this.SetDisposed ()
                    Tracer.NodeDispose (g, this.NodeId)
#if !RANVIER_TRACE
                    if
                        (this.Flags &&& NodeFlags.FusedMap) <> 0
                        && not (Platform.isNull this.OwnerMemo)
                    then
                        (Platform.fastCast<IMemoHandle> this.OwnerMemo).OnOwnerDispose()
#endif
                    this.SourcesField.Clear (this :> IComputation)

                    if not (Platform.isNull (box this.ScopeField)) then
                        g.Retire this.ScopeField

                    if not (Platform.isNull this.Adopted) then
                        let companion = this.Adopted
                        this.Adopted <- Platform.nil ()
                        companion.Dispose ()
        )

    member internal this.DisposeMemoFromUser() =
        let g = this.Graph

        g.Entered (
            "Disposing a node",
            fun () ->
                if not this.IsDisposed then
                    this.SetDisposed ()
                    Tracer.NodeDispose (g, this.NodeId)
                    this.Detach ()
                    this.SourcesField.Clear (this :> IComputation)

                    if not (Platform.isNull (box this.ScopeField)) then
                        g.Retire this.ScopeField

                    if not (Platform.isNull this.Adopted) then
                        let companion = this.Adopted
                        this.Adopted <- Platform.nil ()
                        companion.Dispose ()
        )

    override this.RunDirect() =
#if !FABLE_COMPILER && !RANVIER_TRACE
        this.RunCallback.Invoke (this, Unchecked.defaultof<Node>)
        |> ignore
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        (Platform.fastCast<Memo<obj>> this.OwnerMemo).RunFromNode this
#else
#if FABLE_COMPILER
        this.RunCallback this
#else
        this.RunCallback.Invoke this
#endif
#endif
#endif

    override this.ReleaseChild() =
        this.DisposeMemoFromOwner ()

    override this.CanOwnNode = this.Mode = ScopeMode.Owning

    override this.GetScopeNode() =
        if this.Mode = ScopeMode.Owning then
            if Platform.isNull (box this.ScopeField) then
                let s = new Owner (this.Graph.Root)
                this.ScopeField <- s
                Tracer.ScopeNew (s, this.Graph, this.NodeId)

                if this.IsDisposed then
                    s.Dispose ()

            this.ScopeField
        else
#if !RANVIER_TRACE
            this.Flags <- this.Flags ||| NodeFlags.Violated
#else
#if FABLE_COMPILER
            this.Violated <- true
#else
            this.ViolatedField <- true
#endif
#endif
            raise (InvalidOperationException (ScopeMessages.forMode this.Mode))

    interface ISource with
        member this.AddObserver(c, slot) =
#if FABLE_COMPILER && !RANVIER_TRACE
            (this.EnsureObservers ()).Add(c, slot)
            ||| EdgeHandle.MemoBit
#else
            this.Observers.Add (c, slot)
            ||| EdgeHandle.MemoBit
#endif

        member this.TryAddObserver(c, slot) =
#if FABLE_COMPILER && !RANVIER_TRACE
            let h = (this.EnsureObservers ()).TryAdd(c, slot)
#else
            let h = this.Observers.TryAdd (c, slot)
#endif
            if h >= 0 then h ||| EdgeHandle.MemoBit else h

        member this.RemoveObserverAt(c, handle) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if not (Platform.isNull this.Observers) then
                this.Observers.RemoveAt (c, handle)
#else
            this.Observers.RemoveAt (c, handle)
#endif

        member this.UpdateIfNecessary() =
            NodeOps.EnsureCurrent this

        member this.IsStale =
#if !RANVIER_TRACE
            (this.Flags &&& NodeFlags.FreshMask) <> 0
            && (this.Flags &&& NodeFlags.Disposed) = 0
#else
#if FABLE_COMPILER
            this.Freshness <> Freshness.Clean
            && not this.Disposed
#else
            this.FreshnessField <> Freshness.Clean
            && not this.DisposedField
#endif
#endif

        member this.IsActive =
#if !RANVIER_TRACE
            (this.Flags &&& NodeFlags.ActiveMask) <> 0
#else
#if FABLE_COMPILER
            this.Active <> 0uy
#else
            this.ActiveField <> 0uy
#endif
#endif

/// <summary>
/// Slim concrete node of an effect: zero <c>Observers</c>, zero <c>CheckParent</c>, zero <c>RunCallback</c>,
/// zero separate <c>OwnerLink</c>. Created directly by <c>Effect.Create</c> / <c>createEffect</c> without an
/// <c>Effect</c> wrapper.
/// </summary>
#if FABLE_COMPILER
and [<Sealed; NoEquality; NoComparison>] internal EffectNode(g: Graph, bodyFn: unit -> unit) =
#if !RANVIER_TRACE
    inherit Node(g.NextId (), g, NodeFlags.InitEffect)
#else
    inherit Node(g.NextId (), NodeKind.Effect, g, Freshness.Clean)
#endif
    let mutable publicNode: INode = Platform.nil ()

    member inline internal _.BodyFn = bodyFn

    member inline internal _.PublicNode
        with get () = publicNode
        and set v = publicNode <- v
#else
and [<Sealed; NoEquality; NoComparison>] internal EffectNode =
    inherit Node
    val internal BodyFn: unit -> unit
    val internal BodyAction: Action
    /// <summary>Public <c>Effect</c> wrapper when constructed via <c>new Effect(...)</c>, otherwise null.</summary>
    val mutable internal PublicNode: INode

    internal new(graph: Graph, bodyFn: unit -> unit, bodyAction: Action) =
        {
#if !RANVIER_TRACE
            inherit Node(graph.NextId (), graph, NodeFlags.InitEffect)
#else
            inherit Node(graph.NextId (), NodeKind.Effect, graph, Freshness.Clean)
#endif
            BodyFn = bodyFn
            BodyAction = bodyAction
            PublicNode = Unchecked.defaultof<INode>
        }
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.InvokeBody() =
#if FABLE_COMPILER
        this.BodyFn ()
#else
        if not (Platform.isNull (box this.BodyFn)) then
            this.BodyFn ()
        else
            this.BodyAction.Invoke ()
#endif

    member internal this.Start() =
        Tracer.Bind (this.SourcesField, this.Graph, this.NodeId)
#if RANVIER_COUNTERS
        Counters.EffectCreated ()
#endif
        let owner = this.Graph.CurrentOwner
        Tracer.EffectNew (this.Graph, this.NodeId, owner)
        owner.AttachLink this
#if !RANVIER_TRACE
        if
            not this.IsDisposed
            && this.Graph.CanFuseIdleFlush ()
        then
            NodeOps.RunFusedEffect (this.Graph, this)
        else
            NodeOps.EffectMarkDirty this
            this.Graph.RequestFlush ()
#else
        NodeOps.EffectMarkDirty this
        this.Graph.RequestFlush ()
#endif

    member internal this.Dispose() =
        this.DisposeFromUser ()

    override _.CanOwnNode = true

    override this.GetScopeNode() =
        if Platform.isNull (box this.ScopeField) then
            let s = new Owner (this.Graph.Root)
            this.ScopeField <- s
            Tracer.ScopeNew (s, this.Graph, this.NodeId)

            if this.IsDisposed then
                s.Dispose ()

        this.ScopeField

    interface IScheduled with
        member this.Execute() =
            NodeOps.Execute this

    interface IDisposable with
        member this.Dispose() =
            this.DisposeFromUser ()

    override this.RunDirect() =
#if !RANVIER_TRACE
        if
            Platform.isNull (box this.ScopeField)
            && (this.Flags
                &&& (NodeFlags.Disposed ||| NodeFlags.StatusUnreadyMask))
                =
                0
            && Platform.isNull this.Graph.Journal
            && not this.Graph.OwnerPending
            && this.Graph.CanEnterHostedTop ()
        then
            this.RunTopCore ()
        else
            this.RunGeneral ()
#else
        this.RunGeneral ()
#endif

#if !RANVIER_TRACE
    member private this.RunTopCore() =
        let g = this.Graph
        this.Flags <- this.Flags &&& ~~~NodeFlags.StatusUninitialized
        let prevOwner = g.EnterHostedTop this
#if FABLE_COMPILER
        // The run records the exceptions of its body. An exception raised by the JavaScript engine itself, a stack
        // overflow, restores the reader and owner the run replaced and continues to the caller.
        try
            this.RunTopHosted prevOwner
        with _ ->
            g.LeaveHostedTopFailed (prevOwner, false)
            reraise ()

    member private this.RunTopHosted(prevOwner: Owner) =
#endif
        this.SourcesField.BeginRun ()
        this.RunsField <- this.RunsField + 1
#if RANVIER_COUNTERS
        Counters.EffectRan ()
#endif
        let ex = this.TryInvokeBody ()

        if Platform.isNull ex then
            this.Graph.LeaveHostedTop (prevOwner, false)

            if
                this.IsDisposed
                || this.SourcesField.cursor < this.SourcesField.count
            then
                this.FinishSources ()
        else
            this.BodyFailedTop (prevOwner, ex)
#endif

    member private this.RunGeneral() =
        this.SourcesField.BeginRun ()

        if not (Platform.isNull (box this.ScopeField)) then
            this.DischargeScope ()

        if not this.IsDisposed then
            this.RunBody ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.DischargeScope() =
#if !RANVIER_TRACE
        this.Flags <- this.Flags ||| NodeFlags.InCleanup
#else
#if FABLE_COMPILER
        this.InCleanup <- true
#else
        this.InCleanupField <- true
#endif
#endif

        try
            this.Graph.Discharge this.ScopeField
        finally
#if !RANVIER_TRACE
            this.Flags <- this.Flags &&& ~~~NodeFlags.InCleanup
#else
#if FABLE_COMPILER
            this.InCleanup <- false
#else
            this.InCleanupField <- false
#endif
#endif

    member private this.RunBody() =
        let g = this.Graph

        if not (Platform.isNull (box this.PendingSourceField)) then
            this.PendingSourceField <- Platform.nil ()

        this.SetStatusNone ()
        this.FailureField <- Platform.nil ()
        this.RunsField <- this.RunsField + 1
#if RANVIER_COUNTERS
        Counters.EffectRan ()
#endif
        Tracer.RunStart (g, this.NodeId, this.RunsField)

#if FABLE_COMPILER
#if !RANVIER_TRACE
        if
            Platform.isNull g.Journal
            && not g.OwnerPending
            && g.CanEnterHostedTop ()
        then
            this.RunBodyTop ()
        else
#endif
        try
            try
                if Platform.isNull g.Journal then
#if !RANVIER_TRACE
                        g.RunHostedNode (this, this.BodyFn, ())
#else
                    g.RunHosted (this :> IComputation, this.BodyFn)
#endif
                else
                    this.RunBodyJournaled ()
            finally
                if this.IsDisposed then
                    this.SourcesField.Clear (this :> IComputation)
                else
                    this.SourcesField.EndRun (this :> IComputation)
        with ex ->
            this.RecordFailure ex
#else
        if Platform.isNull g.Journal then
            if not g.OwnerPending && g.CanEnterHostedTop () then
                let prevOwner = g.EnterHostedTop this
                let ex = this.TryInvokeBody ()

                if not (Platform.isNull ex) then
                    this.BodyFailedTop (prevOwner, ex)
                else
                    g.LeaveHostedTop (prevOwner, false)

                    if
                        this.IsDisposed
                        || this.SourcesField.Cursor < this.SourcesField.Count
                    then
                        this.FinishSources ()
            else
                let saved = g.EnterHostedNode this
                let ex = this.TryInvokeBody ()

                if not (Platform.isNull ex) then
                    this.BodyFailed (saved, ex)
                else
                    g.LeaveHosted saved

                    if
                        this.IsDisposed
                        || this.SourcesField.Cursor < this.SourcesField.Count
                    then
                        this.FinishSources ()
        else
            this.RunBodyJournaledGuarded ()
#endif

        Tracer.RunEnd (g, this.NodeId, this.Status)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RecordFailure(ex: exn) =
        match ex with
        | NotReadyException source ->
            this.PendingSourceField <- source
            this.SetStatusPending ()
        | _ ->
            let origin =
                if Platform.isNull (box this.PublicNode) then
                    this :> INode
                else
                    this.PublicNode

            this.FailureField <- this.Graph.FailureOf (ex, origin, null)
            this.SetStatusError ()

#if !FABLE_COMPILER || !RANVIER_TRACE
    member private this.TryInvokeBody() : exn =
        try
            this.InvokeBody ()

            if this.Graph.RaisedPending then
                this.Graph.CheckRaised ()

            Platform.nil ()
        with ex ->
#if FABLE_COMPILER
            Thrown.hold ex
#else
            ex
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.FinishSources() =
        if this.IsDisposed then
            this.SourcesField.Clear (this :> IComputation)
        else
            this.SourcesField.EndRun (this :> IComputation)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.BodyFailedTop(prevOwner: Owner, ex: exn) =
        this.Graph.LeaveHostedTopFailed (prevOwner, false)
        this.FinishSources ()
#if FABLE_COMPILER
        this.RecordFailure (Thrown.take ex)
#else
        this.RecordFailure ex
#endif
#endif

#if FABLE_COMPILER && !RANVIER_TRACE
    member private this.RunBodyTop() =
        let g = this.Graph
        let prevOwner = g.EnterHostedTop this

        try
            let ex = this.TryInvokeBody ()

            if not (Platform.isNull ex) then
                this.BodyFailedTop (prevOwner, ex)
            else
                g.LeaveHostedTop (prevOwner, false)

                if
                    this.IsDisposed
                    || this.SourcesField.cursor < this.SourcesField.count
                then
                    this.FinishSources ()
        with _ ->
            g.LeaveHostedTopFailed (prevOwner, false)
            reraise ()
#endif

#if !FABLE_COMPILER
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.BodyFailed(saved: HostedFrame, ex: exn) =
        this.Graph.LeaveHosted saved
        this.FinishSources ()
        this.RecordFailure ex

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RunBodyJournaledGuarded() =
        try
            try
                this.RunBodyJournaled ()
            finally
                this.FinishSources ()
        with ex ->
            this.RecordFailure ex
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RunBodyJournaled() =
        let g = this.Graph
#if FABLE_COMPILER
#if !RANVIER_TRACE
        g.Journal.RunWork (fun () -> g.RunHostedNode (this, this.BodyFn, ()))
#else
        g.Journal.RunWork (fun () -> g.RunHosted (this :> IComputation, this.BodyFn))
#endif
#else
        g.Journal.RunWork (fun () ->
            let saved = g.EnterHostedNode this

            try
                this.InvokeBody ()
                g.CheckRaised ()
            finally
                g.LeaveHosted saved)
#endif

/// <summary>
/// The graph operations of a <c>Node</c>, called directly by its edges and by the public object's members.
/// </summary>
and [<AbstractClass; Sealed>] internal NodeOps =
    /// <summary>The <c>IScheduled</c> face of an effect or split effect node.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member Scheduled(n: Node) : IScheduled =
        Platform.fastCast<IScheduled>(box n)

    // ---------------------------------------------------------------------------------------------------------
    // Marks
    // ---------------------------------------------------------------------------------------------------------

#if !RANVIER_TRACE
    /// <summary>A memo's <c>MarkDirty</c>.</summary>
    static member MemoMarkDirty(n: MemoNode) =
        let f = n.Flags
        let fresh = f &&& NodeFlags.FreshMask

        if fresh <> NodeFlags.FreshDirty then
            n.Flags <-
                (f &&& ~~~NodeFlags.FreshMask)
                ||| NodeFlags.FreshDirty

            if fresh = NodeFlags.FreshClean then
                n.Graph.NoteStaleTransition ()
                NodeOps.NotifyCheck n
            elif (f &&& NodeFlags.Unannounced) <> 0 then
                n.Flags <- n.Flags &&& ~~~NodeFlags.Unannounced
                n.Graph.NoteStaleTransition ()
                NodeOps.NotifyCheck n
        elif (f &&& NodeFlags.Unannounced) <> 0 then
            n.Flags <- f &&& ~~~NodeFlags.Unannounced
            n.Graph.NoteStaleTransition ()
            NodeOps.NotifyCheck n

    /// <summary>A memo's <c>MarkCheck</c>.</summary>
    static member MemoMarkCheck(n: MemoNode) =
        let f = n.Flags

        if (f &&& NodeFlags.FreshMask) = NodeFlags.FreshClean then
            n.Flags <- f ||| NodeFlags.FreshCheck
            NodeOps.NotifyCheck n
        elif (f &&& NodeFlags.Unannounced) <> 0 then
            n.Flags <- f &&& ~~~NodeFlags.Unannounced
            n.Graph.NoteStaleTransition ()
            NodeOps.NotifyCheck n

    /// <summary>An effect's or split effect's <c>MarkDirty</c>.</summary>
    static member EffectMarkDirty(n: Node) =
        let f = n.Flags

        if (f &&& (NodeFlags.Disposed ||| NodeFlags.InCleanup)) = 0 then
            if (f &&& NodeFlags.Queued) = 0 then
                n.Flags <-
                    (f &&& ~~~NodeFlags.FreshMask)
                    ||| NodeFlags.FreshDirty
                    ||| NodeFlags.Queued

                if (f &&& NodeFlags.Publishes) <> 0 then
                    n.Graph.SchedulePass (NodeOps.Scheduled n)
                else
                    n.Graph.ScheduleNode n
            else
                n.Flags <-
                    (f &&& ~~~NodeFlags.FreshMask)
                    ||| NodeFlags.FreshDirty

    /// <summary>An effect's or split effect's <c>MarkCheck</c>.</summary>
    static member EffectMarkCheck(n: Node) =
        let f = n.Flags

        if (f &&& (NodeFlags.Disposed ||| NodeFlags.InCleanup)) = 0 then
            let f' =
                if (f &&& NodeFlags.FreshMask) = NodeFlags.FreshClean then
                    f ||| NodeFlags.FreshCheck
                else
                    f

            if (f' &&& NodeFlags.Queued) = 0 then
                n.Flags <- f' ||| NodeFlags.Queued

                if (f' &&& NodeFlags.Publishes) <> 0 then
                    n.Graph.SchedulePass (NodeOps.Scheduled n)
                else
                    n.Graph.ScheduleNode n
            elif f' <> f then
                n.Flags <- f'

    /// <summary>Tells a memo's observers <c>Check</c>; no call when it has none.</summary>
#if FABLE_COMPILER
    static member inline NotifyCheck(n: MemoNode) =
        if n.ObserverCount <> 0 then
            NodeOps.NotifyCheckSpine n
#else
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member NotifyCheck(n: MemoNode) =
        if n.ObserverCount <> 0 then
            NodeOps.NotifyCheckSpine n
#endif

    /// <summary>Iterative single-memo <c>MarkCheck</c> spine walk (Theorem B3).</summary>
    static member NotifyCheckSpine(n: MemoNode) =
        let mutable cur = n
        let mutable walking = true

        while walking do
            let count = cur.ObserverCount

            if
                count = 1
                && EdgeHandle.isNodeSlot cur.Observers.FirstSlot
            then
                let next = Platform.fastCast<Node> cur.Observers.FirstRef

                if (next.Flags &&& NodeFlags.CleanMemoMask) = NodeFlags.KindMemo then
                    let m = Platform.fastCast<MemoNode> next
                    m.Flags <- m.Flags ||| NodeFlags.FreshCheck

                    if m.ObserverCount = 0 then walking <- false else cur <- m
                else
                    NodeOps.MarkCheck next
                    walking <- false
            elif count <> 0 then
                cur.Observers.NotifyCheck ()
                walking <- false
            else
                walking <- false

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member MarkDirty(n: Node) =
        let f = n.Flags

        if (f &&& NodeFlags.KindMask) = NodeFlags.KindMemo then
            let m = Platform.fastCast<MemoNode> n

            if
                (f &&& NodeFlags.FreshMask) = NodeFlags.FreshClean
                && m.ObserverCount = 0
            then
                m.Flags <- f ||| NodeFlags.FreshDirty
                m.Graph.NoteStaleTransition ()
            else
                NodeOps.MemoMarkDirty m
        else
            NodeOps.EffectMarkDirty n

#if FABLE_COMPILER
    static member inline MarkCheck(n: Node) =
        if (n.Flags &&& NodeFlags.KindMask) = NodeFlags.KindMemo then
            NodeOps.MemoMarkCheck (Platform.fastCast<MemoNode> n)
        else
            NodeOps.EffectMarkCheck n

    static member inline MarkDirtyOf(refObj: obj, slot: int) =
        if EdgeHandle.isNodeSlot slot then
            NodeOps.MarkDirty (Platform.fastCast<Node> refObj)
        else
            (Platform.fastCast<IComputation> refObj).MarkDirty()

    static member inline MarkCheckOf(refObj: obj, slot: int) =
        if EdgeHandle.isNodeSlot slot then
            NodeOps.MarkCheck (Platform.fastCast<Node> refObj)
        else
            (Platform.fastCast<IComputation> refObj).MarkCheck()
#else
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member MarkCheck(n: Node) =
        if (n.Flags &&& NodeFlags.KindMask) = NodeFlags.KindMemo then
            NodeOps.MemoMarkCheck (Platform.fastCast<MemoNode> n)
        else
            NodeOps.EffectMarkCheck n

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member MarkDirtyOf(refObj: obj, slot: int) =
        if EdgeHandle.isNodeSlot slot then
            NodeOps.MarkDirty (Platform.fastCast<Node> refObj)
        else
            (Platform.fastCast<IComputation> refObj).MarkDirty()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member MarkCheckOf(refObj: obj, slot: int) =
        if EdgeHandle.isNodeSlot slot then
            NodeOps.MarkCheck (Platform.fastCast<Node> refObj)
        else
            (Platform.fastCast<IComputation> refObj).MarkCheck()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member HasReadSlotOf(refObj: obj, slot: int) =
        if EdgeHandle.isNodeSlot slot then
            (slot &&& EdgeHandle.SlotMask) < (Platform.fastCast<Node> refObj).SourcesField.Cursor
        else
            (Platform.fastCast<IComputation> refObj).HasReadSlot slot

    static member MovedIn(refObj: obj, slot: int, handle: int) =
        if EdgeHandle.isNodeSlot slot then
            (Platform.fastCast<Node> refObj).SourcesField.MovedIn(slot &&& EdgeHandle.SlotMask, handle)
        else
            (Platform.fastCast<IComputation> refObj).MovedIn(slot, handle)

#if FABLE_COMPILER
    static member private MarkIdle(refObj: obj, slot: int) =
        if EdgeHandle.isNodeSlot slot then
            let n = Platform.fastCast<Node> refObj

            if (n.Flags &&& NodeFlags.CleanMemoMask) = NodeFlags.KindMemo then
                let m = Platform.fastCast<MemoNode> n
                m.Flags <- m.Flags ||| NodeFlags.FreshDirty

                if
                    not (Platform.isNull m.Observers)
                    && m.Observers.Count <> 0
                then
                    m.Graph.NoteStaleTransition ()
                    NodeOps.NotifyCheckSpine m
            else
                NodeOps.MarkDirty n
        else
            (Platform.fastCast<IComputation> refObj).MarkDirty()

    static member NotifyDirtyIdle(obs: ObserverList) =
        let mutable refs = obs.Rest
        let mutable slots = obs.RestSlots
        let mutable i = obs.Count - 1

        while i > 0 do
            let idx = i - 1
            let refObj = Platform.itemAt idx refs
            let slot = Platform.itemAt idx slots
            i <- i - 1

            if EdgeHandle.isNodeSlot slot then
                let n = Platform.fastCast<Node> refObj

                if (n.Flags &&& NodeFlags.CleanMemoMask) = NodeFlags.KindMemo then
                    let m = Platform.fastCast<MemoNode> n
                    m.Flags <- m.Flags ||| NodeFlags.FreshDirty

                    if
                        not (Platform.isNull m.Observers)
                        && m.Observers.Count <> 0
                    then
                        m.Graph.NoteStaleTransition ()
                        NodeOps.NotifyCheckSpine m
                        refs <- obs.Rest
                        slots <- obs.RestSlots

                        if i >= obs.Count then
                            i <- obs.Count - 1
                else
                    NodeOps.MarkDirty n
                    refs <- obs.Rest
                    slots <- obs.RestSlots

                    if i >= obs.Count then
                        i <- obs.Count - 1
            else
                (Platform.fastCast<IComputation> refObj).MarkDirty()
                refs <- obs.Rest
                slots <- obs.RestSlots

                if i >= obs.Count then
                    i <- obs.Count - 1

        if obs.Count > 0 then
            NodeOps.MarkIdle (obs.FirstRef, obs.FirstSlot)
#endif
#else
#if FABLE_COMPILER
    /// <summary>Tells a memo's observers <c>Check</c>; no call when it has none.</summary>
    static member inline NotifyCheck(n: MemoNode) =
        if n.Observers.Count <> 0 then
            n.Observers.NotifyCheck ()

    /// <summary>A memo's <c>MarkDirty</c>.</summary>
    static member inline MemoMarkDirty(n: MemoNode) =
        if n.Freshness <> Freshness.Dirty then
            let wasClean = n.Freshness = Freshness.Clean
            n.Freshness <- Freshness.Dirty

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
    static member MemoMarkCheck(n: MemoNode) =
        if n.Freshness = Freshness.Clean then
            n.Freshness <- Freshness.Check
            NodeOps.NotifyCheck n
        elif n.Unannounced then
            n.Unannounced <- false
            n.Graph.NoteStaleTransition ()
            NodeOps.NotifyCheck n

    /// <summary>An effect's or split effect's <c>MarkDirty</c>.</summary>
    static member inline EffectMarkDirty(n: Node) =
        if not n.Disposed && not n.InCleanup then
            n.Freshness <- Freshness.Dirty

            if not n.Queued then
                n.Queued <- true

                if n.Publishes then
                    n.Graph.SchedulePass (NodeOps.Scheduled n)
                else
                    n.Graph.ScheduleNode n

    /// <summary>An effect's or split effect's <c>MarkCheck</c>.</summary>
    static member inline EffectMarkCheck(n: Node) =
        if not n.Disposed && not n.InCleanup then
            if n.Freshness = Freshness.Clean then
                n.Freshness <- Freshness.Check

            if not n.Queued then
                n.Queued <- true

                if n.Publishes then
                    n.Graph.SchedulePass (NodeOps.Scheduled n)
                else
                    n.Graph.ScheduleNode n

    static member inline MarkDirty(n: Node) =
        if n.Kind = NodeKind.Memo then
            NodeOps.MemoMarkDirty (Platform.fastCast<MemoNode> n)
        else
            NodeOps.EffectMarkDirty n

    static member inline MarkCheck(n: Node) =
        if n.Kind = NodeKind.Memo then
            NodeOps.MemoMarkCheck (Platform.fastCast<MemoNode> n)
        else
            NodeOps.EffectMarkCheck n
#else
    /// <summary>A memo's <c>MarkDirty</c> (.NET traced build).</summary>
    static member MemoMarkDirty(n: MemoNode) =
        if n.FreshnessField <> Freshness.Dirty then
            let wasClean = n.FreshnessField = Freshness.Clean
            n.FreshnessField <- Freshness.Dirty

            if wasClean then
                n.Graph.NoteStaleTransition ()
                NodeOps.NotifyCheck n
            elif n.UnannouncedField then
                n.UnannouncedField <- false
                n.Graph.NoteStaleTransition ()
                NodeOps.NotifyCheck n
        elif n.UnannouncedField then
            n.UnannouncedField <- false
            n.Graph.NoteStaleTransition ()
            NodeOps.NotifyCheck n

    static member MemoMarkCheck(n: MemoNode) =
        if n.FreshnessField = Freshness.Clean then
            n.FreshnessField <- Freshness.Check
            NodeOps.NotifyCheck n
        elif n.UnannouncedField then
            n.UnannouncedField <- false
            n.Graph.NoteStaleTransition ()
            NodeOps.NotifyCheck n

    static member EffectMarkDirty(n: Node) =
        if not n.DisposedField && not n.InCleanupField then
            n.FreshnessField <- Freshness.Dirty

            if not n.QueuedField then
                n.QueuedField <- true

                if n.PublishesField then
                    n.Graph.SchedulePass (NodeOps.Scheduled n)
                else
                    n.Graph.ScheduleNode n

    static member EffectMarkCheck(n: Node) =
        if not n.DisposedField && not n.InCleanupField then
            if n.FreshnessField = Freshness.Clean then
                n.FreshnessField <- Freshness.Check

            if not n.QueuedField then
                n.QueuedField <- true

                if n.PublishesField then
                    n.Graph.SchedulePass (NodeOps.Scheduled n)
                else
                    n.Graph.ScheduleNode n

    static member NotifyCheck(n: MemoNode) =
        if n.Observers.Count <> 0 then
            n.Observers.NotifyCheck ()

    static member MarkDirty(n: Node) =
        if n.KindField = NodeKind.Memo then
            NodeOps.MemoMarkDirty (Platform.fastCast<MemoNode> n)
        else
            NodeOps.EffectMarkDirty n

    static member MarkCheck(n: Node) =
        if n.KindField = NodeKind.Memo then
            NodeOps.MemoMarkCheck (Platform.fastCast<MemoNode> n)
        else
            NodeOps.EffectMarkCheck n
#endif
#endif

#if !RANVIER_TRACE
    /// <summary>Promotes a sole checked observer in place when no further notification or scheduling is due.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member TryPromoteTop(n: MemoNode) =
        if
            n.ObserverCount = 1
            && n.Graph.StaleTransitions = 0
            && EdgeHandle.isNodeSlot n.Observers.FirstSlot
        then
            let m = Platform.fastCast<Node> n.Observers.FirstRef
            let f = m.Flags

            if (f &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck then
                if (f &&& NodeFlags.KindMask) = NodeFlags.KindMemo then
                    if (f &&& NodeFlags.Unannounced) = 0 then
                        m.Flags <-
                            (f &&& ~~~NodeFlags.FreshMask)
                            ||| NodeFlags.FreshDirty

                        true
                    else
                        false
                elif
                    (f
                     &&& (NodeFlags.Queued
                          ||| NodeFlags.Disposed
                          ||| NodeFlags.InCleanup))
                        =
                        NodeFlags.Queued
                then
                    m.Flags <-
                        (f &&& ~~~NodeFlags.FreshMask)
                        ||| NodeFlags.FreshDirty

                    true
                else
                    false
            else
                false
        else
            false

    /// <summary><c>NotifyMoved</c> after <c>LeaveHostedTop</c>, where <c>CurrentObserver</c> is guaranteed null.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member NotifyMovedTop(n: MemoNode) =
        let count = n.ObserverCount

        if count <> 0 then
            let g = n.Graph

            if
                count = 1
                && g.StaleTransitions = 0
                && EdgeHandle.isNodeSlot n.Observers.FirstSlot
            then
                let m = Platform.fastCast<Node> n.Observers.FirstRef
                let f = m.Flags

                if
                    (f
                     &&& (NodeFlags.KindMask
                          ||| NodeFlags.FreshMask
                          ||| NodeFlags.Unannounced))
                        =
                        NodeFlags.FreshCheck
                then
                    m.Flags <-
                        (f &&& ~~~NodeFlags.FreshMask)
                        ||| NodeFlags.FreshDirty
                else
                    NodeOps.MarkDirty m
            elif g.StaleTransitions = 0 then
                n.Observers.NotifyDirty ()
            else
                n.Observers.NotifyDirtyExcept (Platform.nil ())
#endif

    /// <summary>Marks a moved memo's observers dirty, skipping the running computation as <c>Graph.NotifyMoved</c> does.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member NotifyMoved(n: MemoNode) =
#if !RANVIER_TRACE
        let count = n.ObserverCount

        if count <> 0 then
            let g = n.Graph
            let running = g.CurrentObserver

            if
                count = 1
                && g.StaleTransitions = 0
                && EdgeHandle.isNodeSlot n.Observers.FirstSlot
            then
                if not (obj.ReferenceEquals (n.Observers.FirstRef, box running)) then
                    let m = Platform.fastCast<Node> n.Observers.FirstRef
                    let f = m.Flags

                    if
                        (f
                         &&& (NodeFlags.KindMask
                              ||| NodeFlags.FreshMask
                              ||| NodeFlags.Unannounced))
                            =
                            NodeFlags.FreshCheck
                    then
                        m.Flags <-
                            (f &&& ~~~NodeFlags.FreshMask)
                            ||| NodeFlags.FreshDirty
                    else
                        NodeOps.MarkDirty m
            elif g.StaleTransitions = 0 then
                n.Observers.NotifyDirtyOther running
            else
                n.Observers.NotifyDirtyExcept running
#else
        if n.Observers.Count <> 0 then
            let g = n.Graph
            let running = g.CurrentObserver

            if g.StaleTransitions = 0 then
                n.Observers.NotifyDirtyOther running
            else
                n.Observers.NotifyDirtyExcept running
#endif

    // ---------------------------------------------------------------------------------------------------------
    // Edges
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>Records a read of <c>source</c> by the running node. Ignored once the node is disposed.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member AddSource(n: Node, source: ISource) =
        if not n.IsDisposed then
#if !RANVIER_TRACE
            n.SourcesField.AddTagged (n :> IComputation, source, EdgeHandle.NodeSlotBit)
#else
            n.SourcesField.Add (n :> IComputation, source)
#endif

    // ---------------------------------------------------------------------------------------------------------
    // The check walk
    // ---------------------------------------------------------------------------------------------------------

#if !RANVIER_TRACE
    /// <summary>Brings the source at <c>i</c> up to date when it is not a signal: a <c>MemoNode</c> directly via <c>MemoBit</c>.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member RefreshSourceAt(n: Node, i: int) =
        if i = 0 then
            let h = n.SourcesField.firstHandle

            if not (EdgeHandle.isSignal h) then
                if EdgeHandle.isMemo h then
                    NodeOps.EnsureCurrent (Platform.fastCast<MemoNode> n.SourcesField.first)
                else
                    n.SourcesField.first.UpdateIfNecessary ()
        elif i = 1 then
            let h = n.SourcesField.secondHandle

            if not (EdgeHandle.isSignal h) then
                if EdgeHandle.isMemo h then
                    NodeOps.EnsureCurrent (Platform.fastCast<MemoNode> n.SourcesField.second)
                else
                    n.SourcesField.second.UpdateIfNecessary ()
        else
#if FABLE_COMPILER
            let k = i - 2
            let h = Platform.itemAt k n.SourcesField.restHandles

            if not (EdgeHandle.isSignal h) then
                let s = (Platform.itemAt k n.SourcesField.rest).Source

                if EdgeHandle.isMemo h then
                    NodeOps.EnsureCurrent (Platform.fastCast<MemoNode> s)
                else
                    s.UpdateIfNecessary ()
#else
            let s = Platform.itemAt (i - 2) n.SourcesField.rest

            if not (EdgeHandle.isSignal s.Handle) then
                if EdgeHandle.isMemo s.Handle then
                    NodeOps.EnsureCurrent (Platform.fastCast<MemoNode> s.Source)
                else
                    s.Source.UpdateIfNecessary ()
#endif

    static member private SourceStale(source: ISource, handle: int) =
        if EdgeHandle.isMemo handle then
            let m = Platform.fastCast<MemoNode> source

            (m.Flags &&& NodeFlags.FreshMask) <> 0
            && (m.Flags &&& NodeFlags.Disposed) = 0
        else
            source.IsStale

    /// <summary>Whether any recorded source is currently stale.</summary>
    static member HasStaleSource(n: Node) =
        let count = n.SourcesField.count

        if count = 0 then
            false
        elif
            not (EdgeHandle.isSignal n.SourcesField.firstHandle)
            && NodeOps.SourceStale (n.SourcesField.first, n.SourcesField.firstHandle)
        then
            true
        elif count = 1 then
            false
        elif
            not (EdgeHandle.isSignal n.SourcesField.secondHandle)
            && NodeOps.SourceStale (n.SourcesField.second, n.SourcesField.secondHandle)
        then
            true
        else
            let limit = count - 2
            let rest = n.SourcesField.rest
            let mutable i = 0

#if FABLE_COMPILER
            let handles = n.SourcesField.restHandles

            while i < limit
                  && (let h = Platform.itemAt i handles in

                      EdgeHandle.isSignal h
                      || not (NodeOps.SourceStale ((Platform.itemAt i rest).Source, h))) do
                i <- i + 1
#else
            while i < limit
                  && (let s = Platform.itemAt i rest in

                      EdgeHandle.isSignal s.Handle
                      || not (NodeOps.SourceStale (s.Source, s.Handle))) do
                i <- i + 1
#endif

            i < limit
#else
    static member RefreshSourceAt(n: Node, i: int) =
        n.SourcesField.RefreshSourceAt i

    /// <summary>Whether any recorded source is currently stale.</summary>
    static member HasStaleSource(n: Node) =
        n.SourcesField.HasStaleSource ()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member CheckActiveSource(n: Node, i: int) =
        if
#if !RANVIER_TRACE
            (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck
#else
#if FABLE_COMPILER
            n.Freshness = Freshness.Check
#else
            n.FreshnessField = Freshness.Check
#endif
#endif
        then
            let source = n.SourcesField.SourceAt i

            if source.IsStale && source.IsActive then
                NodeOps.MarkDirty n

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member ResolveCheckAgain(n: Node) =
        let g = n.Graph
        let mutable again = NodeOps.HasStaleSource n

        while again do
            let before = g.StaleTransitions
            let mutable i = 0

            while (
#if !RANVIER_TRACE
                  (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck
#else
#if FABLE_COMPILER
                  n.Freshness = Freshness.Check
#else
                  n.FreshnessField = Freshness.Check
#endif
#endif
                  )
                  && i < n.SourcesField.Count do
                NodeOps.RefreshSourceAt (n, i)

                if g.ActiveSkips <> 0 then
                    NodeOps.CheckActiveSource (n, i)

                i <- i + 1

            again <-
#if !RANVIER_TRACE
                (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck
#else
#if FABLE_COMPILER
                n.Freshness = Freshness.Check
#else
                n.FreshnessField = Freshness.Check
#endif
#endif
                && g.StaleTransitions <> before
                && NodeOps.HasStaleSource n

    /// <summary>
    /// Asks each source, in read order, whether it moved, until one marks the node <c>Dirty</c>.
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member WalkSources(n: Node) =
        let g = n.Graph
        let before = g.StaleTransitions
        let mutable i = 0

        while (
#if !RANVIER_TRACE
              (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck
#else
#if FABLE_COMPILER
              n.Freshness = Freshness.Check
#else
              n.FreshnessField = Freshness.Check
#endif
#endif
              )
              && i < n.SourcesField.Count do
            NodeOps.RefreshSourceAt (n, i)

            if g.ActiveSkips <> 0 then
                NodeOps.CheckActiveSource (n, i)

            i <- i + 1

        if
#if !RANVIER_TRACE
            (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck
#else
#if FABLE_COMPILER
            n.Freshness = Freshness.Check
#else
            n.FreshnessField = Freshness.Check
#endif
#endif
            && g.StaleTransitions <> before
        then
            NodeOps.ResolveCheckAgain n

    /// <summary>Resolves a memo's <c>Check</c> into <c>Clean</c> or <c>Dirty</c>.</summary>
    static member ResolveCheck(n: MemoNode) =
        let g = n.Graph
        Tracer.CheckStart (g, n.NodeId)
#if !RANVIER_TRACE
        n.Flags <-
            (n.Flags &&& ~~~NodeFlags.ActiveMask)
            ||| NodeFlags.ActiveCheck

        NodeOps.WalkSources n
        n.Flags <- n.Flags &&& ~~~NodeFlags.ActiveMask

        if (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck then
            n.Flags <-
                n.Flags
                &&& ~~~(NodeFlags.FreshMask ||| NodeFlags.Unannounced)
#else
#if FABLE_COMPILER
        n.Active <- 2uy
        NodeOps.WalkSources n
        n.Active <- 0uy
        Tracer.CheckResolved (g, n.NodeId, (n.Freshness = Freshness.Dirty))

        if n.Freshness = Freshness.Check then
            n.Freshness <- Freshness.Clean
            n.Unannounced <- false
#else
        n.ActiveField <- 2uy
        NodeOps.WalkSources n
        n.ActiveField <- 0uy
        Tracer.CheckResolved (g, n.NodeId, (n.FreshnessField = Freshness.Dirty))

        if n.FreshnessField = Freshness.Check then
            n.FreshnessField <- Freshness.Clean
            n.UnannouncedField <- false
#endif
#endif

#if !RANVIER_TRACE
    /// <summary>Whether <c>n</c> reads one source, and that source is a <c>MemoNode</c>.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member SingleMemoSource(n: Node) =
        n.SourcesField.count = 1
        && EdgeHandle.isMemo n.SourcesField.firstHandle

#if !FABLE_COMPILER
    /// <summary>
    /// Resolves the first 24 check frames on the call stack and deeper frames through <c>CheckParent</c>,
    /// returning each link's <c>moved</c> bit directly in register (Theorem B11).
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member RefreshChain(n: MemoNode) =
        let g = n.Graph

        NodeOps.RefreshChainStep (g, n, Unchecked.defaultof<Node>, g.StaleTransitions, 0)
        |> ignore

        if g.HasLastRaised then
            g.ClearLastRaisedIfTop ()

    static member private RefreshChainStep(g: Graph, cur: MemoNode, child: Node, before: int, depth: int) : bool =
        cur.Flags <-
            (cur.Flags &&& ~~~NodeFlags.ActiveMask)
            ||| NodeFlags.ActiveCheck

        let source = Platform.fastCast<MemoNode> cur.SourcesField.first

        let sourceMoved =
            if NodeOps.CanDescendChain source then
                if depth = 23 then
                    NodeOps.RefreshChainTail (g, source, cur :> Node, before)
                else
                    NodeOps.RefreshChainStep (g, source, cur :> Node, before, depth + 1)
            else
                NodeOps.RefreshChainLeaf (g, source, cur :> Node)

        NodeOps.UnwindChain (g, cur, child, sourceMoved, before)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member private CanDescendChain(source: MemoNode) =
        (source.Flags &&& NodeFlags.ChainActiveDisposedMask) = NodeFlags.FreshCheck
        && NodeOps.SingleMemoSource source

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member private RefreshChainLeaf(g: Graph, source: MemoNode, child: Node) : bool =
        if
            (source.Flags &&& NodeFlags.ChainActiveDisposedMask) = NodeFlags.FreshDirty
            && Platform.isNull (box source.ScopeField)
        then
            source.RunCallback.Invoke (source, child)
        else
            NodeOps.RefreshOtherChainLeaf source
            false

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member private RefreshOtherChainLeaf(source: MemoNode) =
        NodeOps.EnsureCurrent source

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member private UnwindChain(g: Graph, x: MemoNode, child: Node, sourceMoved: bool, before: int) : bool =
        if
            sourceMoved
            && (x.Flags
                &&& (NodeFlags.FreshMask
                     ||| NodeFlags.Unannounced
                     ||| NodeFlags.Disposed))
                =
                NodeFlags.FreshCheck
            && g.ActiveSkips = 0
        then
            x.Flags <- x.Flags &&& ~~~NodeFlags.ActiveMask

            if g.HasLastRaised then
                g.ClearLastRaisedIfTop ()

            if
                Platform.isNull (box x.ScopeField)
                || x.Mode <> ScopeMode.Owning
            then
                x.RunCallback.Invoke (x, child)
            else
                x.Flags <-
                    (x.Flags &&& ~~~NodeFlags.FreshMask)
                    ||| NodeFlags.FreshDirty

                NodeOps.RecomputeScoped x
                false
        else
            if sourceMoved then
                NodeOps.MemoMarkDirty x

            NodeOps.UnwindChainSlow (g, x, child, before)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member private UnwindChainSlow(g: Graph, x: MemoNode, child: Node, before: int) : bool =
        if g.HasLastRaised then
            g.ClearLastRaisedIfTop ()

        if
            g.ActiveSkips <> 0
            || (x.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck
        then
            NodeOps.FinishChainCheck (g, x, before)

        x.Flags <- x.Flags &&& ~~~NodeFlags.ActiveMask

        if (x.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck then
            x.Flags <-
                x.Flags
                &&& ~~~(NodeFlags.FreshMask ||| NodeFlags.Unannounced)

            false
        elif (x.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshDirty then
            if
                Platform.isNull (box x.ScopeField)
                || x.Mode <> ScopeMode.Owning
            then
                x.RunCallback.Invoke (x, child)
            else
                NodeOps.RecomputeScoped x
                false
        else
            false

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member private RefreshChainTail(g: Graph, n: MemoNode, parentChild: Node, before: int) : bool =
        let mutable cur = n
        let mutable parent = Unchecked.defaultof<MemoNode>
        let mutable descending = true
        let mutable moved = false

        while descending do
            cur.Flags <-
                (cur.Flags &&& ~~~NodeFlags.ActiveMask)
                ||| NodeFlags.ActiveCheck

            cur.CheckParent <- parent
            parent <- cur
            let source = Platform.fastCast<MemoNode> cur.SourcesField.first

            if NodeOps.CanDescendChain source then
                cur <- source
            else
                moved <- NodeOps.RefreshChainLeaf (g, source, cur :> Node)
                descending <- false

        while not (obj.ReferenceEquals (parent, null)) do
            let x = parent
            let nextParent = x.CheckParent
            x.CheckParent <- Unchecked.defaultof<MemoNode>

            let xChild: Node =
                if obj.ReferenceEquals (nextParent, null) then
                    parentChild
                else
                    nextParent :> Node

            moved <- NodeOps.UnwindChain (g, x, xChild, moved, before)
            parent <- nextParent

        moved
#else
    /// <summary>Resolves the first 24 check frames on the call stack and deeper frames through <c>CheckParent</c>.</summary>
    static member RefreshChain(n: MemoNode) =
        let g = n.Graph
        NodeOps.RefreshChainStep (g, n, g.StaleTransitions, 0)

    static member private RefreshChainStep(g: Graph, cur: MemoNode, before: int, depth: int) =
        cur.Flags <-
            (cur.Flags &&& ~~~NodeFlags.ActiveMask)
            ||| NodeFlags.ActiveCheck

        let source = Platform.fastCast<MemoNode> cur.SourcesField.first

        if NodeOps.CanDescendChain source then
            if depth = 23 then
                NodeOps.RefreshChainTail (g, source, before)
            else
                NodeOps.RefreshChainStep (g, source, before, depth + 1)
        else
            NodeOps.RefreshChainLeaf (g, source)

        NodeOps.UnwindChain (g, cur, before)

    static member inline private CanDescendChain(source: MemoNode) =
        (source.Flags &&& NodeFlags.ChainActiveDisposedMask) = NodeFlags.FreshCheck
        && NodeOps.SingleMemoSource source

    static member private RefreshChainLeaf(g: Graph, source: MemoNode) =
        if (source.Flags &&& NodeFlags.ChainActiveDisposedMask) = NodeFlags.FreshDirty then
            NodeOps.Recompute source
            g.ClearLastRaisedIfTop ()
        else
            NodeOps.RefreshOtherChainLeaf source

    static member private RefreshOtherChainLeaf(source: MemoNode) =
        NodeOps.EnsureCurrent source

    static member private UnwindChain(g: Graph, x: MemoNode, before: int) =
        if
            g.ActiveSkips <> 0
            || (x.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck
        then
            NodeOps.FinishChainCheck (g, x, before)

        x.Flags <- x.Flags &&& ~~~NodeFlags.ActiveMask

        if (x.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck then
            x.Flags <-
                x.Flags
                &&& ~~~(NodeFlags.FreshMask ||| NodeFlags.Unannounced)
        elif (x.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshDirty then
            NodeOps.Recompute x
            g.ClearLastRaisedIfTop ()

    static member private RefreshChainTail(g: Graph, n: MemoNode, before: int) =
        let mutable cur = n
        let mutable parent = Platform.nil ()
        let mutable descending = true

        while descending do
            cur.Flags <-
                (cur.Flags &&& ~~~NodeFlags.ActiveMask)
                ||| NodeFlags.ActiveCheck

            cur.CheckParent <- parent
            parent <- cur
            let source = Platform.fastCast<MemoNode> cur.SourcesField.first

            if NodeOps.CanDescendChain source then
                cur <- source
            else
                NodeOps.RefreshChainLeaf (g, source)
                descending <- false

        while not (Platform.isNull (box parent)) do
            let x = parent
            parent <- x.CheckParent
            x.CheckParent <- Platform.nil ()
            NodeOps.UnwindChain (g, x, before)
#endif

    static member private FinishChainCheck(g: Graph, x: MemoNode, before: int) =
        if g.ActiveSkips <> 0 && x.SourcesField.count > 0 then
            NodeOps.CheckActiveSource (x, 0)

        if (x.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck then
            let mutable i = 1

            while (x.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck
                  && i < x.SourcesField.count do
                NodeOps.RefreshSourceAt (x, i)

                if g.ActiveSkips <> 0 then
                    NodeOps.CheckActiveSource (x, i)

                i <- i + 1

            if
                (x.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck
                && g.StaleTransitions <> before
            then
                NodeOps.ResolveCheckAgain x
#endif

    // ---------------------------------------------------------------------------------------------------------
    // Memo refresh
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>Discharges the previous run's scope, then runs the body once, unless the memo was disposed meanwhile.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member Recompute(n: MemoNode) =
        if
            Platform.isNull (box n.ScopeField)
            || n.Mode <> ScopeMode.Owning
        then
            n.RunDirect ()
        else
            NodeOps.RecomputeScoped n

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member RecomputeScoped(n: MemoNode) =
        let before = n.RunsField
#if !RANVIER_TRACE
        n.Flags <- n.Flags &&& ~~~NodeFlags.FreshMask
#else
#if FABLE_COMPILER
        n.Freshness <- Freshness.Clean
#else
        n.FreshnessField <- Freshness.Clean
#endif
#endif
        n.Graph.Discharge n.ScopeField

        if not n.IsDisposed then
            if n.RunsField = before then
                n.RunDirect ()
            elif
#if !RANVIER_TRACE
                (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshDirty
#else
#if FABLE_COMPILER
                n.Freshness = Freshness.Dirty
#else
                n.FreshnessField = Freshness.Dirty
#endif
#endif
            then
                NodeOps.Recompute n
        elif
#if !RANVIER_TRACE
            (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshClean
#else
#if FABLE_COMPILER
            n.Freshness = Freshness.Clean
#else
            n.FreshnessField = Freshness.Clean
#endif
#endif
        then
#if !RANVIER_TRACE
            n.Flags <- n.Flags &&& ~~~NodeFlags.Unannounced
#else
#if FABLE_COMPILER
            n.Unannounced <- false
#else
            n.UnannouncedField <- false
#endif
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member RefreshActive(n: MemoNode) =
        n.Graph.NoteActiveSkip ()

        if
#if !RANVIER_TRACE
            (n.Flags &&& NodeFlags.ActiveMask) = NodeFlags.ActiveCheck
#else
#if FABLE_COMPILER
            n.Active = 2uy
#else
            n.ActiveField = 2uy
#endif
#endif
        then
            NodeOps.MemoMarkDirty n

    /// <summary>Brings a stale memo current.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member Refresh(n: MemoNode) =
#if !RANVIER_TRACE
        let f = n.Flags

        if (f &&& NodeFlags.ActiveMask) <> 0 then
            NodeOps.RefreshActive n
        elif (f &&& NodeFlags.FreshMask) = NodeFlags.FreshDirty then
            NodeOps.Recompute n
        elif (f &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck then
            if
                EdgeHandle.isMemo n.SourcesField.firstHandle
                && (n.SourcesField.count = 1
                    || ((Platform.fastCast<MemoNode> n.SourcesField.first).Flags
                        &&& NodeFlags.FreshMask)
                        =
                        NodeFlags.FreshDirty)
                && n.Graph.Deferring
            then
                NodeOps.RefreshChain n
            else
                NodeOps.ResolveCheck n

                if (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshDirty then
                    NodeOps.Recompute n
#else
#if FABLE_COMPILER
        if n.Active <> 0uy then
            NodeOps.RefreshActive n
        elif n.Freshness = Freshness.Dirty then
            NodeOps.Recompute n
        elif n.Freshness = Freshness.Check then
            NodeOps.ResolveCheck n

            if n.Freshness = Freshness.Dirty then
                NodeOps.Recompute n
#else
        if n.ActiveField <> 0uy then
            NodeOps.RefreshActive n
        elif n.FreshnessField = Freshness.Dirty then
            NodeOps.Recompute n
        elif n.FreshnessField = Freshness.Check then
            NodeOps.ResolveCheck n

            if n.FreshnessField = Freshness.Dirty then
                NodeOps.Recompute n
#endif
#endif

    /// <summary>Brings a stale memo current when <c>Graph.Deferring</c> is false.</summary>
    static member Pull(n: MemoNode) =
        let g = n.Graph
#if !FABLE_COMPILER || !RANVIER_TRACE
        if g.TryEnterPullFast () then
            try
                NodeOps.Refresh n
            with _ ->
                g.ExitPullFastOnException ()
                reraise ()

            if g.ExitPullFast () then
                g.FinishPullSettle ()
        else
            g.EnterPull ()

            try
                NodeOps.Refresh n
            finally
                g.ExitPull ()
#else
        g.EnterPull ()

        try
            NodeOps.Refresh n
        finally
            g.ExitPull ()
#endif

    /// <summary>
    /// <c>Pull</c>, leaving an owed flush for the caller to settle once it has read the memo's fields. True when the
    /// caller must settle, or the memo is still stale.
    /// </summary>
    static member PullWithoutSettle(n: MemoNode) : bool =
        let g = n.Graph
#if !FABLE_COMPILER || !RANVIER_TRACE
        if g.TryEnterPullFast () then
            try
                NodeOps.Refresh n
            with _ ->
                g.ExitPullFastOnException ()
                reraise ()

            g.ExitPullFast ()
            || (
#if !RANVIER_TRACE
            (n.Flags &&& NodeFlags.FreshMask) <> 0
#else
               n.Freshness <> Freshness.Clean
#endif
            )
        else
            g.EnterPull ()

            try
                NodeOps.Refresh n
            with _ ->
                g.ExitPull ()
                reraise ()

            g.ExitPullDeferSettle ()
            || (
#if !RANVIER_TRACE
            (n.Flags &&& NodeFlags.FreshMask) <> 0
#else
               n.Freshness <> Freshness.Clean
#endif
            )
#else
        g.EnterPull ()

        try
            NodeOps.Refresh n
        with _ ->
            g.ExitPull ()
            reraise ()

        g.ExitPullDeferSettle ()
        || n.Freshness <> Freshness.Clean
#endif

    /// <summary>A memo's <c>UpdateIfNecessary</c>.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    static member EnsureCurrent(n: MemoNode) =
        if
#if !RANVIER_TRACE
            (n.Flags &&& NodeFlags.FreshMask) <> 0
            && (n.Flags &&& NodeFlags.Disposed) = 0
#else
#if FABLE_COMPILER
            n.Freshness <> Freshness.Clean && not n.Disposed
#else
            n.FreshnessField <> Freshness.Clean
            && not n.DisposedField
#endif
#endif
        then
            let g = n.Graph

            if g.Deferring then
                NodeOps.Refresh n
                g.ClearLastRaisedIfTop ()
            else
                NodeOps.Pull n

    /// <summary>Brings a stale memo current for a read: true when the caller must settle an owed flush or the memo is still stale.</summary>
    static member RefreshForRead(n: MemoNode) : bool =
#if !RANVIER_TRACE
        (n.Flags &&& NodeFlags.FreshMask) <> 0
        && (n.Flags &&& NodeFlags.Disposed) = 0
        && (let g = n.Graph

            if g.Deferring then
                NodeOps.Refresh n
                g.ClearLastRaisedIfTop ()
                (n.Flags &&& NodeFlags.FreshMask) <> 0
            else
                NodeOps.PullWithoutSettle n)
#else
#if FABLE_COMPILER
        n.Freshness <> Freshness.Clean
        && not n.Disposed
        && (let g = n.Graph

            if g.Deferring then
                NodeOps.Refresh n
                g.ClearLastRaisedIfTop ()
                n.Freshness <> Freshness.Clean
            else
                NodeOps.PullWithoutSettle n)
#else
        n.FreshnessField <> Freshness.Clean
        && not n.DisposedField
        && (let g = n.Graph

            if g.Deferring then
                NodeOps.Refresh n
                g.ClearLastRaisedIfTop ()
                n.FreshnessField <> Freshness.Clean
            else
                NodeOps.PullWithoutSettle n)
#endif
#endif

    /// <summary>Brings a stale memo current for an untracked read: true when the caller must settle an owed flush.</summary>
    static member RefreshForUntrackedRead(n: MemoNode) : bool =
#if !RANVIER_TRACE
        (n.Flags &&& NodeFlags.FreshMask) <> 0
        && (n.Flags &&& NodeFlags.Disposed) = 0
        && (let g = n.Graph

            if g.Deferring then
                NodeOps.Refresh n
                g.ClearLastRaisedIfTop ()
                false
            else
                NodeOps.PullWithoutSettle n)
#else
#if FABLE_COMPILER
        n.Freshness <> Freshness.Clean
        && not n.Disposed
        && (let g = n.Graph

            if g.Deferring then
                NodeOps.Refresh n
                g.ClearLastRaisedIfTop ()
                false
            else
                NodeOps.PullWithoutSettle n)
#else
        n.FreshnessField <> Freshness.Clean
        && not n.DisposedField
        && (let g = n.Graph

            if g.Deferring then
                NodeOps.Refresh n
                g.ClearLastRaisedIfTop ()
                false
            else
                NodeOps.PullWithoutSettle n)
#endif
#endif

    // ---------------------------------------------------------------------------------------------------------
    // Effects & Idle Fused Flush (Theorem B5)
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>An effect's or split effect's <c>Execute</c>.</summary>
    static member Execute(n: Node) =
#if !RANVIER_TRACE
        if (n.Flags &&& NodeFlags.Disposed) <> 0 then
            n.Flags <- n.Flags &&& ~~~NodeFlags.Queued
        else
            if (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck then
                let g = n.Graph
                NodeOps.WalkSources n

                if g.HasLastRaised then
                    g.ClearLastRaised ()

                if (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck then
                    n.Flags <- n.Flags &&& ~~~NodeFlags.FreshMask

            let shouldRun = (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshDirty

            n.Flags <-
                n.Flags
                &&& ~~~(NodeFlags.Queued ||| NodeFlags.FreshMask)

            if shouldRun then
                n.RunDirect ()
#else
#if FABLE_COMPILER
        if n.Disposed then
            n.Queued <- false
        else
            if n.Freshness = Freshness.Check then
                let g = n.Graph
                Tracer.CheckStart (g, n.NodeId)
                NodeOps.WalkSources n
                Tracer.CheckResolved (g, n.NodeId, (n.Freshness = Freshness.Dirty))

                if g.HasLastRaised then
                    g.ClearLastRaised ()

                if n.Freshness = Freshness.Check then
                    n.Freshness <- Freshness.Clean

            let shouldRun = n.Freshness = Freshness.Dirty
            n.Queued <- false
            n.Freshness <- Freshness.Clean

            if shouldRun then
                n.RunDirect ()
#else
        if n.DisposedField then
            n.QueuedField <- false
        else
            if n.FreshnessField = Freshness.Check then
                let g = n.Graph
                Tracer.CheckStart (g, n.NodeId)
                NodeOps.WalkSources n
                Tracer.CheckResolved (g, n.NodeId, (n.FreshnessField = Freshness.Dirty))

                if g.HasLastRaised then
                    g.ClearLastRaised ()

                if n.FreshnessField = Freshness.Check then
                    n.FreshnessField <- Freshness.Clean

            let shouldRun = n.FreshnessField = Freshness.Dirty
            n.QueuedField <- false
            n.FreshnessField <- Freshness.Clean

            if shouldRun then
                n.RunDirect ()
#endif
#endif

#if !RANVIER_TRACE
#if !FABLE_COMPILER
    /// <summary>Runs the sole memo and its scheduled observer in one flush (<c>fused_single_memo_effect_flush_eq</c>).</summary>
    static member RunFusedMemoEffect(g: Graph, m: MemoNode, e: Node) =
        let prevAmbient = g.BeginFusedFlush ()

        e.Flags <-
            e.Flags
            ||| (NodeFlags.FreshCheck ||| NodeFlags.Queued)

        let moved = m.RunCallback.Invoke (m, e)

        if moved && (e.Flags &&& NodeFlags.Disposed) = 0 then
            e.Flags <-
                e.Flags
                &&& ~~~(NodeFlags.FreshMask ||| NodeFlags.Queued)

            if g.HasLastRaised then
                g.ClearLastRaised ()

            e.RunDirect ()
        elif
            not moved
            && g.StaleTransitions = 0
            && (e.Flags
                &&& (NodeFlags.Disposed ||| NodeFlags.FreshMask))
                =
                NodeFlags.FreshCheck
        then
            e.Flags <-
                e.Flags
                &&& ~~~(NodeFlags.FreshMask ||| NodeFlags.Queued)

            if g.HasLastRaised then
                g.ClearLastRaised ()
        else
            NodeOps.FinishFusedMemoEffect (g, e)

        g.EndFusedFlush prevAmbient

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member private FinishFusedMemoEffect(g: Graph, e: Node) =
        if (e.Flags &&& NodeFlags.Disposed) <> 0 then
            e.Flags <- e.Flags &&& ~~~NodeFlags.Queued
        else
            if (e.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck then
                if g.ActiveSkips <> 0 && e.SourcesField.count > 0 then
                    NodeOps.CheckActiveSource (e, 0)

                if
                    (e.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck
                    && g.StaleTransitions <> 0
                then
                    NodeOps.ResolveCheckAgain e

            if g.HasLastRaised then
                g.ClearLastRaised ()

            let shouldRun = (e.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshDirty

            e.Flags <-
                e.Flags
                &&& ~~~(NodeFlags.Queued ||| NodeFlags.FreshMask)

            if shouldRun then
                e.RunDirect ()

    /// <summary>Runs the sole scheduled signal observer (or newly created idle effect) in one flush (<c>fused_single_signal_effect_eq</c>).</summary>
    static member RunFusedEffect(g: Graph, n: Node) =
        let prevAmbient = g.BeginFusedFlush ()
        n.Flags <- n.Flags &&& ~~~NodeFlags.FreshMask
        n.RunDirect ()
        g.EndFusedFlush prevAmbient
#else
    /// <summary>Runs the sole memo and its scheduled observer in one flush (<c>fused_single_memo_effect_flush_eq</c>).</summary>
    static member RunFusedMemoEffect(g: Graph, n: MemoNode, e: Node) =
        let prevAmbient = g.BeginFusedFlush ()

        try
            NodeOps.FusedMemoEffect (g, n, e)
        with _ ->
            g.AbortFusedFlush prevAmbient
            reraise ()

        g.EndFusedFlush prevAmbient

    static member private FusedMemoEffect(g: Graph, n: MemoNode, e: Node) =
        e.Flags <-
            e.Flags
            ||| (NodeFlags.FreshCheck ||| NodeFlags.Queued)

        n.RunDirect ()

        if (e.Flags &&& NodeFlags.Disposed) <> 0 then
            e.Flags <- e.Flags &&& ~~~NodeFlags.Queued
        else
            if (e.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck then
                if g.ActiveSkips <> 0 && e.SourcesField.count > 0 then
                    NodeOps.CheckActiveSource (e, 0)

                if
                    (e.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshCheck
                    && g.StaleTransitions <> 0
                then
                    NodeOps.ResolveCheckAgain e

            if g.HasLastRaised then
                g.ClearLastRaised ()

            let shouldRun = (e.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshDirty

            e.Flags <-
                e.Flags
                &&& ~~~(NodeFlags.Queued ||| NodeFlags.FreshMask)

            if shouldRun then
                e.RunDirect ()

    /// <summary>Runs the sole scheduled signal observer in one flush (<c>fused_single_signal_effect_eq</c>).</summary>
    static member RunFusedEffect(g: Graph, n: Node) =
        let prevAmbient = g.BeginFusedFlush ()
        n.Flags <- n.Flags &&& ~~~NodeFlags.FreshMask

        try
            n.RunDirect ()
        with _ ->
            g.AbortFusedFlush prevAmbient
            reraise ()

        g.EndFusedFlush prevAmbient
#endif
#endif

/// <summary>Common tracked-read contract shared by reactive value nodes and pointwise collection descriptors.</summary>
[<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
type IReactive<'T> =
    /// <summary>The graph that owns this reactive value.</summary>
    abstract Graph: Graph
    /// <summary>Tracked read of the current value, raising <c>NotReadyException</c> when pending or the failure when errored.</summary>
    abstract ReadTracked: unit -> 'T

/// <summary>
/// A settable source.
/// </summary>
type Signal<'T> private (graph: Graph, initial: 'T, equal: IEqualityComparer<'T>) =
    let id = graph.NextId ()
#if !FABLE_COMPILER && !RANVIER_TRACE
    /// <summary>
    /// The signal's readers. On .NET the list is a struct held inline: no allocation for zero or one reader.
    /// </summary>
    let mutable observers = ObserverList.Create ()
#else
#if FABLE_COMPILER && !RANVIER_TRACE
    /// <summary>The signal's readers; null until the first reader arrives.</summary>
    let mutable observers: ObserverList = Platform.nil ()
#else
    let observers = ObserverList.Create ()
    do Tracer.Bind (observers, graph, id)
#endif
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
#if FABLE_COMPILER && !RANVIER_TRACE
            if Platform.isNull observers then
                observers <- ObserverList ()
#endif
            observers.Add (c, slot) ||| EdgeHandle.SignalBit

        member _.TryAddObserver(c, slot) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if Platform.isNull observers then
                observers <- ObserverList ()
#endif
            let h = observers.TryAdd (c, slot)

            if h >= 0 then h ||| EdgeHandle.SignalBit else h

        member _.RemoveObserverAt(c, handle) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if not (Platform.isNull observers) then
                observers.RemoveAt (c, handle)
#else
            observers.RemoveAt (c, handle)
#endif

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
#if !RANVIER_TRACE
            // Written with && so that Fable emits a short-circuit, not a ternary, on the unjournaled path.
            if
                not (
                    not (Platform.isNull graph.Journal)
                    && not (this.RecordWrite v)
                )
            then
                if not (ComparerSentinel.equals equal value v) then
                    this.SetChanged v
#else
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
#endif
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
    member private this.SetCustom(v: 'T) =
        if not (equal.Equals (value, v)) then
            this.SetChanged v
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
#endif

#if !FABLE_COMPILER || !RANVIER_TRACE
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.SetChanged(v: 'T) =
        value <- v
#if !FABLE_COMPILER && !RANVIER_TRACE
        let count = observers.Count

        if count = 1 then
            if EdgeHandle.isNodeSlot observers.FirstSlot then
                let n = Platform.fastCast<Node> observers.FirstRef
                let nf = n.Flags

                if (nf &&& NodeFlags.CleanMemoMask) = NodeFlags.KindMemo then
                    let m = Platform.fastCast<MemoNode> n
                    let mCount = m.Observers.Count

                    if
                        mCount = 1
                        && EdgeHandle.isNodeSlot m.Observers.FirstSlot
                    then
                        let e = Platform.fastCast<Node> m.Observers.FirstRef
                        let ef = e.Flags

                        if
                            (nf
                             &&& (NodeFlags.ActiveMask ||| NodeFlags.Disposed))
                                =
                                0
                            && Platform.isNull (box m.ScopeField)
                            && (ef &&& NodeFlags.KindMask) <> NodeFlags.KindMemo
                            && (ef
                                &&& (NodeFlags.FreshMask
                                     ||| NodeFlags.Disposed
                                     ||| NodeFlags.InCleanup
                                     ||| NodeFlags.Publishes
                                     ||| NodeFlags.Queued))
                                =
                                0
                            && e.SourcesField.count = 1
                            && obj.ReferenceEquals (e.SourcesField.first, m)
                            && graph.CanFuseIdleFlush ()
                        then
                            NodeOps.RunFusedMemoEffect (graph, m, e)
                        else
                            m.Flags <- nf ||| NodeFlags.FreshDirty
                            m.Graph.NoteStaleTransition ()
                            NodeOps.NotifyCheckSpine m

                            if graph.QueuePending then
                                graph.RequestFlushFromEntered ()
                    else
                        m.Flags <- nf ||| NodeFlags.FreshDirty
                        m.Graph.NoteStaleTransition ()

                        if mCount <> 0 then
                            NodeOps.NotifyCheckSpine m

                        if graph.QueuePending then
                            graph.RequestFlushFromEntered ()
                elif
                    (nf &&& NodeFlags.KindMask) <> NodeFlags.KindMemo
                    && (nf
                        &&& (NodeFlags.Disposed
                             ||| NodeFlags.InCleanup
                             ||| NodeFlags.Publishes
                             ||| NodeFlags.Queued))
                        =
                        0
                    && graph.CanFuseIdleFlush ()
                then
                    NodeOps.RunFusedEffect (graph, n)
                else
                    NodeOps.MarkDirty n

                    if graph.QueuePending then
                        graph.RequestFlushFromEntered ()
            else
                (Platform.fastCast<IComputation> observers.FirstRef).MarkDirty()

                if graph.QueuePending then
                    graph.RequestFlushFromEntered ()
        elif count > 1 then
            if graph.IsIdleForMarks () then
                observers.NotifyDirtyIdle ()
            else
                observers.NotifyDirty ()

            if graph.QueuePending then
                graph.RequestFlushFromEntered ()
        elif graph.QueuePending then
            graph.RequestFlushFromEntered ()
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        if not (Platform.isNull observers) then
            let count = observers.Count

            if count = 1 then
                if EdgeHandle.isNodeSlot observers.FirstSlot then
                    let n = Platform.fastCast<Node> observers.FirstRef
                    let nf = n.Flags

                    if (nf &&& NodeFlags.CleanMemoMask) = NodeFlags.KindMemo then
                        let m = Platform.fastCast<MemoNode> n
                        let mObs = m.Observers

                        if
                            not (Platform.isNull mObs)
                            && mObs.Count = 1
                            && EdgeHandle.isNodeSlot mObs.FirstSlot
                        then
                            let e = Platform.fastCast<Node> mObs.FirstRef
                            let ef = e.Flags

                            if
                                (nf
                                 &&& (NodeFlags.ActiveMask ||| NodeFlags.Disposed))
                                    =
                                    0
                                && Platform.isNull (box m.ScopeField)
                                && (ef &&& NodeFlags.KindMask) <> NodeFlags.KindMemo
                                && (ef
                                    &&& (NodeFlags.FreshMask
                                         ||| NodeFlags.Disposed
                                         ||| NodeFlags.InCleanup
                                         ||| NodeFlags.Publishes
                                         ||| NodeFlags.Queued))
                                    =
                                    0
                                && e.SourcesField.count = 1
                                && obj.ReferenceEquals (e.SourcesField.first, m)
                                && graph.CanFuseIdleFlush ()
                            then
                                NodeOps.RunFusedMemoEffect (graph, m, e)
                            else
                                m.Flags <- nf ||| NodeFlags.FreshDirty
                                m.Graph.NoteStaleTransition ()
                                NodeOps.NotifyCheckSpine m

                                if graph.QueuePending then
                                    graph.RequestFlushFromEntered ()
                        else
                            m.Flags <- nf ||| NodeFlags.FreshDirty
                            m.Graph.NoteStaleTransition ()

                            if not (Platform.isNull mObs) && mObs.Count <> 0 then
                                NodeOps.NotifyCheckSpine m

                            if graph.QueuePending then
                                graph.RequestFlushFromEntered ()
                    elif
                        (nf &&& NodeFlags.KindMask) <> NodeFlags.KindMemo
                        && (nf
                            &&& (NodeFlags.Disposed
                                 ||| NodeFlags.InCleanup
                                 ||| NodeFlags.Publishes
                                 ||| NodeFlags.Queued))
                            =
                            0
                        && graph.CanFuseIdleFlush ()
                    then
                        NodeOps.RunFusedEffect (graph, n)
                    else
                        NodeOps.MarkDirty n

                        if graph.QueuePending then
                            graph.RequestFlushFromEntered ()
                else
                    (Platform.fastCast<IComputation> observers.FirstRef).MarkDirty()

                    if graph.QueuePending then
                        graph.RequestFlushFromEntered ()
            elif count > 1 then
                if graph.IsIdleForMarks () then
                    observers.NotifyDirtyIdle ()
                else
                    observers.NotifyDirty ()

                if graph.QueuePending then
                    graph.RequestFlushFromEntered ()
            elif graph.QueuePending then
                graph.RequestFlushFromEntered ()
        elif graph.QueuePending then
            graph.RequestFlushFromEntered ()
#else
        Tracer.Write (observers, true, box v)
        observers.NotifyDirty ()
        Tracer.Notified observers

        if graph.QueuePending then
            graph.RequestFlushFromEntered ()
#endif
#endif
#endif

#if !FABLE_COMPILER
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
    /// Writes <c>v</c> as the setter does, for a caller that has already entered the graph (not necessarily inside a batch).
    /// </summary>
    member internal this.SetFromEntered(v: 'T) =
        if
            Platform.isNull graph.Journal
            || this.RecordWrite v
        then
            if not (ComparerSentinel.equals equal value v) then
#if !FABLE_COMPILER || !RANVIER_TRACE
                this.SetChanged v
#else
                value <- v
                Tracer.Write (observers, true, box v)
                observers.NotifyDirty ()
                Tracer.Notified observers
                graph.RequestFlushFromEntered ()
#endif
            else
                Tracer.Write (observers, false, box v)

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
#if !RANVIER_TRACE
                    if not (Platform.isNull observers) then
                        observers.NotifyDirtyOther running
#else
                    observers.NotifyDirtyOther running
#endif
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

            observers.NotifyDirtyOther (EffectHandles.unwrap running)

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

        observers.NotifyDirtyOther (EffectHandles.unwrap running)

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
        if
            Platform.isNull graph.Journal
            || this.RecordWrite v
        then
            if not (ComparerSentinel.equals equal value v) then
                value <- v
                Tracer.Write (observers, true, box v)
#if FABLE_COMPILER && !RANVIER_TRACE
                if not (Platform.isNull observers) then
                    observers.NotifyDirty ()
#else
                observers.NotifyDirty ()
#endif
                Tracer.Notified observers
            else
                Tracer.Write (observers, false, box v)

    /// <summary>Marks every reader for a check, leaving the value unchanged.</summary>
    member internal _.NotifyCheck() =
#if FABLE_COMPILER && !RANVIER_TRACE
        if not (Platform.isNull observers) then
            observers.NotifyCheck ()
#else
        observers.NotifyCheck ()
#endif

    /// <summary>
    /// Non-throwing, tracked read.
    /// </summary>
#if FABLE_COMPILER
    member this.TryValue: Reading<'T> =
        graph.Track (this :> ISource)
        Ready value
#else
    member this.TryValue: Reading<'T> = this.GetTryFast ()

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
#if FABLE_COMPILER && !RANVIER_TRACE
        if Platform.isNull observers then 0 else observers.Count
#else
        observers.Count
#endif

    interface IReactive<'T> with
        member _.Graph = graph

        member this.ReadTracked() =
            this.Value

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
#if !FABLE_COMPILER && !RANVIER_TRACE
    /// <summary>The source's readers, held inline: no allocation for zero or one reader.</summary>
    let mutable observers = ObserverList.Create ()
#else
#if FABLE_COMPILER && !RANVIER_TRACE
    /// <summary>Slot 0 held inline so single-reader async sources allocate zero <c>ObserverList</c> objects; <c>observers</c> stays null until a second reader arrives.</summary>
    let mutable firstRef: obj = Platform.nil ()
    let mutable firstSlot = EdgeHandle.Unlinked
    let mutable observers: ObserverList = Platform.nil ()
#else
    let observers = ObserverSet ()
    do Tracer.Bind (observers, graph, id)
#endif
#endif
    let mutable value = Platform.zero<'T>()
    let mutable failure: Failure = Platform.nil ()

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
#if FABLE_COMPILER && !RANVIER_TRACE
            if not (Platform.isNull observers) then
                observers.Add (c, slot)
            elif Platform.isNull firstRef then
                firstRef <- box c
                firstSlot <- slot
#if RANVIER_COUNTERS
                Counters.ObserverInserted ()
#endif
                0
            else
                let list = ObserverList ()
                list.FirstRef <- firstRef
                list.FirstSlot <- firstSlot
                list.Count <- 1
                list.Latest <- 0
                firstRef <- Platform.nil ()
                firstSlot <- EdgeHandle.Unlinked
                observers <- list
                list.Add (c, slot)
#else
            observers.Add (c, slot)
#endif

        member _.TryAddObserver(c, slot) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if not (Platform.isNull observers) then
                observers.TryAdd (c, slot)
            elif Platform.isNull firstRef then
                firstRef <- box c
                firstSlot <- slot
#if RANVIER_COUNTERS
                Counters.ObserverInserted ()
#endif
                0
            elif obj.ReferenceEquals (firstRef, box c) then
                EdgeHandle.Unlinked
            else
                let list = ObserverList ()
                list.FirstRef <- firstRef
                list.FirstSlot <- firstSlot
                list.Count <- 1
                list.Latest <- 0
                firstRef <- Platform.nil ()
                firstSlot <- EdgeHandle.Unlinked
                observers <- list
                list.Add (c, slot)
#else
            observers.TryAdd (c, slot)
#endif

        member _.RemoveObserverAt(c, handle) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if not (Platform.isNull observers) then
                observers.RemoveAt (c, handle)
            elif handle >= 0 then
                let idx = handle &&& EdgeHandle.IndexMask

                if
                    idx <> 0
                    || not (obj.ReferenceEquals (firstRef, box c))
                then
                    raise (
                        InvalidOperationException (
                            "Edge handle "
                            + string idx
                            + " of "
                            + (if Platform.isNull firstRef then "0" else "1")
                            + " does not hold the observer removed."
                        )
                    )
#if RANVIER_COUNTERS
                Counters.ObserverRemoved ()
#endif
                firstRef <- Platform.nil ()
                firstSlot <- EdgeHandle.Unlinked
#else
            observers.RemoveAt (c, handle)
#endif

        /// <summary>
        /// Nothing to bring current: the value is whatever was last written.
        /// </summary>
        member _.UpdateIfNecessary() = ()

        member _.IsStale = false

        member _.IsActive = false

    member _.Status = status

#if FABLE_COMPILER && !RANVIER_TRACE
    member inline private _.NotifyOne(refObj: obj, slot: int) =
        if EdgeHandle.isNodeSlot slot then
            let n = Platform.fastCast<Node> refObj
            let nf = n.Flags

            if
                (nf &&& NodeFlags.DirtyAnnouncedMask) = NodeFlags.FreshDirty
                && ((nf &&& NodeFlags.KindMask) = NodeFlags.KindMemo
                    || (nf &&& NodeFlags.Queued) <> 0)
            then
                if graph.BatchDepth = 0 && graph.QueuePending then
                    graph.RequestFlushFromEntered ()
            elif
                (nf &&& NodeFlags.KindMask) <> NodeFlags.KindMemo
                && (nf
                    &&& (NodeFlags.Disposed
                         ||| NodeFlags.InCleanup
                         ||| NodeFlags.Publishes
                         ||| NodeFlags.Queued))
                    =
                    0
                && graph.CanFuseIdleFlush ()
            then
                NodeOps.RunFusedEffect (graph, n)
            else
                NodeOps.MarkDirty n

                if graph.QueuePending then
                    graph.RequestFlushFromEntered ()
        else
            (Platform.fastCast<IComputation> refObj).MarkDirty()

            if graph.QueuePending then
                graph.RequestFlushFromEntered ()
#endif

    member private this.NotifyAndFlush() =
#if !FABLE_COMPILER && !RANVIER_TRACE
        let count = observers.Count

        if count = 1 then
            if EdgeHandle.isNodeSlot observers.FirstSlot then
                let n = Platform.fastCast<Node> observers.FirstRef
                let nf = n.Flags

                if
                    (nf &&& NodeFlags.DirtyAnnouncedMask) = NodeFlags.FreshDirty
                    && ((nf &&& NodeFlags.KindMask) = NodeFlags.KindMemo
                        || (nf &&& NodeFlags.Queued) <> 0)
                then
                    if graph.BatchDepth = 0 && graph.QueuePending then
                        graph.RequestFlushFromEntered ()
                elif
                    (nf &&& NodeFlags.KindMask) <> NodeFlags.KindMemo
                    && (nf
                        &&& (NodeFlags.Disposed
                             ||| NodeFlags.InCleanup
                             ||| NodeFlags.Publishes
                             ||| NodeFlags.Queued))
                        =
                        0
                    && graph.CanFuseIdleFlush ()
                then
                    NodeOps.RunFusedEffect (graph, n)
                else
                    NodeOps.MarkDirty n

                    if graph.QueuePending then
                        graph.RequestFlushFromEntered ()
            else
                (Platform.fastCast<IComputation> observers.FirstRef).MarkDirty()

                if graph.QueuePending then
                    graph.RequestFlushFromEntered ()
        elif count > 1 then
            observers.NotifyDirty ()

            if graph.QueuePending then
                graph.RequestFlushFromEntered ()
        elif graph.QueuePending then
            graph.RequestFlushFromEntered ()
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        if not (Platform.isNull firstRef) then
            this.NotifyOne (firstRef, firstSlot)
        elif not (Platform.isNull observers) then
            let count = observers.Count

            if count = 1 then
                this.NotifyOne (observers.FirstRef, observers.FirstSlot)
            elif count > 1 then
                observers.NotifyDirty ()

                if graph.QueuePending then
                    graph.RequestFlushFromEntered ()
            elif graph.QueuePending then
                graph.RequestFlushFromEntered ()
        elif graph.QueuePending then
            graph.RequestFlushFromEntered ()
#else
        observers.NotifyDirty ()
        Tracer.Notified observers
        graph.RequestFlushFromEntered ()
#endif
#endif

#if FABLE_COMPILER
    member inline private this.SettleOnGraph(v: 'T) =
#else
    member private this.SettleOnGraph(v: 'T) =
#endif
        if
            not (
                not (Platform.isNull graph.Journal)
                && not (this.RecordSettle v)
            )
        then
            value <- v
            status <- Status.None
            Tracer.SourceSettled (observers, false, box v)
            this.NotifyAndFlush ()

#if FABLE_COMPILER
    member inline private this.FailOnGraph(reason: exn) =
#else
    member private this.FailOnGraph(reason: exn) =
#endif
        if
            not (
                not (Platform.isNull graph.Journal)
                && not (this.RecordFail reason)
            )
        then
            failure <- Failure (reason, (this :> INode), true)
            status <- Status.Error
            Tracer.SourceSettled (observers, true, reason)
            this.NotifyAndFlush ()

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
    member this.Value: 'T = this.GetFast ()

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

    member inline internal _.Peek = value

    member inline internal _.Failure = failure

    interface IReactive<'T> with
        member _.Graph = graph

        member this.ReadTracked() =
            this.Value

#if FABLE_COMPILER
type internal Map2Delegate<'A, 'B, 'C> = 'A -> 'B -> 'C
#else
type internal Map2Delegate<'A, 'B, 'C> = OptimizedClosures.FSharpFunc<'A, 'B, 'C>
#endif

/// <summary>Helpers shared by closed-window Phase 2 callbacks of <c>*On</c> computations and combinators.</summary>
module internal UntrackedPhase =
    [<Literal>]
    let memoOnPending =
        "A split memo's compute read a pending source. Read pending sources in read, which suspends the memo until they settle."

    [<Literal>]
    let asyncOnPending =
        "A split async memo's compute read a pending source. Read pending sources in read, which suspends the async memo until they settle."

    [<Literal>]
    let streamOnPending =
        "A split stream memo's start read a pending source. Read pending sources in read, which suspends the stream memo until they settle."

    [<Literal>]
    let mapPending =
        "A map combinator's mapping read a pending source. Pass the pending source as the combinator's input, or combine multiple sources with map2, zip, or bind."

    [<Literal>]
    let bindPending =
        "A bind combinator's binder read a pending source synchronously. Read pending sources upstream of bind, or return a Memo that reads the pending source."

    [<Literal>]
    let crossGraphNodes = "Cannot combine nodes belonging to different graphs."

    [<Literal>]
    let crossGraphBind = "The Memo returned by bind belongs to a different Graph."

    [<Literal>]
    let nullBindReturn = "The binder returned a null Memo."

#if FABLE_COMPILER
    let inline adaptMap2 (f: 'A -> 'B -> 'C) : Map2Delegate<'A, 'B, 'C> = f
    let inline invokeMap2 (d: Map2Delegate<'A, 'B, 'C>) (a: 'A) (b: 'B) : 'C = d a b
#else
    let inline adaptMap2 (f: 'A -> 'B -> 'C) : Map2Delegate<'A, 'B, 'C> =
        OptimizedClosures.FSharpFunc<'A, 'B, 'C>.Adapt f

    let inline invokeMap2 (d: Map2Delegate<'A, 'B, 'C>) (a: 'A) (b: 'B) : 'C =
        d.Invoke (a, b)
#endif

#if FABLE_COMPILER
    [<Fable.Core.Emit("$0 == null")>]
    let isNullFn1 (value: 'A -> 'R) : bool =
        Fable.Core.Util.jsNative

    [<Fable.Core.Emit("$0 == null")>]
    let isNullFn2 (value: 'A -> 'B -> 'R) : bool =
        Fable.Core.Util.jsNative

    [<Fable.Core.Emit("$0 == null")>]
    let isNullFn3 (value: 'A -> 'B -> 'C -> 'R) : bool =
        Fable.Core.Util.jsNative

    [<Fable.Core.Emit("null")>]
    let nullFn2<'A, 'B, 'R> () : ('A -> 'B -> 'R) =
        Fable.Core.Util.jsNative
#else
    let inline isNullFn1 (value: 'A -> 'R) : bool =
        Operators.isNull (box value)

    let inline isNullFn2 (value: 'A -> 'B -> 'R) : bool =
        Operators.isNull (box value)

    let inline isNullFn3 (value: 'A -> 'B -> 'C -> 'R) : bool =
        Operators.isNull (box value)

    let inline nullFn2<'A, 'B, 'R> () : ('A -> 'B -> 'R) =
        Unchecked.defaultof<'A -> 'B -> 'R>
#endif

    /// <summary>
    /// Wraps a <c>NotReadyException</c> raised inside an untracked Phase 2 callback into an
    /// <c>InvalidOperationException</c> carrying <c>ex</c> as its <c>InnerException</c>.
    /// </summary>
    let wrapPending (message: string) (ex: exn) : exn =
        match ex with
        | NotReadyException _ ->
#if FABLE_COMPILER
            let failure = InvalidOperationException message
            Platform.setInner failure ex
            failure :> exn
#else
            InvalidOperationException (message, ex) :> exn
#endif
        | _ -> ex

/// <summary>Type-erased Phase 1/Phase 2 driver stored in <c>Memo&lt;'U&gt;</c> for split memos.</summary>
[<AbstractClass>]
type internal MemoOnRunner<'U>() =
    let mutable moved = false

    member inline _.Moved
        with get () = moved
        and set (v: bool) = moved <- v

    abstract Execute:
        origin: INode *
        n: MemoNode *
        outputEqual: IEqualityComparer<'U> *
        previous: 'U *
        previousStatus: Status *
        previousFailure: Failure *
        previousPending: INode ->
            'U

/// <summary>
/// Typed state of a two-phase <c>Memo&lt;'U&gt;</c> created by <c>createMemoOn*</c> or <c>createMemoWithOn</c> (Theorem B16).
/// </summary>
[<Sealed>]
type internal MemoOnState<'T, 'U>(read: unit -> 'T, compute: 'T -> 'U, ownerMode: ScopeMode, keyEqual: IEqualityComparer<'T>) =
    inherit MemoOnRunner<'U>()

    let mutable node: MemoNode = Platform.nil ()
    let mutable hasKey = false
    let mutable lastKey = Unchecked.defaultof<'T>
    let mutable phase2Failure: Failure = Platform.nil ()
    let mutable computing = false

    member internal _.Bind(n: MemoNode) = node <- n

    member inline private _.ClearActive(n: MemoNode) =
#if !RANVIER_TRACE
        n.Flags <- n.Flags &&& ~~~NodeFlags.ActiveMask
#else
#if FABLE_COMPILER
        n.Active <- 0uy
#else
        n.ActiveField <- 0uy
#endif
#endif

    member inline private _.IsViolated(n: MemoNode) =
#if !RANVIER_TRACE
        (n.Flags &&& NodeFlags.Violated) <> 0
#else
#if FABLE_COMPILER
        n.Violated
#else
        n.ViolatedField
#endif
#endif

    member inline private _.ClearViolated(n: MemoNode) =
#if !RANVIER_TRACE
        n.Flags <- n.Flags &&& ~~~NodeFlags.Violated
#else
#if FABLE_COMPILER
        n.Violated <- false
#else
        n.ViolatedField <- false
#endif
#endif

    member inline private _.SetPublished(n: MemoNode) =
#if !RANVIER_TRACE
        n.Flags <- n.Flags ||| NodeFlags.Published
#else
#if FABLE_COMPILER
        n.Published <- true
#else
        n.PublishedField <- true
#endif
#endif

    member inline private _.StatusMoved(n: MemoNode, previousStatus: Status, previousFailure: Failure) =
        n.Status <> previousStatus
        || (not (obj.ReferenceEquals (n.FailureField, previousFailure))
            && Failure.Moved (n.FailureField, previousFailure))

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RecordPhase1Failure(origin: INode, n: MemoNode, ex: exn, previousFailure: Failure) =
        let g = n.Graph
        this.ClearActive n

        if this.IsViolated n then
            this.ClearViolated n
            n.FailureField <- g.FailureOf (ScopeMessages.failure ScopeMode.Pure ex, origin, previousFailure)
            n.SetStatusError ()
        else
            match ex with
            | NotReadyException source ->
                n.PendingSourceField <- source
                n.SetStatusPending ()
            | _ ->
                n.FailureField <- g.FailureOf (ex, origin, previousFailure)
                n.SetStatusError ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RecordPhase2Failure(origin: INode, n: MemoNode, ex: exn, previousFailure: Failure) =
        let g = n.Graph
        this.ClearActive n

        let err =
            if this.IsViolated n then
                this.ClearViolated n
                ScopeMessages.failure ScopeMode.Pure ex
            else
                UntrackedPhase.wrapPending UntrackedPhase.memoOnPending ex

        let f = g.FailureOf (err, origin, previousFailure)
        phase2Failure <- f
        n.FailureField <- f
        n.SetStatusError ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CompareKeyGuarded(origin: INode, n: MemoNode, nextKey: 'T, previousFailure: Failure) : byte =
        try
            if ComparerSentinel.equals keyEqual lastKey nextKey then
                1uy
            else
                0uy
        with ex ->
            this.ClearActive n
            n.FailureField <- n.Graph.FailureOf (ex, origin, previousFailure)
            n.SetStatusError ()
            2uy

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CompareOutputGuarded
        (origin: INode, n: MemoNode, outputEqual: IEqualityComparer<'U>, previous: 'U, result: 'U, previousFailure: Failure)
        : 'U =
        try
            let changed = not (ComparerSentinel.equals outputEqual previous result)
            phase2Failure <- Platform.nil ()
            this.Moved <- changed
            result
        with ex ->
            let f = n.Graph.FailureOf (ex, origin, previousFailure)
            phase2Failure <- f
            n.FailureField <- f
            n.SetStatusError ()
            this.Moved <- true
            previous

    override this.Execute
        (
            origin: INode,
            n: MemoNode,
            outputEqual: IEqualityComparer<'U>,
            previous: 'U,
            previousStatus: Status,
            previousFailure: Failure,
            previousPending: INode
        ) : 'U =
        let g = n.Graph
        n.SourcesField.BeginRun ()
        let mutable nextKey = Unchecked.defaultof<'T>
        let mutable phase1Ok = false

#if FABLE_COMPILER
        try
            try
#if !RANVIER_TRACE
                nextKey <- g.RunHostedNode (n, read, ())
#else
                nextKey <- g.RunHosted (n :> IComputation, read)
#endif
                if this.IsViolated n then
                    raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                phase1Ok <- true
            finally
                if n.IsDisposed then
                    n.SourcesField.Clear (n :> IComputation)
                else
                    n.SourcesField.EndRun (n :> IComputation)
        with ex ->
            this.RecordPhase1Failure (origin, n, ex, previousFailure)
#else
        let saved = g.EnterHostedNode n

        let phase1Ex =
            try
                nextKey <- read ()

                if g.RaisedPending || this.IsViolated n then
                    g.CheckRaised ()

                    if this.IsViolated n then
                        raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                null
            with ex ->
                ex

        g.LeaveHosted saved

        if not (Platform.isNull phase1Ex) then
            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                n.SourcesField.EndRun (n :> IComputation)

            this.RecordPhase1Failure (origin, n, phase1Ex, previousFailure)
        else
            if
                n.IsDisposed
                || n.SourcesField.Cursor < n.SourcesField.Count
            then
                if n.IsDisposed then
                    n.SourcesField.Clear (n :> IComputation)
                else
                    n.SourcesField.EndRun (n :> IComputation)

            phase1Ok <- true
#endif

        if not phase1Ok then
            this.Moved <- this.StatusMoved (n, previousStatus, previousFailure)
            previous
        else
            let keyCmp =
                if not hasKey then
                    0uy
                elif SafeEquality.isSafe keyEqual then
                    if ComparerSentinel.equals keyEqual lastKey nextKey then
                        1uy
                    else
                        0uy
                else
                    this.CompareKeyGuarded (origin, n, nextKey, previousFailure)

            if keyCmp = 2uy then
                this.Moved <- this.StatusMoved (n, previousStatus, previousFailure)
                previous
            elif keyCmp = 1uy then
                this.ClearActive n

                if not (Platform.isNull phase2Failure) then
                    n.FailureField <- phase2Failure
                    n.SetStatusError ()

                this.Moved <- this.StatusMoved (n, previousStatus, previousFailure)
                previous
            else
                lastKey <- nextKey
                hasKey <- true

                if not (Platform.isNull (box n.ScopeField)) then
                    g.Discharge n.ScopeField

                if n.IsDisposed then
                    this.ClearActive n

                    if StatusBits.isError previousStatus then
                        n.FailureField <- previousFailure
                        n.SetStatusError ()
                    elif StatusBits.isPending previousStatus then
                        n.PendingSourceField <- previousPending
                        n.SetStatusPending ()

                    this.Moved <- false
                    previous
                else
                    if g.HasLastRaised then
                        g.ClearLastRaised ()

                    let mutable result = Unchecked.defaultof<'U>
                    computing <- true

#if FABLE_COMPILER
                    let mutable phase2Ex: exn = Platform.nil ()

                    try
                        try
                            result <- g.RunUntrackedHostedArg (this :> IScopeHost, compute, nextKey)

                            if this.IsViolated n then
                                raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))
                        finally
                            computing <- false
                    with ex ->
                        phase2Ex <- Thrown.hold ex

                    if not (Platform.isNull phase2Ex) then
                        this.RecordPhase2Failure (origin, n, Thrown.take phase2Ex, previousFailure)
                        this.Moved <- this.StatusMoved (n, previousStatus, previousFailure)
                        previous
#else
                    let hasJournal = not (Platform.isNull g.Journal)

                    if hasJournal then
                        g.Journal.EnterDerived ()

                    let savedPhase2 = g.EnterUntrackedHosted (this :> IScopeHost)

                    let phase2Ex =
                        try
                            result <- compute nextKey

                            if this.IsViolated n then
                                raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                            null
                        with ex ->
                            ex

                    g.LeaveUntrackedHosted savedPhase2

                    if hasJournal then
                        g.Journal.LeaveDerived ()

                    computing <- false

                    if not (Platform.isNull phase2Ex) then
                        this.RecordPhase2Failure (origin, n, phase2Ex, previousFailure)
                        this.Moved <- this.StatusMoved (n, previousStatus, previousFailure)
                        previous
#endif
                    else
                        this.ClearActive n
                        this.SetPublished n
                        phase2Failure <- Platform.nil ()

                        if previousStatus <> Status.None then
                            this.Moved <- true
                            result
                        elif SafeEquality.isSafe outputEqual then
                            this.Moved <-
#if !RANVIER_TRACE
                                (n.ObserverCount <> 0 || g.QueuePending)
                                && not (ComparerSentinel.equals outputEqual previous result)
#else
                                not (ComparerSentinel.equals outputEqual previous result)
#endif
                            result
                        else
                            this.CompareOutputGuarded (origin, n, outputEqual, previous, result, previousFailure)

    interface IScopeHost with
        member _.CanOwn = computing && ownerMode = ScopeMode.Owning

        member _.Scope =
            if computing && ownerMode = ScopeMode.Owning then
                if Platform.isNull (box node.ScopeField) then
                    let s = new Owner (node.Graph.Root)
                    node.ScopeField <- s
                    Tracer.ScopeNew (s, node.Graph, node.NodeId)

                    if node.IsDisposed then
                        s.Dispose ()

                node.ScopeField
            else
#if !RANVIER_TRACE
                node.Flags <- node.Flags ||| NodeFlags.Violated
#else
#if FABLE_COMPILER
                node.Violated <- true
#else
                node.ViolatedField <- true
#endif
#endif
                raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

#if !RANVIER_TRACE
/// <summary>Link in an unobserved <c>map</c> fusion chain allowing lazy de-fusion from any claimed intermediate.</summary>
[<AllowNullLiteral>]
type internal IFusedChainNode =
    abstract FusedNext: IFusedChainNode
    abstract MaterializeFromTail: unit -> unit

/// <summary>Base runner for fusable 1..4+ stage <c>map</c> chains (Theorem B18).</summary>
[<AbstractClass>]
type internal FusedRunner<'U>() =
    inherit MemoOnRunner<'U>()

    let mutable ownerNode: MemoNode = Unchecked.defaultof<MemoNode>
    let mutable fusedNext: IFusedChainNode = Platform.nil ()

    member inline _.BindNode(n: MemoNode) =
        ownerNode <- n

    member inline _.FusedNext
        with get () = fusedNext
        and set (v: IFusedChainNode) =
            fusedNext <- v

            if not (obj.ReferenceEquals (ownerNode, null)) then
                if Platform.isNull v then
                    ownerNode.Flags <- ownerNode.Flags &&& ~~~NodeFlags.ClaimedFused
                else
                    ownerNode.Flags <- ownerNode.Flags ||| NodeFlags.ClaimedFused

    member inline _.IsClaimed = not (Platform.isNull fusedNext)

    abstract EvalSignalLeaf: tailMemo: obj * n: MemoNode * g: Graph * safeTailEq: bool * outputEqual: IEqualityComparer<'U> * previous: 'U -> bool

    abstract ExecuteFused:
        tailMemo: obj * n: MemoNode * outputEqual: IEqualityComparer<'U> * previous: 'U * previousStatus: Status * previousFailure: Failure -> 'U

    abstract MaterializeTail: tailMemo: obj -> unit
    abstract ClearIntermediates: unit -> unit
    abstract SetPreRefreshed: unit -> unit
    default _.SetPreRefreshed() = ()

    override this.Execute(origin, n, outputEqual, previous, previousStatus, previousFailure, _previousPending) =
        this.ExecuteFused (box origin, n, outputEqual, previous, previousStatus, previousFailure)
#endif

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
type Memo<'T> private (initialComputeObj: obj, initialComputeKind: byte, seed: 'T, equal: IEqualityComparer<'T>, node: MemoNode) =
    do Tracer.Bind (node.Observers, node.Graph, node.NodeId)
    do Tracer.Bind (node.SourcesField, node.Graph, node.NodeId)

#if !RANVIER_TRACE
    let mutable computeObj = initialComputeObj
    let mutable computeKind = initialComputeKind
#else
    let computeObj = initialComputeObj
    let computeKind = initialComputeKind
#endif
    let mutable value = Unchecked.defaultof<'T>

#if RANVIER_COUNTERS
    do Counters.MemoCreated ()
#endif

    member inline internal _.ComputeObj = computeObj
    member inline internal _.ComputeKind = computeKind
    member inline internal _.EncodedEqual = equal
    member internal _.Graph = node.Graph
    member inline internal _.PeekRaw = value

#if !FABLE_COMPILER && !RANVIER_TRACE
    member inline private this.RunTopCoreWith(n: MemoNode, child: Node, previousPending: bool, [<InlineIfLambda>] invoke: 'T -> 'T) : bool =
        let g = n.Graph
        let prevOwner = g.EnterHostedTop n
        let previous = value

        n.Flags <-
            (n.Flags
             &&& ~~~(NodeFlags.FreshMask ||| NodeFlags.Unannounced))
            ||| NodeFlags.ActiveBody

        n.SourcesField.BeginRun ()
        n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
        Counters.MemoRecomputed ()
#endif
        let result =
            try
                invoke previous
            with ex ->
                g.Caught <- ex
                Unchecked.defaultof<'T>

        if
            not (Platform.isNull g.Caught)
            || g.RaisedPending
            || (n.Flags &&& NodeFlags.Violated) <> 0
        then
            this.RunFailedTop (n, prevOwner, previousPending, this.TakeBodyException ())
            false
        else
            value <- result
            g.LeaveHostedTop (prevOwner, previousPending)
            n.Flags <- n.Flags &&& ~~~NodeFlags.ActiveMask

            let trim =
                (n.Flags &&& NodeFlags.Disposed) <> 0
                || n.SourcesField.cursor < n.SourcesField.count

            if trim then
                this.FinishTop (n, prevOwner, previousPending, previous, true)
                false
            elif n.Observers.Count <> 0 then
                if not (ComparerSentinel.equals null previous value) then
                    if
                        not (obj.ReferenceEquals (child, null))
                        && n.Observers.Count = 1
                        && g.StaleTransitions = 0
                        && (n.Flags &&& NodeFlags.FreshMask) = 0
                        && obj.ReferenceEquals (n.Observers.FirstRef, box child)
                    then
                        if g.QueuePending then
                            g.OweFlush ()

                        true
                    elif NodeOps.TryPromoteTop n then
                        if g.QueuePending then
                            g.OweFlush ()

                        if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
                            this.NoteStale n

                        false
                    else
                        this.FinishTop (n, prevOwner, previousPending, previous, false)
                        false
                else
                    if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
                        this.NoteStale n

                    false
            else
                // Theorem B9: leaf memo (Observers.Count = 0 && not trim) skips equality check unless QueuePending needs OweFlush.
                if
                    g.QueuePending
                    && not (ComparerSentinel.equals null previous value)
                then
                    g.OweFlush ()

                if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
                    this.NoteStale n

                false

    member inline private this.RunTopPendingWith(n: MemoNode, child: Node, [<InlineIfLambda>] invoke: 'T -> 'T) : bool =
        this.RunTopCoreWith (n, child, true, invoke)

    member inline private this.RunReadyNestedWith(n: MemoNode, [<InlineIfLambda>] invoke: 'T -> 'T) =
        let g = n.Graph
        let parent = g.CurrentNode
        let previousOwner = g.EnterHostedTop n
        let previous = value

        n.Flags <-
            (n.Flags
             &&& ~~~(NodeFlags.FreshMask ||| NodeFlags.Unannounced))
            ||| NodeFlags.ActiveBody

        n.SourcesField.BeginRun ()
        n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
        Counters.MemoRecomputed ()
#endif
        let result =
            try
                invoke previous
            with ex ->
                g.Caught <- ex
                Unchecked.defaultof<'T>

        let failed =
            not (Platform.isNull g.Caught)
            || g.RaisedPending
            || (n.Flags &&& NodeFlags.Violated) <> 0

        if not failed then
            value <- result
            g.LeaveNestedReader (parent, previousOwner)
            n.Flags <- n.Flags &&& ~~~NodeFlags.ActiveMask

            if
                (n.Flags &&& NodeFlags.Disposed) <> 0
                || n.SourcesField.cursor < n.SourcesField.count
            then
                this.FinishSources ()
        else
            let saved =
                HostedFrame (Unchecked.defaultof<IComputation>, parent, previousOwner, true, Unchecked.defaultof<INode>)

            this.RunFailed (saved, this.TakeBodyException (), null)

        if
            failed
            || ((n.Observers.Count <> 0 || g.QueuePending)
                && not (ComparerSentinel.equals null previous value))
        then
            if
                n.Observers.Count <> 0
                && (n.Observers.Count <> 1
                    || not (obj.ReferenceEquals (n.Observers.FirstRef, box parent))
                    || g.StaleTransitions <> 0)
            then
                NodeOps.NotifyMoved n

            if g.QueuePending then
                g.OweFlush ()

        if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
            this.NoteStale n

    member inline private this.RunFromNodeWith(n: MemoNode, child: Node, [<InlineIfLambda>] invoke: 'T -> 'T) : bool =
        if (n.Flags &&& NodeFlags.FastRunMemoMask) = NodeFlags.FastRunMemoValue then
            let g = n.Graph

            if g.CanEnterHostedTop () then
                if g.OwnerPending then
                    this.RunTopPendingWith (n, child, invoke)
                else
                    this.RunTopCoreWith (n, child, false, invoke)
            elif g.CanNestInReader then
                this.RunReadyNestedWith (n, invoke)
                false
            else
                this.RunGeneral n
                false
        else
            this.RunGeneral n
            false

    static member private RunOptionFnNode(n: MemoNode, child: Node) : bool =
        let m = Platform.fastCast<Memo<'T>> n.OwnerMemo
        m.RunFromNodeWith (n, child, fun prev -> (Platform.fastCast<'T voption -> 'T> m.ComputeObj) (ValueSome prev))

    static member private RunUnitFnNode(n: MemoNode, child: Node) : bool =
        let m = Platform.fastCast<Memo<'T>> n.OwnerMemo
        m.RunFromNodeWith (n, child, fun _ -> (Platform.fastCast<unit -> 'T> m.ComputeObj) ())

    static member private RunSignalRowNode(n: MemoNode, child: Node) : bool =
        let m = Platform.fastCast<Memo<'T>> n.OwnerMemo
        m.RunFromNodeWith (n, child, fun _ -> (Platform.fastCast<Signal<'T>> m.ComputeObj).Value)

    static member private RunOptionFuncNode(n: MemoNode, child: Node) : bool =
        let m = Platform.fastCast<Memo<'T>> n.OwnerMemo
        m.RunFromNodeWith (n, child, fun prev -> (Platform.fastCast<Func<'T voption, 'T>> m.ComputeObj).Invoke(ValueSome prev))

    static member private RunSeededFuncNode(n: MemoNode, child: Node) : bool =
        let m = Platform.fastCast<Memo<'T>> n.OwnerMemo
        m.RunFromNodeWith (n, child, fun prev -> (Platform.fastCast<Func<'T, 'T>> m.ComputeObj).Invoke prev)

    static member val private OptionFnCb: Func<MemoNode, Node, bool> = Func<MemoNode, Node, bool>(fun n child -> Memo<'T>.RunOptionFnNode(n, child))
    static member val private UnitFnCb: Func<MemoNode, Node, bool> = Func<MemoNode, Node, bool>(fun n child -> Memo<'T>.RunUnitFnNode(n, child))
    static member val private SignalRowCb: Func<MemoNode, Node, bool> = Func<MemoNode, Node, bool>(fun n child -> Memo<'T>.RunSignalRowNode(n, child))

    static member val private OptionFuncCb: Func<MemoNode, Node, bool> =
        Func<MemoNode, Node, bool>(fun n child -> Memo<'T>.RunOptionFuncNode(n, child))

    static member val private SeededFuncCb: Func<MemoNode, Node, bool> =
        Func<MemoNode, Node, bool>(fun n child -> Memo<'T>.RunSeededFuncNode(n, child))

    static member private RunMemoOnNode(n: MemoNode, _child: Node) : bool =
        (Platform.fastCast<Memo<'T>> n.OwnerMemo).RunMemoOn n
        false

    static member val private MemoOnCb: Func<MemoNode, Node, bool> = Func<MemoNode, Node, bool>(fun n child -> Memo<'T>.RunMemoOnNode(n, child))

    static member private RunFusedMapNode(n: MemoNode, _child: Node) : bool =
        (Platform.fastCast<Memo<'T>> n.OwnerMemo).RunFusedMap n
        false

    static member val private FusedMapCb: Func<MemoNode, Node, bool> = Func<MemoNode, Node, bool>(fun n child -> Memo<'T>.RunFusedMapNode(n, child))

    static member private MakeNode(graph: Graph, mode: ScopeMode, equal: IEqualityComparer<'T>, cb: Func<MemoNode, Node, bool>) =
        MemoNode (graph, mode, SafeEquality.isSafe equal, cb)
#else
#if FABLE_COMPILER && !RANVIER_TRACE
    static member private MakeNode(graph: Graph, mode: ScopeMode, equal: IEqualityComparer<'T>) =
        MemoNode (graph, mode, SafeEquality.isSafe equal)
#else
#if FABLE_COMPILER
    static member val internal RunFn: MemoNode -> unit = fun (n: MemoNode) -> (Platform.fastCast<Memo<'T>> n.OwnerMemo).RunGeneral n

    static member val private MemoOnRunFn: MemoNode -> unit = fun (n: MemoNode) -> (Platform.fastCast<Memo<'T>> n.OwnerMemo).RunMemoOn n

    static member private MakeNode(graph: Graph, mode: ScopeMode) =
        MemoNode (graph, mode, Memo<'T>.RunFn)
#else
    static member private RunNode(n: MemoNode) =
        (Platform.fastCast<Memo<'T>> n.OwnerMemo).RunGeneral n

    static member private RunMemoOnTraced(n: MemoNode) =
        (Platform.fastCast<Memo<'T>> n.OwnerMemo).RunMemoOn n

    static member val internal RunFn: Action<MemoNode> = Action<MemoNode>(Memo<'T>.RunNode)

    static member val private MemoOnRunFn: Action<MemoNode> = Action<MemoNode>(Memo<'T>.RunMemoOnTraced)

    static member private MakeNode(graph: Graph, mode: ScopeMode) =
        MemoNode (graph, mode, Memo<'T>.RunFn)
#endif
#endif
#endif

    /// <summary>A memo over <c>compute</c>, owned by the current owner.</summary>
    static member internal Create(graph: Graph, compute: 'T voption -> 'T, mode: ScopeMode) =
        let eq = graph.EncodedComparer<'T>()
#if !FABLE_COMPILER && !RANVIER_TRACE
        let n = Memo<'T>.MakeNode(graph, mode, eq, Memo<'T>.OptionFnCb)
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        let n = Memo<'T>.MakeNode(graph, mode, eq)
#else
        let n = Memo<'T>.MakeNode(graph, mode)
#endif
#endif
        let memo = Memo<'T>(box compute, 0uy, Unchecked.defaultof<'T>, eq, n)
        memo.Attach ()
        memo

    /// <summary>A memo over a parameterless <c>compute</c> (no <c>ValueSome</c> construction), owned by the current owner.</summary>
    static member internal CreateUnit(graph: Graph, compute: unit -> 'T, mode: ScopeMode) =
        let eq = graph.EncodedComparer<'T>()
#if !FABLE_COMPILER && !RANVIER_TRACE
        let n = Memo<'T>.MakeNode(graph, mode, eq, Memo<'T>.UnitFnCb)
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        let n = Memo<'T>.MakeNode(graph, mode, eq)
#else
        let n = Memo<'T>.MakeNode(graph, mode)
#endif
#endif
        let memo = Memo<'T>(box compute, 1uy, Unchecked.defaultof<'T>, eq, n)
        memo.Attach ()
        memo

    /// <summary>A value row that tracks a single item signal.</summary>
    static member internal CreateSignalRow(graph: Graph, signal: Signal<'T>) : Memo<'T> =
        Memo<'T>.CreateSignalRowEncoded(graph, signal, graph.EncodedComparer<'T>())

    /// <summary>A value row that tracks a single item signal, with the cutoff of the encoded comparer <c>encodedEqual</c>.</summary>
    static member internal CreateSignalRowEncoded(graph: Graph, signal: Signal<'T>, encodedEqual: IEqualityComparer<'T>) : Memo<'T> =
#if !FABLE_COMPILER && !RANVIER_TRACE
        let n =
            Memo<'T>.MakeNode(graph, ScopeMode.ValueRow, encodedEqual, Memo<'T>.SignalRowCb)

        let memo = Memo<'T>(box signal, 2uy, Unchecked.defaultof<'T>, encodedEqual, n)

        memo.Attach ()
        memo
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        let n = Memo<'T>.MakeNode(graph, ScopeMode.ValueRow, encodedEqual)

        let memo = Memo<'T>(box signal, 2uy, Unchecked.defaultof<'T>, encodedEqual, n)

        memo.Attach ()
        memo
#else
        Memo<'T>.CreateEncoded(graph, (fun _ -> signal.Value), ScopeMode.ValueRow, encodedEqual)
#endif
#endif

    /// <summary>A memo whose cutoff uses <c>comparer</c>, owned by the current owner.</summary>
    static member internal CreateWithComparer(graph: Graph, compute: 'T voption -> 'T, mode: ScopeMode, comparer: IEqualityComparer<'T>) =
        let eq = ComparerSentinel.encode comparer
#if !FABLE_COMPILER && !RANVIER_TRACE
        let n = Memo<'T>.MakeNode(graph, mode, eq, Memo<'T>.OptionFnCb)
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        let n = Memo<'T>.MakeNode(graph, mode, eq)
#else
        let n = Memo<'T>.MakeNode(graph, mode)
#endif
#endif
        let memo = Memo<'T>(box compute, 0uy, Unchecked.defaultof<'T>, eq, n)
        memo.Attach ()
        memo

    static member internal CreateEncoded(graph: Graph, compute: 'T voption -> 'T, mode: ScopeMode, encodedEqual: IEqualityComparer<'T>) =
#if !FABLE_COMPILER && !RANVIER_TRACE
        let n = Memo<'T>.MakeNode(graph, mode, encodedEqual, Memo<'T>.OptionFnCb)
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        let n = Memo<'T>.MakeNode(graph, mode, encodedEqual)
#else
        let n = Memo<'T>.MakeNode(graph, mode)
#endif
#endif
        let memo = Memo<'T>(box compute, 0uy, Unchecked.defaultof<'T>, encodedEqual, n)

        memo.Attach ()
        memo

    /// <summary>A two-phase split memo over pure <c>read</c> and untracked <c>compute</c>, owned by the current owner.</summary>
    static member internal CreateOn
        (
            graph: Graph,
            read: unit -> 'K,
            compute: 'K -> 'T,
            mode: ScopeMode,
            comparer: IEqualityComparer<'T> voption,
            ?keyComparer: IEqualityComparer<'K>
        ) : Memo<'T> =
        if Platform.isNull (box read) then
            nullArg (nameof read)

        if Platform.isNull (box compute) then
            nullArg (nameof compute)

        let eq =
            match comparer with
            | ValueSome c ->
                if Platform.isNull c then
                    nullArg (nameof comparer)

                ComparerSentinel.encode c
            | ValueNone -> graph.EncodedComparer<'T>()

        let keyEq =
            match keyComparer with
            | Some c ->
                if Platform.isNull c then
                    nullArg (nameof keyComparer)

                ComparerSentinel.encode c
            | None -> graph.EncodedComparer<'K>()

        let state = MemoOnState<'K, 'T>(read, compute, mode, keyEq)
#if !FABLE_COMPILER && !RANVIER_TRACE
        let n = MemoNode (graph, ScopeMode.Pure, false, Memo<'T>.MemoOnCb)
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        let n = MemoNode (graph, ScopeMode.Pure, false)
#else
        let n = MemoNode (graph, ScopeMode.Pure, Memo<'T>.MemoOnRunFn)
#endif
#endif
        state.Bind n
        let memo = Memo<'T>(box state, 5uy, Unchecked.defaultof<'T>, eq, n)
        memo.Attach ()
        memo

    static member internal CreateCombinator(graph: Graph, runner: obj, kind: byte, eq: IEqualityComparer<'T>) : Memo<'T> =
#if !FABLE_COMPILER && !RANVIER_TRACE
        let cb =
            if kind >= 6uy then
                Memo<'T>.FusedMapCb
            else
                Memo<'T>.MemoOnCb

        let n = MemoNode (graph, ScopeMode.Pure, false, cb)
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        let n = MemoNode (graph, ScopeMode.Pure, false)
#else
        let n = MemoNode (graph, ScopeMode.Pure, Memo<'T>.MemoOnRunFn)
#endif
#endif
#if !RANVIER_TRACE
        if kind >= 6uy then
            n.Flags <- n.Flags ||| NodeFlags.FusedMap
            (Platform.fastCast<FusedRunner<'T>> runner).BindNode n
#endif
        let memo = Memo<'T>(runner, kind, Unchecked.defaultof<'T>, eq, n)
        memo.Attach ()
        memo

#if !RANVIER_TRACE
    member internal _.RebindFused(newRunner: obj, newKind: byte) =
        computeObj <- newRunner
        computeKind <- newKind

        if newKind >= 6uy then
            node.Flags <-
                (node.Flags &&& ~~~NodeFlags.ClaimedFused)
                ||| NodeFlags.FusedMap

            (Platform.fastCast<FusedRunner<'T>> newRunner).BindNode node
        else
            node.Flags <-
                node.Flags
                &&& ~~~(NodeFlags.ClaimedFused ||| NodeFlags.FusedMap)

    member inline internal _.SetMaterializedValue(v: 'T) =
        value <- v

    member internal this.PublishDeFusedMove(moved: bool) =
        this.FinishPublish (node, node.Graph, moved)

    member inline internal _.IsClaimedFused = (node.Flags &&& NodeFlags.ClaimedFused) <> 0

    member inline internal this.EnsureMaterializedIfClaimed() =
        if (node.Flags &&& NodeFlags.ClaimedFused) <> 0 then
            this.EnsureMaterializedSlow ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member internal this.EnsureMaterializedSlow() =
        node.Graph.Entered (
            "Reading a node",
            fun () ->
                if this.IsClaimedFused then
                    let mutable cur = this :> IFusedChainNode

                    while not (Platform.isNull cur.FusedNext) do
                        cur <- cur.FusedNext

                    cur.MaterializeFromTail ()
        )

    interface IFusedChainNode with
        member _.FusedNext =
            if computeKind >= 6uy then
                (Platform.fastCast<FusedRunner<'T>> computeObj).FusedNext
            else
                Platform.nil ()

        member this.MaterializeFromTail() =
            if computeKind >= 6uy then
                let runner = Platform.fastCast<FusedRunner<'T>> computeObj
                runner.MaterializeTail (box this)
#endif

    /// <summary>Attaches the memo to the current owner. Disposing that owner disposes the memo.</summary>
    member internal this.Attach() =
        node.OwnerMemo <- box this
        let g = node.Graph
        let owner = g.CurrentOwner
        Tracer.MemoNew (g, node.NodeId, owner)
        owner.AttachLink node

    /// <summary>The memo's reactive state.</summary>
    member inline internal _.Node = node

    member internal _.HasDirectSignal = computeKind = 2uy

    /// <summary>Invokes the body with the argument its delegate takes (general/first-run path).</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private _.Invoke(previous: 'T) : 'T =
#if FABLE_COMPILER
        if computeKind = 0uy then
            (Platform.fastCast<'T voption -> 'T> computeObj) (if node.Published then ValueSome previous else ValueNone)
        elif computeKind = 1uy then
            (Platform.fastCast<unit -> 'T> computeObj) ()
        else
            (Platform.fastCast<Signal<'T>> computeObj).Value
#else
        if computeKind = 0uy then
            (Platform.fastCast<'T voption -> 'T> computeObj) (if node.Published then ValueSome previous else ValueNone)
        elif computeKind = 1uy then
            (Platform.fastCast<unit -> 'T> computeObj) ()
        elif computeKind = 2uy then
            (Platform.fastCast<Signal<'T>> computeObj).Value
        elif computeKind = 3uy then
            (Platform.fastCast<Func<'T voption, 'T>> computeObj).Invoke(if node.Published then ValueSome previous else ValueNone)
        else
            (Platform.fastCast<Func<'T, 'T>> computeObj).Invoke(if node.Published then previous else seed)
#endif

#if !RANVIER_TRACE
    member private _.NoteStale(n: MemoNode) =
        n.Flags <- n.Flags ||| NodeFlags.Unannounced
        n.Graph.NoteStaleTransition ()
#endif

#if FABLE_COMPILER && !RANVIER_TRACE
    // Internal under Fable, where `MemoNode.RunDirect` calls it directly; full signature allows calling on `Memo<obj>`.
    member internal this.RunFromNode(n: MemoNode) : unit =
        if (n.Flags &&& NodeFlags.FastRunMemoMask) = NodeFlags.FastRunMemoValue then
            let g = n.Graph

            if g.CanEnterHostedTop () then
                if g.OwnerPending then
                    this.RunTopPending n
                else
                    this.RunTopCore (n, false)
            elif g.CanNestInReader then
                this.RunReadyNested n
            else
                this.RunGeneral n
        elif computeKind >= 6uy then
            this.RunFusedMap n
        elif computeKind = 5uy then
            this.RunMemoOn n
        else
            this.RunGeneral n

    member private this.RunTopCore(n: MemoNode, previousPending: bool) =
        let g = n.Graph
        let prevOwner = g.EnterHostedTop n
        // The run records the exceptions of its body. An exception raised by the JavaScript engine itself, a stack
        // overflow, restores the reader and owner the run replaced and continues to the caller.
        try
            this.RunTopHosted (n, prevOwner, previousPending)
        with _ ->
            g.LeaveHostedTopFailed (prevOwner, previousPending)
            n.Flags <- n.Flags &&& ~~~NodeFlags.ActiveMask
            reraise ()

    member private this.RunTopHosted(n: MemoNode, prevOwner: Owner, previousPending: bool) =
        let g = n.Graph
        let previous = value
        let result = this.ComputePublished (n, previous)

        if
            not (Platform.isNull g.Caught)
            || g.RaisedPending
            || (n.Flags &&& NodeFlags.Violated) <> 0
        then
            this.RunFailedTop (n, prevOwner, previousPending, this.TakeBodyException ())
        else
            value <- result
            g.LeaveHostedTop (prevOwner, previousPending)
            n.Flags <- n.Flags &&& ~~~NodeFlags.ActiveMask

            let trim =
                (n.Flags &&& NodeFlags.Disposed) <> 0
                || n.SourcesField.cursor < n.SourcesField.count

            if trim then
                this.FinishTop (n, prevOwner, previousPending, previous, true)
            elif
                not (Platform.isNull n.Observers)
                && n.Observers.Count <> 0
            then
                if not (obj.ReferenceEquals (box previous, box value)) then
                    if NodeOps.TryPromoteTop n then
                        if g.QueuePending then
                            g.OweFlush ()

                        if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
                            this.NoteStale n
                    else
                        this.FinishTop (n, prevOwner, previousPending, previous, false)
                elif (n.Flags &&& NodeFlags.FreshMask) <> 0 then
                    this.NoteStale n
            else
                // Theorem B9: leaf memo skips equality check unless QueuePending needs OweFlush.
                if
                    g.QueuePending
                    && not (obj.ReferenceEquals (box previous, box value))
                then
                    g.OweFlush ()

                if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
                    this.NoteStale n

    member private this.RunTopPending(n: MemoNode) =
        this.RunTopCore (n, true)

    member private this.RunReadyNested(n: MemoNode) =
        let g = n.Graph
        let parent = g.CurrentNode
        let previousOwner = g.EnterHostedTop n
        let previous = value
        let result = this.ComputePublished (n, previous)

        let failed =
            not (Platform.isNull g.Caught)
            || g.RaisedPending
            || (n.Flags &&& NodeFlags.Violated) <> 0

        if not failed then
            value <- result
            g.LeaveNestedReader (parent, previousOwner)
            n.Flags <- n.Flags &&& ~~~NodeFlags.ActiveMask

            if
                (n.Flags &&& NodeFlags.Disposed) <> 0
                || n.SourcesField.cursor < n.SourcesField.count
            then
                this.FinishSources ()
        else
            this.RunFailedNested (n, parent, previousOwner, this.TakeBodyException ())

        if
            failed
            || (((not (Platform.isNull n.Observers)
                  && n.Observers.Count <> 0)
                 || g.QueuePending)
                && not (obj.ReferenceEquals (box previous, box value)))
        then
            if
                not (Platform.isNull n.Observers)
                && n.Observers.Count <> 0
                && (n.Observers.Count <> 1
                    || not (obj.ReferenceEquals (n.Observers.FirstRef, box parent))
                    || g.StaleTransitions <> 0)
            then
                NodeOps.NotifyMoved n

            if g.QueuePending then
                g.OweFlush ()

        if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
            this.NoteStale n

    member inline private this.ComputePublished(n: MemoNode, previous: 'T) =
        n.Flags <-
            (n.Flags
             &&& ~~~(NodeFlags.FreshMask ||| NodeFlags.Unannounced))
            ||| NodeFlags.ActiveBody

        n.SourcesField.BeginRun ()
        n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
        Counters.MemoRecomputed ()
#endif

        if computeKind = 2uy then
            (Platform.fastCast<Signal<'T>> computeObj).Value
        else
            this.InvokePublished previous

    member private _.InvokePublished(previous: 'T) =
        try
            if computeKind = 0uy then
                (Platform.fastCast<'T voption -> 'T> computeObj) (ValueSome previous)
            else
                (Platform.fastCast<unit -> 'T> computeObj) ()
        with ex ->
            node.Graph.Caught <- Thrown.hold ex
            Unchecked.defaultof<'T>
#endif

#if !RANVIER_TRACE
    member private this.TakeBodyException() =
        let g = node.Graph
        let ex = g.Caught

        if Platform.isNull ex then
            try
                this.FailAfterBody ()
                Platform.nil ()
            with ex ->
                ex
        else
            g.Caught <- Platform.nil ()
#if FABLE_COMPILER
            Thrown.take ex
#else
            ex
#endif

    member private this.FinishTop(n: MemoNode, prevOwner: Owner, previousPending: bool, previous: 'T, trim: bool) =
        try
            if trim then
                this.FinishSources ()

            if
                not trim
                || ((n.ObserverCount <> 0 || n.Graph.QueuePending)
#if FABLE_COMPILER
                    && not (obj.ReferenceEquals (box previous, box value)))
#else
                    && not (ComparerSentinel.equals null previous value))
#endif
            then
                if n.ObserverCount <> 0 then
                    NodeOps.NotifyMovedTop n

                if n.Graph.QueuePending then
                    n.Graph.OweFlush ()

            if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
                this.NoteStale n
        with ex ->
            this.RunFailedTop (n, prevOwner, previousPending, ex)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RunFailedTop(n: MemoNode, prevOwner: Owner, previousPending: bool, ex: exn) =
        let g = n.Graph
        g.LeaveHostedTopFailed (prevOwner, previousPending)
        n.Flags <- n.Flags &&& ~~~NodeFlags.ActiveMask

        if n.IsDisposed then
            n.SourcesField.Clear (n :> IComputation)
        else
            n.SourcesField.EndRun (n :> IComputation)

        if (n.Flags &&& NodeFlags.Violated) <> 0 then
            n.Flags <- n.Flags &&& ~~~NodeFlags.Violated
            n.FailureField <- g.FailureOf (ScopeMessages.failure n.Mode ex, this, null)
            n.SetStatusError ()
        else
            match ex with
            | NotReadyException source ->
                n.PendingSourceField <- source
                n.SetStatusPending ()
            | _ ->
                n.FailureField <- g.FailureOf (ex, this, null)
                n.SetStatusError ()

        NodeOps.NotifyMoved n

        if g.QueuePending then
            g.OweFlush ()

        if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
            this.NoteStale n
#endif

    member private this.RunGeneral(n: MemoNode) =
        let g = n.Graph
        // Marked clean *before* the body runs, not after.
#if !RANVIER_TRACE
        n.Flags <-
            n.Flags
            &&& ~~~(NodeFlags.FreshMask ||| NodeFlags.Unannounced)
#else
#if FABLE_COMPILER
        n.Freshness <- Freshness.Clean
        n.Unannounced <- false
#else
        n.FreshnessField <- Freshness.Clean
        n.UnannouncedField <- false
#endif
#endif
        n.SourcesField.BeginRun ()

        if not (Platform.isNull (box n.PendingSourceField)) then
            n.PendingSourceField <- Platform.nil ()

        let previous = value
        let previousStatus = n.Status
        let previousFailure = n.FailureField

        n.SetStatusNone ()
        n.FailureField <- Platform.nil ()
        n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
        Counters.MemoRecomputed ()
#endif
        Tracer.RunStart (g, n.NodeId, n.RunsField)
#if !RANVIER_TRACE
        n.Flags <-
            (n.Flags &&& ~~~NodeFlags.ActiveMask)
            ||| NodeFlags.ActiveBody
#else
#if FABLE_COMPILER
        n.Active <- 1uy
#else
        n.ActiveField <- 1uy
#endif
#endif

#if FABLE_COMPILER
        try
            try
#if !RANVIER_TRACE
                let result =
                    if computeKind = 0uy then
                        let prev =
                            if (n.Flags &&& NodeFlags.Published) <> 0 then
                                ValueSome previous
                            else
                                ValueNone

                        g.RunHostedNode (n, Platform.fastCast<'T voption -> 'T> computeObj, prev)
                    elif computeKind = 1uy then
                        g.RunHostedNode (n, Platform.fastCast<unit -> 'T> computeObj, ())
                    else
                        g.RunHostedNode (n, Memo<'T>.ReadSignalFn, Platform.fastCast<Signal<'T>> computeObj)

                if (n.Flags &&& NodeFlags.Violated) <> 0 then
                    raise (InvalidOperationException (ScopeMessages.forMode n.Mode))

                value <- result
                n.Flags <- n.Flags ||| NodeFlags.Published
#else
                let prev = if n.Published then ValueSome previous else ValueNone

                let result =
                    if computeKind = 0uy then
                        g.RunHosted (n :> IComputation, Platform.fastCast<'T voption -> 'T> computeObj, prev)
                    elif computeKind = 1uy then
                        g.RunHosted (n :> IComputation, Platform.fastCast<unit -> 'T> computeObj, ())
                    else
                        g.RunHosted (n :> IComputation, fun () -> (Platform.fastCast<Signal<'T>> computeObj).Value)

                if n.Violated then
                    raise (InvalidOperationException (ScopeMessages.forMode n.Mode))

                value <- result
                n.Published <- true
#endif
            finally
#if !RANVIER_TRACE
                n.Flags <- n.Flags &&& ~~~NodeFlags.ActiveMask
#else
                n.Active <- 0uy
#endif

                if n.IsDisposed then
                    n.SourcesField.Clear (n :> IComputation)
                else
                    n.SourcesField.EndRun (n :> IComputation)
        with
#if !RANVIER_TRACE
        | ex when (n.Flags &&& NodeFlags.Violated) <> 0 ->
            n.Flags <- n.Flags &&& ~~~NodeFlags.Violated
            n.FailureField <- g.FailureOf (ScopeMessages.failure n.Mode ex, this, previousFailure)
            n.SetStatusError ()
#else
        | ex when n.Violated ->
            n.Violated <- false
            n.FailureField <- g.FailureOf (ScopeMessages.failure n.Mode ex, this, previousFailure)
            n.SetStatusError ()
#endif
        | NotReadyException source ->
            n.PendingSourceField <- source
            n.SetStatusPending ()
        | ex ->
            n.FailureField <- g.FailureOf (ex, this, previousFailure)
            n.SetStatusError ()
#else
        let saved = g.EnterHostedNode n
        let ex = this.TryCompute previous

        if not (Platform.isNull ex) then
            this.RunFailed (saved, ex, previousFailure)
        else
#if !RANVIER_TRACE
            n.Flags <-
                (n.Flags &&& ~~~NodeFlags.ActiveMask)
                ||| NodeFlags.Published
#else
            n.PublishedField <- true
            n.ActiveField <- 0uy
#endif
            g.LeaveHosted saved

            if
                n.IsDisposed
                || n.SourcesField.Cursor < n.SourcesField.Count
            then
                this.FinishSources ()
#endif

        this.Publish (previous, previousStatus, previousFailure)

#if FABLE_COMPILER && !RANVIER_TRACE
    static member val private ReadSignalFn: Signal<'T> -> 'T = fun s -> s.Value

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RunFailedNested(n: MemoNode, parent: Node, previousOwner: Owner, ex: exn) =
        let g = n.Graph
        g.LeaveNestedReaderFailed (parent, previousOwner)
        n.Flags <- n.Flags &&& ~~~NodeFlags.ActiveMask

        if n.IsDisposed then
            n.SourcesField.Clear (n :> IComputation)
        else
            n.SourcesField.EndRun (n :> IComputation)

        if (n.Flags &&& NodeFlags.Violated) <> 0 then
            n.Flags <- n.Flags &&& ~~~NodeFlags.Violated
            n.FailureField <- g.FailureOf (ScopeMessages.failure n.Mode ex, this, null)
            n.SetStatusError ()
        else
            match ex with
            | NotReadyException source ->
                n.PendingSourceField <- source
                n.SetStatusPending ()
            | _ ->
                n.FailureField <- g.FailureOf (ex, this, null)
                n.SetStatusError ()

    member private this.RefreshTopGuarded(n: MemoNode) =
        try
            if (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshDirty then
                this.RunTopCore (n, false)
            else
                NodeOps.Refresh n
        with _ ->
            n.Graph.ExitPullFastOnException ()
            reraise ()
#endif

#if !FABLE_COMPILER
    member private this.TryCompute(previous: 'T) : exn =
        try
            let result = this.Invoke previous

            if
                node.Graph.RaisedPending
#if !RANVIER_TRACE
                || (node.Flags &&& NodeFlags.Violated) <> 0
#else
                || node.ViolatedField
#endif
            then
                this.FailAfterBody ()

            value <- result
            null
        with ex ->
            ex
#endif

#if !FABLE_COMPILER || !RANVIER_TRACE
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.FailAfterBody() =
        node.Graph.CheckRaised ()

        if
#if !RANVIER_TRACE
            (node.Flags &&& NodeFlags.Violated) <> 0
#else
#if FABLE_COMPILER
#else
            node.ViolatedField
#endif
#endif
        then
            raise (InvalidOperationException (ScopeMessages.forMode node.Mode))

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.FinishSources() =
        if node.IsDisposed then
            node.SourcesField.Clear (node :> IComputation)
        else
            node.SourcesField.EndRun (node :> IComputation)
#endif

#if !FABLE_COMPILER
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RunFailed(saved: HostedFrame, ex: exn, previousFailure: Failure) =
        let n = node
        let g = n.Graph
        g.LeaveHosted saved
#if !RANVIER_TRACE
        n.Flags <- n.Flags &&& ~~~NodeFlags.ActiveMask
#else
        n.ActiveField <- 0uy
#endif

        if n.IsDisposed then
            n.SourcesField.Clear (n :> IComputation)
        else
            n.SourcesField.EndRun (n :> IComputation)

        if
#if !RANVIER_TRACE
            (n.Flags &&& NodeFlags.Violated) <> 0
#else
            n.ViolatedField
#endif
        then
#if !RANVIER_TRACE
            n.Flags <- n.Flags &&& ~~~NodeFlags.Violated
#else
            n.ViolatedField <- false
#endif
            n.FailureField <- g.FailureOf (ScopeMessages.failure n.Mode ex, this, previousFailure)
            n.SetStatusError ()
        else
            match ex with
            | NotReadyException source ->
                n.PendingSourceField <- source
                n.SetStatusPending ()
            | _ ->
                n.FailureField <- g.FailureOf (ex, this, previousFailure)
                n.SetStatusError ()
#endif

    member internal this.RunMemoOn(n: MemoNode) =
        let g = n.Graph
#if !RANVIER_TRACE
        n.Flags <-
            n.Flags
            &&& ~~~(NodeFlags.FreshMask ||| NodeFlags.Unannounced)
#else
#if FABLE_COMPILER
        n.Freshness <- Freshness.Clean
        n.Unannounced <- false
#else
        n.FreshnessField <- Freshness.Clean
        n.UnannouncedField <- false
#endif
#endif

        let previousPending = n.PendingSourceField

        if not (Platform.isNull (box previousPending)) then
            n.PendingSourceField <- Platform.nil ()

        let previous = value
        let previousStatus = n.Status
        let previousFailure = n.FailureField

        n.SetStatusNone ()
        n.FailureField <- Platform.nil ()
        n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
        Counters.MemoRecomputed ()
#endif
        Tracer.RunStart (g, n.NodeId, n.RunsField)
#if !RANVIER_TRACE
        n.Flags <-
            (n.Flags &&& ~~~NodeFlags.ActiveMask)
            ||| NodeFlags.ActiveBody
#else
#if FABLE_COMPILER
        n.Active <- 1uy
#else
        n.ActiveField <- 1uy
#endif
#endif
        let runner = Platform.fastCast<MemoOnRunner<'T>> computeObj
        value <- runner.Execute (this :> INode, n, equal, previous, previousStatus, previousFailure, previousPending)
        this.FinishPublish (n, g, runner.Moved)

#if !RANVIER_TRACE
    member internal this.RunFusedMap(n: MemoNode) =
        let g = n.Graph

        n.Flags <-
            (n.Flags
             &&& ~~~(NodeFlags.FreshMask
                     ||| NodeFlags.Unannounced
                     ||| NodeFlags.ActiveMask))
            ||| NodeFlags.ActiveBody

        let previous = value
        let previousStatus = n.Status
        let previousFailure = n.FailureField
        let runner = Platform.fastCast<FusedRunner<'T>> computeObj
        value <- runner.ExecuteFused (box this, n, equal, previous, previousStatus, previousFailure)
        this.FinishPublish (n, g, runner.Moved)
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.Publish(previous: 'T, previousStatus: Status, previousFailure: Failure) =
        let n = node
        let g = n.Graph
        let curStatus = n.Status

        let mutable moved =
            curStatus <> previousStatus
            || (not (obj.ReferenceEquals (n.FailureField, previousFailure))
                && Failure.Moved (n.FailureField, previousFailure))

        if not moved then
            moved <-
                if SafeEquality.isSafe equal then
#if !RANVIER_TRACE
                    (n.ObserverCount <> 0 || g.QueuePending)
                    && not (ComparerSentinel.equals equal previous value)
#else
                    not (ComparerSentinel.equals equal previous value)
#endif
                else
                    this.CompareGuarded (previous, previousFailure)

        this.FinishPublish (n, g, moved)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private _.FinishPublish(n: MemoNode, g: Graph, moved: bool) =
        if moved then
            Tracer.Moved (g, n.NodeId, Failure.Payload (n.FailureField, box value))
            NodeOps.NotifyMoved n
            Tracer.Notified g
            Tracer.RunEnd (g, n.NodeId, n.Status)

            if g.QueuePending then
                g.OweFlush ()
        else
            Tracer.RunEnd (g, n.NodeId, n.Status)

        if
#if !RANVIER_TRACE
            (n.Flags &&& NodeFlags.FreshMask) <> 0
#else
#if FABLE_COMPILER
            n.Freshness <> Freshness.Clean
#else
            n.FreshnessField <> Freshness.Clean
#endif
#endif
        then
#if !RANVIER_TRACE
            n.Flags <- n.Flags ||| NodeFlags.Unannounced
#else
#if FABLE_COMPILER
            n.Unannounced <- true
#else
            n.UnannouncedField <- true
#endif
#endif
            g.NoteStaleTransition ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CompareGuarded(previous: 'T, previousFailure: Failure) : bool =
        try
            not (ComparerSentinel.equals equal previous value)
        with ex ->
            value <- previous
            node.FailureField <- node.Graph.FailureOf (ex, this, previousFailure)
            node.SetStatusError ()
            true

    member internal this.EnsureCurrent() =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        NodeOps.EnsureCurrent node

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.SettlePullAndRead() : 'T =
        let g = node.Graph
        let currentStatus = node.Status
        let currentFailure = node.FailureField
        let currentValue = value
        g.FinishPullSettle ()
        g.Track (node :> ISource)

        if
#if !RANVIER_TRACE
            (node.Flags &&& NodeFlags.FreshMask) <> 0
            && (node.Flags &&& NodeFlags.Disposed) = 0
#else
#if FABLE_COMPILER
            node.Freshness <> Freshness.Clean
            && not node.Disposed
#else
            node.FreshnessField <> Freshness.Clean
            && not node.DisposedField
#endif
#endif
        then
            g.MarkReaderCheck ()

        if StatusBits.isUnready currentStatus then
            if StatusBits.isPending currentStatus then
                raise (g.NotReady (this :> INode))

            g.Raise currentFailure

        currentValue

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.SettlePullAndReadTry() : Reading<'T> =
        let g = node.Graph
        let currentStatus = node.Status
        let currentFailure = node.FailureField
        let currentValue = value
        g.FinishPullSettle ()
        g.Track (node :> ISource)

        if
#if !RANVIER_TRACE
            (node.Flags &&& NodeFlags.FreshMask) <> 0
            && (node.Flags &&& NodeFlags.Disposed) = 0
#else
#if FABLE_COMPILER
            node.Freshness <> Freshness.Clean
            && not node.Disposed
#else
            node.FreshnessField <> Freshness.Clean
            && not node.DisposedField
#endif
#endif
        then
            g.MarkReaderCheck ()

        if StatusBits.isUnready currentStatus then
            if StatusBits.isPending currentStatus then
                Pending
            else
                Failed currentFailure.Error
        else
            Ready currentValue

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.SettlePullAndReadUntracked() : Reading<'T> =
        let currentStatus = node.Status
        let currentFailure = node.FailureField
        let currentValue = value
        node.Graph.FinishPullSettle ()

        if StatusBits.isUnready currentStatus then
            if StatusBits.isPending currentStatus then
                Pending
            else
                Failed currentFailure.Error
        else
            Ready currentValue

    /// <summary>Detaches from every source. Idempotent, and usually called for you by the enclosing owner.</summary>
    member this.Dispose() =
#if !RANVIER_TRACE
        if not node.IsDisposed && computeKind >= 6uy then
            node.Graph.Entered (
                "Disposing a node",
                fun () ->
                    if not node.IsDisposed && computeKind >= 6uy then
                        let runner = Platform.fastCast<FusedRunner<'T>> computeObj

                        if runner.IsClaimed then
                            this.EnsureMaterializedSlow ()
                        else
                            runner.MaterializeTail (box this)
            )
#endif
        node.DisposeMemoFromUser ()

    /// <summary>Marks every reader dirty. A reader of a row whose key was removed re-runs and reads the key as absent.</summary>
    member internal _.NotifyRemoved() =
#if FABLE_COMPILER && !RANVIER_TRACE
        if not (Platform.isNull node.Observers) then
            node.Observers.NotifyDirty ()
#else
        node.Observers.NotifyDirty ()
#endif

    member internal _.Violated =
#if !RANVIER_TRACE
        (node.Flags &&& NodeFlags.Violated) <> 0
#else
#if FABLE_COMPILER
        node.Violated
#else
        node.ViolatedField
#endif
#endif

    member internal _.IsUnannouncedForTest =
#if !RANVIER_TRACE
        (node.Flags &&& NodeFlags.Unannounced) <> 0
#else
#if FABLE_COMPILER
        node.Unannounced
#else
        node.UnannouncedField
#endif
#endif

    member internal _.Adopt(companion: IDisposable) =
        if node.IsDisposed then
            companion.Dispose ()
        else
            let replaced = node.Adopted
            node.Adopted <- companion

            if
                not (Platform.isNull replaced)
                && not (obj.ReferenceEquals (replaced, companion))
            then
                replaced.Dispose ()

    interface IOwned with
        member this.Release() =
            this.Dispose ()

    interface IScopeHost with
        member _.CanOwn = node.CanOwnNode
        member _.Scope = node.GetScopeNode ()

    interface IMemoHandle with
        member this.UnwrapMemoNode() =
#if !RANVIER_TRACE
            this.EnsureMaterializedIfClaimed ()
#endif
            node :> ISource

#if !RANVIER_TRACE
        member _.OnOwnerDispose() =
            if computeKind >= 6uy then
                let runner = Platform.fastCast<FusedRunner<'T>> computeObj

                if not runner.IsClaimed then
                    runner.ClearIntermediates ()
#endif

#if RANVIER_TRACE
    interface ITracedNode with
        member _.TraceGraph = box node.Graph
#endif

    interface IReactive<'T> with
        member _.Graph = node.Graph

        member this.ReadTracked() =
            this.Value

    interface INode with
        member _.Id = node.NodeId

        member this.Status =
#if !RANVIER_TRACE
            this.EnsureMaterializedIfClaimed ()
#endif
            node.Status
            ||| Staleness.ofFreshness node.Freshness

    interface ISource with
        member this.AddObserver(c, slot) =
#if !RANVIER_TRACE
            this.EnsureMaterializedIfClaimed ()
#endif
#if FABLE_COMPILER && !RANVIER_TRACE
            (node.EnsureObservers ()).Add(c, slot)
#else
            node.Observers.Add (c, slot)
#endif

        member this.TryAddObserver(c, slot) =
#if !RANVIER_TRACE
            this.EnsureMaterializedIfClaimed ()
#endif
#if FABLE_COMPILER && !RANVIER_TRACE
            (node.EnsureObservers ()).TryAdd(c, slot)
#else
            node.Observers.TryAdd (c, slot)
#endif

        member _.RemoveObserverAt(c, handle) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if not (Platform.isNull node.Observers) then
                node.Observers.RemoveAt (c, handle)
#else
            node.Observers.RemoveAt (c, handle)
#endif

        member this.UpdateIfNecessary() =
#if !RANVIER_TRACE
            this.EnsureMaterializedIfClaimed ()
#endif
            NodeOps.EnsureCurrent node

        member this.IsStale =
#if !RANVIER_TRACE
            this.EnsureMaterializedIfClaimed ()
#endif
            (node :> ISource).IsStale

        member _.IsActive = (node :> ISource).IsActive

    interface IComputation with
        member _.AddSource s =
            NodeOps.AddSource (node, s)

        member _.MovedIn(slot, handle) =
            node.SourcesField.MovedIn (slot &&& EdgeHandle.SlotMask, handle)

        member _.HasReadSlot slot =
            (slot &&& EdgeHandle.SlotMask) < node.SourcesField.Cursor

        member _.MarkDirty() =
            NodeOps.MemoMarkDirty node

        member _.MarkCheck() =
            NodeOps.MemoMarkCheck node

#if FABLE_COMPILER
    member this.Status =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        node.Status
#else
    member this.Status = this.GetStatusFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetStatusFast() =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        node.Status
#endif

    member internal this.PendingSource =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        node.PendingSourceField

    member this.PendingSources =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        if Platform.isNull (box node.PendingSourceField) then
            Seq.empty
        else
            [| node.PendingSourceField |] :> seq<INode>

#if FABLE_COMPILER
    member this.Runs =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        node.RunsField
#else
    member this.Runs = this.GetRunsFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetRunsFast() =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        node.RunsField
#endif

    /// <summary>Transparent, tracked read. Suspends the caller if this memo is itself suspended.</summary>
#if FABLE_COMPILER
    member this.Value: 'T =
#if !RANVIER_TRACE
        if (node.Flags &&& NodeFlags.CleanAndReadyMask) = 0 then
#else
        if StatusBits.isCleanAndReady node.Freshness node.StatusField then
#endif
            node.Graph.Track (node :> ISource)
            value
        else
            this.ReadSlow ()
#else
    member this.Value: 'T = this.GetFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetFast() : 'T =
        let n = node

#if !RANVIER_TRACE
        if (n.Flags &&& NodeFlags.CleanAndReadyMask) = 0 then
#else
        if StatusBits.isCleanAndReady n.FreshnessField n.StatusField then
#endif
            n.Graph.Track (n :> ISource)
            value
        else
            this.ReadSlow ()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ReadSlow() : 'T =
        let n = node
#if !RANVIER_TRACE
        let g = n.Graph

        if
            (n.Flags &&& NodeFlags.FastReadMemoMask) = NodeFlags.FastReadMemoValue
            && Platform.isNull (box n.ScopeField)
        then
            if g.CanPullTopDirect () then
                g.EnterPullDirect ()
#if FABLE_COMPILER
                this.RefreshTopGuarded n
#else
                if (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshDirty then
                    n.RunCallback.Invoke (n, Unchecked.defaultof<Node>)
                    |> ignore
                else
                    this.RefreshIdleCheck n
#endif

                if
                    g.ExitPullFast ()
                    || (n.Flags &&& NodeFlags.FreshMask) <> 0
                then
                    this.SettlePullAndRead ()
                elif (n.Flags &&& NodeFlags.StatusUnreadyMask) <> 0 then
                    this.RaiseUnready n
                else
                    value
            elif
                (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshDirty
                && g.CanNestInReader
                && g.IsHostedUnshadowed ()
            then
#if !FABLE_COMPILER
                n.RunCallback.Invoke (n, Unchecked.defaultof<Node>)
                |> ignore
#else
                this.RunReadyNested n
#endif

                if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
                    this.SettlePullAndRead ()
                else
                    g.Track (n :> ISource)

                    if (n.Flags &&& NodeFlags.StatusUnreadyMask) <> 0 then
                        this.RaiseUnready n
                    else
                        value
            elif
                (n.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshDirty
                && g.CanPullUncached ()
            then
                this.ReadUncachedTop n
            else
                this.ReadSlowGeneral n
        elif (n.Flags &&& NodeFlags.FusedMap) <> 0 then
            this.ReadFusedSlow (n, g)
        else
            this.ReadSlowGeneral n

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ReadFusedSlow(n: MemoNode, g: Graph) : 'T =
        if (n.Flags &&& NodeFlags.ClaimedFused) <> 0 then
            this.EnsureMaterializedSlow ()
            this.ReadSlowGeneral n
        elif
            computeKind = 6uy
            && (n.Flags &&& NodeFlags.FusedLeafDirtyMask) = NodeFlags.FusedLeafDirtyValue
            && n.ObserverCount = 0
            && not g.QueuePending
            && g.CanPullTopDirect ()
        then
            let runner = Platform.fastCast<FusedRunner<'T>> computeObj
            let mutable needsSettle = false

            try
                needsSettle <- runner.EvalSignalLeaf (box this, n, g, SafeEquality.isSafe equal, equal, value)
            with _ ->
                g.CloseLeafPureBody ()
                n.Flags <- n.Flags &&& ~~~NodeFlags.ActiveMask
                g.ExitPullFastOnException ()
                reraise ()

            if needsSettle then
                this.SettlePullAndRead ()
            elif (n.Flags &&& NodeFlags.StatusUnreadyMask) <> 0 then
                this.RaiseUnready n
            else
                value
        else
            this.ReadSlowGeneral n

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ReadUncachedTop(n: MemoNode) =
        let g = n.Graph
        g.EnterPull ()
#if FABLE_COMPILER
        try
            this.RunTopCore (n, g.OwnerPending)
        with _ ->
            g.ExitPull ()
            reraise ()
#else
        n.RunCallback.Invoke (n, Unchecked.defaultof<Node>)
        |> ignore
#endif

        if
            g.ExitPullDeferSettle ()
            || (n.Flags &&& NodeFlags.FreshMask) <> 0
        then
            this.SettlePullAndRead ()
        elif (n.Flags &&& NodeFlags.StatusUnreadyMask) <> 0 then
            this.RaiseUnready n
        else
            value

#if !FABLE_COMPILER
    member private this.RefreshIdleCheck(n: MemoNode) =
        try
            NodeOps.Refresh n
        with _ ->
            n.Graph.ExitPullFastOnException ()
            reraise ()
#endif

    member private this.RaiseUnready(n: MemoNode) : 'T =
        let g = n.Graph

        if (n.Flags &&& NodeFlags.StatusPending) <> 0 then
            raise (g.NotReady (this :> INode))

        g.Raise n.FailureField
        value

    member private this.ReadSlowGeneral(n: MemoNode) : 'T =
#endif
        if NodeOps.RefreshForRead n then
            this.SettlePullAndRead ()
        else
            let g = n.Graph
            g.Track (n :> ISource)
            let st = n.Status

            if StatusBits.isUnready st then
                if StatusBits.isPending st then
                    raise (g.NotReady (this :> INode))

                g.Raise n.FailureField

            value

    /// <summary>Non-throwing, tracked read.</summary>
#if FABLE_COMPILER
    member this.TryValue: Reading<'T> =
#if !RANVIER_TRACE
        if (node.Flags &&& NodeFlags.CleanAndReadyMask) = 0 then
#else
        if StatusBits.isCleanAndReady node.Freshness node.StatusField then
#endif
            node.Graph.Track (node :> ISource)
            Ready value
        else
            this.ReadTrySlow ()

    /// <summary>
    /// Tracks <c>node</c> and returns true only when already clean and ready, without refreshing a stale node.
    /// </summary>
    member inline internal _.TrackIfCleanAndReady() : bool =
#if !RANVIER_TRACE
        if (node.Flags &&& NodeFlags.CleanAndReadyMask) = 0 then
#else
        if StatusBits.isCleanAndReady node.Freshness node.StatusField then
#endif
            node.Graph.Track (node :> ISource)
            true
        else
            false

    /// <summary>
    /// Zero-allocation tracked read readiness probe under Fable: refreshes if stale when the graph is deferring,
    /// tracks the edge, and returns true when ready so the caller can read <c>Peek</c> directly without allocating
    /// <c>Reading&lt;'T&gt;</c>. Returns false without modifying state when stale outside a deferring frame so the
    /// caller's <c>TryValue</c> handles top-level pull settlement with exact pre-flush snapshot semantics.
    /// </summary>
    member internal this.TryTrackReady() : bool =
#if !RANVIER_TRACE
        if (node.Flags &&& NodeFlags.CleanAndReadyMask) = 0 then
#else
        if StatusBits.isCleanAndReady node.Freshness node.StatusField then
#endif
            node.Graph.Track (node :> ISource)
            true
        elif node.Graph.Deferring then
            this.TryTrackReadyDeferring ()
        else
            false

    member private this.TryTrackReadyDeferring() : bool =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        let g = node.Graph

        if NodeOps.RefreshForRead node then
            let currentStatus = node.Status
            g.FinishPullSettle ()
            g.Track (node :> ISource)

            if
#if !RANVIER_TRACE
                (node.Flags &&& NodeFlags.FreshMask) <> 0
                && (node.Flags &&& NodeFlags.Disposed) = 0
#else
                node.Freshness <> Freshness.Clean
                && not node.Disposed
#endif
            then
                g.MarkReaderCheck ()

            not (StatusBits.isUnready currentStatus)
        else
            g.Track (node :> ISource)
            not (StatusBits.isUnready node.Status)
#else
    member this.TryValue: Reading<'T> = this.GetTryFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetTryFast() : Reading<'T> =
#if !RANVIER_TRACE
        if (node.Flags &&& NodeFlags.CleanAndReadyMask) = 0 then
#else
        if StatusBits.isCleanAndReady node.FreshnessField node.StatusField then
#endif
            node.Graph.Track (node :> ISource)
            Ready value
        else
            this.ReadTrySlow ()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ReadTrySlow() : Reading<'T> =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        if NodeOps.RefreshForRead node then
            this.SettlePullAndReadTry ()
        else
            node.Graph.Track (node :> ISource)
            let st = node.Status

            if StatusBits.isUnready st then
                if StatusBits.isPending st then
                    Pending
                else
                    Failed node.FailureField.Error
            else
                Ready value

    static member val private ReadUntrackedFn: Memo<'T> -> Reading<'T> = fun m -> m.ReadUntracked ()

    member inline internal _.IsCleanAndReady =
#if !RANVIER_TRACE
        (node.Flags &&& NodeFlags.CleanAndReadyMask) = 0
#else
#if FABLE_COMPILER
        StatusBits.isCleanAndReady node.Freshness node.StatusField
#else
        StatusBits.isCleanAndReady node.FreshnessField node.StatusField
#endif
#endif

    member inline internal _.IsStaleNode =
#if !RANVIER_TRACE
        (node.Flags &&& NodeFlags.FreshMask) <> 0
        && (node.Flags &&& NodeFlags.Disposed) = 0
#else
#if FABLE_COMPILER
        node.Freshness <> Freshness.Clean
        && not node.Disposed
#else
        node.FreshnessField <> Freshness.Clean
        && not node.DisposedField
#endif
#endif

    member inline internal _.IsActiveNode =
#if !RANVIER_TRACE
        (node.Flags &&& NodeFlags.ActiveMask) <> 0
#else
#if FABLE_COMPILER
        node.Active <> 0uy
#else
        node.ActiveField <> 0uy
#endif
#endif

    member internal this.TryValueUntracked() : Reading<'T> =
        node.Graph.Entered (
            "An untracked read",
            fun () ->
                if this.IsCleanAndReady then
                    Ready value
                else
                    this.TryValueUntrackedSlow ()
        )

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.TryValueUntrackedSlow() : Reading<'T> =
        let g = node.Graph

        if Platform.isNull (box g.CurrentObserver) then
            this.ReadUntracked ()
        else
            g.RunUntrackedWith (Memo<'T>.ReadUntrackedFn, this)

    member internal this.ReadUntracked() : Reading<'T> =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        if NodeOps.RefreshForUntrackedRead node then
            this.SettlePullAndReadUntracked ()
        else
            let st = node.Status

            if StatusBits.isUnready st then
                if StatusBits.isPending st then
                    Pending
                else
                    Failed node.FailureField.Error
            else
                Ready value

    member internal this.RefreshUntrackedNoSettle() : bool =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        NodeOps.RefreshForUntrackedRead node

    member internal this.EnsureCurrentUntracked() : Status =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        if NodeOps.RefreshForUntrackedRead node then
            let currentStatus = node.Status
            node.Graph.FinishPullSettle ()

            if (node :> ISource).IsStale then
                currentStatus ||| Status.Stale
            else
                currentStatus
        else
            let st = node.Status

            if (node :> ISource).IsStale then
                st ||| Status.Stale
            else
                st

#if FABLE_COMPILER
    member this.ErrorOrigin: INode =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        Failure.OriginOf node.FailureField
#else
    member this.ErrorOrigin: INode = this.GetErrorOriginFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetErrorOriginFast() : INode =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        Failure.OriginOf node.FailureField
#endif

    member internal this.Failure =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        node.FailureField

#if FABLE_COMPILER
    member this.Peek =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        value
#else
    member this.Peek = this.GetPeekFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetPeekFast() : 'T =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        value
#endif

    member internal _.Published =
#if !RANVIER_TRACE
        (node.Flags &&& NodeFlags.Published) <> 0
#else
#if FABLE_COMPILER
        node.Published
#else
        node.PublishedField
#endif
#endif

    member internal _.ObserverCount = node.ObserverCount

    member internal _.SourceCount = node.SourcesField.Count

#if !FABLE_COMPILER
    static member val private ReadUntrackedStructFn: Memo<'T> -> struct (Status * 'T * exn) = fun m -> m.ReadUntrackedStructCore ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.TryReadStruct() : struct (Status * 'T * exn) =
        if this.IsCleanAndReady then
            node.Graph.Track (node :> ISource)
            struct (Status.None, value, null)
        else
            this.TryReadStructSlow ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.TryReadStructSlow() : struct (Status * 'T * exn) =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        let g = node.Graph

        if NodeOps.RefreshForRead node then
            let currentStatus = node.Status
            let currentFailure = node.FailureField
            let currentValue = value
            g.FinishPullSettle ()
            g.Track (node :> ISource)

            if (node :> ISource).IsStale then
                g.MarkReaderCheck ()

            if StatusBits.isUnready currentStatus then
                if StatusBits.isPending currentStatus then
                    struct (Status.Pending, Unchecked.defaultof<'T>, null)
                else
                    struct (Status.Error, Unchecked.defaultof<'T>, currentFailure.Error)
            else
                struct (Status.None, currentValue, null)
        else
            g.Track (node :> ISource)
            let st = node.Status

            if StatusBits.isUnready st then
                if StatusBits.isPending st then
                    struct (Status.Pending, Unchecked.defaultof<'T>, null)
                else
                    struct (Status.Error, Unchecked.defaultof<'T>, node.FailureField.Error)
            else
                struct (Status.None, value, null)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member internal this.TryReadUntrackedStruct() : struct (Status * 'T * exn) =
        if
            not (node.Graph.IsTracking ())
            && this.IsCleanAndReady
        then
            struct (Status.None, value, null)
        else
            this.TryReadUntrackedStructSlow ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.TryReadUntrackedStructSlow() : struct (Status * 'T * exn) =
        let g = node.Graph

        if not (g.IsTracking ()) then
            this.ReadUntrackedStructCore ()
        else
            g.RunUntrackedWith (Memo<'T>.ReadUntrackedStructFn, this)

    member private this.ReadUntrackedStructCore() : struct (Status * 'T * exn) =
#if !RANVIER_TRACE
        this.EnsureMaterializedIfClaimed ()
#endif
        if NodeOps.RefreshForUntrackedRead node then
            let currentStatus = node.Status
            let currentFailure = node.FailureField
            let currentValue = value
            node.Graph.FinishPullSettle ()

            if StatusBits.isUnready currentStatus then
                if StatusBits.isPending currentStatus then
                    struct (Status.Pending, Unchecked.defaultof<'T>, null)
                else
                    struct (Status.Error, Unchecked.defaultof<'T>, currentFailure.Error)
            else
                struct (Status.None, currentValue, null)
        else
            let st = node.Status

            if StatusBits.isUnready st then
                if StatusBits.isPending st then
                    struct (Status.Pending, Unchecked.defaultof<'T>, null)
                else
                    struct (Status.Error, Unchecked.defaultof<'T>, node.FailureField.Error)
            else
                struct (Status.None, value, null)
#endif

#if !FABLE_COMPILER
type Memo<'T> with
    /// <summary>A pure memo over <c>compute</c>.</summary>
    new(graph: Graph, compute: Func<'T voption, 'T>) as this =
        let eq = graph.EncodedComparer<'T>()
#if !RANVIER_TRACE
        let n = Memo<'T>.MakeNode(graph, ScopeMode.Pure, eq, Memo<'T>.OptionFuncCb)
#else
        let n = Memo<'T>.MakeNode(graph, ScopeMode.Pure)
#endif
        Memo<'T>(box compute, 3uy, Unchecked.defaultof<'T>, eq, n)
        then this.Attach ()

    /// <summary>An owning memo over <c>compute</c> when <c>owning</c> is true, a pure one otherwise.</summary>
    new(graph: Graph, compute: Func<'T voption, 'T>, owning: bool) as this =
        let mode = if owning then ScopeMode.Owning else ScopeMode.Pure
        let eq = graph.EncodedComparer<'T>()
#if !RANVIER_TRACE
        let n = Memo<'T>.MakeNode(graph, mode, eq, Memo<'T>.OptionFuncCb)
#else
        let n = Memo<'T>.MakeNode(graph, mode)
#endif
        Memo<'T>(box compute, 3uy, Unchecked.defaultof<'T>, eq, n)
        then this.Attach ()

    /// <summary>A pure memo over <c>compute</c>, which receives the value last published, or <c>seed</c> before the first.</summary>
    new(graph: Graph, seed: 'T, compute: Func<'T, 'T>) as this =
        let eq = graph.EncodedComparer<'T>()
#if !RANVIER_TRACE
        let n = Memo<'T>.MakeNode(graph, ScopeMode.Pure, eq, Memo<'T>.SeededFuncCb)
#else
        let n = Memo<'T>.MakeNode(graph, ScopeMode.Pure)
#endif
        Memo<'T>(box compute, 4uy, seed, eq, n)
        then this.Attach ()

    /// <summary>A memo over <c>compute</c>, which receives the value last published, or <c>seed</c> before the first.</summary>
    new(graph: Graph, seed: 'T, compute: Func<'T, 'T>, owning: bool) as this =
        let mode = if owning then ScopeMode.Owning else ScopeMode.Pure
        let eq = graph.EncodedComparer<'T>()
#if !RANVIER_TRACE
        let n = Memo<'T>.MakeNode(graph, mode, eq, Memo<'T>.SeededFuncCb)
#else
        let n = Memo<'T>.MakeNode(graph, mode)
#endif
        Memo<'T>(box compute, 4uy, seed, eq, n)
        then this.Attach ()
#endif

#if FABLE_COMPILER
module private EffectJs =
    [<Fable.Core.Emit("this")>]
    let jsThis<'T> () : 'T =
        Unchecked.defaultof<'T>
#endif

/// <summary>
/// A side effect that re-runs when its dependencies change.
/// </summary>
#if FABLE_COMPILER
type Effect(graph: Graph, bodyFn: unit -> unit) =
    let node = new EffectNode (graph, bodyFn)

    do
        node.PublicNode <- EffectJs.jsThis<Effect>() :> INode
        node.Start ()
#else
type Effect private (node: EffectNode) =
#endif
    /// <summary>
    /// Creates and starts an <c>EffectNode</c> directly without allocating a public <c>Effect</c> wrapper in untraced builds.
    /// </summary>
#if !RANVIER_TRACE
    static member internal Create(graph: Graph, body: unit -> unit) : EffectNode =
#if FABLE_COMPILER
        let n = new EffectNode (graph, body)
#else
        let n = new EffectNode (graph, body, null)
#endif
        n.Start ()
        n
#else
    static member internal Create(graph: Graph, body: unit -> unit) : Effect =
#if FABLE_COMPILER
        new Effect (graph, body)
#else
        let e = new Effect (new EffectNode (graph, body, null))
        e.BindAndStart ()
        e
#endif
#endif

    member internal _.Node = node

    member internal this.BindAndStart() =
        node.PublicNode <- this :> INode
        node.Start ()

    member internal _.Publishes
        with set (value: bool) = node.Publishes <- value

    member _.Dispose() =
        node.Dispose ()

    interface IDisposable with
        member _.Dispose() =
            node.Dispose ()

#if !FABLE_COMPILER
    interface IEffectHandle with
        member _.UnwrapEffectNode() =
            node :> IComputation
#endif

#if RANVIER_TRACE
    interface ITracedNode with
        member _.TraceGraph = box node.Graph
#endif

    interface INode with
        member _.Id = node.NodeId
        member _.Status = (node :> INode).Status

    interface IComputation with
        member _.AddSource s =
            NodeOps.AddSource (node, s)

        member _.MovedIn(slot, handle) =
            node.SourcesField.MovedIn (slot &&& EdgeHandle.SlotMask, handle)

        member _.HasReadSlot slot =
            (slot &&& EdgeHandle.SlotMask) < node.SourcesField.Cursor

        member _.MarkDirty() =
            NodeOps.EffectMarkDirty node

        member _.MarkCheck() =
            NodeOps.EffectMarkCheck node

    interface IScheduled with
        member _.Execute() =
            NodeOps.Execute node

    interface IScopeHost with
        member _.CanOwn = true
        member _.Scope = node.GetScopeNode ()

    member _.Status = node.Status

    member _.Error = Failure.ErrorOf node.FailureField
    member _.ErrorOrigin: INode = Failure.OriginOf node.FailureField
    member internal _.PendingSource = node.PendingSourceField

    member _.PendingSources =
        if Platform.isNull (box node.PendingSourceField) then
            Seq.empty
        else
            [| node.PendingSourceField |] :> seq<INode>

    member _.Runs = node.RunsField

#if !FABLE_COMPILER
type Effect with
    /// <summary>An effect running <c>body</c>, owned by the current owner and queued for its first run.</summary>
    new(graph: Graph, body: Action) as this =
        new Effect (new EffectNode (graph, Unchecked.defaultof<unit -> unit>, body))
        then this.BindAndStart ()
#endif

/// <summary>
/// A side effect split into a tracked, pure <c>compute</c> and an untracked <c>act</c> that runs with each settled value
/// unequal to the last value acted on. Inherits <c>Node</c> directly (zero separate node or owner-link allocation).
/// </summary>
[<Sealed>]
type internal EffectOn<'T> private (g: Graph, compute: unit -> 'T, act: 'T -> unit, equal: IEqualityComparer<'T>) =
#if !RANVIER_TRACE
    inherit Node(g.NextId (), g, NodeFlags.InitEffectOn)
    let mutable last = Unchecked.defaultof<'T>
    let mutable error: exn = Platform.nil ()
#else
    inherit Node(g.NextId (), NodeKind.EffectOn, g, Freshness.Clean)
    let mutable last = Unchecked.defaultof<'T>
    let mutable error: exn = Platform.nil ()
    let mutable hasActed = false
    let mutable acting = false
#endif

    /// <summary>An effect owned by the current owner, with its first run queued.</summary>
    static member internal Create(graph: Graph, compute: unit -> 'T, act: 'T -> unit) =
        let n = new EffectOn<'T> (graph, compute, act, graph.EncodedComparer<'T>())
        n.Start ()
        n

    /// <summary>An owned split effect whose cutoff uses <c>comparer</c>, with its first run queued.</summary>
    static member internal CreateWithComparer(graph: Graph, compute: unit -> 'T, act: 'T -> unit, comparer: IEqualityComparer<'T>) =
        let n = new EffectOn<'T> (graph, compute, act, ComparerSentinel.encode comparer)
        n.Start ()
        n

    member inline private this.HasActed =
#if !RANVIER_TRACE
        (this.Flags &&& NodeFlags.HasActed) <> 0
#else
        hasActed
#endif

    member inline private this.SetHasActed() =
#if !RANVIER_TRACE
        this.Flags <- this.Flags ||| NodeFlags.HasActed
#else
        hasActed <- true
#endif

    member inline private this.IsActing =
#if !RANVIER_TRACE
        (this.Flags &&& NodeFlags.Acting) <> 0
#else
        acting
#endif

    member inline private this.SetActing(v: bool) =
#if !RANVIER_TRACE
        if v then
            this.Flags <- this.Flags ||| NodeFlags.Acting
        else
            this.Flags <- this.Flags &&& ~~~NodeFlags.Acting
#else
        acting <- v
#endif

    member private this.Start() =
        let g = this.Graph
        Tracer.Bind (this.SourcesField, g, this.NodeId)
#if RANVIER_COUNTERS
        Counters.EffectCreated ()
#endif
        let owner = g.CurrentOwner
        Tracer.EffectNew (g, this.NodeId, owner)
        owner.AttachLink this
#if !RANVIER_TRACE
        if not this.IsDisposed && g.CanFuseIdleFlush () then
            NodeOps.RunFusedEffect (g, this)
        else
            NodeOps.EffectMarkDirty this
            g.RequestFlush ()
#else
        NodeOps.EffectMarkDirty this
        g.RequestFlush ()
#endif

    override this.RunDirect() =
#if !RANVIER_TRACE
        let g = this.Graph

        if
            SafeEquality.isSafe equal
            && Platform.isNull (box this.ScopeField)
            && Platform.isNull g.Journal
            && not g.HasUntrackedHost
            && not g.OwnerPending
            && g.CanEnterHostedTop ()
        then
            this.RunTopCore ()
        else
            this.RunGeneral ()
#else
        this.RunGeneral ()
#endif

#if !RANVIER_TRACE
    member private this.RunTopCore() =
        let g = this.Graph
        this.SourcesField.BeginRun ()
        this.SetStatusNone ()
        error <- Platform.nil ()
#if RANVIER_COUNTERS
        Counters.EffectRan ()
#endif
        let previousOwner = g.EnterHostedTop this
#if FABLE_COMPILER
        try
            this.RunTopHosted previousOwner
        with _ ->
            g.LeaveActionTop previousOwner
            g.LeaveHostedTopFailed (previousOwner, false)
            this.SetActing false
            reraise ()

    member private this.RunTopHosted(previousOwner: Owner) =
        let g = this.Graph
        let result = this.ComputeGuarded ()
        let ex = this.TakeCaught ()
#else
        let mutable result = Unchecked.defaultof<'T>
        let ex = this.TryCompute &result
#endif

        if not (Platform.isNull ex) then
            this.ComputeFailedTop (previousOwner, ex)
        elif
            this.IsDisposed
            || this.SourcesField.cursor < this.SourcesField.count
            || not (Platform.isNull (box this.ScopeField))
        then
            g.LeaveHostedTop (previousOwner, false)
            this.FinishSources ()

            if
                not this.IsDisposed
                && (not this.HasActed
                    || not (ComparerSentinel.equals equal last result))
            then
                this.Act result
        elif
            this.HasActed
            && ComparerSentinel.equals equal last result
        then
            g.LeaveHostedTop (previousOwner, false)
        else
            last <- result
            this.SetHasActed ()
            this.SetActing true
            g.SwitchToActionTop (this :> IScopeHost, previousOwner)
            let actEx = this.TryAct result
            g.LeaveActionTop previousOwner
            this.SetActing false

            if not (Platform.isNull actEx) then
#if FABLE_COMPILER
                this.RecordActFailure (Thrown.take actEx)
#else
                this.RecordActFailure actEx
#endif
#endif

    member private this.RunGeneral() =
        let g = this.Graph
        this.SourcesField.BeginRun ()
        this.SetStatusNone ()
        error <- Platform.nil ()
#if RANVIER_COUNTERS
        Counters.EffectRan ()
#endif
        Tracer.RunStart (g, this.NodeId, 0)
        let mutable v = Unchecked.defaultof<'T>
        let mutable settled = false

#if FABLE_COMPILER
        try
            try
#if !RANVIER_TRACE
                v <- g.RunHostedNode (this, compute, ())

                if (this.Flags &&& NodeFlags.Violated) <> 0 then
                    raise (InvalidOperationException ScopeMessages.effectOn)
#else
                v <- g.RunHosted (this :> IComputation, compute)

                if this.Violated then
                    raise (InvalidOperationException ScopeMessages.effectOn)
#endif

                settled <- true
            finally
                if this.IsDisposed then
                    this.SourcesField.Clear (this :> IComputation)
                else
                    this.SourcesField.EndRun (this :> IComputation)
        with ex ->
            this.RecordComputeFailure ex
#else
        if not g.OwnerPending && g.CanEnterHostedTop () then
            let prevOwner = g.EnterHostedTop this
            let ex = this.TryCompute &v

            if not (Platform.isNull ex) then
                this.ComputeFailedTop (prevOwner, ex)
            else
                settled <- true
                g.LeaveHostedTop (prevOwner, false)

                if
                    this.IsDisposed
                    || this.SourcesField.Cursor < this.SourcesField.Count
                then
                    this.FinishSources ()
        else
            settled <- this.ComputeNested &v
#endif

        let mutable unchanged = false

        if settled && this.HasActed then
            if SafeEquality.isSafe equal then
                unchanged <- ComparerSentinel.equals equal last v
            else
                match this.CompareGuarded v with
                | 1uy -> unchanged <- true
                | 2uy -> settled <- false
                | _ -> ()

        // RunEnd in both branches keeps the untraced IL equal to the unhooked method (tools/verify-trace.fsx, gate 1).
        if settled && not this.IsDisposed && not unchanged then
            this.Act v
            Tracer.RunEnd (g, this.NodeId, this.Status)
        else
            Tracer.RunEnd (g, this.NodeId, this.Status)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RecordComputeFailure(ex: exn) =
        if
#if !RANVIER_TRACE
            (this.Flags &&& NodeFlags.Violated) <> 0
#else
#if FABLE_COMPILER
            this.Violated
#else
            this.ViolatedField
#endif
#endif
        then
#if !RANVIER_TRACE
            this.Flags <- this.Flags &&& ~~~NodeFlags.Violated
#else
#if FABLE_COMPILER
            this.Violated <- false
#else
            this.ViolatedField <- false
#endif
#endif
            error <- ScopeMessages.failure ScopeMode.EffectOn ex
            this.SetStatusError ()
        else
            match ex with
            | NotReadyException _ -> this.SetStatusPending ()
            | _ ->
                error <- ex
                this.SetStatusError ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CompareGuarded(v: 'T) : byte =
        try
            if ComparerSentinel.equals equal last v then 1uy else 0uy
        with ex ->
            error <- ex
            this.SetStatusError ()
            2uy

#if !FABLE_COMPILER
    member private this.ComputeNested(result: byref<'T>) =
        let g = this.Graph
        let saved = g.EnterHostedNode this
        let ex = this.TryCompute &result

        if Platform.isNull ex then
            g.LeaveHosted saved

            if
                this.IsDisposed
                || this.SourcesField.Cursor < this.SourcesField.Count
            then
                this.FinishSources ()

            true
        else
            this.ComputeFailed (saved, ex)
            false

    member private this.TryCompute(result: byref<'T>) : exn =
        try
            result <- compute ()

            if
                this.Graph.RaisedPending
#if !RANVIER_TRACE
                || (this.Flags &&& NodeFlags.Violated) <> 0
#else
                || this.ViolatedField
#endif
            then
                this.FailAfterCompute ()

            null
        with ex ->
            ex
#endif

#if FABLE_COMPILER && !RANVIER_TRACE
    member private this.ComputeGuarded() : 'T =
        try
            let result = compute ()

            if
                this.Graph.RaisedPending
                || (this.Flags &&& NodeFlags.Violated) <> 0
            then
                this.FailAfterCompute ()

            result
        with ex ->
            this.Graph.Caught <- Thrown.hold ex
            Unchecked.defaultof<'T>

    member private this.TakeCaught() : exn =
        let g = this.Graph
        let ex = g.Caught

        if not (Platform.isNull ex) then
            g.Caught <- Platform.nil ()

        ex
#endif

#if !FABLE_COMPILER || !RANVIER_TRACE
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.FailAfterCompute() =
        this.Graph.CheckRaised ()

        if
#if !RANVIER_TRACE
            (this.Flags &&& NodeFlags.Violated) <> 0
#else
#if FABLE_COMPILER
#else
            this.ViolatedField
#endif
#endif
        then
            raise (InvalidOperationException ScopeMessages.effectOn)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.FinishSources() =
        if this.IsDisposed then
            this.SourcesField.Clear (this :> IComputation)
        else
            this.SourcesField.EndRun (this :> IComputation)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ComputeFailedTop(prevOwner: Owner, ex: exn) =
        this.Graph.LeaveHostedTopFailed (prevOwner, false)
        this.FinishSources ()
#if FABLE_COMPILER
        this.RecordComputeFailure (Thrown.take ex)
#else
        this.RecordComputeFailure ex
#endif
#endif

#if !FABLE_COMPILER
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ComputeFailed(saved: HostedFrame, ex: exn) =
        this.Graph.LeaveHosted saved
        this.FinishSources ()
        this.RecordComputeFailure ex
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RecordActFailure(ex: exn) =
        match ex with
        | NotReadyException _ -> this.SetStatusPending ()
        | _ ->
            error <- ex
            this.SetStatusError ()

    member private this.Act(v: 'T) =
        let g = this.Graph
        last <- v
        this.SetHasActed ()

        if not (Platform.isNull (box this.ScopeField)) then
            g.Discharge this.ScopeField

        if not this.IsDisposed then
            this.SetActing true

#if FABLE_COMPILER
            try
                try
                    if Platform.isNull g.Journal then
                        g.RunUntrackedHostedWith (this :> IScopeHost, act, v)
                    else
                        this.ActJournaled v
                finally
                    this.SetActing false
            with ex ->
                this.RecordActFailure ex
#else
            if Platform.isNull g.Journal then
                let saved = g.EnterUntrackedHosted (this :> IScopeHost)
                let ex = this.TryAct v
                g.LeaveUntrackedHosted saved
                this.SetActing false

                if not (Platform.isNull ex) then
                    this.RecordActFailure ex
            else
                this.ActJournaledGuarded v
#endif

#if !FABLE_COMPILER || !RANVIER_TRACE
    member private this.TryAct(v: 'T) : exn =
        try
            act v
            Platform.nil ()
        with ex ->
#if FABLE_COMPILER
            Thrown.hold ex
#else
            ex
#endif
#endif

#if !FABLE_COMPILER
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ActJournaledGuarded(v: 'T) =
        try
            try
                this.ActJournaled v
            finally
                this.SetActing false
        with ex ->
            this.RecordActFailure ex
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ActJournaled(v: 'T) =
        let g = this.Graph
        let recorder = g.Journal

        recorder.RunWork (fun () ->
            recorder.EnterDerived ()

            try
                g.RunUntrackedHostedWith (this :> IScopeHost, act, v)
            finally
                recorder.LeaveDerived ())

    /// <summary>Detaches from the sources and disposes the action's scope. Idempotent.</summary>
    member this.Dispose() =
        this.DisposeFromUser ()

    member _.Error = error

    override this.CanOwnNode = this.IsActing

    override this.GetScopeNode() =
        if this.IsActing then
            if Platform.isNull (box this.ScopeField) then
                let s = new Owner (this.Graph.Root)
                this.ScopeField <- s
                Tracer.ScopeNew (s, this.Graph, this.NodeId)

                if this.IsDisposed then
                    s.Dispose ()

            this.ScopeField
        else
#if !RANVIER_TRACE
            this.Flags <- this.Flags ||| NodeFlags.Violated
#else
#if FABLE_COMPILER
            this.Violated <- true
#else
            this.ViolatedField <- true
#endif
#endif
            raise (InvalidOperationException ScopeMessages.effectOn)

    interface IDisposable with
        member this.Dispose() =
            this.DisposeFromUser ()

#if RANVIER_TRACE
    interface ITracedNode with
        member this.TraceGraph = box this.Graph
#endif

    interface IScheduled with
        member this.Execute() =
            NodeOps.Execute this

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

/// <summary>Outcome of evaluating Phase 1 (<c>read</c>) on a two-phase <c>AsyncMemo&lt;'U&gt;</c>.</summary>
type internal AsyncPhase1Result =
    | EqualKey = 0uy
    | UnequalKey = 1uy
    | Phase1Failed = 2uy

/// <summary>Type-erased Phase 1/Phase 2 driver stored on a two-phase <c>AsyncMemo&lt;'U&gt;</c> (Theorem B17).</summary>
type internal IAsyncKeyPhase<'U> =
    abstract ReadAndCompare: unit -> AsyncPhase1Result
    abstract CommitNextKey: unit -> unit
    abstract ResetKey: unit -> unit
    abstract LaunchBody: host: Graph * previous: Previous<'U> * token: CancellationToken -> Task<'U>
    abstract Phase1Exception: exn

/// <summary>Typed state of a two-phase <c>AsyncMemo&lt;'U&gt;</c> created by <c>createAsyncOn*</c>.</summary>
[<Sealed>]
type internal AsyncOnState<'K, 'U>(read: unit -> 'K, compute: 'K -> Previous<'U> -> CancellationToken -> Task<'U>, keyEqual: IEqualityComparer<'K>) =
#if !FABLE_COMPILER
    let adapted =
        OptimizedClosures.FSharpFunc<'K, Previous<'U>, CancellationToken, Task<'U>>.Adapt compute
#endif
    let mutable hasKey = false
    let mutable lastKey = Platform.zero<'K>()
    let mutable candidateKey = Platform.zero<'K>()
    let mutable phase1Ex: exn = Platform.nil ()
#if !FABLE_COMPILER
    let mutable activeKey = Unchecked.defaultof<'K>

    let invokeFlight =
        fun (previous: Previous<'U>) (token: CancellationToken) -> adapted.Invoke (activeKey, previous, token)
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.CompareKeyGuarded(nextKey: 'K) : AsyncPhase1Result =
        try
            if ComparerSentinel.equals keyEqual lastKey nextKey then
                AsyncPhase1Result.EqualKey
            else
                AsyncPhase1Result.UnequalKey
        with ex ->
            phase1Ex <- ex
            AsyncPhase1Result.Phase1Failed

    interface IAsyncKeyPhase<'U> with
        member this.ReadAndCompare() =
            phase1Ex <- Platform.nil ()
            let nextKey = read ()
            candidateKey <- nextKey

            if not hasKey then
                AsyncPhase1Result.UnequalKey
            elif SafeEquality.isSafe keyEqual then
                if ComparerSentinel.equals keyEqual lastKey nextKey then
                    AsyncPhase1Result.EqualKey
                else
                    AsyncPhase1Result.UnequalKey
            else
                this.CompareKeyGuarded nextKey

        member _.CommitNextKey() =
            lastKey <- candidateKey
            hasKey <- true

        member _.ResetKey() =
            hasKey <- false

        member _.LaunchBody(host, previous, token) =
#if FABLE_COMPILER
            ignore host
            compute lastKey previous token
#else
            let prevActive = activeKey
            activeKey <- lastKey

            try
                Platform.runFlightBodyWith (host :> Platform.IContinuationHost) invokeFlight previous token
            finally
                activeKey <- prevActive
#endif

        member _.Phase1Exception = phase1Ex

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
type AsyncMemo<'T>
    private
    (graph: Graph, compute: Previous<'T> -> CancellationToken -> Task<'T>, keyPhase: IAsyncKeyPhase<'T>, mode: ScopeMode, policy: FlightPolicy) =
    let isSplit = not (Platform.isNull (box keyPhase))
    let id = graph.NextId ()
#if !FABLE_COMPILER && !RANVIER_TRACE
    let mutable observers = ObserverList.Create ()
#else
#if FABLE_COMPILER && !RANVIER_TRACE
    let mutable observers: ObserverList = Platform.nil ()
#else
    let observers = ObserverSet ()
    do Tracer.Bind (observers, graph, id)
#endif
#endif
    let mutable sources = SourceList.Create ()
    do Tracer.Bind (sources, graph, id)
    let mutable pendingSource: INode = Platform.nil ()

    let mutable freshness = Freshness.Dirty
    let mutable status = Status.Pending ||| Status.Uninitialized
    let mutable value = Unchecked.defaultof<'T>
    let mutable failure: Failure = Platform.nil ()
    let mutable phase2Status = Status.Pending ||| Status.Uninitialized
    let mutable phase2Failure: Failure = Platform.nil ()
    let mutable phase1Status = Status.None
    let mutable phase1Failure: Failure = Platform.nil ()
    let mutable phase1PendingSource: INode = Platform.nil ()
    let mutable owedTrailing = false
    let mutable phase1Active = false
    let mutable scopeDirty = false
    let mutable unannouncedSettle = false
    let mutable runs = 0
    let mutable disposed = false
    let mutable link: OwnerLink = Platform.nil ()

    /// <summary>Set when a run ends stale, as on <c>Memo</c>. The next mark notifies the observers and clears it.</summary>
    let mutable unannounced = false

    /// <summary>
    /// Owner of the nodes an owning body creates, allocated by the first of
    /// them. Discharged before each flight starts and disposed with the
    /// computation.
    /// </summary>
    let mutable scope: Owner = Platform.nil ()

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
    let mutable cts: CancellationTokenSource = Platform.nil ()

    /// <summary>The number of flights started and not yet settled, under <c>CancelPrevious</c> and <c>KeepLatest</c>.</summary>
    let mutable flying = 0

    /// <summary>The number of bodies executing. The shared source is retained while it is nonzero.</summary>
    let mutable running = 0
    let mutable checking = false

    /// <summary>
    /// Out-of-order settled outcomes awaiting an earlier sequence slot under <c>Queue</c>; null until needed.
    /// </summary>
    let mutable pendingQueue: Dictionary<int, struct (int * Platform.FlightOutcome<'T>)> =
        Platform.nil ()

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
    let mutable settledTask: Task<'T voption> = Platform.nil ()

    /// <summary>
    /// The <c>Previous.Settled</c> tasks still waiting, keyed by the number of applied results that completes each.
    /// </summary>
    let mutable waiters: Dictionary<int, Platform.Deferred<'T voption>> =
        Platform.nil ()

    /// <summary>
    /// True while <c>Launch</c> attaches the settle continuation. A flight already complete
    /// publishes inline, during the read that launched it.
    /// </summary>
    let mutable launching = false
    let mutable wokeInLaunch = false

    member inline private _.SyncVisibleOn() =
        if not disposed then
            if phase1Status = Status.Pending then
                status <-
                    Status.Pending
                    ||| (phase2Status &&& Status.Uninitialized)

                failure <- Platform.nil ()
                pendingSource <- phase1PendingSource
            elif phase1Status = Status.Error then
                status <- Status.Error
                failure <- phase1Failure
                pendingSource <- Platform.nil ()
            elif owedTrailing then
                status <- Status.Pending
                failure <- Platform.nil ()
                pendingSource <- Platform.nil ()
            else
                status <- phase2Status
                failure <- phase2Failure
                pendingSource <- Platform.nil ()

    member private _.NotifyObserversDirty() =
#if FABLE_COMPILER && !RANVIER_TRACE
        if not (Platform.isNull observers) then
            observers.NotifyDirty ()
#else
        observers.NotifyDirty ()
#endif

    member inline private _.NotifyObserversCheck() =
#if FABLE_COMPILER && !RANVIER_TRACE
        if not (Platform.isNull observers) then
            if
                observers.Count = 1
                && EdgeHandle.isNodeSlot observers.FirstSlot
            then
                NodeOps.MarkCheck (Platform.fastCast<Node> observers.FirstRef)
            else
                observers.NotifyCheck ()
#else
        observers.NotifyCheck ()
#endif

    member inline private _.NotifyOtherObservers() =
#if !FABLE_COMPILER && !RANVIER_TRACE
        graph.NotifyMovedList &observers
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        graph.NotifyMovedList observers
#else
        graph.NotifyMoved observers
#endif
#endif

    member private this.Wake() =
        Tracer.Moved (graph, id, Failure.Payload (failure, box value))

        if launching then
            wokeInLaunch <- true
#if !FABLE_COMPILER && !RANVIER_TRACE
            graph.NotifyMovedList &observers
#else
#if FABLE_COMPILER && !RANVIER_TRACE
            graph.NotifyMovedList observers
#else
            graph.NotifyMoved observers
#endif
#endif
        else
            graph.NoteStaleTransition ()
            this.NotifyObserversDirty ()

        Tracer.Notified graph
        graph.RequestFlush ()

    member private this.WakeDuringRun() =
        Tracer.Moved (graph, id, Failure.Payload (failure, box value))
#if !FABLE_COMPILER && !RANVIER_TRACE
        graph.NotifyMovedList &observers
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        graph.NotifyMovedList observers
#else
        graph.NotifyMoved observers
#endif
#endif
        Tracer.Notified graph
        graph.RequestFlush ()

    member private this.NotifyIfMovedOn(prevStatus: Status, prevFailure: Failure, prevPending: INode) =
        if not disposed && not wokeInLaunch then
            if
                unannouncedSettle
                && phase1Status = Status.None
                && not owedTrailing
            then
                unannouncedSettle <- false
                this.WakeDuringRun ()
            else
                let curNorm = status &&& ~~~Status.Uninitialized
                let prevNorm = prevStatus &&& ~~~Status.Uninitialized

                let moved =
                    curNorm <> prevNorm
                    || (curNorm = Status.Error
                        && not (obj.ReferenceEquals (failure, prevFailure))
                        && Failure.Moved (failure, prevFailure))
                    || (curNorm = Status.Pending
                        && not (obj.ReferenceEquals (box pendingSource, box prevPending)))

                if moved then
                    Tracer.Unattributed graph
                    this.NotifyOtherObservers ()
                    Tracer.Notified graph

                    if graph.QueuePending then
                        graph.OweFlush ()

    member private _.LastSettled() =
        if published then ValueSome value else ValueNone

    // Under the lock `Settled` takes, so a read off the graph thread never caches a value this write replaces.
    member private this.Write(v: 'T) =
#if FABLE_COMPILER
        value <- v
        published <- true
        settledTask <- Platform.nil ()
#else
        lock this (fun () ->
            value <- v
            published <- true
            settledTask <- null)
#endif

    /// <summary>
    /// The value last published, as a completed task shared by every read until the next publish. Called under the
    /// instance lock.
    /// </summary>
    member private _.SettledNow() =
        if Platform.isNull settledTask then
            settledTask <-
                if published then
                    Platform.completedWith (ValueSome value)
                else
                    NothingPublished<'T>.Task

        settledTask

    /// <summary>
    /// Counts one more chained result applied and completes the waiter keyed on the new count. Called under the
    /// instance lock.
    /// </summary>
    member private this.ReleaseNext() =
        applied <- applied + 1

        if not (Platform.isNull waiters) then
            let waiter = Platform.findOrDefault waiters applied

            if not (Platform.isNull (box waiter)) then
                waiters.Remove applied |> ignore
                Platform.resolve waiter (this.LastSettled ())

    /// <summary>Resets or disposes the flight token's source without cancelling it.</summary>
    member private _.RetireSource() =
        if not (Platform.isNull cts) then
#if NET6_0_OR_GREATER && !FABLE_COMPILER
            if not (cts.TryReset ()) then
                cts.Dispose ()
                cts <- null
#else
            cts.Dispose ()
            cts <- Platform.nil ()
#endif

    /// <summary>
    /// Retires the flight token's source once no body is executing and every flight holding it has settled.
    /// Registrations left on the token are released with it.
    /// </summary>
    member private this.RetireIfQuiet() =
        if running = 0 then
            match policy with
            | FlightPolicy.Queue
            | FinishCurrent
            | Drop
            | DropWhilePending when queued = 0 -> this.RetireSource ()
#if !FABLE_COMPILER
            | CancelPrevious
#endif
            | KeepLatest when flying = 0 -> this.RetireSource ()
            | _ -> ()

    /// <summary>
    /// Brings every source current. Clears a stale source left behind by a discarded change, so the source's next
    /// change reaches this node.
    /// </summary>
    member private _.RefreshSources() =
        if graph.Deferring then
            let mutable i = 0

            while i < sources.Count do
                sources.RefreshSourceAt i
                i <- i + 1

            graph.ClearLastRaisedIfTop ()
        else
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
    member private this.DiscardAtSettle(gen: int) =
        this.RefreshSources ()

        if freshness = Freshness.Dirty then
            Tracer.RunDropped (graph, id, gen)

        freshness <- Freshness.Clean

    member private this.InspectAtSettleCore(gen: int, dropUnequal: bool) : bool =
        if
            freshness = Freshness.Check
            && not checking
            && running = 0
        then
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

        if freshness <> Freshness.Dirty then
            queued = 2 && not dropUnequal
        else
            freshness <- Freshness.Clean
            unannounced <- false
            let prevStatus = status
            let prevFailure = failure
            let prevPending = pendingSource
            runs <- runs + 1
            Tracer.RunStart (graph, id, runs)
            sources.BeginRun ()
            phase1Active <- true
            running <- running + 1

            let mutable p1Outcome = AsyncPhase1Result.EqualKey
            let mutable p1Pending: INode = Platform.nil ()
            let mutable p1Error: exn = Platform.nil ()

            try
                try
                    p1Outcome <-
#if FABLE_COMPILER
                        graph.RunHosted (this :> IComputation, fun () -> keyPhase.ReadAndCompare ())
#else
                        let saved = graph.EnterHosted (this :> IComputation)

                        try
                            let r = keyPhase.ReadAndCompare ()
                            graph.CheckRaised ()
                            r
                        finally
                            graph.LeaveHosted saved
#endif

                    if violated then
                        raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))
                finally
                    phase1Active <- false
                    running <- running - 1

                    if disposed then
                        sources.Clear (this :> IComputation)
                    else
                        sources.EndRun (this :> IComputation)
            with
            | ex when violated ->
                violated <- false
                p1Outcome <- AsyncPhase1Result.Phase1Failed
                p1Error <- ScopeMessages.failure ScopeMode.Pure ex
            | NotReadyException src -> p1Pending <- src
            | ex ->
                p1Outcome <- AsyncPhase1Result.Phase1Failed
                p1Error <- ex

            let mutable resultOwed = false

            if disposed then
                ()
            elif not (Platform.isNull (box p1Pending)) then
                phase1Status <- Status.Pending
                phase1PendingSource <- p1Pending
                phase1Failure <- Platform.nil ()
                this.SyncVisibleOn ()
                resultOwed <- queued = 2 && not dropUnequal
            elif p1Outcome = AsyncPhase1Result.Phase1Failed then
                let err =
                    if Platform.isNull p1Error then
                        keyPhase.Phase1Exception
                    else
                        p1Error

                phase1Status <- Status.Error
                phase1PendingSource <- Platform.nil ()
                phase1Failure <- graph.FailureOf (err, this, prevFailure)
                this.SyncVisibleOn ()
                resultOwed <- queued = 2 && not dropUnequal
            elif p1Outcome = AsyncPhase1Result.EqualKey then
                phase1Status <- Status.None
                phase1PendingSource <- Platform.nil ()
                phase1Failure <- Platform.nil ()
                this.SyncVisibleOn ()
                resultOwed <- queued = 2 && not dropUnequal
            else
                phase1Status <- Status.None
                phase1PendingSource <- Platform.nil ()
                phase1Failure <- Platform.nil ()
                this.SyncVisibleOn ()

                if dropUnequal then
                    Tracer.RunDropped (graph, id, gen)
                else
                    resultOwed <- true

            if not disposed then
                let curNorm = status &&& ~~~Status.Uninitialized
                let prevNorm = prevStatus &&& ~~~Status.Uninitialized

                if
                    curNorm <> prevNorm
                    || (curNorm = Status.Error
                        && not (obj.ReferenceEquals (failure, prevFailure))
                        && Failure.Moved (failure, prevFailure))
                    || (curNorm = Status.Pending
                        && not (obj.ReferenceEquals (box pendingSource, box prevPending)))
                then
                    Tracer.Unattributed graph
                    this.NotifyOtherObservers ()
                    Tracer.Notified graph

                    if graph.QueuePending then
                        graph.OweFlush ()

            Tracer.RunEnd (graph, id, status)

            if freshness <> Freshness.Clean then
                unannounced <- true
                graph.NoteStaleTransition ()

            resultOwed

    member private this.InspectAtSettleOn(gen: int, dropUnequal: bool) : bool =
        if graph.Deferring then
            let r = this.InspectAtSettleCore (gen, dropUnequal)
            graph.ClearLastRaisedIfTop ()
            r
        else
            graph.EnterPull ()

            try
                this.InspectAtSettleCore (gen, dropUnequal)
            finally
                graph.ExitPull ()

    /// <summary>Applies the result of the run numbered <c>gen</c>. Runs on the graph thread.</summary>
    member private this.ApplyResult(gen: int, outcome: Platform.FlightOutcome<'T>) =
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
                    if isSplit then
                        this.InspectAtSettleOn (gen, true) |> ignore
                    else
                        this.DiscardAtSettle gen

                queued <- 0
                gen = generation
            | DropWhilePending ->
                if
                    not disposed
                    && (queued = 2 || freshness <> Freshness.Clean)
                then
                    let isCompleted =
                        match outcome with
                        | Platform.FlightOutcome.Completed _ -> true
                        | _ -> false

                    if isSplit then
                        owed <- this.InspectAtSettleOn (gen, isCompleted)
                    elif isCompleted then
                        this.DiscardAtSettle gen
                    else
                        // A Check is a discarded change only once a source resolves it to Dirty.
                        if queued = 1 && freshness = Freshness.Check then
                            this.RefreshSources ()

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

        this.RetireIfQuiet ()

        if isSplit then
            if current && not disposed then
                match outcome with
                | Platform.FlightOutcome.Completed v when owed ->
                    Tracer.FlightSettled (graph, id, gen, 0, 2, box v)
                    this.Write v
                    phase2Failure <- Platform.nil ()
                    phase2Status <- Status.None
                    owedTrailing <- true
                    unannouncedSettle <- true
                    this.SyncVisibleOn ()
                | Platform.FlightOutcome.Faulted _
                | Platform.FlightOutcome.Canceled _ when owed ->
                    Tracer.FlightFailureDrop (
                        graph,
                        id,
                        gen,
                        true,
                        (match policy with
                         | FlightPolicy.FinishCurrent -> true
                         | _ -> false)
                    )

                    keyPhase.ResetKey ()
                    owedTrailing <- false
                    unannouncedSettle <- false
                    phase2Failure <- Platform.nil ()

                    phase2Status <-
                        Status.Pending
                        ||| (phase2Status &&& Status.Uninitialized)

                    this.SyncVisibleOn ()
                | Platform.FlightOutcome.Completed v ->
                    let masked = phase1Status <> Status.None
                    let held = if phase1Status = Status.Pending then 1 else 0
                    Tracer.FlightSettled (graph, id, gen, 0, held, box v)
                    this.Write v
                    phase2Failure <- Platform.nil ()
                    phase2Status <- Status.None
                    owedTrailing <- false
                    this.SyncVisibleOn ()

                    if masked then
                        unannouncedSettle <- true
                    else
                        unannouncedSettle <- false
                        this.Wake ()
                | Platform.FlightOutcome.Faulted ex
                | Platform.FlightOutcome.Canceled ex ->
                    let ex = UntrackedPhase.wrapPending UntrackedPhase.asyncOnPending ex
                    let masked = phase1Status <> Status.None

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

                    phase2Failure <- graph.FailureOf (ex, (this :> INode), null)
                    phase2Status <- Status.Error
                    owedTrailing <- false
                    this.SyncVisibleOn ()

                    if masked then
                        unannouncedSettle <- true
                    else
                        unannouncedSettle <- false
                        this.Wake ()
            else
                Tracer.FlightDrop (graph, id, gen, (if disposed then 2 else 1))
        else
            // An owed trailing run holds the node pending as a pending source does.
            let suspended = owed || not (Platform.isNull (box pendingSource))

            if current && not disposed then
                match outcome with
                // Under `Queue`, the newest run is waiting on a source: an
                // older flight's value is the last settled one, and the node
                // stays pending.
                | Platform.FlightOutcome.Completed v when suspended ->
                    Tracer.FlightSettled (graph, id, gen, 0, (if owed then 2 else 1), box v)
                    this.Write v
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
                    this.Write v
                    failure <- Platform.nil ()
                    status <- Status.None
                    this.Wake ()
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

                    failure <- graph.FailureOf (ex, (this :> INode), null)
                    status <- Status.Error
                    this.Wake ()
            else
                Tracer.FlightDrop (graph, id, gen, (if disposed then 2 else 1))

        match policy with
        // Every chained result, dropped or failed included, completes the waiter behind it with the last settled value.
        | FlightPolicy.Queue ->
#if FABLE_COMPILER
            this.ReleaseNext ()
#else
            lock this this.ReleaseNext
#endif
        // The trailing or replacing run starts at the next read: readers are told the value is stale again.
        | FinishCurrent
        | DropWhilePending when owed && not disposed ->
            freshness <- Freshness.Dirty
            Tracer.TrailingRun (graph, id)
            graph.NoteStaleTransition ()

            if isSplit then
                this.NotifyObserversCheck ()
            else
                this.NotifyObserversDirty ()

            Tracer.Notified graph
            graph.RequestFlush ()
        | CancelPrevious
        | KeepLatest
        | FinishCurrent
        | Drop
        | DropWhilePending -> ()

    member private this.ApplyQueued(seqNum: int, gen: int, outcome: Platform.FlightOutcome<'T>) =
        if seqNum = applied + 1 then
            this.ApplyResult (gen, outcome)

            while not (Platform.isNull pendingQueue)
                  && pendingQueue.Count > 0
                  && pendingQueue.ContainsKey (applied + 1) do
                let nextSeq = applied + 1
                let struct (nextGen, nextOutcome) = pendingQueue[nextSeq]
                pendingQueue.Remove nextSeq |> ignore
                this.ApplyResult (nextGen, nextOutcome)
        else
            if Platform.isNull pendingQueue then
                pendingQueue <- Dictionary<int, struct (int * Platform.FlightOutcome<'T>)>()

            pendingQueue[seqNum] <- struct (gen, outcome)

#if FABLE_COMPILER
    member private this.Publish(gen: int, outcome: Platform.FlightOutcome<'T>) =
        this.ApplyResult (gen, outcome)

    member private this.PublishQueued(seqNum: int, gen: int, outcome: Platform.FlightOutcome<'T>) =
        this.ApplyQueued (seqNum, gen, outcome)
#else
    member private this.Publish(gen: int, outcome: Platform.FlightOutcome<'T>) =
        if graph.IsOnGraphThread then
            this.ApplyResult (gen, outcome)
        else
            graph.PostQueued (fun () -> this.ApplyResult (gen, outcome))

    member private this.PublishQueued(seqNum: int, gen: int, outcome: Platform.FlightOutcome<'T>) =
        if graph.IsOnGraphThread then
            this.ApplyQueued (seqNum, gen, outcome)
        else
            graph.PostQueued (fun () -> this.ApplyQueued (seqNum, gen, outcome))

    interface Platform.ISettledWaiter<'T> with
        member this.OnSettled(outcome, gen, seqNum) =
            if seqNum = 0 then
                this.Publish (gen, outcome)
            else
                this.PublishQueued (seqNum, gen, outcome)
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
        let memo = new AsyncMemo<'T> (graph, compute, Platform.nil (), mode, policy)
        memo.Attach ()
        memo

    /// <summary>
    /// A two-phase split async memo over pure <c>read</c> and untracked <c>compute</c>, owned by the current owner.
    /// </summary>
    static member internal CreateOn
        (
            graph: Graph,
            read: unit -> 'K,
            compute: 'K -> Previous<'T> -> CancellationToken -> Task<'T>,
            mode: ScopeMode,
            policy: FlightPolicy,
            ?keyComparer: IEqualityComparer<'K>
        ) : AsyncMemo<'T> =
        if UntrackedPhase.isNullFn1 read then
            nullArg (nameof read)

        if UntrackedPhase.isNullFn3 compute then
            nullArg (nameof compute)

        let keyEq =
            match keyComparer with
            | Some c ->
                if Platform.isNull c then
                    nullArg (nameof keyComparer)

                ComparerSentinel.encode c
            | None -> graph.EncodedComparer<'K>()

        let state = AsyncOnState<'K, 'T>(read, compute, keyEq) :> IAsyncKeyPhase<'T>
        let memo = new AsyncMemo<'T> (graph, UntrackedPhase.nullFn2 (), state, mode, policy)
        memo.Attach ()
        memo

    member internal this.Attach() =
        let owner = graph.CurrentOwner
        Tracer.AsyncMemoNew (graph, id, owner)
        link <- owner.AttachLinked this

    member private this.DetachSources() =
        sources.Clear (this :> IComputation)

    member private this.Start() =
        if isSplit then
            this.StartOn ()
        elif
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

    member private this.FailLaunchOn(gen: int, ex: exn) =
        match policy with
        | FlightPolicy.Queue when queued > 0 ->
            queued <- queued + 1
            chained <- chained + 1
            let seqNum = chained
#if !FABLE_COMPILER
            if graph.IsOnGraphThread then
                this.ApplyQueued (seqNum, gen, Platform.FlightOutcome<'T>.Faulted ex)
            else
                this.PublishQueued (seqNum, gen, Platform.FlightOutcome<'T>.Faulted ex)
#else
            this.PublishQueued (seqNum, gen, Platform.FlightOutcome<'T>.Faulted ex)
#endif
        | _ ->
            phase2Failure <- graph.FailureOf (ex, this, null)
            phase2Status <- Status.Error
            this.SyncVisibleOn ()

    member private this.LaunchUntracked(prevStatus: Status, prevFailure: Failure, prevPending: INode) =
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
        let previous = Previous<'T>(this :> IPreviousSource<'T>, chained)
        phase2Failure <- Platform.nil ()

        phase2Status <-
            Status.Pending
            ||| (phase2Status &&& Status.Uninitialized)

        this.SyncVisibleOn ()

        if graph.HasLastRaised then
            graph.ClearLastRaised ()

        let mutable flight = Platform.nil ()
        running <- running + 1

        try
            try
#if FABLE_COMPILER
                let started =
                    graph.RunUntrackedHosted (
                        this :> IScopeHost,
                        fun () ->
                            let s = keyPhase.LaunchBody (graph, previous, token)

                            if violated && not (Platform.isNull s) then
                                Platform.outcomeOf s |> ignore

                            s
                    )
#else
                let hasJournal = not (Platform.isNull graph.Journal)

                if hasJournal then
                    graph.Journal.EnterDerived ()

                let saved = graph.EnterUntrackedHosted (this :> IScopeHost)

                let started =
                    try
                        let s = keyPhase.LaunchBody (graph, previous, token)

                        if violated && not (Platform.isNull s) then
                            Platform.outcomeOf s |> ignore

                        s
                    finally
                        graph.LeaveUntrackedHosted saved

                        if hasJournal then
                            graph.Journal.LeaveDerived ()
#endif

                if violated then
                    raise (InvalidOperationException (ScopeMessages.forMode mode))

                flight <- started
            finally
                running <- running - 1
        with
        | ex when violated ->
            violated <- false
            this.FailLaunchOn (gen, ScopeMessages.failure mode ex)
        | ex -> this.FailLaunchOn (gen, UntrackedPhase.wrapPending UntrackedPhase.asyncOnPending ex)

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
                        this.ApplyQueued (seqNum, gen, outcome)
                    | KeepLatest
                    | CancelPrevious ->
                        flying <- flying + 1
                        this.ApplyResult (gen, outcome)
                    | FinishCurrent
                    | Drop
                    | DropWhilePending ->
                        queued <- 1
                        this.ApplyResult (gen, outcome)
                else
                    match policy with
                    | FlightPolicy.Queue ->
                        queued <- queued + 1
                        chained <- chained + 1
                        let seqNum = chained
                        Platform.attachSettled flight (this :> Platform.ISettledWaiter<'T>) gen seqNum
                    | KeepLatest
                    | CancelPrevious ->
                        flying <- flying + 1
                        Platform.attachSettled flight (this :> Platform.ISettledWaiter<'T>) gen 0
                    | FinishCurrent
                    | Drop
                    | DropWhilePending ->
                        queued <- 1
                        Platform.attachSettled flight (this :> Platform.ISettledWaiter<'T>) gen 0
#else
                match policy with
                | FlightPolicy.Queue ->
                    queued <- queued + 1
                    chained <- chained + 1
                    let seqNum = chained

                    Platform.whenSettled flight (fun o -> this.PublishQueued (seqNum, gen, o))
                    |> ignore
                | KeepLatest ->
                    flying <- flying + 1

                    Platform.whenSettled flight (fun o -> this.Publish (gen, o))
                    |> ignore
                | CancelPrevious ->
                    Platform.whenSettled flight (fun o -> this.Publish (gen, o))
                    |> ignore
                | FinishCurrent
                | Drop
                | DropWhilePending ->
                    queued <- 1

                    Platform.whenSettled flight (fun o -> this.Publish (gen, o))
                    |> ignore
#endif
            finally
                launching <- false

            this.NotifyIfMovedOn (prevStatus, prevFailure, prevPending)
            Tracer.RunEnd (graph, id, status)
        else
            this.RetireIfQuiet ()
            this.NotifyIfMovedOn (prevStatus, prevFailure, prevPending)
            Tracer.RunEnd (graph, id, status)

        if freshness <> Freshness.Clean then
            unannounced <- true
            graph.NoteStaleTransition ()

    member private this.StartOn() =
        this.StartOnFrom (status, failure, pendingSource)

    member private this.StartOnFrom(prevStatus: Status, prevFailure: Failure, prevPending: INode) =
        freshness <- Freshness.Clean
        unannounced <- false
        wokeInLaunch <- false
        let currentFailure = failure
        runs <- runs + 1
        Tracer.RunStart (graph, id, runs)
        sources.BeginRun ()
        phase1Active <- true
        running <- running + 1

        let mutable p1Outcome = AsyncPhase1Result.EqualKey
        let mutable p1Pending: INode = Platform.nil ()
        let mutable p1Error: exn = Platform.nil ()

        try
            try
                p1Outcome <-
#if FABLE_COMPILER
                    graph.RunHosted (this :> IComputation, fun () -> keyPhase.ReadAndCompare ())
#else
                    let saved = graph.EnterHosted (this :> IComputation)

                    try
                        let r = keyPhase.ReadAndCompare ()
                        graph.CheckRaised ()
                        r
                    finally
                        graph.LeaveHosted saved
#endif

                if violated then
                    raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))
            finally
                phase1Active <- false
                running <- running - 1

                if disposed then
                    sources.Clear (this :> IComputation)
                else
                    sources.EndRun (this :> IComputation)
        with
        | ex when violated ->
            violated <- false
            p1Outcome <- AsyncPhase1Result.Phase1Failed
            p1Error <- ScopeMessages.failure ScopeMode.Pure ex
        | NotReadyException src -> p1Pending <- src
        | ex ->
            p1Outcome <- AsyncPhase1Result.Phase1Failed
            p1Error <- ex

        if disposed then
            Tracer.RunEnd (graph, id, status)
        elif not (Platform.isNull (box p1Pending)) then
            phase1Status <- Status.Pending
            phase1PendingSource <- p1Pending
            phase1Failure <- Platform.nil ()
            this.SyncVisibleOn ()
            this.NotifyIfMovedOn (prevStatus, prevFailure, prevPending)
            Tracer.RunEnd (graph, id, status)

            if freshness <> Freshness.Clean then
                unannounced <- true
                graph.NoteStaleTransition ()
        elif p1Outcome = AsyncPhase1Result.Phase1Failed then
            let err =
                if Platform.isNull p1Error then
                    keyPhase.Phase1Exception
                else
                    p1Error

            phase1Status <- Status.Error
            phase1PendingSource <- Platform.nil ()
            phase1Failure <- graph.FailureOf (err, this, currentFailure)
            this.SyncVisibleOn ()
            this.NotifyIfMovedOn (prevStatus, prevFailure, prevPending)
            Tracer.RunEnd (graph, id, status)

            if freshness <> Freshness.Clean then
                unannounced <- true
                graph.NoteStaleTransition ()
        elif p1Outcome = AsyncPhase1Result.EqualKey then
            phase1Status <- Status.None
            phase1PendingSource <- Platform.nil ()
            phase1Failure <- Platform.nil ()
            owedTrailing <- false
            this.SyncVisibleOn ()
            this.NotifyIfMovedOn (prevStatus, prevFailure, prevPending)
            Tracer.RunEnd (graph, id, status)

            if freshness <> Freshness.Clean then
                unannounced <- true
                graph.NoteStaleTransition ()
        elif
            queued <> 0
            && (match policy with
                | FinishCurrent
                | Drop
                | DropWhilePending -> true
                | _ -> false)
        then
            phase1Status <- Status.None
            phase1PendingSource <- Platform.nil ()
            phase1Failure <- Platform.nil ()
            this.SyncVisibleOn ()
            queued <- 2

            Tracer.RunDeferred (
                graph,
                id,
                generation,
                (match policy with
                 | FlightPolicy.FinishCurrent -> true
                 | _ -> false)
            )

            this.NotifyIfMovedOn (prevStatus, prevFailure, prevPending)
            Tracer.RunEnd (graph, id, status)

            if freshness <> Freshness.Clean then
                unannounced <- true
                graph.NoteStaleTransition ()
        elif not scopeDirty then
            phase1Status <- Status.None
            phase1PendingSource <- Platform.nil ()
            phase1Failure <- Platform.nil ()
            owedTrailing <- false
            unannouncedSettle <- false
            keyPhase.CommitNextKey ()
            this.LaunchUntracked (prevStatus, prevFailure, prevPending)
        else
            scopeDirty <- false
            keyPhase.ResetKey ()
            let before = runs
            graph.Discharge scope

            if not disposed then
                if runs = before then
                    if freshness = Freshness.Dirty then
                        Tracer.RunEnd (graph, id, status)
                        this.StartOnFrom (prevStatus, prevFailure, prevPending)
                    else
                        phase1Status <- Status.None
                        phase1PendingSource <- Platform.nil ()
                        phase1Failure <- Platform.nil ()
                        owedTrailing <- false
                        unannouncedSettle <- false
                        keyPhase.CommitNextKey ()
                        this.LaunchUntracked (prevStatus, prevFailure, prevPending)
                elif freshness = Freshness.Dirty then
                    this.StartOn ()
            else
                if runs = before then
                    Tracer.RunEnd (graph, id, status)

                if freshness = Freshness.Clean then
                    unannounced <- false

    member private this.FailLaunch(gen: int, ex: exn) =
        match policy with
        | FlightPolicy.Queue when queued > 0 ->
            queued <- queued + 1
            chained <- chained + 1
            let seqNum = chained
#if !FABLE_COMPILER
            if graph.IsOnGraphThread then
                this.ApplyQueued (seqNum, gen, Platform.FlightOutcome<'T>.Faulted ex)
            else
                this.PublishQueued (seqNum, gen, Platform.FlightOutcome<'T>.Faulted ex)
#else
            this.PublishQueued (seqNum, gen, Platform.FlightOutcome<'T>.Faulted ex)
#endif
        | _ ->
            failure <- graph.FailureOf (ex, this, null)
            status <- Status.Error

    member private this.Launch() =
        freshness <- Freshness.Clean
        unannounced <- false
        sources.BeginRun ()

        if not (Platform.isNull (box pendingSource)) then
            pendingSource <- Platform.nil ()

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
        failure <- Platform.nil ()

        status <-
            Status.Pending
            ||| (status &&& Status.Uninitialized)

        let mutable flight = Platform.nil ()

        running <- running + 1

        try
            try
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
                            Platform.runFlightBodyWith (graph :> Platform.IContinuationHost) compute previous token

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
        | NotReadyException source -> pendingSource <- source
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
                        this.ApplyQueued (seqNum, gen, outcome)
                    | KeepLatest
                    | CancelPrevious ->
                        flying <- flying + 1
                        this.ApplyResult (gen, outcome)
                    | FinishCurrent
                    | Drop
                    | DropWhilePending ->
                        queued <- 1
                        this.ApplyResult (gen, outcome)
                else
                    match policy with
                    | FlightPolicy.Queue ->
                        queued <- queued + 1
                        chained <- chained + 1
                        let seqNum = chained
                        Platform.attachSettled flight (this :> Platform.ISettledWaiter<'T>) gen seqNum
                    | KeepLatest
                    | CancelPrevious ->
                        flying <- flying + 1
                        Platform.attachSettled flight (this :> Platform.ISettledWaiter<'T>) gen 0
                    | FinishCurrent
                    | Drop
                    | DropWhilePending ->
                        queued <- 1
                        Platform.attachSettled flight (this :> Platform.ISettledWaiter<'T>) gen 0
#else
                match policy with
                | FlightPolicy.Queue ->
                    queued <- queued + 1
                    chained <- chained + 1
                    let seqNum = chained

                    Platform.whenSettled flight (fun o -> this.PublishQueued (seqNum, gen, o))
                    |> ignore
                | KeepLatest ->
                    flying <- flying + 1

                    Platform.whenSettled flight (fun o -> this.Publish (gen, o))
                    |> ignore
                | CancelPrevious ->
                    Platform.whenSettled flight (fun o -> this.Publish (gen, o))
                    |> ignore
                | FinishCurrent
                | Drop
                | DropWhilePending ->
                    queued <- 1

                    Platform.whenSettled flight (fun o -> this.Publish (gen, o))
                    |> ignore
#endif
            finally
                launching <- false

            Tracer.RunEnd (graph, id, status)
        else
            this.RetireIfQuiet ()
            Tracer.RunEnd (graph, id, status)

        if freshness <> Freshness.Clean then
            unannounced <- true
            graph.NoteStaleTransition ()

    member private this.EnsureCurrent() =
        if freshness <> Freshness.Clean && not disposed then
            if graph.Deferring then
                this.Refresh ()
                graph.ClearLastRaisedIfTop ()
            else
                this.Pull ()

    member private this.RefreshForRead() : bool =
        freshness <> Freshness.Clean
        && not disposed
        && (if graph.Deferring then
                this.Refresh ()
                graph.ClearLastRaisedIfTop ()
                freshness <> Freshness.Clean
            else
                this.PullWithoutSettle ())

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.SettlePullAndRead() : 'T =
        let currentStatus = status
        let currentFailure = failure
        let currentValue = value
        graph.FinishPullSettle ()
        graph.Track (this :> ISource)

        if freshness <> Freshness.Clean && not disposed then
            if isSplit then
                graph.MarkReaderCheck ()
            else
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
            if isSplit then
                graph.MarkReaderCheck ()
            else
                graph.MarkReaderDirty ()

        if StatusBits.isUnready currentStatus then
            if StatusBits.isPending currentStatus then
                Pending
            else
                Failed currentFailure.Error
        else
            Ready currentValue

    /// <summary>Brings a stale async memo current when <c>Graph.Deferring</c> is false (Theorem B12).</summary>
    member private this.Pull() =
#if !FABLE_COMPILER || !RANVIER_TRACE
        if graph.TryEnterPullFast () then
            try
                this.Refresh ()
            with _ ->
                graph.ExitPullFastOnException ()
                reraise ()

            if graph.ExitPullFast () then
                graph.FinishPullSettle ()
        else
            graph.EnterPull ()

            try
                this.Refresh ()
            finally
                graph.ExitPull ()
#else
        graph.EnterPull ()

        try
            this.Refresh ()
        finally
            graph.ExitPull ()
#endif

    member private this.PullWithoutSettle() : bool =
#if !FABLE_COMPILER || !RANVIER_TRACE
        if graph.TryEnterPullFast () then
            try
                this.Refresh ()
            with _ ->
                graph.ExitPullFastOnException ()
                reraise ()

            graph.ExitPullFast ()
            || freshness <> Freshness.Clean
        else
            graph.EnterPull ()

            try
                this.Refresh ()
            with _ ->
                graph.ExitPull ()
                reraise ()

            graph.ExitPullDeferSettle ()
            || freshness <> Freshness.Clean
#else
        graph.EnterPull ()

        try
            this.Refresh ()
        with _ ->
            graph.ExitPull ()
            reraise ()

        graph.ExitPullDeferSettle ()
        || freshness <> Freshness.Clean
#endif

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
            link <- Platform.nil ()
            this.Dispose ()

    interface IScopeHost with
        member _.CanOwn =
            not phase1Active
            && (not (Platform.isNull (box scope))
                || mode = ScopeMode.Owning)

        member _.Scope =
            if phase1Active then
                violated <- true
                raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))
            elif mode = ScopeMode.Owning then
                if Platform.isNull (box scope) then
                    scope <- new Owner (graph.Root)
                    Tracer.ScopeNew (scope, graph, id)

                    if disposed then
                        scope.Dispose ()

                scopeDirty <- true
                scope
            else
                violated <- true
                raise (InvalidOperationException (ScopeMessages.forMode mode))

#if RANVIER_TRACE
    interface ITracedNode with
        member _.TraceGraph = box graph
#endif

    interface IReactive<'T> with
        member _.Graph = graph

        member this.ReadTracked() =
            this.Value

    interface INode with
        member _.Id = id
        member _.Status = status ||| Staleness.ofFreshness freshness

    interface ISource with
        member _.AddObserver(c, slot) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if Platform.isNull observers then
                observers <- ObserverList ()
#endif
            observers.Add (c, slot)

        member _.TryAddObserver(c, slot) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if Platform.isNull observers then
                observers <- ObserverList ()
#endif
            observers.TryAdd (c, slot)

        member _.RemoveObserverAt(c, handle) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if not (Platform.isNull observers) then
                observers.RemoveAt (c, handle)
#else
            observers.RemoveAt (c, handle)
#endif

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

        member this.MarkDirty() =
            if isSplit then
                if freshness <> Freshness.Dirty then
                    let wasClean = freshness = Freshness.Clean
                    freshness <- Freshness.Dirty

                    if wasClean then
                        graph.NoteStaleTransition ()
                        this.NotifyObserversCheck ()
                    elif unannounced then
                        unannounced <- false
                        graph.NoteStaleTransition ()
                        this.NotifyObserversCheck ()
                elif unannounced then
                    unannounced <- false
                    graph.NoteStaleTransition ()
                    this.NotifyObserversCheck ()
            elif freshness <> Freshness.Dirty || unannounced then
                if freshness = Freshness.Clean || unannounced then
                    graph.NoteStaleTransition ()

                freshness <- Freshness.Dirty
                unannounced <- false
                this.NotifyObserversDirty ()

        member this.MarkCheck() =
            if freshness = Freshness.Clean then
                freshness <- Freshness.Check
                unannounced <- false
                this.NotifyObserversCheck ()
            elif unannounced then
                unannounced <- false
                graph.NoteStaleTransition ()
                this.NotifyObserversCheck ()

    member this.Dispose() =
        graph.Entered (
            "Disposing a node",
            fun () ->
                if not disposed then
                    disposed <- true
                    Tracer.NodeDispose (graph, id)

                    if not (Platform.isNull link) then
                        link.Detach ()
                        link <- Platform.nil ()

                    this.DetachSources ()

                    if not (Platform.isNull (box scope)) then
                        scopeDirty <- false
                        graph.Retire scope

                    if not (Platform.isNull cts) then
                        let source = cts
                        cts <- Platform.nil ()
                        source.Cancel ()
                        source.Dispose ()

                    if StatusBits.isPending status then
                        pendingSource <- Platform.nil ()
                        phase1PendingSource <- Platform.nil ()
                        phase1Status <- Status.None
                        owedTrailing <- false

                        failure <- Failure (ObjectDisposedException (this.GetType().Name), (this :> INode))
                        status <- Status.Error
                        this.Wake ()

#if FABLE_COMPILER
                    if not (Platform.isNull waiters) then
                        let settled = this.LastSettled ()

                        for waiter in waiters.Values do
                            Platform.resolve waiter settled

                        waiters <- Platform.nil ()
#else
                    lock this (fun () ->
                        if not (Platform.isNull waiters) then
                            let settled = this.LastSettled ()

                            for waiter in waiters.Values do
                                Platform.resolve waiter settled

                            waiters <- null)
#endif
        )

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

    interface IPreviousSource<'T> with
        member this.Settled position =
#if FABLE_COMPILER
            if disposed || applied >= position then
                this.SettledNow ()
            else
                if Platform.isNull waiters then
                    waiters <- Dictionary<int, Platform.Deferred<'T voption>>()

                let waiter = Platform.findOrDefault waiters position

                if Platform.isNull (box waiter) then
                    let waiter = Platform.deferred<'T voption>()
                    waiters[position] <- waiter
                    Platform.deferredTask waiter
                else
                    Platform.deferredTask waiter
#else
            lock this (fun () ->
                if disposed || applied >= position then
                    this.SettledNow ()
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
#endif

    member _.Status = status

    member internal _.PendingSource = pendingSource

    member _.PendingSources =
        if Platform.isNull (box pendingSource) then
            Seq.empty
        else
            [| pendingSource |] :> seq<INode>

    member _.Runs = runs

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
        if this.RefreshForRead () then
            this.SettlePullAndRead ()
        else
            graph.Track (this :> ISource)

            if StatusBits.isUnready status then
                if StatusBits.isPending status then
                    raise (graph.NotReady (this :> INode))

                graph.Raise failure

            value

#if FABLE_COMPILER
    member this.TryValue: Reading<'T> =
        if StatusBits.isCleanAndReady freshness status then
            graph.Track (this :> ISource)
            Ready value
        else
            this.ReadTrySlow ()
#else
    member this.TryValue: Reading<'T> = this.GetTryFast ()

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
        if this.RefreshForRead () then
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

    member _.ErrorOrigin: INode = Failure.OriginOf failure

    member _.Peek = value

    member internal _.Graph = graph

    member inline internal _.Failure = failure
    member inline internal _.IsSplit = isSplit

// Public constructors live in an intrinsic augmentation to avoid `init@` checks on every member.
#if !FABLE_COMPILER
type AsyncMemo<'T> with
    /// <summary>A pure async memo over <c>compute</c>.</summary>
    new(graph: Graph, compute: Func<Previous<'T>, CancellationToken, Task<'T>>) as this =
        new AsyncMemo<'T> (
            graph,
            (fun previous token -> compute.Invoke (previous, token)),
            Platform.nil (),
            ScopeMode.PureAsync,
            graph.Options.FlightPolicy
        )

        then this.Attach ()

    /// <summary>An owning async memo over <c>compute</c> when <c>owning</c> is true, a pure one otherwise.</summary>
    new(graph: Graph, compute: Func<Previous<'T>, CancellationToken, Task<'T>>, owning: bool) as this =
        new AsyncMemo<'T> (
            graph,
            (fun previous token -> compute.Invoke (previous, token)),
            Platform.nil (),
            (if owning then ScopeMode.Owning else ScopeMode.PureAsync),
            graph.Options.FlightPolicy
        )

        then this.Attach ()
#endif

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
    let hasFallback = onPending.IsSome
    let hasRecover = onError.IsSome

    let fallbackFn: 'T voption -> 'T =
        match onPending with
        | ValueSome f -> f
        | ValueNone -> Platform.nil ()

#if FABLE_COMPILER
    let recoverFn: exn -> 'T voption -> 'T =
        match onError with
        | ValueSome f -> f
        | ValueNone -> Platform.nil ()
#else
    let recoverFn: OptimizedClosures.FSharpFunc<exn, 'T voption, 'T> =
        match onError with
        | ValueSome f -> OptimizedClosures.FSharpFunc<exn, 'T voption, 'T>.Adapt f
        | ValueNone -> Platform.nil ()
#endif
#if !FABLE_COMPILER && !RANVIER_TRACE
    let mutable observers = ObserverList.Create ()
#else
#if FABLE_COMPILER && !RANVIER_TRACE
    let mutable observers: ObserverList = Platform.nil ()
#else
    let observers = ObserverSet ()
    do Tracer.Bind (observers, graph, id)
#endif
#endif
    let mutable sources = SourceList.Create ()
    do Tracer.Bind (sources, graph, id)
    let mutable pendingFirst: INode = Platform.nil ()
    let mutable pendingSecond: INode = Platform.nil ()

    let mutable freshness = Freshness.Dirty
    let mutable status = Status.Uninitialized
    let mutable value = Unchecked.defaultof<'T>
    let mutable failure: Failure = Platform.nil ()
    let mutable caught: Failure = Platform.nil ()
    let mutable waiting = false
    let mutable runs = 0
    let mutable active = 0
    let mutable disposed = false
    let mutable link: OwnerLink = Platform.nil ()

    /// <summary>Set when a run ends stale, as on <c>Memo</c>. The next mark notifies the observers and clears it.</summary>
    let mutable unannounced = false

    /// <summary>True once a run has produced a value: the body's, a fallback's or a recover's.</summary>
    let mutable shown = false

    /// <summary>
    /// Owner of the nodes the body creates, allocated by the first of them.
    /// Discharged before each run and disposed with the computation.
    /// </summary>
    let mutable scope: Owner = Platform.nil ()

    member inline private _.NotifyObserversCheck() =
#if FABLE_COMPILER && !RANVIER_TRACE
        if not (Platform.isNull observers) then
            if
                observers.Count = 1
                && EdgeHandle.isNodeSlot observers.FirstSlot
            then
                NodeOps.MarkCheck (Platform.fastCast<Node> observers.FirstRef)
            else
                observers.NotifyCheck ()
#else
        observers.NotifyCheck ()
#endif

    /// <summary>A boundary over <c>body</c>, owned by the current owner.</summary>
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

    member private this.Attach() =
        let owner = graph.CurrentOwner
        Tracer.BoundaryNew (graph, id, owner)
        link <- owner.AttachLinked this

    member private this.DetachSources() =
        sources.Clear (this :> IComputation)

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

    member private this.Suspend(source: INode) : 'T =
        graph.ClearRaised ()
        this.RecordPending source
        status <- Status.Pending
        value

    member private this.RecoverOrFail(ex: exn, last: 'T voption, previousFailure: Failure, previousCaught: Failure) : 'T =
        graph.ClearRaised ()

        if hasRecover then
            let from = graph.FailureOf (ex, this, previousCaught)

            try
#if FABLE_COMPILER
                let recovered = recoverFn ex last
#else
                let recovered = recoverFn.Invoke (ex, last)
#endif
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
        else
            failure <- graph.FailureOf (ex, this, previousFailure)
            status <- Status.Error
            value

    member private this.Run() =
        freshness <- Freshness.Clean
        unannounced <- false
        sources.BeginRun ()

        if not (Platform.isNull (box pendingFirst)) then
            pendingFirst <- Platform.nil ()

            if not (Platform.isNull (box pendingSecond)) then
                pendingSecond <- Platform.nil ()

        let previous = value
        let previousStatus = status
        let previousWaiting = waiting
        let previousFailure = failure
        let previousCaught = caught

        status <- Status.None
        failure <- Platform.nil ()
        caught <- Platform.nil ()
        waiting <- false
        runs <- runs + 1
        Tracer.RunStart (graph, id, runs)

        active <- 1

#if FABLE_COMPILER
        let prevCurrent = graph.HostedCurrent
        let prevNode = graph.HostedNode
        let prevOwner = graph.HostedOwner
        let prevPending = graph.HostedPending
        let prevRaised = graph.HostedRaised
        graph.BeginHosted (this :> IComputation)
#else
        let saved = graph.EnterHosted (this :> IComputation)
#endif

        try
            try
                let result = body ()
                graph.CheckRaised ()
                value <- result
            with
            | NotReadyException source ->
                graph.ClearRaised ()
                this.RecordPending source

                if hasFallback then
                    let last = if shown then ValueSome previous else ValueNone

                    try
                        let substitute = fallbackFn last
                        graph.CheckRaised ()
                        waiting <- true
                        value <- substitute
                    with
                    | NotReadyException inner -> value <- this.Suspend inner
                    | ex -> value <- this.RecoverOrFail (ex, last, previousFailure, previousCaught)
                else
                    value <- this.Suspend source

            | ex ->
                let last = if shown then ValueSome previous else ValueNone
                value <- this.RecoverOrFail (ex, last, previousFailure, previousCaught)
        finally
#if FABLE_COMPILER
            graph.EndHosted (prevCurrent, prevNode, prevOwner, prevPending, prevRaised)
#else
            graph.LeaveHosted saved
#endif
            active <- 0

            if disposed then
                sources.Clear (this :> IComputation)
            else
                sources.EndRun (this :> IComputation)

        let mutable moved =
            status <> previousStatus
            || waiting <> previousWaiting
            || (not (obj.ReferenceEquals (failure, previousFailure))
                && Failure.Moved (failure, previousFailure))
            || (not (obj.ReferenceEquals (caught, previousCaught))
                && Failure.Moved (caught, previousCaught))

        if not moved then
            try
                moved <- not (ComparerSentinel.equals equal previous value)
            with ex ->
                value <- previous
                failure <- graph.FailureOf (ex, this, previousFailure)
                caught <- Platform.nil ()
                waiting <- false
                status <- Status.Error
                moved <- true

        if status = Status.None then
            shown <- true

        if moved then
            Tracer.Moved (graph, id, Failure.Payload (failure, box value))
#if !FABLE_COMPILER && !RANVIER_TRACE
            graph.NotifyMovedList &observers
#else
#if FABLE_COMPILER && !RANVIER_TRACE
            if
                not (Platform.isNull observers)
                && observers.Count <> 0
            then
                if
                    observers.Count = 1
                    && EdgeHandle.isNodeSlot observers.FirstSlot
                    && graph.StaleTransitions = 0
                then
                    let obsNode = Platform.fastCast<Node> observers.FirstRef

                    if not (obj.ReferenceEquals (obsNode, graph.CurrentNode)) then
                        NodeOps.MarkDirty obsNode
                else
                    graph.NotifyMovedList observers
#else
            graph.NotifyMoved observers
#endif
#endif
            Tracer.Notified graph
            Tracer.RunEnd (graph, id, status)

            if graph.QueuePending then
                graph.OweFlush ()
        else
            Tracer.RunEnd (graph, id, status)

        if freshness <> Freshness.Clean then
            unannounced <- true
            graph.NoteStaleTransition ()

    member private this.EnsureCurrent() =
        if freshness <> Freshness.Clean && not disposed then
            if graph.Deferring then
                this.Refresh ()
                graph.ClearLastRaisedIfTop ()
            else
                this.Pull ()

    member private this.RefreshForRead() : bool =
        freshness <> Freshness.Clean
        && not disposed
        && (if graph.Deferring then
                this.Refresh ()
                graph.ClearLastRaisedIfTop ()
                freshness <> Freshness.Clean
            else
                this.PullWithoutSettle ())

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

    /// <summary>Brings a stale boundary current when <c>Graph.Deferring</c> is false (Theorem B12).</summary>
    member private this.Pull() =
#if !FABLE_COMPILER || !RANVIER_TRACE
        if graph.TryEnterPullFast () then
            try
                this.Refresh ()
            with _ ->
                graph.ExitPullFastOnException ()
                reraise ()

            if graph.ExitPullFast () then
                graph.FinishPullSettle ()
        else
            graph.EnterPull ()

            try
                this.Refresh ()
            finally
                graph.ExitPull ()
#else
        graph.EnterPull ()

        try
            this.Refresh ()
        finally
            graph.ExitPull ()
#endif

    member private this.PullWithoutSettle() : bool =
#if !FABLE_COMPILER || !RANVIER_TRACE
        if graph.TryEnterPullFast () then
            try
                this.Refresh ()
            with _ ->
                graph.ExitPullFastOnException ()
                reraise ()

            graph.ExitPullFast ()
            || freshness <> Freshness.Clean
        else
            graph.EnterPull ()

            try
                this.Refresh ()
            with _ ->
                graph.ExitPull ()
                reraise ()

            graph.ExitPullDeferSettle ()
            || freshness <> Freshness.Clean
#else
        graph.EnterPull ()

        try
            this.Refresh ()
        with _ ->
            graph.ExitPull ()
            reraise ()

        graph.ExitPullDeferSettle ()
        || freshness <> Freshness.Clean
#endif

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

    member this.Dispose() =
        graph.Entered (
            "Disposing a node",
            fun () ->
                if not disposed then
                    disposed <- true
                    Tracer.NodeDispose (graph, id)

                    if not (Platform.isNull link) then
                        link.Detach ()
                        link <- Platform.nil ()

                    this.DetachSources ()

                    if not (Platform.isNull (box scope)) then
                        graph.Retire scope
        )

    static member Suspense(graph: Graph, body: Func<'T>, fallback: Func<'T voption, 'T>) =
        Boundary<'T>.Create(graph, body.Invoke, ValueSome fallback.Invoke, ValueNone)

    static member Errors(graph: Graph, body: Func<'T>, recover: Func<exn, 'T voption, 'T>) =
        Boundary<'T>.Create(graph, body.Invoke, ValueNone, ValueSome (fun ex previous -> recover.Invoke (ex, previous)))

    static member Catching(graph: Graph, body: Func<'T>, fallback: Func<'T voption, 'T>, recover: Func<exn, 'T voption, 'T>) =
        Boundary<'T>.Create(graph, body.Invoke, ValueSome fallback.Invoke, ValueSome (fun ex previous -> recover.Invoke (ex, previous)))

    static member Suspense(graph: Graph, body: Func<'T>, seed: 'T, fallback: Func<'T, 'T>) =
        Boundary<'T>.Create(graph, body.Invoke, ValueSome (fun previous -> fallback.Invoke (ValueOption.defaultValue seed previous)), ValueNone)

    static member Errors(graph: Graph, body: Func<'T>, seed: 'T, recover: Func<exn, 'T, 'T>) =
        Boundary<'T>.Create(graph, body.Invoke, ValueNone, ValueSome (fun ex previous -> recover.Invoke (ex, ValueOption.defaultValue seed previous)))

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
            link <- Platform.nil ()
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

    interface IReactive<'T> with
        member _.Graph = graph

        member this.ReadTracked() =
            this.Value

    interface INode with
        member _.Id = id
        member _.Status = status ||| Staleness.ofFreshness freshness

    interface ISource with
        member _.AddObserver(c, slot) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if Platform.isNull observers then
                observers <- ObserverList ()
#endif
            observers.Add (c, slot)

        member _.TryAddObserver(c, slot) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if Platform.isNull observers then
                observers <- ObserverList ()
#endif
            observers.TryAdd (c, slot)

        member _.RemoveObserverAt(c, handle) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if not (Platform.isNull observers) then
                observers.RemoveAt (c, handle)
#else
            observers.RemoveAt (c, handle)
#endif

        member this.UpdateIfNecessary() =
            if
                freshness = Freshness.Dirty
                && not disposed
                && active = 0
                && Platform.isNull (box scope)
                && graph.Deferring
            then
                this.Run ()
                graph.ClearLastRaisedIfTop ()
            else
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

        member this.MarkDirty() =
            if freshness <> Freshness.Dirty then
                let wasClean = freshness = Freshness.Clean
                freshness <- Freshness.Dirty

                if wasClean then
                    graph.NoteStaleTransition ()
                    this.NotifyObserversCheck ()
                elif unannounced then
                    unannounced <- false
                    graph.NoteStaleTransition ()
                    this.NotifyObserversCheck ()
            elif unannounced then
                unannounced <- false
                graph.NoteStaleTransition ()
                this.NotifyObserversCheck ()

        member this.MarkCheck() =
            if freshness = Freshness.Clean then
                freshness <- Freshness.Check
                this.NotifyObserversCheck ()
            elif unannounced then
                unannounced <- false
                graph.NoteStaleTransition ()
                this.NotifyObserversCheck ()

    member _.Status = status

    member this.IsWaiting =
        if this.RefreshForRead () then
            this.SettlePullAndReadWaiting ()
        else
            graph.Track (this :> ISource)
            waiting

    member this.Caught =
        if this.RefreshForRead () then
            Failure.ErrorOf (this.SettlePullAndReadCaught ())
        else
            graph.Track (this :> ISource)
            Failure.ErrorOf caught

    member this.CaughtFrom: INode =
        if this.RefreshForRead () then
            Failure.OriginOf (this.SettlePullAndReadCaught ())
        else
            graph.Track (this :> ISource)
            Failure.OriginOf caught

    member _.PendingSources =
        if Platform.isNull (box pendingFirst) then
            Seq.empty
        elif Platform.isNull (box pendingSecond) then
            [| pendingFirst |] :> seq<INode>
        else
            [| pendingFirst; pendingSecond |] :> seq<INode>

    member _.Runs = runs

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
        if this.RefreshForRead () then
            this.SettlePullAndRead ()
        else
            graph.Track (this :> ISource)

            if StatusBits.isUnready status then
                if StatusBits.isPending status then
                    raise (graph.NotReady (this :> INode))

                graph.Raise failure

            value

#if FABLE_COMPILER
    member this.TryValue: Reading<'T> =
        if StatusBits.isCleanAndReady freshness status then
            graph.Track (this :> ISource)
            Ready value
        else
            this.ReadTrySlow ()
#else
    member this.TryValue: Reading<'T> = this.GetTryFast ()

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
        if this.RefreshForRead () then
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

    member _.ErrorOrigin: INode = Failure.OriginOf failure

    member _.Peek = value

    member internal _.Graph = graph

    member inline internal _.Failure = failure

// -----------------------------------------------------------------------------------------------------------------
// Companion Combinator Runners & Unobserved MapFusion (Theorems B18, B19, B20)
// -----------------------------------------------------------------------------------------------------------------

/// <summary>Polymorphic zero-throw source adapter for <c>map</c>/<c>map2</c>/<c>zip</c> over non-signal sources.</summary>
[<AbstractClass; AllowNullLiteral>]
type internal CombinatorSource<'T>() =
    abstract Graph: Graph
    abstract Source: ISource
    abstract Node: INode
    abstract RefreshAndCheckStale: n: MemoNode -> unit
    abstract MarkStaleAfterPreRefresh: n: MemoNode -> unit
    abstract Status: Status
    abstract Peek: 'T
    abstract Failure: Failure

[<Sealed>]
type internal MemoSource<'T>(memo: Memo<'T>) =
    inherit CombinatorSource<'T>()

    override _.Graph = memo.Graph

    override _.Source =
#if !RANVIER_TRACE
        memo.EnsureMaterializedIfClaimed ()
#endif
        memo.Node :> ISource

    override _.Node = memo :> INode

    override _.RefreshAndCheckStale(n: MemoNode) =
#if !RANVIER_TRACE
        memo.EnsureMaterializedIfClaimed ()
#endif
        NodeOps.EnsureCurrent memo.Node

        if not n.IsDisposed && (memo.Node :> ISource).IsStale then
            n.Graph.NoteStaleTransition ()
            NodeOps.MemoMarkCheck n

    override _.MarkStaleAfterPreRefresh(n: MemoNode) =
        if not n.IsDisposed && (memo.Node :> ISource).IsStale then
            n.Graph.NoteStaleTransition ()
            NodeOps.MemoMarkCheck n

    override _.Status = memo.Node.Status
    override _.Peek = memo.PeekRaw
    override _.Failure = memo.Node.FailureField

[<Sealed>]
type internal AsyncSourceAdapter<'T>(src: AsyncSource<'T>) =
    inherit CombinatorSource<'T>()

    override _.Graph = src.Graph
    override _.Source = src :> ISource
    override _.Node = src :> INode
    override _.RefreshAndCheckStale(_n: MemoNode) = ()
    override _.MarkStaleAfterPreRefresh(_n: MemoNode) = ()
    override _.Status = src.Status
    override _.Peek = src.Peek
    override _.Failure = src.Failure

[<Sealed>]
type internal AsyncMemoAdapter<'T>(src: AsyncMemo<'T>) =
    inherit CombinatorSource<'T>()

    override _.Graph = src.Graph
    override _.Source = src :> ISource
    override _.Node = src :> INode

    override _.RefreshAndCheckStale(n: MemoNode) =
        (src :> ISource).UpdateIfNecessary()

        if not n.IsDisposed && (src :> ISource).IsStale then
            n.Graph.NoteStaleTransition ()

            if src.IsSplit then
                NodeOps.MemoMarkCheck n
            else
                NodeOps.MemoMarkDirty n

    override _.MarkStaleAfterPreRefresh(n: MemoNode) =
        if not n.IsDisposed && (src :> ISource).IsStale then
            n.Graph.NoteStaleTransition ()

            if src.IsSplit then
                NodeOps.MemoMarkCheck n
            else
                NodeOps.MemoMarkDirty n

    override _.Status = src.Status
    override _.Peek = src.Peek
    override _.Failure = src.Failure

[<Sealed>]
type internal BoundaryAdapter<'T>(src: Boundary<'T>) =
    inherit CombinatorSource<'T>()

    override _.Graph = src.Graph
    override _.Source = src :> ISource
    override _.Node = src :> INode

    override _.RefreshAndCheckStale(n: MemoNode) =
        (src :> ISource).UpdateIfNecessary()

        if not n.IsDisposed && (src :> ISource).IsStale then
            n.Graph.NoteStaleTransition ()
            NodeOps.MemoMarkCheck n

    override _.MarkStaleAfterPreRefresh(n: MemoNode) =
        if not n.IsDisposed && (src :> ISource).IsStale then
            n.Graph.NoteStaleTransition ()
            NodeOps.MemoMarkCheck n

    override _.Status = src.Status
    override _.Peek = src.Peek
    override _.Failure = src.Failure

module internal CombinatorHelpers =
    let inline clearActive (n: MemoNode) =
#if !RANVIER_TRACE
        n.Flags <- n.Flags &&& ~~~NodeFlags.ActiveMask
#else
#if FABLE_COMPILER
        n.Active <- 0uy
#else
        n.ActiveField <- 0uy
#endif
#endif

    let inline isViolated (n: MemoNode) =
#if !RANVIER_TRACE
        (n.Flags &&& NodeFlags.Violated) <> 0
#else
#if FABLE_COMPILER
        n.Violated
#else
        n.ViolatedField
#endif
#endif

    let inline clearViolated (n: MemoNode) =
#if !RANVIER_TRACE
        n.Flags <- n.Flags &&& ~~~NodeFlags.Violated
#else
#if FABLE_COMPILER
        n.Violated <- false
#else
        n.ViolatedField <- false
#endif
#endif

    let inline setViolated (n: MemoNode) =
#if !RANVIER_TRACE
        n.Flags <- n.Flags ||| NodeFlags.Violated
#else
#if FABLE_COMPILER
        n.Violated <- true
#else
        n.ViolatedField <- true
#endif
#endif

    let inline setPublished (n: MemoNode) =
#if !RANVIER_TRACE
        n.Flags <- n.Flags ||| NodeFlags.Published
#else
#if FABLE_COMPILER
        n.Published <- true
#else
        n.PublishedField <- true
#endif
#endif

    let inline statusMoved (n: MemoNode, previousStatus: Status, previousFailure: Failure) =
        n.Status <> previousStatus
        || (not (obj.ReferenceEquals (n.FailureField, previousFailure))
            && Failure.Moved (n.FailureField, previousFailure))

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    let restoreDisposedState (n: MemoNode, previousStatus: Status, previousFailure: Failure, previousPending: INode) =
        clearActive n
        n.SourcesField.Clear (n :> IComputation)

        if StatusBits.isError previousStatus then
            n.FailureField <- previousFailure
            n.SetStatusError ()
        elif StatusBits.isPending previousStatus then
            n.PendingSourceField <- previousPending
            n.SetStatusPending ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    let recordCallbackFailure (g: Graph, n: MemoNode, origin: INode, pendingMsg: string, ex: exn, previousFailure: Failure) : Failure =
        clearActive n

        let err =
            if isViolated n then
                clearViolated n
                ScopeMessages.failure ScopeMode.Pure ex
            else
                UntrackedPhase.wrapPending pendingMsg ex

        let reusablePrev =
            if
                not (Platform.isNull previousFailure)
                && obj.ReferenceEquals (previousFailure.Origin, origin)
            then
                previousFailure
            else
                Platform.nil ()

        let f = g.FailureOf (err, origin, reusablePrev)
        n.PendingSourceField <- Platform.nil ()
        n.FailureField <- f
        n.SetStatusError ()
        f

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    let recordFusedFailure (g: Graph, n: MemoNode, origin: INode, isComparer: bool, ex: exn, previousFailure: Failure) : unit =
        clearActive n

        let err =
            if isComparer then
                if isViolated n then
                    clearViolated n

                ex
            elif isViolated n then
                clearViolated n
                ScopeMessages.failure ScopeMode.Pure ex
            else
                UntrackedPhase.wrapPending UntrackedPhase.mapPending ex

        let reusablePrev =
            if
                not (Platform.isNull previousFailure)
                && obj.ReferenceEquals (previousFailure.Origin, origin)
            then
                previousFailure
            else
                Platform.nil ()

        n.PendingSourceField <- Platform.nil ()
        n.FailureField <- g.FailureOf (err, origin, reusablePrev)
        n.SetStatusError ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    let compareOutputGuarded
        (
            runner: MemoOnRunner<'U>,
            g: Graph,
            n: MemoNode,
            origin: INode,
            outputEqual: IEqualityComparer<'U>,
            previous: 'U,
            result: 'U,
            previousFailure: Failure
        ) : 'U =
        try
            let changed = not (ComparerSentinel.equals outputEqual previous result)
            clearActive n
            setPublished n
            runner.Moved <- changed
            result
        with ex ->
            clearActive n
            n.PendingSourceField <- Platform.nil ()
            n.FailureField <- g.FailureOf (ex, origin, previousFailure)
            n.SetStatusError ()
            runner.Moved <- true
            previous

    let inline finishReady
        (
            runner: MemoOnRunner<'U>,
            g: Graph,
            n: MemoNode,
            origin: INode,
            outputEqual: IEqualityComparer<'U>,
            previous: 'U,
            result: 'U,
            previousStatus: Status,
            previousFailure: Failure
        ) : 'U =
        n.PendingSourceField <- Platform.nil ()
        n.FailureField <- Platform.nil ()
        n.SetStatusNone ()

        if previousStatus <> Status.None then
            clearActive n
            setPublished n
            runner.Moved <- true
            result
        elif SafeEquality.isSafe outputEqual then
            clearActive n
            setPublished n

            runner.Moved <-
#if !RANVIER_TRACE
                (n.ObserverCount <> 0 || g.QueuePending)
                && not (ComparerSentinel.equals outputEqual previous result)
#else
                not (ComparerSentinel.equals outputEqual previous result)
#endif
            result
        else
            compareOutputGuarded (runner, g, n, origin, outputEqual, previous, result, previousFailure)

    let markStaleAfterRefresh (n: MemoNode, isSplitOrMemo: bool) =
        n.Graph.NoteStaleTransition ()

        if isSplitOrMemo then
            NodeOps.MemoMarkCheck n
        else
            NodeOps.MemoMarkDirty n

    let inline refreshMemoNode (n: MemoNode, target: MemoNode) =
        NodeOps.EnsureCurrent target

        if not n.IsDisposed && (target :> ISource).IsStale then
            n.Graph.NoteStaleTransition ()
            NodeOps.MemoMarkCheck n

    let inline refreshStaticSlot (g: Graph, n: MemoNode, slotIdx: int, [<InlineIfLambda>] refresh: unit -> unit) =
        n.SourcesField.SetCursor slotIdx
#if FABLE_COMPILER
        g.RunStaticRefresh (n, refresh)
#else
        let saved = g.EnterStaticRefresh n

        try
            refresh ()
        finally
            g.LeaveStaticRefresh saved
#endif
        if not n.IsDisposed then
            n.SourcesField.SetCursor n.SourcesField.Count

    let inline refreshStaticPair
        (g: Graph, n: MemoNode, sameSource: bool, [<InlineIfLambda>] refresh1: unit -> unit, [<InlineIfLambda>] refresh2: unit -> unit)
        =
        n.SourcesField.SetCursor 0
#if FABLE_COMPILER
        g.RunStaticRefresh (
            n,
            fun () ->
                refresh1 ()

                if not sameSource && not n.IsDisposed then
                    n.SourcesField.SetCursor 1
                    refresh2 ()
        )
#else
        let saved = g.EnterStaticRefresh n

        try
            refresh1 ()

            if not sameSource && not n.IsDisposed then
                n.SourcesField.SetCursor 1
                refresh2 ()
        finally
            g.LeaveStaticRefresh saved
#endif
        if not n.IsDisposed then
            n.SourcesField.SetCursor n.SourcesField.Count

#if !FABLE_COMPILER
/// <summary>Cached adapted pair constructor so <c>zip</c> allocates zero per-call mapping or adapter closures on .NET.</summary>
type internal ZipPair<'A, 'B>() =
    static member val Adapted: Map2Delegate<'A, 'B, 'A * 'B> = OptimizedClosures.FSharpFunc<'A, 'B, 'A * 'B>.Adapt(fun a b -> a, b)
#endif

/// <summary>Static 1-source <c>Signal.map</c> runner for traced or journaled graphs.</summary>
[<Sealed>]
type internal SignalMapState<'T, 'U>(signal: Signal<'T>, mapping: 'T -> 'U) =
    inherit MemoOnRunner<'U>()

    let mutable linked = false

    override this.Execute(origin, n, outputEqual, previous, previousStatus, previousFailure, previousPending) =
        let g = n.Graph

        if not linked then
            n.SourcesField.BeginRun ()
            NodeOps.AddSource (n, signal :> ISource)

            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                linked <- true

        if n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
            this.Moved <- false
            previous
        else
            if g.HasLastRaised then
                g.ClearLastRaised ()

            let input = signal.Peek
            let mutable result = Unchecked.defaultof<'U>

#if FABLE_COMPILER
            let mutable callEx: exn = Platform.nil ()

            try
                result <- g.RunUntrackedPureArg (n :> IScopeHost, mapping, input)

                if CombinatorHelpers.isViolated n then
                    raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))
            with ex ->
                callEx <- Thrown.hold ex

            if not (Platform.isNull callEx) then
                CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.mapPending, Thrown.take callEx, previousFailure)
                |> ignore

                this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                previous
#else
            let hasJournal = not (Platform.isNull g.Journal)

            if hasJournal then
                g.Journal.EnterDerived ()

            let saved = g.EnterUntrackedPure (n :> IScopeHost)

            let callEx =
                try
                    result <- mapping input

                    if CombinatorHelpers.isViolated n then
                        raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                    null
                with ex ->
                    ex

            g.LeaveUntrackedPure saved

            if hasJournal then
                g.Journal.LeaveDerived ()

            if not (Platform.isNull callEx) then
                CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.mapPending, callEx, previousFailure)
                |> ignore

                this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                previous
#endif
            else
                CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, result, previousStatus, previousFailure)

/// <summary>Static 1-source <c>map</c> runner over <c>CombinatorSource</c> for traced or journaled graphs.</summary>
[<Sealed>]
type internal SourceMapState<'T, 'U>(src: CombinatorSource<'T>, mapping: 'T -> 'U) =
    inherit MemoOnRunner<'U>()

    let mutable linked = false

    override this.Execute(origin, n, outputEqual, previous, previousStatus, previousFailure, previousPending) =
        let g = n.Graph

        if not linked then
            n.SourcesField.BeginRun ()
            NodeOps.AddSource (n, src.Source)

            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                linked <- true

        if not n.IsDisposed then
            CombinatorHelpers.refreshStaticSlot (g, n, 0, fun () -> src.RefreshAndCheckStale n)

        if n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
            this.Moved <- false
            previous
        else
            let st = src.Status

            if StatusBits.isUnready st then
                CombinatorHelpers.clearActive n

                if StatusBits.isPending st then
                    let pendingNode = src.Node
                    Tracer.Suspend (g, n, pendingNode)
                    n.PendingSourceField <- pendingNode
                    n.SetStatusPending ()
                else
                    n.FailureField <- src.Failure
                    n.SetStatusError ()

                this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                previous
            else
                if g.HasLastRaised then
                    g.ClearLastRaised ()

                let input = src.Peek
                let mutable result = Unchecked.defaultof<'U>

#if FABLE_COMPILER
                let mutable callEx: exn = Platform.nil ()

                try
                    result <- g.RunUntrackedPureArg (n :> IScopeHost, mapping, input)

                    if CombinatorHelpers.isViolated n then
                        raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))
                with ex ->
                    callEx <- Thrown.hold ex

                if not (Platform.isNull callEx) then
                    CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.mapPending, Thrown.take callEx, previousFailure)
                    |> ignore

                    this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                    previous
#else
                let hasJournal = not (Platform.isNull g.Journal)

                if hasJournal then
                    g.Journal.EnterDerived ()

                let saved = g.EnterUntrackedPure (n :> IScopeHost)

                let callEx =
                    try
                        result <- mapping input

                        if CombinatorHelpers.isViolated n then
                            raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                        null
                    with ex ->
                        ex

                g.LeaveUntrackedPure saved

                if hasJournal then
                    g.Journal.LeaveDerived ()

                if not (Platform.isNull callEx) then
                    CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.mapPending, callEx, previousFailure)
                    |> ignore

                    this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                    previous
#endif
                else
                    CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, result, previousStatus, previousFailure)

/// <summary>Static 2-source <c>Signal.map2</c> / <c>Signal.zip</c> runner (Theorem B19).</summary>
[<Sealed>]
type internal SignalMap2State<'A, 'B, 'C>(s1: Signal<'A>, s2: Signal<'B>, mapping: Map2Delegate<'A, 'B, 'C>) =
    inherit MemoOnRunner<'C>()

    let sameSource = obj.ReferenceEquals (box s1, box s2)
    let mutable linked = false

    override this.Execute(origin, n, outputEqual, previous, previousStatus, previousFailure, previousPending) =
        let g = n.Graph

        if not linked then
            n.SourcesField.BeginRun ()
            NodeOps.AddSource (n, s1 :> ISource)

            if not sameSource then
                NodeOps.AddSource (n, s2 :> ISource)

            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                linked <- true

        if n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
            this.Moved <- false
            previous
        else
            if g.HasLastRaised then
                g.ClearLastRaised ()

            let a = s1.Peek
            let b = s2.Peek
            let mutable result = Unchecked.defaultof<'C>

#if FABLE_COMPILER
            let mutable callEx: exn = Platform.nil ()

            try
                result <- g.RunUntrackedPure2 (n :> IScopeHost, mapping, a, b)

                if CombinatorHelpers.isViolated n then
                    raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))
            with ex ->
                callEx <- Thrown.hold ex

            if not (Platform.isNull callEx) then
                CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.mapPending, Thrown.take callEx, previousFailure)
                |> ignore

                this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                previous
#else
            let hasJournal = not (Platform.isNull g.Journal)

            if hasJournal then
                g.Journal.EnterDerived ()

            let saved = g.EnterUntrackedPure (n :> IScopeHost)

            let callEx =
                try
                    result <- mapping.Invoke (a, b)

                    if CombinatorHelpers.isViolated n then
                        raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                    null
                with ex ->
                    ex

            g.LeaveUntrackedPure saved

            if hasJournal then
                g.Journal.LeaveDerived ()

            if not (Platform.isNull callEx) then
                CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.mapPending, callEx, previousFailure)
                |> ignore

                this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                previous
#endif
            else
                CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, result, previousStatus, previousFailure)

/// <summary>Static 2-source <c>Memo.map2</c> / <c>Memo.zip</c> runner with zero adapter allocation (Theorem B19).</summary>
[<Sealed>]
type internal MemoMap2State<'A, 'B, 'C>(m1: Memo<'A>, m2: Memo<'B>, mapping: Map2Delegate<'A, 'B, 'C>) =
    inherit MemoOnRunner<'C>()

    let sameSource = obj.ReferenceEquals (box m1, box m2)
    let mutable linked = false

    override this.Execute(origin, n, outputEqual, previous, previousStatus, previousFailure, previousPending) =
        let g = n.Graph
#if !RANVIER_TRACE
        m1.EnsureMaterializedIfClaimed ()

        if not sameSource then
            m2.EnsureMaterializedIfClaimed ()
#endif

        if not linked then
            n.SourcesField.BeginRun ()
            NodeOps.AddSource (n, m1.Node :> ISource)

            if not sameSource then
                NodeOps.AddSource (n, m2.Node :> ISource)

            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                linked <- true

        if not n.IsDisposed then
            CombinatorHelpers.refreshStaticPair (
                g,
                n,
                sameSource,
                (fun () -> CombinatorHelpers.refreshMemoNode (n, m1.Node)),
                (fun () -> CombinatorHelpers.refreshMemoNode (n, m2.Node))
            )

        if n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
            this.Moved <- false
            previous
        else
            let st1 = m1.Node.Status

            if StatusBits.isUnready st1 then
                CombinatorHelpers.clearActive n

                if StatusBits.isPending st1 then
                    let pendingNode = m1 :> INode
                    Tracer.Suspend (g, n, pendingNode)
                    n.PendingSourceField <- pendingNode
                    n.SetStatusPending ()
                else
                    n.FailureField <- m1.Node.FailureField
                    n.SetStatusError ()

                this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                previous
            else
                let st2 = m2.Node.Status

                if StatusBits.isUnready st2 then
                    CombinatorHelpers.clearActive n

                    if StatusBits.isPending st2 then
                        let pendingNode = m2 :> INode
                        Tracer.Suspend (g, n, pendingNode)
                        n.PendingSourceField <- pendingNode
                        n.SetStatusPending ()
                    else
                        n.FailureField <- m2.Node.FailureField
                        n.SetStatusError ()

                    this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                    previous
                else
                    if g.HasLastRaised then
                        g.ClearLastRaised ()

                    let a = m1.PeekRaw
                    let b = m2.PeekRaw
                    let mutable result = Unchecked.defaultof<'C>

#if FABLE_COMPILER
                    let mutable callEx: exn = Platform.nil ()

                    try
                        result <- g.RunUntrackedPure2 (n :> IScopeHost, mapping, a, b)

                        if CombinatorHelpers.isViolated n then
                            raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))
                    with ex ->
                        callEx <- Thrown.hold ex

                    if not (Platform.isNull callEx) then
                        CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.mapPending, Thrown.take callEx, previousFailure)
                        |> ignore

                        this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                        previous
#else
                    let hasJournal = not (Platform.isNull g.Journal)

                    if hasJournal then
                        g.Journal.EnterDerived ()

                    let saved = g.EnterUntrackedPure (n :> IScopeHost)

                    let callEx =
                        try
                            result <- mapping.Invoke (a, b)

                            if CombinatorHelpers.isViolated n then
                                raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                            null
                        with ex ->
                            ex

                    g.LeaveUntrackedPure saved

                    if hasJournal then
                        g.Journal.LeaveDerived ()

                    if not (Platform.isNull callEx) then
                        CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.mapPending, callEx, previousFailure)
                        |> ignore

                        this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                        previous
#endif
                    else
                        CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, result, previousStatus, previousFailure)

/// <summary>Static 2-source <c>map2</c> / <c>zip</c> runner over <c>CombinatorSource</c> (Theorem B19).</summary>
[<Sealed>]
type internal SourceMap2State<'A, 'B, 'C>(s1: CombinatorSource<'A>, s2: CombinatorSource<'B>, mapping: Map2Delegate<'A, 'B, 'C>) =
    inherit MemoOnRunner<'C>()

    let sameSource = obj.ReferenceEquals (box s1.Node, box s2.Node)
    let mutable linked = false

    override this.Execute(origin, n, outputEqual, previous, previousStatus, previousFailure, previousPending) =
        let g = n.Graph

        if not linked then
            n.SourcesField.BeginRun ()
            NodeOps.AddSource (n, s1.Source)

            if not sameSource then
                NodeOps.AddSource (n, s2.Source)

            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                linked <- true

        if not n.IsDisposed then
            CombinatorHelpers.refreshStaticPair (g, n, sameSource, (fun () -> s1.RefreshAndCheckStale n), (fun () -> s2.RefreshAndCheckStale n))

        if n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
            this.Moved <- false
            previous
        else
            let st1 = s1.Status

            if StatusBits.isUnready st1 then
                CombinatorHelpers.clearActive n

                if StatusBits.isPending st1 then
                    let pendingNode = s1.Node
                    Tracer.Suspend (g, n, pendingNode)
                    n.PendingSourceField <- pendingNode
                    n.SetStatusPending ()
                else
                    n.FailureField <- s1.Failure
                    n.SetStatusError ()

                this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                previous
            else
                let st2 = s2.Status

                if StatusBits.isUnready st2 then
                    CombinatorHelpers.clearActive n

                    if StatusBits.isPending st2 then
                        let pendingNode = s2.Node
                        Tracer.Suspend (g, n, pendingNode)
                        n.PendingSourceField <- pendingNode
                        n.SetStatusPending ()
                    else
                        n.FailureField <- s2.Failure
                        n.SetStatusError ()

                    this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                    previous
                else
                    if g.HasLastRaised then
                        g.ClearLastRaised ()

                    let a = s1.Peek
                    let b = s2.Peek
                    let mutable result = Unchecked.defaultof<'C>

#if FABLE_COMPILER
                    let mutable callEx: exn = Platform.nil ()

                    try
                        result <- g.RunUntrackedPure2 (n :> IScopeHost, mapping, a, b)

                        if CombinatorHelpers.isViolated n then
                            raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))
                    with ex ->
                        callEx <- Thrown.hold ex

                    if not (Platform.isNull callEx) then
                        CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.mapPending, Thrown.take callEx, previousFailure)
                        |> ignore

                        this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                        previous
#else
                    let hasJournal = not (Platform.isNull g.Journal)

                    if hasJournal then
                        g.Journal.EnterDerived ()

                    let saved = g.EnterUntrackedPure (n :> IScopeHost)

                    let callEx =
                        try
                            result <- mapping.Invoke (a, b)

                            if CombinatorHelpers.isViolated n then
                                raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                            null
                        with ex ->
                            ex

                    g.LeaveUntrackedPure saved

                    if hasJournal then
                        g.Journal.LeaveDerived ()

                    if not (Platform.isNull callEx) then
                        CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.mapPending, callEx, previousFailure)
                        |> ignore

                        this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                        previous
#endif
                    else
                        CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, result, previousStatus, previousFailure)

/// <summary>Two-slot split <c>Signal.bind</c> / <c>Memo.bind</c> runner (Theorem B20).</summary>
[<Sealed>]
type internal SplitBindState<'T, 'U>(graph: Graph, outerSignal: Signal<'T>, outerMemo: Memo<'T>, binder: 'T -> Memo<'U>) =
    inherit MemoOnRunner<'U>()

    let keyEqual = graph.EncodedComparer<'T>()
    let isSignalOuter = not (Platform.isNull (box outerSignal))
    let mutable node: MemoNode = Platform.nil ()
    let mutable outerLinked = false
    let mutable hasKey = false
    let mutable lastKey = Unchecked.defaultof<'T>
    let mutable innerMemo: Memo<'U> = Platform.nil ()
    let mutable binderFailure: Failure = Platform.nil ()
    let mutable computing = false

    member internal _.Bind(n: MemoNode) = node <- n

    member inline private _.OuterSource: ISource =
        if isSignalOuter then
            outerSignal :> ISource
        else
            outerMemo.Node :> ISource

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.CompareKeyGuarded(origin: INode, n: MemoNode, nextKey: 'T, previousFailure: Failure) : byte =
        try
            if ComparerSentinel.equals keyEqual lastKey nextKey then
                1uy
            else
                0uy
        with ex ->
            CombinatorHelpers.clearActive n
            n.FailureField <- n.Graph.FailureOf (ex, origin, previousFailure)
            n.SetStatusError ()
            2uy

    member private this.ReadInner
        (
            g: Graph,
            n: MemoNode,
            origin: INode,
            inner: Memo<'U>,
            outputEqual: IEqualityComparer<'U>,
            previous: 'U,
            previousStatus: Status,
            previousFailure: Failure,
            previousPending: INode
        ) : 'U =
        let innerSlot = if n.SourcesField.Count > 1 then 1 else 0
        CombinatorHelpers.refreshStaticSlot (g, n, innerSlot, fun () -> CombinatorHelpers.refreshMemoNode (n, inner.Node))

        if n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
            this.Moved <- false
            previous
        else
            let st = inner.Node.Status

            if StatusBits.isUnready st then
                CombinatorHelpers.clearActive n

                if StatusBits.isPending st then
                    let pendingNode = inner :> INode
                    Tracer.Suspend (g, n, pendingNode)
                    n.PendingSourceField <- pendingNode
                    n.SetStatusPending ()
                else
                    n.FailureField <- inner.Node.FailureField
                    n.SetStatusError ()

                this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                previous
            else
                CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, inner.PeekRaw, previousStatus, previousFailure)

    override this.Execute(origin, n, outputEqual, previous, previousStatus, previousFailure, previousPending) =
        let g = n.Graph

        if not isSignalOuter then
#if !RANVIER_TRACE
            outerMemo.EnsureMaterializedIfClaimed ()
#endif
            ()

        let outerSrc = this.OuterSource

        if not outerLinked then
            n.SourcesField.BeginRun ()
            NodeOps.AddSource (n, outerSrc)

            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                outerLinked <- true

        if not isSignalOuter && not n.IsDisposed then
            CombinatorHelpers.refreshStaticSlot (g, n, 0, fun () -> CombinatorHelpers.refreshMemoNode (n, outerMemo.Node))

        if n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
            this.Moved <- false
            previous
        else
            let outerStatus = if isSignalOuter then Status.None else outerMemo.Node.Status

            if StatusBits.isUnready outerStatus then
                CombinatorHelpers.clearActive n

                if StatusBits.isPending outerStatus then
                    let pendingNode = outerMemo :> INode
                    Tracer.Suspend (g, n, pendingNode)
                    n.PendingSourceField <- pendingNode
                    n.SetStatusPending ()
                else
                    n.FailureField <- outerMemo.Node.FailureField
                    n.SetStatusError ()

                this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                previous
            else
                let nextKey =
                    if isSignalOuter then
                        outerSignal.Peek
                    else
                        outerMemo.PeekRaw

                let keyCmp =
                    if not hasKey then
                        0uy
                    elif SafeEquality.isSafe keyEqual then
                        if ComparerSentinel.equals keyEqual lastKey nextKey then
                            1uy
                        else
                            0uy
                    else
                        this.CompareKeyGuarded (origin, n, nextKey, previousFailure)

                if keyCmp = 2uy then
                    this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                    previous
                elif keyCmp = 1uy then
                    if not (Platform.isNull binderFailure) then
                        CombinatorHelpers.clearActive n
                        n.FailureField <- binderFailure
                        n.SetStatusError ()
                        this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                        previous
                    else
                        this.ReadInner (g, n, origin, innerMemo, outputEqual, previous, previousStatus, previousFailure, previousPending)
                else
                    lastKey <- nextKey
                    hasKey <- true
                    innerMemo <- Platform.nil ()
                    binderFailure <- Platform.nil ()

                    if n.SourcesField.Count > 1 then
                        n.SourcesField.BeginRun ()
                        NodeOps.AddSource (n, outerSrc)
                        n.SourcesField.EndRun (n :> IComputation)

                    if not (Platform.isNull (box n.ScopeField)) then
                        g.Discharge n.ScopeField

                    if n.IsDisposed then
                        CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
                        this.Moved <- false
                        previous
                    else
                        if g.HasLastRaised then
                            g.ClearLastRaised ()

                        let mutable nextInner: Memo<'U> = Platform.nil ()
                        computing <- true

#if FABLE_COMPILER
                        let mutable bindEx: exn = Platform.nil ()

                        try
                            try
                                nextInner <- g.RunUntrackedHostedArg (this :> IScopeHost, binder, nextKey)
                            finally
                                computing <- false
                        with ex ->
                            bindEx <- Thrown.hold ex

                        let unwrappedEx =
                            if Platform.isNull bindEx then
                                Platform.nil ()
                            else
                                Thrown.take bindEx
#else
                        let hasJournal = not (Platform.isNull g.Journal)

                        if hasJournal then
                            g.Journal.EnterDerived ()

                        let saved = g.EnterUntrackedHosted (this :> IScopeHost)

                        let unwrappedEx =
                            try
                                nextInner <- binder nextKey
                                null
                            with ex ->
                                ex

                        g.LeaveUntrackedHosted saved

                        if hasJournal then
                            g.Journal.LeaveDerived ()

                        computing <- false
#endif

                        if not (Platform.isNull unwrappedEx) then
                            let f =
                                CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.bindPending, unwrappedEx, previousFailure)

                            binderFailure <- f
                            this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                            previous
                        elif n.IsDisposed then
                            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
                            this.Moved <- false
                            previous
                        elif Platform.isNull (box nextInner) then
                            CombinatorHelpers.clearActive n

                            let f =
                                g.FailureOf (NullReferenceException UntrackedPhase.nullBindReturn, origin, previousFailure)

                            binderFailure <- f
                            n.FailureField <- f
                            n.SetStatusError ()
                            this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                            previous
                        elif not (obj.ReferenceEquals (nextInner.Graph, g)) then
                            CombinatorHelpers.clearActive n

                            let f =
                                g.FailureOf (InvalidOperationException UntrackedPhase.crossGraphBind, origin, previousFailure)

                            binderFailure <- f
                            n.FailureField <- f
                            n.SetStatusError ()
                            this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                            previous
                        else
#if !RANVIER_TRACE
                            nextInner.EnsureMaterializedIfClaimed ()
#endif
                            innerMemo <- nextInner
                            n.SourcesField.BeginRun ()
                            NodeOps.AddSource (n, outerSrc)
                            NodeOps.AddSource (n, nextInner.Node :> ISource)

                            if n.IsDisposed then
                                n.SourcesField.Clear (n :> IComputation)
                                CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
                                this.Moved <- false
                                previous
                            else
                                n.SourcesField.EndRun (n :> IComputation)

                                this.ReadInner (g, n, origin, nextInner, outputEqual, previous, previousStatus, previousFailure, previousPending)

    interface IScopeHost with
        member _.CanOwn = computing

        member _.Scope =
            if computing then
                if Platform.isNull (box node.ScopeField) then
                    let s = new Owner (node.Graph.Root)
                    node.ScopeField <- s
                    Tracer.ScopeNew (s, node.Graph, node.NodeId)

                    if node.IsDisposed then
                        s.Dispose ()

                node.ScopeField
            else
                raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

/// <summary>Two-phase split <c>bind</c> runner over an arbitrary tracked key reader (generalizing Theorem B20).</summary>
[<Sealed>]
type internal ReactiveSplitBindState<'K, 'U>(readKey: unit -> 'K, binder: 'K -> Memo<'U>, keyEqual: IEqualityComparer<'K>) =
    inherit MemoOnRunner<'U>()

    let mutable node: MemoNode = Platform.nil ()
    let mutable hasKey = false
    let mutable lastKey = Unchecked.defaultof<'K>
    let mutable innerMemo: Memo<'U> = Platform.nil ()
    let mutable binderFailure: Failure = Platform.nil ()
    let mutable computing = false

    member internal _.Bind(n: MemoNode) = node <- n

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.RecordPhase1Failure(origin: INode, n: MemoNode, ex: exn, previousFailure: Failure) =
        let g = n.Graph
        CombinatorHelpers.clearActive n

        if CombinatorHelpers.isViolated n then
            CombinatorHelpers.clearViolated n
            n.FailureField <- g.FailureOf (ScopeMessages.failure ScopeMode.Pure ex, origin, previousFailure)
            n.SetStatusError ()
        else
            match ex with
            | NotReadyException source ->
                n.PendingSourceField <- source
                n.SetStatusPending ()
            | _ ->
                n.FailureField <- g.FailureOf (ex, origin, previousFailure)
                n.SetStatusError ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.CompareKeyGuarded(origin: INode, n: MemoNode, nextKey: 'K, previousFailure: Failure) : byte =
        try
            if ComparerSentinel.equals keyEqual lastKey nextKey then
                1uy
            else
                0uy
        with ex ->
            CombinatorHelpers.clearActive n
            n.FailureField <- n.Graph.FailureOf (ex, origin, previousFailure)
            n.SetStatusError ()
            2uy

    member private this.ReadInner
        (
            g: Graph,
            n: MemoNode,
            origin: INode,
            inner: Memo<'U>,
            outputEqual: IEqualityComparer<'U>,
            previous: 'U,
            previousStatus: Status,
            previousFailure: Failure,
            previousPending: INode
        ) : 'U =
        let innerSlot =
            if n.SourcesField.Count > 0 then
                n.SourcesField.Count - 1
            else
                0

        CombinatorHelpers.refreshStaticSlot (g, n, innerSlot, fun () -> CombinatorHelpers.refreshMemoNode (n, inner.Node))

        if n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
            this.Moved <- false
            previous
        else
            let st = inner.Node.Status

            if StatusBits.isUnready st then
                CombinatorHelpers.clearActive n

                if StatusBits.isPending st then
                    let pendingNode = inner :> INode
                    Tracer.Suspend (g, n, pendingNode)
                    n.PendingSourceField <- pendingNode
                    n.SetStatusPending ()
                else
                    n.FailureField <- inner.Node.FailureField
                    n.SetStatusError ()

                this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                previous
            else
                CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, inner.PeekRaw, previousStatus, previousFailure)

    override this.Execute(origin, n, outputEqual, previous, previousStatus, previousFailure, previousPending) =
        let g = n.Graph
        n.SourcesField.BeginRun ()
        let mutable nextKey = Unchecked.defaultof<'K>
        let mutable phase1Ok = false

#if FABLE_COMPILER
        try
            try
#if !RANVIER_TRACE
                nextKey <- g.RunHostedNode (n, readKey, ())
#else
                nextKey <- g.RunHosted (n :> IComputation, readKey)
#endif
                if CombinatorHelpers.isViolated n then
                    raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                phase1Ok <- true
            with ex ->
                if n.IsDisposed then
                    n.SourcesField.Clear (n :> IComputation)
                else
                    n.SourcesField.EndRun (n :> IComputation)

                this.RecordPhase1Failure (origin, n, ex, previousFailure)
        with _ ->
            reraise ()
#else
        let saved = g.EnterHostedNode n

        let phase1Ex =
            try
                nextKey <- readKey ()

                if g.RaisedPending || CombinatorHelpers.isViolated n then
                    g.CheckRaised ()

                    if CombinatorHelpers.isViolated n then
                        raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                null
            with ex ->
                ex

        g.LeaveHosted saved

        if not (Platform.isNull phase1Ex) then
            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                n.SourcesField.EndRun (n :> IComputation)

            this.RecordPhase1Failure (origin, n, phase1Ex, previousFailure)
        else
            phase1Ok <- true
#endif

        if not phase1Ok then
            this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
            previous
        elif n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
            this.Moved <- false
            previous
        else
            let keyCmp =
                if not hasKey then
                    0uy
                elif SafeEquality.isSafe keyEqual then
                    if ComparerSentinel.equals keyEqual lastKey nextKey then
                        1uy
                    else
                        0uy
                else
                    this.CompareKeyGuarded (origin, n, nextKey, previousFailure)

            if keyCmp = 2uy then
                if n.IsDisposed then
                    n.SourcesField.Clear (n :> IComputation)
                else
                    n.SourcesField.EndRun (n :> IComputation)

                this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                previous
            elif keyCmp = 1uy then
                if not (Platform.isNull binderFailure) then
                    if n.IsDisposed then
                        n.SourcesField.Clear (n :> IComputation)
                    else
                        n.SourcesField.EndRun (n :> IComputation)

                    CombinatorHelpers.clearActive n
                    n.FailureField <- binderFailure
                    n.SetStatusError ()
                    this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                    previous
                else
#if !RANVIER_TRACE
                    innerMemo.EnsureMaterializedIfClaimed ()
#endif
                    NodeOps.AddSource (n, innerMemo.Node :> ISource)

                    if n.IsDisposed then
                        n.SourcesField.Clear (n :> IComputation)
                        CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
                        this.Moved <- false
                        previous
                    else
                        n.SourcesField.EndRun (n :> IComputation)
                        this.ReadInner (g, n, origin, innerMemo, outputEqual, previous, previousStatus, previousFailure, previousPending)
            else
                lastKey <- nextKey
                hasKey <- true
                innerMemo <- Platform.nil ()
                binderFailure <- Platform.nil ()

                if n.IsDisposed then
                    n.SourcesField.Clear (n :> IComputation)
                    CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
                    this.Moved <- false
                    previous
                else
                    n.SourcesField.EndRun (n :> IComputation)

                    if not (Platform.isNull (box n.ScopeField)) then
                        g.Discharge n.ScopeField

                    if n.IsDisposed then
                        CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
                        this.Moved <- false
                        previous
                    else
                        if g.HasLastRaised then
                            g.ClearLastRaised ()

                        let mutable nextInner: Memo<'U> = Platform.nil ()
                        computing <- true

#if FABLE_COMPILER
                        let mutable bindEx: exn = Platform.nil ()

                        try
                            try
                                nextInner <- g.RunUntrackedHostedArg (this :> IScopeHost, binder, nextKey)

                                if CombinatorHelpers.isViolated n then
                                    raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))
                            finally
                                computing <- false
                        with ex ->
                            bindEx <- Thrown.hold ex

                        let unwrappedEx =
                            if Platform.isNull bindEx then
                                Platform.nil ()
                            else
                                Thrown.take bindEx
#else
                        let hasJournal = not (Platform.isNull g.Journal)

                        if hasJournal then
                            g.Journal.EnterDerived ()

                        let savedPhase2 = g.EnterUntrackedHosted (this :> IScopeHost)

                        let unwrappedEx =
                            try
                                nextInner <- binder nextKey

                                if CombinatorHelpers.isViolated n then
                                    raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                                null
                            with ex ->
                                ex

                        g.LeaveUntrackedHosted savedPhase2

                        if hasJournal then
                            g.Journal.LeaveDerived ()

                        computing <- false
#endif

                        if not (Platform.isNull unwrappedEx) then
                            let f =
                                CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.bindPending, unwrappedEx, previousFailure)

                            binderFailure <- f
                            this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                            previous
                        elif n.IsDisposed then
                            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
                            this.Moved <- false
                            previous
                        elif Platform.isNull (box nextInner) then
                            CombinatorHelpers.clearActive n

                            let f =
                                g.FailureOf (NullReferenceException UntrackedPhase.nullBindReturn, origin, previousFailure)

                            binderFailure <- f
                            n.FailureField <- f
                            n.SetStatusError ()
                            this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                            previous
                        elif not (obj.ReferenceEquals (nextInner.Graph, g)) then
                            CombinatorHelpers.clearActive n

                            let f =
                                g.FailureOf (InvalidOperationException UntrackedPhase.crossGraphBind, origin, previousFailure)

                            binderFailure <- f
                            n.FailureField <- f
                            n.SetStatusError ()
                            this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                            previous
                        else
#if !RANVIER_TRACE
                            nextInner.EnsureMaterializedIfClaimed ()
#endif
                            innerMemo <- nextInner
                            NodeOps.AddSource (n, nextInner.Node :> ISource)

                            if n.IsDisposed then
                                n.SourcesField.Clear (n :> IComputation)
                                CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, previousPending)
                                this.Moved <- false
                                previous
                            else
                                this.ReadInner (g, n, origin, nextInner, outputEqual, previous, previousStatus, previousFailure, previousPending)

    interface IScopeHost with
        member _.CanOwn = computing

        member _.Scope =
            if computing then
                if Platform.isNull (box node.ScopeField) then
                    let s = new Owner (node.Graph.Root)
                    node.ScopeField <- s
                    Tracer.ScopeNew (s, node.Graph, node.NodeId)

                    if node.IsDisposed then
                        s.Dispose ()

                node.ScopeField
            else
                CombinatorHelpers.setViolated node
                raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))


#if !RANVIER_TRACE
/// <summary>Outcome of evaluating a prefix of depth &gt;= 4 inside <c>FusedChainMap</c>.</summary>
[<Struct; NoEquality; NoComparison>]
type internal FusedPrefixOutcome<'T> =
    val Tag: byte // 0uy = Cutoff, 1uy = Ready, 2uy = Pending, 3uy = Error, 4uy = DeFused
    val Value: 'T
    val MovedToUnready: bool

    new(tag: byte, value: 'T, movedToUnready: bool) =
        {
            Tag = tag
            Value = value
            MovedToUnready = movedToUnready
        }

[<AbstractClass>]
type internal FusedExtendable<'U>() =
    inherit FusedRunner<'U>()

    abstract Fuse<'Next> : prevMemo: Memo<'U> * mapping: ('U -> 'Next) -> struct (FusedExtendable<'Next> * byte)

    abstract LinkRoot: n: MemoNode -> unit

    abstract MarkRootDirtyIfLive: unit -> unit

    abstract EvalPrefix: g: Graph * n: MemoNode * selfMemo: Memo<'U> * previousStatus: Status * previousFailure: Failure -> FusedPrefixOutcome<'U>

    abstract DetachUnwarmedPrefix: selfMemo: Memo<'U> -> unit

    abstract MaterializeWarmPrefix:
        selfMemo: Memo<'U> *
        tailFresh: int *
        tailStatus: Status *
        tailFailure: Failure *
        hasSelfReady: bool *
        hasSelfPublished: bool *
        selfVal: 'U *
        selfRuns: int ->
            unit

    abstract MaterializeActivePrefix:
        selfMemo: Memo<'U> *
        prevTailStatus: Status *
        prevTailFailure: Failure *
        hasSelfReady: bool *
        hasSelfPublished: bool *
        prevSelfVal: 'U *
        prevSelfRuns: int ->
            unit

    abstract MaterializeWaitingRoot:
        selfMemo: Memo<'U> *
        prevTailStatus: Status *
        prevTailFailure: Failure *
        hasSelfReady: bool *
        hasSelfPublished: bool *
        prevSelfVal: 'U *
        prevSelfRuns: int ->
            unit

and [<AbstractClass; Sealed>] internal FusedStageHelpers =
    static member inline runPureStage(g: Graph, n: MemoNode, f: 'A -> 'B, arg: 'A) : 'B =
#if FABLE_COMPILER
        let res = g.RunUntrackedPureArg (n :> IScopeHost, f, arg)

        if (n.Flags &&& NodeFlags.Violated) <> 0 then
            raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

        res
#else
        let res = f arg

        if (n.Flags &&& NodeFlags.Violated) <> 0 then
            raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

        res
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    static member finishLeafCustomEq<'U>
        (tailMemo: Memo<'U>, n: MemoNode, g: Graph, outputEqual: IEqualityComparer<'U>, previous: 'U, nextVal: 'U)
        : bool =
        tailMemo.SetMaterializedValue nextVal

        n.Flags <-
            (n.Flags &&& ~~~NodeFlags.ActiveMask)
            ||| NodeFlags.Published

        let mutable compEx: exn = Platform.nil ()

        try
            ComparerSentinel.equals outputEqual previous nextVal
            |> ignore
        with ex ->
#if FABLE_COMPILER
            compEx <- Thrown.hold ex
#else
            compEx <- ex
#endif

        if not (Platform.isNull compEx) then
#if FABLE_COMPILER
            let rawEx = Thrown.take compEx
#else
            let rawEx = compEx
#endif
            tailMemo.SetMaterializedValue previous
            n.PendingSourceField <- Platform.nil ()
            n.FailureField <- g.FailureOf (rawEx, tailMemo :> INode, null)
            n.SetStatusError ()

        if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
            n.Flags <- n.Flags ||| NodeFlags.Unannounced
            g.NoteStaleTransition ()

        g.ExitPullFast ()
        || (n.Flags &&& NodeFlags.FreshMask) <> 0

    static member inline finishLeafSuccess<'U>
        (tailMemo: Memo<'U>, n: MemoNode, g: Graph, safeTailEq: bool, outputEqual: IEqualityComparer<'U>, previous: 'U, cutoff: bool, nextVal: 'U)
        : bool =
        g.CloseLeafPureBody ()

        if cutoff then
            n.Flags <- n.Flags &&& ~~~NodeFlags.ActiveMask

            if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
                n.Flags <- n.Flags ||| NodeFlags.Unannounced
                g.NoteStaleTransition ()

            g.ExitPullFast ()
            || (n.Flags &&& NodeFlags.FreshMask) <> 0
        elif safeTailEq then
            tailMemo.SetMaterializedValue nextVal

            n.Flags <-
                (n.Flags &&& ~~~NodeFlags.ActiveMask)
                ||| NodeFlags.Published

            if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
                n.Flags <- n.Flags ||| NodeFlags.Unannounced
                g.NoteStaleTransition ()

            g.ExitPullFast ()
            || (n.Flags &&& NodeFlags.FreshMask) <> 0
        else
            FusedStageHelpers.finishLeafCustomEq (tailMemo, n, g, outputEqual, previous, nextVal)

    static member populateMaterializedStage<'T>
        (
            m: Memo<'T>,
            runner1: obj,
            kind1: byte,
            upSource: ISource,
            upPendingNode: INode,
            isFirst: bool,
            tailFresh: int,
            tailStatus: Status,
            tailFailure: Failure,
            hasReady: bool,
            hasPublished: bool,
            cachedVal: 'T,
            stageRuns: int
        ) =
        m.RebindFused (runner1, kind1)
        let nj = m.Node

        if not nj.IsDisposed then
            NodeOps.AddSource (nj, upSource)

        nj.RunsField <- stageRuns
        m.SetMaterializedValue cachedVal

        let stageFresh =
            if tailFresh = NodeFlags.FreshDirty then
                if isFirst then
                    NodeFlags.FreshDirty
                else
                    NodeFlags.FreshCheck
            else
                tailFresh

        nj.Flags <-
            (nj.Flags
             &&& ~~~(NodeFlags.FreshMask
                     ||| NodeFlags.StatusMask
                     ||| NodeFlags.Unannounced))
            ||| stageFresh
            ||| (if hasPublished then NodeFlags.Published else 0)

        if hasReady then
            nj.PendingSourceField <- Platform.nil ()
            nj.FailureField <- Platform.nil ()
            nj.SetStatusNone ()
        elif StatusBits.isPending tailStatus then
            nj.FailureField <- Platform.nil ()
            nj.PendingSourceField <- upPendingNode
            nj.SetStatusPending ()
        else
            nj.PendingSourceField <- Platform.nil ()
            nj.FailureField <- tailFailure
            nj.SetStatusError ()

    static member populateActiveStage<'T>(m: Memo<'T>, runner1: obj, kind1: byte, upSource: ISource, hasPublished: bool, prevVal: 'T, curRuns: int) =
        m.RebindFused (runner1, kind1)
        let nj = m.Node

        if not nj.IsDisposed then
            NodeOps.AddSource (nj, upSource)

        nj.RunsField <- curRuns
        m.SetMaterializedValue prevVal
        nj.PendingSourceField <- Platform.nil ()
        nj.FailureField <- Platform.nil ()

        nj.Flags <-
            (nj.Flags
             &&& ~~~(NodeFlags.FreshMask
                     ||| NodeFlags.StatusMask
                     ||| NodeFlags.ActiveMask
                     ||| NodeFlags.Unannounced))
            ||| NodeFlags.ActiveBody
            ||| (if hasPublished then NodeFlags.Published else 0)

    static member populateWaitingRoot<'T>
        (
            m: Memo<'T>,
            runner1: obj,
            kind1: byte,
            upSource: ISource,
            upPendingNode: INode,
            prevTailStatus: Status,
            prevTailFailure: Failure,
            hasPrevReady: bool,
            hasPublished: bool,
            prevVal: 'T,
            prevRuns: int
        ) =
        m.RebindFused (runner1, kind1)
        let nj = m.Node

        if not nj.IsDisposed then
            NodeOps.AddSource (nj, upSource)

        nj.RunsField <- prevRuns
        m.SetMaterializedValue prevVal

        if prevRuns = 0 then
            nj.PendingSourceField <- Platform.nil ()
            nj.FailureField <- Platform.nil ()

            nj.Flags <-
                (nj.Flags
                 &&& ~~~(NodeFlags.FreshMask
                         ||| NodeFlags.StatusMask
                         ||| NodeFlags.ActiveMask
                         ||| NodeFlags.Unannounced
                         ||| NodeFlags.Published))
                ||| NodeFlags.StatusUninitialized
                ||| NodeFlags.ActiveBody
        else
            nj.Flags <-
                (nj.Flags
                 &&& ~~~(NodeFlags.FreshMask
                         ||| NodeFlags.StatusMask
                         ||| NodeFlags.ActiveMask
                         ||| NodeFlags.Unannounced))
                ||| NodeFlags.ActiveBody
                ||| (if hasPublished then NodeFlags.Published else 0)

            if hasPrevReady then
                nj.PendingSourceField <- Platform.nil ()
                nj.FailureField <- Platform.nil ()
                nj.SetStatusNone ()
            elif StatusBits.isPending prevTailStatus then
                nj.FailureField <- Platform.nil ()
                nj.PendingSourceField <- upPendingNode
                nj.SetStatusPending ()
            else
                nj.PendingSourceField <- Platform.nil ()
                nj.FailureField <- prevTailFailure
                nj.SetStatusError ()

    static member populateWaitingStage<'T>
        (
            m: Memo<'T>,
            runner1: obj,
            kind1: byte,
            upSource: ISource,
            upPendingNode: INode,
            prevTailStatus: Status,
            prevTailFailure: Failure,
            hasPrevReady: bool,
            hasPublished: bool,
            prevVal: 'T,
            prevRuns: int
        ) =
        m.RebindFused (runner1, kind1)
        let nj = m.Node

        if not nj.IsDisposed then
            NodeOps.AddSource (nj, upSource)

        nj.RunsField <- prevRuns
        m.SetMaterializedValue prevVal

        if prevRuns = 0 then
            nj.PendingSourceField <- Platform.nil ()
            nj.FailureField <- Platform.nil ()

            nj.Flags <-
                (nj.Flags
                 &&& ~~~(NodeFlags.FreshMask
                         ||| NodeFlags.StatusMask
                         ||| NodeFlags.ActiveMask
                         ||| NodeFlags.Unannounced
                         ||| NodeFlags.Published))
                ||| NodeFlags.FreshDirty
                ||| NodeFlags.StatusUninitialized
                ||| NodeFlags.ActiveCheck
        else
            nj.Flags <-
                (nj.Flags
                 &&& ~~~(NodeFlags.FreshMask
                         ||| NodeFlags.StatusMask
                         ||| NodeFlags.ActiveMask
                         ||| NodeFlags.Unannounced))
                ||| NodeFlags.FreshCheck
                ||| NodeFlags.ActiveCheck
                ||| (if hasPublished then NodeFlags.Published else 0)

            if hasPrevReady then
                nj.PendingSourceField <- Platform.nil ()
                nj.FailureField <- Platform.nil ()
                nj.SetStatusNone ()
            elif StatusBits.isPending prevTailStatus then
                nj.FailureField <- Platform.nil ()
                nj.PendingSourceField <- upPendingNode
                nj.SetStatusPending ()
            else
                nj.PendingSourceField <- Platform.nil ()
                nj.FailureField <- prevTailFailure
                nj.SetStatusError ()

    static member advanceWaitingRootFromCurrentSource<'T>(m: Memo<'T>) =
        let nj = m.Node
        nj.Flags <- nj.Flags &&& ~~~NodeFlags.ActiveMask

        if not nj.IsDisposed then
            (Platform.fastCast<FusedRunner<'T>> m.ComputeObj).SetPreRefreshed()
            NodeOps.Recompute nj

    static member advanceWaitingStageFromCurrentPred<'Prev, 'U>(stageMemo: Memo<'U>, predMemo: Memo<'Prev>) =
        let sn = stageMemo.Node
        sn.Flags <- sn.Flags &&& ~~~NodeFlags.ActiveMask

        if not sn.IsDisposed then
            let predStale = (predMemo.Node :> ISource).IsStale

            if
                sn.RunsField = 0
                || (sn.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshDirty
            then
                (Platform.fastCast<FusedRunner<'U>> stageMemo.ComputeObj).SetPreRefreshed()
                NodeOps.Recompute sn
            elif not predStale then
                sn.Flags <-
                    sn.Flags
                    &&& ~~~(NodeFlags.FreshMask ||| NodeFlags.Unannounced)
            else
                sn.Graph.NoteStaleTransition ()

    static member inline prepareTailBeforeStagePublish(tn: MemoNode) : bool =
        let reDirtied = (tn.Flags &&& NodeFlags.FreshMask) <> 0

        if not tn.IsDisposed then
            tn.Flags <-
                (tn.Flags &&& ~~~NodeFlags.FreshMask)
                ||| NodeFlags.FreshCheck

        reDirtied

    static member publishChainPrevCompareSuccess<'T>(m: Memo<'T>, v: 'T, moved: bool) =
        let nj = m.Node

        if not nj.IsDisposed then
            nj.Flags <-
                (nj.Flags &&& ~~~NodeFlags.ActiveMask)
                ||| NodeFlags.Published

            nj.PendingSourceField <- Platform.nil ()
            nj.FailureField <- Platform.nil ()
            nj.SetStatusNone ()
            m.SetMaterializedValue v
            m.PublishDeFusedMove moved

    static member publishChainPrevCompareFailure<'T>(m: Memo<'T>, rawEx: exn, prevTailFailure: Failure) =
        let nj = m.Node

        if not nj.IsDisposed then
            let g = nj.Graph
            nj.Flags <- nj.Flags &&& ~~~NodeFlags.ActiveMask
            nj.PendingSourceField <- Platform.nil ()
            nj.FailureField <- g.FailureOf (rawEx, m :> INode, prevTailFailure)
            nj.SetStatusError ()
            m.PublishDeFusedMove true

    static member finishDeFusedStage<'T>
        (
            m: Memo<'T>,
            v: 'T,
            thrownEx: exn,
            prevRuns: int,
            hasPrevReady: bool,
            prevTailStatus: Status,
            prevTailFailure: Failure,
            upPendingNode: INode,
            precomputedCmp: sbyte
        ) =
        let nj = m.Node
        let g = nj.Graph

        let prevSt =
            if prevRuns = 0 then Status.Uninitialized
            elif hasPrevReady then Status.None
            elif StatusBits.isPending prevTailStatus then Status.Pending
            else Status.Error

        let prevFail =
            if prevSt = Status.Error then
                prevTailFailure
            else
                Platform.nil ()

        let prevPend =
            if prevSt = Status.Pending then
                upPendingNode
            else
                Platform.nil ()

        if nj.IsDisposed then
            CombinatorHelpers.restoreDisposedState (nj, prevSt, prevFail, prevPend)
        elif not (Platform.isNull thrownEx) then
            if precomputedCmp = 2y then
                nj.Flags <- nj.Flags &&& ~~~NodeFlags.ActiveMask
                nj.PendingSourceField <- Platform.nil ()
                nj.FailureField <- g.FailureOf (thrownEx, m :> INode, prevFail)
                nj.SetStatusError ()
                m.PublishDeFusedMove true
            else
                CombinatorHelpers.recordCallbackFailure (g, nj, m :> INode, UntrackedPhase.mapPending, thrownEx, prevFail)
                |> ignore

                let moved = CombinatorHelpers.statusMoved (nj, prevSt, prevFail)
                m.PublishDeFusedMove moved
        elif precomputedCmp >= 0y then
            nj.Flags <-
                (nj.Flags &&& ~~~NodeFlags.ActiveMask)
                ||| NodeFlags.Published

            nj.PendingSourceField <- Platform.nil ()
            nj.FailureField <- Platform.nil ()
            nj.SetStatusNone ()
            m.SetMaterializedValue v
            m.PublishDeFusedMove (prevSt <> Status.None || precomputedCmp = 1y)
        else
            let prevVal = m.PeekRaw
            nj.PendingSourceField <- Platform.nil ()
            nj.FailureField <- Platform.nil ()
            nj.SetStatusNone ()
            let eq = m.EncodedEqual

            if prevSt <> Status.None then
                nj.Flags <-
                    (nj.Flags &&& ~~~NodeFlags.ActiveMask)
                    ||| NodeFlags.Published

                m.SetMaterializedValue v
                m.PublishDeFusedMove true
            elif SafeEquality.isSafe eq then
                nj.Flags <-
                    (nj.Flags &&& ~~~NodeFlags.ActiveMask)
                    ||| NodeFlags.Published

                m.SetMaterializedValue v

                let moved =
                    (nj.ObserverCount <> 0 || g.QueuePending)
                    && not (ComparerSentinel.equals eq prevVal v)

                m.PublishDeFusedMove moved
            else
                let mutable moved = false

                try
                    moved <- not (ComparerSentinel.equals eq prevVal v)
                    m.SetMaterializedValue v

                    nj.Flags <-
                        (nj.Flags &&& ~~~NodeFlags.ActiveMask)
                        ||| NodeFlags.Published
                with ex ->
                    nj.Flags <- nj.Flags &&& ~~~NodeFlags.ActiveMask
                    nj.FailureField <- g.FailureOf (ex, m :> INode, prevFail)
                    nj.SetStatusError ()
                    moved <- true

                m.PublishDeFusedMove moved

    static member finishDeFusedTail<'Prev, 'U>
        (
            callerRunner: FusedExtendable<'U>,
            tailMemo: Memo<'U>,
            predMemo: Memo<'Prev>,
            tn: MemoNode,
            outputEqual: IEqualityComparer<'U>,
            previous: 'U,
            previousStatus: Status,
            previousFailure: Failure,
            reDirtiedBeforeDeFuse: bool
        ) : 'U =
        if tn.IsDisposed then
            if reDirtiedBeforeDeFuse then
                callerRunner.MarkRootDirtyIfLive ()

            CombinatorHelpers.restoreDisposedState (tn, previousStatus, previousFailure, tn.PendingSourceField)
            callerRunner.Moved <- false
            previous
        else
            let predStale = (predMemo.Node :> ISource).IsStale

            let needsRun =
                tn.RunsField = 0
                || (tn.Flags &&& NodeFlags.FreshMask) = NodeFlags.FreshDirty

            tn.Flags <- tn.Flags &&& ~~~NodeFlags.FreshMask

            let res =
                if needsRun then
                    let newRunner = Platform.fastCast<FusedRunner<'U>> tailMemo.ComputeObj
                    newRunner.SetPreRefreshed ()

                    let v =
                        newRunner.ExecuteFused (box tailMemo, tn, outputEqual, previous, previousStatus, previousFailure)

                    callerRunner.Moved <- newRunner.Moved
                    v
                else
                    CombinatorHelpers.clearActive tn
                    callerRunner.Moved <- false
                    previous

            if reDirtiedBeforeDeFuse then
                callerRunner.MarkRootDirtyIfLive ()

            if predStale && not tn.IsDisposed then
                tn.Graph.NoteStaleTransition ()
                NodeOps.MemoMarkCheck tn

            res

    static member finishTailMaterialization<'Prev, 'U>
        (tailMemo: Memo<'U>, predMemo: Memo<'Prev>, fTail: 'Prev -> 'U, tailFresh: int, tailStatus: Status)
        =
        let tn = tailMemo.Node

        if not tn.IsDisposed then
            NodeOps.AddSource (tn, predMemo.Node :> ISource)

        if StatusBits.isPending tailStatus then
            tn.PendingSourceField <- predMemo :> INode

        if tailFresh = NodeFlags.FreshDirty then
            tn.Flags <-
                (tn.Flags &&& ~~~NodeFlags.FreshMask)
                ||| NodeFlags.FreshCheck

        tailMemo.RebindFused (box (FusedSourceMap1<'Prev, 'U>(MemoSource<'Prev>(predMemo), fTail, not tn.IsDisposed)), 7uy)

    static member finishActiveTailMaterialization<'Prev, 'U>(tailMemo: Memo<'U>, predMemo: Memo<'Prev>, fTail: 'Prev -> 'U) =
        let tn = tailMemo.Node
        tn.SourcesField.Clear (tn :> IComputation)

        if not tn.IsDisposed then
            NodeOps.AddSource (tn, predMemo.Node :> ISource)

        tailMemo.RebindFused (box (FusedSourceMap1<'Prev, 'U>(MemoSource<'Prev>(predMemo), fTail, not tn.IsDisposed)), 7uy)

/// <summary>1-stage fusable <c>Signal.map</c> runner (<c>computeKind = 6uy</c>).</summary>
and [<Sealed>] internal FusedSignalMap1<'R, 'A>(sig0: Signal<'R>, f1: 'R -> 'A, initialLinked: bool) =
    inherit FusedExtendable<'A>()

    let mutable linked = initialLinked

    override _.Fuse<'Next>(prevMemo: Memo<'A>, mapping: 'A -> 'Next) =
        struct (FusedSignalMap2<'R, 'A, 'Next>(sig0, f1, prevMemo, mapping) :> FusedExtendable<'Next>, 6uy)

    override _.LinkRoot(n: MemoNode) =
        if not linked then
            n.SourcesField.BeginRun ()
            NodeOps.AddSource (n, sig0 :> ISource)

            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                linked <- true

    override _.MarkRootDirtyIfLive() = ()

    override _.EvalSignalLeaf(tailObj, n, g, safeTailEq, outputEqual, previous) =
        let tailMemo = Platform.fastCast<Memo<'A>> tailObj

        if g.HasLastRaised then
            g.ClearLastRaised ()

        g.EnterUntrackedPureTop (n :> IScopeHost)

        n.Flags <-
            (n.Flags
             &&& ~~~(NodeFlags.FreshMask
                     ||| NodeFlags.Unannounced
                     ||| NodeFlags.ActiveMask))
            ||| NodeFlags.ActiveBody

        n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
        Counters.MemoRecomputed ()
#endif

        let mutable nextVal = Unchecked.defaultof<'A>
        let mutable thrownEx: exn = Platform.nil ()

        try
            nextVal <- f1 sig0.Peek

            if (n.Flags &&& NodeFlags.Violated) <> 0 then
                raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))
        with ex ->
#if FABLE_COMPILER
            thrownEx <- Thrown.hold ex
#else
            thrownEx <- ex
#endif

        if not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
            let ex = Thrown.take thrownEx
#else
            let ex = thrownEx
#endif
            CombinatorHelpers.recordCallbackFailure (g, n, tailMemo :> INode, UntrackedPhase.mapPending, ex, null)
            |> ignore

            if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
                n.Flags <- n.Flags ||| NodeFlags.Unannounced
                g.NoteStaleTransition ()

            g.CloseLeafPureBody ()

            g.ExitPullFast ()
            || (n.Flags &&& NodeFlags.FreshMask) <> 0
        else
            FusedStageHelpers.finishLeafSuccess (tailMemo, n, g, safeTailEq, outputEqual, previous, false, nextVal)

    override this.ExecuteFused(tailObj, n, outputEqual, previous, previousStatus, previousFailure) =
        let origin = Platform.fastCast<INode> tailObj
        let g = n.Graph
        this.LinkRoot n

        if n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, n.PendingSourceField)
            this.Moved <- false
            previous
        else
            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
            Counters.MemoRecomputed ()
#endif

            if g.HasLastRaised then
                g.ClearLastRaised ()

            let mutable nextVal = Unchecked.defaultof<'A>

#if FABLE_COMPILER
            let mutable callEx: exn = Platform.nil ()

            try
                nextVal <- FusedStageHelpers.runPureStage (g, n, f1, sig0.Peek)
            with ex ->
                callEx <- Thrown.hold ex

            if not (Platform.isNull callEx) then
                CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.mapPending, Thrown.take callEx, previousFailure)
                |> ignore

                this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                previous
#else
            let saved = g.EnterUntrackedPure (n :> IScopeHost)

            let callEx =
                try
                    nextVal <- FusedStageHelpers.runPureStage (g, n, f1, sig0.Peek)
                    null
                with ex ->
                    ex

            g.LeaveUntrackedPure saved

            if not (Platform.isNull callEx) then
                CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.mapPending, callEx, previousFailure)
                |> ignore

                this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                previous
#endif
            else
                CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, nextVal, previousStatus, previousFailure)

    override _.EvalPrefix(_g, _n, _selfMemo, _prevStatus, _prevFailure) =
        FusedPrefixOutcome<'A>(0uy, Unchecked.defaultof<'A>, false)

    override this.DetachUnwarmedPrefix(_selfMemo: Memo<'A>) =
        this.FusedNext <- Platform.nil ()

    override this.MaterializeWarmPrefix
        (
            selfMemo: Memo<'A>,
            tailFresh: int,
            tailStatus: Status,
            tailFailure: Failure,
            hasSelfReady: bool,
            hasSelfPublished: bool,
            selfVal: 'A,
            selfRuns: int
        ) =
        this.FusedNext <- Platform.nil ()
        linked <- not selfMemo.Node.IsDisposed

        FusedStageHelpers.populateMaterializedStage (
            selfMemo,
            box this,
            6uy,
            sig0 :> ISource,
            sig0 :> INode,
            true,
            tailFresh,
            tailStatus,
            tailFailure,
            hasSelfReady,
            hasSelfPublished,
            selfVal,
            selfRuns
        )

    override this.MaterializeActivePrefix
        (
            selfMemo: Memo<'A>,
            _prevTailStatus: Status,
            _prevTailFailure: Failure,
            _hasSelfReady: bool,
            hasSelfPublished: bool,
            prevSelfVal: 'A,
            prevSelfRuns: int
        ) =
        this.FusedNext <- Platform.nil ()
        linked <- not selfMemo.Node.IsDisposed
        FusedStageHelpers.populateActiveStage (selfMemo, box this, 6uy, sig0 :> ISource, hasSelfPublished, prevSelfVal, prevSelfRuns + 1)

    override _.MaterializeWaitingRoot(_, _, _, _, _, _, _) = ()
    override _.MaterializeTail(_tailMemo: obj) = ()
    override _.ClearIntermediates() = ()

/// <summary>2-stage unboxed <c>Signal.map |&gt; Memo.map</c> runner (<c>computeKind = 6uy</c>, Theorem B18).</summary>
and [<Sealed>] internal FusedSignalMap2<'R, 'A, 'B>(sig0: Signal<'R>, f1: 'R -> 'A, m1: Memo<'A>, f2: 'A -> 'B) =
    inherit FusedExtendable<'B>()

    let eq1 = m1.EncodedEqual
    let mutable val1 = Unchecked.defaultof<'A>
    let mutable runs1 = 0
    let mutable readyMask = 0uy
    let mutable publishedMask = 0uy
    let mutable activeStep = 0uy
    let mutable linked = false

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CompleteDeFused2
        (
            tailMemo: Memo<'B>,
            n: MemoNode,
            outputEqual: IEqualityComparer<'B>,
            previous: 'B,
            previousStatus: Status,
            previousFailure: Failure,
            prevReady1: bool,
            prevRuns1: int,
            deFusedVal1: 'A,
            deFusedCmp1: sbyte,
            thrownEx: exn
        ) : 'B =
        let reDirtied = FusedStageHelpers.prepareTailBeforeStagePublish n

        let ex =
            if Platform.isNull thrownEx then
                Platform.nil ()
            else
#if FABLE_COMPILER
                Thrown.take thrownEx
#else
                thrownEx
#endif

        FusedStageHelpers.finishDeFusedStage (m1, deFusedVal1, ex, prevRuns1, prevReady1, previousStatus, previousFailure, sig0 :> INode, deFusedCmp1)

        FusedStageHelpers.finishDeFusedTail (this, tailMemo, m1, n, outputEqual, previous, previousStatus, previousFailure, reDirtied)

    override _.Fuse<'Next>(prevMemo: Memo<'B>, mapping: 'B -> 'Next) =
        struct (FusedSignalMap3<'R, 'A, 'B, 'Next>(sig0, f1, m1, f2, prevMemo, mapping) :> FusedExtendable<'Next>, 6uy)

    override _.LinkRoot(n: MemoNode) =
        if not linked then
            n.SourcesField.BeginRun ()
            NodeOps.AddSource (n, sig0 :> ISource)

            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                linked <- true

    override _.MarkRootDirtyIfLive() =
        if not m1.Node.IsDisposed then
            NodeOps.MemoMarkDirty m1.Node

    override this.EvalSignalLeaf(tailObj, n, g, safeTailEq, outputEqual, previous) =
        let tailMemo = Platform.fastCast<Memo<'B>> tailObj

        if g.HasLastRaised then
            g.ClearLastRaised ()

        g.EnterUntrackedPureTop (n :> IScopeHost)

        n.Flags <-
            (n.Flags
             &&& ~~~(NodeFlags.FreshMask
                     ||| NodeFlags.Unannounced
                     ||| NodeFlags.ActiveMask))
            ||| NodeFlags.ActiveBody

        let prevRuns1 = runs1
        let prevReady1 = (readyMask &&& 1uy) <> 0uy
        let mutable stepIdx = 1
        let mutable cutoff = false
        let mutable nextVal = previous
        let mutable deFusedVal1 = Unchecked.defaultof<'A>
        let mutable deFusedCmp1 = -1y
        let mutable thrownEx: exn = Platform.nil ()

        try
            activeStep <- 1uy
            runs1 <- runs1 + 1
#if RANVIER_COUNTERS
            Counters.MemoRecomputed ()
#endif
            let v1 = f1 sig0.Peek

            if (n.Flags &&& NodeFlags.Violated) <> 0 then
                raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

            if activeStep = 255uy then
                deFusedVal1 <- v1
            else
                let mutable same1 = false

                if prevReady1 then
                    deFusedCmp1 <- 2y
                    same1 <- ComparerSentinel.equals eq1 val1 v1
                    deFusedCmp1 <- -1y

                if activeStep = 255uy then
                    deFusedVal1 <- v1
                    deFusedCmp1 <- if same1 then 0y else 1y
                elif same1 then
                    cutoff <- true
                else
                    val1 <- v1
                    readyMask <- 1uy
                    publishedMask <- 1uy
                    stepIdx <- 2
                    activeStep <- 2uy
                    n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
#endif
                    let v2 = f2 v1

                    if (n.Flags &&& NodeFlags.Violated) <> 0 then
                        raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                    nextVal <- v2
        with ex ->
#if FABLE_COMPILER
            thrownEx <- Thrown.hold ex
#else
            thrownEx <- ex
#endif

        let wasDeFused = activeStep = 255uy
        activeStep <- 0uy

        if wasDeFused && stepIdx < 2 then
            g.CloseLeafPureBody ()

            let res =
                this.CompleteDeFused2 (
                    tailMemo,
                    n,
                    outputEqual,
                    previous,
                    Status.None,
                    null,
                    prevReady1,
                    prevRuns1,
                    deFusedVal1,
                    deFusedCmp1,
                    thrownEx
                )

            if (n.Flags &&& NodeFlags.StatusUnreadyMask) = 0 then
                tailMemo.SetMaterializedValue res

            tailMemo.PublishDeFusedMove this.Moved

            g.ExitPullFast ()
            || (n.Flags &&& NodeFlags.FreshMask) <> 0
        elif not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
            let ex = Thrown.take thrownEx
#else
            let ex = thrownEx
#endif
            if stepIdx = 1 then
                readyMask <- 0uy
                n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
#endif

            let errOrigin = if stepIdx = 1 then m1 :> INode else tailMemo :> INode
            CombinatorHelpers.recordFusedFailure (g, n, errOrigin, deFusedCmp1 = 2y, ex, null)

            if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
                n.Flags <- n.Flags ||| NodeFlags.Unannounced
                g.NoteStaleTransition ()

            g.CloseLeafPureBody ()

            g.ExitPullFast ()
            || (n.Flags &&& NodeFlags.FreshMask) <> 0
        else
            FusedStageHelpers.finishLeafSuccess (tailMemo, n, g, safeTailEq, outputEqual, previous, cutoff, nextVal)

    override this.ExecuteFused(tailObj, n, outputEqual, previous, previousStatus, previousFailure) =
        let tailMemo = Platform.fastCast<Memo<'B>> tailObj
        let origin = tailMemo :> INode
        let g = n.Graph
        this.LinkRoot n

        if n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, n.PendingSourceField)
            this.Moved <- false
            previous
        else
            if g.HasLastRaised then
                g.ClearLastRaised ()

            let prevRuns1 = runs1
            let prevReady1 = (readyMask &&& 1uy) <> 0uy
            let mutable stepIdx = 1
            let mutable cutoff = false
            let mutable nextVal = previous
            let mutable deFusedVal1 = Unchecked.defaultof<'A>
            let mutable deFusedCmp1 = -1y

#if !FABLE_COMPILER
            let saved = g.EnterUntrackedPure (n :> IScopeHost)
#endif
            let mutable thrownEx: exn = Platform.nil ()

            try
                activeStep <- 1uy
                runs1 <- runs1 + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
#endif
                let v1 = FusedStageHelpers.runPureStage (g, n, f1, sig0.Peek)

                if activeStep = 255uy then
                    deFusedVal1 <- v1
                else
                    let mutable same1 = false

                    if prevReady1 then
                        deFusedCmp1 <- 2y
                        same1 <- ComparerSentinel.equals eq1 val1 v1
                        deFusedCmp1 <- -1y

                    if activeStep = 255uy then
                        deFusedVal1 <- v1
                        deFusedCmp1 <- if same1 then 0y else 1y
                    elif same1 then
                        cutoff <- true
                    else
                        val1 <- v1
                        readyMask <- 1uy
                        publishedMask <- 1uy
                        stepIdx <- 2
                        activeStep <- 2uy
                        n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                        Counters.MemoRecomputed ()
#endif
                        nextVal <- FusedStageHelpers.runPureStage (g, n, f2, v1)
            with ex ->
#if FABLE_COMPILER
                thrownEx <- Thrown.hold ex
#else
                thrownEx <- ex
#endif

            let wasDeFused = activeStep = 255uy
            activeStep <- 0uy

#if !FABLE_COMPILER
            g.LeaveUntrackedPure saved
#endif

            if wasDeFused && stepIdx < 2 then
                this.CompleteDeFused2 (
                    tailMemo,
                    n,
                    outputEqual,
                    previous,
                    previousStatus,
                    previousFailure,
                    prevReady1,
                    prevRuns1,
                    deFusedVal1,
                    deFusedCmp1,
                    thrownEx
                )
            elif not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
                let ex = Thrown.take thrownEx
#else
                let ex = thrownEx
#endif
                let errOrigin = if stepIdx = 1 then m1 :> INode else origin

                if stepIdx = 1 then
                    readyMask <- 0uy

                CombinatorHelpers.recordFusedFailure (g, n, errOrigin, deFusedCmp1 = 2y, ex, previousFailure)

                let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

                if
                    stepIdx = 1
                    && (prevRuns1 = 0 || prevReady1 || moved)
                then
                    n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
#endif

                this.Moved <- moved
                previous
            elif cutoff then
                CombinatorHelpers.clearActive n
                this.Moved <- false
                previous
            else
                CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, nextVal, previousStatus, previousFailure)

    override _.EvalPrefix(_g, _n, _selfMemo, _prevStatus, _prevFailure) =
        FusedPrefixOutcome<'B>(0uy, Unchecked.defaultof<'B>, false)

    override this.DetachUnwarmedPrefix(selfMemo: Memo<'B>) =
        this.FusedNext <- Platform.nil ()
        (Platform.fastCast<FusedRunner<'A>> m1.ComputeObj).FusedNext <- Platform.nil ()
        selfMemo.RebindFused (box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, false)), 7uy)

    override this.MaterializeWarmPrefix
        (
            selfMemo: Memo<'B>,
            tailFresh: int,
            tailStatus: Status,
            tailFailure: Failure,
            hasSelfReady: bool,
            hasSelfPublished: bool,
            selfVal: 'B,
            selfRuns: int
        ) =
        this.FusedNext <- Platform.nil ()

        (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
            .MaterializeWarmPrefix(m1, tailFresh, tailStatus, tailFailure, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1)

        FusedStageHelpers.populateMaterializedStage (
            selfMemo,
            box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not selfMemo.Node.IsDisposed)),
            7uy,
            m1.Node :> ISource,
            m1 :> INode,
            false,
            tailFresh,
            tailStatus,
            tailFailure,
            hasSelfReady,
            hasSelfPublished,
            selfVal,
            selfRuns
        )

    override _.MaterializeActivePrefix(_, _, _, _, _, _, _) = ()
    override _.MaterializeWaitingRoot(_, _, _, _, _, _, _) = ()

    override this.MaterializeTail(tailObj: obj) =
        let tailMemo = Platform.fastCast<Memo<'B>> tailObj
        let tn = tailMemo.Node
        let step = activeStep

        if step <> 0uy && step <> 255uy then
            activeStep <- 255uy

            if step = 1uy then
                let m1Runner = Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj

                m1Runner.MaterializeActivePrefix (
                    m1,
                    tn.Status,
                    tn.FailureField,
                    (readyMask &&& 1uy) <> 0uy,
                    (publishedMask &&& 1uy) <> 0uy,
                    val1,
                    runs1 - 1
                )

                FusedStageHelpers.finishActiveTailMaterialization (tailMemo, m1, f2)
            else
                let tailFresh = tn.Flags &&& NodeFlags.FreshMask
                tn.SourcesField.Clear (tn :> IComputation)

                (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                    .MaterializeWarmPrefix(m1, tailFresh, Status.None, null, true, true, val1, runs1)

                FusedStageHelpers.finishTailMaterialization (tailMemo, m1, f2, tailFresh, Status.None)
        elif tn.RunsField = 0 then
            this.DetachUnwarmedPrefix tailMemo
        else
            let tailFresh = tn.Flags &&& NodeFlags.FreshMask
            let tailStatus = tn.Status
            let tailFailure = tn.FailureField
            tn.SourcesField.Clear (tn :> IComputation)

            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeWarmPrefix(
                    m1,
                    tailFresh,
                    tailStatus,
                    tailFailure,
                    (readyMask &&& 1uy) <> 0uy,
                    (publishedMask &&& 1uy) <> 0uy,
                    val1,
                    runs1
                )

            FusedStageHelpers.finishTailMaterialization (tailMemo, m1, f2, tailFresh, tailStatus)

    override _.ClearIntermediates() =
        (Platform.fastCast<FusedRunner<'A>> m1.ComputeObj).FusedNext <- Platform.nil ()

/// <summary>3-stage unboxed <c>Signal.map |&gt; Memo.map |&gt; Memo.map</c> runner (<c>computeKind = 6uy</c>, Theorem B18).</summary>
and [<Sealed>] internal FusedSignalMap3<'R, 'A, 'B, 'C>(sig0: Signal<'R>, f1: 'R -> 'A, m1: Memo<'A>, f2: 'A -> 'B, m2: Memo<'B>, f3: 'B -> 'C) =
    inherit FusedExtendable<'C>()

    let eq1 = m1.EncodedEqual
    let eq2 = m2.EncodedEqual
    let mutable val1 = Unchecked.defaultof<'A>
    let mutable val2 = Unchecked.defaultof<'B>
    let mutable runs1 = 0
    let mutable runs2 = 0
    let mutable readyMask = 0uy
    let mutable publishedMask = 0uy
    let mutable activeStep = 0uy
    let mutable linked = false

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CompleteDeFused3
        (
            tailMemo: Memo<'C>,
            n: MemoNode,
            outputEqual: IEqualityComparer<'C>,
            previous: 'C,
            previousStatus: Status,
            previousFailure: Failure,
            prevMask: byte,
            prevRuns1: int,
            prevRuns2: int,
            stepIdx: int,
            deFusedVal1: 'A,
            deFusedVal2: 'B,
            deFusedCmp: sbyte,
            thrownEx: exn
        ) : 'C =
        let reDirtied = FusedStageHelpers.prepareTailBeforeStagePublish n

        let ex =
            if Platform.isNull thrownEx then
                Platform.nil ()
            else
#if FABLE_COMPILER
                Thrown.take thrownEx
#else
                thrownEx
#endif

        if stepIdx = 1 then
            FusedStageHelpers.finishDeFusedStage (
                m1,
                deFusedVal1,
                ex,
                prevRuns1,
                (prevMask &&& 1uy) <> 0uy,
                previousStatus,
                previousFailure,
                sig0 :> INode,
                deFusedCmp
            )

            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m2, m1)

            FusedStageHelpers.finishDeFusedTail (this, tailMemo, m2, n, outputEqual, previous, previousStatus, previousFailure, reDirtied)
        else
            FusedStageHelpers.finishDeFusedStage (
                m2,
                deFusedVal2,
                ex,
                prevRuns2,
                (prevMask &&& 2uy) <> 0uy,
                previousStatus,
                previousFailure,
                m1 :> INode,
                deFusedCmp
            )

            FusedStageHelpers.finishDeFusedTail (this, tailMemo, m2, n, outputEqual, previous, previousStatus, previousFailure, reDirtied)

    override _.Fuse<'Next>(prevMemo: Memo<'C>, mapping: 'C -> 'Next) =
        struct (FusedSignalMap4<'R, 'A, 'B, 'C, 'Next>(sig0, f1, m1, f2, m2, f3, prevMemo, mapping) :> FusedExtendable<'Next>, 6uy)

    override _.LinkRoot(n: MemoNode) =
        if not linked then
            n.SourcesField.BeginRun ()
            NodeOps.AddSource (n, sig0 :> ISource)

            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                linked <- true

    override _.MarkRootDirtyIfLive() =
        if not m1.Node.IsDisposed then
            NodeOps.MemoMarkDirty m1.Node

    override this.EvalSignalLeaf(tailObj, n, g, safeTailEq, outputEqual, previous) =
        let tailMemo = Platform.fastCast<Memo<'C>> tailObj

        if g.HasLastRaised then
            g.ClearLastRaised ()

        g.EnterUntrackedPureTop (n :> IScopeHost)

        n.Flags <-
            (n.Flags
             &&& ~~~(NodeFlags.FreshMask
                     ||| NodeFlags.Unannounced
                     ||| NodeFlags.ActiveMask))
            ||| NodeFlags.ActiveBody

        let prevMask = readyMask
        let prevRuns1 = runs1
        let prevRuns2 = runs2
        let mutable stepIdx = 1
        let mutable cutoff = false
        let mutable nextVal = previous
        let mutable deFusedVal1 = Unchecked.defaultof<'A>
        let mutable deFusedVal2 = Unchecked.defaultof<'B>
        let mutable deFusedCmp = -1y
        let mutable thrownEx: exn = Platform.nil ()

        try
            activeStep <- 1uy
            runs1 <- runs1 + 1
#if RANVIER_COUNTERS
            Counters.MemoRecomputed ()
#endif
            let v1 = f1 sig0.Peek

            if (n.Flags &&& NodeFlags.Violated) <> 0 then
                raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

            if activeStep = 255uy then
                deFusedVal1 <- v1
            else
                let mutable same1 = false

                if (prevMask &&& 1uy) <> 0uy then
                    deFusedCmp <- 2y
                    same1 <- ComparerSentinel.equals eq1 val1 v1
                    deFusedCmp <- -1y

                if activeStep = 255uy then
                    deFusedVal1 <- v1
                    deFusedCmp <- if same1 then 0y else 1y
                elif same1 then
                    cutoff <- true
                else
                    val1 <- v1
                    readyMask <- readyMask ||| 1uy
                    publishedMask <- publishedMask ||| 1uy
                    stepIdx <- 2
                    activeStep <- 2uy
                    runs2 <- runs2 + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
#endif
                    let v2 = f2 v1

                    if (n.Flags &&& NodeFlags.Violated) <> 0 then
                        raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                    if activeStep = 255uy then
                        deFusedVal2 <- v2
                    else
                        let mutable same2 = false

                        if (prevMask &&& 2uy) <> 0uy then
                            deFusedCmp <- 2y
                            same2 <- ComparerSentinel.equals eq2 val2 v2
                            deFusedCmp <- -1y

                        if activeStep = 255uy then
                            deFusedVal2 <- v2
                            deFusedCmp <- if same2 then 0y else 1y
                        elif same2 then
                            cutoff <- true
                        else
                            val2 <- v2
                            readyMask <- readyMask ||| 2uy
                            publishedMask <- publishedMask ||| 2uy
                            stepIdx <- 3
                            activeStep <- 3uy
                            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
#endif
                            let v3 = f3 v2

                            if (n.Flags &&& NodeFlags.Violated) <> 0 then
                                raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                            nextVal <- v3
        with ex ->
#if FABLE_COMPILER
            thrownEx <- Thrown.hold ex
#else
            thrownEx <- ex
#endif

        let wasDeFused = activeStep = 255uy
        activeStep <- 0uy

        if wasDeFused && stepIdx < 3 then
            g.CloseLeafPureBody ()

            let res =
                this.CompleteDeFused3 (
                    tailMemo,
                    n,
                    outputEqual,
                    previous,
                    Status.None,
                    null,
                    prevMask,
                    prevRuns1,
                    prevRuns2,
                    stepIdx,
                    deFusedVal1,
                    deFusedVal2,
                    deFusedCmp,
                    thrownEx
                )

            if (n.Flags &&& NodeFlags.StatusUnreadyMask) = 0 then
                tailMemo.SetMaterializedValue res

            tailMemo.PublishDeFusedMove this.Moved

            g.ExitPullFast ()
            || (n.Flags &&& NodeFlags.FreshMask) <> 0
        elif not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
            let ex = Thrown.take thrownEx
#else
            let ex = thrownEx
#endif
            if stepIdx = 1 then
                readyMask <- 0uy
                runs2 <- runs2 + 1
                n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
                Counters.MemoRecomputed ()
#endif
            elif stepIdx = 2 then
                readyMask <- readyMask &&& 1uy
                n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
#endif

            let errOrigin =
                if stepIdx = 1 then m1 :> INode
                elif stepIdx = 2 then m2 :> INode
                else tailMemo :> INode

            CombinatorHelpers.recordFusedFailure (g, n, errOrigin, deFusedCmp = 2y, ex, null)

            if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
                n.Flags <- n.Flags ||| NodeFlags.Unannounced
                g.NoteStaleTransition ()

            g.CloseLeafPureBody ()

            g.ExitPullFast ()
            || (n.Flags &&& NodeFlags.FreshMask) <> 0
        else
            FusedStageHelpers.finishLeafSuccess (tailMemo, n, g, safeTailEq, outputEqual, previous, cutoff, nextVal)

    override this.ExecuteFused(tailObj, n, outputEqual, previous, previousStatus, previousFailure) =
        let tailMemo = Platform.fastCast<Memo<'C>> tailObj
        let origin = tailMemo :> INode
        let g = n.Graph
        this.LinkRoot n

        if n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, n.PendingSourceField)
            this.Moved <- false
            previous
        else
            if g.HasLastRaised then
                g.ClearLastRaised ()

            let prevMask = readyMask
            let prevRuns1 = runs1
            let prevRuns2 = runs2
            let mutable stepIdx = 1
            let mutable cutoff = false
            let mutable nextVal = previous
            let mutable deFusedVal1 = Unchecked.defaultof<'A>
            let mutable deFusedVal2 = Unchecked.defaultof<'B>
            let mutable deFusedCmp = -1y

#if !FABLE_COMPILER
            let saved = g.EnterUntrackedPure (n :> IScopeHost)
#endif
            let mutable thrownEx: exn = Platform.nil ()

            try
                activeStep <- 1uy
                runs1 <- runs1 + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
#endif
                let v1 = FusedStageHelpers.runPureStage (g, n, f1, sig0.Peek)

                if activeStep = 255uy then
                    deFusedVal1 <- v1
                else
                    let mutable same1 = false

                    if (prevMask &&& 1uy) <> 0uy then
                        deFusedCmp <- 2y
                        same1 <- ComparerSentinel.equals eq1 val1 v1
                        deFusedCmp <- -1y

                    if activeStep = 255uy then
                        deFusedVal1 <- v1
                        deFusedCmp <- if same1 then 0y else 1y
                    elif same1 then
                        cutoff <- true
                    else
                        val1 <- v1
                        readyMask <- readyMask ||| 1uy
                        publishedMask <- publishedMask ||| 1uy
                        stepIdx <- 2
                        activeStep <- 2uy
                        runs2 <- runs2 + 1
#if RANVIER_COUNTERS
                        Counters.MemoRecomputed ()
#endif
                        let v2 = FusedStageHelpers.runPureStage (g, n, f2, v1)

                        if activeStep = 255uy then
                            deFusedVal2 <- v2
                        else
                            let mutable same2 = false

                            if (prevMask &&& 2uy) <> 0uy then
                                deFusedCmp <- 2y
                                same2 <- ComparerSentinel.equals eq2 val2 v2
                                deFusedCmp <- -1y

                            if activeStep = 255uy then
                                deFusedVal2 <- v2
                                deFusedCmp <- if same2 then 0y else 1y
                            elif same2 then
                                cutoff <- true
                            else
                                val2 <- v2
                                readyMask <- readyMask ||| 2uy
                                publishedMask <- publishedMask ||| 2uy
                                stepIdx <- 3
                                activeStep <- 3uy
                                n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                                Counters.MemoRecomputed ()
#endif
                                nextVal <- FusedStageHelpers.runPureStage (g, n, f3, v2)
            with ex ->
#if FABLE_COMPILER
                thrownEx <- Thrown.hold ex
#else
                thrownEx <- ex
#endif

            let wasDeFused = activeStep = 255uy
            activeStep <- 0uy

#if !FABLE_COMPILER
            g.LeaveUntrackedPure saved
#endif

            if wasDeFused && stepIdx < 3 then
                this.CompleteDeFused3 (
                    tailMemo,
                    n,
                    outputEqual,
                    previous,
                    previousStatus,
                    previousFailure,
                    prevMask,
                    prevRuns1,
                    prevRuns2,
                    stepIdx,
                    deFusedVal1,
                    deFusedVal2,
                    deFusedCmp,
                    thrownEx
                )
            elif not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
                let ex = Thrown.take thrownEx
#else
                let ex = thrownEx
#endif
                if stepIdx = 1 then
                    readyMask <- 0uy
                elif stepIdx = 2 then
                    readyMask <- readyMask &&& 1uy

                let errOrigin =
                    if stepIdx = 1 then m1 :> INode
                    elif stepIdx = 2 then m2 :> INode
                    else origin

                CombinatorHelpers.recordFusedFailure (g, n, errOrigin, deFusedCmp = 2y, ex, previousFailure)

                let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

                if
                    stepIdx = 1
                    && (prevRuns1 = 0
                        || (prevMask &&& 1uy) <> 0uy
                        || moved)
                then
                    runs2 <- runs2 + 1
                    n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
                    Counters.MemoRecomputed ()
#endif
                elif
                    stepIdx = 2
                    && (prevRuns2 = 0
                        || (prevMask &&& 2uy) <> 0uy
                        || moved)
                then
                    n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
#endif

                this.Moved <- moved
                previous
            elif cutoff then
                CombinatorHelpers.clearActive n
                this.Moved <- false
                previous
            else
                CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, nextVal, previousStatus, previousFailure)

    override _.EvalPrefix(_g, _n, _selfMemo, _prevStatus, _prevFailure) =
        FusedPrefixOutcome<'C>(0uy, Unchecked.defaultof<'C>, false)

    override this.DetachUnwarmedPrefix(selfMemo: Memo<'C>) =
        this.FusedNext <- Platform.nil ()
        (Platform.fastCast<FusedExtendable<'B>> m2.ComputeObj).DetachUnwarmedPrefix m2
        selfMemo.RebindFused (box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, false)), 7uy)

    override this.MaterializeWarmPrefix
        (
            selfMemo: Memo<'C>,
            tailFresh: int,
            tailStatus: Status,
            tailFailure: Failure,
            hasSelfReady: bool,
            hasSelfPublished: bool,
            selfVal: 'C,
            selfRuns: int
        ) =
        this.FusedNext <- Platform.nil ()

        (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
            .MaterializeWarmPrefix(m1, tailFresh, tailStatus, tailFailure, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1)

        (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

        FusedStageHelpers.populateMaterializedStage (
            m2,
            box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
            7uy,
            m1.Node :> ISource,
            m1 :> INode,
            false,
            tailFresh,
            tailStatus,
            tailFailure,
            (readyMask &&& 2uy) <> 0uy,
            (publishedMask &&& 2uy) <> 0uy,
            val2,
            runs2
        )

        FusedStageHelpers.populateMaterializedStage (
            selfMemo,
            box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not selfMemo.Node.IsDisposed)),
            7uy,
            m2.Node :> ISource,
            m2 :> INode,
            false,
            tailFresh,
            tailStatus,
            tailFailure,
            hasSelfReady,
            hasSelfPublished,
            selfVal,
            selfRuns
        )

    override _.MaterializeActivePrefix(_, _, _, _, _, _, _) = ()
    override _.MaterializeWaitingRoot(_, _, _, _, _, _, _) = ()

    override this.MaterializeTail(tailObj: obj) =
        let tailMemo = Platform.fastCast<Memo<'C>> tailObj
        let tn = tailMemo.Node
        let step = activeStep

        if step <> 0uy && step <> 255uy then
            activeStep <- 255uy
            let prevSt = tn.Status
            let prevFail = tn.FailureField

            if step = 1uy then
                (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                    .MaterializeActivePrefix(m1, prevSt, prevFail, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1 - 1)

                (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                FusedStageHelpers.populateWaitingStage (
                    m2,
                    box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                    7uy,
                    m1.Node :> ISource,
                    m1 :> INode,
                    prevSt,
                    prevFail,
                    (readyMask &&& 2uy) <> 0uy,
                    (publishedMask &&& 2uy) <> 0uy,
                    val2,
                    runs2
                )

                FusedStageHelpers.finishActiveTailMaterialization (tailMemo, m2, f3)
            elif step = 2uy then
                (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                    .MaterializeWarmPrefix(m1, NodeFlags.FreshClean, Status.None, null, true, true, val1, runs1)

                (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                FusedStageHelpers.populateActiveStage (
                    m2,
                    box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                    7uy,
                    m1.Node :> ISource,
                    (publishedMask &&& 2uy) <> 0uy,
                    val2,
                    runs2
                )

                FusedStageHelpers.finishActiveTailMaterialization (tailMemo, m2, f3)
            else
                let tailFresh = tn.Flags &&& NodeFlags.FreshMask
                tn.SourcesField.Clear (tn :> IComputation)

                (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                    .MaterializeWarmPrefix(m1, tailFresh, Status.None, null, true, true, val1, runs1)

                (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                FusedStageHelpers.populateMaterializedStage (
                    m2,
                    box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                    7uy,
                    m1.Node :> ISource,
                    m1 :> INode,
                    false,
                    tailFresh,
                    Status.None,
                    null,
                    true,
                    true,
                    val2,
                    runs2
                )

                FusedStageHelpers.finishTailMaterialization (tailMemo, m2, f3, tailFresh, Status.None)
        elif tn.RunsField = 0 then
            this.DetachUnwarmedPrefix tailMemo
        else
            let tailFresh = tn.Flags &&& NodeFlags.FreshMask
            let tailStatus = tn.Status
            let tailFailure = tn.FailureField
            tn.SourcesField.Clear (tn :> IComputation)

            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeWarmPrefix(
                    m1,
                    tailFresh,
                    tailStatus,
                    tailFailure,
                    (readyMask &&& 1uy) <> 0uy,
                    (publishedMask &&& 1uy) <> 0uy,
                    val1,
                    runs1
                )

            (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateMaterializedStage (
                m2,
                box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                7uy,
                m1.Node :> ISource,
                m1 :> INode,
                false,
                tailFresh,
                tailStatus,
                tailFailure,
                (readyMask &&& 2uy) <> 0uy,
                (publishedMask &&& 2uy) <> 0uy,
                val2,
                runs2
            )

            FusedStageHelpers.finishTailMaterialization (tailMemo, m2, f3, tailFresh, tailStatus)

    override _.ClearIntermediates() =
        (Platform.fastCast<FusedRunner<'A>> m1.ComputeObj).FusedNext <- Platform.nil ()
        (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

/// <summary>4-stage unboxed <c>Signal.map |&gt; Memo.map |&gt; Memo.map |&gt; Memo.map</c> runner (<c>computeKind = 6uy</c>, Theorem B18).</summary>
and [<Sealed>] internal FusedSignalMap4<'R, 'A, 'B, 'C, 'D>
    (sig0: Signal<'R>, f1: 'R -> 'A, m1: Memo<'A>, f2: 'A -> 'B, m2: Memo<'B>, f3: 'B -> 'C, m3: Memo<'C>, f4: 'C -> 'D) =
    inherit FusedExtendable<'D>()

    let eq1 = m1.EncodedEqual
    let eq2 = m2.EncodedEqual
    let eq3 = m3.EncodedEqual

    let safeInterEq =
        SafeEquality.isSafe eq1
        && SafeEquality.isSafe eq2
        && SafeEquality.isSafe eq3

    let mutable val1 = Unchecked.defaultof<'A>
    let mutable val2 = Unchecked.defaultof<'B>
    let mutable val3 = Unchecked.defaultof<'C>
    let mutable runs1 = 0
    let mutable runs2 = 0
    let mutable runs3 = 0
    let mutable readyMask = 0uy
    let mutable publishedMask = 0uy
    let mutable activeStep = 0uy
    let mutable linked = false

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.PublishDeFusedPrefixStage
        (
            selfMemo: Memo<'D>,
            previousStatus: Status,
            previousFailure: Failure,
            prevMask: byte,
            prevRuns1: int,
            prevRuns2: int,
            prevRuns3: int,
            prevSelfRuns: int,
            hasSelfReady: bool,
            stepIdx: int,
            deFusedVal1: 'A,
            deFusedVal2: 'B,
            deFusedVal3: 'C,
            deFusedVal4: 'D,
            deFusedCmp: sbyte,
            thrownEx: exn
        ) =
        let ex =
            if Platform.isNull thrownEx then
                Platform.nil ()
            else
#if FABLE_COMPILER
                Thrown.take thrownEx
#else
                thrownEx
#endif

        if stepIdx = 1 then
            FusedStageHelpers.finishDeFusedStage (
                m1,
                deFusedVal1,
                ex,
                prevRuns1,
                (prevMask &&& 1uy) <> 0uy,
                previousStatus,
                previousFailure,
                sig0 :> INode,
                deFusedCmp
            )

            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m2, m1)
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m3, m2)
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (selfMemo, m3)
        elif stepIdx = 2 then
            FusedStageHelpers.finishDeFusedStage (
                m2,
                deFusedVal2,
                ex,
                prevRuns2,
                (prevMask &&& 2uy) <> 0uy,
                previousStatus,
                previousFailure,
                m1 :> INode,
                deFusedCmp
            )

            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m3, m2)
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (selfMemo, m3)
        elif stepIdx = 3 then
            FusedStageHelpers.finishDeFusedStage (
                m3,
                deFusedVal3,
                ex,
                prevRuns3,
                (prevMask &&& 4uy) <> 0uy,
                previousStatus,
                previousFailure,
                m2 :> INode,
                deFusedCmp
            )

            FusedStageHelpers.advanceWaitingStageFromCurrentPred (selfMemo, m3)
        else
            FusedStageHelpers.finishDeFusedStage (
                selfMemo,
                deFusedVal4,
                ex,
                prevSelfRuns,
                hasSelfReady,
                previousStatus,
                previousFailure,
                m3 :> INode,
                deFusedCmp
            )

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CompleteDeFused4
        (
            tailMemo: Memo<'D>,
            n: MemoNode,
            outputEqual: IEqualityComparer<'D>,
            previous: 'D,
            previousStatus: Status,
            previousFailure: Failure,
            prevMask: byte,
            prevRuns1: int,
            prevRuns2: int,
            prevRuns3: int,
            stepIdx: int,
            deFusedVal1: 'A,
            deFusedVal2: 'B,
            deFusedVal3: 'C,
            deFusedCmp: sbyte,
            thrownEx: exn
        ) : 'D =
        let reDirtied = FusedStageHelpers.prepareTailBeforeStagePublish n

        let ex =
            if Platform.isNull thrownEx then
                Platform.nil ()
            else
#if FABLE_COMPILER
                Thrown.take thrownEx
#else
                thrownEx
#endif

        if stepIdx = 1 then
            FusedStageHelpers.finishDeFusedStage (
                m1,
                deFusedVal1,
                ex,
                prevRuns1,
                (prevMask &&& 1uy) <> 0uy,
                previousStatus,
                previousFailure,
                sig0 :> INode,
                deFusedCmp
            )

            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m2, m1)
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m3, m2)
            FusedStageHelpers.finishDeFusedTail (this, tailMemo, m3, n, outputEqual, previous, previousStatus, previousFailure, reDirtied)
        elif stepIdx = 2 then
            FusedStageHelpers.finishDeFusedStage (
                m2,
                deFusedVal2,
                ex,
                prevRuns2,
                (prevMask &&& 2uy) <> 0uy,
                previousStatus,
                previousFailure,
                m1 :> INode,
                deFusedCmp
            )

            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m3, m2)
            FusedStageHelpers.finishDeFusedTail (this, tailMemo, m3, n, outputEqual, previous, previousStatus, previousFailure, reDirtied)
        else
            FusedStageHelpers.finishDeFusedStage (
                m3,
                deFusedVal3,
                ex,
                prevRuns3,
                (prevMask &&& 4uy) <> 0uy,
                previousStatus,
                previousFailure,
                m2 :> INode,
                deFusedCmp
            )

            FusedStageHelpers.finishDeFusedTail (this, tailMemo, m3, n, outputEqual, previous, previousStatus, previousFailure, reDirtied)

    override this.Fuse<'Next>(prevMemo: Memo<'D>, mapping: 'D -> 'Next) =
        struct (FusedChainMap<'D, 'Next>(this, prevMemo, mapping) :> FusedExtendable<'Next>, 7uy)

    override _.LinkRoot(n: MemoNode) =
        if not linked then
            n.SourcesField.BeginRun ()
            NodeOps.AddSource (n, sig0 :> ISource)

            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                linked <- true

    override _.MarkRootDirtyIfLive() =
        if not m1.Node.IsDisposed then
            NodeOps.MemoMarkDirty m1.Node

    override this.EvalSignalLeaf(tailObj, n, g, safeTailEq, outputEqual, previous) =
        let tailMemo = Platform.fastCast<Memo<'D>> tailObj

        if g.HasLastRaised then
            g.ClearLastRaised ()

        g.EnterUntrackedPureTop (n :> IScopeHost)

        n.Flags <-
            (n.Flags
             &&& ~~~(NodeFlags.FreshMask
                     ||| NodeFlags.Unannounced
                     ||| NodeFlags.ActiveMask))
            ||| NodeFlags.ActiveBody

        let prevMask = readyMask
        let prevRuns1 = runs1
        let prevRuns2 = runs2
        let prevRuns3 = runs3
        let mutable stepIdx = 1
        let mutable cutoff = false
        let mutable nextVal = previous
        let mutable deFusedVal1 = Unchecked.defaultof<'A>
        let mutable deFusedVal2 = Unchecked.defaultof<'B>
        let mutable deFusedVal3 = Unchecked.defaultof<'C>
        let mutable deFusedCmp = -1y
        let mutable thrownEx: exn = Platform.nil ()

        try
            if safeInterEq && prevMask = 7uy then
                activeStep <- 1uy
                runs1 <- runs1 + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
#endif
                let v1 = f1 sig0.Peek

                if (n.Flags &&& NodeFlags.Violated) <> 0 then
                    raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                if activeStep = 255uy then
                    deFusedVal1 <- v1
                elif ComparerSentinel.equals null val1 v1 then
                    cutoff <- true
                else
                    val1 <- v1
                    stepIdx <- 2
                    activeStep <- 2uy
                    runs2 <- runs2 + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
#endif
                    let v2 = f2 v1

                    if (n.Flags &&& NodeFlags.Violated) <> 0 then
                        raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                    if activeStep = 255uy then
                        deFusedVal2 <- v2
                    elif ComparerSentinel.equals null val2 v2 then
                        cutoff <- true
                    else
                        val2 <- v2
                        stepIdx <- 3
                        activeStep <- 3uy
                        runs3 <- runs3 + 1
#if RANVIER_COUNTERS
                        Counters.MemoRecomputed ()
#endif
                        let v3 = f3 v2

                        if (n.Flags &&& NodeFlags.Violated) <> 0 then
                            raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                        if activeStep = 255uy then
                            deFusedVal3 <- v3
                        elif ComparerSentinel.equals null val3 v3 then
                            cutoff <- true
                        else
                            val3 <- v3
                            stepIdx <- 4
                            activeStep <- 4uy
                            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
#endif
                            let v4 = f4 v3

                            if (n.Flags &&& NodeFlags.Violated) <> 0 then
                                raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                            nextVal <- v4
            else
                activeStep <- 1uy
                runs1 <- runs1 + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
#endif
                let v1 = f1 sig0.Peek

                if (n.Flags &&& NodeFlags.Violated) <> 0 then
                    raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                if activeStep = 255uy then
                    deFusedVal1 <- v1
                else
                    let mutable same1 = false

                    if (prevMask &&& 1uy) <> 0uy then
                        deFusedCmp <- 2y
                        same1 <- ComparerSentinel.equals eq1 val1 v1
                        deFusedCmp <- -1y

                    if activeStep = 255uy then
                        deFusedVal1 <- v1
                        deFusedCmp <- if same1 then 0y else 1y
                    elif same1 then
                        cutoff <- true
                    else
                        val1 <- v1
                        readyMask <- readyMask ||| 1uy
                        publishedMask <- publishedMask ||| 1uy
                        stepIdx <- 2
                        activeStep <- 2uy
                        runs2 <- runs2 + 1
#if RANVIER_COUNTERS
                        Counters.MemoRecomputed ()
#endif
                        let v2 = f2 v1

                        if (n.Flags &&& NodeFlags.Violated) <> 0 then
                            raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                        if activeStep = 255uy then
                            deFusedVal2 <- v2
                        else
                            let mutable same2 = false

                            if (prevMask &&& 2uy) <> 0uy then
                                deFusedCmp <- 2y
                                same2 <- ComparerSentinel.equals eq2 val2 v2
                                deFusedCmp <- -1y

                            if activeStep = 255uy then
                                deFusedVal2 <- v2
                                deFusedCmp <- if same2 then 0y else 1y
                            elif same2 then
                                cutoff <- true
                            else
                                val2 <- v2
                                readyMask <- readyMask ||| 2uy
                                publishedMask <- publishedMask ||| 2uy
                                stepIdx <- 3
                                activeStep <- 3uy
                                runs3 <- runs3 + 1
#if RANVIER_COUNTERS
                                Counters.MemoRecomputed ()
#endif
                                let v3 = f3 v2

                                if (n.Flags &&& NodeFlags.Violated) <> 0 then
                                    raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                                if activeStep = 255uy then
                                    deFusedVal3 <- v3
                                else
                                    let mutable same3 = false

                                    if (prevMask &&& 4uy) <> 0uy then
                                        deFusedCmp <- 2y
                                        same3 <- ComparerSentinel.equals eq3 val3 v3
                                        deFusedCmp <- -1y

                                    if activeStep = 255uy then
                                        deFusedVal3 <- v3
                                        deFusedCmp <- if same3 then 0y else 1y
                                    elif same3 then
                                        cutoff <- true
                                    else
                                        val3 <- v3
                                        readyMask <- readyMask ||| 4uy
                                        publishedMask <- publishedMask ||| 4uy
                                        stepIdx <- 4
                                        activeStep <- 4uy
                                        n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                                        Counters.MemoRecomputed ()
#endif
                                        let v4 = f4 v3

                                        if (n.Flags &&& NodeFlags.Violated) <> 0 then
                                            raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                                        nextVal <- v4
        with ex ->
#if FABLE_COMPILER
            thrownEx <- Thrown.hold ex
#else
            thrownEx <- ex
#endif

        let wasDeFused = activeStep = 255uy
        activeStep <- 0uy

        if wasDeFused && stepIdx < 4 then
            g.CloseLeafPureBody ()

            let res =
                this.CompleteDeFused4 (
                    tailMemo,
                    n,
                    outputEqual,
                    previous,
                    Status.None,
                    null,
                    prevMask,
                    prevRuns1,
                    prevRuns2,
                    prevRuns3,
                    stepIdx,
                    deFusedVal1,
                    deFusedVal2,
                    deFusedVal3,
                    deFusedCmp,
                    thrownEx
                )

            if (n.Flags &&& NodeFlags.StatusUnreadyMask) = 0 then
                tailMemo.SetMaterializedValue res

            tailMemo.PublishDeFusedMove this.Moved

            g.ExitPullFast ()
            || (n.Flags &&& NodeFlags.FreshMask) <> 0
        elif not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
            let ex = Thrown.take thrownEx
#else
            let ex = thrownEx
#endif
            if stepIdx = 1 then
                readyMask <- 0uy
                runs2 <- runs2 + 1
                runs3 <- runs3 + 1
                n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
                Counters.MemoRecomputed ()
                Counters.MemoRecomputed ()
#endif
            elif stepIdx = 2 then
                readyMask <- readyMask &&& 1uy
                runs3 <- runs3 + 1
                n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
                Counters.MemoRecomputed ()
#endif
            elif stepIdx = 3 then
                readyMask <- readyMask &&& 3uy
                n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
#endif

            let errOrigin =
                if stepIdx = 1 then m1 :> INode
                elif stepIdx = 2 then m2 :> INode
                elif stepIdx = 3 then m3 :> INode
                else tailMemo :> INode

            CombinatorHelpers.recordFusedFailure (g, n, errOrigin, deFusedCmp = 2y, ex, null)

            if (n.Flags &&& NodeFlags.FreshMask) <> 0 then
                n.Flags <- n.Flags ||| NodeFlags.Unannounced
                g.NoteStaleTransition ()

            g.CloseLeafPureBody ()

            g.ExitPullFast ()
            || (n.Flags &&& NodeFlags.FreshMask) <> 0
        else
            FusedStageHelpers.finishLeafSuccess (tailMemo, n, g, safeTailEq, outputEqual, previous, cutoff, nextVal)

    override this.ExecuteFused(tailObj, n, outputEqual, previous, previousStatus, previousFailure) =
        let tailMemo = Platform.fastCast<Memo<'D>> tailObj
        let origin = tailMemo :> INode
        let g = n.Graph
        this.LinkRoot n

        if n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, n.PendingSourceField)
            this.Moved <- false
            previous
        else
            if g.HasLastRaised then
                g.ClearLastRaised ()

            let prevMask = readyMask
            let prevRuns1 = runs1
            let prevRuns2 = runs2
            let prevRuns3 = runs3
            let mutable stepIdx = 1
            let mutable cutoff = false
            let mutable nextVal = previous
            let mutable deFusedVal1 = Unchecked.defaultof<'A>
            let mutable deFusedVal2 = Unchecked.defaultof<'B>
            let mutable deFusedVal3 = Unchecked.defaultof<'C>
            let mutable deFusedCmp = -1y

#if !FABLE_COMPILER
            let saved = g.EnterUntrackedPure (n :> IScopeHost)
#endif
            let mutable thrownEx: exn = Platform.nil ()

            try
                activeStep <- 1uy
                runs1 <- runs1 + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
#endif
                let v1 = FusedStageHelpers.runPureStage (g, n, f1, sig0.Peek)

                if activeStep = 255uy then
                    deFusedVal1 <- v1
                else
                    let mutable same1 = false

                    if (prevMask &&& 1uy) <> 0uy then
                        deFusedCmp <- 2y
                        same1 <- ComparerSentinel.equals eq1 val1 v1
                        deFusedCmp <- -1y

                    if activeStep = 255uy then
                        deFusedVal1 <- v1
                        deFusedCmp <- if same1 then 0y else 1y
                    elif same1 then
                        cutoff <- true
                    else
                        val1 <- v1
                        readyMask <- readyMask ||| 1uy
                        publishedMask <- publishedMask ||| 1uy
                        stepIdx <- 2
                        activeStep <- 2uy
                        runs2 <- runs2 + 1
#if RANVIER_COUNTERS
                        Counters.MemoRecomputed ()
#endif
                        let v2 = FusedStageHelpers.runPureStage (g, n, f2, v1)

                        if activeStep = 255uy then
                            deFusedVal2 <- v2
                        else
                            let mutable same2 = false

                            if (prevMask &&& 2uy) <> 0uy then
                                deFusedCmp <- 2y
                                same2 <- ComparerSentinel.equals eq2 val2 v2
                                deFusedCmp <- -1y

                            if activeStep = 255uy then
                                deFusedVal2 <- v2
                                deFusedCmp <- if same2 then 0y else 1y
                            elif same2 then
                                cutoff <- true
                            else
                                val2 <- v2
                                readyMask <- readyMask ||| 2uy
                                publishedMask <- publishedMask ||| 2uy
                                stepIdx <- 3
                                activeStep <- 3uy
                                runs3 <- runs3 + 1
#if RANVIER_COUNTERS
                                Counters.MemoRecomputed ()
#endif
                                let v3 = FusedStageHelpers.runPureStage (g, n, f3, v2)

                                if activeStep = 255uy then
                                    deFusedVal3 <- v3
                                else
                                    let mutable same3 = false

                                    if (prevMask &&& 4uy) <> 0uy then
                                        deFusedCmp <- 2y
                                        same3 <- ComparerSentinel.equals eq3 val3 v3
                                        deFusedCmp <- -1y

                                    if activeStep = 255uy then
                                        deFusedVal3 <- v3
                                        deFusedCmp <- if same3 then 0y else 1y
                                    elif same3 then
                                        cutoff <- true
                                    else
                                        val3 <- v3
                                        readyMask <- readyMask ||| 4uy
                                        publishedMask <- publishedMask ||| 4uy
                                        stepIdx <- 4
                                        activeStep <- 4uy
                                        n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                                        Counters.MemoRecomputed ()
#endif
                                        nextVal <- FusedStageHelpers.runPureStage (g, n, f4, v3)
            with ex ->
#if FABLE_COMPILER
                thrownEx <- Thrown.hold ex
#else
                thrownEx <- ex
#endif

            let wasDeFused = activeStep = 255uy
            activeStep <- 0uy

#if !FABLE_COMPILER
            g.LeaveUntrackedPure saved
#endif

            if wasDeFused && stepIdx < 4 then
                this.CompleteDeFused4 (
                    tailMemo,
                    n,
                    outputEqual,
                    previous,
                    previousStatus,
                    previousFailure,
                    prevMask,
                    prevRuns1,
                    prevRuns2,
                    prevRuns3,
                    stepIdx,
                    deFusedVal1,
                    deFusedVal2,
                    deFusedVal3,
                    deFusedCmp,
                    thrownEx
                )
            elif not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
                let ex = Thrown.take thrownEx
#else
                let ex = thrownEx
#endif
                if stepIdx = 1 then
                    readyMask <- 0uy
                elif stepIdx = 2 then
                    readyMask <- readyMask &&& 1uy
                elif stepIdx = 3 then
                    readyMask <- readyMask &&& 3uy

                let errOrigin =
                    if stepIdx = 1 then m1 :> INode
                    elif stepIdx = 2 then m2 :> INode
                    elif stepIdx = 3 then m3 :> INode
                    else origin

                CombinatorHelpers.recordFusedFailure (g, n, errOrigin, deFusedCmp = 2y, ex, previousFailure)

                let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

                if
                    stepIdx = 1
                    && (prevRuns1 = 0
                        || (prevMask &&& 1uy) <> 0uy
                        || moved)
                then
                    runs2 <- runs2 + 1
                    runs3 <- runs3 + 1
                    n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
                    Counters.MemoRecomputed ()
                    Counters.MemoRecomputed ()
#endif
                elif
                    stepIdx = 2
                    && (prevRuns2 = 0
                        || (prevMask &&& 2uy) <> 0uy
                        || moved)
                then
                    runs3 <- runs3 + 1
                    n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
                    Counters.MemoRecomputed ()
#endif
                elif
                    stepIdx = 3
                    && (prevRuns3 = 0
                        || (prevMask &&& 4uy) <> 0uy
                        || moved)
                then
                    n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
#endif

                this.Moved <- moved
                previous
            elif cutoff then
                CombinatorHelpers.clearActive n
                this.Moved <- false
                previous
            else
                CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, nextVal, previousStatus, previousFailure)

    override this.EvalPrefix(g, n, selfMemo, previousStatus, previousFailure) =
        if g.HasLastRaised then
            g.ClearLastRaised ()

        let prevMask = readyMask
        let prevRuns1 = runs1
        let prevRuns2 = runs2
        let prevRuns3 = runs3
        let mutable stepIdx = 1
        let mutable cutoff = false
        let mutable nextVal = Unchecked.defaultof<'D>
        let mutable deFusedVal1 = Unchecked.defaultof<'A>
        let mutable deFusedVal2 = Unchecked.defaultof<'B>
        let mutable deFusedVal3 = Unchecked.defaultof<'C>
        let mutable deFusedVal4 = Unchecked.defaultof<'D>
        let mutable deFusedCmp = -1y

#if !FABLE_COMPILER
        let saved = g.EnterUntrackedPure (n :> IScopeHost)
#endif
        let mutable thrownEx: exn = Platform.nil ()

        try
            activeStep <- 1uy
            runs1 <- runs1 + 1
#if RANVIER_COUNTERS
            Counters.MemoRecomputed ()
#endif
            let v1 = FusedStageHelpers.runPureStage (g, n, f1, sig0.Peek)

            if activeStep = 255uy then
                deFusedVal1 <- v1
            else
                let mutable same1 = false

                if (prevMask &&& 1uy) <> 0uy then
                    deFusedCmp <- 2y
                    same1 <- ComparerSentinel.equals eq1 val1 v1
                    deFusedCmp <- -1y

                if activeStep = 255uy then
                    deFusedVal1 <- v1
                    deFusedCmp <- if same1 then 0y else 1y
                elif same1 then
                    cutoff <- true
                else
                    val1 <- v1
                    readyMask <- readyMask ||| 1uy
                    publishedMask <- publishedMask ||| 1uy
                    stepIdx <- 2
                    activeStep <- 2uy
                    runs2 <- runs2 + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
#endif
                    let v2 = FusedStageHelpers.runPureStage (g, n, f2, v1)

                    if activeStep = 255uy then
                        deFusedVal2 <- v2
                    else
                        let mutable same2 = false

                        if (prevMask &&& 2uy) <> 0uy then
                            deFusedCmp <- 2y
                            same2 <- ComparerSentinel.equals eq2 val2 v2
                            deFusedCmp <- -1y

                        if activeStep = 255uy then
                            deFusedVal2 <- v2
                            deFusedCmp <- if same2 then 0y else 1y
                        elif same2 then
                            cutoff <- true
                        else
                            val2 <- v2
                            readyMask <- readyMask ||| 2uy
                            publishedMask <- publishedMask ||| 2uy
                            stepIdx <- 3
                            activeStep <- 3uy
                            runs3 <- runs3 + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
#endif
                            let v3 = FusedStageHelpers.runPureStage (g, n, f3, v2)

                            if activeStep = 255uy then
                                deFusedVal3 <- v3
                            else
                                let mutable same3 = false

                                if (prevMask &&& 4uy) <> 0uy then
                                    deFusedCmp <- 2y
                                    same3 <- ComparerSentinel.equals eq3 val3 v3
                                    deFusedCmp <- -1y

                                if activeStep = 255uy then
                                    deFusedVal3 <- v3
                                    deFusedCmp <- if same3 then 0y else 1y
                                elif same3 then
                                    cutoff <- true
                                else
                                    val3 <- v3
                                    readyMask <- readyMask ||| 4uy
                                    publishedMask <- publishedMask ||| 4uy
                                    stepIdx <- 4
                                    activeStep <- 4uy
                                    let v4 = FusedStageHelpers.runPureStage (g, n, f4, v3)

                                    if activeStep = 255uy then
                                        deFusedVal4 <- v4
                                    else
                                        nextVal <- v4
        with ex ->
#if FABLE_COMPILER
            thrownEx <- Thrown.hold ex
#else
            thrownEx <- ex
#endif

        let wasDeFused = activeStep = 255uy
        activeStep <- 0uy

#if !FABLE_COMPILER
        g.LeaveUntrackedPure saved
#endif

        if wasDeFused then
            let reDirtied = FusedStageHelpers.prepareTailBeforeStagePublish n
            let hasSelfReadyBefore = (readyMask &&& 8uy) <> 0uy
            readyMask <- readyMask &&& 7uy

            let prevSelfRuns =
                if stepIdx = 4 then
                    selfMemo.Node.RunsField - 1
                else
                    selfMemo.Node.RunsField

            this.PublishDeFusedPrefixStage (
                selfMemo,
                previousStatus,
                previousFailure,
                prevMask,
                prevRuns1,
                prevRuns2,
                prevRuns3,
                prevSelfRuns,
                hasSelfReadyBefore,
                stepIdx,
                deFusedVal1,
                deFusedVal2,
                deFusedVal3,
                deFusedVal4,
                deFusedCmp,
                thrownEx
            )

            FusedPrefixOutcome<'D>(4uy, Unchecked.defaultof<'D>, reDirtied)
        elif not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
            let ex = Thrown.take thrownEx
#else
            let ex = thrownEx
#endif
            if stepIdx = 1 then
                readyMask <- 0uy
            elif stepIdx = 2 then
                readyMask <- readyMask &&& 1uy
            elif stepIdx = 3 then
                readyMask <- readyMask &&& 3uy

            let errOrigin =
                if stepIdx = 1 then m1 :> INode
                elif stepIdx = 2 then m2 :> INode
                elif stepIdx = 3 then m3 :> INode
                else selfMemo :> INode

            CombinatorHelpers.recordFusedFailure (g, n, errOrigin, deFusedCmp = 2y, ex, previousFailure)

            let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

            let unreadyMoved =
                if stepIdx = 1 then
                    let m =
                        prevRuns1 = 0
                        || (prevMask &&& 1uy) <> 0uy
                        || moved

                    if m then
                        runs2 <- runs2 + 1
                        runs3 <- runs3 + 1
#if RANVIER_COUNTERS
                        Counters.MemoRecomputed ()
                        Counters.MemoRecomputed ()
#endif

                    m
                elif stepIdx = 2 then
                    let m =
                        prevRuns2 = 0
                        || (prevMask &&& 2uy) <> 0uy
                        || moved

                    if m then
                        runs3 <- runs3 + 1
#if RANVIER_COUNTERS
                        Counters.MemoRecomputed ()
#endif

                    m
                elif stepIdx = 3 then
                    prevRuns3 = 0
                    || (prevMask &&& 4uy) <> 0uy
                    || moved
                else
                    moved

            FusedPrefixOutcome<'D>(3uy, Unchecked.defaultof<'D>, unreadyMoved)
        elif cutoff then
            FusedPrefixOutcome<'D>(0uy, Unchecked.defaultof<'D>, false)
        else
            FusedPrefixOutcome<'D>(1uy, nextVal, false)

    override this.DetachUnwarmedPrefix(selfMemo: Memo<'D>) =
        this.FusedNext <- Platform.nil ()
        (Platform.fastCast<FusedExtendable<'C>> m3.ComputeObj).DetachUnwarmedPrefix m3
        selfMemo.RebindFused (box (FusedSourceMap1<'C, 'D>(MemoSource<'C>(m3), f4, false)), 7uy)

    override this.MaterializeWarmPrefix
        (
            selfMemo: Memo<'D>,
            tailFresh: int,
            tailStatus: Status,
            tailFailure: Failure,
            hasSelfReady: bool,
            hasSelfPublished: bool,
            selfVal: 'D,
            selfRuns: int
        ) =
        this.FusedNext <- Platform.nil ()

        (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
            .MaterializeWarmPrefix(m1, tailFresh, tailStatus, tailFailure, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1)

        (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

        FusedStageHelpers.populateMaterializedStage (
            m2,
            box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
            7uy,
            m1.Node :> ISource,
            m1 :> INode,
            false,
            tailFresh,
            tailStatus,
            tailFailure,
            (readyMask &&& 2uy) <> 0uy,
            (publishedMask &&& 2uy) <> 0uy,
            val2,
            runs2
        )

        (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

        FusedStageHelpers.populateMaterializedStage (
            m3,
            box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
            7uy,
            m2.Node :> ISource,
            m2 :> INode,
            false,
            tailFresh,
            tailStatus,
            tailFailure,
            (readyMask &&& 4uy) <> 0uy,
            (publishedMask &&& 4uy) <> 0uy,
            val3,
            runs3
        )

        FusedStageHelpers.populateMaterializedStage (
            selfMemo,
            box (FusedSourceMap1<'C, 'D>(MemoSource<'C>(m3), f4, not selfMemo.Node.IsDisposed)),
            7uy,
            m3.Node :> ISource,
            m3 :> INode,
            false,
            tailFresh,
            tailStatus,
            tailFailure,
            hasSelfReady,
            hasSelfPublished,
            selfVal,
            selfRuns
        )

    override this.MaterializeActivePrefix
        (
            selfMemo: Memo<'D>,
            prevTailStatus: Status,
            prevTailFailure: Failure,
            hasSelfReady: bool,
            hasSelfPublished: bool,
            prevSelfVal: 'D,
            prevSelfRuns: int
        ) =
        let step = activeStep
        activeStep <- 255uy
        this.FusedNext <- Platform.nil ()

        if step = 254uy then
            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeWaitingRoot(m1, prevTailStatus, prevTailFailure, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1)

            (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateWaitingStage (
                m2,
                box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                7uy,
                m1.Node :> ISource,
                m1 :> INode,
                prevTailStatus,
                prevTailFailure,
                (readyMask &&& 2uy) <> 0uy,
                (publishedMask &&& 2uy) <> 0uy,
                val2,
                runs2
            )

            (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateWaitingStage (
                m3,
                box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                7uy,
                m2.Node :> ISource,
                m2 :> INode,
                prevTailStatus,
                prevTailFailure,
                (readyMask &&& 4uy) <> 0uy,
                (publishedMask &&& 4uy) <> 0uy,
                val3,
                runs3
            )

            FusedStageHelpers.populateWaitingStage (
                selfMemo,
                box (FusedSourceMap1<'C, 'D>(MemoSource<'C>(m3), f4, not selfMemo.Node.IsDisposed)),
                7uy,
                m3.Node :> ISource,
                m3 :> INode,
                prevTailStatus,
                prevTailFailure,
                hasSelfReady,
                hasSelfPublished,
                prevSelfVal,
                prevSelfRuns
            )
        elif step = 1uy then
            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeActivePrefix(
                    m1,
                    prevTailStatus,
                    prevTailFailure,
                    (readyMask &&& 1uy) <> 0uy,
                    (publishedMask &&& 1uy) <> 0uy,
                    val1,
                    runs1 - 1
                )

            (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateWaitingStage (
                m2,
                box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                7uy,
                m1.Node :> ISource,
                m1 :> INode,
                prevTailStatus,
                prevTailFailure,
                (readyMask &&& 2uy) <> 0uy,
                (publishedMask &&& 2uy) <> 0uy,
                val2,
                runs2
            )

            (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateWaitingStage (
                m3,
                box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                7uy,
                m2.Node :> ISource,
                m2 :> INode,
                prevTailStatus,
                prevTailFailure,
                (readyMask &&& 4uy) <> 0uy,
                (publishedMask &&& 4uy) <> 0uy,
                val3,
                runs3
            )

            FusedStageHelpers.populateWaitingStage (
                selfMemo,
                box (FusedSourceMap1<'C, 'D>(MemoSource<'C>(m3), f4, not selfMemo.Node.IsDisposed)),
                7uy,
                m3.Node :> ISource,
                m3 :> INode,
                prevTailStatus,
                prevTailFailure,
                hasSelfReady,
                hasSelfPublished,
                prevSelfVal,
                prevSelfRuns
            )
        elif step = 2uy then
            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeWarmPrefix(m1, NodeFlags.FreshClean, Status.None, null, true, true, val1, runs1)

            (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateActiveStage (
                m2,
                box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                7uy,
                m1.Node :> ISource,
                (publishedMask &&& 2uy) <> 0uy,
                val2,
                runs2
            )

            (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateWaitingStage (
                m3,
                box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                7uy,
                m2.Node :> ISource,
                m2 :> INode,
                prevTailStatus,
                prevTailFailure,
                (readyMask &&& 4uy) <> 0uy,
                (publishedMask &&& 4uy) <> 0uy,
                val3,
                runs3
            )

            FusedStageHelpers.populateWaitingStage (
                selfMemo,
                box (FusedSourceMap1<'C, 'D>(MemoSource<'C>(m3), f4, not selfMemo.Node.IsDisposed)),
                7uy,
                m3.Node :> ISource,
                m3 :> INode,
                prevTailStatus,
                prevTailFailure,
                hasSelfReady,
                hasSelfPublished,
                prevSelfVal,
                prevSelfRuns
            )
        elif step = 3uy then
            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeWarmPrefix(m1, NodeFlags.FreshClean, Status.None, null, true, true, val1, runs1)

            (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateMaterializedStage (
                m2,
                box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                7uy,
                m1.Node :> ISource,
                m1 :> INode,
                false,
                NodeFlags.FreshClean,
                Status.None,
                null,
                true,
                true,
                val2,
                runs2
            )

            (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateActiveStage (
                m3,
                box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                7uy,
                m2.Node :> ISource,
                (publishedMask &&& 4uy) <> 0uy,
                val3,
                runs3
            )

            FusedStageHelpers.populateWaitingStage (
                selfMemo,
                box (FusedSourceMap1<'C, 'D>(MemoSource<'C>(m3), f4, not selfMemo.Node.IsDisposed)),
                7uy,
                m3.Node :> ISource,
                m3 :> INode,
                prevTailStatus,
                prevTailFailure,
                hasSelfReady,
                hasSelfPublished,
                prevSelfVal,
                prevSelfRuns
            )
        else
            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeWarmPrefix(m1, NodeFlags.FreshClean, Status.None, null, true, true, val1, runs1)

            (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateMaterializedStage (
                m2,
                box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                7uy,
                m1.Node :> ISource,
                m1 :> INode,
                false,
                NodeFlags.FreshClean,
                Status.None,
                null,
                true,
                true,
                val2,
                runs2
            )

            (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateMaterializedStage (
                m3,
                box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                7uy,
                m2.Node :> ISource,
                m2 :> INode,
                false,
                NodeFlags.FreshClean,
                Status.None,
                null,
                true,
                true,
                val3,
                runs3
            )

            FusedStageHelpers.populateActiveStage (
                selfMemo,
                box (FusedSourceMap1<'C, 'D>(MemoSource<'C>(m3), f4, not selfMemo.Node.IsDisposed)),
                7uy,
                m3.Node :> ISource,
                hasSelfPublished,
                prevSelfVal,
                prevSelfRuns + 1
            )

            readyMask <-
                if hasSelfReady then
                    readyMask ||| 8uy
                else
                    readyMask &&& ~~~8uy

    override _.MaterializeWaitingRoot(_, _, _, _, _, _, _) = ()

    override this.MaterializeTail(tailObj: obj) =
        let tailMemo = Platform.fastCast<Memo<'D>> tailObj
        let tn = tailMemo.Node
        let step = activeStep

        if step <> 0uy && step <> 255uy then
            let prevSt = tn.Status
            let prevFail = tn.FailureField

            if step < 4uy then
                activeStep <- 255uy

                if step = 1uy then
                    (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                        .MaterializeActivePrefix(m1, prevSt, prevFail, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1 - 1)

                    (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                    FusedStageHelpers.populateWaitingStage (
                        m2,
                        box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                        7uy,
                        m1.Node :> ISource,
                        m1 :> INode,
                        prevSt,
                        prevFail,
                        (readyMask &&& 2uy) <> 0uy,
                        (publishedMask &&& 2uy) <> 0uy,
                        val2,
                        runs2
                    )

                    (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

                    FusedStageHelpers.populateWaitingStage (
                        m3,
                        box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                        7uy,
                        m2.Node :> ISource,
                        m2 :> INode,
                        prevSt,
                        prevFail,
                        (readyMask &&& 4uy) <> 0uy,
                        (publishedMask &&& 4uy) <> 0uy,
                        val3,
                        runs3
                    )
                elif step = 2uy then
                    (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                        .MaterializeWarmPrefix(m1, NodeFlags.FreshClean, Status.None, null, true, true, val1, runs1)

                    (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                    FusedStageHelpers.populateActiveStage (
                        m2,
                        box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                        7uy,
                        m1.Node :> ISource,
                        (publishedMask &&& 2uy) <> 0uy,
                        val2,
                        runs2
                    )

                    (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

                    FusedStageHelpers.populateWaitingStage (
                        m3,
                        box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                        7uy,
                        m2.Node :> ISource,
                        m2 :> INode,
                        prevSt,
                        prevFail,
                        (readyMask &&& 4uy) <> 0uy,
                        (publishedMask &&& 4uy) <> 0uy,
                        val3,
                        runs3
                    )
                else
                    (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                        .MaterializeWarmPrefix(m1, NodeFlags.FreshClean, Status.None, null, true, true, val1, runs1)

                    (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                    FusedStageHelpers.populateMaterializedStage (
                        m2,
                        box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                        7uy,
                        m1.Node :> ISource,
                        m1 :> INode,
                        false,
                        NodeFlags.FreshClean,
                        Status.None,
                        null,
                        true,
                        true,
                        val2,
                        runs2
                    )

                    (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

                    FusedStageHelpers.populateActiveStage (
                        m3,
                        box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                        7uy,
                        m2.Node :> ISource,
                        (publishedMask &&& 4uy) <> 0uy,
                        val3,
                        runs3
                    )

                FusedStageHelpers.finishActiveTailMaterialization (tailMemo, m3, f4)
            else
                activeStep <- 255uy
                let tailFresh = tn.Flags &&& NodeFlags.FreshMask
                tn.SourcesField.Clear (tn :> IComputation)

                (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                    .MaterializeWarmPrefix(m1, tailFresh, Status.None, null, true, true, val1, runs1)

                (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                FusedStageHelpers.populateMaterializedStage (
                    m2,
                    box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                    7uy,
                    m1.Node :> ISource,
                    m1 :> INode,
                    false,
                    tailFresh,
                    Status.None,
                    null,
                    true,
                    true,
                    val2,
                    runs2
                )

                (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

                FusedStageHelpers.populateMaterializedStage (
                    m3,
                    box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                    7uy,
                    m2.Node :> ISource,
                    m2 :> INode,
                    false,
                    tailFresh,
                    Status.None,
                    null,
                    true,
                    true,
                    val3,
                    runs3
                )

                FusedStageHelpers.finishTailMaterialization (tailMemo, m3, f4, tailFresh, Status.None)
        elif tn.RunsField = 0 then
            this.DetachUnwarmedPrefix tailMemo
        else
            let tailFresh = tn.Flags &&& NodeFlags.FreshMask
            let tailStatus = tn.Status
            let tailFailure = tn.FailureField
            tn.SourcesField.Clear (tn :> IComputation)

            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeWarmPrefix(
                    m1,
                    tailFresh,
                    tailStatus,
                    tailFailure,
                    (readyMask &&& 1uy) <> 0uy,
                    (publishedMask &&& 1uy) <> 0uy,
                    val1,
                    runs1
                )

            (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateMaterializedStage (
                m2,
                box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                7uy,
                m1.Node :> ISource,
                m1 :> INode,
                false,
                tailFresh,
                tailStatus,
                tailFailure,
                (readyMask &&& 2uy) <> 0uy,
                (publishedMask &&& 2uy) <> 0uy,
                val2,
                runs2
            )

            (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateMaterializedStage (
                m3,
                box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                7uy,
                m2.Node :> ISource,
                m2 :> INode,
                false,
                tailFresh,
                tailStatus,
                tailFailure,
                (readyMask &&& 4uy) <> 0uy,
                (publishedMask &&& 4uy) <> 0uy,
                val3,
                runs3
            )

            FusedStageHelpers.finishTailMaterialization (tailMemo, m3, f4, tailFresh, tailStatus)

    override _.ClearIntermediates() =
        (Platform.fastCast<FusedRunner<'A>> m1.ComputeObj).FusedNext <- Platform.nil ()
        (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()
        (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

/// <summary>1-stage fusable <c>map</c> runner over <c>CombinatorSource</c> (<c>computeKind = 7uy</c>).</summary>
and [<Sealed>] internal FusedSourceMap1<'R, 'A>(root: CombinatorSource<'R>, f1: 'R -> 'A, initialLinked: bool) =
    inherit FusedExtendable<'A>()

    let mutable linked = initialLinked
    let mutable preRefreshed = false

    override _.SetPreRefreshed() =
        preRefreshed <- true

    override _.Fuse<'Next>(prevMemo: Memo<'A>, mapping: 'A -> 'Next) =
        struct (FusedSourceMap2<'R, 'A, 'Next>(root, f1, prevMemo, mapping) :> FusedExtendable<'Next>, 7uy)

    override _.LinkRoot(n: MemoNode) =
        if not linked then
            n.SourcesField.BeginRun ()
            NodeOps.AddSource (n, root.Source)

            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                linked <- true

    override _.MarkRootDirtyIfLive() = ()

    override _.EvalSignalLeaf(_tailObj, _n, _g, _safeTailEq, _outputEqual, _previous) = false

    override this.ExecuteFused(tailObj, n, outputEqual, previous, previousStatus, previousFailure) =
        let origin = Platform.fastCast<INode> tailObj
        let g = n.Graph
        this.LinkRoot n

        if preRefreshed then
            preRefreshed <- false
            root.MarkStaleAfterPreRefresh n

            if not n.IsDisposed then
                n.SourcesField.SetCursor n.SourcesField.Count
        elif not n.IsDisposed then
            CombinatorHelpers.refreshStaticSlot (g, n, 0, fun () -> root.RefreshAndCheckStale n)

        if n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, n.PendingSourceField)
            this.Moved <- false
            previous
        else
            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
            Counters.MemoRecomputed ()
#endif
            let st = root.Status

            if StatusBits.isUnready st then
                CombinatorHelpers.clearActive n

                if StatusBits.isPending st then
                    n.FailureField <- Platform.nil ()
                    n.PendingSourceField <- root.Node
                    n.SetStatusPending ()
                else
                    n.PendingSourceField <- Platform.nil ()
                    n.FailureField <- root.Failure
                    n.SetStatusError ()

                this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                previous
            else
                if g.HasLastRaised then
                    g.ClearLastRaised ()

                let mutable nextVal = Unchecked.defaultof<'A>

#if !FABLE_COMPILER
                let saved = g.EnterUntrackedPure (n :> IScopeHost)
#endif
                let mutable thrownEx: exn = Platform.nil ()

                try
                    nextVal <- FusedStageHelpers.runPureStage (g, n, f1, root.Peek)
                with ex ->
#if FABLE_COMPILER
                    thrownEx <- Thrown.hold ex
#else
                    thrownEx <- ex
#endif

#if !FABLE_COMPILER
                g.LeaveUntrackedPure saved
#endif

                if not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
                    let ex = Thrown.take thrownEx
#else
                    let ex = thrownEx
#endif
                    CombinatorHelpers.recordCallbackFailure (g, n, origin, UntrackedPhase.mapPending, ex, previousFailure)
                    |> ignore

                    this.Moved <- CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)
                    previous
                else
                    CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, nextVal, previousStatus, previousFailure)

    override _.EvalPrefix(_g, _n, _selfMemo, _prevStatus, _prevFailure) =
        FusedPrefixOutcome<'A>(0uy, Unchecked.defaultof<'A>, false)

    override this.DetachUnwarmedPrefix(_selfMemo: Memo<'A>) =
        this.FusedNext <- Platform.nil ()

    override this.MaterializeWarmPrefix
        (
            selfMemo: Memo<'A>,
            tailFresh: int,
            tailStatus: Status,
            tailFailure: Failure,
            hasSelfReady: bool,
            hasSelfPublished: bool,
            selfVal: 'A,
            selfRuns: int
        ) =
        this.FusedNext <- Platform.nil ()
        linked <- not selfMemo.Node.IsDisposed

        FusedStageHelpers.populateMaterializedStage (
            selfMemo,
            box this,
            7uy,
            root.Source,
            root.Node,
            true,
            tailFresh,
            tailStatus,
            tailFailure,
            hasSelfReady,
            hasSelfPublished,
            selfVal,
            selfRuns
        )

    override this.MaterializeActivePrefix
        (
            selfMemo: Memo<'A>,
            _prevTailStatus: Status,
            _prevTailFailure: Failure,
            _hasSelfReady: bool,
            hasSelfPublished: bool,
            prevSelfVal: 'A,
            prevSelfRuns: int
        ) =
        this.FusedNext <- Platform.nil ()
        linked <- not selfMemo.Node.IsDisposed
        FusedStageHelpers.populateActiveStage (selfMemo, box this, 7uy, root.Source, hasSelfPublished, prevSelfVal, prevSelfRuns + 1)

    override this.MaterializeWaitingRoot
        (
            selfMemo: Memo<'A>,
            prevTailStatus: Status,
            prevTailFailure: Failure,
            hasSelfReady: bool,
            hasSelfPublished: bool,
            prevSelfVal: 'A,
            prevSelfRuns: int
        ) =
        this.FusedNext <- Platform.nil ()
        linked <- not selfMemo.Node.IsDisposed

        FusedStageHelpers.populateWaitingRoot (
            selfMemo,
            box this,
            7uy,
            root.Source,
            root.Node,
            prevTailStatus,
            prevTailFailure,
            hasSelfReady,
            hasSelfPublished,
            prevSelfVal,
            prevSelfRuns
        )

    override _.MaterializeTail(_tailMemo: obj) = ()
    override _.ClearIntermediates() = ()

/// <summary>2-stage unboxed <c>map</c> runner over <c>CombinatorSource</c> (<c>computeKind = 7uy</c>, Theorem B18).</summary>
and [<Sealed>] internal FusedSourceMap2<'R, 'A, 'B>(root: CombinatorSource<'R>, f1: 'R -> 'A, m1: Memo<'A>, f2: 'A -> 'B) =
    inherit FusedExtendable<'B>()

    let eq1 = m1.EncodedEqual
    let mutable val1 = Unchecked.defaultof<'A>
    let mutable runs1 = 0
    let mutable readyMask = 0uy
    let mutable publishedMask = 0uy
    let mutable activeStep = 0uy
    let mutable linked = false

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CompleteDeFused2
        (
            tailMemo: Memo<'B>,
            n: MemoNode,
            outputEqual: IEqualityComparer<'B>,
            previous: 'B,
            previousStatus: Status,
            previousFailure: Failure,
            prevReady1: bool,
            prevRuns1: int,
            deFusedVal1: 'A,
            deFusedCmp1: sbyte,
            thrownEx: exn
        ) : 'B =
        let reDirtied = FusedStageHelpers.prepareTailBeforeStagePublish n

        let ex =
            if Platform.isNull thrownEx then
                Platform.nil ()
            else
#if FABLE_COMPILER
                Thrown.take thrownEx
#else
                thrownEx
#endif

        FusedStageHelpers.finishDeFusedStage (m1, deFusedVal1, ex, prevRuns1, prevReady1, previousStatus, previousFailure, root.Node, deFusedCmp1)

        FusedStageHelpers.finishDeFusedTail (this, tailMemo, m1, n, outputEqual, previous, previousStatus, previousFailure, reDirtied)

    override _.Fuse<'Next>(prevMemo: Memo<'B>, mapping: 'B -> 'Next) =
        struct (FusedSourceMap3<'R, 'A, 'B, 'Next>(root, f1, m1, f2, prevMemo, mapping) :> FusedExtendable<'Next>, 7uy)

    override _.LinkRoot(n: MemoNode) =
        if not linked then
            n.SourcesField.BeginRun ()
            NodeOps.AddSource (n, root.Source)

            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                linked <- true

    override _.MarkRootDirtyIfLive() =
        if not m1.Node.IsDisposed then
            NodeOps.MemoMarkDirty m1.Node

    override _.EvalSignalLeaf(_tailObj, _n, _g, _safeTailEq, _outputEqual, _previous) = false

    override this.ExecuteFused(tailObj, n, outputEqual, previous, previousStatus, previousFailure) =
        let tailMemo = Platform.fastCast<Memo<'B>> tailObj
        let origin = tailMemo :> INode
        let g = n.Graph
        this.LinkRoot n

        if not n.IsDisposed then
            activeStep <- 254uy
            CombinatorHelpers.refreshStaticSlot (g, n, 0, fun () -> root.RefreshAndCheckStale n)

        if activeStep = 255uy then
            activeStep <- 0uy

            FusedStageHelpers.prepareTailBeforeStagePublish n
            |> ignore

            FusedStageHelpers.advanceWaitingRootFromCurrentSource m1

            FusedStageHelpers.finishDeFusedTail (this, tailMemo, m1, n, outputEqual, previous, previousStatus, previousFailure, false)
        else
            activeStep <- 0uy

            if n.IsDisposed then
                CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, n.PendingSourceField)
                this.Moved <- false
                previous
            else
                let st = root.Status

                if StatusBits.isUnready st then
                    let wasStage1Unready = (readyMask &&& 1uy) = 0uy && runs1 > 0
                    readyMask <- 0uy
                    runs1 <- runs1 + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
#endif
                    CombinatorHelpers.clearActive n

                    if StatusBits.isPending st then
                        n.FailureField <- Platform.nil ()
                        n.PendingSourceField <- m1 :> INode
                        n.SetStatusPending ()
                        let moved = not (StatusBits.isPending previousStatus)

                        if not wasStage1Unready || moved then
                            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
#endif

                        this.Moved <- moved
                    else
                        let rf = root.Failure
                        n.PendingSourceField <- Platform.nil ()
                        n.FailureField <- rf
                        n.SetStatusError ()
                        let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

                        if not wasStage1Unready || moved then
                            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
#endif

                        this.Moved <- moved

                    previous
                else
                    if g.HasLastRaised then
                        g.ClearLastRaised ()

                    let prevMask = readyMask
                    let prevRuns1 = runs1
                    let mutable stepIdx = 1
                    let mutable cutoff = false
                    let mutable nextVal = previous
                    let mutable deFusedVal1 = Unchecked.defaultof<'A>
                    let mutable deFusedCmp1 = -1y

#if !FABLE_COMPILER
                    let saved = g.EnterUntrackedPure (n :> IScopeHost)
#endif
                    let mutable thrownEx: exn = Platform.nil ()

                    try
                        activeStep <- 1uy
                        runs1 <- runs1 + 1
#if RANVIER_COUNTERS
                        Counters.MemoRecomputed ()
#endif
                        let v1 = FusedStageHelpers.runPureStage (g, n, f1, root.Peek)

                        if activeStep = 255uy then
                            deFusedVal1 <- v1
                        else
                            let mutable same1 = false

                            if (prevMask &&& 1uy) <> 0uy then
                                deFusedCmp1 <- 2y
                                same1 <- ComparerSentinel.equals eq1 val1 v1
                                deFusedCmp1 <- -1y

                            if activeStep = 255uy then
                                deFusedVal1 <- v1
                                deFusedCmp1 <- if same1 then 0y else 1y
                            elif same1 then
                                cutoff <- true
                            else
                                val1 <- v1
                                readyMask <- 1uy
                                publishedMask <- 1uy
                                stepIdx <- 2
                                activeStep <- 2uy
                                n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                                Counters.MemoRecomputed ()
#endif
                                nextVal <- FusedStageHelpers.runPureStage (g, n, f2, v1)
                    with ex ->
#if FABLE_COMPILER
                        thrownEx <- Thrown.hold ex
#else
                        thrownEx <- ex
#endif

                    let wasDeFused = activeStep = 255uy
                    activeStep <- 0uy

#if !FABLE_COMPILER
                    g.LeaveUntrackedPure saved
#endif

                    if wasDeFused && stepIdx < 2 then
                        this.CompleteDeFused2 (
                            tailMemo,
                            n,
                            outputEqual,
                            previous,
                            previousStatus,
                            previousFailure,
                            (prevMask &&& 1uy) <> 0uy,
                            prevRuns1,
                            deFusedVal1,
                            deFusedCmp1,
                            thrownEx
                        )
                    elif not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
                        let ex = Thrown.take thrownEx
#else
                        let ex = thrownEx
#endif
                        if stepIdx = 1 then
                            readyMask <- 0uy

                        let errOrigin = if stepIdx = 1 then m1 :> INode else origin

                        CombinatorHelpers.recordFusedFailure (g, n, errOrigin, deFusedCmp1 = 2y, ex, previousFailure)

                        let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

                        if
                            stepIdx = 1
                            && (prevRuns1 = 0
                                || (prevMask &&& 1uy) <> 0uy
                                || moved)
                        then
                            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
#endif

                        this.Moved <- moved
                        previous
                    elif cutoff then
                        CombinatorHelpers.clearActive n
                        this.Moved <- false
                        previous
                    else
                        CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, nextVal, previousStatus, previousFailure)

    override _.EvalPrefix(_g, _n, _selfMemo, _prevStatus, _prevFailure) =
        FusedPrefixOutcome<'B>(0uy, Unchecked.defaultof<'B>, false)

    override this.DetachUnwarmedPrefix(selfMemo: Memo<'B>) =
        this.FusedNext <- Platform.nil ()
        (Platform.fastCast<FusedRunner<'A>> m1.ComputeObj).FusedNext <- Platform.nil ()
        selfMemo.RebindFused (box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, false)), 7uy)

    override this.MaterializeWarmPrefix
        (
            selfMemo: Memo<'B>,
            tailFresh: int,
            tailStatus: Status,
            tailFailure: Failure,
            hasSelfReady: bool,
            hasSelfPublished: bool,
            selfVal: 'B,
            selfRuns: int
        ) =
        this.FusedNext <- Platform.nil ()

        (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
            .MaterializeWarmPrefix(m1, tailFresh, tailStatus, tailFailure, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1)

        FusedStageHelpers.populateMaterializedStage (
            selfMemo,
            box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not selfMemo.Node.IsDisposed)),
            7uy,
            m1.Node :> ISource,
            m1 :> INode,
            false,
            tailFresh,
            tailStatus,
            tailFailure,
            hasSelfReady,
            hasSelfPublished,
            selfVal,
            selfRuns
        )

    override _.MaterializeActivePrefix(_, _, _, _, _, _, _) = ()
    override _.MaterializeWaitingRoot(_, _, _, _, _, _, _) = ()

    override this.MaterializeTail(tailObj: obj) =
        let tailMemo = Platform.fastCast<Memo<'B>> tailObj
        let tn = tailMemo.Node
        let step = activeStep

        if step <> 0uy && step <> 255uy then
            activeStep <- 255uy

            if step = 254uy then
                (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                    .MaterializeWaitingRoot(m1, tn.Status, tn.FailureField, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1)

                FusedStageHelpers.finishActiveTailMaterialization (tailMemo, m1, f2)
            elif step = 1uy then
                (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                    .MaterializeActivePrefix(
                        m1,
                        tn.Status,
                        tn.FailureField,
                        (readyMask &&& 1uy) <> 0uy,
                        (publishedMask &&& 1uy) <> 0uy,
                        val1,
                        runs1 - 1
                    )

                FusedStageHelpers.finishActiveTailMaterialization (tailMemo, m1, f2)
            else
                let tailFresh = tn.Flags &&& NodeFlags.FreshMask
                tn.SourcesField.Clear (tn :> IComputation)

                (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                    .MaterializeWarmPrefix(m1, tailFresh, Status.None, null, true, true, val1, runs1)

                FusedStageHelpers.finishTailMaterialization (tailMemo, m1, f2, tailFresh, Status.None)
        elif tn.RunsField = 0 then
            this.DetachUnwarmedPrefix tailMemo
        else
            let tailFresh = tn.Flags &&& NodeFlags.FreshMask
            let tailStatus = tn.Status
            let tailFailure = tn.FailureField
            tn.SourcesField.Clear (tn :> IComputation)

            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeWarmPrefix(
                    m1,
                    tailFresh,
                    tailStatus,
                    tailFailure,
                    (readyMask &&& 1uy) <> 0uy,
                    (publishedMask &&& 1uy) <> 0uy,
                    val1,
                    runs1
                )

            FusedStageHelpers.finishTailMaterialization (tailMemo, m1, f2, tailFresh, tailStatus)

    override _.ClearIntermediates() =
        (Platform.fastCast<FusedRunner<'A>> m1.ComputeObj).FusedNext <- Platform.nil ()

/// <summary>3-stage unboxed <c>map</c> runner over <c>CombinatorSource</c> (<c>computeKind = 7uy</c>, Theorem B18).</summary>
and [<Sealed>] internal FusedSourceMap3<'R, 'A, 'B, 'C>
    (root: CombinatorSource<'R>, f1: 'R -> 'A, m1: Memo<'A>, f2: 'A -> 'B, m2: Memo<'B>, f3: 'B -> 'C) =
    inherit FusedExtendable<'C>()

    let eq1 = m1.EncodedEqual
    let eq2 = m2.EncodedEqual
    let mutable val1 = Unchecked.defaultof<'A>
    let mutable val2 = Unchecked.defaultof<'B>
    let mutable runs1 = 0
    let mutable runs2 = 0
    let mutable readyMask = 0uy
    let mutable publishedMask = 0uy
    let mutable activeStep = 0uy
    let mutable linked = false

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CompleteDeFused3
        (
            tailMemo: Memo<'C>,
            n: MemoNode,
            outputEqual: IEqualityComparer<'C>,
            previous: 'C,
            previousStatus: Status,
            previousFailure: Failure,
            prevMask: byte,
            prevRuns1: int,
            prevRuns2: int,
            stepIdx: int,
            deFusedVal1: 'A,
            deFusedVal2: 'B,
            deFusedCmp: sbyte,
            thrownEx: exn
        ) : 'C =
        let reDirtied = FusedStageHelpers.prepareTailBeforeStagePublish n

        let ex =
            if Platform.isNull thrownEx then
                Platform.nil ()
            else
#if FABLE_COMPILER
                Thrown.take thrownEx
#else
                thrownEx
#endif

        if stepIdx = 1 then
            FusedStageHelpers.finishDeFusedStage (
                m1,
                deFusedVal1,
                ex,
                prevRuns1,
                (prevMask &&& 1uy) <> 0uy,
                previousStatus,
                previousFailure,
                root.Node,
                deFusedCmp
            )

            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m2, m1)
            FusedStageHelpers.finishDeFusedTail (this, tailMemo, m2, n, outputEqual, previous, previousStatus, previousFailure, reDirtied)
        else
            FusedStageHelpers.finishDeFusedStage (
                m2,
                deFusedVal2,
                ex,
                prevRuns2,
                (prevMask &&& 2uy) <> 0uy,
                previousStatus,
                previousFailure,
                m1 :> INode,
                deFusedCmp
            )

            FusedStageHelpers.finishDeFusedTail (this, tailMemo, m2, n, outputEqual, previous, previousStatus, previousFailure, reDirtied)

    override _.Fuse<'Next>(prevMemo: Memo<'C>, mapping: 'C -> 'Next) =
        struct (FusedSourceMap4<'R, 'A, 'B, 'C, 'Next>(root, f1, m1, f2, m2, f3, prevMemo, mapping) :> FusedExtendable<'Next>, 7uy)

    override _.LinkRoot(n: MemoNode) =
        if not linked then
            n.SourcesField.BeginRun ()
            NodeOps.AddSource (n, root.Source)

            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                linked <- true

    override _.MarkRootDirtyIfLive() =
        if not m1.Node.IsDisposed then
            NodeOps.MemoMarkDirty m1.Node

    override _.EvalSignalLeaf(_tailObj, _n, _g, _safeTailEq, _outputEqual, _previous) = false

    override this.ExecuteFused(tailObj, n, outputEqual, previous, previousStatus, previousFailure) =
        let tailMemo = Platform.fastCast<Memo<'C>> tailObj
        let origin = tailMemo :> INode
        let g = n.Graph
        this.LinkRoot n

        if not n.IsDisposed then
            activeStep <- 254uy
            CombinatorHelpers.refreshStaticSlot (g, n, 0, fun () -> root.RefreshAndCheckStale n)

        if activeStep = 255uy then
            activeStep <- 0uy

            FusedStageHelpers.prepareTailBeforeStagePublish n
            |> ignore

            FusedStageHelpers.advanceWaitingRootFromCurrentSource m1
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m2, m1)

            FusedStageHelpers.finishDeFusedTail (this, tailMemo, m2, n, outputEqual, previous, previousStatus, previousFailure, false)
        else
            activeStep <- 0uy

            if n.IsDisposed then
                CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, n.PendingSourceField)
                this.Moved <- false
                previous
            else
                let st = root.Status

                if StatusBits.isUnready st then
                    let wasStage1Unready = (readyMask &&& 1uy) = 0uy && runs1 > 0
                    readyMask <- 0uy
                    runs1 <- runs1 + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
#endif
                    CombinatorHelpers.clearActive n

                    if StatusBits.isPending st then
                        n.FailureField <- Platform.nil ()
                        n.PendingSourceField <- m2 :> INode
                        n.SetStatusPending ()
                        let moved = not (StatusBits.isPending previousStatus)

                        if not wasStage1Unready || moved then
                            runs2 <- runs2 + 1
                            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
                            Counters.MemoRecomputed ()
#endif

                        this.Moved <- moved
                    else
                        let rf = root.Failure
                        n.PendingSourceField <- Platform.nil ()
                        n.FailureField <- rf
                        n.SetStatusError ()
                        let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

                        if not wasStage1Unready || moved then
                            runs2 <- runs2 + 1
                            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
                            Counters.MemoRecomputed ()
#endif

                        this.Moved <- moved

                    previous
                else
                    if g.HasLastRaised then
                        g.ClearLastRaised ()

                    let prevMask = readyMask
                    let prevRuns1 = runs1
                    let prevRuns2 = runs2
                    let mutable stepIdx = 1
                    let mutable cutoff = false
                    let mutable nextVal = previous
                    let mutable deFusedVal1 = Unchecked.defaultof<'A>
                    let mutable deFusedVal2 = Unchecked.defaultof<'B>
                    let mutable deFusedCmp = -1y

#if !FABLE_COMPILER
                    let saved = g.EnterUntrackedPure (n :> IScopeHost)
#endif
                    let mutable thrownEx: exn = Platform.nil ()

                    try
                        activeStep <- 1uy
                        runs1 <- runs1 + 1
#if RANVIER_COUNTERS
                        Counters.MemoRecomputed ()
#endif
                        let v1 = FusedStageHelpers.runPureStage (g, n, f1, root.Peek)

                        if activeStep = 255uy then
                            deFusedVal1 <- v1
                        else
                            let mutable same1 = false

                            if (prevMask &&& 1uy) <> 0uy then
                                deFusedCmp <- 2y
                                same1 <- ComparerSentinel.equals eq1 val1 v1
                                deFusedCmp <- -1y

                            if activeStep = 255uy then
                                deFusedVal1 <- v1
                                deFusedCmp <- if same1 then 0y else 1y
                            elif same1 then
                                cutoff <- true
                            else
                                val1 <- v1
                                readyMask <- readyMask ||| 1uy
                                publishedMask <- publishedMask ||| 1uy
                                stepIdx <- 2
                                activeStep <- 2uy
                                runs2 <- runs2 + 1
#if RANVIER_COUNTERS
                                Counters.MemoRecomputed ()
#endif
                                let v2 = FusedStageHelpers.runPureStage (g, n, f2, v1)

                                if activeStep = 255uy then
                                    deFusedVal2 <- v2
                                else
                                    let mutable same2 = false

                                    if (prevMask &&& 2uy) <> 0uy then
                                        deFusedCmp <- 2y
                                        same2 <- ComparerSentinel.equals eq2 val2 v2
                                        deFusedCmp <- -1y

                                    if activeStep = 255uy then
                                        deFusedVal2 <- v2
                                        deFusedCmp <- if same2 then 0y else 1y
                                    elif same2 then
                                        cutoff <- true
                                    else
                                        val2 <- v2
                                        readyMask <- readyMask ||| 2uy
                                        publishedMask <- publishedMask ||| 2uy
                                        stepIdx <- 3
                                        activeStep <- 3uy
                                        n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                                        Counters.MemoRecomputed ()
#endif
                                        nextVal <- FusedStageHelpers.runPureStage (g, n, f3, v2)
                    with ex ->
#if FABLE_COMPILER
                        thrownEx <- Thrown.hold ex
#else
                        thrownEx <- ex
#endif

                    let wasDeFused = activeStep = 255uy
                    activeStep <- 0uy

#if !FABLE_COMPILER
                    g.LeaveUntrackedPure saved
#endif

                    if wasDeFused && stepIdx < 3 then
                        this.CompleteDeFused3 (
                            tailMemo,
                            n,
                            outputEqual,
                            previous,
                            previousStatus,
                            previousFailure,
                            prevMask,
                            prevRuns1,
                            prevRuns2,
                            stepIdx,
                            deFusedVal1,
                            deFusedVal2,
                            deFusedCmp,
                            thrownEx
                        )
                    elif not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
                        let ex = Thrown.take thrownEx
#else
                        let ex = thrownEx
#endif
                        if stepIdx = 1 then
                            readyMask <- 0uy
                        elif stepIdx = 2 then
                            readyMask <- readyMask &&& 1uy

                        let errOrigin =
                            if stepIdx = 1 then m1 :> INode
                            elif stepIdx = 2 then m2 :> INode
                            else origin

                        CombinatorHelpers.recordFusedFailure (g, n, errOrigin, deFusedCmp = 2y, ex, previousFailure)

                        let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

                        if
                            stepIdx = 1
                            && (prevRuns1 = 0
                                || (prevMask &&& 1uy) <> 0uy
                                || moved)
                        then
                            runs2 <- runs2 + 1
                            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
                            Counters.MemoRecomputed ()
#endif
                        elif
                            stepIdx = 2
                            && (prevRuns2 = 0
                                || (prevMask &&& 2uy) <> 0uy
                                || moved)
                        then
                            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
#endif

                        this.Moved <- moved
                        previous
                    elif cutoff then
                        CombinatorHelpers.clearActive n
                        this.Moved <- false
                        previous
                    else
                        CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, nextVal, previousStatus, previousFailure)

    override _.EvalPrefix(_g, _n, _selfMemo, _prevStatus, _prevFailure) =
        FusedPrefixOutcome<'C>(0uy, Unchecked.defaultof<'C>, false)

    override this.DetachUnwarmedPrefix(selfMemo: Memo<'C>) =
        this.FusedNext <- Platform.nil ()
        (Platform.fastCast<FusedExtendable<'B>> m2.ComputeObj).DetachUnwarmedPrefix m2
        selfMemo.RebindFused (box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, false)), 7uy)

    override this.MaterializeWarmPrefix
        (
            selfMemo: Memo<'C>,
            tailFresh: int,
            tailStatus: Status,
            tailFailure: Failure,
            hasSelfReady: bool,
            hasSelfPublished: bool,
            selfVal: 'C,
            selfRuns: int
        ) =
        this.FusedNext <- Platform.nil ()

        (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
            .MaterializeWarmPrefix(m1, tailFresh, tailStatus, tailFailure, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1)

        (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

        FusedStageHelpers.populateMaterializedStage (
            m2,
            box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
            7uy,
            m1.Node :> ISource,
            m1 :> INode,
            false,
            tailFresh,
            tailStatus,
            tailFailure,
            (readyMask &&& 2uy) <> 0uy,
            (publishedMask &&& 2uy) <> 0uy,
            val2,
            runs2
        )

        FusedStageHelpers.populateMaterializedStage (
            selfMemo,
            box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not selfMemo.Node.IsDisposed)),
            7uy,
            m2.Node :> ISource,
            m2 :> INode,
            false,
            tailFresh,
            tailStatus,
            tailFailure,
            hasSelfReady,
            hasSelfPublished,
            selfVal,
            selfRuns
        )

    override _.MaterializeActivePrefix(_, _, _, _, _, _, _) = ()
    override _.MaterializeWaitingRoot(_, _, _, _, _, _, _) = ()

    override this.MaterializeTail(tailObj: obj) =
        let tailMemo = Platform.fastCast<Memo<'C>> tailObj
        let tn = tailMemo.Node
        let step = activeStep

        if step <> 0uy && step <> 255uy then
            activeStep <- 255uy
            let prevSt = tn.Status
            let prevFail = tn.FailureField

            if step = 254uy then
                (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                    .MaterializeWaitingRoot(m1, prevSt, prevFail, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1)

                (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                FusedStageHelpers.populateWaitingStage (
                    m2,
                    box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                    7uy,
                    m1.Node :> ISource,
                    m1 :> INode,
                    prevSt,
                    prevFail,
                    (readyMask &&& 2uy) <> 0uy,
                    (publishedMask &&& 2uy) <> 0uy,
                    val2,
                    runs2
                )

                FusedStageHelpers.finishActiveTailMaterialization (tailMemo, m2, f3)
            elif step = 1uy then
                (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                    .MaterializeActivePrefix(m1, prevSt, prevFail, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1 - 1)

                (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                FusedStageHelpers.populateWaitingStage (
                    m2,
                    box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                    7uy,
                    m1.Node :> ISource,
                    m1 :> INode,
                    prevSt,
                    prevFail,
                    (readyMask &&& 2uy) <> 0uy,
                    (publishedMask &&& 2uy) <> 0uy,
                    val2,
                    runs2
                )

                FusedStageHelpers.finishActiveTailMaterialization (tailMemo, m2, f3)
            elif step = 2uy then
                (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                    .MaterializeWarmPrefix(m1, NodeFlags.FreshClean, Status.None, null, true, true, val1, runs1)

                (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                FusedStageHelpers.populateActiveStage (
                    m2,
                    box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                    7uy,
                    m1.Node :> ISource,
                    (publishedMask &&& 2uy) <> 0uy,
                    val2,
                    runs2
                )

                FusedStageHelpers.finishActiveTailMaterialization (tailMemo, m2, f3)
            else
                let tailFresh = tn.Flags &&& NodeFlags.FreshMask
                tn.SourcesField.Clear (tn :> IComputation)

                (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                    .MaterializeWarmPrefix(m1, tailFresh, Status.None, null, true, true, val1, runs1)

                (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                FusedStageHelpers.populateMaterializedStage (
                    m2,
                    box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                    7uy,
                    m1.Node :> ISource,
                    m1 :> INode,
                    false,
                    tailFresh,
                    Status.None,
                    null,
                    true,
                    true,
                    val2,
                    runs2
                )

                FusedStageHelpers.finishTailMaterialization (tailMemo, m2, f3, tailFresh, Status.None)
        elif tn.RunsField = 0 then
            this.DetachUnwarmedPrefix tailMemo
        else
            let tailFresh = tn.Flags &&& NodeFlags.FreshMask
            let tailStatus = tn.Status
            let tailFailure = tn.FailureField
            tn.SourcesField.Clear (tn :> IComputation)

            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeWarmPrefix(
                    m1,
                    tailFresh,
                    tailStatus,
                    tailFailure,
                    (readyMask &&& 1uy) <> 0uy,
                    (publishedMask &&& 1uy) <> 0uy,
                    val1,
                    runs1
                )

            (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateMaterializedStage (
                m2,
                box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                7uy,
                m1.Node :> ISource,
                m1 :> INode,
                false,
                tailFresh,
                tailStatus,
                tailFailure,
                (readyMask &&& 2uy) <> 0uy,
                (publishedMask &&& 2uy) <> 0uy,
                val2,
                runs2
            )

            FusedStageHelpers.finishTailMaterialization (tailMemo, m2, f3, tailFresh, tailStatus)

    override _.ClearIntermediates() =
        (Platform.fastCast<FusedRunner<'A>> m1.ComputeObj).FusedNext <- Platform.nil ()
        (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

/// <summary>4-stage unboxed <c>map</c> runner over <c>CombinatorSource</c> (<c>computeKind = 7uy</c>, Theorem B18).</summary>
and [<Sealed>] internal FusedSourceMap4<'R, 'A, 'B, 'C, 'D>
    (root: CombinatorSource<'R>, f1: 'R -> 'A, m1: Memo<'A>, f2: 'A -> 'B, m2: Memo<'B>, f3: 'B -> 'C, m3: Memo<'C>, f4: 'C -> 'D) =
    inherit FusedExtendable<'D>()

    let eq1 = m1.EncodedEqual
    let eq2 = m2.EncodedEqual
    let eq3 = m3.EncodedEqual
    let mutable val1 = Unchecked.defaultof<'A>
    let mutable val2 = Unchecked.defaultof<'B>
    let mutable val3 = Unchecked.defaultof<'C>
    let mutable runs1 = 0
    let mutable runs2 = 0
    let mutable runs3 = 0
    let mutable readyMask = 0uy
    let mutable publishedMask = 0uy
    let mutable activeStep = 0uy
    let mutable linked = false

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.PublishDeFusedPrefixStage
        (
            selfMemo: Memo<'D>,
            previousStatus: Status,
            previousFailure: Failure,
            prevMask: byte,
            prevRuns1: int,
            prevRuns2: int,
            prevRuns3: int,
            prevSelfRuns: int,
            hasSelfReady: bool,
            stepIdx: int,
            deFusedVal1: 'A,
            deFusedVal2: 'B,
            deFusedVal3: 'C,
            deFusedVal4: 'D,
            deFusedCmp: sbyte,
            thrownEx: exn
        ) =
        let ex =
            if Platform.isNull thrownEx then
                Platform.nil ()
            else
#if FABLE_COMPILER
                Thrown.take thrownEx
#else
                thrownEx
#endif

        if stepIdx = 1 then
            FusedStageHelpers.finishDeFusedStage (
                m1,
                deFusedVal1,
                ex,
                prevRuns1,
                (prevMask &&& 1uy) <> 0uy,
                previousStatus,
                previousFailure,
                root.Node,
                deFusedCmp
            )

            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m2, m1)
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m3, m2)
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (selfMemo, m3)
        elif stepIdx = 2 then
            FusedStageHelpers.finishDeFusedStage (
                m2,
                deFusedVal2,
                ex,
                prevRuns2,
                (prevMask &&& 2uy) <> 0uy,
                previousStatus,
                previousFailure,
                m1 :> INode,
                deFusedCmp
            )

            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m3, m2)
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (selfMemo, m3)
        elif stepIdx = 3 then
            FusedStageHelpers.finishDeFusedStage (
                m3,
                deFusedVal3,
                ex,
                prevRuns3,
                (prevMask &&& 4uy) <> 0uy,
                previousStatus,
                previousFailure,
                m2 :> INode,
                deFusedCmp
            )

            FusedStageHelpers.advanceWaitingStageFromCurrentPred (selfMemo, m3)
        else
            FusedStageHelpers.finishDeFusedStage (
                selfMemo,
                deFusedVal4,
                ex,
                prevSelfRuns,
                hasSelfReady,
                previousStatus,
                previousFailure,
                m3 :> INode,
                deFusedCmp
            )

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CompleteDeFused4
        (
            tailMemo: Memo<'D>,
            n: MemoNode,
            outputEqual: IEqualityComparer<'D>,
            previous: 'D,
            previousStatus: Status,
            previousFailure: Failure,
            prevMask: byte,
            prevRuns1: int,
            prevRuns2: int,
            prevRuns3: int,
            stepIdx: int,
            deFusedVal1: 'A,
            deFusedVal2: 'B,
            deFusedVal3: 'C,
            deFusedCmp: sbyte,
            thrownEx: exn
        ) : 'D =
        let reDirtied = FusedStageHelpers.prepareTailBeforeStagePublish n

        let ex =
            if Platform.isNull thrownEx then
                Platform.nil ()
            else
#if FABLE_COMPILER
                Thrown.take thrownEx
#else
                thrownEx
#endif

        if stepIdx = 1 then
            FusedStageHelpers.finishDeFusedStage (
                m1,
                deFusedVal1,
                ex,
                prevRuns1,
                (prevMask &&& 1uy) <> 0uy,
                previousStatus,
                previousFailure,
                root.Node,
                deFusedCmp
            )

            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m2, m1)
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m3, m2)
            FusedStageHelpers.finishDeFusedTail (this, tailMemo, m3, n, outputEqual, previous, previousStatus, previousFailure, reDirtied)
        elif stepIdx = 2 then
            FusedStageHelpers.finishDeFusedStage (
                m2,
                deFusedVal2,
                ex,
                prevRuns2,
                (prevMask &&& 2uy) <> 0uy,
                previousStatus,
                previousFailure,
                m1 :> INode,
                deFusedCmp
            )

            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m3, m2)
            FusedStageHelpers.finishDeFusedTail (this, tailMemo, m3, n, outputEqual, previous, previousStatus, previousFailure, reDirtied)
        else
            FusedStageHelpers.finishDeFusedStage (
                m3,
                deFusedVal3,
                ex,
                prevRuns3,
                (prevMask &&& 4uy) <> 0uy,
                previousStatus,
                previousFailure,
                m2 :> INode,
                deFusedCmp
            )

            FusedStageHelpers.finishDeFusedTail (this, tailMemo, m3, n, outputEqual, previous, previousStatus, previousFailure, reDirtied)

    override this.Fuse<'Next>(prevMemo: Memo<'D>, mapping: 'D -> 'Next) =
        struct (FusedChainMap<'D, 'Next>(this, prevMemo, mapping) :> FusedExtendable<'Next>, 7uy)

    override _.LinkRoot(n: MemoNode) =
        if not linked then
            n.SourcesField.BeginRun ()
            NodeOps.AddSource (n, root.Source)

            if n.IsDisposed then
                n.SourcesField.Clear (n :> IComputation)
            else
                linked <- true

    override _.MarkRootDirtyIfLive() =
        if not m1.Node.IsDisposed then
            NodeOps.MemoMarkDirty m1.Node

    override _.EvalSignalLeaf(_tailObj, _n, _g, _safeTailEq, _outputEqual, _previous) = false

    override this.ExecuteFused(tailObj, n, outputEqual, previous, previousStatus, previousFailure) =
        let tailMemo = Platform.fastCast<Memo<'D>> tailObj
        let origin = tailMemo :> INode
        let g = n.Graph
        this.LinkRoot n

        if not n.IsDisposed then
            activeStep <- 254uy
            CombinatorHelpers.refreshStaticSlot (g, n, 0, fun () -> root.RefreshAndCheckStale n)

        if activeStep = 255uy then
            activeStep <- 0uy

            FusedStageHelpers.prepareTailBeforeStagePublish n
            |> ignore

            FusedStageHelpers.advanceWaitingRootFromCurrentSource m1
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m2, m1)
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m3, m2)

            FusedStageHelpers.finishDeFusedTail (this, tailMemo, m3, n, outputEqual, previous, previousStatus, previousFailure, false)
        else
            activeStep <- 0uy

            if n.IsDisposed then
                CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, n.PendingSourceField)
                this.Moved <- false
                previous
            else
                let st = root.Status

                if StatusBits.isUnready st then
                    let wasStage1Unready = (readyMask &&& 1uy) = 0uy && runs1 > 0
                    readyMask <- 0uy
                    runs1 <- runs1 + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
#endif
                    CombinatorHelpers.clearActive n

                    if StatusBits.isPending st then
                        n.FailureField <- Platform.nil ()
                        n.PendingSourceField <- m3 :> INode
                        n.SetStatusPending ()
                        let moved = not (StatusBits.isPending previousStatus)

                        if not wasStage1Unready || moved then
                            runs2 <- runs2 + 1
                            runs3 <- runs3 + 1
                            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
                            Counters.MemoRecomputed ()
                            Counters.MemoRecomputed ()
#endif

                        this.Moved <- moved
                    else
                        let rf = root.Failure
                        n.PendingSourceField <- Platform.nil ()
                        n.FailureField <- rf
                        n.SetStatusError ()
                        let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

                        if not wasStage1Unready || moved then
                            runs2 <- runs2 + 1
                            runs3 <- runs3 + 1
                            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
                            Counters.MemoRecomputed ()
                            Counters.MemoRecomputed ()
#endif

                        this.Moved <- moved

                    previous
                else
                    if g.HasLastRaised then
                        g.ClearLastRaised ()

                    let prevMask = readyMask
                    let prevRuns1 = runs1
                    let prevRuns2 = runs2
                    let prevRuns3 = runs3
                    let mutable stepIdx = 1
                    let mutable cutoff = false
                    let mutable nextVal = previous
                    let mutable deFusedVal1 = Unchecked.defaultof<'A>
                    let mutable deFusedVal2 = Unchecked.defaultof<'B>
                    let mutable deFusedVal3 = Unchecked.defaultof<'C>
                    let mutable deFusedCmp = -1y

#if !FABLE_COMPILER
                    let saved = g.EnterUntrackedPure (n :> IScopeHost)
#endif
                    let mutable thrownEx: exn = Platform.nil ()

                    try
                        activeStep <- 1uy
                        runs1 <- runs1 + 1
#if RANVIER_COUNTERS
                        Counters.MemoRecomputed ()
#endif
                        let v1 = FusedStageHelpers.runPureStage (g, n, f1, root.Peek)

                        if activeStep = 255uy then
                            deFusedVal1 <- v1
                        else
                            let mutable same1 = false

                            if (prevMask &&& 1uy) <> 0uy then
                                deFusedCmp <- 2y
                                same1 <- ComparerSentinel.equals eq1 val1 v1
                                deFusedCmp <- -1y

                            if activeStep = 255uy then
                                deFusedVal1 <- v1
                                deFusedCmp <- if same1 then 0y else 1y
                            elif same1 then
                                cutoff <- true
                            else
                                val1 <- v1
                                readyMask <- readyMask ||| 1uy
                                publishedMask <- publishedMask ||| 1uy
                                stepIdx <- 2
                                activeStep <- 2uy
                                runs2 <- runs2 + 1
#if RANVIER_COUNTERS
                                Counters.MemoRecomputed ()
#endif
                                let v2 = FusedStageHelpers.runPureStage (g, n, f2, v1)

                                if activeStep = 255uy then
                                    deFusedVal2 <- v2
                                else
                                    let mutable same2 = false

                                    if (prevMask &&& 2uy) <> 0uy then
                                        deFusedCmp <- 2y
                                        same2 <- ComparerSentinel.equals eq2 val2 v2
                                        deFusedCmp <- -1y

                                    if activeStep = 255uy then
                                        deFusedVal2 <- v2
                                        deFusedCmp <- if same2 then 0y else 1y
                                    elif same2 then
                                        cutoff <- true
                                    else
                                        val2 <- v2
                                        readyMask <- readyMask ||| 2uy
                                        publishedMask <- publishedMask ||| 2uy
                                        stepIdx <- 3
                                        activeStep <- 3uy
                                        runs3 <- runs3 + 1
#if RANVIER_COUNTERS
                                        Counters.MemoRecomputed ()
#endif
                                        let v3 = FusedStageHelpers.runPureStage (g, n, f3, v2)

                                        if activeStep = 255uy then
                                            deFusedVal3 <- v3
                                        else
                                            let mutable same3 = false

                                            if (prevMask &&& 4uy) <> 0uy then
                                                deFusedCmp <- 2y
                                                same3 <- ComparerSentinel.equals eq3 val3 v3
                                                deFusedCmp <- -1y

                                            if activeStep = 255uy then
                                                deFusedVal3 <- v3
                                                deFusedCmp <- if same3 then 0y else 1y
                                            elif same3 then
                                                cutoff <- true
                                            else
                                                val3 <- v3
                                                readyMask <- readyMask ||| 4uy
                                                publishedMask <- publishedMask ||| 4uy
                                                stepIdx <- 4
                                                activeStep <- 4uy
                                                n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                                                Counters.MemoRecomputed ()
#endif
                                                nextVal <- FusedStageHelpers.runPureStage (g, n, f4, v3)
                    with ex ->
#if FABLE_COMPILER
                        thrownEx <- Thrown.hold ex
#else
                        thrownEx <- ex
#endif

                    let wasDeFused = activeStep = 255uy
                    activeStep <- 0uy

#if !FABLE_COMPILER
                    g.LeaveUntrackedPure saved
#endif

                    if wasDeFused && stepIdx < 4 then
                        this.CompleteDeFused4 (
                            tailMemo,
                            n,
                            outputEqual,
                            previous,
                            previousStatus,
                            previousFailure,
                            prevMask,
                            prevRuns1,
                            prevRuns2,
                            prevRuns3,
                            stepIdx,
                            deFusedVal1,
                            deFusedVal2,
                            deFusedVal3,
                            deFusedCmp,
                            thrownEx
                        )
                    elif not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
                        let ex = Thrown.take thrownEx
#else
                        let ex = thrownEx
#endif
                        if stepIdx = 1 then
                            readyMask <- 0uy
                        elif stepIdx = 2 then
                            readyMask <- readyMask &&& 1uy
                        elif stepIdx = 3 then
                            readyMask <- readyMask &&& 3uy

                        let errOrigin =
                            if stepIdx = 1 then m1 :> INode
                            elif stepIdx = 2 then m2 :> INode
                            elif stepIdx = 3 then m3 :> INode
                            else origin

                        CombinatorHelpers.recordFusedFailure (g, n, errOrigin, deFusedCmp = 2y, ex, previousFailure)

                        let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

                        if
                            stepIdx = 1
                            && (prevRuns1 = 0
                                || (prevMask &&& 1uy) <> 0uy
                                || moved)
                        then
                            runs2 <- runs2 + 1
                            runs3 <- runs3 + 1
                            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
                            Counters.MemoRecomputed ()
                            Counters.MemoRecomputed ()
#endif
                        elif
                            stepIdx = 2
                            && (prevRuns2 = 0
                                || (prevMask &&& 2uy) <> 0uy
                                || moved)
                        then
                            runs3 <- runs3 + 1
                            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
                            Counters.MemoRecomputed ()
#endif
                        elif
                            stepIdx = 3
                            && (prevRuns3 = 0
                                || (prevMask &&& 4uy) <> 0uy
                                || moved)
                        then
                            n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
#endif

                        this.Moved <- moved
                        previous
                    elif cutoff then
                        CombinatorHelpers.clearActive n
                        this.Moved <- false
                        previous
                    else
                        CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, nextVal, previousStatus, previousFailure)

    override this.EvalPrefix(g, n, selfMemo, previousStatus, previousFailure) =
        if not n.IsDisposed then
            activeStep <- 254uy
            CombinatorHelpers.refreshStaticSlot (g, n, 0, fun () -> root.RefreshAndCheckStale n)

        if activeStep = 255uy then
            activeStep <- 0uy

            FusedStageHelpers.prepareTailBeforeStagePublish n
            |> ignore

            FusedStageHelpers.advanceWaitingRootFromCurrentSource m1
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m2, m1)
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (m3, m2)
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (selfMemo, m3)

            FusedPrefixOutcome<'D>(4uy, Unchecked.defaultof<'D>, false)
        else
            activeStep <- 0uy

            if n.IsDisposed then
                FusedPrefixOutcome<'D>(0uy, Unchecked.defaultof<'D>, false)
            else
                let st = root.Status

                if StatusBits.isUnready st then
                    let wasStage1Unready = (readyMask &&& 1uy) = 0uy && runs1 > 0
                    readyMask <- 0uy
                    runs1 <- runs1 + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
#endif

                    if StatusBits.isPending st then
                        n.FailureField <- Platform.nil ()
                        n.SetStatusPending ()
                        let moved = not (StatusBits.isPending previousStatus)

                        if not wasStage1Unready || moved then
                            runs2 <- runs2 + 1
                            runs3 <- runs3 + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
                            Counters.MemoRecomputed ()
#endif

                        FusedPrefixOutcome<'D>(2uy, Unchecked.defaultof<'D>, not wasStage1Unready || moved)
                    else
                        n.PendingSourceField <- Platform.nil ()
                        n.FailureField <- root.Failure
                        n.SetStatusError ()
                        let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

                        if not wasStage1Unready || moved then
                            runs2 <- runs2 + 1
                            runs3 <- runs3 + 1
#if RANVIER_COUNTERS
                            Counters.MemoRecomputed ()
                            Counters.MemoRecomputed ()
#endif

                        FusedPrefixOutcome<'D>(3uy, Unchecked.defaultof<'D>, not wasStage1Unready || moved)
                else
                    if g.HasLastRaised then
                        g.ClearLastRaised ()

                    let prevMask = readyMask
                    let prevRuns1 = runs1
                    let prevRuns2 = runs2
                    let prevRuns3 = runs3
                    let mutable stepIdx = 1
                    let mutable cutoff = false
                    let mutable nextVal = Unchecked.defaultof<'D>
                    let mutable deFusedVal1 = Unchecked.defaultof<'A>
                    let mutable deFusedVal2 = Unchecked.defaultof<'B>
                    let mutable deFusedVal3 = Unchecked.defaultof<'C>
                    let mutable deFusedVal4 = Unchecked.defaultof<'D>
                    let mutable deFusedCmp = -1y

#if !FABLE_COMPILER
                    let saved = g.EnterUntrackedPure (n :> IScopeHost)
#endif
                    let mutable thrownEx: exn = Platform.nil ()

                    try
                        activeStep <- 1uy
                        runs1 <- runs1 + 1
#if RANVIER_COUNTERS
                        Counters.MemoRecomputed ()
#endif
                        let v1 = FusedStageHelpers.runPureStage (g, n, f1, root.Peek)

                        if activeStep = 255uy then
                            deFusedVal1 <- v1
                        else
                            let mutable same1 = false

                            if (prevMask &&& 1uy) <> 0uy then
                                deFusedCmp <- 2y
                                same1 <- ComparerSentinel.equals eq1 val1 v1
                                deFusedCmp <- -1y

                            if activeStep = 255uy then
                                deFusedVal1 <- v1
                                deFusedCmp <- if same1 then 0y else 1y
                            elif same1 then
                                cutoff <- true
                            else
                                val1 <- v1
                                readyMask <- readyMask ||| 1uy
                                publishedMask <- publishedMask ||| 1uy
                                stepIdx <- 2
                                activeStep <- 2uy
                                runs2 <- runs2 + 1
#if RANVIER_COUNTERS
                                Counters.MemoRecomputed ()
#endif
                                let v2 = FusedStageHelpers.runPureStage (g, n, f2, v1)

                                if activeStep = 255uy then
                                    deFusedVal2 <- v2
                                else
                                    let mutable same2 = false

                                    if (prevMask &&& 2uy) <> 0uy then
                                        deFusedCmp <- 2y
                                        same2 <- ComparerSentinel.equals eq2 val2 v2
                                        deFusedCmp <- -1y

                                    if activeStep = 255uy then
                                        deFusedVal2 <- v2
                                        deFusedCmp <- if same2 then 0y else 1y
                                    elif same2 then
                                        cutoff <- true
                                    else
                                        val2 <- v2
                                        readyMask <- readyMask ||| 2uy
                                        publishedMask <- publishedMask ||| 2uy
                                        stepIdx <- 3
                                        activeStep <- 3uy
                                        runs3 <- runs3 + 1
#if RANVIER_COUNTERS
                                        Counters.MemoRecomputed ()
#endif
                                        let v3 = FusedStageHelpers.runPureStage (g, n, f3, v2)

                                        if activeStep = 255uy then
                                            deFusedVal3 <- v3
                                        else
                                            let mutable same3 = false

                                            if (prevMask &&& 4uy) <> 0uy then
                                                deFusedCmp <- 2y
                                                same3 <- ComparerSentinel.equals eq3 val3 v3
                                                deFusedCmp <- -1y

                                            if activeStep = 255uy then
                                                deFusedVal3 <- v3
                                                deFusedCmp <- if same3 then 0y else 1y
                                            elif same3 then
                                                cutoff <- true
                                            else
                                                val3 <- v3
                                                readyMask <- readyMask ||| 4uy
                                                publishedMask <- publishedMask ||| 4uy
                                                stepIdx <- 4
                                                activeStep <- 4uy
                                                let v4 = FusedStageHelpers.runPureStage (g, n, f4, v3)

                                                if activeStep = 255uy then
                                                    deFusedVal4 <- v4
                                                else
                                                    nextVal <- v4
                    with ex ->
#if FABLE_COMPILER
                        thrownEx <- Thrown.hold ex
#else
                        thrownEx <- ex
#endif

                    let wasDeFused = activeStep = 255uy
                    activeStep <- 0uy

#if !FABLE_COMPILER
                    g.LeaveUntrackedPure saved
#endif

                    if wasDeFused then
                        let reDirtied = FusedStageHelpers.prepareTailBeforeStagePublish n
                        let hasSelfReadyBefore = (readyMask &&& 8uy) <> 0uy
                        readyMask <- readyMask &&& 7uy

                        let prevSelfRuns =
                            if stepIdx = 4 then
                                selfMemo.Node.RunsField - 1
                            else
                                selfMemo.Node.RunsField

                        this.PublishDeFusedPrefixStage (
                            selfMemo,
                            previousStatus,
                            previousFailure,
                            prevMask,
                            prevRuns1,
                            prevRuns2,
                            prevRuns3,
                            prevSelfRuns,
                            hasSelfReadyBefore,
                            stepIdx,
                            deFusedVal1,
                            deFusedVal2,
                            deFusedVal3,
                            deFusedVal4,
                            deFusedCmp,
                            thrownEx
                        )

                        FusedPrefixOutcome<'D>(4uy, Unchecked.defaultof<'D>, reDirtied)
                    elif not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
                        let ex = Thrown.take thrownEx
#else
                        let ex = thrownEx
#endif
                        if stepIdx = 1 then
                            readyMask <- 0uy
                        elif stepIdx = 2 then
                            readyMask <- readyMask &&& 1uy
                        elif stepIdx = 3 then
                            readyMask <- readyMask &&& 3uy

                        let errOrigin =
                            if stepIdx = 1 then m1 :> INode
                            elif stepIdx = 2 then m2 :> INode
                            elif stepIdx = 3 then m3 :> INode
                            else selfMemo :> INode

                        CombinatorHelpers.recordFusedFailure (g, n, errOrigin, deFusedCmp = 2y, ex, previousFailure)

                        let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

                        let unreadyMoved =
                            if stepIdx = 1 then
                                let m =
                                    prevRuns1 = 0
                                    || (prevMask &&& 1uy) <> 0uy
                                    || moved

                                if m then
                                    runs2 <- runs2 + 1
                                    runs3 <- runs3 + 1
#if RANVIER_COUNTERS
                                    Counters.MemoRecomputed ()
                                    Counters.MemoRecomputed ()
#endif

                                m
                            elif stepIdx = 2 then
                                let m =
                                    prevRuns2 = 0
                                    || (prevMask &&& 2uy) <> 0uy
                                    || moved

                                if m then
                                    runs3 <- runs3 + 1
#if RANVIER_COUNTERS
                                    Counters.MemoRecomputed ()
#endif

                                m
                            elif stepIdx = 3 then
                                prevRuns3 = 0
                                || (prevMask &&& 4uy) <> 0uy
                                || moved
                            else
                                moved

                        FusedPrefixOutcome<'D>(3uy, Unchecked.defaultof<'D>, unreadyMoved)
                    elif cutoff then
                        FusedPrefixOutcome<'D>(0uy, Unchecked.defaultof<'D>, false)
                    else
                        FusedPrefixOutcome<'D>(1uy, nextVal, false)

    override this.DetachUnwarmedPrefix(selfMemo: Memo<'D>) =
        this.FusedNext <- Platform.nil ()
        (Platform.fastCast<FusedExtendable<'C>> m3.ComputeObj).DetachUnwarmedPrefix m3
        selfMemo.RebindFused (box (FusedSourceMap1<'C, 'D>(MemoSource<'C>(m3), f4, false)), 7uy)

    override this.MaterializeWarmPrefix
        (
            selfMemo: Memo<'D>,
            tailFresh: int,
            tailStatus: Status,
            tailFailure: Failure,
            hasSelfReady: bool,
            hasSelfPublished: bool,
            selfVal: 'D,
            selfRuns: int
        ) =
        this.FusedNext <- Platform.nil ()

        (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
            .MaterializeWarmPrefix(m1, tailFresh, tailStatus, tailFailure, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1)

        (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

        FusedStageHelpers.populateMaterializedStage (
            m2,
            box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
            7uy,
            m1.Node :> ISource,
            m1 :> INode,
            false,
            tailFresh,
            tailStatus,
            tailFailure,
            (readyMask &&& 2uy) <> 0uy,
            (publishedMask &&& 2uy) <> 0uy,
            val2,
            runs2
        )

        (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

        FusedStageHelpers.populateMaterializedStage (
            m3,
            box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
            7uy,
            m2.Node :> ISource,
            m2 :> INode,
            false,
            tailFresh,
            tailStatus,
            tailFailure,
            (readyMask &&& 4uy) <> 0uy,
            (publishedMask &&& 4uy) <> 0uy,
            val3,
            runs3
        )

        FusedStageHelpers.populateMaterializedStage (
            selfMemo,
            box (FusedSourceMap1<'C, 'D>(MemoSource<'C>(m3), f4, not selfMemo.Node.IsDisposed)),
            7uy,
            m3.Node :> ISource,
            m3 :> INode,
            false,
            tailFresh,
            tailStatus,
            tailFailure,
            hasSelfReady,
            hasSelfPublished,
            selfVal,
            selfRuns
        )

    override this.MaterializeActivePrefix
        (
            selfMemo: Memo<'D>,
            prevTailStatus: Status,
            prevTailFailure: Failure,
            hasSelfReady: bool,
            hasSelfPublished: bool,
            prevSelfVal: 'D,
            prevSelfRuns: int
        ) =
        let step = activeStep
        activeStep <- 255uy
        this.FusedNext <- Platform.nil ()

        if step = 254uy then
            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeWaitingRoot(m1, prevTailStatus, prevTailFailure, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1)

            (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateWaitingStage (
                m2,
                box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                7uy,
                m1.Node :> ISource,
                m1 :> INode,
                prevTailStatus,
                prevTailFailure,
                (readyMask &&& 2uy) <> 0uy,
                (publishedMask &&& 2uy) <> 0uy,
                val2,
                runs2
            )

            (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateWaitingStage (
                m3,
                box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                7uy,
                m2.Node :> ISource,
                m2 :> INode,
                prevTailStatus,
                prevTailFailure,
                (readyMask &&& 4uy) <> 0uy,
                (publishedMask &&& 4uy) <> 0uy,
                val3,
                runs3
            )

            FusedStageHelpers.populateWaitingStage (
                selfMemo,
                box (FusedSourceMap1<'C, 'D>(MemoSource<'C>(m3), f4, not selfMemo.Node.IsDisposed)),
                7uy,
                m3.Node :> ISource,
                m3 :> INode,
                prevTailStatus,
                prevTailFailure,
                hasSelfReady,
                hasSelfPublished,
                prevSelfVal,
                prevSelfRuns
            )
        elif step = 1uy then
            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeActivePrefix(
                    m1,
                    prevTailStatus,
                    prevTailFailure,
                    (readyMask &&& 1uy) <> 0uy,
                    (publishedMask &&& 1uy) <> 0uy,
                    val1,
                    runs1 - 1
                )

            (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateWaitingStage (
                m2,
                box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                7uy,
                m1.Node :> ISource,
                m1 :> INode,
                prevTailStatus,
                prevTailFailure,
                (readyMask &&& 2uy) <> 0uy,
                (publishedMask &&& 2uy) <> 0uy,
                val2,
                runs2
            )

            (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateWaitingStage (
                m3,
                box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                7uy,
                m2.Node :> ISource,
                m2 :> INode,
                prevTailStatus,
                prevTailFailure,
                (readyMask &&& 4uy) <> 0uy,
                (publishedMask &&& 4uy) <> 0uy,
                val3,
                runs3
            )

            FusedStageHelpers.populateWaitingStage (
                selfMemo,
                box (FusedSourceMap1<'C, 'D>(MemoSource<'C>(m3), f4, not selfMemo.Node.IsDisposed)),
                7uy,
                m3.Node :> ISource,
                m3 :> INode,
                prevTailStatus,
                prevTailFailure,
                hasSelfReady,
                hasSelfPublished,
                prevSelfVal,
                prevSelfRuns
            )
        elif step = 2uy then
            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeWarmPrefix(m1, NodeFlags.FreshClean, Status.None, null, true, true, val1, runs1)

            (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateActiveStage (
                m2,
                box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                7uy,
                m1.Node :> ISource,
                (publishedMask &&& 2uy) <> 0uy,
                val2,
                runs2
            )

            (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateWaitingStage (
                m3,
                box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                7uy,
                m2.Node :> ISource,
                m2 :> INode,
                prevTailStatus,
                prevTailFailure,
                (readyMask &&& 4uy) <> 0uy,
                (publishedMask &&& 4uy) <> 0uy,
                val3,
                runs3
            )

            FusedStageHelpers.populateWaitingStage (
                selfMemo,
                box (FusedSourceMap1<'C, 'D>(MemoSource<'C>(m3), f4, not selfMemo.Node.IsDisposed)),
                7uy,
                m3.Node :> ISource,
                m3 :> INode,
                prevTailStatus,
                prevTailFailure,
                hasSelfReady,
                hasSelfPublished,
                prevSelfVal,
                prevSelfRuns
            )
        elif step = 3uy then
            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeWarmPrefix(m1, NodeFlags.FreshClean, Status.None, null, true, true, val1, runs1)

            (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateMaterializedStage (
                m2,
                box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                7uy,
                m1.Node :> ISource,
                m1 :> INode,
                false,
                NodeFlags.FreshClean,
                Status.None,
                null,
                true,
                true,
                val2,
                runs2
            )

            (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateActiveStage (
                m3,
                box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                7uy,
                m2.Node :> ISource,
                (publishedMask &&& 4uy) <> 0uy,
                val3,
                runs3
            )

            FusedStageHelpers.populateWaitingStage (
                selfMemo,
                box (FusedSourceMap1<'C, 'D>(MemoSource<'C>(m3), f4, not selfMemo.Node.IsDisposed)),
                7uy,
                m3.Node :> ISource,
                m3 :> INode,
                prevTailStatus,
                prevTailFailure,
                hasSelfReady,
                hasSelfPublished,
                prevSelfVal,
                prevSelfRuns
            )
        else
            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeWarmPrefix(m1, NodeFlags.FreshClean, Status.None, null, true, true, val1, runs1)

            (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateMaterializedStage (
                m2,
                box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                7uy,
                m1.Node :> ISource,
                m1 :> INode,
                false,
                NodeFlags.FreshClean,
                Status.None,
                null,
                true,
                true,
                val2,
                runs2
            )

            (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateMaterializedStage (
                m3,
                box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                7uy,
                m2.Node :> ISource,
                m2 :> INode,
                false,
                NodeFlags.FreshClean,
                Status.None,
                null,
                true,
                true,
                val3,
                runs3
            )

            FusedStageHelpers.populateActiveStage (
                selfMemo,
                box (FusedSourceMap1<'C, 'D>(MemoSource<'C>(m3), f4, not selfMemo.Node.IsDisposed)),
                7uy,
                m3.Node :> ISource,
                hasSelfPublished,
                prevSelfVal,
                prevSelfRuns + 1
            )

            readyMask <-
                if hasSelfReady then
                    readyMask ||| 8uy
                else
                    readyMask &&& ~~~8uy

    override _.MaterializeWaitingRoot(_, _, _, _, _, _, _) = ()

    override this.MaterializeTail(tailObj: obj) =
        let tailMemo = Platform.fastCast<Memo<'D>> tailObj
        let tn = tailMemo.Node
        let step = activeStep

        if step <> 0uy && step <> 255uy then
            let prevSt = tn.Status
            let prevFail = tn.FailureField

            if step = 254uy then
                activeStep <- 255uy

                (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                    .MaterializeWaitingRoot(m1, prevSt, prevFail, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1)

                (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                FusedStageHelpers.populateWaitingStage (
                    m2,
                    box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                    7uy,
                    m1.Node :> ISource,
                    m1 :> INode,
                    prevSt,
                    prevFail,
                    (readyMask &&& 2uy) <> 0uy,
                    (publishedMask &&& 2uy) <> 0uy,
                    val2,
                    runs2
                )

                (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

                FusedStageHelpers.populateWaitingStage (
                    m3,
                    box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                    7uy,
                    m2.Node :> ISource,
                    m2 :> INode,
                    prevSt,
                    prevFail,
                    (readyMask &&& 4uy) <> 0uy,
                    (publishedMask &&& 4uy) <> 0uy,
                    val3,
                    runs3
                )

                FusedStageHelpers.finishActiveTailMaterialization (tailMemo, m3, f4)
            elif step < 4uy then
                activeStep <- 255uy

                if step = 1uy then
                    (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                        .MaterializeActivePrefix(m1, prevSt, prevFail, (readyMask &&& 1uy) <> 0uy, (publishedMask &&& 1uy) <> 0uy, val1, runs1 - 1)

                    (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                    FusedStageHelpers.populateWaitingStage (
                        m2,
                        box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                        7uy,
                        m1.Node :> ISource,
                        m1 :> INode,
                        prevSt,
                        prevFail,
                        (readyMask &&& 2uy) <> 0uy,
                        (publishedMask &&& 2uy) <> 0uy,
                        val2,
                        runs2
                    )

                    (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

                    FusedStageHelpers.populateWaitingStage (
                        m3,
                        box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                        7uy,
                        m2.Node :> ISource,
                        m2 :> INode,
                        prevSt,
                        prevFail,
                        (readyMask &&& 4uy) <> 0uy,
                        (publishedMask &&& 4uy) <> 0uy,
                        val3,
                        runs3
                    )
                elif step = 2uy then
                    (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                        .MaterializeWarmPrefix(m1, NodeFlags.FreshClean, Status.None, null, true, true, val1, runs1)

                    (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                    FusedStageHelpers.populateActiveStage (
                        m2,
                        box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                        7uy,
                        m1.Node :> ISource,
                        (publishedMask &&& 2uy) <> 0uy,
                        val2,
                        runs2
                    )

                    (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

                    FusedStageHelpers.populateWaitingStage (
                        m3,
                        box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                        7uy,
                        m2.Node :> ISource,
                        m2 :> INode,
                        prevSt,
                        prevFail,
                        (readyMask &&& 4uy) <> 0uy,
                        (publishedMask &&& 4uy) <> 0uy,
                        val3,
                        runs3
                    )
                else
                    (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                        .MaterializeWarmPrefix(m1, NodeFlags.FreshClean, Status.None, null, true, true, val1, runs1)

                    (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                    FusedStageHelpers.populateMaterializedStage (
                        m2,
                        box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                        7uy,
                        m1.Node :> ISource,
                        m1 :> INode,
                        false,
                        NodeFlags.FreshClean,
                        Status.None,
                        null,
                        true,
                        true,
                        val2,
                        runs2
                    )

                    (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

                    FusedStageHelpers.populateActiveStage (
                        m3,
                        box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                        7uy,
                        m2.Node :> ISource,
                        (publishedMask &&& 4uy) <> 0uy,
                        val3,
                        runs3
                    )

                FusedStageHelpers.finishActiveTailMaterialization (tailMemo, m3, f4)
            else
                activeStep <- 255uy
                let tailFresh = tn.Flags &&& NodeFlags.FreshMask
                tn.SourcesField.Clear (tn :> IComputation)

                (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                    .MaterializeWarmPrefix(m1, tailFresh, Status.None, null, true, true, val1, runs1)

                (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

                FusedStageHelpers.populateMaterializedStage (
                    m2,
                    box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                    7uy,
                    m1.Node :> ISource,
                    m1 :> INode,
                    false,
                    tailFresh,
                    Status.None,
                    null,
                    true,
                    true,
                    val2,
                    runs2
                )

                (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

                FusedStageHelpers.populateMaterializedStage (
                    m3,
                    box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                    7uy,
                    m2.Node :> ISource,
                    m2 :> INode,
                    false,
                    tailFresh,
                    Status.None,
                    null,
                    true,
                    true,
                    val3,
                    runs3
                )

                FusedStageHelpers.finishTailMaterialization (tailMemo, m3, f4, tailFresh, Status.None)
        elif tn.RunsField = 0 then
            this.DetachUnwarmedPrefix tailMemo
        else
            let tailFresh = tn.Flags &&& NodeFlags.FreshMask
            let tailStatus = tn.Status
            let tailFailure = tn.FailureField
            tn.SourcesField.Clear (tn :> IComputation)

            (Platform.fastCast<FusedExtendable<'A>> m1.ComputeObj)
                .MaterializeWarmPrefix(
                    m1,
                    tailFresh,
                    tailStatus,
                    tailFailure,
                    (readyMask &&& 1uy) <> 0uy,
                    (publishedMask &&& 1uy) <> 0uy,
                    val1,
                    runs1
                )

            (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateMaterializedStage (
                m2,
                box (FusedSourceMap1<'A, 'B>(MemoSource<'A>(m1), f2, not m2.Node.IsDisposed)),
                7uy,
                m1.Node :> ISource,
                m1 :> INode,
                false,
                tailFresh,
                tailStatus,
                tailFailure,
                (readyMask &&& 2uy) <> 0uy,
                (publishedMask &&& 2uy) <> 0uy,
                val2,
                runs2
            )

            (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

            FusedStageHelpers.populateMaterializedStage (
                m3,
                box (FusedSourceMap1<'B, 'C>(MemoSource<'B>(m2), f3, not m3.Node.IsDisposed)),
                7uy,
                m2.Node :> ISource,
                m2 :> INode,
                false,
                tailFresh,
                tailStatus,
                tailFailure,
                (readyMask &&& 4uy) <> 0uy,
                (publishedMask &&& 4uy) <> 0uy,
                val3,
                runs3
            )

            FusedStageHelpers.finishTailMaterialization (tailMemo, m3, f4, tailFresh, tailStatus)

    override _.ClearIntermediates() =
        (Platform.fastCast<FusedRunner<'A>> m1.ComputeObj).FusedNext <- Platform.nil ()
        (Platform.fastCast<FusedRunner<'B>> m2.ComputeObj).FusedNext <- Platform.nil ()
        (Platform.fastCast<FusedRunner<'C>> m3.ComputeObj).FusedNext <- Platform.nil ()

/// <summary>Typed stage-&gt;=5 extension over a 4-stage (or deeper) <c>FusedExtendable</c> prefix (zero boxing).</summary>
and [<Sealed>] internal FusedChainMap<'Prev, 'U>(prefix: FusedExtendable<'Prev>, prevMemo: Memo<'Prev>, fTail: 'Prev -> 'U) =
    inherit FusedExtendable<'U>()

    let eqPrev = prevMemo.EncodedEqual
    let mutable prevVal = Unchecked.defaultof<'Prev>
    let mutable prevRuns = 0
    let mutable hasPrevReady = false
    let mutable hasPrevPublished = false
    let mutable savedSelfReady = false
    let mutable activeStep = 0uy

    override this.Fuse<'Next>(curMemo: Memo<'U>, mapping: 'U -> 'Next) =
        struct (FusedChainMap<'U, 'Next>(this, curMemo, mapping) :> FusedExtendable<'Next>, 7uy)

    override _.LinkRoot(n: MemoNode) =
        prefix.LinkRoot n

    override _.MarkRootDirtyIfLive() =
        prefix.MarkRootDirtyIfLive ()

    override _.EvalSignalLeaf(_tailObj, _n, _g, _safeTailEq, _outputEqual, _previous) = false

    override this.EvalPrefix(g, n, selfMemo, previousStatus, previousFailure) =
        activeStep <- 1uy
        let outcome = prefix.EvalPrefix (g, n, prevMemo, previousStatus, previousFailure)

        if outcome.Tag = 4uy then
            activeStep <- 0uy
            FusedStageHelpers.advanceWaitingStageFromCurrentPred (selfMemo, prevMemo)

            FusedPrefixOutcome<'U>(4uy, Unchecked.defaultof<'U>, outcome.MovedToUnready)
        else
            activeStep <- 0uy

            if outcome.Tag = 0uy then
                FusedPrefixOutcome<'U>(0uy, Unchecked.defaultof<'U>, false)
            elif outcome.Tag = 2uy || outcome.Tag = 3uy then
                let wasUnready = not hasPrevReady && prevRuns > 0
                hasPrevReady <- false
                let unreadyMoved = outcome.MovedToUnready || not wasUnready

                if unreadyMoved then
                    prevRuns <- prevRuns + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
#endif

                FusedPrefixOutcome<'U>(outcome.Tag, Unchecked.defaultof<'U>, unreadyMoved)
            else
                let wasReady = hasPrevReady
                let vPrev = outcome.Value
                prevRuns <- prevRuns + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
#endif
                let mutable stepIdx = 0
                let mutable cutoff = false
                let mutable nextVal = Unchecked.defaultof<'U>
                let mutable deFusedVal = Unchecked.defaultof<'U>
                let mutable samePrev = false

#if !FABLE_COMPILER
                let saved = g.EnterUntrackedPure (n :> IScopeHost)
#endif
                let mutable thrownEx: exn = Platform.nil ()

                try
                    activeStep <- 3uy

                    if wasReady then
                        samePrev <- ComparerSentinel.equals eqPrev prevVal vPrev

                    if activeStep <> 255uy then
                        if samePrev then
                            cutoff <- true
                        else
                            prevVal <- vPrev
                            hasPrevReady <- true
                            hasPrevPublished <- true
                            stepIdx <- 1
                            activeStep <- 2uy
                            let v = FusedStageHelpers.runPureStage (g, n, fTail, vPrev)

                            if activeStep = 255uy then deFusedVal <- v else nextVal <- v
                with ex ->
#if FABLE_COMPILER
                    thrownEx <- Thrown.hold ex
#else
                    thrownEx <- ex
#endif

                let wasDeFused = activeStep = 255uy
                activeStep <- 0uy

#if !FABLE_COMPILER
                g.LeaveUntrackedPure saved
#endif

                if wasDeFused then
                    let reDirtied = FusedStageHelpers.prepareTailBeforeStagePublish n

                    let ex =
                        if Platform.isNull thrownEx then
                            Platform.nil ()
                        else
#if FABLE_COMPILER
                            Thrown.take thrownEx
#else
                            thrownEx
#endif

                    if stepIdx = 0 then
                        if not (Platform.isNull ex) then
                            FusedStageHelpers.publishChainPrevCompareFailure (prevMemo, ex, previousFailure)
                        else
                            FusedStageHelpers.publishChainPrevCompareSuccess (prevMemo, vPrev, not wasReady || not samePrev)

                        FusedStageHelpers.advanceWaitingStageFromCurrentPred (selfMemo, prevMemo)

                        FusedPrefixOutcome<'U>(4uy, Unchecked.defaultof<'U>, reDirtied)
                    else
                        let prevSelfRuns = selfMemo.Node.RunsField - 1

                        FusedStageHelpers.finishDeFusedStage (
                            selfMemo,
                            deFusedVal,
                            ex,
                            prevSelfRuns,
                            savedSelfReady,
                            previousStatus,
                            previousFailure,
                            prevMemo :> INode,
                            -1y
                        )

                        FusedPrefixOutcome<'U>(4uy, Unchecked.defaultof<'U>, reDirtied)
                elif not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
                    let ex = Thrown.take thrownEx
#else
                    let ex = thrownEx
#endif
                    if stepIdx = 0 then
                        hasPrevReady <- false

                    let errOrigin = if stepIdx = 0 then prevMemo :> INode else selfMemo :> INode

                    CombinatorHelpers.recordFusedFailure (g, n, errOrigin, stepIdx = 0, ex, previousFailure)

                    let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

                    let unreadyMoved =
                        if stepIdx = 0 then
                            wasReady || prevRuns = 1 || moved
                        else
                            moved

                    FusedPrefixOutcome<'U>(3uy, Unchecked.defaultof<'U>, unreadyMoved)
                elif cutoff then
                    FusedPrefixOutcome<'U>(0uy, Unchecked.defaultof<'U>, false)
                else
                    FusedPrefixOutcome<'U>(1uy, nextVal, false)

    override this.ExecuteFused(tailObj, n, outputEqual, previous, previousStatus, previousFailure) =
        let tailMemo = Platform.fastCast<Memo<'U>> tailObj
        let origin = tailMemo :> INode
        let g = n.Graph
        this.LinkRoot n
        activeStep <- 1uy
        let outcome = prefix.EvalPrefix (g, n, prevMemo, previousStatus, previousFailure)
        activeStep <- 0uy

        if outcome.Tag = 4uy then
            FusedStageHelpers.finishDeFusedTail (
                this,
                tailMemo,
                prevMemo,
                n,
                outputEqual,
                previous,
                previousStatus,
                previousFailure,
                outcome.MovedToUnready
            )
        elif n.IsDisposed then
            CombinatorHelpers.restoreDisposedState (n, previousStatus, previousFailure, n.PendingSourceField)
            this.Moved <- false
            previous
        elif outcome.Tag = 0uy then
            CombinatorHelpers.clearActive n
            this.Moved <- false
            previous
        elif outcome.Tag = 2uy then
            let wasUnready = not hasPrevReady && prevRuns > 0
            hasPrevReady <- false

            if outcome.MovedToUnready || not wasUnready then
                prevRuns <- prevRuns + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
#endif

            let moved = not (StatusBits.isPending previousStatus)

            if not wasUnready || moved then
                n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
#endif

            n.PendingSourceField <- prevMemo :> INode
            CombinatorHelpers.clearActive n
            this.Moved <- moved
            previous
        elif outcome.Tag = 3uy then
            let wasUnready = not hasPrevReady && prevRuns > 0
            hasPrevReady <- false
            let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

            if outcome.MovedToUnready || not wasUnready || moved then
                prevRuns <- prevRuns + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
#endif

            if not wasUnready || moved then
                n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                Counters.MemoRecomputed ()
#endif

            CombinatorHelpers.clearActive n
            this.Moved <- moved
            previous
        else
            let wasReady = hasPrevReady
            let prevRunsBefore = prevRuns
            let vPrev = outcome.Value
            prevRuns <- prevRuns + 1
#if RANVIER_COUNTERS
            Counters.MemoRecomputed ()
#endif
            let mutable stepIdx = 0
            let mutable cutoff = false
            let mutable nextVal = previous
            let mutable samePrev = false

#if !FABLE_COMPILER
            let savedTail = g.EnterUntrackedPure (n :> IScopeHost)
#endif
            let mutable thrownEx: exn = Platform.nil ()

            try
                activeStep <- 3uy

                if wasReady then
                    samePrev <- ComparerSentinel.equals eqPrev prevVal vPrev

                if activeStep <> 255uy then
                    if samePrev then
                        cutoff <- true
                    else
                        prevVal <- vPrev
                        hasPrevReady <- true
                        hasPrevPublished <- true
                        stepIdx <- 1
                        activeStep <- 2uy
                        n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                        Counters.MemoRecomputed ()
#endif
                        nextVal <- FusedStageHelpers.runPureStage (g, n, fTail, vPrev)
            with ex ->
#if FABLE_COMPILER
                thrownEx <- Thrown.hold ex
#else
                thrownEx <- ex
#endif

            let wasDeFused = activeStep = 255uy
            activeStep <- 0uy

#if !FABLE_COMPILER
            g.LeaveUntrackedPure savedTail
#endif

            if wasDeFused && stepIdx = 0 then
                let reDirtied = FusedStageHelpers.prepareTailBeforeStagePublish n

                let ex =
                    if Platform.isNull thrownEx then
                        Platform.nil ()
                    else
#if FABLE_COMPILER
                        Thrown.take thrownEx
#else
                        thrownEx
#endif

                if not (Platform.isNull ex) then
                    FusedStageHelpers.publishChainPrevCompareFailure (prevMemo, ex, previousFailure)
                else
                    FusedStageHelpers.publishChainPrevCompareSuccess (prevMemo, vPrev, not wasReady || not samePrev)

                FusedStageHelpers.finishDeFusedTail (this, tailMemo, prevMemo, n, outputEqual, previous, previousStatus, previousFailure, reDirtied)
            elif not (Platform.isNull thrownEx) then
#if FABLE_COMPILER
                let ex = Thrown.take thrownEx
#else
                let ex = thrownEx
#endif
                if stepIdx = 0 then
                    hasPrevReady <- false

                let errOrigin = if stepIdx = 0 then prevMemo :> INode else origin

                CombinatorHelpers.recordFusedFailure (g, n, errOrigin, stepIdx = 0, ex, previousFailure)

                let moved = CombinatorHelpers.statusMoved (n, previousStatus, previousFailure)

                if
                    stepIdx = 0
                    && (prevRunsBefore = 0 || wasReady || moved)
                then
                    n.RunsField <- n.RunsField + 1
#if RANVIER_COUNTERS
                    Counters.MemoRecomputed ()
#endif

                this.Moved <- moved
                previous
            elif cutoff then
                CombinatorHelpers.clearActive n
                this.Moved <- false
                previous
            else
                CombinatorHelpers.finishReady (this, g, n, origin, outputEqual, previous, nextVal, previousStatus, previousFailure)

    override this.DetachUnwarmedPrefix(selfMemo: Memo<'U>) =
        this.FusedNext <- Platform.nil ()
        prefix.DetachUnwarmedPrefix prevMemo
        selfMemo.RebindFused (box (FusedSourceMap1<'Prev, 'U>(MemoSource<'Prev>(prevMemo), fTail, false)), 7uy)

    override this.MaterializeWarmPrefix
        (
            selfMemo: Memo<'U>,
            tailFresh: int,
            tailStatus: Status,
            tailFailure: Failure,
            hasSelfReady: bool,
            hasSelfPublished: bool,
            selfVal: 'U,
            selfRuns: int
        ) =
        this.FusedNext <- Platform.nil ()

        prefix.MaterializeWarmPrefix (prevMemo, tailFresh, tailStatus, tailFailure, hasPrevReady, hasPrevPublished, prevVal, prevRuns)

        FusedStageHelpers.populateMaterializedStage (
            selfMemo,
            box (FusedSourceMap1<'Prev, 'U>(MemoSource<'Prev>(prevMemo), fTail, not selfMemo.Node.IsDisposed)),
            7uy,
            prevMemo.Node :> ISource,
            prevMemo :> INode,
            false,
            tailFresh,
            tailStatus,
            tailFailure,
            hasSelfReady,
            hasSelfPublished,
            selfVal,
            selfRuns
        )

    override this.MaterializeActivePrefix
        (
            selfMemo: Memo<'U>,
            prevTailStatus: Status,
            prevTailFailure: Failure,
            hasSelfReady: bool,
            hasSelfPublished: bool,
            prevSelfVal: 'U,
            prevSelfRuns: int
        ) =
        let step = activeStep
        activeStep <- 255uy
        this.FusedNext <- Platform.nil ()

        if step = 1uy then
            prefix.MaterializeActivePrefix (prevMemo, prevTailStatus, prevTailFailure, hasPrevReady, hasPrevPublished, prevVal, prevRuns)

            FusedStageHelpers.populateWaitingStage (
                selfMemo,
                box (FusedSourceMap1<'Prev, 'U>(MemoSource<'Prev>(prevMemo), fTail, not selfMemo.Node.IsDisposed)),
                7uy,
                prevMemo.Node :> ISource,
                prevMemo :> INode,
                prevTailStatus,
                prevTailFailure,
                hasSelfReady,
                hasSelfPublished,
                prevSelfVal,
                prevSelfRuns
            )
        elif step = 3uy then
            prefix.MaterializeWarmPrefix (prevMemo, NodeFlags.FreshClean, Status.None, null, hasPrevReady, hasPrevPublished, prevVal, prevRuns)

            if not prevMemo.Node.IsDisposed then
                prevMemo.Node.Flags <-
                    (prevMemo.Node.Flags &&& ~~~NodeFlags.ActiveMask)
                    ||| NodeFlags.ActiveBody

            FusedStageHelpers.populateWaitingStage (
                selfMemo,
                box (FusedSourceMap1<'Prev, 'U>(MemoSource<'Prev>(prevMemo), fTail, not selfMemo.Node.IsDisposed)),
                7uy,
                prevMemo.Node :> ISource,
                prevMemo :> INode,
                prevTailStatus,
                prevTailFailure,
                hasSelfReady,
                hasSelfPublished,
                prevSelfVal,
                prevSelfRuns
            )
        else
            savedSelfReady <- hasSelfReady
            prefix.MaterializeWarmPrefix (prevMemo, NodeFlags.FreshClean, Status.None, null, true, true, prevVal, prevRuns)

            FusedStageHelpers.populateActiveStage (
                selfMemo,
                box (FusedSourceMap1<'Prev, 'U>(MemoSource<'Prev>(prevMemo), fTail, not selfMemo.Node.IsDisposed)),
                7uy,
                prevMemo.Node :> ISource,
                hasSelfPublished,
                prevSelfVal,
                prevSelfRuns + 1
            )

    override _.MaterializeWaitingRoot(_, _, _, _, _, _, _) = ()

    override this.MaterializeTail(tailObj: obj) =
        let tailMemo = Platform.fastCast<Memo<'U>> tailObj
        let tn = tailMemo.Node
        let step = activeStep

        if step <> 0uy && step <> 255uy then
            activeStep <- 255uy

            if step = 1uy then
                prefix.MaterializeActivePrefix (prevMemo, tn.Status, tn.FailureField, hasPrevReady, hasPrevPublished, prevVal, prevRuns)
                FusedStageHelpers.finishActiveTailMaterialization (tailMemo, prevMemo, fTail)
            elif step = 3uy then
                prefix.MaterializeWarmPrefix (prevMemo, NodeFlags.FreshClean, Status.None, null, hasPrevReady, hasPrevPublished, prevVal, prevRuns)

                if not prevMemo.Node.IsDisposed then
                    prevMemo.Node.Flags <-
                        (prevMemo.Node.Flags &&& ~~~NodeFlags.ActiveMask)
                        ||| NodeFlags.ActiveBody

                FusedStageHelpers.finishActiveTailMaterialization (tailMemo, prevMemo, fTail)
            else
                let tailFresh = tn.Flags &&& NodeFlags.FreshMask
                tn.SourcesField.Clear (tn :> IComputation)
                prefix.MaterializeWarmPrefix (prevMemo, tailFresh, Status.None, null, true, true, prevVal, prevRuns)
                FusedStageHelpers.finishTailMaterialization (tailMemo, prevMemo, fTail, tailFresh, Status.None)
        elif tn.RunsField = 0 then
            this.DetachUnwarmedPrefix tailMemo
        else
            let tailFresh = tn.Flags &&& NodeFlags.FreshMask
            let tailStatus = tn.Status
            let tailFailure = tn.FailureField
            tn.SourcesField.Clear (tn :> IComputation)
            prefix.MaterializeWarmPrefix (prevMemo, tailFresh, tailStatus, tailFailure, hasPrevReady, hasPrevPublished, prevVal, prevRuns)
            FusedStageHelpers.finishTailMaterialization (tailMemo, prevMemo, fTail, tailFresh, tailStatus)

    override _.ClearIntermediates() =
        prefix.ClearIntermediates ()
        prefix.FusedNext <- Platform.nil ()
#endif

/// <summary>
/// Factory entry points backing the public <c>Signal</c>, <c>Memo</c>, <c>AsyncSource</c>, <c>AsyncMemo</c>, and
/// <c>StreamMemo</c> modules.
/// </summary>
module internal CombinatorMemos =
    let createSignalMap (mapping: 'T -> 'U) (source: Signal<'T>) : Memo<'U> =
        if UntrackedPhase.isNullFn1 mapping then
            nullArg (nameof mapping)

        if Platform.isNull (box source) then
            nullArg (nameof source)

        let g = source.Graph

        g.Entered (
            "Creating a node",
            fun () ->
                let eq = g.EncodedComparer<'U>()
#if !RANVIER_TRACE
                if Platform.isNull g.Journal then
                    let runner = FusedSignalMap1<'T, 'U>(source, mapping, false)
                    Memo<'U>.CreateCombinator(g, box runner, 6uy, eq)
                else
                    let runner = SignalMapState<'T, 'U>(source, mapping)
                    Memo<'U>.CreateCombinator(g, box runner, 5uy, eq)
#else
                let runner = SignalMapState<'T, 'U>(source, mapping)
                Memo<'U>.CreateCombinator(g, box runner, 5uy, eq)
#endif
        )

    let createMemoMap (mapping: 'T -> 'U) (source: Memo<'T>) : Memo<'U> =
        if UntrackedPhase.isNullFn1 mapping then
            nullArg (nameof mapping)

        if Platform.isNull (box source) then
            nullArg (nameof source)

        let g = source.Graph

        g.Entered (
            "Creating a node",
            fun () ->
                let eq = g.EncodedComparer<'U>()
#if !RANVIER_TRACE
                if Platform.isNull g.Journal then
                    let targetOwner = g.CurrentOwner

                    if
                        source.ComputeKind >= 6uy
                        && not source.Node.IsDisposed
                        && (source.Node.Flags &&& NodeFlags.ActiveMask) = 0
                        && source.Node.RunsField = 0
                        && source.Node.ObserverCount = 0
                        && not targetOwner.IsDisposed
                        && obj.ReferenceEquals (source.Node.Owner, targetOwner)
                        && not (Platform.fastCast<FusedRunner<'T>> source.ComputeObj).IsClaimed
                    then
                        let prevRunner = Platform.fastCast<FusedExtendable<'T>> source.ComputeObj
                        let struct (nextRunner, nextKind) = prevRunner.Fuse<'U>(source, mapping)
                        let tail = Memo<'U>.CreateCombinator(g, box nextRunner, nextKind, eq)
                        prevRunner.FusedNext <- tail :> IFusedChainNode
                        tail
                    else
                        source.EnsureMaterializedIfClaimed ()
                        let runner = FusedSourceMap1<'T, 'U>(MemoSource<'T>(source), mapping, false)
                        Memo<'U>.CreateCombinator(g, box runner, 7uy, eq)
                else
                    source.EnsureMaterializedIfClaimed ()
                    let runner = SourceMapState<'T, 'U>(MemoSource<'T>(source), mapping)
                    Memo<'U>.CreateCombinator(g, box runner, 5uy, eq)
#else
                let runner = SourceMapState<'T, 'U>(MemoSource<'T>(source), mapping)
                Memo<'U>.CreateCombinator(g, box runner, 5uy, eq)
#endif
        )

    let createSourceMap (mapping: 'T -> 'U) (src: CombinatorSource<'T>) : Memo<'U> =
        let g = src.Graph

        g.Entered (
            "Creating a node",
            fun () ->
                let eq = g.EncodedComparer<'U>()
#if !RANVIER_TRACE
                if Platform.isNull g.Journal then
                    let runner = FusedSourceMap1<'T, 'U>(src, mapping, false)
                    Memo<'U>.CreateCombinator(g, box runner, 7uy, eq)
                else
                    let runner = SourceMapState<'T, 'U>(src, mapping)
                    Memo<'U>.CreateCombinator(g, box runner, 5uy, eq)
#else
                let runner = SourceMapState<'T, 'U>(src, mapping)
                Memo<'U>.CreateCombinator(g, box runner, 5uy, eq)
#endif
        )

    let createSignalMap2 (mapping: 'A -> 'B -> 'C) (source1: Signal<'A>) (source2: Signal<'B>) : Memo<'C> =
        if UntrackedPhase.isNullFn2 mapping then
            nullArg (nameof mapping)

        if Platform.isNull (box source1) then
            nullArg (nameof source1)

        if Platform.isNull (box source2) then
            nullArg (nameof source2)

        let g = source1.Graph

        if not (obj.ReferenceEquals (g, source2.Graph)) then
            raise (InvalidOperationException UntrackedPhase.crossGraphNodes)

        let adapted = UntrackedPhase.adaptMap2 mapping

        g.Entered (
            "Creating a node",
            fun () ->
                let eq = g.EncodedComparer<'C>()
                let runner = SignalMap2State<'A, 'B, 'C>(source1, source2, adapted)
                Memo<'C>.CreateCombinator(g, box runner, 5uy, eq)
        )

    let createMemoMap2 (mapping: 'A -> 'B -> 'C) (source1: Memo<'A>) (source2: Memo<'B>) : Memo<'C> =
        if UntrackedPhase.isNullFn2 mapping then
            nullArg (nameof mapping)

        if Platform.isNull (box source1) then
            nullArg (nameof source1)

        if Platform.isNull (box source2) then
            nullArg (nameof source2)

        let g = source1.Graph

        if not (obj.ReferenceEquals (g, source2.Graph)) then
            raise (InvalidOperationException UntrackedPhase.crossGraphNodes)

        let adapted = UntrackedPhase.adaptMap2 mapping

        g.Entered (
            "Creating a node",
            fun () ->
                let eq = g.EncodedComparer<'C>()
                let runner = MemoMap2State<'A, 'B, 'C>(source1, source2, adapted)
                Memo<'C>.CreateCombinator(g, box runner, 5uy, eq)
        )

    let createSourceMap2 (mapping: 'A -> 'B -> 'C) (s1: CombinatorSource<'A>) (s2: CombinatorSource<'B>) : Memo<'C> =
        let g = s1.Graph

        if not (obj.ReferenceEquals (g, s2.Graph)) then
            raise (InvalidOperationException UntrackedPhase.crossGraphNodes)

        let adapted = UntrackedPhase.adaptMap2 mapping

        g.Entered (
            "Creating a node",
            fun () ->
                let eq = g.EncodedComparer<'C>()
                let runner = SourceMap2State<'A, 'B, 'C>(s1, s2, adapted)
                Memo<'C>.CreateCombinator(g, box runner, 5uy, eq)
        )

    let createSignalZip (source1: Signal<'A>) (source2: Signal<'B>) : Memo<'A * 'B> =
#if FABLE_COMPILER
        createSignalMap2 (fun a b -> a, b) source1 source2
#else
        if Platform.isNull (box source1) then
            nullArg (nameof source1)

        if Platform.isNull (box source2) then
            nullArg (nameof source2)

        let g = source1.Graph

        if not (obj.ReferenceEquals (g, source2.Graph)) then
            raise (InvalidOperationException UntrackedPhase.crossGraphNodes)

        g.Entered (
            "Creating a node",
            fun () ->
                let eq = g.EncodedComparer<'A * 'B>()

                let runner =
                    SignalMap2State<'A, 'B, 'A * 'B>(source1, source2, ZipPair<'A, 'B>.Adapted)

                Memo<'A * 'B>.CreateCombinator(g, box runner, 5uy, eq)
        )
#endif

    let createMemoZip (source1: Memo<'A>) (source2: Memo<'B>) : Memo<'A * 'B> =
#if FABLE_COMPILER
        createMemoMap2 (fun a b -> a, b) source1 source2
#else
        if Platform.isNull (box source1) then
            nullArg (nameof source1)

        if Platform.isNull (box source2) then
            nullArg (nameof source2)

        let g = source1.Graph

        if not (obj.ReferenceEquals (g, source2.Graph)) then
            raise (InvalidOperationException UntrackedPhase.crossGraphNodes)

        g.Entered (
            "Creating a node",
            fun () ->
                let eq = g.EncodedComparer<'A * 'B>()

                let runner =
                    MemoMap2State<'A, 'B, 'A * 'B>(source1, source2, ZipPair<'A, 'B>.Adapted)

                Memo<'A * 'B>.CreateCombinator(g, box runner, 5uy, eq)
        )
#endif

    let createSourceZip (s1: CombinatorSource<'A>) (s2: CombinatorSource<'B>) : Memo<'A * 'B> =
#if FABLE_COMPILER
        createSourceMap2 (fun a b -> a, b) s1 s2
#else
        let g = s1.Graph

        if not (obj.ReferenceEquals (g, s2.Graph)) then
            raise (InvalidOperationException UntrackedPhase.crossGraphNodes)

        g.Entered (
            "Creating a node",
            fun () ->
                let eq = g.EncodedComparer<'A * 'B>()
                let runner = SourceMap2State<'A, 'B, 'A * 'B>(s1, s2, ZipPair<'A, 'B>.Adapted)
                Memo<'A * 'B>.CreateCombinator(g, box runner, 5uy, eq)
        )
#endif

    let createSignalBind (binder: 'T -> Memo<'U>) (source: Signal<'T>) : Memo<'U> =
        if UntrackedPhase.isNullFn1 binder then
            nullArg (nameof binder)

        if Platform.isNull (box source) then
            nullArg (nameof source)

        let g = source.Graph

        g.Entered (
            "Creating a node",
            fun () ->
                let eq = g.EncodedComparer<'U>()
                let state = SplitBindState<'T, 'U>(g, source, Platform.nil (), binder)
                let memo = Memo<'U>.CreateCombinator(g, box state, 5uy, eq)
                state.Bind memo.Node
                memo
        )

    let createMemoBind (binder: 'T -> Memo<'U>) (source: Memo<'T>) : Memo<'U> =
        if UntrackedPhase.isNullFn1 binder then
            nullArg (nameof binder)

        if Platform.isNull (box source) then
            nullArg (nameof source)

        let g = source.Graph

        g.Entered (
            "Creating a node",
            fun () ->
                let eq = g.EncodedComparer<'U>()
                let state = SplitBindState<'T, 'U>(g, Platform.nil (), source, binder)
                let memo = Memo<'U>.CreateCombinator(g, box state, 5uy, eq)
                state.Bind memo.Node
                memo
        )

    let ensureSameSignalMemoGraph (outer: Signal<'T>) (inner: Memo<'U>) : unit =
        if not (obj.ReferenceEquals (outer.Graph, inner.Graph)) then
            raise (InvalidOperationException UntrackedPhase.crossGraphBind)

    let ensureSameMemoGraph (outer: Memo<'T>) (inner: Memo<'U>) : unit =
        if not (obj.ReferenceEquals (outer.Graph, inner.Graph)) then
            raise (InvalidOperationException UntrackedPhase.crossGraphBind)

    let createReactiveBind
        (
            graph: Graph,
            readKey: unit -> 'K,
            binder: 'K -> Memo<'U>,
            keyComparer: IEqualityComparer<'K> voption,
            outputComparer: IEqualityComparer<'U> voption
        ) : Memo<'U> =
        if Platform.isNull (box graph) then
            nullArg (nameof graph)

        if UntrackedPhase.isNullFn1 readKey then
            nullArg (nameof readKey)

        if UntrackedPhase.isNullFn1 binder then
            nullArg (nameof binder)

        let keyEq =
            match keyComparer with
            | ValueSome c ->
                if Platform.isNull c then
                    nullArg (nameof keyComparer)

                ComparerSentinel.encode c
            | ValueNone -> graph.EncodedComparer<'K>()

        let outputEq =
            match outputComparer with
            | ValueSome c ->
                if Platform.isNull c then
                    nullArg (nameof outputComparer)

                ComparerSentinel.encode c
            | ValueNone -> graph.EncodedComparer<'U>()

        graph.Entered (
            "Creating a node",
            fun () ->
                let state = ReactiveSplitBindState<'K, 'U>(readKey, binder, keyEq)
                let memo = Memo<'U>.CreateCombinator(graph, box state, 5uy, outputEq)
                state.Bind memo.Node
                memo
        )
