using WaveMP.Core.Organizing.Templates;

namespace WaveMP.Core.Organizing.Previews;

/// <summary>
/// Where the template puts one song of the preview.
/// </summary>
/// <param name="TrackId">The track of the library, or <c>null</c> for an example song.</param>
/// <param name="Path">The folders and the base name.</param>
/// <param name="Extension">Extension of the file the song plays (without dot), or <c>null</c>.</param>
public sealed record TemplatePreviewPath(Guid? TrackId, RenderedPath Path, string? Extension);
