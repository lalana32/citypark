using CityPark.Domain.Common;

namespace CityPark.Domain.Lots;

/// <summary>
/// A parking area with its hourly rate, daily opening hours and spaces. Aggregate root:
/// spaces are added only through the lot.
/// </summary>
public sealed class ParkingLot
{
    private readonly List<ParkingSpace> _spaces = [];

    // Used by EF Core to materialize rows: it cannot pass Rate and OpeningHours to a constructor.
    private ParkingLot()
    {
    }

    private ParkingLot(string name, string address, Rate rate, OpeningHours openingHours)
    {
        Id = Guid.CreateVersion7();
        Name = name;
        Address = address;
        Rate = rate;
        OpeningHours = openingHours;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = "";

    public string Address { get; private set; } = "";

    public Rate Rate { get; private set; } = null!;

    public OpeningHours OpeningHours { get; private set; } = null!;

    public IReadOnlyCollection<ParkingSpace> Spaces => _spaces.AsReadOnly();

    public static ParkingLot Create(string name, string address, Rate rate, OpeningHours openingHours)
    {
        ArgumentNullException.ThrowIfNull(rate);
        ArgumentNullException.ThrowIfNull(openingHours);

        return new ParkingLot(Required(name, nameof(Name)), Required(address, nameof(Address)), rate, openingHours);
    }

    /// <summary>
    /// Replaces the editable values. Existing reservations keep their price and stay valid,
    /// because each one stores its own snapshot (R13, I6).
    /// </summary>
    public void Update(string name, string address, Rate rate, OpeningHours openingHours)
    {
        ArgumentNullException.ThrowIfNull(rate);
        ArgumentNullException.ThrowIfNull(openingHours);

        // Both strings are checked before anything changes, so a failed update leaves the lot as it was.
        var trimmedName = Required(name, nameof(Name));
        var trimmedAddress = Required(address, nameof(Address));

        Name = trimmedName;
        Address = trimmedAddress;
        Rate = rate;
        OpeningHours = openingHours;
    }

    public ParkingSpace AddSpace(string label)
    {
        var normalized = ParkingSpace.NormalizeLabel(label);

        // A conflict with the lot's current spaces, not bad input: the same label is fine in another lot (I1).
        if (_spaces.Exists(s => s.Label == normalized))
        {
            throw new DomainConflictException($"Lot '{Name}' already has a space labelled '{normalized}'.");
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
            throw new DomainException($"{name} must not be empty.") { Field = name };
        }

        return trimmed;
    }
}
