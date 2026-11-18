namespace WaveMP.Core.Subsonic.NowPlaying;

/// <summary>
/// What the users are playing right now, for <c>getNowPlaying</c>: Subsonic apps report it with <c>scrobble</c>
/// (<c>submission=false</c>), the web app with its "Now Playing" report. Kept in memory only; a restart forgets it.
/// </summary>
public interface ISubsonicNowPlayingRegistry
{
    /// <summary>
    /// Remembers the song a user started on a player; it replaces the earlier song of the same user and player.
    /// </summary>
    void Report(Guid userId, Guid trackId, string? playerName);

    /// <summary>
    /// The songs reported within <see cref="SubsonicNowPlayingRegistry.MaxAge"/>, the latest first.
    /// </summary>
    IReadOnlyList<SubsonicNowPlayingEntry> List();
}
