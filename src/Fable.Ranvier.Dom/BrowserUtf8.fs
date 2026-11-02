namespace Ranvier.Dom

open System
open Fable.Core

/// <summary>UTF-8 encoding and decoding through the browser's Encoding API.</summary>
[<RequireQualifiedAccess>]
module Utf8 =
    [<Emit("new TextEncoder()")>]
    let private createEncoder () : obj = jsNative

    [<Emit("$0.encode($1)")>]
    let private encodeBytes (encoder: obj) (input: string) : byte array = jsNative

    [<Emit("$0.encodeInto($1, $2)")>]
    let private encodeIntoBytes (encoder: obj) (input: string) (destination: byte array) : obj = jsNative

    [<Emit("$0.read")>]
    let private readCount (result: obj) : int = jsNative

    [<Emit("$0.written")>]
    let private writtenCount (result: obj) : int = jsNative

    [<Emit("new TextDecoder('utf-8', { fatal: $0, ignoreBOM: $1 })")>]
    let private createTextDecoder (fatal: bool) (ignoreBOM: bool) : obj = jsNative

    [<Emit("$0.decode($1, { stream: !$2 })")>]
    let private decodeChunk (decoder: obj) (chunk: byte array) (finalChunk: bool) : string = jsNative

    let private incompleteSuffix (bytes: byte array) =
        let mutable index = 0
        let mutable suffix = bytes.Length
        let mutable found = false

        while index < bytes.Length && not found do
            let value = int bytes[index]

            let length =
                if value <= 0x7F then 1
                elif value >= 0xC2 && value <= 0xDF then 2
                elif value >= 0xE0 && value <= 0xEF then 3
                elif value >= 0xF0 && value <= 0xF4 then 4
                else 1

            if index + length > bytes.Length then
                suffix <- index
                found <- true
            else
                index <- index + length

        if suffix = bytes.Length then
            Array.empty
        else
            bytes[suffix..]

    let private fatalRemainder (bytes: byte array) =
        let mutable index = 0
        let mutable remainder = bytes.Length
        let mutable found = false

        while index < bytes.Length && not found do
            let value = int bytes[index]

            let length =
                if value <= 0x7F then 1
                elif value >= 0xC2 && value <= 0xDF then 2
                elif value >= 0xE0 && value <= 0xEF then 3
                elif value >= 0xF0 && value <= 0xF4 then 4
                else 0

            if length = 0 then
                remainder <- index + 1
                found <- true
            else
                let mutable offset = 1
                let mutable invalid = -1

                while offset < length
                      && index + offset < bytes.Length
                      && invalid < 0 do
                    let next = int bytes[index + offset]
                    let continuation = next >= 0x80 && next <= 0xBF

                    let firstRange =
                        offset <> 1
                        || ((value <> 0xE0 || next >= 0xA0)
                            && (value <> 0xED || next <= 0x9F)
                            && (value <> 0xF0 || next >= 0x90)
                            && (value <> 0xF4 || next <= 0x8F))

                    if not continuation || not firstRange then
                        invalid <- index + offset

                    offset <- offset + 1

                if invalid >= 0 then
                    remainder <- invalid
                    found <- true
                elif index + length > bytes.Length then
                    // A final flush reports an incomplete prefix and consumes it.
                    remainder <- bytes.Length
                    found <- true
                else
                    index <- index + length

        if remainder = bytes.Length then
            Array.empty
        else
            bytes[remainder..]

    /// <summary>The canonical encoding label.</summary>
    let encoding = "utf-8"

    /// <summary>Encodes a string as UTF-8 using the platform's TextEncoder.</summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="input"/> is null.</exception>
    let encode (input: string) : byte array =
        if isNull input then
            nullArg "input"

        encodeBytes (createEncoder ()) input

    /// <summary>Encodes as many complete UTF-8 scalars as fit in <paramref name="destination"/>.</summary>
    /// <remarks><c>Read</c> counts UTF-16 code units. The destination tail is left unchanged.</remarks>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="input"/> or <paramref name="destination"/> is null.</exception>
    let encodeInto (input: string) (destination: byte array) : EncodeIntoResult =
        if isNull input then
            nullArg "input"

        if isNull destination then
            nullArg "destination"

        let result = encodeIntoBytes (createEncoder ()) input destination

        {
            Read = readCount result
            Written = writtenCount result
        }

    /// <summary>Creates a stateful UTF-8 decoder using the platform's TextDecoder.</summary>
    let createDecoder (options: Utf8DecoderOptions) : IUtf8Decoder =
        let mutable decoder = createTextDecoder options.Fatal true
        let mutable bomPending = true
        let mutable pendingFatalInput = Array.empty<byte>

        let reset () =
            decoder <- createTextDecoder options.Fatal true
            pendingFatalInput <- Array.empty
            bomPending <- true

        let stripBom (text: string) =
            if not bomPending || text.Length = 0 then
                text
            else
                bomPending <- false

                if not options.IgnoreBOM && text[0] = '\uFEFF' then
                    text.Substring 1
                else
                    text

        { new IUtf8Decoder with
            member _.Encoding = encoding
            member _.Fatal = options.Fatal
            member _.IgnoreBOM = options.IgnoreBOM

            member _.Decode(chunk, finalChunk) =
                if isNull chunk then
                    nullArg "chunk"

                if options.Fatal then
                    let input = Array.append pendingFatalInput chunk
                    decoder <- createTextDecoder true true

                    try
                        let value = decodeChunk decoder input finalChunk |> stripBom

                        if finalChunk then
                            reset ()
                        else
                            pendingFatalInput <- incompleteSuffix input

                        Ok value
                    with _ ->
                        pendingFatalInput <- if finalChunk then Array.empty else fatalRemainder input

                        if finalChunk then
                            bomPending <- true

                        decoder <- createTextDecoder true true
                        Error Utf8DecodeError.InvalidUtf8Sequence
                else
                    try
                        let value = decodeChunk decoder chunk finalChunk |> stripBom

                        if finalChunk then
                            reset ()

                        Ok value
                    with _ ->
                        if finalChunk then
                            reset ()

                        Error Utf8DecodeError.InvalidUtf8Sequence
        }
