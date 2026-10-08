namespace Ranvier.View.Foundation

type Scope private (report: exn list -> unit, release: unit -> unit, parent: Scope option) =
    let children = ResizeArray<Scope>()
    let callbacks = ResizeArray<unit -> unit>()
    let mutable disposed = false
    let mutable failures: exn list = []

    new(report, release) = Scope (report, release, None)

    member private _.RemoveChild(child) =
        children.Remove child |> ignore

    member private _.EnsureLive() =
        if disposed then
            raise (System.InvalidOperationException "Renderer scope is disposed")

    member this.Child(childRelease: unit -> unit) =
        this.EnsureLive ()
        let child = Scope (report, childRelease, Some this)
        children.Add child
        child

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
            let order = ResizeArray<Scope>()
            let errors = ResizeArray<exn>()
            this.Collect order

            for scope in order do
                scope.Clean errors

            for scope in order do
                scope.Release errors

            failures <- List.ofSeq errors

            if errors.Count > 0 then
                try
                    report failures
                with error ->
                    failures <- failures @ [ error ]

    member _.IsDisposed = disposed
    member _.ChildCount = children.Count
    member _.Failures = failures
