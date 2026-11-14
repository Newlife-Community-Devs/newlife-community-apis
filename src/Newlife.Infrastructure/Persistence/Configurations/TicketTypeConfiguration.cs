using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newlife.Domain.Entities;

namespace Newlife.Infrastructure.Persistence.Configurations;

public sealed class TicketTypeConfiguration : IEntityTypeConfiguration<TicketType>
{
    public void Configure(EntityTypeBuilder<TicketType> builder)
    {
        builder.ToTable("ticket_types");

        builder.HasKey(ticketType => ticketType.Id);
        builder.Property(ticketType => ticketType.Id).HasColumnName("id");
        builder.Property(ticketType => ticketType.EventId).HasColumnName("event_id");
        builder.Property(ticketType => ticketType.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(ticketType => ticketType.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(ticketType => ticketType.Price).HasColumnName("price").HasPrecision(12, 2);
        builder.Property(ticketType => ticketType.Quantity).HasColumnName("quantity");
        builder.Property(ticketType => ticketType.MaxPerOrder).HasColumnName("max_per_order");
        builder.Property(ticketType => ticketType.SalesStartAt).HasColumnName("sales_start_at");
        builder.Property(ticketType => ticketType.SalesEndAt).HasColumnName("sales_end_at");
        builder.Property(ticketType => ticketType.IsActive).HasColumnName("is_active");
        builder.Property(ticketType => ticketType.CreatedAt).HasColumnName("created_at");
        builder.Property(ticketType => ticketType.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(ticketType => new { ticketType.EventId, ticketType.IsActive });

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(ticketType => ticketType.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
