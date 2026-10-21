module Ranvier.View.Twin.Tests.NativeTests

open Expecto
open Ranvier.View.Foundation

type private Row = { Id: int }

[<Tests>]
let tests =
    testList
        "Native"
        [
            testCase "a map keys strings, numbers and booleans by value"
            <| fun () ->
                let map = Native.newMap ()
                Native.set map "a" 1
                Native.set map 2 2
                Native.set map true 3
                Expect.equal (Native.getIndex map ("a" + "")) 1 "string key"
                Expect.equal (Native.getIndex map 2.0) 2 "an int and a float of equal value are one key"
                Expect.equal (Native.getIndex map true) 3 "boolean key"
                Expect.equal (Native.getIndex map "missing") -1 "absent key"

            testCase "SameValueZero keys erased Fable primitives by value"
            <| fun () ->
                let map = Native.newMap ()
                Native.set map (Some 7) 1
                Native.set map 'x' 2
                Native.set map 9L 3
                Expect.equal (Native.getIndex map 7) 1 "Some 7 is the key 7"
                Expect.equal (Native.getIndex map "x") 2 "a char is a one-character string"
                Expect.equal (Native.getIndex map 9L) 3 "an int64 keys by value"
                Expect.equal (Native.getIndex map nan) -1 "NaN is absent"
                Native.set map nan 4
                Expect.equal (Native.getIndex map nan) 4 "NaN equals NaN"
                Native.set map -0.0 5
                Expect.equal (Native.getIndex map 0.0) 5 "-0 equals +0"

            testCase "records key by reference"
            <| fun () ->
                let map = Native.newMap ()
                Native.set map { Id = 1 } 1
                Expect.equal (Native.getIndex map { Id = 1 }) -1 "a structurally equal record is another key"

            testCase "a set iterates in insertion order over a snapshot"
            <| fun () ->
                let set = Native.newSet ()

                for name in [ "click"; "input"; "focusin" ] do
                    Native.add set name

                let seen = ResizeArray<string>()

                Native.forEach set (fun (name: string) ->
                    seen.Add name
                    Native.add set (name + "!"))

                Expect.equal (List.ofSeq seen) [ "click"; "input"; "focusin" ] "insertion order; entries added during the walk are not visited"

            testCase "a weak map holds a reachable key until it is deleted"
            <| fun () ->
                let key = obj ()
                let table = Native.newWeakMap ()
                Native.set table key "v"
                Expect.isTrue (Native.has table key) "held"
                Expect.equal (Native.get<obj, string> table key) "v" "value"
                Native.delete table key
                Expect.isFalse (Native.has table key) "deleted"

            testCase "list reads past the end are null and writes at the end append"
            <| fun () ->
                let items = ResizeArray<string> [ "a" ]
                Expect.isNull (Native.listAt items 3) "past the end"
                Native.listSet items 1 "b"
                Expect.equal (List.ofSeq items) [ "a"; "b" ] "appended"
                Native.listSplice items 0 1 [| "x"; "y" |]
                Expect.equal (List.ofSeq items) [ "x"; "y"; "b" ] "spliced"
                Native.listTruncate items 1
                Expect.equal (List.ofSeq items) [ "x" ] "truncated"

            testCase "array slices clamp and reads past the end are null"
            <| fun () ->
                let rows = [| "a"; "b"; "c" |]
                Expect.equal (Native.arraySlice rows 1 10) [| "b"; "c" |] "clamped"
                Expect.isNull (Native.arrayAt rows 5) "past the end"
                Expect.equal (Native.allocate 3).Length 3 "allocated"

            testCase "expandos round trip per object"
            <| fun () ->
                let a, b = obj (), obj ()
                Native.setExpando a "$$ranvierWidget" true
                Expect.equal (Native.expando<bool> a "$$ranvierWidget") true "set"
                Expect.isNull (Native.expando<obj> b "$$ranvierWidget") "another object"
        ]
