using HolidayApp.Infrastructure.Services;
using HolidayApp.Shared;

namespace HolidayApp.Web.Endpoints;

/// <summary>Public or private trips, the people in each trip and the "Explore" page.</summary>
public static class SharingEndpoints
{
    public static RouteGroupBuilder MapSharingEndpoints(this RouteGroupBuilder api)
    {
        // GET /api/explore — public trips of everyone
        api.MapGet("/explore", async (ITripSharingService service, CancellationToken ct) =>
            Results.Ok(await service.GetPublicTripsAsync(ct)))
            .WithTags("Sharing");

        var group = api.MapGroup("/events/{id:guid}/sharing").WithTags("Sharing");

        // GET /api/events/{id}/sharing — who sees the trip and what I can do
        group.MapGet("/", async (Guid id, ITripSharingService service, CancellationToken ct) =>
            await service.GetAsync(id, ct) is { } sharing ? Results.Ok(sharing) : Results.NotFound());

        // PUT /api/events/{id}/sharing — public or private (owner)
        group.MapPut("/", async (Guid id, SetTripVisibilityRequest request, ITripSharingService service, CancellationToken ct) =>
            ToResult(await service.SetPublicAsync(id, request.IsPublic, ct)));

        // POST /api/events/{id}/sharing/members — adds someone by email or user name (owner)
        group.MapPost("/members", async (Guid id, AddTripMemberRequest request, ITripSharingService service, CancellationToken ct) =>
            ToResult(await service.AddMemberAsync(id, request.Person, request.CanEdit, ct)));

        // PUT /api/events/{id}/sharing/members/{username} — "can edit" or "view only" (owner)
        group.MapPut("/members/{username}", async (Guid id, string username, SetTripMemberRequest request, ITripSharingService service, CancellationToken ct) =>
            ToResult(await service.SetMemberAsync(id, username, request.CanEdit, ct)));

        // DELETE /api/events/{id}/sharing/members/{username} — the owner removes someone, or I leave
        group.MapDelete("/members/{username}", async (Guid id, string username, ITripSharingService service, CancellationToken ct) =>
            ToResult(await service.RemoveMemberAsync(id, username, ct)));

        return api;
    }

    private static IResult ToResult(SharingResult result) => result.Status switch
    {
        SharingStatus.Ok => result.Sharing is { } sharing ? Results.Ok(sharing) : Results.NoContent(),
        SharingStatus.Forbidden => Results.Problem(title: result.Error, statusCode: StatusCodes.Status403Forbidden),
        SharingStatus.Invalid => Results.ValidationProblem(new Dictionary<string, string[]> { ["Sharing"] = [result.Error ?? "Invalid."] }),
        _ => Results.NotFound(),
    };
}
