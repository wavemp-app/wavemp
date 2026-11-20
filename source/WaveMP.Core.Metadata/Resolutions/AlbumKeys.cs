using System.Security.Cryptography;
using System.Text;
using WaveMP.Core.Metadata.Texts;

namespace WaveMP.Core.Metadata.Resolutions;

/// <summary>
/// The identity of an album: its artist (the album artist, otherwise the artist of the track) and its title, both
/// compared without regard to case and to white space at the start, at the end and in a row, hashed with SHA-256 to a
/// short key of fixed length for the unique index. Folders do not define albums. Only albums without any artist are
/// kept apart per folder (the parameter <c>folder</c> of <see cref="Create"/>), since nothing else tells unrelated ones
/// apart.
/// </summary>
public static class AlbumKeys
{
    /// <param name="title">Title of the album.</param>
    /// <param name="artist">The album artist, otherwise the artist of the track; <c>null</c> if neither is
    /// known.</param>
    /// <param name="folder">Identifies the folder of an album without artist (e.g. source and path of the album
    /// folder); ignored for albums with an artist.</param>
    public static string Create(string title, string? artist, string? folder = null)
    {
        string normalizedTitle = MetadataText.ToKey(title) ?? string.Empty;
        string owner = MetadataText.ToKey(artist) is { } key
            ? "artist:" + key
            : "folder:" + (folder ?? string.Empty);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(owner + "|" + normalizedTitle));
        return Convert.ToHexStringLower(hash);
    }
}
