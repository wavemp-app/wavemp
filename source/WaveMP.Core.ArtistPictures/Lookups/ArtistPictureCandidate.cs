using WaveMP.Data.Entities;

namespace WaveMP.Core.ArtistPictures.Lookups;

/// <summary>
/// A picture the online lookup found for an artist, with its credit. All texts are plain text, the addresses absolute
/// https addresses. The id is random and only valid for this artist until the lookup results expire.
/// </summary>
/// <param name="Id">Id of the candidate (32 hexadecimal digits).</param>
/// <param name="Provider">The service of the picture.</param>
/// <param name="MusicBrainzArtistId">The MusicBrainz artist the picture was found for.</param>
/// <param name="Width">Width of the original in pixels, if the service names it.</param>
/// <param name="Height">Height of the original in pixels, if the service names it.</param>
/// <param name="Author">Author of the picture.</param>
/// <param name="License">Short name of the license.</param>
/// <param name="LicenseUrl">Address of the license text.</param>
/// <param name="SourceUrl">Address of the page of the picture at the service.</param>
public sealed record ArtistPictureCandidate(
    string Id,
    ArtistPictureProvider Provider,
    Guid MusicBrainzArtistId,
    int? Width,
    int? Height,
    string? Author,
    string? License,
    string? LicenseUrl,
    string? SourceUrl);
