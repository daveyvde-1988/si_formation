# Si_Formation
Quick summary

Version 3.16.2 | 16 November 2026

Si_Formation improves commander placement, waypoint cohesion, rally parking and FPS squad spacing. The defaults reflect the current saved server tuning.

## 1. Regular single-click placement

Compact native formations are vulnerable to area-of-effect attacks and surrounding enemies, and can be difficult to relocate or disperse. The mod increases gaps between units and reduces the largest unit's influence on the whole group's spacing.

## 2. Click-and-drag placement

Native drag placements share those crowding problems and begin as a single-file line. The mod adds spacing and starts with a double-file column. Widening the drag expands the formation through a square and a wide rectangle toward a line.

## 3. Shift-click and Shift-click-and-drag

Native waypoints let faster units pull ahead, stretching a group into a long column and increasing commander micromanagement over long journeys. Shift moves activate advanced travel cohesion and regrouping at each waypoint. The group has time to gather before continuing, with bounded waits so stuck or distant stragglers cannot stall it indefinitely.

## 4. Rally fix

Shared rally coordinates can make newly produced units bunch up and collide. Without a rally point, units can remain on the factory exit ramp. These pileups obstruct exits, hinder dispersal, increase area-attack vulnerability and add collision work.

The mod finds separate, valid parking positions near the rally point. When no rally point is set, it looks for empty parking space just outside the factory to keep exits clear. Resource workers retain their native rally behavior.

## 5. Native group slowdown

Widely dispersed selections can suffer excessive native slowdown. A separate switch suppresses this behavior. Shift moves use the mod's custom cohesion and regrouping to keep the group coordinated.

<!-- pagebreak -->

## 6. Travel cover

Infantry can seek cover while travelling between destinations, creating unwanted detours during regular movement. A separate switch disables this travel-cover preference. Infantry retain native combat-cover behavior.

## 7. Pathfinding shortcut pass

Native routes can funnel units along shared paths and passage edges. The mod increases the shortcut pass's maximum span from the native 200m to a default of 1000m, helping remove unnecessary bends where the vehicle's navigation map permits a direct crossing. The distance is adjustable in the control panel.

## 8. FPS squads

Native squad units can crowd the player in Follow mode and bunch around shared move destinations. The mod spaces members away from the leader and each other.

During sustained travel, followers form a double-file column in the direction of movement. When the leader stops, they fan out behind the leader. Move orders spread the squad around the ordered coordinate.

## 9. Scout command

As commander, enter `/scout`, select at least five eligible units and issue a single-click move. The mod replaces that move with separate scouting destinations across the map, reducing the number of manual orders needed.

## 10. ClientModUI control panel

The Formation panel gives admins switches and spacing controls, with separate Human and Alien cohesion tuning. These detailed controls support the experimental fine-tuning phase and may be simplified as the defaults settle.

For detailed settings and behavior, see the separate [group cohesion guide](Group_Cohesion.md).
