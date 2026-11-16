using Newlife.Domain.Common;

namespace Newlife.Domain.Entities;

public sealed class TicketType : BaseEntity
{
    public Guid EventId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int Quantity { get; set; }

    public int StockReserved { get; set; }

    public int MaxPerOrder { get; set; } = 10;

    public DateTime? SalesStartAt { get; set; }

    public DateTime? SalesEndAt { get; set; }

    public bool IsActive { get; set; } = true;
}
