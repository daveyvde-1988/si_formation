# Si_Formation 3.8.1 candidate changes

This candidate reduces pacing during major formation turns, protects new orders from stale navigation-failure state, and expands the diagnostics needed to separate native order dispatch from custom cohesion. Scouting also supports Alien air navigation and returns the remainder of a selection to its original clicked Move.

The single continuous distance profile, defaults and **12 visible numeric controls per faction** are retained. Human/Alien switches, cfg identities and the 42-row menu remain compatible. Existing generic `controlPresentation` metadata is preserved; no new ClientModUI or native-preview code is added.

## Turn-sensitive pacing

Both a new drag and a click reusing an intact dragged layout compare the requested facing with the preceding heading. The absolute yaw angle reduces the captured strengths for that whole command:

```text
r = clamp01((absYaw - 15 degrees) / 75 degrees)
arrivalStrength = distanceProfileStrength * (1 - 0.85 * r)
holdStrength = distanceProfileHoldStrength * (1 - r)
minimumSpeedFraction = max(distanceProfileMinimum, 0.8 * r)
```

Angles up to 15 degrees leave the profile unchanged. At 90 degrees or more, formation holding is disabled, arrival strength is 15% of its distance-profile value, and the custom speed floor is at least 80% of the requested mode's uncapped speed. Native acceleration, terrain, combat or obstruction can still make actual movement slower. This attenuation lasts for the command, not just its turning grace; a later straight command captures the ordinary profile again. No new settings are introduced.

## Startup and temporary movement states

Provisional pacing caps are seeded immediately using initial travel estimates. Failure accounting waits for a fresh usable path associated with the captured Move and the existing `StartGrace`, default 3 seconds, so stale native stuck/move-problem flags from a previous command do not immediately count against the new group. Actual cap application still respects path readiness, pauses and native movement limits.

Fresh path distances replace provisional timing through a one-time ETA rebase. The shared deadline is not repeatedly extended to wait for a blocked rear member. Existing delay budgets, lifetime bounds and ownership checks remain in effect.

An enrolled member temporarily jumping, climbing or falling pauses its custom cap while retaining membership in the same captured order. Death, player takeover, genuine replacement orders and other permanent eligibility changes still release ownership. Temporary traversal does not by itself create missing-member gaps or a new formation snapshot.

## More useful debug traces

The existing `/cohesiondebug next`, `on`, `off`, `groups` and group-ID commands remain. Traces distinguish native dispatch attempts/acceptance/rejection from cohesion enrollment, include target transformations and captured-order state, and show the requested custom cap separately from whether a cap was actually applied and why it was skipped.

Periodic per-unit samples are bounded: globally traced groups sample up to 32 members in rotation; explicitly watched groups can sample up to 256 members. Aggregate histories and configured sample cadence remain bounded too. A zero cohesion-member count alone is not evidence that a native single-unit Move failed.

## Scouting and the selection remainder

Scouting can use eligible Alien air units and validates destinations against each unit's actual native navigation graph and traversable tags, with bounded candidate/connectivity checks. The scouting limit remains **75** units; it is not a larger automatic scout group.

After scout assignment attempts, remaining normally movable selected units—including excess units and failed scout placements/orders—receive normal formation destinations around the original click. Protected or navigation-unready units remain unchanged. Native order acceptance can still fail; outcome counts are reported. The scout action remains one-shot and preserves player-control exclusions.

## Feedback and validation state

On 12 November, the user described **3.8.0** cohesion as subtle and natural, wanted to retain short-move holding, noticed no added delay on long moves, and approved the rectangle-start drag behavior. The same feedback identified a misaligned native preview and a need for smoother presentation. These are qualitative user observations, not measured benchmarks or validation of 3.8.1. Preview/smoothing client work is deferred.

The required 3.8.1 dedicated-server Release build passed on 12 November 2026 with **0 warnings and 0 errors**, in 3.06 seconds. The candidate is staged, not installed. No tests, live gameplay checks or game/server lifecycle actions were run. Compilation does not establish native movement, every locomotion case or multiplayer presentation. User-owned checks should compare straight and 90/180-degree moves, fresh and queued paths, temporary traversal, single-unit native dispatch, both factions' scouts including air units, and failed/excess scout fallback.
