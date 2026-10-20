# View sugar v2 design: free tags and component declarations

> **Status 2026-10-20:** design, not yet approved. It amends the [view sugar design](2026-10-19-view-sugar-design.md) (v1). Every v1 rule this spec does not change stays binding: the lowering contract (§2 of v1), the twin-equality criteria, the definition-in-read check, the `forEach`/`forEachFixed` split and the `show` lowering.
>
> **Decisions taken by the user on 2026-10-20** (binding; §0 quotes them):
>
> - Tags are free lowercase names in the style of Oxpecker, `div (Attrs.className "x") { ... }`, generated as one generic class per tag (§4.1).
> - `match!` arms are not shipped; arms stay free `matchWith` with hoisted `armOf` (§4.2, §3.3).
> - Four component declaration styles ship side by side, each lowering to `Component.define`, ranked in the docs (§4.6).
> - The setup/template split stays internal. The Fable compiler plugin (v1 option C), Myriad and the `IComponent` static interface are rejected (§3.3).
> - Nested Elmish is documented as a pattern; the disposal gap of `Mvu` is a `Ranvier.Elmish` follow-up outside this spec (§4.7).

Date: 2026-10-20. Branch `feat/view-sugar-v2` at `deafe75`, the tip of v1, in the `Ranvier-view-sugar-v2` worktree. Inputs: the v1 spec and its [research note](../../research/2026-10-20-view-sugar.md); the sugar v2 probe report (free tags and `match!`) and the component-declaration probe report, both of 2026-10-20 and run at `df3bb0b`; the probe code in the `Ranvier-sugar-probe2` worktree (staged, uncommitted): `experiments/Ranvier.View.Portable/{FreeSugar,FreeElements,ComponentDecl}.fs`, `examples/Fable.Ranvier.Playground/tests/{FreeFixtures,DeclFixtures,DeclChecks,KeyedRowsFreePerf,FrameworkFreePerf}.fs`, `probe/{FreeProbe,Collide,DeclProbe}`. The probe reports live outside the repository; every number this spec relies on is restated here, and delivery step 6 copies both reports into `docs/research/`.

## 0. Decisions

These are the user's decisions of 2026-10-20, quoted; "[…]" marks omitted instructions to the spec's author. The rest of the spec elaborates them and leaves them closed.

1. **Free tags.** "Tags become free lowercase functions in Oxpecker style (`div (Attrs.className "x") { ... }`); name-collision risk accepted. Attribute spelling: `Attrs.className "x"` (v1's minus `t.`)."
2. **Arms.** "`match!` arms are not shipped; arms stay free `matchWith` with hoisted `armOf`."
3. **Declaration styles.** "Multiple component declaration styles ship side by side, each lowering to `Component.define`, ranked in docs: (1) record with static `Create`/`View` (`Component.ofMembers`), (2) `Component.template setup view`, (3) class with constructor setup and static `View` (`Component.ofClass`, annotate every instance lambda), (4) explicit `Component.define` with `t.` as the documented equivalent. Components used as tags via free `render`/`renderOf`; the per-component tag class is optional."
4. **One declaration, no code generation.** "The setup/template split stays internal; v2 makes it read as one declaration. The Fable compiler plugin (v1 option C) is CLOSED […]. Myriad is rejected […]. `IComponent` static interface is dropped until Fable supports static interface calls […]."
5. **Nested Elmish.** "Document the pattern (each component's setup owns an `Mvu`, props reader down, callback up). The disposal gap (a disposed child's `Mvu` keeps running `update` and commands) is a `Ranvier.Elmish` change out of this spec's scope […]."

## 1. Purpose

v1 made the template path shorter with `t.Div(parts) { children }` and inline row and arm definitions. Two kinds of ceremony remain:

- **The `t.` carrier.** Every tag, attribute and control-flow call goes through the builder value `t`, so a view reads `t.Div(t.Attrs.className "x") { ... }`. Oxpecker.Solid writes `div(class'="x") { ... }`.
- **The declaration.** A component is two values joined by `Component.define`: a setup function and a build function, written apart from the instance type they share. Authors coming from classes or records expect one declaration that holds state and view together.

v2 removes the carrier for tags, attributes and control flow, and adds declaration helpers that make setup and view read as one unit. Both lower to the v1 operations. Per-instance cost stays at zero (one exception, `Component.ofClass`, §4.6), and the explicit form stays the documented equivalent of every sugar form.

### Success criteria

v1's criteria 1–6 apply to every v2 form. The changes and additions:

1. **Same tree.** Every free-tag form and every declaration style matches its explicit twin: `Template.skeletonLayout` field by field, `structuralHash`, `Server.render` output on .NET and under Fable, and `Strict` hydration of the twin's server HTML with no report.
2. **Same mounted resources.** As v1: owners, effects and edges from the core `Counters.Snapshot ()` under `RANVIER_COUNTERS`; listener registrations from a wrapped `addEventListener`; native DOM writes from `MutationObserver` records. Equal per instance at mount, update and disposal.
3. **No per-instance overhead.**
   - `run.mjs --template-comparison`: the free-tag row adapter's ranges overlap the explicit adapter's in every phase and width. A row declared with `Component.ofMembers` is one more adapter with the same requirement. A row declared with `Component.ofClass` is reported against the same ranges; §4.6 names its known per-instance cost.
   - `keyed-rows.mjs` with the free keyed and arm fixtures: overlapping ranges for every operation.
   - `keyed-heap.mjs`: the same retained bytes per row as the explicit fixture.
   - An allocation sample of a 128-row mount contains no sugar frames: `Element`, a tag class, `VoidElement`, `ComponentTag`, `Builders_` or `Delay`.
4. **Bundle.** 0 bytes when the sugar is unused. When used by the benchmark adapter set, at most 1 kB gzip over the explicit entry, reported raw, gzip and Brotli, and also against v1's sugared entry. The declaration helpers are measured in the same entry. Emitted sugar modules contain 0 `curry`, `uncurry` or `partialApply`.
5. **Correctness without sugar.** Unchanged from v1: `ComponentsExample.fs`, `App.fs`, `KeyedRowsPerf.fs` and the explicit fixtures stay as they are.
6. **Authoring gain, measured.** For the demo screen, the keyed fixture, the arm fixture and the benchmark row: written annotations, non-whitespace characters and `Component.define` call sites, before (v1 sugar) and after (v2). §9 gives the probe's figures as the baseline the implementation has to reproduce.
7. **Build once.** A static `View` member is read once per definition by its helper: a counter on `View` reads 1 after mounting several instances of each child.

## 2. Constraints

v1 §2 holds unchanged: construction cost lands at definition time on the template path; definition identity is component identity; `if` and `match` stay ordinary evaluation; no ownership inference and no implicit caching; sugar lowers to `TemplateNode` values both the compiled and the expanded paths handle; a tag fixes `'native`.

v2 adds three:

- **The instance type flows bottom-up only.** No instance type reaches a lambda from `Component.define`, from an enclosing element, from a sibling yield or from the `View` member's type. Each lambda infers its instance type from its own body, and the whole view unifies afterwards (§4.1). This is a property of F#'s checking order for `builder (args) { body }` and for implicit yields, and no library shape changes it (probe 2 §1.2).
- **A property getter runs on every read.** A `static member View` or a `static member` holding an `armOf` definition creates a new definition on each read. Only the declaration helpers read `View`, and arm definitions are bound with `let` (§4.2, §4.6).
- **Fable 5.18.0 is pinned.** Two of its constructor miscompilations shape the generated code (§4.5).

## 3. Approaches

### 3.1 Free tags over the template builder (adopted)

v1 approach A, with the carrier removed. Tags are generic classes, one per HTML tag; `Attrs`, `Props`, `Events` and `Hooks` are type functions over v1's builders; control flow is free functions that take views.

- **Gain:** no `t.` on any line; the demo screen needs 0 annotations (v1 wrote 10); bundle +299 B gzip over v1's sugar.
- **Cost:** a lambda whose body cannot infer the instance type needs an annotation, and the compiler reports a missing one at the element (FS0001, FS0193 or FS0041) rather than at the lambda. 112 lowercase type names enter scope with the namespace.

### 3.2 Declaration helpers (adopted)

SRTP helpers that read `Create` and `View` from a type, plus `Component.template` for the setup-and-view pair. Each is one `Component.define` call.

### 3.3 Rejected

| Alternative | Evidence | Status |
|---|---|---|
| Fable compiler plugin (v1 option C) | v1 §3 C: divergence from the .NET server path, ownership inference, Fable AST coupling | **Closed** (decision 4). v1 open question 6 is answered: never. |
| Myriad code generation | Myriad 1.1.0 needs two packaging workarounds before it generates anything (an undownloaded `PackageDownload`, and a net8.0 tool paired with a net9.0 plugin that writes a header-only file); it adds a net8/9 runtime requirement, an `FSharp.Core (= 9.0.101)` pin warning, stale output under `fable watch`, and it cannot reach instance typing, which is where the class annotations come from. Its gain is the 4-line tag class per component. | **Rejected** (decision 4). |
| `IComponent<'self,'props>` with static abstract `Create` and `View` | Works on .NET with 0 annotations. Fable 5.18.0 fails, inline and not: `error EXCEPTION: Unexpected static interface/override call: Ranvier.View.Foundation.Free.IComponent`2.Create`, raised from `Fable.Transforms.FSharp2Fable.Util.callAttachedMember` and `inlineExpr`. SRTP does not see explicit interface implementations (FS0001 "lacks the required member 'Create'"), so the interface cannot sit on top of `ofMembers` either. | **Dropped until Fable supports static interface calls** (decision 4). A later `Component.ofType` keeps the same member names (§8 Q9). |
| An instance-member interface (`member this.View`) | FS0001; `build` would need an instance before one exists | Rejected. |
| A `component { setup ...; view ... }` CE | Fails with FS0072 at the same lambda as `Component.template` with the same body (probe 3 I15, I16); adds a builder type and two custom operations for one `Component.define` call | Rejected. |
| `match!` arms, strategy (a), rebuild per change | Correct, but mount 1.23×, switch 1.09× and payload update 1.34× the explicit fixture; 11.8× allocation per payload update; +1.9 kB gzip over free tags; an unbounded shape cache when a case body puts its payload into static text; a full remount for nested control flow in a case body | **Not shipped** (decision 2). |
| `match!` arms, strategy (b), each case built once | Refuted: `match!` binds values, so every instance renders the payload of the first sighting | **Not shipped** (decision 2). |
| A type argument on a tag, `span<KeyedRow> (...)` | FS0041: a ParamArray constructor is two candidate forms, so its arguments are checked without the expected type | Refuted. |
| Non-overloaded reader entry points (`Reader.title`) | Type-check only after an annotated sibling in the same ParamArray; never reach the body | Rejected. |

The probe's `match!` runtime (`Watch`, `watch`, `watchOnce`, `CaseArms`, `Element.Bind`), `KeyedRowsMatchPerf.fs` and the `regionRead` visibility change in `Template.fs` are not carried into v2. Re-adding `match!` later is additive: it is one `Bind` member.

## 4. Design

All sugar is opt-in: `open Ranvier.View.Foundation.Sugar`. The namespace keeps v1's name and its two `[<AutoOpen>]` modules, now with v2 contents:

- `Builders` (handwritten, `TemplateSugar.fs`): `Element`, `VoidElement`, `ComponentTag`, the entry points, the control flow, `render`/`renderOf` and the `Component` module of declaration helpers.
- `Elements` (generated, `Generated/HtmlTemplateSugar.fs`): one class per tag.

Both stay in `experiments/Ranvier.View.Portable`, after `Generated/HtmlTemplate.fs`, for v1's reason: they need the internal `TemplateView` constructor and `TemplateParts`. This layout assumes v1's PascalCase members are removed (§4.8, §8 Q1); if they are kept, v2 takes the probe's namespace, `Ranvier.View.Foundation.Free`, instead.

**Two scope rules,** both documented on the namespace and in the docs page:

- **A file that opens the sugar does not open `Ranvier.View.Foundation.Generated.Html`.** Html declares a lowercase function for every tag and its own `Attrs`, `Props` and `Events` modules. Opened after the sugar, Html shadows all of them (`div (Attrs.className "x") { ... }` fails with FS0001, FS0193 and FS0740 "Invalid record, sequence or computation expression"). Opened before, the sugar shadows Html. Either order leaves one vocabulary unusable, so the rule is one vocabulary per file.
- **A local value named like a tag shadows the tag.** `let p = "note"` followed by `p () { "x" }` fails with FS0003. One-letter tags (`a`, `b`, `i`, `p`, `q`, `s`, `u`) meet this most often; a one-letter lambda parameter shadows the tag only inside its own body.

### 4.1 Elements as free tags

The generator (`tools/Ranvier.Dom.Generate`) emits one generic class per tag, 112 in all. A normal tag inherits `Element<'native,'i>`, whose CE body is the child list. A void tag inherits `VoidElement<'i>`, which has no CE members, so children on a void tag remain a compile error (FS0708).

```fsharp
type div<'i> private (ps: Part<Dom.HTMLDivElement, 'i> array, primary: unit) =
    inherit Element<Dom.HTMLDivElement, 'i>(Div, ps)
    new([<ParamArray>] ps: Part<Dom.HTMLDivElement, 'i> array) = div<'i>(ps, ())

type input<'i> private (ps: Part<Dom.HTMLInputElement, 'i> array, primary: unit) =
    inherit VoidElement<'i>(TemplateParts.element Input ps [||])
    new([<ParamArray>] ps: Part<Dom.HTMLInputElement, 'i> array) = input<'i>(ps, ())
```

- **A tag is a class.** A `let` function cannot take `[<ParamArray>]`; a constructor can. Oxpecker.Solid's tags are types for the same reason.
- **The private primary constructor is the Fable 5.18.0 workaround** (§4.5). The `primary: unit` parameter separates it from the public ParamArray constructor.
- **The tag expression** is v1's `tagExpression`: a `Tag` union case where one exists (`Div`, `Button`, `Input`, `Select`, `Option`), otherwise `HtmlTag "<tag>"`.
- **Names.** The raw lowercase tag name, except where the name is an F# keyword or an FSharp.Core type of arity 1. Among the 112 tags that is exactly two: **`base'`** (`base` is a keyword: FS0010 in a type name) and **`option'`** (a generic `option<'i>` has arity 1 and shadows `'T option`: `let c: string option = Some "x"` fails with FS0001). Oxpecker keeps `option` because its tags are not generic. `object`, `select` and the 28 other names the probe checked for collisions (among them `map`, `data`, `title`, `label`, `source`, `style`, `template`, `slot`, `form`, `input` and the one-letter tags) keep their raw spelling: they compile with FSharp.Core, Fable.Core, `Ranvier`, `Representation` and `Template` open, and `query { select }`, `Option.map`, `Map.empty` and `List.map` keep working.

`Element<'native,'inst>` carries v1's `ElementBuilder` members unchanged (`Yield` for a view, a string, an `'inst -> string` and a `Source`; `YieldFrom`, `Zero`, `Combine`, `Delay`, `For`, `Run`), each with the explicit `TemplateView<'inst> array` return type, plus `Yield(element: VoidElement<'inst>)`, which yields `element.View`.

**Entry points.** `Attrs`, `Props`, `Events` and `Hooks` are type functions over v1's builders, so every generated member (`className`, `data`, `aria`, `Props.Button.disabled`, `Props.Input.value`, `Events.onInput`, ...) is reused without regeneration:

```fsharp
let Attrs<'inst> : AttributeBuilder<'inst> = AttributeBuilder<'inst> ()
let Props<'inst> : PropertyBuilder<'inst> = PropertyBuilder<'inst> ()
let Events<'inst> : EventBuilder<'inst> = EventBuilder<'inst> ()
let Hooks<'inst> : HookBuilder<'inst> = HookBuilder<'inst> ()

let inline from<'inst, ^s, 'value when ^s: (member Value: 'value)> ([<InlineIfLambda>] accessor: 'inst -> ^s) : Source<'inst, 'value> = ...
let text (read: 'inst -> string) : TemplateView<'inst> = TemplateParts.readText read
let fragment (children: TemplateView<'inst> array) : TemplateView<'inst> = ...
```

A module cannot replace the type functions: the overloads that tell a value, a reader and a `Source` apart are members, and module functions cannot be overloaded. `from` is statically resolved so that one name accepts `Signal` and `Memo`; it emits one call fewer than `t.from`.

**Inference rule.** This paragraph replaces v1 §4.1's bare-lambda paragraph and goes into the docs verbatim:

> A lambda gets its instance type from its own body. A record label read resolves to the latest record in scope that declares the label. Annotate a lambda that reads a label also declared by a later record, and every lambda that reads a member of a class instance: a class component, `Signal`, `Memo`, or any other class.

The common cases:

| Lambda | Annotation |
|---|---|
| reads a record field whose label only that record declares (`fun r -> string r.Local.Value`) | none |
| reads a record field inside that record's own static members (§4.6) | none |
| reads a label a later record also declares (`fun (row: KeyedRow) -> string row.Id`, where `ArmRow` also declares `Id`) | the record type |
| reads a class member, including an item list held in a `Signal` (`fun (rows: Signal<_ array>) -> rows.Value`) | the class type |
| a non-overloaded `Events.*` entry after an annotated sibling in the same tag's parts | none |

`t.text` was v1's fix for the shared-label case. The free `text` adds nothing for it, because it cannot carry the instance type either. Inside `Component.define setup (fun t -> ...)`, `t.` members still carry the instance type top-down and still mix with free tags (§4.2), so a view that would need many annotations can be written with `t.` instead.

The benchmark row from `FrameworkFreePerf.fs`, 0 annotations, equal `skeletonLayout` to v1's explicit and sugared rows:

```fsharp
let perfRowFree =
    Component.template
        (fun _ (props: PerfProps) ->
            { Index = props.Index; Source = props.Source; Disabled = props.Disabled; Local = Api.createSignal 0 })
        (div (Attrs.id (fun r -> "row-" + string r.Index), Attrs.className "panel", Attrs.title (from (fun r -> r.Source))) {
            input (Props.Input.value (from (fun r -> r.Source)))

            button (Props.Button.disabled (from (fun r -> r.Disabled)), Events.onClick (fun r _ -> r.Local.Value <- r.Local.Value + 1)) {
                fun r -> string r.Local.Value
            }
        })
```

### 4.2 Control flow

Every control-flow form is a free function that takes views where v1 took builder functions. Each creates its row or arm definition when it is called, which is while the enclosing view is built, and lowers to v1's region.

```fsharp
let armOf (view: TemplateView<unit -> 'payload>) : Component<unit -> 'payload> =
    Component.define (fun _ payload -> payload) (fun _ -> view)

let arm (definition: Component<unit -> 'payload>) (payload: 'payload) : Arm = ...
let matchWith (read: 'inst -> 'union) (arms: 'union -> Arm) : TemplateView<'inst> = ...

let forEach (key: 'item -> 'key) (items: 'inst -> 'item array) (row: TemplateView<unit -> 'item>) : TemplateView<'inst> =
    TemplateBuilder<'inst>().each key items (Component.define (fun _ (current: unit -> 'item) -> current) (fun _ -> row))

let forEachFixed (key: 'item -> 'key) (items: 'inst -> 'item array) (row: TemplateView<'item>) : TemplateView<'inst> =
    TemplateBuilder<'inst>().each key items (Component.define (fun _ (current: unit -> 'item) -> current ()) (fun _ -> row))

let show (condition: 'inst -> bool) (content: TemplateView<'inst>) : TemplateView<'inst> = ...  // v1 lowering, shared empty arm
let showElse (condition: 'inst -> bool) (content: TemplateView<'inst>) (fallback: TemplateView<'inst>) : TemplateView<'inst> = ...
```

`showElse`, and the free `each`, `branch` and `boundary` (direct ports of the `t.` members), were not compiled by the probes; delivery step 2 compiles and twins each. `isolated`, `view`, `Unsafe` and `Document` have no free form in v2 (§8 Q4); a view that needs them uses `Component.define setup (fun t -> ...)` and calls `t.isolated`, `t.view` or `t.Unsafe` inside free tags.

**Rows keep their guarantee.** The row view's own instance type, `unit -> 'item` for `forEach` and `'item` for `forEachFixed`, is unified with every part and child inside it, so a read of the outer instance inside a row is a compile error (FS0041 through `text`, FS0193 through a part or a pre-bound outer part).

**Arms are hoisted.** An arm definition is bound with `let`, at module level or before the view. A `static member` property holding one creates a new definition on every read of its getter. The arm fixture from `KeyedRowsFreePerf.fs`, 1 annotation and 3 definitions:

```fsharp
let private plain = armOf (span (Attrs.className "a") { "A" })
let private counted = armOf (b () { fun payload -> string (payload ()) })

let private armList =
    Component.template (fun _ (rows: Signal<ArmRow array>) -> rows) (
        table () {
            tbody () {
                forEachFixed (fun row -> row.Id) (fun (rows: Signal<_ array>) -> rows.Value) (
                    tr () {
                        td () { fun row -> string row.Id }

                        td () {
                            matchWith (fun row -> row.Mode.Value) (function
                                | Plain -> arm plain ()
                                | Counted count -> arm counted count)
                        }
                    }
                )
            }
        }
    )
```

`counted`'s payload type comes from `arm counted count`; the v1 annotation `(a: TemplateBuilder<unit -> int>)` has no counterpart, because there is no builder parameter. The keyed fixture, 3 annotations (the key lambda and the `Id` cell for the shared `Id` label, the item list for `Signal`):

```fsharp
let private keyedList =
    Component.template (fun _ (rows: Signal<KeyedRow array>) -> rows) (
        table (Attrs.className "table") {
            tbody () {
                forEachFixed (fun (row: KeyedRow) -> row.Id) (fun (rows: Signal<_ array>) -> rows.Value) (
                    tr () {
                        td (Attrs.className "col-md-1") { fun (row: KeyedRow) -> string row.Id }
                        td (Attrs.className "col-md-4") { a (Events.onClick (fun _ _ -> clicks <- clicks + 1)) { from (fun row -> row.Label) } }

                        td (Attrs.className "col-md-1") {
                            a (Events.onClick (fun _ _ -> clicks <- clicks - 1)) {
                                span (Attrs.className "glyphicon glyphicon-remove", Attrs.attribute ("aria-hidden", "true")) { }
                            }
                        }

                        td (Attrs.className "col-md-6") { }
                    }
                )
            }
        }
    )
```

v1's `forEach`/`forEachFixed` semantics (live reader against snapshot row instance), its `show` lowering and its shared, lazily created empty arm carry over unchanged. The empty arm moves with `show` into the v2 `TemplateSugar.fs`.

### 4.3 Lowering summary

| Sugar | Lowers to | Extra work per instance |
|---|---|---|
| `div (parts) { children }` | `TemplateParts.element Div parts children` | none |
| `input (parts)` (void) | `TemplateParts.element Input parts [||]`, yielded through `VoidElement.View` | none |
| string, reader or `Source` yield | v1: `TemplateParts.text`, `readText` or `sourceText` | none |
| `armOf view` | `Component.define (fun _ p -> p) (fun _ -> view)` | none |
| `forEach key items row` | `each key items (Component.define (fun _ c -> c) (fun _ -> row))` | none |
| `forEachFixed key items row` | `each key items (Component.define (fun _ c -> c ()) (fun _ -> row))` | none |
| `show cond content` | v1's `matchWith` with a `ValueOption` selector and the shared empty arm | v1's: two markers, the arm memo, one `Arm` and one `ValueSome` per selector evaluation |
| `render definition props` / `renderOf definition value` | `TemplateBuilder<'inst>().render definition props` / `(fun _ -> value)` | none |
| `Component.template setup view` | `Component.define setup (fun _ -> view)` | none |
| `Component.ofMembers<T, _> ()` | `Component.define T.Create (fun _ -> T.View)`; Fable emits a direct reference to `T.Create` | none |
| `Component.ofClass Ctor` | `Component.define (fun s p -> Ctor (s, p)) (fun _ -> T.View)` | Fable: one 2-element array per instance setup; member reads are getter calls (§4.6) |
| a tag class over `ComponentTag` | `render definition props` | none |

The compiled and expanded paths receive ordinary `TemplateNode` trees. No backend changes.

### 4.4 Costs

- **Runtime per instance:** none, except `Component.ofClass` (§4.6).
- **Runtime per definition, once:** one tag-class instance per element (v1: one `ElementBuilder`); one `AttributeBuilder` (or `PropertyBuilder`, `EventBuilder`) per part, where `t.Attrs` returned a cached one; one `TemplateBuilder` per control-flow call; v1's Delay closures and `Array.append`s. All at module initialisation.
- **Bundle:** one class plus its constructor helpers per tag used, `Element`, `VoidElement`, and the free functions used. Rollup keeps only used tag classes (10 in the probe entry). Measured in the probe: +876 B gzip over explicit, +299 B over v1's sugar (§9).
- **A module-level `Dictionary` pulls in `Reflection.js`.** The probe's first bundle run measured +1,263 B gzip because of one. v2 code keeps .NET-only caches under `#if !FABLE_COMPILER`.

### 4.5 Fable and F# compatibility

Probes run on F# 10 (FSharp.Core 10.1.401), net10.0 Release and Fable 5.18.0 Release and Debug, with jsdom.

| Assumption | Result (probes, 2026-10-20) |
|---|---|
| `div (args) { ... }` parses as a CE on a constructor application | Proved, for all 112 tags. |
| The instance type flows down from `Component.define`, an enclosing element or a sibling | **Refuted** (F1–F10): FS0193, FS0001 or FS0041 depending on the site. |
| A type argument on a tag fixes the instance | **Refuted**: FS0041 "No overloads match for method 'span`1'". |
| Fable compiles a ParamArray primary constructor | **Refuted on 5.18.0.** It emits `constructor(...ps)` together with a `$ctor(...ps) { return new X(ps) }` helper, which nests the parts array one level deeper; jsdom fails with `TypeError: Cannot read properties of undefined (reading '0')` in `elementNode`. |
| Fable compiles a class with no primary constructor, `new(p) = { inherit Base(..) }` | **Refuted on 5.18.0.** It emits `class X { constructor() {} }` with no `extends`, and a `$ctor` that calls a nonexistent `Base_$ctor_...`; Node fails with "does not provide an export named ...". |
| A private primary constructor with public secondary constructors works around both | Proved. It emits `class X extends Base { constructor(ps, primary) { super(...); } }`. |
| `base'` and `option'` compile and leave FSharp.Core usable | Proved for `base'` (it also wins over Html's ``` ``base`` ```); `option'` was kept as `option` in the probe to show the collision, so the rename is checked in delivery step 1. |
| Free tags, entry points, control flow and helpers emit no `curry` or `partialApply` | Proved: 0 matches in `FreeSugar.js`, `FreeElements.js`, `ComponentDecl.js` and the fixture modules. |
| Static abstract interface members under Fable 5.18.0 | **Refuted**: the compiler exception in §3.3. |
| SRTP sees explicit interface implementations | **Refuted**: FS0001. |
| A component CE flows the instance type into the view | **Refuted**: FS0072. |
| A constructor constraint with arguments (`^T: (new: Setup * 'props -> ^T)`) | **Refuted**: FS0700 "'new' constraints must take one argument of type 'unit'"; `ofClass` takes the constructor as a function instead. |
| The sugar's `Component` module merges with `Template.Component` under one name | Partly proved: with the free namespace open, `Component.define` and `Component.template` resolve in one file (`DeclFixtures.fs`). `ofMembers` and `ofClass` in that module are unchecked (probe 3 used `Declare`); delivery step 2 checks them. |

The generator's workaround is the only shape it emits for tag classes, and the docs show the same shape for hand-written component tag classes. Both Fable defects are reported upstream as a follow-up (§7).

### 4.6 Declaring components

All styles return a plain `Component<'props>` from one `Component.define` call, so `Mounting.mount`, `Hydration.hydrate`, `Server.render`, the Debug definition-in-read check and twin equality apply to them unchanged. They mix freely: a record parent renders a class child, a class parent renders a record child, and an explicit `t.render` root renders both (probe 3's mixed tree: hash `9f5e7312` for all three spellings, equal server HTML on .NET and under Fable, `Strict` hydration with 39 of 39 server nodes adopted).

**Public names.** The helpers live in the sugar's `Component` module: `Component.template`, `Component.ofMembers` and `Component.ofClass`, beside the core's `Component.define`. One module name holds every way to make a definition, so completion on `Component.` lists all four styles together, and the docs' ranking maps to one prefix. The probe's `Declare` module is not carried; it was a probe-isolation name. `render` and `renderOf` are free functions in `Builders`, since they are view parts rather than declarations.

The four styles, in the docs' ranking:

**1. Record with static `Create` and `View`, `Component.ofMembers`.** Setup and view live on one type with 0 annotations: inside a record's own static members, a label read resolves to that record, because it is the latest declaration of the label at that point (this holds for a label shared with a props record, such as `Caption`).

```fsharp
type CounterProps = { Start: int; Caption: string }

type Counter =
    { Count: Signal<int>; Caption: string }

    static member Create(_: Setup, props: CounterProps) =
        { Count = Api.createSignal props.Start; Caption = props.Caption }

    static member View =
        div (Attrs.className "counter") {
            span (Attrs.className "caption") { fun c -> c.Caption }

            button (Attrs.attribute ("type", "button"), Events.onClick (fun c _ -> c.Count.Value <- c.Count.Value + 1)) {
                fun c -> string c.Count.Value
            }
        }

let counter = Component.ofMembers<Counter, _> ()
```

```fsharp
let inline ofMembers< ^T, 'props
    when ^T: (static member Create: Setup * 'props -> ^T) and ^T: (static member View: TemplateView< ^T >)> () : Component<'props> =
    Component.define
        (fun setup props -> (^T: (static member Create: Setup * 'props -> ^T) (setup, props)))
        (fun _ -> (^T: (static member View: TemplateView< ^T >) ()))
```

Fable emits `Component_define(Counter_Create_..., (_arg) => Counter_get_View())`: one function reference, with no tuple and no wrapper lambda. A record without `Create` or `View` fails with FS0001 naming the missing member.

**2. `Component.template setup view`.** For small and one-off components; 0 annotations when the instance is a record. The view argument is evaluated once, where the definition is bound.

```fsharp
let template (setup: Setup -> 'props -> 'inst) (view: TemplateView<'inst>) : Component<'props> =
    Component.define setup (fun _ -> view)
```

**3. Class with constructor setup and static `View`, `Component.ofClass`.** For authors who want constructor-as-setup and private state. Every instance lambda is annotated with the class type (decision 3); the only exception is a non-overloaded `Events.*` entry after an annotated sibling. A missing annotation surfaces as FS0193 on the whole view ("`TemplateView<X>` is not compatible with `TemplateView<CounterClass>`") when `View` is annotated, or FS0001 at `Component.ofClass` when it is not; annotating `View` is optional and moves the error to the right line.

```fsharp
type CounterClass(setup: Setup, props: CounterProps) =
    member val Count = Api.createSignal props.Start
    member _.Caption = props.Caption

    static member View: TemplateView<CounterClass> =
        div (Attrs.className "counter") {
            span (Attrs.className "caption") { fun (c: CounterClass) -> c.Caption }

            button (Attrs.attribute ("type", "button"), Events.onClick (fun (c: CounterClass) _ -> c.Count.Value <- c.Count.Value + 1)) {
                fun (c: CounterClass) -> string c.Count.Value
            }
        }

let counterClass = Component.ofClass CounterClass
```

```fsharp
let inline ofClass ([<InlineIfLambda>] create: Setup * 'props -> ^T) : Component<'props> =
    Component.define (fun setup props -> create (setup, props)) (fun _ -> (^T: (static member View: TemplateView< ^T >) ()))
```

Costs specific to this style: Fable builds one 2-element array per instance setup (`const tupledArg = [setup_1, props]`), and every member read is a getter call (`Memo$1__get_Value(DemoClass__get_Doubled(demo_1))`, where a record reads `demo_1.Doubled`). Criterion 3 measures the class row against the explicit ranges; §8 Q5 covers the outcome. On the demo screen the style needs 8 of 10 instance lambdas annotated.

**4. Explicit `Component.define setup (fun t -> ...)`.** The documented equivalent of every style, and the form with top-down typing through `t.`.

**`View` is read once.** Each helper reads `View` inside `build`, which runs once per definition (probe 3: one read per child after mounting three instances of each). `View` is a property, so any other read builds a second view. The docs state: only the helpers read `View`.

**Components as tags.** `render definition (fun p -> props)` takes props read from the parent instance (untracked, once per instance, as `t.render`); `renderOf definition props` takes fixed props. A free function cannot be overloaded, so one name for both forms needs a class. v2 ships the abstract base and documents the 4-line per-component class, with the private-primary-constructor shape (§4.5):

```fsharp
[<AbstractClass>]
type ComponentTag<'props, 'inst>(definition: Component<'props>, props: 'inst -> 'props) =
    inherit VoidElement<'inst>(TemplateBuilder<'inst>().render definition props)

type counterOf<'i> private (props: 'i -> CounterProps, primary: unit) =
    inherit ComponentTag<CounterProps, 'i>(counter, props)
    new(props: 'i -> CounterProps) = counterOf<'i>(props, ())
    new(props: CounterProps) = counterOf<'i>((fun _ -> props), ())
```

The tag class is optional (decision 3); `render`/`renderOf` come first in the docs. In a class parent, `render`'s props lambda is annotated like any other instance lambda (probe 3 I1: FS0041 on `Yield` without it).

**Definition identity.** The Debug check covers every helper, because each calls `Component.define`: `Component.ofClass ArmClass` written inside a `matchWith` arms function raises "a template definition was created while a region was read; bind it in the template's build function" in the Fable Debug compile, and `ofMembers` behaves the same.

### 4.7 Nested Elmish

The pattern, documented on the docs page in the rank-1 style:

- **Each component's setup owns an `Mvu`.** `Create` (or the class constructor, or the `template` setup) calls `Mvu.create` or `Mvu.withCmd`; the instance holds `Dispatch` and the `Select` memos the view reads.
- **Props reader down.** The parent passes a props reader over its own selector memo, `render child (fun p -> { Step = p.CurrentStep; Report = fun n -> p.Loop.Dispatch (ChildReported n) })`. A parent message that leaves the selected value unchanged causes 0 child reads (probe 3: `Rename` gave +0 step-slot reads; `Bump` gave +1).
- **Callback up.** The child keeps its own `Msg` type and reports through a callback in its props. The parent has one case for the report (`ChildReported of int`) and lifts no child messages.

Probe 3 proved this pattern for selectors, props, callbacks, server render and `Strict` hydration. In the probe's MVU pair, hydration adopted 11 of 18 server nodes, because the child sits in a `show` arm and arms mount fresh in today's hydration; that is existing hydration behaviour, outside this spec.

**The disposal gap (dependency, not v2 work).** The child's `Select` memos are owned by its scope and stop on disposal. Its loop is not owned: the model is a graph `Signal`, and commands receive the raw `Dispatch`. A `dispatch` held by a timer or subscription keeps running `update` and commands after the child is disposed, including outbound callbacks into a live parent (probe 3: the parent's last report went from `2` to `204` after disposal). This is a property of `Mvu` itself and of every declaration style. The fix belongs in `Ranvier.Elmish`, as a follow-up with probe 3's tested shape:

```fsharp
module OwnedMvu =
    let withCmd (setup: Setup) (init: 'model * (('msg -> unit) -> unit) list) (update: 'msg -> 'model -> 'model * (('msg -> unit) -> unit) list)
        : Mvu<'model, 'msg> * ('msg -> unit) =
        let mutable alive = true
        setup.OnCleanup (fun () -> alive <- false)
        let gate (command: ('msg -> unit) -> unit) = fun dispatch -> command (fun msg -> if alive then dispatch msg)
        let model, commands = init
        let loop = Mvu.withCmd (model, List.map gate commands) (fun msg model -> let next, commands = update msg model in next, List.map gate commands)
        loop, (fun msg -> if alive then loop.Dispatch msg)
```

After disposal, a held dispatch through this constructor reaches nothing (probe 3: reports +0). The final name and placement (a `Setup`-taking overload in `Ranvier.Elmish` or a View-side bridge) are the follow-up's decision. v2 ships no `Mvu`-specific declaration style: an instance shape such as `{ Model selectors; Dispatch }` would fix per-view selector choices. Until the follow-up lands, the docs page states the gap next to the pattern (§8 Q7).

### 4.8 The v1 `t.` sugar

v1's sugar layer is the PascalCase `t.Div` extension members (112 generated in `Generated/HtmlTemplateSugar.fs`), `ElementBuilder`, and the `t.armOf`, `t.forEach`, `t.forEachFixed`, `t.show` and `t.showElse` members in `TemplateSugar.fs`. The explicit lowercase `t.div parts children` members and the other `t.` members are core, not sugar, and stay.

**Recommendation: remove the v1 sugar layer** once its fixtures are migrated (§6.8), in the same branch. The costs of each option:

| Option | Cost |
|---|---|
| **Remove (recommended)** | One migration pass over 7 files and `check-types.py`'s sugar cases (§6.8). v1's criterion 3 and 4 runs are repeated against the migrated adapters. `Ranvier.View.Portable` is `IsPackable=false`, so no external caller breaks. |
| Deprecate (`[<Obsolete>]` on the v1 members) | Both layers are generated, tested and documented for a release; every v1 fixture compiles with warnings until migrated; the namespace moves to `Ranvier.View.Foundation.Free`. |
| Keep | Two sugar spellings indefinitely, two generated files of 112 members each, two sets of negative checks, and a docs page that has to explain when to pick which. |

Removing keeps one spelling on top of the explicit form. The `t.`-carried top-down typing stays available through the explicit members (§4.1).

## 5. Error handling

- **Compile time.** Negative cases go in `check-types.py`, ported to the free spelling:

  | Mistake | Code |
  |---|---|
  | An element-specific property inline on another element (`div (Props.Input.value (from ...)) { }`) | FS0041 "No overloads match for method 'div`1'" |
  | A generated part of another element (`span (Props.Input.value "x")`) | FS0041 |
  | A part bound with another element's type (`Part<HTMLButtonElement, unit>` passed to `span`) | FS0001 |
  | Children on a void tag (`input () { "x" }`) | FS0708 |
  | The outer instance inside a row through `text` | FS0041 |
  | The outer instance inside a row through a part, or a pre-bound outer part | FS0193 |
  | A `Program.View` in a body | FS0041 |
  | A lambda whose label resolves to another record | FS0001, FS0193 or FS0041, by site |
  | An unannotated class-member read | FS0001 (the label resolves to an unrelated record) or FS0072 (no record declares it) |
  | `base` as a tag name | FS0010 |
  | `Generated.Html` opened after the sugar | FS0001, FS0193, FS0740 |
  | A local value named like a tag, used as one | FS0003 |
  | `Component.ofMembers` on a type without `Create`, or `Component.ofClass` on a type without `View` | FS0001 "lacks the required member" |

  Positive cases: `let c: string option = Some "x"` and `defaultArg (x: int option)` compile with the sugar open; all 112 tags compile in one body; `Component.define` and `Component.ofMembers` resolve in one file.
- **Definition time.** Unchanged from v1.
- **A definition created during an update.** v1's Debug check (`DEBUG && FABLE_COMPILER`, `RegionReads.depth`) covers every v2 form and helper unchanged. Two v2 ways to reach it are documented: a `static member` holding an `armOf` definition read inside an arms function, and a declaration helper called there.
- **Runtime.** Unchanged.

## 6. Testing strategy

1. **Structural equivalence.** `SugarChecks.fs` in the free spelling: each form and each declaration style against its explicit twin (layout field by field, `structuralHash`, `Server.render` on .NET and under Fable), from fixtures in `Ranvier.View.Portable/Checks` linked into both.
2. **Behavioural equivalence.** v1's cases in the free spelling, plus: the demo screen in all four declaration styles; the mixed record/class parent-child tree with `render`, `renderOf` and a tag class; build-once of static `View`; synchronous and queued commit modes.
3. **Hydration.** Every declaration style and the mixed tree, under `Strict`, from the twin's HTML and from its own, followed by updates.
4. **Nested Elmish.** The pattern of §4.7 as a fixture: selector isolation, props down, callback up, server render and hydration. The disposal case (a held child `dispatch` after the child is hidden reaches nothing) is written now and marked skipped, with a link to the `Ranvier.Elmish` follow-up that enables it.
5. **Debug.** The misuse fixtures (`ofClass` and `ofMembers` inside an arms function) fail in the Fable Debug compile and mount in Release; the Release JS has no counter.
6. **Compiler-negative tests.** §5's table in `check-types.py`, one case per row, each pinned to its code; the generated-file assertion counts 112 tag classes.
7. **Performance.** The `ranvier-template-sugar` adapter of `run.mjs --template-comparison`, the sugared rows of `keyed-rows.mjs` and `keyed-heap.mjs`, and the bundle entries of `sugar-size.mjs` switch to the free fixtures under their v1 names, so v1's harness and comparisons stay. Add an `ofMembers` row adapter and an `ofClass` row adapter to the template comparison. Take an allocation sample of a 128-row mount and search its frames (criterion 3). Paired, alternating runs, only when no other benchmark uses the machine.
8. **Fixture migration.** Each v1 sugar fixture is rewritten in place in the free spelling; the probe files supply the rewrites:

   | v1 file | Rewrite from |
   |---|---|
   | `experiments/Ranvier.View.Portable/Checks/SugarFixtures.fs` | `FreeFixtures.fs`, `DeclFixtures.fs` |
   | `experiments/Ranvier.View.Portable/Checks/SugarServerChecks.fs` | `probe/DeclProbe` |
   | `tests/FrameworkSugarPerf.fs` | `FrameworkFreePerf.fs` |
   | `tests/KeyedRowsSugarPerf.fs` | `KeyedRowsFreePerf.fs` |
   | `tests/PlaygroundSugar.fs` | `FreeFixtures.demoFree`, `DeclFixtures.demoMembers` |
   | `tests/SugarChecks.fs`, `tests/SugarDebugChecks.fs` | `freeCases`, `DeclChecks.fs` |
   | `tools/Ranvier.Dom.Generate/check-types.py` | `probe/FreeProbe`, `probe/Collide`, `probe/DeclProbe` `Inference.fs` |

   The `.mjs` runners (`sugar*.test.mjs`, `sugar-*.mjs`) keep their names and gain the declaration cases. `KeyedRowsMatchPerf.fs` and the `match!` adapters are not carried.
9. **Permanent no-sugar app.** Unchanged from v1.

## 7. Delivery order

1. **Generator.** `tools/Ranvier.Dom.Generate` emits the 112 tag classes into `Generated/HtmlTemplateSugar.fs`, with the private-primary-constructor shape and `base'`/`option'`. Byte-identical regeneration; `check-types.py` counts and compiles every tag.
2. **Handwritten layer.** `TemplateSugar.fs`: `Element`, `VoidElement`, `ComponentTag`, the entry points, the control flow (including the uncompiled ports `showElse`, `each`, `branch`, `boundary`), `render`/`renderOf`, the `Component` module and the empty arm. Check the `Component` module merge with `ofMembers` and `ofClass`, and compile the record-to-record pair of §4.6 (probe 3 compiled only the crossed record/class pair).
3. **Fixtures and checks.** §6.1–§6.6 and §6.8.
4. **Remove the v1 sugar layer** (§4.8), if approved.
5. **Measurements.** Criteria 3, 4, 6 and 7, recorded in `docs/research/2026-10-20-view-sugar-v2.md` and summarised in §9.
6. **Docs.** The docs page: the ranking of §4.6, the inference rule of §4.1, the two scope rules of §4, the arm-hoisting and `View` rules, the nested Elmish pattern with the disposal gap stated, and the probe reports copied into `docs/research/`.

**Follow-ups outside this spec:**

- `Ranvier.Elmish`: an owned `Mvu` constructor in the shape of `OwnedMvu.withCmd` (§4.7).
- Fable: report the ParamArray primary-constructor and missing-primary-constructor miscompilations upstream; the generator keeps the workaround until a fixed Fable is pinned.
- Fable: re-test static interface calls on each Fable upgrade; `Component.ofType` follows if they compile (§8 Q9).

## 8. Open questions (recommended answers)

1. **v1's PascalCase `t.Div` layer: keep, deprecate or remove?** Recommended: remove, after migrating its fixtures (§4.8). Cost: one migration pass and a repeat of v1's criterion 3 and 4 runs.
2. **Namespace.** Recommended: reuse `Ranvier.View.Foundation.Sugar` if Q1 is answered "remove"; otherwise `Ranvier.View.Foundation.Free`, as in the probes.
3. **Should `Generated.Html` become `[<RequireQualifiedAccess>]` to remove the open-order hazard?** Recommended: no in v2. It would change every authored-path call site from `div` to `Html.div`. The one-vocabulary-per-file rule and its negative check cover the hazard.
4. **Free forms of `isolated`, `view`, `Unsafe` and `Document`?** Recommended: not in v2. They are reached through `t.` inside `Component.define`; port them when a fixture needs them.
5. **`Component.ofClass`'s per-instance array.** Recommended: ship it ranked third with the cost documented if the class row adapter's ranges overlap the explicit ones (criterion 3). If they do not, look for a constructor shape that Fable calls without the tuple before shipping.
6. **A one-type-argument `Component.ofMembers<Counter> ()`?** Recommended: keep `<Counter, _>`. F# requires every explicit type argument or none, so the `_` stays unless a one-parameter shape exists; the probe did not try one, and delivery step 2 may.
7. **Should the nested Elmish docs wait for the `Ranvier.Elmish` follow-up?** Recommended: no. Publish the pattern with the disposal gap stated and the follow-up linked, and switch the example to the owned constructor when it lands.
8. **Bundle budget.** Recommended: keep v1's 1 kB gzip over explicit for the benchmark adapter set, now including the declaration helpers (probe: 876 B without them).
9. **`Component.ofType` over `IComponent` on a later Fable?** Recommended: add it when a pinned Fable compiles static interface calls, with the same `Create` and `View` member names, so `ofMembers` records only gain the `interface ... with` lines.
10. **Ship the tag-class base `ComponentTag` in v2, or leave tag classes entirely to users?** Recommended: ship it. It is one abstract class, and its private-primary-constructor shape is the part users get wrong under Fable 5.18.0.

## 9. Measurements

Not yet measured on the implementation. The probe figures below (2026-10-20, Ryzen 9 9900X, Chromium 153.0.8010.12, at `df3bb0b`) are the baseline the implementation has to reproduce.

**Twin equality (criteria 1, 2).** For the demo screen, the keyed fixture, the arm fixture and the benchmark row, the free twins match the explicit twins on layout signature, `structuralHash` and server HTML, and show identical markup, reports, core counter deltas, `addEventListener` registrations and MutationObserver writes (`free-counters.test.mjs`, 13 pass). The demo in every declaration style shares hash `21e93577` with `demoExplicit` and hydrates 38 of 38 server nodes under `Strict`; the mixed tree shares `9f5e7312` and hydrates 39 of 39. `decl.test.mjs`: 19 of 19 pass in Release; in Debug, the two misuse cases fail as intended.

**Arm timing (criterion 3, partial).** Free `matchWith` against explicit `t.matchWith`, paired same-round ratios over 45 samples per cell: mount 1.049 (Q1–Q3 0.945–1.117), switch 1.001 (0.910–1.131), payload update 0.946 (0.863–1.185). Allocation per operation equals explicit within 1 % (5.59 MB, 2.15 MB, 121.6 kB against 5.59 MB, 2.16 MB, 122.5 kB). Keyed operations, `keyed-heap.mjs`, the template comparison and the frame search were not run by the probes.

**Bundle (criterion 4).** Entries hold the benchmark row and the keyed and arm fixtures:

| Entry | raw | gzip | Brotli | Δ gzip vs explicit |
|---|---|---|---|---|
| explicit | 204,474 | 58,784 | 51,572 | — |
| v1 sugar | 207,281 | 59,361 | 52,020 | +577 |
| free tags | 208,580 | 59,660 | 52,200 | **+876** |

The declaration helpers were not in these entries. 0 `curry`, `uncurry` or `partialApply` in the emitted sugar, helper and fixture modules.

**Authoring (criterion 6).**

| Fixture | Annotations, v1 sugar (written / needed) | Annotations, v2 | Non-whitespace characters, v1 → v2 |
|---|---|---|---|
| Demo screen | 10 / 0 | 0 (records), 8 (`ofClass`) | 2,436 → 2,135 (`Component.template`, setup included) |
| Keyed rows | 4 / 1 | 3 | 637 → 559 |
| Arm rows | 6 / 0 | 1 | 537 → 484 |
| Benchmark row | 6 / not measured | 0 | not measured |

The arm fixture keeps 3 definitions (the list and two hoisted arms).

## References

- The [view sugar design](2026-10-19-view-sugar-design.md) (v1) and its [research note](../../research/2026-10-20-view-sugar.md).
- Oxpecker.Solid tags as types, `src/Oxpecker.Solid/Tags.fs` (develop), and its README. https://github.com/Lanayx/Oxpecker/blob/main/src/Oxpecker.Solid/README.md
- Myriad 1.1.0 on NuGet (Myriad.Sdk, Myriad.Plugins, Myriad). https://www.nuget.org/packages/Myriad.Sdk
