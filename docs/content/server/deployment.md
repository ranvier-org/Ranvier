---
title: Deployment
---

:::warning Experimental
Server rendering and hydration are experimental and unpackaged; see [Server rendering](index.md) for
their status.
:::

A server-rendered app compiles one set of component files twice: on .NET for the server, and with Fable
for the browser. This page assembles the counter from [Getting started](../view/getting-started.md)
into that shape.

## Projects

| Project | Target | References | Holds |
| --- | --- | --- | --- |
| `Shared` | .NET and Fable | `Ranvier.View.Portable`, `Ranvier.Wire` | Components, props types and their [codecs](state-transfer.md). |
| `Server` | .NET | `Shared` | The page function and the HTTP endpoints. |
| `Client` | Fable | `Shared`, `Ranvier.View.Foundation` | The hydration entry point. |

`Ranvier.View.Portable` holds `Server` and every template type, with no browser dependency.
`Ranvier.View.Foundation` holds `Mounting` and `Hydration` and runs only under Fable. The component
files reference DOM types (`Dom.HTMLInputElement` in a property writer, for example) through
`Fable.Core.TS`, which compiles on .NET as well.

## The page

```fsharp
module ServerPage

open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Counter
open Shared

let options = { Server.Options.defaults with Policy = Omit }

let page (props: CounterProps) : string =
    let rendered = Server.renderPage options counter props

    $"""<!doctype html>
<html>
<head><meta charset="utf-8">{rendered.Head}</head>
<body>
<div id="app">{rendered.Body}</div>
{Server.stateScript stateId (counterProps.Encode props)}
<script type="module">import "/main.js";</script>
</body>
</html>"""
```

- The host element, `#app`, holds only the body, so hydration's root marker is its first
  child.
- The state script sits outside the host.
- The client script loads after the body.

## The endpoint

With ASP.NET Core minimal APIs:

```fsharp
open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

let run (args: string array) =
    let app = WebApplication.CreateBuilder(args).Build ()

    app.MapGet ("/", Func<IResult> (fun () -> Results.Content (page { Start = 0; Label = "Clicks" }, "text/html")))
    |> ignore

    app.Run ()
```

Each `Server.render` and `renderPage` call creates and disposes its own graph, so concurrent requests
share no reactive state. State shared between requests (a cache, a configuration signal) is read as it
is; see [Rendering](rendering.md#a-render-is-one-request).

## The client

`Client/Main.fs` is the [hydration entry point](hydration.md#hydrating-a-component). Build it with Fable
and a bundler, and serve the bundle as `/main.js`:

```bash
dotnet fable Client/Client.fsproj --outDir Client/generated
npx vite build Client
```

```js
// Client/main.js
import { start } from "./generated/Main.js";

start();
```

## Choosing a shape

| Shape | Use it when |
| --- | --- |
| Server render and hydrate | The first paint should come from the server, and the page then runs in the browser. |
| Client mount only | The page needs no server HTML: mount with [`Mounting.mount`](../view/lifetimes.md) into an empty host. |
| Server render only | The page is static: render with `Server.render` and send no client bundle. |
| [Blazor Server](../guide/blazor-server.md) | The UI runs on the server over a circuit, with Ranvier holding the circuit's state. |
