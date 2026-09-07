using System;
using System.IO;
using System.Text.Json;
namespace Orbit;
internal sealed class Settings
{
    public int SchemaVersion {get;set;}=1;
    public string Theme {get;set;}="moss";
    public string Drive {get;set;}="";
    public string Vault {get;set;}="";
    public string Codex {get;set;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");
    public string Claude {get;set;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","projects");
    public static string DataRoot=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Orbit");
    public static Settings Load() {
        try {var s=JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(DataRoot,"settings.json")));if(s?.SchemaVersion!=1)return new();if(s.Theme is not ("moss" or "pearl" or "cobalt"))s.Theme="moss";RootDiscovery.Fill(s);return s;}
        catch{var s=new Settings();RootDiscovery.Fill(s);return s;}
    }
    public void Save() {
        Directory.CreateDirectory(DataRoot);var path=Path.Combine(DataRoot,"settings.json");var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,path,true);
    }
}
