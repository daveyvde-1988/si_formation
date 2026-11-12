# Native formation tuning — Si_Formation 3.8.2

This is the native implementation for Silica game 1.0. The mod version is **3.8.2**, and this `si_formation` folder is its canonical source. The legacy implementation is preserved on [legacy/pre-native-1.0-20261111](https://github.com/daveyvde-1988/si_formation/tree/legacy/pre-native-1.0-20261111) and in a separate local archive. The native implementation does not include the old public FormationApi or custom blueprint system.

- [Group cohesion quick guide (3.8.2 PDF)](docs/Formation_Quick_Summary.pdf) | [Editable quick guide](docs/Formation_Quick_Summary.md)
- [Group cohesion settings, behavior and debug guide](docs/Group_Cohesion.md)
- [3.8.2 tuned defaults](docs/Changes_3.8.2.md)
- [3.8.1 turn pacing, fresh-path startup, diagnostics and scouting](docs/Changes_3.8.1.md)
- [3.8.0 continuous distance profile and drag-only intended layouts](docs/Changes_3.8.0.md)
- [3.7.0 travel cover, intended layouts and local arrival cohorts](docs/Changes_3.7.0.md)
- [3.6.1 partial-selection reformation](docs/Changes_3.6.1.md)
- [3.6.0 rotation, Alien settings and air placement](docs/Changes_3.6.0.md)
- [3.5.1 startup correction](docs/Startup_Fix_3.5.1.md)
- [Purpose and functions overview (3.4.4 PDF snapshot)](docs/Formation_Overview.pdf)
- [Editable overview](docs/Formation_Overview.md)
- [Source migration and legacy archive](docs/MIGRATION.md)

## Menu and permissions

Regular players have one Formation Status button. `/formation` and `/formation status` invoke the same read-only report, which aggregates the existing per-setting status handlers and reports active features, spacing, rally reservations, FPS groups and commander scout state. Reading status neither saves configuration nor calculates or issues formations.

Tuning and cohesion debug commands require AdminMod Generic permission. Admins may change settings from either FPS or commander role; watching one's next move requires commander role. Existing slash syntax remains supported; the menu uses the registry's `!` spelling for admin commands. `/scout [on|off|status]` remains a commander action and is an alias of the shared player command callback, so it does not add a second Controls entry. It may still appear as an alias in the generic command reference.

The provider supplies collapsible groups. Updated ClientModUI clients also read the generic `controlPresentation` hints retained by the original Si_ModUI_Server 1.4.4, which discards the command-level `ui` metadata. Both the updated client and this provider export are required for categories with that companion; no companion replacement is needed.

- **General / Commander placement / FPS squads / Rally:** master control, ordinary placement/movement tuning, follower controls and production parking.
- **Human cohesion:** independent enable switch and 12 numeric controls for one continuous distance profile.
- **Alien cohesion:** an independent switch and 12 separate numeric controls. Alien placement spacing is also located here, with its effect independent of the cohesion switch.
- **Diagnostics:** global and per-group debug actions shared by both factions. Labels retain faction names and near/far endpoints for clients that do not consume grouping metadata.

The presentation compatibility export is retained, with no command-handler, gameplay or personal-setting replay change from that compatibility feature. In-game accordion verification remains pending. Version 3.8.2 updates defaults only; it adds no ClientModUI change or native-preview hook.

The schema-2 export contains **42 rows**. Both faction switches default ON and preserve saved values. Each faction now has one profile from zero distance to `MaximumRange` (default **5000m**); longer commands use no custom pacing or holding. Near/Far mean the profile's zero-distance and maximum-distance endpoints, not different units. Old Close/Medium/Long boundaries and endpoint keys are ignored.

Compatible structured ServerModUI/ClientModUI versions can render the supplied groups as collapsible sections and use their slider ranges/descriptions. Older bridges that discard per-command `ui` hints can show ordinary admin value/choice controls without accordions or typed sliders. Rendering depends on the compatible UI bridge; this implementation does not supply a client mod or an additional UI polling loop.

## Server configuration

Configuration is saved by MelonPreferences in `UserData/cfg/Formation/si_formation_native.cfg`, category `[Si_Formation_Native]`. Admin changes save immediately. Existing saved values take precedence over the 3.8.2 defaults; absent settings use the tuned defaults below. The old `si_formation.cfg` and blueprint catalogs are untouched. Loaded cohesion numbers are clamped to valid bounds; nonfinite values and reversed endpoint pairs restore the affected defaults with warnings. Edit the file while the server is stopped.

This version uses one shared server configuration and does not opt into ClientModUI's personal preference restoration. Existing client preference files are neither read nor migrated, and regular-player replay attempts cannot change server settings. Pending scout orders remain transient command state, not personal configuration.

| Setting | Command | Default | Valid values |
| --- | --- | --- | --- |
| Enabled | `/formation enabled on\|off` | true | on/off |
| MovementFix | `/movementfix on\|off\|status` | true | on/off |
| GroupCohesion | `/groupcohesion on\|off\|status` | true | on/off |
| AlienGroupCohesion | `/aliengroupcohesion on\|off\|status` | true | on/off |
| TravelCoverPreferenceDisabler | `/travelcoverdisabler on\|off\|status` | true | ON disables infantry travel-cover substitution; combat decisions remain native |
| FpsSpacing | `/fpsspace <value>` | 32% | 0..100% |
| FpsDistance | `/fpsdistance <value>` | **10%** | 0..100% |
| FpsAllowance | `/fpsallowance <value>` | 20m | 1..100m |
| Spacing | `/formationspace <value>` | 32% | 0..100% |
| Rally | `/rallyspace <value>` | 25% | 0..100% |
| SizeInfluence | `/sizeinfluence <value>` | 30% | 0..100% |
| AlienSpread | `/alienspread <value>` | 50% | 0..100% |

Use `/cohesion <key> <number>` for Humans and `/aliencohesion <key> <number>` for Aliens. Each faction exposes `MaximumRange = 5000m` (1..100000m), `DestinationRadius = 700m` (0..10000m), and these five Near/Far pairs:

| Key before `Near` / `Far` | Human Near | Human Far | Alien Near | Alien Far |
| --- | --- | --- | --- | --- |
| `Strength` | 1 | 0.2 | 1 | 0 |
| `HoldStrength` | 1 | 0 | 1 | 0 |
| `MinimumSpeed` | 0.05 | 1 | 0.15 | 1 |
| `ArrivalWindow` | 0.5s | 15s | 2s | 20s |
| `MaxDelay` | 20s | 2s | 30s | 3s |

Human numeric keys use `Cohesion<Key>` and Alien keys use `AlienCohesion<Key>`, for example `CohesionMaximumRange` and `AlienCohesionStrengthNear`. Existing faction switches and common safety CFG values are retained. The 12 common safety/debug keys remain configurable through cfg or the generic command but are hidden from the normal menu. `StartGrace` defaults to 5s for Humans and 3s for Aliens; `Retention` defaults to 500s for both. Three-stage keys such as `CloseRange` and `MediumStrengthFar` no longer affect behavior; they are not automatically converted into the new single-profile endpoints.

The original tuning commands also accept `status` for admins. Everyone can read all their values through `/formation status`.

Spacing/distance multiplier: `1 + percentage/15` through 30%, then `3 + (percentage-30)/10`. FPS distance uses `max((5m + player radius) * multiplier, player radius + largest follower radius + 1m)`. Thus 10% means approximately **1.667 times** the baseline, not ten percent of that baseline.

Settings affect subsequent placements. Existing rally tickets retain their captured scale. FPS settings are observed by the existing movement-update logic; no extra update loop or synchronization was added. Disabling the master releases formation ownership and air-height reservations, clears pending scouting, and hands pending production destinations back to native rally handling. Already issued destinations remain native orders.

## Preserved native behavior

Group cohesion uses bounded server updates with a default 0.35s interval per faction. Profiles are captured for each new command; general watchdog/debug controls are observed live. Settings share the existing category: human settings retain `GroupCohesion` and `Cohesion...` keys, while Alien settings use `AlienGroupCohesion` and `AlienCohesion...`. `/cohesion <key> <number>` edits humans and `/aliencohesion <key> <number>` edits Aliens, with independent validation and saving. No live CFG reload is added.

Arrival pacing and eligible formation holding weaken continuously with travel distance across the same profile; their strength controls are separate. Beyond `MaximumRange`, the command receives neither custom cap. Native slowdown remains suppressed for an enabled faction, including skipped or beyond-range selections, independent of MovementFix. Severe loss, spread, lag, stalls or exhausted delay budgets release pacing permanently to native travel. Targeted/attack/forced work and unsupported special movement receive no custom controller.

Only a complete, accepted **dragged formation** establishes a reusable intended layout. An ordinary click can translate and rotate that intact dragged shape toward travel anywhere within `MaximumRange`. Turns above five degrees receive a configurable hidden `TurnGrace` allowance (default 6s) with arrival pacing before holding a healthy shape. Holding requires positive `HoldStrength`, and can degrade to arrival pacing if formation health fails. Normal click grids, current physical arrangements, destination-local cohorts and rally parking never become remembered shapes. A drag explicitly defines a new shape and facing and uses arrival pacing while reforming.

Major turns weaken pacing for that whole command, on both new drags and remembered-layout clicks. Up to 15 degrees leaves the distance profile unchanged; at 90 degrees or more, holding is disabled, arrival strength is 15% of its original value, and the custom speed floor is at least 80% of the requested mode's uncapped speed. Native movement can still be slower. A later straight move captures the normal profile again; no new controls are added.

New moves seed provisional caps immediately, while failure accounting waits for a fresh usable path associated with the captured order and the startup grace (default 5s for Humans, 3s for Aliens). Fresh path distances rebase the initial ETA once; the deadline is not continually extended for a stuck rear member. An enrolled unit temporarily jumping, climbing or falling pauses its cap but keeps membership in the same captured order. Death, takeover and genuine replacement orders still release ownership.

For a focused trace: `/cohesiondebug next`, issue a move, then `/cohesiondebug groups` and `/cohesiondebug <id>`. Bounded traces distinguish native dispatch attempts/acceptance from cohesion enrollment, show target/order state, and separate requested caps from caps actually applied or skipped with reasons. Periodic global detail samples up to 32 members per group in rotation; explicitly watched groups can sample up to 256. A zero cohesion count alone does not establish that a native single-unit Move failed. `/cohesiondebug off` ends global and individual watches.

Death, player takeover, team/eligibility changes, subsets, added members or merged cached groups invalidate the **whole** affected intended shape, including its old heading and offsets. Survivors receive fresh normal placement on the next ordinary click; their current native orders remain untouched. Missing members do not leave remembered holes. A new complete drag can establish a new intended shape. Debug traces identify membership-triggered reformation.

Valid drag-derived layout memory is independent of speed coordination. Older move cleanup cannot erase a newer layout, natural completion does not immediately forget intended offsets, and any captured current/queued move keeps the intact layout retained while unfinished, even after pacing releases. Ordinary retention applies after those moves end. Repeated complete-group clicks therefore use the deliberately dragged shape rather than an unfinished physical arrangement.

For a dispersed or oversized selection, `DestinationRadius` (700m per faction by default) admits selected eligible ground units already within that horizontal distance of the clicked/drag centre. Up to `MaxUnits` nearest members receive local arrival pacing; distant units use independent rear destinations and never govern the cohort's pace or casualty count. This is a fixed issue-time cohort, not automatic late recruitment. Set the faction's radius to 0 to disable the fallback. Commands: `/cohesion DestinationRadius 700` and `/aliencohesion DestinationRadius 700`.

The **Travel cover preference disabler** defaults ON in Commander placement. It suppresses the soldier agent's automatic long-path cover waypoint substitution, retaining native Stealth area handling. Combat reactions, threat/cover choice and claims remain native. OFF restores native travel cover on newly calculated paths; changing the switch does not reissue existing orders. The Formation master switch still controls effective operation.

Without a valid reusable dragged shape, single-click ground placements use individually sized grid cells with a blend between each ground unit's radius and the selection's largest ground footprint. Reused drag shapes retain projection and physical-clearance checks. A short valid drag starts at a compact rectangle's minimum column footprint instead of squeezing units into single file; wider drags expand the frontage. Native ground priority bands, assignment order and facing are retained. Aircraft do not affect ground count, rows, footprint, spacing or cohesion membership; their separate centered rows begin over the rear half of the ground layout. Native navigation and flight height remain authoritative, including explicit grounded/stealth movement for flyer-capable Aliens. Commander previews remain vanilla and can differ from tuned destinations.

Rally parking preserves production dispatch and forced vehicle exits, reserves navigation-projected ground or vertically separated aircraft destinations before dispatch, and releases ownership on replacement orders, player control, team changes, destruction or despawn. Search remains bounded; native preferred flight height is restored when owned air reservations end.

FPS Follow/Attack retain the rearward moving two-column trail and stopped fan; explicit Move retains the destination grid. Native combat, navigation, explicit movement modes and external-order precedence remain authoritative. Ordinary follow repositioning requires displacement greater than FpsAllowance. Convoy entry requires more than twice that allowance from rest, with a 10m minimum; stopping for 0.75s restores the fan. Existing observation interval remains 0.25s. These are implementation behaviors, not new advanced controls.

## One-shot scouting

While in commander role, use `/scout` or `/scout on` to arm scouting. Select at least **five eligible friendly AI units**, then issue an immediate, non-queued ordinary Move on empty ground. That move consumes the armed action once: successful scouts are distributed among map sectors, while remaining normally movable selected units receive the original clicked Move.

At most **75 eligible units** receive scout assignments. Eligible Alien air units use their actual navigation graph and traversable tags in bounded destination/connectivity checks. Failed or excess scouts and other remaining normally movable selected units receive normal formation destinations around the original click; native acceptance can still fail and outcomes are reported. Protected or navigation-unready units remain unchanged. Player-controlled/driven/embarked units retain their control. With fewer than five eligible units, scouting is disarmed and the normal move is used. Successful placement is not guaranteed for every unit.

`/scout status` reports whether the action is armed; `/scout off` cancels it. Scouting requires commander role, not admin permission. Arming clears on role/team changes, master disable and round/scene cleanup. It issues one set of native Move orders, not a repeating patrol.

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
6. Enable faction cohesion and compare straight/90/180-degree turns, very short and near/beyond-maximum moves, fresh/queued paths and temporary traversal. Check requested versus applied caps alongside native dispatch results. Retain the drag-memory, whole-shape invalidation, narrow-rectangle and mixed air/ground checks in the detailed guide.
7. Scout with both factions, including Alien air units; verify bounded destinations and original-click fallback for failed/excess scouts.

The required **3.8.2 dedicated-server Release build passed on 12 November 2026 with 0 warnings and 0 errors**. The candidate is staged, not installed. No tests, live gameplay checks or game/server lifecycle actions were run. Compilation does not verify Harmony installation, native movement or multiplayer UI behavior; the checklist above is for user-run live verification. The overview PDF remains a historical 3.4.4 snapshot; the group cohesion quick guide covers 3.8.2.

User observations from 12 November on **3.8.0** described cohesion as subtle/natural, wanted short-move holding retained, noticed no added long-move delay and approved rectangle-start dragging. They also identified native-preview misalignment and a need for smoother presentation. These are qualitative observations, not measured tests or 3.8.2 validation. Preview/smoothing client work is deferred.
