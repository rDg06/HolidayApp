using HolidayApp.Infrastructure.Services;
using HolidayApp.Shared.Validation;

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
                : YearRangeResults.Invalid());

        // GET /api/calendar/2026/summary
        group.MapGet("/summary", async (int year, ICalendarService service, CancellationToken ct) =>
            YearRange.IsValid(year)
                ? Results.Ok(await service.GetSummaryAsync(year, ct))
                : YearRangeResults.Invalid());

        return api;
    }
}

internal static class YearRangeResults
{
    public static IResult Invalid() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["year"] = [YearRange.Message],
    });
}
