# G07 shared geometry values

## Approved contract

Reuse actual browser/WebCore geometry operations and generated instance interfaces. The borrowed GeometryService exposes mutable/read-only rectangle and point constructors/from-dictionary factories plus quad constructors/from-rectangle/from-quad factories. Geometry.forDocument and native Geometry.forHost bind a live document/session; disposal invalidates the provider and preserves existing value objects.

Manual DOMPointInit, DOMRectInit and DOMQuadInit records preserve optional dictionary fields. Empty records omit every field. Point coordinates default to 0/0/0/1; rectangle coordinates and extents default to zero. Null dictionaries and null nested dictionaries use WebIDL empty-dictionary semantics. Doubles remain unrestricted, including NaN, infinities, signed zero and values beyond float32 precision.

Native wrapper dispatch must preserve trusted mutable versus read-only metadata. Existing Rect cache kind remains unchanged; Point and Quad kinds append after all existing kinds. Quad points retain actual engine identity, remain live under mutation and survive wrapper collection; factory copies and bounds snapshots stay independent.

## Ownership and scope

Factories anchor results to the provider's document/session. Actual point/rectangle operations remain generated. Host/session/thread validation follows the established borrowed service pattern. Browser providers use the selected document's active realm and reject expired document context.

Canonical Native source checkpoint 829078076a616dc3f757773b505448c3e2f05a4e supplies 141 interfaces / 1775 exports / 22 handle roots. It is local and unpublished; final pins await root's pushed G07/G08 checkpoint. Existing cohort versions and C13 evidence remain unchanged.

Matrix transforms await the root-owned G08 producer and later shared integration. No matrix initializer or generic toJSON transport is introduced in this G07 checkpoint.

## Ordered work

- [x] Read selected G07 acceptance, pinned producer plan and actual generated dictionary/constructor shapes; obtain root approval for the service and initialization records.
- [x] Import the exact canonical generated contract and obtain a fresh clean FCS check.
- [x] Prove optional dictionary/default/service shapes in Sage; implement documented Core/browser providers.
- [x] Update backend root dispatch for checked geometry metadata, append Point/Quad releases, regenerate and compare old wrapper bodies.
- [x] Implement native factories with owned result wrapping and scoped nested dictionary inputs; independently review.
- [x] Prove constructors/defaults, mutable/read-only policy, double precision, unrestricted values, signed extents, copies and all quad corners/bounds in shared JIT/AOT/Chromium fixtures.
- [x] Prove native thread/document/session/host lifetime and browser provider/navigation context, with exact count guards and clean teardown.
- [ ] Persist evidence and bounded source commits; combine broader repository/conformance gates with G08 where practical.
- [ ] Verify coordinated platform/released consumers before shared acceptance.

## Focused private result

Windows JIT and NativeAOT each pass 177 shared assertions plus 26 lifetime assertions; Chromium156.0.8078.4 passes 177 shared assertions plus three actual document-replacement assertions. Strict counts guard both focused and full runner paths. Native teardown leaves zero wrappers. Parent independently reviewed the public service, browser/native providers and generator repair without an actionable finding. The focused fixtures are under separate acceptance review.

The backend generator first failed on the new Point root without a release kind. Its next build exposed FS0361 for mutable properties redeclaring inherited read-only getters; a Sage RED/GREEN confirmed explicit base-interface reimplementation resolves the actual F# slot ambiguity. The final generated output has zero unmapped members; 242 old class bodies are unchanged, and the four changed old classes are restricted to rectangle properties and trusted rectangle result dispatch.

Exact private runtime binary hashes, commands and logs are persisted in the Views geometry-values-runtime-evidence.json. This Core snapshot preserves complete fresh FCS tool results in docs/research/native-api-delivery/geometry-values-public-api.json. Broader repository, full conformance, non-Windows and released-package checks remain pending the combined G08 checkpoint.
