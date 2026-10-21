/// <summary>The runtime shape every Arms.Match site lowers to, on both targets.</summary>
module internal Ranvier.View.Arms.Site

open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar

/// <summary>
/// One arm per body, built when the site is built. The same target keeps its arm and replaces its bound values;
/// another target remounts. A body's instance is the enclosing instance.
/// </summary>
let create (cells: Cells.Cells) (read: 'inst -> 'u) (select: 'u -> int * obj array) (bodies: TemplateView<'inst> array) : TemplateView<'inst> =
    let definitions =
        bodies
        |> Array.mapi (fun target body ->
            Component.define
                (fun setup (current: unit -> 'inst * obj array) ->
                    let instance = fst (current ())
                    Cells.enter cells setup (box instance) target (fun () -> snd (current ()))
                    instance)
                (fun _ -> body))

    matchWith id (fun instance ->
        let target, values = select (read instance)
        arm definitions[target] (instance, values))
