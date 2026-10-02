# Headless regression checks

Run `Run-Tests.cmd`, or run `dotnet run --project Tests.csproj -c Release` from this directory. .NET 8 SDK is required; no package dependencies are used.

The console executable compiles the actual `../FollowFormation.cs` alongside small controlled substitutes for Unity, game state, pathfinding, Harmony, and Si-AdminMod. These checks cover quiet-stop cadence, motion thresholds, stop-time heading, the eight formation offsets, projection failure fallback, native Follow suppression, lifecycle cleanup, admin access, and external-order handling. `AstarPath.Pending` stays empty because formation placement does not submit path probes. A blocked physics stub does not affect orders.

The stubs do not validate Unity, Harmony IL patching, network replication, or dedicated-server behavior.
