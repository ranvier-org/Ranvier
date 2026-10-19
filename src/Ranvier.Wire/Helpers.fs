module internal Ranvier.Wire.Helpers

open Ranvier.Wire.Thoth.Json.Core

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
