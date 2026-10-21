/// The rule registry, and one CLI and one editor entry per rule.
module Ranvier.Analyzers.Analyzers

open FSharp.Analyzers.SDK
open Ranvier.Analyzers.Rule

/// Every rule, in code order.
let rules: Rule list = [ Rules.Fable.ran001; Rules.Fable.ran002; Rules.Core.ran010 ]

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

[<CliAnalyzer("RAN001", "Fable inlines a snapshot of a module mutable", "https://shayanhabibi.github.io/Ranvier/analyzers/ran001/")>]
let ran001Cli: Analyzer<CliContext> =
    fun context -> async { return runCli Rules.Fable.ran001 context }

[<EditorAnalyzer("RAN001", "Fable inlines a snapshot of a module mutable", "https://shayanhabibi.github.io/Ranvier/analyzers/ran001/")>]
let ran001Editor: Analyzer<EditorContext> =
    fun context -> async { return runEditor Rules.Fable.ran001 context }

[<CliAnalyzer("RAN002", "Generated union case tester in a Fable library", "https://shayanhabibi.github.io/Ranvier/analyzers/ran002/")>]
let ran002Cli: Analyzer<CliContext> =
    fun context -> async { return runCli Rules.Fable.ran002 context }

[<EditorAnalyzer("RAN002", "Generated union case tester in a Fable library", "https://shayanhabibi.github.io/Ranvier/analyzers/ran002/")>]
let ran002Editor: Analyzer<EditorContext> =
    fun context -> async { return runEditor Rules.Fable.ran002 context }

[<CliAnalyzer("RAN010", "Owned node created in a pure derivation", "https://shayanhabibi.github.io/Ranvier/analyzers/ran010/")>]
let ran010Cli: Analyzer<CliContext> =
    fun context -> async { return runCli Rules.Core.ran010 context }

[<EditorAnalyzer("RAN010", "Owned node created in a pure derivation", "https://shayanhabibi.github.io/Ranvier/analyzers/ran010/")>]
let ran010Editor: Analyzer<EditorContext> =
    fun context -> async { return runEditor Rules.Core.ran010 context }
