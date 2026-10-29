namespace Ranvier.Dom

open System
open Fable.Core
open Fable.Core.JsInterop

/// <summary>Browser providers for custom-property registration in a document's Window.</summary>
module CustomProperties =
    [<Emit("$0.defaultView != null && typeof $0.defaultView.CSS?.registerProperty === 'function'")>]
    let private available (document: Document) : bool = jsNative

    [<Emit("$0.defaultView.CSS.registerProperty($1)")>]
    let private register (document: Document) (definition: obj) : unit = jsNative

    [<Emit("$0.name || 'Error'")>]
    let private errorName (error: obj) : string = jsNative

    [<Emit("$0.message || String($0)")>]
    let private errorMessage (error: obj) : string = jsNative

    let private domString value =
        if isNull value then "null" else value

    /// <summary>Creates a borrowed provider using the document's Window and custom-property registry.</summary>
    /// <param name="document">The document whose CSS engine validates and retains registrations.</param>
    /// <remarks>Disposal stops further calls through this provider; existing registrations remain in the document.</remarks>
    /// <exception cref="T:System.ArgumentNullException">The document is null.</exception>
    /// <exception cref="T:System.NotSupportedException">The Window has no CSS.registerProperty implementation.</exception>
    let forDocument (document: Document) : CustomPropertyService =
        if isNull (box document) then
            nullArg (nameof document)

        if not (available document) then
            raise (NotSupportedException "The custom-property service requires a document with CSS.registerProperty")

        let mutable disposed = false

        { new CustomPropertyService with
            member _.Register definition =
                if disposed then
                    raise (ObjectDisposedException "CustomPropertyService")

                if isNull (box definition) then
                    nullArg (nameof definition)

                let properties =
                    ResizeArray<string * obj>(
                        [
                            "name", box (domString definition.Name)
                            "syntax", box (domString definition.Syntax)
                            "inherits", box definition.Inherits
                        ]
                    )

                match definition.InitialValue with
                | CustomPropertyInitialValue.Omitted -> ()
                | CustomPropertyInitialValue.Value value -> properties.Add ("initialValue", box (domString value))

                try
                    register document (createObj properties)
                with error ->
                    raise (CustomPropertyException (errorName (box error), errorMessage (box error)))

            member _.Dispose() =
                disposed <- true
        }
