using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Asks for the preview of an organize operation.
/// </summary>
public sealed class CreateOrganizePreviewDto
{
    /// <summary>
    /// What to organize.
    /// </summary>
    [Required]
    public required OrganizePreviewKindDto? Kind { get; init; }

    /// <summary>
    /// The own album to apply; required for the kind OwnAlbum.
    /// </summary>
    public Guid? OwnAlbumId { get; init; }

    /// <summary>
    /// The media source to reorganize for the kind Library; all sources if missing.
    /// </summary>
    public Guid? SourceId { get; init; }
}
