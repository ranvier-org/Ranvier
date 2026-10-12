module AuthoringPerfMain

open System
open System.Diagnostics
open System.Text.Json
open global.Ranvier

let private sample mode width count =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")

    let disabled =
        graph.Run (fun () -> Api.createMemo (fun _ -> source.Value = "blocked"))

    let startBytes = GC.GetAllocatedBytesForCurrentThread ()
    let started = Stopwatch.GetTimestamp ()

    let definitions =
        Array.init count (fun _ -> AuthoringPerfFixtures.create mode width source disabled ignore)

    let elapsed = Stopwatch.GetElapsedTime started

    let bytes =
        GC.GetAllocatedBytesForCurrentThread ()
        - startBytes

    GC.KeepAlive definitions
    elapsed.TotalMilliseconds / float count, float bytes / float count

[<EntryPoint>]
let main _ =
    let rows = ResizeArray<_>()

    for width in [ 1; 16 ] do
        let iterations = if width = 1 then 5000 else 1000

        for round in -3 .. 10 do
            for offset in 0..1 do
                let mode =
                    if (round + offset + 4) % 2 = 0 then
                        "explicit"
                    else
                        "overloaded"

                let ms, bytes = sample mode width iterations

                if round >= 0 then
                    rows.Add
                        {|
                            width = width
                            round = round
                            mode = mode
                            iterations = iterations
                            ms = ms
                            bytes = bytes
                        |}

    printfn
        "%s"
        (JsonSerializer.Serialize
            {|
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
                samples = rows.ToArray ()
            |})

    0
