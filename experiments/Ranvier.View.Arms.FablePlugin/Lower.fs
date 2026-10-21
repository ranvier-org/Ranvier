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

/// <summary>Whether <c>value</c> is a field, tuple item or case payload of a controlled identifier.</summary>
let rec private extracts (control: Set<string>) (value: Expr) =
    match value with
    | IdentExpr id -> control.Contains id.Name
    | Get (inner, (UnionField _ | TupleIndex _ | FieldGet _ | OptionValue | ListHead | ListTail), _, _)
    | TypeCast (inner, _) -> extracts control inner
    | _ -> false

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
        | IfThenElse (condition, whenTrue, whenFalse, r) when Shape.refersTo control condition ->
            let whenTrue = walk control bound whenTrue
            IfThenElse (condition, whenTrue, walk control bound whenFalse, r)
        | Let (id, value, body) when extracts control value -> Let (id, value, walk (control.Add id.Name) (bound @ [ id ]) body)
        | body -> leaf bound body

    walk control [] tree

/// <summary>
/// The suffix of a site's emitted names: the line and column of the first available range, in the order given (the
/// call, its arms, its read).
/// </summary>
let siteName (ranges: SourceLocation option list) : string option =
    ranges
    |> List.tryPick id
    |> Option.map (fun r -> sprintf "L%dC%d" r.start.line r.start.column)

/// <summary>The packed function of a call, or None after reporting a rejection.</summary>
let site (helper: PluginHelper) (read: Expr) (arms: Expr) (r: SourceLocation option) : Expr option =
    match arms, siteName [ r; arms.Range; read.Range ] with
    | Lambda _, None ->
        helper.LogError Diagnostics.noRange
        None
    | Lambda (matched, tree, _), Some name ->

        let instanceType =
            match read.Type with
            | LambdaType (instance, _) -> instance
            | other -> other

        let control = Set.singleton matched.Name
        let leaves = System.Collections.Generic.List<Ident list * Expr>()

        mapLeaves
            control
            (fun bound body ->
                leaves.Add (bound, body)
                body)
            tree
        |> ignore

        let valuesFn = ident ("armsValues_" + name) Any

        let selector =
            let position = ref 0

            let pick (bound: Ident list) (_: Expr) =
                let target = position.Value
                position.Value <- target + 1
                let values = [ for id in bound @ [ matched ] -> IdentExpr id ]
                Value (NewTuple ([ int32 target; Value (NewArray (ArrayValues values, Any, MutableArray), None) ], false), None)

            Lambda (matched, mapLeaves control pick tree, None)

        let rewrite (target: int) (bound: Ident list, body: Expr) =
            let slots = bound @ [ matched ]
            let names = set [ for id in slots -> id.Name ]

            let lowered =
                body
                |> Shape.map (fun expr ->
                    match expr with
                    | Lambda (argument, inner, n) when
                        argument.Type = instanceType
                        && Shape.refersTo names inner
                        ->
                        let values = ident ("armsRead_" + name) Any
                        let fresh = [ for id in slots -> id, ident (sprintf "%s_%s" id.Name name) id.Type ]

                        let renamed =
                            inner
                            |> Shape.map (function
                                | IdentExpr id ->
                                    fresh
                                    |> List.tryFind (fun (slot, _) -> slot.Name = id.Name)
                                    |> Option.map (snd >> IdentExpr)
                                | _ -> None)

                        let prologue =
                            List.foldBack
                                (fun (position, (_, local: Ident)) acc ->
                                    Let (local, Get (IdentExpr values, ExprGet (int32 position), local.Type, None), acc))
                                (List.indexed fresh)
                                renamed

                        let current =
                            emit "$0($1, $2)" [ IdentExpr valuesFn; IdentExpr argument; int32 target ] None

                        Some (Lambda (argument, Let (values, current, prologue), n))
                    | _ -> None)

            let outsideReaders =
                body
                |> Shape.map (fun expr ->
                    match expr with
                    | Lambda (argument, _, _) when argument.Type = instanceType -> Some (Value (UnitConstant, None))
                    | _ -> None)

            match Shape.firstReference names outsideReaders with
            | Some id ->
                let at =
                    match id.Range with
                    | Some _ -> id.Range
                    | None -> r

                helper.LogError (Diagnostics.outsideReader id.DisplayName, ?range = at)
            | None -> ()

            lowered

        let bodies = [ for target, leaf in Seq.indexed leaves -> rewrite target leaf ]

        Some (
            Lambda (
                valuesFn,
                Value (
                    NewTuple (
                        [
                            selector
                            Value (NewArray (ArrayValues bodies, tree.Type, MutableArray), None)
                        ],
                        false
                    ),
                    None
                ),
                None
            )
        )
    | _ ->
        helper.LogError (Diagnostics.notLiteral, ?range = r)
        None
