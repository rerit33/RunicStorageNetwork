#if FAST_PATH_RUNTIME_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RunicStorageNetwork;

sealed class RuntimeItem {
 internal string Name,Metadata;internal int Stack,Quality,Slot;
 internal RuntimeItem Clone()=>new RuntimeItem{Name=Name,Stack=Stack,Quality=Quality,Metadata=Metadata,Slot=Slot};
}
sealed class RuntimeInventory {
 internal readonly List<RuntimeItem> Items=new List<RuntimeItem>();
 internal Action OnChanged;internal bool FailAdd;internal int FailRemoveAt,Removes,Adds;
 internal byte[] Bytes(){using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream)){writer.Write(Items.Count);foreach(var item in Items){writer.Write(item.Name);writer.Write(item.Stack);writer.Write(item.Quality);writer.Write(item.Metadata);writer.Write(item.Slot);}return stream.ToArray();}}}
 internal void Changed()=>OnChanged?.Invoke();
 internal void Remove(RuntimeItem item,int amount){item.Stack-=amount;if(item.Stack==0)Items.Remove(item);Removes++;Changed();if(Removes==FailRemoveAt)throw new InvalidOperationException("RemoveItem refused after mutation");}
 internal RuntimeItem Add(RuntimeItem item,int amount){Adds++;if(FailAdd)throw new InvalidOperationException("AddItem refused");var restored=Items.FirstOrDefault(existing=>existing.Slot==item.Slot);if(restored!=null){if(restored.Name!=item.Name||restored.Quality!=item.Quality)throw new InvalidOperationException("rollback slot occupied");restored.Stack+=amount;}else{restored=item.Clone();restored.Stack=amount;Items.Add(restored);}Changed();return restored;}
 internal int Count(string name)=>Items.Where(item=>item.Name==name).Sum(item=>item.Stack);
}
sealed class RuntimeDelta {
 sealed class Part {internal RuntimeItem Item;internal int Amount,Removed;}
 readonly RuntimeInventory inventory;readonly List<Part> parts=new List<Part>();internal bool Applied;
 internal RuntimeDelta(RuntimeInventory inventory,Dictionary<string,int> debits){
  this.inventory=inventory;
  foreach(var debit in debits){int left=debit.Value;foreach(var item in inventory.Items.Where(item=>item.Name==debit.Key)){int amount=Math.Min(left,item.Stack);if(amount==0)continue;parts.Add(new Part{Item=item,Amount=amount});left-=amount;if(left==0)break;}if(left!=0)throw new InvalidOperationException("insufficient stock");}
 }
 internal string Description=>string.Join(",",parts.Where(part=>part.Removed>0).GroupBy(part=>part.Item.Name).Select(group=>group.Key+"="+group.Sum(part=>part.Removed)));
 internal bool RestoreComplete=>parts.All(part=>part.Removed==0);
 internal void Apply(){
  if(Applied)return;
  foreach(var part in parts)if(!inventory.Items.Contains(part.Item)||part.Item.Stack<part.Amount)throw new InvalidOperationException("stale inventory");
  Applied=true;
  try{foreach(var part in parts){int before=part.Item.Stack;try{inventory.Remove(part.Item,part.Amount);}finally{part.Removed=before-(inventory.Items.Contains(part.Item)?part.Item.Stack:0);}}}
  catch{Restore();throw;}
 }
 internal void Restore(){
  if(!Applied)return;
  foreach(var part in parts.Where(part=>part.Removed>0)){
   int before=inventory.Items.Contains(part.Item)?part.Item.Stack:0,amount=part.Removed;
   try{if(inventory.Items.Contains(part.Item))part.Item.Stack+=amount;else {try{inventory.Add(part.Item,amount);}finally{var placed=inventory.Items.FirstOrDefault(item=>item.Slot==part.Item.Slot&&item.Name==part.Item.Name&&item.Quality==part.Item.Quality);if(placed!=null)part.Item=placed;}}}
   finally{int restored=(inventory.Items.Contains(part.Item)?part.Item.Stack:0)-before;part.Removed-=Math.Max(0,Math.Min(amount,restored));}
  }
  Applied=parts.Any(part=>part.Removed>0);inventory.Changed();
 }
}
sealed class RuntimePlayer:FastPathCorePlayer {
 internal readonly RuntimeInventory Inventory=new RuntimeInventory();internal readonly Dictionary<string,int> Debits;
 internal RuntimeDelta Delta;internal int[] Order;internal bool Held,ToolLocked,PlacementRefused,ThrowAfterOutput,FailFinish,FailOrderAfterRestore,CorruptAfterRestore,FailRelease;
 internal int Outputs,Costs,Stamina=100,Skill,Debt,Durability=100,LastUse,Effects,Restores,ByteReads,FailBytesAt;internal Action AfterRestore;
 internal RuntimePlayer(Dictionary<string,int> debits){Debits=debits;}
 internal void Prepare(){Delta=new RuntimeDelta(Inventory,Debits);}
 internal override byte[] Bytes{get{if(++ByteReads==FailBytesAt)throw new InvalidOperationException("player inventory bytes unavailable");return Inventory.Bytes();}}
 internal override string DebitDescription=>Delta.Description;
 internal override bool RestoreComplete=>Delta.RestoreComplete;
 internal override void AcquireHold(){Order=Inventory.Items.Select(item=>item.Slot).ToArray();Held=true;ToolLocked=true;}
 internal override void ApplyDebit()=>Delta.Apply();
 internal override void RestoreDebit(){Restores++;Delta.Restore();AfterRestore?.Invoke();if(FailOrderAfterRestore)Inventory.Items[0].Slot++;var restored=FastPathCore.RestoreOrder(Inventory.Items,Order,item=>item.Slot);Inventory.Items.Clear();Inventory.Items.AddRange(restored);Inventory.Changed();if(CorruptAfterRestore)Inventory.Items[0].Metadata="changed-after-restore";}
 internal override void ReleaseHold(){if(FailRelease)throw new InvalidOperationException("player unblock refused");Held=false;ToolLocked=false;}
 internal override void ClearHold()=>ReleaseHold();
 internal bool Place(){if(PlacementRefused)return false;Outputs++;if(ThrowAfterOutput)throw new InvalidOperationException("placement callback failed after output");return true;}
 internal void Finish(){Costs++;Stamina-=5;if(Debt>0)Debt--;else Skill++;Durability-=2;LastUse=42;Effects++;if(FailFinish)throw new InvalidOperationException("build effect failed");}
}
sealed class RuntimeChest:FastPathCoreSource {
 internal readonly string Name;internal readonly RuntimeInventory Inventory=new RuntimeInventory();internal readonly Dictionary<string,int> Debits;
 internal RuntimeDelta Delta;internal int[] Order;internal byte[] Stored;
 internal long OwnerId=1;internal uint RevisionId;internal bool InUse,IntegrationBlocked,Held,Valid=true,FailSaveBefore,FailSaveAfter,FailRelease,FailOrderAfterRestore,CorruptAfterRestore,FailBytes,FailDescription;
 internal int Saves,Releases,ForcedReleases,Restores;
 internal Action OnApply,OnSave,AfterRestore;internal RuntimeChest(string name,Dictionary<string,int> debits){Name=name;Debits=debits;Inventory.OnChanged=()=>{if(OwnerId==1)Save();};}
 internal void Prepare(){Stored=Inventory.Bytes();Delta=new RuntimeDelta(Inventory,Debits);}
 internal override string Key=>Name;internal override long Owner=>1;internal override long CurrentOwner=>OwnerId;internal override uint Revision=>RevisionId;internal override bool IsValid=>Valid;
 internal override byte[] Bytes{get{if(FailBytes)throw new InvalidOperationException("inventory bytes unavailable");return Inventory.Bytes();}}
 internal override string DebitDescription{get{if(FailDescription)throw new InvalidOperationException("debit description unavailable");return Delta.Description;}}
 internal override bool RestoreComplete=>Delta.RestoreComplete;
 internal override void AcquireHold(){Order=Inventory.Items.Select(item=>item.Slot).ToArray();Held=true;InUse=true;IntegrationBlocked=true;}
 internal override void ApplyDebit(){OnApply?.Invoke();Delta.Apply();}
 internal override void Save(){Saves++;OnSave?.Invoke();if(FailSaveBefore)throw new InvalidOperationException("save refused before write");var bytes=Inventory.Bytes();if(!bytes.SequenceEqual(Stored)){Stored=bytes;RevisionId++;}if(FailSaveAfter)throw new InvalidOperationException("save refused after write");}
 internal override void RestoreDebit(){Restores++;Delta.Restore();AfterRestore?.Invoke();if(FailOrderAfterRestore)Inventory.Items[0].Slot++;var restored=FastPathCore.RestoreOrder(Inventory.Items,Order,item=>item.Slot);Inventory.Items.Clear();Inventory.Items.AddRange(restored);Inventory.Changed();if(CorruptAfterRestore)Inventory.Items[0].Metadata="changed-after-restore";}
 internal override void ReleaseHold(){if(!Held)return;Releases++;if(FailRelease)throw new InvalidOperationException("integration unblock refused");DropHold();}
 void DropHold(){Held=false;InUse=false;IntegrationBlocked=false;}
 internal override void ClearHold(){ForcedReleases++;try{ReleaseHold();}finally{DropHold();}}
}

static class FastPathRuntimeTests {
 static int passed,failed;static readonly List<string> warnings=new List<string>();
 static void Test(string name,string reason,Action action){warnings.Clear();try{action();passed++;Console.WriteLine("PASS fast path "+name+": "+reason);}catch(Exception error){failed++;Console.WriteLine("FAIL fast path "+name+": "+error.Message);}}
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static Dictionary<string,int> Debit(string name,int amount)=>new Dictionary<string,int>{{name,amount}};
 static RuntimeItem Item(string name,int stack,int slot,string metadata)=>new RuntimeItem{Name=name,Stack=stack,Slot=slot,Quality=1,Metadata=metadata};
 static RuntimePlayer Player(Dictionary<string,int> debit=null){var player=new RuntimePlayer(debit??new Dictionary<string,int>());player.Inventory.Items.Add(Item("Wood",2,4,"player"));return player;}
 static RuntimeChest Chest(string key,int stock=4,int debit=3){var chest=new RuntimeChest(key,Debit("Wood",debit));chest.Inventory.Items.Add(Item("Wood",stock,7,key));return chest;}
 static FastPathCore Engine()=>new FastPathCore(message=>warnings.Add(message));
 static void Prepare(RuntimePlayer player,params RuntimeChest[] chests){player.Prepare();foreach(var chest in chests)chest.Prepare();}
 static bool Build(FastPathCore engine,RuntimePlayer player,params RuntimeChest[] chests){Prepare(player,chests);return engine.TryBuild(player,chests,player.Place,player.Finish,()=>player.Outputs>0);}
 static void Released(FastPathCore engine,RuntimePlayer player,params RuntimeChest[] chests){Check(!engine.Running&&!engine.Blocks(player)&&!engine.Active&&!player.Held&&!player.ToolLocked,"player state remained");foreach(var chest in chests)Check(!chest.Held&&!chest.InUse&&!chest.IntegrationBlocked,"chest hold remained: "+chest.Name);}
 static FastPathSourceState Eligible()=>new FastPathSourceState{Valid=true,SceneOwner=true,Owner=1,LocalOwner=1,Access=true,Fresh=true};
 static int Main(){
  Test("build entry preserves other players and ordinary flow","guards apply only to the transaction's player",()=>{
   Check(FastPathCore.BuildEntry(false,false,false,false)==FastPathBuildEntry.Continue,"ordinary flow blocked");
   Check(FastPathCore.BuildEntry(false,false,true,false)==FastPathBuildEntry.Continue,"no transaction blocked");
   foreach(bool blocked in new[]{false,true})Check(FastPathCore.BuildEntry(blocked,true,false,false)==FastPathBuildEntry.Continue,"other player blocked");
   Check(FastPathCore.BuildEntry(false,true,true,false)==FastPathBuildEntry.Refuse,"transaction reentered");
   Check(FastPathCore.BuildEntry(true,true,true,false)==FastPathBuildEntry.Refuse,"pending compensation or cleanup allowed");
  });
  Test("nested native build passes once","the transaction permits one nested native entry",()=>{
   var player=Player();var chest=Chest("one");var engine=Engine();Prepare(player,chest);bool active=false;
   bool handled=engine.TryBuild(player,new[]{chest},()=>{
    Check(active&&engine.Active,"placement flag missing");
    Check(FastPathCore.BuildEntry(engine.Blocks(player),engine.Running,true,engine.NativeReady(player))==FastPathBuildEntry.Native,"native call refused");
    Check(engine.ConsumeNativePlacement(player)&&!engine.ConsumeNativePlacement(player),"native token not one-use");
    Check(FastPathCore.BuildEntry(engine.Blocks(player),engine.Running,true,engine.NativeReady(player))==FastPathBuildEntry.Refuse,"placement callback reentered");
    return player.Place();
   },player.Finish,()=>player.Outputs>0,placing=>active=placing);
   Check(handled&&!active&&player.Outputs==1&&player.Costs==1,"placement or cost count wrong");
   Released(engine,player,chest);
  });
  Test("source eligibility refuses every unsafe flag","all plain source values are decided by production core",()=>{
   Check(FastPathCore.SourceEligible(Eligible()),"eligible source refused");
   var changes=new Action<FastPathSourceState>[]{state=>state.Valid=false,state=>state.SceneOwner=false,state=>state.Owner=2,state=>state.Replica=true,state=>state.Access=false,state=>state.InUse=true,state=>state.NetworkInUse=true,state=>state.Locked=true,state=>state.Reserved=true,state=>state.Busy=true,state=>state.Held=true};
   foreach(var change in changes){var state=Eligible();change(state);Check(!FastPathCore.SourceEligible(state),"unsafe source allowed");}
   Check(!FastPathCore.SourceEligible(null),"missing source allowed");
  });
  Test("stale source falls back","a source must reload current saved contents before its stock is used",()=>{var state=Eligible();state.Fresh=false;Check(FastPathCore.SourceCanReload(state),"otherwise safe source refused");Check(!FastPathCore.SourceEligible(state),"stale source allowed");});
  Test("player eligibility falls back","feature off and player or tool locks never enter the fast path",()=>{
   Check(FastPathCore.PlayerEligible(true,true,false,false,false,false,false,false),"eligible player refused");
   for(int index=0;index<8;index++){var flags=new[]{true,true,false,false,false,false,false,false};flags[index]=!flags[index];Check(!FastPathCore.PlayerEligible(flags[0],flags[1],flags[2],flags[3],flags[4],flags[5],flags[6],flags[7]),"unsafe player flag allowed: "+index);}
  });
  Test("gateway source sets must match","filtered or newly substituted stock uses the coordinated path",()=>{
   Check(FastPathCore.PlanSourcesMatch(new[]{"one","two"},new[]{"two","one","one"}),"same set refused");
   Check(!FastPathCore.PlanSourcesMatch(new[]{"one","two"},new[]{"one"})&&!FastPathCore.PlanSourcesMatch(new[]{"one"},new[]{"two"}),"filtered stock allowed");
   Check(!FastPathCore.PlanSourcesMatch(Array.Empty<string>(),Array.Empty<string>())&&!FastPathCore.PlanSourcesMatch(null,new[]{"one"})&&!FastPathCore.PlanSourcesMatch(new[]{""},new[]{""})&&!FastPathCore.PlanSourcesMatch(new[]{"player"},new[]{"player"}),"missing or malformed set allowed");
  });
  Test("first player bytes fault falls back without a hold","snapshot failure happens before any debit or reservation",()=>{
   var player=Player(Debit("Wood",2));var chest=Chest("one");Prepare(player,chest);var playerBefore=player.Inventory.Bytes();var sourceBefore=chest.Bytes;player.FailBytesAt=1;var engine=Engine();
   Check(!engine.TryBuild(player,new[]{chest},player.Place,player.Finish),"initial bytes fault was handled as a build");Check(player.ByteReads==1&&player.Inventory.Bytes().SequenceEqual(playerBefore)&&chest.Bytes.SequenceEqual(sourceBefore)&&chest.Saves==0&&chest.Inventory.Removes==0&&player.Inventory.Removes==0&&player.Outputs==0&&player.Costs==0,"initial fault changed inventory or placed output");Released(engine,player,chest);
   Check(engine.TryBuild(player,new[]{chest},player.Place,player.Finish)&&player.Outputs==1&&player.Costs==1,"fallback left stale transaction state");Released(engine,player,chest);
  });
  Test("first compensation bytes fault retries without escaping","first-frame errors share the tick warning flag and retain the refund",()=>{
   var player=Player(Debit("Wood",2));var chest=Chest("one",3,3);Prepare(player,chest);var before=chest.Bytes;var playerBefore=player.Bytes;var messages=new List<string>();var engine=new FastPathCore(message=>warnings.Add(message),message=>messages.Add(message));
   Check(engine.TryBuild(player,new[]{chest},()=>{chest.FailBytes=true;return false;},player.Finish),"first compensation fault was not handled");string warning="fast path first-frame retry pending: inventory bytes unavailable";
   Check(engine.Compensating&&engine.Blocks(player)&&chest.Held&&chest.InUse&&chest.IntegrationBlocked&&!engine.Active&&warnings.SequenceEqual(new[]{warning})&&player.Outputs==0&&player.Costs==0,"first compensation fault lost its hold or diagnostic");int removes=chest.Inventory.Removes;engine.Tick();Check(engine.Compensating&&chest.Held&&warnings.SequenceEqual(new[]{warning})&&messages.Last()=="fast path tick retry pending: inventory bytes unavailable","tick repeated the first-frame warning or lost the transaction");
   chest.FailBytes=false;engine.Tick();Check(chest.Bytes.SequenceEqual(before)&&chest.Stored.SequenceEqual(before)&&player.Bytes.SequenceEqual(playerBefore)&&chest.Inventory.Removes==removes&&warnings.SequenceEqual(new[]{warning})&&player.Outputs==0&&player.Costs==0,"fault recovery repeated payment or lost the refund");Released(engine,player,chest);
  });
  Test("vanilla change-save completes","the debit's own revision bump is not treated as an external change",()=>{
   var player=Player();var chest=Chest("one");Prepare(player,chest);chest.Save();chest.Save();Check(chest.Revision==0,"unchanged save bumped revision");
   var engine=Engine();Check(engine.TryBuild(player,new[]{chest},player.Place,player.Finish,()=>player.Outputs>0),"build not handled");
   Check(chest.Inventory.Count("Wood")==1&&chest.Revision==1&&player.Outputs==1&&player.Costs==1,"wrong debit or output");Check(chest.Stored.SequenceEqual(chest.Bytes),"debit not saved");chest.Save();Check(chest.Revision==1,"explicit save double bumped revision");Released(engine,player,chest);
  });
  Test("mixed player and several chest debits","the exact plan removes several item groups and whole stacks once",()=>{
   var player=Player(Debit("Wood",2));var first=Chest("first",3,3);first.Inventory.Items.Add(Item("Stone",2,1,"stone"));first.Debits.Add("Stone",1);var second=Chest("second",4,2);var engine=Engine();
   Check(Build(engine,player,first,second),"build not handled");Check(player.Inventory.Count("Wood")==0&&first.Inventory.Count("Wood")==0&&first.Inventory.Count("Stone")==1&&second.Inventory.Count("Wood")==2,"wrong mixed debit");
   Check(player.Outputs==1&&player.Costs==1&&player.Stamina==95&&player.Skill==1&&player.Debt==0&&player.Durability==98&&player.LastUse==42&&player.Effects==1,"build costs not once");Released(engine,player,first,second);
  });
  Test("build debt charged once","output pays debt instead of raising skill",()=>{var player=Player();player.Debt=2;var chest=Chest("one");var engine=Engine();Build(engine,player,chest);Check(player.Debt==1&&player.Skill==0&&player.Costs==1,"debt charged incorrectly");});
  Test("placement refusal restores ordered bytes","whole-stack restoration preserves metadata, slots and serialized order",()=>{
   var player=Player(Debit("Wood",2));player.Inventory.Items.Add(Item("Stone",2,1,"player-stone"));player.PlacementRefused=true;var chest=Chest("one",3,3);chest.Inventory.Items.Add(Item("Iron",1,0,"extra"));var before=chest.Bytes;var playerBefore=player.Bytes;var engine=Engine();
   Build(engine,player,chest);Check(chest.Bytes.SequenceEqual(before)&&chest.Stored.SequenceEqual(before)&&player.Bytes.SequenceEqual(playerBefore),"restore not byte exact");Check(player.Outputs==0&&player.Costs==0,"failed placement charged");Released(engine,player,chest);
  });
  Test("partial RemoveItem restores within click","an exception after one changed stack leaves no payment",()=>{
   var player=Player();var chest=Chest("one",2,2);chest.Inventory.Items.Add(Item("Stone",2,0,"extra"));chest.Debits.Add("Stone",1);chest.Inventory.FailRemoveAt=1;var before=chest.Bytes;var engine=Engine();Build(engine,player,chest);
   Check(chest.Bytes.SequenceEqual(before)&&player.Costs==0&&player.Outputs==0,"partial removal retained debit");Released(engine,player,chest);
  });
  Test("later source failure restores earlier source","each source restores independently",()=>{
   var player=Player();var first=Chest("first",3,3);first.Inventory.Items.Add(Item("Iron",1,0,"extra"));var second=Chest("second");second.Inventory.FailRemoveAt=1;var firstBefore=first.Bytes;var secondBefore=second.Bytes;var engine=Engine();Build(engine,player,first,second);
   Check(first.Bytes.SequenceEqual(firstBefore)&&second.Bytes.SequenceEqual(secondBefore)&&player.Costs==0,"earlier source lost");Released(engine,player,first,second);
  });
  foreach(bool after in new[]{false,true})Test("save failure "+(after?"after":"before")+" write","automatic save errors recapture changed contents before compensation",()=>{
   var player=Player();var chest=Chest("one",3,3);var before=chest.Bytes;chest.FailSaveBefore=!after;chest.FailSaveAfter=after;var engine=Engine();Build(engine,player,chest);Check(engine.Blocks(player)&&!engine.Active&&player.Costs==0,"save failure not pending");
   chest.FailSaveBefore=false;chest.FailSaveAfter=false;engine.Tick();Check(chest.Bytes.SequenceEqual(before)&&chest.Stored.SequenceEqual(before),"failed save restore not exact");Released(engine,player,chest);
  });
  Test("compensation retries AddItem without another payment","owning player stays blocked while a later frame restores",()=>{
   var player=Player(Debit("Wood",2));player.PlacementRefused=true;var chest=Chest("one",3,3);var before=chest.Bytes;chest.Inventory.FailAdd=true;var engine=Engine();Build(engine,player,chest);
   Check(engine.Compensating&&engine.Blocks(player)&&chest.Held&&!engine.Active&&player.Costs==0,"restore not pending");int removes=chest.Inventory.Removes;
   Check(!engine.TryBuild(player,new[]{chest},player.Place,player.Finish)&&chest.Inventory.Removes==removes,"pending transaction started another payment");
   Check(FastPathCore.BuildEntry(engine.Blocks(player),engine.Running,true,engine.NativeReady(player))==FastPathBuildEntry.Refuse,"owning player allowed");
   chest.Inventory.FailAdd=false;engine.Tick();Check(chest.Bytes.SequenceEqual(before)&&player.Inventory.Count("Wood")==2,"retry did not restore");Released(engine,player,chest);
  });
  foreach(string mismatch in new[]{"owner","revision","bytes","invalid"})Test(mismatch+" mismatch abandons only that source","permanent mismatch never writes and does not prevent other restores",()=>{
   var player=Player();player.PlacementRefused=true;var first=Chest("first",3,3);var second=Chest("second",3,3);first.Inventory.FailAdd=true;second.Inventory.FailAdd=true;var secondBefore=second.Bytes;var engine=Engine();Build(engine,player,first,second);
   if(mismatch=="owner")first.OwnerId=2;else if(mismatch=="revision")first.RevisionId++;else if(mismatch=="bytes")first.Inventory.Items.Add(Item("Iron",1,0,"external"));else first.Valid=false;
   var external=first.Bytes;int saves=first.Saves,adds=first.Inventory.Adds;second.Inventory.FailAdd=false;engine.Tick();
   Check(first.Bytes.SequenceEqual(external)&&first.Saves==saves&&first.Inventory.Adds==adds,"mismatch wrote to source");Check(second.Bytes.SequenceEqual(secondBefore),"other source not restored");
   Check(warnings.SequenceEqual(new[]{"fast path debit unrestored key=first items=Wood=3"}),"debit warning missing or wrong");Released(engine,player,first,second);
  });
  Test("world clear logs each debit despite release exception","multiple sources and player leave no hold, lock or block",()=>{
   var player=Player(Debit("Wood",2));player.PlacementRefused=true;player.Inventory.FailAdd=true;var first=Chest("first",3,3);first.Inventory.FailAdd=true;first.FailRelease=true;var second=Chest("second",3,3);second.Inventory.FailAdd=true;var engine=Engine();Build(engine,player,first,second);Check(engine.Compensating,"no compensation to clear");engine.Clear();
   Check(warnings.SequenceEqual(new[]{"fast path debit unrestored key=first items=Wood=3","fast path debit unrestored key=second items=Wood=3","fast path debit unrestored key=player items=Wood=2","fast path world hold cleanup failed key=first: integration unblock refused"}),"world debit warnings wrong");
   Check(warnings.Any(message=>message.Contains("world hold cleanup failed key=first"))&&first.ForcedReleases==1&&second.ForcedReleases==1,"release failure not handled");Released(engine,player,first,second);engine.Clear();Released(engine,player,first,second);
  });
  foreach(bool description in new[]{false,true})Test("tick "+(description?"description":"bytes")+" fault retries without escaping","unexpected adapter errors warn once and retain the transaction",()=>{
   var player=Player();player.PlacementRefused=true;var chest=Chest("one",3,3);chest.Inventory.FailAdd=true;var before=chest.Bytes;var messages=new List<string>();var engine=new FastPathCore(message=>warnings.Add(message),message=>messages.Add(message));Build(engine,player,chest);messages.Clear();
   chest.FailBytes=!description;chest.FailDescription=description;if(description)chest.RevisionId++;string warning="fast path tick retry pending: "+(description?"debit description unavailable":"inventory bytes unavailable");
   for(int frame=0;frame<3;frame++){engine.Tick();Check(engine.Compensating&&engine.Blocks(player)&&chest.Held,"fault lost pending transaction");}
   Check(warnings.SequenceEqual(new[]{warning})&&messages.SequenceEqual(new[]{warning,warning}),"unexpected retry diagnostics wrong");chest.FailBytes=chest.FailDescription=chest.Inventory.FailAdd=false;engine.Tick();
   Check(description?warnings.SequenceEqual(new[]{warning,"fast path debit unrestored key=one items=Wood=3"}):warnings.SequenceEqual(new[]{warning})&&chest.Bytes.SequenceEqual(before),"fault recovery outcome wrong");Released(engine,player,chest);
  });
  foreach(bool warningFault in new[]{false,true})Test("world clear survives "+(warningFault?"warning":"description")+" fault","one diagnostic failure cannot skip any source or player hold",()=>{
   var player=Player(Debit("Wood",2));player.PlacementRefused=true;player.Inventory.FailAdd=true;var first=Chest("first",3,3);var second=Chest("second",3,3);first.Inventory.FailAdd=second.Inventory.FailAdd=true;
   var engine=new FastPathCore(message=>{if(warningFault&&message=="fast path debit unrestored key=first items=Wood=3")throw new InvalidOperationException("warning sink refused");warnings.Add(message);});Build(engine,player,first,second);first.FailDescription=!warningFault;engine.Clear();
   Check(warnings.SequenceEqual(new[]{"fast path world debit warning failed key=first: "+(warningFault?"warning sink refused":"debit description unavailable"),"fast path debit unrestored key=second items=Wood=3","fast path debit unrestored key=player items=Wood=2"}),"world failure diagnostics wrong");Check(first.ForcedReleases==1&&second.ForcedReleases==1,"source force release skipped");Released(engine,player,first,second);int count=warnings.Count;engine.Clear();Check(warnings.Count==count,"cleared state warned again");
  });
  Test("compensation keeps its source hold","the core keeps a failed restore pending and blocks the owning player",()=>{
   var player=Player();player.PlacementRefused=true;var chest=Chest("one",3,3);chest.Inventory.FailAdd=true;var engine=Engine();Build(engine,player,chest);
   Check(engine.Compensating&&engine.Blocks(player)&&chest.Held&&chest.InUse,"failed restore did not remain held");chest.Inventory.FailAdd=false;engine.Tick();Released(engine,player,chest);
  });
  foreach(bool save in new[]{false,true})Test("reentry during "+(save?"Save":"Apply"),"callbacks cannot debit or place again",()=>{
   var player=Player();var chest=Chest("one");var engine=Engine();bool called=false;Action reentry=()=>{called=true;Check(!engine.TryBuild(player,new[]{chest},player.Place,player.Finish),"callback build accepted");Check(FastPathCore.BuildEntry(engine.Blocks(player),engine.Running,true,engine.NativeReady(player))==FastPathBuildEntry.Refuse,"callback entry allowed");Check(FastPathCore.BuildEntry(false,engine.Running,false,false)==FastPathBuildEntry.Continue,"other player blocked");};
   if(save)chest.OnSave=reentry;else chest.OnApply=reentry;Build(engine,player,chest);Check(called&&player.Outputs==1&&player.Costs==1,"callback not covered or duplicate output");Released(engine,player,chest);
  });
  Test("released chest may change during cleanup retry","a released source is skipped while another source still needs release",()=>{
   var player=Player();var first=Chest("first");var second=Chest("second");second.FailRelease=true;var engine=Engine();Build(engine,player,first,second);
   Check(engine.Cleaning&&engine.Blocks(player)&&!engine.Active&&!first.Held&&second.Held,"cleanup did not block owning player");
   Check(FastPathCore.BuildEntry(engine.Blocks(player),engine.Running,true,engine.NativeReady(player))==FastPathBuildEntry.Refuse,"cleanup entry allowed");first.Inventory.Items.Add(Item("Iron",1,0,"external"));first.Inventory.Changed();int releases=first.Releases;var bytes=first.Bytes;second.FailRelease=false;engine.Tick();
   Check(first.Releases==releases&&first.Bytes.SequenceEqual(bytes)&&player.Outputs==1&&player.Costs==1,"released source revisited or payment repeated");Released(engine,player,first,second);
  });
  Test("output observed before throw retains payment","an exception after spawning still charges exactly once",()=>{
   var player=Player();player.ThrowAfterOutput=true;var chest=Chest("one");chest.FailRelease=true;var engine=Engine();Build(engine,player,chest);Check(player.Outputs==1&&player.Costs==1&&chest.Inventory.Count("Wood")==1&&engine.Cleaning&&!engine.Active,"output exception rolled back or missed costs");chest.FailRelease=false;engine.Tick();Check(player.Costs==1,"cleanup repeated costs");Released(engine,player,chest);
  });
  Test("cost callback exception is not retried","a partial cost charge cannot double-charge on cleanup",()=>{
   var player=Player();player.FailFinish=true;var chest=Chest("one");chest.FailRelease=true;var engine=Engine();Build(engine,player,chest);chest.FailRelease=false;engine.Tick();Check(player.Outputs==1&&player.Costs==1&&player.Stamina==95,"costs repeated or output lost");Check(warnings.Any(message=>message.Contains("build cost incomplete")),"cost failure was silent");Released(engine,player,chest);
  });
  Test("placement flag clears on compensation and cleanup","the glue callback matches Active only during the native call",()=>{
   foreach(bool output in new[]{false,true}){var player=Player();player.PlacementRefused=!output;var chest=Chest("one",3,3);chest.Inventory.FailAdd=!output;chest.FailRelease=output;var engine=Engine();Prepare(player,chest);bool active=false;
    engine.TryBuild(player,new[]{chest},()=>{Check(active&&engine.Active,"placement flag absent");return player.Place();},player.Finish,()=>player.Outputs>0,placing=>active=placing);
    Check(engine.Running&&!engine.Active&&!active&&engine.Blocks(player),"placement flag survived return");chest.Inventory.FailAdd=false;chest.FailRelease=false;engine.Tick();Released(engine,player,chest);
   }
  });
  Test("reduced stock is visible after debit","a coordinated reader cannot spend the pre-debit stock",()=>{var player=Player();var chest=Chest("one");var engine=Engine();Prepare(player,chest);bool observed=false;engine.TryBuild(player,new[]{chest},()=>{observed=true;Check(chest.Inventory.Count("Wood")==1&&chest.Stored.SequenceEqual(chest.Bytes),"debit not visible to reader");return player.Place();},player.Finish);Check(observed&&player.Outputs==1,"reader callback not reached");Released(engine,player,chest);});
  foreach(bool order in new[]{false,true})Test("source restore "+(order?"order":"bytes")+" mismatch ends compensation","a completed delta with a permanent mismatch warns once and never restores again",()=>{
   var player=Player();player.PlacementRefused=true;var chest=Chest("one");chest.FailOrderAfterRestore=order;chest.CorruptAfterRestore=!order;var engine=Engine();Build(engine,player,chest);
   Check(chest.RestoreComplete&&warnings.SequenceEqual(new[]{"fast path restore mismatch key=one"}),"mismatch warning missing");Released(engine,player,chest);int restores=chest.Restores;engine.Tick();Check(chest.Restores==restores&&warnings.Count==1,"permanent mismatch retried");
  });
  foreach(bool order in new[]{false,true})Test("player restore "+(order?"order":"bytes")+" mismatch ends compensation","a completed player delta releases the hold and warns once",()=>{
   var player=Player(Debit("Wood",2));player.PlacementRefused=true;player.FailOrderAfterRestore=order;player.CorruptAfterRestore=!order;var chest=Chest("one");var engine=Engine();Build(engine,player,chest);
   Check(player.RestoreComplete&&warnings.SequenceEqual(new[]{"fast path restore mismatch key=player"}),"player warning missing");Released(engine,player,chest);int restores=player.Restores;engine.Tick();Check(player.Restores==restores&&warnings.Count==1,"player mismatch retried");
  });
  Test("empty player debit releases after mismatch","an empty remaining debit reports the inventory mismatch",()=>{
   var player=Player();player.PlacementRefused=true;player.CorruptAfterRestore=true;var chest=Chest("one");var engine=Engine();Build(engine,player,chest);Check(warnings.SequenceEqual(new[]{"fast path restore mismatch key=player"}),"empty debit mismatch warning missing");Released(engine,player,chest);
  });
  Test("player AddItem retry restores once","an incomplete player refund remains pending until the next successful restore",()=>{
   var player=Player(Debit("Wood",2));var before=player.Bytes;player.PlacementRefused=true;player.Inventory.FailAdd=true;var chest=Chest("one");var engine=Engine();Build(engine,player,chest);Check(engine.Compensating&&engine.Blocks(player)&&player.Held&&!chest.Held&&warnings.Count==0,"player retry did not remain held");player.Inventory.FailAdd=false;engine.Tick();Check(player.Bytes.SequenceEqual(before)&&player.Inventory.Count("Wood")==2,"player refund duplicated or lost");Released(engine,player,chest);
  });
  foreach(bool playerSource in new[]{false,true})foreach(bool mismatch in new[]{false,true})Test((playerSource?"player":"source")+" duplicate slots "+(mismatch?"mismatch":"matching bytes"),"completed deltas finish according to bytes even if order restoration throws",()=>{
   var player=Player(playerSource?Debit("Wood",1):null);player.PlacementRefused=true;var chest=Chest("one");var inventory=playerSource?player.Inventory:chest.Inventory;inventory.Items.Add(Item("Iron",1,inventory.Items[0].Slot,"duplicate"));
   if(mismatch){Action corrupt=()=>inventory.Items[0].Metadata="changed";if(playerSource)player.AfterRestore=corrupt;else chest.AfterRestore=corrupt;}
   var engine=Engine();Build(engine,player,chest);Check(player.RestoreComplete&&chest.RestoreComplete,"delta incomplete");Check(warnings.SequenceEqual(mismatch?new[]{"fast path restore mismatch key="+(playerSource?"player":"one")}:Array.Empty<string>()),"duplicate-slot warning wrong");Released(engine,player,chest);int restores=playerSource?player.Restores:chest.Restores;engine.Tick();Check((playerSource?player.Restores:chest.Restores)==restores,"duplicate-slot restore retried");
  });
  foreach(bool playerSource in new[]{false,true})Test((playerSource?"player":"source")+" complete restore throw keeps matching bytes","exception type does not repeat an already completed restore",()=>{
   var player=Player(playerSource?Debit("Wood",1):null);player.PlacementRefused=true;var chest=Chest("one");Action fail=()=>throw new ArgumentException("after completed restore");if(playerSource)player.AfterRestore=fail;else chest.AfterRestore=fail;var engine=Engine();Build(engine,player,chest);Released(engine,player,chest);Check(warnings.Count==0,"matching restore warned");int restores=playerSource?player.Restores:chest.Restores;engine.Tick();Check((playerSource?player.Restores:chest.Restores)==restores,"completed restore retried");
  });
  foreach(string stage in new[]{"restore","save","release"})Test(stage+" retry warns once "+(stage=="release"?"on first failure":"after later failure"),"three failed frames produce one source warning and retain the retry",()=>{
   var player=Player();player.PlacementRefused=stage!="release";var chest=Chest("one",3,3);if(stage=="restore")chest.Inventory.FailAdd=true;else if(stage=="save")chest.FailSaveBefore=true;else chest.FailRelease=true;var engine=Engine();Build(engine,player,chest);Check(warnings.Count==(stage=="release"?1:0),"first attempt warning wrong");int restores=chest.Restores,releases=chest.Releases;if(stage=="release")Check(releases==1&&chest.Held&&chest.InUse&&chest.IntegrationBlocked,"failed unblock dropped hold");
   for(int frame=0;frame<3;frame++){engine.Tick();if(stage=="release")Check(chest.Releases==releases+frame+1&&chest.Held&&chest.InUse&&chest.IntegrationBlocked,"failed tick did not retry blocked integration");}
   string error=stage=="restore"?"AddItem refused":stage=="save"?"save refused before write":"integration unblock refused";Check(warnings.SequenceEqual(new[]{"fast path "+stage+" pending key=one: "+error}),"pending warning wrong");if(stage=="save")Check(chest.Restores==restores,"save retry repeated restore");chest.Inventory.FailAdd=chest.FailSaveBefore=chest.FailRelease=false;engine.Tick();if(stage=="release")Check(chest.Releases==releases+4&&!chest.IntegrationBlocked,"successful tick did not unblock integration");Released(engine,player,chest);
  });
  foreach(bool release in new[]{false,true})Test("player "+(release?"release":"restore")+" retry warns once","player retry errors name the player once across three frames",()=>{
   var player=Player(Debit("Wood",2));player.PlacementRefused=true;player.Inventory.FailAdd=!release;player.FailRelease=release;var chest=Chest("one");var engine=Engine();Build(engine,player,chest);Check(warnings.Count==(release?1:0),"initial player retry warning wrong");for(int frame=0;frame<3;frame++)engine.Tick();Check(warnings.SequenceEqual(new[]{"fast path "+(release?"release":"restore")+" pending key=player: "+(release?"player unblock refused":"AddItem refused")}),"player pending warning wrong");player.Inventory.FailAdd=player.FailRelease=false;engine.Tick();Released(engine,player,chest);
  });
  Test("first later restore succeeds without warning","resolved transient failures do not warn",()=>{var player=Player();player.PlacementRefused=true;var chest=Chest("one",3,3);chest.Inventory.FailAdd=true;var engine=Engine();Build(engine,player,chest);chest.Inventory.FailAdd=false;engine.Tick();Check(warnings.Count==0,"resolved retry warned");Released(engine,player,chest);});
  Test("empty remaining fence and world clear warn exactly once","abandonments report mismatches when the delta already returned its items",()=>{
   foreach(bool clear in new[]{false,true}){warnings.Clear();var player=Player();var chest=Chest("one",3,3);chest.FailSaveBefore=true;var engine=Engine();Build(engine,player,chest);Check(chest.RestoreComplete&&engine.Compensating,"completed refund not waiting on save");if(clear)engine.Clear();else{chest.RevisionId++;engine.Tick();}Check(warnings.SequenceEqual(new[]{"fast path restore mismatch key=one"}),"empty remaining abandon warning wrong");Released(engine,player,chest);}
  });
  Test("player fence abandonment names remaining items","changed player bytes abandon only the missing contribution",()=>{var player=Player(Debit("Wood",2));player.PlacementRefused=true;player.Inventory.FailAdd=true;var chest=Chest("one");var engine=Engine();Build(engine,player,chest);player.Inventory.Items.Add(Item("Iron",1,0,"external"));engine.Tick();Check(warnings.SequenceEqual(new[]{"fast path debit unrestored key=player items=Wood=2"}),"player fence warning wrong");Released(engine,player,chest);});
  Console.WriteLine("RESULT "+passed+" fast path runtime tests passed; "+failed+" failed; production FastPathCore, game adapters are stand-ins.");return failed==0?0:1;
 }
}
#endif
