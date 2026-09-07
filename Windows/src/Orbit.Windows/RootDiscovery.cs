using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
namespace Orbit;
internal static class RootDiscovery
{
    public static void Fill(Settings settings)
    {
        if(settings.Vault.Length==0)
        {
            try {
                var config=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"obsidian","obsidian.json");
                if(File.Exists(config)&&new FileInfo(config).Length<1024*1024&&JsonNode.Parse(File.ReadAllText(config))?["vaults"] is JsonObject vaults)
                {
                    var items=vaults.Select(p=>p.Value).OfType<JsonObject>().ToArray();
                    var open=items.Where(v=>v["open"]?.GetValue<bool>()==true).ToArray();
                    var selected=open.Length==1?open:items;
                    if(selected.Length==1&&selected[0]["path"]?.GetValue<string>() is string path&&Directory.Exists(path))settings.Vault=path;
                }
            }catch{}
        }
        if(settings.Drive.Length==0)
        {
            try {
                var candidates=DriveInfo.GetDrives().Where(d=>d.IsReady&&d.VolumeLabel.Contains("Google Drive",StringComparison.OrdinalIgnoreCase))
                    .SelectMany(d=>new[]{"My Drive","내 드라이브"}.Select(n=>Path.Combine(d.RootDirectory.FullName,n))).Where(Directory.Exists).ToArray();
                if(candidates.Length==1)settings.Drive=candidates[0];
            }catch{}
        }
    }
}
