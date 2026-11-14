using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newlife.Domain.Entities;

namespace Newlife.Infrastructure.Persistence.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");

        builder.HasKey(evt => evt.Id);
        builder.Property(evt => evt.Id).HasColumnName("id");
        builder.Property(evt => evt.Slug).HasColumnName("slug").HasMaxLength(200).IsRequired();
        builder.Property(evt => evt.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(evt => evt.Description).HasColumnName("description");
        builder.Property(evt => evt.Venue).HasColumnName("venue").HasMaxLength(300);
        builder.Property(evt => evt.StartAt).HasColumnName("start_at");
        builder.Property(evt => evt.EndAt).HasColumnName("end_at");
        builder.Property(evt => evt.BannerMediaId).HasColumnName("banner_media_id");
        builder.Property(evt => evt.BannerUrl).HasColumnName("banner_url").HasMaxLength(1000);
        builder.Property(evt => evt.IsPublished).HasColumnName("is_published");
        builder.Property(evt => evt.IsFeatured).HasColumnName("is_featured");
        builder.Property(evt => evt.TicketingEnabled).HasColumnName("ticketing_enabled");
        builder.Property(evt => evt.CreatedAt).HasColumnName("created_at");
        builder.Property(evt => evt.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(evt => evt.Slug).IsUnique();
        builder.HasIndex(evt => new { evt.IsPublished, evt.StartAt });

        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(evt => evt.BannerMediaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
