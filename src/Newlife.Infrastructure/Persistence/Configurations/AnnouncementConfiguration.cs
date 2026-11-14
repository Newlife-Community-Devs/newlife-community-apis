using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newlife.Domain.Entities;

namespace Newlife.Infrastructure.Persistence.Configurations;

public sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("announcements");

        builder.HasKey(announcement => announcement.Id);
        builder.Property(announcement => announcement.Id).HasColumnName("id");
        builder.Property(announcement => announcement.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(announcement => announcement.Body).HasColumnName("body").IsRequired();
        builder.Property(announcement => announcement.IsPinned).HasColumnName("is_pinned");
        builder.Property(announcement => announcement.IsPublished).HasColumnName("is_published");
        builder.Property(announcement => announcement.PublishAt).HasColumnName("publish_at");
        builder.Property(announcement => announcement.ExpiresAt).HasColumnName("expires_at");
        builder.Property(announcement => announcement.CreatedAt).HasColumnName("created_at");
        builder.Property(announcement => announcement.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(announcement => new { announcement.IsPublished, announcement.IsPinned, announcement.PublishAt });
    }
}
