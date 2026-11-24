namespace WaveMP.Core.Settings.Templates;

/// <summary>
/// A value of the song, e.g. <c>{TrackNumber:00}</c>.
/// </summary>
/// <param name="Field">The value.</param>
/// <param name="Width">For numbers: the number of digits it is padded to with zeros (1 to
/// <see cref="LayoutTemplateSyntax.MaxNumberWidth"/>), otherwise <c>null</c>.</param>
public sealed record LayoutTemplatePlaceholder(TemplateField Field, int? Width = null) : LayoutTemplatePart;
