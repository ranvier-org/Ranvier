module Ranvier.Docs.Maps.Tests.PageTests

open Expecto
open Ranvier.View
open Ranvier.Docs.Maps

let private omit =
    { Server.Options.defaults with
        Policy = Omit
    }

[<Tests>]
let tests =
    testList
        "Pages"
        [
            test "the pipeline fixture renders deterministically on .NET" {
                let first = Server.render omit PipelineFixture.counter ()
                let second = Server.render omit PipelineFixture.counter ()

                Expect.equal
                    first
                    """<!--[r--><div class="rv-map-fixture"><button type="button" class="rv-map-fixture__count">0</button></div><!--]-->"""
                    "server HTML"

                Expect.equal second first "a second render"
            }
        ]
