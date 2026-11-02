namespace Ranvier.Dom.Cryptography

open System
open Ranvier.Dom

/// <summary>Pure argument conversion and HMAC normalization shared by native and browser providers.</summary>
module internal CryptoKeyContract =
    /// <summary>Raises a WebCrypto error with the supplied name.</summary>
    let error name detail =
        raise (CryptoException (name, detail))

    /// <summary>Converts a numeric dictionary value to unsigned long, rejecting overflow before browser conversion.</summary>
    let checked32 (value: uint64) =
        if value > uint64 UInt32.MaxValue then
            error "TypeError" "Value exceeds unsigned long."
        else
            uint32 value

    /// <summary>Reads a present dictionary member or raises TypeError.</summary>
    let required name =
        function
        | CryptoField.Present value -> value
        | CryptoField.Missing -> error "TypeError" (name + " is required.")

    /// <summary>Reads an optional dictionary member while preserving explicitly supplied values.</summary>
    let optional =
        function
        | CryptoField.Present value -> Some value
        | CryptoField.Missing -> None

    /// <summary>Copies a validated byte range before asynchronous work begins.</summary>
    let copyBuffer (buffer: CryptoBuffer) =
        if isNull (box buffer) then
            error "TypeError" "A byte range is required."

        CryptoContract.range buffer.Bytes buffer.Offset buffer.Count
        Array.sub buffer.Bytes buffer.Offset buffer.Count

    /// <summary>Converts a requested usage enum to its WebCrypto name or raises TypeError.</summary>
    let usageName =
        function
        | CryptoKeyUsage.Encrypt -> "encrypt"
        | CryptoKeyUsage.Decrypt -> "decrypt"
        | CryptoKeyUsage.Sign -> "sign"
        | CryptoKeyUsage.Verify -> "verify"
        | CryptoKeyUsage.DeriveKey -> "deriveKey"
        | CryptoKeyUsage.DeriveBits -> "deriveBits"
        | CryptoKeyUsage.WrapKey -> "wrapKey"
        | CryptoKeyUsage.UnwrapKey -> "unwrapKey"
        | _ -> error "TypeError" "Unknown KeyUsage."

    /// <summary>Copies, validates and lexicographically normalizes requested key usages.</summary>
    let usages (input: CryptoKeyUsage[]) =
        if isNull input then
            error "TypeError" "Key usages are required."

        input
        |> Array.map (fun usage ->
            usageName usage |> ignore
            usage)
        |> Array.distinct
        |> Array.sortBy usageName

    /// <summary>Rejects unknown key-format enum values with TypeError.</summary>
    let format (value: CryptoKeyFormat) =
        if int value < 0 || int value > 3 then
            error "TypeError" "Unknown KeyFormat."

        value

    /// <summary>Reads the name carried by a typed algorithm dictionary.</summary>
    let algorithmName =
        function
        | CryptoAlgorithm.Name name -> name
        | CryptoAlgorithm.HmacKey p -> p.Name
        | CryptoAlgorithm.AesKey p -> p.Name
        | CryptoAlgorithm.AesCbc p -> p.Name
        | CryptoAlgorithm.AesCtr p -> p.Name
        | CryptoAlgorithm.AesGcm p -> p.Name
        | CryptoAlgorithm.Hkdf p -> p.Name
        | CryptoAlgorithm.Pbkdf2 p -> p.Name
        | CryptoAlgorithm.RsaKey p -> p.Name
        | CryptoAlgorithm.RsaPss p -> p.Name
        | CryptoAlgorithm.RsaOaep p -> p.Name
        | CryptoAlgorithm.EcKey p -> p.Name
        | CryptoAlgorithm.Ecdsa p -> p.Name
        | CryptoAlgorithm.Agreement p -> p.Name

    /// <summary>Reads and normalizes a supplied algorithm name.</summary>
    let name (algorithm: CryptoAlgorithm) =
        if isNull (box algorithm) then
            error "TypeError" "Algorithm is required."

        let result = algorithmName algorithm

        if isNull result then
            error "TypeError" "Algorithm name is required."

        result.ToUpperInvariant ()

    /// <summary>Rejects algorithm names outside the HMAC slice.</summary>
    let requireHmac algorithm =
        if name algorithm <> "HMAC" then
            error "NotSupportedError" "This slice implements HMAC."

    /// <summary>Reads the hash member when the supplied dictionary contains it.</summary>
    let hashField =
        function
        | CryptoAlgorithm.HmacKey p -> p.Hash
        | CryptoAlgorithm.Hkdf p -> p.Hash
        | CryptoAlgorithm.Pbkdf2 p -> p.Hash
        | CryptoAlgorithm.RsaKey p -> p.Hash
        | CryptoAlgorithm.Ecdsa p -> p.Hash
        | _ -> CryptoField.Missing

    /// <summary>Reads the length member when the supplied dictionary contains it.</summary>
    let lengthField =
        function
        | CryptoAlgorithm.HmacKey p -> p.LengthBits
        | CryptoAlgorithm.AesKey p -> p.LengthBits
        | CryptoAlgorithm.AesCtr p -> p.LengthBits
        | _ -> CryptoField.Missing

    /// <summary>Normalizes only the hash and length members required for an HMAC key operation.</summary>
    let hmacParameters algorithm =
        requireHmac algorithm
        let hash = hashField algorithm |> required "hash"

        let length =
            lengthField algorithm
            |> optional
            |> Option.map checked32

        let normalized =
            match CryptoContract.digestAlgorithm hash with
            | Some value -> value
            | None -> error "NotSupportedError" "Unknown HMAC hash."

        normalized, length

    /// <summary>Rejects recognized usages outside sign and verify with SyntaxError.</summary>
    let allowedHmac usages =
        if
            Array.exists
                (fun value ->
                    value <> CryptoKeyUsage.Sign
                    && value <> CryptoKeyUsage.Verify)
                usages
        then
            error "SyntaxError" "HMAC permits sign and verify."

    /// <summary>Rejects an empty usage list for a secret key.</summary>
    let nonemptyUsages usages =
        if Array.isEmpty usages then
            error "SyntaxError" "A secret key needs a usage."

    /// <summary>The default HMAC key length in bits for a hash.</summary>
    let defaultBits =
        function
        | DigestAlgorithm.Sha384
        | DigestAlgorithm.Sha512 -> 1024u
        | _ -> 512u

    /// <summary>Validates an imported key's positive logical bit length against its ceiling-byte storage.</summary>
    let logicalBits (bytes: byte[]) length =
        let rawBits = uint64 bytes.Length * 8UL
        let bits = defaultArg length rawBits

        if
            bits = 0UL
            || bits > rawBits
            || (bits + 7UL) / 8UL <> uint64 bytes.Length
        then
            error "DataError" "Invalid HMAC key length."

        bits

    /// <summary>Copies a key's ceiling-byte storage and clears unused low bits.</summary>
    let mask (bytes: byte[]) (bits: uint64) =
        let output = Array.copy bytes
        let rem = int (bits % 8UL)

        if rem <> 0 then
            output[output.Length - 1] <-
                output[output.Length - 1]
                &&& byte (255 <<< (8 - rem))

        output

    /// <summary>The exact JWA algorithm name of an HMAC hash.</summary>
    let jwkAlg =
        function
        | DigestAlgorithm.Sha1 -> "HS1"
        | DigestAlgorithm.Sha256 -> "HS256"
        | DigestAlgorithm.Sha384 -> "HS384"
        | DigestAlgorithm.Sha512 -> "HS512"

    /// <summary>Encodes independent bytes as unpadded base64url.</summary>
    let base64url (bytes: byte[]) =
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')

    /// <summary>Decodes base64url key data or raises DataError.</summary>
    let decode64 (text: string) =
        if
            isNull text
            || text.Length % 4 = 1
            || text
               |> Seq.exists (fun c ->
                   not (
                       (c >= 'a' && c <= 'z')
                       || (c >= 'A' && c <= 'Z')
                       || (c >= '0' && c <= '9')
                       || c = '-'
                       || c = '_'
                   ))
        then
            error "DataError" "Invalid base64url."

        let value = text.Replace('-', '+').Replace('_', '/')

        try
            Convert.FromBase64String (value.PadRight ((value.Length + 3) / 4 * 4, '='))
        with :? FormatException ->
            error "DataError" "Invalid base64url."
