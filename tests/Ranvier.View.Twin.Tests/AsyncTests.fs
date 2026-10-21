module Ranvier.View.Twin.Tests.AsyncTests

open System.Threading.Tasks
open Expecto
open Ranvier
open Ranvier.View.Twin
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template

type private Loaded = { Result: AsyncMemo<string> }

let private loader (source: TaskCompletionSource<string>) =
    Component.define
        (fun _ () ->
            {
                Result = Api.createAsync (fun _ _ -> source.Task)
            })
        (fun t ->
            t.element
                (HtmlTag "p")
                [||]
                [|
                    t.text (fun s ->
                        match s.Result.TryValue with
                        | Ready value -> value
                        | Pending -> "loading"
                        | Failed error -> error.Message)
                |])

let private queued =
    { TwinMount.defaults with
        Commits = Commits.Queued Ranvier.View.Foundation.Scheduling.CollectAndContinue
    }

[<Tests>]
let tests =
    testList
        "Async"
        [
            testCase "a result completed off-thread applies when the session settles"
            <| fun () ->
                let source = TaskCompletionSource<string>()
                use session = Twin.mountWith queued (loader source) ()
                session.Act ignore
                Expect.equal (session.Text ()) "loading" "pending"
                Task.Run(fun () -> source.SetResult "done").Wait()
                session.Act ignore
                Expect.equal (session.Text ()) "done" "settled"

            testCase "a result completed on the session's thread applies when the session settles"
            <| fun () ->
                let source = TaskCompletionSource<string>()
                use session = Twin.mountWith TwinMount.defaults (loader source) ()
                Expect.equal (session.Text ()) "loading" "pending"
                session.Act (fun () -> source.SetResult "here")
                Expect.equal (session.Text ()) "here" "settled"

            testCase "queued commits wait for the clock"
            <| fun () ->
                use session = Twin.mountWith queued Fixtures.counter 1
                Expect.isGreaterThan session.Clock.Pending.Microtasks -1 "queue readable"
                session.Clock.RunMicrotasks () |> ignore
                Expect.equal (session.Text ()) "1" "committed"
        ]
