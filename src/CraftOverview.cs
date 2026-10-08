using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 internal static class CraftOverview {
  static InventoryGui gui;static Player player;static CraftingStation station;static Core core;
  static float nextScan;static bool rowsDirty;static int revision=-1;
  static int topologyRevision=-1;static bool teleportAll,gatewayCarriesAllItems;
  static object[] rowQueue;static int rowIndex;
  internal static void Clear(){gui=null;player=null;station=null;core=null;nextScan=0;rowsDirty=false;rowQueue=null;revision=-1;}
  internal static void Rescan(){nextScan=0;rowsDirty=true;}
  internal static void Open(InventoryGui current,Player actor){
   var at=actor?actor.GetCurrentCraftingStation():null;
   if(!actor||!current||!at||at.m_upgrader||!Plugin.Enabled){Clear();return;}
   if(gui==current&&player==actor&&station==at)return;
   Clear();gui=current;player=actor;station=at;RecipeIndex.Ensure();
   core=Actions.Context(player,true);if(core)StorageIndex.Reconcile(core);
  }
  internal static void Tick(){
   if(!gui)return;
   if(!InventoryGui.IsVisible()||!player||player.IsDead()||player.GetCurrentCraftingStation()!=station||!Plugin.Enabled){Clear();return;}
   if(Time.unscaledTime>=nextScan){
    nextScan=Time.unscaledTime+.5f;var current=Actions.Context(player,true);
    if(core!=current){core=current;rowsDirty=true;}
    bool portals=UnloadedNetworks.Enabled&&ZoneSystem.instance&&ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll);
    bool carriesAll=Plugin.GatewayCarriesAllItems.Value;
    if(topologyRevision!=Topology.DisplayRevision||teleportAll!=portals||gatewayCarriesAllItems!=carriesAll){topologyRevision=Topology.DisplayRevision;teleportAll=portals;gatewayCarriesAllItems=carriesAll;rowsDirty=true;}
   }
   if(revision!=StorageIndex.Revision){revision=StorageIndex.Revision;rowsDirty=true;}
  }
  internal static List<Stock> Stock(Player actor,IEnumerable<Need> requirements){
   var needs=requirements.ToList();var result=Stockroom.Snapshot(actor.GetInventory(),"player",needs,false);
   if(actor==player&&core)result.AddRange(StorageIndex.Query(core,actor.GetPlayerID(),needs));
   return result;
  }
  internal static bool Ready(Player actor)=>actor==player&&core&&StorageIndex.Ready(core);
  internal static void Fresh(string key,List<Stock> items){
   StorageIndex.Fresh(key);rowsDirty=true;
  }
  internal static void RefreshRows(InventoryGui current){
   if(gui!=current||!player||Actions.Active!=null)return;
   // Recolour existing rows once a completed overview changes. Do not recreate
   // or reorder the full list, move the selection, or reserve every recipe.
   if(rowQueue==null){if(!rowsDirty)return;rowsDirty=false;rowQueue=((IList)R.Get<object>(gui,"m_availableRecipes")).Cast<object>().ToArray();rowIndex=0;}
   var clock=Stopwatch.StartNew();int budget=32;
   while(rowIndex<rowQueue.Length&&budget-->0){
    var row=rowQueue[rowIndex++];
    var type=row.GetType();var recipe=(Recipe)type.GetProperty("Recipe").GetValue(row,null);
    var item=(ItemDrop.ItemData)type.GetProperty("ItemData").GetValue(row,null);
    var element=(GameObject)type.GetProperty("InterfaceElement").GetValue(row,null);
    if(!recipe||!element)continue;
    int quality=item==null?1:item.m_quality+1;
    bool available=quality<=recipe.m_item.m_itemData.m_shared.m_maxQuality&&(player.HaveRequirements(recipe,false,quality,1)||player.NoCostCheat()||ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost));
    var icon=element.transform.Find("icon")?.GetComponent<Image>();var label=element.transform.Find("name")?.GetComponent<TMP_Text>();
    if(icon)icon.color=available?Color.white:new Color(1,0,1,0);
    if(label)label.color=available?Color.white:new Color(.66f,.66f,.66f,1);
    if(clock.Elapsed.TotalMilliseconds>=.75)break;
   }
   if(rowIndex==rowQueue.Length)rowQueue=null;
  }
 }
}
