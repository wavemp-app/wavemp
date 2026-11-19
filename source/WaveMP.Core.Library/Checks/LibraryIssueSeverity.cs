namespace WaveMP.Core.Library.Checks;

/// <summary>
/// How much a problem of the library matters.
/// </summary>
public enum LibraryIssueSeverity
{
    /// <summary>
    /// Something the admin should fix, e.g. a missing cover or an unreadable file.
    /// </summary>
    Problem,

    /// <summary>
    /// Only a hint, e.g. a song without video; often nothing is wrong.
    /// </summary>
    Hint,
}
