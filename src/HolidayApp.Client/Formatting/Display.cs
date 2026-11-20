using HolidayApp.Shared;

namespace HolidayApp.Client.Formatting;

/// <summary>
/// Display texts (dates, amounts, labels). Written by hand so they do not depend on the
/// browser's culture data (WebAssembly does not always load every culture).
/// </summary>
public static class Display
{
    public static readonly string[] Months =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    ];

    public static readonly string[] MonthsShort =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    // Indexed by DayOfWeek (Sunday = 0)
    private static readonly string[] WeekdaysShort = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    public static string Short(DateOnly d) => $"{d.Day} {MonthsShort[d.Month - 1]}";

    public static string Long(DateOnly d) => $"{WeekdaysShort[(int)d.DayOfWeek]}, {d.Day} {MonthsShort[d.Month - 1]}";

    public static string Range(DateOnly start, DateOnly end)
    {
        if (start == end)
            return Long(start);

        if (start.Year == end.Year && start.Month == end.Month)
            return $"{start.Day}–{end.Day} {MonthsShort[end.Month - 1]}";

        return $"{Short(start)} – {Short(end)}";
    }

    public static string Days(int n) => n == 1 ? "1 day" : $"{n} days";

    public static string Workdays(int n) => n == 1 ? "1 working day" : $"{n} working days";

    public static string Label(EventType type) => type switch
    {
        EventType.Vacation => "Vacation",
        EventType.Personal => "Personal",
        _ => "Other",
    };

    public static string Label(HolidayKind kind) => kind switch
    {
        HolidayKind.National => "National holiday",
        HolidayKind.Optional => "Optional holiday",
        _ => "Municipal holiday",
    };

    public static string Label(BookingType type) => type switch
    {
        BookingType.Flight => "Flight",
        BookingType.Lodging => "Lodging",
        BookingType.Transport => "Transport",
        BookingType.Activity => "Activity",
        _ => "Other",
    };

    /// <summary>Duration of a route: "8 min", "1 h", "1 h 05".</summary>
    public static string Duration(double seconds)
    {
        var minutes = Math.Max(1, (int)Math.Round(seconds / 60));
        if (minutes < 60)
            return $"{minutes} min";
        var (h, m) = (minutes / 60, minutes % 60);
        return m == 0 ? $"{h} h" : $"{h} h {m:00}";
    }

    /// <summary>How you get around: "on foot", "by car", "by bike".</summary>
    public static string Label(RouteMode mode) => mode switch
    {
        RouteMode.Car => "by car",
        RouteMode.Bike => "by bike",
        _ => "on foot",
    };

    public static string Label(PlaceKind kind) => kind switch
    {
        PlaceKind.Lodging => "Lodging",
        PlaceKind.Airport => "Airport",
        PlaceKind.Metro => "Metro station",
        PlaceKind.Bus => "Bus stop",
        PlaceKind.Train => "Train station",
        _ => "Place to visit",
    };

    public static string Label(ExpenseCategory category) => category switch
    {
        ExpenseCategory.Transport => "Transport",
        ExpenseCategory.Lodging => "Lodging",
        ExpenseCategory.Food => "Food",
        ExpenseCategory.Activities => "Activities",
        ExpenseCategory.Shopping => "Shopping",
        _ => "Other",
    };

    /// <summary>Amount in euros (1234.5 → "€1,234.50").</summary>
    public static string Money(decimal value) =>
        (value < 0 ? "-€" : "€") + Math.Abs(value).ToString("#,0.00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Distance: "850 m" under 1 km, then "1.2 km" (or "35 km" from 10 km).</summary>
    public static string Km(double km) => km switch
    {
        < 1 => $"{Math.Round(km * 1000 / 10) * 10:0} m",
        < 10 => km.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " km",
        _ => $"{Math.Round(km):0} km",
    };

    /// <summary>CSS suffix for each type (ev-vacation, ev-personal, ev-other).</summary>
    public static string Css(EventType type) => "ev-" + type.ToString().ToLowerInvariant();
}
