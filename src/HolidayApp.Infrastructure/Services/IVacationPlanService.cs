using HolidayApp.Shared;

namespace HolidayApp.Infrastructure.Services;

/// <summary>Plan of each vacation entry.</summary>
public interface IVacationPlanService
{
    /// <summary>Every vacation entry, with a summary of each one's plan.</summary>
    Task<IReadOnlyList<VacationSummaryDto>> GetVacationsAsync(CancellationToken ct = default);

    /// <summary>Returns the plan (empty if there is none yet) or null if the entry does not exist.</summary>
    Task<VacationPlanDto?> GetPlanAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Validates (including the days against the entry dates) and saves the plan.</summary>
    Task<SavePlanResult> SavePlanAsync(Guid eventId, VacationPlanDto plan, CancellationToken ct = default);
}

/// <summary>Result of saving a plan: not found, invalid or saved.</summary>
public sealed record SavePlanResult(VacationPlanDto? Plan, IDictionary<string, string[]>? Errors, bool Conflict = false)
{
    public static SavePlanResult NotFound { get; } = new(null, null);

    /// <summary>Someone else saved this plan after it was opened.</summary>
    public static SavePlanResult Outdated { get; } = new(null, null, Conflict: true);
    public static SavePlanResult Invalid(IDictionary<string, string[]> errors) => new(null, errors);
    public static SavePlanResult Saved(VacationPlanDto plan) => new(plan, null);
}
