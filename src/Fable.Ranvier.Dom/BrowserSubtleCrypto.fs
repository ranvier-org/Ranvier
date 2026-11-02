namespace Ranvier.Dom

open System
open Fable.Core
open Ranvier.Dom.Cryptography
open Ranvier.Dom.Cryptography.CryptoKeyContract

/// <summary>Key operations backed by the active document realm's WebCrypto provider.</summary>
module BrowserSubtleCrypto =
    [<Emit("$0.defaultView != null && $0.defaultView.document === $0")>]
    let private current (document: Document) : bool = jsNative

    [<Emit("$0.defaultView")>]
    let private realm (document: Document) : obj = jsNative

    [<Emit("typeof $0.crypto?.subtle?.importKey === 'function'")>]
    let private available (window: obj) : bool = jsNative

    [<Emit("Promise.resolve().then($0)")>]
    let private defer (work: unit -> JS.Promise<'T>) : JS.Promise<'T> = jsNative

    [<Emit("$0.then($1)")>]
    let private map (pending: JS.Promise<'T>) (accept: 'T -> 'U) : JS.Promise<'U> = jsNative

    [<Emit("(() => { const p={name:'HMAC',hash:$1}; if($2) p.length=$3; return $0.crypto.subtle.generateKey(p,$4,$5); })()")>]
    let private generate (window: obj) (hash: string) (present: bool) (bits: uint32) (extractable: bool) (usages: string array) : JS.Promise<obj> =
        jsNative

    [<Emit("(() => { const p={name:'HMAC',hash:$2}; if($3) p.length=$4; return $0.crypto.subtle.importKey('raw',$1,p,$5,$6); })()")>]
    let private importRaw
        (window: obj)
        (bytes: byte array)
        (hash: string)
        (present: bool)
        (bits: uint32)
        (extractable: bool)
        (usages: string array)
        : JS.Promise<obj> =
        jsNative

    [<Emit("$0.crypto.subtle.exportKey('raw',$1).then(b=>new Uint8Array(b))")>]
    let private exportRaw (window: obj) (key: obj) : JS.Promise<byte array> = jsNative

    [<Emit("$0.crypto.subtle.sign('HMAC',$1,$2).then(b=>new Uint8Array(b))")>]
    let private sign (window: obj) (key: obj) (bytes: byte array) : JS.Promise<byte array> = jsNative

    [<Emit("$0.crypto.subtle.verify('HMAC',$1,$2,$3)")>]
    let private verify (window: obj) (key: obj) (signature: byte array) (bytes: byte array) : JS.Promise<bool> = jsNative

    [<Emit("String($0?.name ?? 'Error')")>]
    let private errorName (value: obj) : string = jsNative

    [<Emit("String($0?.message ?? $0)")>]
    let private errorMessage (value: obj) : string = jsNative

    type private BrowserKey(native: obj, hash, bits: uint64, extractable, permitted: CryptoKeyUsage array) =
        let mutable key = Some native
        member _.Hash = hash
        member _.Permitted = Array.copy permitted

        member _.Capture() =
            match key with
            | Some value -> Result.Ok value
            | None -> Result.Error (CryptoException ("InvalidAccessError", "Key is disposed."))

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
                key <- None

    let private capture (key: CryptoKey) =
        match box key with
        | :? BrowserKey as own -> own, own.Capture ()
        | _ -> Unchecked.defaultof<BrowserKey>, Result.Error (CryptoException ("InvalidAccessError", "Key is not a compatible browser key."))

    let private native =
        function
        | Result.Ok value -> value
        | Result.Error failure -> raise failure

    let private checkUse (key: BrowserKey) usage =
        if not (Array.contains usage key.Permitted) then
            error "InvalidAccessError" "Key usage does not permit this operation."

    /// <summary>A key-operation service for an active document's realm.</summary>
    /// <remarks>
    /// Returned keys remain usable through compatible services after this service is disposed.
    /// SubtleCrypto requires a secure context.
    /// </remarks>
    /// <param name="document">An active document whose realm supplies WebCrypto.</param>
    let forDocument (document: Document) : SubtleCryptoService =
        if isNull (box document) then
            nullArg "document"

        if not (current document) then
            raise (NotSupportedException "Crypto requires an active document Window")

        let window = realm document
        let mutable disposed = false

        let check () =
            if disposed then
                error "InvalidStateError" "Service is disposed."

            if not (current document) then
                raise (InvalidOperationException "The crypto document is no longer active")

        let start work drop =
            check ()

            let pending =
                defer work
                |> fun pending ->
                    map pending (fun result ->
                        if disposed then
                            drop result
                            error "AbortError" "Service was disposed before completion."

                        CryptoOutcome.Value result)

            async {
                try
                    return! Async.AwaitPromise pending
                with
                | CryptoException _ as failure -> return raise failure
                | failure -> return raise (CryptoException (errorName failure, errorMessage failure))
            }

        let unsupported () =
            check ()
            async { return CryptoOutcome.Unsupported "This provider/context has no implementation of the requested key operation." }

        let ready () =
            available window

        let supported =
            [
                SubtleOperation.GenerateKey
                SubtleOperation.ImportKey
                SubtleOperation.ExportKey
                SubtleOperation.Sign
                SubtleOperation.Verify
            ]

        { new SubtleCryptoService with
            member _.Availability(operation, algorithm) =
                check ()

                if
                    ready ()
                    && isHmacName algorithm
                    && List.contains operation supported
                then
                    CryptoOutcome.Value ()
                else
                    CryptoOutcome.Unsupported "HMAC key operations require an available secure-context SubtleCrypto provider."

            member _.GenerateKey(algorithm, extractable, inputUsages) =
                check ()
                let requested = usages inputUsages

                if not (ready ()) then
                    unsupported ()
                else
                    start
                        (fun () ->
                            let hash, length = hmacParameters algorithm
                            allowedHmac requested
                            let bits = defaultArg length (defaultBits hash)

                            if bits = 0u then
                                error "OperationError" "HMAC length is zero."

                            nonemptyUsages requested

                            generate window (CryptoContract.digestName hash) (Option.isSome length) bits extractable (Array.map usageName requested)
                            |> fun pending ->
                                map pending (fun key ->
                                    CryptoGeneratedKey.Key (new BrowserKey (key, hash, uint64 bits, extractable, requested) :> CryptoKey)))
                        (function
                         | CryptoGeneratedKey.Key key -> key.Dispose ()
                         | CryptoGeneratedKey.Pair pair ->
                             pair.PublicKey.Dispose ()
                             pair.PrivateKey.Dispose ())

            member _.ImportKey(formatValue, inputData, algorithm, extractable, inputUsages) =
                check ()
                let formatValue = format formatValue
                let requested = usages inputUsages

                let data =
                    match inputData with
                    | CryptoKeyData.Bytes buffer -> CryptoKeyData.Bytes (CryptoBuffer.whole (copyBuffer buffer))
                    | CryptoKeyData.Jwk jwk -> CryptoKeyData.Jwk (CryptoKeyJwk.copy jwk)

                if not (ready ()) then
                    unsupported ()
                else
                    start
                        (fun () ->
                            let hash, length = hmacParameters algorithm

                            if length = Some 0u then
                                error "DataError" "HMAC length is zero."

                            allowedHmac requested

                            let bytes =
                                match formatValue, data with
                                | CryptoKeyFormat.Raw, CryptoKeyData.Bytes buffer -> buffer.Bytes
                                | CryptoKeyFormat.Jwk, CryptoKeyData.Jwk jwk -> CryptoKeyJwk.importHmac hash extractable requested jwk
                                | CryptoKeyFormat.Raw, _
                                | CryptoKeyFormat.Jwk, _ -> error "DataError" "Key data does not match its format."
                                | _ -> error "NotSupportedError" "HMAC permits raw and jwk formats."

                            let bits = logicalBits bytes (length |> Option.map uint64)
                            nonemptyUsages requested
                            let canonical = mask bytes bits

                            importRaw
                                window
                                canonical
                                (CryptoContract.digestName hash)
                                true
                                (checked32 bits)
                                extractable
                                (Array.map usageName requested)
                            |> fun pending -> map pending (fun key -> new BrowserKey (key, hash, bits, extractable, requested) :> CryptoKey))
                        (fun key -> key.Dispose ())

            member _.ExportKey(formatValue, inputKey) =
                check ()
                let formatValue = format formatValue
                let key, captured = capture inputKey

                if not (ready ()) then
                    unsupported ()
                else
                    start
                        (fun () ->
                            if
                                formatValue <> CryptoKeyFormat.Raw
                                && formatValue <> CryptoKeyFormat.Jwk
                            then
                                error "NotSupportedError" "HMAC permits raw and jwk formats."

                            let held = native captured

                            if not inputKey.Extractable then
                                error "InvalidAccessError" "Key is not extractable."

                            exportRaw window held
                            |> fun pending ->
                                map pending (fun bytes ->
                                    if formatValue = CryptoKeyFormat.Raw then
                                        CryptoExportedKey.Bytes bytes
                                    else
                                        CryptoExportedKey.Jwk (CryptoKeyJwk.exportHmac key.Hash inputKey.Extractable key.Permitted bytes)))
                        ignore

            member _.Sign(algorithm, inputKey, input) =
                check ()
                let bytes = copyBuffer input
                let key, captured = capture inputKey

                if not (ready ()) then
                    unsupported ()
                else
                    start
                        (fun () ->
                            requireHmac algorithm
                            let held = native captured
                            checkUse key CryptoKeyUsage.Sign
                            sign window held bytes)
                        ignore

            member _.Verify(algorithm, inputKey, signature, input) =
                check ()
                let bytes = copyBuffer input
                let signature = copyBuffer signature
                let key, captured = capture inputKey

                if not (ready ()) then
                    unsupported ()
                else
                    start
                        (fun () ->
                            requireHmac algorithm
                            let held = native captured
                            checkUse key CryptoKeyUsage.Verify
                            verify window held signature bytes)
                        ignore

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
                disposed <- true
        }
