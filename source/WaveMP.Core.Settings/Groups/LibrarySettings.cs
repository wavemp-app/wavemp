using System.Text.Json.Serialization;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Settings.Groups;

/// <summary>
/// Settings of the media library: how often the media folder is scanned, whether changes are watched, which files
/// belong to the library, which cover a track shows and whether lyrics are downloaded.
/// Stored as the settings group <see cref="Key"/>; values missing in the stored JSON keep the defaults below.
/// Extensions are lower case without a dot.
/// </summary>
public sealed class LibrarySettings
{
    /// <summary>
    /// Key of the settings group.
    /// </summary>
    public const string Key = "library";

    /// <summary>
    /// Extension of lyrics files (<c>Song.lrc</c> next to <c>Song.mp3</c>). Always part of the library, so it cannot
    /// be one of the extensions below.
    /// </summary>
    public const string LyricsExtension = "lrc";

    /// <summary>
    /// Minutes between two periodic scans. The periodic scan always runs, also with the file watcher, because file
    /// system events are unreliable on network shares.
    /// </summary>
    public int ScanIntervalMinutes { get; init; } = 360;

    /// <summary>
    /// Watches the media folder for changes and scans changed folders shortly after.
    /// </summary>
    public bool FileWatcherEnabled { get; init; } = true;

    public IReadOnlyList<string> AudioExtensions { get; init; } =
    [
        "mp3", "flac", "m4a", "aac", "ogg", "oga", "opus", "wav",
        "wma", "aif", "aiff", "ape", "wv", "mka", "mpc", "dsf",
    ];

    public IReadOnlyList<string> VideoExtensions { get; init; } =
        ["mp4", "m4v", "mkv", "webm", "mov", "avi", "wmv", "mpg", "mpeg", "ts"];

    public IReadOnlyList<string> ImageExtensions { get; init; } = ["jpg", "jpeg", "png", "webp", "gif", "bmp"];

    /// <summary>
    /// Base names of pictures that are the cover of the album in their folder, in the order of their priority.
    /// Lower case; compared without regard to case.
    /// </summary>
    public IReadOnlyList<string> AlbumCoverNames { get; init; } = ["cover", "folder", "front", "album", "albumart"];

    /// <summary>
    /// Base names of pictures that are the picture of an artist in the artist's folder (the folder named after the
    /// artist, e.g. <c>Artist/artist.jpg</c>), in the order of their priority. Lower case; compared without regard to
    /// case.
    /// </summary>
    public IReadOnlyList<string> ArtistPictureNames { get; init; } = ["artist", "folder", "photo"];

    /// <summary>
    /// Which cover a song shows: its own cover, else the cover of its album (the default); the cover of its album,
    /// else its own; or only its own. Never the picture of an artist. Applies to all songs without an exception of
    /// their own or of their album. Stored as its name. Replaces the setting <c>coverPriority</c> of earlier versions,
    /// whose two values both showed a picture with the name of a song first, as the default does now; a stored value of
    /// it is no longer read.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<CoverPriority>))]
    public CoverPriority TrackCoverPriority { get; init; } = CoverPriority.TrackFirst;

    /// <summary>
    /// What an album shows without a cover of its own: the picture of its album artist (the default) or nothing.
    /// Never the cover of another album; the collage of album covers of an artist without a picture is no picture of
    /// the artist. Applies to all albums without an exception of their own. Stored as its name.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AlbumCoverFallback>))]
    public AlbumCoverFallback AlbumCoverFallback { get; init; } = AlbumCoverFallback.ArtistPicture;

    /// <summary>
    /// Downloads the lyrics of tracks without lyrics from the online lyrics database LRCLIB (lrclib.net): for new
    /// tracks after scans and when an admin asks for a track. Off by default, because it sends the titles, artists and
    /// albums of the library to the service.
    /// </summary>
    public bool DownloadLyrics { get; init; }

    /// <summary>
    /// Whether Subsonic apps may download the original files of the tracks (the endpoint <c>download</c> of the
    /// Subsonic API, e.g. to keep them on the device). Off by default; streaming is always allowed.
    /// </summary>
    public bool SubsonicDownloads { get; init; }
}
