/// <summary>Deterministic stepping of anime's engine, for tests that control the clock.</summary>
/// <remarks>
/// anime reads time from <c>Date.now</c>, captured when <c>animejs</c> is first imported: a test mocks <c>Date</c>
/// before that import, calls <c>manual</c>, then advances the mocked time and calls <c>step</c>.
/// </remarks>
module Ranvier.Anime.Testing

/// <summary>Stops the engine ticking on animation frames and on document visibility, leaving ticks to <c>step</c>.</summary>
let manual () =
    Engine.settings.useDefaultMainLoop <- false
    Engine.settings.pauseOnDocumentHidden <- false

/// <summary>Advances every running tickable to the current time.</summary>
let step () =
    Engine.settings.update ()
