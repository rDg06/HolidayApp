using HolidayApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Infrastructure.Persistence;

internal static class AppDbContextExtensions
{
    /// <summary>
    /// Returns the saved settings or the defaults (without saving,
    /// to avoid conflicts when several requests arrive in parallel).
    /// </summary>
    public static async Task<UserSettings> GetSettingsOrDefaultAsync(this AppDbContext db, CancellationToken ct) =>
        await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(ct)
        ?? new UserSettings { UserId = db.CurrentUserId };

    /// <summary>
    /// Saves a new row; if another request created it in the meantime (e.g. a double click on
    /// "Save"), applies the changes to that row instead of failing.
    /// </summary>
    public static async Task InsertOrUpdateAsync<TEntity>(
        this AppDbContext db,
        TEntity entity,
        Func<CancellationToken, Task<TEntity?>> findExisting,
        Action<TEntity> applyChanges,
        CancellationToken ct)
        where TEntity : class
    {
        db.Add(entity);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(entity).State = EntityState.Detached;

            var existing = await findExisting(ct);
            if (existing is null)
                throw; // not a race: the error is something else

            applyChanges(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}
