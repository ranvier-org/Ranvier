module Ranvier.Tests.NullChecks

open System
open System.Collections.Generic
open Expecto
open Ranvier

#if FABLE_COMPILER
[<Fable.Core.Emit("undefined")>]
let private undefinedValue: obj = Fable.Core.Util.jsNative

[<AllowNullLiteral>]
type private Holder() =
    member val Next: Holder = null with get, set
#endif

/// <summary>
/// Tests of <c>Platform.isNull</c> under Fable and null-argument validation on public API entry points.
/// </summary>
[<Tests>]
let tests =
    testList
        "NullChecks"
        [
#if FABLE_COMPILER
            testList
                "Platform.isNull"
                [
                    test "is true for null" {
                        Expect.isTrue (Platform.isNull (null: obj)) "null object"
                        Expect.isTrue (Platform.isNull (null: string)) "null string"
                        Expect.isTrue (Platform.isNull (Holder().Next)) "unset field"
                    }

                    test "is false for a value" {
                        Expect.isFalse (Platform.isNull (box 0)) "boxed zero"
                        Expect.isFalse (Platform.isNull "") "empty string"
                        Expect.isFalse (Platform.isNull (box false)) "boxed false"
                        Expect.isFalse (Platform.isNull (Holder ())) "object"
                        Expect.isFalse (Platform.isNull (Array.empty<int>)) "empty array"
                    }

                    test "is true for undefined, as isNull is" {
                        Expect.isTrue (isNull undefinedValue) "FSharp.Core isNull"
                        Expect.isTrue (Platform.isNull undefinedValue) "Platform.isNull"
                    }
                ]
#endif
            test "createMemoOn and its variants reject null read, compute, or comparer" {
                use g = new Graph ()
                use _ = g.Activate ()
                let nullRead = Unchecked.defaultof<unit -> int>
                let nullCompute = Unchecked.defaultof<int -> int>
                let nullComparer = Unchecked.defaultof<IEqualityComparer<int>>
                let validComparer = EqualityComparer<int>.Default

                Expect.throwsT<ArgumentNullException> (fun () -> createMemoOn nullRead id |> ignore) "createMemoOn null read"
                Expect.throwsT<ArgumentNullException> (fun () -> createMemoOn (fun () -> 1) nullCompute |> ignore) "createMemoOn null compute"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createMemoOnWithComparer nullComparer (fun () -> 1) id
                        |> ignore)
                    "createMemoOnWithComparer null comparer"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createMemoOnWithComparer validComparer nullRead id
                        |> ignore)
                    "createMemoOnWithComparer null read"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createMemoOnWithComparer validComparer (fun () -> 1) nullCompute
                        |> ignore)
                    "createMemoOnWithComparer null compute"

                Expect.throwsT<ArgumentNullException> (fun () -> createMemoWithOn nullRead id |> ignore) "createMemoWithOn null read"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createMemoWithOn (fun () -> 1) nullCompute
                        |> ignore)
                    "createMemoWithOn null compute"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createOwningMemoOnWithComparer nullComparer (fun () -> 1) id
                        |> ignore)
                    "createOwningMemoOnWithComparer null comparer"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createOwningMemoOnWithComparer validComparer nullRead id
                        |> ignore)
                    "createOwningMemoOnWithComparer null read"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        createOwningMemoOnWithComparer validComparer (fun () -> 1) nullCompute
                        |> ignore)
                    "createOwningMemoOnWithComparer null compute"
            }
        ]
