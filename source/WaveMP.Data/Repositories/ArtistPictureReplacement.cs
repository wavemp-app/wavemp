namespace WaveMP.Data.Repositories;

/// <summary>
/// Outcome of storing a picture of an artist.
/// </summary>
/// <param name="Saved">Whether the picture was stored; <c>false</c> if the artist does not exist (any more).</param>
/// <param name="ReplacedPictureIds">The pictures of the same kind that were removed; their files can go.</param>
public sealed record ArtistPictureReplacement(bool Saved, IReadOnlyList<Guid> ReplacedPictureIds);
