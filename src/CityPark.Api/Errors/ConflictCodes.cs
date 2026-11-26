namespace CityPark.Api.Errors;

/// <summary>Values of the <c>code</c> extension on a 409 from creating a reservation.</summary>
public static class ConflictCodes
{
    /// <summary>The space, or every free space of the lot, is taken for the interval (R6).</summary>
    public const string SpaceTaken = "space_taken";

    /// <summary>The plate already has an overlapping reservation, in any lot (R7).</summary>
    public const string PlateOverlap = "plate_overlap";

    /// <summary>The user already holds the most upcoming reservations allowed (R10).</summary>
    public const string ReservationLimit = "reservation_limit";
}
