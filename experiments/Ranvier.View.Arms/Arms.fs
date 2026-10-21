namespace Ranvier.View.Arms

open System
open System.Diagnostics.CodeAnalysis
open Ranvier.View.Foundation.Template
#if !FABLE_COMPILER
open Microsoft.FSharp.Quotations
#endif

/// <summary>Arm sites written as a plain F# match: one arm per match target.</summary>
[<AbstractClass; Sealed>]
type Arms =
#if FABLE_COMPILER && RANVIER_ARMS_PLUGIN
    /// <summary>One arm per match target of <c>arms</c>; the Ranvier.View.Arms Fable plugin supplies <c>lowered</c>.</summary>
    [<Ranvier.View.Arms.FablePlugin.CaseArms>]
    static member Match(read: 'inst -> 'u, arms: 'u -> TemplateView<'inst>, ?lowered: obj) : TemplateView<'inst> =
        match lowered with
        | Some packed ->
            let cells = Cells.create ()
            let lower = unbox<Func<Func<obj, int, obj array>, obj>> packed

            let select, bodies =
                unbox<('u -> int * obj array) * TemplateView<'inst> array>(
                    lower.Invoke (Func<obj, int, obj array>(fun instance target -> Cells.values cells instance target))
                )

            Site.create cells read select bodies
        | None -> Errors.noPlugin ()
#else
#if FABLE_COMPILER
    /// <summary>AM005: the project compiled by Fable does not import Ranvier.View.Arms.targets.</summary>
    [<Obsolete(Diagnostics.noPlugin, true)>]
    static member Match(read: 'inst -> 'u, arms: 'u -> TemplateView<'inst>) : TemplateView<'inst> =
        Errors.noPlugin ()
#else
    /// <summary>One arm per match target of <c>arms</c>, lowered from its quotation when the site is built.</summary>
    [<RequiresUnreferencedCode(Diagnostics.reflection); RequiresDynamicCode(Diagnostics.reflection)>]
    static member Match
        (
            read: 'inst -> 'u,
            [<ReflectedDefinition>] arms: Expr<'u -> TemplateView<'inst>>,
            [<System.Runtime.CompilerServices.CallerFilePath>] ?file: string,
            [<System.Runtime.CompilerServices.CallerLineNumber>] ?line: int
        ) : TemplateView<'inst> =
        let at: Quoted.At =
            {
                File = defaultArg file ""
                Line = defaultArg line 0
            }

        let cells = Cells.create ()
        let select, bodies = Quoted.lower cells at arms
        Site.create cells read select bodies
#endif
#endif
