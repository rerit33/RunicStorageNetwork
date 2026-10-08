# Changelog

## 1.0.1

- Fixed a building conflict with ValheimRAFT that could prevent new pieces from attaching to a ship. Improved compatibility with other mods that customize building placement.

## 1.0.0

- Experimental distant storage is now enabled by default for new configurations. Existing settings are kept.
- Added separate settings to enable or disable the Storage Codex, equippable Builder's Codex and Runic Gateway. Disabled content keeps its existing items and buildings, while its recipes and functions are unavailable.

- Fixed remote storage refusing chests when item mods initialize extra item data during loading.
- Crafting no longer waits for unrelated or unavailable chests before confirming materials from the selected sources.
- Fixed remote crafting refusing readable inventories after a save-format update or durability rounding. An unreadable chest no longer interrupts resource checks for other chests.
- Added the Runic Builder's Codex, crafted at the black forge. Bind it to a core and equip it to build using that network's materials within 50 m of its core or connected relays, or the configured relay link range.
- Each builder's book remembers its own network. Use it on another core to change the binding; renaming the network keeps the connection.
- The equipped Builder's Codex hangs from a leather belt on the character's left hip.
- Added the Runic Gateway: name a pair of gateways to connect distant parts of the same storage network. Requires experimental distant storage.
- Craft, build and retrieve items through gateways using the normal relay ranges. Only items allowed through ordinary portals can cross a gateway; local materials and ordinary relay routes remain available.
- Gateways show their connection state and dim when disconnected. Conflicting link names cannot combine separate networks.
- Gateway banners display their rune design on both sides.

## 0.8.8

- Added an experimental API for other mods to read network resources and consume materials for their machines. Requires experimental distant storage to be enabled.
- Machine requests automatically recheck the network and resource counts after temporary failures, and repeated requests do not consume materials twice.
- Added GitHub Wiki guides for mod settings and API integration, and shortened the README's experimental-feature notice.

## 0.8.7

- Fixed chests remaining or reappearing after destruction in multiplayer with experimental distant storage enabled, allowing building materials to be collected repeatedly.

## 0.8.6

- Fixed the storage window ignoring screen resolution and the game's **Scale GUI** setting. Thanks to [hoxton314](https://github.com/hoxton314) for the contribution.
- Added an experimental option to use connected storage outside the player's loaded area in single-player and multiplayer, including when entering a world far from the core.
- In experimental mode, distant resource counts update while the network is in use, including changes made by other players.
- The experimental option is disabled by default and may cause inventory errors. Back up your world before testing; changing the option requires a full restart of the game or dedicated server.

## 0.8.1

- Added the Runic Codex, crafted at a level 1 forge from silver, crystals, greydwarf eyes, linen thread and leather scraps.
- Added the Storage Codex, a book stand built with a hammer from a Runic Codex, fine wood, stone, iron and red jute. Place it within the supply range of a core or connected relay.
- Interact with a Storage Codex to search connected storage, choose an amount and retrieve items into your inventory.
- The storage window shows item counts, icons and descriptions. Items of different qualities appear separately, and retrieved items keep their original properties.
- Updated the core's network-renaming dialog to match the storage window.
- Updated core and relay icons with directional lighting and softer highlights. The mod icon now uses the updated core icon.

## 0.6.0

- Improved crafting-menu responsiveness on large storage networks by updating changed containers instead of repeatedly recounting every chest.
- Browsing recipes no longer reserves materials or holds containers for a craft. Reservations now begin when you press Craft.
- Selecting a recipe refreshes its ingredients, including newly added supplies in previously empty storage.
- Fixed ingredients disappearing from the crafting menu when a storage check is delayed or temporarily unavailable.
- Confirmed materials stay available during the crafting animation while background counts update.
- Fixed older storage replies overwriting newer resource counts.
- Reduced repeated recipe checks while keeping support for recipes changed during a session.

## 0.5.6

- Fixed network crafting choosing the wrong recipe when mods give several recipes the same internal name.
- Network crafting now follows recipes added, enabled or changed during a session.
- Changed recipes no longer use resource reservations prepared for their previous requirements.
- Added clearer log messages when a recipe cannot be matched between players.
- Serving trays can now place food using supplies from connected storage.
- Building now draws resources from the network with build tools added by other mods, instead of only the vanilla hammer.
- Added a `Building` configuration section to choose which build tools use the network. Changes apply without restarting the game, and in multiplayer the server decides.
- Building with enough materials in your inventory no longer depends on network availability.

Thanks to [hoxton314](https://github.com/hoxton314) for contributing support for additional build tools in [PR #2](https://github.com/rerit33/RunicStorageNetwork/pull/2).
Thanks to [hoxton314](https://github.com/hoxton314) for contributing unified recipe lookup in [PR #3](https://github.com/rerit33/RunicStorageNetwork/pull/3).

## 0.5.5

- Networks can now have an optional name, set by interacting with a core. Clear the name to leave the network unnamed.
- Connected containers now show their network in the hover text, including its name when set.
- Fixed trophies and other recipe ingredients being ignored when stored in connected containers.
- Items with upgrade levels or additional mod data can now be used from storage when required by a recipe.

## 0.5.4

- Added automatic support for eligible stationary storage containers from other mods.
- Added server-controlled settings to allow or exclude container types without restarting the game.
- Added diagnostic information about supported and excluded container types.

Thanks to [hoxton314](https://github.com/hoxton314) for contributing support for modded containers, configurable container rules and diagnostics in [PR #1](https://github.com/rerit33/RunicStorageNetwork/pull/1).

## 0.5.3

- Relay placement now shows every nearby connection with a path to a core. The nearest connection is highlighted; alternate links are thinner.
- Added a placement status showing whether a relay will connect. Relays can still be placed without a connection and linked later.
- Combined overlapping storage and supply circles when their ranges match, with clear range labels during placement.
- Simplified relay hover text by removing the number of links to the core. Storage counts now include barrels in their wording.
- Added incompatible mods and integration version requirements to the README, plus links for reporting bugs on GitHub.

## 0.5.2

- Fixed resources disappearing from crafting recipes when another ingredient was missing.
- Fixed ingredient counts showing only the reserved amount instead of the full available stock.
- Opening a crafting station now refreshes available resources for all recipes. Only the selected recipe reserves materials.
- Selecting a recipe refreshes its ingredient counts, including resources used by other players. Counts continue to refresh while the recipe is selected.
- Improved responsiveness when browsing recipes on large storage networks.
- Fixed resources temporarily disappearing from the overview when a chest was reserved or had locked slots in MultiUserChest.
- Fixed ingredient selection for recipes that accept alternative materials.

## 0.5.1

- Added material reservations for the selected recipe before enabling network crafting.
- Reservations now last through the crafting animation and are released when changing recipes, cancelling crafting or closing the menu.
- Improved checks for inventory space, including bonus crafting output.
- Added automatic recovery from temporary network failures and removed the on-screen synchronization message.
- Reduced the time spent starting new recovery attempts after clicking Craft from 45 to 8 seconds.
- Improved protection against duplicate resource use when network responses are delayed.

## 0.5.0

- Fixed crafting being rejected after reloading a world.
- Removed manual network selection on relays and network names from hover text. Cores and relays within connection range now join automatically.
- Added automatic recovery from outdated connections and resource counts during crafting.
- Improved recovery from lost network responses to reduce failed operations and prevent duplicate items.

## 0.4.2

- Fixed missing collision on the core's metal frame, crystal, top ornament, stone steps and crystal pedestal.

## 0.4.1

- Fixed the serving tray being unable to place food inside network coverage.
- Fixed network building support interfering with the hoe, cultivator and other tools with separate build menus.

## 0.4.0

- Removed the requirement for the host to be near a player's storage network for crafting, upgrades and building to work.
- Fixed relay connections depending on the host being nearby.
- Improved handling of simultaneous crafting from shared chests.
- Fixed temporary material reservations hiding a chest's contents from resource counts.

## 0.3.2

- Reduced FPS drops when looking at cores and relays, especially on large storage networks.

## 0.3.1

- Added support for vanilla barrels as resource storage for crafting, upgrades and building.

## 0.3.0

- Removed the 64-chest network limit.
- Improved crafting performance on large networks by reserving only chests needed for the current action.
- Improved chest selection to use fewer sources when resources are spread across many chests.

## 0.2.3

- Added English and Russian localization for building names, descriptions, hover text and messages, based on each player's game language.

## 0.2.2

- Fixed wood, stone and metal disappearing from build-menu icons.
- Improved icon edges and transparency.

## 0.2.1

- Updated both build-menu icons to match the buildings' new materials and appearance.
- Added the core as the mod icon and an English/Russian README.

## 0.2.0

- Added Runic Relays to extend storage network coverage through chains and branches, with placement guides showing nearby connections.
- Added manual relay network selection and alternate connections if a route is broken.
- Added settings for relay connection, storage and supply ranges.

## 0.1.5

- Restored the stone texture on the platform beneath the core crystal while keeping its surface smooth.

## 0.1.4

- Replaced floating runes with glowing engravings recessed into wood and stone.
- Smoothed the platform beneath the core crystal.

## 0.1.3

- Improved wood grain and stone texture placement, with more visible surface detail under lighting.

## 0.1.2

- Replaced the core's wood, stone and metal materials with native Valheim materials to improve their appearance and lighting.

## 0.1.1

- Improved core lighting and shadows.
- Added native building, destruction and hit sounds and effects.

## 0.1.0

- Added the Storage Network Core, allowing nearby chests to supply crafting, item upgrades and building.
