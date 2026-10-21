/// <summary>Arms.Match fixtures compared with their explicit twins by layout, structural hash and server HTML.</summary>
module ArmsServerChecks

open global.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open ArmsFixtures

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

let private omit =
    { Server.Options.defaults with
        Policy = Omit
    }

let private sameLayouts () =
    for case in all do
        equal
            (case.Fixture, SugarFixtures.layoutSignature case.Twin.Root, structuralHash case.Twin)
            (case.Fixture, SugarFixtures.layoutSignature case.Site.Root, structuralHash case.Site)

/// <summary>Equal HTML for every fixture state; only the editor's listener under Reject may raise, on both sides alike.</summary>
let private sameHtml (options: Server.Options) () =
    for case in all do
        let site, twin = case.Render options
        let mayRaise = options.Policy = Reject && case.Fixture = "editor"

        if
            not mayRaise
            && (site.StartsWith "raised: "
                || twin.StartsWith "raised: ")
        then
            failwithf "%s %s raised: site %s; twin %s" case.Fixture case.State site twin

        equal (case.Fixture, case.State, twin) (case.Fixture, case.State, site)

let cases: (string * (unit -> unit)) array =
    [|
        "Arms.Match fixtures share their twins' layouts and hashes", sameLayouts
        "Arms.Match fixtures render their twins' server HTML under Reject", sameHtml Server.Options.defaults
        "Arms.Match fixtures render their twins' server HTML under Omit", sameHtml omit
        "an incomplete Arms.Match raises for its unmatched case", (fun () -> equal true (incompleteRaises ()))
    |]
