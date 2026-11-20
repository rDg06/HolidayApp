using HolidayApp.Domain.Abstractions;
using HolidayApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
    : DbContext(options)
{
    /// <summary>Used by the global filters: each user only sees their own data.</summary>
    public string CurrentUserId => currentUser.UserId;

    public DbSet<CalendarEvent> Events => Set<CalendarEvent>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<VacationPlan> VacationPlans => Set<VacationPlan>();
    public DbSet<VisitedPlace> VisitedPlaces => Set<VisitedPlace>();

    /// <summary>Accounts (no per-user filter: sign-in needs to look up any email).</summary>
    public DbSet<UserAccount> Accounts => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Per-user isolation applied automatically to every query.
        modelBuilder.Entity<CalendarEvent>().HasQueryFilter(e => e.UserId == CurrentUserId);
        modelBuilder.Entity<UserSettings>().HasQueryFilter(s => s.UserId == CurrentUserId);
        modelBuilder.Entity<VacationPlan>().HasQueryFilter(p => p.UserId == CurrentUserId);
        modelBuilder.Entity<VisitedPlace>().HasQueryFilter(p => p.UserId == CurrentUserId);
    }
}
