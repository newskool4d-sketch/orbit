using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Orbit;

internal static class DdaySelfTests
{
    static bool Rejects(Action work) { try { work(); return false; } catch { return true; } }
    public static void Run(Action<bool,string> check)
    {
        var root = Path.Combine(Path.GetTempPath(), "Orbit-dday-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "ddays.json");
        try
        {
            var store = new DdayStore(path);
            store.Add("  합성 날짜  ", "2028-02-29", true);
            var id = store.Snapshot()[0]!["id"]!.ToString();
            store = new DdayStore(path);
            check(store.Error == null && store.Snapshot()[0]!["title"]!.ToString() == "합성 날짜", "dday-disk-reload");
            store.Update(id, "합성 변경", "2030-12-31", false);
            var restored = new DdayStore(path).Snapshot()[0]!;
            check(restored["targetDate"]!.ToString() == "2030-12-31" && !restored["pinned"]!.GetValue<bool>(), "dday-edit-persisted");
            store.Archive(id, true);
            check(new DdayStore(path).Snapshot()[0]!["archived"]!.GetValue<bool>(), "dday-archive-persisted");
            store.Archive(id, false);
            check(!new DdayStore(path).Snapshot()[0]!["archived"]!.GetValue<bool>(), "dday-restore-persisted");
            var detached = store.Snapshot(); detached[0]!["title"] = "changed snapshot";
            check(store.Snapshot()[0]!["title"]!.ToString() == "합성 변경", "dday-snapshot-isolation");
            foreach (var date in new[]{"0000-01-01","2027-02-29","2026-04-31","2026-13-01","2026-1-01","2026-01-01T00:00:00Z"})
                check(Rejects(() => store.Add("fixture",date,false)), "dday-invalid-date-rejected");
            foreach (var title in new[]{" ",new string('가',121),"a\tb","a\u0085b"})
                check(Rejects(() => store.Add(title,"2030-01-01",false)), "dday-invalid-title-rejected");
            check(Rejects(() => store.Delete(Guid.NewGuid().ToString("N"))) && store.Snapshot().Count==1, "dday-unknown-id-no-mutation");
            store.Add("합성 변경", "2030-12-31", false);
            check(store.Snapshot().Count == 2, "dday-duplicate-titles-allowed");
            store.Delete(id);
            check(new DdayStore(path).Snapshot().Count == 1, "dday-delete-persisted");
            var valid = File.ReadAllText(path);
            var duplicate = JsonNode.Parse(valid)!;
            duplicate["items"]!.AsArray().Add(duplicate["items"]![0]!.DeepClone());
            var missingBoolean = JsonNode.Parse(valid)!; missingBoolean["items"]![0]!.AsObject().Remove("pinned");
            foreach (var damaged in new[]{"{not-json","{}","null","{\"schemaVersion\":1}","{\"items\":[]}","{\"schemaVersion\":2,\"items\":[]}","{\"schemaVersion\":1,\"items\":null}",duplicate.ToJsonString(),missingBoolean.ToJsonString()})
            {
                File.WriteAllText(path, damaged);
                var blocked = new DdayStore(path);
                check(blocked.Error != null && Rejects(() => blocked.Add("fixture","2030-01-01",false)) && File.ReadAllText(path)==damaged, "dday-damaged-file-preserved");
            }
            File.WriteAllText(path, valid);
            var failureStore = new DdayStore(path);
            var before = failureStore.Snapshot().ToJsonString();
            // Make only the fixture destination unwritable by occupying it with a directory.
            File.Delete(path); Directory.CreateDirectory(path);
            check(Rejects(() => failureStore.Add("fixture","2030-01-01",false)) && failureStore.Snapshot().ToJsonString()==before, "dday-failed-save-rollback");
            check(Directory.GetFiles(root,"*.tmp").Length==0, "dday-failed-save-temp-cleanup");
            Directory.Delete(path);
            failureStore.Add("fixture","2030-01-01",false);
            check(new DdayStore(path).Snapshot().Count==2, "dday-retry-after-save-failure");
        }
        finally
        {
            // root is a unique directory created above; remove only these exact fixture files.
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(path)) Directory.Delete(path);
            foreach (var temp in Directory.GetFiles(root,"ddays.json.*.tmp")) File.Delete(temp);
            Directory.Delete(root);
        }
    }
}
