module JsdomHarness

open Fable.Core.TS.Dom
open Ranvier.View.Testing.Conformance

/// <summary>
/// A harness over a jsdom or browser document: a post queue drained by <c>RunMicrotasks</c>, and the caller's clock
/// ticked by <c>AdvanceBy</c>.
/// </summary>
let create (document: Document) (record: System.Action<string, string>) (tick: System.Action<float>) (errors: System.Func<string array>) : Harness =
    let queue = ResizeArray<unit -> unit>()

    { new Harness with
        member _.Platform = "jsdom"
        member _.Document = document

        member _.Post callback =
            queue.Add callback

        member _.RunMicrotasks() =
            let mutable i = 0

            while i < queue.Count do
                queue[i]()
                i <- i + 1

            queue.Clear ()

        member _.AdvanceBy milliseconds =
            tick.Invoke milliseconds

        member _.Record(name, text) =
            record.Invoke (name, text)

        member _.ListenerErrors() =
            List.ofArray (errors.Invoke ())
    }
