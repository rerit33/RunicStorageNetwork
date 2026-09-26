using System;
using System.Linq;
using RunicStorageNetwork.Logic;

static class BuildToolTests {
 static int passed;
 static void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
 static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS build tool "+name);}
 static BuildToolRules Defaults()=>new BuildToolRules("",BuildToolRules.DeniedToolDefault,BuildToolRules.DeniedPieceComponentDefault);
 static string[] Building=>new[]{"Piece","WearNTear","ZNetView"};
 static string[] Terrain=>new[]{"Piece","TerrainOp","ZNetView"};
 static string[] Hammer=>new[]{"Hammer"};

 internal static int Run(){
  Test("modded build tools qualify without configuration",()=>{
   var rules=Defaults();
   foreach(string tool in new[]{"Hammer","OdinsHammer","ModdedBuildHammer"})
    Assert(rules.Verdict(new[]{tool},Building)==null,"rejected "+tool);
   Assert(!rules.Restricted,"default configuration must not restrict");
  });
  Test("terrain tools stay out by name and by component",()=>{
   var rules=Defaults();
   Assert(rules.Verdict(new[]{"Hoe"},Building)==BuildToolRules.ExcludedToolReason,"hoe accepted");
   Assert(rules.Verdict(new[]{"Cultivator"},Building)==BuildToolRules.ExcludedToolReason,"cultivator accepted");
   Assert(rules.Verdict(new[]{"ModdedTerraformer"},Terrain)==BuildToolRules.ExcludedPieceReason,"modded terrain piece accepted");
  });
  Test("the piece rule outranks an allowed tool",()=>{
   var rules=Defaults();
   // A hammer that can also raise ground must not pull stone from the network for it.
   Assert(rules.Verdict(new[]{"Hammer","Hoe"},Terrain)==BuildToolRules.ExcludedPieceReason,"terrain piece supplied through an allowed tool");
   Assert(rules.Verdict(new[]{"Hammer","Hoe"},Building)==null,"shared piece lost its allowed placer");
  });
  Test("a piece in several tables qualifies through any allowed one",()=>{
   var rules=Defaults();
   Assert(rules.Verdict(new[]{"Hoe","OdinsHammer"},Building)==null,"allowed placer ignored");
   Assert(rules.Verdict(new[]{"Hoe","Cultivator"},Building)==BuildToolRules.ExcludedToolReason,"all placers denied yet accepted");
  });
  Test("allow list restricts, deny list still wins",()=>{
   var rules=new BuildToolRules("Hammer, Hoe","Hoe",BuildToolRules.DeniedPieceComponentDefault);
   Assert(rules.Restricted&&rules.Tool("Hammer"),"listed tool rejected");
   Assert(!rules.Tool("OdinsHammer"),"unlisted tool accepted");
   Assert(!rules.Tool("Hoe"),"denied tool re-enabled by the allow list");
  });
  Test("hand written lists tolerate spacing, blanks and case",()=>{
   var rules=new BuildToolRules(" OdinsHammer ,, \n Hammer;BuildHammer ","","");
   foreach(string tool in new[]{"odinshammer","HAMMER","BuildHammer"})Assert(rules.Tool(tool),"lost "+tool);
   Assert(!rules.Tool("Cultivator")&&!rules.Tool(null)&&!rules.Tool(""),"unnamed or unlisted tool accepted");
   Assert(rules.DeniedComponentCount==0,"blank component list must be empty");
  });
  Test("cleared lists supply every tool including terrain",()=>{
   var rules=new BuildToolRules("","","");
   Assert(rules.Verdict(new[]{"Hoe"},Terrain)==null,"empty lists must not exclude");
   Assert(rules.Verdict(new string[0],Building)==BuildToolRules.ExcludedToolReason,"a piece no tool can place must not qualify");
   Assert(rules.Verdict(null,Building)==BuildToolRules.ExcludedToolReason,"missing placer list accepted");
  });
  Test("summary reports the effective lists",()=>{
   Assert(new BuildToolRules("","","").Summary.Contains("every build tool"),"default allow description");
   Assert(Defaults().Summary.Contains("Hoe")&&Defaults().Summary.Contains("TerrainOp"),"defaults missing from summary");
   Assert(Defaults().DeniedComponentCount==BuildToolRules.DeniedPieceComponentDefault.Split(',').Length,"component defaults lost");
  });
  return passed;
 }
}
