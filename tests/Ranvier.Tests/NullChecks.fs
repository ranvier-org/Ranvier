module Ranvier.Tests.NullChecks

open System
open System.Collections.Generic
open Expecto
open Ranvier

#if FABLE_COMPILER
[<Fable.Core.Emit("undefined")>]
let private undefinedValue: obj = Fable.Core.Util.jsNative

[<AllowNullLiteral>]
type private Holder() =
    member val Next: Holder = null with get, set
#endif

/// <summary>
/// Tests of <c>Platform.isNull</c> under Fable and null-argument validation on public API entry points.
/// </summary>
[<Tests>]
let tests =
    testList
        "NullChecks"
        [
#if FABLE_COMPILER
            testList
                "Platform.isNull"
                [
                    test "is true for null" {
                        Expect.isTrue (Platform.isNull (null: obj)) "null object"
                        Expect.isTrue (Platform.isNull (null: string)) "null string"
                        Expect.isTrue (Platform.isNull (Holder().Next)) "unset field"
                    }

                    test "is false for a value" {
                        Expect.isFalse (Platform.isNull (box 0)) "boxed zero"
                        Expect.isFalse (Platform.isNull "") "empty string"
                        Expect.isFalse (Platform.isNull (box false)) "boxed false"
                        Expect.isFalse (Platform.isNull (Holder ())) "object"
                        Expect.isFalse (Platform.isNull (Array.empty<int>)) "empty array"
                    }

                    test "is true for undefined, as isNull is" {
                        Expect.isTrue (isNull undefinedValue) "FSharp.Core isNull"
                        Expect.isTrue (Platform.isNull undefinedValue) "Platform.isNull"
                    }
                ]
#endif
            test "createMemoOn and its variants reject null read, compute, or comparer" {
                use g = new Graph ()
                use _ = g.Activate ()
                let nullRead = Unchecked.defaultof<unit -> int>
                let nullCompute = Unchecked.defaultof<int -> int>
                let nullComparer = Unchecked.defaultof<IEqualityComparer<int>>
                let validComparer = EqualityComparer<int>.Default

                Expect.throwsT<ArgumentNullException> (fun () -> createMemoOn nullRead id |> ignore) "createMemoOn null read"
                Expect.throwsT<ArgumentNullException> (fun () -> createMemoOn (fun () -> 1) nullCompute |> ignore) "createMemoOn null compute"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createMemoOnWithComparer nullComparer (fun () -> 1) id
                        |> ignore)
                    "createMemoOnWithComparer null comparer"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createMemoOnWithComparer validComparer nullRead id
                        |> ignore)
                    "createMemoOnWithComparer null read"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createMemoOnWithComparer validComparer (fun () -> 1) nullCompute
                        |> ignore)
                    "createMemoOnWithComparer null compute"

                Expect.throwsT<ArgumentNullException> (fun () -> createMemoWithOn nullRead id |> ignore) "createMemoWithOn null read"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createMemoWithOn (fun () -> 1) nullCompute
                        |> ignore)
                    "createMemoWithOn null compute"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createOwningMemoOnWithComparer nullComparer (fun () -> 1) id
                        |> ignore)
                    "createOwningMemoOnWithComparer null comparer"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createOwningMemoOnWithComparer validComparer nullRead id
                        |> ignore)
                    "createOwningMemoOnWithComparer null read"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createOwningMemoOnWithComparer validComparer (fun () -> 1) nullCompute
                        |> ignore)
                    "createOwningMemoOnWithComparer null compute"
            }

            test "createAsyncOn and createStreamOn families reject null read, compute, fold, or start" {
                use g = new Graph ()
                use _ = g.Activate ()
                let nullRead = Unchecked.defaultof<unit -> int>

                let nullAsyncCompute =
                    Unchecked.defaultof<int -> Previous<int> -> Threading.CancellationToken -> Threading.Tasks.Task<int>>

                let validAsyncCompute =
                    fun (x: int) (_: Previous<int>) (_: Threading.CancellationToken) -> completed x

                let nullFold = Unchecked.defaultof<int -> int -> int>

                let nullStart =
                    Unchecked.defaultof<int -> Threading.CancellationToken -> StreamSource<int>>

                let validStart =
                    fun (_: int) (_: Threading.CancellationToken) ->
                        StreamSource.create (fun _ _ -> Threading.Tasks.TaskCompletionSource<unit>().Task :> Threading.Tasks.Task)

                Expect.throwsT<ArgumentNullException> (fun () -> createAsyncOn nullRead validAsyncCompute |> ignore) "createAsyncOn null read"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createAsyncOn (fun () -> 1) nullAsyncCompute
                        |> ignore)
                    "createAsyncOn null compute"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createAsyncWithOn nullRead validAsyncCompute
                        |> ignore)
                    "createAsyncWithOn null read"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createAsyncWithOn (fun () -> 1) nullAsyncCompute
                        |> ignore)
                    "createAsyncWithOn null compute"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createAsyncOnWithPolicy CancelPrevious nullRead validAsyncCompute
                        |> ignore)
                    "createAsyncOnWithPolicy null read"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createAsyncOnWithPolicy CancelPrevious (fun () -> 1) nullAsyncCompute
                        |> ignore)
                    "createAsyncOnWithPolicy null compute"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createOwningAsyncOnWithPolicy CancelPrevious nullRead validAsyncCompute
                        |> ignore)
                    "createOwningAsyncOnWithPolicy null read"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createOwningAsyncOnWithPolicy CancelPrevious (fun () -> 1) nullAsyncCompute
                        |> ignore)
                    "createOwningAsyncOnWithPolicy null compute"

                Expect.throwsT<ArgumentNullException> (fun () -> createStreamOn nullRead validStart |> ignore) "createStreamOn null read"

                Expect.throwsT<ArgumentNullException> (fun () -> createStreamOn (fun () -> 1) nullStart |> ignore) "createStreamOn null start"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createStreamFoldOn 0 nullFold (fun () -> 1) validStart
                        |> ignore)
                    "createStreamFoldOn null fold"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createStreamFoldOn 0 (+) nullRead validStart
                        |> ignore)
                    "createStreamFoldOn null read"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createStreamFoldOn 0 (+) (fun () -> 1) nullStart
                        |> ignore)
                    "createStreamFoldOn null start"
            }

            test "Signal, Memo, AsyncMemo, AsyncSource, and StreamMemo combinators reject null sources and callbacks" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 1
                let m = createMemo (fun _ -> 1)
                let asrc = createAsyncSource<int>()
                let am = Make.AsyncMemo<int>(g, fun _ _ -> completed 1)
                let emptyFeed = Ranvier.Tests.Streams.ManualStream<int>()
                let sm = createStream (fun _ -> emptyFeed.Source)

                let nullSig = Unchecked.defaultof<Signal<int>>
                let nullMemo = Unchecked.defaultof<Memo<int>>
                let nullAsrc = Unchecked.defaultof<AsyncSource<int>>
                let nullAm = Unchecked.defaultof<AsyncMemo<int>>
                let nullSm = Unchecked.defaultof<StreamMemo<int>>

                let nullMap = Unchecked.defaultof<int -> int>
                let nullMap2 = Unchecked.defaultof<int -> int -> int>
                let nullMemoBind = Unchecked.defaultof<int -> Memo<int>>

                let nullAsyncBind =
                    Unchecked.defaultof<int -> System.Threading.CancellationToken -> System.Threading.Tasks.Task<int>>

                let nullStreamBind =
                    Unchecked.defaultof<int -> System.Threading.CancellationToken -> StreamSource<int>>

                let nullFold = Unchecked.defaultof<int -> int -> int>

                // Signal
                Expect.throwsT<ArgumentNullException> (fun () -> Signal.map nullMap s |> ignore) "Signal.map null mapping"
                Expect.throwsT<ArgumentNullException> (fun () -> Signal.map id nullSig |> ignore) "Signal.map null source"
                Expect.throwsT<ArgumentNullException> (fun () -> Signal.map2 nullMap2 s s |> ignore) "Signal.map2 null mapping"
                Expect.throwsT<ArgumentNullException> (fun () -> Signal.map2 (+) nullSig s |> ignore) "Signal.map2 null source1"
                Expect.throwsT<ArgumentNullException> (fun () -> Signal.map2 (+) s nullSig |> ignore) "Signal.map2 null source2"
                Expect.throwsT<ArgumentNullException> (fun () -> Signal.zip nullSig s |> ignore) "Signal.zip null source1"
                Expect.throwsT<ArgumentNullException> (fun () -> Signal.zip s nullSig |> ignore) "Signal.zip null source2"
                Expect.throwsT<ArgumentNullException> (fun () -> Signal.bind nullMemoBind s |> ignore) "Signal.bind null binder"
                Expect.throwsT<ArgumentNullException> (fun () -> Signal.bind (fun _ -> m) nullSig |> ignore) "Signal.bind null source"

                // Memo
                Expect.throwsT<ArgumentNullException> (fun () -> Memo.map nullMap m |> ignore) "Memo.map null mapping"
                Expect.throwsT<ArgumentNullException> (fun () -> Memo.map id nullMemo |> ignore) "Memo.map null source"
                Expect.throwsT<ArgumentNullException> (fun () -> Memo.map2 nullMap2 m m |> ignore) "Memo.map2 null mapping"
                Expect.throwsT<ArgumentNullException> (fun () -> Memo.map2 (+) nullMemo m |> ignore) "Memo.map2 null source1"
                Expect.throwsT<ArgumentNullException> (fun () -> Memo.map2 (+) m nullMemo |> ignore) "Memo.map2 null source2"
                Expect.throwsT<ArgumentNullException> (fun () -> Memo.zip nullMemo m |> ignore) "Memo.zip null source1"
                Expect.throwsT<ArgumentNullException> (fun () -> Memo.zip m nullMemo |> ignore) "Memo.zip null source2"
                Expect.throwsT<ArgumentNullException> (fun () -> Memo.bind nullMemoBind m |> ignore) "Memo.bind null binder"
                Expect.throwsT<ArgumentNullException> (fun () -> Memo.bind (fun _ -> m) nullMemo |> ignore) "Memo.bind null source"

                // AsyncMemo
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncMemo.map nullMap am |> ignore) "AsyncMemo.map null mapping"
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncMemo.map id nullAm |> ignore) "AsyncMemo.map null source"
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncMemo.map2 nullMap2 am am |> ignore) "AsyncMemo.map2 null mapping"
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncMemo.map2 (+) nullAm am |> ignore) "AsyncMemo.map2 null source1"
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncMemo.map2 (+) am nullAm |> ignore) "AsyncMemo.map2 null source2"
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncMemo.zip nullAm am |> ignore) "AsyncMemo.zip null source1"
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncMemo.zip am nullAm |> ignore) "AsyncMemo.zip null source2"
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncMemo.bind nullAsyncBind am |> ignore) "AsyncMemo.bind null binder"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        AsyncMemo.bind (fun x _ -> completed x) nullAm
                        |> ignore)
                    "AsyncMemo.bind null source"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        AsyncMemo.bindWithPolicy FlightPolicy.CancelPrevious nullAsyncBind am
                        |> ignore)
                    "AsyncMemo.bindWithPolicy null binder"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        AsyncMemo.bindWithPolicy FlightPolicy.CancelPrevious (fun x _ -> completed x) nullAm
                        |> ignore)
                    "AsyncMemo.bindWithPolicy null source"

                // AsyncSource
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncSource.map nullMap asrc |> ignore) "AsyncSource.map null mapping"
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncSource.map id nullAsrc |> ignore) "AsyncSource.map null source"
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncSource.map2 nullMap2 asrc asrc |> ignore) "AsyncSource.map2 null mapping"
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncSource.map2 (+) nullAsrc asrc |> ignore) "AsyncSource.map2 null source1"
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncSource.map2 (+) asrc nullAsrc |> ignore) "AsyncSource.map2 null source2"
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncSource.zip nullAsrc asrc |> ignore) "AsyncSource.zip null source1"
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncSource.zip asrc nullAsrc |> ignore) "AsyncSource.zip null source2"
                Expect.throwsT<ArgumentNullException> (fun () -> AsyncSource.bind nullAsyncBind asrc |> ignore) "AsyncSource.bind null binder"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        AsyncSource.bind (fun x _ -> completed x) nullAsrc
                        |> ignore)
                    "AsyncSource.bind null source"

                // StreamMemo
                Expect.throwsT<ArgumentNullException> (fun () -> StreamMemo.map nullMap sm |> ignore) "StreamMemo.map null mapping"
                Expect.throwsT<ArgumentNullException> (fun () -> StreamMemo.map id nullSm |> ignore) "StreamMemo.map null source"
                Expect.throwsT<ArgumentNullException> (fun () -> StreamMemo.map2 nullMap2 sm sm |> ignore) "StreamMemo.map2 null mapping"
                Expect.throwsT<ArgumentNullException> (fun () -> StreamMemo.map2 (+) nullSm sm |> ignore) "StreamMemo.map2 null source1"
                Expect.throwsT<ArgumentNullException> (fun () -> StreamMemo.map2 (+) sm nullSm |> ignore) "StreamMemo.map2 null source2"
                Expect.throwsT<ArgumentNullException> (fun () -> StreamMemo.zip nullSm sm |> ignore) "StreamMemo.zip null source1"
                Expect.throwsT<ArgumentNullException> (fun () -> StreamMemo.zip sm nullSm |> ignore) "StreamMemo.zip null source2"
                Expect.throwsT<ArgumentNullException> (fun () -> StreamMemo.bind nullStreamBind sm |> ignore) "StreamMemo.bind null binder"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        StreamMemo.bind (fun _ _ -> emptyFeed.Source) nullSm
                        |> ignore)
                    "StreamMemo.bind null source"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        StreamMemo.bindFold 0 nullFold (fun _ _ -> emptyFeed.Source) sm
                        |> ignore)
                    "StreamMemo.bindFold null fold"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        StreamMemo.bindFold 0 (+) nullStreamBind sm
                        |> ignore)
                    "StreamMemo.bindFold null binder"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        StreamMemo.bindFold 0 (+) (fun _ _ -> emptyFeed.Source) nullSm
                        |> ignore)
                    "StreamMemo.bindFold null source"
            }
        ]
