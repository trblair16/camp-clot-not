using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CampClotNot.Data;
using CampClotNot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampClotNot.Services;

/// <summary>One parsed spreadsheet row, with what it resolved to and anything wrong with it.</summary>
public class ImportRow
{
    public int RowNumber { get; init; }
    public DateOnly? Day { get; set; }
    public TimeOnly? Start { get; set; }
    public TimeOnly? End { get; set; }
    public string Title { get; set; } = "";
    public ScheduleItemType? Type { get; set; }
    public bool EnableType { get; set; }              // known type, not yet enabled for the event
    public Location? Location { get; set; }
    public string? NewLocationName { get; set; }      // unknown location text
    public string? LocationOther { get; set; }
    public string? Description { get; set; }
    public string? Presenter { get; set; }
    public bool TrackAttendance { get; set; }
    public bool Duplicate { get; set; }
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];
    public bool CanImport => Errors.Count == 0 && !Duplicate;
}

public record CopyDay(DateOnly SourceDay, DateOnly? TargetDay, int Items, int Duplicates);
public record CopyPreview(Event Source, List<CopyDay> Days)
{
    public int ToCopy => Days.Where(d => d.TargetDay is not null).Sum(d => d.Items - d.Duplicates);
    public int SkippedDays => Days.Count(d => d.TargetDay is null);
}

/// <summary>
/// Faster schedule setup: lenient time/day parsing, a per-event .xlsx template, spreadsheet/paste
/// import with a preview, and copying a past event's schedule. See
/// docs/superpowers/specs/2026-09-26-faster-schedule-setup-design.md.
/// </summary>
public class ScheduleImportService(IDbContextFactory<AppDbContext> factory, ScheduleService scheduleSvc, ScheduleItemTypeService typeSvc)
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    // ── Times ────────────────────────────────────────────────────────────────

    private static readonly Regex TimeRx = new(@"^(\d{1,2})(?::?(\d{2}))?\s*(a|am|p|pm)?$", RegexOptions.IgnoreCase);

    /// <summary>
    /// Accepts 9, 930, 9:30, 9:30a, 2pm, 2:15 PM, 14:00, noon, midnight. Without am/pm, 7–11 read as
    /// morning and 12–6 as afternoon/evening (a camp day), unless <paramref name="meridiemHint"/>
    /// ("am"/"pm") says otherwise. 13–23 are 24-hour times.
    /// </summary>
    public static bool TryParseTime(string? text, out TimeOnly time, string? meridiemHint = null)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim().ToLowerInvariant().Replace(".", "").Replace(" ", "");
        if (t == "noon") { time = new TimeOnly(12, 0); return true; }
        if (t == "midnight") { time = new TimeOnly(0, 0); return true; }

        var m = TimeRx.Match(t);
        if (!m.Success) return false;
        var hour = int.Parse(m.Groups[1].Value);
        var minute = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
        if (minute > 59) return false;

        var meridiem = m.Groups[3].Success ? m.Groups[3].Value[..1] : meridiemHint?[..1];
        if (meridiem is not null)
        {
            if (hour is < 1 or > 12) return false;
            if (meridiem == "a") hour = hour == 12 ? 0 : hour;
            else hour = hour == 12 ? 12 : hour + 12;
        }
        else if (hour > 23) return false;
        else if (hour is >= 1 and <= 6) hour += 12;

        time = new TimeOnly(hour, minute);
        return true;
    }

    private static readonly Regex RangeSplit = new(@"\s*(?:-|–|—|\bto\b)\s*", RegexOptions.IgnoreCase);
    private static readonly Regex MeridiemRx = new(@"(a|p)\.?m?\.?\s*$", RegexOptions.IgnoreCase);

    /// <summary>"9:00 – 10:30 AM", "11:30-1pm", "2 to 3 pm" → start and end. A single time gives no end.</summary>
    public static bool TryParseTimeRange(string? text, out TimeOnly start, out TimeOnly? end)
    {
        start = default; end = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = RangeSplit.Split(text.Trim());
        if (parts.Length == 1) return TryParseTime(parts[0], out start);
        if (parts.Length != 2 || !TryParseTime(parts[1], out var e)) return false;

        // The end's am/pm carries to the start ("9–10:30 AM") unless the start then lands after
        // the end ("11:30–1 PM" means 11:30 AM).
        var hint = MeridiemRx.Match(parts[1].Trim()) is { Success: true } mm ? mm.Groups[1].Value.ToLowerInvariant() + "m" : null;
        if (!TryParseTime(parts[0], out var s, hint)) return false;
        if (hint is not null && !MeridiemRx.IsMatch(parts[0].Trim()) && s > e && TryParseTime(parts[0], out var s2, hint == "pm" ? "am" : "pm"))
            s = s2;
        start = s; end = e;
        return true;
    }

    // ── Days ─────────────────────────────────────────────────────────────────

    private static readonly Regex WeekdayPrefix = new(
        @"^(mon|tue|tues|wed|thu|thur|thurs|fri|sat|sun)(day|nesday|rsday|urday|sday)?\.?,?\s*", RegexOptions.IgnoreCase);
    private static readonly string[] DateFormats =
        ["M/d/yyyy", "M/d/yy", "yyyy-MM-dd", "yyyy-MM-dd HH:mm", "MMM d yyyy", "MMMM d yyyy", "MMM d, yyyy", "MMMM d, yyyy"];
    private static readonly string[] NoYearFormats = ["M/d", "MMM d", "MMMM d", "d MMM", "d MMMM"];

    /// <summary>A date, weekday name, or "Day N" → one of the event's days.</summary>
    public static bool TryParseDay(string? text, Event ev, out DateOnly day, out string? error)
    {
        day = default; error = null;
        var days = Enumerable.Range(0, ev.ExpDate.DayNumber - ev.EffDate.DayNumber + 1).Select(i => ev.EffDate.AddDays(i)).ToList();
        if (string.IsNullOrWhiteSpace(text)) { error = "Day is blank"; return false; }
        var t = text.Trim();

        var dayN = Regex.Match(t, @"^(?:day\s*)?(\d{1,2})$", RegexOptions.IgnoreCase);
        if (dayN.Success)
        {
            var n = int.Parse(dayN.Groups[1].Value);
            if (n >= 1 && n <= days.Count) { day = days[n - 1]; return true; }
            error = $"\"{t}\" — the event only has {days.Count} day{(days.Count == 1 ? "" : "s")}";
            return false;
        }

        // A bare weekday ("Sat", "Saturday") works when it's unique within the event.
        var word = t.TrimEnd('.');
        if (word.Length >= 3 && Enum.GetValues<DayOfWeek>().FirstOrDefault(d => d.ToString().StartsWith(word, StringComparison.OrdinalIgnoreCase)) is var dow
            && dow.ToString().StartsWith(word, StringComparison.OrdinalIgnoreCase))
        {
            var matches = days.Where(d => d.DayOfWeek == dow).ToList();
            if (matches.Count == 1) { day = matches[0]; return true; }
            error = matches.Count == 0 ? $"No {dow} during the event" : $"\"{t}\" matches more than one day — use a date";
            return false;
        }

        var rest = WeekdayPrefix.Replace(t, "").Trim();
        if (DateTime.TryParseExact(rest, DateFormats, Us, DateTimeStyles.AllowWhiteSpaces, out var full))
            day = DateOnly.FromDateTime(full);
        else if (DateTime.TryParseExact(rest, NoYearFormats, Us, DateTimeStyles.AllowWhiteSpaces, out var noYear))
            day = days.FirstOrDefault(d => d.Month == noYear.Month && d.Day == noYear.Day);
        else { error = $"Can't read \"{t}\" as a day"; return false; }

        if (day == default || day < ev.EffDate || day > ev.ExpDate)
        {
            error = $"\"{t}\" isn't during the event ({ev.EffDate:MMM d}–{ev.ExpDate:MMM d})";
            return false;
        }
        return true;
    }

    // ── Reading tables ───────────────────────────────────────────────────────

    /// <summary>Pasted text from Excel/Sheets/Word (tab-separated) or a CSV file's text.</summary>
    public static List<string[]> ReadDelimited(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var tab = lines.Contains('\t');
        var rows = new List<string[]>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var c = lines[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < lines.Length && lines[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"' && cell.Length == 0) quoted = true;
            else if (c == (tab ? '\t' : ',')) { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n') { row.Add(cell.ToString()); cell.Clear(); rows.Add([.. row]); row.Clear(); }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add([.. row]); }
        return rows;
    }

    /// <summary>The "Schedule" sheet (or the first sheet) as text cells. Excel dates/times become ISO text.</summary>
    public static List<string[]> ReadXlsx(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.FirstOrDefault(w => w.Name.Equals("Schedule", StringComparison.OrdinalIgnoreCase)) ?? wb.Worksheet(1);
        var used = ws.RangeUsed();
        if (used is null) return [];
        var rows = new List<string[]>();
        var lastCol = used.LastColumn().ColumnNumber();
        foreach (var r in ws.Rows(1, used.LastRow().RowNumber()))
        {
            var cells = new string[lastCol];
            for (var c = 1; c <= lastCol; c++) cells[c - 1] = CellText(r.Cell(c));
            rows.Add(cells);
        }
        return rows;
    }

    private static string CellText(IXLCell cell)
    {
        var v = cell.Value;
        if (v.IsBlank) return "";
        if (v.IsDateTime)
        {
            var dt = v.GetDateTime();
            if (dt.Year < 1901) return dt.ToString("HH:mm");                 // time-only cell
            return dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("yyyy-MM-dd") : dt.ToString("yyyy-MM-dd HH:mm");
        }
        if (v.IsTimeSpan) return TimeOnly.FromTimeSpan(v.GetTimeSpan()).ToString("HH:mm");
        if (v.IsNumber)
        {
            var n = v.GetNumber();
            // A bare fraction is an Excel time the sheet didn't format as one.
            return n is > 0 and < 1 ? TimeOnly.FromTimeSpan(TimeSpan.FromDays(n)).ToString("HH:mm") : n.ToString(Us);
        }
        return v.ToString(Us).Trim();
    }

    // ── Parsing + preview ────────────────────────────────────────────────────

    private enum Col { Day, Start, End, Title, Type, Location, Info, Description, Presenter, Track }

    private static readonly Dictionary<string, Col> HeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["day"] = Col.Day, ["date"] = Col.Day,
        ["start"] = Col.Start, ["time"] = Col.Start, ["start time"] = Col.Start, ["times"] = Col.Start,
        ["end"] = Col.End, ["end time"] = Col.End,
        ["title"] = Col.Title, ["event"] = Col.Title, ["name"] = Col.Title, ["session"] = Col.Title, ["what"] = Col.Title,
        ["type"] = Col.Type, ["category"] = Col.Type,
        ["location"] = Col.Location, ["room"] = Col.Location, ["where"] = Col.Location, ["place"] = Col.Location,
        ["additional info"] = Col.Info, ["notes"] = Col.Info, ["note"] = Col.Info, ["info"] = Col.Info,
        ["description"] = Col.Description, ["details"] = Col.Description,
        ["presenter"] = Col.Presenter, ["speaker"] = Col.Presenter,
        ["track attendance"] = Col.Track, ["attendance"] = Col.Track, ["check-in"] = Col.Track, ["check in"] = Col.Track,
    };

    private static readonly Col[] TemplateOrder = [Col.Day, Col.Start, Col.End, Col.Title, Col.Type, Col.Location, Col.Info, Col.Description, Col.Presenter, Col.Track];

    /// <summary>Turns a table (header row optional) into rows resolved against the event, for preview.</summary>
    public async Task<List<ImportRow>> ParseAsync(Guid eventId, List<string[]> table)
    {
        using var db = factory.CreateDbContext();
        var ev = await db.Events.AsNoTracking().FirstAsync(e => e.EventId == eventId);
        var enabled = await typeSvc.GetForEventAsync(eventId);
        var allTypes = await db.ScheduleItemTypes.AsNoTracking().OrderBy(t => t.SortOrder).ToListAsync();
        var locations = await db.Locations.AsNoTracking().Select(l => new Location { LocationId = l.LocationId, Name = l.Name }).ToListAsync();
        var existing = (await db.ScheduleItems.AsNoTracking().Where(i => i.CampEventId == eventId)
                .Select(i => new { i.CampDay, i.StartTime, i.Title }).ToListAsync())
            .Select(i => Key(i.CampDay, i.StartTime, i.Title)).ToHashSet();

        var nonEmpty = table.Select((cells, i) => (cells, line: i + 1)).Where(x => x.cells.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();
        if (nonEmpty.Count == 0) return [];

        // Header row: two or more cells that name a known column.
        var map = new Dictionary<Col, int>();
        var first = nonEmpty[0].cells;
        for (var c = 0; c < first.Length; c++)
            if (HeaderNames.TryGetValue(first[c].Trim().TrimEnd(':'), out var col) && !map.ContainsKey(col)) map[col] = c;
        var hasHeader = map.Count >= 2;
        if (!hasHeader)
        {
            map.Clear();
            for (var c = 0; c < TemplateOrder.Length; c++) map[TemplateOrder[c]] = c;
        }

        var defaultType = enabled.FirstOrDefault(t => t.SystemName == "Activity") ?? enabled.FirstOrDefault() ?? allTypes.First();
        var seen = new HashSet<string>();
        var result = new List<ImportRow>();
        foreach (var (cells, line) in nonEmpty.Skip(hasHeader ? 1 : 0))
        {
            string? Get(Col col) => map.TryGetValue(col, out var i) && i < cells.Length && !string.IsNullOrWhiteSpace(cells[i]) ? cells[i].Trim() : null;
            var row = new ImportRow { RowNumber = line, Title = Get(Col.Title) ?? "" };

            if (string.IsNullOrWhiteSpace(row.Title)) row.Errors.Add("No title");

            if (TryParseDay(Get(Col.Day), ev, out var day, out var dayError)) row.Day = day;
            else row.Errors.Add(dayError!);

            var startText = Get(Col.Start);
            if (TryParseTimeRange(startText, out var start, out var rangeEnd))
            {
                row.Start = start;
                row.End = rangeEnd;
            }
            else row.Errors.Add(startText is null ? "No start time" : $"Can't read \"{startText}\" as a time");

            if (Get(Col.End) is { } endText)
            {
                if (TryParseTime(endText, out var end)) row.End = end;
                else row.Warnings.Add($"Ignored end time \"{endText}\"");
            }

            var typeText = Get(Col.Type);
            if (typeText is null) row.Type = defaultType;
            else if (allTypes.FirstOrDefault(t => t.Name.Equals(typeText, StringComparison.OrdinalIgnoreCase)
                                               || t.SystemName.Equals(typeText, StringComparison.OrdinalIgnoreCase)) is { } type)
            {
                row.Type = type;
                if (enabled.All(e => e.ScheduleItemTypeId != type.ScheduleItemTypeId))
                {
                    row.EnableType = true;
                    row.Warnings.Add($"Type \"{type.Name}\" will be turned on for this event");
                }
            }
            else
            {
                row.Type = defaultType;
                row.Warnings.Add($"Unknown type \"{typeText}\" — using {defaultType.Name}");
            }

            if (Get(Col.Location) is { } locText)
            {
                var loc = locations.FirstOrDefault(l => l.Name.Equals(locText, StringComparison.OrdinalIgnoreCase));
                if (loc is not null) row.Location = loc;
                else
                {
                    row.NewLocationName = locText;
                    row.Warnings.Add($"New location \"{locText}\"");
                }
            }

            row.LocationOther   = Get(Col.Info);
            row.Description     = Get(Col.Description);
            row.Presenter       = Get(Col.Presenter);
            row.TrackAttendance = Get(Col.Track) is { } tr && tr.Trim().ToLowerInvariant() is "yes" or "y" or "true" or "x" or "✓" or "1";

            if (row.Day is { } d && row.Start is { } s && row.Title.Length > 0)
            {
                var key = Key(d, s, row.Title);
                if (existing.Contains(key)) row.Duplicate = true;
                else if (!seen.Add(key)) row.Errors.Add("Same day, time, and title as an earlier row");
            }
            result.Add(row);
        }
        return result;
    }

    private static string Key(DateOnly day, TimeOnly start, string title) => $"{day:yyyyMMdd}|{start:HHmm}|{title.Trim().ToLowerInvariant()}";

    /// <summary>
    /// Adds the importable rows in one save. Unknown locations are created (or, with
    /// <paramref name="createLocations"/> off, their text goes into Additional Info), and types
    /// used but not enabled for the event are enabled. Returns how many items were added.
    /// </summary>
    public async Task<int> ImportAsync(Guid eventId, IEnumerable<ImportRow> rows, bool createLocations, Guid userId)
    {
        var toAdd = rows.Where(r => r.CanImport).ToList();
        if (toAdd.Count == 0) return 0;

        using var db = factory.CreateDbContext();
        var newLocations = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var nextSort = (await db.Locations.MaxAsync(l => (int?)l.SortOrder) ?? 0) + 1;
        if (createLocations)
        {
            foreach (var name in toAdd.Where(r => r.NewLocationName is not null).Select(r => r.NewLocationName!).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var existingLoc = await db.Locations.FirstOrDefaultAsync(l => l.Name.ToLower() == name.ToLower());
                if (existingLoc is not null) { newLocations[name] = existingLoc.LocationId; continue; }
                var id = Guid.NewGuid();
                db.Locations.Add(new Location { LocationId = id, Name = name, SortOrder = nextSort++ });
                newLocations[name] = id;
            }
        }

        var enabledIds = await db.EventScheduleItemTypes.Where(t => t.EventId == eventId).Select(t => t.ScheduleItemTypeId).ToListAsync();
        foreach (var typeId in toAdd.Select(r => r.Type!.ScheduleItemTypeId).Distinct().Where(id => !enabledIds.Contains(id)))
            db.EventScheduleItemTypes.Add(new EventScheduleItemType { EventId = eventId, ScheduleItemTypeId = typeId });

        foreach (var r in toAdd)
        {
            Guid? locationId = r.Location?.LocationId;
            var other = r.LocationOther;
            if (r.NewLocationName is not null)
            {
                if (createLocations) locationId = newLocations[r.NewLocationName];
                else other = string.IsNullOrEmpty(other) ? r.NewLocationName : $"{r.NewLocationName} · {other}";
            }
            db.ScheduleItems.Add(new ScheduleItem
            {
                ScheduleItemId     = Guid.NewGuid(),
                CampEventId        = eventId,
                CampDay            = r.Day!.Value,
                StartTime          = r.Start!.Value,
                EndTime            = r.End,
                Title              = r.Title.Trim(),
                Description        = r.Description,
                LocationId         = locationId,
                LocationOther      = other,
                ScheduleItemTypeId = r.Type!.ScheduleItemTypeId,
                PresenterName      = r.Presenter,
                TrackAttendance    = r.TrackAttendance,
                AppliesToAllGroups = true,
                CreatedBy          = userId,
                UpdatedAt          = CampTime.Now
            });
        }
        await db.SaveChangesAsync();

        typeSvc.InvalidateEvent(eventId);
        foreach (var d in toAdd.Select(r => r.Day!.Value).Distinct()) scheduleSvc.Invalidate(eventId, d);
        return toAdd.Count;
    }

    // ── Template ─────────────────────────────────────────────────────────────

    /// <summary>An .xlsx for the event: headers, dropdowns for Day/Type/Location/Track, and instructions.</summary>
    public async Task<(string FileName, byte[] Content)?> BuildTemplateAsync(Guid eventId)
    {
        using var db = factory.CreateDbContext();
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return null;
        var types = await typeSvc.GetForEventAsync(eventId);
        var locations = await db.Locations.AsNoTracking().OrderBy(l => l.SortOrder).ThenBy(l => l.Name).Select(l => l.Name).ToListAsync();
        var days = Enumerable.Range(0, ev.ExpDate.DayNumber - ev.EffDate.DayNumber + 1)
            .Select(i => ev.EffDate.AddDays(i).ToString("ddd M/d/yyyy", Us)).ToList();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Schedule");
        string[] headers = ["Day", "Start", "End", "Title", "Type", "Location", "Additional Info", "Description", "Presenter", "Track Attendance"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        var head = ws.Range(1, 1, 1, headers.Length);
        head.Style.Font.Bold = true;
        head.Style.Fill.BackgroundColor = XLColor.FromHtml("#F5C800");
        head.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        ws.SheetView.FreezeRows(1);
        double[] widths = [18, 11, 11, 34, 16, 22, 26, 40, 22, 17];
        for (var c = 0; c < widths.Length; c++) ws.Column(c + 1).Width = widths[c];
        ws.Range("B2:C500").Style.NumberFormat.Format = "h:mm AM/PM";

        var lists = wb.Worksheets.Add("Lists");
        for (var i = 0; i < days.Count; i++) lists.Cell(i + 1, 1).Value = days[i];
        for (var i = 0; i < types.Count; i++) lists.Cell(i + 1, 2).Value = types[i].Name;
        for (var i = 0; i < locations.Count; i++) lists.Cell(i + 1, 3).Value = locations[i];
        lists.Cell(1, 4).Value = "Yes"; lists.Cell(2, 4).Value = "No";
        lists.Visibility = XLWorksheetVisibility.Hidden;

        // Named ranges keep the dropdowns in the standard validation format (a direct cross-sheet
        // reference is written as an Excel-only extension that Google Sheets/LibreOffice may drop).
        void Dropdown(string range, int col, int count, bool strict, string message)
        {
            if (count == 0) return;
            var name = $"List{col}";
            wb.DefinedNames.Add(name, lists.Range(1, col, count, col));
            var dv = ws.Range(range).CreateDataValidation();
            dv.List("=" + name, true);
            dv.ErrorStyle = strict ? XLErrorStyle.Stop : XLErrorStyle.Warning;
            dv.ErrorMessage = message;
        }
        Dropdown("A2:A500", 1, days.Count, true, "Pick one of the event's days from the list.");
        Dropdown("E2:E500", 2, types.Count, false, "Not one of this event's types — it will fall back to the default type.");
        Dropdown("F2:F500", 3, locations.Count, false, "Not an existing location — it will be created when you import.");
        Dropdown("J2:J500", 4, 2, true, "Yes or No.");

        var help = wb.Worksheets.Add("How to fill this in");
        string[] lines =
        [
            $"Schedule template for {ev.Name} ({ev.EffDate:MMM d} – {ev.ExpDate:MMM d, yyyy})",
            "",
            "One row per schedule item on the Schedule sheet. Only Day, Start, and Title are required.",
            "",
            "Day — pick from the dropdown. You can also type a date (10/17), a weekday (Saturday), or Day 1.",
            "Start / End — like 9:30 AM or 2pm. A range like 9:00 - 10:30 AM in Start fills both.",
            "        Without AM/PM, 7-11 are read as morning and 12-6 as afternoon/evening. Add AM/PM to be sure.",
            "Type — pick from the dropdown (blank = Activity).",
            "Location — pick an existing location, or type a new one and it will be created.",
            "Additional Info — short extra detail shown with the location (e.g. \"Bring a towel\").",
            "Description, Presenter — optional.",
            "Track Attendance — Yes to show the check-in button and QR code for this item.",
            "",
            "When you're done: Admin → Schedule → Import spreadsheet, and upload this file.",
            "You'll see a preview before anything is saved. Rows already on the schedule are skipped.",
            "Breakout sessions (pick-one options) are set up on the Schedule page after importing.",
        ];
        for (var i = 0; i < lines.Length; i++) help.Cell(i + 1, 1).Value = lines[i];
        help.Cell(1, 1).Style.Font.Bold = true;
        help.Cell(1, 1).Style.Font.FontSize = 14;
        help.Column(1).Width = 110;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var slug = Regex.Replace(ev.Name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return ($"schedule-template-{slug}.xlsx", ms.ToArray());
    }

    // ── Copy from another event ──────────────────────────────────────────────

    /// <summary>Other events with a schedule, same kind (event type) first, then newest first.</summary>
    public async Task<List<(Event Event, bool SameKind, int Items)>> GetCopySourcesAsync(Guid targetEventId)
    {
        using var db = factory.CreateDbContext();
        var target = await db.Events.AsNoTracking().FirstAsync(e => e.EventId == targetEventId);
        var rows = await db.Events.AsNoTracking()
            .Where(e => e.EventId != targetEventId)
            .Select(e => new { Event = e, Items = db.ScheduleItems.Count(i => i.CampEventId == e.EventId) })
            .Where(x => x.Items > 0)
            .ToListAsync();
        return rows
            .Select(x => (x.Event, x.Event.EventTypeId == target.EventTypeId, x.Items))
            .OrderByDescending(x => x.Item2).ThenByDescending(x => x.Event.EffDate)
            .ToList();
    }

    public async Task<CopyPreview?> PreviewCopyAsync(Guid sourceEventId, Guid targetEventId)
    {
        using var db = factory.CreateDbContext();
        var source = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == sourceEventId);
        var target = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == targetEventId);
        if (source is null || target is null) return null;
        var items = await db.ScheduleItems.AsNoTracking().Where(i => i.CampEventId == sourceEventId)
            .Select(i => new { i.CampDay, i.StartTime, i.Title }).ToListAsync();
        var existing = (await db.ScheduleItems.AsNoTracking().Where(i => i.CampEventId == targetEventId)
                .Select(i => new { i.CampDay, i.StartTime, i.Title }).ToListAsync())
            .Select(i => Key(i.CampDay, i.StartTime, i.Title)).ToHashSet();

        var days = items.GroupBy(i => i.CampDay).OrderBy(g => g.Key).Select(g =>
        {
            var targetDay = MapDay(g.Key, source, target);
            var dupes = targetDay is { } td ? g.Count(i => existing.Contains(Key(td, i.StartTime, i.Title))) : 0;
            return new CopyDay(g.Key, targetDay, g.Count(), dupes);
        }).ToList();
        return new CopyPreview(source, days);
    }

    // Day N of the source becomes day N of the target; days past the target's end are dropped.
    private static DateOnly? MapDay(DateOnly sourceDay, Event source, Event target)
    {
        var mapped = target.EffDate.AddDays(sourceDay.DayNumber - source.EffDate.DayNumber);
        return mapped >= target.EffDate && mapped <= target.ExpDate ? mapped : null;
    }

    /// <summary>
    /// Copies the source schedule into the target by day position. Breakout slots keep their
    /// options; activities and group assignments are matched by name in the target event. Items
    /// already on the target (same day, start, title) are skipped, along with a skipped slot's
    /// options. Returns how many items were added.
    /// </summary>
    public async Task<int> CopyAsync(Guid sourceEventId, Guid targetEventId, Guid userId)
    {
        using var db = factory.CreateDbContext();
        var source = await db.Events.AsNoTracking().FirstAsync(e => e.EventId == sourceEventId);
        var target = await db.Events.AsNoTracking().FirstAsync(e => e.EventId == targetEventId);
        var items = await db.ScheduleItems.AsNoTracking()
            .Where(i => i.CampEventId == sourceEventId)
            .Include(i => i.Activity)
            .Include(i => i.ItemGroups).ThenInclude(g => g.Group)
            .Include(i => i.ItemGroups).ThenInclude(g => g.Activity)
            .ToListAsync();
        var existing = (await db.ScheduleItems.AsNoTracking().Where(i => i.CampEventId == targetEventId)
                .Select(i => new { i.CampDay, i.StartTime, i.Title }).ToListAsync())
            .Select(i => Key(i.CampDay, i.StartTime, i.Title)).ToHashSet();
        var activities = await db.Activities.AsNoTracking().Where(a => a.EventId == targetEventId).ToListAsync();
        var groups = await db.Groups.AsNoTracking().Where(g => g.EventId == targetEventId).ToListAsync();
        Guid? ActivityByName(Activity? a) =>
            a is null ? null : activities.FirstOrDefault(x => x.Name.Equals(a.Name, StringComparison.OrdinalIgnoreCase))?.ActivityId;

        // Slots first so options can point at their new ids.
        var newIds = new Dictionary<Guid, Guid>();
        var added = new List<ScheduleItem>();
        foreach (var i in items.OrderBy(i => i.ParentScheduleItemId is null ? 0 : 1))
        {
            if (MapDay(i.CampDay, source, target) is not { } day) continue;
            if (existing.Contains(Key(day, i.StartTime, i.Title))) continue;
            Guid? parent = null;
            if (i.ParentScheduleItemId is { } p)
            {
                if (!newIds.TryGetValue(p, out var np)) continue;   // its slot was skipped
                parent = np;
            }

            var id = Guid.NewGuid();
            newIds[i.ScheduleItemId] = id;
            var itemGroups = i.ItemGroups
                .Select(g => (g, target: groups.FirstOrDefault(x => x.Name.Equals(g.Group.Name, StringComparison.OrdinalIgnoreCase))))
                .Where(x => x.target is not null)
                .Select(x => new ScheduleItemGroup
                {
                    ScheduleItemId = id,
                    GroupId        = x.target!.GroupId,
                    ActivityId     = ActivityByName(x.g.Activity),
                    LocationId     = x.g.LocationId,
                    Note           = x.g.Note
                }).ToList();
            added.Add(new ScheduleItem
            {
                ScheduleItemId       = id,
                CampEventId          = targetEventId,
                CampDay              = day,
                StartTime            = i.StartTime,
                EndTime              = i.EndTime,
                Title                = i.Title,
                Description          = i.Description,
                LocationId           = i.LocationId,
                LocationOther        = i.LocationOther,
                ActivityId           = ActivityByName(i.Activity),
                ScheduleItemTypeId   = i.ScheduleItemTypeId,
                PresenterName        = i.PresenterName,
                PresenterBio         = i.PresenterBio,
                AppliesToAllGroups   = itemGroups.Count == 0,
                MaxCapacity          = i.MaxCapacity,
                TrackAttendance      = i.TrackAttendance,
                SelfCheckInMode      = i.SelfCheckInMode,
                IsBreakoutSlot       = i.IsBreakoutSlot,
                AllowSelfSignup      = i.AllowSelfSignup,
                ParentScheduleItemId = parent,
                CreatedBy            = userId,
                UpdatedAt            = CampTime.Now,
                ItemGroups           = itemGroups
            });
        }
        if (added.Count == 0) return 0;

        var enabledIds = await db.EventScheduleItemTypes.Where(t => t.EventId == targetEventId).Select(t => t.ScheduleItemTypeId).ToListAsync();
        foreach (var typeId in added.Select(a => a.ScheduleItemTypeId).Distinct().Where(t => !enabledIds.Contains(t)))
            db.EventScheduleItemTypes.Add(new EventScheduleItemType { EventId = targetEventId, ScheduleItemTypeId = typeId });

        db.ScheduleItems.AddRange(added);
        await db.SaveChangesAsync();

        typeSvc.InvalidateEvent(targetEventId);
        foreach (var d in added.Select(a => a.CampDay).Distinct()) scheduleSvc.Invalidate(targetEventId, d);
        return added.Count;
    }
}
