using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newlife.Domain.Entities;

namespace Newlife.Infrastructure.Persistence.Configurations;

public sealed class SermonEpisodeConfiguration : IEntityTypeConfiguration<SermonEpisode>
{
    public void Configure(EntityTypeBuilder<SermonEpisode> builder)
    {
        builder.ToTable("sermon_episodes");

        builder.HasKey(episode => episode.Id);
        builder.Property(episode => episode.Id).HasColumnName("id");
        builder.Property(episode => episode.FeedGuid).HasColumnName("feed_guid").HasMaxLength(300).IsRequired();
        builder.Property(episode => episode.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(episode => episode.DescriptionHtml).HasColumnName("description_html");
        builder.Property(episode => episode.Summary).HasColumnName("summary").HasMaxLength(1000);
        builder.Property(episode => episode.AudioUrl).HasColumnName("audio_url").HasMaxLength(1000).IsRequired();
        builder.Property(episode => episode.DurationSeconds).HasColumnName("duration_seconds");
        builder.Property(episode => episode.ArtworkUrl).HasColumnName("artwork_url").HasMaxLength(1000);
        builder.Property(episode => episode.PublishedAt).HasColumnName("published_at");
        builder.Property(episode => episode.IsVisible).HasColumnName("is_visible");
        builder.Property(episode => episode.CreatedAt).HasColumnName("created_at");
        builder.Property(episode => episode.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(episode => episode.FeedGuid).IsUnique();
        builder.HasIndex(episode => new { episode.IsVisible, episode.PublishedAt })
            .HasDatabaseName("ix_sermon_episodes_visible_published")
            .IsDescending(false, true);
    }
}
