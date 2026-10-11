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
    require (declaration.Arity = 0) $"Unsupported generic native type: {declaration.Name}"
    require (declaration.Name.StartsWith ("Fable.Core.TS.Dom.", StringComparison.Ordinal)) $"Unsupported native module: {declaration.Name}"
    nativeAssembly.GetType (declaration.Name.Replace ("Fable.Core.TS.Dom.", "Fable.Core.TS+Dom+"), true)

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
            let name = native.FullName.Replace ("Fable.Core.TS+Dom+", "Fable.Core.TS.Dom.")

            let declaration =
                declarations
                |> Array.tryFind (fun d -> d.Name = name)
                |> Option.defaultWith (fun () -> fail $"Unsupported native type shape: {native}")

            require (nativeType declaration = native) $"Native representation mismatch: {name}"
            "Dom." + native.Name

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
    let eventName = typeName declarations eventNative.PropertyType
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

    for tag, caseName, writable, getters in
        [
            "div", "Div", [ "title" ], []
            "button", "Button", [ "title"; "value" ], []
            "input", "Input", [ "title"; "value"; "checked" ], [ "files"; "form" ]
            "select", "Select", [ "title"; "value" ], []
            "option", "Option", [ "title"; "value" ], []
        ] do
        let tagSymbol = memberSymbol tags tag

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
        require ((property (nativeType (catalogueFor tagMap)) tag).PropertyType = native) $"Tag-map native type mismatch: {tag}"

        if tag = "input" then
            emit $"let {tag} ops (modifiers: Typed.Modifier<{name}> list) = Typed.element ops {caseName} modifiers []"
        else
            emit $"let {tag} ops (modifiers: Typed.Modifier<{name}> list) children = Typed.element ops {caseName} modifiers children"

        emit ""
        emit $"module {caseName} ="
        let members = properties symbol

        for memberName in writable do
            let memberInfo = memberSymbol members memberName
            require (not (readonly memberInfo)) $"Refusing setter for readonly compiler property: {native.Name}.{memberName}"
            let nativeProperty = property native memberName
            require nativeProperty.CanWrite $"Native property is not writable: {native.Name}.{memberName}"
            let valueType = typeName declarations nativeProperty.PropertyType
            emit $"    let {memberName}Static (value: {valueType}) : Typed.Modifier<{name}> ="
            emit $"        Typed.staticProperty (fun (node: {name}) value -> node.``{memberName}`` <- value) value"
            emit $"    let {memberName}Reactive (read: unit -> {valueType}) : Typed.Modifier<{name}> ="
            emit $"        Typed.reactiveProperty (fun (node: {name}) value -> node.``{memberName}`` <- value) read"

        for memberName in getters do
            let memberInfo = memberSymbol members memberName

            if memberName = "form" then
                require (readonly memberInfo) "Expected readonly form property"

            let nativeProperty = property native memberName
            let valueType = typeName declarations nativeProperty.PropertyType
            emit $"    let {memberName} (node: {name}) : {valueType} = node.{memberName}"

        emit $"    let onInput (handler: {eventName} -> unit) : Typed.Modifier<{name}> ="
        emit "        Typed.listener \"input\" (fun event -> handler (unbox event))"
        emit ""

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

    File.WriteAllText (output, contents, UTF8Encoding (false))
    printfn "Generated five HTML tags from Core.TS 0.2.1 catalogue + Wire 0.3.0 + pinned TypeScript; SHA256 %s" (hashFile output)

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
