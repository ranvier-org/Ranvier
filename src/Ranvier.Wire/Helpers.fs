module internal Ranvier.Wire.Helpers

open System
open System.Text
open Ranvier.Wire.Thoth.Json.Core
#if FABLE_COMPILER
open Fable.Core
open Fable.Core.JsInterop
#else
open System.Buffers
open System.Collections.Generic
open System.Globalization
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text.Json
open System.Text.Json.Nodes
#endif

let private scanSurrogatesFrom (text: string) (start: int) : int =
    let length = text.Length
    let mutable found = -1
    let mutable i = start

    while found < 0 && i < length do
        let code = int text[i]

        if
            code >= 0xD800
            && code <= 0xDBFF
            && i + 1 < length
            && int text[i + 1] >= 0xDC00
            && int text[i + 1] <= 0xDFFF
        then
            i <- i + 2
        elif code >= 0xD800 && code <= 0xDFFF then
            found <- i
        else
            i <- i + 1

    found

/// <summary>
/// The index of the first UTF-16 code unit of the text that is a surrogate without its pair; -1 when every surrogate
/// is paired or the text is null.
/// </summary>
let loneSurrogate (text: string) : int =
    if isNull text || text.Length = 0 then
        -1
    else
#if FABLE_COMPILER
        scanSurrogatesFrom text 0
#else
#if NET8_0_OR_GREATER
        let first = text.AsSpan().IndexOfAnyInRange('\uD800', '\uDFFF')
        if first < 0 then -1 else scanSurrogatesFrom text first
#else
        scanSurrogatesFrom text 0
#endif
#endif

let private requireScalars (text: string) =
    let index = loneSurrogate text

    if index >= 0 then
        invalidArg
            "value"
            ("A string holds a lone surrogate at index "
             + string index
             + "; wire text carries Unicode scalar values only.")

let private requireCharScalar (value: char) =
    let code = int value

    if code >= 0xD800 && code <= 0xDFFF then
        invalidArg "value" "A string holds a lone surrogate at index 0; wire text carries Unicode scalar values only."

/// <summary>The helpers of <c>inner</c>, raising <c>ArgumentException</c> for a string, char or object key holding a lone surrogate.</summary>
let encoding (inner: IEncoderHelpers<'J>) : IEncoderHelpers<'J> =
    { new IEncoderHelpers<'J> with
        member _.encodeString value =
            requireScalars value
            inner.encodeString value

        member _.encodeChar value =
            requireCharScalar value
            inner.encodeChar value

        member _.encodeDecimalNumber value =
            inner.encodeDecimalNumber value

        member _.encodeBool value =
            inner.encodeBool value

        member _.encodeNull() =
            inner.encodeNull ()

        member _.encodeObject values =
#if FABLE_COMPILER
            inner.encodeObject (
                values
                |> Seq.map (fun (key, value) ->
                    requireScalars key
                    key, value)
            )
#else
            let items = ResizeArray<string * 'J>()

            for key, value in values do
                requireScalars key
                items.Add (key, value)

            inner.encodeObject (items.ToArray ())
#endif

        member _.encodeArray values =
            inner.encodeArray values

        member _.encodeList values =
            inner.encodeList values

        member _.encodeSeq values =
            inner.encodeSeq values

        member _.encodeResizeArray values =
            inner.encodeResizeArray values

        member _.encodeSignedIntegralNumber value =
            inner.encodeSignedIntegralNumber value

        member _.encodeUnsignedIntegralNumber value =
            inner.encodeUnsignedIntegralNumber value
    }

/// <summary>The caps of a <c>WireCodec</c> decode, for the decoders that enforce them.</summary>
type DecodeScope(maxBigIntDigits: int, limitIsError: bool) =
    member _.MaxBigIntDigits = maxBigIntDigits

    /// <summary>
    /// Whether a decoder gives a value over a cap as a decoder error, which carries its path; otherwise it raises
    /// <c>LimitExceeded</c>.
    /// </summary>
    member _.LimitIsError = limitIsError

/// <summary>Raised by a decoder for a value over a <c>DecodeScope</c> cap; <c>WireCodec</c> gives it as <c>WireError.Limit</c>.</summary>
type LimitExceeded(message: string) =
    inherit Exception(message)

/// <summary>Whether a JSON number's text is <c>-?digits</c> with an optional non-negative exponent.</summary>
let private isIntegralText (text: string) =
    text.IndexOf '.' < 0
    && not (text.Contains "e-")
    && not (text.Contains "E-")

let private isFiniteWhole (number: float) =
    not (Double.IsInfinity number)
    && not (Double.IsNaN number)
    && Math.Floor number = number

type private ScopedHelpers<'J>(inner: IDecoderHelpers<'J>, numberText: 'J -> string option, maxBigIntDigits: int, limitIsError: bool) =
    inherit DecodeScope(maxBigIntDigits, limitIsError)

    interface IDecoderHelpers<'J> with
        member _.isString value =
            inner.isString value

        member _.isNumber value =
            inner.isNumber value

        member _.isBoolean value =
            inner.isBoolean value

        member _.isNullValue value =
            inner.isNullValue value

        member _.isArray value =
            inner.isArray value

        member _.isObject value =
            inner.isObject value

        member _.hasProperty name value =
            inner.hasProperty name value

        member _.isIntegralValue value =
            inner.isNumber value
            && (match numberText value with
                | Some text when isIntegralText text ->
#if FABLE_COMPILER
                    isFiniteWhole (inner.asFloat value)
#else
                    match Double.TryParse (text, NumberStyles.Float, CultureInfo.InvariantCulture) with
                    | true, number -> isFiniteWhole number
                    | _ -> false
#endif
                | _ -> false)

        member _.asString value =
            inner.asString value

        member _.asFloat value =
            inner.asFloat value

        member _.asFloat32 value =
            inner.asFloat32 value

        member _.asInt value =
            inner.asInt value

        member _.asBoolean value =
            inner.asBoolean value

        member _.asArray value =
            inner.asArray value

        member _.getProperty(name, value) =
            inner.getProperty (name, value)

        member _.getProperties value =
            inner.getProperties value

        member _.anyToString value =
            inner.anyToString value

        member _.numberToString value =
            match numberText value with
            | Some text -> text
            | None ->
                failwith (
                    "The number's source text is unavailable: this JavaScript engine's JSON.parse gives a reviver no source text"
                    + " (Node 21+, Chrome 114+, Firefox 135+ and Safari 18.4+ do)."
                )

/// <summary>
/// The helpers of <c>inner</c> for one decode under <c>maxBigIntDigits</c>, raising <c>LimitExceeded</c> for a value
/// over it. A number's text is <c>numberText</c>'s (<c>None</c> when unknown, which makes <c>numberToString</c> raise),
/// and a number is integral exactly when its text is known, is <c>-?digits</c> with an optional non-negative exponent,
/// and denotes a finite whole number; the check returns <c>false</c> for every other number.
/// </summary>
let decoding (numberText: 'J -> string option) (maxBigIntDigits: int) (inner: IDecoderHelpers<'J>) : IDecoderHelpers<'J> =
    ScopedHelpers (inner, numberText, maxBigIntDigits, false)

/// <summary>The helpers of <c>decoding</c>, giving a value over <c>maxBigIntDigits</c> as a decoder error with its path.</summary>
let locating (numberText: 'J -> string option) (maxBigIntDigits: int) (inner: IDecoderHelpers<'J>) : IDecoderHelpers<'J> =
    ScopedHelpers (inner, numberText, maxBigIntDigits, true)

let private echoLength = 256

/// <summary>
/// The text, or its first 256 UTF-16 code units followed by <c>…</c> when it is longer; a surrogate pair at the cut is
/// left out whole.
/// </summary>
let private capEcho (text: string) : string =
    if text.Length <= echoLength then
        text
    else
        let last = int text[echoLength - 1]

        let cut =
            if last >= 0xD800 && last <= 0xDBFF then
                echoLength - 1
            else
                echoLength

        text.Substring (0, cut) + "…"

#if !FABLE_COMPILER
/// <summary>
/// Parses <c>key</c> as an ECMAScript array index (canonical decimal integer from 0 to 4294967294), or returns -1L.
/// </summary>
let private tryParseArrayIndex (key: string) : int64 =
    if
        isNull key
        || key.Length < 1
        || key.Length > 10
        || (key.Length > 1 && key[0] = '0')
    then
        -1L
    else
        let mutable value = 0L
        let mutable i = 0

        while value >= 0L && i < key.Length do
            let c = key[i]

            if c >= '0' && c <= '9' then
                value <- value * 10L + int64 (int c - int '0')
                i <- i + 1
            else
                value <- -1L

        if value >= 0L && value <= 4294967294L then value else -1L

/// <summary>
/// The items in ECMAScript own-property order of their keys: the array indexes in ascending numeric order, then the
/// other keys in their given order.
/// </summary>
let private inPropertyOrder (key: 'T -> string) (items: 'T array) : 'T array =
    let mutable firstIdx = -1
    let mutable firstOrder = -1L
    let mutable i = 0

    while firstIdx < 0 && i < items.Length do
        let parsed = tryParseArrayIndex (key items[i])

        if parsed >= 0L then
            firstIdx <- i
            firstOrder <- parsed
        else
            i <- i + 1

    if firstIdx < 0 then
        items
    else
        let indexed = ResizeArray<struct (int64 * int * 'T)>()
        indexed.Add (struct (firstOrder, firstIdx, items[firstIdx]))
        let others = ResizeArray<'T>(max 0 (items.Length - 1))

        for j = 0 to firstIdx - 1 do
            others.Add items[j]

        for j = firstIdx + 1 to items.Length - 1 do
            let item = items[j]
            let parsed = tryParseArrayIndex (key item)

            if parsed >= 0L then
                indexed.Add (struct (parsed, j, item))
            else
                others.Add item

        let idxArr = indexed.ToArray ()

        if idxArr.Length > 1 then
            Array.Sort (
                idxArr,
                Comparison<struct (int64 * int * 'T)>(fun (struct (ka, ia, _)) (struct (kb, ib, _)) ->
                    let c = ka.CompareTo kb
                    if c <> 0 then c else ia.CompareTo ib)
            )

        let result = Array.zeroCreate<'T> items.Length

        for j = 0 to idxArr.Length - 1 do
            let struct (_, _, item) = idxArr[j]
            result[j] <- item

        let offset = idxArr.Length

        for j = 0 to others.Count - 1 do
            result[offset + j] <- others[j]

        result

/// <summary>
/// The System.Text.Json encoder helpers, with every double written as ECMAScript number text and every object's keys
/// in ECMAScript own-property order.
/// </summary>
let netEncoding: IEncoderHelpers<JsonNode> =
    let inner = Ranvier.Wire.Thoth.Json.System.Text.Json.Encode.helpers

    { new IEncoderHelpers<JsonNode> with
        member _.encodeString value =
            inner.encodeString value

        member _.encodeChar value =
            inner.encodeChar value

        member _.encodeDecimalNumber value =
            JsonNode.Parse (Numbers.ecmaString value)

        member _.encodeBool value =
            inner.encodeBool value

        member _.encodeNull() =
            inner.encodeNull ()

        member _.encodeObject values =
            let arr =
                match values with
                | :? ((string * JsonNode) array) as ready -> ready
                | _ -> Seq.toArray values

            inner.encodeObject (inPropertyOrder fst arr)

        member _.encodeArray values =
            inner.encodeArray values

        member _.encodeList values =
            inner.encodeList values

        member _.encodeSeq values =
            inner.encodeSeq values

        member _.encodeResizeArray values =
            inner.encodeResizeArray values

        member _.encodeSignedIntegralNumber value =
            inner.encodeSignedIntegralNumber value

        member _.encodeUnsignedIntegralNumber value =
            inner.encodeUnsignedIntegralNumber value
    }

/// <summary>The distinct member lookups an object answers by scanning its element before it builds an index.</summary>
[<Literal>]
let private MaxMemberScans = 8

/// <summary>
/// An object's members as <c>JSON.parse</c> gives them: its keys in ECMAScript own-property order, and a repeated key
/// at its first position with its last value. Key matching is ordinal.
/// </summary>
/// <remarks>
/// The first <c>MaxMemberScans</c> distinct lookups scan the element; a later lookup or the first read of
/// <c>Keys</c> indexes every member.
/// </remarks>
[<Sealed; AllowNullLiteral>]
type private ObjectMembers(element: JsonElement) =
    let mutable found: Dictionary<string, JsonElement> = null
    // The distinct keys: in first-position order once indexed, in own-property order once Keys has been read.
    let mutable keys: string array = null
    let mutable ordered = false
    let mutable seenKeys: string array = null
    let mutable seenValues: JsonElement array = null
    let mutable seenPresent: bool array = null
    let mutable scans = 0

    member private _.Index() =
        let index = Dictionary<string, JsonElement>(StringComparer.Ordinal)
        let order = ResizeArray<string>()

        for property in element.EnumerateObject () do
            let name = property.Name
            let prevCount = index.Count
            index[name] <- property.Value

            if index.Count > prevCount then
                order.Add name

        keys <- order.ToArray ()
        found <- index

    member private _.Indexed(key: string) : JsonElement voption =
        match found.TryGetValue key with
        | true, value -> ValueSome value
        | _ -> ValueNone

    member this.Keys: string array =
        if isNull found then
            this.Index ()

        if not ordered then
            keys <- inPropertyOrder id keys
            ordered <- true

        keys

    member this.TryFind(key: string) : JsonElement voption =
        if not (isNull found) then
            this.Indexed key
        elif loneSurrogate key >= 0 then
            ValueNone
        else
            let mutable hit = -1

            if scans > 0 then
                let mutable i = 0

                while hit < 0 && i < scans do
                    if String.Equals (seenKeys[i], key, StringComparison.Ordinal) then
                        hit <- i

                    i <- i + 1

            if hit >= 0 then
                if seenPresent[hit] then
                    ValueSome seenValues[hit]
                else
                    ValueNone
            elif scans < MaxMemberScans then
                if isNull seenKeys then
                    seenKeys <- Array.zeroCreate MaxMemberScans
                    seenValues <- Array.zeroCreate MaxMemberScans
                    seenPresent <- Array.zeroCreate MaxMemberScans

                let mutable value = Unchecked.defaultof<JsonElement>
                let present = element.TryGetProperty (key, &value)
                let idx = scans
                seenKeys[idx] <- key
                seenValues[idx] <- value
                seenPresent[idx] <- present
                scans <- idx + 1
                if present then ValueSome value else ValueNone
            else
                this.Index ()
                this.Indexed key

/// <summary>The byte offset of the element's text from the root's, which identifies the element within its document.</summary>
let private offsetIn (root: JsonElement) (element: JsonElement) : int64 =
    let rootText = JsonMarshal.GetRawUtf8Value root
    let text = JsonMarshal.GetRawUtf8Value element
    int64 (Unsafe.ByteOffset (&MemoryMarshal.GetReference rootText, &MemoryMarshal.GetReference text))

/// <summary>Raised by <c>EchoBuffer</c> on a request for memory once it holds its capacity.</summary>
type private EchoFull() =
    inherit Exception("The echo buffer is full.")

/// <summary>A buffer writer holding the first <c>capacity</c> bytes written to it.</summary>
type private EchoBuffer(capacity: int) =
    let kept: byte array = Array.zeroCreate capacity
    let mutable count = 0
    let mutable current: byte array = Array.empty

    /// <summary>The bytes held, as UTF-8 text.</summary>
    member _.Text = Encoding.UTF8.GetString (kept, 0, min count capacity)

    interface IBufferWriter<byte> with
        member _.Advance written =
            let held = min count capacity
            Array.blit current 0 kept held (min written (capacity - held))
            count <- count + written

        member _.GetMemory sizeHint =
            if count >= capacity then
                raise (EchoFull ())

            current <- Array.zeroCreate (max sizeHint 256)
            Memory<byte> current

        member this.GetSpan sizeHint =
            (this :> IBufferWriter<byte>).GetMemory(sizeHint).Span

/// <summary>
/// The element's text for an error message: a number's source text, or at most 256 characters of any other value's
/// indented text, written into a buffer of 1,024 bytes.
/// </summary>
let private echo (element: JsonElement) : string =
    if element.ValueKind = JsonValueKind.Number then
        element.GetRawText ()
    else
        let buffer = EchoBuffer 1024

        try
            use writer =
                new Utf8JsonWriter (buffer, JsonWriterOptions (Indented = true, NewLine = "\n", IndentSize = 4))

            element.WriteTo writer
            writer.Flush ()
        with :? EchoFull ->
            ()

        capEcho buffer.Text

let private netInner = Ranvier.Wire.Thoth.Json.System.Text.Json.Decode.helpers

/// <summary>
/// The System.Text.Json decoder helpers for one decode of the document whose root is <c>root</c>. Each object keeps
/// one <c>ObjectMembers</c> for the decode, and <c>getProperties</c> gives the keys in ECMAScript own-property order;
/// a value is echoed as <c>echo</c> gives it.
/// </summary>
[<Sealed>]
type private NetDecoderHelpers(root: JsonElement) =
    // The members of the object read last, at its offset; -1 before the first object.
    let mutable lastOffset = -1L
    let mutable lastMembers: ObjectMembers = null
    // Every object's members by offset, from the second object read; null before it.
    let mutable objects: Dictionary<int64, ObjectMembers> = null

    member private _.MembersOf(value: JsonElement) : ObjectMembers =
        if value.ValueKind <> JsonValueKind.Object then
            raise (InvalidOperationException "Object members are read from a JSON object only.")

        let offset = offsetIn root value

        if offset = lastOffset then
            lastMembers
        else
            let members =
                if isNull objects then
                    if isNull lastMembers then
                        ObjectMembers value
                    else
                        let created = ObjectMembers value
                        let all = Dictionary<int64, ObjectMembers>()
                        all[lastOffset] <- lastMembers
                        all[offset] <- created
                        objects <- all
                        created
                else
                    match objects.TryGetValue offset with
                    | true, known -> known
                    | _ ->
                        let created = ObjectMembers value
                        objects[offset] <- created
                        created

            lastOffset <- offset
            lastMembers <- members
            members

    interface IDecoderHelpers<JsonElement> with
        member _.isString value =
            netInner.isString value

        member _.isNumber value =
            netInner.isNumber value

        member _.isBoolean value =
            netInner.isBoolean value

        member _.isNullValue value =
            netInner.isNullValue value

        member _.isArray value =
            netInner.isArray value

        member _.isObject value =
            netInner.isObject value

        member this.hasProperty name value =
            value.ValueKind = JsonValueKind.Object
            && (this.MembersOf value).TryFind(name).IsSome

        member _.isIntegralValue value =
            netInner.isIntegralValue value

        member _.asString value =
            netInner.asString value

        member _.asFloat value =
            netInner.asFloat value

        member _.asFloat32 value =
            netInner.asFloat32 value

        member _.asInt value =
            netInner.asInt value

        member _.asBoolean value =
            netInner.asBoolean value

        member _.asArray value =
            netInner.asArray value

        member this.getProperty(name, value) =
            match (this.MembersOf value).TryFind name with
            | ValueSome found -> found
            | ValueNone -> raise (KeyNotFoundException ("The object has no member named " + name + "."))

        member this.getProperties value =
            (this.MembersOf value).Keys :> string seq

        member _.anyToString value =
            echo value

        member _.numberToString value =
            netInner.numberToString value

/// <summary>The System.Text.Json decoder helpers for one decode of the document whose root is <c>root</c>.</summary>
let netDecoding (root: JsonElement) : IDecoderHelpers<JsonElement> =
    NetDecoderHelpers root

/// <summary>A number's text, read verbatim from the document.</summary>
let netNumberText (value: JsonElement) : string option =
    Some (value.GetRawText ())
#else
let private stringify (value: obj) : string =
    emitJsExpr value "JSON.stringify($0)"

/// <summary>
/// Appends the value's <c>JSON.stringify(value, null, 4)</c> text to the builder, stopping once the builder holds more
/// than 256 characters.
/// </summary>
let rec private render (builder: StringBuilder) (indent: string) (value: obj) : unit =
    if builder.Length <= echoLength then
        let inner = indent + "    "

        if JS.Constructors.Array.isArray value then
            let items: obj array = unbox value

            if items.Length = 0 then
                builder.Append "[]" |> ignore
            else
                builder.Append "[" |> ignore
                let mutable i = 0

                while i < items.Length && builder.Length <= echoLength do
                    builder.Append(if i = 0 then "\n" else ",\n").Append inner
                    |> ignore

                    render builder inner items[i]
                    i <- i + 1

                builder.Append("\n").Append(indent).Append "]"
                |> ignore
        elif emitJsExpr value "$0 !== null && typeof $0 === 'object' && !($0 instanceof Number)" then
            let keys: string array = emitJsExpr value "Object.keys($0)"

            if keys.Length = 0 then
                builder.Append "{}" |> ignore
            else
                builder.Append "{" |> ignore
                let mutable i = 0

                while i < keys.Length && builder.Length <= echoLength do
                    builder.Append(if i = 0 then "\n" else ",\n").Append(inner).Append(stringify (box keys[i])).Append ": "
                    |> ignore

                    render builder inner (emitJsExpr (value, keys[i]) "$0[$1]")
                    i <- i + 1

                builder.Append("\n").Append(indent).Append "}"
                |> ignore
        elif
            value :? string
            && (unbox<string> value).Length > echoLength
        then
            builder.Append (stringify (box ((unbox<string> value).Substring(0, echoLength + 1))))
            |> ignore
        else
            builder.Append (stringify value) |> ignore

/// <summary>The JavaScript encoder helpers, writing each object with a null prototype so that every key is its own property.</summary>
let fableEncoding: IEncoderHelpers<obj> =
    let inner = Ranvier.Wire.Thoth.Json.JavaScript.Encode.helpers

    { new IEncoderHelpers<obj> with
        member _.encodeString value =
            inner.encodeString value

        member _.encodeChar value =
            inner.encodeChar value

        member _.encodeDecimalNumber value =
            inner.encodeDecimalNumber value

        member _.encodeBool value =
            inner.encodeBool value

        member _.encodeNull() =
            inner.encodeNull ()

        member _.encodeObject values =
            let o: obj = emitJsExpr () "Object.create(null)"

            for key, value in values do
                o?(key) <- value

            o

        member _.encodeArray values =
            inner.encodeArray values

        member _.encodeList values =
            inner.encodeList values

        member _.encodeSeq values =
            inner.encodeSeq values

        member _.encodeResizeArray values =
            inner.encodeResizeArray values

        member _.encodeSignedIntegralNumber value =
            inner.encodeSignedIntegralNumber value

        member _.encodeUnsignedIntegralNumber value =
            inner.encodeUnsignedIntegralNumber value
    }

/// <summary>
/// The JavaScript decoder helpers, reading only an object's own properties, and echoing a number as its source text
/// and any other value as at most 256 characters of its <c>JSON.stringify(value, null, 4)</c> text.
/// </summary>
let fableDecoding: IDecoderHelpers<obj> =
    let inner = Ranvier.Wire.Thoth.Json.JavaScript.Decode.helpers

    { new IDecoderHelpers<obj> with
        member _.isString value =
            inner.isString value

        member _.isNumber value =
            inner.isNumber value

        member _.isBoolean value =
            inner.isBoolean value

        member _.isNullValue value =
            inner.isNullValue value

        member _.isArray value =
            inner.isArray value

        member _.isObject value =
            inner.isObject value

        member _.hasProperty name value =
            emitJsExpr (value, name) "Object.prototype.hasOwnProperty.call($0, $1)"

        member _.isIntegralValue value =
            inner.isIntegralValue value

        member _.asString value =
            inner.asString value

        member _.asFloat value =
            inner.asFloat value

        member _.asFloat32 value =
            inner.asFloat32 value

        member _.asInt value =
            inner.asInt value

        member _.asBoolean value =
            inner.asBoolean value

        member _.asArray value =
            inner.asArray value

        member _.getProperty(name, value) =
            emitJsExpr (value, name) "Object.prototype.hasOwnProperty.call($0, $1) ? $0[$1] : undefined"

        member _.getProperties value =
            inner.getProperties value

        member _.anyToString value =
            if inner.isNumber value then
                inner.anyToString value
            else
                let builder = StringBuilder ()
                render builder "" value
                capEcho (builder.ToString ())

        member _.numberToString value =
            inner.numberToString value
    }
#endif
