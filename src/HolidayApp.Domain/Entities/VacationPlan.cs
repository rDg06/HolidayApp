namespace HolidayApp.Domain.Entities;

/// <summary>
/// Planeamento de umas férias (um por marcação). O conteúdo — destino, reservas,
/// plano dia a dia, tarefas e orçamento — é guardado como um documento JSON,
/// por isso acrescentar campos ao planeamento não obriga a mudar tabelas.
/// </summary>
public sealed class VacationPlan
{
    /// <summary>Marcação a que este planeamento pertence (também é a chave).</summary>
    public Guid EventId { get; set; }

    public required string UserId { get; set; }

    /// <summary>JSON com o <c>VacationPlanDto</c>.</summary>
    public required string Content { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
