namespace Ranvier.Dom

open System
open Fable.Core

/// <summary>Resize observation of a browser document through its Window's <c>ResizeObserver</c>.</summary>
module BrowserResizeObservation =
    [<Emit("$0.defaultView")>]
    let private windowOf (document: Document) : obj = jsNative

    [<Emit("typeof $0.ResizeObserverEntry === 'function' && 'devicePixelContentBoxSize' in $0.ResizeObserverEntry.prototype")>]
    let private hasDevicePixel (window: obj) : bool = jsNative

    [<Emit("new $0.ResizeObserver($1)")>]
    let private construct (window: obj, callback: obj array -> unit) : obj = jsNative

    [<Emit("$0.observe($1, { box: $2 })")>]
    let private observe (observer: obj, target: Element, box: string) : unit = jsNative

    [<Emit("$0.unobserve($1)")>]
    let private unobserve (observer: obj, target: Element) : unit = jsNative

    [<Emit("$0.disconnect()")>]
    let private disconnect (observer: obj) : unit = jsNative

    [<Emit("$0.target")>]
    let private targetOf (entry: obj) : Element = jsNative

    [<Emit("$0.contentRect")>]
    let private contentRectOf (entry: obj) : obj = jsNative

    [<Emit("$0.contentBoxSize")>]
    let private contentBoxOf (entry: obj) : obj = jsNative

    [<Emit("$0.borderBoxSize")>]
    let private borderBoxOf (entry: obj) : obj = jsNative

    [<Emit("$0.devicePixelContentBoxSize")>]
    let private devicePixelBoxOf (entry: obj) : obj = jsNative

    [<Emit("$0.x")>]
    let private xOf (rect: obj) : float = jsNative

    [<Emit("$0.y")>]
    let private yOf (rect: obj) : float = jsNative

    [<Emit("$0.width")>]
    let private widthOf (rect: obj) : float = jsNative

    [<Emit("$0.height")>]
    let private heightOf (rect: obj) : float = jsNative

    // The length Array.from gives the list: 0 for a missing list and for a value without a length.
    [<Emit("($0 ? $0.length >>> 0 : 0)")>]
    let private countOf (sizes: obj) : int = jsNative

    [<Emit("$0[$1].inlineSize")>]
    let private inlineSizeAt (sizes: obj, index: int) : float = jsNative

    [<Emit("$0[$1].blockSize")>]
    let private blockSizeAt (sizes: obj, index: int) : float = jsNative

    [<Emit("new Array($0)")>]
    let private newSizes (count: int) : ResizeObserverSize array = jsNative

    [<Emit("$0[$1] = $2")>]
    let private setSize (sizes: ResizeObserverSize array, index: int, size: ResizeObserverSize) : unit = jsNative

    let private sizes (list: obj) : ResizeObserverSize array =
        let count = countOf list
        let result = newSizes count

        for index = 0 to count - 1 do
            setSize (
                result,
                index,
                {
                    InlineSize = inlineSizeAt (list, index)
                    BlockSize = blockSizeAt (list, index)
                }
            )

        result

    let private copy (entry: obj) : ResizeObserverEntry =
        let rect = contentRectOf entry

        {
            Target = targetOf entry
            ContentRect =
                {
                    X = xOf rect
                    Y = yOf rect
                    Width = widthOf rect
                    Height = heightOf rect
                }
            ContentBoxSize = sizes (contentBoxOf entry)
            BorderBoxSize = sizes (borderBoxOf entry)
            DevicePixelContentBoxSize = sizes (devicePixelBoxOf entry)
        }

    let private boxName (observed: ResizeObserverBox) =
        match observed with
        | ResizeObserverBox.ContentBox -> "content-box"
        | ResizeObserverBox.BorderBox -> "border-box"
        | ResizeObserverBox.DevicePixelContentBox -> "device-pixel-content-box"

    /// <summary>The provider over <c>document</c>'s Window.</summary>
    /// <remarks>An exception a callback raises reaches the window's error event; other observers keep delivering.</remarks>
    let provider (document: Document) : ResizeObservationProvider =
        if isNull (box document) then
            nullArg "document"

        let window = windowOf document

        if isNull window then
            raise (NotSupportedException "Resize observation requires a document with a Window")

        let devicePixel = hasDevicePixel window

        {
            Supports =
                fun observed ->
                    match observed with
                    | ResizeObserverBox.DevicePixelContentBox -> devicePixel
                    | _ -> true
            Create =
                fun deliver ->
                    let observer = construct (window, (fun entries -> deliver (Array.map copy entries)))

                    {
                        Observe = fun target observed -> observe (observer, target, boxName observed)
                        Unobserve = fun target -> unobserve (observer, target)
                        Disconnect = fun () -> disconnect observer
                        Release = fun () -> disconnect observer
                    }
        }

    /// <summary>A resize observation service of <c>document</c>.</summary>
    let forDocument (document: Document) : ResizeObservationService =
        ResizeObservation.create (provider document)
