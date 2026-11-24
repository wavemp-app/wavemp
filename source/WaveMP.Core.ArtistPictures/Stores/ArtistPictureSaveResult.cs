namespace WaveMP.Core.ArtistPictures.Stores;

/// <summary>
/// Outcome of storing an artist picture with the size of its largest stored file.
/// </summary>
/// <param name="Status">Whether the picture was stored.</param>
/// <param name="Width">Width of the largest stored size in pixels; 0 if not stored.</param>
/// <param name="Height">Height of the largest stored size in pixels; 0 if not stored.</param>
public sealed record ArtistPictureSaveResult(ArtistPictureSaveStatus Status, int Width = 0, int Height = 0)
{
    public static ArtistPictureSaveResult Failed(ArtistPictureSaveStatus status)
    {
        return new ArtistPictureSaveResult(status);
    }
}
