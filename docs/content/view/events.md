---
title: Events and hooks
---

:::warning Experimental
The view layer is experimental and unpackaged; see [Views](index.md) for its status.
:::

Events and hooks are the parts of a template that run code in the browser: event handlers, code that
runs once an element is placed, references to elements, and elements handed to other libraries. All of
them are browser-only; see [Browser-only parts](../server/rendering.md#browser-only-parts) for what the
server does with them.

The samples use the opens from [Templates and bindings](templates.md).

## Handlers

```fsharp
type Basket = { Items: Signal<int>; Total: Signal<decimal> }

let basket =
    Component.template (fun _ (basket: Basket) -> basket) (
        div () {
            button (
                Attrs.attribute ("type", "button"),
                Events.onClick (fun b _ ->
                    Api.batch (fun () ->
                        b.Items.Value <- b.Items.Value + 1
                        b.Total.Value <- b.Total.Value + 2.50m))
            ) {
                "Add"
            }

            span (Events.on ("dblclick", fun b _ -> b.Items.Value <- 0)) { fun b -> string b.Items.Value + " items" }
        }
    )
```

- A handler receives the instance and the event. `Events.onClick`, `Events.onInput` and the other
  generated members type the event (`PointerEvent`, `InputEvent`, ...); `Events.on (name, handler)`
  takes any event name with an `Event`.
- **Handlers run unbatched.** Each signal write propagates before the next statement. Wrap several
  writes in [`Api.batch`](../guide/batch.md) so effects run once, after the batch.
- Reads in a handler are untracked.
- One handler is shared by every instance of the template. A handler stops running once its instance
  is disposed.
- A handler that throws under a [boundary](boundaries.md) shows the boundary's error view; outside
  every boundary the exception goes to the mount's `report`.

## Hooks

```fsharp
type Search =
    { Query: Signal<string>; Box: NodeRef<Dom.HTMLInputElement> }

    static member Create(_: Setup, initial: string) =
        { Query = createSignal initial; Box = NodeRef.create () }

    static member View =
        div () {
            input (
                Hooks.ref (fun s -> s.Box),
                Hooks.mounted (fun _ (element: Dom.HTMLInputElement) ->
                    element.focus ()
                    fun () -> ()),
                Props.Input.value (from (fun s -> s.Query))
            )

            button (Events.onClick (fun s _ -> s.Box.Current |> Option.iter (fun box -> box.select ()))) { "Select all" }
        }

let search = Component.ofMembers<Search, _> ()
```

| Hook | Runs | Its cleanup |
| --- | --- | --- |
| `Hooks.mounted (fun inst element -> cleanup)` | Once the element is placed in the mount's host. | Runs when the instance is disposed, before its nodes leave. |
| `Hooks.ref (fun inst -> nodeRef)` | Sets the `NodeRef` to `Some element` while the element is placed. | Sets it back to `None`. |
| `Hooks.widget (fun inst element -> cleanup)` | Hands the element's content to other code once placed. | Destroys the widget. |
| `Hooks.lend (fun inst element -> cleanup)` | Lends the element to an integration that adds listeners, attributes or properties beyond its declared parts. | Removes what the integration added. |

A `NodeRef<'native>` comes from `NodeRef.create ()`, usually in setup. `Current` is `None` during
setup, before an enclosing boundary reveals the element, and after disposal; reading it is untracked.
Hooks of a mount run after its first insertion. A mount created inside a batch or inside a computation
runs its hooks after `Mounting.mount` returns.

## Widgets and lent elements

```fsharp
type Chart = { Points: Signal<float array> }

/// Stands in for a third-party library that draws into an element and returns its teardown.
let drawChart (host: Dom.HTMLElement) (points: unit -> float array) : unit -> unit =
    ignore (host, points)
    fun () -> ()

let chart =
    Component.template (fun _ (chart: Chart) -> chart) (
        figure () {
            div (Attrs.className "chart", Hooks.widget (fun c (host: Dom.HTMLDivElement) -> drawChart host (fun () -> c.Points.Value))) { }
            figcaption () { "Weekly totals" }
        }
    )
```

- A **widget** element is declared empty in the template, and the widget owns everything inside it.
  The playground mounts a second Ranvier mount into a widget element.
- A **lent** element keeps its template content. The integration keeps the element under its parent;
  moving it elsewhere is reported after the cleanup.
- Nodes the template owns stay where the template placed them.

## The explicit form

`t.Events.on`, `t.Events.onClick`, `t.Hooks.mounted`, `t.Hooks.ref`, `t.Hooks.widget` and
`t.Hooks.lend` are the same members on the template builder.
