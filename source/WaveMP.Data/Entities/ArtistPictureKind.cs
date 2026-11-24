namespace WaveMP.Data.Entities;

/// <summary>
/// Where a stored artist picture came from. The picture in the artist's folder is not stored as an
/// <see cref="ArtistPicture"/>; the scan links it through <see cref="Artist.LocalPictureFileId"/>.
/// </summary>
public enum ArtistPictureKind
{
    /// <summary>
    /// Uploaded by an admin.
    /// </summary>
    Uploaded,

    /// <summary>
    /// Taken over by an admin from the online lookup.
    /// </summary>
    Online,
}
