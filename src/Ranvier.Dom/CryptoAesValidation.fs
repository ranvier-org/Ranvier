namespace Ranvier.Dom.Cryptography

open System
open Ranvier.Dom
open CryptoKeyContract

module internal CryptoAesContract =
    let isCbcName value =
        name (CryptoAlgorithm.Name value) = "AES-CBC"

    let supports operation =
        List.contains
            operation
            [
                SubtleOperation.GenerateKey
                SubtleOperation.ImportKey
                SubtleOperation.ExportKey
                SubtleOperation.Encrypt
                SubtleOperation.Decrypt
            ]

    let requireCbc algorithm =
        if name algorithm <> "AES-CBC" then
            error "NotSupportedError" "Unknown AES-CBC algorithm."

    let generationLength algorithm =
        requireCbc algorithm
        let bits = lengthField algorithm |> required "length"

        if bits > uint64 UInt16.MaxValue then
            error "TypeError" "Value exceeds unsigned short."

        uint16 bits

    let validLength errorName bits =
        if bits <> 128UL && bits <> 192UL && bits <> 256UL then
            error errorName "AES key length must be 128, 192 or 256 bits."

    let allowedUsages usages =
        if
            usages
            |> Array.exists (fun usage ->
                usage <> CryptoKeyUsage.Encrypt
                && usage <> CryptoKeyUsage.Decrypt
                && usage <> CryptoKeyUsage.WrapKey
                && usage <> CryptoKeyUsage.UnwrapKey)
        then
            error "SyntaxError" "AES-CBC permits encrypt, decrypt, wrapKey and unwrapKey."

    let copyIv algorithm =
        requireCbc algorithm

        let field =
            match algorithm with
            | CryptoAlgorithm.AesCbc p -> p.Iv
            | CryptoAlgorithm.AesGcm p -> p.Iv
            | _ -> CryptoField.Missing

        field |> required "iv" |> copyBuffer

    let checkIv (iv: byte[]) =
        if iv.Length <> 16 then
            error "OperationError" "AES-CBC iv must contain 16 bytes."

    let private jwkAlgorithm bits =
        "A" + string bits + "CBC"

    let importJwk extractable usages (jwk: JsonWebKey) =
        if optional jwk.Kty <> Some "oct" then
            error "DataError" "AES JWK kty must be oct."

        let encoded =
            match jwk.K with
            | CryptoField.Present value -> value
            | _ -> error "DataError" "AES JWK k is required."

        let bytes = decode64 encoded

        try
            let bits = uint64 bytes.Length * 8UL
            validLength "DataError" bits

            match jwk.Alg with
            | CryptoField.Present value when value <> jwkAlgorithm bits -> error "DataError" "AES JWK alg does not match."
            | _ -> ()

            match jwk.Use with
            | CryptoField.Present value when not (Array.isEmpty usages) && value <> "enc" -> error "DataError" "AES JWK use must be enc."
            | _ -> ()

            match jwk.KeyOps with
            | CryptoField.Present ops ->
                if Array.distinct ops |> Array.length <> ops.Length then
                    error "DataError" "JWK key_ops has duplicates."

                if
                    usages
                    |> Array.exists (fun usage -> not (Array.contains (usageName usage) ops))
                then
                    error "DataError" "JWK key_ops omits a requested usage."
            | _ -> ()

            match jwk.Ext with
            | CryptoField.Present false when extractable -> error "DataError" "JWK ext is false."
            | _ -> ()

            bytes
        with failure ->
            Array.fill bytes 0 bytes.Length 0uy
            raise failure

    let exportJwk bits extractable usages bytes =
        { JsonWebKey.empty with
            Kty = CryptoField.Present "oct"
            K = CryptoField.Present (base64url bytes)
            Alg = CryptoField.Present (jwkAlgorithm bits)
            Ext = CryptoField.Present extractable
            KeyOps = CryptoField.Present (Array.map usageName usages)
        }
