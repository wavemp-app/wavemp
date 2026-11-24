namespace WaveMP.Core.Settings.Templates;

/// <summary>
/// A part of a folder or file name of a layout template: literal text (<see cref="LayoutTemplateText"/>), a
/// placeholder (<see cref="LayoutTemplatePlaceholder"/>) or an optional section (<see cref="LayoutTemplateSection"/>).
/// </summary>
public abstract record LayoutTemplatePart;
