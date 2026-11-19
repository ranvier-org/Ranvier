namespace Ranvier

/// <summary>An edit to a positional list of keys, valid when applied in sequence.</summary>
[<RequireQualifiedAccess>]
type PositionalChange<'K> =
    /// <summary>Removes the key at <c>index</c>.</summary>
    | RemoveAt of Index: int
    /// <summary>Inserts <c>key</c> so that it lands at <c>index</c>.</summary>
    | InsertAt of Index: int * Key: 'K
    /// <summary>Removes the key at <c>oldIndex</c>, then inserts it at <c>newIndex</c> of the shortened list.</summary>
    | Move of OldIndex: int * NewIndex: int

module internal Positional =
    /// <summary>Adds <c>delta</c> at zero-based <c>index</c> of a Fenwick tree.</summary>
    let private addAt (tree: int[]) (index: int) (delta: int) =
        let mutable i = index + 1

        while i < tree.Length do
            Platform.setItemAt i tree (Platform.itemAt i tree + delta)
            i <- i + (i &&& -i)

    /// <summary>The sum of a Fenwick tree's entries at zero-based indices below <c>index</c>.</summary>
    let private sumBelow (tree: int[]) (index: int) =
        let mutable i = index
        let mutable sum = 0

        while i > 0 do
            sum <- sum + Platform.itemAt i tree
            i <- i - (i &&& -i)

        sum

    /// <summary>
    /// Marks the members of one longest strictly increasing subsequence of <c>values</c>. O(N log N).
    /// </summary>
    let longestIncreasing (values: int[]) : bool[] =
        let n = values.Length
        let tails = Platform.positions n
        let previous = Array.create n -1
        let mutable length = 0

        for i in 0 .. n - 1 do
            let vi = Platform.itemAt i values
            let mutable lo = 0
            let mutable hi = length

            while lo < hi do
                let mid = (lo + hi) >>> 1

                if Platform.itemAt (Platform.itemAt mid tails) values < vi then
                    lo <- mid + 1
                else
                    hi <- mid

            if lo > 0 then
                Platform.setItemAt i previous (Platform.itemAt (lo - 1) tails)

            Platform.setItemAt lo tails i

            if lo = length then
                length <- length + 1

        let members = Array.zeroCreate<bool> n

        let mutable i =
            if length > 0 then
                Platform.itemAt (length - 1) tails
            else
                -1

        while i >= 0 do
            Platform.setItemAt i members true
            i <- Platform.itemAt i previous

        members

    /// <summary>
    /// The edits that turn <c>previous</c> into <c>next</c>: removals from back to front, then the moves and inserts in
    /// the order of <c>next</c>. Both arrays hold distinct keys.
    /// </summary>
    /// <remarks>
    /// Shared prefix and suffix runs trim in O(N) without intermediate allocations; contiguous inserts or removals, single
    /// replacements, and adjacent swaps emit directly, and the longest-increasing-subsequence pass runs in O(M log M)
    /// over the M keys in the changed middle slice
    /// (<see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Deltas.html#positional_frame_prefix_suffix">positional_frame_prefix_suffix</see>,
    /// <see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Deltas.html#positional_pure_inserts_correct">positional_pure_inserts_correct</see>,
    /// <see href="https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Deltas.html#positional_pure_removes_correct">positional_pure_removes_correct</see>).
    /// One longest run of survivors that keeps its relative order stays in place; the moves number at most the survivors
    /// minus that run's length.
    /// </remarks>
    let diff (previous: 'K[]) (next: 'K[]) : ResizeArray<PositionalChange<'K>> =
        let edits = ResizeArray<PositionalChange<'K>>()

        if not (obj.ReferenceEquals (previous, next)) then
            let prevTotal = previous.Length
            let nextTotal = next.Length
            let minLen = min prevTotal nextTotal
            let mutable pref = 0

            while pref < minLen
                  && Platform.keyEquals (Platform.itemAt pref previous) (Platform.itemAt pref next) do
                pref <- pref + 1

            let maxSuff = minLen - pref
            let mutable suff = 0

            while suff < maxSuff
                  && Platform.keyEquals (Platform.itemAt (prevTotal - 1 - suff) previous) (Platform.itemAt (nextTotal - 1 - suff) next) do
                suff <- suff + 1

            let prevLen = prevTotal - pref - suff
            let nextLen = nextTotal - pref - suff

            if prevLen = 0 && nextLen = 0 then
                ()
            elif prevLen = 0 then
                Platform.reserve edits nextLen

                for k in 0 .. nextLen - 1 do
                    edits.Add (PositionalChange.InsertAt (pref + k, Platform.itemAt (pref + k) next))
            elif nextLen = 0 then
                Platform.reserve edits prevLen

                for i = pref + prevLen - 1 downto pref do
                    edits.Add (PositionalChange.RemoveAt i)
            elif prevLen = 1 && nextLen = 1 then
                edits.Add (PositionalChange.RemoveAt pref)
                edits.Add (PositionalChange.InsertAt (pref, Platform.itemAt pref next))
            elif
                prevLen = 2
                && nextLen = 2
                && Platform.keyEquals (Platform.itemAt pref previous) (Platform.itemAt (pref + 1) next)
                && Platform.keyEquals (Platform.itemAt (pref + 1) previous) (Platform.itemAt pref next)
            then
                edits.Add (PositionalChange.Move (pref, pref + 1))
            else
                let mutable fastHandled = false

                if prevLen = nextLen then
                    let firstPrev = Platform.itemAt pref previous
                    let lastPrev = Platform.itemAt (pref + prevLen - 1) previous
                    let firstNext = Platform.itemAt pref next
                    let lastNext = Platform.itemAt (pref + prevLen - 1) next

                    if Platform.keyEquals firstPrev lastNext then
                        let mutable shiftedLeft = true
                        let mutable k = 0

                        while shiftedLeft && k <= prevLen - 2 do
                            if not (Platform.keyEquals (Platform.itemAt (pref + k + 1) previous) (Platform.itemAt (pref + k) next)) then
                                shiftedLeft <- false

                            k <- k + 1

                        if shiftedLeft then
                            edits.Add (PositionalChange.Move (pref, pref + prevLen - 1))
                            fastHandled <- true

                    if
                        not fastHandled
                        && Platform.keyEquals lastPrev firstNext
                    then
                        let mutable shiftedRight = true
                        let mutable k = 0

                        while shiftedRight && k <= prevLen - 2 do
                            if not (Platform.keyEquals (Platform.itemAt (pref + k) previous) (Platform.itemAt (pref + k + 1) next)) then
                                shiftedRight <- false

                            k <- k + 1

                        if shiftedRight then
                            edits.Add (PositionalChange.Move (pref + prevLen - 1, pref))
                            fastHandled <- true

                    if
                        not fastHandled
                        && Platform.keyEquals firstPrev lastNext
                        && Platform.keyEquals lastPrev firstNext
                    then
                        let mutable sameMid = true
                        let mutable k = 1

                        while sameMid && k <= prevLen - 2 do
                            if not (Platform.keyEquals (Platform.itemAt (pref + k) previous) (Platform.itemAt (pref + k) next)) then
                                sameMid <- false

                            k <- k + 1

                        if sameMid then
                            edits.Add (PositionalChange.Move (pref + prevLen - 1, pref))
                            edits.Add (PositionalChange.Move (pref + 1, pref + prevLen - 1))
                            fastHandled <- true

                if not fastHandled then
                    // One more than the key's middle-slice index in `next`; absent keys read as 0, or null under Fable, and fail `> 0`.
                    let target = Platform.KeyMap<'K, int>()
                    target.Reserve nextLen

                    for j in 0 .. nextLen - 1 do
                        target.Set (Platform.itemAt (pref + j) next, j + 1)

                    let rawTargets = Platform.positions prevLen
                    let mutable survivors = 0

                    for i = prevLen - 1 downto 0 do
                        let t = target.Find (Platform.itemAt (pref + i) previous)

                        if t > 0 then
                            Platform.setItemAt i rawTargets t
                            survivors <- survivors + 1
                        else
                            Platform.setItemAt i rawTargets 0
                            edits.Add (PositionalChange.RemoveAt (pref + i))

                    if survivors = 0 then
                        for j in 0 .. nextLen - 1 do
                            edits.Add (PositionalChange.InsertAt (pref + j, Platform.itemAt (pref + j) next))
                    else
                        let positions = Platform.positions survivors
                        let mutable s = 0

                        for i in 0 .. prevLen - 1 do
                            let t = Platform.itemAt i rawTargets

                            if t > 0 then
                                Platform.setItemAt s positions (t - 1)
                                s <- s + 1

                        let staying = longestIncreasing positions
                        // The survivor index of each middle-slice key of `next`, or -1 for an inserted key.
                        let survivorAt = Array.create nextLen -1

                        for idx in 0 .. survivors - 1 do
                            Platform.setItemAt (Platform.itemAt idx positions) survivorAt idx

                        // The target of the first staying survivor after each survivor, or the middle slice length when none follows.
                        let stayingAfter = Platform.positions survivors
                        let mutable upcoming = nextLen

                        for idx = survivors - 1 downto 0 do
                            Platform.setItemAt idx stayingAfter upcoming

                            if Platform.itemAt idx staying then
                                upcoming <- Platform.itemAt idx positions

                        // Survivors not yet placed, by survivor index. Starts all ones: each node holds its range's length.
                        let unplaced = Array.zeroCreate<int>(survivors + 1)

                        for i in 1..survivors do
                            Platform.setItemAt i unplaced (i &&& -i)

                        // The survivor index of the last staying key placed; the keys placed since it follow it contiguously.
                        let mutable anchor = 0

                        for j in 0 .. nextLen - 1 do
                            let sidx = Platform.itemAt j survivorAt

                            if sidx < 0 then
                                edits.Add (PositionalChange.InsertAt (pref + j + sumBelow unplaced anchor, Platform.itemAt (pref + j) next))
                            elif Platform.itemAt sidx staying then
                                addAt unplaced sidx -1
                                anchor <- sidx
                            else
                                let from =
                                    min j (Platform.itemAt sidx stayingAfter)
                                    + sumBelow unplaced sidx

                                addAt unplaced sidx -1
                                let into = j + sumBelow unplaced anchor

                                if from <> into then
                                    edits.Add (PositionalChange.Move (pref + from, pref + into))

        edits
