using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Texts;

namespace WaveMP.Data.Repositories;

internal sealed class AutoplayRepository(WaveMpDbContext dbContext) : IAutoplayRepository
{
    /// <summary>
    /// Rating that keeps a track out of autoplay.
    /// </summary>
    private const int NeverRating = 1;

    public async Task<IReadOnlyList<AutoplaySeedRow>> ListSeedsAsync(
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default)
    {
        if (trackIds.Count == 0)
        {
            return [];
        }

        Guid[] distinct = [.. trackIds.Distinct()];
        return await dbContext.Tracks.AsNoTracking()
            .Where(track => distinct.Contains(track.Id))
            .Select(track => new AutoplaySeedRow(
                track.Id,
                track.ArtistCredits.OrderBy(credit => credit.Position).Select(credit => credit.ArtistId).ToList(),
                track.Genre != null ? track.Genre.Name : null,
                track.Year))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AutoplayCandidateRow>> ListCandidatesAsync(
        AutoplayCandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Limit);
        Guid userId = query.UserId;
        Guid[] excluded = [.. query.ExcludedTrackIds.Distinct()];
        IQueryable<Track> tracks = dbContext.Tracks.AsNoTracking()
            .Where(track => track.HasAudio || (query.IncludeVideoOnly && track.HasVideo))
            .Where(track => !excluded.Contains(track.Id))
            .Where(track => !dbContext.TrackRatings.Any(rating =>
                rating.UserId == userId && rating.TrackId == track.Id && rating.Rating <= NeverRating));

        if (query.ArtistIds is { } artistIds)
        {
            Guid[] artists = [.. artistIds.Distinct()];
            tracks = tracks.Where(track => track.ArtistCredits.Any(credit => artists.Contains(credit.ArtistId)));
        }
        else
        {
            if (query.Genres is { } genres)
            {
                string[] keys = [.. genres.Select(NameKeys.Of).OfType<string>().Distinct()];
                tracks = tracks.Where(track => track.Genre != null && keys.Contains(track.Genre.NormalizedName));
            }

            if (query.Decades is { } decades)
            {
                int[] firstYears = [.. decades.Distinct()];
                tracks = tracks.Where(track => track.Year != null && firstYears.Contains(track.Year.Value / 10 * 10));
            }
        }

        return await tracks
            .OrderBy(_ => EF.Functions.Random())
            .Take(query.Limit)
            .Select(track => new AutoplayCandidateRow(
                track.Id,
                track.ArtistCredits.OrderBy(credit => credit.Position).Select(credit => credit.ArtistId).ToList(),
                track.Genre != null ? track.Genre.Name : null,
                track.Year,
                track.HasAudio,
                dbContext.Favorites.Any(favorite => favorite.UserId == userId && favorite.TrackId == track.Id),
                dbContext.TrackRatings
                    .Where(rating => rating.UserId == userId && rating.TrackId == track.Id)
                    .Select(rating => rating.Rating)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ListRecentPlaysAsync(
        Guid userId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PlayHistory.AsNoTracking()
            .Where(entry => entry.UserId == userId && entry.PlayedAt >= since)
            .GroupBy(entry => entry.TrackId)
            .Select(group => new { TrackId = group.Key, LastPlayedAt = group.Max(entry => entry.PlayedAt) })
            .ToDictionaryAsync(row => row.TrackId, row => row.LastPlayedAt, cancellationToken);
    }
}
