# Correspondence

Each row maps a symbol of the model to the F# in `src/Ranvier/Core.fs` (pinned at commit
`d701e6c5`), and lists every simplification with its reason.

| Model symbol | Core.fs symbol | Simplification and reason |
|---|---|---|
| `nid` | node identity | `nat`; node ids are opaque. |
| `oid` | owner identity | `nat`; owner ids are opaque. |
| `val` | node value | `Z`; the model fixes one value type. |
| `fresh` | node freshness state | `Clean`, `Check`, `Dirty`; ordered `Clean <= Check <= Dirty`. |
