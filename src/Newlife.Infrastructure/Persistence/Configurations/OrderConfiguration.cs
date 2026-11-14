using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newlife.Domain.Entities;

namespace Newlife.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).HasColumnName("id");
        builder.Property(order => order.OrderNumber).HasColumnName("order_number").HasMaxLength(20).IsRequired();
        builder.Property(order => order.OrderToken).HasColumnName("order_token");
        builder.Property(order => order.EventId).HasColumnName("event_id");
        builder.Property(order => order.BuyerFullName).HasColumnName("buyer_full_name").HasMaxLength(200).IsRequired();
        builder.Property(order => order.BuyerEmail).HasColumnName("buyer_email").HasMaxLength(320).IsRequired();
        builder.Property(order => order.BuyerPhone).HasColumnName("buyer_phone").HasMaxLength(32).IsRequired();
        builder.Property(order => order.Status).HasColumnName("status").HasMaxLength(30).HasConversion<string>();
        builder.Property(order => order.TotalAmount).HasColumnName("total_amount").HasPrecision(12, 2);
        builder.Property(order => order.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(order => order.HubtelSalesInvoiceId).HasColumnName("hubtel_sales_invoice_id").HasMaxLength(64);
        builder.Property(order => order.HubtelTransactionId).HasColumnName("hubtel_transaction_id").HasMaxLength(64);
        builder.Property(order => order.HubtelRefundId).HasColumnName("hubtel_refund_id").HasMaxLength(64);
        builder.Property(order => order.PaymentMethod).HasColumnName("payment_method").HasMaxLength(30);
        builder.Property(order => order.PaidAt).HasColumnName("paid_at");
        builder.Property(order => order.RefundedAt).HasColumnName("refunded_at");
        builder.Property(order => order.RequiresReview).HasColumnName("requires_review");
        builder.Property(order => order.ReviewReason).HasColumnName("review_reason").HasMaxLength(300);
        builder.Property(order => order.HoldExpiresAt).HasColumnName("hold_expires_at");
        builder.Property(order => order.CreatedAt).HasColumnName("created_at");
        builder.Property(order => order.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(order => order.OrderNumber).IsUnique();
        builder.HasIndex(order => order.OrderToken).IsUnique();
        builder.HasIndex(order => new { order.EventId, order.Status });
        builder.HasIndex(order => new { order.Status, order.CreatedAt });
        builder.HasIndex(order => order.HubtelTransactionId)
            .IsUnique()
            .HasDatabaseName("ix_orders_hubtel_transaction_id")
            .HasFilter("hubtel_transaction_id IS NOT NULL");

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(order => order.EventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
