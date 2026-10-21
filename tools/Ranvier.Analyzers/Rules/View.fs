/// RAN020-RAN025: view layer traps.
module Ranvier.Analyzers.Rules.View

open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Text
open Ranvier.Analyzers
open Ranvier.Analyzers.Index
open Ranvier.Analyzers.Rule
open Ranvier.Analyzers.Symbols.View

/// Whether the expression at `path` is lexically inside a region lambda.
let private inRegion (path: Step list) : bool =
    path
    |> List.exists (fun step ->
        match step with
        | Step.Argument (callee, index, _) -> isRegionLambda callee index
        | _ -> false)

[<RequireQualifiedAccess>]
type private BuildSite =
    | Text
    | Part of kind: string
    | RenderOf

let private parameterAt (m: FSharpMemberOrFunctionOrValue) (index: int) : FSharpParameter option =
    m.CurriedParameterGroups
    |> Seq.concat
    |> Seq.tryItem index

/// The full name of `t`'s type definition, through abbreviations such as `string`.
let private typeName (t: FSharpType) : string option =
    let t = t.StripAbbreviations ()

    if t.HasTypeDefinition then
        t.TypeDefinition.TryFullName
    else
        None

let private isString (t: FSharpType) =
    typeName t = Some Symbols.StringType

let private isStatic (t: FSharpType) =
    not (t.StripAbbreviations ()).IsFunctionType
    && typeName t <> Some Source

/// The build-time site that argument `index` of a call of `m` is.
let private buildSite (m: FSharpMemberOrFunctionOrValue) (index: int) : BuildSite option =
    let entity = Symbols.enclosing m

    match parameterAt m index with
    | Some parameter when isTextMember m && isString parameter.Type -> Some BuildSite.Text
    | Some parameter when
        (entity = Attributes || entity = Properties)
        && parameter.Name = Some ValueParameter
        && isStatic parameter.Type
        ->
        Some (BuildSite.Part (if entity = Attributes then "attribute" else "property"))
    | Some _ when isRenderOf m && index = 1 -> Some BuildSite.RenderOf
    | _ -> None

/// The build-time site whose argument holds the expression at `path` outside any lambda, with the callee and index.
let private buildSiteOf (path: Step list) =
    let rec go (steps: Step list) =
        match steps with
        | [] -> None
        | Step.Lambda (_, true) :: rest -> go rest
        | Step.Lambda (_, false) :: _ -> None
        | Step.Argument (callee, index, _) :: rest ->
            match buildSite callee index with
            | Some site -> Some (callee, index, site)
            | None -> go rest
        | _ :: rest -> go rest

    go path

/// Whether `m` has an overload of the same name that takes a reader (a parameter named `read`).
let private hasReaderOverload (m: FSharpMemberOrFunctionOrValue) =
    match m.DeclaringEntity with
    | Some entity ->
        entity.MembersFunctionsAndValues
        |> Seq.exists (fun candidate ->
            candidate.LogicalName = m.LogicalName
            && candidate.CurriedParameterGroups
               |> Seq.concat
               |> Seq.exists (fun p -> p.Name = Some ReaderParameter))
    | None -> false

/// The expressions in the return position of `expr`: through `let`, sequencing, records and tuples.
let rec private returned (expr: FSharpExpr) : FSharpExpr list =
    match expr with
    | Let (_, body) -> returned body
    | Sequential (_, last) -> returned last
    | NewRecord (_, fields) -> fields
    | NewTuple (_, items) -> items
    | _ -> [ expr ]

let rec private bodyAfter (layers: int) (expr: FSharpExpr) =
    match expr with
    | Lambda (_, body) when layers > 0 -> bodyAfter (layers - 1) body
    | _ -> expr

let private readCall (expr: FSharpExpr) =
    match expr with
    | Call (Some _, m, _, _, _) when Symbols.Core.isReactiveRead m -> true
    | _ -> false

let private buildTimeReads (input: Input) : Message list =
    let calls = input.Index.Calls

    let staticSites =
        [
            for read in calls do
                if
                    Symbols.Core.isReactiveRead read.Callee
                    && not (inRegion read.Path)
                then
                    match buildSiteOf read.Path with
                    | Some (callee, index, site) ->
                        let owner =
                            calls
                            |> Array.tryFind (fun call ->
                                call.Callee = callee
                                && index < call.Arguments.Length
                                && Range.rangeContainsRange call.Arguments[index].Range read.Range)

                        match owner with
                        | Some call -> call, index, site, read
                        | None -> ()
                    | None -> ()
        ]
        |> List.distinctBy (fun (call, index, _, _) -> call.Range, index)

    [
        for call, index, site, read in staticSites do
            let readText = text input read.Range
            let argument = call.Arguments[index]
            let argumentText = text input argument.Range

            match site with
            | BuildSite.Text ->
                message
                    "RAN020"
                    Severity.Warning
                    read.Range
                    $"`{readText}` is read once, when the template is built, so this text never updates. Yield a reader: `fun _ -> {argumentText}`."
                    [ replace argument.Range argumentText $"fun _ -> {argumentText}" ]
            | BuildSite.Part kind ->
                let fixes =
                    if hasReaderOverload call.Callee then
                        [ replace argument.Range argumentText $"fun _ -> {argumentText}" ]
                    else
                        []

                message
                    "RAN020"
                    Severity.Warning
                    read.Range
                    $"`{readText}` is read once, when the template is built, so the {kind} never updates. Pass a reader: `fun _ -> {argumentText}`."
                    fixes
            | BuildSite.RenderOf ->
                message
                    "RAN020"
                    Severity.Warning
                    read.Range
                    $"`{readText}` is read once, when the template is built, so the child's props never update. Pass the signal or memo in the props and read it in the child."
                    []

        for call in calls do
            let entity = Symbols.enclosing call.Callee

            let setup = isSetupDefinition call.Callee
            let renderProps = isRender call.Callee

            if setup && not call.Arguments.IsEmpty then
                for expr in returned (bodyAfter 2 call.Arguments[0]) do
                    if readCall expr then
                        message
                            "RAN020"
                            Severity.Warning
                            expr.Range
                            $"`{text input expr.Range}` is read once per instance, when setup runs untracked, so the instance holds the value at mount. Return the signal or memo itself and read it in the template."
                            []

            if renderProps && call.Arguments.Length = 2 then
                for expr in returned (bodyAfter 1 call.Arguments[1]) do
                    if readCall expr then
                        let readText = text input expr.Range

                        let signal =
                            match expr with
                            | Call (Some receiver, _, _, _, _) -> text input receiver.Range
                            | _ -> readText

                        message
                            "RAN020"
                            Severity.Warning
                            expr.Range
                            $"`render`'s props reader runs once per instance, so the child receives the value of `{readText}` at mount. Pass the signal: `fun s -> {signal}`."
                            []
    ]

let ran020: Rule =
    {
        Code = "RAN020"
        Title = "Reactive read at template build time"
        Gate = Symbols.Assemblies.View
        Run = buildTimeReads
    }

let rec private subExpressions (expr: FSharpExpr) : FSharpExpr list =
    expr
    :: List.collect subExpressions expr.ImmediateSubExpressions

let private isPrimitive (t: FSharpType) =
    t.HasTypeDefinition
    && (t.TypeDefinition.IsValueType
        || t.TypeDefinition.TryFullName = Some Symbols.StringType)

let private comparedByReference (t: FSharpType) =
    t.HasTypeDefinition
    && (let entity = t.TypeDefinition

        entity.IsClass
        && not entity.IsFSharpRecord
        && not entity.IsFSharpUnion
        && entity.TryFullName <> Some Symbols.StringType
        && not (
            entity.MembersFunctionsAndValues
            |> Seq.exists (fun m ->
                m.LogicalName = "Equals"
                && m.IsOverrideOrExplicitInterfaceImplementation)
        ))

let private unstableKeys (input: Input) : Message list =
    [
        for site in input.Index.Calls do
            if isKeyed site.Callee && not site.Arguments.IsEmpty then
                let key = site.Arguments[0]

                match key with
                | Lambda (item, body) ->
                    let inner = subExpressions body

                    let impure =
                        inner
                        |> List.tryPick (fun expr ->
                            match expr with
                            | Call (_, m, _, _, _)
                            | NewObject (m, _, _) when Symbols.Keys.impureCall m -> Some (text input expr.Range)
                            | ValueSet (v, _) when not (Range.rangeContainsRange key.Range v.DeclarationLocation) -> Some (text input expr.Range)
                            | _ -> None)

                    let positional =
                        inner
                        |> List.tryPick (fun expr ->
                            match expr with
                            | Call (_, m, _, _, _) when Symbols.Keys.positionalCall m ->
                                // The call as written, up to its arguments: `Array.IndexOf`, `List.findIndex`.
                                let written = text input expr.Range
                                let stop = written.IndexOfAny [| ' '; '(' |]
                                Some (if stop > 0 then written.Substring (0, stop) else written)
                            | _ -> None)

                    let tracked =
                        inner
                        |> List.tryPick (fun expr ->
                            match expr with
                            | Call (Some _, m, _, _, _) when Symbols.Core.isReactiveRead m -> Some (text input expr.Range)
                            | _ -> None)

                    let structuredItem =
                        item.FullType.HasTypeDefinition
                        && (item.FullType.TypeDefinition.IsFSharpRecord
                            || item.FullType.TypeDefinition.IsFSharpUnion)

                    let identity =
                        isForEach site.Callee
                        && structuredItem
                        && (match body with
                            | Value v -> v = item
                            | Call (None, m, _, _, [ Value v ]) -> m.LogicalName = "id" && v = item
                            | _ -> false)

                    match impure, positional, tracked with
                    | Some call, _, _ ->
                        message
                            "RAN021"
                            Severity.Warning
                            key.Range
                            $"This key changes on every pass (`{call}`), so every row remounts. Key by a value unique to the item, such as an id."
                            []
                    | None, Some call, _ ->
                        message
                            "RAN021"
                            Severity.Warning
                            key.Range
                            $"This key is the item's position (`{call}`), so moving or inserting an item remounts every row after it. Key by a value unique to the item, such as an id."
                            []
                    | None, None, Some read ->
                        message
                            "RAN021"
                            Severity.Warning
                            key.Range
                            $"This key reads `{read}`, a tracked value outside the item, so a change to it re-keys the rows. Key by a value of the item."
                            []
                    | None, None, None when identity ->
                        message
                            "RAN021"
                            Severity.Info
                            key.Range
                            "The key is the item itself, so a replaced item gets a new key and its row remounts instead of following the item. Key by an id field."
                            []
                    | None, None, None when
                        comparedByReference body.Type
                        && not (isPrimitive body.Type)
                        ->
                        message
                            "RAN021"
                            Severity.Info
                            key.Range
                            $"The key `{body.Type.Format FSharpDisplayContext.Empty}` is compared by reference, so a rebuilt item gets a new key and its row remounts. Key by a value with structural equality, such as an id."
                            []
                    | _ -> ()
                | _ -> ()
    ]

let ran021: Rule =
    {
        Code = "RAN021"
        Title = "Unstable list key"
        Gate = Symbols.Assemblies.View
        Run = unstableKeys
    }

let rec private createsDefinition (expr: FSharpExpr) =
    match expr with
    | Call (_, m, _, _, _) when isDefinitionCreator m -> true
    | _ -> List.exists createsDefinition expr.ImmediateSubExpressions

/// Whether the getter `m` is a static `Component` property whose implementation creates a definition.
let private recreatingProperty (project: FSharpCheckProjectResults option) (m: FSharpMemberOrFunctionOrValue) =
    m.IsPropertyGetterMethod
    && not m.IsInstanceMember
    && m.ReturnParameter.Type.HasTypeDefinition
    && m.ReturnParameter.Type.TypeDefinition.TryFullName = Some ComponentType
    && (match project with
        | Some results ->
            let rec find (item: FSharpImplementationFileDeclaration) =
                match item with
                | FSharpImplementationFileDeclaration.Entity (_, items) -> List.exists find items
                | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue (value, _, body) ->
                    value.IsEffectivelySameAs m
                    && createsDefinition body
                | FSharpImplementationFileDeclaration.InitAction _ -> false

            results.AssemblyContents.ImplementationFiles
            |> List.exists (fun file -> List.exists find file.Declarations)
        | None -> false)

let private regionDefinitions (input: Input) : Message list =
    [
        for site in input.Index.Calls do
            let creator = isDefinitionCreator site.Callee

            let property =
                not creator
                && recreatingProperty input.Project site.Callee

            if (creator || property) && inRegion site.Path then
                let name =
                    if creator then
                        site.Callee.DisplayName
                    else
                        text input site.Range

                message
                    "RAN022"
                    Severity.Warning
                    site.Range
                    $"`{name}` runs on every read of this region, so the arm is a new definition each time and remounts. Bind the definition with `let` outside the template."
                    []
    ]

let ran022: Rule =
    {
        Code = "RAN022"
        Title = "Definition created on every region read"
        Gate = Symbols.Assemblies.View
        Run = regionDefinitions
    }

/// An element written with literal parts: an explicit `t.tag parts children` call or a sugar tag.
[<NoEquality; NoComparison>]
type ElementSite =
    {
        Tag: string
        Parts: FSharpExpr list
        HasChildren: bool
        Range: range
        /// The tags of the literal enclosing elements, nearest first, lower case.
        Ancestors: string list
    }

let rec private items (expr: FSharpExpr) : FSharpExpr list =
    match expr with
    | NewArray (_, elements) -> List.collect items elements
    | Coerce (_, inner) -> items inner
    | _ -> [ expr ]

/// Whether a call of `m` with `arguments` writes an element: a generated tag member of the builder, `t.div parts
/// children`, or `t.br parts` for a void element.
let private isTagMember (m: FSharpMemberOrFunctionOrValue) (arguments: FSharpExpr list) =
    m.IsExtensionMember
    && Symbols.enclosing m = Builder
    && (arguments.Length = 1 || arguments.Length = 2)
    && arguments.Head.Type.IsArrayType

let private sugarTag (m: FSharpMemberOrFunctionOrValue) =
    match m.ApparentEnclosingEntity with
    | Some entity when
        m.IsConstructor
        && (Symbols.enclosing m).StartsWith(Elements + ".")
        ->
        Some (entity.DisplayName.Trim '`')
    | _ -> None

/// The literal elements of the file, each with its literal ancestors.
let elements (input: Input) : ElementSite list =
    let calls = input.Index.Calls

    let builders =
        calls
        |> Array.choose (fun site ->
            match sugarTag site.Callee, site.Path with
            | Some tag, Step.Applied parameter :: _ -> Some (parameter, tag)
            | _ -> None)

    let builderTag (parameter: FSharpMemberOrFunctionOrValue) =
        builders
        |> Array.tryPick (fun (b, tag) -> if b = parameter then Some tag else None)

    let ancestors (path: Step list) =
        path
        |> List.choose (fun step ->
            match step with
            | Step.Argument (callee, 1, 2) when
                callee.IsExtensionMember
                && Symbols.enclosing callee = Builder
                ->
                Some callee.LogicalName
            | Step.Lambda (Some parameter, true) -> builderTag parameter
            | _ -> None)
        |> List.map (fun tag -> tag.ToLowerInvariant ())

    [
        for site in calls do
            if isTagMember site.Callee site.Arguments then
                {
                    Tag = site.Callee.LogicalName.ToLowerInvariant ()
                    Parts = items site.Arguments[0]
                    HasChildren =
                        site.Arguments.Length = 2
                        && not (items site.Arguments[1]).IsEmpty
                    Range = site.Range
                    Ancestors = ancestors site.Path
                }
            else
                match sugarTag site.Callee, site.Path with
                | Some tag, Step.Applied parameter :: outer ->
                    let children =
                        calls
                        |> Array.exists (fun call ->
                            isChildYield call.Callee
                            && (match call.Receiver with
                                | Some (Value receiver) -> receiver = parameter
                                | _ -> false))

                    {
                        Tag = tag.ToLowerInvariant ()
                        Parts = List.collect items site.Arguments
                        HasChildren = children
                        Range = site.Range
                        Ancestors = ancestors outer
                    }
                | _ -> ()
    ]

let private invalidContent (input: Input) : Message list =
    [
        for element in elements input do
            let contents =
                element.Parts
                |> List.choose (fun part ->
                    Symbols.View.contentPart part
                    |> Option.map (fun name -> name, part))

            match contents with
            | (first, _) :: (second, part) :: _ ->
                message
                    "RAN023"
                    Severity.Error
                    part.Range
                    $"This element has two content parts (`{first}` and `{second}`), which fails when the definition is created. Give each content part its own element."
                    []
            | [ name, part ] when element.HasChildren ->
                message
                    "RAN023"
                    Severity.Error
                    part.Range
                    $"This element has a content part (`{name}`) and template children, which fails when the definition is created. Move the children into another element."
                    []
            | [ "Unsafe.html", part ] when
                Symbols.View.voidTags.Contains element.Tag
                || Symbols.View.rawTextTags.Contains element.Tag
                || element.Tag = "template"
                || element.Tag = "textarea"
                || element.Tag = "title"
                ->
                message
                    "RAN023"
                    Severity.Error
                    part.Range
                    $"`Unsafe.html` sets the content of `{element.Tag}`, whose children the HTML parser does not build from markup, which fails when the definition is created. Put the markup in an element such as `div`."
                    []
            | _ -> ()
    ]

let ran023: Rule =
    {
        Code = "RAN023"
        Title = "Invalid content parts on an element"
        Gate = Symbols.Assemblies.View
        Run = invalidContent
    }

let private refused (element: ElementSite) : string option =
    match element.Tag with
    | "script" ->
        Some
            "`script` is refused by every render (\"Script elements are not created by templates or typed views\"). Load scripts through the server document shell, for example `Server.Page` scripts."
    | "template" ->
        Some
            "`template` is refused by every render (\"Template elements are not supported in templates\"). Build the content with a component instead."
    | tag when Symbols.View.rawTextTags.Contains tag ->
        Some
            $"`{tag}` is a raw-text element, which the server render refuses (\"Raw-text elements require an explicit HTML serialization policy\"). Write it outside the template."
    | _ -> None

let private restructured (element: ElementSite) : string option =
    try
        Symbols.View.checkNesting element.Ancestors element.Tag
        None
    with :? System.InvalidOperationException as failure ->
        Some $"{failure.Message}, which the server render refuses at its first render. Nest the elements as the HTML parser builds them."

let private serverRefusals (input: Input) : Message list =
    [
        for element in elements input do
            match refused element, restructured element with
            | Some text, _
            | None, Some text -> message "RAN024" Severity.Warning element.Range text []
            | None, None -> ()
    ]

let ran024: Rule =
    {
        Code = "RAN024"
        Title = "Markup the server render refuses"
        Gate = Symbols.Assemblies.View
        Run = serverRefusals
    }

let rec private queuedMode (lets: LetSite array) (expr: FSharpExpr) : bool =
    match expr with
    | NewUnionCase (_, case, _) -> Symbols.View.isQueuedCommits case
    | Call (None, m, _, _, _) when Symbols.View.isQueuedModeFunction m -> true
    | Value value ->
        lets
        |> Array.exists (fun site -> site.Value = value && queuedMode lets site.Bound)
    | Coerce (_, inner) -> queuedMode lets inner
    | _ -> false

let private queuedHydration (input: Input) : Message list =
    [
        for site in input.Index.Calls do
            match site.Arguments with
            | mode :: _ when
                Symbols.View.isHydrateWith site.Callee
                && queuedMode input.Index.Lets mode
                ->
                message
                    "RAN025"
                    Severity.Error
                    mode.Range
                    "Hydration supports synchronous commits only, so this call raises. Pass `SynchronousCommits`, or call `hydrate`."
                    []
            | _ -> ()
    ]

let ran025: Rule =
    {
        Code = "RAN025"
        Title = "hydrateWith with queued commits"
        Gate = Symbols.Assemblies.Foundation
        Run = queuedHydration
    }
