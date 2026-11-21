using System.Buffers.Binary;
using System.Security.Cryptography;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Subsonic.Requests;

/// <summary>
/// The ids of the Subsonic API. Artists, albums, songs and playlists keep the ids of WaveMP. Cover ids carry the kind
/// of their owner as prefix (<c>al-</c> album, <c>tr-</c> track, <c>pl-</c> playlist), so <c>getCoverArt</c> knows
/// where to look; a plain id is looked up as track, album and playlist in turn. Music folders are the media sources;
/// their ids are whole numbers, as the specification asks, derived from the id of the source.
/// </summary>
public static class SubsonicIds
{
    /// <summary>
    /// The music folder of the first media source (<see cref="MediaSource.DefaultId"/>). It keeps the id of the one
    /// music folder of earlier versions, so apps that remember it still find it.
    /// </summary>
    public const int DefaultMusicFolderId = 1;

    /// <summary>
    /// The id of the music folder of a media source: <see cref="DefaultMusicFolderId"/> for the first source,
    /// otherwise a positive number from a hash of the id of the source, the same on every start.
    /// </summary>
    public static int MusicFolder(Guid sourceId)
    {
        if (sourceId == MediaSource.DefaultId)
        {
            return DefaultMusicFolderId;
        }

        Span<byte> bytes = stackalloc byte[16];
        sourceId.TryWriteBytes(bytes, bigEndian: true, out _);
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(bytes, hash);
        int value = BinaryPrimitives.ReadInt32BigEndian(hash) & int.MaxValue;

        // 0 and 1 stay free: 1 belongs to the first source, and some apps take 0 for "no folder".
        return value <= DefaultMusicFolderId ? value + 2 : value;
    }

    public const string AlbumCoverPrefix = "al-";
    public const string TrackCoverPrefix = "tr-";
    public const string PlaylistCoverPrefix = "pl-";

    public static string Format(Guid id)
    {
        return id.ToString("D");
    }

    /// <summary>
    /// The id of WaveMP in the value; <c>null</c> if it is none.
    /// </summary>
    public static Guid? Parse(string? value)
    {
        return Guid.TryParse(value?.Trim(), out Guid id) ? id : null;
    }

    public static string AlbumCover(Guid albumId)
    {
        return AlbumCoverPrefix + Format(albumId);
    }

    public static string TrackCover(Guid trackId)
    {
        return TrackCoverPrefix + Format(trackId);
    }

    public static string PlaylistCover(Guid playlistId)
    {
        return PlaylistCoverPrefix + Format(playlistId);
    }

    /// <summary>
    /// The owner of a cover id; <c>null</c> if the value is no cover id.
    /// </summary>
    public static SubsonicCoverId? ParseCover(string? value)
    {
        string text = value?.Trim() ?? string.Empty;
        foreach ((string prefix, SubsonicCoverOwner owner) in new[]
        {
            (AlbumCoverPrefix, SubsonicCoverOwner.Album),
            (TrackCoverPrefix, SubsonicCoverOwner.Track),
            (PlaylistCoverPrefix, SubsonicCoverOwner.Playlist),
        })
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return Parse(text[prefix.Length..]) is { } id ? new SubsonicCoverId(owner, id) : null;
            }
        }

        return Parse(text) is { } plain ? new SubsonicCoverId(SubsonicCoverOwner.Any, plain) : null;
    }
}
