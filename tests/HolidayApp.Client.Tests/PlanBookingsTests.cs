using HolidayApp.Client.Components;

namespace HolidayApp.Client.Tests;

public sealed class PlanBookingsTests : BunitContext
{
    [Fact]
    public void Por_no_mapa_so_aparece_em_alojamentos_que_ainda_nao_estao_no_mapa()
    {
        PlanBookingDto? clicked = null;
        var bookings = new List<PlanBookingDto>
        {
            new() { Type = BookingType.Voo, Name = "Lisboa-Roma" },
            new() { Type = BookingType.Alojamento, Name = "Hotel Roma", Date = new DateOnly(2026, 11, 2) },
            new() { Type = BookingType.Alojamento, Name = "Hotel Paris" },
        };
        var places = new List<PlanPlaceDto> { new() { Name = "hotel paris", IsLodging = true } };

        var cut = Render<PlanBookings>(p => p
            .Add(x => x.Bookings, bookings)
            .Add(x => x.Places, places)
            .Add(x => x.TripStart, new DateOnly(2026, 11, 2))
            .Add(x => x.TripEnd, new DateOnly(2026, 11, 6))
            .Add(x => x.OnPutOnMap, b => clicked = b));

        var button = Assert.Single(cut.FindAll(".put-on-map"));
        button.Click();

        Assert.Same(bookings[1], clicked);
    }
}
