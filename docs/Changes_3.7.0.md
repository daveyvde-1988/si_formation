# Si_Formation 3.7.0 candidate changes

This candidate adds a separate infantry travel-cover switch, preserves intended layouts across native order transitions, and coordinates a nearby destination cohort when a large or dispersed ground selection cannot move as one group. Existing Human/Alien profile controls, Close turns, partial-group reformation and separate aircraft placement remain in place.

## Travel cover preference disabler

The new **Travel cover preference disabler** is **ON by default**. ON disables the infantry long-path step that replaces ordinary waypoints with nearby cover points; OFF restores that native preference. The switch is independent of Human/Alien group cohesion and appears under **Commander placement**.

| Command | Result |
| --- | --- |
| `/travelcoverdisabler on` | Disable infantry travel-cover waypoint preference and save the switch |
| `/travelcoverdisabler off` | Restore native infantry travel-cover waypoint preference and save the switch |
| `/travelcoverdisabler status` | Show its current state and the ON/OFF meaning |

Changes require AdminMod Generic permission. The existing public Formation Status report also reads this state without changing it. The cfg key is `TravelCoverPreferenceDisabler = true` in the existing `UserData/cfg/Formation/si_formation_native.cfg`, category `[Si_Formation_Native]`.

The hook affects only `AISoldierAgent.ProcessLongRangePath`. It preserves the native movement-area statement, including the smaller Stealth completion area, and skips that method's cover-waypoint substitution/pruning while enabled on the server. OFF, master disable or non-server execution leaves the original method in control. The hook adds no Harmony `__state` pair or order mutation.

Combat reactions, combat state, threat decisions, cover search/claims, `TakeCover` and `BTTakeCover` are not patched. Infantry can still choose and occupy cover when native combat behavior calls for it. The same long-path postprocessor can also receive a route to a cover point already chosen by combat: ON leaves that choice intact while skipping an additional round of travel-cover substitution. This is a route-postprocessing change, not a promise that combat travel paths remain byte-for-byte identical.

The switch applies to newly calculated or recalculated long paths. Toggling it does not immediately reissue orders or rebuild an existing path. A new Move order provides a direct comparison. The native travel substitution is infantry-specific; this switch does not add a cover-route preference to aliens or vehicles and does not change their graph costs, masks or movement definitions.

## Intended layout retention

Speed coordination ownership and remembered formation intention now have separate lifetimes. A released controller can stop applying speed caps while its intended layout remains available for a subsequent click.

Cleanup observes the layout that existed before an order transition and may remove only that same snapshot. A late cleanup from an older command cannot erase a newer command's layout. Native AI replans do not themselves discard intended layout identity. Natural movement completion and loss of a captured order no longer delete the cache immediately.

A valid snapshot refreshes its age while any captured move is still current or queued, even if speed coordination has already released. The faction's ordinary `Retention` period applies after its pending moves end. Explicit replacement, changed membership, faction/master cleanup, invalidation and ordinary expiry remain meaningful boundaries.

Partial-group reformation is retained: selecting a subset, merging groups or otherwise changing the remembered ground membership invalidates the touched old identity. The new command uses a fresh formation with arrival pacing instead of locking old gaps into place. Unselected survivors keep their current native orders, and their next command reforms rather than restoring the invalidated shape. A complete group with valid remembered identity can still reuse its intended layout and heading.

## Destination arrival radius

`DestinationRadius` provides a bounded fallback for ground selections that are too dispersed or exceed the faction's `MaxUnits`. Compact selections continue through the ordinary cohesion admission rules.

| Setting | Default | Valid range | Menu step |
| --- | --- | --- | --- |
| Human `DestinationRadius` | 500m | 0–10000m | 25m |
| Alien `DestinationRadius` | 500m | 0–10000m | 25m |

The values are independent per faction:

```text
/cohesion DestinationRadius 500
/aliencohesion DestinationRadius 500
```

Cfg keys are `CohesionDestinationRadius` and `AlienCohesionDestinationRadius`. Setting either to **0** disables that faction's fallback and retains the earlier behavior of skipping custom coordination for a dispersed/oversized selection. The slider step is presentation metadata; numeric command validation uses the stated range.

When the fallback is needed, the controller examines **only already-selected friendly ground units** at the moment the order is issued. It admits eligible members within the horizontal radius of the clicked destination or drag midpoint. If too many qualify, the nearest members are taken up to `MaxUnits`. At least two valid accepted moves are required. Membership is fixed for this command: units arriving inside the radius later do not join, and unselected units are never recruited. Queued segments also capture this membership when issued rather than waiting to gather units at activation.

The local cohort receives its own destination layout and **arrival pacing**, including when its distance falls in Close range. It does not use formation holding or wait for distant selected units to arrive. Its normal faction profile, delay budgets, health checks, order ownership and release rules still apply.

Farther selected ground units receive independent destinations in a separate rear band behind the local formation. They are not part of the local cohort's speed or failure denominator. Aircraft remain separately placed over the local formation's rear, rather than being displaced behind the distant arrivals. Native navigation, preferred flight altitude and explicit movement modes remain in control.

This fallback changes how selected units are assigned destinations; it does not continuously gather a map-wide army into a persistent group. Unsupported ground special movement remains conservative, and insufficient valid local slots cause custom coordination to decline safely.

## Configuration and integration

Existing configuration values are retained. The new travel switch and the two faction-specific destination-radius controls use their defaults when absent; ordinary Human/Alien profile values are not reset or copied between factions. The Formation runtime identity, cfg filename/category and schema-2 provider contracts remain unchanged.

The existing menu bridge carries the added checkbox and sliders. No ClientModUI implementation change, new client extension, wire protocol, legacy FormationApi or optimizer bridge is introduced.

## Validation and deployment state

The source is prepared for the required dedicated-server Release build:

```powershell
dotnet build Si_Formation.csproj -c Release --ignore-failed-sources
```

The 3.7.0 Release build succeeded on 11 November 2026 with **zero warnings and zero errors**. Output remains `bin/Release/netstandard2.1/Si_Formation.dll`. Source review confirms the travel hook's narrow method scope and preserved Stealth statement; it does not verify Harmony execution, native gameplay or multiplayer presentation. The DLL is staged only; the installed DLL was left unchanged.

This documentation update runs no tests, build, installation or server start/stop action and does not edit the live cfg. User-owned live checks should compare travel-cover ON/OFF with fresh Normal/Stealth orders, verify native combat cover decisions, repeat complete/partial-group clicks across natural completion and queued transitions, and compare destination-radius fallback with its value set to zero. Include mixed ground/air selections and local members near obstacles. Gameplay behavior and interaction with other native movement patches remain pending live verification.
