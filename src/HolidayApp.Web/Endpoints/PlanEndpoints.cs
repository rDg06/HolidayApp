using HolidayApp.Infrastructure.Services;
using HolidayApp.Shared;

namespace HolidayApp.Web.Endpoints;

public static class PlanEndpoints
{
    public static RouteGroupBuilder MapPlanEndpoints(this RouteGroupBuilder api)
    {
        // GET /api/vacations — todas as férias com o resumo do planeamento
        api.MapGet("/vacations", async (IVacationPlanService service, CancellationToken ct) =>
            Results.Ok(await service.GetVacationsAsync(ct)))
            .WithTags("Planeamento");

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
            plan.Places ??= [];

            var result = await service.SavePlanAsync(id, plan, ct);
            return result switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Plan: { } saved } => Results.Ok(saved),
                _ => Results.NotFound(),
            };
        });

        return api;
    }
}
