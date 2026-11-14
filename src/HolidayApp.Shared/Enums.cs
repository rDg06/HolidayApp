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

/// <summary>Como se anda entre os locais de um dia (para o caminho e os tempos no mapa).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RouteMode>))]
public enum RouteMode
{
    APe = 0,
    Carro = 1,
    Bicicleta = 2,
}

/// <summary>Tipo de local no mapa das férias.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlaceKind>))]
public enum PlaceKind
{
    /// <summary>Local a visitar: tem número, dia e "visitado".</summary>
    Visita = 0,

    /// <summary>Onde se dorme: tem primeiro e último dia.</summary>
    Alojamento = 1,

    Aeroporto = 2,
    Metro = 3,
    Autocarro = 4,
    Comboio = 5,
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
