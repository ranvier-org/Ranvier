namespace Ranvier.Dom

open System
open Fable.Core

/// <summary>Browser providers for CSS support queries in a document's Window.</summary>
module CssQueries =
    [<Emit("$0.defaultView != null && typeof $0.defaultView.CSS?.supports === 'function'")>]
    let private available (document: Document) : bool = jsNative

    [<Emit("$0.defaultView.CSS.supports($1, $2)")>]
    let private declaration (document: Document) (property: string) (value: string) : bool = jsNative

    [<Emit("$0.defaultView.CSS.supports($1)")>]
    let private condition (document: Document) (conditionText: string) : bool = jsNative

    let private domString value =
        if isNull value then "null" else value

    /// <summary>Creates a borrowed provider; documents without a CSS support-query implementation are unsupported.</summary>
    /// <param name="document">The page document whose Window supplies the CSS parser.</param>
    /// <exception cref="T:System.ArgumentNullException">The document is null.</exception>
    /// <exception cref="T:System.NotSupportedException">The document's Window has no CSS support-query implementation.</exception>
    let forDocument (document: Document) : CssQueryService =
        if isNull (box document) then
            nullArg (nameof document)

        if not (available document) then
            raise (NotSupportedException "The CSS query service requires a document with CSS.supports")

        { new CssQueryService with
            member _.Supports(property, value) =
                declaration document (domString property) (domString value)

            member _.Supports(conditionText) =
                condition document (domString conditionText)
        }
