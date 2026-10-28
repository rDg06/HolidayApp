using System.Text.Json;
using HolidayApp.Domain.Abstractions;
using HolidayApp.Domain.Entities;
using HolidayApp.Infrastructure.Persistence;
using HolidayApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Infrastructure.Services;

internal sealed class VacationPlanService(AppDbContext db, ICurrentUser currentUser) : IVacationPlanService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<VacationPlanDto?> GetPlanAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null)
            return null;

        var entity = await db.VacationPlans.AsNoTracking().FirstOrDefaultAsync(p => p.EventId == eventId, ct);
        var plan = entity is null ? new VacationPlanDto() : Deserialize(entity.Content);

        // As datas da marcação podem ter mudado: os dias fora do intervalo deixam de aparecer.
        plan.Days = plan.Days.Where(d => d.Date >= ev.StartDate && d.Date <= ev.EndDate).ToList();
        return plan;
    }

    public async Task<VacationPlanDto?> SavePlanAsync(Guid eventId, VacationPlanDto plan, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (ev is null)
            return null;

        var clean = Normalise(plan, ev);
        var content = JsonSerializer.Serialize(clean, Json);

        var entity = await db.VacationPlans.FirstOrDefaultAsync(p => p.EventId == eventId, ct);
        if (entity is null)
        {
            db.VacationPlans.Add(new VacationPlan { EventId = eventId, UserId = currentUser.UserId, Content = content });
        }
        else
        {
            entity.Content = content;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return clean;
    }

    private static VacationPlanDto Deserialize(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<VacationPlanDto>(content, Json) ?? new VacationPlanDto();
        }
        catch (JsonException)
        {
            // Conteúdo estragado não deve impedir de abrir a página.
            return new VacationPlanDto();
        }
    }

    /// <summary>Limpa espaços, retira linhas vazias e dias fora das datas da marcação.</summary>
    private static VacationPlanDto Normalise(VacationPlanDto plan, CalendarEvent ev) => new()
    {
        Destination = Clean(plan.Destination),
        Bookings = plan.Bookings
            .Select(b => new PlanBookingDto
            {
                Type = b.Type,
                Name = b.Name.Trim(),
                Date = b.Date,
                Reference = Clean(b.Reference),
                Url = Clean(b.Url),
                Notes = Clean(b.Notes),
                Cost = b.Cost,
            })
            .ToList(),
        Days = plan.Days
            .Where(d => d.Date >= ev.StartDate && d.Date <= ev.EndDate)
            .Select(d => new PlanDayDto { Date = d.Date, Place = Clean(d.Place), Activities = Clean(d.Activities) })
            .Where(d => d.Place is not null || d.Activities is not null)
            .DistinctBy(d => d.Date)
            .OrderBy(d => d.Date)
            .ToList(),
        Tasks = plan.Tasks
            .Select(t => new PlanTaskDto { Text = t.Text.Trim(), Done = t.Done })
            .ToList(),
        Expenses = plan.Expenses
            .Select(e => new PlanExpenseDto
            {
                Category = e.Category,
                Description = Clean(e.Description),
                Planned = e.Planned,
                Actual = e.Actual,
            })
            .ToList(),
        Places = plan.Places
            .Select(p => new PlanPlaceDto
            {
                Name = p.Name.Trim(),
                // 6 casas decimais ≈ 10 cm: chega e evita guardar ruído
                Latitude = Math.Round(p.Latitude, 6),
                Longitude = Math.Round(p.Longitude, 6),
                Date = p.Date,
                Notes = Clean(p.Notes),
                Visited = p.Visited,
            })
            .ToList(),
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
