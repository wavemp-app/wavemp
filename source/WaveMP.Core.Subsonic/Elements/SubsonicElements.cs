using WaveMP.Core.Playlists.Playlists;
using WaveMP.Core.Subsonic.Media;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Core.Subsonic.Responses;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Subsonic.Elements;

/// <summary>
/// Turns the rows of the library into the elements of the Subsonic API (<c>Child</c>, <c>AlbumID3</c>,
/// <c>ArtistID3</c>, <c>Playlist</c>) with the extended fields of OpenSubsonic. Values WaveMP does not know are left
/// out; WaveMP adds no texts of its own (e.g. no name for unknown artists).
/// </summary>
public static class SubsonicElements
{
    /// <summary>
    /// A song (<c>Child</c>), e.g. as <c>song</c> or <c>entry</c>. A track that only has a video plays the sound of
    /// the video, transcoded; it is a song like the others.
    /// </summary>
    public static SubsonicElement Song(TrackRow track, SubsonicTrackDetails? details, string name = "song")
    {
        SubsonicFileRow? file = details?.File;
        SubsonicElement song = new SubsonicElement(name)
            .Set("id", SubsonicIds.Format(track.Id))
            .Set("parent", track.AlbumId is { } albumId ? SubsonicIds.Format(albumId) : null)
            .Set("isDir", false)
            .Set("title", track.Title)
            .Set("album", track.AlbumTitle)
            .Set("artist", track.ArtistName)
            .Set("track", track.TrackNumber)
            .Set("year", track.Year)
            .Set("genre", track.Genre)
            .Set("coverArt", track.HasCover ? SubsonicIds.TrackCover(track.Id) : null);
        if (file is not null)
        {
            song.Set("size", file.Size)
                .Set("contentType", SubsonicContentTypes.Of(file.Extension))
                .Set("suffix", file.Extension);
            if (file.Kind == MediaFileKind.Video)
            {
                SubsonicTranscodeFormat format = SubsonicTranscodeFormats.Default;
                song.Set("transcodedContentType", SubsonicTranscodeFormats.ContentTypeOf(format))
                    .Set("transcodedSuffix", SubsonicTranscodeFormats.SuffixOf(format));
            }
        }

        song.Set("duration", Seconds(track.Duration))
            .Set("bitRate", file?.Bitrate is { } bitrate ? (int)Math.Round(bitrate / 1000d) : null)
            .Set("bitDepth", file?.BitDepth)
            .Set("samplingRate", file?.SampleRate)
            .Set("channelCount", file?.Channels)
            .Set("path", file?.RelativePath)
            .Set("isVideo", false)
            .Set("userRating", track.Rating > 0 ? track.Rating : null)
            .Set("starred", details?.StarredAt)
            .Set("discNumber", track.DiscNumber)
            .Set("created", track.FirstSeenAt)
            .Set("albumId", track.AlbumId is { } album ? SubsonicIds.Format(album) : null)
            .Set("artistId", track.ArtistId is { } artist ? SubsonicIds.Format(artist) : null)
            .Set("type", "music")
            .Set("mediaType", "song")
            .Set("playCount", (long)(details?.PlayCount ?? 0))
            .Set("played", details?.LastPlayedAt)
            .Set("displayArtist", track.ArtistName)
            .Set("displayAlbumArtist", track.AlbumArtistName);
        song.AddList("genres", Genres(track.Genre));
        song.AddList("artists", References("artists", track.ArtistId, track.ArtistName));
        song.AddList("albumArtists", References("albumArtists", track.AlbumArtistId, track.AlbumArtistName));
        return song;
    }

    /// <summary>
    /// An album with its tags (<c>AlbumID3</c>). Compilations and albums without a known artist have no artist.
    /// </summary>
    public static SubsonicElement Album(AlbumRow album, SubsonicAlbumDetails? details, string name = "album")
    {
        SubsonicElement element = new SubsonicElement(name)
            .Set("id", SubsonicIds.Format(album.Id))
            .Set("name", album.Title)
            .Set("artist", album.ArtistName)
            .Set("artistId", album.ArtistId is { } artistId ? SubsonicIds.Format(artistId) : null)
            .Set("coverArt", album.HasCover ? SubsonicIds.AlbumCover(album.Id) : null)
            .Set("songCount", album.TrackCount)
            .Set("duration", (int)Math.Round(album.DurationMilliseconds / 1000))
            .Set("playCount", (long)(details?.PlayCount ?? 0))
            .Set("created", album.FirstSeenAt)
            .Set("starred", details?.StarredAt)
            .Set("year", album.Year)
            .Set("genre", album.Genre)
            .Set("played", details?.LastPlayedAt)
            .Set("isCompilation", album.IsCompilation)
            .Set("displayArtist", album.ArtistName);
        element.AddList("genres", Genres(album.Genre));
        element.AddList("artists", References("artists", album.ArtistId, album.ArtistName));
        return element;
    }

    /// <summary>
    /// An album as a folder of <c>getMusicDirectory</c> (<c>Child</c> with <c>isDir</c>), below its artist.
    /// </summary>
    public static SubsonicElement AlbumFolder(AlbumRow album, SubsonicAlbumDetails? details, Guid? parentId)
    {
        return new SubsonicElement("child")
            .Set("id", SubsonicIds.Format(album.Id))
            .Set("parent", parentId is { } parent ? SubsonicIds.Format(parent) : null)
            .Set("isDir", true)
            .Set("title", album.Title)
            .Set("album", album.Title)
            .Set("artist", album.ArtistName)
            .Set("year", album.Year)
            .Set("genre", album.Genre)
            .Set("coverArt", album.HasCover ? SubsonicIds.AlbumCover(album.Id) : null)
            .Set("duration", (int)Math.Round(album.DurationMilliseconds / 1000))
            .Set("created", album.FirstSeenAt)
            .Set("starred", details?.StarredAt)
            .Set("playCount", (long)(details?.PlayCount ?? 0))
            .Set("played", details?.LastPlayedAt)
            .Set("albumId", SubsonicIds.Format(album.Id))
            .Set("artistId", album.ArtistId is { } artistId ? SubsonicIds.Format(artistId) : null)
            .Set("mediaType", "album");
    }

    /// <summary>
    /// An artist with its tags (<c>ArtistID3</c>), also the artists of the indexes.
    /// </summary>
    public static SubsonicElement Artist(ArtistRow artist, DateTimeOffset? starredAt, string name = "artist")
    {
        return new SubsonicElement(name)
            .Set("id", SubsonicIds.Format(artist.Id))
            .Set("name", artist.Name)
            .Set("albumCount", artist.AlbumCount)
            .Set("starred", starredAt);
    }

    /// <summary>
    /// A playlist of the user; playlists of WaveMP are never public.
    /// </summary>
    public static SubsonicElement Playlist(PlaylistItem item, string owner, string name = "playlist")
    {
        PlaylistRow playlist = item.Playlist;
        return new SubsonicElement(name)
            .Set("id", SubsonicIds.Format(playlist.Id))
            .Set("name", playlist.Name)
            .Set("owner", owner)
            .Set("public", false)
            .Set("songCount", playlist.TrackCount)
            .Set("duration", (int)Math.Round(playlist.DurationMilliseconds / 1000))
            .Set("created", playlist.CreatedAt)
            .Set("changed", playlist.UpdatedAt)
            .Set("coverArt", SubsonicIds.PlaylistCover(playlist.Id));
    }

    private static int? Seconds(TimeSpan? duration)
    {
        return duration is { } value ? (int)Math.Round(value.TotalSeconds) : null;
    }

    private static IEnumerable<SubsonicElement> Genres(string? genre)
    {
        return genre is null ? [] : [new SubsonicElement("genres").Set("name", genre)];
    }

    private static IEnumerable<SubsonicElement> References(string name, Guid? id, string? artistName)
    {
        return id is { } artistId && artistName is not null
            ? [new SubsonicElement(name).Set("id", SubsonicIds.Format(artistId)).Set("name", artistName)]
            : [];
    }
}
