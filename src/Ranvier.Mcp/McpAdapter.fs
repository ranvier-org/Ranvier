namespace Ranvier.Mcp

open System
open System.Collections.Concurrent
open System.Text.Json
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open ModelContextProtocol
open ModelContextProtocol.Protocol
open ModelContextProtocol.Server

/// <summary>A tool the adapter lists and runs.</summary>
type internal AdapterTool =
    {
        Tool: Tool
        Run: CallToolRequestParams -> CancellationToken -> Task<CallToolResult>
    }

/// <summary>The URIs and JSON documents of the adapter's resources.</summary>
[<RequireQualifiedAccess>]
module internal Resources =
    [<Literal>]
    let State = "ranvier://state"

    let valueUri (name: string) =
        $"{State}/{name}"

    let private values (snapshot: Snapshot) =
        let values = JsonObject ()

        for KeyValue (name, value) in snapshot.Values do
            values[name] <- JsonNode.Parse (value.GetRawText ())

        values

    /// <summary>The whole snapshot: <c>{"version": n, "values": {...}}</c>.</summary>
    let state (snapshot: Snapshot) =
        let document = JsonObject ()
        document["version"] <- JsonValue.Create snapshot.Version
        document["values"] <- values snapshot
        document.ToJsonString ()

    /// <summary>One value: <c>{"version": n, "value": ...}</c>.</summary>
    let value (snapshot: Snapshot) (name: string) =
        let document = JsonObject ()
        document["version"] <- JsonValue.Create snapshot.Version
        document["value"] <- JsonNode.Parse (snapshot.Values[name].GetRawText())
        document.ToJsonString ()

/// <summary>
/// An MCP server surface over an <c>Exposure</c>: resources for the published state, subscriptions with
/// <c>notifications/resources/updated</c>, and the <c>snapshot</c> tool.
/// </summary>
/// <remarks>
/// <para>
/// Resources: <c>ranvier://state</c> holds the whole snapshot, and <c>ranvier://state/{name}</c> one exposed value with
/// the snapshot's version. Reads come from the published snapshot and never enter the graph.
/// </para>
/// <para>
/// Each publication notifies the subscribers of <c>ranvier://state</c> and of each value whose encoding changed, in
/// publication order. A memo cutoff, an equal write, or a re-run that re-encodes every value identically sends nothing;
/// a batch sends one notification per changed resource.
/// </para>
/// <para>
/// Construct the adapter on the graph thread, after <c>Exposure.Start</c>. <c>Options</c> is safe to use from any
/// thread, and several servers may share it; each server's subscriptions are its own.
/// </para>
/// </remarks>
[<Sealed>]
type McpAdapter(exposure: Exposure) =
    let names = exposure.Names

    let known =
        Set.ofList (
            Resources.State
            :: List.map Resources.valueUri names
        )

    let subscriptions =
        ConcurrentDictionary<McpServer, ConcurrentDictionary<string, byte>>(HashIdentity.Reference)

    let outbox =
        Channel.CreateUnbounded<struct (McpServer * string)>(UnboundedChannelOptions (SingleReader = true))

    // On the pool: a loop started here would capture the graph thread's context.
    let sender =
        Task.Run<unit>(fun () ->
            task {
                let reader = outbox.Reader
                let mutable reading = true

                while reading do
                    let! more = reader.WaitToReadAsync ()

                    if not more then
                        reading <- false
                    else
                        let mutable item = Unchecked.defaultof<_>

                        while reader.TryRead &item do
                            let struct (server, uri) = item

                            try
                                do!
                                    server.SendNotificationAsync (
                                        NotificationMethods.ResourceUpdatedNotification,
                                        ResourceUpdatedNotificationParams (Uri = uri)
                                    )
                            with _ ->
                                // The session has ended: its subscriptions go with it.
                                subscriptions.TryRemove server |> ignore
            })

    let notify (publication: Publication) =
        let uris =
            Resources.State
            :: (publication.Changed |> List.map Resources.valueUri)

        for KeyValue (server, subscribed) in subscriptions do
            for uri in uris do
                if subscribed.ContainsKey uri then
                    outbox.Writer.TryWrite (struct (server, uri))
                    |> ignore

    let subscription = exposure.Published.Subscribe notify

    let notFound (uri: string) =
        McpProtocolException ($"Unknown resource '{uri}'.", McpErrorCode.ResourceNotFound)

    let json (text: string) =
        use document = JsonDocument.Parse text
        document.RootElement.Clone ()

    let emptySchema = json """{"type":"object","properties":{}}"""

    let tools: AdapterTool list =
        [
            {
                Tool =
                    Tool (
                        Name = "snapshot",
                        Description = "The latest published snapshot of the exposed graph state: {\"version\": n, \"values\": {name: value}}.",
                        InputSchema = emptySchema
                    )
                Run =
                    fun _ _ ->
                        let text = Resources.state exposure.Snapshot

                        Task.FromResult (
                            CallToolResult (
                                Content = ResizeArray<ContentBlock> [ TextContentBlock (Text = text) :> ContentBlock ],
                                StructuredContent = Nullable (json text)
                            )
                        )
            }
        ]

    let resource (uri: string) (name: string) (description: string) =
        Resource (Uri = uri, Name = name, Description = description, MimeType = "application/json")

    let listResources =
        McpRequestHandler<ListResourcesRequestParams, ListResourcesResult>(fun _ _ ->
            let resources =
                resource Resources.State "state" "Every exposed value, from one snapshot, with its version."
                :: [
                    for name in names -> resource (Resources.valueUri name) name $"The exposed value '{name}', with the snapshot's version."
                ]

            ValueTask<ListResourcesResult>(ListResourcesResult (Resources = ResizeArray resources)))

    let readResource =
        McpRequestHandler<ReadResourceRequestParams, ReadResourceResult>(fun request _ ->
            let uri = request.Params.Uri
            let snapshot = exposure.Snapshot

            let text =
                if uri = Resources.State then
                    Resources.state snapshot
                elif known.Contains uri then
                    Resources.value snapshot (uri.Substring (Resources.State.Length + 1))
                else
                    raise (notFound uri)

            let contents =
                TextResourceContents (Uri = uri, MimeType = "application/json", Text = text)

            ValueTask<ReadResourceResult>(ReadResourceResult (Contents = ResizeArray<ResourceContents> [ contents :> ResourceContents ])))

    let subscribe =
        McpRequestHandler<SubscribeRequestParams, EmptyResult>(fun request _ ->
            let uri = request.Params.Uri

            if not (known.Contains uri) then
                raise (notFound uri)

            let subscribed =
                subscriptions.GetOrAdd (request.Server, fun _ -> ConcurrentDictionary<string, byte>())

            subscribed[uri] <- 0uy
            ValueTask<EmptyResult>(EmptyResult ()))

    let unsubscribe =
        McpRequestHandler<UnsubscribeRequestParams, EmptyResult>(fun request _ ->
            match subscriptions.TryGetValue request.Server with
            | true, subscribed -> subscribed.TryRemove request.Params.Uri |> ignore
            | _ -> ()

            ValueTask<EmptyResult>(EmptyResult ()))

    let listTools =
        McpRequestHandler<ListToolsRequestParams, ListToolsResult>(fun _ _ ->
            ValueTask<ListToolsResult>(ListToolsResult (Tools = ResizeArray (tools |> List.map _.Tool))))

    let callTool =
        McpRequestHandler<CallToolRequestParams, CallToolResult>(fun request cancellationToken ->
            match
                tools
                |> List.tryFind (fun tool -> tool.Tool.Name = request.Params.Name)
            with
            | Some tool -> ValueTask<CallToolResult>(tool.Run request.Params cancellationToken)
            | None -> raise (McpProtocolException ($"Unknown tool '{request.Params.Name}'.", McpErrorCode.InvalidParams)))

    /// <summary>Server options carrying the adapter's resources, subscriptions and tools.</summary>
    member val Options =
        McpServerOptions (
            ServerInfo = Implementation (Name = "ranvier", Version = "0.1.0"),
            // 2026-07-28 replaces resources/subscribe with subscriptions/listen, which the adapter does not serve.
            ProtocolVersion = "2025-11-25",
            Capabilities =
                ServerCapabilities (
                    Resources = ResourcesCapability (Subscribe = Nullable true, ListChanged = Nullable false),
                    Tools = ToolsCapability (ListChanged = Nullable false)
                ),
            Handlers =
                McpServerHandlers (
                    ListResourcesHandler = listResources,
                    ReadResourceHandler = readResource,
                    SubscribeToResourcesHandler = subscribe,
                    UnsubscribeFromResourcesHandler = unsubscribe,
                    ListToolsHandler = listTools,
                    CallToolHandler = callTool
                )
        )

    /// <summary>Stops notifying subscribers. Idempotent.</summary>
    member _.Dispose() =
        subscription.Dispose ()
        outbox.Writer.TryComplete () |> ignore

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()
