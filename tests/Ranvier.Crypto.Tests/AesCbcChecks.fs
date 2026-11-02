module internal Ranvier.Crypto.Tests.AesCbcChecks

open System
open Ranvier.Dom
open Ranvier.Dom.Cryptography

let private bytes (text: string) =
    Array.init (text.Length / 2) (fun i -> Convert.ToByte (text.Substring (i * 2, 2), 16))

let private value =
    function
    | CryptoOutcome.Value x -> x
    | CryptoOutcome.Unsupported why -> failwith why

let private keyParameters bits =
    CryptoAlgorithm.AesKey
        {
            Name = "AES-CBC"
            LengthBits = CryptoField.Present bits
        }

let private parameters iv =
    CryptoAlgorithm.AesCbc
        {
            Name = "AES-CBC"
            Iv = CryptoField.Present (CryptoBuffer.whole iv)
        }

/// <summary>Runs AES-CBC primary vectors, key formats, normalization and ownership checks through the public provider.</summary>
let run (create: unit -> SubtleCryptoService) =
    async {
        let mutable assertions = 0
        let mutable groups = 0
        let failures = ResizeArray<string>()

        let equal label expected actual =
            assertions <- assertions + 1

            if expected <> actual then
                failwith (label + ": values differ")

        let group label work =
            async {
                groups <- groups + 1

                try
                    do! work ()
                with failure ->
                    failures.Add (label + ": " + failure.Message)
            }

        let errorName expected work =
            async {
                let! actual =
                    async {
                        try
                            let! _ = work ()
                            return "no error"
                        with CryptoException (name, _) ->
                            return name
                    }

                equal "error name" expected actual
            }

        let import (service: SubtleCryptoService) raw permitted =
            async {
                let! result =
                    service.ImportKey (
                        CryptoKeyFormat.Raw,
                        CryptoKeyData.Bytes (CryptoBuffer.whole raw),
                        CryptoAlgorithm.Name "AES-CBC",
                        true,
                        permitted
                    )

                return value result
            }

        let iv = bytes "000102030405060708090a0b0c0d0e0f"

        let plain =
            bytes (
                "6bc1bee22e409f96e93d7e117393172a"
                + "ae2d8a571e03ac9c9eb76fac45af8e51"
                + "30c81c46a35ce411e5fbc1191a0a52ef"
                + "f69f2445df4f9b17ad2b417be66c3710"
            )
        // NIST SP 800-38A F.2.1, F.2.3 and F.2.5 publish four CBC blocks without padding.
        let vectors =
            [|
                "128",
                "2b7e151628aed2a6abf7158809cf4f3c",
                "7649abac8119b246cee98e9b12e9197d5086cb9b507219ee95db113a917678b273bed6b8e3c1743b7116e69e222295163ff1caa1681fac09120eca307586e1a7"
                "192",
                "8e73b0f7da0e6452c810f32b809079e562f8ead2522c6b7b",
                "4f021db243bc633d7178183a9fa071e8b4d9ada9ad7dedf4e5e738763f69145a571b242012fb7ae07fa9baac3df102e008b0e27988598881d920a9e64f5615cd"
                "256",
                "603deb1015ca71be2b73aef0857d77811f352c073b6108d72d9810a30914dff4",
                "f58c4c04d6e5f1ba779eabfb5f7bfbd69cfc4e967edb808d679f777bc6702c7d39f23369a9d9bacfa530e26304231461b2eb05e2c39be9fcda6c19078c6a9d1b"
            |]

        for label, keyHex, expected in vectors do
            do!
                group ("CBC NIST " + label) (fun () ->
                    async {
                        use service = create ()
                        let raw = bytes keyHex

                        let! imported =
                            import service raw [| CryptoKeyUsage.Encrypt; CryptoKeyUsage.Decrypt |]

                        use key = imported
                        equal "name" "AES-CBC" key.Algorithm.Name
                        equal "length" (Some (uint64 raw.Length * 8UL)) key.Algorithm.LengthBits
                        equal "hash" None key.Algorithm.Hash
                        let! encrypted = service.Encrypt (parameters iv, key, CryptoBuffer.whole plain)
                        let cipher = value encrypted
                        equal "PKCS7 output length" 80 cipher.Length
                        equal "published blocks" (bytes expected) cipher[0..63]
                        let! decrypted = service.Decrypt (parameters iv, key, CryptoBuffer.whole cipher)
                        equal "decrypt" plain (value decrypted)
                    })

        for bits in [| 128UL; 192UL; 256UL |] do
            do!
                group ("CBC generation " + string bits) (fun () ->
                    async {
                        use service = create ()

                        let! result =
                            service.GenerateKey (keyParameters bits, true, [| CryptoKeyUsage.Encrypt |])

                        use key =
                            match value result with
                            | CryptoGeneratedKey.Key k -> k
                            | _ -> failwith "Expected secret key."

                        equal "generated name" "AES-CBC" key.Algorithm.Name
                        equal "generated length" (Some bits) key.Algorithm.LengthBits
                        let! exported = service.ExportKey (CryptoKeyFormat.Raw, key)

                        match value exported with
                        | CryptoExportedKey.Bytes raw -> equal "generated bytes" (int bits / 8) raw.Length
                        | _ -> failwith "Expected raw bytes."
                    })

        do!
            group "CBC key-before-IV error ordering" (fun () ->
                async {
                    use service = create ()
                    let! imported = import service (Array.zeroCreate 16) [| CryptoKeyUsage.Decrypt |]
                    use key = imported
                    do! errorName "InvalidAccessError" (fun () -> service.Encrypt (parameters [| 0uy |], key, CryptoBuffer.whole [||]))
                    do! errorName "TypeError" (fun () -> service.Encrypt (CryptoAlgorithm.Name "AES-CBC", key, CryptoBuffer.whole [||]))
                })

        for bits in [| 128UL; 192UL; 256UL |] do
            do!
                group ("CBC raw/JWK " + string bits) (fun () ->
                    async {
                        use service = create ()
                        let raw = Array.init (int bits / 8) byte

                        let! imported =
                            import service raw [| CryptoKeyUsage.Encrypt; CryptoKeyUsage.Decrypt |]

                        use key = imported
                        let! exported = service.ExportKey (CryptoKeyFormat.Jwk, key)

                        let jwk =
                            match value exported with
                            | CryptoExportedKey.Jwk j -> j
                            | _ -> failwith "Expected JWK."

                        equal "kty" (CryptoField.Present "oct") jwk.Kty
                        equal "alg" (CryptoField.Present ("A" + string bits + "CBC")) jwk.Alg
                        equal "ext" (CryptoField.Present true) jwk.Ext
                        equal "key_ops" (CryptoField.Present [| "decrypt"; "encrypt" |]) jwk.KeyOps

                        let! again =
                            service.ImportKey (
                                CryptoKeyFormat.Jwk,
                                CryptoKeyData.Jwk jwk,
                                CryptoAlgorithm.Name "aEs-CbC",
                                true,
                                [| CryptoKeyUsage.Encrypt |]
                            )

                        use again = value again
                        let! copied = service.ExportKey (CryptoKeyFormat.Raw, again)

                        match value copied with
                        | CryptoExportedKey.Bytes b -> equal "raw roundtrip" raw b
                        | _ -> failwith "Expected bytes."

                        for wrong in
                            [|
                                { jwk with
                                    Kty = CryptoField.Present "OCT"
                                }
                                { jwk with
                                    Alg = CryptoField.Present "A128GCM"
                                }
                                { jwk with
                                    Use = CryptoField.Present "sig"
                                }
                                { jwk with
                                    Use = CryptoField.Present ""
                                }
                                { jwk with
                                    Ext = CryptoField.Present false
                                }
                                { jwk with K = CryptoField.Missing }
                                { jwk with
                                    KeyOps = CryptoField.Present [| "encrypt"; "encrypt" |]
                                }
                                { jwk with
                                    KeyOps = CryptoField.Present [| "decrypt" |]
                                }
                            |] do
                            do!
                                errorName "DataError" (fun () ->
                                    service.ImportKey (
                                        CryptoKeyFormat.Jwk,
                                        CryptoKeyData.Jwk wrong,
                                        CryptoAlgorithm.Name "AES-CBC",
                                        true,
                                        [| CryptoKeyUsage.Encrypt |]
                                    ))

                        let usages = key.Usages
                        usages[0] <- CryptoKeyUsage.Sign
                        equal "independent usages" [| CryptoKeyUsage.Decrypt; CryptoKeyUsage.Encrypt |] key.Usages
                    })

        do!
            group "CBC padding and lengths" (fun () ->
                async {
                    use service = create ()

                    let! imported =
                        import service (bytes "2b7e151628aed2a6abf7158809cf4f3c") [| CryptoKeyUsage.Encrypt; CryptoKeyUsage.Decrypt |]

                    use key = imported

                    for count in [| 0; 1; 14; 15; 16; 17; 31; 32; 63 |] do
                        let input = Array.init count byte
                        let! encrypted = service.Encrypt (parameters iv, key, CryptoBuffer.whole input)
                        let cipher = value encrypted
                        equal "padded length" ((count / 16 + 1) * 16) cipher.Length
                        let! decrypted = service.Decrypt (parameters iv, key, CryptoBuffer.whole cipher)
                        equal "padding roundtrip" input (value decrypted)

                    let c1 = bytes "7649abac8119b246cee98e9b12e9197d"

                    for padding in [| 0uy; 17uy |] do
                        let changed = Array.copy iv
                        changed[15] <- iv[15] ^^^ 0x2auy ^^^ padding
                        do! errorName "OperationError" (fun () -> service.Decrypt (parameters changed, key, CryptoBuffer.whole c1))

                    let wrong = Array.copy iv
                    wrong[15] <- iv[15] ^^^ 0x2auy ^^^ 2uy
                    do! errorName "OperationError" (fun () -> service.Decrypt (parameters wrong, key, CryptoBuffer.whole c1))

                    for padding in [| 1; 2 |] do
                        let changed = Array.copy iv
                        changed[15] <- iv[15] ^^^ 0x2auy ^^^ byte padding

                        if padding = 2 then
                            changed[14] <- iv[14] ^^^ 0x17uy ^^^ 2uy

                        let! decrypted = service.Decrypt (parameters changed, key, CryptoBuffer.whole c1)
                        equal "valid explicit padding" plain[0 .. 15 - padding] (value decrypted)

                    for length in [| 0; 1; 15; 17 |] do
                        do! errorName "OperationError" (fun () -> service.Decrypt (parameters iv, key, CryptoBuffer.whole (Array.zeroCreate length)))

                    for length in [| 0; 15; 17 |] do
                        do!
                            errorName "OperationError" (fun () ->
                                service.Encrypt (parameters (Array.zeroCreate length), key, CryptoBuffer.whole [||]))
                })

        do!
            group "CBC dictionary conversion and semantic ordering" (fun () ->
                async {
                    use service = create ()

                    for bits in [| 0UL; 1UL; 127UL; 129UL; 65535UL |] do
                        do! errorName "OperationError" (fun () -> service.GenerateKey (keyParameters bits, true, [| CryptoKeyUsage.Encrypt |]))
                        do! errorName "SyntaxError" (fun () -> service.GenerateKey (keyParameters bits, true, [| CryptoKeyUsage.Sign |]))

                    do! errorName "TypeError" (fun () -> service.GenerateKey (keyParameters 65536UL, true, [| CryptoKeyUsage.Sign |]))
                    do! errorName "TypeError" (fun () -> service.GenerateKey (CryptoAlgorithm.Name "AES-CBC", true, [| CryptoKeyUsage.Encrypt |]))
                    do! errorName "TypeError" (fun () -> service.GenerateKey (CryptoAlgorithm.Name "future", true, [| enum<CryptoKeyUsage> 99 |]))
                    do! errorName "SyntaxError" (fun () -> service.GenerateKey (keyParameters 128UL, true, [||]))

                    for length in [| 0; 1; 15; 17; 31; 33 |] do
                        do!
                            errorName "DataError" (fun () ->
                                service.ImportKey (
                                    CryptoKeyFormat.Raw,
                                    CryptoKeyData.Bytes (CryptoBuffer.whole (Array.zeroCreate length)),
                                    CryptoAlgorithm.Name "AES-CBC",
                                    true,
                                    [| CryptoKeyUsage.Encrypt |]
                                ))

                    do!
                        errorName "SyntaxError" (fun () ->
                            service.ImportKey (
                                CryptoKeyFormat.Spki,
                                CryptoKeyData.Bytes (CryptoBuffer.whole [||]),
                                CryptoAlgorithm.Name "AES-CBC",
                                true,
                                [| CryptoKeyUsage.Sign |]
                            ))

                    do!
                        errorName "NotSupportedError" (fun () ->
                            service.ImportKey (
                                CryptoKeyFormat.Spki,
                                CryptoKeyData.Bytes (CryptoBuffer.whole [||]),
                                CryptoAlgorithm.Name "AES-CBC",
                                true,
                                [| CryptoKeyUsage.Encrypt |]
                            ))

                    let relevant =
                        CryptoAlgorithm.AesCtr
                            {
                                Name = "AES-CBC"
                                LengthBits = CryptoField.Present 128UL
                                Counter = CryptoField.Present Unchecked.defaultof<CryptoBuffer>
                            }

                    let! generated = service.GenerateKey (relevant, true, [| CryptoKeyUsage.Encrypt |])

                    use key =
                        match value generated with
                        | CryptoGeneratedKey.Key key -> key
                        | _ -> failwith "Expected key."

                    let input = [| 1uy; 2uy |]

                    let shaped =
                        CryptoAlgorithm.AesGcm
                            {
                                Name = "AES-CBC"
                                Iv = CryptoField.Present (CryptoBuffer.whole iv)
                                AdditionalData = CryptoField.Present Unchecked.defaultof<CryptoBuffer>
                                TagLengthBits = CryptoField.Present UInt64.MaxValue
                            }

                    let! direct = service.Encrypt (parameters iv, key, CryptoBuffer.whole input)
                    let! extra = service.Encrypt (shaped, key, CryptoBuffer.whole input)
                    equal "ignored irrelevant members" (value direct) (value extra)
                })

        do!
            group "CBC independent services and captured buffers" (fun () ->
                async {
                    let source = create ()

                    let! imported =
                        import source (Array.zeroCreate 16) [| CryptoKeyUsage.Encrypt; CryptoKeyUsage.Decrypt |]

                    source.Dispose ()
                    use key = imported
                    use service = create ()
                    let sourceIv = Array.copy iv
                    let sourceBytes = [| 99uy; 1uy; 2uy; 3uy; 99uy |]

                    let operation =
                        service.Encrypt (
                            parameters sourceIv,
                            key,
                            {
                                Bytes = sourceBytes
                                Offset = 1
                                Count = 3
                            }
                        )

                    Array.fill sourceBytes 0 sourceBytes.Length 0uy
                    Array.fill sourceIv 0 sourceIv.Length 0uy
                    let! first = operation
                    let! second = operation
                    equal "same admitted result" (value first) (value second)

                    let! decrypted =
                        service.Decrypt (parameters iv, key, CryptoBuffer.whole (value first))

                    equal "captured bytes and iv" [| 1uy; 2uy; 3uy |] (value decrypted)
                    let admitted = service.Encrypt (parameters iv, key, CryptoBuffer.whole [| 4uy |])
                    key.Dispose ()
                    let! retained = admitted
                    equal "retained key" 16 (value retained).Length
                    do! errorName "InvalidAccessError" (fun () -> service.Encrypt (parameters iv, key, CryptoBuffer.whole [||]))
                })

        do!
            group "CBC/HMAC algorithm isolation and extractability" (fun () ->
                async {
                    use service = create ()

                    let! hmac =
                        service.ImportKey (
                            CryptoKeyFormat.Raw,
                            CryptoKeyData.Bytes (CryptoBuffer.whole (Array.zeroCreate 16)),
                            CryptoAlgorithm.HmacKey
                                {
                                    Name = "HMAC"
                                    Hash = CryptoField.Present "SHA-256"
                                    LengthBits = CryptoField.Missing
                                },
                            true,
                            [| CryptoKeyUsage.Sign |]
                        )

                    use hmac = value hmac
                    do! errorName "InvalidAccessError" (fun () -> service.Encrypt (parameters iv, hmac, CryptoBuffer.whole [||]))

                    let! cbc =
                        service.GenerateKey (keyParameters 128UL, false, [| CryptoKeyUsage.Encrypt |])

                    use cbc =
                        match value cbc with
                        | CryptoGeneratedKey.Key key -> key
                        | _ -> failwith "Expected key."

                    do! errorName "InvalidAccessError" (fun () -> service.Sign (CryptoAlgorithm.Name "HMAC", cbc, CryptoBuffer.whole [||]))
                    do! errorName "InvalidAccessError" (fun () -> service.ExportKey (CryptoKeyFormat.Raw, cbc))
                    do! errorName "NotSupportedError" (fun () -> service.ExportKey (CryptoKeyFormat.Spki, cbc))
                })

        return
            ({
                assertions = assertions
                groups = groups
                failures = failures.ToArray ()
            }
            : HmacChecks.Report)
    }
