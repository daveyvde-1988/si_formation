# Native Formation migration

On 11 November 2026, the native implementation for **Silica game 1.0** became the canonical source at `Silica-1283/si_formation`. Its mod version remains **3.4.4**; game compatibility and mod version are separate numbers.

## Folder and build references

The source moved from `Silica-1283/si_formation_native` to `Silica-1283/si_formation`, at the same directory depth. `Si_Formation.csproj` resolves the server through `..\..`, so its game, MelonLoader, Harmony and Admin Mod references remain valid. The project/assembly name and output stay `Si_Formation` / `Si_Formation.dll`.

The shared workspace and existing ModUI release script already target `si_formation`; no path change is needed there. Older developer notes linking removed legacy files describe the archived implementation. Generated `obj`/PDB files may still mention the former source path until the next requested build; they are ignored build artifacts.

No C# or project bytes, installed DLLs or live settings were changed for this migration. The existing native Release DLL and installed `Mods/Si_Formation.dll` matched SHA-256 `79D5C5D16458409382A170BCB8C041299A2F064CFD5DCD1EFC91F4B785FD17A3` when inspected. This is a binary identity check, not new gameplay validation or proof of a fresh source build.

## Existing configuration stays in use

Keep `UserData/cfg/Formation/si_formation_native.cfg` and its `[Si_Formation_Native]` category. These names are explicit runtime settings, not derived from the source folder name. Renaming them would stop the mod from reading the existing settings. The native implementation does not migrate old `si_formation.cfg`, client personal preferences or custom formation catalogs.

## Legacy preservation and GitHub history

The entire former 2.16.2 working tree is preserved locally at workspace-relative `Backups/Formation-legacy-2.16.2-20261111/si_formation`. It includes ignored build files, the top-level Git history and the separate nested `Si_Formation` Git repository. A hash inventory is stored beside it as `migration-manifest.json`.

The existing [GitHub repository](https://github.com/daveyvde-1988/si_formation) and its history are retained. The legacy checkpoint is commit `4d324a40d9c715a6a483e6ec286332018c7e2133`, preserved on [legacy/pre-native-1.0-20261111](https://github.com/daveyvde-1988/si_formation/tree/legacy/pre-native-1.0-20261111). The native replacement is a normal descendant commit on `main`; repository deletion and history rewriting are unnecessary.

Keep legacy files outside the active native project: the SDK's default C# glob includes subdirectories. An archive of old source inside this project could accidentally compile alongside the native implementation.

## Compatibility changes come from the implementation

- **Ranks:** native Formation does not provide `Si_Formation.FormationApi`. The inspected Ranks adapter cannot apply its legacy recruitment/category/squad policies without that API. Folder renaming does not restore it.
- **AlienOptimizer:** the legacy reflected placement and scout bridge is absent. The inspected consumer guards missing delegates and uses its corresponding native placement fallback; the old bridge contract is not supported here.
- **Formation Tool:** native Formation does not consume the legacy custom-blueprint JSON/catalog system.
- **ModUI:** `ModUIData()` still supplies schema-2 commands and settings. `ModUIPlayerData(ulong)` returns null; settings are server-wide, with no personal-controls restoration opt-in. Rich grouped sliders depend on a compatible bridge.
- **Loading:** use one Formation implementation at a time. Both produce `Si_Formation.dll` and register overlapping commands; native initialization rejects command collisions.

These differences already belong to native 3.4.4. The repository migration does not add an adapter, change their behavior or run the server.
