# Si_Formation 3.6.1: partial selections reform

Moving only part of a cached ground formation now forgets the old layout and uses fresh standard commander placement. The mod's spacing, projection, ground/air separation and faction-specific cohesion remain active. A fresh group uses Arrival coordination rather than trying to hold old offsets during reformation. Subsequent full-group moves can preserve the newly created layout.

Cache mismatch is checked before single-unit or eligibility exits. Taking a single unit therefore invalidates the parent layout even though that unit needs no group cohesion. Adding uncached members, merging cached groups and selecting survivors of an older membership also reform. A whole unchanged ground cohort preserves its remembered layout; changing only aircraft selection does not count as a ground-membership change.

The entire touched cache loses its stored offsets and heading. An invalidation marker remains for unselected survivors until their next accepted layout or the existing retention/cleanup limit, so their next move also reforms rather than copying old gaps. That marker does not contain old positions. Active travel uses separate per-member offsets and order IDs: neither unselected current orders nor a queued segment's predecessor pacing is changed merely by invalidating the layout.

Debug creation traces report `membership changed: fresh formation`. No setting, permission, public API, client code, menu schema or protocol change is introduced. The existing isolated Harmony observer is retained.

The required Release build validates compilation only. No tests, installation or server/client start/stop are performed for this adjustment. Live evaluation should compare a complete group, a subset, the remaining survivors, a merged group and a queued subset, with both factions and mixed ground/air selections.

Release build on 11 November 2026 succeeded with zero warnings and zero errors using `dotnet build Si_Formation.csproj -c Release --ignore-failed-sources`. The 3.6.1 candidate is staged at `bin/Release/netstandard2.1/Si_Formation.dll`; the installed DLL is unchanged.
