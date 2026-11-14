using System.Threading.Channels;
using Newlife.Domain.Entities;

namespace Newlife.Application.Orders;

public sealed class FulfillmentQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

    public ValueTask WriteAsync(Guid orderId, CancellationToken cancellationToken)
        => _channel.Writer.WriteAsync(orderId, cancellationToken);

    public IAsyncEnumerable<Guid> Reader => _channel.Reader.ReadAllAsync();
}

public static class OrderTicketIssuer
{
    public static IReadOnlyList<Ticket> Issue(Order order, IReadOnlyList<OrderItem> items, string? ticketTypeName = null)
    {
        var tickets = new List<Ticket>();

        foreach (var item in items)
        {
            for (var seat = 0; seat < item.Quantity; seat++)
            {
                tickets.Add(new Ticket
                {
                    Code = TicketCodeGenerator.GenerateTicketCode(),
                    OrderId = order.Id,
                    OrderItemId = item.Id,
                    TicketTypeId = item.TicketTypeId,
                    EventId = order.EventId,
                    HolderName = order.BuyerFullName,
                    Status = Domain.Enums.TicketStatus.Valid
                });
            }
        }

        return tickets;
    }
}
