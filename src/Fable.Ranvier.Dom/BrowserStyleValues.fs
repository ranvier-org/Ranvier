namespace Ranvier.Dom

open System
open Fable.Core

/// <summary>Browser providers for parsing and constructing CSS Typed OM values in a document's realm.</summary>
module StyleValues =
    [<Emit("""(() => {
        try { const realm = $0.defaultView; return realm != null && realm.document === $0; }
        catch { return false; }
    })()""")>]
    let private current (document: Document) : bool = jsNative

    [<Emit("""(() => {
        const realm = $0.defaultView;
        return realm != null && typeof realm.CSSStyleValue?.parse === 'function'
            && typeof realm.CSSStyleValue?.parseAll === 'function'
            && typeof realm.CSSKeywordValue === 'function' && typeof realm.CSSUnitValue === 'function'
            && typeof realm.CSSUnparsedValue === 'function' && typeof realm.CSSVariableReferenceValue === 'function';
    })()""")>]
    let private available (document: Document) : bool = jsNative

    [<Emit("($0 == null ? 'null' : $0).toWellFormed()")>]
    let private usv (value: string) : string = jsNative

    [<Emit("$0.defaultView.CSSStyleValue.parse($1, $2)")>]
    let private parse (document: Document) (property: string) (cssText: string) : CSSStyleValue = jsNative

    [<Emit("Array.from($0.defaultView.CSSStyleValue.parseAll($1, $2))")>]
    let private parseAll (document: Document) (property: string) (cssText: string) : CSSStyleValue array = jsNative

    [<Emit("new $0.defaultView.CSSKeywordValue($1)")>]
    let private keyword (document: Document) (value: string) : CSSKeywordValue = jsNative

    [<Emit("new $0.defaultView.CSSUnitValue($1, $2)")>]
    let private unitValue (document: Document) (value: float) (unitName: string) : CSSUnitValue = jsNative

    [<Emit("new $0.defaultView.CSSUnparsedValue($1)")>]
    let private unparsed (document: Document) (segments: obj array) : CSSUnparsedValue = jsNative

    [<Emit("new $0.defaultView.CSSVariableReferenceValue($1, $2)")>]
    let private variable (document: Document) (name: string) (fallback: CSSUnparsedValue) : CSSVariableReferenceValue = jsNative

    [<Emit("Object.getOwnPropertyDescriptor($0.defaultView.CSSVariableReferenceValue.prototype, 'variable').get.call($1)")>]
    let private checkReference (document: Document) (value: CSSVariableReferenceValue) : string = jsNative

    [<Emit("Object.getOwnPropertyDescriptor($0.defaultView.CSSUnparsedValue.prototype, 'length').get.call($1)")>]
    let private checkFallback (document: Document) (value: CSSUnparsedValue) : uint32 = jsNative

    [<Emit("$0.name || 'Error'")>]
    let private errorName (error: obj) : string = jsNative

    [<Emit("$0.message || String($0)")>]
    let private errorMessage (error: obj) : string = jsNative

    /// <summary>Creates a borrowed provider bound to the document's active Window.</summary>
    /// <param name="document">The live document supplying parser context and Typed OM constructors.</param>
    /// <remarks>Existing value objects remain usable after provider disposal. Navigation invalidates the old document's provider.</remarks>
    let forDocument (document: Document) : StyleValueService =
        if isNull (box document) then
            nullArg (nameof document)

        if not (current document) || not (available document) then
            raise (NotSupportedException "The style-value service requires an active document with CSS Typed OM")

        let mutable disposed = false

        let active () =
            if disposed then
                raise (ObjectDisposedException "StyleValueService")

            if not (current document) then
                raise (InvalidOperationException "The style-value service document is no longer active")

        let engine action =
            try
                action ()
            with error ->
                raise (StyleValueException (errorName (box error), errorMessage (box error)))

        { new StyleValueService with
            member _.Parse(property, cssText) =
                active ()
                engine (fun () -> parse document (usv property) (usv cssText))

            member _.ParseAll(property, cssText) =
                active ()
                engine (fun () -> parseAll document (usv property) (usv cssText))

            member _.CreateKeyword value =
                active ()
                engine (fun () -> keyword document (usv value))

            member _.CreateUnit(value, unitName) =
                active ()
                engine (fun () -> unitValue document value (usv unitName))

            member _.CreateUnparsed segments =
                active ()

                if isNull segments then
                    nullArg (nameof segments)

                let members =
                    segments
                    |> Array.map (fun segment ->
                        if isNull (box segment) then
                            nullArg (nameof segments)

                        match segment with
                        | USVStringOrCSSOMVariableReferenceValue.USVString text -> box (usv text)
                        | USVStringOrCSSOMVariableReferenceValue.CSSOMVariableReferenceValue reference ->
                            if isNull (box reference) then
                                nullArg (nameof segments)

                            engine (fun () -> checkReference document reference |> ignore)
                            box reference)

                engine (fun () -> unparsed document members)

            member _.CreateVariableReference(name, fallback) =
                active ()
                let fallback = fallback |> Option.defaultValue null

                if not (isNull (box fallback)) then
                    engine (fun () -> checkFallback document fallback |> ignore)

                engine (fun () -> variable document (usv name) fallback)

            member _.Dispose() =
                disposed <- true
        }
