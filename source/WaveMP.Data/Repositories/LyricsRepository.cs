using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Segments;

namespace WaveMP.Data.Repositories;

internal sealed class LyricsRepository(WaveMpDbContext dbContext) : ILyricsRepository
{
    public async Task<LyricsTrackRow?> FindTrackAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        List<LyricsTrackRow> rows = await Rows(dbContext.Tracks.AsNoTracking().Where(track => track.Id == trackId))
            .ToListAsync(cancellationToken);
        return (await WithSongDurationsAsync(rows, cancellationToken)).FirstOrDefault();
    }

    public async Task<IReadOnlyList<LyricsTrackRow>> ListTracksToLookUpAsync(
        int limit,
        IReadOnlyCollection<Guid> excluded,
        DateTimeOffset? lookUpAgainBefore,
        int matcherVersion,
        CancellationToken cancellationToken = default)
    {
        Guid[] skipped = [.. excluded];
        bool lookUpAgain = lookUpAgainBefore is not null;
        DateTimeOffset before = lookUpAgainBefore.GetValueOrDefault();
        IQueryable<Track> tracks = dbContext.Tracks.AsNoTracking()
            .Where(track =>
                !dbContext.LyricsDownloads.Any(download => download.TrackId == track.Id)
                || dbContext.LyricsDownloads.Any(download => download.TrackId == track.Id
                    && ((download.Status == LyricsDownloadStatus.NotNeeded
                            && track.Files.Any(file => file.IsPrimary
                                && (file.Kind == MediaFileKind.Audio || file.Kind == MediaFileKind.Video)
                                && file.ScannedAt > download.CheckedAt))
                        || (download.Status == LyricsDownloadStatus.NotFound
                            && download.MatcherVersion < matcherVersion)
                        || (lookUpAgain
                            && (download.Status == LyricsDownloadStatus.NotFound
                                || download.Status == LyricsDownloadStatus.Instrumental
                                || download.Status == LyricsDownloadStatus.NotNeeded)
                            && download.CheckedAt < before))))
            .Where(track => !track.Files.Any(file => file.Kind == MediaFileKind.Lyrics))
            .Where(track => !skipped.Contains(track.Id))
            .OrderBy(track => dbContext.LyricsDownloads.Any(download => download.TrackId == track.Id))
            .ThenBy(track => track.FirstSeenAt)
            .ThenBy(track => track.Id)
            .Take(limit);
        return await WithSongDurationsAsync(await Rows(tracks).ToListAsync(cancellationToken), cancellationToken);
    }

    public async Task<bool> SaveDownloadAsync(LyricsDownload download, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Tracks.AnyAsync(track => track.Id == download.TrackId, cancellationToken))
        {
            return false;
        }

        LyricsDownload? existing = await dbContext.LyricsDownloads
            .FirstOrDefaultAsync(item => item.TrackId == download.TrackId, cancellationToken);
        if (existing is null)
        {
            existing = new LyricsDownload
            {
                TrackId = download.TrackId,
                Status = download.Status,
                CheckedAt = download.CheckedAt,
            };
            dbContext.LyricsDownloads.Add(existing);
        }

        existing.Status = download.Status;
        existing.Content = download.Content;
        existing.CheckedAt = download.CheckedAt;
        existing.OtherVersion = download.OtherVersion;
        existing.MatcherVersion = download.MatcherVersion;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // A scan may have removed the track in the meantime.
            if (await dbContext.Tracks.AnyAsync(track => track.Id == download.TrackId, CancellationToken.None))
            {
                throw;
            }

            return false;
        }
        finally
        {
            dbContext.Entry(existing).State = EntityState.Detached;
        }
    }

    public async Task<IReadOnlyDictionary<LyricsDownloadStatus, int>> CountDownloadsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.LyricsDownloads.AsNoTracking()
            .GroupBy(download => download.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
    }

    public async Task<int> DeleteDownloadsAsync(
        IReadOnlyCollection<LyricsDownloadStatus> statuses,
        Guid? trackId = null,
        CancellationToken cancellationToken = default)
    {
        LyricsDownloadStatus[] removed = [.. statuses];
        IQueryable<LyricsDownload> downloads = dbContext.LyricsDownloads
            .Where(download => removed.Contains(download.Status));
        if (trackId is { } id)
        {
            downloads = downloads.Where(download => download.TrackId == id);
        }

        return await downloads.ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Gives the rows the duration of the song instead of the duration of the file where the players use a song
    /// segment of the file the track plays (its audio file, otherwise its video): lyrics of the album version then fit
    /// a video with an intro.
    /// </summary>
    private async Task<IReadOnlyList<LyricsTrackRow>> WithSongDurationsAsync(
        List<LyricsTrackRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return rows;
        }

        Guid[] ids = [.. rows.Select(row => row.TrackId)];
        var files = await dbContext.MediaFiles.AsNoTracking()
            .Where(file => file.TrackId != null
                && ids.Contains(file.TrackId.Value)
                && file.IsPrimary
                && (file.Kind == MediaFileKind.Audio || file.Kind == MediaFileKind.Video))
            .Select(file => new { TrackId = file.TrackId!.Value, file.Kind, file.Duration, file.Analysis })
            .ToListAsync(cancellationToken);
        var manual = await dbContext.Tracks.AsNoTracking()
            .Where(track => ids.Contains(track.Id))
            .Select(track => new
            {
                track.Id,
                track.AudioSongStartMilliseconds,
                track.AudioSongEndMilliseconds,
                track.VideoSongStartMilliseconds,
                track.VideoSongEndMilliseconds,
            })
            .ToDictionaryAsync(track => track.Id, cancellationToken);
        return [.. rows.Select(row =>
        {
            var file = files.FirstOrDefault(file => file.TrackId == row.TrackId && file.Kind == MediaFileKind.Audio)
                ?? files.FirstOrDefault(file => file.TrackId == row.TrackId && file.Kind == MediaFileKind.Video);
            if (file is null || !manual.TryGetValue(row.TrackId, out var track))
            {
                return row;
            }

            bool audio = file.Kind == MediaFileKind.Audio;
            var segment = SongSegment.Of(
                file.Analysis,
                audio ? track.AudioSongStartMilliseconds : track.VideoSongStartMilliseconds,
                audio ? track.AudioSongEndMilliseconds : track.VideoSongEndMilliseconds,
                file.Duration);
            return segment is { IsConfident: true, SongLengthMilliseconds: { } length }
                ? row with { Duration = TimeSpan.FromMilliseconds(length) }
                : row;
        })];
    }

    private IQueryable<LyricsTrackRow> Rows(IQueryable<Track> tracks)
    {
        return tracks.Select(track => new LyricsTrackRow(
            track.Id,
            track.SourceId,
            track.DirectoryPath,
            track.BaseName,
            track.Title,
            track.Artist == null ? null : track.Artist.Name,
            track.Album == null ? null : track.Album.Title,
            track.Duration,
            track.Files
                .Where(file => file.Kind == MediaFileKind.Lyrics)
                .OrderBy(file => file.RelativePath)
                .Select(file => file.RelativePath)
                .FirstOrDefault(),
            track.Files
                .Where(file => file.Kind == MediaFileKind.Audio && file.IsPrimary)
                .Select(file => file.RelativePath)
                .FirstOrDefault(),
            track.Files
                .Where(file => file.Kind == MediaFileKind.Video && file.IsPrimary)
                .Select(file => file.RelativePath)
                .FirstOrDefault(),
            dbContext.LyricsDownloads
                .Where(download => download.TrackId == track.Id)
                .Select(download => new LyricsDownloadRow(
                    download.Status,
                    download.Content,
                    download.CheckedAt,
                    download.OtherVersion,
                    download.MatcherVersion))
                .FirstOrDefault())
        {
            Artists = track.ArtistCredits
                .OrderBy(credit => credit.Position)
                .Select(credit => new LyricsArtistRow(credit.Artist!.Name, credit.Role))
                .ToList(),
        });
    }
}
