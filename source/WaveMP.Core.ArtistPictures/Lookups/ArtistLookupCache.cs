using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.ArtistPictures.Options;
using WaveMP.Core.Caching.Stores;

namespace WaveMP.Core.ArtistPictures.Lookups;

/// <summary>
/// The answers, candidates and previews of the online lookup in the area <see cref="CacheArea.ArtistLookups"/> of the
/// cache: they count towards its size limit, are removed least recently used first and with the cache. JSON entries
/// carry the time they were fetched and are only used for <see cref="ArtistPictureOptions.LookupCacheDuration"/>.
/// Names are built from hashes and ids formatted here, never from text of a client or a service, and never contain the
/// API key of fanart.tv.
/// </summary>
internal sealed class ArtistLookupCache(
    ICacheStore store,
    IOptions<ArtistPictureOptions> options,
    TimeProvider timeProvider,
    ILogger<ArtistLookupCache> logger)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Whether entries can be stored.
    /// </summary>
    public bool IsAvailable => store.IsAvailable;

    /// <summary>
    /// A stable part of a name for any text: the hexadecimal SHA-256 hash of its parts.
    /// </summary>
    public static string Hash(params string[] parts)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts)));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// The value of a JSON entry that has not expired; <c>null</c> if there is none or it cannot be read.
    /// </summary>
    public async Task<T?> ReadAsync<T>(string name, CancellationToken cancellationToken)
        where T : class
    {
        Stream? content = store.TryOpen(CacheArea.ArtistLookups, name);
        if (content is null)
        {
            return null;
        }

        try
        {
            await using (content)
            {
                Entry<T>? entry = await JsonSerializer.DeserializeAsync<Entry<T>>(
                    content,
                    _jsonOptions,
                    cancellationToken);
                return entry is not null && !IsExpired(entry.FetchedAt) ? entry.Value : null;
            }
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            logger.LogDebug(exception, "Cannot read {Name} of the artist lookups from the cache", name);
            return null;
        }
    }

    /// <summary>
    /// Stores a JSON entry with the current time; returns <c>false</c> if the cache cannot store it.
    /// </summary>
    public async Task<bool> WriteAsync<T>(string name, T value, CancellationToken cancellationToken)
        where T : class
    {
        if (!store.IsAvailable)
        {
            return false;
        }

        string temporary = store.CreateTemporaryPath(".json");
        try
        {
            await using (FileStream file = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(
                    file,
                    new Entry<T>(timeProvider.GetUtcNow(), value),
                    _jsonOptions,
                    cancellationToken);
            }

            return store.Commit(CacheArea.ArtistLookups, name, temporary);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(exception, "Cannot store {Name} of the artist lookups in the cache", name);
            store.DeleteTemporary(temporary);
            return false;
        }
    }

    /// <summary>
    /// Opens a stored file, e.g. a preview; <c>null</c> if there is none.
    /// </summary>
    public Stream? Open(string name)
    {
        return store.TryOpen(CacheArea.ArtistLookups, name);
    }

    /// <summary>
    /// A path for an unfinished file that <see cref="Commit"/> moves into the area.
    /// </summary>
    public string CreateTemporaryPath(string extension)
    {
        return store.CreateTemporaryPath(extension);
    }

    public bool Commit(string name, string temporaryPath)
    {
        return store.Commit(CacheArea.ArtistLookups, name, temporaryPath);
    }

    public void DeleteTemporary(string temporaryPath)
    {
        store.DeleteTemporary(temporaryPath);
    }

    /// <summary>
    /// Whether something fetched at that time is too old to be used.
    /// </summary>
    public bool IsExpired(DateTimeOffset fetchedAt)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        return fetchedAt > now || now - fetchedAt >= options.Value.LookupCacheDuration;
    }

    /// <summary>
    /// A JSON entry: the value and when it was fetched.
    /// </summary>
    private sealed record Entry<T>(DateTimeOffset FetchedAt, T Value);
}
