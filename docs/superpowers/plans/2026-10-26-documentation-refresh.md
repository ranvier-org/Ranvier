# Documentation refresh implementation plan

**Goal:** Make the published documentation concise, example-led and accurate for Ranvier.View, with no Partas.Solid dependency.

**Architecture:** Keep the Nacara site, existing Ranvier.View signal maps, compiled documentation samples and link validator. Replace the final Solid landing example with a Ranvier.View map. Preserve historical research as historical evidence.

**Scope:** The user's eight requested steps, executed in this lane through merge and publication.

- [ ] Remove the Solid site plugin, package feed entries, assets and example fences; use the existing map renderer.
- [ ] Audit user guides, concepts, view/server guides, package READMEs and contributor instructions against current source and compiled samples.
- [ ] Replace repetitive explanations with concrete examples, observable output and links to the owning reference page.
- [ ] Add diagrams and interactive maps where they explain dependencies, ownership, rendering or async state.
- [ ] Make a second prose pass and validate internal links, anchors and external destinations.
- [ ] Build the site and compiled samples; run relevant full test suites and browser checks.
- [ ] Review, push, merge, dispatch the docs workflow and verify the deployed revision and public pages.

**Verification focus:** Missing live maps after plugin removal; stale claims about the DOM layer; examples missing activation or disposal; package versions differing from current releases; links to renamed pages or unavailable anchors.
