using Microsoft.EntityFrameworkCore;
using Npgsql;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

internal sealed class ArtistPictureRepository(WaveMpDbContext dbContext) : IArtistPictureRepository
{
    /// <summary>
    /// Attempts to store a picture when another one of the same kind is stored at the same moment: the later one
    /// removes the earlier one and tries again.
    /// </summary>
    private const int MaxAttempts = 3;

    public Task<ArtistPictureSources?> FindSourcesAsync(
        Guid artistId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Artists.AsNoTracking()
            .Where(artist => artist.Id == artistId)
            .Select(artist => new ArtistPictureSources(
                artist.Id,
                artist.LocalPictureFile == null
                    ? null
                    : new ArtistLocalPictureRow(
                        artist.LocalPictureFile.Id,
                        artist.LocalPictureFile.SourceId,
                        artist.LocalPictureFile.RelativePath,
                        artist.LocalPictureFile.Size,
                        artist.LocalPictureFile.ModifiedAt),
                artist.Pictures
                    .Where(picture => picture.Kind == ArtistPictureKind.Uploaded)
                    .Select(picture => new ArtistStoredPictureRow(picture.Id, picture.CreatedAt))
                    .FirstOrDefault(),
                artist.Pictures
                    .Where(picture => picture.Kind == ArtistPictureKind.Online)
                    .Select(picture => new ArtistStoredPictureRow(picture.Id, picture.CreatedAt))
                    .FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ArtistPictureReplacement> ReplaceAsync(
        ArtistPicture picture,
        CancellationToken cancellationToken = default)
    {
        List<Guid> replaced = [];
        for (int attempt = 1; ; attempt++)
        {
            IQueryable<ArtistPicture> earlier = dbContext.ArtistPictures
                .Where(stored => stored.ArtistId == picture.ArtistId && stored.Kind == picture.Kind);
            List<Guid> earlierIds = await earlier.Select(stored => stored.Id).ToListAsync(cancellationToken);
            if (earlierIds.Count > 0)
            {
                await earlier.Where(stored => earlierIds.Contains(stored.Id)).ExecuteDeleteAsync(cancellationToken);
                replaced.AddRange(earlierIds);
            }

            dbContext.ArtistPictures.Add(picture);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return new ArtistPictureReplacement(true, replaced);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.ForeignKeyViolation,
            })
            {
                // The artist was removed in the meantime.
                return new ArtistPictureReplacement(false, replaced);
            }
            catch (DbUpdateException exception) when (attempt < MaxAttempts
                && exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Another picture of the kind was stored at the same moment; this one replaces it.
            }
            finally
            {
                dbContext.Entry(picture).State = EntityState.Detached;
            }
        }
    }

    public async Task<ArtistPictureRemoval?> RemoveAsync(
        Guid artistId,
        ArtistPictureKind kind,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Artists.AnyAsync(artist => artist.Id == artistId, cancellationToken))
        {
            return null;
        }

        IQueryable<ArtistPicture> pictures = dbContext.ArtistPictures
            .Where(picture => picture.ArtistId == artistId && picture.Kind == kind);
        Guid? removed = await pictures.Select(picture => (Guid?)picture.Id).FirstOrDefaultAsync(cancellationToken);
        if (removed is not null)
        {
            await pictures.ExecuteDeleteAsync(cancellationToken);
        }

        return new ArtistPictureRemoval(removed);
    }

    public async Task<IReadOnlySet<Guid>> ListExistingIdsAsync(
        IReadOnlyCollection<Guid> pictureIds,
        CancellationToken cancellationToken = default)
    {
        if (pictureIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        Guid[] ids = [.. pictureIds.Distinct()];
        List<Guid> existing = await dbContext.ArtistPictures.AsNoTracking()
            .Where(picture => ids.Contains(picture.Id))
            .Select(picture => picture.Id)
            .ToListAsync(cancellationToken);
        return existing.ToHashSet();
    }
}
