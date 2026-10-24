module CounterBench.Tests.TraceReportTests

open System.Text.Json
open Expecto
open CounterBench
open CounterBench.TraceGrowth
open CounterBench.TraceReport

let private flag (value: bool) =
    if value then "true" else "false"

let private dotnetRun (rows: (string * string * float * bool) list) =
    let body =
        rows
        |> List.map (fun (scenario, engine, bytes, gcFree) ->
            $"""{{"Scenario":"%s{scenario}","Engine":"%s{engine}","BytesPerOp":%g{bytes},"GcFree":%s{flag gcFree}}}""")
        |> String.concat ","

    (JsonDocument.Parse ("{\"Rows\":[" + body + "]}")).RootElement

let private nodeRun (rows: (string * string * float * bool) list) =
    let body =
        rows
        |> List.map (fun (scenario, engine, bytes, gcFree) ->
            $"""{{"Scenario":"%s{scenario}","Engine":"%s{engine}","Bytes":{{"Median":%g{bytes},"Min":0,"Max":0}},"GcFree":%s{flag gcFree}}}""")
        |> String.concat ","

    (JsonDocument.Parse (
        "{\"Rows\":[],\"Fable\":{\"Rows\":["
        + body
        + "]}}"
    ))
        .RootElement

let private row scenario untraced traced gcFree : AllocationRow =
    {
        Runtime = "dotnet"
        Scenario = scenario
        Engine = "Ranvier"
        Untraced = untraced
        Traced = traced
        GcFree = gcFree
    }

let private checkpoint updates events retained ms : Checkpoint =
    {
        Workload = "memo"
        Updates = updates
        Events = events
        RetainedBytes = retained
        EventsMs = ms
        SnapshotMs = ms
        DumpTextMs = ms
        TailReadMs = ms
        TailEvents = if events = 0 then 0 else 1400
    }

let private growth traced =
    let checkpoints =
        if traced then
            [| checkpoint 1000 14016 900000. 0.5; checkpoint 10000 140016 9000000. 5. |]
        else
            [| checkpoint 1000 0 7000. 0.; checkpoint 10000 0 7000. 0. |]

    {
        Runtime = "dotnet"
        Traced = traced
        Checkpoints = checkpoints
        Summaries =
            match summarise checkpoints with
            | Ok s -> [| s |]
            | Error e -> failtest e
    }

[<Tests>]
let tests =
    testList
        "TraceReport"
        [
            test "allocation rows pair Ranvier scenarios across the variants" {
                let untraced =
                    dotnetRun
                        [
                            "memo", "Ranvier", 100., true
                            "memo", "R3", 50., true
                            "gone", "Ranvier", 1., true
                        ]

                let traced = dotnetRun [ "memo", "Ranvier", 250., true; "memo", "R3", 50., true ]

                Expect.equal (TraceReport.allocationRows untraced traced) [| row "memo" 100. 250. true |] "one Ranvier row in both variants"
            }

            test "a row is GC-free only when both variants are" {
                let rows =
                    TraceReport.allocationRows
                        (dotnetRun [ "a", "Ranvier", 1., true; "b", "Ranvier", 1., false ])
                        (dotnetRun [ "a", "Ranvier", 2., false; "b", "Ranvier", 2., true ])

                Expect.equal (rows |> Array.map _.GcFree) [| false; false |] "either variant collecting"
            }

            test "allocation rows read the Node.js section by its median" {
                let rows =
                    TraceReport.allocationRows (nodeRun [ "memo", "Ranvier", 40., true ]) (nodeRun [ "memo", "Ranvier", 120., true ])

                Expect.equal
                    (rows
                     |> Array.map (fun r -> r.Runtime, r.Untraced, r.Traced))
                    [| "node", 40., 120. |]
                    "node row"
            }

            test "the report holds versions, ratios, growth and export rows" {
                let text =
                    TraceReport.markdown
                        [| "Source", "abc1234"; "OS", "TestOS" |]
                        [| row "memo" 100. 250. true |]
                        [| growth false; growth true |]
                        [| "TraceCostBenchmarks", "| Method | Mean |" |]

                Expect.stringContains text "| Source | abc1234 |" "versions"
                Expect.stringContains text "| dotnet | memo | Ranvier | 100 | 250 | 150 | 2.50 | yes |" "allocation ratio"
                Expect.stringContains text "| dotnet | memo | 14.0 | 0.0 | 900.0 | 900.0 | 64.3 |" "growth summary"
                Expect.stringContains text "| dotnet | memo | 10000 | 140016 | 5.000 | 5.000 | 5.000 | 5.000 | 1400 |" "export row"
                Expect.stringContains text "| Method | Mean |" "BenchmarkDotNet table"
            }

            test "a zero untraced allocation has no ratio" {
                let text = TraceReport.markdown [||] [| row "signal" 0. 123. true |] [||] [||]

                Expect.stringContains text "| dotnet | signal | Ranvier | 0 | 123 | 123 | n/a | yes |" "no ratio"
            }

            test "a row with a collection inside a region has no ratio" {
                let text = TraceReport.markdown [||] [| row "timed" 10. -200. false |] [||] [||]

                Expect.stringContains text "| dotnet | timed | Ranvier | 10 | -200 | -210 | n/a | no |" "collected"
            }
        ]
