# Group cohesion — Si_Formation 3.16.1 candidate

Si_Formation coordinates Shift-issued commander ground Move orders with one continuous distance profile. **Human and Alien cohesion have independent switches, both ON by default.** Near moves can receive strong arrival pacing and speed-based holding of a deliberately dragged formation; those effects fade as distance increases.

The 3.16.1 dedicated-server Release build passed on 16 November 2026 with 0 warnings and 0 errors. The DLL is staged, not installed; no tests, live gameplay checks or server lifecycle actions were run. This guide describes 3.16.1. The [3.11.0 release notes](Changes_3.11.0.md) describe the earlier regrouping implementation. Older release notes and PDF guides are historical snapshots.

## One distance profile

Ordinary Move cohesion and bounded regroup waits apply to Shift-click and Shift-drag orders, including a single Shift-click with an empty queue. Each order uses the game queue-command flag. A Shift order appended behind existing work starts when its native queue reaches it. Ordinary clicks and drags use formation placement with independent completion. Aircraft retain horizontal cylindrical arrival on those commands. Reverse retains snapshot pacing and regroup handling.

Near means zero travel distance. Far means the faction's `MaximumRange`, default **5000m for Humans and 5034m for Aliens**. Every endpoint value interpolates linearly using `distance / MaximumRange`. The distance is horizontal, from the admitted ground cohort's centre to the clicked destination or drag midpoint; a queued segment estimates its origin from predecessor Move destinations. Aircraft do not affect that ground centre.

The profile is captured for the new command. It does not become stronger merely because units approach their destination. There are no Close/Medium/Long stages.

At exactly **MaximumRange**, the Far settings still apply. Beyond it, an ordinary Move has **no custom arrival pacing or holding**. Native group slowdown remains suppressed while that faction's cohesion switch is enabled, so an over-range move does not fall back to the native crawl. Native Reverse uses the separate strict snapshot behavior below. Explicit movement modes, terrain, combat and physical obstructions still apply.

Planning uses nominal requested-mode statistics for both cohesion estimates and waypoint deadlines. Fast infantry orders use SprintSpeed, normal orders use RunSpeed, and Stealth uses WalkSpeed. Vehicle Reverse uses ReverseNormalSpeed. Alien ground/flight modes use their corresponding configured speeds. Standing movement input, CanSprint, stance, damage slowdowns, MoveSpeedScale and measured velocity do not lower the planning estimate. Native execution restrictions still determine actual movement.

During ordinary Shift Move cohesion, the active slowest nominal speed tier receives no custom speed cap. Faster members follow the median formation-relative position and measured forward progress of that tier. Progress is smoothed over approximately one second. Members behind can catch up, while members ahead receive lower targets. The position allowance is the larger of 1m or half the member radius plus ArrivalWindow times the slowest nominal speed. Position correction uses a two-second response. Strength blends this target with independent speed. Minimum-speed fractions, startup/path protections, health release and existing delay budgets still apply. A unit arriving or leaving the active cohort causes the remaining speed tiers to be reconsidered. Reverse uses its strict snapshot controller.

`MaxDelay` limits each member in two ways: cumulative deliberately lost travel time, approximately `elapsed * (1 - cap / uncappedModeSpeed)`, and a deadline at its initial uncapped arrival estimate plus the budget. Startup permits a one-time path-based ETA rebase, not repeated extensions for a failing rear unit. Expiring either allowance ends that member's pacing. These are controller budgets, not exact physical arrival-delay measurements.

### Fresh-path startup

Failure accounting waits until a fresh usable path is associated with the captured Move and the existing `StartGrace` has elapsed, default 5 seconds for Humans and 3 seconds for Aliens. Stuck/move-problem state left over from the preceding command should not immediately release the new group. Provisional caps are still prepared immediately; fresh route lengths replace provisional timing once. Subsequent congestion does not keep moving the shared deadline. Queued commands retain captured order-ID activation checks.

## Dragged layouts and ordinary clicks

Queued single-click formation facing follows the segment from the previous waypoint to the new destination. Captured batches use the preceding command anchor, avoiding a bias from individual rear slots or the aircraft offset. Untracked Move/Reverse predecessors use their assigned destination positions. A first Shift-click from idle uses current positions. Explicit click-and-drag facing follows the drawn line. The same planned origin is used for ground placement, the rear air band, cohesion heading and the degenerate-drag grid fallback.

An ordinary one-click Move clears dragged-layout memory for the selected group and uses fresh grid placement with arrival pacing. This also applies when the click is queued. It does not translate the previous dragged shape or recreate memory from the units' current physical arrangement.

A deliberate drag still defines its own intended slots and facing. Shift drags use progress-based pacing while reforming. Substantial facing changes attenuate its profile: with `r = clamp01((absYaw - 15) / 75)`, arrival strength is multiplied by `1 - 0.85*r`, holding strength by `1 - r`, and the minimum custom speed fraction becomes at least `0.8*r`. Native movement can still be slower.

Valid drag memory can remain available for Reverse. Destroyed/despawned losses of at most 40% of the original group retain it; exactly 40% is allowed. The original denominator remains fixed. Live omissions, additions, merged selections, control/team/eligibility changes and greater casualties invalidate it. Retention after completion defaults to 500 seconds. A fresh drag establishes a new baseline; an ordinary click explicitly clears it. Existing native orders are not canceled merely to clear memory.

## Native Reverse and snapshot holding

When that faction's cohesion is enabled, using native Reverse captures eligible reverse-capable ground vehicles and eligible infantry in the same snapshot. Each destination is the clicked anchor plus that unit's original world-space offset from the group centre. The shape is translated without rotating its offsets toward travel. Queued Reverse uses the last queued Move/Reverse destinations; unsupported predecessors leave the command native.

Vehicles receive native `AIReverseOrder`; infantry lack that receiver and receive ordinary `AIMoveOrder` to their snapshot slots. Infantry turn and walk/run normally, with the requested speed mode and queue flag retained. Both the formation-move and direct command-card/network routes handle this fallback. Accepted Move and Reverse order IDs are tracked independently inside the same group. At least two accepted members are needed for strict pacing; a single eligible soldier can still receive its ordinary Move. Reverse does not require a previous drag. A fresh snapshot cannot create reusable drag memory by itself; reversing the whole valid remembered dragged group retains its drag lineage and original casualty baseline, while a later ordinary click clears that memory and uses fresh placement. Unsupported/player-driven movement, excessive membership, distorted projection or lost physical clearance keeps native handling. Partial native acceptance releases strict pacing for the whole snapshot and reports the accepted count; accepted native orders continue.

Infantry fallback runs once after the native batch completes. A changed order processor, replacement or queued append since preparation cancels that soldier's fallback. With faction cohesion OFF it can still move eligible infantry to their snapshot slots, while vehicle destinations and pacing remain native. The Formation master switch still controls operation. No infantry animation, facing lock or infantry-only client Reverse button is added.

Reverse uses full arrival/holding strength rather than ordinary distance/turn relaxation, can request a complete pause for a leading unit, and retains the bounded 120-second per-member delay allowance plus ordinary controller lifetime and release checks. Its longitudinal hold tolerance is at least 0.5m or 10% of unit radius, whichever is larger. Requested speed estimates use native `Vehicle.ReverseNormalSpeed`, including when Fast is requested; the game has no separate ReverseFastSpeed.

Fixed slot rotation does not lock actual vehicle yaw. The native reverse controller still steers, routes around obstacles, handles collision avoidance and respects engagement/path holds. A turret aiming at an enemy alone does not cancel Reverse pacing; native movement/engagement holds still take precedence. Actual geometry and simultaneous arrival are not guaranteed. No transform rotation, teleport or repeated Reverse order is used to enforce the snapshot.

## Drag width

Commander placement exposes separate Formation spacing (single click) and Formation spacing (click and drag) sliders. The new DragSpacing key and /dragspace command accept 0..100 percent and default to 0, the compact baseline. Thirty percent gives a 3x scale and 100 percent gives 10x. Drag length defines frontage, while the control changes packing gaps and minimum row spacing subject to physical clearance. It applies to normal and Shift drags, mixed-air placement and the destination-local fallback. Changes save through the existing server configuration and affect subsequent placements.

A short valid drag starts two units across in a longitudinal rectangle. Increasing drag width adds columns through a square and a sideways rectangle, then forms a single line when the total required footprint fits. Native priority ordering applies during packing, and priority bands can share the final single row. Unit count, mixed physical sizes and terrain projection determine the exact proportions.

Native unit order and front/back priority bands govern the intermediate rows. At full single-line width the bands share one row. The requested midpoint, facing, physical clearance and navigation projection are applied.

A tiny or degenerate drag rejected by native line placement uses the ordinary grid fallback at the native click anchor. It does not create remembered drag identity. Aircraft stay separate. The vanilla client preview can differ from these server destinations.

## The 12 visible controls per faction

Use `/cohesion` for Human values and `/aliencohesion` for Alien values. Their defaults differ as shown below; changing one does not copy it to the other. Minimum/maximum and step below are menu metadata; commands validate the numeric range and endpoint ordering.

| Key | Human default | Alien default | Valid range | Menu step | Effect |
| --- | --- | --- | --- | --- | --- |
| `MaximumRange` | 5000m | 5034m | 1–100000m | 1m | Far interpolation endpoint and custom-cohesion distance limit |
| `DestinationRadius` | 600m | 600m | 0–10000m | 25m | Local-cohort radius when Nearby destination cohesion is ON; 0 disables the faction's fallback |
| `StrengthNear` | 1 | 1 | 0–1 | 0.05 | Progress pacing strength at zero distance |
| `StrengthFar` | 0.4 | 0.5 | 0–1 | 0.05 | Progress pacing strength at MaximumRange |
| `HoldStrengthNear` | 1 | 1 | 0–1 | 0.05 | Remembered-formation holding strength at zero distance |
| `HoldStrengthFar` | 0 | 0.25 | 0–1 | 0.05 | Remembered-formation holding strength at MaximumRange |
| `MinimumSpeedNear` | 0.05 | 0.1 | 0.05–1 | 0.05 | Minimum custom cap as a fraction of uncapped requested-mode speed |
| `MinimumSpeedFar` | 1 | 1 | 0.05–1 | 0.05 | Corresponding speed fraction at MaximumRange |
| `ArrivalWindowNear` | 0.25s | 0.5s | 0–60s | 0.25s | Allowed lead in nominal pace seconds at zero distance |
| `ArrivalWindowFar` | 5s | 10s | 0–60s | 0.25s | Allowed lead in nominal pace seconds at MaximumRange |
| `MaxDelayNear` | 15s | 15s | 0–120s | 0.5s | Per-member pacing budget at zero distance |
| `MaxDelayFar` | 10s | 10s | 0–120s | 0.5s | Per-member pacing budget at MaximumRange |

At the Human profile midpoint of 2500m, pacing strength is 0.7, holding strength 0.5, minimum speed fraction 0.525, arrival allowance 2.625s and pacing budget 12.5s. At the Alien midpoint of 2517m, the corresponding values are 0.75, 0.625, 0.55, 5.25s and 12.5s. Major turns can weaken the captured strengths.

Strength, HoldStrength and MaxDelay must not increase toward Far. MinimumSpeed and ArrivalWindow must not decrease. Setting a strength to zero disables that corresponding custom effect. A minimum speed fraction does not accelerate a naturally slower, damaged or blocked unit.

## Dispersed selections and destination-local arrival

**Nearby destination cohesion** is a shared switch in **General**, default **ON**. General also contains the independent Human and Alien cohesion switches, Suppress native group slowdown and Travel cover preference disabler. The generic `!aliencohesion`, `!cohesion` and `!waypointwait` command forms are in Diagnostics. `/destinationcohesion on|off|status` controls the nearby-destination switch; changes require AdminMod Generic permission and save as `DestinationLocalCohesion`. A missing key uses the tuned ON default and both faction radii default to 600m. The switch applies to both factions and does not disable ordinary compact-group cohesion.

With the switch ON, if a ground selection is too dispersed or exceeds `MaxUnits`, `DestinationRadius` can admit only eligible selected ground units already inside the horizontal radius around the clicked destination or drag midpoint. The nearest qualifying members are admitted up to `MaxUnits`; at least two valid accepted moves are required.

Membership is captured when the order is issued, including queued commands. Unselected units are never recruited, and distant members do not join later when they reach the area. Switching OFF immediately releases destination-local pacing, including queued groups, while preserving native orders and destinations. It retains each faction's radius. Setting `DestinationRadius` to 0 also disables that faction's fallback. OFF uses ordinary independent movement for scattered/oversized selections; compact selections retain their normal cohesion rules.

The local cohort receives a fresh destination layout and arrival pacing, with no remembered-layout holding. Other selected ground units receive independent destinations in a separate rear band. Aircraft are placed over the local layout's rear. Distant selected units do not determine the local cohort's speed or failure denominator. The continuous distance profile applies to the actual admitted cohort.

## Aircraft, eligibility and release

While Formation is enabled, commander click/drag placement separates aircraft from ground footprints whether cohesion is ON or OFF. Mixed formations place aircraft in rows 50m behind the rear edge of the ground footprint. When ground units are at least as numerous, aircraft span that ground width. When eligible aircraft outnumber the placed ground units, the air block uses a compact, roughly square footprint based on aircraft count and horizontal cell dimensions. Its width can extend beyond the ground formation. A partial final row is centred at normal air spacing. All-air selections use the same compact packing around their own command anchor. Colossus and Defiler use separate clearance-sized rear slots. Their radii, bounds and spacing influence are excluded from the ordinary flyers cell dimensions and air-pitch estimate. Ordinary flyers therefore retain their compact block when either large type joins the selection. Physical clearance is checked between both blocks. Physical clearance can require additional rows farther back. All-air selections retain their own command anchor. Invalid rear slots leave that aircraft on its previous native order and emit a watched/global diagnostic; a grouped waypoint then waits for that unresolved member only until its regroup deadline. Their own navigation graph and existing preferred altitude remain native. Takeoff is not forced. Arrival uses a horizontal cylinder with the native final-waypoint radius; altitude does not affect arrival or regroup travel distance.

Flyer-capable aliens retain air membership even while landed. Native Stealth can keep them walking, so horizontal overlap with ground troops remains a specific live check. Failed projections retain native placement for that unit.

Custom speed coordination requires friendly movable AI ground units. Player-controlled, driven/embarked, forced-exit and unsupported locomotion remain excluded. An enrolled member that temporarily jumps, climbs or falls pauses its custom cap while keeping membership in the same captured Move; the transient state alone does not count as a lost unit. Genuine control changes, death or replacement orders still release that member's speed ownership. Ordinary Move on empty ground and eligible native Reverse are supported; other attack, targeted and forced-order actions are not enrolled in custom cohesion. Aircraft are filtered out without rejecting an otherwise eligible ground cohort.

Caps are combined with native movement limits; a lower native limit wins. The controller does not overwrite `MoveSpeedScale` or movement modes. Replacement orders, combat holds/targets, navigation problems, casualties, excess spread/progress lag, expired budgets and maximum lifetime can remove caps or release coordination. A released command does not automatically regroup. Native destinations continue unless replaced.

Queued coordination follows the captured order IDs and waits for segment activation. If members start too far apart, it releases gracefully. Changing one faction's switch does not disable the other; the master switch still controls the whole Formation mod.

## Commands and diagnostics

Cohesion tuning and debug commands require AdminMod Generic permission. Settings are shared server values, not personal preferences. Use a decimal point.

| Command | Effect |
| --- | --- |
| `/groupcohesion on\|off\|status` | Human cohesion switch/status |
| `/aliengroupcohesion on\|off\|status` | Independent Alien cohesion switch/status |
| `/destinationcohesion on\|off\|status` | Shared nearby-destination fallback switch/status; defaults ON |
| `/waypointwait human\|alien <seconds>` | Set independent faction overdue waits, 0..120 seconds, fresh defaults 5s Human / 7s Alien |
| `/waypointwait combat <seconds>` | Set additional Alien combat wait, 0..120 seconds, default 10s |
| `/waypointwait status` | Read grace settings and 1000m waiting cutoff |
| `/dragspace <0..100>\|status` | Configure independent click-and-drag spacing, default 15% |
| `/cohesion <key> <number>\|status` | Set/read a Human control |
| `/aliencohesion <key> <number>\|status` | Set/read an Alien control |
| `/cohesiondebug next` | Trace your next grouped commander move, including skip reasons; commander role required |
| `/cohesiondebug groups` | List recent retained groups |
| `/cohesiondebug <id>` | Read a group's recent history and watch it |
| `/cohesiondebug on\|off\|status` | Shared global diagnostics; OFF also clears individual/next-group watches |
| `/travelcoverdisabler on\|off\|status` | ON disables infantry travel-cover waypoint preference; OFF restores native preference |

The switch/tuning commands without arguments show status. Diagnostics identify faction, phase and continuous distance/beyond-range state. Native dispatch attempts, acceptance/rejection and missing-attempt records are reported separately from custom cohesion enrollment. Per-unit records include target transformations and captured-order state, while samples distinguish requested versus actually applied caps and the reason a cap was skipped. Zero cohesion enrollment alone does not show whether a native single-unit Move succeeded.

Samples report freeSpeed as nominal requested-mode speed, paceSetter, smoothed progressSpeed, groupPace and signed progressGap. Positive gaps indicate a member ahead of the reference after formation offsets are removed.

Periodic detail is bounded: global tracing samples up to 32 members per group in rotation; an explicitly watched group can sample up to 256. Timing, aggregate history and retention use the owning faction's existing controls. There are no new debug settings. Existing group/slider and generic `controlPresentation` compatibility metadata are preserved; no additional ClientModUI or native-preview work is included in 3.9.0. Rendering depends on the loaded bridge.

Travel cover preference disabler defaults **ON** independently of cohesion. It changes infantry long-path cover substitution for new/recalculated paths while retaining native Stealth completion-area handling. Combat cover decisions, claims and behavior trees remain unpatched; already-chosen combat-cover routes also skip the shared travel substitution. It does not immediately repath existing orders.

Scouting remains a separate one-shot commander action. Eligible Alien air units can use their actual graph and traversable tags in bounded destination checks. Up to 75 receive scout-sector orders; remaining normally movable selected units, including failed/excess scouts, receive normal formation destinations around the original click. Protected or navigation-unready units remain unchanged. Native acceptance can still fail and is reported. Scouting does not create a remembered dragged shape.

## Bounded waypoint regrouping and layout diagnostics

Shift-issued ordinary commander Move orders and commander Reverse orders capture the selected commandable AI group, including aircraft. At activation, the main-body centre is the horizontal coordinate-wise median of living members' actual positions. If no member is within 1000m of that median, the member nearest the commanded anchor breaks the split tie. Members **more than 1000m** from that centre are excluded from waiting only; they keep their formation membership and every accepted waypoint order.

One shared ETA divides the longest remaining included route by the slowest included unit's uncapped requested-mode speed. Current velocity and custom speed caps are not used. Provisional horizontal distances receive at most one startup correction when paths are ready, or after 3 seconds; subsequent reroutes never extend the base deadline. The separately captured Alien combat allowance can extend it once. Aircraft use horizontal distance. At every new waypoint, positions, inclusion and timing are recalculated, automatically restoring caught-up units to waiting calculations.

The group advances as soon as all included members have completed their waypoint and any Alien combat return, or when its bounded wait expires. Human overdue checkpoint wait and Alien overdue checkpoint wait are independent fixed timers after the shared ETA. Fresh defaults are 5 seconds for Humans and 7 seconds for Aliens, configurable from 0 to 120 seconds through Commander placement or `/waypointwait human|alien <seconds>`. Alien combat checkpoint wait adds one extra allowance, default 10 seconds and configurable from 0 to 120 through `/waypointwait combat <seconds>`. The extra allowance is applied once per waypoint when an included Alien is interrupted by combat. It also covers the return after disengagement. Repeated engagements cannot extend the deadline again. Completing the regroup earlier advances immediately. Settings are captured when the waypoint activates.

Alien combat detection uses a live visible/shootable hostile target or an active AI-issued Attack/AttackMove order. A combat-interrupted Move must return within the horizontal native waypoint radius before it counts as arrived. After one second without combat, an owned head resumes its captured destination if the native path ended or changed. Resume attempts are limited to once every two seconds. AI-issued combat replacements preserve pending group waypoints by queuing them behind the combat order, including retries when native queue capacity becomes available. If the group advances during combat, the member catches up through later waypoints after disengaging. A member with no later waypoint continues toward its final assigned destination. Manual replacements, explicit queue clearing, utility replacements, takeover and team/control changes release ownership.

Ground units outside this combat recovery use native completion, including order-ended handling. Aircraft use the horizontal native final-waypoint radius. Combat can invalidate a previously latched arrival if the member leaves its checkpoint. Debug traces include combat start/end, queue preservation, movement resumption, the single combat allowance and combat regroup timeout.

This is independent of cohesion ON/OFF and pacing budgets. Death/despawn removes members; explicit replacements, takeover, team changes and forced control retain native precedence. Rejected moves remain unresolved until timeout; a rejected native order cannot be manufactured by the queue gate. Attack, Patrol and utility orders retain native completion. Master/round/scene/unload cleanup releases ownership. `[Waypoint #...]` diagnostics distinguish native completion, horizontal air arrival, distance exclusion, initial/corrected ETA, countdown and regroup timeout.

Drag creation and Reverse projection share a circular footprint floor plus 0.5m clearance. Debug traces identify failed units, desired/projected coordinates, projection displacement and pair separation versus required clearance. Aiming alone no longer disables pacing; native paused paths, engagement holds, obstructions and traversal exclusions remain authoritative. Traversal exemptions and pacing deadlines retain their previous values.

Perimeter defense remains removed; native Defend is authoritative. Old saved defense keys are ignored. Human harvesters and Alien Shrimp remain excluded from rally intervention.

## Configuration and older versions

The file remains `UserData/cfg/Formation/si_formation_native.cfg`, category `[Si_Formation_Native]`.

- Human switch: `GroupCohesion`; numeric keys: `Cohesion<Key>`.
- Alien switch: `AlienGroupCohesion`; numeric keys: `AlienCohesion<Key>`.
- Travel-cover switch: `TravelCoverPreferenceDisabler`.
- Nearby-destination switch: `DestinationLocalCohesion`, default ON.
- Drag packing key: `DragSpacing`, default 15%, 0..100 through `/dragspace` or Commander placement.
- Fixed faction waits use `HumanWaypointWait` and `AlienWaypointWait`, defaults 5s Human / 7s Alien. `AlienCombatWait` defaults to 10s. All accept 0..120 seconds through `/waypointwait` and Commander placement sliders.

On first creation older configurations inherit a saved WaypointGraceNear value. Fresh configurations use the separate 5s Human and 7s Alien defaults. Existing new keys take precedence. The old WaypointGraceFar distance endpoint is historical. The legacy near/far command forms set both faction timers to the same supplied constant.

For example, `StrengthNear` becomes `CohesionStrengthNear` or `AlienCohesionStrengthNear`. Both tuning commands accept their prefixed keys. Existing saved settings take precedence, including faction switches; absent switches default ON. Missing numeric settings use the tuned faction-specific defaults.

**Old Close/Medium/Long keys are ignored and are not migrated into the new profile.** This includes their old boundaries and stage-prefixed endpoints. Their presence in an older cfg does not configure 3.9.0. Use `MaximumRange` and unprefixed-stage keys such as `StrengthNear`.

Existing common safety/diagnostic keys remain available in cfg and through tuning commands, but are hidden from the normal menu:

| Retained key | Human default | Alien default |
| --- | --- | --- |
| `UpdateInterval` | 0.35s | 0.35s |
| `InitialSpread` | 250m | 250m |
| `StartGrace` | 5s | 3s |
| `DebugInterval` | 3s | 3s |
| `MaxUnits` | 256 | 256 |
| `MaximumDuration` | 600s | 600s |
| `Retention` | 500s | 500s |
| `PathDetourRatio` | 1.8 | 1.8 |
| `HoldGrace` | 2s | 2s |
| `TurnGrace` | 6s | 6s |
| `ProgressDistance` | 1m | 1m |
| `TraceHistory` | 80 samples | 80 samples |

Additional internal health tolerances vary continuously with distance; the old stage-specific watchdog endpoints do not configure them. Profile values are captured for new commands; retained general controls can affect active controller timing. Commands save immediately. Direct cfg edits should be made while the server is stopped; this source update does not change the live cfg.

## Straggler cleanup and performance

A captured formation member continuously more than 1000m horizontally from its current main body for 300 simulation seconds has its entire order queue cleared and its current native movement stopped. The monitor samples current positions once per second and uses the same median/tie policy as waypoint admission. Rejoining at or inside 1000m resets the timer. The timer carries across queued waypoints. Replacement commands, explicit queue clearing, takeover, team changes and lifecycle cleanup release the watch. The monitor also covers the exact final native path when a timed-out waypoint has already left the queue. Debug logging records timer starts, rejoining and cancellation.

Waypoint maintenance reuses its barrier snapshot until the collection changes, allocates recovery sets only when needed, and checks current heads directly. Pending-order checks reuse identity/ID validation. Empty rally work skips the stopwatch and ticket snapshot after normal reservation pruning. Update intervals, path requests, movement modes and queue dispatch use their existing mechanisms. The new distance monitor uses position samples and shares each computed main-body centre within its scan.

The static review also identified temporary cohort-list allocation and bounded rally occupancy searches as candidates for a later performance pass. Runtime CPU and allocation measurements are user-owned live checks.

The 65 settings captured on 15 November 2026 retain those tuned defaults. PathfindingShortcutPass adds the native 200m default. This includes independent Human/Alien profiles and hidden general controls, all placement/FPS settings, feature switches and the three checkpoint waits. Fresh configurations use Nearby destination cohesion ON with a 600m radius. Existing saved values load before defaults. Debug watches are per-session runtime state.
## Build and user-owned live checks

Build command: `dotnet build Si_Formation.csproj -c Release --ignore-failed-sources`. The 3.16.1 Release build passed on 16 November 2026 with 0 warnings and 0 errors in 3.41 seconds. Output: `bin/Release/netstandard2.1/Si_Formation.dll`. Staged only; no tests, live checks or server lifecycle actions were run.

User-owned checks: exercise the five-minute continuous 1000m timeout, rejoining/reset across queued waypoints, a final path with no queued order, manual replacement, takeover and full native queue/path cancellation. Compare fresh settings with all 65 captured defaults and saved-value precedence. Check Alien return after combat during travel, from a held checkpoint and after group timeout. Include AI-issued combat replacement, a full native queue, repeated combat, final destination recovery, explicit Stop/replacement and takeover. Verify independent faction waits, the single combat allowance and saved migration. Compare ordinary flyers with and without Colossus/Defiler and all-large selections. Check narrow column, square, wide rectangle and final mixed-priority line. Verify nominal Fast speed and uncapped slowest-tier members at startup. Compare faster members behind/ahead of that tier, native holds and pacing budget release. Queue an L-shaped route before units move and inspect waypoint facing. Adjust dragspace 0/30/100, verify physical clearance, normal/Shift and destination-local drags, single-click independence and saved persistence. Compare ordinary versus single Shift-click/Shift-drag from idle, queued activation and ordinary replacement of a coordinated move. Check compact air-heavy layouts and centred partial rows. Compare a drag followed by ordinary clicks, fresh drags and Reverse; normal clicks must reset memory. Check mixed ground/air placement at 50m behind the rear, all-air anchors and air arrival at different heights. Queue multiple waypoints with early arrivals, slow units, blocked members, casualties and takeover. Check the 1000m cutoff from current main-body positions, automatic re-inclusion at later waypoints, separate Human/Alien overdue waits, the bounded Alien combat allowance and one-time path correction without deadline drift on reroutes. Inspect native-completion, horizontal-air, timeout and exclusion diagnostics. Retain normal navigation, map-edge, rally, scouting and native Defend checks.

User observations from 12 November on **3.8.0** described subtle/natural cohesion, desirable short-move holding, no noticeable long-move delay and useful rectangle-start dragging. They also identified native-preview misalignment and a need for smoother presentation. These are qualitative observations, not measured tests or 3.9.0 validation. Client preview/smoothing work is deferred; native obstacle handling, multiplayer presentation and other movement-mod interactions still need live checks.
