using HolidayApp.Domain.Holidays;

namespace HolidayApp.Domain.Entities;

/// <summary>Preferences of each user.</summary>
public sealed class UserSettings
{
    /// <summary>Legal minimum in Portugal (working days per year).</summary>
    public const int DefaultAnnualVacationDays = 22;

    public required string UserId { get; set; }
    public int AnnualVacationDays { get; set; } = DefaultAnnualVacationDays;
    public bool IncludeCarnival { get; set; }
    public string? MunicipalHolidayName { get; set; }
    public int? MunicipalHolidayMonth { get; set; }
    public int? MunicipalHolidayDay { get; set; }

    public HolidayOptions ToHolidayOptions() =>
        new(IncludeCarnival, MunicipalHolidayName, MunicipalHolidayMonth, MunicipalHolidayDay);
}
