namespace CityPark.Application.Common;

/// <summary>
/// A 1-based page. Values below 1 are rejected; a page size above <see cref="MaxPageSize"/> is clamped,
/// and <see cref="PageSize"/> holds the size actually used (docs/api.md).
/// </summary>
public sealed record PageRequest
{
    public const int MaxPageSize = 50;

    public PageRequest(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new RequestValidationException($"Page must be 1 or greater, but is {page}.");
        }

        if (pageSize < 1)
        {
            throw new RequestValidationException($"Page size must be 1 or greater, but is {pageSize}.");
        }

        Page = page;
        PageSize = Math.Min(pageSize, MaxPageSize);

        // Computed in long: page int.MaxValue would overflow int and give a negative offset.
        var skip = (long)(page - 1) * PageSize;

        if (skip > int.MaxValue)
        {
            throw new RequestValidationException($"Page {page} is too large for page size {PageSize}.");
        }

        Skip = (int)skip;
    }

    public int Page { get; }

    public int PageSize { get; }

    /// <summary>Rows before this page, for OFFSET.</summary>
    public int Skip { get; }
}
