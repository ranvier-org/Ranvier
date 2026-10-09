namespace Ranvier.View.Foundation

type ScopePhase =
    | SettingUp
    | Attached
    | Disposed

type Scope private (report: exn list -> unit, release: unit -> unit, parent: Scope option, drain: ResizeArray<Scope> option ref) =
    let children = ResizeArray<Scope>()
    let callbacks = ResizeArray<unit -> unit>()
    let mutable disposed = false
    let mutable attached = false
    let mutable failures: exn list = []

    new(report, release) = Scope (report, release, None, ref None)

    member private _.RemoveChild(child) =
        children.Remove child |> ignore

    member private _.EnsureLive() =
        if disposed then
            raise (System.InvalidOperationException "Renderer scope is disposed")

    member this.Child(childReport: exn list -> unit, childRelease: unit -> unit) =
        this.EnsureLive ()
        let child = Scope (childReport, childRelease, Some this, drain)
        children.Add child
        child

    member this.Child(childRelease: unit -> unit) =
        this.Child (report, childRelease)

    member this.OnCleanup(callback: unit -> unit) =
        this.EnsureLive ()
        callbacks.Add callback

    member private this.Collect(order: ResizeArray<Scope>) =
        if not disposed then
            disposed <- true

            for child in children do
                child.Collect order

            order.Add this

    member private _.Clean(errors: ResizeArray<exn>) =
        for callback in callbacks do
            try
                callback ()
            with error ->
                errors.Add error

        callbacks.Clear ()

    member private this.Release(errors: ResizeArray<exn>) =
        try
            release ()
        with error ->
            errors.Add error

        children.Clear ()

        match parent with
        | Some owner -> owner.RemoveChild this
        | None -> ()

    member this.Dispose() =
        if not disposed then
            match drain.Value with
            | Some order -> this.Collect order
            | None ->
                let order = ResizeArray<Scope>()
                let errors = ResizeArray<exn>()
                drain.Value <- Some order

                try
                    this.Collect order
                    let mutable cleaned = 0
                    let mutable released = 0

                    while released < order.Count do
                        while cleaned < order.Count do
                            order[cleaned].Clean errors
                            cleaned <- cleaned + 1

                        order[released].Release errors
                        released <- released + 1

                    failures <- List.ofSeq errors
                finally
                    drain.Value <- None

                if errors.Count > 0 then
                    try
                        report failures
                    with error ->
                        failures <- failures @ [ error ]

    member this.MarkAttached() =
        this.EnsureLive ()

        if attached then
            raise (System.InvalidOperationException "Renderer scope is already attached")

        attached <- true

        for child in children do
            if not child.IsDisposed && child.Phase = SettingUp then
                child.MarkAttached ()

    member _.Phase =
        if disposed then Disposed
        elif attached then Attached
        else SettingUp

    member _.IsDisposed = disposed
    member _.ChildCount = children.Count
    member _.Failures = failures
