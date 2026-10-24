module Ranvier.Docs.Maps.Tests.PageTests

open System
open Expecto
open Ranvier.View
open Ranvier.Docs.Maps
open Ranvier.Docs.Maps.Authoring

let private omit =
    { Server.Options.defaults with
        Policy = Omit
    }

let private fixtureCell id : PageCell =
    {
        Id = id
        Kind = PageCellKind.Expression
        Code = "Ranvier.Docs.Maps.PipelineFixture.counter"
        Line = 4
    }

let private unit' cells =
    MapPage.unit "p0123456789" (Some "/docs/content/page.md") 3 cells

[<Tests>]
let tests =
    testList
        "Pages"
        [
            test "the pipeline fixture renders deterministically on .NET" {
                let first = Server.render omit PipelineFixture.counter ()
                let second = Server.render omit PipelineFixture.counter ()

                Expect.equal
                    first
                    """<!--[r--><div class="rv-map-fixture"><button type="button" class="rv-map-fixture__count">0</button></div><!--]-->"""
                    "server HTML"

                Expect.equal second first "a second render"
            }

            test "each rendered cell gets a distinct host and id prefix" {
                let page = unit' [ fixtureCell "c1"; fixtureCell "c2" ]

                let hosts =
                    page.Cells
                    |> List.map (fun cell -> MapPage.hostId page.Key cell.Id)

                Expect.equal hosts [ "rvm-p0123456789-c1"; "rvm-p0123456789-c2" ] "host ids"
                Expect.stringContains page.Code "HostId = \"rvm-p0123456789-c1\"" "the first definition's host"
                Expect.stringContains page.Code "IdPrefix = \"rvm-p0123456789-c2\"" "the second definition's prefix"
                Expect.stringContains page.Code "let page_c1: Page.Definition<unit>" "a definition per cell"
                Expect.stringContains page.Code "State = None" "no state codec"
            }

            test "a cell's code maps back to its body line and column" {
                let page = unit' [ fixtureCell "c1" ]
                let lines = page.Code.Split '\n'

                let line =
                    lines
                    |> Array.findIndex (fun l -> l.Contains "PipelineFixture.counter")

                Expect.equal (MapPage.locate page.Spans (line + 1) 9) (Some (4, 5)) "the fence line, moved left by the indent"

                let definition =
                    lines
                    |> Array.findIndex (fun l -> l.StartsWith "let page_c1")

                Expect.equal (MapPage.locate page.Spans (definition + 1) 9) (Some (3, 1)) "generated lines blame the opening line"
            }

            test "a map cell compiles its scenario and renders its SignalMap call" {
                let code =
                    "let count = createSignal 1\ncontrols [ button \"Add\" (fun () -> count.Value <- 2) ]"

                match MapFence.compile "c1" [ "timeline" ] code, MapFlags.parse [ "timeline" ] with
                | Ok output, Ok flags ->
                    let page =
                        unit'
                            [
                                {
                                    Id = "c1"
                                    Kind = PageCellKind.Map (output, flags)
                                    Code = code
                                    Line = 10
                                }
                            ]

                    Expect.stringContains page.Code "module Map_c1 =" "the scenario module"
                    Expect.stringContains page.Code ("let Cell_c1: Component<unit> = " + output.Render) "the render call"
                | _ -> failtest "the fence compiles"
            }

            test "declarations render no host and a named component renders its value" {
                let page =
                    unit'
                        [
                            {
                                Id = "c1"
                                Kind = PageCellKind.Declarations
                                Code = "let start = 1"
                                Line = 2
                            }
                            {
                                Id = "c2"
                                Kind = PageCellKind.Render "Ranvier.Docs.Maps.PipelineFixture.counter"
                                Code = "let other = 2"
                                Line = 6
                            }
                        ]

                Expect.isFalse (page.Code.Contains "page_c1") "declarations have no page"
                Expect.stringContains page.Code "let Cell_c2: Component<unit> = Ranvier.Docs.Maps.PipelineFixture.counter" "the named component"
                Expect.equal (MapPage.rendered page |> List.map _.Id) [ "c2" ] "rendered cells"
            }

            test "the scanner's placeholders become map hosts and cards" {
                let body =
                    String.concat
                        "\n"
                        [
                            "<div class=\"partas-solid-card partas-solid-card--map\">"
                            ""
                            "<div class=\"partas-solid\" data-partas-page=\"p0123456789\" data-partas-cell=\"c1\"></div>"
                            ""
                            "</div>"
                            "<div class=\"partas-solid partas-solid--inline\" data-partas-page=\"p0123456789\" data-partas-cell=\"c2\"></div>"
                        ]

                let rewritten = MapPage.hosts body
                Expect.isFalse (rewritten.Contains "partas-solid") "the Solid loader claims nothing"
                Expect.stringContains rewritten "<div class=\"rv-map-card\">" "the card"

                Expect.stringContains
                    rewritten
                    "<div class=\"rv-map-host\" id=\"rvm-p0123456789-c1\" data-ranvier-map-page=\"p0123456789\" data-ranvier-map-cell=\"c1\"></div>"
                    "the host"

                Expect.stringContains rewritten "class=\"rv-map-host rv-map-host--inline\" id=\"rvm-p0123456789-c2\"" "an inline host"
            }

            test "the exporter lists every page definition and each page's entry imports its cells" {
                let first = unit' [ fixtureCell "c1"; fixtureCell "c2" ]
                let program = MapPage.exporter [ first ]

                Expect.stringContains
                    program
                    "\"p0123456789\", \"c2\", \"Ranvier.Docs.MapPages.P0123456789.page_c2\", (fun () -> Ranvier.Docs.MapPages.P0123456789.page_c2)"
                    "the second definition"

                let entry = MapPage.pageEntry "../entries" first

                Expect.equal
                    entry
                    "import \"../entries/P0123456789_c1.js\";\nimport \"../entries/P0123456789_c2.js\";\n"
                    "one import per rendered cell"
            }

            test "fragment hosts are filled after minification and minified attributes still match" {
                let html =
                    "<main><div class=rv-map-host id=rvm-p0123456789-c1 data-ranvier-map-page=p0123456789 data-ranvier-map-cell=c1></div>"
                    + "<div class=\"rv-map-host\" id=\"rvm-p0123456789-c2\"></div></main></body>"

                let fragments =
                    Map.ofList [ "rvm-p0123456789-c1", "<!--[r-->one<!--]-->"; "rvm-p0123456789-c2", "two" ]

                let filled =
                    MapPage.fill (fun host -> Map.tryFind host fragments) (fun key -> Some $"/Ranvier/_ranvier/maps/{key}.js") html

                Expect.stringContains filled "data-ranvier-map-cell=c1><!--[r-->one<!--]--></div>" "the first host"
                Expect.stringContains filled "id=\"rvm-p0123456789-c2\">two</div>" "the second host"

                Expect.stringContains
                    filled
                    "<script type=\"module\" src=\"/Ranvier/_ranvier/maps/p0123456789.js\"></script></body>"
                    "one page script"

                Expect.equal (MapPage.fill (fun _ -> None) (fun _ -> None) "<p>plain</p>") "<p>plain</p>" "a page without hosts"
            }
        ]
