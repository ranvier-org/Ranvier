module Utf8Tests

open System
open Ranvier.Dom

#if FABLE_COMPILER
open Fable.Core

[<Emit("String.fromCharCode($0)")>]
let private codeUnit (value: int) : string = jsNative
#else
let private codeUnit (value: int) =
    string (char value)
#endif

let mutable private checks = 0

let private check label condition =
    checks <- checks + 1

    if not condition then
        failwith label

let private equal label expected actual =
    check
        (label
         + " (expected "
         + string expected
         + ", got "
         + string actual
         + ")")
        (expected = actual)

let private nullArgument label action =
    let mutable rejected = false

    try
        action ()
    with :? ArgumentNullException ->
        rejected <- true

    check label rejected

let run () =
    checks <- 0

    equal "canonical encoding label" "utf-8" Utf8.encoding
    equal "empty input" [||] (Utf8.encode "")
    equal "ASCII and BMP scalars" [| 0x41uy; 0xC3uy; 0xA9uy; 0xE6uy; 0xB0uy; 0xB4uy |] (Utf8.encode "Aé水")
    equal "supplementary scalar" [| 0xF0uy; 0x9Fuy; 0x8Cuy; 0x8Duy |] (Utf8.encode "🌍")
    equal "unpaired high surrogate replacement" [| 0xEFuy; 0xBFuy; 0xBDuy |] (Utf8.encode (codeUnit 0xD800))
    equal "unpaired low surrogate replacement" [| 0xEFuy; 0xBFuy; 0xBDuy |] (Utf8.encode (codeUnit 0xDC00))
    nullArgument "null encode input rejected" (fun () -> Utf8.encode null |> ignore)

    let tooSmall = Utf8.encodeInto "🌍x" (Array.zeroCreate 3)
    equal "encodeInto does not split a four-byte scalar" { Read = 0; Written = 0 } tooSmall

    let emptyCapacity = Array.empty<byte>
    equal "encodeInto with zero capacity consumes nothing" { Read = 0; Written = 0 } (Utf8.encodeInto "A" emptyCapacity)
    let oneByteCapacity = Array.create 1 0xAAuy
    equal "encodeInto stops before a two-byte scalar" { Read = 0; Written = 0 } (Utf8.encodeInto "é" oneByteCapacity)
    equal "encodeInto leaves a too-small destination untouched" [| 0xAAuy |] oneByteCapacity

    let exact = Utf8.encodeInto "🌍x" (Array.zeroCreate 4)
    equal "encodeInto counts a surrogate pair as two UTF-16 units" { Read = 2; Written = 4 } exact

    let mixed = Array.create 8 0xAAuy
    let mixedResult = Utf8.encodeInto "A🌍x" mixed
    equal "encodeInto reads UTF-16 units and writes bytes" { Read = 4; Written = 6 } mixedResult
    equal "encodeInto leaves unused destination bytes unchanged" [| 0x41uy; 0xF0uy; 0x9Fuy; 0x8Cuy; 0x8Duy; 0x78uy; 0xAAuy; 0xAAuy |] mixed

    let tail = Array.create 4 0xAAuy
    let tailResult = Utf8.encodeInto "é🌍" tail
    equal "encodeInto stops before a scalar that does not fit" { Read = 1; Written = 2 } tailResult
    equal "encodeInto does not modify its tail" [| 0xC3uy; 0xA9uy; 0xAAuy; 0xAAuy |] tail

    for split in 1..3 do
        let decoder = Utf8.createDecoder { Fatal = false; IgnoreBOM = false }
        let scalar = [| 0xF0uy; 0x9Fuy; 0x8Cuy; 0x8Duy |]
        equal ("four-byte scalar prefix split " + string split) (Ok "") (decoder.Decode (scalar[0 .. split - 1], false))
        equal ("four-byte scalar suffix split " + string split) (Ok "🌍") (decoder.Decode (scalar[split..], true))

    let truncatedReplacement = Utf8.createDecoder { Fatal = false; IgnoreBOM = false }
    equal "incomplete input stays buffered" (Ok "") (truncatedReplacement.Decode ([| 0xE2uy; 0x82uy |], false))
    equal "final flush replaces an incomplete sequence" (Ok "�") (truncatedReplacement.Decode ([||], true))
    equal "final flush starts a new decode session" (Ok "A") (truncatedReplacement.Decode ([| 0x41uy |], true))

    let stripBom = Utf8.createDecoder { Fatal = false; IgnoreBOM = false }
    equal "split BOM prefix is buffered" (Ok "") (stripBom.Decode ([| 0xEFuy; 0xBBuy |], false))
    equal "initial split BOM is stripped" (Ok "A") (stripBom.Decode ([| 0xBFuy; 0x41uy |], true))
    equal "a new final call strips its initial BOM" (Ok "B") (stripBom.Decode ([| 0xEFuy; 0xBBuy; 0xBFuy; 0x42uy |], true))
    equal "a later BOM in the same session is preserved" (Ok "A\uFEFFB") (stripBom.Decode ([| 0x41uy; 0xEFuy; 0xBBuy; 0xBFuy; 0x42uy |], true))

    let keepBom = Utf8.createDecoder { Fatal = false; IgnoreBOM = true }
    equal "decoder constructor exposes canonical encoding" "utf-8" keepBom.Encoding
    equal "decoder constructor preserves fatal option" false keepBom.Fatal
    equal "decoder constructor preserves IgnoreBOM option" true keepBom.IgnoreBOM
    equal "IgnoreBOM preserves the initial BOM" (Ok "\uFEFFA") (keepBom.Decode ([| 0xEFuy; 0xBBuy; 0xBFuy; 0x41uy |], true))

    let replaceMalformed = Utf8.createDecoder { Fatal = false; IgnoreBOM = false }
    equal "replacement mode replaces malformed lead bytes" (Ok "��") (replaceMalformed.Decode ([| 0xC0uy; 0xAFuy |], true))

    let fatalFinal = Utf8.createDecoder { Fatal = true; IgnoreBOM = false }
    equal "fatal mode reports an invalid sequence" (Error InvalidUtf8Sequence) (fatalFinal.Decode ([| 0xC0uy |], true))
    equal "a fatal final call resets the session" (Ok "A") (fatalFinal.Decode ([| 0x41uy |], true))

    let afterFinalFatal = Utf8.createDecoder { Fatal = true; IgnoreBOM = false }
    equal "successful text before fatal final call" (Ok "A") (afterFinalFatal.Decode ([| 0x41uy |], false))
    equal "fatal final call reports an invalid sequence" (Error InvalidUtf8Sequence) (afterFinalFatal.Decode ([| 0xFFuy |], true))
    equal "fatal final error resets the BOM decision" (Ok "B") (afterFinalFatal.Decode ([| 0xEFuy; 0xBBuy; 0xBFuy; 0x42uy |], true))

    let afterInitialFatal = Utf8.createDecoder { Fatal = true; IgnoreBOM = false }
    equal "fatal nonfinal invalid byte" (Error InvalidUtf8Sequence) (afterInitialFatal.Decode ([| 0xFFuy |], false))

    equal
        "initial fatal error resets buffered state and BOM decision"
        (Ok "A")
        (afterInitialFatal.Decode ([| 0xEFuy; 0xBBuy; 0xBFuy; 0x41uy |], true))

    let afterTextThenFatal = Utf8.createDecoder { Fatal = true; IgnoreBOM = false }
    equal "successful prefix before failing chunk" (Ok "A") (afterTextThenFatal.Decode ([| 0x41uy |], false))
    equal "fatal nonfinal invalid byte after successful chunk" (Error InvalidUtf8Sequence) (afterTextThenFatal.Decode ([| 0xFFuy |], false))

    equal
        "fatal failure preserves the already-consumed BOM state"
        (Ok "\uFEFFB")
        (afterTextThenFatal.Decode ([| 0xEFuy; 0xBBuy; 0xBFuy; 0x42uy |], true))

    let afterConsumedBomThenFatal =
        Utf8.createDecoder { Fatal = true; IgnoreBOM = false }

    equal "successful BOM is stripped before fatal chunk" (Ok "A") (afterConsumedBomThenFatal.Decode ([| 0xEFuy; 0xBBuy; 0xBFuy; 0x41uy |], false))
    equal "fatal chunk after consumed BOM" (Error InvalidUtf8Sequence) (afterConsumedBomThenFatal.Decode ([| 0xFFuy |], false))

    equal
        "successful BOM state remains consumed after fatal chunk"
        (Ok "\uFEFFC")
        (afterConsumedBomThenFatal.Decode ([| 0xEFuy; 0xBBuy; 0xBFuy; 0x43uy |], true))

    let sameChunkPrefix = Utf8.createDecoder { Fatal = true; IgnoreBOM = false }
    equal "failed decode discards same-call valid prefix" (Error InvalidUtf8Sequence) (sameChunkPrefix.Decode ([| 0x41uy; 0xFFuy |], false))

    equal
        "failed same-call prefix does not consume the initial BOM decision"
        (Ok "C")
        (sameChunkPrefix.Decode ([| 0xEFuy; 0xBBuy; 0xBFuy; 0x43uy |], true))

    let afterBufferedPrefix = Utf8.createDecoder { Fatal = true; IgnoreBOM = false }
    equal "incomplete fatal prefix is buffered" (Ok "") (afterBufferedPrefix.Decode ([| 0xEFuy |], false))
    equal "invalid lead after buffered prefix fails" (Error InvalidUtf8Sequence) (afterBufferedPrefix.Decode ([| 0xFFuy; 0x41uy; 0x42uy |], false))
    equal "next stream call consumes the restored invalid lead" (Error InvalidUtf8Sequence) (afterBufferedPrefix.Decode ([| 0x43uy |], false))
    equal "final call returns bytes left after the restored lead" (Ok "ABCD") (afterBufferedPrefix.Decode ([| 0x44uy |], true))
    equal "call after final success starts fresh" (Ok "E") (afterBufferedPrefix.Decode ([| 0x45uy |], false))

    let afterInvalidContinuation =
        Utf8.createDecoder { Fatal = true; IgnoreBOM = false }

    equal "incomplete three-byte sequence is buffered" (Ok "") (afterInvalidContinuation.Decode ([| 0xE2uy; 0x82uy |], false))

    equal
        "ASCII byte invalidates the buffered continuation"
        (Error InvalidUtf8Sequence)
        (afterInvalidContinuation.Decode ([| 0x41uy; 0x42uy |], false))

    equal "next stream call consumes the restored ASCII and remainder" (Ok "AB") (afterInvalidContinuation.Decode ([||], false))
    equal "final call after recovered continuation succeeds" (Ok "C") (afterInvalidContinuation.Decode ([| 0x43uy |], true))

    let retainedInput = Utf8.createDecoder { Fatal = true; IgnoreBOM = false }
    equal "input ownership setup buffers an incomplete sequence" (Ok "") (retainedInput.Decode ([| 0xE2uy; 0x82uy |], false))
    let callerBytes = [| 0x41uy; 0x42uy |]
    equal "fatal continuation retains unread caller bytes" (Error InvalidUtf8Sequence) (retainedInput.Decode (callerBytes, false))
    callerBytes[0] <- 0x58uy
    callerBytes[1] <- 0x59uy
    equal "retained input is copied before caller mutation" (Ok "AB") (retainedInput.Decode ([||], false))

    let largeText = String.replicate 4096 "Aé🌍"
    let largeBytes = Utf8.encode largeText
    let largeDecoder = Utf8.createDecoder { Fatal = true; IgnoreBOM = false }
    equal "large mixed-scalar fatal chunk decodes in order" (Ok largeText) (largeDecoder.Decode (largeBytes, true))

    checks
