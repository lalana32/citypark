using CityPark.Application.Common;
using CityPark.Application.Persistence;
using CityPark.Domain.Common;
using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;

namespace CityPark.Application.Reservations;

public sealed class CreateReservationService(
    IAppDbContext db,
    IDatabaseErrors databaseErrors,
    TimeProvider clock,
    TimeZoneInfo timeZone)
{
    public async Task<Reservation> CreateAsync(
        Guid userId,
        Guid spaceId,
        DateTimeOffset start,
        DateTimeOffset end,
        string plate,
        CancellationToken cancellationToken = default)
    {
        var interval = new TimeInterval(start, end);
        var licensePlate = new LicensePlate(plate);

        var lot = await db.ParkingLots
            .AsNoTracking()
            .Include(l => l.Spaces)
            .FirstOrDefaultAsync(l => l.Spaces.Any(s => s.Id == spaceId), cancellationToken)
            ?? throw new NotFoundException($"Parking space {spaceId} was not found.");

        var space = lot.Spaces.Single(s => s.Id == spaceId);

        if (!space.IsActive)
        {
            throw new DomainException($"Space '{space.Label}' is inactive and cannot be reserved (R11).");
        }

        var reservation = Reservation.Create(lot, space, interval, licensePlate, userId, timeZone, clock);
        db.Reservations.Add(reservation);

        // No "is it free?" query first: it would race with a parallel request. The exclusion
        // constraint decides, under the default READ COMMITTED isolation (ADR-002).
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (databaseErrors.IsSpaceOverlapViolation(ex))
        {
            throw new ReservationConflictException(spaceId, interval, ex);
        }

        return reservation;
    }
}
