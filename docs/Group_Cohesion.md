# Group cohesion — Si_Formation 3.11.0 candidate

Si_Formation coordinates ordinary commander ground Move orders with one continuous distance profile. **Human and Alien cohesion have independent switches, both ON by default.** Near moves can receive strong arrival pacing and speed-based holding of a deliberately dragged formation; those effects fade as distance increases.

The 3.11.0 dedicated-server Release build passed on 13 November 2026 with 0 warnings and 0 errors. The DLL is staged, not installed; no tests, live gameplay checks or server lifecycle actions were run. See [3.11.0 changes](Changes_3.11.0.md) for current behavior. Older release notes and PDF guides are historical snapshots.

## One distance profile

Near means zero travel distance. Far means the faction's `MaximumRange`, default **5000m**. Every endpoint value interpolates linearly using `distance / MaximumRange`. The distance is horizontal, from the admitted ground cohort's centre to the clicked destination or drag midpoint; a queued segment estimates its origin from predecessor Move destinations. Aircraft do not affect that ground centre.

The profile is captured for the new command. It does not become stronger merely because units approach their destination. There are no Close/Medium/Long stages.

At exactly **MaximumRange**, the Far settings still apply. Beyond it, an ordinary Move has **no custom arrival pacing or holding**. Native group slowdown remains suppressed while that faction's cohesion switch is enabled, so an over-range move does not fall back to the native crawl. Native Reverse uses the separate strict snapshot behavior below. Explicit movement modes, terrain, combat and physical obstructions still apply.

Arrival pacing estimates remaining travel time, slows early members within the configured limits, and allows the stated early-arrival window. It aims for approximate coordination; it cannot guarantee simultaneous arrival through obstacles. Initial direct-distance estimates seed provisional caps promptly, including on very short moves. Actual application requires usable captured paths and respects native pauses and movement limits.

`MaxDelay` limits each member in two ways: cumulative deliberately lost travel time, approximately `elapsed * (1 - cap / uncappedModeSpeed)`, and a deadline at its initial uncapped arrival estimate plus the budget. Startup permits a one-time path-based ETA rebase, not repeated extensions for a failing rear unit. Expiring either allowance ends that member's pacing. These are controller budgets, not exact physical arrival-delay measurements.

### Fresh-path startup

Failure accounting waits until a fresh usable path is associated with the captured Move and the existing `StartGrace` has elapsed, default 5 seconds for Humans and 3 seconds for Aliens. Stuck/move-problem state left over from the preceding command should not immediately release the new group. Provisional caps are still prepared immediately; fresh route lengths replace provisional timing once. Subsequent congestion does not keep moving the shared deadline. Queued commands retain captured order-ID activation checks.

## Dragged layouts and ordinary clicks

An ordinary one-click Move clears dragged-layout memory for the selected group and uses fresh grid placement with arrival pacing. This also applies when the click is queued. It does not translate the previous dragged shape or recreate memory from the units' current physical arrangement.

A deliberate drag still defines its own intended slots and facing, with arrival pacing while reforming. Substantial facing changes attenuate its profile: with `r = clamp01((absYaw - 15) / 75)`, arrival strength is multiplied by `1 - 0.85*r`, holding strength by `1 - r`, and the minimum custom speed fraction becomes at least `0.8*r`. Native movement can still be slower.

Valid drag memory can remain available for Reverse. Destroyed/despawned losses of at most 40% of the original group retain it; exactly 40% is allowed. The original denominator remains fixed. Live omissions, additions, merged selections, control/team/eligibility changes and greater casualties invalidate it. Retention after completion defaults to 500 seconds. A fresh drag establishes a new baseline; an ordinary click explicitly clears it. Existing native orders are not canceled merely to clear memory.

## Native Reverse and snapshot holding

When that faction's cohesion is enabled, using native Reverse captures eligible reverse-capable ground vehicles and eligible infantry in the same snapshot. Each destination is the clicked anchor plus that unit's original world-space offset from the group centre. The shape is translated without rotating its offsets toward travel. Queued Reverse uses the last queued Move/Reverse destinations; unsupported predecessors leave the command native.

Vehicles receive native `AIReverseOrder`; infantry lack that receiver and receive ordinary `AIMoveOrder` to their snapshot slots. Infantry turn and walk/run normally, with the requested speed mode and queue flag retained. Both the formation-move and direct command-card/network routes handle this fallback. Accepted Move and Reverse order IDs are tracked independently inside the same group. At least two accepted members are needed for strict pacing; a single eligible soldier can still receive its ordinary Move. Reverse does not require a previous drag. A fresh snapshot cannot create reusable drag memory by itself; reversing the whole valid remembered dragged group retains its drag lineage and original casualty baseline, while a later ordinary click clears that memory and uses fresh placement. Unsupported/player-driven movement, excessive membership, distorted projection or lost physical clearance keeps native handling. Partial native acceptance releases strict pacing for the whole snapshot and reports the accepted count; accepted native orders continue.

Infantry fallback runs once after the native batch completes. A changed order processor, replacement or queued append since preparation cancels that soldier's fallback. With faction cohesion OFF it can still move eligible infantry to their snapshot slots, while vehicle destinations and pacing remain native. The Formation master switch still controls operation. No infantry animation, facing lock or infantry-only client Reverse button is added.

Reverse uses full arrival/holding strength rather than ordinary distance/turn relaxation, can request a complete pause for a leading unit, and retains the bounded 120-second per-member delay allowance plus ordinary controller lifetime and release checks. Its longitudinal hold tolerance is at least 0.5m or 10% of unit radius, whichever is larger. Requested speed estimates use native `Vehicle.ReverseNormalSpeed`, including when Fast is requested; the game has no separate ReverseFastSpeed.

Fixed slot rotation does not lock actual vehicle yaw. The native reverse controller still steers, routes around obstacles, handles collision avoidance and respects engagement/path holds. A turret aiming at an enemy alone does not cancel Reverse pacing; native movement/engagement holds still take precedence. Actual geometry and simultaneous arrival are not guaranteed. No transform rotation, teleport or repeated Reverse order is used to enforce the snapshot.

## Drag width

A narrow valid drag starts at a rectangle-sized minimum width, using the ordinary click grid's row/column count and the dragged layout's physical footprint and size-influence calculations. Widening the drag admits more units per row, progressing toward two rows and then one row, followed by greater lateral spacing.

Native unit order and front/back priority bands remain intact. Different priority bands retain at least one row each, so a mixed-priority selection may not collapse into a single overall row. The requested midpoint and facing are retained; physical clearance and terrain/navigation projection remain in force.

A tiny or degenerate drag rejected by native line placement uses the ordinary grid fallback at the native click anchor. It does not create remembered drag identity. Aircraft stay separate. The vanilla client preview can differ from these server destinations.

## The 12 visible controls per faction

Use `/cohesion` for Human values and `/aliencohesion` for Alien values. Their defaults differ as shown below; changing one does not copy it to the other. Minimum/maximum and step below are menu metadata; commands validate the numeric range and endpoint ordering.

| Key | Human default | Alien default | Valid range | Menu step | Effect |
| --- | --- | --- | --- | --- | --- |
| `MaximumRange` | 5000m | 5000m | 1–100000m | 1m | Far interpolation endpoint and custom-cohesion distance limit |
| `DestinationRadius` | 700m | 700m | 0–10000m | 25m | Local-cohort radius when Nearby destination cohesion is ON; 0 disables the faction's fallback |
| `StrengthNear` | 1 | 1 | 0–1 | 0.05 | Arrival pacing strength at zero distance |
| `StrengthFar` | 0.2 | 0 | 0–1 | 0.05 | Arrival pacing strength at MaximumRange |
| `HoldStrengthNear` | 1 | 1 | 0–1 | 0.05 | Remembered-formation holding strength at zero distance |
| `HoldStrengthFar` | 0 | 0 | 0–1 | 0.05 | Remembered-formation holding strength at MaximumRange |
| `MinimumSpeedNear` | 0.05 | 0.15 | 0.05–1 | 0.05 | Minimum custom cap as a fraction of uncapped requested-mode speed |
| `MinimumSpeedFar` | 1 | 1 | 0.05–1 | 0.05 | Corresponding speed fraction at MaximumRange |
| `ArrivalWindowNear` | 0.5s | 2s | 0–60s | 0.25s | Allowed early arrival at zero distance |
| `ArrivalWindowFar` | 15s | 20s | 0–60s | 0.25s | Allowed early arrival at MaximumRange |
| `MaxDelayNear` | 20s | 30s | 0–120s | 0.5s | Per-member pacing budget at zero distance |
| `MaxDelayFar` | 2s | 3s | 0–120s | 0.5s | Per-member pacing budget at MaximumRange |

At the default profile midpoint, 2500m, Human arrival strength is 0.6, holding strength 0.5, minimum speed fraction 0.525, arrival allowance 7.75s and pacing budget 11s. Alien values are 0.5, 0.5, 0.575, 11s and 16.5s respectively. Major turns can weaken those captured values as described above.

Strength, HoldStrength and MaxDelay must not increase toward Far. MinimumSpeed and ArrivalWindow must not decrease. Setting a strength to zero disables that corresponding custom effect. A minimum speed fraction does not accelerate a naturally slower, damaged or blocked unit.

## Dispersed selections and destination-local arrival

**Nearby destination cohesion** is a shared switch in **Commander placement**, default **OFF**. `/destinationcohesion on|off|status` controls it; changes require AdminMod Generic permission and save as `DestinationLocalCohesion`. A missing key defaults OFF even if the existing faction radii are 700m. The switch applies to both factions and does not disable ordinary compact-group cohesion.

With the switch ON, if a ground selection is too dispersed or exceeds `MaxUnits`, `DestinationRadius` can admit only eligible selected ground units already inside the horizontal radius around the clicked destination or drag midpoint. The nearest qualifying members are admitted up to `MaxUnits`; at least two valid accepted moves are required.

Membership is captured when the order is issued, including queued commands. Unselected units are never recruited, and distant members do not join later when they reach the area. Switching OFF immediately releases destination-local pacing, including queued groups, while preserving native orders and destinations. It retains each faction's radius. Setting `DestinationRadius` to 0 also disables that faction's fallback. OFF uses ordinary independent movement for scattered/oversized selections; compact selections retain their normal cohesion rules.

The local cohort receives a fresh destination layout and arrival pacing, with no remembered-layout holding. Other selected ground units receive independent destinations in a separate rear band. Aircraft are placed over the local layout's rear. Distant selected units do not determine the local cohort's speed or failure denominator. The continuous distance profile applies to the actual admitted cohort.

## Aircraft, eligibility and release

While Formation is enabled, commander click/drag placement separates aircraft from ground footprints whether cohesion is ON or OFF. Mixed formations place aircraft in rows 50m behind the rear edge of the ground footprint, spanning its width. Physical clearance can require additional rows farther back. All-air selections retain their own command anchor. Invalid rear slots leave that aircraft on its previous native order and emit a watched/global diagnostic; a grouped waypoint then waits for that unresolved member only until its regroup deadline. Their own navigation graph and existing preferred altitude remain native. Takeoff is not forced. Arrival uses a horizontal cylinder with the native final-waypoint radius; altitude does not affect arrival or regroup travel distance.

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
| `/destinationcohesion on\|off\|status` | Shared nearby-destination fallback switch/status; defaults OFF |
| `/waypointwait near\|far <seconds>` | Set shared regroup grace, 0..120 seconds; defaults near 5s, far 15s |
| `/waypointwait status` | Read grace settings and 1000m waiting cutoff |
| `/cohesion <key> <number>\|status` | Set/read a Human control |
| `/aliencohesion <key> <number>\|status` | Set/read an Alien control |
| `/cohesiondebug next` | Trace your next grouped commander move, including skip reasons; commander role required |
| `/cohesiondebug groups` | List recent retained groups |
| `/cohesiondebug <id>` | Read a group's recent history and watch it |
| `/cohesiondebug on\|off\|status` | Shared global diagnostics; OFF also clears individual/next-group watches |
| `/travelcoverdisabler on\|off\|status` | ON disables infantry travel-cover waypoint preference; OFF restores native preference |

The switch/tuning commands without arguments show status. Diagnostics identify faction, phase and continuous distance/beyond-range state. Native dispatch attempts, acceptance/rejection and missing-attempt records are reported separately from custom cohesion enrollment. Per-unit records include target transformations and captured-order state, while samples distinguish requested versus actually applied caps and the reason a cap was skipped. Zero cohesion enrollment alone does not show whether a native single-unit Move succeeded.

Periodic detail is bounded: global tracing samples up to 32 members per group in rotation; an explicitly watched group can sample up to 256. Timing, aggregate history and retention use the owning faction's existing controls. There are no new debug settings. Existing group/slider and generic `controlPresentation` compatibility metadata are preserved; no additional ClientModUI or native-preview work is included in 3.9.0. Rendering depends on the loaded bridge.

Travel cover preference disabler defaults **ON** independently of cohesion. It changes infantry long-path cover substitution for new/recalculated paths while retaining native Stealth completion-area handling. Combat cover decisions, claims and behavior trees remain unpatched; already-chosen combat-cover routes also skip the shared travel substitution. It does not immediately repath existing orders.

Scouting remains a separate one-shot commander action. Eligible Alien air units can use their actual graph and traversable tags in bounded destination checks. Up to 75 receive scout-sector orders; remaining normally movable selected units, including failed/excess scouts, receive normal formation destinations around the original click. Protected or navigation-unready units remain unchanged. Native acceptance can still fail and is reported. Scouting does not create a remembered dragged shape.

## Bounded waypoint regrouping and layout diagnostics

New commander Move/Reverse orders capture the selected commandable AI group, including aircraft. At activation, the main-body centre is the horizontal coordinate-wise median of living members' actual positions. If no member is within 1000m of that median, the member nearest the commanded anchor breaks the split tie. Members **more than 1000m** from that centre are excluded from waiting only; they keep their formation membership and every accepted waypoint order.

One shared ETA divides the longest remaining included route by the slowest included unit's uncapped requested-mode speed. Current velocity and custom speed caps are not used. Provisional horizontal distances receive at most one startup correction when paths are ready, or after 3 seconds; subsequent reroutes never extend the deadline. Aircraft use horizontal distance. At every new waypoint, positions, inclusion and timing are recalculated, automatically restoring caught-up units to waiting calculations.

The group advances as soon as every included member has native completion, or at ETA plus grace. Ground completion honours the native evaluator, including its order-ended handling; aircraft use the horizontal final-waypoint radius regardless of altitude. Completion is latched. Grace interpolates from **5s** at zero distance to **15s** at/beyond the faction's `MaximumRange`, using current main-body centre to command anchor. Admin panel sliders under Commander placement and `/waypointwait near|far <seconds>` configure 0..120 seconds; `/waypointwait status` reads them. Settings are captured on activation. Timeout releases every owned queued head, including excluded stragglers, so each continues to the next waypoint.

This is independent of cohesion ON/OFF and pacing budgets. Death/despawn removes members; explicit replacements, takeover, team changes and forced control retain native precedence. Rejected/AI-interrupted moves remain unresolved until timeout; a rejected native order cannot be manufactured by the queue gate. Attack, Patrol and utility orders retain native completion. Master/round/scene/unload cleanup releases ownership. `[Waypoint #...]` diagnostics distinguish native completion, horizontal air arrival, distance exclusion, initial/corrected ETA, countdown and regroup timeout.

Drag creation and Reverse projection share a circular footprint floor plus 0.5m clearance. Debug traces identify failed units, desired/projected coordinates, projection displacement and pair separation versus required clearance. Aiming alone no longer disables pacing; native paused paths, engagement holds, obstructions and traversal exclusions remain authoritative. Traversal exemptions and pacing deadlines retain their previous values.

Perimeter defense remains removed; native Defend is authoritative. Old saved defense keys are ignored. Human harvesters and Alien Shrimp remain excluded from rally intervention.

## Configuration and older versions

The file remains `UserData/cfg/Formation/si_formation_native.cfg`, category `[Si_Formation_Native]`.

- Human switch: `GroupCohesion`; numeric keys: `Cohesion<Key>`.
- Alien switch: `AlienGroupCohesion`; numeric keys: `AlienCohesion<Key>`.
- Travel-cover switch: `TravelCoverPreferenceDisabler`.
- Nearby-destination switch: `DestinationLocalCohesion`, default OFF.
- Shared regroup grace: `WaypointGraceNear` (5s), `WaypointGraceFar` (15s), both 0..120 seconds. Commander placement exposes two sliders; `/waypointwait` provides the same settings. They are separate from per-member cohesion pacing budgets.

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

## Build and user-owned live checks

Build command: `dotnet build Si_Formation.csproj -c Release --ignore-failed-sources`. The 3.11.0 Release build passed on 13 November 2026 with 0 warnings and 0 errors in 9.02 seconds. Output: `bin/Release/netstandard2.1/Si_Formation.dll`. Staged only; no tests, live checks or server lifecycle actions were run.

User-owned checks: compare a drag followed by ordinary clicks, fresh drags and Reverse; normal clicks must reset memory. Check mixed ground/air placement at 50m behind the rear, all-air anchors and air arrival at different heights. Queue multiple waypoints with early arrivals, slow units, blocked members, casualties and takeover. Check the 1000m cutoff from current main-body positions, automatic re-inclusion at later waypoints, 5-to-15s interpolation, changed grace settings and one-time path correction without deadline drift on reroutes. Inspect native-completion, horizontal-air, timeout and exclusion diagnostics. Retain normal navigation, map-edge, rally, scouting and native Defend checks.

User observations from 12 November on **3.8.0** described subtle/natural cohesion, desirable short-move holding, no noticeable long-move delay and useful rectangle-start dragging. They also identified native-preview misalignment and a need for smoother presentation. These are qualitative observations, not measured tests or 3.9.0 validation. Client preview/smoothing work is deferred; native obstacle handling, multiplayer presentation and other movement-mod interactions still need live checks.
