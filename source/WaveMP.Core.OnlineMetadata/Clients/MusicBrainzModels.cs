using System.Text.Json.Serialization;

namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// The answer of the recording search of the MusicBrainz API (<c>/ws/2/recording?query=…&amp;fmt=json</c>).
/// </summary>
public sealed class MusicBrainzRecordingList
{
    [JsonPropertyName("recordings")]
    public IReadOnlyList<MusicBrainzRecording> Recordings { get; init; } = [];
}

/// <summary>
/// The answer of the release search of the MusicBrainz API (<c>/ws/2/release?query=…&amp;fmt=json</c>).
/// </summary>
public sealed class MusicBrainzReleaseList
{
    [JsonPropertyName("releases")]
    public IReadOnlyList<MusicBrainzRelease> Releases { get; init; } = [];
}

/// <summary>
/// A recording (a song as recorded) with the releases it appears on.
/// </summary>
public sealed class MusicBrainzRecording
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>
    /// Relevance of the search, 0 to 100.
    /// </summary>
    [JsonPropertyName("score")]
    public int Score { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Length in milliseconds.
    /// </summary>
    [JsonPropertyName("length")]
    public long? Length { get; init; }

    [JsonPropertyName("artist-credit")]
    public IReadOnlyList<MusicBrainzArtistCredit> ArtistCredit { get; init; } = [];

    [JsonPropertyName("first-release-date")]
    public string? FirstReleaseDate { get; init; }

    [JsonPropertyName("releases")]
    public IReadOnlyList<MusicBrainzRelease> Releases { get; init; } = [];

    [JsonPropertyName("tags")]
    public IReadOnlyList<MusicBrainzTag> Tags { get; init; } = [];
}

/// <summary>
/// A release (an album as published). In the recording search its media contain only the track of the recording.
/// </summary>
public sealed class MusicBrainzRelease
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>
    /// Relevance of the release search, 0 to 100; missing in the recording search.
    /// </summary>
    [JsonPropertyName("score")]
    public int? Score { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Status such as <c>Official</c> or <c>Bootleg</c>.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>
    /// Date of the release: <c>YYYY</c>, <c>YYYY-MM</c> or <c>YYYY-MM-DD</c>.
    /// </summary>
    [JsonPropertyName("date")]
    public string? Date { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    /// <summary>
    /// The artists of the release; in the recording search only if they differ from those of the recording.
    /// </summary>
    [JsonPropertyName("artist-credit")]
    public IReadOnlyList<MusicBrainzArtistCredit>? ArtistCredit { get; init; }

    /// <summary>
    /// Number of tracks on all media.
    /// </summary>
    [JsonPropertyName("track-count")]
    public int? TrackCount { get; init; }

    [JsonPropertyName("media")]
    public IReadOnlyList<MusicBrainzMedium> Media { get; init; } = [];

    [JsonPropertyName("tags")]
    public IReadOnlyList<MusicBrainzTag> Tags { get; init; } = [];
}

/// <summary>
/// A medium of a release, e.g. a CD.
/// </summary>
public sealed class MusicBrainzMedium
{
    /// <summary>
    /// Number of the medium in the release (the disc number).
    /// </summary>
    [JsonPropertyName("position")]
    public int? Position { get; init; }

    /// <summary>
    /// Format such as <c>CD</c> or <c>Digital Media</c>.
    /// </summary>
    [JsonPropertyName("format")]
    public string? Format { get; init; }

    [JsonPropertyName("track-count")]
    public int? TrackCount { get; init; }

    /// <summary>
    /// Number of tracks before the first listed one, in the recording search.
    /// </summary>
    [JsonPropertyName("track-offset")]
    public int? TrackOffset { get; init; }

    /// <summary>
    /// The tracks; in the recording search only the one of the recording.
    /// </summary>
    [JsonPropertyName("track")]
    public IReadOnlyList<MusicBrainzTrack> Tracks { get; init; } = [];
}

/// <summary>
/// A track of a medium.
/// </summary>
public sealed class MusicBrainzTrack
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>
    /// The number as printed, e.g. <c>3</c> or <c>A3</c> on vinyl.
    /// </summary>
    [JsonPropertyName("number")]
    public string? Number { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("length")]
    public long? Length { get; init; }
}

/// <summary>
/// One artist of the artists of a recording or release, with the phrase that joins it to the next
/// (e.g. <c> &amp; </c>).
/// </summary>
public sealed class MusicBrainzArtistCredit
{
    /// <summary>
    /// The name as credited.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("joinphrase")]
    public string? JoinPhrase { get; init; }
}

/// <summary>
/// A tag of the users of MusicBrainz, often a genre.
/// </summary>
public sealed class MusicBrainzTag
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
