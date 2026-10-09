# si_formation

Assembly and preferences section remain `Si_Formation`; .NET Standard 2.1, dedicated-server only.

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

`/moveformation`, `/followformation`, `/attackformation` and every `/commanderformation` form are removed. Use the formation menu or numbered shortcuts. Commander formation handling follows the global master switch automatically; selecting Default still means vanilla. FPS Default retains its existing personal-disable behaviour; choosing a custom formation enables it again.

## Mixed selections, capacity and lock range (2.9.0)

Ordinary commander moves split the selection: up to `CommanderOrderLimit` eligible AI ground units, in selection order, receive formation handling. Aircraft, player-controlled/unsupported objects and excess units keep the original vanilla destination. They do not count towards formation capacity or its centre, and their previous formation state is cleared. The same split applies to armed explicit locks. At most two eligible ground units remain vanilla. Other unselected groups retain their orders. The cap is per move selection, not a combined quota over all of a commander's active groups. FPS move/attack formations likewise allow aircraft to move natively while eligible ground squad members form up; FPS squad-size limits remain unchanged. Queued orders, commander attack orders and scouting retain their separate native/scouting behaviour.

`FormationTravelDistance` is now an active **maximum for explicit locks only**, measured horizontally from the selected eligible ground units' starting centre. A farther click is shortened along that direction to the configured distance; captured offsets remain relative to that shortened centre. Individual units' path lengths and final positions can differ because of offsets, terrain projection and obstacle avoidance. A value of 0 keeps the snapshot centre at its starting point; it does not mean unlimited. Aircraft and excess units still use the original clicked destination. Soft-lock moves are not range-limited. Changing the setting affects new locked moves, not snapshots already travelling.

Preferences live in `UserData/Formations_cfg/si_formation.cfg`, grouped by General, Capacity, Starting formations, Soft-lock departures, Explicit lock and Legacy compatibility. Your existing values are preserved, including commander capacity 75 and lock range 1000. The mod reapplies this readable layout at startup and after its preference saves, retaining unknown setting values. The example CFG uses the same layout; its built-in FPS limit is 10 and its formation defaults are vanilla, while your local choices remain unchanged.

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

Soft locks apply temporary departure caps only, as described below; they do not synchronize speed after departure. Soft-lock orders now target their final slots directly; the 10-metre staging step and its navigation check are removed. Rejected destinations are rescanned without disturbing successful reservations. Follow preserves native moving-leader behaviour and moves directly into its slots when settling. See optimization pass below for uncached moves.

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

Reversals greater than 150 degrees mirror only the local left/right offset, leaving the local front/back offset unchanged before applying the new heading. At 180 degrees this keeps units on the same physical side instead of sending both flanks through the centre. Direction-sensitive soft-lock groups remember their heading and mirror parity across complete-group moves; the whole candidate layout uses the same parity, including fallback positions, while slot preferences/exclusivity stay attached to their dots. Fixed world layouts remain fixed. Held Follow layouts apply the same rule between settled headings; moving-leader Follow remains native.

Hard locks remain one-move snapshots. Rearming captures current positions, using the previous successful hard-lock heading for members where available (otherwise their current facing), then orients the snapshot to the new travel direction and mirrors laterally on a reversal. Only heading history survives arrival; no extra move is automatically hard-locked. Manual replacement, unlock, owner/team/role changes, disable and round cleanup discard that history.

StartDelaySpeedLock defaults to 0.3 seconds. /startdelayspeedlock <seconds> changes the interval for new hard locks this match; 0 disables, otherwise 0.1..5. Units are sorted by TopSpeed, slowest first; speeds within 0.001 m/s of a tier's first member share a launch time. Each faster tier starts one interval later. One speed tier starts immediately. These are scheduled order times, not waits for physical movement.

StartDelayLock defaults to 0 (positional departures disabled). A positive speed delay always takes priority if both configuration values are positive. Enabling speed delay clears the positional match override; attempts to enable positional delay while speed delay is active are rejected with an explanation. To use positional rows, first issue /startdelayspeedlock 0, then /startdelay lock <seconds>. Disabling speed mode does not resurrect a previously suppressed positional delay. Round start restores configuration values. This installation sets StartDelayLock = 0 and StartDelaySpeedLock = 0.3 while retaining other preferences.

The redundant /formationstatus and /formations (including /formations status) commands are no longer registered. /formation status retains its existing behaviour. /formation test is removed; centre pacing is part of /formation lock.
## optimization pass

Version 2.8.0 adds a two-stage first move for custom commander moves, FPS moves and attack staging when the selected group has no reusable cached slots. The destination and formation heading are captured with the request. Units first receive actual native StrategyMode placement orders in the existing front-to-back batches (at most five, with the configured soft delay and departure pacing). Native placement is calculated separately for each batch; this can temporarily overlap batches near a close destination. The scheduler budgets native dispatches, but a single native batch call cannot be preempted.

Only after the final surviving batch has been dispatched does the custom slot search begin. Units continue their vanilla orders while searching. Confirmed assignments are redirected straight to their final formation slots, without a second departure delay, and cached. Unassigned units keep their initial native order (or their previous order if native dispatch failed). Subsequent cached moves skip the initial vanilla stage and retain the configured normal soft departure behaviour. New manual orders, partial replacements, destruction, reassignment, disconnect, role/team changes, Default, disable and round reset cancel affected outstanding work. The internal first order updates the pending order IDs, so it is not mistaken for a player replacement.

The 10-metre approach is removed throughout soft-lock placement, including held Follow. Follow already travels natively with its leader, so no extra preliminary StrategyMode move is injected into Follow. Explicit locks and their centre/speed coordination remain unchanged.

A request-local cache lazily stores each unit's identity/category and footprint radius, each slot's colour and transformed world position, and allowed unit/slot combinations plus preference ranks. Ordinary placement retries reuse this cache. Follow search passes use a fresh cache for their captured anchor. Navigation projections, CanIssueOrder checks, live spacing reservations and order ownership are not cached across passes or shared between different units. Colour priority, exclusive slot restrictions, full matching, per-frame search budgets and 1.5-second retries remain intact; this pass does not implement tentative-assignment matching or remove native pathfinding work.

Movement-triggered status, denial and placement-failure messages are silent in chat (bounded Debug tracing remains available). Opening the formation menu still displays its choices; `/formation lock` acknowledges only `formation lock armed for one move; maximum <value> metres`. Explicit status/configuration commands retain their requested responses. No new configuration options are required.

User live checks: uncached batches begin before scanning; cached repeat skips the initial vanilla move; no 10-metre approach; final destinations and attack restoration; cancellation between stages and during batch waits; a blocked/unassigned unit retains its native order; partial selections and 1–2-unit vanilla handling; Follow and one-shot explicit locks; quiet move orders. The additional first native route is a responsiveness tradeoff, not a claim of lower total pathfinding cost. Compilation alone does not verify game behaviour.
