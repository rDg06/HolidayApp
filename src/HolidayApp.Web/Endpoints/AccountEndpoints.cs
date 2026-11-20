using System.Security.Claims;
using HolidayApp.Infrastructure.Services;
using HolidayApp.Shared;
using HolidayApp.Web.Identity;
using Microsoft.AspNetCore.Authentication;

namespace HolidayApp.Web.Endpoints;

/// <summary>The signed-in account: who it is, download the data and delete the account (GDPR).</summary>
public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/me").WithTags("Account");

        // GET /api/me
        group.MapGet("/", async (ClaimsPrincipal user, IAccountService accounts, CancellationToken ct) =>
            AccountId(user) is { } id && await accounts.FindAsync(id, ct) is { } account
                ? Results.Ok(new AccountDto(account.Name, account.Email, account.Username))
                : Results.NotFound());

        // GET /api/me/export — all the account's data in a JSON file
        group.MapGet("/export", async (ClaimsPrincipal user, IAccountService accounts, CancellationToken ct) =>
        {
            if (AccountId(user) is not { } id || await accounts.ExportAsync(id, ct) is not { } data)
                return Results.NotFound();

            var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(data, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
            {
                WriteIndented = true,
                // A file for people to read: accents as they are (it never goes inside HTML)
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
            return Results.File(json, "application/json", $"holidayapp-data-{DateTime.UtcNow:yyyy-MM-dd}.json");
        });

        // PUT /api/me — changes the name (and refreshes the session, which keeps the name)
        group.MapPut("/", async (RenameAccountRequest request, HttpContext context, IAccountService accounts, CancellationToken ct) =>
        {
            if (AccountId(context.User) is not { } id)
                return Results.NotFound();

            var (account, errors) = await accounts.RenameAsync(id, request.Name, ct);
            if (account is null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Name"] = [.. errors] });

            await Accounts.SignInAsync(context, account);
            return Results.Ok(new AccountDto(account.Name, account.Email, account.Username));
        });

        // PUT /api/me/username — changes the user name (how other people add you to a trip)
        group.MapPut("/username", async (ChangeUsernameRequest request, ClaimsPrincipal user, IAccountService accounts, CancellationToken ct) =>
        {
            if (AccountId(user) is not { } id)
                return Results.NotFound();

            var (account, errors) = await accounts.ChangeUsernameAsync(id, request.Username, ct);
            return account is null
                ? Results.ValidationProblem(new Dictionary<string, string[]> { ["Username"] = [.. errors] })
                : Results.Ok(new AccountDto(account.Name, account.Email, account.Username));
        });

        // POST /api/me/password — changes the password (asks for the current one)
        group.MapPost("/password", async (ChangePasswordRequest request, ClaimsPrincipal user, IAccountService accounts, CancellationToken ct) =>
        {
            if (AccountId(user) is not { } id)
                return Results.NotFound();

            var errors = await accounts.ChangePasswordAsync(id, request.Current, request.Replacement, ct);
            return errors.Count == 0
                ? Results.NoContent()
                : Results.ValidationProblem(new Dictionary<string, string[]> { ["Password"] = [.. errors] });
        });

        // DELETE /api/me — deletes the account and all its data, and ends the session
        group.MapDelete("/", async (HttpContext context, IAccountService accounts, CancellationToken ct) =>
        {
            if (AccountId(context.User) is not { } id)
                return Results.NotFound();

            await accounts.DeleteAsync(id, ct);
            await context.SignOutAsync(Accounts.SchemeName);
            return Results.NoContent();
        });

        return api;
    }

    private static Guid? AccountId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(Accounts.AccountIdClaim), out var id) ? id : null;
}
