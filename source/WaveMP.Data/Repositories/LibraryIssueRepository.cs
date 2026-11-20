using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// The tags of a track are those of its primary audio file, for a track with a video only those of its primary video;
/// a track has a cover like in the rest of the library: its own or its album's, also an online cover of
/// either.
/// </summary>
internal sealed class LibraryIssueRepository(WaveMpDbContext dbContext) : ILibraryIssueRepository
{
    /// <summary>
    /// Ids sent to the database at once when problems are removed or checked.
    /// </summary>
    internal const int BatchSize = 1000;

    /// <summary>
    /// The tags the check looks at, in this order.
    /// </summary>
    private static readonly MetadataTag[] _checkedTags = [MetadataTag.Title, MetadataTag.Artist, MetadataTag.Album];

    /// <summary>
    /// Media source, folder and file name of the track of a problem, or of its file if it has no track.
    /// </summary>
    private static readonly Expression<Func<LibraryIssue, string>> _sourceNameOf = issue =>
        issue.Track != null ? issue.Track.Source!.Name : issue.MediaFile!.Source!.Name;

    private static readonly Expression<Func<LibraryIssue, string>> _directoryOf = issue =>
        issue.Track != null ? issue.Track.DirectoryPath : issue.MediaFile!.DirectoryPath;

    private static readonly Expression<Func<LibraryIssue, string>> _nameOf = issue =>
        issue.Track != null ? issue.Track.BaseName : issue.MediaFile!.FileName;

    public async Task<IReadOnlyList<LibraryCheckTrackRow>> ListTracksAsync(
        CancellationToken cancellationToken = default)
    {
        var tracks = await dbContext.Tracks.AsNoTracking()
            .OrderBy(track => track.Source!.Name)
            .ThenBy(track => track.DirectoryPath)
            .ThenBy(track => track.BaseName)
            .ThenBy(track => track.Id)
            .Select(track => new
            {
                track.Id,
                track.Title,
                track.ArtistId,
                track.AlbumId,
                track.Duration,
                track.HasAudio,
                track.HasVideo,
            })
            .ToListAsync(cancellationToken);

        // The primary audio and video files with the tags the check looks at, read at once instead of per track.
        var files = await dbContext.MediaFiles.AsNoTracking()
            .Where(file => file.TrackId != null
                && file.IsPrimary
                && (file.Kind == MediaFileKind.Audio || file.Kind == MediaFileKind.Video))
            .Select(file => new
            {
                TrackId = file.TrackId!.Value,
                file.Kind,
                Title = file.Tags.Title != null,
                Artist = file.Tags.Artist != null,
                Album = file.Tags.Album != null,
            })
            .ToListAsync(cancellationToken);
        Dictionary<(Guid, MediaFileKind), (bool Title, bool Artist, bool Album)> tags = [];
        foreach (var file in files)
        {
            tags[(file.TrackId, file.Kind)] = (file.Title, file.Artist, file.Album);
        }

        HashSet<Guid> tracksWithCover = await TracksWithOwnCoverAsync(cancellationToken);
        HashSet<Guid> albumsWithCover = await AlbumsWithCoverAsync(cancellationToken);

        return
        [
            .. tracks.Select(track => new LibraryCheckTrackRow(
                track.Id,
                track.Title,
                track.ArtistId,
                track.AlbumId,
                track.Duration,
                track.HasAudio,
                track.HasVideo,
                tracksWithCover.Contains(track.Id)
                    || (track.AlbumId is { } albumId && albumsWithCover.Contains(albumId)),
                MissingOf(tags.TryGetValue(
                    (track.Id, track.HasAudio ? MediaFileKind.Audio : MediaFileKind.Video),
                    out (bool Title, bool Artist, bool Album) presence)
                    ? presence
                    : null))),
        ];
    }

    public async Task<IReadOnlyList<LibraryCheckFileRow>> ListUnreadableFilesAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.MediaFiles.AsNoTracking()
            .Where(file => file.Issue != null)
            .OrderBy(file => file.Source!.Name)
            .ThenBy(file => file.RelativePath)
            .ThenBy(file => file.Id)
            .Select(file => new LibraryCheckFileRow(file.Id, file.TrackId, file.Issue!.Value))
            .ToListAsync(cancellationToken);
    }

    public Task<LibraryIssueChanges> ReplaceAsync(
        IReadOnlyCollection<DetectedLibraryIssue> issues,
        int trackCount,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken = default)
    {
        IExecutionStrategy strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async token =>
            {
                dbContext.ChangeTracker.Clear();
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(token);
                LibraryIssueChanges changes = await ReplaceInTransactionAsync(issues, trackCount, checkedAt, token);
                await transaction.CommitAsync(token);
                dbContext.ChangeTracker.Clear();
                return changes;
            },
            cancellationToken);
    }

    public async Task<Page<LibraryIssueRow>> ListAsync(
        LibraryIssueListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<LibraryIssue> issues = Filter(dbContext.LibraryIssues.AsNoTracking(), query);
        IOrderedQueryable<LibraryIssue> sorted = query.Sort switch
        {
            LibraryIssueSortField.Artist => ThenByPath(issues
                .OrderBy(issue => issue.Track!.Artist!.NormalizedName, query.Direction)
                .ThenBy(issue => issue.Track!.Album!.NormalizedTitle, query.Direction)
                .ThenBy(issue => issue.Track!.DiscNumber)
                .ThenBy(issue => issue.Track!.TrackNumber)
                .ThenBy(issue => issue.Track!.Title)),
            LibraryIssueSortField.Title => ThenByPath(issues
                .OrderBy(issue => issue.Track!.Title, query.Direction)),
            LibraryIssueSortField.DetectedAt => ThenByPath(issues
                .OrderBy(issue => issue.DetectedAt, query.Direction)),
            _ => issues
                .OrderBy(_sourceNameOf, query.Direction)
                .ThenBy(_directoryOf, query.Direction)
                .ThenBy(_nameOf, query.Direction),
        };

        Page<IssueData> page = await sorted
            .ThenBy(issue => issue.Type)
            .ThenBy(issue => issue.Id)
            .Select(issue => new IssueData(
                issue.Id,
                issue.Type,
                issue.DetectedAt,
                issue.Track == null
                    ? null
                    : new LibraryIssueTrackRow(
                        issue.Track.Id,
                        issue.Track.Title,
                        issue.Track.ArtistId,
                        issue.Track.Artist != null ? issue.Track.Artist.Name : null,
                        issue.Track.AlbumId,
                        issue.Track.Album != null ? issue.Track.Album.Title : null,
                        issue.Track.Album != null ? issue.Track.Album.AlbumArtistId : null,
                        issue.Track.Album != null && issue.Track.Album.AlbumArtist != null
                            ? issue.Track.Album.AlbumArtist.Name
                            : null,
                        issue.Track.Album != null && issue.Track.Album.IsCompilation,
                        issue.Track.SourceId,
                        issue.Track.Source!.Name,
                        issue.Track.DirectoryPath,
                        issue.Track.BaseName,
                        issue.Track.Duration,
                        issue.Track.HasAudio,
                        issue.Track.HasVideo),
                issue.MediaFile == null
                    ? null
                    : new LibraryIssueFileRow(
                        issue.MediaFile.Id,
                        issue.MediaFile.SourceId,
                        issue.MediaFile.Source!.Name,
                        issue.MediaFile.RelativePath,
                        issue.MediaFile.Kind),
                issue.FileIssue,
                issue.MissingTags,
                issue.RelatedTrack == null
                    ? null
                    : new LibraryIssueTrackRow(
                        issue.RelatedTrack.Id,
                        issue.RelatedTrack.Title,
                        issue.RelatedTrack.ArtistId,
                        issue.RelatedTrack.Artist != null ? issue.RelatedTrack.Artist.Name : null,
                        issue.RelatedTrack.AlbumId,
                        issue.RelatedTrack.Album != null ? issue.RelatedTrack.Album.Title : null,
                        issue.RelatedTrack.Album != null ? issue.RelatedTrack.Album.AlbumArtistId : null,
                        issue.RelatedTrack.Album != null && issue.RelatedTrack.Album.AlbumArtist != null
                            ? issue.RelatedTrack.Album.AlbumArtist.Name
                            : null,
                        issue.RelatedTrack.Album != null && issue.RelatedTrack.Album.IsCompilation,
                        issue.RelatedTrack.SourceId,
                        issue.RelatedTrack.Source!.Name,
                        issue.RelatedTrack.DirectoryPath,
                        issue.RelatedTrack.BaseName,
                        issue.RelatedTrack.Duration,
                        issue.RelatedTrack.HasAudio,
                        issue.RelatedTrack.HasVideo)))
            .ToPageAsync(query.Offset, query.Limit, cancellationToken);

        return new Page<LibraryIssueRow>(
            [.. page.Items.Select(item => new LibraryIssueRow(
                item.Id,
                item.Type,
                item.DetectedAt,
                item.Track,
                item.File,
                item.FileIssue,
                item.MissingTags,
                item.RelatedTrack))],
            page.Offset,
            page.Limit,
            page.Total);
    }

    public async Task<IReadOnlyDictionary<LibraryIssueType, int>> CountAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.LibraryIssues.AsNoTracking()
            .GroupBy(issue => issue.Type)
            .Select(group => new { Type = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Type, group => group.Count, cancellationToken);
    }

    public Task<LibraryCheck?> FindLastCheckAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.LibraryChecks.AsNoTracking()
            .FirstOrDefaultAsync(check => check.Id == LibraryCheck.SingletonId, cancellationToken);
    }

    /// <summary>
    /// Which of the checked tags are missing; all of them without tags.
    /// </summary>
    internal static IReadOnlyList<MetadataTag> MissingOf((bool Title, bool Artist, bool Album)? tags)
    {
        if (tags is not { } presence)
        {
            return _checkedTags;
        }

        List<MetadataTag> missing = [];
        if (!presence.Title)
        {
            missing.Add(MetadataTag.Title);
        }

        if (!presence.Artist)
        {
            missing.Add(MetadataTag.Artist);
        }

        if (!presence.Album)
        {
            missing.Add(MetadataTag.Album);
        }

        return missing;
    }

    private async Task<LibraryIssueChanges> ReplaceInTransactionAsync(
        IReadOnlyCollection<DetectedLibraryIssue> issues,
        int trackCount,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        // Not tracked: most problems stay as they are, and only those whose details change are attached.
        List<LibraryIssue> stored = await dbContext.LibraryIssues.AsNoTracking().ToListAsync(cancellationToken);
        Dictionary<IssueKey, LibraryIssue> byKey = [];
        List<Guid> removed = [];
        foreach (LibraryIssue issue in stored)
        {
            // Two rows with the same key cannot be created by this repository; a second one is removed.
            if (!byKey.TryAdd(new IssueKey(issue.Type, issue.TrackId, issue.MediaFileId), issue))
            {
                removed.Add(issue.Id);
            }
        }

        HashSet<Guid> tracks = await ExistingAsync(
            issues.SelectMany(issue => (Guid?[])[issue.TrackId, issue.RelatedTrackId]),
            ids => dbContext.Tracks.Where(track => ids.Contains(track.Id)).Select(track => track.Id),
            cancellationToken);
        HashSet<Guid> files = await ExistingAsync(
            issues.Select(issue => issue.MediaFileId),
            ids => dbContext.MediaFiles.Where(file => ids.Contains(file.Id)).Select(file => file.Id),
            cancellationToken);

        HashSet<IssueKey> found = [];
        int added = 0;
        int kept = 0;
        foreach (DetectedLibraryIssue issue in issues)
        {
            if ((issue.TrackId is { } trackId && !tracks.Contains(trackId))
                || (issue.MediaFileId is { } fileId && !files.Contains(fileId)))
            {
                continue;
            }

            IssueKey key = new(issue.Type, issue.TrackId, issue.MediaFileId);
            if (!found.Add(key))
            {
                continue;
            }

            Guid? related = issue.RelatedTrackId is { } relatedId && tracks.Contains(relatedId) ? relatedId : null;
            List<MetadataTag> missingTags = [.. issue.MissingTags ?? []];
            if (byKey.TryGetValue(key, out LibraryIssue? existing))
            {
                kept++;
                if (existing.FileIssue != issue.FileIssue
                    || existing.RelatedTrackId != related
                    || !existing.MissingTags.SequenceEqual(missingTags))
                {
                    dbContext.LibraryIssues.Attach(existing);
                    existing.FileIssue = issue.FileIssue;
                    existing.MissingTags = missingTags;
                    existing.RelatedTrackId = related;
                }

                continue;
            }

            added++;
            dbContext.LibraryIssues.Add(new LibraryIssue
            {
                Id = Guid.CreateVersion7(),
                Type = issue.Type,
                TrackId = issue.TrackId,
                MediaFileId = issue.MediaFileId,
                FileIssue = issue.FileIssue,
                MissingTags = missingTags,
                RelatedTrackId = related,
                DetectedAt = checkedAt,
            });
        }

        foreach ((IssueKey key, LibraryIssue issue) in byKey)
        {
            if (!found.Contains(key))
            {
                removed.Add(issue.Id);
            }
        }

        foreach (Guid[] batch in removed.Chunk(BatchSize))
        {
            await dbContext.LibraryIssues
                .Where(issue => batch.Contains(issue.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        LibraryCheck? check = await dbContext.LibraryChecks
            .FirstOrDefaultAsync(row => row.Id == LibraryCheck.SingletonId, cancellationToken);
        if (check is null)
        {
            dbContext.LibraryChecks.Add(new LibraryCheck
            {
                Id = LibraryCheck.SingletonId,
                CheckedAt = checkedAt,
                TrackCount = trackCount,
            });
        }
        else
        {
            check.CheckedAt = checkedAt;
            check.TrackCount = trackCount;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new LibraryIssueChanges(added, removed.Count, kept);
    }

    /// <summary>
    /// The ids among <paramref name="ids"/> that still exist, asked in batches.
    /// </summary>
    private static async Task<HashSet<Guid>> ExistingAsync(
        IEnumerable<Guid?> ids,
        Func<Guid[], IQueryable<Guid>> query,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> existing = [];
        foreach (Guid[] batch in ids.OfType<Guid>().Distinct().Chunk(BatchSize))
        {
            existing.UnionWith(await query(batch).ToListAsync(cancellationToken));
        }

        return existing;
    }

    private async Task<HashSet<Guid>> TracksWithOwnCoverAsync(CancellationToken cancellationToken)
    {
        List<Guid> covers = await dbContext.Covers.AsNoTracking()
            .Where(cover => cover.TrackId != null)
            .Select(cover => cover.TrackId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        List<Guid> online = await dbContext.OnlineCovers.AsNoTracking()
            .Where(cover => cover.TrackId != null)
            .Select(cover => cover.TrackId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        return [.. covers, .. online];
    }

    /// <summary>
    /// Albums with a cover of their own (also an online cover). The covers of their tracks do not count, since a track
    /// never shows the cover of another track.
    /// </summary>
    private async Task<HashSet<Guid>> AlbumsWithCoverAsync(CancellationToken cancellationToken)
    {
        List<Guid> albumCovers = await dbContext.Covers.AsNoTracking()
            .Where(cover => cover.AlbumId != null)
            .Select(cover => cover.AlbumId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        List<Guid> onlineCovers = await dbContext.OnlineCovers.AsNoTracking()
            .Where(cover => cover.AlbumId != null)
            .Select(cover => cover.AlbumId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        return [.. albumCovers, .. onlineCovers];
    }

    private static IQueryable<LibraryIssue> Filter(IQueryable<LibraryIssue> issues, LibraryIssueListQuery query)
    {
        if (query.Types is { Count: > 0 } types)
        {
            LibraryIssueType[] wanted = [.. types.Distinct()];
            issues = issues.Where(issue => wanted.Contains(issue.Type));
        }

        if (query.Search is { Length: > 0 } search)
        {
            string pattern = LikePatterns.Contains(search);
            issues = issues.Where(issue =>
                (issue.Track != null
                    && (EF.Functions.ILike(issue.Track.Title, pattern, LikePatterns.EscapeCharacter)
                        || (issue.Track.Artist != null
                            && EF.Functions.ILike(issue.Track.Artist.Name, pattern, LikePatterns.EscapeCharacter))
                        || (issue.Track.Album != null
                            && EF.Functions.ILike(issue.Track.Album.Title, pattern, LikePatterns.EscapeCharacter))
                        || EF.Functions.ILike(issue.Track.DirectoryPath, pattern, LikePatterns.EscapeCharacter)
                        || EF.Functions.ILike(issue.Track.BaseName, pattern, LikePatterns.EscapeCharacter)))
                || (issue.MediaFile != null
                    && EF.Functions.ILike(issue.MediaFile.RelativePath, pattern, LikePatterns.EscapeCharacter)));
        }

        return issues;
    }

    private static IOrderedQueryable<LibraryIssue> ThenByPath(IOrderedQueryable<LibraryIssue> sorted)
    {
        return sorted.ThenBy(_sourceNameOf).ThenBy(_directoryOf).ThenBy(_nameOf);
    }

    /// <summary>
    /// What makes a problem the same as a stored one.
    /// </summary>
    private readonly record struct IssueKey(LibraryIssueType Type, Guid? TrackId, Guid? MediaFileId);

    /// <summary>
    /// A row of the list as the database returns it.
    /// </summary>
    private sealed record IssueData(
        Guid Id,
        LibraryIssueType Type,
        DateTimeOffset DetectedAt,
        LibraryIssueTrackRow? Track,
        LibraryIssueFileRow? File,
        MediaFileIssue? FileIssue,
        List<MetadataTag> MissingTags,
        LibraryIssueTrackRow? RelatedTrack);
}
