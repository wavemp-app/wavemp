using WaveMP.Data.Entities;

namespace WaveMP.Core.Library.Checks;

/// <summary>
/// The number of problems of a kind.
/// </summary>
/// <param name="Type">The kind of problem.</param>
/// <param name="Severity">How much the kind matters.</param>
/// <param name="Count">Number of problems of the kind.</param>
public sealed record LibraryIssueCount(LibraryIssueType Type, LibraryIssueSeverity Severity, int Count);
