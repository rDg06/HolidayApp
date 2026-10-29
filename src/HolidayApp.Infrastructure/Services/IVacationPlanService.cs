using HolidayApp.Shared;

namespace HolidayApp.Infrastructure.Services;

/// <summary>Planeamento de cada marcação de férias.</summary>
public interface IVacationPlanService
{
    /// <summary>Todas as marcações de férias, com um resumo do planeamento de cada uma.</summary>
    Task<IReadOnlyList<VacationSummaryDto>> GetVacationsAsync(CancellationToken ct = default);

    /// <summary>Devolve o planeamento (vazio se ainda não existir) ou null se a marcação não existir.</summary>
    Task<VacationPlanDto?> GetPlanAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Valida (incluindo os dias face às datas da marcação) e guarda o planeamento.</summary>
    Task<SavePlanResult> SavePlanAsync(Guid eventId, VacationPlanDto plan, CancellationToken ct = default);
}

/// <summary>Resultado de guardar um planeamento: não encontrado, inválido ou guardado.</summary>
public sealed record SavePlanResult(VacationPlanDto? Plan, IDictionary<string, string[]>? Errors)
{
    public static SavePlanResult NotFound { get; } = new(null, null);
    public static SavePlanResult Invalid(IDictionary<string, string[]> errors) => new(null, errors);
    public static SavePlanResult Saved(VacationPlanDto plan) => new(plan, null);
}
