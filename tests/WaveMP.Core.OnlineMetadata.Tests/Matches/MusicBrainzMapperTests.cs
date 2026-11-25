using AwesomeAssertions;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.OnlineMetadata.Matches;

namespace WaveMP.Core.OnlineMetadata.Tests.Matches;

[TestFixture]
public class MusicBrainzMapperTests
{
    private static readonly Guid _releaseId = Guid.Parse("7a0c3d43-1d7e-4b0e-9a69-2c6dd1b46a01");

    [Test]
    public void ForRecording_WithRelease_SuggestsTheValuesOfTheTrackOnTheRelease()
    {
        MusicBrainzRecording recording = new()
        {
            Id = Guid.NewGuid(),
            Title = "Song",
            Length = 201000,
            ArtistCredit = [new() { Name = "Band", JoinPhrase = " feat. " }, new() { Name = "Guest" }],
            FirstReleaseDate = "1998",
            Tags = [new() { Name = "pop", Count = 1 }, new() { Name = "alternative rock", Count = 3 }],
            Releases =
            [
                new MusicBrainzRelease
                {
                    Id = _releaseId,
                    Title = "Album",
                    Date = "1999-03-01",
                    Country = "GB",
                    TrackCount = 12,
                    ArtistCredit = [new() { Name = "Band" }],
                    Media =
                    [
                        new MusicBrainzMedium
                        {
                            Position = 2,
                            Format = "CD",
                            TrackOffset = 4,
                            Tracks =
                            [
                                new MusicBrainzTrack { Number = "5", Title = "Song (Album Version)", Length = 200500 },
                            ],
                        },
                    ],
                },
            ],
        };

        (OnlineRelease? release, OnlineTrackValues values, TimeSpan? duration) =
            MusicBrainzMapper.ForRecording(recording).Should().ContainSingle().Subject;

        release.Should().Be(new OnlineRelease(_releaseId, "1999-03-01", "GB", "CD", 12));
        values.Should().Be(new OnlineTrackValues(
            "Song (Album Version)",
            "Band feat. Guest",
            "Album",
            "Band",
            1999,
            5,
            2,
            "Alternative Rock"));
        duration.Should().Be(TimeSpan.FromMilliseconds(200500));
    }

    [Test]
    public void ForRecording_WithoutRelease_SuggestsNoAlbum()
    {
        MusicBrainzRecording recording = new()
        {
            Id = Guid.NewGuid(),
            Title = "Song",
            Length = 1000,
            ArtistCredit = [new() { Name = "Band" }],
            FirstReleaseDate = "2001-05",
        };

        (OnlineRelease? release, OnlineTrackValues values, _) =
            MusicBrainzMapper.ForRecording(recording).Should().ContainSingle().Subject;

        release.Should().BeNull();
        values.Album.Should().BeNull();
        values.Year.Should().Be(2001);
    }

    [Test]
    public void ForRecording_TrackNumberMissing_UsesTheOffset()
    {
        MusicBrainzRecording recording = new()
        {
            Id = Guid.NewGuid(),
            Title = "Song",
            Releases = [new MusicBrainzRelease { Id = _releaseId, Media = [new() { TrackOffset = 6 }] }],
        };

        MusicBrainzMapper.ForRecording(recording).Should().ContainSingle()
            .Which.Values.TrackNumber.Should().Be(7);
    }

    [Test]
    public void ForRelease_Values_AreTheAlbumTags()
    {
        MusicBrainzRelease release = new()
        {
            Id = _releaseId,
            Title = "Album",
            Date = "1999",
            ArtistCredit = [new() { Name = "Band" }],
            Tags = [new() { Name = "rock", Count = 4 }],
        };

        (OnlineRelease online, OnlineAlbumValues values) = MusicBrainzMapper.ForRelease(release);

        online.Id.Should().Be(_releaseId);
        values.Should().Be(new OnlineAlbumValues("Album", "Band", 1999, "Rock"));
    }

    [TestCase("1999-03-01", 1999)]
    [TestCase("1999", 1999)]
    [TestCase("19", null)]
    [TestCase("????", null)]
    [TestCase(null, null)]
    public void Year_ReadsTheFirstFourDigits(string? date, int? expected)
    {
        MusicBrainzMapper.Year(date).Should().Be(expected);
    }

    [Test]
    public void Genre_NoTagWithVotes_IsNull()
    {
        MusicBrainzMapper.Genre([new MusicBrainzTag { Name = "rock", Count = 0 }]).Should().BeNull();
    }
}
