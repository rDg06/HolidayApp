using HolidayApp.Infrastructure.Services;
using HolidayApp.Shared;
using HolidayApp.Shared.Validation;

namespace HolidayApp.Web.Endpoints;

public static class EventEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/events").WithTags("Marcações");

        // GET /api/events?year=2026
        group.MapGet("/", async (int year, ICalendarService service, CancellationToken ct) =>
        {
            if (!YearRange.IsValid(year))
                return YearRange.Invalid();

            return Results.Ok(await service.GetEventsAsync(year, ct));
        });

        // GET /api/events/{id}
        group.MapGet("/{id:guid}", async (Guid id, ICalendarService service, CancellationToken ct) =>
        {
            var found = await service.GetEventAsync(id, ct);
            return found is null ? Results.NotFound() : Results.Ok(found);
        });

        // POST /api/events
        group.MapPost("/", async (SaveCalendarEventRequest request, ICalendarService service, CancellationToken ct) =>
        {
            var errors = CalendarEventValidator.Validate(request);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var created = await service.CreateEventAsync(request, ct);
            return Results.Created($"/api/events/{created.Id}", created);
        });

        // PUT /api/events/{id}
        group.MapPut("/{id:guid}", async (Guid id, SaveCalendarEventRequest request, ICalendarService service, CancellationToken ct) =>
        {
            var errors = CalendarEventValidator.Validate(request);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var updated = await service.UpdateEventAsync(id, request, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        // DELETE /api/events/{id}
        group.MapDelete("/{id:guid}", async (Guid id, ICalendarService service, CancellationToken ct) =>
            await service.DeleteEventAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        return api;
    }
}
