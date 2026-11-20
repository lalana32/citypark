namespace CityPark.Domain.Common;

public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The input the broken rule is about, for example Start; null when it is about no single input.</summary>
    public string? Field { get; init; }
}
