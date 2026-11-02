namespace Ranvier.Dom.Cryptography

open System
open Ranvier.Dom
open CryptoKeyContract

/// <summary>Copied HMAC JWK data and algorithm-specific import validation.</summary>
module internal CryptoKeyJwk =
    let private empty = JsonWebKey.empty

    let private mapField f =
        function
        | CryptoField.Missing -> CryptoField.Missing
        | CryptoField.Present value -> CryptoField.Present (f value)

    let private text value =
        if isNull value then "null" else value

    let private copyArray f input =
        if isNull input then
            error "TypeError" "A sequence cannot be null."
        else
            Array.map f input

    /// <summary>Copies a typed JWK dictionary and its sequences while preserving UTF16 strings and absence.</summary>
    let copy (jwk: JsonWebKey) =
        if isNull (box jwk) then
            empty
        else
            let keyOps = jwk.KeyOps |> mapField (copyArray text)

            let oth =
                jwk.Oth
                |> mapField (
                    copyArray (fun item ->
                        if isNull (box item) then
                            {
                                R = CryptoField.Missing
                                D = CryptoField.Missing
                                T = CryptoField.Missing
                            }
                        else
                            {
                                R = mapField text item.R
                                D = mapField text item.D
                                T = mapField text item.T
                            })
                )

            { jwk with
                Kty = mapField text jwk.Kty
                Use = mapField text jwk.Use
                Alg = mapField text jwk.Alg
                K = mapField text jwk.K
                Crv = mapField text jwk.Crv
                X = mapField text jwk.X
                Y = mapField text jwk.Y
                D = mapField text jwk.D
                N = mapField text jwk.N
                E = mapField text jwk.E
                P = mapField text jwk.P
                Q = mapField text jwk.Q
                Dp = mapField text jwk.Dp
                Dq = mapField text jwk.Dq
                Qi = mapField text jwk.Qi
                KeyOps = keyOps
                Oth = oth
            }

    /// <summary>Decodes and validates an HMAC JWK under its hash, extractability and requested usages.</summary>
    let importHmac hash extractable usages (jwk: JsonWebKey) =
        if optional jwk.Kty <> Some "oct" then
            error "DataError" "HMAC JWK kty must be oct."

        let encoded =
            match jwk.K with
            | CryptoField.Present value -> value
            | _ -> error "DataError" "HMAC JWK k is required."

        let bytes = decode64 encoded

        match jwk.Alg with
        | CryptoField.Present value when value <> jwkAlg hash -> error "DataError" "HMAC JWK alg does not match."
        | _ -> ()

        match jwk.Use with
        | CryptoField.Present value when not (Array.isEmpty usages) && value <> "sig" -> error "DataError" "HMAC JWK use must be sig."
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

    /// <summary>Builds an independent HMAC JWK export.</summary>
    let exportHmac hash extractable usages bytes =
        { empty with
            Kty = CryptoField.Present "oct"
            K = CryptoField.Present (base64url bytes)
            Alg = CryptoField.Present (jwkAlg hash)
            Ext = CryptoField.Present extractable
            KeyOps = CryptoField.Present (Array.map usageName usages)
        }
