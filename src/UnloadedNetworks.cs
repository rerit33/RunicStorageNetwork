using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace RunicStorageNetwork {
 // Experimental. Inactive adapters expose saved records to the
 // existing transaction code. They never enter ZNetScene, run Awake/Update, or carry
 // renderers, colliders, WearNTear, default loot or third-party prefab behaviours.
 internal sealed class UnloadedReplica:MonoBehaviour {
  internal ZDO Data;internal byte[] Expected;internal bool Read,SavedAfterLoad;
  internal uint FailedRevision;internal bool ReportedFailure;
 }
 internal static partial class UnloadedNetworks {
  static readonly HashSet<int> chestTypes=new HashSet<int>();
  static readonly HashSet<int> nodeTypes=new HashSet<int>(new[]{"RSN_NetworkCore","RSN_RunicRelay","RSN_RunicGateway"}.Select(s=>s.GetStableHashCode()));
  static readonly Dictionary<ZDOID,ZDO> nodes=new Dictionary<ZDOID,ZDO>();
  static readonly Dictionary<ZDOID,ZDO> chests=new Dictionary<ZDOID,ZDO>();
  static readonly Dictionary<ZDOID,ZDO> wards=new Dictionary<ZDOID,ZDO>();
  static readonly Dictionary<Vector2s,HashSet<ZDOID>> chestSectors=new Dictionary<Vector2s,HashSet<ZDOID>>();
  static readonly Dictionary<ZDOID,Vector2s> positions=new Dictionary<ZDOID,Vector2s>();
  static readonly Dictionary<ZDOID,(Vector3,string,long,int)> nodeState=new Dictionary<ZDOID,(Vector3,string,long,int)>();
  static readonly Dictionary<ZDOID,long> creators=new Dictionary<ZDOID,long>();
  static readonly Dictionary<ZDOID,long> owners=new Dictionary<ZDOID,long>();
  static readonly Dictionary<(Vector3,long),bool> wardAccess=new Dictionary<(Vector3,long),bool>();
  static readonly Dictionary<ZDOID,GameObject> replicas=new Dictionary<ZDOID,GameObject>();
  static readonly Queue<ZDOID> queue=new Queue<ZDOID>();
  static readonly HashSet<ZDOID> queued=new HashSet<ZDOID>(),requestedChests=new HashSet<ZDOID>();
  static readonly HashSet<ZDOID> ownerPending=new HashSet<ZDOID>();
  static readonly Dictionary<string,long> requestedNetworks=new Dictionary<string,long>();
  static readonly HashSet<int> wardTypes=new HashSet<int>();
  static ZDOMan world;static bool enabled,bootstrapped;static long structure,nodesRequested=-1;static float demandUntil;
  static Harmony patches;
  internal static bool Installed=>patches!=null;
  internal static bool Requested=>enabled&&world==ZDOMan.instance&&ZNet.instance;
  internal static bool Authority=>ZNet.instance&&ZNet.instance.IsServer();
  internal static bool Enabled=>Requested&&(Authority||UnloadedMultiplayer.ServerEnabled);
  internal static long CatalogRevision=>structure;
  internal static bool Demand=>Enabled&&Time.unscaledTime<demandUntil;
  internal static IEnumerable<NetworkMember> Members=>replicas.Values.Where(g=>g).Select(g=>g.GetComponent<NetworkMember>()).Where(m=>m&&m.Valid);
  internal static IEnumerable<Container> Containers=>replicas.Values.Where(g=>g).Select(g=>g.GetComponent<Container>()).Where(c=>c&&R.Valid(R.View(c))&&(!ZNetScene.instance.FindInstance(R.View(c).GetZDO().m_uid)||Transport.Leases.ContainsKey(c.GetInventory())));
  static void Hook(Harmony h,Type type,string method,string handler,bool prefix=false,Type[] args=null,bool transpiler=false){
   var target=args==null?AccessTools.Method(type,method):AccessTools.Method(type,method,args);
   if(target==null)throw new MissingMethodException(type.Name,method);
   var patch=new HarmonyMethod(typeof(UnloadedNetworks),handler){priority=prefix?Priority.First:Priority.Last};
   h.Patch(target,prefix:prefix?patch:null,postfix:prefix||transpiler?null:patch,transpiler:transpiler?patch:null);
  }
  internal static void Install(){
   if(Installed||!Plugin.ExperimentalUnloadedNetworks.Value)return;
   // Separate owner: a failed optional patch must not disable normal supply or
   // remove the stable mod's patches. No game API lookup occurs when opted out.
   var h=new Harmony(Plugin.Guid+".unloaded");
   try{InstallHooks(h);patches=h;}
   catch(Exception e){
    Shutdown();StorageIndex.Offline=null;CraftInspection.FindContainer=CraftInspection.LoadedContainer;
    try{h.UnpatchSelf();}catch(Exception cleanup){Plugin.Error("experimental patch cleanup",cleanup);}
    Plugin.Error("Unloaded-network experiment unavailable; normal networking remains active",e);
   }
  }
  internal static void Uninstall(){
   Shutdown();StorageIndex.Offline=null;CraftInspection.FindContainer=CraftInspection.LoadedContainer;
   var h=patches;patches=null;h?.UnpatchSelf();
  }
  internal static void InstallPinning(Harmony h)=>Hook(h,typeof(ZNetScene),"RemoveObjects",nameof(PinTransactions),true,args:new[]{typeof(List<ZDO>),typeof(List<ZDO>)});
  static void InstallHooks(Harmony h){
   Hook(h,typeof(ZNetScene),"Awake",nameof(Bootstrap));
   Hook(h,typeof(ZNet),"Start",nameof(WorldLoaded));
   Hook(h,typeof(ZNetScene),"OnDestroy",nameof(Shutdown),true);
   Hook(h,typeof(ZDOMan),"AddToSector",nameof(Added));
   Hook(h,typeof(ZDOMan),"HandleDestroyedZDO",nameof(Destroyed),true);
   Hook(h,typeof(ZDOMan),"RemovePeer",nameof(Disconnected));
   Hook(h,typeof(ZDO),"IncreaseDataRevision",nameof(Changed));
   Hook(h,typeof(ZDO),"Deserialize",nameof(Received),false,new[]{typeof(ZPackage)});
   Hook(h,typeof(ZDO),"SetOwner",nameof(OwnershipChanged));
   Hook(h,typeof(ZDO),"SetOwnerInternal",nameof(OwnershipChanged));
   Hook(h,typeof(ZNetView),"Awake",nameof(Loaded));
   Hook(h,typeof(ZNetScene),"RemoveObjects",nameof(UnloadIL),args:new[]{typeof(List<ZDO>),typeof(List<ZDO>)},transpiler:true);
   Hook(h,typeof(Container),"Load",nameof(LoadReplica),true,Type.EmptyTypes);
   Hook(h,typeof(Container),"Save",nameof(SaveReplica),true,Type.EmptyTypes);
   StorageIndex.Offline=new StorageIndex.UnloadedMode(()=>Enabled,()=>Demand,SourceReady,ReadFailure);
   CraftInspection.FindContainer=FindContainer;
  }
  static void Shutdown(){
   UnloadedMultiplayer.Clear();
   foreach(var go in replicas.Values)if(go){var core=go.GetComponent<Core>();if(core)core.Detach();var member=go.GetComponent<NetworkMember>();if(member)Topology.Members.Remove(member);R.Set(go.GetComponent<ZNetView>(),"m_zdo",null);UnityEngine.Object.Destroy(go);}
   replicas.Clear();nodes.Clear();chests.Clear();wards.Clear();chestSectors.Clear();positions.Clear();nodeState.Clear();creators.Clear();owners.Clear();wardAccess.Clear();queue.Clear();queued.Clear();ownerPending.Clear();requestedChests.Clear();requestedNetworks.Clear();wardTypes.Clear();chestTypes.Clear();world=null;enabled=false;bootstrapped=false;structure=0;nodesRequested=-1;demandUntil=0;
  }
  static void Bootstrap(){
   Shutdown();world=ZDOMan.instance;enabled=Installed;
   if(!enabled)return;
   // ZNetScene.Awake runs BEFORE ZNet.Start reads both old and chunked saves.
   // Only latch the setting here. Index the completed records after that load.
   Plugin.Info("EXPERIMENTAL unloaded networks enabled; "+(Authority?"server indexes saved world records":"client requests distant records from the server")+". May cause errors. Back up the world before testing.");
  }
  static void WorldLoaded(){
   if(!Requested||bootstrapped||ZNet.m_loadError)return;
   foreach(var prefab in ZNetScene.instance.m_prefabs)if(prefab){if(prefab.GetComponent<PrivateArea>())wardTypes.Add(prefab.name.GetStableHashCode());if(prefab.GetComponent<Container>())chestTypes.Add(prefab.name.GetStableHashCode());}
   // Exactly one metadata pass AFTER the world load; no inventories are
   // deserialized here. Includes records which have never had a local instance.
   if(Authority)foreach(var z in R.Get<Dictionary<ZDOID,ZDO>>(world,"m_objectsByID").Values)Record(z);
   bootstrapped=true;Topology.Dirty();
   Plugin.Info("Experimental unloaded network index ready: "+nodes.Count+" nodes, "+chests.Count+" storage records (eligibility checked on access).");
  }
  static void Added(ZDO zdo){if(Enabled&&bootstrapped)Record(zdo);}
  static void Record(ZDO z){
   if(z==null||!z.IsValid())return;int prefab=z.GetPrefab();
   if(nodeTypes.Contains(prefab)){
    long creator=z.GetLong(ZDOVars.s_creator,0);int schema=z.GetInt(NetworkMember.SchemaKey,0);if(creator==0||schema!=1)return;
    var stamp=(z.GetPosition(),z.GetString(NetworkMember.NetworkKey,"")+"\n"+z.GetString("rsn_gateway_tag","")+"\n"+z.GetString("rsn_gateway_network",""),creator,schema);
    if(nodeState.TryGetValue(z.m_uid,out var old)&&old.Equals(stamp))return;
    nodeState[z.m_uid]=stamp;nodes[z.m_uid]=z;UpdateReplica(z);structure++;if(Demand)Enqueue(z.m_uid);Topology.Dirty();
   }
   else if(chestTypes.Contains(prefab)){
    chests[z.m_uid]=z;UpdateReplica(z);var sector=ZoneSystem.GetZone(z.GetPosition());long creator=z.GetLong(ZDOVars.s_creator,0);
    if(!owners.ContainsKey(z.m_uid))owners[z.m_uid]=z.GetOwner();
    if(positions.TryGetValue(z.m_uid,out var prior)&&prior==sector&&creators.TryGetValue(z.m_uid,out var owner)&&owner==creator)return;
    creators[z.m_uid]=creator;
    if(positions.TryGetValue(z.m_uid,out var old)&&old!=sector&&chestSectors.TryGetValue(old,out var previous))previous.Remove(z.m_uid);
    positions[z.m_uid]=sector;if(!chestSectors.TryGetValue(sector,out var list))chestSectors[sector]=list=new HashSet<ZDOID>();list.Add(z.m_uid);structure++;Topology.Dirty();
   }else if(wardTypes.Contains(prefab)){wards[z.m_uid]=z;wardAccess.Clear();structure++;Topology.Dirty();}
  }
  static void UpdateReplica(ZDO z){
   if(!replicas.TryGetValue(z.m_uid,out var go)||!go)return;
   go.transform.position=z.GetPosition();go.transform.rotation=z.GetRotation();R.Set(go.GetComponent<Piece>(),"m_creator",z.GetLong(ZDOVars.s_creator,0));
  }
  static void Changed(ZDO __instance){
   if(!Enabled||!bootstrapped)return;var id=__instance.m_uid;
   if(nodeTypes.Contains(__instance.GetPrefab()))Record(__instance);
   else if(wards.ContainsKey(id)){wardAccess.Clear();structure++;Topology.Dirty();}
   else if(chests.ContainsKey(id)){if(!creators.TryGetValue(id,out var owner)||owner!=__instance.GetLong(ZDOVars.s_creator,0))Record(__instance);StorageIndex.Changed(R.Key(id));}
   UnloadedMultiplayer.Changed(__instance);
  }
  static void Received(ZDO __instance){
   if(!Enabled||!bootstrapped)return;
   if(!Authority&&!UnloadedMultiplayer.Accepted(__instance.m_uid)&&!ZNetScene.instance.FindInstance(__instance.m_uid))return;
   Record(__instance);
   Changed(__instance);
   UnloadedMultiplayer.Received(__instance);
  }
  static void OwnershipChanged(ZDO __instance){
   if(!Enabled||!bootstrapped||!chests.ContainsKey(__instance.m_uid))return;
   long owner=__instance.GetOwner();if(owners.TryGetValue(__instance.m_uid,out long previous)&&previous==owner)return;owners[__instance.m_uid]=owner;
   // SetOwnerInternal is also called in the middle of deserialization; wait
   // until the next update before claiming the fully received record.
   if(Authority&&__instance.GetOwner()==0&&requestedChests.Contains(__instance.m_uid))ownerPending.Add(__instance.m_uid);
   StorageIndex.Changed(R.Key(__instance.m_uid));UnloadedMultiplayer.Changed(__instance);
  }
  static void Disconnected(ZNetPeer __0){
   if(!Enabled||!Authority||__0==null)return;
   foreach(var id in requestedChests)if(chests.TryGetValue(id,out var z)&&z.GetOwner()==__0.m_uid)ownerPending.Add(id);
  }
  static void Destroyed(ZDOID uid){
   if(!Enabled)return;bool changed=nodes.Remove(uid)|chests.Remove(uid)|wards.Remove(uid);if(!changed)return;
   if(positions.TryGetValue(uid,out var sector)){if(chestSectors.TryGetValue(sector,out var ids))ids.Remove(uid);positions.Remove(uid);}
   requestedChests.Remove(uid);queued.Remove(uid);ownerPending.Remove(uid);nodeState.Remove(uid);creators.Remove(uid);owners.Remove(uid);wardAccess.Clear();
   if(replicas.TryGetValue(uid,out var go)){replicas.Remove(uid);if(go){var core=go.GetComponent<Core>();if(core)core.Detach();R.Set(go.GetComponent<ZNetView>(),"m_zdo",null);UnityEngine.Object.Destroy(go);}}
   StorageIndex.Changed(R.Key(uid));structure++;Topology.Dirty();
  }
  static void Loaded(ZNetView __instance){
   if(!Enabled||!R.Valid(__instance))return;var z=__instance.GetZDO();
   if(nodeTypes.Contains(z.GetPrefab())||chestTypes.Contains(z.GetPrefab())||wardTypes.Contains(z.GetPrefab())){Record(z);Topology.Dirty();}
   else if(__instance.GetComponent<Container>())Topology.Dirty();
  }
  static IEnumerable<CodeInstruction> UnloadIL(IEnumerable<CodeInstruction> instructions){
   // ResetZDO is also used by destruction. Releasing its owner there makes
   // ZNetScene.Destroy skip DestroyZDO AFTER the building materials have dropped.
   // Only the area-unload call site may save/release a departing live chest.
   var reset=AccessTools.Method(typeof(ZNetView),nameof(ZNetView.ResetZDO),Type.EmptyTypes);
   var unload=AccessTools.Method(typeof(UnloadedNetworks),nameof(UnloadView));
   var code=instructions.ToList();int count=0;
   foreach(var i in code)if((i.opcode==OpCodes.Call||i.opcode==OpCodes.Callvirt)&&i.operand is MethodInfo method&&method==reset){i.opcode=OpCodes.Call;i.operand=unload;count++;}
   if(count!=1)throw new InvalidOperationException("Area unload ResetZDO anchor changed: "+count);
   return code;
  }
  static void UnloadView(ZNetView view){Unloading(view);view.ResetZDO();}
  static void Unloading(ZNetView __instance){
   if(!Enabled||!R.Valid(__instance))return;var id=__instance.GetZDO().m_uid;
   var c=__instance.GetComponent<Container>();
   if(!Authority&&c&&c.GetInventory()!=null&&__instance.IsOwner()&&chests.ContainsKey(id)&&!Transport.Reserved(__instance.GetZDO())&&!c.IsInUse()){
    // Publish the last inventory together with release of ownership, through
    // vanilla's ordered replication, before the live component disappears.
    R.Call(c,"Save");__instance.GetZDO().SetOwner(0);ZDOMan.instance.ForceSendZDO(id);
   }
   if(requestedChests.Contains(id)){Enqueue(id);StorageIndex.Changed(R.Key(id));}
   if(nodes.ContainsKey(id)||chests.ContainsKey(id)||__instance.GetComponent<Container>())Topology.Dirty();
  }
  static void PinTransactions(List<ZDO> currentNearObjects){
   // Pending refunds need their live chest even when unloaded networks are disabled.
   foreach(var data in FastPath.HeldZDOs)if(data!=null&&data.IsValid()&&!currentNearObjects.Contains(data))currentNearObjects.Add(data);
   if(Enabled)foreach(var lease in Transport.Leases.Values)if(lease.Container&&!IsReplica(lease.Container)&&R.Valid(R.View(lease.Container))){var z=R.View(lease.Container).GetZDO();if(!currentNearObjects.Contains(z))currentNearObjects.Add(z);}
  }
  static void Enqueue(ZDOID id){if(!replicas.ContainsKey(id)&&queued.Add(id))queue.Enqueue(id);}
  internal static bool Prepare(){
   if(!Enabled)return true;demandUntil=Time.unscaledTime+.25f;
   UnloadedMultiplayer.Pulse();
   if(!bootstrapped)return false;
   if(UnloadedMultiplayer.Preparing)return false;
   if(nodesRequested!=structure){nodesRequested=structure;foreach(var id in nodes.Keys)if(!replicas.ContainsKey(id))Enqueue(id);}
   return !queued.Any(id=>nodes.ContainsKey(id));
  }
  internal static void Wake(){if(Enabled)demandUntil=Time.unscaledTime+.25f;}
  internal static void Request(Core core){
   if(!Enabled||!core)return;demandUntil=Time.unscaledTime+.25f;
   var member=core.GetComponent<NetworkMember>();string network=member.Network;
   if(requestedNetworks.TryGetValue(network,out long revision)&&revision==structure)return;
   requestedNetworks[network]=structure;
   foreach(var node in Topology.Graph.Nodes.Values.Where(n=>n.Network==network&&Topology.Graph.Hops.ContainsKey(n.Id))){
    var center=ZoneSystem.GetZone(new Vector3((float)node.Position.X,(float)node.Position.Y,(float)node.Position.Z));int radius=Mathf.CeilToInt((float)node.Storage/64)+1;
    for(int x=-radius;x<=radius;x++)for(int y=-radius;y<=radius;y++)if(chestSectors.TryGetValue(new Vector2s(center.x+x,center.y+y),out var ids))foreach(var id in ids){
     var z=chests[id];var prefab=RemoteContext.Prefab(z);if(!prefab||!ContainerPolicy.Eligible(prefab.name)||z.GetLong(ZDOVars.s_creator,0)==0||node.Position.Distance2(Topology.Position(z.GetPosition()))>node.Storage*node.Storage)continue;
     requestedChests.Add(id);
     // Retained live instances also need an owner after their zone
     // unloads. Keep their real inventory; an offline clone would duplicate it.
     ClaimUnowned(z);
     if(!ZNetScene.instance.FindInstance(id))Enqueue(id);
    }
   }
  }
  internal static void SettingsChanged(){if(!Enabled)return;structure++;requestedNetworks.Clear();wardAccess.Clear();}
  internal static void Tick(){
   if(!Demand)return;
   foreach(var id in ownerPending.Take(8).ToArray()){ownerPending.Remove(id);var z=RemoteContext.Data(id);if(z!=null)ClaimUnowned(z);}
   if(queue.Count==0)return;var clock=Stopwatch.StartNew();int budget=8;
   while(queue.Count>0&&budget-->0){var id=queue.Dequeue();queued.Remove(id);if(!replicas.ContainsKey(id)){
     var z=RemoteContext.Data(id);if(z!=null&&(nodes.ContainsKey(id)||requestedChests.Contains(id)))try{Create(z);}catch(Exception e){Plugin.Error("experimental adapter "+R.Key(id),e);}
    }if(clock.Elapsed.TotalMilliseconds>=.75)break;}
   if(queue.Count==0)Topology.Dirty();
  }
  static void Create(ZDO z){
   var prefab=RemoteContext.Prefab(z);if(!prefab||z.GetLong(ZDOVars.s_creator,0)==0)return;
   bool node=nodes.ContainsKey(z.m_uid);if(!node&&(!ContainerPolicy.Eligible(prefab.name)||!chestTypes.Contains(z.GetPrefab())))return;
   var go=new GameObject(prefab.name);go.SetActive(false);go.transform.position=z.GetPosition();go.transform.rotation=z.GetRotation();
   try{
    var replica=go.AddComponent<UnloadedReplica>();replica.Data=z;
    var view=go.AddComponent<ZNetView>();R.Set(view,"m_zdo",z);
    var piece=go.AddComponent<Piece>();R.Set(piece,"m_nview",view);R.Set(piece,"m_creator",z.GetLong(ZDOVars.s_creator,0));
    if(node){
     var member=go.AddComponent<NetworkMember>();member.View=view;
     if(prefab.GetComponent<Core>()){var core=go.AddComponent<Core>();R.Set(core,"view",view);Core.Live.Add(core);}
    }else{
     var original=prefab.GetComponent<Container>();if(!original||original.m_privacy!=Container.PrivacySetting.Public)throw new InvalidOperationException("unsupported offline container");
     var c=go.AddComponent<Container>();c.m_name=original.m_name;c.m_width=original.m_width;c.m_height=original.m_height;c.m_privacy=original.m_privacy;c.m_bkg=original.m_bkg;
     R.Set(c,"m_nview",view);R.Set(c,"m_piece",piece);R.Set(c,"m_inventory",new Inventory(c.m_name,c.m_bkg,c.m_width,c.m_height));
     ClaimUnowned(z);
    }
    replicas.Add(z.m_uid,go);
   }catch{R.Set(go.GetComponent<ZNetView>(),"m_zdo",null);UnityEngine.Object.Destroy(go);throw;}
  }
  static void ClaimUnowned(ZDO z){
   // Never steal a live peer's inventory or write from a client-side adapter.
   // Distant persistent objects can retain a disconnected peer as their owner.
   // Reservations survive that loss; only an unreserved source can be reclaimed.
   if(!Authority||Transport.Reserved(z))return;long owner=z.GetOwner();
   bool departed=owner!=0&&owner!=ZNet.GetUID()&&ZNet.instance.GetPeer(owner)==null;
   if(departed){z.SetOwner(ZNet.GetUID());z.Set(ZDOVars.s_inUse,0);}
   else if(owner==0&&z.GetInt(ZDOVars.s_inUse,0)==0)z.SetOwner(ZNet.GetUID());
  }
  internal static void Import(ZDOID id){
   if(!Enabled||Authority)return;var z=RemoteContext.Data(id);if(z==null)return;
   Record(z);if(nodes.ContainsKey(id))Enqueue(id);
   else if(chests.ContainsKey(id)){requestedChests.Add(id);if(!ZNetScene.instance.FindInstance(id))Enqueue(id);}
   StorageIndex.Changed(R.Key(id));
  }
  internal static void Forget(ZDOID id){if(!Authority)Destroyed(id);}
  internal static List<ZDOID> Export(Core core,long actor){
   var result=new HashSet<ZDOID>();if(!Authority)return result.ToList();
   // Topology-only records make pairing deterministic on clients too: a third
   // same-tag gateway or an independent nearby core must not disappear merely
   // because it isn't part of the currently usable inventory pool.
   if(nodes.Values.Any(z=>z.GetPrefab()=="RSN_RunicGateway".GetStableHashCode()))foreach(var z in nodes.Values)if(z.IsValid())result.Add(z.m_uid);
   var graph=Topology.ForActor(actor);var member=core?core.GetComponent<NetworkMember>():null;
   var network=member&&graph.Nodes.TryGetValue(member.Id,out var root)?root.Network:null;
   foreach(var node in graph.Nodes.Values.Where(n=>network!=null&&n.Network==network&&graph.Hops.ContainsKey(n.Id))){
    var nodeMember=Topology.Member(node.Id);if(nodeMember)result.Add(nodeMember.View.GetZDO().m_uid);
    var center=ZoneSystem.GetZone(new Vector3((float)node.Position.X,(float)node.Position.Y,(float)node.Position.Z));int radius=Mathf.CeilToInt((float)node.Storage/64)+1;
    for(int x=-radius;x<=radius;x++)for(int y=-radius;y<=radius;y++)if(chestSectors.TryGetValue(new Vector2s(center.x+x,center.y+y),out var ids))foreach(var id in ids){
     var z=chests[id];var prefab=RemoteContext.Prefab(z);
     if(prefab&&ContainerPolicy.Eligible(prefab.name)&&z.GetLong(ZDOVars.s_creator,0)!=0&&node.Position.Distance2(Topology.Position(z.GetPosition()))<=node.Storage*node.Storage&&Ward(z.GetPosition(),actor))result.Add(id);
    }
   }
   // Include only wards which can affect one of the advertised objects.
   var points=result.Select(id=>RemoteContext.Data(id)?.GetPosition()).Where(p=>p.HasValue).Select(p=>p.Value).ToArray();
   foreach(var z in wards.Values){var p=RemoteContext.Prefab(z);var ward=p?p.GetComponent<PrivateArea>():null;if(ward&&points.Any(point=>{var d=point-z.GetPosition();return d.x*d.x+d.z*d.z<ward.m_radius*ward.m_radius;}))result.Add(z.m_uid);}
   return result.OrderBy(R.Key,StringComparer.Ordinal).ToList();
  }
  internal static bool IsReplica(Component c)=>Enabled&&c&&c.GetComponent<UnloadedReplica>()&&R.Valid(R.View(c));
  internal static bool CanOwn(Container c)=>!IsReplica(c)||Authority;
  // A refused, unpaid write changed only the temporary inventory. Discard it
  // instead of overwriting newer saved data or holding the chest forever.
  internal static bool DiscardUnpaid(Container c,bool paid){
   if(!IsReplica(c)||paid)return false;var r=c.GetComponent<UnloadedReplica>();if(r.SavedAfterLoad)return false;
   r.Read=false;StorageIndex.Changed(R.Key(r.Data.m_uid));return true;
  }
  internal static void Released(Container c){if(IsReplica(c)){StorageIndex.Changed(R.Key(R.View(c).GetZDO().m_uid));Topology.Dirty();}}
  static bool ReadFailure(Container c,Exception e){
   if(!IsReplica(c))return false;var r=c.GetComponent<UnloadedReplica>();
   if(!r.ReportedFailure||r.FailedRevision!=r.Data.DataRevision){r.ReportedFailure=true;r.FailedRevision=r.Data.DataRevision;Plugin.Error("experimental inventory unavailable "+R.Key(r.Data.m_uid),e);}return true;
  }
  internal static bool LoadForRead(Container c){
   try{R.Call(c,"Load");return true;}
   catch(Exception e) when(ReadFailure(c,e)){return false;}
  }
  internal static bool CanUseUnloaded(Container c){
   if(!Enabled||!c||!R.Valid(R.View(c))||c.GetInventory()==null)return false;
   var z=R.View(c).GetZDO();
   if(!chests.TryGetValue(z.m_uid,out var saved)||!ReferenceEquals(saved,z)||!ReferenceEquals(RemoteContext.Data(z.m_uid),z))return false;
   // A live Container may outlast its zone (including containers retained by
   // another mod). Area readiness describes the entire zone, not this source.
   // Access/OwnerSource still enforce eligibility, placement, wards and leases.
   return IsReplica(c)||(ZNetScene.instance&&ZNetScene.instance.FindInstance(z.m_uid)==c.gameObject);
  }
  internal static bool SourceReady(Container c)=>CanUseUnloaded(c)||(c&&ZNetScene.instance&&ZNetScene.instance.IsAreaReady(c.transform.position));
  internal static bool KeepOwner(ZDO z,long owner)=>Enabled&&Authority&&z.GetOwner()==ZNet.GetUID()&&owner==0&&requestedChests.Contains(z.m_uid)&&chests.TryGetValue(z.m_uid,out var saved)&&ReferenceEquals(saved,z);
  internal static Core FindCore(ZDOID id){if(Enabled&&replicas.TryGetValue(id,out var go)&&go)return go.GetComponent<Core>();return null;}
  internal static Container FindContainer(ZDOID id){
   if(!Enabled)return CraftInspection.LoadedContainer(id);
   Container proxy=null;if(Enabled&&replicas.TryGetValue(id,out var go)&&go)proxy=go.GetComponent<Container>();
   if(proxy&&Transport.Leases.ContainsKey(proxy.GetInventory()))return proxy;
   if(Enabled&&Authority&&chests.TryGetValue(id,out var z))ClaimUnowned(z);
   var live=ZNetScene.instance?ZNetScene.instance.FindInstance(id)?.GetComponent<Container>():null;
   return live&&R.Valid(R.View(live))?live:proxy;
  }
  internal static bool Ward(Vector3 point,long actor){
   if(wardAccess.TryGetValue((point,actor),out bool cached))return cached;
   bool denied=false,allowed=false;
   foreach(var z in wards.Values){if(!z.IsValid()||!z.GetBool(ZDOVars.s_enabled,false))continue;var prefab=RemoteContext.Prefab(z);var ward=prefab?prefab.GetComponent<PrivateArea>():null;if(!ward)continue;
    var delta=z.GetPosition()-point;if(delta.x*delta.x+delta.z*delta.z>=ward.m_radius*ward.m_radius)continue;
    bool permitted=z.GetLong(ZDOVars.s_creator,0)==actor;int count=z.GetInt(ZDOVars.s_permitted,0);for(int i=0;i<count&&!permitted;i++)permitted=z.GetLong("pu_id"+i,0)==actor;
    if(permitted)allowed=true;else denied=true;
   }if(wardAccess.Count>=512)wardAccess.Clear();return wardAccess[(point,actor)]=allowed||!denied;
  }
  static bool LoadReplica(Container __instance,ref bool __result){
   if(!IsReplica(__instance))return true;__result=false;var r=__instance.GetComponent<UnloadedReplica>();
   if(Transport.Leases.ContainsKey(__instance.GetInventory())||__instance.IsInUse())return false;
   r.SavedAfterLoad=false;
   var bytes=r.Data.GetByteArray(ZDOVars.s_items)??Array.Empty<byte>();
   if(r.Read&&bytes.SequenceEqual(r.Expected))return false;
   r.Read=false;var inventory=__instance.GetInventory();
   // Never silently drop unknown or altered item records on an offline rewrite.
   OfflineInventory.Load(inventory,bytes);
   if(bytes.Length>0){var roundTrip=new ZPackage();inventory.Save(roundTrip);if(!Logic.InventoryRoundTrip.Preserved(bytes,roundTrip.GetArray(),s=>s.GetStableHashCode(),out var reason)){inventory.GetAllItems().Clear();r.Read=false;throw new InvalidOperationException("Offline inventory read refused: "+reason+"; saved chest data was not changed.");}}
   r.Expected=(byte[])bytes.Clone();r.Read=true;r.ReportedFailure=false;R.Set(__instance,"m_lastRevision",r.Data.DataRevision);__result=true;return false;
  }
  static bool SaveReplica(Container __instance){
   if(!IsReplica(__instance))return true;var r=__instance.GetComponent<UnloadedReplica>();
   if(!Authority||r.Data.GetOwner()!=ZNet.GetUID())throw new InvalidOperationException("Only the server owning an unloaded container may save it.");
   var current=r.Data.GetByteArray(ZDOVars.s_items)??Array.Empty<byte>();
   if(!r.Read||!current.SequenceEqual(r.Expected))throw new InvalidOperationException("Offline inventory changed before save; refusing stale overwrite.");
   var package=new ZPackage();__instance.GetInventory().Save(package);var bytes=package.GetArray();r.Data.Set(ZDOVars.s_items,bytes);r.SavedAfterLoad=true;r.Expected=(byte[])bytes.Clone();R.Set(__instance,"m_lastRevision",r.Data.DataRevision);return false;
  }
 }
}
