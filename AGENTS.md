# FollowFormation working instructions

- This folder is the user-selected source of truth. Do not switch to another copy or archive based on timestamps.
- Main code: `FollowFormation.cs`; terrain/navigation projection: `TerrainProjection.cs`. Consult `README.md` for behavior relevant to the requested change; do not reread all documentation for every edit.
- Implement the requested behavior with minimal scope. Search relevant source first; exclude `bin`, `obj`, unrelated mods, archives and full logs unless needed. Inspect game assemblies only to resolve a specific API uncertainty; do not guess signatures.
- Preserve server-only compatibility, native Follow behavior, external command precedence, player-driven exclusions, and cleanup unless the requested change requires otherwise.
- The user handles tests and live verification. Do not run or add tests, or start/stop the server unless explicitly requested. Existing `Tests` fixtures are documented as older than version 1.5.0.
- After code changes, compile from this folder using:
  `dotnet build Si_Formation.csproj -c Release --ignore-failed-sources "-p:ServerRoot=D:\SteamLibrary\steamapps\common\Silica Dedicated Server"`
  The renamed project's default ServerRoot resolves two directories up to the server; the explicit argument keeps the build target unambiguous. Avoid the interactive `Build.cmd` wrapper.
- Output: `bin/Release/netstandard2.1/Si_Formation.dll`. Compilation does not verify in-game behavior. For documentation-only edits, no build is needed.
- Fix build errors caused by the change and rebuild; stop after a successful build. If blocked by dependencies, report the specific blocker without installing tools or changing unrelated settings.
- Finish briefly: what changed, build result, and a short focused checklist for the user's in-game verification. Keep confirmed API discoveries or changed behavior in existing documentation only when useful for future work; avoid session transcripts.

## Shared project context

- Explicitly read `../../Silica-Shared-Context/AGENTS.md`, MOD_REGISTRY.md and only relevant API_CONTRACTS.md / DEPENDENCIES.md sections. These sibling instructions are not automatically inherited. If inaccessible, report the missing context.
- Use shared search ID `formation`, adding `ranks`, `logistics`, `server-ui`, `formation-tool` or support IDs when relevant; inspect original source before decisions. Ranks' owner-scoped policy consumer, editor JSON and ModUI exports are documented cross-project relationships; vector similarity alone proves none.
- After authorized changes, review dependency/API impact, update verified local facts, propose shared changes and refresh affected index IDs. Preserve the existing test/build/server restrictions above. Do not commit across repositories or push without explicit authorization.
