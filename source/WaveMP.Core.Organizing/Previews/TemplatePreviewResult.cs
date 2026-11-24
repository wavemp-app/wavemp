using WaveMP.Core.Settings.Templates;

namespace WaveMP.Core.Organizing.Previews;

/// <summary>
/// The preview of a layout template: whether it is valid, its problems, and where it puts the songs.
/// </summary>
/// <param name="IsValid">Whether the template can be saved.</param>
/// <param name="IsFolderBased">Whether the valid template lays the songs out in folders.</param>
/// <param name="Errors">The problems of an invalid template.</param>
/// <param name="Paths">The paths of the songs; empty for an invalid template.</param>
public sealed record TemplatePreviewResult(
    bool IsValid,
    bool IsFolderBased,
    IReadOnlyList<LayoutTemplateError> Errors,
    IReadOnlyList<TemplatePreviewPath> Paths);
