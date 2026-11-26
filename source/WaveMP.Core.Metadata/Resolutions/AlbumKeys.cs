using System.Security.Cryptography;
using System.Text;
using WaveMP.Data.Texts;

namespace WaveMP.Core.Metadata.Resolutions;

/// <summary>
/// The identity of an album: its artist (the album artist, otherwise the artist of the track) and its title, both
/// compared by their normalized names (see <see cref="NameKeys"/>: without regard to case, diacritics, spaces and
/// punctuation), hashed with SHA-256 to a short key of fixed length for the unique index. Folders do not define
/// albums with an artist. Albums without artist (tracks without any artist, and tracks with the compilation flag but
/// without album artist) are kept apart per folder (the parameter <c>folder</c> of <see cref="Create"/>), since
/// nothing else tells unrelated ones apart.
/// </summary>
public static class AlbumKeys
{
    /// <param name="title">Title of the album.</param>
    /// <param name="artist">The artist of the album (see <see cref="AlbumResolver.ArtistOf"/>); <c>null</c> for albums
    /// that are identified by their folder.</param>
    /// <param name="folder">Identifies the folder of an album without artist (e.g. source and path of the album
    /// folder); ignored for albums with an artist.</param>
    public static string Create(string title, string? artist, string? folder = null)
    {
        string normalizedTitle = NameKeys.Of(title) ?? string.Empty;
        string owner = NameKeys.Of(artist) is { } key
            ? "artist:" + key
            : "folder:" + (folder ?? string.Empty);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(owner + "|" + normalizedTitle));
        return Convert.ToHexStringLower(hash);
    }
}
