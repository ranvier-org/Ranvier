module Ranvier.Crypto.Tests.BrowserMain

open Fable.Core
open Ranvier.Dom
open Ranvier.Dom.Cryptography

[<Emit("document")>]
let private document: Document = jsNative

[<Emit("fetch('fixtures/hmac-provider-rows.json').then(r=>r.json())")>]
let private vectors () : JS.Promise<HmacChecks.Vector array> = jsNative

[<Emit("globalThis.crypto289Report=$0")>]
let private finish (report: HmacChecks.Report) : unit = jsNative

let private run () =
    async {
        try
            use service = BrowserSubtleCrypto.forDocument document

            match service.Availability (SubtleOperation.Sign, "HMAC") with
            | CryptoOutcome.Unsupported _ ->
                let! generated =
                    service.GenerateKey (
                        CryptoAlgorithm.HmacKey
                            {
                                Name = "HMAC"
                                Hash = CryptoField.Present "SHA-256"
                                LengthBits = CryptoField.Missing
                            },
                        true,
                        [| CryptoKeyUsage.Sign |]
                    )

                match generated with
                | CryptoOutcome.Unsupported _ ->
                    finish
                        {
                            assertions = 2
                            groups = 1
                            failures = [||]
                        }
                | _ ->
                    finish
                        {
                            assertions = 2
                            groups = 1
                            failures = [| "insecure context unexpectedly generated a key" |]
                        }
            | _ ->
                let! rows = Async.AwaitPromise (vectors ())

                let! report =
                    HmacChecks.run (fun () -> BrowserSubtleCrypto.forDocument document) rows

                finish report
        with error ->
            finish
                {
                    assertions = 0
                    groups = 0
                    failures = [| error.Message |]
                }
    }

Async.StartImmediate (run ())
