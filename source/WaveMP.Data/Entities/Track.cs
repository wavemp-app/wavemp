namespace WaveMP.Data.Entities;

/// <summary>
/// A track: all media files with the same base name in the same folder (e.g. <c>Song.flac</c>, <c>Song.mkv</c> and
/// <c>Song.jpg</c>), with its metadata from tags, folder and file names. When its folder is renamed or moved or its
/// files are renamed, a scan that sees the old and the new place keeps the track at the new place (see
/// <c>TrackMovePlanner</c> in the scanning project): its <see cref="Id"/> and everything that refers to it stay.
/// </summary>
public class Track
{
    /// <summary>
    /// Id of the track. It stays when a scan finds the track at a new place, so favorites, ratings, plays, playlist
    /// entries, saved queues and the ids clients keep remain valid.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The media source the files of the track lie in.
    /// </summary>
    public required Guid SourceId { get; set; }

    public MediaSource? Source { get; set; }

    /// <summary>
    /// Folder relative to the folder of the source with <c>/</c> as separator; empty for the folder of the source
    /// itself.
    /// </summary>
    public required string DirectoryPath { get; set; }

    /// <summary>
    /// File name without extension, as on disk.
    /// </summary>
    public required string BaseName { get; set; }

    /// <summary>
    /// Upper-case form of <see cref="BaseName"/>; files are grouped without regard to case.
    /// </summary>
    public required string NormalizedBaseName { get; set; }

    public required string Title { get; set; }

    /// <summary>
    /// The key the library search finds the track by (see <see cref="Texts.SearchKeys"/>). Set from
    /// <see cref="Title"/> whenever the track is saved; never shown.
    /// </summary>
    public string SearchKey { get; set; } = string.Empty;

    /// <summary>
    /// The first artist of the track (position 0 of <see cref="ArtistCredits"/>), kept with the track: it sorts and
    /// groups the songs and serves the fields that name one artist. The scan and the editor keep it equal to the first
    /// credit.
    /// </summary>
    public Guid? ArtistId { get; set; }

    public Artist? Artist { get; set; }

    /// <summary>
    /// All artists of the track in their order, each with its role (main or featured artist).
    /// </summary>
    public ICollection<TrackArtistCredit> ArtistCredits { get; set; } = [];

    public Guid? AlbumId { get; set; }

    public Album? Album { get; set; }

    public int? TrackNumber { get; set; }

    public int? DiscNumber { get; set; }

    public int? Year { get; set; }

    /// <summary>
    /// The genre of the track (its first genre tag), by id.
    /// </summary>
    public Guid? GenreId { get; set; }

    public Genre? Genre { get; set; }

    /// <summary>
    /// Duration of the primary audio file, otherwise of the primary video file.
    /// </summary>
    public TimeSpan? Duration { get; set; }

    public required bool HasAudio { get; set; }

    public required bool HasVideo { get; set; }

    /// <summary>
    /// Where the title comes from.
    /// </summary>
    public required TrackMetadataSource MetadataSource { get; set; }

    /// <summary>
    /// When the track was found for the first time; never changed, also not when a scan finds the track at a new place
    /// (basis of "recently added").
    /// </summary>
    public required DateTimeOffset FirstSeenAt { get; set; }

    public required DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Where the song starts in the primary audio file, in milliseconds, as the admin corrected it; <c>null</c> takes
    /// the start the song analysis found (see <see cref="MediaFileAnalysis"/>). Neither a scan nor an analysis ever
    /// changes it; it stays with the track when its files move.
    /// </summary>
    public int? AudioSongStartMilliseconds { get; set; }

    /// <summary>
    /// Where the song ends in the primary audio file, in milliseconds, as the admin corrected it; <c>null</c> takes the
    /// end the song analysis found.
    /// </summary>
    public int? AudioSongEndMilliseconds { get; set; }

    /// <summary>
    /// Where the song starts in the primary video, in milliseconds, as the admin corrected it: the position of the
    /// video at which the audio file is at its song start. Negative if the video only starts after the beginning of
    /// the song. <c>null</c> takes the start the song analysis found. Neither a scan nor an analysis ever changes it.
    /// It replaces the former offset of the video against the audio, which had the same meaning.
    /// </summary>
    public int? VideoSongStartMilliseconds { get; set; }

    /// <summary>
    /// Where the song ends in the primary video, in milliseconds, as the admin corrected it; <c>null</c> takes the end
    /// the song analysis found.
    /// </summary>
    public int? VideoSongEndMilliseconds { get; set; }

    /// <summary>
    /// The cover rule of the track, an exception the admin set: whether it shows its own cover or the cover of its
    /// album first, or only its own. <c>null</c> inherits the rule of its album, and that one the global rule. A scan
    /// never changes it. Stored as its name.
    /// </summary>
    public CoverPriority? CoverPriority { get; set; }

    /// <summary>
    /// When an admin marked the metadata of the track as reviewed; <c>null</c> while the track is unreviewed. New
    /// tracks start unreviewed; a scan never changes it.
    /// </summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>
    /// The profile that marked the track as reviewed; <c>null</c> while unreviewed or after the profile was deleted.
    /// </summary>
    public Guid? ReviewedByUserId { get; set; }

    public User? ReviewedBy { get; set; }

    public ICollection<MediaFile> Files { get; set; } = [];

    public ICollection<Cover> Covers { get; set; } = [];

    /// <summary>
    /// The files the admin selected instead of the automatic choice by quality, at most one per category.
    /// </summary>
    public ICollection<TrackFileSelection> FileSelections { get; set; } = [];
}
