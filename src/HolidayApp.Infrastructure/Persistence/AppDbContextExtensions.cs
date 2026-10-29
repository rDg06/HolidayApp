using HolidayApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Infrastructure.Persistence;

internal static class AppDbContextExtensions
{
    /// <summary>
    /// Devolve as definições guardadas ou valores por omissão (sem gravar,
    /// para evitar conflitos quando vários pedidos chegam em paralelo).
    /// </summary>
    public static async Task<UserSettings> GetSettingsOrDefaultAsync(this AppDbContext db, CancellationToken ct) =>
        await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(ct)
        ?? new UserSettings { UserId = db.CurrentUserId };

    /// <summary>
    /// Grava uma linha nova; se outro pedido a criou entretanto (ex.: duplo clique em
    /// "Guardar"), aplica as alterações sobre essa linha em vez de dar erro.
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
                throw; // não foi uma corrida: o erro é outro

            applyChanges(existing);
            await db.SaveChangesAsync(ct);
        }
    }
}
