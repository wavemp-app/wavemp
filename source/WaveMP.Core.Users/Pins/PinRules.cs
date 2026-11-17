using System.Diagnostics.CodeAnalysis;

namespace WaveMP.Core.Users.Pins;

/// <summary>
/// Rules for PINs: 4 to 8 digits from 0 to 9, nothing else.
/// </summary>
public static class PinRules
{
    public const int MinLength = 4;
    public const int MaxLength = 8;

    /// <summary>
    /// Pattern of a valid PIN for the validation of requests.
    /// </summary>
    public const string Pattern = "^[0-9]{4,8}$";

    public static bool IsValid([NotNullWhen(true)] string? pin)
    {
        return pin is { Length: >= MinLength and <= MaxLength } && pin.All(char.IsAsciiDigit);
    }
}
