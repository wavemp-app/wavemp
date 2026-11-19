namespace WaveMP.Core.Library.Options;

/// <summary>
/// Settings of the library check (configuration section <c>LibraryCheck</c>, e.g.
/// <c>LibraryCheck__DuplicateDurationTolerance</c>).
/// </summary>
public sealed class LibraryCheckOptions
{
    public const string SectionName = "LibraryCheck";

    /// <summary>
    /// Largest difference of the durations of two tracks with the same artist and title that still counts as possible
    /// duplicate. Different recordings of a song (live, remix) usually differ more.
    /// </summary>
    public TimeSpan DuplicateDurationTolerance { get; set; } = TimeSpan.FromSeconds(5);
}
