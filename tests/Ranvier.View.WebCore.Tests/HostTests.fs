module Ranvier.View.WebCore.Tests.HostTests

open System
open Expecto
open Fable.Core.TS.Dom
open Ranvier.View
open Ranvier.View.Twin
open Ranvier.View.WebCore
open Ranvier.View.WebCore.Tests.Support

let private name (node: obj) =
    match node with
    | null -> "null"
    | :? Node as node -> node.nodeName
    | other -> other.GetType().Name

let private relative (node: Node voption) =
    match node with
    | ValueSome node -> name node
    | ValueNone -> "none"

/// <summary>Drives every IHost member and records what each returns or leaves in the tree.</summary>
let private script (host: Native.IHost) : string list =
    let log = ResizeArray<string>()
    let document = host.Document
    let root = document.createElement "div" :> Node
    let span = document.createElement "span" :> Node
    let bold = document.createElement "b" :> Node
    let text = document.createTextNode "x" :> Node
    let comment = document.createComment "m" :> Node

    let children (parent: Node) =
        let rec go (node: Node voption) acc =
            match node with
            | ValueSome node -> go (host.NextOf node) (name node :: acc)
            | ValueNone -> List.rev acc

        go (host.FirstChildOf parent) []
        |> String.concat ","

    host.AppendChild (root, span)
    host.AppendChild (root, text)
    host.InsertBefore (root, comment, span)
    host.InsertBefore (root, bold, Unchecked.defaultof<_>)
    log.Add $"children {children root}"
    log.Add $"last {relative (host.LastChildOf root)}"
    log.Add $"parent {relative (host.ParentOf span)} / {relative (host.ParentOf root)}"
    log.Add $"data {host.DataOf text}"
    host.SetData (text, "y")
    log.Add $"data {host.DataOf text} {host.DataOf comment}"

    let element = span :?> Element
    host.SetAttribute (element, "id", "v")
    log.Add $"""attribute {host.GetAttribute (element, "id")} {isNull (host.GetAttribute (element, "missing"))}"""
    host.RemoveAttribute (element, "id")
    log.Add $"""removed {isNull (host.GetAttribute (element, "id"))}"""

    let deep = host.CloneNode (root, true)
    let shallow = host.CloneNode (root, false)
    log.Add $"clones {children deep} / {children shallow} / {obj.ReferenceEquals (deep, root)}"

    let nodes = Array.zeroCreate<obj> 4
    host.Walk (root, [| 0; 3; 1; 3; 1; 1; 3 |], nodes, 1)
    log.Add $"""walk {nodes |> Array.map name |> String.concat ","} {obj.ReferenceEquals (nodes[2], span)}"""

    host.DeleteBetween (comment, bold)
    log.Add $"deleted {children root}"
    host.Remove comment
    host.RemoveChild (root, bold)
    log.Add $"emptied [{children root}]"

    let button = document.createElement "button"
    host.AppendChild (document.body, button)
    let mutable count = 0
    let mutable seen = ""

    let remove =
        host.Listen (
            button,
            "click",
            fun event ->
                count <- count + 1
                host.OverrideCurrentTarget (event, fun () -> box root)

                seen <-
                    (unbox<Event> event).currentTarget
                    |> Option.map box
                    |> Option.toObj
                    |> name
        )

    button.click ()
    remove ()
    button.click ()
    log.Add $"listener {count} {seen}"
    host.Remove button
    List.ofSeq log

[<Tests>]
let tests =
    testList
        "IHost"
        [
            testCase "each member matches the DOM twin"
            <| fun () ->
                onHostThread (fun () ->
                    let twin = TwinHost.Create ()

                    let expected =
                        try
                            script twin
                        finally
                            (twin.Document :> IDisposable).Dispose()

                    use host = WebCoreHost.Create (100, 100)
                    let actual = script host
                    Expect.sequenceEqual actual expected "the native trace equals the twin's"
                    Expect.equal expected.Length 12 "every step recorded")
        ]
