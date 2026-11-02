# Native Ranvier scope: current authority

User scope reset: deliver a lean native Ranvier webview on WebCore with full native application abilities, without WebFrame/browser-platform bloat. This supersedes every earlier all-board/deferred-API instruction in historical notes.

All95 Project2 items were individually reviewed from full bodies. Independent live readback confirms exactly40 retained native items and55 archived exclusions;48 items newly archived,42 issues newly closed NOT_PLANNED,7 older archived exclusions unchanged, no unknown/new items. Core database185 and Node-jitless209 remain separate open backlog outside Project2; undefined mirror218 and Views-removal54 remain paused outside Project2. No excluded feature is claimed implemented.

Sixteen retained group/AOT issue titles and bodies now explicitly define the native boundary and preserve old wording as historical context. Core291 targets real native View/Interop strict AOT, not exhaustive optional browser-facade rooting or a generic pretty-print framework. Media190/191/195 are one optional native presentation/lifetime/OS-action boundary; decoding, networking, Web Audio, MSE/DRM/casting and portable media facade packages are excluded.

## Retained native items

- [ranvier-org/Ranvier#291](https://github.com/ranvier-org/Ranvier/issues/291): Make native Ranvier View/Interop applications pass strict NativeAOT
- [ranvier-org/Ranvier.View#14](https://github.com/ranvier-org/Ranvier.View/issues/14): bug(analyzers): fixed SDL byte buffers crash FCS expression traversal
- [ranvier-org/Ranvier.View#48](https://github.com/ranvier-org/Ranvier.View/issues/48): CanvasExport and Snapshots duplicate the image encoding map
- [ranvier-org/Ranvier.View#51](https://github.com/ranvier-org/Ranvier.View/issues/51): Four .fsx files are not fantomas-formatted: build.fsx, PluginTests.fsx, and two under .claude/skills/fsharp-xml-docs
- [ranvier-org/Ranvier.View#53](https://github.com/ranvier-org/Ranvier.View/issues/53): Paused: size trimming (NativeAOT and IL trimming size)
- [ranvier-org/Ranvier.View#65](https://github.com/ranvier-org/Ranvier.View/issues/65): Finish independent page cleanup phases after teardown failures
- [ranvier-org/Ranvier.WebCore#70](https://github.com/ranvier-org/Ranvier.WebCore/issues/70): A11: View transitions
- [ranvier-org/Ranvier.WebCore#120](https://github.com/ranvier-org/Ranvier.WebCore/issues/120): Maintain a matching Windows GPU package cohort for native previews
- [ranvier-org/Ranvier.WebCore#146](https://github.com/ranvier-org/Ranvier.WebCore/issues/146): ResizeObserver device-pixel-content-box in the native engine (follow-up to G10)
- [ranvier-org/Ranvier.WebCore#156](https://github.com/ranvier-org/Ranvier.WebCore/issues/156): Linux (WPE): drops expose no files; DataTransfer::allowsFileAccess() is false for every drag
- [ranvier-org/Ranvier.WebCore#157](https://github.com/ranvier-org/Ranvier.WebCore/issues/157): D02: D02: Native live-DOM mutation observation
- [ranvier-org/Ranvier.WebCore#162](https://github.com/ranvier-org/Ranvier.WebCore/issues/162): D15: D15: Native control picker integration
- [ranvier-org/Ranvier.WebCore#169](https://github.com/ranvier-org/Ranvier.WebCore/issues/169): T05: T05: Native editing undo transactions
- [ranvier-org/Ranvier.WebCore#171](https://github.com/ranvier-org/Ranvier.WebCore/issues/171): T08: T08: Native IME and text-service integration
- [ranvier-org/Ranvier.WebCore#172](https://github.com/ranvier-org/Ranvier.WebCore/issues/172): T10: T10: Native spelling-provider and editor integration
- [ranvier-org/Ranvier.WebCore#176](https://github.com/ranvier-org/Ranvier.WebCore/issues/176): R13: R13: Native compositor and presentation integration
- [ranvier-org/Ranvier.WebCore#177](https://github.com/ranvier-org/Ranvier.WebCore/issues/177): R14: R14: Native printing and PDF output
- [ranvier-org/Ranvier.WebCore#180](https://github.com/ranvier-org/Ranvier.WebCore/issues/180): I05: I05: Native gesture and force-input integration
- [ranvier-org/Ranvier.WebCore#182](https://github.com/ranvier-org/Ranvier.WebCore/issues/182): I10: I10: Native accessibility platform adapters
- [ranvier-org/Ranvier.WebCore#186](https://github.com/ranvier-org/Ranvier.WebCore/issues/186): H09: H09: Native paint, style and layout tracing
- [ranvier-org/Ranvier.WebCore#187](https://github.com/ranvier-org/Ranvier.WebCore/issues/187): H10: H10: Native host and renderer error records
- [ranvier-org/Ranvier.WebCore#190](https://github.com/ranvier-org/Ranvier.WebCore/issues/190): B01: B01: Native media/audio playback integration
- [ranvier-org/Ranvier.WebCore#191](https://github.com/ranvier-org/Ranvier.WebCore/issues/191): B02: B02: Native video surface and playback integration
- [ranvier-org/Ranvier.WebCore#195](https://github.com/ranvier-org/Ranvier.WebCore/issues/195): B07: B07: Native OS media-session integration
- [ranvier-org/Ranvier.WebCore#199](https://github.com/ranvier-org/Ranvier.WebCore/issues/199): B13: B13: Native window and display integration
- [ranvier-org/Ranvier.WebCore#210](https://github.com/ranvier-org/Ranvier.WebCore/issues/210): Linux host does not register the WPE image GResource, so built-in icons are empty
- [ranvier-org/Ranvier.WebCore#211](https://github.com/ranvier-org/Ranvier.WebCore/issues/211): macOS: WTF RunLoop::Timer CF timers still wait for kernel delivery (residual of #155)
- [ranvier-org/Ranvier.WebCore#212](https://github.com/ranvier-org/Ranvier.WebCore/issues/212): abi_layouts missing for RvImageDataLayout, RvImageBitmapOptions, RvDomKeyframeAnimationOptions
- [ranvier-org/Ranvier.WebCore#213](https://github.com/ranvier-org/Ranvier.WebCore/issues/213): burn.sh pinned mode loses exit codes
- [ranvier-org/Ranvier.WebCore#215](https://github.com/ranvier-org/Ranvier.WebCore/issues/215): Managed AOT R07 intermittent timeout (1 in 2 full runs)
- [ranvier-org/Ranvier.WebCore#216](https://github.com/ranvier-org/Ranvier.WebCore/issues/216): Paused: macOS 15 support
- [ranvier-org/Ranvier.WebCore#217](https://github.com/ranvier-org/Ranvier.WebCore/issues/217): Linux GPU flavor and compositing
- [ranvier-org/Ranvier.WebCore#219](https://github.com/ranvier-org/Ranvier.WebCore/issues/219): sccache for native CI compile caching
- [ranvier-org/Ranvier.WebCore#220](https://github.com/ranvier-org/Ranvier.WebCore/issues/220): macOS GPU flavor and compositing (spike first)
- [ranvier-org/Ranvier.WebCore#224](https://github.com/ranvier-org/Ranvier.WebCore/issues/224): macOS: indeterminate <progress> animation reads the system Reduce Motion setting instead of the page preference
- [ranvier-org/Ranvier.WebCore#227](https://github.com/ranvier-org/Ranvier.WebCore/issues/227): Linux GPU: WHEN_NEEDED mode paints animated layers white after dynamic compositing
- [ranvier-org/Ranvier.WebCore#229](https://github.com/ranvier-org/Ranvier.WebCore/issues/229): build cache: canonical Git hashes hide raw source changes and reuse invalid PCH
- [ranvier-org/Ranvier.WebCore#230](https://github.com/ranvier-org/Ranvier.WebCore/issues/230): Fractional-DPR content clipping shifts a bordered content box by one pixel (blocks #146 parity)
- [ranvier-org/Ranvier.WebCore#232](https://github.com/ranvier-org/Ranvier.WebCore/issues/232): Failed SVG images paint the HTML broken-image icon when resources are available
- [ranvier-org/Ranvier.WebCore#243](https://github.com/ranvier-org/Ranvier.WebCore/issues/243): Guard reentrant host destruction and mutation admission during teardown

## Delivery and active work

- Project2 is NOT complete. Required native platform, rendering, interaction/accessibility, JIT/AOT, release and actual public-consumer gates remain.
- Native preview3 exactce24 TEN public packages independently verified against actual producer ZIP members, hashes, clean manifests and official repository signatures; fresh public HEAD200. Linux x64/ARM64 each97 groups including9EGL frames and64context sentinels; producer total769groups. All CI watchers terminal. Receipt native-preview3-ten-public-audit.json. Managed Core288/View publication still on Root HOLD for actual native AOT source fixes and exact final heads.
- HOLD managed Core288/View publication pending current real native AOT source fixes and exact head review; implementation proceeds independently of CI. Native preview4 eebd View JIT175, GPU/UIA30 reopen, A11 and P3false controls pass; strict AOT remains RED.
- Native Mounting commitc6d9 removes all3 IL3054 with zero boxing/System.Tuple heap constructors; final normal/traced backend784pass/12ignored each and Fable159sourcecompile pass. Windows port1c9d clean/pushed. Linux SameValueZero candidate2 actualnative35/strictcompile0warnings proven; minimal3lookup source edit approved and fullproductgates underway. No full native AOT success yet.
- Minimal native Core future base e982 = first78e+D02AAD2+point fix. Four private normal/traced Dom/Fable packages exclude all new CryptoKey/UTF8 facade sources and embedded compile includes, preserve existing legacyCryptoService+D02; source unchanged and67 D02 tests pass. No public version/pin allocation. Its worktree archived after QA reader release.
- First native View release excludes4 optional Anime IDs after reviewed publisher filter; proposed cohort22 =10Native+4Dom+8View instead of26. Filter source/aggregate verification is being completed; no public publication claim.
- GPU/API agent now researches native177 print layout/output only; aot_dom owns private mount fix/Core actualroots; Linux owns SameValueZero; Windows owns P4platform proof; acceptance owns immutable audits/merges/publication; root coordinates. All six lanes assigned.

## Cleanup and source safety

- Closed crypto/CBC/H12 research is preserved on pushed/archive branches and compact receipts; no new related feature work. Their disposable worktrees/processes/leases are cleaned after reader release.
- Root retired unrelated209: all baseline leases/children exited; managed tree archived and owned WSL clone removed118698539logical bytes. Root active checkout is C:/Users/shaya/.codex/worktrees/project2-native-coordination/Ranvier; original user checkout untouched except authorized handover updates.
- Protected incremental WebCore sites, objects/PCH, shared Git objects and shared daemon9516 stay untouched. Protected orphan74720/sessione7d78966 remains explicitly DO NOT TOUCH pending established ownership; do not call it cleaned.
- Own stalled Sage sessions must stop through the documented dashboard route on actual port37750, not by repeatedly killing respawned workers. Verify exact own session/worker identities and NoSession. No shared daemon restart without user authorization.

Audit evidence: scope-audit-2026-11-02/{approved-all95.json,after-board.json,after-independent-verification.json,mutation-journal.jsonl,native-scope-updates/verified-updates.json}. Source commitments/release history in older resume/handover are historical, not instructions to resume excluded work.
