using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 internal static class GatewayRuntime {
  static readonly int hash=Gateway.PrefabName.GetStableHashCode();
  static readonly Dictionary<long,NetworkGraph> graphs=new Dictionary<long,NetworkGraph>();
  static ZDOMan world;static long revision=-1;static bool gatewayEnabled;
  internal static bool Is(ZDO z)=>z!=null&&z.GetPrefab()==hash;
  internal static NetworkNode Describe(ZDO z,long actor=0){
   bool root=z.GetPrefab()=="RSN_NetworkCore".GetStableHashCode();
   return Decorate(new NetworkNode{Id=R.Key(z.m_uid),Network=z.GetString(NetworkMember.NetworkKey,""),Root=root,Confirmed=z.IsValid()&&z.GetLong(ZDOVars.s_creator,0)!=0&&z.GetInt(NetworkMember.SchemaKey,0)==1&&(actor==0||UnloadedNetworks.Ward(z.GetPosition(),actor)),Position=Topology.Position(z.GetPosition()),Storage=root?Plugin.StorageRadius.Value:Plugin.RelayStorage.Value,Supply=root?Plugin.SupplyRadius.Value:Plugin.RelaySupply.Value},z);
  }
  internal static NetworkNode Decorate(NetworkNode node,ZDO z){node.Gateway=Is(z);if(node.Gateway){node.Tag=NetworkLabels.Normalize(z.GetString(Gateway.TagKey,""));node.Binding=z.GetString(Gateway.BindingKey,"");}return node;}
  internal static void Clear(){world=null;revision=-1;graphs.Clear();}
  internal static NetworkGraph Graph(long actor){
   if(world!=ZDOMan.instance||revision!=UnloadedNetworks.CatalogRevision||gatewayEnabled!=ContentSettings.GatewayEnabled){world=ZDOMan.instance;revision=UnloadedNetworks.CatalogRevision;gatewayEnabled=ContentSettings.GatewayEnabled;graphs.Clear();}
   if(graphs.TryGetValue(actor,out var cached))return cached;
   // Once per topology revision and actor, never per recipe or item. Counts and
   // inventory revisions do not invalidate these local-component/path caches.
   var records=UnloadedNetworks.ApiNodes.Where(z=>z!=null&&z.IsValid()&&(gatewayEnabled||!Is(z))).ToArray();
   if(UnloadedNetworks.Authority&&!graphs.ContainsKey(0)){
    // Binding is a topology fact, independent of the requesting player's wards.
    var global=NetworkGraph.Automatic(records.Select(z=>Describe(z)),Plugin.RelayLink.Value);
    // Wait for a tag before persisting: a freshly placed second endpoint near
    // a foreign core must be free to inherit the named first endpoint's binding.
    foreach(var z in records.Where(Is))if(NetworkLabels.Normalize(z.GetString(Gateway.TagKey,""))!=""&&z.GetString(Gateway.BindingKey,"")==""&&global.GatewayBindings.TryGetValue(R.Key(z.m_uid),out var binding)&&binding!=""){
     z.SetOwner(ZNet.GetUID());z.Set(Gateway.BindingKey,binding);ZDOMan.instance.ForceSendZDO(z.m_uid);
    }
    if(revision!=UnloadedNetworks.CatalogRevision){revision=UnloadedNetworks.CatalogRevision;graphs.Clear();}
    graphs[0]=global;if(actor==0)return global;
   }
   var graph=NetworkGraph.Automatic(records.Select(z=>Describe(z,actor)),Plugin.RelayLink.Value);
   if(graphs.Count>=128)graphs.Clear();graphs[actor]=graph;return graph;
  }
  internal static bool Teleportable(string item){
   var prefab=ZNetScene.instance?ZNetScene.instance.GetPrefab(item):null;var drop=prefab?prefab.GetComponent<ItemDrop>():null;var data=drop?.m_itemData?.m_shared;
   // This setting is the only difference from vanilla's portal rule.
   return data!=null&&data.m_toolTier<1000&&(data.m_teleportable||ZoneSystem.instance&&ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll)||Plugin.GatewayCarriesAllItems.Value);
  }
  internal static bool ValidatePlan(Operation op,IEnumerable<Debit> plan){
   if(!UnloadedNetworks.Enabled)return true;
   var graph=Graph(op.PlayerId);if(!graph.HasGateways)return true;
   // The caller already validated the recipe, station, actor and access. Only
   // consult the cached routes here; do not repeat that full validation.
   var consumer=RemoteContext.Data(op.Build?op.Actor:op.Station);
   if(consumer==null||!graph.Nodes.TryGetValue(R.Key(op.Core),out var root))return false;
   var point=Topology.Position(consumer.GetPosition());
   string binding=BuilderCodex.ForOperation(op);if(binding!=null&&graph.BoundNetwork(binding)!=root.Network)return false;
   return plan.All(d=>d.Source=="player"||RemoteContext.Source(d.Source) is ZDO source&&graph.CanTransfer(root.Network,Topology.Position(source.GetPosition()),point,Teleportable(d.Item),binding!=null));
  }
  internal static IEnumerable<Stock> Filter(NetworkGraph graph,string network,Vector3 source,Vector3 consumer,IEnumerable<Stock> stock,bool linkedConsumer=false){
   if(!graph.HasGateways)return stock;
   int route=graph.TransferRoute(network,Topology.Position(source),Topology.Position(consumer),linkedConsumer);
   return route==1?stock:route==2?stock.Where(s=>Teleportable(s.Item)):Enumerable.Empty<Stock>();
  }
  internal static Vector3 Consumer(long player){
   var p=Player.m_localPlayer;if(!p||p.GetPlayerID()!=player)return new Vector3(float.PositiveInfinity,0,0);
   var station=p.GetCurrentCraftingStation();return station&&!p.InPlaceMode()?station.transform.position:p.transform.position;
  }
  internal static IEnumerable<Stock> Local(Core core,long actor,Container c,IEnumerable<Stock> stock,Vector3? point=null){
   if(!UnloadedNetworks.Enabled||!Topology.Graph.HasGateways)return stock;
   var graph=Topology.ForActor(actor);var member=core.GetComponent<NetworkMember>();
   string network=graph.Nodes.TryGetValue(member.Id,out var root)?root.Network:"";
   return Filter(graph,network,c.transform.position,point??Consumer(actor),stock,point==null&&BuilderCodex.Building(actor));
  }
 }
}
