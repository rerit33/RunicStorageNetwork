#if OFFLINE_INVENTORY_RUNTIME_TESTS
#pragma warning disable 0649
using System;
using System.Collections.Generic;
using System.Linq;
using RunicStorageNetwork;
using RunicStorageNetwork.Logic;

class GameObject {
 internal string name;internal ItemDrop Drop;internal Container Container;
 public T GetComponent<T>() where T:class=>(Drop as T)??(Container as T);
 public static implicit operator bool(GameObject o)=>o!=null;
}
struct Vector2i {public int x,y;public Vector2i(int x,int y){this.x=x;this.y=y;}}
class ItemDrop {
 public static implicit operator bool(ItemDrop o)=>o!=null;
 public ItemData m_itemData=new ItemData();
 public class SharedData {public string m_name="Wood";public int m_maxStackSize=50;}
 public class ItemData {
  public GameObject m_dropPrefab;public SharedData m_shared=new SharedData();
  public int m_stack,m_quality,m_variant,m_worldLevel;public float m_durability;public Vector2i m_gridPos;
  public bool m_equipped,m_pickedUp,m_cheated;public long m_crafterID;public string m_crafterName;
  public Dictionary<string,string> m_customData=new Dictionary<string,string>();
 }
}
class ObjectDB {
 public static ObjectDB instance=new ObjectDB();public Dictionary<int,GameObject> Items=new Dictionary<int,GameObject>();
 public static implicit operator bool(ObjectDB o)=>o!=null;
 public GameObject GetItemPrefab(int hash)=>Items.TryGetValue(hash,out var prefab)?prefab:null;
}
static class Strings {public static int GetStableHashCode(this string s)=>InventoryRoundTripTests.Hash(s);}
class ZPackage {internal byte[] Bytes;public ZPackage(){}public ZPackage(byte[] bytes){Bytes=bytes;}public byte[] GetArray()=>Bytes;}
class Inventory {
 readonly List<ItemDrop.ItemData> items=new List<ItemDrop.ItemData>();
 internal static int LoadCalls,AddCalls,ChangeCalls;
 public Inventory(string name,object background,int w,int h){}
 public List<ItemDrop.ItemData> GetAllItems()=>items;
 public void Load(ZPackage p){LoadCalls++;foreach(var item in items)item.m_customData["EpicLoot:Migrated"]="new data";throw new InvalidOperationException("Native Load must not run during offline reads");}
 public void AddItem(ItemDrop.ItemData i){AddCalls++;throw new InvalidOperationException("Native AddItem must not run during offline reads");}
 public void Changed(){ChangeCalls++;}
 public void Save(ZPackage p){p.Bytes=InventoryRoundTripTests.Encode(110,items.Select(i=>new InventoryRoundTripTests.Record{
  Prefab=i.m_dropPrefab.name,Stack=i.m_stack,Quality=i.m_quality,Variant=i.m_variant,World=i.m_worldLevel,X=i.m_gridPos.x,Y=i.m_gridPos.y,
  Durability=(int)(float)(i.m_durability*100f),Equipped=i.m_equipped,PickedUp=i.m_pickedUp,Cheated=i.m_cheated,
  Crafter=i.m_crafterID,Name=i.m_crafterName,Data=i.m_customData
 }).ToArray());}
}
class Container {public string m_name="Chest";public object m_bkg;public int m_width=4,m_height=2;public static implicit operator bool(Container c)=>c!=null;}
class ZDO {internal GameObject Prefab=new GameObject{Container=new Container()};}
namespace RunicStorageNetwork {
 static partial class ApiWorld {}
 static class ApiRules {internal const int MaxInventoryBytes=1024*1024;}
 static class RemoteContext {internal static GameObject Prefab(ZDO z)=>z.Prefab;}
}
static class OfflineInventoryRuntimeTests {
 static int passed;
 static void Check(bool value,string why){if(!value)throw new Exception(why);}
 static Inventory New()=>new Inventory("remote",null,4,2);
 static void Test(string name,Action body){ObjectDB.instance=new ObjectDB();foreach(var id in new[]{"Wood","Iron"})ObjectDB.instance.Items[id.GetStableHashCode()]=new GameObject{name=id,Drop=new ItemDrop()};Inventory.LoadCalls=Inventory.AddCalls=Inventory.ChangeCalls=0;body();passed++;Console.WriteLine("PASS offline inventory "+name);}
 static bool Refuses(Action action){try{action();return false;}catch(Exception e) when(e is InvalidOperationException||e is System.IO.InvalidDataException){return true;}}
 public static int Main(){try{
  Test("remote reads preserve custom data without invoking migration hooks",()=>{var saved=new InventoryRoundTripTests.Record();var bytes=InventoryRoundTripTests.Encode(109,saved);var inv=New();OfflineInventory.Load(inv,bytes);var item=inv.GetAllItems().Single();Check(item.m_customData.Count==saved.Data.Count&&saved.Data.All(p=>item.m_customData[p.Key]==p.Value),"custom data changed");Check(Inventory.LoadCalls==0&&Inventory.AddCalls==0&&Inventory.ChangeCalls==0,"live inventory lifecycle ran");var p=new ZPackage();inv.Save(p);Check(InventoryRoundTrip.Preserved(bytes,p.GetArray(),Strings.GetStableHashCode,out _),"round trip failed");});
  Test("ordinary items do not receive generated metadata",()=>{var saved=new InventoryRoundTripTests.Record{Data=new Dictionary<string,string>()};var inv=New();OfflineInventory.Load(inv,InventoryRoundTripTests.Encode(109,saved));Check(inv.GetAllItems().Single().m_customData.Count==0,"metadata initialized during read");});
  Test("shared prefab definition is attached without cloning its metadata",()=>{var prefab=ObjectDB.instance.GetItemPrefab("Wood".GetStableHashCode());prefab.Drop.m_itemData.m_customData["prefab-only"]="default";var inv=New();OfflineInventory.Load(inv,InventoryRoundTripTests.Encode(107,new InventoryRoundTripTests.Record()));var item=inv.GetAllItems().Single();Check(item.m_dropPrefab==prefab&&ReferenceEquals(item.m_shared,prefab.Drop.m_itemData.m_shared)&&!item.m_customData.ContainsKey("prefab-only"),"prefab data leaked into stored item");item.m_customData["local"]="changed";Check(prefab.Drop.m_itemData.m_customData.Count==1,"prefab mutated");});
  Test("missing item fails atomically without dropping the existing inventory",()=>{var inv=New();var sentinel=new ItemDrop.ItemData();inv.GetAllItems().Add(sentinel);Check(Refuses(()=>OfflineInventory.Load(inv,InventoryRoundTripTests.Encode(109,new InventoryRoundTripTests.Record(),new InventoryRoundTripTests.Record{X=1,Prefab="Missing"})))&&inv.GetAllItems().Single()==sentinel,"partial load dropped inventory");});
  Test("duplicate slots and unknown formats are refused",()=>{var inv=New();var r=new InventoryRoundTripTests.Record();Check(Refuses(()=>OfflineInventory.Load(inv,InventoryRoundTripTests.Encode(109,r,r)))&&Refuses(()=>OfflineInventory.Load(inv,InventoryRoundTripTests.Encode(111,r))),"ambiguous input accepted");});
  Test("empty saved inventory clears the temporary view",()=>{var inv=New();inv.GetAllItems().Add(new ItemDrop.ItemData());OfflineInventory.Load(inv,Array.Empty<byte>());Check(inv.GetAllItems().Count==0&&Inventory.ChangeCalls==0,"empty view retained stock or ran hooks");});
  Test("debit preserves unrelated item metadata and remaining stack metadata",()=>{var wood=new InventoryRoundTripTests.Record();var iron=new InventoryRoundTripTests.Record{X=1,Prefab="Iron",Data=new Dictionary<string,string>{{"EpicLoot:MagicItem","{\"Rarity\":3,\"Effects\":[]}"}}};var inv=New();OfflineInventory.Load(inv,InventoryRoundTripTests.Encode(109,wood,iron));inv.GetAllItems()[0].m_stack-=10;wood.Stack-=10;var p=new ZPackage();inv.Save(p);Check(InventoryRoundTrip.Preserved(InventoryRoundTripTests.Encode(109,wood,iron),p.GetArray(),Strings.GetStableHashCode,out _),"payment modified unrelated data");});
  Test("API decoding uses the same passive reader",()=>{var bytes=InventoryRoundTripTests.Encode(107,new InventoryRoundTripTests.Record());var inv=ApiWorld.Decode(new ZDO(),bytes);Check(inv.GetAllItems().Count==1&&Inventory.LoadCalls==0,"API used live Load");Check(Refuses(()=>ApiWorld.Decode(new ZDO(),new byte[ApiRules.MaxInventoryBytes+1])),"API byte limit lost");});
  Console.WriteLine("Offline inventory runtime tests: "+passed+" passed (game stand-ins)");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
#endif
