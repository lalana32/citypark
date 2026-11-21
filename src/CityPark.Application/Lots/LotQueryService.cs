using CityPark.Application.Common;
using CityPark.Application.Persistence;
using CityPark.Domain.Lots;

using Microsoft.EntityFrameworkCore;

namespace CityPark.Application.Lots;

public sealed class LotQueryService(IAppDbContext db)
{
    public async Task<PagedResult<LotSummary>> ListAsync(PageRequest page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);

        var lots = db.ParkingLots.AsNoTracking();

        var totalCount = await lots.CountAsync(cancellationToken);

        // Id breaks ties between equal names, so OFFSET never repeats or skips a lot.
        var items = await ToSummaries(lots
                .OrderBy(l => l.Name)
                .ThenBy(l => l.Id)
                .Skip(page.Skip)
                .Take(page.PageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<LotSummary>(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<LotSummary> GetAsync(Guid lotId, CancellationToken cancellationToken = default) =>
        await ToSummaries(db.ParkingLots.AsNoTracking().Where(l => l.Id == lotId))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException($"Parking lot {lotId} was not found.");

    // A projection, not loaded entities: the space count becomes a COUNT subquery, no space rows are read.
    private static IQueryable<LotSummary> ToSummaries(IQueryable<ParkingLot> lots) =>
        lots.Select(l => new LotSummary(
            l.Id,
            l.Name,
            l.Address,
            l.Rate.HourlyPrice,
            l.Rate.Currency,
            l.OpeningHours.OpensAt,
            l.OpeningHours.ClosesAt,
            l.Spaces.Count(s => s.IsActive)));
}
