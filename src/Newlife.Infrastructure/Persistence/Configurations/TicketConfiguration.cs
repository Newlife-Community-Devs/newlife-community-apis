using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newlife.Domain.Entities;

namespace Newlife.Infrastructure.Persistence.Configurations;

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("tickets");

        builder.HasKey(ticket => ticket.Id);
        builder.Property(ticket => ticket.Id).HasColumnName("id");
        builder.Property(ticket => ticket.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        builder.Property(ticket => ticket.OrderId).HasColumnName("order_id");
        builder.Property(ticket => ticket.OrderItemId).HasColumnName("order_item_id");
        builder.Property(ticket => ticket.TicketTypeId).HasColumnName("ticket_type_id");
        builder.Property(ticket => ticket.EventId).HasColumnName("event_id");
        builder.Property(ticket => ticket.HolderName).HasColumnName("holder_name").HasMaxLength(200);
        builder.Property(ticket => ticket.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>();
        builder.Property(ticket => ticket.CheckedInAt).HasColumnName("checked_in_at");
        builder.Property(ticket => ticket.CheckedInByUserId).HasColumnName("checked_in_by_user_id");
        builder.Property(ticket => ticket.CreatedAt).HasColumnName("created_at");
        builder.Property(ticket => ticket.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(ticket => ticket.Code).IsUnique();
        builder.HasIndex(ticket => ticket.OrderId);
        builder.HasIndex(ticket => new { ticket.EventId, ticket.Status });

        builder.HasOne<Domain.Entities.Order>()
            .WithMany()
            .HasForeignKey(ticket => ticket.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<OrderItem>()
            .WithMany()
            .HasForeignKey(ticket => ticket.OrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TicketType>()
            .WithMany()
            .HasForeignKey(ticket => ticket.TicketTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(ticket => ticket.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AdminUser>()
            .WithMany()
            .HasForeignKey(ticket => ticket.CheckedInByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
