using System.Diagnostics.CodeAnalysis;

namespace WaveMP.Core.Settings.Templates;

/// <summary>
/// The parsed template, or the problems why it cannot be used.
/// </summary>
public sealed record LayoutTemplateParseResult(LayoutTemplate? Template, IReadOnlyList<LayoutTemplateError> Errors)
{
    [MemberNotNullWhen(true, nameof(Template))]
    public bool IsValid => Template is not null;
}
