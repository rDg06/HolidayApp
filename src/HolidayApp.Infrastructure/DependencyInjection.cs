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
    /// Registers the database and services. The database provider comes from
    /// configuration ("Database:Provider"), so moving from SQLite to
    /// PostgreSQL/SQL Server does not touch the application code.
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

                // When you need to scale:
                // case "postgres": options.UseNpgsql(connectionString); break;
                // case "sqlserver": options.UseSqlServer(connectionString); break;

                default:
                    throw new InvalidOperationException($"Unsupported database provider: '{provider}'.");
            }
        });

        services.AddSingleton<IHolidayProvider, PortugueseHolidayProvider>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<IVacationPlanService, VacationPlanService>();
        services.AddScoped<IVisitedPlaceService, VisitedPlaceService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<TripAccess>();
        services.AddScoped<ITripSharingService, TripSharingService>();
        services.AddScoped<IFeedService, FeedService>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }

    /// <summary>
    /// Creates the database or updates it to the latest version (migrations).
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await BaselineLegacyDatabaseAsync(db, ct);
        await db.Database.MigrateAsync(ct);

        // Accounts from before user names get one (from the email)
        await AccountService.GiveUsernamesAsync(db, ct);
    }

    /// <summary>
    /// Databases created before migrations (with EnsureCreated) already have the
    /// tables of the first migration but not the history. Records that migration
    /// as applied so MigrateAsync does not try to create the tables again
    /// and the existing data is kept.

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
