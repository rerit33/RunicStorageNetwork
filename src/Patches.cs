using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TMPro;
using UnityEngine;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 internal static class Patches {
  internal static void Install(Harmony h){
   Patch(h,typeof(Container),"GetHoverText",Type.EmptyTypes,null,nameof(ContainerInfo));
   Patch(h,typeof(Hud),"SetupPieceInfo",new[]{typeof(Piece)},null,nameof(RelayPlacementInfo));
   Patch(h,typeof(InventoryGui),"DoCrafting",new[]{typeof(Player)},nameof(Craft),null,nameof(CraftIL));
   Patch(h,typeof(InventoryGui),"OnCraftPressed",Type.EmptyTypes,nameof(CraftPressed),nameof(CraftStarted));
   Patch(h,typeof(InventoryGui),"UpdateRecipe",new[]{typeof(Player),typeof(float)},nameof(RecipeSelected),nameof(RecipeUpdated));
   Patch(h,typeof(InventoryGui),"OnCraftCancelPressed",Type.EmptyTypes,null,nameof(CraftCancelled));
   Patch(h,typeof(InventoryGui),"Hide",Type.EmptyTypes,null,nameof(CraftClosed));
   Patch(h,typeof(InventoryGui),"UpdateCraftingPanel",new[]{typeof(bool)},nameof(CraftOpened));
   Patch(h,typeof(Player),"TryPlacePiece",new[]{typeof(Piece)},nameof(Build));
   Patch(h,typeof(Player),"PlacePiece",new[]{typeof(Piece),typeof(Vector3),typeof(Quaternion),typeof(bool),typeof(bool)},null,null,nameof(BuildIL));
   Patch(h,typeof(Player),"HaveRequirementItems",new[]{typeof(Recipe),typeof(bool),typeof(int),typeof(int)},nameof(HaveCraft));
   Patch(h,typeof(Player),"HaveRequirements",new[]{typeof(Piece),typeof(Player.RequirementMode)},nameof(HaveBuild));
   Patch(h,typeof(Player),"GetFirstRequiredItem",new[]{typeof(Inventory),typeof(Recipe),typeof(int),typeof(int).MakeByRefType(),typeof(int).MakeByRefType(),typeof(int)},nameof(FirstIngredient));
   Patch(h,typeof(Player),"ConsumeResources",new[]{typeof(Piece.Requirement[]),typeof(int),typeof(int),typeof(int)},nameof(Consume));
   Patch(h,typeof(InventoryGui),"SetupRequirement",new[]{typeof(Transform),typeof(Piece.Requirement),typeof(Player),typeof(bool),typeof(int),typeof(int)},null,nameof(Requirement));
   foreach(string name in new[]{"RPC_RequestOpen","RPC_RequestStack","RPC_RequestTakeAll"})Patch(h,typeof(Container),name,new[]{typeof(long),typeof(long)},nameof(Open));
   Patch(h,typeof(ZDO),"SetOwner",new[]{typeof(long)},nameof(Owner));
   // Each exact overload is obtained from its MethodInfo; no ambiguous name-only Harmony binding.
   foreach(var m in typeof(Inventory).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Where(m=>new[]{"AddItem","RemoveItem","RemoveOneItem","RemoveAll","RemoveUnequipped","MoveAll","MoveItemToThis","MoveInventoryToGrave","StackAll","Load","LoadOld"}.Contains(m.Name))){
    h.Patch(m,prefix:new HarmonyMethod(typeof(Patches),nameof(Mutate)){priority=Priority.First});Plugin.Info("Patch OK: "+m.DeclaringType.Name+"."+m);
   }
   Patch(h,typeof(WearNTear),"RPC_Remove",new[]{typeof(long),typeof(bool)},nameof(RemoveBuilding));
   Patch(h,typeof(WearNTear),"Destroy",new[]{typeof(HitData),typeof(bool)},nameof(RemoveBuilding));
   Patch(h,typeof(Piece),"DropResources",new[]{typeof(HitData)},nameof(RelayRefund));
  }
  static void Patch(Harmony h,Type type,string name,Type[] args,string prefix=null,string postfix=null,string transpiler=null){
   var target=AccessTools.DeclaredMethod(type,name,args)??throw new MissingMethodException(type.Name+"."+name);
   h.Patch(target,prefix==null?null:new HarmonyMethod(typeof(Patches),prefix){priority=Priority.First},postfix==null?null:new HarmonyMethod(typeof(Patches),postfix),transpiler==null?null:new HarmonyMethod(typeof(Patches),transpiler));
   Plugin.Info("Patch OK: "+type.Name+"."+target);
  }
  static void ContainerInfo(Container __instance,ref string __result){
   string text=ContainerHover.Text(__instance);if(text.Length>0)__result+="\n"+text;
  }
  static void RelayPlacementInfo(Hud __instance,Piece __0){
   var player=Player.m_localPlayer;
   if(!__0||!__0.GetComponent<Relay>()||!player||!player.InPlaceMode()||Hud.IsPieceSelectionVisible())return;
   var ghost=R.Get<GameObject>(player,"m_placementGhost");
   var preview=ghost&&ghost.activeInHierarchy?ghost.GetComponent<RelayPresentation>():null;
   if(preview&&preview.PlacementText!=null)__instance.m_pieceDescription.text=preview.PlacementText;
  }
  static bool Craft(InventoryGui __instance,Player player)=>Actions.Craft(__instance,player);
  static void CraftOpened(InventoryGui __instance)=>CraftOverview.Open(__instance,Player.m_localPlayer);
  static void CraftClosed(){CraftCancelled();CraftOverview.Clear();}
  static bool CraftPressed(InventoryGui __instance)=>CraftPreparation.Press(__instance);
  static void CraftStarted(InventoryGui __instance)=>CraftPreparation.Pressed(__instance);
  static void RecipeSelected(InventoryGui __instance,Player player)=>CraftPreparation.Selection(__instance,player);
  static void RecipeUpdated(InventoryGui __instance)=>CraftPreparation.Button(__instance);
  static void CraftCancelled(){CraftPreparation.Cancel();if(Actions.Waiting?.Op.Build==false)Actions.Cancel();}
  static bool Build(Player __instance,Piece piece,ref bool __result){if(Actions.Build(__instance,piece))return true;__result=false;return false;}
  static bool Consume(Player __instance)=>Actions.Active==null||Actions.Active.Player!=__instance;
  static bool HaveCraft(Player __instance,Recipe piece,bool discover,int qualityLevel,int amount,ref bool __result){if(discover)return true;if(!Actions.HaveCraft(__instance,piece,qualityLevel,amount,out bool result))return true;__result=result;return false;}
  static bool HaveBuild(Player __instance,Piece piece,Player.RequirementMode mode,ref bool __result){
   if(mode==Player.RequirementMode.IsKnown||!Actions.BuildPiece(__instance,piece))return true;var core=Actions.Context(__instance,false);if(!core)return true;
   if(Actions.Active?.Piece==piece){__result=true;return false;}
   if(piece.m_craftingStation){if(mode==Player.RequirementMode.CanAlmostBuild){if(!R.Get<Dictionary<string,int>>(__instance,"m_knownStations").ContainsKey(piece.m_craftingStation.m_name)){__result=false;return false;}}
    else if(!CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name,__instance.transform.position)&&!ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench)){__result=false;return false;}}
   if(piece.m_dlc.Length>0&&!DLCMan.instance.IsDLCInstalled(piece.m_dlc)){__result=false;return false;}
   if(ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey())){__result=true;return false;}
   var needs=Stockroom.Requirements(piece.m_resources,0,1);if(mode==Player.RequirementMode.CanAlmostBuild)foreach(var n in needs)n.Amount=1;
   __result=Planner.Plan(needs,Stockroom.Available(__instance,core,needs))!=null;return false;
  }
  static bool FirstIngredient(Player __instance,Recipe recipe,int qualityLevel,int craftMultiplier,ref int amount,ref int extraAmount,ref ItemDrop.ItemData __result){
   if(!Actions.FirstIngredient(__instance,recipe,qualityLevel,craftMultiplier,out var item,out int need,out int extra))return true;__result=item;amount=need;extraAmount=extra;return false;
  }
  static void Requirement(Transform elementRoot,Piece.Requirement req,Player player,bool craft,int quality,int craftMultiplier,bool __result){
   if(!craft&&!Actions.BuildPiece(player,player?player.GetSelectedPiece():null))return;
   if(!__result||!req.m_resItem)return;var core=Actions.Context(player,craft);if(!core)return;
   int needed=req.GetAmount(quality)*craftMultiplier;if(needed<=0)return;var needs=new List<Need>{new Need(req.m_resItem.name,needed)};
   var stock=craft?CraftPreparation.Stock(player,needs):Stockroom.Available(player,core,needs);int count=craft?stock.GroupBy(s=>s.Quality).Select(g=>g.Sum(s=>s.Amount)).DefaultIfEmpty(0).Max():stock.Sum(s=>s.Amount);
   var label=elementRoot.Find("res_amount")?.GetComponent<TMP_Text>();if(label){label.text=count+" / "+needed;label.color=count>=needed?Color.white:Color.red;}
  }
  static bool Mutate(Inventory __instance,object[] __args)=>!Transport.Locked(__instance)&&!__args.OfType<Inventory>().Any(Transport.Locked)&&!__args.OfType<ItemDrop.ItemData>().Any(Transport.LockedItem);
  static bool Open(Container __instance,long uid,MethodBase __originalMethod){
   if(!Transport.Reserved(R.View(__instance)?.GetZDO()))return true;
   string response=__originalMethod.Name.Replace("Request"," ").Contains("Open")?"RPC_OpenResponse":__originalMethod.Name.Contains("Stack")?"RPC_StackResponse":"RPC_TakeAllResponse";
   R.View(__instance).InvokeRPC(uid,response,false);return false;
  }
  static bool Owner(ZDO __instance,long uid)=>!Transport.Reserved(__instance)||__instance.GetOwner()==uid;
  static bool RemoveBuilding(WearNTear __instance)=>!Transport.Reserved(R.View(__instance)?.GetZDO());
  static bool RelayRefund(Piece __instance)=>!__instance.GetComponent<Relay>()||!R.Valid(R.View(__instance))||!R.View(__instance).GetZDO().GetBool("rsn_free_relay",false);
  static IEnumerable<CodeInstruction> CraftIL(IEnumerable<CodeInstruction> instructions){
   int added=0,removed=0;foreach(var i in instructions){
    if(i.operand is MethodInfo m&&m.DeclaringType==typeof(Inventory)){
     var p=m.GetParameters();
     if(m.Name=="AddItem"&&p.Length==10&&p[0].ParameterType==typeof(string)&&p[6].ParameterType==typeof(Vector2i)){i.opcode=OpCodes.Call;i.operand=AccessTools.Method(typeof(Patches),nameof(MakeItem));added++;}
     else if(m.Name=="RemoveItem"&&p.Length==1&&p[0].ParameterType==typeof(ItemDrop.ItemData)){i.opcode=OpCodes.Call;i.operand=AccessTools.Method(typeof(Patches),nameof(RemoveUpgrade));removed++;}
     else if(m.Name=="RemoveItem"&&p.Length==4&&p[0].ParameterType==typeof(string)){i.opcode=OpCodes.Call;i.operand=AccessTools.Method(typeof(Patches),nameof(RemoveAlternate));}
    }yield return i;
   }
   if(added!=4||removed!=1)throw new InvalidOperationException("DoCrafting IL changed: AddItem="+added+", RemoveUpgrade="+removed);
  }
  static IEnumerable<CodeInstruction> BuildIL(IEnumerable<CodeInstruction> instructions){int count=0;foreach(var i in instructions){if(i.operand is MethodInfo m&&m.DeclaringType==typeof(UnityEngine.Object)&&m.Name=="Instantiate"&&m.IsGenericMethod&&m.GetGenericArguments()[0]==typeof(GameObject)&&m.GetParameters().Length==3){var actor=new CodeInstruction(OpCodes.Ldarg_0);actor.labels.AddRange(i.labels);i.labels.Clear();yield return actor;i.opcode=OpCodes.Call;i.operand=AccessTools.Method(typeof(Patches),nameof(Spawn));count++;}yield return i;}if(count!=1)throw new InvalidOperationException("PlacePiece IL changed");}
  static GameObject Spawn(GameObject prefab,Vector3 position,Quaternion rotation,Player player){
   var go=UnityEngine.Object.Instantiate(prefab,position,rotation);if(Actions.Active?.Op.Build==true&&go)Actions.Active.Output=true;
   if(go&&go.GetComponent<Relay>()&&R.Valid(R.View(go.GetComponent<Piece>()))){
    bool free=player&&(player.NoCostCheat()||R.Get<bool>(player,"m_noPlacementCost"))||ZoneSystem.instance.GetGlobalKey(prefab.GetComponent<Piece>().FreeBuildKey());
    R.View(go.GetComponent<Piece>()).GetZDO().Set("rsn_free_relay",free);
   }
   return go;
  }
  static bool RemoveUpgrade(Inventory inventory,ItemDrop.ItemData item){if(Actions.Active?.Upgrade==item)return true;return inventory.RemoveItem(item);}
  static void RemoveAlternate(Inventory inventory,string name,int amount,int quality,bool worldLevel){if(Actions.Active!=null)return;inventory.RemoveItem(name,amount,quality,worldLevel);}
  static ItemDrop.ItemData MakeItem(Inventory inventory,string name,int stack,int quality,int variant,long crafterID,string crafterName,Vector2i position,bool cheated,bool pickedUp,bool dropIfFullInv){
   var active=Actions.Active;ItemDrop.ItemData result;
   if(active?.Upgrade!=null){result=active.Upgrade;if(!inventory.ContainsItem(result))throw new InvalidOperationException("Upgrade target missing");result.m_quality=quality;result.m_durability=result.GetMaxDurability();R.Call(inventory,"Changed",new[]{typeof(bool),typeof(bool)},true,false);}
   else result=inventory.AddItem(name,stack,quality,variant,crafterID,crafterName,position,cheated,pickedUp,dropIfFullInv);
   if(active!=null&&result!=null)active.Output=true;return result;
  }
 }
}
