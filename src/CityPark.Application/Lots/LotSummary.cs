namespace CityPark.Application.Lots;

/// <summary>
/// A lot as a list or detail view shows it. <see cref="TotalSpaces"/> counts active spaces only.
/// Opening hours are local wall-clock times in the city's time zone.
/// </summary>
public sealed record LotSummary(
    Guid Id,
    string Name,
    string Address,
    decimal HourlyPrice,
    string Currency,
    TimeOnly OpensAt,
    TimeOnly ClosesAt,
    int TotalSpaces);
