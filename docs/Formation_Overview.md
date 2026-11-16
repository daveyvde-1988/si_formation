# Feature legend
Si_Formation — detailed overview

Version 3.16.2 | 16 November 2026

This manual explains why each feature exists, how it behaves and which controls tune it in ClientModUI. Page numbers refer to the accompanying PDF. Defaults are the 66 saved server settings captured on 16 November 2026.

| Feature | Page |
| --- | --- |
| Control panel, permissions, saved settings and master switch | [2](#using-the-control-panel) |
| Regular single-click placement and large-unit influence | [3](#single-click-placement) |
| Click-and-drag placement, frontage and formation memory | [4](#click-and-drag-placement) |
| Aircraft placement and arrival | [5](#aircraft-and-reverse) |
| Reverse movement and infantry fallback | [5](#aircraft-and-reverse) |
| Suppress native group slowdown | [6](#native-group-slowdown-and-travel-cover) |
| Travel cover preference disabler | [6](#native-group-slowdown-and-travel-cover) |
| Shift movement cohesion and the slowest speed tier | [7](#how-shift-cohesion-works) |
| Human and Alien cohesion parameters | [8–9](#cohesion-distance-and-arrival-controls) |
| Nearby destination cohesion | [10](#nearby-destination-cohesion) |
| Waypoint regrouping, shared ETA and overdue waits | [11](#waypoint-regrouping) |
| Alien combat return and combat wait | [12](#alien-combat-return-and-straggler-cleanup) |
| Five-minute distant-straggler cleanup | [12](#alien-combat-return-and-straggler-cleanup) |
| Production rally parking and clear factory exits | [13](#production-rally-parking) |
| FPS squad Follow, Move and Attack | [14](#fps-squads) |
| Pathfinding shortcut pass | [15](#pathfinding-shortcuts-and-scouting) |
| Scout command | [15](#pathfinding-shortcuts-and-scouting) |
| Diagnostics and focused tracing | [16](#diagnostics) |
| Available performance information | [17](#performance-information) |
| Compact chat-command reference | [18](#chat-command-reference) |

**Reading the settings.** m means metres and s means seconds. Near and Far are the two endpoints of one travel-distance profile. Spacing percentages are tuning inputs whose conversion is explained on page 3. Menu labels and locations are written as they appear in the current panel.

<!-- pagebreak -->

# Using the control panel

## Shared server controls

Open Formation in ClientModUI. Formation Status is a public read-only report of settings and activity. About cohesion controls provides usage help. Tuning and diagnostics require AdminMod Generic permission from either role. Watch next cohesion group also requires commander role. Scout is a commander action.

Settings belong to the server and save immediately after an accepted change. They are shared by all players. Valid existing saved settings take precedence over defaults. The gameplay runs on the server, while ClientModUI provides the tuning interface. The saved file is `UserData/cfg/Formation/si_formation_native.cfg`.

## Finding the controls

| Panel group | Contents |
| --- | --- |
| General | Formation Status, Formation mod, Pathfinding shortcut pass, About cohesion controls, Suppress native group slowdown, Travel cover preference disabler, Nearby destination cohesion, Human group cohesion and Alien group cohesion. |
| Commander placement | Separate single-click and drag spacing, Large-unit influence and the three checkpoint-wait sliders. |
| Human cohesion | Twelve numeric distance-profile controls. |
| Alien cohesion | The same twelve controls, plus Alien: Placement spacing scale. |
| FPS squads / Rally | Three FPS controls and one production-parking spacing control. |
| Diagnostics | Debug controls and the generic cohesion/waypoint command forms. |

## Master switch and eligibility

**General → Formation mod** defaults ON. Turning it OFF releases custom speed caps, waypoint waits, FPS layout ownership, rally reservations and pending scout arming. Pending production is handed back to native rally handling. Issued native destinations can continue. The shortcut limit returns to the native 200m value.

Placement normally requires living, movable, friendly AI units with usable order and navigation components. Player-controlled, player-driven and embarked units retain their own control. Fresh accepted replacement orders take ownership from a previous managed task. Invalid projected slots may be skipped, retried or left to native handling according to the feature.

## When a changed value takes effect

| Setting family | Application |
| --- | --- |
| Placement spacing and size influence | New commander placements; FPS layouts observe relevant changes on their update path. |
| Human / Alien profile endpoints | Captured when a new custom move is dispatched. |
| Checkpoint and combat waits | Captured when each waypoint activates. |
| Rally spacing | Captured when a new production parking ticket is created. |
| Shortcut pass / travel cover | Subsequent native path processing / long-range path calculation. |
| Debug switches and watches | Runtime state; these are not saved gameplay defaults. |

<!-- pagebreak -->

# Single-click placement

## Why it exists

Compact native destination formations can expose many units to one area attack, make dispersal awkward and create congestion when a mixed-size army relocates. Allowing the largest unit to dictate every smaller unit's gap can produce the opposite problem: excessive spread across the whole selection.

## How placement works

A regular click builds a footprint-aware destination grid. Each ground unit receives its own slot around the ordered area, facing along the movement direction. The formation is generally wider than it is deep. Physical size, row packing, terrain and navigation affect the exact result. Aircraft use a separate layout, described on page 5.

Queued placements derive their next movement direction from the preceding Move or Reverse position, so an L-shaped waypoint route turns relative to the previous leg. An ordinary unqueued move completes independently. Shift orders add the coordination described on pages 7 and 11.

## Controls

| Menu control | Default | Range / step | Effect |
| --- | --- | --- | --- |
| Commander placement → Formation spacing (single click) | 35% | 0–100 / 1 | Raises or lowers the nominal gaps used by new single-click placements. |
| Commander placement → Large-unit influence | 0% | 0–100 / 1 | Blends smaller members' spacing toward the largest relevant footprint. At 0, each member uses its own footprint. At 100, the largest footprint fully influences packing. |
| Alien cohesion → Alien: Placement spacing scale | 45% | 0–100 / 1 | Direct Alien faction multiplier for configured commander, FPS and rally spacing. It works independently of the Alien cohesion switch. |

## Understanding the spacing numbers

Single-click, drag, FPS spacing, FPS distance and rally percentages use a piecewise conversion. Through 30, the multiplier is `1 + value / 15`. Above 30, it is `3 + (value − 30) / 10`.

| Input | Nominal multiplier |
| --- | --- |
| 0 / 15 / 20 | 1× / 2× / approximately 2.333× |
| 30 / 35 / 100 | 3× / 3.5× / 10× |

These multiply the feature's packing formula. They are not literal increases in final centre-to-centre distance. Body clearance is a lower bound, so 0 does not allow bodies to overlap. Alien placement scale uses a different conversion: 45% means 0.45×. A low Alien value can reach the physical-clearance floor, after which further reductions have little visible effect.

<!-- pagebreak -->

# Click-and-drag placement

## Why it exists

A narrow native drag can begin as a single-file line. This is awkward when the commander wants a compact marching formation and can concentrate collisions or leave an army unnecessarily stretched. Separate drag spacing also lets the commander tune deliberate formations without changing ordinary click placement.

## How the shape develops

A valid short drag begins with two units across, forming a longitudinal rectangle. Widening the drag admits more columns. The shape progresses through a square, a sideways rectangle and eventually a single line when all required footprints fit across the frontage.

Drag direction establishes the lateral axis and facing. Native unit priorities influence assignment and row bands. Different body sizes and terrain projection can change the exact proportions. The server computes the tuned destinations, while the native client preview can still show a different arrangement.

| Relevant control | Default | Range / step | Practical use |
| --- | --- | --- | --- |
| Commander placement → Formation spacing (click and drag) | 15% | 0–100 / 1 | Changes packing gaps and required row space. The default nominal multiplier is 2×. |
| Commander placement → Large-unit influence | 0% | 0–100 / 1 | Changes how strongly large footprints enlarge smaller members' packing cells. |
| Alien cohesion → Alien: Placement spacing scale | 45% | 0–100 / 1 | Scales configured Alien gaps while retaining physical clearance. |

The length of the drag controls frontage. The spacing slider controls the space required by the units. Increasing spacing while keeping the same drag length can therefore increase the number of rows. A degenerate or rejected short drag can use a separated click-grid fallback.

## Reusing a formation and losing members

A one-click move, including a Shift-click, clears cached dragged-layout intention and builds its new grid. A new drag explicitly defines a new arrangement. The mod does not carry a previous drag through ordinary one-click movement.

Coordinated drags and Reverse can retain layout identity and heading information for related orders. Destroyed or despawned losses of at most 40% preserve eligible cached metadata, using the original member count as the denominator. Exactly 40% is allowed. Adding units, deliberately selecting a live subset, changing teams or transferring control invalidates that identity. Retention after completion defaults to 500 seconds.

Reverse captures current positions or queued predecessor destinations, as explained on page 5. Retained identity does not make an ordinary move replay an old set of empty slots.

<!-- pagebreak -->

# Aircraft and Reverse

## Aircraft placement

Aircraft are separated from ground packing so their size and count do not inflate the ground grid. In a mixed formation, the air band begins **50m behind the rear of the ground formation**. Ordinary aircraft are distributed across that rear area. An air-heavy or all-air selection uses a compact cluster with additional rows instead of an increasingly long single-file line. An all-air order is anchored at the clicked destination.

Colossus and Defiler receive their own rear placement and are excluded from the spacing metrics used for ordinary flyers. Their own footprint still needs room. The purpose is to stop one very large air unit from spreading all smaller flyers excessively.

Aircraft arrival is horizontal. Their destination behaves like an upright cylinder using the native horizontal arrival tolerance. Flight height does not prevent arrival or inflate the waypoint ETA into an unnecessary vertical journey. Ground speed-cohesion calculations exclude aircraft, while the shared waypoint wait can include eligible aircraft.

The 50m offset, compact-cluster rule and Colossus/Defiler treatment are built-in behavior. Placement spacing and Alien placement scale still affect relevant layouts. Native flight and obstacle handling determine the actual route.

## Reverse movement

Custom Reverse moves a selected formation backwards while preserving its current orientation and relative arrangement. Full custom snapshot placement and strict pacing require the relevant faction's cohesion switch to be ON. For a queued Reverse, the snapshot can use the preceding queued destinations. The mod translates those offsets to the new anchor instead of rotating the shape toward the new destination.

Vehicles use their native Reverse behavior and nominal reverse speed. Infantry that cannot receive native Reverse are given ordinary Move orders into their translated slots. They can turn and walk normally while still moving into the requested formation.

Strict Reverse pacing uses fixed values: pacing strength 1, hold strength 1, minimum custom speed fraction 0, arrival allowance 0.25s and a 120s pacing-delay budget. These override the ordinary Near/Far profile values. With faction cohesion OFF, vehicles use native Reverse targets, while eligible infantry can still receive the ordinary Move fallback to snapshot positions. Waypoint regrouping operates separately.

| UI control | Relation to this feature |
| --- | --- |
| General → Human group cohesion / Alien group cohesion | Enables that faction's custom speed coordination, including strict Reverse. |
| Commander placement → Human / Alien overdue checkpoint wait | Controls the additional checkpoint grace for captured Reverse waypoints. |
| General → Formation mod | Master ownership and cleanup. |

Projection, clearance, native order availability and ownership can prevent a strict snapshot from being applied. An explicit new order or player takeover takes precedence. Strict pacing adjusts speeds; navigation and native vehicle steering still decide how units negotiate obstacles.

<!-- pagebreak -->

# Native group slowdown and travel cover

## Suppress native group slowdown

Widely dispersed selections can receive severe native group slowdown. Units may spend much longer crossing open ground even though their own movement mode permits a higher speed. This control provides independent native dispatch speeds when the commander wants a direct relocation.

**General → Suppress native group slowdown** defaults **ON**. During new grouped commander dispatches it removes the native grouped speed scale and native UnitCohesionGroup slowdown layer. Requested Normal, Fast, Stealth or Reverse modes still determine the underlying speed. Terrain, collision avoidance, combat and native movement restrictions can reduce actual motion.

### Interaction with faction cohesion

Enabled faction cohesion also suppresses native group slowdown for that faction. Effective suppression is the combination of the separate slowdown switch and that faction's cohesion switch.

| Slowdown switch | Faction cohesion | Result for new grouped dispatches |
| --- | --- | --- |
| ON | Either value | Native group slowdown is suppressed. |
| OFF | ON | Native group slowdown is still suppressed by faction cohesion. |
| OFF | OFF | Native group slowdown can operate. |

Shift movement uses the mod's bounded custom pacing and checkpoint waits. An ordinary click can use independent speeds without entering that dynamic pacing system. This separates broad relocation from deliberately coordinated journeys.

## Travel cover preference disabler

Infantry can substitute or prune long-range travel waypoints according to cover preferences. This can create detours while moving between two locations, even when the commander's immediate aim is relocation.

**General → Travel cover preference disabler** defaults **ON**. It suppresses that automatic infantry travel-cover substitution during long-range path processing. Native movement-area assignment is retained, including the smaller completion area for Stealth movement. Combat-cover decisions, targeting and cover claims continue through the game's combat logic.

Turning the switch OFF restores native travel-cover preference for subsequent path calculations. Changing it does not immediately reissue existing orders. It is an infantry travel control. The vehicle shortcut pass is a separate feature on page 15.

Both controls are ON/OFF switches. They require the Formation master to be active. Their saved values are independent, so either can be tuned without changing formation spacing.

<!-- pagebreak -->

# How Shift cohesion works

## Why it exists

Native waypoint movement lets each unit proceed at its own pace. On long journeys, fast units can separate from slow ones and arrive before the rest of the selected army. This increases micromanagement and can expose the leading units to combat without support.

The mod adds ground travel pacing to **Shift-click and Shift-click-and-drag**, including a single Shift-click with an empty queue. Each queued segment starts when its captured native order becomes active. Ordinary unqueued clicks and drags use placement with independent completion. Reverse has its own snapshot controller.

## The slowest nominal speed tier

Planning uses requested-mode speed statistics. Fast infantry uses SprintSpeed, Normal infantry RunSpeed and Stealth infantry WalkSpeed. Vehicle Reverse uses ReverseNormalSpeed. Alien units use their ground or flight statistics for the requested mode. Current velocity, a temporary stop or a native speed scale does not turn the planning speed into zero.

During ordinary Shift travel, the active slowest nominal tier receives no custom speed cap. Faster members are paced against that tier's actual forward progress and formation-relative median position. Progress is smoothed over approximately one second. Faster units behind can catch up, while units ahead receive lower speed targets.

This avoids a loop in which slowing the slowest members lowers the whole group's reference speed again. Native restrictions can still slow any unit. The mod supplies speed limits and destination slots; collision avoidance and route choice continue through native navigation.

## Pacing and checkpoint waiting are separate

**General → Human group cohesion** and **Alien group cohesion** both default **ON** and can be switched independently.

| Mechanism | What it coordinates | Main controls |
| --- | --- | --- |
| Travel pacing | How far faster ground members get ahead during a leg. | Faction cohesion switch and the distance-profile controls. |
| Waypoint regrouping | When the selected group may advance to its next queued leg. | Human / Alien overdue checkpoint wait and Alien combat checkpoint wait. |

Turning Human or Alien group cohesion OFF releases that faction's custom speed caps. Captured Shift waypoint waiting still operates while the Formation master is ON. Setting pacing strength to zero likewise does not disable checkpoint waiting.

Aircraft have their own placement and horizontal arrival behavior. They do not set the ground pacing tier, but eligible aircraft can participate in the shared waypoint ETA and waiting set.

Large turns on new drags weaken ordinary pacing to give units space to turn. Navigation problems, casualties, excessive separation and exhausted delay budgets can release pacing. Regrouping then continues according to its own bounded waypoint deadline.

<!-- pagebreak -->

# Cohesion distance and arrival controls

Each faction has twelve numeric controls in its own panel group. The table below uses the exact label after the Human: or Alien: prefix. These are independent settings. Editing one faction does not copy values to the other.

## Distance endpoints

Near means a zero-distance command. Far means that faction's Maximum cohesion range. Values interpolate linearly according to the command's horizontal origin-to-destination distance, captured when the move is issued. Queued planning uses the preceding Move or Reverse destination as its origin.

The profile stays associated with that command. Moving closer to the target does not continually increase its strictness. A command beyond Maximum cohesion range receives no ordinary custom pacing or holding. Waypoint regrouping can still operate.

| Control | Human default | Alien default | Range / step |
| --- | --- | --- | --- |
| Maximum cohesion range | 5000m | 5034m | 1–100000m / 1m |
| Destination arrival radius | 600m | 600m | 0–10000m / 25m |
| Cohesion pacing strength (near) | 1 | 1 | 0–1 / 0.05 |
| Cohesion pacing strength (far) | 0.4 | 0.5 | 0–1 / 0.05 |
| Arrival allowance (near) | 0.25s | 0.5s | 0–60s / 0.25s |
| Arrival allowance (far) | 5s | 10s | 0–60s / 0.25s |

## What changing them does

**Maximum cohesion range.** Increasing it admits longer coordinated journeys. It also makes an unchanged journey fall nearer the Near end of the profile. Reducing it moves that same journey toward the Far values and can put it beyond the custom-pacing range.

**Destination arrival radius.** This belongs to the dispersed-selection fallback, not the unit's final arrival tolerance. It determines which selected ground units are near the clicked destination when that fallback is prepared. The full explanation is on page 10.

**Cohesion pacing strength.** Zero removes the profile's pacing influence. One applies its full calculated target to eligible faster members. Intermediate values blend that target with independent speed. The slowest nominal tier stays uncapped during ordinary Shift movement.

**Arrival allowance.** During ordinary pacing this is the permitted lead expressed in seconds of the slowest nominal speed. Its distance allowance is approximately half the member's radius plus slowest nominal speed multiplied by Arrival allowance, with a 1m minimum. Increasing it permits looser travel. Native arrival detection remains a separate decision.

For a 2500m Human command, the distance fraction is one half. The effective pacing strength is 0.7 and arrival allowance is 2.625s. Menu changes apply to subsequent custom moves.

<!-- pagebreak -->

# Cohesion speed, holding and delay controls

| Control | Human default | Alien default | Range / step |
| --- | --- | --- | --- |
| Minimum speed fraction (near) | 0.05 | 0.1 | 0.05–1 / 0.05 |
| Minimum speed fraction (far) | 1 | 1 | 0.05–1 / 0.05 |
| Formation hold strength (near) | 1 | 1 | 0–1 / 0.05 |
| Formation hold strength (far) | 0 | 0.25 | 0–1 / 0.05 |
| Pacing delay budget (near) | 15s | 15s | 0–120s / 0.5s |
| Pacing delay budget (far) | 10s | 10s | 0–120s / 0.5s |

## Minimum speed fraction

This sets the floor of a custom pacing cap as a fraction of the member's nominal requested-mode speed. Raising it allows less deliberate slowing. A value of 1 prevents the custom controller from requesting a lower speed for that member. It does not make a blocked or turning unit physically maintain that speed.

The Far default of 1 means the exact maximum-distance endpoint has no room to reduce ordinary custom speed, even when its pacing strength is nonzero. At intermediate distances the interpolated floor and strength act together.

## Formation hold strength and the current implementation

These fields represent speed-based holding of an eligible saved arrangement. They are intended to control how strongly the holding phase resists members pulling away from their relative slots.

**In version 3.16.2, these two sliders have no effect on the normal commands described here.** Ordinary Shift moves use progress pacing. Reverse uses its own fixed hold strength of 1. Use pacing strength, arrival allowance and speed floors to tune ordinary travel.

## Pacing delay budget

This limits how long a member can lose time through custom slowing. The controller accumulates an equivalent delay from applied speed reductions. A second limit compares time with the member's initial free-speed arrival estimate plus the same budget. Exhausting either releases that member from pacing. Larger budgets permit stronger coordination for longer. Zero leaves no pacing-delay allowance.

This budget is separate from the overdue checkpoint wait. A unit can finish its pacing budget while its group still has a valid waypoint regroup deadline.

## Turns, validation and release

On a new dragged turn, relief increases from 15° to 90°. At 90° or more, ordinary pacing strength becomes 15% of the profile value and the custom speed floor is at least 80% of nominal speed. Health checks also release or degrade coordination for sustained spread, detours, stalls and casualties.

Pacing strength, hold strength and delay budget require Near ≥ Far. Minimum speed fraction and arrival allowance require Near ≤ Far. Reversed endpoint pairs are rejected or restored to defaults when loaded invalidly.

<!-- pagebreak -->

# Nearby destination cohesion

## Why it exists

A very scattered selection can include units that are already near the requested destination and others far away. Trying to pace the whole selection as one ground formation can cause excessive waiting or poor coordination. The nearby fallback gives the already-local selected units a usable arrival group.

## How admission works

When a selected ground group is too dispersed or exceeds its configured member limit, the mod can build a destination-local cohort. It takes eligible selected ground units already inside the horizontal radius around the clicked destination or drag centre, nearest first up to the member limit. At least two eligible ground units are required.

The default custom-cohesion member limit is 256. The dispersed-selection test allows a 250m starting spread plus an allowance for the selected bodies. These advanced admission settings are outside the normal panel.

This check happens when the order is issued. The local cohort does not automatically recruit distant selected units that enter the radius later. It uses arrival/progress pacing without shape holding. Farther selected ground units receive independent rear-band destinations, and aircraft use the corresponding rear air placement.

| Menu control | Default | Range / step | Use |
| --- | --- | --- | --- |
| General → Nearby destination cohesion | ON | ON/OFF | Allows this fallback for both factions. OFF also releases active destination-local pacing. |
| Human cohesion → Human: Destination arrival radius | 600m | 0–10000 / 25m | Human admission radius. Zero disables the fallback for Humans. |
| Alien cohesion → Alien: Destination arrival radius | 600m | 0–10000 / 25m | Independent Alien admission radius. |

Each faction's cohesion switch must be enabled for its custom pacing. The ordinary maximum-distance profile still applies. If no suitable local cohort can be formed, eligible units retain their available independent/native movement handling.

## Three distances with different jobs

| Distance | Measured from | Purpose |
| --- | --- | --- |
| Destination arrival radius, default 600m | Clicked destination or drag centre at issue time. | Admit an already-local ground cohort from a dispersed selection. |
| Waypoint cutoff, fixed 1000m | Current main body at each waypoint activation. | Decide which captured members the group will wait for. |
| Maximum cohesion range, 5000m Human / 5034m Alien | Planned movement origin to destination. | Select the ordinary ground pacing profile and its eligibility. |

Changing Destination arrival radius does not enlarge native final-arrival tolerance or the 1000m waypoint cutoff. The nearby switch is a fallback control, while compact selections continue to use their faction's ordinary pacing rules.

<!-- pagebreak -->

# Waypoint regrouping

## Why it exists

Units advancing independently through queued points can turn a coordinated army into a long stream. A strict requirement to wait forever for every member has the opposite problem: one stuck unit can stop the whole queue. Bounded regrouping gives slow units a fair chance to catch up at each checkpoint while retaining an eventual continuation deadline.

## A shared deadline for each waypoint

1. At activation, identify the current main body from the horizontal median of captured living members' positions. A severely split selection uses a deterministic actual-member tie-break when the median has nobody near it.
2. Include members at or within **1000m** of that current centre. More distant members keep their accepted orders but are excluded from this waypoint's waiting calculation.
3. Find the longest remaining distance among included members and divide it by the slowest included unit's uncapped nominal speed for the requested mode. This produces one shared ETA.
4. Use native ground arrival handling and horizontal aircraft arrival. Advance immediately when all included members have completed their checkpoint, including any pending Alien combat return.
5. Otherwise allow the configured overdue wait after the shared ETA, then advance the queued group. Alien combat can add one extra allowance, described on page 12.

Ground estimates use fresh remaining path length when available, including the long-range part. Aircraft timing is horizontal. One initial correction accepts fresh paths when they are ready or after the three-second startup window. The deadline is then fixed apart from the one bounded Alien combat allowance. Repeated reroutes do not restart the countdown.

## Overdue wait sliders

| Commander placement control | Default | Range / step |
| --- | --- | --- |
| Human overdue checkpoint wait | 5s | 0–120s / 1s |
| Alien overdue checkpoint wait | 7s | 0–120s / 1s |

A larger value gives overdue members more time. Zero removes the extra grace after ETA; native early completion and the ETA-based deadline still apply. These are fixed faction timers, replacing the earlier distance-scaled 5-to-15-second design.

For example, a longest remaining route of 600m and slowest included nominal speed of 10m/s produce a 60s estimate. After the initial path correction, the ordinary Human deadline adds 5s and the Alien deadline adds 7s. Early completion can advance sooner.

Every new waypoint recomputes actual positions, inclusion and ETA. A straggler that has caught up can be included again. A member still beyond 1000m keeps attempting its accepted route without setting the group's waiting time. Waypoint waits operate independently of the faction speed-cohesion switches.

<!-- pagebreak -->

# Alien combat return and straggler cleanup

## Returning after combat

Native Alien combat can interrupt movement or replace a waypoint order. Without recovery, a few members can finish fighting and remain separated from their group's route. The mod tracks captured Alien waypoint intentions so eligible members can resume their checkpoint after disengaging.

AI-issued Attack or Attack-move interruptions can preserve and requeue the captured waypoint chain. After combat ends, the current captured order can be re-executed to resume movement, retaining the queue. A fresh commander replacement, utility order, player takeover or ownership change takes precedence over restoration.

If the group has already advanced, the unit continues with its valid queued progression. Recovery also covers a displaced member returning to the final checkpoint. Combat remains controlled by the game's targeting and behavior logic.

| Commander placement control | Default | Range / step |
| --- | --- | --- |
| Alien combat checkpoint wait | 10s | 0–120s / 1s |

When an included Alien member has interrupted the waypoint for combat, the effective deadline becomes **shared ETA + Alien overdue wait + Alien combat wait**. The combat allowance is added at most once per waypoint, irrespective of repeated engagements or combat duration. Distant excluded members do not trigger it. Setting it to 0 retains combat return but adds no combat grace. Earlier complete regrouping can advance immediately.

## Five-minute distant-straggler cleanup

A member that continuously remains more than **1000m** from its current main body for **300 simulation seconds** has all of its queued orders cleared and its active native movement stopped. This removes abandoned movement work that could otherwise continue requesting navigation for a long time.

The monitor samples roughly once per simulation second. Rejoining at or inside the cutoff resets the timer. The timer carries across related queued waypoints, so reaching a new checkpoint does not grant a perpetually distant member another five minutes.

The check applies while the mod still owns that captured group movement intention. It includes an exact final native path that is still running after its queue entry has advanced. A new replacement command, control transfer, team change or lifecycle cleanup releases the old watch. After cancellation, the unit can receive new orders normally.

The 1000m cutoff and five-minute timeout are built-in values with no sliders. This cleanup has a different job from a checkpoint wait: the checkpoint can advance much sooner while distant units keep trying to catch up, until their continuous-outside timer actually expires.

<!-- pagebreak -->

# Production rally parking

## Why it exists

Sending newly produced units to one shared coordinate can cause bunching, collisions and vulnerability to area attacks. When no explicit rally point exists, a default exit coordinate can leave units around the offramp and obstruct subsequent production. Parking reservations give each eligible unit space to settle outside that shared point.

## How a destination is chosen

The mod records a production parking request and captures its rally spacing. It retains the factory's native forced-exit movement. Once the unit can receive its rally order, it searches for a valid parking position near the rally anchor.

If no rally point is set, or the rally point is the native exit marker, the anchor is moved outward from the factory along its exit direction. The outward distance includes the factory and unit sizes. This creates a parking area beyond the immediate exit instead of reusing the offramp coordinate.

Ground candidates are checked against terrain/navigation, other parking reservations, units, structures and construction sites. A position is reserved before dispatch, so another facility can account for units still travelling to their parking slots. Aircraft parking uses height-separated candidates and reservations; an owned preferred-height change is restored when parking ownership ends.

## Control and scope

| Menu control | Default | Range / step | Effect |
| --- | --- | --- | --- |
| Rally → Rally spacing | 20% | 0–100 / 1 | Increases or decreases the nominal reserved parking space. Default nominal multiplier approximately 2.333×. |
| Alien cohesion → Alien: Placement spacing scale | 45% | 0–100 / 1 | Applies the Alien faction factor to configured parking gaps. |

Physical clearance remains a lower bound. Existing pending tickets keep the scale captured when they were created, so a new slider value primarily affects subsequent production.

Shrimp, Harvesters and equivalent resource workers retain native rally and automatic resource behavior. Unsupported flight agents and units under direct player control also remain subject to their own eligible native handling.

## Crowding, retries and new orders

Search work is bounded and can resume later. Exhausted searches wait and retry when the area clears; they are not all redirected to one common marker. A crowded or unsuitable area can therefore delay parking assignment.

A fresh accepted external order, takeover, team change or destruction releases the old reservation. Internal replanning toward the same captured rally destination can keep it. Master OFF releases reservations and returns pending production to native rally handling. The mod improves parking destinations, while the native route and collision system still determine how units get there.

<!-- pagebreak -->

# FPS squads

## Why it exists

Native followers can crowd the player, and a shared move coordinate can bunch the whole squad into a small space. The FPS layout controller gives squad members separate positions relative to the leader or their ordered destination. It applies to eligible units in the noncommander player's own group.

## Follow, stopping and explicit orders

During sustained travel, followers form a staggered double-file column behind the leader, aligned with movement. When the leader stops, they use a fan or additional rings behind the player. An explicit Move creates a footprint-aware grid around its destination. Completing that Move does not automatically turn it back into Follow.

Attack orders retain their native combat target while the managed layout supplies approach positions. Native combat and individual accepted replacement orders can take ownership. New group or Follow commands can restore managed following.

## Controls

| FPS squads control | Default | Range / step | Effect |
| --- | --- | --- | --- |
| FPS unit spacing | 35% | 0–100 / 1 | Gaps in travelling columns, stopped fans and destination grids. Default nominal multiplier 3.5×. |
| FPS distance to player | 10% | 0–100 / 1 | Leader standoff. Default multiplier approximately 1.667×. |
| FPS movement allowance | 15m | 1–100 / 1m | Leader displacement before ordinary follow repositioning. Larger values require fewer corrections but let the player move farther before the squad updates. |

Large-unit influence and Alien placement scale also affect relevant FPS packing. Leader distance starts from **(5m + leader physical radius) × distance multiplier**. The final standoff is at least leader radius + largest follower radius + 1m, so physical clearance can make it larger than the simple scaled value.

## The 15m and 30m distinction

With the current allowance of 15m, ordinary repositioning occurs after more than 15m of leader displacement. Entering the travelling column requires departure greater than **max(10m, 2 × allowance)** from the stopped origin, currently **30m**. This avoids changing into a convoy for every short adjustment. Once in the column, continuous movement retains it. About 0.75s without motion returns to the fan.

The observation/update interval is 0.25s. Initial placement, membership or setting changes and motion-state transitions can trigger an update before the usual displacement threshold. Invalid or overlapping projected slots can be skipped. Player-controlled, driven, embarked, transferring and forced-exit members are excluded from managed positioning as appropriate.

<!-- pagebreak -->

# Pathfinding shortcuts and scouting

## Pathfinding shortcut pass

Native routes can retain bends that funnel units toward the same passage edges. The vehicle path processor already tries to remove unnecessary corners, but its normal maximum tested span is 200m. The Formation control makes that distance adjustable.

| General control | Default | Range / step |
| --- | --- | --- |
| Pathfinding shortcut pass | 1000m | 0–2000m / 25m |

Increasing the value lets the existing native pass test longer direct segments. Decreasing it restricts those tests. Zero disables corner shortcuts. Native traversal constraints and subsequent vehicle-width corner offsets still govern accepted paths. A crossing rejected by the navigation map continues to require an allowed route.

Changes apply when the server next processes a vehicle path; they do not immediately reissue current movement. The Formation master restores the native 200m limit when OFF. Prototype vehicles with navigation delegated to a client calculate their paths on that client, outside this server-side setting.

This is a route-simplification control. It can shorten valid detours, but the resulting routes still depend on map connectivity, clearance and native avoidance. Its default is the saved 1000m tuning confirmed for this version.

## Scout command

Scouting reduces the manual work of sending several units to separate parts of the map. As commander, arm Scout with `/scout`, select at least five eligible AI units and issue a single-click nonqueued Move. That next qualifying move is consumed once and replaced with separate map-sector destinations.

Up to 75 eligible units can be distributed. Bounded checks consider the unit's navigation graph, traversable tags, terrain and separation from other scout destinations. Supported aircraft can participate. This is a one-time spread across the map.

Fewer than five eligible units use the normal move. Failed or excess scouts that can still move receive formation destinations around the original click. Protected or navigation-unready units retain their valid existing handling. Native order acceptance can still fail and the command reports outcomes.

Scout arming is temporary commander state, with no saved tuning slider. Formation Status reports the current commander's state. The compact chat list is on page 18.

<!-- pagebreak -->

# Diagnostics

## Why these controls exist

A unit can fail to join custom pacing for several different reasons: its native order was rejected, its navigation was unavailable, it became player-controlled, or its accepted route triggered a health release. Focused traces distinguish those cases so tuning can follow the actual cause.

| Diagnostics control | Use |
| --- | --- |
| Cohesion debug | Global tracing for both factions. Runtime default OFF. Turning it OFF also clears individual and next-group watches. |
| Cohesion debug status | Shows global tracing state and watched groups. |
| List cohesion groups | Lists up to eight recent retained Human or Alien groups, including completed or released groups. |
| Watch next cohesion group | Traces the commander's next grouped movement attempt, including skip reasons. Requires commander role. |
| !cohesion / !aliencohesion / !waypointwait forms | Generic command forms grouped here for advanced access. Ordinary tuning is available through the dedicated sliders. |

## Reading a trace

Native dispatch records show attempts, acceptance or rejection, projected targets and captured order state. A group having no enrolled custom-cohesion members is not by itself evidence that its native Move order failed.

Periodic movement detail reports requested and actually applied caps, speed mode, path readiness, direct/remaining distance and reasons for skipping a cap. Ordinary slowest-tier members should be labelled as pace setters with no custom cap. A native hold or obstacle can still make their actual speed lower.

Waypoint output distinguishes native completion, horizontal air arrival, regroup timeout and distance-based exclusion. Alien interruptions and resumption are recorded. Straggler logging identifies timer start, rejoining/reset and five-minute cancellation. Use those messages to separate a deliberate timeout from failed native arrival.

## Focused use and retained information

Watch next cohesion group is useful for isolating one selection without tracing every group. The existing chat interface can also target a retained group ID, listed on page 18. Global periodic detail rotates through a bounded sample of members; an explicitly watched group can show more detail.

Debug state and watches are runtime state, rather than saved movement defaults. The common sampling and history controls are hidden from the normal panel. Their defaults are a 3s debug interval and 80 retained trace samples per command. Regular pacing updates use a separate default interval of 0.35s.

Older Close/Medium/Long profile keys in historical configurations do not drive the current continuous Near/Far controls. Use the labels and defaults in this manual when interpreting a current trace. Logging itself adds work, especially with many simultaneously active groups.

<!-- pagebreak -->

# Performance information

## What the available measurements cover

An existing 16 November log contains instrumented movement observations from **Si_Formation 3.16.0**. The installed monitor times entire Harmony-wrapped game methods. Those elapsed times can contain native execution, nested calls and multiple patches. They are not an exclusive measurement of Formation's CPU cost.

For example, two approximately 20-second reporting windows recorded the following whole-method observations for `AIMoveOrder.Evaluate`.

| Log window ending | Calls | Summed elapsed time | Reported mean | Maximum call |
| --- | --- | --- | --- | --- |
| 14:15:16 | 12,745 | 22.02ms | 0.002ms | 0.95ms |
| 14:15:36 | 23,815 | 35.04ms | 0.001ms | 0.55ms |

These are scoped observations from an older running version, not a benchmark of 3.16.2 or a before/after comparison. The monitor displays only its selected top rows, combines patch-owner labels and does not cover ordinary Melon update callbacks comprehensively. It also reported an instrumentation failure for the FPS native-order hook. Consequently these numbers cannot establish total mod overhead or net server savings.

## Verified work scheduling

| Area | Current implementation |
| --- | --- |
| Ground cohesion | Default evaluation interval 0.35s per group. |
| FPS layouts | Observation/update interval 0.25s; movement allowance avoids reissuing a layout for every small displacement. |
| Distant-straggler monitor | At most one position scan per simulation second while tracked work exists; each group's main-body result is shared within that scan. |
| Rally parking | Reservation pruning once per unscaled second. Empty pending work exits early. Searches examine at most four pending tickets per tick, up to 24 candidate attempts per ticket, and cooperatively yield after the search stopwatch exceeds roughly 2ms. |
| Waypoints | Barrier snapshots are reused until the collection changes, with temporary recovery sets created only when needed. |
| Shortcut pass | Adjusts the existing native guard. Path workers read a published numeric value without accessing Unity or preferences. |

The rally stopwatch is a cooperative search check, not a strict cap on the cost of an entire frame. One candidate's occupancy or navigation work can take time before the next budget check.

## What can change the workload

More managed units, more queued waypoints, crowded rally areas and extensive debug logging can increase work. Larger shortcut spans can change the length of native graph checks. Better spacing and abandoned-order cleanup can reduce avoidable congestion or lingering movement requests, but no controlled measurement currently quantifies the net benefit. The available evidence supports the scheduling description above and the limited whole-method observations.

<!-- pagebreak -->

# Chat command reference

Tuning is intended to use ClientModUI. This list is a compact reference for the existing chat interface. The menu uses the registry's ! spelling for admin commands; slash forms remain available.

| Command | Argument forms |
| --- | --- |
| /formation | No argument; status; enabled on / off; advanced |
| /formationspace | 0–100; status |
| /dragspace | 0–100; status |
| /sizeinfluence | 0–100; status |
| /rallyspace | 0–100; status |
| /alienspread | 0–100; status |
| /fpsspace | 0–100; status |
| /fpsdistance | 0–100; status |
| /fpsallowance | 1–100; status |
| /movementfix | on; off; status |
| /travelcoverdisabler | on; off; status |
| /destinationcohesion | on; off; status |
| /pathshortcut | 0–2000; status |
| /groupcohesion | on; off; status |
| /aliengroupcohesion | on; off; status |
| /cohesion | key and number; status |
| /aliencohesion | key and number; status |
| /waypointwait | human / alien / combat and 0–120 seconds; status |
| /cohesiondebug | on; off; status; groups; next; group ID |
| /scout | No argument; on; off; status |

Setting changes and diagnostics require AdminMod Generic permission. Formation Status is public. Scout requires commander role, and watching the next group also requires commander role. Settings and parameter meanings are explained with their UI controls on the preceding pages.
