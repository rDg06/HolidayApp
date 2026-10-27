using HolidayApp.Domain.Holidays;
using HolidayApp.Infrastructure.Persistence;
using HolidayApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HolidayApp.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Regista base de dados e serviços. O fornecedor da base de dados vem da
    /// configuração ("Database:Provider"), por isso mudar de SQLite para
    /// PostgreSQL/SQL Server não mexe no código da aplicação.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=holidayapp.db";

        services.AddDbContext<AppDbContext>(options =>
        {
            switch (provider.Trim().ToLowerInvariant())
            {
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;

                // Quando precisares de escalar:
                // case "postgres": options.UseNpgsql(connectionString); break;
                // case "sqlserver": options.UseSqlServer(connectionString); break;

                default:
                    throw new InvalidOperationException($"Fornecedor de base de dados não suportado: '{provider}'.");
            }
        });

        services.AddSingleton<IHolidayProvider, PortugueseHolidayProvider>();
        services.AddScoped<ICalendarService, CalendarService>();

        return services;
    }

    /// <summary>
    /// Cria a base de dados se ainda não existir.
    /// Quando o modelo começar a mudar, troca por migrações (ver README).
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(ct);
    }
}
