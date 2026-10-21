# Arms.Match: measurements

Measurements of the `Arms.Match` implementation ([spec](../superpowers/specs/2026-10-21-arms-match-design.md),
[plan](../superpowers/plans/2026-10-21-arms-match.md)). Fable 5.18.0, net10.0, Node 26.

## Plugin discovery

From `experiments/Ranvier.View.Arms/Checks/Discovery`, an app with a project reference to `Ranvier.View.Arms` and an
import of `Ranvier.View.Arms.targets`:

- **Discovery through a project reference and the `.targets`:** the plugin lowers the app's `Arms.Match` call; the
  emitted `Main.js` holds the `armsValues_L…` prologues.
- **`FABLE_COMPILER` during project cracking:** set; the `.targets` reference is conditional, and a .NET build carries
  no plugin assembly.
- **netstandard2.0 plugin in Fable 5.18:** loads (Fable.AST 5.0.0 compile reference, FSharp.Core 10.1.203).
- **Server HTML:** the three states render byte-identical HTML on .NET (quotation path) and under Fable (plugin path).
- **Fable.AST in the compilation:** the plugin attribute on `Arms.Match` derives from `MemberDeclarationPluginAttribute`,
  so the compilation that holds `Arms.fs` references `Fable.AST.dll` as well; without it Fable reports FS0074 at the
  attribute and skips the plugin assembly ("`ScanForPluginsAttribute` is required here and is unavailable"). The plugin
  project copies `Fable.AST.dll` beside its own assembly, and the `.targets` references both.
- **References and defines of referenced sources:** Fable compiles `Ranvier.View.Arms` with the app's assembly
  references and the app's `DefineConstants`, not those of `Ranvier.View.Arms.fsproj`. With the `.targets` imported by
  the app alone, `RANVIER_ARMS_PLUGIN` reaches `Arms.fs`; an app without the import (`Tests/NoTargets`) fails at its
  call with FS0101 and the AM005 text.
- **MSBuild targets during cracking:** Fable's project cracking runs a target declared
  `BeforeTargets="ResolveAssemblyReferences"`; the `.targets` check for a missing plugin assembly fires under
  `dotnet fable`.
- **`dotnet fsi build.fsx -- test` cleans the plugin's `bin` folder** with every other; the plugin is rebuilt after it.
