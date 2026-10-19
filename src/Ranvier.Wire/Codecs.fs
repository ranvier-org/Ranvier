namespace Ranvier.Wire

open System
open System.Globalization
open System.Numerics
open Ranvier.Wire.Thoth.Json.Core

/// <summary>
/// Codecs with the same wire text and the same decoding on .NET and Fable, for the types whose Thoth.Json codecs
/// differ between the two.
/// </summary>
[<RequireQualifiedAccess>]
module Codecs =

    let private allDigits (text: string) =
        let mutable result = text.Length > 0
        let mutable i = 0

        while result && i < text.Length do
            result <- text[i] >= '0' && text[i] <= '9'
            i <- i + 1

        result

    let private unsigned (text: string) =
        if text.Length > 0 && text[0] = '-' then
            text.Substring 1
        else
            text

    /// <summary>Whether the text is <c>-?digits(.digits)?</c>.</summary>
    let private isDecimalText (text: string) =
        let body = unsigned text
        let point = body.IndexOf '.'

        if point < 0 then
            allDigits body
        else
            allDigits (body.Substring (0, point))
            && allDigits (body.Substring (point + 1))

    /// <summary>Whether a decimal text has at most 28 fraction digits and a mantissa no greater than Decimal.MaxValue's.</summary>
    let private fitsDecimal (text: string) =
        let body = unsigned text
        let point = body.IndexOf '.'
        let fractionDigits = if point < 0 then 0 else body.Length - point - 1
        let mantissa = body.Replace(".", "").TrimStart '0'

        fractionDigits <= 28
        && (mantissa.Length < 29
            || (mantissa.Length = 29
                && String.CompareOrdinal (mantissa, "79228162514264337593543950335")
                   <= 0))

    /// <summary>The text of a JSON string, or the source text of a JSON number.</summary>
    let private numericText (helpers: IDecoderHelpers<'J>) (value: 'J) =
        if helpers.isString value then
            Some (helpers.asString value)
        elif helpers.isNumber value then
            Some (helpers.numberToString value)
        else
            None

    let private bigintDecoder: Decoder<BigInteger> =
        { new Decoder<BigInteger> with
            member _.Decode(helpers, value) =
                match numericText helpers value with
                | Some text when allDigits (unsigned text) ->
                    let digits = (unsigned text).Length

                    let cap, raises =
                        match box helpers with
                        | :? Helpers.DecodeScope as scope -> scope.MaxBigIntDigits, not scope.LimitIsError
                        | _ -> WireLimits.Default.MaxBigIntDigits, false

                    if digits <= cap then
#if FABLE_COMPILER
                        Ok (BigInteger.Parse text)
#else
                        Ok (BigInteger.Parse (text, CultureInfo.InvariantCulture))
#endif
                    else
                        let message =
                            "The bigint has "
                            + string digits
                            + " digits, more than "
                            + string cap
                            + "."

                        if raises then
                            raise (Helpers.LimitExceeded message)
                        else
                            Error ("", BadPrimitiveExtra ("a bigint", value, message))
                | _ -> Error ("", BadPrimitive ("a bigint written as digits", value))
        }

    let private decimalDecoder: Decoder<decimal> =
        { new Decoder<decimal> with
            member _.Decode(helpers, value) =
                match numericText helpers value with
                | Some text when isDecimalText text ->
                    if fitsDecimal text then
#if FABLE_COMPILER
                        Ok (Decimal.Parse text)
#else
                        Ok (
                            Decimal.Parse (
                                text,
                                NumberStyles.AllowLeadingSign
                                ||| NumberStyles.AllowDecimalPoint,
                                CultureInfo.InvariantCulture
                            )
                        )
#endif
                    else
                        Error ("", BadPrimitiveExtra ("a decimal", value, "Value is outside the decimal range or has more than 28 fraction digits"))
                | _ -> Error ("", BadPrimitive ("a decimal written as digits with an optional fraction", value))
        }

    /// <summary>
    /// A BigInteger as a string of digits. Decoding reads a string or a number written as <c>-?digits</c>, exactly,
    /// with at most <c>WireLimits.MaxBigIntDigits</c> digits; a <c>WireCodec</c> gives more as <c>WireError.Limit</c>
    /// naming the value's path. Outside a <c>WireCodec</c> the cap is <c>WireLimits.Default</c>'s, and more is a
    /// decoder error.
    /// </summary>
    let bigint: Codec<BigInteger> =
        Codec.create (fun (value: BigInteger) -> Encode.string (string value)) bigintDecoder

    /// <summary>
    /// A decimal as a string, keeping its scale. Decoding reads a string or a number written as
    /// <c>-?digits(.digits)?</c> that a decimal holds exactly.
    /// </summary>
    let decimal: Codec<decimal> =
        Codec.create (fun (value: decimal) -> Encode.string (value.ToString (CultureInfo.InvariantCulture))) decimalDecoder
