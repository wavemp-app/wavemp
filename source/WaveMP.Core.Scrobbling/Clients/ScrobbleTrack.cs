namespace WaveMP.Core.Scrobbling.Clients;

/// <summary>
/// A song as the scrobbling services learn about it.
/// </summary>
/// <param name="Artist">Artist of the song; required by the services.</param>
/// <param name="Title">Title of the song.</param>
/// <param name="Album">Album, if known.</param>
/// <param name="AlbumArtist">Artist of the album, if known.</param>
/// <param name="TrackNumber">Number of the song on its album, if known.</param>
/// <param name="Duration">Length of the song, if known.</param>
public sealed record ScrobbleTrack(
    string Artist,
    string Title,
    string? Album = null,
    string? AlbumArtist = null,
    int? TrackNumber = null,
    TimeSpan? Duration = null);
