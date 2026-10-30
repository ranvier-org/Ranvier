# Typed style map services and backend integration

## Selected contract

C13 provides actual `StylePropertyMapReadOnly` and `StylePropertyMap` objects for inline, declared and computed styles. Generated members retain get/getAll/has/size and set/append/delete/clear. The small borrowed service adds acquisition, an option-returning Get, and entry snapshots without flattening typed values or filtering nullable entries. C15 arithmetic and C17 color APIs remain deferred.

Approved shared shape:

```fsharp
type StyleMapEntry =
    { Property: string
      Values: CSSStyleValue array }

type StyleMapService =
    inherit System.IDisposable
    abstract Attribute: ElementCSSInlineStyle -> StylePropertyMap
    abstract Computed: Element -> StylePropertyMapReadOnly
    abstract Declared: CSSStyleRule -> StylePropertyMap
    abstract Get: StylePropertyMapReadOnly * string -> CSSStyleValue option
    abstract Entries: StylePropertyMapReadOnly -> StyleMapEntry array
```

The pinned inline-style mixin applies to HTML, SVG and MathML elements. The computed-map operation has no pseudo-element argument. Entry arrays preserve the engine iterator's order, actual value subtypes, and null slots permitted by the pinned IDL's `sequence<CSSStyleValue?>`. They are independent array snapshots; mutating an array slot does not update an engine declaration. Mutating a returned value similarly does not imply a map write; liveness is checked by reading again after map operations.

## Document and lifetime ownership

Browser `StyleMaps.forDocument` binds the active document/realm. Native `StyleMaps.forHost` and `StyleMaps.forDocument` bind a real document wrapper from a live host/session, validating the owning thread. All input handles must belong to that session; same-session `createHTMLDocument` objects remain supported rather than being blanket-rejected.

Each native map wrapper keeps its first acquisition document and owner wrappers reachable. Element acquisition derives `ownerDocument`. Attached declared acquisition derives `rule.parentStyleSheet.ownerNode.ownerDocument`; ownerless/detached acquisition carries the explicit service document. Existing ownership anchors already connect retained rules through rule lists/sheets to their owners; a bounded internal resolver can reuse them without a global owner registry. Cached same-object maps never silently rebind when reacquired through another service. Service Get/Entries use the map's bound document context.

Provider disposal invalidates that provider without mutating retained map/value objects. Native host/session disposal invalidates every associated handle. Document validity and expiry must be proven, including same-object reacquisition through bound services after owner-node disappearance. Retained maps anchor their owning element/rule; returned value handles are independently owned through the existing typed-value cache.

## Backend mechanics

Root owns the canonical Native producer, WebKit source/repairs, native candidate and entries transport. This lane waits for its checkpoint before changing generated contracts. The existing 1,725 exports and all prior compound layouts must remain unchanged.

- Exclude only the explicitly named `contextParameter` from logical IDL inputs when `contextReceiver=Document`. Insert the map's checked bound document handle after its target; retain existing scalar length-pair handling.
- Decode the `void` arm of `UndefinedOrCSSStyleValue` as its generated monostate case. Service Get maps only that arm to None, preserving actual typed objects in the other arm.
- Preserve null value elements in the new distinct nullable-element sequence, while retaining/releasing non-null values exactly once. Existing C14 sequence layouts and paths remain unchanged.
- Add the selected map root/cache kind and runtime mutable/read-only dispatch. Same-object acquisitions must preserve wrapper identity and context.
- Decode native entry snapshots before releasing their owned nested containers, with cleanup even when conversion fails part way through. Do not introduce iterator handles or a parallel CSS engine.

## Ordered proof

- [x] Read the pinned IDLs, existing ownership/cache implementation and root's producer plan.
- [x] Obtain approval for the exact small service/record, native document factory, and first-acquisition context rule.
- [x] Import the exact canonical producer contract and inspect the fresh generated interfaces with FCS before public signature changes.
- [x] Prove context exclusion, monostate and nullable decoding in Sage, persist the bounded backend changes, regenerate and compare all pre-existing wrapper bodies.
- [x] Implement and independently review the Core/browser/native service providers and exact public API snapshot.
- [ ] Add focused shared proof for missing values, list order/cardinality, empty variadics, grammar/property/type errors, null/foreign inputs, USV/NUL, read-only maps, inline priority and actual computed effects.
- [ ] Prove entries snapshot order/liveness/null preservation, owner/document/session/thread behavior and same-object context retention across services.
- [x] Preserve all six two-sheet COW mutations with complete survivor CSSOM and actual owning/survivor layout checks. Root's reviewed engine repairs preserve the required scope.
- [ ] Run focused Windows JIT/NativeAOT/Chromium and exact count guards, then one combined batch. Audit XML/comments/formatting and obtain independent review.
- [ ] Allocate the next cohort and producer pins with root before publication. Platform CI and released normal/traced consumers remain separate acceptance gates.

The first focused private ABI23 run passes 127 shared assertions and 33 native lifecycle assertions; actual Chromium passes the same 127 shared assertions after canonical selected-union validation repair. Exact count guards, final navigation context checks, AOT and combined gates remain in progress. Public package availability and completed platform acceptance are not claimed. The C14 branch and its CI head remain unchanged.

## Runtime staging diagnostic

Computed-map iteration initially crashed in managed execution while an equivalent raw ABI probe passed all 414 entries. Hash comparison established that the executable contained an older JavaScriptCore DLL alongside the new candidate Host/WebCore pair: PreserveNewest had retained the destination because the coherent source copy had an older timestamp. Replacing only that owned output DLL with the matching candidate bytes made the unchanged computed-entry test and complete focused fixture pass. This is a private test-staging failure, not an engine defect. Every subsequent build/publish must compare all three actual executed DLL hashes to the coherent candidate before testing; timestamps alone do not establish provenance.

The native lifetime fixture covers same-session secondary documents, ownerless declared maps first acquired through another bound service, cached map reacquisition across providers after rule removal and stylesheet owner-node disappearance, wrong-thread/session rejection, retained values after provider disposal, and invalidation after host/session disposal. Partial native snapshot conversion cleanup is independently source-reviewed; malformed snapshot injection is not claimed as runtime evidence.

## Local tool qualification

Sage project session `f9bcddfa` demonstrated that `assert false;; printfn "ASSERT_FALSE_SURVIVED";;` returns success and prints the marker: assertions are compiled away in this session context. Assertion-only snippets are not verification evidence. Explicit `if ... then failwith` produced the required RED when the extra context parameter leaked into IDL argument pairing, then GREEN after the bounded metadata repair. Subsequent pure checks use explicit failures. The daemon was not restarted. C14's final executable fixtures already use explicit failure checks and are unaffected.
