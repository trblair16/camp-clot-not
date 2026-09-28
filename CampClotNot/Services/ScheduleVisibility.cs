using CampClotNot.Data.Entities;

namespace CampClotNot.Services;

/// <summary>
/// Which schedule items a person sees, given breakout slots (issue #311). Admins see every slot
/// and option. Everyone else sees each slot once: as the option they're signed up for, or as the
/// slot itself (rendered as a "choose yours" / "will be assigned" placeholder). Options they
/// didn't pick never appear. Shared by /hub/schedule and the Dashboard so they can't disagree.
/// </summary>
public static class ScheduleVisibility
{
    /// <param name="myRegistrations">slot id → option id the person is signed up for.</param>
    public static List<ScheduleItem> Apply(
        IEnumerable<ScheduleItem> items, bool isAdmin, IReadOnlyDictionary<Guid, Guid> myRegistrations)
    {
        var list = items.ToList();
        if (isAdmin) return OrderWithOptionsUnderSlots(list);

        var byId = list.ToDictionary(i => i.ScheduleItemId);
        var result = new List<ScheduleItem>();
        foreach (var item in list)
        {
            if (item.ParentScheduleItemId is not null) continue;   // options only appear in place of their slot
            if (item.IsBreakoutSlot
                && myRegistrations.TryGetValue(item.ScheduleItemId, out var optionId)
                && byId.TryGetValue(optionId, out var option))
                result.Add(option);
            else
                result.Add(item);
        }
        return result.OrderBy(i => i.CampDay).ThenBy(i => i.StartTime).ToList();
    }

    /// <summary>Day/time order, with each slot's options listed right after it.</summary>
    public static List<ScheduleItem> OrderWithOptionsUnderSlots(IEnumerable<ScheduleItem> items)
    {
        var list = items.OrderBy(i => i.CampDay).ThenBy(i => i.StartTime).ToList();
        var ids = list.Select(i => i.ScheduleItemId).ToHashSet();
        var optionsBySlot = list.Where(i => i.ParentScheduleItemId is { } p && ids.Contains(p))
            .GroupBy(i => i.ParentScheduleItemId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.StartTime).ThenBy(o => o.Title).ToList());

        var result = new List<ScheduleItem>();
        foreach (var item in list.Where(i => i.ParentScheduleItemId is not { } p || !ids.Contains(p)))
        {
            result.Add(item);
            if (optionsBySlot.TryGetValue(item.ScheduleItemId, out var options)) result.AddRange(options);
        }
        return result;
    }
}
