namespace Ranvier.View.Twin

open System
open System.Collections.Generic

[<RequireQualifiedAccess>]
type NodeKind =
    | Element
    | Text
    | Comment
    | Fragment
    | Document

/// <summary>A tree operation the DOM rejects; <c>name</c> is the DOM exception name.</summary>
exception TwinDomException of name: string * detail: string with
    override this.Message = this.name + ": " + this.detail

/// <summary>A node of a twin document. Each node has one bridged object, the value Foundation sees.</summary>
[<AbstractClass; AllowNullLiteral>]
type TwinNode internal (kind: NodeKind, owner: TwinDocument) =
    let mutable parent: TwinNode = null
    let mutable firstChild: TwinNode = null
    let mutable lastChild: TwinNode = null
    let mutable nextSibling: TwinNode = null
    let mutable previousSibling: TwinNode = null

    member _.Kind = kind
    member _.Parent = parent
    member _.FirstChild = firstChild
    member _.LastChild = lastChild
    member _.NextSibling = nextSibling
    member _.PreviousSibling = previousSibling

    member this.Document: TwinDocument =
        if isNull owner then this :?> TwinDocument else owner

    /// <summary>The bridged object; set once by the bridge.</summary>
    member val internal Proxy: obj = null with get, set

    member _.Children: TwinNode list =
        let rec collect (node: TwinNode) acc =
            if isNull node then
                List.rev acc
            else
                collect node.NextSibling (node :: acc)

        collect firstChild []

    member internal _.SetLinks(p, n, pr) =
        parent <- p
        nextSibling <- n
        previousSibling <- pr

    member internal _.SetNext n =
        nextSibling <- n

    member internal _.SetPrevious p =
        previousSibling <- p

    member internal _.SetFirst f =
        firstChild <- f

    member internal _.SetLast l =
        lastChild <- l

    member this.Contains(other: TwinNode) =
        let mutable cursor = other
        let mutable found = false

        while not found && not (isNull cursor) do
            found <- obj.ReferenceEquals (cursor, this)
            cursor <- cursor.Parent

        found

    member this.IsConnected =
        let mutable cursor: TwinNode = this

        while not (isNull cursor.Parent) do
            cursor <- cursor.Parent

        cursor.Kind = NodeKind.Document

    member private this.Unlink(child: TwinNode) =
        let previous, next = child.PreviousSibling, child.NextSibling

        if isNull previous then
            this.SetFirst next
        else
            previous.SetNext next

        if isNull next then
            this.SetLast previous
        else
            next.SetPrevious previous

        child.SetLinks (null, null, null)

    member this.RemoveChild(child: TwinNode) : TwinNode =
        if
            isNull child
            || not (obj.ReferenceEquals (child.Parent, this))
        then
            raise (TwinDomException ("NotFoundError", "The node to remove is not a child of this node."))

        this.Unlink child
        child

    member this.Remove() =
        if not (isNull parent) then
            parent.RemoveChild this |> ignore

    member this.InsertBefore(node: TwinNode, reference: TwinNode) : TwinNode =
        if
            not (isNull reference)
            && not (obj.ReferenceEquals (reference.Parent, this))
        then
            raise (TwinDomException ("NotFoundError", "The reference node is not a child of this node."))

        if node.Contains this then
            raise (TwinDomException ("HierarchyRequestError", "The new child is an ancestor of the parent."))

        if node.Kind = NodeKind.Document then
            raise (TwinDomException ("HierarchyRequestError", "A document cannot be inserted."))

        if node.Kind = NodeKind.Fragment then
            for child in node.Children do
                this.InsertBefore (child, reference) |> ignore

            node
        else
            let reference =
                if obj.ReferenceEquals (reference, node) then
                    node.NextSibling
                else
                    reference

            node.Remove ()

            let previous =
                if isNull reference then
                    lastChild
                else
                    reference.PreviousSibling

            node.SetLinks (this, reference, previous)

            if isNull previous then
                firstChild <- node
            else
                previous.SetNext node

            if isNull reference then
                lastChild <- node
            else
                reference.SetPrevious node

            node

    member this.AppendChild(node: TwinNode) =
        this.InsertBefore (node, null)

    member this.ReplaceChild(node: TwinNode, child: TwinNode) : TwinNode =
        if
            isNull child
            || not (obj.ReferenceEquals (child.Parent, this))
        then
            raise (TwinDomException ("NotFoundError", "The node to replace is not a child of this node."))

        let reference =
            if obj.ReferenceEquals (child.NextSibling, node) then
                node.NextSibling
            else
                child.NextSibling

        this.RemoveChild child |> ignore
        this.InsertBefore (node, reference) |> ignore
        child

    abstract TextContent: string with get, set

    default this.TextContent
        with get () =
            let text = Text.StringBuilder ()

            let rec walk (node: TwinNode) =
                for child in node.Children do
                    match child with
                    | :? TwinCharacterData as data when data.Kind = NodeKind.Text -> text.Append (data.Data: string) |> ignore
                    | :? TwinCharacterData -> ()
                    | _ -> walk child

            walk this
            text.ToString ()
        and set value =
            for child in this.Children do
                this.RemoveChild child |> ignore

            if not (String.IsNullOrEmpty value) then
                this.AppendChild (this.Document.CreateText value)
                |> ignore

    abstract Clone: deep: bool -> TwinNode

and [<Sealed; AllowNullLiteral>] TwinCharacterData internal (kind: NodeKind, owner: TwinDocument, data: string) =
    inherit TwinNode(kind, owner)
    let mutable data = data

    member _.Data
        with get () = data
        and set value = data <- (if isNull value then "" else value)

    override _.TextContent
        with get () = data
        and set value = data <- (if isNull value then "" else value)

    override this.Clone _ =
        TwinCharacterData (kind, this.Document, data)

and [<Sealed; AllowNullLiteral>] TwinFragment internal (owner: TwinDocument) =
    inherit TwinNode(NodeKind.Fragment, owner)

    override this.Clone deep =
        let copy = TwinFragment this.Document

        if deep then
            for child in this.Children do
                copy.AppendChild (child.Clone true) |> ignore

        copy

and [<Sealed; AllowNullLiteral>] TwinElement internal (owner: TwinDocument, localName: string) =
    inherit TwinNode(NodeKind.Element, owner)
    let attributes = ResizeArray<string * string>()
    let content = if localName = "template" then TwinFragment owner else null
    let mutable dirtyValue: string = null
    let mutable checkedness = false
    let mutable dirtyChecked = false
    let mutable selectedness = false
    let mutable dirtySelected = false

    member _.LocalName = localName
    member _.Attributes: (string * string) list = List.ofSeq attributes
    member _.Content: TwinNode = content

    member _.GetAttribute(name: string) : string =
        let name = name.ToLowerInvariant ()

        match attributes |> Seq.tryFind (fun (n, _) -> n = name) with
        | Some (_, value) -> value
        | None -> null

    member this.HasAttribute(name: string) =
        not (isNull (this.GetAttribute name))

    member _.SetAttribute(name: string, value: string) =
        let name = name.ToLowerInvariant ()

        match attributes.FindIndex (fun (n, _) -> n = name) with
        | -1 -> attributes.Add ((name, value))
        | index -> attributes[index] <- (name, value)

    member _.RemoveAttribute(name: string) =
        let name = name.ToLowerInvariant ()

        attributes.RemoveAll (fun (n, _) -> n = name)
        |> ignore

    member private this.InputType =
        match this.GetAttribute "type" with
        | null -> "text"
        | t -> t.ToLowerInvariant ()

    member private this.Options: TwinElement list =
        let rec walk (node: TwinNode) =
            node.Children
            |> List.collect (fun c ->
                match c with
                | :? TwinElement as e when e.LocalName = "option" -> [ e ]
                | :? TwinElement as e when e.LocalName = "optgroup" -> walk e
                | _ -> [])

        walk this

    /// <summary>The form value, by HTML's value modes: the attribute until the property is written, then the written value.</summary>
    member this.Value
        with get () =
            match localName with
            | "input" when
                this.InputType = "checkbox"
                || this.InputType = "radio"
                ->
                match this.GetAttribute "value" with
                | null -> "on"
                | value -> value
            | "input" ->
                if isNull dirtyValue then
                    (match this.GetAttribute "value" with
                     | null -> ""
                     | v -> v)
                else
                    dirtyValue
            | "textarea" -> if isNull dirtyValue then this.TextContent else dirtyValue
            | "option" ->
                match this.GetAttribute "value" with
                | null -> String.Join (" ", this.TextContent.Split ([| ' '; '\t'; '\n'; '\f'; '\r' |], StringSplitOptions.RemoveEmptyEntries))
                | value -> value
            | "select" ->
                match this.Options |> List.tryFind (fun o -> o.Selected) with
                | Some option -> option.Value
                | None -> ""
            | "button" ->
                match this.GetAttribute "value" with
                | null -> ""
                | v -> v
            | _ -> null
        and set (value: string) =
            match localName with
            | "input" when
                this.InputType = "checkbox"
                || this.InputType = "radio"
                ->
                this.SetAttribute ("value", value)
            | "input"
            | "textarea" -> dirtyValue <- (if isNull value then "" else value)
            | "select" ->
                let mutable found = false

                for option in this.Options do
                    let selected = not found && option.Value = value
                    found <- found || selected
                    option.Selected <- selected
            | "option"
            | "button" -> this.SetAttribute ("value", value)
            | _ -> ()

    member this.DefaultValue
        with get () =
            match localName with
            | "textarea" -> this.TextContent
            | _ ->
                match this.GetAttribute "value" with
                | null -> ""
                | v -> v
        and set (value: string) =
            match localName with
            | "textarea" -> this.TextContent <- value
            | _ -> this.SetAttribute ("value", value)

    member this.Checked
        with get () =
            if dirtyChecked then
                checkedness
            else
                this.HasAttribute "checked"
        and set (value: bool) =
            dirtyChecked <- true
            checkedness <- value

            if
                value
                && localName = "input"
                && this.InputType = "radio"
            then
                let name = this.GetAttribute "name"

                if not (String.IsNullOrEmpty name) then
                    let mutable root: TwinNode = this

                    while not (isNull root.Parent) do
                        root <- root.Parent

                    let rec walk (node: TwinNode) =
                        for child in node.Children do
                            match child with
                            | :? TwinElement as e when
                                not (obj.ReferenceEquals (e, this))
                                && e.LocalName = "input"
                                && e.InputType = "radio"
                                && e.GetAttribute "name" = name
                                ->
                                e.Checked <- false
                            | _ -> ()

                            walk child

                    walk root

    /// <summary>An option's selectedness; a select with no selected option selects its first, as a single-choice select does.</summary>
    member this.Selected
        with get () =
            if localName <> "option" then
                false
            elif dirtySelected then
                selectedness
            elif this.HasAttribute "selected" then
                true
            else
                match this.Parent with
                | :? TwinElement as select when
                    select.LocalName = "select"
                    && not (select.HasAttribute "multiple")
                    ->
                    let options = select.Options

                    not (
                        options
                        |> List.exists (fun o -> o.HasAttribute "selected" || o.SelectedIsDirty)
                    )
                    && obj.ReferenceEquals (List.tryHead options |> Option.toObj, this)
                | _ -> false
        and set (value: bool) =
            dirtySelected <- true
            selectedness <- value

    member internal _.SelectedIsDirty = dirtySelected && selectedness

    override this.Clone deep =
        let copy = TwinElement (this.Document, localName)

        for name, value in attributes do
            copy.SetAttribute (name, value)

        if localName = "input" then
            if not (isNull dirtyValue) then
                copy.Value <- dirtyValue

            if dirtyChecked then
                copy.Checked <- checkedness

        if deep then
            for child in this.Children do
                copy.AppendChild (child.Clone true) |> ignore

            if not (isNull content) then
                for child in content.Children do
                    copy.Content.AppendChild (child.Clone true)
                    |> ignore

        copy

and [<Sealed; AllowNullLiteral>] TwinDocument() as this =
    inherit TwinNode(NodeKind.Document, null)
    let ownerThread = Environment.CurrentManagedThreadId
    let disposals = ResizeArray<unit -> unit>()
    let html = TwinElement (this, "html")
    let head = TwinElement (this, "head")
    let body = TwinElement (this, "body")

    do
        this.AppendChild html |> ignore
        html.AppendChild head |> ignore
        html.AppendChild body |> ignore

    member _.Html = html
    member _.Head = head
    member _.Body = body
    member _.OwnerThread = ownerThread
    member val ActiveElement: TwinElement = null with get, set

    /// <summary>The exceptions raised by listeners during dispatches, in order; a dispatch continues past each.</summary>
    member val ListenerErrors = ResizeArray<exn>()

    /// <summary>Whether a dispatch raises its first listener exception once the dispatch ends.</summary>
    member val RaiseListenerErrors = false with get, set

    member this.CreateElement(tag: string) =
        TwinElement (this, tag.ToLowerInvariant ())

    member this.CreateText(data: string) =
        TwinCharacterData (NodeKind.Text, this, data)

    member this.CreateComment(data: string) =
        TwinCharacterData (NodeKind.Comment, this, data)

    member this.CreateFragment() =
        TwinFragment this

    member this.ElementById(id: string) : TwinElement =
        let rec find (node: TwinNode) =
            node.Children
            |> List.tryPick (fun c ->
                match c with
                | :? TwinElement as e when e.GetAttribute "id" = id -> Some e
                | _ -> find c)

        find this |> Option.toObj

    /// <summary>Raises <c>InvalidOperationException</c> naming <c>operation</c> when called off the creating thread.</summary>
    member _.Check(operation: string) =
        let current = Environment.CurrentManagedThreadId

        if current <> ownerThread then
            raise (InvalidOperationException ($"The DOM twin document belongs to thread {ownerThread}; {operation} ran on thread {current}."))

    member _.OnDispose(action: unit -> unit) =
        disposals.Add action

    override this.Clone _ =
        raise (TwinDomException ("NotSupportedError", "A twin document cannot be cloned."))

    interface IDisposable with
        member _.Dispose() =
            for action in Seq.rev (List.ofSeq disposals) do
                action ()

            disposals.Clear ()
