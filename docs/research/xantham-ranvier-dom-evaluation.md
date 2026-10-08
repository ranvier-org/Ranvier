# Xantham generation for Ranvier DOM

Date: 2026-10-08. Status: architectural assessment, not an implemented or compiler-validated pipeline.

## Verdict

Use Xantham as the source of typed DOM knowledge and as build-time generation infrastructure. Let Ranvier own the generated public vocabulary and the runtime semantics. The promising path is a Ranvier-owned generation pass consuming Xantham's semantic snapshot, not assuming that its stock property companion hook can generate the whole renderer-facing scaffold.

Upstream capabilities and constraints are recorded in [the source investigation](xantham-generation-sources.md). Ranvier requirements come from the [proposed framework brief](../superpowers/specs/2026-10-06-fable-ranvier-framework-brief.md), especially C01, C04, C06, C07, C09 and C10. Existing behavior is documented in the [DOM PoC README](../../src/Fable.Ranvier/README.md); the proposal is not an implementation audit.

## Proposed pipeline

1. Pin TypeScript DOM declarations and Xantham's generator/library version.
2. Use Xantham to resolve declarations and produce semantic snapshots and native bindings.
3. Apply a versioned Ranvier mapping: tag/interface identity, inherited writable properties, event maps, names, nullability and explicit exceptions.
4. Generate reusable element definitions and distinct static/reactive property, attribute and listener helpers. Delegate behavior to Ranvier's small handwritten runtime.
5. Validate deterministic regeneration, compile generated examples in Release Fable, and exercise the resulting browser behavior.

The snapshot adapter must establish that the required tag maps, inheritance and event types survive in a usable form. The public customization contract currently uses opaque binding-type handles and a display string; it does not expose a complete structured type decomposition or dedicated tag-map/event-map queries. Follow-up inspection found that the wider generator API already exposes `FsTypeRef`, `FsMember`, `FsDecl` and `RenderModel.Decls`: a Ranvier-owned emitter can inspect declarations returned by `Pipeline.generateWith` and write its own files without a new output hook. This path requires the needed declarations to be reached and shipped, and does not preserve every source-semantic distinction. Small upstream inspection and qualified-name functions would improve the richer semantic path. Avoid reverse-engineering emitted F# text or parsing display strings to reconstruct semantic types.

## Alternative: minimal generator over Xantham.TypeScript.Wire

Discussed on 2026-10-08. This option is currently being discussed in [Xantham #110](https://github.com/shayanhabibi/Xantham/issues/110). It remains a proposal; no upstream commitment or implementation is assumed.

Use `Xantham.TypeScript.Wire` directly for TypeScript checker queries and AST access, then implement a narrowly scoped Ranvier HTML model and F# scaffold emitter. Select tag maps, inherited writable properties and event maps; generate reusable views and distinct static/reactive helpers that delegate behavior to Ranvier's runtime.

Reuse `Xantham.Fable.Core.TS` native bindings throughout, including native types within the HTML domain. For example, an input scaffold uses the existing `HTMLInputElement`, and its `files` property references the existing `FileList`. Generate the Ranvier authoring surface without redeclaring these native types.

The main integration requirement is a reliable bridge from TypeScript declaration identity to the existing qualified F# binding name, generic arity and representation. Wire supplies TypeScript facts, not Xantham's F# mapping. Xantham's declaration catalog records much of this information, but the inspected Core.TS 0.1.0 package does not ship it; the current package project also has no catalog packaging item. Catalog loading and identity calculation are private, and no standalone Wire-to-catalog resolver was identified. A producer-generated binding map distributed with Core.TS, plus a small identity resolver, would make this approach substantially easier without expanding Xantham's framework customization machinery.

Pin compatible Wire/compiler versions and the TypeScript declaration inputs associated with the Core.TS package. Support a small verified type grammar: explicit primitive, optional, array, generic and callback conversion rules; catalog-resolved named types; and curated union/HTML exceptions. Unsupported shapes must produce generation diagnostics rather than silently widening to `obj`.

The attraction is direct access to the compiler's semantic data and full control of a small HTML emitter. The maintenance risk is gradually rebuilding Xantham's general TypeScript-to-F# mapper. First validate an input with `value`, `checked`, `files` and an input event to exercise primitives, native references, optionality and event typing. No Wire-based prototype has been compiled or run.

## Author's strongest case

HTML has a broad, repetitive typed surface. Generating it can remove string tag names, repeated casts and handwritten setters from application code while retaining ordinary F# functions. Fixing a naming or type-mapping rule once improves all generated helpers. Static and reactive operations can share conventions without concealing which one a caller selected. Since generation runs at build time, applications need not load Xantham's generator or parse declarations at runtime.

For the framework author, the central benefit is coverage and maintenance consistency. For the application author, the benefit is discoverable element-specific properties and event types, backed by a consistent lifetime contract. Neither benefit proves smaller bundles or faster rendering.

## Sceptical case

- **Types are insufficient behavioral specifications.** A writable boolean property does not establish boolean-attribute serialization. Live form properties, default values, removal, focus and IME behavior require explicit rules. Read-only properties must not produce setters.
- **Current customization is narrower than arbitrary scaffolding.** Property companions help adapt properties; constructors, event registration and reusable views require another generation pass or extension work. A raw interface replacement escape is a poor default for systematic HTML generation.
- **The target is moving.** The PoC constructs native nodes; the proposed framework constructs reusable definitions and allocates DOM at instantiation. Generating the former now risks rewriting the entire surface later.
- **Ownership cannot be generated from DOM declarations.** Generated listeners and bindings must delegate disposal, untracked writes, outcome handling and scheduling to Ranvier. Duplicating these behaviors in each helper multiplies failure points.
- **Browser setters cannot by themselves support server rendering.** Portable operations need metadata and curated serialization rules; arbitrary native callbacks remain explicitly browser-only.
- **A huge generated surface still has a cost.** Measure compiler time, declaration size, editor completion quality and bundled output. Upstream changes must produce reviewable diffs rather than silently expanding or breaking Ranvier's public interface.
- **Element-specific typing needs a proof.** Shared modifiers should work on derived elements, while input-only helpers must fail on unrelated elements. Typed events must not pretend every event target is the element that registered the listener.

The declaration source supplies useful tag, property and event information: [TypeScript DOM generator baseline](https://github.com/microsoft/TypeScript-DOM-lib-generator/blob/main/baselines/dom.generated.d.ts). The behavioral cautions above are design implications, not claims that Xantham implements Ranvier semantics.

## Smallest decisive experiment

Generate only `div`, `button`, `input`, `select` and `option`. Compare them with equivalent handwritten definitions using the same runtime. Prove:

- Two mounts of one definition create independent nodes, bindings and listeners.
- Generated static and reactive helpers preserve the documented differences; no policy is inferred from a getter.
- Form properties and attributes behave correctly, including false/removal and redundant input writes.
- Inherited helpers compile; incompatible element/property combinations fail to compile.
- Event types are preserved and listeners are removed on disposal.
- Disposal cancels queued writes; pending/error handling matches the chosen runtime policy.
- Two regeneration runs are identical; input/version changes produce attributable diffs.
- Release JS, bundle size and mount/update/disposal workloads remain comparable to the handwritten equivalent.
- A small portable definition can be interpreted as escaped server HTML without browser nodes or globals.

Proceed to broader coverage after these gates pass. This assessment ran no generated-output compilation, runtime tests or performance benchmarks, and changes no runtime code or existing implementation plan.
