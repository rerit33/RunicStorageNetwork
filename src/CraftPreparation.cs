using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 // Only the selected recipe reserves stock. Cached chest totals are candidates
 // for a request, never evidence that the craft button may be enabled.
 internal static class CraftPreparation {
  static Actions.Pending desired,offer,lostClaim;
  static OfferWindow window;
  static float nextProbe,lastSend,nextValidation,failureSince=-1;
  static string failure;
  static bool queued;
  sealed class Release {internal float Since,Sent;}
  static readonly Dictionary<string,Release> retiring=new Dictionary<string,Release>();
  internal static void Clear(){desired=offer=lostClaim=null;CraftOverview.Clear();CraftInspection.Clear();window=null;retiring.Clear();nextProbe=lastSend=0;failureSince=-1;failure=null;}
  static bool Free(Player p)=>p.NoCostCheat()||ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost);
  static bool Same(Actions.Pending a,Actions.Pending b)=>a!=null&&b!=null&&a.Player==b.Player&&a.Gui==b.Gui&&a.Recipe==b.Recipe&&a.Upgrade==b.Upgrade&&a.UpgradeQuality==b.UpgradeQuality&&a.Op.Station==b.Op.Station&&a.Op.Quality==b.Op.Quality&&a.Op.Multiplier==b.Op.Multiplier&&a.Variant==b.Variant;
  internal static void Selection(InventoryGui gui,Player player){
   if(Actions.Active!=null)return;
   CraftOverview.Open(gui,player);
   if(!player||!gui||!InventoryGui.IsVisible()||player.IsDead()||Free(player)){Cancel();return;}
   var pair=R.Get<object>(gui,"m_selectedRecipe");var type=pair.GetType();
   var recipe=(Recipe)type.GetProperty("Recipe").GetValue(pair,null);
   var upgrade=(ItemDrop.ItemData)type.GetProperty("ItemData").GetValue(pair,null);
   var station=player.GetCurrentCraftingStation();
   if(!recipe||!station||station.m_upgrader||!Plugin.Enabled){Cancel();return;}
   // Touch multi-craft is cleared by vanilla OnCraftPressed. Preserve the
   // claimed quantity while its animation is running.
   var claimed=window?.Claimed==true?offer:lostClaim;
   if(claimed!=null&&claimed.Recipe==recipe&&claimed.Upgrade==upgrade&&claimed.Op.Station==R.View(station)?.GetZDO()?.m_uid)return;
   bool multi=upgrade==null&&(ZInput.GetButton("AltPlace")||ZInput.GetButton("JoyLStick")||R.Get<bool>(gui,"m_touchMultiCrafting"));
   var selection=new Actions.Pending{Player=player,Gui=gui,Recipe=recipe,Upgrade=upgrade,UpgradeQuality=upgrade?.m_quality??0,Variant=R.Get<int>(gui,"m_selectedVariant"),Multi=multi,
    Op=new Operation{Target=recipe.name,Quality=upgrade==null?1:upgrade.m_quality+1,Multiplier=multi?gui.m_multiCraftAmount:1,Station=R.View(station)?.GetZDO()?.m_uid??ZDOID.None}};
   if(Same(desired,selection))return;
   Cancel();desired=selection;nextProbe=Time.unscaledTime;failureSince=-1;
  }
  static bool ContextValid(Actions.Pending p)=>p!=null&&p.Player&&p.Player==Player.m_localPlayer&&!p.Player.IsDead()&&p.Gui&&InventoryGui.IsVisible()&&p.Player.GetCurrentCraftingStation()?.GetComponent<ZNetView>().GetZDO()?.m_uid==p.Op.Station&&(p.Upgrade==null||(p.Player.GetInventory().ContainsItem(p.Upgrade)&&p.Upgrade.m_quality==p.UpgradeQuality));
  internal static bool Personal(Player p,Recipe recipe,int quality,int amount){
   var needs=Stockroom.Requirements(recipe.m_resources,quality,amount);var stock=Stockroom.Snapshot(p.GetInventory(),"player",needs,false);
   return Satisfies(recipe,needs,stock);
  }
  static bool Satisfies(Recipe recipe,List<Need> needs,List<Stock> stock){
   if(recipe.m_requireOnlyOneIngredient)return needs.Any(n=>{var one=new List<Need>{new Need(n.Item,n.Amount)};return Stockroom.Qualities(one,stock,true)&&Planner.Plan(one,stock)!=null;});
   return Stockroom.Qualities(needs,stock,true)&&Planner.Plan(needs,stock)!=null;
  }
  static bool PersonalShare(Actions.Pending p){
   if(p?.Plan==null||!p.Player)return false;
   var stock=Stockroom.Snapshot(p.Player.GetInventory(),"player",p.Op.Needs,false);
   var needs=p.Plan.Where(d=>d.Source=="player").Select(d=>new Need(d.Item,d.Amount,d.Quality));
   return Planner.Plan(needs,stock)!=null;
  }
  static bool Ready()=>offer!=null&&window!=null&&window.Ready(Time.unscaledTime)&&Same(desired,offer)&&ContextValid(offer)&&PersonalShare(offer);
  internal static List<Stock> Stock(Player p,IEnumerable<Need> needs,Recipe recipe=null,int quality=0,int multiplier=0)=>CraftOverview.Stock(p,needs);
  internal static List<Stock> IngredientStock(Player p,Recipe recipe,int quality,int amount,List<Need> needs){
   // Vanilla's alternate-ingredient choice controls the actual debit and output
   // amount. A broad browsing count must never select network stock for a
   // craft which is going to run entirely through personal-inventory vanilla.
   if(Personal(p,recipe,quality,amount))return Stockroom.Snapshot(p.GetInventory(),"player",needs,false);
   if(Ready()&&offer.Player==p&&offer.Recipe==recipe&&offer.Op.Quality==quality&&offer.Op.Multiplier==amount)
    return offer.Plan.Select(d=>new Stock(d.Source,d.Item,d.Quality,d.Amount)).ToList();
   return CraftOverview.Stock(p,needs);
  }
  internal static bool Available(Player p,Recipe r,int quality,int amount){
   var needs=Stockroom.Requirements(r.m_resources,quality,amount);return Satisfies(r,needs,CraftOverview.Stock(p,needs));
  }
  static bool CanStart(Actions.Pending p)=>Personal(p.Player,p.Recipe,p.Op.Quality,p.Op.Multiplier)||(Ready()&&Same(desired,p));
  internal static bool Releasing=>retiring.Count>0;
  internal static bool Claimed=>window?.Claimed==true||lostClaim!=null;
  internal static bool Contains(string id,string key)=>offer?.Op.Id==id&&offer.Op.Sources.Contains(key);
  internal static bool Locked(Inventory inv)=>window?.Claimed==true&&offer!=null&&offer.Player&&offer.Player.GetInventory()==inv;
  internal static bool LockedItem(ItemDrop.ItemData item)=>window?.Claimed==true&&offer!=null&&offer.Player&&offer.Player.GetInventory().ContainsItem(item);
  static void Send(Action action){try{action();}catch(Exception e){Plugin.Debug("preflight transport: "+e.Message);}}
  internal static void Cancel(){
   desired=null;lostClaim=null;CraftInspection.Clear();
   if(offer!=null){string id=offer.Op.Id;offer=null;window?.Cancel();window=null;retiring[id]=new Release{Since=Time.unscaledTime,Sent=Time.unscaledTime};Send(()=>Transport.Instance.DropQuote(id));}
  }
  static void Retire(){var selection=desired;var claim=window?.Claimed==true?offer:null;Cancel();desired=selection;lostClaim=claim;nextProbe=Time.unscaledTime+.5f;}
  internal static void Tick(){
   float now=Time.unscaledTime;
   foreach(var entry in retiring.ToArray()){
    if(now-entry.Value.Since>10)Plugin.Critical(entry.Key,"Unused craft reservation release unconfirmed after 10 seconds; retrying cancellation");
    if(now-entry.Value.Sent>=2){entry.Value.Sent=now;Send(()=>Transport.Instance.DropQuote(entry.Key));}
   }
   if(Actions.Waiting!=null)return;
   if(!ContextValid(desired)){if(desired!=null||offer!=null)Cancel();return;}
   if(lostClaim!=null)return;
   var contextCore=Actions.Context(desired.Player,true);if(contextCore)CraftInspection.Ensure(desired,contextCore);
   if(offer!=null){
    if(window.Confirmed&&!Ready()){Retire();return;}
    if(!window.Confirmed&&now-offer.Started>=8){if(!queued)Problem("preflight confirmation timeout");Retire();return;}
    if(window.Confirmed&&now>=nextValidation){nextValidation=now+.5f;if(!offer.Op.Validate(out _,out _,out _,false)){Retire();return;}}
    if(window.Claimed&&R.Get<float>(offer.Gui,"m_craftTimer")<0){Retire();lostClaim=null;return;}
    if(now-lastSend>=1){lastSend=now;var current=offer;Send(()=>{if(window.Claimed)Transport.Instance.HoldQuote(current.Op.Id);else if(!window.Confirmed)Transport.Instance.Begin(current.Op);});}
    return;
   }
   if(retiring.Count>0||now<nextProbe||!CraftOverview.Ready(desired.Player)||!CraftInspection.Ready)return;nextProbe=now+1;
   if(Personal(desired.Player,desired.Recipe,desired.Op.Quality,desired.Op.Multiplier)){failureSince=-1;return;}
   try{
    var core=contextCore;if(!core)return;
    var op=Actions.Create(desired.Player,core,false,desired.Recipe.name,desired.Op.Quality,desired.Op.Multiplier);op.Quote=true;
    if(!op.Validate(out _,out _,out string why,false)){Problem(why);return;}
    var stock=CraftOverview.Stock(desired.Player,op.Needs);
    var plan=op.SelectNeeds(stock)?Planner.Plan(op.Needs,stock,true):null;
    if(plan==null){failureSince=-1;return;}
    var candidate=new Actions.Pending{Player=desired.Player,Gui=desired.Gui,Recipe=desired.Recipe,Upgrade=desired.Upgrade,UpgradeQuality=desired.UpgradeQuality,Variant=desired.Variant,Multi=desired.Multi,Op=op,Started=now};
    if(!Actions.Propose(candidate,plan)){Problem("invalid source proposal");return;}
    offer=candidate;queued=false;window=new OfferWindow();lastSend=now;Send(()=>Transport.Instance.Begin(op));
   }catch(Exception e){Problem(e.Message);}
  }
  internal static void Progress(string id,bool inQueue){if(offer?.Op.Id==id)queued=inQueue;}
  internal static void Offered(string id,List<Debit> plan,float remaining){
   if(offer?.Op.Id!=id||window==null||window.Claimed||window.Confirmed)return;
   if(!ContextValid(offer)||!Same(desired,offer)||remaining<=.5f){Retire();return;}
   var needs=Stockroom.Requirements(offer.Recipe.m_resources,offer.Op.Quality,offer.Op.Multiplier);
   bool valid=!float.IsNaN(remaining)&&!float.IsInfinity(remaining)&&plan.Count>0&&plan.All(d=>d.Amount>0&&d.Quality>=1&&(d.Source=="player"||offer.Op.Sources.Contains(d.Source))&&needs.Any(n=>n.Item==d.Item))&&Satisfies(offer.Recipe,needs,plan.Select(d=>new Stock(d.Source,d.Item,d.Quality,d.Amount)).ToList());
   offer.Plan=plan;
   if(!valid||!PersonalShare(offer)){Problem("preflight plan changed");Retire();return;}
   window.Confirm(Math.Min(offer.Started+8,Time.unscaledTime+remaining-.5));
   if(!window.Ready(Time.unscaledTime)){Retire();return;}
   failureSince=-1;failure=null;
  }
  internal static void Refused(string id,string reason){
   if(retiring.Remove(id))return;
   if(offer?.Op.Id!=id)return;
   CraftInspection.Clear();
   if(window?.Claimed==true)lostClaim=offer;
   offer=null;window=null;nextProbe=Time.unscaledTime+1;Topology.Dirty();Problem(reason);
  }
  static void Problem(string reason){
   Plugin.Debug("preflight: "+reason+" target="+(desired?.Op.Target??"none")+(RecipeIndex.Ambiguous(desired?.Op.Target)?" (ambiguous recipe name)":""));
   if(reason=="offer expired"||reason=="offer cancelled"||reason.Contains("insufficient")||reason.Contains("busy/reserved")||reason=="queue timeout"||reason=="station unavailable"||reason=="recipe unavailable"||reason.Contains("access denied")){failureSince=-1;return;}
   failure=reason;if(failureSince<0)failureSince=Time.unscaledTime;
   if(Time.unscaledTime-failureSince>=8)Plugin.Critical("preflight-"+desired?.Op.Target,"Cannot confirm selected recipe after background recovery: "+failure);
   Topology.Dirty();
  }
  static bool Capacity(Actions.Pending p){
   string dlc=p.Recipe.m_item.m_itemData.m_shared.m_dlc;
   if(dlc.Length>0&&!DLCMan.instance.IsDLCInstalled(dlc))return false;
   if(p.Upgrade!=null)return true;
   int amount=p.Recipe.GetAmount(p.Op.Quality,out _,out _,p.Op.Multiplier);
   // Vanilla adds each accumulated bonus to the result inside the loop.
   // Reserve enough output space for its maximum possible bonus as well.
   var station=p.Player.GetCurrentCraftingStation();
   if(station&&station.m_craftingSkill!=Skills.SkillType.None&&p.Player.GetSkillFactor(station.m_craftingSkill)>0&&p.Gui.m_craftBonusChance>0&&p.Recipe.m_item.m_itemData.m_shared.m_maxStackSize>1)
    amount=CraftCapacity.Maximum(amount,p.Op.Multiplier,p.Gui.m_craftBonusAmount);
   return amount>0&&p.Player.GetInventory().CanAddItem(p.Recipe.m_item.gameObject,amount);
  }
  internal static void Button(InventoryGui gui){
   if(Actions.Active!=null)return;
   if(Actions.Waiting!=null)gui.m_craftButton.interactable=false;
   if(desired?.Gui==gui&&gui.m_craftButton.interactable&&(!CanStart(desired)||!Capacity(desired)))gui.m_craftButton.interactable=false;
   CraftOverview.RefreshRows(gui);
  }
  internal static bool Press(InventoryGui gui){
   if(Actions.Waiting!=null||window?.Claimed==true||lostClaim!=null)return false;
   Selection(gui,Player.m_localPlayer);var p=desired;
   if(p==null)return true;
   if(!gui.m_craftButton.interactable||!Capacity(p))return false;
   if(Personal(p.Player,p.Recipe,p.Op.Quality,p.Op.Multiplier)){Cancel();return true;}
   if(!Ready()||!offer.Op.Validate(out _,out _,out _,false))return false;
   if(!window.Claim(Time.unscaledTime))return false;
   lastSend=Time.unscaledTime;Send(()=>Transport.Instance.HoldQuote(offer.Op.Id));return true;
  }
  internal static void Pressed(InventoryGui gui){if(window?.Claimed==true&&R.Get<float>(gui,"m_craftTimer")<0){Retire();lostClaim=null;}}
  internal static bool Execute(InventoryGui gui,Player player){
   var recipe=R.Get<Recipe>(gui,"m_craftRecipe");var upgrade=R.Get<ItemDrop.ItemData>(gui,"m_craftUpgradeItem");
   int quality=upgrade==null?1:upgrade.m_quality+1,amount=R.Get<bool>(gui,"m_multiCrafting")?gui.m_multiCraftAmount:1;
   if(offer==null||window?.Claimed!=true){
    var retry=lostClaim;lostClaim=null;
    if(retry!=null&&retry.Gui==gui&&retry.Player==player&&retry.Recipe==recipe&&retry.Upgrade==upgrade){Actions.RecoverCraft(retry);return false;}
    return Personal(player,recipe,quality,amount);
   }
   if(!Ready()||offer.Gui!=gui||offer.Player!=player||offer.Recipe!=recipe||offer.Upgrade!=upgrade||offer.Op.Quality!=quality||offer.Op.Multiplier!=amount||!window.Consume(Time.unscaledTime)){var retry=offer;Retire();lostClaim=null;Actions.RecoverCraft(retry);return false;}
   var pending=offer;offer=null;window=null;pending.Prepared=true;pending.Started=Time.unscaledTime;pending.LastPoll=pending.Started;pending.Recovery=new RecoveryAttempt(pending.Started);pending.Recovery.Sent();Actions.Waiting=pending;
   Send(()=>Transport.Instance.UseQuote(pending.Op.Id));return false;
  }
 }
}
