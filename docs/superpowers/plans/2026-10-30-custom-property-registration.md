# C12 custom-property registration

Implement the selected custom-property registration and `CSSPropertyRule` contract using the existing document-bound service pattern. The engine owns validation, registry lifetime and interpolation.

## Source contract

Pinned WebKit `b95c91b1c0ee43cc86d25ff7af73038f67a88e04` exposes `CSS.registerProperty` through `DOMCSSRegisterCustomProperty` and `DOMCSSCustomPropertyDescriptor`: required name/inherits, syntax default `*`, and optional initialValue. Invalid names, syntax or initial values produce SyntaxError; duplicate registrations produce InvalidModificationError. Registration belongs to the document and has no unregister operation.

`CSSPropertyRule` exposes readonly name, syntax, inherits and nullable initialValue. Absent initialValue must remain null, rather than becoming an empty string.

## Approved shared API

- `PropertyDefinition` contains Name, Syntax, Inherits and InitialValue:CustomPropertyInitialValue. `Create(name,inherits)` defaults syntax to `*` and initialValue to Omitted.
- `CustomPropertyInitialValue` is a qualified, non-erased union: Omitted or Value of string. Fable 5.13 erased Some null in a string option, including with a nullable-string annotation and null checking. An actual emitted-code probe preserves all four distinct inputs with this union: omitted, empty, null and embedded NUL.
- A borrowed `CustomPropertyService.Register` delegates validation to the engine. Dispose invalidates the provider without unregistering definitions.
- `CustomProperties.forDocument` supplies the browser provider; `CustomProperties.forHost` supplies the native provider.
- `CustomPropertyException(name,detail)` preserves engine exception names across providers. Disposal follows existing provider behavior; Fable BCL exception identity is not promised.
- Null string references convert to the text `null`, consistent with CSS query WebIDL conversion. Omitted omits initialValue; Value empty and Value null remain present. Existing UTF-8 transport limitations for malformed UTF-16 are unchanged.

## Native transport and integration

- Allocate ABI22, Native preview.14 and Core alpha.14. Existing IDs and locked structures remain unchanged.
- Add explicit length/presence input spans only for the new registration dictionary. Embedded NUL and absent versus empty values must survive transport. Reject invalid span lengths/pointers and missing required fields.
- Keep the registration capability separate from CSS supports.
- Add the four CSSPropertyRule getters with source-derived nullable metadata.
- Before final regeneration, merge authoritative Native122 named-lookup and Native13 context/counter changes forward. Preserve Core13 canonical named-lookup emits. Shared `C:/wk/src` stays read only during the parent's candidate build.

## Implementation and proof

- [ ] Generator RED/GREEN for independent namespace features, required dictionary fields, UTF-8 span decoding and preserved old layouts.
- [ ] Shared record/service, browser provider and native host provider; XML/comment audits and fresh FCS public API capture.
- [ ] Invalid/duplicate registration and exact exception names; absent/empty/NUL input distinctions.
- [ ] Registered initial/inherited values and readonly @property inspection.
- [ ] Deterministic paused animation midpoint interpolation compared with an unregistered control; restore modified style priorities.
- [ ] Provider disposal does not unregister document definitions; owning thread/host lifetime checks.
- [ ] Focused Windows JIT, NativeAOT and real Chromium; broader gates coordinated with parent. Public package acceptance waits official dependencies and CI, not private candidates.

No native compile, package publication or completion claim has occurred in this lane.
