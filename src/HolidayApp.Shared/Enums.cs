using System.Text.Json.Serialization;

namespace HolidayApp.Shared;

/// <summary>Kind of calendar entry.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EventType>))]
public enum EventType
{
    /// <summary>Vacation days (count against the yearly allowance).</summary>
    Vacation = 0,

    /// <summary>Personal appointments (do not use vacation days).</summary>
    Personal = 1,

    /// <summary>Anything else.</summary>
    Other = 2,
}

/// <summary>Kind of public holiday.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HolidayKind>))]
public enum HolidayKind
{
    National = 0,
    Optional = 1,
    Municipal = 2,
}

/// <summary>Kind of booking in a vacation plan.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BookingType>))]
public enum BookingType
{
    Flight = 0,
    Lodging = 1,
    Transport = 2,
    Activity = 3,
    Other = 4,
}

/// <summary>How people get around between the places of a day (for the route and times on the map).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RouteMode>))]
public enum RouteMode
{
    Walk = 0,
    Car = 1,
    Bike = 2,
}

/// <summary>Kind of place on the vacation map.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlaceKind>))]
public enum PlaceKind
{
    /// <summary>A place to visit: has a number, a day and "visited".</summary>
    Visit = 0,

    /// <summary>Where you sleep: has a first and a last day.</summary>
    Lodging = 1,

    Airport = 2,
    Metro = 3,
    Bus = 4,
    Train = 5,
}

/// <summary>Category of an expense in the vacation budget.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExpenseCategory>))]
public enum ExpenseCategory
{
    Transport = 0,
    Lodging = 1,
    Food = 2,
    Activities = 3,
    Shopping = 4,
    Other = 5,
}
