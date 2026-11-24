using System.ComponentModel.DataAnnotations;
using WaveMP.Service.Validators;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Takes a picture of the online lookup over as the online picture of an artist.
/// </summary>
public sealed class TakeOverArtistPictureDto
{
    /// <summary>
    /// Id of a candidate of ListArtistPictureCandidates for this artist.
    /// </summary>
    [RequiredValue]
    [RegularExpression("^[0-9a-f]{32}$")]
    public required string CandidateId { get; init; }

    /// <summary>
    /// The square section to keep; without it, the largest square in the middle of the picture.
    /// </summary>
    public ArtistPictureCropDto? Crop { get; init; }
}
