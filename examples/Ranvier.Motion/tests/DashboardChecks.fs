/// <summary>The dashboard's samples and paths, and the mounted dashboard fed by hand on the test clock.</summary>
module DashboardChecks

open global.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Scheduling
open Dashboard
open Harness

let private samplesAreSeeded (_: Clock) =
    let first = Array.init 40 sampleAt
    let again = Array.init 40 sampleAt

    let inRange =
        first
        |> Array.forall (fun s ->
            s.Cpu > 0.
            && s.Cpu < 1.
            && s.Memory > 0.
            && s.Memory < 1.
            && s.Latency > 0.
            && s.Latency < 1.)

    let kept = Array.fold keep [||] first
    equal (true, true, capacity, 16, 39) (first = again, inRange, kept.Length, kept[0].Tick, (Array.last kept).Tick)

let private pathsHoldCapacity (_: Clock) =
    let line = linePath [| 0.5; 1. |]

    let points =
        line.Split ' '
        |> Array.filter (fun part -> part.StartsWith "M" || part.StartsWith "L")

    let area = areaPath [| 0.5 |]

    equal (capacity, "M0.0", "L600.0 18.0", true) (points.Length, points[0], line.Substring (line.Length - 11), area.EndsWith "L600 200 L0 200 Z")

let private mounted () =
    let graph = new Graph ()
    let target = host ()
    let page = Dashboard.mount SynchronousCommits graph report target
    graph, target, page

let private push (graph: Graph) (page: Dashboard.Mounted) (sample: Sample) =
    graph.Run (fun () -> page.Store.Feed.Push sample)

let private close (graph: Graph) (page: Dashboard.Mounted) =
    page.Mount.Dispose ()
    graph.Dispose ()

let private pendingSkeleton (clock: Clock) =
    let graph, target, page = mounted ()
    let tile = find target ".skeleton-tile"
    advance clock 0.
    run clock 16. 400.
    let pulsing = styleOf tile "opacity"

    let waiting =
        countOf target ".skeleton-tile", countOf target ".metrics", countOf target ".failed", page.Store.Feed.Subscribers

    push graph page (sampleAt 1)

    let revealed =
        countOf target ".skeleton", countOf target ".metrics", connected tile, textOf (find target ".requests .note")

    close graph page
    equal ((4, 0, 0, 1), (0, 1, false, "sample 1"), true) (waiting, revealed, pulsing <> "" && pulsing <> "1")

let private gaugesFollow (clock: Clock) =
    let graph, target, page = mounted ()
    push graph page (sampleAt 1)
    let fill = find target ".gauge-fill"
    let first = styleOf fill "transform"
    let next = sampleAt 2
    push graph page next
    advance clock 0.
    run clock 16. 160.
    let moving = styleOf fill "transform"
    run clock 16. 900.
    let settled = styleOf fill "transform"
    let label = textOf (find target ".gauge-head strong")
    close graph page

    equal
        (sprintf "scaleX(%g)" (sampleAt 1).Cpu, true, sprintf "scaleX(%g)" next.Cpu, sprintf "%.0f%%" (next.Cpu * 100.))
        (first, moving <> first && moving <> settled, settled, label)

let private chartMorphsAndRingDraws (clock: Clock) =
    let graph, target, page = mounted ()
    push graph page (sampleAt 1)
    let line = find target ".chart-line"
    let ring = find target ".ring-value"
    let first = attributeOf line "d", attributeOf ring "stroke-dasharray"
    push graph page (sampleAt 2)
    advance clock 0.
    run clock 16. 200.
    let middle = attributeOf line "d", attributeOf ring "stroke-dasharray"
    run clock 16. 900.
    let settled = attributeOf line "d", attributeOf ring "stroke-dasharray"
    let expected = linePath [| (sampleAt 1).Cpu; (sampleAt 2).Cpu |]
    close graph page

    equal
        (linePath [| (sampleAt 1).Cpu |], true, true, expected, true)
        (fst first, fst middle <> fst first && fst middle <> expected, snd middle <> snd first, fst settled, snd settled <> snd middle)

let private counterTweens (clock: Clock) =
    let graph, target, page = mounted ()
    let first = sampleAt 1
    push graph page first
    let counter = find target ".counter"
    advance clock 0.
    run clock 16. 300.
    let rising = float (textOf counter)
    run clock 16. 800.
    let reached = textOf counter
    let second = sampleAt 5
    push graph page second
    advance clock 0.
    run clock 16. 300.
    let between = float (textOf counter)
    run clock 16. 800.
    let landed = textOf counter
    close graph page

    let low, high =
        min first.Requests second.Requests, max first.Requests second.Requests

    equal
        (true, sprintf "%.0f" first.Requests, true, sprintf "%.0f" second.Requests)
        (rising > 0. && rising < first.Requests, reached, between > low && between < high, landed)

let private alertFades (clock: Clock) =
    let graph, target, page = mounted ()
    push graph page (sampleAt 1)
    let quiet = countOf target ".alert"
    push graph page (spikeAt 2)
    let alert = find target ".alert"
    advance clock 0.
    run clock 16. 100.
    let fading = styleOf alert "opacity"
    run clock 16. 300.
    let shown = styleOf alert "opacity"
    push graph page (sampleAt 3)
    let held = connected alert
    run clock 16. 300.
    let gone = connected alert
    close graph page
    equal (0, true, "1", true, false) (quiet, fading <> "" && fading <> "0" && fading <> "1", shown, held, gone)

let private detailsToggle (clock: Clock) =
    let graph, target, page = mounted ()
    push graph page (sampleAt 3)
    let hidden = countOf target ".details"
    write graph page.Mount (fun () -> page.Store.Details.Value <- true)
    let values = textsOf target ".details dd"
    write graph page.Mount (fun () -> page.Store.Details.Value <- false)
    let leaving = countOf target ".details"
    run clock 16. 300.
    let left = countOf target ".details"
    close graph page
    equal (0, "3", 1, 0) (hidden, values[0], leaving, left)

let private reconnectRestarts (clock: Clock) =
    let graph, target, page = mounted ()
    push graph page (sampleAt 1)
    push graph page (sampleAt 2)
    write graph page.Mount (fun () -> page.Store.Session.Value <- 1)

    let refreshing =
        attributeOf (find target ".metrics") "data-refreshing", page.Store.Feed.Subscribers

    push graph page (sampleAt 7)

    let resumed =
        attributeOf (find target ".metrics") "data-refreshing", textOf (find target ".chart .note")

    close graph page
    equal (("true", 1), (null, "1 of 24")) (refreshing, resumed)

let cases: Case list =
    [
        case "samples are seeded and in range, and keep holds the latest 24" samplesAreSeeded
        case "the chart's paths always hold 24 points, padded with the first value" pathsHoldCapacity
        case "the skeleton pulses while the stream is pending and leaves at the first sample" pendingSkeleton
        case "gauges set their first value and follow each sample" gaugesFollow
        case "the chart morphs to each window and the ring draws the CPU load" chartMorphsAndRingDraws
        case "the request counter tweens to each rate through its tracked animation" counterTweens
        case "the CPU alert fades in on a spike and out after it" alertFades
        case "the details panel shows the latest sample and fades out when hidden" detailsToggle
        case "reconnect restarts the stream: the content dims until the next sample, then starts over" reconnectRestarts
    ]
