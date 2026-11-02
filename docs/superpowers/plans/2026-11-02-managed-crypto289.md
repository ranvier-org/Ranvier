# Managed native and browser crypto implementation plan

> **For agentic workers:** Use `superpowers:executing-plans` to implement reviewed tasks in order. This is a design checkpoint; no F# implementation is authorized until the coordinator reviews it.

**Goal:** Deliver Core [#289](https://github.com/ranvier-org/Ranvier/issues/289)'s eleven key operations through established .NET providers on native targets and WebCrypto on Fable/browser targets.

**Architecture:** Shared F# contracts and normalization own WebCrypto semantics. A host-independent .NET provider owns native key material; a Fable provider wraps actual browser keys. Existing random/UUID/digest consumers stay compatible. No WebCore crypto bridge, engine patch or crypto ABI is part of delivery.

**Tech stack:** F#; .NET 10 `System.Security.Cryptography`; a reviewed, pinned managed companion provider for BCL gaps; Fable/browser WebCrypto; Expecto; NativeAOT.

**Spec:** Core #289 and the selected WebCrypto profile retained from #143. Normalization is pinned to the 22 October 2026 Level 2 editor's draft, source commit `84ce201266f605a92f9ae790763db9f70a026ffa`; this dated choice was a coordinator decision, not an independent user requirement.

## Constraints and status

- All eleven operations remain required: generate/import/export/sign/verify/encrypt/decrypt/deriveBits/deriveKey/wrapKey/unwrapKey. Existing random, UUID and SHA support remains.
- Preserve AES-128/192/256 CBC/CTR/GCM/KW; HMAC and HKDF/PBKDF2 with SHA-1/256/384/512; RSA PKCS1-v1.5/PSS/OAEP with the selected hashes; ECDSA/ECDH P-256/384/521; Ed25519/X25519. Preserve the selected parameter and key-format matrix, rather than replacing it with the easiest BCL subset.
- No custom cryptographic primitive, WebCore key registry, engine repair, new native crypto export or package/ABI allocation. WebCore #234–240 are closed as not planned, not publicly fixed.
- Native research is preserved separately. Its passing C++ probes are useful fixtures and regression history, not evidence that the new managed facade works.
- No provider dependency has been adopted, restored or qualified by this plan. BouncyCastle.Cryptography 2.7.0 is the proposed companion candidate. Package identity, exact hash, notices and runtime/AOT proof precede adoption.

## Existing API and source map

Read against Core `e613ea27ecf4fa5a1e2cc935a1adbfc3d093f5c2`, the alpha55 contract, in the owned `codex/project2-managed-crypto289` worktree.

- `src/Ranvier.Dom/Crypto.fs`: `RandomValuesKind`, `CryptoException(name, detail)`, `CryptoOutcome<'T>`, `DigestAlgorithm`, `SubtleOperation`, `CryptoService`, `CryptoContract`. `CryptoService` inherits IDisposable and currently has only GetRandomValues, RandomUUID, Digest and Availability. Digest returns `Async<CryptoOutcome<byte array>>`; its input bytes are copied before the method returns. The file has no key-operation implementation to migrate.
- `src/Fable.Ranvier.Dom/BrowserCrypto.fs`: `BrowserCrypto.forDocument : Document -> CryptoService`. It captures the document's active realm, uses real browser random/UUID/digest, and returns controlled Unsupported without secure-context SubtleCrypto. Fable's project links the shared Crypto.fs source.
- `src/Ranvier.Dom/Ranvier.Dom.fsproj`: shared net10.0/netstandard2.1 contract. `src/Fable.Ranvier.Dom/Fable.Ranvier.Dom.fsproj`: same contract assembly identity, linked shared sources plus browser adapters.
- View `src/Ranvier.View.WebCore/Crypto.fs`: `Crypto.forHost : WebCoreHost -> CryptoService`. SHA digests already use .NET. Random/UUID currently use the native document ABI; this existing B22 route need not change to add host-independent key operations.
- View `tests/NativeApi.Conformance/CryptoChecks.fs`: existing shared random/UUID/digest/unsupported/teardown checks. Preserve those while adding key-operation checks in a separate shared helper and routing them through the default full suite.

Discovery evidence: a bounded FCS outline of the pinned Core Crypto.fs returned no diagnostics. BrowserCrypto's outline on the un-restored new snapshot reports missing Fable references; this is not a clean browser type-check. The old View snapshot also lacks resolved references, so its inferred generic signatures were not used. No whole-workspace FCS check, usage-negative inference, compiler audit or build was run. Exact Core Crypto.fs SHA256 is `71f64243d83541ea97592e32fde0d4a6a207d66f04824130568da7dd73579ec7`; BrowserCrypto.fs is `ca1e1875e4934d9f9c1563305305b196378f4034ee8dfaecc9638b2d979f15e1`.

## Provider coverage audit and proposed routing

This is API/source feasibility, not actual platform qualification.

- **Random, SHA and HMAC:** use BCL RandomNumberGenerator and SHA/HMAC static APIs. Shared validation retains quota/kind/order, logical HMAC bit length and canonical masked ceiling-byte storage. UUID remains RFC4122 version 4 with correct variant and lowercase formatting; no assumption that native Guid byte order matches WebCrypto bytes.
- **CBC:** BCL AES CBC with explicit PKCS7. Validate the WebCrypto IV/input/key contract before dispatch. Test malformed zero padding, oversized padding and inconsistent padding without importing the abandoned GCrypt fix.
- **CTR:** .NET 10 CipherMode has no CTR. Bouncy Castle SIC/CTR supplies the cipher, but increments a whole block and does not by itself express WebCrypto's 1..128-bit counter field. This is an explicit adaptation review item: retain the original high bits, bound message blocks to the counter space, and split/reset the established provider CTR transform at the low-field wrap boundary. This is counter-range composition over a provider transform, not an F# AES implementation. Do not claim coverage or implement this adapter until the coordinator accepts that exact seam and wrap tests. If that seam is disallowed, choose another established provider with the required counter-width API; scope remains open.
- **GCM:** BCL AesGcm only accepts 12-byte nonces; Windows/Linux support 12–16-byte tags and Apple supports 16-byte tags. The selected WebCrypto profile includes non-96-bit IVs and tag lengths 32/64/96/104/112/120/128 bits. Route this family consistently through the companion GcmBlockCipher after qualification, including AES192 and one-byte-IV primary rows. Never label a failing valid input Unsupported just to pass a matrix.
- **KW:** .NET 10's new EncryptKeyWrapPadded is RFC5649 KWP, not selected RFC3394 KW. Use the companion Rfc3394WrapEngine with the fixed default IV and WebCrypto length/integrity checks. No KWP substitution.
- **HKDF/PBKDF2:** BCL HKDF and static Rfc2898DeriveBytes.Pbkdf2 are candidates for all four selected hashes. Verify empty password, explicit empty salt, NUL input, zero iterations, byte/nonbyte output rules and 16M-iteration primary vector. Required missing salt/info stays a conversion error; RFC omitted salt is mapped to explicitly present empty bytes for the positive WebCrypto fixture.
- **RSA:** BCL covers common PKCS1-v1.5, PSS and OAEP cases, but PSS salt is fixed to hash length and the OAEP API has no label. Generation also needs an audit for selected modulus/public-exponent constraints. Prefer one companion RSA implementation for the family using explicit PssSigner salt length and OaepEncoding label/hash parameters, avoiding internal provider-switch exports of nonextractable material. BCL may be used only for a complete, explicitly selected equivalent path.
- **ECDSA/ECDH:** BCL ECDsa/ECDiffieHellman candidates support the selected named curves and raw agreement. Use explicit IEEE-P1363 fixed-width r||s for ECDSA, never DER by accident. Normalize agreement truncation and unused low bits in the shared layer. Strict fixed-width JWK coordinates/scalars, on-curve validation and public/private consistency remain adapter responsibilities, with actual platform checks. If a selected platform cannot meet these requirements, use the reviewed companion path for the entire key family.
- **Ed25519/X25519:** absent from the pinned .NET 10 public reference API. The companion has explicit Ed25519Signer and X25519 key/agreement types. Use pure Ed25519 (not ph/ctx), and retain X25519 all-zero-secret rejection and nonbyte output masking. New .NET 11 APIs are not a .NET 10 solution.
- **Formats:** shared JWK validation/encoding; raw only where each WebCrypto algorithm permits; SPKI/PKCS8 via reviewed BCL or companion ASN.1 APIs with complete consumption, correct OID/parameters/type and canonical width checks. No raw private Ed/X import promise. Nonextractable protects public facade export; internal provider representation remains inaccessible.

Official sources checked 2 November 2026: [.NET 10 public API](https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/src/libraries/System.Security.Cryptography/ref/System.Security.Cryptography.cs), [cross-platform support](https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography), [PSS salt](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rsasignaturepadding.pss?view=net-10.0), [KWP](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aes.encryptkeywrappadded?view=net-10.0). The provider candidate's official distribution lists [2.7.0](https://www.bouncycastle.org/download/bouncy-castle-c/); pinned source exposes [explicit PSS salt](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/crypto/src/crypto/signers/PssSigner.cs), [OAEP encoding parameters](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/crypto/src/crypto/encodings/OaepEncoding.cs), [GCM](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/crypto/src/crypto/modes/GCMBlockCipher.cs), [KW](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/crypto/src/crypto/engines/RFC3394WrapEngine.cs), [SIC](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/crypto/src/crypto/modes/SicBlockCipher.cs), [Ed25519](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/crypto/src/crypto/signers/Ed25519Signer.cs) and [X25519](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/crypto/src/crypto/parameters/X25519PrivateKeyParameters.cs). Its [MIT license](https://github.com/bcgit/bc-csharp/blob/release-2.7.0/LICENSE.md) must be retained. Source availability is not an AOT, cryptographic-policy or runtime acceptance claim.

## Shared API and ownership proposal

Keep the existing CryptoService interface unchanged so existing object-expression implementers remain valid. Add `Ranvier.Dom.Cryptography` contracts in `src/Ranvier.Dom/CryptoKeys.fs`, linked into Fable alongside existing Crypto.fs. Use a sibling `SubtleCryptoService : IDisposable` exposing eleven named methods; each consumes a typed per-operation request record and returns `Async<CryptoOutcome<'T>>`. GenerateKey returns a discriminated single-key/key-pair result. The shared key name is `Ranvier.Dom.Cryptography.CryptoKey`, avoiding an accidental collision with generated DOM contracts.

Keys expose immutable Type, Extractable, Algorithm metadata and Usages snapshots, plus IDisposable. Constructors and material access are provider-internal. The key is an opaque managed object, not an engine integer handle or serializable public token. A pair owns independently disposable public/private keys; publication is atomic. Owned keys are context-independent: every compatible instance of the same provider can use them, including after the originating service is disposed. Cross-backend incompatibility is explicit; there is no per-service origin check. Secret/private zero usages fail; public zero usages remain legal.

Requests use explicit presence (`Missing | Present value`) where false, zero or empty differ from omission. They contain strings for algorithm names, dedicated dictionaries for HMAC/AES/KDF/RSA/EC/OKP parameters, byte ranges, usage enums, key format and a full JWK snapshot. Invalid numeric enum values are rejected as TypeError at binding conversion, before algorithm/hash normalization. Recognized-but-disallowed usages remain algorithm validation, including the dated HMAC length-zero ordering. This keeps the confirmed crossed-unknown-usage regression visible in the managed/browser tests without reviving native fixes.

Native `ManagedCrypto.create : unit -> SubtleCryptoService` lives in a small new Core project `src/Ranvier.Crypto/Ranvier.Crypto.fsproj` targeting net10.0 and referencing Ranvier.Dom. This isolates BCL/companion dependencies from the shared netstandard/Fable contract. Files separate Keys, Validation, Jwk, Providers/Bcl, Providers/Companion and Service; no public provider types leak. Fable adds `BrowserSubtleCrypto.forDocument : Document -> SubtleCryptoService`, stores actual realm CryptoKey objects and calls actual browser subtle methods. View may add a convenience factory later; a WebCoreHost is unnecessary for native key operations.

At method admission copy every input range, salt/info/IV/counter/AAD/label/exponent/JWK member/usage list before returning the Async. Start an admitted operation once and retain its participating keys/material; repeated awaits observe the same completion rather than repeat generation. Async cancellation is waiter-local: cancelling an observer leaves the admitted operation and its cached result usable by later awaits. Service disposal rejects new admissions and cancels publication of its pending results; it does not dispose keys already returned to callers. An uninterruptible provider call may finish internally but cannot publish a live result through a disposed service. Key disposal independently prevents future use while retained in-flight operations finish safely. Dispose BCL objects deterministically and wipe only buffers we own; do not promise forensic wiping of provider-managed storage. No global registry or finalizer-only lifetime.

Preserve existing CryptoOutcome.Unsupported for genuinely unavailable implementations/contexts, and use CryptoException with the exact WebCrypto name for invalid inputs and operation failures. Wrong key/type/usage maps to InvalidAccessError; malformed key data to DataError; cryptographic execution/authentication/padding failure to OperationError; unsupported algorithm/format to NotSupportedError where required. Conversion TypeError and JSON SyntaxError keep their distinct names. Do not map every CryptographicException to one name without knowing the validated stage. Error detail may be provider-specific; names/order are contractual.

JWK uses exact-case kty/alg/use constraints and presence-aware ext/key_ops/oth. The shared parser must preserve .NET UTF16 code units, unknown key_ops and duplicate semantics, lexicographical conversion order, nested optional oth fields, JS numeric ToString (including overflow Infinity and -0), and parse/conversion error propagation from the dated profile. Avoid a generic System.Text.Json serialization roundtrip that silently replaces lone surrogates or changes coercion. The typed API does not promise arbitrary JavaScript getter/proxy side effects; browser dictionary entry performs the actual Web IDL conversion at its boundary. Raw browser behavior differences remain recorded separately from shared facade normalization.

## Review focus

- Mutation after admission or disposing one key while derive/wrap uses two keys must not change output or free retained material early.
- Unknown requested usage must win over unsupported algorithm/hash; JWK unknown key_ops is a DOMString sequence and follows different semantics.
- Empty-present bytes/false/zero and lone-surrogate strings must survive shared requests and JWK round trips without becoming missing or colliding.
- Nonextractable keys must never escape through wrap/export/diagnostics; wrong format/operation precedence still precedes extractability where the dated algorithm requires it.
- NativeAOT and the browser must exercise the public facade, including the default full suite, without engine crypto symbols or fixture-only production exports.

## Implementation tasks after review

### 1. Freeze additive contracts and HMAC admission tests

Files: new `src/Ranvier.Dom/CryptoKeys.fs`; linked Fable project entry; new `tests/Ranvier.Crypto.Tests/ContractTests.fs`; API snapshots. Proposed eleven request/return signatures above are frozen as a focused public API review before their first implementation commit.

- [ ] Run targeted fcs_refactor_impact/public API snapshots before public edits; no whole generated-file audit. Restore the bounded projects, use the persistent direct SageFs MCP connection, acquire/inspect the proper lease, create one owned session. Do not restart the daemon.
- [ ] RED in SageFs: missing vs present empty/zero; invalid usage enum before unknown algorithm; recognized invalid usage vs HMAC length0; duplicate key_ops; immutable metadata snapshots.
- [ ] Add the contracts and pure normalization, persist/reset/reload and repeat the same checks. Read XML/comment skills before public documentation.
- [ ] Verify old CryptoService implementer still compiles unchanged; commit only this additive contract slice.

### 2. First vertical slice: managed and browser HMAC

Files: new Core `src/Ranvier.Crypto/{Ranvier.Crypto.fsproj,Keys.fs,Hmac.fs,Service.fs}`; Fable `BrowserSubtleCrypto.fs`; shared tests `tests/Ranvier.Crypto.Tests/{HmacTests.fs,LifetimeTests.fs}` and a browser runner. Only HMAC generate/import/export/sign/verify is initially supported; every other required family stays explicitly incomplete, not a completed #289.

- [ ] RED: RFC4231/RFC2202 published outputs, raw/JWK roundtrip, SHA1/256/384/512, logical nonbyte length, invalid zero/missing length, all extractability/usage/type cases, same-provider cross-service reuse before/after origin service disposal, explicit cross-backend mismatch, mutation after admission and dispose during admitted work.
- [ ] Implement BCL HMAC over owned canonical bytes and the same public request/error model; wrap actual browser HMAC keys for Fable. Preserve the full profile contract on both paths and record any browser normalization discrepancy.
- [ ] Pure SageFs RED/GREEN, persist/reset/recheck; then CLI full tests and actual Chromium. Confirm no dependency on WebCore packages/native libraries in the native crypto test app.
- [ ] Publish-trimmed/NativeAOT smoke must generate/import/sign/verify/export through the public facade. Review and commit the independently usable HMAC slice before extending families.

### 3. Qualify and pin the companion provider

Files: proposed `src/Ranvier.Crypto/Providers/Companion.fs`, project package lock, notices, `tests/Ranvier.Crypto.Tests/ProviderCoverageTests.fs`, AOT consumer.

- [ ] Review exact 2.7.0 package/source identity and MIT notices; pin hash/version in normal restore. No reflection/string-name crypto factories or dynamic assembly loading.
- [ ] RED/actual proof for GCM all seven tag lengths/non96IV/AES192, RFC3394, arbitrary selected PSS salt/OAEP label, Ed/X generate/import/export/sign/verify/derive, and CTR counter-field wrap. Resolve the explicit CTR adaptation seam before code.
- [ ] Run actual NativeAOT/trimming on Windows and Linux before adopting the dependency; macOS proof remains required. Any IL2026/IL3050 issue is resolved or blocks the path, never blanket-suppressed. No claim that net6.0 compatibility alone proves AOT.

### 4. AES and composition

Files: `src/Ranvier.Crypto/{Aes.fs,KeyFormats.fs,Composition.fs}`; matching browser adapter and shared AES tests.

- [ ] RED all NIST CBC/CTR/GCM and RFC3394 fixtures; CBC p0; tag failure; CTR1/9/128 and wrap boundary; all AES sizes; malformed/missing/present-empty dictionaries.
- [ ] Implement generate/import/export/encrypt/decrypt plus wrap/unwrap as shared export→encrypt and decrypt→import composition with distinct wrap/unwrap usages and extractability/order. KW uses its dedicated provider operation.
- [ ] Exercise generic cross-family wrappers, JWK parse failures and two-key lifetime/copy cases in every public facade; full tests then review/commit.

### 5. KDF and deriveKey

Files: `src/Ranvier.Crypto/Kdf.fs`; browser KDF adapter; shared KdfTests.

- [ ] RED all 13 RFC5869/RFC6070 primary cases including 16M, SHA1/256/384/512 empty/NUL/password/salt/info matrix, required missing salt, zero iterations/output, nonextractable base and nonbyte HMAC159 target behavior.
- [ ] Implement deriveBits and deriveKey using shared normalize target→get length→derive bits→raw import, preserving target metadata/usages/extractability. HKDF/PBKDF2 base keys cannot be exported merely because their stored bytes are available.
- [ ] Prove cancellation/teardown, copied inputs, output masks and all existing HMAC/AES tests; review/commit.

### 6. RSA, EC and OKP

Files: `src/Ranvier.Crypto/Asymmetric.fs`, provider files, format validation; browser adapter; shared asymmetric tests.

- [ ] RED published RFC7515/5903/7748/8032 vectors and retained strict JWK/DER/coordinate/scalar/point/error controls. Include crossed unknown usage with unknown algorithm/hash on generate/import/deriveKey.
- [ ] Implement paired key generation, import/export, sign/verify, OAEP and ECDH/X deriveBits/deriveKey through the same key owner and composition paths. All supported key formats and public zero-usages cases stay required.
- [ ] Review pair publication/disposal and all eleven operation families through actual public tokens; full suite/NativeAOT/Chromium before claiming family acceptance.

### 7. Official cohort and acceptance

- [ ] Core shared/native/Fable packages and View convenience consumers use exact official versions; no private absolute reference paths, locally patched Microsoft packages or WebCore crypto dependency.
- [ ] Native Windows/Linux JIT and NativeAOT plus macOS applicable RID proofs; normal/traced cohort where applicable; Chromium secure/insecure contexts and real browser CryptoKey behavior. Run unfiltered suites, Release and original analyzer commands for every affected group.
- [ ] Copy published primary fixtures unchanged from retained Git commits, with source/hash attribution. Move provider-neutral cases from ctypes scripts into shared F# checks; keep raw native research logs read-only. Baseline counts are history, not a target to game.
- [ ] Independent QA confirms contract/errors/lifetimes/provider/package closure. Then update #289; no closure while a selected algorithm/parameter path or required platform is unresolved.

## Preservation and cleanup

Native research branch `codex/project2-crypto143` at `a56dec9122d4096c1566362fe9e9fb6361fca0a1` is pushed; its only unstaged crossed-enum test delta is separately saved to `C:/wk/project2/crypto143/paused-crossed-enum-tests.patch`. Canonical native integration `3f6ebd5` is source research only. Do not merge engine patches/config/bridge into Core or the release candidate. Keep compact proof, primary fixtures, immutable Git refs and one source archive; remove released private objects/runtime copies and retired worktrees after the coordinator's no-borrower check. Protected warm trees remain untouched.

At this checkpoint no compiler/runtime process, native slot, source/PCH borrow, SageFs session or managed lease is held by this lane. The new Core worktree contains this Markdown plan only. Codex prepared this plan; no managed/provider runtime claim is made.
