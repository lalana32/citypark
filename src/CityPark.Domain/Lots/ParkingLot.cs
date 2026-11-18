using CityPark.Domain.Common;

namespace CityPark.Domain.Lots;

/// <summary>
/// A parking area with its hourly rate, daily opening hours and spaces. Aggregate root:
/// spaces are added only through the lot.
/// </summary>
public sealed class ParkingLot
{
    private readonly List<ParkingSpace> _spaces = [];

    private ParkingLot(string name, string address, Rate rate, OpeningHours openingHours)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Address = address;
        Rate = rate;
        OpeningHours = openingHours;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string Address { get; private set; }

    public Rate Rate { get; private set; }

    public OpeningHours OpeningHours { get; private set; }

    public IReadOnlyCollection<ParkingSpace> Spaces => _spaces.AsReadOnly();

    public static ParkingLot Create(string name, string address, Rate rate, OpeningHours openingHours)
    {
        ArgumentNullException.ThrowIfNull(rate);
        ArgumentNullException.ThrowIfNull(openingHours);

        return new ParkingLot(Required(name, nameof(Name)), Required(address, nameof(Address)), rate, openingHours);
    }

    public ParkingSpace AddSpace(string label)
    {
        var normalized = ParkingSpace.NormalizeLabel(label);

        if (_spaces.Exists(s => s.Label == normalized))
        {
            throw new DomainException($"Lot '{Name}' already has a space labelled '{normalized}'.");
        }

        var space = new ParkingSpace(normalized);
        _spaces.Add(space);
        return space;
    }

    private static string Required(string value, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);

        var trimmed = value.Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainException($"{name} must not be empty.");
        }

        return trimmed;
    }
}
