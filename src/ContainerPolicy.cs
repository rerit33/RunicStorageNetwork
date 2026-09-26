using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 // Which prefabs may act as network storage. Nothing is identified by name: a prefab
 // qualifies when it carries the components the transfer protocol needs, and the
 // configured lists only ever narrow that set.
 //
 // Each prefab is judged once per world and per configuration change, so the per-frame
 // scan in Topology and the per-container checks in Access only do a dictionary lookup.
 internal static class ContainerPolicy {
  static readonly Dictionary<string,string> verdicts=new Dictionary<string,string>(StringComparer.Ordinal);
  static ContainerRules rules;static ZNetScene scene;
  internal static int Supported {get;private set;}
  internal static readonly List<string> Excluded=new List<string>();

  internal static void Invalidate(){scene=null;rules=null;}

  // Plugin's SettingChanged handlers invalidate the cache for local edits and synced settings.
  // The per-container path only needs to check whether the world changed.
  static string Text(BepInEx.Configuration.ConfigEntry<string> entry)=>entry==null?"":entry.Value??"";

  internal static ContainerRules Rules {
   get {
    if(rules==null)rules=new ContainerRules(Text(Plugin.AllowedContainers),Text(Plugin.DeniedContainers),Text(Plugin.DeniedComponents));
    return rules;
   }
  }

  // Reason why this prefab cannot be network storage, or null when it can.
  internal static string Reason(string prefab){
   if(!ZNetScene.instance)return "unloaded";
   if(scene!=ZNetScene.instance)Catalog();
   if(string.IsNullOrEmpty(prefab))return "unsupported prefab";
   return verdicts.TryGetValue(prefab,out string reason)?reason:"unsupported prefab";
  }
  internal static bool Eligible(string prefab)=>Reason(prefab)==null;
  // Builds the catalog if nothing has asked for a verdict yet, so diagnostics report real counts.
  internal static void Ensure(){Reason("");}

  static void Catalog(){
   scene=ZNetScene.instance;rules=null;
   verdicts.Clear();Excluded.Clear();int containers=0;
   foreach(var prefab in scene.m_prefabs){
    if(!prefab||verdicts.ContainsKey(prefab.name))continue;
    string reason=Evaluate(prefab);
    if(reason=="unsupported prefab")continue; // Not a container at all; nothing to report.
    containers++;verdicts[prefab.name]=reason;
    if(reason!=null)Excluded.Add(prefab.name);
   }
   Supported=containers-Excluded.Count;Excluded.Sort(StringComparer.Ordinal);
   Plugin.Info("Container policy: "+Supported+" of "+containers+" container prefabs supported; "+Rules.Summary);
   if(Excluded.Count>0)Plugin.Info("Containers excluded: "+string.Join(", ",Excluded.ToArray()));
   Plugin.Debug("Containers supported: "+string.Join(", ",verdicts.Where(p=>p.Value==null).Select(p=>p.Key).OrderBy(n=>n,StringComparer.Ordinal).ToArray()));
  }

  // A container is addressed by its own ZDO throughout the reservation protocol, so it must
  // carry Container, Piece and ZNetView on one object. Storage mounted on a parent (ship holds,
  // carts) reports through m_rootObjectOverride and stays out, as it always has.
  static string Evaluate(GameObject prefab){
   var c=prefab.GetComponent<global::Container>();
   if(!c||!prefab.GetComponent<Piece>()||!prefab.GetComponent<ZNetView>())return "unsupported prefab";
   if(c.m_privacy!=global::Container.PrivacySetting.Public||c.m_wagon||c.m_rootObjectOverride||prefab.GetComponent<Ship>()||prefab.GetComponent<Rigidbody>())return "moving/private";
   return Rules.Verdict(prefab.name,R.Components(prefab));
  }

 }
}
