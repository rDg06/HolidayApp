using HolidayApp.Domain.Abstractions;
using HolidayApp.Domain.Entities;
using HolidayApp.Domain.Holidays;
using HolidayApp.Domain.Planning;
using HolidayApp.Infrastructure.Persistence;
using HolidayApp.Shared;
using HolidayApp.Shared.Calendar;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Infrastructure.Services;

internal sealed class CalendarService(
    AppDbContext db,
    ICurrentUser currentUser,
    IHolidayProvider holidayProvider) : ICalendarService
{
    // ---------- Marcações ----------

    public async Task<IReadOnlyList<CalendarEventDto>> GetEventsAsync(int year, CancellationToken ct = default)
    {
        var events = await EventsOverlappingYear(year)
            .OrderBy(e => e.StartDate)
            .ToListAsync(ct);

        return events.Select(e => e.ToDto()).ToList();
    }

    public async Task<CalendarEventDto?> GetEventAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        return entity?.ToDto();
    }

    public async Task<CalendarEventDto> CreateEventAsync(SaveCalendarEventRequest request, CancellationToken ct = default)
    {
        var entity = new CalendarEvent
        {
            UserId = currentUser.UserId,
            Title = request.Title.Trim(),
        };
        Apply(entity, request);

        db.Events.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<CalendarEventDto?> UpdateEventAsync(Guid id, SaveCalendarEventRequest request, CancellationToken ct = default)
    {
        var entity = await db.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null)
            return null;

        Apply(entity, request);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return entity.ToDto();
    }

    public async Task<bool> DeleteEventAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null)
            return false;

        db.Events.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ---------- Feriados e resumo ----------

    public async Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(int year, CancellationToken ct = default)
    {
        var settings = await db.GetSettingsOrDefaultAsync(ct);
        return holidayProvider
            .GetHolidays(year, settings.ToHolidayOptions())
            .Select(h => new HolidayDto(h.Date, h.Name, h.Kind))
            .ToList();
    }

    public async Task<YearSummaryDto> GetSummaryAsync(int year, CancellationToken ct = default)
    {
        var settings = await db.GetSettingsOrDefaultAsync(ct);
        var holidays = holidayProvider.GetHolidays(year, settings.ToHolidayOptions());
        var holidayDates = holidays.Select(h => h.Date).ToHashSet();

        var vacations = await EventsOverlappingYear(year)
            .Where(e => e.Type == EventType.Ferias)
            .ToListAsync(ct);

        // Conjunto de dias úteis de férias (marcações sobrepostas não contam a dobrar).
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);
        var vacationDays = new HashSet<DateOnly>();

        foreach (var vacation in vacations)
        {
            var from = vacation.StartDate < yearStart ? yearStart : vacation.StartDate;
            var to = vacation.EndDate > yearEnd ? yearEnd : vacation.EndDate;

            for (var day = from; day <= to; day = day.AddDays(1))
            {
                if (WorkdayCalculator.IsWorkday(day, holidayDates))
                    vacationDays.Add(day);
            }
        }

        var bridges = BridgeSuggester.Suggest(holidays)
            .Select(b => new BridgeSuggestionDto(
                b.Holiday.Date,
                b.Holiday.Name,
                b.DayOff,
                b.ConsecutiveDaysOff,
                vacationDays.Contains(b.DayOff)))
            .ToList();

        var used = vacationDays.Count;
        return new YearSummaryDto(year, settings.AnnualVacationDays, used, settings.AnnualVacationDays - used, bridges);
    }

    // ---------- Definições ----------

    public async Task<UserSettingsDto> GetSettingsAsync(CancellationToken ct = default) =>
        (await db.GetSettingsOrDefaultAsync(ct)).ToDto();

    public async Task<UserSettingsDto> UpdateSettingsAsync(UserSettingsDto dto, CancellationToken ct = default)
    {
        void Apply(UserSettings settings)
        {
            settings.AnnualVacationDays = dto.AnnualVacationDays;
            settings.IncludeCarnival = dto.IncludeCarnival;
            settings.MunicipalHolidayName = string.IsNullOrWhiteSpace(dto.MunicipalHolidayName) ? null : dto.MunicipalHolidayName.Trim();
            settings.MunicipalHolidayMonth = dto.MunicipalHolidayMonth;
            settings.MunicipalHolidayDay = dto.MunicipalHolidayDay;
        }

        var existing = await db.UserSettings.FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            Apply(existing);
            await db.SaveChangesAsync(ct);
            return existing.ToDto();
        }

        var created = new UserSettings { UserId = currentUser.UserId };
        Apply(created);
        await db.InsertOrUpdateAsync(created, token => db.UserSettings.FirstOrDefaultAsync(token), Apply, ct);
        return (await db.GetSettingsOrDefaultAsync(ct)).ToDto();
    }

    // ---------- Auxiliares ----------

    private IQueryable<CalendarEvent> EventsOverlappingYear(int year)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);
        return db.Events.Where(e => e.StartDate <= yearEnd && e.EndDate >= yearStart);
    }

    private static void Apply(CalendarEvent entity, SaveCalendarEventRequest request)
    {
        entity.Title = request.Title.Trim();
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.Type = request.Type;
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    }
}

internal static class MappingExtensions
{
    public static CalendarEventDto ToDto(this CalendarEvent e) =>
        new(e.Id, e.Title, e.StartDate, e.EndDate, e.Type, e.Notes);

    public static UserSettingsDto ToDto(this UserSettings s) => new()
    {
        AnnualVacationDays = s.AnnualVacationDays,
        IncludeCarnival = s.IncludeCarnival,
        MunicipalHolidayName = s.MunicipalHolidayName,
        MunicipalHolidayMonth = s.MunicipalHolidayMonth,
        MunicipalHolidayDay = s.MunicipalHolidayDay,
    };
}
