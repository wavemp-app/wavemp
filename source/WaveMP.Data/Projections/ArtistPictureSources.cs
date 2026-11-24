namespace WaveMP.Data.Projections;

/// <summary>
/// The picture sources of an artist for the delivery of its pictures.
/// </summary>
/// <param name="ArtistId">Id of the artist.</param>
/// <param name="LocalFile">The picture file in the folder of the artist, if the scan found one.</param>
/// <param name="Uploaded">The uploaded picture, if there is one.</param>
/// <param name="Online">The picture taken over from the online lookup, if there is one.</param>
public sealed record ArtistPictureSources(
    Guid ArtistId,
    ArtistLocalPictureRow? LocalFile,
    ArtistStoredPictureRow? Uploaded,
    ArtistStoredPictureRow? Online);
