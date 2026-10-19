module Ranvier.Wire.Tests.NumericDecoding

open System.Numerics
open Expecto
open Ranvier.Wire.Thoth.Json.Core
open Ranvier.Wire
open Ranvier.Wire.Tests.WireSupport

let private decodes (codec: Codec<'T>) (text: string) (expected: 'T) =
    testCase ("decodes " + text)
    <| fun () ->
        Expect.equal
            ((WireCodec.ofCodec codec).Decode text
             |> expectOk text)
            expected
            text

let private rejects (codec: Codec<'T>) (text: string) =
    testCase ("rejects " + text)
    <| fun () ->
        Expect.isTrue
            ((WireCodec.ofCodec codec).Decode text
             |> expectError text
             |> isShape)
            text

let private sevens (count: int) =
    String.replicate count "7"

#if FABLE_COMPILER
/// <summary>The text decoded as on an engine whose <c>JSON.parse</c> gives a reviver no source text.</summary>
let private withoutSourceText (codec: Codec<'T>) (text: string) =
    Ranvier.Wire.Backend.decodeWithSourceText false WireLimits.Default codec.Decoder text
#endif

[<Tests>]
let tests =
    testList
        "numeric decoding"
        [
            testList
                "int"
                [
                    rejects Codec.int "1.0"
                    rejects Codec.int "1e-1"
                    decodes Codec.int "1e3" 1000
                    rejects Codec.int "2147483648"
                    decodes Codec.int "-0" 0
                    rejects Codec.int "1e400"
                    decodes Codec.int "\"42\"" 42
                ]
            testList
                "int64"
                [
                    decodes Codec.int64 "9223372036854775807" 9223372036854775807L
                    decodes Codec.int64 "-9223372036854775808" -9223372036854775808L
                    rejects Codec.int64 "9223372036854775808"
                    rejects Codec.int64 "-9223372036854775809"
                    decodes Codec.int64 "9007199254740993" 9007199254740993L
                    rejects Codec.int64 "1.5"
                    decodes Codec.int64 "\"9007199254740993\"" 9007199254740993L
                    // A number in exponent form: .NET's Int64.TryParse allows the exponent, Fable's takes no styles.
#if FABLE_COMPILER
                    rejects Codec.int64 "1e3"
#else
                    decodes Codec.int64 "1e3" 1000L
#endif
                ]
            testList
                "uint64"
                [
                    decodes Codec.uint64 "18446744073709551615" 18446744073709551615UL
                    rejects Codec.uint64 "18446744073709551616"
                    rejects Codec.uint64 "-1"
                ]
            testList
                "float"
                [
                    rejects Codec.float "1e400"
                    rejects Codec.float "-1e400"
                    rejects Codec.float "\"1e400\""
                    decodes Codec.float "1.7976931348623157e308" 1.7976931348623157e308
                    decodes Codec.float "\"Infinity\"" infinity
                ]
            testList "float32" [ rejects Codec.float32 "1e400" ]
            testList
                "Codecs.bigint"
                [
                    decodes Codecs.bigint "9007199254740993" (BigInteger.Parse "9007199254740993")
                    decodes Codecs.bigint "-123456789012345678901234567890" (BigInteger.Parse "-123456789012345678901234567890")
                    rejects Codecs.bigint "1.5"
                    rejects Codecs.bigint "1e3"
                    decodes Codecs.bigint "\"-123456789012345678901234567890\"" (BigInteger.Parse "-123456789012345678901234567890")
                    decodes Codecs.bigint "\"5\"" (BigInteger.Parse "5")
                    rejects Codecs.bigint "\" 5\""
                    rejects Codecs.bigint "\"+5\""
                    rejects Codecs.bigint "\"\""
                    rejects Codecs.bigint "true"
                    testCase "a bigint encodes as a string of digits"
                    <| fun () ->
                        let value = BigInteger.Parse "-123456789012345678901234567890"
                        Expect.equal ((WireCodec.ofCodec Codecs.bigint).Encode value) "\"-123456789012345678901234567890\"" "wire text"
                ]
            testList
                "the bigint digit limit"
                [
                    testCase "a bigint at the default limit of 4,300 digits decodes, as a number or a string"
                    <| fun () ->
                        let wire = WireCodec.ofCodec Codecs.bigint
                        Expect.equal ((wire.Decode (sevens 4300) |> expectOk "number").ToString().Length) 4300 "number"

                        Expect.equal
                            ((wire.Decode ("-" + sevens 4300)
                              |> expectOk "negative")
                                .ToString()
                                .Length)
                            4301
                            "negative"

                        Expect.equal
                            ((wire.Decode (quote + sevens 4300 + quote)
                              |> expectOk "string")
                                .ToString()
                                .Length)
                            4300
                            "string"
                    testCase "a bigint over the limit is a limit error, as a number or a string"
                    <| fun () ->
                        let wire = WireCodec.ofCodec Codecs.bigint

                        Expect.isTrue
                            (wire.Decode (sevens 4301)
                             |> expectError "number"
                             |> isLimit)
                            "number"

                        Expect.isTrue
                            (wire.Decode (quote + sevens 4301 + quote)
                             |> expectError "string"
                             |> isLimit)
                            "string"
                    testCase "a bigint over the limit inside a list is a limit error"
                    <| fun () ->
                        let wire = WireCodec.ofCodec (Codec.list (Codec.tuple2 Codec.int Codecs.bigint))
                        let text = "[[1,\"5\"],[2," + sevens 4301 + "]]"

                        Expect.isTrue
                            (wire.Decode text
                             |> expectError "nested"
                             |> isLimit)
                            "nested"
                    testCase "a custom limit counts every digit, leading zeros included, and no sign"
                    <| fun () ->
                        let wire =
                            WireCodec.ofCodecWith
                                { WireLimits.Default with
                                    MaxBigIntDigits = 3
                                }
                                Codecs.bigint

                        Expect.equal (wire.Decode "999" |> expectOk "999") (BigInteger.Parse "999") "999"
                        Expect.equal (wire.Decode "-999" |> expectOk "-999") (BigInteger.Parse "-999") "-999"
                        let error = wire.Decode "1000" |> expectError "1000"
                        Expect.isTrue (isLimit error) "1000"
                        Expect.stringContains error.Message "4 digits" "message"

                        Expect.isTrue
                            (wire.Decode "\"0001\""
                             |> expectError "0001"
                             |> isLimit)
                            "0001"
                    testCase "MaxBigIntDigits outside 1 to 100,000 is rejected"
                    <| fun () ->
                        let limited (digits: int) =
                            WireCodec.ofCodecWith
                                { WireLimits.Default with
                                    MaxBigIntDigits = digits
                                }
                                Codecs.bigint
                            |> ignore

                        Expect.throws (fun () -> limited 0) "0"
                        Expect.throws (fun () -> limited 100001) "100001"
                        limited 1
                        limited 100000
                ]
            testList
                "Codecs.decimal"
                [
                    decodes Codecs.decimal "79228162514264337593543950335" 79228162514264337593543950335M
                    rejects Codecs.decimal "1e30"
                    rejects Codecs.decimal "1e5"
                    decodes Codecs.decimal "\"1.10\"" 1.10M
                    decodes Codecs.decimal "\"-0.5\"" -0.5M
                    decodes Codecs.decimal "\"0.0000000000000000000000000001\"" 0.0000000000000000000000000001M
                    rejects Codecs.decimal "\"0.00000000000000000000000000001\""
                    rejects Codecs.decimal "\"79228162514264337593543950336\""
                    rejects Codecs.decimal "\"1e5\""
                    rejects Codecs.decimal "\"1,000.5\""
                    rejects Codecs.decimal "\".5\""
                    rejects Codecs.decimal "\"5.\""
                    rejects Codecs.decimal "\"-\""
                    testCase "a decimal keeps its scale"
                    <| fun () -> Expect.equal ((WireCodec.ofCodec Codecs.decimal).Encode 1.10M) "\"1.10\"" "wire text"
                ]
#if !FABLE_COMPILER
            // .NET only: .NET formats numbers by the current culture; Fable has no cultures.
            testCase "bigint and decimal text is the same under any culture"
            <| fun () ->
                let previous = System.Globalization.CultureInfo.CurrentCulture

                try
                    System.Globalization.CultureInfo.CurrentCulture <- System.Globalization.CultureInfo "sv-SE"
                    Expect.equal ((WireCodec.ofCodec Codecs.bigint).Encode(BigInteger.Parse "-5")) "\"-5\"" "bigint"
                    Expect.equal ((WireCodec.ofCodec Codecs.decimal).Encode -1.50M) "\"-1.50\"" "decimal"
                finally
                    System.Globalization.CultureInfo.CurrentCulture <- previous
#endif
#if FABLE_COMPILER
            testCase "this engine's JSON.parse gives a reviver the source text of numbers"
            <| fun () -> Expect.isTrue Ranvier.Wire.Backend.sourceText "source text"
            testCase "without source text, a number decodes when its value is a safe integer or a float"
            <| fun () ->
                Expect.equal (withoutSourceText Codec.int "1" |> expectOk "int") 1 "int"

                Expect.equal
                    (withoutSourceText Codec.int "1.0"
                     |> expectOk "int 1.0")
                    1
                    "int 1.0"

                Expect.equal
                    (withoutSourceText Codec.int64 "9007199254740991"
                     |> expectOk "int64")
                    9007199254740991L
                    "int64"

                Expect.equal
                    (withoutSourceText Codecs.bigint "-9007199254740991"
                     |> expectOk "bigint")
                    (BigInteger.Parse "-9007199254740991")
                    "bigint"

                Expect.equal
                    (withoutSourceText Codecs.decimal "15"
                     |> expectOk "decimal")
                    15M
                    "decimal"

                Expect.equal
                    (withoutSourceText Codec.float "0.1"
                     |> expectOk "float")
                    0.1
                    "float"
            testCase "without source text, a number whose text matters is a shape error"
            <| fun () ->
                let expectUnavailable (name: string) (result: Result<'T, WireError>) =
                    let error = result |> expectError name
                    Expect.isTrue (isShape error) name
                    Expect.stringContains error.Message "source text" name

                expectUnavailable "int64 past 2^53" (withoutSourceText Codec.int64 "9007199254740993")
                expectUnavailable "bigint past 2^53" (withoutSourceText Codecs.bigint "9007199254740993")
                expectUnavailable "decimal with a fraction" (withoutSourceText Codecs.decimal "1.5")
                expectUnavailable "decimal 0.1" (withoutSourceText Codecs.decimal "0.1")
            testCase "without source text, numbers written as strings decode exactly"
            <| fun () ->
                Expect.equal
                    (withoutSourceText Codec.int64 "\"9007199254740993\""
                     |> expectOk "int64")
                    9007199254740993L
                    "int64"

                Expect.equal
                    (withoutSourceText Codecs.bigint "\"9007199254740993\""
                     |> expectOk "bigint")
                    (BigInteger.Parse "9007199254740993")
                    "bigint"

                Expect.equal
                    (withoutSourceText Codecs.decimal "\"1.50\""
                     |> expectOk "decimal")
                    1.50M
                    "decimal"
#endif
        ]
