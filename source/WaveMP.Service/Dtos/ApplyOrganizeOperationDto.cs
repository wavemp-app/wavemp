using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Confirms the preview of an organize operation, so its files are moved.
/// </summary>
public sealed class ApplyOrganizeOperationDto
{
    /// <summary>
    /// The operation in the state Previewed.
    /// </summary>
    [Required]
    public required Guid? OperationId { get; init; }

    /// <summary>
    /// Whether the songs with conflicts are left out; required to confirm a preview with conflicts.
    /// </summary>
    public bool SkipConflicts { get; init; }
}
