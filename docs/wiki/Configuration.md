# Configuration

For Runic Storage Network **1.0.0**. [Русская версия](https://github.com/rerit33/RunicStorageNetwork/wiki/Configuration-RU).

For each building's purpose, ranges and materials, see [Buildings, ranges and recipes](https://github.com/rerit33/RunicStorageNetwork/wiki/Buildings-and-recipes).

The configuration file is `BepInEx/config/local.runicstoragenetwork.cfg`. It is created after the mod first starts. With a mod manager, use the file inside the profile you actually play with.

`Network`, `Containers`, `Building` and `Content` settings are administrator-only in multiplayer and synchronized by Jötunn. The server decides these settings for the session. Changes applied through the configuration system take effect during play; editing a file on disk alone does not guarantee a live reload. Editing while the game/server is stopped and then starting it is the simplest way to apply file changes.

`Experimental` and `Diagnostics` settings are local to each installation. In particular, changing the experimental option only on the server does **not** enable it on clients.

## Experimental distant storage

> **New in 1.0:** Distant storage and the mod API are enabled by default for new configurations. To turn them off, set `ExperimentalUnloadedNetworks = false` and restart. Existing values are kept when updating.

```ini
[Experimental]
ExperimentalUnloadedNetworks = true
```

`ExperimentalUnloadedNetworks = true` allows connected networks and eligible storage outside a player's loaded area. If an older config has `false`, change it to `true` and restart to use this feature. This also enables the [API for other mods](https://github.com/rerit33/RunicStorageNetwork/wiki/API). Runic Gateway links additionally require `RunicGatewayEnabled = true`. Gateways use the relay's local storage, supply and connection ranges; only their paired distant link ignores distance. When disabled, placed gateways remain visible but do not supply resources or act as relays.

For multiplayer, install the same RSN version on the host or dedicated server and every client, then enable the option in **each** installation. Fully restart the game clients and host/server after changing it. To disable it, set `false` everywhere and restart. If the server has it disabled, clients use ordinary loaded-area networking and the experimental API is unavailable.

The feature uses saved network and inventory records; it does not keep the entire base's world area running. You do not need to visit the core first after joining. Discovery starts when the network is used, so the first request may need time to finish. This does not make unrelated machines or world simulation run while unloaded.

Vanilla and eligible modded storage use the same container filters. If an inventory cannot be read and saved safely in this mode, it is unavailable for distant access. Custom storage behavior needs individual testing. The new API has been tested in single-player near a core and with the core unloaded; its multiplayer integration remains under testing.

## Optional content

Content is available only when its switch is `true`. All three switches default to `true` and are controlled by the server in multiplayer.

```ini
[Content]
StorageCodexEnabled = true
BuildersCodexEnabled = true
RunicGatewayEnabled = true
```

| Setting | When set to `false` |
| --- | --- |
| `StorageCodexEnabled` | Hides the Storage Codex building recipe and disables its storage window and withdrawals. |
| `BuildersCodexEnabled` | Hides the equippable Builder's Codex recipe and disables binding and its extra building range. |
| `RunicGatewayEnabled` | Hides the Runic Gateway building recipe and removes gateways from network routes. |

Existing items, buildings and saved bindings are kept. Set the switch back to `true` to restore their functions. The ordinary Runic Codex crafting ingredient is unaffected. Only gateways also require experimental distant storage; the Storage Codex and Builder's Codex can use ordinary loaded-area networks.

## Network

All distances are in metres. Larger radii can include more nodes and chests and increase the work needed to inspect the network.

| Setting in `[Network]` | Default | Allowed values | Effect |
| --- | --- | --- | --- |
| `SupplyEnabled` | `true` | `true` / `false` | Enables network resource supply. Disabling it keeps the build pieces available, but stops new API payments as well. |
| `OwnerFastPath` | `true` | `true` / `false` | Builds immediately when every needed chest is loaded and owned by the building player's game. Set `false` to use normal network building. |
| `StorageRadius` | `20` | `1`–`100` | Core-to-storage connection radius. |
| `SupplyRadius` | `20` | `1`–`100` | Supply radius around a core for crafting, building and storage access. |
| `RelayLinkRange` | `50` | `1`–`100` | Maximum distance for links between network nodes. |
| `RelayStorageRadius` | `20` | `1`–`100` | Relay-to-storage connection radius. |
| `RelaySupplyRadius` | `20` | `1`–`100` | Supply radius around a connected relay. |
| `RescanIntervalSeconds` | `2` | `0.5`–`30` | Interval for ordinary network topology rescans. It is not a timer for every inventory update or API request. |

The Storage Codex uses the coverage of its core or relay; it does not extend it. No recipe changes are needed when changing ranges.

`OwnerFastPath` is administrator-only, on by default and synchronized by the server. It removes the required materials and finishes the build immediately, with normal build costs charged once. Builds use normal network payment if a needed chest is busy, reserved, unloaded, owned by another player's game or unavailable through a gateway. Pending material refunds or chest release block further builds by this player until they finish.

If a chest changes owner or an inventory changes in a way that prevents a refund, RSN releases the chest and logs a warning about the materials it could not restore. Leaving the world during a pending refund also logs the remaining materials before releasing the chests.

## Containers

| Setting in `[Containers]` | Default | Effect |
| --- | --- | --- |
| `AllowedContainers` | empty | Empty: connect every eligible container. Filled: connect only the listed prefab names. |
| `DeniedContainers` | `piece_trashcan` | Listed container prefabs are excluded. |
| `DeniedComponents` | See below | Exclude a container whose prefab has any listed component. |

The default `DeniedComponents` value is:

```text
Incinerator,Turret,Catapult,Vagon,Ship,ItemStand,ArmorStand,Smelter,CookingStation,Fermenter,Beehive,SapCollector
```

Use internal prefab/component names, not translated display names. Separate entries with commas. Whitespace and letter case are ignored. Exclusion wins over inclusion: a container in both lists stays excluded. These lists filter containers, **not resource categories**.

The allowlist cannot make an unsupported inventory compatible: storage still has to satisfy the mod's stationary, player-built, public-container requirements and access checks. Removing component exclusions does not add support for a machine's custom inventory logic. The defaults keep machines that consume or fire their contents out of the resource pool.

## Building tools

| Setting in `[Building]` | Default | Effect |
| --- | --- | --- |
| `AllowedBuildTools` | empty | Empty: any tool with a build menu can use the network. Filled: only the listed item prefabs can. |
| `DeniedBuildTools` | empty | Listed item prefabs never build from network supplies. |
| `DeniedPieceComponents` | `TerrainOp,TerrainModifier` | Pieces with a listed component use inventory materials only. |

Lists use the same name format and exclusion priority as container settings. The **equipped tool** must be allowed, even if it shares a menu with another tool. Terrain shaping is excluded by default; ordinary construction, planting and serving trays can use network resources.

Excluded actions still work with items in the player's inventory. When carried materials are sufficient, building does not wait for the network. Normal placement and station requirements still apply.

## Diagnostics and compatibility

`[Diagnostics] DebugLogging = false` is the default. Enable it when collecting detailed transaction diagnostics; it does not dump entire inventories.

The console command `rsn_status` (also `rsn status`) reports the network state and supported/excluded container types. The mod log lists their names. Use the log from the affected profile; for multiplayer problems include both the affected client and server logs when possible.

`SupplyEnabled = true` cannot override an incompatible-mod check. See the [compatibility section](https://github.com/rerit33/RunicStorageNetwork#incompatible-mods-and-integration-limits). With Quick Stack but without MultiUserChest, `AllowAreaStackingInMultiplayerWithoutMUC` must be disabled in **Quick Stack's own configuration**.
