using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using RunicStorageNetwork.Logic;

static class InventoryRoundTripTests {
 internal sealed class Record {
  internal string Prefab="Wood",Name="Crafter";internal int Stack=50,X,Y,Quality=2,Variant=1,World=2,Durability=12345;
  internal float LegacyDurability=123.45f;internal long Crafter=42;internal bool Equipped,PickedUp=true,Cheated=true;
  internal Dictionary<string,string> Data=new Dictionary<string,string>{{"mod:data","value"},{"rsn_builder_network","identity"}};
 }
 internal static int Hash(string id){unchecked{int hash=17;foreach(char c in id)hash=hash*31+c;return hash;}}
 internal static byte[] Encode(int version,params Record[] items){
  using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){
   w.Write(version);if(version>=108)w.Write((ushort)items.Length);else w.Write(items.Length);
   foreach(var i in items){
    if(version>=108){
     w.Write(i.Durability);w.Write((byte)i.X);w.Write((byte)i.Y);w.Write((byte)i.World);
     w.Write((byte)((i.PickedUp?1:0)|(i.Equipped?2:0)|4|8|16|32|64|128));w.Write((ushort)i.Quality);w.Write((ushort)i.Stack);w.Write(i.Variant);w.Write(i.Crafter);w.Write(i.Name);w.Write(Hash(i.Prefab));
     if(i.Data.Count<128)w.Write((byte)i.Data.Count);else {w.Write((byte)((i.Data.Count>>8)|128));w.Write((byte)i.Data.Count);}
     foreach(var p in i.Data){w.Write(p.Key);w.Write(p.Value);}if(version>=109)w.Write((byte)(i.Cheated?1:0));
    }else {
     w.Write(i.Prefab);w.Write(i.Stack);w.Write(i.LegacyDurability);w.Write(i.X);w.Write(i.Y);w.Write(i.Equipped);
     if(version>=101)w.Write(i.Quality);if(version>=102)w.Write(i.Variant);if(version>=103){w.Write(i.Crafter);w.Write(i.Name);}
     if(version>=104){w.Write(i.Data.Count);foreach(var p in i.Data){w.Write(p.Key);w.Write(p.Value);}}
     if(version>=105)w.Write(i.World);if(version>=106)w.Write(i.PickedUp);if(version==107)w.Write(i.Cheated);
    }
   }return s.ToArray();
  }
 }
 static int passed;
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static bool Same(byte[] a,byte[] b)=>InventoryRoundTrip.Preserved(a,b,Hash,out _);
 static void Test(string title,Action action){action();passed++;Console.WriteLine("PASS inventory round trip "+title);}
 public static int Run(){
  Test("legacy inventory upgrades without losing fields",()=>{var r=new Record();Check(Same(Encode(107,r),Encode(110,r)),"legacy upgrade rejected");});
  Test("compact inventory adds the default cheated flag",()=>{var r=new Record{Cheated=false};Check(Same(Encode(108,r),Encode(110,r)),"108 upgrade rejected");});
  Test("oldest supported records retain their defaults",()=>{var r=new Record{Quality=1,Variant=0,Crafter=0,Name="",World=0,PickedUp=false,Cheated=false,Data=new Dictionary<string,string>()};Check(Same(Encode(100,r),Encode(110,r)),"defaults rejected");});
  Test("native float durability quantization is not data loss",()=>{var r=new Record();int n=Enumerable.Range(1,20000).First(x=>(int)(float)((float)(x*.01f)*100f)!=x);r.Durability=n;var before=Encode(110,r);r.Durability=(int)(float)((float)(n*.01f)*100f);Check(!before.SequenceEqual(Encode(110,r))&&Same(before,Encode(110,r)),"native quantization rejected");});
  Test("arbitrary durability change is refused",()=>{var r=new Record();var a=Encode(110,r);r.Durability-=10;Check(!Same(a,Encode(110,r)),"durability damage accepted");});
  Test("dictionary order may change but every value survives",()=>{var r=new Record();var a=Encode(110,r);r.Data=r.Data.Reverse().ToDictionary(p=>p.Key,p=>p.Value);Check(Same(a,Encode(110,r)),"dictionary order rejected");r.Data["mod:data"]="lost";Check(!Same(a,Encode(110,r)),"custom value lost");});
  Test("large custom-data count uses native two-byte encoding",()=>{var r=new Record{Data=Enumerable.Range(0,130).ToDictionary(x=>"k"+x,x=>"v"+x)};Check(Same(Encode(107,r),Encode(110,r)),"large dictionary rejected");});
  Test("item ordering can change without merging stacks",()=>{var a=new Record();var b=new Record{X=1,Prefab="Iron"};Check(Same(Encode(107,a,b),Encode(110,b,a)),"order rejected");});
  Test("dropped unknown item is refused",()=>{var a=new Record();var b=new Record{X=1,Prefab="Missing"};Check(!Same(Encode(110,a,b),Encode(110,a)),"dropped item accepted");});
  Test("stack clipping and merging are refused",()=>{var r=new Record();var a=Encode(110,r);r.Stack=49;Check(!Same(a,Encode(110,r)),"stack clipping accepted");});
  Test("prefab and quality substitution are refused",()=>{var r=new Record();var a=Encode(110,r);r.Prefab="Iron";Check(!Same(a,Encode(110,r)),"prefab accepted");r.Prefab="Wood";r.Quality++;Check(!Same(a,Encode(110,r)),"quality accepted");});
  Test("flags and crafter metadata are preserved",()=>{foreach(var mutate in new Action<Record>[] {r=>r.Equipped=true,r=>r.PickedUp=false,r=>r.Cheated=false,r=>r.Crafter=99,r=>r.Name="Other",r=>r.World++,r=>r.Variant++}){var r=new Record();var a=Encode(110,r);mutate(r);Check(!Same(a,Encode(110,r)),"changed item field accepted");}});
  Test("moving or overlapping slots is refused",()=>{var r=new Record();var a=Encode(110,r);r.X++;Check(!Same(a,Encode(110,r)),"slot move accepted");Check(!Same(Encode(107,r,r),Encode(110,r,r)),"duplicate slot accepted");});
  Test("unknown versions and trailing fields are refused",()=>{var r=new Record();Check(!Same(Encode(111,r),Encode(110,r)),"future format accepted");Check(!Same(Encode(107,r).Concat(new byte[]{0}).ToArray(),Encode(110,r)),"trailing data lost");});
  Test("truncated records and malformed counts are refused",()=>{var a=Encode(110,new Record());Check(!Same(a.Take(a.Length-1).ToArray(),a),"truncated record accepted");Check(!Same(new byte[]{107,0,0,0,255,255,255,127},a),"invalid count accepted");});
  Test("version 110 inventory is read with its cheated flag",()=>{var r=new Record();var items=InventoryRoundTrip.Read(Encode(110,r),Hash);Check(items.Count==1&&items[0].Cheated&&items[0].Prefab==Hash("Wood"),"110 record misread");});
  Test("version 109 cheated flag is ignored as the game does",()=>{var r=new Record();var items=InventoryRoundTrip.Read(Encode(109,r),Hash);Check(items.Count==1&&!items[0].Cheated,"109 cheated flag honored");var clean=new Record{Cheated=false};Check(Same(Encode(109,r),Encode(110,clean)),"109 save rejected against the 110 writer");Check(!Same(Encode(109,r),Encode(110,r)),"109 flag treated as kept");});
  return passed;
 }
}
