/// The page sample of docs/content/server/deployment.md.
module ServerPage

open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Counter
open Shared

let options = { Server.Options.defaults with Policy = Omit }

let page (props: CounterProps) : string =
    let document =
        { Server.Document.defaults with
            Lang = Some "en"
            State = [ stateId, counterProps.Encode props ]
            Scripts = [ Server.Script.Module "/main.js" ] }

    Server.renderDocument options document counter props
