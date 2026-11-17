namespace WaveMP.Core.Lyrics.Services;

/// <summary>
/// The lyrics of the tracks, from their lyrics file, downloaded lyrics or their tags.
/// </summary>
public interface ILyricsService
{
    /// <summary>
    /// The lyrics of a track from the source of the highest priority that has lyrics: the lyrics file with its base
    /// name (<c>Song.lrc</c>), downloaded lyrics stored in the database, synchronized and then unsynchronized lyrics of
    /// the tags of its audio file (or of its video file).
    /// </summary>
    Task<LyricsResult> GetAsync(Guid trackId, CancellationToken cancellationToken = default);
}
