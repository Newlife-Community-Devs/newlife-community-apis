using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newlife.Domain.Entities;

namespace Newlife.Infrastructure.Persistence.Configurations;

public sealed class SiteSettingConfiguration : IEntityTypeConfiguration<SiteSetting>
{
    public void Configure(EntityTypeBuilder<SiteSetting> builder)
    {
        builder.ToTable("site_settings");

        builder.HasKey(setting => setting.Id);
        builder.Property(setting => setting.Id).HasColumnName("id");
        builder.Property(setting => setting.Key).HasColumnName("key").HasMaxLength(100).IsRequired();
        builder.Property(setting => setting.Value).HasColumnName("value").IsRequired();
        builder.Property(setting => setting.Group).HasColumnName("group").HasMaxLength(50).IsRequired();
        builder.Property(setting => setting.CreatedAt).HasColumnName("created_at");
        builder.Property(setting => setting.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(setting => setting.Key).IsUnique();
    }
}
