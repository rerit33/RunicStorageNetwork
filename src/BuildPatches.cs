using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace RunicStorageNetwork {
 internal static partial class Patches {
  // Gate only the successful path. A prefix returning false would prevent the
  // game's placement/raycast checks and can skip other mods' prefixes as well.
  // This marker precedes all placement statistics and object creation in Valheim.
  static IEnumerable<CodeInstruction> BuildGateIL(IEnumerable<CodeInstruction> instructions,ILGenerator generator){
   var codes=instructions.ToList();
   var anchors=codes.Where(i=>i.opcode==OpCodes.Ldstr&&Equals(i.operand,"Placed ")).ToList();
   if(anchors.Count!=1)throw new InvalidOperationException("TryPlacePiece valid-placement branch changed");
   var anchor=anchors[0];var resume=generator.DefineLabel();
   var first=new CodeInstruction(OpCodes.Ldarg_0);
   first.labels.AddRange(anchor.labels);anchor.labels.Clear();anchor.labels.Add(resume);
   first.blocks.AddRange(anchor.blocks);anchor.blocks.Clear();
   codes.InsertRange(codes.IndexOf(anchor),new[]{
    first,new CodeInstruction(OpCodes.Ldarg_1),
    new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Patches),nameof(BuildAllowed))),
    new CodeInstruction(OpCodes.Brtrue,resume),new CodeInstruction(OpCodes.Ldc_I4_0),new CodeInstruction(OpCodes.Ret)
   });
   return codes;
  }
  static bool BuildAllowed(Player player,Piece piece)=>ContentSettings.AllowsPiece(piece)&&Actions.Build(player,piece);

  // Observe the new object's first native use, leaving Instantiate (or a wrapper
  // installed by another mod) intact in either transpiler order. The receiver is
  // still on the stack; duplicate it for observation without changing the call.
  static IEnumerable<CodeInstruction> BuildIL(IEnumerable<CodeInstruction> instructions){
   var codes=instructions.ToList();
   var anchors=codes.Where(i=>(i.opcode==OpCodes.Call||i.opcode==OpCodes.Callvirt)&&i.operand is MethodInfo m&&
    m.DeclaringType==typeof(GameObject)&&m.Name=="GetComponentInChildren"&&m.IsGenericMethod&&
    m.GetGenericArguments().Length==1&&m.GetGenericArguments()[0]==typeof(CraftingStation)&&m.GetParameters().Length==0).ToList();
   if(anchors.Count!=1)throw new InvalidOperationException("PlacePiece created-object anchor changed");
   var anchor=anchors[0];var first=new CodeInstruction(OpCodes.Dup);
   first.labels.AddRange(anchor.labels);anchor.labels.Clear();
   first.blocks.AddRange(anchor.blocks);anchor.blocks.Clear();
   codes.InsertRange(codes.IndexOf(anchor),new[]{first,new CodeInstruction(OpCodes.Ldarg_0),new CodeInstruction(OpCodes.Ldarg_1),
    new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Patches),nameof(BuildCreated)))});
   return codes;
  }
  static void BuildCreated(GameObject created,Player player,Piece piece){
   if(!created)return;
   var active=Actions.Active;
   // Record output before later placement callbacks can throw, preventing a
   // refund for a building which already exists. Ignore unrelated nested builds.
   if(active?.Op.Build==true&&active.Player==player&&active.Piece==piece)active.Output=true;
   if(created.GetComponent<Relay>()&&R.Valid(R.View(created.GetComponent<Piece>()))){
    bool free=player&&(player.NoCostCheat()||R.Get<bool>(player,"m_noPlacementCost"))||ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey());
    R.View(created.GetComponent<Piece>()).GetZDO().Set("rsn_free_relay",free);
   }
  }
 }
}
