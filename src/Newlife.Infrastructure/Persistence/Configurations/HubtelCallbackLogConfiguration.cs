using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newlife.Domain.Entities;

namespace Newlife.Infrastructure.Persistence.Configurations;

public sealed class HubtelCallbackLogConfiguration : IEntityTypeConfiguration<HubtelCallbackLog>
{
    public void Configure(EntityTypeBuilder<HubtelCallbackLog> builder)
    {
        builder.ToTable("hubtel_callback_logs");

        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).HasColumnName("id");
        builder.Property(log => log.ReceivedAt).HasColumnName("received_at");
        builder.Property(log => log.SourceIp).HasColumnName("source_ip").HasMaxLength(64);
        builder.Property(log => log.ClientReference).HasColumnName("client_reference").HasMaxLength(64);
        builder.Property(log => log.Payload).HasColumnName("payload").HasColumnType("jsonb");
        builder.Property(log => log.ProcessingResult).HasColumnName("processing_result").HasMaxLength(100);
        builder.Property(log => log.ProcessedAt).HasColumnName("processed_at");
        builder.Property(log => log.CreatedAt).HasColumnName("created_at");
        builder.Property(log => log.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(log => log.ClientReference);
    }
}
