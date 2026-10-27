/// <summary>
/// Writes <c>src/Ranvier.View.WebCore/Generated/Objects.fs</c> and its report from the <c>Fable.Core.TS.Dom</c> interfaces
/// and the copied ABI in <c>src/Ranvier.View.WebCore/Interop</c>. Run by hand after a refresh of the interop:
/// <c>dotnet run --project tools/Ranvier.View.WebCore.Generate</c>.
/// </summary>
module Ranvier.View.WebCore.Generate.Program

open System
open System.IO
open System.Reflection
open Ranvier.View.WebCore.Generate.Emit

let private repository =
    Path.GetFullPath (Path.Combine (__SOURCE_DIRECTORY__, "..", ".."))

let private kebab (name: string) =
    let text =
        Text.RegularExpressions.Regex.Replace (name, "[A-Z]", fun m -> "-" + m.Value.ToLowerInvariant ())

    if text.StartsWith "webkit-" then "-" + text else text

let private hand (dom: Type) : Map<string, string> =
    let fixedBodies =
        [
            "EventTarget.addEventListener", "Events.Add(this, ``type``, callback, options)"
            "EventTarget.removeEventListener", "Events.Remove(this, ``type``, callback, options)"
            "EventTarget.dispatchEvent", "Events.Dispatch(this, event)"
            "Event.currentTarget.get", "Events.CurrentTarget this"
            "HTMLTemplateElement.content.get", "Templates.Content this"
            "Element.innerHTML.get", "Templates.InnerHTML this"
            "Element.innerHTML.set", "Templates.SetInnerHTML(this, value)"
            "Element.attributes.get", "Attributes.Map this"
            "Document.createElement", "Documents.CreateElement(this, tagName)"
            "Document.importNode<>", "Documents.ImportNode(this, node, options)"
            "ChildNode.after", "Nodes.After(this, nodes)"
            "ChildNode.before", "Nodes.Before(this, nodes)"
            "ChildNode.replaceWith", "Nodes.ReplaceWith(this, nodes)"
            "ParentNode.append", "Nodes.Append(this, nodes)"
            "ParentNode.prepend", "Nodes.Prepend(this, nodes)"
            "ParentNode.replaceChildren", "Nodes.ReplaceChildren(this, nodes)"
        ]

    // The CSS properties of CSSStyleProperties read and write getPropertyValue and setProperty by their CSS name.
    let styles =
        [
            for p in
                dom
                    .GetNestedType("CSSStyleProperties")
                    .GetProperties(
                        BindingFlags.Public
                        ||| BindingFlags.Instance
                        ||| BindingFlags.DeclaredOnly
                    ) do
                if
                    p.PropertyType = typeof<string>
                    && p.GetIndexParameters().Length = 0
                then
                    $"CSSStyleProperties.{p.Name}.get", $"Styles.Get(this, \"{kebab p.Name}\")"

                    if p.CanWrite then
                        $"CSSStyleProperties.{p.Name}.set", $"Styles.Set(this, \"{kebab p.Name}\", value)"
        ]

    Map.ofList (fixedBodies @ styles)

let private kinds =
    Map.ofList
        [
            "EventTarget", "Kind.Node"
            "Event", "Kind.Event"
            "CSSStyleDeclaration", "Kind.Style"
            "DOMTokenList", "Kind.TokenList"
            "DOMStringMap", "Kind.StringMap"
            "AbstractRange", "Kind.Range"
            "Selection", "Kind.Selection"
            "DOMRectReadOnly", "Kind.Rect"
            "NodeList", "Kind.NodeList"
            "HTMLCollection", "Kind.Collection"
            "HTMLCollectionOf`1", "Kind.Collection"
            "ValidityState", "Kind.Validity"
        ]

[<EntryPoint>]
let main argv =
    let interop = Path.Combine (repository, "src", "Ranvier.View.WebCore", "Interop")

    let output =
        match argv with
        | [| path |] -> path
        | _ -> Path.Combine (repository, "src", "Ranvier.View.WebCore", "Generated")

    let dom = typeof<Fable.Core.TS.Dom.Node>.DeclaringType

    let functions =
        Abi.read (File.ReadAllText (Path.Combine (interop, "RanvierWebCoreDom.h"))) (File.ReadAllText (Path.Combine (interop, "NativeDom.fs")))

    let tagMap =
        [
            for p in dom.GetNestedType("HTMLElementTagNameMap").GetProperties() -> p.Name, p.PropertyType.Name
        ]

    let input =
        {
            Dom = dom
            Functions = functions
            Hand = hand dom
            Extra =
                [
                    "EventTarget"
                    "Comment"
                    "NodeListOf`1"
                    "HTMLCollectionOf`1"
                    yield! tagMap |> List.map snd
                ]
            Kinds = kinds
        }

    let code, entries = Emit.run input tagMap

    let count f =
        entries
        |> List.filter (fun e -> f e.Status)
        |> List.length

    let mapped =
        count (function
            | Mapped _ -> true
            | _ -> false)

    let handWritten = count ((=) Hand)

    let unmapped =
        count (function
            | Unmapped _ -> true
            | _ -> false)

    let used =
        entries
        |> List.choose (fun e ->
            match e.Status with
            | Mapped f -> Some f
            | _ -> None)
        |> List.distinct
        |> List.length

    let classes =
        entries
        |> List.map (fun e -> e.Class)
        |> List.distinct
        |> List.length

    let summary =
        $"{classes} classes; {entries.Length} members: {mapped} mapped to {used} of {functions.Length} rv_dom_* functions, {handWritten} hand-written, {unmapped} unmapped"

    let header =
        [
            "// <auto-generated>"
            "// Written by tools/Ranvier.View.WebCore.Generate from Xantham.Fable.Core.TS and Interop/RanvierWebCoreDom.h; do not edit."
            $"// {summary}."
            "// An unmapped member raises NotSupportedException; Generated/report.txt lists each one with the reason."
            "// </auto-generated>"
            "namespace Ranvier.View.WebCore"
            ""
            "open RanvierWebCoreAbi"
            ""
        ]

    Directory.CreateDirectory output |> ignore

    File.WriteAllText (
        Path.Combine (output, "Objects.fs"),
        String.concat "\r\n" header
        + code.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n")
    )

    let report =
        [
            summary
            ""
            "Unmapped members by reason:"
            for reason, group in
                entries
                |> List.choose (fun e ->
                    match e.Status with
                    | Unmapped r -> Some (r, e)
                    | _ -> None)
                |> List.groupBy (fun (r, _) -> Text.RegularExpressions.Regex.Replace (r, @"argument \w+", "argument"))
                |> List.sortByDescending (fun (_, g) -> g.Length) do
                $"  {group.Length, 5}  {reason}"
            ""
            "Members by class (mapped / hand-written / unmapped):"
            for cls, group in
                entries
                |> List.groupBy (fun e -> e.Class)
                |> List.sortBy fst do
                let c f =
                    group
                    |> List.filter (fun e -> f e.Status)
                    |> List.length

                $"  {cls}: {c (function
                                | Mapped _ -> true
                                | _ -> false)} / {c ((=) Hand)} / {c (function
                                                                       | Unmapped _ -> true
                                                                       | _ -> false)}"
            ""
            "Unmapped members:"
            for e in entries do
                match e.Status with
                | Unmapped reason -> $"  {e.Class} {e.Interface}.{e.Member}: {reason}"
                | _ -> ()
        ]

    File.WriteAllLines (Path.Combine (output, "report.txt"), report)
    printfn "%s" summary
    0
