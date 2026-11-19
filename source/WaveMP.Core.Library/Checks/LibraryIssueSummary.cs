namespace WaveMP.Core.Library.Checks;

/// <summary>
/// The overview of the library check.
/// </summary>
/// <param name="Counts">The number of problems of every kind, also of kinds without problems, in the order of
/// <see cref="LibraryIssueTypes.All"/>.</param>
/// <param name="CheckedAt">When the last run stored its result; <c>null</c> if the check never ran.</param>
/// <param name="TrackCount">Tracks the last run checked; <c>null</c> if the check never ran.</param>
public sealed record LibraryIssueSummary(
    IReadOnlyList<LibraryIssueCount> Counts,
    DateTimeOffset? CheckedAt,
    int? TrackCount);
