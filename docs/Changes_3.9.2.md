# Si_Formation 3.9.2 — nearby-destination rule switch

The Formation control panel now exposes **Commander placement → Nearby destination cohesion**, default **OFF**. This single server switch controls the nearby-destination fallback for both Human and Alien units. AdminMod Generic command: `/destinationcohesion on|off|status`. It saves as `DestinationLocalCohesion` in the existing Formation preference category/file.

Missing settings default OFF even when existing `CohesionDestinationRadius` and `AlienCohesionDestinationRadius` values are positive. The radius controls retain their values and 700m defaults. When ON, the existing fallback can coordinate selected ground units near the destination of a scattered or oversized selection. A faction radius of 0 still disables its fallback.

Turning OFF releases active and queued destination-local pacing immediately, preserving native orders and their destinations. Compact-group cohesion, its faction switches and ordinary distance profiles retain their existing behavior. No new update loop, client dependency or provider schema version is added; the schema-2 menu now contains 45 rows.

Release build passed on 13 November 2026 with **0 warnings and 0 errors**, in 2.42 seconds. The DLL is staged, not installed. No tests, live gameplay checks or game/server lifecycle actions were performed. Control-panel display, OFF/ON behavior for both factions, active/queued release and restart persistence remain user-owned live checks.
