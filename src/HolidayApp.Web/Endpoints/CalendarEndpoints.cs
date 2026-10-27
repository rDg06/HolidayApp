using HolidayApp.Infrastructure.Services;

namespace HolidayApp.Web.Endpoints;

public static class CalendarEndpoints
{
    public static RouteGroupBuilder MapCalendarEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/calendar/{year:int}").WithTags("Calendário");

        // GET /api/calendar/2026/holidays
        group.MapGet("/holidays", async (int year, ICalendarService service, CancellationToken ct) =>
            YearRange.IsValid(year)
                ? Results.Ok(await service.GetHolidaysAsync(year, ct))
                : YearRange.Invalid());

        // GET /api/calendar/2026/summary
        group.MapGet("/summary", async (int year, ICalendarService service, CancellationToken ct) =>
            YearRange.IsValid(year)
                ? Results.Ok(await service.GetSummaryAsync(year, ct))
                : YearRange.Invalid());

        return api;
    }
}

internal static class YearRange
{
    public const int Min = 1900;
    public const int Max = 2200;

    public static bool IsValid(int year) => year is >= Min and <= Max;

    public static IResult Invalid() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["year"] = [$"O ano tem de estar entre {Min} e {Max}."],
    });
}
