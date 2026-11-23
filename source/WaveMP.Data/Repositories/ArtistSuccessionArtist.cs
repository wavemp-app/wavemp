namespace WaveMP.Data.Repositories;

/// <summary>
/// An artist as the carry-over of <see cref="ArtistSuccessionPlanner"/> sees it: whether it still has songs (as track
/// artist or as album artist of an album; an artist without is left empty and deleted) and its first seen date.
/// </summary>
internal sealed record ArtistSuccessionArtist(Guid Id, bool HasSongs, DateTimeOffset FirstSeenAt);
