using System;
using System.Collections.Generic;

namespace RunicStorageNetwork {
 // Embedded in the DLL: no external language files are needed in the package.
 internal static class TranslationCatalog {
  internal static readonly Dictionary<string,string> English=new Dictionary<string,string>();
  internal static readonly Dictionary<string,string> Russian=new Dictionary<string,string>();
  internal static readonly Dictionary<string,string> ReasonKeys=new Dictionary<string,string>(StringComparer.Ordinal);
  static void Add(string key,string en,string ru){English.Add("rsn_"+key,en);Russian.Add("rsn_"+key,ru);}
  static void Reason(string key,string en,string ru,params string[] reasons){Add(key,en,ru);foreach(var reason in reasons)ReasonKeys.Add(reason,"rsn_"+key);}
  static TranslationCatalog(){
   Add("name","Storage Network Core","Ядро сети хранилищ");
   Add("description","Connects nearby chests and supplies crafting, item upgrades and building with stored resources.","Объединяет ближайшие сундуки и снабжает крафт, улучшение предметов и строительство хранящимися ресурсами.");
   Add("relay_name","Runic Relay","Рунное реле");
   Add("relay_description","Automatically connects to nearby cores and relays. Extends shared storage coverage for crafting, upgrades and building.","Автоматически соединяется с соседними ядрами и реле. Расширяет общую сеть хранилищ для крафта, улучшений и строительства.");
   Add("disabled","Supply disabled","Снабжение выключено");
   Add("choose","Choose a network","Выберите сеть");
   Add("unbound","No network selected","Сеть не выбрана");
   Add("connected","Connected","Подключено");
   Add("placement_connected","Will connect to the network","Подключится к сети");
   Add("placement_disconnected","No connection to a core — can be connected later","Нет связи с ядром — можно подключить позже");
   Add("placement_checking","Checking nearby connections…","Проверка ближайших соединений…");
   Add("placement_shared_range","Storage and supply: {0} m","Хранилища и снабжение: {0} м");
   Add("placement_storage_range","Storage: {0} m","Хранилища: {0} м");
   Add("placement_supply_range","Supply: {0} m","Снабжение: {0} м");
   Add("disconnected","Path to core lost","Путь до ядра потерян");
   Add("unknown","Path not yet confirmed","Путь пока не подтверждён");
   Add("network","Network","Сеть");
   Add("network_rename","Name network","Назвать сеть");
   Add("network_name_input","Network name (optional)","Название сети (необязательно)");
   Add("chest_connected","Connected to network","Подключено к сети");
   Add("chest_connected_named","Connected to network: {0}","Подключено к сети: {0}");
   Add("hops","Links to core:","Соединений до ядра:");
   Add("containers","Nearby storage:","Хранилищ рядом:");
   Add("network_containers","Network storage:","Хранилищ в сети:");
   Add("link","Link range","Радиус связи");Add("storage","Storage range","Радиус хранилищ");Add("supply","Supply range","Радиус снабжения");
   Add("metres","m","м");
   Add("select","Select network","Выбрать сеть");Add("confirm","Confirm","Подтвердить");Add("cancel","Cancel","Отмена");
   Add("no_networks","No confirmed accessible networks nearby.","Нет подтверждённых доступных сетей поблизости.");
   Add("wait","Awaiting confirmation…","Ожидание подтверждения…");
   Add("bind_timeout","Confirmation not received. Close and reopen network selection.","Подтверждение не получено. Закройте и снова откройте выбор сети.");
   Add("bind_refused","Binding refused: check access and connectivity.","Привязка отклонена: проверьте доступ и связь.");
   Add("action_wait","Awaiting network confirmation. The result is not yet confirmed.","Ожидание подтверждения сети. Результат операции пока не подтверждён.");
   Add("action_recovering","Synchronizing resources…","Синхронизация ресурсов…");
   Add("action_refused","Action cancelled: {0}","Действие отменено: {0}");
   Add("error_unknown","Unable to complete the operation. See the mod log for details.","Не удалось завершить операцию. Подробности — в журнале мода.");
   Reason("error_supply","Network supply is unavailable.","Снабжение из сети недоступно.","supply unavailable");
   Reason("error_actor","The player is unavailable or their session changed.","Игрок недоступен или его сеанс изменился.","actor unavailable","actor data unavailable","actor session mismatch","actor identity mismatch","actor dead","sender does not own character","player context changed");
   Reason("error_piece","This build piece is unavailable.","Эта постройка недоступна.","invalid hammer piece","excluded build tool","excluded build piece");
   Reason("error_free","Resource requirements changed because free crafting or building is enabled.","Требования к ресурсам изменились: включён бесплатный крафт или строительство.","free building","free crafting");
   Reason("error_station","The required crafting station is unavailable, out of range or below the required level.","Нужный станок недоступен, слишком далеко или не достиг требуемого уровня.","missing build station","station unavailable");
   Reason("error_recipe","The selected recipe or item upgrade is unavailable.","Выбранный рецепт или улучшение предмета недоступны.","recipe unavailable","recipe selection changed","upgrade item changed");
   Reason("error_path","The network connection or coverage changed.","Связь с сетью или её зона действия изменились.","network path or coverage changed","network path/storage coverage unavailable");
   Reason("error_request","The resource request could not be validated.","Не удалось проверить запрос ресурсов.","invalid requirements","invalid character contribution","invalid owner snapshot","Invalid debit");
   Reason("error_pending","A previous operation is still awaiting confirmation.","Предыдущая операция ещё ожидает подтверждения.","previous operation pending");
   Reason("error_queue","Storage is busy. Please try again shortly.","Хранилище занято. Повторите попытку чуть позже.","queue timeout");
   Reason("error_limit","This operation needs over 1,024 resource entries. Consolidate the required resources into fewer chests or craft fewer items at once.","Для этой операции требуется более 1024 записей списания. Соберите нужные ресурсы в меньшем числе сундуков или уменьшите количество создаваемых предметов.","operation source limit");
   Reason("error_timeout","The network did not respond in time. Try again.","Сеть не ответила вовремя. Повторите попытку.","prepare timeout","dispatch failed");
   Reason("error_resources","Available resources changed or are insufficient.","Доступные ресурсы изменились или их недостаточно.","insufficient fresh resources","Stale inventory","RemoveItem refused","Unexpected debit amount");
   Reason("error_reservation","The resource reservation is no longer available.","Резервирование ресурсов больше недоступно.","operation already released","reservation missing");
   Reason("error_owner","The chest owner is unavailable or changed.","Владелец сундука недоступен или изменился.","owner unavailable","ownership changed","ok");
   Reason("error_unloaded","The chest or network node is not loaded.","Сундук или узел сети не загружен.","unloaded","unconfirmed loaded area");
   Reason("error_unsupported","This container type is not supported.","Этот тип хранилища не поддерживается.","unsupported prefab");
   Reason("error_excluded","This container type is excluded by the storage network settings.","Этот тип хранилища исключён настройками сети хранилищ.","excluded by configuration","excluded container type");
   Reason("error_built","The chest must be built by a player.","Сундук должен быть построен игроком.","not player built");
   Reason("error_private","Moving or private storage cannot be connected.","Подвижное или личное хранилище нельзя подключить.","moving/private");
   Reason("error_access","You do not have access to this chest or network.","Нет доступа к сундуку или сети.","access denied");
   Reason("error_inventory","The chest inventory is unavailable.","Содержимое сундука недоступно.","inventory unavailable");
   Reason("error_busy","The chest is in use or its resources are reserved.","Сундук занят или его ресурсы зарезервированы.","busy/reserved");
   Reason("error_cancelled","The selected action changed or was cancelled.","Выбранное действие изменилось или было отменено.","no pending action","hammer context changed","placement moved/cancelled","craft cancelled/changed","action not completed");
   Reason("available","Available","Доступен","available");
   Add("diag_help","Show nearby storage network status and write diagnostics to the mod log.","Показать состояние ближайшей сети хранилищ и записать диагностику в журнал мода.");
   Add("diag_no_player","No local player.","Локальный игрок отсутствует.");
   Add("diag_no_nodes","No confirmed local nodes.","Нет подтверждённых узлов поблизости.");
   Add("diag_node","Node: {0} | Network: {1} | State: {2} | Links to core: {3}","Узел: {0} | Сеть: {1} | Состояние: {2} | Соединений до ядра: {3}");
   Add("diag_neighbors","Neighboring nodes: {0}","Соседние узлы: {0}");
   Add("diag_path","Path to core: {0}","Путь до ядра: {0}");
   Add("diag_pool","Chests to check: {0}","Сундуков для проверки: {0}");
   Add("diag_chest","Chest {0}: {1}","Сундук {0}: {1}");
   Add("diag_policy","Container types: {0} supported, {1} excluded","Типы хранилищ: {0} поддерживается, {1} исключено");
   Add("diag_excluded","Excluded container types: {0}","Исключённые типы хранилищ: {0}");
   Add("diag_supply","Supply: {0} | Core: {1}","Снабжение: {0} | Ядро: {1}");
   Add("enabled","Enabled","Включено");Add("none","None","Нет");
  }
  internal static string Get(string language,string key){var map=language=="Russian"?Russian:English;return map.TryGetValue(key,out var value)?value:key;}
  internal static string ReasonKey(string reason){
   if(string.IsNullOrEmpty(reason))return "rsn_error_unknown";
   foreach(string prefix in new[]{"owner refused: ","commit refused: ","action refused: "})if(reason.StartsWith(prefix,StringComparison.Ordinal))return ReasonKey(reason.Substring(prefix.Length));
   if(reason.Contains(" / source path changed")||reason.StartsWith("source path/access changed before result:",StringComparison.Ordinal))return "rsn_error_path";
   if(reason.StartsWith("Fresh stock insufficient:",StringComparison.Ordinal))return "rsn_error_resources";
   return ReasonKeys.TryGetValue(reason,out var key)?key:"rsn_error_unknown";
  }
 }
}
