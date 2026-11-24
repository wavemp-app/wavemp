namespace WaveMP.Service.Dtos;

/// <summary>
/// One file of an organize item. Without fromPath the cover of an own album is written from the server; without
/// toPath a file the undone operation wrote is removed.
/// </summary>
public sealed class OrganizeFileMoveDto
{
    public required Guid Id { get; init; }

    public required OrganizeFileKindDto FileKind { get; init; }

    /// <summary>
    /// Path before, relative to the folder of the source.
    /// </summary>
    public string? FromPath { get; init; }

    /// <summary>
    /// Path afterwards, relative to the folder of the source.
    /// </summary>
    public string? ToPath { get; init; }

    public required OrganizeMoveStatusDto Status { get; init; }
}
