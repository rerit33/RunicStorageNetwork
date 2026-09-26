using System;
using UnityEngine;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 // One recipe lookup for every side of an operation. Recipes are identified on the wire by
 // the name of their asset, so the client, the coordinator and the chest owner must resolve
 // that name identically: same enabled filter, same winner when a name is not unique.
 //
 // Built once per item database, which also keeps the per-operation lookups off a linear
 // scan of every recipe the installed mods contribute.
 internal static class RecipeIndex {
  static NameIndex<Recipe> index=new NameIndex<Recipe>();
  static ObjectDB catalogued;
  internal static void Invalidate(){catalogued=null;}

  static bool Ready(){
   if(!ObjectDB.instance||ObjectDB.instance.m_recipes==null)return false;
   if(catalogued!=ObjectDB.instance)Catalog();
   return true;
  }
  internal static Recipe Find(string name){
   if(!Ready())return null;
   var recipe=index.Find(name);
   if(!recipe)Plugin.Debug("recipe not resolved from the item database: "+(string.IsNullOrEmpty(name)?"<unnamed>":name));
   return recipe;
  }
  // Reported so a crafting refusal can name the cause instead of only its symptom.
  internal static bool Ambiguous(string name)=>Ready()&&index.Ambiguous(name);
  internal static void Ensure(){Ready();}

  static void Catalog(){
   catalogued=ObjectDB.instance;index=new NameIndex<Recipe>();
   foreach(var recipe in catalogued.m_recipes)if(recipe&&recipe.m_enabled)index.Add(recipe.name,recipe);
   Plugin.Info("Recipe index: "+index.Report);
   if(index.Unnamed>0||index.DuplicateCount>0)Plugin.Info("Recipes without a unique asset name cannot be requested from the network; the first of each ambiguous name is used.");
  }
 }
}
