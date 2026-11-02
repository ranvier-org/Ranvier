# Core289 additive API review

Proposal only, before F# source edits. Preserve `Ranvier.Dom.CryptoService`, its four members, and existing implementations byte-for-byte. New contracts live in `Ranvier.Dom.Cryptography` and are linked into the Fable package. The full eleven-method interface is fixed before the first slice; HMAC supports five methods initially and reports explicit Unsupported for the other required families until implemented. No new dependency in the first slice.

## Shared declarations

The following are exact proposed signature shapes. Public XML documentation is written with the declarations after review; this is not a compiled API snapshot.

```fsharp
namespace Ranvier.Dom.Cryptography

open System
open Ranvier.Dom

[<RequireQualifiedAccess>]
type CryptoField<'T> = Missing | Present of 'T

type CryptoBuffer = { Bytes: byte array; Offset: int; Count: int }

type CryptoKeyUsage =
    | Encrypt = 0 | Decrypt = 1 | Sign = 2 | Verify = 3
    | DeriveKey = 4 | DeriveBits = 5 | WrapKey = 6 | UnwrapKey = 7

type CryptoKeyType = Secret = 0 | Public = 1 | Private = 2
type CryptoKeyFormat = Raw = 0 | Spki = 1 | Pkcs8 = 2 | Jwk = 3

type CryptoKeyAlgorithm =
    { Name: string
      Hash: string option
      LengthBits: uint64 option
      NamedCurve: string option
      ModulusLengthBits: uint64 option
      PublicExponent: byte array option }

type CryptoKey =
    inherit IDisposable
    abstract Type: CryptoKeyType
    abstract Extractable: bool
    abstract Algorithm: CryptoKeyAlgorithm
    abstract Usages: CryptoKeyUsage array

type CryptoKeyPair = { PublicKey: CryptoKey; PrivateKey: CryptoKey }

[<RequireQualifiedAccess>]
type CryptoGeneratedKey = Key of CryptoKey | Pair of CryptoKeyPair

type RsaOtherPrime =
    { R: CryptoField<string>; D: CryptoField<string>; T: CryptoField<string> }

type JsonWebKey =
    { Kty: CryptoField<string>; Use: CryptoField<string>; Alg: CryptoField<string>
      KeyOps: CryptoField<string array>; Ext: CryptoField<bool>
      K: CryptoField<string>; Crv: CryptoField<string>
      X: CryptoField<string>; Y: CryptoField<string>; D: CryptoField<string>
      N: CryptoField<string>; E: CryptoField<string>; P: CryptoField<string>
      Q: CryptoField<string>; Dp: CryptoField<string>; Dq: CryptoField<string>
      Qi: CryptoField<string>; Oth: CryptoField<RsaOtherPrime array> }

[<RequireQualifiedAccess>]
type CryptoKeyData = Bytes of CryptoBuffer | Jwk of JsonWebKey

[<RequireQualifiedAccess>]
type CryptoExportedKey = Bytes of byte array | Jwk of JsonWebKey

type HmacKeyParameters =
    { Name: string; Hash: CryptoField<string>; LengthBits: CryptoField<uint64> }

type AesKeyParameters = { Name: string; LengthBits: CryptoField<uint64> }
type AesCbcParameters = { Name: string; Iv: CryptoField<CryptoBuffer> }
type AesCtrParameters =
    { Name: string; Counter: CryptoField<CryptoBuffer>; LengthBits: CryptoField<uint64> }
type AesGcmParameters =
    { Name: string; Iv: CryptoField<CryptoBuffer>
      AdditionalData: CryptoField<CryptoBuffer>; TagLengthBits: CryptoField<uint64> }
type HkdfParameters =
    { Name: string; Hash: CryptoField<string>
      Salt: CryptoField<CryptoBuffer>; Info: CryptoField<CryptoBuffer> }
type Pbkdf2Parameters =
    { Name: string; Hash: CryptoField<string>
      Salt: CryptoField<CryptoBuffer>; Iterations: CryptoField<uint64> }
type RsaKeyParameters =
    { Name: string; Hash: CryptoField<string>
      ModulusLengthBits: CryptoField<uint64>; PublicExponent: CryptoField<CryptoBuffer> }
type RsaPssParameters = { Name: string; SaltLengthBytes: CryptoField<uint64> }
type RsaOaepParameters = { Name: string; Label: CryptoField<CryptoBuffer> }
type EcKeyParameters = { Name: string; NamedCurve: CryptoField<string> }
type EcdsaParameters = { Name: string; Hash: CryptoField<string> }
type AgreementParameters = { Name: string; PublicKey: CryptoField<CryptoKey> }

[<RequireQualifiedAccess>]
type CryptoAlgorithm =
    | Name of string
    | HmacKey of HmacKeyParameters
    | AesKey of AesKeyParameters
    | AesCbc of AesCbcParameters
    | AesCtr of AesCtrParameters
    | AesGcm of AesGcmParameters
    | Hkdf of HkdfParameters
    | Pbkdf2 of Pbkdf2Parameters
    | RsaKey of RsaKeyParameters
    | RsaPss of RsaPssParameters
    | RsaOaep of RsaOaepParameters
    | EcKey of EcKeyParameters
    | Ecdsa of EcdsaParameters
    | Agreement of AgreementParameters

type SubtleCryptoService =
    inherit IDisposable
    abstract Availability: SubtleOperation * algorithm: string -> CryptoOutcome<unit>
    abstract GenerateKey:
        CryptoAlgorithm * extractable: bool * CryptoKeyUsage array -> Async<CryptoOutcome<CryptoGeneratedKey>>
    abstract ImportKey:
        CryptoKeyFormat * CryptoKeyData * CryptoAlgorithm * extractable: bool * CryptoKeyUsage array -> Async<CryptoOutcome<CryptoKey>>
    abstract ExportKey:
        CryptoKeyFormat * CryptoKey -> Async<CryptoOutcome<CryptoExportedKey>>
    abstract Sign:
        CryptoAlgorithm * CryptoKey * CryptoBuffer -> Async<CryptoOutcome<byte array>>
    abstract Verify:
        CryptoAlgorithm * CryptoKey * signature: CryptoBuffer * data: CryptoBuffer -> Async<CryptoOutcome<bool>>
    abstract Encrypt:
        CryptoAlgorithm * CryptoKey * CryptoBuffer -> Async<CryptoOutcome<byte array>>
    abstract Decrypt:
        CryptoAlgorithm * CryptoKey * CryptoBuffer -> Async<CryptoOutcome<byte array>>
    abstract DeriveBits:
        CryptoAlgorithm * CryptoKey * lengthBits: CryptoField<uint64> -> Async<CryptoOutcome<byte array>>
    abstract DeriveKey:
        CryptoAlgorithm * CryptoKey * target: CryptoAlgorithm * extractable: bool * CryptoKeyUsage array -> Async<CryptoOutcome<CryptoKey>>
    abstract WrapKey:
        CryptoKeyFormat * key: CryptoKey * wrappingKey: CryptoKey * CryptoAlgorithm -> Async<CryptoOutcome<byte array>>
    abstract UnwrapKey:
        CryptoKeyFormat * CryptoBuffer * unwrappingKey: CryptoKey * CryptoAlgorithm * target: CryptoAlgorithm * extractable: bool * CryptoKeyUsage array -> Async<CryptoOutcome<CryptoKey>>
```

Use `CryptoAlgorithm.Name "Ed25519"`, `Name "X25519"`, `Name "HMAC"` for operation names, `Name "HKDF"`/`Name "PBKDF2"` for base-key import, and `Name "AES-KW"` for wrapping. Algorithm dictionaries are typed variants. Normalization reads only the fields required by the requested operation: a `HmacKey` value can identify HMAC for signing without supplying its irrelevant hash or length fields. Missing fields required by the requested operation produce conversion errors. Names are case-insensitive; JWK string restrictions remain exact-case.

Helper module signatures, with implementations kept pure and small:

```fsharp
module CryptoBuffer =
    val whole: bytes: byte array -> CryptoBuffer

module JsonWebKey =
    val empty: JsonWebKey

namespace Ranvier.Crypto
module ManagedCrypto =
    val create: unit -> Ranvier.Dom.Cryptography.SubtleCryptoService

namespace Ranvier.Dom
module BrowserSubtleCrypto =
    val forDocument: Document -> Ranvier.Dom.Cryptography.SubtleCryptoService
```

## Ownership and timing

- The native provider's key implementation is private, reference-counted through ordinary managed ownership plus an internal operation lease. Material identity is provider-wide, not tied to a service object. Keys returned by service A work through compatible service B, before and after A.Dispose. Disposing A never disposes already-returned keys. Disposing the key itself prevents later admission but retained operations complete safely.
- Browser wrappers contain actual browser CryptoKey references. Same-compatible-provider services can use them; browser cross-realm compatibility follows actual WebCrypto behavior and is measured, rather than imposing an artificial service-origin rule.
- Every metadata read returns snapshots of arrays/records containing arrays. Callers cannot alter internal usages, exponent or algorithm metadata. Public key pair records contain caller-owned keys; disposing one does not dispose the other.
- All request buffers/JWK arrays are copied synchronously when the service method is called. The returned Async observes a single admitted computation, including repeated awaits. Native work is scheduled away from the UI thread; captured key material stays retained. Promise/Async completion names/errors preserve the validated stage. Cancelling an Async waiter stops only that observation: the admitted operation and cached result remain usable by later awaits. Service disposal independently closes pending publication.
- Service disposal closes only that service's admission/publication boundary and pending operations. New admission raises CryptoException InvalidStateError on both targets; this is the facade's lifetime rule because browser WebCrypto itself has no Dispose. Native key material is not document-owned. Browser secure-context/document capability loss is explicit; native keys have no WebCoreHost dependency. Existing CryptoService behavior is unchanged.
- Unsupported means this provider/context has no implementation, not invalid data. The initial native HMAC slice supports only valid HMAC generation/import/export/sign/verify; the remaining operations and algorithm families remain open until implemented and qualified. Digest stays on the existing CryptoService; this new interface's Availability returns Unsupported for Digest because digest remains on the existing service.

## First-slice review tests

1. Existing CryptoService signatures and object-expression implementations unchanged.
2. Native A creates HMAC key; native B signs/verifies; dispose A; B still signs/verifies; dispose key; B rejects new use. A pending operation admitted before key disposal retains material until completion.
3. Raw/JWK generation/import/export/sign/verify for SHA1/256/384/512; standard vectors; nonextractable exports; wrong usage; fake/cross-backend key rejected; metadata and input mutation do not alter output.
4. Unknown usage enum plus unknown algorithm/hash/length0 gives TypeError first. Recognized forbidden usage plus HMAC explicit0 retains the dated DataError order. Missing hash, omitted length, empty data, invalid format and duplicate JWK key_ops are separate cases.
5. Browser A/B reuse of the same actual HMAC key, origin-service disposal, secure/insecure contexts, copied input and ordinary WebCrypto error names. The full default helper runs these checks; focused runs are development evidence only.
6. Exact published RFC fixtures drive BCL and browser; NativeAOT/trimmed public-facade smoke uses no native WebCore library. No companion provider is restored for this first slice.

The prior full edge matrix and companion choices were coordinator-selected implementation planning. They are review inputs, not new user mandates; any scope revision must be explicit against #289 rather than silently changing expectations to fit a provider.
