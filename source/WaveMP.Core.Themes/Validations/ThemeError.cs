namespace WaveMP.Core.Themes.Validations;

/// <summary>
/// One reason why a theme package is rejected.
/// </summary>
/// <param name="Code">What is wrong.</param>
/// <param name="Path">The file in the package it concerns, if any.</param>
/// <param name="Line">The line in that file, from 1, if known.</param>
/// <param name="Field">The member of <c>manifest.json</c> it concerns, if any.</param>
/// <param name="File">A further file, e.g. the missing file an address of the CSS file names.</param>
/// <param name="Min">Lower limit, e.g. the oldest supported theme API version.</param>
/// <param name="Max">Upper limit, e.g. the largest allowed size in bytes.</param>
public sealed record ThemeError(
    ThemeErrorCode Code,
    string? Path = null,
    int? Line = null,
    string? Field = null,
    string? File = null,
    long? Min = null,
    long? Max = null);
