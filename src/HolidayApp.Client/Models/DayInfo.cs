using HolidayApp.Shared;

namespace HolidayApp.Client.Models;

/// <summary>Everything on a day: holiday and/or entries.</summary>
public sealed class DayInfo
{
    public HolidayDto? Holiday { get; set; }
    public List<CalendarEventDto> Events { get; } = [];
}

/// <summary>Mouse info when hovering over a day (to drag selections).</summary>

public readonly record struct DayPointer(DateOnly Date, long Buttons);
