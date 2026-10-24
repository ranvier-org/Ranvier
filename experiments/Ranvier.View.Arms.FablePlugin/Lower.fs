/// <summary>The lowering of an Arms.Match call into a target selector and one case body per target.</summary>
module internal Ranvier.View.Arms.FablePlugin.Lower

open Fable
open Fable.AST
open Fable.AST.Fable
open Ranvier.View.Arms

let ident (name: string) (t: Type) : Ident =
    {
        Name = name
        Type = t
        IsMutable = false
        IsThisArgument = false
        IsCompilerGenerated = true
        Range = None
        IsInlineIfLambda = false
    }

let private int32 (value: int) =
    Value (NumberConstant (NumberValue.Int32 value, NumberInfo.Empty), None)

let private emit (macro: string) (args: Expr list) (r: SourceLocation option) =
    Emit (
        {
            Macro = macro
            IsStatement = false
            CallInfo = CallInfo.Create (args = args)
        },
        Any,
        r
    )

/// <summary>An F# rendering of a type, for diagnostics.</summary>
let rec show (helper: PluginHelper) (t: Type) : string =
    match t with
    | Type.Unit -> "unit"
    | Type.Boolean -> "bool"
    | Type.Char -> "char"
    | Type.String -> "string"
    | Type.Number (kind, _) ->
        Diagnostics.numberName (
            match kind with
            | NumberKind.Int8 -> "SByte"
            | NumberKind.UInt8 -> "Byte"
            | NumberKind.BigInt -> "BigInteger"
            | NumberKind.NativeInt -> "IntPtr"
            | NumberKind.UNativeInt -> "UIntPtr"
            | NumberKind.Float32 -> "Single"
            | NumberKind.Float64 -> "Double"
            | other -> string other
        )
    | Type.Option (arg, false) -> atom helper arg + " option"
    | Type.Option (arg, true) -> atom helper arg + " voption"
    | Type.Array (arg, _) -> atom helper arg + " array"
    | Type.List arg -> atom helper arg + " list"
    | Type.Tuple (args, _) ->
        args
        |> List.map (atom helper)
        |> String.concat " * "
    | Type.LambdaType (domain, range) -> atom helper domain + " -> " + show helper range
    | Type.DeclaredType (ref, []) -> (helper.GetEntity ref).DisplayName
    | Type.DeclaredType (ref, args) ->
        (helper.GetEntity ref).DisplayName
        + "<"
        + (args
           |> List.map (show helper)
           |> String.concat ", ")
        + ">"
    | Type.GenericParam (name, _, _) -> "'" + name
    | _ -> "obj"

and private atom (helper: PluginHelper) (t: Type) =
    match t with
    | Type.LambdaType _
    | Type.Tuple _ -> "(" + show helper t + ")"
    | _ -> show helper t

/// <summary>Whether a value of the type is a JavaScript primitive or a .NET value type (AM004).</summary>
let private notObject (helper: PluginHelper) (t: Type) =
    match t with
    | Type.Unit
    | Type.Boolean
    | Type.Char
    | Type.String
    | Type.Number _
    | Type.Option _
    | Type.Tuple (_, true)
    | Type.AnonymousRecordType (_, _, true) -> true
    | Type.DeclaredType (ref, _) ->
        let entity = helper.GetEntity ref
        entity.IsValueType || entity.IsEnum
    | _ -> false

let private raising =
    set
        [
            "Microsoft.FSharp.Core.Operators.raise"
            "Microsoft.FSharp.Core.Operators.failwith"
            "Microsoft.FSharp.Core.Operators.invalidOp"
            "Microsoft.FSharp.Core.Operators.invalidArg"
            "Microsoft.FSharp.Core.Operators.nullArg"
            "Microsoft.FSharp.Core.ExtraTopLevelOperators.failwithf"
        ]

/// <summary>Whether a case body is a raised exception, which builds no definition.</summary>
let rec private isThrow (helper: PluginHelper) (expr: Expr) =
    match expr with
    | Extended (Throw _, _) -> true
    | Call (_, { MemberRef = Some ref }, _, _) -> raising.Contains (helper.GetMember ref).FullName
    | Import (info, _, _) -> info.Selector = "toFail"
    | Let (id, value, _) when id.IsCompilerGenerated -> isThrow helper value
    | Call (f, _, _, _)
    | CurriedApply (f, _, _, _)
    | TypeCast (f, _) -> isThrow helper f
    | _ -> false

/// <summary>The raising body where the selector returns a target: a throw retyped, any other raise followed by null.</summary>
let private raiseAs (t: Type) (body: Expr) =
    match body with
    | Extended (Throw (e, _), r) -> Extended (Throw (e, t), r)
    | _ -> Sequential [ body; Value (Null t, None) ]

/// <summary>Whether <c>value</c> is a field, tuple item or case payload of a controlled identifier.</summary>
let rec private extracts (control: Set<string>) (value: Expr) =
    match value with
    | IdentExpr id -> control.Contains id.Name
    | Get (inner, (UnionField _ | TupleIndex _ | FieldGet _ | OptionValue | ListHead | ListTail), _, _)
    | TypeCast (inner, _) -> extracts control inner
    | _ -> false

/// <summary>
/// Whether a <c>let</c> belongs to the selection: its value refers to a controlled identifier outside every lambda, or,
/// for a compiler-generated identifier, extracts a part of one.
/// </summary>
let private decides (control: Set<string>) (id: Ident) (value: Expr) =
    if id.IsCompilerGenerated then
        extracts control value
    else
        Shape.refersOutsideLambdas control value

/// <summary>Rebuilds the decision part of <c>tree</c>, replacing each leaf by <c>leaf bound body</c> in walk order.</summary>
let mapLeaves (control: Set<string>) (leaf: Ident list -> Expr -> Expr) (tree: Expr) : Expr =
    let rec walk (control: Set<string>) (bound: Ident list) (expr: Expr) =
        match expr with
        | DecisionTree (decision, targets) ->
            DecisionTree (
                decision,
                [
                    for ids, body in targets -> ids, walk (control + set [ for id in ids -> id.Name ]) (bound @ ids) body
                ]
            )
        | IfThenElse (condition, whenTrue, whenFalse, r) when Shape.refersOutsideLambdas control condition ->
            let whenTrue = walk control bound whenTrue
            IfThenElse (condition, whenTrue, walk control bound whenFalse, r)
        | Let (id, value, body) when decides control id value -> Let (id, value, walk (control.Add id.Name) (bound @ [ id ]) body)
        | body -> leaf bound body

    walk control [] tree

/// <summary>The range of a decision node that a wrapper at the root of a leaf hides from the walk (AM007).</summary>
let rec private hidden (control: Set<string>) (expr: Expr) : SourceLocation option option =
    match expr with
    | IfThenElse (condition, _, _, r) when Shape.refersOutsideLambdas control condition -> Some r
    | Let (id, value, _) when decides control id value -> Some id.Range
    | DecisionTree (decision, _) when Shape.refersOutsideLambdas control decision -> Some None
    | TypeCast (inner, _)
    | TryCatch (inner, _, _, _)
    | Let (_, _, inner) -> hidden control inner
    | Sequential xs when not xs.IsEmpty -> hidden control (List.last xs)
    | _ -> None

/// <summary>
/// The suffix of a site's emitted names: the line and column of the first available range, in the order given (the
/// call, its arms, its read).
/// </summary>
let siteName (ranges: SourceLocation option list) : string option =
    ranges
    |> List.tryPick id
    |> Option.map (fun r -> sprintf "L%dC%d" r.start.line r.start.column)

/// <summary>
/// Whether a lambda is the compiler's eta-expansion of a function value, <c>fun x -> f x</c>, which Fable passes for a
/// named arms function (AM003).
/// </summary>
let private isEtaExpansion (expr: Expr) =
    match expr with
    | Lambda (argument, CurriedApply (f, [ IdentExpr passed ], _, _), _)
    | Lambda (argument, Call (f, { Args = [ IdentExpr passed ] }, _, _), _) ->
        argument.IsCompilerGenerated
        && passed.Name = argument.Name
        && not (Shape.refersTo (Set.singleton argument.Name) f)
    | _ -> false

/// <summary>The first parameter of a lambda or delegate.</summary>
let private firstParameter (expr: Expr) : Ident option =
    match expr with
    | Lambda (argument, _, _)
    | Delegate (argument :: _, _, _, _) -> Some argument
    | _ -> None

/// <summary>
/// The type of a rewritten reader's parameter, the values thunk of an arm of a site over <c>instanceType</c>. The
/// element type records the site's instance type.
/// </summary>
let private thunkType (instanceType: Type) =
    Type.LambdaType (Type.Unit, Type.Array (instanceType, MutableArray))

/// <summary>The instance type of the site whose rewritten reader takes the parameter.</summary>
let private (|ThunkOver|_|) (id: Ident) : Type option =
    match id.Type with
    | Type.LambdaType (Type.Unit, Type.Array (instanceType, _)) when id.Name.StartsWith "armsArm_" -> Some instanceType
    | _ -> None

/// <summary>
/// A reader parameter rewritten by a site over <c>instanceType</c> (<c>armsArm_[tag]_[index]..._[index]</c>): the path of
/// instance indexes from the parameter to the values of the innermost site that has not rewritten the reader, which
/// <c>p ()[i1] ()[i2] ()...</c> reaches.
/// </summary>
let private (|Thunk|_|) (instanceType: Type) (id: Ident) : int list option =
    match id with
    | ThunkOver over when over = instanceType ->
        let indexes =
            id.Name.Split '_'
            |> Array.skip 2
            |> Array.map System.Int32.TryParse

        if indexes.Length > 0 && Array.forall fst indexes then
            Some [ for _, index in indexes -> index ]
        else
            None
    | _ -> None

/// <summary>The JavaScript that reaches the values array at the end of a thunk parameter's path from <c>$0</c>.</summary>
let private pathMacro (path: int list) =
    "$0()"
    + String.concat "" [ for index in path -> sprintf "[%d]()" index ]

/// <summary>
/// The binding of a reader's instance by a site nested in a case body: <c>let x = armsRead_[site][index]</c>, where
/// <c>x</c> holds the values thunk of the enclosing arm.
/// </summary>
let private (|HeldInstance|_|) (instanceType: Type) (expr: Expr) : (Ident * Ident * Expr * Expr) option =
    match expr with
    | Let (id, Get (IdentExpr values, ExprGet index, _, _), body) when
        id.Type = instanceType
        && values.Name.StartsWith "armsRead_"
        ->
        Some (id, values, index, body)
    | _ -> None

/// <summary>Whether a value of the type is a function over the instance, such as a reader passed by name.</summary>
let private isReaderType (instanceType: Type) (t: Type) =
    match t with
    | Type.LambdaType (domain, _) -> domain = instanceType
    | _ -> false

let private isHeldInstance (instanceType: Type) (expr: Expr) =
    (|HeldInstance|_|) instanceType expr
    |> Option.isSome

let private bodyOf (expr: Expr) =
    match expr with
    | Lambda (_, body, _)
    | Delegate (_, body, _, _) -> body
    | other -> other

let private withBody (expr: Expr) (body: Expr) =
    match expr with
    | Lambda (argument, _, n) -> Lambda (argument, body, n)
    | Delegate (args, _, n, tags) -> Delegate (args, body, n, tags)
    | other -> other

let private withParameter (expr: Expr) (parameter: Ident) (body: Expr) =
    match expr with
    | Lambda (_, _, n) -> Lambda (parameter, body, n)
    | Delegate (_ :: rest, _, n, tags) -> Delegate (parameter :: rest, body, n, tags)
    | other -> other

/// <summary>Whether an identifier is a computation expression's builder binding (<c>builder@</c>).</summary>
let private isBuilder (id: Ident) =
    id.Name.Contains "@" || id.Name.Contains "$0040"

/// <summary>A pattern-variable reference left outside every reader, with the lambda it sits in.</summary>
type private Found =
    {
        Ident: Ident
        Field: (UnionFieldInfo * SourceLocation option) option
        Over: Type option
    }

let rec private find (names: Set<string>) (over: Type option) (expr: Expr) : Found option =
    match expr with
    | Get (IdentExpr id, UnionField info, _, r) when names.Contains id.Name ->
        Some
            {
                Ident = id
                Field = Some (info, r)
                Over = over
            }
    | IdentExpr id when names.Contains id.Name ->
        Some
            {
                Ident = id
                Field = None
                Over = over
            }
    | Lambda (argument, body, _) when argument.Type = Type.Unit || isBuilder argument -> find names over body
    | Lambda (ThunkOver instanceType, body, _)
    | Delegate (ThunkOver instanceType :: _, body, _, _) -> find names (Some instanceType) body
    | Delegate (instance :: argument :: _, body, _, _) when instance.Name.StartsWith "armsInstance_" -> find names (Some argument.Type) body
    | Lambda (argument, body, _) -> find names (Some argument.Type) body
    | Delegate (argument :: _, body, _, _) -> find names (Some argument.Type) body
    | _ ->
        fst (Shape.children expr)
        |> List.tryPick (find names over)

/// <summary>Logs AM001, AM001b or AM002 for a reference outside every reader.</summary>
let private report (helper: PluginHelper) (matched: Ident) (instanceType: Type) (fallback: SourceLocation option) (found: Found) =
    let at (r: SourceLocation option) =
        match r with
        | Some _ -> r
        | None -> fallback

    let field =
        match found.Field with
        | Some (info, r) when found.Ident.Name = matched.Name ->
            let case =
                (helper.GetEntity info.Entity).UnionCases
                |> List.item info.CaseIndex

            Some ((case.UnionCaseFields |> List.item info.FieldIndex).Name, case.Name, r)
        | _ -> None

    match found.Over, field with
    | Some over, Some (name, _, r) -> helper.LogError (Diagnostics.otherLambda name (show helper over) (show helper instanceType), ?range = at r)
    | Some over, None ->
        helper.LogError (Diagnostics.otherLambda found.Ident.DisplayName (show helper over) (show helper instanceType), ?range = at found.Ident.Range)
    | None, Some (name, case, r) -> helper.LogError (Diagnostics.fieldOutsideReader name case, ?range = at r)
    | None, None -> helper.LogError (Diagnostics.outsideReader found.Ident.DisplayName, ?range = at found.Ident.Range)

/// <summary>
/// The lowered argument of a call, the selector over (instance, value) and the case bodies; or None after reporting AM003
/// or AM004.
/// </summary>
let site (helper: PluginHelper) (splice: Expr -> Expr -> Expr) (read: Expr) (arms: Expr) (r: SourceLocation option) : Expr option =
    let instanceType =
        match read.Type with
        | LambdaType (instance, _) -> instance
        | other -> other

    match arms, siteName [ r; arms.Range; read.Range ] with
    | _ when notObject helper instanceType ->
        helper.LogError (Diagnostics.notObject (show helper instanceType), ?range = r)
        None
    | _ when isEtaExpansion arms ->
        helper.LogError (Diagnostics.notLiteral, ?range = r)
        None
    | Lambda _, None ->
        helper.LogError Diagnostics.noRange
        None
    | Lambda (matched, tree, _), Some name ->
        let control = Set.singleton matched.Name
        let leaves = System.Collections.Generic.List<Ident list * Expr>()

        mapLeaves
            control
            (fun bound body ->
                leaves.Add (bound, body)
                body)
            tree
        |> ignore

        // The target of each leaf; a raising leaf has none.
        let targets =
            leaves
            |> Seq.fold
                (fun (acc, next) (_, body) ->
                    if isThrow helper body then
                        None :: acc, next
                    else
                        Some next :: acc, next + 1)
                ([], 0)
            |> fst
            |> List.rev
            |> Array.ofList

        let instance = ident ("armsInstance_" + name) Any
        let valuesType = Type.Array (Any, MutableArray)

        let selector =
            let position = ref 0

            let pick (bound: Ident list) (body: Expr) =
                let leaf = position.Value
                position.Value <- leaf + 1

                match targets[leaf] with
                | None -> raiseAs valuesType body
                | Some target ->
                    let values =
                        int32 target
                        :: [ for id in bound @ [ matched; instance ] -> IdentExpr id ]

                    Value (NewArray (ArrayValues values, Any, MutableArray), None)

            Delegate ([ instance; matched ], mapLeaves control pick tree, None, [])

        // A tag per rewritten function: its emitted names stay distinct when Fable inlines one function into another.
        let tags = ref 0

        let nextTag () =
            tags.Value <- tags.Value + 1
            sprintf "%sn%d" name tags.Value

        // The thunk parameters this site created.
        let own = System.Collections.Generic.HashSet<string>()

        let select = ident ("armsSelect_" + name) Any
        let reader = ident ("armsReader_" + name) Any
        let recomputed = ref false

        let isView (t: Type) =
            match t with
            | Type.DeclaredType (ref, _) -> (helper.GetEntity ref).FullName.StartsWith "Ranvier.View"
            | _ -> false

        let isViewMember (info: CallInfo) =
            info.MemberRef
            |> Option.exists (fun ref -> (helper.GetMember ref).FullName.StartsWith "Ranvier.View")

        let rec peel (expr: Expr) =
            match expr with
            | TypeCast (inner, _) -> peel inner
            | _ -> expr

        let isInstanceLambda (expr: Expr) =
            match firstParameter (peel expr) with
            | Some argument -> argument.Type = instanceType
            | None -> false

        // A call outside Ranvier.View with a non-view result: it calls the lambdas it receives with values of its own.
        let isPlainCall (expr: Expr) =
            match expr with
            | Call (_, info, t, _) -> not (isViewMember info || isView t)
            | CurriedApply (_, _, t, _) -> not (isView t)
            | _ -> false

        // A call of a helper whose view is over the concrete instance type rather than a type parameter.
        let isConcreteHelper (expr: Expr) =
            match expr with
            | Call (_, ({ MemberRef = Some ref } as info), t, _) when t = tree.Type && not (isViewMember info) ->
                match (helper.GetMember ref).ReturnParameter.Type with
                | Type.DeclaredType (_, [ Type.GenericParam _ ]) -> false
                | _ -> true
            | _ -> false

        let rename (from: Ident) (target: Ident) (expr: Expr) =
            expr
            |> Shape.map (function
                | IdentExpr id when id.Name = from.Name -> Some (IdentExpr target)
                | _ -> None)

        let rewrite (bound: Ident list) (body: Expr) =
            let slots = bound @ [ matched ]
            let names = set [ for id in slots -> id.Name ]
            let last = slots.Length + 1

            // The reader body with the instance and the slots it uses bound from the array of source, or None when it
            // uses neither.
            let opened (tag: string) (source: SourceLocation option -> Expr) (held: Ident option) (inner: Expr) : Expr option =
                let values = ident ("armsRead_" + tag) Any

                let instanceUsed =
                    held
                    |> Option.filter (fun id -> Shape.refersTo (Set.singleton id.Name) inner)

                let used =
                    slots
                    |> List.indexed
                    |> List.filter (fun (_, id) -> Shape.refersTo (Set.singleton id.Name) inner)

                if instanceUsed.IsNone && used.IsEmpty then
                    None
                else
                    let fresh =
                        [
                            for index, id in used -> index, id, ident (sprintf "%s_%s" id.Name tag) id.Type
                        ]

                    let renamed =
                        inner
                        |> Shape.map (function
                            | IdentExpr id ->
                                fresh
                                |> List.tryFind (fun (_, slot, _) -> slot.Name = id.Name)
                                |> Option.map (fun (_, _, local) -> IdentExpr local)
                            | _ -> None)

                    let withSlots =
                        List.foldBack
                            (fun (index, _, local: Ident) acc ->
                                Let (local, Get (IdentExpr values, ExprGet (int32 (index + 1)), local.Type, None), acc))
                            fresh
                            renamed

                    let withInstance =
                        match instanceUsed with
                        | Some id -> Let (id, Get (IdentExpr values, ExprGet (int32 last), id.Type, None), withSlots)
                        | None -> withSlots

                    let range =
                        Shape.firstReference
                            (names
                             + set [ for id in Option.toList instanceUsed -> id.Name ])
                            inner
                        |> Option.bind _.Range

                    Some (Let (values, source range, withInstance))

            // A new thunk parameter of this site, whose path is the instance index of this site's values.
            let thunk () =
                let tag = nextTag ()
                let parameter = ident (sprintf "armsArm_%s_%d" tag last) (thunkType instanceType)
                own.Add parameter.Name |> ignore
                tag, parameter

            // Applications of the functions in excluded to the instance, given the expression of the arm's values thunk.
            let rec adapt (excluded: Set<string>) (instance: Ident) (values: Expr) (expr: Expr) =
                let isInstance (arg: Expr) =
                    match arg with
                    | IdentExpr id -> id.Name = instance.Name
                    | _ -> false

                let isAdapted (f: Ident) =
                    excluded.Contains f.Name
                    && isReaderType instanceType f.Type

                expr
                |> Shape.map (function
                    | CurriedApply (IdentExpr f as callee, arg :: rest, t, r) when isAdapted f && isInstance arg ->
                        Some (
                            CurriedApply (
                                callee,
                                values
                                :: List.map (adapt excluded instance values) rest,
                                t,
                                r
                            )
                        )
                    | Call (IdentExpr f as callee, ({ Args = arg :: rest } as info), t, r) when isAdapted f && isInstance arg ->
                        Some (
                            Call (
                                callee,
                                { info with
                                    Args =
                                        values
                                        :: List.map (adapt excluded instance values) rest
                                },
                                t,
                                r
                            )
                        )
                    | _ -> None)

            // A helper view value over the instance, built outside the body: a reference, or a call with no function
            // argument and no view built in the body.
            let rec spliceable (excluded: Set<string>) (view: Expr) =
                match view with
                | IdentExpr id -> not (excluded.Contains id.Name)
                | Import _
                | Get _ -> true
                | TypeCast (inner, _) -> spliceable excluded inner
                | Call _
                | CurriedApply _ ->
                    fst (Shape.children view)
                    |> List.forall (
                        Shape.exists (fun expr ->
                            match expr with
                            | Lambda _
                            | Delegate _ -> true
                            | IdentExpr id -> excluded.Contains id.Name
                            | expr -> isReaderType instanceType expr.Type)
                        >> not
                    )
                | _ -> false

            // The adapter of a splice: the instance of the arm's values thunk.
            let instanceOf (range: SourceLocation option) =
                let _, parameter = thunk ()
                Lambda (parameter, emit "$0()[$1]" [ IdentExpr parameter; int32 last ] range, None)

            // A lambda over the instance that a helper over the concrete instance type calls with the instance: it reads
            // the slots it uses from the selector's array for that instance.
            let recompute (lambda: Expr) =
                let inner = peel lambda

                match firstParameter inner with
                | Some argument ->
                    opened (nextTag ()) (emit "$0($1, $2($1))" [ IdentExpr select; IdentExpr argument; IdentExpr reader ]) None (bodyOf inner)
                    |> Option.map (fun body ->
                        recomputed.Value <- true
                        withBody inner body)
                    |> Option.defaultValue lambda
                | None -> lambda

            // Function values over the instance bound by the names in excluded already take the values thunk.
            let rec lower (excluded: Set<string>) (body: Expr) =
                body
                |> Shape.map (fun expr ->
                    match expr, firstParameter expr with
                    | HeldInstance instanceType (id, held, index, rest), _ ->
                        let values = Get (IdentExpr held, ExprGet index, Any, None)
                        opened (nextTag ()) (emit "$0[$1]()" [ IdentExpr held; index ]) (Some id) (adapt excluded id values rest)
                    | _, Some argument when argument.Type = instanceType ->
                        let tag, thunk = thunk ()
                        let inner = adapt excluded argument (IdentExpr thunk) (bodyOf expr)

                        opened tag (emit "$0()" [ IdentExpr thunk ]) (Some argument) inner
                        |> Option.defaultValue inner
                        |> withParameter expr thunk
                        |> Some
                    | _, Some (Thunk instanceType path as foreign) when
                        not (own.Contains foreign.Name)
                        && not (Shape.exists (isHeldInstance instanceType) (bodyOf expr))
                        ->
                        // The parameter's path grows by this site's instance index, for the sites enclosing this one.
                        let tag = nextTag ()

                        let parameter =
                            ident (sprintf "armsArm_%s_%s" tag (String.concat "_" [ for index in path @ [ last ] -> string index ])) foreign.Type

                        own.Add parameter.Name |> ignore
                        let inner = rename foreign parameter (bodyOf expr)

                        opened tag (emit (pathMacro path) [ IdentExpr parameter ]) None inner
                        |> Option.defaultValue inner
                        |> withParameter expr parameter
                        |> Some
                    | Let (id, value, rest), _ when isReaderType instanceType id.Type ->
                        // A function that reads a slot takes the values thunk; any other keeps the instance.
                        if Shape.refersTo names value then
                            Some (Let (id, lower excluded value, lower (excluded.Add id.Name) rest))
                        else
                            Some (Let (id, value, lower excluded rest))
                    | CurriedApply (f, args, t, r), _ when isReaderType instanceType f.Type ->
                        Some (CurriedApply (f, List.map (lower excluded) args, t, r))
                    | Call (f, info, t, r), _ when isReaderType instanceType f.Type ->
                        Some (
                            Call (
                                f,
                                { info with
                                    ThisArg = Option.map (lower excluded) info.ThisArg
                                    Args = List.map (lower excluded) info.Args
                                },
                                t,
                                r
                            )
                        )
                    | IdentExpr id, _ when excluded.Contains id.Name -> Some expr
                    | Let (id, value, rest), _ when id.Type = tree.Type -> Some (Let (id, lower excluded value, lower (excluded.Add id.Name) rest))
                    | call, _ when isConcreteHelper call ->
                        let args, rebuild = Shape.children call

                        let lowered =
                            [
                                for arg in args ->
                                    if isInstanceLambda arg then
                                        recompute arg
                                    else
                                        lower excluded arg
                            ]

                        Some (splice (instanceOf call.Range) (rebuild lowered))
                    | call, _ when
                        isPlainCall call
                        && List.exists isInstanceLambda (fst (Shape.children call))
                        ->
                        let args, rebuild = Shape.children call

                        Some (rebuild [ for arg in args -> if isInstanceLambda arg then arg else lower excluded arg ])
                    | view, _ when view.Type = tree.Type && spliceable excluded view -> Some (splice (instanceOf view.Range) view)
                    | value, _ when isReaderType instanceType value.Type ->
                        match value.Type with
                        | Type.LambdaType (_, range) ->
                            let tag, thunk = thunk ()
                            let held = ident ("armsSelf_" + tag) instanceType

                            opened tag (emit "$0()" [ IdentExpr thunk ]) (Some held) (CurriedApply (value, [ IdentExpr held ], range, value.Range))
                            |> Option.map (fun inner -> Lambda (thunk, inner, None))
                        | _ -> None
                    | _ -> None)

            lower Set.empty body

        let check (bound: Ident list) (body: Expr) =
            let names = set [ for id in bound @ [ matched ] -> id.Name ]

            match hidden names body with
            | Some range ->
                helper.LogError (
                    Diagnostics.unsupported,
                    ?range =
                        (match range with
                         | Some _ -> range
                         | None -> r)
                )
            | None ->
                let outsideReaders =
                    body
                    |> Shape.map (fun expr ->
                        match firstParameter expr with
                        | Some argument when argument.Type = instanceType -> Some (Value (UnitConstant, None))
                        | Some (Thunk instanceType _) -> Some (Value (UnitConstant, None))
                        | _ -> None)

                match find names None outsideReaders with
                | Some found -> report helper matched instanceType r found
                | None -> ()

        let bodies =
            [
                for leaf, (bound, body) in Seq.indexed leaves do
                    match targets[leaf] with
                    | Some _ ->
                        check bound body
                        yield rewrite bound body
                    | None -> ()
            ]

        let packed (selector: Expr) =
            Value (
                NewTuple (
                    [
                        selector
                        Value (NewArray (ArrayValues bodies, tree.Type, MutableArray), None)
                    ],
                    false
                ),
                None
            )

        // A recomputing reader calls the selector and the read by name.
        if recomputed.Value then
            Some (Let (select, selector, Let (reader, read, packed (IdentExpr select))))
        else
            Some (packed selector)
    | _ ->
        helper.LogError (Diagnostics.notLiteral, ?range = r)
        None
