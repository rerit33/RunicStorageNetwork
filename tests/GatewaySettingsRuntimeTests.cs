#if GATEWAY_SETTINGS_RUNTIME_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RunicStorageNetwork;
using BepInEx.Configuration;

namespace BepInEx.Configuration {
 public sealed class ConfigEntry<T> {public T Value;}
 public sealed class ConfigDescription {public object[] Tags;public ConfigDescription(string text,object range,params object[] tags){Tags=tags;}}
 public sealed class ConfigFile {
  public ConfigEntry<bool> Setting;public ConfigDescription Description;
  public ConfigEntry<bool> Bind(string section,string name,bool value,ConfigDescription description){Description=description;return Setting??(Setting=new ConfigEntry<bool>{Value=value});}
 }
}
namespace Jotunn.Utils {public sealed class ConfigurationManagerAttributes {public bool IsAdminOnly;}}
namespace Jotunn.Entities {public sealed class CustomRPC {public Func<long,ZPackage,IEnumerator> Receive;}}
namespace Jotunn.Managers {
 public sealed class NetworkManager {
  public static NetworkManager Instance=new NetworkManager();public Jotunn.Entities.CustomRPC Rpc;
  public Jotunn.Entities.CustomRPC AddRPC(string name,Func<long,ZPackage,IEnumerator> server,Func<long,ZPackage,IEnumerator> client){if(server!=null)throw new Exception("client must not change server rule");return Rpc=new Jotunn.Entities.CustomRPC{Receive=client};}
 }
 public sealed class SynchronizationManager {
  public static SynchronizationManager Instance=new SynchronizationManager();public Func<ZPackage> Initial;
  public void AddInitialSynchronization(Jotunn.Entities.CustomRPC rpc,Func<ZPackage> package){Initial=package;}
 }
}
public sealed class ZNetPeer {public long m_uid;public bool m_server=true;}
public sealed class ZNet {
 public enum ConnectionStatus {Connecting,Connected}
 public static ConnectionStatus Status=ConnectionStatus.Connecting;
 public static ZNet instance;public bool Server;public ZNetPeer Peer=new ZNetPeer{m_uid=42};
 public bool IsServer()=>Server;public List<ZNetPeer> GetPeers()=>Peer==null?new List<ZNetPeer>():new List<ZNetPeer>{Peer};public static ConnectionStatus GetConnectionStatus()=>Status;public static implicit operator bool(ZNet net)=>net!=null;
}
public sealed class ZPackage {
 readonly MemoryStream stream=new MemoryStream();
 public void Write(int value)=>new BinaryWriter(stream).Write(value);public void Write(bool value)=>new BinaryWriter(stream).Write(value);
 public int ReadInt()=>new BinaryReader(stream).ReadInt32();public bool ReadBool()=>new BinaryReader(stream).ReadBoolean();public void Rewind(){stream.Position=0;}
}
static class GatewaySettingsRuntimeTests {
 static int passed;
 static void Check(bool value,string why){if(!value)throw new Exception(why);}
 static ConfigFile Start(bool? value=null){ZNet.instance=new ZNet();ZNet.Status=ZNet.ConnectionStatus.Connecting;var file=new ConfigFile{Setting=value.HasValue?new ConfigEntry<bool>{Value=value.Value}:null};GatewaySettings.Bind(file);return file;}
 static bool Published(){var p=Jotunn.Managers.SynchronizationManager.Instance.Initial();p.Rewind();Check(p.ReadInt()==1,"protocol");return p.ReadBool();}
 static void Receive(bool value,long sender=42,int version=1){var p=new ZPackage();p.Write(version);p.Write(value);p.Rewind();var routine=Jotunn.Managers.NetworkManager.Instance.Rpc.Receive(sender,p);while(routine.MoveNext()){};}
 static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS gateway settings "+name);}
 static int Main(){try{
  Test("default keeps portal restrictions and is admin only",()=>{var config=Start();Check(!GatewaySettings.CarriesAllItems&&!Published()&&((Jotunn.Utils.ConfigurationManagerAttributes)config.Description.Tags.Single()).IsAdminOnly,"default or authority changed");});
  Test("live config edits do not change rule or late-join package",()=>{var config=Start(true);config.Setting.Value=false;Check(GatewaySettings.CarriesAllItems&&Published(),"live setting applied");});
  Test("restart applies saved setting",()=>{Start(true);var config=Start(false);Check(!GatewaySettings.CarriesAllItems&&!Published(),"restart ignored");});
  Test("client uses server startup rule only once",()=>{var config=Start(false);Receive(true);config.Setting.Value=false;Receive(false);Check(GatewaySettings.CarriesAllItems,"running client rule changed");});
  Test("other peers cannot change rule",()=>{Start();Receive(true,7);Check(!GatewaySettings.CarriesAllItems,"foreign peer changed rule");Receive(true);Check(GatewaySettings.CarriesAllItems,"server rule rejected");});
  Test("server ignores incoming rules",()=>{Start();ZNet.instance.Server=true;Receive(true);Check(!GatewaySettings.CarriesAllItems&&!Published(),"client changed server");});
  Test("unsupported package cannot latch partial rule",()=>{Start();bool failed=false;try{Receive(true,42,2);}catch(InvalidOperationException){failed=true;}Check(failed&&!GatewaySettings.CarriesAllItems,"unknown protocol applied");Receive(true);Check(GatewaySettings.CarriesAllItems,"valid retry blocked");});
  Test("new world clears previous server override",()=>{Start();Receive(true);GatewaySettings.Reset();Check(!GatewaySettings.CarriesAllItems,"previous server retained");Receive(false);Check(!GatewaySettings.CarriesAllItems,"new server ignored");});
  Test("joining server cannot change later local hosting rule",()=>{Start(false);Receive(true);Check(!Published(),"client override leaked into hosting");GatewaySettings.Reset();ZNet.instance.Server=true;Check(!GatewaySettings.CarriesAllItems,"hosting retained remote override");});
  Test("no connection cannot apply rule",()=>{Start();ZNet.instance=null;Receive(true);Check(!GatewaySettings.CarriesAllItems,"disconnected rule applied");});
  Test("login applies rule before server peer becomes ready",()=>{Start();ZNet.instance.Peer.m_uid=0;Receive(true);Check(GatewaySettings.CarriesAllItems,"initial sync ignored during PeerInfo buffering");});
  Test("unidentified peer cannot change an established session",()=>{Start();ZNet.instance.Peer.m_uid=0;ZNet.Status=ZNet.ConnectionStatus.Connected;Receive(true);Check(!GatewaySettings.CarriesAllItems,"unidentified live peer changed rule");});
  Console.WriteLine("Gateway settings runtime tests: "+passed+" passed (stand-ins)");return 0;
 }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
#endif
