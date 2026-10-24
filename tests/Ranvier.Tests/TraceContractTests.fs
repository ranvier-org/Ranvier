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
#endif
        ]
