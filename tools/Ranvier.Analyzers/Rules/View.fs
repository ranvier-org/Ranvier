/// RAN020-RAN025: view layer traps.
module Ranvier.Analyzers.Rules.View

open FSharp.Analyzers.SDK
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
    typeName t = Some "System.String"

let private isStatic (t: FSharpType) =
    not (t.StripAbbreviations ()).IsFunctionType
    && typeName t <> Some Source

/// The build-time site that argument `index` of a call of `m` is.
let private buildSite (m: FSharpMemberOrFunctionOrValue) (index: int) : BuildSite option =
    let entity = Symbols.enclosing m

    match parameterAt m index with
    | Some parameter when
        entity = Element
        && m.LogicalName = "Yield"
        && isString parameter.Type
        ->
        Some BuildSite.Text
    | Some parameter when
        entity = Builder
        && m.LogicalName = "text"
        && isString parameter.Type
        ->
        Some BuildSite.Text
    | Some parameter when
        (entity = Attributes || entity = Properties)
        && parameter.Name = Some "value"
        && isStatic parameter.Type
        ->
        Some (BuildSite.Part (if entity = Attributes then "attribute" else "property"))
    | Some _ when
        entity = Sugar
        && m.LogicalName = "renderOf"
        && index = 1
        ->
        Some BuildSite.RenderOf
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
               |> Seq.exists (fun p -> p.Name = Some "read"))
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

            let setup =
                (entity = Component
                 && call.Callee.LogicalName = "define")
                || (entity = SugarComponent
                    && call.Callee.LogicalName = "template")

            let renderProps =
                (entity = Sugar || entity = Builder)
                && call.Callee.LogicalName = "render"

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

let private isKeyed (m: FSharpMemberOrFunctionOrValue) =
    let entity = Symbols.enclosing m

    (entity = Builder && m.LogicalName = "each")
    || (entity = Sugar
        && (m.LogicalName = "each"
            || m.LogicalName = "forEach"
            || m.LogicalName = "forEachFixed"))

let rec private subExpressions (expr: FSharpExpr) : FSharpExpr list =
    expr
    :: List.collect subExpressions expr.ImmediateSubExpressions

let private isPrimitive (t: FSharpType) =
    t.HasTypeDefinition
    && (t.TypeDefinition.IsValueType
        || t.TypeDefinition.TryFullName = Some "System.String")

let private comparedByReference (t: FSharpType) =
    t.HasTypeDefinition
    && (let entity = t.TypeDefinition

        entity.IsClass
        && not entity.IsFSharpRecord
        && not entity.IsFSharpUnion
        && entity.TryFullName <> Some "System.String"
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
                        site.Callee.LogicalName = "forEach"
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
