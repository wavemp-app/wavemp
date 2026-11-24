namespace WaveMP.Core.Organizing.Previews;

/// <summary>
/// What a preview of a layout template shows: the given tracks of the library, the given example songs, or, without
/// both, the first songs of the library (see <see cref="TemplatePreviewService.DefaultSongCount"/>).
/// </summary>
/// <param name="Template">The layout template to try.</param>
/// <param name="TrackIds">Tracks of the library, at most <see cref="TemplatePreviewService.MaxSongs"/>; unknown ids
/// are left out.</param>
/// <param name="Samples">Example songs, at most <see cref="TemplatePreviewService.MaxSongs"/>.</param>
public sealed record TemplatePreviewRequest(
    string? Template,
    IReadOnlyList<Guid>? TrackIds = null,
    IReadOnlyList<TemplateSample>? Samples = null);
