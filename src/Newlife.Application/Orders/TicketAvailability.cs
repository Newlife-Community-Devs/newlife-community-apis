using Microsoft.EntityFrameworkCore;
using Newlife.Application.Common.Interfaces;
using Newlife.Domain.Entities;

namespace Newlife.Application.Orders;

public sealed class TicketAvailability(IAppDbContext db)
{
    public async Task<Dictionary<Guid, int>> GetConsumedMapAsync(
        IReadOnlyCollection<TicketType> types,
        CancellationToken cancellationToken)
    {
        if (types.Count == 0)
        {
            return [];
        }

        var typeIds = types.Select(type => type.Id).ToArray();

        return await db.Set<TicketType>()
            .AsNoTracking()
            .Where(type => typeIds.Contains(type.Id))
            .ToDictionaryAsync(type => type.Id, type => type.StockReserved, cancellationToken);
    }

    public async Task<bool> TryReserveAsync(
        Guid ticketTypeId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var rows = await db.Set<TicketType>()
            .Where(type => type.Id == ticketTypeId && type.StockReserved + quantity <= type.Quantity)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(type => type.StockReserved, type => type.StockReserved + quantity),
                cancellationToken);

        return rows == 1;
    }

    public async Task ReleaseStockAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var items = await db.Set<OrderItem>()
            .Where(item => item.OrderId == orderId)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            var quantity = item.Quantity;
            var ticketTypeId = item.TicketTypeId;

            await db.Set<TicketType>()
                .Where(type => type.Id == ticketTypeId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(type => type.StockReserved, type => Math.Max(0, type.StockReserved - quantity)),
                    cancellationToken);
        }
    }
}
