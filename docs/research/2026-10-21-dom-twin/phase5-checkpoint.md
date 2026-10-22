# Phase 5 finalization checkpoint (2026-10-22)

This checkpoint fixes the Phase 5 critical/important inspection findings and stops before Task 23. It is a draft checkpoint, not a merge-ready claim. The minor follow-ups are listed in the parent research note.

## Verification

- Persisted SageFs tests: Dump 16 and Inspect 9 passed. The owned session was stopped.
- Full Debug twin: 1,043 passed, 12 ignored, including normal golden verification after recording 38 text/JSON/HTML fixtures.
- Release twin with trace and counters: 1,044 passed, 12 ignored, unfiltered and sequential. The initial parallel run failed shared global-counter comparisons (1 failed, 34 errored); sequential execution prevents unrelated tests from changing those counters during assertions.
- Inspect jsdom conformance: 782 cases passed; all 92 shared artifacts compare byte-for-byte, including opaque classes with throwing methods/getters and canonical value kinds.
- Independent review approved the safe prototype classification and small-list elision fixes. Exception formatting additionally uses the trusted Fable exception prototype; the final host comparison covers its message.
- Whole solution Release build: zero errors, seven warnings. Unfiltered Release core 1,456, C# 93, and Wire 290 passed.
- Unfiltered traced Release core: 1,588 passed, 13 skipped, zero failures.
- Browser: all 31 Playwright tests passed after the app Release compile.
- Gate tooling: 12 tests passed; no-define inspection absence and the actual inspect positive control passed.
- XML compiler audit on the six edited F# files passed without FS3390. Six long-comment findings are unchanged preexisting lines. The comment-hygiene PowerShell runner was blocked by Windows execution policy; the static scan found no selected agent-comment signatures.

## Historical byte-equality gates remain blocked

The original `bundle-i1.json` baseline is retained. All six current bundle hashes differ from it. Gate 3 also rejects inherited shipping changes from `1ea136e3`: `src/Ranvier/{Core,Bindings,Projections,Streams,Timed}.js` status getters now include the stale flag; `Api.js` adds `createEffectNode`; Core also has a generated initialization-field line-number change. The exact `Mounting.js` exception passes against the pristine baseline. Existing conformance/harness files and `experiments/Ranvier.View.Testing/*` also differ from the historical baseline as expected for the expanded shared test suite; these are still rejected by the strict default allowlist.

Current SHA-256 values (measurement only, not a replacement baseline):

| Entry | SHA-256 |
|---|---|
| explicit | a8e64de7727749d118e5ba5110a6d146c9220e667497f40a7fa97f9b11680063 |
| explicit-combined | 6ee449c6c6890dfe26dab20ecc0ef6ef40ef3e5026c101934e79954e8939624d |
| sugared | 068ddbfe94115cd71fe13f3f68965d2f1546d21bdddc0ceed2f058f220492998 |
| members | 47721ac1fc4d5378c059c9fe933e8904cdbfa7c4139815ab58e26df0282645b3 |
| class | 349e3b808d20112f7481a2948103d4cdec25c84a5e1548eefa171bce4bf2fc56 |
| combined | d9ab3ed487801f8e32a01986726ec2933641bcbc2a59eb6399d6005783a483f9 |

Before merge: review an explicit exception or baseline decision for the public `Status.Stale`/`createEffectNode` changes, obtain the primitive instruction-count comparison, and pass CI. No historical hash or broad allowlist was silently replaced.
