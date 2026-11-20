using HolidayApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Api.Tests;

/// <summary>
/// Starts the real app (API + database) with its own SQLite database,
/// in a temporary file deleted at the end.
/// </summary>
public sealed class AppFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly Dictionary<string, string?> _settings;

    public string DatabasePath { get; } = Path.Combine(Path.GetTempPath(), $"holidayapp-test-{Guid.NewGuid():N}.db");

    public AppFactory(string environment = "Development", Dictionary<string, string?>? settings = null)
    {
        _environment = environment;
        _settings = settings ?? [];
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={DatabasePath}");
        foreach (var (key, value) in _settings)
            builder.UseSetting(key, value);
    }

    /// <summary>Direct database access to check what was saved.</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        File.Delete(DatabasePath);
    }
}
