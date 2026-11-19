using Microsoft.Extensions.Logging;
using WaveMP.Core.Lyrics.Files;
using WaveMP.Core.Lyrics.Services;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// Looks up the lyrics of a track in LRCLIB: first exactly by title, artist, album and duration, then by a search for
/// title and artist, preferring synchronized lyrics (see <see cref="LrclibMatcher"/>). Found lyrics become the file
/// <c>Song.lrc</c> next to the track, which the folder is read again for at once, so the library knows the file; if the
/// folder is not writable, they are stored in the database. An existing lyrics file is never replaced.
/// </summary>
internal sealed class LyricsDownloader(
    ILyricsRepository repository,
    ILyricsService lyrics,
    ILrclibClient lrclib,
    ILyricsFileStore files,
    ILibraryScanner scanner,
    ISettingsSnapshot<LibrarySettings> settings,
    TimeProvider timeProvider,
    ILogger<LyricsDownloader> logger) : ILyricsDownloader
{
    public async Task<LyricsDownloadResult> DownloadAsync(
        Guid trackId,
        LyricsDownloadMode mode,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Current.DownloadLyrics)
        {
            return new LyricsDownloadResult(LyricsDownloadOutcome.Disabled);
        }

        LyricsTrackRow? track = await repository.FindTrackAsync(trackId, cancellationToken);
        if (track is null)
        {
            return new LyricsDownloadResult(LyricsDownloadOutcome.TrackNotFound);
        }

        if (track.LyricsFilePath is not null)
        {
            return new LyricsDownloadResult(LyricsDownloadOutcome.LyricsFileExists);
        }

        if (mode == LyricsDownloadMode.Automatic
            && (await lyrics.GetAsync(trackId, cancellationToken)).Status == LyricsStatus.Found)
        {
            return await SaveResultAsync(track, LyricsDownloadStatus.NotNeeded, null, cancellationToken)
                ?? new LyricsDownloadResult(LyricsDownloadOutcome.NotNeeded);
        }

        LrclibQuery query = new(track.Title, track.ArtistName, track.AlbumTitle, track.Duration);
        (LrclibResponse response, LrclibRecord? record, bool instrumental) = await LookUpAsync(query, cancellationToken);
        if (response.Status is LrclibStatus.RateLimited)
        {
            return new LyricsDownloadResult(LyricsDownloadOutcome.RateLimited, response.RetryAfter);
        }

        if (response.Status is LrclibStatus.Unavailable)
        {
            return new LyricsDownloadResult(LyricsDownloadOutcome.Unavailable);
        }

        if (record?.Lyrics is not { } text)
        {
            LyricsDownloadStatus status = instrumental ? LyricsDownloadStatus.Instrumental : LyricsDownloadStatus.NotFound;
            return await SaveResultAsync(track, status, null, cancellationToken)
                ?? new LyricsDownloadResult(instrumental
                    ? LyricsDownloadOutcome.Instrumental
                    : LyricsDownloadOutcome.NotFound);
        }

        return await SaveLyricsAsync(track, text, cancellationToken);
    }

    /// <summary>
    /// The path of the lyrics file of a track relative to the media folder.
    /// </summary>
    internal static string LyricsFilePathOf(LyricsTrackRow track)
    {
        string fileName = $"{track.BaseName}.{LibrarySettings.LyricsExtension}";
        return track.DirectoryPath.Length == 0 ? fileName : $"{track.DirectoryPath}/{fileName}";
    }

    /// <summary>
    /// Asks LRCLIB exactly if the query is complete, otherwise (or if nothing fits) searches. Returns the last
    /// answer, the chosen track and whether a fitting track is instrumental.
    /// </summary>
    private async Task<(LrclibResponse Response, LrclibRecord? Record, bool Instrumental)> LookUpAsync(
        LrclibQuery query,
        CancellationToken cancellationToken)
    {
        bool instrumental = false;
        if (query.IsComplete)
        {
            LrclibResponse exact = await lrclib.GetAsync(query, cancellationToken);
            if (exact.Status is LrclibStatus.RateLimited or LrclibStatus.Unavailable)
            {
                return (exact, null, false);
            }

            if (LrclibMatcher.Choose(exact.Records, query, out instrumental) is { } match)
            {
                return (exact, match, false);
            }
        }

        LrclibResponse search = await lrclib.SearchAsync(query, cancellationToken);
        LrclibRecord? found = LrclibMatcher.Choose(search.Records, query, out bool searchInstrumental);
        return (search, found, found is null && (instrumental || searchInstrumental));
    }

    private async Task<LyricsDownloadResult> SaveLyricsAsync(
        LyricsTrackRow track,
        string text,
        CancellationToken cancellationToken)
    {
        string path = LyricsFilePathOf(track);
        LyricsFileWriteStatus written = await files.CreateAsync(track.SourceId, path, text, cancellationToken);
        if (written == LyricsFileWriteStatus.Exists)
        {
            return new LyricsDownloadResult(LyricsDownloadOutcome.LyricsFileExists);
        }

        if (written == LyricsFileWriteStatus.NotWritable)
        {
            return await SaveResultAsync(track, LyricsDownloadStatus.SavedToDatabase, text, cancellationToken)
                ?? new LyricsDownloadResult(LyricsDownloadOutcome.SavedToDatabase);
        }

        LyricsDownloadResult? missing =
            await SaveResultAsync(track, LyricsDownloadStatus.SavedToFile, null, cancellationToken);
        await RefreshFolderAsync(track, path);
        logger.LogInformation("Lyrics of track {TrackId} saved as {File}", track.TrackId, path);
        return missing ?? new LyricsDownloadResult(LyricsDownloadOutcome.SavedToFile);
    }

    /// <summary>
    /// Stores the result of the lookup with the track. Returns <see cref="LyricsDownloadOutcome.TrackNotFound"/> if
    /// the track is gone in the meantime, otherwise <c>null</c>.
    /// </summary>
    private async Task<LyricsDownloadResult?> SaveResultAsync(
        LyricsTrackRow track,
        LyricsDownloadStatus status,
        string? content,
        CancellationToken cancellationToken)
    {
        bool saved = await repository.SaveDownloadAsync(
            new LyricsDownload
            {
                TrackId = track.TrackId,
                Status = status,
                Content = content,
                CheckedAt = timeProvider.GetUtcNow(),
            },
            cancellationToken);
        return saved ? null : new LyricsDownloadResult(LyricsDownloadOutcome.TrackNotFound);
    }

    /// <summary>
    /// Reads the folder of the track again, so the new lyrics file becomes part of the library. If that fails, the
    /// next scan finds the file.
    /// </summary>
    private async Task RefreshFolderAsync(LyricsTrackRow track, string path)
    {
        try
        {
            await scanner.RefreshFolderAsync(
                track.SourceId,
                track.DirectoryPath,
                [path],
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "The folder {Folder} could not be read again after saving lyrics; the next scan finds them",
                track.DirectoryPath);
        }
    }
}
