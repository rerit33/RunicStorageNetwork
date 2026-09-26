# Runic Storage Network

[English](#english) | [Русский](#русский)

[Report a bug / Сообщить об ошибке](https://github.com/rerit33/RunicStorageNetwork/issues/new/choose) · [Changelog (EN)](https://github.com/rerit33/RunicStorageNetwork/blob/main/CHANGELOG_EN.md) · [История изменений (RU)](https://github.com/rerit33/RunicStorageNetwork/blob/main/CHANGELOG.md)

## English

**Keep your resources in storage — craft and build where you need them.**

Runic Storage Network connects your base's chests into a supply network. Craft and upgrade items at connected crafting stations, build with your hammer, and expand your base without hauling materials from one building to another.

No terminals or additional inventories: use the familiar menus while the required materials are consumed directly from connected chests.

### Build pieces

#### Storage Network Core

The center of your network. Connects nearby chests and makes their resources available for crafting, upgrading items, and building within its coverage area. One core is enough for a small workshop and storage area.

#### Runic Relay

Extends the network between buildings. Connects to the core directly or through other relays, links nearby chests, and lets you craft and build nearby using resources from the entire network.

Relays can form chains and branches. They need an uninterrupted connection to the core to function. If the connection breaks, the disconnected section loses access to network resources, but items remain in their chests and can still be retrieved manually.

Both structures are built with the regular hammer near a workbench and require no fuel.

### Getting started

1. Build a core near your storage chests.
2. Place crafting stations within its coverage area, or extend the network to other buildings with relays.
3. Use crafting stations and your hammer as usual — the required materials will be drawn from the available supply.

Materials in your inventory are used first, followed by any missing materials from connected chests. Recipe requirements and crafting station level requirements still apply.

Interact with a core to give its network an optional name using the standard Valheim text input window. Leave the field empty to remove the name. Connected cores share one network name. Hover over a connected container to see its network; unnamed networks simply show "Connected to network". Unconnected containers receive no additional line.

### Ranges and recipes

By default, chests connect within **20 m** of a node. Crafting stations and builders are supplied within **20 m**, and neighboring network nodes can connect over distances of up to **50 m**. These ranges can be changed in the mod configuration.

| Material | Core | Relay |
|---|---:|---:|
| Stone | 30 | 10 |
| Fine wood | 20 | 6 |
| Chains | 2 | — |
| Iron ingots | — | 2 |
| Surtling cores | 4 | 1 |
| Greydwarf eyes | 10 | 5 |

### Installation and compatibility

Requires **BepInExPack Valheim** and **Jötunn**. For multiplayer, install the same version of the mod and its required dependencies on the server and every player's client.

The network works with stationary containers built by players in loaded areas of the world, including containers added by other mods. Remote storage in unloaded areas is not supported. Backpacks, tombstones, ship and cart storage, and personal chests are not connected.

Machines that consume or fire their contents stay out of the network by default. The network does not draw crafting materials from the obliterator. Smelters, kilns, cooking stations, fermenters, beehives, sap collectors, ballistae and catapults are excluded on the same rule, including modded equivalents built on the same components.

Automatic eligibility does not guarantee compatibility with every modded container. Containers with custom inventory, saving or access behavior need separate compatibility testing.

The `Containers` section of the configuration decides which containers take part:

| Setting | Default | Effect |
|---|---|---|
| `AllowedContainers` | empty | Empty: every eligible container is connected. Filled: only the listed prefab names are connected. |
| `DeniedContainers` | `piece_trashcan` | Prefab names that are never connected. |
| `DeniedComponents` | machine components | A container is never connected when its prefab has one of these components. |

Exclusion always wins over inclusion, so a container listed in both is excluded. Changes take effect without restarting the game. In multiplayer these settings are administrator-only and come from the server, so the server decides which containers the whole session uses.

`rsn_status` in the console reports how many container types are supported and which are excluded, and the mod log lists them by name.

Building draws on the network with any build tool, including hammers added by other mods. A tool takes part when the game gives it its own build menu, so nothing needs to be registered with this mod. The hoe and cultivator are excluded by default: they place terrain, not buildings.

The `Building` section of the configuration decides which tools take part:

| Setting | Default | Effect |
|---|---|---|
| `AllowedBuildTools` | empty | Empty: every build tool qualifies. Filled: only the listed item prefabs build from the network. |
| `DeniedBuildTools` | `Hoe,Cultivator` | Item prefabs that never build from the network. |
| `DeniedPieceComponents` | `TerrainOp,TerrainModifier` | A piece is never supplied when its prefab has one of these components. |

Exclusion wins over inclusion here too, and these settings are administrator-only, so the server decides for the session. A piece that several tools can place stays available as long as one allowed tool can place it.

Do not enable multiple crafting-from-chests systems at the same time without checking compatibility. Back up your world and character before installing or updating the mod.

### Created with AI assistance

AI tools were used to develop the code, create concept art, and produce the 3D models. Gameplay decisions, selection of results, and in-game testing are handled by the author.

### Incompatible mods and integration limits

Runic Storage Network disables its resource supply when it detects NearbyCrafting, AzuCraftyBoxes, DvergerAutomation, CraftFromContainers or CraftFromChests. Use one storage-supply system at a time.

MultiUserChest and Quick Stack Store Sort Trash Restock are optional. The current integrations accept **MultiUserChest 0.6.2** and **Quick Stack 1.4.15**; other versions disable network supply until their integration is updated. With Quick Stack but without MultiUserChest, `AllowAreaStackingInMultiplayerWithoutMUC` must be disabled. These version checks do not guarantee compatibility with every mod combination.

---

## Русский

**Ресурсы остаются на складе — стройте и создавайте предметы там, где удобно.**

Runic Storage Network объединяет сундуки базы в сеть снабжения. Изготавливайте и улучшайте предметы на подключённых станках, стройте молотом и расширяйте базу, не перенося материалы из одного здания в другое.

Никаких терминалов или новых инвентарей: вы пользуетесь привычными меню, а необходимые ресурсы расходуются прямо из подключённых сундуков.

### Постройки

#### Ядро сети хранилищ — Storage Network Core

Центр вашей сети. Подключает соседние сундуки и предоставляет их ресурсы для крафта, улучшения предметов и строительства в своей зоне действия. Одного ядра достаточно для небольшой мастерской со складом.

#### Рунное реле — Runic Relay

Расширяет сеть между зданиями. Соединяется с ядром напрямую или через другие реле, подключает сундуки вокруг себя и снабжает ближайшие станки и строителя ресурсами всей сети.

Реле можно выстраивать в цепочки и ответвления. Для работы нужен непрерывный путь до ядра. При разрыве связи отключённый участок перестаёт снабжаться, но предметы остаются в сундуках и доступны вручную.

Обе постройки устанавливаются обычным молотом рядом с верстаком и не требуют топлива.

### Как начать

1. Постройте ядро рядом со складскими сундуками.
2. Разместите станки в его зоне действия или протяните сеть реле к другим зданиям.
3. Пользуйтесь станками и молотом как обычно — необходимые материалы будут взяты из доступного запаса.

Сначала расходуются материалы при себе, затем — недостающее из сундуков. Требования рецептов и уровни станков сохраняются.

Взаимодействуйте с ядром, чтобы задать необязательное название сети через стандартное окно ввода Valheim. Пустое поле удаляет название. Соединённые ядра используют общее название сети. При наведении на подключённое хранилище показывается его сеть; для безымянной сети — просто «Подключено к сети». У неподключённых хранилищ дополнительной строки нет.

### Радиусы и рецепты

По умолчанию сундуки подключаются в радиусе **20 м** от узла. Радиус снабжения станков и строителя — также **20 м**, а расстояние между соседними узлами связи — до **50 м**. Радиусы можно изменить в конфигурации мода.

| Материал | Ядро | Реле |
|---|---:|---:|
| Камень | 30 | 10 |
| Качественная древесина | 20 | 6 |
| Цепи | 2 | — |
| Железные слитки | — | 2 |
| Ядра суртлинга | 4 | 1 |
| Глаза грейдворфа | 10 | 5 |

### Установка и совместимость

Требуются **BepInExPack Valheim** и **Jötunn**. Для совместной игры установите одинаковую версию мода и необходимые зависимости на сервере и у всех игроков.

Сеть работает со стационарными хранилищами, построенными игроками, в загруженной области мира, включая хранилища из других модов. Доступ к удалённым выгруженным складам не поддерживается. Рюкзаки, надгробия, корабельные трюмы, повозки и личные сундуки не подключаются.

Устройства, которые расходуют или расстреливают своё содержимое, по умолчанию в сеть не входят. Сеть не забирает материалы для крафта из уничтожителя. По тому же правилу исключаются плавильни, углевыжигательные печи, очаги, бродильни, ульи, сокосборники, баллисты и катапульты, в том числе их аналоги из других модов, собранные на тех же компонентах.

Автоматическое подключение не гарантирует совместимость со всеми модовыми хранилищами. Хранилища с нестандартной работой инвентаря, сохранений или прав доступа требуют отдельной проверки совместимости.

Состав сети задаётся в разделе `Containers` конфигурации:

| Параметр | По умолчанию | Действие |
|---|---|---|
| `AllowedContainers` | пусто | Пусто: подключаются все подходящие хранилища. Заполнено: подключаются только перечисленные префабы. |
| `DeniedContainers` | `piece_trashcan` | Префабы, которые не подключаются никогда. |
| `DeniedComponents` | компоненты устройств | Хранилище не подключается, если в его префабе есть один из этих компонентов. |

Исключение всегда важнее включения: хранилище, указанное в обоих списках, остаётся отключённым. Изменения применяются без перезапуска игры. В совместной игре эти параметры доступны только администратору и приходят с сервера, поэтому состав хранилищ для всей сессии определяет сервер.

Команда `rsn_status` в консоли показывает, сколько типов хранилищ поддерживается и сколько исключено, а журнал мода перечисляет их по именам.

Строительство берёт ресурсы из сети любым строительным инструментом, включая молоты из других модов. Инструмент участвует, если игра даёт ему собственное меню построек, поэтому регистрировать его в этом моде не нужно. Мотыга и культиватор исключены по умолчанию: они меняют ландшафт, а не строят.

Состав инструментов задаётся в разделе `Building` конфигурации:

| Параметр | По умолчанию | Действие |
|---|---|---|
| `AllowedBuildTools` | пусто | Пусто: подходит любой строительный инструмент. Заполнено: из сети строят только перечисленные префабы предметов. |
| `DeniedBuildTools` | `Hoe,Cultivator` | Префабы предметов, которые никогда не строят из сети. |
| `DeniedPieceComponents` | `TerrainOp,TerrainModifier` | Постройка не снабжается, если в её префабе есть один из этих компонентов. |

Исключение здесь также важнее включения, а сами параметры доступны только администратору, поэтому состав определяет сервер. Постройка, доступная нескольким инструментам, остаётся доступной, пока её может поставить хотя бы один разрешённый инструмент.

Не включайте одновременно несколько систем крафта из сундуков без проверки совместимости. Перед установкой и обновлением делайте резервную копию мира и персонажа.

### Создано с помощью AI

Мод создан с использованием AI-инструментов при разработке кода, концептов и 3D-моделей. Игровые решения, отбор результатов и проверку в игре выполняет автор.

### Несовместимые моды и ограничения интеграций

Runic Storage Network отключает снабжение ресурсами при обнаружении NearbyCrafting, AzuCraftyBoxes, DvergerAutomation, CraftFromContainers или CraftFromChests. Используйте одну систему снабжения из хранилищ.

MultiUserChest и Quick Stack Store Sort Trash Restock необязательны. Текущие интеграции допускают **MultiUserChest 0.6.2** и **Quick Stack 1.4.15**; с другими версиями снабжение отключается до обновления интеграции. При использовании Quick Stack без MultiUserChest параметр `AllowAreaStackingInMultiplayerWithoutMUC` должен быть выключен. Эти проверки версий не гарантируют совместимость с любой комбинацией модов.
