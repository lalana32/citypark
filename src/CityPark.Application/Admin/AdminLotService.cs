using CityPark.Application.Common;
using CityPark.Application.Persistence;
using CityPark.Domain.Common;
using CityPark.Domain.Lots;
using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;

namespace CityPark.Application.Admin;

/// <summary>Inventory changes by an administrator. The API allows only the Admin role to call it.</summary>
public sealed class AdminLotService(IAppDbContext db, IDatabaseErrors databaseErrors, TimeProvider clock)
{
    public async Task<ParkingLot> CreateLotAsync(LotDetails details, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(details);

        var lot = ParkingLot.Create(details.Name, details.Address, RateOf(details), OpeningHoursOf(details));

        db.ParkingLots.Add(lot);
        await db.SaveChangesAsync(cancellationToken);

        return lot;
    }

    public async Task UpdateLotAsync(Guid lotId, LotDetails details, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(details);

        var lot = await db.ParkingLots.FirstOrDefaultAsync(l => l.Id == lotId, cancellationToken)
            ?? throw LotNotFound(lotId);

        lot.Update(details.Name, details.Address, RateOf(details), OpeningHoursOf(details));

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ParkingSpace> AddSpaceAsync(Guid lotId, string label, CancellationToken cancellationToken = default)
    {
        var lot = await LoadWithSpacesAsync(lotId, cancellationToken);

        var space = lot.AddSpace(label);

        // AddSpace sees only the spaces loaded above; a parallel request with the same label is
        // stopped by the unique index (I1).
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (databaseErrors.IsSpaceLabelViolation(ex))
        {
            throw new DomainConflictException($"Lot '{lot.Name}' already has a space labelled '{space.Label}'.", ex);
        }

        return space;
    }

    public async Task DeactivateSpaceAsync(Guid lotId, Guid spaceId, CancellationToken cancellationToken = default)
    {
        var lot = await LoadWithSpacesAsync(lotId, cancellationToken);

        var space = lot.Spaces.FirstOrDefault(s => s.Id == spaceId)
            ?? throw new NotFoundException($"Parking space {spaceId} was not found in lot {lotId}.");

        // A reservation in progress blocks too: its car is still in the space.
        var now = clock.GetUtcNow().UtcDateTime;
        var blocking = await db.Reservations
            .Where(r => r.SpaceId == spaceId && r.Status == ReservationStatus.Active && r.EndTime > now)
            .OrderBy(r => r.StartTime)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (blocking.Count > 0)
        {
            throw new SpaceHasActiveReservationsException(spaceId, blocking);
        }

        space.Deactivate();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ParkingLot> LoadWithSpacesAsync(Guid lotId, CancellationToken cancellationToken) =>
        await db.ParkingLots
            .Include(l => l.Spaces)
            .FirstOrDefaultAsync(l => l.Id == lotId, cancellationToken)
        ?? throw LotNotFound(lotId);

    private static Rate RateOf(LotDetails details) => new(details.HourlyPrice, details.Currency);

    private static OpeningHours OpeningHoursOf(LotDetails details) => new(details.OpensAt, details.ClosesAt);

    private static NotFoundException LotNotFound(Guid lotId) => new($"Parking lot {lotId} was not found.");
}
