namespace Ranvier.Dom

/// <summary>Reads live, read-only computed CSS declarations in the owning document.</summary>
/// <remarks>The service borrows its document; native declarations remain valid while their host session is live.</remarks>
type ComputedStyleService =
    /// <summary>Returns resolved styles for an element or pseudo-element; unresolved pseudo-element selectors yield an empty declaration.</summary>
    /// <param name="element">An element belonging to the service's document.</param>
    /// <param name="pseudoElement">None or Some null/empty selects the element; otherwise uses getComputedStyle pseudo-element syntax.</param>
    /// <remarks>Later style changes update reads. Writing the returned declaration raises NoModificationAllowedError.</remarks>
    abstract Get: element: Element * pseudoElement: string option -> CSSStyleDeclaration
