# Si_Formation 3.9.1 — infantry movement during Reverse

Infantry has no native Reverse receiver. The previous implementation admitted only reverse-capable vehicles, so soldiers accompanying a Reverse command could remain on their old orders instead of joining the formation.

Version 3.9.1 includes eligible infantry in the translated snapshot. Vehicles retain native Reverse orders; soldiers receive ordinary native Move orders to their assigned slots, with normal turning and walking/running. Both native dispatch paths are covered. The fallback retains the requested speed mode and queue flag, uses predecessor Move/Reverse destinations for queued snapshots, and tracks the accepted order type per member. A mixed group can therefore coordinate vehicle backing with infantry movement without requiring backward-walking infantry animations.

The infantry Move is issued once after native batch dispatch. The captured order processor and every queued order ID must still match preparation; an intervening replacement or queued append wins. Player-controlled, embarked, unsupported or navigation-unready units remain excluded. Existing projection, clearance and selection limits remain in force. Partial native acceptance releases strict pacing while preserving accepted native orders. A single eligible soldier can receive its Move without starting a multi-unit controller.

With faction cohesion OFF, eligible infantry still receive their Move fallback; vehicle destinations and pacing remain native. The Formation master switch controls all custom operation. This server change does not expose a Reverse command-card button for an infantry-only selection or lock soldier facing.

No new configuration, public provider API, client dependency or repeated order issuer is introduced. The dedicated-server Release build passed on 13 November 2026 with **0 warnings and 0 errors**, in 6.32 seconds. The DLL is staged, not installed. No tests, live gameplay checks or game/server lifecycle actions were performed. Mixed-group movement, queued fallback, single infantry movement and external-order precedence remain user-owned live checks.
