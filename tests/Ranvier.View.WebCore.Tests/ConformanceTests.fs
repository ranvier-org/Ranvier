/// <summary>
/// The conformance modules of the DOM twin (RangeChecks, TemplateChecks, DelegationChecks, TestingApiChecks,
/// BoundaryChecks, PlacementChecks), each case run on a new page and on a new twin document, with equal artefacts.
/// </summary>
module Ranvier.View.WebCore.Tests.ConformanceTests

open System
open System.Collections.Generic
open Expecto
open Fable.Core.TS.Dom
open Ranvier.View.Testing
open Ranvier.View.Twin
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support

/// <summary>Creates events on the page: a native event of the spec's interface, its init fields kept on the object.</summary>
let private eventSource (host: WebCoreHost) =
    { new EventSpec.IEventSource with
        member _.Create spec =
            let session = host.Session

            let kind =
                match spec.Kind with
                | Plain -> 0
                | Mouse -> 2
                | Pointer -> 3
                | Keyboard -> 4
                | Input -> 5
                | Focus -> 6

            let handle =
                RanvierWebCoreAbi.Native.rv_host_create_event (
                    host.Handle,
                    kind,
                    spec.Type,
                    (if spec.Bubbles then 1 else 0),
                    (if spec.Cancelable then 1 else 0)
                )

            let event: NativeObject =
                match spec.Kind with
                | Plain -> EventObject (session, handle)
                | Mouse -> MouseEventObject (session, handle)
                | Pointer -> PointerEventObject (session, handle)
                | Keyboard -> KeyboardEventObject (session, handle)
                | Input -> InputEventObject (session, handle)
                | Focus -> FocusEventObject (session, handle)

            session.Register event
            event.Init <- Dictionary (dict spec.Init)
            unbox<Event> event
    }

let private webcore (records: List<string * string>) (run: Conformance.Harness -> unit) =
    use host = WebCoreHost.Create (800, 600)
    use _ = EventSpec.install (eventSource host)

    let harness =
        { new Conformance.Harness with
            member _.Platform = "webcore"
            member _.Document = host.Document

            member _.Post callback =
                host.QueueMicrotask callback

            member _.RunMicrotasks() =
                host.RunMicrotasks ()

            member _.AdvanceBy milliseconds =
                host.AdvanceBy milliseconds

            member _.ListenerErrors() =
                let messages = [ for error in host.Errors -> error.Message ]
                host.Errors.Clear ()
                messages

            member _.Intercept(target, memberName, wrap) =
                host.Intercept (target, memberName, wrap)

            member _.Record(name, text) =
                records.Add (name, text)
        }

    run harness

let private twin (records: List<string * string>) (run: Conformance.Harness -> unit) =
    let host = TwinHost.Create ()

    try
        let harness =
            { new Conformance.Harness with
                member _.Platform = "twin"
                member _.Document = host.Document.Native :?> Document

                member _.Post callback =
                    host.Clock.Post callback

                member _.RunMicrotasks() =
                    host.Clock.RunMicrotasks () |> ignore

                member _.AdvanceBy milliseconds =
                    host.Clock.AdvanceBy milliseconds

                member _.ListenerErrors() =
                    let messages =
                        [
                            for error in host.Document.ListenerErrors ->
                                match error with
                                | TwinListenerException (_, inner) -> inner.Message
                                | other -> other.Message
                        ]

                    host.Document.ListenerErrors.Clear ()
                    messages

                member _.Intercept(target, memberName, wrap) =
                    Bridge.intercept host.Document (Bridge.nodeOf target) memberName wrap

                member _.Record(name, text) =
                    records.Add (name, text)
            }

        run harness
    finally
        (host.Document :> IDisposable).Dispose()

let private runCase (run: Conformance.Harness -> unit) () =
    onHostThread (fun () ->
        let expected = List<string * string>()
        twin expected run
        let actual = List<string * string>()
        webcore actual run
        Expect.sequenceEqual actual expected "the artefacts the twin recorded")

[<Tests>]
let tests =
    testList
        "Conformance"
        [
            for moduleName, cases in ConformanceCases.modules do
                testList
                    moduleName
                    [
                        for name, run in cases do
                            if Conformance.skippedOn "webcore" name then
                                ptestCase name ignore
                            else
                                testCase name (runCase run)
                    ]
        ]
