---
title: DOM contracts and host services
---

Use `Ranvier.Dom` when a component needs to work with both a browser document and a native Ranvier host.
Reference `Fable.Ranvier.Dom` in the browser project. The shared interfaces follow WebKit's supported DOM
surface. A host supplies the document, window and services for its own engine.

Generated interfaces such as `Document`, `Element`, `EventTarget`, `KeyboardEvent`, `SVGElement` and
`CSSStyleDeclaration` describe engine objects. A service adds operations that need a host facility, a
subscription lifetime or conversion to F# values. For the full interface and member signatures, see the
[Ranvier.Dom API reference](/reference/ranvier-dom/index.md).

## Observe input and viewport changes

`InputState.create window document maxTouchPoints` installs listeners on a window. `Current ()` reads an
`InputSnapshot` containing focus, last trusted input modality, focus visibility and touch capacity.
`Subscribe` reports activation, modality and pointer capture transitions. `Captured pointerId` retrieves the
element holding capture. Disposing the subscription removes that observer. Dispose the service when its window
closes.

To publish a service into a graph, take an initial snapshot, then update a signal on callbacks. This example
receives an existing service, so the owner releases only its subscription.

```fsharp
open Ranvier
open Ranvier.Dom

let observeInput (graph: Graph) (input: InputStateService) =
    graph.Run(fun () ->
        let state = createSignal (input.Current())
        let subscription =
            input.Subscribe(fun _ ->
                let current = input.Current()
                graph.Dispatch(fun () -> state.Value <- current))
        onCleanup subscription.Dispose
        state)
```

`PointerInput.contacts` copies a touch list into contact values. `PointerInput.lock provider document` exposes
`Current`, `Request`, `Exit` and `Subscribe` for pointer lock. Call `Request` from the user gesture that
initiates the lock. `PointerInput.refusal` classifies an engine refusal. `InputState.keys` and
`PointerInput.keys` identify the facilities described by their `capabilities` functions.

`Viewport.create provider` exposes window dimensions in CSS pixels, device pixel ratio and document visibility
through `Current ()` and `Hidden ()`. `Subscribe` delivers resize, scroll, scroll end, activation, visibility
and scale changes. For the visible portion of a zoomed page, use `VisualViewports.create` and
`VisualViewportService.Current`. `ViewportScrollService` reads or requests scroll positions.

## Match a media query

`MediaQueries.create provider` produces a `MediaQueryService`. Keep the query handle for as long as the
component needs updates. Dispose it to remove its engine listener.

```fsharp
let reducedMotion (service: MediaQueryService) (changed: bool -> unit) =
    let query = service.Match "(prefers-reduced-motion: reduce)"
    onCleanup query.Dispose
    changed query.Matches
    let subscription = query.Subscribe changed
    onCleanup subscription.Dispose
```

`Media` gives the serialized query. A host that implements `CanSupply` can set and inspect preferences with
`Supply` and `Supplied ()`. `Unavailable` names preference features the host cannot supply.
`MediaQueries.validate` checks a proposed preference record.

## Cancel a group of operations

`Cancellation.create host` binds a cancellation domain to a host's timers, owner queue and event listeners.
`CreateController ()` supplies a signal that can be aborted explicitly. `Timeout` produces a deadline signal.
`Any` combines signals from the same service and keeps the first abort reason. `Abort` creates an
already-aborted signal.

Pass the signal to `Listen` to tie a DOM listener to its lifetime. `AbortSignal.Aborted` and `Reason` inspect
state, `ThrowIfAborted ()` raises `AbortedException` and `OnAbort` registers a removable callback for a future
abort. `ToCancellationToken` and `FromCancellationToken` bridge to .NET asynchronous operations. Dispose the
service to release its timers and registrations.

## Find a host facility

`Capabilities.create ()` creates a registry. Register providers, enumerate `KnownKeys ()` and `Observe` a key
for a report about the host. A report distinguishes unavailable or unknown evidence from an observed value.
The following entry points cover the shared service families. Check the host's provider before using an
optional facility.

| Task | Contracts and entry points |
| --- | --- |
| Inspect styles | `CssQueryService.Supports` tests a declaration or a condition. `ComputedStyleService.Get` reads styles for an element or pseudo-element. Escape an identifier with `CSS.escape`. |
| Edit styles | `StyleSheetService.Create`, `ReplaceAsync` and `SetAdopted` manage constructed sheets. `CustomPropertyService.Register` registers a definition. Use `StyleValueService.Parse` for typed CSS and `StyleMapService` for attribute, declared or computed maps. |
| Transform geometry | Use `GeometryService` to construct matrices, points, quads and rectangles. `TransformValueService` creates typed transforms. `Coordinates.create` supplies `ConvertPoints`, `ConvertQuad` and `BoxQuad` for conversions across coordinate spaces. |
| Work with SVG | `SvgPaths.getData` and `setData` read and write path segments. `SvgViewState` carries the current view and whether it is active. |
| Observe layout and mutations | `ResizeObservation.create`, `IntersectionObservation.create` and `MutationObservation.create` wrap their host providers. Create an observer from the resulting service, select its targets and disconnect it when observation ends. |
| Inspect the rendering engine | Read `DiagnosticsService.Features`, then enable the supported inspection and request `MatchedRules`, `Cascade`, `BoxModel` or `RenderTree`. |
| Create events and submit forms | `EventService` constructs events and reads their detail. `EventDetailService`, `ToggleEventService` and `ValueFactoryService` construct typed events. Use `FormDataService.FromForm` for the form entries and `FormSubmissionService.Subscribe` for subsequent submissions. |
| Load resources and fonts | `ResourceService.Load`, `LoadImage` and `LoadFont` start resource operations. `ResourceSignals.cancelOn` ties an operation to an abort signal. `FontSet.Check`, `Load`, `Ready` and `Subscribe` expose the host font set. |
| Transfer binary data | `BinaryValueService` creates blobs, files and file lists. `DataTransferService.Create` supplies a transfer object. Pair `BlobUrlService.CreateObjectUrl` with `RevokeObjectUrl` when the consumer finishes. |
| Draw and export pixels | `CanvasContextService.GetContext` negotiates a drawing context. `CanvasText.measure` reads text metrics. `ImageDataService.Read` and `Write` access pixel data. `CanvasExportService` produces bitmaps, blobs or data URLs. Check `SnapshotService.Available` and supported encodings before `Capture`. |
| Schedule and measure work | Use `FrameScheduler.Frame`, `Microtask`, `Timeout` or `IdleScheduler.Idle`. Tests can advance a `ManualFrameScheduler`. `TimingService.Mark`, `Measure` and `GetEntries` record and query a user timing timeline. |
| Drive animations | `AnimationService` creates keyframe effects and animations and exposes their readiness and completion. `TimelineService` creates view or scroll timelines, `CustomEffectService` attaches custom effects and `ViewTransitionService.Start` begins a document transition. |
| Adapt to the display | Use `ScreenService.Read`, `Subscribe`, `Lock` and `Unlock` for screen state and orientation. `MediaCapabilitiesService.DecodingInfo` and `EncodingInfo` query media configurations. |
| Localize and highlight text | `LocaleService` resolves locale and time-zone overrides. `TextSegmenter.Segment` finds text segments. `HighlightService` registers ranges and can retrieve highlights at a point. |
| Use platform facilities | `UrlService` parses URLs, search parameters and URL patterns. `CryptoService` exposes random values, UUIDs and digests. Use `WakeLockService.Request` for a wake lock, `NotificationService` for permission and display, and `AppBadgeService.Set` or `Clear` for badges. |
