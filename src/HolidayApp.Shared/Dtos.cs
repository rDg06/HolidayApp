namespace HolidayApp.Shared;

/// <summary>Calendar entry returned by the API.</summary>
public sealed record CalendarEventDto(
    Guid Id,
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    EventType Type,
    string? Notes);

/// <summary>Request to create or update a calendar entry.</summary>
public sealed record SaveCalendarEventRequest(
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    EventType Type,
    string? Notes);

/// <summary>Public holiday on a given day.</summary>
public sealed record HolidayDto(DateOnly Date, string Name, HolidayKind Kind);

/// <summary>"Bridge" suggestion: one vacation day that, next to a holiday, makes a long weekend.</summary>
public sealed record BridgeSuggestionDto(
    DateOnly HolidayDate,
    string HolidayName,
    DateOnly SuggestedDayOff,
    int ConsecutiveDaysOff,
    bool AlreadyBooked);

/// <summary>Yearly vacation allowance summary.</summary>
public sealed record YearSummaryDto(
    int Year,
    int AnnualVacationDays,
    int UsedVacationDays,
    int RemainingVacationDays,
    IReadOnlyList<BridgeSuggestionDto> Bridges);

/// <summary>
/// Plan for a vacation: destination, bookings, day-by-day plan, tasks and budget.
/// Mutable classes so the page can edit the fields directly.
/// </summary>
public sealed class VacationPlanDto
{
    public string? Destination { get; set; }
    public List<PlanBookingDto> Bookings { get; set; } = [];
    public List<PlanDayDto> Days { get; set; } = [];
    public List<PlanTaskDto> Tasks { get; set; } = [];
    public List<PlanExpenseDto> Expenses { get; set; } = [];
    public List<PlanPlaceDto> Places { get; set; } = [];

    /// <summary>How people get around between places (route and times on the map): walking by default.</summary>
    public RouteMode RouteMode { get; set; }

    /// <summary>
    /// Version of what is saved (changes on every save; 0 = no plan yet).
    /// When saving, if this is not the current version someone else saved in the meantime and
    /// the save is rejected instead of wiping their changes. Without a version (null) it always saves.
    /// </summary>
    public long? Version { get; set; }

    /// <summary>
    /// Spent per category: actual amount of the expenses + amount of the bookings (in the
    /// category of the booking type). In the fixed category order; only those with an amount.
    /// </summary>
    public List<CategoryAmountDto> SpentByCategory() =>
        Expenses.Select(e => (e.Category, Amount: e.Actual ?? 0))
            .Concat(Bookings.Select(b => (Category: PlanBookingDto.BudgetCategory(b.Type), Amount: b.Cost ?? 0)))
            .Where(x => x.Amount > 0)
            .GroupBy(x => x.Category)
            .Select(g => new CategoryAmountDto(g.Key, g.Sum(x => x.Amount)))
            .OrderBy(x => x.Category)
            .ToList();
}

/// <summary>Place to visit, pinned on the map.</summary>
public sealed class PlanPlaceDto
{
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>
    /// Day of the visit (shows in the day-by-day plan). For lodging it is the first day of
    /// the stay. A transport point without a day shows on every day.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>Lodging only: last day of the stay (inclusive). No value = a single day.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Opening hours, ticket price, tips…</summary>
    public string? Notes { get; set; }

    /// <summary>Link (official site, tickets, lodging booking…).</summary>
    public string? Url { get; set; }

    public bool Visited { get; set; }

    /// <summary>Visit, lodging or transport point (airport, metro, bus, train).</summary>
    public PlaceKind Kind { get; set; }

    /// <summary>
    /// Where we stay (hotel, house…): house icon, no number and no "visited".
    /// Shortcut for <see cref="Kind"/>; still in the JSON so older plans can be read.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsLodging
    {
        get => Kind == PlaceKind.Lodging;
        set
        {
            if (value)
                Kind = PlaceKind.Lodging;
            else if (Kind == PlaceKind.Lodging)
                Kind = PlaceKind.Visit;
        }
    }

    /// <summary>Place to visit (with a number and "visited").</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsVisit => Kind == PlaceKind.Visit;

    /// <summary>Airport or station (metro, bus, train).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsTransport => Kind is PlaceKind.Airport or PlaceKind.Metro or PlaceKind.Bus or PlaceKind.Train;

    /// <summary>
    /// Whether this is the lodging on that day. On the day you change lodging (e.g. Rome 1–3,
    /// Paris 3–6) both count.
    /// </summary>
    public bool StaysOn(DateOnly day) =>
        IsLodging && Date is { } from && from <= day && day <= (EndDate ?? from);

    /// <summary>
    /// Number of each place, the same in the list, on the map and in the day-by-day plan. Only
    /// places to visit are numbered (1, 2, 3…); lodging and transport get <c>null</c>.
    /// </summary>
    public static int?[] Numbers(IReadOnlyList<PlanPlaceDto> places)
    {
        var numbers = new int?[places.Count];
        var next = 1;
        for (var i = 0; i < places.Count; i++)
            numbers[i] = places[i].IsVisit ? next++ : null;
        return numbers;
    }
}

public sealed class PlanBookingDto
{
    public BookingType Type { get; set; } = BookingType.Lodging;
    public string Name { get; set; } = "";
    public DateOnly? Date { get; set; }
    public string? Reference { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }

    /// <summary>Booking cost (€). Counted in the budget automatically.</summary>
    public decimal? Cost { get; set; }

    /// <summary>Budget category where this booking's cost is counted.</summary>
    public static ExpenseCategory BudgetCategory(BookingType type) => type switch
    {
        BookingType.Flight or BookingType.Transport => ExpenseCategory.Transport,
        BookingType.Lodging => ExpenseCategory.Lodging,
        BookingType.Activity => ExpenseCategory.Activities,
        _ => ExpenseCategory.Other,
    };
}

public sealed class PlanDayDto
{
    public DateOnly Date { get; set; }
    public string? Place { get; set; }
    public string? Activities { get; set; }
}

public sealed class PlanTaskDto
{
    public string Text { get; set; } = "";
    public bool Done { get; set; }
}

public sealed class PlanExpenseDto
{
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;
    public string? Description { get; set; }
    public decimal? Planned { get; set; }
    public decimal? Actual { get; set; }
}

/// <summary>Summary of a vacation for the "Vacations" page (every vacation, from every year).</summary>
public sealed record VacationSummaryDto(
    Guid Id,
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    int Workdays,
    string? Destination,
    int Bookings,
    int Places,
    int PlacesVisited,
    int Tasks,
    int TasksDone,
    decimal Planned,
    decimal Spent,
    IReadOnlyList<MapPointDto>? Points = null,
    TripRole Role = TripRole.Owner,
    PersonDto? Owner = null,
    int People = 0,
    bool IsPublic = false)
{
    /// <summary>Someone else's trip that I was added to.</summary>
    public bool IsShared => Role is TripRole.Editor or TripRole.Viewer;

    /// <summary>Can change the plan (owner or added with "can edit").</summary>
    public bool CanEdit => Role is TripRole.Owner or TripRole.Editor;
}

/// <summary>The signed-in account.</summary>
public sealed record AccountDto(string Name, string Email, string? Username = null);

/// <summary>Change the user name (how other people find you to add you to a trip).</summary>
public sealed record ChangeUsernameRequest(string Username);

/// <summary>Another person, as others see them: user name and display name (never the email).</summary>
public sealed record PersonDto(string Username, string Name);

/// <summary>A person added to a trip.</summary>
public sealed record TripMemberDto(string Username, string Name, bool CanEdit);

/// <summary>
/// Who sees a trip: public or private, the owner and the people added to it, and what I can do.
/// People who only see it because it is public do not get the list of people.
/// </summary>
public sealed record TripSharingDto(
    TripRole MyRole,
    bool IsPublic,
    PersonDto Owner,
    IReadOnlyList<TripMemberDto> Members)
{
    public bool IsOwner => MyRole == TripRole.Owner;
    public bool CanEdit => MyRole is TripRole.Owner or TripRole.Editor;
}

/// <summary>Make a trip public or private.</summary>
public sealed record SetTripVisibilityRequest(bool IsPublic);

/// <summary>Add someone to a trip, by email or user name.</summary>
public sealed record AddTripMemberRequest(string Person, bool CanEdit);

/// <summary>Change what someone added to a trip can do.</summary>
public sealed record SetTripMemberRequest(bool CanEdit);

/// <summary>A public trip on the "Explore" page.</summary>
public sealed record PublicTripDto(
    Guid Id,
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Destination,
    int Places,
    PersonDto Owner,
    bool IsMine);

/// <summary>Change the display name.</summary>
public sealed record RenameAccountRequest(string Name);

/// <summary>Change the password (asks for the current one).</summary>
public sealed record ChangePasswordRequest(string Current, string Replacement);

/// <summary>Place you have been to, added by hand on the "Where I've been" map.</summary>
public sealed record VisitedPlaceDto(Guid Id, string Name, double Latitude, double Longitude, string? Country, int Year);

/// <summary>Request to add a place you have been to.</summary>
public sealed record SaveVisitedPlaceRequest(string Name, double Latitude, double Longitude, string? Country, int Year);

/// <summary>
/// Where a trip sits on the "Where I've been" map: one point per lodging (each city where
/// you slept) or, without lodging, the centre of the pinned places.
/// </summary>
public sealed record MapPointDto(string Name, double Latitude, double Longitude);

/// <summary>Amount spent in a budget category (includes booking costs).</summary>
public sealed record CategoryAmountDto(ExpenseCategory Category, decimal Amount);

/// <summary>User preferences (editable on the settings page).</summary>
public sealed class UserSettingsDto
{
    public int AnnualVacationDays { get; set; } = 22;
    public bool IncludeCarnival { get; set; }
    public string? MunicipalHolidayName { get; set; }
    public int? MunicipalHolidayMonth { get; set; }
    public int? MunicipalHolidayDay { get; set; }
}
