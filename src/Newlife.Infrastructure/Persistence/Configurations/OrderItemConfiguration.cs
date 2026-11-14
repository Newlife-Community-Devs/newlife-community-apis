using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newlife.Domain.Entities;

namespace Newlife.Infrastructure.Persistence.Configurations;

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.OrderId).HasColumnName("order_id");
        builder.Property(item => item.TicketTypeId).HasColumnName("ticket_type_id");
        builder.Property(item => item.Quantity).HasColumnName("quantity");
        builder.Property(item => item.UnitPrice).HasColumnName("unit_price").HasPrecision(12, 2);
        builder.Property(item => item.CreatedAt).HasColumnName("created_at");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(item => new { item.OrderId, item.TicketTypeId }).IsUnique();

        builder.HasOne<Domain.Entities.Order>()
            .WithMany()
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<TicketType>()
            .WithMany()
            .HasForeignKey(item => item.TicketTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
