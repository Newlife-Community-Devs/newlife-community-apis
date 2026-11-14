using System.ServiceModel.Syndication;
using System.Xml;
using System.Xml.Linq;
using Newlife.Application.Common.Interfaces;

namespace Newlife.Infrastructure.Sermons;

public static class SermonFeedParser
{
    public static IReadOnlyList<FeedEpisode> Parse(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml));
        var feed = SyndicationFeed.Load(reader);
        var feedImage = feed.ImageUrl?.AbsoluteUri;
        var episodes = new List<FeedEpisode>();

        foreach (var item in feed.Items)
        {
            var guid = item.Id?.Trim();
            var enclosure = item.Links.FirstOrDefault(link => link.RelationshipType == "enclosure");

            if (string.IsNullOrEmpty(guid) || enclosure?.Uri is null)
            {
                continue;
            }

            var published = item.PublishDate != default
                ? item.PublishDate
                : item.LastUpdatedTime != default
                    ? item.LastUpdatedTime
                    : new DateTimeOffset(DateTime.UtcNow);

            episodes.Add(new FeedEpisode(
                guid,
                item.Title?.Text?.Trim() ?? string.Empty,
                item.Summary?.Text ?? string.Empty,
                enclosure.Uri.AbsoluteUri,
                published.ToUniversalTime().DateTime,
                ReadItunesImageHref(item) ?? feedImage,
                ParseDuration(ReadItunesDuration(item))));
        }

        return episodes;
    }

    public static int? ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        if (!value.Contains(':'))
        {
            return int.TryParse(value, out var seconds) && seconds >= 0 ? seconds : null;
        }

        var parts = value.Split(':');

        if (parts.Length == 3 &&
            int.TryParse(parts[0], out var hours) &&
            int.TryParse(parts[1], out var minutes) &&
            int.TryParse(parts[2], out var itemSeconds))
        {
            return hours * 3600 + minutes * 60 + itemSeconds;
        }

        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var plainMinutes) &&
            int.TryParse(parts[1], out var plainSeconds))
        {
            return plainMinutes * 60 + plainSeconds;
        }

        return null;
    }

    private static string? ReadItunesImageHref(SyndicationItem item)
    {
        var extension = item.ElementExtensions.FirstOrDefault(
            element => element.OuterName == "image" &&
                       element.OuterNamespace == "http://www.itunes.com/dtds/podcast-1.0.dtd");

        if (extension is null)
        {
            return null;
        }

        var element = extension.GetObject<XElement>();
        return element?.Attribute("href")?.Value;
    }

    private static string? ReadItunesDuration(SyndicationItem item)
    {
        var extension = item.ElementExtensions.FirstOrDefault(
            element => element.OuterName == "duration" &&
                       element.OuterNamespace == "http://www.itunes.com/dtds/podcast-1.0.dtd");

        return extension?.GetObject<string>();
    }
}
