namespace CityPark.Domain.Common;

/// <summary>
/// A rule broken by the current state of an object rather than by the input, for example
/// cancelling a reservation that is already cancelled. The API maps it to 409 Conflict.
/// </summary>
public class DomainConflictException : DomainException
{
    public DomainConflictException(string message)
        : base(message)
    {
    }

    public DomainConflictException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
