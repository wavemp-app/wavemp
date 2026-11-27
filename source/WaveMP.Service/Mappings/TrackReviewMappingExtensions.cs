using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class TrackReviewMappingExtensions
{
    public static TrackReviewDto ToDto(this TrackReviewRow row)
    {
        return new TrackReviewDto
        {
            TrackId = row.TrackId,
            Title = row.Title,
            Artist = LibraryMappingExtensions.ToReference(row.ArtistId, row.ArtistName),
            Album = row.AlbumId is { } albumId && row.AlbumTitle is { } albumTitle
                ? new AlbumReferenceDto
                {
                    Id = albumId,
                    Title = albumTitle,
                    Artist = LibraryMappingExtensions.ToReference(row.AlbumArtistId, row.AlbumArtistName),
                    Artists = LibraryMappingExtensions.ToCredits(row.AlbumArtistId, row.AlbumArtistName),
                    IsCompilation = row.AlbumIsCompilation,
                }
                : null,
            DirectoryPath = row.DirectoryPath,
            BaseName = row.BaseName,
            HasAudio = row.HasAudio,
            HasVideo = row.HasVideo,
            HasCover = row.HasCover,
            FirstSeenAt = row.FirstSeenAt,
            MissingTags = [.. row.MissingTags.Select(tag => tag.ToDto())],
            Status = StatusOf(row.Review),
            ReviewedAt = row.Review.ReviewedAt,
            ReviewedBy = ReviewerOf(row.Review),
        };
    }

    public static TrackReviewStateDto ToDto(this TrackReviewStateRow row)
    {
        return new TrackReviewStateDto
        {
            TrackId = row.TrackId,
            Status = StatusOf(row),
            ReviewedAt = row.ReviewedAt,
            ReviewedBy = ReviewerOf(row),
        };
    }

    public static TrackReviewSummaryDto ToDto(this TrackReviewCounts counts)
    {
        return new TrackReviewSummaryDto
        {
            UnreviewedCount = counts.Unreviewed,
            ReviewedCount = counts.Reviewed,
        };
    }

    public static MetadataTagDto ToDto(this MetadataTag tag)
    {
        return tag switch
        {
            MetadataTag.Title => MetadataTagDto.Title,
            MetadataTag.Artist => MetadataTagDto.Artist,
            MetadataTag.Album => MetadataTagDto.Album,
            MetadataTag.Year => MetadataTagDto.Year,
            MetadataTag.TrackNumber => MetadataTagDto.TrackNumber,
            MetadataTag.Genre => MetadataTagDto.Genre,
            _ => throw new ArgumentOutOfRangeException(nameof(tag), tag, "Unknown tag."),
        };
    }

    public static TrackReviewStatus ToEntity(this TrackReviewStatusDto status)
    {
        return status switch
        {
            TrackReviewStatusDto.Unreviewed => TrackReviewStatus.Unreviewed,
            TrackReviewStatusDto.Reviewed => TrackReviewStatus.Reviewed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown status."),
        };
    }

    public static TrackReviewSortField ToEntity(this TrackReviewSortDto sort)
    {
        return sort switch
        {
            TrackReviewSortDto.FirstSeenAt => TrackReviewSortField.FirstSeenAt,
            TrackReviewSortDto.Artist => TrackReviewSortField.Artist,
            TrackReviewSortDto.Album => TrackReviewSortField.Album,
            TrackReviewSortDto.Title => TrackReviewSortField.Title,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unknown sort key."),
        };
    }

    private static TrackReviewStatusDto StatusOf(TrackReviewStateRow row)
    {
        return row.IsReviewed ? TrackReviewStatusDto.Reviewed : TrackReviewStatusDto.Unreviewed;
    }

    private static ProfileReferenceDto? ReviewerOf(TrackReviewStateRow row)
    {
        return row.ReviewedByUserId is { } id && row.ReviewedByName is { } name
            ? new ProfileReferenceDto { Id = id, Name = name }
            : null;
    }
}
