#if BUILD_PATCH_TESTS
// Executes production transpilers with the installed Harmony against managed
// stand-ins. The runner separately checks the installed game's IL anchors.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Object=UnityEngine.Object;

namespace UnityEngine {
 public class Object {
  public string name="piece";
  public static implicit operator bool(Object o)=>o!=null;
  [MethodImpl(MethodImplOptions.NoInlining)]
  public static T Instantiate<T>(T original,Vector3 pos,Quaternion rot) where T:Object {
   Fixture.Creates++;var prefab=(GameObject)(Object)original;
   var go=new GameObject();go.Piece=new Piece{gameObject=go};go.Relay=prefab.Relay;
   Fixture.LastCreated=go;return (T)(Object)go;
  }
 }
 public class GameObject:Object {
  public Piece Piece;public RunicStorageNetwork.Relay Relay;
  public T GetComponent<T>() where T:class=>(typeof(T)==typeof(Piece)?(object)Piece:Relay) as T;
  [MethodImpl(MethodImplOptions.NoInlining)]
  public T GetComponentInChildren<T>() where T:class {
   if(Fixture.ThrowAfterCreation)throw new InvalidOperationException("placement callback failed");return null;
  }
 }
 public struct Vector3 {} public struct Quaternion {}
}
public class Piece:Object {
 public GameObject gameObject;public ZNetView View=new ZNetView();public string FreeBuildKey()=>"free";
}
public class CraftingStation:Object {}
public class ZDO {public bool Free;public void Set(string key,bool value){if(key!="rsn_free_relay")throw new Exception(key);Free=value;}}
public class ZNetView:Object {public ZDO Data=new ZDO();public ZDO GetZDO()=>Data;}
public class ZoneSystem {public static ZoneSystem instance=new ZoneSystem();public bool Free;public bool GetGlobalKey(string key)=>Free;}
public static class TerrainModifier {public static void SetTriggerOnPlaced(bool value){}}
public static class ZLog {public static void Log(string text){Fixture.Logs++;}}
public class Player:Object {
 public bool Invalid,Free,NoPlacementCost;public bool NoCostCheat()=>Free;
 [MethodImpl(MethodImplOptions.NoInlining)]
 public void UpdatePlacementGhost(bool flash){Fixture.Raycasts++;}
 [MethodImpl(MethodImplOptions.NoInlining)]
 public bool TryPlacePiece(Piece piece){
  UpdatePlacementGhost(true);
  if(Invalid)return false;
  ZLog.Log("Placed "+piece.gameObject.name);Fixture.Stats++;
  PlacePiece(piece,new Vector3(),new Quaternion(),true,false);return true;
 }
 [MethodImpl(MethodImplOptions.NoInlining)]
 public void PlacePiece(Piece piece,Vector3 pos,Quaternion rot,bool doAttack,bool cheated){
  var original=piece.gameObject;TerrainModifier.SetTriggerOnPlaced(true);
  var created=Object.Instantiate(original,pos,rot);TerrainModifier.SetTriggerOnPlaced(false);
  var station=created.GetComponentInChildren<CraftingStation>();GC.KeepAlive(station);
  Fixture.Completed++;
 }
}
namespace RunicStorageNetwork {
 public class Relay:Object {}
 static class ContentSettings {public static bool Enabled=true;public static bool AllowsPiece(Piece p)=>Enabled;}
 static class R {
  public static T Get<T>(Player p,string name)=>(T)(object)p.NoPlacementCost;
  public static ZNetView View(Piece p)=>p?.View;
  public static bool Valid(ZNetView v)=>v!=null;
 }
 static class Actions {
  public class Operation {public bool Build=true;}
  public class Pending {public Operation Op=new Operation();public Player Player;public Piece Piece;public bool Output;}
  public static Pending Active;public static bool Wait;public static int Calls;
  public static bool Build(Player p,Piece piece){Calls++;return Active!=null||!Wait;}
 }
}
static class Fixture {
 internal static int Creates,Completed,Raycasts,Stats,Logs,Prefixes,Postfixes,Attachments;
 internal static bool ThrowAfterCreation,ForeignVeto;internal static GameObject LastCreated;
 internal static void Reset(){
  Creates=Completed=Raycasts=Stats=Logs=Prefixes=Postfixes=Attachments=0;
  ThrowAfterCreation=ForeignVeto=false;LastCreated=null;
  RunicStorageNetwork.Actions.Active=null;RunicStorageNetwork.Actions.Wait=false;RunicStorageNetwork.Actions.Calls=0;
  RunicStorageNetwork.ContentSettings.Enabled=true;ZoneSystem.instance.Free=false;
 }
 // Same interception shape as ValheimRAFT's PlacePiece transpiler: replace the
 // generic creation call, then attach the created object in the wrapper.
 internal static IEnumerable<CodeInstruction> ForeignCreation(IEnumerable<CodeInstruction> instructions){
  foreach(var i in instructions){
   if(i.operand is MethodInfo m&&m.DeclaringType==typeof(Object)&&m.Name=="Instantiate")
    i.operand=AccessTools.Method(typeof(Fixture),nameof(CreateAndAttach));
   yield return i;
  }
 }
 static GameObject CreateAndAttach(Object prefab,Vector3 pos,Quaternion rot){
  var created=Object.Instantiate(prefab,pos,rot) as GameObject;Attachments++;return created;
 }
 internal static bool ForeignPrefix(ref bool __result){Prefixes++;if(ForeignVeto){__result=false;return false;}return true;}
 internal static void ForeignPostfix(){Postfixes++;}
}
static class BuildPatchTests {
 static int passed;
 static void Check(bool value,string name){if(!value)throw new Exception("FAIL "+name);passed++;Console.WriteLine("PASS "+name);}
 static MethodInfo Hook(string name)=>AccessTools.Method(typeof(RunicStorageNetwork.Patches),name);
 static Piece NewPiece(bool relay=false){var go=new GameObject();var piece=new Piece{gameObject=go};go.Piece=piece;if(relay)go.Relay=new RunicStorageNetwork.Relay();return piece;}
 static RunicStorageNetwork.Actions.Pending Paid(Player player,Piece piece){
  var pending=new RunicStorageNetwork.Actions.Pending{Player=player,Piece=piece};RunicStorageNetwork.Actions.Active=pending;return pending;
 }
 static void RunOrder(bool foreignFirst){
  string order=foreignFirst?"foreign first: ":"RSN first: ";
  var rsn=new Harmony("rsn.test.build");var foreign=new Harmony("rsn.test.foreign");
  var tryPlace=AccessTools.Method(typeof(Player),"TryPlacePiece");var place=AccessTools.Method(typeof(Player),"PlacePiece");
  Action addRsn=()=>{rsn.Patch(tryPlace,transpiler:new HarmonyMethod(Hook("BuildGateIL")));rsn.Patch(place,transpiler:new HarmonyMethod(Hook("BuildIL")));};
  Action addForeign=()=>{
   foreign.Patch(tryPlace,prefix:new HarmonyMethod(typeof(Fixture),"ForeignPrefix"){priority=Priority.Last},postfix:new HarmonyMethod(typeof(Fixture),"ForeignPostfix"));
   foreign.Patch(place,transpiler:new HarmonyMethod(typeof(Fixture),"ForeignCreation"));
  };
  try {
   if(foreignFirst){addForeign();addRsn();}else{addRsn();addForeign();}
   Fixture.Reset();var p=new Player();var piece=NewPiece();
   Check(p.TryPlacePiece(piece)&&Fixture.Creates==1&&Fixture.Attachments==1,order+"ordinary build retains foreign creation callback");
   Check(Fixture.Prefixes==1&&Fixture.Postfixes==1&&Fixture.Raycasts==1,order+"prefix, raycast and postfix run once");
   Fixture.Reset();RunicStorageNetwork.Actions.Wait=true;
   Check(!p.TryPlacePiece(piece),order+"unpaid placement returns false");
   Check(Fixture.Prefixes==1&&Fixture.Postfixes==1&&Fixture.Raycasts==1,order+"pending payment preserves other hooks and raycast");
   Check(Fixture.Creates==0&&Fixture.Stats==0&&Fixture.Logs==0,order+"pending payment creates nothing and records no build");
   var pending=Paid(p,piece);
   Check(p.TryPlacePiece(piece)&&pending.Output,order+"paid replay records the created object");
   Check(Fixture.Creates==1&&Fixture.Stats==1&&Fixture.Attachments==1,order+"replay creates, counts and attaches exactly one piece");
   Fixture.Reset();p.Invalid=true;
   Check(!p.TryPlacePiece(piece)&&RunicStorageNetwork.Actions.Calls==0&&Fixture.Creates==0,order+"invalid native placement never requests payment");p.Invalid=false;
   Fixture.Reset();Fixture.ForeignVeto=true;
   Check(!p.TryPlacePiece(piece)&&RunicStorageNetwork.Actions.Calls==0&&Fixture.Creates==0,order+"foreign placement veto is respected");
   Fixture.Reset();RunicStorageNetwork.ContentSettings.Enabled=false;
   Check(!p.TryPlacePiece(piece)&&Fixture.Creates==0&&Fixture.Prefixes==1&&Fixture.Raycasts==1,order+"disabled content is blocked after placement checks");
   Fixture.Reset();pending=Paid(p,piece);Fixture.ThrowAfterCreation=true;bool threw=false;
   try{p.TryPlacePiece(piece);}catch(InvalidOperationException){threw=true;}
   Check(threw&&pending.Output&&Fixture.Creates==1&&Fixture.Attachments==1,order+"output survives a later placement exception");
   Fixture.Reset();pending=Paid(new Player(),piece);p.TryPlacePiece(piece);
   Check(!pending.Output,order+"another player's build cannot acknowledge the transaction");
   Fixture.Reset();pending=Paid(p,NewPiece());p.TryPlacePiece(piece);
   Check(!pending.Output,order+"another piece cannot acknowledge the transaction");
   Fixture.Reset();pending=Paid(p,piece);pending.Op.Build=false;p.TryPlacePiece(piece);
   Check(!pending.Output,order+"build cannot acknowledge a craft transaction");
   var relay=NewPiece(true);
   Fixture.Reset();p.TryPlacePiece(relay);Check(!Fixture.LastCreated.Piece.View.Data.Free,order+"paid relay retains normal refund");
   Fixture.Reset();p.Free=true;p.TryPlacePiece(relay);Check(Fixture.LastCreated.Piece.View.Data.Free,order+"no-cost relay suppresses material refund");p.Free=false;
   Fixture.Reset();p.NoPlacementCost=true;p.TryPlacePiece(relay);Check(Fixture.LastCreated.Piece.View.Data.Free,order+"placement cheat relay suppresses refund");p.NoPlacementCost=false;
   Fixture.Reset();ZoneSystem.instance.Free=true;p.TryPlacePiece(relay);Check(Fixture.LastCreated.Piece.View.Data.Free,order+"world free-build rule suppresses relay refund");
   foreign.UnpatchSelf();Fixture.Reset();Check(p.TryPlacePiece(piece)&&Fixture.Creates==1&&Fixture.Attachments==0,order+"RSN still works when the other patch is removed");
  }finally{rsn.UnpatchSelf();foreign.UnpatchSelf();}
 }
 static void GuardTests(){
  var original=PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(Player),"TryPlacePiece"),out var generator);
  var marker=original.Single(i=>i.opcode==OpCodes.Ldstr&&Equals(i.operand,"Placed "));var incoming=generator.DefineLabel();marker.labels.Add(incoming);
  var result=((IEnumerable<CodeInstruction>)Hook("BuildGateIL").Invoke(null,new object[]{original,generator})).ToList();
  int branch=result.FindIndex(i=>i.labels.Contains(incoming)),gate=result.FindIndex(i=>Equals(i.operand,Hook("BuildAllowed")));
  Check(branch<gate&&branch>=0,"incoming valid-placement branches pass through the payment gate");
  foreach(string name in new[]{"BuildGateIL","BuildIL"}){
   bool rejected=false;try{Hook(name).Invoke(null,name=="BuildGateIL"?new object[]{new List<CodeInstruction>(),generator}:new object[]{new List<CodeInstruction>()});}
   catch(TargetInvocationException e){rejected=e.InnerException is InvalidOperationException;}
   Check(rejected,name+" rejects missing anchors");
  }
  original=PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(Player),"TryPlacePiece"),out generator);
  original.Add(new CodeInstruction(OpCodes.Ldstr,"Placed "));bool ambiguous=false;
  try{Hook("BuildGateIL").Invoke(null,new object[]{original,generator});}
  catch(TargetInvocationException e){ambiguous=e.InnerException is InvalidOperationException;}
  Check(ambiguous,"ambiguous gate is rejected before changing instructions");
 }
 [MethodImpl(MethodImplOptions.NoInlining)]
 static int Run(string[] args){
  RunOrder(false);RunOrder(true);GuardTests();
  Console.WriteLine("RESULT "+passed+" build patch checks passed; real Harmony on managed stand-ins, not in-game verification.");return 0;
 }
 public static int Main(string[] args){
  AppDomain.CurrentDomain.AssemblyResolve+=(sender,e)=>{
   foreach(var folder in new[]{args[0],args[1],Path.Combine(args[0],"..","plugins","ValheimModding-Jotunn")}){
    string path=Path.Combine(folder,new AssemblyName(e.Name).Name+".dll");if(File.Exists(path))return Assembly.LoadFrom(path);
   }return null;
  };
  try{return Run(args);}catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}
#endif
