module Ranvier.Tests.TraceContractTests

open Expecto
open Ranvier

#if RANVIER_TRACE
type private Point = { X: int; Y: int }

type private Unregistered = { Name: string }

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

let private throws<'E when 'E :> exn> (f: unit -> unit) (message: string) =
    let raised =
        try
            f ()
            false
        with :? 'E ->
            true

    Expect.isTrue raised message

let private refOf (g: Graph) (node: INode) =
    match TraceContract.nodeRef (Trace.snapshot g) node.Id with
    | Some r -> r
    | None -> failwith "the node must have a ref"

/// <summary>
/// A labelled memo whose body creates a labelled memo, run twice by a write; the refs of both inner memos and the
/// outer memo.
/// </summary>
let private labelledProgram () =
    use g = new Graph ()
    use _ = g.Activate ()
    let count = Trace.named "count" (fun () -> createSignal 1)
    let inners = ResizeArray<INode>()

    let outer =
        Trace.named "outer" (fun () ->
            createMemoWith (fun _ ->
                inners.Add (Trace.named "inner" (fun () -> createMemo (fun _ -> 0)))
                count.Value))

    outer.Value |> ignore
    count.Value <- 2
    outer.Value |> ignore

    [
        yield refOf g count
        yield refOf g outer
        for inner in inners do
            yield refOf g inner
    ]
#endif

[<Tests>]
let tests =
    testList
        "TraceContract"
        [
#if RANVIER_TRACE
            test "a scalar payload encodes as Exact" {
                let codecs = TraceCodecs.ofList []
                Expect.equal (TraceCodecs.encode codecs (box 3)) (TracePayload.Exact (TraceScalar.Int 3)) "an int is exact"

                Expect.equal
                    (TraceCodecs.encode codecs (box "a\nb"))
                    (TracePayload.Exact (TraceScalar.String "a\nb"))
                    "a string keeps its line breaks"

                Expect.equal (TraceCodecs.encode codecs (box true)) (TracePayload.Exact (TraceScalar.Bool true)) "a bool is exact"
                Expect.equal (TraceCodecs.encode codecs (box 2.5)) (TracePayload.Exact (TraceScalar.Float 2.5)) "a float is exact"
                Expect.equal (TraceCodecs.encode codecs null) TracePayload.Null "null is Null"
            }

            test "a registered codec encodes as Encoded and decodes back" {
                let codecs = TraceCodecs.ofList [ pointCodec ]
                let payload = TraceCodecs.encode codecs (box { X = 1; Y = 2 })
                Expect.equal payload (TracePayload.Encoded ("point", 2, "1,2")) "the codec's name, version and data"
                Expect.equal (TraceCodecs.decode codecs payload) (box { X = 1; Y = 2 }) "decoding gives the value back"
            }

            test "the first codec that claims a value encodes it" {
                let other =
                    { pointCodec with
                        Name = "other"
                        Encode = fun _ -> Some "x"
                    }

                let codecs = TraceCodecs.ofList [ pointCodec; other ]
                Expect.equal (TraceCodecs.encode codecs (box { X = 1; Y = 2 })) (TracePayload.Encoded ("point", 2, "1,2")) "the earlier codec wins"

                Expect.equal
                    (TraceCodecs.encode codecs (box { Name = "n" }))
                    (TracePayload.Encoded ("other", 2, "x"))
                    "a later codec takes what the earlier declines"
            }

            test "a value with no codec encodes as Lossy of its valueText" {
                let value = box { Name = "n" }
                let payload = TraceCodecs.encode (TraceCodecs.ofList [ pointCodec ]) value
                Expect.equal payload (TracePayload.Lossy (TraceModel.valueText value)) "the diagnostic text, marked lossy"
            }

            test "strict codecs raise for a value with no codec" {
                let codecs = TraceCodecs.strict (TraceCodecs.ofList [ pointCodec ])

                throws<TraceCodecException>
                    (fun () ->
                        TraceCodecs.encode codecs (box { Name = "n" })
                        |> ignore)
                    "strict must raise"

                Expect.equal (TraceCodecs.encode codecs (box 3)) (TracePayload.Exact (TraceScalar.Int 3)) "a scalar stays exact"
            }

            test "decoding raises for an unknown codec, an unknown version and a lossy payload" {
                let codecs = TraceCodecs.ofList [ pointCodec ]

                throws<TraceCodecException>
                    (fun () ->
                        TraceCodecs.decode codecs (TracePayload.Encoded ("other", 2, "1,2"))
                        |> ignore)
                    "an unknown codec must raise"

                throws<TraceCodecException>
                    (fun () ->
                        TraceCodecs.decode codecs (TracePayload.Encoded ("point", 1, "1,2"))
                        |> ignore)
                    "an unknown version must raise"

                throws<TraceCodecException>
                    (fun () ->
                        TraceCodecs.decode codecs (TracePayload.Lossy "text")
                        |> ignore)
                    "a lossy payload must raise"
            }

            test "decoding Exact and Null gives the scalar and null" {
                let codecs = TraceCodecs.ofList []
                Expect.equal (TraceCodecs.decode codecs (TracePayload.Exact (TraceScalar.Int 3))) (box 3) "an int"
                Expect.equal (TraceCodecs.decode codecs (TracePayload.Exact (TraceScalar.String "s"))) (box "s") "a string"
                Expect.isNull (TraceCodecs.decode codecs TracePayload.Null) "null"
            }

            test "a chain of Trace.named labels gives a Named ref" {
                match labelledProgram () with
                | count :: outer :: inner :: _ ->
                    Expect.equal (count.Path, count.Stability, count.Label) ("/count", TracePathStability.Named, Some "count") "a labelled signal"
                    Expect.equal (outer.Path, outer.Stability) ("/outer", TracePathStability.Named) "a labelled memo"
                    Expect.equal (inner.Path, inner.Stability) ("/outer/inner@1", TracePathStability.Named) "a labelled memo under a labelled host"
                    Expect.equal inner.Graph 1 "the graph is the root owner id"
                | refs -> failtestf "expected three refs, got %A" refs
            }

            test "an @k incarnation of a named path stays Named" {
                let second = List.last (labelledProgram ())
                Expect.equal (second.Path, second.Stability) ("/outer/inner@2", TracePathStability.Named) "the rerun's inner memo"
            }

            test "an unlabelled node, or a labelled node under one, gives a Located ref" {
                use g = new Graph ()
                use _ = g.Activate ()
                let site = createSignal 1
                let mutable inner = Unchecked.defaultof<Memo<int>>

                let host =
                    createMemoWith (fun _ ->
                        inner <- Trace.named "inner" (fun () -> createMemo (fun _ -> 0))
                        site.Value)

                host.Value |> ignore
                Expect.equal (refOf g site).Stability TracePathStability.Located "a site segment"
                Expect.equal (refOf g host).Stability TracePathStability.Located "an unlabelled memo"
                Expect.equal (refOf g inner).Stability TracePathStability.Located "a label under a site segment"
                Expect.equal (refOf g inner).Label (Some "inner") "the node's own label"
            }

            test "a labelled node under createRoot gives a Named ref" {
                use g = new Graph ()
                use _ = g.Activate ()
                let mutable inner = Unchecked.defaultof<Memo<int>>

                createRoot (fun _ -> inner <- Trace.named "inner" (fun () -> createMemo (fun _ -> 1)))
                |> ignore

                Expect.equal
                    ((refOf g inner).Path, (refOf g inner).Stability)
                    ("/root#0/inner", TracePathStability.Named)
                    "a root scope counts by order"
            }

            test "two runs of the same program give equal refs" {
                let first = labelledProgram ()
                let second = labelledProgram ()
                Expect.equal second first "the refs must not depend on the run"
            }

            test "a node absent from the snapshot has no ref" { Expect.isNone (TraceContract.nodeRef TraceModel.emptySnapshot 7) "no node, no ref" }
#endif
        ]
