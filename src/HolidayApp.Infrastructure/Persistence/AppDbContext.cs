using HolidayApp.Domain.Abstractions;
using HolidayApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
    : DbContext(options)
{
    /// <summary>Usado pelos filtros globais: cada utilizador só vê os seus dados.</summary>
    public string CurrentUserId => currentUser.UserId;

    public DbSet<CalendarEvent> Events => Set<CalendarEvent>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<VacationPlan> VacationPlans => Set<VacationPlan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Isolamento por utilizador aplicado automaticamente a todas as consultas.
        modelBuilder.Entity<CalendarEvent>().HasQueryFilter(e => e.UserId == CurrentUserId);
        modelBuilder.Entity<UserSettings>().HasQueryFilter(s => s.UserId == CurrentUserId);
        modelBuilder.Entity<VacationPlan>().HasQueryFilter(p => p.UserId == CurrentUserId);
    }
}
