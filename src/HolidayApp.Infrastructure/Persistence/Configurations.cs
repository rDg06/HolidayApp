using HolidayApp.Domain.Entities;
using HolidayApp.Shared.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HolidayApp.Infrastructure.Persistence;

internal sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("Events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId).IsRequired().HasMaxLength(128);
        builder.Property(e => e.Title).IsRequired().HasMaxLength(CalendarEventValidator.TitleMaxLength);
        builder.Property(e => e.Notes).HasMaxLength(CalendarEventValidator.NotesMaxLength);
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);

        // Consultas típicas: "eventos deste utilizador neste intervalo de datas".
        builder.HasIndex(e => new { e.UserId, e.StartDate, e.EndDate });
    }
}

internal sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("UserSettings");
        builder.HasKey(s => s.UserId);

        builder.Property(s => s.UserId).HasMaxLength(128);
        builder.Property(s => s.MunicipalHolidayName).HasMaxLength(SettingsValidator.MunicipalNameMaxLength);
    }
}
