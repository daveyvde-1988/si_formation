# How Formation shares status with ModUI

This describes the local Si_ServerModUI 2.0.0 and Si_ClientModUI integration. Si_Formation 2.15.4 uses standard exports only; its custom status compatibility hook has been removed.

## The two sources of status values

Client-side mods can register status definitions through `ClientModControls`. Each definition supplies an ID, label and a callback that reads its current value; optional role/admin restrictions control whether the local picker lists it. These values are read on the player's own client.

Server-side mods publish JSON. They do not draw directly into the client's status window and do not require a compile-time reference to the UI assembly. Si_ServerModUI finds their export methods by reflection and packages their information into its authenticated, per-recipient network frames. Si_ClientModUI reads the received catalog and offers the supplied fields in its status picker.

## Shared server exports

`public static string ModUIData()` returns the mod's shared document. Formation uses these fields:

| Field | Purpose |
| --- | --- |
| `info` | Short explanatory lines displayed above the commands; the client also offers them as summary status parameters. |
| `stats` | Read-only status rows, each containing `id`, `label`, `value` and `group`. |
| `commands` | Actions and controls, including descriptions, values, choices, presentation hints and permission information. |

For example, the shared Formation document contains a `master` status row whose value is `ON` or `OFF`. This is appropriate as shared information because the service state is the same for all recipients.

The server starts with the registered AdminMod commands and decorates or extends them using the mod's exported command metadata. Shared command entries are consumed into the frame's command list; the remaining document becomes that mod's `data`. Shared frames are cached by admin status and powers, with a two-second cache lifetime. Consequently, `ModUIData()` must not put an individual player's settings into the shared document.

## Personal exports already work for command controls

`public static string ModUIPlayerData(ulong steamId)` supplies information for one recipient. ModUI obtains this Steam ID from the actual connected player; it is not a player ID supplied by the client in the export request.

The current server consumes two parts of this personal document:

1. `v`: a mapping from command text to the player's current value. It replaces the value on that command's control.
2. `commands`: personal controls and choices under a command root already published by the mod. Entries inherit the parent command's access restrictions; the server also applies its final permission/role filter.

Formation uses the second path to provide the correct faction/role's dropdown choices and highlight the player's selected option. Selection commands still go through Formation's existing player, role, team and catalog-revision checks when executed.

This means ModUI **does allow personal information and personal choices in command controls**.

## Why personal status fields were missing

The current `Frame.Personalize` implementation reads `commands` and `v`, but has no code to merge a personal `stats` array into the recipient's mod `data`. The client status picker, meanwhile, reads server status rows only from that received `data.stats` array. A field left solely in the personal export never reaches that array.

For example, this personal export expresses a valid desired status row:

```json
{
  "stats": [
    {
      "id": "group-limit",
      "label": "Allowed group size",
      "value": "10 including leader",
      "group": "My formations"
    }
  ]
}
```

The unmodified server ignores this `stats` field. Changing a personal dropdown's current `v` cannot fix it: command values and status rows are read from different parts of the received frame.

The code shows a missing personal-data merge step, rather than an explicit prohibition on personal information. It does not establish why the original implementation omitted that step. Putting each player's value into the shared export would be incorrect because recipients share cached frames.

## Formation's current integration

Formation publishes its personal status fields through `ModUIPlayerData`, alongside its existing formation controls. It does not patch ModUI's frame builder or supply a separate status delivery channel.

The rows are `group-limit`, `formation-move`, `formation-follow`, `formation-attack` and `formation-commander`; only the applicable role's formation rows are supplied. The group-size row uses Formation's existing effective FPS limit calculation or the commander selection cap. A selected custom formation that is currently inactive is labelled accordingly.

The client understands status rows once they reach the received mod's `data.stats`. With the custom hook removed, the current server ignores Formation's personal `stats`, so these personal fields do not reach the status picker. Native personal-status support in ModUI is required to display them. The shared Formation service status remains available, and personal formation dropdowns continue using the supported command path.

## Compatibility and verification

There is no custom ModUI status patch, reflection adapter or hook-installation maintenance tick in Formation. Its standard exports have no hard UI assembly dependency, and ordinary server-only/chat usage remains supported.

Native support would mean merging personal `stats` into a cloned recipient's `data` inside ModUI itself; the shared cache must remain untouched. No such ModUI change is included here.

Live verification belongs to the server owner: confirm shared status, personal formation controls, role/team changes and admin-only commands. A successful build does not validate in-game network/UI behaviour.

Implementation references: Formation's `ModUIData.cs` and `ModUI_Export.cs`; the existing server's `ClientMods/Si_ServerModUI/Server/Frame.cs` and `ServerMod.cs`; and the existing client's `ClientMods/Si_ClientModUI/Client/OverlayWindows.cs` and `ClientModControls` API. The client/server paths are relative to the dedicated-server workspace root.
