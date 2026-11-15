# Si_Formation 3.8.0 candidate changes

This candidate replaces the three range stages with one continuous distance profile, restricts remembered shapes to deliberate intact drag formations, and makes narrow drags start as a rectangle instead of a deep column. See [Group cohesion](Group_Cohesion.md) for current controls and behavior. Earlier release notes remain historical snapshots.

## Continuous distance tuning

Human and Alien cohesion retain independent switches, both OFF by default when absent. Each faction exposes twelve numeric controls:

| Control | Default |
| --- | --- |
| `MaximumRange` | 5000m |
| `DestinationRadius` | 500m |
| `StrengthNear` / `StrengthFar` | 1 / 0.05 |
| `HoldStrengthNear` / `HoldStrengthFar` | 1 / 0 |
| `MinimumSpeedNear` / `MinimumSpeedFar` | 0.1 / 0.95 |
| `ArrivalWindowNear` / `ArrivalWindowFar` | 0 / 15s |
| `MaxDelayNear` / `MaxDelayFar` | 30 / 0.5s |

Values interpolate linearly from zero travel distance to MaximumRange. Exact MaximumRange still uses the Far values. Longer commands receive no custom pacing or holding, while native group slowdown stays suppressed when the faction's cohesion is enabled. Explicit movement modes and physical obstacles remain native.

Initial direct-distance estimates start coordination without an artificial three-second wait on very short moves. Physical caps still require usable native paths; navigation, obstruction and replan safeguards remain active.

Existing faction switches and common safety/diagnostic cfg keys are retained. The old Close/Medium/Long boundaries and stage endpoints are ignored, not migrated. New keys use the existing `Cohesion` / `AlienCohesion` prefixes, for example `CohesionStrengthNear`. Hidden common watchdog keys remain accessible through cfg/tuning commands without restoring the large stage menus.

## Deliberate formation memory

Only a successfully issued, complete drag-derived ground layout can become remembered formation intention. Ordinary clicks use fresh grids; rally parking does not create remembered identity. Neither captures accidental gaps. A whole intact dragged group can reuse its intended slots on subsequent clicks, rotating toward travel while holding strength fades with distance.

The drag command itself reforms with arrival pacing; holding is considered on later whole-group clicks. Membership changes, casualties, player control or invalid members invalidate the whole remembered shape. Unfinished valid drag intention persists while captured moves remain current/queued, then uses normal post-move retention. Older command cleanup cannot delete a newer layout.

## Drag width and retained features

Valid narrow drags clamp to a rectangle-sized minimum using the click grid's columns and physical packing widths. Wider drags fit fewer rows, then expand lateral spacing. Native priority bands retain at least one row each; different bands are not flattened into a single mixed row. Midpoint, facing, unit order, physical bounds, projection and aircraft separation remain in control. Tiny rejected drags use ordinary grid fallback and create no remembered drag identity. The native preview can differ from server destinations.

Destination-radius fallback, separate aircraft placement and the default-ON travel cover preference disabler continue. Destination-local cohorts still use fresh arrival layouts, with distant selected ground units independent in a rear band. No ClientModUI implementation change, new protocol or legacy FormationApi is introduced.

## Validation and deployment

The required dedicated-server Release build passed on **12 November 2026** with **zero warnings and zero errors**, using `dotnet build Si_Formation.csproj -c Release --ignore-failed-sources`. The initial build took 9.77 seconds; a final incremental build after preserving a concurrent menu-presentation edit also passed, in 0.86 seconds. Output remains `bin/Release/netstandard2.1/Si_Formation.dll`. This is a staged build, not an installation record. No tests, installation or server start/stop actions were performed. User-owned live verification should cover short/long/beyond-range moves, drag-width transitions, intact versus changed membership, local-destination fallback, aircraft, queued activation and native obstacles.
