/// <summary>
/// The server render of the lifecycle fixtures equals the render of its twin without Motion parts, under the default
/// <c>Reject</c> policy. Run on .NET with <c>dotnet run</c>.
/// </summary>
module ServerChecks

open global.Ranvier
open Ranvier.Anime
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar
open Ranvier.View.Anime

type Notes = { Notes: string array }

let private notes =
    Component.template
        (fun _ () -> { Notes = [| "a"; "b" |] })
        (ol () {
            forEachFixed
                id
                (fun (n: Notes) -> n.Notes)
                (li (
                    Motion.enter (fun _ el -> Anime.animate (el, Anim (x = Value.fromTo (40, 0), duration = 200))),
                    Motion.exit (fun _ el -> Anime.animate (el, Anim (x = 40, duration = 200)))
                ) {
                    fun (note: string) -> note
                })
        })

let private notesTwin =
    Component.template
        (fun _ () -> { Notes = [| "a"; "b" |] })
        (ol () { forEachFixed id (fun (n: Notes) -> n.Notes) (li () { fun (note: string) -> note }) })

type Shelf = { Books: string array }

let private presets =
    Component.template
        (fun _ () -> { Books = [| "a"; "b" |] })
        (div () {
            ul (Motion.flip (LayoutParams (duration = 300))) {
                forEachFixed id (fun (s: Shelf) -> s.Books) (li (Motion.rise) { fun (book: string) -> book })
            }

            ul () { forEachFixed id (fun (s: Shelf) -> s.Books) (li (Motion.scale) { fun (book: string) -> book }) }
            ul () { forEachFixed id (fun (s: Shelf) -> s.Books) (li (Motion.collapse) { fun (book: string) -> book }) }
        })

let private presetsTwin =
    Component.template
        (fun _ () -> { Books = [| "a"; "b" |] })
        (div () {
            ul () { forEachFixed id (fun (s: Shelf) -> s.Books) (li () { fun (book: string) -> book }) }
            ul () { forEachFixed id (fun (s: Shelf) -> s.Books) (li () { fun (book: string) -> book }) }
            ul () { forEachFixed id (fun (s: Shelf) -> s.Books) (li () { fun (book: string) -> book }) }
        })

let private render (definition: Component<'props>) (props: 'props) =
    Server.render Server.Options.defaults definition props

/// <summary>Each comparison by name: the render with Motion parts and the render of its twin.</summary>
let comparisons: (string * (unit -> string * string)) array =
    [|
        "notes", (fun () -> render notes (), render notesTwin ())
        "presets", (fun () -> render presets (), render presetsTwin ())
    |]

#if !FABLE_COMPILER
[<EntryPoint>]
let main _ =
    let failures =
        [
            for name, compare in comparisons do
                let withMotion, twin = compare ()

                if withMotion = twin then
                    printfn "ok %s" name
                else
                    printfn "FAIL %s\n  motion: %s\n  twin:   %s" name withMotion twin
                    yield name
        ]

    printfn "%d of %d server renders equal their twins" (comparisons.Length - failures.Length) comparisons.Length
    if failures.IsEmpty then 0 else 1
#endif
