/// <summary>
/// Renders the story page and its twin without Motion parts under the story's Omit policy, and fails when they differ.
/// Given a path, writes the story's render there.
/// </summary>
module Program

open System.IO
open Ranvier.View.Foundation

[<EntryPoint>]
let main args =
    let render definition =
        Server.render Story.serverOptions definition ignore

    let story = render Story.view
    let twin = render StoryTwin.view

    match args with
    | [| "--write"; path |] ->
        Directory.CreateDirectory (Path.GetDirectoryName (Path.GetFullPath path))
        |> ignore

        File.WriteAllText (path, story)
        printfn "wrote %s (%d characters)" path story.Length
    | _ -> ()

    if story = twin then
        printfn "ok story: the server render equals its twin without Motion parts (%d characters)" story.Length
        0
    else
        printfn "FAIL story\n  motion: %s\n  twin:   %s" story twin
        1
