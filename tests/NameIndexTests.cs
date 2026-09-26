using System;
using System.Linq;
using RunicStorageNetwork.Logic;

static class NameIndexTests {
 static int passed;
 static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS name index "+name);}
 sealed class Entry { public readonly string Tag; public Entry(string tag){Tag=tag;} }
 static NameIndex<Entry> Index(params string[] names){
  var index=new NameIndex<Entry>();
  for(int i=0;i<names.Length;i++)index.Add(names[i],new Entry(names[i]+"#"+i));
  return index;
 }

 internal static int Run(){
  Test("names resolve to their own entry",()=>{
   var index=Index("Recipe_Wood","Recipe_Iron","Recipe_ModdedBlade");
   Assert(index.Count==3&&index.Find("Recipe_ModdedBlade").Tag=="Recipe_ModdedBlade#2","lookup");
   Assert(index.Find("missing")==null&&index.Find("")==null&&index.Find(null)==null,"absent name must not resolve");
  });
  Test("an ambiguous name resolves to the first entry on every side",()=>{
   var index=Index("Recipe_Wood","Recipe_Shared","Recipe_Shared","Recipe_Shared");
   Assert(index.Find("Recipe_Shared").Tag=="Recipe_Shared#1","later entry won");
   Assert(index.Ambiguous("Recipe_Shared")&&!index.Ambiguous("Recipe_Wood"),"ambiguity not reported");
   Assert(index.DuplicateCount==1&&index.Duplicates.Single()=="Recipe_Shared","duplicate reported more than once");
   Assert(index.Count==2,"ambiguous name counted twice");
  });
  Test("unnamed entries are counted, never indexed",()=>{
   var index=Index("Recipe_Wood",null,"","Recipe_Iron");
   Assert(index.Unnamed==2&&index.Count==2,"unnamed entries indexed");
   Assert(index.Find(null)==null&&index.Find("")==null,"unnamed entry resolved");
  });
  Test("null values are ignored",()=>{
   var index=new NameIndex<Entry>();index.Add("Recipe_Wood",null);
   Assert(index.Count==0&&index.Unnamed==0&&index.Find("Recipe_Wood")==null,"null value indexed");
  });
  Test("lookup is ordinal, matching the name comparison on the wire",()=>{
   var index=Index("Recipe_Wood");
   Assert(index.Find("recipe_wood")==null&&index.Find("Recipe_Wood")!=null,"case folded");
  });
  Test("the report names what a server operator has to fix",()=>{
   Assert(Index("a","b").Report=="2 named","clean report: "+Index("a","b").Report);
   var messy=Index("a","a",null,"b","b");
   Assert(messy.Report.Contains("1 unnamed")&&messy.Report.Contains("ambiguous: a, b"),"report: "+messy.Report);
  });
  return passed;
 }
}
