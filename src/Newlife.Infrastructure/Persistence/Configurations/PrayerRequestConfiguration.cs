using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newlife.Domain.Entities;

namespace Newlife.Infrastructure.Persistence.Configurations;

public sealed class PrayerRequestConfiguration : IEntityTypeConfiguration<PrayerRequest>
{
    public void Configure(EntityTypeBuilder<PrayerRequest> builder)
    {
        builder.ToTable("prayer_requests");

        builder.HasKey(prayer => prayer.Id);
        builder.Property(prayer => prayer.Id).HasColumnName("id");
        builder.Property(prayer => prayer.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(prayer => prayer.Content).HasColumnName("content").HasMaxLength(4000).IsRequired();
        builder.Property(prayer => prayer.SubmitterName).HasColumnName("submitter_name").HasMaxLength(120);
        builder.Property(prayer => prayer.PhoneNumber).HasColumnName("phone_number").HasMaxLength(32);
        builder.Property(prayer => prayer.IsAnonymous).HasColumnName("is_anonymous");
        builder.Property(prayer => prayer.IsAnswered).HasColumnName("is_answered");
        builder.Property(prayer => prayer.AnsweredAt).HasColumnName("answered_at");
        builder.Property(prayer => prayer.IsFlagged).HasColumnName("is_flagged");
        builder.Property(prayer => prayer.FlagReason).HasColumnName("flag_reason").HasMaxLength(500);
        builder.Property(prayer => prayer.DeletedAt).HasColumnName("deleted_at");
        builder.Property(prayer => prayer.CreatedAt).HasColumnName("created_at");
        builder.Property(prayer => prayer.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(prayer => new { prayer.IsFlagged, prayer.DeletedAt, prayer.CreatedAt })
            .HasDatabaseName("ix_prayer_requests_wall")
            .IsDescending(false, false, true);
        builder.HasIndex(prayer => prayer.IsAnswered).HasDatabaseName("ix_prayer_requests_is_answered");
        builder.HasIndex(prayer => prayer.DeletedAt).HasDatabaseName("ix_prayer_requests_deleted_at");
    }
}
