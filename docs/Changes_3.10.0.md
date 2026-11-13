# Si_Formation 3.10.0

## Formation memory and cohesion

Dragged placement now uses a physical-radius diameter floor plus 0.5m spacing, and captured/reused snapshots validate the same pairwise circular clearance. Unsafe projected captures are not remembered. If reuse fails projection or clearance, the current command uses fallback placement while the previous valid snapshot is retained and its retention timestamp refreshed. The protection is scoped to that fallback dispatch and its expected per-unit destinations; genuine external orders, changed selections, takeover and casualty rules retain their invalidation behavior.

Watched/next/global debug output adds concise `[Formation layout]` failure records with the unit, desired/projected slot, snap displacement and, for clearance failures, the second unit/slot and actual versus required separation. Merely having an aim target no longer bypasses speed caps or classifies a member as obstructed. Native path pauses, engagement holds, stuck/move-problem flags, traversal exemptions and existing pacing deadlines remain unchanged.

## Aircraft behind mixed formations

Aircraft rows start horizontally 250m behind the rear edge of the ground footprint, spanning the ground formation's width. Additional rows extend farther back when physical clearance requires them. Native flight altitude, movement mode and navigation graphs remain authoritative. Air-only selections retain their own anchor.

Projection cannot move a rear slot ahead of the required rear boundary or outside the lateral span. If no safe valid point exists, that aircraft keeps its previous native order rather than receiving a front-of-formation fallback; watched/global diagnostics identify it. Such an unresolved member can block a group waypoint.

## Shared queued Move/Reverse waypoints

Each newly issued commander group Move/Reverse captures its commandable AI membership, including ground units and aircraft, independently of the cohesion switches. Native order completion is gated until every surviving member has reached its own destination within the native horizontal arrival threshold. Arrival is latched once reached. Native queues, requested modes, order IDs and synchronization remain in use; no repeated move-order issuer is added.

Early arrivals wait at the head waypoint. Death/despawn removes membership. Explicit replacement orders, takeover, team/processor changes and forced control relinquish that member to native ownership. A living member that is temporarily unable to move remains required. A native AI interruption does not count as arrival or remove a living member. A stuck member or rejected waypoint move blocks progression; no timeout silently skips it. Replace/cancel orders to change the instruction. Attack, Patrol, utility and FPS squad orders retain their native behavior.

Barrier checks use a 0.1s per-barrier cadence. Watched/global diagnostics report `[Waypoint #...]` waiting counts. Barriers clear on master disable, round/scene changes, commander/team loss and unload. Native combat may move a unit after its latched arrival; all members must have completed the waypoint, rather than occupy their slots simultaneously.

## Defense removal and validation

Both advanced friendly-target perimeter positioning and the one-shot ground-perimeter action are removed, including hooks, commands, menu controls and preference registration. Defend is native. Old saved defense keys are ignored; the live configuration was not edited. Removed source was backed up outside the canonical project. The schema-2 provider now exports 43 rows; provider method signatures are unchanged.

Dedicated-server Release build passed on 13 November 2026 with **0 warnings and 0 errors**, in 1.38 seconds. The DLL is staged, not installed. No tests, gameplay checks or server lifecycle actions were run. User-owned verification should cover the logged Alien drag/reuse failures and retry, aim versus actual engagement holds, rear aircraft placement near map boundaries, and mixed queued groups with early arrivals, deaths, rejected moves, stuck members and manual overrides. Compilation does not verify Harmony installation or native queue/flight behavior.
