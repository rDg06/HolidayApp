using HolidayApp.Shared;
using HolidayApp.Shared.Validation;

namespace HolidayApp.Domain.Tests;

public class VacationPlanValidatorTests
{
    [Fact]
    public void Accepts_an_empty_plan()
    {
        Assert.Empty(VacationPlanValidator.Validate(new VacationPlanDto()));
    }

    [Theory]
    [InlineData("https://www.booking.com/booking/123", true)]
    [InlineData("http://example.com", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,<script>", false)]
    [InlineData("www.example.com", false)]
    public void Only_accepts_http_links(string url, bool valid)
    {
        Assert.Equal(valid, VacationPlanValidator.IsSafeUrl(url));
    }

    [Fact]
    public void Rejects_bookings_without_a_name_and_empty_tasks()
    {
        var plan = new VacationPlanDto
        {
            Bookings = [new PlanBookingDto { Name = " " }],
            Tasks = [new PlanTaskDto { Text = "" }],
        };

        var errors = VacationPlanValidator.Validate(plan);

        Assert.Contains(nameof(VacationPlanDto.Bookings), errors.Keys);
        Assert.Contains(nameof(VacationPlanDto.Tasks), errors.Keys);
    }

    [Fact]
    public void Rejects_negative_amounts_in_the_budget()
    {
        var plan = new VacationPlanDto { Expenses = [new PlanExpenseDto { Planned = -5 }] };

        Assert.Contains(nameof(VacationPlanDto.Expenses), VacationPlanValidator.Validate(plan).Keys);
    }

    [Fact]
    public void Rejects_a_negative_booking_cost()
    {
        var plan = new VacationPlanDto { Bookings = [new PlanBookingDto { Name = "Hotel", Cost = -1 }] };

        Assert.Contains(nameof(VacationPlanDto.Bookings), VacationPlanValidator.Validate(plan).Keys);
    }

    [Theory]
    [InlineData("2026-08-03", true)]
    [InlineData("2026-08-14", true)]
    [InlineData("2026-08-02", false)]
    [InlineData("2026-08-15", false)]
    public void Only_accepts_bookings_on_vacation_days(string date, bool valid)
    {
        var plan = new VacationPlanDto { Bookings = [new PlanBookingDto { Name = "Hotel", Date = DateOnly.Parse(date) }] };

        var errors = VacationPlanValidator.Validate(plan, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 14));

        Assert.Equal(valid, errors.Count == 0);
    }

    [Theory]
    [InlineData(41.8902, 12.4922, true)]
    [InlineData(91, 0, false)]
    [InlineData(0, -181, false)]
    [InlineData(double.NaN, 0, false)]
    public void Validates_place_coordinates(double lat, double lng, bool valid)
    {
        var plan = new VacationPlanDto { Places = [new PlanPlaceDto { Name = "Colosseum", Latitude = lat, Longitude = lng }] };

        Assert.Equal(valid, VacationPlanValidator.Validate(plan).Count == 0);
    }

    [Fact]
    public void Places_need_a_name_and_a_vacation_day()
    {
        var plan = new VacationPlanDto
        {
            Places =
            [
                new PlanPlaceDto { Name = " ", Latitude = 41.9, Longitude = 12.5 },
                new PlanPlaceDto { Name = "Vatican", Latitude = 41.9, Longitude = 12.45, Date = new DateOnly(2026, 9, 1) },
            ],
        };

        var errors = VacationPlanValidator.Validate(plan, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 14));

        Assert.Equal(2, errors[nameof(VacationPlanDto.Places)].Length);
    }

    [Fact]
    public void Accepts_bookings_without_a_day()
    {
        var plan = new VacationPlanDto { Bookings = [new PlanBookingDto { Name = "Insurance" }] };

        Assert.Empty(VacationPlanValidator.Validate(plan, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 14)));
    }

    [Theory]
    [InlineData(BookingType.Flight, ExpenseCategory.Transport)]
    [InlineData(BookingType.Lodging, ExpenseCategory.Lodging)]
    [InlineData(BookingType.Activity, ExpenseCategory.Activities)]
    [InlineData(BookingType.Other, ExpenseCategory.Other)]
    public void Booking_goes_into_the_right_budget_category(BookingType type, ExpenseCategory expected)
    {
        Assert.Equal(expected, PlanBookingDto.BudgetCategory(type));
    }

    [Fact]
    public void Lodging_has_no_number_and_the_other_places_follow_the_order()
    {
        PlanPlaceDto[] places =
        [
            new() { Name = "Colosseum" },
            new() { Name = "Hotel", IsLodging = true },
            new() { Name = "Pantheon" },

        ];

        Assert.Equal([1, null, 2], PlanPlaceDto.Numbers(places));
    }
}
