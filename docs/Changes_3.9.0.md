# Si_Formation 3.9.0 candidate changes

This candidate retains dragged formations through limited casualties, gives native Reverse a strict translated snapshot, adds optional perimeter defense, and leaves harvesters/Shrimp entirely on native rally handling. Existing tuned defaults, configuration identity and public provider contracts are retained. No client mod or native-preview change is included.

## Remembered layouts after casualties

Destroyed or despawned members may leave gaps while losses are **at most 40% of the original dragged layout**. Exactly 40% retains the shape. Repeated survivor moves keep that original denominator; they do not renew the casualty allowance. A new complete drag creates a new baseline.

A living member omitted from the selection is a selection change, not a casualty. Partial selections, additions, merges, player control, live team/eligibility changes or more than 40% casualties reset the whole affected remembered shape. Existing native orders remain untouched; the next ordinary click uses fresh placement. This memory rule is separate from active pacing's health and slowdown limits.

## Native Reverse

Both the formation-move route and direct command-card/network route capture native Reverse orders. With faction cohesion enabled, at least two eligible reverse-capable ground vehicles receive destinations translated from their current world-space offsets. Queued Reverse uses predecessor Move/Reverse destinations. Slot offsets are not turned toward the click.

Reverse uses full-strength snapshot pacing rather than ordinary distance/turn relaxation. It can request a pause for a leading member, with a 120-second per-member slowdown allowance and existing maximum lifetime/release checks. The longitudinal holding tolerance is at least 0.5m or 10% of unit radius. Its uncapped estimate uses `Vehicle.ReverseNormalSpeed`, even when Fast was requested; no ReverseFastSpeed API exists.

A fresh Reverse snapshot does not establish ordinary reusable drag memory by itself. If the whole selected group already has valid dragged-layout identity, Reverse retains that lineage and original casualty baseline; a later ordinary click can reuse its translated snapshot with normal rotation. Distorted projection, lost clearance, unsupported movement, excessive membership or fewer than two eligible units uses native Reverse without custom snapshot pacing. Partial native acceptance releases strict pacing for the entire snapshot and reports the accepted count while leaving accepted native orders intact.

Fixed destination offsets do not lock body yaw or rigid geometry. Native reverse steering, obstacle navigation, collision avoidance and engagement/path holds remain authoritative. Turret aiming alone does not remove Reverse pacing, but native movement holds still win. No transform rotation, teleport or repeated Reverse order is used to maintain the snapshot.

## Optional advanced defense

`AdvancedDefenseMove` is a new shared saved switch, **OFF by default**. AdminMod Generic permission is required for `/advanceddefense on|off`; `status` reads it. The menu includes this switch and a commander ground-perimeter action, bringing the schema-2 export to 44 rows.

Friendly-object clicks are customized only for members whose actual native action resolves to Defend around a stationary target. Repair, Harvest and other friendly actions keep their native semantics. Explicit command-card Defend also works. Accepted Defend orders keep the actual target, queued activation and native combat behavior. Moving targets use native following; if a stationary target later moves or shifts over 1m from the planned anchor, custom positioning permanently releases to native following.

The initial planner is bounded and conservative:

- It accepts 2–128 eligible friendly ground AI units. Every admitted member needs positive current vision, AI engagement, targeting and damaging-weapon information; unsupported data falls back to native placement.
- Native armor/health ratings primarily rank a compact core, with current health as a tie-breaker. The core uses the top quarter of members, at least two and at most eight. Outer positions support that core through overlapping vision; a melee core's short weapon range does not force ranged supporters into melee distance. This is a heuristic, not an enemy combat or damage simulation.
- Physical separation includes unit radii plus 0.75m. Friendly-target footprint clearance is retained. Beyond that physical inner radius, spread is at most 80% of the smaller of the unit's own usable coverage and the core's current vision. Own coverage is the minimum of current vision, AI engagement and targeting distance. Native Defend also has an absolute 125m placement limit within its 150m combat leash. Empty ground has no fixed radius cap beyond the measured range constraint.
- Every strong-core pair must be within 45% of the smaller member's vision and have clear static inter-unit sight. Each outer member must be close enough to at least one core member: separation cannot exceed 90% of the smaller of their vision radii. Outer members sample their nearest one or two supporting core links; ray-budget exhaustion can leave outer links unmeasured. Two outward rays only guide the first core position. These checks do not prove every firing line, cover quality or turret arc.
- A plan permits at most 768 terrain/navigation projection attempts and 128 visibility rays, with a soft 20ms guard checked between attempts. An individual navigation call can extend elapsed time beyond that guard. An incomplete placement or exhausted time/projection budget uses native placement. There is no continuous visibility scan or full perimeter replanning.

Persistent friendly-object positioning uses the existing native logic cadence and changes only its noncombat fixed-ring positioning. Native aiming/combat remains active and may move defenders from their slots. It retains the validated stationary anchor rather than translating unverified terrain slots. Paused navigation, or stuck/move-problem state after a five-second active grace, releases custom positioning. Ownership pairs each accepted order reference with its ID, follows queued activation, and releases on replacement, death, control/team changes or forced vehicle movement. Cleanup uses a one-second cadence and at most 1024 retained order records. Toggle/master/round/scene cleanup clears custom state. Debug output reports actual projection/ray counts.

## Empty-ground perimeter action

While Advanced defense is ON, a commander can use `/perimeterdefense [on|off|status]` to arm the next non-queued ordinary click Move on empty ground. Queued moves, drags, attacks and forced orders do not consume it. The same planner supplies native Move destinations; failure keeps the ordinary move.

The ground action provides placement followed by native combat. The game exposes no Hold Position order, and native Defend requires a real target. No synthetic target, friendly-target leash or repeating ground-order issuer is created. Units can leave their slots under native behavior.

Arming a ground perimeter cancels scouting; arming scouting cancels the perimeter. Arms are transient commander/team state and clear on role/team changes, disconnect, round/scene cleanup, master/toggle disable or unload. They are not personal or saved configuration.

## Harvester rally exclusions

Human harvesters and Alien Shrimp are excluded using native `UnitType.Harvester`, with human/Alien harvester agents as initialization fallbacks. They receive no custom production ticket, reservation, suppressed native rally or reissued rally move, including the master-disable handoff. Their native production, forced exit, rally and resource-work pipeline remains in control.

## Build and live checks

The required 3.9.0 dedicated-server Release build passed on 13 November 2026 with **0 warnings and 0 errors**, in 6.38 seconds. The candidate is staged, not installed. No tests were added or run, and no live gameplay or game/server lifecycle actions were performed. The 3.8.2 quick-guide PDF and editable quick guide remain historical snapshots; current behavior is documented here and in [Group cohesion](Group_Cohesion.md).

User-owned checks should cover casualty boundaries and repeated survivor moves; split/add/control resets; both Reverse routes, queued snapshots, mixed speeds, turret combat and obstacles; advanced defense OFF/ON, moving friendly targets, range-mixed/blocked formations, ground arms and scout cancellation; and native harvester/Shrimp production and resource work. Compilation alone cannot verify Harmony hook installation, native movement, multiplayer presentation or interactions with other movement mods.
