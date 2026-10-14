using System;
using System.Collections.Generic;
using System.Linq;

namespace RunicStorageNetwork {
 internal enum FastPathBuildEntry { Continue, Native, Refuse }
 internal sealed class FastPathFenceException:InvalidOperationException {
  internal FastPathFenceException(string message):base(message){}
 }
 internal sealed class FastPathPermanentRestoreException:InvalidOperationException {
  internal FastPathPermanentRestoreException(string message,Exception inner=null):base(message,inner){}
 }
 internal sealed class FastPathSourceState {
  internal bool Valid,SceneOwner,Replica,Access,InUse,NetworkInUse,Locked,Reserved,Busy,Held,Fresh;
  internal long Owner,LocalOwner;
 }
 internal abstract class FastPathCoreSource {
  internal abstract string Key {get;}
  internal abstract long Owner {get;}
  internal abstract long CurrentOwner {get;}
  internal abstract uint Revision {get;}
  internal abstract bool IsValid {get;}
  internal abstract byte[] Bytes {get;}
  internal abstract string DebitDescription {get;}
  internal abstract bool RestoreComplete {get;}
  internal abstract void AcquireHold();
  internal abstract void ApplyDebit();
  internal abstract void Save();
  internal abstract void RestoreDebit();
  internal abstract void ReleaseHold();
  internal abstract void ClearHold();
 }
 internal abstract class FastPathCorePlayer {
  internal abstract byte[] Bytes {get;}
  internal abstract string DebitDescription {get;}
  internal abstract bool RestoreComplete {get;}
  internal abstract void AcquireHold();
  internal abstract void ApplyDebit();
  internal abstract void RestoreDebit();
  internal abstract void ReleaseHold();
  internal abstract void ClearHold();
 }
 internal sealed class FastPathCore {
  enum RestoreState { Pending,Restored,Mismatch }
  sealed class HeldSource {
   internal FastPathCoreSource Source;
   internal long ExpectedOwner;
   internal uint ExpectedRevision;
   internal byte[] Before,ExpectedBytes;
   internal bool Applied,Restored,Released,RestoreWarned,SaveWarned,ReleaseWarned;
  }
  sealed class Transaction {
   internal FastPathCorePlayer Player;
   internal List<HeldSource> Sources;
   internal byte[] PlayerBefore,PlayerExpected;
   internal bool PlayerApplied,PlayerHeld,Compensating,Cleaning,Output,Charged,PlayerRestoreWarned,PlayerReleaseWarned,TickWarned;
   internal Action Finish;
   internal Action<bool> PlacementChanged;
  }
  readonly Action<string> warning,debug;
  Transaction current;
  bool executing,nativeCall,ticking;

  internal FastPathCore(Action<string> warning,Action<string> debug=null){this.warning=warning??throw new ArgumentNullException(nameof(warning));this.debug=debug??(_=>{});}
  internal bool Running=>current!=null;
  internal bool Active=>executing;
  internal bool Compensating=>current!=null&&current.Compensating;
  internal bool Cleaning=>current!=null&&current.Cleaning;
  internal bool Blocks(FastPathCorePlayer player)=>current!=null&&current.Player==player&&!executing;
  internal bool NativeReady(FastPathCorePlayer player)=>current!=null&&current.Player==player&&nativeCall;
  internal bool ConsumeNativePlacement(FastPathCorePlayer player){if(!NativeReady(player))return false;nativeCall=false;return true;}
  internal static FastPathBuildEntry BuildEntry(bool blocked,bool running,bool owns,bool native){
   if(!owns)return FastPathBuildEntry.Continue;
   if(blocked)return FastPathBuildEntry.Refuse;
   if(running)return native?FastPathBuildEntry.Native:FastPathBuildEntry.Refuse;
   return FastPathBuildEntry.Continue;
  }
  internal static bool SourceCanReload(FastPathSourceState source)=>source!=null&&source.Valid&&source.SceneOwner&&source.Owner==source.LocalOwner&&source.Access&&!source.Replica&&!source.InUse&&!source.NetworkInUse&&!source.Locked&&!source.Reserved&&!source.Busy&&!source.Held;
  internal static bool SourceEligible(FastPathSourceState source)=>SourceCanReload(source)&&source.Fresh;
  internal static bool PlayerEligible(bool enabled,bool local,bool carried,bool inventoryLocked,bool toolLocked,bool waiting,bool active,bool running)=>enabled&&local&&!carried&&!inventoryLocked&&!toolLocked&&!waiting&&!active&&!running;
  internal static bool PlanSourcesMatch(IEnumerable<string> selected,IEnumerable<string> actual){
   if(selected==null||actual==null)return false;
   var selectedSet=new HashSet<string>(selected,StringComparer.Ordinal);var actualSet=new HashSet<string>(actual,StringComparer.Ordinal);
   return selectedSet.Count>0&&selectedSet.All(key=>!string.IsNullOrEmpty(key)&&key!="player")&&selectedSet.SetEquals(actualSet);
  }
  internal static List<T> RestoreOrder<T>(IEnumerable<T> items,IReadOnlyList<int> before,Func<T,int> slot){
   var ordered=items.ToList();var positions=new Dictionary<int,int>();
   for(int index=0;index<before.Count;index++){if(positions.ContainsKey(before[index]))throw new FastPathPermanentRestoreException("duplicate restored inventory slot");positions.Add(before[index],index);}
   if(ordered.Count!=before.Count||!new HashSet<int>(ordered.Select(slot)).SetEquals(before))throw new FastPathPermanentRestoreException("restored inventory slots changed");
   return ordered.OrderBy(item=>positions[slot(item)]).ToList();
  }

  internal bool TryBuild(FastPathCorePlayer player,IReadOnlyList<FastPathCoreSource> sources,Func<bool> place,Action finish,Func<bool> outputObserved=null,Action<bool> placementChanged=null){
   if(player==null||sources==null||sources.Count==0||sources.Any(source=>source==null)||place==null||finish==null||current!=null)return false;
   Transaction transaction;
   try{var before=Copy(player.Bytes);transaction=new Transaction{Player=player,Sources=new List<HeldSource>(),PlayerBefore=before,PlayerExpected=Copy(before),Finish=finish,PlacementChanged=placementChanged};}
   catch(Exception){return false;}
   current=transaction;bool wrote=false;
   try {
    transaction.PlayerHeld=true;player.AcquireHold();
    foreach(var source in sources){
     var before=Copy(source.Bytes);var held=new HeldSource{Source=source,ExpectedOwner=source.Owner,ExpectedRevision=source.Revision,Before=before,ExpectedBytes=Copy(before)};
     transaction.Sources.Add(held);source.AcquireHold();
    }
    foreach(var held in transaction.Sources){Check(held);held.Applied=true;wrote=true;Mutate(held);}
    CheckPlayer(transaction);transaction.PlayerApplied=true;wrote=true;
    try{player.ApplyDebit();}finally{transaction.PlayerExpected=Copy(player.Bytes);}
    executing=true;nativeCall=true;
    try{transaction.PlacementChanged?.Invoke(true);transaction.Output=place();}
    finally{try{transaction.Output|=outputObserved?.Invoke()??false;}finally{nativeCall=false;executing=false;transaction.PlacementChanged?.Invoke(false);}}
    if(!transaction.Output)throw new InvalidOperationException("placement refused");
    Charge(transaction);transaction.Cleaning=true;Cleanup(transaction);
   }catch(Exception error){
    try{debug("fast path transaction: "+error.Message);}catch(Exception logging){transaction.TickWarned=ReportFailure("fast path transaction logging failed: "+logging.Message,transaction.TickWarned);}
    // A failed first refund must keep its hold for the next frame.
    try {
     if(transaction.Output){
      transaction.Cleaning=true;try{Charge(transaction);}catch(Exception charge){warning("fast path build cost incomplete: "+charge.Message);}
      Cleanup(transaction);
     }else{transaction.Compensating=true;Compensate(transaction);}
    }catch(Exception retry){current=transaction;transaction.TickWarned=ReportFailure("fast path first-frame retry pending: "+retry.Message,transaction.TickWarned);}
   }
   return wrote||current!=null;
  }

  static byte[] Copy(byte[] bytes){if(bytes==null)throw new InvalidOperationException("inventory bytes missing");return (byte[])bytes.Clone();}
  void Charge(Transaction transaction){if(transaction.Charged)return;transaction.Charged=true;try{transaction.Finish();}catch(Exception error){warning("fast path build cost incomplete: "+error.Message);throw;}}
  bool FenceMatches(HeldSource held)=>held.Source.IsValid&&held.Source.CurrentOwner==held.ExpectedOwner&&held.Source.Revision==held.ExpectedRevision&&held.Source.Bytes.SequenceEqual(held.ExpectedBytes);
  void Check(HeldSource held){if(!FenceMatches(held))throw new FastPathFenceException("source fence changed: "+held.Source.Key);}
  void CheckOwner(HeldSource held){if(!held.Source.IsValid||held.Source.CurrentOwner!=held.ExpectedOwner)throw new FastPathFenceException("source owner changed: "+held.Source.Key);}
  void Recapture(HeldSource held){held.ExpectedBytes=Copy(held.Source.Bytes);held.ExpectedRevision=held.Source.Revision;}
  void CaptureAfterMutation(HeldSource held){if(held.Source.IsValid&&held.Source.CurrentOwner==held.ExpectedOwner)Recapture(held);}
  void Mutate(HeldSource held){
   try {
    held.Source.ApplyDebit();
    // Vanilla change-save advances the revision inside the mutation.
    CheckOwner(held);held.Source.Save();Recapture(held);
   }catch{CaptureAfterMutation(held);throw;}
  }
  void CheckPlayer(Transaction transaction){if(!transaction.Player.Bytes.SequenceEqual(transaction.PlayerExpected))throw new FastPathFenceException("player contents changed");}
  void WarnDebit(string key,string description){warning(string.IsNullOrEmpty(description)?"fast path restore mismatch key="+key:"fast path debit unrestored key="+key+" items="+description);}
  static RestoreState Restored(bool complete,byte[] bytes,byte[] before)=>!complete?RestoreState.Pending:bytes.SequenceEqual(before)?RestoreState.Restored:RestoreState.Mismatch;
  void Pending(string key,string stage,Exception error,ref bool warned){string message="fast path "+stage+" pending key="+key+": "+error.Message;if((ticking||stage=="release")&&!warned){warned=true;warning(message);}else debug(message);}
  void Restore(HeldSource held){
   if(!held.Restored){
    Exception failure=null;try{held.Source.RestoreDebit();}catch(Exception error){failure=error;}finally{CaptureAfterMutation(held);}
    // Completion and bytes decide the outcome even when an adapter throws.
    var state=Restored(held.Source.RestoreComplete,held.Source.Bytes,held.Before);
    if(state==RestoreState.Pending){Pending(held.Source.Key,"restore",failure??new InvalidOperationException("delta restore incomplete"),ref held.RestoreWarned);return;}
    if(state==RestoreState.Mismatch){WarnDebit(held.Source.Key,held.Source.DebitDescription);held.Applied=false;return;}
    held.Restored=true;
   }
   try{CheckOwner(held);held.Source.Save();Recapture(held);held.Applied=false;}
   catch(Exception error){CaptureAfterMutation(held);Pending(held.Source.Key,"save",error,ref held.SaveWarned);}
  }
  void RestorePlayer(Transaction transaction){
   Exception failure=null;try{transaction.Player.RestoreDebit();}catch(Exception error){failure=error;}finally{transaction.PlayerExpected=Copy(transaction.Player.Bytes);}
   var state=Restored(transaction.Player.RestoreComplete,transaction.PlayerExpected,transaction.PlayerBefore);
   if(state==RestoreState.Pending){Pending("player","restore",failure??new InvalidOperationException("delta restore incomplete"),ref transaction.PlayerRestoreWarned);return;}
   if(state==RestoreState.Mismatch)WarnDebit("player",transaction.Player.DebitDescription);
   transaction.PlayerApplied=false;
  }
  void Compensate(Transaction transaction){
   if(transaction.PlayerApplied){
    if(!transaction.Player.Bytes.SequenceEqual(transaction.PlayerExpected)){WarnDebit("player",transaction.Player.DebitDescription);transaction.PlayerApplied=false;}
    else RestorePlayer(transaction);
   }
   if(!transaction.PlayerApplied&&transaction.PlayerHeld){try{ReleasePlayer(transaction);}catch(Exception error){Pending("player","release",error,ref transaction.PlayerReleaseWarned);}}
   for(int index=transaction.Sources.Count-1;index>=0;index--){var held=transaction.Sources[index];if(!held.Applied)continue;
    if(!FenceMatches(held)){WarnDebit(held.Source.Key,held.Source.DebitDescription);held.Applied=false;}
    else Restore(held);
    if(!held.Applied){try{Release(held);}catch(Exception error){Pending(held.Source.Key,"release",error,ref held.ReleaseWarned);}}
   }
   if(transaction.PlayerApplied||transaction.Sources.Any(held=>held.Applied))return;
   transaction.Compensating=false;transaction.Cleaning=true;Cleanup(transaction);
  }
  void Release(HeldSource held){if(held.Released)return;held.Source.ReleaseHold();held.Released=true;}
  void ReleasePlayer(Transaction transaction){if(!transaction.PlayerHeld)return;transaction.Player.ReleaseHold();transaction.PlayerHeld=false;}
  void Cleanup(Transaction transaction){
   foreach(var held in transaction.Sources.Where(held=>!held.Released)){
    try{Release(held);}catch(Exception error){Pending(held.Source.Key,"release",error,ref held.ReleaseWarned);}
   }
   if(transaction.Sources.Any(held=>!held.Released))return;
   try{ReleasePlayer(transaction);}catch(Exception error){Pending("player","release",error,ref transaction.PlayerReleaseWarned);return;}
   current=null;
  }
  bool ReportFailure(string message,bool warned=false){
   try{if(warned)debug(message);else{warning(message);return true;}}
   catch(Exception error){try{debug("fast path logging failed: "+error.Message);}catch(Exception){return warned;}}
   return warned;
  }
  internal void Tick(){
   var transaction=current;if(transaction==null)return;ticking=true;
   try{if(transaction.Compensating)Compensate(transaction);else if(transaction.Cleaning)Cleanup(transaction);}
   catch(Exception error){current=transaction;transaction.TickWarned=ReportFailure("fast path tick retry pending: "+error.Message,transaction.TickWarned);}
   finally{ticking=false;}
  }
  internal void Clear(){
   var transaction=current;if(transaction==null)return;
   try {
    if(!transaction.Output){
     foreach(var held in transaction.Sources.Where(held=>held.Applied)){
      string key="unknown";try{key=held.Source.Key;WarnDebit(key,held.Source.DebitDescription);}catch(Exception error){ReportFailure("fast path world debit warning failed key="+key+": "+error.Message);}
     }
     if(transaction.PlayerApplied){try{WarnDebit("player",transaction.Player.DebitDescription);}catch(Exception error){ReportFailure("fast path world debit warning failed key=player: "+error.Message);}}
    }
    foreach(var held in transaction.Sources.Where(held=>!held.Released)){
     string key="unknown";try{key=held.Source.Key;}catch(Exception error){ReportFailure("fast path world source key failed: "+error.Message);}
     try{held.Source.ClearHold();}catch(Exception error){ReportFailure("fast path world hold cleanup failed key="+key+": "+error.Message);}finally{held.Released=true;}
    }
    try{transaction.Player.ClearHold();}catch(Exception error){ReportFailure("fast path world player cleanup failed: "+error.Message);}
   }finally{nativeCall=false;executing=false;current=null;try{transaction.PlacementChanged?.Invoke(false);}catch(Exception error){ReportFailure("fast path world placement cleanup failed: "+error.Message);}}
  }
 }
}
