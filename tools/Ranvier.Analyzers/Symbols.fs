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

/// The core node types, their creators and their pure contexts.
module Core =
    [<Literal>]
    let Api = "Ranvier.Api"

    let private valueTypes =
        set
            [
                "Ranvier.Signal`1"
                "Ranvier.AsyncSource`1"
                "Ranvier.Memo`1"
                "Ranvier.AsyncMemo`1"
                "Ranvier.Boundary`1"
                "Ranvier.Editable`1"
            ]

    let private keyedTypes = set [ "Ranvier.Projection`2"; "Ranvier.Lookup`2" ]

    /// Whether `m` is a tracked read: `Value`, `TryValue` or `Status` of a node, `Get`, `TryGet` or `Keys` of a
    /// projection or lookup. `Peek` is untracked.
    let isReactiveRead (m: FSharpMemberOrFunctionOrValue) : bool =
        let entity = enclosing m

        (valueTypes.Contains entity
         && (m.LogicalName = "get_Value"
             || m.LogicalName = "get_TryValue"
             || m.LogicalName = "get_Status"))
        || (keyedTypes.Contains entity
            && (m.LogicalName = "Get"
                || m.LogicalName = "TryGet"
                || m.LogicalName = "get_Keys"))

    let private creatorFunctions =
        set
            [
                "createMemo"
                "createMemoWithComparer"
                "createMemoWith"
                "createOwningMemoWithComparer"
                "createEffect"
                "createEffectOn"
                "createEffectOnWithComparer"
                "createAsync"
                "createAsyncWith"
                "createAsyncWithPolicy"
                "createOwningAsyncWithPolicy"
                "createAsyncSource"
                "createStream"
                "createStreamFold"
                "createSuspense"
                "createSuspenseWithComparer"
                "createErrorBoundary"
                "createErrorBoundaryWithComparer"
                "createBoundary"
                "createBoundaryWithComparer"
                "createRoot"
                "createProjection"
                "createProjectionWith"
                "createProjectionWithOptions"
                "createProjectionOnce"
                "createIndexProjection"
                "createIndexProjectionWith"
                "createLookup"
                "createSelector"
                "createOptionMemo"
                "onCleanup"
            ]

    let private creatorTypes =
        set [ "Ranvier.Memo`1"; "Ranvier.Effect"; "Ranvier.AsyncMemo`1" ]

    /// Whether a call of `m` creates an owned node.
    let isCreator (m: FSharpMemberOrFunctionOrValue) : bool =
        (enclosing m = Api
         && creatorFunctions.Contains m.LogicalName)
        || (m.IsConstructor
            && creatorTypes.Contains (enclosing m))

    /// Whether the lambda argument of a call of `m` runs in the caller's run: `untrack`, `batch`, and the `Delay` of a
    /// task expression.
    let isTransparent (m: FSharpMemberOrFunctionOrValue) : bool =
        (enclosing m = Api
         && (m.LogicalName = "untrack"
             || m.LogicalName = "batch"))
        || (m.LogicalName = "Delay"
            && enclosing m = "Microsoft.FSharp.Control.TaskBuilderBase")

    /// How a pure context fails when its body creates an owned node.
    [<RequireQualifiedAccess>]
    type Pure =
        | Memo
        | Async
        | EffectOn
        | Map
        | Reader
        | LookupF
        | LookupAffected
        | Seed
        | Aggregate

    /// A lambda argument that runs as a pure derivation: its kind, the lambdas before its body, and the owning
    /// function that replaces the callee.
    type PureContext =
        {
            Kind: Pure
            Layers: int
            Owning: string option
        }

    let private pureFunctions: Map<string * string * int, PureContext> =
        let context kind layers owning =
            {
                Kind = kind
                Layers = layers
                Owning = owning
            }

        Map.ofList
            [
                (Api, "createMemo", 0), context Pure.Memo 1 (Some "createMemoWith")
                (Api, "createMemoWithComparer", 1), context Pure.Memo 1 (Some "createOwningMemoWithComparer")
                (Api, "createAsync", 0), context Pure.Async 2 (Some "createAsyncWith")
                (Api, "createAsyncWithPolicy", 1), context Pure.Async 2 (Some "createOwningAsyncWithPolicy")
                (Api, "createEffectOn", 0), context Pure.EffectOn 1 None
                (Api, "createEffectOnWithComparer", 1), context Pure.EffectOn 1 None
                (Api, "createProjection", 1), context Pure.Map 1 None
                (Api, "createIndexProjection", 0), context Pure.Map 1 None
                (Api, "createProjectionWith", 1), context Pure.Reader 2 None
                (Api, "createIndexProjectionWith", 0), context Pure.Reader 2 None
                (Api, "createLookup", 0), context Pure.LookupF 2 None
                (Api, "createLookup", 1), context Pure.LookupAffected 2 None
                (Api, "createEditable", 0), context Pure.Seed 1 None
                (Api, "createDraft", 0), context Pure.Seed 1 None
                ("Ranvier.ProjectionModule", "foldGroup", 0), context Pure.Aggregate 2 None
                ("Ranvier.ProjectionModule", "foldGroup", 1), context Pure.Aggregate 2 None
                ("Ranvier.ProjectionModule", "fold", 0), context Pure.Aggregate 2 None
                ("Ranvier.ProjectionModule", "foldGroupBy", 1), context Pure.Aggregate 2 None
                ("Ranvier.ProjectionModule", "foldGroupBy", 2), context Pure.Aggregate 2 None
            ]

    let private owningConstructor (m: FSharpMemberOrFunctionOrValue) =
        m.CurriedParameterGroups
        |> Seq.concat
        |> Seq.exists (fun parameter ->
            parameter.Type.HasTypeDefinition
            && parameter.Type.TypeDefinition.TryFullName = Some "System.Boolean")

    /// The pure context that argument `index` of `count` arguments of a call of `m` is.
    let pureContext (m: FSharpMemberOrFunctionOrValue) (index: int) (count: int) : PureContext option =
        if m.IsConstructor then
            match enclosing m with
            | "Ranvier.Memo`1" when index = count - 1 && not (owningConstructor m) ->
                Some
                    {
                        Kind = Pure.Memo
                        Layers = 1
                        Owning = None
                    }
            | "Ranvier.AsyncMemo`1" when index = count - 1 && not (owningConstructor m) ->
                Some
                    {
                        Kind = Pure.Async
                        Layers = 2
                        Owning = None
                    }
            | _ -> None
        else
            pureFunctions
            |> Map.tryFind (enclosing m, m.LogicalName, index)

    /// The index of the compute argument of an async value's creator, when `m` is one; `count` is the call's
    /// argument count.
    let asyncComputeIndex (m: FSharpMemberOrFunctionOrValue) (count: int) : int option =
        if
            m.IsConstructor
            && enclosing m = "Ranvier.AsyncMemo`1"
        then
            Some (count - 1)
        elif enclosing m = Api then
            match m.LogicalName with
            | "createAsync"
            | "createAsyncWith" -> Some 0
            | "createAsyncWithPolicy"
            | "createOwningAsyncWithPolicy" -> Some 1
            | _ -> None
        else
            None
