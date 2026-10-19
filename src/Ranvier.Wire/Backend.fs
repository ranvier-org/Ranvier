module internal Ranvier.Wire.Backend

open Ranvier.Wire.Thoth.Json.Core
#if FABLE_COMPILER
open Fable.Core
#else
open System.IO
open System.Text
open System.Text.Encodings.Web
open System.Text.Json
#endif

/// <summary>Runs the decoder, giving a decoder failure or a raised exception as <c>WireError.Shape</c>.</summary>
let private decodeWith (helpers: IDecoderHelpers<'J>) (decoder: Decoder<'T>) (json: 'J) : Result<'T, WireError> =
    try
        match decoder.Decode (helpers, json) with
        | Ok value -> Ok value
        | Error error -> Error (WireError.Shape (Decode.errorToString helpers (Decode.Helpers.prependPath "$" error)))
    with ex ->
        Error (WireError.Shape ex.Message)

#if FABLE_COMPILER
let private helpers: IDecoderHelpers<obj> =
    Ranvier.Wire.Thoth.Json.JavaScript.Decode.helpers

let private encoderHelpers =
    Helpers.encoding Ranvier.Wire.Thoth.Json.JavaScript.Encode.helpers

/// <summary>The value's wire text, as <c>JSON.stringify</c> writes it.</summary>
let encode (value: IEncodable) : string =
    JS.JSON.stringify (Encode.toJsonValue encoderHelpers value)

/// <summary>The decoded text, once it passes the pre-scan and parses.</summary>
let decode (limits: WireLimits) (decoder: Decoder<'T>) (text: string) : Result<'T, WireError> =
    match Prescan.check limits text with
    | Some error -> Error error
    | None ->
        match
            (try
                Ok (JS.JSON.parse text)
             with ex ->
                 Error ex.Message)
        with
        | Error message -> Error (WireError.Syntax message)
        | Ok json -> decodeWith helpers decoder json
#else
let private helpers: IDecoderHelpers<JsonElement> =
    Ranvier.Wire.Thoth.Json.System.Text.Json.Decode.helpers

let private encoderHelpers =
    Helpers.encoding Ranvier.Wire.Thoth.Json.System.Text.Json.Encode.helpers

/// <summary>The value's wire text: System.Text.Json with relaxed escaping.</summary>
let encode (value: IEncodable) : string =
    let json = Encode.toJsonValue encoderHelpers value
    use stream = new MemoryStream ()

    do
        use writer =
            new Utf8JsonWriter (stream, JsonWriterOptions (Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping))

        match json with
        | null -> writer.WriteNullValue ()
        | node -> node.WriteTo writer

        writer.Flush ()

    Encoding.UTF8.GetString (stream.ToArray ())

/// <summary>The decoded text, once it passes the pre-scan and parses.</summary>
let decode (limits: WireLimits) (decoder: Decoder<'T>) (text: string) : Result<'T, WireError> =
    match Prescan.check limits text with
    | Some error -> Error error
    | None ->
        try
            use document =
                JsonDocument.Parse (text, JsonDocumentOptions (MaxDepth = limits.MaxDepth))

            decodeWith helpers decoder document.RootElement
        with ex ->
            Error (WireError.Syntax ex.Message)
#endif
