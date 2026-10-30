namespace Ranvier.Dom

/// <summary>A snapshot entry preserving its property name, value order and actual typed value objects.</summary>
/// <remarks>The pinned engine permits null value elements. Changing the array does not update the map.</remarks>
type StyleMapEntry =
    {
        /// <summary>The property name emitted by the engine iterator.</summary>
        Property: string
        /// <summary>Ordered typed values, including nullable elements from the pinned engine.</summary>
        Values: CSSStyleValue array
    }

/// <summary>Acquires live typed style maps and snapshots their values using borrowed document context.</summary>
/// <remarks>
/// Disposing the provider leaves previously acquired maps and values unchanged. Native objects require their live
/// host session and owning thread. Cached native maps preserve their first acquisition document across providers.
/// Service engine failures use <see cref="T:Ranvier.Dom.StyleValueException"/>.
/// </remarks>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type StyleMapService =
    inherit System.IDisposable

    /// <summary>Returns the same-object mutable inline map of an HTML, SVG or MathML element.</summary>
    abstract Attribute: element: ElementCSSInlineStyle -> StylePropertyMap

    /// <summary>Returns a live read-only computed map for an element.</summary>
    abstract Computed: element: Element -> StylePropertyMapReadOnly

    /// <summary>Returns the same-object mutable map of a style rule.</summary>
    /// <remarks>Ownerless native rules use the explicit provider document on their first map acquisition.</remarks>
    abstract Declared: rule: CSSStyleRule -> StylePropertyMap

    /// <summary>Returns None only for the engine's undefined result, preserving actual typed values otherwise.</summary>
    abstract Get: map: StylePropertyMapReadOnly * property: string -> CSSStyleValue option

    /// <summary>Snapshots entries in engine iterator order, retaining nullable value slots.</summary>
    /// <remarks>Values remain engine objects; changing a returned value does not implicitly write it back to the map.</remarks>
    abstract Entries: map: StylePropertyMapReadOnly -> StyleMapEntry array
