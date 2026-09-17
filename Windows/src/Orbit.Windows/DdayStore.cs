using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Orbit;

internal sealed class DdayEntry
{
    [JsonRequired] public string Id { get; set; } = "";
    [JsonRequired] public string Title { get; set; } = "";
    [JsonRequired] public string TargetDate { get; set; } = "";
    [JsonRequired] public bool Pinned { get; set; }
    [JsonRequired] public bool Archived { get; set; }
    [JsonRequired] public string CreatedAt { get; set; } = "";
    [JsonRequired] public string UpdatedAt { get; set; } = "";
}

internal sealed class DdayDocument
{
    [JsonRequired] public int SchemaVersion { get; set; } = 1;
    [JsonRequired] public List<DdayEntry> Items { get; set; } = [];
}

internal sealed class DdayStore
{
    const string LoadMessage = "중요 날짜 저장 파일을 읽지 못했습니다. 원본을 보존했으므로 파일을 확인하세요.";
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    readonly string? path;
    List<DdayEntry> items = [];
    public string? Error { get; private set; }

    public DdayStore() : this(Path.Combine(Settings.DataRoot, "ddays.json")) { }
    internal DdayStore(string? path, IEnumerable<DdayEntry>? seed = null)
    {
        this.path = path;
        if (seed != null) items = seed.Select(Clone).ToList();
        else Load();
    }
    internal static DdayStore Memory(IEnumerable<DdayEntry>? seed = null) => new(null, seed);

    public JsonArray Snapshot() => new(items.Select(item => JsonSerializer.SerializeToNode(item, JsonOptions)!).ToArray());

    public void Add(string title, string targetDate, bool pinned)
    {
        title = ValidTitle(title);
        ValidDate(targetDate);
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        Change(next => next.Add(new DdayEntry
        {
            Id = Guid.NewGuid().ToString("N"), Title = title, TargetDate = targetDate,
            Pinned = pinned, Archived = false, CreatedAt = now, UpdatedAt = now
        }));
    }

    public void Update(string id, string title, string targetDate, bool pinned)
    {
        title = ValidTitle(title);
        ValidDate(targetDate);
        Change(next =>
        {
            var item = Find(next, id);
            item.Title = title; item.TargetDate = targetDate; item.Pinned = pinned;
            item.UpdatedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        });
    }

    public void Archive(string id, bool value) => Change(next =>
    {
        var item = Find(next, id);
        item.Archived = value;
        item.UpdatedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    });

    public void Delete(string id) => Change(next =>
    {
        var item = Find(next, id);
        next.Remove(item);
    });

    static DdayEntry Find(List<DdayEntry> values, string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidOperationException("invalid-dday-id");
        return values.FirstOrDefault(value => value.Id == id) ?? throw new InvalidOperationException("missing-dday");
    }
    static string ValidTitle(string value)
    {
        var title = value.Trim();
        if (title.Length is < 1 or > 120 || title.Any(char.IsControl)) throw new InvalidOperationException("invalid-dday-title");
        return title;
    }
    internal static void ValidDate(string value)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) != value)
            throw new InvalidOperationException("invalid-dday-date");
    }
    void Change(Action<List<DdayEntry>> change)
    {
        if (Error != null) throw new InvalidOperationException("dday-store-unavailable");
        var next = items.Select(Clone).ToList();
        change(next);
        Save(next);
        items = next;
    }
    void Load()
    {
        if (path == null || !File.Exists(path)) return;
        try
        {
            var document = JsonSerializer.Deserialize<DdayDocument>(File.ReadAllText(path), JsonOptions);
            if (document?.SchemaVersion != 1 || document.Items == null) throw new InvalidDataException();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in document.Items)
            {
                if (!Guid.TryParseExact(item.Id, "N", out _) || !ids.Add(item.Id) || ValidTitle(item.Title) != item.Title) throw new InvalidDataException();
                ValidDate(item.TargetDate);
                if (!DateTimeOffset.TryParse(item.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)
                    || !DateTimeOffset.TryParse(item.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)) throw new InvalidDataException();
            }
            items = document.Items.Select(Clone).ToList();
        }
        catch { Error = LoadMessage; items = []; }
    }
    void Save(List<DdayEntry> next)
    {
        if (path == null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(new DdayDocument { Items = next }, JsonOptions));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    static DdayEntry Clone(DdayEntry value) => new()
    {
        Id=value.Id, Title=value.Title, TargetDate=value.TargetDate, Pinned=value.Pinned,
        Archived=value.Archived, CreatedAt=value.CreatedAt, UpdatedAt=value.UpdatedAt
    };
}
