namespace WaveMP.Core.ArtistPictures.Lookups;

/// <summary>
/// Outcome of a step of the online lookup of artist pictures.
/// </summary>
public enum ArtistLookupStatus
{
    /// <summary>
    /// The step succeeded.
    /// </summary>
    Found,

    /// <summary>
    /// The artist does not exist.
    /// </summary>
    ArtistNotFound,

    /// <summary>
    /// The online lookup is switched off; no request was sent.
    /// </summary>
    Disabled,

    /// <summary>
    /// A service cannot be reached, failed, kept refusing requests or answered something that cannot be used.
    /// </summary>
    Unavailable,

    /// <summary>
    /// No MusicBrainz artist was given and none is remembered for the artist.
    /// </summary>
    MusicBrainzArtistMissing,

    /// <summary>
    /// The candidate does not exist, expired or belongs to another artist.
    /// </summary>
    CandidateNotFound,

    /// <summary>
    /// Pictures cannot be processed: ffmpeg is missing or the data folder or the cache cannot be written.
    /// </summary>
    ProcessingUnavailable,
}
