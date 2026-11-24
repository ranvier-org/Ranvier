module Ranvier.Wire.Tests.WireDecoding

open System
open Expecto
open Ranvier.Wire.Thoth.Json.Core
open Ranvier.Wire
open Ranvier.Wire.Tests.WireSupport

let private people = WireCodec.ofCodec person
let private anything = WireCodec.ofCodec Codec.value
let private ints = WireCodec.ofCodec (Codec.list Codec.int)

let private nested (depth: int) =
    String.replicate depth "["
    + String.replicate depth "]"

let private zeros (count: int) =
    "["
    + String.concat "," (List.replicate count "0")
    + "]"

let private strings = WireCodec.ofCodec Codec.string
let private counts = WireCodec.ofCodec (Codec.dict Codec.int)

let private within (depth: int) (items: int) =
    WireCodec.ofCodecWith
        { WireLimits.Default with
            MaxDepth = depth
            MaxItems = items
        }
        Codec.value

let private loneSurrogateMessage =
    "A string in the wire text holds a lone surrogate."

let private unmatchedMessage = "The wire text closes more brackets than it opens."

let private deeperThan (depth: int) =
    "The wire text nests deeper than "
    + string depth
    + " levels."

let private moreThan (items: int) =
    "The wire text holds more than "
    + string items
    + " items."

/// <summary>Fails unless the result is a <c>WireError.Limit</c> with the message.</summary>
let private expectLimit (name: string) (message: string) (result: Result<'T, WireError>) =
    let error = result |> expectError name
    Expect.isTrue (isLimit error) (name + ": kind")
    Expect.equal error.Message message (name + ": message")

/// <summary>Fails unless the result is a <c>WireError.Syntax</c> with the message.</summary>
let private expectSyntax (name: string) (message: string) (result: Result<'T, WireError>) =
    let error = result |> expectError name
    Expect.isTrue (isSyntax error) (name + ": kind")
    Expect.equal error.Message message (name + ": message")

let private raisesLoneSurrogate (name: string) (index: int) (encode: unit -> string) =
    Expect.throwsC (encode >> ignore) (fun ex -> Expect.stringContains ex.Message ("lone surrogate at index " + string index) name)

let private raisesNaming (field: string) (create: unit -> WireCodec<Json>) =
    Expect.throwsC (create >> ignore) (fun ex -> Expect.stringContains ex.Message field field)

/// <summary>A 60-deep array holding 90,001 ints, within the default limits.</summary>
let private deepInts =
    String.replicate 60 "["
    + String.concat "," (List.replicate 90001 "7")
    + String.replicate 60 "]"

let private throwing: Codec<int> =
    Codec.create
        Encode.int
        (Decode.int
         |> Decode.map (fun (_: int) -> (failwith "boom": int)))

/// <summary>A wire codec that decodes with <c>decoder</c>.</summary>
let private reading (decoder: Decoder<'T>) : WireCodec<'T> =
    WireCodec.ofCodec (Codec.create (fun (_: 'T) -> Encode.int 0) decoder)

/// <summary>The int members <c>f0</c> to <c>f11</c> of one object, read in that order.</summary>
let private twelve: Decoder<int list> =
    List.foldBack
        (fun (index: int) (rest: Decoder<int list>) ->
            Decode.map2 (fun (item: int) (items: int list) -> item :: items) (Decode.field ("f" + string index) Decode.int) rest)
        [ 0..11 ]
        (Decode.succeed [])

/// <summary>The members <c>f0</c> to <c>f11</c>, each holding its index, written in a scattered order.</summary>
let private twelveMembers =
    [ 7; 3; 11; 0; 9; 5; 1; 10; 2; 8; 4; 6 ]
    |> List.map (fun index ->
        quote
        + "f"
        + string index
        + quote
        + ":"
        + string index)
    |> String.concat ","

[<Tests>]
let tests =
    testList
        "WireCodec"
        [
            testCase "a record encodes to its text and decodes back"
            <| fun () ->
                let alice =
                    {
                        Name = "Alice"
                        Age = 30
                        Email = Some "alice@example.com"
                    }

                let text = people.Encode alice
                Expect.equal text """{"name":"Alice","age":30,"email":"alice@example.com"}""" "wire text"
                Expect.equal (people.Decode text |> expectOk "decode") alice "decoded"

            testCase "text that is not JSON is a syntax error"
            <| fun () ->
                for text in [ ""; "{"; "1 2"; "[1,]"; u 0xFEFF + "{}"; "{'a':1}" ] do
                    Expect.isTrue (people.Decode text |> expectError text |> isSyntax) text

            testCase "a null text is a syntax error"
            <| fun () ->
                expectSyntax "a string" "The wire text is null." (strings.Decode null)
                expectSyntax "a record" "The wire text is null." (people.Decode null)

            testCase "a null string encodes as JSON null"
            <| fun () -> Expect.equal (strings.Encode null) "null" "wire text"

            testCase "JSON of the wrong shape is a shape error"
            <| fun () ->
                for text in [ "null"; "[]"; """{"name":1,"age":2}"""; """{"name":"a"}""" ] do
                    Expect.isTrue (people.Decode text |> expectError text |> isShape) text

            testCase "an exception raised by a decoder is a shape error"
            <| fun () ->
                let error =
                    (WireCodec.ofCodec throwing).Decode "1"
                    |> expectError "throwing decoder"

                Expect.isTrue (isShape error) "kind"
                Expect.stringContains error.Message "boom" "message"

            testCase "nesting at the depth limit decodes and one level deeper is a limit error"
            <| fun () ->
                anything.Decode (nested 64)
                |> expectOk "64 levels"
                |> ignore

                anything.Decode (nested 65)
                |> expectLimit "65 levels" (deeperThan 64)

            testCase "a million unclosed brackets is a limit error"
            <| fun () ->
                anything.Decode (String.replicate 1000000 "[")
                |> expectLimit "unclosed" (deeperThan 64)

            testCase "a closing bracket without its opening one is a syntax error"
            <| fun () ->
                for text in [ "]"; "[]]"; "}{"; "[1]]"; """{"a":1}}""" ] do
                    expectSyntax text unmatchedMessage (anything.Decode text)

            testCase "brackets and escaped quotes inside strings do not count as nesting"
            <| fun () ->
                let text =
                    "[\""
                    + String.replicate 100 "[{"
                    + "\\\""
                    + String.replicate 100 "]}"
                    + "\"]"

                anything.Decode text
                |> expectOk "brackets in a string"
                |> ignore

            testCase "commas inside strings do not count as items"
            <| fun () ->
                (within 64 1).Decode "[\"a,b,c\"]"
                |> expectOk "one string"
                |> ignore

            testCase "a string ending in an escaped backslash closes before the brackets after it"
            <| fun () ->
                let text = "[" + quote + "\\\\" + quote + ",[[1]]]"

                (within 3 100).Decode text
                |> expectOk "three levels"
                |> ignore

                (within 2 100).Decode text
                |> expectLimit "two levels" (deeperThan 2)

            testCase "a list at the item limit decodes and one more item is a limit error"
            <| fun () ->
                Expect.equal
                    (ints.Decode (zeros 100000)
                     |> expectOk "100000 items"
                     |> List.length)
                    100000
                    "length"

                ints.Decode (zeros 100001)
                |> expectLimit "100001 items" (moreThan 100000)

            testCase "object members count as items"
            <| fun () ->
                let members =
                    "{"
                    + String.concat "," [ for i in 0..100000 -> "\"k" + string i + "\":0" ]
                    + "}"

                anything.Decode members
                |> expectLimit "100001 members" (moreThan 100000)

            testCase "an empty container is one item of its parent"
            <| fun () ->
                let three = within 64 3

                three.Decode "[[],{},[]]"
                |> expectOk "three empty containers"
                |> ignore

                three.Decode "[[],{},[],[]]"
                |> expectLimit "four" (moreThan 3)

            testCase "whitespace inside an empty container is no item"
            <| fun () ->
                (within 64 3).Decode "[[ ],[\r\n],{ }]"
                |> expectOk "three empty containers with whitespace"
                |> ignore

                (within 64 1).Decode "[{\t}]"
                |> expectOk "one empty object with a tab"
                |> ignore

            testCase "custom limits apply"
            <| fun () ->
                let shallow = within 2 100

                shallow.Decode "[[1]]"
                |> expectOk "two levels"
                |> ignore

                shallow.Decode "[[[1]]]"
                |> expectLimit "three levels" (deeperThan 2)

            testCase "a lone surrogate in a string, char or object key does not encode"
            <| fun () ->
                raisesLoneSurrogate "a high surrogate" 1 (fun () -> strings.Encode ("a" + u 0xD800 + "b"))
                raisesLoneSurrogate "a low surrogate" 1 (fun () -> strings.Encode ("a" + u 0xDC00))
                raisesLoneSurrogate "a high surrogate at the end" 1 (fun () -> strings.Encode ("a" + u 0xD83C))
                raisesLoneSurrogate "a char" 0 (fun () -> (WireCodec.ofCodec Codec.char).Encode(char 0xD800))
                raisesLoneSurrogate "an object key" 1 (fun () -> counts.Encode (Map.ofList [ "k" + u 0xDFFF, 1 ]))
                raisesLoneSurrogate "a list item" 0 (fun () -> (WireCodec.ofCodec (Codec.list Codec.string)).Encode [ "ok"; u 0xD800 ])

            testCase "a lone surrogate in the text is a syntax error, raw or escaped"
            <| fun () ->
                for name, text in
                    [
                        "a raw high surrogate", quote + "a" + u 0xD800 + "b" + quote
                        "a raw low surrogate", quote + u 0xDC00 + quote
                        "an escaped high surrogate", quote + "a" + esc "d800" + "b" + quote
                        "an escaped low surrogate", quote + esc "DC00" + quote
                        "an escaped high surrogate before a letter", quote + esc "D83C" + "a" + quote
                        "an escaped high surrogate at the end", quote + esc "D83C" + quote
                        "an escaped high surrogate before another escape", quote + esc "D83C" + "\\n" + quote
                        "an escaped high surrogate before a raw low one", quote + esc "D83C" + u 0xDF89 + quote
                        "a raw high surrogate before an escaped low one", quote + u 0xD83C + esc "DF89" + quote
                    ] do
                    expectSyntax name loneSurrogateMessage (strings.Decode text)

                let ignored =
                    "{\"name\":\"a\",\"age\":1,\"note\":\""
                    + esc "d800"
                    + "\"}"

                expectSyntax "an ignored field" loneSurrogateMessage (people.Decode ignored)

                let key = "{" + quote + esc "d800" + quote + ":1}"

                expectSyntax "an object key" loneSurrogateMessage (counts.Decode key)

            testCase "a surrogate pair decodes, raw or escaped"
            <| fun () ->
                let party = u 0xD83C + u 0xDF89

                Expect.equal
                    (strings.Decode (quote + party + quote)
                     |> expectOk "raw")
                    party
                    "raw"

                Expect.equal
                    (strings.Decode (quote + esc "D83C" + esc "DF89" + quote)
                     |> expectOk "escaped")
                    party
                    "escaped"

                let text = quote + "\\\\" + "ud800" + quote

                Expect.equal
                    (strings.Decode text
                     |> expectOk "an escaped backslash")
                    ("\\" + "ud800")
                    "an escaped backslash"

            testCase "limits at the edges of their range are accepted"
            <| fun () ->
                (within 1 100).Decode "[1]"
                |> expectOk "MaxDepth 1, one level"
                |> ignore

                (within 1 100).Decode "[[1]]"
                |> expectLimit "MaxDepth 1, two levels" (deeperThan 1)

                (within 1000 100000).Decode(nested 1000)
                |> expectOk "MaxDepth 1000, 1000 levels"
                |> ignore

                (within 1000 100000).Decode(nested 1001)
                |> expectLimit "MaxDepth 1000, 1001 levels" (deeperThan 1000)

                (within 64 1).Decode "[1]"
                |> expectOk "MaxItems 1, one item"
                |> ignore

                (within 64 1).Decode "[1,2]"
                |> expectLimit "MaxItems 1, two items" (moreThan 1)

            testCase "limits outside their range are rejected"
            <| fun () ->
                raisesNaming "MaxDepth" (fun () -> within 0 100)
                raisesNaming "MaxDepth" (fun () -> within 1001 100)
                raisesNaming "MaxItems" (fun () -> within 64 0)

            testCase "an object of 100,000 members decodes through Codec.dict in under three seconds"
            <| fun () ->
                let text =
                    "{"
                    + String.concat "," (List.init 100000 (fun i -> quote + "k" + string i + quote + ":" + string i))
                    + "}"

                let started = DateTime.UtcNow
                let decoded = counts.Decode text |> expectOk "100,000 members"
                let elapsed = (DateTime.UtcNow - started).TotalMilliseconds
                Expect.equal decoded.Count 100000 "member count"
                Expect.equal decoded["k99999"] 99999 "last member"
                Expect.isLessThan elapsed 3000.0 "elapsed milliseconds"

            testCase "a shape error echoes at most 256 characters of a large value"
            <| fun () ->
                let error =
                    people.Decode deepInts
                    |> expectError "a deep array"

                Expect.isTrue (isShape error) "kind"
                Expect.isLessThan error.Message.Length 2000 "message length"
                Expect.stringContains error.Message "…" "truncation mark"

            testCase "a field reads the last value of a repeated key, written plainly or escaped"
            <| fun () ->
                let text =
                    """{"name":"A","age":1,"name":"B","""
                    + quote
                    + esc "0061"
                    + "ge"
                    + quote
                    + ":2}"

                Expect.equal (people.Decode text |> expectOk "repeated keys") { Name = "B"; Age = 2; Email = None } "last values"

            testCase "a field name matches by ordinal case"
            <| fun () ->
                let decoded =
                    people.Decode """{"Name":"A","name":"B","AGE":7,"age":1,"EMAIL":"x"}"""
                    |> expectOk "mixed case"

                Expect.equal decoded { Name = "B"; Age = 1; Email = None } "exact names only"

                let error =
                    people.Decode """{"Name":"A","age":1}"""
                    |> expectError "a name in another case"

                Expect.isTrue (isShape error) "kind"

            testCase "twelve fields of one object read whatever the member order, with repeated keys"
            <| fun () ->
                let text = """{"f3":30,"f9":90,""" + twelveMembers + "}"

                Expect.equal
                    ((reading twelve).Decode text
                     |> expectOk "twelve fields")
                    [ 0..11 ]
                    "each field's last value"

                let error =
                    (reading twelve).Decode """{"f0":0,"f1":1,"f2":2,"f3":3,"f4":4,"f5":5,"f6":6,"f7":7,"f8":8,"f9":9,"f10":10}"""
                    |> expectError "a missing twelfth field"

                Expect.isTrue (isShape error) "kind"
                Expect.stringContains error.Message "f11" "the missing field is named"

            testCase "fields read before and after an object's keys see the same members"
            <| fun () ->
                let decoder =
                    Decode.map3
                        (fun (before: int) (pairs: (string * int) list) (after: int) -> before, pairs, after)
                        (Decode.field "b" Decode.int)
                        (Decode.keyValuePairs Decode.int)
                        (Decode.field "10" Decode.int)

                let decoded =
                    (reading decoder).Decode """{"b":1,"2":2,"a":3,"b":4,"10":5}"""
                    |> expectOk "fields around keys"

                Expect.equal decoded (4, [ "2", 2; "10", 5; "b", 4; "a", 3 ], 5) "own-property order, first position, last value"

            testCase "keys read after twelve fields keep own-property order"
            <| fun () ->
                let decoder =
                    Decode.map2 (fun (fields: int list) (keys: string list) -> fields, keys) twelve Decode.keys

                let text =
                    """{"z":0,"5":5,"""
                    + twelveMembers
                    + ""","1":1,"z":9}"""

                let fields, keys =
                    (reading decoder).Decode text
                    |> expectOk "fields then keys"

                Expect.equal fields [ 0..11 ] "fields"

                Expect.equal
                    keys
                    [
                        "1"
                        "5"
                        "z"
                        "f7"
                        "f3"
                        "f11"
                        "f0"
                        "f9"
                        "f5"
                        "f1"
                        "f10"
                        "f2"
                        "f8"
                        "f4"
                        "f6"
                    ]
                    "array indexes ascending, then the other keys at their first position"

            testCase "fields of nested and sibling objects read in any order"
            <| fun () ->
                let decoder =
                    Decode.map2
                        (fun (first: int * int * int) (second: int * int * int) -> first, second)
                        (Decode.map3
                            (fun (a: int) (b: int) (c: int) -> a, b, c)
                            (Decode.at [ "a"; "x" ] Decode.int)
                            (Decode.at [ "b"; "x" ] Decode.int)
                            (Decode.at [ "a"; "y"; "x" ] Decode.int))
                        (Decode.map3
                            (fun (a: int) (b: int) (c: int) -> a, b, c)
                            (Decode.field "x" Decode.int)
                            (Decode.at [ "a"; "x" ] Decode.int)
                            (Decode.at [ "b"; "x" ] Decode.int))

                let decoded =
                    (reading decoder).Decode """{"a":{"x":1,"y":{"x":2}},"b":{"x":3},"x":4}"""
                    |> expectOk "nested objects"

                Expect.equal decoded ((1, 3, 2), (4, 1, 3)) "each object keeps its own members"
#if !FABLE_COMPILER
            // .NET only: GC.GetAllocatedBytesForCurrentThread has no Fable counterpart.
            testCase "a shape error on a large value allocates less than 16 MB"
            <| fun () ->
                let before = GC.GetAllocatedBytesForCurrentThread ()

                people.Decode deepInts
                |> expectError "a deep array"
                |> ignore

                Expect.isLessThan (GC.GetAllocatedBytesForCurrentThread () - before) (16L * 1024L * 1024L) "allocated bytes"
#endif
        ]
