# Si_Formation - quick summary

Version 3.7.0 | 11 November 2026 | Brief helper, not the full report

Si_Formation tunes native commander formations, FPS followers and production rally parking. Recent work adds optional ground-group cohesion, separate Human/Alien tuning and an infantry travel-cover switch.

## Group movement

Human and Alien **Group cohesion** switches are independent and **OFF by default**. Each faction has its own General, Close, Medium and Long settings. Distance boundaries and near/far tuning are configurable.

| Default move distance | Behavior |
| --- | --- |
| Close: below 500m | A single click turns the remembered formation toward travel, then attempts formation holding and coordinated arrival. |
| Medium: 500-1000m inclusive | Arrival pacing without holding the shape during travel; units return to their intended formation at the destination. |
| Long: above 1000m | Lighter arrival pacing without formation holding. |

Dragging specifies a new formation and uses arrival pacing. Obstructions or a poor shape can cancel holding while arrival pacing continues. Excessive spread, lag, stalls, losses or delay can release coordination entirely, letting units proceed independently. This is soft speed control, not rigid movement or guaranteed simultaneous arrival.

## Formation memory and aircraft

- Complete unchanged groups reuse their intended destination layout, including when the previous move is unfinished. Temporary detours do not become the remembered shape.
- Selecting only part of a remembered group, adding units or merging groups creates a fresh formation. The unselected remainder reforms on its next move too.
- Aircraft use separate centered rear rows and do not occupy ground slots or influence ground-group pacing. Native flight altitude and navigation remain in control.

## Large or map-wide selections

If a selection is too dispersed or oversized, eligible selected ground units already within **500m of the destination centre** can coordinate arrival separately. Distant selected units move independently and do not slow this local group or count toward its failures.

Membership is captured when the order is issued; later arrivals do not join. Each faction's **DestinationRadius** setting changes the radius; **0 disables this fallback**. Normal unit limits and cancellation rules still apply.

## Infantry travel-cover preference

**Travel cover preference disabler is ON by default**, under Commander placement. ON skips the native infantry step that redirects travel waypoints toward nearby cover; OFF restores it. Combat reactions, cover choices and claims remain native. The shared path postprocessor can also affect travel to combat-selected cover. Changes apply to new or recalculated paths, with the Formation master enabled.

## Handy commands

```text
/groupcohesion on                 /aliengroupcohesion on
/cohesion DestinationRadius 500   /aliencohesion DestinationRadius 500
/travelcoverdisabler on           /travelcoverdisabler status
/cohesiondebug next               /cohesiondebug groups
```

Changes and debug commands require AdminMod Generic permission; `debug next` also requires commander role. Settings are shared by the server and available through the existing mod menu and `UserData/cfg/Formation/si_formation_native.cfg`.

**Status:** The 3.7.0 Release build passed with zero warnings and errors. The candidate was staged, not installed or gameplay-tested during this work. Live behavior remains to be verified. See [Group_Cohesion.md](Group_Cohesion.md) and [Changes_3.7.0.md](Changes_3.7.0.md) for details.
