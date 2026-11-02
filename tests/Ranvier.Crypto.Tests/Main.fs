module Ranvier.Crypto.Tests.Main

open System
open System.IO
open System.Runtime.CompilerServices
open System.Text.Json

/// <summary>Runs the full HMAC checks and returns nonzero for a failure or unmet NativeAOT expectation.</summary>
[<EntryPoint>]
let main args =
    let path =
        Path.Combine (AppContext.BaseDirectory, "fixtures", "hmac-provider-rows.json")

    use document = JsonDocument.Parse (File.ReadAllText path)

    let vectors =
        document.RootElement.EnumerateArray ()
        |> Seq.map (fun item ->
            let vector: HmacChecks.Vector =
                {
                    id = item.GetProperty("id").GetString()
                    hash = item.GetProperty("hash").GetString()
                    key = item.GetProperty("key").GetString()
                    data = item.GetProperty("data").GetString()
                    mac = item.GetProperty("mac").GetString()
                    prefix = item.GetProperty("prefix").GetBoolean()
                }

            vector)
        |> Seq.toArray

    let report =
        HmacChecks.run Ranvier.Crypto.ManagedCrypto.create vectors
        |> Async.RunSynchronously

    Console.WriteLine (
        "HMAC289 assertions="
        + string report.assertions
        + " groups="
        + string report.groups
        + " failures="
        + string report.failures.Length
        + " dynamicCode="
        + RuntimeFeature.IsDynamicCodeSupported.ToString().ToLowerInvariant()
    )

    report.failures
    |> Array.iter (fun failure -> Console.Error.WriteLine ("FAIL " + failure))

    if
        Array.contains "--expect-aot" args
        && RuntimeFeature.IsDynamicCodeSupported
    then
        2
    elif report.failures.Length > 0 then
        1
    else
        0
