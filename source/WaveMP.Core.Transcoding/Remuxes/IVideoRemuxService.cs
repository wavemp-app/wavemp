using WaveMP.Core.Streaming.Decisions;
using WaveMP.Core.Streaming.Sources;

namespace WaveMP.Core.Transcoding.Remuxes;

/// <summary>
/// Videos whose picture a client plays in another container: the picture is copied into an MP4 file in the cache
/// (a remux), which the client plays like a file, with range requests, instead of an HLS stream that is transcoded.
/// </summary>
public interface IVideoRemuxService
{
    /// <summary>
    /// Whether the video can be remuxed: ffmpeg and the cache are available and the file did not fail before.
    /// </summary>
    bool CanRemux(VideoSource source, RemuxAudio audio);

    /// <summary>
    /// The remuxed video of the track, remuxed first if it is not in the cache yet.
    /// </summary>
    Task<RemuxResult> GetAsync(Guid trackId, RemuxAudio audio, CancellationToken cancellationToken = default);
}
