namespace Ranvier.Dom

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

    /// <summary>Copies a canonical point dictionary, whose omitted fields use 0/0/0/1 defaults.</summary>
    abstract PointFrom: init: DOMPointInit -> DOMPoint

    /// <summary>Copies a canonical point dictionary into a read-only point, preserving its unrestricted doubles.</summary>
    abstract ReadOnlyPointFrom: init: DOMPointInit -> DOMPointReadOnly

    /// <summary>Copies four optional corner dictionaries into a quad containing live mutable points.</summary>
    abstract CreateQuad: ?p1: DOMPointInit * ?p2: DOMPointInit * ?p3: DOMPointInit * ?p4: DOMPointInit -> DOMQuad

    /// <summary>Constructs a quad whose corners preserve the rectangle's signed extents.</summary>
    abstract QuadFromRect: init: DOMRectInit -> DOMQuad

    /// <summary>Copies optional nested corner dictionaries into a quad containing live mutable points.</summary>
    abstract QuadFrom: init: DOMQuadInit -> DOMQuad

    /// <summary>Constructs an identity mutable matrix with the initializer omitted.</summary>
    abstract CreateMatrix: unit -> DOMMatrix

    /// <summary>Parses a transform string using the engine's geometry constructor.</summary>
    abstract CreateMatrix: transformList: string -> DOMMatrix

    /// <summary>Copies six or sixteen doubles into a mutable matrix; present empty arrays remain invalid inputs.</summary>
    abstract CreateMatrix: values: float array -> DOMMatrix

    /// <summary>Constructs an identity read-only matrix with the initializer omitted.</summary>
    abstract CreateReadOnlyMatrix: unit -> DOMMatrixReadOnly

    /// <summary>Parses a transform string into a read-only matrix.</summary>
    abstract CreateReadOnlyMatrix: transformList: string -> DOMMatrixReadOnly

    /// <summary>Copies six or sixteen doubles into a read-only matrix.</summary>
    abstract CreateReadOnlyMatrix: values: float array -> DOMMatrixReadOnly

    /// <summary>Copies a canonical matrix dictionary, preserving optional aliases and is2D for engine validation.</summary>
    abstract MatrixFrom: init: DOMMatrixInit -> DOMMatrix

    /// <summary>Copies a canonical matrix dictionary into a read-only matrix with engine alias validation.</summary>
    abstract ReadOnlyMatrixFrom: init: DOMMatrixInit -> DOMMatrixReadOnly
