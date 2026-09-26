using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace RunicStorageNetwork {
 [BepInPlugin(Guid, "Runic Storage Network", "0.5.5")]
 [BepInDependency("com.jotunn.jotunn", "2.30.2")]
 [BepInDependency("com.maxsch.valheim.MultiUserChest",BepInDependency.DependencyFlags.SoftDependency)]
 [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod,VersionStrictness.Patch)]
 public sealed class Plugin:BaseUnityPlugin {
  public const string Guid="local.runicstoragenetwork";
  internal static ManualLogSource Log;
  internal static ConfigEntry<bool> Supply,DebugLogging;
  internal static ConfigEntry<float> StorageRadius,SupplyRadius,Rescan,RelayLink,RelayStorage,RelaySupply;
  internal static ConfigEntry<string> AllowedContainers,DeniedContainers,DeniedComponents;
  internal static ConfigEntry<string> AllowedBuildTools,DeniedBuildTools,DeniedPieceComponents;
  internal static bool Healthy=true;
  Harmony harmony; AssetBundle bundle; GameObject corePrefab,relayPrefab;
  internal static bool Enabled=>Healthy&&Supply.Value;
  internal new static void Info(string text)=>Log.LogInfo("[RSN] "+text);
  internal static void Debug(string text){if(DebugLogging.Value)Info(text);}
  internal static void Error(string id,Exception e){Critical(id,e.ToString());}
  static readonly System.Collections.Generic.HashSet<string> critical=new System.Collections.Generic.HashSet<string>();
  internal static void ClearCritical()=>critical.Clear();
  internal static void Critical(string id,string text){
   if(!critical.Add(id))return;
   string message="[RSN] "+id+" "+text;Log.LogError(message);
   if(global::Console.instance)global::Console.instance.AddString(message);
  }
  internal static void Disable(string reason){if(!Healthy)return;Healthy=false;Log.LogWarning("[RSN] Supply disabled: "+reason);}
  void Awake(){
   Log=Logger;
   Supply=Config.Bind("Network","SupplyEnabled",true,new ConfigDescription("Enable supply; registered building remains available.",null,new ConfigurationManagerAttributes{IsAdminOnly=true}));
   StorageRadius=Number("StorageRadius",20,1,100);SupplyRadius=Number("SupplyRadius",20,1,100);Rescan=Number("RescanIntervalSeconds",2,0.5f,30);
   RelayLink=Number("RelayLinkRange",50,1,100);RelayStorage=Number("RelayStorageRadius",20,1,100);RelaySupply=Number("RelaySupplyRadius",20,1,100);
   RelayLink.SettingChanged+=SettingsChanged;RelayStorage.SettingChanged+=SettingsChanged;RelaySupply.SettingChanged+=SettingsChanged;
   AllowedContainers=Names("AllowedContainers","","Prefab names, comma separated. Empty: every player-built container qualifies, including containers added by other mods. When filled, only the listed prefabs are connected.");
   DeniedContainers=Names("DeniedContainers",Logic.ContainerRules.DeniedPrefabDefault,"Prefab names, comma separated, that are never connected. Exclusion wins over AllowedContainers.");
   DeniedComponents=Names("DeniedComponents",Logic.ContainerRules.DeniedComponentDefault,"Component names, comma separated. A container whose prefab has any of these is never connected. Keeps machines that consume or fire their contents out of the network, including modded ones.");
   foreach(var entry in new[]{AllowedContainers,DeniedContainers,DeniedComponents})entry.SettingChanged+=ContainersChanged;
   AllowedBuildTools=Tools("AllowedBuildTools","","Item prefab names, comma separated. Empty: every build tool qualifies, including tools added by other mods. When filled, only the listed tools build from the network.");
   DeniedBuildTools=Tools("DeniedBuildTools",Logic.BuildToolRules.DeniedToolDefault,"Item prefab names, comma separated, that never build from the network. Exclusion wins over AllowedBuildTools.");
   DeniedPieceComponents=Tools("DeniedPieceComponents",Logic.BuildToolRules.DeniedPieceComponentDefault,"Component names, comma separated. A piece whose prefab has any of these is never supplied. Keeps terrain shaping out of the network, including modded terrain tools.");
   foreach(var entry in new[]{AllowedBuildTools,DeniedBuildTools,DeniedPieceComponents})entry.SettingChanged+=BuildToolsChanged;
   DebugLogging=Config.Bind("Diagnostics","DebugLogging",false,"Detailed transaction diagnostics without inventory dumps.");
   Supply.SettingChanged+=SettingsChanged;StorageRadius.SettingChanged+=SettingsChanged;SupplyRadius.SettingChanged+=SettingsChanged;Rescan.SettingChanged+=SettingsChanged;
   Info("0.5.5; Valheim="+global::Version.CurrentVersion+" Unity="+Application.unityVersion+" BepInEx="+typeof(BaseUnityPlugin).Assembly.GetName().Version+" Jotunn="+typeof(PieceManager).Assembly.GetName().Version);
   RsnLocalization.Add();
   try {
    string path=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"Assets","rsn_core_windows");
    Info("Loading bundle: "+path);bundle=AssetBundle.LoadFromFile(path);if(!bundle)throw new InvalidOperationException("AssetBundle load failed");
    var prefab=bundle.LoadAsset<GameObject>("assets/runicstoragegame/rsn_networkcore.prefab");if(!prefab)throw new InvalidOperationException("RSN_NetworkCore asset missing");
    corePrefab=prefab;
    prefab.SetActive(false);
    foreach(var t in prefab.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("piece");
    var nv=prefab.AddComponent<ZNetView>();nv.m_persistent=true;nv.m_type=ZDO.ObjectType.Default;
    var p=prefab.AddComponent<Piece>();p.m_name="$rsn_name";p.m_description="$rsn_description";p.m_canBeRemoved=true;p.m_usage=Piece.UsageTagFlags.Storage|Piece.UsageTagFlags.Crafting;
    p.m_icon=bundle.LoadAsset<Sprite>("assets/runicstoragegame/rsn_coreicon.png");
    var wear=prefab.AddComponent<WearNTear>();wear.m_health=1000;wear.m_materialType=WearNTear.MaterialType.Stone;wear.m_noRoofWear=true;wear.m_noSupportWear=false;wear.m_burnable=false;
    prefab.AddComponent<Core>();
    prefab.AddComponent<NetworkMember>();
    prefab.AddComponent<NetworkName>();
    var cfg=new PieceConfig{Name="$rsn_name",Description="$rsn_description",PieceTable="Hammer",CraftingStation="piece_workbench",Category="Crafting",Usage=new[]{"Storage","Crafting"},Requirements=new[]{new RequirementConfig("Stone",30,0,true),new RequirementConfig("FineWood",20,0,true),new RequirementConfig("Chain",2,0,true),new RequirementConfig("SurtlingCore",4,0,true),new RequirementConfig("GreydwarfEye",10,0,true)}};
    if(!PieceManager.Instance.AddPiece(new CustomPiece(prefab,false,cfg)))throw new InvalidOperationException("Jotunn rejected core");
    // Jotunn keeps the registered template under its inactive prefab container.
    prefab.SetActive(true);Info("Bundle loaded; RSN_NetworkCore registered with Hammer");
    RegisterRelay();
    PrefabManager.OnVanillaPrefabsAvailable+=CheckIds;
    harmony=new Harmony(Guid);Patches.Install(harmony);
    gameObject.AddComponent<Transport>();
    gameObject.AddComponent<NetworkSystem>();
    new Terminal.ConsoleCommand("rsn_status",RsnLocalization.Text("diag_help"),args=>Topology.Diagnose(args.Context));
    new Terminal.ConsoleCommand("rsn",RsnLocalization.Text("diag_help"),args=>Topology.Diagnose(args.Context));
   }catch(Exception e){Disable("Initialization failed");Error("startup",e);}
   Info("Supply="+Enabled+" storage="+StorageRadius.Value+" supply="+SupplyRadius.Value+" interval="+Rescan.Value);
  }
  void Start(){try{if(harmony!=null)Integrations.Install(harmony);}catch(Exception e){Disable("Integration API mismatch");Error("integrations",e);}}
  void SettingsChanged(object sender,EventArgs e){Info("Applied configuration: supply="+Enabled+" storage="+StorageRadius.Value+" supplyRadius="+SupplyRadius.Value+" relayLink="+RelayLink.Value+" rescan="+Rescan.Value);Topology.Dirty();}
  // The coordinator validates every source against these lists, so the server copy decides.
  void ContainersChanged(object sender,EventArgs e){ContainerPolicy.Invalidate();Stockroom.ClearObservations();foreach(var core in Core.Live)if(core)core.Invalidate();Topology.Dirty();}
  // The coordinator validates every placement against these lists, so the server copy decides.
  void BuildToolsChanged(object sender,EventArgs e){BuildToolPolicy.Invalidate();Topology.Dirty();}
  void RegisterRelay(){
   relayPrefab=bundle.LoadAsset<GameObject>("assets/runicstoragegame/rsn_runicrelay.prefab");if(!relayPrefab)throw new InvalidOperationException("Runic relay asset missing");
   relayPrefab.SetActive(false);foreach(var t in relayPrefab.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("piece");
   var nv=relayPrefab.AddComponent<ZNetView>();nv.m_persistent=true;nv.m_type=ZDO.ObjectType.Default;
   var p=relayPrefab.AddComponent<Piece>();p.m_name="$rsn_relay_name";p.m_description="$rsn_relay_description";p.m_canBeRemoved=true;p.m_usage=Piece.UsageTagFlags.Storage|Piece.UsageTagFlags.Crafting;
   p.m_icon=bundle.LoadAsset<Sprite>("assets/runicstoragegame/rsn_relayicon.png");
   var wear=relayPrefab.AddComponent<WearNTear>();wear.m_health=600;wear.m_materialType=WearNTear.MaterialType.Stone;wear.m_noRoofWear=true;wear.m_noSupportWear=false;wear.m_burnable=false;
   relayPrefab.AddComponent<Relay>();relayPrefab.AddComponent<RelayPresentation>();
   var cfg=new PieceConfig{Name="$rsn_relay_name",Description="$rsn_relay_description",PieceTable="Hammer",CraftingStation="piece_workbench",Category="Crafting",Usage=new[]{"Storage","Crafting"},Requirements=new[]{new RequirementConfig("Stone",10,0,true),new RequirementConfig("FineWood",6,0,true),new RequirementConfig("Iron",2,0,true),new RequirementConfig("SurtlingCore",1,0,true),new RequirementConfig("GreydwarfEye",5,0,true)}};
   if(!PieceManager.Instance.AddPiece(new CustomPiece(relayPrefab,false,cfg)))throw new InvalidOperationException("Jotunn rejected relay");relayPrefab.SetActive(true);Info("RSN_RunicRelay registered with Hammer");
  }
  ConfigEntry<float> Number(string name,float value,float min,float max)=>Config.Bind("Network",name,value,new ConfigDescription(name,new AcceptableValueRange<float>(min,max),new ConfigurationManagerAttributes{IsAdminOnly=true}));
  ConfigEntry<string> Names(string name,string value,string description)=>Config.Bind("Containers",name,value,new ConfigDescription(description,null,new ConfigurationManagerAttributes{IsAdminOnly=true}));
  ConfigEntry<string> Tools(string name,string value,string description)=>Config.Bind("Building",name,value,new ConfigDescription(description,null,new ConfigurationManagerAttributes{IsAdminOnly=true}));
  void CheckIds(){
   foreach(string id in new[]{"Stone","FineWood","Chain","Iron","SurtlingCore","GreydwarfEye","piece_workbench","Hammer"})if(!PrefabManager.Instance.GetPrefab(id)){Disable("Missing prefab "+id);Log.LogError("[RSN] Required prefab ID unresolved: "+id);}
   try{CoreMaterials.Apply(corePrefab);}catch(Exception e){Error("Native core materials failed; bundle materials retained",e);}
   try{CoreMaterials.Apply(relayPrefab,true);}catch(Exception e){Error("Native relay materials failed; bundle materials retained",e);}
   // Vanilla references become available here, before a world/placement ghost is created.
   // The registered prefab keeps its identity, so previously saved cores inherit this too.
   try {
    var donor=PrefabManager.Instance.GetPrefab("stone_floor");
    if(!donor||!corePrefab)throw new InvalidOperationException("stone_floor/core prefab unavailable");
    var piece=donor.GetComponent<Piece>();var wear=donor.GetComponent<WearNTear>();
    if(!piece||!wear)throw new InvalidOperationException("stone_floor Piece/WearNTear missing");
    var place=CopyEffects(piece.m_placeEffect);var destroyed=CopyEffects(wear.m_destroyedEffect);var hit=CopyEffects(wear.m_hitEffect);
    if(!place.HasEffects()||!destroyed.HasEffects())throw new InvalidOperationException("stone_floor build/destruction effects empty");
    corePrefab.GetComponent<Piece>().m_placeEffect=place;
    var target=corePrefab.GetComponent<WearNTear>();target.m_destroyedEffect=destroyed;target.m_hitEffect=hit;
    relayPrefab.GetComponent<Piece>().m_placeEffect=CopyEffects(piece.m_placeEffect);
    var relayWear=relayPrefab.GetComponent<WearNTear>();relayWear.m_destroyedEffect=CopyEffects(wear.m_destroyedEffect);relayWear.m_hitEffect=CopyEffects(wear.m_hitEffect);
    Info("Relay effects from stone_floor: place="+EffectNames(place)+"; destroy="+EffectNames(destroyed));
    Info("Core effects from stone_floor: place="+EffectNames(place)+"; destroy="+EffectNames(destroyed)+"; hit="+EffectNames(hit));
    Info("Game color space="+QualitySettings.activeColorSpace+"; see Core material entries for native material bindings");
   }catch(Exception e){Error("Core presentation setup failed",e);}
  }
  static EffectList CopyEffects(EffectList source){
   if(source==null||source.m_effectPrefabs==null)throw new InvalidOperationException("Missing vanilla EffectList");
   var result=new EffectList{m_effectPrefabs=new EffectList.EffectData[source.m_effectPrefabs.Length]};
   for(int i=0;i<result.m_effectPrefabs.Length;i++){
    var e=source.m_effectPrefabs[i];if(e==null||!e.m_prefab)throw new InvalidOperationException("Unresolved vanilla effect");
    result.m_effectPrefabs[i]=new EffectList.EffectData{m_prefab=e.m_prefab,m_enabled=e.m_enabled,m_variant=e.m_variant,m_attach=e.m_attach,m_follow=e.m_follow,m_inheritParentRotation=e.m_inheritParentRotation,m_inheritParentScale=e.m_inheritParentScale,m_multiplyParentVisualScale=e.m_multiplyParentVisualScale,m_randomRotation=e.m_randomRotation,m_scale=e.m_scale,m_childTransform=e.m_childTransform};
   }
   return result;
  }
  static string EffectNames(EffectList effects)=>string.Join(",",Array.ConvertAll(effects.m_effectPrefabs,e=>e.m_prefab.name));
  void OnDestroy(){PrefabManager.OnVanillaPrefabsAvailable-=CheckIds;Supply.SettingChanged-=SettingsChanged;StorageRadius.SettingChanged-=SettingsChanged;SupplyRadius.SettingChanged-=SettingsChanged;Rescan.SettingChanged-=SettingsChanged;RelayLink.SettingChanged-=SettingsChanged;RelayStorage.SettingChanged-=SettingsChanged;RelaySupply.SettingChanged-=SettingsChanged;foreach(var entry in new[]{AllowedContainers,DeniedContainers,DeniedComponents})if(entry!=null)entry.SettingChanged-=ContainersChanged;foreach(var entry in new[]{AllowedBuildTools,DeniedBuildTools,DeniedPieceComponents})if(entry!=null)entry.SettingChanged-=BuildToolsChanged;harmony?.UnpatchSelf();}
 }
}
