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

/// <summary>How far a node's identity path is stable.</summary>
[<RequireQualifiedAccess>]
type TracePathStability =
    /// <summary>
    /// Every segment is a label, a <c>createRoot</c> scope or a node kind. Equal across reruns and across code edits
    /// that keep the labelled tree and its creation order.
    /// </summary>
    | Named
    /// <summary>
    /// A segment is a creation site. Equal across reruns of the same build; an edit that moves the site changes it.
    /// </summary>
    | Located

/// <summary>A node's identity in each of its scopes.</summary>
type TraceNodeRef =
    {
        /// <summary>The graph's root owner id. With <c>Node</c>, valid for one graph instance in one process.</summary>
        Graph: int
        /// <summary>The graph-local node id, valid for the graph instance alone.</summary>
        Node: int
        /// <summary>The identity path, with its <c>@k</c> suffix when more than one node has held the path.</summary>
        Path: string
        Stability: TracePathStability
        Label: string option
    }

/// <summary>Contract values over a trace snapshot.</summary>
[<RequireQualifiedAccess>]
module TraceContract =
    // The last segment of an owner path, without its `#n` sibling suffix.
    let private bareSegment (path: string) =
        let mutable start = 0
        let mutable hash = -1
        let mutable i = 0

        while i < path.Length do
            match path[i] with
            | '\\' -> i <- i + 1
            | '/' ->
                start <- i + 1
                hash <- -1
            | '#' -> hash <- i
            | _ -> ()

            i <- i + 1

        if hash >= 0 then
            path.Substring (start, hash - start)
        else
            path.Substring start

    let rec private nodeLocated (s: TraceSnapshot) (node: int) =
        match s.Nodes.TryFind node with
        | Some n ->
            (n.Label.IsNone && n.Site <> "?")
            || ownerLocated s n.Owner
        | None -> false

    and private ownerLocated (s: TraceSnapshot) (owner: int) =
        let owner = if owner = 0 then s.Root else owner

        match s.Owners.TryFind owner with
        | None -> false
        | Some o when o.Id = s.Root -> false
        | Some o when o.Host <> 0 -> nodeLocated s o.Host
        | Some o when o.Root -> ownerLocated s o.Parent
        | Some o ->
            (o.Label.IsNone && bareSegment o.Path <> "owner")
            || ownerLocated s o.Parent

    /// <summary>The ref of node <c>node</c> in <c>snapshot</c>, or <c>None</c> when the snapshot does not hold it.</summary>
    let nodeRef (snapshot: TraceSnapshot) (node: int) : TraceNodeRef option =
        snapshot.Nodes.TryFind node
        |> Option.map (fun n ->
            {
                Graph = snapshot.Root
                Node = node
                Path = TraceModel.pathOf snapshot node
                Stability =
                    if nodeLocated snapshot node then
                        TracePathStability.Located
                    else
                        TracePathStability.Named
                Label = n.Label
            })
