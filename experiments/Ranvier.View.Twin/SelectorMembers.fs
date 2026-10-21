module internal Ranvier.View.Twin.SelectorMembers

open Ranvier.View.Testing

let private registered =
    lazy
        (Bridge.ensure ()

         Bridge.registerMember "querySelector" 1 (fun node args ->
             match Selector.query node.Native (string args[0]) with
             | Some element -> box (Bridge.nodeOf element)
             | None -> null)

         Bridge.registerMember "querySelectorAll" 1 (fun node args ->
             let found =
                 Selector.queryAll node.Native (string args[0])
                 |> Array.map (fun e -> Bridge.nodeOf e)
                 |> List.ofArray

             box (ListValue (fun () -> found)))

         Bridge.registerMember "matches" 1 (fun node args -> box (Selector.matches (Selector.parse (string args[0])) (unbox node.Native)))

         Bridge.registerMember "closest" 1 (fun node args ->
             let parsed = Selector.parse (string args[0])
             let mutable cursor = node

             while not (isNull cursor)
                   && not (
                       cursor :? TwinElement
                       && Selector.matches parsed (unbox cursor.Native)
                   ) do
                 cursor <- cursor.Parent

             box cursor))

let ensure () =
    registered.Force ()
