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
