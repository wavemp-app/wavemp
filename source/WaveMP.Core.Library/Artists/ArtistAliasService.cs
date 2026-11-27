using WaveMP.Core.Metadata.Texts;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Artists;

internal sealed class ArtistAliasService(
    IArtistRepository artists,
    IArtistAliasRepository aliases,
    TimeProvider timeProvider) : IArtistAliasService
{
    public async Task<Page<ArtistAliasRow>?> ListAsync(
        Guid artistId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!await artists.ExistsAsync(artistId, cancellationToken))
        {
            return null;
        }

        return await aliases.ListAsync(artistId, offset, limit, cancellationToken);
    }

    public Task<ArtistAliasRow?> GetAsync(Guid artistId, Guid aliasId, CancellationToken cancellationToken = default)
    {
        return aliases.FindAsync(artistId, aliasId, cancellationToken);
    }

    public Task<ArtistAliasAddResult> AddAsync(
        Guid artistId,
        string? name,
        CancellationToken cancellationToken = default)
    {
        string? cleaned = Clean(name);
        return cleaned is null
            ? Task.FromResult(new ArtistAliasAddResult(ArtistAliasAddStatus.Empty))
            : aliases.AddAsync(artistId, cleaned, timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task<bool> RemoveAsync(Guid artistId, Guid aliasId, CancellationToken cancellationToken = default)
    {
        return aliases.RemoveAsync(artistId, aliasId, cancellationToken);
    }

    /// <summary>
    /// The name cleaned like names from tags and cut to <see cref="IArtistAliasService.MaxNameLength"/>; <c>null</c>
    /// without content.
    /// </summary>
    internal static string? Clean(string? name)
    {
        string? cleaned = MetadataText.Normalize(name);
        if (cleaned is null || cleaned.Length <= IArtistAliasService.MaxNameLength)
        {
            return cleaned;
        }

        int length = char.IsHighSurrogate(cleaned[IArtistAliasService.MaxNameLength - 1])
            ? IArtistAliasService.MaxNameLength - 1
            : IArtistAliasService.MaxNameLength;
        return cleaned[..length].TrimEnd();
    }
}
