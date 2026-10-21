/// RAN001 and RAN002: code that a Fable build compiles to different behaviour, or fails to precompile.
module Ranvier.Analyzers.Rules.Fable

open System
open System.IO
open FSharp.Analyzers.SDK
open FSharp.Compiler.Symbols
open FSharp.Compiler.Symbols.FSharpExprPatterns
open FSharp.Compiler.Text
open Ranvier.Analyzers
open Ranvier.Analyzers.Index
open Ranvier.Analyzers.Rule

[<Literal>]
let private CompilerGenerated =
    "System.Runtime.CompilerServices.CompilerGeneratedAttribute"

/// The union and case of a compiler-generated `IsCase` getter of a union declared in the analyzed project.
let private generatedTester (input: Input) (m: FSharpMemberOrFunctionOrValue) : (FSharpEntity * FSharpUnionCase) option =
    match m.DeclaringEntity with
    | Some union when
        m.IsPropertyGetterMethod
        && union.IsFSharpUnion
        && inProject input union.DeclarationLocation.FileName
        ->
        let generated =
            m.Attributes
            |> Seq.exists (fun attribute -> attribute.AttributeType.TryFullName = Some CompilerGenerated)

        if generated then
            union.UnionCases
            |> Seq.tryFind (fun case -> m.LogicalName = "get_Is" + case.Name)
            |> Option.map (fun case -> union, case)
        else
            None
    | _ -> None

/// The pattern of `case` as written in `file`: qualified by the union's name inside the union's own module or
/// namespace, and by its access path elsewhere. A case with fields takes one wildcard.
let private casePattern (file: string) (union: FSharpEntity) (case: FSharpUnionCase) : string =
    let local = union.DisplayName + "." + case.Name

    let name =
        if
            file = union.AccessPath
            || file.StartsWith (union.AccessPath + ".")
        then
            local
        else
            union.AccessPath + "." + local

    if case.Fields.Count = 0 then name else name + " _"

let private testers (input: Input) : Message list =
    [
        for site in input.Index.Calls do
            match generatedTester input site.Callee, site.Receiver with
            | Some (union, case), Some receiver ->
                let receiverText = text input receiver.Range

                let replacement =
                    $"(match {receiverText} with {casePattern input.Tree.QualifiedName union case} -> true | _ -> false)"

                message
                    "RAN002"
                    Severity.Warning
                    site.Range
                    $"`{receiverText}.Is{case.Name}` calls a compiler-generated union tester, which a Fable precompiled assembly does not declare. Write `{replacement}`."
                    [ replace site.Range (text input site.Range) replacement ]
            | _ -> ()
    ]

let ran002: Rule =
    {
        Code = "RAN002"
        Title = "Generated union case tester in a Fable library"
        Gate = Symbols.Assemblies.Fable
        Run = testers
    }

/// The module-level `let mutable` that `expr` reads bare.
let private moduleMutable (expr: FSharpExpr) : FSharpMemberOrFunctionOrValue option =
    match expr with
    | Call (None, value, [], [], []) when value.IsMutable && value.IsModuleValueOrMember -> Some value
    | _ -> None

let private sameFile (left: string) (right: string) =
    String.Equals (Path.GetFullPath left, Path.GetFullPath right, StringComparison.OrdinalIgnoreCase)

/// The mutable a binding snapshots, and whether a fix can make the binding mutable: false for a tuple pattern.
let private snapshotOf (lets: LetSite array) (site: LetSite) : (FSharpMemberOrFunctionOrValue * bool) option =
    match moduleMutable site.Bound, site.Bound with
    | Some source, _ -> Some (source, true)
    | None, TupleGet (_, item, Value tuple) ->
        lets
        |> Array.tryPick (fun outer ->
            match outer.Bound with
            | NewTuple (_, elements) when outer.Value = tuple && item < elements.Length ->
                moduleMutable elements[item]
                |> Option.map (fun source -> source, false)
            | _ -> None)
    | None, _ -> None

let private qualified (source: FSharpMemberOrFunctionOrValue) =
    match source.ApparentEnclosingEntity with
    | Some entity -> entity.DisplayName + "." + source.DisplayName
    | None -> source.DisplayName

let private snapshots (input: Input) : Message list =
    let lets = input.Index.Lets

    [
        for site in lets do
            if
                not site.Value.IsMutable
                && not site.Value.IsCompilerGenerated
            then
                match snapshotOf lets site with
                | Some (source, fixable) when not (sameFile source.DeclarationLocation.FileName site.Value.DeclarationLocation.FileName) ->
                    match Index.uses site.Value site.Body with
                    | [ single ] ->
                        let name = site.Value.DisplayName
                        let at = site.Value.DeclarationLocation

                        let fixes =
                            if fixable then
                                [ replace (Range.mkRange at.FileName at.Start at.Start) "" "mutable " ]
                            else
                                []

                        message
                            "RAN001"
                            Severity.Warning
                            at
                            $"Fable Release compiles `{name}` as a second read of `{qualified source}` at its single use (line {single.StartLine}), so writes between the two lines change it. Declare it `let mutable {name}` to keep the snapshot."
                            fixes
                    | _ -> ()
                | _ -> ()
    ]

let ran001: Rule =
    {
        Code = "RAN001"
        Title = "Fable inlines a snapshot of a module mutable"
        Gate = Symbols.Assemblies.Fable
        Run = snapshots
    }
