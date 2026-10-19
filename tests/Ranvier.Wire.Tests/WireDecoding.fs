module Ranvier.Wire.Tests.WireDecoding

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

let private raisesLoneSurrogate (name: string) (encode: unit -> string) =
    Expect.throwsC (encode >> ignore) (fun ex -> Expect.stringContains ex.Message "lone surrogate" name)

let private throwing: Codec<int> =
    Codec.create
        Encode.int
        (Decode.int
         |> Decode.map (fun (_: int) -> (failwith "boom": int)))

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

                Expect.isTrue
                    (anything.Decode (nested 65)
                     |> expectError "65 levels"
                     |> isLimit)
                    "65 levels"

            testCase "a million unclosed brackets is a limit error"
            <| fun () ->
                let error =
                    anything.Decode (String.replicate 1000000 "[")
                    |> expectError "unclosed"

                Expect.isTrue (isLimit error) "kind"

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

            testCase "a list at the item limit decodes and one more item is a limit error"
            <| fun () ->
                Expect.equal
                    (ints.Decode (zeros 100000)
                     |> expectOk "100000 items"
                     |> List.length)
                    100000
                    "length"

                Expect.isTrue
                    (ints.Decode (zeros 100001)
                     |> expectError "100001 items"
                     |> isLimit)
                    "kind"

            testCase "object members count as items"
            <| fun () ->
                let members =
                    "{"
                    + String.concat "," [ for i in 0..100000 -> "\"k" + string i + "\":0" ]
                    + "}"

                Expect.isTrue
                    (anything.Decode members
                     |> expectError "100001 members"
                     |> isLimit)
                    "kind"

            testCase "an empty container is one item of its parent"
            <| fun () ->
                let three =
                    WireCodec.ofCodecWith { WireLimits.Default with MaxItems = 3 } Codec.value

                three.Decode "[[],{},[]]"
                |> expectOk "three empty containers"
                |> ignore

                Expect.isTrue
                    (three.Decode "[[],{},[],[]]"
                     |> expectError "four"
                     |> isLimit)
                    "kind"

            testCase "custom limits apply"
            <| fun () ->
                let shallow =
                    WireCodec.ofCodecWith
                        { WireLimits.Default with
                            MaxDepth = 2
                            MaxItems = 100
                        }
                        Codec.value

                shallow.Decode "[[1]]"
                |> expectOk "two levels"
                |> ignore

                Expect.isTrue
                    (shallow.Decode "[[[1]]]"
                     |> expectError "three levels"
                     |> isLimit)
                    "kind"

            testCase "a lone surrogate in a string, char or object key does not encode"
            <| fun () ->
                raisesLoneSurrogate "a high surrogate" (fun () -> strings.Encode ("a" + u 0xD800 + "b"))
                raisesLoneSurrogate "a low surrogate" (fun () -> strings.Encode ("a" + u 0xDC00))
                raisesLoneSurrogate "a high surrogate at the end" (fun () -> strings.Encode ("a" + u 0xD83C))
                raisesLoneSurrogate "a char" (fun () -> (WireCodec.ofCodec Codec.char).Encode(char 0xD800))
                raisesLoneSurrogate "an object key" (fun () -> counts.Encode (Map.ofList [ "k" + u 0xDFFF, 1 ]))
                raisesLoneSurrogate "a list item" (fun () -> (WireCodec.ofCodec (Codec.list Codec.string)).Encode [ "ok"; u 0xD800 ])

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
                    Expect.isTrue
                        (strings.Decode text
                         |> expectError name
                         |> isSyntax)
                        name

                let ignored =
                    "{\"name\":\"a\",\"age\":1,\"note\":\""
                    + esc "d800"
                    + "\"}"

                Expect.isTrue
                    (people.Decode ignored
                     |> expectError "an ignored field"
                     |> isSyntax)
                    "an ignored field"

                let key = "{" + quote + esc "d800" + quote + ":1}"

                Expect.isTrue
                    (counts.Decode key
                     |> expectError "an object key"
                     |> isSyntax)
                    "an object key"

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

            testCase "limits outside their range are rejected"
            <| fun () ->
                Expect.throws
                    (fun () ->
                        WireCodec.ofCodecWith { WireLimits.Default with MaxDepth = 0 } Codec.value
                        |> ignore)
                    "MaxDepth 0"

                Expect.throws
                    (fun () ->
                        WireCodec.ofCodecWith
                            { WireLimits.Default with
                                MaxDepth = 1025
                            }
                            Codec.value
                        |> ignore)
                    "MaxDepth 1025"

                Expect.throws
                    (fun () ->
                        WireCodec.ofCodecWith { WireLimits.Default with MaxItems = 0 } Codec.value
                        |> ignore)
                    "MaxItems 0"
        ]
