using Microsoft.AspNetCore.Mvc;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Problem Details (RFC 9457) of a rejected theme package (422, code <c>theme.invalid</c>). Instead of texts, every
/// error names a machine-readable code with the file, line and limits it concerns; clients translate them.
/// </summary>
public sealed class ThemeProblemDto : ProblemDetails
{
    /// <summary>
    /// The errors, at least one and at most 50, by file and line.
    /// </summary>
    public required IReadOnlyList<ThemeErrorDto> Errors { get; init; }
}
