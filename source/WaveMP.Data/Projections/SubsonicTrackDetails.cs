namespace WaveMP.Data.Projections;

/// <summary>
/// What the Subsonic API shows of a track beyond <see cref="TrackRow"/>: its file and what the user did with it.
/// </summary>
/// <param name="File">The file whose sound the track plays; <c>null</c> if the track has none.</param>
/// <param name="StarredAt">When the user marked the track as favorite; <c>null</c> if it is no favorite.</param>
/// <param name="PlayCount">How often the user played the track.</param>
/// <param name="LastPlayedAt">When the user played the track the last time; <c>null</c> if never.</param>
public sealed record SubsonicTrackDetails(
    SubsonicFileRow? File,
    DateTimeOffset? StarredAt,
    int PlayCount,
    DateTimeOffset? LastPlayedAt);
