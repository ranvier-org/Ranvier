namespace Ranvier.Dom

/// <summary>Options for a stateful UTF-8 decoder.</summary>
type Utf8DecoderOptions =
    {
        /// <summary>Whether malformed input is reported as an error instead of replaced with U+FFFD.</summary>
        Fatal: bool
        /// <summary>Whether an initial UTF-8 byte order mark is preserved in decoded text.</summary>
        IgnoreBOM: bool
    }

/// <summary>The UTF-16 units consumed and UTF-8 bytes written by <c>Utf8.encodeInto</c>.</summary>
type EncodeIntoResult =
    {
        /// <summary>The number of UTF-16 code units consumed.</summary>
        Read: int
        /// <summary>The number of UTF-8 bytes written.</summary>
        Written: int
    }

/// <summary>A malformed UTF-8 sequence encountered by a fatal decoder.</summary>
type Utf8DecodeError =
    /// <summary>The input contains a byte sequence that is not valid UTF-8.</summary>
    | InvalidUtf8Sequence

/// <summary>A stateful UTF-8 decoder.</summary>
/// <remarks>
/// Calls on one decoder must be made by a single caller and serialized in order. It is not thread-safe.
/// A fatal non-final error leaves unread bytes from the failed call pending; later calls process them
/// before newly supplied bytes. Repeated fatal errors can therefore retain more than one short prefix.
/// A final call, including one that reports a fatal decoding error, resets the decoder for a new session.
/// </remarks>
type IUtf8Decoder =
    /// <summary>The decoder's encoding label, always <c>utf-8</c>.</summary>
    abstract Encoding: string
    /// <summary>Whether malformed input is reported as an error.</summary>
    abstract Fatal: bool
    /// <summary>Whether an initial UTF-8 byte order mark is preserved.</summary>
    abstract IgnoreBOM: bool
    /// <summary>Decodes a byte chunk, optionally flushing and resetting the current session.</summary>
    abstract Decode: chunk: byte array * finalChunk: bool -> Result<string, Utf8DecodeError>
