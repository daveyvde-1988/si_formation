# Formation
Purpose and functions | Silica game 1.0 | Mod version 3.8.1 | 12 November 2026

## Why the mod exists
Formation tunes the game's native formation system so mixed-size units have usable spacing, production units have reserved rally destinations, and FPS followers reposition predictably. It also offers independent grouped movement speeds, separately tuned Human/Alien cohesion, an infantry travel-cover switch and a one-shot scouting action. The server owns these settings; players need no client mod for gameplay.

## Commander placement and movement
- **Click formations.** New destination grids blend each unit's footprint with the selection's largest footprint. Commander spacing and large-unit influence are adjustable, while physical clearance remains a lower bound. Only eligible AI units and valid projected positions are used.
- **Dragged formations.** A short valid drag uses a compact rectangle's minimum column footprint instead of a deep single file; wider drags expand the frontage. Packing retains native facing, unit assignment order and formation priority bands, with body clearance. A complete accepted drag establishes the only reusable intended shape. The native client preview can look different.
- **Alien spacing.** A separate Alien scale applies to configured commander, FPS and rally spacing without editing unit definitions or body radii. Physical footprint clearance remains in force even with a low scale.
- **Movement fix.** New grouped commander Move/Attack orders can avoid cohesion slowdown, allowing units to use their own speeds. This is scoped to those orders. Explicit speed modes, combat, terrain and pathfinding remain native; the switch does not disable FPS layouts.
- **Travel cover preference disabler.** ON by default, it disables human infantry long-path cover-waypoint substitution/pruning while the Formation master is enabled. OFF restores native travel preference. The native movement-area assignment, including the smaller Stealth area, is retained. Combat decisions, combat state and cover claims are not patched. Changes affect newly calculated/recalculated paths, without reissuing orders or immediately rebuilding current paths.
- **Group cohesion.** Independently enabled/tuned for Humans and Aliens, both OFF by default. One profile interpolates from zero travel distance to MaximumRange, default 5000m. Arrival pacing and eligible remembered-shape holding have separate strength controls; both weaken with distance. Beyond the maximum, neither custom pacing nor holding applies. Within range, an ordinary click can rotate and translate an intact dragged shape toward travel; there is no Close/Medium/Long split. Loss, spreading, lag, stalls and bounded delay budgets can permanently release pacing to native travel. See [the complete settings and debug guide](Group_Cohesion.md).
- **Major turns.** New drags and remembered-layout clicks weaken pacing for the whole command as facing changes beyond 15 degrees. At 90 degrees or more, holding is disabled, arrival strength is 15% of the original distance-profile value, and the custom speed floor is at least 80% of uncapped requested-mode speed. Native movement can still be slower. A later straight move captures ordinary strengths again. No settings are added.
- **Destination arrival radius.** A dispersed or oversized selection can coordinate only eligible already-selected ground units within 500m of the clicked destination or drag midpoint at issue time, nearest first up to its faction's unit limit. This local cohort uses arrival pacing without holding and never waits for distant selected units or counts their losses; MaximumRange still applies. Farther ground units receive independent rear-band destinations; aircraft remain over the local formation's rear. Each faction can adjust its radius or set 0 to disable this fallback. Membership does not grow when other units enter the radius later.
- **Aircraft placement.** Air units no longer occupy ground cells or affect ground dimensions. They use separate centered rear rows, with native flight height and movement modes retained. This applies to commander click/drag placement independently of cohesion switches.
- **Shape invalidation.** Death, player takeover, team/eligibility changes, selecting a subset, adding members or merging groups invalidate the whole remembered layout, including heading and offsets. Survivors receive fresh normal placement on the next ordinary click, while current native orders remain intact. Missing units do not leave saved gaps. A new complete drag can define a new reusable shape.

Only drag-derived intention is remembered: ordinary click grids, current physical arrangements, destination-local cohorts and rally parking never seed this cache. An intact dragged layout is refreshed while any captured native Move remains current or queued, even after speed pacing releases. Natural completion starts ordinary retention instead of immediately forgetting the shape. Replacement cleanup can affect only the snapshot it observed, so an older command cannot erase newer intention. Turning an intact remembered shape uses arrival pacing during its allowance before eligible holding; failing shapes remain in Arrival.

New orders prepare provisional caps immediately, but startup failure accounting waits for a fresh usable path associated with the captured Move and the existing grace, default 3 seconds. Fresh paths rebase provisional ETAs once rather than continually postponing a deadline for stuck units. Temporary jumping, climbing or falling pauses an enrolled member's cap without dropping the same captured order; genuine death, takeover and replacement still release ownership.

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
Existing spacing controls accept whole numbers; cohesion controls also accept decimal values. Spacing percentages are tuning inputs, not literal percentages of the original distance.

| Command / setting | Default | Accepted values and purpose |
| --- | --- | --- |
| /formation enabled on / off | ON | Master switch; /formation on / off also works |
| /movementfix on / off / status | ON | Remove grouped commander cohesion slowdown |
| /travelcoverdisabler on / off / status | ON | ON disables infantry travel-cover preference; combat decisions/claims remain native |
| /groupcohesion on / off / status | OFF | Enable adaptive ground-move cohesion; suppress native slowdown while enabled |
| /aliengroupcohesion on / off / status | OFF | Separate Alien cohesion switch |
| /cohesion key number / status | Profile defaults | 12 Human controls for one continuous near/far distance profile |
| /aliencohesion key number / status | Profile defaults | Separate Alien controls with the same keys |
| /cohesion MaximumRange n | 5000m | 1–100000m; farther commands receive no custom pacing or holding |
| /aliencohesion MaximumRange n | 5000m | Separate Alien maximum distance with the same bounds |
| /cohesion DestinationRadius n | 500m | 0–10000m; Human destination-local fallback radius; menu step 25m; 0 disables fallback |
| /aliencohesion DestinationRadius n | 500m | 0–10000m; separate Alien fallback radius; menu step 25m; 0 disables fallback |
| /cohesiondebug next / groups / id / on / off | OFF | Bounded native dispatch, target/state and requested/applied-cap traces |
| /formationspace n | 32% | 0-100; commander click/drag spacing |
| /rallyspace n | 25% | 0-100; newly produced unit parking |
| /sizeinfluence n | 30% | 0-100; individual versus largest footprint |
| /alienspread n | 50% | 0-100; Alien share of configured spacing |
| /fpsspace n | 32% | 0-100; FPS follower spacing |
| /fpsdistance n | 10% | 0-100; FPS leader standoff scale |
| /fpsallowance n | 20m | 1-100m; ordinary reposition allowance |

Spacing/distance multiplier: 1 + percentage/15 through 30%, then 3 + (percentage-30)/10. For example, 10% is about 1.667x, 25% about 2.667x and 32% is 3.2x. FPS standoff is the larger of (5m + leader radius) times that multiplier, or leader radius + largest follower radius + 1m. Body clearance can therefore increase the result.

Each faction exposes MaximumRange, DestinationRadius and five Near/Far pairs: arrival `Strength` 1→0.05, `HoldStrength` 1→0, `MinimumSpeed` 0.1→0.95, `ArrivalWindow` 0→15s and `MaxDelay` 30→0.5s. Near means zero travel distance; Far means MaximumRange. Values interpolate continuously and are captured for new moves. Existing common watchdog/debug settings remain in cfg and the generic command, hidden from the ordinary menu.

## Read-only status and scouting
- **/formation or /formation status:** public report of settings, active features, rally reservations, FPS groups and the current commander's scouting state. Reading status does not save settings, calculate layouts or issue movement orders. The standalone tuning commands also accept status for admins.
- **/formation advanced:** explains the single distance profile, separate arrival/hold strengths and focused debug tracing.
- **/scout, /scout on, /scout off, /scout status:** commander action; no admin power required. Arm scouting, select at least five eligible AI units, then issue an immediate normal move with no target, attack, forced order or queue. That qualifying click is consumed once; its position anchors the selection remainder's normal move.

Scouting distributes up to 75 eligible units among map sectors, including supported Alien air units. Bounded checks use each unit's actual graph and traversable tags, terrain and destination separation. Remaining normally movable selected units—including failed/excess scouts—receive normal formation destinations around the original click. Protected or navigation-unready units remain unchanged. Native acceptance can still fail and outcomes are reported. Fewer than five eligible units use the normal move instead. This is a one-time spread, not an automatic patrol.

## Saved server settings
MelonPreferences stores UserData/cfg/Formation/si_formation_native.cfg in category [Si_Formation_Native]. Admin changes save immediately. Existing settings load at initialization and are validated. Cohesion values must be finite; out-of-range loaded numbers are clamped and reversed endpoint pairs restore affected defaults with warnings. Edit the file while the server is stopped; there is no reload command.

Human settings use `GroupCohesion` / `Cohesion<Key>`, and Alien settings use `AlienGroupCohesion` / `AlienCohesion<Key>`. Faction switch values and existing common safety settings are preserved. New single-profile keys include `CohesionMaximumRange`, `AlienCohesionMaximumRange`, `CohesionStrengthNear` and `AlienCohesionHoldStrengthFar`. Old Close/Medium/Long boundaries and endpoints are ignored, without automatic conversion into the new profile. `TravelCoverPreferenceDisabler` remains a separate shared boolean defaulting to `true`.

The source folder is now si_formation. Keep the native configuration filename/category unchanged so existing settings remain in use. The old si_formation.cfg, blueprint catalogs and client personal preferences are not read or migrated. No personal-settings restoration is enabled.

<!-- pagebreak -->

# Operation and compatibility
The native implementation is the current Formation source for game 1.0. The folder migration preserves the project, assembly and runtime configuration identities.

## Optional menu and permissions
The schema-2 ModUI export provides one public Formation Status action and 42 total rows. Admin controls form contiguous General, Commander placement, FPS squads, Rally, Human cohesion, Alien cohesion and Diagnostics groups. Each faction has an independent switch and 12 numeric controls. Debug actions are shared; the travel-cover checkbox is under Commander placement. Existing grouping metadata supports collapse on a compatible bridge. ClientModUI is unchanged.

Compatible ServerModUI/ClientModUI bridges can render the groups as collapsible sections and typed sliders. The existing generic `controlPresentation` compatibility export is retained for updated clients using an older companion that drops command-level `ui` hints. Older clients may expose simpler controls. Gameplay does not require these optional UI companions. Version 3.8.1 adds no further ClientModUI change, client-preview code, network protocol or extra UI polling loop. Dynamic speed-cap presentation on remote clients still needs live verification.

Watched/global diagnostics distinguish native order attempts and acceptance from cohesion enrollment, show target transformations/order state, and record requested versus actually applied caps with skip reasons. Periodic detail is bounded to 32 rotating members per global group or up to 256 for an explicitly watched group. Zero cohesion enrollment alone does not establish native Move failure.

## Native behavior remains authoritative
Only live movable AI units with usable order/navigation components are eligible. Player-controlled, driven or embarked units retain their own control. An enrolled cohesion member may temporarily jump, climb or fall while its cap is paused; continuing the same captured order preserves membership. FPS followers must also belong to a valid noncommander player's own group; native transfers and forced exits are excluded.

Terrain projection checks map bounds and navigation, including the desired climbing surface where appropriate. It validates destination candidates; it does not guarantee a clear route or replace collision avoidance. Invalid or crowded destinations may wait, retry or be skipped. Native previews can differ from tuned placements.

## Switches and cleanup
Disabling the master releases cohesion/FPS/rally ownership, clears pending scouting, restores owned aircraft height and hands pending production back to native rally handling. Destinations already issued remain native orders. It also leaves native infantry travel-cover postprocessing in control for subsequent path calculations. MovementFix is a separate sub-switch for future grouped commander orders. Switching one faction's cohesion OFF immediately removes only that faction's owned state/caps; enabling it replaces native slowdown for that faction even if MovementFix is OFF.

Rally reservations, FPS ownership and scout arming are temporary. Round start/end, scene unload and mod unload clear them; scout arming also clears on commander/team changes, with disconnected players removed. Persistent server tuning is separate from that temporary state. Initialization rejects overlapping Formation commands, and a failure deactivates the mod and unpatches its Harmony patches.

## What changed from legacy Formation
The former 2.16.2 implementation is preserved separately. Native 3.8.1 has no legacy FormationApi, rank-controlled recruitment policies, custom-blueprint catalog or Formation Tool consumer. Ranks' legacy policy adapter cannot apply its policies here. AlienOptimizer's old reflected placement/scout bridge is absent; the inspected guarded consumer uses its corresponding native fallback. Cohesion reuses the legacy lock's longitudinal pacing idea without restoring those APIs.

Load only one Formation DLL. Both implementations use Si_Formation.dll and overlapping commands. The native project uses default recursive C# inclusion, so legacy source and tests must stay outside the active project.

## Build and source basis
Keep the repository directly under the server's Silica-1283 folder. Its ServerRoot resolves two directories upward, so renaming si_formation_native to si_formation does not change the dedicated-server reference paths. Build from the project folder with dotnet build Si_Formation.csproj -c Release --ignore-failed-sources. Output: bin/Release/netstandard2.1/Si_Formation.dll.

Prepared 12 November 2026 for native 3.8.1. The required dedicated-server Release build passed with 0 warnings and 0 errors; the candidate is staged, not installed. No tests, live gameplay checks or game/server lifecycle actions were run. Compilation does not establish gameplay or multiplayer presentation. See [Changes_3.8.1.md](Changes_3.8.1.md) for current changes, [Group_Cohesion.md](Group_Cohesion.md) for controls and [MIGRATION.md](MIGRATION.md) for historical archive/API details. The overview PDF remains a historical 3.4.4 snapshot; the group cohesion quick guide covers 3.8.1.

User observations on 12 November described **3.8.0** cohesion as subtle/natural, short-move holding as desirable, long moves as having no noticeable added delay, and rectangle-start dragging as useful. They also identified native-preview misalignment and a need for smoothing. These are qualitative observations, not measured tests or 3.8.1 validation. Preview/smoothing client work is deferred.
