using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Extensions;

namespace WaveMP.Data.Repositories;

internal sealed class TrackRatingRepository(WaveMpDbContext dbContext) : ITrackRatingRepository
{
    public Task<bool> SetAsync(
        Guid userId,
        Guid trackId,
        int rating,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset updatedAt = now.ToUniversalTime();
        return dbContext.InUserTransactionAsync(
            userId,
            async token =>
            {
                // The lock keeps a scan from removing the track until the rating is saved.
                if (!await dbContext.LockTrackAsync(trackId, token))
                {
                    return false;
                }

                await dbContext.Database.ExecuteSqlAsync(
                    $"""
                    INSERT INTO track_ratings (user_id, track_id, rating, updated_at)
                    VALUES ({userId}, {trackId}, {rating}, {updatedAt})
                    ON CONFLICT (user_id, track_id) DO UPDATE
                    SET rating = EXCLUDED.rating, updated_at = EXCLUDED.updated_at
                    WHERE track_ratings.rating <> EXCLUDED.rating
                    """,
                    token);
                return true;
            },
            cancellationToken);
    }

    public async Task<bool> RemoveAsync(Guid userId, Guid trackId, CancellationToken cancellationToken = default)
    {
        if (!await TrackExistsAsync(trackId, cancellationToken))
        {
            return false;
        }

        await dbContext.TrackRatings
            .Where(rating => rating.UserId == userId && rating.TrackId == trackId)
            .ExecuteDeleteAsync(cancellationToken);
        return true;
    }

    private Task<bool> TrackExistsAsync(Guid trackId, CancellationToken cancellationToken)
    {
        return dbContext.Tracks.AnyAsync(track => track.Id == trackId, cancellationToken);
    }
}
