using HolidayApp.Infrastructure.Services;
using HolidayApp.Shared;

namespace HolidayApp.Web.Endpoints;

public static class PlanEndpoints
{
    public static RouteGroupBuilder MapPlanEndpoints(this RouteGroupBuilder api)
    {
        // GET /api/vacations — every vacation with its plan summary
        api.MapGet("/vacations", async (IVacationPlanService service, CancellationToken ct) =>
            Results.Ok(await service.GetVacationsAsync(ct)))
            .WithTags("Plans");

        var group = api.MapGroup("/events/{id:guid}/plan").WithTags("Plans");

        // GET /api/events/{id}/plan
        group.MapGet("/", async (Guid id, IVacationPlanService service, CancellationToken ct) =>
        {
            var plan = await service.GetPlanAsync(id, ct);
            return plan is null ? Results.NotFound() : Results.Ok(plan);
        });

        // PUT /api/events/{id}/plan
        group.MapPut("/", async (Guid id, VacationPlanDto plan, IVacationPlanService service, CancellationToken ct) =>
        {
            // Lists sent as null become empty
            plan.Bookings ??= [];
            plan.Days ??= [];
            plan.Tasks ??= [];
            plan.Expenses ??= [];
            plan.Places ??= [];

            var result = await service.SavePlanAsync(id, plan, ct);
            return result switch
            {
                { Errors: { } errors } => Results.ValidationProblem(errors),
                { Denied: true } => Results.Problem(
                    title: "You can only view this trip. Ask the person who created it to let you edit.",
                    statusCode: StatusCodes.Status403Forbidden),
                { Conflict: true } => Results.Problem(
                    title: "Someone else saved changes to this trip after you opened it. "
                           + "Reload the page to see the new version and make your changes again.",
                    statusCode: StatusCodes.Status409Conflict),
                { Plan: { } saved } => Results.Ok(saved),
                _ => Results.NotFound(),
            };
        });

        return api;
    }
}
