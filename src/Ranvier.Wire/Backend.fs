module internal Ranvier.Wire.Backend

open Ranvier.Wire.Thoth.Json.Core
#if FABLE_COMPILER
open Fable.Core
open Fable.Core.JsInterop
#else
open System.IO
open System.Text
open System.Text.Encodings.Web
open System.Text.Json
#endif

/// <summary>
/// Runs the decoder: a decoder failure or a raised exception is <c>WireError.Shape</c>, and a raised
/// <c>LimitExceeded</c> is <c>WireError.Limit</c>.
/// </summary>
let private decodeWith (helpers: IDecoderHelpers<'J>) (decoder: Decoder<'T>) (json: 'J) : Result<'T, WireError> =
    try
        match decoder.Decode (helpers, json) with
        | Ok value -> Ok value
        | Error error -> Error (WireError.Shape (Decode.errorToString helpers (Decode.Helpers.prependPath "$" error)))
    with
    | :? Helpers.LimitExceeded as ex -> Error (WireError.Limit ex.Message)
    | ex -> Error (WireError.Shape ex.Message)

#if FABLE_COMPILER
/// <summary>Whether this engine's <c>JSON.parse</c> gives a reviver the source text of each number.</summary>
let sourceText: bool =
    emitJsExpr () "JSON.parse('1', function (key, value, context) { return context !== undefined && context.source === '1'; })"

let private exactNumberText (value: obj) : string option =
    Some (Ranvier.Wire.Thoth.Json.JavaScript.Decode.helpers.numberToString value)

let private safeIntegerText (value: obj) : string option =
    if emitJsExpr value "Number.isSafeInteger(Number($0))" then
        Some (emitJsExpr value "String(Number($0))")
    else
        None

let private encoderHelpers =
    Helpers.encoding Ranvier.Wire.Thoth.Json.JavaScript.Encode.helpers

/// <summary>The value's wire text, as <c>JSON.stringify</c> writes it.</summary>
let encode (value: IEncodable) : string =
    JS.JSON.stringify (Encode.toJsonValue encoderHelpers value)

/// <summary>
/// The decoded text, once it passes the pre-scan and parses. With <c>sourceText</c>, numbers keep their source text
/// through Thoth's number-literal reviver; without it, a number's text is known only for a safe integer.
/// </summary>
let decodeWithSourceText (sourceText: bool) (limits: WireLimits) (decoder: Decoder<'T>) (text: string) : Result<'T, WireError> =
    match Prescan.check limits text with
    | Some error -> Error error
    | None ->
        let parsed =
            try
                if sourceText then
                    Ok (emitJsExpr (text, Ranvier.Wire.Thoth.Json.JavaScript.Decode.numberLiteralReviver) "JSON.parse($0, $1)")
                else
                    Ok (emitJsExpr text "JSON.parse($0)")
            with ex ->
                Error ex.Message

        match parsed with
        | Error message -> Error (WireError.Syntax message)
        | Ok json ->
            let numberText = if sourceText then exactNumberText else safeIntegerText

            let helpers =
                Helpers.decoding numberText limits.MaxBigIntDigits Ranvier.Wire.Thoth.Json.JavaScript.Decode.helpers

            decodeWith helpers decoder json

/// <summary>The decoded text, once it passes the pre-scan and parses.</summary>
let decode (limits: WireLimits) (decoder: Decoder<'T>) (text: string) : Result<'T, WireError> =
    decodeWithSourceText sourceText limits decoder text
#else
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

            let helpers =
                Helpers.decoding Helpers.netNumberText limits.MaxBigIntDigits Ranvier.Wire.Thoth.Json.System.Text.Json.Decode.helpers

            decodeWith helpers decoder document.RootElement
        with ex ->
            Error (WireError.Syntax ex.Message)
#endif
