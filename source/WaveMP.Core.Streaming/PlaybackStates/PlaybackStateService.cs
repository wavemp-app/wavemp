using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Streaming.PlaybackStates;

internal sealed class PlaybackStateService(
    IPlaybackStateRepository states,
    ITrackRepository tracks,
    TimeProvider timeProvider) : IPlaybackStateService
{
    public async Task<PlaybackStateView?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        PlaybackState? state = await states.FindAsync(userId, cancellationToken);
        if (state is null)
        {
            return null;
        }

        IReadOnlyList<TrackRow> rows = state.TrackIds.Count == 0
            ? []
            : await tracks.ListByIdsAsync([.. state.TrackIds.Distinct()], userId, cancellationToken);
        var byId = rows.ToDictionary(row => row.Id);
        RemainingQueue remaining = PlaybackStateRules.Remaining(
            state.TrackIds.Count,
            state.CurrentIndex,
            state.AutoplayPositions,
            state.Shuffle ? state.OriginalOrder : [],
            position => byId.ContainsKey(state.TrackIds[position]));

        HashSet<int> autoplay = [.. remaining.AutoplayPositions];
        List<PlaybackStateEntry> items = [.. remaining.Positions.Select(
            (position, index) => new PlaybackStateEntry(byId[state.TrackIds[position]], autoplay.Contains(index)))];
        return new PlaybackStateView(
            items,
            remaining.CurrentIndex,
            remaining.CurrentKept ? state.PositionMilliseconds : 0,
            state.Shuffle,
            remaining.OriginalOrder,
            state.Repeat,
            state.SourceKind is { } kind && state.SourceId is { } id ? new PlaybackSource(kind, id) : null,
            state.DeviceId,
            state.DeviceName,
            state.ChangedAt);
    }

    public async Task<PlaybackStateSaveStatus> SaveAsync(
        Guid userId,
        SavePlaybackStateCommand command,
        CancellationToken cancellationToken = default)
    {
        if (PlaybackStateRules.Validate(command) is { } invalid)
        {
            return invalid;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset changedAt = command.ChangedAt?.ToUniversalTime() is { } reported && reported < now
            ? reported
            : now;
        PlaybackState state = new()
        {
            UserId = userId,
            TrackIds = [.. command.Items.Select(item => item.TrackId)],
            AutoplayPositions = [.. command.Items
                .Select((item, position) => (item.Autoplay, position))
                .Where(entry => entry.Autoplay)
                .Select(entry => entry.position)],
            CurrentIndex = command.CurrentIndex,
            PositionMilliseconds = command.PositionMilliseconds,
            Shuffle = command.Shuffle,
            OriginalOrder = command.Shuffle ? [.. command.OriginalOrder] : [],
            Repeat = command.Repeat,
            SourceKind = command.Source?.Kind,
            SourceId = command.Source?.Id,
            DeviceId = command.DeviceId,
            DeviceName = command.DeviceName,
            ChangedAt = TruncateToMicroseconds(changedAt),
        };

        return await states.SaveAsync(state, cancellationToken)
            ? PlaybackStateSaveStatus.Saved
            : PlaybackStateSaveStatus.Outdated;
    }

    public Task<bool> ClearAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return states.DeleteAsync(userId, cancellationToken);
    }

    /// <summary>
    /// PostgreSQL keeps microseconds; the time is compared with the precision it is read with later.
    /// </summary>
    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value)
    {
        return new DateTimeOffset(value.Ticks - (value.Ticks % TimeSpan.TicksPerMicrosecond), value.Offset);
    }
}
