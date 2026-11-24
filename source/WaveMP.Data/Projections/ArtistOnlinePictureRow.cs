using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// The online picture of an artist with its credit; all texts are plain text, addresses absolute https addresses.
/// </summary>
/// <param name="Id">Id of the picture.</param>
/// <param name="Provider">The service the picture came from.</param>
/// <param name="Author">Author as the service names it.</param>
/// <param name="License">Short name of the license.</param>
/// <param name="LicenseUrl">Address of the license text.</param>
/// <param name="SourceUrl">Address of the page of the picture at the service.</param>
public sealed record ArtistOnlinePictureRow(
    Guid Id,
    ArtistPictureProvider Provider,
    string? Author,
    string? License,
    string? LicenseUrl,
    string? SourceUrl);
