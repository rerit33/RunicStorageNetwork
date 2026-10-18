using System;
using System.Collections;
using BepInEx.Configuration;
using Jotunn.Managers;
using Jotunn.Utils;

namespace RunicStorageNetwork {
 // Freeze the local rule at process startup. Send that effective rule during
 // Jotunn's login synchronization, not the config value edited for next launch.
 internal static class GatewaySettings {
  static bool startup,active,received;
  internal static bool CarriesAllItems=>active;
  internal static void Bind(ConfigFile config){
   var setting=config.Bind("Network","GatewayCarriesAllItems",false,new ConfigDescription("Allow ore, metal and other portal-restricted items through Runic Gateways. Missing items and items with tool tier 1000 or higher remain blocked. Applied on game/server startup; restart after changing. Clients receive the server's active rule when joining. / Разрешить перенос руды, металла и других запрещённых для порталов предметов через Рунические мосты. Неизвестные предметы и предметы уровня инструмента 1000 и выше остаются запрещены. Применяется при запуске игры/сервера; после изменения нужен перезапуск. Клиенты получают действующее правило сервера при подключении.",null,new ConfigurationManagerAttributes{IsAdminOnly=true}));
   startup=setting.Value;Reset();
   var rpc=NetworkManager.Instance.AddRPC("RSN_GatewayRules",null,Receive);
   SynchronizationManager.Instance.AddInitialSynchronization(rpc,Package);
  }
  internal static void Reset(){active=startup;received=false;}
  static ZPackage Package(){var p=new ZPackage();p.Write(1);p.Write(startup);return p;}
  static IEnumerator Receive(long sender,ZPackage package){
   if(!ZNet.instance||ZNet.instance.IsServer()||received||ZNet.instance.GetServerPeer()?.m_uid!=sender)yield break;
   int version=package.ReadInt();if(version!=1)throw new InvalidOperationException("Unsupported gateway rule protocol: "+version);
   bool value=package.ReadBool();active=value;received=true;
  }
 }
}
