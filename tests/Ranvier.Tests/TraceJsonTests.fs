module Ranvier.Tests.TraceJsonTests

open Expecto
open Ranvier

#if RANVIER_TRACE
type private Point = { X: int; Y: int }

let private pointCodec: TraceCodec =
    {
        Name = "point"
        Version = 2
        Encode =
            fun value ->
                match value with
                | :? Point as p -> Some (string p.X + "," + string p.Y)
                | _ -> None
        Decode =
            fun data ->
                let parts = data.Split ','
                box { X = int parts[0]; Y = int parts[1] }
    }

let private doc (kind: string) (seq: int) (body: string) =
    "{\"schema\":3,\"kind\":\""
    + kind
    + "\",\"seq\":"
    + string seq
    + ",\"body\":"
    + body
    + "}"

let private named: TraceNodeRef =
    {
        Node = 2
        Path = "/count"
        Stability = TracePathStability.Named
        Label = Some "count"
    }

let private namedText =
    "{\"node\":2,\"path\":\"/count\",\"stability\":{\"case\":\"Named\"},\"label\":\"count\"}"

let private located: TraceNodeRef =
    {
        Node = 3
        Path = "/App.fs:4 \"q\"\n"
        Stability = TracePathStability.Located
        Label = None
    }

let private locatedText =
    "{\"node\":3,\"path\":\"/App.fs:4 \\\"q\\\"\\n\",\"stability\":{\"case\":\"Located\"},\"label\":null}"

/// <summary>Every payload case, with its JSON text.</summary>
let private payloads =
    [
        TracePayload.Null, "{\"case\":\"Null\"}"
        TracePayload.Exact (TraceScalar.Int -4), "{\"case\":\"Exact\",\"value\":{\"case\":\"Int\",\"value\":-4}}"
        TracePayload.Exact (TraceScalar.String "a\tb"), "{\"case\":\"Exact\",\"value\":{\"case\":\"String\",\"value\":\"a\\tb\"}}"
        TracePayload.Exact (TraceScalar.Bool false), "{\"case\":\"Exact\",\"value\":{\"case\":\"Bool\",\"value\":false}}"
        TracePayload.Exact (TraceScalar.Float 2.5), "{\"case\":\"Exact\",\"value\":{\"case\":\"Float\",\"value\":2.5}}"
        TracePayload.Exact (TraceScalar.Float nan), "{\"case\":\"Exact\",\"value\":{\"case\":\"Float\",\"value\":\"NaN\"}}"
        TracePayload.Exact (TraceScalar.Float infinity), "{\"case\":\"Exact\",\"value\":{\"case\":\"Float\",\"value\":\"Infinity\"}}"
        TracePayload.Exact (TraceScalar.Float -infinity), "{\"case\":\"Exact\",\"value\":{\"case\":\"Float\",\"value\":\"-Infinity\"}}"
        TracePayload.Encoded ("point", 2, "1,2"), "{\"case\":\"Encoded\",\"codec\":\"point\",\"version\":2,\"data\":\"1,2\"}"
        TracePayload.Lossy "{ X = 1 }", "{\"case\":\"Lossy\",\"text\":\"{ X = 1 }\"}"
    ]

/// <summary>Every <c>WhyRoot</c> case, with its JSON text.</summary>
let private roots =
    [
        UserWrite 3, "{\"case\":\"UserWrite\",\"write\":3}"
        Created 4, "{\"case\":\"Created\",\"nodeNew\":4}"
        Pulled 5, "{\"case\":\"Pulled\",\"reader\":5}"
        BeforeCheckpoint "cut", "{\"case\":\"BeforeCheckpoint\",\"file\":\"cut\"}"
        BeforeCheckpoint null, "{\"case\":\"BeforeCheckpoint\",\"file\":null}"
        Unrecorded 6, "{\"case\":\"Unrecorded\",\"last\":6}"
        Settled 7, "{\"case\":\"Settled\",\"settle\":7}"
    ]

/// <summary>A hand-built state holding every node status, both stabilities and every edge map.</summary>
let private handState: TraceGraphState =
    let node (r: TraceNodeRef) kind status value : TraceNodeState =
        {
            Ref = r
            Kind = kind
            Owner = 1
            Created = r.Node + 1
            Runs = 2
            LastRun = 8
            PendingMark = 0
            Status = status
            Value = value
        }

    {
        Schema = 3
        Seq = 12
        Root = 1
        Checkpoint = Some "cut"
        SeqFrom = 5
        Nodes =
            Map.ofList
                [
                    2, node named TraceNodeKind.Signal TraceNodeStatus.Fresh (Some (TracePayload.Exact (TraceScalar.Int 1)))
                    3, node located TraceNodeKind.Memo (TraceNodeStatus.Ended RunStatus.Pending) None
                    4, node { named with Node = 4; Path = "/e" } TraceNodeKind.Effect TraceNodeStatus.Running None
                    5, node { named with Node = 5; Path = "/d" } TraceNodeKind.AsyncMemo TraceNodeStatus.Disposed (Some (TracePayload.Lossy "x"))
                ]
        Owners =
            Map.ofList
                [
                    1,
                    {
                        Id = 1
                        Parent = 0
                        Host = 0
                        Root = false
                        Path = ""
                        Label = None
                        Disposed = false
                    }
                    6,
                    {
                        Id = 6
                        Parent = 1
                        Host = 3
                        Root = true
                        Path = "/r"
                        Label = Some "r"
                        Disposed = true
                    }
                ]
        Sources = Map.ofList [ 3, [ 2 ]; 4, [ 3; 2 ] ]
        Observers = Map.ofList [ 2, Set.ofList [ 4; 3 ]; 3, Set.ofList [ 4 ] ]
    }

let private handStateText =
    let n (r: string) (id: int) (kind: string) (status: string) (value: string) =
        "{\"ref\":"
        + r
        + ",\"kind\":\""
        + kind
        + "\",\"owner\":1,\"created\":"
        + string (id + 1)
        + ",\"runs\":2,\"lastRun\":8,\"pendingMark\":0,\"status\":"
        + status
        + ",\"value\":"
        + value
        + "}"

    let refText (id: int) (path: string) =
        "{\"node\":"
        + string id
        + ",\"path\":\""
        + path
        + "\",\"stability\":{\"case\":\"Named\"},\"label\":\"count\"}"

    doc
        "state"
        12
        ("{\"root\":1,\"checkpoint\":\"cut\",\"seqFrom\":5,\"nodes\":["
         + n namedText 2 "Signal" "{\"case\":\"Fresh\"}" "{\"case\":\"Exact\",\"value\":{\"case\":\"Int\",\"value\":1}}"
         + ","
         + n locatedText 3 "Memo" "{\"case\":\"Ended\",\"status\":\"Pending\"}" "null"
         + ","
         + n (refText 4 "/e") 4 "Effect" "{\"case\":\"Running\"}" "null"
         + ","
         + n (refText 5 "/d") 5 "AsyncMemo" "{\"case\":\"Disposed\"}" "{\"case\":\"Lossy\",\"text\":\"x\"}"
         + "],\"owners\":["
         + "{\"id\":1,\"parent\":0,\"host\":0,\"root\":false,\"path\":\"\",\"label\":null,\"disposed\":false},"
         + "{\"id\":6,\"parent\":1,\"host\":3,\"root\":true,\"path\":\"/r\",\"label\":\"r\",\"disposed\":true}"
         + "],\"sources\":[[3,[2]],[4,[3,2]]],\"observers\":[[2,[3,4]],[3,[4]]]}")

/// <summary>A traced program whose state, why chain and runs carry codec-encoded and exact values.</summary>
let private program () =
    use g = new Graph ()
    use _ = g.Activate ()
    let codecs = TraceCodecs.ofList [ pointCodec ]
    let x = Trace.named "x" (fun () -> createSignal 1)
    let m = Trace.named "m" (fun () -> createMemo (fun _ -> { X = x.Value; Y = 0 }))
    createEffect (fun () -> m.Value |> ignore)
    x.Value <- 2
    let state = Trace.state g codecs

    [
        TraceJson.state state
        TraceJson.why state.Seq (Trace.whyWith g codecs m)
        TraceJson.runs state.Seq (Trace.runs g codecs m)
        TraceJson.whyNot state.Seq (Trace.whyNot g m)
        TraceJson.waiting state.Seq (Trace.waitingOn g m)
        TraceJson.lookup state.Seq (Trace.lookupNode g m)
    ]
#endif

[<Tests>]
let tests =
    testList
        "TraceJson"
        [
#if RANVIER_TRACE
            test "a state document writes every field, node status and stability in id order" {
                Expect.equal (TraceJson.state handState) handStateText "the state text"
            }

            test "a runs document writes every payload, root and run status case" {
                let statuses =
                    [
                        Some RunStatus.Ok, "\"Ok\""
                        Some RunStatus.Pending, "\"Pending\""
                        Some RunStatus.Error, "\"Error\""
                        Some RunStatus.Abandoned, "\"Abandoned\""
                        None, "null"
                    ]

                let cases =
                    [
                        for i, (p, pText) in List.indexed payloads ->
                            let r, rText = roots[i % roots.Length]
                            let s, sText = statuses[i % statuses.Length]
                            let value, valueText = if i = 0 then None, "null" else Some p, pText

                            {
                                TraceRunState.Run = i + 1
                                TraceRunState.Start = 10 * i
                                TraceRunState.Status = s
                                TraceRunState.Moved = i % 2 = 0
                                TraceRunState.Root = r
                                TraceRunState.Flush = i
                                TraceRunState.Value = value
                            },
                            "{\"run\":"
                            + string (i + 1)
                            + ",\"start\":"
                            + string (10 * i)
                            + ",\"status\":"
                            + sText
                            + ",\"moved\":"
                            + (if i % 2 = 0 then "true" else "false")
                            + ",\"root\":"
                            + rText
                            + ",\"flush\":"
                            + string i
                            + ",\"value\":"
                            + valueText
                            + "}"
                    ]

                Expect.isTrue (cases.Length >= roots.Length) "every root is written"

                Expect.equal
                    (TraceJson.runs 9 (List.map fst cases))
                    (doc "runs" 9 ("[" + String.concat "," (List.map snd cases) + "]"))
                    "the runs text"

                Expect.equal (TraceJson.runs 1 []) (doc "runs" 1 "[]") "no runs"
            }

            test "a why document writes its steps and every root case" {
                for r, rText in
                    (None, "null")
                    :: List.map (fun (r, t) -> Some r, t) roots do
                    let why =
                        {
                            TraceWhy.Node = 2
                            TraceWhy.Run = 3
                            TraceWhy.Steps =
                                [
                                    {
                                        TraceWhyStep.Seq = 8
                                        Kind = TraceEventKind.Write
                                        Node = 2
                                        Other = 0
                                        Value = Some (TracePayload.Exact (TraceScalar.Int 5))
                                    }
                                    {
                                        TraceWhyStep.Seq = 7
                                        Kind = TraceEventKind.RunStart
                                        Node = 2
                                        Other = 1
                                        Value = None
                                    }
                                ]
                            TraceWhy.Root = r
                        }

                    Expect.equal
                        (TraceJson.why 9 why)
                        (doc
                            "why"
                            9
                            ("{\"node\":2,\"run\":3,\"steps\":["
                             + "{\"seq\":8,\"kind\":\"Write\",\"node\":2,\"other\":0,\"value\":{\"case\":\"Exact\",\"value\":{\"case\":\"Int\",\"value\":5}}},"
                             + "{\"seq\":7,\"kind\":\"RunStart\",\"node\":2,\"other\":1,\"value\":null}"
                             + "],\"root\":"
                             + rText
                             + "}"))
                        ("the why text with root " + rText)
            }

            test "the why projection encodes step values through the codecs" {
                let why: Why =
                    {
                        Node = 2
                        Run = 1
                        Steps =
                            [
                                {
                                    Seq = 4
                                    Kind = TraceEventKind.Moved
                                    Node = 2
                                    Other = 0
                                    Value = Some (box { X = 1; Y = 2 })
                                }
                            ]
                        Root = Some (Created 3)
                    }

                let projected = TraceContract.why (TraceCodecs.ofList [ pointCodec ]) why
                Expect.equal (projected.Steps |> List.map _.Value) [ Some (TracePayload.Encoded ("point", 2, "1,2")) ] "the step value"
                Expect.equal (projected.Node, projected.Run, projected.Root) (2, 1, Some (Created 3)) "the chain fields"
            }

            test "a whyNot document writes every reason case and an absent reason" {
                let cases =
                    [
                        None, "null"
                        Some (WhyNotReason.Disposed 4), "{\"case\":\"Disposed\",\"seq\":4}"
                        Some (Queued 5), "{\"case\":\"Queued\",\"schedule\":5}"
                        Some (Unobserved 6), "{\"case\":\"Unobserved\",\"mark\":6}"
                        Some (CheckedClean (7, [ 2; 1 ])), "{\"case\":\"CheckedClean\",\"resolvedAt\":7,\"upstream\":[2,1]}"
                        Some (CheckedClean (7, [])), "{\"case\":\"CheckedClean\",\"resolvedAt\":7,\"upstream\":[]}"
                        Some (SkippedAsRunningReader 8), "{\"case\":\"SkippedAsRunningReader\",\"seq\":8}"
                        Some (NotReached 9), "{\"case\":\"NotReached\",\"stopAt\":9}"
                    ]

                for reason, text in cases do
                    Expect.equal (TraceJson.whyNot 10 reason) (doc "whyNot" 10 text) text
            }

            test "a waiting document writes every flight state case" {
                let waiting: TraceWaiting =
                    {
                        Node = 4
                        Sources = [ 2; 3 ]
                        Flights =
                            [
                                {
                                    Flight = 4
                                    Start = 20
                                    Run = 4
                                    State = TraceFlightState.InFlight
                                }
                                {
                                    Flight = 3
                                    Start = 15
                                    Run = 3
                                    State = TraceFlightState.Settled (16, true)
                                }
                                {
                                    Flight = 2
                                    Start = 10
                                    Run = 0
                                    State = TraceFlightState.Failed (11, false)
                                }
                                {
                                    Flight = 1
                                    Start = 5
                                    Run = 1
                                    State = TraceFlightState.Dropped (6, TraceDropReason.Superseded)
                                }
                            ]
                    }

                Expect.equal
                    (TraceJson.waiting 21 waiting)
                    (doc
                        "waiting"
                        21
                        ("{\"node\":4,\"sources\":[2,3],\"flights\":["
                         + "{\"flight\":4,\"start\":20,\"run\":4,\"state\":{\"case\":\"InFlight\"}},"
                         + "{\"flight\":3,\"start\":15,\"run\":3,\"state\":{\"case\":\"Settled\",\"seq\":16,\"held\":true}},"
                         + "{\"flight\":2,\"start\":10,\"run\":0,\"state\":{\"case\":\"Failed\",\"seq\":11,\"cancelled\":false}},"
                         + "{\"flight\":1,\"start\":5,\"run\":1,\"state\":{\"case\":\"Dropped\",\"seq\":6,\"reason\":\"Superseded\"}}"
                         + "]}"))
                    "the waiting text"
            }

            test "a lookup document writes every lookup case" {
                let cases =
                    [
                        TraceLookup.Live named, "{\"case\":\"Live\",\"node\":" + namedText + "}"
                        TraceLookup.Disposed (located, 7),
                        "{\"case\":\"Disposed\",\"node\":"
                        + locatedText
                        + ",\"seq\":7}"
                        TraceLookup.Checkpointed named,
                        "{\"case\":\"Checkpointed\",\"node\":"
                        + namedText
                        + "}"
                        TraceLookup.Missing, "{\"case\":\"Missing\"}"
                        TraceLookup.Foreign, "{\"case\":\"Foreign\"}"
                    ]

                for lookup, text in cases do
                    Expect.equal (TraceJson.lookup 3 lookup) (doc "lookup" 3 text) text
            }

            test "two runs of the same program write the same documents" {
                let first = program ()
                Expect.equal (program ()) first "the documents"
                Expect.stringContains first[0] "{\"case\":\"Encoded\",\"codec\":\"point\",\"version\":2,\"data\":\"2,0\"}" "the memo's value"
                Expect.stringContains first[1] "\"kind\":\"why\"" "the why document"
            }
#endif
        ]
