namespace HolidayApp.Shared.Validation;

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

        if (request.EndDate < request.StartDate)
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
