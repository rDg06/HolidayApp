using HolidayApp.Infrastructure.Services;
using HolidayApp.Shared;
using HolidayApp.Shared.Validation;

namespace HolidayApp.Web.Endpoints;

/// <summary>Places you have been to, added by hand on the "Where I've been" map.</summary>
public static class VisitedPlaceEndpoints
{
    public static RouteGroupBuilder MapVisitedPlaceEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/visited").WithTags("Where I've been");

        // GET /api/visited
        group.MapGet("/", async (IVisitedPlaceService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));

        // POST /api/visited
        group.MapPost("/", async (SaveVisitedPlaceRequest request, IVisitedPlaceService service, TimeProvider clock, CancellationToken ct) =>
        {
            var errors = VisitedPlaceValidator.Validate(request, clock.GetLocalNow().Year);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var created = await service.AddAsync(request, ct);
            return Results.Created($"/api/visited/{created.Id}", created);
        });

        // DELETE /api/visited/{id}
        group.MapDelete("/{id:guid}", async (Guid id, IVisitedPlaceService service, CancellationToken ct) =>
            await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        return api;
    }
}
