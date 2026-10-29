using System.Net.Http.Json;
using HolidayApp.Shared;
using Microsoft.Data.Sqlite;

namespace HolidayApp.Api.Tests;

/// <summary>Bases de dados criadas antes das migrações (com EnsureCreated) continuam a funcionar.</summary>
public sealed class LegacyDatabaseTests
{
    [Fact]
    public async Task Base_de_dados_antiga_e_atualizada_sem_perder_marcacoes()
    {
        await using var factory = new AppFactory();

        // Esquema da primeira versão, sem histórico de migrações, com uma marcação lá dentro
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

        var client = factory.CreateClient(); // arranca a app: aplica as migrações em falta

        var events = await client.GetFromJsonAsync<List<CalendarEventDto>>("api/events?year=2026");
        Assert.Equal("Algarve", Assert.Single(events!).Title);

        // A tabela nova (planeamento) existe e funciona
        var response = await client.PutAsJsonAsync($"api/events/{events![0].Id}/plan", new VacationPlanDto { Destination = "Faro" });
        response.EnsureSuccessStatusCode();
    }
}
