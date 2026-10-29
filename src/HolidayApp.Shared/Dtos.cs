namespace HolidayApp.Shared;

/// <summary>Marcação devolvida pela API.</summary>
public sealed record CalendarEventDto(
    Guid Id,
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    EventType Type,
    string? Notes);

/// <summary>Pedido para criar ou atualizar uma marcação.</summary>
public sealed record SaveCalendarEventRequest(
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    EventType Type,
    string? Notes);

/// <summary>Feriado num determinado dia.</summary>
public sealed record HolidayDto(DateOnly Date, string Name, HolidayKind Kind);

/// <summary>Sugestão de "ponte": um dia de férias que, junto a um feriado, dá um fim de semana prolongado.</summary>
public sealed record BridgeSuggestionDto(
    DateOnly HolidayDate,
    string HolidayName,
    DateOnly SuggestedDayOff,
    int ConsecutiveDaysOff,
    bool AlreadyBooked);

/// <summary>Resumo anual do saldo de férias.</summary>
public sealed record YearSummaryDto(
    int Year,
    int AnnualVacationDays,
    int UsedVacationDays,
    int RemainingVacationDays,
    IReadOnlyList<BridgeSuggestionDto> Bridges);

/// <summary>
/// Planeamento de umas férias: destino, reservas, plano dia a dia, tarefas e orçamento.
/// Classes mutáveis para a página poder editar os campos diretamente.
/// </summary>
public sealed class VacationPlanDto
{
    public string? Destination { get; set; }
    public List<PlanBookingDto> Bookings { get; set; } = [];
    public List<PlanDayDto> Days { get; set; } = [];
    public List<PlanTaskDto> Tasks { get; set; } = [];
    public List<PlanExpenseDto> Expenses { get; set; } = [];
    public List<PlanPlaceDto> Places { get; set; } = [];
}

/// <summary>Local a visitar, marcado no mapa.</summary>
public sealed class PlanPlaceDto
{
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Dia em que se vai visitar (aparece no plano dia a dia).</summary>
    public DateOnly? Date { get; set; }

    public string? Notes { get; set; }
    public bool Visited { get; set; }
}

public sealed class PlanBookingDto
{
    public BookingType Type { get; set; } = BookingType.Alojamento;
    public string Name { get; set; } = "";
    public DateOnly? Date { get; set; }
    public string? Reference { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }

    /// <summary>Valor da reserva (€). Entra automaticamente no orçamento.</summary>
    public decimal? Cost { get; set; }

    /// <summary>Categoria do orçamento onde o valor desta reserva é contado.</summary>
    public static ExpenseCategory BudgetCategory(BookingType type) => type switch
    {
        BookingType.Voo or BookingType.Transporte => ExpenseCategory.Transporte,
        BookingType.Alojamento => ExpenseCategory.Alojamento,
        BookingType.Atividade => ExpenseCategory.Atividades,
        _ => ExpenseCategory.Outro,
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
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Outro;
    public string? Description { get; set; }
    public decimal? Planned { get; set; }
    public decimal? Actual { get; set; }
}

/// <summary>Resumo de umas férias para a página "Férias" (todas as férias, de todos os anos).</summary>
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
    decimal Spent);

/// <summary>Preferências do utilizador (editáveis na página de definições).</summary>
public sealed class UserSettingsDto
{
    public int AnnualVacationDays { get; set; } = 22;
    public bool IncludeCarnival { get; set; }
    public string? MunicipalHolidayName { get; set; }
    public int? MunicipalHolidayMonth { get; set; }
    public int? MunicipalHolidayDay { get; set; }
}
