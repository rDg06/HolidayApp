using System.Net.Http.Json;
using HolidayApp.Infrastructure.Persistence;
using HolidayApp.Shared;
using HolidayApp.Web.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HolidayApp.Api.Tests;

/// <summary>Older databases keep working: created before migrations, or with Portuguese enum values.</summary>
public sealed class LegacyDatabaseTests
{
    [Fact]
    public async Task Old_database_is_upgraded_without_losing_entries()
    {
        await using var factory = new AppFactory();

        // Schema of the first version, without migration history, with one entry inside
        await using (var connection = new SqliteConnection($"Data Source={factory.DatabasePath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE "Events" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Events" PRIMARY KEY,
                    "UserId" TEXT NOT NULL, "Title" TEXT NOT NULL,
                    "StartDate" TEXT NOT NULL, "EndDate" TEXT NOT NULL,
                    "Type" TEXT NOT NULL, "Notes" TEXT NULL,
                    "CreatedAtUtc" TEXT NOT NULL, "UpdatedAtUtc" TEXT NULL);
                CREATE TABLE "UserSettings" (
                    "UserId" TEXT NOT NULL CONSTRAINT "PK_UserSettings" PRIMARY KEY,
                    "AnnualVacationDays" INTEGER NOT NULL, "IncludeCarnival" INTEGER NOT NULL,
                    "MunicipalHolidayName" TEXT NULL, "MunicipalHolidayMonth" INTEGER NULL, "MunicipalHolidayDay" INTEGER NULL);
                INSERT INTO "Events" VALUES ('01A184B1-486A-711F-8543-9C92822BF6B4', 'local', 'Algarve',
                    '2026-08-03', '2026-08-14', 'Ferias', NULL, '2026-01-01 00:00:00', NULL);
                """;
            await command.ExecuteNonQueryAsync();
        }
        SqliteConnection.ClearAllPools();

        var client = factory.CreateClient(); // starts the app: applies the missing migrations

        var events = await client.GetFromJsonAsync<List<CalendarEventDto>>("api/events?year=2026");
        var algarve = Assert.Single(events!);
        Assert.Equal("Algarve", algarve.Title);
        Assert.Equal(EventType.Vacation, algarve.Type); // "Ferias" was converted to "Vacation"

        // The newer table (plans) exists and works
        var response = await client.PutAsJsonAsync($"api/events/{events![0].Id}/plan", new VacationPlanDto { Destination = "Faro" });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Portuguese_enum_values_are_converted_to_English()
    {
        await using var factory = new AppFactory();
        var eventId = Guid.Parse("01A184B1-486A-711F-8543-9C92822BF6B5");

        // Database as it was before the English migration, with the old values in the entry and the plan
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={factory.DatabasePath}").Options;
        await using (var db = new AppDbContext(options, new FixedUser()))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261119230850_Contas");
            await db.Database.ExecuteSqlRawAsync(EscapeBraces($$"""
                INSERT INTO "Events" VALUES ('{{eventId.ToString().ToUpperInvariant()}}', 'local', 'Roma', '2026-11-02', '2026-11-06', 'Ferias', NULL, '2026-01-01 00:00:00', NULL);
                INSERT INTO "VacationPlans" ("EventId", "UserId", "Content", "UpdatedAtUtc") VALUES ('{{eventId.ToString().ToUpperInvariant()}}', 'local',
                    '{"routeMode":"Carro","bookings":[{"type":"Voo","name":"Voo"},{"type":"Alojamento","name":"Hotel"}],"expenses":[{"category":"Alimentacao","actual":20}],"places":[{"name":"Termini","kind":"Comboio"}],"days":[],"tasks":[]}',
                    '2026-01-01 00:00:00');
                """));
        }
        SqliteConnection.ClearAllPools();

        var client = factory.CreateClient(); // applies EnglishEnumValues

        var entry = await client.GetFromJsonAsync<CalendarEventDto>($"api/events/{eventId}");
        var plan = await client.GetFromJsonAsync<VacationPlanDto>($"api/events/{eventId}/plan");

        Assert.Equal(EventType.Vacation, entry!.Type);
        Assert.Equal(RouteMode.Car, plan!.RouteMode);
        Assert.Equal([BookingType.Flight, BookingType.Lodging], plan.Bookings.Select(b => b.Type));
        Assert.Equal("Voo", plan.Bookings[0].Name); // user text is not touched
        Assert.Equal(ExpenseCategory.Food, Assert.Single(plan.Expenses).Category);
        Assert.Equal(PlaceKind.Train, Assert.Single(plan.Places).Kind);
    }

    /// <summary>ExecuteSqlRaw reads braces as placeholders: the JSON braces are doubled.</summary>
    private static string EscapeBraces(string sql) => sql.Replace("{", "{{").Replace("}", "}}");

    private sealed class FixedUser : Domain.Abstractions.ICurrentUser
    {
        public string UserId => HttpCurrentUser.LocalUserId;
    }
}
