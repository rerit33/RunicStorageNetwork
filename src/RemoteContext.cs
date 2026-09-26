using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 // The coordinator reads synchronized object records; it never needs a rendered
 // Player, Core, station or Container in the host's own simulation area.
 internal sealed class RemoteContext {
  readonly Operation op;
  readonly Dictionary<Vector2s,List<ZDO>> sectors=new Dictionary<Vector2s,List<ZDO>>();
  readonly Dictionary<Vector3,bool> wards=new Dictionary<Vector3,bool>();
  internal NetworkGraph Graph;
  internal ZDO Root;
  readonly List<NetworkNode> nodes=new List<NetworkNode>();
  bool repaired;
  string Network=>Graph.Nodes.TryGetValue(R.Key(op.Core),out var root)?root.Network:"";
  static ZNetScene catalogScene;
  static float wardRange,extensionRange;
  internal RemoteContext(Operation value){op=value;Catalog();}
  static void Catalog(){
   if(catalogScene==ZNetScene.instance)return;catalogScene=ZNetScene.instance;wardRange=extensionRange=0;
   foreach(var prefab in catalogScene.m_prefabs){
    var ward=prefab.GetComponent<PrivateArea>();if(ward)wardRange=Mathf.Max(wardRange,ward.m_radius);
    var extension=prefab.GetComponent<StationExtension>();if(extension)extensionRange=Mathf.Max(extensionRange,extension.m_maxStationDistance);
   }
  }
  internal static ZDO Data(ZDOID id){var z=ZDOMan.instance?.GetZDO(id);return z!=null&&z.IsValid()?z:null;}
  internal static ZDO Source(string key){
   var parts=key.Split(':');if(parts.Length!=2||!long.TryParse(parts[0],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out long user)||!uint.TryParse(parts[1],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out uint id))return null;
   var value=new ZDOID(user,id);return R.Key(value)==key?Data(value):null;
  }
  internal static GameObject Prefab(ZDO z)=>z==null?null:ZNetScene.instance.GetPrefab(z.GetPrefab());
  internal static bool Actor(ZDOID id,long peer,long expected,out ZDO actor,out string reason){
   actor=Data(id);var prefab=Prefab(actor);
   reason=SessionGuard.Check(actor!=null&&prefab&&prefab.GetComponent<Player>(),actor?.GetOwner()??0,peer,actor?.GetLong(ZDOVars.s_playerID,0)??0,expected,actor?.GetBool(ZDOVars.s_dead,false)??false,ZNet.instance.IsServer(),peer==ZNet.GetUID(),ZNet.instance.GetPeer(peer)?.m_characterID==id);
   return reason==null;
  }
  IEnumerable<ZDO> Around(Vector3 point,float radius){
   var center=ZoneSystem.GetZone(point);int r=Mathf.CeilToInt(radius/64f)+1;var seen=new HashSet<ZDOID>();
   for(int x=-r;x<=r;x++)for(int y=-r;y<=r;y++){
    var zone=new Vector2s(center.x+x,center.y+y);
    if(!sectors.TryGetValue(zone,out var list)){
     list=new List<ZDO>();ZDOMan.instance.FindSectorObjects(zone,new SimulationDistance(0,0,true),list,null);sectors[zone]=list;
    }
    foreach(var z in list)if(z!=null&&z.IsValid()&&seen.Add(z.m_uid))yield return z;
   }
  }
  internal bool Ward(Vector3 point){
   if(wards.TryGetValue(point,out bool hit))return hit;bool denied=false,allowed=false;
   foreach(var z in Around(point,wardRange)){
    var prefab=Prefab(z);var ward=prefab?prefab.GetComponent<PrivateArea>():null;
    if(!ward||!z.GetBool(ZDOVars.s_enabled,false))continue;
    var delta=z.GetPosition()-point;if(delta.x*delta.x+delta.z*delta.z>=ward.m_radius*ward.m_radius)continue;
    bool permitted=z.GetLong(ZDOVars.s_creator,0)==op.PlayerId;int count=z.GetInt(ZDOVars.s_permitted,0);
    for(int i=0;i<count&&!permitted;i++)permitted=z.GetLong("pu_id"+i,0)==op.PlayerId;
    if(permitted)allowed=true;else denied=true;
   }
   return wards[point]=allowed||!denied;
  }
  internal bool Validate(out string reason){
   reason="supply unavailable";if(!Plugin.Enabled||!ZNetScene.instance||ZDOMan.instance==null)return false;
   if(!Actor(op.Actor,op.Peer,op.PlayerId,out var actor,out reason))return false;
   var point=actor.GetPosition();Piece.Requirement[] req;
   if(op.Build){
    var prefab=ZNetScene.instance.GetPrefab(op.Target);var piece=prefab?prefab.GetComponent<Piece>():null;
    var hammer=ObjectDB.instance.GetItemPrefab("Hammer")?.GetComponent<ItemDrop>();
    reason="invalid hammer piece";if(!piece||!piece.m_enabled||!hammer||!hammer.m_itemData.m_shared.m_buildPieces.m_pieces.Contains(prefab)||op.Quality!=0||op.Multiplier!=1)return false;
    reason="free building";if(ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()))return false;
    reason="missing build station";
    if(piece.m_craftingStation&&!ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench)&&!HaveBuildStation(point,piece.m_craftingStation.m_name))return false;
    req=piece.m_resources;
   }else{
    var recipe=RecipeIndex.Find(op.Target);
    reason="recipe unavailable";if(!recipe||op.Quality<1||op.Quality>recipe.m_item.m_itemData.m_shared.m_maxQuality)return false;
    var z=Data(op.Station);var prefab=Prefab(z);var station=prefab?prefab.GetComponent<CraftingStation>():null;
    reason="station unavailable";if(!station||station.m_upgrader||Vector3.Distance(point,z.GetPosition())>=station.m_useDistance)return false;
    var required=recipe.GetRequiredStation(op.Quality);
    if(required&&(station.m_name!=required.m_name||Level(z,station)<recipe.GetRequiredStationLevel(op.Quality)))return false;
    // Roof, fire and the live interaction/UI are checked by the acting client
    // before requesting and immediately before producing the result.
    reason="free crafting";if(ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost))return false;
    req=recipe.m_resources;point=z.GetPosition();
   }
   reason="network path or coverage changed";Root=Data(op.Core);
   var rootPrefab=Prefab(Root);
   if(!rootPrefab||!rootPrefab.GetComponent<Core>()||Root.GetLong(ZDOVars.s_creator,0)==0||Root.GetInt(NetworkMember.SchemaKey,0)!=1||!NetworkGraph.MatchesRoot(Root.GetString(NetworkMember.NetworkKey,""),op.Network)){
    Plugin.Debug(op.Id+" root mismatch current="+R.Key(op.Core)+" saved="+(Root?.GetString(NetworkMember.NetworkKey,"")??"missing")+" requested="+op.Network);return false;
   }
   foreach(var id in op.Nodes.Concat(new[]{op.Core}).Distinct())AddNode(Data(id));
   Graph=NetworkGraph.Automatic(nodes,Plugin.RelayLink.Value);
   if(!Connected(point)){RepairGraph();if(!Connected(point)){Plugin.Debug(op.Id+" coverage mismatch nodes="+nodes.Count+" point="+point);return false;}}
   op.Needs=Stockroom.Requirements(req,op.Quality,op.Multiplier);reason="invalid requirements";
   if(op.Needs.Count==0||op.Needs.Count>32||op.Needs.Any(n=>n.Amount>100000))return false;
   foreach(var s in op.PlayerStock)if(s.Source!="player"||s.Amount<0||s.Amount>100000||!op.Needs.Any(n=>n.Item==s.Item)||s.Quality<1||s.Quality>100){reason="invalid character contribution";return false;}
   reason="ok";return true;
  }
  void AddNode(ZDO z){
   var prefab=Prefab(z);if(!prefab)return;bool root=prefab.GetComponent<Core>();if(!root&&!prefab.GetComponent<Relay>())return;
   if(z.GetLong(ZDOVars.s_creator,0)==0||z.GetInt(NetworkMember.SchemaKey,0)!=1)return;
   nodes.Add(new NetworkNode{Id=R.Key(z.m_uid),Network=z.GetString(NetworkMember.NetworkKey,""),Root=root,Confirmed=Ward(z.GetPosition()),Position=Topology.Position(z.GetPosition()),Storage=root?Plugin.StorageRadius.Value:Plugin.RelayStorage.Value,Supply=root?Plugin.SupplyRadius.Value:Plugin.RelaySupply.Value});
  }
  void RepairGraph(){
   if(repaired)return;repaired=true;
   var seen=new HashSet<string>(nodes.Select(n=>n.Id));var scheduled=new HashSet<ZDOID>{Root.m_uid};var queue=new Queue<ZDO>();queue.Enqueue(Root);
   while(queue.Count>0){
    var current=queue.Dequeue();if(!Ward(current.GetPosition()))continue;
    foreach(var z in Around(current.GetPosition(),Plugin.RelayLink.Value)){
     if(Vector3.Distance(current.GetPosition(),z.GetPosition())>Plugin.RelayLink.Value||z.GetInt(NetworkMember.SchemaKey,0)!=1||z.GetLong(ZDOVars.s_creator,0)==0)continue;
     var prefab=Prefab(z);if(!prefab||(!prefab.GetComponent<Core>()&&!prefab.GetComponent<Relay>()))continue;
     if(scheduled.Count>=4096&&!scheduled.Contains(z.m_uid))continue;
     if(scheduled.Add(z.m_uid))queue.Enqueue(z);
     if(seen.Add(R.Key(z.m_uid)))AddNode(z);
    }
   }
   Graph=NetworkGraph.Automatic(nodes,Plugin.RelayLink.Value);
   Plugin.Debug(op.Id+" rebuilt component from synchronized records; nodes="+nodes.Count);
  }
  bool Connected(Vector3 point)=>Graph.Nodes.TryGetValue(R.Key(op.Core),out var root)&&root.Root&&root.Confirmed&&Graph.Supplies(root.Network,Topology.Position(point),n=>true);
  int Level(ZDO z,CraftingStation station){
   int level=1;var kinds=new HashSet<string>(StringComparer.Ordinal);
   foreach(var candidate in Around(z.GetPosition(),extensionRange)){
    var prefab=Prefab(candidate);var e=prefab?prefab.GetComponent<StationExtension>():null;
    if(e&&e.m_craftingStation&&e.m_craftingStation.m_name==station.m_name&&Vector3.Distance(candidate.GetPosition(),z.GetPosition())<e.m_maxStationDistance&&(e.m_stack||kinds.Add(prefab.GetComponent<Piece>()?.m_name??prefab.name)))level++;
   }
   return level;
  }
  bool HaveBuildStation(Vector3 point,string name){
   var z=Data(op.Station);var prefab=Prefab(z);var station=prefab?prefab.GetComponent<CraftingStation>():null;
   if(!station||station.m_name!=name)return false;
   point.y=z.GetPosition().y; // Vanilla building range is horizontal.
   return Vector3.Distance(point,z.GetPosition())<station.m_rangeBuild+(Level(z,station)-1)*station.m_extraRangePerLevel;
  }
  internal bool SourceAllowed(ZDO z,out string reason){
   reason="unloaded";var prefab=Prefab(z);if(!prefab||z.GetOwner()==0)return false;
   reason=ContainerPolicy.Reason(prefab.name);if(reason!=null)return false;
   reason="not player built";if(z.GetLong(ZDOVars.s_creator,0)==0)return false;
   // Privacy, wagon and root-override are prefab facts already settled by ContainerPolicy.
   reason="network path/storage coverage unavailable";if(!Graph.Covers(Network,Topology.Position(z.GetPosition()),n=>true)){RepairGraph();if(!Graph.Covers(Network,Topology.Position(z.GetPosition()),n=>true))return false;}
   reason="access denied";if(!Ward(z.GetPosition()))return false;reason="available";return true;
  }
  internal bool OwnerSource(Container c,out string reason,bool ownLease=false){
   reason="unloaded";var v=R.View(c);if(!c||!R.Valid(v)||!v.IsOwner())return false;
   reason=ContainerPolicy.Reason(R.Id(c.gameObject));if(reason!=null)return false;
   reason="not player built";var piece=c.GetComponent<Piece>();if(!piece||!piece.IsPlacedByPlayer())return false;
   reason="unconfirmed loaded area";if(!ZNetScene.instance.IsAreaReady(c.transform.position))return false;
   reason="moving/private";if(c.m_privacy!=Container.PrivacySetting.Public||c.m_wagon||c.m_rootObjectOverride||c.GetComponentInParent<Ship>()||c.GetComponentInParent<Rigidbody>())return false;
   reason="access denied";if(!(bool)R.Call(c,"CheckAccess",new[]{typeof(long)},op.PlayerId)||!Access.Ward(c.transform.position,op.PlayerId))return false;
   reason="inventory unavailable";if(c.GetInventory()==null)return false;
   reason="busy/reserved";if(!ownLease&&(c.IsInUse()||v.GetZDO().GetInt(ZDOVars.s_inUse)!=0||Transport.Reserved(v.GetZDO(),op.Id)||Integrations.IsBusy(c.GetInventory())))return false;
   reason="available";return true;
  }
 }
}
