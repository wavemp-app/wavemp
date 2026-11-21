using AwesomeAssertions;
using WaveMP.Core.Lyrics.Documents;
using WaveMP.Core.Lyrics.Lookups;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class LyricsMappingExtensionsTests
{
    [Test]
    public void ToDto_WordSynchronizedLyrics_GivesTheTimesInMilliseconds()
    {
        TrackLyrics lyrics = new(
            new LyricsDocument(
                LyricsType.WordSynchronized,
                [
                    new LyricsLine(
                        TimeSpan.FromMilliseconds(1000),
                        TimeSpan.FromMilliseconds(2500),
                        "Hello world",
                        [
                            new LyricsWord(TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1400), "Hello "),
                            new LyricsWord(TimeSpan.FromMilliseconds(1400), null, "world"),
                        ]),
                ]),
            LyricsSource.File);

        LyricsDto dto = lyrics.ToDto();

        dto.Should().BeEquivalentTo(new LyricsDto
        {
            Type = LyricsTypeDto.WordSynchronized,
            Source = LyricsSourceDto.File,
            Lines =
            [
                new LyricsLineDto
                {
                    StartMilliseconds = 1000,
                    EndMilliseconds = 2500,
                    Text = "Hello world",
                    Words =
                    [
                        new LyricsWordDto { StartMilliseconds = 1000, EndMilliseconds = 1400, Text = "Hello " },
                        new LyricsWordDto { StartMilliseconds = 1400, EndMilliseconds = null, Text = "world" },
                    ],
                },
            ],
        });
    }

    [Test]
    public void ToDto_StaticLine_HasNoTimes()
    {
        LyricsLineDto dto = new LyricsLine(null, null, "Line", []).ToDto();

        dto.StartMilliseconds.Should().BeNull();
        dto.EndMilliseconds.Should().BeNull();
        dto.Words.Should().BeEmpty();
    }

    [Test]
    public void ToDto_EveryTypeAndSource_HasItsOwnValue()
    {
        Enum.GetValues<LyricsType>().Select(type => type.ToDto().ToString())
            .Should().Equal(Enum.GetValues<LyricsType>().Select(type => type.ToString()));
        Enum.GetValues<LyricsSource>().Select(source => source.ToDto().ToString())
            .Should().Equal(Enum.GetValues<LyricsSource>().Select(source => source.ToString()));
        Enum.GetValues<LyricsDownloadStatus>().Select(status => status.ToDto().ToString())
            .Should().Equal(Enum.GetValues<LyricsDownloadStatus>().Select(status => status.ToString()));
    }

    [Test]
    public void ToDto_Lookups_TellWhichCanBeForgotten()
    {
        DateTimeOffset checkedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

        new LyricsDownloadRow(LyricsDownloadStatus.Instrumental, null, checkedAt).ToDto().Should().BeEquivalentTo(
            new LyricsLookupDto
            {
                Status = LyricsLookupStatusDto.Instrumental,
                CheckedAt = checkedAt,
                IsForgettable = true,
            });
        new LyricsDownloadRow(LyricsDownloadStatus.SavedToDatabase, "Line", checkedAt).ToDto().IsForgettable
            .Should().BeFalse();
    }

    [Test]
    public void ToDto_LookupSummary_GivesTheWaitInWholeMinutes()
    {
        new LyricsLookupSummary(3, 1, TimeSpan.FromDays(30)).ToDto().Should().BeEquivalentTo(
            new LyricsLookupSummaryDto { NotFoundCount = 3, InstrumentalCount = 1, LookUpAgainAfterMinutes = 43_200 });
        new LyricsLookupSummary(0, 0, TimeSpan.FromSeconds(90)).ToDto().LookUpAgainAfterMinutes.Should().Be(2);
        new LyricsLookupSummary(0, 0, TimeSpan.MaxValue).ToDto().LookUpAgainAfterMinutes.Should().Be(int.MaxValue);
        new LyricsLookupSummary(0, 0, null).ToDto().LookUpAgainAfterMinutes.Should().BeNull();
    }
}
