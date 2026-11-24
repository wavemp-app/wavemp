using WaveMP.Core.Organizing.Templates;

namespace WaveMP.Core.Organizing.Previews;

/// <summary>
/// An example song of a client for the preview of a layout template, e.g. when the library is still empty.
/// </summary>
/// <param name="Values">The values of the example song.</param>
/// <param name="Extension">Extension of its file without dot, or <c>null</c>.</param>
public sealed record TemplateSample(TemplateValues Values, string? Extension);
