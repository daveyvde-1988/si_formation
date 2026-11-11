# Formation
Purpose and functions | Silica game 1.0 | Mod version 3.4.4 | 11 November 2026

## Why the mod exists
Formation tunes the game's native formation system so mixed-size units have usable spacing, production units have reserved rally destinations, and FPS followers reposition predictably. It also offers independent grouped movement speeds and a one-shot scouting action. The server owns these settings; players need no client mod for gameplay.

## Commander placement and movement
- **Click formations.** New destination grids blend each unit's footprint with the selection's largest footprint. Commander spacing and large-unit influence are adjustable, while physical clearance remains a lower bound. Only eligible AI units and valid projected positions are used.
- **Dragged formations.** Tuned packing retains the native drag width, facing, unit order and formation priority bands. Unit bounds influence the rows and columns. The server changes destinations; the native client preview can look different.
- **Alien spacing.** A separate Alien scale applies to configured commander, FPS and rally spacing without editing unit definitions or body radii. Physical footprint clearance remains in force even with a low scale.
- **Movement fix.** New grouped commander Move/Attack orders can avoid cohesion slowdown, allowing units to use their own speeds. This is scoped to those orders. Explicit speed modes, combat, terrain and pathfinding remain native; the switch does not disable FPS layouts.

## Production rally parking
Newly produced units reserve destinations before dispatch, including vehicles still travelling to their parking spaces. Ground parking considers terrain, navigation, other reservations, units and structures/sites. Aircraft use vertically separated destinations and searched height layers. Native factory dispatch and forced exits are retained.

The current rally scale is captured when a ticket is created. Searches are bounded and can retry when crowded; failed placements are not all sent to one common marker. Replacement orders, player takeover, team changes, destruction and cleanup release ownership. Owned preferred aircraft height is restored when the reservation ends.

## FPS Follow, Move and Attack
- **Follow and Attack.** Followers use a rearward two-column trail while moving and a fan/rings behind the player when stopped. Attack preserves its native combat target. Ordinary repositioning requires player displacement greater than the configured allowance, with exceptions for first placement, membership/settings changes and state transitions.
- **Move.** An explicit destination uses a footprint-aware grid. Completing that task does not automatically replace it with Follow. Native priority and instance ID order keep placement deterministic; invalid or crowded slots can be skipped.
- **Motion and control.** The observation interval is 0.25 seconds. Convoy entry requires departure greater than twice the movement allowance, with a 10m minimum. Stopping for 0.75 seconds returns to the fan. Accepted external orders release affected members; new group/Follow commands can restore managed placement. Player-controlled, driven and embarked units are excluded.

<!-- pagebreak -->

# Settings and commands
Configuration is shared by the server. Setting changes require a connected player with Admin Mod Generic permission, from either FPS or commander role. These handlers do not support server-console setting changes.

## Current defaults
Numeric controls accept whole numbers. Spacing percentages are tuning inputs, not literal percentages of the original distance.

| Command / setting | Default | Accepted values and purpose |
| --- | --- | --- |
| /formation enabled on / off | ON | Master switch; /formation on / off also works |
| /movementfix on / off / status | ON | Remove grouped commander cohesion slowdown |
| /formationspace n | 32% | 0-100; commander click/drag spacing |
| /rallyspace n | 25% | 0-100; newly produced unit parking |
| /sizeinfluence n | 30% | 0-100; individual versus largest footprint |
| /alienspread n | 50% | 0-100; Alien share of configured spacing |
| /fpsspace n | 32% | 0-100; FPS follower spacing |
| /fpsdistance n | 10% | 0-100; FPS leader standoff scale |
| /fpsallowance n | 20m | 1-100m; ordinary reposition allowance |

Spacing/distance multiplier: 1 + percentage/15 through 30%, then 3 + (percentage-30)/10. For example, 10% is about 1.667x, 25% about 2.667x and 32% is 3.2x. FPS standoff is the larger of (5m + leader radius) times that multiplier, or leader radius + largest follower radius + 1m. Body clearance can therefore increase the result.

## Read-only status and scouting
- **/formation or /formation status:** public report of settings, active features, rally reservations, FPS groups and the current commander's scouting state. Reading status does not save settings, calculate layouts or issue movement orders. The standalone tuning commands also accept status for admins.
- **/formation advanced:** explains that travel-distance, departure-delay, prediction, cache and observation-interval settings are not adjustable in this implementation.
- **/scout, /scout on, /scout off, /scout status:** commander action; no admin power required. Arm scouting, select at least five eligible AI units, then issue an immediate normal move with no target, attack, forced order or queue. That qualifying click is consumed once and its position is ignored.

Scouting distributes up to 75 eligible units among map sectors, choosing nearby units and checking terrain, navigation connectivity and separation. Excess units remain untouched. Fewer than five eligible units use the normal move instead. Missing map navigation leaves selected orders unchanged; destination/order failures are reported. This is a one-time spread, not an automatic patrol.

## Saved server settings
MelonPreferences stores UserData/cfg/Formation/si_formation_native.cfg in category [Si_Formation_Native]. Admin changes save immediately. Existing settings load at initialization; invalid numeric values use built-in defaults. Edit the file while the server is stopped; there is no reload command.

The source folder is now si_formation. Keep the native configuration filename/category unchanged so existing settings remain in use. The old si_formation.cfg, blueprint catalogs and client personal preferences are not read or migrated. No personal-settings restoration is enabled.

<!-- pagebreak -->

# Operation and compatibility
The native implementation is the current Formation source for game 1.0. The folder migration preserves the project, assembly and runtime configuration identities.

## Optional menu and permissions
The schema-2 ModUI export provides one public Formation Status action. Admin controls include master/movement switches, seven numeric sliders and an explanation under Advanced controls. Commands use the same permission-checked handlers and current server values.

Compatible ServerModUI/ClientModUI bridges can render the three groups as collapsible sections and typed sliders. Older bridges may expose simpler controls. Gameplay does not require these optional UI companions. Formation does not add client preview code, a network protocol or an extra UI polling loop.

## Native behavior remains authoritative
Only live movable AI units with usable order/navigation components are eligible. Player-controlled, driven or embarked units retain their own control. FPS followers must also belong to a valid noncommander player's own group; native transfers and forced exits are excluded.

Terrain projection checks map bounds and navigation, including the desired climbing surface where appropriate. It validates destination candidates; it does not guarantee a clear route or replace collision avoidance. Invalid or crowded destinations may wait, retry or be skipped. Native previews can differ from tuned placements.

## Switches and cleanup
Disabling the master releases FPS/rally ownership, clears pending scouting, restores owned aircraft height and hands pending production back to native rally handling. Destinations already issued remain native orders. MovementFix is a separate sub-switch for future grouped commander orders.

Rally reservations, FPS ownership and scout arming are temporary. Round start/end, scene unload and mod unload clear them; scout arming also clears on commander/team changes, with disconnected players removed. Persistent server tuning is separate from that temporary state. Initialization rejects overlapping Formation commands, and a failure deactivates the mod and unpatches its Harmony patches.

## What changed from legacy Formation
The former 2.16.2 implementation is preserved separately. Native 3.4.4 has no legacy FormationApi, rank-controlled recruitment policies, custom-blueprint catalog or Formation Tool consumer. Ranks' legacy policy adapter cannot apply its policies here. AlienOptimizer's old reflected placement/scout bridge is absent; the inspected guarded consumer uses its corresponding native fallback. A source-folder rename does not restore these APIs.

Load only one Formation DLL. Both implementations use Si_Formation.dll and overlapping commands. The native source has 11 C# files and uses default recursive project inclusion, so legacy source and tests must stay outside the active project.

## Build and source basis
Keep the repository directly under the server's Silica-1283 folder. Its ServerRoot resolves two directories upward, so renaming si_formation_native to si_formation does not change the dedicated-server reference paths. Build from the project folder with dotnet build Si_Formation.csproj -c Release --ignore-failed-sources. Output: bin/Release/netstandard2.1/Si_Formation.dll.

Reviewed 11 November 2026 from native 3.4.4 source: Formations.cs, Spacing.cs, CommanderLineLayout.cs, MovementFix.cs, RallyFix.cs, FpsFormations.cs, FpsLayouts.cs, Scouting.cs, ServerSettings.cs, FormationMenu.cs and TerrainProjection.cs. Purpose is summarized from implementation. Folder migration and document creation changed no gameplay C# or installed DLL bytes; no new build or in-game verification was performed. See docs/MIGRATION.md for archive and API compatibility details.
