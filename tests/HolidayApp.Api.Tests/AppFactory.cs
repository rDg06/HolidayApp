using HolidayApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Api.Tests;

/// <summary>
/// Arranca a aplicação a sério (API + base de dados) com uma base de dados SQLite
/// própria, num ficheiro temporário apagado no fim.
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

    /// <summary>Acesso direto à base de dados para verificar o que ficou gravado.</summary>
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
