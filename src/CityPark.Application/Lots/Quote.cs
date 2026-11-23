namespace CityPark.Application.Lots;

/// <summary>
/// What a reservation of a lot for one interval would cost: <see cref="BilledHours"/> are the
/// started hours (R8), <see cref="Price"/> is in <see cref="Currency"/>.
/// </summary>
public sealed record Quote(int BilledHours, decimal Price, string Currency);
