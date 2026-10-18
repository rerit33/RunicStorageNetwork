#if FAST_PATH_SAFETY_REVIEW
using System;
using System.Collections.Generic;
using RunicStorageNetwork;

// Deliberately separate from the author's happy-path suite: these are the
// no-loss acceptance criteria for PR #10. Run with FAST_PATH_RUNTIME_TESTS too.
static class FastPathSafetyReview {
 sealed class Scenario {
  internal readonly RuntimePlayer Player;
  internal readonly RuntimeChest Chest;
  internal readonly FastPathCore Engine;
  internal readonly List<string> Warnings=new List<string>();
  internal Scenario(bool playerDebit=false){
   Player=new RuntimePlayer(playerDebit?new Dictionary<string,int>{{"Wood",2}}:new Dictionary<string,int>()){PlacementRefused=true};
   Player.Inventory.Items.Add(Item("Wood",2,0));Player.Inventory.Items.Add(Item("Armor",1,1));
   Chest=new RuntimeChest("chest",new Dictionary<string,int>{{"Wood",3}});
   Chest.Inventory.Items.Add(Item("Wood",3,0));Engine=new FastPathCore(Warnings.Add);
  }
  internal void Build(){Player.Prepare();Chest.Prepare();Engine.TryBuild(Player,new[]{Chest},Player.Place,Player.Finish,()=>Player.Outputs>0);}
  internal void RefundComplete(){Check(Player.Outputs==0&&Player.Inventory.Count("Wood")==2&&Chest.Inventory.Count("Wood")==3&&!Engine.Running,"no building exists, but a material debit was abandoned");}
 }
 static RuntimeItem Item(string name,int stack,int slot)=>new RuntimeItem{Name=name,Stack=stack,Slot=slot,Quality=1,Metadata=name};
 static int passed,failed;
 static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
 static void Test(string name,Action action){try{action();passed++;Console.WriteLine("PASS safety "+name);}catch(Exception error){failed++;Console.WriteLine("FAIL safety "+name+": "+error.Message);}}
 static int Main(){
  Test("successful build debits exactly once",()=>{var s=new Scenario();s.Player.PlacementRefused=false;s.Build();Check(s.Player.Outputs==1&&s.Chest.Inventory.Count("Wood")==0,"wrong output/debit");});
  Test("immediate refusal refunds materials",()=>{var s=new Scenario(true);s.Build();s.RefundComplete();});
  Test("armor wear cannot cancel a pending player refund",()=>{var s=new Scenario(true);s.Player.Inventory.FailAdd=true;s.Build();s.Player.Inventory.Items.Find(i=>i.Name=="Armor").Metadata="armor after wear";s.Player.Inventory.FailAdd=false;s.Engine.Tick();s.RefundComplete();});
  Test("unrelated chest revision cannot cancel its refund",()=>{var s=new Scenario();s.Chest.Inventory.FailAdd=true;s.Build();s.Chest.RevisionId++;s.Chest.Inventory.FailAdd=false;s.Engine.Tick();s.RefundComplete();});
  Test("unrelated chest item cannot cancel its refund",()=>{var s=new Scenario();s.Chest.Inventory.FailAdd=true;s.Build();s.Chest.Inventory.Items.Add(Item("Iron",1,1));s.Chest.Inventory.Changed();s.Chest.Inventory.FailAdd=false;s.Engine.Tick();s.RefundComplete();});
  Test("owner handoff cannot discard refund debt",()=>{var s=new Scenario();s.Chest.Inventory.FailAdd=true;s.Build();s.Chest.OwnerId=2;s.Chest.Inventory.FailAdd=false;s.Engine.Tick();Check(s.Chest.Inventory.Count("Wood")==3||s.Engine.Compensating,"refund debt discarded after owner handoff");});
  Test("world exit cannot discard refund debt",()=>{var s=new Scenario();s.Chest.Inventory.FailAdd=true;s.Build();s.Engine.Clear();Check(s.Chest.Inventory.Count("Wood")==3||s.Engine.Compensating,"refund debt discarded on world exit");});
  Console.WriteLine("Safety acceptance: "+passed+" passed, "+failed+" failed. Stand-ins; no game launched.");return failed==0?0:1;
 }
}
#endif
