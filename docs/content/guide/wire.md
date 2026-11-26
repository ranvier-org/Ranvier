---
title: JSON wire codecs
---

Use `Ranvier.Wire` to share a JSON contract between .NET and Fable. Define the shape with the vendored
`Ranvier.Wire.Thoth.Json.Core` combinators and wrap it in a `WireCodec<'T>` for the transport boundary.

```fsharp
open Ranvier.Wire
open Ranvier.Wire.Thoth.Json.Core

type Price = { Currency: string; Amount: decimal }

let priceCodec : Codec<Price> =
    objectCodec {
        let! currency = Codec.field "currency" _.Currency Codec.string
        and! amount = Codec.field "amount" _.Amount Codecs.decimal
        return { Currency = currency; Amount = amount }
    }

let wire = WireCodec.ofCodec priceCodec
let text = wire.Encode { Currency = "AUD"; Amount = 1.10M }
let received = wire.Decode text
```

The amount travels as `"1.10"`, preserving decimal scale. Use `Codec.optional` for optional fields and
`variantCodec` with `Codec.case` for a discriminated union. `variantCodecWithTag` and `variantCodecTuple`
select alternative union encodings. `Codec.create` pairs an encoder and decoder for a custom representation.
The `Encode` and `Decode` modules and `decoder` expression expose the individual directions. See the
[wire API reference](/reference/ranvier-wire/index.md).

For nested shapes use `Codec.list`, `array`, `dict`, `keyValuePairs` and the tuple codecs. `lossyOption` and
`losslessOption` choose how to represent optional values. `Codec.lazily` and `Codec.fix` support recursive
shapes. The target-specific backends are under `Ranvier.Wire.Thoth.Json.System.Text.Json` on .NET and
`Ranvier.Wire.Thoth.Json.JavaScript` on Fable. Their `Decode.fromValue` accepts an already parsed value.
`fromString` and `fromStringWithOptions` parse text. In the JavaScript backend, `DecodeOptions.ExactNumbers`
requests numeric source text when the engine supplies it, and `Encode.fromCodec` formats a value with an
explicit indentation. Use the `WireCodec` wrapper at the transport boundary to apply the shared limits and
error cases.

## Represent values across runtimes

| Value | Codec | Wire form |
| --- | --- | --- |
| Timestamp with offset | `Codecs.dateTimeOffset` | A string containing local clock time, milliseconds and UTC offset. Encoding truncates finer precision. |
| Arbitrary-size integer | `Codecs.bigint` | A signed decimal integer string. The decoder also accepts an exact integer literal within its digit limit. |
| Decimal | `Codecs.decimal` | A string retaining scale. A representable numeric literal can also decode. |
| Signed or unsigned long integer | `Codec.int64`, `Codec.uint64` | A decimal integer string that survives a JavaScript round trip. |
| Text | `Codec.string`, `Codec.char` | Unicode scalar text. A lone surrogate in a value or a property name fails encoding. |

Use the string wire forms when exact number text must survive engines that cannot recover a JSON number's
original spelling. Apply application rules such as allowed currencies to the decoded value.

## Handle a rejected input

`WireCodec.ofCodecWith limits codec` uses explicit `WireLimits`. Start from `WireLimits.Default` and change
`MaxDepth`, `MaxItems` or `MaxBigIntDigits` to fit the message contract. The first two bound nesting and total
array elements plus object members before parsing. The last bounds each `Codecs.bigint` value. Invalid limits
raise when the codec is constructed.

`Decode` returns `Result<'T, WireError>`. Use `Syntax` for null text, malformed JSON or lone surrogates,
`Limit` for exceeded bounds and `Shape` for valid JSON the decoder cannot accept. Each case carries a message,
also available as `WireError.Message`. Encoding a value containing a lone surrogate raises. To record the same
contract as a stable, versioned input, adapt the codec to a [journal codec](journal.md#keys-and-codecs).
