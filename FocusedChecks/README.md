# Focused checks

This separate net8.0 executable links production Si_Formation source and replaces game/Unity/Harmony services with lightweight doubles. It uses the installed Newtonsoft.Json assembly, not downloaded packages. Tests do not start the server or install a DLL.

Run from the mod project:

    dotnet run --project FocusedChecks/FocusedChecks.csproj -- "D:\SteamLibrary\steamapps\common\Silica Dedicated Server"

The disk-fixture check intentionally expects the supplied Formations.json (three team-specific Circle move definitions). If you later edit that fixture, update that expectation. Temporary malformed/duplicate JSON fixtures are created in the system temp directory and removed afterwards.

Production algorithms run unchanged; the fake order processor calls the real cancellation hook to exercise recursion guards. Actual Harmony patch installation, RPC transport, terrain sampling and navigation require the in-game checklist in ../README.md. The separate ../Verify-LocalApi.ps1 checks installed method metadata. The older ../Tests fixtures target the retired implementation and are not used.
