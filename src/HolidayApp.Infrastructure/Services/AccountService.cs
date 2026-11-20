using HolidayApp.Domain.Entities;
using HolidayApp.Infrastructure.Persistence;
using HolidayApp.Shared.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Infrastructure.Services;

/// <summary>Accounts with email and password (register, sign in, delete and export the data).</summary>
public interface IAccountService
{
    /// <summary>Creates the account. Returns the errors (to show) if it cannot.</summary>
    Task<(UserAccount? Account, IReadOnlyList<string> Errors)> RegisterAsync(string? name, string? email, string? password, CancellationToken ct = default);

    /// <summary>The account, if the email and password are right.</summary>
    Task<UserAccount?> SignInAsync(string? email, string? password, CancellationToken ct = default);

    Task<UserAccount?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Changes the display name. Returns the errors (to show) if it cannot.</summary>
    Task<(UserAccount? Account, IReadOnlyList<string> Errors)> RenameAsync(Guid id, string? name, CancellationToken ct = default);

    /// <summary>Changes the password (asks for the current one). Returns the errors, or none if it changed.</summary>
    Task<IReadOnlyList<string>> ChangePasswordAsync(Guid id, string? current, string? replacement, CancellationToken ct = default);

    /// <summary>Deletes the account and all its data (entries, plans, settings, places).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>All the account's data (GDPR: "download my data").</summary>
    Task<object?> ExportAsync(Guid id, CancellationToken ct = default);
}

public sealed class AccountService(AppDbContext db) : IAccountService
{
    /// <summary>Owner of the data before there were accounts (see HttpCurrentUser).</summary>
    private const string LocalUserId = "local";

    private static readonly PasswordHasher<UserAccount> Hasher = new();

    /// <summary>Used when the email does not exist, so the answer takes as long (does not reveal which emails exist).</summary>
    private static readonly string DummyHash = Hasher.HashPassword(null!, "a-password-nobody-uses");

    public async Task<(UserAccount? Account, IReadOnlyList<string> Errors)> RegisterAsync(
        string? name, string? email, string? password, CancellationToken ct = default)
    {
        var errors = AccountValidator.ValidateNew(name, email, password);
        if (errors.Count > 0)
            return (null, errors);

        var cleanEmail = AccountValidator.NormaliseEmail(email);
        if (await db.Accounts.AnyAsync(a => a.Email == cleanEmail, ct))
            return (null, ["There is already an account with this email. Sign in with it."]);

        var account = new UserAccount { Email = cleanEmail, Name = name!.Trim(), PasswordHash = "" };
        account.PasswordHash = Hasher.HashPassword(account, password!);

        var isFirst = !await db.Accounts.AnyAsync(ct);
        db.Accounts.Add(account);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two accounts with the same email at the same time: the unique index stops the second
            return (null, ["There is already an account with this email. Sign in with it."]);
        }

        // The first account gets what already existed (data of the "local" user, from before accounts)
        if (isFirst)
            await ClaimLocalDataAsync(account.UserId, ct);

        return (account, []);
    }

    public async Task<UserAccount?> SignInAsync(string? email, string? password, CancellationToken ct = default)
    {
        var cleanEmail = AccountValidator.NormaliseEmail(email);
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Email == cleanEmail, ct);
        if (account is null)
        {
            Hasher.VerifyHashedPassword(null!, DummyHash, password ?? "");
            return null;
        }

        var result = Hasher.VerifyHashedPassword(account, account.PasswordHash, password ?? "");
        if (result == PasswordVerificationResult.Failed)
            return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.PasswordHash = Hasher.HashPassword(account, password!);
            await db.SaveChangesAsync(ct);
        }

        return account;
    }

    public Task<UserAccount?> FindAsync(Guid id, CancellationToken ct = default) =>
        db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<(UserAccount? Account, IReadOnlyList<string> Errors)> RenameAsync(Guid id, string? name, CancellationToken ct = default)
    {
        var errors = AccountValidator.ValidateName(name);
        if (errors.Count > 0)
            return (null, errors);

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null)
            return (null, ["The account no longer exists."]);

        account.Name = name!.Trim();
        await db.SaveChangesAsync(ct);
        return (account, []);
    }

    public async Task<IReadOnlyList<string>> ChangePasswordAsync(Guid id, string? current, string? replacement, CancellationToken ct = default)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null)
            return ["The account no longer exists."];

        if (Hasher.VerifyHashedPassword(account, account.PasswordHash, current ?? "") == PasswordVerificationResult.Failed)
            return ["The current password is wrong."];

        var errors = AccountValidator.ValidatePassword(replacement);
        if (errors.Count > 0)
            return errors;

        account.PasswordHash = Hasher.HashPassword(account, replacement!);
        await db.SaveChangesAsync(ct);
        return [];
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var userId = id.ToString();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await db.VacationPlans.IgnoreQueryFilters().Where(p => p.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Events.IgnoreQueryFilters().Where(e => e.UserId == userId).ExecuteDeleteAsync(ct);
        await db.UserSettings.IgnoreQueryFilters().Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);
        await db.VisitedPlaces.IgnoreQueryFilters().Where(p => p.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Accounts.Where(a => a.Id == id).ExecuteDeleteAsync(ct);

        await transaction.CommitAsync(ct);
    }

    public async Task<object?> ExportAsync(Guid id, CancellationToken ct = default)
    {
        var account = await FindAsync(id, ct);
        if (account is null)
            return null;

        var userId = account.UserId;
        var events = await db.Events.IgnoreQueryFilters().AsNoTracking().Where(e => e.UserId == userId)
            .OrderBy(e => e.StartDate).ToListAsync(ct);
        var plans = await db.VacationPlans.IgnoreQueryFilters().AsNoTracking().Where(p => p.UserId == userId)
            .ToDictionaryAsync(p => p.EventId, p => p.Content, ct);

        return new
        {
            exportedAtUtc = DateTime.UtcNow,
            account = new { account.Name, account.Email, account.CreatedAtUtc },
            settings = await db.UserSettings.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct),
            events = events.Select(e => new
            {
                e.Id,
                e.Title,
                e.StartDate,
                e.EndDate,
                Type = e.Type.ToString(),
                e.Notes,
                // The plan goes as it is stored (JSON)
                Plan = plans.TryGetValue(e.Id, out var content) ? System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content) : (System.Text.Json.JsonElement?)null,
            }),
            visitedPlaces = await db.VisitedPlaces.IgnoreQueryFilters().AsNoTracking().Where(p => p.UserId == userId)
                .OrderByDescending(p => p.Year)
                .Select(p => new { p.Name, p.Country, p.Year, p.Latitude, p.Longitude })
                .ToListAsync(ct),
        };
    }

    /// <summary>Moves to the account everything that belonged to the "local" user (before accounts).</summary>
    private async Task ClaimLocalDataAsync(string userId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Events\" SET \"UserId\" = {userId} WHERE \"UserId\" = {LocalUserId}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"VacationPlans\" SET \"UserId\" = {userId} WHERE \"UserId\" = {LocalUserId}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"UserSettings\" SET \"UserId\" = {userId} WHERE \"UserId\" = {LocalUserId}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"VisitedPlaces\" SET \"UserId\" = {userId} WHERE \"UserId\" = {LocalUserId}", ct);
    }
}
