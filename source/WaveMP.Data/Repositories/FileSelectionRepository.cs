using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

internal sealed class FileSelectionRepository(WaveMpDbContext dbContext) : IFileSelectionRepository
{
    public async Task<TrackFiles?> LoadTrackAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        string? directoryPath = await dbContext.Tracks.AsNoTracking()
            .Where(track => track.Id == trackId)
            .Select(track => track.DirectoryPath)
            .FirstOrDefaultAsync(cancellationToken);
        if (directoryPath is null)
        {
            return null;
        }

        List<MediaFile> files = await dbContext.MediaFiles.AsNoTracking()
            .Where(file => file.TrackId == trackId)
            .OrderBy(file => file.RelativePath)
            .ToListAsync(cancellationToken);
        List<Cover> covers = await dbContext.Covers.AsNoTracking()
            .Where(cover => cover.TrackId == trackId)
            .OrderBy(cover => cover.Id)
            .ToListAsync(cancellationToken);
        List<TrackFileSelection> selections = await dbContext.TrackFileSelections.AsNoTracking()
            .Where(selection => selection.TrackId == trackId)
            .ToListAsync(cancellationToken);
        return new TrackFiles(trackId, directoryPath, files, covers, selections);
    }

    public async Task<bool> ReplaceAsync(
        Guid trackId,
        IReadOnlyCollection<TrackFileSelection> selections,
        CancellationToken cancellationToken = default)
    {
        if (selections.Any(selection => selection.TrackId != trackId))
        {
            throw new ArgumentException("All selections must belong to the track.", nameof(selections));
        }

        if (!await dbContext.Tracks.AnyAsync(track => track.Id == trackId, cancellationToken))
        {
            return false;
        }

        try
        {
            // Rows that stay are updated in place; all changes are saved in one transaction.
            List<TrackFileSelection> existing = await dbContext.TrackFileSelections
                .Where(selection => selection.TrackId == trackId)
                .ToListAsync(cancellationToken);
            foreach (TrackFileSelection selection in selections)
            {
                TrackFileSelection? stored = existing.Find(item => item.Category == selection.Category);
                if (stored is null)
                {
                    dbContext.TrackFileSelections.Add(selection);
                    continue;
                }

                existing.Remove(stored);
                stored.MediaFileId = selection.MediaFileId;
                stored.RelativePath = selection.RelativePath;
                stored.IsMissing = selection.IsMissing;
                stored.SelectedAt = selection.SelectedAt;
            }

            dbContext.TrackFileSelections.RemoveRange(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
        }

        return true;
    }

    public async Task<Page<TrackFileSummaryRow>> ListAsync(
        FileSelectionListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Track> tracks = Filter(dbContext.Tracks.AsNoTracking(), query);
        IOrderedQueryable<Track> sorted = query.Sort switch
        {
            FileSelectionSortField.Title => tracks
                .OrderBy(track => track.Title, query.Direction)
                .ThenBy(track => track.DirectoryPath)
                .ThenBy(track => track.BaseName),
            FileSelectionSortField.FileCount => tracks
                .OrderBy(
                    track => track.Files.Count(file => file.Kind != MediaFileKind.Image) + track.Covers.Count,
                    query.Direction)
                .ThenBy(track => track.DirectoryPath)
                .ThenBy(track => track.BaseName),
            _ => tracks
                .OrderBy(track => track.DirectoryPath, query.Direction)
                .ThenBy(track => track.BaseName, query.Direction),
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
                AudioCount = track.Files.Count(file => file.Kind == MediaFileKind.Audio),
                VideoCount = track.Files.Count(file => file.Kind == MediaFileKind.Video),
                CoverCount = track.Covers.Count,
                Audio = track.FileSelections
                    .Where(selection => selection.Category == FileSelectionCategory.Audio)
                    .Select(selection => (bool?)selection.IsMissing)
                    .FirstOrDefault(),
                Video = track.FileSelections
                    .Where(selection => selection.Category == FileSelectionCategory.Video)
                    .Select(selection => (bool?)selection.IsMissing)
                    .FirstOrDefault(),
                Cover = track.FileSelections
                    .Where(selection => selection.Category == FileSelectionCategory.Cover)
                    .Select(selection => (bool?)selection.IsMissing)
                    .FirstOrDefault(),
            })
            .ToPageAsync(query.Offset, query.Limit, cancellationToken);

        return new Page<TrackFileSummaryRow>(
            [.. page.Items.Select(item => new TrackFileSummaryRow(
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
                item.AudioCount,
                item.VideoCount,
                item.CoverCount,
                StateOf(item.Audio),
                StateOf(item.Video),
                StateOf(item.Cover)))],
            page.Offset,
            page.Limit,
            page.Total);
    }

    private static IQueryable<Track> Filter(IQueryable<Track> tracks, FileSelectionListQuery query)
    {
        if (query.Search is { Length: > 0 } search)
        {
            string pattern = LikePatterns.Contains(search);
            tracks = tracks.Where(track =>
                EF.Functions.ILike(track.Title, pattern, LikePatterns.EscapeCharacter)
                || EF.Functions.ILike(track.DirectoryPath, pattern, LikePatterns.EscapeCharacter)
                || EF.Functions.ILike(track.BaseName, pattern, LikePatterns.EscapeCharacter));
        }

        return query.Filter switch
        {
            FileSelectionFilter.SeveralAudio =>
                tracks.Where(track => track.Files.Count(file => file.Kind == MediaFileKind.Audio) > 1),
            FileSelectionFilter.SeveralVideo =>
                tracks.Where(track => track.Files.Count(file => file.Kind == MediaFileKind.Video) > 1),
            FileSelectionFilter.SeveralCovers => tracks.Where(track => track.Covers.Count > 1),
            FileSelectionFilter.Choices => tracks.Where(track =>
                track.Files.Count(file => file.Kind == MediaFileKind.Audio) > 1
                || track.Files.Count(file => file.Kind == MediaFileKind.Video) > 1
                || track.Covers.Count > 1),
            FileSelectionFilter.Selected => tracks.Where(track => track.FileSelections.Any()),
            FileSelectionFilter.Missing =>
                tracks.Where(track => track.FileSelections.Any(selection => selection.IsMissing)),
            _ => tracks,
        };
    }

    private static FileSelectionState StateOf(bool? isMissing)
    {
        return isMissing switch
        {
            null => FileSelectionState.Automatic,
            true => FileSelectionState.Missing,
            false => FileSelectionState.Selected,
        };
    }
}
