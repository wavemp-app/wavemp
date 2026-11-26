using WaveMP.Data.Entities;

namespace WaveMP.Core.Metadata.Resolutions;

/// <summary>
/// The metadata of a track, field by field from the tags, the folder and file name or the file name itself.
/// </summary>
/// <param name="Title">Title of the track.</param>
/// <param name="TitleSource">Where the title comes from.</param>
/// <param name="Artist">Artist of the track.</param>
/// <param name="AlbumArtist">Album artist; only from the tags if <paramref name="IsCompilation"/> is set.</param>
/// <param name="Album">Title of the album.</param>
/// <param name="TrackNumber">Number of the track.</param>
/// <param name="DiscNumber">Number of the disc.</param>
/// <param name="Year">Year of the track.</param>
/// <param name="Genre">Genre of the track.</param>
/// <param name="IsCompilation">Whether the tags mark the track as part of a compilation, which changes how its album
/// is formed (see <see cref="AlbumResolver"/>).</param>
/// <param name="MusicBrainzArtistId">The MusicBrainz artist of <paramref name="Artist"/>, if its tag names exactly
/// one.</param>
/// <param name="MusicBrainzAlbumArtistId">The MusicBrainz artist of <paramref name="AlbumArtist"/>, if its tag names
/// exactly one.</param>
/// <param name="MusicBrainzReleaseId">The MusicBrainz release of <paramref name="Album"/>.</param>
public sealed record ResolvedTrackMetadata(
    string Title,
    TrackMetadataSource TitleSource,
    string? Artist,
    string? AlbumArtist,
    string? Album,
    int? TrackNumber,
    int? DiscNumber,
    int? Year,
    string? Genre,
    bool IsCompilation = false,
    Guid? MusicBrainzArtistId = null,
    Guid? MusicBrainzAlbumArtistId = null,
    Guid? MusicBrainzReleaseId = null);
