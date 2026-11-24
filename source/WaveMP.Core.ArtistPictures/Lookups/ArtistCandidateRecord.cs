using WaveMP.Data.Entities;

namespace WaveMP.Core.ArtistPictures.Lookups;

/// <summary>
/// A candidate of the online lookup as kept in the cache: bound to its artist, with the addresses of its picture that
/// were found by the service (requested later only through the host allowlist of the sender) and its credit as plain
/// text.
/// </summary>
/// <param name="Id">Random id of the candidate (32 hexadecimal digits).</param>
/// <param name="ArtistId">The artist the candidate was found for; no other artist can use it.</param>
/// <param name="Provider">The service of the picture.</param>
/// <param name="MusicBrainzArtistId">The MusicBrainz artist the picture was found for.</param>
/// <param name="FileName">Name of the file on Wikimedia Commons; the full picture is requested by it.</param>
/// <param name="ImageUrl">Address of the full picture at fanart.tv.</param>
/// <param name="PreviewUrl">Address of the picture the preview is made of.</param>
/// <param name="Width">Width of the original in pixels, if known.</param>
/// <param name="Height">Height of the original in pixels, if known.</param>
/// <param name="Author">Author as plain text.</param>
/// <param name="License">Short name of the license as plain text.</param>
/// <param name="LicenseUrl">Address of the license text (https).</param>
/// <param name="SourceUrl">Address of the page of the picture (https).</param>
internal sealed record ArtistCandidateRecord(
    string Id,
    Guid ArtistId,
    ArtistPictureProvider Provider,
    Guid MusicBrainzArtistId,
    string? FileName,
    string? ImageUrl,
    string PreviewUrl,
    int? Width,
    int? Height,
    string? Author,
    string? License,
    string? LicenseUrl,
    string? SourceUrl)
{
    public ArtistPictureCandidate ToCandidate()
    {
        return new ArtistPictureCandidate(
            Id,
            Provider,
            MusicBrainzArtistId,
            Width,
            Height,
            Author,
            License,
            LicenseUrl,
            SourceUrl);
    }
}
