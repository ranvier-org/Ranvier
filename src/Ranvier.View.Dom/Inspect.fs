/// <summary>The inspection registry: per region start marker and template instance root, closures over the state the mount keeps.</summary>
module internal Ranvier.View.Dom.Inspect

#if RANVIER_VIEW_INSPECT || !FABLE_COMPILER
open Fable.Core.TS.Dom
open Ranvier
open Ranvier.View
open Ranvier.View.Template

[<NoEquality; NoComparison>]
type Probe =
    /// <summary>A list region: the keys of its laid-out rows, in order, and each row's first node.</summary>
    | ListProbe of path: InstancePath * rows: (unit -> (obj * Node) array) * stale: (unit -> bool)
    /// <summary>An arm region: the selected key and definition, or null when empty or the arm is authored.</summary>
    | ArmProbe of path: InstancePath * selected: (unit -> struct (obj * TemplateComponent)) * stale: (unit -> bool)
    | BoundaryProbe of path: InstancePath * state: (unit -> BoundaryProbeState)
    | HoleProbe of path: InstancePath
    /// <summary>A template instance: its definition, instance value, owner and path; keyed by its first root node.</summary>
    | InstanceProbe of TemplateComponent * instance: obj * owner: Owner * path: InstancePath
    /// <summary>A mount: its phase, commit mode, whether a pass or its queue is flushing, its identity prefix and its graph; keyed by its range's start marker.</summary>
    | MountProbe of phase: (unit -> ScopePhase) * queued: bool * flushing: (unit -> bool) * prefix: string * graph: Graph

and [<NoEquality; NoComparison>] BoundaryProbeState =
    | Ready
    | Pending of waiting: int * revealed: bool
    | Failed of error: exn * generation: int

let private table = Native.newWeakMap ()
let private instances = Native.newWeakMap ()

/// <summary>The instances rooted at <c>node</c>, in outer-to-inner registration order.</summary>
let instancesOf (node: Node) : Probe array =
    match Native.get<Node, Probe array> instances node with
    | null -> [||]
    | probes -> probes

/// <summary>Whether the node's sources changed after its last run; false for null.</summary>
let isStale (node: INode) =
    not (isNull (box node))
    && node.Status &&& Status.Stale <> Status.None

/// <summary>Registers <c>probe</c> for <c>node</c> until the returned cleanup runs.</summary>
let register (node: Node) (probe: Probe) : unit -> unit =
    match probe with
    | InstanceProbe _ ->
        Native.set instances node (Array.append (instancesOf node) [| probe |])

        fun () ->
            let remaining =
                instancesOf node
                |> Array.filter (fun current -> not (obj.ReferenceEquals (current, probe)))

            if remaining.Length = 0 then
                Native.delete instances node
            else
                Native.set instances node remaining
    | _ ->
        Native.set table node probe

        fun () ->
            match Native.get<Node, Probe> table node with
            | current when obj.ReferenceEquals (current, probe) -> Native.delete table node
            | _ -> ()

let private bound = Native.newWeakSet ()

/// <summary>Marks a text node as a slot target.</summary>
let markBound (node: Node) =
    Native.add bound node

/// <summary>Whether the text node is a slot target.</summary>
let isBound (node: Node) =
    Native.has bound node

let probeOf (node: Node) : Probe option =
    match box (Native.get<Node, Probe> table node) with
    | null -> instancesOf node |> Array.tryHead
    | probe -> Some (unbox<Probe> probe)
#endif
