namespace Ranvier.Dom

open System
open Fable.Core

/// <summary>Browser providers over actual inline, declared and computed typed style maps.</summary>
module StyleMaps =
    [<Emit("""(() => {
        try { const realm = $0.defaultView; return realm != null && realm.document === $0; }
        catch { return false; }
    })()""")>]
    let private current (document: Document) : bool = jsNative

    [<Emit("""(() => {
        const realm = $0.defaultView;
        return typeof realm?.Element?.prototype.computedStyleMap === 'function'
            && typeof realm?.StylePropertyMapReadOnly?.prototype.entries === 'function';
    })()""")>]
    let private available (document: Document) : bool = jsNative

    [<Emit("Object.getOwnPropertyDescriptor($0.defaultView.Node.prototype, 'ownerDocument').get.call($1)")>]
    let private checkElement (document: Document) (element: obj) : Document = jsNative

    [<Emit("Object.getOwnPropertyDescriptor($0.defaultView.CSSStyleRule.prototype, 'styleMap').get.call($1)")>]
    let private declared (document: Document) (rule: CSSStyleRule) : StylePropertyMap = jsNative

    [<Emit("Object.getOwnPropertyDescriptor($0.defaultView.StylePropertyMapReadOnly.prototype, 'size').get.call($1)")>]
    let private checkMap (document: Document) (map: StylePropertyMapReadOnly) : uint32 = jsNative

    [<Emit("Array.from($0.entries(), ([property, values]) => [property, Array.from(values)])")>]
    let private entries (map: StylePropertyMapReadOnly) : (string * CSSStyleValue array) array = jsNative

    [<Emit("($0 == null ? 'null' : $0).toWellFormed()")>]
    let private usv (value: string) : string = jsNative

    [<Emit("$0.name || 'Error'")>]
    let private errorName (error: obj) : string = jsNative

    [<Emit("$0.message || String($0)")>]
    let private errorMessage (error: obj) : string = jsNative

    /// <summary>Creates a borrowed typed-map provider bound to the supplied active document.</summary>
    /// <param name="document">The active document supplying the provider's realm and validation context.</param>
    /// <remarks>Disposal invalidates this provider without changing previously acquired engine objects.</remarks>
    let forDocument (document: Document) : StyleMapService =
        if isNull (box document) then
            nullArg (nameof document)

        if not (current document) || not (available document) then
            raise (NotSupportedException "The style-map service requires an active document with CSS Typed OM")

        let mutable disposed = false

        let active () =
            if disposed then
                raise (ObjectDisposedException "StyleMapService")

            if not (current document) then
                invalidOp "The style-map service document is no longer active"

        let engine action =
            try
                action ()
            with error ->
                raise (StyleValueException (errorName (box error), errorMessage (box error)))

        let map value =
            active ()

            if isNull (box value) then
                nullArg (nameof value)

            engine (fun () -> checkMap document value |> ignore)

        { new StyleMapService with
            member _.Attribute element =
                active ()

                if isNull (box element) then
                    nullArg (nameof element)

                engine (fun () ->
                    checkElement document (box element) |> ignore
                    let value = element.attributeStyleMap
                    checkMap document value |> ignore
                    value)

            member _.Computed element =
                active ()

                if isNull (box element) then
                    nullArg (nameof element)

                engine (fun () ->
                    checkElement document (box element) |> ignore
                    let value = element.computedStyleMap ()
                    checkMap document value |> ignore
                    value)

            member _.Declared rule =
                active ()

                if isNull (box rule) then
                    nullArg (nameof rule)

                engine (fun () -> declared document rule)

            member _.Get(value, property) =
                map value

                engine (fun () ->
                    match value.get (usv property) with
                    | UndefinedOrCSSStyleValue.Undefined () -> None
                    | UndefinedOrCSSStyleValue.CSSStyleValue value -> Some value)

            member _.Entries value =
                map value

                engine (fun () ->
                    entries value
                    |> Array.map (fun (property, values) -> { Property = property; Values = values }))

            member _.Dispose() =
                disposed <- true
        }
