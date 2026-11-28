using WaveMP.Data.Entities;
using WaveMP.Data.Segments;

namespace WaveMP.Data.Projections;

/// <summary>
/// A track with the file of one kind it plays.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="File">The primary file of the kind with its analysis; <c>null</c> if the track has no file of this
/// kind.</param>
/// <param name="ManualSongStartMilliseconds">Start of the song in the file as the admin corrected it (see
/// <see cref="Track.AudioSongStartMilliseconds"/> and <see cref="Track.VideoSongStartMilliseconds"/>).</param>
/// <param name="ManualSongEndMilliseconds">End of the song in the file as the admin corrected it.</param>
public sealed record TrackFileRow(
    Guid TrackId,
    MediaFile? File,
    int? ManualSongStartMilliseconds = null,
    int? ManualSongEndMilliseconds = null)
{
    /// <summary>
    /// Where the song lies in the file: the corrections of the admin, otherwise the analysis of the file.
    /// </summary>
    public SongSegment Segment => File is null
        ? SongSegment.WholeFile
        : SongSegment.Of(File.Analysis, ManualSongStartMilliseconds, ManualSongEndMilliseconds, File.Duration);
}
