module Ranvier.Wire.Tests.WireSupport

/// <summary>Whether the suite runs under Fable.</summary>
let isFable =
#if FABLE_COMPILER
    true
#else
    false
#endif

/// <summary>The UTF-16 code unit as a one-character string; lone surrogates included.</summary>
let u (code: int) =
    string (char code)

/// <summary>The JSON escape <c>\u</c> followed by the four hex digits, as written in wire text.</summary>
let esc (hex: string) =
    "\\u" + hex

let quote = "\""
