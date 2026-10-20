---
title: Sugar
---

:::warning Experimental
The view layer is experimental and unpackaged; see [Views](index.md) for its status.
:::

`open Ranvier.View.Foundation.Sugar` brings in the vocabulary the other pages use: lowercase tags with
computation-expression bodies, `Attrs`, `Props`, `Events` and `Hooks` as free values, control flow over
views, and the declaration helpers `Component.template`, `Component.ofMembers` and `Component.ofClass`.
Every form lowers to one `Component.define` call and the template builder's operations, so a sugared
component renders the same template tree, server HTML and mounted resources as its
[explicit form](templates.md#the-explicit-form), and adds zero cost per instance except under
`Component.ofClass`.

This page is the tutorial-level summary. The full reference is the
[view sugar README](https://github.com/shayanhabibi/Ranvier/blob/master/experiments/Ranvier.View.Portable/README.md),
which holds every form, its compile errors and its costs.

## Opening the sugar

```fsharp
open global.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
```

`open Ranvier.View.Foundation` brings in the generated members of `Attrs`, `Props` and `Events`, and
`global.Ranvier` brings in `Signal`, `Memo` and `Api`. Two scope rules hold in a file that opens the
sugar:

- **A file uses either the sugar or `Generated.Html.Authoring`.** Both declare `Attrs` and `Props`.
  With `Authoring` opened after the sugar, `div (Attrs.className "x") { }` fails with FS0041; opened
  before it, the sugar's names shadow `Authoring`'s.
- **A local value named like a tag shadows the tag.** `let p = "note"` followed by `p () { "x" }` fails
  with FS0003. One-letter tags (`a`, `b`, `i`, `p`, `q`, `s`, `u`) meet this most often.

`Ranvier.View.Foundation.Generated.Html`, the [authored layer's](authored.md) module of the same tag
names, requires qualified access (`open` of it fails with FS0892). An alias,
`module H = Ranvier.View.Foundation.Generated.Html`, compiles beside the sugar's tags.

See [Opening the sugar](https://github.com/shayanhabibi/Ranvier/blob/master/experiments/Ranvier.View.Portable/README.md#opening-the-sugar)
in the README.

## Tags

Each of the 112 HTML tags is a class named by its lowercase tag name. `base'` and `option'` take an
apostrophe, because `base` is an F# keyword and a generic `option` would shadow `'T option`.

| Form | Meaning |
| --- | --- |
| `div (parts) { children }` | An element. `()` for no parts, `{ }` for no children. |
| `input (parts)` | A void element. It takes no body: `input () { "x" }` fails with FS0708. |
| `"text"` in a body | Static text. |
| `fun i -> ...` in a body | A text binding; `text read` is the same. |
| `from (fun i -> i.Signal)` | A binding over one signal or memo, for text or a part. |
| `for`, `if` in a body | Static structure, evaluated once while the template is built. |

A tag's parts are typed by its DOM interface: `span (Props.Input.value "x")` fails with FS0041. A void
element is a child of another element; a template's root is a non-void element or a fragment.
[Templates and bindings](templates.md) walks through each kind of part.

## The inference rule

> A lambda gets its instance type from its own body. A record label read resolves to the latest record
> in scope that declares the label. Annotate a lambda that reads a label also declared by a later
> record, and every lambda that reads a member of a class instance: a class component, `Signal`,
> `Memo`, or any other class.

| Lambda | Annotation |
| --- | --- |
| Reads a record field whose label only that record declares: `fun r -> string r.Local.Value` | None. |
| Reads a record field inside that record's own static members | None. |
| Reads a label a later record also declares: `fun (row: KeyedRow) -> string row.Id` | The record type. |
| Reads a class member, including an item list held in a `Signal`: `fun (rows: Signal<_ array>) -> rows.Value` | The class type. |
| A non-overloaded `Events.*` entry after an annotated sibling in the same tag's parts | None. |

A missing annotation is reported at the element, as FS0001, FS0193 or FS0041, rather than at the
lambda. Inside `Component.define setup (fun t -> ...)`, `t.` members carry the instance type top-down
and mix with free tags, which suits a view that would otherwise need many annotations:

```fsharp
type Entry = { Id: int; Label: string }
type Marker = { Id: int; Colour: string }

let entry =
    Component.define (fun _ (e: Entry) -> e) (fun t ->
        li (t.Attrs.className "entry") {
            span () { t.text (fun e -> string e.Id) }
            span () { fun e -> e.Label }
        })
```

`t.isolated`, `t.view`, `t.Unsafe` and `t.Document` have no free form and are written the same way;
see [Explicit-only members](templates.md#explicit-only-members).

## Control flow and components

| Forms | Page |
| --- | --- |
| `forEach`, `forEachFixed`, `each` | [Lists and keyed rows](collections.md) |
| `show`, `showElse`, `matchWith`, `branch`, `armOf`, `arm` | [Conditional arms](arms.md) |
| `boundary` | [Boundaries](boundaries.md) |
| `Component.ofMembers`, `Component.template`, `Component.ofClass`, `Component.define` | [Components](components.md#declaration-styles) |
| `render`, `renderOf`, `ComponentTag` | [Components](components.md#child-components) |
| An `Mvu` per component | [Elmish components](elmish.md) |

Three rules carry across these pages:

- **Bind arm definitions with `let`.** An `armOf` inside an `arms` function, or in a `static member`
  property, creates a new definition on every evaluation and remounts the arm each time.
- **Only the helpers read `View`.** Each read of a `static member View` builds a new template;
  `Component.ofMembers` and `Component.ofClass` read it once per definition.
- **Rows keep their own instance.** Reading the enclosing instance inside a row fails to compile:
  FS0041 through a text yield, FS0193 through a part.

A definition created while a list or arm region is read raises in a Fable Debug build: "a template
definition was created while a region was read; bind it in the template's build function".

## Costs

The free tags, the control flow, `Component.template`, `Component.ofMembers`, `render`, `renderOf`
and `ComponentTag` mount the same owners, effects, listeners and DOM writes as their explicit twins.
Unused sugar adds 0 bytes to a bundle; [Performance](../server/performance.md#sugar) gives the bundle
cost of using it, and the README's
[Costs](https://github.com/shayanhabibi/Ranvier/blob/master/experiments/Ranvier.View.Portable/README.md#costs)
section has the per-style figures.
