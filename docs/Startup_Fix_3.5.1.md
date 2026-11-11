# Si_Formation 3.5.1: startup correction

The 11 November 2026 server log shows that MelonLoader discovered Si_Formation 3.5.0, then initialization failed with `HarmonyException: IL Compile Error` / `InvalidProgramException` while patching `Silica.AI.AIOrderProcessor.IssueOrder`. Commands were registered after patch installation, so neither Formation commands nor the corresponding menu became available. The installed 3.5.0 DLL matched the built candidate; this was an initialization defect rather than a missing file.

The FPS and new cohesion prefix/postfix pairs both declared `__state` on `Si_Formation.Formations`, but their types differed: `bool` versus `CohesionOrderStamp`. The installed Harmony implementation allocates state locals by patch declaring type, so the pairs collided and produced invalid Mono IL. This was verified by inspecting the installed `MelonLoader/net35/0Harmony.dll`, in addition to the game log and mod source. Harmony's [injection documentation](https://harmony.pardeike.net/articles/patching-injections.html) describes the class-scoped prefix/postfix state mechanism.

The cohesion observer pair now belongs to its own nested `CohesionOrderObserver` class. Both ends retain the same snapshot type and calling semantics, while FPS keeps its existing boolean state. An explicit captured flag prevents a skipped snapshot prefix from being treated as a valid order observation. No range, pacing, configuration, command or public API behavior is changed by this correction.

Initialization now logs the original exception before attempting cleanup. Patch-cleanup failures are logged separately instead of masking the startup error; unload cleanup can continue after an unpatch failure.

Only the required Release build is used for compile validation. No gameplay tests or server/client start/stop actions accompany this correction. After loading the corrected build, the expected successful-startup line contains `Formation tuning ready`; `/formation status` should identify 3.5.1. Actual Harmony installation, command/menu availability and gameplay still require the next user-run server session.

The Release build succeeded with zero warnings and zero errors. With the dedicated server stopped, the installed 3.5.0 DLL was backed up under `Backups/Si_Formation-3.5.0-startup-failure-20261111T172003592Z` and replaced with the 3.5.1 build. The copied DLL's SHA-256 was verified as `952042B9B9E2CD14744152E81271BAE01B2580139A96BC99083A19BF310DCE56`. No configuration was edited and no process was started or stopped.
