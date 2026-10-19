module Ranvier.AotSmoke.WireSmoke

open Ranvier.Wire.Thoth.Json.Core
open Ranvier.Wire.Thoth.Json.System.Text.Json

type private Shape =
    | Circle of radius: float
    | Dot

type private Point = { X: int; Label: string }

let private shape: Codec<Shape> =
    variantCodec {
        let! circle = Codec.case "circle" Circle Codec.float
        and! dot = Codec.case "dot" (fun () -> Dot) Codec.unit

        return
            function
            | Circle r -> circle r
            | Dot -> dot ()
    }

let private point: Codec<Point> =
    objectCodec {
        let! x = Codec.field "x" _.X Codec.int
        and! label = Codec.field "label" _.Label Codec.string
        return { X = x; Label = label }
    }

let private failure (result: Result<'T, string>) =
    match result with
    | Ok _ -> "decoded"
    | Error message -> message

/// <summary>Each wire check as its name, whether it passed, and the text it observed.</summary>
let checks () : (string * bool * string) list =
    let pointText = Encode.toString 0 (point.Encoder { X = 3; Label = "a" })
    let tag = failure (Decode.fromString shape """{"hexagon":null}""")
    let index = failure (Decode.fromString (Codec.array Codec.int) """[1,"x"]""")
    let overflow = failure (Decode.fromString Codec.float "1e400")
    let overflow32 = failure (Decode.fromString Codec.float32 "1e39")

    [
        "a vendored Thoth record encodes", pointText = """{"x":3,"label":"a"}""", pointText
        "an unknown variant tag is an error", tag.Contains "Expected exactly one object key", tag
        "an array element error names its index", index.Contains "$.[1]", index
        "a vendored float past the double range is an error", overflow.Contains "Value is outside the float range", overflow
        "a vendored float32 past the single range is an error", overflow32.Contains "Value is outside the float32 range", overflow32
    ]
