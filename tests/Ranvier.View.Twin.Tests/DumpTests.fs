module Ranvier.View.Twin.Tests.DumpTests

open System
open Expecto
open Ranvier
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.Twin
open Ranvier.View.Twin.Tests.Fixtures

type Node' =
    {
        Name: string
        mutable Next: Node' option
    }

type DeepState =
    {
        Cyclic: Node'
        Long: string
        Deep: obj
    }

let private deep =
    Component.define
        (fun _ () ->
            let cyclic = { Name = "a"; Next = None }
            cyclic.Next <- Some cyclic

            let rec nest n : obj =
                if n = 0 then box "bottom" else box [ nest (n - 1) ]

            {
                Cyclic = cyclic
                Long = String ('x', 10_000)
                Deep = nest 200
            })
        (fun t -> t.p [||] [||])

type ProbeState =
    {
        Source: Signal<int>
        Doubled: Memo<int>
    }

// A memo bound to no node, counting its runs, beside a signal bound through a text slot.
let private probe (runs: int ref) =
    Component.define
        (fun _ () ->
            let source = Api.createSignal 1

            {
                Source = source
                Doubled =
                    Api.createMemo (fun _ ->
                        runs.Value <- runs.Value + 1
                        source.Value * 2)
            })
        (fun t -> t.p [||] [| t.text (fun (s: ProbeState) -> string s.Source.Value) |])

// The first instance value of type 'T registered under the session's host.
let private instanceOf<'T> (session: Session) : 'T =
    let rec find (node: TwinNode) =
        match Inspect.probeOf (unbox node.Native) with
        | Some (Inspect.InstanceProbe (_, instance, _, _)) when (instance :? 'T) -> Some (unbox<'T> instance)
        | _ -> node.Children |> List.tryPick find

    match find (Bridge.nodeOf session.Host) with
    | Some value -> value
    | None -> failwith "no instance"

[<Tests>]
let tests =
    testList
        "Dump"
        [
            testCase "a counter dumps its instance, field and bound text"
            <| fun () ->
                use session = Twin.mount counter 4
                let text = session.Dump ()
                Expect.stringContains text "instance CounterState" "instance"
                Expect.stringContains text "Count  signal  4" "field"
                Expect.stringContains text "=\"4\"" "bound text"

            testCase "dump bounds cyclic, long and deep values"
            <| fun () ->
                use session = Twin.mount deep ()
                let text = session.Dump ()
                Expect.stringContains text "(10000)" "long string cut with its length"
                Expect.stringContains text "<cycle>" "cycle"
                Expect.isLessThan text.Length 65536 "within MaxChars"
                Expect.stringContains text "…" "elided"

            testCase "a 1000-row list dumps 12 rows and one elision line"
            <| fun () ->
                let source = ref Unchecked.defaultof<Signal<int array>>

                use session =
                    Twin.mountView TwinMount.defaults (fun context ->
                        source.Value <- Api.createSignal (Array.init 1000 id)
                        Program.run (Component.render rows source.Value) context.Backend)

                let lines = (session.Dump ()).Split '\n'

                Expect.equal
                    (lines
                     |> Array.filter (fun l -> l.TrimStart().StartsWith "row "))
                        .Length
                    12
                    "rows"

                Expect.equal
                    (lines
                     |> Array.filter (fun l -> l.Contains "… 988 rows (keys 9 … 996)"))
                        .Length
                    1
                    "elision"

            testCase "MaxChars ends the text with the omitted line count"
            <| fun () ->
                use session = Twin.mount counter 1

                let text =
                    session.Dump
                        { Debug.DumpOptions.defaults with
                            MaxChars = 40
                        }

                Expect.stringContains text "lines omitted" "truncated"

            testCase "a dump runs no memo and shows a stale memo's previous value"
            <| fun () ->
                let runs = ref 0
                use session = Twin.mount (probe runs) ()

                let state = instanceOf<ProbeState> session

                session.Graph.Run (fun () -> state.Doubled.Value |> ignore)
                Expect.equal runs.Value 1 "one run"
                session.Graph.Run (fun () -> state.Source.Value <- 5)
                let before = runs.Value
                let text = session.Dump ()
                Expect.equal runs.Value before "the dump ran no memo"
                Expect.stringContains text "Doubled  memo  2" "the previous value"
                session.Graph.Run (fun () -> state.Doubled.Value |> ignore)
                Expect.equal runs.Value (before + 1) "the memo was still stale"

            testCase "the JSON form names its schema and parses"
            <| fun () ->
                use session = Twin.mount counter 2
                let json = Debug.json (Debug.capture Debug.DumpOptions.defaults session.Mounted)
                let document = Text.Json.JsonDocument.Parse json
                Expect.equal (document.RootElement.GetProperty("schema").GetString()) "ranvier-view-dump/1" "schema"

            testCase "a dump off the graph's thread raises"
            <| fun () ->
                use session = Twin.mount counter 2
                let mutable failure: exn = null

                let thread =
                    Threading.Thread (fun () ->
                        try
                            session.Dump () |> ignore
                        with e ->
                            failure <- e)

                thread.Start ()
                thread.Join ()
                Expect.isTrue (failure :? InvalidOperationException) "raised"
                Expect.stringContains failure.Message "off the graph's thread" "message"

            testCase "a dump from a row's setup during a synchronous keyed pass raises"
            <| fun () ->
                let source = ref Unchecked.defaultof<Signal<int array>>
                let mounted = ref Unchecked.defaultof<Mounting.Mounted>
                let failures = ResizeArray<string>()

                let dumpingRow =
                    Component.define
                        (fun _ (item: unit -> int) ->
                            if not (isNull (box mounted.Value)) then
                                try
                                    Debug.capture Debug.DumpOptions.defaults mounted.Value
                                    |> ignore
                                with :? InvalidOperationException as e ->
                                    failures.Add e.Message

                            item)
                        (fun t -> t.li [||] [||])

                let dumpingRows =
                    Component.define (fun _ (items: Signal<int array>) -> items) (fun t ->
                        t.ul [||] [| t.each id (fun (items: Signal<int array>) -> items.Value) dumpingRow |])

                use session =
                    Twin.mountView TwinMount.defaults (fun context ->
                        source.Value <- Api.createSignal [| 1 |]
                        Program.run (Component.render dumpingRows source.Value) context.Backend)

                mounted.Value <- session.Mounted
                session.Act (fun () -> source.Value.Value <- [| 1; 2 |])
                Expect.equal (List.ofSeq failures) [ "Debug.dump ran inside a flush" ] "raised in the pass"

            testCase "NodeIds ends each field line with the core node id"
            <| fun () ->
                use session = Twin.mount counter 3

                let text =
                    session.Dump
                        { Debug.DumpOptions.defaults with
                            NodeIds = true
                        }

                Expect.isTrue (Text.RegularExpressions.Regex.IsMatch (text, "Count  signal  3  node=\\d+")) text

            testCase "nodeAt returns the field's core node"
            <| fun () ->
                use session = Twin.mount counter 3
                let dump = Debug.capture Debug.DumpOptions.defaults session.Mounted

                let path =
                    match
                        dump.Root
                        |> Array.tryPick (function
                            | Debug.Instance (instance, _) -> Some instance.Path
                            | _ -> None)
                    with
                    | Some path -> path
                    | None -> failwith "no instance"

                let signal = (instanceOf<CounterState> session).Count

                match Debug.nodeAt (session.Mounted, path + ".Count") with
                | Some node -> Expect.isTrue (obj.ReferenceEquals (node, signal)) "the same signal"
                | None -> failtest "no node"

#if RANVIER_TRACE
            testCase "a dump records no trace event"
            <| fun () ->
                use session = Twin.mount counter 3
                let before = (Trace.events session.Graph).Length

                Debug.capture Debug.DumpOptions.defaults session.Mounted
                |> ignore

                Expect.equal (Trace.events session.Graph).Length before "trace events"
                Expect.isEmpty (Trace.reconcile session.Graph) "reconcile"
#endif

#if RANVIER_COUNTERS
            testCase "a dump leaves every counter unchanged"
            <| fun () ->
                use session = Twin.mount counter 3
                let before = Counters.Snapshot ()

                Debug.capture Debug.DumpOptions.defaults session.Mounted
                |> ignore

                Expect.equal (Counters.Snapshot ()) before "counters"
#endif
        ]
