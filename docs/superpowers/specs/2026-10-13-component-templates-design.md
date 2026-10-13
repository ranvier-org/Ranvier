# Component templates design

Date: 2026-10-13. Status: design approved section by section; awaiting review of this document. Nothing below is implemented.

Baseline: `1c317d2` on `poc/fable-ranvier-dom`. Motivation: the [mount cost rationale](../../research/framework-performance/investigation/mount-rationale.md) and the [allocation cuts](../../research/2026-10-13-allocation-cuts.md). After those cuts a 128-row mount allocates about 513 KiB against Solid 2's 140 KiB; about 140 KiB of Ranvier's comes from each instance rebuilding its view definition, and a further share from one effect, owner link and observer entry per binding.

## Intent

Build a component's definition once per component type instead of once per instance, with bleeding-edge mount performance as the goal and Solid 2 as the reference. Prior art, including Fable.Ripple's templates, is a comparison point only.

Decisions taken during design:

- **Explicit, opt-in API.** A template is a typed value whose dynamic parts receive the instance as a parameter. The type system guarantees that a template captures no instance state. Existing `createComponent` code is unchanged. Implicit caching was rejected because closures capture instance values, structure can vary per instance, closures created per call have no stable identity, and setup must still run per instance. A Fable compiler plugin was rejected as it replaces the no-compiler design point; it remains possible later on top of this API.
- **Portable.** A template is a backend-neutral value. The DOM backend compiles it; server HTML (.NET) and the tree representation expand it.
- **Server means .NET.** Templates and every `T.*` helper compile and run on .NET. DOM types are type tags there; no `Emit` or `jsNative` code is reachable on the server path.
- **Hydration is out of scope**, but the skeleton is round-trippable HTML so a later hydrating mount can reuse the walk.

## Goals and non-goals

Goals:

- Per instance: one `cloneNode`, one state object, one render effect, and two property writes per event node. Everything else exists once per component type.
- 128-row benchmark row, against Solid 2.0.0-rc.14 in the same processes: mount within 1.3× Solid 2; JavaScript allocation within 1.5× Solid 2 (about 210 KiB). These are targets for the measurement section, not acceptance gates.
- Every guarantee kept by owner lifetime and delegated listeners: `use`-order cleanup with DOM attached, rollback after a failing setter, owner-checked handlers, and a reader failure confined to its slot.

Non-goals:

- Hydration, server state transfer and streaming.
- Slimmer keyed row mounts; rows remain nested mounts and are a separate, list-benchmark-driven change.
- Runtime code generation (`new Function`); a possible later specialisation behind a CSP check.
- Changes to ordinary components, which keep their own owner.

## Authoring surface

```fsharp
type Row = { Index: int; Source: Signal<string>; Disabled: Memo<bool>; Local: Signal<int> }

let row : Component<RowProps> =
    Component.define
        (fun setup props ->
            { Index = props.Index; Source = props.Source; Disabled = props.Disabled; Local = Api.createSignal 0 })
        (T.div [| T.Attrs.className "panel"
                  T.Attrs.id (fun r -> "row-" + string r.Index)
                  T.Attrs.title (fun r -> r.Source.Value) |]
               [| T.input [| T.Props.value (fun r -> r.Source.Value) |]
                  T.button [| T.Props.disabled (fun r -> r.Disabled.Value)
                              T.Events.onClick (fun r _ -> r.Local.Value <- r.Local.Value + 1) |]
                           [| T.text (fun r -> string r.Local.Value) |] |])

H.Program.section [||] [| for i in 1..128 -> Component.render row { Index = i; Source = source; Disabled = disabled } |]
```

- `Component.define (setup: Setup -> 'props -> 'inst) (template: Template<'inst>) : Component<'props>` is created once, normally at module level. Its value is the component's identity.
- `T.*` helpers are generated from the same catalogue as `H.Program.*`, with the same element typing. A value argument gives a static part; an `'inst -> value` argument gives a dynamic part. Static and dynamic forms are overloads.
- Event handlers are `'inst -> Event -> unit`: one shared function per template.
- `T.isolated part` gives one dynamic part its own effect, for readers too expensive to re-run with the instance's other slots; the documented remedy before it is a memo created in setup.
- Dynamic structure: `T.render childDef (fun r -> props)`, `T.view (fun r -> view)`, `T.branch (fun r -> key) arms`, `T.each key (fun r -> items) row`.
- `Component.render def props : Program.View` and `Component.renderWith ops def props : 'view` embed a template component in any representation.

## Definition and instance model

`Component<'props>` derives from a non-generic `ComponentDefinition` holding the erased setup, the template tree, and one lazily filled compiled-form slot per backend kind.

The template tree is immutable. Nodes: element (tag, static attributes and properties, dynamic slots, event slots, children), static text, text slot, child template, view hole, branch, keyed list, fragment. Slots are stored as `obj -> obj` readers and `Func<obj, Event, unit>`-shaped handlers; only the typed `T.*` constructors create them. Static attribute names are validated when the template is built.

Per instance:

1. Setup runs untracked under the enclosing owner and returns the instance record.
2. The backend materialises the template for that record.

A template instance has no owner of its own. It cannot outlive or be disposed apart from its enclosing mount, branch arm or keyed row, each of which already has an owner, so its cleanups and render effect register on the enclosing owner and are torn down in `use` order. `Setup.OnCleanup` runs when the enclosing structure is disposed. A throwing setup or setter rolls back the mount; handlers are inert once the enclosing owner is disposed.

## DOM backend

### Compilation, once per definition

- **Markup.** One portable function, shared with the server path, renders the template's static structure, static attributes and properties, and static text into an HTML string with markers:
  - `<!--[-->` and `<!--]-->` delimit a region: branch, keyed list or view hole.
  - A text slot is a text node containing one space. Where it would be adjacent to other text or to a region, a `<!--!-->` separator is emitted so parsing preserves node boundaries.
  - Child templates are inlined: their markup is part of the parent's, so one clone covers a parent and all inlined descendants.
- **Skeleton.** The markup is parsed once through a `<template>` element. The skeleton must be round-trippable: serialising it and parsing the result yields the same node structure. Compilation asserts this in development builds.
- **Walk program.** An integer array of `FirstChild`, `NextSibling`, `Parent` and `Collect` steps reaching every dynamic node of a clone in document order, interpreted by a tight loop.
- **Slot table.** Per slot: node index, kind (attribute, property, text, event, structure, isolated), shared reader or handler, comparer, and for child templates the inlined child's slot range.

### Materialisation, per instance

1. `skeleton.cloneNode(true)`; the walk collects dynamic nodes.
2. One state object: instance record, enclosing owner and graph, dynamic node references, previous slot values.
3. One render effect: for each non-isolated slot, read, compare with the previous value, write only on change, record. The first run writes every slot. Each reader is guarded on its own; a throwing reader fails its slot and leaves the others running. A throwing setter during setup rolls back the mount. Each `T.isolated` slot gets its own effect.
4. Events: each event node receives `node["$$ranvier:<event>"] = handler` and `node["$$ranvier:<event>:data"] = state`. The delegated dispatcher, seeing data, calls `handler state.Inst event` under the state's graph and owner, untracked, and does nothing if that owner is disposed. Non-bubbling events attach a direct listener as today.
5. Inlined child templates: each child instance runs its setup, receives its own state object and render effect over its slot range, and reuses the parent's cloned nodes.
6. Regions mount their content at the anchors through the existing branch, keyed and interpreter paths.
7. The clone joins the mount through the existing append path.

## Non-DOM backends

`Backend` gains `abstract Template: ComponentDefinition * obj -> unit` with a default implementation that runs setup and expands the template into existing `Backend` calls: static parts to `Attribute`, `Property` and `Text`; slots to `ReactiveAttribute`, `ReactiveProperty` and `Bind` with readers applying the slot to the instance; events to `Listen`; structure to `Component`, `Keyed` and branch calls. The mount backend overrides it with the compiled path. Server HTML, the tree representation and `Browser.fs` use the default; prepared skeletons reject templates through it.

Server HTML for a template is the same markup function's output with slot values filled, so markers and escaping are identical on .NET and under Fable.

## Supersession

`Prepared` templates are superseded: the template adapter replaces the prepared benchmark adapter, and `Prepared` is marked superseded in its documentation. Removal is a later decision.

## Testing

- Equivalence: each template feature has a `createComponent` twin; both produce identical server HTML on .NET and under Fable, and identical live DOM under the same updates.
- Cross-runtime: a template's HTML on .NET byte-matches its HTML under Fable.
- Materialisation: clone and walk including `<template>` content, multiple roots and separators; slot comparison and first-run writes; `T.isolated`; reader failure confined to its slot; setter failure rolling back the mount; delegated template handlers including after disposal and after movement; cleanup order without an instance owner.
- Structure: inlined child templates, view holes, branch switching and disposal, keyed lists of template rows.
- Round-trip: every test skeleton round-trips.
- Fallback: templates render through `Browser.fs` and the tree representation, and prepared skeletons reject them.
- Existing suites pass unchanged.

## Measurement

- A `ranvier-template` adapter in the framework harness renders the benchmark row as a template, alongside the authored adapter, against Solid 2 with `flush()` after timed writes.
- Allocation per mount from the sampler against Solid 2 and Ripple.
- The ladder's component-ownership rung re-run to confirm the per-instance owner cost is absent for templates.

## Risks and open questions

- **Readers re-run together.** Any signal change re-evaluates every non-isolated reader of an instance. Mitigation: memos in setup, `T.isolated`.
- **Generated helper surface.** `T.*` doubles the generated surface; the generator must emit both forms from one catalogue entry.
- **Erased slot storage.** Fable function adaptation can reintroduce closures at the erasure boundary; the implementation must verify emitted JavaScript, as the allocation cuts did.
- **Inlining depth.** Deeply nested inlined children lengthen the walk program; measure before adding limits.
- **Keyed rows** remain nested mounts; list performance is a separate design.
- **Text slot placeholders.** A single-space text node is dropped or moved by the HTML parser in some contexts, such as directly inside `table` or `select`. The development-build round-trip assertion detects this; in those contexts the slot uses a `<!--#-->` placeholder that materialisation replaces with a text node.
