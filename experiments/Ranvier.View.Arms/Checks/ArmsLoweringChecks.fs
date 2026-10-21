/// <summary>The .NET lowering of Arms.Match: each rejection with its text and location, accepted forms, and the lowering cost.</summary>
module ArmsLoweringChecks

#nowarn "25"

open System
open System.IO
open global.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Arms

type Load =
    | Idle
    | Loading of percent: int
    | Loaded of text: string * count: int

[<Struct>]
type Point = { X: int; Y: int }

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

let private here = Path.Combine (__SOURCE_DIRECTORY__, __SOURCE_FILE__)

let private omit =
    { Server.Options.defaults with
        Policy = Omit
    }

let private read (state: Signal<Load>) =
    state.Value

/// <summary>What building a site raised: the kind and variable, and the message after the location prefix.</summary>
let private raised (build: unit -> obj) =
    let unprefixed (message: string) =
        if message.StartsWith (here + "(") then
            message.Substring (message.IndexOf "): " + 3)
        else
            "unprefixed: " + message

    try
        build () |> ignore
        "none", ""
    with
    | :? BuildTimeReadException as error -> "BuildTimeRead " + error.Variable, unprefixed error.Message
    | :? ArmsMatchException as error -> "ArmsMatch", unprefixed error.Message

let private notObject (name: string) =
    "ArmsMatch",
    "Arms.Match: the instance type '"
    + name
    + "' is not an object type. An arm site keys each arm's payload by its instance; use a record, class, union or function instance."

let private am001 () =
    equal
        ("BuildTimeRead text",
         "Arms.Match: 'text' is read outside a reader of the instance. Read it inside a lambda over the instance, such as (fun _ -> text).")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (state: Signal<Load>) -> state)
                    (div () {
                        Arms.Match (
                            read,
                            function
                            | Loaded (text, _) -> span () { text }
                            | _ -> span () { "Other" }
                        )
                    })
            )))

let private am002 () =
    equal
        ("BuildTimeRead text",
         "Arms.Match: 'text' is read inside a lambda over 'unit -> string', which is not the instance type 'Signal<Load>'. Pass the value to the nested view through its items or props.")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (state: Signal<Load>) -> state)
                    (div () {
                        Arms.Match (
                            read,
                            function
                            | Loaded (text, _) -> ul () { forEach id (fun _ -> [| "a" |]) (li () { fun (item: unit -> string) -> item () + text }) }
                            | _ -> span () { "Other" }
                        )
                    })
            )))

let private am003 () =
    let arms: Load -> TemplateView<Signal<Load>> =
        function
        | Idle -> span () { "Idle" }
        | _ -> span () { "Other" }

    equal
        ("ArmsMatch", "Arms.Match takes a function literal as its second argument, such as (function | Case x -> ...).")
        (raised (fun () -> box (Component.template (fun _ (state: Signal<Load>) -> state) (div () { Arms.Match (read, arms) }))))

let private am004 () =
    equal
        (notObject "int")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (n: int) -> n)
                    (div () {
                        Arms.Match (
                            (fun (n: int) -> n),
                            function
                            | 0 -> span () { "zero" }
                            | _ -> span () { "other" }
                        )
                    })
            )))

    equal
        (notObject "string")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (s: string) -> s)
                    (div () {
                        Arms.Match (
                            (fun (s: string) -> s.Length),
                            function
                            | 0 -> span () { "zero" }
                            | _ -> span () { "other" }
                        )
                    })
            )))

    equal
        (notObject "unit")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (u: unit) -> u)
                    (div () {
                        Arms.Match (
                            (fun (_: unit) -> 0),
                            function
                            | 0 -> span () { "zero" }
                            | _ -> span () { "other" }
                        )
                    })
            )))

    equal
        (notObject "int option")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (o: int option) -> o)
                    (div () {
                        Arms.Match (
                            (fun (o: int option) -> defaultArg o 0),
                            function
                            | 0 -> span () { "zero" }
                            | _ -> span () { "other" }
                        )
                    })
            )))

    equal
        (notObject "Point")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (p: Point) -> p)
                    (div () {
                        Arms.Match (
                            (fun (p: Point) -> p.X),
                            function
                            | 0 -> span () { "zero" }
                            | _ -> span () { "other" }
                        )
                    })
            )))

let private am007 () =
    equal
        ("ArmsMatch", "Arms.Match: this pattern form is not supported.")
        (raised (fun () ->
            box (
                Component.template
                    (fun _ (state: Signal<Load>) -> state)
                    (div () {
                        Arms.Match (
                            read,
                            function
                            | Loaded (text, _) ->
                                (Console.Write ""

                                 if text = "" then
                                     span () { "empty" }
                                 else
                                     span () { "full" })
                            | _ -> span () { "Other" }
                        )
                    })
            )))

let private render (definition: Component<Signal<Load>>) (state: Load) =
    use graph = new Graph ()
    let signal = graph.Run (fun () -> Api.createSignal state)
    Server.render omit definition signal

/// <summary>An incomplete match builds; the unmatched case raises at the region read, as a plain match does.</summary>
let private incompleteRaisesAtRead () =
    let site =
        Component.template
            (fun _ (state: Signal<Load>) -> state)
            (div () {
                Arms.Match (
                    read,
                    function
                    | Idle -> span () { "Not started" }
                    | Loading percent -> b () { fun _ -> string percent }
                )
            })

    equal "<!--[r--><div><!--[a:220727aa--><span>Not started</span><!--]--></div><!--]-->" (render site Idle)

    let unmatched =
        try
            render site (Loaded ("x", 1)) |> ignore
            false
        with :? MatchFailureException ->
            true

    equal true unmatched

/// <summary>A delegate over the instance reading a pattern variable is rewritten as a reader.</summary>
let private delegateReader () =
    let site =
        Component.template
            (fun _ (state: Signal<Load>) -> state)
            (div () {
                Arms.Match (
                    read,
                    function
                    | Loaded (text, _) ->
                        span (
                            Hooks.exit (
                                Func<_, _, _, _>(fun (_: Signal<Load>) (_: Fable.Core.TS.Dom.HTMLSpanElement) finish ->
                                    if text = "" then
                                        finish ()

                                    ignore)
                            )
                        ) {
                            "held"
                        }
                    | _ -> span () { "Other" }
                )
            })

    equal true ((render site (Loaded ("a", 1))).Contains "<span>held</span>")

/// <summary>An if and a let at the top of case bodies, and a let of a view.</summary>
let private bodyForms () =
    let site =
        Component.template
            (fun _ (state: Signal<Load>) -> state)
            (div () {
                Arms.Match (
                    read,
                    function
                    | Loaded (_, n) when n > 100 -> if n > 1000 then b () { "huge" } else i () { "big" }
                    | Loaded (t, 0) ->
                        let label = t + "!"
                        pre () { fun _ -> label }
                    | Loaded (t, _) ->
                        let view = pre () { fun _ -> t }
                        view
                    | _ -> span () { "Other" }
                )
            })

    let hugeArm = armOf (b () { "huge" })
    let bigArm = armOf (i () { "big" })
    let textArm = armOf (pre () { fun (p: unit -> string) -> p () })
    let otherArm = armOf (span () { "Other" })

    let twin =
        Component.template
            (fun _ (state: Signal<Load>) -> state)
            (div () {
                matchWith read (function
                    | Loaded (_, n) when n > 1000 -> arm hugeArm ()
                    | Loaded (_, n) when n > 100 -> arm bigArm ()
                    | Loaded (t, 0) -> arm textArm (t + "!")
                    | Loaded (t, _) -> arm textArm t
                    | _ -> arm otherArm ())
            })

    for state in
        [
            Loaded ("x", 2000)
            Loaded ("x", 200)
            Loaded ("x", 0)
            Loaded ("y", 3)
            Idle
        ] do
        equal (state, render twin state) (state, render site state)

/// <summary>Milliseconds for the first and the mean of 200 further lowerings of a five-target site.</summary>
let private loweringCost () =
    let lower () =
        Arms.Match (
            read,
            function
            | Idle
            | Loading 0 -> span () { "Not started" }
            | Loading percent -> b () { fun _ -> string percent }
            | Loaded (_, 0) -> span () { "Empty" }
            | Loaded (text, count) when count > 99 -> b () { fun _ -> text }
            | Loaded (text, count) -> pre () { fun _ -> text + string count }
        )
        |> ignore

    let watch = Diagnostics.Stopwatch.StartNew ()
    lower ()
    let first = watch.Elapsed.TotalMilliseconds
    watch.Restart ()

    for _ in 1..200 do
        lower ()

    first, watch.Elapsed.TotalMilliseconds / 200.0

let private loweringBudget () =
    let first, mean = loweringCost ()
    printfn "lowering|first=%.2fms mean=%.3fms" first mean

    if
        Environment.GetCommandLineArgs ()
        |> Array.contains "--timing"
    then
        equal (true, true) (first <= 10.0, mean <= 4.0)

let cases: (string * (unit -> unit)) array =
    [|
        "a pattern variable as static text raises AM001 with the call's location", am001
        "a pattern variable in a row reader raises AM002", am002
        "a non-literal arms function raises AM003", am003
        "int, string, unit, option and struct instances raise AM004", am004
        "a decision hidden under a sequence raises AM007", am007
        "an incomplete match builds and raises MatchFailureException for the unmatched case", incompleteRaisesAtRead
        "a delegate over the instance reading a pattern variable is a reader", delegateReader
        "if, let and a let of a view at the top of case bodies", bodyForms
        "the lowering of a five-target site stays within its budget", loweringBudget
    |]

/// <summary>A reader with no arm of its site for its instance raises AM006.</summary>
let private noArm () =
    let message =
        try
            Ranvier.View.Arms.Cells.values (Ranvier.View.Arms.Cells.create ()) (obj ()) 0
            |> ignore

            ""
        with :? ArmsMatchException as error ->
            error.Message

    equal
        "Arms.Match: no arm of this site is mounted for the instance this reader received. A lambda over the instance type inside a case body runs with the arm's instance."
        message

/// <summary>Sixteen threads render one site for one shared instance, each under its own graph, 50 times each (Review Focus 1).</summary>
let private concurrentRenders () =
    let score: ArmsFixtures.Score = { Name = "ada"; Points = 5 }
    let expected = Server.render omit ArmsFixtures.plainScore score
    let failures = Collections.Concurrent.ConcurrentBag<string>()
    use barrier = new Threading.Barrier 16

    let threads =
        [|
            for _ in 1..16 ->
                Threading.Thread (fun () ->
                    barrier.SignalAndWait ()

                    for _ in 1..50 do
                        try
                            let html = Server.render omit ArmsFixtures.plainScore score

                            if html <> expected then
                                failures.Add html
                        with error ->
                            failures.Add error.Message)
        |]

    for thread in threads do
        thread.Start ()

    for thread in threads do
        thread.Join ()

    equal ("<!--[r--><div><!--[a:", []) (expected.Substring (0, 21), List.ofSeq failures)

let runtimeCases: (string * (unit -> unit)) array =
    [|
        "a reader with no arm for its instance raises AM006", noArm
        "concurrent server renders of one instance read their own arms", concurrentRenders
    |]
