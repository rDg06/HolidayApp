using System.Net;
using System.Net.Http.Json;
using HolidayApp.Shared;

namespace HolidayApp.Api.Tests;

/// <summary>Places added by hand on the "Where I've been" map.</summary>
public sealed class VisitedPlaceApiTests : IAsyncLifetime
{
    private readonly AppFactory _factory = new();
    private HttpClient _client = default!;

    public async Task InitializeAsync() => _client = await _factory.SignedInAsync();

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private static SaveVisitedPlaceRequest Paris(int year = 2019) => new(" Paris ", 48.8566, 2.3522, "France", year);

    [Fact]
    public async Task Adds_lists_by_year_and_deletes()
    {
        var paris = await (await _client.PostAsJsonAsync("api/visited", Paris(2019))).Content.ReadFromJsonAsync<VisitedPlaceDto>();
        await _client.PostAsJsonAsync("api/visited", new SaveVisitedPlaceRequest("Cusco", -13.53, -71.97, "Peru", 2023));

        var all = (await _client.GetFromJsonAsync<List<VisitedPlaceDto>>("api/visited"))!;
        Assert.Equal(["Cusco", "Paris"], all.Select(p => p.Name)); // most recent first, name trimmed
        Assert.Equal("France", all[1].Country);

        var deleted = await _client.DeleteAsync($"api/visited/{paris!.Id}");
        var again = await _client.DeleteAsync($"api/visited/{paris.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Single((await _client.GetFromJsonAsync<List<VisitedPlaceDto>>("api/visited"))!);
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(3000)]
    public async Task Year_outside_1900_to_now_is_rejected(int year)
    {
        var response = await _client.PostAsJsonAsync("api/visited", Paris(year));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task No_name_or_invalid_coordinates_is_rejected()

    {
        var noName = await _client.PostAsJsonAsync("api/visited", new SaveVisitedPlaceRequest(" ", 48.8, 2.3, null, 2020));
        var badCoordinates = await _client.PostAsJsonAsync("api/visited", new SaveVisitedPlaceRequest("X", 120, 2.3, null, 2020));

        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badCoordinates.StatusCode);
    }
}
