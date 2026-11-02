module Utf8NativeTestHost

[<EntryPoint>]
let main _ =
    let checks = Utf8Tests.run ()
    printfn "UTF8_SHARED PASS checks=%d" checks
    0
