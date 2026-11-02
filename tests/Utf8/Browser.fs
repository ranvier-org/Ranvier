module Utf8BrowserTestHost

open Fable.Core

[<Emit("globalThis.__utf8Proof = { checks: $0 }")>]
let private publish (checks: int) : unit = jsNative

let private checks = Utf8Tests.run ()
do publish checks
