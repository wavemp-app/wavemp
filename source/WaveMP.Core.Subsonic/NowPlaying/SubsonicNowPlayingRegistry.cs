using System.Collections.Concurrent;

namespace WaveMP.Core.Subsonic.NowPlaying;

internal sealed class SubsonicNowPlayingRegistry(TimeProvider timeProvider) : ISubsonicNowPlayingRegistry
{
    /// <summary>
    /// Songs reported longer ago are forgotten; <c>getNowPlaying</c> also leaves out songs that have ended.
    /// </summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<(Guid UserId, string Player), SubsonicNowPlayingEntry> _entries = new();

    public void Report(Guid userId, Guid trackId, string? playerName)
    {
        string? player = string.IsNullOrWhiteSpace(playerName) ? null : playerName.Trim();
        _entries[(userId, player ?? string.Empty)] =
            new SubsonicNowPlayingEntry(userId, trackId, player, timeProvider.GetUtcNow());
        RemoveExpired();
    }

    public IReadOnlyList<SubsonicNowPlayingEntry> List()
    {
        RemoveExpired();
        return [.. _entries.Values.OrderByDescending(entry => entry.StartedAt)];
    }

    private void RemoveExpired()
    {
        DateTimeOffset oldest = timeProvider.GetUtcNow() - MaxAge;
        foreach (KeyValuePair<(Guid UserId, string Player), SubsonicNowPlayingEntry> entry in _entries)
        {
            if (entry.Value.StartedAt < oldest)
            {
                _entries.TryRemove(entry);
            }
        }
    }
}
