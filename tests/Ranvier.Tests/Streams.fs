module Ranvier.Tests.Streams

open System
open System.Threading
open System.Threading.Tasks
open Expecto
open Ranvier

/// <summary>One start of a <c>ManualStream</c>: its sink, its token and the task the test completes.</summary>
type StreamStart<'T> =
    {
        Sink: StreamSink<'T>
        Token: CancellationToken
        Done: TaskCompletionSource<unit>
    }

/// <summary>A stream source the test drives: every start records its sink, token and completion.</summary>
type ManualStream<'T>() =
    let starts = ResizeArray<StreamStart<'T>>()

    /// <summary>A source whose every <c>Run</c> records a start; the same object serves every restart.</summary>
    member val Source =
        { new StreamSource<'T>() with
            member _.Run(sink, token) =
                let start =
                    {
                        Sink = sink
                        Token = token
                        Done = TaskCompletionSource<unit>()
                    }

                starts.Add start
                start.Done.Task :> Task
        }

    member _.Starts = starts.Count
    member _.Latest = starts[starts.Count - 1]

    member _.At(index: int) =
        starts[index]

    member this.Push(item: 'T) =
        this.Latest.Sink.Next item

    member this.Complete() =
        this.Latest.Done.SetResult ()

    member this.Fail(error: exn) =
        this.Latest.Done.SetException error

let private reading (memo: StreamMemo<'S>) =
    memo.TryValue

[<Tests>]
let tests =
    testList
        "Stream memos"
        [
            testCase "a stream starts on its first read, not at creation"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let memo = graph.Run (fun () -> Api.createStream (fun _ -> feed.Source))
                Expect.equal feed.Starts 0 "not started at creation"
                Expect.equal (reading memo) Pending "pending before the first item"
                Expect.equal feed.Starts 1 "started by the read"

            testCase "an item publishes the latest state; a fold accumulates"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<string>()
                let latest = graph.Run (fun () -> Api.createStream (fun _ -> feed.Source))

                let joined =
                    graph.Run (fun () -> Api.createStreamFold "" (fun state item -> state + item) (fun _ -> feed.Source))

                reading latest |> ignore
                reading joined |> ignore
                feed.At(0).Sink.Next "a"
                feed.At(0).Sink.Next "b"
                feed.At(1).Sink.Next "x"
                feed.At(1).Sink.Next "y"
                Expect.equal (reading latest) (Ready "b") "latest item"
                Expect.equal (reading joined) (Ready "xy") "folded items"

            testCase "an effect sees one run per item on the graph thread, and one per batch"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let memo = graph.Run (fun () -> Api.createStreamFold 0 (+) (fun _ -> feed.Source))
                let seen = ResizeArray<int>()

                new Effect (
                    graph,
                    fun () ->
                        match memo.TryValue with
                        | Ready v -> seen.Add v
                        | _ -> ()
                )
                |> ignore

                feed.Push 1
                feed.Push 2

                graph.Run (fun () ->
                    Api.batch (fun () ->
                        feed.Push 3
                        feed.Push 4))

                Expect.sequenceEqual seen [ 1; 3; 10 ] "one run per item outside a batch, one for the batch"

            testCase "items delivered while Run has not returned publish when it returns"
            <| fun _ ->
                use graph = new Graph ()

                let eager =
                    { new StreamSource<int>() with
                        member _.Run(sink, _) =
                            sink.Next 1
                            sink.Next 2
                            TaskCompletionSource<unit>().Task :> Task
                    }

                let memo = graph.Run (fun () -> Api.createStreamFold 0 (+) (fun _ -> eager))
                Expect.equal (reading memo) (Ready 3) "the synchronous prefix is published by the first read"

            testCase "completion keeps the last state"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let memo = graph.Run (fun () -> Api.createStream (fun _ -> feed.Source))
                reading memo |> ignore
                feed.Push 5
                feed.Complete ()
                Expect.equal (reading memo) (Ready 5) "ready with the last item"

            testCase "empty completion gives seed for a fold and InvalidOperationException for createStream"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let latest = graph.Run (fun () -> Api.createStream (fun _ -> feed.Source))
                let folded = graph.Run (fun () -> Api.createStreamFold 7 (+) (fun _ -> feed.Source))
                reading latest |> ignore
                reading folded |> ignore
                feed.At(0).Done.SetResult()
                feed.At(1).Done.SetResult()

                match reading latest with
                | Failed (:? InvalidOperationException) -> ()
                | other -> failtestf "expected InvalidOperationException, got %A" other

                Expect.equal (reading folded) (Ready 7) "seed"

            testCase "a stream failure publishes Failed with the stream memo as origin"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let memo = graph.Run (fun () -> Api.createStream (fun _ -> feed.Source))
                reading memo |> ignore
                feed.Push 1
                let boom = Exception "boom"
                feed.Fail boom

                match reading memo with
                | Failed error -> Expect.equal error.Message "boom" "the stream's exception"
                | other -> failtestf "expected Failed, got %A" other

                Expect.isTrue (obj.ReferenceEquals (memo.ErrorOrigin, memo)) "origin"

            testCase "a dependency change cancels the stream and restarts it from seed"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let topic = Signal (graph, "a")

                let memo =
                    graph.Run (fun () ->
                        Api.createStreamFold 0 (+) (fun _ ->
                            topic.Value |> ignore
                            feed.Source))

                reading memo |> ignore
                feed.Push 4
                let first = feed.Latest
                topic.Value <- "b"
                Expect.equal (reading memo) Pending "pending until the new stream's first item"
                Expect.isTrue first.Token.IsCancellationRequested "the old token is cancelled"
                Expect.equal feed.Starts 2 "restarted"
                first.Sink.Next 100
                feed.Push 1
                Expect.equal (reading memo) (Ready 1) "reset to seed; the old sink's item is ignored"

            testCase "a start that returns the same StreamSource object on every restart"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let topic = Signal (graph, 0)

                let memo =
                    graph.Run (fun () ->
                        Api.createStream (fun _ ->
                            topic.Value |> ignore
                            feed.Source))

                reading memo |> ignore
                topic.Value <- 1
                reading memo |> ignore
                Expect.isFalse (obj.ReferenceEquals (feed.At(0).Sink, feed.At(1).Sink)) "a fresh sink per start"
                feed.At(0).Sink.Next 9
                Expect.equal (reading memo) Pending "the first start's sink is closed"
                feed.At(1).Sink.Next 2
                Expect.equal (reading memo) (Ready 2) "the second start's sink publishes"

            testCase "a pending read in start keeps the node pending and starts it when the read settles"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let gate = AsyncSource<int>(graph)

                let memo =
                    graph.Run (fun () ->
                        Api.createStream (fun _ ->
                            gate.Value |> ignore
                            feed.Source))

                let mutable runs = 0

                new Effect (
                    graph,
                    fun () ->
                        memo.TryValue |> ignore
                        runs <- runs + 1
                )
                |> ignore

                Expect.equal feed.Starts 0 "start suspended on the gate"
                gate.Settle 1
                Expect.equal feed.Starts 1 "started once the gate settled"

            testCase "a misbehaving source's items after completion are ignored"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let memo = graph.Run (fun () -> Api.createStream (fun _ -> feed.Source))
                reading memo |> ignore
                feed.Push 1
                feed.Complete ()
                feed.Push 2
                Expect.equal (reading memo) (Ready 1) "nothing republished after completion"

            testCase "Run throwing synchronously, and Run returning null, both publish Failed"
            <| fun _ ->
                use graph = new Graph ()

                let throwing =
                    { new StreamSource<int>() with
                        member _.Run(_, _) =
                            raise (Exception "sync")
                    }

                let nothing =
                    { new StreamSource<int>() with
                        member _.Run(_, _) = null
                    }

                let a = graph.Run (fun () -> Api.createStream (fun _ -> throwing))
                let b = graph.Run (fun () -> Api.createStream (fun _ -> nothing))

                match reading a, reading b with
                | Failed error, Failed (:? InvalidOperationException) -> Expect.equal error.Message "sync" "thrown"
                | other -> failtestf "expected two failures, got %A" other

            testCase "a fold that throws on the second item"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let topic = Signal (graph, 0)

                let memo =
                    graph.Run (fun () ->
                        Api.createStreamFold 0 (fun state item -> if item = 2 then failwith "fold" else state + item) (fun _ ->
                            topic.Value |> ignore
                            feed.Source))

                reading memo |> ignore
                feed.Push 1
                feed.Push 2

                match reading memo with
                | Failed error -> Expect.equal error.Message "fold" "fold's exception"
                | other -> failtestf "expected Failed, got %A" other

                Expect.isTrue feed.Latest.Token.IsCancellationRequested "token cancelled"
                topic.Value <- 1
                reading memo |> ignore
                feed.Push 3
                Expect.equal (reading memo) (Ready 3) "a fresh stream from seed"

            testCase "an equal published state notifies no reader"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let memo = graph.Run (fun () -> Api.createStream (fun _ -> feed.Source))
                let mutable runs = 0

                new Effect (
                    graph,
                    fun () ->
                        memo.TryValue |> ignore
                        runs <- runs + 1
                )
                |> ignore

                feed.Push 1
                let after = runs
                feed.Push 1
                Expect.equal runs after "equal state, no run"

            testCase "disposing a pending stream memo publishes ObjectDisposedException and cancels the token"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let memo = graph.Run (fun () -> Api.createStream (fun _ -> feed.Source))
                reading memo |> ignore
                memo.Dispose ()
                Expect.isTrue feed.Latest.Token.IsCancellationRequested "cancelled"

                match reading memo with
                | Failed error when isDisposedError error -> ()
                | other -> failtestf "expected ObjectDisposedException, got %A" other

                feed.Push 1
                Expect.isTrue (memo.TryValue.IsFailed) "items after disposal are ignored"

            testCase "owner disposal disposes the stream memo"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()

                let owner, memo =
                    graph.Run (fun () -> Api.createRoot (fun owner -> owner, Api.createStream (fun _ -> feed.Source)))

                reading memo |> ignore
                owner.Dispose ()
                Expect.isTrue feed.Latest.Token.IsCancellationRequested "cancelled with its owner"

            testCase "an effect that disposes the stream memo while reacting to its publication"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let memo = graph.Run (fun () -> Api.createStream (fun _ -> feed.Source))
                let seen = ResizeArray<int>()

                new Effect (
                    graph,
                    fun () ->
                        match memo.TryValue with
                        | Ready v ->
                            seen.Add v
                            memo.Dispose ()
                        | _ -> ()
                )
                |> ignore

                feed.Push 1
                feed.Push 2
                Expect.sequenceEqual seen [ 1 ] "no publication after disposal"

            testCase "a pure memo that creates a stream memo fails"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()

                let bad =
                    graph.Run (fun () ->
                        Api.createMemo (fun _ ->
                            Api.createStream (fun _ -> feed.Source) |> ignore
                            0))

                Expect.throwsT<InvalidOperationException> (fun () -> graph.Run (fun () -> bad.Value) |> ignore) "owned node in a pure body"

            testCase "a closed stream's items are not folded"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let topic = Signal (graph, 0)
                let folds = ref 0

                let memo =
                    graph.Run (fun () ->
                        Api.createStreamFold
                            0
                            (fun state item ->
                                folds.Value <- folds.Value + 1
                                state + item)
                            (fun _ ->
                                topic.Value |> ignore
                                feed.Source))

                reading memo |> ignore
                feed.Push 1
                feed.Complete ()
                feed.Push 2
                Expect.equal folds.Value 1 "nothing folded after completion"
                topic.Value <- 1
                reading memo |> ignore
                feed.At(0).Sink.Next 3
                Expect.equal folds.Value 1 "nothing folded by a restarted stream's old sink"

            testCase "a completion from a replaced stream leaves the running stream alone"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let topic = Signal (graph, 0)

                let memo =
                    graph.Run (fun () ->
                        Api.createStream (fun _ ->
                            topic.Value |> ignore
                            feed.Source))

                reading memo |> ignore
                topic.Value <- 1
                reading memo |> ignore
                feed.At(0).Done.SetResult()
                Expect.equal (reading memo) Pending "the new stream is still waiting for its first item"
                Expect.isFalse feed.Latest.Token.IsCancellationRequested "the running stream is not cancelled"
                feed.Push 3
                Expect.equal (reading memo) (Ready 3) "the running stream publishes"

            testCase "items awaiting a wake are not published once a dependency change has made the stream stale"
            <| fun _ ->
                use graph = new Graph ()
                let feed = ManualStream<int>()
                let topic = Signal (graph, 0)

                let memo =
                    graph.Run (fun () ->
                        Api.createStream (fun _ ->
                            topic.Value |> ignore
                            feed.Source))

                reading memo |> ignore

                graph.Run (fun () ->
                    Api.batch (fun () ->
                        feed.Push 3
                        topic.Value <- 1))

                Expect.isTrue (memo.Status.HasFlag Status.Pending) "the stale stream's item is not published"
        ]
