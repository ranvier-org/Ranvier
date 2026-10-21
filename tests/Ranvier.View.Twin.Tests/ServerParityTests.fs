module Ranvier.View.Twin.Tests.ServerParityTests

open Expecto
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.Twin

// Browser-only parts render under Omit.
let private omit =
    { Server.Options.defaults with
        Policy = Representation.Omit
    }

let private serverFixtures () : (string * string) list =
    let options = Server.Options.defaults

    [
        "greeting", Server.render options ServerFixtures.greeting ServerFixtures.greetingProps
        "named", Server.render options ServerFixtures.named ServerFixtures.greetingProps
        "null title", Server.render options ServerFixtures.nullTitle ServerFixtures.greetingProps
        "staged waiting", Server.render options ServerFixtures.staged ServerFixtures.Waiting
        "staged working", Server.render options ServerFixtures.staged (ServerFixtures.Working 3)
        "staged complete", Server.render options ServerFixtures.staged (ServerFixtures.Complete "<done>")
        "decades", Server.render options ServerFixtures.decades 42
        "listed", Server.render options ServerFixtures.listed ServerFixtures.listedItems
        "carded", Server.render options ServerFixtures.carded ServerFixtures.greetingProps
        "pair", Server.render options ServerFixtures.pair ServerFixtures.greetingProps
        "static template", Server.render options ServerFixtures.staticTemplate ()
        "empty text", Server.render options ServerFixtures.emptyText ""
        "clicking", Server.render omit ServerFixtures.clicking ServerFixtures.greetingProps
        // The head fixture renders a <head> element into a fragment, which the HTML parser drops there.
        for name, explicitHtml, sugarHtml in SugarFixtures.elementServerPairs () do
            if not (name.Contains "head") then
                "sugar explicit: " + name, explicitHtml
                "sugar: " + name, sugarHtml
    ]

[<Tests>]
let tests =
    testList
        "Server parity"
        [
            for name, html in serverFixtures () do
                testCase $"{name}: Html.server of the parsed render is the render"
                <| fun () ->
                    use document = new TwinDocument ()
                    Expect.equal (Html.server (Html.parse document html)) html "identity"

            testCase "strict hydration of the greeting keeps the server's nodes"
            <| fun () ->
                let page =
                    Server.renderPage omit ServerFixtures.clicking ServerFixtures.greetingProps

                use session =
                    Twin.hydrateHtml
                        TwinMount.defaults
                        { Hydration.HydrationOptions.defaults with
                            Strict = true
                        }
                        page.Body
                        ServerFixtures.clicking
                        ServerFixtures.greetingProps

                Expect.isEmpty session.Reports "no reports"
                Expect.stringContains (session.Html ()) "<button" "button kept"

            testCase "hydration adopts the parsed button rather than building one"
            <| fun () ->
                let page =
                    Server.renderPage omit ServerFixtures.clicking ServerFixtures.greetingProps

                let host = TwinHost.Create ()
                use _ = host.Document
                let element = host.Document.CreateElement "div"
                host.Document.Body.AppendChild element |> ignore
                Html.parseInto element page.Body

                let parsed =
                    (element.Native :?> Fable.Core.TS.Dom.Element).querySelector "button"
                    |> Option.get

                use graph = new Ranvier.Graph (TwinMount.defaults.GraphOptions)

                let mounted =
                    Hydration.hydrateWith
                        Scheduling.SynchronousCommits
                        graph
                        None
                        ignore
                        Hydration.HydrationOptions.defaults
                        element.Native
                        ServerFixtures.clicking
                        ServerFixtures.greetingProps

                let hydrated =
                    (element.Native :?> Fable.Core.TS.Dom.Element).querySelector "button"
                    |> Option.get

                mounted.Dispose ()
                Expect.isTrue (obj.ReferenceEquals (parsed, hydrated)) "the same node"

            testCase "hydration over queued commits is not supported"
            <| fun () ->
                Expect.throwsT<System.NotSupportedException>
                    (fun () ->
                        Twin.hydrateHtml
                            { TwinMount.defaults with
                                Commits = Commits.Queued Ranvier.View.Dom.Scheduling.CollectAndContinue
                            }
                            Hydration.HydrationOptions.defaults
                            ""
                            ServerFixtures.clicking
                            ServerFixtures.greetingProps
                        |> ignore)
                    "queued"

            testCase "Twin.hydrate renders the page and hydrates it"
            <| fun () ->
                use session =
                    Twin.hydrate omit Hydration.HydrationOptions.defaults ServerFixtures.clicking ServerFixtures.greetingProps

                Expect.isEmpty session.Reports "no reports"
                Expect.stringContains (session.Html ()) "<button" "rendered and kept"
        ]
