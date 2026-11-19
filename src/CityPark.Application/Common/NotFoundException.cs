namespace CityPark.Application.Common;

/// <summary>
/// The object a use case was asked about does not exist, for example an unknown lot id.
/// The API maps it to 404 Not Found.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}
