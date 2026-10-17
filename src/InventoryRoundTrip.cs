using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RunicStorageNetwork.Logic {
 // Compare saved item records, not just bytes. Valheim upgrades old saves and
 // quantizes durability during Load/Save. Neither is evidence of a lost item.
 // Unknown formats, fields, missing items and changed metadata still fail closed.
 internal static class InventoryRoundTrip {
  internal sealed class Item {
   internal int Prefab,Stack=1,X,Y,Quality=1,Variant,World,Durability;
   internal float LoadedDurability;
   internal bool Compact,Equipped,PickedUp,Cheated;
   internal long Crafter;internal string Name="";
   internal readonly Dictionary<string,string> Data=new Dictionary<string,string>(StringComparer.Ordinal);
  }
  internal static bool Preserved(byte[] before,byte[] after,Func<string,int> prefabHash,out string reason){
   reason="";if(before.SequenceEqual(after))return true;
   try {
    var a=Read(before,prefabHash);var b=Read(after,prefabHash);
    if(a.Count!=b.Count){reason="item count changed";return false;}
    var slots=new Dictionary<(int,int),Item>();foreach(var item in b)slots.Add((item.X,item.Y),item);
    var seen=new HashSet<(int,int)>();
    foreach(var item in a){
     if(!seen.Add((item.X,item.Y))||!slots.TryGetValue((item.X,item.Y),out var saved)){reason="item slot changed";return false;}
     if(item.Prefab!=saved.Prefab||item.Stack!=saved.Stack||item.Quality!=saved.Quality||item.Variant!=saved.Variant||item.World!=saved.World||item.Equipped!=saved.Equipped||item.PickedUp!=saved.PickedUp||item.Cheated!=saved.Cheated||item.Crafter!=saved.Crafter||item.Name!=saved.Name){reason="item fields changed at "+item.X+","+item.Y;return false;}
     // Match exactly one native serialization step, not an arbitrary tolerance.
     int normalized=(int)(float)(item.LoadedDurability*100f);
     if(!saved.Compact||(saved.Durability!=normalized&&(!item.Compact||saved.Durability!=item.Durability))){reason="item durability changed";return false;}
     if(item.Data.Count!=saved.Data.Count||item.Data.Any(p=>!saved.Data.TryGetValue(p.Key,out var v)||v!=p.Value)){reason="item custom data changed";return false;}
    }
    return true;
   }catch(Exception e) when(e is IOException||e is InvalidDataException||e is ArgumentException||e is OverflowException){reason="unsupported or malformed inventory: "+e.Message;return false;}
  }
  internal static List<Item> Read(byte[] bytes,Func<string,int> prefabHash){
   using(var stream=new MemoryStream(bytes,false))using(var r=new BinaryReader(stream,new UTF8Encoding(false,true))){
    int version=r.ReadInt32();if(version<100||version>110)throw new InvalidDataException("version "+version);
    bool compact=version>=108;int count=compact?r.ReadUInt16():r.ReadInt32();
    if(count<0||count>65535||count>stream.Length-stream.Position)throw new InvalidDataException("item count");
    var items=new List<Item>(count);
    for(int i=0;i<count;i++){
     var item=new Item{Compact=compact};
     if(compact){
      item.Durability=r.ReadInt32();item.LoadedDurability=(float)(item.Durability*.01f);
      item.X=r.ReadByte();item.Y=r.ReadByte();item.World=r.ReadByte();int flags=r.ReadByte();
      item.PickedUp=(flags&1)!=0;item.Equipped=(flags&2)!=0;
      if((flags&4)!=0)item.Quality=r.ReadUInt16();if((flags&8)!=0)item.Stack=r.ReadUInt16();if((flags&16)!=0)item.Variant=r.ReadInt32();
      if((flags&32)!=0){item.Crafter=r.ReadInt64();item.Name=r.ReadString();}
      if((flags&64)!=0)item.Prefab=r.ReadInt32();
      if((flags&128)!=0){int n=r.ReadByte();if((n&128)!=0)n=((n&127)<<8)|r.ReadByte();Custom(r,item,n);}
      if(version>=109){int flags2=r.ReadByte();if((flags2&~1)!=0)throw new InvalidDataException("unknown item flags");item.Cheated=version>=110&&(flags2&1)!=0;}
     }else {
      string prefab=r.ReadString();if(prefab.Length==0)throw new InvalidDataException("missing prefab");item.Prefab=prefabHash(prefab);
      item.Stack=r.ReadInt32();item.LoadedDurability=r.ReadSingle();item.X=r.ReadInt32();item.Y=r.ReadInt32();item.Equipped=r.ReadBoolean();
      if(version>=101)item.Quality=r.ReadInt32();if(version>=102)item.Variant=r.ReadInt32();
      if(version>=103){item.Crafter=r.ReadInt64();item.Name=r.ReadString();}if(version>=104)Custom(r,item,r.ReadInt32());
      if(version>=105)item.World=r.ReadInt32();if(version>=106)item.PickedUp=r.ReadBoolean();if(version==107)item.Cheated=r.ReadBoolean();
     }
     if(item.Prefab==0||item.Stack<1||item.Quality<1||float.IsNaN(item.LoadedDurability)||float.IsInfinity(item.LoadedDurability))throw new InvalidDataException("invalid item");
     items.Add(item);
    }
    if(stream.Position!=stream.Length)throw new InvalidDataException("unrecognized trailing data");return items;
   }
  }
  static void Custom(BinaryReader r,Item item,int count){
   if(count<0||count>32767||count>(r.BaseStream.Length-r.BaseStream.Position)/2)throw new InvalidDataException("custom data count");
   for(int i=0;i<count;i++)item.Data.Add(r.ReadString(),r.ReadString());
  }
 }
}
