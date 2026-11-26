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
    /// <summary>Upper bound on inserts per lot request, so heavy contention cannot keep one request busy.</summary>
    public const int MaxAttempts = 10;

    /// <summary>R10: Active reservations one user may hold that have not started yet.</summary>
    public const int MaxUpcomingPerUser = 10;

    public async Task<Reservation> CreateAsync(
        Guid userId,
        Guid spaceId,
        DateTimeOffset start,
        DateTimeOffset end,
        string plate,
        CancellationToken cancellationToken = default)
    {
        var interval = new TimeInterval(start.ToUniversalTime(), end.ToUniversalTime());
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
        await EnsureUnderLimitAsync(userId, cancellationToken);
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
        catch (DbUpdateException ex) when (databaseErrors.IsPlateOverlapViolation(ex))
        {
            throw new PlateOverlapException(licensePlate, interval, ex);
        }

        return reservation;
    }

    /// <summary>Reserves the first free active space of the lot, by label.</summary>
    public async Task<Reservation> CreateInLotAsync(
        Guid userId,
        Guid lotId,
        DateTimeOffset start,
        DateTimeOffset end,
        string plate,
        CancellationToken cancellationToken = default)
    {
        var interval = new TimeInterval(start.ToUniversalTime(), end.ToUniversalTime());
        var licensePlate = new LicensePlate(plate);

        var lot = await db.ParkingLots
            .AsNoTracking()
            .Include(l => l.Spaces)
            .FirstOrDefaultAsync(l => l.Id == lotId, cancellationToken)
            ?? throw new NotFoundException($"Parking lot {lotId} was not found.");

        // Rules first: a broken rule is 422 even when the lot is full.
        Reservation.EnsureBookable(lot, interval, timeZone, clock);
        await EnsureUnderLimitAsync(userId, cancellationToken);

        // Only a hint: a parallel request can take any of these before our insert.
        var blocking = db.Reservations.Where(Reservation.ActiveAndOverlapping(interval));
        var candidates = await db.ParkingLots
            .Where(l => l.Id == lotId)
            .SelectMany(l => l.Spaces)
            .Where(s => s.IsActive && !blocking.Any(r => r.SpaceId == s.Id))
            .OrderBy(s => s.Label)
            .Select(s => s.Id)
            .Take(MaxAttempts)
            .ToListAsync(cancellationToken);

        foreach (var spaceId in candidates)
        {
            var space = lot.Spaces.Single(s => s.Id == spaceId);
            var reservation = Reservation.Create(lot, space, interval, licensePlate, userId, timeZone, clock);
            db.Reservations.Add(reservation);

            // Each save is its own transaction: after a failed statement Postgres would reject
            // every later one in the same transaction (25P02).
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return reservation;
            }
            catch (DbUpdateException ex) when (databaseErrors.IsSpaceOverlapViolation(ex))
            {
                // Lost the race for this space. Without detaching, the next save would retry this insert too.
                db.Reservations.Entry(reservation).State = EntityState.Detached;
            }
            catch (DbUpdateException ex) when (databaseErrors.IsPlateOverlapViolation(ex))
            {
                // The car is booked elsewhere: every other space would fail the same way.
                throw new PlateOverlapException(licensePlate, interval, ex);
            }
        }

        throw new NoFreeSpaceException(lotId, interval);
    }

    // A soft rule (product brief): parallel requests of one user can all pass the count. Unlike
    // R6 and R7, going over by a few harms nobody, so it is not worth a lock.
    private async Task EnsureUnderLimitAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var upcoming = await db.Reservations.CountAsync(
            r => r.UserId == userId && r.Status == ReservationStatus.Active && r.StartTime > now,
            cancellationToken);

        if (upcoming >= MaxUpcomingPerUser)
        {
            throw new ReservationLimitException(MaxUpcomingPerUser);
        }
    }
}
