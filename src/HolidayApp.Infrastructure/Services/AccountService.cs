using HolidayApp.Domain.Entities;
using HolidayApp.Infrastructure.Persistence;
using HolidayApp.Shared.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Infrastructure.Services;

/// <summary>Accounts with email and password (register, sign in, delete and export the data).</summary>
public interface IAccountService
{
    /// <summary>
    /// Creates the account. Without a user name, one is made from the email. Returns the errors
    /// (to show) if it cannot.
    /// </summary>
    Task<(UserAccount? Account, IReadOnlyList<string> Errors)> RegisterAsync(string? name, string? email, string? password, string? username = null, CancellationToken ct = default);

    /// <summary>Changes the user name. Returns the errors (to show) if it cannot.</summary>
    Task<(UserAccount? Account, IReadOnlyList<string> Errors)> ChangeUsernameAsync(Guid id, string? username, CancellationToken ct = default);

    /// <summary>The account, if the email (or user name) and password are right.</summary>
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
        string? name, string? email, string? password, string? username = null, CancellationToken ct = default)
    {
        var errors = AccountValidator.ValidateNew(name, email, password);
        var chosen = !string.IsNullOrWhiteSpace(username);
        if (chosen)
            errors.AddRange(AccountValidator.ValidateUsername(username));
        if (errors.Count > 0)
            return (null, errors);

        var cleanEmail = AccountValidator.NormaliseEmail(email);
        if (await db.Accounts.AnyAsync(a => a.Email == cleanEmail, ct))
            return (null, ["There is already an account with this email. Sign in with it."]);

        var cleanUsername = chosen
            ? AccountValidator.NormaliseUsername(username)
            : await FreeUsernameAsync(db, AccountValidator.UsernameFromEmail(cleanEmail), ct);
        if (chosen && await db.Accounts.AnyAsync(a => a.Username == cleanUsername, ct))
            return (null, [UsernameTaken]);

        var account = new UserAccount { Email = cleanEmail, Name = name!.Trim(), Username = cleanUsername, PasswordHash = "" };
        account.PasswordHash = Hasher.HashPassword(account, password!);

        var isFirst = !await db.Accounts.AnyAsync(ct);
        db.Accounts.Add(account);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two accounts with the same email or user name at the same time: the unique index stops the second
            return (null, ["There is already an account with this email or user name. Sign in, or choose another user name."]);
        }

        // The first account gets what already existed (data of the "local" user, from before accounts)
        if (isFirst)
            await ClaimLocalDataAsync(account.UserId, ct);

        return (account, []);
    }

    public async Task<UserAccount?> SignInAsync(string? email, string? password, CancellationToken ct = default)
    {
        var login = (email ?? "").Trim();
        UserAccount? account;
        if (login.Contains('@') && !login.StartsWith('@'))
        {
            var cleanEmail = AccountValidator.NormaliseEmail(login);
            account = await db.Accounts.FirstOrDefaultAsync(a => a.Email == cleanEmail, ct);
        }
        else
        {
            var cleanUsername = AccountValidator.NormaliseUsername(login);
            account = cleanUsername.Length == 0 ? null : await db.Accounts.FirstOrDefaultAsync(a => a.Username == cleanUsername, ct);
        }

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

    public async Task<(UserAccount? Account, IReadOnlyList<string> Errors)> ChangeUsernameAsync(Guid id, string? username, CancellationToken ct = default)
    {
        var errors = AccountValidator.ValidateUsername(username);
        if (errors.Count > 0)
            return (null, errors);

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null)
            return (null, ["The account no longer exists."]);

        var clean = AccountValidator.NormaliseUsername(username);
        if (clean == account.Username)
            return (account, []);
        if (await db.Accounts.AnyAsync(a => a.Username == clean, ct))
            return (null, [UsernameTaken]);

        account.Username = clean;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return (null, [UsernameTaken]);
        }

        return (account, []);
    }

    private const string UsernameTaken = "That user name is taken. Choose another one.";

    /// <summary>
    /// Gives a user name to the accounts that do not have one (created before user names), made
    /// from the email. Runs when the app starts.
    /// </summary>
    public static async Task GiveUsernamesAsync(AppDbContext db, CancellationToken ct = default)
    {
        var accounts = await db.Accounts.Where(a => a.Username == null).OrderBy(a => a.CreatedAtUtc).ToListAsync(ct);
        foreach (var account in accounts)
        {
            account.Username = await FreeUsernameAsync(db, AccountValidator.UsernameFromEmail(account.Email), ct);
            await db.SaveChangesAsync(ct); // one at a time: the next one sees this name as taken
        }
    }

    /// <summary>The name itself if it is free, otherwise with a number at the end ("ana2", "ana3"…).</summary>
    private static async Task<string> FreeUsernameAsync(AppDbContext db, string wanted, CancellationToken ct)
    {
        var taken = await db.Accounts.AsNoTracking()
            .Where(a => a.Username != null && a.Username.StartsWith(wanted))
            .Select(a => a.Username!)
            .ToListAsync(ct);
        if (!taken.Contains(wanted))
            return wanted;

        var n = 2;
        while (taken.Contains($"{wanted}{n}"))
            n++;
        return $"{wanted}{n}";
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

        // Leaves the trips of other people, and removes the people from my trips
        await db.TripMembers.Where(m => m.UserId == userId).ExecuteDeleteAsync(ct);
        var myEvents = db.Events.IgnoreQueryFilters().Where(e => e.UserId == userId).Select(e => e.Id);
        await db.TripMembers.Where(m => myEvents.Contains(m.EventId)).ExecuteDeleteAsync(ct);

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
            account = new { account.Name, account.Username, account.Email, account.CreatedAtUtc },
            settings = await db.UserSettings.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId, ct),
            events = events.Select(e => new
            {
                e.Id,
                e.Title,
                e.StartDate,
                e.EndDate,
                Type = e.Type.ToString(),
                e.Notes,
                e.IsPublic,
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
