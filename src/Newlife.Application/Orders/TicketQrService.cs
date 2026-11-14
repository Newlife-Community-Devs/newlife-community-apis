using Microsoft.EntityFrameworkCore;
using Newlife.Application.Common.Exceptions;
using Newlife.Application.Common.Interfaces;
using Newlife.Domain.Entities;

namespace Newlife.Application.Orders;

public sealed class TicketQrService(IAppDbContext db)
{
    public async Task<string> GetCodeAsync(string code, Guid orderToken, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();

        var exists = await db.Set<Ticket>()
            .AsNoTracking()
            .AnyAsync(ticket =>
                ticket.Code == normalized &&
                db.Set<Order>().Any(order => order.Id == ticket.OrderId && order.OrderToken == orderToken),
                cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("ticket.not_found", "Ticket was not found.");
        }

        return normalized;
    }
}
