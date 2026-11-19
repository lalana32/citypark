using CityPark.Application.Common;
using CityPark.Application.Persistence;
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

        await db.SaveChangesAsync(cancellationToken);
    }
}
