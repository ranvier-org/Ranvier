module Ranvier.View.Foundation.Template

open System
open System.Text
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier
open Ranvier.View.Foundation.Representation

/// <summary>
/// Markup inserted into an element without escaping. Create it only from trusted or sanitised content. On the client,
/// script elements in the markup stay inert while inline event handler attributes and <c>javascript:</c> URLs run; in
/// server HTML the markup is part of the document, and every script, handler and URL in it runs.
/// </summary>
/// <remarks>The markup must be a well-formed fragment for the element that receives it.</remarks>
[<Sealed>]
type UnsafeHtml private (markup: string) =
    member _.Markup = markup

    static member Trust(markup: string) =
        if isNull markup then
            raise (ArgumentException ("Markup cannot be null", "markup"))

        UnsafeHtml markup

/// <summary>The markup of a raw-HTML slot value; a null value raises "An UnsafeHtml reader returned null".</summary>
let internal markupOf (value: obj) : string =
    if isNull value then
        raise (ArgumentException ("An UnsafeHtml reader returned null", "read"))

    (unbox<UnsafeHtml> value).Markup

/// <summary>Where a dynamic slot writes on its node.</summary>
[<NoEquality; NoComparison>]
type SlotTarget =
    /// <summary>An attribute written from a <c>string option</c>; <c>None</c> removes it.</summary>
    | AttributeTarget of string
    /// <summary>A property written by a shared writer.</summary>
    | PropertyTarget of Func<obj, obj, unit>
    /// <summary>The data of a text node.</summary>
    | TextTarget
    /// <summary>The element's children, parsed from an <c>UnsafeHtml</c> value.</summary>
    | ContentTarget

/// <summary>How a dynamic slot is re-evaluated.</summary>
type SlotMode =
    /// <summary>Within the instance's render effect.</summary>
    | RenderMode
    /// <summary>In its own effect.</summary>
    | IsolatedMode
    /// <summary>From one fixed source, evaluated in the render effect.</summary>
    | DirectMode

/// <summary>A dynamic part of a template: reads a value from the instance and writes it to a node.</summary>
[<NoEquality; NoComparison>]
type Slot =
    {
        Target: SlotTarget
        Mode: SlotMode
        Read: Func<obj, obj>
        Same: Func<obj, obj, bool>
    }

/// <summary>A node of a backend-neutral template tree.</summary>
[<NoEquality; NoComparison>]
type TemplateNode =
    /// <summary>An element: its tag, static attributes, static properties, slots, events, placed hooks and children.</summary>
    | ElementNode of
        Tag *
        (string * string) array *
        (Func<obj, obj, unit> * obj) array *
        Slot array *
        (string * Func<obj, obj, unit>) array *
        (PlacedKind * Func<obj, obj, (unit -> unit)>) array *
        TemplateNode array
    | TextNode of string
    | TextSlotNode of Slot
    | FragmentNode of TemplateNode array
    /// <summary>Dynamic structure mounted between a pair of region markers.</summary>
    | RegionNode of Region
    /// <summary>A child template inlined into this template's skeleton, with the reader of its props from the inlining instance.</summary>
    | ChildNode of TemplateComponent * Func<obj, obj>
    /// <summary>A document head entry; it occupies zero skeleton width.</summary>
    | HeadNode of HeadTemplate

/// <summary>Dynamic structure of a template: keyed items read from an instance, rendered one row per item.</summary>
and [<NoEquality; NoComparison>] Region =
    {
        /// <summary>Reads the items from an instance: an <c>ItemRow</c> region's row values, otherwise one boxed <c>(key, value)</c> pair.</summary>
        Items: Func<obj, obj array>
        Key: Func<obj, obj>
        Row: RegionRow
    }

/// <summary>What a region's item is and how its row renders it.</summary>
and [<NoEquality; NoComparison>] RegionRow =
    /// <summary>The item is a <c>Program.View</c>, read once per instance.</summary>
    | ViewRow
    /// <summary>The item is an <c>Arm</c>; the arm's definition receives a reader of the current arm's payload.</summary>
    | ArmRow
    /// <summary>The item is a row value; the conversion turns a reader of the current item into the definition's props.</summary>
    | ItemRow of TemplateComponent * Func<(unit -> obj), obj>
    /// <summary>The item is the region's <c>BoundaryViews</c>, read once per instance.</summary>
    | BoundaryRow

/// <summary>A document head entry of a template; its readers receive the instance.</summary>
and [<NoEquality; NoComparison>] HeadTemplate =
    {
        Kind: HeadKind
        /// <summary>The entry's key; null for an unkeyed link.</summary>
        Key: string
        Attributes: (string * string) array
        /// <summary>Dynamic attributes: the name and a reader returning <c>string option</c>.</summary>
        Slots: (string * Func<obj, obj>) array
        /// <summary>The reader of a title entry, returning <c>string</c>; null for a meta or link entry.</summary>
        Title: Func<obj, obj>
    }

/// <summary>A template component definition without its props type.</summary>
and [<AbstractClass>] TemplateComponent internal (setup: Func<Setup, obj, obj>, root: TemplateNode) =
    inherit TemplateDefinition()

    /// <summary>The definition's <c>ServerPlan</c>; null before the first server render.</summary>
    [<DefaultValue>]
    val mutable internal serverPlan: obj

    [<DefaultValue>]
    val mutable internal armReader: Func<(unit -> obj), obj>

    member internal _.Setup = setup
    member internal _.Root = root

    /// <summary>The arm reader shared by every arm of the definition; <c>null</c> until the definition first appears in an arm.</summary>
    member internal this.ArmReader = this.armReader

/// <summary>A component definition selected by a <c>matchWith</c> or <c>branch</c> arm, with the payload read by its props.</summary>
[<NoEquality; NoComparison>]
type Arm =
    internal
        {
            Definition: TemplateComponent
            Payload: obj
            /// <summary>Turns a reader of the current arm into the definition's props, a reader of the current payload.</summary>
            Reader: Func<(unit -> obj), obj>
        }

#if FABLE_COMPILER
[<Emit("$0 === $1")>]
let private strictEquals (left: obj) (right: obj) : bool = jsNative
#else
let private strictEquals (left: obj) (right: obj) =
    Object.Equals (left, right)
#endif

/// <summary>Compares primitive slot values: strings, booleans, numbers and options of them.</summary>
let primitiveSame = Func<obj, obj, bool>(fun left right -> strictEquals left right)

/// <summary>Compares raw-HTML slot values by their markup.</summary>
let private contentSame =
    Func<obj, obj, bool>(fun left right -> markupOf left = markupOf right)

let private innerHtml =
    Func<obj, obj, unit>(fun node markup -> (unbox<HTMLElement> node).innerHTML <- unbox<string> markup)

let private contentSlot (slots: Slot array) =
    slots
    |> Array.tryFind (fun slot ->
        match slot.Target with
        | ContentTarget -> true
        | _ -> false)

[<NoEquality; NoComparison>]
type private Flat =
    | Item of TemplateNode
    | Enter of TemplateComponent * Func<obj, obj>
    | Leave

let rec private flatten (nodes: TemplateNode array) : Flat list =
    [
        for node in nodes do
            match node with
            | FragmentNode children -> yield! flatten children
            | TextNode "" -> ()
            | ChildNode (definition, props) ->
                yield Enter (definition, props)
                yield! flatten [| definition.Root |]
                yield Leave
            | other -> yield Item other
    ]

let private isText node =
    match node with
    | TextNode _
    | TextSlotNode _ -> true
    | _ -> false

let private rawText tag =
    match name tag with
    | "script"
    | "style"
    | "iframe"
    | "noscript"
    | "xmp"
    | "noembed"
    | "noframes"
    | "plaintext" -> true
    | _ -> false

/// <summary>The number of skeleton sibling nodes occupied by a flattened node.</summary>
let private width node =
    match node with
    | RegionNode _ -> 2
    | _ -> 1

/// <summary>Renders a region row through a backend; the reader returns the row's current item.</summary>
let internal rowRenderer (row: RegionRow) (current: unit -> obj) (backend: Backend) : unit =
    match row with
    | ViewRow -> (unbox<Program.View>(current ())).Invoke backend
    | ArmRow ->
        let arm = unbox<Arm>(current ())
        backend.Template (arm.Definition, arm.Reader.Invoke current)
    | ItemRow (definition, props) -> backend.Template (definition, props.Invoke current)
    | BoundaryRow -> backend.Boundary (unbox<BoundaryViews>(current ()))

[<NoEquality; NoComparison>]
type InlinedChild =
    {
        Definition: TemplateComponent
        Props: Func<obj, obj>
        /// <summary>The part whose instance the props reader receives: 0 for the template, k for the k-th inlined child in document order.</summary>
        Owner: int
        /// <summary>The index of the child's first dynamic node.</summary>
        First: int
        /// <summary>The number of dynamic nodes of the child and its inlined descendants.</summary>
        Count: int
    }

/// <summary>The markup of a template's skeleton with its dynamic nodes and inlined children.</summary>
[<NoEquality; NoComparison>]
type SkeletonLayout =
    {
        Markup: string
        /// <summary>Each dynamic node in document order, with its child-index path from the skeleton's root fragment.</summary>
        Targets: (TemplateNode * int array) array
        /// <summary>The part owning each dynamic node: 0 for the template, k for the k-th inlined child in document order.</summary>
        Owners: int array
        RootCount: int
        /// <summary>The inlined children, at every depth, in document order.</summary>
        Children: InlinedChild array
        /// <summary>The document head entries, at every depth, with their owning part.</summary>
        Heads: (HeadTemplate * int) array
    }

type private WriterState() =
    member val Targets = ResizeArray<TemplateNode * int array>()
    member val Owners = ResizeArray<int>()
    member val Children = ResizeArray<InlinedChild>()
    member val Heads = ResizeArray<HeadTemplate * int>()
    member val Parts = ResizeArray<int>()
    member val Part = 0 with get, set

let private writeMarkup (append: string -> unit) (state: WriterState) (root: TemplateNode) =
    let target node (path: int list) =
        state.Targets.Add (node, Array.ofList (List.rev path))
        state.Owners.Add state.Part

    let enter (definition: TemplateComponent) (props: Func<obj, obj>) =
        state.Children.Add
            {
                InlinedChild.Definition = definition
                Props = props
                Owner = state.Part
                First = state.Targets.Count
                Count = 0
            }

        state.Parts.Add state.Part
        state.Part <- state.Children.Count

    let leave () =
        let index = state.Part - 1
        let child = state.Children[index]

        state.Children[index] <-
            { child with
                Count = state.Targets.Count - child.First
            }

        state.Part <- state.Parts[state.Parts.Count - 1]
        state.Parts.RemoveAt (state.Parts.Count - 1)

    let writeHead (head: HeadTemplate) =
        state.Heads.Add ((head, state.Part))

    let rec writeChildren (children: TemplateNode array) (path: int list) =
        let mutable index = 0
        let mutable previousText = false

        for flat in flatten children do
            match flat with
            | Enter (definition, props) -> enter definition props
            | Leave -> leave ()
            | Item (HeadNode head) -> writeHead head
            | Item child ->
                let text = isText child

                if text && previousText then
                    append "<!--!-->"
                    index <- index + 1

                writeNode child (index :: path)
                index <- index + width child
                previousText <- text

        index

    and writeNode node path =
        match node with
        | TextNode value -> append (Html.escape false value)
        | TextSlotNode _ ->
            target node path
            append " "
        | FragmentNode _
        | ChildNode _
        | HeadNode _ -> invalidOp "Fragments, inlined children and head entries are flattened before writing"
        | ElementNode (tag, attributes, properties, slots, events, placed, children) ->
            if
                rawText tag
                && (children.Length > 0 || name tag = "plaintext")
            then
                raise (InvalidOperationException "Raw-text elements require an explicit HTML serialization policy")

            if
                properties.Length > 0
                || slots.Length > 0
                || events.Length > 0
                || placed.Length > 0
            then
                target node path

            append ("<" + name tag)

            for key, value in attributes do
                append (" " + key + "=\"" + Html.escape true value + "\"")

            append ">"

            if not (isVoid tag) then
                writeChildren children path |> ignore
                append ("</" + name tag + ">")
        | RegionNode _ ->
            target node path
            append "<!--[--><!--]-->"

    writeChildren [| root |] []

/// <summary>The skeleton of a template: its markup, its dynamic nodes in document order with their owning parts, its root count and its inlined children.</summary>
/// <remarks>A path lists child indexes from the skeleton's root fragment. A region's path leads to its start marker.</remarks>
let skeletonLayout (root: TemplateNode) : SkeletonLayout =
    let output = StringBuilder ()
    let state = WriterState ()

    let rootCount = writeMarkup (fun value -> output.Append value |> ignore) state root

    {
        Markup = output.ToString ()
        Targets = state.Targets.ToArray ()
        Owners = state.Owners.ToArray ()
        RootCount = rootCount
        Children = state.Children.ToArray ()
        Heads = state.Heads.ToArray ()
    }

/// <summary>The skeleton markup of a template, the path of every dynamic node in document order, and the number of root nodes.</summary>
/// <remarks>A path lists child indexes from the skeleton's root fragment. A region's path leads to its start marker.</remarks>
let skeletonMarkup (root: TemplateNode) =
    let layout = skeletonLayout root
    layout.Markup, layout.Targets, layout.RootCount

/// <summary>A document head entry of a server plan; its readers receive the current part's instance.</summary>
[<NoEquality; NoComparison>]
type internal HeadPlan =
    {
        /// <summary>The entry's key; null for an unkeyed link.</summary>
        Key: string
        /// <summary>The marked start tag through the static attributes: closed for a title, open for a meta or link.</summary>
        Opening: string
        /// <summary>Dynamic attributes: the name and a reader returning <c>string option</c>.</summary>
        Slots: (string * Func<obj, obj>) array
        /// <summary>The reader of a title entry, returning <c>string</c>; null for a meta or link entry.</summary>
        Title: Func<obj, obj>
    }

/// <summary>One step of a definition's server plan, interpreted by <c>ServerWriter</c> against the current part.</summary>
[<NoEquality; NoComparison>]
type internal ServerOp =
    /// <summary>Markup written as it is.</summary>
    | Static of string
    /// <summary>A text slot's escaped value; with <c>leadingLineFeed</c>, a value starting with a line feed gets a second one.</summary>
    | TextOp of Slot * leadingLineFeed: bool
    /// <summary>An attribute slot: <c> name="value"</c> for <c>Some</c>; the attribute is omitted for <c>None</c>.</summary>
    | AttributeOp of string * Slot
    /// <summary>A raw-HTML slot's markup, written verbatim.</summary>
    | ContentOp of Slot
    /// <summary>A document head entry: collected under <c>Server.renderPage</c>; otherwise raises under <c>Reject</c> and is dropped under <c>Omit</c>.</summary>
    | HeadOp of HeadPlan
    /// <summary>A browser-only part: raises under <c>Reject</c>, dropped under <c>Omit</c>; the description names the element and the part.</summary>
    | BrowserOp of description: string
    /// <summary>A region, with its position among the regions of its owning part.</summary>
    | RegionOp of index: int * Region
    /// <summary>Sets up the inlined child <c>child</c>, which becomes the current part.</summary>
    | EnterOp of child: int
    /// <summary>Restores the enclosing part.</summary>
    | LeaveOp

/// <summary>A definition's server HTML as operations over its flattened template; no two <c>Static</c> operations are adjacent.</summary>
[<NoEquality; NoComparison>]
type internal ServerPlan =
    {
        Ops: ServerOp array
        /// <summary>The inlined children, at every depth, in document order; <c>EnterOp k</c> sets up the child at <c>k - 1</c>.</summary>
        Children: InlinedChild array
        /// <summary>Whether the plan has an inlined child or a region, whose position needs the instance's path.</summary>
        Positioned: bool
    }

let private placedPart kind =
    match kind with
    | PlacedKind.Mounted -> "mounted hook"
    | PlacedKind.Lent -> "lent hook"
    | PlacedKind.Widget -> "widget"

/// <summary>Elements whose first child text loses one leading line feed when parsed.</summary>
let private dropsLeadingLineFeed tagName =
    tagName = "pre"
    || tagName = "textarea"
    || tagName = "listing"

let private compilePlan (root: TemplateNode) : ServerPlan =
    let ops = ResizeArray<ServerOp>()
    let pending = StringBuilder ()
    let children = ResizeArray<InlinedChild>()
    let enclosing = ResizeArray<int>()
    // The number of regions compiled so far, per part.
    let regions = ResizeArray<int>([ 0 ])
    let mutable part = 0
    let mutable targets = 0

    let write (markup: string) =
        pending.Append markup |> ignore

    let emit op =
        if pending.Length > 0 then
            ops.Add (Static (pending.ToString ()))
            pending.Clear () |> ignore

        ops.Add op

    let rec compileChildren (nodes: TemplateNode array) (leading: bool) =
        let mutable first = leading
        let mutable previousText = false

        for flat in flatten nodes do
            match flat with
            | Enter (definition, props) ->
                children.Add
                    {
                        InlinedChild.Definition = definition
                        Props = props
                        Owner = part
                        First = targets
                        Count = 0
                    }

                enclosing.Add part
                part <- children.Count
                regions.Add 0
                emit (EnterOp part)
            | Leave ->
                let child = children[part - 1]

                children[part - 1] <-
                    { child with
                        Count = targets - child.First
                    }

                part <- enclosing[enclosing.Count - 1]
                enclosing.RemoveAt (enclosing.Count - 1)
                emit LeaveOp
            | Item (HeadNode head) ->
                for name, _ in head.Slots do
                    validateAttribute name

                emit (
                    HeadOp
                        {
                            Key = head.Key
                            Opening = HeadMarkup.opening head.Kind head.Key head.Attributes
                            Slots = head.Slots
                            Title = head.Title
                        }
                )
            | Item node ->
                let text = isText node

                if text && previousText then
                    write "<!--!-->"

                compileNode node first
                first <- false
                previousText <- text

    and compileNode node leading =
        match node with
        | TextNode value ->
            let escaped = Html.escape false value

            if leading && escaped.Length > 0 && escaped[0] = '\n' then
                write "\n"

            write escaped
        | TextSlotNode slot ->
            targets <- targets + 1
            emit (TextOp (slot, leading))
        | ElementNode (tag, attributes, properties, slots, events, placed, nodes) ->
            let tagName = name tag

            if rawText tag then
                raise (InvalidOperationException "Raw-text elements require an explicit HTML serialization policy")

            if
                properties.Length > 0
                || slots.Length > 0
                || events.Length > 0
                || placed.Length > 0
            then
                targets <- targets + 1

            for _ in properties do
                emit (BrowserOp ("<" + tagName + "> property"))

            for slot in slots do
                match slot.Target with
                | PropertyTarget _ -> emit (BrowserOp ("<" + tagName + "> property"))
                | _ -> ()

            for eventName, _ in events do
                emit (BrowserOp ("<" + tagName + "> listener " + eventName))

            for kind, _ in placed do
                emit (BrowserOp ("<" + tagName + "> " + placedPart kind))

            write ("<" + tagName)

            for key, value in attributes do
                write (" " + key + "=\"" + Html.escape true value + "\"")

            for slot in slots do
                match slot.Target with
                | AttributeTarget key -> emit (AttributeOp (key, slot))
                | _ -> ()

            write ">"

            if not (isVoid tag) then
                match contentSlot slots with
                | Some content -> emit (ContentOp content)
                | None -> compileChildren nodes (dropsLeadingLineFeed tagName)

                write ("</" + tagName + ">")
        | RegionNode region ->
            targets <- targets + 1
            emit (RegionOp (regions[part], region))
            regions[part] <- regions[part] + 1
        | FragmentNode _
        | ChildNode _
        | HeadNode _ -> invalidOp "Fragments, inlined children and head entries are flattened before compiling"

    compileChildren [| root |] false

    if pending.Length > 0 then
        ops.Add (Static (pending.ToString ()))

    {
        Ops = ops.ToArray ()
        Children = children.ToArray ()
        Positioned =
            children.Count > 0
            || ops
               |> Seq.exists (fun op ->
                   match op with
                   | RegionOp _ -> true
                   | _ -> false)
    }

/// <summary>The definition's server plan, compiled on first use; every later call returns the same plan.</summary>
let internal serverPlan (definition: TemplateComponent) : ServerPlan =
    match definition.serverPlan with
    | null ->
        let plan = compilePlan definition.Root
#if FABLE_COMPILER
        definition.serverPlan <- plan
        plan
#else
        match Threading.Interlocked.CompareExchange (&definition.serverPlan, box plan, null) with
        | null -> plan
        | published -> unbox published
#endif
    | plan -> unbox plan

/// <summary>
/// Whether an HTML parser can produce a comment from <c>markup</c> whose data starts with <c>[</c> or equals <c>]</c>,
/// in any parsing context.
/// </summary>
/// <remarks>
/// Covers <c>&lt;!--</c> comments closed by <c>--&gt;</c>, <c>--!&gt;</c> or the end of input, and the bogus comments
/// opened by <c>&lt;!</c> (including <c>&lt;![CDATA[</c> in HTML content) and by <c>&lt;/</c> followed by a
/// non-letter. Markup inside attribute values, raw-text elements and foreign <c>CDATA</c> is also matched.
/// </remarks>
let private hasMarkerComment (markup: string) =
    let at (index: int) (value: string) =
        let mutable same = index + value.Length <= markup.Length
        let mutable offset = 0

        while same && offset < value.Length do
            same <- markup[index + offset] = value[offset]
            offset <- offset + 1

        same

    let rest (index: int) =
        markup.Substring index

    // The data "]" is complete when the comment closes or the input ends inside the comment's closing sequence.
    let commentCloses (tail: string) =
        "--!".StartsWith(tail, StringComparison.Ordinal)
        || tail.StartsWith ("-->", StringComparison.Ordinal)
        || tail.StartsWith ("--!>", StringComparison.Ordinal)

    let bogusCloses (tail: string) =
        tail.Length = 0 || tail[0] = '>'

    let mutable found = false
    let mutable index = markup.IndexOf '<'

    while not found && index >= 0 do
        found <-
            at index "<!--["
            || at index "<!["
            || at index "</["
            || (at index "<!--]"
                && commentCloses (rest (index + 5)))
            || ((at index "<!]" || at index "</]")
                && bogusCloses (rest (index + 3)))

        index <- markup.IndexOf ('<', index + 1)

    found

/// <summary>The owners of instances set up by a server writer.</summary>
[<AbstractClass>]
type internal ServerScope() =
    /// <summary>Runs the body with the scope and <c>Setup</c> of a new nested instance.</summary>
    abstract Nest: (ServerScope -> Setup -> unit) -> unit

    /// <summary>Runs the body in a new nested scope; when it raises, that scope's cleanups run before the exception propagates.</summary>
    abstract Attempt: (ServerScope -> unit) -> unit

    /// <summary>Runs the body with the scope and <c>Setup</c> of an inlined child of the instance set up with <c>parent</c>.</summary>
    abstract Inline: parent: Setup * body: (ServerScope -> Setup -> unit) -> unit

    default this.Inline(_, body) =
        this.Nest body

/// <summary>
/// The scope of <c>Html.render</c>: an instance's cleanups run after its markup is written, and an inlined child's run
/// with its parent's.
/// </summary>
[<Sealed>]
type internal HtmlScope(backend: HtmlBackend) =
    inherit ServerScope()

    override this.Nest body =
        backend.ComponentWith ((fun setup -> body this setup), (fun () -> backend.Identity.CreateId ()))

    override this.Inline(parent, body) =
        body this parent

    override this.Attempt body = body this

/// <summary>Writes template instances to a backend from their definitions' server plans.</summary>
/// <remarks>
/// Each instance is written at a position, one step below a parent path (see <c>InstanceStep</c>), and its setup runs in
/// the identity window of that position.
/// </remarks>
[<Sealed>]
type internal ServerWriter(backend: HtmlBackend) =
    let reject = backend.Policy = Reject
    // The position the next template reached from authored content takes, while `pending` is set.
    let mutable pending = false
    let mutable pendingParent: InstancePath = null
    let mutable pendingKind = 0
    let mutable pendingA = 0
    let mutable pendingB = 0
    // The templates reached from authored content at the authored step under `backend.Path`.
    let mutable authored = 0

    /// <summary>Runs <c>body</c> with <c>scope</c> as the backend's active scope.</summary>
    let under (scope: ServerScope) (body: unit -> unit) =
        let outer = backend.Scope
        backend.Scope <- scope

        try
            body ()
        finally
            backend.Scope <- outer

    /// <summary>Sets the position the next template reached from authored content takes.</summary>
    member _.Pend(parent: InstancePath, kind: int, a: int, b: int) =
        pending <- true
        pendingParent <- parent
        pendingKind <- kind
        pendingA <- a
        pendingB <- b

    /// <summary>
    /// Writes an instance reached from authored content at the pending position, or, when none is pending, at the next
    /// authored step under the backend's <c>Path</c>.
    /// </summary>
    member this.Embed(scope: ServerScope, definition: TemplateComponent, props: obj) : unit =
        if pending then
            pending <- false
            this.Write (scope, definition, props, pendingParent, pendingKind, pendingA, pendingB)
        else
            let n = authored
            authored <- n + 1
            this.Write (scope, definition, props, backend.Path, InstanceStep.Authored, n, 0)

    /// <summary>
    /// Runs <c>render</c> under <c>scope</c> as the view of row <c>row</c> of region <c>region</c> under <c>parent</c>:
    /// the first template it reaches takes the row's position, and its authored content sits under the row's path.
    /// </summary>
    member _.View(scope: ServerScope, render: Backend -> unit, parent: InstancePath, region: int, row: int) : unit =
        let outerPending = pending
        let outerParent = pendingParent
        let outerKind = pendingKind
        let outerA = pendingA
        let outerB = pendingB
        let outerAuthored = authored
        let outerPath = backend.Path
        pending <- true
        pendingParent <- parent
        pendingKind <- InstanceStep.Row
        pendingA <- region
        pendingB <- row
        authored <- 0
        backend.Path <- parent.Row (region, row)

        try
            under scope (fun () -> render backend)
        finally
            pending <- outerPending
            pendingParent <- outerParent
            pendingKind <- outerKind
            pendingA <- outerA
            pendingB <- outerB
            authored <- outerAuthored
            backend.Path <- outerPath

    /// <summary>
    /// Sets up an instance of <c>definition</c> in <c>scope.Nest</c>, at the position one step of <c>kind</c> below
    /// <c>parent</c>, and writes it.
    /// </summary>
    /// <remarks>
    /// The instance and each inlined child are groups of the backend's innermost open head container, entered in
    /// document order; each region is a child container of it, closed in region order.
    /// </remarks>
    member this.Write(scope: ServerScope, definition: TemplateComponent, props: obj, parent: InstancePath, kind: int, a: int, b: int) : unit =
        match backend.Heads with
        | null -> this.Instance (scope, definition, props, parent, kind, a, b)
        | heads ->
            let outer = heads.EnterGroup ()

            try
                this.Instance (scope, definition, props, parent, kind, a, b)
            finally
                heads.LeaveGroup outer

    member private this.Instance
        (scope: ServerScope, definition: TemplateComponent, props: obj, parent: InstancePath, kind: int, a: int, b: int)
        : unit =
        let plan = serverPlan definition

        scope.Nest (fun scope setup ->
            let identity = backend.Identity
            let parts = Array.zeroCreate<obj>(plan.Children.Length + 1)
            let mutable path: InstancePath = null
            identity.Enter (parent, kind, a, b)

            try
                parts[0] <- definition.Setup.Invoke (setup, props)

                if plan.Positioned then
                    path <- identity.Path ()
            finally
                identity.Leave ()

            this.Run (scope, setup, plan, parts, path, 0, 0)
            |> ignore)

    /// <summary>
    /// Interprets the plan from <c>start</c> against <c>part</c> until its <c>LeaveOp</c> or the end; returns the index
    /// after it. <c>path</c> is the instance's path, null for a plan that is not <c>Positioned</c>.
    /// </summary>
    member private this.Run(scope: ServerScope, setup: Setup, plan: ServerPlan, parts: obj array, path: InstancePath, part: int, start: int) : int =
        let ops = plan.Ops
        let instance = parts[part]
        let mutable partPath: InstancePath = null
        let mutable index = start
        let mutable running = true

        while running && index < ops.Length do
            match ops[index] with
            | Static markup ->
                backend.Append markup
                index <- index + 1
            | TextOp (slot, leadingLineFeed) ->
                let value = unbox<string>(slot.Read.Invoke instance)

                if not (isNull value) then
                    if
                        leadingLineFeed
                        && value.Length > 0
                        && value[0] = '\n'
                    then
                        backend.Append "\n"

                    backend.Append (Html.escape false value)

                index <- index + 1
            | AttributeOp (key, slot) ->
                match unbox<string option>(slot.Read.Invoke instance) with
                | Some value -> backend.Append (" " + key + "=\"" + Html.escape true value + "\"")
                | None -> ()

                index <- index + 1
            | ContentOp slot ->
                let markup = markupOf (slot.Read.Invoke instance)

                if hasMarkerComment markup then
                    raise (InvalidOperationException "Raw HTML cannot contain a comment that starts with [ or equals ]")

                backend.Append markup
                index <- index + 1
            | HeadOp head ->
                match backend.Heads with
                | null ->
                    if reject then
                        raise (InvalidOperationException "Document metadata requires Server.renderPage")
                | heads ->
                    let markup =
                        if isNull head.Title then
                            let parts = Array.zeroCreate<string>(head.Slots.Length + 2)
                            parts[0] <- head.Opening

                            for slot in 0 .. head.Slots.Length - 1 do
                                let name, read = head.Slots[slot]
                                parts[slot + 1] <- HeadMarkup.attribute name (unbox<string option>(read.Invoke instance))

                            parts[parts.Length - 1] <- ">"
                            String.Join ("", parts)
                        else
                            HeadMarkup.title head.Opening (unbox<string>(head.Title.Invoke instance))

                    heads.Add (head.Key, markup)

                index <- index + 1
            | BrowserOp _ ->
                if reject then
                    raise (InvalidOperationException "Browser capability requires explicit Omit policy")

                index <- index + 1
            | RegionOp (regionIndex, region) ->
                if isNull partPath then
                    partPath <- if part = 0 then path else path.Child (part - 1)

                match backend.Heads with
                | null -> this.Region (scope, region, instance, partPath, regionIndex)
                | heads ->
                    heads.Open ()

                    try
                        this.Region (scope, region, instance, partPath, regionIndex)
                    finally
                        heads.Close ()

                index <- index + 1
            | EnterOp child ->
                let inlined = plan.Children[child - 1]
                let props = inlined.Props.Invoke parts[inlined.Owner]
                let resume = index + 1
                let next = ref resume

                let write (scope: ServerScope) (childSetup: Setup) =
                    let identity = backend.Identity
                    identity.Enter (path, InstanceStep.Child, child - 1, 0)

                    try
                        parts[child] <- inlined.Definition.Setup.Invoke (childSetup, props)
                    finally
                        identity.Leave ()

                    next.Value <- this.Run (scope, childSetup, plan, parts, path, child, resume)

                scope.Inline (
                    setup,
                    fun scope childSetup ->
                        match backend.Heads with
                        | null -> write scope childSetup
                        | heads ->
                            let outer = heads.EnterGroup ()

                            try
                                write scope childSetup
                            finally
                                heads.LeaveGroup outer
                )

                index <- next.Value
            | LeaveOp ->
                running <- false
                index <- index + 1

        index

    /// <summary>Writes a region whose rows sit under <c>parent</c>, the path of its owning part, at index <c>regionIndex</c>.</summary>
    member private this.Region(scope: ServerScope, region: Region, instance: obj, parent: InstancePath, regionIndex: int) =
        let items = Array.copy (region.Items.Invoke instance)
        Projection.ensureUniqueKeys region.Key.Invoke items

        match region.Row with
        | BoundaryRow -> under scope (fun () -> backend.Boundary (unbox<BoundaryViews>(KeyedItems.value items[0])))
        | ViewRow ->
            backend.Append "<!--[-->"

            for row in 0 .. items.Length - 1 do
                let view = unbox<Program.View>(KeyedItems.value items[row])
                this.View (scope, (fun target -> view.Invoke target), parent, regionIndex, row)

            backend.Append "<!--]-->"
        | ArmRow ->
            backend.Append "<!--[-->"

            for row in 0 .. items.Length - 1 do
                let value = KeyedItems.value items[row]
                let arm = unbox<Arm> value
                this.Write (scope, arm.Definition, arm.Reader.Invoke (fun () -> value), parent, InstanceStep.Row, regionIndex, row)

            backend.Append "<!--]-->"
        | ItemRow (definition, props) ->
            backend.Append "<!--[-->"

            for row in 0 .. items.Length - 1 do
                let item = items[row]
                this.Write (scope, definition, props.Invoke (fun () -> item), parent, InstanceStep.Row, regionIndex, row)

            backend.Append "<!--]-->"

/// <summary>The head entry of <c>head</c> for <c>instance</c>.</summary>
let internal headEntry (head: HeadTemplate) (instance: obj) : HeadEntry =
    {
        HeadEntry.Kind = head.Kind
        Key = head.Key
        Attributes = head.Attributes
        Readers =
            head.Slots
            |> Array.map (fun (name, read) -> name, (fun () -> unbox<string option>(read.Invoke instance)))
        Title =
            if isNull head.Title then
                Unchecked.defaultof<_>
            else
                fun () ->
                    let value = unbox<string>(head.Title.Invoke instance)
                    if isNull value then "" else value
    }

let rec private expandNode (ops: Operations<'view>) (instance: obj) (node: TemplateNode) : 'view =
    match node with
    | TextNode value -> ops.Text value
    | TextSlotNode slot -> ops.Bind (fun left right -> slot.Same.Invoke (box left, box right)) (fun () -> unbox<string>(slot.Read.Invoke instance))
    | FragmentNode children -> ops.Fragment (Array.map (expandNode ops instance) children)
    | ElementNode (tag, attributes, properties, slots, events, placed, children) ->
        let modifiers: ModifierAction array =
            [|
                for key, value in attributes -> Attribute (key, value)
                for write, value in properties -> fun (backend: Backend) -> backend.Property (fun target -> write.Invoke (target, value))
                for slot in slots ->
                    match slot.Target with
                    | AttributeTarget key -> ReactiveAttribute (key, fun () -> unbox<string option>(slot.Read.Invoke instance))
                    | PropertyTarget write -> fun (backend: Backend) -> backend.ReactiveProperty ((fun () -> slot.Read.Invoke instance), write)
                    | ContentTarget ->
                        fun (backend: Backend) -> backend.ReactiveProperty ((fun () -> box (markupOf (slot.Read.Invoke instance))), innerHtml)
                    | TextTarget -> invalidOp "A text slot is a child node"
                for eventName, handler in events -> listenModifier eventName (fun event -> handler.Invoke (instance, event))
                for kind, hook in placed -> fun (backend: Backend) -> backend.Placed (kind, (fun node -> hook.Invoke (instance, node)))
            |]

        ops.Element tag modifiers (Array.map (expandNode ops instance) children)
    | RegionNode region ->
        match region.Row with
        | ViewRow
        | BoundaryRow ->
            let item = KeyedItems.value ((region.Items.Invoke instance)[0])
            expandRow ops region.Row (fun () -> item)
        | ArmRow -> ops.Switch (fun () -> Array.map unbox<obj * obj> (region.Items.Invoke instance)) (expandRow ops region.Row)
        | ItemRow _ ->
            ops.Keyed
                {
                    Read = fun () -> region.Items.Invoke instance
                    Key = region.Key
                }
                (expandRow ops region.Row)
    | ChildNode (definition, props) -> expandWith ops definition (props.Invoke instance)
    | HeadNode head -> ops.Embed (fun backend -> backend.Head (headEntry head instance))

/// <summary>Expands a region row through the operations; the reader returns the row's current item.</summary>
and private expandRow (ops: Operations<'view>) (row: RegionRow) (current: unit -> obj) : 'view =
    match row with
    | ViewRow
    | BoundaryRow -> ops.Embed (rowRenderer row current)
    | ArmRow ->
        let arm = unbox<Arm>(current ())
        expandWith ops arm.Definition (arm.Reader.Invoke current)
    | ItemRow (definition, props) -> expandWith ops definition (props.Invoke current)

/// <summary>Expands a definition into any view representation through its operations.</summary>
and expandWith (ops: Operations<'view>) (definition: TemplateComponent) (props: obj) : 'view =
    ops.Component (fun context -> expandNode ops (definition.Setup.Invoke (context, props)) definition.Root)

/// <summary>A component whose template is built once per definition; setup runs per instance.</summary>
[<Sealed>]
type Component<'props> internal (setup: Func<Setup, obj, obj>, root: TemplateNode) =
    inherit TemplateComponent(setup, root)

    override this.Expand(backend, props) =
        Program.run (expandWith Program.ops (this :> TemplateComponent) props) backend

    override this.RenderHtml(backend, props) =
        let writer =
            match backend.Writer with
            | null ->
                // The first template of an Html.render backend is its root instance.
                let writer = ServerWriter backend
                writer.Pend (null, InstanceStep.Root, 0, 0)
                backend.Writer <- writer
                writer
            | writer -> unbox<ServerWriter> writer

        let scope =
            match backend.Scope with
            | null ->
                let scope = HtmlScope backend
                backend.Scope <- scope
                scope :> ServerScope
            | scope -> unbox<ServerScope> scope

        writer.Embed (scope, this, props)

/// <summary>One part of an element in a template for instances of type <c>'inst</c>.</summary>
[<NoEquality; NoComparison>]
type Part<'native, 'inst> =
    internal
    | StaticAttribute of string * string
    | StaticProperty of Func<obj, obj, unit> * obj
    | DynamicPart of Slot
    | EventPart of string * Func<obj, obj, unit>
    | PlacedPart of PlacedKind * Func<obj, obj, (unit -> unit)>
    | NoPart

/// <summary>A template subtree for instances of type <c>'inst</c>.</summary>
[<NoEquality; NoComparison>]
type TemplateView<'inst> = internal TemplateView of TemplateNode

/// <summary>A reader of one fixed signal or memo of an instance.</summary>
[<NoEquality; NoComparison>]
type Source<'inst, 'value> = internal Source of Func<obj, obj>

let private slot target mode (read: 'inst -> 'value) same =
    {
        Slot.Target = target
        Mode = mode
        Read = Func<obj, obj>(fun instance -> box (read (unbox instance)))
        Same = same
    }

let private sourceSlot target (Source read: Source<'inst, 'value>) same =
    {
        Slot.Target = target
        Mode = DirectMode
        Read = read
        Same = same
    }

let private writer (write: 'native -> 'value -> unit) =
    Func<obj, obj, unit>(fun node value -> write (unbox node) (unbox value))

let private nodeOf (TemplateView node) = node

let private elementNode tag (parts: Part<'native, 'inst> array) (children: TemplateView<'inst> array) =
    name tag |> ignore
    let attributes = ResizeArray ()
    let properties = ResizeArray ()
    let slots = ResizeArray ()
    let events = ResizeArray ()
    let placed = ResizeArray ()

    for part in parts do
        match part with
        | StaticAttribute (name, value) ->
            validateAttribute name
            attributes.Add (name, value)
        | StaticProperty (write, value) -> properties.Add (write, value)
        | DynamicPart slot -> slots.Add slot
        | EventPart (name, handler) -> events.Add (name, handler)
        | PlacedPart (kind, hook) -> placed.Add (kind, hook)
        | NoPart -> ()

    let rawContents =
        slots
        |> Seq.filter (fun slot ->
            match slot.Target with
            | ContentTarget -> true
            | _ -> false)
        |> Seq.length

    let contents =
        rawContents
        + (placed
           |> Seq.filter (fun (kind, _) -> kind = PlacedKind.Widget)
           |> Seq.length)

    if contents > 1 then
        raise (ArgumentException ("An element takes at most one content part", "parts"))

    if contents = 1 && children.Length > 0 then
        raise (ArgumentException ("An element with a content part has no template children", "children"))

    if
        rawContents = 1
        && (isVoid tag
            || rawText tag
            || (match name tag with
                | "template"
                | "textarea"
                | "title" -> true
                | _ -> false))
    then
        raise (ArgumentException ("Raw HTML requires an element whose children the HTML parser builds from markup", "parts"))

    TemplateView (
        ElementNode (
            tag,
            attributes.ToArray (),
            properties.ToArray (),
            slots.ToArray (),
            events.ToArray (),
            placed.ToArray (),
            Array.map nodeOf children
        )
    )

/// <summary>Part constructors used by the generated template catalogue.</summary>
module internal TemplateParts =
    let element tag (parts: Part<'native, 'inst> array) (children: TemplateView<'inst> array) : TemplateView<'inst> =
        elementNode tag parts children

    let attribute name (value: string) : Part<'native, 'inst> =
        validateAttribute name
        StaticAttribute (name, value)

    let optionalAttribute name (value: string option) : Part<'native, 'inst> =
        validateAttribute name

        match value with
        | Some value -> StaticAttribute (name, value)
        | None -> NoPart

    let readAttribute name (read: 'inst -> string option) : Part<'native, 'inst> =
        validateAttribute name
        DynamicPart (slot (AttributeTarget name) RenderMode read primitiveSame)

    let sourceAttribute name (source: Source<'inst, string option>) : Part<'native, 'inst> =
        validateAttribute name
        DynamicPart (sourceSlot (AttributeTarget name) source primitiveSame)

    let mapSource (map: 'value -> 'next) (Source read: Source<'inst, 'value>) : Source<'inst, 'next> =
        Source (Func<obj, obj>(fun instance -> box (map (unbox<'value>(read.Invoke instance)))))

    /// <summary>Wraps a string as an attribute value; <c>null</c> gives <c>None</c>.</summary>
    let present (value: string) =
        if isNull value then None else Some value

    let presence enabled =
        if enabled then Some "" else None

    let ariaText enabled =
        if enabled then "true" else "false"

    let ariaOption (value: bool option) =
        Option.map ariaText value

    let property (write: 'native -> 'value -> unit) (value: 'value) : Part<'native, 'inst> =
        StaticProperty (writer write, box value)

    let readProperty (write: 'native -> 'value -> unit) (read: 'inst -> 'value) : Part<'native, 'inst> =
        DynamicPart (slot (PropertyTarget (writer write)) RenderMode read primitiveSame)

    let sourceProperty (write: 'native -> 'value -> unit) (source: Source<'inst, 'value>) : Part<'native, 'inst> =
        DynamicPart (sourceSlot (PropertyTarget (writer write)) source primitiveSame)

    /// <summary>
    /// An event part. The handler runs unbatched: a handler that groups several writes wraps them in <c>Api.batch</c>,
    /// and effects then run once, after the outermost batch.
    /// </summary>
    let event name (handler: 'inst -> 'event -> unit) : Part<'native, 'inst> =
        EventPart (name, Func<obj, obj, unit>(fun instance event -> handler (unbox instance) (unbox event)))

/// <summary>Attribute parts: a value is static, a reader or source is dynamic.</summary>
[<Sealed>]
type AttributeBuilder<'inst> internal () =
    member _.attribute(name: string, value: string) : Part<'native, 'inst> =
        TemplateParts.attribute name value

    member _.attribute(name: string, read: 'inst -> string option) : Part<'native, 'inst> =
        TemplateParts.readAttribute name read

    member _.attribute(name: string, source: Source<'inst, string option>) : Part<'native, 'inst> =
        TemplateParts.sourceAttribute name source

/// <summary>Property parts: a value is written once per instance, a reader or source is dynamic.</summary>
/// <remarks>A writer sets properties and leaves the element's child nodes intact: nodes moved into a template clone by a writer bypass the mount's ownership checks.</remarks>
[<Sealed>]
type PropertyBuilder<'inst> internal () =
    member _.propertyStatic(write: 'native -> 'value -> unit, value: 'value) : Part<'native, 'inst> =
        TemplateParts.property write value

    member _.propertyWith(write: 'native -> 'value -> unit, read: 'inst -> 'value, same: Func<obj, obj, bool>) : Part<'native, 'inst> =
        DynamicPart (slot (PropertyTarget (writer write)) RenderMode read same)

    member _.propertyFrom(write: 'native -> 'value -> unit, source: Source<'inst, 'value>, same: Func<obj, obj, bool>) : Part<'native, 'inst> =
        DynamicPart (sourceSlot (PropertyTarget (writer write)) source same)

/// <summary>Event parts: one shared handler per template, given the instance and the event.</summary>
[<Sealed>]
type EventBuilder<'inst> internal () =
    /// <summary>
    /// Handles <c>eventName</c>. The handler runs unbatched: a handler that groups several writes wraps them in
    /// <c>Api.batch</c>, and effects then run once, after the outermost batch.
    /// </summary>
    member _.on(eventName: string, handler: 'inst -> Event -> unit) : Part<'native, 'inst> =
        TemplateParts.event eventName handler

let private placedHook (hook: 'inst -> 'native -> (unit -> unit)) =
    Func<obj, obj, (unit -> unit)>(fun instance node -> hook (unbox instance) (unbox node))

/// <summary>Hook parts: code that runs once the element is placed in the mount's host.</summary>
[<Sealed>]
type HookBuilder<'inst> internal () =
    /// <summary>Runs once the element is placed; the returned cleanup runs when the instance is disposed, before its nodes leave.</summary>
    member _.mounted(hook: 'inst -> 'native -> (unit -> unit)) : Part<'native, 'inst> =
        PlacedPart (PlacedKind.Mounted, placedHook hook)

    /// <summary>
    /// Sets the <c>NodeRef</c> selected from the instance to the element while the element is placed, and to <c>None</c>
    /// once the instance is disposed.
    /// </summary>
    member _.ref(select: 'inst -> NodeRef<'native>) : Part<'native, 'inst> =
        PlacedPart (PlacedKind.Mounted, Func<obj, obj, (unit -> unit)>(fun instance node -> (select (unbox instance)).Place node))

    /// <summary>The widget owns the element's content once placed, and the template declares the element empty. The cleanup destroys the widget.</summary>
    member _.widget(attach: 'inst -> 'native -> (unit -> unit)) : Part<'native, 'inst> =
        PlacedPart (PlacedKind.Widget, placedHook attach)

    /// <summary>The integration borrows the element once placed; the cleanup removes what it added. The integration keeps the element under its parent; moving it is reported after the cleanup.</summary>
    member _.lend(attach: 'inst -> 'native -> (unit -> unit)) : Part<'native, 'inst> =
        PlacedPart (PlacedKind.Lent, placedHook attach)

/// <summary>Raw-HTML parts: the element's whole content, parsed without escaping.</summary>
[<Sealed>]
type UnsafeBuilder<'inst> internal () =
    /// <summary>
    /// The element's content, parsed as HTML without escaping. The template declares the element empty. Scripts,
    /// handlers and URLs in the markup run as <c>UnsafeHtml</c> states for the client and for server HTML.
    /// </summary>
    member _.html(content: UnsafeHtml) : Part<'native, 'inst> =
        DynamicPart
            {
                Slot.Target = ContentTarget
                Mode = RenderMode
                Read = Func<obj, obj>(fun _ -> box content)
                Same = contentSame
            }

    /// <summary>
    /// The element's content, parsed as HTML without escaping and parsed again when the reader returns different
    /// markup. Scripts, handlers and URLs in the markup run as <c>UnsafeHtml</c> states for the client and for server HTML.
    /// </summary>
    member _.html(read: 'inst -> UnsafeHtml) : Part<'native, 'inst> =
        DynamicPart (slot ContentTarget RenderMode read contentSame)

let private metaKeys = [| "name"; "property"; "http-equiv"; "charset" |]

let private linkKeys = [| "rel" |]

/// <summary>The attribute holding a head element's registry key.</summary>
let private headMark = "data-ranvier-head"

let private rejectMark (name: string) =
    if String.Equals (name, headMark, StringComparison.OrdinalIgnoreCase) then
        raise (ArgumentException ("A document entry cannot set data-ranvier-head; the registry writes it", "parts"))

/// <summary>
/// The static attributes and attribute slots of a head entry's parts; the attributes in <c>keys</c> are static, and a
/// <c>data-ranvier-head</c> attribute raises.
/// </summary>
let private headParts (keys: string array) (parts: Part<'native, 'inst> array) =
    let attributes = ResizeArray<string * string>()
    let slots = ResizeArray<string * Func<obj, obj>>()

    for part in parts do
        match part with
        | StaticAttribute (name, value) ->
            rejectMark name
            attributes.Add ((name, value))
        | DynamicPart slot ->
            match slot.Target with
            | AttributeTarget name ->
                rejectMark name

                if Array.contains name keys then
                    raise (ArgumentException ("A document entry's key attribute must be static", "parts"))

                slots.Add ((name, slot.Read))
            | _ -> raise (ArgumentException ("Document entries take attribute parts only", "parts"))
        | NoPart -> ()
        | _ -> raise (ArgumentException ("Document entries take attribute parts only", "parts"))

    attributes.ToArray (), slots.ToArray ()

let private staticValue (name: string) (attributes: (string * string) array) =
    attributes
    |> Array.tryFind (fun (key, _) -> key = name)
    |> Option.map snd

let private headView kind key (attributes, slots) title : TemplateView<'inst> =
    TemplateView (
        HeadNode
            {
                HeadTemplate.Kind = kind
                Key = key
                Attributes = attributes
                Slots = slots
                Title = title
            }
    )

/// <summary>
/// The registry key of a link: <c>link:canonical</c> for a static canonical <c>rel</c>, <c>link:key:</c> followed by
/// the explicit key otherwise, and null for an unkeyed link. An explicit key and a canonical link have distinct keys.
/// </summary>
let private linkKey (attributes: (string * string) array) (explicit: string) =
    if staticValue "rel" attributes = Some "canonical" then
        "link:canonical"
    elif isNull explicit then
        null
    else
        "link:key:" + explicit

/// <summary>Entries for the document's head: shown once the instance is placed, withdrawn when it is disposed.</summary>
/// <remarks>Per key, the most recently shown live entry is in the head; unkeyed links are each in the head while live.</remarks>
[<Sealed>]
type DocumentBuilder<'inst> internal () =
    member _.title(value: string) : TemplateView<'inst> =
        headView HeadKind.Title "title" ([||], [||]) (Func<obj, obj>(fun _ -> box value))

    member _.title(read: 'inst -> string) : TemplateView<'inst> =
        headView HeadKind.Title "title" ([||], [||]) (Func<obj, obj>(fun instance -> box (read (unbox instance))))

    /// <summary>A meta element, keyed by its static name, property, http-equiv or charset attribute.</summary>
    member _.meta(parts: Part<HTMLMetaElement, 'inst> array) : TemplateView<'inst> =
        let attributes, slots = headParts metaKeys parts

        let key =
            metaKeys
            |> Array.tryPick (fun name ->
                staticValue name attributes
                |> Option.map (fun value ->
                    if name = "charset" then
                        "meta:charset"
                    else
                        "meta:" + name + "=" + value))
            |> Option.defaultWith (fun () ->
                raise (ArgumentException ("A meta entry needs a static name, property, http-equiv or charset attribute", "parts")))

        headView HeadKind.Meta key (attributes, slots) null

    /// <summary>A link element, keyed by its <c>rel</c> when that is <c>canonical</c> and unkeyed otherwise.</summary>
    member _.link(parts: Part<HTMLLinkElement, 'inst> array) : TemplateView<'inst> =
        let attributes, slots = headParts linkKeys parts
        headView HeadKind.Link (linkKey attributes null) (attributes, slots) null

    /// <summary>A link element, keyed by its <c>rel</c> when that is <c>canonical</c> and by <c>key</c> otherwise.</summary>
    member _.link(key: string, parts: Part<HTMLLinkElement, 'inst> array) : TemplateView<'inst> =
        if String.IsNullOrEmpty key then
            raise (ArgumentException ("A link key cannot be empty", "key"))

        let attributes, slots = headParts linkKeys parts
        headView HeadKind.Link (linkKey attributes key) (attributes, slots) null

/// <summary>Builds a template for instances of type <c>'inst</c>; dynamic parts receive the instance as their argument.</summary>
[<Sealed>]
type TemplateBuilder<'inst> internal () =
    let attrs = AttributeBuilder<'inst>()
    let props = PropertyBuilder<'inst>()
    let events = EventBuilder<'inst>()
    let hooks = HookBuilder<'inst>()
    let unsafe = UnsafeBuilder<'inst>()
    let documentEntries = DocumentBuilder<'inst>()

    member _.Attrs = attrs
    member _.Props = props
    member _.Events = events
    member _.Hooks = hooks
    member _.Unsafe = unsafe
    member _.Document = documentEntries

    member _.element (tag: Tag) (parts: Part<'native, 'inst> array) (children: TemplateView<'inst> array) : TemplateView<'inst> =
        elementNode tag parts children

    member _.fragment(children: TemplateView<'inst> array) : TemplateView<'inst> =
        TemplateView (FragmentNode (Array.map nodeOf children))

    member _.text(value: string) : TemplateView<'inst> =
        TemplateView (TextNode value)

    member _.text(read: 'inst -> string) : TemplateView<'inst> =
        TemplateView (TextSlotNode (slot TextTarget RenderMode read primitiveSame))

    member _.text(source: Source<'inst, string>) : TemplateView<'inst> =
        TemplateView (TextSlotNode (sourceSlot TextTarget source primitiveSame))

    member _.from(accessor: 'inst -> Signal<'value>) : Source<'inst, 'value> =
        Source (Func<obj, obj>(fun instance -> box (accessor (unbox instance)).Value))

    member _.from(accessor: 'inst -> Memo<'value>) : Source<'inst, 'value> =
        Source (Func<obj, obj>(fun instance -> box (accessor (unbox instance)).Value))

    member _.isolated(part: Part<'native, 'inst>) : Part<'native, 'inst> =
        match part with
        | DynamicPart slot -> DynamicPart { slot with Mode = IsolatedMode }
        | _ -> invalidArg "part" "Only dynamic parts can be isolated"

    member _.isolated(view: TemplateView<'inst>) : TemplateView<'inst> =
        match view with
        | TemplateView (TextSlotNode slot) -> TemplateView (TextSlotNode { slot with Mode = IsolatedMode })
        | _ -> invalidArg "view" "Only text slots can be isolated"

    /// <summary>A view hole: renders the view read from the instance as it is created, between region markers.</summary>
    /// <remarks>The reader runs once per instance, untracked.</remarks>
    member _.view(read: 'inst -> Program.View) : TemplateView<'inst> =
        TemplateView (
            RegionNode
                {
                    Items = Func<obj, obj array>(fun instance -> [| box ((null: obj), box (read (unbox instance))) |])
                    Key = KeyedItems.pairKey
                    Row = ViewRow
                }
        )

    /// <summary>
    /// A boundary between region markers. <c>content</c> receives the boundary's status and is shown once every binding
    /// under it has read a value; <c>pending</c> is shown until then, and <c>failed</c> replaces either after a failure
    /// under the content.
    /// </summary>
    member _.boundary (pending: Component<unit>) (failed: Component<BoundaryFailure>) (content: Component<BoundaryStatus>) : TemplateView<'inst> =
        let views =
            {
                Pending = fun backend -> backend.Template (pending, box ())
                Failed = fun failure backend -> backend.Template (failed, box failure)
                Content = fun status backend -> backend.Template (content, box status)
            }

        TemplateView (
            RegionNode
                {
                    Items = Func<obj, obj array>(fun _ -> [| box ((null: obj), box views) |])
                    Key = KeyedItems.pairKey
                    Row = BoundaryRow
                }
        )

    /// <summary>An arm that renders <c>definition</c>; its props read the payload of the arm currently selected.</summary>
    member _.arm (definition: Component<unit -> 'payload>) (payload: 'payload) : Arm =
        let template = definition :> TemplateComponent

        if isNull template.armReader then
            let reader =
                Func<(unit -> obj), obj>(fun current -> box (fun () -> unbox<'payload> (unbox<Arm>(current ())).Payload))

#if FABLE_COMPILER
            template.armReader <- reader
#else
            Threading.Interlocked.CompareExchange (&template.armReader, reader, null)
            |> ignore
#endif

        {
            Arm.Definition = template
            Payload = box payload
            Reader = template.ArmReader
        }

    /// <summary>Renders the arm that <c>arms</c> selects for the instance's union value, between region markers.</summary>
    /// <remarks>Arms are compared by key under <c>=</c>; a <c>matchWith</c> key is the arm's definition. Selecting another definition remounts, and selecting the same definition updates the payload read by its props. Writing <c>arms</c> as a <c>function</c> match lets the compiler check that every case has an arm. A remount runs the previous arm's cleanups and removes its nodes before the new arm is set up.</remarks>
    member _.matchWith (read: 'inst -> 'union) (arms: 'union -> Arm) : TemplateView<'inst> =
        TemplateView (
            RegionNode
                {
                    Items =
                        Func<obj, obj array>(fun instance ->
                            let arm = arms (read (unbox instance))
                            [| box (box arm.Definition, box arm) |])
                    Key = KeyedItems.pairKey
                    Row = ArmRow
                }
        )

    /// <summary>Renders the arm selected for the instance's key, between region markers; a key change under <c>=</c> or a change of the selected definition remounts the arm.</summary>
    /// <remarks>An equal key with the same definition keeps the arm and updates the payload read by its props. A remount runs the previous arm's cleanups and removes its nodes before the new arm is set up.</remarks>
    member _.branch<'key when 'key: equality> (key: 'inst -> 'key) (arms: 'key -> Arm) : TemplateView<'inst> =
        TemplateView (
            RegionNode
                {
                    Items =
                        Func<obj, obj array>(fun instance ->
                            let current = key (unbox instance)
                            let arm = arms current
                            [| box (box (box current, arm.Definition), box arm) |])
                    Key = KeyedItems.pairKey
                    Row = ArmRow
                }
        )

    /// <summary>Renders one <c>row</c> instance per item, between region markers; items with equal keys under <c>=</c> keep their row, whose props read the current item.</summary>
    /// <remarks>Keys must be pure. An item keeps its key while it is in the list; replace the item to change its key.
    /// Duplicate keys fail before the rows change.</remarks>
    member _.each<'item, 'key when 'key: equality>
        (key: 'item -> 'key)
        (items: 'inst -> 'item array)
        (row: Component<unit -> 'item>)
        : TemplateView<'inst> =
        TemplateView (
            RegionNode
                {
                    Items = Func<obj, obj array>(fun instance -> KeyedItems.boxAll (items (unbox instance)))
                    Key = Func<obj, obj>(fun item -> box (key (unbox<'item> item)))
                    Row = ItemRow (row :> TemplateComponent, Func<(unit -> obj), obj>(fun current -> box (fun () -> unbox<'item>(current ()))))
                }
        )

    /// <summary>Inlines a child template: its markup joins this template's skeleton, and its setup runs once per instance with props read untracked from the instance.</summary>
    member _.render (definition: Component<'props>) (props: 'inst -> 'props) : TemplateView<'inst> =
        TemplateView (ChildNode (definition :> TemplateComponent, Func<obj, obj>(fun instance -> box (props (unbox instance)))))

module Component =
    /// <summary>Defines a component from per-instance setup and a template built once.</summary>
    /// <remarks><c>setup</c> runs untracked for each instance and returns the instance that dynamic parts receive.</remarks>
    let define (setup: Setup -> 'props -> 'inst) (build: TemplateBuilder<'inst> -> TemplateView<'inst>) : Component<'props> =
        let (TemplateView root) = build (TemplateBuilder<'inst>())
        Component<'props>(Func<Setup, obj, obj>(fun context props -> box (setup context (unbox props))), root)

    /// <summary>Renders an instance as a Program view; the DOM backend materialises the compiled template.</summary>
    let render (definition: Component<'props>) (props: 'props) : Program.View =
        Program.View (fun backend -> backend.Template (definition, box props))

    /// <summary>Renders an instance in any representation through the default expansion.</summary>
    let renderWith (ops: Operations<'view>) (definition: Component<'props>) (props: 'props) : 'view =
        expandWith ops (definition :> TemplateComponent) (box props)
