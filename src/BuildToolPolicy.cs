using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 // Which build pieces the network may supply. Nothing is identified by the vanilla hammer:
 // every item carrying a PieceTable is a build tool, so tools added by other mods take part
 // on the same terms, and the configured lists only ever narrow that set.
 //
 // Judged once per item database and per configuration change; placement checks are a
 // dictionary lookup.
 internal static class BuildToolPolicy {
  static readonly Dictionary<string,string> verdicts=new Dictionary<string,string>(StringComparer.Ordinal);
  static readonly HashSet<PieceTable> allowedTables=new HashSet<PieceTable>();
  static BuildToolRules rules;static ObjectDB catalogued;
  internal static int Tools {get;private set;}
  internal static int Supported {get;private set;}
  internal static readonly List<string> ExcludedTools=new List<string>();

  // Plugin's SettingChanged handlers invalidate for local edits and synced settings.
  internal static void Invalidate(){catalogued=null;rules=null;}

  static string Text(BepInEx.Configuration.ConfigEntry<string> entry)=>entry==null?"":entry.Value??"";
  internal static BuildToolRules Rules {
   get {
    if(rules==null)rules=new BuildToolRules(Text(Plugin.AllowedBuildTools),Text(Plugin.DeniedBuildTools),Text(Plugin.DeniedPieceComponents));
    return rules;
   }
  }

  static bool Ready(){
   if(!ObjectDB.instance||ObjectDB.instance.m_items==null)return false;
   if(catalogued!=ObjectDB.instance)Catalog();
   return true;
  }
  // Reason why this piece cannot be built from network resources, or null when it can.
  internal static string Reason(GameObject piecePrefab){
   if(!piecePrefab)return "invalid hammer piece";
   if(!Ready())return "supply unavailable";
   return verdicts.TryGetValue(piecePrefab.name,out string reason)?reason:BuildToolRules.ExcludedToolReason;
  }
  internal static bool Eligible(GameObject piecePrefab)=>Reason(piecePrefab)==null;
  // The equipped tool's own table, rather than the vanilla hammer's.
  internal static bool Table(PieceTable table)=>table&&Ready()&&allowedTables.Contains(table);
  internal static void Ensure(){Ready();}

  static void Catalog(){
   catalogued=ObjectDB.instance;rules=null;
   verdicts.Clear();allowedTables.Clear();ExcludedTools.Clear();
   var byPiece=new Dictionary<GameObject,List<string>>();int tools=0;
   foreach(var prefab in catalogued.m_items){
    var drop=prefab?prefab.GetComponent<ItemDrop>():null;
    var table=drop&&drop.m_itemData!=null&&drop.m_itemData.m_shared!=null?drop.m_itemData.m_shared.m_buildPieces:null;
    if(!table||table.m_pieces==null)continue;
    tools++;
    if(Rules.Tool(prefab.name))allowedTables.Add(table);else ExcludedTools.Add(prefab.name);
    foreach(var piece in table.m_pieces){
     if(!piece)continue;
     if(!byPiece.TryGetValue(piece,out var placers))byPiece[piece]=placers=new List<string>();
     placers.Add(prefab.name);
    }
   }
   foreach(var pair in byPiece){
    string reason=Rules.Verdict(pair.Value,R.Components(pair.Key));
    // A piece can sit in several tables; the first allowed placer settles it.
    if(!verdicts.TryGetValue(pair.Key.name,out string previous)||previous!=null)verdicts[pair.Key.name]=reason;
   }
   Tools=tools;Supported=verdicts.Count(p=>p.Value==null);ExcludedTools.Sort(StringComparer.Ordinal);
   Plugin.Info("Build tool policy: "+allowedTables.Count+" of "+tools+" build tools supported, "+Supported+" of "+verdicts.Count+" pieces; "+Rules.Summary);
   if(ExcludedTools.Count>0)Plugin.Info("Build tools excluded: "+string.Join(", ",ExcludedTools.ToArray()));
  }
 }
}
