/// The rule registry, and one CLI and one editor entry per rule.
module Ranvier.Analyzers.Analyzers

open FSharp.Analyzers.SDK
open Ranvier.Analyzers.Rule

/// Every rule, in code order.
let rules: Rule list = []

/// The findings of `rule` in a CLI context; empty when the project lacks the rule's gate reference.
let runCli (rule: Rule) (context: CliContext) : Message list =
    match context.TypedTree with
    | Some tree when Symbols.references rule.Gate context.ProjectOptions ->
        rule.Run
            {
                FileName = context.FileName
                Source = context.SourceText
                Tree = tree
                Index = Index.ofFile tree
                Options = context.ProjectOptions
                Project = Some context.CheckProjectResults
            }
    | _ -> []

let private ignoresFile (code: string) (ranges: Map<string, AnalyzerIgnoreRange list>) =
    ranges
    |> Map.tryFind code
    |> Option.exists (
        List.exists (fun range ->
            match range with
            | AnalyzerIgnoreRange.File -> true
            | _ -> false)
    )

/// The findings of `rule` in an editor context; empty for a file that an `ignore-file` comment exempts.
let runEditor (rule: Rule) (context: EditorContext) : Message list =
    match context.TypedTree with
    | Some tree when
        Symbols.references rule.Gate context.ProjectOptions
        && not (ignoresFile rule.Code context.AnalyzerIgnoreRanges)
        ->
        rule.Run
            {
                FileName = context.FileName
                Source = context.SourceText
                Tree = tree
                Index = Index.ofFile tree
                Options = context.ProjectOptions
                Project = context.CheckProjectResults
            }
    | _ -> []
