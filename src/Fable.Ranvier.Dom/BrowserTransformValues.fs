namespace Ranvier.Dom

open System
open Fable.Core
open Fable.Core.JsInterop

/// <summary>Borrowed browser providers for the engine's Typed OM transform objects.</summary>
module TransformValues =
    [<Emit("""(()=>{try{return $0.defaultView!=null&&$0.defaultView.document===$0;}catch{return false;}})()""")>]
    let private current (document: Document) : bool = jsNative

    [<Emit("['CSSTransformValue','CSSTransformComponent','CSSTranslate','CSSRotate','CSSScale','CSSSkew','CSSSkewX','CSSSkewY','CSSPerspective','CSSMatrixComponent','CSSNumericValue','CSSKeywordValue','DOMMatrixReadOnly'].every(k=>typeof $0.defaultView[k]==='function')")>]
    let private available (document: Document) : bool = jsNative

    [<Emit("""((d,v,k)=>{
        if(v==null)return false;
        const r=d.defaultView;
        try{
            if(k==='numeric')r.CSSNumericValue.prototype.type.call(v);
            else if(k==='keyword')Object.getOwnPropertyDescriptor(r.CSSKeywordValue.prototype,'value').get.call(v);
            else if(k==='matrix')Object.getOwnPropertyDescriptor(r.DOMMatrixReadOnly.prototype,'m11').get.call(v);
            else if(k==='component')Object.getOwnPropertyDescriptor(r.CSSTransformComponent.prototype,'is2D').get.call(v);
            else if(k==='list')Object.getOwnPropertyDescriptor(r.CSSTransformValue.prototype,'length').get.call(v);
            else return false;
            return true;
        }catch{return false;}
    })($0,$1,$2)""")>]
    let private branded (document: Document) (value: obj) (kind: string) : bool = jsNative

    [<Emit("new $0.defaultView.CSSTransformValue($1)")>]
    let private list (document: Document) (values: CSSTransformComponent array) : CSSTransformValue = jsNative

    [<Emit("new $0.defaultView.CSSTranslate($1,$2)")>]
    let private translate2 (document: Document) (x: CSSNumericValue) (y: CSSNumericValue) : CSSTranslate = jsNative

    [<Emit("new $0.defaultView.CSSTranslate($1,$2,$3)")>]
    let private translate3 (document: Document) (x: CSSNumericValue) (y: CSSNumericValue) (z: CSSNumericValue) : CSSTranslate = jsNative

    [<Emit("new $0.defaultView.CSSRotate($1)")>]
    let private rotate2 (document: Document) (angle: CSSNumericValue) : CSSRotate = jsNative

    [<Emit("new $0.defaultView.CSSRotate($1,$2,$3,$4)")>]
    let private rotate3 (document: Document) (x: obj) (y: obj) (z: obj) (angle: CSSNumericValue) : CSSRotate = jsNative

    [<Emit("new $0.defaultView.CSSScale($1,$2)")>]
    let private scale2 (document: Document) (x: obj) (y: obj) : CSSScale = jsNative

    [<Emit("new $0.defaultView.CSSScale($1,$2,$3)")>]
    let private scale3 (document: Document) (x: obj) (y: obj) (z: obj) : CSSScale = jsNative

    [<Emit("new $0.defaultView.CSSSkew($1,$2)")>]
    let private skew (document: Document) (ax: CSSNumericValue) (ay: CSSNumericValue) : CSSSkew = jsNative

    [<Emit("new $0.defaultView.CSSSkewX($1)")>]
    let private skewX (document: Document) (angle: CSSNumericValue) : CSSSkewX = jsNative

    [<Emit("new $0.defaultView.CSSSkewY($1)")>]
    let private skewY (document: Document) (angle: CSSNumericValue) : CSSSkewY = jsNative

    [<Emit("new $0.defaultView.CSSPerspective($1)")>]
    let private perspective (document: Document) (length: obj) : CSSPerspective = jsNative

    [<Emit("new $0.defaultView.CSSMatrixComponent($1,$2)")>]
    let private matrixComponent (document: Document) (matrix: DOMMatrixReadOnly) (options: obj) : CSSMatrixComponent = jsNative

    [<Emit("$0.name || 'Error'")>]
    let private errorName (error: obj) : string = jsNative

    [<Emit("$0.message || String($0)")>]
    let private errorMessage (error: obj) : string = jsNative

    /// <summary>Creates a borrowed transform provider in the document's active browser realm.</summary>
    /// <param name="document">The live document supplying the Typed OM constructors and validation intrinsics.</param>
    /// <remarks>Disposal preserves returned objects; navigation invalidates the old document's provider.</remarks>
    let forDocument (document: Document) : TransformValueService =
        if isNull (box document) then
            nullArg (nameof document)

        if not (current document) || not (available document) then
            raise (NotSupportedException "Typed transform values require an active document with Typed OM constructors")

        let mutable disposed = false

        let active () =
            if disposed then
                raise (ObjectDisposedException "TransformValueService")

            if not (current document) then
                invalidOp "The transform document is no longer active"

        let require name kind value =
            if isNull value then
                nullArg name

            if not (branded document value kind) then
                invalidArg name $"Expected an engine {kind} value"

        let numberish name value =
            if isNull (box value) then
                nullArg name

            match value with
            | DoubleOrCSSNumericValue.Double value -> box value
            | DoubleOrCSSNumericValue.CSSNumericValue value ->
                require name "numeric" (box value)
                box value

        let perspectiveInput value =
            if isNull (box value) then
                nullArg (nameof value)

            match value with
            | CSSNumericValueOrDOMStringOrCSSOMKeywordValue.CSSNumericValue value ->
                require "length" "numeric" (box value)
                box value
            | CSSNumericValueOrDOMStringOrCSSOMKeywordValue.CSSOMKeywordValue value ->
                require "length" "keyword" (box value)
                box value
            | CSSNumericValueOrDOMStringOrCSSOMKeywordValue.DOMString value -> box (if isNull value then "null" else value)

        let engine action =
            try
                action ()
            with error ->
                raise (StyleValueException (errorName (box error), errorMessage (box error)))

        { new TransformValueService with
            member _.CreateList components =
                active ()

                if isNull components then
                    nullArg (nameof components)

                components
                |> Array.iter (fun value -> require "component" "component" (box value))

                engine (fun () -> list document components)

            member _.CreateTranslate(x, y) =
                active ()
                require (nameof x) "numeric" (box x)
                require (nameof y) "numeric" (box y)
                engine (fun () -> translate2 document x y)

            member _.CreateTranslate(x, y, z) =
                active ()
                require (nameof x) "numeric" (box x)
                require (nameof y) "numeric" (box y)
                require (nameof z) "numeric" (box z)
                engine (fun () -> translate3 document x y z)

            member _.CreateRotate(angle) =
                active ()
                require (nameof angle) "numeric" (box angle)
                engine (fun () -> rotate2 document angle)

            member _.CreateRotate(x, y, z, angle) =
                active ()
                let x, y, z = numberish (nameof x) x, numberish (nameof y) y, numberish (nameof z) z
                require (nameof angle) "numeric" (box angle)
                engine (fun () -> rotate3 document x y z angle)

            member _.CreateScale(x, y) =
                active ()
                let x, y = numberish (nameof x) x, numberish (nameof y) y
                engine (fun () -> scale2 document x y)

            member _.CreateScale(x, y, z) =
                active ()
                let x, y, z = numberish (nameof x) x, numberish (nameof y) y, numberish (nameof z) z
                engine (fun () -> scale3 document x y z)

            member _.CreateSkew(ax, ay) =
                active ()
                require (nameof ax) "numeric" (box ax)
                require (nameof ay) "numeric" (box ay)
                engine (fun () -> skew document ax ay)

            member _.CreateSkewX ax =
                active ()
                require (nameof ax) "numeric" (box ax)
                engine (fun () -> skewX document ax)

            member _.CreateSkewY ay =
                active ()
                require (nameof ay) "numeric" (box ay)
                engine (fun () -> skewY document ay)

            member _.CreatePerspective length =
                active ()
                let value = perspectiveInput length
                engine (fun () -> perspective document value)

            member _.CreateMatrixComponent(matrix, ?is2D) =
                active ()
                require (nameof matrix) "matrix" (box matrix)

                let options =
                    createObj
                        [
                            match is2D with
                            | None -> ()
                            | Some value -> yield "is2D" ==> value
                        ]

                engine (fun () -> matrixComponent document matrix options)

            member _.Get(value, index) =
                active ()
                require (nameof value) "list" (box value)

                engine (fun () ->
                    if index >= value.length then
                        None
                    else
                        let entry = value.item index in if isNull entry then None else Some entry)

            member _.Dispose() =
                disposed <- true
        }
