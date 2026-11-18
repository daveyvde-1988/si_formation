# Native formation tuning — Si_Formation 3.18.0

This is the native implementation for Silica game 1.0. The mod version is **3.18.0**, and this `si_formation` folder is its canonical source. The legacy implementation is preserved on [legacy/pre-native-1.0-20261111](https://github.com/daveyvde-1988/si_formation/tree/legacy/pre-native-1.0-20261111) and in a separate local archive. The native implementation does not include the old public FormationApi or custom blueprint system.

- [Quick summary (3.16.2 PDF snapshot)](docs/Formation_Quick_Summary.pdf) | [Editable quick summary](docs/Formation_Quick_Summary.md)
- [Group cohesion settings, behavior and debug guide](docs/Group_Cohesion.md)
- [3.11.0 bounded waypoint regrouping and aircraft arrival](docs/Changes_3.11.0.md)
- [3.10.0 formation fixes, rear aircraft and waypoint barriers (historical)](docs/Changes_3.10.0.md)
- [3.9.2 nearby-destination rule switch](docs/Changes_3.9.2.md)
- [3.9.1 infantry movement during Reverse](docs/Changes_3.9.1.md)
- [3.9.0 casualty retention, reverse formations and advanced defense](docs/Changes_3.9.0.md)
- [3.8.2 tuned defaults](docs/Changes_3.8.2.md)
- [3.8.1 turn pacing, fresh-path startup, diagnostics and scouting](docs/Changes_3.8.1.md)
- [3.8.0 continuous distance profile and drag-only intended layouts](docs/Changes_3.8.0.md)
- [3.7.0 travel cover, intended layouts and local arrival cohorts](docs/Changes_3.7.0.md)
- [3.6.1 partial-selection reformation](docs/Changes_3.6.1.md)
- [3.6.0 rotation, Alien settings and air placement](docs/Changes_3.6.0.md)
- [3.5.1 startup correction](docs/Startup_Fix_3.5.1.md)
- [Detailed formation overview (3.16.2 PDF snapshot)](docs/Formation_Overview.pdf)
- [Editable detailed overview](docs/Formation_Overview.md)
- [Source migration and legacy archive](docs/MIGRATION.md)

## Menu and permissions

Regular players can read Formation Status. `/formation` and `/formation status` invoke the same read-only report, which aggregates the existing per-setting status handlers and reports active features, spacing, rally reservations, FPS groups and commander action state. Reading status neither saves configuration nor calculates or issues formations.

Tuning and cohesion debug commands require AdminMod Generic permission. Admins may change settings from either FPS or commander role; watching one's next move requires commander role. Existing slash syntax remains supported; the menu uses the registry's `!` spelling for admin commands. `/scout [on|off|status]` remains a commander action and is an alias of the shared player command callback, so it does not add a second Controls entry. It may still appear as an alias in the generic command reference.

The provider supplies collapsible groups. Updated ClientModUI clients also read the generic `controlPresentation` hints retained by the original Si_ModUI_Server 1.4.4, which discards the command-level `ui` metadata. Both the updated client and this provider export are required for categories with that companion; no companion replacement is needed.

- **General:** Formation master, pathfinding shortcut pass, Suppress native group slowdown, Travel cover preference disabler, Nearby destination cohesion and both faction cohesion switches.
- **Commander placement / FPS squads / Rally:** placement spacing and checkpoint waits, follower controls and production parking.
- **Human cohesion:** 12 numeric controls for one continuous distance profile.
- **Alien cohesion:** 12 separate numeric controls, Alien placement spacing and dedicated Lesser Spawn commander-air spacing.
- **Diagnostics:** global and per-group debug actions plus the generic `!aliencohesion`, `!cohesion` and `!waypointwait` command forms. Labels retain faction names and near/far endpoints for clients that do not consume grouping metadata.

The presentation compatibility export includes group-only hints for the three registry-generated command forms, preserving their argument controls. In-game accordion verification remains pending. No ClientModUI change or native-preview hook is included. Perimeter defense remains removed.

The schema-2 export contains **49 rows**, including the General pathfinding shortcut slider, nearby-destination switch and three checkpoint wait sliders. Both faction cohesion switches default ON and preserve saved values. Nearby destination cohesion defaults ON for both factions. Ordinary cohesion uses one profile from zero distance to `MaximumRange` (default **5000m for Humans and 5034m for Aliens**); longer ordinary commands use no custom pacing or holding. Reverse uses its own strict snapshot pacing. Near/Far mean the ordinary profile's zero-distance and maximum-distance endpoints, not different units. Old Close/Medium/Long boundaries and endpoint keys are ignored.

Compatible structured ServerModUI/ClientModUI versions can render the supplied groups as collapsible sections and use their slider ranges/descriptions. Older bridges that discard per-command `ui` hints can show ordinary admin value/choice controls without accordions or typed sliders. Rendering depends on the compatible UI bridge. Formation supplies no client gameplay mod or additional UI polling loop.

## Commander orders and player FPS squads

While Formation is enabled, commander orders exclude units currently in another player's FPS group on the same team. The server filters Move/Attack, Patrol and targeted-order selections before Formation, UnitAI and native formation processing, so protected members do not affect spacing, speed planning, waypoint membership or native cohesion detachment. A per-unit order guard also rejects commanded orders that bypass those batch routes. Automatic AI orders, the FPS owner's commands and Formation's internal FPS/rally/waypoint dispatch retain their existing handling.

Protection follows native `Player.Group` / `Unit.AIGroup` membership, independently of Formation's follow/task/slot tracking. It covers Follow, Move, Attack and stopped squads. When native ownership or membership ends, the unit becomes available to the commander again; no permanent exclusion list is maintained.

Native **Select All Units** builds its selection locally on each commander client, so FPS members can still appear highlighted. The server excludes them from commander order processing and leaves their FPS orders intact. Other selection tools retain native selection behavior, with the same server order protection. This feature requires only the server's `Si_Formation.dll`; there is no Formation client companion, additional setting or network dependency.

## Server configuration

Configuration is saved by MelonPreferences in `UserData/cfg/Formation/si_formation_native.cfg`, category `[Si_Formation_Native]`. Admin changes save immediately. Existing saved values take precedence; the defaults below match the active saved tuning captured on 16 November 2026. Perimeter defense is removed; old defense settings are ignored. The old `si_formation.cfg` and blueprint catalogs are untouched. Loaded cohesion numbers are clamped to valid bounds; nonfinite values and reversed endpoint pairs restore the affected defaults with warnings. Edit the file while the server is stopped.

This version uses one shared server configuration and does not opt into ClientModUI's personal preference restoration. Existing client preference files are neither read nor migrated, and regular-player replay attempts cannot change server settings. Pending scout actions remain transient commander state, not personal configuration.

| Setting | Command | Default | Valid values |
| --- | --- | --- | --- |
| Enabled | `/formation enabled on\|off` | true | on/off |
| MovementFix | `/movementfix on\|off\|status` | true | on/off |
| PathfindingShortcutPass | `/pathshortcut <metres>\|status` | 1000m | 0..2000m; menu steps 25m; 0 disables corner shortcuts |
| GroupCohesion | `/groupcohesion on\|off\|status` | true | on/off |
| AlienGroupCohesion | `/aliengroupcohesion on\|off\|status` | true | on/off |
| DestinationLocalCohesion | `/destinationcohesion on\|off\|status` | true | on/off; shared Human/Alien nearby-destination rule |
| HumanWaypointWait | `/waypointwait human <seconds>` | 5s | 0..120s after Human shared ETA |
| AlienWaypointWait | `/waypointwait alien <seconds>` | 7s | 0..120s after Alien shared ETA |
| AlienCombatWait | `/waypointwait combat <seconds>` | 10s | 0..120s additional Alien combat allowance |
| TravelCoverPreferenceDisabler | `/travelcoverdisabler on\|off\|status` | true | ON disables infantry travel-cover substitution; combat decisions remain native |
| FpsSpacing | `/fpsspace <value>` | 35% | 0..100% |
| FpsDistance | `/fpsdistance <value>` | **10%** | 0..100% |
| FpsAllowance | `/fpsallowance <value>` | 15m | 1..100m |
| Spacing | `/formationspace <value>` | 35% | 0..100%, single-click placement |
| DragSpacing | `/dragspace <value>` | 15% | 0..100%, click-and-drag packing |
| Rally | `/rallyspace <value>` | 20% | 0..100% |
| SizeInfluence | `/sizeinfluence <value>` | 0% | 0..100% |
| AlienSpread | `/alienspread <value>` | 45% | 0..100% |
| LesserSpawnAirSpacing | `/lesserspawnairspace <value>` | +50% | 0..200% extra; menu step 5%; Wasp, Dragonfly and Squid commander placements |

Use `/cohesion <key> <number>` for Humans and `/aliencohesion <key> <number>` for Aliens. Human `MaximumRange` defaults to 5000m and Alien to 5034m (1..100000m). Both use `DestinationRadius = 600m` (0..10000m), and these five Near/Far pairs:

| Key before `Near` / `Far` | Human Near | Human Far | Alien Near | Alien Far |
| --- | --- | --- | --- | --- |
| `Strength` | 1 | 0.4 | 1 | 0.5 |
| `HoldStrength` | 1 | 0 | 1 | 0.25 |
| `MinimumSpeed` | 0.05 | 1 | 0.1 | 1 |
| `ArrivalWindow` | 0.25s | 5s | 0.5s | 10s |
| `MaxDelay` | 15s | 10s | 15s | 10s |

Human numeric keys use `Cohesion<Key>` and Alien keys use `AlienCohesion<Key>`, for example `CohesionMaximumRange` and `AlienCohesionStrengthNear`. Existing faction switches and common safety CFG values are retained. The 12 common safety/debug keys remain configurable through cfg or the generic command but are hidden from the normal menu. `StartGrace` defaults to 5s for Humans and 3s for Aliens; `Retention` defaults to 500s for both. Three-stage keys such as `CloseRange` and `MediumStrengthFar` no longer affect behavior; they are not automatically converted into the new single-profile endpoints.

The original tuning commands also accept `status` for admins. Everyone can read all their values through `/formation status`.

Spacing/distance multiplier: `1 + percentage/15` through 30%, then `3 + (percentage-30)/10`. FPS distance uses `max((5m + player radius) * multiplier, player radius + largest follower radius + 1m)`. Thus 10% means approximately **1.667 times** the baseline, not ten percent of that baseline.

Older configurations with a saved WaypointGraceNear value use it when the fixed faction waits are first created. Fresh configurations use 5 seconds for Humans and 7 seconds for Aliens. Existing HumanWaypointWait and AlienWaypointWait keys take precedence. WaypointGraceFar is historical. Legacy /waypointwait near and /waypointwait far commands now set both faction timers to the supplied constant.

Settings affect subsequent placements. Existing rally tickets retain their captured scale. FPS settings are observed by the existing movement-update logic. Disabling the master releases formation ownership and air-height reservations, clears pending scouting and waypoint barriers, and hands pending production destinations back to native rally handling. Already issued destinations remain native orders.

**General → Pathfinding shortcut pass** adjusts the maximum horizontal span checked by native vehicle `RemoveOpenCorners` when processing a fresh path. The default span is 1000m. The game's native limit is 200m. The slider ranges from 0 to 2000m in 25m steps. Zero disables corner shortcuts. `/pathshortcut` accepts any whole metre within that range and saves immediately. Existing saved configurations receive 1000m when the key is absent. The server publishes the setting for path workers without Unity or preference reads on those workers. Master OFF restores the native 200m limit.

The native traversal checks and subsequent vehicle-width corner offsets govern accepted shortcuts. Larger values can remove additional bends where the vehicle navigation graph permits a direct crossing. This applies to native vehicle paths processed on the server, including automatic orders. Changes take effect on subsequent path processing. Prototype vehicles whose navigation is delegated to a client use that client's path processing.

## Preserved native behavior

Planning uses nominal requested-mode statistics for both cohesion estimates and waypoint deadlines. Fast infantry orders use SprintSpeed, normal orders use RunSpeed, and Stealth uses WalkSpeed. Vehicle Reverse uses ReverseNormalSpeed. Alien ground/flight modes use their corresponding configured speeds. Standing movement input, CanSprint, stance, damage slowdowns, MoveSpeedScale and measured velocity do not lower the planning estimate. Native execution restrictions still determine actual movement.

During ordinary Shift Move cohesion, the active slowest nominal speed tier receives no custom speed cap. Faster members follow the median formation-relative position and measured forward progress of that tier. Progress is smoothed over approximately one second. Members behind can catch up, while members ahead receive lower targets. The position allowance is the larger of 1m or half the member radius plus ArrivalWindow times the slowest nominal speed. Position correction uses a two-second response. Strength blends this target with independent speed. Minimum-speed fractions, startup/path protections, health release and existing delay budgets still apply. A unit arriving or leaving the active cohort causes the remaining speed tiers to be reconsidered. Reverse uses its strict snapshot controller.

Queued single-click formation facing follows the segment from the previous waypoint to the new destination. Captured batches use the preceding command anchor, avoiding a bias from individual rear slots or the aircraft offset. Untracked Move/Reverse predecessors use their assigned destination positions. A first Shift-click from idle uses current positions. Explicit click-and-drag facing follows the drawn line. The same planned origin is used for ground placement, the rear air band, cohesion heading and the degenerate-drag grid fallback.

Commander placement exposes separate Formation spacing (single click) and Formation spacing (click and drag) sliders. The new DragSpacing key and /dragspace command accept 0..100 percent and default to 0, the compact baseline. Thirty percent gives a 3x scale and 100 percent gives 10x. Drag length defines frontage, while the control changes packing gaps and minimum row spacing subject to physical clearance. It applies to normal and Shift drags, mixed-air placement and the destination-local fallback. Changes save through the existing server configuration and affect subsequent placements.

Ordinary Move cohesion and bounded regroup waits apply to Shift-click and Shift-drag orders, including a single Shift-click with an empty queue. Each order uses the game queue-command flag. A Shift order appended behind existing work starts when its native queue reaches it. Ordinary clicks and drags use formation placement with independent completion. Aircraft retain horizontal cylindrical arrival on those commands. Reverse retains snapshot pacing and regroup handling.

Group cohesion uses bounded server updates with a default 0.35s interval per faction. Profiles are captured for each new command; general watchdog/debug controls are observed live. Settings share the existing category: human settings retain `GroupCohesion` and `Cohesion...` keys, while Alien settings use `AlienGroupCohesion` and `AlienCohesion...`. `/cohesion <key> <number>` edits humans and `/aliencohesion <key> <number>` edits Aliens, with independent validation and saving. No live CFG reload is added.

Ordinary arrival pacing and eligible formation holding weaken continuously with travel distance across the same profile; their strength controls are separate. Beyond `MaximumRange`, an ordinary command receives neither custom cap. Native slowdown remains suppressed for an enabled faction, including skipped or beyond-range selections, independent of MovementFix. Severe loss, spread, lag, stalls or exhausted delay budgets release pacing permanently to native travel. Native Reverse is handled separately below; other attack/forced work and unsupported special movement receive no custom cohesion controller. Friendly-target Defend uses native behavior.

An ordinary one-click Move clears the selected group's dragged-layout memory and uses fresh grid placement. Shift-issued Moves add progress-based pacing for faster members. It does not carry the preceding drag's shape forward. A deliberate drag defines its own shape and facing; Reverse retains its separate snapshot behavior. Current physical arrangements, destination-local cohorts and rally parking never become remembered shapes.

Major turns weaken pacing for a new dragged command. Up to 15 degrees leaves the distance profile unchanged; at 90 degrees or more, holding is disabled, arrival strength is 15% of its original value, and the custom speed floor is at least 80% of the requested mode's uncapped speed. Native movement can still be slower. Each new command captures its own profile.

New moves seed provisional caps immediately, while failure accounting waits for a fresh usable path associated with the captured order and the startup grace (default 5s for Humans, 3s for Aliens). Fresh path distances rebase the initial ETA once; the deadline is not continually extended for a stuck rear member. An enrolled unit temporarily jumping, climbing or falling pauses its cap but keeps membership in the same captured order. Death, takeover and genuine replacement orders still release ownership.

Debug samples report freeSpeed as nominal requested-mode speed, paceSetter, smoothed progressSpeed, groupPace and signed progressGap. Positive gaps indicate a member ahead of the reference after formation offsets are removed.

For a focused trace: `/cohesiondebug next`, issue a move, then `/cohesiondebug groups` and `/cohesiondebug <id>`. Bounded traces distinguish native dispatch attempts/acceptance from cohesion enrollment, show target/order state, and separate requested caps from caps actually applied or skipped with reasons. Periodic global detail samples up to 32 members per group in rotation; explicitly watched groups can sample up to 256. A zero cohesion count alone does not establish that a native single-unit Move failed. `/cohesiondebug off` ends global and individual watches.

Destroyed or despawned members can leave remembered gaps while total casualties are **at most 40% of the original dragged layout**. Exactly 40% retains the shape; the original denominator survives repeated survivor moves. More than 40%, player takeover, live team/eligibility changes, partial selections, added members or merged groups invalidate the whole affected intended shape. This rule never treats a living unit omitted from the selection as a casualty. Invalidated survivors receive fresh normal placement on the next ordinary click; their current native orders remain untouched. A new complete drag can establish a new intended shape.

Valid drag-derived layout memory is independent of speed coordination. Older move cleanup cannot erase a newer layout, natural completion does not immediately forget intended offsets, and captured current/queued moves can retain an intact layout for Reverse. Ordinary retention applies after those moves end. A new ordinary one-click Move explicitly clears this memory, including when queued.

The **General → Nearby destination cohesion** switch controls the 600m rule for both factions and defaults **ON**. Its saved key is `DestinationLocalCohesion`; command: `/destinationcohesion on|off|status`. A missing switch uses the tuned ON default. Saved OFF values load normally. Ordinary compact-group cohesion keeps its existing faction settings.

When the switch is ON, `DestinationRadius` (600m per faction by default) admits selected eligible ground units from a dispersed or oversized selection already within that horizontal distance of the clicked/drag centre. Up to `MaxUnits` nearest members receive local arrival pacing; distant units use independent rear destinations and never govern the cohort's pace or casualty count. This is a fixed issue-time cohort, not automatic late recruitment. Turning the switch OFF immediately releases active and queued destination-local pacing without replacing native orders or their destinations. Each faction's radius is retained; radius 0 also disables that faction's fallback. Commands: `/cohesion DestinationRadius 600` and `/aliencohesion DestinationRadius 600`.

The **Travel cover preference disabler** defaults ON in General. It suppresses the soldier agent's automatic long-path cover waypoint substitution, retaining native Stealth area handling. Combat reactions, threat/cover choice and claims remain native. OFF restores native travel cover on newly calculated paths; changing the switch does not reissue existing orders. The Formation master switch still controls effective operation.

Single-click ground placements use fresh, individually sized grid cells with a blend between each ground unit's radius and the selection's largest ground footprint. Drag and Reverse layouts retain projection and physical-clearance checks. A short valid drag starts two units across in a longitudinal rectangle. Increasing drag width adds columns through a square and a sideways rectangle, then forms a single line when the total required footprint fits. Native priority ordering applies during packing, and priority bands can share the final single row. Unit count, mixed physical sizes and terrain projection determine the exact proportions. Aircraft do not affect ground count, rows, footprint, spacing or cohesion membership; their separate rows begin **50m** behind the ground formation rear edge. When ground units are at least as numerous, aircraft span that ground width. When eligible aircraft outnumber the placed ground units, the air block uses a compact, roughly square footprint based on aircraft count and horizontal cell dimensions. Its width can extend beyond the ground formation. A partial final row is centred at normal air spacing. All-air selections use the same compact packing around their own command anchor. Colossus and Defiler form a separate rear block across the original ground formation width when two fit. All-air or narrow-ground selections use compact rows with at least two columns for two or more large flyers. Their radii, bounds and spacing influence are excluded from the ordinary flyers cell dimensions and air-pitch estimate. Ordinary flyers therefore retain their compact block when either large type joins the selection. Physical clearance is checked between both blocks. Aircraft arrival uses a horizontal cylinder with the native final-waypoint radius; height does not affect arrival or regroup ETA. Native navigation and flight height remain authoritative, including explicit grounded/stealth movement for flyer-capable Aliens. Commander previews remain vanilla and can differ from tuned destinations.

Rally parking preserves production dispatch and forced vehicle exits, reserves navigation-projected ground or vertically separated aircraft destinations before dispatch, and releases ownership on replacement orders, player control, team changes, destruction or despawn. Human harvesters and Alien Shrimp are excluded from all custom rally reservations, interception and redispatch; their native rally and auto-harvest pipeline continues. Search remains bounded; native preferred flight height is restored when owned air reservations end.

FPS Follow/Attack retain the rearward moving two-column trail and stopped fan; explicit Move retains the destination grid. Native combat, navigation, explicit movement modes and external-order precedence remain authoritative. Ordinary follow repositioning requires displacement greater than FpsAllowance. Convoy entry requires more than twice that allowance from rest, with a 10m minimum; stopping for 0.75s restores the fan. Existing observation interval remains 0.25s. These are implementation behaviors, not new advanced controls.

The **Alien cohesion → Alien: Lesser Spawn air spacing** slider applies to Wasp, Dragonfly and Squid in new commander click, drag and destination-local formations. It uses a consistent native line-footprint baseline for both horizontal axes: the larger of body size plus the native 4m gap and required physical clearance. Zero uses that baseline, +50% uses 1.5 times it, and +200% uses 3 times it. This value replaces the shared spacing scale for those commander aircraft. It does not multiply an already tuned Alien spacing value. FPS layouts and rally parking use their existing controls. The baseline does not reproduce the game's distinct vanilla click-grid geometry.

## Reverse formations

With that faction's cohesion enabled, the native Reverse command translates the selected reverse-capable ground vehicles and eligible infantry as one snapshot. Vehicles receive native Reverse orders; infantry receive ordinary native Move orders to their assigned snapshot slots, turning and walking/running normally. The game exposes no native infantry Reverse receiver. A queued Reverse uses predecessor Move/Reverse destinations. It preserves each unit's world-space slot offset instead of rotating the shape toward the destination; both the formation-move route and direct command-card/network route are supported.

Infantry fallback keeps the requested movement mode and queue flag. It only issues while the soldier's order processor and complete order-ID sequence remain unchanged since preparation, so an intervening external order wins. Eligible infantry still receive this Move fallback when faction cohesion is OFF, without custom pacing or vehicle snapshot overrides. The Formation master switch must remain ON; this server feature does not add an infantry-only Reverse button to the client.

Reverse uses strict snapshot pacing rather than ordinary distance relaxation. It can hold a leading unit while others catch up, within bounded slowdown and lifetime limits. Native `ReverseNormalSpeed` supplies the uncapped estimate even when Fast was requested. A fresh Reverse snapshot does not create reusable drag memory by itself. Reversing a whole valid remembered dragged group retains that lineage and original casualty baseline; a later ordinary click clears it and uses fresh placement.

This preserves destination geometry, not a rigid body-yaw lock. Native reverse steering, terrain, collision avoidance, combat holds and movement restrictions remain authoritative. Projection or clearance failures use native Reverse without the custom snapshot. Unsupported/player-driven selections and single vehicles also remain native. Partial native acceptance releases strict pacing for the whole snapshot and reports the outcome; accepted native orders continue.

## Queued group waypoints

Shift-issued ordinary commander Move orders and commander Reverse orders capture the selected commandable AI group, including aircraft. At activation, the main-body centre is the horizontal coordinate-wise median of living members' actual positions. If no member is within 1000m of that median, the member nearest the commanded anchor breaks the split tie. Members **more than 1000m** from that centre are excluded from waiting only; they keep their formation membership and every accepted waypoint order.

One shared ETA divides the longest remaining included route by the slowest included unit's uncapped requested-mode speed. Current velocity and custom speed caps are not used. Provisional horizontal distances receive at most one startup correction when paths are ready, or after 3 seconds; subsequent reroutes never extend the base deadline. The separately captured Alien combat allowance can extend it once. Aircraft use horizontal distance. At every new waypoint, positions, inclusion and timing are recalculated, automatically restoring caught-up units to waiting calculations.

The group advances as soon as all included members have completed their waypoint and any Alien combat return, or when its bounded wait expires. Human overdue checkpoint wait and Alien overdue checkpoint wait are independent fixed timers after the shared ETA. Fresh defaults are 5 seconds for Humans and 7 seconds for Aliens, configurable from 0 to 120 seconds through Commander placement or `/waypointwait human|alien <seconds>`. Alien combat checkpoint wait adds one extra allowance, default 10 seconds and configurable from 0 to 120 through `/waypointwait combat <seconds>`. The extra allowance is applied once per waypoint when an included Alien is interrupted by combat. It also covers the return after disengagement. Repeated engagements cannot extend the deadline again. Completing the regroup earlier advances immediately. Settings are captured when the waypoint activates.

Alien combat detection uses a live visible/shootable hostile target or an active AI-issued Attack/AttackMove order. After one second without combat, an owned captured head gets one return execution per combat episode. Its native ground completion then counts as arrival, including navigation shortfall or ended-movement handling. A stale completion from the re-execution frame is ignored. AI-issued combat replacements can preserve pending waypoints behind the combat order. Once a regroup deadline opens, combat or exact-position checks cannot reblock its captured queues. If a final return is still owed, it becomes one ordinary native move after combat and the old captured order has consumed; it has no group barrier or repeated shortfall retries. Expired intermediate restoration legs are discarded in favor of later objectives. Manual replacements, explicit queue clearing, AI utility replacements, takeover and team/control changes cancel obsolete recovery.

Ground units use native completion, including after their one captured post-combat return attempt. Aircraft retain the horizontal native final-waypoint radius. Combat can invalidate a previously latched arrival if a member leaves the checkpoint before release. Recovery-only groups can start a bounded ETA when no legitimate earlier native or captured waypoint still owns their queue; all-restoration state no longer leaves the deadline uninitialized. Later waypoints continue to wait behind earlier active Move/Reverse legs. Debug traces retain combat start/end, queue preservation, movement resumption, the single combat allowance and combat regroup timeout.

This is independent of cohesion ON/OFF and pacing budgets. Death/despawn removes members; explicit replacements, takeover, team changes and forced control retain native precedence. Rejected moves remain unresolved until timeout; a rejected native order cannot be manufactured by the queue gate. Attack, Patrol and utility orders retain native completion. Master/round/scene/unload cleanup releases ownership. `[Waypoint #...]` diagnostics distinguish native completion, horizontal air arrival, distance exclusion, initial/corrected ETA, countdown and regroup timeout.

Drag creation and Reverse projection share a circular footprint floor plus 0.5m clearance. Debug traces identify failed units, desired/projected coordinates, projection displacement and pair separation versus required clearance. Aiming alone no longer disables pacing; native paused paths, engagement holds, obstructions and traversal exclusions remain authoritative. Traversal exemptions and pacing deadlines retain their previous values.

## One-shot scouting

While in commander role, use `/scout` or `/scout on` to arm scouting. Select at least **five eligible friendly AI units**, then issue an immediate, non-queued ordinary Move on empty ground. That move consumes the armed action once: successful scouts are distributed among map sectors, while remaining normally movable selected units receive the original clicked Move.

At most **75 eligible units** receive scout assignments. Eligible Alien air units use their actual navigation graph and traversable tags in bounded destination/connectivity checks. Failed or excess scouts and other remaining normally movable selected units receive normal formation destinations around the original click; native acceptance can still fail and outcomes are reported. Protected or navigation-unready units remain unchanged. Player-controlled/driven/embarked units retain their control. With fewer than five eligible units, scouting is disarmed and the normal move is used. Successful placement is not guaranteed for every unit.

`/scout status` reports whether the action is armed; `/scout off` cancels it. Scouting requires commander role, not admin permission. Arming clears on role/team changes, master disable and round/scene cleanup. It issues one set of native Move orders, not a repeating patrol.

## Straggler cleanup and performance

A captured formation member continuously more than 1000m horizontally from its current main body for 300 simulation seconds has its entire order queue cleared and its current native movement stopped. The monitor samples current positions once per second and uses the same median/tie policy as waypoint admission. Rejoining at or inside 1000m resets the timer. The timer carries across queued waypoints. Replacement commands, explicit queue clearing, takeover, team changes and lifecycle cleanup release the watch. The monitor also covers the exact final native path when a timed-out waypoint has already left the queue, including its one-shot deferred return. Existing watches transfer to later captured members without resetting continuous outside time. Debug logging records timer starts, rejoining and cancellation.

Waypoint maintenance reuses its barrier snapshot until the collection changes, allocates recovery sets only when needed, and checks current heads directly. Pending-order checks reuse identity/ID validation. Empty rally work skips the stopwatch and ticket snapshot after normal reservation pruning. Update intervals, path requests, movement modes and queue dispatch use their existing mechanisms. The new distance monitor uses position samples and shares each computed main-body centre within its scan.

The static review also identified temporary cohort-list allocation and bounded rally occupancy searches as candidates for a later performance pass. Runtime CPU and allocation measurements are user-owned live checks.

There are 67 active persisted settings. The previous 66 retain the tuning captured on 16 November 2026, including PathfindingShortcutPass at 1000m; LesserSpawnAirSpacing adds a default of +50%. This includes independent Human/Alien profiles and hidden general controls, all placement/FPS settings, feature switches and the three checkpoint waits. Fresh configurations use Nearby destination cohesion ON with a 600m radius. Existing saved values load before defaults. Debug watches are per-session runtime state.
## Build and verification

From this repository folder under the dedicated server's `Silica-1283` directory, run:

```powershell
dotnet build Si_Formation.csproj -c Release --ignore-failed-sources
```

The project resolves `ServerRoot` two folders above itself by default. Use dedicated-server Mono references, not client IL2CPP assemblies. Output: `bin/Release/netstandard2.1/Si_Formation.dll`. Do not load both old and new formation DLLs.

In game, verify:

1. A regular player sees Formation Status, can read current values, and cannot change tuning through slash commands or old remembered client controls. An admin with Generic permission can change settings from either role.
2. After an admin change, `/formation status` reports it immediately without moving units. Restart persistence restores saved values; old personal values are not replayed. A fresh configuration defaults FPS distance to 10%.
3. Compare FPS spacing/distance/allowance, commander click/drag spacing, mixed-size influence, Alien scaling and rally parking with the reported current values.
4. Toggle Enabled off/on and MovementFix off/on; test pending production, aircraft height restoration, subsequent native orders and `/scout` arming/cancellation.
5. Verify grouped sliders on a compatible structured UI bridge. On older bridges without presentation hints, expect ordinary controls.
6. Confirm Fast riflemen start with nominal SprintSpeed and paceSetter=True, with no requested custom cap. Check faster units behind/ahead of the slowest tier, turns and temporary native holds. Queue an L-shaped route before movement and verify each heading follows its preceding waypoint. Compare dragspace 0/30/100 and persistence. Compare ordinary clicks/drags with a single Shift-click/Shift-drag from idle and a multi-waypoint Shift queue. Verify only Shift Move orders receive custom pacing and waits. Check ordinary replacement releases previous pacing. Include aircraft outnumbering ground units and a partial final air row. Enable faction cohesion and compare straight/90/180-degree turns, very short and near/beyond-maximum moves, fresh/queued paths and temporary traversal. Check requested versus applied caps alongside native dispatch results. Verify casualty retention below/exactly/above 40% of the original group, repeated survivor moves, partial selections, added members and player takeover.
7. Scout with both factions, including Alien air units; verify bounded destinations and original-click fallback for failed/excess scouts.
8. Use Reverse through the command card and formation/network route, with queued predecessors and mixed vehicle speeds. Compare preserved slot geometry with native steering around obstacles; verify replacement orders release custom caps.
9. Keep a captured member beyond 1000m for five minutes across several waypoints, then repeat with a rejoin before expiry. Include a final empty queue with a native path still running. Verify timer reset, full queue clearing, path stop, replacement orders, takeover and saved default values. Check Alien combat during a queued move, after checkpoint arrival and after the group timeout. Include native AI combat replacements, repeated engagements, queue capacity, manual Stop/replacement and player takeover. Confirm return to the final checkpoint when the rest of the group has finished. Verify separate faction waits and saved values. Compare small-flyer spacing with and without Colossus/Defiler, including an all-large selection. Drag from the narrow column through square, wide rectangle and mixed-priority single line. Verify mixed ground/air queued Move/Reverse: native completion releases early, timeout releases stuck members, height does not block aircraft, distant members remain ordered and rejoin waiting after catching up. Check 1000m boundaries, one-time path correction, grace controls and manual overrides. Confirm native Defend behavior.

The **3.18.0 dedicated-server Release build passed with 0 warnings and 0 errors** in 5.12 seconds. The server DLL is staged, not installed. No tests, live gameplay checks or server lifecycle actions were run. Compilation does not verify Harmony installation, native queue/flight behavior or multiplayer UI. The PDF overview and quick summary remain 3.16.2 snapshots. Historical release notes describe their original versions.

For the new squad protection, check a teammate's FPS squad while following, moving, attacking and stopped. Use Select All and issue ordinary/Shift Move, drag, Reverse, Attack, Patrol and Stop. Highlighted FPS members must retain their FPS orders. Include mixed selections and selections containing only protected members, then check the FPS owner's commands and removal from the squad.

User observations from 12 November on **3.8.0** described cohesion as subtle/natural, wanted short-move holding retained, noticed no added long-move delay and approved rectangle-start dragging. They also identified native-preview misalignment and a need for smoother presentation. These are qualitative observations, not measured tests or 3.9.0 validation. Preview/smoothing client work is deferred.
