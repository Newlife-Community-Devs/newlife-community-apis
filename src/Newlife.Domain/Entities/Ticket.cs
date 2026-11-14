using Newlife.Domain.Common;
using Newlife.Domain.Enums;

namespace Newlife.Domain.Entities;

public sealed class Ticket : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public Guid OrderId { get; set; }

    public Guid OrderItemId { get; set; }

    public Guid TicketTypeId { get; set; }

    public Guid EventId { get; set; }

    public string? HolderName { get; set; }

    public TicketStatus Status { get; set; }

    public DateTime? CheckedInAt { get; set; }

    public Guid? CheckedInByUserId { get; set; }
}
