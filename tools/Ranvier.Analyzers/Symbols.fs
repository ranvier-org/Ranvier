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

/// The case-name prefix of a tester property: `IsQueue` tests `Queue`.
[<Literal>]
let TesterProperty = "Is"

/// The prefix of a generated union case tester's getter: `get_IsQueue` tests `Queue`.
[<Literal>]
let TesterPrefix = "get_" + TesterProperty

/// The names of the equality, hashing and comparison members the compiler generates for a type.
let generatedComparisons = set [ "CompareTo"; "Equals"; "GetHashCode" ]

/// The path that source code names a module, namespace or type by: its namespace, then the display names of its
/// enclosing modules and types. A module compiled with the `Module` suffix keeps its source name.
let rec pathOf (entity: FSharpEntity) : string =
    if entity.IsNamespace then
        match entity.AccessPath with
        | null
        | ""
        | "global" -> entity.DisplayName
        | access -> access + "." + entity.DisplayName
    else
        match entity.DeclaringEntity with
        | Some parent -> pathOf parent + "." + entity.DisplayName
        | None ->
            match entity.Namespace with
            | Some space when space <> "" -> space + "." + entity.DisplayName
            | _ -> entity.DisplayName

/// The source path of the module, namespace or type that declares `entity`; empty at the global level.
let containerOf (entity: FSharpEntity) : string =
    match entity.DeclaringEntity with
    | Some parent -> pathOf parent
    | None -> defaultArg entity.Namespace ""

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

    let private ownerFunctions: Map<string * int, int> =
        Map.ofList
            [
                ("createSuspense", 1), 1
                ("createSuspenseWithComparer", 2), 1
                ("createErrorBoundary", 1), 1
                ("createErrorBoundaryWithComparer", 2), 1
                ("createBoundary", 2), 1
                ("createBoundaryWithComparer", 3), 1
                ("createMemoWith", 0), 1
                ("createOwningMemoWithComparer", 1), 1
                ("createEffect", 0), 1
                ("createAsyncWith", 0), 2
                ("createOwningAsyncWithPolicy", 1), 2
            ]

    /// The lambdas before the body when argument `index` of `count` arguments of a call of `m` is an owner body: a
    /// body that disposes the nodes it created before each re-run.
    let ownerBody (m: FSharpMemberOrFunctionOrValue) (index: int) (count: int) : int option =
        if
            m.IsConstructor
            && enclosing m = "Ranvier.Effect"
            && index = count - 1
        then
            Some 1
        elif enclosing m = Api then
            ownerFunctions
            |> Map.tryFind (m.LogicalName, index)
        else
            None

    /// Whether a call of `m` creates an async value: an async memo, an async source or a stream.
    let isAsyncCreator (m: FSharpMemberOrFunctionOrValue) : bool =
        (m.IsConstructor
         && enclosing m = "Ranvier.AsyncMemo`1")
        || (enclosing m = Api
            && (m.LogicalName.StartsWith "createAsync"
                || m.LogicalName.StartsWith "createStream"
                || m.LogicalName = "createOwningAsyncWithPolicy"))

    /// Whether a call of `m` writes a signal or pumps the graph: work the graph's owning thread must do.
    let isGraphWrite (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember "Ranvier.Signal`1" "set_Value" m
        || isMember "Ranvier.SignalModule" "update" m
        || isMember "Ranvier.Graph" "Pump" m

    /// Whether `m` is `Graph.Dispatch`, which runs its work on the owning thread.
    let isDispatch (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember "Ranvier.Graph" "Dispatch" m

    /// Whether a call of `m` runs its delegate argument on a thread-pool or new thread.
    let threadStarter (m: FSharpMemberOrFunctionOrValue) : bool =
        match enclosing m, m.LogicalName with
        | "System.Threading.Tasks.Task", "Run"
        | "System.Threading.Tasks.TaskFactory", "StartNew"
        | "System.Threading.ThreadPool", "QueueUserWorkItem"
        | "System.Threading.ThreadPool", "UnsafeQueueUserWorkItem"
        | "System.Threading.Thread", ".ctor"
        | "System.Threading.Tasks.Parallel", "For"
        | "System.Threading.Tasks.Parallel", "ForEach"
        | "System.Threading.Tasks.Parallel", "Invoke"
        | "System.Threading.Tasks.Task", "ContinueWith"
        | "System.Threading.Tasks.Task`1", "ContinueWith"
        | "System.Threading.Timer", ".ctor" -> true
        | _ -> false

    /// Whether `m` is a computation-expression bind, whose last argument is the continuation after an await:
    /// `Bind`, `BindReturn`, `Bind2` and the like.
    let isBind (m: FSharpMemberOrFunctionOrValue) : bool =
        m.LogicalName.StartsWith "Bind"

    /// Whether `m` is a computation-expression `Combine`, whose second argument runs after its first.
    let isCombine (m: FSharpMemberOrFunctionOrValue) : bool =
        m.LogicalName = "Combine"

    /// Whether `m` constructs a graph.
    let isGraphConstructor (m: FSharpMemberOrFunctionOrValue) : bool =
        m.IsConstructor && enclosing m = "Ranvier.Graph"

    /// Whether `m` constructs a node over the graph it takes first: a signal, an async source or a memo.
    let isNodeConstructor (m: FSharpMemberOrFunctionOrValue) : bool =
        m.IsConstructor
        && (match enclosing m with
            | "Ranvier.Signal`1"
            | "Ranvier.AsyncSource`1"
            | "Ranvier.Memo`1"
            | "Ranvier.AsyncMemo`1" -> true
            | _ -> false)

    /// Whether `m` is `untrack`.
    let isUntrack (m: FSharpMemberOrFunctionOrValue) : bool =
        isMember Api "untrack" m

/// The view layer's builders, sugar and definitions.
module View =
    [<Literal>]
    let Builder = "Ranvier.View.Foundation.Template.TemplateBuilder`1"

    [<Literal>]
    let Attributes = "Ranvier.View.Foundation.Template.AttributeBuilder`1"

    [<Literal>]
    let Properties = "Ranvier.View.Foundation.Template.PropertyBuilder`1"

    [<Literal>]
    let Hooks = "Ranvier.View.Foundation.Template.HookBuilder`1"

    [<Literal>]
    let Unsafe = "Ranvier.View.Foundation.Template.UnsafeBuilder`1"

    [<Literal>]
    let Component = "Ranvier.View.Foundation.Template.Component"

    [<Literal>]
    let ComponentType = "Ranvier.View.Foundation.Template.Component`1"

    [<Literal>]
    let Sugar = "Ranvier.View.Foundation.Sugar.Builders"

    [<Literal>]
    let SugarComponent = "Ranvier.View.Foundation.Sugar.Builders.Component"

    [<Literal>]
    let Element = "Ranvier.View.Foundation.Sugar.Builders.Element`2"

    [<Literal>]
    let Elements = "Ranvier.View.Foundation.Sugar.Elements"

    [<Literal>]
    let Source = "Ranvier.View.Foundation.Template.Source`2"

    [<Literal>]
    let Hydration = "Ranvier.View.Foundation.Hydration"

    [<Literal>]
    let Microtask = "Ranvier.View.Foundation.Microtask"

    /// Whether argument `index` of a call of `m` is read per region read: the arms of `matchWith` and `branch`, the
    /// key and items of `each`, `forEach` and `forEachFixed`.
    let isRegionLambda (m: FSharpMemberOrFunctionOrValue) (index: int) : bool =
        let entity = enclosing m

        (entity = Builder || entity = Sugar)
        && (match m.LogicalName, index with
            | ("matchWith" | "branch"), 1 -> true
            | ("each" | "forEach" | "forEachFixed"), (0 | 1) -> true
            | _ -> false)
