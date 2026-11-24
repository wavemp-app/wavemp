namespace WaveMP.Service.Dtos;

/// <summary>
/// The credit of an online artist picture. All texts are plain text (never markup); clients show them as text. The
/// addresses are absolute https addresses.
/// </summary>
public sealed class ArtistPictureCreditDto
{
    public required ArtistPictureProviderDto Provider { get; init; }

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
