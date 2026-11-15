using CityPark.Domain.Lots;

namespace CityPark.Domain.Reservations;

public sealed class Reservation
{
    public Guid Id { get; set; }

    public Guid SpaceId { get; set; }

    public ParkingSpace Space { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public ReservationStatus Status { get; set; }
}
