using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WaveMP.Core.Metadata.Options;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Metadata.Sources;

/// <summary>
/// Keeps the media sources in memory. The first source follows <c>MEDIA_PATH</c> as long as it has no path of its own.
/// </summary>
internal sealed class MediaSourceCatalog(
    IServiceScopeFactory scopeFactory,
    IOptions<MediaLibraryOptions> mediaLibrary) : IMediaSourceCatalog
{
    private readonly Lock _lock = new();
    private IReadOnlyList<MediaSourceInfo>? _sources;

    public event EventHandler? Changed;

    public IReadOnlyList<MediaSourceInfo> Sources => GetOrLoad();

    public MediaSourceInfo? Find(Guid id)
    {
        return Sources.FirstOrDefault(source => source.Id == id);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MediaSourceInfo> loaded = await LoadAsync(cancellationToken);
        lock (_lock)
        {
            _sources = loaded;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<MediaSourceInfo> GetOrLoad()
    {
        lock (_lock)
        {
            if (_sources is not null)
            {
                return _sources;
            }
        }

        // Only if the start did not load them (e.g. in tests); the start loads them with RefreshAsync.
        IReadOnlyList<MediaSourceInfo> loaded = LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        lock (_lock)
        {
            _sources ??= loaded;
            return _sources;
        }
    }

    /// <summary>
    /// The source with the folder it uses: its own path, or <paramref name="mediaPath"/> without one.
    /// </summary>
    public static MediaSourceInfo ToInfo(MediaSource source, string mediaPath)
    {
        return new MediaSourceInfo(
            source.Id,
            source.Name,
            source.Path ?? mediaPath,
            source.IsActive,
            source.Path is null);
    }

    private async Task<IReadOnlyList<MediaSourceInfo>> LoadAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IReadOnlyList<MediaSource> sources = await scope.ServiceProvider
            .GetRequiredService<IMediaSourceRepository>()
            .ListAsync(cancellationToken);
        string mediaPath = mediaLibrary.Value.RootPath;
        return [.. sources.Select(source => ToInfo(source, mediaPath))];
    }
}
