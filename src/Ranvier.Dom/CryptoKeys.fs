namespace Ranvier.Dom.Cryptography

open System
open Ranvier.Dom

/// <summary>A missing dictionary member or an explicitly supplied value, including zero, false and empty values.</summary>
[<RequireQualifiedAccess>]
type CryptoField<'T> =
    | Missing
    | Present of 'T

/// <summary>A byte range copied by a crypto operation before its method returns.</summary>
type CryptoBuffer =
    {
        Bytes: byte array
        Offset: int
        Count: int
    }

/// <summary>An operation a key permits; values outside this enumeration fail conversion with TypeError.</summary>
type CryptoKeyUsage =
    | Encrypt = 0
    | Decrypt = 1
    | Sign = 2
    | Verify = 3
    | DeriveKey = 4
    | DeriveBits = 5
    | WrapKey = 6
    | UnwrapKey = 7

/// <summary>The secret, public or private part of a key.</summary>
type CryptoKeyType =
    | Secret = 0
    | Public = 1
    | Private = 2

/// <summary>A WebCrypto key encoding.</summary>
type CryptoKeyFormat =
    | Raw = 0
    | Spki = 1
    | Pkcs8 = 2
    | Jwk = 3

/// <summary>A key's normalized algorithm metadata; array values are independent snapshots.</summary>
type CryptoKeyAlgorithm =
    {
        Name: string
        Hash: string option
        LengthBits: uint64 option
        NamedCurve: string option
        ModulusLengthBits: uint64 option
        PublicExponent: byte array option
    }

/// <summary>An owned key usable by compatible provider instances until it is disposed.</summary>
/// <remarks>Disposal rejects new uses while admitted operations retain their material. Service disposal leaves returned keys usable.</remarks>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type CryptoKey =
    inherit IDisposable
    /// <summary>The key's secret, public or private type.</summary>
    abstract Type: CryptoKeyType
    /// <summary>Whether the provider permits key material to be exported.</summary>
    abstract Extractable: bool
    /// <summary>An independent snapshot of normalized algorithm metadata.</summary>
    abstract Algorithm: CryptoKeyAlgorithm
    /// <summary>An independent snapshot of permitted operations.</summary>
    abstract Usages: CryptoKeyUsage array

/// <summary>Independently owned public and private keys published together.</summary>
type CryptoKeyPair =
    {
        PublicKey: CryptoKey
        PrivateKey: CryptoKey
    }

/// <summary>The single key or key pair returned by generation.</summary>
[<RequireQualifiedAccess>]
type CryptoGeneratedKey =
    | Key of CryptoKey
    | Pair of CryptoKeyPair

/// <summary>Optional RSA additional-prime JWK members.</summary>
type RsaOtherPrime =
    {
        R: CryptoField<string>
        D: CryptoField<string>
        T: CryptoField<string>
    }

/// <summary>A presence-aware JSON Web Key dictionary; key_ops contains DOMStrings rather than requested-usage enums.</summary>
type JsonWebKey =
    {
        Kty: CryptoField<string>
        Use: CryptoField<string>
        Alg: CryptoField<string>
        KeyOps: CryptoField<string array>
        Ext: CryptoField<bool>
        K: CryptoField<string>
        Crv: CryptoField<string>
        X: CryptoField<string>
        Y: CryptoField<string>
        D: CryptoField<string>
        N: CryptoField<string>
        E: CryptoField<string>
        P: CryptoField<string>
        Q: CryptoField<string>
        Dp: CryptoField<string>
        Dq: CryptoField<string>
        Qi: CryptoField<string>
        Oth: CryptoField<RsaOtherPrime array>
    }

/// <summary>An encoded byte range or a JWK dictionary supplied to import.</summary>
[<RequireQualifiedAccess>]
type CryptoKeyData =
    | Bytes of CryptoBuffer
    | Jwk of JsonWebKey

/// <summary>An independent encoded byte array or JWK dictionary returned by export.</summary>
[<RequireQualifiedAccess>]
type CryptoExportedKey =
    | Bytes of byte array
    | Jwk of JsonWebKey

/// <summary>HMAC key parameters with optional logical length in bits.</summary>
type HmacKeyParameters =
    {
        Name: string
        Hash: CryptoField<string>
        LengthBits: CryptoField<uint64>
    }

/// <summary>AES key parameters with length in bits.</summary>
type AesKeyParameters =
    {
        Name: string
        LengthBits: CryptoField<uint64>
    }

/// <summary>AES-CBC parameters with a required sixteen-byte IV.</summary>
type AesCbcParameters =
    {
        Name: string
        Iv: CryptoField<CryptoBuffer>
    }

/// <summary>AES-CTR parameters with a sixteen-byte counter and a counter-field length in bits.</summary>
type AesCtrParameters =
    {
        Name: string
        Counter: CryptoField<CryptoBuffer>
        LengthBits: CryptoField<uint64>
    }

/// <summary>AES-GCM parameters with a required IV and optional AAD and tag length in bits.</summary>
type AesGcmParameters =
    {
        Name: string
        Iv: CryptoField<CryptoBuffer>
        AdditionalData: CryptoField<CryptoBuffer>
        TagLengthBits: CryptoField<uint64>
    }

/// <summary>HKDF parameters with required hash, salt and info members.</summary>
type HkdfParameters =
    {
        Name: string
        Hash: CryptoField<string>
        Salt: CryptoField<CryptoBuffer>
        Info: CryptoField<CryptoBuffer>
    }

/// <summary>PBKDF2 parameters with required hash, salt and positive iteration count.</summary>
type Pbkdf2Parameters =
    {
        Name: string
        Hash: CryptoField<string>
        Salt: CryptoField<CryptoBuffer>
        Iterations: CryptoField<uint64>
    }

/// <summary>RSA key parameters; import requires the hash, and generation also requires modulus length and exponent.</summary>
type RsaKeyParameters =
    {
        Name: string
        Hash: CryptoField<string>
        ModulusLengthBits: CryptoField<uint64>
        PublicExponent: CryptoField<CryptoBuffer>
    }

/// <summary>RSA-PSS parameters with salt length in bytes.</summary>
type RsaPssParameters =
    {
        Name: string
        SaltLengthBytes: CryptoField<uint64>
    }

/// <summary>RSA-OAEP parameters with an optional label.</summary>
type RsaOaepParameters =
    {
        Name: string
        Label: CryptoField<CryptoBuffer>
    }

/// <summary>EC key parameters with a required named curve.</summary>
type EcKeyParameters =
    {
        Name: string
        NamedCurve: CryptoField<string>
    }

/// <summary>ECDSA parameters with a required hash.</summary>
type EcdsaParameters =
    {
        Name: string
        Hash: CryptoField<string>
    }

/// <summary>ECDH or X25519 parameters with a required peer public key.</summary>
type AgreementParameters =
    {
        Name: string
        PublicKey: CryptoField<CryptoKey>
    }

/// <summary>A named algorithm or typed dictionary; normalization reads members required by the requested operation.</summary>
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

/// <summary>Asynchronous WebCrypto key operations of a provider.</summary>
/// <remarks>
/// Inputs are copied at admission; repeated awaits observe the same operation. Closed-service admission raises
/// CryptoException InvalidStateError. Disposal closes pending publication and leaves returned keys usable.
/// </remarks>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type SubtleCryptoService =
    inherit IDisposable
    /// <summary>Whether this provider implements the key operation for the named algorithm.</summary>
    abstract Availability: SubtleOperation * algorithm: string -> CryptoOutcome<unit>
    /// <summary>Generates an owned key or key pair with the requested metadata and usages.</summary>
    abstract GenerateKey: CryptoAlgorithm * extractable: bool * CryptoKeyUsage array -> Async<CryptoOutcome<CryptoGeneratedKey>>

    /// <summary>Imports copied key data under the requested algorithm, extractability and usages.</summary>
    abstract ImportKey:
        CryptoKeyFormat * CryptoKeyData * CryptoAlgorithm * extractable: bool * CryptoKeyUsage array -> Async<CryptoOutcome<CryptoKey>>

    /// <summary>Exports an independent encoding of an extractable key.</summary>
    abstract ExportKey: CryptoKeyFormat * CryptoKey -> Async<CryptoOutcome<CryptoExportedKey>>
    /// <summary>Signs copied input using a key permitted to sign.</summary>
    abstract Sign: CryptoAlgorithm * CryptoKey * CryptoBuffer -> Async<CryptoOutcome<byte array>>
    /// <summary>Checks a copied signature against copied input; an invalid signature returns false.</summary>
    abstract Verify: CryptoAlgorithm * CryptoKey * signature: CryptoBuffer * data: CryptoBuffer -> Async<CryptoOutcome<bool>>
    /// <summary>Encrypts copied input using the requested algorithm and key.</summary>
    abstract Encrypt: CryptoAlgorithm * CryptoKey * CryptoBuffer -> Async<CryptoOutcome<byte array>>
    /// <summary>Decrypts copied input; authentication or padding failure raises OperationError.</summary>
    abstract Decrypt: CryptoAlgorithm * CryptoKey * CryptoBuffer -> Async<CryptoOutcome<byte array>>
    /// <summary>Derives key material with the requested bit length and output masking.</summary>
    abstract DeriveBits: CryptoAlgorithm * CryptoKey * lengthBits: CryptoField<uint64> -> Async<CryptoOutcome<byte array>>

    /// <summary>Derives and imports a key under the target algorithm and usages.</summary>
    abstract DeriveKey:
        CryptoAlgorithm * CryptoKey * target: CryptoAlgorithm * extractable: bool * CryptoKeyUsage array -> Async<CryptoOutcome<CryptoKey>>

    /// <summary>Exports and wraps a key using a key permitted to wrap.</summary>
    abstract WrapKey: CryptoKeyFormat * key: CryptoKey * wrappingKey: CryptoKey * CryptoAlgorithm -> Async<CryptoOutcome<byte array>>

    /// <summary>Unwraps copied data and imports the result under the target algorithm and usages.</summary>
    abstract UnwrapKey:
        CryptoKeyFormat *
        CryptoBuffer *
        unwrappingKey: CryptoKey *
        CryptoAlgorithm *
        target: CryptoAlgorithm *
        extractable: bool *
        CryptoKeyUsage array ->
            Async<CryptoOutcome<CryptoKey>>

/// <summary>Byte-range construction for crypto requests.</summary>
module CryptoBuffer =
    /// <summary>A view of the entire supplied array; operations copy it at admission.</summary>
    let whole (bytes: byte array) =
        CryptoContract.range bytes 0 (if isNull bytes then 0 else bytes.Length)

        {
            Bytes = bytes
            Offset = 0
            Count = bytes.Length
        }

/// <summary>Construction of presence-aware JWK dictionaries.</summary>
module JsonWebKey =
    /// <summary>A JWK dictionary whose members are absent.</summary>
    let empty: JsonWebKey =
        {
            Kty = CryptoField.Missing
            Use = CryptoField.Missing
            Alg = CryptoField.Missing
            KeyOps = CryptoField.Missing
            Ext = CryptoField.Missing
            K = CryptoField.Missing
            Crv = CryptoField.Missing
            X = CryptoField.Missing
            Y = CryptoField.Missing
            D = CryptoField.Missing
            N = CryptoField.Missing
            E = CryptoField.Missing
            P = CryptoField.Missing
            Q = CryptoField.Missing
            Dp = CryptoField.Missing
            Dq = CryptoField.Missing
            Qi = CryptoField.Missing
            Oth = CryptoField.Missing
        }
