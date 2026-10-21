namespace Ranvier.View.Twin

open System
open System.Collections.Generic
open System.Reflection
open Microsoft.FSharp.Reflection

type internal Member = TwinNode -> obj array -> obj

/// <summary>A live list of twin nodes, which the bridge presents as the declared list interface.</summary>
type internal ListValue(items: unit -> TwinNode list) =
    member _.Items = items ()

/// <summary>An element's attributes, which the bridge presents as <c>NamedNodeMap</c>.</summary>
type internal AttributesValue(element: TwinElement) =
    member _.Element = element

/// <summary>One attribute of an element, which the bridge presents as <c>Attr</c>.</summary>
type internal AttributeValue(element: TwinElement, name: string) =
    member _.Element = element
    member _.Name = name

/// <summary>A proxy whose members are answered by a handler: lists, attribute maps and attributes.</summary>
type ValueProxy() =
    inherit DispatchProxy()
    member val internal Handler: Func<MethodInfo, obj array, obj> = null with get, set

    override this.Invoke(targetMethod: MethodInfo, args: obj array) : obj =
        this.Handler.Invoke (targetMethod, (if isNull args then [||] else args))

/// <summary>The bridged object of a twin node: a proxy of the DOM interface its kind and tag imply.</summary>
type NodeProxy() =
    inherit DispatchProxy()
    member val Node: TwinNode = null with get, set

    override this.Invoke(targetMethod: MethodInfo, args: obj array) : obj =
        BridgeCore.invoke this.Node targetMethod args

and internal BridgeCore private () =
    static let table = System.Collections.Concurrent.ConcurrentDictionary<string, Member>()
    static let interfaces = System.Collections.Concurrent.ConcurrentDictionary<string, Type>()
    static let someOf = System.Collections.Concurrent.ConcurrentDictionary<Type, MethodInfo>()

    // Every member served in the process, across threads: the record behind members.txt.
    static let served = System.Collections.Concurrent.ConcurrentDictionary<string, byte>()

    static member Table = table

    static member Domain = typeof<Fable.Core.TS.Dom.Node>.Assembly

    static member Interface(name: string) : Type =
        match interfaces.TryGetValue name with
        | true, t -> t
        | false, _ ->
            let t = BridgeCore.Domain.GetType ("Fable.Core.TS+Dom+" + name, true)
            interfaces[name] <- t
            t

    static member Serve(name: string) =
        served.TryAdd (name, 0uy) |> ignore

    static member Served: string seq = served.Keys

    static member ResetServed() =
        served.Clear ()

    /// <summary>The proxy of <c>node</c>, created on first use.</summary>
    static member ProxyOf(node: TwinNode) : obj =
        if isNull node then
            null
        else
            if isNull node.Proxy then
                let proxy =
                    DispatchProxy.Create (BridgeCore.InterfaceOf node, typeof<NodeProxy>) :?> NodeProxy

                proxy.Node <- node
                node.Proxy <- proxy

            node.Proxy

    static member InterfaceOf(node: TwinNode) : Type =
        match node with
        | :? TwinDocument -> BridgeCore.Interface "Document"
        | :? TwinFragment -> BridgeCore.Interface "DocumentFragment"
        | :? TwinCharacterData as c when c.Kind = NodeKind.Text -> BridgeCore.Interface "Text"
        | :? TwinCharacterData -> BridgeCore.Interface "Comment"
        | :? TwinElement as e ->
            let map = BridgeCore.Interface "HTMLElementTagNameMap"

            match map.GetProperty e.LocalName with
            | null -> BridgeCore.Interface "HTMLElement"
            | property -> property.PropertyType
        | _ -> BridgeCore.Interface "Node"

    static member Unwrap(value: obj) : obj =
        match value with
        | :? NodeProxy as proxy -> box proxy.Node
        | null -> null
        | _ ->
            let t = value.GetType ()

            if
                t.IsGenericType
                && t.GetGenericTypeDefinition () = typedefof<option<_>>
            then
                BridgeCore.Unwrap (t.GetProperty("Value").GetValue value)
            elif
                FSharpType.IsUnion (t, true)
                && t.Namespace = "Fable.Core"
            then
                BridgeCore.Unwrap ((FSharpValue.GetUnionFields (value, t, true) |> snd)[0])
            else
                value

    static member Unsupported(targetMethod: MethodInfo) : exn =
        let name = targetMethod.Name

        let display =
            if name.StartsWith "get_" || name.StartsWith "set_" then
                name.Substring 4
            else
                name

        NotSupportedException (
            "The DOM twin does not implement "
            + targetMethod.DeclaringType.Name
            + "."
            + display
        )

    static member ServeMethod(targetMethod: MethodInfo) =
        let name = targetMethod.Name

        let display =
            if name.StartsWith "get_" || name.StartsWith "set_" then
                name.Substring 4
            else
                name

        BridgeCore.Serve (targetMethod.DeclaringType.Name + "." + display)

    static member ValueProxyOf(interfaceType: Type, handler: MethodInfo -> obj array -> obj) : obj =
        let proxy = DispatchProxy.Create (interfaceType, typeof<ValueProxy>) :?> ValueProxy
        proxy.Handler <- Func<MethodInfo, obj array, obj> handler
        box proxy

    static member ListProxy(interfaceType: Type, list: ListValue) : obj =
        BridgeCore.ValueProxyOf (
            interfaceType,
            fun m args ->
                let items = list.Items

                match m.Name with
                | "get_length" ->
                    BridgeCore.ServeMethod m
                    box (float items.Length)
                | "item"
                | "get_Item" ->
                    BridgeCore.ServeMethod m
                    let index = int (Convert.ToDouble args[0])
                    BridgeCore.Wrap (m.ReturnType, (List.tryItem index items |> Option.toObj |> box))
                | _ -> raise (BridgeCore.Unsupported m)
        )

    static member AttributesProxy(interfaceType: Type, attributes: AttributesValue) : obj =
        BridgeCore.ValueProxyOf (
            interfaceType,
            fun m args ->
                let element = attributes.Element

                match m.Name with
                | "get_length" ->
                    BridgeCore.ServeMethod m
                    box (float element.Attributes.Length)
                | "item"
                | "get_Item" ->
                    BridgeCore.ServeMethod m

                    match List.tryItem (int (Convert.ToDouble args[0])) element.Attributes with
                    | Some (name, _) -> BridgeCore.Wrap (m.ReturnType, AttributeValue (element, name))
                    | None -> null
                | _ -> raise (BridgeCore.Unsupported m)
        )

    static member AttributeProxy(interfaceType: Type, attribute: AttributeValue) : obj =
        BridgeCore.ValueProxyOf (
            interfaceType,
            fun m _ ->
                match m.Name with
                | "get_name"
                | "get_localName" ->
                    BridgeCore.ServeMethod m
                    box attribute.Name
                | "get_value" ->
                    BridgeCore.ServeMethod m

                    box (
                        match attribute.Element.GetAttribute attribute.Name with
                        | null -> ""
                        | v -> v
                    )
                | _ -> raise (BridgeCore.Unsupported m)
        )

    static member Wrap(returnType: Type, value: obj) : obj =
        let target =
            if
                returnType.IsGenericType
                && returnType.GetGenericTypeDefinition () = typedefof<option<_>>
            then
                returnType.GetGenericArguments()[0]
            else
                returnType

        let value =
            match value with
            | :? TwinNode as node -> BridgeCore.ProxyOf node
            | :? ListValue as list -> BridgeCore.ListProxy (target, list)
            | :? AttributesValue as attributes -> BridgeCore.AttributesProxy (target, attributes)
            | :? AttributeValue as attribute -> BridgeCore.AttributeProxy (target, attribute)
            | _ -> value

        if returnType = typeof<Void> then
            null
        elif
            returnType.IsGenericType
            && returnType.GetGenericTypeDefinition () = typedefof<option<_>>
        then
            if isNull value then
                null
            else
                let some =
                    match someOf.TryGetValue returnType with
                    | true, m -> m
                    | false, _ ->
                        let m = returnType.GetMethod "Some"
                        someOf[returnType] <- m
                        m

                some.Invoke (null, [| value |])
        elif returnType = typeof<float> then
            match value with
            | :? int as i -> box (float i)
            | _ -> value
        else
            value

    static member invoke (node: TwinNode) (targetMethod: MethodInfo) (args: obj array) : obj =
        node.Document.Check targetMethod.Name
        let name = targetMethod.Name
        let arity = if isNull args then 0 else args.Length
        let key = $"{name}/{arity}"

        let display =
            if name.StartsWith "get_" || name.StartsWith "set_" then
                name.Substring 4
            else
                name

        let qualified = targetMethod.DeclaringType.Name + "." + display

        match table.TryGetValue key with
        | true, implementation ->
            BridgeCore.Serve qualified

            let args =
                if isNull args then
                    [||]
                else
                    Array.map BridgeCore.Unwrap args

            BridgeCore.Wrap (targetMethod.ReturnType, implementation node args)
        | false, _ -> raise (NotSupportedException ("The DOM twin does not implement " + qualified))

[<RequireQualifiedAccess>]
module Bridge =
    let nodeOf (value: obj) : TwinNode =
        match value with
        | :? NodeProxy as proxy -> proxy.Node
        | _ -> null

    let registerMember (name: string) (arity: int) (implementation: TwinNode -> obj array -> obj) =
        BridgeCore.Table[$"{name}/{arity}"] <- implementation

    let served () : string list =
        BridgeCore.Served |> Seq.sort |> List.ofSeq

    let resetServed () =
        BridgeCore.ResetServed ()

    let private element (node: TwinNode) =
        node :?> TwinElement

    let private text (value: obj) =
        if isNull value then "" else string value

    let private flag (value: obj) =
        match value with
        | :? bool as b -> b
        | _ -> false

    let private getter name (read: TwinNode -> obj) =
        registerMember ("get_" + name) 0 (fun node _ -> read node)

    let private setter name (write: TwinNode -> obj -> unit) =
        registerMember ("set_" + name) 1 (fun node args ->
            write node args[0]
            null)

    let private method name arity (run: TwinNode -> obj array -> obj) =
        registerMember name arity run

    let private nodeType (node: TwinNode) =
        match node.Kind with
        | NodeKind.Element -> 1
        | NodeKind.Text -> 3
        | NodeKind.Comment -> 8
        | NodeKind.Document -> 9
        | NodeKind.Fragment -> 11

    let private nodeName (node: TwinNode) =
        match node with
        | :? TwinElement as e -> e.LocalName.ToUpperInvariant ()
        | _ ->
            match node.Kind with
            | NodeKind.Text -> "#text"
            | NodeKind.Comment -> "#comment"
            | NodeKind.Document -> "#document"
            | _ -> "#document-fragment"

    let private childList (nodes: unit -> TwinNode list) : obj =
        box (ListValue nodes)

    let private core =
        lazy
            (
             // Node
             getter "nodeType" (nodeType >> box)
             getter "nodeName" (nodeName >> box)
             getter "parentNode" (fun n -> box n.Parent)

             getter "parentElement" (fun n ->
                 match n.Parent with
                 | :? TwinElement as e -> box e
                 | _ -> null)

             getter "firstChild" (fun n -> box n.FirstChild)
             getter "lastChild" (fun n -> box n.LastChild)
             getter "nextSibling" (fun n -> box n.NextSibling)
             getter "previousSibling" (fun n -> box n.PreviousSibling)
             getter "ownerDocument" (fun n -> if n.Kind = NodeKind.Document then null else box n.Document)
             getter "isConnected" (fun n -> box n.IsConnected)
             getter "childNodes" (fun n -> childList (fun () -> n.Children))
             getter "textContent" (fun n -> box n.TextContent)
             setter "textContent" (fun n v -> n.TextContent <- text v)
             method "appendChild" 1 (fun n a -> box (n.AppendChild (a[0] :?> TwinNode)))
             method "insertBefore" 2 (fun n a -> box (n.InsertBefore (a[0] :?> TwinNode, a[1] :?> TwinNode)))
             method "removeChild" 1 (fun n a -> box (n.RemoveChild (a[0] :?> TwinNode)))
             method "replaceChild" 2 (fun n a -> box (n.ReplaceChild (a[0] :?> TwinNode, a[1] :?> TwinNode)))

             method "contains" 1 (fun n a ->
                 box (
                     not (isNull a[0])
                     && n.Contains (a[0] :?> TwinNode)
                 ))

             method "cloneNode" 1 (fun n a -> box (n.Clone (flag a[0])))
             // CharacterData
             getter "data" (fun n -> box (n :?> TwinCharacterData).Data)
             setter "data" (fun n v -> (n :?> TwinCharacterData).Data <- text v)
             // ChildNode
             method "remove" 0 (fun n _ ->
                 n.Remove ()
                 null)

             method "replaceWith" 1 (fun n a ->
                 let parent = n.Parent

                 for replacement in
                     (a[0] :?> obj array)
                     |> Array.map BridgeCore.Unwrap do
                     parent.InsertBefore (
                         (match replacement with
                          | :? TwinNode as r -> r
                          | v -> n.Document.CreateText (string v) :> TwinNode),
                         n
                     )
                     |> ignore

                 n.Remove ()
                 null)
             // ParentNode
             getter "children" (fun n ->
                 childList (fun () ->
                     n.Children
                     |> List.filter (fun c -> c.Kind = NodeKind.Element)))

             method "replaceChildren" 1 (fun n a ->
                 for child in n.Children do
                     n.RemoveChild child |> ignore

                 for child in
                     (a[0] :?> obj array)
                     |> Array.map BridgeCore.Unwrap do
                     n.AppendChild (
                         match child with
                         | :? TwinNode as c -> c
                         | v -> n.Document.CreateText (string v) :> TwinNode
                     )
                     |> ignore

                 null)
             // Element
             getter "localName" (fun n -> box (element n).LocalName)
             getter "tagName" (fun n -> box ((element n).LocalName.ToUpperInvariant()))
             getter "prefix" (fun _ -> null)
             getter "id" (fun n -> box (text ((element n).GetAttribute "id")))
             setter "id" (fun n v -> (element n).SetAttribute("id", text v))
             getter "className" (fun n -> box (text ((element n).GetAttribute "class")))
             setter "className" (fun n v -> (element n).SetAttribute("class", text v))
             method "getAttribute" 1 (fun n a -> box ((element n).GetAttribute(string a[0])))

             method "setAttribute" 2 (fun n a ->
                 (element n).SetAttribute(string a[0], text a[1])
                 null)

             method "removeAttribute" 1 (fun n a ->
                 (element n).RemoveAttribute(string a[0])
                 null)

             method "hasAttribute" 1 (fun n a -> box ((element n).HasAttribute(string a[0])))
             getter "attributes" (fun n -> box (AttributesValue (element n)))
             getter "innerHTML" (fun n -> box (Html.inner n))

             setter "innerHTML" (fun n v ->
                 let target =
                     match n with
                     | :? TwinElement as e when e.LocalName = "template" -> e.Content
                     | _ -> n

                 for child in target.Children do
                     target.RemoveChild child |> ignore

                 Html.parseInto n (text v))

             getter "outerHTML" (fun n -> box (Html.outer n))
             // HTMLTemplateElement
             getter "content" (fun n -> box (element n).Content)
             // Form controls
             getter "value" (fun n -> box (element n).Value)
             setter "value" (fun n v -> (element n).Value <- text v)
             getter "defaultValue" (fun n -> box (element n).DefaultValue)
             setter "defaultValue" (fun n v -> (element n).DefaultValue <- text v)
             getter "checked" (fun n -> box (element n).Checked)
             setter "checked" (fun n v -> (element n).Checked <- flag v)
             getter "defaultChecked" (fun n -> box ((element n).HasAttribute "checked"))
             getter "selected" (fun n -> box (element n).Selected)
             setter "selected" (fun n v -> (element n).Selected <- flag v)

             getter "type" (fun n ->
                 box (
                     match (element n).GetAttribute "type" with
                     | null ->
                         (if (element n).LocalName = "button" then
                              "submit"
                          else
                              "text")
                     | t -> t.ToLowerInvariant ()
                 ))

             getter "disabled" (fun n -> box ((element n).HasAttribute "disabled"))

             setter "disabled" (fun n v ->
                 if flag v then
                     (element n).SetAttribute("disabled", "")
                 else
                     (element n).RemoveAttribute "disabled")
             // Document
             getter "head" (fun n -> box (n :?> TwinDocument).Head)
             getter "body" (fun n -> box (n :?> TwinDocument).Body)
             getter "documentElement" (fun n -> box (n :?> TwinDocument).Html)

             getter "title" (fun n ->
                 match n with
                 | :? TwinDocument as d ->
                     d.Head.Children
                     |> List.tryPick (fun c ->
                         match c with
                         | :? TwinElement as e when e.LocalName = "title" -> Some (box e.TextContent)
                         | _ -> None)
                     |> Option.defaultValue (box "")
                 | _ -> box (text ((element n).GetAttribute "title")))

             setter "title" (fun n v ->
                 match n with
                 | :? TwinDocument as d ->
                     let existing =
                         d.Head.Children
                         |> List.tryPick (fun c ->
                             match c with
                             | :? TwinElement as e when e.LocalName = "title" -> Some e
                             | _ -> None)

                     let title =
                         match existing with
                         | Some t -> t
                         | None -> d.Head.AppendChild (d.CreateElement "title") :?> TwinElement

                     title.TextContent <- text v
                 | _ -> (element n).SetAttribute("title", text v))

             method "createElement" 2 (fun n a -> box ((n :?> TwinDocument).CreateElement(string a[0])))
             method "createTextNode" 1 (fun n a -> box ((n :?> TwinDocument).CreateText(text a[0])))
             method "createComment" 1 (fun n a -> box ((n :?> TwinDocument).CreateComment(text a[0])))
             method "createDocumentFragment" 0 (fun n _ -> box ((n :?> TwinDocument).CreateFragment()))
             method "importNode" 2 (fun _ a -> box ((a[0] :?> TwinNode).Clone(flag a[1])))
             method "getElementById" 1 (fun n a -> box ((n :?> TwinDocument).ElementById(string a[0])))
             getter "activeElement" (fun n -> box (n :?> TwinDocument).ActiveElement))

    /// <summary>Registers the core members once per process; every entry point that creates a proxy calls it first.</summary>
    let ensure () =
        core.Force ()

[<AutoOpen>]
module NodeNative =
    type TwinNode with
        /// <summary>The node as <c>Fable.Core.TS.Dom</c> sees it: the same object on every read.</summary>
        member this.Native: Fable.Core.TS.Dom.Node =
            Bridge.ensure ()
            BridgeCore.ProxyOf this :?> Fable.Core.TS.Dom.Node
