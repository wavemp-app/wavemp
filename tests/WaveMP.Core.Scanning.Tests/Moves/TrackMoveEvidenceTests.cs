using AwesomeAssertions;
using WaveMP.Core.Metadata.Readers;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.Scanning.Moves;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Scanning.Tests.Moves;

[TestFixture]
public class TrackMoveEvidenceTests
{
    private static readonly DateTimeOffset _time = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan _duration = TimeSpan.FromSeconds(185.123456);

    private static readonly MediaTags _tags = new()
    {
        Title = "Song",
        Artist = "Band",
        AlbumArtist = "Band",
        Album = "Album",
        TrackNumber = 3,
        DiscNumber = 1,
        Year = 2020,
        Genre = "Pop",
    };

    [Test]
    public void Matches_SameDurationAndCoreTags_IsTrue()
    {
        TrackMoveEvidence.Matches(Stored(), Read()).Should().BeTrue();
    }

    [Test]
    public void Matches_DurationStoredAtThePrecisionOfTheDatabase_IsTrue()
    {
        MediaFile stored = Stored();
        long microseconds = _duration.Ticks / TimeSpan.TicksPerMicrosecond;
        stored.Duration = TimeSpan.FromTicks(microseconds * TimeSpan.TicksPerMicrosecond);

        TrackMoveEvidence.Matches(stored, Read() with { Duration = _duration + TimeSpan.FromTicks(5) })
            .Should().BeTrue();
    }

    [Test]
    public void Matches_OtherTagsDiffer_IsTrue()
    {
        TrackMoveEvidence.Matches(Stored(), Read(_tags with { Genre = "Rock", Composer = "Someone" }))
            .Should().BeTrue();
    }

    [Test]
    public void Matches_NoTagsOnBothFiles_IsTrue()
    {
        MediaFile stored = Stored();
        stored.Tags = new MediaFileTags();

        TrackMoveEvidence.Matches(stored, Read(MediaTags.Empty)).Should().BeTrue();
    }

    [Test]
    public void Matches_OtherDuration_IsFalse()
    {
        TrackMoveEvidence.Matches(Stored(), Read() with { Duration = _duration + TimeSpan.FromMilliseconds(1) })
            .Should().BeFalse();
    }

    [Test]
    public void Matches_UnknownDuration_IsFalse()
    {
        MediaFile stored = Stored();
        stored.Duration = null;

        TrackMoveEvidence.Matches(Stored(), Read() with { Duration = null }).Should().BeFalse();
        TrackMoveEvidence.Matches(stored, Read()).Should().BeFalse();
        TrackMoveEvidence.Matches(stored, Read() with { Duration = null }).Should().BeFalse();
    }

    [TestCase(MediaFileIssue.TagsUnreadable)]
    [TestCase(MediaFileIssue.ProbeFailed)]
    [TestCase(MediaFileIssue.Unreadable)]
    public void Matches_ReadWithAProblem_IsFalse(MediaFileIssue issue)
    {
        MediaFile stored = Stored();
        stored.Issue = issue;

        TrackMoveEvidence.Matches(Stored(), Read() with { Issue = issue }).Should().BeFalse();
        TrackMoveEvidence.Matches(stored, Read()).Should().BeFalse();
    }

    [TestCase("Title")]
    [TestCase("Artist")]
    [TestCase("AlbumArtist")]
    [TestCase("Album")]
    [TestCase("TrackNumber")]
    [TestCase("DiscNumber")]
    [TestCase("Year")]
    public void Matches_CoreTagDiffers_IsFalse(string tag)
    {
        MediaTags changed = tag switch
        {
            "Title" => _tags with { Title = "song" },
            "Artist" => _tags with { Artist = null },
            "AlbumArtist" => _tags with { AlbumArtist = "Various Artists" },
            "Album" => _tags with { Album = "Album (Deluxe)" },
            "TrackNumber" => _tags with { TrackNumber = 4 },
            "DiscNumber" => _tags with { DiscNumber = null },
            _ => _tags with { Year = 2021 },
        };

        TrackMoveEvidence.Matches(Stored(), Read(changed)).Should().BeFalse();
    }

    private static MediaFileMetadata Read(MediaTags? tags = null)
    {
        return new MediaFileMetadata { Tags = tags ?? _tags, Duration = _duration, Codec = "mp3" };
    }

    private static MediaFile Stored()
    {
        return new MediaFile
        {
            Id = Guid.NewGuid(),
            SourceId = Guid.NewGuid(),
            RelativePath = "Old/Song.mp3",
            DirectoryPath = "Old",
            FileName = "Song.mp3",
            BaseName = "Song",
            NormalizedBaseName = "SONG",
            Extension = "mp3",
            Kind = MediaFileKind.Audio,
            Size = 1000,
            ModifiedAt = _time,
            IsPrimary = true,
            IsLossless = false,
            HasEmbeddedPicture = false,
            IsWritable = true,
            Duration = _duration,
            Tags = new MediaFileTags
            {
                Title = _tags.Title,
                Artist = _tags.Artist,
                AlbumArtist = _tags.AlbumArtist,
                Album = _tags.Album,
                TrackNumber = _tags.TrackNumber,
                DiscNumber = _tags.DiscNumber,
                Year = _tags.Year,
                Genre = _tags.Genre,
            },
            MetadataVersion = 1,
            FirstSeenAt = _time,
            ScannedAt = _time,
        };
    }
}
