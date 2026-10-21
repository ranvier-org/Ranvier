/// RAN010-RAN015: core graph traps.
module Ranvier.Analyzers.Rules.Core

open FSharp.Analyzers.SDK
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Text
open Ranvier.Analyzers
open Ranvier.Analyzers.Index
open Ranvier.Analyzers.Rule
open Ranvier.Analyzers.Symbols.Core

/// Where the node is created and what the failure is, then the fix, for a pure context of `kind`.
let private failure (kind: Pure) (context: string) (owning: string option) : string * string =
    match kind, owning with
    | Pure.Memo, Some owner ->
        $"`{context}`'s body, which fails every read of the memo",
        $"Use `{owner}` for a memo that owns the nodes its body creates, or create the node outside the memo."
    | Pure.Memo, None ->
        $"`{context}`'s body, which fails every read of the memo", "Pass `true` as the owning argument, or create the node outside the memo."
    | Pure.Async, Some owner ->
        $"`{context}`'s compute, which fails every read of the async value",
        $"Use `{owner}` for an async value that owns the nodes its body creates, or create the node outside the async value."
    | Pure.Async, None ->
        $"`{context}`'s compute, which fails every read of the async value",
        "Pass `true` as the owning argument, or create the node outside the async value."
    | Pure.EffectOn, _ -> $"`{context}`'s compute, which fails the effect", "Create the node in act, which owns it."
    | Pure.Map, _ ->
        "a projection's map, which fails the row's reads",
        "Use createProjectionWith, or createIndexProjectionWith, and create the node in the factory, which runs once per key."
    | Pure.Reader, _ -> "a projection row's reader, which fails the row's reads", "Move the creation into the factory body, which runs once per key."
    | Pure.LookupF, _ -> "a lookup's f, which fails the key's reads", "Create the node outside the lookup."
    | Pure.LookupAffected, _ -> "a lookup's affected, which fails every live key", "Create the node outside the lookup."
    | Pure.Seed, _ -> $"`{context}`'s seed, which fails every read of the editable", "Create the node outside the editable."
    | Pure.Aggregate, _ -> "a projection aggregate's add, subtract or folder, which fails the aggregate", "Create the node outside the aggregate."

/// The range of `name` at the start of the call at `range`, when the call's text begins with a qualified `name`.
let private calleeName (input: Input) (range: range) (name: string) : range option =
    let callText = text input range
    let at = callText.IndexOf name

    if
        at >= 0
        && callText.Substring (0, at)
           |> Seq.forall (fun c ->
               System.Char.IsLetterOrDigit c
               || c = '.'
               || c = '_')
    then
        let start = Position.mkPos range.StartLine (range.StartColumn + at)
        Some (Range.mkRange range.FileName start (Position.mkPos range.StartLine (range.StartColumn + at + name.Length)))
    else
        None

let private ownedInPure (input: Input) : Message list =
    [
        for site in input.Index.Calls do
            if isCreator site.Callee then
                let context =
                    Index.arguments isTransparent site.Path
                    |> List.tryPick (fun (callee, index, count, lambdas) ->
                        match pureContext callee index count with
                        | Some pure when pure.Layers = lambdas -> Some (callee, pure)
                        | _ -> None)

                match context with
                | Some (callee, pure) ->
                    let contextName = Symbols.display callee
                    let where, fix = failure pure.Kind contextName pure.Owning

                    let fixes =
                        match pure.Owning with
                        | Some owner ->
                            // The innermost call of the context's callee around the creator: a memo can nest in a memo.
                            let contextSite =
                                input.Index.Calls
                                |> Array.filter (fun candidate ->
                                    candidate.Callee = callee
                                    && Range.rangeContainsRange candidate.Range site.Range)
                                |> Array.sortBy (fun candidate ->
                                    candidate.Range.EndLine
                                    - candidate.Range.StartLine,
                                    candidate.Range.EndColumn
                                    - candidate.Range.StartColumn)
                                |> Array.tryHead

                            match
                                contextSite
                                |> Option.bind (fun s -> calleeName input s.Range callee.LogicalName)
                            with
                            | Some nameRange -> [ replace nameRange callee.LogicalName owner ]
                            | None -> []
                        | None -> []

                    message "RAN010" Severity.Error site.Range $"`{Symbols.display site.Callee}` creates an owned node inside {where}. {fix}" fixes
                | None -> ()
    ]

let ran010: Rule =
    {
        Code = "RAN010"
        Title = "Owned node created in a pure derivation"
        Gate = Symbols.Assemblies.Ranvier
        Run = ownedInPure
    }

/// The lambdas on `steps` that run outside the enclosing run: immediate and transparent lambdas do not count.
let private lambdaCount (steps: Step list) =
    let rec go count steps =
        match steps with
        | [] -> count
        | Step.Lambda (_, true) :: rest -> go count rest
        | Step.Lambda (_, false) :: Step.Argument (callee, _, _) :: rest when isTransparent callee -> go count rest
        | Step.Lambda (_, false) :: rest -> go (count + 1) rest
        | _ :: rest -> go count rest

    go 0 steps

let private recreated (input: Input) : Message list =
    [
        for site in input.Index.Lets do
            let created =
                input.Index.Calls
                |> Array.exists (fun call ->
                    isAsyncCreator call.Callee
                    && (match call.Path with
                        | Step.Bound value :: rest ->
                            value = site.Value
                            && obj.ReferenceEquals (rest, site.Path)
                        | _ -> false))

            let owner =
                if created then
                    Index.arguments isTransparent site.Path
                    |> List.tryHead
                    |> Option.bind (fun (callee, index, count, lambdas) ->
                        match ownerBody callee index count with
                        | Some layers when layers = lambdas -> Some callee
                        | _ -> None)
                else
                    None

            match owner with
            | Some callee ->
                let readInBody =
                    input.Index.Calls
                    |> Array.exists (fun call ->
                        isReactiveRead call.Callee
                        && (match call.Receiver with
                            | Some (Value value) -> value = site.Value
                            | _ -> false)
                        && (match Index.within site.Path call.Path with
                            | Some inner -> lambdaCount inner = 0
                            | None -> false))

                if readInBody then
                    let name = site.Value.DisplayName
                    let ownerName = Symbols.display callee

                    message
                        "RAN011"
                        Severity.Warning
                        site.Value.DeclarationLocation
                        $"`{name}` is created inside `{ownerName}`'s body and read there; each run of the body disposes `{name}` and starts a new flight. Create `{name}` outside `{ownerName}` and read it inside."
                        []
            | None -> ()
    ]

let ran011: Rule =
    {
        Code = "RAN011"
        Title = "Async value created and read in the owner that replaces it"
        Gate = Symbols.Assemblies.Ranvier
        Run = recreated
    }

/// Whether the expression at `path` sits in the continuation of a computation-expression bind inside the compute of
/// an async value's creator, outside the body of a node created there and outside `untrack`.
let private afterAwait (path: Step list) : bool =
    let rec go (awaited: bool) (steps: Step list) =
        match steps with
        | [] -> false
        | Step.Lambda (_, false) :: (Step.Argument (callee, index, count) :: _ as rest) when
            callee.LogicalName.StartsWith "Bind"
            && index = count - 1
            ->
            go true rest
        // A node created in the continuation, or `untrack`, owns the reads of its own body.
        | Step.Lambda (_, false) :: Step.Argument (callee, index, count) :: _ when
            asyncComputeIndex callee count <> Some index
            && (isCreator callee
                || Symbols.isMember Api "untrack" callee)
            ->
            false
        | Step.Argument (callee, index, count) :: rest ->
            match asyncComputeIndex callee count with
            | Some compute when compute = index -> awaited
            | _ -> go awaited rest
        | _ :: rest -> go awaited rest

    go false path

let private untrackedReads (input: Input) : Message list =
    [
        for site in input.Index.Calls do
            match site.Receiver with
            | Some receiver when isReactiveRead site.Callee && afterAwait site.Path ->
                let read = text input site.Range
                let source = text input receiver.Range

                message
                    "RAN012"
                    Severity.Warning
                    site.Range
                    $"`{read}` is read after the first `let!`/`do!` of this async memo; a read after an await that suspends is untracked, and a change to `{source}` starts no flight. Read it before the first await and bind it to a local."
                    []
            | _ -> ()
    ]

let ran012: Rule =
    {
        Code = "RAN012"
        Title = "Reactive read after the first await of an async memo"
        Gate = Symbols.Assemblies.Ranvier
        Run = untrackedReads
    }
