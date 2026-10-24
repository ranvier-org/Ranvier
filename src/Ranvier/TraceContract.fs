namespace Ranvier

/// <summary>A payload value of one of the four types every trace schema writes losslessly.</summary>
[<RequireQualifiedAccess>]
type TraceScalar =
    | Int of int
    | String of string
    | Bool of bool
    | Float of float

/// <summary>A recorded value in a trace contract document, marked as lossless or lossy.</summary>
[<RequireQualifiedAccess>]
type TracePayload =
    | Null
    /// <summary>An <c>int</c>, <c>string</c>, <c>bool</c> or <c>float</c>, held exactly.</summary>
    | Exact of TraceScalar
    /// <summary>The <c>data</c> written by version <c>version</c> of the codec named <c>codec</c>; lossless.</summary>
    | Encoded of codec: string * version: int * data: string
    /// <summary>The value's <c>TraceModel.valueText</c>: diagnostic text that will not decode to a value.</summary>
    | Lossy of text: string

/// <summary>An application codec for payload values of one shape.</summary>
/// <remarks>
/// <c>Encode</c> claims a value by returning its data, and returns <c>None</c> for a value of another shape.
/// <c>Decode</c> inverts <c>Encode</c> for data written by the same <c>Version</c>.
/// </remarks>
type TraceCodec =
    {
        Name: string
        Version: int
        Encode: obj -> string option
        Decode: string -> obj
    }

/// <summary>A payload that a <c>TraceCodecs</c> list will not encode or decode.</summary>
type TraceCodecException(message: string) =
    inherit System.Exception(message)

/// <summary>An ordered codec list, and whether a value claimed by none of them raises.</summary>
type TraceCodecs =
    {
        /// <summary>The codecs, tried in order: the first whose <c>Encode</c> returns <c>Some</c> encodes the value.</summary>
        Codecs: TraceCodec list
        /// <summary>True when a value claimed by no codec raises <c>TraceCodecException</c> instead of encoding as <c>Lossy</c>.</summary>
        Strict: bool
    }

/// <summary>Encoding and decoding of trace payloads through a <c>TraceCodecs</c> list.</summary>
[<RequireQualifiedAccess>]
module TraceCodecs =
    /// <summary>The non-strict list of <c>codecs</c>.</summary>
    let ofList (codecs: TraceCodec list) : TraceCodecs =
        { Codecs = codecs; Strict = false }

    /// <summary><c>codecs</c> with a value claimed by no codec raising <c>TraceCodecException</c>.</summary>
    let strict (codecs: TraceCodecs) : TraceCodecs =
        { codecs with Strict = true }

    /// <summary>
    /// The payload of <c>value</c>: <c>Null</c>, <c>Exact</c> for the four scalar types, <c>Encoded</c> by the first
    /// codec that claims it, else <c>Lossy</c> of its <c>valueText</c>.
    /// </summary>
    /// <exception cref="T:Ranvier.TraceCodecException">The list is strict and no codec claims <c>value</c>.</exception>
    let encode (codecs: TraceCodecs) (value: obj) : TracePayload =
        match value with
        | null -> TracePayload.Null
        | :? string as s -> TracePayload.Exact (TraceScalar.String s)
        | :? int as i -> TracePayload.Exact (TraceScalar.Int i)
        | :? bool as b -> TracePayload.Exact (TraceScalar.Bool b)
        | :? float as f -> TracePayload.Exact (TraceScalar.Float f)
        | value ->
            match
                codecs.Codecs
                |> List.tryPick (fun c -> c.Encode value |> Option.map (fun data -> c, data))
            with
            | Some (c, data) -> TracePayload.Encoded (c.Name, c.Version, data)
            | None when codecs.Strict ->
                let shape =
#if FABLE_COMPILER
                    TraceModel.valueText value
#else
                    value.GetType().FullName
#endif
                raise (TraceCodecException ("No trace codec encodes a value of " + shape + "."))
            | None -> TracePayload.Lossy (TraceModel.valueText value)

    /// <summary>The value of <c>payload</c>: null, the scalar, or the value decoded by its codec and version.</summary>
    /// <exception cref="T:Ranvier.TraceCodecException">
    /// The payload is <c>Lossy</c>, or its codec and version are not in the list.
    /// </exception>
    let decode (codecs: TraceCodecs) (payload: TracePayload) : obj =
        match payload with
        | TracePayload.Null -> null
        | TracePayload.Exact (TraceScalar.Int i) -> box i
        | TracePayload.Exact (TraceScalar.String s) -> box s
        | TracePayload.Exact (TraceScalar.Bool b) -> box b
        | TracePayload.Exact (TraceScalar.Float f) -> box f
        | TracePayload.Encoded (name, version, data) ->
            match
                codecs.Codecs
                |> List.tryFind (fun c -> c.Name = name && c.Version = version)
            with
            | Some c -> c.Decode data
            | None ->
                raise (
                    TraceCodecException (
                        "No trace codec decodes "
                        + name
                        + " version "
                        + string version
                        + "."
                    )
                )
        | TracePayload.Lossy text -> raise (TraceCodecException ("A lossy payload holds text alone: " + text))
