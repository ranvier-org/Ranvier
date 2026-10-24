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
    /// <remarks>
    /// Under Fable an <c>int</c> and a <c>float</c> share one runtime representation: a number with an integral
    /// value in the 32-bit signed range encodes as <c>Int</c>, and every other number, NaN and the infinities
    /// included, encodes as <c>Float</c>. A <c>float</c> such as <c>2.0</c> therefore encodes as <c>Int 2</c> on Fable
    /// and as <c>Float 2.0</c> on .NET.
    /// </remarks>
    /// <exception cref="T:Ranvier.TraceCodecException">The list is strict and no codec claims <c>value</c>.</exception>
    let encode (codecs: TraceCodecs) (value: obj) : TracePayload =
        match value with
        | null -> TracePayload.Null
        | :? string as s -> TracePayload.Exact (TraceScalar.String s)
        | :? bool as b -> TracePayload.Exact (TraceScalar.Bool b)
#if FABLE_COMPILER
        | value when Platform.isInt32 value -> TracePayload.Exact (TraceScalar.Int (unbox<int> value))
        | :? float as f -> TracePayload.Exact (TraceScalar.Float f)
#else
        | :? int as i -> TracePayload.Exact (TraceScalar.Int i)
        | :? float as f -> TracePayload.Exact (TraceScalar.Float f)
#endif
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
    /// <remarks>Fable records no creation sites, so every ref under Fable is <c>Named</c>.</remarks>
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

/// <summary>A node address in a trace lookup.</summary>
[<RequireQualifiedAccess>]
type TraceAddress =
    /// <summary>A graph-local node id.</summary>
    | ById of node: int
    /// <summary>An identity path, resolved as by <c>TraceModel.resolve</c>.</summary>
    | ByPath of path: string

/// <summary>The node at a <c>TraceAddress</c> in a graph's checkpoint and retained events.</summary>
[<RequireQualifiedAccess>]
type TraceLookup =
    /// <summary>A live node created in the retained events.</summary>
    | Live of TraceNodeRef
    /// <summary>A disposed node: the <c>Dispose</c> seq, or 0 when the dispose is folded into the checkpoint.</summary>
    | Disposed of node: TraceNodeRef * seq: int
    /// <summary>A live node created before the checkpoint: its <c>NodeNew</c> and earlier causes are folded into it.</summary>
    | Checkpointed of TraceNodeRef
    /// <summary>
    /// The address matches no node in the checkpoint or the retained events, including a node released by the
    /// checkpoint.
    /// </summary>
    | Missing
    /// <summary>A node created by another graph, or a node that records no <c>NodeNew</c>.</summary>
    | Foreign

/// <summary>A node in a <c>TraceGraphState</c>.</summary>
type TraceNodeState =
    {
        Ref: TraceNodeRef
        Kind: TraceNodeKind
        /// <summary>The owner id recorded by the node's <c>NodeNew</c>, or 0.</summary>
        Owner: int
        /// <summary>The seq of the node's <c>NodeNew</c>.</summary>
        Created: int
        /// <summary>The number of runs recorded.</summary>
        Runs: int
        /// <summary>The seq of the node's last <c>RunStart</c>, or 0.</summary>
        LastRun: int
        /// <summary>The first dirty <c>Mark</c> seq since the last <c>RunStart</c>, or 0.</summary>
        PendingMark: int
        Status: TraceNodeStatus
        /// <summary>
        /// The latest value or exception recorded by a moving <c>Write</c>, a <c>Moved</c>, or an async source's
        /// <c>Settle</c> or <c>Fail</c>, or <c>None</c>. A value folded into the checkpoint is <c>Lossy</c>.
        /// </summary>
        Value: TracePayload option
    }

/// <summary>A graph's state at <c>Seq</c>, folded from its checkpoint and retained events.</summary>
type TraceGraphState =
    {
        /// <summary>The contract schema version.</summary>
        Schema: int
        /// <summary>The seq of the last event folded, or 0.</summary>
        Seq: int
        /// <summary>The graph root's owner id.</summary>
        Root: int
        /// <summary>The checkpoint's name, or <c>None</c> before the first checkpoint.</summary>
        Checkpoint: string option
        /// <summary>The seq of the first retained event.</summary>
        SeqFrom: int
        Nodes: Map<int, TraceNodeState>
        Owners: Map<int, TraceSnapshotOwner>
        /// <summary>Each computation's sources, in slot order. Empty lists are left out.</summary>
        Sources: Map<int, int list>
        /// <summary>Each source's observers. Empty sets are left out.</summary>
        Observers: Map<int, Set<int>>
    }

/// <summary>One run of a node, in <c>Trace.runs</c>.</summary>
type TraceRunState =
    {
        /// <summary>The run number, from 1.</summary>
        Run: int
        /// <summary>The seq of the run's <c>RunStart</c>.</summary>
        Start: int
        /// <summary>How the run ended, or <c>None</c> while it is open.</summary>
        Status: RunStatus option
        /// <summary>True when the run moved the node's value, during the run or at its flight's settle.</summary>
        Moved: bool
        /// <summary>The end of the run's cause chain.</summary>
        Root: WhyRoot
        /// <summary>The number of the flush the run started in, or 0 outside every flush.</summary>
        Flush: int
        /// <summary>The value recorded by the run's <c>Moved</c>, else by the latest earlier run's.</summary>
        Value: TracePayload option
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

    /// <summary>The node at <c>address</c> in <c>checkpoint</c> advanced by <c>events</c>, which follow it.</summary>
    let lookup (checkpoint: TraceSnapshot) (events: TraceEvent[]) (address: TraceAddress) : TraceLookup =
        let snap = TraceModel.fold checkpoint events

        let id =
            match address with
            | TraceAddress.ById id when snap.Nodes.ContainsKey id -> Some id
            | TraceAddress.ById _ -> None
            | TraceAddress.ByPath path -> TraceModel.resolve snap path

        match
            id
            |> Option.bind (fun id ->
                nodeRef snap id
                |> Option.map (fun r -> r, snap.Nodes[id]))
        with
        | None -> TraceLookup.Missing
        | Some (r, n) when n.Status = TraceNodeStatus.Disposed ->
            let seq =
                events
                |> Array.tryFindBack (fun e -> e.Kind = TraceEventKind.Dispose && e.Node = r.Node)
                |> Option.map _.Seq
                |> Option.defaultValue 0

            TraceLookup.Disposed (r, seq)
        | Some (r, n) when n.Created <= checkpoint.Seq -> TraceLookup.Checkpointed r
        | Some (r, _) -> TraceLookup.Live r

    /// <summary>The schema version of the contract documents.</summary>
    [<Literal>]
    let Schema = 3

    /// <summary>
    /// The state folded from <c>checkpoint</c> and <c>events</c>, which follow it, with retained values encoded by
    /// <c>codecs</c>.
    /// </summary>
    /// <exception cref="T:Ranvier.TraceCodecException">
    /// <c>codecs</c> is strict and no codec claims a retained value.
    /// </exception>
    let state (codecs: TraceCodecs) (checkpoint: TraceCheckpoint) (events: TraceEvent[]) : TraceGraphState =
        let snap = TraceModel.fold checkpoint.Snapshot events
        let values = System.Collections.Generic.Dictionary<int, obj>()

        // The value events of `TraceModel.fold`, the latest per node.
        for e in events do
            match e.Kind with
            | TraceEventKind.Write when e.Flag = 1 -> values[e.Node] <- e.Payload
            | TraceEventKind.Moved when not (Platform.isNull e.Payload) -> values[e.Node] <- e.Payload
            | TraceEventKind.Settle
            | TraceEventKind.Fail when e.Arg = 0 -> values[e.Node] <- e.Payload
            | _ -> ()

        let node (id: int) (n: TraceSnapshotNode) =
            {
                Ref = (nodeRef snap id).Value
                Kind = n.Kind
                Owner = n.Owner
                Created = n.Created
                Runs = n.Runs
                LastRun = n.LastRun
                PendingMark = n.PendingMark
                Status = n.Status
                Value =
                    match values.TryGetValue id with
                    | true, value -> Some (TraceCodecs.encode codecs value)
                    | _ -> n.Value |> Option.map TracePayload.Lossy
            }

        {
            Schema = Schema
            Seq = snap.Seq
            Root = snap.Root
            Checkpoint = Option.ofObj checkpoint.Name
            SeqFrom =
                if events.Length = 0 then
                    checkpoint.Snapshot.Seq + 1
                else
                    events[0].Seq
            Nodes = snap.Nodes |> Map.map node
            Owners = snap.Owners
            Sources = snap.Sources
            Observers = snap.Observers
        }

    /// <summary>The runs of <c>history</c>, with their values encoded by <c>codecs</c>.</summary>
    /// <exception cref="T:Ranvier.TraceCodecException"><c>codecs</c> is strict and no codec claims a run's value.</exception>
    let runs (codecs: TraceCodecs) (history: TraceHistory) : TraceRunState list =
        history.Runs
        |> List.map (fun r ->
            {
                Run = r.Run
                Start = r.Start
                Status = r.Status
                Moved = r.Moved
                Root = r.Root
                Flush = r.Flush
                Value = r.Value |> Option.map (TraceCodecs.encode codecs)
            })
