namespace Ranvier.Crypto

open System
open System.Security.Cryptography
open Ranvier.Dom
open Ranvier.Dom.Cryptography
open Ranvier.Dom.Cryptography.CryptoKeyContract

module internal ManagedKeys =
    let private wipe (bytes: byte[]) =
        CryptographicOperations.ZeroMemory (bytes.AsSpan ())

    type Lease =
        {
            Bytes: byte[]
            Release: unit -> unit
        }

        override _.ToString() =
            "CryptoKeyLease"

    type SecretKey private (bytes: byte[], name: string, hash: DigestAlgorithm option, bits: uint64, extractable, permitted: CryptoKeyUsage[]) =
        let gate = obj ()
        let mutable disposed = false
        let mutable readers = 0
        let permitted = Array.copy permitted

        static member CreateHmac(bytes, hash, bits, extractable, permitted) =
            new SecretKey (bytes, "HMAC", Some hash, bits, extractable, permitted)

        member _.Name = name

        member _.Hash =
            match hash with
            | Some value -> value
            | None -> error "InvalidAccessError" "Key is not an HMAC key."

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
                    Name = name
                    Hash = hash |> Option.map CryptoContract.digestName
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

    let retain (key: CryptoKey) =
        match box key with
        | :? SecretKey as own -> own, own.Acquire ()
        | _ -> Unchecked.defaultof<SecretKey>, Result.Error (CryptoException ("InvalidAccessError", "Key is not a compatible managed key."))

    let release retained =
        match retained with
        | Result.Ok held -> held.Release ()
        | _ -> ()

    let lease retained =
        match retained with
        | Result.Ok value -> value
        | Result.Error failure -> raise failure

    let checkUse (key: SecretKey) usage =
        if not (Array.contains usage key.Permitted) then
            error "InvalidAccessError" "Key usage does not permit this operation."
