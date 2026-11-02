module internal Ranvier.Crypto.Tests.HmacChecks

open System
open System.Threading
open Ranvier.Dom
open Ranvier.Dom.Cryptography

/// <summary>A published HMAC output or explicitly published truncated prefix.</summary>
type Vector =
    {
        id: string
        hash: string
        key: string
        data: string
        mac: string
        prefix: bool
    }

/// <summary>Assertions and independently aggregated test-group failures.</summary>
type Report =
    {
        assertions: int
        groups: int
        failures: string array
    }

let private bytes (text: string) =
    Array.init (text.Length / 2) (fun i -> Convert.ToByte (text.Substring (i * 2, 2), 16))

let private hex (input: byte array) =
    input
    |> Array.map (fun b -> b.ToString "x2")
    |> String.concat ""

let private value =
    function
    | CryptoOutcome.Value x -> x
    | CryptoOutcome.Unsupported why -> failwith why

let private keyParameters hash length =
    CryptoAlgorithm.HmacKey
        {
            Name = "HMAC"
            Hash = CryptoField.Present hash
            LengthBits = length
        }

/// <summary>Runs the complete HMAC slice through a public provider, including key lifetime and error ordering.</summary>
let run (create: unit -> SubtleCryptoService) (vectors: Vector array) =
    async {
        let mutable assertions = 0
        let mutable groups = 0
        let failures = ResizeArray<string>()

        let equal label expected actual =
            assertions <- assertions + 1

            if expected <> actual then
                failwith (label + ": values differ")

        let group name work =
            async {
                groups <- groups + 1

                try
                    do! work ()
                with error ->
                    failures.Add (name + ": " + error.Message)
            }

        let errorName expected action =
            async {
                let! actual =
                    async {
                        try
                            let! _ = action ()
                            return "no error"
                        with CryptoException (name, _) ->
                            return name
                    }

                equal "error name" expected actual
            }

        let createImported (service: SubtleCryptoService) hash input length extractable usages =
            async {
                let! imported =
                    service.ImportKey (
                        CryptoKeyFormat.Raw,
                        CryptoKeyData.Bytes (CryptoBuffer.whole input),
                        keyParameters hash length,
                        extractable,
                        usages
                    )

                return value imported
            }

        for vector in vectors do
            do!
                group vector.id (fun () ->
                    async {
                        use service = create ()
                        let input = bytes vector.data
                        let raw = bytes vector.key

                        let! key =
                            createImported service vector.hash raw CryptoField.Missing true [| CryptoKeyUsage.Sign; CryptoKeyUsage.Verify |]

                        try
                            let! signed =
                                service.Sign (CryptoAlgorithm.Name "HMAC", key, CryptoBuffer.whole input)

                            let mac = value signed

                            equal
                                "published output"
                                vector.mac
                                (if vector.prefix then
                                     (hex mac).Substring(0, vector.mac.Length)
                                 else
                                     hex mac)

                            let! verified =
                                service.Verify (CryptoAlgorithm.Name "HMAC", key, CryptoBuffer.whole mac, CryptoBuffer.whole input)

                            equal "full signature verifies" true (value verified)
                            let altered = Array.copy mac
                            altered[0] <- altered[0] ^^^ 1uy

                            let! rejected =
                                service.Verify (CryptoAlgorithm.Name "HMAC", key, CryptoBuffer.whole altered, CryptoBuffer.whole input)

                            equal "altered signature" false (value rejected)
                            equal "algorithm hash" (Some vector.hash) key.Algorithm.Hash
                            let! exported = service.ExportKey (CryptoKeyFormat.Raw, key)

                            match value exported with
                            | CryptoExportedKey.Bytes b -> equal "raw roundtrip" raw b
                            | _ -> failwith "Wrong export format"

                            let! jwk = service.ExportKey (CryptoKeyFormat.Jwk, key)

                            let jwk =
                                match value jwk with
                                | CryptoExportedKey.Jwk j -> j
                                | _ -> failwith "Wrong JWK export"

                            equal "JWK ext" (CryptoField.Present true) jwk.Ext

                            let! reimported =
                                service.ImportKey (
                                    CryptoKeyFormat.Jwk,
                                    CryptoKeyData.Jwk jwk,
                                    keyParameters vector.hash CryptoField.Missing,
                                    true,
                                    [| CryptoKeyUsage.Sign |]
                                )

                            let reimported = value reimported

                            try
                                let! signedAgain =
                                    service.Sign (CryptoAlgorithm.Name "HMAC", reimported, CryptoBuffer.whole input)

                                equal "JWK signature roundtrip" mac (value signedAgain)
                            finally
                                reimported.Dispose ()
                        finally
                            key.Dispose ()
                    })

        for hash in [ "SHA-1"; "SHA-256"; "SHA-384"; "SHA-512" ] do
            for length in [ CryptoField.Missing; CryptoField.Present 159UL ] do
                let label =
                    match length with
                    | CryptoField.Missing -> "default"
                    | CryptoField.Present bits -> string bits

                do!
                    group ("generate/" + hash + "/" + label) (fun () ->
                        async {
                            use a = create ()
                            use b = create ()

                            let! generated =
                                a.GenerateKey (keyParameters hash length, true, [| CryptoKeyUsage.Sign; CryptoKeyUsage.Verify |])

                            let key =
                                match value generated with
                                | CryptoGeneratedKey.Key k -> k
                                | _ -> failwith "Expected secret key"

                            try
                                a.Dispose ()

                                let bits =
                                    match length with
                                    | CryptoField.Present n -> n
                                    | _ ->
                                        if hash = "SHA-384" || hash = "SHA-512" then
                                            1024UL
                                        else
                                            512UL

                                equal "logical length" (Some bits) key.Algorithm.LengthBits
                                let input = CryptoBuffer.whole [| 1uy; 2uy; 3uy |]
                                let! signed = b.Sign (CryptoAlgorithm.Name "hmac", key, input)

                                let! verified =
                                    b.Verify (CryptoAlgorithm.Name "HMAC", key, CryptoBuffer.whole (value signed), input)

                                equal "generated key survives originating service" true (value verified)
                                let! exported = b.ExportKey (CryptoKeyFormat.Raw, key)

                                match value exported with
                                | CryptoExportedKey.Bytes data ->
                                    equal "ceiling byte length" (int ((bits + 7UL) / 8UL)) data.Length

                                    if bits % 8UL <> 0UL then
                                        equal "canonical low bit" 0 (int data[data.Length - 1] &&& 1)
                                | _ -> failwith "Expected raw"
                            finally
                                key.Dispose ()
                        })

        do!
            group "lifetime/copies/repeated-await" (fun () ->
                async {
                    use a = create ()
                    use b = create ()
                    let raw = Array.create 20 0x0buy
                    let requested = [| CryptoKeyUsage.Sign; CryptoKeyUsage.Verify |]

                    let imported =
                        a.ImportKey (
                            CryptoKeyFormat.Raw,
                            CryptoKeyData.Bytes (CryptoBuffer.whole raw),
                            keyParameters "SHA-256" CryptoField.Missing,
                            true,
                            requested
                        )

                    Array.fill raw 0 raw.Length 0uy
                    requested[0] <- CryptoKeyUsage.Encrypt
                    let! first = imported
                    let! repeated = imported
                    let key = value first
                    equal "single computation" true (obj.ReferenceEquals (key, value repeated))
                    a.Dispose ()
                    let metadata = key.Usages
                    metadata[0] <- CryptoKeyUsage.Encrypt
                    let input = [| 72uy; 105uy; 32uy; 84uy; 104uy; 101uy; 114uy; 101uy |]

                    let signed =
                        b.Sign (
                            CryptoAlgorithm.HmacKey
                                {
                                    Name = "HMAC"
                                    Hash = CryptoField.Missing
                                    LengthBits = CryptoField.Present UInt64.MaxValue
                                },
                            key,
                            CryptoBuffer.whole input
                        )

                    Array.fill input 0 input.Length 0uy
                    key.Dispose ()
                    let! mac = signed
                    equal "copied input and retained key" "b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7" (hex (value mac))
                    do! errorName "InvalidAccessError" (fun () -> b.Sign (CryptoAlgorithm.Name "HMAC", key, CryptoBuffer.whole [||]))
                    b.Dispose ()

                    do!
                        errorName "InvalidStateError" (fun () ->
                            b.GenerateKey (keyParameters "SHA-256" CryptoField.Missing, true, [| CryptoKeyUsage.Sign |]))
                })

        do!
            group "lifetime/waiter-local-cancellation" (fun () ->
                async {
                    use service = create ()
                    use observerCancellation = new CancellationTokenSource ()

                    let operation =
                        service.GenerateKey (keyParameters "SHA-256" CryptoField.Missing, true, [| CryptoKeyUsage.Sign; CryptoKeyUsage.Verify |])

                    let mutable observed: CryptoKey option = None

                    let! cancelled =
                        Async.FromContinuations (fun (success, failure, _) ->
                            let observer =
                                async {
                                    let! generated = operation

                                    observed <-
                                        match value generated with
                                        | CryptoGeneratedKey.Key key -> Some key
                                        | _ -> failwith "Expected secret key"

                                    observerCancellation.Cancel ()
                                    do! Async.Sleep 1
                                }

                            Async.StartWithContinuations (
                                observer,
                                (fun () -> success false),
                                failure,
                                (fun _ -> success true),
                                observerCancellation.Token
                            ))

                    let! generated = operation

                    let key =
                        match value generated with
                        | CryptoGeneratedKey.Key key -> key
                        | _ -> failwith "Expected secret key"

                    try
                        equal "first waiter cancelled" true cancelled
                        equal "same cached key after cancellation" true (obj.ReferenceEquals (key, observed.Value))
                        let input = CryptoBuffer.whole [| 1uy; 2uy |]
                        let! signed = service.Sign (CryptoAlgorithm.Name "HMAC", key, input)
                        let signature = value signed
                        equal "key remains signable" 32 signature.Length

                        let! verified =
                            service.Verify (CryptoAlgorithm.Name "HMAC", key, CryptoBuffer.whole signature, input)

                        equal "signature verifies after waiter cancellation" true (value verified)
                    finally
                        key.Dispose ()
                })

        let nameCases =
            [|
                "upper", "HMAC", "SHA-256", true
                "lower", "hmac", "SHA-256", true
                "mixed", "HmAc", "SHA-256", true
                "long-s", "\u017fHMAC", "SHA-256", false
                "dotless-i", "HMA\u0131", "SHA-256", false
                "kelvin", "HMA\u212a", "SHA-256", false
                "accent", "H\u00c1MAC", "SHA-256", false
                "unicode-leading", "\u00a0HMAC", "SHA-256", false
                "unicode-trailing", "HMAC\u00a0", "SHA-256", false
                "bom-leading", "\ufeffHMAC", "SHA-256", false
                "ascii-leading", " HMAC", "SHA-256", false
                "ascii-trailing", "HMAC ", "SHA-256", false
                "nul", "HMAC\u0000", "SHA-256", false
                "null", null, "SHA-256", false
                "empty", "", "SHA-256", false
            |]

        let hashCases =
            [|
                "upper", "HMAC", "SHA-256", true
                "lower", "HMAC", "sha-256", true
                "mixed", "HMAC", "sHa-256", true
                "long-s", "HMAC", "\u017fHA-256", false
                "dotless-i", "HMAC", "SHA-\u0131", false
                "kelvin", "HMAC", "\u212aSHA-256", false
                "accent", "HMAC", "SH\u00c1-256", false
                "unicode-leading", "HMAC", "\u00a0SHA-256", false
                "unicode-trailing", "HMAC", "SHA-256\u00a0", false
                "bom-leading", "HMAC", "\ufeffSHA-256", false
                "ascii-leading", "HMAC", " SHA-256", false
                "ascii-trailing", "HMAC", "SHA-256 ", false
                "nul", "HMAC", "SHA\u0000-256", false
                "null", "HMAC", null, false
                "empty", "HMAC", "", false
            |]

        for field, cases in [ "name", nameCases; "hash", hashCases ] do
            for label, name, hash, accepted in cases do
                do!
                    group ("ascii-normalization/" + field + "/" + label) (fun () ->
                        async {
                            use service = create ()

                            let algorithm =
                                CryptoAlgorithm.HmacKey
                                    {
                                        Name = name
                                        Hash = CryptoField.Present hash
                                        LengthBits = CryptoField.Missing
                                    }

                            let generate () =
                                service.GenerateKey (algorithm, true, [| CryptoKeyUsage.Sign |])

                            let import () =
                                service.ImportKey (
                                    CryptoKeyFormat.Raw,
                                    CryptoKeyData.Bytes (CryptoBuffer.whole [| 1uy; 2uy |]),
                                    algorithm,
                                    true,
                                    [| CryptoKeyUsage.Sign |]
                                )

                            if accepted then
                                let! generated = generate ()

                                use generatedKey =
                                    match value generated with
                                    | CryptoGeneratedKey.Key key -> key
                                    | _ -> failwith "Expected secret key"

                                equal "generated name" "HMAC" generatedKey.Algorithm.Name
                                equal "generated hash" (Some "SHA-256") generatedKey.Algorithm.Hash
                                let! imported = import ()
                                use importedKey = value imported
                                equal "imported name" "HMAC" importedKey.Algorithm.Name
                                equal "imported hash" (Some "SHA-256") importedKey.Algorithm.Hash
                            else
                                do! errorName "NotSupportedError" generate
                                do! errorName "NotSupportedError" import
                        })

        do!
            group "ascii-normalization/ignored-operation-fields" (fun () ->
                async {
                    use service = create ()

                    let! imported =
                        createImported service "SHA-256" [| 1uy; 2uy |] CryptoField.Missing true [| CryptoKeyUsage.Sign; CryptoKeyUsage.Verify |]

                    use key = imported

                    let operation =
                        CryptoAlgorithm.HmacKey
                            {
                                Name = "hMaC"
                                Hash = CryptoField.Present "\u017fHA-256"
                                LengthBits = CryptoField.Present UInt64.MaxValue
                            }

                    let input = CryptoBuffer.whole [| 3uy |]
                    let! signed = service.Sign (operation, key, input)
                    equal "irrelevant fields ignored when signing" 32 (value signed).Length

                    let! verified =
                        service.Verify (operation, key, CryptoBuffer.whole (value signed), input)

                    equal "irrelevant fields ignored when verifying" true (value verified)

                    do! errorName "TypeError" (fun () -> service.GenerateKey (Unchecked.defaultof<CryptoAlgorithm>, true, [| CryptoKeyUsage.Sign |]))

                    do!
                        errorName "TypeError" (fun () ->
                            service.ImportKey (
                                CryptoKeyFormat.Raw,
                                CryptoKeyData.Bytes input,
                                Unchecked.defaultof<CryptoAlgorithm>,
                                true,
                                [| CryptoKeyUsage.Sign |]
                            ))
                })

        do!
            group "aot/diagnostic-displays" (fun () ->
                async {
                    let secret = "private-material-do-not-print"
                    equal "Value display" "Value" ((CryptoOutcome.Value secret).ToString())
                    equal "Unsupported display" "Unsupported: reason" ((CryptoOutcome.Unsupported "reason").ToString())

                    for algorithm, expected in
                        [
                            DigestAlgorithm.Sha1, "Sha1"
                            DigestAlgorithm.Sha256, "Sha256"
                            DigestAlgorithm.Sha384, "Sha384"
                            DigestAlgorithm.Sha512, "Sha512"
                        ] do
                        equal "digest case display" expected (algorithm.ToString ())

                    for operation, expected in
                        [
                            SubtleOperation.Digest, "Digest"
                            SubtleOperation.Encrypt, "Encrypt"
                            SubtleOperation.Decrypt, "Decrypt"
                            SubtleOperation.Sign, "Sign"
                            SubtleOperation.Verify, "Verify"
                            SubtleOperation.GenerateKey, "GenerateKey"
                            SubtleOperation.DeriveKey, "DeriveKey"
                            SubtleOperation.DeriveBits, "DeriveBits"
                            SubtleOperation.ImportKey, "ImportKey"
                            SubtleOperation.ExportKey, "ExportKey"
                            SubtleOperation.WrapKey, "WrapKey"
                            SubtleOperation.UnwrapKey, "UnwrapKey"
                        ] do
                        equal "operation case display" expected (operation.ToString ())

                    equal "present display" "Present" ((CryptoField.Present secret).ToString())
                    equal "missing display" "Missing" (CryptoField<string>.Missing.ToString())
                    let buffer = CryptoBuffer.whole [| 42uy |]
                    equal "buffer display" "CryptoBuffer" (buffer.ToString ())

                    let jwk =
                        { JsonWebKey.empty with
                            K = CryptoField.Present secret
                            D = CryptoField.Present secret
                        }

                    equal "JWK display" "JsonWebKey" (jwk.ToString ())

                    let prime: RsaOtherPrime =
                        {
                            R = CryptoField.Present secret
                            D = CryptoField.Present secret
                            T = CryptoField.Present secret
                        }

                    equal "prime display" "RsaOtherPrime" (prime.ToString ())

                    let parameters: HmacKeyParameters =
                        {
                            Name = "HMAC"
                            Hash = CryptoField.Present "SHA-256"
                            LengthBits = CryptoField.Missing
                        }

                    equal "parameter display" "HmacKeyParameters" (parameters.ToString ())
                    equal "algorithm display" "HMAC" ((CryptoAlgorithm.HmacKey parameters).ToString())
                    equal "raw import display" "Bytes" ((CryptoKeyData.Bytes buffer).ToString())
                    equal "JWK import display" "Jwk" ((CryptoKeyData.Jwk jwk).ToString())
                    equal "raw export display" "Bytes" ((CryptoExportedKey.Bytes buffer.Bytes).ToString())
                    equal "JWK export display" "Jwk" ((CryptoExportedKey.Jwk jwk).ToString())
                    use service = create ()

                    let! imported =
                        createImported service "SHA-256" [| 1uy; 2uy |] CryptoField.Missing true [| CryptoKeyUsage.Sign |]

                    use key = imported
                    equal "metadata display" "HMAC" (key.Algorithm.ToString ())
                    equal "generated key display" "Key" ((CryptoGeneratedKey.Key key).ToString())
                })

        do!
            group "aot/legacy-crypto-diagnostics" (fun () ->
                async {
                    let expect name detail action =
                        try
                            action ()
                            failwith "Expected legacy crypto error"
                        with CryptoException (actualName, actualDetail) as failure ->
                            equal "legacy error name" name actualName
                            equal "legacy error detail" detail actualDetail
                            equal "legacy Message" (name + ": " + detail) failure.Message

                    expect "QuotaExceededError" "The byte length 65537 exceeds 65536." (fun () ->
                        CryptoContract.randomValues RandomValuesKind.Uint8 (Array.zeroCreate 65537) 0 65537)

                    expect "TypeMismatchError" "The view is not an integer typed array." (fun () ->
                        CryptoContract.randomValues RandomValuesKind.Float32 (Array.zeroCreate 65537) 0 65537)

                    expect "RangeError" "The byte length is not a multiple of the element size." (fun () ->
                        CryptoContract.randomValues RandomValuesKind.Uint16 (Array.zeroCreate 3) 0 3)

                    match CryptoContract.unsupportedDigest "'quoted'" with
                    | CryptoOutcome.Unsupported reason ->
                        equal "legacy digest reason" "SubtleCrypto.digest supports SHA-1, SHA-256, SHA-384 and SHA-512, not ''quoted''." reason
                    | _ -> failwith "Expected unsupported digest"

                    match CryptoContract.availability SubtleOperation.Sign "'quoted'" with
                    | CryptoOutcome.Unsupported reason ->
                        equal "legacy availability reason" "SubtleCrypto.sign is not implemented for ''quoted''." reason
                    | _ -> failwith "Expected unsupported operation"
                })

        do!
            group "logical-import-and-masking" (fun () ->
                async {
                    use service = create ()

                    let! key =
                        createImported service "SHA-256" (Array.create 20 255uy) (CryptoField.Present 159UL) true [| CryptoKeyUsage.Sign |]

                    try
                        equal "159 bits" (Some 159UL) key.Algorithm.LengthBits
                        let! encoded = service.ExportKey (CryptoKeyFormat.Raw, key)

                        match value encoded with
                        | CryptoExportedKey.Bytes data -> equal "masked export" (Array.append (Array.create 19 255uy) [| 254uy |]) data
                        | _ -> failwith "Expected bytes"
                    finally
                        key.Dispose ()
                })

        do!
            group "conversion-and-crossed-error-order" (fun () ->
                async {
                    use service = create ()
                    let raw = CryptoKeyData.Bytes (CryptoBuffer.whole [| 1uy |])

                    for algorithm in
                        [
                            CryptoAlgorithm.Name "future"
                            keyParameters "future" CryptoField.Missing
                            keyParameters "SHA-256" (CryptoField.Present 0UL)
                        ] do
                        do! errorName "TypeError" (fun () -> service.GenerateKey (algorithm, true, [| enum<CryptoKeyUsage> 999 |]))

                        do!
                            errorName "TypeError" (fun () ->
                                service.ImportKey (CryptoKeyFormat.Raw, raw, algorithm, true, [| enum<CryptoKeyUsage> 999 |]))

                    do!
                        errorName "TypeError" (fun () ->
                            service.ImportKey (
                                CryptoKeyFormat.Raw,
                                raw,
                                keyParameters "future" (CryptoField.Present UInt64.MaxValue),
                                true,
                                [| CryptoKeyUsage.Sign |]
                            ))

                    do!
                        errorName "TypeError" (fun () ->
                            service.ImportKey (enum<CryptoKeyFormat> 999, raw, CryptoAlgorithm.Name "future", true, [| CryptoKeyUsage.Sign |]))

                    do!
                        errorName "TypeError" (fun () ->
                            service.ImportKey (CryptoKeyFormat.Raw, raw, CryptoAlgorithm.Name "HMAC", true, [| CryptoKeyUsage.Sign |]))

                    do!
                        errorName "DataError" (fun () ->
                            service.ImportKey (
                                CryptoKeyFormat.Raw,
                                raw,
                                keyParameters "SHA-256" (CryptoField.Present 0UL),
                                true,
                                [| CryptoKeyUsage.Encrypt |]
                            ))

                    do!
                        errorName "SyntaxError" (fun () ->
                            service.GenerateKey (keyParameters "SHA-256" (CryptoField.Present 0UL), true, [| CryptoKeyUsage.Encrypt |]))

                    do!
                        errorName "OperationError" (fun () ->
                            service.GenerateKey (keyParameters "SHA-256" (CryptoField.Present 0UL), true, [| CryptoKeyUsage.Sign |]))

                    do!
                        errorName "SyntaxError" (fun () ->
                            service.ImportKey (CryptoKeyFormat.Raw, raw, keyParameters "SHA-256" CryptoField.Missing, true, [||]))
                })

        do!
            group "jwk/extractability/usages" (fun () ->
                async {
                    use service = create ()

                    let jwk =
                        { JsonWebKey.empty with
                            Kty = CryptoField.Present "oct"
                            K = CryptoField.Present "AQI"
                            Alg = CryptoField.Present "HS256"
                            Ext = CryptoField.Present true
                            KeyOps = CryptoField.Present [| "sign" |]
                        }

                    let import data extractable =
                        service.ImportKey (
                            CryptoKeyFormat.Jwk,
                            CryptoKeyData.Jwk data,
                            keyParameters "SHA-256" CryptoField.Missing,
                            extractable,
                            [| CryptoKeyUsage.Sign |]
                        )

                    for wrong in
                        [
                            { jwk with
                                Kty = CryptoField.Present "OCT"
                            }
                            { jwk with
                                Alg = CryptoField.Present "hs256"
                            }
                            { jwk with
                                Use = CryptoField.Present "enc"
                            }
                            { jwk with
                                Ext = CryptoField.Present false
                            }
                            { jwk with
                                KeyOps = CryptoField.Present [| "sign"; "sign" |]
                            }
                            { jwk with
                                KeyOps = CryptoField.Present [| "verify" |]
                            }
                        ] do
                        do! errorName "DataError" (fun () -> import wrong true)

                    let unknown =
                        [| new String ([| char 0xD800 |]); new String ([| char 0xD801 |]); "sign" |]

                    let! imported =
                        import
                            { jwk with
                                KeyOps = CryptoField.Present unknown
                            }
                            false

                    let key = value imported

                    try
                        do! errorName "InvalidAccessError" (fun () -> service.ExportKey (CryptoKeyFormat.Raw, key))
                        do! errorName "NotSupportedError" (fun () -> service.ExportKey (CryptoKeyFormat.Spki, key))

                        do!
                            errorName "InvalidAccessError" (fun () ->
                                service.Verify (CryptoAlgorithm.Name "HMAC", key, CryptoBuffer.whole [||], CryptoBuffer.whole [||]))
                    finally
                        key.Dispose ()
                })

        return
            {
                assertions = assertions
                groups = groups
                failures = failures.ToArray ()
            }
    }
