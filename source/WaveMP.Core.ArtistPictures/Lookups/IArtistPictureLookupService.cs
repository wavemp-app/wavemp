using WaveMP.Core.Caching.Images;

namespace WaveMP.Core.ArtistPictures.Lookups;

/// <summary>
/// The online lookup of artist pictures, for admins: (1) the MusicBrainz artists that may be the artist, (2) the
/// pictures of the chosen one on Wikimedia Commons (through its Wikidata item) and, with the API key of the admin, on
/// fanart.tv, (3) a preview, (4) taking one over as the online picture of the artist. While the lookup is switched off,
/// every step answers <see cref="ArtistLookupStatus.Disabled"/> without any request. Answers of the services are kept
/// in the cache (area <c>artist-lookups</c>) for the lookup cache duration; candidates are only referenced by random
/// ids, never by an address a client sends.
/// </summary>
public interface IArtistPictureLookupService
{
    /// <summary>
    /// Most matches of one page.
    /// </summary>
    public const int MaxMatchLimit = 25;

    /// <summary>
    /// Searches MusicBrainz for artists with the name of the artist.
    /// </summary>
    Task<ArtistMatchesResult> FindMatchesAsync(
        Guid artistId,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The pictures of the MusicBrainz artist (the remembered one if none is given). Writes nothing to the artist.
    /// </summary>
    Task<ArtistCandidatesResult> FindCandidatesAsync(
        Guid artistId,
        Guid? musicBrainzArtistId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The preview of a candidate as WebP of 300 or 500 pixels (the size rounded up), keeping its shape.
    /// </summary>
    Task<ArtistCandidatePreview> GetPreviewAsync(
        Guid artistId,
        string candidateId,
        int? size,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the picture of the candidate in full size, crops it to the section (the center square without one),
    /// stores it as the online picture of the artist with its credit, replacing an earlier one, and remembers the
    /// MusicBrainz artist for the next lookup.
    /// </summary>
    Task<ArtistLookupStatus> TakeOverAsync(
        Guid artistId,
        string candidateId,
        CoverCrop? crop,
        CancellationToken cancellationToken = default);
}
