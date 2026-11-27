using Microsoft.Extensions.Logging;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Core.Lyrics.Files;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Settings.Groups;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Lyrics.Saving;

/// <summary>
/// Keeps lyrics of a track: as file <c>Song.lrc</c> next to the track, which the folder is read again for at once, so
/// the library knows the file; if the folder is not writable, in the database. The result is stored with the track
/// (see <see cref="LyricsDownload"/>), so the download job does not look the track up again. An existing lyrics file is
/// only replaced when an admin chose or uploaded the lyrics.
/// </summary>
internal sealed class LyricsSaver(
    ILyricsRepository repository,
    ILyricsFileStore files,
    ILibraryScanner scanner,
    TimeProvider timeProvider,
    ILogger<LyricsSaver> logger)
{
    /// <summary>
    /// The path of the lyrics file of a track relative to the media folder.
    /// </summary>
    public static string LyricsFilePathOf(LyricsTrackRow track)
    {
        string fileName = $"{track.BaseName}.{LibrarySettings.LyricsExtension}";
        return track.DirectoryPath.Length == 0 ? fileName : $"{track.DirectoryPath}/{fileName}";
    }

    /// <summary>
    /// Saves the lyrics of a track.
    /// </summary>
    /// <param name="track">The track.</param>
    /// <param name="text">The lyrics, in the LRC format or as plain lines.</param>
    /// <param name="origin">Where the lyrics come from; uploaded lyrics are stored as
    /// <see cref="LyricsDownloadStatus.Uploaded"/>.</param>
    /// <param name="otherVersion">Whether the lyrics belong to another version of the song.</param>
    /// <param name="replace">Whether an existing lyrics file is replaced (lyrics an admin chose or uploaded).</param>
    /// <param name="cancellationToken">Aborts saving.</param>
    public async Task<LyricsSaveOutcome> SaveAsync(
        LyricsTrackRow track,
        string text,
        LyricsOrigin origin,
        bool otherVersion,
        bool replace,
        CancellationToken cancellationToken)
    {
        string path = track.LyricsFilePath ?? LyricsFilePathOf(track);
        LyricsFileWriteStatus written = track.LyricsFilePath is null
            ? await files.CreateAsync(track.SourceId, path, text, cancellationToken)
            : LyricsFileWriteStatus.Exists;
        if (written == LyricsFileWriteStatus.Exists)
        {
            if (!replace)
            {
                return LyricsSaveOutcome.FileExists;
            }

            // A lyrics file that is there is replaced; its folder is writable or the lyrics cannot be saved, since the
            // file would hide lyrics in the database.
            written = await files.ReplaceAsync(track.SourceId, path, text, cancellationToken);
            if (written != LyricsFileWriteStatus.Created)
            {
                return LyricsSaveOutcome.FileNotWritable;
            }
        }

        if (written == LyricsFileWriteStatus.NotWritable)
        {
            LyricsDownloadStatus inDatabase = origin == LyricsOrigin.Upload
                ? LyricsDownloadStatus.Uploaded
                : LyricsDownloadStatus.SavedToDatabase;
            return await SaveResultAsync(track, inDatabase, text, otherVersion, cancellationToken)
                ? LyricsSaveOutcome.SavedToDatabase
                : LyricsSaveOutcome.TrackNotFound;
        }

        LyricsDownloadStatus asFile = origin == LyricsOrigin.Upload
            ? LyricsDownloadStatus.Uploaded
            : LyricsDownloadStatus.SavedToFile;
        bool saved = await SaveResultAsync(track, asFile, null, otherVersion, cancellationToken);
        await RefreshFolderAsync(track, path);
        logger.LogInformation("Lyrics of track {TrackId} saved as {File}", track.TrackId, path);
        return saved ? LyricsSaveOutcome.SavedToFile : LyricsSaveOutcome.TrackNotFound;
    }

    /// <summary>
    /// Stores the result of a lookup with the track, with the current version of the rules of the lookup. Returns
    /// <c>false</c> if the track is gone in the meantime.
    /// </summary>
    public Task<bool> SaveResultAsync(
        LyricsTrackRow track,
        LyricsDownloadStatus status,
        string? content,
        bool otherVersion,
        CancellationToken cancellationToken)
    {
        return repository.SaveDownloadAsync(
            new LyricsDownload
            {
                TrackId = track.TrackId,
                Status = status,
                Content = content,
                CheckedAt = timeProvider.GetUtcNow(),
                OtherVersion = otherVersion,
                MatcherVersion = LrclibMatcher.Version,
            },
            cancellationToken);
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
