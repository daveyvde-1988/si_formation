# Si_Formation 3.8.2 — tuned defaults

Version 3.8.2 makes the current tuning the built-in defaults for new or missing settings. Both Human and Alien cohesion default ON. The two factions retain independent settings, and saved configuration values take precedence. This patch changes defaults and their menu metadata; placement, movement, cohesion algorithms and command/API contracts are unchanged.

Each faction defaults to `MaximumRange = 5000m` and `DestinationRadius = 700m`.

| Profile key | Human Near | Human Far | Alien Near | Alien Far |
| --- | --- | --- | --- | --- |
| `Strength` | 1 | 0.2 | 1 | 0 |
| `HoldStrength` | 1 | 0 | 1 | 0 |
| `MinimumSpeed` | 0.05 | 1 | 0.15 | 1 |
| `ArrivalWindow` | 0.5s | 15s | 2s | 20s |
| `MaxDelay` | 20s | 2s | 30s | 3s |

Hidden `StartGrace` defaults to 5s for Humans and 3s for Aliens. `Retention` defaults to 500s for both. Other safety/debug defaults remain unchanged; see the [complete settings guide](Group_Cohesion.md).

The existing ordinary defaults remain: Enabled, MovementFix and TravelCoverPreferenceDisabler ON; Spacing 32%, Rally 25%, SizeInfluence 30%, AlienSpread 50%, FpsSpacing 32%, FpsDistance 10% and FpsAllowance 20m. Old Close/Medium/Long keys remain ignored.

The configuration path and category remain `UserData/cfg/Formation/si_formation_native.cfg` and `[Si_Formation_Native]`. Existing configuration is preserved; the patch does not overwrite saved values or add a live reload. Missing values, nonfinite-value fallbacks and invalid endpoint-pair fallbacks use the updated faction defaults.

The dedicated-server Release build passed on 12 November 2026 with 0 warnings and 0 errors in 3.47 seconds. Output remains `bin/Release/netstandard2.1/Si_Formation.dll`. The candidate is staged, not installed. No tests, live gameplay checks or game/server lifecycle actions were run.
