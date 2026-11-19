namespace WaveMP.Service.Dtos;

/// <summary>
/// The number of problems of a kind.
/// </summary>
public sealed class LibraryIssueCountDto
{
    public required LibraryIssueTypeDto Type { get; init; }

    public required LibraryIssueSeverityDto Severity { get; init; }

    public required int Count { get; init; }
}
