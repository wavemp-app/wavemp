namespace WaveMP.Core.Organizing.Previews;

/// <summary>
/// Shows where a layout template would put songs, without touching any file.
/// </summary>
public interface ITemplatePreviewService
{
    Task<TemplatePreviewResult> PreviewAsync(
        TemplatePreviewRequest request,
        CancellationToken cancellationToken = default);
}
