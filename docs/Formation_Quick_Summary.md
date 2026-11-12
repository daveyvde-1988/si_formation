# Si_Formation
Quick guide - group cohesion and everyday controls

Version 3.8.1 candidate | 12 November 2026

## What the mod does

Si_Formation adjusts commander click/drag placement, FPS follower spacing and production rally parking. Optional group cohesion coordinates ground units while retaining native navigation, combat and requested movement modes. Settings belong to the server; gameplay does not require a client mod.

## How group cohesion works

Human and Alien **Group cohesion** switches are independent and default **OFF**. Each faction has one continuous distance profile, from zero to **MaximumRange** (5000m by default). The command captures its tuning when issued; approaching the destination does not make the rules stricter again.

Distance is measured horizontally from the admitted ground group's centre to the clicked destination or drag midpoint. Queued moves estimate their origin from predecessor Move destinations. Aircraft do not influence ground distance or pacing.

| Command distance | Intended behavior with default settings |
| --- | --- |
| A few metres | Strong arrival coordination. An intact remembered drag formation can also hold its shape during travel. |
| Intermediate distances | Holding and arrival influence fade smoothly. Units have increasing freedom to navigate independently. |
| Exactly 5000m | Far settings apply: very light arrival pacing, no holding and a 95% minimum speed fraction. |
| Beyond 5000m | No custom holding or arrival pacing. Enabled faction cohesion still suppresses native group slowdown. |

Coordination slows early units within defined limits; it never increases native speed. Arrival is a best-effort target through native navigation, rather than rigid geometry or a guarantee. Stalls, spreading, casualties and delay budgets can release a command to independent travel.

## Deliberate formations and drag width

Only a complete, intact layout deliberately set with a successful click-drag is remembered. A later click with the same ground membership reuses intended slots, including while a previous move is unfinished, and rotates the layout toward travel. Ordinary click grids and rally parking never become remembered shapes. Losses, changed membership or player control invalidate the whole identity so gaps are not carried forward.

A new drag uses arrival pacing while units reform. Narrow valid drags start at a rectangle-sized minimum instead of a deep single-file column. Widening admits more units per row and eventually increases lateral spacing. Native priority bands remain separate, so mixed-priority groups can retain multiple rows. Aircraft use separate centered rear rows and retain native flight behavior.

**Preview limitation:** server destinations use this rectangle minimum; the native client preview can still display the old geometry. Preview alignment and visual smoothing remain follow-up client work.

<!-- pagebreak -->

# Tuning cohesion
The same 12 numeric controls exist separately for Humans and Aliens.

## Near and Far are distance endpoints

Near means a zero-distance command; Far means MaximumRange. Every pair interpolates linearly. With a 5000m maximum, a 2500m command uses the midpoint of each pair. A longer-than-maximum command receives no custom cohesion, even if Far strength is nonzero.

| Control | Default | Meaning and useful direction |
| --- | --- | --- |
| MaximumRange | 5000m | Custom-cohesion distance limit; valid 1-100000m. Increasing it makes the same journey use stricter Near-side values. |
| DestinationRadius | 500m | For dispersed/oversized selections, coordinate already-selected ground units near the destination; valid 0-10000m. Zero disables this fallback. |
| StrengthNear / StrengthFar | 1 / 0.05 | Arrival-pacing influence, 0-1. Higher values try harder to coordinate arrival. Zero removes this effect at the chosen endpoint. |
| HoldStrengthNear / HoldStrengthFar | 1 / 0 | Holding influence for intact remembered drag layouts, 0-1. Higher values more strongly limit units pulling ahead; they do not steer lateral paths. |
| MinimumSpeedNear / MinimumSpeedFar | 0.1 / 0.95 | Lowest custom cap as a fraction of requested-mode native speed; valid 0.05-1. Higher values prevent deeper deliberate slowdown. Native limits can still be lower. |
| ArrivalWindowNear / ArrivalWindowFar | 0 / 15s | Allowed early arrival; valid 0-60s. Larger values let early members reach their slots sooner. |
| MaxDelayNear / MaxDelayFar | 30 / 0.5s | Deliberate slowdown budget; valid 0-120s. Exhaustion ends that member's pacing. It also bounds waiting beyond the initial free-speed arrival estimate. |

Strength, HoldStrength and MaxDelay must stay level or decrease toward Far. MinimumSpeed and ArrivalWindow must stay level or increase. Commands validate these relationships. Defaults above do not overwrite existing saved tuning.

## Example at half the maximum distance

Untouched defaults at 2500m give arrival strength **0.525**, holding strength **0.5**, minimum speed fraction **0.525**, arrival allowance **7.5s** and delay budget **15.25s**. Holding still requires an intact remembered drag formation; a fresh grid or new drag uses arrival pacing only.

## Large turns receive extra freedom

The 3.8.1 correction progressively relaxes the captured profile between 15 and 90 degrees of rotation. At 90 degrees or more, holding is off, arrival strength is 15% of its normal distance-based value, and our cap cannot fall below 80% of requested-mode speed. A lower native limit still wins. Relief lasts for that command; a later straight move uses ordinary distance tuning again.

## Where settings live

Use the existing **Human cohesion** and **Alien cohesion** menu sections. Commands: `/cohesion <key> <number>` and `/aliencohesion <key> <number>`; `status` reads a value. CFG: `UserData/cfg/Formation/si_formation_native.cfg`, category `[Si_Formation_Native]`. Numeric keys use `Cohesion<Key>` or `AlienCohesion<Key>`. Old Close/Medium/Long stage keys are ignored. Changes require AdminMod Generic permission and save server-wide.

<!-- pagebreak -->

# Safety, diagnostics and feedback
Supporting behavior and the current candidate's limits

## Nearby groups in a map-wide selection

DestinationRadius captures a fixed cohort when a dispersed/oversized command is issued. At least two eligible selected ground units must already be within the radius of the destination centre. The nearest qualify up to the unit limit. Distant units do not determine its speed or failure count, and later arrivals do not join. This local cohort uses arrival pacing without remembered-layout holding.

## Movement corrections in 3.8.1

Failure checks wait for navigation associated with the captured move and its startup health grace. Movement and provisional pacing begin immediately. The arrival estimate is recalculated once from fresh paths, rather than continually waiting longer for a stuck rear unit.

Jumping, climbing or falling temporarily suspends a cap while retaining membership. A member can resume pacing for the same order if coordination remains active. Death, player control, team changes and replacement orders release ownership. Native combat holds and navigation restrictions remain authoritative.

## Advanced CFG controls retained

The normal menu stays compact. Hidden timing/safety keys remain available in CFG and tuning commands: **UpdateInterval 0.35s**, **StartGrace 3s**, **TurnGrace 6s**, **HoldGrace 2s**, **InitialSpread 250m**, **PathDetourRatio 1.8**, **ProgressDistance 1m**, **MaxUnits 256**, **MaximumDuration 600s**, **Retention 120s**, **DebugInterval 3s** and **TraceHistory 80 samples**. These are defaults. General timing/admission controls are separate from the Near/Far profile; other health tolerances vary internally with distance.

## Scouting and travel cover

Scouting distributes up to 75 eligible units across map sectors. Alien flyers use their native agent, graph and movement mode. Restricted-tag checks use bounded navigation searches; uncertain/failed scout destinations fall back to the original clicked move. Ready movable units outside the scout cohort also receive an ordinary layout around that click. Player-controlled, embarked, forced-movement and navigation-unready exclusions are reported.

**Travel cover preference disabler** defaults ON under Commander placement. It skips infantry travel-waypoint cover substitution for new/recalculated paths. Combat decisions, claims and behavior trees remain native, although routes to already-chosen combat cover also pass through this shared travel step. OFF restores native travel preference.

## Commands and debug output

```text
/groupcohesion on
/aliengroupcohesion on
/cohesion MaximumRange 5000
/aliencohesion DestinationRadius 500
/cohesiondebug next
/cohesiondebug groups
```

Issue a move after `debug next`, then use `/cohesiondebug <id>` for its retained group history. Global logging is `/cohesiondebug on`; `off` clears watches. Debug needs Generic permission; watching your next move also needs commander role. Logs distinguish selection, native dispatch and cohesion enrollment, with unit IDs, destinations and cap/bypass reasons. Periodic detail is bounded. Ordinary orders are not automatically retried.

## User feedback and verification status

The author's 12 November feedback on **3.8.0** describes subtle, natural cohesion: desirable short-move holding and longer journeys without noticeable delay. Starting drags from a rectangle was welcomed. Preview mismatch and smoothing were identified for follow-up. These are qualitative observations, not measured performance or verification of 3.8.1.

**3.8.1 status:** Release build passed with zero warnings/errors. DLL staged only; no installation or live tests. Full controls: Group_Cohesion.md. Build details: Changes_3.8.1.md. A full technical report will follow.
