namespace Ranvier.Dom

open System
open Fable.Core

/// <summary>Browser providers for live computed-style declarations.</summary>
module ComputedStyles =
    [<Emit("$0.defaultView != null && typeof $0.defaultView.getComputedStyle === 'function'")>]
    let private available (document: Document) : bool = jsNative

    [<Emit("$0.defaultView.getComputedStyle($1, $2)")>]
    let private computed (document: Document) (element: Element) (pseudoElement: string) : CSSStyleDeclaration = jsNative

    /// <summary>Creates a borrowed provider bound to the document's Window.</summary>
    /// <param name="document">The live page document that owns each queried element.</param>
    /// <exception cref="T:System.ArgumentNullException">The document is null.</exception>
    /// <exception cref="T:System.NotSupportedException">The document has no Window with getComputedStyle.</exception>
    let forDocument (document: Document) : ComputedStyleService =
        if isNull (box document) then
            nullArg (nameof document)

        if not (available document) then
            raise (NotSupportedException "Computed styles require a document with getComputedStyle")

        { new ComputedStyleService with
            member _.Get(element, pseudoElement) =
                if isNull (box element) then
                    nullArg (nameof element)

                if not (obj.ReferenceEquals (element.ownerDocument, document)) then
                    invalidArg (nameof element) "The element belongs to another document"

                computed document element (Option.defaultValue null pseudoElement)
        }
