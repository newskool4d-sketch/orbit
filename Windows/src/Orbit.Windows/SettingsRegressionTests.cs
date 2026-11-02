using System;
using System.IO;
using System.Linq;

namespace Orbit;

internal static class SettingsRegressionTests
{
    internal static void Run(Action<bool,string> check)
    {
        var root=Path.Combine(Path.GetTempPath(),"Orbit-settings-fixture-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var path=Path.Combine(root,"settings.json");
            File.WriteAllText(path,"{\"SchemaVersion\":1,\"Theme\":\"pearl\",\"TextSize\":\"large\",\"Drive\":null,\"Vault\":null,\"Codex\":null,\"Claude\":null}");
            var loaded=Settings.Load(root,false);
            check(loaded.Theme=="pearl"&&loaded.TextSize=="large"&&loaded.Drive==""&&loaded.Vault==""&&loaded.Codex==""&&loaded.Claude=="","settings-null-roots-preserve-valid-preferences");
            var snapshot=LocalProviders.Read(loaded);
            check(snapshot.Statuses.Count==4&&snapshot.Statuses.All(s=>s["state"]!.ToString()=="disconnected"),"settings-null-roots-scan-safely");
            var invalid=new Settings{Drive="",Vault="",Claude="",Codex="\0"};
            check(LocalProviders.Read(invalid).Statuses.Single(s=>s["source"]!.ToString()=="codex")["state"]!.ToString()=="error","invalid-codex-path-is-source-error");
            loaded.Save(root);
            var before=File.ReadAllText(path);
            using(var held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                bool rejected=false;
                try {loaded.Change(next=>{next.Theme="cobalt";next.TextSize="xlarge";next.Drive=Path.Combine(root,"new-root");next.Vault="new-vault";next.Codex="new-codex";next.Claude="new-claude";},root);}
                catch(Exception error) when(error is IOException or UnauthorizedAccessException){rejected=true;}
                check(rejected&&loaded.Theme=="pearl"&&loaded.TextSize=="large"&&loaded.Drive==""&&loaded.Vault==""&&loaded.Codex==""&&loaded.Claude=="","settings-failed-save-keeps-all-live-values");
                check(File.ReadAllText(path)==before,"settings-failed-save-preserves-disk");
                check(Directory.GetFiles(root,"*.tmp").Length==0,"settings-failed-save-cleans-temp");
            }
            loaded.Change(next=>{next.Theme="cobalt";next.TextSize="xlarge";next.Drive=root;},root);
            var restored=Settings.Load(root,false);
            check(loaded.Theme=="cobalt"&&restored.Theme=="cobalt"&&restored.TextSize=="xlarge"&&restored.Drive==root,"settings-retry-commits-and-reloads");
        } finally {
            foreach(var file in Directory.GetFiles(root))File.Delete(file);
            Directory.Delete(root);
        }
    }
}
