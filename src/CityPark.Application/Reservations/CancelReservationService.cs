using CityPark.Application.Common;
using CityPark.Application.Persistence;
using CityPark.Domain.Common;
using CityPark.Domain.Reservations;

using Microsoft.EntityFrameworkCore;

namespace CityPark.Application.Reservations;

public sealed class CancelReservationService(IAppDbContext db, TimeProvider clock)
{
    public async Task CancelAsync(Guid reservationId, Actor actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var reservation = await db.Reservations
            .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);

        // A customer gets the same answer for a foreign reservation as for a missing one,
        // so reservation ids of other users cannot be probed.
        if (reservation is null
            || (actor.Role == CancellationActor.Customer && reservation.UserId != actor.UserId))
        {
            throw new NotFoundException($"Reservation {reservationId} was not found.");
        }

        reservation.Cancel(actor.Role, clock);

        // A parallel request changed the row after we read it (xmin token), e.g. a double click.
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new DomainConflictException($"Reservation {reservationId} was changed by another request.", ex);
        }
    }
}
