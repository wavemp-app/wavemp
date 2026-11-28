using WaveMP.Core.Streaming.Formats;
using WaveMP.Data.Entities;
using WaveMP.Data.Segments;

namespace WaveMP.Core.Streaming.Sources;

/// <summary>
/// The file whose sound a track plays, as found on the disk right now: its audio file or, for tracks with a video
/// only, the video (see <see cref="Origin"/>).
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="FileId">Id of the file in the library.</param>
/// <param name="FullPath">Absolute path of the file; always inside the media folder.</param>
/// <param name="Format">Format of the file; <c>null</c> if clients cannot play it directly, always for videos.</param>
/// <param name="ContentType">MIME type the file is delivered with.</param>
/// <param name="Length">Size of the file in bytes.</param>
/// <param name="LastModified">Time of the last change of the file, in whole seconds (HTTP precision).</param>
/// <param name="ETag">Strong entity tag (without quotes) of this version of the file.</param>
/// <param name="Duration">Duration of the file, if known.</param>
/// <param name="ReplayGain">The ReplayGain tags of the file.</param>
/// <param name="Origin">Whether the sound comes from the audio file or from the video of the track.</param>
/// <param name="SampleRate">Sample rate of the sound, if known.</param>
/// <param name="Segment">Where the song lies in the file (see <see cref="SongSegment"/>); the whole file if
/// unknown.</param>
/// <param name="LoudnessLufs">Integrated loudness of the sound in LUFS, if measured.</param>
/// <param name="LoudnessMatchDb">Gain in dB that brings the sound of a video to the loudness of the audio file of the
/// track (see <see cref="LoudnessMatch"/>); <c>null</c> for the audio file itself and if a loudness is unknown.
/// </param>
public sealed record StreamSource(
    Guid TrackId,
    Guid FileId,
    string FullPath,
    AudioFormat? Format,
    string ContentType,
    long Length,
    DateTimeOffset LastModified,
    string ETag,
    TimeSpan? Duration,
    ReplayGainValues ReplayGain,
    MediaFileKind Origin = MediaFileKind.Audio,
    int? SampleRate = null,
    SongSegment? Segment = null,
    double? LoudnessLufs = null,
    double? LoudnessMatchDb = null);
