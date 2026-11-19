using WaveMP.Core.Lyrics.Documents;
using WaveMP.Core.Lyrics.Files;
using WaveMP.Core.Lyrics.Parsers;
using WaveMP.Core.Metadata.Files;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Lyrics.Services;

/// <summary>
/// Finds the lyrics of a track in the order of their sources. A source without a line with text (an empty or
/// unreadable file, empty tags) is skipped. Tags are read when the lyrics are asked for, so changed tags apply at once.
/// </summary>
internal sealed class LyricsService(
    ILyricsRepository repository,
    ILyricsFileStore files,
    ILyricsTagReader tagReader,
    IMediaFileLocator locator) : ILyricsService
{
    public async Task<LyricsResult> GetAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        LyricsTrackRow? track = await repository.FindTrackAsync(trackId, cancellationToken);
        if (track is null)
        {
            return LyricsResult.TrackNotFound;
        }

        TrackLyrics? lyrics = await FindAsync(track, cancellationToken);
        return lyrics is null ? LyricsResult.NoLyrics : LyricsResult.Found(lyrics);
    }

    /// <summary>
    /// The lyrics of the track, or <c>null</c> if no source has any.
    /// </summary>
    internal async Task<TrackLyrics?> FindAsync(LyricsTrackRow track, CancellationToken cancellationToken)
    {
        if (track.LyricsFilePath is { } path
            && await files.ReadAsync(track.SourceId, path, cancellationToken) is { } text
            && LrcParser.Parse(text) is { IsEmpty: false } fromFile)
        {
            return new TrackLyrics(fromFile, LyricsSource.File);
        }

        if (track.Download is { Status: LyricsDownloadStatus.SavedToDatabase, Content: { } stored }
            && LrcParser.Parse(stored) is { IsEmpty: false } downloaded)
        {
            return new TrackLyrics(downloaded, LyricsSource.Download);
        }

        return ReadTags(track) is { } tags ? new TrackLyrics(tags, LyricsSource.Tags) : null;
    }

    /// <summary>
    /// The lyrics of the tags of the audio file, otherwise of the video file.
    /// </summary>
    internal LyricsDocument? ReadTags(LyricsTrackRow track)
    {
        foreach (string? relativePath in (string?[])[track.AudioFilePath, track.VideoFilePath])
        {
            if (relativePath is not null
                && locator.TryGetFullPath(track.SourceId, relativePath, out string path)
                && tagReader.Read(path) is { } tags
                && TagLyricsParser.Parse(tags) is { IsEmpty: false } document)
            {
                return document;
            }
        }

        return null;
    }
}
