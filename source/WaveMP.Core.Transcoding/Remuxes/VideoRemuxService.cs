using WaveMP.Core.Caching.Stores;
using WaveMP.Core.Streaming.Decisions;
using WaveMP.Core.Streaming.Sources;

namespace WaveMP.Core.Transcoding.Remuxes;

internal sealed class VideoRemuxService(IStreamService streams, VideoRemuxer remuxer, ICacheStore cache)
    : IVideoRemuxService
{
    public bool CanRemux(VideoSource source, RemuxAudio audio)
    {
        ArgumentNullException.ThrowIfNull(source);
        return remuxer.IsAvailable && !remuxer.HasFailed(RemuxKeys.Of(source, audio));
    }

    public async Task<RemuxResult> GetAsync(
        Guid trackId,
        RemuxAudio audio,
        CancellationToken cancellationToken = default)
    {
        VideoSourceResult video = await streams.GetVideoSourceAsync(trackId, cancellationToken);
        if (!video.IsFound)
        {
            return RemuxResult.Of(video.Status switch
            {
                StreamSourceStatus.TrackNotFound => RemuxStatus.TrackNotFound,
                StreamSourceStatus.FileMissing => RemuxStatus.FileMissing,
                _ => RemuxStatus.NoVideo,
            });
        }

        if (!remuxer.IsAvailable)
        {
            return RemuxResult.Of(RemuxStatus.Unavailable);
        }

        VideoSource source = video.Source;
        string key = RemuxKeys.Of(source, audio);
        if (!await remuxer.EnsureAsync(source, audio, key, cancellationToken))
        {
            return RemuxResult.Of(RemuxStatus.Failed);
        }

        // The clean-up of the cache may remove the file right after the remux; the next request remuxes it again.
        Stream? stream = cache.TryOpen(CacheArea.Transcodes, RemuxKeys.NameOf(key));
        return stream is null
            ? RemuxResult.Of(RemuxStatus.Failed)
            : new RemuxResult(RemuxStatus.Ready, stream, key, source.LastModified);
    }
}
