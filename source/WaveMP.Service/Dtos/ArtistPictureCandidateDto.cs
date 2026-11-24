namespace WaveMP.Service.Dtos;

/// <summary>
/// A picture the online lookup found for an artist. Its preview comes from GetArtistPictureCandidatePreview, and
/// TakeOverArtistOnlinePicture stores it as the online picture of the artist. All texts are plain text (never markup)
/// and clients show them as text; the addresses are absolute https addresses.
/// </summary>
public sealed class ArtistPictureCandidateDto
{
    /// <summary>
    /// Id of the candidate (32 hexadecimal digits); only valid for this artist and only while the lookup results are
    /// kept (7 days by default).
    /// </summary>
    public required string Id { get; init; }

    public required ArtistPictureProviderDto Provider { get; init; }

    /// <summary>
    /// The MusicBrainz artist the picture was found for.
    /// </summary>
    public required Guid MusicBrainzArtistId { get; init; }

    /// <summary>
    /// Width of the original in pixels, if the service names it.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    /// Height of the original in pixels, if the service names it.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    /// Author of the picture as the service names it.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Short name of the license, e.g. CC BY-SA 4.0.
    /// </summary>
    public string? License { get; init; }

    /// <summary>
    /// Address of the license text.
    /// </summary>
    public string? LicenseUrl { get; init; }

    /// <summary>
    /// Address of the page of the picture at the service.
    /// </summary>
    public string? SourceUrl { get; init; }
}
