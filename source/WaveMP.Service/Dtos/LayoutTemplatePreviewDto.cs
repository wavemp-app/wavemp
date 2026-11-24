namespace WaveMP.Service.Dtos;

/// <summary>
/// The preview of a layout template. Nothing on disk is touched.
/// </summary>
public sealed class LayoutTemplatePreviewDto
{
    /// <summary>
    /// Whether the template can be saved.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Whether the valid template lays the songs out in folders; <c>false</c> for an invalid one.
    /// </summary>
    public required bool IsFolderBased { get; init; }

    /// <summary>
    /// The problems of an invalid template; empty for a valid one.
    /// </summary>
    public required IReadOnlyList<LayoutTemplateErrorDto> Errors { get; init; }

    /// <summary>
    /// Where the template puts the songs: first the tracks, then the example songs; empty for an invalid template.
    /// </summary>
    public required IReadOnlyList<LayoutTemplatePathDto> Paths { get; init; }
}
