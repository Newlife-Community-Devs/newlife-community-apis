using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newlife.Domain.Entities;

namespace Newlife.Infrastructure.Persistence.Configurations;

public sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("media_assets");

        builder.HasKey(media => media.Id);
        builder.Property(media => media.Id).HasColumnName("id");
        builder.Property(media => media.StorageKey).HasColumnName("storage_key").HasMaxLength(300).IsRequired();
        builder.Property(media => media.Url).HasColumnName("url").HasMaxLength(1000).IsRequired();
        builder.Property(media => media.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        builder.Property(media => media.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        builder.Property(media => media.SizeBytes).HasColumnName("size_bytes");
        builder.Property(media => media.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(media => media.AltText).HasColumnName("alt_text").HasMaxLength(300);
        builder.Property(media => media.CreatedAt).HasColumnName("created_at");
        builder.Property(media => media.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(media => media.StorageKey).IsUnique();
    }
}
