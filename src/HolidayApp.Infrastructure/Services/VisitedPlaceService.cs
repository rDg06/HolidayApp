using HolidayApp.Domain.Abstractions;
using HolidayApp.Domain.Entities;
using HolidayApp.Infrastructure.Persistence;
using HolidayApp.Shared;
using Microsoft.EntityFrameworkCore;

namespace HolidayApp.Infrastructure.Services;

/// <summary>Places you have been to, added by hand ("Where I've been" map).</summary>
public interface IVisitedPlaceService
{
    Task<IReadOnlyList<VisitedPlaceDto>> GetAllAsync(CancellationToken ct = default);
    Task<VisitedPlaceDto> AddAsync(SaveVisitedPlaceRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class VisitedPlaceService(AppDbContext db, ICurrentUser currentUser) : IVisitedPlaceService
{
    public async Task<IReadOnlyList<VisitedPlaceDto>> GetAllAsync(CancellationToken ct = default) =>
        await db.VisitedPlaces.AsNoTracking()
            .OrderByDescending(p => p.Year)
            .ThenBy(p => p.Name)
            .Select(p => new VisitedPlaceDto(p.Id, p.Name, p.Latitude, p.Longitude, p.Country, p.Year))
            .ToListAsync(ct);

    /// <summary>Adds a place (the request has already been validated).</summary>
    public async Task<VisitedPlaceDto> AddAsync(SaveVisitedPlaceRequest request, CancellationToken ct = default)
    {
        var place = new VisitedPlace
        {
            UserId = currentUser.UserId,
            Name = request.Name.Trim(),
            // 6 decimal places ≈ 10 cm: enough, and avoids storing noise
            Latitude = Math.Round(request.Latitude, 6),
            Longitude = Math.Round(request.Longitude, 6),
            Country = string.IsNullOrWhiteSpace(request.Country) ? null : request.Country.Trim(),
            Year = request.Year,
        };

        db.VisitedPlaces.Add(place);
        await db.SaveChangesAsync(ct);
        return new VisitedPlaceDto(place.Id, place.Name, place.Latitude, place.Longitude, place.Country, place.Year);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) =>
        await db.VisitedPlaces.Where(p => p.Id == id).ExecuteDeleteAsync(ct) > 0;
}
