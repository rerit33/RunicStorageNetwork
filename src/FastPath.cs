using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 internal static class FastPath {
  sealed class Source:FastPathCoreSource {
   internal string SourceKey;internal Container Container;internal Inventory Inventory;internal ZDO Data;internal long InitialOwner;internal InventoryDelta Delta;
   internal bool Held;internal int[] Order;
   internal override string Key=>SourceKey;
   internal override long Owner=>InitialOwner;
   internal override long CurrentOwner=>Data.GetOwner();
   internal override uint Revision=>Data.DataRevision;
   internal override bool IsValid=>Data.IsValid()&&Container&&Inventory!=null;
   internal override byte[] Bytes=>InventoryBytes(Inventory);
   internal override string DebitDescription=>DebitDescriptionOf(Delta);
   internal override bool RestoreComplete=>Delta.Parts.All(part=>part.Removed==0);
   internal override void AcquireHold(){
    if(!IsValid||Holds(Data)||heldInventories.ContainsKey(Inventory))throw new FastPathFenceException("source became reserved: "+SourceKey);
    Order=InventoryOrder(Inventory);heldSources.Add(Data.m_uid,this);heldInventories.Add(Inventory,this);Held=true;Integrations.Block(Inventory,true);R.Set(Container,"m_inUse",true);
   }
   internal override void ApplyDebit(){Transport.InternalMutation++;try{Delta.Apply();}finally{Transport.InternalMutation--;}}
   internal override void Save(){Transport.InternalMutation++;try{R.Call(Container,"Save");}finally{Transport.InternalMutation--;}}
   internal override void RestoreDebit(){
    Transport.InternalMutation++;try{Delta.Restore();RestoreInventoryOrder(Inventory,Order);}finally{Transport.InternalMutation--;}
   }
   // Keep the hold until integration unblocking succeeds so later frames can retry.
   internal override void ReleaseHold(){if(!Held)return;Integrations.Block(Inventory,false);try{if(Container)R.Set(Container,"m_inUse",false);}finally{DropHold();}}
   void DropHold(){heldInventories.Remove(Inventory);heldSources.Remove(Data.m_uid);Held=false;}
   internal override void ClearHold(){if(!Held)return;try{Integrations.Block(Inventory,false);}finally{try{if(Container)R.Set(Container,"m_inUse",false);}finally{DropHold();}}}
  }
  sealed class PlayerAdapter:FastPathCorePlayer {
   internal Player Value;internal ItemDrop.ItemData Tool;internal InventoryDelta Delta;internal int[] Order;
   internal override byte[] Bytes=>InventoryBytes(Value.GetInventory());
   internal override string DebitDescription=>DebitDescriptionOf(Delta);
   internal override bool RestoreComplete=>Delta.Parts.All(part=>part.Removed==0);
   internal override void AcquireHold(){Order=InventoryOrder(Value.GetInventory());lockedPlayerInventory=Value.GetInventory();lockedTool=Tool;}
   internal override void ApplyDebit(){Transport.InternalMutation++;try{Delta.Apply();}finally{Transport.InternalMutation--;}}
   internal override void RestoreDebit(){Transport.InternalMutation++;try{Delta.Restore();RestoreInventoryOrder(Value.GetInventory(),Order);}finally{Transport.InternalMutation--;}}
   internal override void ReleaseHold(){if(lockedPlayerInventory==Value.GetInventory())lockedPlayerInventory=null;if(lockedTool==Tool)lockedTool=null;}
   internal override void ClearHold(){lockedPlayerInventory=null;lockedTool=null;}
  }
  sealed class Transaction {internal PlayerAdapter Player;internal string OperationId;}

  static readonly Dictionary<Inventory,Source> heldInventories=new Dictionary<Inventory,Source>();
  static readonly Dictionary<ZDOID,Source> heldSources=new Dictionary<ZDOID,Source>();
  static readonly FastPathCore engine=new FastPathCore(message=>Plugin.Log.LogWarning("[RSN] "+message),Plugin.Debug);
  static Inventory lockedPlayerInventory;static ItemDrop.ItemData lockedTool;static Transaction current;

  internal static bool Running=>engine.Running;
  internal static bool Active=>engine.Active;
  internal static IEnumerable<ZDO> HeldZDOs=>heldSources.Values.Select(source=>source.Data);
  internal static bool Holds(ZDO data)=>data!=null&&heldSources.ContainsKey(data.m_uid);
  internal static bool Locked(Inventory inventory)=>inventory!=null&&(heldInventories.ContainsKey(inventory)||lockedPlayerInventory==inventory);
  // Keep transfers from copying carried items while a player refund is pending.
  internal static bool LockedItem(ItemDrop.ItemData item)=>item!=null&&(item==lockedTool||(lockedPlayerInventory!=null&&lockedPlayerInventory.ContainsItem(item))||heldInventories.Keys.Any(inventory=>inventory.ContainsItem(item)));
  internal static bool Blocks(Player player)=>Owns(player)&&engine.Blocks(current.Player);
  internal static bool Owns(Player player)=>current!=null&&current.Player.Value==player;
  internal static bool NativeReady(Player player)=>Owns(player)&&engine.NativeReady(current.Player);
  internal static bool ConsumeNativePlacement(Player player)=>Owns(player)&&engine.ConsumeNativePlacement(current.Player);

  internal static bool TryBuild(Actions.Pending pending,Core core,List<Debit> preview){
   var stopwatch=System.Diagnostics.Stopwatch.StartNew();
   string operationId=pending?.Op?.Id,pieceName=pending!=null&&pending.Piece?pending.Piece.name:"missing";
   List<Debit> plan;List<Source> sources;PlayerAdapter playerAdapter;string reason;
   try {
    if(!Eligible(pending,core,preview,out plan,out sources,out reason)){stopwatch.Stop();Plugin.Debug("fast path "+operationId+" "+pieceName+" fallback reason="+reason+" ms="+stopwatch.ElapsedMilliseconds);return false;}
    playerAdapter=new PlayerAdapter{Value=pending.Player,Tool=pending.Tool,Delta=new InventoryDelta(pending.Player.GetInventory(),plan.Where(debit=>debit.Source=="player"),false)};
    foreach(var source in sources)source.Delta=new InventoryDelta(source.Inventory,plan.Where(debit=>debit.Source==source.SourceKey),true);
   }catch(Exception error){stopwatch.Stop();Plugin.Debug("fast path "+operationId+" "+pieceName+" fallback reason=error ms="+stopwatch.ElapsedMilliseconds+": "+error.Message);return false;}
   current=new Transaction{Player=playerAdapter,OperationId=operationId};
   bool wasRunning=engine.Running;
   bool handled;
   try{handled=engine.TryBuild(playerAdapter,sources.Cast<FastPathCoreSource>().ToList(),()=>pending.Player.TryPlacePiece(pending.Piece),()=>{
    pending.Output=true;Transport.InternalMutation++;try{Actions.FinishBuild(pending);}finally{Transport.InternalMutation--;}
   },()=>pending.Output,placing=>Actions.Active=placing?pending:null);}
   catch(Exception error){Plugin.Log.LogWarning("[RSN] fast path unexpected build failure: "+error.Message);handled=engine.Running;}
   finally{if(!engine.Running)current=null;}
   if(handled){Stockroom.ClearObservations();CraftInspection.Clear();CraftOverview.Rescan();Topology.Dirty();}
   stopwatch.Stop();Plugin.Debug("fast path "+operationId+" "+pieceName+(handled?" handled sources="+sources.Count:" fallback reason=transaction")+" ms="+stopwatch.ElapsedMilliseconds);
   if(!wasRunning&&engine.Running)Plugin.Debug("fast path "+operationId+" pending "+(engine.Compensating?"compensation":"cleanup"));
   return handled;
  }

  static bool Eligible(Actions.Pending pending,Core core,List<Debit> preview,out List<Debit> plan,out List<Source> sources,out string reason){
   plan=null;sources=null;reason=null;
   if(pending==null){reason="pending";return false;}var player=pending.Player;var piece=pending.Piece;var operation=pending.Op;
   if(!player){reason="player";return false;}
   if(!piece){reason="piece";return false;}
   if(operation==null){reason="operation";return false;}
   if(core==null){reason="core";return false;}
   if(preview==null||!preview.Any(debit=>debit.Source!="player"&&debit.Amount>0)){reason="no-network-debit";return false;}
   bool enabled=Plugin.OwnerFastPath!=null&&Plugin.OwnerFastPath.Value,local=player==Player.m_localPlayer,carried=Actions.LocalBuildMaterials(player,piece,Player.RequirementMode.CanBuild),inventoryLocked=Transport.Locked(player.GetInventory()),toolLocked=Transport.LockedItem(pending.Tool),waiting=Actions.Waiting!=null,active=Actions.Active!=null,running=Running;
   if(!FastPathCore.PlayerEligible(enabled,local,carried,inventoryLocked,toolLocked,waiting,active,running)){reason=!enabled?"disabled":!local?"player":carried?"carried":inventoryLocked?"inventory-locked":toolLocked?"tool-locked":waiting?"waiting":active?"active":"running";return false;}
   core.Scan();var selected=preview.Where(debit=>debit.Source!="player"&&debit.Amount>0).Select(debit=>debit.Source).Distinct(StringComparer.Ordinal).ToArray();var stock=Stockroom.Snapshot(player.GetInventory(),"player",operation.Needs,false);var byKey=new Dictionary<string,Container>(StringComparer.Ordinal);
   foreach(string key in selected){
    var container=core.Pool.FirstOrDefault(candidate=>candidate&&R.Valid(R.View(candidate))&&R.Key(R.View(candidate).GetZDO().m_uid)==key);
    var view=R.View(container);if(!container||!R.Valid(view)){reason="source-missing";return false;}var data=view.GetZDO();var inventory=container.GetInventory();
    var state=new FastPathSourceState{Valid=data.IsValid()&&inventory!=null,SceneOwner=view.IsOwner(),Owner=data.GetOwner(),LocalOwner=ZNet.GetUID(),Replica=container.GetComponent<UnloadedReplica>(),Access=Access.Container(container,player.GetPlayerID(),core,out _,ownLease:false),InUse=container.IsInUse(),NetworkInUse=data.GetInt(ZDOVars.s_inUse)!=0,Locked=Transport.Locked(inventory),Reserved=Transport.Reserved(data),Busy=Integrations.IsBusy(inventory),Held=Holds(data)};
    if(!FastPathCore.SourceCanReload(state)){reason=!state.Valid?"source-valid":!state.SceneOwner?"source-scene-owner":state.Owner!=state.LocalOwner?"source-owner":!state.Access?"source-access":state.Replica?"source-replica":state.InUse?"source-in-use":state.NetworkInUse?"source-network-in-use":state.Locked?"source-locked":state.Reserved?"source-reserved":state.Busy?"source-busy":"source-held";return false;}
    state.Fresh=UnloadedNetworks.LoadForRead(container)&&R.Get<uint>(container,"m_lastRevision")==data.DataRevision;
    if(!FastPathCore.SourceEligible(state)){reason="stale";return false;}
    stock.AddRange(GatewayRuntime.Local(core,player.GetPlayerID(),container,Stockroom.Snapshot(inventory,key,operation.Needs,true)));byKey.Add(key,container);
   }
   plan=Planner.Plan(operation.Needs,stock,true);if(plan==null||!GatewayRuntime.ValidatePlan(operation,plan)){reason="plan";return false;}
   var actual=plan.Where(debit=>debit.Source!="player"&&debit.Amount>0).Select(debit=>debit.Source);
   if(!FastPathCore.PlanSourcesMatch(selected,actual)){reason="plan-sources";return false;}
   sources=new List<Source>();foreach(string key in selected){var container=byKey[key];var data=R.View(container).GetZDO();sources.Add(new Source{SourceKey=key,Container=container,Inventory=container.GetInventory(),Data=data,InitialOwner=data.GetOwner()});}
   return true;
  }

  static string DebitDescriptionOf(InventoryDelta delta)=>string.Join(",",delta.Parts.Where(part=>part.Removed>0).GroupBy(part=>part.Item.m_dropPrefab.name,StringComparer.Ordinal).Select(group=>group.Key+"="+group.Sum(part=>part.Removed)));
  static int[] InventoryOrder(Inventory inventory)=>inventory.GetAllItems().Select(item=>item.m_gridPos.y*inventory.GetWidth()+item.m_gridPos.x).ToArray();
  static void RestoreInventoryOrder(Inventory inventory,int[] order){var items=inventory.GetAllItems();var restored=FastPathCore.RestoreOrder(items,order,item=>item.m_gridPos.y*inventory.GetWidth()+item.m_gridPos.x);items.Clear();items.AddRange(restored);R.Call(inventory,"Changed",new[]{typeof(bool),typeof(bool)},false,false);}
  internal static void Tick(){
   bool wasRunning=engine.Running,wasCompensating=engine.Compensating;string operationId=current?.OperationId;
   engine.Tick();
   if(wasRunning&&!engine.Running)Plugin.Debug("fast path "+operationId+" settled");
   else if(wasCompensating&&!engine.Compensating&&engine.Running)Plugin.Debug("fast path "+operationId+" pending cleanup");
   if(!engine.Running)current=null;
  }
  internal static void ClearWorld(){engine.Clear();heldInventories.Clear();heldSources.Clear();lockedPlayerInventory=null;lockedTool=null;current=null;}
  internal static byte[] InventoryBytes(Inventory inventory){var package=new ZPackage();inventory.Save(package);return package.GetArray();}
 }
}
