using System.Net.Http.Json;
using HolidayApp.Shared;

namespace HolidayApp.Client.Services;

public sealed class CalendarApi(HttpClient http) : ICalendarApi
{
    public async Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(int year, CancellationToken ct = default) =>
        await GetAsync<List<HolidayDto>>($"api/calendar/{year}/holidays", ct);

    public async Task<IReadOnlyList<CalendarEventDto>> GetEventsAsync(int year, CancellationToken ct = default) =>
        await GetAsync<List<CalendarEventDto>>($"api/events?year={year}", ct);

    public Task<YearSummaryDto> GetSummaryAsync(int year, CancellationToken ct = default) =>
        GetAsync<YearSummaryDto>($"api/calendar/{year}/summary", ct);

    public Task<CalendarEventDto> GetEventAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<CalendarEventDto>($"api/events/{id}", ct);

    public Task<CalendarEventDto> CreateEventAsync(SaveCalendarEventRequest request, CancellationToken ct = default) =>
        SendAsync<SaveCalendarEventRequest, CalendarEventDto>(HttpMethod.Post, "api/events", request, ct);

    public Task<CalendarEventDto> UpdateEventAsync(Guid id, SaveCalendarEventRequest request, CancellationToken ct = default) =>
        SendAsync<SaveCalendarEventRequest, CalendarEventDto>(HttpMethod.Put, $"api/events/{id}", request, ct);

    public async Task DeleteEventAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/events/{id}", ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<IReadOnlyList<VacationSummaryDto>> GetVacationsAsync(CancellationToken ct = default) =>
        await GetAsync<List<VacationSummaryDto>>("api/vacations", ct);

    public Task<VacationPlanDto> GetPlanAsync(Guid eventId, CancellationToken ct = default) =>
        GetAsync<VacationPlanDto>($"api/events/{eventId}/plan", ct);

    public Task<VacationPlanDto> SavePlanAsync(Guid eventId, VacationPlanDto plan, CancellationToken ct = default) =>
        SendAsync<VacationPlanDto, VacationPlanDto>(HttpMethod.Put, $"api/events/{eventId}/plan", plan, ct);

    public Task<UserSettingsDto> GetSettingsAsync(CancellationToken ct = default) =>
        GetAsync<UserSettingsDto>("api/settings", ct);

    public Task<UserSettingsDto> SaveSettingsAsync(UserSettingsDto settings, CancellationToken ct = default) =>
        SendAsync<UserSettingsDto, UserSettingsDto>(HttpMethod.Put, "api/settings", settings, ct);

    public async Task<IReadOnlyList<VisitedPlaceDto>> GetVisitedPlacesAsync(CancellationToken ct = default) =>
        await GetAsync<List<VisitedPlaceDto>>("api/visited", ct);

    public Task<VisitedPlaceDto> AddVisitedPlaceAsync(SaveVisitedPlaceRequest request, CancellationToken ct = default) =>
        SendAsync<SaveVisitedPlaceRequest, VisitedPlaceDto>(HttpMethod.Post, "api/visited", request, ct);

    public async Task DeleteVisitedPlaceAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/visited/{id}", ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<AccountDto?> GetMeAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/me", ct);
        await EnsureSuccessAsync(response, ct);
        return response.StatusCode == System.Net.HttpStatusCode.NoContent
            ? null
            : await response.Content.ReadFromJsonAsync<AccountDto>(ct);
    }

    public async Task DeleteMeAsync(CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync("api/me", ct);
        await EnsureSuccessAsync(response, ct);
    }

    public Task<AccountDto> RenameMeAsync(string name, CancellationToken ct = default) =>
        SendAsync<RenameAccountRequest, AccountDto>(HttpMethod.Put, "api/me", new RenameAccountRequest(name), ct);

    public async Task ChangePasswordAsync(string current, string replacement, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/me/password", new ChangePasswordRequest(current, replacement), ct);
        await EnsureSuccessAsync(response, ct);
    }

    // ---------- Helpers ----------

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(ct)
               ?? throw new ApiException("The server returned an empty response.");
    }

    private async Task<TResponse> SendAsync<TBody, TResponse>(HttpMethod method, string url, TBody body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        using var response = await http.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TResponse>(ct)
               ?? throw new ApiException("The server returned an empty response.");
    }

    /// <summary>Turns error responses (ProblemDetails) into a message for the user.</summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        switch (response.StatusCode)
        {
            case System.Net.HttpStatusCode.Unauthorized:
                throw new ApiException("You need to sign in to use the app.");
            case System.Net.HttpStatusCode.Forbidden:
                throw new ApiException("Your account does not have access to this app.");
        }

        var message = $"Error {(int)response.StatusCode} talking to the server.";
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(ct);
            if (problem?.Errors is { Count: > 0 } errors)
                message = string.Join(" ", errors.SelectMany(e => e.Value));
            else if (!string.IsNullOrWhiteSpace(problem?.Title))
                message = problem.Title;
        }
        catch
        {
            // The response was not JSON: keep the generic message.
        }

        throw new ApiException(message);
    }

    private sealed class ProblemPayload
    {
        public string? Title { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
