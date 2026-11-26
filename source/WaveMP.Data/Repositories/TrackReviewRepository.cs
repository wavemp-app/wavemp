using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// The tags of a track are those of its primary audio file, for a track with a video only those of its primary video.
/// </summary>
internal sealed class TrackReviewRepository(WaveMpDbContext dbContext) : ITrackReviewRepository
{
    public async Task<Page<TrackReviewRow>> ListAsync(
        TrackReviewListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Track> tracks = Filter(dbContext.Tracks.AsNoTracking(), query);
        IOrderedQueryable<Track> sorted = query.Sort switch
        {
            TrackReviewSortField.Artist => ThenByAlbum(tracks
                .OrderBy(track => track.Artist!.NormalizedName, query.Direction)
                .ThenBy(track => track.Album!.NormalizedTitle)),
            TrackReviewSortField.Album => ThenByAlbum(tracks
                .OrderBy(track => track.Album!.NormalizedTitle, query.Direction)),
            TrackReviewSortField.Title => tracks
                .OrderBy(track => track.Title, query.Direction)
                .ThenBy(track => track.DirectoryPath)
                .ThenBy(track => track.BaseName),
            // Tracks of one folder are found at the same time, so albums stay together.
            _ => ThenByAlbum(tracks.OrderBy(track => track.FirstSeenAt, query.Direction)),
        };

        var page = await sorted
            .ThenBy(track => track.Id)
            .Select(track => new
            {
                track.Id,
                track.Title,
                track.ArtistId,
                ArtistName = track.Artist != null ? track.Artist.Name : null,
                track.AlbumId,
                AlbumTitle = track.Album != null ? track.Album.Title : null,
                AlbumArtistId = track.Album != null ? track.Album.AlbumArtistId : null,
                AlbumArtistName = track.Album != null && track.Album.AlbumArtist != null
                    ? track.Album.AlbumArtist.Name
                    : null,
                AlbumIsCompilation = track.Album != null && track.Album.IsCompilation,
                track.DirectoryPath,
                track.BaseName,
                track.HasAudio,
                track.HasVideo,
                HasCover = track.Covers.Any()
                    || (track.AlbumId != null
                        && dbContext.Covers.Any(cover =>
                            cover.AlbumId == track.AlbumId
                            || (cover.Source == CoverSource.Embedded
                                && cover.Track != null
                                && cover.Track.AlbumId == track.AlbumId)))
                    || dbContext.OnlineCovers.Any(cover =>
                        cover.TrackId == track.Id
                        || (track.AlbumId != null
                            && cover.AlbumId == track.AlbumId)),
                track.FirstSeenAt,
                Tags = track.Files
                    .Where(file => file.IsPrimary
                        && ((track.HasAudio && file.Kind == MediaFileKind.Audio)
                            || (!track.HasAudio && file.Kind == MediaFileKind.Video)))
                    .Select(file => new TagPresence(
                        file.Tags.Title != null,
                        file.Tags.Artist != null,
                        file.Tags.Album != null,
                        file.Tags.Year != null,
                        file.Tags.TrackNumber != null,
                        file.Tags.Genre != null))
                    .FirstOrDefault(),
                track.ReviewedAt,
                track.ReviewedByUserId,
                ReviewedByName = track.ReviewedBy != null ? track.ReviewedBy.Name : null,
            })
            .ToPageAsync(query.Offset, query.Limit, cancellationToken);

        return new Page<TrackReviewRow>(
            [.. page.Items.Select(item => new TrackReviewRow(
                item.Id,
                item.Title,
                item.ArtistId,
                item.ArtistName,
                item.AlbumId,
                item.AlbumTitle,
                item.AlbumArtistId,
                item.AlbumArtistName,
                item.AlbumIsCompilation,
                item.DirectoryPath,
                item.BaseName,
                item.HasAudio,
                item.HasVideo,
                item.HasCover,
                item.FirstSeenAt,
                MissingOf(item.Tags),
                new TrackReviewStateRow(item.Id, item.ReviewedAt, item.ReviewedByUserId, item.ReviewedByName)))],
            page.Offset,
            page.Limit,
            page.Total);
    }

    public async Task<TrackReviewCounts> CountAsync(CancellationToken cancellationToken = default)
    {
        int unreviewed = await dbContext.Tracks.CountAsync(track => track.ReviewedAt == null, cancellationToken);
        int reviewed = await dbContext.Tracks.CountAsync(track => track.ReviewedAt != null, cancellationToken);
        return new TrackReviewCounts(unreviewed, reviewed);
    }

    public Task<TrackReviewStateRow?> FindAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        return dbContext.Tracks.AsNoTracking()
            .Where(track => track.Id == trackId)
            .Select(track => new TrackReviewStateRow(
                track.Id,
                track.ReviewedAt,
                track.ReviewedByUserId,
                track.ReviewedBy != null ? track.ReviewedBy.Name : null))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> MarkReviewedAsync(
        IReadOnlyCollection<Guid> trackIds,
        Guid userId,
        DateTimeOffset reviewedAt,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset? time = reviewedAt;
        return dbContext.Tracks
            .Where(track => trackIds.Contains(track.Id) && track.ReviewedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(track => track.ReviewedAt, time)
                    .SetProperty(
                        track => track.ReviewedByUserId,
                        track => dbContext.Users
                            .Where(user => user.Id == userId)
                            .Select(user => (Guid?)user.Id)
                            .FirstOrDefault()),
                cancellationToken);
    }

    public Task<int> MarkUnreviewedAsync(
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Tracks
            .Where(track => trackIds.Contains(track.Id) && track.ReviewedAt != null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(track => track.ReviewedAt, (DateTimeOffset?)null)
                    .SetProperty(track => track.ReviewedByUserId, (Guid?)null),
                cancellationToken);
    }

    /// <summary>
    /// The checked fields the tags lack, in the order of <see cref="MetadataTag"/>; all of them without tags.
    /// </summary>
    internal static IReadOnlyList<MetadataTag> MissingOf(TagPresence? tags)
    {
        if (tags is null)
        {
            return Enum.GetValues<MetadataTag>();
        }

        List<MetadataTag> missing = [];
        foreach ((MetadataTag tag, bool present) in (ReadOnlySpan<(MetadataTag, bool)>)
            [
                (MetadataTag.Title, tags.Title),
                (MetadataTag.Artist, tags.Artist),
                (MetadataTag.Album, tags.Album),
                (MetadataTag.Year, tags.Year),
                (MetadataTag.TrackNumber, tags.TrackNumber),
                (MetadataTag.Genre, tags.Genre),
            ])
        {
            if (!present)
            {
                missing.Add(tag);
            }
        }

        return missing;
    }

    private static IQueryable<Track> Filter(IQueryable<Track> tracks, TrackReviewListQuery query)
    {
        tracks = query.Status == TrackReviewStatus.Reviewed
            ? tracks.Where(track => track.ReviewedAt != null)
            : tracks.Where(track => track.ReviewedAt == null);

        if (query.Search is { Length: > 0 } search)
        {
            string pattern = LikePatterns.Contains(search);
            tracks = tracks.Where(track =>
                EF.Functions.ILike(track.Title, pattern, LikePatterns.EscapeCharacter)
                || (track.Artist != null
                    && EF.Functions.ILike(track.Artist.Name, pattern, LikePatterns.EscapeCharacter))
                || (track.Album != null
                    && EF.Functions.ILike(track.Album.Title, pattern, LikePatterns.EscapeCharacter))
                || EF.Functions.ILike(track.DirectoryPath, pattern, LikePatterns.EscapeCharacter)
                || EF.Functions.ILike(track.BaseName, pattern, LikePatterns.EscapeCharacter));
        }

        if (query.MissingTags is { } missing)
        {
            // Complete: the file of the tags has every checked field. A track without such a file lacks all of them.
            tracks = tracks.Where(track => track.Files.Any(file =>
                file.IsPrimary
                && ((track.HasAudio && file.Kind == MediaFileKind.Audio)
                    || (!track.HasAudio && file.Kind == MediaFileKind.Video))
                && file.Tags.Title != null
                && file.Tags.Artist != null
                && file.Tags.Album != null
                && file.Tags.Year != null
                && file.Tags.TrackNumber != null
                && file.Tags.Genre != null) != missing);
        }

        return tracks;
    }

    private static IOrderedQueryable<Track> ThenByAlbum(IOrderedQueryable<Track> sorted)
    {
        return sorted
            .ThenBy(track => track.AlbumId)
            .ThenBy(track => track.DiscNumber)
            .ThenBy(track => track.TrackNumber)
            .ThenBy(track => track.Title);
    }

    /// <summary>
    /// Which checked fields the tags of a file contain.
    /// </summary>
    internal sealed record TagPresence(
        bool Title,
        bool Artist,
        bool Album,
        bool Year,
        bool TrackNumber,
        bool Genre);
}
