using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HolidayApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Feed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_IsPublic",
                table: "Events");

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAtUtc",
                table: "Events",
                type: "TEXT",
                nullable: true);

            // Trips that are already public go on the feed by their last change
            migrationBuilder.Sql(
                "UPDATE \"Events\" SET \"PublishedAtUtc\" = COALESCE(\"UpdatedAtUtc\", \"CreatedAtUtc\") WHERE \"IsPublic\" = 1;");

            migrationBuilder.CreateTable(
                name: "TripComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripComments_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripLikes",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripLikes", x => new { x.EventId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TripLikes_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_IsPublic_PublishedAtUtc",
                table: "Events",
                columns: new[] { "IsPublic", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TripComments_EventId_CreatedAtUtc",
                table: "TripComments",
                columns: new[] { "EventId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TripComments_UserId",
                table: "TripComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripLikes_UserId",
                table: "TripLikes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripComments");

            migrationBuilder.DropTable(
                name: "TripLikes");

            migrationBuilder.DropIndex(
                name: "IX_Events_IsPublic_PublishedAtUtc",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "PublishedAtUtc",
                table: "Events");

            migrationBuilder.CreateIndex(
                name: "IX_Events_IsPublic",
                table: "Events",
                column: "IsPublic");
        }
    }
}
