namespace CityPark.Application.Admin;

/// <summary>The values an administrator sets on a lot. Opening hours are local Europe/Sarajevo times.</summary>
public sealed record LotDetails(
    string Name,
    string Address,
    decimal HourlyPrice,
    string Currency,
    TimeOnly OpensAt,
    TimeOnly ClosesAt);
