/// <summary>The .NET lowering of a quoted arms function into a target selector and one case body per target.</summary>
module internal Ranvier.View.Arms.Quoted

#if !FABLE_COMPILER
open System
open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.FSharp.Reflection
open Microsoft.FSharp.Linq.RuntimeHelpers
open Ranvier.View.Foundation.Template

/// <summary>The call site of an Arms.Match call, prefixed to its exception messages.</summary>
type At = { File: string; Line: int }

let private located (at: At) (message: string) =
    sprintf "%s(%d): %s" at.File at.Line message

/// <summary>An F# rendering of a type, for diagnostics; the same text as the plugin's for the same type.</summary>
let rec show (t: Type) : string =
    if t = typeof<unit> then
        "unit"
    elif t = typeof<int> then
        "int"
    elif t = typeof<string> then
        "string"
    elif t = typeof<bool> then
        "bool"
    elif t = typeof<char> then
        "char"
    elif t = typeof<float> then
        "float"
    elif t = typeof<decimal> then
        "decimal"
    elif t = typeof<int64> then
        "int64"
    elif t = typeof<float32> then
        "float32"
    elif t = typeof<byte> then
        "byte"
    elif t = typeof<obj> then
        "obj"
    elif FSharpType.IsFunction t then
        let domain, range = FSharpType.GetFunctionElements t
        atom domain + " -> " + show range
    elif FSharpType.IsTuple t then
        FSharpType.GetTupleElements t
        |> Array.map atom
        |> String.concat " * "
    elif t.IsArray then
        atom (t.GetElementType ()) + " array"
    elif t.IsGenericType then
        let definition = t.GetGenericTypeDefinition ()
        let args = t.GetGenericArguments ()

        if definition = typedefof<option<_>> then
            atom args[0] + " option"
        elif definition = typedefof<voption<_>> then
            atom args[0] + " voption"
        elif definition = typedefof<list<_>> then
            atom args[0] + " list"
        else
            t.Name.Substring (0, t.Name.IndexOf '`')
            + "<"
            + (args |> Array.map show |> String.concat ", ")
            + ">"
    else
        t.Name

and private atom (t: Type) =
    if FSharpType.IsFunction t || FSharpType.IsTuple t then
        "(" + show t + ")"
    else
        show t

/// <summary>Raises AM004 when the instance type is a value type, string, unit or an option.</summary>
let checkInstance (at: At) (t: Type) =
    if
        t.IsValueType
        || t = typeof<string>
        || t = typeof<unit>
        || (t.IsGenericType
            && t.GetGenericTypeDefinition () = typedefof<option<_>>)
    then
        raise (ArmsMatchException (located at (Diagnostics.notObject (show t))))

/// <summary>Whether the expression refers to a controlled variable outside every lambda.</summary>
let rec private refersOutsideLambdas (control: Set<Var>) (expr: Expr) =
    let refers = refersOutsideLambdas control

    match expr with
    | Var var -> control.Contains var
    | Lambda _ -> false
    | Let (_, value, body) -> refers value || refers body
    | LetRecursive (bindings, body) ->
        List.exists (snd >> refers) bindings
        || refers body
    | ExprShape.ShapeVar _
    | ExprShape.ShapeLambda _ -> false
    | ExprShape.ShapeCombination (_, args) -> List.exists refers args

let private refersTo (control: Set<Var>) (expr: Expr) =
    expr.GetFreeVars () |> Seq.exists control.Contains

let rec private extracts (control: Set<Var>) (value: Expr) =
    match value with
    | Var var -> control.Contains var
    | PropertyGet (Some inner, _, [])
    | TupleGet (inner, _)
    | Coerce (inner, _) -> extracts control inner
    | _ -> false

/// <summary>
/// Whether a <c>let</c> belongs to the selection: its value refers to a controlled variable outside every lambda, or,
/// for a compiler-generated variable, extracts a part of one.
/// </summary>
let private decides (control: Set<Var>) (var: Var) (value: Expr) =
    if var.Name.Contains "@" then
        extracts control value
    else
        refersOutsideLambdas control value

let private mapLeaves (control: Set<Var>) (leaf: Var list -> Expr -> Expr) (expr: Expr) : Expr =
    let rec walk (control: Set<Var>) (bound: Var list) (expr: Expr) =
        match expr with
        | IfThenElse (condition, whenTrue, whenFalse) when refersOutsideLambdas control condition ->
            let whenTrue = walk control bound whenTrue
            Expr.IfThenElse (condition, whenTrue, walk control bound whenFalse)
        | Let (var, value, body) when decides control var value -> Expr.Let (var, value, walk (Set.add var control) (bound @ [ var ]) body)
        | body -> leaf bound body

    walk control [] expr

/// <summary>Whether a wrapper at the root of a leaf hides a decision node from the walk (AM007).</summary>
let rec private hidden (control: Set<Var>) (expr: Expr) =
    match expr with
    | IfThenElse (condition, _, _) when refersOutsideLambdas control condition -> true
    | Let (var, value, _) when decides control var value -> true
    | Coerce (inner, _)
    | TryFinally (inner, _)
    | Let (_, _, inner)
    | Sequential (_, inner) -> hidden control inner
    | TryWith (inner, _, _, _, _) -> hidden control inner
    | _ -> false

let private raising =
    set
        [
            "Microsoft.FSharp.Core.Operators.Raise"
            "Microsoft.FSharp.Core.Operators.FailWith"
            "Microsoft.FSharp.Core.Operators.InvalidOp"
            "Microsoft.FSharp.Core.Operators.InvalidArg"
            "Microsoft.FSharp.Core.Operators.NullArg"
            "Microsoft.FSharp.Core.ExtraTopLevelOperators.PrintFormatToStringThenFail"
        ]

/// <summary>Whether a case body is a raised exception, which builds no definition.</summary>
let rec private isThrow (expr: Expr) =
    match expr with
    | Call (None, info, _) -> raising.Contains (info.DeclaringType.FullName + "." + info.Name)
    | Application (f, _) -> isThrow f
    | Let (_, value, _) -> isThrow value
    | Coerce (inner, _) -> isThrow inner
    | _ -> false

/// <summary>The raising body evaluated where the selector returns a target.</summary>
let private raiseAs (t: Type) (body: Expr) =
    Expr.Let (Var ("raised", body.Type), body, Expr.Value (null, t))

/// <summary>
/// Whether <c>arms</c> is the compiler's eta-expansion of a named function, <c>fun x -> f x</c>, which a module-level
/// arms function is quoted as (AM003).
/// </summary>
let private isEtaExpansion (arms: Expr) =
    match arms with
    | Lambda (argument, Call (target, _, args)) ->
        let others =
            List.truncate (args.Length - 1) args
            @ Option.toList target

        (match List.tryLast args with
         | Some (Var passed) -> passed = argument
         | _ -> false)
        && not (
            others
            |> List.exists (fun e -> e.GetFreeVars () |> Seq.contains argument)
        )
    | Lambda (argument, Application (f, Var passed)) ->
        passed = argument
        && not (f.GetFreeVars () |> Seq.contains argument)
    | _ -> false

/// <summary>The first slot variable left free in a lowered body, with the type of the lambda it sits in.</summary>
let private find (slots: Set<Var>) (expr: Expr) : (Var * Type option) option =
    let rec go (over: Type option) (expr: Expr) =
        match expr with
        | Var var when slots.Contains var -> Some (var, over)
        | Let (_, value, body) -> go over value |> Option.orElse (go over body)
        | ExprShape.ShapeVar _ -> None
        | ExprShape.ShapeLambda (var, body) when var.Type = typeof<unit> || var.Name.Contains "@" -> go over body
        | ExprShape.ShapeLambda (var, body) -> go (Some var.Type) body
        | ExprShape.ShapeCombination (_, args) -> List.tryPick (go over) args

    go None expr

/// <summary>The expression with <c>()</c> after each assignment.</summary>
let rec private unitAssignments (expr: Expr) : Expr =
    match expr with
    | ExprShape.ShapeVar _ -> expr
    | ExprShape.ShapeLambda (var, body) -> Expr.Lambda (var, unitAssignments body)
    | ExprShape.ShapeCombination (shape, args) ->
        match ExprShape.RebuildShapeCombination (shape, List.map unitAssignments args) with
        | PropertySet _
        | FieldSet _
        | VarSet _ as assignment -> Expr.Sequential (assignment, Expr.Value (box (), typeof<unit>))
        | rebuilt -> rebuilt

/// <summary>The value of a closed expression, from a delegate compiled or interpreted.</summary>
let private evaluate (interpret: bool) (expr: Expr) : obj =
    Linq.Expressions.Expression
        .Lambda<Func<obj>>(Linq.Expressions.Expression.Convert (LeafExpressionConverter.QuotationToExpression expr, typeof<obj>))
        .Compile(preferInterpretation = interpret)
        .Invoke()

/// <summary>The number of calls a per-call lambda runs interpreted before it is compiled.</summary>
[<Literal>]
let hotCalls = 64

/// <summary>A function that runs interpreted for its first <c>hotCalls</c> calls, then compiled.</summary>
[<Sealed>]
type internal Hot<'a, 'b>(interpreted: 'a -> 'b, compile: unit -> obj) =
    inherit FSharpFunc<'a, 'b>()
    let mutable current = interpreted
    let mutable calls = 0

    override _.Invoke(argument: 'a) : 'b =
        if calls < hotCalls then
            calls <- calls + 1

            if calls = hotCalls then
                current <- unbox<'a -> 'b>(compile ())

        current argument

/// <summary>
/// The expression with each closed lambda that runs per call (a reader, a row reader, a handler, the selector) replaced
/// by a <c>Hot</c> function; thunks over <c>unit</c> and computation-expression builder lambdas are descended into, and
/// delegates stay interpreted; a nested site's quotation is left to its own lowering.
/// </summary>
let rec private compileHot (expr: Expr) : Expr =
    match expr with
    | NewDelegate _
    | QuoteTyped _
    | QuoteRaw _ -> expr
    | Let (var, value, body) -> Expr.Let (var, compileHot value, compileHot body)
    | LetRecursive _
    | ForIntegerRangeLoop _
    | TryWith _ -> expr
    | ExprShape.ShapeLambda (var, _) when
        var.Type <> typeof<unit>
        && not (var.Name.Contains "@")
        && Seq.isEmpty (expr.GetFreeVars ())
        ->
        let domain, range = FSharpType.GetFunctionElements expr.Type
        let hot = typedefof<Hot<_, _>>.MakeGenericType [| domain; range |]
        let compile: unit -> obj = fun () -> evaluate false expr
        Expr.Value (Activator.CreateInstance (hot, [| evaluate true expr; box compile |]), expr.Type)
    | ExprShape.ShapeVar _ -> expr
    | ExprShape.ShapeLambda (var, body) -> Expr.Lambda (var, compileHot body)
    | ExprShape.ShapeCombination (shape, args) -> ExprShape.RebuildShapeCombination (shape, List.map compileHot args)

let private unboxMethod =
    match <@ unbox<int> null @> with
    | Call (_, info, _) -> info.GetGenericMethodDefinition ()
    | _ -> failwith "unbox"

let private getMethod =
    match <@ (null: obj array).[0] @> with
    | Call (_, info, _) -> info
    | _ -> failwith "get"

let private valuesMethod =
    match <@ Cells.values Unchecked.defaultof<Cells.Cells> null 0 @> with
    | Call (_, info, _) -> info
    | _ -> failwith "Cells.values"

/// <summary>
/// Lowers <c>arms</c> into a target selector and one body per target, for the site's cells, evaluated as one LINQ
/// expression by the interpreter.
/// </summary>
let lower (cells: Cells.Cells) (at: At) (arms: Expr<'u -> TemplateView<'inst>>) : ('u -> int * obj array) * TemplateView<'inst> array =
    let matched, tree =
        match arms with
        | Lambda (matched, tree) when not (isEtaExpansion arms) -> matched, tree
        | _ -> raise (ArmsMatchException (located at Diagnostics.notLiteral))

    let control = Set.singleton matched
    let leaves = ResizeArray<Var list * Expr>()

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
                if isThrow body then
                    None :: acc, next
                else
                    Some next :: acc, next + 1)
            ([], 0)
        |> fst
        |> List.rev
        |> Array.ofList

    let selector =
        let position = ref 0

        let pick (bound: Var list) (body: Expr) =
            let leaf = position.Value
            position.Value <- leaf + 1

            match targets[leaf] with
            | None -> raiseAs typeof<int * obj array> body
            | Some target ->
                Expr.NewTuple
                    [
                        Expr.Value target
                        Expr.NewArray (typeof<obj>, [ for var in bound @ [ matched ] -> Expr.Coerce (Expr.Var var, typeof<obj>) ])
                    ]

        Expr.Lambda (matched, mapLeaves control pick tree)

    let rewrite (target: int) (bound: Var list) (body: Expr) =
        let slots = bound @ [ matched ]
        let slotSet = Set.ofList slots

        if hidden slotSet body then
            raise (ArmsMatchException (located at Diagnostics.unsupported))

        let prologue (argument: Var) (inner: Expr) =
            let values = Var ("values", typeof<obj array>)

            let substituted =
                inner.Substitute (fun var ->
                    match List.tryFindIndex ((=) var) slots with
                    | Some field ->
                        Some (Expr.Call (unboxMethod.MakeGenericMethod var.Type, [ Expr.Call (getMethod, [ Expr.Var values; Expr.Value field ]) ]))
                    | None -> None)

            let current =
                Expr.Call (
                    valuesMethod,
                    [
                        Expr.Value (box cells, typeof<Cells.Cells>)
                        Expr.Coerce (Expr.Var argument, typeof<obj>)
                        Expr.Value target
                    ]
                )

            Expr.Let (values, current, substituted)

        let rec visit (expr: Expr) =
            match expr with
            | Lambda (argument, inner) when
                argument.Type = typeof<'inst>
                && refersTo slotSet inner
                ->
                Expr.Lambda (argument, prologue argument inner)
            | NewDelegate (delegateType, argument :: rest, inner) when
                argument.Type = typeof<'inst>
                && refersTo slotSet inner
                ->
                Expr.NewDelegate (delegateType, argument :: rest, prologue argument inner)
            | ExprShape.ShapeVar _ -> expr
            | ExprShape.ShapeLambda (var, inner) -> Expr.Lambda (var, visit inner)
            | ExprShape.ShapeCombination (shape, args) -> ExprShape.RebuildShapeCombination (shape, List.map visit args)

        let lowered = visit body |> unitAssignments

        match find slotSet lowered with
        | Some (var, Some over) when over <> typeof<'inst> ->
            raise (BuildTimeReadException (var.Name, located at (Diagnostics.otherLambda var.Name (show over) (show typeof<'inst>))))
        | Some (var, _) -> raise (BuildTimeReadException (var.Name, located at (Diagnostics.outsideReader var.Name)))
        | None -> lowered

    let bodies =
        [
            for leaf, (bound, body) in Seq.indexed leaves do
                match targets[leaf] with
                | Some target -> yield rewrite target bound body
                | None -> ()
        ]

    let site =
        LeafExpressionConverter.QuotationToExpression (
            Expr.NewTuple
                [
                    compileHot selector
                    Expr.NewArray (typeof<TemplateView<'inst>>, List.map compileHot bodies)
                ]
        )

    // The construction runs once in the LINQ interpreter; each per-call lambda inside it is compiled after hotCalls calls.
    Linq.Expressions.Expression
        .Lambda<Func<obj>>(Linq.Expressions.Expression.Convert (site, typeof<obj>))
        .Compile(preferInterpretation = true)
        .Invoke()
    |> unbox<('u -> int * obj array) * TemplateView<'inst> array>
#endif
