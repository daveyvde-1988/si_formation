# Si_Formation 3.6.0 candidate changes

This candidate turns Close-click formation snapshots toward travel, adds a settling phase, separates aircraft placement, and gives Humans and Aliens independent cohesion tuning. The full control reference is [Group cohesion](Group_Cohesion.md).

## Formation turns

Close single clicks rotate the captured or remembered ground layout toward the new travel direction. Medium/Long single clicks preserve its heading; dragged formations explicitly set a new layout and heading. Remembered ground membership and heading prevent temporary detours from becoming the next intended shape.

A Close click turning more than five degrees uses the new **Turning** phase when `TurnGrace` is positive. Its default is **6 seconds**, configurable independently per faction from **0 to 30 seconds**, with a menu step of **0.5 seconds**. Turning applies arrival pacing without the longitudinal hold cap; its custom no-progress timer waits until the allowance ends. A healthy settled shape then enters Holding. Otherwise it continues Arrival without trying to restore holding during that move. Native obstruction flags, losses, spread and other safety conditions may still release coordination. Zero disables the extra allowance and begins Holding immediately.

Final destinations are issued once. These changes do not repeatedly reissue Move orders or substitute new path waypoints while units turn.

## Independent Human and Alien settings

Each faction now has its own enable switch and **71 numeric cohesion controls**: 15 General values and 56 Close/Medium/Long endpoint values. These cover distance boundaries, pacing, timing, movement health, admission limits, diagnostics sampling/history and retention. Defaults initially match, but settings and loaded values remain separate.

| Scope | Switch command / cfg key | Tuning command / cfg prefix |
| --- | --- | --- |
| Human | `/groupcohesion` / `GroupCohesion` | `/cohesion` / `Cohesion<Key>` |
| Alien | `/aliengroupcohesion` / `AlienGroupCohesion` | `/aliencohesion` / `AlienCohesion<Key>` |

Both switches default OFF when absent. Existing Human configuration keys and saved values are retained. Alien values start from descriptor defaults, rather than copying loaded Human values. The cfg remains `UserData/cfg/Formation/si_formation_native.cfg`, category `[Si_Formation_Native]`.

Disabling one faction releases that faction's owned cohesion without disabling the other. `/cohesiondebug` remains shared and labels traces with Human/Alien, range and phase. Each group uses its own faction's sampling, watchdog, timing, history and retention values.

The default distance bands remain Close below **500m**, Medium from **500m through 1000m inclusive**, and Long above **1000m**. Long values interpolate to the **3000m** endpoint and remain there on longer moves. Long still provides light, bounded arrival coordination.

## Separate aircraft placement

Aircraft no longer set the ground formation's largest footprint, row/column counts, facing centre or ground cohesion admission/speed/casualty denominator. Mixed selections can coordinate eligible ground members while placing aircraft separately. Unsuitable ground movement still conservatively declines custom ground coordination.

Normal commander clicks and drags place aircraft in independent centered rows of up to three, starting over the ground formation's rear half and extending rearward. Pure-air selections receive their own layout. Short/degenerate drags receive the same separation through the native grid fallback. These placement changes apply while Formation is enabled even when both cohesion switches are OFF.

Aircraft use their own navigation graph and native preferred altitude. `FlyPreferredHeight` is not modified and takeoff is not forced. Body clearance is checked between accepted aircraft positions; ground and air layouts may overlap horizontally. Failed projections retain native placement. Flyer-capable aliens using native Stealth may remain on the ground, so that overlap remains a specific live check.

## Menu and compatibility

The provider exports **159 rows** arranged through the existing `ui.group` contract: General, Commander placement, FPS squads, Rally, Human General/Close/Medium/Long, Alien General/Close/Medium/Long, and Diagnostics. Compatible bridges can collapse these groups. Faction/range prefixes keep slider labels understandable on older bridges. No ClientModUI implementation change is included.

The existing Alien placement spacing control is shown under Alien General. `/alienspread` still scales Alien commander, FPS and rally placement independently of cohesion enablement.

Runtime identity remains `Si_Formation.dll`; schema-2 `ModUIData()` and `ModUIPlayerData(ulong)` signatures remain unchanged. No legacy FormationApi, optimizer bridge, client extension or new wire protocol is introduced. Native order/movement patch overlap and client presentation still need live verification.

## Validation and deployment state

The 3.6.0 Release build succeeded on 11 November 2026 with **zero warnings and zero errors**, using the repository's required command:

```powershell
dotnet build Si_Formation.csproj -c Release --ignore-failed-sources
```

Output path: `bin/Release/netstandard2.1/Si_Formation.dll`, built against dedicated-server assemblies. A successful build does not establish gameplay behavior. The candidate is staged only: the dedicated server was running, so its installed DLL was left unchanged. No tests, server start/stop or installation were performed.

Historical startup evidence: `MelonLoader/Latest.log` identifies **Si_Formation v3.5.1** at **18:21:30** and records **Formation tuning ready** at **18:21:45 on 11 November 2026**, followed by UI-provider registration. This confirms the previous startup correction reached initialization. It is not 3.6.0 deployment or gameplay evidence.

User-run live evaluation should cover Close turns and degradation, mixed speeds and obstacles, both factions' independent settings/switches, mixed/pure-air selections, short drags, native Stealth for flyer-capable aliens, replacement orders, queued activation, map-wide dispersion and menu grouping. Do not infer these checks passed from compilation or historical 3.5.1 startup.
