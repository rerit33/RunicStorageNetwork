#if GATEWAY_RUNTIME_TESTS
#pragma warning disable 0649
using System;
using System.Collections.Generic;
using System.Linq;
using RunicStorageNetwork;
using RunicStorageNetwork.Logic;
using UnityEngine;
namespace UnityEngine {
 public class Object {public static implicit operator bool(Object value)=>value!=null;}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
 public class Transform {public Vector3 position;}
 public class Component:Object {public Transform transform=new Transform();public virtual T GetComponent<T>() where T:class=>null;}
 public class GameObject:Object {internal ItemDrop Drop;public T GetComponent<T>() where T:class=>Drop as T;}
}
struct ZDOID {public string Id;public ZDOID(string id){Id=id;}}
class ZDO {
 public ZDOID m_uid;public int Prefab;public long Owner=15;public Vector3 Position;public string Identity,Tag="",Binding="",BuilderBinding;public int InventoryRevision;
 public bool IsValid()=>true;public int GetPrefab()=>Prefab;public long GetLong(int key,long fallback)=>1;public int GetInt(string key,int fallback)=>1;public Vector3 GetPosition()=>Position;
 public string GetString(string key,string fallback)=>key==Gateway.TagKey?Tag:key==Gateway.BindingKey?Binding:Identity??fallback;
 public void Set(string key,string value){Binding=value;UnloadedNetworks.CatalogRevision++;}public void SetOwner(long id){Owner=id;}
}
class ZDOMan {public static ZDOMan instance=new ZDOMan();public void ForceSendZDO(ZDOID id){}}
class ZNet {public static long GetUID()=>77;}
static class ZDOVars {public const int s_creator=1;}
static class Hashes {public static int GetStableHashCode(this string value){int hash=17;unchecked{foreach(char c in value)hash=hash*31+c;}return hash;}}
class ItemDrop:Component {public ItemData m_itemData=new ItemData();public class ItemData {public SharedData m_shared=new SharedData();}public class SharedData {public bool m_teleportable;public int m_toolTier;}}
class ZNetScene:UnityEngine.Object {public static ZNetScene instance=new ZNetScene();public readonly Dictionary<string,GameObject> Prefabs=new Dictionary<string,GameObject>();public GameObject GetPrefab(string id)=>Prefabs.TryGetValue(id,out var p)?p:null;}
enum GlobalKeys {TeleportAll}
class ZoneSystem:UnityEngine.Object {public static ZoneSystem instance=new ZoneSystem();public bool All;public bool GetGlobalKey(GlobalKeys key)=>All;}
class CraftingStation:Component {}
class Player:Component {public static Player m_localPlayer;public bool Building;public CraftingStation Station;public long GetPlayerID()=>1;public CraftingStation GetCurrentCraftingStation()=>Station;public bool InPlaceMode()=>Building;}
class Container:Component {}
namespace RunicStorageNetwork {
 static class ContentSettings {internal static bool GatewayEnabled=true;}
 static class BuilderCodex {internal static string ForOperation(Operation op)=>op.Build?RemoteContext.Data(op.Actor)?.BuilderBinding:null;internal static bool Building(long actor)=>false;}
 static class Gateway {internal const string PrefabName="RSN_RunicGateway",TagKey="tag",BindingKey="binding";}
 class NetworkMember {internal const string NetworkKey="network",SchemaKey="schema";internal string Id;}
 class Core:Component {internal NetworkMember Member;public override T GetComponent<T>()=>Member as T;}
 static class Plugin {internal class Number {internal float Value;public Number(float n){Value=n;}}internal class Setting {internal bool Value;public Setting(bool value){Value=value;}}internal static Setting GatewayCarriesAllItems=new Setting(false);internal static Number RelayLink=new Number(50),StorageRadius=new Number(20),SupplyRadius=new Number(20),RelayStorage=new Number(20),RelaySupply=new Number(20);}
 static class UnloadedNetworks {internal static bool Enabled=true,Authority=true;internal static long CatalogRevision;internal static List<ZDO> Records=new List<ZDO>();internal static IEnumerable<ZDO> ApiNodes=>Records;internal static bool Ward(Vector3 p,long actor)=>true;}
 static class R {internal static string Key(ZDOID id)=>id.Id;}
 static class Topology {internal static NetworkGraph Graph;internal static Point Position(Vector3 p)=>new Point(p.x,p.y,p.z);internal static NetworkGraph ForActor(long actor)=>GatewayRuntime.Graph(actor);}
 class Operation {internal ZDOID Core=new ZDOID("core"),Station=new ZDOID("station"),Actor=new ZDOID("actor");internal bool Build;internal long PlayerId=1;internal Vector3 Point=new Vector3(5000,0,0);}
 partial class RemoteContext {
  readonly Operation op;internal NetworkGraph Graph;Vector3 consumer;string builderBinding;string Network=>Graph.Nodes[R.Key(op.Core)].Network;
  internal RemoteContext(Operation value){op=value;}
  internal static ZDO Source(string key)=>GatewayRuntimeTests.sources.TryGetValue(key,out var z)?z:UnloadedNetworks.Records.FirstOrDefault(x=>x.m_uid.Id==key);
  internal static ZDO Data(ZDOID id)=>Source(id.Id);
  internal bool Validate(out string reason){reason="";Graph=GatewayRuntime.Graph(op.PlayerId);consumer=op.Point;builderBinding=BuilderCodex.ForOperation(op);return Connected(consumer);}
 }
}
static class GatewayRuntimeTests {
 static int passed;
 static ZDO Add(string id,float x,string prefab,string identity=null,string tag=""){var z=new ZDO{m_uid=new ZDOID(id),Position=new Vector3(x,0,0),Prefab=prefab.GetStableHashCode(),Identity=identity,Tag=tag};UnloadedNetworks.Records.Add(z);return z;}
 static void Item(string id,bool teleportable,int tier=0)=>ZNetScene.instance.Prefabs[id]=new GameObject{Drop=new ItemDrop{m_itemData=new ItemDrop.ItemData{m_shared=new ItemDrop.SharedData{m_teleportable=teleportable,m_toolTier=tier}}}};
 static void Check(bool value,string why){if(!value)throw new Exception(why);}
 static void Test(string name,Action body){GatewayRuntime.Clear();ContentSettings.GatewayEnabled=UnloadedNetworks.Enabled=UnloadedNetworks.Authority=true;Plugin.GatewayCarriesAllItems.Value=false;UnloadedNetworks.CatalogRevision=0;UnloadedNetworks.Records.Clear();ZDOMan.instance=new ZDOMan();ZoneSystem.instance.All=false;ZNetScene.instance.Prefabs.Clear();Add("core",0,"RSN_NetworkCore","A");Add("a",40,"RSN_RunicGateway",tag:"pair");Add("b",5000,"RSN_RunicGateway",tag:"pair");Item("Wood",true);Item("Iron",false);Item("CopperOre",false);body();passed++;Console.WriteLine("PASS gateway runtime "+name);}
 public static int Main(){try{
  Test("disabling gateways invalidates cached links without erasing saved endpoints",()=>{var before=GatewayRuntime.Graph(1);Check(before.GatewayPairs.Count==2,"pair absent");ContentSettings.GatewayEnabled=false;var off=GatewayRuntime.Graph(1);Check(!ReferenceEquals(before,off)&&!off.HasGateways&&!off.Nodes.ContainsKey("a")&&!off.Nodes.ContainsKey("b"),"disabled gateways still link or supply locally");Check(off.Nodes.ContainsKey("core")&&UnloadedNetworks.Records.Count==3&&UnloadedNetworks.Records[1].Binding=="A","core or saved binding removed");Check(!new RemoteContext(new Operation()).Validate(out _),"remote build/craft remains connected");ContentSettings.GatewayEnabled=true;Check(GatewayRuntime.Graph(1).GatewayPairs.Count==2,"pair not restored");});
  Test("server persists same binding on both endpoints and owns writes",()=>{var g=GatewayRuntime.Graph(1);Check(UnloadedNetworks.Records.Where(GatewayRuntime.Is).All(z=>z.Binding=="A"&&z.Owner==77),"unowned/nonpersistent gateway binding");Check(UnloadedNetworks.Records[0].Owner==15,"unrelated core ownership changed");});
  Test("recipe and inventory updates reuse the topology object",()=>{var a=GatewayRuntime.Graph(1);for(int i=0;i<2000;i++){UnloadedNetworks.Records[0].InventoryRevision++;Check(ReferenceEquals(a,GatewayRuntime.Graph(1)),"graph rebuilt with unchanged topology");}});
  Test("new actor has separate permissions snapshot but no new binding writes",()=>{var a=GatewayRuntime.Graph(1);long revision=UnloadedNetworks.CatalogRevision;var b=GatewayRuntime.Graph(2);Check(!ReferenceEquals(a,b)&&revision==UnloadedNetworks.CatalogRevision,"binding writes per actor");});
  Test("placing an unnamed second endpoint near another core does not bind it there",()=>{var b=UnloadedNetworks.Records[2];b.Tag="";Add("foreign",5000,"RSN_NetworkCore","B");GatewayRuntime.Graph(1);Check(b.Binding==""&&UnloadedNetworks.Records[1].Binding=="A","unnamed endpoint permanently bound before editing");b.Tag="pair";UnloadedNetworks.CatalogRevision++;var graph=GatewayRuntime.Graph(1);Check(b.Binding=="A"&&graph.GatewayPairs.Count==2&&graph.Nodes["b"].Network!=graph.Nodes["foreign"].Network,"second endpoint failed to inherit first network");});
  Test("tag edit invalidates cached bridge",()=>{var a=GatewayRuntime.Graph(1);UnloadedNetworks.Records[2].Tag="new";UnloadedNetworks.CatalogRevision++;var b=GatewayRuntime.Graph(1);Check(!ReferenceEquals(a,b)&&!b.Hops.ContainsKey("b"),"old link retained");});
  Test("setting off preserves ordinary portal rules",()=>{Item("Quest",true,1000);Check(GatewayRuntime.Teleportable("Wood")&&!GatewayRuntime.Teleportable("Iron")&&!GatewayRuntime.Teleportable("CopperOre")&&!GatewayRuntime.Teleportable("Missing"),"setting-off item property");ZoneSystem.instance.All=true;Check(GatewayRuntime.Teleportable("Iron")&&!GatewayRuntime.Teleportable("Quest"),"vanilla modifier rules");});
  Test("setting on carries ore and metal but preserves tier and missing-prefab rules",()=>{Plugin.GatewayCarriesAllItems.Value=true;Item("Quest",true,1000);Check(GatewayRuntime.Teleportable("Iron")&&GatewayRuntime.Teleportable("CopperOre")&&!GatewayRuntime.Teleportable("Quest")&&!GatewayRuntime.Teleportable("Missing"),"setting-on item property");});
  Test("consumer filtering keeps local metal and never mutates shared stock",()=>{var g=GatewayRuntime.Graph(1);var raw=new List<Stock>{new Stock("chest","Wood",1,20),new Stock("chest","Iron",1,100)};var remote=GatewayRuntime.Filter(g,g.Nodes["core"].Network,new Vector3(0,0,0),new Vector3(5000,0,0),raw).ToList();var local=GatewayRuntime.Filter(g,g.Nodes["core"].Network,new Vector3(0,0,0),new Vector3(0,0,0),raw).ToList();Check(remote.Count==1&&local.Count==2&&raw.Count==2&&raw[1].Amount==100,"shared catalogue contaminated by filtered view");});
  Test("world portal setting refreshes eligibility without rebuilding route",()=>{var g=GatewayRuntime.Graph(1);var raw=new[]{new Stock("chest","Iron",1,100)};Func<int> count=()=>GatewayRuntime.Filter(g,g.Nodes["core"].Network,new Vector3(0,0,0),new Vector3(5000,0,0),raw).Count();Check(count()==0,"metal before modifier");ZoneSystem.instance.All=true;Check(count()==1&&ReferenceEquals(g,GatewayRuntime.Graph(1)),"cached forbidden item after modifier");ZoneSystem.instance.All=false;Check(count()==0,"cached allowed item after modifier removed");});
  Test("coordinator rejects forbidden plan even after owner reports enough items",()=>{Add("chest",0,"piece_chest");UnloadedNetworks.Records.RemoveAt(3);var chest=new ZDO{m_uid=new ZDOID("chest"),Position=new Vector3(0,0,0)};sources["chest"]=chest;var context=new RemoteContext(new Operation());Check(context.Validate(out _),"consumer not connected");Check(context.Allows(new Debit("chest","Wood",1,1))&&!context.Allows(new Debit("chest","Iron",1,1)),"owner counts bypass path restriction");sources.Clear();});
  Test("disabled experiment bypasses inventory and path integration",()=>{UnloadedNetworks.Enabled=false;Topology.Graph=null;var raw=new[]{new Stock("chest","Iron",1,100)};Check(ReferenceEquals(GatewayRuntime.Local(null,1,null,raw),raw)&&GatewayRuntime.ValidatePlan(null,null),"disabled mode calls inventory/graph");});
  Test("final result check uses cached route at the actual craft or build point",()=>{sources["station"]=new ZDO{Position=new Vector3(5000,0,0)};sources["actor"]=new ZDO{Position=new Vector3(0,0,0)};sources["chest"]=new ZDO{Position=new Vector3(0,0,0)};var op=new Operation();var graph=GatewayRuntime.Graph(1);var plan=new[]{new Debit("chest","Iron",1,1)};Check(!GatewayRuntime.ValidatePlan(op,plan),"distant craft bypassed");op.Build=true;Check(GatewayRuntime.ValidatePlan(op,plan)&&ReferenceEquals(graph,GatewayRuntime.Graph(1)),"local build blocked or graph rebuilt");UnloadedNetworks.Records[2].Tag="different";UnloadedNetworks.CatalogRevision++;op.Build=false;Check(!GatewayRuntime.ValidatePlan(op,new[]{new Debit("chest","Wood",1,1)}),"deleted pair remained in cached route");sources.Clear();});
  Test("new world with same revision cannot reuse the previous graph",()=>{var a=GatewayRuntime.Graph(1);ZDOMan.instance=new ZDOMan();var b=GatewayRuntime.Graph(1);Check(!ReferenceEquals(a,b),"cross-world cache");});
  Test("server accepts only bound equipped wearable in extended build range",()=>{sources["actor"]=new ZDO{BuilderBinding="A"};var op=new Operation{Build=true,Point=new Vector3(-50,0,0)};Check(new RemoteContext(op).Validate(out _),"bound wearable rejected at 50m");sources["actor"].BuilderBinding="B";Check(!new RemoteContext(op).Validate(out _),"foreign binding accepted");sources["actor"].BuilderBinding="";Check(!new RemoteContext(op).Validate(out _),"unbound wearable accepted");sources["actor"].BuilderBinding=null;Check(!new RemoteContext(op).Validate(out _),"removed book retained extended range");sources.Clear();});
  Test("wearable does not extend ordinary crafting on server",()=>{sources["actor"]=new ZDO{BuilderBinding="A"};Check(!new RemoteContext(new Operation{Build=false,Point=new Vector3(-40,0,0)}).Validate(out _),"craft received wearable range");sources.Clear();});
  Test("extended remote build observes gateway restrictions at both payment and output",()=>{Add("relay",5040,"RSN_RunicRelay");sources["actor"]=new ZDO{BuilderBinding="A",Position=new Vector3(5090,0,0)};sources["chest"]=new ZDO{Position=new Vector3(0,0,0)};var op=new Operation{Build=true,Point=new Vector3(5090,0,0)};var context=new RemoteContext(op);Check(context.Validate(out _),"remote wearable disconnected");var wood=new Debit("chest","Wood",1,1);var iron=new Debit("chest","Iron",1,1);Check(context.Allows(wood)&&!context.Allows(iron)&&GatewayRuntime.ValidatePlan(op,new[]{wood})&&!GatewayRuntime.ValidatePlan(op,new[]{iron}),"gateway restriction differs between payment/output");sources["actor"].BuilderBinding="B";Check(!GatewayRuntime.ValidatePlan(op,new[]{wood}),"rebound book accepted old payment");sources.Clear();});
  Console.WriteLine("Gateway runtime tests: "+passed+" passed (game stand-ins)");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 internal static readonly Dictionary<string,ZDO> sources=new Dictionary<string,ZDO>();
}
#endif
