#load "Lifetime.fs"

open Ranvier.View.Foundation

let equal expected actual =
    if actual <> expected then
        failwithf "Expected %A; got %A" expected actual

let rejects action =
    let mutable rejected = false

    try
        action ()
    with :? System.InvalidOperationException ->
        rejected <- true

    equal true rejected

let cases =
    [|
        "postorder, failure and reentrant disposal",
        fun () ->
            let trace = ResizeArray<string>()
            let errors = ResizeArray<exn>()
            let parent = Scope (List.iter errors.Add, fun () -> trace.Add "remove-parent")
            let child = parent.Child (fun () -> trace.Add "remove-child")
            parent.OnCleanup (fun () -> trace.Add "parent")
            child.OnCleanup (fun () -> trace.Add "child")
            child.OnCleanup (fun () -> failwith "child failure")

            child.OnCleanup (fun () ->
                parent.Dispose ()
                trace.Add "after-reentry")

            parent.Dispose ()
            parent.Dispose ()
            child.Dispose ()
            equal [ "child"; "after-reentry"; "parent"; "remove-child"; "remove-parent" ] (List.ofSeq trace)
            equal 1 errors.Count
            equal 0 parent.ChildCount
        "disposed children unlink from a live parent",
        fun () ->
            let parent = Scope (ignore, ignore)

            for _ in 1..1000 do
                let child = parent.Child (ignore)
                child.Dispose ()

            equal 0 parent.ChildCount
            equal false parent.IsDisposed
            parent.Dispose ()
        "disposed scopes reject registration",
        fun () ->
            let parent = Scope (ignore, ignore)
            parent.Dispose ()
            rejects (fun () -> parent.OnCleanup (ignore))
            rejects (fun () -> parent.Child (ignore) |> ignore)
        "reporter failure follows complete release",
        fun () ->
            let releases = ResizeArray<string>()

            let scope =
                Scope ((fun _ -> failwith "reporter"), fun () -> releases.Add "released")

            scope.OnCleanup (fun () -> failwith "cleanup")
            scope.Dispose ()
            equal [ "released" ] (List.ofSeq releases)
            equal [ "cleanup"; "reporter" ] (scope.Failures |> List.map _.Message)
        "three levels clean before any release",
        fun () ->
            let trace = ResizeArray<string>()
            let parent = Scope (ignore, fun () -> trace.Add "remove-parent")
            let middle = parent.Child (fun () -> trace.Add "remove-middle")

            let leaf =
                middle.Child (fun () ->
                    trace.Add "remove-leaf"
                    failwith "release")

            leaf.OnCleanup (fun () -> trace.Add "leaf")
            middle.OnCleanup (fun () -> trace.Add "middle")

            parent.OnCleanup (fun () ->
                trace.Add "parent"
                rejects (fun () -> parent.Child (ignore) |> ignore))

            parent.Dispose ()
            equal [ "leaf"; "middle"; "parent"; "remove-leaf"; "remove-middle"; "remove-parent" ] (List.ofSeq trace)
            equal [ "release" ] (parent.Failures |> List.map _.Message)
        "child disposal leaves its parent reusable",
        fun () ->
            let parent = Scope (ignore, ignore)
            let removed = parent.Child (ignore)
            removed.Dispose ()
            equal false parent.IsDisposed
            equal 0 parent.ChildCount
            let later = parent.Child (ignore)
            equal false later.IsDisposed
            parent.Dispose ()
            equal true later.IsDisposed
        "sibling reentry does not skip callbacks",
        fun () ->
            let trace = ResizeArray<string>()
            let parent = Scope (ignore, ignore)
            let first = parent.Child (ignore)
            let second = parent.Child (ignore)

            first.OnCleanup (fun () ->
                second.Dispose ()
                trace.Add "first")

            second.OnCleanup (fun () -> trace.Add "second")
            parent.OnCleanup (fun () -> trace.Add "parent")
            parent.Dispose ()
            equal [ "first"; "second"; "parent" ] (List.ofSeq trace)
    |]

for name, check in cases do
    check ()
    printfn "PASS %s" name

printfn "Passed %d pure lifetime checks" cases.Length
