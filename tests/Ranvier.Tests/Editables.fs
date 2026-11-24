module Ranvier.Tests.Editables

open System
open Expecto
open Ranvier

type private Line = { Sku: string; Quantity: int }

/// <summary>Counts the runs of an effect that reads <c>read</c>.</summary>
let private countRuns (read: unit -> 'a) =
    let runs = ref 0

    createEffect (fun () ->
        runs.Value <- runs.Value + 1
        read () |> ignore)

    runs

/// <summary>Expects an <c>ObjectDisposedException</c>; Fable cannot type-test it, so any exception passes there.</summary>
let private throwsDisposed (f: unit -> unit) (message: string) =
#if FABLE_COMPILER
    Expect.throws f message
#else
    Expect.throwsT<ObjectDisposedException> f message
#endif

[<Tests>]
let tests =
    testList
        "Editables"
        [
            test "an editable reads the seed until edited, then the edit" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createSignal 1
                let e = createEditable (fun _ -> source.Value * 10)

                Expect.equal e.Value 10 "the seed's value"
                Expect.isFalse e.IsEdited "unedited"

                e.Value <- 7

                Expect.equal e.Value 7 "the edit"
                Expect.isTrue e.IsEdited "edited"
                Expect.equal e.Upstream 10 "the seed is unchanged"
            }

            test "an upstream change drops the edit of an editable" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createSignal 1
                let e = createEditable (fun _ -> source.Value)
                let seen = ResizeArray<int>()
                createEffect (fun () -> seen.Add e.Value)

                e.Value <- 5
                source.Value <- 2

                Expect.equal e.Value 2 "the seed's new value"
                Expect.isFalse e.IsEdited "the edit is dropped"
                Expect.sequenceEqual seen [ 1; 5; 2 ] "each value reaches the reader"
            }

            test "A -> B -> A observed drops the edit" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createSignal "A"
                let e = createEditable (fun _ -> source.Value)
                countRuns (fun () -> e.Value) |> ignore

                e.Value <- "edit"
                source.Value <- "B"
                source.Value <- "A"

                Expect.equal e.Value "A" "the seed's value"
                Expect.isFalse e.IsEdited "B was published, so the edit is dropped"
            }

            test "A -> B -> A inside a batch keeps the edit" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createSignal "A"
                let e = createEditable (fun _ -> source.Value)
                countRuns (fun () -> e.Value) |> ignore

                e.Value <- "edit"

                batch (fun () ->
                    source.Value <- "B"
                    source.Value <- "A")

                Expect.equal e.Value "edit" "the seed ran once and published an equal value"
                Expect.isTrue e.IsEdited "edited"
            }

            test "A -> B -> A with nothing reading the seed keeps the edit" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createSignal "A"
                let e = createEditable (fun _ -> source.Value)
                e.Value <- "edit"

                source.Value <- "B"
                source.Value <- "A"

                Expect.equal e.Value "edit" "the seed ran once and published an equal value"
            }

            test "a seed that re-runs to an equal value keeps the edit and wakes no reader" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createSignal 1
                let e = createEditable (fun _ -> source.Value % 2)
                let runs = countRuns (fun () -> e.Value)

                e.Value <- 9
                source.Value <- 3

                Expect.equal e.Value 9 "the edit survives an equal seed value"
                Expect.equal runs.Value 2 "one run for the edit, none for the seed re-run"
            }

            test "a seed returning a new but equal record drops the edit under the default policy" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createSignal 1

                let e =
                    createEditable (fun _ ->
                        {
                            Sku = "x"
                            Quantity = source.Value * 0
                        })

                countRuns (fun () -> e.Value) |> ignore

                e.Value <- { Sku = "x"; Quantity = 4 }
                source.Value <- 2

                Expect.equal e.Value.Quantity 0 "a new record is a new value under reference comparison"
                Expect.isFalse e.IsEdited "the edit is dropped"
            }

            test "a seed returning its previous record keeps the edit" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createSignal 1

                let e =
                    createEditable (fun prev ->
                        source.Value |> ignore

                        match prev with
                        | ValueSome line -> line
                        | ValueNone -> { Sku = "x"; Quantity = 1 })

                countRuns (fun () -> e.Value) |> ignore

                e.Value <- { Sku = "x"; Quantity = 4 }
                source.Value <- 2

                Expect.equal e.Value.Quantity 4 "the same record is an unchanged seed"
                Expect.isTrue e.IsEdited "edited"
            }

            test "an equal edit wakes no reader" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createSignal 1
                let e = createEditable (fun _ -> source.Value)
                let runs = countRuns (fun () -> e.Value)

                e.Value <- 5
                e.Value <- 5

                Expect.equal runs.Value 2 "one run for the first edit"
            }

            test "Reset drops the edit" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createSignal 1
                let e = createEditable (fun _ -> source.Value)

                e.Value <- 5
                e.Reset ()

                Expect.equal e.Value 1 "the seed's value"
                Expect.isFalse e.IsEdited "unedited"
            }

            test "IsEdited wakes its reader on edit, on reset and when upstream drops the edit" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createSignal 1
                let e = createEditable (fun _ -> source.Value)
                let seen = ResizeArray<bool>()
                createEffectOn (fun () -> e.IsEdited) seen.Add

                e.Value <- 5
                e.Reset ()
                e.Value <- 6
                source.Value <- 2

                Expect.sequenceEqual seen [ false; true; false; true; false ] "every transition is seen"
            }

            test "the seed receives its own last value" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createSignal 1
                let prevs = ResizeArray<int voption>()

                let e =
                    createEditable (fun prev ->
                        prevs.Add prev
                        source.Value)

                e.Value |> ignore
                e.Value <- 50
                source.Value <- 2
                e.Value |> ignore

                Expect.sequenceEqual prevs [ ValueNone; ValueSome 1 ] "the seed's values, never the edit"
            }

            test "a draft keeps its edit across upstream changes until Reset" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createSignal 1
                let d = createDraft (fun _ -> source.Value)
                let upstream = ResizeArray<int>()
                createEffect (fun () -> upstream.Add d.Upstream)

                d.Value <- 5
                source.Value <- 2

                Expect.equal d.Value 5 "the edit"
                Expect.isTrue d.IsEdited "edited"
                Expect.sequenceEqual upstream [ 1; 2 ] "Upstream tracks the seed"

                d.Reset ()

                Expect.equal d.Value 2 "the seed's value after Reset"
            }

            test "an edit while the seed is pending belongs to the last settled value" {
                use g = new Graph ()
                use _ = g.Activate ()
                let trigger = createSignal 0
                let first = createAsyncSource<int>()
                first.Settle 1
                let later = createAsyncSource<int>()

                let e =
                    createEditable (fun _ -> if trigger.Value = 0 then first.Value else later.Value)

                countRuns (fun () -> e.TryValue) |> ignore

                trigger.Value <- 1
                Expect.equal e.Status Status.Pending "the seed is pending"

                e.Value <- 5
                Expect.equal e.Value 5 "the edit is in force while the seed is pending"

                later.Settle 1
                Expect.equal e.Value 5 "an equal settled value keeps the edit"

                trigger.Value <- 2
                trigger.Value <- 3
                Expect.equal e.Value 5 "an unrelated re-run keeps the edit"
            }

            test "a pending seed that settles to a new value drops the edit" {
                use g = new Graph ()
                use _ = g.Activate ()
                let trigger = createSignal 0
                let first = createAsyncSource<int>()
                first.Settle 1
                let later = createAsyncSource<int>()

                let e =
                    createEditable (fun _ -> if trigger.Value = 0 then first.Value else later.Value)

                countRuns (fun () -> e.TryValue) |> ignore

                trigger.Value <- 1
                e.Value <- 5
                later.Settle 2

                Expect.equal e.Value 2 "the seed's new value"
                Expect.isFalse e.IsEdited "the edit is dropped"
            }

            test "an edit before the seed first settles is dropped by an editable and kept by a draft" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createAsyncSource<int>()
                let e = createEditable (fun _ -> source.Value)
                let d = createDraft (fun _ -> source.Value)

                Expect.equal e.TryValue Pending "the seed is pending"

                e.Value <- 5
                d.Value <- 5
                source.Settle 1

                Expect.equal e.Value 1 "the first settled value drops the edit"
                Expect.equal d.Value 5 "the draft keeps the edit"
            }

            test "disposing the owner detaches the editable from its seed" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = createSignal 1
                let runs = ref 0

                let e, owner =
                    createRoot (fun owner ->
                        createEditable (fun _ ->
                            runs.Value <- runs.Value + 1
                            source.Value),
                        owner)

                e.Value |> ignore
                owner.Dispose ()
                source.Value <- 2
                e.Value |> ignore

                Expect.equal runs.Value 1 "the seed stays detached"
                Expect.equal source.ObserverCount 0 "the source holds no edge to the seed"
            }

            test "a keyed collection whose selector disposes it raises and stays empty" {
                use g = new Graph ()
                use _ = g.Activate ()
                let armed = ref false
                let cell = ref Unchecked.defaultof<KeyedCollection<int, int * int>>

                let col =
                    createKeyedCollection (fun (k, _) ->
                        if armed.Value then
                            cell.Value.Dispose ()

                        k)

                cell.Value <- col
                col.AddOrUpdate (1, 1)
                armed.Value <- true

                throwsDisposed (fun () -> col.AddOrUpdate (2, 2)) "the disposal during the selector is seen after it"
                Expect.isEmpty col.Rows.Keys "no zombie key survives the disposal"
                Expect.isNone (col.Rows.TryGet 2) "the key of the failed upsert has no row"
                throwsDisposed (fun () -> col.AddOrUpdate (3, 3)) "the next upsert raises before its selector"
            }

            test "a selector that upserts the same key leaves one row holding the outer value" {
                use g = new Graph ()
                use _ = g.Activate ()
                let armed = ref true
                let cell = ref Unchecked.defaultof<KeyedCollection<int, int * int>>

                let col =
                    createKeyedCollection (fun (k, _) ->
                        if armed.Value then
                            armed.Value <- false
                            cell.Value.AddOrUpdate (k, 99)

                        k)

                cell.Value <- col
                let runs = countRuns (fun () -> col.Rows.Keys)

                col.AddOrUpdate (1, 1)

                Expect.sequenceEqual col.Rows.Keys [| 1 |] "one row for the key"
                Expect.equal (col.Rows.Get 1) (1, 1) "the outer upsert is applied last"
                Expect.equal runs.Value 2 "the nested and outer upserts reach the key reader as one change"
            }

            test "a selector that writes and then throws inside an outer batch keeps the write and the batch" {
                use g = new Graph ()
                use _ = g.Activate ()
                let cell = ref Unchecked.defaultof<KeyedCollection<int, int * int>>

                let col =
                    createKeyedCollection (fun (k, v) ->
                        if v = 20 then
                            cell.Value.AddOrUpdate (3, 30)
                            failwith "selector failed"

                        k)

                cell.Value <- col
                let runs = countRuns (fun () -> col.Rows.Keys)

                col.Edit (fun c ->
                    c.AddOrUpdate (1, 10)
                    Expect.throws (fun () -> c.AddOrUpdate (2, 20)) "the selector's failure reaches the edit"
                    Expect.equal runs.Value 1 "the reader waits for the outer batch"
                    c.AddOrUpdate (4, 40))

                Expect.sequenceEqual col.Rows.Keys [| 1; 3; 4 |] "the writes before the failure and the nested write survive; the failed key is absent"
                Expect.equal runs.Value 2 "the outer batch flushes once"
            }

            test "an upsert of a removed key adds a new row at the end" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)
                col.AddOrUpdate (1, 1)
                col.AddOrUpdate (2, 2)
                col.AddOrUpdate (1, 10)

                Expect.isTrue (col.Remove 1) "the key was present"
                col.AddOrUpdate (1, 11)

                Expect.sequenceEqual col.Rows.Keys [| 2; 1 |] "the key returns at the end"
                Expect.equal (col.Rows.Get 1) (1, 11) "the new row holds the upserted value"
            }

            test "an upsert after Clear adds its key again" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)
                col.AddOrUpdate (1, 1)
                col.AddOrUpdate (1, 2)
                let runs = countRuns (fun () -> col.Rows.Keys)

                col.Clear ()
                col.AddOrUpdate (1, 3)

                Expect.sequenceEqual col.Rows.Keys [| 1 |] "the key is live again"
                Expect.equal (col.Rows.Get 1) (1, 3) "the new row holds the upserted value"
                Expect.equal runs.Value 3 "the clear and the upsert each reach the key reader"
            }

            test "a selector that removes its own key leaves the upsert a new row at the end" {
                use g = new Graph ()
                use _ = g.Activate ()
                let armed = ref false
                let cell = ref Unchecked.defaultof<KeyedCollection<int, int * int>>

                let col =
                    createKeyedCollection (fun (k, _) ->
                        if armed.Value then
                            cell.Value.Remove k |> ignore

                        k)

                cell.Value <- col
                col.AddOrUpdate (1, 1)
                col.AddOrUpdate (2, 2)
                col.AddOrUpdate (1, 10)
                armed.Value <- true

                col.AddOrUpdate (1, 11)

                Expect.sequenceEqual col.Rows.Keys [| 2; 1 |] "the removal inside the selector is applied before the upsert"
                Expect.equal (col.Rows.Get 1) (1, 11) "the new row holds the upserted value"
            }

            test "upserts alternating between two keys reach each key's row" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)
                col.AddOrUpdate (1, 0)
                col.AddOrUpdate (2, 0)
                let first = ResizeArray<int>()
                let second = ResizeArray<int>()
                createEffectOn (fun () -> snd (col.Rows.Get 1)) first.Add
                createEffectOn (fun () -> snd (col.Rows.Get 2)) second.Add

                for i in 1..3 do
                    col.AddOrUpdate (1, i)
                    col.AddOrUpdate (2, i * 10)

                Expect.sequenceEqual first [ 0; 1; 2; 3 ] "every upsert of the first key reaches its row"
                Expect.sequenceEqual second [ 0; 10; 20; 30 ] "every upsert of the second key reaches its row"
            }

            test "a keyed collection upserts a None key as any other key" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int option, _: int) -> k)
                col.AddOrUpdate (None, 1)
                col.AddOrUpdate (None, 2)
                col.AddOrUpdate (Some 1, 3)
                col.AddOrUpdate (None, 4)

                Expect.sequenceEqual col.Rows.Keys [| None; Some 1 |] "one row per key, in insertion order"
                Expect.equal (col.Rows.Get None) (None, 4) "the None key holds its last value"
                Expect.equal (col.Rows.Get (Some 1)) (Some 1, 3) "the other key is untouched by the later upsert"
            }

            test "the snapshot of a keyed collection holds each key's last value" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)
                col.AddOrUpdate (1, 1)
                col.AddOrUpdate (2, 2)
                col.AddOrUpdate (1, 3)

                let snapshot = col.Rows.Snapshot

                Expect.equal snapshot.Count 2 "every row is settled"
                Expect.equal snapshot[1] (1, 3) "the edited key"
                Expect.equal snapshot[2] (2, 2) "the unedited key"
            }

            test "a fold over a keyed collection follows value edits between key changes" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)

                let total =
                    col.Rows
                    |> Projection.foldGroup (fun n (_, p) -> n + p) (fun n (_, p) -> n - p) 0

                let seen = ResizeArray<int>()
                createEffectOn (fun () -> total.Value) seen.Add

                col.AddOrUpdate (1, 5)
                col.AddOrUpdate (2, 7)
                col.AddOrUpdate (1, 6)
                col.AddOrUpdate (1, 8)
                col.AddOrUpdate (3, 1)
                col.AddOrUpdate (3, 2)
                col.Remove 2 |> ignore
                col.AddOrUpdate (1, 1)

                col.Edit (fun c ->
                    c.Remove 1 |> ignore
                    c.AddOrUpdate (1, 4))

                col.AddOrUpdate (1, 5)
                col.Clear ()
                col.AddOrUpdate (9, 9)

                Expect.sequenceEqual
                    seen
                    [ 0; 5; 12; 13; 15; 16; 17; 10; 3; 6; 7; 0; 9 ]
                    "each addition, edit, removal, replacement and clear moves the total once"
            }

            test "a fold read after key changes and value edits it did not observe holds the current total" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)
                col.AddOrUpdate (1, 1)

                let fold () =
                    col.Rows
                    |> Projection.foldGroup (fun n (_, p) -> n + p) (fun n (_, p) -> n - p) 0

                let watched = fold ()
                let pulled = fold ()
                countRuns (fun () -> watched.Value) |> ignore
                Expect.equal pulled.Value 1 "the first read"

                col.AddOrUpdate (1, 2)
                col.AddOrUpdate (2, 10)
                col.AddOrUpdate (2, 20)
                col.Remove 1 |> ignore
                Expect.equal pulled.Value 20 "an addition, two edits and a removal since the last read"

                col.AddOrUpdate (2, 30)
                Expect.equal pulled.Value 30 "a value edit alone"

                col.AddOrUpdate (3, 1)
                Expect.equal pulled.Value 31 "an addition after a read that saw no key change"
                Expect.equal watched.Value 31 "the observed fold agrees"
            }

            test "a fold whose add reads a signal folds again when the signal changes after a value edit" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)
                let weight = createSignal 1

                let total =
                    col.Rows
                    |> Projection.foldGroup (fun n (_, p) -> n + p * weight.Value) (fun n (_, p) -> n - p * weight.Value) 0

                let seen = ResizeArray<int>()
                createEffectOn (fun () -> total.Value) seen.Add

                col.AddOrUpdate (1, 2)
                col.AddOrUpdate (2, 3)
                col.AddOrUpdate (1, 4)
                weight.Value <- 10
                col.AddOrUpdate (2, 1)

                Expect.sequenceEqual seen [ 0; 2; 5; 7; 70; 50 ] "the edits step the total and the weight folds every row again"
            }

            test "a fold whose subtract throws on a value edit folds the row values again" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)
                let armed = ref false

                let total =
                    col.Rows
                    |> Projection.foldGroup
                        (fun n (_, p) -> n + p)
                        (fun n (_, p) ->
                            if armed.Value then
                                failwith "subtract failed"

                            n - p)
                        0

                let seen = ResizeArray<int>()
                createEffectOn (fun () -> total.Value) seen.Add

                col.AddOrUpdate (1, 2)
                col.AddOrUpdate (2, 3)
                armed.Value <- true
                col.AddOrUpdate (1, 10)
                armed.Value <- false
                col.AddOrUpdate (2, 4)

                Expect.sequenceEqual seen [ 0; 2; 5; 13; 14 ] "the failed step leaves the total to a fold of the row values"
            }

            test "a fold whose add reads a pending source on a value edit is pending until an edit avoids the read" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)
                let later = createAsyncSource<int>()

                let total =
                    col.Rows
                    |> Projection.foldGroup (fun n (_, p) -> if p > 100 then n + later.Value else n + p) (fun n (_, p) -> n - p) 0

                countRuns (fun () -> total.TryValue) |> ignore
                col.AddOrUpdate (1, 2)
                col.AddOrUpdate (2, 3)
                Expect.equal total.Value 5 "both rows are folded"

                col.AddOrUpdate (1, 200)
                Expect.equal total.TryValue Pending "the step's pending read suspends the fold"

                col.AddOrUpdate (1, 5)
                Expect.equal total.Value 8 "the total is a fold of the row values, without the suspended step"
            }

            test "a fold over a view that keeps its key array follows a key replaced beside a value edit" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)
                col.AddOrUpdate (1, 2)
                col.AddOrUpdate (2, 4)
                col.AddOrUpdate (3, 6)
                let sorted = col.Rows |> Projection.sortBy fst

                let total =
                    sorted
                    |> Projection.foldGroup (fun n (_, p) -> n + p) (fun n (_, p) -> n - p) 0

                let seen = ResizeArray<int>()
                createEffectOn (fun () -> total.Value) seen.Add
                let keys = sorted.Keys

                // The edit of key 1 makes the fold read again after the view's pass replaced the row of key 3.
                col.Edit (fun c ->
                    c.Remove 3 |> ignore
                    c.AddOrUpdate (3, 100)
                    c.AddOrUpdate (1, 20))

                Expect.isTrue (LanguagePrimitives.PhysicalEquality keys sorted.Keys) "the replacement leaves the view its key array"
                Expect.sequenceEqual seen [ 12; 124 ] "the replaced row's value and the edit enter the total together"

                col.Edit (fun c ->
                    c.Remove 3 |> ignore
                    c.AddOrUpdate (3, 1000))

                Expect.sequenceEqual seen [ 12; 124; 1024 ] "a replacement alone enters the total"
            }

            test "a fold over a mapped view folds the rows that replaced its own when a signal its add reads changes" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)

                col.Edit (fun c ->
                    for k in 1..70 do
                        c.AddOrUpdate (k, 1))

                let mapped = col.Rows |> Projection.map id
                let weight = createSignal 1

                let total =
                    mapped
                    |> Projection.foldGroup (fun n (_, p) -> n + p * weight.Value) (fun n (_, p) -> n - p * weight.Value) 0

                countRuns (fun () -> total.Value) |> ignore
                Expect.equal total.Value 70 "every row is folded"
                let keys = mapped.Keys

                // More key changes than a key reader holds, so the view rebuilds each row over an unchanged key sequence.
                col.Edit (fun c ->
                    for k in 1..70 do
                        c.Remove k |> ignore

                    for k in 1..70 do
                        c.AddOrUpdate (k, 2))

                Expect.isTrue (LanguagePrimitives.PhysicalEquality keys mapped.Keys) "the rebuild leaves the view its key array"
                weight.Value <- 10
                Expect.equal total.Value 1400 "the fold reads the rows that replaced its own"

                col.AddOrUpdate (1, 3)
                Expect.equal total.Value 1410 "an edit steps the total from the replacement rows"
            }

            test "a fold whose add swallows a pending read on a value edit is pending" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)
                let later = createAsyncSource<int>()

                let add n (_: int, p) =
                    if p > 100 then
                        try
                            n + later.Value
                        with _ ->
                            n
                    else
                        n + p

                let total =
                    col.Rows
                    |> Projection.foldGroup add (fun n (_, p) -> n - p) 0

                countRuns (fun () -> total.TryValue) |> ignore
                col.AddOrUpdate (1, 2)
                col.AddOrUpdate (2, 3)
                Expect.equal total.Value 5 "both rows are folded"

                col.AddOrUpdate (1, 200)
                Expect.equal total.TryValue Pending "a pending read suspends the fold whatever add returns"

                col.AddOrUpdate (1, 5)
                Expect.equal total.Value 8 "the total is a fold of the row values"
            }

            test "disposing a keyed collection returns its fold to zero" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)

                let total =
                    col.Rows
                    |> Projection.foldGroup (fun n (_, p) -> n + p) (fun n (_, p) -> n - p) 0

                let seen = ResizeArray<int>()
                createEffectOn (fun () -> total.Value) seen.Add
                col.AddOrUpdate (1, 5)
                col.AddOrUpdate (1, 6)

                col.Dispose ()

                Expect.sequenceEqual seen [ 0; 5; 6; 0 ] "the disposal empties the fold after a value edit"
            }

            test "a keyed collection created under a disposed owner is disposed at once" {
                use g = new Graph ()
                use _ = g.Activate ()
                let owner = createRoot id
                owner.Dispose ()

                let col =
                    runWithOwner owner (fun () -> createKeyedCollection (fun (k: int, _: int) -> k))

                throwsDisposed (fun () -> col.AddOrUpdate (1, 1)) "the collection was disposed as it attached"
                Expect.isEmpty col.Rows.Keys "a disposed collection reads as empty"
                Expect.equal owner.ErrorCount 0 "the disposal raised nothing"
            }

            test "a projection and its views created under a disposed owner read as empty" {
                use g = new Graph ()
                use _ = g.Activate ()
                let items = createSignal [ 1; 2; 3 ]
                let live = createProjection id (fun x -> x * 10) (fun () -> items.Value)
                let owner = createRoot id
                owner.Dispose ()

                let direct =
                    runWithOwner owner (fun () -> createProjection id (fun x -> x * 10) (fun () -> items.Value))

                let mapped = runWithOwner owner (fun () -> live |> Projection.map (fun v -> v + 1))

                let filtered =
                    runWithOwner owner (fun () -> live |> Projection.filter (fun v -> v > 10))

                let sorted = runWithOwner owner (fun () -> live |> Projection.sortBy (fun v -> -v))

                Expect.isEmpty direct.Keys "the projection was disposed as it attached"
                Expect.isEmpty mapped.Keys "the mapped view was disposed as it attached"
                Expect.isEmpty filtered.Keys "the filtered view was disposed as it attached"
                Expect.isEmpty sorted.Keys "the sorted view was disposed as it attached"
                Expect.equal owner.ErrorCount 0 "the disposals raised nothing"
                Expect.sequenceEqual live.Keys [| 1; 2; 3 |] "the upstream projection keeps its rows"
                Expect.equal (live.Get 2) 20 "and their values"
            }

            test "an Edit inside an Edit applies its removals and upserts in order, in one flush" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)
                let runs = countRuns (fun () -> col.Rows.Keys)

                col.Edit (fun c ->
                    c.AddOrUpdate (1, 1)
                    c.AddOrUpdate (2, 2)

                    c.Edit (fun inner ->
                        Expect.isTrue (inner.Remove 1) "the nested removal finds the key the outer edit added"
                        Expect.isFalse (inner.Remove 9) "an absent key reports false"
                        inner.AddOrUpdate (3, 3))

                    Expect.equal runs.Value 1 "the reader waits for the outer edit"
                    c.AddOrUpdate (2, 20))

                Expect.sequenceEqual col.Rows.Keys [| 2; 3 |] "the nested removal and upsert are applied between the outer ones"
                Expect.equal (col.Rows.Get 2) (2, 20) "the upsert after the nested edit is applied last"
                Expect.equal runs.Value 2 "the outer edit flushes once"
            }

            test "a Clear and an upsert inside an Edit reach the key reader once" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)
                col.AddOrUpdate (1, 1)
                col.AddOrUpdate (2, 2)
                let runs = countRuns (fun () -> col.Rows.Keys)

                col.Edit (fun c ->
                    c.Clear ()
                    c.AddOrUpdate (2, 20)
                    Expect.isFalse (c.Remove 1) "the cleared key is absent")

                Expect.sequenceEqual col.Rows.Keys [| 2 |] "the key upserted after the clear is the only row"
                Expect.equal (col.Rows.Get 2) (2, 20) "it holds the upserted value"
                Expect.equal runs.Value 2 "the edit flushes once"
            }

            test "an Edit whose callback throws keeps the edits before the throw and the collection usable" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)

                Expect.throws
                    (fun () ->
                        col.Edit (fun c ->
                            c.AddOrUpdate (1, 1)
                            failwith "edit failed"))
                    "the callback's failure reaches the caller"

                col.AddOrUpdate (2, 2)
                Expect.isTrue (col.Remove 1) "the upsert before the throw was applied"
                Expect.sequenceEqual col.Rows.Keys [| 2 |] "later edits apply as usual"
            }

            test "a fold over a projection raises a failed row's exception and recovers with the row" {
                use g = new Graph ()
                use _ = g.Activate ()
                let items = createSignal [ 1; 2 ]
                let bad = createSignal false

                let proj =
                    createProjection id (fun x -> if x = 2 && bad.Value then failwith "row failed" else x * 10) (fun () -> items.Value)

                let total = proj |> Projection.foldGroup (+) (-) 0
                countRuns (fun () -> total.TryValue) |> ignore
                Expect.equal total.Value 30 "both rows are folded"

                bad.Value <- true

                match total.TryValue with
                | Failed ex -> Expect.equal ex.Message "row failed" "the fold raises the row's exception"
                | other -> failtestf "expected the row's failure, got %A" other

                bad.Value <- false
                Expect.equal total.Value 30 "the recovered row's value is folded again"

                items.Value <- [ 1; 2; 3 ]
                Expect.equal total.Value 60 "an added row steps the total"
            }

            test "a fold over a projection keeps a pending row's last value and follows its settle" {
                use g = new Graph ()
                use _ = g.Activate ()
                let items = createSignal [ 1; 2 ]
                let wait = createSignal false
                let later = createAsyncSource<int>()

                let proj =
                    createProjection id (fun x -> if x = 2 && wait.Value then later.Value else x * 10) (fun () -> items.Value)

                let total = proj |> Projection.foldGroup (+) (-) 0
                countRuns (fun () -> total.TryValue) |> ignore
                Expect.equal total.Value 30 "both rows are folded"

                wait.Value <- true
                Expect.equal total.Value 30 "the pending row keeps its last settled value in the total"

                later.Settle 7
                Expect.equal total.Value 17 "the settled value replaces it"
            }

            test "a fold without a subtract folds the row values once for each value edit" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (k: int, _: int) -> k)
                col.AddOrUpdate (1, 2)
                col.AddOrUpdate (2, 3)
                let folds = ref 0

                let total =
                    col.Rows
                    |> Projection.fold
                        (fun n (k, p) ->
                            if k = 1 then
                                folds.Value <- folds.Value + 1

                            n + p)
                        0

                let seen = ResizeArray<int>()
                createEffectOn (fun () -> total.Value) seen.Add
                let first = folds.Value

                col.AddOrUpdate (1, 10)
                Expect.equal folds.Value (first + 1) "a changed value folds the rows once"
                col.AddOrUpdate (2, 4)
                Expect.equal folds.Value (first + 2) "and so does the next"

                Expect.sequenceEqual seen [ 5; 13; 14 ] "each changed value reaches the total"
            }

#if !FABLE_COMPILER
            // .NET only: JavaScript exposes no allocation counter.
            untracedOnly
            <| test "an existing-key upsert of a keyed collection allocates nothing" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (struct (k: int, _: int)) -> k)
                col.AddOrUpdate (struct (1, 0))
                let sum = ref 0
                createEffect (fun () -> sum.Value <- sum.Value + (let struct (_, v) = col.Rows.Get 1 in v))

                for i in 1..100 do
                    col.AddOrUpdate (struct (1, i))

                let before = GC.GetAllocatedBytesForCurrentThread ()

                for i in 1..10_000 do
                    col.AddOrUpdate (struct (1, i + 100))

                let perEdit = (GC.GetAllocatedBytesForCurrentThread () - before) / 10_000L
                Expect.equal perEdit 0L $"an upsert allocated %d{perEdit} bytes"
            }

            untracedOnly
            <| test "an existing-key upsert read by a fold, the fold's effect and a row effect allocates nothing" {
                use g = new Graph ()
                use _ = g.Activate ()
                let col = createKeyedCollection (fun (struct (k: int, _: int)) -> k)

                col.Edit (fun c ->
                    for k in 0..63 do
                        c.AddOrUpdate (struct (k, 5)))

                let total =
                    col.Rows
                    |> Projection.foldGroup (fun n (struct (_, p)) -> n + p) (fun n (struct (_, p)) -> n - p) 0

                let row k =
                    let struct (_, p) = col.Rows.Get k
                    p

                let sum = ref 0
                createEffectOn (fun () -> total.Value) (fun n -> sum.Value <- sum.Value + n + row 7)

                for k in 0..63 do
                    createEffectOn (fun () -> row k) (fun p -> sum.Value <- sum.Value + p + total.Value)

                // Each key is written twice in a row, away from its value and back, as the keys rotate.
                let edit i =
                    col.AddOrUpdate (struct ((i / 2 * 37) % 64, (if i % 2 = 0 then 7 else 5)))

                for i in 0..999 do
                    edit i

                let before = GC.GetAllocatedBytesForCurrentThread ()

                for i in 1000..10_999 do
                    edit i

                let perEdit =
                    (GC.GetAllocatedBytesForCurrentThread () - before)
                    / 10_000L

                Expect.equal perEdit 0L $"an upsert allocated %d{perEdit} bytes"
            }

            untracedOnly
            <| test "an edit of an int allocates no more than a write through a chain of two memos" {
                let perWrite (setup: unit -> int -> unit) =
                    let g = new Graph ()
                    use _ = g.Activate ()
                    let write = setup ()

                    for i in 1..100 do
                        write i

                    let before = GC.GetAllocatedBytesForCurrentThread ()

                    for i in 1..10_000 do
                        write (i + 100)

                    (GC.GetAllocatedBytesForCurrentThread () - before)
                    / 10_000L

                let chain =
                    perWrite (fun () ->
                        let s = createSignal 0
                        let a = createMemo (fun _ -> s.Value)
                        let b = createMemo (fun _ -> a.Value)
                        let mutable sum = 0
                        createEffect (fun () -> sum <- sum + b.Value)
                        fun i -> s.Value <- i)

                let edit =
                    perWrite (fun () ->
                        let source = createSignal 1
                        let e = createEditable (fun _ -> source.Value)
                        let mutable sum = 0
                        createEffect (fun () -> sum <- sum + e.Value)
                        fun i -> e.Value <- i)

                Expect.isLessThanOrEqual edit chain $"an edit allocated %d{edit} bytes, a two-memo chain write %d{chain}"
            }
#endif
        ]
