namespace Ranvier.Crypto

open System
open System.Security.Cryptography
open System.Threading.Tasks
open Ranvier.Dom
open Ranvier.Dom.Cryptography
open Ranvier.Dom.Cryptography.CryptoKeyContract

/// <summary>Host-independent .NET crypto services.</summary>
module ManagedCrypto =
    open ManagedKeys

    module Jwk = CryptoKeyJwk

    let private wipe (bytes: byte[]) =
        CryptographicOperations.ZeroMemory (bytes.AsSpan ())

    let private compute hash (key: byte[]) (data: byte[]) =
        match hash with
        | DigestAlgorithm.Sha1 -> HMACSHA1.HashData (key, data)
        | DigestAlgorithm.Sha256 -> HMACSHA256.HashData (key, data)
        | DigestAlgorithm.Sha384 -> HMACSHA384.HashData (key, data)
        | DigestAlgorithm.Sha512 -> HMACSHA512.HashData (key, data)

    let private importHmac formatValue keyData algorithm extractable requested =
        let hash, length = hmacParameters algorithm

        if length = Some 0u then
            error "DataError" "HMAC length is zero."

        allowedHmac requested

        let bytes =
            match formatValue, keyData with
            | CryptoKeyFormat.Raw, CryptoKeyData.Bytes buffer -> buffer.Bytes
            | CryptoKeyFormat.Jwk, CryptoKeyData.Jwk jwk -> Jwk.importHmac hash extractable requested jwk
            | CryptoKeyFormat.Raw, _
            | CryptoKeyFormat.Jwk, _ -> error "DataError" "Key data does not match its format."
            | _ -> error "NotSupportedError" "HMAC permits raw and jwk formats."

        try
            let bits = logicalBits bytes (length |> Option.map uint64)
            nonemptyUsages requested

            let key =
                SecretKey.CreateHmac (mask bytes bits, hash, bits, extractable, requested) :> CryptoKey

            key
        finally
            if formatValue = CryptoKeyFormat.Jwk then
                wipe bytes

    let private importKey formatValue keyData algorithm extractable requested =
        if name algorithm = "AES-CBC" then
            CryptoAesContract.allowedUsages requested

            let bytes =
                match formatValue, keyData with
                | CryptoKeyFormat.Raw, CryptoKeyData.Bytes buffer -> buffer.Bytes
                | CryptoKeyFormat.Jwk, CryptoKeyData.Jwk jwk -> CryptoAesContract.importJwk extractable requested jwk
                | CryptoKeyFormat.Raw, _
                | CryptoKeyFormat.Jwk, _ -> error "DataError" "Key data does not match its format."
                | _ -> error "NotSupportedError" "AES-CBC permits raw and jwk formats."

            try
                CryptoAesContract.validLength "DataError" (uint64 bytes.Length * 8UL)
                nonemptyUsages requested
                SecretKey.CreateCbc (Array.copy bytes, extractable, requested) :> CryptoKey
            finally
                if formatValue = CryptoKeyFormat.Jwk then
                    wipe bytes
        else
            importHmac formatValue keyData algorithm extractable requested

    let private generateKey algorithm extractable requested =
        if name algorithm = "AES-CBC" then
            let bits = CryptoAesContract.generationLength algorithm
            CryptoAesContract.allowedUsages requested
            CryptoAesContract.validLength "OperationError" (uint64 bits)
            nonemptyUsages requested
            SecretKey.CreateCbc (RandomNumberGenerator.GetBytes (int bits / 8), extractable, requested) :> CryptoKey
        else
            let hash, length = hmacParameters algorithm
            allowedHmac requested
            let bits = defaultArg length (defaultBits hash)

            if bits = 0u then
                error "OperationError" "HMAC length is zero."

            nonemptyUsages requested
            let bytes = RandomNumberGenerator.GetBytes (int ((uint64 bits + 7UL) / 8UL))
            let canonical = mask bytes (uint64 bits)
            wipe bytes
            SecretKey.CreateHmac (canonical, hash, uint64 bits, extractable, requested) :> CryptoKey

    /// <summary>A managed HMAC and AES-CBC service with context-independent owned keys.</summary>
    /// <remarks>Supports HMAC sign/verify and AES-CBC encrypt/decrypt, with generation and raw/JWK key formats.</remarks>
    let create () : SubtleCryptoService =
        let gate = obj ()
        let mutable disposed = false

        let check () =
            if disposed then
                error "InvalidStateError" "Service is disposed."

        let start (work: unit -> CryptoOutcome<'T>) (drop: CryptoOutcome<'T> -> unit) releaseInputs : Async<CryptoOutcome<'T>> =
            let pending =
                lock gate (fun () ->
                    if disposed then
                        releaseInputs ()
                        error "InvalidStateError" "Service is disposed."

                    Task.Run<CryptoOutcome<'T>>(
                        Func<CryptoOutcome<'T>>(fun () ->
                            try
                                let result = work ()

                                lock gate (fun () ->
                                    if disposed then
                                        drop result
                                        error "AbortError" "Service was disposed before completion."

                                    result)
                            finally
                                releaseInputs ())
                    ))

            async {
                try
                    return! Async.AwaitTask pending
                with :? AggregateException as failure when failure.InnerExceptions.Count = 1 ->
                    return raise failure.InnerExceptions[0]
            }

        let noDrop _ = ()
        let noRelease () = ()

        let dropKey =
            function
            | CryptoOutcome.Value (key: CryptoKey) -> key.Dispose ()
            | _ -> ()

        let dropGenerated =
            function
            | CryptoOutcome.Value (CryptoGeneratedKey.Key key) -> key.Dispose ()
            | CryptoOutcome.Value (CryptoGeneratedKey.Pair pair) ->
                pair.PublicKey.Dispose ()
                pair.PrivateKey.Dispose ()
            | _ -> ()

        let dropBytes =
            function
            | CryptoOutcome.Value bytes -> wipe bytes
            | _ -> ()

        let unsupported () =
            start (fun () -> CryptoOutcome.Unsupported "This slice implements HMAC key operations.") noDrop noRelease

        let crypt decrypt algorithm inputKey input =
            check ()
            let bytes = copyBuffer input
            let iv = CryptoAesContract.copyIv algorithm
            let key, retained = retain inputKey

            start
                (fun () ->
                    let held = lease retained

                    if key.Name <> "AES-CBC" then
                        error "InvalidAccessError" "Key is not an AES-CBC key."

                    checkUse
                        key
                        (if decrypt then
                             CryptoKeyUsage.Decrypt
                         else
                             CryptoKeyUsage.Encrypt)

                    CryptoAesContract.checkIv iv

                    try
                        use aes = Aes.Create ()
                        aes.Key <- held.Bytes

                        let result =
                            if decrypt then
                                aes.DecryptCbc (bytes, iv, PaddingMode.PKCS7)
                            else
                                aes.EncryptCbc (bytes, iv, PaddingMode.PKCS7)

                        CryptoOutcome.Value result
                    with :? CryptographicException ->
                        error "OperationError" "AES-CBC operation failed.")
                dropBytes
                (fun () ->
                    wipe bytes
                    wipe iv
                    release retained)

        { new SubtleCryptoService with
            member _.Availability(operation, algorithm) =
                lock gate (fun () ->
                    check ()

                    if
                        (isHmacName algorithm
                         && List.contains
                             operation
                             [
                                 SubtleOperation.GenerateKey
                                 SubtleOperation.ImportKey
                                 SubtleOperation.ExportKey
                                 SubtleOperation.Sign
                                 SubtleOperation.Verify
                             ])
                        || (CryptoAesContract.isCbcName algorithm
                            && CryptoAesContract.supports operation)
                    then
                        CryptoOutcome.Value ()
                    else
                        CryptoOutcome.Unsupported "This slice implements HMAC key operations.")

            member _.GenerateKey(algorithm, extractable, inputUsages) =
                check ()
                let requested = usages inputUsages

                start (fun () -> CryptoOutcome.Value (CryptoGeneratedKey.Key (generateKey algorithm extractable requested))) dropGenerated noRelease

            member _.ImportKey(formatValue, inputData, algorithm, extractable, inputUsages) =
                check ()
                let formatValue = format formatValue
                let requested = usages inputUsages

                let data =
                    match inputData with
                    | CryptoKeyData.Bytes input ->
                        CryptoKeyData.Bytes
                            {
                                Bytes = copyBuffer input
                                Offset = 0
                                Count = input.Count
                            }
                    | CryptoKeyData.Jwk jwk -> CryptoKeyData.Jwk (Jwk.copy jwk)

                let finish () =
                    match data with
                    | CryptoKeyData.Bytes b -> wipe b.Bytes
                    | _ -> ()

                start (fun () -> CryptoOutcome.Value (importKey formatValue data algorithm extractable requested)) dropKey finish

            member _.ExportKey(formatValue, inputKey) =
                check ()
                let formatValue = format formatValue
                let key, retained = retain inputKey

                start
                    (fun () ->
                        if
                            formatValue <> CryptoKeyFormat.Raw
                            && formatValue <> CryptoKeyFormat.Jwk
                        then
                            error "NotSupportedError" "HMAC permits raw and jwk formats."

                        let held = lease retained

                        if not inputKey.Extractable then
                            error "InvalidAccessError" "Key is not extractable."

                        match formatValue with
                        | CryptoKeyFormat.Raw -> CryptoOutcome.Value (CryptoExportedKey.Bytes (Array.copy held.Bytes))
                        | _ ->
                            let jwk =
                                if key.Name = "AES-CBC" then
                                    CryptoAesContract.exportJwk key.Bits inputKey.Extractable key.Permitted held.Bytes
                                else
                                    Jwk.exportHmac key.Hash inputKey.Extractable key.Permitted held.Bytes

                            CryptoOutcome.Value (CryptoExportedKey.Jwk jwk))
                    (function
                    | CryptoOutcome.Value (CryptoExportedKey.Bytes b) -> wipe b
                    | _ -> ())
                    (fun () -> release retained)

            member _.Sign(algorithm, inputKey, input) =
                check ()
                let bytes = copyBuffer input
                let key, retained = retain inputKey

                start
                    (fun () ->
                        requireHmac algorithm
                        let held = lease retained

                        if key.Name <> "HMAC" then
                            error "InvalidAccessError" "Key is not an HMAC key."

                        checkUse key CryptoKeyUsage.Sign
                        CryptoOutcome.Value (compute key.Hash held.Bytes bytes))
                    dropBytes
                    (fun () ->
                        wipe bytes
                        release retained)

            member _.Verify(algorithm, inputKey, signature, input) =
                check ()
                let bytes = copyBuffer input
                let signature = copyBuffer signature
                let key, retained = retain inputKey

                start
                    (fun () ->
                        requireHmac algorithm
                        let held = lease retained

                        if key.Name <> "HMAC" then
                            error "InvalidAccessError" "Key is not an HMAC key."

                        checkUse key CryptoKeyUsage.Verify
                        let actual = compute key.Hash held.Bytes bytes

                        try
                            CryptoOutcome.Value (CryptographicOperations.FixedTimeEquals (actual, signature))
                        finally
                            wipe actual)
                    noDrop
                    (fun () ->
                        wipe bytes
                        wipe signature
                        release retained)

            member _.Encrypt(algorithm, key, input) =
                crypt false algorithm key input

            member _.Decrypt(algorithm, key, input) =
                crypt true algorithm key input

            member _.DeriveBits(_, _, _) =
                unsupported ()

            member _.DeriveKey(_, _, _, _, inputUsages) =
                check ()
                usages inputUsages |> ignore
                unsupported ()

            member _.WrapKey(_, _, _, _) =
                unsupported ()

            member _.UnwrapKey(_, _, _, _, _, _, inputUsages) =
                check ()
                usages inputUsages |> ignore
                unsupported ()

            member _.Dispose() =
                lock gate (fun () -> disposed <- true)
        }
