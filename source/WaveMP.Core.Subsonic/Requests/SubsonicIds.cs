namespace WaveMP.Core.Subsonic.Requests;

/// <summary>
/// The ids of the Subsonic API. Artists, albums, songs and playlists keep the ids of WaveMP. Cover ids carry the kind
/// of their owner as prefix (<c>al-</c> album, <c>tr-</c> track, <c>pl-</c> playlist), so <c>getCoverArt</c> knows
/// where to look; a plain id is looked up as track, album and playlist in turn.
/// </summary>
public static class SubsonicIds
{
    /// <summary>
    /// The one music folder of WaveMP: the media folder.
    /// </summary>
    public const int MusicFolderId = 1;

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
