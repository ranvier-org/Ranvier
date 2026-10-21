module Ranvier.Analyzers.Tests.Ran013

open Expecto
open Ranvier.Analyzers
open Ranvier.Analyzers.Tests.Options

let private findings (body: string) =
    let source =
        "module Fixture\nopen System.Threading\nopen System.Threading.Tasks\nopen Ranvier\n"
        + body

    run Analyzers.ran013Cli (context (ranvier.Force ()) source)

let private codes body =
    findings body |> List.map _.Code

[<Tests>]
let tests =
    testList
        "RAN013"
        [
            test "a write in Task.Run, with its exact message" {
                Expect.equal
                    (findings "let f (temperature: Signal<int>) = Task.Run (fun () -> temperature.Value <- 25)\n"
                     |> List.map _.Message)
                    [
                        "`temperature.Value <- 25` runs on a thread-pool thread; a write off the graph's owning thread raises under `ThreadAffinity.Guarded`. Hand the write to `graph.Dispatch (fun () -> ...)`."
                    ]
                    "one message"
            }

            test "Pump in ThreadPool.QueueUserWorkItem" {
                Expect.equal (codes "let f (graph: Graph) = ThreadPool.QueueUserWorkItem (fun _ -> graph.Pump () |> ignore)\n") [ "RAN013" ] "Pump"
            }

            test "Signal.update in Parallel.For" {
                Expect.equal
                    (codes "let f (s: Signal<int>) = Parallel.For (0, 4, fun _ -> Signal.update s ((+) 1)) |> ignore\n")
                    [ "RAN013" ]
                    "update"
            }

            test "a write in a new Thread" { Expect.equal (codes "let f (s: Signal<int>) = Thread (fun () -> s.Value <- 1)\n") [ "RAN013" ] "Thread" }

            test "a write inside graph.Dispatch inside Task.Run" {
                Expect.isEmpty
                    (codes "let f (graph: Graph) (s: Signal<int>) = Task.Run (fun () -> graph.Dispatch (fun () -> s.Value <- 1))\n")
                    "dispatched"
            }

            test "Settle inside Task.Run" {
                Expect.isEmpty (codes "let f (source: AsyncSource<int>) = Task.Run (fun () -> source.Settle 1)\n") "settle waits in the inbox"
            }

            test "a write to a graph the thread creates" {
                Expect.isEmpty
                    (codes
                        "let f () = Thread (fun () -> let g = new Graph () in let s = Signal (g, 0) in s.Value <- 1; g.Pump () |> ignore)
")
                    "the thread owns the graph"
            }

            test "a write in an ordinary lambda" {
                Expect.isEmpty (codes "let f (s: Signal<int>) = List.iter (fun x -> s.Value <- x) [ 1 ]\n") "same thread"
            }
        ]
