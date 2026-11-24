namespace WaveMP.Core.Settings.Templates;

/// <summary>
/// Text that is taken as it is (after the replacement of characters that file names cannot hold).
/// </summary>
public sealed record LayoutTemplateText(string Text) : LayoutTemplatePart;
