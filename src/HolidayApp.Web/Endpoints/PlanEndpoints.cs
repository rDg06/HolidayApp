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
        group.MapPut("/", async (Guid id, VacationPlanDto plan, IVacationPlanService service, ICalendarService calendar, CancellationToken ct) =>
        {
            // Listas enviadas como null passam a vazias
            plan.Bookings ??= [];
            plan.Days ??= [];
            plan.Tasks ??= [];
            plan.Expenses ??= [];
            plan.Places ??= [];

            // As datas da marcação limitam os dias das reservas
            var ev = await calendar.GetEventAsync(id, ct);
            if (ev is null)
                return Results.NotFound();

            var errors = VacationPlanValidator.Validate(plan, ev.StartDate, ev.EndDate);
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var saved = await service.SavePlanAsync(id, plan, ct);
            return saved is null ? Results.NotFound() : Results.Ok(saved);
        });

        return api;
    }
}
