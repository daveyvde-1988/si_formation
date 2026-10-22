# si_formation

Assembly and preferences section remain `Si_Formation`; .NET Standard 2.1, dedicated-server only.

## Small FPS Follow squads (2.14.2)

Selected custom Follow layouts now apply with one or two eligible AI followers as well as larger squads. The previous cutoff counted eligible AI followers, excluding the controlled leader, so it also affected three-unit groups containing the player and two AI. Zero eligible followers still release the Follow state. Native/default Follow selection, unavailable or blocked slots, player-controlled exclusions, and manual-order precedence retain their existing behavior. The separate small-selection fallback for FPS Move/Attack and commander formations is unchanged.

Owner live checks: select a visibly asymmetric Follow layout with one AI follower, add a second and a third, then dismiss back to two, one, and zero. Check the selected layout, locked/unlocked facing, manual orders, and default/native Follow selection. Compilation does not verify in-game movement.

## Previous Follow movement restored (2.14.1)

All Follow layouts, including formations named Convoy, again use the pre-2.14 moving-slot system: assigned JSON positions move with the leader, and native pathfinding takes each unit directly to its slot. The trail recorder, intermediate convoy waypoints, tail-only recruitment and preceding-vehicle bunching brake are removed from the runtime. There is no waiting for recorded trail. The experimental `convoy` JSON property is ignored; existing layouts require no edits.

`/followformation lock|unlock|status` and menu `/5` remain. Lock still coordinates AI travel speed across Move, ordinary Follow and Attack staging, and captures player facing at significant Follow updates. Follow refresh thresholds remain 2 m infantry / 15 m siege / 10 m other when locked, and 5 / 30 / 20 m unlocked. Player vehicle speed remains unrestricted. The near-side Attack staging changes, commander handover protection and FPS move marker fix remain.

Owner live checks: Convoy and other Follow layouts begin moving immediately; turns and obstacle recovery with lock off; lock/unlock and menu closure; manual stop/regroup and individual orders; Move/Attack lock coordination. Live lock coordination needs further owner testing; compilation alone does not verify it.

## Trail convoys, FPS coordination and near-side attacks (2.14.0; trail behaviour superseded by 2.14.1)

The trail-specific behaviour below describes the withdrawn 2.14.0 implementation. Use the 2.14.1 section above for current Follow behaviour. Its FPS lock coordination and near-side Attack changes remain active.

Follow definitions named `Convoy` (case-insensitive) now follow a recorded leader trail. Any Follow JSON can opt in with the formation-level boolean `"convoy": true`; `false` explicitly opts out, including for the legacy name. Convoy is rejected on other functions. Existing JSON version 3 files remain compatible. Negative slot Z sets distance behind the leader in metres (minimum 20 m); slot X sets the lane offset along the local trail, irrespective of player facing and `directionSensitive`. Use distinct trailing Z values for a single file, or matching Z with separated X for multiple lanes. Colour, unit/type restrictions and footprint checks still determine which slots can be assigned. Editing the name to Convoy works with the existing editor; differently named layouts require the optional JSON property.

The leader trail samples at most every 0.1 seconds and records points after at least 1 m horizontal movement. It retains at most 8192 points / about 10 km; sampling never invents pre-start trail. Convoys assemble progressively as enough actual trail exists for each slot. Destinations advance through successive short route sections (5..12 m lookahead, stopping at bends over approximately 20 degrees), with a 0.25-second per-unit order rate limit and a shared 12-projection / approximately 2 ms motion budget per sample. Native navigation still computes the actual route. Slot assignment keeps the existing separate search budget. Teleports over 150 m between samples reset the trail; controlled-unit changes, default/selection changes and existing player/round cleanup discard it. New members seek slots behind the established tail; existing assignments stay fixed. A blocked slot receives up to three placement passes, with new leader travel allowing another search. Unassigned members retain native Follow while waiting for enough route or a valid slot.

Unlocked convoy members travel independently toward their route positions without deliberate speed matching or preceding-vehicle braking. `/followformation lock` (FPS root menu `/5 Lock formations`) additionally coordinates all three custom FPS functions: Move, ordinary/trail Follow and Attack staging. Faster AI members brake when ahead of their own slots; trailing AI can catch up within their native maximum speed. The travel reference subtracts each assigned slot before comparing longitudinal progress, using a median to limit the effect of outliers. A member over 25 m (or four footprint tolerances) behind for five seconds enters uncapped recovery and is excluded from the reference until it catches up. Locked convoys additionally slow members that approach the immediately preceding vehicle in the same lane. These are temporary AI speed ceilings, updated at most every 0.2 seconds; native lower limits remain authoritative. Player vehicles are never paced. This is not collision-free rigid-body formation driving.

Ordinary Follow retains its captured-facing behaviour when locked, with 2 m infantry / 15 m siege / 10 m other destination refresh thresholds; unlocked ordinary Follow retains 5 / 30 / 20 m. Convoy orientation always follows its trail rather than player facing. Unlock removes coordination caps immediately; lock mode also applies to already tracked ongoing FPS moves. Menu lock/unlock still closes the menu. Aircraft, player-driven units, manual-order precedence, group/recruitment state and commander movement are unchanged. Attack restoration removes staging caps before native combat takes over. Coordinated FPS Move waits for final slot assignment instead of making an initial uncached vanilla move.

Attack staging now orients the preferred part of a direction-sensitive asymmetric layout toward the approaching AI squad centre. Symmetric layouts keep the approach heading; world-fixed JSON geometry remains fixed. Allocation rejects destinations beyond the target's centre plane relative to the approach, and uses distance as a tie-breaker within the existing colour/preference matching. Thus farther-side slots are intentionally left unused, with no preliminary move through the target centre. Units without an accepted near-side slot receive a native target attack after the bounded search; accepted members retain the existing arrival/30-second restoration. Distance comparisons use straight-line proximity to navigation-projected slots; actual shortest obstacle routes remain the game's responsibility, and a full ring is not forcibly filled on the far side.

Owner live checks: Sol/Centauri Convoy around corners in both modes; startup with no trail, lane spacing, recruitment, a removed or stuck member, self-crossing routes and teleport reset; ordinary Move/Follow/Attack staging with mixed speeds and lock/unlock mid-travel; player vehicle remains unrestricted; attack from opposite sides of an asymmetric layout and a ring, blocked/unsupported staging slots, no pre-move through the enemy and normal combat restoration; manual orders, stop/regroup, takeover, commander handover and round cleanup. Build success does not verify multiplayer movement or physical braking.

## FPS Follow orientation menu toggle (2.13.3)

The FPS `/formation` root menu keeps `/1 Move`, `/2 Follow`, `/3 Attack`, `/4 Disband all` and adds `/5 Lock Follow orientation` or `/5 Unlock Follow orientation`, showing the current state. Selecting `/5` uses the same FPS squad-leader validation and behaviour as `/followformation lock|unlock`, then closes the menu. Successful direct lock/unlock commands also close it. Commander menus and formation-choice numbering are unchanged.

While Follow orientation lock is enabled, destination refresh thresholds are infantry 2 m, siege 15 m, other eligible ground units 10 m. Unlocking restores infantry 5 m, siege 30 m, other ground units 20 m. Both the shared anchor and individual follower checks use the selected thresholds. Facing is still captured only on anchor updates; looking around in place does not rotate the formation. Final stopped settling and bounded retries are unchanged. Verify `/5` closes the menu when locking and unlocking, and compare locked/unlocked refresh distances for each unit category.

## FPS squads survive commander handover (2.13.1)

The native `AIUnitHandler.Init(bool)` and `Deinit(bool)` both call team-wide task and group pool cleanup. Those routines exempt Sentry tasks/groups but do not exempt player-controlled groups, so switching between AI and human commanders disbands unrelated FPS squads on that team.

The mod replaces only those two cleanup calls in each unit-handler method with filtered cleanup: groups whose living leader is player-controlled, and tasks attached to any such group, survive. Remaining army tasks/groups still use the native instance pool methods and their replication, with the native Sentry exemptions preserved. Shared tasks remain available to FPS squads while other army groups detach. This protection is server-only and independent of custom formation selection and the formation master switch. Global round/scene pool cleanup, manual disband, recruitment limits, unit destruction, and individual removals are unchanged.

Owner live checks: AI-to-human and human-to-AI commander handover with several other FPS squads following, stopped and moving; retain members, slots, pending moves and the move diamond; verify AI army cleanup/reassignment and Sentry units; repeat with Default selected and formations disabled; manual disband, destruction and round reset still clean up normally. Compilation does not verify live handover behaviour.

## Moving FPS Follow and move marker (2.13.0)

Custom FPS Follow now moves assigned formation slots with the leader instead of releasing them to native Follow during travel. Destinations refresh after horizontal leader displacement from that follower's last requested anchor: infantry 5 m, siege 30 m, other eligible ground units 20 m. A quiet one-second sample (at most 0.2 m displacement) also settles residual movement greater than 1 m at the final anchor. Looking around in place does not trigger a refresh.

Orientation uses the direction from the eligible AI squad's centre toward the new leader anchor, like an ordinary group move, independently of player facing. Existing direction-sensitive reversal mirroring and world-fixed JSON layouts retain their semantics. Slots remain owned by the same units throughout travel; only new/unassigned members use the placement search. Refreshes project and validate each unit's own slot, with at most 12 refresh/departure steps per frame. Blocked refreshes retain the previous destination and slot, retry up to three times 1.5 seconds apart, and try again after further leader movement. Native pathfinding still computes routes; this does not guarantee a rigid shape through obstacles.

FPS squad leaders can use `/followformation lock` to capture player facing at each significant anchor update and hold it between updates. `/followformation unlock` returns to travel-based orientation; `/followformation status` reports the mode. Changes take effect at the next anchor update, so turning in place does not rotate the formation. The setting follows the personal team/role selection lifetime and resets each round. A world-fixed JSON layout remains world-fixed.

Recruitment, native FOLLOWING_LEADER flags, manual orders, player-control exclusions and cleanup remain in place. Default formations and units without an accepted slot retain native Follow as fallback. As of 2.14.2, selected custom Follow layouts also apply with one or two eligible AI followers.

FPS move diamonds disappeared because the custom MoveServer prefix skipped the native Explore group task and cleared `group.Task`. The game's `GroupVisualization.UpdateTaskMarkers` reads that task through `Unit.CurrentTask` and hides its marker when absent. Custom FPS moves now create the same native Explore task at the clicked position for normal network/UI replication. Its two native group-task repath sites are suppressed only for the original formation members, so it cannot replace their individual destinations or later manual overrides. Unsupported members keep native task handling. New group tasks, player cleanup and global cleanup release the marker ownership.

Owner live checks: infantry, mixed ground and siege squads travelling around corners while looking sideways; stopped final settling; stable slots after adding/removing members; blocked slots; individual manual move/stop, group stop/regroup and player takeover; lock mode captures facing on successive movement updates without rotating in place; unlock; FPS move diamond at the clicked point, cached/repeated moves and move-to-follow transitions; round/team/disconnect cleanup. Compilation alone does not verify game movement or client UI.

## Growing production rally shapes (2.12.0)

Admin Generic power: `/formation rallyfix on|off|status` (case-insensitive). On/off saves `Rallyfix` in `UserData/Formations_cfg/si_formation.cfg`, default `true`. The formation master switch also gates Rallyfix. Applies to newly produced AI ground units for AI and player commanders, independent of their selected formation. Aircraft and production tiers outside Units1..Units4 stay native.

Rallyfix allocates one position at a time, without colour tiers, unit/type preferences, cluster capacities or blocked-percentage tests. Sol fills nested squares (complete at 1, 4, 9, 16, 25... positions); Centauri fills axial hexagon rings (1, 7, 19, 37...); Alien fills circular rings with enough angular separation for the spacing. Incomplete outer shapes are intentional. Blocked candidates are skipped individually. Existing unit destinations stay fixed; only newly produced units use the next position. Vacated positions are released for occupancy but the shape cursor continues outward rather than reshuffling older units.

| Facility tier | Minimum centre spacing |
| --- | --- |
| Barracks / Units1 | 5 m |
| Light factory / Units2 | 15 m |
| Heavy factory / Units3 | Commander formation option 2 scale: installed Sol 30 m, Centauri 25 m, Alien 26 m (minimum 20 m; missing definition falls back to 30 m) |
| Ultra heavy factory / Units4 | 30 m |

Spacing is uniform per facility/area, increased when necessary to fit the largest physical bounds in its ground-unit production roster, including a 0.5 m radius margin. This reads measurements, not formation unit preferences. Shape follows faction regardless of the selected commander formation; ordinary formations are unchanged. Equal-spacing comparisons tolerate 5 cm of coordinate rounding.

Native default rally points are captured when structures initialize, including off-ramp defaults. With no explicit AI/player rally point, the anchor starts beyond the exit and every accepted automatic spot stays at least 75 m beyond the facility's horizontal physical bounds (plus the unit clearance radius). With an explicit rally point, that point is the stable shape anchor and the 75 m exclusion does not apply. External `MoveRallyPoint` calls establish a new area for future production; already produced units retain their captured area/destination. Returning to the captured native default, or zero, resumes automatic placement.

The factory's visible rally marker advances to a validated, reserved next position after each allocation. Mod-owned marker updates do not change the shape anchor. The next production claims that preview immediately, then revalidates it using its own navigation graph; rapid simultaneous production cannot claim the same preview twice. All facilities share destination and preview occupancy checks. Turning Rallyfix off restores the commander's original/default marker when the mod still owns it, clears pending work/reservations, and lets already issued movement finish. Turning it on does not rearrange old units.

Timed and instant production are observed before the native shared rally destination is applied. Native forced exits are preserved; an individual destination can be queued while a vehicle is still exiting, so the game applies that destination once the forced exit finishes. Barracks and facilities without exit transforms receive ordinary individual move orders. Searches check navigation projection (at most 1 m lateral snap), order eligibility, structures, construction sites, existing ground units, reserved destinations and preview spots. Full driving routes and final vehicle orientation are handled by the game. No parked unit is deliberately reissued an order to complete a shape.

Work yields across frames: up to eight continuation steps / approximately 2 ms per frame, with bounded occupancy chunks (a single engine call cannot be preempted). Each assignment and next-marker preview has at most 2048 candidates, with offsets limited to 500 m around its anchor. Failure restores the produced unit's captured native rally destination. A subsequent successful external order, player control, destruction, team changes or departure after arrival releases that unit's reservation. Global disable, round/scene cleanup and unload cancel pending work and restore owned markers.

All diagnostics use the Formation mod's existing `Debug` gate and bounded trace budget. `/formation debug on|off|status` toggles logging until the next round; CFG `Debug` supplies the round default. `/formation rallyfix status` reports assigned/pending units and the active faction shape and spacing.

Owner gameplay verification: Sol 4 then 9 units without earlier units moving; Centauri hexagons and Alien circles; native default/off-ramp rallies and the 75 m buffer; selected AI/player rallies and changes while units are exiting; large heavy/ultra units; blocked-position skipping; rapid/multiple factory production; manual orders/player takeover; off/on marker restoration and round reset. Compilation does not verify live movement.

## Player policy API (2.10.0)

`Si_Formation.FormationApi` provides owner-scoped FPS recruitment policies for rank mods. Call on the server main thread after `IsReady`; `GlobalEnabled` reports whether enforcement is active (including master-disable and fault state). Policies apply to native FPS recruitment and `AIGroup.AddUnit`, independent of a player's custom formation selections. Commander orders keep their separate existing capacity.

```csharp
RequestPlayerPolicy(ulong steamId, string owner, int limit,
    string[] allowedCategories, string[] allowedObjectNames, string[] deniedObjectNames);
TryGetPlayerPolicy(ulong steamId, out string owner, out int limit,
    out string[] allowedCategories, out string[] allowedObjectNames, out string[] deniedObjectNames);
ClearPlayerPolicy(ulong steamId, string owner);
ClearOwnerPolicies(string owner);
TryGetUnitDefinitions(out string[] objectNames, out string[] unitTypes, out string[] unitWeights);
GetNativeUnitCategories();
```

All mutating/read methods above except `GetNativeUnitCategories` return `ChangeResult`. Limits are **1..50 including the controlled leader**: limit **1** permits zero AI followers. A policy's limit takes precedence over the older unowned per-player limit API and configured defaults. Explicit admin player/match size overrides take precedence over both policy capacity and eligibility; the policy remains stored underneath. A policy request returning `AdminOverride` was stored successfully but is shadowed. A policy read returns `AdminOverride` for a shadowed policy. Existing API enum values are preserved; new values are `InvalidPolicy`, `OwnerConflict`, and `NoPolicy`.

Use a stable nonempty owner such as `Si_Ranks`. A different owner cannot replace or clear a policy. Reads and requests copy all arrays, so callers cannot mutate active policies afterward. Repeating the same owner, limit and array contents is idempotent and leaves active orders intact. Missing policies return `NoPolicy`. Clear methods also work for disconnected IDs and removing all records owned by a mod. Policies are cleared on round end/start, scene unload, disconnect and mod unload; callers should inspect/reapply their policies after lifecycle changes and clear their owner during their own unload.

Category rules use native `ObjectInfo.UnitType` and `UnitWeight`, for all factions: `Infantry`, `Tank:Light`, `Siege:Heavy`, or `*`. A type alone or `Type:*` accepts all weights of that type. `Type:Any` matches only definitions whose native weight is exactly the `Any` enum value. Names and categories are case-sensitive. Null categories permit all definitions; an empty array permits only explicit `allowedObjectNames`. Exact native `ObjectInfo.name` entries in `allowedObjectNames` allow exceptions; `deniedObjectNames` always win. Unknown native category/weight strings are rejected instead of guessed. Unit definitions without ObjectInfo fail closed under a policy. `TryGetUnitDefinitions` returns parallel arrays from initialized native unit definitions, without formation-editor overrides; it returns `NotReady` until `ObjectInfo.AllInitialized`. `GetNativeUnitCategories` returns available native enum combinations without needing game initialization.

Changing a policy immediately releases disallowed AI followers first, then excess followers through the existing native `RemoveUnit` cleanup. Periodic enforcement handles later changes. The controlled leader, player-controlled members and player-driven units are preserved. Release cannot automatically restore previously removed members if a later promotion allows them again.

Build validation: 2.10.0 compiled with zero warnings/errors. Gameplay verification is performed by the server owner: Private at limit 1, promotion/recruitment, all-faction category exceptions, demotion release, admin size precedence, reconnect/round reset and master-disable behavior.

## Commands (2.9.0)

Command names are case-insensitive. **Admin** means AdminMod Generic power. `/formation help` shows general commands, the caller's current role commands, and administrative commands only to admins. Numeric menu replies and `/back` are handled only while this mod owns the menu.

| Command | Who | Explanation |
| --- | --- | --- |
| `/formation` | Everyone | Open/close the menu for your team and role. FPS menu: Move, Follow, Attack, Disband all; commander menu: formations. |
| `/formation help` | Everyone | Show commands available to your role and permissions. |
| `/formation status` | Everyone | Global status and personal selections; commanders also see capacity, lock distance and pending action. |
| `/<number>` | Menu user | Select the displayed function or formation. Formation option 1 is Default/vanilla. |
| `/back` | Menu user | Go back, or close the top-level menu. |
| `/formation <number>` | Commander | Select a commander formation by its menu number. |
| `/formation <function> <number>` | FPS player | Select by number: function 1 = Move, 2 = Follow, 3 = Attack. |
| `/disband all` | FPS squad leader | Remove AI squad members; retain the leader and player-controlled units. Also available in the menu. |
| `/formation lock` | Commander | Arm one move using the current layout of at least 3 eligible AI ground units. Chat: `formation lock armed for one move; maximum <value> metres`. |
| `/formation unlock` | Commander | Release your explicit locks and cancel your pending lock/scout action. |
| `/scout` | Commander | Arm one scouting action; the next ordinary move supplies at least 5 eligible units, and its clicked destination is ignored. |
| `/formation on` / `/formation off` | Admin | Global master switch for this match; next round restores CFG state. |
| `/formationsizeglobal <1..50>` | Admin | Match-wide FPS squad limit, including the controlled leader; match-only. |
| `/formationsize "Player Name" <1..50>` | Admin | Set a specific connected player's FPS squad limit, including leader; match-only. |
| `/formationsizecommander <1..100>` | Admin | Save the eligible ground-unit cap per formation move; default **75**. No argument or `status` displays it. |
| `/Formationtraveldistance <0..10000>` | Admin | Save maximum locked centre travel in metres; default **1000**. No argument or `status` displays it. |
| `/formationTravel <0..10000>` | Admin | Compatibility alias for `/Formationtraveldistance`, including its status form. |
| `/Formationspeedcap on` / `/Formationspeedcap off` | Admin | Toggle the optional faster-unit ceiling near explicit-lock arrival for this match. Centre-based travel pacing remains active. |
| `/startdelaysoftlock <seconds>` | Admin | Interval between soft-lock batches; match-only. Default 0.2 seconds. |
| `/startdelayspeedlock <seconds>` | Admin | Interval between explicit-lock speed tiers, slowest first; match-only. Default 0.3 seconds. |
| `/startdelaylock <seconds>` | Admin | Interval between explicit-lock positional rows; match-only. Default 0. Requires speed-tier delay to be disabled. |
| `/startdelay lock <seconds>` | Admin | Alias for `/startdelaylock`. |
| `/formationTolerance <10..100>` | Commander or admin | Legacy compatibility setting; currently does not affect movement. |
| `/FormationFallof <10..90>` | Commander or admin | Legacy compatibility setting; currently does not affect movement. Spelling retains the original single final `f`. |

All three departure delays accept **0** to disable or **0.1..5** seconds. To experiment with positional lock rows, use `/startdelayspeedlock 0` before `/startdelaylock 0.3`. A positive speed-tier delay takes priority over positional rows.

Formation selection through `/moveformation`, `/followformation`, `/attackformation` and every `/commanderformation` form is removed. Version 2.13.0 reintroduces `/followformation` only for orientation lock/unlock/status. Use the formation menu or numbered shortcuts. Commander formation handling follows the global master switch automatically; selecting Default still means vanilla. FPS Default retains its existing personal-disable behaviour; choosing a custom formation enables it again.

## Mixed selections, capacity and lock range (2.9.0)

Ordinary commander moves split the selection: up to `CommanderOrderLimit` eligible AI ground units, in selection order, receive formation handling. Aircraft, player-controlled/unsupported objects and excess units keep the original vanilla destination. They do not count towards formation capacity or its centre, and their previous formation state is cleared. The same split applies to armed explicit locks. At most two eligible ground units remain vanilla. Other unselected groups retain their orders. The cap is per move selection, not a combined quota over all of a commander's active groups. FPS move/attack formations likewise allow aircraft to move natively while eligible ground squad members form up; FPS squad-size limits remain unchanged. Queued orders, commander attack orders and scouting retain their separate native/scouting behaviour.

`FormationTravelDistance` is now an active **maximum for explicit locks only**, measured horizontally from the selected eligible ground units' starting centre. A farther click is shortened along that direction to the configured distance; captured offsets remain relative to that shortened centre. Individual units' path lengths and final positions can differ because of offsets, terrain projection and obstacle avoidance. A value of 0 keeps the snapshot centre at its starting point; it does not mean unlimited. Aircraft and excess units still use the original clicked destination. Soft-lock moves are not range-limited. Changing the setting affects new locked moves, not snapshots already travelling.

Preferences live in `UserData/Formations_cfg/si_formation.cfg`, grouped by General, Capacity, Starting formations, Soft-lock departures, Explicit lock and Legacy compatibility. Your existing values are preserved, including commander capacity 75 and lock range 1000. The mod captures the CFG layout before loading preferences and merges saved values into that layout inside the MelonLoader file-write path. Headings, comment lines, grouping and unknown setting values survive ordinary and loader-wide saves; new keys are appended. If no CFG exists, the embedded example supplies the layout. This replaces the previous after-save formatting callback. Edit the layout while the server is stopped, then restart to capture it; edits made while running are not adopted as a new layout. Other preference files retain their normal serialization. The example CFG uses the same layout; its built-in FPS limit is 10 and its formation defaults are vanilla, while your local choices remain unchanged.

## Bundled formation JSON

`Defaults/silica-formations.json` is a source-controlled copy of this installation's `UserData/Formations_cfg/silica-formations.json`. Building embeds it in `Si_Formation.dll`, without depending on the original server path. On startup, an installation with no JSON files in `UserData/Formations_cfg` receives this bundled catalog as `silica-formations.json`. Any existing JSON catalog is left untouched, avoiding duplicate definitions and preserving custom formations. Update the repository copy before building to change future bundled defaults. Catalog loading remains startup-only.

## Commander lock and scouting details


- `/formation lock`: arms one nonqueued, position-only move for at least three AI-controlled movable ground units, including infantry, vehicles and aliens. Flying and player-controlled units remain excluded. It captures the selected units relative to their own centre and places that snapshot at the clicked destination. Arrival releases the snapshot; the next move uses ordinary soft-lock/custom formation handling (or native handling for Default) unless `/formation lock` is typed again. Complete selections retain their existing soft-lock slot cache during the explicit move. Partial selections discard their old assignments; unselected units retain their orders and remaining group. `/formation unlock` releases active snapshots and cancels the pending action.
- `/scout`: replaces pending lock with a single scouting action. The next ordinary move selection supplies scouts, ignoring the destination. Locked members are excluded even on denial. At least five eligible units are required; denial forwards the original move only to eligible unlocked units. Valid scouting uses terrain sectors, nearest-unit assignment, at most 13 navigation candidates within 300 m per scout, separation, and connected walkable navigation nodes. Ground units and native AIAirVehicleAgent flight navigation are supported. Units with restricted traversal tags are conservatively unsupported because a graph-area connectivity check alone cannot validate tag-restricted routes. No patrol/correction work remains after scout dispatch; failures and cap exclusions are reported.
- `/formationTolerance` and `/FormationFallof` retain legacy personal commander settings; admins outside the commander role can change defaults for new commander sessions this match. These two settings do not affect current movement. `/settings` is removed; formation numbering is unchanged.

Preferences in `[Si_Formation]` inside `UserData/Formations_cfg/si_formation.cfg`: `FormationTravelDistance = 1000`, `FormationPositionTolerance = 25`, `FormationFalloffSlowdown = 25`. Out-of-range values use built-in defaults. The existing `CommanderOrderLimit` is **75**, configurable 1..100, not the FPS `GroupLimit` maximum of 50. Formation membership is capped per move selection; scout dispatch uses the same numerical cap.

Locked groups use ordinary individual pathfinding within the configured maximum centre travel distance. Each eligible unit receives one move order to its captured offset at the destination; the snapshot is oriented to travel, with lateral mirroring on reversals as described below. There are no intermediate waypoints, continuous correction orders, shared acceleration, recovery slowdown or 110% boosts. Centre-based speed control is standard for hard locks, as described below. Native obstacle avoidance is retained. Snapshot locks and scouting exclusions still work as before.

`FormationSpeedCap = true` in `[Si_Formation]` enables the optional final-arrival speed cap by default. `/Formationspeedcap on|off` is an admin-only (Generic power), case-insensitive command, with no menu. It immediately changes final-arrival cap handling for all active locked groups for this match; round start restores the configuration default. It does not save to the shared configuration file.

During hard-lock travel, centre pacing controls speed limits for all members, including the slowest. Near final arrival, FormationSpeedCap optionally caps faster members to the slowest maximum. Caps use Unit.MoveSpeedLimit and respect lower native limits without changing speed/acceleration fields. Membership and maximum speeds are rechecked every 0.2 seconds. Arrival, unlock, disable, round end, disconnect, role/team changes, reassignment and manual replacement remove caps.

Only tolerance and fall-off remain inactive compatibility settings. Travel distance now limits the explicit-lock destination as described above. No other mod's configuration is changed.

Networking evidence: `NetworkLayer.ProcessMessage(remoteID,isServer)` identifies the sender; `StrategyNetworkManager.SyncMoveAttackOrder` reads the selected-object list and calls `StrategyMode.PerformMoveAttack(remote:true)`. Host orders use `Player.CurrentPlayer`. Client CTRL+number groups are never inspected. Internal orders use the existing generated-depth guard.

Menus: installed AdminMod has no shared ownership/cancellation API. Read-only, fail-closed reflection detects KoH `_buyStates` (non-Closed), Unit Balance `_menuStates` and `_statsStates`, keyed by player ID. Formation menus refuse to open while those menus are active; close `/buy` with `/buy close`, or the balance editor with `/b`. No foreign state is mutated. Input ownership is rechecked before every menu reply. HeavyTeleporter's list only accepts namespaced `/st` commands, so it does not own numeric chat replies. Our ownership is removed on exit, another command, disconnect, round reset or disable.

Validation: build `Si_Formation.csproj -c Release` with the server ServerRoot. `CoordinationChecks` checks the active speed-cap policy; `Verify-LocalApi.ps1` checks installed API metadata. In-game validation is still required: unequal-speed and tied-slowest groups, cap on/off during travel, destination layout, manual replacement and cleanup, scouting exclusions, existing JSON formations, and menu exclusivity. Compilation and policy checks do not prove live vehicle behavior.


## Soft locks and departures (2.6.1)

Custom commander soft-lock moves exclude human-controlled units and player-driven vehicles from formation placement, the formation centre and the capacity count. The remaining selection still forms up when it contains at least three supported AI units. Excluded player units retain native order handling, as with hard locks. With at most two remaining units, their formation state is cleared and movement stays native. Aircraft, unsupported objects and excess units use native movement without blocking the remaining eligible ground selection.

All custom commander, FPS move, attack-staging and Follow formations remember unit-to-slot assignments automatically. Option 1/Default stays native and creates no soft lock. On another move, existing members try their saved slots before the allocator scans alternatives; new members compete for free positions. Removing, destroying, reassigning or manually ordering a member removes only that member's cached assignment and pending work. The remaining members keep their slots. Team/role changes, disconnect, selecting Default, disabling and round reset clear the relevant state. Selecting only part of any existing group clears saved assignments for the whole new selection. Three or more selected units form a fresh custom group anchored at the clicked destination; an armed explicit lock instead captures their current layout around their own centre. One or two selected units clear their formation state and use vanilla movement before either lock can handle the request; an armed lock is consumed. A complete group (including newly added members) can still reuse its cache. Scouting keeps its separate eligibility and minimum-size rules. FPS move/attack and held Follow formations also leave groups of at most two AI members native.

Soft locks apply temporary departure caps only, as described below; they do not synchronize speed after departure. Soft-lock orders now target their final slots directly; the 10-metre staging step and its navigation check are removed. Rejected destinations are rescanned without disturbing successful reservations. Follow moves its saved slots with the leader (2.13.0) and moves directly into its slots when settling. See optimization pass below for uncached moves.

Soft departures sort current unit positions along the direction toward the destination, front first, and use batches of `ceil(unit count / 5)` units (approximately 20% each). There are at most five scheduled starts; the first starts immediately and the last is scheduled at most four intervals later. Explicit locks default to slowest-first speed tiers; physical-size-based rows are available only when speed-ordered delay is disabled. Cached moves schedule successfully planned members after the placement search completes; uncached moves depart using vanilla placement before scanning as described below. failures get at most three passes, 1.5 seconds apart. Waiting members retain their previous orders. A new order cancels their pending departure or formation replacement. Large groups remain subject to per-frame work budgets, so timing is frame-dependent. Explicit `/formation lock` retains its captured offsets and direct destination moves for one order only. Later ordinary moves restore soft-lock handling without its optional speed cap.

Standalone preferences (seconds between soft batches or explicit-lock rows; `0` disables, otherwise `0.1` through `5`):

```toml
[Si_Formation]
StartDelaySoftLock = 0.2
StartDelayLock = 0
StartDelaySpeedLock = 0.3
```

Departure commands require AdminMod Generic power: `/startdelaysoftlock 0.2`, `/startdelayspeedlock 0.3`, `/startdelay lock 0.3`, and the alias `/startdelaylock 0.3`. These change new departures for the current match without saving; the next round uses the configuration values. There is no soft-lock menu or enable switch.

`Install.ps1` backs up the deployed DLL and preferences, migrates only `[Si_Formation]` from `UserData/MelonPreferences.cfg` into `UserData/Formations_cfg/si_formation.cfg`, preserves existing values, and verifies the copied DLL. An existing standalone file takes precedence. Other mods' categories are preserved. Restart the server after installation; no other mods need changes.

Live verification is user-owned: repeated moves and added/lost members; one-member replacement while others wait; infantry/alien ground locks; partial selections of 1, 2 and 3+ units; a second move after one-shot lock; complete-group cache reuse; at most five soft departure batches; default/native commands; blocked final slots; both departure settings and zero; FPS Follow/attack restoration; existing explicit-lock speed-cap on/off. Compilation does not validate live movement.

## Editor and position assignment (2.5.0)

Open `silica-formation-editor/Silica-FormationTool/index.html`; the distributable is `silica-formation-editor.zip`. This is a local editor update, not a publication to GitHub Pages. All sidebar sections collapse. ZIP export contains formation JSON only; the PNG option was removed. Tactical fill creates left/right mirror pairs inside the selected shape, and reports reduced counts if space is insufficient. Auto backup positions adds yellow one grid interval and orange two intervals behind green dots, copying unit/type choices; it adds unrestricted red dots on the available side of green/yellow/orange dots. It skips occupied/out-of-grid positions and the 4096-position limit. Undo restores the pre-generation layout; automatic colour gradients are disabled by this action.

The editor and server use formation-specific categories, leaving native UnitType unchanged:

| Category | Changed/listed units |
| --- | --- |
| Anti-air | AA Truck, Flak Truck |
| Scout | Light Quad, Heavy Quad, Light Raider, Heavy Raider |
| Cavalry | Light Striker, Heavy Striker, Strike Tank, Assault Car, Scorpion |
| Tank | Hover Tank, Railgun Tank, Combat Tank, Heavy Tank, Behemoth |
| Siege | Siege Tank, Crimson Tank, Goliath |
| Artillery | Barrage Truck, Rocket Truck |
| Special | Pulse Truck, Pyro Tank |
| Transport | Platoon Hauler, Squad Transport |
| Repair | Repair Rig, Repair Truck |

Unlisted units keep their native category (including infantry, aircraft and existing transports). Harvester is removed from the selector; legacy Harvester preferences are removed on import/load. Review old category-based layouts: the same category names now use these memberships.

Physical footprints: after the game initializes ObjectInfo definitions, the server exports cached local `PhysicalBounds.size.x/z` once to `UserData/Formations_cfg/metadata/footprints.json`. Use **Load game footprints** in the editor; measurements are cached in browser storage when available. Specific units use their measured dimensions; multiple/type preferences use the maximum width and maximum length of all preferred units. Unrestricted slots, or preferences with missing measurements, use a clearly labelled dashed **generic 8 × 4 m** rectangle. The editor does not invent measured data. Width/length on the grid equal metres divided by metres per position, and redraw immediately when scale or preferences change. Rectangles are approximate local physical bounds, not exact collision meshes; dots remain position markers. Real measurements require a game initialization; offline checks use sample measurements only.

Move, commander, attack-staging and Follow use the same staged assignment search. Each complete pass exhausts green before yellow, orange and red (legacy numeric 3 and numeric 4 both mean red). Within a colour, maximum matching considers the whole pending group, then favours explicit unit matches, category matches, unrestricted positions, and nonmatching fallback. Navigation rejection is per unit/slot; physical spacing reserves a conservative horizontal bounds radius plus 0.5 m. Successful slot reservations stay fixed during retries. A failed order frees that candidate for rescanning. A position may remain empty for concrete navigation, size/spacing or eligibility reasons, never because of the old first-six/128/256 search cutoff.

Navigation validation uses the existing unit graph-mask projection and CanIssueOrder APIs; native movement still finds the actual route. Flying/player-controlled/unsupported units remain excluded from formation placement while eligible ground members can form up. Each job yields after at most one terrain projection; matching also yields in bounded chunks. Ordinary and Follow schedulers each share up to 12 projection/order steps and 1024 cheap continuation steps / approximately 2 ms per frame between their jobs (a single engine call cannot be preempted). Very large layouts can take longer to assign. Unassigned units retain their previous order/native Follow during search. Up to three complete passes retry failures, with 1.5 seconds between passes; passes are not colour tiers. Attack arrival/30-second restoration timing begins after the final formation orders have been issued. Manual orders, disconnect, role/team changes and disabling still cancel pending work. Explicit snapshot locks do not use this allocator and use their captured offsets and standard centre-based pacing.

Focused checks: `dotnet run --project PriorityChecks/PriorityChecks.csproj -c Release` links the production allocator and definition code with navigation/unit stand-ins. It covers the 18-unit acceptance example, preference ordering, augmenting assignments, per-unit rejection, late colours, issue failure, saved assignments, cancellation, physical spacing and legacy red. `node PriorityChecks/editor-checks.cjs` checks editor logic and syntax. These do not validate Unity navigation, live multiplayer behavior or visual browser rendering.

## Departure pacing, centre coordination and exclusive dots (2.7.0)

Soft-lock moves, attack staging and held Follow departures temporarily cap departed members to the slowest participating unit's `TopSpeed`. After each complete batch receives its first move order (vanilla placement for an uncached move, final slot for a cached move), earlier batches may accelerate toward their own maximum: baseline + 0%, 25%, 50%, 75% of their extra speed for five batches. The final successful dispatch immediately removes the departure caps, without waiting for physical movement or arrival. Fewer stages spread the progression evenly. A zero departure delay or single batch adds no cap. Equal-speed units are unaffected. Native lower limits still win.

Failed/cancelled/dead/manually replaced members are excluded from the current departure cohort so they cannot hold its caps indefinitely. A later placement retry can start a new cohort. Completion refers to the remaining successfully planned members, not unreachable units. Caps are cleared on replacement, owner/role/team invalidation, disable and round cleanup. A speed ceiling does not equalize acceleration or guarantee collision-free starts.

Centre-based pacing is now always active for explicit /formation lock moves. The /formation test command has been removed. The slowest maximum supplies the baseline; ahead-of-slot members brake, and behind-slot members catch up only to their own maximum (no 110% boost). All units, including the slowest, can be slowed when ahead. Native lower limits still win. Soft locks retain only their temporary departure caps.

The reference is the mean of member positions minus their captured slot offsets, avoiding a membership-change shift in the snapshot origin. Signed progress is measured along the start-to-destination direction, with a size-based deadband. Lateral distance alone never triggers catch-up. Units near their final destination are left to ordinary arrival and speed-cap handling. This is experimental longitudinal speed coordination: destinations and native paths stay fixed; it does not steer units sideways back into a moving formation or guarantee cohesion around obstacles. It does not reactivate legacy tolerance/fall-off recovery; the travel-distance setting limits the destination centre separately.

Editor menu option `/1 default` now says **reserved for Vanilla**. **Exclusive — selected units or types** in the shared Unit and type exclusivity section applies to all selected dots, with a mixed-selection checkbox state. When checked, a unit must match a selected individual model OR a selected unit type to occupy the dot. An exclusive dot with neither stays empty. Other units cannot use it as a fallback, including through cached assignments. Unchecked dots retain ordinary preference ranking. `forceExclusive` is an optional boolean in version-3 JSON, defaulting to false in older files. It survives export/import, copying and yellow/orange backups; generated red backups remain unrestricted. Clear all preferences also clears exclusivity. Use the updated mod with exclusive JSON; older builds ignore this field.

Live checks: slowest-first hard-lock launch including tied speeds; speed-delay zero and positional-delay priority; repeated 180-degree reversals without lateral crossing in soft locks and rearmed hard locks; partial selection and manual replacement; centre pacing and arrival; /formation status retained while removed aliases are unavailable; exclusive models/types separately and together, and JSON round-trip. Live testing remains user-owned.
## Reversals and hard-lock departures (2.7.0)

Reversals greater than 150 degrees mirror only the local left/right offset, leaving the local front/back offset unchanged before applying the new heading. At 180 degrees this keeps units on the same physical side instead of sending both flanks through the centre. Direction-sensitive soft-lock groups remember their heading and mirror parity across complete-group moves; the whole candidate layout uses the same parity, including fallback positions, while slot preferences/exclusivity stay attached to their dots. Fixed world layouts remain fixed. Moving Follow layouts apply the same rule between anchor-update headings (2.13.0).

Hard locks remain one-move snapshots. Rearming captures current positions, using the previous successful hard-lock heading for members where available (otherwise their current facing), then orients the snapshot to the new travel direction and mirrors laterally on a reversal. Only heading history survives arrival; no extra move is automatically hard-locked. Manual replacement, unlock, owner/team/role changes, disable and round cleanup discard that history.

StartDelaySpeedLock defaults to 0.3 seconds. /startdelayspeedlock <seconds> changes the interval for new hard locks this match; 0 disables, otherwise 0.1..5. Units are sorted by TopSpeed, slowest first; speeds within 0.001 m/s of a tier's first member share a launch time. Each faster tier starts one interval later. One speed tier starts immediately. These are scheduled order times, not waits for physical movement.

StartDelayLock defaults to 0 (positional departures disabled). A positive speed delay always takes priority if both configuration values are positive. Enabling speed delay clears the positional match override; attempts to enable positional delay while speed delay is active are rejected with an explanation. To use positional rows, first issue /startdelayspeedlock 0, then /startdelay lock <seconds>. Disabling speed mode does not resurrect a previously suppressed positional delay. Round start restores configuration values. This installation sets StartDelayLock = 0 and StartDelaySpeedLock = 0.3 while retaining other preferences.

The redundant /formationstatus and /formations (including /formations status) commands are no longer registered. /formation status retains its existing behaviour. /formation test is removed; centre pacing is part of /formation lock.
## optimization pass

Version 2.8.0 adds a two-stage first move for custom commander moves, FPS moves and attack staging when the selected group has no reusable cached slots. The destination and formation heading are captured with the request. Units first receive actual native StrategyMode placement orders in the existing front-to-back batches (at most five, with the configured soft delay and departure pacing). Native placement is calculated separately for each batch; this can temporarily overlap batches near a close destination. The scheduler budgets native dispatches, but a single native batch call cannot be preempted.

Only after the final surviving batch has been dispatched does the custom slot search begin. Units continue their vanilla orders while searching. Confirmed assignments are redirected straight to their final formation slots, without a second departure delay, and cached. Unassigned units keep their initial native order (or their previous order if native dispatch failed). Subsequent cached moves skip the initial vanilla stage and retain the configured normal soft departure behaviour. New manual orders, partial replacements, destruction, reassignment, disconnect, role/team changes, Default, disable and round reset cancel affected outstanding work. The internal first order updates the pending order IDs, so it is not mistaken for a player replacement.

The 10-metre approach is removed throughout soft-lock placement, including held Follow. Follow uses individual slot orders during travel (2.13.0), with no extra preliminary StrategyMode move. Explicit locks and their centre/speed coordination remain unchanged.

A request-local cache lazily stores each unit's identity/category and footprint radius, each slot's colour and transformed world position, and allowed unit/slot combinations plus preference ranks. Ordinary placement retries reuse this cache. Follow search passes use a fresh cache for their captured anchor. Navigation projections, CanIssueOrder checks, live spacing reservations and order ownership are not cached across passes or shared between different units. Colour priority, exclusive slot restrictions, full matching, per-frame search budgets and 1.5-second retries remain intact; this pass does not implement tentative-assignment matching or remove native pathfinding work.

Movement-triggered status, denial and placement-failure messages are silent in chat (bounded Debug tracing remains available). Opening the formation menu still displays its choices; `/formation lock` acknowledges only `formation lock armed for one move; maximum <value> metres`. Explicit status/configuration commands retain their requested responses. No new configuration options are required.

User live checks: uncached batches begin before scanning; cached repeat skips the initial vanilla move; no 10-metre approach; final destinations and attack restoration; cancellation between stages and during batch waits; a blocked/unassigned unit retains its native order; partial selections and 1–2-unit vanilla handling; Follow and one-shot explicit locks; quiet move orders. The additional first native route is a responsiveness tradeoff, not a claim of lower total pathfinding cost. Compilation alone does not verify game behaviour.

All mod-owned server logging (including startup, status, warnings and errors) requires the existing `Debug` setting to be enabled. Player chat replies are unaffected. MelonLoader itself may still print mod loading messages.

## Client Formation panel API (2.15.0)

The optional Squad HUD 0.2.0 companion uses `FormationApi.GetPlayerPanelPage(ulong steamId, string function, int page)` and `RequestPlayerPanelAction(ulong steamId, string function, int option, string action, string revision)`. Both require the server main thread and resolve a connected player. Catalog pages contain six choices, current state, leader eligibility and a revision over faction/function/catalog plus a round epoch. Revisions reset on round start. The companion authenticates the remote player before calling these APIs; these methods do not authenticate a remote connection themselves.

Read returns a dictionary with page/pages/numbers/names/selected/locked/enabled/revision/leader, or an error. Actions validate role, revision and option before invoking existing selection, Follow facing-lock and AI-dismissal handlers. Lock/dismiss require an FPS leader. No admin/global settings are exposed. Choosing Native disables all personal FPS formations, as the chat option already does; selecting a custom option enables personal formations with retained function selections. This version adds the panel API without changing placement algorithms. Server-only clients and existing chat commands remain supported. See the [panel testing guide](../../ClientMods/Si_SquadHUD/TESTING.md); live verification remains user-owned.

## Commander panel lock (Formation 2.15.1)

GetPlayerPanelPage now advertises commanderLock=true for commander scope. Its locked value means a pending snapshot lock or an owned, same-team active locked group for commanders; FPS retains FollowFacingLock. RequestPlayerPanelAction accepts action=lock, option=1/0 for commander scope and invokes existing LockCommand lock/unlock after thread/player/role/revision/service validation. No movement algorithms or permanent locking mode change. The HUD 0.4.2 companion adds a distinct UI row after assembling the catalog; it does not reserve a formation menu number. Verify armed/active/arrival/unlock status in game; compilation is not live verification.

## Optional ModUI integration (2.15.2)

ModUIData.cs and ModUI_Export.cs supply shared settings and authenticated player-specific choices to Si_ServerModUI 2.0.0, without a UI assembly dependency. Selection commands carry PanelRevision and use ApiPanelAction for role/team/stale-catalog checks. /formation enabled on|off is a thin master alias; /formation close|exit and active-menu /close, /exit, /0 use common menu cleanup. Ownership probing recognizes Si_Buy as well as legacy KoH. Native movement and Follow remain unchanged. Built, not installed; user-owned live checks remain required.
