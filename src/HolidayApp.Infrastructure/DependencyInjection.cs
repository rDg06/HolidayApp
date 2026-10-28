using System.Reflection;
using HolidayApp.Domain.Holidays;
using HolidayApp.Infrastructure.Persistence;
using HolidayApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
        services.AddScoped<IVacationPlanService, VacationPlanService>();

        return services;
    }

    /// <summary>
    /// Cria a base de dados ou atualiza-a para a versão mais recente (migrações).
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await BaselineLegacyDatabaseAsync(db, ct);
        await db.Database.MigrateAsync(ct);
    }

    /// <summary>
    /// Bases de dados criadas antes das migrações (com EnsureCreated) já têm as
    /// tabelas da primeira migração mas não o histórico. Regista essa migração
    /// como aplicada para o MigrateAsync não tentar criar as tabelas outra vez
    /// e os dados existentes serem mantidos.
    /// </summary>
    private static async Task BaselineLegacyDatabaseAsync(AppDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsSqlite() || !await db.Database.CanConnectAsync(ct))
            return;

        if (!await SqliteTableExistsAsync(db, "Events", ct) || await SqliteTableExistsAsync(db, HistoryRepository.DefaultTableName, ct))
            return;

        var history = db.GetService<IHistoryRepository>();
        var initialMigration = db.Database.GetMigrations().First();
        var efVersion = typeof(DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "10.0.0";

        await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), ct);
        await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(initialMigration, efVersion)), ct);
    }

    private static async Task<bool> SqliteTableExistsAsync(AppDbContext db, string table, CancellationToken ct) =>
        await db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM sqlite_master WHERE type = 'table' AND name = {table}")
            .SingleAsync(ct) > 0;
}
