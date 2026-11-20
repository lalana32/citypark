using System.Linq.Expressions;

using CityPark.Domain.Common;
using CityPark.Domain.Lots;

namespace CityPark.Domain.Reservations;

public sealed class Reservation
{
    // Rule values from docs/product-brief.md (R1, R2, R4, R5).
    public static readonly TimeSpan Grid = TimeSpan.FromMinutes(15);

    public static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan BookingHorizon = TimeSpan.FromDays(30);

    public static readonly TimeSpan StartTolerance = TimeSpan.FromMinutes(5);

    // Used by Create and by EF Core to materialize rows; no other code can build a reservation.
    private Reservation()
    {
    }

    public Guid Id { get; private set; }

    public Guid SpaceId { get; private set; }

    /// <summary>The ASP.NET Core Identity user who made the reservation (ADR-003).</summary>
    public Guid UserId { get; private set; }

    public ParkingSpace Space { get; private set; } = null!;

    // StartTime and EndTime have Kind = Utc, as Npgsql needs for timestamptz (ADR-004).
    public DateTime StartTime { get; private set; }

    public DateTime EndTime { get; private set; }

    public ReservationStatus Status { get; private set; }

    public LicensePlate Plate { get; private set; } = null!;

    /// <summary>Snapshot taken at creation; later rate changes do not touch it (R13, I6).</summary>
    public decimal Price { get; private set; }

    public string Currency { get; private set; } = "";

    public TimeInterval Interval =>
        new(new DateTimeOffset(StartTime, TimeSpan.Zero), new DateTimeOffset(EndTime, TimeSpan.Zero));

    public static Reservation Create(
        ParkingLot lot,
        ParkingSpace space,
        TimeInterval interval,
        LicensePlate plate,
        Guid userId,
        TimeZoneInfo timeZone,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(lot);
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(interval);
        ArgumentNullException.ThrowIfNull(plate);
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(clock);

        // An empty id is a bug in the caller (the user comes from the sign-in cookie), not a broken business rule.
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id must not be empty.", nameof(userId));
        }

        if (!lot.Spaces.Contains(space))
        {
            throw new DomainException($"Space '{space.Label}' does not belong to lot '{lot.Name}'.");
        }

        EnsureBookable(lot, interval, timeZone, clock);

        return new Reservation
        {
            Id = Guid.CreateVersion7(),
            SpaceId = space.Id,
            UserId = userId,
            StartTime = interval.Start.UtcDateTime,
            EndTime = interval.End.UtcDateTime,
            Status = ReservationStatus.Active,
            Plate = plate,
            Price = lot.Rate.PriceFor(interval.Duration),
            Currency = lot.Rate.Currency,
        };
    }

    public static void EnsureBookable(ParkingLot lot, TimeInterval interval, TimeZoneInfo timeZone, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(lot);
        ArgumentNullException.ThrowIfNull(interval);
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(clock);

        if (interval.Duration < MinimumDuration)
        {
            throw new DomainException(
                $"A reservation must last at least {MinimumDuration.TotalMinutes} minutes, but lasts {interval.Duration.TotalMinutes}.");
        }

        EnsureOnGrid(interval.Start, timeZone, nameof(TimeInterval.Start));
        EnsureOnGrid(interval.End, timeZone, nameof(TimeInterval.End));

        if (!lot.OpeningHours.Contains(interval, timeZone))
        {
            throw new DomainException(
                $"A reservation must lie within one day and inside the opening hours " +
                $"{lot.OpeningHours.OpensAt:HH:mm}–{lot.OpeningHours.ClosesAt:HH:mm}.");
        }

        var now = clock.GetUtcNow();

        if (interval.Start > now + BookingHorizon)
        {
            throw new DomainException($"A reservation may start at most {BookingHorizon.TotalDays} days ahead.");
        }

        if (interval.Start < now - StartTolerance)
        {
            throw new DomainException(
                $"A reservation may start at most {StartTolerance.TotalMinutes} minutes in the past.");
        }
    }

    public static Expression<Func<Reservation, bool>> ActiveAndOverlapping(TimeInterval interval)
    {
        ArgumentNullException.ThrowIfNull(interval);

        // Locals, not interval.Start inside the lambda: EF Core sends them as two timestamptz parameters.
        var start = interval.Start.UtcDateTime;
        var end = interval.End.UtcDateTime;

        return r => r.Status == ReservationStatus.Active && r.StartTime < end && start < r.EndTime;
    }

    /// <summary>Derived from time, never stored; the end is exclusive.</summary>
    public bool IsFinished(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        return clock.GetUtcNow() >= Interval.End;
    }

    /// <summary>
    /// Static and on plain values, so a query can call it on projected columns without loading the entity.
    /// Cancelled wins over time; the end is exclusive.
    /// </summary>
    public static ReservationState StateAt(ReservationStatus status, DateTime startUtc, DateTime endUtc, DateTimeOffset now)
    {
        if (status == ReservationStatus.Cancelled)
        {
            return ReservationState.Cancelled;
        }

        var start = new DateTimeOffset(startUtc, TimeSpan.Zero);
        var end = new DateTimeOffset(endUtc, TimeSpan.Zero);

        if (now < start)
        {
            return ReservationState.Upcoming;
        }

        return now < end ? ReservationState.InProgress : ReservationState.Finished;
    }

    /// <summary>R9 and I9: only Active → Cancelled.</summary>
    public void Cancel(CancellationActor actor, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        // Checked first: a second cancel is a conflict with the current state, whoever sends it.
        if (Status == ReservationStatus.Cancelled)
        {
            throw new DomainConflictException($"Reservation {Id} is already cancelled.");
        }

        var mayCancel = actor switch
        {
            CancellationActor.Customer => clock.GetUtcNow() < Interval.Start,
            CancellationActor.Administrator => true,
            _ => throw new ArgumentOutOfRangeException(nameof(actor), actor, "Unknown cancellation actor."),
        };

        if (!mayCancel)
        {
            throw new DomainException("A customer can cancel a reservation only before it starts.");
        }

        Status = ReservationStatus.Cancelled;
    }

    // Checked in local time, so the rule does not rely on the zone offset being whole hours.
    private static void EnsureOnGrid(DateTimeOffset instant, TimeZoneInfo timeZone, string name)
    {
        var local = TimeZoneInfo.ConvertTime(instant, timeZone);

        if (local.TimeOfDay.Ticks % Grid.Ticks != 0)
        {
            throw new DomainException(
                $"{name} ({local:HH:mm:ss}) must fall on the {Grid.TotalMinutes}-minute grid (:00, :15, :30, :45) (R1).")
            {
                Field = name,
            };
        }
    }
}
