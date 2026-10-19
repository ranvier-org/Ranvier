namespace Ranvier.View.Foundation

/// <summary>The receiver of the pending reads and failures of the bindings, rows and handlers mounted under it.</summary>
type internal ViewScope =
    /// <summary>True for a boundary: its bindings report pending reads through <c>Wait</c> and <c>Ready</c>.</summary>
    abstract Tracks: bool
    /// <summary>Records a binding whose read is pending.</summary>
    abstract Wait: unit -> unit
    /// <summary>Records that a waiting binding read a value or was disposed.</summary>
    abstract Ready: unit -> unit
    /// <summary>Takes failures in the order they occurred.</summary>
    abstract Fail: exn list -> unit

/// <summary>The bits of a binding's state word.</summary>
module internal BindingState =
    /// <summary>The binding's last read raised an exception other than a pending read.</summary>
    [<Literal>]
    let ReadFailed = 1
