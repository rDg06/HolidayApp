namespace HolidayApp.Shared.Validation;

/// <summary>Years supported by the app (calendar, holidays and entries).</summary>
public static class YearRange
{
    public const int Min = 1900;
    public const int Max = 2200;

    public static bool IsValid(int year) => year is >= Min and <= Max;

    public static string Message => $"The year must be between {Min} and {Max}.";
}

/// <summary>
/// Shared validation: the client uses it for instant feedback and the server
/// applies it again (never trust the client alone).
/// </summary>
public static class CalendarEventValidator
{
    public const int TitleMaxLength = 120;
    public const int NotesMaxLength = 1000;
    public const int MaxDurationDays = 366;

    public static Dictionary<string, string[]> Validate(SaveCalendarEventRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title))
            errors[nameof(request.Title)] = ["The title is required."];
        else if (request.Title.Trim().Length > TitleMaxLength)
            errors[nameof(request.Title)] = [$"The title cannot be longer than {TitleMaxLength} characters."];

        // Outside these years the day calculations (holidays, Easter, working days) stop making sense
        if (!YearRange.IsValid(request.StartDate.Year) || !YearRange.IsValid(request.EndDate.Year))
            errors[nameof(request.StartDate)] = [YearRange.Message];
        else if (request.EndDate < request.StartDate)
            errors[nameof(request.EndDate)] = ["The end date cannot be before the start date."];
        else if (request.EndDate.DayNumber - request.StartDate.DayNumber + 1 > MaxDurationDays)
            errors[nameof(request.EndDate)] = [$"An entry cannot be longer than {MaxDurationDays} days."];

        if (!Enum.IsDefined(request.Type))
            errors[nameof(request.Type)] = ["Invalid entry type."];

        if (request.Notes is { Length: > NotesMaxLength })
            errors[nameof(request.Notes)] = [$"The notes cannot be longer than {NotesMaxLength} characters."];

        return errors;
    }
}

public static class VacationPlanValidator
{
    public const int DestinationMaxLength = 120;
    public const int ShortTextMaxLength = 120;
    public const int ReferenceMaxLength = 60;
    public const int UrlMaxLength = 500;
    public const int NotesMaxLength = 500;
    public const int ActivitiesMaxLength = 2000;
    public const int TaskMaxLength = 200;
    public const int MaxBookings = 50;
    public const int MaxTasks = 200;
    public const int MaxExpenses = 200;
    public const int MaxPlaces = 200;
    public const decimal MaxAmount = 1_000_000m;

    /// <summary>Validates the plan and makes sure bookings fall within the vacation dates.</summary>
    public static Dictionary<string, string[]> Validate(VacationPlanDto plan, DateOnly tripStart, DateOnly tripEnd)
    {
        var errors = Validate(plan);
        if (errors.ContainsKey(MalformedKey))
            return errors;

        bool Outside(DateOnly? date) => date is { } d && (d < tripStart || d > tripEnd);
        static string Label(string? name) => string.IsNullOrWhiteSpace(name) ? "no name" : $"\"{name.Trim()}\"";

        var outsideBookings = plan.Bookings.Where(b => Outside(b.Date)).Select(b => Label(b.Name)).ToList();
        if (outsideBookings.Count > 0)
        {
            var message = $"Booking days must be between {Format(tripStart)} and {Format(tripEnd)} ({string.Join(", ", outsideBookings)}).";
            errors[nameof(plan.Bookings)] = [.. errors.GetValueOrDefault(nameof(plan.Bookings)) ?? [], message];
        }

        var outsidePlaces = plan.Places.Where(p => Outside(p.Date) || Outside(p.EndDate)).Select(p => Label(p.Name)).ToList();
        if (outsidePlaces.Count > 0)
        {
            var message = $"Place days must be between {Format(tripStart)} and {Format(tripEnd)} ({string.Join(", ", outsidePlaces)}).";
            errors[nameof(plan.Places)] = [.. errors.GetValueOrDefault(nameof(plan.Places)) ?? [], message];
        }

        var backwards = plan.Places
            .Where(p => p.Date is { } from && p.EndDate is { } to && to < from)
            .Select(p => Label(p.Name))
            .ToList();
        if (backwards.Count > 0)
        {
            var message = $"Check-out cannot be before check-in ({string.Join(", ", backwards)}).";
            errors[nameof(plan.Places)] = [.. errors.GetValueOrDefault(nameof(plan.Places)) ?? [], message];
        }

        return errors;
    }

    private static string Format(DateOnly date) => date.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Error key for malformed requests (missing lists or lists with empty items).</summary>
    public const string MalformedKey = "Plan";

    public static Dictionary<string, string[]> Validate(VacationPlanDto plan)
    {
        // API requests can carry null lists or null items: that is a request error, not a server error
        if (plan.Bookings is null || plan.Days is null || plan.Tasks is null || plan.Expenses is null || plan.Places is null
            || plan.Bookings.Contains(null!) || plan.Days.Contains(null!) || plan.Tasks.Contains(null!)
            || plan.Expenses.Contains(null!) || plan.Places.Contains(null!))
        {
            return new Dictionary<string, string[]> { [MalformedKey] = ["The plan sent has empty lists or items."] };
        }

        var errors = new Dictionary<string, List<string>>();

        void Add(string key, string message)
        {
            if (!errors.TryGetValue(key, out var list))
                errors[key] = list = [];
            if (!list.Contains(message))
                list.Add(message);
        }

        if (!Enum.IsDefined(plan.RouteMode))
            Add(nameof(plan.RouteMode), "Invalid route mode.");

        if (plan.Destination is { Length: > DestinationMaxLength })
            Add(nameof(plan.Destination), $"The destination cannot be longer than {DestinationMaxLength} characters.");

        if (plan.Bookings.Count > MaxBookings)
            Add(nameof(plan.Bookings), $"You cannot have more than {MaxBookings} bookings.");

        foreach (var booking in plan.Bookings)
        {
            if (!Enum.IsDefined(booking.Type))
                Add(nameof(plan.Bookings), "Invalid booking type.");
            if (string.IsNullOrWhiteSpace(booking.Name))
                Add(nameof(plan.Bookings), "Every booking needs a name.");
            else if (booking.Name.Trim().Length > ShortTextMaxLength)
                Add(nameof(plan.Bookings), $"The booking name cannot be longer than {ShortTextMaxLength} characters.");
            if (booking.Reference is { Length: > ReferenceMaxLength })
                Add(nameof(plan.Bookings), $"The booking reference cannot be longer than {ReferenceMaxLength} characters.");
            if (booking.Notes is { Length: > NotesMaxLength })
                Add(nameof(plan.Bookings), $"The booking notes cannot be longer than {NotesMaxLength} characters.");
            if (!string.IsNullOrWhiteSpace(booking.Url) && !IsSafeUrl(booking.Url))
                Add(nameof(plan.Bookings), "The link must start with http:// or https://.");
            if (booking.Cost is < 0 or > MaxAmount)
                Add(nameof(plan.Bookings), $"The booking cost must be between 0 and {MaxAmount:0} €.");
        }

        if (plan.Days.Count > CalendarEventValidator.MaxDurationDays)
            Add(nameof(plan.Days), "The plan has too many days.");

        foreach (var day in plan.Days)
        {
            if (day.Place is { Length: > ShortTextMaxLength })
                Add(nameof(plan.Days), $"Each day's place cannot be longer than {ShortTextMaxLength} characters.");
            if (day.Activities is { Length: > ActivitiesMaxLength })
                Add(nameof(plan.Days), $"Each day's activities cannot be longer than {ActivitiesMaxLength} characters.");
        }

        if (plan.Tasks.Count > MaxTasks)
            Add(nameof(plan.Tasks), $"You cannot have more than {MaxTasks} tasks.");

        foreach (var task in plan.Tasks)
        {
            if (string.IsNullOrWhiteSpace(task.Text))
                Add(nameof(plan.Tasks), "Tasks cannot be empty.");
            else if (task.Text.Trim().Length > TaskMaxLength)
                Add(nameof(plan.Tasks), $"Each task cannot be longer than {TaskMaxLength} characters.");
        }

        if (plan.Expenses.Count > MaxExpenses)
            Add(nameof(plan.Expenses), $"You cannot have more than {MaxExpenses} expenses.");

        foreach (var expense in plan.Expenses)
        {
            if (!Enum.IsDefined(expense.Category))
                Add(nameof(plan.Expenses), "Invalid expense category.");
            if (expense.Description is { Length: > ShortTextMaxLength })
                Add(nameof(plan.Expenses), $"The expense description cannot be longer than {ShortTextMaxLength} characters.");
            if (expense.Planned is < 0 or > MaxAmount || expense.Actual is < 0 or > MaxAmount)
                Add(nameof(plan.Expenses), $"Expense amounts must be between 0 and {MaxAmount:0} €.");
        }

        if (plan.Places.Count > MaxPlaces)
            Add(nameof(plan.Places), $"You cannot have more than {MaxPlaces} places.");

        foreach (var place in plan.Places)
        {
            if (!Enum.IsDefined(place.Kind))
                Add(nameof(plan.Places), "Invalid place type.");
            if (string.IsNullOrWhiteSpace(place.Name))
                Add(nameof(plan.Places), "Every place needs a name.");
            else if (place.Name.Trim().Length > ShortTextMaxLength)
                Add(nameof(plan.Places), $"The place name cannot be longer than {ShortTextMaxLength} characters.");
            if (place.Notes is { Length: > NotesMaxLength })
                Add(nameof(plan.Places), $"The place notes cannot be longer than {NotesMaxLength} characters.");
            if (!string.IsNullOrWhiteSpace(place.Url) && !IsSafeUrl(place.Url))
                Add(nameof(plan.Places), "The place link must start with http:// or https://.");
            if (!double.IsFinite(place.Latitude) || place.Latitude is < -90 or > 90
                || !double.IsFinite(place.Longitude) || place.Longitude is < -180 or > 180)
                Add(nameof(plan.Places), "Invalid place coordinates.");
        }

        return errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    /// <summary>Only accepts http(s) links, so "javascript:" and the like are not allowed.</summary>
    public static bool IsSafeUrl(string? url) =>
        url is { Length: > 0 and <= UrlMaxLength }
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public static class AccountValidator
{
    public const int EmailMaxLength = 254;
    public const int NameMaxLength = 60;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    /// <summary>Normalised email (no spaces, lower case): this is how it is stored and looked up.</summary>
    public static string NormaliseEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    /// <summary>Errors for a new account (messages ready to show).</summary>
    public static List<string> ValidateNew(string? name, string? email, string? password)
    {
        var errors = ValidateName(name);
        var cleanEmail = NormaliseEmail(email);

        // Simple check: something@something.something, no spaces (the email is only used to sign in)
        var at = cleanEmail.IndexOf('@');
        if (cleanEmail.Length > EmailMaxLength || at < 1 || cleanEmail.LastIndexOf('.') < at + 2
            || cleanEmail.EndsWith('.') || cleanEmail.Contains(' ') || cleanEmail.Count(c => c == '@') != 1)
            errors.Add("Enter a valid email.");

        errors.AddRange(ValidatePassword(password));
        return errors;
    }

    public static List<string> ValidateName(string? name)
    {
        var cleanName = (name ?? "").Trim();
        if (cleanName.Length == 0)
            return ["Enter your name."];
        return cleanName.Length > NameMaxLength ? [$"The name cannot be longer than {NameMaxLength} characters."] : [];
    }

    public static List<string> ValidatePassword(string? password)
    {
        if ((password ?? "").Length < PasswordMinLength)
            return [$"The password must have at least {PasswordMinLength} characters."];
        return password!.Length > PasswordMaxLength ? [$"The password cannot be longer than {PasswordMaxLength} characters."] : [];
    }
}

public static class VisitedPlaceValidator
{
    public const int NameMaxLength = 120;
    public const int CountryMaxLength = 100;
    public const int MinYear = 1900;

    /// <summary>Name, coordinates and year (from 1900 up to this year: it is where you have been).</summary>
    public static Dictionary<string, string[]> Validate(SaveVisitedPlaceRequest request, int currentYear)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors[nameof(request.Name)] = ["Choose the place."];
        else if (request.Name.Trim().Length > NameMaxLength)
            errors[nameof(request.Name)] = [$"The place name cannot be longer than {NameMaxLength} characters."];

        if (request.Country is { Length: > CountryMaxLength })
            errors[nameof(request.Country)] = [$"The country cannot be longer than {CountryMaxLength} characters."];

        if (!double.IsFinite(request.Latitude) || request.Latitude is < -90 or > 90
            || !double.IsFinite(request.Longitude) || request.Longitude is < -180 or > 180)
            errors["Coordinates"] = ["Invalid place coordinates."];

        if (request.Year < MinYear || request.Year > currentYear)
            errors[nameof(request.Year)] = [$"The year must be between {MinYear} and {currentYear}."];

        return errors;
    }
}

public static class SettingsValidator
{
    public const int MaxAnnualVacationDays = 60;
    public const int MunicipalNameMaxLength = 80;

    public static Dictionary<string, string[]> Validate(UserSettingsDto settings)
    {
        var errors = new Dictionary<string, string[]>();

        if (settings.AnnualVacationDays is < 0 or > MaxAnnualVacationDays)
            errors[nameof(settings.AnnualVacationDays)] = [$"Vacation days must be between 0 and {MaxAnnualVacationDays}."];

        var hasMonth = settings.MunicipalHolidayMonth is not null;
        var hasDay = settings.MunicipalHolidayDay is not null;

        if (hasMonth != hasDay)
        {
            errors[nameof(settings.MunicipalHolidayDay)] = ["Enter the month and day of the municipal holiday (or leave both empty)."];
        }
        else if (settings.MunicipalHolidayMonth is int month && settings.MunicipalHolidayDay is int day)
        {
            if (month is < 1 or > 12)
                errors[nameof(settings.MunicipalHolidayMonth)] = ["Invalid month."];
            else if (day < 1 || day > DateTime.DaysInMonth(2024, month)) // 2024 is a leap year: accepts 29/02
                errors[nameof(settings.MunicipalHolidayDay)] = ["Invalid day for the chosen month."];
        }

        if (settings.MunicipalHolidayName is { Length: > MunicipalNameMaxLength })
            errors[nameof(settings.MunicipalHolidayName)] = [$"The name cannot be longer than {MunicipalNameMaxLength} characters."];

        return errors;
    }
}
