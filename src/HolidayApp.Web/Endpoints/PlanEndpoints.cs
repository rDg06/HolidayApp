using HolidayApp.Infrastructure.Services;
using HolidayApp.Shared;
using HolidayApp.Shared.Validation;

namespace HolidayApp.Web.Endpoints;

public static class PlanEndpoints
{
    public static RouteGroupBuilder MapPlanEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/events/{id:guid}/plan").WithTags("Planeamento");

        // GET /api/events/{id}/plan
        group.MapGet("/", async (Guid id, IVacationPlanService service, CancellationToken ct) =>
        {
            var plan = await service.GetPlanAsync(id, ct);
            return plan is null ? Results.NotFound() : Results.Ok(plan);
        });

        // PUT /api/events/{id}/plan
        group.MapPut("/", async (Guid id, VacationPlanDto plan, IVacationPlanService service, CancellationToken ct) =>
        {
            // Listas enviadas como null passam a vazias
            plan.Bookings ??= [];
            plan.Days ??= [];
            plan.Tasks ??= [];
            plan.Expenses ??= [];

            var errors = VacationPlanValidator.Validate(plan);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var saved = await service.SavePlanAsync(id, plan, ct);
            return saved is null ? Results.NotFound() : Results.Ok(saved);
        });

        return api;
    }
}
