using Newlife.Domain.Common;
using Newlife.Domain.Enums;

namespace Newlife.Domain.Entities;

public sealed class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }

    public Guid TicketTypeId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}
