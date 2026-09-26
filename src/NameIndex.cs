using System;
using System.Collections.Generic;
using System.Linq;

namespace RunicStorageNetwork.Logic {
 // Name to object lookup with the ambiguities reported rather than hidden. Recipes travel
 // between clients and the coordinator as the name of their asset, so a name that two
 // recipes share, or that no recipe carries, makes an operation unresolvable on one side
 // while the acting client resolved it. Pure logic with no Unity or game types.
 public sealed class NameIndex<T> where T:class {
  readonly Dictionary<string,T> byName=new Dictionary<string,T>(StringComparer.Ordinal);
  readonly List<string> duplicates=new List<string>();
  public int Count=>byName.Count;
  public int Unnamed {get;private set;}
  public IEnumerable<string> Duplicates=>duplicates;
  public int DuplicateCount=>duplicates.Count;
  public bool Ambiguous(string name)=>name!=null&&duplicates.Contains(name);

  // First entry wins, matching the FirstOrDefault scan this replaces, so an ambiguous
  // name resolves the same way on every side instead of depending on the call site.
  public void Add(string name,T value){
   if(value==null)return;
   if(string.IsNullOrEmpty(name)){Unnamed++;return;}
   if(byName.ContainsKey(name)){if(!duplicates.Contains(name))duplicates.Add(name);return;}
   byName[name]=value;
  }
  public T Find(string name)=>!string.IsNullOrEmpty(name)&&byName.TryGetValue(name,out var value)?value:null;

  public string Report {
   get {
    var parts=new List<string>{Count+" named"};
    if(Unnamed>0)parts.Add(Unnamed+" unnamed");
    if(duplicates.Count>0)parts.Add(duplicates.Count+" ambiguous: "+string.Join(", ",duplicates.OrderBy(d=>d,StringComparer.Ordinal).ToArray()));
    return string.Join("; ",parts.ToArray());
   }
  }
 }
}
