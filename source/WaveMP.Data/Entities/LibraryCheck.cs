namespace WaveMP.Data.Entities;

/// <summary>
/// The last completed run of the library check: one row (id <see cref="SingletonId"/>), replaced together with the
/// problems by every run.
/// </summary>
public class LibraryCheck
{
    /// <summary>
    /// The id of the only row.
    /// </summary>
    public const int SingletonId = 1;

    public int Id { get; set; }

    /// <summary>
    /// When the run stored its result.
    /// </summary>
    public required DateTimeOffset CheckedAt { get; set; }

    /// <summary>
    /// Number of tracks the run checked.
    /// </summary>
    public required int TrackCount { get; set; }
}
