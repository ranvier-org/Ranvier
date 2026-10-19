/// <summary>Server rendering of a template component as one request with its own graph and owners.</summary>
module Ranvier.View.Foundation.Server

open System
open global.Ranvier
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template

/// <summary>The settings of one server render.</summary>
type Options =
    {
        /// <summary>Whether a browser-only part raises (<c>Reject</c>) or is dropped (<c>Omit</c>).</summary>
        Policy: BrowserPolicy
        /// <summary>The root prefix of the ids <c>Setup.CreateId</c> returns; must match the client's.</summary>
        IdPrefix: string
        /// <summary>The options of the request graph.</summary>
        GraphOptions: GraphOptions
    }

module Options =
    let defaults =
        {
            Policy = Reject
            IdPrefix = "r"
            GraphOptions = GraphOptions.Default
        }

/// <summary>
/// The owners of a request: each nested instance runs under its own child of <c>lifetime</c>, and its <c>Setup</c>
/// registers cleanups on that child until the instance is written.
/// </summary>
[<Sealed>]
type internal RequestScope(lifetime: MountLifetime) =
    inherit ServerScope()

    override _.Nest body =
        lifetime.Child (
            ignore,
            fun child ->
                let completed = ref false

                let setup =
                    {
                        OnCleanup =
                            fun cleanup ->
                                if completed.Value then
                                    raise (InvalidOperationException "Server component setup is completed")

                                child.OnCleanup cleanup
                    }

                try
                    body (RequestScope child) setup
                finally
                    completed.Value <- true
        )
        |> ignore

    override _.Attempt body =
        let attempt: MountLifetime ref = ref null

        try
            lifetime.Child (
                ignore,
                fun child ->
                    attempt.Value <- child
                    body (RequestScope child)
            )
            |> ignore
        with _ ->
            if not (isNull attempt.Value) then
                attempt.Value.Dispose ()

            reraise ()

/// <summary>
/// Renders <c>definition</c> into <c>sink</c> inside <c>graph</c> under a new request root, and disposes the root before
/// returning or raising.
/// </summary>
let internal renderOn (graph: Graph) (options: Options) (definition: Component<'props>) (props: 'props) (sink: HtmlSink) : unit =
    let cleanupErrors = ResizeArray<exn>()

    let failure =
        graph.Run (fun () ->
            Api.untrack (fun () ->
                let root =
                    MountLifetime.Root (graph, (fun errors -> cleanupErrors.AddRange errors), ignore)

                let failure =
                    try
                        try
                            let backend = HtmlBackend (options.Policy, sink)
                            let writer = ServerWriter backend
                            backend.Writer <- writer
                            sink.Append "<!--[r-->"
                            writer.Write (RequestScope root, definition, box props)
                            sink.Append "<!--]-->"
                            None
                        with error ->
                            Some error
                    finally
                        try
                            root.Dispose ()
                        with error ->
                            cleanupErrors.Add error

                failure))

    match failure, List.ofSeq cleanupErrors with
    | None, [] -> ()
    | Some error, [] -> raise error
    | Some error, errors -> raise (AggregateException (error :: errors))
    | None, errors -> raise (AggregateException ("Server render cleanup failed", errors))

/// <summary>Renders a template component as one hydratable root; raises when a read outside every boundary is pending or fails.</summary>
/// <remarks>
/// <para>
/// The output is <c>&lt;!--[r--&gt;</c>, the instance, <c>&lt;!--]--&gt;</c>. The render runs untracked in a new graph
/// built from <c>options.GraphOptions</c>; every instance runs under its own owner of the request root, and the root
/// and the graph are disposed before <c>render</c> returns or raises.
/// </para>
/// <para>
/// A signal of a graph outside the request is read as it is. A write to it from the request thread raises under
/// <c>ThreadAffinity.Guarded</c> and changes state shared with every other reader under <c>ThreadAffinity.Unchecked</c>.
/// Under Fable <c>render</c> is synchronous only.
/// </para>
/// </remarks>
let render (options: Options) (definition: Component<'props>) (props: 'props) : string =
    let sink = HtmlSink ()
    use graph = new Graph (options.GraphOptions)
    renderOn graph options definition props sink
    sink.ToString ()

/// <summary>Renders as <c>render</c> does, then passes the output's segments to <c>write</c> in order; a failed render writes nothing.</summary>
let renderTo (options: Options) (definition: Component<'props>) (props: 'props) (write: string -> unit) : unit =
    let sink = HtmlSink ()

    do
        use graph = new Graph (options.GraphOptions)
        renderOn graph options definition props sink

    sink.Segments write
