# View authoring

The foundation is an experimental view layer. `Authoring` binds the existing operations to `Program.View`; the generated `Html.Program` module provides all 112 typed HTML constructors without an `ops` argument.

```fsharp
open global.Ranvier
open Ranvier.View.Foundation.Authoring
open Ranvier.View.Foundation.Components

module Html = Ranvier.View.Foundation.Generated.Html.Program
module Attr = Ranvier.View.Foundation.Generated.Html.Attrs
module Events = Ranvier.View.Foundation.Generated.Html.Events

type CounterProps =
    { Label: Prop<string>
      InitialCount: int
      Changed: int -> unit }

let counter props =
    createComponent
        (fun context props ->
            let count = Api.createSignal props.InitialCount

            Html.section [ Attr.classStatic "counter" ]
                [ Html.h2 [] [ propText props.Label ]
                  Html.button
                      [ Attr.Button.typeStatic "button"
                        Events.onClick (fun _ ->
                            count.Value <- count.Value + 1
                            props.Changed count.Value) ]
                      [ text "Count: "
                        reactiveText (fun () -> string count.Value) ] ])
        props
```

Setup runs once per mounted instance. Use `context.OnCleanup` for external resources. Reactive readers update their bindings under the component's owner; reading a prop in setup captures its current value. `Constant` creates fixed prop text, while `Reactive`, `Prop.ofSignal` and `Prop.ofMemo` provide explicit reactive sources.

`fragment` combines child views. `keyedArray equals key read render` retains rows by key and gives each row a reader for its current item. `branch equals key read render` retains a selected instance until its key changes. Child and modifier authoring collections remain lists; keyed snapshots are arrays.

The authoring functions and generated bound constructors are inline. Fable expands them to the existing operations and helpers. The emitted-JavaScript check verifies the wrapper functions disappear; it is not a throughput benchmark. Existing representation-neutral constructors remain available under `Generated.Html` for `Tree` and other operation sets.

Run `Program.run view context.Backend` inside the existing `Mounting` callback, or pass `Program.run view` to `Representation.Html.render` for a snapshot. Browser properties and listeners require the snapshot renderer's explicit `Omit` policy.
