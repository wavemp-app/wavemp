using System.Globalization;

namespace WaveMP.Core.ArtistPictures.Pictures;

/// <summary>
/// The versions of the pictures of an artist: they change whenever another picture is shown, so clients use them as
/// part of the key of a loaded picture, and they start the entity tags of the delivered pictures. <c>l-</c> names the
/// picture file in the artist folder with the time of its last change, <c>u-</c> an uploaded and <c>o-</c> an online
/// picture by its id (a new picture always gets a new id).
/// </summary>
public static class ArtistPictureVersions
{
    public static string Local(Guid mediaFileId, DateTimeOffset modifiedAt)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"l-{mediaFileId:N}-{modifiedAt.UtcTicks:x}");
    }

    public static string Uploaded(Guid pictureId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"u-{pictureId:N}");
    }

    public static string Online(Guid pictureId)
    {
        return string.Create(CultureInfo.InvariantCulture, $"o-{pictureId:N}");
    }
}
