namespace Ranvier.Dom

/// <summary>Evaluates CSS support using the owning document's parser and enabled features.</summary>
/// <remarks>The provider borrows its document context. Native calls require a live host on its owning thread.</remarks>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type CssQueryService =
    /// <summary>Tests whether the engine accepts a property and value; invalid declarations return false.</summary>
    /// <remarks>Null strings convert to the text <c>null</c>; embedded NUL characters remain part of the input.</remarks>
    abstract Supports: property: string * value: string -> bool

    /// <summary>Tests a CSS supports condition, including compound conditions and selector queries.</summary>
    /// <remarks>Invalid conditions return false. Null converts to the text <c>null</c>.</remarks>
    abstract Supports: conditionText: string -> bool
