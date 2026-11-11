# Native formation tuning — Si_Formation 3.7.0

This is the native implementation for Silica game 1.0. The mod version is **3.7.0**, and this `si_formation` folder is its canonical source. The legacy implementation is preserved on [legacy/pre-native-1.0-20261111](https://github.com/daveyvde-1988/si_formation/tree/legacy/pre-native-1.0-20261111) and in a separate local archive. The native implementation does not include the old public FormationApi or custom blueprint system.

- [Group cohesion settings, behavior and debug guide](docs/Group_Cohesion.md)
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

The provider supplies contiguous, collapsible groups through the existing menu bridge:

- **General / Commander placement / FPS squads / Rally:** master control, ordinary placement/movement tuning, follower controls and production parking.
- **Human cohesion – General / Close / Medium / Long:** independent enable switch and 72 numeric values, including turning allowance and destination arrival radius.
- **Alien cohesion – General / Close / Medium / Long:** an independent switch and 72 separate numeric values. Alien placement spacing is also located here, with its effect independent of the cohesion switch.
- **Diagnostics:** global and per-group debug actions shared by both factions. No ClientModUI source or DLL is changed. Labels retain faction/range names for simpler bridges that discard grouping metadata.

Both faction switches default OFF. Existing human values retain their original CFG keys. Close is below 500m, medium is 500–1000m inclusive, and long is above 1000m; both factions have independently configurable boundaries.

Compatible structured ServerModUI/ClientModUI versions can render the supplied groups as collapsible sections and use their slider ranges/descriptions. Older bridges that discard per-command `ui` hints can show ordinary admin value/choice controls without accordions or typed sliders. Rendering depends on the compatible UI bridge; this implementation does not supply a client mod or an additional UI polling loop.

## Server configuration

Configuration is saved by MelonPreferences in `UserData/cfg/Formation/si_formation_native.cfg`, category `[Si_Formation_Native]`. Admin changes save immediately. Existing files are loaded; the old `si_formation.cfg` and blueprint catalogs are untouched. Invalid numeric values use the corresponding built-in default. Edit the file while the server is stopped.

This version uses one shared server configuration and does not opt into ClientModUI's personal preference restoration. Existing client preference files are neither read nor migrated, and regular-player replay attempts cannot change server settings. Pending scout orders remain transient command state, not personal configuration.

| Setting | Command | Default | Valid values |
| --- | --- | --- | --- |
| Enabled | `/formation enabled on\|off` | true | on/off |
| MovementFix | `/movementfix on\|off\|status` | true | on/off |
| GroupCohesion | `/groupcohesion on\|off\|status` | false | on/off |
| AlienGroupCohesion | `/aliengroupcohesion on\|off\|status` | false | on/off |
| TravelCoverPreferenceDisabler | `/travelcoverdisabler on\|off\|status` | true | ON disables infantry travel-cover substitution; combat decisions remain native |
| FpsSpacing | `/fpsspace <value>` | 32% | 0..100% |
| FpsDistance | `/fpsdistance <value>` | **10%** | 0..100% |
| FpsAllowance | `/fpsallowance <value>` | 20m | 1..100m |
| Spacing | `/formationspace <value>` | 32% | 0..100% |
| Rally | `/rallyspace <value>` | 25% | 0..100% |
| SizeInfluence | `/sizeinfluence <value>` | 30% | 0..100% |
| AlienSpread | `/alienspread <value>` | 50% | 0..100% |

The original tuning commands also accept `status` for admins. Everyone can read all their values through `/formation status`.

Spacing/distance multiplier: `1 + percentage/15` through 30%, then `3 + (percentage-30)/10`. FPS distance uses `max((5m + player radius) * multiplier, player radius + largest follower radius + 1m)`. Thus 10% means approximately **1.667 times** the baseline, not ten percent of that baseline.

Settings affect subsequent placements. Existing rally tickets retain their captured scale. FPS settings are observed by the existing movement-update logic; no extra update loop or synchronization was added. Disabling the master releases formation ownership and air-height reservations, clears pending scouting, and hands pending production destinations back to native rally handling. Already issued destinations remain native orders.

## Preserved native behavior

Group cohesion uses bounded server updates with a default 0.35s interval per faction. Profiles are captured for each new command; general watchdog/debug controls are observed live. Settings share the existing category: human settings retain `GroupCohesion` and `Cohesion...` keys, while Alien settings use `AlienGroupCohesion` and `AlienCohesion...`. `/cohesion <key> <number>` edits humans and `/aliencohesion <key> <number>` edits Aliens, with independent validation and saving. No live CFG reload is added.

When enabled for its faction, ordinary commander ground moves preserve unit-to-slot identity. Close single clicks rotate that layout toward travel; turns above five degrees get a configurable `TurnGrace` allowance (default 6s) with arrival pacing before holding a healthy shape. A failed turn continues with arrival pacing. Holding later degrades if formation health fails. Medium and long clicks retain the remembered layout orientation and use arrival coordination only; dragging explicitly defines new facing. Severe loss, spread, lag, stalls or exhausted delay budgets release the command permanently to native travel. Native slowdown remains suppressed for an enabled faction, including skipped selections, independent of MovementFix. Targeted/attack/forced work and unsupported special movement receive no custom controller.

For a focused trace: `/cohesiondebug next`, issue a move, then `/cohesiondebug groups` and `/cohesiondebug <id>`. Logs include group ID, elapsed time, state transitions, samples and release reasons. `/cohesiondebug off` ends global and individual watches.

Moving only part of a cached ground formation discards the old cached offsets and uses fresh standard click/drag placement, including configured spacing and separate aircraft positions. Cohesion coordinates the new group's arrival without holding the old shape. The unselected remainder is marked for fresh placement on its next move too; its current native orders remain untouched. Full unchanged ground membership still preserves its layout. Adding units or merging cached groups also reforms. Debug traces identify membership-triggered reformation.

Intended layout memory is independent of speed coordination. Older move cleanup cannot erase a newer layout, natural completion does not immediately forget intended offsets, and a captured current/queued move keeps the layout retained while unfinished. Ordinary retention applies after those moves end. Repeated complete-group clicks therefore use intended slots rather than an unfinished physical arrangement.

For a dispersed or oversized selection, `DestinationRadius` (500m per faction by default) admits selected eligible ground units already within that horizontal distance of the clicked/drag centre. Up to `MaxUnits` nearest members receive local arrival pacing; distant units use independent rear destinations and never govern the cohort's pace or casualty count. This is a fixed issue-time cohort, not automatic late recruitment. Set the faction's radius to 0 to disable the fallback. Commands: `/cohesion DestinationRadius 500` and `/aliencohesion DestinationRadius 500`.

The **Travel cover preference disabler** defaults ON in Commander placement. It suppresses the soldier agent's automatic long-path cover waypoint substitution, retaining native Stealth area handling. Combat reactions, threat/cover choice and claims remain native. OFF restores native travel cover on newly calculated paths; changing the switch does not reissue existing orders. The Formation master switch still controls effective operation.

With faction cohesion disabled or skipped, single-click ground placements use individually sized grid cells with a blend between each ground unit's radius and the selection's largest ground footprint. Managed single clicks transform the saved intended shape or a compact current snapshot, with projection and physical-clearance checks. Dragged placements retain native ground priority bands, facing, unit order and drag width. Aircraft do not affect ground count, rows, footprint, spacing or cohesion membership; their separate centered rows begin over the rear half of the ground layout. Native navigation and flight height remain authoritative, including explicit grounded/stealth movement for flyer-capable Aliens. Commander previews remain vanilla and can differ from tuned destinations.

Rally parking preserves production dispatch and forced vehicle exits, reserves navigation-projected ground or vertically separated aircraft destinations before dispatch, and releases ownership on replacement orders, player control, team changes, destruction or despawn. Search remains bounded; native preferred flight height is restored when owned air reservations end.

FPS Follow/Attack retain the rearward moving two-column trail and stopped fan; explicit Move retains the destination grid. Native combat, navigation, explicit movement modes and external-order precedence remain authoritative. Ordinary follow repositioning requires displacement greater than FpsAllowance. Convoy entry requires more than twice that allowance from rest, with a 10m minimum; stopping for 0.75s restores the fan. Existing observation interval remains 0.25s. These are implementation behaviors, not new advanced controls.

## One-shot scouting

While in commander role, use `/scout` or `/scout on` to arm scouting. Select at least **five eligible friendly AI units**, then issue an immediate, non-queued normal Move on empty ground. That move consumes the armed action: its clicked position is ignored and the selected units are distributed among map sectors, subject to terrain, navigation and destination-separation checks.

At most **75 eligible units** are dispatched; excess and excluded units are left untouched. Player-controlled/driven/embarked units and units without suitable movement/navigation are excluded. With fewer than five eligible units, scouting is disarmed and the normal move is used. Navigation or destination/order failures are reported; successful placement is not guaranteed for every unit.

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
6. Enable faction cohesion and compare close turns, medium/long and dispersed selections; verify Turning/hold/arrival/ASAP transitions, debug reasons, repeated clicks, drag reforms, queued segment activation, independent Alien values and mixed air/ground placement. See the detailed guide.

A successful build does not verify Harmony installation, native movement or multiplayer UI behavior. The checklist above is for user-run live verification. Version 3.5.1 startup was subsequently observed in the user's server log; that does not establish 3.6.0 gameplay behavior. No gameplay check or game/server start/stop accompanies this update.
