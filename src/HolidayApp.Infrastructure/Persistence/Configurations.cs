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

        // The feed: public trips of everyone, newest first
        builder.HasIndex(e => new { e.IsPublic, e.PublishedAtUtc });
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

internal sealed class TripLikeConfiguration : IEntityTypeConfiguration<TripLike>
{
    public void Configure(EntityTypeBuilder<TripLike> builder)
    {
        builder.ToTable("TripLikes");
        builder.HasKey(l => new { l.EventId, l.UserId });

        builder.Property(l => l.UserId).IsRequired().HasMaxLength(128);
        builder.HasIndex(l => l.UserId);

        // Deleting the trip removes its likes
        builder.HasOne<CalendarEvent>()
            .WithMany()
            .HasForeignKey(l => l.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TripCommentConfiguration : IEntityTypeConfiguration<TripComment>
{
    public void Configure(EntityTypeBuilder<TripComment> builder)
    {
        builder.ToTable("TripComments");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId).IsRequired().HasMaxLength(128);
        builder.Property(c => c.Text).IsRequired().HasMaxLength(TripCommentValidator.TextMaxLength);

        // A trip's comments in order, and "my comments" (export, deleting the account)
        builder.HasIndex(c => new { c.EventId, c.CreatedAtUtc });
        builder.HasIndex(c => c.UserId);

        // Deleting the trip removes its comments
        builder.HasOne<CalendarEvent>()
            .WithMany()
            .HasForeignKey(c => c.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
