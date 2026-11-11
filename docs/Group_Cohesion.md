# Group cohesion — Si_Formation 3.7.0 candidate

The required dedicated-server Release build for 3.7.0 passed on 11 November 2026 with zero warnings and zero errors. No tests, installation, live checks or game/server start/stop actions were performed. This documentation update performs no additional build.

Group cohesion adds bounded coordination to ordinary commander ground Move orders. **Human and Alien cohesion are independently configurable and OFF by default**. Close moves can turn and preserve a formation snapshot while coordinating arrival; medium and long moves coordinate arrival with progressively lighter pacing. A dispersed or oversized selection can instead coordinate a bounded cohort already near its destination, without waiting for distant members. Aircraft receive separate placement without joining ground speed coordination.

This is a server-side implementation. Source inspection or compilation does not establish gameplay behavior, client presentation or multiplayer compatibility. See [3.7.0 changes](Changes_3.7.0.md) for the travel-cover switch, intended-layout retention and destination-radius fallback. Earlier [3.6.0 changes](Changes_3.6.0.md) and [startup correction](Startup_Fix_3.5.1.md) remain historical records.

Build against dedicated-server assemblies using `dotnet build Si_Formation.csproj -c Release --ignore-failed-sources`. Output remains `bin/Release/netstandard2.1/Si_Formation.dll`. Live checks remain user-owned. The [3.6.1 partial-selection rule](Changes_3.6.1.md) remains in place.

The assembly/config identity and schema-2 `ModUIData()` / `ModUIPlayerData(ulong)` signatures are retained. No legacy FormationApi, optimizer bridge or client protocol was added. The patches share native order and movement-cap endpoints with other gameplay mods, so installed patch-stack behavior remains a live compatibility check. The menu exports **162 rows** using the existing `ui.group` presentation contract; no ClientModUI changes are required by this implementation. Actual collapsing and presentation depend on the loaded bridge.

## Movement behavior

The distance is measured horizontally from the ground cohort's centre to the clicked destination, or the midpoint of a dragged line. Aircraft do not influence that centre, ground footprint or distance classification. A queued segment uses the previous queued ground Move destinations to estimate its starting centre. The faction's range profile and its interpolated values are captured for that command; they do not change as units approach the destination. Both factions initially use the defaults below.

| Default distance | Profile | Single click | Click and drag |
| --- | --- | --- | --- |
| Below 500m | Close | Turn the intended layout toward travel, then use formation holding and arrival pacing | Use the newly specified formation and arrival pacing |
| 500m through 1000m, inclusive | Medium | Preserve the intended layout; arrival pacing | Use the newly specified formation; arrival pacing |
| Above 1000m | Long | Preserve the intended layout; lighter arrival pacing | Use the newly specified formation; lighter arrival pacing |

Exactly **500m starts Medium**; exactly **1000m remains Medium**. Long interpolation reaches its far endpoint at **3000m** and retains those values for longer moves. Long distance does **not** disable arrival cohesion.

For unchanged ground membership, each unit keeps its intended relative destination offset. A repeated click with the same complete ground cohort reuses its remembered layout and heading instead of capturing temporary obstacle detours. An entirely unknown cohort can capture its current positions and facing. **Close clicks yaw that snapshot toward the new travel direction**; Medium/Long clicks carry its heading unchanged. Final positions are terrain/navigation projected and checked for physical clearance. A drag deliberately specifies a new layout and heading using the existing native line-placement rules.

Remembered intention has a separate lifetime from speed ownership. Its age is refreshed while any captured native Move is current or queued, even after pacing cancels. The faction's `Retention` period applies after those moves end. Natural completion or disappearance of a captured order does not immediately erase the cache. Replacement cleanup may remove only the snapshot it observed before the transition, so delayed cleanup from an older command cannot erase a newer layout. Explicit replacement, changed membership, faction/master cleanup, invalidation and expiry still apply.

When a move uses only part of a cached cohort, adds members or merges cached cohorts, the old cached offsets are discarded. The selected ground units get fresh standard native-style click/drag placement with the mod's configured spacing, projection and aircraft separation. This move starts in Arrival coordination rather than holding the old shape. A bounded invalidation marker also makes the unselected remainder reform on its next move; it stores no old offsets and does not change current orders or predecessor pacing. A later full move of the newly formed group can preserve its new layout normally. Aircraft selection changes alone do not invalidate an unchanged ground cohort. Debug creation traces include `membership changed: fresh formation`.

A Close click rotating more than five degrees enters **Turning** when `TurnGrace` is positive. For the default **6 seconds**, the command uses arrival pacing without the longitudinal formation-hold cap. Its own no-progress timeout also waits until that turn allowance ends. At the end, a healthy, settled shape enters Holding; otherwise the command stays in Arrival for the rest of that move. Native obstruction flags, losses, spread and other safety limits can still release coordination during Turning. `TurnGrace = 0` starts Holding immediately with normal failure checks.

Close formation holding follows the legacy one-shot lock principle: issue final destinations once, then regulate longitudinal progress with speed caps. Units ahead of the estimated formation centre can slow; those behind can catch up within their own native speed. It does not impose rigid geometry, correct lateral paths, teleport units, or repeatedly issue Move/path orders. Native navigation, avoidance, terrain and combat still determine the actual route.

Arrival pacing uses remaining native route distance and an estimate of each unit's uncapped speed in its requested movement mode. It sets a shared predicted arrival time, allows the configured early-arrival window, blends pacing with independent speed, and enforces a minimum speed fraction. The predicted arrival time is not continually extended to follow a failing rear unit. Arrival is a best-effort target, not a guarantee.

The additional cap is combined with the native `Unit.MoveSpeedLimit`; an existing lower native cap wins. The controller does not change `MoveSpeedScale`, movement modes or unit speed definitions. Removing its cap immediately returns control to native movement.

## Admission, degradation and release

Custom coordination accepts at least two eligible friendly AI ground units, up to the faction's `MaxUnits`. Aircraft are filtered into their separate placement cohort and do not reject an otherwise eligible ground cohort. A compact selection with an unsupported ground member is rejected. An initial ground radius beyond the approximate packed-body footprint plus `InitialSpread`, or a selection larger than `MaxUnits`, uses the destination-radius fallback below. This handles dispersed map-wide ground selections without relying on the select-all gesture itself. Admission, timing, watchdog and history limits are independent per faction. There is additionally a shared hard capacity of 64 retained/active command records; finished records can be evicted to admit new commands.

### Destination arrival radius

Each faction's `DestinationRadius` defaults to **500m**. For a dispersed or oversized selection, it admits only already-selected eligible friendly ground members within that horizontal distance of the clicked destination or dragged line's midpoint **when the order is issued**. If too many qualify, the nearest members are taken up to `MaxUnits`. At least two valid accepted moves are required. Unselected units are never recruited; units entering the radius later do not join. Queued segments capture this membership when issued too.

The local cohort receives its own layout and **arrival pacing**, even in Close range. It does not enter formation holding or wait for distant selected units. Excluded members do not affect its pace, casualty/failure denominator or profile classification. Distant ground members receive independent native Move destinations in a separate rear band; aircraft remain separately placed over the local formation's rear. The local cohort retains its normal faction profile, budgets, health checks and order-ownership rules.

Set `DestinationRadius` to **0** to disable that faction's fallback and restore fully independent movement for dispersed/oversized selections. Too few eligible nearby members or insufficient valid local slots safely decline custom coordination. Compact selections continue through ordinary admission. The commands are `/cohesion DestinationRadius <number>` and `/aliencohesion DestinationRadius <number>`; cfg keys are `CohesionDestinationRadius` and `AlienCohesionDestinationRadius`.

Eligibility is conservative: player-controlled, driven or embarked units, flyers/aircraft, forced vehicle exits, and units without usable native movement/navigation are excluded. Decapods currently submerged, transitioning, flying, wall-climbing or jumping are excluded; soldiers currently jumping, climbing or falling are excluded. Ordinary Move on empty ground is supported. Attack/targeted/forced-order actions are not enrolled. If a unit begins a combat hold, aims at a target, or encounters a navigation problem, its custom cap is removed while that condition applies.

Enabling a faction's Group cohesion suppresses both native commander slowdown layers for that faction even when a selection skips custom coordination. Skipped selections therefore retain independent movement. With that faction's switch OFF, the existing `MovementFix` switch controls native suppression as before. Turning one faction OFF releases its own cohesion ownership without disabling the other faction. These switches do not change physical obstacles or explicit movement modes.

Each member is owned by its current native Move order ID, processor, destination and requested mode. A replacement order releases that member; appending a queued order keeps the current segment's ownership. Queued coordination waits for the captured segment to become active. If members start that segment more than `StartGrace` apart, the group releases coordination and continues toward its destinations independently.

Close holding can degrade **once** to arrival pacing after sustained formation-offset failures, obstruction, casualties or excess spread. It does not resume holding later in the same command. Full cohesion can subsequently release when the full failure/casualty threshold is reached, or when sustained extra radius exceeds twice the close spread allowance. Medium/long cohesion releases after sustained excess radius or its failure threshold. Route-progress lag is measured relative to each member's own captured route baseline, rather than comparing raw distances to different destination slots.

An individual member permanently leaves pacing for that command when its delay budget expires. Completion, replacement orders, commander/team changes, maximum duration, master disable, its faction's Group cohesion OFF, scene cleanup and unload also remove owned caps. A released command does not automatically regroup. Its already-issued native destination remains in force unless another order replaces it.

`MaxDelay` has two bounds:

1. A cumulative estimate of deliberately lost travel time: each capped interval contributes approximately `elapsed × (1 − cap / uncappedModeSpeed)` seconds.
2. A fixed deadline at the member's initial uncapped arrival estimate plus `MaxDelay`.

Whichever bound expires first ends that member's pacing. These bounds measure the controller's allowance; they are not an exact measurement or guarantee of physical arrival delay. Native congestion, acceleration, combat or terrain can still make travel take longer.

## Separate aircraft placement

While the Formation master is enabled, normal commander click and drag placement separate aircraft from ground packing **whether either cohesion switch is ON or OFF**. Ground size influence, row/column counts, facing and spacing use ground units only. Aircraft use independent centered rows of up to three, beginning over the rear half of the ground layout and extending rearward. Air-to-air body clearance is retained; the ground and air layouts may overlap horizontally. An all-air selection receives its own layout at the requested anchor.

Air membership follows capability (`VehicleAir` or `IsFlyingType`), including flyer-capable Decapods that are currently landed. Positions use the aircraft's own navigation graph. Native air path processing supplies its existing preferred altitude; this placement does not change `FlyPreferredHeight` or force takeoff. Failed/degenerate drag lines use the separated grid fallback. A failed projected destination retains native placement for that unit.

Native Stealth behavior may keep a flyer-capable alien moving on the ground even though it belongs to the air placement cohort. Horizontal overlap with ground troops in that case remains a live safety/presentation check. Air speed synchronization and rigid flight formations are not introduced.

## Infantry travel-cover preference

**Travel cover preference disabler** defaults to **ON**. ON disables the human infantry long-path postprocessor that substitutes nearby cover points for ordinary waypoints and prunes the resulting route. OFF restores native postprocessing. The switch is independent of Human/Alien cohesion and is effective only while the Formation master is enabled on the server.

Only `AISoldierAgent.ProcessLongRangePath` is intercepted. Its native movement-area assignment is preserved, including the smaller Stealth completion area. Native combat state, threat decisions, cover selection, reservations/claims, `TakeCover` and `BTTakeCover` are not patched. Infantry can still select combat cover. A combat-selected route may also pass through this postprocessor: ON preserves that chosen destination but skips another round of travel-cover substitution, so it does not promise identical combat travel paths.

Use `/travelcoverdisabler on|off|status`. Changes require Generic permission; the public Formation Status report also reads the state. The cfg key is `TravelCoverPreferenceDisabler`, default `true`. Toggling affects only newly calculated or recalculated long paths; it neither reissues current orders nor immediately rebuilds an existing path. This infantry-specific switch does not add cover navigation to Aliens or vehicles.

## Commands and admin controls

All standalone commands below require AdminMod **Generic** permission. Settings are shared server settings, not personal preferences. Humans and Aliens have independent switches and **72 numeric controls each**: 16 general controls and 56 profile endpoints. The default values/ranges below initially match between factions; editing one never copies its values into the other. Use a decimal point for numeric values.

| Command | Effect |
| --- | --- |
| `/groupcohesion on\|off\|status` | Enable, disable/release or inspect **Human** cohesion; changes save immediately |
| `/aliengroupcohesion on\|off\|status` | Enable, disable/release or inspect **Alien** cohesion independently |
| `/cohesion status` | Show Human state and range boundaries |
| `/aliencohesion status` | Show Alien state and range boundaries |
| `/cohesion <key> status` | Read one Human value; omitting `status` also reads it |
| `/aliencohesion <key> status` | Read one Alien value; omitting `status` also reads it |
| `/cohesion <key> <number>` | Validate and save a Human tuning value |
| `/aliencohesion <key> <number>` | Validate and save an Alien tuning value |
| `/travelcoverdisabler on\|off\|status` | ON disables infantry travel-cover waypoint preference; OFF restores native travel preference; status reads the switch |
| `/cohesiondebug next` | Trace the commander's next grouped move, including skip reasons; requires commander role |
| `/cohesiondebug groups` | List up to eight recent retained groups and their latest state/reason |
| `/cohesiondebug <id>` | Show up to eight recent samples for a retained group and watch subsequent samples |
| `/cohesiondebug on` | Enable periodic global logging to the MelonLoader server log |
| `/cohesiondebug off` | Disable global logging and clear individual/next-group watches |
| `/cohesiondebug status` | Show trace state and current cohesion summary |

The two switch commands, two tuning commands and `/cohesiondebug` without arguments show their respective status. Diagnostics are shared across factions; trace lines identify **Human** or **Alien**, range and phase. Sampling intervals, history size, retention and group update cadence still use the owning faction's values. Debug selection and history are transient. A group ID expires when its retained record is pruned or evicted.

The Formation admin menu supplies **General**, **Commander placement**, **FPS squads**, **Rally**, four **Human cohesion** groups (General/Close/Medium/Long), four matching **Alien cohesion** groups, and **Diagnostics**. Existing `ui.group` metadata lets compatible bridges collapse these sections. Slider labels also name their faction and range so older bridges remain understandable. Structured ModUI bridges receive each slider's minimum, maximum, step, unit and default. General cohesion changes can affect active commands; range-profile values are captured for new commands.

The existing `/alienspread` placement scale appears in **Alien cohesion – General** for discoverability. It still affects Alien commander, FPS and rally placement regardless of the Alien cohesion switch; it is not one of the 72 cohesion controls. The **Travel cover preference disabler** checkbox appears under **Commander placement** and is separate from both faction cohesion switches.

Examples:

```text
/groupcohesion on
/aliengroupcohesion on
/cohesiondebug next
/cohesion CloseStrengthNear status
/cohesion MediumMinimumSpeedFar 0.75
/aliencohesion TurnGrace 8
/cohesion DestinationRadius 500
/aliencohesion DestinationRadius 0
/travelcoverdisabler status
/cohesiondebug groups
/cohesiondebug 12
```

## Configuration and common settings

The filename and category remain `UserData/cfg/Formation/si_formation_native.cfg`, `[Si_Formation_Native]`. Existing **Human** settings retain `GroupCohesion` and `Cohesion<Key>`, preserving previously saved Human values. **Alien** settings use the new `AlienGroupCohesion` and `AlienCohesion<Key>`. Both switches default to `false` when absent; a previously saved Human switch value is preserved. Alien defaults come from built-in defaults rather than loaded Human settings.

`TravelCoverPreferenceDisabler` is a separate shared boolean defaulting to `true` when absent. It is effective only while the Formation master is enabled on the server. Existing cfg values are preserved.

For example, command key `UpdateInterval` maps to Human `CohesionUpdateInterval` or Alien `AlienCohesionUpdateInterval`; `CloseStrengthNear` maps to `CohesionCloseStrengthNear` or `AlienCohesionCloseStrengthNear`. `/cohesion` and `/aliencohesion` accept their prefixed numeric keys as well as short keys. The following tables apply separately to both factions.

Commands save immediately. For direct file edits, edit the cfg while the server is stopped. Numeric values must be finite. Whole-number controls reject fractions. Loaded out-of-range numbers are clamped; invalid/nonfinite numbers use defaults. Invalid boundary ordering or reversed endpoint pairs restore the affected defaults and produce a server-log warning.

| Command key | Default | Valid range | Menu step | Meaning |
| --- | --- | --- | --- | --- |
| `CloseRange` | 500m | 1–10000m | 1m | Close/medium boundary; the boundary itself is Medium |
| `MediumRange` | 1000m | 2–50000m | 1m | Medium/long boundary; the boundary itself is Medium |
| `LongRange` | 3000m | 3–100000m | 1m | Long-profile far interpolation endpoint |
| `UpdateInterval` | 0.35s | 0.1–5s | 0.05s | Minimum interval between pacing evaluations |
| `InitialSpread` | 250m | 0–2000m | 10m | Starting radius allowance beyond the approximate packed-body footprint |
| `DestinationRadius` | 500m | 0–10000m | 25m | Issue-time radius for local arrival pacing when a selection is dispersed or oversized; 0 disables fallback |
| `StartGrace` | 3s | 0–30s | 0.5s | Initial no-progress grace and allowed separation of queued segment start times |
| `DebugInterval` | 3s | 0.25–60s | 0.25s | Minimum periodic trace interval for global/watched groups |
| `MaxUnits` | 256 units | 2–1024, whole number | 1 | Maximum selection admitted to a custom command |
| `MaximumDuration` | 600s | 5–3600s | 5s | Maximum command lifetime, including time waiting in a queue |
| `Retention` | 120s | 0–1800s | 5s | Finished-layout identity and diagnostic retention; active ownership has a separate lifetime |
| `PathDetourRatio` | 1.8× | 1–10× | 0.1× | Route/direct-distance ratio beyond which a member counts as obstructed for holding |
| `HoldGrace` | 2s | 0.25–30s | 0.25s | Required persistence of excess spread or hold failure before degradation/release |
| `TurnGrace` | 6s | 0–30s | 0.5s | Arrival-only pacing while a rotating Close click settles before checking whether to begin holding |
| `ProgressDistance` | 1m | 0.1–20m | 0.1m | Position change or route-distance gain that refreshes no-progress time |
| `TraceHistory` | 80 samples | 10–500, whole number | 1 | Maximum retained trace samples per group |

Each faction's boundaries must satisfy `CloseRange < MediumRange < LongRange`. Layouts and remembered headings are refreshed while any captured Move remains current or queued, including after speed pacing releases; the finished retention period follows those moves. Reusing layout identity requires the same complete ground membership and team. Menu steps are presentation hints; numeric commands validate the stated ranges rather than requiring step multiples.

## Profile settings and interpolation

Each setting uses `<Profile><Setting>Near` and `<Profile><Setting>Far`, where Profile is `Close`, `Medium` or `Long`. Near/Far are distance endpoints, not near/far units within a group. Interpolation is linear inside each profile:

- Close: 0m toward `CloseRange`, with its far value approached just below that boundary.
- Medium: `CloseRange` through `MediumRange`.
- Long: above `MediumRange` toward `LongRange`, clamped at its far value thereafter.

Profiles can have distinct values at their boundaries. Strength and permitted delay decrease toward Far; minimum speed and arrival allowance increase. Close spread/lag/failure tolerances tighten toward Far, making holding easier to abandon. Medium/long spread and lag allowances increase to tolerate longer routes, while failure thresholds and no-progress timeouts decrease.

| Setting suffix | Close Near → Far | Medium Near → Far | Long Near → Far |
| --- | --- | --- | --- |
| `Strength` | 1 → 0.85 | 0.65 → 0.35 | 0.4 → 0.25 |
| `MinimumSpeed` | 0.25 → 0.4 | 0.5 → 0.7 | 0.65 → 0.8 |
| `ArrivalWindow` | 0.75 → 2s | 3 → 6s | 8 → 15s |
| `MaxDelay` | 12 → 8s | 6 → 3s | 4 → 2s |
| `MaxSpread` | 60 → 30m | 60 → 120m | 180 → 300m |
| `LagDistance` | 30 → 15m | 30 → 60m | 90 → 150m |
| `StuckSeconds` | 3 → 2.5s | 2.5 → 2s | 2 → 1.5s |
| `BreakPercent` | 70 → 55% | 40 → 30% | 20 → 10% |

For example, `MediumMinimumSpeedFar = 0.7` means the custom cap cannot deliberately fall below 70% of the requested mode's estimated uncapped speed at the far end of Medium. Native movement may still be slower. It does not raise a blocked or damaged unit's speed.

| Setting suffix | Valid range | Menu step | Meaning |
| --- | --- | --- | --- |
| `Strength` | 0–1 | 0.05 | Blend from independent speed (0) to full configured pacing (1) |
| `MinimumSpeed` | 0.05–1 | 0.05 | Lower bound on the custom cap as a fraction of uncapped requested-mode speed |
| `ArrivalWindow` | 0–60s | 0.25s | Allowed early arrival relative to the shared prediction |
| `MaxDelay` | 0–120s | 0.5s | Cumulative slowdown budget and initial free-arrival deadline allowance |
| `MaxSpread` | 0–2000m | 5m | Extra group radius above its captured starting footprint |
| `LagDistance` | 0–1000m | 5m | Route-progress lag allowance relative to the group's median progress |
| `StuckSeconds` | 0.5–30s | 0.5s | No-progress timeout after startup grace; adjusted to avoid mistaking deliberately slow pacing for a blockage |
| `BreakPercent` | 1–100% | 1% | Full-release threshold for casualties/lost members or unhealthy moving members |

The following keys exist only in Close and also have Near/Far endpoints:

| Setting suffix | Near → Far | Valid range | Menu step | Meaning |
| --- | --- | --- | --- | --- |
| `HoldError` | 3 → 6m | 0.5–100m | 0.5m | Formation-offset tolerance; physical size can increase it |
| `HoldBreakPercent` | 40 → 30% | 1–100% | 1% | Formation/casualty threshold for degrading holding to arrival pacing |
| `ObstructedPercent` | 50 → 35% | 1–100% | 1% | Obstructed/stuck moving-member threshold for degrading holding |
| `HoldCorrectionInterval` | 0.75 → 1.5s | 0.25–10s | 0.25s | Hold-health check interval; it does not issue corrective path orders |

Each `CloseHoldBreakPercent` endpoint must be lower than the matching `CloseBreakPercent` endpoint, so formation holding releases before full cohesion. Endpoint ordering is validated: Strength, MaxDelay, StuckSeconds, BreakPercent, Close MaxSpread/LagDistance/HoldBreakPercent/ObstructedPercent must not rise toward Far; the other pairs must not fall. When changing both endpoints, set them in an order that keeps the intermediate pair valid.

## Live evaluation

Use `/cohesiondebug next` before a grouped commander move to record faction, profile, accepted ground-member count, phase transitions and release reasons. Compare a short single click with a short drag, then medium and long moves; include mixed speeds, obstacles, replacement orders and queued segments. Repeated Close clicks in different directions should show Turning followed by Holding or Arrival, with no repeated Move issuance. A dispersed/oversized selection should coordinate only its valid destination-local cohort, or skip if too few qualify; setting `DestinationRadius` to 0 should restore independent movement for that selection. A blocked member should not hold the rest indefinitely.

Check Human and Alien switches/values independently. Compare ground-only, mixed ground/air and all-air selections with both faction switches OFF as well as ON; aircraft should not expand ground spacing. Include short/degenerate drags and native Stealth orders to flyer-capable aliens. Verify grouped and older-bridge menu presentation without changing ClientModUI.

Compare fresh Normal/Stealth long paths with `/travelcoverdisabler on` and `off`; verify native combat-cover reactions still occur. Repeat complete-group clicks after natural completion and after pacing releases while a captured move is still pending. Include queued transitions and partial selections to check that old cleanup cannot erase a new intended layout and that the existing partial-group reformation rule remains intact.

Check server-log traces alongside the actual unit motion. Client visual smoothing, unmodified-client presentation, native collision/arrival behavior, mixed locomotion coverage and interaction with other movement mods remain live-verification items. This document reports source behavior and defaults; it does not claim those gameplay checks have passed.
