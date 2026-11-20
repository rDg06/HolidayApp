using HolidayApp.Client.Components;

namespace HolidayApp.Client.Tests;

public sealed class PlanBookingsTests : BunitContext
{
    [Fact]
    public void Put_on_map_only_shows_on_lodgings_not_on_the_map_yet()

    {
        PlanBookingDto? clicked = null;
        var bookings = new List<PlanBookingDto>
        {
            new() { Type = BookingType.Flight, Name = "Lisbon-Rome" },
            new() { Type = BookingType.Lodging, Name = "Hotel Roma", Date = new DateOnly(2026, 11, 2) },
            new() { Type = BookingType.Lodging, Name = "Hotel Paris" },
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
