using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RunicStorageNetwork {
 internal static class R {
  static readonly Dictionary<string,FieldInfo> Fields=new Dictionary<string,FieldInfo>();
  static readonly Dictionary<string,MethodInfo> Methods=new Dictionary<string,MethodInfo>();
  internal static T Get<T>(object obj,string name){var type=obj as Type??obj.GetType();string key=type.FullName+":"+name;if(!Fields.TryGetValue(key,out var f))Fields[key]=f=AccessTools.Field(type,name)??throw new MissingFieldException(key);return (T)f.GetValue(obj is Type?null:obj);}
  internal static void Set(object obj,string name,object value){var type=obj.GetType();var f=AccessTools.Field(type,name)??throw new MissingFieldException(name);f.SetValue(obj,value);}
  internal static object Call(object obj,string name,Type[] types,params object[] args){var type=obj as Type??obj.GetType();string key=type.FullName+name+string.Join(",",Array.ConvertAll(types,t=>t.FullName));if(!Methods.TryGetValue(key,out var m))Methods[key]=m=AccessTools.Method(type,name,types)??throw new MissingMethodException(key);return m.Invoke(obj is Type?null:obj,args);}
  internal static void Call(object obj,string name){Call(obj,name,Type.EmptyTypes);}
  internal static string Id(GameObject go)=>Utils.GetPrefabName(go);
  internal static ZNetView View(Component c)=>c?c.GetComponent<ZNetView>():null;
  internal static bool Valid(ZNetView v)=>v&&v.IsValid()&&v.GetZDO()!=null;
  internal static string Key(ZDOID id)=>id.UserID.ToString("X16")+":"+id.ID.ToString("X8");
  // Every component type name on a prefab, base types included, so a configured rule
  // matches a mod that subclasses the component it names.
  internal static IEnumerable<string> Components(GameObject prefab){
   var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var component in prefab.GetComponentsInChildren(typeof(Component),true)){
    if(!component)continue;
    for(var type=component.GetType();type!=null&&type!=typeof(Component)&&type!=typeof(Behaviour)&&type!=typeof(MonoBehaviour);type=type.BaseType)names.Add(type.Name);
   }
   return names;
  }
 }
}
