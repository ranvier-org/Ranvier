/// <summary>
/// The head entries of each document across every mount and graph: per key, the live entries in activation order.
/// </summary>
module internal Ranvier.View.Foundation.DocumentHead

open System.Collections.Generic
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation.Representation

[<Emit("$0.createElement($1)")>]
let private createElement (document: Document) (tag: string) : Element = jsNative

[<Emit("$0.replaceWith($1)")>]
let private replaceWith (node: Element) (next: Element) : unit = jsNative

[<Emit("$0.remove()")>]
let private detach (node: Element) : unit = jsNative

[<Emit("$0.isConnected")>]
let private isConnected (node: Element) : bool = jsNative

[<Emit("$0.getAttribute('data-ranvier-head')")>]
let private markOf (node: Element) : string = jsNative

[<Emit("$0.head.appendChild($1)")>]
let private appendToHead (document: Document) (node: Element) : unit = jsNative

/// <summary>The head element of the document marked with the key and absent from <c>claimed</c>; null when there is none.</summary>
[<Emit("(Array.from($0.head.querySelectorAll('[data-ranvier-head]')).find(node => node.getAttribute('data-ranvier-head') === $1 && !$2.includes(node)) ?? null)")>]
let private marked (document: Document) (key: string) (claimed: ResizeArray<Element>) : Element = jsNative

/// <summary>Removes the marked head elements of the document absent from <c>claimed</c>.</summary>
[<Emit("Array.from($0.head?.querySelectorAll('[data-ranvier-head]') ?? []).filter(node => !$1.includes(node)).forEach(node => node.remove())")>]
let private removeUnclaimed (document: Document) (claimed: ResizeArray<Element>) : unit = jsNative

/// <summary>
/// The inverse of edits to the document's head: its child list, the attributes of each child element in order and the
/// children of each <c>title</c> element are set back. Equal parts are left untouched.
/// </summary>
[<Emit("(() => { const h = $0.head; if (!h) return () => {}; const c = Array.from(h.childNodes), a = c.map(n => n.nodeType === 1 ? Array.from(n.attributes, x => [x.name, x.value]) : null), t = c.map(n => n.nodeName === 'TITLE' ? Array.from(n.childNodes) : null); return () => { c.forEach((n, i) => { if (h.childNodes[i] !== n) h.insertBefore(n, h.childNodes[i] ?? null); }); while (h.childNodes.length > c.length) h.lastChild.remove(); c.forEach((n, i) => { const x = a[i]; if (x && (n.attributes.length !== x.length || x.some(([k, v], j) => n.attributes[j].name !== k || n.attributes[j].value !== v))) { for (const y of Array.from(n.attributes)) n.removeAttribute(y.name); for (const [k, v] of x) n.setAttribute(k, v); } const y = t[i]; if (y && (n.childNodes.length !== y.length || y.some((m, j) => n.childNodes[j] !== m))) n.replaceChildren(...y); }); }; })()")>]
let keep (document: Document) : unit -> unit = jsNative

[<Emit("$0.title")>]
let private titleOf (document: Document) : string = jsNative

[<Emit("$0.title = $1")>]
let private setTitleOf (document: Document) (value: string) : unit = jsNative

/// <summary>The document owning the node; the global document when the node is itself a document.</summary>
[<Emit("($0.ownerDocument ?? document)")>]
let documentOf (node: Node) : Document = jsNative

/// <summary>A head entry of a document, shown by <c>activate</c> and removed by <c>withdraw</c>.</summary>
[<Sealed; AllowNullLiteral>]
type Record internal (registry: Registry, kind: HeadKind, key: string, element: Element) =
    member _.Registry = registry
    member _.Kind = kind
    /// <summary>The entry's key; null for an unkeyed link.</summary>
    member _.Key = key
    /// <summary>The entry's head element; for a title entry, the adopted marked title element or null.</summary>
    member _.Element = element
    member val Title = "" with get, set
    member val Shown = false with get, set
    member val Withdrawn = false with get, set

/// <summary>The head entries of one document.</summary>
and [<Sealed>] Registry(document: Document) =
    member _.Document = document
    /// <summary>Per key, the live records in activation order; the last one is shown.</summary>
    member val Live = Dictionary<string, ResizeArray<Record>>()
    /// <summary>The elements of records not yet withdrawn: adopted marked elements and created ones.</summary>
    member val Claimed = ResizeArray<Element>()
    /// <summary>While a title record is live, the document's title from before the first one.</summary>
    member val Original: string = null with get, set
    /// <summary>The records created and not yet withdrawn.</summary>
    member val Records = 0 with get, set

// The registries of documents with records not yet withdrawn.
let private registries = ResizeArray<Registry>()

/// <summary>The registry of the document; null when it has no record.</summary>
let private existingRegistry (document: Document) =
    let mutable found: Registry = Unchecked.defaultof<_>

    for index in 0 .. registries.Count - 1 do
        if
            isNull (box found)
            && obj.ReferenceEquals (registries[index].Document, document)
        then
            found <- registries[index]

    found

let private registryOf (document: Document) =
    let mutable found = existingRegistry document

    if isNull (box found) then
        found <- Registry document
        registries.Add found

    found

let private stackOf (registry: Registry) (key: string) =
    match registry.Live.TryGetValue key with
    | true, entries -> entries
    | _ ->
        let entries = ResizeArray<Record>()
        registry.Live[key] <- entries
        entries

let private indexOf (items: ResizeArray<'T>) (item: 'T) =
    let mutable found = -1

    for index in 0 .. items.Count - 1 do
        if
            found < 0
            && obj.ReferenceEquals (items[index], item)
        then
            found <- index

    found

/// <summary>The connected element of the key adopted by a record other than the owner of <c>own</c>; null when there is none.</summary>
let private heldFor (registry: Registry) (key: string) (own: Element) : Element =
    let mutable found: Element = Unchecked.defaultof<_>

    for index in 0 .. registry.Claimed.Count - 1 do
        let element = registry.Claimed[index]

        if
            isNull (box found)
            && not (obj.ReferenceEquals (element, own))
            && isConnected element
            && markOf element = key
        then
            found <- element

    found

/// <summary>
/// A record for an entry of the document. A keyed record adopts the document's marked head element of the same key when
/// one is unclaimed; otherwise a meta or link record owns a new element marked with its key, and a title record has no
/// element. An unkeyed link always owns a new element.
/// </summary>
let create (document: Document) (kind: HeadKind) (key: string) : Record =
    let registry = registryOf document
    registry.Records <- registry.Records + 1

    let server =
        if isNull key then
            Unchecked.defaultof<_>
        else
            marked document key registry.Claimed

    if not (isNull (box server)) then
        registry.Claimed.Add server
        Record (registry, kind, key, server)
    elif kind = HeadKind.Title then
        Record (registry, kind, key, null)
    else
        let element =
            createElement document (if kind = HeadKind.Meta then "meta" else "link")

        element.setAttribute ("data-ranvier-head", (if isNull key then "" else key))
        registry.Claimed.Add element
        Record (registry, kind, key, element)

/// <summary>Removes each marked head element of the document that its registry has not claimed.</summary>
let removeUnadopted (document: Document) =
    let registry = existingRegistry document

    removeUnclaimed
        document
        (if isNull (box registry) then
             ResizeArray ()
         else
             registry.Claimed)

/// <summary>Sets the attribute of a head element to <c>Some</c> value and removes it for <c>None</c>, when it differs.</summary>
let writeAttribute (element: Element) (name: string) (value: string option) =
    match value with
    | Some text ->
        if element.getAttribute name <> Some text then
            element.setAttribute (name, text)
    | None ->
        if element.hasAttribute name then
            element.removeAttribute name

/// <summary>
/// Shows the record above the key's other live records. A title record writes the document's title when it differs; the
/// first title record of a key keeps the document's title for <c>withdraw</c>. The first shown meta or link record of a
/// key takes the place of a marked element adopted by a record not yet shown.
/// </summary>
let activate (record: Record) =
    let registry = record.Registry

    if record.Withdrawn then
        ()
    elif isNull record.Key then
        appendToHead registry.Document record.Element
        record.Shown <- true
    else
        let entries = stackOf registry record.Key

        if entries.Count > 0 then
            let previous = entries[entries.Count - 1]
            previous.Shown <- false

            if record.Kind <> HeadKind.Title then
                replaceWith previous.Element record.Element
        elif record.Kind = HeadKind.Title then
            registry.Original <- titleOf registry.Document
        elif not (isConnected record.Element) then
            let held = heldFor registry record.Key record.Element

            if isNull (box held) then
                appendToHead registry.Document record.Element
            else
                replaceWith held record.Element

        entries.Add record
        record.Shown <- true

        if
            record.Kind = HeadKind.Title
            && titleOf registry.Document <> record.Title
        then
            setTitleOf registry.Document record.Title

/// <summary>
/// Withdraws the record, once. When it was shown, the key's most recent remaining record is shown; the last title record
/// restores the document's title from before the first. The element of a meta or link record leaves the head.
/// </summary>
let withdraw (record: Record) =
    if not record.Withdrawn then
        record.Withdrawn <- true
        let registry = record.Registry
        let claim = indexOf registry.Claimed record.Element

        if claim >= 0 then
            registry.Claimed.RemoveAt claim

        if isNull record.Key then
            if record.Shown then
                record.Shown <- false
                detach record.Element
        else
            match registry.Live.TryGetValue record.Key with
            | true, entries ->
                let index = indexOf entries record

                if index >= 0 then
                    entries.RemoveAt index

                    if record.Shown then
                        record.Shown <- false

                        if entries.Count > 0 then
                            let next = entries[entries.Count - 1]
                            next.Shown <- true

                            if record.Kind = HeadKind.Title then
                                setTitleOf registry.Document next.Title
                            else
                                replaceWith record.Element next.Element
                        else
                            registry.Live.Remove record.Key |> ignore

                            if record.Kind = HeadKind.Title then
                                setTitleOf registry.Document registry.Original
                                registry.Original <- null
                            else
                                detach record.Element
            | _ -> ()

            if
                record.Kind <> HeadKind.Title
                && isConnected record.Element
            then
                detach record.Element

        registry.Records <- registry.Records - 1

        if registry.Records = 0 then
            registries.RemoveAt (indexOf registries registry)

/// <summary>Stores a title record's latest value and writes it to the document while the record is shown.</summary>
let setTitle (record: Record) (value: string) =
    record.Title <- value

    if record.Shown then
        setTitleOf record.Registry.Document value
