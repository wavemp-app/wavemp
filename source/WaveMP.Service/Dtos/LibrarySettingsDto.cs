namespace WaveMP.Service.Dtos;

/// <summary>
/// Settings of the media library.
/// </summary>
public sealed class LibrarySettingsDto
{
    /// <summary>
    /// Minutes between two periodic scans (15 to 1440). The periodic scan always runs.
    /// </summary>
    public required int ScanIntervalMinutes { get; init; }

    /// <summary>
    /// Watches the media folder for changes and scans changed folders shortly after.
    /// </summary>
    public required bool FileWatcherEnabled { get; init; }

    /// <summary>
    /// Extensions of audio files, lower case without dot.
    /// </summary>
    public required IReadOnlyList<string> AudioExtensions { get; init; }

    /// <summary>
    /// Extensions of video files, lower case without dot.
    /// </summary>
    public required IReadOnlyList<string> VideoExtensions { get; init; }

    /// <summary>
    /// Extensions of pictures (covers), lower case without dot.
    /// </summary>
    public required IReadOnlyList<string> ImageExtensions { get; init; }

    /// <summary>
    /// Base names of pictures that are album covers, in the order of their priority.
    /// </summary>
    public required IReadOnlyList<string> AlbumCoverNames { get; init; }

    /// <summary>
    /// Base names of pictures that are artist pictures in the folder named after the artist, in the order of their
    /// priority (default artist, folder, photo).
    /// </summary>
    public required IReadOnlyList<string> ArtistPictureNames { get; init; }

    /// <summary>
    /// The global cover rule of the songs: own cover, else album cover (TrackFirst, the default); album cover, else own
    /// cover (AlbumFirst); or only the own cover (TrackOnly). Songs and albums with an exception (GetTrackCoverRule,
    /// GetAlbumCoverRule) override it.
    /// </summary>
    public required CoverPriorityDto CoverPriority { get; init; }

    /// <summary>
    /// What albums without a cover of their own show: the picture of their album artist (ArtistPicture, the default)
    /// or nothing (None). Albums with an exception (GetAlbumCoverFallback) override it.
    /// </summary>
    public required AlbumCoverFallbackDto AlbumCoverFallback { get; init; }

    /// <summary>
    /// Downloads the lyrics of tracks without lyrics from the online lyrics database LRCLIB (lrclib.net): new tracks
    /// after scans, all tracks when it is switched on, and single tracks on request (DownloadTrackLyrics). Off by
    /// default, because titles, artists and albums of the library are sent to the service.
    /// </summary>
    public required bool DownloadLyrics { get; init; }

    /// <summary>
    /// Whether Subsonic apps may download the original files of the tracks (endpoint download of the Subsonic API under
    /// /rest). Off by default; streaming is always allowed.
    /// </summary>
    public required bool SubsonicDownloads { get; init; }
}
