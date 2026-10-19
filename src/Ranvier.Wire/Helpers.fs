module internal Ranvier.Wire.Helpers

open System
open Ranvier.Wire.Thoth.Json.Core
#if !FABLE_COMPILER
open System.Text.Json
#endif

/// <summary>
/// The index of the first UTF-16 code unit of the text that is a surrogate without its pair; -1 when every surrogate
/// is paired or the text is null.
/// </summary>
let loneSurrogate (text: string) : int =
    let length = if isNull text then 0 else text.Length
    let mutable found = -1
    let mutable i = 0

    while found < 0 && i < length do
        let code = int text[i]

        if
            code >= 0xD800
            && code <= 0xDBFF
            && i + 1 < length
            && int text[i + 1] >= 0xDC00
            && int text[i + 1] <= 0xDFFF
        then
            i <- i + 2
        elif code >= 0xD800 && code <= 0xDFFF then
            found <- i
        else
            i <- i + 1

    found

let private requireScalars (text: string) =
    let index = loneSurrogate text

    if index >= 0 then
        invalidArg
            "value"
            ("A string holds a lone surrogate at index "
             + string index
             + "; wire text carries Unicode scalar values only.")

/// <summary>The helpers of <c>inner</c>, raising <c>ArgumentException</c> for a string, char or object key holding a lone surrogate.</summary>
let encoding (inner: IEncoderHelpers<'J>) : IEncoderHelpers<'J> =
    { new IEncoderHelpers<'J> with
        member _.encodeString value =
            requireScalars value
            inner.encodeString value

        member _.encodeChar value =
            requireScalars (string value)
            inner.encodeChar value

        member _.encodeDecimalNumber value =
            inner.encodeDecimalNumber value

        member _.encodeBool value =
            inner.encodeBool value

        member _.encodeNull() =
            inner.encodeNull ()

        member _.encodeObject values =
            inner.encodeObject (
                values
                |> Seq.map (fun (key, value) ->
                    requireScalars key
                    key, value)
            )

        member _.encodeArray values =
            inner.encodeArray values

        member _.encodeList values =
            inner.encodeList values

        member _.encodeSeq values =
            inner.encodeSeq values

        member _.encodeResizeArray values =
            inner.encodeResizeArray values

        member _.encodeSignedIntegralNumber value =
            inner.encodeSignedIntegralNumber value

        member _.encodeUnsignedIntegralNumber value =
            inner.encodeUnsignedIntegralNumber value
    }

/// <summary>The caps of a <c>WireCodec</c> decode, for the decoders that enforce them.</summary>
type DecodeScope(maxBigIntDigits: int) =
    member _.MaxBigIntDigits = maxBigIntDigits

/// <summary>Raised by a decoder for a value over a <c>DecodeScope</c> cap; <c>WireCodec</c> gives it as <c>WireError.Limit</c>.</summary>
type LimitExceeded(message: string) =
    inherit Exception(message)

type private ScopedHelpers<'J>(inner: IDecoderHelpers<'J>, numberText: 'J -> string option, maxBigIntDigits: int) =
    inherit DecodeScope(maxBigIntDigits)

    interface IDecoderHelpers<'J> with
        member _.isString value =
            inner.isString value

        member _.isNumber value =
            inner.isNumber value

        member _.isBoolean value =
            inner.isBoolean value

        member _.isNullValue value =
            inner.isNullValue value

        member _.isArray value =
            inner.isArray value

        member _.isObject value =
            inner.isObject value

        member _.hasProperty name value =
            inner.hasProperty name value

        member _.isIntegralValue value =
            inner.isNumber value
            && (match numberText value with
                | Some text ->
                    let number = inner.asFloat value

                    text.IndexOf '.' < 0
                    && not (Double.IsInfinity number)
                    && Math.Floor number = number
                | None -> false)

        member _.asString value =
            inner.asString value

        member _.asFloat value =
            inner.asFloat value

        member _.asFloat32 value =
            inner.asFloat32 value

        member _.asInt value =
            inner.asInt value

        member _.asBoolean value =
            inner.asBoolean value

        member _.asArray value =
            inner.asArray value

        member _.getProperty(name, value) =
            inner.getProperty (name, value)

        member _.getProperties value =
            inner.getProperties value

        member _.anyToString value =
            inner.anyToString value

        member _.numberToString value =
            match numberText value with
            | Some text -> text
            | None ->
                failwith (
                    "The number's source text is unavailable: this JavaScript engine's JSON.parse gives a reviver no source text"
                    + " (Node 21+, Chrome 114+, Firefox 135+ and Safari 18.4+ do)."
                )

/// <summary>
/// The helpers of <c>inner</c> for one decode under <c>maxBigIntDigits</c>. A number's text is <c>numberText</c>'s
/// (<c>None</c> when unknown, which makes <c>numberToString</c> raise), and a number is integral exactly when its text
/// is known and has no <c>.</c> and its value is a finite whole number.
/// </summary>
let decoding (numberText: 'J -> string option) (maxBigIntDigits: int) (inner: IDecoderHelpers<'J>) : IDecoderHelpers<'J> =
    ScopedHelpers (inner, numberText, maxBigIntDigits)

#if !FABLE_COMPILER
/// <summary>A number's text, read verbatim from the document.</summary>
let netNumberText (value: JsonElement) : string option =
    Some (value.GetRawText ())
#endif
