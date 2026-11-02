module internal Ranvier.Crypto.Tests.HmacChecks

open System
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
