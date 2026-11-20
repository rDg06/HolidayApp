using HolidayApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HolidayApp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Converts the stored enum values from Portuguese to English: the event type column and the
    /// enum values inside the saved vacation plans (JSON). Data only, the schema does not change.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261120000000_EnglishEnumValues")]
    public partial class EnglishEnumValues : Migration
    {
        /// <summary>Event type column: old value → new value.</summary>
        private static readonly (string Old, string New)[] EventTypes =
        [
            ("Ferias", "Vacation"),
            ("Pessoal", "Personal"),
            ("Outro", "Other"),
        ];

        /// <summary>
        /// Enum values inside the plan JSON, per property (the property name keeps user text,
        /// such as a booking called "Voo", from being touched).
        /// </summary>
        private static readonly (string Property, string Old, string New)[] PlanValues =
        [
            // Bookings ("type")
            ("type", "Voo", "Flight"),
            ("type", "Alojamento", "Lodging"),
            ("type", "Transporte", "Transport"),
            ("type", "Atividade", "Activity"),
            ("type", "Outro", "Other"),
            // Expenses ("category")
            ("category", "Transporte", "Transport"),
            ("category", "Alojamento", "Lodging"),
            ("category", "Alimentacao", "Food"),
            ("category", "Atividades", "Activities"),
            ("category", "Compras", "Shopping"),
            ("category", "Outro", "Other"),
            // Places ("kind")
            ("kind", "Visita", "Visit"),
            ("kind", "Alojamento", "Lodging"),
            ("kind", "Aeroporto", "Airport"),
            ("kind", "Autocarro", "Bus"),
            ("kind", "Comboio", "Train"),
            // How to get around ("routeMode")
            ("routeMode", "APe", "Walk"),
            ("routeMode", "Carro", "Car"),
            ("routeMode", "Bicicleta", "Bike"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => Convert(migrationBuilder, toEnglish: true);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => Convert(migrationBuilder, toEnglish: false);

        private static void Convert(MigrationBuilder migrationBuilder, bool toEnglish)
        {
            foreach (var (old, @new) in EventTypes)
            {
                var (from, to) = toEnglish ? (old, @new) : (@new, old);
                migrationBuilder.Sql($"UPDATE \"Events\" SET \"Type\" = '{to}' WHERE \"Type\" = '{from}';");
            }

            // Going back, "Lodging"/"Transport"/"Other" are ambiguous per property only in name, not in
            // meaning, so each pair is reversed on its own property.
            foreach (var (property, old, @new) in PlanValues)
            {
                var (from, to) = toEnglish ? (old, @new) : (@new, old);
                migrationBuilder.Sql(
                    $"UPDATE \"VacationPlans\" SET \"Content\" = REPLACE(\"Content\", '\"{property}\":\"{from}\"', '\"{property}\":\"{to}\"') " +
                    $"WHERE \"Content\" LIKE '%\"{property}\":\"{from}\"%';");
            }
        }
    }
}
