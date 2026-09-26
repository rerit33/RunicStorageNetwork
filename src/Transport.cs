using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RunicStorageNetwork.Logic;

namespace RunicStorageNetwork {
 internal sealed class Operation {
  internal string Id,Target,Network;internal bool Build,Quote;internal int Quality,Multiplier;
  internal ZDOID Actor,Station,Core;internal long Peer,PlayerId;
  internal List<Need> Needs;internal List<Stock> PlayerStock=new List<Stock>();
  internal string[] Sources=Array.Empty<string>();
  internal ZDOID[] Nodes=Array.Empty<ZDOID>();
  // Owners receive an authenticated server request. They verify the recipe and
  // their physical chest; only the server needs the complete actor/network view.
  internal bool ReadRequirements(out string reason){
   reason="recipe unavailable";Piece.Requirement[] requirements;
   if(Build){
    var prefab=ZNetScene.instance.GetPrefab(Target);var piece=prefab?prefab.GetComponent<Piece>():null;
    var hammer=ObjectDB.instance.GetItemPrefab("Hammer")?.GetComponent<ItemDrop>();
    if(!piece||!piece.m_enabled||!hammer||!hammer.m_itemData.m_shared.m_buildPieces.m_pieces.Contains(prefab)||Quality!=0||Multiplier!=1)return false;
    requirements=piece.m_resources;
   }else{
    var recipe=RecipeIndex.Find(Target);
    if(!recipe||Quality<1||Quality>recipe.m_item.m_itemData.m_shared.m_maxQuality)return false;requirements=recipe.m_resources;
   }
   Needs=Stockroom.Requirements(requirements,Quality,Multiplier);reason="invalid requirements";
   return Needs.Count>0&&Needs.Count<=32&&Needs.All(n=>n.Amount<=100000);
  }
  internal ZPackage Write(bool includeSources=false){var p=new ZPackage();p.Write(Id);p.Write(Target);p.Write(Build);p.Write(Quote);p.Write(Quality);p.Write(Multiplier);p.Write(Actor);p.Write(Station);p.Write(Core);p.Write(Network);p.Write(Peer);p.Write(PlayerId);p.Write(Nodes.Length);foreach(var node in Nodes)p.Write(node);Wire.Stocks(p,PlayerStock);p.Write(includeSources?Sources.Length:0);if(includeSources)foreach(string source in Sources)p.Write(source);return p;}
  internal static Operation Read(ZPackage p){var o=new Operation{Id=p.ReadString(),Target=p.ReadString(),Build=p.ReadBool(),Quote=p.ReadBool(),Quality=p.ReadInt(),Multiplier=p.ReadInt(),Actor=p.ReadZDOID(),Station=p.ReadZDOID(),Core=p.ReadZDOID(),Network=p.ReadString(),Peer=p.ReadLong(),PlayerId=p.ReadLong()};int nodeCount=p.ReadInt();if(nodeCount<1||nodeCount>4096)throw new InvalidOperationException("Node limit");o.Nodes=new ZDOID[nodeCount];var nodeIds=new HashSet<ZDOID>();for(int i=0;i<nodeCount;i++){var node=p.ReadZDOID();if(!nodeIds.Add(node))throw new InvalidOperationException("Duplicate node");o.Nodes[i]=node;}o.PlayerStock=Wire.Stocks(p);int count=p.ReadInt();if(count<0||count>SourceSelection.MaxEntries)throw new InvalidOperationException("Source limit");o.Sources=new string[count];var unique=new HashSet<string>(StringComparer.Ordinal);for(int i=0;i<count;i++){string source=p.ReadString();if(source.Length==0||source.Length>80||source=="player"||!unique.Add(source))throw new InvalidOperationException("Invalid source selection");o.Sources[i]=source;}if((o.Quote&&o.Build)||!System.Guid.TryParseExact(o.Id,"N",out _)||o.Target.Length>160||o.Network.Length>80||o.Multiplier<1||o.Multiplier>100||o.Quality<0||o.Quality>100)throw new InvalidOperationException("Malformed operation");return o;}
  internal bool Validate(out Player player,out Core core,out string reason,bool forceTopology=true){
   reason="supply unavailable";player=null;core=null;if(!Plugin.Enabled||!ZNetScene.instance)return false;
   player=ZNetScene.instance.FindInstance(Actor)?.GetComponent<Player>();reason="actor unavailable";
   if(!player||!R.Valid(R.View(player))||R.View(player).GetZDO().GetOwner()!=Peer||player.GetPlayerID()!=PlayerId||player.IsDead())return false;
   if(ZNet.instance.IsServer()&&Peer!=ZNet.GetUID()&&ZNet.instance.GetPeer(Peer)?.m_characterID!=Actor){reason="sender does not own character";return false;}
   Vector3 point=player.transform.position;Piece.Requirement[] req;
   if(Build){
    var prefab=ZNetScene.instance.GetPrefab(Target);var piece=prefab?prefab.GetComponent<Piece>():null;
    var hammer=ObjectDB.instance.GetItemPrefab("Hammer")?.GetComponent<ItemDrop>();
    reason="invalid hammer piece";if(!piece||!piece.m_enabled||!hammer||!hammer.m_itemData.m_shared.m_buildPieces.m_pieces.Contains(prefab)||Quality!=0||Multiplier!=1)return false;
    reason="free building";if(ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()))return false;
    reason="missing build station";if(piece.m_craftingStation&&!ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench)&&!CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name,point))return false;
    req=piece.m_resources;
   }else{
    var recipe=RecipeIndex.Find(Target);reason="recipe unavailable";if(!recipe||Quality<1||Quality>recipe.m_item.m_itemData.m_shared.m_maxQuality)return false;
    var station=ZNetScene.instance.FindInstance(Station)?.GetComponent<CraftingStation>();reason="station unavailable";if(!station||station.m_upgrader||!station.InUseDistance(player)||!station.CheckUsable(player,false))return false;
    var required=recipe.GetRequiredStation(Quality);if(required&&(station.m_name!=required.m_name||station.GetLevel()<recipe.GetRequiredStationLevel(Quality)))return false;
    reason="free crafting";if(ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost))return false;
    // Alternate-ingredient recipes are selected deterministically after fresh owner snapshots.
    req=recipe.m_resources;point=station.transform.position;
   }
   Topology.Refresh(forceTopology);core=CoreObject(Core);reason="network path or coverage changed";if(!core||!NetworkGraph.MatchesRoot(core.GetComponent<NetworkMember>().SavedNetwork,Network)||!Topology.Supplies(core,point,PlayerId))return false;
   Needs=Stockroom.Requirements(req,Quality,Multiplier);
   reason="invalid requirements";if(Needs.Count==0||Needs.Count>32||Needs.Any(n=>n.Amount>100000))return false;
   foreach(var s in PlayerStock)if(s.Source!="player"||s.Amount<0||s.Amount>100000||!Needs.Any(n=>n.Item==s.Item)||s.Quality<1||s.Quality>100){reason="invalid character contribution";return false;}
   reason="ok";return true;
  }
  internal bool SelectNeeds(List<Stock> stock){
   // Resolved through the same index as Validate: a linear First() here could pick a
   // different recipe for an ambiguous name, or throw where the caller expects a refusal.
   var recipe=Build?null:RecipeIndex.Find(Target);
   if(!Build&&!recipe)return false;
   if(!Build&&recipe.m_requireOnlyOneIngredient){
    foreach(var n in Needs){var one=new List<Need>{new Need(n.Item,n.Amount)};if(Stockroom.Qualities(one,stock,true)){Needs=one;return true;}}return false;
   }
   return Stockroom.Qualities(Needs,stock,!Build);
  }
  internal static Core CoreObject(ZDOID id)=>ZNetScene.instance?ZNetScene.instance.FindInstance(id)?.GetComponent<Core>():null;
 }
 internal static class Wire {
  internal static void Stocks(ZPackage p,List<Stock> items){if(items.Count>SourceSelection.MaxEntries)throw new InvalidOperationException("Snapshot limit");p.Write(items.Count);foreach(var s in items){p.Write(s.Source);p.Write(s.Item);p.Write(s.Quality);p.Write(s.Amount);}}
  internal static List<Stock> Stocks(ZPackage p){int count=p.ReadInt();if(count<0||count>SourceSelection.MaxEntries)throw new InvalidOperationException("Snapshot limit");var r=new List<Stock>();for(int i=0;i<count;i++){var s=new Stock(p.ReadString(),p.ReadString(),p.ReadInt(),p.ReadInt());if(s.Source.Length>80||s.Item.Length>160||s.Amount<0||s.Amount>100000)throw new InvalidOperationException("Bad snapshot");r.Add(s);}return r;}
  internal static void Debits(ZPackage p,List<Debit> debits)=>Stocks(p,debits.Select(d=>new Stock(d.Source,d.Item,d.Quality,d.Amount)).ToList());
  internal static List<Debit> Debits(ZPackage p)=>Stocks(p).Select(s=>new Debit(s.Source,s.Item,s.Quality,s.Amount)).ToList();
 }
 internal sealed class Transport:MonoBehaviour {
  internal static Transport Instance;internal static int InternalMutation;
  sealed class ServerJob {internal Operation Op;internal List<ZDO> Containers;internal Dictionary<string,ZDO> SourcesById=new Dictionary<string,ZDO>();internal Dictionary<string,long> Owners=new Dictionary<string,long>();internal Dictionary<string,List<Stock>> Snapshots=new Dictionary<string,List<Stock>>();internal readonly HashSet<string> PaidSources=new HashSet<string>();internal List<Debit> Plan;internal Dictionary<string,List<Debit>> Debits;internal Decision Decision;internal Queue<string> Outgoing=new Queue<string>();internal float Started,LastReplay,QuoteUntil;internal bool Notified,ReadyOffered,QuoteClaimed;}
  internal sealed class Lease {internal Operation Op;internal Container Container;internal InventoryDelta Delta;internal string Key;internal List<Stock> Snapshot;internal bool Paid;}
  readonly Dictionary<string,ServerJob> jobs=new Dictionary<string,ServerJob>();
  internal static readonly Dictionary<Inventory,Lease> Leases=new Dictionary<Inventory,Lease>();
  readonly SourceGate sourceGate=new SourceGate();
  sealed class Queued {internal Operation Op;internal float Since;}
  sealed class Releasing {internal long Owner;internal float Since,LastSend;internal bool Warned,Rollback;}
  sealed class Refusal {internal Operation Op;internal string Reason;}
  readonly Dictionary<string,Refusal> terminal=new Dictionary<string,Refusal>();
  readonly Dictionary<string,Refusal> awaitingRelease=new Dictionary<string,Refusal>();
  readonly List<Queued> queued=new List<Queued>();
  readonly Dictionary<string,Dictionary<string,Releasing>> releasing=new Dictionary<string,Dictionary<string,Releasing>>();
  readonly HashSet<string> ended=new HashSet<string>();
  readonly HashSet<string> releasedLeases=new HashSet<string>();
  ZRoutedRpc rpc;ZNet world;float next;
  internal static long Server=>ZNet.instance.IsServer()?ZNet.GetUID():ZNet.instance.GetServerPeer()?.m_uid??0;
  void Awake(){Instance=this;}
  void Update(){
   if(world!=ZNet.instance){if(world||rpc!=null){foreach(var inventory in Leases.Keys)Integrations.Block(inventory,false);jobs.Clear();queued.Clear();releasing.Clear();terminal.Clear();awaitingRelease.Clear();sourceGate.Clear();Leases.Clear();ended.Clear();releasedLeases.Clear();Actions.Clear();}world=ZNet.instance;rpc=null;}
   if(!world||ZRoutedRpc.instance==null)return;
   if(rpc!=ZRoutedRpc.instance){rpc=ZRoutedRpc.instance;Register();}
   Pump();Dispatch();CraftOverview.Tick();CraftInspection.Tick();CraftPreparation.Tick();
   if(Time.unscaledTime<next)return;next=Time.unscaledTime+1;
   foreach(var j in jobs.Values.ToArray()){
    if(j.Op.Quote&&j.Decision.Phase==Phase.Prepared&&j.Plan!=null&&Time.unscaledTime>=j.QuoteUntil){Abort(j,"offer expired",false);continue;}
    if(!j.Notified&&!(j.Op.Quote&&j.Decision.Phase==Phase.Prepared&&j.Plan!=null)&&Time.unscaledTime-j.Started>10){j.Notified=true;if(j.Decision.Phase==Phase.Preparing||j.Decision.Phase==Phase.Prepared){Abort(j,"prepare timeout",true);continue;}Plugin.Critical(j.Op.Id,"Payment acknowledgement recovery exceeded 10 seconds; preserving the same transaction");}
    try{Replay(j);}catch(Exception e){Plugin.Debug(j.Op.Id+" acknowledgement retry: "+e.Message);}
   }
   foreach(var release in releasing.ToArray())foreach(var key in release.Value.ToArray()){
    if(!key.Value.Warned&&Time.unscaledTime-key.Value.Since>20){key.Value.Warned=true;Plugin.Critical(release.Key,"Release acknowledgement unresolved; source="+key.Key+" owner="+key.Value.Owner);}
    if(Time.unscaledTime-key.Value.LastSend>=2)SendRelease(release.Key,key.Key,key.Value);
   }
   Actions.Tick();
  }
  void Register(){
   foreach(var pair in new Dictionary<string,Action<long,ZPackage>>{{"request",Request},{"inspect",CraftInspection.Request},{"inspected",CraftInspection.Response},{"progress",Progress},{"offer",Offer},{"claim",Claim},{"accept",Accept},{"cancelquote",CancelQuote},{"prepare",Prepare},{"prepared",Prepared},{"commit",Commit},{"paid",Paid},{"ready",Ready},{"finish",Finish},{"release",Release},{"released",Released},{"fresh",Fresh},{"refused",Refused}}){var handler=pair.Value;rpc.Register<ZPackage>("RSN_"+pair.Key,(sender,p)=>{try{handler(sender,p);}catch(Exception e){Plugin.Error("RPC "+pair.Key,e);}});}
  }
  internal static void Send(long peer,string name,ZPackage package){if(peer==0)throw new InvalidOperationException("No coordinator");ZRoutedRpc.instance.InvokeRoutedRPC(peer,"RSN_"+name,package);}
  internal void Begin(Operation op){Send(Server,"request",op.Write(true));}
  static ZPackage Header(string id){var p=new ZPackage();p.Write(id);return p;}
  void Request(long sender,ZPackage p){
   if(!ZNet.instance.IsServer())return;var op=Operation.Read(p);op.Peer=sender;
   if(terminal.TryGetValue(op.Id,out var final)){if(final.Op.Peer==sender)SendRefusal(final.Op,final.Reason);return;}
   if(jobs.TryGetValue(op.Id,out var existing)){if(existing.Op.Peer==sender)Replay(existing);return;}
   if(ended.Contains(op.Id))return;
   var inQueue=queued.FirstOrDefault(q=>q.Op.Id==op.Id);if(inQueue!=null){if(inQueue.Op.Peer==sender)SendProgress(inQueue.Op,true);return;}
   if(jobs.Count+queued.Count+releasing.Count>=256||jobs.Values.Any(j=>j.Op.Peer==sender)||queued.Any(q=>q.Op.Peer==sender)){Reject(op,"previous operation pending");return;}
   if(!RemoteContext.Actor(op.Actor,sender,op.PlayerId,out _,out string why)){Reject(op,why);return;}
   queued.Add(new Queued{Op=op,Since=Time.unscaledTime});SendProgress(op,true);
  }
  void SendProgress(Operation op,bool inQueue){if(!op.Quote)return;var p=Header(op.Id);p.Write(inQueue);Send(op.Peer,"progress",p);}
  void Progress(long sender,ZPackage p){if(sender!=Server)return;string id=p.ReadString();CraftPreparation.Progress(id,p.ReadBool());}
  void Replay(ServerJob j){
   if(Time.unscaledTime-j.LastReplay<2)return;j.LastReplay=Time.unscaledTime;
   if(j.Op.Quote&&j.Decision.Phase==Phase.Prepared&&j.Plan!=null){SendOffer(j);return;}
   if(j.Decision.Phase==Phase.Paid){if(j.ReadyOffered)SendReady(j);else NotifyPaid(j);return;}
   foreach(string key in j.Owners.Keys){
    bool missing=j.Decision.Phase==Phase.Preparing?!j.Snapshots.ContainsKey(key):j.Decision.Phase==Phase.Committing&&!j.PaidSources.Contains(key);
    if(missing&&!j.Outgoing.Contains(key))j.Outgoing.Enqueue(key);
   }
  }
  void Pump(){
   if(!ZNet.instance.IsServer())return;
   // Preserve arrival order for overlapping sources, but allow independent
   // networks/chests to progress while an unrelated release is still pending.
   var blocked=new HashSet<string>(StringComparer.Ordinal);int started=0;
   foreach(var entry in queued.ToArray()){
    var op=entry.Op;
    if(Time.unscaledTime-entry.Since>15){queued.Remove(entry);ended.Add(op.Id);Reject(op,"queue timeout");continue;}
    if(op.Sources.Any(blocked.Contains)||!sourceGate.TryAcquire(op.Id,op.Sources)){blocked.UnionWith(op.Sources);continue;}
    queued.Remove(entry);
    try{StartJob(op);}catch(Exception e){Plugin.Error(op.Id+" coordinator start",e);if(jobs.TryGetValue(op.Id,out var job))Abort(job,"request validation failed",true);else{sourceGate.Cancel(op.Id);ended.Add(op.Id);Reject(op,"request validation failed");}}
    if(++started==4)break;
   }
  }
  void StartJob(Operation op){
   SendProgress(op,false);var context=new RemoteContext(op);
   if(!context.Validate(out string why)){sourceGate.Cancel(op.Id);ended.Add(op.Id);Reject(op,why);return;}
   var containers=new List<ZDO>();
   foreach(string key in op.Sources){var z=RemoteContext.Source(key);if(!context.SourceAllowed(z,out why)){sourceGate.Cancel(op.Id);ended.Add(op.Id);Reject(op,why);return;}containers.Add(z);}
   var job=new ServerJob{Op=op,Containers=containers,Started=Time.unscaledTime};
   foreach(var z in containers){string key=R.Key(z.m_uid);job.Owners.Add(key,z.GetOwner());job.SourcesById.Add(key,z);}
   job.Decision=new Decision(op.Id,job.Owners.Keys);jobs.Add(op.Id,job);
   Plugin.Debug(op.Id+" prepare "+(op.Build?"build":op.Quality>1?"upgrade":"craft")+" NetworkId="+op.Network+" core="+R.Key(op.Core));
   foreach(string key in job.Owners.Keys)job.Outgoing.Enqueue(key);
   if(containers.Count==0)Plan(job);
  }
  bool ValidateJob(ServerJob j,out string why){
   var context=new RemoteContext(j.Op);if(!context.Validate(out why))return false;
   foreach(var previous in j.Containers){var z=RemoteContext.Data(previous.m_uid);if(!context.SourceAllowed(z,out why))return false;if(z.GetOwner()!=j.Owners[R.Key(z.m_uid)]){why="ownership changed";return false;}}
   return true;
  }
  int dispatchOffset;
  void Dispatch(){
   // Bound outbound work per frame, including single-player RPCs, and rotate
   // jobs so a large payment cannot starve other players' small payments.
   var pending=jobs.Values.Where(j=>j.Outgoing.Count>0).ToArray();if(pending.Length==0)return;
   int budget=32,index=dispatchOffset%pending.Length,idle=0;
   while(budget>0&&idle<pending.Length){
    var j=pending[index];index=(index+1)%pending.Length;
    if(!jobs.ContainsKey(j.Op.Id)||j.Outgoing.Count==0){idle++;continue;}
    idle=0;budget--;string key=j.Outgoing.Dequeue();
    try {
     if(j.Decision.Phase==Phase.Preparing){
      if(!j.SourcesById.TryGetValue(key,out var c)||RemoteContext.Data(c.m_uid)==null||c.GetOwner()!=j.Owners[key]){Abort(j,"unloaded",true);continue;}
      var q=j.Op.Write();q.Write(c.m_uid);Send(j.Owners[key],"prepare",q);
     }else if(j.Decision.Phase==Phase.Committing){var q=Header(j.Op.Id);q.Write(key);Wire.Debits(q,j.Debits[key]);Send(j.Owners[key],"commit",q);}
     else continue;
    }catch(Exception e){if(e is InvalidOperationException)Plugin.Debug(j.Op.Id+" dispatch postponed: "+e.Message);else Plugin.Error(j.Op.Id+" dispatch",e);if(jobs.ContainsKey(j.Op.Id)&&j.Decision.Phase!=Phase.Paid)Abort(j,"dispatch failed",true);}
   }
   dispatchOffset=index;
  }
  void Prepare(long sender,ZPackage p){
   if(sender!=Server)return;var op=Operation.Read(p);var id=p.ReadZDOID();var c=ZNetScene.instance.FindInstance(id)?.GetComponent<Container>();var key=R.Key(id);var answer=Header(op.Id);answer.Write(key);
   if(releasedLeases.Contains(op.Id+key)){answer.Write(false);answer.Write("operation already released");Send(Server,"prepared",answer);return;}
   if(c&&c.GetInventory()!=null&&Leases.TryGetValue(c.GetInventory(),out var existing)&&existing.Op.Id==op.Id){answer.Write(true);Wire.Stocks(answer,existing.Snapshot);Send(Server,"prepared",answer);return;}
   var context=new RemoteContext(op);bool valid=op.ReadRequirements(out string why)&&context.OwnerSource(c,out why);
   if(valid){
    R.Call(c,"Load");var inventory=c.GetInventory();var lease=new Lease{Op=op,Container=c,Key=key,Snapshot=Stockroom.Snapshot(inventory,key,op.Needs,true)};
    Leases.Add(inventory,lease);R.Set(c,"m_inUse",true);R.View(c).GetZDO().Set(ZDOVars.s_inUse,1);R.View(c).GetZDO().Set("rsn_lease",op.Id);Integrations.Block(inventory,true);
    answer.Write(true);Wire.Stocks(answer,lease.Snapshot);
   }else {answer.Write(false);answer.Write(why??"owner unavailable");}
   Send(Server,"prepared",answer);
  }
  void Prepared(long sender,ZPackage p){
   if(!ZNet.instance.IsServer())return;string id=p.ReadString(),key=p.ReadString();bool ok=p.ReadBool();
   if(!jobs.TryGetValue(id,out var j)||!j.Owners.TryGetValue(key,out long owner)||owner!=sender||j.Decision.Phase!=Phase.Preparing)return;
   if(!ok){Abort(j,"owner refused: "+p.ReadString(),true);return;}
   var snapshot=Wire.Stocks(p);if(snapshot.Any(s=>s.Source!=key||!j.Op.Needs.Any(n=>n.Item==s.Item))){Abort(j,"invalid owner snapshot",true);return;}
   j.Snapshots[key]=snapshot;j.Decision.Prepared(key);if(j.Decision.Phase==Phase.Prepared)Plan(j);
  }
  void Plan(ServerJob j){
   if(!ValidateJob(j,out string why)){Abort(j,why+" / source path changed",true);return;}
   // Publish fresh counts independently of whether the complete recipe can
   // be paid. Includes explicit zeros for ingredients consumed by another player.
   foreach(var pair in j.Snapshots){var q=Header(j.Op.Id);q.Write(pair.Key);var current=pair.Value.ToList();foreach(var need in j.Op.Needs)if(!current.Any(s=>s.Item==need.Item))current.Add(new Stock(pair.Key,need.Item,1,0));Wire.Stocks(q,current);Send(j.Op.Peer,"fresh",q);}
   var stock=j.Op.PlayerStock.Concat(j.Snapshots.Values.SelectMany(s=>s)).ToList();
   if(!j.Op.SelectNeeds(stock)||(j.Plan=Planner.Plan(j.Op.Needs,stock,true))==null){
    Abort(j,"insufficient fresh resources",true);return;
   }
   var used=SourceSelection.Sources(j.Plan);if(used==null){Abort(j,"operation source limit",true);return;}
   // Fresh owner snapshots can make some preview sources unnecessary. Release
   // those immediately; only actual contributors take part in commit/rollback.
   var usedSet=new HashSet<string>(used,StringComparer.Ordinal);
   foreach(string key in j.Owners.Keys.Where(key=>!usedSet.Contains(key)).ToArray()){
    ReleaseSource(j.Op.Id,key,j.Owners[key],false);j.Owners.Remove(key);j.Snapshots.Remove(key);
   }
   j.Containers=j.Containers.Where(c=>usedSet.Contains(R.Key(c.m_uid))).ToList();
   j.Decision=new Decision(j.Op.Id,used);foreach(string key in used)j.Decision.Prepared(key);
   j.Debits=j.Plan.Where(d=>d.Source!="player").GroupBy(d=>d.Source).ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.Ordinal);
   foreach(var d in j.Plan)Plugin.Debug(j.Op.Id+" plan "+d.Item+" x"+d.Amount+" source="+d.Source+" quality="+d.Quality);
   j.Outgoing.Clear();j.Started=Time.unscaledTime;
   if(j.Op.Quote){j.QuoteUntil=Time.unscaledTime+10;SendOffer(j);return;}
   CommitPlan(j);
  }
  void CommitPlan(ServerJob j){
   j.Decision.Commit();j.Outgoing.Clear();foreach(string key in j.Owners.Keys)j.Outgoing.Enqueue(key);j.Started=Time.unscaledTime;
   if(j.Containers.Count==0)NotifyPaid(j);
  }
  void SendOffer(ServerJob j){var q=Header(j.Op.Id);Wire.Debits(q,j.Plan);q.Write(Mathf.Max(0,j.QuoteUntil-Time.unscaledTime));Send(j.Op.Peer,"offer",q);}
  void Offer(long sender,ZPackage p){if(sender!=Server)return;string id=p.ReadString();var plan=Wire.Debits(p);CraftPreparation.Offered(id,plan,p.ReadSingle());}
  internal void HoldQuote(string id){Send(Server,"claim",Header(id));}
  internal void UseQuote(string id){Send(Server,"accept",Header(id));}
  internal void DropQuote(string id){Send(Server,"cancelquote",Header(id));}
  void Claim(long sender,ZPackage p){
   string id=p.ReadString();if(!ZNet.instance.IsServer()||!jobs.TryGetValue(id,out var j)||j.Op.Peer!=sender||!j.Op.Quote||j.Decision.Phase!=Phase.Prepared||j.Plan==null)return;
   if(Time.unscaledTime>=j.QuoteUntil){Abort(j,"offer expired",false);return;}
   // First claim pins the existing reservation through the vanilla animation.
   // Replays cannot extend it indefinitely.
   if(!j.QuoteClaimed){j.QuoteClaimed=true;j.QuoteUntil=Time.unscaledTime+25;}
  }
  void Accept(long sender,ZPackage p){
   if(!ZNet.instance.IsServer())return;string id=p.ReadString();
   if(terminal.TryGetValue(id,out var failure)){if(failure.Op.Peer==sender)SendRefusal(failure.Op,failure.Reason);return;}
   if(!jobs.TryGetValue(id,out var j)||j.Op.Peer!=sender||!j.Op.Quote)return;
   if(j.Decision.Phase!=Phase.Prepared){Replay(j);return;}
   if(j.Plan==null||Time.unscaledTime>=j.QuoteUntil){Abort(j,"offer expired",false);return;}
   if(!ValidateJob(j,out string why)){Abort(j,why,false);return;}
   j.ReadyOffered=false;CommitPlan(j);
  }
  void CancelQuote(long sender,ZPackage p){
   if(!ZNet.instance.IsServer())return;string id=p.ReadString();
   if(jobs.TryGetValue(id,out var j)){if(j.Op.Peer==sender&&j.Op.Quote&&(j.Decision.Phase==Phase.Preparing||j.Decision.Phase==Phase.Prepared))Abort(j,"offer cancelled",false);return;}
   var queuedQuote=queued.FirstOrDefault(q=>q.Op.Id==id&&q.Op.Peer==sender&&q.Op.Quote);
   if(queuedQuote!=null){queued.Remove(queuedQuote);Reject(queuedQuote.Op,"offer cancelled");return;}
   if(awaitingRelease.ContainsKey(id))return;
   if(terminal.TryGetValue(id,out var known)){if(known.Op.Peer==sender)SendRefusal(known.Op,known.Reason);return;}
   // A cancel may arrive before its request. Retain a tombstone so that a
   // delayed request cannot acquire stock after the client has moved on.
   if(System.Guid.TryParseExact(id,"N",out _))Reject(new Operation{Id=id,Peer=sender},"offer cancelled");
  }

  void Commit(long sender,ZPackage p){
   if(sender!=Server)return;string id=p.ReadString(),key=p.ReadString();var debits=Wire.Debits(p);var lease=Leases.Values.FirstOrDefault(l=>l.Op.Id==id&&l.Key==key);var answer=Header(id);answer.Write(key);
   if(lease==null){answer.Write(false);answer.Write("reservation missing");Send(Server,"paid",answer);return;}
   try {
    if(!lease.Paid){
     var context=new RemoteContext(lease.Op);if(!lease.Op.ReadRequirements(out string why)||!context.OwnerSource(lease.Container,out why,true))throw new InvalidOperationException(why??"ownership changed");
     foreach(var d in debits)if(d.Source!=key||d.Amount<0||!lease.Op.Needs.Any(n=>n.Item==d.Item&&n.Amount>=d.Amount))throw new InvalidOperationException("Invalid debit");
     if(lease.Delta==null)lease.Delta=new InventoryDelta(lease.Container.GetInventory(),debits,true);
     InternalMutation++;try{lease.Delta.Apply();R.Call(lease.Container,"Save");lease.Paid=true;}finally{InternalMutation--;}
     Plugin.Debug(id+" owner debit confirmed "+key);
    }
    answer.Write(true);
   }catch(Exception e){if(e is InvalidOperationException)Plugin.Debug(id+" owner commit postponed: "+e.Message);else Plugin.Error(id+" owner commit",e);answer.Write(false);answer.Write(e.Message);}
   Send(Server,"paid",answer);
  }
  void Paid(long sender,ZPackage p){
   if(!ZNet.instance.IsServer())return;string id=p.ReadString(),key=p.ReadString();bool ok=p.ReadBool();
   if(!jobs.TryGetValue(id,out var j)||!j.Owners.TryGetValue(key,out long owner)||owner!=sender||j.Decision.Phase!=Phase.Committing)return;
   if(!ok){Abort(j,"commit refused: "+p.ReadString(),true);return;}
   j.PaidSources.Add(key);j.Decision.Paid(key);if(j.Decision.Phase==Phase.Paid)NotifyPaid(j);
  }
  void NotifyPaid(ServerJob j){if(!ValidateJob(j,out string why)){Abort(j,why+" / source path changed before completion",true);return;}j.ReadyOffered=true;j.LastReplay=Time.unscaledTime;SendReady(j);}
  void SendReady(ServerJob j){var q=Header(j.Op.Id);Wire.Debits(q,j.Plan);Send(j.Op.Peer,"ready",q);}
  void Ready(long sender,ZPackage p){if(sender!=Server)return;string id=p.ReadString();var debits=Wire.Debits(p);Actions.Ready(id,debits);}
  void Fresh(long sender,ZPackage p){if(sender!=Server)return;string id=p.ReadString(),key=p.ReadString();var items=Wire.Stocks(p);if(((Actions.Waiting?.Op.Id==id&&Actions.Waiting.Op.Sources.Contains(key))||CraftPreparation.Contains(id,key))&&items.All(s=>s.Source==key)){CraftInspection.Clear();Stockroom.Observe(key,items);}}
  internal void Result(string id,bool success,string reason){var p=Header(id);p.Write(success);p.Write(reason);Send(Server,"finish",p);}
  void Finish(long sender,ZPackage p){
   if(!ZNet.instance.IsServer())return;string id=p.ReadString();bool success=p.ReadBool();string reason=p.ReadString();
   if(!jobs.TryGetValue(id,out var j)||sender!=j.Op.Peer)return;
   if(j.Decision.Phase!=Phase.Paid){Plugin.Log.LogWarning("[RSN] "+id+" late outcome retained for investigation: "+success);return;}
   if(success){j.Decision.Complete();Plugin.Debug(id+" action completion confirmed");ReleaseAll(j,false);jobs.Remove(id);ended.Add(id);}
   else Abort(j,"action refused: "+reason,true);
  }
  void Abort(ServerJob j,string reason,bool compensate){
   jobs.Remove(j.Op.Id);ended.Add(j.Op.Id);ReleaseAll(j,compensate);
   // A new attempt is permitted only after every owner confirms rollback and
   // release. An ambiguous payment must never become a second independent debit.
   awaitingRelease[j.Op.Id]=new Refusal{Op=j.Op,Reason=reason};CompleteRelease(j.Op.Id);Plugin.Debug(j.Op.Id+" refusal: "+reason);
  }
  void CompleteRelease(string id){if(!releasing.ContainsKey(id)&&awaitingRelease.TryGetValue(id,out var failure)){awaitingRelease.Remove(id);Reject(failure.Op,failure.Reason);}}
  void ReleaseAll(ServerJob j,bool rollback){foreach(var pair in j.Owners)ReleaseSource(j.Op.Id,pair.Key,pair.Value,rollback);}
  void ReleaseSource(string id,string key,long owner,bool rollback){
   if(!releasing.TryGetValue(id,out var sources))releasing[id]=sources=new Dictionary<string,Releasing>();
   sources[key]=new Releasing{Owner=owner,Since=Time.unscaledTime,Rollback=rollback};SendRelease(id,key,sources[key]);
  }
  void SendRelease(string id,string key,Releasing release){release.LastSend=Time.unscaledTime;var q=Header(id);q.Write(key);q.Write(release.Rollback);try{Send(release.Owner,"release",q);}catch(Exception e){Plugin.Debug(id+" release retry: "+e.Message);}}
  void AcknowledgeRelease(string id,string key){var q=Header(id);q.Write(key);Send(Server,"released",q);}
  void Released(long sender,ZPackage p){
   if(!ZNet.instance.IsServer())return;string id=p.ReadString(),key=p.ReadString();
   if(!releasing.TryGetValue(id,out var sources)||!sources.TryGetValue(key,out var release)||release.Owner!=sender)return;
   sources.Remove(key);sourceGate.Released(id,key);if(sources.Count==0)releasing.Remove(id);CompleteRelease(id);
  }
  void Release(long sender,ZPackage p){
   if(sender!=Server)return;string id=p.ReadString(),key=p.ReadString();bool rollback=p.ReadBool();var lease=Leases.Values.FirstOrDefault(l=>l.Op.Id==id&&l.Key==key);if(lease==null){releasedLeases.Add(id+key);AcknowledgeRelease(id,key);return;}
   if(!lease.Container||!R.View(lease.Container).IsOwner()){Plugin.Critical(id,"Release ownership lost; no blind rollback");return;}
   InternalMutation++;try{
    if(rollback)lease.Delta?.Restore();R.Call(lease.Container,"Save");Integrations.Block(lease.Container.GetInventory(),false);
    R.Set(lease.Container,"m_inUse",false);R.View(lease.Container).GetZDO().Set(ZDOVars.s_inUse,0);R.View(lease.Container).GetZDO().Set("rsn_lease","");Leases.Remove(lease.Container.GetInventory());releasedLeases.Add(id+key);Plugin.Debug(id+(rollback?" compensated/released ":" released ")+key);AcknowledgeRelease(id,key);
   }catch(Exception e){Plugin.Error(id+" compensation/release",e);}finally{InternalMutation--;}
  }
  void Reject(Operation op,string reason){ended.Add(op.Id);terminal[op.Id]=new Refusal{Op=op,Reason=reason};Plugin.Debug(op.Id+" refused: "+reason+" peer="+op.Peer+" actor="+R.Key(op.Actor)+" core="+R.Key(op.Core)+" network="+op.Network+" target="+op.Target);SendRefusal(op,reason);}
  void SendRefusal(Operation op,string reason){var q=Header(op.Id);q.Write(reason);Send(op.Peer,"refused",q);}
  void Refused(long sender,ZPackage p){if(sender!=Server)return;string id=p.ReadString(),why=p.ReadString();CraftPreparation.Refused(id,why);Actions.Refused(id,why);}
  internal static bool Locked(Inventory inv)=>InternalMutation==0&&inv!=null&&(Leases.ContainsKey(inv)||Actions.Locked(inv)||CraftPreparation.Locked(inv));
  internal static bool LockedItem(ItemDrop.ItemData item)=>InternalMutation==0&&(Leases.Keys.Any(i=>i.ContainsItem(item))||(Actions.Waiting?.Player&&Actions.Waiting.Player.GetInventory().ContainsItem(item))||CraftPreparation.LockedItem(item));
  internal static bool Reserved(ZDO zdo,string except=null)=>zdo!=null&&((zdo.GetString("rsn_lease","")!=""&&zdo.GetString("rsn_lease","")!=except)||(Instance!=null&&ZNet.instance&&ZNet.instance.IsServer()&&Instance.sourceGate.Held(R.Key(zdo.m_uid),except)));
 }
}
