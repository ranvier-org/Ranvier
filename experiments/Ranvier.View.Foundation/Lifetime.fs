namespace Ranvier.View.Foundation

type ScopePhase =
    | SettingUp
    | Attached
    | Disposed

type Scope private (report: exn list -> unit, release: unit -> unit, parent: Scope option) =
    let children = ResizeArray<Scope>()
    let callbacks = ResizeArray<unit -> unit>()
    let mutable disposed = false
    let mutable attached = false
    let mutable failures: exn list = []
    // The active disposal order of this scope's tree. Set only on the topmost scope; null outside a disposal.
    let mutable draining: ResizeArray<Scope> = null

    new(report, release) = Scope (report, release, None)

    member private this.Top: Scope =
        match parent with
        | Some owner -> owner.Top
        | None -> this

    member private _.Draining
        with get () = draining
        and set value = draining <- value

    member private _.RemoveChild(child) =
        children.Remove child |> ignore

    member private _.EnsureLive() =
        if disposed then
            raise (System.InvalidOperationException "Renderer scope is disposed")

    member this.Child(childReport: exn list -> unit, childRelease: unit -> unit) =
        this.EnsureLive ()
        let child = Scope (childReport, childRelease, Some this)
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

            for index in 0 .. children.Count - 1 do
                children[index].Collect order

            order.Add this

    member private _.Clean(errors: ResizeArray<exn>) =
        for index in 0 .. callbacks.Count - 1 do
            try
                callbacks[index]()
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
            let top = this.Top

            match top.Draining with
            | null ->
                let order = ResizeArray<Scope>()
                let errors = ResizeArray<exn>()
                top.Draining <- order

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

                    if errors.Count > 0 then
                        failures <- List.ofSeq errors
                finally
                    top.Draining <- null

                if errors.Count > 0 then
                    try
                        report failures
                    with error ->
                        failures <- failures @ [ error ]
            | order -> this.Collect order

    member this.MarkAttached() =
        this.EnsureLive ()

        if attached then
            raise (System.InvalidOperationException "Renderer scope is already attached")

        attached <- true

        for index in 0 .. children.Count - 1 do
            let child = children[index]

            if not child.IsDisposed && child.Phase = SettingUp then
                child.MarkAttached ()

    member _.Phase =
        if disposed then Disposed
        elif attached then Attached
        else SettingUp

    member _.IsDisposed = disposed
    member _.ChildCount = children.Count
    member _.Failures = failures
