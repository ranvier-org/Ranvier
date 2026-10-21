/// Full names of the symbols the rules recognise, and the reference gates of the rule sets.
module Ranvier.Analyzers.Symbols

open System
open System.IO
open FSharp.Analyzers.SDK
open FSharp.Compiler.Symbols

/// The full name of the entity that declares a member or, for an extension member, the entity it extends; empty for a
/// local value.
let enclosing (m: FSharpMemberOrFunctionOrValue) : string =
    match m.ApparentEnclosingEntity with
    | Some entity -> defaultArg entity.TryFullName ""
    | None -> ""

/// Whether `m` is the member or function `name` of `entity`.
let isMember (entity: string) (name: string) (m: FSharpMemberOrFunctionOrValue) : bool =
    m.LogicalName = name && enclosing m = entity

/// The name a message gives `m`: the type's name for a constructor, the display name otherwise.
let display (m: FSharpMemberOrFunctionOrValue) : string =
    match m.ApparentEnclosingEntity with
    | Some entity when m.IsConstructor -> entity.DisplayName
    | _ -> m.DisplayName

[<Literal>]
let Operators = "Microsoft.FSharp.Core.Operators"

[<Literal>]
let PipeRight = "op_PipeRight"

[<Literal>]
let PipeLeft = "op_PipeLeft"

[<Literal>]
let CompilerGenerated = "System.Runtime.CompilerServices.CompilerGeneratedAttribute"

/// The prefix of a generated union case tester's getter: `get_IsQueue` tests `Queue`.
[<Literal>]
let TesterPrefix = "get_Is"

/// The names of the equality, hashing and comparison members the compiler generates for a type.
let generatedComparisons = set [ "CompareTo"; "Equals"; "GetHashCode" ]

/// The logical path of a module, namespace or type: its access path and its display name.
let pathOf (entity: FSharpEntity) : string =
    match entity.AccessPath with
    | null
    | ""
    | "global" -> entity.DisplayName
    | access -> access + "." + entity.DisplayName

let private referencedFiles (options: AnalyzerProjectOptions) : string seq =
    options.OtherOptions
    |> Seq.choose (fun option ->
        if option.StartsWith ("-r:", StringComparison.Ordinal) then
            Some (Path.GetFileName (option.Substring 3))
        elif option.StartsWith ("--reference:", StringComparison.Ordinal) then
            Some (Path.GetFileName (option.Substring 12))
        else
            None)

/// Whether the project's `-r:` options name the assembly file `assembly`.
let references (assembly: string) (options: AnalyzerProjectOptions) : bool =
    referencedFiles options
    |> Seq.exists (fun file -> String.Equals (file, assembly, StringComparison.OrdinalIgnoreCase))

/// The assembly files whose reference enables a rule set.
module Assemblies =
    [<Literal>]
    let Ranvier = "Ranvier.dll"

    [<Literal>]
    let View = "Ranvier.View.Portable.dll"

    [<Literal>]
    let Foundation = "Ranvier.View.Foundation.dll"

    [<Literal>]
    let Fable = "Fable.Core.dll"
