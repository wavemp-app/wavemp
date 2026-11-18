namespace WaveMP.Service.Dtos;

/// <summary>
/// The preview picture of one variant of a theme.
/// </summary>
public sealed class ThemePreviewDto
{
    public required ThemeVariantDto Variant { get; init; }

    /// <summary>
    /// Address of the picture (SVG, PNG or WebP), relative to the root of the server. It needs no session and never
    /// changes its content.
    /// </summary>
    public required string Url { get; init; }
}
