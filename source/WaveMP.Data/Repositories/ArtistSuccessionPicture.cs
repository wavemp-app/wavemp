namespace WaveMP.Data.Repositories;

/// <summary>
/// A stored artist picture (uploaded or online) that moves to a main successor.
/// </summary>
internal sealed record ArtistSuccessionPicture(Guid PictureId, Guid ArtistId);
