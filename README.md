# Native formation tuning — Si_Formation 3.4.4

This is the native implementation for Silica game 1.0. The mod version is **3.4.4**, and this `si_formation` folder is its canonical source. The legacy implementation is preserved on [legacy/pre-native-1.0-20261111](https://github.com/daveyvde-1988/si_formation/tree/legacy/pre-native-1.0-20261111) and in a separate local archive. The native implementation does not include the old public FormationApi or custom blueprint system.

- [Purpose and functions overview (PDF)](docs/Formation_Overview.pdf)
- [Editable overview](docs/Formation_Overview.md)
- [Source migration and legacy archive](docs/MIGRATION.md)

## Menu and permissions

Regular players have one Formation Status button. `/formation` and `/formation status` invoke the same read-only report, which aggregates the existing per-setting status handlers and reports active features, spacing, rally reservations, FPS groups and commander scout state. Reading status neither saves configuration nor calculates or issues formations.

All eight tuning commands require AdminMod Generic permission to modify server settings. Admins may use them from either FPS or commander role. Existing slash syntax remains supported; the menu uses the registry's `!` spelling for admin commands. `/scout [on|off|status]` remains a commander action and is an alias of the shared player command callback, so it does not add a second Controls entry. It may still appear as an alias in the generic command reference.

The provider supplies three admin groups:

- **On/off switches:** master Enabled and MovementFix.
- **Server default settings:** FPS spacing, FPS distance, FPS allowance, commander click/drag spacing, rally spacing, large-unit influence and Alien spacing scale.
- **Advanced controls:** an explanation that this native implementation has no adjustable advanced settings. FormationTravelDistance, StartDelaySpeedLock, prediction/cache/update controls belong to other implementations or do not exist here; none are introduced.

Compatible structured ServerModUI/ClientModUI versions can render the supplied groups as collapsible sections and use their slider ranges/descriptions. Older bridges that discard per-command `ui` hints can show ordinary admin value/choice controls without accordions or typed sliders. Rendering depends on the compatible UI bridge; this implementation does not supply a client mod or an additional UI polling loop.

## Server configuration

Configuration is saved by MelonPreferences in `UserData/cfg/Formation/si_formation_native.cfg`, category `[Si_Formation_Native]`. Admin changes save immediately. Existing files are loaded; the old `si_formation.cfg` and blueprint catalogs are untouched. Invalid numeric values use the corresponding built-in default. Edit the file while the server is stopped.

This version uses one shared server configuration and does not opt into ClientModUI's personal preference restoration. Existing client preference files are neither read nor migrated, and regular-player replay attempts cannot change server settings. Pending scout orders remain transient command state, not personal configuration.

| Setting | Command | Default | Valid values |
| --- | --- | --- | --- |
| Enabled | `/formation enabled on\|off` | true | on/off |
| MovementFix | `/movementfix on\|off\|status` | true | on/off |
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

Single-click placements use individually sized grid cells with a blend between each unit's radius and the selection's largest radius. Dragged placements retain native priority bands, facing, unit order and drag width. Physical footprint clearance remains a lower bound. Alien scale changes spacing without changing unit definitions, radii or bounds. Commander previews remain vanilla and can differ from tuned destinations.

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

1. A regular player sees Formation Status, can read current values, and cannot change tuning through slash commands or old remembered client controls. An admin with Generic permission can change all eight settings from either role.
2. After an admin change, `/formation status` reports it immediately without moving units. Restart persistence restores saved values; old personal values are not replayed. A fresh configuration defaults FPS distance to 10%.
3. Compare FPS spacing/distance/allowance, commander click/drag spacing, mixed-size influence, Alien scaling and rally parking with the reported current values.
4. Toggle Enabled off/on and MovementFix off/on; test pending production, aircraft height restoration, subsequent native orders and `/scout` arming/cancellation.
5. Verify grouped sliders on a compatible structured UI bridge. On older bridges without presentation hints, expect ordinary controls.

A successful build does not verify Harmony installation, native movement or multiplayer UI behavior. This documentation cleanup did not run a build, gameplay checks or a game/server restart; the checklist above is for user-run live verification.
