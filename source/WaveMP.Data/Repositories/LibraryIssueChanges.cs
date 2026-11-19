namespace WaveMP.Data.Repositories;

/// <summary>
/// How a run of the library check changed the stored problems.
/// </summary>
/// <param name="Added">Problems found for the first time.</param>
/// <param name="Removed">Problems that are gone.</param>
/// <param name="Kept">Problems found again; they keep their id and the time they were found first.</param>
public sealed record LibraryIssueChanges(int Added, int Removed, int Kept);
