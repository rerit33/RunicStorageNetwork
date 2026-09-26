using System;
using System.Collections.Generic;
using System.Linq;

namespace RunicStorageNetwork.Logic {
 // Which build tools draw from the network. Pure decision logic with no Unity or game
 // types, so the isolated tests exercise exactly the rules the client and the
 // coordinator apply. Shares list parsing with ContainerRules.
 public sealed class BuildToolRules {
  // The hoe and cultivator reach the same placement code as the hammer. Their pieces
  // reshape ground rather than build, and were never supplied from the network.
  public const string DeniedToolDefault="Hoe,Cultivator";
  // Component names, not prefab names, so modded terrain tools are covered by the same
  // rule. A name no installed assembly uses simply never matches.
  public const string DeniedPieceComponentDefault="TerrainOp,TerrainModifier";
  public const string ExcludedToolReason="excluded build tool";
  public const string ExcludedPieceReason="excluded build piece";
  readonly HashSet<string> allowed,denied,components;
  public BuildToolRules(string allow,string deny,string denyComponents){
   allowed=ContainerRules.Parse(allow);denied=ContainerRules.Parse(deny);components=ContainerRules.Parse(denyComponents);
  }
  public bool Restricted=>allowed.Count>0;
  public int DeniedComponentCount=>components.Count;

  // Exclusion wins over inclusion, as it does for containers: naming a tool in both
  // lists leaves it excluded rather than re-enabling it.
  public bool Tool(string prefab){
   if(string.IsNullOrEmpty(prefab)||denied.Contains(prefab))return false;
   return allowed.Count==0||allowed.Contains(prefab);
  }
  public bool Piece(IEnumerable<string> componentNames){
   if(components.Count==0||componentNames==null)return true;
   return !componentNames.Any(n=>n!=null&&components.Contains(n));
  }
  // A piece qualifies when it survives the component rule and at least one tool that can
  // place it is allowed. Callers pass every tool whose table contains the piece.
  public string Verdict(IEnumerable<string> tools,IEnumerable<string> pieceComponents){
   if(!Piece(pieceComponents))return ExcludedPieceReason;
   return tools!=null&&tools.Any(Tool)?null:ExcludedToolReason;
  }
  public string Summary=>"allowTools="+Describe(allowed,"every build tool")+" denyTools="+Describe(denied,"none")+" denyPieceComponents="+Describe(components,"none");
  static string Describe(HashSet<string> set,string empty)=>set.Count==0?empty:string.Join(",",set.OrderBy(s=>s,StringComparer.OrdinalIgnoreCase).ToArray());
 }
}
