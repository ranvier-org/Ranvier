namespace Ranvier

/// <summary>Debounce and throttle factories with explicit graph ownership.</summary>
/// <remarks>Durations are nonnegative; zero publishes through normal graph scheduling. Captures run in a pure scope.</remarks>
[<AutoOpen>]
module TimedCombinators =
    let private timed mode options delay read (graph: Graph) =
        graph.Entered (
            "Creating a node",
            fun () ->
                let node = new Timed<_> (graph, options, delay, mode, read)
                node.Start ()
                node
        )

    /// <summary>Publishes the latest changed capture after <c>delay</c> of quiet, using the supplied clock and comparer.</summary>
    let debounceWith options delay read graph =
        timed TimedMode.Debounce options delay read graph

    /// <summary>Publishes the latest changed capture after <c>delay</c> of quiet, using system time and graph equality.</summary>
    let debounce delay read graph =
        debounceWith TimedOptions.defaults delay read graph

    /// <summary>Admits the first eligible changed capture and discards changes during its cooldown.</summary>
    /// <remarks>Uses the supplied clock and comparer; creates no timer.</remarks>
    let throttleFirstWith options interval read graph =
        timed TimedMode.First options interval read graph

    /// <summary>Admits leading changes at most once per interval, using system time and graph equality.</summary>
    let throttleFirst interval read graph =
        throttleFirstWith TimedOptions.defaults interval read graph

    /// <summary>Admits the latest changed capture at the first change's fixed deadline, using the supplied options.</summary>
    let throttleLastWith options interval read graph =
        timed TimedMode.Last options interval read graph

    /// <summary>Admits the latest changed capture after each fixed window, using system time and graph equality.</summary>
    let throttleLast interval read graph =
        throttleLastWith TimedOptions.defaults interval read graph

    /// <summary>Admits a leading change and one latest trailing change per interval, using the supplied options.</summary>
    /// <remarks>A trailing admission starts another cooldown at actual owner-thread admission time.</remarks>
    let throttleWith options interval read graph =
        timed TimedMode.Both options interval read graph

    /// <summary>Admits leading and latest trailing changes, using system time and graph equality.</summary>
    let throttle interval read graph =
        throttleWith TimedOptions.defaults interval read graph

/// <summary>A view's pending keys absent from its <c>Keys</c>, written by the view's pass.</summary>
[<AllowNullLiteral>]
type internal HeldOut<'K when 'K: equality>(graph: Graph) =
    let mutable cell: Signal<'K[]> = Unchecked.defaultof<_>
    let mutable pass: ResizeArray<'K> = null

    member private _.EnsureCell() =
        if Platform.isNull (box cell) then
            cell <- Signal<'K[]>(graph, Array.empty)

        cell

    /// <summary>The keys the last pass held out, in upstream order. A tracked read.</summary>
    member this.Keys = this.EnsureCell().Value

    /// <summary>The cell behind <c>Keys</c>.</summary>
    member this.Cell = this.EnsureCell ()

    /// <summary>Whether any computation currently observes <c>Cell</c>.</summary>
    member _.HasObservers =
        not (Platform.isNull (box cell))
        && cell.ObserverCount > 0

    /// <summary>Discards the keys a failed pass staged.</summary>
    member _.Begin() =
        if not (Platform.isNull pass) then
            pass.Clear ()

    /// <summary>Stages <c>key</c> for the pass in progress.</summary>
    member _.Add(key: 'K) =
        if Platform.isNull pass then
            pass <- ResizeArray<'K>()

        pass.Add key

    /// <summary>
    /// Stages the keys <c>upstream</c> holds out of its <c>Keys</c>, after those already staged, then writes the staged
    /// keys when they differ from the last pass.
    /// </summary>
    member this.Publish(upstream: Projection<'K, 'V>) =
        let inherited = upstream.PendingExtra

        if not (Platform.isNull (box inherited)) then
            let extra = inherited ()

            if extra.Length > 0 then
                if Platform.isNull pass then
                    pass <- ResizeArray<'K>(extra.Length)

                pass.AddRange extra

        let count = if Platform.isNull pass then 0 else pass.Count

        if count = 0 then
            if
                not (Platform.isNull (box cell))
                && cell.Peek.Length > 0
            then
                cell.Value <- Array.empty
        else
            let target = this.EnsureCell ()
            let current = target.Peek
            let mutable same = current.Length = count
            let mutable i = 0

            while same && i < count do
                if not (Platform.keyEquals (Platform.itemAt i current) (Platform.entryAt i pass)) then
                    same <- false

                i <- i + 1

            if not same then
                target.Value <- pass.ToArray ()

            pass.Clear ()

/// <summary>Cached pair of upstream row entries for dual-input derived views.</summary>
[<Sealed; AllowNullLiteral>]
type internal DualEntry<'K, 'A, 'B>(first: RowEntry<'K, 'A>, second: RowEntry<'K, 'B>) =
    member val First = first with get, set
    member val Second = second with get, set

module internal EntryCache =
    let inline readSingle (upstream: Projection<'K, 'V>) (kr: KeyRow<'K, 'U>) : 'V =
        let key = kr.Key
        let mutable up = unbox<RowEntry<'K, 'V>> kr.Tag

        if Platform.isNull up || not up.Live then
            up <- upstream.Entries.Find key
            kr.Tag <- box up

        upstream.ReadEntry (key, up)

    let inline readDual (first: Projection<'K, 'A>) (second: Projection<'K, 'B>) (kr: KeyRow<'K, 'U>) : struct ('A * 'B) =
        let key = kr.Key
        let mutable cached = unbox<DualEntry<'K, 'A, 'B>> kr.Tag

        if Platform.isNull cached then
            let e1 = first.Entries.Find key
            let e2 = second.Entries.Find key
            cached <- DualEntry (e1, e2)
            kr.Tag <- box cached
        else
            if
                Platform.isNull cached.First
                || not cached.First.Live
            then
                cached.First <- first.Entries.Find key

            if
                Platform.isNull cached.Second
                || not cached.Second.Live
            then
                cached.Second <- second.Entries.Find key

        struct (first.ReadEntry (key, cached.First), second.ReadEntry (key, cached.Second))

/// <summary>
/// The keys of <c>upstream</c>, in upstream order, with rows from exactly one of <c>map</c> and <c>factory</c>, as on
/// <c>KeyedProjection</c>.
/// </summary>
type internal MapView<'K, 'V, 'U when 'K: equality>
    (graph: Graph, upstream: Projection<'K, 'V>, map: 'K -> 'U, mapValue: 'V -> 'U, factory: 'K -> (unit -> 'U), withHeldOut: bool) as this =
    inherit
        RowsByKey<'K, 'U>(
            graph,
            map,
            factory,
            tagged = true,
            ?mapEntry =
                (if Platform.isNull (box mapValue) then
                     None
                 else
                     Some (fun entry -> mapValue (EntryCache.readSingle upstream entry)))
        )

    let heldOut =
        if
            withHeldOut
            && not (Platform.isNull (box upstream.PendingExtra))
        then
            HeldOut<'K> graph
        else
            null

    let reader = graph.RunOwned (this.Scope, upstream.NewKeyReader)

    do
        this.ReadDelta <-
            if Platform.isNull heldOut then
                reader.Read
            else
                fun () ->
                    heldOut.Begin ()
                    let delta = reader.Read ()
                    heldOut.Publish upstream
                    delta

        this.VisitDelta <- fun key -> this.VisitTagged (key, box (upstream.Entries.Find key))

    /// <summary>The keys <c>upstream</c> held out of its <c>Keys</c> at the last pass. A tracked read.</summary>
    member _.HeldOut = heldOut.Keys

    override this.Enumerate() =
        if not (Platform.isNull heldOut) then
            heldOut.Begin ()

        let mine = this.Runs
        let keys = upstream.Keys
        this.Reserve keys.Length
        let mutable i = 0

        while this.Runs = mine && i < keys.Length do
            let key = Platform.itemAt i keys
            i <- i + 1
            let item = box (upstream.Entries.Find key)
            let entry = this.Entries.Find key

            if
                not (Platform.isNull entry)
                && not (obj.ReferenceEquals ((entry :?> KeyRow<'K, 'U>).Tag, item))
            then
                this.RetireChanged key

            if this.Runs = mine then
                this.VisitTagged (key, item)

        if not (Platform.isNull heldOut) then
            heldOut.Publish upstream

/// <summary>
/// The keys of <c>upstream</c> from position <c>offset ()</c>, at most <c>count ()</c> of them, in upstream order, each with
/// a row reading the upstream value.
/// </summary>
/// <remarks>
/// A null <c>offset</c> starts at position 0, and a null <c>count</c> runs to the last key. A negative offset is 0. An
/// offset past the last key or a negative count gives an empty window, and a count past the last key ends it at the last key.
/// </remarks>
type internal SliceView<'K, 'V when 'K: equality>(graph: Graph, upstream: Projection<'K, 'V>, offset: unit -> int, count: unit -> int) =
    inherit
        RowsByKey<'K, 'V>(
            graph,
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>,
            tagged = true,
            mapEntry = (fun entry -> EntryCache.readSingle upstream entry)
        )

    let heldOut =
        if not (Platform.isNull (box upstream.PendingExtra)) then
            HeldOut<'K> graph
        else
            null

    let mutable synced = false
    let mutable lastUpVersion = -1
    let mutable lastStart = 0
    let mutable lastStop = 0
    let mutable pendingUpVersion = -1
    let mutable pendingStart = 0
    let mutable pendingStop = 0
    let mutable pendingRangeValid = false
    let mutable pendingRangeFrom = -1
    let mutable pendingRangeStart = 0
    let mutable pendingRangePrevEnd = 0
    let mutable pendingRangeEnd = 0

    /// <summary>The keys <c>upstream</c> held out of its <c>Keys</c> at the last pass. A tracked read.</summary>
    member _.HeldOut = heldOut.Keys

    override this.Enumerate() =
        pendingRangeValid <- false

        if not (Platform.isNull heldOut) then
            heldOut.Begin ()

        let offset =
            if Platform.isNull (box offset) then
                0
            else
                max 0 (offset ())

        let count =
            if Platform.isNull (box count) then
                System.Int32.MaxValue
            else
                count ()

        let keys = upstream.Keys
        let upVer = upstream.PublishedVersion
        let start = min offset keys.Length

        let stop =
            if count <= 0 then start
            elif count >= keys.Length - start then keys.Length
            else start + count

        let winLen = stop - start
        pendingUpVersion <- upVer
        pendingStart <- start
        pendingStop <- stop

        if synced then
            let mutable unchanged =
                (winLen = 0 && lastStart = lastStop)
                || (upVer = lastUpVersion
                    && start = lastStart
                    && stop = lastStop)

            if
                not unchanged
                && winLen = lastStop - lastStart
                && lastUpVersion >= 0
            then
                match upstream.ChangedSince lastUpVersion with
                | ValueSome (struct (chgStart, chgPrevEnd, chgEnd)) ->
                    if
                        (start = lastStart && stop <= chgStart)
                        || (start > chgEnd
                            && lastStart > chgPrevEnd
                            && start - chgEnd = lastStart - chgPrevEnd)
                    then
                        unchanged <- true
                | ValueNone -> ()

            if unchanged then
                this.StageRemovals ()
                this.StageKeys null
            else
                let prev = this.PublishedKeys
                let prevLen = prev.Length
                let mutable p = 0

                while p < prevLen
                      && p < winLen
                      && Platform.keyEquals (Platform.itemAt p prev) (Platform.itemAt (start + p) keys) do
                    p <- p + 1

                if p = prevLen && prevLen = winLen then
                    this.StageRemovals ()
                    this.StageKeys null
                else
                    let mutable s = 0

                    while s < prevLen - p
                          && s < winLen - p
                          && Platform.keyEquals (Platform.itemAt (prevLen - 1 - s) prev) (Platform.itemAt (stop - 1 - s) keys) do
                        s <- s + 1

                    let middleStop = winLen - 1 - s

                    for i in p..middleStop do
                        let k = Platform.itemAt (start + i) keys
                        this.Seen.Add k |> ignore
                        this.StageAdd k

                    for i in p .. prevLen - 1 - s do
                        let k = Platform.itemAt i prev

                        if not (this.Seen.Contains k) then
                            this.Removed.Add k

                    this.Seen.Clear ()
                    this.StageRemovals ()

                    let nextKeys =
                        if winLen = 0 then
                            Array.empty
                        elif p = 0 && s = 0 && start = 0 && stop = keys.Length then
                            keys
                        else
                            Array.sub keys start winLen

                    this.StageKeys nextKeys
                    pendingRangeFrom <- this.PublishedVersion
                    pendingRangeStart <- p
                    pendingRangePrevEnd <- prevLen - 1 - s
                    pendingRangeEnd <- winLen - 1 - s
                    pendingRangeValid <- true
        else
            this.Reserve winLen

            for i in start .. stop - 1 do
                this.Visit (Platform.itemAt i keys)

        if not (Platform.isNull heldOut) then
            heldOut.Publish upstream

    override this.Applied() =
        synced <- true
        lastUpVersion <- pendingUpVersion
        lastStart <- pendingStart
        lastStop <- pendingStop

        if
            pendingRangeValid
            && this.PublishedVersion = pendingRangeFrom + 1
        then
            this.RecordKeyRange (pendingRangeFrom, pendingRangeStart, pendingRangePrevEnd, pendingRangeEnd)

        pendingRangeValid <- false

    override _.Abandoned() =
        synced <- false
        lastUpVersion <- -1
        pendingRangeValid <- false

/// <summary>
/// One-based Fenwick prefix-sum table over binary inclusion indicators, used to compute output key positions in
/// <c>O(log N)</c> time without rescanning unchanged prefix elements.
/// </summary>
module internal Fenwick =
    let ensureCapacity (tree: int[]) (oldCount: int) (neededCount: int) : int[] =
        let required = neededCount + 1

        if Platform.isNull tree then
            Array.zeroCreate<int>(max 16 required)
        elif tree.Length < required then
            let grown = Array.zeroCreate<int>(max (max 16 required) (tree.Length * 2))

            if oldCount > 0 then
                Array.blit tree 0 grown 0 (oldCount + 1)

            grown
        else
            tree

    let sumPrefix (tree: int[]) (count: int) =
        let mutable sum = 0
        let mutable i = count

        while i > 0 do
            sum <- sum + Platform.itemAt i tree
            i <- i - (i &&& (0 - i))

        sum

    let add (tree: int[]) (n: int) (idx0: int) (delta: int) =
        let mutable i = idx0 + 1

        while i <= n do
            Platform.setItemAt i tree (Platform.itemAt i tree + delta)
            i <- i + (i &&& (0 - i))

    let append (tree: int[]) (idx0: int) (bit: int) =
        let idx1 = idx0 + 1

        if (idx1 &&& 1) = 1 then
            Platform.setItemAt idx1 tree bit
        else
            let low = idx1 - (idx1 &&& (0 - idx1))
            let range = sumPrefix tree (idx1 - 1) - sumPrefix tree low
            Platform.setItemAt idx1 tree (range + bit)

    let clearRange (tree: int[]) (count: int) =
        for i in 0..count do
            Platform.setItemAt i tree 0

    let propagate (tree: int[]) (count: int) =
        for i in 1..count do
            let v = Platform.itemAt i tree
            let parent = i + (i &&& (0 - i))

            if parent <= count then
                Platform.setItemAt parent tree (Platform.itemAt parent tree + v)

type internal InclusionState =
    | Excluded = 0uy
    | Included = 1uy
    | Failed = 2uy

/// <summary>
/// Shared per-view dirty/check queue and notification gate for incremental slot observers, using explicit fields to
/// avoid getter/setter overhead in Fable JS.
/// </summary>
[<Sealed; AllowNullLiteral>]
type internal SlotQueue<'Slot> =
    val ViewNode: IComputation
    val IsInvalidated: unit -> bool
    val IsCheckQueued: unit -> bool
    val Dirty: ResizeArray<'Slot>
    val Check: ResizeArray<'Slot>

    [<DefaultValue>]
    val mutable ActiveSlot: obj

    [<DefaultValue>]
    val mutable CheckNotified: bool

    [<DefaultValue>]
    val mutable DirtyNotified: bool

    new(viewNode: IComputation, isInvalidated: unit -> bool, isCheckQueued: unit -> bool) =
        {
            ViewNode = viewNode
            IsInvalidated = isInvalidated
            IsCheckQueued = isCheckQueued
            Dirty = ResizeArray<'Slot>()
            Check = ResizeArray<'Slot>()
        }

    static member Create<'K, 'V when 'K: equality>(view: Projection<'K, 'V>) =
        SlotQueue<'Slot>(view :> IComputation, (fun () -> view.IsInvalidated), (fun () -> view.IsCheckQueued))

/// <summary>
/// Observes one key's boolean inclusion memo on behalf of an incremental filter or choose view, forwarding
/// <c>Check</c> and <c>Dirty</c> notifications without retaining every row memo in the view's <c>sources</c> list.
/// </summary>
[<Sealed>]
type internal InclusionSlot<'K> =
    val Key: 'K
    val Queue: SlotQueue<InclusionSlot<'K>>
    val mutable Source: Memo<bool>
    val mutable Handle: int
    val mutable Index: int
    val mutable State: InclusionState
    val mutable Unsettled: bool
    val mutable QueuedDirty: bool
    val mutable QueuedCheck: bool
    val mutable Disposed: bool

    new(key: 'K, source: Memo<bool>, queue: SlotQueue<InclusionSlot<'K>>) =
        {
            Key = key
            Queue = queue
            Source = source
            Handle = EdgeHandle.Unlinked
            Index = 0
            State = InclusionState.Excluded
            Unsettled = false
            QueuedDirty = false
            QueuedCheck = false
            Disposed = false
        }

    member this.Attach() =
        this.Handle <- (this.Source :> ISource).AddObserver(this, 0)

    member this.Detach() =
        if not this.Disposed then
            this.Disposed <- true

            if this.Handle <> EdgeHandle.Unlinked then
                (this.Source :> ISource).RemoveObserverAt(this, this.Handle)
                this.Handle <- EdgeHandle.Unlinked

    interface INode with
        member this.Id = this.Queue.ViewNode.Id
        member this.Status = this.Queue.ViewNode.Status

    interface IComputation with
        member _.AddSource _ = ()
        member _.HasReadSlot _ = false

        member this.MovedIn(_, handle) =
            if not this.Disposed then
                this.Handle <- handle

        member this.MarkDirty() =
            let q = this.Queue

            if
                not this.Disposed
                && not (obj.ReferenceEquals (this, q.ActiveSlot))
            then
                if not this.QueuedDirty then
                    this.QueuedDirty <- true
                    q.Dirty.Add this

                if not q.DirtyNotified then
                    q.ViewNode.MarkDirty ()
                    q.DirtyNotified <- q.IsInvalidated ()

        member this.MarkCheck() =
            let q = this.Queue

            if
                not this.Disposed
                && not (obj.ReferenceEquals (this, q.ActiveSlot))
            then
                if not this.QueuedDirty && not this.QueuedCheck then
                    this.QueuedCheck <- true
                    q.Check.Add this

                if not q.CheckNotified && not q.DirtyNotified then
                    q.ViewNode.MarkCheck ()
                    q.CheckNotified <- q.IsCheckQueued ()

/// <summary>
/// Shared incremental membership engine for <c>FilterView</c> and <c>ChooseView</c>: resolves <c>Check</c> on queued
/// row slots without running a pass, splices small delta batches in <c>O(Δ log N)</c> via a Fenwick prefix-rank index,
/// and switches to a dense linear slot scan for large batches.
/// </summary>
type internal InclusionCore<'K, 'V, 'U, 'T when 'K: equality>
    (
        graph: Graph,
        upstream: Projection<'K, 'V>,
        view: RowsByKey<'K, 'U>,
        heldOut: HeldOut<'K>,
        helper: unit -> Projection<'K, 'T>,
        resolveSource: 'K -> Memo<bool> voption,
        admit: bool
    ) =
    let viewNode = view :> IComputation
    let queue = SlotQueue<InclusionSlot<'K>>.Create view
    let dirtySlots = queue.Dirty
    let checkSlots = queue.Check
    let mutable initialized = false
    let mutable disposed = false
    let mutable synced = false
    let mutable pendingRangeValid = false
    let mutable pendingRangeFrom = -1
    let mutable pendingRangeStart = 0
    let mutable pendingRangePrevEnd = 0
    let mutable pendingRangeEnd = 0

    let mutable keyReader: ProjectionReader<'K> =
        Unchecked.defaultof<ProjectionReader<'K>>

    let mutable slots: InclusionSlot<'K>[] = Array.empty
    let mutable scratchSlots: InclusionSlot<'K>[] = Array.empty
    let mutable count = 0
    let byKey = Platform.KeyMap<'K, InclusionSlot<'K>>()
    let failedKeys = Platform.KeySet<'K>()
    let mutable fenwick: int[] = Array.empty
    let mutable fenwickValid = false
    let mutable includedCount = 0
    let mutable unsettledCount = 0
    let mutable checkIndex = 0
    let flipIndices = Array.zeroCreate<int> 64
    let flipDirs = Array.zeroCreate<int> 64
    let flipKeys = Array.zeroCreate<'K> 64

    let incrementalLimit n =
        max 8 (min 64 (n >>> 4))

    let ensureSlotsCapacity needed =
        if slots.Length < needed then
            let grown =
                Array.zeroCreate<InclusionSlot<'K>>(max (max 16 needed) (slots.Length * 2))

            if count > 0 then
                Array.blit slots 0 grown 0 count

            slots <- grown

    let ensureFenwick (neededCount: int) =
        if not fenwickValid then
            fenwick <- Fenwick.ensureCapacity fenwick 0 neededCount
            Fenwick.clearRange fenwick count

            for i in 0 .. count - 1 do
                if (Platform.itemAt i slots).State = InclusionState.Included then
                    Platform.setItemAt (i + 1) fenwick 1

            Fenwick.propagate fenwick count
            fenwickValid <- true
        elif fenwick.Length < neededCount + 1 then
            fenwick <- Fenwick.ensureCapacity fenwick count neededCount

    let clearQueues () =
        if dirtySlots.Count > 0 then
            for i in 0 .. dirtySlots.Count - 1 do
                (Platform.entryAt i dirtySlots).QueuedDirty <- false

            dirtySlots.Clear ()

        if checkIndex < checkSlots.Count then
            for i in checkIndex .. checkSlots.Count - 1 do
                (Platform.entryAt i checkSlots).QueuedCheck <- false

        if checkSlots.Count > 0 then
            checkSlots.Clear ()

        checkIndex <- 0
        queue.CheckNotified <- false
        queue.DirtyNotified <- false

    let enqueueCheckSlot (slot: InclusionSlot<'K>) =
        if not slot.QueuedDirty && not slot.QueuedCheck then
            slot.QueuedCheck <- true
            checkSlots.Add slot

    let evaluateSlow (slot: InclusionSlot<'K>) (source: Memo<bool>) (st: Status) : InclusionState =
        if
            not slot.Disposed
            && (st &&& Status.Stale) <> Status.None
        then
            enqueueCheckSlot slot

            if not queue.CheckNotified && not queue.DirtyNotified then
                viewNode.MarkCheck ()
                queue.CheckNotified <- queue.IsCheckQueued ()

        if StatusBits.isPending st then
            if not source.Published then
                slot.Unsettled <- true
                InclusionState.Excluded
            else
                slot.Unsettled <- false

                if source.Peek = admit then
                    InclusionState.Included
                else
                    InclusionState.Excluded
        elif StatusBits.isError st then
            slot.Unsettled <- false
            InclusionState.Failed
        else
            slot.Unsettled <- false

            if source.Peek = admit then
                InclusionState.Included
            else
                InclusionState.Excluded

    let recordStateChange (key: 'K) (prevState: InclusionState) (nextState: InclusionState) =
        if prevState <> nextState then
            if prevState = InclusionState.Failed then
                failedKeys.Remove key

            if nextState = InclusionState.Excluded then
                view.Removed.Add key
            else
                if nextState = InclusionState.Failed then
                    failedKeys.Add key |> ignore

                if prevState = InclusionState.Excluded then
                    view.StageAdd key

    let refreshSlot (slot: InclusionSlot<'K>) =
        let prevState = slot.State
        let prevUnsettled = slot.Unsettled
        let source = slot.Source
        queue.ActiveSlot <- box slot
        let st = source.EnsureCurrentUntracked ()

        let nextState =
            if st = Status.None then
                slot.Unsettled <- false

                if source.Peek = admit then
                    InclusionState.Included
                else
                    InclusionState.Excluded
            else
                evaluateSlow slot source st

        let nextUnsettled = slot.Unsettled

        if prevUnsettled <> nextUnsettled then
            unsettledCount <- unsettledCount + (if nextUnsettled then 1 else -1)

        if prevState <> nextState then
            recordStateChange slot.Key prevState nextState
            slot.State <- nextState

        prevState

    let resolveCheckStep (keepRunning: unit -> bool) =
        let stepEnd = checkSlots.Count

        while keepRunning () && checkIndex < stepEnd do
            let slot = Platform.entryAt checkIndex checkSlots
            checkIndex <- checkIndex + 1

            if not slot.Disposed then
                slot.QueuedCheck <- false

                if not slot.QueuedDirty then
                    let source = slot.Source :> ISource
                    source.UpdateIfNecessary ()

                    if
                        not slot.Disposed
                        && not slot.QueuedDirty
                        && source.IsStale
                    then
                        if source.IsActive then
                            (slot :> IComputation).MarkDirty()
                        else
                            enqueueCheckSlot slot

        if checkIndex >= checkSlots.Count then
            if checkSlots.Count > 0 then
                checkSlots.Clear ()

            checkIndex <- 0

        queue.CheckNotified <- false

    let drainCheckSlots (limit: int) =
        let drainEnd = checkSlots.Count

        while checkIndex < drainEnd && dirtySlots.Count <= limit do
            let slot = Platform.entryAt checkIndex checkSlots
            checkIndex <- checkIndex + 1

            if not slot.Disposed then
                slot.QueuedCheck <- false

                if not slot.QueuedDirty then
                    let source = slot.Source :> ISource
                    source.UpdateIfNecessary ()

                    if
                        not slot.Disposed
                        && not slot.QueuedDirty
                        && source.IsStale
                    then
                        slot.QueuedDirty <- true
                        dirtySlots.Add slot

        if checkIndex >= checkSlots.Count then
            if checkSlots.Count > 0 then
                checkSlots.Clear ()

            checkIndex <- 0
            queue.CheckNotified <- false

    let applyDirtySplice (prevKeys: 'K[]) (treeCount: int) =
        if dirtySlots.Count = 0 then
            struct (false, prevKeys)
        else
            ensureFenwick treeCount
            let mutable flipCount = 0
            let mutable deltaIncluded = 0

            try
                for i in 0 .. dirtySlots.Count - 1 do
                    let slot = Platform.entryAt i dirtySlots

                    if not slot.Disposed then
                        slot.QueuedDirty <- false
                        let prevState = refreshSlot slot
                        let nextState = slot.State

                        if
                            (prevState = InclusionState.Included)
                            <> (nextState = InclusionState.Included)
                        then
                            let dir = if nextState = InclusionState.Included then 1 else -1
                            Platform.setItemAt flipCount flipIndices slot.Index
                            Platform.setItemAt flipCount flipDirs dir
                            Platform.setItemAt flipCount flipKeys slot.Key
                            flipCount <- flipCount + 1
                            deltaIncluded <- deltaIncluded + dir
            finally
                queue.ActiveSlot <- null

            dirtySlots.Clear ()
            queue.DirtyNotified <- false

            if flipCount = 0 then
                struct (false, prevKeys)
            else
                for i in 1 .. flipCount - 1 do
                    let idx = Platform.itemAt i flipIndices
                    let dir = Platform.itemAt i flipDirs
                    let key = Platform.itemAt i flipKeys
                    let mutable j = i - 1

                    while j >= 0 && Platform.itemAt j flipIndices > idx do
                        Platform.setItemAt (j + 1) flipIndices (Platform.itemAt j flipIndices)
                        Platform.setItemAt (j + 1) flipDirs (Platform.itemAt j flipDirs)
                        Platform.setItemAt (j + 1) flipKeys (Platform.itemAt j flipKeys)
                        j <- j - 1

                    Platform.setItemAt (j + 1) flipIndices idx
                    Platform.setItemAt (j + 1) flipDirs dir
                    Platform.setItemAt (j + 1) flipKeys key

                let nextLen = includedCount + deltaIncluded
                let mutable firstStart = 0
                let mutable endOldPos = 0
                let mutable endNewPos = 0

                let nextKeys =
                    if nextLen = 0 then
                        let mutable oldPos = 0
                        let mutable newPos = 0

                        for f in 0 .. flipCount - 1 do
                            let uIdx = Platform.itemAt f flipIndices
                            let dir = Platform.itemAt f flipDirs
                            let oldRank = Fenwick.sumPrefix fenwick uIdx

                            if f = 0 then
                                firstStart <- oldRank

                            let segLen = oldRank - oldPos

                            if segLen > 0 then
                                oldPos <- oldRank
                                newPos <- newPos + segLen

                            if dir > 0 then
                                newPos <- newPos + 1
                            else
                                oldPos <- oldPos + 1

                        endOldPos <- oldPos
                        endNewPos <- newPos
                        Array.empty
                    else
                        let arr = Array.zeroCreate<'K> nextLen
                        let mutable oldPos = 0
                        let mutable newPos = 0

                        for f in 0 .. flipCount - 1 do
                            let uIdx = Platform.itemAt f flipIndices
                            let dir = Platform.itemAt f flipDirs
                            let oldRank = Fenwick.sumPrefix fenwick uIdx

                            if f = 0 then
                                firstStart <- oldRank

                            let segLen = oldRank - oldPos

                            if segLen > 0 then
                                Array.blit prevKeys oldPos arr newPos segLen
                                oldPos <- oldRank
                                newPos <- newPos + segLen

                            if dir > 0 then
                                Platform.setItemAt newPos arr (Platform.itemAt f flipKeys)
                                newPos <- newPos + 1
                            else
                                oldPos <- oldPos + 1

                        endOldPos <- oldPos
                        endNewPos <- newPos
                        let tailLen = prevKeys.Length - oldPos

                        if tailLen > 0 then
                            Array.blit prevKeys oldPos arr newPos tailLen

                        arr

                for f in 0 .. flipCount - 1 do
                    Fenwick.add fenwick treeCount (Platform.itemAt f flipIndices) (Platform.itemAt f flipDirs)

                includedCount <- nextLen
                pendingRangeFrom <- view.PublishedVersion
                pendingRangeStart <- firstStart
                pendingRangePrevEnd <- endOldPos - 1
                pendingRangeEnd <- endNewPos - 1
                pendingRangeValid <- true
                struct (true, nextKeys)

    let runDenseBulk () =
        clearQueues ()
        fenwickValid <- false
        let mutable nextIncluded = 0
        let mutable nextUnsettled = 0
        let mutable membershipChanged = false

        try
            for i in 0 .. count - 1 do
                let slot = Platform.itemAt i slots
                let prevState = slot.State
                let source = slot.Source
                queue.ActiveSlot <- box slot
                let st = source.EnsureCurrentUntracked ()

                let nextState =
                    if st = Status.None then
                        slot.Unsettled <- false

                        if source.Peek = admit then
                            InclusionState.Included
                        else
                            InclusionState.Excluded
                    else
                        evaluateSlow slot source st

                slot.State <- nextState

                if prevState <> nextState then
                    recordStateChange slot.Key prevState nextState

                    if
                        (prevState = InclusionState.Included)
                        <> (nextState = InclusionState.Included)
                    then
                        membershipChanged <- true

                if slot.Unsettled then
                    nextUnsettled <- nextUnsettled + 1

                if nextState = InclusionState.Included then
                    view.PassKeys.Add slot.Key
                    nextIncluded <- nextIncluded + 1
        finally
            queue.ActiveSlot <- null

        includedCount <- nextIncluded
        unsettledCount <- nextUnsettled
        view.StageRemovals ()

        if membershipChanged then
            view.StageKeys (
                if nextIncluded = 0 then
                    Array.empty
                else
                    view.PassKeys.ToArray ()
            )
        else
            view.StageKeys null

    let rebuildFull (newKeys: 'K[]) =
        clearQueues ()
        fenwickValid <- false
        failedKeys.Clear ()
        let oldSlots = slots
        let oldCount = count
        let newCount = newKeys.Length

        for i in 0 .. oldCount - 1 do
            (Platform.itemAt i oldSlots).Index <- -1

        let nextSlots =
            if newCount = 0 then
                Array.empty
            elif scratchSlots.Length >= newCount then
                let reused = scratchSlots
                scratchSlots <- Array.empty
                reused
            else
                Array.zeroCreate<InclusionSlot<'K>>(max 16 newCount)

        let mutable outCount = 0
        let mutable nextIncluded = 0
        let mutable nextUnsettled = 0

        try
            for i in 0 .. newCount - 1 do
                let key = Platform.itemAt i newKeys

                match resolveSource key with
                | ValueNone -> ()
                | ValueSome resolved ->
                    let mutable slot = byKey.Find key

                    if
                        not (Platform.isNull (box slot))
                        && not (obj.ReferenceEquals (slot.Source, resolved))
                    then
                        slot.Detach ()
                        slot <- Unchecked.defaultof<_>

                    if Platform.isNull (box slot) then
                        slot <- InclusionSlot<'K>(key, resolved, queue)
                        slot.Attach ()
                        byKey.Set (key, slot)

                    slot.Index <- outCount
                    queue.ActiveSlot <- box slot
                    let st = resolved.EnsureCurrentUntracked ()

                    let nextState =
                        if st = Status.None then
                            slot.Unsettled <- false

                            if resolved.Peek = admit then
                                InclusionState.Included
                            else
                                InclusionState.Excluded
                        else
                            evaluateSlow slot resolved st

                    slot.State <- nextState
                    Platform.setItemAt outCount nextSlots slot

                    if slot.Unsettled then
                        nextUnsettled <- nextUnsettled + 1

                    if nextState = InclusionState.Included then
                        view.PassKeys.Add key
                        nextIncluded <- nextIncluded + 1
                        view.StageAdd key
                    elif nextState = InclusionState.Failed then
                        failedKeys.Add key |> ignore
                        view.StageAdd key

                    outCount <- outCount + 1
        finally
            queue.ActiveSlot <- null

        for i in 0 .. oldCount - 1 do
            let slot = Platform.itemAt i oldSlots

            if slot.Index < 0 && not slot.Disposed then
                if obj.ReferenceEquals (byKey.Find slot.Key, slot) then
                    byKey.Remove slot.Key

                slot.Detach ()

        if oldCount > 0 then
            Array.fill oldSlots 0 oldCount Unchecked.defaultof<InclusionSlot<'K>>

        if
            oldSlots.Length > 0
            && not (obj.ReferenceEquals (oldSlots, nextSlots))
        then
            scratchSlots <- oldSlots

        slots <- nextSlots
        count <- outCount
        includedCount <- nextIncluded
        unsettledCount <- nextUnsettled

        if view.Entries.Count > 0 then
            view.Entries.Iterate (fun key _ ->
                let slot = byKey.Find key

                if
                    Platform.isNull (box slot)
                    || slot.State = InclusionState.Excluded
                then
                    view.Removed.Add key)

        view.StageRemovals ()

    let tryTailAppend (delta: ProjectionDelta<'K>) (limit: int) =
        let newKeys = delta.Keys
        let addedCount = delta.ChangeCount

        if
            addedCount <= 0
            || addedCount > limit
            || newKeys.Length <> count + addedCount
            || (dirtySlots.Count + (checkSlots.Count - checkIndex)) > limit
        then
            false
        else
            let mutable allAdded = true
            let mutable c = 0

            while allAdded && c < addedCount do
                if (delta.ChangeAt c).Value <> KeyChange.Added then
                    allAdded <- false

                c <- c + 1

            if not allAdded then
                false
            else
                let mutable samePrefix = true
                let mutable i = 0

                while samePrefix && i < count do
                    if not (Platform.keyEquals (Platform.itemAt i slots).Key (Platform.itemAt i newKeys)) then
                        samePrefix <- false

                    i <- i + 1

                if not samePrefix then
                    false
                else
                    drainCheckSlots limit

                    if dirtySlots.Count > limit then
                        false
                    else
                        let oldCount = count
                        let prevKeys = view.PublishedKeys
                        let struct (baseChanged, baseKeys) = applyDirtySplice prevKeys oldCount
                        let addedIncluded = ResizeArray<'K>(addedCount)

                        ensureSlotsCapacity (oldCount + addedCount)
                        ensureFenwick (oldCount + addedCount)
                        let mutable nextCount = oldCount

                        try
                            for j in 0 .. addedCount - 1 do
                                let key = Platform.itemAt (oldCount + j) newKeys

                                match resolveSource key with
                                | ValueNone -> ()
                                | ValueSome resolved ->
                                    let slot = InclusionSlot<'K>(key, resolved, queue)
                                    slot.Attach ()
                                    slot.Index <- nextCount
                                    queue.ActiveSlot <- box slot
                                    let st = resolved.EnsureCurrentUntracked ()

                                    let nextState =
                                        if st = Status.None then
                                            slot.Unsettled <- false

                                            if resolved.Peek = admit then
                                                InclusionState.Included
                                            else
                                                InclusionState.Excluded
                                        else
                                            evaluateSlow slot resolved st

                                    slot.State <- nextState
                                    byKey.Set (key, slot)
                                    Platform.setItemAt nextCount slots slot

                                    if slot.Unsettled then
                                        unsettledCount <- unsettledCount + 1

                                    let bit =
                                        if nextState = InclusionState.Included then
                                            view.StageAdd key
                                            addedIncluded.Add key
                                            1
                                        elif nextState = InclusionState.Failed then
                                            view.StageAdd key
                                            failedKeys.Add key |> ignore
                                            0
                                        else
                                            0

                                    Fenwick.append fenwick nextCount bit
                                    nextCount <- nextCount + 1
                        finally
                            queue.ActiveSlot <- null

                        count <- nextCount

                        if not baseChanged && addedIncluded.Count = 0 then
                            view.StageKeys null
                        else
                            let nextKeys = Array.zeroCreate<'K>(baseKeys.Length + addedIncluded.Count)

                            if baseKeys.Length > 0 then
                                Array.blit baseKeys 0 nextKeys 0 baseKeys.Length

                            for k in 0 .. addedIncluded.Count - 1 do
                                Platform.setItemAt (baseKeys.Length + k) nextKeys (Platform.entryAt k addedIncluded)

                            includedCount <- nextKeys.Length
                            view.StageKeys nextKeys

                            if addedIncluded.Count > 0 then
                                if baseChanged then
                                    pendingRangePrevEnd <- prevKeys.Length - 1
                                    pendingRangeEnd <- nextKeys.Length - 1
                                else
                                    pendingRangeFrom <- view.PublishedVersion
                                    pendingRangeStart <- prevKeys.Length
                                    pendingRangePrevEnd <- prevKeys.Length - 1
                                    pendingRangeEnd <- nextKeys.Length - 1
                                    pendingRangeValid <- true

                        view.StageRemovals ()
                        true

    let applyGeneralDelta (delta: ProjectionDelta<'K>) =
        let oldSlots = slots
        let oldCount = count
        let changeCount = delta.ChangeCount
        fenwickValid <- false

        try
            for c in 0 .. changeCount - 1 do
                let change = delta.ChangeAt c
                let key = change.Key

                match change.Value with
                | KeyChange.Removed ->
                    let slot = byKey.Find key

                    if not (Platform.isNull (box slot)) then
                        byKey.Remove key
                        slot.Detach ()

                        if slot.Unsettled then
                            unsettledCount <- unsettledCount - 1

                        if slot.State = InclusionState.Failed then
                            failedKeys.Remove key
                            view.Removed.Add key
                        elif slot.State = InclusionState.Included then
                            view.Removed.Add key
                | KeyChange.Replaced ->
                    let oldSlot = byKey.Find key

                    let prevState, prevUnsettled =
                        if Platform.isNull (box oldSlot) then
                            InclusionState.Excluded, false
                        else
                            let st = oldSlot.State
                            let un = oldSlot.Unsettled
                            oldSlot.Detach ()
                            st, un

                    match resolveSource key with
                    | ValueNone ->
                        if not (Platform.isNull (box oldSlot)) then
                            byKey.Remove key

                            if prevUnsettled then
                                unsettledCount <- unsettledCount - 1

                            if prevState = InclusionState.Failed then
                                failedKeys.Remove key
                                view.Removed.Add key
                            elif prevState = InclusionState.Included then
                                view.Removed.Add key
                    | ValueSome resolved ->
                        let slot = InclusionSlot<'K>(key, resolved, queue)
                        slot.Attach ()
                        queue.ActiveSlot <- box slot
                        let st = resolved.EnsureCurrentUntracked ()

                        let nextState =
                            if st = Status.None then
                                slot.Unsettled <- false

                                if resolved.Peek = admit then
                                    InclusionState.Included
                                else
                                    InclusionState.Excluded
                            else
                                evaluateSlow slot resolved st

                        let nextUnsettled = slot.Unsettled
                        slot.State <- nextState
                        byKey.Set (key, slot)

                        if prevUnsettled <> nextUnsettled then
                            unsettledCount <- unsettledCount + (if nextUnsettled then 1 else -1)

                        recordStateChange key prevState nextState
                | KeyChange.Added ->
                    match resolveSource key with
                    | ValueNone -> ()
                    | ValueSome resolved ->
                        let slot = InclusionSlot<'K>(key, resolved, queue)
                        slot.Attach ()
                        queue.ActiveSlot <- box slot
                        let st = resolved.EnsureCurrentUntracked ()

                        let nextState =
                            if st = Status.None then
                                slot.Unsettled <- false

                                if resolved.Peek = admit then
                                    InclusionState.Included
                                else
                                    InclusionState.Excluded
                            else
                                evaluateSlow slot resolved st

                        slot.State <- nextState
                        byKey.Set (key, slot)

                        if slot.Unsettled then
                            unsettledCount <- unsettledCount + 1

                        recordStateChange key InclusionState.Excluded nextState
                | _ -> ()

            let newKeys = delta.Keys
            let newCount = newKeys.Length

            let nextSlots =
                if newCount = 0 then
                    Array.empty
                elif scratchSlots.Length >= newCount then
                    let reused = scratchSlots
                    scratchSlots <- Array.empty
                    reused
                else
                    Array.zeroCreate<InclusionSlot<'K>>(max 16 newCount)

            let mutable cursor = 0
            let mutable outCount = 0
            let mutable nextIncluded = 0

            for i in 0 .. newCount - 1 do
                let key = Platform.itemAt i newKeys

                while cursor < oldCount
                      && (Platform.itemAt cursor oldSlots).Disposed do
                    cursor <- cursor + 1

                let mutable slot =
                    if
                        cursor < oldCount
                        && Platform.keyEquals (Platform.itemAt cursor oldSlots).Key key
                    then
                        let hit = Platform.itemAt cursor oldSlots
                        cursor <- cursor + 1
                        hit
                    else
                        byKey.Find key

                if not (Platform.isNull (box slot)) then
                    if slot.QueuedDirty || slot.QueuedCheck then
                        slot.QueuedDirty <- false
                        slot.QueuedCheck <- false
                        refreshSlot slot |> ignore

                    slot.Index <- outCount
                    Platform.setItemAt outCount nextSlots slot

                    if slot.State = InclusionState.Included then
                        view.PassKeys.Add key
                        nextIncluded <- nextIncluded + 1

                    outCount <- outCount + 1

            if oldCount > 0 then
                Array.fill oldSlots 0 oldCount Unchecked.defaultof<InclusionSlot<'K>>

            if
                oldSlots.Length > 0
                && not (obj.ReferenceEquals (oldSlots, nextSlots))
            then
                scratchSlots <- oldSlots

            slots <- nextSlots
            count <- outCount
            clearQueues ()
            includedCount <- nextIncluded
            view.StageRemovals ()
        finally
            queue.ActiveSlot <- null

    let finishPass () =
        if failedKeys.Count > 0 then
            failedKeys.Iterate (fun key -> view.StageHidden key)

        if unsettledCount > 0 then
            for i in 0 .. count - 1 do
                let slot = Platform.itemAt i slots

                if slot.Unsettled then
                    heldOut.Add slot.Key

        heldOut.Publish upstream

    let dispose () =
        if not disposed then
            disposed <- true
            synced <- false
            view.ExtraCheck <- Unchecked.defaultof<_>
            view.HasExtraCheck <- Unchecked.defaultof<_>
            view.ExtraFailedCheck <- Unchecked.defaultof<_>

            for i in 0 .. count - 1 do
                (Platform.itemAt i slots).Detach()

            if not (Platform.isNull (box keyReader)) then
                keyReader.Stop ()
                keyReader <- Unchecked.defaultof<ProjectionReader<'K>>

            if dirtySlots.Count > 0 then
                dirtySlots.Clear ()

            if checkSlots.Count > 0 then
                checkSlots.Clear ()

            checkIndex <- 0
            queue.CheckNotified <- false
            queue.DirtyNotified <- false
            queue.ActiveSlot <- null
            byKey.Clear ()
            failedKeys.Clear ()
            slots <- Array.empty
            scratchSlots <- Array.empty
            fenwick <- Array.empty
            fenwickValid <- false
            count <- 0
            includedCount <- 0
            unsettledCount <- 0

    let init () =
        initialized <- true
        let rows = helper ()
        keyReader <- rows.NewInternalKeyReader ()
        view.ExtraCheck <- fun () -> resolveCheckStep (fun () -> view.IsCheck)
        view.HasExtraCheck <- fun () -> checkIndex < checkSlots.Count
        view.ExtraFailedCheck <- fun () -> resolveCheckStep (fun () -> not view.IsInvalidated)
        graph.RunUntracked (fun () -> view.Scope.OnCleanup dispose)

    member _.Dispose() =
        dispose ()

    member _.Enumerate() =
        if not initialized then
            init ()

        pendingRangeValid <- false
        let wasSynced = synced
        synced <- false
        queue.CheckNotified <- false
        queue.DirtyNotified <- false
        heldOut.Begin ()
        let delta = keyReader.Read ()

        if not wasSynced || delta.IsReset then
            rebuildFull delta.Keys
        elif delta.IsEmpty then
            let limit = incrementalLimit count
            let pendingChecks = checkSlots.Count - checkIndex

            if
                dirtySlots.Count > limit
                || dirtySlots.Count + pendingChecks > (limit <<< 2)
            then
                runDenseBulk ()
            else
                drainCheckSlots limit

                if dirtySlots.Count <= limit then
                    let struct (changed, nextKeys) = applyDirtySplice view.PublishedKeys count
                    view.StageRemovals ()
                    view.StageKeys (if changed then nextKeys else null)
                else
                    runDenseBulk ()
        else
            let limit = incrementalLimit count

            if not (tryTailAppend delta limit) then
                applyGeneralDelta delta

        finishPass ()

    member _.Applied() =
        synced <- true

        if
            pendingRangeValid
            && view.PublishedVersion = pendingRangeFrom + 1
        then
            view.RecordKeyRange (pendingRangeFrom, pendingRangeStart, pendingRangePrevEnd, pendingRangeEnd)

        pendingRangeValid <- false

    member _.Abandoned() =
        synced <- false
        pendingRangeValid <- false
        queue.CheckNotified <- false
        queue.DirtyNotified <- false

/// <summary>
/// The keys of <c>inclusion</c> whose predicate row holds <c>admit</c>, each reading the upstream value after forcing
/// the predicate row.
/// </summary>
/// <remarks>
/// A pending predicate keeps the key's last settled membership. A failed predicate leaves the key out of <c>Keys</c> and
/// keeps its row, which raises the failure.
/// </remarks>
type internal FilterView<'K, 'V when 'K: equality>(graph: Graph, upstream: Projection<'K, 'V>, inclusion: Projection<'K, bool> ref, admit: bool) as this
    =
    inherit
        RowsByKey<'K, 'V>(
            graph,
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>,
            tagged = true,
            mapEntry =
                (fun entry ->
                    let struct (_, v) = EntryCache.readDual inclusion.Value upstream entry

                    v)
        )

    let heldOut = HeldOut<'K> graph

    let core =
        let resolve key =
            let entry = inclusion.Value.Entries.Find key

            if Platform.isNull entry then
                ValueNone
            else
                ValueSome entry.Row

        InclusionCore<'K, 'V, 'V, bool>(graph, upstream, this, heldOut, (fun () -> inclusion.Value), resolve, admit)

    /// <summary>
    /// The upstream keys whose predicate has never settled, in upstream order, followed by the keys <c>upstream</c> holds
    /// out of its <c>Keys</c>. A tracked read.
    /// </summary>
    member _.HeldOut = heldOut.Keys

    override _.Enumerate() =
        core.Enumerate ()

    override _.Applied() =
        core.Applied ()

    override _.Abandoned() =
        core.Abandoned ()

/// <summary>
/// A boolean inclusion memo paired with a key's row in <c>choices</c>, disposed when that row is retired.
/// </summary>
[<Sealed; AllowNullLiteral>]
type internal ChoiceGate<'K, 'U when 'K: equality>(key: 'K, source: Memo<'U option>, gate: Memo<bool>, gates: Platform.KeyMap<'K, ChoiceGate<'K, 'U>>)
    =
    let mutable disposed = false

    member _.Source = source
    member _.Gate = gate

    interface System.IDisposable with
        member this.Dispose() =
            if not disposed then
                disposed <- true

                if obj.ReferenceEquals (gates.Find key, this) then
                    gates.Remove key

                gate.Dispose ()

/// <summary>
/// The keys of <c>choices</c> whose row holds <c>Some</c>, in upstream order, each with the value inside the <c>Some</c>.
/// </summary>
/// <remarks>
/// Each key's boolean gate cuts off <c>Some</c>-to-<c>Some</c> edits before the view pass without maintaining a second
/// projection, and retires with its <c>choices</c> row via <c>Memo.Adopt</c>.
/// </remarks>
type internal ChooseView<'K, 'V, 'U when 'K: equality>(graph: Graph, upstream: Projection<'K, 'V>, choices: Projection<'K, 'U option> ref) as this =
    inherit
        RowsByKey<'K, 'U>(
            graph,
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>,
            tagged = true,
            mapEntry =
                (fun entry ->
                    match EntryCache.readSingle choices.Value entry with
                    | Some value -> value
                    | None ->
                        raise (
                            System.Collections.Generic.KeyNotFoundException (
                                "The projection has no key "
                                + string entry.Key
                                + "."
                            )
                        ))
        )

    let heldOut = HeldOut<'K> graph
    let gates = Platform.KeyMap<'K, ChoiceGate<'K, 'U>>()

    let core =
        let resolve key =
            let entry = choices.Value.Entries.Find key

            if Platform.isNull entry then
                ValueNone
            else
                let row = entry.Row
                let mutable companion = gates.Find key

                if
                    Platform.isNull companion
                    || not (obj.ReferenceEquals (companion.Source, row))
                then
                    let gate =
                        graph.RunOwned (
                            this.Scope,
                            fun () -> Memo<bool>.CreateEncoded(graph, (fun _ -> Option.isSome row.Value), ScopeMode.Pure, Unchecked.defaultof<_>)
                        )

                    companion <- new ChoiceGate<'K, 'U> (key, row, gate, gates)
                    gates.Set (key, companion)
                    row.Adopt companion

                ValueSome companion.Gate

        InclusionCore<'K, 'V, 'U, 'U option>(graph, upstream, this, heldOut, (fun () -> choices.Value), resolve, true)

    /// <summary>
    /// The upstream keys whose chooser has never settled, in upstream order, followed by the keys <c>upstream</c> holds
    /// out of its <c>Keys</c>. A tracked read.
    /// </summary>
    member _.HeldOut = heldOut.Keys

    override _.Enumerate() =
        core.Enumerate ()

    override _.Applied() =
        core.Applied ()

    override _.Abandoned() =
        core.Abandoned ()

/// <summary>
/// Shared binary-search maintenance helpers for member lists sorted strictly ascending by upstream index.
/// </summary>
module internal MemberSlots =
    let inline indexOfUpIdx (upIdxOf: 'Slot -> int) (members: ResizeArray<'Slot>) (upIdx: int) =
        let mutable lo = 0
        let mutable hi = members.Count - 1
        let mutable found = -1

        while lo <= hi do
            let mid = lo + ((hi - lo) >>> 1)
            let midIdx = upIdxOf (Platform.entryAt mid members)

            if midIdx = upIdx then
                found <- mid
                lo <- hi + 1
            elif midIdx < upIdx then
                lo <- mid + 1
            else
                hi <- mid - 1

        found

    let inline insertBoundByUpIdx (upIdxOf: 'Slot -> int) (members: ResizeArray<'Slot>) (upIdx: int) =
        let mutable lo = 0
        let mutable hi = members.Count

        while lo < hi do
            let mid = lo + ((hi - lo) >>> 1)

            if upIdxOf (Platform.entryAt mid members) < upIdx then
                lo <- mid + 1
            else
                hi <- mid

        lo

    let inline insertSortedByUpIdx (upIdxOf: 'Slot -> int) (members: ResizeArray<'Slot>) (slot: 'Slot) =
        let pos = insertBoundByUpIdx upIdxOf members (upIdxOf slot)
        members.Insert (pos, slot)
        pos

    let inline removeSortedByUpIdx (upIdxOf: 'Slot -> int) (members: ResizeArray<'Slot>) (upIdx: int) =
        let idx = indexOfUpIdx upIdxOf members upIdx

        if idx >= 0 then
            members.RemoveAt idx

        idx

[<Sealed>]
type internal DistinctBucket<'K, 'G when 'K: equality and 'G: equality> =
    val GroupKey: 'G
    val Members: ResizeArray<DistinctSlot<'K, 'G>>

    new(groupKey: 'G) =
        {
            GroupKey = groupKey
            Members = ResizeArray<DistinctSlot<'K, 'G>>()
        }

/// <summary>
/// Observes one key's distinct-key projection memo on behalf of an incremental <c>DistinctView</c>.
/// </summary>
and [<Sealed>] internal DistinctSlot<'K, 'G when 'K: equality and 'G: equality> =
    val Key: 'K
    val Queue: SlotQueue<DistinctSlot<'K, 'G>>
    val mutable Source: Memo<'G>
    val mutable Handle: int
    val mutable UpIdx: int
    val mutable CommittedState: InclusionState
    val mutable Bucket: DistinctBucket<'K, 'G>
    val mutable Value: 'G
    val mutable Failed: bool
    val mutable Unsettled: bool
    val mutable Touched: bool
    val mutable QueuedDirty: bool
    val mutable QueuedCheck: bool
    val mutable Disposed: bool

    new(key: 'K, source: Memo<'G>, queue: SlotQueue<DistinctSlot<'K, 'G>>) =
        {
            Key = key
            Queue = queue
            Source = source
            Handle = EdgeHandle.Unlinked
            UpIdx = -1
            CommittedState = InclusionState.Excluded
            Bucket = Unchecked.defaultof<DistinctBucket<'K, 'G>>
            Value = Unchecked.defaultof<'G>
            Failed = false
            Unsettled = false
            Touched = false
            QueuedDirty = false
            QueuedCheck = false
            Disposed = false
        }

    member this.Attach() =
        this.Handle <- (this.Source :> ISource).AddObserver(this, 0)

    member this.Detach() =
        if not this.Disposed then
            this.Disposed <- true

            if this.Handle <> EdgeHandle.Unlinked then
                (this.Source :> ISource).RemoveObserverAt(this, this.Handle)
                this.Handle <- EdgeHandle.Unlinked

    interface INode with
        member this.Id = this.Queue.ViewNode.Id
        member this.Status = this.Queue.ViewNode.Status

    interface IComputation with
        member _.AddSource _ = ()
        member _.HasReadSlot _ = false

        member this.MovedIn(_, handle) =
            if not this.Disposed then
                this.Handle <- handle

        member this.MarkDirty() =
            let q = this.Queue

            if
                not this.Disposed
                && not (obj.ReferenceEquals (this, q.ActiveSlot))
            then
                if not this.QueuedDirty then
                    this.QueuedDirty <- true
                    q.Dirty.Add this

                if not q.DirtyNotified then
                    q.ViewNode.MarkDirty ()
                    q.DirtyNotified <- q.IsInvalidated ()

        member this.MarkCheck() =
            let q = this.Queue

            if
                not this.Disposed
                && not (obj.ReferenceEquals (this, q.ActiveSlot))
            then
                if not this.QueuedDirty && not this.QueuedCheck then
                    this.QueuedCheck <- true
                    q.Check.Add this

                if not q.CheckNotified && not q.DirtyNotified then
                    q.ViewNode.MarkCheck ()
                    q.CheckNotified <- q.IsCheckQueued ()

/// <summary>
/// The first key holding each distinct value of <c>values</c>, in upstream order, each with a row reading the upstream
/// value.
/// </summary>
/// <remarks>
/// A pending value keeps the key's last settled value. A failed value leaves the key out of <c>Keys</c> and keeps its
/// row, which raises the failure.
/// </remarks>
type internal DistinctView<'K, 'V, 'G when 'K: equality and 'G: equality>(graph: Graph, upstream: Projection<'K, 'V>, values: Projection<'K, 'G> ref) as this
    =
    inherit
        RowsByKey<'K, 'V>(
            graph,
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>,
            tagged = true,
            mapEntry =
                (fun entry ->
                    let struct (_, v) = EntryCache.readDual values.Value upstream entry

                    v)
        )

    let heldOut = HeldOut<'K> graph
    let queue = SlotQueue<DistinctSlot<'K, 'G>>.Create this
    let dirtySlots = queue.Dirty
    let checkSlots = queue.Check
    let mutable initialized = false
    let mutable disposed = false
    let mutable synced = false
    let mutable pendingRangeValid = false
    let mutable pendingRangeFrom = -1
    let mutable pendingRangeStart = 0
    let mutable pendingRangePrevEnd = -1
    let mutable pendingRangeEnd = -1

    let mutable keyReader: ProjectionReader<'K> =
        Unchecked.defaultof<ProjectionReader<'K>>

    let mutable upSlots: DistinctSlot<'K, 'G>[] = Array.empty
    let mutable scratchUpSlots: DistinctSlot<'K, 'G>[] = Array.empty
    let mutable upCount = 0
    let byKey = Platform.KeyMap<'K, DistinctSlot<'K, 'G>>()
    let buckets = Platform.KeyMap<'G, DistinctBucket<'K, 'G>>()
    let failedKeys = Platform.KeySet<'K>()
    let touchedSlots = ResizeArray<DistinctSlot<'K, 'G>>()
    let pendingBucketInserts = ResizeArray<DistinctSlot<'K, 'G>>()
    let mutable fenwick: int[] = Array.empty
    let mutable fenwickValid = false
    let mutable includedCount = 0
    let mutable unsettledCount = 0
    let mutable checkIndex = 0
    let mutable flipIndices = Array.zeroCreate<int> 64
    let mutable flipDirs = Array.zeroCreate<int> 64
    let mutable flipKeys = Array.zeroCreate<'K> 64

    let incrementalLimit n =
        max 8 (min 64 (n >>> 4))

    let ensureUpCapacity needed =
        if upSlots.Length < needed then
            let grown =
                Array.zeroCreate<DistinctSlot<'K, 'G>>(max (max 16 needed) (upSlots.Length * 2))

            if upCount > 0 then
                Array.blit upSlots 0 grown 0 upCount

            upSlots <- grown

    let ensureFlipCapacity needed =
        if flipIndices.Length < needed then
            let cap = max (needed * 2) (flipIndices.Length * 2)
            let nextIndices = Array.zeroCreate<int> cap
            let nextDirs = Array.zeroCreate<int> cap
            let nextKeys = Array.zeroCreate<'K> cap
            Array.blit flipIndices 0 nextIndices 0 flipIndices.Length
            Array.blit flipDirs 0 nextDirs 0 flipDirs.Length
            Array.blit flipKeys 0 nextKeys 0 flipKeys.Length
            flipIndices <- nextIndices
            flipDirs <- nextDirs
            flipKeys <- nextKeys

    let ensureFenwick neededCount =
        if not fenwickValid then
            fenwick <- Fenwick.ensureCapacity fenwick 0 neededCount
            Fenwick.clearRange fenwick upCount

            for i in 0 .. upCount - 1 do
                if (Platform.itemAt i upSlots).CommittedState = InclusionState.Included then
                    Platform.setItemAt (i + 1) fenwick 1

            Fenwick.propagate fenwick upCount
            fenwickValid <- true
        elif fenwick.Length < neededCount + 1 then
            fenwick <- Fenwick.ensureCapacity fenwick upCount neededCount

    let markTouched (slot: DistinctSlot<'K, 'G>) =
        if not slot.Touched then
            slot.Touched <- true
            touchedSlots.Add slot

    let clearTouched () =
        for i in 0 .. touchedSlots.Count - 1 do
            (Platform.entryAt i touchedSlots).Touched <- false

        if touchedSlots.Count > 0 then
            touchedSlots.Clear ()

    let clearQueues () =
        if dirtySlots.Count > 0 then
            for i in 0 .. dirtySlots.Count - 1 do
                (Platform.entryAt i dirtySlots).QueuedDirty <- false

            dirtySlots.Clear ()

        if checkIndex < checkSlots.Count then
            for i in checkIndex .. checkSlots.Count - 1 do
                (Platform.entryAt i checkSlots).QueuedCheck <- false

        if checkSlots.Count > 0 then
            checkSlots.Clear ()

        checkIndex <- 0
        queue.CheckNotified <- false
        queue.DirtyNotified <- false

    let enqueueCheckSlot (slot: DistinctSlot<'K, 'G>) =
        if not slot.QueuedDirty && not slot.QueuedCheck then
            slot.QueuedCheck <- true
            checkSlots.Add slot

    let evaluateSlot (slot: DistinctSlot<'K, 'G>) (source: Memo<'G>) =
        queue.ActiveSlot <- box slot
        let st = source.EnsureCurrentUntracked ()

        if st = Status.None then
            slot.Unsettled <- false
            slot.Failed <- false
            slot.Value <- source.Peek
        else
            if
                not slot.Disposed
                && (st &&& Status.Stale) <> Status.None
            then
                enqueueCheckSlot slot

                if not queue.CheckNotified && not queue.DirtyNotified then
                    (this :> IComputation).MarkCheck()
                    queue.CheckNotified <- queue.IsCheckQueued ()

            if StatusBits.isPending st then
                if not source.Published then
                    slot.Unsettled <- true
                    slot.Failed <- false
                else
                    slot.Unsettled <- false
                    slot.Failed <- false
                    slot.Value <- source.Peek
            elif StatusBits.isError st then
                slot.Unsettled <- false
                slot.Failed <- true
            else
                slot.Unsettled <- false
                slot.Failed <- false
                slot.Value <- source.Peek

    let detachFromBucket (slot: DistinctSlot<'K, 'G>) (trackTouches: bool) =
        let bucket = slot.Bucket

        if not (Platform.isNull (box bucket)) then
            let members = bucket.Members

            let oldWinner =
                if members.Count > 0 then
                    Platform.entryAt 0 members
                else
                    Unchecked.defaultof<_>

            MemberSlots.removeSortedByUpIdx (fun (s: DistinctSlot<'K, 'G>) -> s.UpIdx) members slot.UpIdx
            |> ignore

            slot.Bucket <- Unchecked.defaultof<_>

            if members.Count = 0 then
                buckets.Remove bucket.GroupKey
            elif
                trackTouches
                && obj.ReferenceEquals (oldWinner, slot)
            then
                markTouched (Platform.entryAt 0 members)

            if trackTouches then
                markTouched slot

    let insertIntoBucket (slot: DistinctSlot<'K, 'G>) (trackTouches: bool) =
        let v = slot.Value
        let mutable bucket = buckets.Find v

        if Platform.isNull (box bucket) then
            bucket <- DistinctBucket<'K, 'G>(v)
            buckets.Set (v, bucket)

        let members = bucket.Members

        let oldWinner =
            if members.Count > 0 then
                Platform.entryAt 0 members
            else
                Unchecked.defaultof<_>

        let pos =
            MemberSlots.insertSortedByUpIdx (fun (s: DistinctSlot<'K, 'G>) -> s.UpIdx) members slot

        slot.Bucket <- bucket

        if trackTouches then
            markTouched slot

            if pos = 0 && not (Platform.isNull (box oldWinner)) then
                markTouched oldWinner

    let appendIntoBucket (slot: DistinctSlot<'K, 'G>) =
        let v = slot.Value
        let mutable bucket = buckets.Find v

        if Platform.isNull (box bucket) then
            bucket <- DistinctBucket<'K, 'G>(v)
            buckets.Set (v, bucket)

        bucket.Members.Add slot
        slot.Bucket <- bucket

    let computeSlotState (slot: DistinctSlot<'K, 'G>) =
        if slot.Failed then
            InclusionState.Failed
        else
            let bucket = slot.Bucket

            if
                not (Platform.isNull (box bucket))
                && bucket.Members.Count > 0
                && obj.ReferenceEquals (Platform.entryAt 0 bucket.Members, slot)
            then
                InclusionState.Included
            else
                InclusionState.Excluded

    let recordStateChange (key: 'K) (prevState: InclusionState) (nextState: InclusionState) =
        if prevState <> nextState then
            if prevState = InclusionState.Failed then
                failedKeys.Remove key

            if nextState = InclusionState.Excluded then
                this.Removed.Add key
            else
                if nextState = InclusionState.Failed then
                    failedKeys.Add key |> ignore

                if prevState = InclusionState.Excluded then
                    this.StageAdd key

    let refreshSlot (slot: DistinctSlot<'K, 'G>) (insertNow: bool) =
        let prevFailed = slot.Failed
        let prevUnsettled = slot.Unsettled
        let wasSettled = not prevFailed && not prevUnsettled
        let prevVal = slot.Value
        evaluateSlot slot slot.Source
        let nowSettled = not slot.Failed && not slot.Unsettled

        if prevUnsettled <> slot.Unsettled then
            unsettledCount <-
                unsettledCount
                + (if slot.Unsettled then 1 else -1)

        if wasSettled && nowSettled then
            if not (Platform.keyEquals prevVal slot.Value) then
                detachFromBucket slot true

                if insertNow then
                    insertIntoBucket slot true
                else
                    pendingBucketInserts.Add slot
        else
            if wasSettled then
                detachFromBucket slot true

            if nowSettled then
                if insertNow then
                    insertIntoBucket slot true
                else
                    pendingBucketInserts.Add slot
            elif slot.Failed <> prevFailed then
                markTouched slot

    let resolveCheckStep (keepRunning: unit -> bool) =
        let stepEnd = checkSlots.Count

        while keepRunning () && checkIndex < stepEnd do
            let slot = Platform.entryAt checkIndex checkSlots
            checkIndex <- checkIndex + 1

            if not slot.Disposed then
                slot.QueuedCheck <- false

                if not slot.QueuedDirty then
                    let source = slot.Source :> ISource
                    source.UpdateIfNecessary ()

                    if
                        not slot.Disposed
                        && not slot.QueuedDirty
                        && source.IsStale
                    then
                        if source.IsActive then
                            (slot :> IComputation).MarkDirty()
                        else
                            enqueueCheckSlot slot

        if checkIndex >= checkSlots.Count then
            if checkSlots.Count > 0 then
                checkSlots.Clear ()

            checkIndex <- 0

        queue.CheckNotified <- false

    let drainCheckSlots (limit: int) =
        let drainEnd = checkSlots.Count

        while checkIndex < drainEnd && dirtySlots.Count <= limit do
            let slot = Platform.entryAt checkIndex checkSlots
            checkIndex <- checkIndex + 1

            if not slot.Disposed then
                slot.QueuedCheck <- false

                if not slot.QueuedDirty then
                    let source = slot.Source :> ISource
                    source.UpdateIfNecessary ()

                    if
                        not slot.Disposed
                        && not slot.QueuedDirty
                        && source.IsStale
                    then
                        slot.QueuedDirty <- true
                        dirtySlots.Add slot

        if checkIndex >= checkSlots.Count then
            if checkSlots.Count > 0 then
                checkSlots.Clear ()

            checkIndex <- 0
            queue.CheckNotified <- false

    let applyDirtySplice (prevKeys: 'K[]) (treeCount: int) =
        if dirtySlots.Count = 0 then
            struct (false, prevKeys)
        else
            clearTouched ()

            try
                let mutable i = 0

                while i < dirtySlots.Count do
                    let slot = Platform.entryAt i dirtySlots
                    i <- i + 1

                    if not slot.Disposed then
                        slot.QueuedDirty <- false
                        refreshSlot slot true
            finally
                queue.ActiveSlot <- null

            dirtySlots.Clear ()
            queue.DirtyNotified <- false

            if touchedSlots.Count = 0 then
                struct (false, prevKeys)
            else
                let mutable flipCount = 0
                let mutable deltaIncluded = 0

                for i in 0 .. touchedSlots.Count - 1 do
                    let slot = Platform.entryAt i touchedSlots
                    slot.Touched <- false

                    if not slot.Disposed then
                        let prevState = slot.CommittedState
                        let nextState = computeSlotState slot

                        if prevState <> nextState then
                            if
                                (prevState = InclusionState.Included)
                                <> (nextState = InclusionState.Included)
                            then
                                if flipCount = 0 then
                                    ensureFenwick treeCount

                                ensureFlipCapacity (flipCount + 1)
                                let dir = if nextState = InclusionState.Included then 1 else -1
                                Platform.setItemAt flipCount flipIndices slot.UpIdx
                                Platform.setItemAt flipCount flipDirs dir
                                Platform.setItemAt flipCount flipKeys slot.Key
                                flipCount <- flipCount + 1
                                deltaIncluded <- deltaIncluded + dir

                            recordStateChange slot.Key prevState nextState
                            slot.CommittedState <- nextState

                touchedSlots.Clear ()

                if flipCount = 0 then
                    struct (false, prevKeys)
                else

                    for i in 1 .. flipCount - 1 do
                        let idx = Platform.itemAt i flipIndices
                        let dir = Platform.itemAt i flipDirs
                        let key = Platform.itemAt i flipKeys
                        let mutable j = i - 1

                        while j >= 0 && Platform.itemAt j flipIndices > idx do
                            Platform.setItemAt (j + 1) flipIndices (Platform.itemAt j flipIndices)
                            Platform.setItemAt (j + 1) flipDirs (Platform.itemAt j flipDirs)
                            Platform.setItemAt (j + 1) flipKeys (Platform.itemAt j flipKeys)
                            j <- j - 1

                        Platform.setItemAt (j + 1) flipIndices idx
                        Platform.setItemAt (j + 1) flipDirs dir
                        Platform.setItemAt (j + 1) flipKeys key

                    let nextLen = includedCount + deltaIncluded
                    let mutable firstStart = 0
                    let mutable endOldPos = 0
                    let mutable endNewPos = 0

                    let nextKeys =
                        if nextLen = 0 then
                            let mutable oldPos = 0
                            let mutable newPos = 0

                            for f in 0 .. flipCount - 1 do
                                let uIdx = Platform.itemAt f flipIndices
                                let dir = Platform.itemAt f flipDirs
                                let oldRank = Fenwick.sumPrefix fenwick uIdx

                                if f = 0 then
                                    firstStart <- oldRank

                                let segLen = oldRank - oldPos

                                if segLen > 0 then
                                    oldPos <- oldRank
                                    newPos <- newPos + segLen

                                if dir > 0 then
                                    newPos <- newPos + 1
                                else
                                    oldPos <- oldPos + 1

                            endOldPos <- oldPos
                            endNewPos <- newPos
                            Array.empty
                        else
                            let arr = Array.zeroCreate<'K> nextLen
                            let mutable oldPos = 0
                            let mutable newPos = 0

                            for f in 0 .. flipCount - 1 do
                                let uIdx = Platform.itemAt f flipIndices
                                let dir = Platform.itemAt f flipDirs
                                let oldRank = Fenwick.sumPrefix fenwick uIdx

                                if f = 0 then
                                    firstStart <- oldRank

                                let segLen = oldRank - oldPos

                                if segLen > 0 then
                                    Array.blit prevKeys oldPos arr newPos segLen
                                    oldPos <- oldRank
                                    newPos <- newPos + segLen

                                if dir > 0 then
                                    Platform.setItemAt newPos arr (Platform.itemAt f flipKeys)
                                    newPos <- newPos + 1
                                else
                                    oldPos <- oldPos + 1

                            endOldPos <- oldPos
                            endNewPos <- newPos
                            let tailLen = prevKeys.Length - oldPos

                            if tailLen > 0 then
                                Array.blit prevKeys oldPos arr newPos tailLen

                            arr

                    for f in 0 .. flipCount - 1 do
                        Fenwick.add fenwick treeCount (Platform.itemAt f flipIndices) (Platform.itemAt f flipDirs)

                    includedCount <- nextLen
                    pendingRangeFrom <- this.PublishedVersion
                    pendingRangeStart <- firstStart
                    pendingRangePrevEnd <- endOldPos - 1
                    pendingRangeEnd <- endNewPos - 1
                    pendingRangeValid <- true
                    struct (true, nextKeys)

    let publishPassKeysIfChanged () =
        let prevKeys = this.PublishedKeys
        let passCount = this.PassKeys.Count

        let same =
            prevKeys.Length = passCount
            && (let mutable eq = true
                let mutable i = 0

                while eq && i < passCount do
                    if not (Platform.keyEquals (Platform.itemAt i prevKeys) (Platform.entryAt i this.PassKeys)) then
                        eq <- false

                    i <- i + 1

                eq)

        if same then
            this.StageKeys null
        else
            this.StageKeys (
                if passCount = 0 then
                    Array.empty
                else
                    this.PassKeys.ToArray ()
            )

    let runDenseDistinct () =
        clearQueues ()
        clearTouched ()
        fenwickValid <- false
        let mutable nextUnsettled = 0
        let mutable membershipChanged = false

        try
            for i in 0 .. upCount - 1 do
                let slot = Platform.itemAt i upSlots
                let wasSettled = not slot.Failed && not slot.Unsettled
                let prevVal = slot.Value
                evaluateSlot slot slot.Source
                let nowSettled = not slot.Failed && not slot.Unsettled

                if slot.Unsettled then
                    nextUnsettled <- nextUnsettled + 1

                if wasSettled && nowSettled then
                    if not (Platform.keyEquals prevVal slot.Value) then
                        detachFromBucket slot false
                        insertIntoBucket slot false
                else
                    if wasSettled then
                        detachFromBucket slot false

                    if nowSettled then
                        insertIntoBucket slot false

            let mutable nextIncluded = 0

            for i in 0 .. upCount - 1 do
                let slot = Platform.itemAt i upSlots
                let prevState = slot.CommittedState
                let nextState = computeSlotState slot

                if prevState <> nextState then
                    recordStateChange slot.Key prevState nextState
                    slot.CommittedState <- nextState

                    if
                        (prevState = InclusionState.Included)
                        <> (nextState = InclusionState.Included)
                    then
                        membershipChanged <- true

                if nextState = InclusionState.Included then
                    this.PassKeys.Add slot.Key
                    nextIncluded <- nextIncluded + 1

            includedCount <- nextIncluded
            unsettledCount <- nextUnsettled
            this.StageRemovals ()

            if membershipChanged then
                this.StageKeys (
                    if nextIncluded = 0 then
                        Array.empty
                    else
                        this.PassKeys.ToArray ()
                )
            else
                this.StageKeys null
        finally
            queue.ActiveSlot <- null

    let rebuildFull (newKeys: 'K[]) =
        clearQueues ()
        clearTouched ()
        pendingBucketInserts.Clear ()
        buckets.Clear ()
        failedKeys.Clear ()
        let oldUpSlots = upSlots
        let oldUpCount = upCount
        let newCount = newKeys.Length

        for i in 0 .. oldUpCount - 1 do
            let slot = Platform.itemAt i oldUpSlots
            slot.UpIdx <- -1
            slot.Bucket <- Unchecked.defaultof<_>

        let nextUpSlots =
            if newCount = 0 then
                Array.empty
            elif scratchUpSlots.Length >= newCount then
                let reused = scratchUpSlots
                scratchUpSlots <- Array.empty
                reused
            else
                Array.zeroCreate<DistinctSlot<'K, 'G>>(max 16 newCount)

        let mutable outCount = 0
        let mutable nextIncluded = 0
        let mutable nextUnsettled = 0
        let rows = values.Value

        try
            for i in 0 .. newCount - 1 do
                let key = Platform.itemAt i newKeys
                let entry = rows.Entries.Find key

                if not (Platform.isNull entry) then
                    let resolved = entry.Row
                    let mutable slot = byKey.Find key

                    if
                        not (Platform.isNull (box slot))
                        && not (obj.ReferenceEquals (slot.Source, resolved))
                    then
                        slot.Detach ()
                        slot <- Unchecked.defaultof<_>

                    if Platform.isNull (box slot) then
                        slot <- DistinctSlot<'K, 'G>(key, resolved, queue)
                        slot.Attach ()
                        byKey.Set (key, slot)

                    slot.UpIdx <- outCount
                    evaluateSlot slot resolved

                    if slot.Unsettled then
                        nextUnsettled <- nextUnsettled + 1

                    if not slot.Failed && not slot.Unsettled then
                        appendIntoBucket slot

                    let nextState = computeSlotState slot
                    slot.CommittedState <- nextState

                    if nextState = InclusionState.Included then
                        this.PassKeys.Add key
                        nextIncluded <- nextIncluded + 1
                        this.StageAdd key
                    elif nextState = InclusionState.Failed then
                        failedKeys.Add key |> ignore
                        this.StageAdd key

                    Platform.setItemAt outCount nextUpSlots slot
                    outCount <- outCount + 1
        finally
            queue.ActiveSlot <- null

        for i in 0 .. oldUpCount - 1 do
            let slot = Platform.itemAt i oldUpSlots

            if slot.UpIdx < 0 && not slot.Disposed then
                if obj.ReferenceEquals (byKey.Find slot.Key, slot) then
                    byKey.Remove slot.Key

                slot.Detach ()

        if oldUpCount > 0 then
            Array.fill oldUpSlots 0 oldUpCount Unchecked.defaultof<DistinctSlot<'K, 'G>>

        if
            oldUpSlots.Length > 0
            && not (obj.ReferenceEquals (oldUpSlots, nextUpSlots))
        then
            scratchUpSlots <- oldUpSlots

        upSlots <- nextUpSlots
        upCount <- outCount
        includedCount <- nextIncluded
        unsettledCount <- nextUnsettled
        fenwickValid <- false

        if this.Entries.Count > 0 then
            this.Entries.Iterate (fun key _ ->
                let slot = byKey.Find key

                if
                    Platform.isNull (box slot)
                    || slot.CommittedState = InclusionState.Excluded
                then
                    this.Removed.Add key)

        this.StageRemovals ()

    let tryTailAppend (delta: ProjectionDelta<'K>) (limit: int) =
        let newKeys = delta.Keys
        let addedCount = delta.ChangeCount

        if
            addedCount <= 0
            || addedCount > limit
            || newKeys.Length <> upCount + addedCount
            || (dirtySlots.Count + (checkSlots.Count - checkIndex)) > limit
        then
            false
        else
            let mutable allAdded = true
            let mutable c = 0

            while allAdded && c < addedCount do
                if (delta.ChangeAt c).Value <> KeyChange.Added then
                    allAdded <- false

                c <- c + 1

            if not allAdded then
                false
            else
                let mutable samePrefix = true
                let mutable i = 0

                while samePrefix && i < upCount do
                    if not (Platform.keyEquals (Platform.itemAt i upSlots).Key (Platform.itemAt i newKeys)) then
                        samePrefix <- false

                    i <- i + 1

                if not samePrefix then
                    false
                else
                    drainCheckSlots limit

                    if dirtySlots.Count > limit then
                        false
                    else
                        let oldCount = upCount
                        let prevKeys = this.PublishedKeys
                        let struct (baseChanged, baseKeys) = applyDirtySplice prevKeys oldCount
                        let addedIncluded = ResizeArray<'K>(addedCount)
                        let rows = values.Value

                        ensureUpCapacity (oldCount + addedCount)
                        ensureFenwick (oldCount + addedCount)
                        let mutable nextCount = oldCount

                        try
                            for j in 0 .. addedCount - 1 do
                                let key = Platform.itemAt (oldCount + j) newKeys
                                let entry = rows.Entries.Find key

                                if not (Platform.isNull entry) then
                                    let resolved = entry.Row
                                    let slot = DistinctSlot<'K, 'G>(key, resolved, queue)
                                    slot.Attach ()
                                    slot.UpIdx <- nextCount
                                    evaluateSlot slot resolved
                                    byKey.Set (key, slot)
                                    Platform.setItemAt nextCount upSlots slot

                                    if slot.Unsettled then
                                        unsettledCount <- unsettledCount + 1

                                    if not slot.Failed && not slot.Unsettled then
                                        appendIntoBucket slot

                                    let nextState = computeSlotState slot
                                    slot.CommittedState <- nextState

                                    let bit =
                                        if nextState = InclusionState.Included then
                                            this.StageAdd key
                                            addedIncluded.Add key
                                            1
                                        elif nextState = InclusionState.Failed then
                                            this.StageAdd key
                                            failedKeys.Add key |> ignore
                                            0
                                        else
                                            0

                                    Fenwick.append fenwick nextCount bit
                                    nextCount <- nextCount + 1
                        finally
                            queue.ActiveSlot <- null

                        upCount <- nextCount
                        this.StageRemovals ()

                        if not baseChanged && addedIncluded.Count = 0 then
                            this.StageKeys null
                        else
                            let nextKeys = Array.zeroCreate<'K>(baseKeys.Length + addedIncluded.Count)

                            if baseKeys.Length > 0 then
                                Array.blit baseKeys 0 nextKeys 0 baseKeys.Length

                            for k in 0 .. addedIncluded.Count - 1 do
                                Platform.setItemAt (baseKeys.Length + k) nextKeys (Platform.entryAt k addedIncluded)

                            includedCount <- nextKeys.Length
                            this.StageKeys nextKeys

                            if addedIncluded.Count > 0 then
                                if baseChanged then
                                    pendingRangePrevEnd <- prevKeys.Length - 1
                                    pendingRangeEnd <- nextKeys.Length - 1
                                else
                                    pendingRangeFrom <- this.PublishedVersion
                                    pendingRangeStart <- prevKeys.Length
                                    pendingRangePrevEnd <- prevKeys.Length - 1
                                    pendingRangeEnd <- nextKeys.Length - 1
                                    pendingRangeValid <- true

                        true

    let applyGeneralDelta (delta: ProjectionDelta<'K>) =
        let oldUpSlots = upSlots
        let oldUpCount = upCount
        let changeCount = delta.ChangeCount
        let rows = values.Value
        fenwickValid <- false
        pendingBucketInserts.Clear ()

        try
            for c in 0 .. changeCount - 1 do
                let change = delta.ChangeAt c
                let key = change.Key

                match change.Value with
                | KeyChange.Removed ->
                    let slot = byKey.Find key

                    if not (Platform.isNull (box slot)) then
                        byKey.Remove key
                        detachFromBucket slot false
                        slot.Detach ()

                        if slot.Unsettled then
                            unsettledCount <- unsettledCount - 1

                        recordStateChange key slot.CommittedState InclusionState.Excluded
                | KeyChange.Replaced ->
                    let oldSlot = byKey.Find key

                    let prevCommitted, prevUnsettled =
                        if Platform.isNull (box oldSlot) then
                            InclusionState.Excluded, false
                        else
                            let st = oldSlot.CommittedState
                            let un = oldSlot.Unsettled
                            detachFromBucket oldSlot false
                            oldSlot.Detach ()
                            st, un

                    let entry = rows.Entries.Find key

                    if Platform.isNull entry then
                        if not (Platform.isNull (box oldSlot)) then
                            byKey.Remove key

                            if prevUnsettled then
                                unsettledCount <- unsettledCount - 1

                            recordStateChange key prevCommitted InclusionState.Excluded
                    else
                        let resolved = entry.Row
                        let slot = DistinctSlot<'K, 'G>(key, resolved, queue)
                        slot.Attach ()
                        slot.CommittedState <- prevCommitted
                        evaluateSlot slot resolved
                        byKey.Set (key, slot)

                        if prevUnsettled <> slot.Unsettled then
                            unsettledCount <-
                                unsettledCount
                                + (if slot.Unsettled then 1 else -1)

                        if not slot.Failed && not slot.Unsettled then
                            pendingBucketInserts.Add slot
                | KeyChange.Added ->
                    let entry = rows.Entries.Find key

                    if not (Platform.isNull entry) then
                        let resolved = entry.Row
                        let slot = DistinctSlot<'K, 'G>(key, resolved, queue)
                        slot.Attach ()
                        evaluateSlot slot resolved
                        byKey.Set (key, slot)

                        if slot.Unsettled then
                            unsettledCount <- unsettledCount + 1

                        if not slot.Failed && not slot.Unsettled then
                            pendingBucketInserts.Add slot
                | _ -> ()

            queue.ActiveSlot <- null

            while checkIndex < checkSlots.Count do
                let slot = Platform.entryAt checkIndex checkSlots
                checkIndex <- checkIndex + 1

                if not slot.Disposed then
                    slot.QueuedCheck <- false

                    if not slot.QueuedDirty then
                        let source = slot.Source :> ISource
                        source.UpdateIfNecessary ()

                        if
                            not slot.Disposed
                            && not slot.QueuedDirty
                            && source.IsStale
                        then
                            slot.QueuedDirty <- true
                            dirtySlots.Add slot

            let mutable i = 0

            while i < dirtySlots.Count do
                let slot = Platform.entryAt i dirtySlots
                i <- i + 1

                if not slot.Disposed then
                    slot.QueuedDirty <- false
                    refreshSlot slot false
        finally
            queue.ActiveSlot <- null

        clearQueues ()
        clearTouched ()

        let newKeys = delta.Keys
        let newCount = newKeys.Length

        let nextUpSlots =
            if newCount = 0 then
                Array.empty
            elif scratchUpSlots.Length >= newCount then
                let reused = scratchUpSlots
                scratchUpSlots <- Array.empty
                reused
            else
                Array.zeroCreate<DistinctSlot<'K, 'G>>(max 16 newCount)

        let mutable cursor = 0
        let mutable outCount = 0
        let mutable reordered = false

        for i in 0 .. newCount - 1 do
            let key = Platform.itemAt i newKeys

            while cursor < oldUpCount
                  && (Platform.itemAt cursor oldUpSlots).Disposed do
                cursor <- cursor + 1

            let slot =
                if
                    cursor < oldUpCount
                    && Platform.keyEquals (Platform.itemAt cursor oldUpSlots).Key key
                then
                    let hit = Platform.itemAt cursor oldUpSlots
                    cursor <- cursor + 1
                    hit
                else
                    let found = byKey.Find key

                    if
                        not (Platform.isNull (box found))
                        && found.UpIdx >= 0
                    then
                        reordered <- true

                    found

            if not (Platform.isNull (box slot)) then
                slot.UpIdx <- outCount
                Platform.setItemAt outCount nextUpSlots slot
                outCount <- outCount + 1

        if oldUpCount > 0 then
            Array.fill oldUpSlots 0 oldUpCount Unchecked.defaultof<DistinctSlot<'K, 'G>>

        if
            oldUpSlots.Length > 0
            && not (obj.ReferenceEquals (oldUpSlots, nextUpSlots))
        then
            scratchUpSlots <- oldUpSlots

        upSlots <- nextUpSlots
        upCount <- outCount

        if reordered then
            buckets.Clear ()

            for i in 0 .. outCount - 1 do
                let slot = Platform.itemAt i upSlots
                slot.Bucket <- Unchecked.defaultof<_>

                if not slot.Failed && not slot.Unsettled then
                    appendIntoBucket slot
        else
            for i in 0 .. pendingBucketInserts.Count - 1 do
                let slot = Platform.entryAt i pendingBucketInserts

                if
                    not slot.Disposed
                    && slot.UpIdx >= 0
                    && not slot.Failed
                    && not slot.Unsettled
                    && Platform.isNull (box slot.Bucket)
                then
                    insertIntoBucket slot false

        pendingBucketInserts.Clear ()

        let mutable nextIncluded = 0

        for i in 0 .. outCount - 1 do
            let slot = Platform.itemAt i upSlots
            let prevState = slot.CommittedState
            let nextState = computeSlotState slot

            if prevState <> nextState then
                recordStateChange slot.Key prevState nextState
                slot.CommittedState <- nextState

            if nextState = InclusionState.Included then
                this.PassKeys.Add slot.Key
                nextIncluded <- nextIncluded + 1

        includedCount <- nextIncluded
        this.StageRemovals ()
        publishPassKeysIfChanged ()

    let finishPass () =
        if failedKeys.Count > 0 then
            failedKeys.Iterate (fun key -> this.StageHidden key)

        if unsettledCount > 0 then
            for i in 0 .. upCount - 1 do
                let slot = Platform.itemAt i upSlots

                if slot.Unsettled then
                    heldOut.Add slot.Key

        heldOut.Publish upstream

    let dispose () =
        if not disposed then
            disposed <- true
            synced <- false
            pendingRangeValid <- false
            this.ExtraCheck <- Unchecked.defaultof<_>
            this.HasExtraCheck <- Unchecked.defaultof<_>
            this.ExtraFailedCheck <- Unchecked.defaultof<_>

            for i in 0 .. upCount - 1 do
                (Platform.itemAt i upSlots).Detach()

            if not (Platform.isNull (box keyReader)) then
                keyReader.Stop ()
                keyReader <- Unchecked.defaultof<ProjectionReader<'K>>

            clearQueues ()
            clearTouched ()
            pendingBucketInserts.Clear ()
            queue.ActiveSlot <- null
            byKey.Clear ()
            buckets.Clear ()
            failedKeys.Clear ()
            upSlots <- Array.empty
            scratchUpSlots <- Array.empty
            fenwick <- Array.empty
            fenwickValid <- false
            upCount <- 0
            includedCount <- 0
            unsettledCount <- 0

    let init () =
        initialized <- true
        let rows = values.Value
        keyReader <- rows.NewInternalKeyReader ()
        this.ExtraCheck <- fun () -> resolveCheckStep (fun () -> this.IsCheck)
        this.HasExtraCheck <- fun () -> checkIndex < checkSlots.Count
        this.ExtraFailedCheck <- fun () -> resolveCheckStep (fun () -> not this.IsInvalidated)
        graph.RunUntracked (fun () -> this.Scope.OnCleanup dispose)

    /// <summary>
    /// The upstream keys whose value has never settled, in upstream order, followed by the keys <c>upstream</c> holds out
    /// of its <c>Keys</c>. A tracked read.
    /// </summary>
    member _.HeldOut = heldOut.Keys

    override _.Enumerate() =
        if not initialized then
            init ()

        pendingRangeValid <- false
        let wasSynced = synced
        synced <- false
        queue.CheckNotified <- false
        queue.DirtyNotified <- false
        heldOut.Begin ()
        let delta = keyReader.Read ()

        if not wasSynced || delta.IsReset then
            rebuildFull delta.Keys
        elif delta.IsEmpty then
            let limit = incrementalLimit upCount
            let pendingChecks = checkSlots.Count - checkIndex

            if
                dirtySlots.Count > limit
                || dirtySlots.Count + pendingChecks > (limit <<< 2)
            then
                runDenseDistinct ()
            else
                drainCheckSlots limit

                if dirtySlots.Count <= limit then
                    let struct (changed, nextKeys) = applyDirtySplice this.PublishedKeys upCount
                    this.StageRemovals ()
                    this.StageKeys (if changed then nextKeys else null)
                else
                    runDenseDistinct ()
        else
            let limit = incrementalLimit upCount

            if not (tryTailAppend delta limit) then
                applyGeneralDelta delta

        finishPass ()

    override _.Applied() =
        synced <- true

        if
            pendingRangeValid
            && this.PublishedVersion = pendingRangeFrom + 1
        then
            this.RecordKeyRange (pendingRangeFrom, pendingRangeStart, pendingRangePrevEnd, pendingRangeEnd)

        pendingRangeValid <- false

    override _.Abandoned() =
        synced <- false
        pendingRangeValid <- false
        queue.CheckNotified <- false
        queue.DirtyNotified <- false

/// <summary>
/// The rank order of extremes: <c>compare</c>, with a <c>float</c> or <c>float32</c> NaN equal to NaN and after every
/// other rank.
/// </summary>
module internal Ranks =
#if FABLE_COMPILER
    let isNaN (rank: 'S) =
        match box rank with
        | :? float as f -> System.Double.IsNaN f
        | :? float32 as f -> System.Single.IsNaN f
        | _ -> false

    /// <summary>The order of <c>sortBy</c>.</summary>
    let ascending (a: 'S) (b: 'S) =
        let fast = Platform.compareNumberRanks a b false

        if fast <> 2 then
            fast
        else
            match isNaN a, isNaN b with
            | true, true -> 0
            | true, false -> 1
            | false, true -> -1
            | false, false -> compare a b
#else
    let isNaN (rank: 'S) =
        match box rank with
        | :? float as f -> System.Double.IsNaN f
        | :? float32 as f -> System.Single.IsNaN f
        | _ -> false

    /// <summary>The order of <c>sortBy</c>.</summary>
    let ascending (a: 'S) (b: 'S) =
        match isNaN a, isNaN b with
        | true, true -> 0
        | true, false -> 1
        | false, true -> -1
        | false, false -> LanguagePrimitives.FastGenericComparer<'S>.Compare(a, b)
#endif

/// <summary>
/// Observes one key's sort-key memo on behalf of an incremental <c>SortView</c>, forwarding <c>Check</c> and
/// <c>Dirty</c> notifications without retaining every row memo in the view's <c>sources</c> list.
/// </summary>
[<Sealed>]
type internal SortSlot<'K, 'S> =
    val Key: 'K
    val Source: Memo<'S>
    val Queue: SlotQueue<SortSlot<'K, 'S>>
    val mutable Handle: int
    val mutable UpIdx: int
    val mutable PlacedIdx: int
    val mutable Rank: 'S
    val mutable State: InclusionState
    val mutable Unsettled: bool
    val mutable QueuedDirty: bool
    val mutable QueuedCheck: bool
    val mutable Disposed: bool

    new(key: 'K, source: Memo<'S>, queue: SlotQueue<SortSlot<'K, 'S>>) =
        {
            Key = key
            Source = source
            Queue = queue
            Handle = EdgeHandle.Unlinked
            UpIdx = -1
            PlacedIdx = -1
            Rank = Unchecked.defaultof<'S>
            State = InclusionState.Excluded
            Unsettled = false
            QueuedDirty = false
            QueuedCheck = false
            Disposed = false
        }

    member this.Attach() =
        this.Handle <- (this.Source :> ISource).AddObserver(this, 0)

    member this.Detach() =
        if not this.Disposed then
            this.Disposed <- true

            if this.Handle <> EdgeHandle.Unlinked then
                (this.Source :> ISource).RemoveObserverAt(this, this.Handle)
                this.Handle <- EdgeHandle.Unlinked

    interface INode with
        member this.Id = this.Queue.ViewNode.Id
        member this.Status = this.Queue.ViewNode.Status

    interface IComputation with
        member _.AddSource _ = ()
        member _.HasReadSlot _ = false

        member this.MovedIn(_, handle) =
            if not this.Disposed then
                this.Handle <- handle

        member this.MarkDirty() =
            let q = this.Queue

            if
                not this.Disposed
                && not (obj.ReferenceEquals (this, q.ActiveSlot))
            then
                if not this.QueuedDirty then
                    this.QueuedDirty <- true
                    q.Dirty.Add this

                if not q.DirtyNotified then
                    q.ViewNode.MarkDirty ()
                    q.DirtyNotified <- q.IsInvalidated ()

        member this.MarkCheck() =
            let q = this.Queue

            if
                not this.Disposed
                && not (obj.ReferenceEquals (this, q.ActiveSlot))
            then
                if not this.QueuedDirty && not this.QueuedCheck then
                    this.QueuedCheck <- true
                    q.Check.Add this

                if not q.CheckNotified && not q.DirtyNotified then
                    q.ViewNode.MarkCheck ()
                    q.CheckNotified <- q.IsCheckQueued ()

/// <summary>
/// The keys of <c>sortKeys</c> whose sort key has settled, ascending by sort key, or descending when <c>descending</c> is
/// set, and then by upstream position, each with a row reading the upstream value.
/// </summary>
/// <remarks>
/// A pending sort key keeps the key's last settled sort key. A failed sort key leaves the key out of <c>Keys</c> and keeps
/// its row, which raises the failure. A <c>float</c> or <c>float32</c> NaN sort key orders after every other sort key in
/// both directions.
/// </remarks>
type internal SortView<'K, 'V, 'S when 'K: equality and 'S: comparison>
    (graph: Graph, upstream: Projection<'K, 'V>, sortKeys: Projection<'K, 'S> ref, descending: bool) as this =
    inherit
        RowsByKey<'K, 'V>(
            graph,
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>,
            tagged = true,
            mapEntry =
                (fun entry ->
                    let struct (_, v) = EntryCache.readDual sortKeys.Value upstream entry

                    v)
        )

    let heldOut = HeldOut<'K> graph
    let queue = SlotQueue<SortSlot<'K, 'S>>.Create this
    let dirtySlots = queue.Dirty
    let checkSlots = queue.Check
    let mutable initialized = false
    let mutable disposed = false
    let mutable synced = false
    let mutable rangeOps = 0
    let mutable pendingRangeValid = false
    let mutable pendingRangeFrom = -1
    let mutable pendingRangeStart = 0
    let mutable pendingRangePrevEnd = -1
    let mutable pendingRangeEnd = -1

    let recordRangeOp (opStart: int) (opPrevEnd: int) (opEnd: int) =
        if rangeOps = 0 then
            rangeOps <- 1
            pendingRangeFrom <- this.PublishedVersion
            pendingRangeStart <- opStart
            pendingRangePrevEnd <- opPrevEnd
            pendingRangeEnd <- opEnd
        else
            rangeOps <- 2

    let mutable keyReader: ProjectionReader<'K> =
        Unchecked.defaultof<ProjectionReader<'K>>

    let mutable upSlots: SortSlot<'K, 'S>[] = Array.empty
    let mutable scratchUpSlots: SortSlot<'K, 'S>[] = Array.empty
    let mutable upCount = 0
    let mutable placedSlots: SortSlot<'K, 'S>[] = Array.empty
    let mutable placedCount = 0
    let byKey = Platform.KeyMap<'K, SortSlot<'K, 'S>>()
    let failedKeys = Platform.KeySet<'K>()
    let removedPlaced = ResizeArray<SortSlot<'K, 'S>>()
    let addedPlaced = ResizeArray<SortSlot<'K, 'S>>()
    let mutable unsettledCount = 0
    let mutable checkIndex = 0

    /// <summary>Positions into <c>placedSlots</c>, in output order.</summary>
    let mutable order: int[] = Platform.positions 0

    /// <summary>Inverse permutation of <c>order</c>: for placed index <c>p</c>, <c>order[orderPos[p]] = p</c>.</summary>
    let mutable orderPos: int[] = Platform.positions 0

    let mutable orderPosValid = false

    /// <summary>Whether <c>order[0 .. placedCount - 1]</c> holds the result of a completed sort.</summary>
    let mutable sorted = false

#if FABLE_COMPILER
    let isNaN (rank: 'S) =
        match box rank with
        | :? float as f -> System.Double.IsNaN f
        | :? float32 as f -> System.Single.IsNaN f
        | _ -> false

    /// <summary>The order of <c>sortByDescending</c> when <c>descending</c> is set, of <c>sortBy</c> otherwise.</summary>
    let compareRanks (a: 'S) (b: 'S) =
        let fast = Platform.compareNumberRanks a b descending

        if fast <> 2 then
            fast
        else
            match isNaN a, isNaN b with
            | true, true -> 0
            | true, false -> 1
            | false, true -> -1
            | false, false -> if descending then compare b a else compare a b
#else
    // The order of compare, unboxed for value types.
    let comparer = LanguagePrimitives.FastGenericComparer<'S>

    // True when a rank can hold a float or float32. isNaN boxes its rank only when true, at every JIT tier.
    let floatRanks =
        typeof<'S>.IsAssignableFrom typeof<float>
        || typeof<'S>.IsAssignableFrom typeof<float32>

    let isNaN (rank: 'S) =
        match box rank with
        | :? float as f -> System.Double.IsNaN f
        | :? float32 as f -> System.Single.IsNaN f
        | _ -> false

    /// <summary>The order of <c>sortByDescending</c> when <c>descending</c> is set, of <c>sortBy</c> otherwise.</summary>
    let compareRanks (a: 'S) (b: 'S) =
        if floatRanks then
            match isNaN a, isNaN b with
            | true, true -> 0
            | true, false -> 1
            | false, true -> -1
            | false, false ->
                if descending then
                    comparer.Compare (b, a)
                else
                    comparer.Compare (a, b)
        elif descending then
            comparer.Compare (b, a)
        else
            comparer.Compare (a, b)
#endif

    let incrementalLimit n =
        max 8 (min 64 (n >>> 4))

    let ensureUpCapacity needed =
        if upSlots.Length < needed then
            let grown =
                Array.zeroCreate<SortSlot<'K, 'S>>(max (max 16 needed) (upSlots.Length * 2))

            if upCount > 0 then
                Array.blit upSlots 0 grown 0 upCount

            upSlots <- grown

    let ensurePlacedCapacity needed =
        if placedSlots.Length < needed then
            let grown =
                Array.zeroCreate<SortSlot<'K, 'S>>(max (max 16 needed) (placedSlots.Length * 2))

            if placedCount > 0 then
                Array.blit placedSlots 0 grown 0 placedCount

            placedSlots <- grown

    let ensureOrderCapacity needed =
        if order.Length < needed then
            let grown = Platform.positions (max (max 16 needed) (order.Length * 2))

            if placedCount > 0 then
                Platform.copyPositions order grown placedCount

            order <- grown

        if orderPosValid && orderPos.Length < needed then
            let grownPos = Platform.positions (max (max 16 needed) (orderPos.Length * 2))

            if placedCount > 0 then
                Platform.copyPositions orderPos grownPos placedCount

            orderPos <- grownPos

    let ensureOrderPos () =
        if not orderPosValid then
            if orderPos.Length < placedSlots.Length then
                orderPos <- Platform.positions (max 16 placedSlots.Length)

            for pos in 0 .. placedCount - 1 do
                Platform.setItemAt (Platform.itemAt pos order) orderPos pos

            orderPosValid <- true

#if FABLE_COMPILER
    let slotOrderCmp =
        System.Func<int, int, int>(fun i j ->
            let a = Platform.itemAt i placedSlots
            let b = Platform.itemAt j placedSlots
            let byRank = compareRanks a.Rank b.Rank

            if byRank <> 0 then byRank
            elif a.UpIdx < b.UpIdx then -1
            elif a.UpIdx > b.UpIdx then 1
            else 0)

    let fullSort () =
        sorted <- false
        orderPosValid <- false
        rangeOps <- 2
        order <- Platform.resizePositions order placedCount

        for i in 0 .. placedCount - 1 do
            Platform.setItemAt i order i

        if placedCount > 1 then
            Platform.sortPositions order slotOrderCmp

        sorted <- true
#else
    let slotOrderComparison =
        System.Comparison<int>(fun i j ->
            let a = Platform.itemAt i placedSlots
            let b = Platform.itemAt j placedSlots
            let byRank = compareRanks a.Rank b.Rank

            if byRank <> 0 then byRank
            elif a.UpIdx < b.UpIdx then -1
            elif a.UpIdx > b.UpIdx then 1
            else 0)

#if NET5_0_OR_GREATER
    let fullSort () =
        sorted <- false
        orderPosValid <- false
        rangeOps <- 2

        if order.Length < placedCount then
            order <- Platform.positions (max 16 placedCount)

        for i in 0 .. placedCount - 1 do
            Platform.setItemAt i order i

        if placedCount > 1 then
            System.MemoryExtensions.Sort<int>(System.Span<int>(order, 0, placedCount), slotOrderComparison)

        sorted <- true
#else
    let fullSort () =
        sorted <- false
        orderPosValid <- false
        rangeOps <- 2

        if order.Length <> placedCount then
            order <- Array.zeroCreate<int> placedCount

        for i in 0 .. placedCount - 1 do
            Platform.setItemAt i order i

        if placedCount > 1 then
            System.Array.Sort<int>(order, slotOrderComparison)

        sorted <- true
#endif
#endif

    let buildKeysArray () =
        if placedCount = 0 then
            Array.empty
        else
            let arr = Array.zeroCreate<'K> placedCount

            for k in 0 .. placedCount - 1 do
                let pIdx = Platform.itemAt k order
                Platform.setItemAt k arr (Platform.itemAt pIdx placedSlots).Key

            arr

    let stageChangedKeys () =
        let prev = this.PublishedKeys

        if prev.Length <> placedCount then
            this.StageKeys (buildKeysArray ())
        else
            let mutable same = true
            let mutable k = 0

            while same && k < placedCount do
                let pIdx = Platform.itemAt k order

                if not (Platform.keyEquals (Platform.itemAt k prev) (Platform.itemAt pIdx placedSlots).Key) then
                    same <- false

                k <- k + 1

            this.StageKeys (if same then null else buildKeysArray ())

    let insertPlacedSlot (slot: SortSlot<'K, 'S>) =
        let pIdx = placedCount
        ensurePlacedCapacity (pIdx + 1)
        ensureOrderCapacity (pIdx + 1)
        slot.PlacedIdx <- pIdx
        Platform.setItemAt pIdx placedSlots slot
        let newRank = slot.Rank
        let newUpIdx = slot.UpIdx
        let mutable lo = 0
        let mutable hi = pIdx

        while lo < hi do
            let mid = (lo + hi) >>> 1
            let other = Platform.itemAt (Platform.itemAt mid order) placedSlots
            let byRank = compareRanks other.Rank newRank

            let c =
                if byRank <> 0 then byRank
                elif other.UpIdx < newUpIdx then -1
                elif other.UpIdx > newUpIdx then 1
                else 0

            if c < 0 then lo <- mid + 1 else hi <- mid

        let insPos = lo
        let mutable k = pIdx

        while k > insPos do
            let moved = Platform.itemAt (k - 1) order
            Platform.setItemAt k order moved
            Platform.setItemAt moved orderPos k
            k <- k - 1

        Platform.setItemAt insPos order pIdx
        Platform.setItemAt pIdx orderPos insPos
        placedCount <- pIdx + 1
        recordRangeOp insPos (insPos - 1) insPos

    let removePlacedSlot (slot: SortSlot<'K, 'S>) =
        let pIdx = slot.PlacedIdx

        if pIdx >= 0 then
            let oldPos = Platform.itemAt pIdx orderPos
            let lastIdx = placedCount - 1
            let mutable k = oldPos

            while k < lastIdx do
                let moved = Platform.itemAt (k + 1) order
                Platform.setItemAt k order moved
                Platform.setItemAt moved orderPos k
                k <- k + 1

            if pIdx < lastIdx then
                let tailSlot = Platform.itemAt lastIdx placedSlots
                let tailOutPos = Platform.itemAt lastIdx orderPos
                Platform.setItemAt pIdx placedSlots tailSlot
                tailSlot.PlacedIdx <- pIdx
                Platform.setItemAt tailOutPos order pIdx
                Platform.setItemAt pIdx orderPos tailOutPos

            Platform.setItemAt lastIdx placedSlots Unchecked.defaultof<_>
            slot.PlacedIdx <- -1
            placedCount <- lastIdx
            recordRangeOp oldPos oldPos (oldPos - 1)

    let reinsertPlacedSlot (slot: SortSlot<'K, 'S>) =
        if placedCount <= 1 then
            false
        else
            let pIdx = slot.PlacedIdx
            let oldPos = Platform.itemAt pIdx orderPos
            let newRank = slot.Rank
            let myUpIdx = slot.UpIdx
            let mutable lo = 0
            let mutable hi = placedCount - 1

            while lo < hi do
                let mid = (lo + hi) >>> 1
                let otherIdx = Platform.itemAt (if mid < oldPos then mid else mid + 1) order
                let other = Platform.itemAt otherIdx placedSlots
                let byRank = compareRanks other.Rank newRank

                let c =
                    if byRank <> 0 then byRank
                    elif other.UpIdx < myUpIdx then -1
                    elif other.UpIdx > myUpIdx then 1
                    else 0

                if c < 0 then lo <- mid + 1 else hi <- mid

            let newPos = lo

            if newPos < oldPos then
                let mutable k = oldPos

                while k > newPos do
                    let moved = Platform.itemAt (k - 1) order
                    Platform.setItemAt k order moved
                    Platform.setItemAt moved orderPos k
                    k <- k - 1

                Platform.setItemAt newPos order pIdx
                Platform.setItemAt pIdx orderPos newPos
                recordRangeOp newPos oldPos oldPos
                true
            elif newPos > oldPos then
                let mutable k = oldPos

                while k < newPos do
                    let moved = Platform.itemAt (k + 1) order
                    Platform.setItemAt k order moved
                    Platform.setItemAt moved orderPos k
                    k <- k + 1

                Platform.setItemAt newPos order pIdx
                Platform.setItemAt pIdx orderPos newPos
                recordRangeOp oldPos newPos newPos
                true
            else
                false

    let clearQueues () =
        if dirtySlots.Count > 0 then
            for i in 0 .. dirtySlots.Count - 1 do
                (Platform.entryAt i dirtySlots).QueuedDirty <- false

            dirtySlots.Clear ()

        if checkIndex < checkSlots.Count then
            for i in checkIndex .. checkSlots.Count - 1 do
                (Platform.entryAt i checkSlots).QueuedCheck <- false

        if checkSlots.Count > 0 then
            checkSlots.Clear ()

        checkIndex <- 0
        queue.CheckNotified <- false
        queue.DirtyNotified <- false

    let enqueueCheckSlot (slot: SortSlot<'K, 'S>) =
        if not slot.QueuedDirty && not slot.QueuedCheck then
            slot.QueuedCheck <- true
            checkSlots.Add slot

    let evaluateSlow (slot: SortSlot<'K, 'S>) (source: Memo<'S>) (st: Status) : InclusionState =
        if
            not slot.Disposed
            && (st &&& Status.Stale) <> Status.None
        then
            enqueueCheckSlot slot

            if not queue.CheckNotified && not queue.DirtyNotified then
                (this :> IComputation).MarkCheck()
                queue.CheckNotified <- queue.IsCheckQueued ()

        if StatusBits.isPending st then
            if not source.Published then
                slot.Unsettled <- true
                InclusionState.Excluded
            else
                slot.Unsettled <- false
                slot.Rank <- source.Peek
                InclusionState.Included
        elif StatusBits.isError st then
            slot.Unsettled <- false
            InclusionState.Failed
        else
            slot.Unsettled <- false
            slot.Rank <- source.Peek
            InclusionState.Included

    let recordStateChange (key: 'K) (prevState: InclusionState) (nextState: InclusionState) =
        if prevState <> nextState then
            if prevState = InclusionState.Failed then
                failedKeys.Remove key

            if nextState = InclusionState.Excluded then
                this.Removed.Add key
            else
                if nextState = InclusionState.Failed then
                    failedKeys.Add key |> ignore

                if prevState = InclusionState.Excluded then
                    this.StageAdd key

    let refreshSlot (slot: SortSlot<'K, 'S>) =
        let prevState = slot.State
        let prevUnsettled = slot.Unsettled
        let source = slot.Source
        queue.ActiveSlot <- box slot
        let st = source.EnsureCurrentUntracked ()

        let nextState =
            if st = Status.None then
                slot.Unsettled <- false
                slot.Rank <- source.Peek
                InclusionState.Included
            else
                evaluateSlow slot source st

        let nextUnsettled = slot.Unsettled

        if prevUnsettled <> nextUnsettled then
            unsettledCount <- unsettledCount + (if nextUnsettled then 1 else -1)

        if prevState <> nextState then
            recordStateChange slot.Key prevState nextState
            slot.State <- nextState

        prevState

    let spliceSlot (slot: SortSlot<'K, 'S>) (prevState: InclusionState) (prevRank: 'S) =
        let nextState = slot.State

        if prevState <> nextState then
            if prevState = InclusionState.Included then
                removePlacedSlot slot
                true
            elif nextState = InclusionState.Included then
                insertPlacedSlot slot
                true
            else
                false
        elif
            nextState = InclusionState.Included
            && compareRanks prevRank slot.Rank <> 0
        then
            reinsertPlacedSlot slot
        else
            false

    let resolveCheckStep (keepRunning: unit -> bool) =
        let stepEnd = checkSlots.Count

        while keepRunning () && checkIndex < stepEnd do
            let slot = Platform.entryAt checkIndex checkSlots
            checkIndex <- checkIndex + 1

            if not slot.Disposed then
                slot.QueuedCheck <- false

                if not slot.QueuedDirty then
                    let source = slot.Source :> ISource
                    source.UpdateIfNecessary ()

                    if
                        not slot.Disposed
                        && not slot.QueuedDirty
                        && source.IsStale
                    then
                        if source.IsActive then
                            (slot :> IComputation).MarkDirty()
                        else
                            enqueueCheckSlot slot

        if checkIndex >= checkSlots.Count then
            if checkSlots.Count > 0 then
                checkSlots.Clear ()

            checkIndex <- 0

        queue.CheckNotified <- false

    let drainCheckSlots (limit: int) =
        let drainEnd = checkSlots.Count

        while checkIndex < drainEnd && dirtySlots.Count <= limit do
            let slot = Platform.entryAt checkIndex checkSlots
            checkIndex <- checkIndex + 1

            if not slot.Disposed then
                slot.QueuedCheck <- false

                if not slot.QueuedDirty then
                    let source = slot.Source :> ISource
                    source.UpdateIfNecessary ()

                    if
                        not slot.Disposed
                        && not slot.QueuedDirty
                        && source.IsStale
                    then
                        slot.QueuedDirty <- true
                        dirtySlots.Add slot

        if checkIndex >= checkSlots.Count then
            if checkSlots.Count > 0 then
                checkSlots.Clear ()

            checkIndex <- 0
            queue.CheckNotified <- false

    let applyDirtySplice () =
        if dirtySlots.Count = 0 then
            false
        else
            let mutable changed = false

            try
                for i in 0 .. dirtySlots.Count - 1 do
                    let slot = Platform.entryAt i dirtySlots

                    if not slot.Disposed then
                        slot.QueuedDirty <- false
                        let prevRank = slot.Rank
                        let prevState = refreshSlot slot

                        if
                            prevState <> slot.State
                            && (prevState = InclusionState.Included
                                || slot.State = InclusionState.Included)
                        then
                            ensureOrderPos ()

                            if spliceSlot slot prevState prevRank then
                                changed <- true
                        elif
                            slot.State = InclusionState.Included
                            && compareRanks prevRank slot.Rank <> 0
                        then
                            ensureOrderPos ()

                            if reinsertPlacedSlot slot then
                                changed <- true
            finally
                queue.ActiveSlot <- null

            dirtySlots.Clear ()
            queue.DirtyNotified <- false
            changed

    let runDenseSort () =
        clearQueues ()
        let mutable nextUnsettled = 0
        let mutable membershipChanged = false
        let mutable rankChangedCount = 0
        let mutable singleChanged = Unchecked.defaultof<SortSlot<'K, 'S>>

        try
            for i in 0 .. upCount - 1 do
                let slot = Platform.itemAt i upSlots
                let prevState = slot.State
                let prevRank = slot.Rank
                let source = slot.Source
                queue.ActiveSlot <- box slot
                let st = source.EnsureCurrentUntracked ()

                let nextState =
                    if st = Status.None then
                        slot.Unsettled <- false
                        slot.Rank <- source.Peek
                        InclusionState.Included
                    else
                        evaluateSlow slot source st

                slot.State <- nextState

                if prevState <> nextState then
                    recordStateChange slot.Key prevState nextState

                    if
                        (prevState = InclusionState.Included)
                        <> (nextState = InclusionState.Included)
                    then
                        membershipChanged <- true
                elif
                    nextState = InclusionState.Included
                    && compareRanks prevRank slot.Rank <> 0
                then
                    rankChangedCount <- rankChangedCount + 1
                    singleChanged <- slot

                if slot.Unsettled then
                    nextUnsettled <- nextUnsettled + 1
        finally
            queue.ActiveSlot <- null

        unsettledCount <- nextUnsettled
        this.StageRemovals ()

        if not membershipChanged && rankChangedCount = 0 then
            this.StageKeys null
        elif
            not membershipChanged
            && rankChangedCount = 1
            && sorted
        then
            ensureOrderPos ()

            if reinsertPlacedSlot singleChanged then
                this.StageKeys (buildKeysArray ())
            else
                this.StageKeys null
        else
            let prevPlaced = placedCount
            ensurePlacedCapacity upCount
            let mutable nextPlaced = 0

            for i in 0 .. upCount - 1 do
                let slot = Platform.itemAt i upSlots

                if slot.State = InclusionState.Included then
                    slot.PlacedIdx <- nextPlaced
                    Platform.setItemAt nextPlaced placedSlots slot
                    nextPlaced <- nextPlaced + 1
                else
                    slot.PlacedIdx <- -1

            for k in nextPlaced .. prevPlaced - 1 do
                Platform.setItemAt k placedSlots Unchecked.defaultof<_>

            placedCount <- nextPlaced
            fullSort ()
            stageChangedKeys ()

    let rebuildFull (newKeys: 'K[]) =
        clearQueues ()
        failedKeys.Clear ()
        let oldUpSlots = upSlots
        let oldUpCount = upCount
        let prevPlaced = placedCount
        let newCount = newKeys.Length

        for i in 0 .. oldUpCount - 1 do
            (Platform.itemAt i oldUpSlots).UpIdx <- -1

        let nextUpSlots =
            if newCount = 0 then
                Array.empty
            elif scratchUpSlots.Length >= newCount then
                let reused = scratchUpSlots
                scratchUpSlots <- Array.empty
                reused
            else
                Array.zeroCreate<SortSlot<'K, 'S>>(max 16 newCount)

        ensurePlacedCapacity newCount
        let mutable outCount = 0
        let mutable nextPlaced = 0
        let mutable nextUnsettled = 0
        let rows = sortKeys.Value

        try
            for i in 0 .. newCount - 1 do
                let key = Platform.itemAt i newKeys
                let entry = rows.Entries.Find key

                if not (Platform.isNull entry) then
                    let resolved = entry.Row
                    let mutable slot = byKey.Find key

                    if
                        not (Platform.isNull (box slot))
                        && not (obj.ReferenceEquals (slot.Source, resolved))
                    then
                        slot.Detach ()
                        slot <- Unchecked.defaultof<_>

                    if Platform.isNull (box slot) then
                        slot <- SortSlot<'K, 'S>(key, resolved, queue)
                        slot.Attach ()
                        byKey.Set (key, slot)

                    slot.UpIdx <- outCount
                    queue.ActiveSlot <- box slot
                    let st = resolved.EnsureCurrentUntracked ()

                    let nextState =
                        if st = Status.None then
                            slot.Unsettled <- false
                            slot.Rank <- resolved.Peek
                            InclusionState.Included
                        else
                            evaluateSlow slot resolved st

                    slot.State <- nextState
                    Platform.setItemAt outCount nextUpSlots slot

                    if slot.Unsettled then
                        nextUnsettled <- nextUnsettled + 1

                    if nextState = InclusionState.Included then
                        slot.PlacedIdx <- nextPlaced
                        Platform.setItemAt nextPlaced placedSlots slot
                        nextPlaced <- nextPlaced + 1
                        this.StageAdd key
                    else
                        slot.PlacedIdx <- -1

                        if nextState = InclusionState.Failed then
                            failedKeys.Add key |> ignore
                            this.StageAdd key

                    outCount <- outCount + 1
        finally
            queue.ActiveSlot <- null

        for i in 0 .. oldUpCount - 1 do
            let slot = Platform.itemAt i oldUpSlots

            if slot.UpIdx < 0 && not slot.Disposed then
                if obj.ReferenceEquals (byKey.Find slot.Key, slot) then
                    byKey.Remove slot.Key

                slot.Detach ()

        if oldUpCount > 0 then
            Array.fill oldUpSlots 0 oldUpCount Unchecked.defaultof<SortSlot<'K, 'S>>

        if
            oldUpSlots.Length > 0
            && not (obj.ReferenceEquals (oldUpSlots, nextUpSlots))
        then
            scratchUpSlots <- oldUpSlots

        for k in nextPlaced .. prevPlaced - 1 do
            Platform.setItemAt k placedSlots Unchecked.defaultof<_>

        upSlots <- nextUpSlots
        upCount <- outCount
        placedCount <- nextPlaced
        unsettledCount <- nextUnsettled

        if this.Entries.Count > 0 then
            this.Entries.Iterate (fun key _ ->
                let slot = byKey.Find key

                if
                    Platform.isNull (box slot)
                    || slot.State = InclusionState.Excluded
                then
                    this.Removed.Add key)

        this.StageRemovals ()
        fullSort ()
        stageChangedKeys ()

    let tryTailAppend (delta: ProjectionDelta<'K>) (limit: int) =
        let newKeys = delta.Keys
        let addedCount = delta.ChangeCount

        if
            not sorted
            || addedCount <= 0
            || addedCount > limit
            || newKeys.Length <> upCount + addedCount
            || (dirtySlots.Count + (checkSlots.Count - checkIndex)) > limit
        then
            false
        else
            let mutable allAdded = true
            let mutable c = 0

            while allAdded && c < addedCount do
                if (delta.ChangeAt c).Value <> KeyChange.Added then
                    allAdded <- false

                c <- c + 1

            if not allAdded then
                false
            else
                let mutable samePrefix = true
                let mutable i = 0

                while samePrefix && i < upCount do
                    if not (Platform.keyEquals (Platform.itemAt i upSlots).Key (Platform.itemAt i newKeys)) then
                        samePrefix <- false

                    i <- i + 1

                if not samePrefix then
                    false
                else
                    drainCheckSlots limit

                    if dirtySlots.Count > limit then
                        false
                    else
                        let oldCount = upCount
                        let mutable changed = applyDirtySplice ()
                        ensureUpCapacity (oldCount + addedCount)
                        let mutable nextCount = oldCount
                        let rows = sortKeys.Value

                        try
                            for j in 0 .. addedCount - 1 do
                                let key = Platform.itemAt (oldCount + j) newKeys
                                let entry = rows.Entries.Find key

                                if not (Platform.isNull entry) then
                                    let resolved = entry.Row
                                    let slot = SortSlot<'K, 'S>(key, resolved, queue)
                                    slot.Attach ()
                                    slot.UpIdx <- nextCount
                                    queue.ActiveSlot <- box slot
                                    let st = resolved.EnsureCurrentUntracked ()

                                    let nextState =
                                        if st = Status.None then
                                            slot.Unsettled <- false
                                            slot.Rank <- resolved.Peek
                                            InclusionState.Included
                                        else
                                            evaluateSlow slot resolved st

                                    slot.State <- nextState
                                    byKey.Set (key, slot)
                                    Platform.setItemAt nextCount upSlots slot

                                    if slot.Unsettled then
                                        unsettledCount <- unsettledCount + 1

                                    if nextState = InclusionState.Included then
                                        ensureOrderPos ()
                                        insertPlacedSlot slot
                                        changed <- true
                                        this.StageAdd key
                                    elif nextState = InclusionState.Failed then
                                        this.StageAdd key
                                        failedKeys.Add key |> ignore

                                    nextCount <- nextCount + 1
                        finally
                            queue.ActiveSlot <- null

                        upCount <- nextCount
                        this.StageRemovals ()

                        if changed then
                            this.StageKeys (buildKeysArray ())
                        else
                            this.StageKeys null

                        true

    let applyGeneralDelta (delta: ProjectionDelta<'K>) (limit: int) =
        let oldUpSlots = upSlots
        let oldUpCount = upCount
        let changeCount = delta.ChangeCount
        let pendingChecks = checkSlots.Count - checkIndex

        let mutable sparseCandidate =
            sorted
            && (changeCount + dirtySlots.Count + pendingChecks)
               <= limit

        if removedPlaced.Count > 0 then
            removedPlaced.Clear ()

        if addedPlaced.Count > 0 then
            addedPlaced.Clear ()

        let rows = sortKeys.Value

        try
            for c in 0 .. changeCount - 1 do
                let change = delta.ChangeAt c
                let key = change.Key

                match change.Value with
                | KeyChange.Removed ->
                    let slot = byKey.Find key

                    if not (Platform.isNull (box slot)) then
                        byKey.Remove key
                        slot.Detach ()

                        if slot.Unsettled then
                            unsettledCount <- unsettledCount - 1

                        if slot.State = InclusionState.Failed then
                            failedKeys.Remove key
                            this.Removed.Add key
                        elif slot.State = InclusionState.Included then
                            this.Removed.Add key

                            if sparseCandidate && slot.PlacedIdx >= 0 then
                                removedPlaced.Add slot
                | KeyChange.Replaced ->
                    let oldSlot = byKey.Find key

                    let prevState, prevUnsettled =
                        if Platform.isNull (box oldSlot) then
                            InclusionState.Excluded, false
                        else
                            let st = oldSlot.State
                            let un = oldSlot.Unsettled
                            oldSlot.Detach ()

                            if
                                sparseCandidate
                                && st = InclusionState.Included
                                && oldSlot.PlacedIdx >= 0
                            then
                                removedPlaced.Add oldSlot

                            st, un

                    let entry = rows.Entries.Find key

                    if Platform.isNull entry then
                        if not (Platform.isNull (box oldSlot)) then
                            byKey.Remove key

                            if prevUnsettled then
                                unsettledCount <- unsettledCount - 1

                            if prevState = InclusionState.Failed then
                                failedKeys.Remove key
                                this.Removed.Add key
                            elif prevState = InclusionState.Included then
                                this.Removed.Add key
                    else
                        let resolved = entry.Row
                        let slot = SortSlot<'K, 'S>(key, resolved, queue)
                        slot.Attach ()
                        queue.ActiveSlot <- box slot
                        let st = resolved.EnsureCurrentUntracked ()

                        let nextState =
                            if st = Status.None then
                                slot.Unsettled <- false
                                slot.Rank <- resolved.Peek
                                InclusionState.Included
                            else
                                evaluateSlow slot resolved st

                        let nextUnsettled = slot.Unsettled
                        slot.State <- nextState
                        byKey.Set (key, slot)

                        if prevUnsettled <> nextUnsettled then
                            unsettledCount <- unsettledCount + (if nextUnsettled then 1 else -1)

                        recordStateChange key prevState nextState

                        if
                            sparseCandidate
                            && nextState = InclusionState.Included
                        then
                            addedPlaced.Add slot
                | KeyChange.Added ->
                    let entry = rows.Entries.Find key

                    if not (Platform.isNull entry) then
                        let resolved = entry.Row
                        let slot = SortSlot<'K, 'S>(key, resolved, queue)
                        slot.Attach ()
                        queue.ActiveSlot <- box slot
                        let st = resolved.EnsureCurrentUntracked ()

                        let nextState =
                            if st = Status.None then
                                slot.Unsettled <- false
                                slot.Rank <- resolved.Peek
                                InclusionState.Included
                            else
                                evaluateSlow slot resolved st

                        slot.State <- nextState
                        byKey.Set (key, slot)

                        if slot.Unsettled then
                            unsettledCount <- unsettledCount + 1

                        recordStateChange key InclusionState.Excluded nextState

                        if
                            sparseCandidate
                            && nextState = InclusionState.Included
                        then
                            addedPlaced.Add slot
                | _ -> ()
        finally
            queue.ActiveSlot <- null

        let newKeys = delta.Keys
        let newCount = newKeys.Length

        let nextUpSlots =
            if newCount = 0 then
                Array.empty
            elif scratchUpSlots.Length >= newCount then
                let reused = scratchUpSlots
                scratchUpSlots <- Array.empty
                reused
            else
                Array.zeroCreate<SortSlot<'K, 'S>>(max 16 newCount)

        let mutable cursor = 0
        let mutable outCount = 0
        let mutable reordered = false

        for i in 0 .. newCount - 1 do
            let key = Platform.itemAt i newKeys

            while cursor < oldUpCount
                  && (Platform.itemAt cursor oldUpSlots).Disposed do
                cursor <- cursor + 1

            let mutable slot =
                if
                    cursor < oldUpCount
                    && Platform.keyEquals (Platform.itemAt cursor oldUpSlots).Key key
                then
                    let hit = Platform.itemAt cursor oldUpSlots
                    cursor <- cursor + 1
                    hit
                else
                    let found = byKey.Find key

                    if
                        not (Platform.isNull (box found))
                        && found.UpIdx >= 0
                    then
                        reordered <- true

                    found

            if not (Platform.isNull (box slot)) then
                slot.UpIdx <- outCount
                Platform.setItemAt outCount nextUpSlots slot
                outCount <- outCount + 1

        if oldUpCount > 0 then
            Array.fill oldUpSlots 0 oldUpCount Unchecked.defaultof<SortSlot<'K, 'S>>

        if
            oldUpSlots.Length > 0
            && not (obj.ReferenceEquals (oldUpSlots, nextUpSlots))
        then
            scratchUpSlots <- oldUpSlots

        upSlots <- nextUpSlots
        upCount <- outCount

        if sparseCandidate && not reordered then
            drainCheckSlots limit

            if
                checkIndex >= checkSlots.Count
                && dirtySlots.Count <= limit
            then
                if
                    removedPlaced.Count = 0
                    && addedPlaced.Count = 0
                    && dirtySlots.Count = 0
                then
                    queue.DirtyNotified <- false
                    queue.CheckNotified <- false
                    this.StageRemovals ()
                    this.StageKeys null
                else
                    ensureOrderPos ()
                    let mutable changed = false

                    for i in 0 .. removedPlaced.Count - 1 do
                        let rem = Platform.entryAt i removedPlaced

                        if rem.PlacedIdx >= 0 then
                            removePlacedSlot rem
                            changed <- true

                    removedPlaced.Clear ()

                    if applyDirtySplice () then
                        changed <- true

                    for i in 0 .. addedPlaced.Count - 1 do
                        let s = Platform.entryAt i addedPlaced

                        if
                            not s.Disposed
                            && s.UpIdx >= 0
                            && s.State = InclusionState.Included
                        then
                            insertPlacedSlot s
                            changed <- true

                    addedPlaced.Clear ()
                    this.StageRemovals ()

                    if changed then stageChangedKeys () else this.StageKeys null
            else
                sparseCandidate <- false

        if not sparseCandidate || reordered then
            if removedPlaced.Count > 0 then
                removedPlaced.Clear ()

            if addedPlaced.Count > 0 then
                addedPlaced.Clear ()

            let prevPlaced = placedCount
            ensurePlacedCapacity outCount
            let mutable nextPlaced = 0

            try
                for i in 0 .. outCount - 1 do
                    let slot = Platform.itemAt i upSlots

                    if slot.QueuedDirty || slot.QueuedCheck then
                        slot.QueuedDirty <- false
                        slot.QueuedCheck <- false
                        refreshSlot slot |> ignore

                    if slot.State = InclusionState.Included then
                        slot.PlacedIdx <- nextPlaced
                        Platform.setItemAt nextPlaced placedSlots slot
                        nextPlaced <- nextPlaced + 1
                    else
                        slot.PlacedIdx <- -1
            finally
                queue.ActiveSlot <- null

            for k in nextPlaced .. prevPlaced - 1 do
                Platform.setItemAt k placedSlots Unchecked.defaultof<_>

            placedCount <- nextPlaced
            clearQueues ()
            this.StageRemovals ()
            fullSort ()
            stageChangedKeys ()

    let finishPass () =
        if failedKeys.Count > 0 then
            failedKeys.Iterate (fun key -> this.StageHidden key)

        if unsettledCount > 0 then
            for i in 0 .. upCount - 1 do
                let slot = Platform.itemAt i upSlots

                if slot.Unsettled then
                    heldOut.Add slot.Key

        heldOut.Publish upstream

    let dispose () =
        if not disposed then
            disposed <- true
            synced <- false
            sorted <- false
            orderPosValid <- false
            rangeOps <- 0
            pendingRangeValid <- false
            this.ExtraCheck <- Unchecked.defaultof<_>
            this.HasExtraCheck <- Unchecked.defaultof<_>
            this.ExtraFailedCheck <- Unchecked.defaultof<_>

            for i in 0 .. upCount - 1 do
                (Platform.itemAt i upSlots).Detach()

            if not (Platform.isNull (box keyReader)) then
                keyReader.Stop ()
                keyReader <- Unchecked.defaultof<ProjectionReader<'K>>

            if dirtySlots.Count > 0 then
                dirtySlots.Clear ()

            if checkSlots.Count > 0 then
                checkSlots.Clear ()

            checkIndex <- 0
            queue.CheckNotified <- false
            queue.DirtyNotified <- false
            queue.ActiveSlot <- null
            byKey.Clear ()
            failedKeys.Clear ()
            removedPlaced.Clear ()
            addedPlaced.Clear ()
            upSlots <- Array.empty
            scratchUpSlots <- Array.empty
            placedSlots <- Array.empty
            order <- Platform.positions 0
            orderPos <- Platform.positions 0
            upCount <- 0
            placedCount <- 0
            unsettledCount <- 0

    let init () =
        initialized <- true
        let rows = sortKeys.Value
        keyReader <- rows.NewInternalKeyReader ()
        this.ExtraCheck <- fun () -> resolveCheckStep (fun () -> this.IsCheck)
        this.HasExtraCheck <- fun () -> checkIndex < checkSlots.Count
        this.ExtraFailedCheck <- fun () -> resolveCheckStep (fun () -> not this.IsInvalidated)
        graph.RunUntracked (fun () -> this.Scope.OnCleanup dispose)

    /// <summary>
    /// The upstream keys whose sort key has never settled, in upstream order, followed by the keys <c>upstream</c> holds
    /// out of its <c>Keys</c>. A tracked read.
    /// </summary>
    member _.HeldOut = heldOut.Keys

    override _.Enumerate() =
        if not initialized then
            init ()

        rangeOps <- 0
        pendingRangeValid <- false
        let wasSynced = synced
        synced <- false
        queue.CheckNotified <- false
        queue.DirtyNotified <- false
        heldOut.Begin ()
        let delta = keyReader.Read ()

        if not wasSynced || delta.IsReset then
            rebuildFull delta.Keys
        elif delta.IsEmpty then
            let limit = incrementalLimit upCount
            let pendingChecks = checkSlots.Count - checkIndex

            if
                dirtySlots.Count > limit
                || dirtySlots.Count + pendingChecks > (limit <<< 2)
            then
                runDenseSort ()
            else
                drainCheckSlots limit

                if dirtySlots.Count <= limit then
                    let changed = applyDirtySplice ()
                    this.StageRemovals ()

                    if changed then
                        this.StageKeys (buildKeysArray ())
                    else
                        this.StageKeys null
                else
                    runDenseSort ()
        else
            let limit = incrementalLimit upCount

            if not (tryTailAppend delta limit) then
                applyGeneralDelta delta limit

        pendingRangeValid <- rangeOps = 1
        finishPass ()

    override _.Applied() =
        synced <- true

        if
            pendingRangeValid
            && this.PublishedVersion = pendingRangeFrom + 1
        then
            this.RecordKeyRange (pendingRangeFrom, pendingRangeStart, pendingRangePrevEnd, pendingRangeEnd)

        pendingRangeValid <- false

    override _.Abandoned() =
        synced <- false
        pendingRangeValid <- false
        queue.CheckNotified <- false
        queue.DirtyNotified <- false

type internal GroupView<'K, 'V when 'K: equality>(graph: Graph, upstream: Projection<'K, 'V>, readMembers: unit -> 'K[]) as this =
    inherit
        RowsByKey<'K, 'V>(
            graph,
            Unchecked.defaultof<_>,
            Unchecked.defaultof<_>,
            tagged = true,
            mapEntry = (fun entry -> EntryCache.readSingle upstream entry)
        )

    let mutable synced = false
    let mutable pendingRangeValid = false
    let mutable pendingRangeFrom = -1
    let mutable pendingRangeStart = 0
    let mutable pendingRangePrevEnd = 0
    let mutable pendingRangeEnd = 0

    do this.GetRaisesDisposed <- true

    override this.Enumerate() =
        pendingRangeValid <- false
        let keys = readMembers ()
        let winLen = keys.Length

        if synced then
            let prev = this.PublishedKeys
            let prevLen = prev.Length
            let mutable p = 0

            while p < prevLen
                  && p < winLen
                  && Platform.keyEquals (Platform.itemAt p prev) (Platform.itemAt p keys) do
                p <- p + 1

            if p = prevLen && prevLen = winLen then
                this.StageRemovals ()
                this.StageKeys null
            else
                let mutable s = 0

                while s < prevLen - p
                      && s < winLen - p
                      && Platform.keyEquals (Platform.itemAt (prevLen - 1 - s) prev) (Platform.itemAt (winLen - 1 - s) keys) do
                    s <- s + 1

                let middleStop = winLen - 1 - s

                for i in p..middleStop do
                    let k = Platform.itemAt i keys
                    this.Seen.Add k |> ignore
                    this.StageAdd k

                for i in p .. prevLen - 1 - s do
                    let k = Platform.itemAt i prev

                    if not (this.Seen.Contains k) then
                        this.Removed.Add k

                this.Seen.Clear ()
                this.StageRemovals ()
                this.StageKeys keys
                pendingRangeFrom <- this.PublishedVersion
                pendingRangeStart <- p
                pendingRangePrevEnd <- prevLen - 1 - s
                pendingRangeEnd <- middleStop
                pendingRangeValid <- true
        else
            this.Reserve winLen

            for i in 0 .. winLen - 1 do
                this.Visit (Platform.itemAt i keys)

    override this.Applied() =
        synced <- true

        if
            pendingRangeValid
            && this.PublishedVersion = pendingRangeFrom + 1
        then
            this.RecordKeyRange (pendingRangeFrom, pendingRangeStart, pendingRangePrevEnd, pendingRangeEnd)

        pendingRangeValid <- false

    override _.Abandoned() =
        synced <- false
        pendingRangeValid <- false

/// <summary>
/// A group of a <c>Grouping</c>: its members in upstream order and the inner view over them. The inner view's pass reads
/// <c>MembersCell</c> through the outer grouping's <c>ReadAfterPass</c>, isolating untouched groups from unrelated outer
/// key changes.
/// </summary>
[<Sealed>]
type internal Group<'G, 'K, 'V when 'G: equality and 'K: equality> =
    val Key: 'G
    val MembersCell: Signal<'K[]>
    val View: Projection<'K, 'V>
    val Members: ResizeArray<GroupSlot<'G, 'K, 'V>>
    val mutable OrderIdx: int
    val mutable Touched: bool

    new(graph: Graph, upstream: Projection<'K, 'V>, readCell: Signal<'K[]> -> 'K[], key: 'G) =
        let members = Signal<'K[]>(graph, Array.empty)

        let view =
            new GroupView<'K, 'V> (graph, upstream, (fun () -> readCell members)) :> Projection<'K, 'V>

        {
            Key = key
            MembersCell = members
            View = view
            Members = ResizeArray<GroupSlot<'G, 'K, 'V>>()
            OrderIdx = -1
            Touched = false
        }

    member this.FirstIdx = (Platform.entryAt 0 this.Members).UpIdx

    /// <summary>Writes the group's sorted member keys when they differ from the last pass.</summary>
    member this.Publish() =
        let current = this.MembersCell.Peek
        let n = this.Members.Count
        let mutable same = current.Length = n
        let mutable i = 0

        while same && i < n do
            if not (Platform.keyEquals (Platform.itemAt i current) (Platform.entryAt i this.Members).Key) then
                same <- false

            i <- i + 1

        if not same then
            let arr =
                if n = 0 then
                    Array.empty
                else
                    let next = Array.zeroCreate<'K> n

                    for j in 0 .. n - 1 do
                        Platform.setItemAt j next (Platform.entryAt j this.Members).Key

                    next

            this.MembersCell.Value <- arr

/// <summary>
/// Observes one key's group-key projection memo on behalf of an incremental <c>Grouping</c>.
/// </summary>
and [<Sealed>] internal GroupSlot<'G, 'K, 'V when 'G: equality and 'K: equality> =
    val Key: 'K
    val Queue: SlotQueue<GroupSlot<'G, 'K, 'V>>
    val mutable Source: Memo<'G>
    val mutable Handle: int
    val mutable UpIdx: int
    val mutable Group: Group<'G, 'K, 'V>
    val mutable GroupKey: 'G
    val mutable Ungrouped: bool
    val mutable QueuedDirty: bool
    val mutable QueuedCheck: bool
    val mutable Disposed: bool

    new(key: 'K, source: Memo<'G>, queue: SlotQueue<GroupSlot<'G, 'K, 'V>>) =
        {
            Key = key
            Queue = queue
            Source = source
            Handle = EdgeHandle.Unlinked
            UpIdx = -1
            Group = Unchecked.defaultof<Group<'G, 'K, 'V>>
            GroupKey = Unchecked.defaultof<'G>
            Ungrouped = false
            QueuedDirty = false
            QueuedCheck = false
            Disposed = false
        }

    member this.Attach() =
        this.Handle <- (this.Source :> ISource).AddObserver(this, 0)

    member this.Detach() =
        if not this.Disposed then
            this.Disposed <- true

            if this.Handle <> EdgeHandle.Unlinked then
                (this.Source :> ISource).RemoveObserverAt(this, this.Handle)
                this.Handle <- EdgeHandle.Unlinked

    interface INode with
        member this.Id = this.Queue.ViewNode.Id
        member this.Status = this.Queue.ViewNode.Status

    interface IComputation with
        member _.AddSource _ = ()
        member _.HasReadSlot _ = false

        member this.MovedIn(_, handle) =
            if not this.Disposed then
                this.Handle <- handle

        member this.MarkDirty() =
            let q = this.Queue

            if
                not this.Disposed
                && not (obj.ReferenceEquals (this, q.ActiveSlot))
            then
                if not this.QueuedDirty then
                    this.QueuedDirty <- true
                    q.Dirty.Add this

                if not q.DirtyNotified then
                    q.ViewNode.MarkDirty ()
                    q.DirtyNotified <- q.IsInvalidated ()

        member this.MarkCheck() =
            let q = this.Queue

            if
                not this.Disposed
                && not (obj.ReferenceEquals (this, q.ActiveSlot))
            then
                if not this.QueuedDirty && not this.QueuedCheck then
                    this.QueuedCheck <- true
                    q.Check.Add this

                if not q.CheckNotified && not q.DirtyNotified then
                    q.ViewNode.MarkCheck ()
                    q.CheckNotified <- q.IsCheckQueued ()

/// <summary>
/// A projection keyed by group key, ordered by the upstream position of each group's first member, whose row is the inner
/// view of the group's keys in upstream order. Returned by <c>Projection.groupBy</c>.
/// </summary>
/// <remarks>
/// A pending group key keeps the key's last settled group. A key whose group key failed or has never settled is in
/// <c>UngroupedKeys</c> and in no group. An inner view is disposed once its group is empty.
/// </remarks>
type Grouping<'G, 'K, 'V when 'G: equality and 'K: equality> internal (graph: Graph, upstream: Projection<'K, 'V>, groupKeys: Projection<'K, 'G> ref) as this
    =
    inherit Projection<'G, Projection<'K, 'V>>(graph)

    let groups = Platform.KeyMap<'G, Group<'G, 'K, 'V>>()
    let orderedGroups = ResizeArray<Group<'G, 'K, 'V>>()
    let touchedGroups = ResizeArray<Group<'G, 'K, 'V>>()
    let emptiedGroups = ResizeArray<Group<'G, 'K, 'V>>()
    let heldOut = HeldOut<'K> graph
    let adds = ResizeArray<struct ('G * Group<'G, 'K, 'V>)>()
    let writes = ResizeArray<struct (Signal<Projection<'K, 'V>> * Projection<'K, 'V>)>()
    let encodedGroupEqual = graph.EncodedComparer<Projection<'K, 'V>>()
    let queue = SlotQueue<GroupSlot<'G, 'K, 'V>>.Create this
    let dirtySlots = queue.Dirty
    let checkSlots = queue.Check
    let mutable initialized = false
    let mutable disposed = false
    let mutable synced = false

    let mutable keyReader: ProjectionReader<'K> =
        Unchecked.defaultof<ProjectionReader<'K>>

    let mutable upSlots: GroupSlot<'G, 'K, 'V>[] = Array.empty
    let mutable scratchUpSlots: GroupSlot<'G, 'K, 'V>[] = Array.empty
    let mutable upCount = 0
    let byKey = Platform.KeyMap<'K, GroupSlot<'G, 'K, 'V>>()
    let pendingInserts = ResizeArray<GroupSlot<'G, 'K, 'V>>()
    let mutable ungroupedCount = 0
    let mutable checkIndex = 0
    let mutable outerOrderDirty = false
    let mutable outerMembershipChanged = false

    let hasObservedGroup () =
        let mutable found = false
        let mutable i = 0
        let count = orderedGroups.Count

        while not found && i < count do
            if (Platform.entryAt i orderedGroups).MembersCell.ObserverCount > 0 then
                found <- true

            i <- i + 1

        found

    do this.ExtraObserved <- fun () -> heldOut.HasObservers || hasObservedGroup ()

    let incrementalLimit n =
        max 8 (min 64 (n >>> 4))

    let ensureUpCapacity needed =
        if upSlots.Length < needed then
            let grown =
                Array.zeroCreate<GroupSlot<'G, 'K, 'V>>(max (max 16 needed) (upSlots.Length * 2))

            if upCount > 0 then
                Array.blit upSlots 0 grown 0 upCount

            upSlots <- grown

    let clearQueues () =
        if dirtySlots.Count > 0 then
            for i in 0 .. dirtySlots.Count - 1 do
                (Platform.entryAt i dirtySlots).QueuedDirty <- false

            dirtySlots.Clear ()

        if checkIndex < checkSlots.Count then
            for i in checkIndex .. checkSlots.Count - 1 do
                (Platform.entryAt i checkSlots).QueuedCheck <- false

        if checkSlots.Count > 0 then
            checkSlots.Clear ()

        checkIndex <- 0
        queue.CheckNotified <- false
        queue.DirtyNotified <- false

    let enqueueCheckSlot (slot: GroupSlot<'G, 'K, 'V>) =
        if not slot.QueuedDirty && not slot.QueuedCheck then
            slot.QueuedCheck <- true
            checkSlots.Add slot

    let evaluateSlot (slot: GroupSlot<'G, 'K, 'V>) (source: Memo<'G>) =
        queue.ActiveSlot <- box slot
        let st = source.EnsureCurrentUntracked ()

        if st = Status.None then
            slot.Ungrouped <- false
            slot.GroupKey <- source.Peek
        else
            if
                not slot.Disposed
                && (st &&& Status.Stale) <> Status.None
            then
                enqueueCheckSlot slot

                if not queue.CheckNotified && not queue.DirtyNotified then
                    (this :> IComputation).MarkCheck()
                    queue.CheckNotified <- queue.IsCheckQueued ()

            if StatusBits.isPending st then
                if not source.Published then
                    slot.Ungrouped <- true
                else
                    slot.Ungrouped <- false
                    slot.GroupKey <- source.Peek
            elif StatusBits.isError st then
                slot.Ungrouped <- true
            else
                slot.Ungrouped <- false
                slot.GroupKey <- source.Peek

    let markGroupTouched (group: Group<'G, 'K, 'V>) =
        if not group.Touched then
            group.Touched <- true
            touchedGroups.Add group

    let clearTouchedGroups () =
        for i in 0 .. touchedGroups.Count - 1 do
            (Platform.entryAt i touchedGroups).Touched <- false

        if touchedGroups.Count > 0 then
            touchedGroups.Clear ()

    let getOrCreateGroup (groupKey: 'G) =
        let mutable group = groups.Find groupKey

        if Platform.isNull (box group) then
            group <- graph.RunOwned (this.Scope, fun () -> Group<'G, 'K, 'V>(graph, upstream, this.ReadAfterPass, groupKey))

            groups.Set (groupKey, group)

        group

    let detachFromGroup (slot: GroupSlot<'G, 'K, 'V>) (trackTouches: bool) =
        let group = slot.Group

        if not (Platform.isNull (box group)) then
            let idx =
                MemberSlots.removeSortedByUpIdx (fun (s: GroupSlot<'G, 'K, 'V>) -> s.UpIdx) group.Members slot.UpIdx

            slot.Group <- Unchecked.defaultof<_>

            if trackTouches then
                markGroupTouched group

                if idx = 0 && group.Members.Count > 0 then
                    outerOrderDirty <- true

    let insertIntoGroup (slot: GroupSlot<'G, 'K, 'V>) (trackTouches: bool) =
        let group = getOrCreateGroup slot.GroupKey

        let pos =
            MemberSlots.insertSortedByUpIdx (fun (s: GroupSlot<'G, 'K, 'V>) -> s.UpIdx) group.Members slot

        slot.Group <- group

        if trackTouches then
            markGroupTouched group

            if pos = 0 && group.OrderIdx >= 0 then
                outerOrderDirty <- true

    let appendIntoGroup (slot: GroupSlot<'G, 'K, 'V>) (trackTouches: bool) =
        let group = getOrCreateGroup slot.GroupKey
        group.Members.Add slot
        slot.Group <- group

        if trackTouches then
            markGroupTouched group

            if group.Members.Count = 1 && group.OrderIdx >= 0 then
                outerOrderDirty <- true

    let removeGroupAt (idx: int) =
        orderedGroups.RemoveAt idx

        for i in idx .. orderedGroups.Count - 1 do
            (Platform.entryAt i orderedGroups).OrderIdx <- i

    let insertOrderedGroup (group: Group<'G, 'K, 'V>) =
        let pos =
            MemberSlots.insertBoundByUpIdx (fun (g: Group<'G, 'K, 'V>) -> g.FirstIdx) orderedGroups group.FirstIdx

        orderedGroups.Insert (pos, group)

        for i in pos .. orderedGroups.Count - 1 do
            (Platform.entryAt i orderedGroups).OrderIdx <- i

    let sortOrderedGroups () =
        let n = orderedGroups.Count

        for i in 1 .. n - 1 do
            let g = Platform.entryAt i orderedGroups
            let keyIdx = g.FirstIdx
            let mutable j = i - 1

            while j >= 0
                  && (Platform.entryAt j orderedGroups).FirstIdx > keyIdx do
                let prev = Platform.entryAt j orderedGroups
                orderedGroups[j + 1] <- prev
                prev.OrderIdx <- j + 1
                j <- j - 1

            orderedGroups[j + 1] <- g
            g.OrderIdx <- j + 1

        if n > 0 then
            (Platform.entryAt 0 orderedGroups).OrderIdx <- 0

    let stageOuterKeys (checkPublishChange: bool) =
        this.StageRemovals ()

        if not checkPublishChange then
            this.StageKeys null
        else
            let n = orderedGroups.Count
            let prev = this.PublishedKeys

            let same =
                prev.Length = n
                && (let mutable eq = true
                    let mutable i = 0

                    while eq && i < n do
                        if not (Platform.keyEquals (Platform.itemAt i prev) (Platform.entryAt i orderedGroups).Key) then
                            eq <- false

                        i <- i + 1

                    eq)

            if same then
                this.StageKeys null
            elif n = 0 then
                this.StageKeys Array.empty
            else
                let arr = Array.zeroCreate<'G> n

                for i in 0 .. n - 1 do
                    Platform.setItemAt i arr (Platform.entryAt i orderedGroups).Key

                this.StageKeys arr

    let finalizeTouchedGroups () =
        for i in 0 .. touchedGroups.Count - 1 do
            let group = Platform.entryAt i touchedGroups

            if group.Members.Count = 0 then
                group.Touched <- false
                groups.Remove group.Key

                if group.OrderIdx >= 0 then
                    removeGroupAt group.OrderIdx
                    group.OrderIdx <- -1
                    outerMembershipChanged <- true

                if not (Platform.isNull (this.Entries.Find group.Key)) then
                    this.Removed.Add group.Key

                emptiedGroups.Add group

        for i in 0 .. touchedGroups.Count - 1 do
            let group = Platform.entryAt i touchedGroups

            if group.Touched then
                group.Touched <- false

                if group.Members.Count > 0 then
                    group.Publish ()

                    if group.OrderIdx < 0 then
                        if outerOrderDirty then
                            group.OrderIdx <- orderedGroups.Count
                            orderedGroups.Add group
                        else
                            insertOrderedGroup group

                        outerMembershipChanged <- true

                    let entry = this.Entries.Find group.Key

                    if Platform.isNull entry then
                        adds.Add (struct (group.Key, group))
                    else
                        let item = (entry :?> ItemRow<Projection<'K, 'V>, 'G, Projection<'K, 'V>>).Item

                        if not (obj.ReferenceEquals (item.Peek, group.View)) then
                            writes.Add (struct (item, group.View))

        touchedGroups.Clear ()

        if outerOrderDirty && orderedGroups.Count > 1 then
            sortOrderedGroups ()

        stageOuterKeys (outerMembershipChanged || outerOrderDirty)
        outerOrderDirty <- false
        outerMembershipChanged <- false

    let disposeEmptiedGroups () =
        for i in 0 .. emptiedGroups.Count - 1 do
            (Platform.entryAt i emptiedGroups).View.Dispose()

        if emptiedGroups.Count > 0 then
            emptiedGroups.Clear ()

    let refreshSlot (slot: GroupSlot<'G, 'K, 'V>) (insertNow: bool) =
        let wasUngrouped = slot.Ungrouped
        let prevGroupKey = slot.GroupKey
        evaluateSlot slot slot.Source

        if wasUngrouped <> slot.Ungrouped then
            ungroupedCount <-
                ungroupedCount
                + (if slot.Ungrouped then 1 else -1)

        if not wasUngrouped && not slot.Ungrouped then
            if not (Platform.keyEquals prevGroupKey slot.GroupKey) then
                detachFromGroup slot true

                if insertNow then
                    insertIntoGroup slot true
                else
                    pendingInserts.Add slot
        else
            if not wasUngrouped then
                detachFromGroup slot true

            if not slot.Ungrouped then
                if insertNow then
                    insertIntoGroup slot true
                else
                    pendingInserts.Add slot

    let resolveCheckStep (keepRunning: unit -> bool) =
        let stepEnd = checkSlots.Count

        while keepRunning () && checkIndex < stepEnd do
            let slot = Platform.entryAt checkIndex checkSlots
            checkIndex <- checkIndex + 1

            if not slot.Disposed then
                slot.QueuedCheck <- false

                if not slot.QueuedDirty then
                    let source = slot.Source :> ISource
                    source.UpdateIfNecessary ()

                    if
                        not slot.Disposed
                        && not slot.QueuedDirty
                        && source.IsStale
                    then
                        if source.IsActive then
                            (slot :> IComputation).MarkDirty()
                        else
                            enqueueCheckSlot slot

        if checkIndex >= checkSlots.Count then
            if checkSlots.Count > 0 then
                checkSlots.Clear ()

            checkIndex <- 0

        queue.CheckNotified <- false

    let drainCheckSlots (limit: int) =
        let drainEnd = checkSlots.Count

        while checkIndex < drainEnd && dirtySlots.Count <= limit do
            let slot = Platform.entryAt checkIndex checkSlots
            checkIndex <- checkIndex + 1

            if not slot.Disposed then
                slot.QueuedCheck <- false

                if not slot.QueuedDirty then
                    let source = slot.Source :> ISource
                    source.UpdateIfNecessary ()

                    if
                        not slot.Disposed
                        && not slot.QueuedDirty
                        && source.IsStale
                    then
                        slot.QueuedDirty <- true
                        dirtySlots.Add slot

        if checkIndex >= checkSlots.Count then
            if checkSlots.Count > 0 then
                checkSlots.Clear ()

            checkIndex <- 0
            queue.CheckNotified <- false

    let applyDirtySplice () =
        outerOrderDirty <- false
        outerMembershipChanged <- false

        if dirtySlots.Count = 0 then
            this.StageRemovals ()
            this.StageKeys null
        else
            try
                let mutable i = 0

                while i < dirtySlots.Count do
                    let slot = Platform.entryAt i dirtySlots
                    i <- i + 1

                    if not slot.Disposed then
                        slot.QueuedDirty <- false
                        refreshSlot slot true
            finally
                queue.ActiveSlot <- null

            dirtySlots.Clear ()
            queue.DirtyNotified <- false
            finalizeTouchedGroups ()

    let finalizeAllGroupsFromUpSlots () =
        for i in 0 .. orderedGroups.Count - 1 do
            let g = Platform.entryAt i orderedGroups
            g.Members.Clear ()
            g.OrderIdx <- -1

        orderedGroups.Clear ()
        clearTouchedGroups ()

        for i in 0 .. upCount - 1 do
            let slot = Platform.itemAt i upSlots
            slot.Group <- Unchecked.defaultof<_>

            if not slot.Ungrouped then
                appendIntoGroup slot false

                if slot.Group.Members.Count = 1 then
                    slot.Group.OrderIdx <- orderedGroups.Count
                    orderedGroups.Add slot.Group

        groups.Iterate (fun _ g ->
            if g.Members.Count = 0 then
                emptiedGroups.Add g
            else
                g.Publish ()

                let entry = this.Entries.Find g.Key

                if Platform.isNull entry then
                    adds.Add (struct (g.Key, g))
                else
                    let item = (entry :?> ItemRow<Projection<'K, 'V>, 'G, Projection<'K, 'V>>).Item

                    if not (obj.ReferenceEquals (item.Peek, g.View)) then
                        writes.Add (struct (item, g.View)))

        for i in 0 .. emptiedGroups.Count - 1 do
            let g = Platform.entryAt i emptiedGroups
            groups.Remove g.Key

        if this.Entries.Count > 0 then
            this.Entries.Iterate (fun gKey _ ->
                if Platform.isNull (box (groups.Find gKey)) then
                    this.Removed.Add gKey)

        stageOuterKeys true
        outerOrderDirty <- false
        outerMembershipChanged <- false

    let runDenseGrouping () =
        clearQueues ()
        let mutable nextUngrouped = 0

        try
            for i in 0 .. upCount - 1 do
                let slot = Platform.itemAt i upSlots
                evaluateSlot slot slot.Source

                if slot.Ungrouped then
                    nextUngrouped <- nextUngrouped + 1
        finally
            queue.ActiveSlot <- null

        ungroupedCount <- nextUngrouped
        finalizeAllGroupsFromUpSlots ()

    let rebuildFull (newKeys: 'K[]) =
        clearQueues ()
        clearTouchedGroups ()
        pendingInserts.Clear ()
        outerOrderDirty <- false
        outerMembershipChanged <- false
        let oldUpSlots = upSlots
        let oldUpCount = upCount
        let newCount = newKeys.Length

        for i in 0 .. oldUpCount - 1 do
            let slot = Platform.itemAt i oldUpSlots
            slot.UpIdx <- -1
            slot.Group <- Unchecked.defaultof<_>

        let nextUpSlots =
            if newCount = 0 then
                Array.empty
            elif scratchUpSlots.Length >= newCount then
                let reused = scratchUpSlots
                scratchUpSlots <- Array.empty
                reused
            else
                Array.zeroCreate<GroupSlot<'G, 'K, 'V>>(max 16 newCount)

        let mutable outCount = 0
        let mutable nextUngrouped = 0
        let rows = groupKeys.Value

        try
            for i in 0 .. newCount - 1 do
                let key = Platform.itemAt i newKeys
                let entry = rows.Entries.Find key

                if not (Platform.isNull entry) then
                    let resolved = entry.Row
                    let mutable slot = byKey.Find key

                    if
                        not (Platform.isNull (box slot))
                        && not (obj.ReferenceEquals (slot.Source, resolved))
                    then
                        slot.Detach ()
                        slot <- Unchecked.defaultof<_>

                    if Platform.isNull (box slot) then
                        slot <- GroupSlot<'G, 'K, 'V>(key, resolved, queue)
                        slot.Attach ()
                        byKey.Set (key, slot)

                    slot.UpIdx <- outCount
                    evaluateSlot slot resolved

                    if slot.Ungrouped then
                        nextUngrouped <- nextUngrouped + 1

                    Platform.setItemAt outCount nextUpSlots slot
                    outCount <- outCount + 1
        finally
            queue.ActiveSlot <- null

        for i in 0 .. oldUpCount - 1 do
            let slot = Platform.itemAt i oldUpSlots

            if slot.UpIdx < 0 && not slot.Disposed then
                if obj.ReferenceEquals (byKey.Find slot.Key, slot) then
                    byKey.Remove slot.Key

                slot.Detach ()

        if oldUpCount > 0 then
            Array.fill oldUpSlots 0 oldUpCount Unchecked.defaultof<GroupSlot<'G, 'K, 'V>>

        if
            oldUpSlots.Length > 0
            && not (obj.ReferenceEquals (oldUpSlots, nextUpSlots))
        then
            scratchUpSlots <- oldUpSlots

        upSlots <- nextUpSlots
        upCount <- outCount
        ungroupedCount <- nextUngrouped
        finalizeAllGroupsFromUpSlots ()

    let tryTailAppend (delta: ProjectionDelta<'K>) (limit: int) =
        let newKeys = delta.Keys
        let addedCount = delta.ChangeCount

        if
            addedCount <= 0
            || addedCount > limit
            || newKeys.Length <> upCount + addedCount
            || (dirtySlots.Count + (checkSlots.Count - checkIndex)) > limit
        then
            false
        else
            let mutable allAdded = true
            let mutable c = 0

            while allAdded && c < addedCount do
                if (delta.ChangeAt c).Value <> KeyChange.Added then
                    allAdded <- false

                c <- c + 1

            if not allAdded then
                false
            else
                let mutable samePrefix = true
                let mutable i = 0

                while samePrefix && i < upCount do
                    if not (Platform.keyEquals (Platform.itemAt i upSlots).Key (Platform.itemAt i newKeys)) then
                        samePrefix <- false

                    i <- i + 1

                if not samePrefix then
                    false
                else
                    drainCheckSlots limit

                    if dirtySlots.Count > limit then
                        false
                    else
                        outerOrderDirty <- false
                        outerMembershipChanged <- false
                        let oldCount = upCount

                        try
                            let mutable i = 0

                            while i < dirtySlots.Count do
                                let slot = Platform.entryAt i dirtySlots
                                i <- i + 1

                                if not slot.Disposed then
                                    slot.QueuedDirty <- false
                                    refreshSlot slot true
                        finally
                            queue.ActiveSlot <- null

                        dirtySlots.Clear ()
                        queue.DirtyNotified <- false

                        ensureUpCapacity (oldCount + addedCount)
                        let mutable nextCount = oldCount
                        let rows = groupKeys.Value

                        try
                            for j in 0 .. addedCount - 1 do
                                let key = Platform.itemAt (oldCount + j) newKeys
                                let entry = rows.Entries.Find key

                                if not (Platform.isNull entry) then
                                    let resolved = entry.Row
                                    let slot = GroupSlot<'G, 'K, 'V>(key, resolved, queue)
                                    slot.Attach ()
                                    slot.UpIdx <- nextCount
                                    evaluateSlot slot resolved
                                    byKey.Set (key, slot)
                                    Platform.setItemAt nextCount upSlots slot

                                    if slot.Ungrouped then
                                        ungroupedCount <- ungroupedCount + 1
                                    else
                                        appendIntoGroup slot true

                                    nextCount <- nextCount + 1
                        finally
                            queue.ActiveSlot <- null

                        upCount <- nextCount
                        finalizeTouchedGroups ()
                        true

    let applyGeneralDelta (delta: ProjectionDelta<'K>) =
        let oldUpSlots = upSlots
        let oldUpCount = upCount
        let changeCount = delta.ChangeCount
        let rows = groupKeys.Value
        outerOrderDirty <- false
        outerMembershipChanged <- false
        pendingInserts.Clear ()

        try
            for c in 0 .. changeCount - 1 do
                let change = delta.ChangeAt c
                let key = change.Key

                match change.Value with
                | KeyChange.Removed ->
                    let slot = byKey.Find key

                    if not (Platform.isNull (box slot)) then
                        byKey.Remove key
                        detachFromGroup slot true
                        slot.Detach ()

                        if slot.Ungrouped then
                            ungroupedCount <- ungroupedCount - 1
                | KeyChange.Replaced ->
                    let oldSlot = byKey.Find key

                    let prevUngrouped =
                        not (Platform.isNull (box oldSlot))
                        && oldSlot.Ungrouped

                    if not (Platform.isNull (box oldSlot)) then
                        detachFromGroup oldSlot true
                        oldSlot.Detach ()

                    let entry = rows.Entries.Find key

                    if Platform.isNull entry then
                        if not (Platform.isNull (box oldSlot)) then
                            byKey.Remove key

                            if prevUngrouped then
                                ungroupedCount <- ungroupedCount - 1
                    else
                        let resolved = entry.Row
                        let slot = GroupSlot<'G, 'K, 'V>(key, resolved, queue)
                        slot.Attach ()
                        evaluateSlot slot resolved
                        byKey.Set (key, slot)

                        if prevUngrouped <> slot.Ungrouped then
                            ungroupedCount <-
                                ungroupedCount
                                + (if slot.Ungrouped then 1 else -1)

                        if not slot.Ungrouped then
                            pendingInserts.Add slot
                | KeyChange.Added ->
                    let entry = rows.Entries.Find key

                    if not (Platform.isNull entry) then
                        let resolved = entry.Row
                        let slot = GroupSlot<'G, 'K, 'V>(key, resolved, queue)
                        slot.Attach ()
                        evaluateSlot slot resolved
                        byKey.Set (key, slot)

                        if slot.Ungrouped then
                            ungroupedCount <- ungroupedCount + 1
                        else
                            pendingInserts.Add slot
                | _ -> ()

            queue.ActiveSlot <- null

            while checkIndex < checkSlots.Count do
                let slot = Platform.entryAt checkIndex checkSlots
                checkIndex <- checkIndex + 1

                if not slot.Disposed then
                    slot.QueuedCheck <- false

                    if not slot.QueuedDirty then
                        let source = slot.Source :> ISource
                        source.UpdateIfNecessary ()

                        if
                            not slot.Disposed
                            && not slot.QueuedDirty
                            && source.IsStale
                        then
                            slot.QueuedDirty <- true
                            dirtySlots.Add slot

            let mutable i = 0

            while i < dirtySlots.Count do
                let slot = Platform.entryAt i dirtySlots
                i <- i + 1

                if not slot.Disposed then
                    slot.QueuedDirty <- false
                    refreshSlot slot false
        finally
            queue.ActiveSlot <- null

        clearQueues ()

        let newKeys = delta.Keys
        let newCount = newKeys.Length

        let nextUpSlots =
            if newCount = 0 then
                Array.empty
            elif scratchUpSlots.Length >= newCount then
                let reused = scratchUpSlots
                scratchUpSlots <- Array.empty
                reused
            else
                Array.zeroCreate<GroupSlot<'G, 'K, 'V>>(max 16 newCount)

        let mutable cursor = 0
        let mutable outCount = 0
        let mutable reordered = false

        for i in 0 .. newCount - 1 do
            let key = Platform.itemAt i newKeys

            while cursor < oldUpCount
                  && (Platform.itemAt cursor oldUpSlots).Disposed do
                cursor <- cursor + 1

            let slot =
                if
                    cursor < oldUpCount
                    && Platform.keyEquals (Platform.itemAt cursor oldUpSlots).Key key
                then
                    let hit = Platform.itemAt cursor oldUpSlots
                    cursor <- cursor + 1
                    hit
                else
                    let found = byKey.Find key

                    if
                        not (Platform.isNull (box found))
                        && found.UpIdx >= 0
                    then
                        reordered <- true

                    found

            if not (Platform.isNull (box slot)) then
                slot.UpIdx <- outCount
                Platform.setItemAt outCount nextUpSlots slot
                outCount <- outCount + 1

        if oldUpCount > 0 then
            Array.fill oldUpSlots 0 oldUpCount Unchecked.defaultof<GroupSlot<'G, 'K, 'V>>

        if
            oldUpSlots.Length > 0
            && not (obj.ReferenceEquals (oldUpSlots, nextUpSlots))
        then
            scratchUpSlots <- oldUpSlots

        upSlots <- nextUpSlots
        upCount <- outCount

        if reordered then
            pendingInserts.Clear ()
            finalizeAllGroupsFromUpSlots ()
        else
            for i in 0 .. pendingInserts.Count - 1 do
                let slot = Platform.entryAt i pendingInserts

                if
                    not slot.Disposed
                    && slot.UpIdx >= 0
                    && not slot.Ungrouped
                    && Platform.isNull (box slot.Group)
                then
                    insertIntoGroup slot true

            pendingInserts.Clear ()
            finalizeTouchedGroups ()

    let finishPass () =
        if ungroupedCount > 0 then
            for i in 0 .. upCount - 1 do
                let slot = Platform.itemAt i upSlots

                if slot.Ungrouped then
                    heldOut.Add slot.Key

        heldOut.Publish upstream
        disposeEmptiedGroups ()

    let dispose () =
        if not disposed then
            disposed <- true
            synced <- false
            this.ExtraCheck <- Unchecked.defaultof<_>
            this.HasExtraCheck <- Unchecked.defaultof<_>
            this.ExtraFailedCheck <- Unchecked.defaultof<_>

            for i in 0 .. upCount - 1 do
                (Platform.itemAt i upSlots).Detach()

            if not (Platform.isNull (box keyReader)) then
                keyReader.Stop ()
                keyReader <- Unchecked.defaultof<ProjectionReader<'K>>

            clearQueues ()
            clearTouchedGroups ()
            pendingInserts.Clear ()
            emptiedGroups.Clear ()
            orderedGroups.Clear ()
            queue.ActiveSlot <- null
            byKey.Clear ()
            groups.Clear ()
            upSlots <- Array.empty
            scratchUpSlots <- Array.empty
            upCount <- 0
            ungroupedCount <- 0

    let init () =
        initialized <- true
        let rows = groupKeys.Value
        keyReader <- rows.NewInternalKeyReader ()
        this.ExtraCheck <- fun () -> resolveCheckStep (fun () -> this.IsCheck)
        this.HasExtraCheck <- fun () -> checkIndex < checkSlots.Count
        this.ExtraFailedCheck <- fun () -> resolveCheckStep (fun () -> not this.IsInvalidated)
        graph.RunUntracked (fun () -> this.Scope.OnCleanup dispose)

    member private this.RunGroupRow(entry: ItemRow<Projection<'K, 'V>, 'G, Projection<'K, 'V>>) : Projection<'K, 'V> =
        try
            let value = entry.Item.Value

            if not entry.Row.Violated then
                entry.Settled <- true

            this.Report (entry, false)
            value
        with ex ->
            this.FailRow (entry, ex)
            reraise ()

    member private this.CreateAdded() =
        for i in 0 .. adds.Count - 1 do
            let struct (groupKey, group) = Platform.entryAt i adds

            let source =
                Signal<Projection<'K, 'V>>.OfEncoded(graph, group.View, encodedGroupEqual)

            let entry = ItemRow<Projection<'K, 'V>, 'G, Projection<'K, 'V>>(groupKey, source)
            this.AddEntry (groupKey, entry)

            entry.Row <- Memo<Projection<'K, 'V>>.CreateEncoded(graph, (fun _ -> this.RunGroupRow entry), ScopeMode.ValueRow, encodedGroupEqual)

    member private _.Enumerate() =
        if not initialized then
            init ()

        let wasSynced = synced
        synced <- false
        queue.CheckNotified <- false
        queue.DirtyNotified <- false
        heldOut.Begin ()
        let delta = keyReader.Read ()

        if not wasSynced || delta.IsReset then
            rebuildFull delta.Keys
        elif delta.IsEmpty then
            let limit = incrementalLimit upCount
            let pendingChecks = checkSlots.Count - checkIndex

            if
                dirtySlots.Count > limit
                || dirtySlots.Count + pendingChecks > (limit <<< 2)
            then
                runDenseGrouping ()
            else
                drainCheckSlots limit

                if dirtySlots.Count <= limit then
                    applyDirtySplice ()
                else
                    runDenseGrouping ()
        else
            let limit = incrementalLimit upCount

            if not (tryTailAppend delta limit) then
                applyGeneralDelta delta

        finishPass ()

    /// <summary>
    /// The upstream keys without a group: keys whose group key has never settled or has raised, in upstream order, then the
    /// pending keys held out of the upstream view's <c>Keys</c>.
    /// </summary>
    /// <remarks>
    /// Tracked; wakes its reader when the list changes. A read raises what the pass raised, as <c>Keys</c> does.
    /// </remarks>
    member this.UngroupedKeys: 'K[] = this.ReadAfterPass heldOut.Cell

    /// <summary>The group of <c>key</c>, as a tracked read of the key's group key.</summary>
    /// <remarks>
    /// While the key's group key is pending, returns the key's last settled group, or raises <c>NotReadyException</c> if it
    /// has never settled. A raising group key re-raises its exception, including for a key that settled before. Raises
    /// <c>KeyNotFoundException</c> for a key absent from the upstream <c>Keys</c>, including a pending key held out by the
    /// upstream view, as the upstream <c>Get</c> does.
    /// </remarks>
    member this.GroupOf(key: 'K) : 'G =
        groupKeys.Value.GetSettled key

    interface IProjectionPass with
        member this.Enumerate() =
            this.Enumerate ()

        member this.CreateAdded() =
            if adds.Count > 0 then
                graph.RunOwned (this.Scope, this.CreateAdded)

        member _.AddedCount = adds.Count

        member _.CommitWrites() =
            for i in 0 .. writes.Count - 1 do
                let struct (source, item) = Platform.entryAt i writes
                source.Value <- item

        member _.ClearStaged() =
            adds.Clear ()
            writes.Clear ()

        member _.Applied() =
            synced <- true

        member _.Abandoned() =
            synced <- false
            queue.CheckNotified <- false
            queue.DirtyNotified <- false

/// <summary>
/// The reads of an aggregate's <c>add</c>, <c>subtract</c> or folder since its last full recompute. A change to any of them
/// runs <c>changed</c>.
/// </summary>
[<Sealed>]
type internal FoldReads(graph: Graph, changed: unit -> unit) =
    let id = graph.NextId ()
    let read = System.Collections.Generic.HashSet<ISource>(HashIdentity.Reference)
    let order = ResizeArray<ISource>()

    /// <summary>The edge handle of each source in <c>order</c>.</summary>
    let handles = ResizeArray<int>()
    let mutable dirty = false
    let mutable check = false

    /// <summary>Whether any recorded source is currently stale.</summary>
    member private _.HasStaleSource() =
        let mutable i = 0

        while i < order.Count
              && not (Platform.entryAt i order).IsStale do
            i <- i + 1

        i < order.Count

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CheckActiveSource(i: int) =
        if not dirty then
            let source = Platform.entryAt i order

            if source.IsStale && source.IsActive then
                (this :> IComputation).MarkDirty()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.MovedAgain() =
        let mutable again = this.HasStaleSource ()

        while again do
            let before = graph.StaleTransitions
            let mutable i = 0

            while not dirty && i < order.Count do
                (Platform.entryAt i order).UpdateIfNecessary()

                if graph.ActiveSkips <> 0 then
                    this.CheckActiveSource i

                i <- i + 1

            again <-
                not dirty
                && graph.StaleTransitions <> before
                && this.HasStaleSource ()

    /// <summary>Whether a read source moved since the last call. Brings each read source current first.</summary>
    member this.Moved() =
        if check && not dirty then
            let before = graph.StaleTransitions
            let mutable i = 0

            while not dirty && i < order.Count do
                (Platform.entryAt i order).UpdateIfNecessary()

                if graph.ActiveSkips <> 0 then
                    this.CheckActiveSource i

                i <- i + 1

            if not dirty && graph.StaleTransitions <> before then
                this.MovedAgain ()

        let moved = dirty
        check <- false
        dirty <- false
        moved

    /// <summary>Whether a read source has signalled a check or a change since the last <c>Moved</c>.</summary>
    member _.IsPendingCheckOrDirty = check || dirty

    /// <summary>Drops every read edge.</summary>
    member this.Clear() =
        for i in 0 .. order.Count - 1 do
            (Platform.entryAt i order).RemoveObserverAt(this :> IComputation, Platform.entryAt i handles)
            Platform.setEntryAt i handles EdgeHandle.Unlinked

        read.Clear ()
        order.Clear ()
        handles.Clear ()
        check <- false
        dirty <- false

    interface INode with
        member _.Id = id
        member _.Status = Status.None

    interface IComputation with
        member this.AddSource source =
            if read.Add source then
                order.Add source
                handles.Add (source.AddObserver (this :> IComputation, order.Count - 1))

        member _.MovedIn(slot, handle) =
            Platform.setEntryAt slot handles handle

        member _.HasReadSlot _ = false

        member _.MarkDirty() =
            dirty <- true
            changed ()

        member _.MarkCheck() =
            check <- true
            changed ()

    interface IScopeHost with
        member _.CanOwn = false

        member _.Scope =
            raise (
                System.InvalidOperationException
                    "A projection aggregate's add, subtract or folder created an owned node. These functions re-run on every row change; create the node outside the aggregate."
            )

/// <summary>
/// One live key of an aggregate: the observer of the key's row and the row's last settled value.
/// </summary>
[<Sealed>]
type internal FoldRow<'K, 'V>(graph: Graph, entry: RowEntry<'K, 'V>, touched: FoldRow<'K, 'V> -> unit) =
    let id = graph.NextId ()

    member _.Entry = entry

    /// <summary>Whether the row waits in the aggregate's queue, or is being read.</summary>
    member val Queued = false with get, set

    /// <summary>Whether the aggregate observes the row.</summary>
    member val Attached = false with get, set

    /// <summary>Whether <c>Value</c> holds a settled value, contained in the aggregate state.</summary>
    member val HasValue = false with get, set

    member val Value = Unchecked.defaultof<'V> with get, set

    /// <summary>The row's exception while the row is failed, or null.</summary>
    member val Error: exn = null with get, set

    /// <summary>The last membership diff containing the key.</summary>
    member val Generation = 0 with get, set

    member val Handle = EdgeHandle.Unlinked with get, set

    interface INode with
        member _.Id = id
        member _.Status = Status.None

    interface IComputation with
        member _.AddSource _ = ()

        member this.MovedIn(_, handle) =
            this.Handle <- handle

        member _.HasReadSlot _ = false

        member this.MarkDirty() =
            if not this.Queued then
                touched this

        member this.MarkCheck() =
            if not this.Queued then
                touched this

/// <summary>
/// The fold of the settled row values of <c>upstream</c>, kept current in O(changed rows) when <c>subtract</c> is non-null
/// and recomputed from the cached row values otherwise. Read through the memo it computes.
/// </summary>
/// <remarks>
/// A pending row keeps its last settled value in the state, and a row contributes from its first settled value. The state
/// returns to <c>zero</c> when the last row value leaves it. While a row is failed, the memo raises the row's exception. A throwing <c>add</c> or <c>subtract</c> makes the next
/// read recompute from the cached row values.
/// </remarks>
[<Sealed>]
type internal ProjectionFold<'K, 'V, 'S when 'K: equality>
    (graph: Graph, upstream: Projection<'K, 'V>, add: 'S -> 'V -> 'S, subtract: 'S -> 'V -> 'S, zero: 'S, invertible: bool) =
    let id = graph.NextId ()
    let observers = ObserverSet ()
    let equal = graph.EncodedComparer<'V>()

    /// <summary>Whether a comparison through <c>equal</c> runs outside an exception handler.</summary>
    let equalIsSafe = SafeEquality.isSafe equal

    let rows = Platform.KeyMap<'K, FoldRow<'K, 'V>>()

    /// <summary>The rows waiting for a read, in the first <c>queued</c> slots.</summary>
    let mutable queue: FoldRow<'K, 'V>[] = Array.zeroCreate 4

    let mutable queued = 0
    let added = ResizeArray<FoldRow<'K, 'V>>()
    let removed = ResizeArray<'K>()

#if FABLE_COMPILER
    let invokeAdd (s: 'S) (v: 'V) = add s v

    let invokeSub (s: 'S) (v: 'V) =
        subtract s v
#else
    let addFn = OptimizedClosures.FSharpFunc<'S, 'V, 'S>.Adapt add

    let subFn =
        if invertible then
            OptimizedClosures.FSharpFunc<'S, 'V, 'S>.Adapt subtract
        else
            Unchecked.defaultof<OptimizedClosures.FSharpFunc<'S, 'V, 'S>>

    let invokeAdd (s: 'S) (v: 'V) =
        addFn.Invoke (s, v)

    let invokeSub (s: 'S) (v: 'V) =
        subFn.Invoke (s, v)
#endif

    /// <summary>The <c>Keys</c> array of the last membership diff, or null before the first read.</summary>
    let mutable lastKeys: 'K[] = null

    let mutable orderedRows: FoldRow<'K, 'V>[] = null
    let mutable generation = 0
    let mutable state = zero

    /// <summary>Set when <c>state</c> no longer follows from the cached row values.</summary>
    let mutable full = false

    /// <summary>The count of rows with <c>HasValue</c> set.</summary>
    let mutable valued = 0

    let mutable failed = 0
    let mutable failure: FoldRow<'K, 'V> = Unchecked.defaultof<_>

    /// <summary>Set when <c>state</c> or the failed rows change.</summary>
    let mutable moved = false

    let mutable disposed = false
    let mutable link: OwnerLink = null
    let mutable keyReader: ProjectionReader<'K> = Unchecked.defaultof<_>

    /// <summary>The cursor of <c>keyReader</c>: the <c>Keys</c> of the last delta read from it, or null before the first read.</summary>
    let mutable readKeys: 'K[] = null

    /// <summary>The upstream <c>MembershipVersion</c> at the last read of <c>keyReader</c>.</summary>
    let mutable readVersion = 0

    let mutable memo: Memo<'S> = Unchecked.defaultof<Memo<'S>>

    let touch () =
        if not disposed then
            observers.NotifyCheck ()

    let reads = FoldReads (graph, touch)

    let touched (row: FoldRow<'K, 'V>) =
        if not row.Queued then
            row.Queued <- true

            if queued = queue.Length then
                let grown = Array.zeroCreate<FoldRow<'K, 'V>>(queued * 2)

                for i in 0 .. queued - 1 do
                    Platform.setItemAt i grown (Platform.itemAt i queue)

                queue <- grown

            Platform.setItemAt queued queue row
            queued <- queued + 1
            touch ()

    let clearQueue () =
        for i in 0 .. queued - 1 do
            Platform.setItemAt i queue Unchecked.defaultof<_>

        queued <- 0

    // Function objects built once by Attach. A let-bound lambda compiles to a method, and passing it as a value
    // allocates a closure at every use; a mutable field assigned once holds one object.
    let mutable touchedFn: FoldRow<'K, 'V> -> unit = Unchecked.defaultof<_>
#if FABLE_COMPILER
    let mutable applyAddFn: struct ('S * 'V) -> 'S = Unchecked.defaultof<_>
    let mutable applyReplaceFn: struct ('S * 'V * 'V) -> 'S = Unchecked.defaultof<_>
    let mutable applySubtractFn: struct ('S * 'V) -> 'S = Unchecked.defaultof<_>
#endif

    let foldAll () =
        let keys = lastKeys
        let mutable cached = orderedRows

        if Platform.isNull cached && not invertible then
            let n = keys.Length

            let arr =
                if n = 0 then
                    Array.empty
                else
                    Array.zeroCreate<FoldRow<'K, 'V>> n

            for i in 0 .. n - 1 do
                Platform.setItemAt i arr (rows.Find (Platform.itemAt i keys))

            cached <- arr
            orderedRows <- arr

        let mutable s = zero

        if not (Platform.isNull cached) then
            for i in 0 .. cached.Length - 1 do
                let row = Platform.itemAt i cached

                if not (Platform.isNull (box row)) && row.HasValue then
                    s <- invokeAdd s row.Value
        else
            for i in 0 .. keys.Length - 1 do
                let row = rows.Find (Platform.itemAt i keys)

                if not (Platform.isNull (box row)) && row.HasValue then
                    s <- invokeAdd s row.Value

        s

    member private this.Attach() =
        touchedFn <- touched
#if FABLE_COMPILER
        applyAddFn <- fun (struct (s, v)) -> invokeAdd s v
        applyReplaceFn <- fun (struct (s, old, v)) -> invokeAdd (invokeSub s old) v
        applySubtractFn <- fun (struct (s, old)) -> invokeSub s old
#endif
        link <- graph.CurrentOwner.AttachLinked this
        keyReader <- upstream.NewInternalKeyReader ()

    static member internal Create
        (graph: Graph, upstream: Projection<'K, 'V>, add: 'S -> 'V -> 'S, subtract: 'S -> 'V -> 'S, zero: 'S, invertible: bool)
        =
        let fold =
            new ProjectionFold<'K, 'V, 'S> (graph, upstream, add, subtract, zero, invertible)

        fold.Attach ()
        fold

    member _.Memo
        with get () = memo
        and set v = memo <- v

#if FABLE_COMPILER
    /// <summary>Applies <c>step arg</c> to the state, or marks the state for a full recompute when <c>step</c> raises.</summary>
    member private _.StepWith(step: 'A -> 'S, arg: 'A) =
        if not full then
            try
                state <- graph.RunHosted (reads :> IComputation, step, arg)
                moved <- true
            with _ ->
                full <- true

    member private this.StepAdd(v: 'V) =
        this.StepWith (applyAddFn, struct (state, v))

    member private this.StepSubtract(old: 'V) =
        this.StepWith (applySubtractFn, struct (state, old))

    member private this.StepReplace(old: 'V, v: 'V) =
        this.StepWith (applyReplaceFn, struct (state, old, v))
#else
    /// <summary>
    /// Applies <c>subtract old</c> when <c>subtracts</c>, then <c>add v</c> when <c>adds</c>, to the state, tracking their
    /// reads in <c>reads</c>. A raise or a pending read in either keeps the state and marks it for a full recompute.
    /// </summary>
    member private _.Step(old: 'V, v: 'V, subtracts: bool, adds: bool) =
        if not full then
            let saved = graph.EnterHosted (reads :> IComputation)

            try
                let mutable next = state

                if subtracts then
                    next <- subFn.Invoke (next, old)

                if adds then
                    next <- addFn.Invoke (next, v)

                if graph.RaisedPending then
                    graph.CheckRaised ()

                graph.LeaveHosted saved
                state <- next
                moved <- true
            with _ ->
                graph.LeaveHosted saved
                full <- true

    member private this.StepAdd(v: 'V) =
        this.Step (Unchecked.defaultof<'V>, v, false, true)

    member private this.StepSubtract(old: 'V) =
        this.Step (old, Unchecked.defaultof<'V>, true, false)

    member private this.StepReplace(old: 'V, v: 'V) =
        this.Step (old, v, true, true)
#endif

    member private this.Fail(row: FoldRow<'K, 'V>, ex: exn) =
        if Platform.isNull row.Error then
            failed <- failed + 1

            if Platform.isNull (box failure) then
                failure <- row

        if not (obj.ReferenceEquals (row.Error, ex)) then
            row.Error <- ex
            moved <- true

    member private _.Unfail(row: FoldRow<'K, 'V>) =
        if not (Platform.isNull row.Error) then
            row.Error <- null
            failed <- failed - 1
            moved <- true

            if obj.ReferenceEquals (failure, row) then
                failure <- Unchecked.defaultof<_>

    /// <summary>Moves the contribution of a row to its newly settled value <c>v</c>.</summary>
    member private this.ReadReady(row: FoldRow<'K, 'V>, v: 'V) =
        if not (Platform.isNull row.Error) then
            this.Unfail row

        if not row.HasValue then
            row.HasValue <- true
            row.Value <- v
            valued <- valued + 1

            if invertible then this.StepAdd v else full <- true
        else
            let changed =
                if equalIsSafe then
                    not (ComparerSentinel.equals equal row.Value v)
                else
                    this.ChangedGuarded (row, v)

            if changed then
                let old = row.Value
                row.Value <- v

                if invertible then
                    this.StepReplace (old, v)
                else
                    full <- true

    /// <summary>
    /// Whether <c>v</c> differs from the row's value under a comparer that may raise. A raise fails the row as a throwing
    /// row does, and reports the value unchanged.
    /// </summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ChangedGuarded(row: FoldRow<'K, 'V>, v: 'V) : bool =
        try
            not (ComparerSentinel.equals equal row.Value v)
        with ex ->
            this.Fail (row, ex)
            false

    /// <summary>Reads the row untracked and moves its contribution to the row's value.</summary>
    member private this.Read(row: FoldRow<'K, 'V>) =
#if FABLE_COMPILER
        this.ReadStale row
#else
        let source = row.Entry.Row

        // A clean, ready row outside a tracking context: the value the untracked read returns.
        if
            source.IsCleanAndReady
            && not (graph.IsTracking ())
        then
            row.Queued <- false
            this.ReadReady (row, source.Peek)
        else
            this.ReadStale row
#endif

    /// <summary><c>Read</c> for a row that is stale, pending or failed, or read inside a tracking context.</summary>
    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ReadStale(row: FoldRow<'K, 'V>) =
#if FABLE_COMPILER
        let reading = row.Entry.Row.TryValueUntracked ()
        row.Queued <- false

        match reading with
        | Ready v -> this.ReadReady (row, v)
        | Pending -> this.Unfail row
        | Failed ex -> this.Fail (row, ex)
#else
        let struct (status, v, error) = row.Entry.Row.TryReadUntrackedStruct ()
        row.Queued <- false

        if status = Status.None then
            this.ReadReady (row, v)
        elif status = Status.Pending then
            this.Unfail row
        else
            this.Fail (row, error)
#endif

    /// <summary>Stops observing the row and removes its contribution.</summary>
    member private this.Detach(row: FoldRow<'K, 'V>) =
        row.Attached <- false
        (row.Entry.Row :> ISource).RemoveObserverAt(row :> IComputation, row.Handle)
        row.Handle <- EdgeHandle.Unlinked
        this.Unfail row

        if row.HasValue then
            row.HasValue <- false
            let old = row.Value
            row.Value <- Unchecked.defaultof<'V>
            valued <- valued - 1

            if not invertible then
                full <- true
            elif valued = 0 && not full then
                state <- zero
                moved <- true
            else
                this.StepSubtract old

    /// <summary>Attaches a row to every added key and detaches the row of every removed key. O(N) in <c>keys</c>.</summary>
    member private this.Diff(keys: 'K[]) =
        generation <- generation + 1
        added.Clear ()
        removed.Clear ()

        for i in 0 .. keys.Length - 1 do
            let key = Platform.itemAt i keys
            let entry = upstream.Entries.Find key

            if not (Platform.isNull entry) then
                let row = rows.Find key

                if
                    not (Platform.isNull (box row))
                    && obj.ReferenceEquals (row.Entry, entry)
                then
                    row.Generation <- generation
                else
                    if not (Platform.isNull (box row)) then
                        this.Detach row

                    let fresh = FoldRow<'K, 'V>(graph, entry, touchedFn)
                    fresh.Generation <- generation
                    rows.Set (key, fresh)
                    added.Add fresh

        rows.Iterate (fun key row ->
            if row.Generation <> generation then
                removed.Add key)

        for i in 0 .. removed.Count - 1 do
            let key = Platform.entryAt i removed
            this.Detach (rows.Find key)
            rows.Remove key

        orderedRows <- null
        lastKeys <- keys

        for i in 0 .. added.Count - 1 do
            let row = Platform.entryAt i added
            row.Queued <- true
            row.Attached <- true
            row.Handle <- (row.Entry.Row :> ISource).AddObserver(row :> IComputation, 0)
            this.Read row

        added.Clear ()
        removed.Clear ()

    /// <summary>Applies coalesced membership changes from <c>delta</c> in O(Δ).</summary>
    member private this.ApplyDelta(delta: ProjectionDelta<'K>) =
        for i in 0 .. delta.ChangeCount - 1 do
            let change = delta.ChangeAt i

            if
                change.Value = KeyChange.Removed
                || change.Value = KeyChange.Replaced
            then
                let old = rows.Find change.Key

                if not (Platform.isNull (box old)) then
                    this.Detach old
                    rows.Remove change.Key

        added.Clear ()

        for i in 0 .. delta.ChangeCount - 1 do
            let change = delta.ChangeAt i

            if
                change.Value = KeyChange.Added
                || change.Value = KeyChange.Replaced
            then
                let entry = upstream.Entries.Find change.Key

                if not (Platform.isNull entry) then
                    let old = rows.Find change.Key

                    if not (Platform.isNull (box old)) then
                        this.Detach old

                    let fresh = FoldRow<'K, 'V>(graph, entry, touchedFn)
                    rows.Set (change.Key, fresh)
                    added.Add fresh

        orderedRows <- null
        lastKeys <- delta.Keys

        for i in 0 .. added.Count - 1 do
            let row = Platform.entryAt i added
            row.Queued <- true
            row.Attached <- true
            row.Handle <- (row.Entry.Row :> ISource).AddObserver(row :> IComputation, 0)
            this.Read row

        added.Clear ()

    /// <summary>Folds the cached row values in <c>Keys</c> order into a fresh state. O(N).</summary>
    member private _.Recompute() =
        reads.Clear ()
        state <- graph.RunHosted (reads :> IComputation, foldAll)
        full <- false
        moved <- true

    /// <summary>
    /// Brings the state current: the membership and order of <c>delta</c> when non-null, then every queued row, then a full
    /// recompute if a read of the fold functions moved or the fold is non-invertible.
    /// </summary>
    member private this.Sync(delta: ProjectionDelta<'K>) =
        if reads.IsPendingCheckOrDirty && reads.Moved () then
            full <- true

        if not (Platform.isNull (box delta)) then
            let hadKeys = not (Platform.isNull lastKeys)

            if delta.IsReset then
                this.Diff delta.Keys
            else if delta.ChangeCount > 0 then
                this.ApplyDelta delta
            else
                if delta.OrderChanged then
                    orderedRows <- null

                lastKeys <- delta.Keys

            if not invertible && hadKeys && delta.OrderChanged then
                full <- true

        let mutable i = 0

        while i < queued do
            let row = Platform.itemAt i queue

            if row.Attached && row.Entry.Live then
                this.Read row
            else
                row.Queued <- false

            i <- i + 1

        i <- 0

        while i < queued do
            Platform.setItemAt i queue Unchecked.defaultof<_>
            i <- i + 1

        queued <- 0

        if full then
            this.Recompute ()

    /// <summary>Raises the exception of the first failed row, with the row's origin when the row still holds it.</summary>
    member private _.RaiseFailure() =
        if Platform.isNull (box failure) then
            rows.Iterate (fun _ row ->
                if
                    Platform.isNull (box failure)
                    && not (Platform.isNull row.Error)
                then
                    failure <- row)

        let recorded = failure.Entry.Row.Failure

        if
            not (Platform.isNull recorded)
            && obj.ReferenceEquals (recorded.Error, failure.Error)
        then
            graph.Raise recorded
        else
            raise failure.Error

    /// <summary>The memo's body: tracks the upstream key delta and this node, then returns the current state.</summary>
    member this.Compute(_: 'S voption) : 'S =
        if disposed then
            state
        else
            // At the keys and membership version of the previous key read, a read of the key reader makes the tracked reads
            // of Keys and returns an empty delta.
            if upstream.TrackKeysAt (readKeys, readVersion) then
                graph.Track (this :> ISource)

                if not (obj.ReferenceEquals (lastKeys, readKeys)) then
                    lastKeys <- readKeys

                if queued <> 0 || full || reads.IsPendingCheckOrDirty then
                    this.Sync Unchecked.defaultof<_>
            else
                let delta = keyReader.Read ()
                readKeys <- delta.Keys
                readVersion <- upstream.MembershipVersion
                graph.Track (this :> ISource)

                // An unchanged membership with no queued row, pending read or recompute leaves Sync only the key array to
                // keep, as after the Sync of UpdateIfNecessary.
                if
                    queued = 0
                    && not full
                    && not reads.IsPendingCheckOrDirty
                    && not (Platform.isNull (box delta))
                    && not delta.IsReset
                    && delta.ChangeCount = 0
                    && not delta.OrderChanged
                then
                    lastKeys <- delta.Keys
                else
                    this.Sync delta

            moved <- false

            if failed > 0 then
                this.RaiseFailure ()

            state

    member this.Dispose() =
        if not disposed then
            disposed <- true
            orderedRows <- null

            if not (Platform.isNull link) then
                link.Detach ()
                link <- null

            if not (Platform.isNull (box keyReader)) then
                keyReader.Stop ()
                keyReader <- Unchecked.defaultof<_>

            rows.Iterate (fun _ row ->
                row.Attached <- false
                (row.Entry.Row :> ISource).RemoveObserverAt(row :> IComputation, row.Handle)
                row.Handle <- EdgeHandle.Unlinked)

            rows.Clear ()
            clearQueue ()
            reads.Clear ()

            if not (Platform.isNull (box memo)) then
                memo.Dispose ()

    interface INode with
        member _.Id = id
        member _.Status = Status.None

    interface ISource with
        member _.AddObserver(c, slot) =
            observers.Add (c, slot)

        member _.TryAddObserver(c, slot) =
            observers.TryAdd (c, slot)

        member _.RemoveObserverAt(c, handle) =
            observers.RemoveAt (c, handle)

        member this.UpdateIfNecessary() =
            if not disposed && not (Platform.isNull lastKeys) then
                moved <- false

                try
                    this.Sync Unchecked.defaultof<_>
                with _ ->
                    moved <- true

                if moved then
                    observers.NotifyDirty ()

        member _.IsStale = false

        member _.IsActive = false

    interface IOwned with
        member this.Release() =
            link <- null
            this.Dispose ()

    interface System.IDisposable with
        member this.Dispose() =
            this.Dispose ()

/// <summary>
/// One live key of a tournament extreme aggregate: the observer of the key's rank/value row and its cached state.
/// </summary>
[<Sealed>]
type internal ExtremeRow<'K, 'V, 'S>(graph: Graph, key: 'K, entry: RowEntry<'K, struct ('S * 'V)>, touched: ExtremeRow<'K, 'V, 'S> -> unit) =
    let id = graph.NextId ()

    member _.Key = key
    member _.Entry = entry

    /// <summary>Leaf slot index in the containing tournament tree, or -1 while unplaced.</summary>
    member val Slot = -1 with get, set

    /// <summary>Whether the row waits in the aggregate's queue, or is being read.</summary>
    member val Queued = false with get, set

    /// <summary>Whether the aggregate observes the row.</summary>
    member val Attached = false with get, set

    /// <summary>Whether <c>Rank</c> and <c>Value</c> hold a settled pair present in the tournament tree.</summary>
    member val HasValue = false with get, set

    member val Rank = Unchecked.defaultof<'S> with get, set
    member val Value = Unchecked.defaultof<'V> with get, set

    /// <summary>The row's exception while the row is failed, or null.</summary>
    member val Error: exn = null with get, set

    /// <summary>The last membership diff containing the key.</summary>
    member val Generation = 0 with get, set

    member val Handle = EdgeHandle.Unlinked with get, set

    interface INode with
        member _.Id = id
        member _.Status = Status.None

    interface IComputation with
        member _.AddSource _ = ()

        member this.MovedIn(_, handle) =
            this.Handle <- handle

        member _.HasReadSlot _ = false

        member this.MarkDirty() =
            touched this

        member this.MarkCheck() =
            touched this

/// <summary>
/// The extreme value of <c>upstream</c> ordered by rank, maintained in O(log N) comparisons across row edits,
/// removals and tail appends via a binary tournament winner tree with a sticky holder across ties.
/// </summary>
[<Sealed>]
type internal ProjectionExtreme<'K, 'V, 'S, 'R when 'K: equality and 'S: comparison>
    (
        graph: Graph,
        upstream: Projection<'K, struct ('S * 'V)>,
        isMax: bool,
        encodedEqualValues: System.Collections.Generic.IEqualityComparer<'V>,
        finish: 'V option -> 'R
    ) =
    let id = graph.NextId ()
    let observers = ObserverSet ()
#if !FABLE_COMPILER
    let floatRanks =
        typeof<'S>.IsAssignableFrom typeof<float>
        || typeof<'S>.IsAssignableFrom typeof<float32>

    let rankComparer = LanguagePrimitives.FastGenericComparer<'S>
#endif

    let rows = Platform.KeyMap<'K, ExtremeRow<'K, 'V, 'S>>()
    let queue = ResizeArray<ExtremeRow<'K, 'V, 'S>>()
    let added = ResizeArray<ExtremeRow<'K, 'V, 'S>>()
    let removed = ResizeArray<'K>()

    let mutable lastKeys: 'K[] = null
    let mutable generation = 0

    let mutable cap = 0
    let mutable slotCount = 0
    let mutable slots: ExtremeRow<'K, 'V, 'S>[] = Array.empty
    let mutable tree: ExtremeRow<'K, 'V, 'S>[] = Array.empty
    let mutable dirtyTree = true

    let mutable stickyHolder: ExtremeRow<'K, 'V, 'S> = Unchecked.defaultof<_>
    let mutable hasStickyKey = false
    let mutable stickyKey = Unchecked.defaultof<'K>
    let mutable hasPublished = false
    let mutable publishedValue = Unchecked.defaultof<'V>
    let mutable lastResult = Unchecked.defaultof<'R>
    let mutable lastOutcomeFailed = false

    let mutable valued = 0
    let mutable failed = 0
    let mutable failure: ExtremeRow<'K, 'V, 'S> = Unchecked.defaultof<_>
    let mutable moved = false

    let mutable disposed = false
    let mutable link: OwnerLink = null
    let mutable keyReader: ProjectionReader<'K> = Unchecked.defaultof<_>

    let touch () =
        if not disposed then
            observers.NotifyCheck ()

    let touched (row: ExtremeRow<'K, 'V, 'S>) =
        if not row.Queued then
            row.Queued <- true
            queue.Add row
            touch ()

#if FABLE_COMPILER
    let compareAsc (a: 'S) (b: 'S) =
        Ranks.ascending a b
#else
    let compareAsc (a: 'S) (b: 'S) =
        if floatRanks then
            Ranks.ascending a b
        else
            rankComparer.Compare (a, b)
#endif

    let compareExtreme (a: 'S) (b: 'S) =
        if isMax then compareAsc b a else compareAsc a b

    member private this.Attach() =
        link <- graph.CurrentOwner.AttachLinked this
        keyReader <- upstream.NewInternalKeyReader ()

    static member internal Create
        (
            graph: Graph,
            upstream: Projection<'K, struct ('S * 'V)>,
            isMax: bool,
            encodedEqualValues: System.Collections.Generic.IEqualityComparer<'V>,
            finish: 'V option -> 'R
        ) =
        let extreme =
            new ProjectionExtreme<'K, 'V, 'S, 'R> (graph, upstream, isMax, encodedEqualValues, finish)

        extreme.Attach ()
        extreme

    /// <summary>Selects the better of two optional settled leaf rows, favouring <c>left</c> on equal ranks.</summary>
    member private _.PickWinner(left: ExtremeRow<'K, 'V, 'S>, right: ExtremeRow<'K, 'V, 'S>) =
        if Platform.isNull (box left) then right
        elif Platform.isNull (box right) then left
        elif compareExtreme left.Rank right.Rank <= 0 then left
        else right

    /// <summary>Updates tournament internal winners along the ancestor path of <c>slot</c>. O(log N).</summary>
    member private this.SiftUp(slot: int) =
        let mutable idx = cap + slot
        let mutable keepGoing = true

        while idx > 1 && keepGoing do
            let parent = idx >>> 1
            let oldW = Platform.itemAt parent tree

            let nextW =
                this.PickWinner (Platform.itemAt (parent <<< 1) tree, Platform.itemAt ((parent <<< 1) ||| 1) tree)

            if obj.ReferenceEquals (oldW, nextW) then
                if
                    Platform.isNull (box nextW)
                    || not (obj.ReferenceEquals (oldW, Platform.itemAt idx tree))
                then
                    keepGoing <- false
                else
                    idx <- parent
            else
                Platform.setItemAt parent tree nextW
                idx <- parent

    /// <summary>Doubles leaf capacity with zero comparisons by shifting each tree level down by one bit.</summary>
    member private _.DoubleCap() =
        let oldCap = cap
        let newCap = oldCap <<< 1
        let newSlots = Array.zeroCreate<ExtremeRow<'K, 'V, 'S>> newCap

        for i in 0 .. slotCount - 1 do
            Platform.setItemAt i newSlots (Platform.itemAt i slots)

        let newTree = Array.zeroCreate<ExtremeRow<'K, 'V, 'S>>(newCap <<< 1)
        Platform.setItemAt 1 newTree (Platform.itemAt 1 tree)

        let mutable len = 1

        while len <= oldCap do
            let dstBase = len <<< 1

            for j in 0 .. len - 1 do
                Platform.setItemAt (dstBase + j) newTree (Platform.itemAt (len + j) tree)

            len <- len <<< 1

        slots <- newSlots
        tree <- newTree
        cap <- newCap

    /// <summary>Ensures leaf capacity can hold at least <c>target</c> slots.</summary>
    member private this.EnsureCap(target: int) =
        if cap = 0 then
            cap <- 1
            slots <- Array.zeroCreate<ExtremeRow<'K, 'V, 'S>> 1
            tree <- Array.zeroCreate<ExtremeRow<'K, 'V, 'S>> 2

        while cap < target do
            this.DoubleCap ()

    /// <summary>Assigns compact <c>Keys</c>-order slots and builds the tournament tree bottom-up. O(N).</summary>
    member private this.RebuildTree(keys: 'K[]) =
        let mutable targetCap = 1

        while targetCap < keys.Length do
            targetCap <- targetCap <<< 1

        if cap = targetCap && slots.Length = targetCap then
            for i in 0 .. cap - 1 do
                Platform.setItemAt i slots Unchecked.defaultof<_>

            for i in 0 .. (cap <<< 1) - 1 do
                Platform.setItemAt i tree Unchecked.defaultof<_>
        else
            cap <- targetCap
            slots <- Array.zeroCreate<ExtremeRow<'K, 'V, 'S>> targetCap
            tree <- Array.zeroCreate<ExtremeRow<'K, 'V, 'S>>(targetCap <<< 1)

        slotCount <- keys.Length

        for i in 0 .. keys.Length - 1 do
            let row = rows.Find (Platform.itemAt i keys)

            if not (Platform.isNull (box row)) then
                row.Slot <- i
                Platform.setItemAt i slots row

                if row.HasValue then
                    Platform.setItemAt (cap + i) tree row

        for idx = cap - 1 downto 1 do
            Platform.setItemAt idx tree (this.PickWinner (Platform.itemAt (idx <<< 1) tree, Platform.itemAt ((idx <<< 1) ||| 1) tree))

        dirtyTree <- false

    member private _.Fail(row: ExtremeRow<'K, 'V, 'S>, ex: exn) =
        if Platform.isNull row.Error then
            failed <- failed + 1

            if Platform.isNull (box failure) then
                failure <- row

        if not (obj.ReferenceEquals (row.Error, ex)) then
            row.Error <- ex

    member private _.Unfail(row: ExtremeRow<'K, 'V, 'S>) =
        if not (Platform.isNull row.Error) then
            row.Error <- null
            failed <- failed - 1

            if obj.ReferenceEquals (failure, row) then
                failure <- Unchecked.defaultof<_>

    /// <summary>Reads the row untracked and updates its leaf in the tournament tree.</summary>
    member private this.Read(row: ExtremeRow<'K, 'V, 'S>) =
        let reading = row.Entry.Row.TryValueUntracked ()
        row.Queued <- false

        match reading with
        | Ready (struct (rank, value)) ->
            this.Unfail row

            if not row.HasValue then
                row.HasValue <- true
                row.Rank <- rank
                row.Value <- value
                valued <- valued + 1

                if not dirtyTree && row.Slot >= 0 then
                    Platform.setItemAt (cap + row.Slot) tree row

                    try
                        this.SiftUp row.Slot
                    with _ ->
                        dirtyTree <- true
            else
                let mutable sameRank = false
                let mutable cmpErr: exn = null

                try
                    sameRank <- compareExtreme row.Rank rank = 0
                with ex ->
                    cmpErr <- ex

                if not (Platform.isNull cmpErr) then
                    this.Fail (row, cmpErr)
                else
                    row.Rank <- rank
                    row.Value <- value

                    if not sameRank && not dirtyTree && row.Slot >= 0 then
                        try
                            this.SiftUp row.Slot
                        with _ ->
                            dirtyTree <- true
        | Pending -> this.Unfail row
        | Failed ex -> this.Fail (row, ex)

    /// <summary>Stops observing the row and clears its tournament leaf.</summary>
    member private this.Detach(row: ExtremeRow<'K, 'V, 'S>) =
        row.Attached <- false
        (row.Entry.Row :> ISource).RemoveObserverAt(row :> IComputation, row.Handle)
        row.Handle <- EdgeHandle.Unlinked
        this.Unfail row
        let oldSlot = row.Slot
        row.Slot <- -1

        if
            oldSlot >= 0
            && oldSlot < slots.Length
            && obj.ReferenceEquals (Platform.itemAt oldSlot slots, row)
        then
            Platform.setItemAt oldSlot slots Unchecked.defaultof<_>

        if row.HasValue then
            row.HasValue <- false
            row.Rank <- Unchecked.defaultof<'S>
            row.Value <- Unchecked.defaultof<'V>
            valued <- valued - 1

            if not dirtyTree && oldSlot >= 0 then
                Platform.setItemAt (cap + oldSlot) tree Unchecked.defaultof<_>

                try
                    this.SiftUp oldSlot
                with _ ->
                    dirtyTree <- true

    /// <summary>Attaches a row to every added key and detaches the row of every removed key. O(N) in <c>keys</c>.</summary>
    member private this.Diff(keys: 'K[]) =
        dirtyTree <- true
        generation <- generation + 1
        added.Clear ()
        removed.Clear ()

        for i in 0 .. keys.Length - 1 do
            let key = Platform.itemAt i keys
            let entry = upstream.Entries.Find key

            if not (Platform.isNull entry) then
                let row = rows.Find key

                if
                    not (Platform.isNull (box row))
                    && obj.ReferenceEquals (row.Entry, entry)
                then
                    row.Generation <- generation
                else
                    if not (Platform.isNull (box row)) then
                        this.Detach row

                    let fresh = ExtremeRow<'K, 'V, 'S>(graph, key, entry, touched)
                    fresh.Generation <- generation
                    rows.Set (key, fresh)
                    added.Add fresh

        rows.Iterate (fun key row ->
            if row.Generation <> generation then
                removed.Add key)

        for i in 0 .. removed.Count - 1 do
            let key = Platform.entryAt i removed
            this.Detach (rows.Find key)
            rows.Remove key

        lastKeys <- keys

        for i in 0 .. added.Count - 1 do
            let row = Platform.entryAt i added
            row.Queued <- true
            row.Attached <- true
            row.Handle <- (row.Entry.Row :> ISource).AddObserver(row :> IComputation, 0)
            this.Read row

        added.Clear ()
        removed.Clear ()

    /// <summary>Checks whether non-null slots in <c>slots</c> match <c>keys.[0 .. expectedPlaced - 1]</c>.</summary>
    member private _.SlotsMatchPrefix(keys: 'K[], expectedPlaced: int) =
        if expectedPlaced < 0 || expectedPlaced > keys.Length then
            false
        else
            let mutable k = 0
            let mutable s = 0
            let mutable ok = true

            while s < slotCount && ok do
                let row = Platform.itemAt s slots

                if not (Platform.isNull (box row)) then
                    if
                        k < expectedPlaced
                        && Platform.keyEquals row.Key (Platform.itemAt k keys)
                    then
                        k <- k + 1
                    else
                        ok <- false

                s <- s + 1

            ok && k = expectedPlaced

    /// <summary>Applies coalesced membership changes from <c>delta</c>, preserving slots on removals, replacements and tail appends.</summary>
    member private this.ApplyDelta(delta: ProjectionDelta<'K>) =
        added.Clear ()
        let mutable unplaced = 0

        for i in 0 .. delta.ChangeCount - 1 do
            let change = delta.ChangeAt i

            if change.Value = KeyChange.Removed then
                let old = rows.Find change.Key

                if not (Platform.isNull (box old)) then
                    this.Detach old
                    rows.Remove change.Key
            elif change.Value = KeyChange.Replaced then
                let old = rows.Find change.Key
                let reuseSlot = if not (Platform.isNull (box old)) then old.Slot else -1

                if not (Platform.isNull (box old)) then
                    this.Detach old
                    rows.Remove change.Key

                let entry = upstream.Entries.Find change.Key

                if not (Platform.isNull entry) then
                    let fresh = ExtremeRow<'K, 'V, 'S>(graph, change.Key, entry, touched)

                    if reuseSlot >= 0 && not dirtyTree then
                        fresh.Slot <- reuseSlot
                        Platform.setItemAt reuseSlot slots fresh
                    else
                        unplaced <- unplaced + 1

                    rows.Set (change.Key, fresh)
                    added.Add fresh

        for i in 0 .. delta.ChangeCount - 1 do
            let change = delta.ChangeAt i

            if change.Value = KeyChange.Added then
                let entry = upstream.Entries.Find change.Key

                if not (Platform.isNull entry) then
                    let old = rows.Find change.Key

                    if not (Platform.isNull (box old)) then
                        this.Detach old

                    let fresh = ExtremeRow<'K, 'V, 'S>(graph, change.Key, entry, touched)
                    rows.Set (change.Key, fresh)
                    added.Add fresh
                    unplaced <- unplaced + 1

        let keys = delta.Keys

        if delta.OrderChanged || added.Count > 0 then
            let placedCount = keys.Length - unplaced

            if
                dirtyTree
                || (slotCount > 64 && keys.Length * 4 < slotCount)
                || not (this.SlotsMatchPrefix (keys, placedCount))
            then
                dirtyTree <- true
            elif keys.Length = 0 then
                slotCount <- 0
            elif unplaced > 0 then
                this.EnsureCap (slotCount + unplaced)
                let mutable idx = placedCount

                while idx < keys.Length && not dirtyTree do
                    let r = rows.Find (Platform.itemAt idx keys)

                    if not (Platform.isNull (box r)) && r.Slot < 0 then
                        r.Slot <- slotCount
                        Platform.setItemAt slotCount slots r
                        slotCount <- slotCount + 1
                    else
                        dirtyTree <- true

                    idx <- idx + 1

        lastKeys <- keys

        for i in 0 .. added.Count - 1 do
            let row = Platform.entryAt i added
            row.Queued <- true
            row.Attached <- true
            row.Handle <- (row.Entry.Row :> ISource).AddObserver(row :> IComputation, 0)
            this.Read row

        added.Clear ()

    /// <summary>Brings membership, queued rows, the tournament tree and <c>stickyHolder</c> current.</summary>
    member private this.Sync(delta: ProjectionDelta<'K>) =
        if not (Platform.isNull (box delta)) then
            if delta.IsReset then
                this.Diff delta.Keys
            elif delta.ChangeCount > 0 then
                this.ApplyDelta delta
            elif delta.OrderChanged then
                lastKeys <- delta.Keys

                if
                    not dirtyTree
                    && not (this.SlotsMatchPrefix (delta.Keys, delta.Keys.Length))
                then
                    dirtyTree <- true
            else
                lastKeys <- delta.Keys

        let mutable i = 0

        while i < queue.Count do
            let row = Platform.entryAt i queue

            if row.Attached && row.Entry.Live then
                this.Read row
            else
                row.Queued <- false

            i <- i + 1

        queue.Clear ()

        if dirtyTree then
            this.RebuildTree lastKeys

        if failed = 0 then
            let top =
                if cap > 0 then
                    Platform.itemAt 1 tree
                else
                    Unchecked.defaultof<_>

            if Platform.isNull (box top) then
                stickyHolder <- Unchecked.defaultof<_>
                hasStickyKey <- false
                stickyKey <- Unchecked.defaultof<'K>
            else
                let candidate =
                    if
                        not (Platform.isNull (box stickyHolder))
                        && stickyHolder.Attached
                    then
                        stickyHolder
                    elif hasStickyKey then
                        rows.Find stickyKey
                    else
                        Unchecked.defaultof<_>

                let chosen =
                    if
                        not (Platform.isNull (box candidate))
                        && candidate.Attached
                        && candidate.HasValue
                        && (obj.ReferenceEquals (candidate, top)
                            || compareExtreme candidate.Rank top.Rank <= 0)
                    then
                        candidate
                    else
                        top

                stickyHolder <- chosen
                hasStickyKey <- true
                stickyKey <- chosen.Key

    member private _.CurrentFailureRow() =
        if Platform.isNull (box failure) then
            rows.Iterate (fun _ row ->
                if
                    Platform.isNull (box failure)
                    && not (Platform.isNull row.Error)
                then
                    failure <- row)

        failure

    /// <summary>Raises the exception of the first failed row, with the row's origin when the row still holds it.</summary>
    member private this.RaiseFailure() =
        let row = this.CurrentFailureRow ()
        let recorded = row.Entry.Row.Failure

        if
            not (Platform.isNull recorded)
            && obj.ReferenceEquals (recorded.Error, row.Error)
        then
            graph.Raise recorded
        else
            raise row.Error

    /// <summary>The memo's body: tracks the upstream key delta and this node, then returns the finished extreme value.</summary>
    member this.Compute(_: 'R voption) : 'R =
        if disposed then
            lastResult
        else
            let delta = keyReader.Read ()
            graph.Track (this :> ISource)
            this.Sync delta
            moved <- false

            if failed > 0 then
                lastOutcomeFailed <- true
                this.RaiseFailure ()

            lastOutcomeFailed <- false

            if Platform.isNull (box stickyHolder) then
                hasPublished <- false
                publishedValue <- Unchecked.defaultof<'V>
                lastResult <- Unchecked.defaultof<'R>
                finish None
            elif
                hasPublished
                && ComparerSentinel.equals encodedEqualValues stickyHolder.Value publishedValue
            then
                lastResult
            else
                let current = finish (Some stickyHolder.Value)
                hasPublished <- true
                publishedValue <- stickyHolder.Value
                lastResult <- current
                current

    member this.Dispose() =
        if not disposed then
            disposed <- true

            if not (Platform.isNull link) then
                link.Detach ()
                link <- null

            if not (Platform.isNull (box keyReader)) then
                keyReader.Stop ()
                keyReader <- Unchecked.defaultof<_>

            rows.Iterate (fun _ row ->
                row.Attached <- false
                row.Slot <- -1
                (row.Entry.Row :> ISource).RemoveObserverAt(row :> IComputation, row.Handle)
                row.Handle <- EdgeHandle.Unlinked)

            rows.Clear ()
            queue.Clear ()
            slots <- Array.empty
            tree <- Array.empty
            stickyHolder <- Unchecked.defaultof<_>

    interface INode with
        member _.Id = id
        member _.Status = Status.None

    interface ISource with
        member _.AddObserver(c, slot) =
            observers.Add (c, slot)

        member _.TryAddObserver(c, slot) =
            observers.TryAdd (c, slot)

        member _.RemoveObserverAt(c, handle) =
            observers.RemoveAt (c, handle)

        member this.UpdateIfNecessary() =
            if not disposed && not (Platform.isNull lastKeys) then
                moved <- false
                let prevFailed = failed

                let prevErr =
                    if prevFailed > 0 then
                        (this.CurrentFailureRow ()).Error
                    else
                        null

                try
                    this.Sync Unchecked.defaultof<_>

                    if failed > 0 then
                        let curErr = (this.CurrentFailureRow ()).Error

                        if
                            prevFailed = 0
                            || not lastOutcomeFailed
                            || not (obj.ReferenceEquals (prevErr, curErr))
                        then
                            moved <- true
                    elif prevFailed > 0 || lastOutcomeFailed then
                        moved <- true
                    elif Platform.isNull (box stickyHolder) then
                        if hasPublished then
                            moved <- true
                    elif not hasPublished then
                        moved <- true
                    elif not (ComparerSentinel.equals encodedEqualValues stickyHolder.Value publishedValue) then
                        moved <- true
                with _ ->
                    moved <- true

                if moved then
                    observers.NotifyDirty ()

        member _.IsStale = false

        member _.IsActive = false

    interface IOwned with
        member this.Release() =
            link <- null
            this.Dispose ()

    interface System.IDisposable with
        member this.Dispose() =
            this.Dispose ()

/// <summary>Views over a <c>Projection</c> that re-run the user function only for keys whose upstream row changed.</summary>
/// <remarks>
/// A view is a <c>Projection</c> owned by the scope that creates it. A membership or order change costs O(N) per view, and O(window) for <c>take</c>, <c>skip</c> and <c>sub</c>.
/// A pending key a view holds out of <c>Keys</c> is in the <c>PendingKeys</c> of every view built on that view. A key excluded on failure
/// is absent from those views, and only <c>Get</c> and <c>TryGet</c> of the excluding view raise its error.
/// </remarks>
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Projection =
    /// <summary>Raises the exception a projection pass raises for the first key of <c>items</c> that repeats an earlier key.</summary>
    /// <remarks>Keys are compared as a projection compares them: <c>'K: equality</c>, hashed.</remarks>
    let ensureUniqueKeys (keyOf: 'T -> 'K) (items: 'T seq) : unit =
        let seen = Platform.KeySet<'K>()

        for item in items do
            let key = keyOf item

            if not (seen.Add key) then
                raise (ProjectionFailures.duplicateKey key)

    /// <summary>An owner parented to the calling scope, for the nodes built by a composed memo.</summary>
    let private ownedScope (graph: Graph) =
        let parent = graph.CurrentOwner
        let owner = new Owner (graph.Root)
        owner.SetParent (parent.AttachLinked owner)
        owner

    /// <summary>A projection over the keys of <c>upstream</c>, each row holding <c>f</c> of the upstream value.</summary>
    let private perKey (graph: Graph) (upstream: Projection<'K, 'V>) (f: 'V -> 'X) : Projection<'K, 'X> =
        new MapView<'K, 'V, 'X> (graph, upstream, Unchecked.defaultof<_>, f, Unchecked.defaultof<_>, false) :> Projection<'K, 'X>

    let inline private raiseKeyNotFound (empty: exn ref) (message: string) =
        if isNull empty.Value then
            empty.Value <- System.Collections.Generic.KeyNotFoundException message :> exn

        raise empty.Value

    /// <summary>
    /// A function returning the value inside <c>Some</c>, and raising one <c>KeyNotFoundException</c> instance with
    /// <c>message</c> for every <c>None</c>.
    /// </summary>
    let inline private orRaise (message: string) : 'T option -> 'T =
        let empty = ref (null: exn)

        fun result ->
            match result with
            | Some value -> value
            | None -> raiseKeyNotFound empty message

    let inline private boxPair (entry: struct ('K * 'V) voption) : ('K * 'V) option =
        match entry with
        | ValueSome (struct (k, v)) -> Some (k, v)
        | ValueNone -> None

    let inline private boxSnd (entry: struct ('K * 'V) voption) : 'V option =
        match entry with
        | ValueSome (struct (_, v)) -> Some v
        | ValueNone -> None

    let inline private boxFst (entry: struct ('K * 'V) voption) : 'K option =
        match entry with
        | ValueSome (struct (k, _)) -> Some k
        | ValueNone -> None

    let inline private pairOrRaise (message: string) : struct ('K * 'V) voption -> 'K * 'V =
        let empty = ref (null: exn)

        fun entry ->
            match entry with
            | ValueSome (struct (k, v)) -> (k, v)
            | ValueNone -> raiseKeyNotFound empty message

    let inline private sndOrRaise (message: string) : struct ('K * 'V) voption -> 'V =
        let empty = ref (null: exn)

        fun entry ->
            match entry with
            | ValueSome (struct (_, v)) -> v
            | ValueNone -> raiseKeyNotFound empty message

    let inline private fstOrRaise (message: string) : struct ('K * 'V) voption -> 'K =
        let empty = ref (null: exn)

        fun entry ->
            match entry with
            | ValueSome (struct (k, _)) -> k
            | ValueNone -> raiseKeyNotFound empty message

    /// <summary>
    /// The key at position <c>position count</c> of <c>upstream</c>, where <c>count</c> is the key count, with its value;
    /// <c>ValueNone</c> for a position outside <c>Keys</c>.
    /// </summary>
    /// <remarks>Tracked reads of <c>Keys</c>, of what <c>position</c> reads, and of the row at the position.</remarks>
    let private entryAt (upstream: Projection<'K, 'V>) (position: int -> int) : struct ('K * 'V) voption =
        let keys = upstream.Keys
        let index = position keys.Length

        if index < 0 || index >= keys.Length then
            ValueNone
        else
            let key = Platform.itemAt index keys
            ValueSome (struct (key, upstream.Get key))

    /// <summary>A memo of <c>finish</c> applied to <c>entryAt upstream position</c>.</summary>
    /// <remarks>
    /// Readers re-run only when the entry changes: an equal entry returns the last result instance.
    /// </remarks>
    let private positional (upstream: Projection<'K, 'V>) (position: int -> int) (finish: struct ('K * 'V) voption -> 'R) : Memo<'R> =
        let equalValues = upstream.Graph.EncodedComparer<'V>()
        let mutable hasLast = false
        let mutable lastKey = Unchecked.defaultof<'K>
        let mutable lastValue = Unchecked.defaultof<'V>
        let mutable lastResult = Unchecked.defaultof<'R>

        let compute (_: 'R voption) =
            let entry = entryAt upstream position

            match entry with
            | ValueSome (struct (key, value)) ->
                if
                    hasLast
                    && Platform.keyEquals key lastKey
                    && ComparerSentinel.equals equalValues value lastValue
                then
                    lastResult
                else
                    let result = finish entry
                    hasLast <- true
                    lastKey <- key
                    lastValue <- value
                    lastResult <- result
                    result
            | ValueNone ->
                hasLast <- false
                lastKey <- Unchecked.defaultof<'K>
                lastValue <- Unchecked.defaultof<'V>
                lastResult <- Unchecked.defaultof<'R>
                finish ValueNone

        Memo<'R>.Create(upstream.Graph, compute, ScopeMode.Pure)

    /// <summary>The keys of <c>upstream</c> whose value satisfies <c>predicate</c>, in upstream order, with their values.</summary>
    /// <remarks>
    /// <c>predicate</c> re-runs only for keys whose upstream row changed, and unchanged passes cut off per row
    /// (<see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#row_slot_check_cutoff_skips_pass">row_slot_check_cutoff_skips_pass</see>).
    /// Sparse membership flips compute output positions in O(log N) via a binary-indexed tree
    /// (<see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#filter_incremental_step_refines_scan">filter_incremental_step_refines_scan</see>,
    /// <see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Heap.Collections.html#fenwick_sum_spec">fenwick_sum_spec</see>)
    /// and splice <c>Keys</c>, falling back to a full pass on dense bursts or upstream reorders. A pending predicate keeps
    /// the key's last membership; a key whose predicate has never settled is absent from <c>Keys</c> and present in
    /// <c>PendingKeys</c>, while <c>AnyPending</c> counts only rows of keys in <c>Keys</c>. A throwing predicate excludes the
    /// key, and <c>Get</c> and <c>TryGet</c> of the key raise its exception.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let active = rows |> Projection.filter (fun t -> not t.Done)
    /// </code>
    /// </example>
    let filter (predicate: 'V -> bool) (upstream: Projection<'K, 'V>) : Projection<'K, 'V> =
        upstream.RequireRowMemos "Projection.filter"
        let graph = upstream.Graph
        let inclusion = ref Unchecked.defaultof<Projection<'K, bool>>
        let view = new FilterView<'K, 'V> (graph, upstream, inclusion, true)

        inclusion.Value <- graph.RunOwned (view.Scope, fun () -> perKey graph upstream predicate)

        view.PendingExtra <- fun () -> view.HeldOut
        view :> Projection<'K, 'V>

    /// <summary>
    /// The keys of <c>upstream</c> whose value <c>chooser</c> maps to <c>Some</c>, in upstream order, each with the value
    /// inside the <c>Some</c>.
    /// </summary>
    /// <remarks>
    /// <c>chooser</c> re-runs once per key when its upstream row changes, unchanged passes cut off per row
    /// (<see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#row_slot_check_cutoff_skips_pass">row_slot_check_cutoff_skips_pass</see>),
    /// a change between two <c>Some</c> values wakes only readers of the key's row, and sparse membership flips update the
    /// output incrementally via binary-indexed rank mapping and <c>Keys</c> splicing
    /// (<see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#filter_incremental_step_refines_scan">filter_incremental_step_refines_scan</see>,
    /// <see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Heap.Collections.html#fenwick_sum_spec">fenwick_sum_spec</see>).
    /// A pending <c>chooser</c> keeps the key's last membership; a key whose <c>chooser</c> has
    /// never settled is absent from <c>Keys</c> and present in <c>PendingKeys</c>, while <c>AnyPending</c> counts only rows
    /// of keys in <c>Keys</c>. A throwing <c>chooser</c> excludes the key, and <c>Get</c> and <c>TryGet</c> of the key raise
    /// its exception.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let due = rows |> Projection.choose (fun t -> t.Due)
    /// </code>
    /// </example>
    let choose (chooser: 'V -> 'U option) (upstream: Projection<'K, 'V>) : Projection<'K, 'U> =
        upstream.RequireRowMemos "Projection.choose"
        let graph = upstream.Graph
        let choices = ref Unchecked.defaultof<Projection<'K, 'U option>>
        let view = new ChooseView<'K, 'V, 'U> (graph, upstream, choices)

        choices.Value <- graph.RunOwned (view.Scope, fun () -> perKey graph upstream chooser)

        view.PendingExtra <- fun () -> view.HeldOut
        view :> Projection<'K, 'U>

    /// <summary>The keys of <c>upstream</c>, in upstream order, each with <c>mapping</c> of its value.</summary>
    /// <remarks>
    /// <c>mapping</c> re-runs for a key when its upstream row changes. A throwing <c>mapping</c> keeps the key, and <c>Get</c>
    /// and <c>TryGet</c> of the key raise its exception. <c>PendingKeys</c> includes the pending keys <c>upstream</c> holds
    /// outside its <c>Keys</c>.
    /// </remarks>
    let map (mapping: 'V -> 'U) (upstream: Projection<'K, 'V>) : Projection<'K, 'U> =
        upstream.RequireRowMemos "Projection.map"

        let view =
            new MapView<'K, 'V, 'U> (upstream.Graph, upstream, Unchecked.defaultof<_>, mapping, Unchecked.defaultof<_>, true)

        if not (Platform.isNull (box upstream.PendingExtra)) then
            view.PendingExtra <- fun () -> view.HeldOut

        view :> Projection<'K, 'U>

    /// <summary>
    /// The keys of <c>upstream</c>, in upstream order, each row the reader <c>mapping</c> returns for the key and a
    /// tracked read of its upstream value.
    /// </summary>
    /// <remarks>
    /// <c>mapping</c> runs once per key, untracked, in a scope that owns the nodes it creates and is disposed with the key.
    /// The reader re-runs when a value it read changes. <c>PendingKeys</c> includes the pending keys <c>upstream</c> holds
    /// outside its <c>Keys</c>.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let labels = rows |> Projection.mapWith (fun id todo -> fun () -> $"{id}: {(todo ()).Title}")
    /// </code>
    /// </example>
    let mapWith (mapping: 'K -> (unit -> 'V) -> (unit -> 'U)) (upstream: Projection<'K, 'V>) : Projection<'K, 'U> =
        upstream.RequireRowMemos "Projection.mapWith"

        let factory (key: 'K) =
            mapping key (fun () -> upstream.Get key)

        let view =
            new MapView<'K, 'V, 'U> (upstream.Graph, upstream, Unchecked.defaultof<_>, Unchecked.defaultof<_>, factory, true)

        if not (Platform.isNull (box upstream.PendingExtra)) then
            view.PendingExtra <- fun () -> view.HeldOut

        view :> Projection<'K, 'U>

    let private sortIn (operation: string) (descending: bool) (projection: 'V -> 'S) (upstream: Projection<'K, 'V>) : Projection<'K, 'V> =
        upstream.RequireRowMemos operation
        let graph = upstream.Graph
        let sortKeys = ref Unchecked.defaultof<Projection<'K, 'S>>
        let view = new SortView<'K, 'V, 'S> (graph, upstream, sortKeys, descending)

        sortKeys.Value <- graph.RunOwned (view.Scope, fun () -> perKey graph upstream projection)

        view.PendingExtra <- fun () -> view.HeldOut
        view :> Projection<'K, 'V>

    /// <summary>
    /// The keys of <c>upstream</c> with their values, ascending by <c>projection</c> of the value; equal sort keys keep upstream
    /// order.
    /// </summary>
    /// <remarks>
    /// A <c>float</c> or <c>float32</c> NaN sort key orders last; <c>None</c> orders first. Untouched rows cut off via per-row
    /// checks (<see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#row_slot_check_cutoff_skips_pass">row_slot_check_cutoff_skips_pass</see>);
    /// sparse sort-key or membership changes check local neighbours in O(1) and binary-search/shift only touched positions
    /// (<see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#sortview_insert_remove_refines_sort">sortview_insert_remove_refines_sort</see>),
    /// falling back to O(N log N) sorting on dense bursts or upstream reorders. A pending sort key keeps the key's last
    /// settled sort key; a key whose sort key has never settled is only in <c>PendingKeys</c>. A throwing sort key excludes the
    /// key, and <c>Get</c> and <c>TryGet</c> of the key raise its exception.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let byDue = rows |> Projection.sortBy (fun t -> t.Due)
    /// </code>
    /// </example>
    let sortBy (projection: 'V -> 'S) (upstream: Projection<'K, 'V>) : Projection<'K, 'V> =
        sortIn "Projection.sortBy" false projection upstream

    /// <summary>
    /// The keys of <c>upstream</c> with their values, descending by <c>projection</c> of the value; equal sort keys keep
    /// upstream order.
    /// </summary>
    /// <remarks>
    /// A <c>float</c> or <c>float32</c> NaN sort key orders last; <c>None</c> orders after <c>Some</c>. Incremental
    /// checks, sparse reinsertion (<see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#sortview_insert_remove_refines_sort">sortview_insert_remove_refines_sort</see>),
    /// pending, and failed sort keys follow <c>sortBy</c>.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let newestFirst = rows |> Projection.sortByDescending (fun t -> t.Created)
    /// </code>
    /// </example>
    let sortByDescending (projection: 'V -> 'S) (upstream: Projection<'K, 'V>) : Projection<'K, 'V> =
        sortIn "Projection.sortByDescending" true projection upstream

    let private slice (offset: unit -> int) (count: unit -> int) (upstream: Projection<'K, 'V>) : Projection<'K, 'V> =
        upstream.RequireRowMemos "Projection.slice"
        let view = new SliceView<'K, 'V> (upstream.Graph, upstream, offset, count)

        if not (Platform.isNull (box upstream.PendingExtra)) then
            view.PendingExtra <- fun () -> view.HeldOut

        view :> Projection<'K, 'V>

    /// <summary>The first <c>count ()</c> keys of <c>upstream</c>, in upstream order, with their values.</summary>
    /// <remarks>
    /// <c>count</c> is a tracked read: the window follows a signal it reads, as well as membership and order changes
    /// upstream. A count past the last key selects every key, and a negative count gives an empty window,
    /// as <c>List.truncate</c> does. A key that stays in the window keeps its row. A pass costs O(window), and a throwing
    /// <c>count</c> fails the pass. <c>PendingKeys</c> includes the pending keys <c>upstream</c> holds outside its
    /// <c>Keys</c>.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let pageSize = createSignal 20
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let firstPage = rows |> Projection.sortBy (fun t -> t.Due) |> Projection.take (fun () -> pageSize.Value)
    /// </code>
    /// </example>
    let take (count: unit -> int) (upstream: Projection<'K, 'V>) : Projection<'K, 'V> =
        slice Unchecked.defaultof<_> count upstream

    /// <summary>The keys of <c>upstream</c> after the first <c>count ()</c>, in upstream order, with their values.</summary>
    /// <remarks>
    /// <c>count</c> is a tracked read, as in <c>take</c>. A count past the last key gives an empty window, and a
    /// negative count selects every key. A key that stays in the window keeps its row. A pass costs O(window), and a throwing
    /// <c>count</c> fails the pass. <c>PendingKeys</c> includes the pending keys <c>upstream</c> holds outside its
    /// <c>Keys</c>.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let rest = rows |> Projection.skip (fun () -> 1)
    /// </code>
    /// </example>
    let skip (count: unit -> int) (upstream: Projection<'K, 'V>) : Projection<'K, 'V> =
        slice count Unchecked.defaultof<_> upstream

    /// <summary>
    /// The keys of <c>upstream</c> from position <c>offset ()</c>, at most <c>count ()</c> of them, in upstream order, with
    /// their values.
    /// </summary>
    /// <remarks>
    /// <c>offset</c> and <c>count</c> are tracked reads, as in <c>take</c>. <c>offset</c> clamps as the count of
    /// <c>skip</c> does, and <c>count</c> as the count of <c>take</c>. A key that stays in the window keeps its row, so
    /// shifting the window by d positions creates and disposes at most d rows. A pass costs O(window), and a throwing
    /// <c>offset</c> or <c>count</c> fails the pass. <c>PendingKeys</c> includes the pending keys <c>upstream</c> holds
    /// outside its <c>Keys</c>.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let page = createSignal 0
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let visible = rows |> Projection.sub (fun () -> page.Value * 20) (fun () -> 20)
    /// </code>
    /// </example>
    let sub (offset: unit -> int) (count: unit -> int) (upstream: Projection<'K, 'V>) : Projection<'K, 'V> =
        slice offset count upstream

    /// <summary>
    /// The groups of <c>upstream</c> by <c>projection</c> of each value, ordered by the upstream position of each group's
    /// first member, each an inner view of the group's keys in upstream order.
    /// </summary>
    /// <remarks>
    /// Sparse group-key or membership changes update only the affected source and destination groups so untouched inner views
    /// keep their <c>Keys</c> and stay asleep, while active groups stay ordered by first upstream member
    /// (<see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#grouping_move_refines_scan">grouping_move_refines_scan</see>,
    /// <see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#row_slot_check_cutoff_skips_pass">row_slot_check_cutoff_skips_pass</see>).
    /// A pending group key keeps the key's last settled group. A key whose group key throws or has never settled is in
    /// <c>UngroupedKeys</c> and in no group. A moved key leaves its old group and joins its new one in the same pass. An
    /// emptied group's inner view is disposed: its <c>Keys</c> is empty and <c>Get</c> raises
    /// <c>ObjectDisposedException</c>.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let byOwner = rows |> Projection.groupBy (fun t -> t.Owner)
    /// </code>
    /// </example>
    let groupBy (projection: 'V -> 'G) (upstream: Projection<'K, 'V>) : Grouping<'G, 'K, 'V> =
        upstream.RequireRowMemos "Projection.groupBy"
        let graph = upstream.Graph
        let groupKeys = ref Unchecked.defaultof<Projection<'K, 'G>>
        let view = new Grouping<'G, 'K, 'V> (graph, upstream, groupKeys)

        groupKeys.Value <- graph.RunOwned (view.Scope, fun () -> perKey graph upstream projection)

        view

    /// <summary>
    /// A memo of <c>result</c> applied to the fold of <c>rows ()</c>. The memo owns the fold and every node created by
    /// <c>rows</c>.
    /// </summary>
    let private foldOwned
        (graph: Graph)
        (rows: unit -> Projection<'K, 'U>)
        (add: 'S -> 'U -> 'S)
        (subtract: 'S -> 'U -> 'S)
        (zero: 'S)
        (result: 'S -> 'R)
        : Memo<'R> =
        let owner = ownedScope graph

        let aggregate =
            graph.RunOwned (owner, fun () -> ProjectionFold<'K, 'U, 'S>.Create(graph, rows (), add, subtract, zero, true))

        let memo =
            Memo<'R>.Create(graph, (fun _ -> result (aggregate.Compute ValueNone)), ScopeMode.Pure)

        memo.Adopt owner
        memo

    /// <summary>
    /// A memo of the settled values of <c>upstream</c> folded into <c>zero</c> with <c>add</c>, kept current by applying each
    /// row change: <c>add</c> for an added key, <c>subtract</c> of its last value for a removed key, and both for a changed value.
    /// </summary>
    /// <remarks>
    /// <c>subtract</c> must invert <c>add</c>. A row edit costs O(1) calls and reads the edited row alone; a key change costs
    /// O(Δ) up to <c>max(64, N)</c> changes, falling back to an O(N) key diff on a larger burst. The fold covers settled values:
    /// a pending row keeps its last one, as in <c>Snapshot</c>, and <c>AnyPending</c> reports it. A failed row makes a read raise
    /// its exception. A value read by <c>add</c> or <c>subtract</c> re-folds the cached values on change. Disposing the memo, or
    /// the calling scope, disposes the fold.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) (fun t -> t.Estimate) (fun () -> todos.Value)
    /// let total = rows |> Projection.foldGroup (+) (-) 0
    /// </code>
    /// </example>
    let foldGroup (add: 'S -> 'V -> 'S) (subtract: 'S -> 'V -> 'S) (zero: 'S) (upstream: Projection<'K, 'V>) : Memo<'S> =
        upstream.RequireRowMemos "Projection.foldGroup"

        if Platform.isNull (box subtract) then
            raise (System.ArgumentNullException (nameof subtract))

        let graph = upstream.Graph

        let aggregate =
            ProjectionFold<'K, 'V, 'S>.Create(graph, upstream, add, subtract, zero, true)

        let memo = Memo<'S>.Create(graph, aggregate.Compute, ScopeMode.Pure)
        aggregate.Memo <- memo
        memo.Adopt aggregate
        memo

    /// <summary>A memo of the settled values of <c>upstream</c> folded with <c>folder</c> from <c>state</c>, in <c>Keys</c> order.</summary>
    /// <remarks>
    /// Any key or value change re-folds every row's cached value: O(N) calls of <c>folder</c>, reading only the changed rows.
    /// Use <c>foldGroup</c> when the fold has an inverse. Pending, failed and disposal behave as in <c>foldGroup</c>.
    /// </remarks>
    let fold (folder: 'S -> 'V -> 'S) (state: 'S) (upstream: Projection<'K, 'V>) : Memo<'S> =
        upstream.RequireRowMemos "Projection.fold"
        let graph = upstream.Graph

        let aggregate =
            ProjectionFold<'K, 'V, 'S>.Create(graph, upstream, folder, Unchecked.defaultof<_>, state, false)

        let memo = Memo<'S>.Create(graph, aggregate.Compute, ScopeMode.Pure)
        aggregate.Memo <- memo
        memo.Adopt aggregate
        memo

    /// <summary>
    /// A memo of <c>result</c> applied to the <c>foldGroup add subtract zero</c> of <c>projection</c> over the values of
    /// <c>upstream</c>.
    /// </summary>
    /// <remarks>
    /// <c>projection</c> re-runs for a key when its upstream row changes, in a view the memo owns; <c>result</c> re-runs
    /// when the fold state changes. Costs, pending and failed rows follow <c>foldGroup</c>. Disposing the memo, or the
    /// calling scope, disposes the view and the fold.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">On .NET, <c>subtract</c> is null.</exception>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    ///
    /// let anyOpen =
    ///     rows
    ///     |> Projection.foldGroupBy
    ///         (fun t -> t.Done)
    ///         (fun n d -> if d then n else n + 1)
    ///         (fun n d -> if d then n else n - 1)
    ///         0
    ///         (fun n -> n > 0)
    /// </code>
    /// </example>
    let foldGroupBy
        (projection: 'V -> 'U)
        (add: 'S -> 'U -> 'S)
        (subtract: 'S -> 'U -> 'S)
        (zero: 'S)
        (result: 'S -> 'R)
        (upstream: Projection<'K, 'V>)
        : Memo<'R> =
        upstream.RequireRowMemos "Projection.foldGroupBy"

        if Platform.isNull (box subtract) then
            raise (System.ArgumentNullException (nameof subtract))

        foldOwned upstream.Graph (fun () -> map projection upstream) add subtract zero result

    /// <summary><c>s - n</c>, raising ArithmeticException when the difference is infinite or NaN.</summary>
    [<NoDynamicInvocation>]
    let inline private subtractFinite (s: ^N) (n: ^N) : ^N =
        let d = s - n

        // A throwing subtract re-folds from the cached values. d - d is non-zero exactly when d is infinite or NaN.
        if d - d <> LanguagePrimitives.GenericZero then
            raise (System.ArithmeticException ())

        d

    /// <summary>A memo of the sum of <c>projection</c> over the values of <c>upstream</c>, kept current per row change.</summary>
    /// <remarks>
    /// <c>projection</c> re-runs for a key when its upstream row changes, in a view the memo owns.
    /// A pending or throwing <c>projection</c> behaves as a pending or failed row of <c>foldGroup</c>. While the sum is
    /// infinite or NaN, each row change re-adds every cached value: O(N).
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let hours = rows |> Projection.sumBy (fun t -> t.Hours)
    /// </code>
    /// </example>
    [<NoDynamicInvocation>]
    let inline sumBy (projection: 'V -> ^N) (upstream: Projection<'K, 'V>) : Memo< ^N > =
        upstream
        |> foldGroupBy projection (fun s n -> s + n) subtractFinite LanguagePrimitives.GenericZero id

    /// <summary>A memo of how many values of <c>upstream</c> satisfy <c>predicate</c>, kept current per row change.</summary>
    /// <remarks>
    /// <c>predicate</c> re-runs for a key when its upstream row changes. A pending or throwing <c>predicate</c> behaves as a
    /// pending or failed row of <c>foldGroup</c>.
    /// </remarks>
    let countBy (predicate: 'V -> bool) (upstream: Projection<'K, 'V>) : Memo<int> =
        upstream
        |> foldGroupBy predicate (fun n hit -> if hit then n + 1 else n) (fun n hit -> if hit then n - 1 else n) 0 id

    /// <summary>A memo of whether some value of <c>upstream</c> satisfies <c>predicate</c>: a <c>countBy</c> above zero.</summary>
    /// <remarks>Costs and pending and failed rows follow <c>countBy</c>. An empty projection reads <c>false</c>.</remarks>
    let exists (predicate: 'V -> bool) (upstream: Projection<'K, 'V>) : Memo<bool> =
        upstream
        |> foldGroupBy predicate (fun n hit -> if hit then n + 1 else n) (fun n hit -> if hit then n - 1 else n) 0 (fun n -> n > 0)

    /// <summary>A memo of whether every value of <c>upstream</c> satisfies <c>predicate</c>: a <c>countBy</c> of misses at zero.</summary>
    /// <remarks>Costs and pending and failed rows follow <c>countBy</c>. An empty projection reads <c>true</c>.</remarks>
    let forall (predicate: 'V -> bool) (upstream: Projection<'K, 'V>) : Memo<bool> =
        upstream
        |> foldGroupBy predicate (fun n hit -> if hit then n else n + 1) (fun n hit -> if hit then n else n - 1) 0 (fun n -> n = 0)

    /// <summary>A memo of the number of keys of <c>upstream</c>.</summary>
    /// <remarks>
    /// Depends exclusively on <c>Keys</c>. Pending rows count, as in <c>Count</c>. A pending or failed pass makes the memo
    /// pending or failed.
    /// </remarks>
    let length (upstream: Projection<'K, 'V>) : Memo<int> =
        Memo<int>.Create(upstream.Graph, (fun _ -> upstream.Keys.Length), ScopeMode.Pure)

    /// <summary>A memo of whether <c>Keys</c> of <c>upstream</c> is empty.</summary>
    /// <remarks>Depends on <c>Keys</c> as <c>length</c> does, and wakes its readers only when emptiness changes.</remarks>
    let isEmpty (upstream: Projection<'K, 'V>) : Memo<bool> =
        Memo<bool>.Create(upstream.Graph, (fun _ -> upstream.Keys.Length = 0), ScopeMode.Pure)

    /// <summary>A memo of the first key of <c>upstream</c> with its value, or <c>None</c> while <c>Keys</c> is empty.</summary>
    /// <remarks>
    /// Depends exclusively on <c>Keys</c> and the first key's row. A pending first row makes the memo pending, and a failed
    /// one makes it raise the row's exception.
    /// </remarks>
    let tryHead (upstream: Projection<'K, 'V>) : Memo<('K * 'V) option> =
        positional upstream (fun _ -> 0) boxPair

    /// <summary>A memo of the last key of <c>upstream</c> with its value, or <c>None</c> while <c>Keys</c> is empty.</summary>
    /// <remarks>Reads as <c>tryHead</c> does, at the last position.</remarks>
    let tryLast (upstream: Projection<'K, 'V>) : Memo<('K * 'V) option> =
        positional upstream (fun count -> count - 1) boxPair

    /// <summary>
    /// A memo of the key at position <c>index ()</c> of <c>upstream</c> with its value, or <c>None</c> for a position
    /// outside <c>Keys</c>, negative included.
    /// </summary>
    /// <remarks>
    /// <c>index</c> is a tracked read, as the count of <c>take</c> is. Reads as <c>tryHead</c> does, at that position. A
    /// throwing <c>index</c> fails the memo.
    /// </remarks>
    let tryItem (index: unit -> int) (upstream: Projection<'K, 'V>) : Memo<('K * 'V) option> =
        positional upstream (fun _ -> index ()) boxPair

    /// <summary><c>tryHead</c>, raising <c>KeyNotFoundException</c> while <c>Keys</c> is empty.</summary>
    /// <remarks>The memo fails while empty and recovers once a key arrives; prefer <c>tryHead</c> for a binding.</remarks>
    let head (upstream: Projection<'K, 'V>) : Memo<'K * 'V> =
        positional upstream (fun _ -> 0) (pairOrRaise "Projection.head: the projection has no keys.")

    /// <summary><c>tryLast</c>, raising <c>KeyNotFoundException</c> while <c>Keys</c> is empty.</summary>
    /// <remarks>Fails and recovers as <c>head</c> does.</remarks>
    let last (upstream: Projection<'K, 'V>) : Memo<'K * 'V> =
        positional upstream (fun count -> count - 1) (pairOrRaise "Projection.last: the projection has no keys.")

    /// <summary><c>tryItem</c>, raising <c>KeyNotFoundException</c> for a position outside <c>Keys</c>.</summary>
    /// <remarks>Fails and recovers as <c>head</c> does.</remarks>
    let item (index: unit -> int) (upstream: Projection<'K, 'V>) : Memo<'K * 'V> =
        positional upstream (fun _ -> index ()) (pairOrRaise "Projection.item: the projection has no key at the index.")

    /// <summary>
    /// A memo of <c>finish</c> applied to the first key of the view returned by <c>build</c>, with its value. The memo owns
    /// the view.
    /// </summary>
    /// <remarks>
    /// Readers re-run only when the compared part changes: the key when <c>byKey</c> is true, and the value otherwise. An
    /// equal part returns the last result instance.
    /// </remarks>
    let private firstOf (graph: Graph) (build: unit -> Projection<'K, 'U>) (byKey: bool) (finish: struct ('K * 'U) voption -> 'R) : Memo<'R> =
        let owner = ownedScope graph
        let view = graph.RunOwned (owner, build)

        let equalValues =
            if byKey then
                Unchecked.defaultof<System.Collections.Generic.IEqualityComparer<'U>>
            else
                graph.EncodedComparer<'U>()

        let mutable hasLast = false
        let mutable lastKey = Unchecked.defaultof<'K>
        let mutable lastValue = Unchecked.defaultof<'U>
        let mutable lastResult = Unchecked.defaultof<'R>

        let compute (_: 'R voption) =
            let entry = entryAt view (fun _ -> 0)

            match entry with
            | ValueSome (struct (key, value)) ->
                let unchanged =
                    hasLast
                    && (if byKey then
                            Platform.keyEquals key lastKey
                        else
                            ComparerSentinel.equals equalValues value lastValue)

                if unchanged then
                    lastResult
                else
                    let result = finish entry
                    hasLast <- true
                    lastKey <- key

                    if not byKey then
                        lastValue <- value

                    lastResult <- result
                    result
            | ValueNone ->
                hasLast <- false
                lastKey <- Unchecked.defaultof<'K>
                lastValue <- Unchecked.defaultof<'U>
                lastResult <- Unchecked.defaultof<'R>
                finish ValueNone

        let memo = Memo<'R>.Create(graph, compute, ScopeMode.Pure)
        memo.Adopt owner
        memo

    /// <summary>A memo of the first value of <c>upstream</c>, in upstream order, that satisfies <c>predicate</c>.</summary>
    /// <remarks>
    /// The memo owns a <c>filter</c> view: <c>predicate</c> re-runs only for changed upstream rows, and sparse membership
    /// flips update incrementally via the filter's binary-indexed tree and <c>Keys</c> splice
    /// (<see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#filter_incremental_step_refines_scan">filter_incremental_step_refines_scan</see>).
    /// A match whose predicate is pending makes the memo pending.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let firstOpen = rows |> Projection.tryFind (fun t -> not t.Done)
    /// </code>
    /// </example>
    let tryFind (predicate: 'V -> bool) (upstream: Projection<'K, 'V>) : Memo<'V option> =
        upstream.RequireRowMemos "Projection.tryFind"
        firstOf upstream.Graph (fun () -> filter predicate upstream) false boxSnd

    /// <summary>A memo of the first key of <c>upstream</c>, in upstream order, whose value satisfies <c>predicate</c>.</summary>
    /// <remarks>Follows <c>tryFind</c>.</remarks>
    let tryFindKey (predicate: 'V -> bool) (upstream: Projection<'K, 'V>) : Memo<'K option> =
        upstream.RequireRowMemos "Projection.tryFindKey"
        firstOf upstream.Graph (fun () -> filter predicate upstream) true boxFst

    /// <summary>
    /// A memo of the first <c>Some</c> returned by <c>chooser</c> over the values of <c>upstream</c>, in upstream order.
    /// </summary>
    /// <remarks>The memo owns a <c>choose</c> view; costs, pending and failure follow <c>tryFind</c>.</remarks>
    let tryPick (chooser: 'V -> 'U option) (upstream: Projection<'K, 'V>) : Memo<'U option> =
        upstream.RequireRowMemos "Projection.tryPick"
        firstOf upstream.Graph (fun () -> choose chooser upstream) false boxSnd

    /// <summary><c>tryFind</c>, raising <c>KeyNotFoundException</c> while no value satisfies <c>predicate</c>.</summary>
    /// <remarks>Fails and recovers as <c>head</c> does.</remarks>
    let find (predicate: 'V -> bool) (upstream: Projection<'K, 'V>) : Memo<'V> =
        upstream.RequireRowMemos "Projection.find"

        firstOf upstream.Graph (fun () -> filter predicate upstream) false (sndOrRaise "Projection.find: no value satisfies the predicate.")

    /// <summary><c>tryFindKey</c>, raising <c>KeyNotFoundException</c> while no value satisfies <c>predicate</c>.</summary>
    /// <remarks>Fails and recovers as <c>head</c> does.</remarks>
    let findKey (predicate: 'V -> bool) (upstream: Projection<'K, 'V>) : Memo<'K> =
        upstream.RequireRowMemos "Projection.findKey"

        firstOf upstream.Graph (fun () -> filter predicate upstream) true (fstOrRaise "Projection.findKey: no value satisfies the predicate.")

    /// <summary><c>tryPick</c>, raising <c>KeyNotFoundException</c> while <c>chooser</c> returns <c>None</c> for every value.</summary>
    /// <remarks>Fails and recovers as <c>head</c> does.</remarks>
    let pick (chooser: 'V -> 'U option) (upstream: Projection<'K, 'V>) : Memo<'U> =
        upstream.RequireRowMemos "Projection.pick"

        firstOf upstream.Graph (fun () -> choose chooser upstream) false (sndOrRaise "Projection.pick: the chooser returns None for every value.")

    /// <summary>The mean of a <c>struct (sum, count)</c> state, or <c>None</c> at count zero.</summary>
    [<NoDynamicInvocation>]
    let inline private mean (struct (sum: ^N, count: int)) : ^N option =
        if count = 0 then
            None
        else
            Some (LanguagePrimitives.DivideByInt sum count)

    /// <summary>
    /// A memo of <c>result</c> applied to the running <c>struct (sum, count)</c> of <c>projection</c> over the values of
    /// <c>upstream</c>.
    /// </summary>
    [<NoDynamicInvocation>]
    let inline private meanBy (projection: 'V -> ^N) (result: struct (^N * int) -> 'R) (upstream: Projection<'K, 'V>) : Memo<'R> =
        upstream
        |> foldGroupBy
            projection
            (fun (struct (sum, count)) n -> struct (sum + n, count + 1))
            (fun (struct (sum, count)) n -> struct (subtractFinite sum n, count - 1))
            (struct (LanguagePrimitives.GenericZero, 0))
            result

    /// <summary>
    /// A memo of the mean of <c>projection</c> over the settled values of <c>upstream</c>, or <c>None</c> while no value has
    /// settled.
    /// </summary>
    /// <remarks>
    /// Costs and pending rows follow <c>sumBy</c>. Readers wake only when the mean changes. Supports <c>float</c>,
    /// <c>float32</c>, <c>decimal</c> and any type with <c>+</c>, <c>-</c>, <c>Zero</c>, <c>DivideByInt</c> and equality.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let meanHours = rows |> Projection.tryAverageBy (fun t -> t.Hours)
    /// </code>
    /// </example>
    [<NoDynamicInvocation>]
    let inline tryAverageBy (projection: 'V -> ^N) (upstream: Projection<'K, 'V>) : Memo<^N option> =
        let last: ^N option ref = ref None

        meanBy
            projection
            (fun state ->
                match mean state, last.Value with
                | Some current, Some previous when current = previous -> last.Value
                | current, _ ->
                    last.Value <- current
                    current)
            upstream

    /// <summary><c>tryAverageBy</c>, raising <c>KeyNotFoundException</c> while no value has settled.</summary>
    /// <remarks>Fails and recovers as <c>head</c> does.</remarks>
    [<NoDynamicInvocation>]
    let inline averageBy (projection: 'V -> ^N) (upstream: Projection<'K, 'V>) : Memo< ^N > =
        let raiseEmpty =
            orRaise "Projection.averageBy: the projection has no settled values."

        meanBy projection (fun state -> raiseEmpty (mean state)) upstream

    /// <summary>
    /// A memo of <c>finish</c> applied to the value holding the best rank of <c>projection</c>. <c>isMax</c> selects
    /// greatest ranks when true and least ranks when false.
    /// </summary>
    /// <remarks>
    /// An equal rank keeps the holder, including when its holder's row is edited to an equal rank. Removing, editing or
    /// tail-appending rows updates a binary tournament winner tree in O(log N) comparisons; non-tail insertions and upstream
    /// reorders rebuild in O(N) over cached ranks. Readers re-run only when the published result input changes.
    /// </remarks>
    let private extremeBy
        (operation: string)
        (isMax: bool)
        (projection: 'V -> 'S)
        (finish: 'V option -> 'R)
        (upstream: Projection<'K, 'V>)
        : Memo<'R> =
        upstream.RequireRowMemos operation
        let graph = upstream.Graph
        let encodedEqualValues = graph.EncodedComparer<'V>()
        let owner = ownedScope graph

        let aggregate =
            graph.RunOwned (
                owner,
                fun () ->
                    let ranked = perKey graph upstream (fun value -> struct (projection value, value))

                    ProjectionExtreme<'K, 'V, 'S, 'R>.Create(graph, ranked, isMax, encodedEqualValues, finish)
            )

        let memo = Memo<'R>.Create(graph, aggregate.Compute, ScopeMode.Pure)
        memo.Adopt owner
        memo

    /// <summary>
    /// A memo of a value of <c>upstream</c> with the least <c>projection</c> under the <c>sortBy</c> order, or <c>None</c>
    /// while no value has settled.
    /// </summary>
    /// <remarks>
    /// A NaN rank orders after every other rank. Among equal ranks the current result stays, including when its holder's
    /// row is edited to an equal rank; when the holder is removed, the result is the first holder in <c>Keys</c> order. A
    /// row edit, removal or tail append updates a binary tournament winner tree in O(log N) comparisons
    /// (<see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#tournament_winner_optimal_and_first">tournament_winner_optimal_and_first</see>,
    /// <see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#tournament_point_update_refines_eval">tournament_point_update_refines_eval</see>,
    /// <see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#sticky_holder_refines_extreme_scan">sticky_holder_refines_extreme_scan</see>),
    /// or O(1) when its rank is unchanged; non-tail key insertions and upstream reorders rebuild in O(N) from cached ranks.
    /// Pending and failed ranks follow <c>foldGroup</c>.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let next = rows |> Projection.tryMinBy (fun t -> t.Due)
    /// </code>
    /// </example>
    let tryMinBy (projection: 'V -> 'S) (upstream: Projection<'K, 'V>) : Memo<'V option> =
        extremeBy "Projection.tryMinBy" false projection id upstream

    /// <summary>
    /// A memo of a value of <c>upstream</c> with the greatest <c>projection</c> under the <c>sortBy</c> order, or
    /// <c>None</c> while no value has settled.
    /// </summary>
    /// <remarks>A NaN rank is the greatest. Ties, costs, pending and failure follow <c>tryMinBy</c>.</remarks>
    let tryMaxBy (projection: 'V -> 'S) (upstream: Projection<'K, 'V>) : Memo<'V option> =
        extremeBy "Projection.tryMaxBy" true projection id upstream

    /// <summary><c>tryMinBy</c>, raising <c>KeyNotFoundException</c> while no value has settled.</summary>
    /// <remarks>Fails and recovers as <c>head</c> does.</remarks>
    let minBy (projection: 'V -> 'S) (upstream: Projection<'K, 'V>) : Memo<'V> =
        extremeBy "Projection.minBy" false projection (orRaise "Projection.minBy: the projection has no settled values.") upstream

    /// <summary><c>tryMaxBy</c>, raising <c>KeyNotFoundException</c> while no value has settled.</summary>
    /// <remarks>Fails and recovers as <c>head</c> does.</remarks>
    let maxBy (projection: 'V -> 'S) (upstream: Projection<'K, 'V>) : Memo<'V> =
        extremeBy "Projection.maxBy" true projection (orRaise "Projection.maxBy: the projection has no settled values.") upstream

    /// <summary>
    /// The keys of <c>upstream</c> satisfying <c>predicate</c>, and the remaining keys, each in upstream order with their
    /// values.
    /// </summary>
    /// <remarks>
    /// The halves share one predicate view, disposed with the calling scope; each half is disposable on its own, and sparse
    /// membership flips update each half's output incrementally via binary-indexed rank mapping and <c>Keys</c> splicing
    /// (<see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#filter_incremental_step_refines_scan">filter_incremental_step_refines_scan</see>,
    /// <see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#row_slot_check_cutoff_skips_pass">row_slot_check_cutoff_skips_pass</see>).
    /// A throwing predicate removes the key from both halves, and <c>Get</c> and <c>TryGet</c> of either raise its exception.
    /// A pending predicate keeps the key on its last side; a never-settled key is in the <c>PendingKeys</c> of both.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let finished, pending = rows |> Projection.partition (fun t -> t.Done)
    /// </code>
    /// </example>
    let partition (predicate: 'V -> bool) (upstream: Projection<'K, 'V>) : Projection<'K, 'V> * Projection<'K, 'V> =
        upstream.RequireRowMemos "Projection.partition"
        let graph = upstream.Graph
        let owner = ownedScope graph

        let inclusion =
            ref (graph.RunOwned (owner, fun () -> perKey graph upstream predicate))

        let half admit =
            let view = new FilterView<'K, 'V> (graph, upstream, inclusion, admit)
            view.PendingExtra <- fun () -> view.HeldOut
            view :> Projection<'K, 'V>

        half true, half false

    /// <summary>
    /// The first key of <c>upstream</c> for each distinct <c>projection</c> of the value, in upstream order, with its
    /// value.
    /// </summary>
    /// <remarks>
    /// Untouched rows cut off per row
    /// (<see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#row_slot_check_cutoff_skips_pass">row_slot_check_cutoff_skips_pass</see>);
    /// sparse key or projected-value changes update only the old and new value buckets, keeping non-winner moves out of
    /// <c>Keys</c> and promoting the next upstream key when a holder leaves via its bucket and binary-indexed rank splice
    /// (<see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#distinct_bucket_update_refines_scan">distinct_bucket_update_refines_scan</see>,
    /// <see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Heap.Collections.html#fenwick_sum_spec">fenwick_sum_spec</see>),
    /// falling back to a full pass on dense bursts or upstream reorders. A pending value keeps the key's last settled
    /// value, and a never-settled key is in <c>PendingKeys</c>. A throwing <c>projection</c> excludes the key, and
    /// <c>Get</c> and <c>TryGet</c> of the key raise its exception.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    /// let onePerOwner = rows |> Projection.distinctBy (fun t -> t.Owner)
    /// </code>
    /// </example>
    let distinctBy (projection: 'V -> 'G) (upstream: Projection<'K, 'V>) : Projection<'K, 'V> =
        upstream.RequireRowMemos "Projection.distinctBy"
        let graph = upstream.Graph
        let values = ref Unchecked.defaultof<Projection<'K, 'G>>
        let view = new DistinctView<'K, 'V, 'G> (graph, upstream, values)

        values.Value <- graph.RunOwned (view.Scope, fun () -> perKey graph upstream projection)

        view.PendingExtra <- fun () -> view.HeldOut
        view :> Projection<'K, 'V>

/// <summary>Operations over the groups of a <c>Grouping</c>.</summary>
[<RequireQualifiedAccess>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Grouping =
    /// <summary>
    /// A projection keyed and ordered as <c>grouping</c>, whose row for a group is the value of the memo created by
    /// <c>perGroup</c> over the group's inner view.
    /// </summary>
    /// <remarks>
    /// <c>perGroup</c> runs once per group row, in a scope that owns the nodes it creates and is disposed with the row: an
    /// emptied group disposes its memo, and a returning group gets a new one. A pending or failed memo makes its row
    /// pending or failed. Each memo covers only the keys of its group.
    /// </remarks>
    /// <example>
    /// <code lang="fsharp">
    /// let rows = createProjection (fun t -> t.Id) id (fun () -> todos.Value)
    ///
    /// let hoursByOwner =
    ///     rows
    ///     |> Projection.groupBy (fun t -> t.Owner)
    ///     |> Grouping.aggregate (Projection.sumBy (fun t -> t.Hours))
    /// </code>
    /// </example>
    let aggregate (perGroup: Projection<'K, 'V> -> Memo<'S>) (grouping: Grouping<'G, 'K, 'V>) : Projection<'G, 'S> =
        (grouping :> Projection<'G, Projection<'K, 'V>>)
        |> Projection.mapWith (fun _ group ->
            let total = perGroup (group ())
            fun () -> total.Value)
