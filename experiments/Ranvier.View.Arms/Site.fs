/// <summary>The runtime shapes Arms.Match sites lower to: payload-direct under Fable, cells on .NET.</summary>
module internal Ranvier.View.Arms.Site

open Ranvier.View.Template
open Ranvier.View.Sugar

/// <summary>
/// One arm per body, built when the site is built. The same target keeps its arm and replaces its bound values;
/// another target remounts. A body's instance is the enclosing instance.
/// </summary>
let inline create (cells: Cells.Cells) (read: 'inst -> 'u) (select: 'u -> int * obj array) (bodies: TemplateView<'inst> array) : TemplateView<'inst> =
    let definitions =
        bodies
        |> Array.mapi (fun target body ->
            Component.define
                (fun setup (current: unit -> 'inst * obj array) ->
                    let props = current ()
                    let instance = fst props
#if FABLE_COMPILER
                    Cells.enter cells setup (box instance) target (unbox<unit -> obj * obj array>(box current)) (snd props)
#else
                    Cells.enter cells setup (box instance) target (fun () -> snd (current ())) (snd props)
#endif
                    instance)
                (fun _ -> body))

    matchWith id (fun instance ->
        let target, values = select (read instance)
#if FABLE_COMPILER
        arm (Fable.Core.JsInterop.emitJsExpr (definitions, target) "$0[$1]") (instance, values))
#else
        arm definitions[target] (instance, values))
#endif

#if FABLE_COMPILER
/// <summary>
/// The values array of an arm of the site tagged <c>tag</c>, read from its values thunk; any other value raises AM006.
/// A rewritten reader opens with it.
/// </summary>
let values (thunk: obj) (tag: obj) : obj array =
    let values: obj array =
        Fable.Core.JsInterop.emitJsExpr thunk "typeof $0 === \"function\" ? $0() : null"

    if
        not (isNull values)
        && obj.ReferenceEquals (values[1], tag)
    then
        values
    else
        Errors.noArm ()

/// <summary>
/// One <c>armOf</c> definition per body, whose instance is the arm's values thunk. The selector's array
/// <c>[target, site tag, pattern variables..., matched value, instance]</c> is the selected arm's payload; the same
/// target keeps its arm and receives the new array, and another target remounts.
/// </summary>
let inline direct
    (read: 'inst -> 'u)
    (select: System.Func<'inst, 'u, obj array>)
    (bodies: TemplateView<unit -> obj array> array)
    : TemplateView<'inst> =
    let definitions = Array.map armOf bodies

    matchWith id (fun instance ->
        let values = select.Invoke (instance, read instance)
        arm (Fable.Core.JsInterop.emitJsExpr (definitions, values) "$0[$1[0]]") values)

/// <summary>
/// A helper view over the enclosing instance inside a payload-direct body: an inlined child whose instance is
/// <c>instance</c> of the arm's values thunk. The child keeps the helper's structural hash and server HTML.
/// </summary>
let splice (instance: (unit -> obj array) -> obj) (view: TemplateView<obj>) : TemplateView<unit -> obj array> =
    render (Component.define (fun _ (current: obj) -> current) (fun _ -> view)) instance
#endif
