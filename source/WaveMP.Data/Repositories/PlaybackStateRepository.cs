using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Extensions;

namespace WaveMP.Data.Repositories;

internal sealed class PlaybackStateRepository(WaveMpDbContext dbContext) : IPlaybackStateRepository
{
    public Task<PlaybackState?> FindAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.PlaybackStates.AsNoTracking()
            .FirstOrDefaultAsync(state => state.UserId == userId, cancellationToken);
    }

    public Task<bool> SaveAsync(PlaybackState state, CancellationToken cancellationToken = default)
    {
        Guid[] trackIds = [.. state.TrackIds];
        int[] autoplayPositions = [.. state.AutoplayPositions];
        int[] originalOrder = [.. state.OriginalOrder];
        string repeat = state.Repeat.ToString();
        string? sourceKind = state.SourceKind?.ToString();
        DateTimeOffset changedAt = state.ChangedAt.ToUniversalTime();
        return dbContext.InUserTransactionAsync(
            state.UserId,
            async token =>
            {
                // One statement, so two devices saving at the same time cannot both win: the row is only replaced
                // by a state that changed at the same time or later.
                int saved = await dbContext.Database.ExecuteSqlAsync(
                    $"""
                    INSERT INTO playback_states (user_id, track_ids, autoplay_positions, current_index,
                        position_milliseconds, shuffle, original_order, repeat, source_kind, source_id, device_id,
                        device_name, changed_at)
                    VALUES ({state.UserId}, {trackIds}, {autoplayPositions}, {state.CurrentIndex},
                        {state.PositionMilliseconds}, {state.Shuffle}, {originalOrder}, {repeat}, {sourceKind},
                        {state.SourceId}, {state.DeviceId}, {state.DeviceName}, {changedAt})
                    ON CONFLICT (user_id) DO UPDATE
                    SET track_ids = EXCLUDED.track_ids, autoplay_positions = EXCLUDED.autoplay_positions,
                        current_index = EXCLUDED.current_index, position_milliseconds = EXCLUDED.position_milliseconds,
                        shuffle = EXCLUDED.shuffle, original_order = EXCLUDED.original_order,
                        repeat = EXCLUDED.repeat, source_kind = EXCLUDED.source_kind, source_id = EXCLUDED.source_id,
                        device_id = EXCLUDED.device_id, device_name = EXCLUDED.device_name,
                        changed_at = EXCLUDED.changed_at
                    WHERE playback_states.changed_at <= EXCLUDED.changed_at
                    """,
                    token);
                return saved > 0;
            },
            cancellationToken);
    }
}
