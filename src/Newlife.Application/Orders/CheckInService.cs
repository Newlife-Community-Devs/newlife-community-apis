using Microsoft.EntityFrameworkCore;
using Newlife.Application.Common.Exceptions;
using Newlife.Application.Common.Interfaces;
using Newlife.Application.Orders.DTOs;
using Newlife.Domain.Enums;

namespace Newlife.Application.Orders;

public sealed class CheckInService(IAppDbContext db)
{
    public async Task<CheckInResponse> CheckInAsync(string code, Guid? userId, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();

        var ticket = await db.Set<Domain.Entities.Ticket>()
            .FirstOrDefaultAsync(item => item.Code == normalized, cancellationToken);

        if (ticket is null)
        {
            return new CheckInResponse(CheckInResultType.Invalid, null, null);
        }

        var typeName = await db.Set<Domain.Entities.TicketType>()
            .AsNoTracking()
            .Where(type => type.Id == ticket.TicketTypeId)
            .Select(type => type.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var responseTicket = new CheckInTicketResponse(
            ticket.Code, ticket.HolderName, typeName, ticket.Status);

        if (ticket.Status == TicketStatus.Cancelled)
        {
            return new CheckInResponse(CheckInResultType.Cancelled, responseTicket, null);
        }

        if (ticket.Status == TicketStatus.CheckedIn)
        {
            return new CheckInResponse(CheckInResultType.AlreadyCheckedIn, responseTicket, ticket.CheckedInAt);
        }

        var now = DateTime.UtcNow;
        ticket.Status = TicketStatus.CheckedIn;
        ticket.CheckedInAt = now;
        ticket.CheckedInByUserId = userId;
        await db.SaveChangesAsync(cancellationToken);

        return new CheckInResponse(
            CheckInResultType.CheckedIn,
            responseTicket with { Status = TicketStatus.CheckedIn },
            now);
    }

    public async Task<CheckInStatsResponse> GetStatsAsync(Guid? eventId, CancellationToken cancellationToken)
    {
        var source = db.Set<Domain.Entities.Ticket>().AsNoTracking();

        if (eventId.HasValue)
        {
            source = source.Where(ticket => ticket.EventId == eventId.Value);
        }

        var total = await source.CountAsync(ticket => ticket.Status != TicketStatus.Cancelled, cancellationToken);
        var checkedIn = await source.CountAsync(ticket => ticket.Status == TicketStatus.CheckedIn, cancellationToken);

        return new CheckInStatsResponse(total, checkedIn, total - checkedIn);
    }
}
