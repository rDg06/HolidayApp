using HolidayApp.Client.Services;

namespace HolidayApp.Client.Tests;

/// <summary>API falsa com uma viagem a Roma (2–4 nov 2026), para os testes das páginas.</summary>
public sealed class FakeApi(Guid tripId) : ICalendarApi
{
    public Task<CalendarEventDto> GetEventAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(new CalendarEventDto(tripId, "Roma", Nov(2), Nov(4), EventType.Ferias, null));

    public Task<VacationPlanDto> GetPlanAsync(Guid eventId, CancellationToken ct = default) =>
        Task.FromResult(new VacationPlanDto
        {
            Destination = "Roma",
            Bookings =
            [
                new PlanBookingDto { Type = BookingType.Voo, Name = "Lisboa-Roma", Date = Nov(2), Reference = "TP832", Cost = 189.90m },
                new PlanBookingDto { Type = BookingType.Alojamento, Name = "Hotel Trevi", Date = Nov(3) },
            ],
            Tasks = [new PlanTaskDto { Text = "Malas", Done = true }, new PlanTaskDto { Text = "Bilhetes" }],
            Places =
            [
                new PlanPlaceDto { Name = "Hotel Roma", Kind = PlaceKind.Alojamento, Date = Nov(2), EndDate = Nov(3), Latitude = 41.9, Longitude = 12.49 },
                new PlanPlaceDto { Name = "Coliseu", Date = Nov(3), Latitude = 41.89, Longitude = 12.49 },
                new PlanPlaceDto { Name = "Metro Colosseo", Kind = PlaceKind.Metro, Latitude = 41.891, Longitude = 12.4923 },
            ],
        });

    public Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(int year, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<CalendarEventDto>> GetEventsAsync(int year, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<YearSummaryDto> GetSummaryAsync(int year, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CalendarEventDto> CreateEventAsync(SaveCalendarEventRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CalendarEventDto> UpdateEventAsync(Guid id, SaveCalendarEventRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteEventAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<VacationSummaryDto>> GetVacationsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<VacationSummaryDto>>(
        [
            // 2–6 nov 2026: 5 dias, 4 úteis (feriado ou ponte à parte); 3 locais a visitar, 1 visitado
            new VacationSummaryDto(tripId, "Roma", Nov(2), Nov(6), 4, "Roma", 2, 3, 1, 2, 1, 500m, 557.90m),
        ]);
    public Task<VacationPlanDto> SavePlanAsync(Guid eventId, VacationPlanDto plan, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<UserSettingsDto> GetSettingsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<UserSettingsDto> SaveSettingsAsync(UserSettingsDto settings, CancellationToken ct = default) => throw new NotSupportedException();

    private static DateOnly Nov(int day) => new(2026, 11, day);
}
