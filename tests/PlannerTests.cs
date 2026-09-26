using System;
using System.Collections.Generic;
using System.Linq;
using RunicStorageNetwork.Logic;

static class PlannerTests {
 static int passed;
 static void Assert(bool x,string reason){if(!x)throw new Exception(reason);}
 static void Test(string name,Action body){body();passed++;Console.WriteLine("PASS "+name);}
 static List<Debit> Plan(params Stock[] stock)=>Planner.Plan(new[]{new Need("Wood",10)},stock);
 sealed class FakeOwner {
  public int Count; public string Lease;public int Debited;public bool Access=true,Loaded=true;public readonly HashSet<string> Finished=new HashSet<string>();
  public bool Prepare(string id){if(!Loaded||!Access||Finished.Contains(id)||(Lease!=null&&Lease!=id))return false;Lease=id;return true;}
  public bool Pay(string id,int amount){if(id!=Lease||!Loaded||!Access)return false;if(Debited>0)return Debited==amount;if(Count<amount)return false;Count-=amount;Debited=amount;return true;}
  public void Release(string id,bool rollback){if(Lease!=id)return;if(rollback)Count+=Debited;Debited=0;Lease=null;Finished.Add(id);}
 }
 public static int Main(){try{
  Test("all player",()=>{var p=Plan(new Stock("player","Wood",1,10));Assert(p.Count==1&&p[0].Source=="player"&&p[0].Amount==10,"plan");});
  Test("all chest",()=>Assert(Plan(new Stock("a","Wood",1,10))[0].Amount==10,"plan"));
  Test("mixed 2+3+5 exact",()=>{var p=Plan(new Stock("b","Wood",1,5),new Stock("player","Wood",1,2),new Stock("a","Wood",1,3));Assert(p.Select(x=>x.Amount).SequenceEqual(new[]{2,3,5}),"order");});
  Test("one ingredient multiple chests",()=>Assert(Plan(new Stock("a","Wood",1,4),new Stock("b","Wood",1,6)).Sum(d=>d.Amount)==10,"split"));
  Test("last ingredient shortage is pure",()=>{var s=new Stock("a","Wood",1,20);var p=Planner.Plan(new[]{new Need("Wood",10),new Need("Iron",1)},new[]{s});Assert(p==null&&s.Amount==20,"partial debit");});
  Test("duplicate source does not duplicate supply",()=>Assert(Plan(new Stock("a","Wood",1,5),new Stock("a","Wood",1,5))==null,"duplicate"));
  Test("conflicting duplicate fails",()=>{bool fail=false;try{Plan(new Stock("a","Wood",1,5),new Stock("a","Wood",1,7));}catch(InvalidOperationException){fail=true;}Assert(fail,"conflicting snapshot");});
  Test("access excluded from counts",()=>Assert(Plan(new Stock("a","Wood",1,10,false))==null,"access"));
  Test("quality mismatch",()=>Assert(Planner.Plan(new[]{new Need("Wood",10,2)},new[]{new Stock("a","Wood",1,10)})==null,"quality"));
  Test("exact prefab identity",()=>Assert(Plan(new Stock("a","WoodModded","Wood".Length,100))==null,"identity"));
  Test("shared snapshot not mutated",()=>{var s=new Stock("a","Wood",1,10);Plan(s);Assert(s.Amount==10,"snapshot mutated");});
  Test("repeated requirement cannot overdraw",()=>Assert(Planner.Plan(new[]{new Need("Wood",6),new Need("Wood",6)},new[]{new Stock("a","Wood",1,10)})==null,"overdraw"));
  Test("late access loss",()=>{var o=new FakeOwner{Count=10};Assert(o.Prepare("x"),"prepare");o.Access=false;Assert(!o.Pay("x",10)&&o.Count==10,"lost access");o.Release("x",true);});
  Test("stale cache fresh recheck",()=>{var o=new FakeOwner{Count=5};Assert(o.Prepare("x"),"prepare");Assert(!o.Pay("x",10)&&o.Count==5,"stale stock");});
  Test("unloaded source refused",()=>Assert(!new FakeOwner{Loaded=false}.Prepare("x"),"unloaded"));
  Test("competing last resource",()=>{var o=new FakeOwner{Count=1};Assert(o.Prepare("a")&&!o.Prepare("b"),"interleaved lease");Assert(o.Pay("a",1),"pay");o.Release("a",false);Assert(o.Prepare("b")&&!o.Pay("b",1)&&o.Count==0,"second result");});
  Test("duplicate owner debit idempotent",()=>{var o=new FakeOwner{Count=10};o.Prepare("a");Assert(o.Pay("a",10)&&o.Pay("a",10)&&o.Count==0,"double debit");o.Release("a",false);Assert(!o.Prepare("a"),"replay");});
  Test("prepare failure no payment",()=>{var d=new Decision("a",new[]{"x","y"});d.Prepared("x");d.Refuse();Assert(d.Phase==Phase.Aborted,"abort");bool threw=false;try{d.Commit();}catch(InvalidOperationException){threw=true;}Assert(threw,"commit after refusal");});
  Test("all owner acknowledgements required",()=>{var d=new Decision("a",new[]{"x","y"});d.Prepared("x");d.Prepared("x");Assert(d.Phase==Phase.Preparing,"duplicate ack");d.Prepared("y");d.Commit();d.Paid("x");Assert(d.Phase==Phase.Committing,"early ready");d.Paid("y");Assert(d.Complete()&&!d.Complete(),"double completion");});
  Test("timeout after debit is uncertain",()=>{var d=new Decision("a",new[]{"x"});d.Prepared("x");d.Commit();d.Timeout();Assert(d.Phase==Phase.Uncertain,"blind refund");bool threw=false;try{d.Complete();}catch(InvalidOperationException){threw=true;}Assert(threw,"unconfirmed result");});
  Test("known failure restores operation delta",()=>{var o=new FakeOwner{Count=12};o.Prepare("a");o.Pay("a",10);o.Release("a",true);o.Release("a",true);Assert(o.Count==12,"compensation repeated");});
  Test("pure player protocol",()=>{var d=new Decision("a",new string[0]);d.Commit();Assert(d.Complete(),"local protocol");});
  Test("unknown sender rejected",()=>{var d=new Decision("a",new[]{"x"});bool threw=false;try{d.Prepared("intruder");}catch(InvalidOperationException){threw=true;}Assert(threw,"sender");});
  passed+=RelayTests.Run();passed+=LocalizationTests.Run();passed+=LargeNetworkTests.Run();passed+=HoverCountTests.Run();passed+=MultiplayerTests.Run();passed+=RecoveryTests.Run();passed+=CraftOfferTests.Run();passed+=CraftOverviewTests.Run();passed+=ContainerPolicyTests.Run();passed+=BuildToolTests.Run();passed+=NetworkLabelTests.Run();Console.WriteLine("RESULT "+passed+" isolated tests passed; no Valheim process, world or clients.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
