# Typed CSS value parsing implementation plan

## Scope and source evidence

Required C14 delivers CSSStyleValue.parse/parseAll and structured keyword, image, unparsed and variable-reference values. C13 consumes the resulting values for typed maps; C16 adds transform component operations later. C15 numeric math and C17 color operations remain deferred. Parsed numeric or transform values must still retain their base identity and engine serialization; encountering one does not justify implementing the deferred operations.

Core primitives used are shared DOM interfaces and borrowed document-scoped services. View state is the existing host/session wrapper cache and owned native value references. This work adds engine bindings and typed value ownership, with no new reactive or authoring primitive.

Pinned WebKit b95c91b1 already implements both static parsers in `css/typedom/CSSStyleValue.cpp` through CSSStyleValueFactory. `parse` returns one retained value; `parseAll` returns a vector of retained values. The factory validates property names and CSS grammar, handles custom-property token streams, subdivides list-valued properties and reifies engine subtypes. It uses the calling Document for parser context. No JavaScript or new engine dependency is required by the native implementation.

Exact selected value interfaces:

- CSSStyleValue: parse, parseAll and its stringifier.
- CSSOMKeywordValue (browser name CSSKeywordValue): constructor and mutable value.
- CSSStyleImageValue (browser name CSSImageValue): no extra members in the pinned IDL; preserve subtype and base serialization.
- CSSUnparsedValue: segment-sequence constructor, length, indexed nullable getter and indexed setter. Segments are strings or CSSOMVariableReferenceValue objects.
- CSSOMVariableReferenceValue (browser name CSSVariableReferenceValue): constructor, mutable variable name and readonly nullable fallback CSSUnparsedValue.
- CSSNumericValue/CSSUnitValue: only the minimum value representation needed by parsed units and subsequent typed maps. Expose value/unit and unit construction if needed for the selected contracts; do not expose arithmetic/conversion families from deferred C15. Other parsed math/transform/color subclasses remain valid CSSStyleValue values and retain engine serialization until their selected group supplies richer operations.

## Actual binding gaps

The current canonical generator explicitly rejects static members and discards bare stringifier declarations in its IDL parser. The selected typed-value roots are absent from the native scope. A deeper generator read confirms unnamed indexed operations already map to item/setItem before unsupported-member filtering, and invoke already propagates ExceptionOr errors while discarding successful nonvoid results. Reuse those paths and add regression coverage rather than replacing them. Existing tagged unions, sequences and owned result-release support already represent string-or-reference segments and parseAll results.

There is one concrete transport qualification: `gen/domtypes.py` currently stores compound strings as null-terminated const char pointers and allocates result strings without recording their byte length. Direct generated string inputs also use null-terminated pointers. These cannot preserve an embedded NUL in newly exposed USVString parsing/segments. Use the C12 length-delimited span machinery for new typed-value input signatures and new segment union string fields, including owned-result lengths and matching release. Preserve all old function/compound layouts. Test NUL and ill-formed UTF-16 against WebIDL USVString conversion explicitly; do not silently truncate or pretend the old representation is sufficient.

Add configured document-bound static operations and explicit stringifier emission. Verify CSSUnparsedValue indexed getter/setter names (`item`/`setItem`) and that the existing invoke helper correctly handles the setter's ExceptionOr<segment> result despite IDL returning undefined. Extend the existing configured constructor path to factories that do not take its currently mandatory receiver argument. Append runtime type identities and handle roots; preserve every pre-existing ABI ID, signature and struct layout. The parsed-value type dispatch must recognize selected concrete subtypes and return the base wrapper for unselected subclasses, rather than misclassifying or rejecting a successfully parsed value.

## Shared facade and ownership proposal

Use a borrowed document-bound typed-value service with browser `forDocument` and native `forHost` factories, consistent with the existing stylesheet/query/computed providers. It exposes Parse/ParseAll plus selected keyword/unparsed/variable-reference construction through shared generated interfaces. Browser calls use the supplied Document's realm and validate its active document context; native calls obtain a checked Document handle for each operation. Disposal invalidates the service without changing parsed engine state.

Return actual typed value objects, not flattened CSS snapshots. Native handles belong to the existing session and release through its normal object ownership; result-sequence storage is released after wrapping every retained element, including failures part way through conversion. Preserve borrowed fallback/segment identity through the wrapper cache. Check thread, null/interface, host/session disposal and foreign inputs before C++ access. Browser values remain engine objects; document/provider lifetime restrictions must be stated separately from the independent value's semantics.

## Ordered tasks

- [x] Inspect exact pinned parser/factory implementations, selected IDLs, value factories and canonical transport/static/special-member behavior.
- [x] Probe actual Chromium parse/parseAll, subtype/serialization, malformed grammar, list values and nested variable fallback behavior before fixing shared expected profiles. `artifacts/c14-browser-probe.json` records Chromium156.0.8078.4 source behavior; these are raw engine observations, not shared API acceptance.
- [x] Review the facade and owned-value design, then stack after the committed C12 transport/provider work. Acceptance review is clear. C12 released generator ownership at Native7b3d698 and Coref473ea10; root's isolated Native worktree is `C:/wk/native-typed-css-values`. Batch into the unpublished ABI22/Core14/Native14 cohort if bounded; otherwise allocate a later cohort explicitly before release.
- [ ] Add failing generator tests for configured static parsing, bare stringifier and indexed unparsed operations, then implement the bounded mappings and generated interfaces/runtime dispatch. No unselected existing static operation is enabled implicitly.
- [ ] Add a direct ABI test for scalar/list parsing, canonical serialization, concrete keyword/unit/image/unparsed identity, mutable keyword and unparsed segments, variable name/fallback errors, malformed input recovery, checked ownership/thread failures and zero retained handles.
- [ ] Add the shared document service and real Fable/native providers; use FCS impact/public-surface checks before changing existing declarations and SageFs for pure type/shape questions. Audit XML/comments and preserve repository line endings.
- [ ] Add shared conformance for valid and invalid representative properties, parse versus parseAll list cardinality, opaque structured values, nested var fallback round-tripping and USVString conversions. Grammar acceptance alone does not prove interpolation; C12 owns its independent animation test.
- [ ] Run focused JIT/AOT/Chromium proof, then the combined full shared batch once with C11/C12/C19/C21. Require exact per-group and lifecycle counts before PASS output.
- [ ] Independently review source and generated ABI/model differences. Complete platform CI and official normal/traced package-consumer checks, then let the acceptance lane reconcile the issue/project evidence. Private runtime success does not close C14.

## Current state

Root now owns the isolated C14 generator stage. The configured static parser regression failed because parse exports were absent, then passed with checked Document context, AtomString property conversion and explicit UTF-8 lengths. This is a generator unit proof only; no C14 engine candidate, public contract, shared runtime proof, package or acceptance is claimed. Existing CSSUnparsedValue unnamed getter/setter and ExceptionOr-value discard paths were confirmed reusable by deeper source inspection.

The browser probe confirms parse returns the first list value while parseAll preserves two values for animation-name/background-image; shorthand margin remains an opaque CSSStyleValue, and calc reifies CSSMathSum without requiring arithmetic operations. Custom-property parsing exposes nested variable references/fallbacks. Parsed NUL/lone-surrogate input serializes with replacement characters. Constructed unparsed segments retain an embedded NUL in indexed reads even though their stringifier substitutes it, confirming byte-length transport is necessary. Invalid grammar/property/empty values throw TypeError; invalid variable names throw TypeError and out-of-range segment writes throw RangeError. A raw Chromium keyword constructor retains a lone surrogate despite the pinned WebKit IDL declaring USVString; verify the native result and specify any common facade conversion deliberately rather than assuming raw engines agree.
