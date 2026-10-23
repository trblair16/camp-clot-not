using System.Text.Json;
using System.Text.Json.Serialization;
using CampClotNot.Data;

namespace CampClotNot.Services;

/// <summary>
/// An event's schedule with database references written as names, so it can be applied to another
/// event or another site (staging → production). Ids are kept alongside the names and win when they
/// exist in the target database. See docs/superpowers/specs/2026-10-14-schedule-export-design.md.
/// </summary>
public class PortableSchedule
{
    public const string FormatName = "hbda-events-schedule";
    public const int CurrentVersion = 1;
    public const int MaxItems = 2000;

    // No defaults: a file must say what it is, so a stray JSON file isn't mistaken for an export.
    public string Format { get; set; } = "";
    public int Version { get; set; }
    public DateTime ExportedAt { get; set; }
    public PortableSource Source { get; set; } = new();
    public List<PortableScheduleItem> Items { get; set; } = [];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public byte[] ToJson() => JsonSerializer.SerializeToUtf8Bytes(this, Json);

    /// <summary>Reads an exported file. Returns null with a message the admin can act on when it isn't one.</summary>
    public static async Task<(PortableSchedule? Schedule, string? Error)> ReadAsync(Stream stream)
    {
        PortableSchedule? s;
        try { s = await JsonSerializer.DeserializeAsync<PortableSchedule>(stream, Json); }
        catch (JsonException) { return (null, "That file isn't a schedule export. Use ⬇ Export (.json) on the other site's Schedule page."); }

        if (s is null || s.Format != FormatName || s.Version < 1)
            return (null, "That file isn't a schedule export. Use ⬇ Export (.json) on the other site's Schedule page.");
        // Explicit nulls in hand-edited files would otherwise surface as crashes further on.
        s.Source ??= new();
        s.Source.EventName ??= "";
        s.Items ??= [];
        s.Items.RemoveAll(i => i is null);
        foreach (var i in s.Items) { i.Groups ??= []; i.Groups.RemoveAll(g => g is null || string.IsNullOrWhiteSpace(g.Group)); }
        if (s.Version > CurrentVersion)
            return (null, "That export is from a newer version of the app. Update this site first, then import it.");
        if (s.Items.Count > MaxItems)
            return (null, $"That export has {s.Items.Count} items; the limit is {MaxItems}.");
        if (s.Items.Count == 0)
            return (null, "That export has no schedule items.");

        var keys = new HashSet<string>();
        foreach (var (item, n) in s.Items.Select((x, i) => (x, i + 1)))
        {
            if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrEmpty(item.Key) || !keys.Add(item.Key))
                return (null, $"Item {n} in that file is incomplete (it needs a title and a unique key). Export it again.");
        }
        if (s.Items.Any(i => i.ParentKey is { } p && !s.Items.Any(o => o.Key == p && o.IsBreakoutSlot)))
            return (null, "A breakout option in that file points at a slot that isn't there. Export it again.");
        return (s, null);
    }
}

public class PortableSource
{
    public string EventName { get; set; } = "";
    public DateOnly EffDate { get; set; }
    public DateOnly ExpDate { get; set; }
}

public class PortableScheduleItem
{
    public string Key { get; set; } = "";              // unique within the file; options point at their slot's key
    public string? ParentKey { get; set; }
    public int DayIndex { get; set; }                   // 0 = the event's first day; days outside the target are skipped
    public DateOnly Date { get; set; }                  // the original date, for reference only
    public TimeOnly Start { get; set; }
    public TimeOnly? End { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Guid? TypeId { get; set; }
    public string? TypeSystemName { get; set; }
    public string? TypeName { get; set; }
    public Guid? LocationId { get; set; }
    public string? Location { get; set; }
    public string? LocationOther { get; set; }
    public string? Activity { get; set; }
    public string? PresenterName { get; set; }
    public string? PresenterBio { get; set; }
    public int? MaxCapacity { get; set; }
    public bool TrackAttendance { get; set; }
    public SelfCheckInMode SelfCheckInMode { get; set; }
    public bool IsBreakoutSlot { get; set; }
    public bool AllowSelfSignup { get; set; } = true;
    public List<PortableItemGroup> Groups { get; set; } = [];
}

public class PortableItemGroup
{
    public string Group { get; set; } = "";
    public string? GroupShortName { get; set; }
    public string? Activity { get; set; }
    public Guid? LocationId { get; set; }
    public string? Location { get; set; }
    public string? Note { get; set; }
}
