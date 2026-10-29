namespace Ranvier.Dom

/// <summary>A typed CSS value engine error preserving its exception name and message.</summary>
exception StyleValueException of name: string * detail: string with
    override this.Message = this.name + ": " + this.detail

/// <summary>Parses and constructs actual CSS Typed OM values using one borrowed document context.</summary>
/// <remarks>
/// All string inputs use WebIDL USVString conversion: null becomes the text <c>null</c>, unmatched surrogates
/// become replacement characters and embedded NUL remains part of the input. Disposing the provider leaves
/// existing values unchanged. Native values belong to their host session and require its live owning thread.
/// </remarks>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type StyleValueService =
    inherit System.IDisposable

    /// <summary>Parses one property's CSS text and returns its first reified value.</summary>
    /// <remarks>Unselected subtypes retain their base CSSStyleValue identity and engine serialization.</remarks>
    abstract Parse: property: string * cssText: string -> CSSStyleValue

    /// <summary>Parses all reified values of a property, preserving their engine order and object identities.</summary>
    /// <remarks>Changing the returned array does not mutate its values or another parse result.</remarks>
    abstract ParseAll: property: string * cssText: string -> CSSStyleValue array

    /// <summary>Constructs a mutable keyword value using the engine's validation and serialization.</summary>
    abstract CreateKeyword: value: string -> CSSKeywordValue

    /// <summary>Constructs a finite numeric value in an engine-supported unit without exposing numeric math operations.</summary>
    abstract CreateUnit: value: float * unitName: string -> CSSUnitValue

    /// <summary>Constructs an unparsed value from ordered string and variable-reference segments.</summary>
    /// <remarks>Null arrays, union values and reference payloads are rejected. Null string payloads use USVString conversion.</remarks>
    abstract CreateUnparsed: segments: USVStringOrCSSOMVariableReferenceValue array -> CSSUnparsedValue

    /// <summary>Constructs a mutable variable reference with an optional existing unparsed fallback.</summary>
    /// <remarks>None and Some null both mean a null fallback. Native fallback values must belong to the same live host session.</remarks>
    abstract CreateVariableReference: variable: string * fallback: CSSUnparsedValue option -> CSSVariableReferenceValue
