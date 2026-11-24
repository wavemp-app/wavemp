namespace WaveMP.Core.Settings.Validations;

/// <summary>
/// Why a value of a setting was rejected.
/// </summary>
public enum SettingsErrorReason
{
    /// <summary>
    /// A number outside the allowed range.
    /// </summary>
    OutOfRange,

    /// <summary>
    /// An entry of a list has an invalid form.
    /// </summary>
    Invalid,

    /// <summary>
    /// An extension is in more than one list, or is the extension of lyrics files (<c>lrc</c>).
    /// </summary>
    Overlap,

    /// <summary>
    /// A list has too many entries.
    /// </summary>
    TooMany,

    /// <summary>
    /// A list or text must not be empty (audio and video extensions together, the layout template).
    /// </summary>
    Empty,

    /// <summary>
    /// A text is longer than allowed.
    /// </summary>
    TooLong,
}
