namespace Ranvier.Wire

/// <summary>Why a wire text failed to decode.</summary>
[<RequireQualifiedAccess>]
type WireError =
    /// <summary>The text is null or not JSON, or one of its strings holds a lone surrogate.</summary>
    | Syntax of message: string
    /// <summary>The text exceeds one of the codec's <see cref="T:Ranvier.Wire.WireLimits"/>.</summary>
    | Limit of message: string
    /// <summary>The JSON does not have the shape of the decoded type.</summary>
    | Shape of message: string

    /// <summary>The text carried by the case.</summary>
    member this.Message =
        match this with
        | Syntax message
        | Limit message
        | Shape message -> message

/// <summary>The largest wire text a codec decodes.</summary>
type WireLimits =
    {
        /// <summary>The deepest nesting of arrays and objects, between 1 and 1024; checked before parsing.</summary>
        MaxDepth: int
        /// <summary>The total number of array elements and object members, at least 1; checked before parsing.</summary>
        MaxItems: int
    }

    /// <summary>A depth of 64, as System.Text.Json's default, and 100,000 items.</summary>
    static member Default = { MaxDepth = 64; MaxItems = 100_000 }

/// <summary>Writes a <c>'T</c> as wire text and reads it back, with the same text on .NET and Fable.</summary>
type WireCodec<'T> =
    /// <summary>
    /// The wire text of the value; a null string is written as JSON <c>null</c>. Raises <c>ArgumentException</c>,
    /// naming the index, when a string, char or object key in the value holds a lone surrogate.
    /// </summary>
    abstract Encode: value: 'T -> string
    /// <summary>
    /// The value the text denotes, or the reason it denotes none; every failure is an <c>Error</c>, and a null text
    /// is <c>WireError.Syntax</c>.
    /// </summary>
    abstract Decode: text: string -> Result<'T, WireError>
