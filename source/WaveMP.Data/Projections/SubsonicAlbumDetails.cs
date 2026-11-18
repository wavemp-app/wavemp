namespace WaveMP.Data.Projections;

/// <summary>
/// What the Subsonic API shows of an album beyond <see cref="AlbumRow"/>: what the user did with it.
/// </summary>
/// <param name="StarredAt">When the user marked the album as favorite; <c>null</c> if it is no favorite.</param>
/// <param name="PlayCount">How often the user played tracks of the album.</param>
/// <param name="LastPlayedAt">When the user played a track of the album the last time; <c>null</c> if never.</param>
public sealed record SubsonicAlbumDetails(DateTimeOffset? StarredAt, int PlayCount, DateTimeOffset? LastPlayedAt);
