using HolidayApp.Shared;

namespace HolidayApp.Infrastructure.Services;

/// <summary>Planeamento de cada marcação de férias.</summary>
public interface IVacationPlanService
{
    /// <summary>Todas as marcações de férias, com um resumo do planeamento de cada uma.</summary>
    Task<IReadOnlyList<VacationSummaryDto>> GetVacationsAsync(CancellationToken ct = default);

    /// <summary>Devolve o planeamento (vazio se ainda não existir) ou null se a marcação não existir.</summary>
    Task<VacationPlanDto?> GetPlanAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Guarda o planeamento. Devolve null se a marcação não existir.</summary>
    Task<VacationPlanDto?> SavePlanAsync(Guid eventId, VacationPlanDto plan, CancellationToken ct = default);
}
