/// RAN001 and RAN002: code that a Fable build compiles to different behaviour, or fails to precompile.
module Ranvier.Analyzers.Rules.Fable

open FSharp.Analyzers.SDK
open FSharp.Compiler.Symbols
open Ranvier.Analyzers
open Ranvier.Analyzers.Rule

[<Literal>]
let private CompilerGenerated =
    "System.Runtime.CompilerServices.CompilerGeneratedAttribute"

/// The union and case of a compiler-generated `IsCase` getter of a union declared in the analyzed assembly.
let private generatedTester (m: FSharpMemberOrFunctionOrValue) : (FSharpEntity * FSharpUnionCase) option =
    match m.DeclaringEntity with
    | Some union when
        m.IsPropertyGetterMethod
        && union.IsFSharpUnion
        && union.Assembly.FileName.IsNone
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
            match generatedTester site.Callee, site.Receiver with
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
