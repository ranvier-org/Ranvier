namespace Ranvier.Crypto

open System
open System.Security.Cryptography
open System.Threading.Tasks
open Ranvier.Dom
open Ranvier.Dom.Cryptography
open Ranvier.Dom.Cryptography.CryptoKeyContract

/// <summary>Host-independent .NET crypto services.</summary>
module ManagedCrypto =
    module Jwk = CryptoKeyJwk

    let private wipe (bytes: byte[]) =
        CryptographicOperations.ZeroMemory (bytes.AsSpan ())

    let private compute hash (key: byte[]) (data: byte[]) =
        match hash with
        | DigestAlgorithm.Sha1 -> HMACSHA1.HashData (key, data)
        | DigestAlgorithm.Sha256 -> HMACSHA256.HashData (key, data)
        | DigestAlgorithm.Sha384 -> HMACSHA384.HashData (key, data)
        | DigestAlgorithm.Sha512 -> HMACSHA512.HashData (key, data)

    type private Lease =
        { Bytes: byte[]; Release: unit -> unit }

    type private HmacKey(bytes: byte[], hash, bits: uint64, extractable, permitted: CryptoKeyUsage[]) =
        let gate = obj ()
        let mutable disposed = false
        let mutable readers = 0
        member _.Hash = hash
        member _.Bits = bits
        member _.Permitted = Array.copy permitted

        member _.Acquire() =
            lock gate (fun () ->
                if disposed then
                    Result.Error (CryptoException ("InvalidAccessError", "Key is disposed."))
                else
                    readers <- readers + 1
                    let mutable released = false

                    Result.Ok
                        {
                            Bytes = bytes
                            Release =
                                fun () ->
                                    lock gate (fun () ->
                                        if not released then
                                            released <- true
                                            readers <- readers - 1

                                            if disposed && readers = 0 then
                                                wipe bytes)
                        })

        interface CryptoKey with
            member _.Type = CryptoKeyType.Secret
            member _.Extractable = extractable

            member _.Algorithm =
                {
                    Name = "HMAC"
                    Hash = Some (CryptoContract.digestName hash)
                    LengthBits = Some bits
                    NamedCurve = None
                    ModulusLengthBits = None
                    PublicExponent = None
                }

            member _.Usages = Array.copy permitted

            member _.Dispose() =
                lock gate (fun () ->
                    disposed <- true

                    if readers = 0 then
                        wipe bytes)

    let private retain (key: CryptoKey) =
        match box key with
        | :? HmacKey as own -> own, own.Acquire ()
        | _ -> Unchecked.defaultof<HmacKey>, Result.Error (CryptoException ("InvalidAccessError", "Key is not a compatible managed key."))

    let private release retained =
        match retained with
        | Result.Ok lease -> lease.Release ()
        | _ -> ()

    let private lease retained =
        match retained with
        | Result.Ok value -> value
        | Result.Error error -> raise error

    let private checkUse (key: HmacKey) usage =
        if not (Array.contains usage key.Permitted) then
            error "InvalidAccessError" "Key usage does not permit this operation."

    let private importKey formatValue keyData algorithm extractable requested =
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
                new HmacKey (mask bytes bits, hash, bits, extractable, requested) :> CryptoKey

            key
        finally
            if formatValue = CryptoKeyFormat.Jwk then
                wipe bytes

    /// <summary>A managed HMAC service with context-independent owned keys.</summary>
    /// <remarks>Supports HMAC generation, import, export, sign and verify. Other key operations report Unsupported.</remarks>
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

        { new SubtleCryptoService with
            member _.Availability(operation, algorithm) =
                lock gate (fun () ->
                    check ()

                    if
                        not (isNull algorithm)
                        && algorithm.Equals ("HMAC", StringComparison.OrdinalIgnoreCase)
                        && List.contains
                            operation
                            [
                                SubtleOperation.GenerateKey
                                SubtleOperation.ImportKey
                                SubtleOperation.ExportKey
                                SubtleOperation.Sign
                                SubtleOperation.Verify
                            ]
                    then
                        CryptoOutcome.Value ()
                    else
                        CryptoOutcome.Unsupported "This slice implements HMAC key operations.")

            member _.GenerateKey(algorithm, extractable, inputUsages) =
                check ()
                let requested = usages inputUsages

                start
                    (fun () ->
                        let hash, length = hmacParameters algorithm
                        allowedHmac requested
                        let bits = defaultArg length (defaultBits hash)

                        if bits = 0u then
                            error "OperationError" "HMAC length is zero."

                        nonemptyUsages requested
                        let bytes = RandomNumberGenerator.GetBytes (int ((uint64 bits + 7UL) / 8UL))
                        let canonical = mask bytes (uint64 bits)
                        wipe bytes

                        CryptoOutcome.Value (
                            CryptoGeneratedKey.Key (new HmacKey (canonical, hash, uint64 bits, extractable, requested) :> CryptoKey)
                        ))
                    dropGenerated
                    noRelease

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
                        | _ -> CryptoOutcome.Value (CryptoExportedKey.Jwk (Jwk.exportHmac key.Hash inputKey.Extractable key.Permitted held.Bytes)))
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

            member _.Encrypt(_, _, _) =
                unsupported ()

            member _.Decrypt(_, _, _) =
                unsupported ()

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
