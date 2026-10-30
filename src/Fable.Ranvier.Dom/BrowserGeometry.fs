namespace Ranvier.Dom

open System
open Fable.Core
open Fable.Core.JsInterop

/// <summary>Browser providers for geometry values in a document's active realm.</summary>
module Geometry =
    [<Emit("""(() => {
        try { const realm=$0.defaultView; return realm!=null && realm.document===$0; }
        catch { return false; }
    })()""")>]
    let private current (document: Document) : bool = jsNative

    [<Emit("""(() => {
        const r=$0.defaultView;
        return r!=null && ['DOMRect','DOMRectReadOnly','DOMPoint','DOMPointReadOnly','DOMQuad','DOMMatrix','DOMMatrixReadOnly']
            .every(name => typeof r[name]==='function');
    })()""")>]
    let private available (document: Document) : bool = jsNative

    [<Emit("new $0.defaultView.DOMRect($1,$2,$3,$4)")>]
    let private rect (document: Document) (x: float) (y: float) (width: float) (height: float) : DOMRect = jsNative

    [<Emit("new $0.defaultView.DOMRectReadOnly($1,$2,$3,$4)")>]
    let private readonlyRect (document: Document) (x: float) (y: float) (width: float) (height: float) : DOMRectReadOnly = jsNative

    [<Emit("new $0.defaultView.DOMPoint($1,$2,$3,$4)")>]
    let private point (document: Document) (x: float) (y: float) (z: float) (w: float) : DOMPoint = jsNative

    [<Emit("new $0.defaultView.DOMPointReadOnly($1,$2,$3,$4)")>]
    let private readonlyPoint (document: Document) (x: float) (y: float) (z: float) (w: float) : DOMPointReadOnly = jsNative

    [<Emit("$0.defaultView.DOMRect.fromRect($1)")>]
    let private rectFrom (document: Document) (init: obj) : DOMRect = jsNative

    [<Emit("$0.defaultView.DOMRectReadOnly.fromRect($1)")>]
    let private readonlyRectFrom (document: Document) (init: obj) : DOMRectReadOnly = jsNative

    [<Emit("$0.defaultView.DOMPoint.fromPoint($1)")>]
    let private pointFrom (document: Document) (init: obj) : DOMPoint = jsNative

    [<Emit("$0.defaultView.DOMPointReadOnly.fromPoint($1)")>]
    let private readonlyPointFrom (document: Document) (init: obj) : DOMPointReadOnly = jsNative

    [<Emit("new $0.defaultView.DOMQuad($1,$2,$3,$4)")>]
    let private quad (document: Document) (p1: obj) (p2: obj) (p3: obj) (p4: obj) : DOMQuad = jsNative

    [<Emit("$0.defaultView.DOMQuad.fromRect($1)")>]
    let private quadFromRect (document: Document) (init: obj) : DOMQuad = jsNative

    [<Emit("$0.defaultView.DOMQuad.fromQuad($1)")>]
    let private quadFrom (document: Document) (init: obj) : DOMQuad = jsNative

    [<Emit("new $0.defaultView.DOMMatrix()")>]
    let private matrix (document: Document) : DOMMatrix = jsNative

    [<Emit("new $0.defaultView.DOMMatrix($1)")>]
    let private matrixInput (document: Document) (init: obj) : DOMMatrix = jsNative

    [<Emit("new $0.defaultView.DOMMatrixReadOnly()")>]
    let private readonlyMatrix (document: Document) : DOMMatrixReadOnly = jsNative

    [<Emit("new $0.defaultView.DOMMatrixReadOnly($1)")>]
    let private readonlyMatrixInput (document: Document) (init: obj) : DOMMatrixReadOnly = jsNative

    [<Emit("$0.defaultView.DOMMatrix.fromMatrix($1)")>]
    let private matrixFrom (document: Document) (init: obj) : DOMMatrix = jsNative

    [<Emit("$0.defaultView.DOMMatrixReadOnly.fromMatrix($1)")>]
    let private readonlyMatrixFrom (document: Document) (init: obj) : DOMMatrixReadOnly = jsNative

    [<Emit("$0.name || 'Error'")>]
    let private errorName (error: obj) : string = jsNative

    [<Emit("$0.message || String($0)")>]
    let private errorMessage (error: obj) : string = jsNative

    let private pointInput (init: DOMPointInit) =
        let init = if isNull init then DOMPointInit () else init
        createObj [ "x" ==> init.x; "y" ==> init.y; "z" ==> init.z; "w" ==> init.w ]

    let private matrixDictionary (init: DOMMatrixInit) =
        let init = if isNull init then DOMMatrixInit () else init

        createObj
            [
                for key, value in
                    [
                        "a", init.a
                        "b", init.b
                        "c", init.c
                        "d", init.d
                        "e", init.e
                        "f", init.f
                        "m11", init.m11
                        "m12", init.m12
                        "m21", init.m21
                        "m22", init.m22
                        "m41", init.m41
                        "m42", init.m42
                    ] do
                    match value with
                    | Some value -> yield key ==> value
                    | None -> ()
                for key, value in
                    [
                        "m13", init.m13
                        "m14", init.m14
                        "m23", init.m23
                        "m24", init.m24
                        "m31", init.m31
                        "m32", init.m32
                        "m33", init.m33
                        "m34", init.m34
                        "m43", init.m43
                        "m44", init.m44
                    ] do
                    yield key ==> value
                match init.is2D with
                | Some value -> yield "is2D" ==> value
                | None -> ()
            ]

    let private rectInput (init: DOMRectInit) =
        let init = if isNull (box init) then DOMRectInit.Empty else init

        createObj
            [
                for key, value in [ "x", init.X; "y", init.Y; "width", init.Width; "height", init.Height ] do
                    match value with
                    | Some number -> yield key ==> number
                    | None -> ()
            ]

    let private quadInput (init: DOMQuadInit) =
        let init = if isNull (box init) then DOMQuadInit.Empty else init

        createObj
            [
                for key, value in [ "p1", init.P1; "p2", init.P2; "p3", init.P3; "p4", init.P4 ] do
                    match value with
                    | Some point -> yield key ==> pointInput point
                    | None -> ()
            ]

    /// <summary>Creates a borrowed provider bound to the document's active Window geometry constructors.</summary>
    /// <param name="document">The live document supplying the construction realm.</param>
    /// <remarks>Existing geometry values remain usable after provider disposal. Navigation invalidates the old document's provider.</remarks>
    let forDocument (document: Document) : GeometryService =
        if isNull (box document) then
            nullArg (nameof document)

        if not (current document) || not (available document) then
            raise (NotSupportedException "Geometry values require an active document with geometry constructors")

        let mutable disposed = false

        let active () =
            if disposed then
                raise (ObjectDisposedException "GeometryService")

            if not (current document) then
                raise (InvalidOperationException "The geometry document is no longer active")

        let engine action =
            try
                action ()
            with error ->
                raise (GeometryException (errorName (box error), errorMessage (box error)))

        { new GeometryService with
            member _.CreateRect(?x, ?y, ?width, ?height) =
                active ()
                engine (fun () -> rect document (defaultArg x 0.) (defaultArg y 0.) (defaultArg width 0.) (defaultArg height 0.))

            member _.CreateReadOnlyRect(?x, ?y, ?width, ?height) =
                active ()
                engine (fun () -> readonlyRect document (defaultArg x 0.) (defaultArg y 0.) (defaultArg width 0.) (defaultArg height 0.))

            member _.RectFrom init =
                active ()
                engine (fun () -> rectFrom document (rectInput init))

            member _.ReadOnlyRectFrom init =
                active ()
                engine (fun () -> readonlyRectFrom document (rectInput init))

            member _.CreatePoint(?x, ?y, ?z, ?w) =
                active ()
                engine (fun () -> point document (defaultArg x 0.) (defaultArg y 0.) (defaultArg z 0.) (defaultArg w 1.))

            member _.CreateReadOnlyPoint(?x, ?y, ?z, ?w) =
                active ()
                engine (fun () -> readonlyPoint document (defaultArg x 0.) (defaultArg y 0.) (defaultArg z 0.) (defaultArg w 1.))

            member _.PointFrom init =
                active ()
                engine (fun () -> pointFrom document (pointInput init))

            member _.ReadOnlyPointFrom init =
                active ()
                engine (fun () -> readonlyPointFrom document (pointInput init))

            member _.CreateQuad(?p1, ?p2, ?p3, ?p4) =
                active ()

                engine (fun () ->
                    quad
                        document
                        (pointInput (defaultArg p1 Unchecked.defaultof<_>))
                        (pointInput (defaultArg p2 Unchecked.defaultof<_>))
                        (pointInput (defaultArg p3 Unchecked.defaultof<_>))
                        (pointInput (defaultArg p4 Unchecked.defaultof<_>)))

            member _.QuadFromRect init =
                active ()
                engine (fun () -> quadFromRect document (rectInput init))

            member _.QuadFrom init =
                active ()
                engine (fun () -> quadFrom document (quadInput init))

            member _.CreateMatrix() =
                active ()
                engine (fun () -> matrix document)

            member _.CreateMatrix(transformList: string) =
                active ()
                engine (fun () -> matrixInput document (box (if isNull transformList then "null" else transformList)))

            member _.CreateMatrix(values: float array) =
                active ()

                if isNull values then
                    nullArg (nameof values)

                engine (fun () -> matrixInput document (box values))

            member _.CreateReadOnlyMatrix() =
                active ()
                engine (fun () -> readonlyMatrix document)

            member _.CreateReadOnlyMatrix(transformList: string) =
                active ()
                engine (fun () -> readonlyMatrixInput document (box (if isNull transformList then "null" else transformList)))

            member _.CreateReadOnlyMatrix(values: float array) =
                active ()

                if isNull values then
                    nullArg (nameof values)

                engine (fun () -> readonlyMatrixInput document (box values))

            member _.MatrixFrom init =
                active ()
                engine (fun () -> matrixFrom document (matrixDictionary init))

            member _.ReadOnlyMatrixFrom init =
                active ()
                engine (fun () -> readonlyMatrixFrom document (matrixDictionary init))

            member _.Dispose() =
                disposed <- true
        }
