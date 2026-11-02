namespace Ranvier.Dom

open System
open System.Text

/// <summary>UTF-8 encoding and decoding for the native .NET target.</summary>
[<RequireQualifiedAccess>]
module Utf8 =
    /// <summary>The canonical encoding label.</summary>
    let encoding = "utf-8"

    /// <summary>Encodes a string as UTF-8, replacing each unpaired surrogate with U+FFFD.</summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="input"/> is null.</exception>
    let encode (input: string) : byte array =
        if isNull input then
            nullArg "input"

        UTF8Encoding(false, false).GetBytes input

    let private scalarAt (input: string) index =
        let first = int input[index]

        if first >= 0xD800 && first <= 0xDBFF then
            if index + 1 < input.Length then
                let second = int input[index + 1]

                if second >= 0xDC00 && second <= 0xDFFF then
                    0x10000 + ((first - 0xD800) <<< 10) + second
                    - 0xDC00,
                    2
                else
                    0xFFFD, 1
            else
                0xFFFD, 1
        elif first >= 0xDC00 && first <= 0xDFFF then
            0xFFFD, 1
        else
            first, 1

    let private byteCount scalar =
        if scalar <= 0x7F then 1
        elif scalar <= 0x7FF then 2
        elif scalar <= 0xFFFF then 3
        else 4

    let private writeScalar (destination: byte array) offset scalar =
        match byteCount scalar with
        | 1 -> destination[offset] <- byte scalar
        | 2 ->
            destination[offset] <- byte (0xC0 ||| (scalar >>> 6))
            destination[offset + 1] <- byte (0x80 ||| (scalar &&& 0x3F))
        | 3 ->
            destination[offset] <- byte (0xE0 ||| (scalar >>> 12))
            destination[offset + 1] <- byte (0x80 ||| ((scalar >>> 6) &&& 0x3F))
            destination[offset + 2] <- byte (0x80 ||| (scalar &&& 0x3F))
        | _ ->
            destination[offset] <- byte (0xF0 ||| (scalar >>> 18))
            destination[offset + 1] <- byte (0x80 ||| ((scalar >>> 12) &&& 0x3F))
            destination[offset + 2] <- byte (0x80 ||| ((scalar >>> 6) &&& 0x3F))
            destination[offset + 3] <- byte (0x80 ||| (scalar &&& 0x3F))

    /// <summary>Encodes as many complete UTF-8 scalars as fit in <paramref name="destination"/>.</summary>
    /// <remarks><c>Read</c> counts UTF-16 code units. The destination tail is left unchanged.</remarks>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="input"/> or <paramref name="destination"/> is null.</exception>
    let encodeInto (input: string) (destination: byte array) : EncodeIntoResult =
        if isNull input then
            nullArg "input"

        if isNull destination then
            nullArg "destination"

        let mutable read = 0
        let mutable written = 0
        let mutable stopped = false

        while read < input.Length && not stopped do
            let scalar, units = scalarAt input read
            let count = byteCount scalar

            if written + count <= destination.Length then
                writeScalar destination written scalar
                read <- read + units
                written <- written + count
            else
                stopped <- true

        { Read = read; Written = written }

    /// <summary>Creates a stateful UTF-8 decoder.</summary>
    let createDecoder (options: Utf8DecoderOptions) : IUtf8Decoder =
        let decoder = UTF8Encoding(false, options.Fatal).GetDecoder()
        let pendingInput = ResizeArray<byte>()
        let oneByte = Array.zeroCreate<byte> 1
        let mutable bomPending = true
        let mutable pendingContinuations = 0

        let reset () =
            decoder.Reset ()
            pendingInput.Clear ()
            pendingContinuations <- 0
            bomPending <- true

        let updatePendingState value =
            if pendingContinuations = 0 then
                if value >= 0xC2uy && value <= 0xDFuy then
                    pendingContinuations <- 1
                elif value >= 0xE0uy && value <= 0xEFuy then
                    pendingContinuations <- 2
                elif value >= 0xF0uy && value <= 0xF4uy then
                    pendingContinuations <- 3
            elif value >= 0x80uy && value <= 0xBFuy then
                pendingContinuations <- pendingContinuations - 1

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
            member _.Encoding = "utf-8"
            member _.Fatal = options.Fatal
            member _.IgnoreBOM = options.IgnoreBOM

            member _.Decode(chunk, finalChunk) =
                if isNull chunk then
                    nullArg "chunk"

                if options.Fatal then
                    pendingInput.AddRange chunk
                    let chars = Array.zeroCreate<char> 2
                    let output = StringBuilder ()
                    let mutable failed = false
                    let mutable consumed = 0

                    while consumed < pendingInput.Count && not failed do
                        let value = pendingInput[consumed]
                        oneByte[0] <- value
                        let mutable bytesUsed = 0
                        let mutable charsUsed = 0
                        let mutable completed = false
                        let hadPendingSequence = pendingContinuations > 0

                        try
                            decoder.Convert (oneByte, 0, 1, chars, 0, chars.Length, false, &bytesUsed, &charsUsed, &completed)

                            if bytesUsed <> 1 then
                                invalidOp "The UTF-8 decoder did not consume its input byte."

                            consumed <- consumed + 1
                            updatePendingState value
                            output.Append (chars, 0, charsUsed) |> ignore
                        with :? DecoderFallbackException ->
                            // UTF-8 restores an invalid continuation to the input queue, but
                            // consumes an invalid leading byte. The decoder state is reset.
                            decoder.Reset ()
                            pendingContinuations <- 0

                            if not hadPendingSequence then
                                consumed <- consumed + 1

                            failed <- true

                    if consumed > 0 then
                        pendingInput.RemoveRange (0, consumed)

                    if not failed && finalChunk then
                        try
                            let mutable bytesUsed = 0
                            let mutable charsUsed = 0
                            let mutable completed = false

                            decoder.Convert (Array.empty, 0, 0, chars, 0, chars.Length, true, &bytesUsed, &charsUsed, &completed)
                            output.Append (chars, 0, charsUsed) |> ignore
                        with :? DecoderFallbackException ->
                            failed <- true

                    if failed then
                        if finalChunk then
                            reset ()

                        Error Utf8DecodeError.InvalidUtf8Sequence
                    else
                        let value = output.ToString () |> stripBom

                        if finalChunk then
                            reset ()

                        Ok value
                else
                    try
                        let chars = Array.zeroCreate<char>(max 4 (chunk.Length + 2))
                        let output = StringBuilder ()
                        let mutable offset = 0
                        let mutable completed = false

                        while not completed do
                            let mutable bytesUsed = 0
                            let mutable charsUsed = 0

                            decoder.Convert (
                                chunk,
                                offset,
                                chunk.Length - offset,
                                chars,
                                0,
                                chars.Length,
                                finalChunk,
                                &bytesUsed,
                                &charsUsed,
                                &completed
                            )

                            offset <- offset + bytesUsed
                            output.Append (chars, 0, charsUsed) |> ignore

                            if not completed && bytesUsed = 0 && charsUsed = 0 then
                                invalidOp "The UTF-8 decoder made no progress."

                        let value = output.ToString () |> stripBom

                        if finalChunk then
                            reset ()

                        Ok value
                    with :? DecoderFallbackException ->
                        decoder.Reset ()

                        if finalChunk then
                            bomPending <- true

                        Error Utf8DecodeError.InvalidUtf8Sequence
        }
