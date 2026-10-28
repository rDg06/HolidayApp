using HolidayApp.Shared;

namespace HolidayApp.Client.Formatting;

/// <summary>
/// Textos em português. Feito à mão para não depender dos dados de cultura
/// do navegador (o WebAssembly nem sempre carrega todas as culturas).
/// </summary>
public static class Pt
{
    public static readonly string[] Months =
    [
        "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
        "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro",
    ];

    public static readonly string[] MonthsShort =
        ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];

    // Indexado por DayOfWeek (domingo = 0)
    private static readonly string[] WeekdaysShort = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];

    public static string Short(DateOnly d) => $"{d.Day} {MonthsShort[d.Month - 1]}";

    public static string Long(DateOnly d) => $"{WeekdaysShort[(int)d.DayOfWeek]}, {d.Day} {MonthsShort[d.Month - 1]}";

    public static string Range(DateOnly start, DateOnly end)
    {
        if (start == end)
            return Long(start);

        if (start.Year == end.Year && start.Month == end.Month)
            return $"{start.Day}–{end.Day} {MonthsShort[end.Month - 1]}";

        return $"{Short(start)} – {Short(end)}";
    }

    public static string Days(int n) => n == 1 ? "1 dia" : $"{n} dias";

    public static string Workdays(int n) => n == 1 ? "1 dia útil" : $"{n} dias úteis";

    public static string Label(EventType type) => type switch
    {
        EventType.Ferias => "Férias",
        EventType.Pessoal => "Pessoal",
        _ => "Outro",
    };

    public static string Label(HolidayKind kind) => kind switch
    {
        HolidayKind.Nacional => "Feriado nacional",
        HolidayKind.Facultativo => "Tolerância",
        _ => "Feriado municipal",
    };

    public static string Label(BookingType type) => type switch
    {
        BookingType.Voo => "Voo",
        BookingType.Alojamento => "Alojamento",
        BookingType.Transporte => "Transporte",
        BookingType.Atividade => "Atividade",
        _ => "Outro",
    };

    public static string Label(ExpenseCategory category) => category switch
    {
        ExpenseCategory.Transporte => "Transporte",
        ExpenseCategory.Alojamento => "Alojamento",
        ExpenseCategory.Alimentacao => "Alimentação",
        ExpenseCategory.Atividades => "Atividades",
        ExpenseCategory.Compras => "Compras",
        _ => "Outro",
    };

    /// <summary>Valor em euros no formato português (1234.5 → "1 234,50 €").</summary>
    public static string Money(decimal value) =>
        value.ToString("#,0.00", System.Globalization.CultureInfo.InvariantCulture)
            .Replace(',', ' ')
            .Replace('.', ',') + " €";

    /// <summary>Sufixo CSS para cada tipo (ev-ferias, ev-pessoal, ev-outro).</summary>
    public static string Css(EventType type) => "ev-" + type.ToString().ToLowerInvariant();
}
