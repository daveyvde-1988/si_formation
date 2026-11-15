# Si_Formation 3.11.0 — bounded regrouping

Ordinary one-click Moves now clear the selected group's drag memory and use fresh placement, including queued clicks. Explicit drags and Reverse keep their own layout behavior. Aircraft rows start 50m behind the ground formation's rear edge, spread across its width; clearance can require additional rows farther back. All-air selections keep their own command anchor.

Aircraft arrival is horizontal: the native final-waypoint radius forms an upright cylinder. Altitude does not affect arrival or aircraft regroup ETA. Native navigation and preferred flight height remain authoritative.

## Waypoint progression

The strict all-members arrival barrier is replaced for commander Move/Reverse batches, independently of faction cohesion switches:

1. On activation, use living members' actual horizontal positions to calculate a coordinate-wise median centre. If no member is within 1000m, use the member nearest the command anchor to resolve the split. Members more than 1000m from the centre are excluded from waiting, but retain their native queues and formation membership.
2. Divide the longest included remaining route by the slowest included unit's uncapped requested-mode speed. Use native nominal mode statistics, including vehicle reverse speed and Alien flight speed; never current velocity or custom cohesion caps.
3. Make one initial correction from fresh paths, when ready or after a maximum 3-second startup window. Later reroutes cannot extend the deadline.
4. Advance immediately when every included member has native completion. Ground units honour the native evaluator, which can also finish an order when native movement has ended. Aircraft use the horizontal cylinder. Completion is latched.
5. Otherwise advance at ETA plus grace: 5s at zero distance, linearly increasing to 15s at/beyond the owning faction's MaximumRange. Measure profile distance from the current main-body centre to the command anchor.
6. Release all owned queued heads, including stragglers. At the next activation, recalculate positions, cutoff and ETA; caught-up units automatically count again.

Commander placement has two shared grace sliders, 0..120 seconds. Commands: `/waypointwait near <seconds>`, `/waypointwait far <seconds>`, `/waypointwait status`. Saved keys are `WaypointGraceNear` and `WaypointGraceFar` in the existing configuration category. Settings are captured on waypoint activation; existing saved tuning remains unchanged. The schema-2 menu now has 45 rows.

Debug traces distinguish native completion, horizontal air arrival, distance exclusion, initial/corrected shared ETA, countdown and regroup timeout. Rejected or AI-interrupted orders remain unresolved only until timeout; the gate cannot create an order rejected by native dispatch. Explicit replacements, player control, forced control, team changes and lifecycle cleanup retain precedence. Attack, Patrol, utility and native Defend behavior remain unchanged. No dependency or public API change is introduced.

## Build

`dotnet build Si_Formation.csproj -c Release --ignore-failed-sources` passed on 13 November 2026: 0 warnings, 0 errors, 9.02 seconds. Output: `bin/Release/netstandard2.1/Si_Formation.dll`. Staged only; no installation, tests, gameplay checks or server lifecycle actions were performed. See [the current guide](Group_Cohesion.md) for user-owned live checks. Historical release notes and PDFs retain their original behavior descriptions.
