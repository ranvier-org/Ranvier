/// <summary>Helpers shared by the view check files.</summary>
module CheckSupport

open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Scheduling
open Ranvier.View.Foundation.Mounting

let equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

/// <summary>A detached div to mount into.</summary>
let host () : HTMLElement =
    globalThis.document.createElement "div"

let modeOf queued =
    if queued then
        QueuedCommits (CollectAndContinue, ignore)
    else
        SynchronousCommits

/// <summary>A report callback that raises on every report.</summary>
let strict (errors: exn list) =
    failwithf "Unexpected report %A" [ for error in errors -> error.Message ]

/// <summary>The messages of each recorded report, in report order.</summary>
let messages (reports: ResizeArray<exn list>) =
    [ for errors in reports -> errors |> List.map _.Message ]

/// <summary>Mounts <c>view</c> as a root mount at the end of <c>target</c>.</summary>
let mountView (_: Operations<'view>) (run: 'view -> Backend -> unit) queued graph report (target: HTMLElement) (view: 'view) =
    mountWith (modeOf queued) graph None report target None (fun context -> run view context.Backend)

let span (ops: Operations<'view>) (modifiers: Typed.Modifier<HTMLSpanElement> array) children =
    Typed.element ops (HtmlTag "span") modifiers children

[<Emit("document.body.appendChild($0)")>]
let attachToBody (node: Node) : unit = jsNative

[<Emit("document.activeElement")>]
let activeElement () : obj = jsNative

/// <summary>The tree and program variants of a check, under synchronous and queued commits.</summary>
let both (name: string) (tree: bool -> unit) (program: bool -> unit) =
    [|
        for queued in [ false; true ] do
            let mode = if queued then "queued" else "synchronous"
            yield sprintf "tree %s: %s" mode name, (fun () -> tree queued)
            yield sprintf "program %s: %s" mode name, (fun () -> program queued)
    |]
