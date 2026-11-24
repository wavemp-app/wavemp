namespace WaveMP.Core.Settings.Templates;

/// <summary>
/// An optional section in square brackets, e.g. <c>[{DiscNumber}-]</c>: it vanishes as a whole when one of its
/// placeholders has no value. It holds only text and placeholders.
/// </summary>
public sealed record LayoutTemplateSection(IReadOnlyList<LayoutTemplatePart> Parts) : LayoutTemplatePart;
