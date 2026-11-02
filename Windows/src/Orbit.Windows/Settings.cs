using System;
using System.IO;
using System.Text.Json;
namespace Orbit;
internal sealed class Settings
{
    public int SchemaVersion {get;set;}=1;
    public string Theme {get;set;}="moss";
    public string TextSize {get;set;}="normal";
    public string Drive {get;set;}="";
    public string Vault {get;set;}="";
    public string Codex {get;set;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");
    public string Claude {get;set;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","projects");
    public static string DataRoot=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Orbit");
    public static Settings Load(string? dataRoot=null,bool discoverRoots=true) {
        try {var s=JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(dataRoot??DataRoot,"settings.json")));if(s?.SchemaVersion!=1)return new();if(s.Theme is not ("moss" or "pearl" or "cobalt"))s.Theme="moss";if(s.TextSize is not ("normal" or "large" or "xlarge"))s.TextSize="normal";s.Drive??="";s.Vault??="";s.Codex??="";s.Claude??="";if(discoverRoots)RootDiscovery.Fill(s);return s;}
        catch{var s=new Settings();if(discoverRoots)RootDiscovery.Fill(s);return s;}
    }
    public void Save(string? dataRoot=null) {
        var root=dataRoot??DataRoot;Directory.CreateDirectory(root);var path=Path.Combine(root,"settings.json");var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,path,true);}
        finally {if(File.Exists(temp))File.Delete(temp);}
    }
    public void Change(Action<Settings> change,string? dataRoot=null) {
        var next=(Settings)MemberwiseClone();
        change(next);
        next.Save(dataRoot);
        Theme=next.Theme;TextSize=next.TextSize;Drive=next.Drive;Vault=next.Vault;Codex=next.Codex;Claude=next.Claude;
    }
}
