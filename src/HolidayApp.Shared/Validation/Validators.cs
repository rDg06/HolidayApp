namespace HolidayApp.Shared.Validation;

/// <summary>Anos suportados pela aplicação (calendário, feriados e marcações).</summary>
public static class YearRange
{
    public const int Min = 1900;
    public const int Max = 2200;

    public static bool IsValid(int year) => year is >= Min and <= Max;

    public static string Message => $"O ano tem de estar entre {Min} e {Max}.";
}

/// <summary>
/// Validação partilhada: o cliente usa-a para dar feedback imediato e o servidor
/// volta a aplicá-la (nunca confiar apenas no cliente).
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
            errors[nameof(request.Title)] = ["O título é obrigatório."];
        else if (request.Title.Trim().Length > TitleMaxLength)
            errors[nameof(request.Title)] = [$"O título não pode ter mais de {TitleMaxLength} caracteres."];

        // Fora destes anos os cálculos de dias (feriados, Páscoa, dias úteis) deixam de fazer sentido
        if (!YearRange.IsValid(request.StartDate.Year) || !YearRange.IsValid(request.EndDate.Year))
            errors[nameof(request.StartDate)] = [YearRange.Message];
        else if (request.EndDate < request.StartDate)
            errors[nameof(request.EndDate)] = ["A data de fim não pode ser anterior à data de início."];
        else if (request.EndDate.DayNumber - request.StartDate.DayNumber + 1 > MaxDurationDays)
            errors[nameof(request.EndDate)] = [$"Uma marcação não pode ter mais de {MaxDurationDays} dias."];

        if (!Enum.IsDefined(request.Type))
            errors[nameof(request.Type)] = ["Tipo de marcação inválido."];

        if (request.Notes is { Length: > NotesMaxLength })
            errors[nameof(request.Notes)] = [$"As notas não podem ter mais de {NotesMaxLength} caracteres."];

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

    /// <summary>Valida o planeamento e garante que as reservas ficam dentro das datas das férias.</summary>
    public static Dictionary<string, string[]> Validate(VacationPlanDto plan, DateOnly tripStart, DateOnly tripEnd)
    {
        var errors = Validate(plan);
        if (errors.ContainsKey(MalformedKey))
            return errors;

        bool Outside(DateOnly? date) => date is { } d && (d < tripStart || d > tripEnd);
        static string Label(string? name) => string.IsNullOrWhiteSpace(name) ? "sem nome" : $"\"{name.Trim()}\"";

        var outsideBookings = plan.Bookings.Where(b => Outside(b.Date)).Select(b => Label(b.Name)).ToList();
        if (outsideBookings.Count > 0)
        {
            var message = $"O dia das reservas tem de estar entre {Format(tripStart)} e {Format(tripEnd)} ({string.Join(", ", outsideBookings)}).";
            errors[nameof(plan.Bookings)] = [.. errors.GetValueOrDefault(nameof(plan.Bookings)) ?? [], message];
        }

        var outsidePlaces = plan.Places.Where(p => Outside(p.Date)).Select(p => Label(p.Name)).ToList();
        if (outsidePlaces.Count > 0)
        {
            var message = $"O dia dos locais tem de estar entre {Format(tripStart)} e {Format(tripEnd)} ({string.Join(", ", outsidePlaces)}).";
            errors[nameof(plan.Places)] = [.. errors.GetValueOrDefault(nameof(plan.Places)) ?? [], message];
        }

        return errors;
    }

    private static string Format(DateOnly date) => date.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Chave do erro para pedidos mal formados (listas em falta ou com elementos vazios).</summary>
    public const string MalformedKey = "Plan";

    public static Dictionary<string, string[]> Validate(VacationPlanDto plan)
    {
        // Pedidos à API podem trazer listas null ou elementos null: é um erro do pedido, não do servidor
        if (plan.Bookings is null || plan.Days is null || plan.Tasks is null || plan.Expenses is null || plan.Places is null
            || plan.Bookings.Contains(null!) || plan.Days.Contains(null!) || plan.Tasks.Contains(null!)
            || plan.Expenses.Contains(null!) || plan.Places.Contains(null!))
        {
            return new Dictionary<string, string[]> { [MalformedKey] = ["O planeamento enviado tem listas ou elementos vazios."] };
        }

        var errors = new Dictionary<string, List<string>>();

        void Add(string key, string message)
        {
            if (!errors.TryGetValue(key, out var list))
                errors[key] = list = [];
            if (!list.Contains(message))
                list.Add(message);
        }

        if (plan.Destination is { Length: > DestinationMaxLength })
            Add(nameof(plan.Destination), $"O destino não pode ter mais de {DestinationMaxLength} caracteres.");

        if (plan.Bookings.Count > MaxBookings)
            Add(nameof(plan.Bookings), $"Não podes ter mais de {MaxBookings} reservas.");

        foreach (var booking in plan.Bookings)
        {
            if (!Enum.IsDefined(booking.Type))
                Add(nameof(plan.Bookings), "Tipo de reserva inválido.");
            if (string.IsNullOrWhiteSpace(booking.Name))
                Add(nameof(plan.Bookings), "Cada reserva precisa de um nome.");
            else if (booking.Name.Trim().Length > ShortTextMaxLength)
                Add(nameof(plan.Bookings), $"O nome da reserva não pode ter mais de {ShortTextMaxLength} caracteres.");
            if (booking.Reference is { Length: > ReferenceMaxLength })
                Add(nameof(plan.Bookings), $"O código de reserva não pode ter mais de {ReferenceMaxLength} caracteres.");
            if (booking.Notes is { Length: > NotesMaxLength })
                Add(nameof(plan.Bookings), $"As notas da reserva não podem ter mais de {NotesMaxLength} caracteres.");
            if (!string.IsNullOrWhiteSpace(booking.Url) && !IsSafeUrl(booking.Url))
                Add(nameof(plan.Bookings), "A ligação tem de começar por http:// ou https://.");
            if (booking.Cost is < 0 or > MaxAmount)
                Add(nameof(plan.Bookings), $"O valor da reserva tem de estar entre 0 e {MaxAmount:0} €.");
        }

        if (plan.Days.Count > CalendarEventValidator.MaxDurationDays)
            Add(nameof(plan.Days), "O plano tem demasiados dias.");

        foreach (var day in plan.Days)
        {
            if (day.Place is { Length: > ShortTextMaxLength })
                Add(nameof(plan.Days), $"O local de cada dia não pode ter mais de {ShortTextMaxLength} caracteres.");
            if (day.Activities is { Length: > ActivitiesMaxLength })
                Add(nameof(plan.Days), $"As atividades de cada dia não podem ter mais de {ActivitiesMaxLength} caracteres.");
        }

        if (plan.Tasks.Count > MaxTasks)
            Add(nameof(plan.Tasks), $"Não podes ter mais de {MaxTasks} tarefas.");

        foreach (var task in plan.Tasks)
        {
            if (string.IsNullOrWhiteSpace(task.Text))
                Add(nameof(plan.Tasks), "As tarefas não podem estar vazias.");
            else if (task.Text.Trim().Length > TaskMaxLength)
                Add(nameof(plan.Tasks), $"Cada tarefa não pode ter mais de {TaskMaxLength} caracteres.");
        }

        if (plan.Expenses.Count > MaxExpenses)
            Add(nameof(plan.Expenses), $"Não podes ter mais de {MaxExpenses} despesas.");

        foreach (var expense in plan.Expenses)
        {
            if (!Enum.IsDefined(expense.Category))
                Add(nameof(plan.Expenses), "Categoria de despesa inválida.");
            if (expense.Description is { Length: > ShortTextMaxLength })
                Add(nameof(plan.Expenses), $"A descrição da despesa não pode ter mais de {ShortTextMaxLength} caracteres.");
            if (expense.Planned is < 0 or > MaxAmount || expense.Actual is < 0 or > MaxAmount)
                Add(nameof(plan.Expenses), $"Os valores das despesas têm de estar entre 0 e {MaxAmount:0} €.");
        }

        if (plan.Places.Count > MaxPlaces)
            Add(nameof(plan.Places), $"Não podes ter mais de {MaxPlaces} locais.");

        foreach (var place in plan.Places)
        {
            if (string.IsNullOrWhiteSpace(place.Name))
                Add(nameof(plan.Places), "Cada local precisa de um nome.");
            else if (place.Name.Trim().Length > ShortTextMaxLength)
                Add(nameof(plan.Places), $"O nome do local não pode ter mais de {ShortTextMaxLength} caracteres.");
            if (place.Notes is { Length: > NotesMaxLength })
                Add(nameof(plan.Places), $"As notas do local não podem ter mais de {NotesMaxLength} caracteres.");
            if (!double.IsFinite(place.Latitude) || place.Latitude is < -90 or > 90
                || !double.IsFinite(place.Longitude) || place.Longitude is < -180 or > 180)
                Add(nameof(plan.Places), "Coordenadas do local inválidas.");
        }

        return errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    /// <summary>Só aceita ligações http(s), para não permitir "javascript:" e afins.</summary>
    public static bool IsSafeUrl(string? url) =>
        url is { Length: > 0 and <= UrlMaxLength }
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public static class SettingsValidator
{
    public const int MaxAnnualVacationDays = 60;
    public const int MunicipalNameMaxLength = 80;

    public static Dictionary<string, string[]> Validate(UserSettingsDto settings)
    {
        var errors = new Dictionary<string, string[]>();

        if (settings.AnnualVacationDays is < 0 or > MaxAnnualVacationDays)
            errors[nameof(settings.AnnualVacationDays)] = [$"Os dias de férias têm de estar entre 0 e {MaxAnnualVacationDays}."];

        var hasMonth = settings.MunicipalHolidayMonth is not null;
        var hasDay = settings.MunicipalHolidayDay is not null;

        if (hasMonth != hasDay)
        {
            errors[nameof(settings.MunicipalHolidayDay)] = ["Indica o mês e o dia do feriado municipal (ou deixa ambos vazios)."];
        }
        else if (settings.MunicipalHolidayMonth is int month && settings.MunicipalHolidayDay is int day)
        {
            if (month is < 1 or > 12)
                errors[nameof(settings.MunicipalHolidayMonth)] = ["Mês inválido."];
            else if (day < 1 || day > DateTime.DaysInMonth(2024, month)) // 2024 é bissexto: aceita 29/02
                errors[nameof(settings.MunicipalHolidayDay)] = ["Dia inválido para o mês escolhido."];
        }

        if (settings.MunicipalHolidayName is { Length: > MunicipalNameMaxLength })
            errors[nameof(settings.MunicipalHolidayName)] = [$"O nome não pode ter mais de {MunicipalNameMaxLength} caracteres."];

        return errors;
    }
}
