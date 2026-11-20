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

        // Typical queries: "this user's events in this date range".
        builder.HasIndex(e => new { e.UserId, e.StartDate, e.EndDate });

        // "Explore": public trips of everyone
        builder.HasIndex(e => e.IsPublic);
    }
}

internal sealed class VacationPlanConfiguration : IEntityTypeConfiguration<VacationPlan>
{
    public void Configure(EntityTypeBuilder<VacationPlan> builder)
    {
        builder.ToTable("VacationPlans");
        builder.HasKey(p => p.EventId);

        builder.Property(p => p.UserId).IsRequired().HasMaxLength(128);
        builder.Property(p => p.Content).IsRequired();

        // Deleting the entry also deletes the plan.
        builder.HasOne<CalendarEvent>()
            .WithOne()
            .HasForeignKey<VacationPlan>(p => p.EventId)
            .OnDelete(DeleteBehavior.Cascade);
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

internal sealed class VisitedPlaceConfiguration : IEntityTypeConfiguration<VisitedPlace>
{
    public void Configure(EntityTypeBuilder<VisitedPlace> builder)
    {
        builder.ToTable("VisitedPlaces");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.UserId).IsRequired().HasMaxLength(128);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(VisitedPlaceValidator.NameMaxLength);
        builder.Property(p => p.Country).HasMaxLength(VisitedPlaceValidator.CountryMaxLength);

        builder.HasIndex(p => new { p.UserId, p.Year });
    }
}

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(a => a.Id);
        builder.Ignore(a => a.UserId);

        builder.Property(a => a.Email).IsRequired().HasMaxLength(AccountValidator.EmailMaxLength);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(AccountValidator.NameMaxLength);
        builder.Property(a => a.PasswordHash).IsRequired().HasMaxLength(512);

        builder.Property(a => a.Username).HasMaxLength(AccountValidator.UsernameMaxLength);

        builder.HasIndex(a => a.Email).IsUnique();
        builder.HasIndex(a => a.Username).IsUnique();
    }
}

internal sealed class TripMemberConfiguration : IEntityTypeConfiguration<TripMember>
{
    public void Configure(EntityTypeBuilder<TripMember> builder)
    {
        builder.ToTable("TripMembers");
        builder.HasKey(m => new { m.EventId, m.UserId });

        builder.Property(m => m.UserId).IsRequired().HasMaxLength(128);

        // "Trips I was added to"
        builder.HasIndex(m => m.UserId);

        // Deleting the trip removes the people from it
        builder.HasOne<CalendarEvent>()
            .WithMany()
            .HasForeignKey(m => m.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
