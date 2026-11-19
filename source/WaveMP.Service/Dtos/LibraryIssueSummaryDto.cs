namespace WaveMP.Service.Dtos;

/// <summary>
/// The overview of the library check: the number of problems per kind and when the check ran last.
/// </summary>
public sealed class LibraryIssueSummaryDto
{
    /// <summary>
    /// Every kind of problem with its number, also kinds without problems; the problems first, then the hints.
    /// </summary>
    public required IReadOnlyList<LibraryIssueCountDto> Counts { get; init; }

    /// <summary>
    /// Number of problems of severity Problem.
    /// </summary>
    public required int ProblemCount { get; init; }

    /// <summary>
    /// Number of problems of severity Hint.
    /// </summary>
    public required int HintCount { get; init; }

    /// <summary>
    /// When the last run of the check stored its result; null if the check never ran.
    /// </summary>
    public DateTimeOffset? CheckedAt { get; init; }

    /// <summary>
    /// Number of tracks the last run checked; null if the check never ran.
    /// </summary>
    public int? TrackCount { get; init; }
}
