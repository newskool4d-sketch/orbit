using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Orbit;

internal static class BridgePolicy
{
    public const string Document = "https://orbit.invalid/index.html";
    private static readonly Dictionary<string, string[]> Fields = new()
    {
        ["ready"]=[], ["refresh"]=[], ["close"]=[], ["quit"]=[],
        ["theme"]=["value"], ["pin"]=["value"], ["openFile"]=["id"],
        ["openAgent"]=["id","mode"], ["openEvent"]=["id","meeting"],
        ["completeTask"]=["id","value"], ["openCalendar"]=[], ["openTasks"]=[],
        ["chooseDrive"]=[], ["chooseVault"]=[], ["chooseCodex"]=[], ["chooseClaude"]=[],
        ["importGoogle"]=[], ["connectGoogle"]=[], ["cancelGoogle"]=[],
        ["disconnectGoogle"]=[], ["googleHelp"]=[]
    };
    public static bool Trusted(string sender, string current) => sender == Document && current == Document;
    public static bool Validate(string json, out JsonElement message)
    {
        message = default;
        if (Encoding.UTF8.GetByteCount(json) > 65536) return false;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth=8 });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            var names = root.EnumerateObject().Select(p=>p.Name).ToArray();
            if (names.Distinct().Count()!=names.Length) return false;
            if (!root.TryGetProperty("action",out var a) || a.ValueKind!=JsonValueKind.String ||
                !Fields.TryGetValue(a.GetString()!,out var fields)) return false;
            if (!root.TryGetProperty("request",out var r) || r.ValueKind!=JsonValueKind.String || r.GetString()!.Length is <1 or >99) return false;
            if (names.Any(n=>n!="action" && n!="request" && !fields.Contains(n))) return false;
            foreach (var p in root.EnumerateObject())
            {
                if (p.Name=="id" && (p.Value.ValueKind!=JsonValueKind.String || !Guid.TryParseExact(p.Value.GetString(),"N",out _))) return false;
                if ((p.Name=="meeting" || p.Name=="value" && a.GetString()!="theme") && p.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
                if (p.Name=="mode" && (p.Value.ValueKind!=JsonValueKind.String || p.Value.GetString() is not ("default" or "fallback"))) return false;
            }
            if (fields.Contains("id") && !root.TryGetProperty("id",out _)) return false;
            if (fields.Contains("value") && !root.TryGetProperty("value",out _)) return false;
            if (a.GetString()=="theme" && (root.GetProperty("value").ValueKind!=JsonValueKind.String || root.GetProperty("value").GetString() is not ("moss" or "pearl" or "cobalt"))) return false;
            message = root.Clone(); return true;
        } catch (JsonException) { return false; }
    }
}

internal static class PathPolicy
{
    public static bool Within(string root,string path)
    {
        try
        {
            root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            path=Path.GetFullPath(path);
            if (!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) return false;
            var cursor=path;
            while (!string.Equals(cursor,root,StringComparison.OrdinalIgnoreCase))
            {
                if (!ReparsePolicy.Safe(cursor)) return false;
                cursor=Path.GetDirectoryName(cursor)!;
                if (cursor==null) return false;
            }
            return ReparsePolicy.Safe(root);
        } catch { return false; }
    }
    public static bool Https(string value) => Uri.TryCreate(value,UriKind.Absolute,out var uri) && uri.Scheme=="https" && uri.UserInfo.Length==0;
}

internal static class OAuthPolicy
{
    public static string Base64Url(byte[] bytes)=>Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
    public static string Challenge(string verifier)=>Base64Url(System.Security.Cryptography.SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    public static Dictionary<string,string> Parameters(string query)
    {
        var result=new Dictionary<string,string>();
        foreach(var pair in query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries))
        {
            var parts=pair.Split('=',2);
            var key=Uri.UnescapeDataString(parts[0].Replace('+',' '));
            if(!result.TryAdd(key,Uri.UnescapeDataString((parts.Length==2?parts[1]:"").Replace('+',' ')))) throw new InvalidDataException("duplicate-parameter");
        }
        return result;
    }
}
