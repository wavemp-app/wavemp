namespace WaveMP.Core.OnlineMetadata.Matches;

/// <summary>
/// The tags of a song as MusicBrainz knows them; <c>null</c> where it has no value.
/// </summary>
public sealed record OnlineTrackValues(
    string? Title,
    string? Artist,
    string? Album,
    string? AlbumArtist,
    int? Year,
    int? TrackNumber,
    int? DiscNumber,
    string? Genre);

/// <summary>
/// The tags of an album as MusicBrainz knows them; <c>null</c> where it has no value.
/// </summary>
public sealed record OnlineAlbumValues(string? Album, string? AlbumArtist, int? Year, string? Genre);

/// <summary>
/// A release of MusicBrainz with what tells it apart from other releases of the same album.
/// </summary>
/// <param name="Id">The MusicBrainz id (MBID) of the release; its cover comes from the Cover Art Archive.</param>
/// <param name="Date">Date of the release: <c>YYYY</c>, <c>YYYY-MM</c> or <c>YYYY-MM-DD</c>.</param>
/// <param name="Country">Country of the release, e.g. <c>GB</c> or <c>XW</c> (worldwide).</param>
/// <param name="Format">Format of its first medium, e.g. <c>CD</c>.</param>
/// <param name="TrackCount">Number of tracks on all media.</param>
public sealed record OnlineRelease(Guid Id, string? Date, string? Country, string? Format, int? TrackCount);

/// <summary>
/// A recording of MusicBrainz on one of its releases that may be a song of the library.
/// </summary>
/// <param name="RecordingId">The MusicBrainz id of the recording.</param>
/// <param name="Release">The release; <c>null</c> for a recording without release (no album, no cover).</param>
/// <param name="Agreement">How well it agrees with the song, 0 to 100.</param>
/// <param name="Duration">Length of the recording, if known.</param>
/// <param name="Values">The tags it suggests.</param>
public sealed record TrackMatch(
    Guid RecordingId,
    OnlineRelease? Release,
    int Agreement,
    TimeSpan? Duration,
    OnlineTrackValues Values);

/// <summary>
/// A release of MusicBrainz that may be an album of the library.
/// </summary>
/// <param name="Release">The release.</param>
/// <param name="Agreement">How well it agrees with the album, 0 to 100.</param>
/// <param name="Values">The tags it suggests.</param>
public sealed record AlbumMatch(OnlineRelease Release, int Agreement, OnlineAlbumValues Values);

/// <summary>
/// What a song of the library is searched by.
/// </summary>
public sealed record TrackLookup(string Title, string? Artist, string? Album, TimeSpan? Duration);

/// <summary>
/// What an album of the library is searched by.
/// </summary>
public sealed record AlbumLookup(string Album, string? Artist, int TrackCount);
