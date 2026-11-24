namespace WaveMP.Data.Repositories;

/// <summary>
/// An artist as the carry-over of <see cref="ArtistSuccessionPlanner"/> sees it: whether it still has songs (as track
/// artist or as album artist of an album; an artist without is left empty and deleted), its first seen date, its
/// uploaded and its online picture and its MusicBrainz artist, if any.
/// </summary>
internal sealed record ArtistSuccessionArtist(
    Guid Id,
    bool HasSongs,
    DateTimeOffset FirstSeenAt,
    Guid? UploadedPictureId = null,
    Guid? OnlinePictureId = null,
    Guid? MusicBrainzArtistId = null);
