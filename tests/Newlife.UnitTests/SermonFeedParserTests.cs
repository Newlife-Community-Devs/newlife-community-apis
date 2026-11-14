using Newlife.Infrastructure.Sermons;
using Shouldly;

namespace Newlife.UnitTests;

public sealed class SermonFeedParserTests
{
    private const string Fixture = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0" xmlns:itunes="http://www.itunes.com/dtds/podcast-1.0.dtd">
          <channel>
            <title>The Newlife Podcast</title>
            <link>https://example.test</link>
            <description>Weekly sermons</description>
            <itunes:image href="https://cdn.test/channel.jpg"/>
            <image><url>https://cdn.test/channel.jpg</url><title>Channel</title><link>https://example.test</link></image>
            <item>
              <title>  Faith That Moves Mountains  </title>
              <guid isPermaLink="false">episode-one</guid>
              <pubDate>Mon, 09 Nov 2026 06:00:00 GMT</pubDate>
              <description><![CDATA[<p>A <b>sermon</b> about faith.</p>]]></description>
              <enclosure url="https://cdn.test/one.mp3" length="123" type="audio/mpeg"/>
              <itunes:image href="https://cdn.test/one.jpg"/>
              <itunes:duration>1:02:03</itunes:duration>
            </item>
            <item>
              <title>Short and Sweet</title>
              <guid isPermaLink="false">episode-two</guid>
              <pubDate>Mon, 02 Nov 2026 06:00:00 GMT</pubDate>
              <description>Plain text summary</description>
              <enclosure url="https://cdn.test/two.mp3" length="456" type="audio/mpeg"/>
              <itunes:duration>45:30</itunes:duration>
            </item>
            <item>
              <title>No Enclosure</title>
              <guid isPermaLink="false">episode-three</guid>
              <pubDate>Mon, 26 Oct 2026 06:00:00 GMT</pubDate>
              <description>Skip me</description>
            </item>
            <item>
              <title>No Guid</title>
              <pubDate>Mon, 19 Oct 2026 06:00:00 GMT</pubDate>
              <enclosure url="https://cdn.test/four.mp3" length="789" type="audio/mpeg"/>
            </item>
          </channel>
        </rss>
        """;

    [Fact]
    public void Parse_extracts_items_and_skips_incomplete_entries()
    {
        var episodes = SermonFeedParser.Parse(Fixture);

        episodes.Count.ShouldBe(2);
        episodes[0].Guid.ShouldBe("episode-one");
        episodes[0].Title.ShouldBe("Faith That Moves Mountains");
        episodes[0].AudioUrl.ShouldBe("https://cdn.test/one.mp3");
        episodes[0].DurationSeconds.ShouldBe(3723);
        episodes[0].ArtworkUrl.ShouldBe("https://cdn.test/one.jpg");
        episodes[0].PublishedAt.ShouldBe(new DateTime(2026, 11, 9, 6, 0, 0, DateTimeKind.Utc));

        episodes[1].Guid.ShouldBe("episode-two");
        episodes[1].DurationSeconds.ShouldBe(2730);
        episodes[1].ArtworkUrl.ShouldBe("https://cdn.test/channel.jpg");
    }

    [Theory]
    [InlineData("1:02:03", 3723)]
    [InlineData("45:30", 2730)]
    [InlineData("3600", 3600)]
    [InlineData("0", 0)]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("abc", null)]
    [InlineData("1:2:3:4", null)]
    [InlineData("1:xx:03", null)]
    public void ParseDuration_handles_all_documented_formats(string? value, int? expected)
        => SermonFeedParser.ParseDuration(value).ShouldBe(expected);
}
