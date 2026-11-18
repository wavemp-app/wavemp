namespace WaveMP.Service.Dtos;

/// <summary>
/// A theme an admin uploaded. The themes shipped with WaveMP are part of the clients and not listed here.
/// </summary>
public sealed class ThemeDto
{
    /// <summary>
    /// Id of the theme from its manifest: lowercase letters, digits and single hyphens. Profiles choose it in their
    /// settings (<c>theme.id</c>).
    /// </summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Author { get; init; }

    /// <summary>
    /// Version of the theme as its author names it, e.g. <c>1.2.0</c>.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Version of the theme API the theme is written for.
    /// </summary>
    public required int ThemeApi { get; init; }

    /// <summary>
    /// The variants of the theme, one or both; the first one is shown where the appearance of a profile has no say.
    /// </summary>
    public required IReadOnlyList<ThemeVariantDto> Variants { get; init; }

    /// <summary>
    /// Address of the CSS file of the theme, relative to the root of the server. It needs no session; its content
    /// never changes, a new version of the theme has a new address.
    /// </summary>
    public required string StylesheetUrl { get; init; }

    /// <summary>
    /// The preview picture of each variant.
    /// </summary>
    public required IReadOnlyList<ThemePreviewDto> Previews { get; init; }

    /// <summary>
    /// Size of all files of the theme in bytes.
    /// </summary>
    public required long SizeBytes { get; init; }

    /// <summary>
    /// When this version of the theme was installed.
    /// </summary>
    public required DateTimeOffset InstalledAt { get; init; }
}
