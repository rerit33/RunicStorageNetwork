#if BUILD_TOOL_RUNTIME_TESTS
// Runs production policy, build gates and Planner with game stand-ins. No Unity/Valheim process.
#pragma warning disable 0649
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RunicStorageNetwork;
using RunicStorageNetwork.Logic;
namespace UnityEngine {
 public class Object {public string name;public static implicit operator bool(Object x)=>x!=null;}
 public class GameObject:Object {
  public readonly List<object> Parts=new List<object>();public string[] Names=new[]{"Piece","ZNetView"};
  public Transform transform=new Transform();
  public T GetComponent<T>() where T:class=>Parts.OfType<T>().FirstOrDefault();
  public T Add<T>(T part){Parts.Add(part);return part;}
 }
 public class Transform {public Vector3 position;public Quaternion rotation;}
 public struct Vector3 {} public struct Quaternion {}
}
namespace BepInEx.Configuration {public class ConfigEntry<T> {public T Value;public ConfigEntry(T value){Value=value;}}}
class PieceTable:UnityEngine.Object {public bool m_canRemoveFeasts,m_canRemovePieces=true;public List<GameObject> m_pieces=new List<GameObject>();}
class ItemDrop:UnityEngine.Object {
 public ItemData m_itemData=new ItemData();
 public class ItemData {public Shared m_shared=new Shared();public GameObject m_dropPrefab;public float m_durability;}
 public class Shared {public PieceTable m_buildPieces;public string m_name;public bool m_useDurability;public Attack m_attack=new Attack();}
 public class Attack {public float m_attackStamina;}
}
class Feast:UnityEngine.Object {}
class Piece:UnityEngine.Object {
 public bool m_enabled=true,m_repairPiece,m_removePiece;public GameObject gameObject;public CraftingStation m_craftingStation;public string m_dlc="";
 public Requirement[] m_resources=new Requirement[0];public string FreeBuildKey()=>"FreeBuild";
 public class Requirement {public ItemDrop m_resItem;public int m_amount;}
}
class Inventory {public readonly Dictionary<string,int> Counts=new Dictionary<string,int>();public int CountItems(string name)=>Counts.TryGetValue(name,out int n)?n:0;}
class Player:UnityEngine.Object {
 public static Player m_localPlayer;public PieceTable Table;public ItemDrop.ItemData Tool;public Inventory Inventory=new Inventory();public bool Free;
 public enum RequirementMode {IsKnown,CanAlmostBuild,CanBuild}
 public PieceTable GetBuildTool()=>Table;public Inventory GetInventory()=>Inventory;public bool NoCostCheat()=>Free;public Transform transform=new Transform();public float Stamina=100;public bool HaveStamina(float n)=>Stamina>=n;
}
class ObjectDB:UnityEngine.Object {public static ObjectDB instance;public List<GameObject> m_items=new List<GameObject>();}
class ZNetScene:UnityEngine.Object {public static ZNetScene instance;public readonly Dictionary<string,GameObject> Prefabs=new Dictionary<string,GameObject>();public GameObject GetPrefab(string id)=>Prefabs.TryGetValue(id,out var p)?p:null;}
class CraftingStation:UnityEngine.Object {public string m_name;public static CraftingStation HaveBuildStationInRange(string n,Vector3 p)=>null;}
class ZDO {public int m_uid;}
class ZNetView:UnityEngine.Object {public ZDO GetZDO()=>new ZDO();}
class ZoneSystem {public static ZoneSystem instance=new ZoneSystem();public bool GetGlobalKey(string key)=>false;}
class GlobalKeys {public const string NoWorkbench="NoWorkbench";}
class DLCMan {public static DLCMan instance=new DLCMan();public bool IsDLCInstalled(string name)=>false;}
namespace RunicStorageNetwork {
 static class Plugin {
  public static BepInEx.Configuration.ConfigEntry<string> AllowedBuildTools=new BepInEx.Configuration.ConfigEntry<string>("");
  public static BepInEx.Configuration.ConfigEntry<string> DeniedBuildTools=new BepInEx.Configuration.ConfigEntry<string>("");
  public static BepInEx.Configuration.ConfigEntry<string> DeniedPieceComponents=new BepInEx.Configuration.ConfigEntry<string>(BuildToolRules.DeniedPieceComponentDefault);
 }
 static class R {
  public static string Id(GameObject go)=>go.name;public static IEnumerable<string> Components(GameObject go)=>go.Names;
  public static object Call(Player p,string method,Type[] types,params object[] args)=>method=="GetRightItem"?p.Tool:null;
  public static T Get<T>(object o,string name){object v=name=="m_placementStatus"?(object)"Valid":name=="m_placementGhost"?new GameObject():name=="m_knownStations"?new Dictionary<string,int>():default(T);return (T)v;}
  public static ZNetView View(object o)=>null;public static bool Valid(ZNetView v)=>false;
 }
 class Core:UnityEngine.Object {}
 class Operation {public int Station;public List<Need> Needs;public bool Validate(out object a,out object b,out string c){a=b=c=null;return true;}}
 static class FastPath {
  internal static bool Handled;
  internal static bool Running,BlocksResult,OwnsResult,NativeReadyResult,ConsumeNativeResult;
  internal static int ConsumeCalls;
  internal static Actions.Pending LastPending;
  internal static bool Blocks(Player player)=>BlocksResult;
  internal static bool Owns(Player player)=>OwnsResult;
  internal static bool NativeReady(Player player)=>NativeReadyResult;
  internal static bool ConsumeNativePlacement(Player player){ConsumeCalls++;return ConsumeNativeResult;}
  internal static bool TryBuild(Actions.Pending pending,Core core,List<Debit> preview){LastPending=pending;return Handled;}
 }
 static class Stockroom {
  public static List<Stock> Stock=new List<Stock>();
  public static List<Need> Requirements(Piece.Requirement[] req,int quality,int mult)=>req.Where(r=>r.m_resItem&&r.m_amount>0).Select(r=>new Need(r.m_resItem.name,r.m_amount*mult)).ToList();
  public static List<Stock> Available(Player p,Core c,List<Need> n)=>Stock;
 }
 internal static partial class Actions {
  internal class Pending {internal Piece Piece;internal Player Player;internal ItemDrop.ItemData Tool;internal Operation Op;internal Vector3 Position;internal Quaternion Rotation;}
  internal static Pending Active,Waiting;internal static int ContextCalls,Started;internal static Core Supply;internal static List<Debit> LastPlan;
  internal static Core Context(Player p,bool craft){ContextCalls++;return Supply;}
  internal static Operation Create(Player p,Core c,bool build,string id,int quality,int multiplier)=>new Operation{Needs=Stockroom.Requirements(ZNetScene.instance.GetPrefab(id).GetComponent<Piece>().m_resources,0,1)};
  static void Start(Pending p,List<Debit> plan){Waiting=p;LastPlan=plan;Started++;}
 }
 internal static partial class Patches {internal static bool Check(Player p,Piece piece,Player.RequirementMode mode,ref bool result)=>HaveBuild(p,piece,mode,ref result);}
}
static class BuildToolRuntimeTests {
 static int passed;
 static void Test(string name,Action test){test();passed++;Console.WriteLine("PASS build runtime "+name);}
 static void Assert(bool ok){if(!ok)throw new Exception("Assertion failed");}
 static Piece AddPiece(string name,PieceTable table,params object[] extras){var go=new GameObject{name=name};var piece=go.Add(new Piece{name=name,gameObject=go});foreach(var e in extras)go.Add(e);go.Names=go.Names.Concat(extras.Select(e=>e.GetType().Name)).ToArray();table.m_pieces.Add(go);ZNetScene.instance.Prefabs[name]=go;return piece;}
 static ItemDrop.ItemData AddTool(string name,PieceTable table){var go=new GameObject{name=name};var drop=go.Add(new ItemDrop{name=name});drop.m_itemData.m_dropPrefab=go;drop.m_itemData.m_shared.m_buildPieces=table;ObjectDB.instance.m_items.Add(go);return drop.m_itemData;}
 static void Equip(Player p,ItemDrop.ItemData tool){p.Tool=tool;p.Table=tool.m_shared.m_buildPieces;}
 static int Main(){
  ObjectDB.instance=new ObjectDB();ZNetScene.instance=new ZNetScene();var p=Player.m_localPlayer=new Player();var table=new PieceTable();var wall=AddPiece("wall",table);var hammer=AddTool("Hammer",table);var mod=AddTool("ModHammer",table);Equip(p,mod);
  Test("modded tool uses both local and coordinator gates",()=>Assert(Actions.BuildPiece(p,wall)&&BuildToolPolicy.Eligible(wall.gameObject)));
  Test("denied equipped tool cannot borrow an allowed shared table",()=>{Plugin.DeniedBuildTools.Value="ModHammer";BuildToolPolicy.Invalidate();Assert(!Actions.BuildPiece(p,wall)&&BuildToolPolicy.Eligible(wall.gameObject));});
  Test("allow list applies to actual equipped tool",()=>{Plugin.DeniedBuildTools.Value="";Plugin.AllowedBuildTools.Value="Hammer";BuildToolPolicy.Invalidate();Assert(!Actions.BuildPiece(p,wall));Equip(p,hammer);Assert(Actions.BuildPiece(p,wall));Plugin.AllowedBuildTools.Value="";BuildToolPolicy.Invalidate();});
  Test("tool cannot borrow another active menu",()=>{p.Table=new PieceTable();Assert(!Actions.BuildPiece(p,wall));Equip(p,hammer);});
  Test("late added and removed pieces take effect without invalidation",()=>{var late=AddPiece("late",table);Assert(Actions.BuildPiece(p,late)&&BuildToolPolicy.Eligible(late.gameObject));table.m_pieces.Remove(late.gameObject);Assert(!Actions.BuildPiece(p,late)&&!BuildToolPolicy.Eligible(late.gameObject));});
  Test("late tool and table registration is visible",()=>{var lateTable=new PieceTable();var piece=AddPiece("late_tool_piece",lateTable);Assert(!BuildToolPolicy.Eligible(piece.gameObject));var late=AddTool("LateTool",lateTable);Equip(p,late);Assert(Actions.BuildPiece(p,piece)&&BuildToolPolicy.Eligible(piece.gameObject));Equip(p,hammer);});
  var trayTable=new PieceTable{m_canRemovePieces=false,m_canRemoveFeasts=true};var tray=AddTool("AnyTrayName",trayTable);var food=AddPiece("food",trayTable,new ItemDrop());
  Test("tray menu qualifies locally and for coordinator validation",()=>{Equip(p,tray);Assert(Actions.BuildPiece(p,food)&&BuildToolPolicy.Eligible(food.gameObject));Equip(p,hammer);});
  Test("food and feasts qualify in a general build menu too",()=>{table.m_pieces.Add(food.gameObject);Assert(Actions.BuildPiece(p,food)&&BuildToolPolicy.Eligible(food.gameObject));var feast=AddPiece("feast",table,new Feast());Assert(Actions.BuildPiece(p,feast)&&BuildToolPolicy.Eligible(feast.gameObject));});
  Test("terrain stays native but cultivator planting qualifies",()=>{var plantTable=new PieceTable();var cultivator=AddTool("Cultivator",plantTable);var plant=AddPiece("plant",plantTable);var ground=AddPiece("terrain",plantTable);ground.gameObject.Names=new[]{"Piece","TerrainOp"};Equip(p,cultivator);Assert(Actions.BuildPiece(p,plant)&&BuildToolPolicy.Eligible(plant.gameObject));Assert(!Actions.BuildPiece(p,ground)&&Actions.Build(p,ground));Equip(p,hammer);});
  Test("disabled repair and removal entries cannot request resources",()=>{wall.m_enabled=false;Assert(!Actions.BuildPiece(p,wall)&&!BuildToolPolicy.Eligible(wall.gameObject));wall.m_enabled=true;wall.m_repairPiece=true;Assert(!Actions.BuildPiece(p,wall));wall.m_repairPiece=false;wall.m_removePiece=true;Assert(!BuildToolPolicy.Eligible(wall.gameObject));wall.m_removePiece=false;});
  Test("component setting changes invalidate cached verdicts",()=>{Plugin.DeniedPieceComponents.Value="Piece";BuildToolPolicy.Invalidate();Assert(!Actions.BuildPiece(p,wall));Plugin.DeniedPieceComponents.Value=BuildToolRules.DeniedPieceComponentDefault;BuildToolPolicy.Invalidate();Assert(Actions.BuildPiece(p,wall));});
  var wood=new ItemDrop{name="Wood"};wood.m_itemData.m_shared.m_name="$item_wood";wall.m_resources=new[]{new Piece.Requirement{m_resItem=wood,m_amount=10}};
  Test("inventory-only placement never consults network",()=>{p.Inventory.Counts["$item_wood"]=10;Actions.ContextCalls=0;Assert(Actions.Build(p,wall)&&Actions.ContextCalls==0&&Actions.Started==0);});
  Test("inventory-only menu check delegates station and DLC rules to vanilla",()=>{bool result=false;Actions.ContextCalls=0;Assert(Patches.Check(p,wall,Player.RequirementMode.CanBuild,ref result)&&Actions.ContextCalls==0&&!result);});
  Test("discovery remains native even without resources",()=>{p.Inventory.Counts.Clear();bool result=false;Assert(Patches.Check(p,wall,Player.RequirementMode.IsKnown,ref result));});
  Test("partial local supply distinguishes almost-build from build",()=>{p.Inventory.Counts["$item_wood"]=1;Assert(Actions.LocalBuildMaterials(p,wall,Player.RequirementMode.CanAlmostBuild)&&!Actions.LocalBuildMaterials(p,wall,Player.RequirementMode.CanBuild));});
  Test("same missing materials enable menu and queue network payment",()=>{Actions.Supply=new Core();Stockroom.Stock=new List<Stock>{new Stock("player","Wood",1,1),new Stock("chest","Wood",1,9)};bool result=false;Assert(!Patches.Check(p,wall,Player.RequirementMode.CanBuild,ref result)&&result);Assert(!Actions.Build(p,wall)&&Actions.Started==1&&ReferenceEquals(Actions.Waiting,FastPath.LastPending));Actions.Waiting=null;FastPath.LastPending=null;});
  Test("handled fast build suppresses native entry without queuing payment",()=>{int started=Actions.Started;FastPath.Handled=true;try{Assert(!Actions.Build(p,wall)&&Actions.Started==started&&Actions.Waiting==null&&FastPath.LastPending.Player==p&&FastPath.LastPending.Piece==wall&&FastPath.LastPending.Tool==hammer);}finally{FastPath.Handled=false;FastPath.LastPending=null;}});
  Test("native fast build entry returns the consume result",()=>{int started=Actions.Started,contexts=Actions.ContextCalls;FastPath.Running=FastPath.OwnsResult=FastPath.NativeReadyResult=true;try{foreach(bool consume in new[]{true,false}){FastPath.ConsumeNativeResult=consume;Assert(Actions.Build(p,wall)==consume);}Assert(FastPath.ConsumeCalls==2&&Actions.Started==started&&Actions.ContextCalls==contexts&&Actions.Waiting==null);}finally{FastPath.Handled=FastPath.Running=FastPath.BlocksResult=FastPath.OwnsResult=FastPath.NativeReadyResult=FastPath.ConsumeNativeResult=false;FastPath.ConsumeCalls=0;}});
  Test("refused fast build entry returns false without consuming",()=>{int started=Actions.Started,contexts=Actions.ContextCalls;FastPath.Running=FastPath.BlocksResult=FastPath.OwnsResult=FastPath.NativeReadyResult=FastPath.ConsumeNativeResult=true;try{Assert(!Actions.Build(p,wall)&&FastPath.ConsumeCalls==0&&Actions.Started==started&&Actions.ContextCalls==contexts&&Actions.Waiting==null);}finally{FastPath.Handled=FastPath.Running=FastPath.BlocksResult=FastPath.OwnsResult=FastPath.NativeReadyResult=FastPath.ConsumeNativeResult=false;FastPath.ConsumeCalls=0;}});
  Test("insufficient combined stock cannot start payment",()=>{Stockroom.Stock[1].Amount=8;bool result=true;Assert(!Patches.Check(p,wall,Player.RequirementMode.CanBuild,ref result)&&!result);Assert(!Actions.Build(p,wall)&&Actions.Started==1);});
  Test("tools without durability can complete network placement",()=>{hammer.m_durability=0;hammer.m_shared.m_useDurability=false;Assert(Actions.BuildToolUsable(p,hammer));hammer.m_shared.m_useDurability=true;Assert(!Actions.BuildToolUsable(p,hammer));hammer.m_durability=10;Assert(Actions.BuildToolUsable(p,hammer));});
  Test("tool stamina requirement still applies",()=>{hammer.m_shared.m_attack.m_attackStamina=10;p.Stamina=0;Assert(!Actions.BuildToolUsable(p,hammer));});
  // Serving tests use the same production Build/HaveBuild methods and real Planner.
  var meal=food.gameObject.GetComponent<ItemDrop>();meal.name="CookedMeal";meal.m_itemData.m_shared.m_name="$item_meal";
  food.m_resources=new[]{new Piece.Requirement{m_resItem=meal,m_amount=1}};Equip(p,tray);p.Inventory.Counts.Clear();Actions.Started=0;
  Test("tray uses one stored meal when inventory is empty",()=>{Stockroom.Stock=new List<Stock>{new Stock("chest","CookedMeal",1,5)};bool result=false;Assert(!Patches.Check(p,food,Player.RequirementMode.CanBuild,ref result)&&result);Assert(!Actions.Build(p,food)&&Actions.Started==1);Assert(Actions.LastPlan.Count==1&&Actions.LastPlan[0].Source=="chest"&&Actions.LastPlan[0].Amount==1);Actions.Waiting=null;});
  Test("tray with carried food keeps the native path without network",()=>{p.Inventory.Counts["$item_meal"]=1;Actions.ContextCalls=0;bool result=false;Assert(Patches.Check(p,food,Player.RequirementMode.CanBuild,ref result));Assert(Actions.Build(p,food)&&Actions.ContextCalls==0&&Actions.Started==1);p.Inventory.Counts.Clear();});
  Test("tray with no food cannot queue or enable placement",()=>{Stockroom.Stock.Clear();bool result=true;Assert(!Patches.Check(p,food,Player.RequirementMode.CanBuild,ref result)&&!result);Assert(!Actions.Build(p,food)&&Actions.Started==1);});
  Test("tray rechecks stock lost after the menu preview",()=>{Stockroom.Stock.Add(new Stock("chest","CookedMeal",1,1));bool result=false;Assert(!Patches.Check(p,food,Player.RequirementMode.CanBuild,ref result)&&result);Stockroom.Stock.Clear();Assert(!Actions.Build(p,food)&&Actions.Started==1);});
  Test("tray pending payment cannot be started twice",()=>{Stockroom.Stock.Add(new Stock("chest","CookedMeal",1,2));Assert(!Actions.Build(p,food)&&Actions.Started==2);Assert(!Actions.Build(p,food)&&Actions.Started==2);Actions.Waiting=null;});
  Test("feast combines carried and stored ingredients without overpayment",()=>{var feast=AddPiece("tray_feast",trayTable,new Feast());feast.m_resources=new[]{new Piece.Requirement{m_resItem=meal,m_amount=3}};p.Inventory.Counts["$item_meal"]=1;Stockroom.Stock=new List<Stock>{new Stock("player","CookedMeal",1,1),new Stock("chest","CookedMeal",1,8)};bool result=false;Assert(!Patches.Check(p,feast,Player.RequirementMode.CanBuild,ref result)&&result);Assert(!Actions.Build(p,feast));Assert(Actions.LastPlan.Single(d=>d.Source=="player").Amount==1&&Actions.LastPlan.Single(d=>d.Source=="chest").Amount==2);Actions.Waiting=null;});
  Test("tray remains subject to administrator tool and piece exclusions",()=>{Plugin.DeniedBuildTools.Value="AnyTrayName";BuildToolPolicy.Invalidate();Assert(!Actions.BuildPiece(p,food));Assert(Actions.Build(p,food));Plugin.DeniedBuildTools.Value="";Plugin.DeniedPieceComponents.Value="ItemDrop";BuildToolPolicy.Invalidate();Assert(!Actions.BuildPiece(p,food)&&!BuildToolPolicy.Eligible(food.gameObject));Plugin.DeniedPieceComponents.Value=BuildToolRules.DeniedPieceComponentDefault;BuildToolPolicy.Invalidate();Assert(Actions.BuildPiece(p,food));});
  Console.WriteLine("RESULT "+passed+" build runtime stand-in tests passed; no game or multiplayer verification.");return 0;
 }
}
#endif
