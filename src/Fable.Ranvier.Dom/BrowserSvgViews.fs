namespace Ranvier.Dom

open Fable.Core
open Fable.Core.JsInterop

/// <summary>Observes SVG view properties exposed by the actual browser root.</summary>
module SvgViews =
    [<Emit("$1 in $0")>]
    let private exposed (root: SVGSVGElement) (name: string) : bool = jsNative

    [<Emit("$0[$1]")>]
    let private property (root: SVGSVGElement) (name: string) : obj = jsNative

    [<Emit("$0 != null && typeof $0 === $1")>]
    let private matches (value: obj) (kind: string) : bool = jsNative

    [<Emit("$0 != null && $0.name === 'NotSupportedError'")>]
    let private unsupported (error: obj) : bool = jsNative

    /// <summary>Reads actual view properties and reports absent members without manufacturing view objects.</summary>
    /// <param name="root">The nonnull SVG root whose current properties are observed.</param>
    /// <exception cref="T:System.ArgumentNullException">The root is null.</exception>
    let inspect (root: SVGSVGElement) : SvgViewState =
        if isNull (box root) then
            nullArg "root"

        let read name kind =
            if not (exposed root name) then
                SvgViewAvailability.NotExposed
            else
                try
                    let value = property root name

                    if matches value kind then
                        SvgViewAvailability.Available value
                    else
                        SvgViewAvailability.Unknown $"The {name} getter returned no {kind} value."
                with error when unsupported (box error) ->
                    SvgViewAvailability.Unsupported error.Message

        let map convert =
            function
            | SvgViewAvailability.Available value -> SvgViewAvailability.Available (convert value)
            | SvgViewAvailability.NotExposed -> SvgViewAvailability.NotExposed
            | SvgViewAvailability.Unsupported reason -> SvgViewAvailability.Unsupported reason
            | SvgViewAvailability.Unknown reason -> SvgViewAvailability.Unknown reason

        {
            CurrentView =
                read "currentView" "object"
                |> map unbox<SVGViewSpec>
            UseCurrentView = read "useCurrentView" "boolean" |> map unbox<bool>
        }
