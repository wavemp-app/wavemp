namespace WaveMP.Testing.Media;

/// <summary>
/// Tags written into synthetic test media with <c>-metadata</c>. <c>null</c> values are not written.
/// </summary>
public sealed record TestTags
{
    public string? Title { get; init; }

    public string? Artist { get; init; }

    public string? Album { get; init; }

    public string? AlbumArtist { get; init; }

    public string? Track { get; init; }

    public string? Disc { get; init; }

    public string? Date { get; init; }

    public string? Genre { get; init; }

    /// <summary>
    /// ReplayGain gain of the track as written by taggers, for example <c>-6.50 dB</c>.
    /// </summary>
    public string? ReplayGainTrackGain { get; init; }

    /// <summary>
    /// ReplayGain peak of the track, for example <c>0.988525</c>.
    /// </summary>
    public string? ReplayGainTrackPeak { get; init; }

    public string? ReplayGainAlbumGain { get; init; }

    public string? ReplayGainAlbumPeak { get; init; }

    /// <summary>
    /// Compilation flag, for example <c>1</c>; ffmpeg writes it as ID3 <c>TCMP</c>, MP4 <c>cpil</c> or the Vorbis
    /// comment <c>compilation</c>.
    /// </summary>
    public string? Compilation { get; init; }

    /// <summary>
    /// MusicBrainz id of the artist (a single id, or several separated by <c>/</c>).
    /// </summary>
    public string? MusicBrainzArtistId { get; init; }

    public string? MusicBrainzAlbumArtistId { get; init; }

    /// <summary>
    /// MusicBrainz id of the release (the album).
    /// </summary>
    public string? MusicBrainzAlbumId { get; init; }

    /// <summary>
    /// The arguments of ffmpeg; the MusicBrainz ids are written under the names TagLib reads for MP3 files (ID3v2
    /// TXXX frames) and FLAC or Ogg files (Vorbis comments), the only formats tests write them to.
    /// </summary>
    internal IEnumerable<string> ToArguments(AudioFormat? format = null)
    {
        bool id3 = format == AudioFormat.Mp3;
        (string Key, string? Value)[] values =
        [
            ("title", Title),
            ("artist", Artist),
            ("album", Album),
            ("album_artist", AlbumArtist),
            ("track", Track),
            ("disc", Disc),
            ("date", Date),
            ("genre", Genre),
            ("REPLAYGAIN_TRACK_GAIN", ReplayGainTrackGain),
            ("REPLAYGAIN_TRACK_PEAK", ReplayGainTrackPeak),
            ("REPLAYGAIN_ALBUM_GAIN", ReplayGainAlbumGain),
            ("REPLAYGAIN_ALBUM_PEAK", ReplayGainAlbumPeak),
            ("compilation", Compilation),
            (id3 ? "MusicBrainz Artist Id" : "MUSICBRAINZ_ARTISTID", MusicBrainzArtistId),
            (id3 ? "MusicBrainz Album Artist Id" : "MUSICBRAINZ_ALBUMARTISTID", MusicBrainzAlbumArtistId),
            (id3 ? "MusicBrainz Album Id" : "MUSICBRAINZ_ALBUMID", MusicBrainzAlbumId),
        ];

        foreach ((string key, string? value) in values)
        {
            if (value is not null)
            {
                yield return "-metadata";
                yield return $"{key}={value}";
            }
        }
    }
}
