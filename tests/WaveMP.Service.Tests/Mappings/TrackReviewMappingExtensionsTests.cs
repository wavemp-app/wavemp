using AwesomeAssertions;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class TrackReviewMappingExtensionsTests
{
    private static readonly DateTimeOffset _reviewedAt = new(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);

    [Test]
    public void ToDto_ReviewedRow_MapsTrackTagsAndReviewer()
    {
        var trackId = Guid.NewGuid();
        var albumId = Guid.NewGuid();
        var artistId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        TrackReviewRow row = new(
            trackId,
            "Song",
            artistId,
            "Band",
            albumId,
            "Album",
            artistId,
            "Band",
            false,
            "Band/Album",
            "01 Song",
            true,
            false,
            true,
            _reviewedAt.AddDays(-1),
            [MetadataTag.Year, MetadataTag.Genre],
            new TrackReviewStateRow(trackId, _reviewedAt, userId, "Admin"));

        TrackReviewDto dto = row.ToDto();

        dto.Should().BeEquivalentTo(new
        {
            TrackId = trackId,
            Title = "Song",
            Artist = new { Id = artistId, Name = "Band" },
            Album = new { Id = albumId, Title = "Album", IsCompilation = false },
            DirectoryPath = "Band/Album",
            BaseName = "01 Song",
            HasAudio = true,
            HasVideo = false,
            HasCover = true,
            FirstSeenAt = _reviewedAt.AddDays(-1),
            MissingTags = new[] { MetadataTagDto.Year, MetadataTagDto.Genre },
            Status = TrackReviewStatusDto.Reviewed,
            ReviewedAt = _reviewedAt,
            ReviewedBy = new { Id = userId, Name = "Admin" },
        });
    }

    [Test]
    public void ToDto_UnreviewedState_HasNoReviewer()
    {
        var trackId = Guid.NewGuid();

        TrackReviewStateDto dto = new TrackReviewStateRow(trackId, null, null, null).ToDto();

        dto.Should().BeEquivalentTo(new TrackReviewStateDto
        {
            TrackId = trackId,
            Status = TrackReviewStatusDto.Unreviewed,
        });
    }

    [Test]
    public void ToDto_ReviewerDeleted_KeepsTheTimeWithoutReviewer()
    {
        TrackReviewStateDto dto = new TrackReviewStateRow(Guid.NewGuid(), _reviewedAt, null, null).ToDto();

        dto.Status.Should().Be(TrackReviewStatusDto.Reviewed);
        dto.ReviewedAt.Should().Be(_reviewedAt);
        dto.ReviewedBy.Should().BeNull();
    }

    [Test]
    public void ToDto_Counts_MapsBothCounts()
    {
        TrackReviewSummaryDto dto = new TrackReviewCounts(4, 6).ToDto();

        dto.Should().BeEquivalentTo(new TrackReviewSummaryDto { UnreviewedCount = 4, ReviewedCount = 6 });
    }

    [Test]
    public void Enums_EveryValue_IsMapped()
    {
        Enum.GetValues<MetadataTag>().Select(tag => tag.ToDto().ToString())
            .Should().Equal(Enum.GetNames<MetadataTag>());
        Enum.GetValues<TrackReviewStatusDto>().Select(status => status.ToEntity().ToString())
            .Should().Equal(Enum.GetNames<TrackReviewStatusDto>());
        Enum.GetValues<TrackReviewSortDto>().Select(sort => sort.ToEntity().ToString())
            .Should().Equal(Enum.GetNames<TrackReviewSortDto>());
    }
}
