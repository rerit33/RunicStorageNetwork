using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 internal static class Actions {
  internal sealed class Pending {
   internal Operation Op;internal Player Player;internal InventoryGui Gui;internal Recipe Recipe;internal Piece Piece;internal ItemDrop.ItemData Upgrade,Tool;
   internal int UpgradeQuality,Variant;internal bool Multi,Output,Cancelled,Prepared;internal Vector3 Position;internal Quaternion Rotation;internal float Started,LastPoll;internal bool Warned;
   internal RecoveryAttempt Recovery;
   internal string RollbackFailure;
   internal List<Debit> Plan;internal InventoryDelta Delta;
  }
  internal static Pending Waiting,Active;
  static readonly OutcomeReceipts Outcomes=new OutcomeReceipts();
  internal static bool Locked(Inventory inv)=>Waiting!=null&&Waiting.Player&&Waiting.Player.GetInventory()==inv;
  internal static void Clear(){Waiting=null;Active=null;Outcomes.Clear();Stockroom.ClearObservations();CraftPreparation.Clear();Plugin.ClearCritical();}
  // The serving tray, hoe and cultivator also use TryPlacePiece/HaveRequirements.
  // BuildToolPolicy decides which tables take part; the equipped tool must own the table
  // it is placing from, so a tool cannot borrow another tool's pieces.
  internal static PieceTable BuildTable(Player p){
   if(!p||p!=Player.m_localPlayer||!ObjectDB.instance)return null;
   var tool=(ItemDrop.ItemData)R.Call(p,"GetRightItem",Type.EmptyTypes);
   var table=tool?.m_shared.m_buildPieces;
   return table&&p.GetBuildTool()==table&&BuildToolPolicy.Table(table)?table:null;
  }
  internal static bool BuildPiece(Player p,Piece piece){
   var table=BuildTable(p);if(!table||!piece||piece.m_repairPiece||piece.m_removePiece)return false;
   var prefab=ZNetScene.instance?ZNetScene.instance.GetPrefab(R.Id(piece.gameObject)):piece.gameObject;
   return prefab&&table.m_pieces.Contains(prefab)&&BuildToolPolicy.Eligible(prefab);
  }
  internal static Core Context(Player p,bool craft){if(!p||p!=Player.m_localPlayer||!Plugin.Enabled||(!craft&&!BuildTable(p)))return null;var station=p.GetCurrentCraftingStation();if(craft&&(!station||station.m_upgrader))return null;return Core.Choose(craft?station.transform.position:p.transform.position,p.GetPlayerID());}
  internal static bool Craft(InventoryGui gui,Player p){
   if(Active!=null)return true;if(Waiting!=null)return false;
   if(CraftPreparation.Claimed)return CraftPreparation.Execute(gui,p);
   var recipe=R.Get<Recipe>(gui,"m_craftRecipe");var core=Context(p,true);
   if(!recipe||!core||p.NoCostCheat()||ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost))return true;
   return CraftPreparation.Execute(gui,p);
  }

  internal static bool Build(Player p,Piece piece){
   if(Active!=null)return true;if(Waiting!=null)return false;
   if(!BuildPiece(p,piece))return true;var core=Context(p,false);
   if(!core||p.NoCostCheat()||R.Get<bool>(p,"m_noPlacementCost")||ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()))return true;
   var op=Create(p,core,true,R.Id(piece.gameObject),0,1);
   if(piece.m_craftingStation){var station=CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name,p.transform.position);if(station&&R.Valid(R.View(station)))op.Station=R.View(station).GetZDO().m_uid;}
   if(!op.Validate(out _,out _,out _))return false;
   var plan=Planner.Plan(op.Needs,Stockroom.Available(p,core,op.Needs),true);if(plan==null)return false;if(plan.All(d=>d.Source=="player"))return true;
   R.Call(p,"UpdatePlacementGhost",new[]{typeof(bool)},false);
   if(R.Get<object>(p,"m_placementStatus").ToString()!="Valid")return true;
   var ghost=R.Get<GameObject>(p,"m_placementGhost");if(!ghost)return true;
   Start(new Pending{Op=op,Player=p,Piece=piece,Tool=(ItemDrop.ItemData)R.Call(p,"GetRightItem",Type.EmptyTypes),Position=ghost.transform.position,Rotation=ghost.transform.rotation},plan);return false;
  }
  internal static Operation Create(Player p,Core core,bool build,string target,int quality,int multiplier){return new Operation{Id=System.Guid.NewGuid().ToString("N"),Target=target,Build=build,Quality=quality,Multiplier=multiplier,Actor=p.GetZDOID(),Station=build?ZDOID.None:R.View(p.GetCurrentCraftingStation()).GetZDO().m_uid,Core=core.Id,Network=core.GetComponent<NetworkMember>().SavedNetwork,Peer=ZNet.GetUID(),PlayerId=p.GetPlayerID()};}
  internal static bool Propose(Pending pending,List<Debit> preview){
   var root=Operation.CoreObject(pending.Op.Core);var network=root?root.GetComponent<NetworkMember>().Network:"";
   pending.Op.Nodes=Topology.Members.Where(m=>m&&m.Valid&&m.Network==network).Select(m=>m.View.GetZDO().m_uid).Distinct().ToArray();
   if(pending.Op.Nodes.Length==0||pending.Op.Nodes.Length>4096)return false;
   pending.Op.Sources=SourceSelection.Sources(preview);if(pending.Op.Sources==null)return false;
   pending.Op.PlayerStock=Stockroom.Snapshot(pending.Player.GetInventory(),"player",pending.Op.Needs,false);return true;
  }
  static void Start(Pending pending,List<Debit> preview){
   if(!Propose(pending,preview)){if(Waiting==pending)Waiting=null;return;}
   Waiting=pending;if(pending.Recovery==null){pending.Started=Time.unscaledTime;pending.Recovery=new RecoveryAttempt(pending.Started);}
   pending.Recovery.Sent();pending.LastPoll=Time.unscaledTime;
   try{Transport.Instance.Begin(pending.Op);}catch(Exception e){Plugin.Debug(pending.Op.Id+" request; will replay same ID: "+e.Message);}
  }
  internal static void RecoverCraft(Pending pending){
   pending.Prepared=false;pending.Started=Time.unscaledTime;pending.Recovery=new RecoveryAttempt(pending.Started);pending.Recovery.Retry("offer expired",pending.Started);Waiting=pending;Topology.Dirty();
  }
  internal static void Tick(){
   var p=Waiting;if(p==null)return;float now=Time.unscaledTime;
   if(!Intent(p,out _))p.Cancelled=true;
   if(!p.Warned&&now-p.Started>10){p.Warned=true;Plugin.Critical(p.Op.Id,"Craft/build acknowledgement unresolved after 10 seconds; replaying the existing operation");}
   if(p.Recovery.InFlight){
    if(now-p.LastPoll>=2){p.LastPoll=now;try{if(p.Prepared)Transport.Instance.UseQuote(p.Op.Id);else Transport.Instance.Begin(p.Op);}catch(Exception e){Plugin.Debug(p.Op.Id+" waiting for coordinator: "+e.Message);}}
    return;
   }
   if(now<p.Recovery.Due||CraftPreparation.Releasing)return;
   if(!Intent(p,out _)||p.Recovery.Expired(now)){Waiting=null;Topology.Dirty();return;}
   Retry(p);
  }
  internal static void Cancel(){if(Waiting!=null)Waiting.Cancelled=true;}
  internal static void Refused(string id,string reason){
   Plugin.Debug(id+" terminal refusal (resources released): "+reason);
   var p=Waiting;if(p?.Op.Id!=id||!p.Recovery.InFlight)return;
   p.Prepared=false;
   if(p.Recovery.Retry(reason,Time.unscaledTime)&&Intent(p,out _)){Topology.Dirty();Plugin.Debug(id+" rebuilding supply and retrying automatically");return;}
   if(!p.Cancelled&&p.Recovery.Expired(Time.unscaledTime)&&RecoveryAttempt.Transient(reason)&&!reason.Contains("insufficient"))Plugin.Critical(id,"Automatic recovery exhausted: "+reason);
   Waiting=null;Topology.Dirty();
  }
  static void Retry(Pending p){
   try{
    Topology.Dirty();Topology.Refresh(true);var core=Context(p.Player,!p.Op.Build);
    if(!core){if(!p.Recovery.Retry("network path or coverage changed",Time.unscaledTime))Waiting=null;return;}
    var next=Create(p.Player,core,p.Op.Build,p.Op.Target,p.Op.Quality,p.Op.Multiplier);next.Station=p.Op.Station;
    if(!next.Validate(out _,out _,out string why)){if(!p.Recovery.Retry(why,Time.unscaledTime))Waiting=null;return;}
    Stockroom.Refresh(core,p.Op.Sources);var stock=Stockroom.Available(p.Player,core,next.Needs);
    var plan=next.SelectNeeds(stock)?Planner.Plan(next.Needs,stock,true):null;
    if(plan==null){if(!p.Recovery.Retry("insufficient fresh resources",Time.unscaledTime))Waiting=null;return;}
    Plugin.Debug(p.Op.Id+" retry -> "+next.Id+" attempt="+(p.Recovery.Attempts+1));p.Op=next;p.Plan=null;p.Delta=null;Start(p,plan);
   }catch(Exception e){Plugin.Error(p.Op.Id+" recovery",e);Waiting=null;}
  }
  static bool Intent(Pending p,out string reason){
   reason="player context changed";if(p.Cancelled||!p.Player||p.Player!=Player.m_localPlayer||p.Player.IsDead()||p.Player.NoCostCheat())return false;
   if(p.Op.Build){
    reason="hammer context changed";if(!BuildPiece(p.Player,p.Piece)||!p.Player.InPlaceMode()||p.Player.GetSelectedPiece()!=p.Piece||(ItemDrop.ItemData)R.Call(p.Player,"GetRightItem",Type.EmptyTypes)!=p.Tool)return false;
   }else{
    reason="craft cancelled/changed";if(!p.Gui||!InventoryGui.IsVisible()||p.Player.GetCurrentCraftingStation()?.GetComponent<ZNetView>().GetZDO()?.m_uid!=p.Op.Station||R.Get<Recipe>(p.Gui,"m_craftRecipe")!=p.Recipe||R.Get<ItemDrop.ItemData>(p.Gui,"m_craftUpgradeItem")!=p.Upgrade)return false;
    var selection=R.Get<object>(p.Gui,"m_selectedRecipe");if((Recipe)selection.GetType().GetProperty("Recipe").GetValue(selection,null)!=p.Recipe)return false;
    if(p.Upgrade!=null&&(!p.Player.GetInventory().ContainsItem(p.Upgrade)||p.Upgrade.m_quality!=p.UpgradeQuality))return false;
   }
   reason="ok";return true;
  }
  internal static void Ready(string id,List<Debit> plan){
   if(Outcomes.TryGet(id,out var recorded)){Transport.Instance.Result(id,recorded.Success,recorded.Reason);return;}
   var pending=Waiting;if(pending==null||pending.Op.Id!=id){Transport.Instance.Result(id,false,"no pending action");return;}
   if(pending.RollbackFailure!=null){
    Transport.InternalMutation++;try{pending.Delta.Restore();}catch(Exception e){Plugin.Error(id+" retrying local rollback",e);return;}finally{Transport.InternalMutation--;}
    Outcomes.Record(id,false,pending.RollbackFailure);Transport.Instance.Result(id,false,pending.RollbackFailure);return;
   }
   bool success=false;string failure="action not completed";
   try {
    if(!Intent(pending,out string intent))throw new InvalidOperationException(intent);
    if(!pending.Op.Validate(out _,out var core,out string why))throw new InvalidOperationException(why);
    // Recheck every planned physical source immediately before producing the result.
    // The receiving client may have unloaded a branch since the coordinator's check.
    core.Scan();
    foreach(var debit in plan.Where(d=>d.Source!="player")){
     var c=core.Pool.FirstOrDefault(source=>source&&R.Valid(R.View(source))&&R.Key(R.View(source).GetZDO().m_uid)==debit.Source);
     if(!c||!Access.Container(c,pending.Op.PlayerId,core,out _,true))throw new InvalidOperationException("source path/access changed before result: "+debit.Source);
    }
    if(!pending.Player||pending.Player!=Player.m_localPlayer||pending.Player.IsDead()||pending.Player.NoCostCheat())throw new InvalidOperationException("player context changed");
    if(pending.Op.Build){
     if(!BuildPiece(pending.Player,pending.Piece)||R.Get<bool>(pending.Player,"m_noPlacementCost")||!pending.Player.InPlaceMode()||pending.Player.GetSelectedPiece()!=pending.Piece||(ItemDrop.ItemData)R.Call(pending.Player,"GetRightItem",Type.EmptyTypes)!=pending.Tool||pending.Tool==null||pending.Tool.m_durability<=0||!pending.Player.HaveStamina(pending.Tool.m_shared.m_attack.m_attackStamina))throw new InvalidOperationException("hammer context changed");
     R.Call(pending.Player,"UpdatePlacementGhost",new[]{typeof(bool)},false);var ghost=R.Get<GameObject>(pending.Player,"m_placementGhost");
     if(!ghost||R.Get<object>(pending.Player,"m_placementStatus").ToString()!="Valid"||Vector3.Distance(ghost.transform.position,pending.Position)>0.05f||Quaternion.Angle(ghost.transform.rotation,pending.Rotation)>0.5f)throw new InvalidOperationException("placement moved/cancelled");
    }else{
     if(!pending.Gui||!InventoryGui.IsVisible()||pending.Player.GetCurrentCraftingStation()?.GetComponent<ZNetView>().GetZDO().m_uid!=pending.Op.Station||R.Get<Recipe>(pending.Gui,"m_craftRecipe")!=pending.Recipe||R.Get<ItemDrop.ItemData>(pending.Gui,"m_craftUpgradeItem")!=pending.Upgrade)throw new InvalidOperationException("craft cancelled/changed");
     var selection=R.Get<object>(pending.Gui,"m_selectedRecipe");
     if((Recipe)selection.GetType().GetProperty("Recipe").GetValue(selection,null)!=pending.Recipe)throw new InvalidOperationException("recipe selection changed");
     if(pending.Upgrade!=null&&(!pending.Player.GetInventory().ContainsItem(pending.Upgrade)||pending.Upgrade.m_quality!=pending.UpgradeQuality))throw new InvalidOperationException("upgrade item changed");
    }
    pending.Plan=plan;pending.Delta=new InventoryDelta(pending.Player.GetInventory(),plan.Where(d=>d.Source=="player"),false);
    Active=pending;Transport.InternalMutation++;
    try {
     pending.Delta.Apply();Plugin.Debug(id+" player debit confirmed");
     if(pending.Op.Build){
      bool built=pending.Player.TryPlacePiece(pending.Piece);if(built){pending.Output=true;FinishHammer(pending);}
     }else {R.Set(pending.Gui,"m_craftVariant",pending.Variant);R.Set(pending.Gui,"m_multiCrafting",pending.Multi);pending.Gui.m_multiCraftAmount=pending.Op.Multiplier;R.Call(pending.Gui,"DoCrafting",new[]{typeof(Player)},pending.Player);}
     success=pending.Output;
     if(!success){pending.Delta.Restore();Plugin.Debug(id+" player delta restored: no result");}
    }finally{Transport.InternalMutation--;Active=null;}
   }catch(Exception e){
    failure=e.Message;if(e is InvalidOperationException)Plugin.Debug(id+" action postponed: "+e.Message);else Plugin.Error(id+" action",e);success=pending.Output;
    if(!success&&pending.Delta?.Applied==true){Transport.InternalMutation++;try{pending.Delta.Restore();}catch(Exception rollback){pending.RollbackFailure=failure;Plugin.Error(id+" local rollback unresolved",rollback);return;}finally{Transport.InternalMutation--;}}
   }
   Outcomes.Record(id,success,success?"result observed":failure);if(success){Waiting=null;Stockroom.ClearObservations();CraftInspection.Clear();CraftOverview.Rescan();Topology.Dirty();}Transport.Instance.Result(id,success,success?"result observed":failure);
  }
  static void FinishHammer(Pending p){
   R.Set(p.Player,"m_lastToolUseTime",Time.time);p.Player.UseStamina((float)R.Call(p.Player,"GetBuildStamina",Type.EmptyTypes));
   var table=R.Get<PieceTable>(p.Player,"m_buildPieces");
   if(table.m_skill!=Skills.SkillType.None){int debt=R.Get<int>(p.Player,"m_buildRemoveDebt");if(debt>0)R.Set(p.Player,"m_buildRemoveDebt",debt-1);else p.Player.RaiseSkill(table.m_skill);}
   if(p.Tool.m_shared.m_useDurability)p.Tool.m_durability-=(float)R.Call(p.Player,"GetPlaceDurability",new[]{typeof(ItemDrop.ItemData)},p.Tool)*Game.m_durabilityRate;
   p.Tool.m_shared.m_buildEffect.Create(p.Player.transform.position,Quaternion.identity,null,1,-1,p.Player.GetZDOID());
  }
  internal static bool PaidRequirements(Player p,Recipe recipe)=>Active!=null&&Active.Player==p&&Active.Recipe==recipe;
  internal static bool HaveCraft(Player player,Recipe recipe,int quality,int amount,out bool result){
   result=false;if(PaidRequirements(player,recipe)){result=true;return true;}var core=Context(player,true);if(!core)return false;
   result=CraftPreparation.Available(player,recipe,quality,amount);return true;
  }

  internal static bool FirstIngredient(Player p,Recipe recipe,int quality,int multiplier,out ItemDrop.ItemData item,out int amount,out int extra){
   item=null;amount=extra=0;var core=Context(p,true);if(!core)return false;
   var needs=Stockroom.Requirements(recipe.m_resources,quality,multiplier);List<Stock> stock;
   if(Active?.Recipe==recipe)stock=Active.Plan.Select(d=>new Stock(d.Source,d.Item,d.Quality,d.Amount)).ToList();else stock=CraftPreparation.IngredientStock(p,recipe,quality,multiplier,needs);
   foreach(var n in needs){var one=new List<Need>{n};if(!Stockroom.Qualities(one,stock,true))continue;var req=recipe.m_resources.First(r=>r.m_resItem&&r.m_resItem.name==n.Item);item=req.m_resItem.m_itemData.Clone();item.m_quality=n.Quality;amount=n.Amount;extra=req.m_extraAmountOnlyOneIngredient;return true;}
   return false;
  }
 }
}
