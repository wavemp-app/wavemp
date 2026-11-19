using AwesomeAssertions;
using WaveMP.Core.Library.Checks;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class LibraryIssueMappingExtensionsTests
{
    private static readonly DateTimeOffset _detectedAt = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

    [Test]
    public void ToDto_Duplicate_MapsBothTracksWithSourceAndDuration()
    {
        LibraryIssueTrackRow track = Track("Song", "Band/Album");
        LibraryIssueTrackRow related = Track("Song", "Band/Best Of");
        LibraryIssueRow row = new(
            Guid.NewGuid(),
            LibraryIssueType.PossibleDuplicate,
            _detectedAt,
            track,
            null,
            null,
            [],
            related);

        LibraryIssueDto dto = row.ToDto();

        dto.Should().BeEquivalentTo(new
        {
            row.Id,
            Type = LibraryIssueTypeDto.PossibleDuplicate,
            Severity = LibraryIssueSeverityDto.Problem,
            DetectedAt = _detectedAt,
            Track = new
            {
                track.Id,
                Title = "Song",
                Artist = new { Id = track.ArtistId, Name = "Band" },
                Album = new { Id = track.AlbumId, Title = "Album", Artist = new { Name = "Band" } },
                track.SourceId,
                SourceName = "Music",
                DirectoryPath = "Band/Album",
                BaseName = "01 Song",
                DurationMilliseconds = 201_500L,
                HasAudio = true,
                HasVideo = false,
            },
            RelatedTrack = new { related.Id, DirectoryPath = "Band/Best Of" },
            MissingTags = Array.Empty<MetadataTagDto>(),
        });
        dto.File.Should().BeNull();
        dto.FileIssue.Should().BeNull();
    }

    [Test]
    public void ToDto_UnreadableFileWithoutTrack_MapsTheFileAndTheCodeOfItsIssue()
    {
        var fileId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        LibraryIssueRow row = new(
            Guid.NewGuid(),
            LibraryIssueType.UnreadableFile,
            _detectedAt,
            null,
            new LibraryIssueFileRow(fileId, sourceId, "Music", "Band/broken.jpg", MediaFileKind.Image),
            MediaFileIssue.Unreadable,
            [],
            null);

        LibraryIssueDto dto = row.ToDto();

        dto.Track.Should().BeNull();
        dto.File.Should().BeEquivalentTo(new LibraryIssueFileDto
        {
            Id = fileId,
            SourceId = sourceId,
            SourceName = "Music",
            RelativePath = "Band/broken.jpg",
            Kind = MediaFileKindDto.Image,
        });
        dto.FileIssue.Should().Be(ScanIssueCodeDto.FileUnreadable);
    }

    [Test]
    public void ToDto_MissingTagsAndHints_MapTheTagsAndTheSeverity()
    {
        LibraryIssueRow tags = new(
            Guid.NewGuid(),
            LibraryIssueType.MissingTags,
            _detectedAt,
            Track("Song", string.Empty),
            null,
            null,
            [MetadataTag.Artist, MetadataTag.Album],
            null);
        LibraryIssueRow hint = tags with { Type = LibraryIssueType.AudioWithoutVideo, MissingTags = [] };

        tags.ToDto().MissingTags.Should().Equal(MetadataTagDto.Artist, MetadataTagDto.Album);
        hint.ToDto().Severity.Should().Be(LibraryIssueSeverityDto.Hint);
    }

    [Test]
    public void ToDto_Summary_SumsProblemsAndHints()
    {
        LibraryIssueSummary summary = new(
            [
                new LibraryIssueCount(LibraryIssueType.MissingCover, LibraryIssueSeverity.Problem, 3),
                new LibraryIssueCount(LibraryIssueType.UnknownAlbum, LibraryIssueSeverity.Problem, 2),
                new LibraryIssueCount(LibraryIssueType.AudioWithoutVideo, LibraryIssueSeverity.Hint, 7),
            ],
            _detectedAt,
            12);

        LibraryIssueSummaryDto dto = summary.ToDto();

        dto.ProblemCount.Should().Be(5);
        dto.HintCount.Should().Be(7);
        dto.CheckedAt.Should().Be(_detectedAt);
        dto.TrackCount.Should().Be(12);
        dto.Counts.Select(count => (count.Type, count.Severity, count.Count)).Should().Equal(
            (LibraryIssueTypeDto.MissingCover, LibraryIssueSeverityDto.Problem, 3),
            (LibraryIssueTypeDto.UnknownAlbum, LibraryIssueSeverityDto.Problem, 2),
            (LibraryIssueTypeDto.AudioWithoutVideo, LibraryIssueSeverityDto.Hint, 7));
    }

    [Test]
    public void Enums_EveryValue_MapsBothWays()
    {
        foreach (LibraryIssueType type in Enum.GetValues<LibraryIssueType>())
        {
            type.ToDto().ToEntity().Should().Be(type);
        }

        foreach (LibraryIssueSeverity severity in Enum.GetValues<LibraryIssueSeverity>())
        {
            severity.ToDto().ToEntity().Should().Be(severity);
        }

        Enum.GetValues<LibraryIssueSortDto>().Select(sort => sort.ToEntity())
            .Should().BeEquivalentTo(Enum.GetValues<LibraryIssueSortField>());
    }

    private static LibraryIssueTrackRow Track(string title, string directory)
    {
        var artistId = Guid.NewGuid();
        return new LibraryIssueTrackRow(
            Guid.NewGuid(),
            title,
            artistId,
            "Band",
            Guid.NewGuid(),
            "Album",
            artistId,
            "Band",
            false,
            Guid.NewGuid(),
            "Music",
            directory,
            "01 " + title,
            TimeSpan.FromMilliseconds(201_500),
            true,
            false);
    }
}
