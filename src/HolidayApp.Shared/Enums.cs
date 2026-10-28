using System.Text.Json.Serialization;

namespace HolidayApp.Shared;

/// <summary>Tipo de marcação no calendário.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EventType>))]
public enum EventType
{
    /// <summary>Dias de férias (descontam ao saldo anual).</summary>
    Ferias = 0,

    /// <summary>Compromissos pessoais (não descontam férias).</summary>
    Pessoal = 1,

    /// <summary>Qualquer outra marcação.</summary>
    Outro = 2,
}

/// <summary>Natureza de um feriado.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HolidayKind>))]
public enum HolidayKind
{
    Nacional = 0,
    Facultativo = 1,
    Municipal = 2,
}

/// <summary>Tipo de reserva no planeamento de umas férias.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BookingType>))]
public enum BookingType
{
    Voo = 0,
    Alojamento = 1,
    Transporte = 2,
    Atividade = 3,
    Outro = 4,
}

/// <summary>Categoria de uma despesa no orçamento das férias.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExpenseCategory>))]
public enum ExpenseCategory
{
    Transporte = 0,
    Alojamento = 1,
    Alimentacao = 2,
    Atividades = 3,
    Compras = 4,
    Outro = 5,
}
