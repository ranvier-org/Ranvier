namespace Ranvier.Dom

/// <summary>Distinguishes an omitted initial-value descriptor from a supplied CSS string.</summary>
/// <remarks>
/// The explicit cases preserve presence when compiling to JavaScript; a string option can erase a present null.
/// A Value containing null converts to the WebIDL text <c>null</c>, while Value empty remains present and empty.
/// </remarks>
[<RequireQualifiedAccess>]
type CustomPropertyInitialValue =
    /// <summary>Omits the descriptor; only universal syntax permits registration without an initial value.</summary>
    | Omitted
    /// <summary>Supplies the CSS text for the initial value, including an empty or null string.</summary>
    | Value of string

/// <summary>A custom-property registration interpreted by the document's CSS engine.</summary>
type PropertyDefinition =
    {
        /// <summary>The custom property name, including its leading double hyphen.</summary>
        Name: string
        /// <summary>The accepted value grammar; the universal grammar is <c>*</c>.</summary>
        Syntax: string
        /// <summary>Whether the registered property inherits from its parent element.</summary>
        Inherits: bool
        /// <summary>The optional initial CSS value, with omission represented explicitly.</summary>
        InitialValue: CustomPropertyInitialValue
    }

    /// <summary>Creates a registration with universal syntax and no explicit initial value.</summary>
    /// <param name="name">The custom property name.</param>
    /// <param name="inherits">Whether the property inherits.</param>
    static member Create(name, inherits) =
        {
            Name = name
            Syntax = "*"
            Inherits = inherits
            InitialValue = CustomPropertyInitialValue.Omitted
        }

/// <summary>A custom-property engine error preserving its DOM exception name and message.</summary>
exception CustomPropertyException of name: string * detail: string with
    override this.Message = this.name + ": " + this.detail

/// <summary>Registers custom properties in one borrowed document context.</summary>
/// <remarks>
/// Disposal invalidates this provider without unregistering document definitions.
/// Native calls require a live owning host and its thread.
/// </remarks>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type CustomPropertyService =
    inherit System.IDisposable

    /// <summary>Registers the definition; duplicate or invalid definitions raise CustomPropertyException.</summary>
    /// <remarks>
    /// Null strings convert to the text <c>null</c>. Embedded NUL remains part of the input.
    /// Registrations persist for the document's lifetime.
    /// </remarks>
    abstract Register: definition: PropertyDefinition -> unit
