using CityPark.Domain.Common;

namespace CityPark.Domain.Lots;

/// <summary>
/// One physical place in a lot. Created only through <see cref="ParkingLot.AddSpace"/>,
/// which keeps labels unique within the lot.
/// </summary>
public sealed class ParkingSpace
{
    // Used by EF Core to materialize rows.
    private ParkingSpace()
    {
    }

    internal ParkingSpace(string label)
    {
        Id = Guid.NewGuid();
        Label = NormalizeLabel(label);
    }

    public Guid Id { get; private set; }

    public string Label { get; private set; } = "";

    public bool IsActive { get; private set; } = true;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    internal static string NormalizeLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        var normalized = label.Trim().ToUpperInvariant();

        if (normalized.Length == 0)
        {
            throw new DomainException("Space label must not be empty.");
        }

        return normalized;
    }
}
