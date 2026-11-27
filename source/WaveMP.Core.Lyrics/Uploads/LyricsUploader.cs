using Microsoft.Extensions.Logging;
using WaveMP.Core.Lyrics.Documents;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Core.Lyrics.Parsers;
using WaveMP.Core.Lyrics.Saving;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Lyrics.Uploads;

/// <summary>
/// Saves uploaded lyrics through the <see cref="LyricsSaver"/> (as they were uploaded, without byte order mark and with
/// <c>\n</c> line breaks) and publishes them to LRCLIB with the title, artists, album and duration of the track: the
/// lines without times as plain lyrics and, for lyrics with times, one timestamp per line as synchronized lyrics.
/// </summary>
internal sealed class LyricsUploader(
    ILyricsRepository repository,
    LyricsSaver saver,
    ILrclibClient lrclib,
    ISettingsSnapshot<LibrarySettings> settings,
    ILogger<LyricsUploader> logger) : ILyricsUploader
{
    public async Task<LyricsUploadResult> UploadAsync(
        Guid trackId,
        string text,
        bool publish,
        CancellationToken cancellationToken = default)
    {
        string cleaned = LyricsTexts.Clean(text);
        LyricsDocument document = LrcParser.Parse(cleaned);
        if (document.IsEmpty)
        {
            return new LyricsUploadResult(LyricsUploadOutcome.Empty);
        }

        LyricsTrackRow? track = await repository.FindTrackAsync(trackId, cancellationToken);
        if (track is null)
        {
            return new LyricsUploadResult(LyricsUploadOutcome.TrackNotFound);
        }

        LyricsUploadOutcome outcome = await saver.SaveAsync(
            track,
            cleaned,
            LyricsOrigin.Upload,
            otherVersion: false,
            replace: true,
            cancellationToken) switch
        {
            LyricsSaveOutcome.SavedToFile => LyricsUploadOutcome.SavedToFile,
            LyricsSaveOutcome.SavedToDatabase => LyricsUploadOutcome.SavedToDatabase,
            LyricsSaveOutcome.TrackNotFound => LyricsUploadOutcome.TrackNotFound,
            _ => LyricsUploadOutcome.FileNotWritable,
        };
        if (outcome is not (LyricsUploadOutcome.SavedToFile or LyricsUploadOutcome.SavedToDatabase) || !publish)
        {
            return new LyricsUploadResult(outcome);
        }

        return new LyricsUploadResult(outcome, await PublishAsync(track, document, cancellationToken));
    }

    /// <summary>
    /// What LRCLIB gets for the lyrics of a track, or <c>null</c> if the track lacks title, artist, album or
    /// duration.
    /// </summary>
    internal static LrclibPublication? PublicationOf(LyricsTrackRow track, LyricsDocument document)
    {
        var plan = LyricsLookupPlan.Of(track);
        string? artist = ArtistTextOf(track) ?? plan.Artist;
        if (artist is null || track.AlbumTitle is not { } album || track.Duration is not { } duration
            || duration <= TimeSpan.Zero)
        {
            return null;
        }

        return new LrclibPublication(
            plan.Title,
            artist,
            album,
            duration,
            LyricsTexts.ToPlain(document),
            LyricsTexts.ToSynchronized(document) ?? string.Empty);
    }

    /// <summary>
    /// The artists of the track as one text: the main artists joined by commas, then "feat." and the featured
    /// artists; <c>null</c> without artists.
    /// </summary>
    internal static string? ArtistTextOf(LyricsTrackRow track)
    {
        string main = string.Join(
            ", ",
            track.Artists.Where(artist => artist.Role == ArtistRole.Main).Select(artist => artist.Name));
        string featured = string.Join(
            ", ",
            track.Artists.Where(artist => artist.Role == ArtistRole.Featured).Select(artist => artist.Name));
        if (main.Length == 0)
        {
            return featured.Length == 0 ? null : featured;
        }

        return featured.Length == 0 ? main : $"{main} feat. {featured}";
    }

    private async Task<LyricsPublication> PublishAsync(
        LyricsTrackRow track,
        LyricsDocument document,
        CancellationToken cancellationToken)
    {
        if (!settings.Current.DownloadLyrics)
        {
            return LyricsPublication.Disabled;
        }

        if (PublicationOf(track, document) is not { } publication)
        {
            return LyricsPublication.MissingMetadata;
        }

        LrclibPublishResult result = await lrclib.PublishAsync(publication, cancellationToken);
        if (result.Status == LrclibPublishStatus.Published)
        {
            logger.LogInformation("Lyrics of track {TrackId} published to LRCLIB", track.TrackId);
        }

        return result.Status switch
        {
            LrclibPublishStatus.Published => LyricsPublication.Published,
            LrclibPublishStatus.Rejected => LyricsPublication.Rejected,
            LrclibPublishStatus.RateLimited => LyricsPublication.RateLimited,
            _ => LyricsPublication.Unavailable,
        };
    }
}
