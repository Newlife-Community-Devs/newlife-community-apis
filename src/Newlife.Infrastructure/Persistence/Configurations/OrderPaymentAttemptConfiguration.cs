using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newlife.Domain.Entities;

namespace Newlife.Infrastructure.Persistence.Configurations;

public sealed class OrderPaymentAttemptConfiguration : IEntityTypeConfiguration<OrderPaymentAttempt>
{
    public void Configure(EntityTypeBuilder<OrderPaymentAttempt> builder)
    {
        builder.ToTable("order_payment_attempts");

        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Id).HasColumnName("id");
        builder.Property(attempt => attempt.OrderId).HasColumnName("order_id");
        builder.Property(attempt => attempt.ClientReference).HasColumnName("client_reference").HasMaxLength(32).IsRequired();
        builder.Property(attempt => attempt.AttemptNumber).HasColumnName("attempt_number");
        builder.Property(attempt => attempt.HubtelCheckoutId).HasColumnName("hubtel_checkout_id").HasMaxLength(64);
        builder.Property(attempt => attempt.CheckoutUrl).HasColumnName("checkout_url").HasMaxLength(1000);
        builder.Property(attempt => attempt.CreatedAt).HasColumnName("created_at");
        builder.Property(attempt => attempt.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(attempt => attempt.ClientReference).IsUnique();
        builder.HasIndex(attempt => attempt.OrderId);

        builder.HasOne<Domain.Entities.Order>()
            .WithMany()
            .HasForeignKey(attempt => attempt.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
