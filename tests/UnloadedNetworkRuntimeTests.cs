#if UNLOADED_NETWORK_RUNTIME_TESTS
#pragma warning disable 0649,0414
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using RunicStorageNetwork;
using UnityEngine;
// Only Unity/game primitives are stand-ins. The registry, inactive adapters,
// request queue, source selection and save guards are the production class.
namespace UnityEngine {
 public class Object {internal bool Dead;public static implicit operator bool(Object o)=>o!=null&&!o.Dead;public static void Destroy(Object o){if(o!=null)o.Dead=true;}}
 public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();}
 public class MonoBehaviour:Component {}
 public class Transform {public Vector3 position;public Quaternion rotation;}
 public class GameObject:Object {
  readonly List<Component> components=new List<Component>();public string name;public bool activeSelf=true;public Transform transform=new Transform();
  public GameObject(string name){this.name=name;}public void SetActive(bool active){activeSelf=active;}
  public T AddComponent<T>() where T:Component,new(){if(activeSelf)throw new Exception("Adapter must be inactive before components are added");var c=new T{gameObject=this};components.Add(c);return c;}
  public T GetComponent<T>() where T:Component=>components.OfType<T>().FirstOrDefault();
 }
 public struct Vector3 {public float x,y,z;public float sqrMagnitude=>x*x+y*y+z*z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}
 public struct Quaternion {}
 public static class Mathf {public static int CeilToInt(float n)=>(int)Math.Ceiling(n);}
 public static class Time {public static float unscaledTime;}
}
namespace HarmonyLib {
 public enum MethodType { Normal }
 public static class Priority {public const int First=0,Last=1;}
 public class HarmonyMethod {public int priority;internal MethodInfo Method;public HarmonyMethod(Type t,string n){Method=t.GetMethod(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);}}
 public class CodeInstruction {public OpCode opcode;public object operand;public readonly List<Label> labels=new List<Label>();public readonly List<object> blocks=new List<object>();public CodeInstruction(OpCode code,object value=null){opcode=code;operand=value;}}
 public class Harmony {
  public static int Lookups,Attempts,FailAt;public static readonly List<string> Owners=new List<string>();readonly string id;
  internal static readonly List<(string Owner,MethodInfo Target,HarmonyMethod Prefix,HarmonyMethod Transpiler)> Bindings=new List<(string,MethodInfo,HarmonyMethod,HarmonyMethod)>();
  public Harmony(string id){this.id=id;}public void Patch(MethodInfo m,HarmonyMethod prefix=null,HarmonyMethod postfix=null,HarmonyMethod transpiler=null){Owners.Add(id);Bindings.Add((id,m,prefix,transpiler));if(++Attempts==FailAt)throw new InvalidOperationException("simulated patch failure");}
  public void UnpatchSelf(){Owners.RemoveAll(owner=>owner==id);Bindings.RemoveAll(b=>b.Owner==id);}
  internal static void Prefix(Type type,string method,object arg){foreach(var b in Bindings.Where(b=>b.Target.DeclaringType==type&&b.Target.Name==method&&b.Prefix!=null).ToArray())b.Prefix.Method.Invoke(null,new[]{arg});}
  internal static void AreaReset(ZNetView view){
   IEnumerable<CodeInstruction> code=new[]{new CodeInstruction(OpCodes.Callvirt,typeof(ZNetView).GetMethod("ResetZDO"))};
   foreach(var b in Bindings.Where(b=>b.Target.DeclaringType==typeof(ZNetScene)&&b.Target.Name=="RemoveObjects"&&b.Transpiler!=null))code=(IEnumerable<CodeInstruction>)b.Transpiler.Method.Invoke(null,new object[]{code});
   var reset=(MethodInfo)code.Single().operand;reset.Invoke(reset.IsStatic?null:view,reset.IsStatic?new object[]{view}:null);
  }
 }
 public static class AccessTools {public static MethodInfo Method(Type t,string n){Harmony.Lookups++;return t.GetMethod(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static);}public static MethodInfo Method(Type t,string n,Type[] a){Harmony.Lookups++;return t.GetMethod(n,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static,null,a,null);}}
}
static class Strings {public static int GetStableHashCode(this string s){unchecked {int h=17;foreach(char c in s)h=h*31+c;return h;}}}
struct ZDOID {internal int Id;public static ZDOID None=>default;public static bool operator ==(ZDOID a,ZDOID b)=>a.Id==b.Id;public static bool operator !=(ZDOID a,ZDOID b)=>a.Id!=b.Id;public override bool Equals(object o)=>o is ZDOID v&&this==v;public override int GetHashCode()=>Id;}
struct Vector2s {public int x,y;public Vector2s(int x,int y){this.x=x;this.y=y;}public static bool operator ==(Vector2s a,Vector2s b)=>a.x==b.x&&a.y==b.y;public static bool operator !=(Vector2s a,Vector2s b)=>!(a==b);public override bool Equals(object o)=>o is Vector2s v&&v==this;public override int GetHashCode()=>x*397^y;}
static class ZoneSystem {public static Vector2s GetZone(Vector3 p)=>new Vector2s((int)Math.Floor(p.x/64),(int)Math.Floor(p.z/64));}
static class ZDOVars {public const int s_creator=1,s_enabled=2,s_permitted=3,s_items=4,s_inUse=5;}
class ZDO {
 public ZDOID m_uid;public uint DataRevision;public ushort OwnerRevision;public long Owner=7,Creator=1,PlayerId=1;public int Schema=1,Prefab,InUse;public bool Valid=true,Guard;
 public string Network="network",BuilderBinding;public Vector3 Position;public byte[] Bytes=Array.Empty<byte>();
 public bool IsValid()=>Valid;public int GetPrefab()=>Prefab;public Vector3 GetPosition()=>Position;public Quaternion GetRotation()=>new Quaternion();
 public long GetLong(int key,long fallback)=>Creator;public long GetLong(string key,long fallback)=>fallback;
 public int GetInt(string key,int fallback)=>Schema;public int GetInt(int key,int fallback)=>key==ZDOVars.s_inUse?InUse:0;
 public bool GetBool(int key,bool fallback)=>Guard;public string GetString(string key,string fallback)=>Network;
 public long GetOwner()=>Owner;public void SetOwner(long id){Owner=id;OwnerRevision++;}public byte[] GetByteArray(int key)=>Bytes;
 public void Set(int key,byte[] bytes){Bytes=bytes;DataRevision++;}
 public void Set(int key,int value){if(key==ZDOVars.s_inUse)InUse=value;DataRevision++;}
 public void IncreaseDataRevision(){}public void Deserialize(ZPackage p){}public void SetOwnerInternal(long owner){Owner=owner;}
}
class ZDOMan {public static ZDOMan instance;internal Dictionary<ZDOID,ZDO> m_objectsByID=new Dictionary<ZDOID,ZDO>();internal List<(long Peer,ZDOID Id)> Sent=new List<(long,ZDOID)>();public void ForceSendZDO(long peer,ZDOID id)=>Sent.Add((peer,id));public void ForceSendZDO(ZDOID id)=>Sent.Add((Transport.Server,id));public void AddToSector(ZDO z){}public void HandleDestroyedZDO(ZDOID id){HarmonyLib.Harmony.Prefix(typeof(ZDOMan),nameof(HandleDestroyedZDO),id);if(ZNetScene.instance.Live.TryGetValue(id,out var live)){live.GetComponent<ZNetView>().ResetZDO();ZNetScene.instance.Live.Remove(id);UnityEngine.Object.Destroy(live);}m_objectsByID.Remove(id);}public void RemovePeer(ZNetPeer peer){}}
class ZNetPeer {public long m_uid;}
class ZNet:UnityEngine.Object {public static ZNet instance;public static bool m_loadError;internal bool Server=true;internal HashSet<long> Peers=new HashSet<long>{17};public ZNetPeer GetPeer(long id)=>Peers.Contains(id)?new ZNetPeer{m_uid=id}:null;public bool IsServer()=>Server;public static long GetUID()=>instance.Server?7:17;public void Start(){}}
class ZNetView:MonoBehaviour {internal ZDO m_zdo;public ZDO GetZDO()=>m_zdo;public bool IsOwner()=>m_zdo.Owner==ZNet.GetUID();public void Awake(){}public void ResetZDO(){HarmonyLib.Harmony.Prefix(typeof(ZNetView),nameof(ResetZDO),this);m_zdo=null;}}
class Player:UnityEngine.Object {public static Player m_localPlayer;internal ZDOID Id;public ZDOID GetZDOID()=>Id;public long GetPlayerID()=>1;}
class ZNetScene:UnityEngine.Object {
 public static ZNetScene instance;public List<GameObject> m_prefabs=new List<GameObject>();public Dictionary<ZDOID,GameObject> Live=new Dictionary<ZDOID,GameObject>();
 public GameObject FindInstance(ZDOID id)=>Live.TryGetValue(id,out var g)?g:null;
 public bool AreaLoaded=true;public bool IsAreaReady(Vector3 p)=>AreaLoaded;
 public void Awake(){}public void OnDestroy(){}
 public void RemoveObjects(List<ZDO> currentNearObjects,List<ZDO> currentDistantObjects){
  HarmonyLib.Harmony.Prefix(typeof(ZNetScene),nameof(RemoveObjects),currentNearObjects);
  foreach(var entry in Live.ToArray()){var view=entry.Value.GetComponent<ZNetView>();if(currentNearObjects.Contains(view.GetZDO())||currentDistantObjects.Contains(view.GetZDO()))continue;HarmonyLib.Harmony.AreaReset(view);Live.Remove(entry.Key);UnityEngine.Object.Destroy(entry.Value);}
 }
 // Installed Valheim checks ownership AFTER ResetZDO. Keep that order here:
 // the regression is a surviving world record despite already returned materials.
 public void Destroy(GameObject go){var view=go.GetComponent<ZNetView>();var z=view.GetZDO();view.ResetZDO();Live.Remove(z.m_uid);if(z.Owner==ZNet.GetUID())ZDOMan.instance.HandleDestroyedZDO(z.m_uid);UnityEngine.Object.Destroy(go);}
}
class Piece:MonoBehaviour {internal ZNetView m_nview;internal long m_creator;}
class PrivateArea:MonoBehaviour {public float m_radius=32;}
class Container:MonoBehaviour {
 public enum PrivacySetting {Public,Private}
 public string m_name;public int m_width=4,m_height=2;public object m_bkg;public PrivacySetting m_privacy;
 internal ZNetView m_nview;internal Piece m_piece;internal Inventory m_inventory;internal uint m_lastRevision;
 public Inventory GetInventory()=>m_inventory;internal bool Open;internal Action Loader;public bool IsInUse()=>Open;internal bool Load(){Loader?.Invoke();return true;}internal void Save(){GetComponent<ZNetView>().GetZDO().Set(ZDOVars.s_items,m_inventory.Bytes);}
}
class ZPackage {
 MemoryStream stream=new MemoryStream();BinaryReader reader;BinaryWriter writer;
 public ZPackage(){reader=new BinaryReader(stream);writer=new BinaryWriter(stream);}public ZPackage(byte[] data):this(){Put(data);}
 public byte[] GetArray()=>stream.ToArray();internal void Put(byte[] data){stream.SetLength(0);stream.Write(data,0,data.Length);stream.Position=0;}
 public int Size()=>(int)stream.Length;
 public void Write(int n)=>writer.Write(n);public void Write(long n)=>writer.Write(n);public void Write(bool b)=>writer.Write(b);public void Write(ZDOID id)=>Write(id.Id);public void Write(Vector3 v){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
 public int ReadInt()=>reader.ReadInt32();public long ReadLong()=>reader.ReadInt64();public bool ReadBool()=>reader.ReadBoolean();public ZDOID ReadZDOID()=>new ZDOID{Id=ReadInt()};public Vector3 ReadVector3()=>new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
}
class Inventory {
 internal byte[] Bytes=Array.Empty<byte>();internal static bool DropUnknown;internal static Func<byte[],byte[]> Normalize;
 public Inventory(string n,object b,int w,int h){}public void Load(ZPackage p){Bytes=(byte[])p.GetArray().Clone();if(DropUnknown)Bytes=new byte[]{0};if(Normalize!=null)Bytes=Normalize(Bytes);}
 public void Save(ZPackage p){p.Put((byte[])Bytes.Clone());}public void RemoveAll(){Bytes=Array.Empty<byte>();}
 public Inventory GetAllItems()=>this;public void Clear()=>RemoveAll();
}
namespace RunicStorageNetwork {
 static class OfflineInventory {internal static void Load(Inventory inv,byte[] bytes){if(bytes.Length==0)inv.RemoveAll();else inv.Load(new ZPackage(bytes));}}
 static class BuilderCodex {internal static string RemoteBinding(ZDO z)=>z?.BuilderBinding;}
 class ConfigEntry {public bool Value;}
 static class Plugin {internal const string Guid="local.runicstoragenetwork";internal static bool Healthy=true;internal static ConfigEntry ExperimentalUnloadedNetworks=new ConfigEntry();internal static List<string> Messages=new List<string>();internal static void Info(string s)=>Messages.Add(s);internal static void Error(string s,Exception e){Messages.Add(s+": "+e.Message);}}
 class Core:MonoBehaviour {internal static HashSet<Core> Live=new HashSet<Core>();internal ZNetView view;internal void Detach(){Live.Remove(this);}}
 class NetworkMember:MonoBehaviour {internal const string NetworkKey="network",SchemaKey="schema";internal ZNetView View;internal bool Valid=>R.Valid(View);internal string Id=>R.Key(View.GetZDO().m_uid);internal string Network=>View.GetZDO().Network;}
 static class R {
  internal static T Get<T>(object o,string key)=>(T)o.GetType().GetField(key,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
  internal static void Set(object o,string key,object value)=>o.GetType().GetField(key,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);
  internal static object Call(object o,string key)=>o.GetType().GetMethod(key,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,null);
  internal static ZNetView View(Component c)=>c?c.GetComponent<ZNetView>():null;
  internal static bool Valid(ZNetView v)=>v&&v.GetZDO()!=null&&v.GetZDO().IsValid();internal static string Key(ZDOID id)=>id.Id.ToString();
 }
 static class ContainerPolicy {internal static bool Eligible(string name)=>name!="ExcludedChest";}
 static class RemoteContext {
  internal static ZDO Data(ZDOID id)=>ZDOMan.instance.m_objectsByID.TryGetValue(id,out var z)&&z.Valid?z:null;
  internal static GameObject Prefab(ZDO z)=>ZNetScene.instance.m_prefabs.FirstOrDefault(p=>p.name.GetStableHashCode()==z.Prefab);
  internal static bool Actor(ZDOID id,long peer,long player,out ZDO z,out string reason){z=Data(id);reason="";return z!=null&&z.Owner==peer&&z.PlayerId==player;}
 }
 static class StorageIndex {internal sealed class UnloadedMode {internal UnloadedMode(Func<bool> enabled,Func<bool> demand,Func<Container,bool> ready,Func<Container,Exception,bool> readFailure){}internal void Suspend(){}}internal static UnloadedMode Offline;internal static void Changed(string key){}}
 static class CraftInspection {internal static Container LoadedContainer(ZDOID id)=>ZNetScene.instance.FindInstance(id)?.GetComponent<Container>();internal static Func<ZDOID,Container> FindContainer=LoadedContainer;}
 class Number {public double X,Y,Z;public double Distance2(Number p)=>Math.Pow(X-p.X,2)+Math.Pow(Y-p.Y,2)+Math.Pow(Z-p.Z,2);}
 class Node {internal string Id,Network;internal Number Position;internal double Storage=20;}
 class Graph {internal Dictionary<string,Node> Nodes=new Dictionary<string,Node>();internal Dictionary<string,int> Hops=new Dictionary<string,int>();}
 static partial class Topology {
  internal static HashSet<NetworkMember> Members=new HashSet<NetworkMember>();internal static Graph Graph=new Graph();internal static int Changes;internal static void Dirty(){Changes++;}internal static Number Position(Vector3 p)=>new Number{X=p.x,Y=p.y,Z=p.z};
  internal static Graph ForActor(long actor)=>Graph;internal static NetworkMember Member(string id)=>UnloadedNetworks.Members.FirstOrDefault(m=>m.Id==id);
  internal static Vector3 ChosenPoint;internal static string ChosenBinding;
  internal static void Refresh(){}internal static Core Choose(Vector3 at,long actor,string binding=null){ChosenPoint=at;ChosenBinding=binding;return binding==""?null:Core.Live.FirstOrDefault();}
 }
 static class Transport {
  internal class Lease {internal Container Container;}internal static Dictionary<Inventory,Lease> Leases=new Dictionary<Inventory,Lease>();
  internal static bool Reserved(ZDO z)=>Leases.Values.Any(l=>R.View(l.Container)?.GetZDO()==z);
  internal static long Server=7;internal static List<(long Peer,string Name,byte[] Bytes)> Sent=new List<(long,string,byte[])>();
  internal static void Send(long peer,string name,ZPackage p)=>Sent.Add((peer,name,p.GetArray()));
 }
 static class FastPath {internal static readonly HashSet<ZDO> Held=new HashSet<ZDO>();internal static IEnumerable<ZDO> HeldZDOs=>Held;}
}
static class UnloadedNetworkRuntimeTests {
 static int passed;static ZDO core,chest;static GameObject chestPrefab;
 static object Call(string method,params object[] args){try{return typeof(UnloadedNetworks).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
 static void Check(bool value,string why){if(!value)throw new Exception(why);}
 static GameObject Prefab<T>(string name) where T:Component,new(){var g=new GameObject(name);g.SetActive(false);g.AddComponent<T>();ZNetScene.instance.m_prefabs.Add(g);return g;}
 static ZDO Record(string name,int id,float x){var z=new ZDO{m_uid=new ZDOID{Id=id},Prefab=name.GetStableHashCode(),Position=new Vector3(x,0,0)};ZDOMan.instance.m_objectsByID[z.m_uid]=z;return z;}
 static void Setup(){
  UnloadedNetworks.Uninstall();HarmonyLib.Harmony.Owners.Clear();HarmonyLib.Harmony.Bindings.Clear();HarmonyLib.Harmony.Lookups=HarmonyLib.Harmony.Attempts=HarmonyLib.Harmony.FailAt=0;handlers.Clear();Plugin.Healthy=true;ZNet.instance=new ZNet();ZNet.m_loadError=false;ZDOMan.instance=new ZDOMan();ZNetScene.instance=new ZNetScene();Plugin.ExperimentalUnloadedNetworks.Value=true;Plugin.Messages.Clear();Transport.Leases.Clear();Transport.Sent.Clear();FastPath.Held.Clear();Player.m_localPlayer=null;Core.Live.Clear();Topology.Members.Clear();Topology.Graph=new Graph();Topology.Changes=0;Time.unscaledTime=0;Inventory.DropUnknown=false;
  Prefab<Core>("RSN_NetworkCore");Prefab<NetworkMember>("RSN_RunicRelay");chestPrefab=Prefab<Container>("ModdedDrawer");Prefab<Container>("ExcludedChest");
  core=Record("RSN_NetworkCore",1,0);chest=Record("ModdedDrawer",2,5);chest.Bytes=new byte[]{109,3,7,8};
 }
 static void Boot(){var stable=new HarmonyLib.Harmony(Plugin.Guid);stable.UnpatchSelf();UnloadedNetworks.InstallPinning(stable);UnloadedNetworks.Install();Call("Bootstrap");}
 static void Start(){Boot();Call("WorldLoaded");UnloadedNetworks.Prepare();for(int i=0;i<20;i++)UnloadedNetworks.Tick();}
 static Container LiveChest(){var g=new GameObject("ModdedDrawer");g.SetActive(false);g.AddComponent<ZNetView>().m_zdo=chest;var c=g.AddComponent<Container>();c.m_inventory=new Inventory("",null,4,2);ZNetScene.instance.Live[chest.m_uid]=g;return c;}
 static Container Request(){
  var c=UnloadedNetworks.FindCore(core.m_uid);Topology.Graph.Nodes["1"]=new Node{Id="1",Network="network",Position=Topology.Position(core.Position)};Topology.Graph.Hops["1"]=0;
  UnloadedNetworks.Request(c);for(int i=0;i<20;i++)UnloadedNetworks.Tick();return UnloadedNetworks.FindContainer(chest.m_uid);
 }
 static void Load(Container c){var args=new object[]{c,false};Call("LoadReplica",args);}
 static bool Refuses(Action work){try{work();return false;}catch(InvalidOperationException){return true;}}
 static readonly Dictionary<string,Action<long,ZPackage>> handlers=new Dictionary<string,Action<long,ZPackage>>();
 static void Receive(string name,long sender,ZPackage p){if(handlers.Count==0)UnloadedMultiplayer.Register((key,handle)=>handlers[key]=handle);handlers[name](sender,new ZPackage(p.GetArray()));}
 static ZDO Actor(){var z=Record("Player",10,0);z.Owner=17;return z;}
 static void Query(int token=1,Vector3 at=default,long sender=17,long player=1,bool building=false){var p=new ZPackage();p.Write(token);p.Write(new ZDOID{Id=10});p.Write(player);p.Write(at);p.Write(building);Receive("unloaded_query",sender,p);}
 static void Client(){ZNet.instance.Server=false;Actor();Player.m_localPlayer=new Player{Id=new ZDOID{Id=10}};Start();UnloadedMultiplayer.Touch(default,1);}
 static ZPackage Catalog(int generation,int page,params ZDO[] records){
  var p=new ZPackage();p.Write(1);p.Write(true);p.Write(generation);p.Write(page);p.Write(Math.Max(1,(records.Length+63)/64));p.Write(records.Length);
  var slice=records.Skip(page*64).Take(64).ToArray();p.Write(slice.Length);foreach(var z in slice){p.Write(z.m_uid);p.Write((long)z.DataRevision);p.Write((int)z.OwnerRevision);}return p;
 }
 static void Deliver(params ZDO[] records){Receive("unloaded_catalog",7,Catalog(1,0,records));UnloadedMultiplayer.Tick();for(int i=0;i<20;i++)UnloadedNetworks.Tick();}
 static void Test(string name,Action test){Setup();test();passed++;Console.WriteLine("PASS unloaded runtime "+name);}
 public static int Main(){try{
  Test("disabled setting creates no adapters",()=>{Plugin.ExperimentalUnloadedNetworks.Value=false;Call("Bootstrap");UnloadedNetworks.Prepare();UnloadedNetworks.Tick();Check(!UnloadedNetworks.Enabled&&Core.Live.Count==0,"disabled mode changed world");});
  Test("multiplayer server can index the saved world",()=>{Start();Check(UnloadedNetworks.Enabled&&Request(),"server cannot discover saved storage");});
  Test("cold start finds core without any live instances",()=>{Start();Check(UnloadedNetworks.FindCore(core.m_uid)&&ZNetScene.instance.Live.Count==0,"requires loaded core");Check(Core.Live.All(c=>!c.gameObject.activeSelf),"active adapter");});
  Test("chunked save arriving after scene Awake is indexed without visiting core",()=>{ZDOMan.instance.m_objectsByID.Clear();Boot();Check(!UnloadedNetworks.Prepare(),"indexed before save loaded");ZDOMan.instance.m_objectsByID[core.m_uid]=core;ZDOMan.instance.m_objectsByID[chest.m_uid]=chest;Call("WorldLoaded");UnloadedNetworks.Prepare();for(int i=0;i<20;i++)UnloadedNetworks.Tick();Check(UnloadedNetworks.FindCore(core.m_uid)&&Request()&&ZNetScene.instance.Live.Count==0,"late saved records missed");});
  Test("completed world index is not rebuilt on repeated notification",()=>{Start();int before=Topology.Changes;Call("WorldLoaded");Check(Topology.Changes==before,"repeated full scan");});
  Test("failed world load cannot index a partial save",()=>{ZNet.m_loadError=true;Start();Check(!UnloadedNetworks.Prepare()&&!UnloadedNetworks.FindCore(core.m_uid),"indexed failed load");});
  Test("startup indexes metadata without eagerly creating storage",()=>{Start();Check(!UnloadedNetworks.FindContainer(chest.m_uid),"eager storage load");});
  Test("modded standard containers share the normal eligibility filter",()=>{Start();var c=Request();Check(c&&c.gameObject.name=="ModdedDrawer"&&!c.gameObject.activeSelf,"modded storage excluded");});
  Test("denied container is not connected",()=>{chest.Prefab="ExcludedChest".GetStableHashCode();Start();Check(!Request(),"excluded storage bypassed");});
  Test("unplaced nodes cannot stall discovery",()=>{var ghost=Record("RSN_RunicRelay",3,10);ghost.Creator=0;Start();Check(UnloadedNetworks.Prepare(),"unplaced record blocked initialization");});
  Test("idle work queue does not create adapters",()=>{Start();var c=UnloadedNetworks.FindCore(core.m_uid);Topology.Graph.Nodes["1"]=new Node{Id="1",Network="network",Position=Topology.Position(core.Position)};Topology.Graph.Hops["1"]=0;UnloadedNetworks.Request(c);Time.unscaledTime=1;UnloadedNetworks.Tick();Check(!UnloadedNetworks.FindContainer(chest.m_uid),"idle queue ran");});
  Test("ordinary node data revision does not rebuild graph",()=>{Start();int before=Topology.Changes;core.DataRevision++;Call("Changed",core);Check(Topology.Changes==before,"health/other data dirtied graph");});
  Test("node movement updates retained position and invalidates graph",()=>{Start();int before=Topology.Changes;core.Position=new Vector3(40,0,0);Call("Changed",core);Check(Topology.Changes>before&&UnloadedNetworks.FindCore(core.m_uid).transform.position.x==40,"movement missed");});
  Test("offline inventory round trip preserves all bytes",()=>{Start();var c=Request();Load(c);Check(c.GetInventory().Bytes.SequenceEqual(chest.Bytes),"metadata changed");});
  Test("remote legacy inventory normalizes without writing during reads",()=>{var item=new InventoryRoundTripTests.Record{Prefab="Wood"};chest.Bytes=InventoryRoundTripTests.Encode(107,item);var original=(byte[])chest.Bytes.Clone();var normalized=InventoryRoundTripTests.Encode(109,item);Inventory.Normalize=_=>normalized;try{Start();var c=Request();Load(c);Check(chest.Bytes.SequenceEqual(original)&&c.GetInventory().Bytes.SequenceEqual(normalized)&&c.GetComponent<UnloadedReplica>().Read,"read overwrote source or normalization was refused");Call("SaveReplica",c);Check(chest.Bytes.SequenceEqual(normalized),"validated write failed");}finally{Inventory.Normalize=null;}});
  Test("normalized remote inventory still rejects a concurrent writer",()=>{var item=new InventoryRoundTripTests.Record();chest.Bytes=InventoryRoundTripTests.Encode(109,item);var original=(byte[])chest.Bytes.Clone();item.Data=item.Data.Reverse().ToDictionary(p=>p.Key,p=>p.Value);Inventory.Normalize=_=>InventoryRoundTripTests.Encode(109,item);try{Start();var c=Request();Load(c);chest.Bytes=original.Concat(new byte[]{1}).ToArray();var changed=(byte[])chest.Bytes.Clone();Check(Refuses(()=>Call("SaveReplica",c))&&chest.Bytes.SequenceEqual(changed),"normalized data bypassed concurrent-write guard");}finally{Inventory.Normalize=null;}});
  Test("unknown or lossy inventory format refuses offline read",()=>{Start();var c=Request();var before=(byte[])chest.Bytes.Clone();Inventory.DropUnknown=true;Check(Refuses(()=>Load(c))&&chest.Bytes.SequenceEqual(before),"lossy read wrote data");});
  Test("failed remote read returns refusal and logs once per revision",()=>{Start();var c=Request();c.Loader=()=>Load(c);Inventory.DropUnknown=true;int messages=Plugin.Messages.Count;for(int i=0;i<3;i++)Check(!UnloadedNetworks.LoadForRead(c),"failed read reported successful");Check(Plugin.Messages.Count==messages+1&&!c.GetComponent<UnloadedReplica>().Read,"repeated logs or unreadable snapshot accepted");Inventory.DropUnknown=false;Check(UnloadedNetworks.LoadForRead(c)&&c.GetComponent<UnloadedReplica>().Read,"retry did not recover");});
  Test("successful offline save updates authoritative record",()=>{Start();var c=Request();Load(c);c.GetInventory().Bytes=new byte[]{109,2,7,8};Call("SaveReplica",c);Check(chest.Bytes.SequenceEqual(new byte[]{109,2,7,8}),"not saved");});
  Test("concurrent record change refuses stale overwrite",()=>{Start();var c=Request();Load(c);chest.Bytes=new byte[]{109,1,4};Check(Refuses(()=>Call("SaveReplica",c))&&chest.Bytes.SequenceEqual(new byte[]{109,1,4}),"overwrote external change");});
  Test("unpaid conflict can release without overwriting saved items",()=>{Start();var c=Request();Load(c);chest.Bytes=new byte[]{109,1,4};c.GetInventory().Bytes=new byte[]{109,2,7};Check(Refuses(()=>Call("SaveReplica",c))&&UnloadedNetworks.DiscardUnpaid(c,false),"failed write stayed locked");Load(c);Check(c.GetInventory().Bytes.SequenceEqual(chest.Bytes),"discarded inventory was reused");});
  Test("saved debit cannot be discarded even before acknowledgement",()=>{Start();var c=Request();Load(c);c.GetInventory().Bytes=new byte[]{109,2,7};Call("SaveReplica",c);Check(!UnloadedNetworks.DiscardUnpaid(c,false)&&!UnloadedNetworks.DiscardUnpaid(c,true),"persisted debit skipped rollback");});
  Test("loaded chest takes over without duplicate source",()=>{Start();var proxy=Request();var live=new GameObject("ModdedDrawer");live.SetActive(false);live.AddComponent<ZNetView>().m_zdo=chest;var c=live.AddComponent<Container>();c.m_inventory=new Inventory("",null,4,2);ZNetScene.instance.Live[chest.m_uid]=live;Check(UnloadedNetworks.FindContainer(chest.m_uid)==c&&!UnloadedNetworks.Containers.Contains(proxy),"duplicate adapters");});
  Test("retained live chest remains usable after its zone unloads",()=>{Start();var live=LiveChest();ZNetScene.instance.AreaLoaded=false;chest.Owner=0;Check(Request()==live&&UnloadedNetworks.CanUseUnloaded(live)&&UnloadedNetworks.SourceReady(live),"live chest rejected with unconfirmed loaded area");Check(chest.Owner==ZNet.GetUID()&&UnloadedNetworks.KeepOwner(chest,0),"retained source lost single-player owner");Check(!UnloadedNetworks.Containers.Any(),"duplicated retained inventory");});
  Test("untracked live chest cannot bypass readiness",()=>{Start();var live=LiveChest();var unknown=new ZDO{m_uid=new ZDOID{Id=99},Prefab=chest.Prefab};live.m_nview=null;live.GetComponent<ZNetView>().m_zdo=unknown;ZNetScene.instance.Live[unknown.m_uid]=live.gameObject;ZDOMan.instance.m_objectsByID[unknown.m_uid]=unknown;ZNetScene.instance.AreaLoaded=false;Check(!UnloadedNetworks.CanUseUnloaded(live)&&!UnloadedNetworks.SourceReady(live),"untracked source bypassed readiness");});
  Test("stale live instance cannot borrow replacement record with same id",()=>{Start();var live=LiveChest();ZNetScene.instance.AreaLoaded=false;ZDOMan.instance.m_objectsByID[chest.m_uid]=new ZDO{m_uid=chest.m_uid,Prefab=chest.Prefab};Check(!UnloadedNetworks.CanUseUnloaded(live),"stale instance accepted");});
  Test("disabled experiment keeps normal area readiness for live sources",()=>{Plugin.ExperimentalUnloadedNetworks.Value=false;Call("Bootstrap");Call("WorldLoaded");var live=LiveChest();ZNetScene.instance.AreaLoaded=false;Check(!UnloadedNetworks.CanUseUnloaded(live)&&!UnloadedNetworks.SourceReady(live),"disabled behavior changed");ZNetScene.instance.AreaLoaded=true;Check(UnloadedNetworks.SourceReady(live),"normal loaded source rejected");});
  Test("in-flight lease retains original offline inventory",()=>{Start();var proxy=Request();Transport.Leases[proxy.GetInventory()]=new Transport.Lease{Container=proxy};ZNetScene.instance.Live[chest.m_uid]=chestPrefab;Check(UnloadedNetworks.FindContainer(chest.m_uid)==proxy,"lease source switched");});
  Test("active real chest lease is pinned until release",()=>{Start();var live=new GameObject("ModdedDrawer");live.SetActive(false);live.AddComponent<ZNetView>().m_zdo=chest;var c=live.AddComponent<Container>();var inv=new Inventory("",null,4,2);Transport.Leases[inv]=new Transport.Lease{Container=c};var list=new List<ZDO>();Call("PinTransactions",list);Check(list.Count==1&&list[0]==chest,"leased live source unloaded");Transport.Leases.Clear();list.Clear();Call("PinTransactions",list);Check(list.Count==0,"pin survived release");});
  Test("fast hold survives area unload with the experiment disabled until release",()=>{Plugin.ExperimentalUnloadedNetworks.Value=false;Start();var live=LiveChest();FastPath.Held.Add(chest);var near=new List<ZDO>();ZNetScene.instance.RemoveObjects(near,new List<ZDO>());Check(!UnloadedNetworks.Enabled&&Transport.Leases.Count==0&&near.SequenceEqual(new[]{chest})&&ZNetScene.instance.FindInstance(chest.m_uid)==live.gameObject&&R.Valid(R.View(live)),"fast-only hold unloaded without the experiment");FastPath.Held.Clear();ZNetScene.instance.RemoveObjects(new List<ZDO>(),new List<ZDO>());Check(!ZNetScene.instance.FindInstance(chest.m_uid)&&!R.Valid(R.View(live)),"released fast hold stayed pinned");});
  Test("fast hold survives experimental area unload without a lease",()=>{Client();Deliver(core,chest);chest.Owner=17;var live=LiveChest();FastPath.Held.Add(chest);ZNetScene.instance.RemoveObjects(new List<ZDO>(),new List<ZDO>());Check(Transport.Leases.Count==0&&ZNetScene.instance.FindInstance(chest.m_uid)==live.gameObject&&R.Valid(R.View(live))&&chest.Owner==17&&ZDOMan.instance.Sent.Count==0,"fast hold unloaded or handed off");});
  Test("fast and lease pins share one near entry and release independently",()=>{Start();var live=LiveChest();FastPath.Held.Add(chest);Transport.Leases[live.GetInventory()]=new Transport.Lease{Container=live};var near=new List<ZDO>{chest};Call("PinTransactions",near);Call("PinTransactions",near);Check(near.Count==1,"shared hold duplicated near record");FastPath.Held.Clear();near.Clear();Call("PinTransactions",near);Check(near.SequenceEqual(new[]{chest}),"fast release dropped lease pin");Transport.Leases.Clear();near.Clear();Call("PinTransactions",near);Check(near.Count==0,"pin survived both releases");});
  Test("empty and invalid fast holds do not change disabled lease behavior",()=>{Plugin.ExperimentalUnloadedNetworks.Value=false;Start();var live=LiveChest();Transport.Leases[live.GetInventory()]=new Transport.Lease{Container=live};var near=new List<ZDO>();Call("PinTransactions",near);FastPath.Held.Add(null);FastPath.Held.Add(new ZDO{Valid=false});Call("PinTransactions",near);Check(near.Count==0,"invalid fast record or disabled lease was pinned");});
  Test("optional uninstall keeps the stable fast hold pin",()=>{Start();var live=LiveChest();FastPath.Held.Add(chest);UnloadedNetworks.Uninstall();ZNetScene.instance.RemoveObjects(new List<ZDO>(),new List<ZDO>());Check(!UnloadedNetworks.Installed&&ZNetScene.instance.FindInstance(chest.m_uid)==live.gameObject&&R.Valid(R.View(live)),"optional uninstall removed stable pin");});
  Test("destroying core removes retained reference",()=>{Start();var c=UnloadedNetworks.FindCore(core.m_uid);Call("Destroyed",core.m_uid);Check(!UnloadedNetworks.FindCore(core.m_uid)&&!R.Valid(R.View(c)),"ghost core survived");});
  Test("shutdown clears previous world adapters",()=>{Start();Request();Call("Shutdown");Check(!UnloadedNetworks.Enabled&&Core.Live.Count==0&&!UnloadedNetworks.FindContainer(chest.m_uid),"world state leaked");});
  Test("configuration change requires plugin restart rather than changing patches mid-world",()=>{Start();Plugin.ExperimentalUnloadedNetworks.Value=false;Call("Bootstrap");Check(UnloadedNetworks.Enabled,"world reload changed installed mode");UnloadedNetworks.Uninstall();Boot();Check(!UnloadedNetworks.Enabled&&!UnloadedNetworks.Installed,"restart ignored setting");});
  Test("client does not scan unsolicited remote records at startup",()=>{Client();Check(!UnloadedNetworks.FindCore(core.m_uid)&&!UnloadedNetworks.FindContainer(chest.m_uid),"client discovered records without server");});
  Test("authenticated server discovery connects a client without visiting the core",()=>{
   Start();Request();Actor();Query();UnloadedMultiplayer.Tick();var message=Transport.Sent.Single(s=>s.Name=="unloaded_catalog");
   Check(message.Peer==17&&ZDOMan.instance.Sent.Any(s=>s.Id==chest.m_uid&&s.Peer==17),"discovery did not target requester");
   Client();Receive("unloaded_catalog",7,new ZPackage(message.Bytes));UnloadedMultiplayer.Tick();for(int i=0;i<20;i++)UnloadedNetworks.Tick();
   Check(UnloadedNetworks.FindCore(core.m_uid)&&UnloadedNetworks.FindContainer(chest.m_uid)&&ZNetScene.instance.Live.Count==0,"cold remote discovery failed");
  });
  Test("client adapters can display but never own or save remote inventory",()=>{Client();Deliver(core,chest);var c=UnloadedNetworks.FindContainer(chest.m_uid);Load(c);chest.Owner=ZNet.GetUID();Check(!UnloadedNetworks.CanOwn(c)&&Refuses(()=>Call("SaveReplica",c)),"client adapter wrote remote source");Check(c.GetInventory().Bytes.SequenceEqual(chest.Bytes),"display inventory missing");});
  Test("server never steals a chest from another live player",()=>{chest.Owner=17;Start();var c=Request();Load(c);Check(chest.Owner==17&&Refuses(()=>Call("SaveReplica",c))&&!UnloadedNetworks.KeepOwner(chest,0),"server stole peer inventory");});
  Test("server claims released source on demand after complete receipt",()=>{chest.Owner=17;Start();Request();chest.Owner=0;Call("OwnershipChanged",chest);Check(chest.Owner==0,"claimed during packet deserialization");UnloadedNetworks.Tick();Check(chest.Owner==7&&!UnloadedNetworks.KeepOwner(chest,17),"handoff to server or back to loaded client blocked");});
  Test("departed peer cannot leave an unreserved distant chest inaccessible",()=>{chest.Owner=17;Start();Request();chest.InUse=1;ZNet.instance.Peers.Clear();Call("Disconnected",new ZNetPeer{m_uid=17});UnloadedNetworks.Tick();Check(chest.Owner==7&&chest.InUse==0,"departed owner blocked storage");});
  Test("disconnect never discards an unresolved inventory reservation",()=>{chest.Owner=17;Start();var c=Request();Transport.Leases[c.GetInventory()]=new Transport.Lease{Container=c};ZNet.instance.Peers.Clear();Call("Disconnected",new ZNetPeer{m_uid=17});UnloadedNetworks.Tick();Check(chest.Owner==17,"lost-owner reservation bypassed");});
  Test("reserved or busy ownerless source is never claimed",()=>{chest.Owner=0;chest.InUse=1;Start();var c=Request();Check(chest.Owner==0,"busy inventory claimed");chest.InUse=0;Transport.Leases[c.GetInventory()]=new Transport.Lease{Container=c};Call("OwnershipChanged",chest);UnloadedNetworks.Tick();Check(chest.Owner==0,"reserved source claimed");});
  Test("client unload flushes live inventory and releases ownership together",()=>{Client();Deliver(core,chest);chest.Owner=17;var c=LiveChest();c.GetInventory().Bytes=new byte[]{9,8,7};Call("Unloading",R.View(c));Check(chest.Owner==0&&chest.Bytes.SequenceEqual(new byte[]{9,8,7})&&ZDOMan.instance.Sent.Any(s=>s.Id==chest.m_uid),"unload left stale inventory or owner");});
  Test("client unload cannot release an active lease or an open chest",()=>{Client();Deliver(core,chest);chest.Owner=17;var c=LiveChest();Transport.Leases[c.GetInventory()]=new Transport.Lease{Container=c};Call("Unloading",R.View(c));Check(chest.Owner==17,"lease owner released");Transport.Leases.Clear();c.Open=true;Call("Unloading",R.View(c));Check(chest.Owner==17,"open chest owner released");});
  Test("client demolition deletes the chest record after materials drop",()=>{
   Client();Deliver(core,chest);chest.Owner=17;var c=LiveChest();int drops=0;
   for(int attempt=0;attempt<2;attempt++)if(RemoteContext.Data(chest.m_uid)!=null){if(!ZNetScene.instance.FindInstance(chest.m_uid))c=LiveChest();drops++;ZNetScene.instance.Destroy(c.gameObject);}
   UnloadedNetworks.Tick();
   Check(drops==1&&RemoteContext.Data(chest.m_uid)==null&&!ZNetScene.instance.FindInstance(chest.m_uid)&&!UnloadedNetworks.FindContainer(chest.m_uid),"demolition returned materials but retained/recreated the chest");
   Check(chest.Owner==17&&ZDOMan.instance.Sent.Count==0,"destruction released ownership as if the area unloaded");
  });
  Test("host destruction removes both live and offline chest references",()=>{Start();Request();var c=LiveChest();ZNetScene.instance.Destroy(c.gameObject);UnloadedNetworks.Tick();Check(RemoteContext.Data(chest.m_uid)==null&&!UnloadedNetworks.FindContainer(chest.m_uid),"destroyed host chest remained in the network");});
  Test("confirmed remote destruction cannot save or requeue the chest",()=>{Client();Deliver(core,chest);chest.Owner=17;LiveChest();ZDOMan.instance.HandleDestroyedZDO(chest.m_uid);UnloadedNetworks.Tick();Check(chest.Owner==17&&ZDOMan.instance.Sent.Count==0&&RemoteContext.Data(chest.m_uid)==null&&!UnloadedNetworks.FindContainer(chest.m_uid),"remote destruction was treated as an unload");});
  Test("area unload still saves and hands off a live client chest",()=>{Client();Deliver(core,chest);chest.Owner=17;var c=LiveChest();c.GetInventory().Bytes=new byte[]{5,6,7};ZNetScene.instance.RemoveObjects(new List<ZDO>(),new List<ZDO>());Check(chest.Owner==0&&chest.Bytes.SequenceEqual(new byte[]{5,6,7})&&RemoteContext.Data(chest.m_uid)!=null&&!R.Valid(R.View(c))&&ZDOMan.instance.Sent.Any(s=>s.Id==chest.m_uid),"ordinary area unload lost its save/handoff");});
  Test("area unload pins a leased live chest",()=>{Client();Deliver(core,chest);chest.Owner=17;var c=LiveChest();Transport.Leases[c.GetInventory()]=new Transport.Lease{Container=c};ZNetScene.instance.RemoveObjects(new List<ZDO>(),new List<ZDO>());Check(ZNetScene.instance.FindInstance(chest.m_uid)==c.gameObject&&R.Valid(R.View(c))&&chest.Owner==17&&ZDOMan.instance.Sent.Count==0,"active lease unloaded or lost ownership");});
  Test("unload patch is scoped to area removal and preserves instruction metadata",()=>{
   Start();Check(!HarmonyLib.Harmony.Bindings.Any(b=>b.Target.DeclaringType==typeof(ZNetView)&&b.Target.Name=="ResetZDO"),"global reset hook also affects destruction");
   var instruction=new HarmonyLib.CodeInstruction(OpCodes.Callvirt,typeof(ZNetView).GetMethod("ResetZDO"));instruction.labels.Add(default);instruction.blocks.Add(new object());
   var result=((IEnumerable<HarmonyLib.CodeInstruction>)Call("UnloadIL",new object[]{new[]{instruction}})).Single();
   Check(ReferenceEquals(result,instruction)&&result.labels.Count==1&&result.blocks.Count==1&&result.opcode==OpCodes.Call&&((MethodInfo)result.operand).Name=="UnloadView","replacement lost branch/exception metadata");
   Check(Refuses(()=>Call("UnloadIL",new object[]{Array.Empty<HarmonyLib.CodeInstruction>()}))&&Refuses(()=>Call("UnloadIL",new object[]{new[]{new HarmonyLib.CodeInstruction(OpCodes.Callvirt,typeof(ZNetView).GetMethod("ResetZDO")),new HarmonyLib.CodeInstruction(OpCodes.Callvirt,typeof(ZNetView).GetMethod("ResetZDO"))}})),"changed game method accepted an ambiguous unload anchor");
  });
  Test("disabled experiment leaves client destruction on the vanilla path",()=>{Plugin.ExperimentalUnloadedNetworks.Value=false;ZNet.instance.Server=false;chest.Owner=17;Start();var c=LiveChest();ZNetScene.instance.Destroy(c.gameObject);Check(RemoteContext.Data(chest.m_uid)==null&&chest.Owner==17&&ZDOMan.instance.Sent.Count==0&&HarmonyLib.Harmony.Bindings.Count==1&&HarmonyLib.Harmony.Bindings[0].Prefix.Method.Name=="PinTransactions","disabled feature altered destruction");});
  Test("forged actor, player and remote point cannot discover storage",()=>{Start();Request();Actor();Query(sender:99);Query(player:2);Query(at:new Vector3(100,0,0));Query(at:new Vector3(float.NaN,0,0));UnloadedMultiplayer.Tick();Check(Transport.Sent.Count==0&&ZDOMan.instance.Sent.Count==0,"unauthorized discovery accepted");});
  Test("catalog from another peer cannot introduce sources",()=>{Client();Receive("unloaded_catalog",99,Catalog(1,0,core,chest));UnloadedMultiplayer.Tick();Check(!UnloadedMultiplayer.Accepted(chest.m_uid),"untrusted catalog accepted");});
  Test("catalog waits for actual native data and requests missing revisions",()=>{Client();chest.DataRevision=5;var packet=Catalog(1,0,core,chest);chest.DataRevision=4;Receive("unloaded_catalog",7,packet);UnloadedMultiplayer.Tick();Check(UnloadedMultiplayer.Preparing&&!UnloadedNetworks.FindContainer(chest.m_uid)&&Transport.Sent.Any(s=>s.Name=="unloaded_missing"),"stale record treated as ready");chest.DataRevision=5;Call("Received",chest);UnloadedNetworks.Tick();Check(!UnloadedMultiplayer.Preparing&&UnloadedNetworks.FindContainer(chest.m_uid),"native update failed to finish discovery");});
  Test("out of order catalog pages apply atomically and ignore old generations",()=>{Client();var records=Enumerable.Range(100,65).Select(i=>Record("ModdedDrawer",i,5)).ToArray();Receive("unloaded_catalog",7,Catalog(2,1,records));Check(!UnloadedMultiplayer.Accepted(records[64].m_uid),"partial catalog published");Receive("unloaded_catalog",7,Catalog(1,0,core,chest));Receive("unloaded_catalog",7,Catalog(2,0,records));Check(UnloadedMultiplayer.Accepted(records[64].m_uid)&&!UnloadedMultiplayer.Accepted(chest.m_uid),"stale catalog replaced newer pages");});
  Test("switching network removes previous distant adapters",()=>{Client();Deliver(core,chest);Receive("unloaded_catalog",7,Catalog(2,0,core));Check(!UnloadedMultiplayer.Accepted(chest.m_uid)&&!UnloadedNetworks.FindContainer(chest.m_uid),"old source remained connected");});
  Test("disabled server falls back without keeping remote adapters",()=>{Client();Deliver(core,chest);var off=new ZPackage();off.Write(1);off.Write(false);Receive("unloaded_catalog",7,off);Check(!UnloadedNetworks.Enabled&&Core.Live.Count==0&&!UnloadedNetworks.FindContainer(chest.m_uid),"disabled server left remote state");});
  Test("server updates only watched sources then stops when access ends",()=>{Start();Request();Actor();Query();UnloadedMultiplayer.Tick();ZDOMan.instance.Sent.Clear();chest.DataRevision++;Call("Changed",chest);Call("Changed",Actor());UnloadedMultiplayer.Tick();Check(ZDOMan.instance.Sent.Count==1&&ZDOMan.instance.Sent[0].Id==chest.m_uid,"wrong updates forwarded");Time.unscaledTime=7;UnloadedMultiplayer.Tick();ZDOMan.instance.Sent.Clear();Call("Changed",chest);UnloadedMultiplayer.Tick();Check(ZDOMan.instance.Sent.Count==0,"idle subscription kept sending");});
  Test("unchanged ownership attempts do not trigger resource replication",()=>{Start();Request();Actor();Query();UnloadedMultiplayer.Tick();ZDOMan.instance.Sent.Clear();Call("OwnershipChanged",chest);Call("OwnershipChanged",chest);UnloadedMultiplayer.Tick();Check(ZDOMan.instance.Sent.Count==0,"blocked release requeued unchanged inventory");});
  Test("reopening after subscription expiry uses a newer generation",()=>{Start();Request();Actor();Query();UnloadedMultiplayer.Tick();var a=new ZPackage(Transport.Sent.Last().Bytes);a.ReadInt();a.ReadBool();int first=a.ReadInt();Time.unscaledTime=7;UnloadedMultiplayer.Tick();Query();UnloadedMultiplayer.Tick();var b=new ZPackage(Transport.Sent.Last().Bytes);b.ReadInt();b.ReadBool();Check(b.ReadInt()>first,"reopened response appears older to client");});
  Test("small player movement does not retransmit the entire same network",()=>{Start();Request();Actor();Query();UnloadedMultiplayer.Tick();Transport.Sent.Clear();ZDOMan.instance.Sent.Clear();Time.unscaledTime=1;Query(at:new Vector3(1,0,0));UnloadedMultiplayer.Tick();Check(Transport.Sent.Count==0&&ZDOMan.instance.Sent.Count==0,"same network re-exported");});
  Test("builder discovery uses synchronized binding and actual player position",()=>{Start();Request();Actor().BuilderBinding="saved-core";Query(at:new Vector3(10,0,0),building:true);UnloadedMultiplayer.Tick();Check(Topology.ChosenBinding=="saved-core"&&Topology.ChosenPoint.x==0,"client point/binding used instead of authenticated actor");});
  Test("ordinary discovery ignores equipped builder binding",()=>{Start();Request();Actor().BuilderBinding="saved-core";Query();UnloadedMultiplayer.Tick();Check(Topology.ChosenBinding==null,"book hijacked crafting or terminal discovery");});
  Test("missing-record requests cannot retrieve unrelated world data",()=>{Start();Request();Actor();Query();UnloadedMultiplayer.Tick();var catalog=new ZPackage(Transport.Sent.Last().Bytes);catalog.ReadInt();catalog.ReadBool();int version=catalog.ReadInt();ZDOMan.instance.Sent.Clear();var p=new ZPackage();p.Write(1);p.Write(version);p.Write(2);p.Write(new ZDOID{Id=10});p.Write(chest.m_uid);Receive("unloaded_missing",17,p);UnloadedMultiplayer.Tick();Check(ZDOMan.instance.Sent.Count==1&&ZDOMan.instance.Sent[0].Id==chest.m_uid,"unadvertised source sent");});
  Test("client missing-data recovery stops when there is no access",()=>{Client();chest.DataRevision=1;var p=Catalog(1,0,chest);ZDOMan.instance.m_objectsByID.Remove(chest.m_uid);Receive("unloaded_catalog",7,p);UnloadedMultiplayer.Tick();Transport.Sent.Clear();Time.unscaledTime=10;UnloadedMultiplayer.Tick();Check(Transport.Sent.Count==0,"idle recovery continued");UnloadedMultiplayer.Touch(default,1);UnloadedMultiplayer.Tick();Check(Transport.Sent.Any(s=>s.Name=="unloaded_missing"),"reopening did not recover missing record");});
  Test("large discovery is delivered across bounded frames without dropping chests",()=>{foreach(int i in Enumerable.Range(100,130))Record("ModdedDrawer",i,5);Start();Request();Actor();Query();for(int i=0;i<10;i++){int before=ZDOMan.instance.Sent.Count;UnloadedMultiplayer.Tick();UnloadedNetworks.Tick();Check(ZDOMan.instance.Sent.Count-before<=64,"native record frame budget exceeded");}Check(ZDOMan.instance.Sent.Select(s=>s.Id).Distinct().Count()==132&&Transport.Sent.Count(s=>s.Name=="unloaded_catalog")==3,"paged discovery lost records");});
  Test("disabled startup never looks up or patches optional game APIs",()=>{Plugin.ExperimentalUnloadedNetworks.Value=false;HarmonyLib.Harmony.FailAt=2;Start();Check(!UnloadedNetworks.Installed&&HarmonyLib.Harmony.Lookups==1&&HarmonyLib.Harmony.Attempts==1&&HarmonyLib.Harmony.Bindings.Single().Prefix.Method.Name=="PinTransactions"&&StorageIndex.Offline==null,"disabled experiment initialized");Check(CraftInspection.FindContainer==CraftInspection.LoadedContainer,"disabled inspection replaced");});
  Test("optional patch failure preserves stable patches and source lookup",()=>{HarmonyLib.Harmony.Owners.Add(Plugin.Guid);HarmonyLib.Harmony.FailAt=5;UnloadedNetworks.Install();Check(!UnloadedNetworks.Installed&&!UnloadedNetworks.Enabled&&Plugin.Healthy,"optional failure disabled stable supply");Check(HarmonyLib.Harmony.Owners.SequenceEqual(new[]{Plugin.Guid})&&StorageIndex.Offline==null&&CraftInspection.FindContainer==CraftInspection.LoadedContainer,"partial experiment or wrong patch owner survived");Check(Plugin.Messages.Any(m=>m.Contains("normal networking remains active")),"failure not logged");});
  Test("successful optional patches have their own owner and are removed together",()=>{Start();Check(UnloadedNetworks.Installed&&StorageIndex.Offline!=null&&HarmonyLib.Harmony.Owners.Count(o=>o==Plugin.Guid+".unloaded")==14,"optional install incomplete");UnloadedNetworks.Uninstall();Check(HarmonyLib.Harmony.Owners.SequenceEqual(new[]{Plugin.Guid})&&StorageIndex.Offline==null&&CraftInspection.FindContainer==CraftInspection.LoadedContainer,"optional cleanup affected stable patches");});
  Test("changing false to true cannot activate without restarting plugin",()=>{Plugin.ExperimentalUnloadedNetworks.Value=false;Start();Plugin.ExperimentalUnloadedNetworks.Value=true;Call("Bootstrap");Call("WorldLoaded");Check(!UnloadedNetworks.Enabled&&!UnloadedNetworks.Installed&&StorageIndex.Offline==null,"live config installed an experiment");});
  Test("disabled server replies unavailable without looking up world records",()=>{Plugin.ExperimentalUnloadedNetworks.Value=false;Start();ZDOMan.instance.m_objectsByID.Clear();var p=new ZPackage();p.Write(1);UnloadedAvailability.Decline(17,new ZPackage(p.GetArray()));Check(Transport.Sent.Count==1&&ZDOMan.instance.Sent.Count==0&&Topology.Changes==0,"decline accessed topology or storage");var reply=new ZPackage(Transport.Sent[0].Bytes);Check(reply.ReadInt()==1&&!reply.ReadBool(),"wrong unavailable response");UnloadedAvailability.Decline(17,new ZPackage());Check(Transport.Sent.Count==1,"malformed query replied");});
  Test("disabled inventory hooks leave original load save ownership and bytes untouched",()=>{Plugin.ExperimentalUnloadedNetworks.Value=false;Start();var c=LiveChest();var before=(byte[])chest.Bytes.Clone();c.GetInventory().Bytes=new byte[]{1,2,3};var args=new object[]{c,true};Check((bool)Call("LoadReplica",args)&&(bool)args[1]&&(bool)Call("SaveReplica",c),"ordinary Load/Save blocked");Call("Unloading",R.View(c));Call("Changed",chest);Call("Received",chest);Call("OwnershipChanged",chest);Check(chest.Bytes.SequenceEqual(before)&&chest.Owner==7&&chest.DataRevision==0&&!UnloadedNetworks.KeepOwner(chest,0)&&!UnloadedNetworks.DiscardUnpaid(c,false),"disabled hook changed inventory or ownership");Check(CraftInspection.FindContainer(chest.m_uid)==c&&ZDOMan.instance.Sent.Count==0,"loaded lookup or traffic changed");});
  Test("disabled craft proposal uses registered members even before graph refresh",()=>{Plugin.ExperimentalUnloadedNetworks.Value=false;Start();var go=new GameObject("node");go.SetActive(false);var member=go.AddComponent<NetworkMember>();member.View=go.AddComponent<ZNetView>();member.View.m_zdo=core;Topology.Members.Add(member);Check(Topology.Graph.Nodes.Count==0&&Topology.OperationNodes("network").SequenceEqual(new[]{core.m_uid}),"stable proposal switched to graph cache");});
  Test("enabled craft proposal includes inactive nodes from the graph",()=>{Start();Request();Check(Topology.Members.Count==0&&Topology.OperationNodes("network").SequenceEqual(new[]{core.m_uid}),"offline proposal lost inactive node");});
  Test("disconnected gateway discovery exports topology without chest contents",()=>{Prefab<NetworkMember>("RSN_RunicGateway");var a=Record("RSN_RunicGateway",31,5000);var b=Record("RSN_RunicGateway",32,9000);Start();var exported=UnloadedNetworks.Export(null,1);Check(exported.Contains(core.m_uid)&&exported.Contains(a.m_uid)&&exported.Contains(b.m_uid)&&!exported.Contains(chest.m_uid),"missing remote endpoint or unrelated chest exported");});
  Test("coreless discovery without gateways still returns no records",()=>{Start();Check(UnloadedNetworks.Export(null,1).Count==0,"ordinary coreless discovery changed");});
  Console.WriteLine("Unloaded network runtime tests: "+passed+" passed (game stand-ins)");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
#endif
