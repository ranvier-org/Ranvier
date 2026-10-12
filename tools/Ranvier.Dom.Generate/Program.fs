module Ranvier.Dom.Generate

open System
open System.IO
open System.IO.Compression
open System.Reflection
open System.Security.Cryptography
open System.Text
open System.Text.Json
open Fable.Core.TS
open Xantham.TypeScript.Wire
open Xantham.TypeScript.Wire.Proto

let fail message =
    invalidOp message

let hashBytes (bytes: byte array) =
    SHA256.HashData (bytes)
    |> Convert.ToHexStringLower

let hashFile path =
    File.ReadAllBytes path |> hashBytes

let str (name: string) (value: JsonElement) =
    value.GetProperty(name).GetString()

let require condition message =
    if not condition then
        fail message

let compilerVersion = "7.1.0-dev.20260902.1"
let compilerRevision = "43a90f4c105bc9db7cb7aa299beddafbabe1d23e"

let catalogueHash =
    "ad2722747e79381c6ec729db17d96c9eb1046d68627099cb63986942563687c8"

let assemblyHash =
    "663021de210d5f4a4ae4b9edbbd71d850343edf1243a16bb322634051ffe0450"

let descriptorHash =
    "40d670b86bdb994b6108ae271cee1441215f04893b6d820e5d39075895f73942"

type Declaration =
    {
        Name: string
        Arity: int
        Handles: string array
    }

let loadCatalogue root =
    let descriptorPath = Path.Combine (root, "xantham", "catalogue.json")
    require (hashFile descriptorPath = descriptorHash) "Descriptor hash mismatch"
    use descriptor = JsonDocument.Parse (File.ReadAllBytes descriptorPath)
    let d = descriptor.RootElement
    require (d.GetProperty("schemaVersion").GetInt32() = 1) "Unsupported descriptor schema"

    require
        (str "packageId" d = "Xantham.Fable.Core.TS"
         && str "packageVersion" d = "0.2.1")
        "Unexpected binding package"

    require (str "catalogue" d = "xantham/declarations.json.br") "Unexpected catalogue path"

    require
        (str "catalogueSha256" d = catalogueHash
         && str "assemblySha256" d = assemblyHash)
        "Descriptor disagrees with release pins"

    let payload = Path.Combine (root, "xantham", "declarations.json.br")
    require (hashFile payload = catalogueHash) "Catalogue hash mismatch"
    require (hashFile typeof<Dom.HTMLInputElement>.Assembly.Location = assemblyHash) "Native binding assembly hash mismatch"
    use input = File.OpenRead payload
    use decoded = new BrotliStream (input, CompressionMode.Decompress)
    use bounded = new MemoryStream ()
    let buffer = Array.zeroCreate<byte> 65536
    let mutable count = decoded.Read (buffer)

    while count > 0 do
        require
            (bounded.Length + int64 count
             <= 128L * 1024L * 1024L)
            "Catalogue exceeds decoded size limit"

        bounded.Write (buffer, 0, count)
        count <- decoded.Read (buffer)

    use document = JsonDocument.Parse (bounded.ToArray ())
    let catalog = document.RootElement
    require (catalog.GetProperty("schemaVersion").GetInt32() = 2) "Unsupported declaration schema"
    let compatibility = catalog.GetProperty ("compatibility")

    for name, expected in
        [
            "contractVersion", 1
            "identityVersion", 5
            "apiVersion", 4
            "inferenceVersion", 2
            "customizationVersion", 1
        ] do
        require (compatibility.GetProperty(name).GetInt32() = expected) $"Unsupported compatibility field {name}"

    let compiler = compatibility.GetProperty ("compiler")

    require
        (str "kind" compiler = "typescript-package"
         && str "version" compiler = compilerVersion
         && str "revision" compiler = compilerRevision
         && compiler.GetProperty("astProtocolVersion").GetInt32() = 8)
        "Unsupported compiler contract"

    require (str "owner" d = str "owner" catalog) "Descriptor owner mismatch"

    let declarations =
        catalog.GetProperty("declarations").EnumerateArray()
        |> Seq.filter (fun declaration -> str "role" declaration = "type")
        |> Seq.map (fun declaration ->
            {
                Name = str "fSharpName" declaration
                Arity = declaration.GetProperty("arity").GetInt32()
                Handles =
                    declaration.GetProperty("handles").EnumerateArray()
                    |> Seq.map _.GetString()
                    |> Seq.toArray
            })
        |> Seq.toArray

    let sources =
        catalog.GetProperty("inputs").EnumerateArray()
        |> Seq.filter (fun source -> str "package" source = "typescript")
        |> Seq.map (fun source -> str "file" source, str "sha256" source)
        |> Seq.toArray

    declarations, sources

let nativeAssembly = typeof<Dom.HTMLInputElement>.Assembly

let nativeType (declaration: Declaration) =
    require (declaration.Name.StartsWith ("Fable.Core.TS.Dom.", StringComparison.Ordinal)) $"Unsupported native module: {declaration.Name}"

    let suffix =
        if declaration.Arity = 0 then
            ""
        else
            "`" + string declaration.Arity

    nativeAssembly.GetType (
        declaration.Name.Replace ("Fable.Core.TS.Dom.", "Fable.Core.TS+Dom+")
        + suffix,
        true
    )

let allProperties (native: Type) =
    Array.append [| native |] (native.GetInterfaces ())
    |> Array.collect _.GetProperties()
    |> Array.distinctBy _.Name

let property native name =
    allProperties native
    |> Array.tryFind (fun p -> p.Name = name)
    |> Option.defaultWith (fun () -> fail $"Native property missing: {native.Name}.{name}")

let typeName (declarations: Declaration array) (native: Type) =
    let rec render (native: Type) =
        if native = typeof<string> then
            "string"
        elif native = typeof<bool> then
            "bool"
        elif native = typeof<float> then
            "float"
        elif
            native.IsGenericType
            && native.GetGenericTypeDefinition () = typedefof<option<_>>
        then
            $"{render (native.GetGenericArguments()[0])} option"
        else
            let definition =
                if native.IsGenericType then
                    native.GetGenericTypeDefinition ()
                else
                    native

            let name =
                (definition.FullName.Split('`')[0]).Replace("Fable.Core.TS+Dom+", "Fable.Core.TS.Dom.")

            let declaration =
                declarations
                |> Array.tryFind (fun d -> d.Name = name)
                |> Option.defaultWith (fun () -> fail $"Unsupported native type shape: {native}")

            require (nativeType declaration = definition) $"Native representation mismatch: {name}"
            let basic = "Dom." + definition.Name.Split('`')[0]

            if native.IsGenericType then
                basic
                + "<"
                + (native.GetGenericArguments ()
                   |> Array.map render
                   |> String.concat ", ")
                + ">"
            else
                basic

    render native

let generate compilerRoot packageRoot output =
    let declarations, sources = loadCatalogue packageRoot

    use package =
        JsonDocument.Parse (File.ReadAllBytes (Path.Combine (compilerRoot, "node_modules", "typescript", "package.json")))

    require
        (str "version" package.RootElement = compilerVersion
         && str "gitHead" package.RootElement = compilerRevision)
        "Installed compiler version/revision mismatch"

    let exe =
        Tsc.locate compilerRoot
        |> Option.defaultWith (fun () -> fail "Pinned TypeScript native compiler not found; run npm ci in tools/Ranvier.Dom.Generate")

    let libRoot = Path.GetDirectoryName exe

    for relative, expected in sources do
        let path = Path.Combine (libRoot, Path.GetFileName (relative: string))
        require (hashFile path = expected) $"TypeScript declaration input hash mismatch: {relative}"

    use channel = new TscChannel (exe, compilerRoot)
    Api.initialize channel |> ignore

    let program =
        Api.createProgram
            channel
            { CreateProgramParams.Default with
                RootFiles =
                    ValueSome
                        [|
                            DocumentIdentifier.FileName (Path.Combine (libRoot, "lib.dom.d.ts"))
                            DocumentIdentifier.FileName (Path.Combine (libRoot, "lib.esnext.d.ts"))
                        |]
            }

    let project = program.Project.Value.Id
    let snapshot = program.Snapshot

    let resolve name =
        Api.resolveName
            channel
            {
                Snapshot = snapshot
                Project = project
                Name = name
                Location = ValueNone
                File = ValueNone
                Position = ValueNone
                Meaning = SymbolFlags.Type
                ExcludeGlobals = ValueNone
            }
        |> ValueOption.defaultWith (fun () -> fail $"Compiler symbol missing: {name}")

    let catalogueFor (symbol: SymbolResponse) =
        let handles =
            symbol.Declarations
            |> ValueOption.defaultValue [||]
            |> Array.map (fun handle ->
                let parts = handle.Split ('.', 3)
                require (parts.Length = 3) $"Unsupported compiler declaration handle: {handle}"
                let file = Path.GetFileName parts[2]
                $"typescript@{compilerVersion}/lib/{file}#{parts[0]}.{parts[1]}")

        let candidates =
            declarations
            |> Array.filter (fun declaration ->
                declaration.Handles
                |> Array.exists (fun handle -> Array.contains handle handles))

        require (candidates.Length = 1) $"Missing or ambiguous catalogue identity: {symbol.Name}"
        candidates[0]

    let properties (symbol: SymbolResponse) =
        let native =
            Api.getDeclaredTypeOfSymbol
                channel
                {
                    Snapshot = snapshot
                    Project = project
                    Symbol = symbol.Id
                }

        Api.getPropertiesOfType
            channel
            {
                Snapshot = snapshot
                Project = project
                Type = native.Id
            }
        |> ValueOption.defaultValue [||]

    let memberSymbol symbols name =
        symbols
        |> Array.tryFind (fun (symbol: SymbolResponse) -> symbol.Name = name)
        |> Option.defaultWith (fun () -> fail $"Compiler property missing: {name}")

    let readonly (symbol: SymbolResponse) =
        Api.isReadonlySymbol
            channel
            {
                Snapshot = snapshot
                Project = project
                Symbol = symbol.Id
            }

    let tagMap = resolve "HTMLElementTagNameMap"
    catalogueFor tagMap |> nativeType |> ignore
    let tags = properties tagMap
    let eventMap = resolve "HTMLElementEventMap"
    let eventMapNative = catalogueFor eventMap |> nativeType
    let eventSymbol = memberSymbol (properties eventMap) "input"

    let eventType =
        Api.getTypeOfSymbol
            channel
            {
                Snapshot = snapshot
                Project = project
                Symbol = eventSymbol.Id
            }

    let symbolOfType (native: TypeResponse) =
        Api.getSymbolOfType
            channel
            {
                Snapshot = snapshot
                Project = project
                ObjectId = native.Id
            }
        |> ValueOption.defaultWith (fun () -> fail "Unsupported type without declaration identity")

    let eventDeclaration = symbolOfType eventType |> catalogueFor
    let eventNative = property eventMapNative "input"
    require (nativeType eventDeclaration = eventNative.PropertyType) "Event-map native type mismatch"
    let lines = ResizeArray<string>()

    let emit text =
        lines.Add text

    emit "// <auto-generated> tools/Ranvier.Dom.Generate; regenerate instead of editing. </auto-generated>"
    emit "module Ranvier.View.Foundation.Generated.Html"
    emit ""
    emit "open Fable.Core.TS"
    emit "open Ranvier.View.Foundation.Representation"
    emit "open Ranvier.View.Foundation"
    emit ""

    let omissions = ResizeArray<string * string * string>()
    let counts = ResizeArray<string * int * int * int>()

    let sharedProperties =
        System.Collections.Generic.SortedDictionary<string, ResizeArray<string>>()

    let emittedProperties = System.Collections.Generic.HashSet<string>()
    let sharedEvents = System.Collections.Generic.SortedDictionary<string, Type>()

    let commonEvents =
        properties eventMap
        |> Array.map _.Name
        |> Set.ofArray

    let voidTags =
        set
            [
                "area"
                "base"
                "br"
                "col"
                "embed"
                "hr"
                "img"
                "input"
                "link"
                "meta"
                "param"
                "source"
                "track"
                "wbr"
            ]

    let excluded =
        set
            [
                "innerHTML"
                "outerHTML"
                "textContent"
                "innerText"
                "outerText"
                "srcdoc"
                "files"
            ]

    let tryType native =
        try
            Some (typeName declarations native)
        with :? InvalidOperationException ->
            None

    let rec primitive (native: Type) =
        native = typeof<string>
        || native = typeof<bool>
        || native = typeof<float>
        || (native.IsGenericType
            && native.GetGenericTypeDefinition () = typedefof<option<_>>
            && primitive (native.GetGenericArguments()[0]))

    let moduleName (tag: string) =
        Char.ToUpperInvariant(tag[0]).ToString()
        + tag.Substring (1)

    let tagExpression tag =
        match tag with
        | "div" -> "Div"
        | "button" -> "Button"
        | "input" -> "Input"
        | "select" -> "Select"
        | "option" -> "Option"
        | _ -> "(HtmlTag \"" + tag + "\")"

    let tagMapNative = nativeType (catalogueFor tagMap)

    for tagSymbol in tags |> Array.sortBy _.Name do
        let tag = tagSymbol.Name

        let tagType =
            Api.getTypeOfSymbol
                channel
                {
                    Snapshot = snapshot
                    Project = project
                    Symbol = tagSymbol.Id
                }

        let symbol = symbolOfType tagType
        let declaration = catalogueFor symbol
        let native = nativeType declaration
        let name = typeName declarations native
        require ((property tagMapNative tag).PropertyType = native) $"Tag-map native type mismatch: {tag}"
        let expression = tagExpression tag

        if voidTags.Contains tag then
            emit $"let ``{tag}`` ops (modifiers: Typed.Modifier<{name}> list) = Typed.element ops {expression} modifiers []"
        else
            emit $"let ``{tag}`` ops (modifiers: Typed.Modifier<{name}> list) children = Typed.element ops {expression} modifiers children"

        emit ""
        emit $"module {moduleName tag} ="
        let members = properties symbol
        let mutable getters, setters, listeners = 0, 0, 0

        for memberInfo in members |> Array.sortBy _.Name do
            let memberName = memberInfo.Name

            let nativeProperties =
                Array.append [| native |] (native.GetInterfaces ())
                |> Array.collect _.GetProperties()
                |> Array.filter (fun property -> property.Name = memberName)

            if nativeProperties.Length > 0 then
                let own =
                    native.GetProperties ()
                    |> Array.filter (fun property -> property.Name = memberName)

                let candidates =
                    if own.Length > 0 then
                        own
                    else
                        nativeProperties
                        |> Array.filter (fun candidate ->
                            not (
                                nativeProperties
                                |> Array.exists (fun other ->
                                    candidate.DeclaringType <> other.DeclaringType
                                    && candidate.DeclaringType.IsAssignableFrom other.DeclaringType)
                            ))

                let signatures =
                    candidates
                    |> Array.map _.PropertyType
                    |> Array.distinct

                require (signatures.Length = 1) $"Conflicting inherited native property: {native.Name}.{memberName}"

                let nativeProperty =
                    candidates
                    |> Array.tryFind _.CanWrite
                    |> Option.defaultValue candidates[0]

                match tryType nativeProperty.PropertyType with
                | None -> omissions.Add (tag, memberName, $"unsupported type: {nativeProperty.PropertyType}")
                | Some valueType ->
                    let owner = nativeProperty.DeclaringType
                    let ownerName = typeName declarations owner

                    let canSet =
                        not (readonly memberInfo)
                        && nativeProperty.CanWrite
                        && not (excluded.Contains memberName)
                        && primitive nativeProperty.PropertyType

                    if emittedProperties.Add (owner.FullName + "." + memberName) then
                        if not (sharedProperties.ContainsKey owner.Name) then
                            sharedProperties.Add (owner.Name, ResizeArray<string>())

                        let shared = sharedProperties[owner.Name]
                        shared.Add $"        let ``{memberName}`` (node: #{ownerName}) : {valueType} = node.``{memberName}``"

                        if canSet then
                            shared.Add
                                $"        let {memberName}Static<'native when 'native :> {ownerName}> (value: {valueType}) : Typed.Modifier<'native> ="

                            shared.Add
                                $"            Typed.staticProperty (fun (node: 'native) value -> (node :> {ownerName}).``{memberName}`` <- value) value"

                            shared.Add
                                $"        let {memberName}Reactive<'native when 'native :> {ownerName}> (read: unit -> {valueType}) : Typed.Modifier<'native> ="

                            shared.Add
                                $"            Typed.reactiveProperty (fun (node: 'native) value -> (node :> {ownerName}).``{memberName}`` <- value) read"

                        shared.Add ""

                    let compatibility =
                        (Set.ofList [ "div"; "button"; "input"; "select"; "option" ]).Contains tag
                        && (Set.ofList [ "title"; "value"; "checked"; "files"; "form" ]).Contains memberName

                    let expose =
                        (native.Name <> "HTMLElement" && owner = native)
                        || compatibility

                    if expose then
                        getters <- getters + 1
                        emit $"    let ``{memberName}`` (node: {name}) : {valueType} = node.``{memberName}``"

                        if canSet then
                            setters <- setters + 1
                            emit $"    let {memberName}Static (value: {valueType}) : Typed.Modifier<{name}> ="
                            emit $"        Typed.staticProperty (fun (node: {name}) value -> node.``{memberName}`` <- value) value"
                            emit $"    let {memberName}Reactive (read: unit -> {valueType}) : Typed.Modifier<{name}> ="
                            emit $"        Typed.reactiveProperty (fun (node: {name}) value -> node.``{memberName}`` <- value) read"

                        emit ""

                    if
                        not (readonly memberInfo)
                        && nativeProperty.CanWrite
                    then
                        if excluded.Contains memberName then
                            omissions.Add (tag, memberName, "setter excluded by Ranvier ownership/form policy")
                        elif not (primitive nativeProperty.PropertyType) then
                            omissions.Add (tag, memberName, "setter requires explicit native-object equality/ownership policy")


        let eventMaps =
            Array.append [| native |] (native.GetInterfaces ())
            |> Array.choose (fun candidate ->
                let fullName = $"Fable.Core.TS.Dom.{candidate.Name}EventMap"

                declarations
                |> Array.tryFind (fun d -> d.Name = fullName))
            |> Array.distinctBy _.Name

        let events = System.Collections.Generic.Dictionary<string, Type>()

        for mapDeclaration in eventMaps |> Array.sortBy _.Name do
            let mapNative = nativeType mapDeclaration
            let mapSymbol = resolve mapNative.Name
            require (nativeType (catalogueFor mapSymbol) = mapNative) "Event map identity mismatch"

            for eventInfo in properties mapSymbol |> Array.sortBy _.Name do
                let eventProperty = property mapNative eventInfo.Name

                match tryType eventProperty.PropertyType with
                | None -> omissions.Add (tag, "event:" + eventInfo.Name, $"unsupported event type: {eventProperty.PropertyType}")
                | Some _ ->
                    let response =
                        Api.getTypeOfSymbol
                            channel
                            {
                                Snapshot = snapshot
                                Project = project
                                Symbol = eventInfo.Id
                            }

                    let eventDeclaration = symbolOfType response |> catalogueFor

                    let eventDefinition =
                        if eventProperty.PropertyType.IsGenericType then
                            eventProperty.PropertyType.GetGenericTypeDefinition ()
                        else
                            eventProperty.PropertyType

                    require (nativeType eventDeclaration = eventDefinition) $"Event-map native type mismatch: {eventInfo.Name}"

                    match events.TryGetValue eventInfo.Name with
                    | true, previous ->
                        require (previous = eventProperty.PropertyType) $"Conflicting inherited event type: {native.Name}.{eventInfo.Name}"
                    | _ -> events.Add (eventInfo.Name, eventProperty.PropertyType)

        for event in events |> Seq.sortBy _.Key do
            let eventType = typeName declarations event.Value

            if commonEvents.Contains event.Key then
                match sharedEvents.TryGetValue event.Key with
                | true, previous -> require (previous = event.Value) $"Conflicting common event: {event.Key}"
                | _ -> sharedEvents.Add (event.Key, event.Value)

            if
                not (commonEvents.Contains event.Key)
                || event.Key = "input"
                || event.Key = "click"
            then
                listeners <- listeners + 1
                emit $"    let on{moduleName event.Key} (handler: {eventType} -> unit) : Typed.Modifier<{name}> ="
                emit $"        Typed.listener \"{event.Key}\" (fun event -> handler (unbox event))"
                emit ""

        require (getters + listeners > 0) $"No usable members for tag: {tag}"
        counts.Add (tag, getters, setters, listeners)

    let shared = ResizeArray<string>()
    shared.Add "module Properties ="

    for owner in sharedProperties do
        shared.Add $"    module {owner.Key} ="
        shared.AddRange owner.Value

    shared.Add "module Events ="

    for event in sharedEvents do
        let eventType = typeName declarations event.Value

        shared.Add
            $"    let on{moduleName event.Key}<'native when 'native :> Dom.HTMLElement> (handler: {eventType} -> unit) : Typed.Modifier<'native> ="

        shared.Add $"        Typed.listener \"{event.Key}\" (fun event -> handler (unbox event))"
        shared.Add ""

    lines.InsertRange (7, shared)

    let format =
        { Fantomas.Core.FormatConfig.Default with
            MaxLineLength = 150
            MaxInfixOperatorExpression = 50
            KeepMaxNumberOfBlankLines = 3
            MaxFunctionBindingWidth = 10
            SpaceBeforeUppercaseInvocation = true
        }

    let contents =
        Fantomas.Core.CodeFormatter.FormatDocumentAsync (false, String.concat "\n" lines + "\n", format)
        |> Async.RunSynchronously
        |> fun formatted -> formatted.Code.Replace ("\r\n", "\n")

    Directory.CreateDirectory (Path.GetDirectoryName (Path.GetFullPath output))
    |> ignore

    let coverage =
        JsonSerializer.Serialize (
            {|
                package = "Xantham.Fable.Core.TS@0.2.1"
                tags =
                    counts
                    |> Seq.map (fun (tag, getters, setters, events) ->
                        {|
                            tag = tag
                            getters = getters
                            setters = setters
                            events = events
                            isVoid = voidTags.Contains tag
                        |})
                    |> Seq.toArray
                sharedPropertyCount = emittedProperties.Count
                sharedEventCount = sharedEvents.Count
                omissions =
                    omissions
                    |> Seq.map (fun (tag, memberName, reason) ->
                        {|
                            tag = tag
                            memberName = memberName
                            reason = reason
                        |})
                    |> Seq.toArray
            |},
            JsonSerializerOptions (WriteIndented = true)
        )

    File.WriteAllText (output, contents, UTF8Encoding (false))
    File.WriteAllText (Path.ChangeExtension (output, ".coverage.json"), coverage + "\n", UTF8Encoding (false))
    printfn "Generated %d HTML tags from Core.TS 0.2.1 catalogue + Wire 0.3.0 + pinned TypeScript; SHA256 %s" counts.Count (hashFile output)

[<EntryPoint>]
let main args =
    try
        let compilerRoot = __SOURCE_DIRECTORY__

        let packageRoot =
            if args.Length > 1 then
                args[1]
            else
                AppContext.BaseDirectory

        let output =
            if args.Length = 0 then
                Path.GetFullPath (Path.Combine (__SOURCE_DIRECTORY__, "../../experiments/Ranvier.View.Foundation/Generated/Html.fs"))
            else
                args[0]

        generate compilerRoot packageRoot output
        0
    with error ->
        eprintfn "%s" error.Message
        1
