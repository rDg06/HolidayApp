using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HolidayApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VisitedPlaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VisitedPlaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitedPlaces", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisitedPlaces_UserId_Year",
                table: "VisitedPlaces",
                columns: new[] { "UserId", "Year" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisitedPlaces");
        }
    }
}
