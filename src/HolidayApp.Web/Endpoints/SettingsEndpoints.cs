using HolidayApp.Infrastructure.Services;
using HolidayApp.Shared;
using HolidayApp.Shared.Validation;

namespace HolidayApp.Web.Endpoints;

public static class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/settings").WithTags("Definições");

        // GET /api/settings
        group.MapGet("/", async (ICalendarService service, CancellationToken ct) =>
            Results.Ok(await service.GetSettingsAsync(ct)));

        // PUT /api/settings
        group.MapPut("/", async (UserSettingsDto settings, ICalendarService service, CancellationToken ct) =>
        {
            var errors = SettingsValidator.Validate(settings);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            return Results.Ok(await service.UpdateSettingsAsync(settings, ct));
        });

        return api;
    }
}
