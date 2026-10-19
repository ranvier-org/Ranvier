# Mounted hooks, integrations, raw HTML and client document metadata

Date: 2026-10-19. Branch `poc/fable-ranvier-dom`. Task 7 of the [framework plan](../superpowers/plans/2026-10-06-fable-ranvier-framework.md#task-7--typed-dom-events-lifecycle-and-owned-async-helpers): mounted access hooks on one placement mechanism, `widget` and `lend` integrations, raw HTML, client-side document metadata, a generated listener inside an explicit batch, and the playground screen migration.

## Mutation checks

Each mutation was applied alone to the commit named (`78ea5b9`; the owner-cleanup row to the commit that adds the owner cleanup, the next three rows to the commit that adds `widget` and `lend`, and the last row to its first fix round), compiled, run against the named test file, and reverted.

| Mutation | Location | Test file | Killed by |
|---|---|---|---|
| Effects created at registration | `Placement.Add` | `placement.test.mjs` | tree synchronous: a mounted hook runs after the element is in the host and its cleanup runs before the element leaves |
| Content never placed | `ViewBoundary.Reveal` | `placement.test.mjs` | tree synchronous: hooks under a hidden boundary content run at reveal |
| Content placed at setup | `ViewBoundary.MountContent` | `placement.test.mjs` | tree synchronous: hooks under a hidden boundary content run at reveal |
| Nested mount placed by its own `Run` | `MountState.MountRow` | `placement.test.mjs` | tree synchronous: hooks run in setup order: an instance, its inlined children, then region rows |
| Pass never placed | `ListRegion` | `placement.test.mjs` | tree synchronous: hooks run in setup order: an instance, its inlined children, then region rows |
| Failure `try` dropped | `PlacedHook.register` | `placement.test.mjs` | tree synchronous: a hook failure during mountWith unwinds the mount and rethrows |
| `contentContainer.Discard ()` dropped | `ViewBoundary.TearDown` | `placement.test.mjs` | tree synchronous: a content torn down before its reveal leaves the held-hook count at its prior value |
| Owner cleanup of an unrevealed content dropped | `ViewBoundary.Start` | `placement.test.mjs` | tree synchronous: a content disposed while pending leaves the held-hook count at its prior value and runs no hook |
| No-children check removed | `Template.elementNode` | `integration.test.mjs` | an element with a widget part and template children fails at definition time |
| Release check removed | `PlacedHook.register` | `integration.test.mjs` | tree synchronous: an integration that moves its element is reported after its cleanup |
| Attach at setup | `MountState.Placed` | `integration.test.mjs` | tree synchronous: a widget renders into its element once placed and its destroy runs while the element is attached |
| Root exemption removed | `PlacedHook.register` | `integration.test.mjs` | tree synchronous: a lent mount root moved with its mount to another host is not reported |

Dropping either `contentContainer.Discard ()` call alone changes no hook's behaviour: a torn-down or disposed content's container is never placed again, and the core disposes an effect created under a disposed owner as it attaches. `Discard` releases the held closures, which the held-hook count (`PlacementCounters.held`, defined under `RANVIER_VIEW_COUNTERS`) observes.
