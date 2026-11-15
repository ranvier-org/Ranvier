# AGENTS.md

## Agent Source Comments

Use the plugin `comment-hygiene@roboz0r` to check for common mistakes in agent source comments.
If it is not setup in your environment, ask your USER for permission to set it up.

For `///` XML documentation comments, read the `fsharp-xml-docs` skill
(`.claude/skills/fsharp-xml-docs/SKILL.md`) before writing, reviewing or fixing them. It covers the
tag set and ships an audit script (`scripts/audit.fsx`) for FS3390 and convention checks.

Use scoped structural XML checks and an FS3390-enabled project build for documentation
validation. Do not run `audit.fsx --compiler`: even a four-file invocation has retained
32 GB of managed memory on this solution, and earlier attempts exceeded 49 GB. Treat this
as a tooling limitation; preserve the actual project build and analyzer gates.

## F# semantics — use `rfs`, not grep

For semantic lookups in `.fs`, `.fsi`, `.fsproj`, and `.slnx` files, use `rfs` (`Ranvier.FSharp.Agent`,
installed as `rfs` / `.mcp.json` still lists legacy `fslangmcp`, which `rfs` replaces). Reach for grep
only for prose, JSON, `.mts`/`.js` tooling, and other non-F# files.

- `rfs check` — fast incremental compiler diagnostics across dependent projects in memory. Always ensure
  `check` is clean before trusting a negative `refs` lookup or running `rename`.
- `rfs symbol` / `rfs refs` / `rfs search` — resolve symbol definitions and cross-project call sites by
  1-based line plus `--symbol <name>` rather than counting columns.
- `rfs rename --dry-run` — preview a symbol rename before applying.
- `rfs status --reload` — reload after `.fsproj`, `.slnx`, or `.props` edits; pass worktree-local paths
  (or `--workspace <dir>`) inside git worktrees. Ask the user before running `rfs daemon kill`.

## F# experiments — use `sagefs`; the build CLI stays the gate

The repo configures `sagefs` in `.mcp.json` (SageFs live F# REPL). Follow `.claude/skills/sagefs/SKILL.md`
for session setup (`create_session`, `get_fsi_status`, `switch_session`, `stop_session`) and Expecto REPL
helpers, applying these repository-specific limits:

- **No process spawning, file-path discovery, or Release-only checks in the REPL.** `use` inside `task { }`
  hits `MissingMethodException` against the NuGet `FSharp.Core` ([SageFs#141](https://github.com/WillEhrendreich/SageFs/issues/141)),
  `AppContext.BaseDirectory` / `Assembly.Location` resolve to SageFs host/shadow paths ([SageFs#142](https://github.com/WillEhrendreich/SageFs/issues/142)),
  and `inline` CE members can pass in Debug (REPL) while failing Release with `FS1118`.
- **Known bridge/check quirks.** Restart the client if tools drop after `initialize` ([SageFs#138](https://github.com/WillEhrendreich/SageFs/issues/138));
  ignore `sagefs check` SDK version mismatches from `global.json` roll-forward ([SageFs#139](https://github.com/WillEhrendreich/SageFs/issues/139));
  wait for `Ready` before `switch_session` ([SageFs#140](https://github.com/WillEhrendreich/SageFs/issues/140)). Ask the user before stopping or reinstalling the SageFs daemon.
- **Final gate through `build.fsx`.** Always finish with `dotnet fsi build.fsx -- test` and the analyzer gate:
  `dotnet fsi build.fsx -- analyze --group <library|examples> --build` (one run per group).
