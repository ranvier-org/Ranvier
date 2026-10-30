namespace Ranvier.Dom

/// <summary>Optional point coordinates for geometry factories, defaulting to x/y/z=0 and w=1.</summary>
/// <remarks>None omits a dictionary field. Present values preserve unrestricted doubles, including NaN and infinities.</remarks>
type DOMPointInit =
    {
        X: float option
        Y: float option
        Z: float option
        W: float option
    }

    /// <summary>Omits every point field and uses the engine's coordinate defaults.</summary>
    static member Empty =
        {
            X = None
            Y = None
            Z = None
            W = None
        }

/// <summary>Optional rectangle coordinates and signed extents, each defaulting to zero.</summary>
/// <remarks>None omits a dictionary field. Present values preserve unrestricted doubles.</remarks>
type DOMRectInit =
    {
        X: float option
        Y: float option
        Width: float option
        Height: float option
    }

    /// <summary>Omits every rectangle field and uses zero coordinates and extents.</summary>
    static member Empty =
        {
            X = None
            Y = None
            Width = None
            Height = None
        }

/// <summary>Optional quad corners, each defaulting to the point (0,0,0,1).</summary>
/// <remarks>Absent and null nested dictionaries use the engine's empty point dictionary defaults.</remarks>
type DOMQuadInit =
    {
        P1: DOMPointInit option
        P2: DOMPointInit option
        P3: DOMPointInit option
        P4: DOMPointInit option
    }

    /// <summary>Omits all corners and uses their engine defaults.</summary>
    static member Empty =
        {
            P1 = None
            P2 = None
            P3 = None
            P4 = None
        }

/// <summary>A geometry engine error preserving its exception name and message.</summary>
exception GeometryException of name: string * detail: string with
    override this.Message = this.name + ": " + this.detail

/// <summary>Constructs actual geometry values through a borrowed document context.</summary>
/// <remarks>
/// Geometry dictionaries are copied by the engine; null dictionaries use empty-dictionary defaults.
/// Disposal invalidates the provider and preserves existing values. Native values require their live host session and owning thread.
/// </remarks>
#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type GeometryService =
    inherit System.IDisposable

    /// <summary>Constructs a mutable rectangle with omitted coordinates and extents defaulting to zero.</summary>
    abstract CreateRect: ?x: float * ?y: float * ?width: float * ?height: float -> DOMRect

    /// <summary>Constructs a read-only rectangle with omitted coordinates and extents defaulting to zero.</summary>
    abstract CreateReadOnlyRect: ?x: float * ?y: float * ?width: float * ?height: float -> DOMRectReadOnly

    /// <summary>Copies optional dictionary coordinates and extents into a mutable rectangle.</summary>
    abstract RectFrom: init: DOMRectInit -> DOMRect

    /// <summary>Copies optional dictionary coordinates and extents into a read-only rectangle.</summary>
    abstract ReadOnlyRectFrom: init: DOMRectInit -> DOMRectReadOnly

    /// <summary>Constructs a mutable point with omitted x/y/z defaulting to zero and w defaulting to one.</summary>
    abstract CreatePoint: ?x: float * ?y: float * ?z: float * ?w: float -> DOMPoint

    /// <summary>Constructs a read-only point with omitted x/y/z defaulting to zero and w defaulting to one.</summary>
    abstract CreateReadOnlyPoint: ?x: float * ?y: float * ?z: float * ?w: float -> DOMPointReadOnly

    /// <summary>Copies optional dictionary coordinates into a mutable point.</summary>
    abstract PointFrom: init: DOMPointInit -> DOMPoint

    /// <summary>Copies optional dictionary coordinates into a read-only point.</summary>
    abstract ReadOnlyPointFrom: init: DOMPointInit -> DOMPointReadOnly

    /// <summary>Copies four optional corner dictionaries into a quad containing live mutable points.</summary>
    abstract CreateQuad: ?p1: DOMPointInit * ?p2: DOMPointInit * ?p3: DOMPointInit * ?p4: DOMPointInit -> DOMQuad

    /// <summary>Constructs a quad whose corners preserve the rectangle's signed extents.</summary>
    abstract QuadFromRect: init: DOMRectInit -> DOMQuad

    /// <summary>Copies optional nested corner dictionaries into a quad containing live mutable points.</summary>
    abstract QuadFrom: init: DOMQuadInit -> DOMQuad
