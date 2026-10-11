module Ranvier.View.Foundation.Microtask

open Fable.Core
open Ranvier.View.Foundation.Scheduling

[<Emit("queueMicrotask($0)")>]
let private post (callback: unit -> unit) : unit = jsNative

let create graph failure =
    Queue (graph, failure, post)

let mode failure =
    QueuedCommits (failure, post)
