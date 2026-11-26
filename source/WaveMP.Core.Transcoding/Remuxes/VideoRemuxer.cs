using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Caching.Stores;
using WaveMP.Core.Streaming.Decisions;
using WaveMP.Core.Streaming.Sources;
using WaveMP.Core.Transcoding.Accelerators;
using WaveMP.Core.Transcoding.Arguments;
using WaveMP.Core.Transcoding.Options;
using WaveMP.Core.Transcoding.Processes;

namespace WaveMP.Core.Transcoding.Remuxes;

/// <summary>
/// Remuxes videos into the cache, each file version once: requests for the same remux wait for the one that runs, a
/// limited number runs at a time, and a file ffmpeg cannot remux is remembered until the restart, so it is transcoded
/// instead.
/// </summary>
internal sealed class VideoRemuxer(
    IFfmpegCommandRunner runner,
    ICacheStore cache,
    IHardwareAccelerationService hardware,
    IOptions<TranscodingOptions> options,
    ILogger<VideoRemuxer> logger) : IDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<Task<bool>>> _running = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _failed = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _slots = new(Math.Max(1, options.Value.MaxParallelRemuxes));

    /// <summary>
    /// Whether ffmpeg can be started and the cache can be written.
    /// </summary>
    public bool IsAvailable => hardware.Current.FfmpegAvailable && cache.IsAvailable;

    /// <summary>
    /// Whether ffmpeg could not remux the file version of the key.
    /// </summary>
    public bool HasFailed(string key)
    {
        return _failed.ContainsKey(key);
    }

    /// <summary>
    /// Makes sure the remuxed file of the key is in the cache; <c>false</c> if it cannot be made. The remux goes on
    /// when the request is cancelled, so the next request finds it.
    /// </summary>
    public async Task<bool> EnsureAsync(
        VideoSource source,
        RemuxAudio audio,
        string key,
        CancellationToken cancellationToken)
    {
        if (cache.Contains(CacheArea.Transcodes, RemuxKeys.NameOf(key)))
        {
            return true;
        }

        if (HasFailed(key))
        {
            return false;
        }

        Lazy<Task<bool>> remux = _running.GetOrAdd(
            key,
            name => new Lazy<Task<bool>>(() => Task.Run(() => RemuxAsync(source, audio, name))));
        return await remux.Value.WaitAsync(cancellationToken);
    }

    public void Dispose()
    {
        _slots.Dispose();
    }

    private async Task<bool> RemuxAsync(VideoSource source, RemuxAudio audio, string key)
    {
        await _slots.WaitAsync();
        string temporary = cache.CreateTemporaryPath(".mp4");
        try
        {
            var watch = Stopwatch.StartNew();
            FfmpegCommandResult result = await runner.RunAsync(
                FfmpegRemuxArguments.Build(source.FullPath, source.VideoCodec, audio, temporary),
                options.Value.RemuxTimeout);
            if (!result.Succeeded)
            {
                if (result.Started)
                {
                    _failed.TryAdd(key, 0);
                }

                logger.LogWarning(
                    "Remux of {Path} failed with exit code {ExitCode}, the video is transcoded instead: {Error}",
                    source.FullPath,
                    result.ExitCode,
                    result.ErrorOutput);
                cache.DeleteTemporary(temporary);
                return false;
            }

            if (!cache.Commit(CacheArea.Transcodes, RemuxKeys.NameOf(key), temporary))
            {
                logger.LogWarning("The remux of {Path} cannot be put into the cache", source.FullPath);
                return false;
            }

            logger.LogInformation(
                "Remuxed the video of track {TrackId} ({Audio} sound) in {Seconds:0.0} s",
                source.TrackId,
                audio,
                watch.Elapsed.TotalSeconds);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Remux of {Path} failed", source.FullPath);
            cache.DeleteTemporary(temporary);
            return false;
        }
        finally
        {
            _slots.Release();
            _running.TryRemove(key, out _);
        }
    }
}
