namespace Ranvier.Dom

/// <summary>Constructs actual Typed OM transform components and ordered transform lists in a borrowed document context.</summary>
/// <remarks>
/// Engine dimension checks and matrix mathematics are preserved. Disposal invalidates this provider without changing
/// returned objects; native objects require their live host session and owning thread. Missing list indexes return None.
/// </remarks>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type TransformValueService =
    inherit System.IDisposable

    /// <summary>Copies an ordered array of retained component identities into an engine transform list.</summary>
    abstract CreateList: components: CSSTransformComponent array -> CSSTransformValue

    /// <summary>Creates a 2D translation with the z argument omitted.</summary>
    abstract CreateTranslate: x: CSSNumericValue * y: CSSNumericValue -> CSSTranslate

    /// <summary>Creates a 3D translation with a required explicit z value; null is rejected before engine dispatch.</summary>
    abstract CreateTranslate: x: CSSNumericValue * y: CSSNumericValue * z: CSSNumericValue -> CSSTranslate

    /// <summary>Creates a 2D rotation from an angle value.</summary>
    abstract CreateRotate: angle: CSSNumericValue -> CSSRotate

    /// <summary>Creates a 3D axis rotation, preserving numeric reference arms and the engine's unit validation.</summary>
    abstract CreateRotate: x: DoubleOrCSSNumericValue * y: DoubleOrCSSNumericValue * z: DoubleOrCSSNumericValue * angle: CSSNumericValue -> CSSRotate

    /// <summary>Creates a 2D scale with the z argument omitted.</summary>
    abstract CreateScale: x: DoubleOrCSSNumericValue * y: DoubleOrCSSNumericValue -> CSSScale

    /// <summary>Creates a 3D scale with a required explicit z union; null containers and reference arms are rejected.</summary>
    abstract CreateScale: x: DoubleOrCSSNumericValue * y: DoubleOrCSSNumericValue * z: DoubleOrCSSNumericValue -> CSSScale

    /// <summary>Creates a skew from independent x and y angle values.</summary>
    abstract CreateSkew: ax: CSSNumericValue * ay: CSSNumericValue -> CSSSkew

    /// <summary>Creates an x-axis skew from an angle value.</summary>
    abstract CreateSkewX: ax: CSSNumericValue -> CSSSkewX

    /// <summary>Creates a y-axis skew from an angle value.</summary>
    abstract CreateSkewY: ay: CSSNumericValue -> CSSSkewY

    /// <summary>Creates a perspective from a numeric length or the engine's string/keyword none representation.</summary>
    abstract CreatePerspective: length: CSSNumericValueOrDOMStringOrCSSOMKeywordValue -> CSSPerspective

    /// <summary>Copies a geometry matrix into a component, preserving omitted versus explicit is2D options.</summary>
    abstract CreateMatrixComponent: matrix: DOMMatrixReadOnly * ?is2D: bool -> CSSMatrixComponent

    /// <summary>Returns the retained component at an index, or None for a missing indexed value.</summary>
    abstract Get: value: CSSTransformValue * index: uint32 -> CSSTransformComponent option
